// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Threading;

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.ISM;
using System.Collections.Generic;
using System.Text;
using nsCDEngine.Security;

namespace nsCDEngine.Communication
{
    internal partial class TheQueuedSender
    {
        private bool IsInPost;
        private TheREST MyREST;

        public void TheRESTSenderThread()
        {
            if (IsSenderThreadRunning) return;
            try
            {
                TheDiagnostics.SetThreadName($"QSender:{MyTargetNodeChannel} SenderThread", true);
                MyREST = new TheREST();
                IsSenderThreadRunning = true;
                StringBuilder tSendBufferStr = new StringBuilder(TheBaseAssets.MyServiceHostInfo?.IsMemoryOptimized == true ? 1024 : TheBaseAssets.MAX_MessageSize[(int)MyTargetNodeChannel.SenderType] * 2);
                TargetUri = TheCommonUtils.CUri(MyTargetNodeChannel.TargetUrl, true);
                TargetUriPath = TargetUri.AbsolutePath;

                int QDelay = TheCommonUtils.CInt(TheBaseAssets.MySettings.GetSetting("ThrottleWS"));
                if (QDelay < 2) QDelay = 2;
                if (MyTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON && TheBaseAssets.MyServiceHostInfo.WsJsThrottle > QDelay)
                    QDelay = TheBaseAssets.MyServiceHostInfo.WsJsThrottle;
                mre = new ManualResetEvent(false);
                if (eventSenderThreadRunning != null && TheBaseAssets.MasterSwitch)
                    TheCommonUtils.cdeRunAsync("EventSenderThreadRunning", true, (p) => { eventSenderThreadRunning(this); });
                MyISBlock?.FireEvent("SenderThreadCreated");
                while (TheBaseAssets.MasterSwitch && IsAlive && IsSenderThreadRunning)
                {
                    if (QDelay>2)
                        mre.WaitOne(QDelay);    //Throtteling should be here not in the wait loop
                    int WaitCounter = 0;
                    while (TheBaseAssets.MasterSwitch && IsAlive && IsSenderThreadRunning && ((MyCoreQueue.Count == 0 && IsConnected) || IsConnecting || IsInPost || MyREST.IsPosting > (IsConnected?TheBaseAssets.MyServiceHostInfo.ParallelPosts:0)))
                    {
                        // CODE REVIEW: This was seen looping after service route to local node was closed (due to node shutdown): IsCOnnecting/IsConnected/IsInPost were false; ISAlive true. MyREST.IsPosting was 8. Queue count ~345
                        //              This should be fixed now with IsSenderThreadRunning in the loop (I was wrong :) this happens only with cloud connections where POST fails and the IsPosting was not reset properly).
                        // Update: If found the same problem. Sometime IsPosting is not reduced properly when a Timeout or other error occured during Posting
                        if (IsInPost && MyCoreQueue.Count > 100)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(235, new TSM("QueuedSender", $"IsInPost was still on and has been reset for {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l2_Warning));
                            IsInPost = false;
                        }
                        if (!IsConnected)
                        {
                            if (IsConnecting && MyREST.IsPosting == 0)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(235, new TSM("QueuedSender", $"IsConnecting is set but no Pending Post {MyTargetNodeChannel?.ToMLString()} - resetting IsConnecting", eMsgLevel.l2_Warning));
                                IsConnecting = false;
                            }
                            if (!IsConnecting && MyREST.IsPosting > (IsConnected ? 10 : 0))
                            {
                                WaitCounter++;
                                if (WaitCounter > (10000 / QDelay))
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(235, new TSM("QueuedSender", $"MyREST.IsPosting ({MyREST.IsPosting}) was higher then expected...possible POST problem. Resetting POST Counter for {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l2_Warning));
                                    MyREST.IsPosting = 0;
                                }
                            }
                        }

                        mre.WaitOne(QDelay);
                    }
                    if (!TheBaseAssets.MasterSwitch || !IsAlive || !IsSenderThreadRunning)
                    {
                        IsInPost = false;
                        break;
                    }
                    IsInPost = true;
                    if (MyTargetNodeChannel.SenderType == cdeSenderType.CDE_BACKCHANNEL ||
                        MyTargetNodeChannel.SenderType == cdeSenderType.CDE_PHONE ||
                        MyTargetNodeChannel.SenderType == cdeSenderType.CDE_DEVICE ||
                        MyTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(235, new TSM("QueuedSender", $"QSender Should NEVER GO HERE because SenderType is {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l1_Error));
                        IsInPost = false;
                        IsSenderThreadRunning = false;
                        return;
                    }
                    //NEW3.124: Sender Loop has to terminate if Cloud was Disabled.
                    if (MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE && (TheBaseAssets.MyServiceHostInfo.IsCloudDisabled || string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ServiceRoute) || !TheBaseAssets.MyServiceHostInfo.HasServiceRoute(TargetUri)))  //ServiceRoute can contain multiple routes...
                        break;
                    TheCoreQueueContent tQueued = null;
                    int MCQCount = 0;
                    int IsBatchOn = 0;
#if CDE_NET35
                    tSendBufferStr = new StringBuilder(TheBaseAssets.MAX_MessageSize[(int)MyTargetNodeChannel.SenderType]*2);
#else
                    tSendBufferStr.Clear();
#endif
                    tSendBufferStr.Append("[");
                    byte[] BinSendBuffer = null;
                    do
                    {
                        if (!IsConnected)
                        {
                            tQueued = new TheCoreQueueContent();
                            IsConnecting = true;
                            ConnectRetries = 0;
                            if (CloudInRetry)
                            {
                                var count = Interlocked.Increment(ref CloudRetryCount);
                                if (count > 10)
                                {
                                    int delaySeconds = TheBaseAssets.MyServiceHostInfo.HeartbeatDelay * TheBaseAssets.MyServiceHostInfo.TO.HeartBeatRate;
                                    TheBaseAssets.MySYSLOG.WriteToLog(235, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"Too many retries for {MyTargetNodeChannel?.ToMLString()}: {count}. Waiting {delaySeconds} seconds.", eMsgLevel.l2_Warning));
                                    TheCommonUtils.SleepOneEye((uint)delaySeconds * 1000, 100);
                                    Interlocked.Exchange(ref CloudRetryCount, 0);
                                }
                                if (!TheBaseAssets.MasterSwitch || !IsAlive || !IsSenderThreadRunning || (MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE &&
                                    (TheBaseAssets.MyServiceHostInfo.IsCloudDisabled || string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ServiceRoute) || !TheBaseAssets.MyServiceHostInfo.HasServiceRoute(TargetUri))))
                                {
                                    //Very rare but required after Sleep
                                    throw new Exception("QSender had to be terminated in Cloud Retry due to disabling of ServiceRoute or C-DEngine Shutdown");
                                }
                            }//NEW3.124: Sender Loop has to terminate if Cloud was Disabled.
                            if (MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE && (TheBaseAssets.MyServiceHostInfo.IsCloudDisabled || string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ServiceRoute) || !TheBaseAssets.MyServiceHostInfo.HasServiceRoute(TargetUri)))
                                break;
                        }
                        else
                        {
                            if (CloudInRetry)
                            {
                                CloudInRetry = false;
                                IsInPost = false;
                                continue;
                            }
                            tQueued = GetNextMessage(out MCQCount);
                        }
                        if (tQueued != null)
                        {
                            TheCDEKPIs.IncrementKPI(eKPINames.QSSent);

                            if (tQueued.OrgMessage == null && !IsConnecting)
                            {
                                if (MySubscriptions.Count == 0)
                                {
                                    if (IsBatchOn == 0) //Only jump out if no telegrams are in batch..
                                    {
                                        IsInPost = false;
                                        continue;
                                    }
                                }
                                if (IsBatchOn == 0) //If telegrams are in the batch dont send short message
                                {
                                    BinSendBuffer = new byte[1];
                                    BinSendBuffer[0] = (byte)((int)(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType)).ToString()[0];
                                    break;
                                }
                            }
                            if (tQueued.OrgMessage != null && tQueued.OrgMessage.ToCloudOnly() && MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE)
                                tQueued.OrgMessage.SetToCloudOnly(false);

                            TheDeviceMessage tDev = new TheDeviceMessage
                            {
                                DID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString() // MyTargetNodeChannel.cdeMID.ToString();
                            };

                            if (MyTargetNodeChannel.MySessionState != null)
                            {
                                //BUGFIX: After fast Cloud Disconnect/reconnect-Relay lost Scopeing because it Scope was not properly stored in SessionState
                                if (string.IsNullOrEmpty(MyTargetNodeChannel.MySessionState.SScopeID) && TheBaseAssets.MyScopeManager.IsScopingEnabled)
                                    MyTargetNodeChannel.MySessionState.SScopeID = TheBaseAssets.MyScopeManager.GetScrambledScopeID();       //GRSI: rare

                                if (TheBaseAssets.MyServiceHostInfo.EnableFastSecurity)
                                    tDev.SID = MyTargetNodeChannel.MySessionState.SScopeID;
                                else
                                    tDev.SID = TheBaseAssets.MyScopeManager.GetScrambledScopeID(MyTargetNodeChannel.MySessionState.SScopeID, false); //GRSI: high frequency!
                            }
                            else
                                tDev.SID = TheBaseAssets.MyScopeManager.GetScrambledScopeID();      //GRSI: rare

                            tDev.MSG = tQueued.OrgMessage;
                            tDev.FID = "1";
                            if (IsConnecting)
                            {
                                tDev.TOP = TheCommCore.SetConnectingBufferStr(MyTargetNodeChannel, null);
                            }
                            else
                            {
                                tDev.TOP = tQueued.Topic;
                                tQueued.OrgMessage?.GetNextSerial(tQueued.SubMsgCnt);
                                if (MyTargetNodeChannel.MySessionState != null)
                                    tDev.FID = MyTargetNodeChannel.MySessionState.GetNextSerial().ToString(); //(SendCounter + 1).ToString();    //V3B3: New was empty //V4.1010: Same as WS now
                            }

                            #region Batch Serialization
                            IsBatchOn++;
                            if (MCQCount == 0 || IsBatchOn > TheBaseAssets.MyServiceHostInfo.MaxBatchedTelegrams || tQueued.IsChunked)
                            {
                                if (MCQCount != 0)
                                    tDev.CNT = MCQCount;
                                IsBatchOn = 0;
                            }
                            else
                            {
                                if (tSendBufferStr.Length > TheBaseAssets.MAX_MessageSize[(int)MyTargetNodeChannel.SenderType])
                                {
                                    tDev.CNT = MCQCount;
                                    IsBatchOn = 0;
                                }
                            }
                            if (tSendBufferStr.Length > 1) tSendBufferStr.Append(",");
                            tSendBufferStr.Append(TheCommonUtils.SerializeObjectToJSONString(tDev));
                            #endregion
                        }
                        else
                            IsBatchOn = 0;
                    } while (IsBatchOn > 0 && IsInPost && TheBaseAssets.MasterSwitch);
                    if ((!IsInPost || tSendBufferStr.Length < 2) && BinSendBuffer == null)
                    {
                        IsInPost = false;
                        continue;
                    }

                    string tMimeType = "application/octet-stream";
                    if (BinSendBuffer == null)
                    {
                        tSendBufferStr.Append("]");
                        if (MyTargetNodeChannel.SenderType == cdeSenderType.CDE_MINI || TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType == cdeSenderType.CDE_MINI)
                        {
                            tMimeType = "application/json";
                            BinSendBuffer = TheCommonUtils.CUTF8String2Array(tSendBufferStr.ToString());
                        }
                        else
                        {
                            tMimeType = "application/x-gzip";
                            BinSendBuffer = TheCommonUtils.cdeCompressString(tSendBufferStr.ToString());
                        }
                    }
                    if (MyTargetNodeChannel==null)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(235, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", $"Channel was deleted - exiting SenderThread", eMsgLevel.l2_Warning));
                        IsInPost = false;
                        break;
                    }
                    string ISBPath = new Uri(TargetUri, TheBaseAssets.MyScopeManager.GetISBPath(TargetUriPath, TheCommonUtils.GetOriginST(MyTargetNodeChannel), MyTargetNodeChannel.SenderType, MyTargetNodeChannel.MySessionState==null? 1: MyTargetNodeChannel.MySessionState.FID /*Interlocked.Increment(ref SendCounter)*/, (MyTargetNodeChannel.MySessionState == null || !IsConnected) ?Guid.Empty: MyTargetNodeChannel.MySessionState.cdeMID, MyTargetNodeChannel.IsWebSocket)).ToString(); //V3B4: changed from TheBaseAssets.MyServiceHostInfo.MyDeviceInfo
                    TheRequestData pData = new TheRequestData
                    {
                        RemoteAddress = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false),
                        RequestUri = new Uri(ISBPath),
                        PostData = BinSendBuffer,
                        ResponseMimeType = tMimeType,
                        DeviceID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                        HttpMethod = "POST"
                    };
                    if (!TheCertificates.SetClientCert(pData, MyTargetNodeChannel?.ClientCertificateThumb))
                    {
                        throw new Exception("Client Certificate could not be added");
                    }
                    MyREST.PostRESTAsync(pData, sinkUploadDataCompleted, FireSenderProblem);
                    if (BinSendBuffer != null)
                        TheCDEKPIs.IncrementKPI(eKPINames.QKBSent, BinSendBuffer.Length);

                    IsInPost = false;
                }
                FlushQueue();
                TheBaseAssets.MySYSLOG.WriteToLog(235, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"QSenderThread was closed for {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l6_Debug));
            }
            catch (Exception e)
            {
                IsInPost = false;
                TheBaseAssets.MySYSLOG.WriteToLog(235, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", "Exception in jcSenderThread.", eMsgLevel.l1_Error, "Error:" + e));
                if (MyTargetNodeChannel?.SenderType != cdeSenderType.CDE_BACKCHANNEL)
                    FireSenderProblem(new TheRequestData() { ErrorDescription = $"1308:{e}" });
            }
            finally
            {
                IsSenderThreadRunning = false;
                IsAlive = false;
                IsInPost = false;
                CloudInRetry = false;
                StopHeartBeat();
            }
        }

