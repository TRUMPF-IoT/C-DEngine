// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading;

namespace nsCDEngine.Communication
{

    internal class TheWSProcessorBase : IDisposable
    {
        internal Action<string> eventClosed;
        internal Action<TheWSProcessorBase, bool, string> eventConnected;
        internal TheQueuedSender MyQSender = null;
        internal Guid OwnerNodeID = Guid.Empty;
        internal bool IsActive;

        protected TheRequestData MySessionRequestData;
        protected bool CloseFired;
        protected bool IsClient = false;
        protected CancellationTokenSource mCancelToken;

        protected ManualResetEventSlim mre; //NEW:V3BETA2: Was AutoReset Event
        internal bool ProcessingAllowed
        {
            get
            {
                if (mre == null) return true;
                return mre.IsSet;
            }
            set
            {
                if (mre == null) return;
                if (value)
                    mre.Set();
                else
                    mre.Reset();
            }
        }
        internal void WaitUntilProcessingAllowed()
        {
            mre.Wait();
        }

        /// <summary>
        /// Set the first request to the WS-Processor. It will be used to handle all WebSocket data for the session of the related node
        /// </summary>
        /// <param name="pRequest">If null, the Processor will create a new empty TheRequestData to handle all traffic</param>
        public void SetRequest(TheRequestData pRequest)
        {
            if (pRequest == null)
                MySessionRequestData = new TheRequestData();
            else
                MySessionRequestData = pRequest;
            MySessionRequestData.WebSocket = this;
            mre = new ManualResetEventSlim(true);
            var oldCancelToken = mCancelToken;
            mCancelToken = new();
            try
            {
                oldCancelToken?.Cancel();
            }
            catch
            {
                //intent blank
            }
            try
            {
                oldCancelToken?.Dispose();
            }
            catch
            {
                //intent blank
            }
            IsActive = true;
            CloseFired = false;
            try
            {
                if (TheCommCore.MyHttpService != null && MySessionRequestData.RequestUri != null)
                    TheCommCore.MyHttpService.cdeProcessPost(MySessionRequestData);
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4360, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSProcessor", "cdeProcssPost Error", eMsgLevel.l1_Error, e.ToString()));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                eventClosed = null;
                eventConnected = null;
                if (mre != null) mre.Dispose();
                mre = null;
                mCancelToken?.Cancel();
                mCancelToken?.Dispose();
                mCancelToken = null;
                iDispose();
            }
        }

        protected virtual void iDispose()
        {
        }

        internal virtual void PostToSocket(TheDeviceMessage pMsg, byte[] pPostBuffer, bool pSendAsBinary, bool IsInitialConnect)
        {

        }

        internal virtual bool Connect(TheQueuedSender pSender)
        {
            return true;
        }

        public virtual void Shutdown(bool IsAsync, string pReason)
        {
            if (MyQSender != null)
            {
                MyQSender.IsSenderThreadRunning = false;
                MyQSender.IsConnected = false;
                MyQSender.IsConnecting = false;
            }
            if (MySessionRequestData != null)
                MySessionRequestData.SessionState = null;   //Wiping out Session State triggers recreate on next connect
            IsActive = false;
        }

        internal void ProcessIncomingData(string pPostString, byte[] pPostData, int pPostDataLength)
        {
            if (!IsActive) return;

            if (MySessionRequestData == null || MySessionRequestData?.SessionState?.HasExpired == true)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4360, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSProcessor", $"Incoming Data on expired session ({MySessionRequestData?.SessionState?.HasExpired}) detected - shutting down websockets", eMsgLevel.l6_Debug));
                Shutdown(false, "1600:Incoming Data on expired session detected - shutting down websockets");
                return;
            }
            TheRequestData tRequestData = TheRequestData.CloneForWS(MySessionRequestData);

            Dictionary<string, string> kpiLabels = null;
            if (TheCDEKPIs.EnableKpis)
            {
                if (MyQSender?.MyTargetNodeChannel != null)
                {
                    var scopeHash = MyQSender?.MyTargetNodeChannel?.ScopeIDHash;
                    var nodeId = TheCommonUtils.CStr(MyQSender?.MyTargetNodeChannel?.cdeMID);
                    kpiLabels = new Dictionary<string, string> {{"scope", scopeHash}, {"device", nodeId}};

                    TheCDEKPIs.IncrementKPI(eKPINames.QSReceivedTSM.ToString(), kpiLabels, 1, false, TheCDEKPIs.KpiExpiration);
                }

                TheCDEKPIs.IncrementKPI(eKPINames.QSReceivedTSM);
            }

