// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Threading;
using nsCDEngine.ISM;
using System.Text;

// ReSharper disable All

namespace nsCDEngine.Communication
{
    internal partial class TheQueuedSender
    {
        private bool IsInWSPost;

        internal void SetWebSocketProcessor(object pSocket)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(2370, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("WSQueuedSender", $"Setting WebSocketProcessor HasPSocket:{pSocket != null}", eMsgLevel.l4_Message));
            if (pSocket == null) return;
            if (MyTargetNodeChannel != null && MyREST == null)
            {
                MyTargetNodeChannel.IsWebSocket = true;
                if (string.IsNullOrEmpty(MyTargetNodeChannel.TargetUrl) || MyTargetNodeChannel.TargetUrl.StartsWith("/"))
                    MyTargetNodeChannel.TargetUrl = "WS://" + MyTargetNodeChannel.cdeMID;
            }
            if (MyWebSocketProcessor == null && MyREST == null)
            {
                MyWebSocketProcessor = pSocket as TheWSProcessorBase;
                if (MyWebSocketProcessor != null)
                {
                    MyWebSocketProcessor.eventClosed = null;
                    MyWebSocketProcessor.eventClosed += DoFireSenderProblem;
                    MyWebSocketProcessor.MyQSender = this;
                }
            }
            if (MyWebSocketProcessor != null && MyTargetNodeChannel != null)
            {
                MyWebSocketProcessor.OwnerNodeID = MyTargetNodeChannel.cdeMID;
                if (MyTargetNodeChannel.SenderType == cdeSenderType.CDE_BACKCHANNEL)
                {
                    //if this is a backchannel the connection was established by the incoming node and the QS is already connected
                    IsConnected = true;
                    IsConnecting = false;
                }
                else
                {
                    IsConnected = false;
                    IsConnecting = true;
                }
                if (!IsSenderThreadRunning)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2371, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("WSQueuedSender", $"Starting new WSSender Thread IsAlive:{IsAlive}", eMsgLevel.l4_Message));
                    MyWebSocketProcessor.IsActive = true;
                    TheCommonUtils.cdeRunTaskAsync($"WSQSender for ORG:{MyTargetNodeChannel}", o =>
                    {
                        if (IsSenderThreadRunning) return;  //In Case the Thread took longer to create and another one was successful in the meantime
                        TheWSSenderThread();
                        TheBaseAssets.MySYSLOG.WriteToLog(2372, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("WSQueuedSender", $"WSQSenderThread was closed (In RunAync) for {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l1_Error));
                        if (MyTargetNodeChannel?.SenderType != cdeSenderType.CDE_CLOUDROUTE)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(236, new TSM("WSQueuedSender", $"WSQSender Thread {MyTargetNodeChannel?.ToMLString()} died", eMsgLevel.l1_Error));
                            FireSenderProblem(new TheRequestData() { ErrorDescription = $"1309:WSQSender Thread {MyTargetNodeChannel?.ToMLString()} died" });
                            IsAlive = false;
                            IsInWSPost = false;
                        }
                    }, null, true);
                }
            }
        }
        private TheWSProcessorBase MyWebSocketProcessor;
        private void TheWSSenderThread()
        {
            if (IsSenderThreadRunning /*|| MyTargetNodeChannel==null*/) return;
            if (MyTargetNodeChannel == null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2371, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("WSQueuedSender", $"WSSender Thread could not be started because MTNC is null", eMsgLevel.l4_Message));
                return;
            }
            TheDiagnostics.SetThreadName($"QSender:{MyTargetNodeChannel} WSSenderThread", true);
            IsSenderThreadRunning = true;
            mre = new ManualResetEvent(false);

            CloudCounter++;
            StringBuilder tSendBufferStr = null;
            int QDelay = TheCommonUtils.CInt(TheBaseAssets.MySettings.GetSetting("ThrottleWS"));
            if (QDelay < 2) QDelay = 2;
            if (MyTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON && TheBaseAssets.MyServiceHostInfo.WsJsThrottle > QDelay)
                QDelay = TheBaseAssets.MyServiceHostInfo.WsJsThrottle;
            if (eventSenderThreadRunning != null && TheBaseAssets.MasterSwitch)
                TheCommonUtils.cdeRunAsync("EventSenderThreadRunning", true, (p) => { eventSenderThreadRunning(this); });
            MyISBlock?.FireEvent("SenderThreadCreated");
            try
            {
                while (TheBaseAssets.MasterSwitch && IsAlive && MyWebSocketProcessor.IsActive && IsSenderThreadRunning)
                {
                    while (TheBaseAssets.MasterSwitch && IsAlive && MyWebSocketProcessor.IsActive && IsSenderThreadRunning && (MyTargetNodeChannel.MySessionState == null || IsConnecting || IsInWSPost || !MyWebSocketProcessor.ProcessingAllowed || MyCoreQueue.Count == 0))
                    {
                        if (IsInWSPost && MyCoreQueue.Count > 200)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(235, new TSM("WSQueuedSender", $"IsInWSPost was still on and has been reset for {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l2_Warning));
                            IsInWSPost = false;
                        }
                        if (MyTargetNodeChannel.MySessionState == null && !IsConnecting && MyCoreQueue.Count > 0)
                            break;

                        tSendBufferStr = null;
                        mre.WaitOne(QDelay);
                    }
                    if (!TheBaseAssets.MasterSwitch || !IsAlive || !MyWebSocketProcessor.IsActive || MyTargetNodeChannel.MySessionState == null)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(235, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("WSQueuedSender", $"WSSender Thread Condition failed - Ending SenderThread IsAlive:{IsAlive} MyWSProccAlive:{(MyWebSocketProcessor != null && MyWebSocketProcessor.IsActive)},SessionState:{(MyTargetNodeChannel?.MySessionState != null)} IsConnecting:{IsConnecting} IsConnected:{IsConnected}", eMsgLevel.l2_Warning));
                        break;
                    }
                    IsInWSPost = true;

                    int MCQCount = 0;
                    int IsBatchOn = 0;
                    int FinalCnt = 0;

                    tSendBufferStr ??= new StringBuilder(TheBaseAssets.MyServiceHostInfo?.IsMemoryOptimized == true ? 1024 : TheBaseAssets.MAX_MessageSize[(int)MyTargetNodeChannel.SenderType] * 2);
                    tSendBufferStr.Append("[");
                    do
                    {
                        TheCoreQueueContent tQueued = GetNextMessage(out MCQCount);
                        if (tQueued != null)
                        {
                            TheDeviceMessage tDev = new ();
                            if (tQueued.OrgMessage != null)
                            {
                                if (tQueued.OrgMessage.ToCloudOnly() && MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE)
                                    tQueued.OrgMessage.SetToCloudOnly(false);

                                tQueued.OrgMessage.GetNextSerial(tQueued.SubMsgCnt);
                                tDev.MSG = tQueued.OrgMessage;
                            }
                            var tCurSessState = MyTargetNodeChannel.MySessionState;
                            if (MyTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON)
                            {
                                if (tDev.MSG == null)
                                {
                                    // we have a pickup: never send to browser
                                    if (IsBatchOn > 0)
                                    {
                                        continue;   //ignore HB as other messages are already in the queue
                                    }
                                    else
                                    {
                                        IsInWSPost = false;
                                        continue;   //dont sent empty Message to Browser - HB not necessary
                                    }
                                }
                                else
                                {
                                    tDev.MSG.SEID = null; //SECURITY: Don't send SEID to Browser
                                    tDev.MSG.UID = null;//SECURITY: Don't send SEID to Browser
                                    tDev.MSG.SID = null;//SECURITY: Don't send SID to Browser
                                }
                                tDev.DID = MyTargetNodeChannel.cdeMID.ToString();
                            }
                            else
                            {
                                if (tDev.MSG == null) // CODE REVIEW: DO we also need to detect other simple topics with response message and ensure they get into their own batch?
                                {
                                    // we have a pickup (only to be sent if nothing else is being sent)
                                    if (IsBatchOn > 0)
                                    {
                                        // Ignore pickups if another message is already being sent (can happen with race condition between check before pickup enqueue and dequeue here - another TSM could be enqueue in that time)
                                        continue;
                                    }
                                    else
                                    {
                                        // Close the batch here so that no other TSM gets into the same batch
                                        // Possible optimization: check if other TSMs are available and skip the pickup here as well
                                        IsBatchOn = -1; 
                                    }
                                }
                                tDev.DID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString();
                                if (!string.IsNullOrEmpty(tQueued?.OrgMessage?.SID))
                                    tDev.SID = tQueued.OrgMessage.SID;
                                else
                                {
                                    if (TheBaseAssets.MyServiceHostInfo.EnableFastSecurity)
                                        tDev.SID = tCurSessState.SScopeID;  //SECURITY: All tDevs will have same Session Scrambled ScopeID - 4.209: SID from TSM if set.
                                    else
                                        tDev.SID = TheBaseAssets.MyScopeManager.GetScrambledScopeID(tCurSessState.SScopeID, false);  //GRSI: high frequency
                                }
                            }

                            TheCDEKPIs.IncrementKPI(eKPINames.QSSent);
                            tDev.TOP = tQueued.Topic;
                            tDev.FID = tCurSessState.GetNextSerial().ToString();
                            if (TheCommonUtils.IsDeviceSenderType(MyTargetNodeChannel.SenderType))  //IDST-OK: Must create RSA for Devices
                            {
                                TheCommonUtils.CreateRSAKeys(tCurSessState);
                                if (TheBaseAssets.MyServiceHostInfo.SecurityLevel > 3)
                                    tDev.RSA = tCurSessState.RSAPublic;  //Security improvement possible: Make depending on switch HighSecurity = RSAGeneration every three seconds
                            }
                            if (MyTargetNodeChannel.SenderType != cdeSenderType.CDE_CLOUDROUTE) //CODE-REVIEW: 4.0113 is this still valid: Must not reset HB if talking to cloud due to Cloud-Disconnect issue
                                ResetHeartbeatTimer(false, tCurSessState);
                            if (!cdeSenderType.CDE_JAVAJASON.Equals(MyTargetNodeChannel.SenderType))
                                tDev.NPA = TheBaseAssets.MyScopeManager.GetISBPath(TheBaseAssets.MyServiceHostInfo.RootDir, MyTargetNodeChannel.SenderType, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType, tCurSessState.FID, tCurSessState.cdeMID, true);
                            if (MyTargetNodeChannel.MySessionState == null || MyTargetNodeChannel.MySessionState.HasExpired)
                                throw new Exception($"Session was deleted or has expired ({MyTargetNodeChannel?.MySessionState?.HasExpired})");
                            #region Batch Serialization
                            IsBatchOn++;
                            FinalCnt++;
                            var tToAdd = TheCommonUtils.SerializeObjectToJSONString(tDev);
                            if (MCQCount == 0 || tQueued.IsChunked || IsBatchOn > TheBaseAssets.MyServiceHostInfo.MaxBatchedTelegrams)
                            {
                                if (MCQCount != 0)
                                    tDev.CNT = MCQCount;
                                IsBatchOn = 0;
                            }
                            else
                            {
                                if (tSendBufferStr.Length + tToAdd.Length > TheBaseAssets.MAX_MessageSize[(int)MyTargetNodeChannel.SenderType])
                                {
                                    tDev.CNT = MCQCount;
                                    IsBatchOn = 0;
                                }
                            }
                            if (tSendBufferStr.Length > 1) tSendBufferStr.Append(",");
                            tSendBufferStr.Append(tToAdd);
                            #endregion
                        }
                        else
                        {
                            IsBatchOn = 0;
                        }
                    } while (IsBatchOn > 0 && IsInWSPost && TheBaseAssets.MasterSwitch);
                    if (!IsInWSPost || tSendBufferStr.Length < 2)
                    {
                        IsInWSPost = false;
                        continue;
                    }
                    tSendBufferStr.Append("]");
                    if (FinalCnt > 1)
                        TheBaseAssets.MySYSLOG.WriteToLog(235, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("WSQueuedSender", $"Batched:{FinalCnt}", eMsgLevel.l3_ImportantMessage));

                    if (cdeSenderType.CDE_JAVAJASON != MyTargetNodeChannel.SenderType)
                        MyWebSocketProcessor.PostToSocket(null, TheCommonUtils.cdeCompressString(tSendBufferStr.ToString()), true, false);
                    else
                        MyWebSocketProcessor.PostToSocket(null, TheCommonUtils.CUTF8String2Array(tSendBufferStr.ToString()), false, false);
                    IsInWSPost = false;

                    tSendBufferStr.Clear();
                }
                TheBaseAssets.MySYSLOG.WriteToLog(235, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("WSQueuedSender", $"WSQSenderThread was closed for {MyTargetNodeChannel?.ToMLString()} IsAlive:{IsAlive} MyWSProccAlive:{(MyWebSocketProcessor == null ? "Is Null" : MyWebSocketProcessor?.IsActive.ToString())},SessionState:{(MyTargetNodeChannel?.MySessionState != null)} IsConnecting:{IsConnecting} IsConnected:{IsConnected}", eMsgLevel.l1_Error));
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(235, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("WSQueuedSender", "Exception in WSSenderThread.", eMsgLevel.l1_Error, "Error:" + e));
            }
            finally
            {
                IsInWSPost = false;
                IsSenderThreadRunning = false;
                StopHeartBeat();
            }
            if (IsAlive || (MyWebSocketProcessor != null && MyWebSocketProcessor.IsActive))
            {
                IsAlive = false;
                if (MyWebSocketProcessor != null)
                    MyWebSocketProcessor.Shutdown(true, "1310:SenderThread Closed");
            }
            CloudCounter--;
        }
        private static int CloudCounter;
    }
}