        Uri TargetUri;
        string TargetUriPath;

        void sinkUploadDataCompleted(TheRequestData eResult)
        {
            if (eResult != null && eResult.ResponseBuffer != null && eResult.ResponseBuffer.Length > 0)
            {
                try
                {
                    TheCDEKPIs.IncrementKPI(eKPINames.QSReceivedTSM);
                    TheCDEKPIs.IncrementKPI(eKPINames.QKBReceived, eResult.ResponseBuffer.Length);
                    if (eResult.StatusCode != 200)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(243, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", $"Server responded with not-ok ...Code: {eResult.StatusCode} error: {TheCommonUtils.CArray2UTF8String(eResult.ResponseBuffer)}", eMsgLevel.l2_Warning), true);
                        FireSenderProblem(new TheRequestData() { ErrorDescription = $"{eResult.StatusCode}:Server responded with not-ok ...Code: {eResult.StatusCode}" });
                        return;
                    }
                    List<TheDeviceMessage> tDevList = null;
                    if (eResult.ResponseMimeType.Contains("zip"))
                        tDevList = TheDeviceMessage.DeserializeJSONToObject(TheCommonUtils.cdeDecompressToString(eResult.ResponseBuffer));
                    else
                        tDevList = TheDeviceMessage.DeserializeJSONToObject(TheCommonUtils.CArray2UTF8String(eResult.ResponseBuffer));
                    if (tDevList != null && tDevList.Count > 0)
                    {
                        eResult.StatusCode = 0;
                        TheCorePubSub.ProcessClientDeviceMessage(this, eResult, tDevList);
                    }
                }
                catch (Exception ee)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(243, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"Error in {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l1_Error, ee.ToString()));
                }
            }
            else
            {
                if (MyTargetNodeChannel.SenderType != cdeSenderType.CDE_SERVICE)
                    TheBaseAssets.MySYSLOG.WriteToLog(243, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Did not receive any data from {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l6_Debug));
            }
        }
    }
}