            tRequestData.PostData = pPostData;
            tRequestData.PostDataIdx = 0;
            if (pPostData == null)
                tRequestData.PostDataLength = 0;
            else
            {
                tRequestData.PostDataLength = pPostDataLength > 0 ? pPostDataLength : pPostData.Length;
                if (TheCDEKPIs.EnableKpis)
                {
                    TheCDEKPIs.IncrementKPI(eKPINames.QKBReceived, tRequestData.PostDataLength);
                    if (kpiLabels is {Count: > 0})
                    {
                        TheCDEKPIs.IncrementKPI(eKPINames.QKBReceived.ToString(), kpiLabels,
                            tRequestData.PostDataLength, false, TheCDEKPIs.KpiExpiration);
                    }
                }
            }

            if (IsClient)
            {
                string cmdString = "";
                try
                {
                    List<TheDeviceMessage> tDevList;
                    if (tRequestData.PostData == null && !string.IsNullOrEmpty(pPostString))
                    {
                        if (pPostString[0] != '[')
                            return;
                        tDevList = TheDeviceMessage.DeserializeJSONToObject(pPostString);
                    }
                    else
                    {
                        if (tRequestData.PostData==null) return; //Edge Case but could happen - all what follows required ProcessClientDeviceMsg

                        cmdString = tRequestData.PostData[0] == (byte)'[' ? TheCommonUtils.CArray2UTF8String(tRequestData.PostData, 0, tRequestData.PostDataLength) : TheCommonUtils.cdeDecompressToString(tRequestData.PostData, 0, tRequestData.PostDataLength);
                        tDevList = TheDeviceMessage.DeserializeJSONToObject(cmdString);
                    }

                    TheCorePubSub.ProcessClientDeviceMessage(MyQSender, tRequestData, tDevList);
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4361, new TSM("WSClient", "Message-Received Processing Error", eMsgLevel.l1_Error, e.ToString()));
                }
            }
            else
            {
                try
                {
                    if (tRequestData.PostData==null && !string.IsNullOrEmpty(pPostString))
                    {
                        tRequestData.PostData = TheCommonUtils.CUTF8String2Array(pPostString);
                        tRequestData.PostDataLength = tRequestData.PostData.Length;
                    }
                    if (TheCommCore.MyHttpService != null)
                    {
                        TheCommCore.MyHttpService.cdeProcessPost(tRequestData);
                    }
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4362, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSServer", "WebSocketServer-ProcessRequest Error", eMsgLevel.l1_Error, e.ToString()));
                }
            }
            MySessionRequestData.SessionState ??= tRequestData.SessionState;
            if (MySessionRequestData.DeviceID == Guid.Empty)
                MySessionRequestData.DeviceID = tRequestData.DeviceID;
            if (tRequestData.SessionState != null && MyQSender != null)
            {
                if (MyQSender.IsConnecting)
                {
                    MyQSender.IsConnected = true;
                    if (MyQSender.eventConnected != null)
                        TheCommonUtils.cdeRunAsync("QueueConnected", true, (p) =>
                        {
                            MyQSender?.eventConnected?.Invoke(MyQSender, MyQSender.MyTargetNodeChannel);
                        });
                    MyQSender.MyISBlock?.FireEvent("Connected");
                }
                //NEW3.124: Reset Heartbeat on Ws Post
                MyQSender.ResetHeartbeatTimer(false, tRequestData.SessionState);
            }
            else
                TheBaseAssets.MySYSLOG.WriteToLog(4361,TSM.L(eDEBUG_LEVELS.ESSENTIALS)?null: new TSM("WSClient", $"No Request Session {tRequestData.SessionState!=null} or no QSender Found {MyQSender!=null}) IsClient={IsClient}", eMsgLevel.l2_Warning));


            try
            {
                if (tRequestData.ResponseBuffer != null)
                    PostToSocket(null, tRequestData.ResponseBuffer, IsClient,false);
            }
            catch (Exception ex) // For debugging
            {
                Shutdown(true,"1601:PostToSocket Error: "+ ex);
            }
        }
    }
}
