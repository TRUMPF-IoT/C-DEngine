// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.ISM;
using nsCDEngine.ViewModels;
using System;
using System.Threading;

namespace nsCDEngine.Communication
{
    internal partial class TheQueuedSender
    {
        private readonly object lockStartLock = new ();
        internal void StartHeartBeat()
        {
            if (IsQSenderReadyForHB || !TheBaseAssets.MasterSwitch)
                return;
            IsQSenderReadyForHB = true;
            try
            {
                MyTSMHistory ??= new TheMirrorCache<TheSentRegistryItem>(TheBaseAssets.MyServiceHostInfo.TO.QSenderDejaSentTime);

                if (!TheBaseAssets.MyServiceHostInfo.UseHBTimerPerSender)
                {
                    TheQueuedSenderRegistry.RegisterHBTimer(sinkHeartBeatTimer);
                }
                else
                {
                    lock (lockStartLock)
                    {
                        mMyHeartBeatTimer ??= new Timer(sinkHeartBeatTimerLocal, null, TheBaseAssets.MyServiceHostInfo.TO.QSenderHealthTime, TheBaseAssets.MyServiceHostInfo.TO.QSenderHealthTime);
                    }
                }
                InitHeartbeatTimer();
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(247, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"StartHearbeat failed: {e}", eMsgLevel.l1_Error), true);
                IsQSenderReadyForHB = false;
            }
        }

        internal void StopHeartBeat()
        {
            IsQSenderReadyForHB = false;
        }

        internal void InitHeartbeatTimer()
        {
            HeartBeatCnt = 0;
            mLastHeartBeatReceived = DateTimeOffset.Now;
            if (MyTargetNodeChannel!=null)
                Engines.NMIService.TheFormsGenerator.UpdateRegisteredNMINode(MyTargetNodeChannel.cdeMID);
        }

        internal bool IsHeartBeatAlive(int pTimeOut)
        {
            return DateTimeOffset.Now.Subtract(mLastHeartBeatReceived).TotalSeconds < pTimeOut;
        }
        internal DateTimeOffset GetLastHeartBeat()
        {
            return mLastHeartBeatReceived;
        }

        internal void ResetHeartbeatTimer(bool IsChangeRequest, TheSessionState pSession)
        {
            InitHeartbeatTimer();
            if (MyTargetNodeChannel == null || MyTargetNodeChannel.SenderType == cdeSenderType.CDE_LOCALHOST) return;
            TheBaseAssets.MySession.WriteSession(pSession);
            if (string.IsNullOrEmpty(MyTargetNodeChannel.RealScopeID) && !string.IsNullOrEmpty(pSession.SScopeID) && MyISBlock == null) //RScope-OK: This should be super rare that a channel has no RScope but the SessionState has a Scope
                UpdateSubscriptionScope(TheBaseAssets.MyScopeManager.GetRealScopeID(pSession.SScopeID));       //GRSI: rare
            if (IsChangeRequest)
                TheBaseAssets.MySYSLOG.WriteToLog(247, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("QueuedSender", $"{TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false)} received Heartbeat Change Request for {MyTargetNodeChannel.ToMLString()}", eMsgLevel.l6_Debug), true);
            else
            {
                TheBaseAssets.MySYSLOG.WriteToLog(247, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("QueuedSender", $"{TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false)} received and reset Heartbeat for {MyTargetNodeChannel.ToMLString()}", eMsgLevel.l6_Debug), true);
            }
        }

        internal bool SetLastHeartbeat(TheSessionState pState)
        {
            if (TheCommonUtils.IsLocalhost(MyTargetNodeChannel.cdeMID))
                return true;
            MyTargetNodeChannel.MySessionState ??= pState;
            ResetHeartbeatTimer(false, MyTargetNodeChannel.MySessionState);
            return true;
        }

        #region Heartbeat Monitor
        private int HeartBeatCnt;
        private bool IsQSenderReadyForHB = false;
        private bool mInHeartBeatTimer;
        private DateTimeOffset mLastHeartBeatReceived;

        long mHeartBeatTicker;
        private void sinkHeartBeatTimerLocal(object NOTUSED)
        {
            sinkHeartBeatTimer(0);
        }
        private void sinkHeartBeatTimer(long NOTUSED)
        {
            try
            {
                TheDiagnostics.SetThreadName("HeartbeatTimer", true);
                mHeartBeatTicker++;
                TheBaseAssets.MySYSLOG.WriteToLog(2803, TSM.L(eDEBUG_LEVELS.EVERYTHING) ? null : new TSM("QSender", $"Enter HearbeatTimer for ORG:{MyTargetNodeChannel}", eMsgLevel.l7_HostDebugMessage));
                TheTimeouts tTO = TheBaseAssets.MyServiceHostInfo.TO;

                if (!IsHeartBeatAlive(tTO.DeviceCleanSweepTimeout * 2) && TheCommonUtils.IsDeviceSenderType(MyTargetNodeChannel?.SenderType ?? cdeSenderType.NOTSET) && MyTargetNodeChannel?.IsWebSocket == false)  //IDST-OK: Remove dead devices that might have hard disconnected (no correct disconnect) i.e. http browsers were just closed
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2820, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QSender", $"Forced Removal of QSender {MyTargetNodeChannel.ToMLString()} due to DeviceCleanSweep", eMsgLevel.l4_Message));
                    Guid? tTarget = MyTargetNodeChannel?.cdeMID;
                    DisposeSender(true);
                    if (tTarget.HasValue)
                    {
                        TheQueuedSenderRegistry.RemoveOrphan(tTarget.Value);
                        TheCommCore.PublishCentral(new TSM(eEngineName.ContentService, "CDE_DELETEORPHAN", tTarget.ToString()));
                    }
                    return;
                }
                if (!IsAlive && !MyTargetNodeChannel?.IsWebSocket == true) return; //NEW:V3 2013/12/13 allow for cloud reconnect if WebSockets

                if ((mHeartBeatTicker % (tTO.HeartBeatRate * 2)) == 0)
                {
                    if (MyTargetNodeChannel?.SenderType != cdeSenderType.CDE_LOCALHOST)
                        timerMyHeartbeatTimer();
                    if (MyTargetNodeChannel?.SenderType == cdeSenderType.CDE_CLOUDROUTE && ((!IsConnected && !IsConnecting) || !IsAlive) && (mHeartBeatTicker % (tTO.HeartBeatRate * 10)) == 0) //tQ.MyTargetNodeChannel.IsWebSocket &&  NOW ALWAYS RECONNECT
                    {
                        TheCommonUtils.cdeRunAsync("ReconnectCloud", true, (o) => ReconnectCloud());
                    }
                    if (MyTargetNodeChannel?.SenderType != cdeSenderType.CDE_LOCALHOST && IsAlive && IsConnected) //!TheBaseAssets.MyServiceHostInfo.IsCloudService && !tQ.MyTargetNodeChannel.IsWebSocket &&
                    {
                        if (MyTargetNodeChannel?.IsWebSocket != true)
                        {
                            if (GetQueLength() == 0)
                            {
                                TheBaseAssets.MyServiceHostInfo.TO.MakeHeartPump(); // CODE REVIEW Markus: Isn't this backwards: we should pump faster while we have more work to do?
                                SendPickupMessage(); //Pickup
                            }
                            else
                                TheBaseAssets.MyServiceHostInfo.TO.MakeHeartNormal();
                        }
                        else
                            SendPickupMessage(); //Pickup
                    }
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2821, new TSM("QSRegistry", $"Fatal Error in HealthTimer for QS:{this.cdeMID} and MTNC:{MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l1_Error, e.ToString()));
            }
        }



        internal void timerMyHeartbeatTimer()
        {
            if (mInHeartBeatTimer)
            {
                // This should never happen: we are running this under a lock
                TheBaseAssets.MySYSLOG.WriteToLog(248, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", $"QSender {MyTargetNodeChannel?.ToMLString()}: Internal error (timer reentered)", eMsgLevel.l2_Warning), true);
                return;
            }
            if (!TheBaseAssets.MasterSwitch || !IsQSenderReadyForHB || IsInWSPost || IsInPost || MyREST?.IsPosting > 0)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(248, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"QSender {MyTargetNodeChannel?.ToMLString()} suspended due to: MS:{TheBaseAssets.MasterSwitch} IA:{IsAlive} ISSRFHB:{IsQSenderReadyForHB} INWSP:{IsInWSPost} INP:{IsInPost} RPCNT:{MyREST?.IsPosting}", eMsgLevel.l6_Debug), true);
                return; //New in 4.205: HB is not checked if QSender is in post (for example during a large telegram going to a browser)
            }

            if (MyTargetNodeChannel.IsWebSocket && !IsConnecting && !TheCommonUtils.IsDeviceSenderType(MyTargetNodeChannel.SenderType))
                TheBaseAssets.MySession.WriteSession(MyTargetNodeChannel.MySessionState);
            mInHeartBeatTimer = true;

            try
            {
                if (!IsConnecting && !IsConnected)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(248, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"QSender {MyTargetNodeChannel?.ToMLString()} in illegal state (not connected AND not connecting) for Heartbeat!", eMsgLevel.l2_Warning), false);
                    FireSenderProblem(new TheRequestData() { ErrorDescription = "1304:Heartbeat found !Connected and !IsConnecting" });
                    return;
                }
                if (!IsConnected && IsConnecting)
                {
                    ConnectRetries++;
                    if (ConnectRetries > TheBaseAssets.MyServiceHostInfo.TO.HeartBeatMissed)
                    {
                        TSM tMsg = new ("QueuedSender", $"Initial Connection failed. {MyTargetNodeChannel?.ToMLString()} might be down!", eMsgLevel.l2_Warning);
                        HeartBeatCnt = 0;
                        if (MyTargetNodeChannel != null)
                            tMsg.ORG = MyTargetNodeChannel.cdeMID.ToString();
                        else
                            tMsg.ORG = "No Channel";
                        TheBaseAssets.MySYSLOG.WriteToLog(248, tMsg, true);
                        FireSenderProblem(new TheRequestData() { ErrorDescription = $"1305:Connecting failed for {ConnectRetries} retries" });
                        ConnectRetries = 0;
                        IsConnecting = false;
                    }
                }
                else
                {
                    if (IsConnecting)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(248, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"QSender {MyTargetNodeChannel?.ToMLString()} in illegal state (Connected AND Connecting) for Heartbeat!", eMsgLevel.l2_Warning), false);
                        return;
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(248, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("QueuedSender", "Enter HeartBeatTick for " + MyTargetNodeChannel, eMsgLevel.l6_Debug), true);
                    int Misses = TheBaseAssets.MyServiceHostInfo.TO.OnAdrenalin ? 2 : 1;
                    if (HeartBeatCnt > TheBaseAssets.MyServiceHostInfo.TO.HeartBeatMissed * Misses)
                    {
                        TSM tMsg = new ("QueuedSender", $"Too Many Heartbeats ({HeartBeatCnt}) missed - {MyTargetNodeChannel?.ToMLString()} might be down!", eMsgLevel.l2_Warning);
                        HeartBeatCnt = 0;
                        tMsg.ORG = MyTargetNodeChannel.cdeMID.ToString();
                        TheBaseAssets.MySYSLOG.WriteToLog(248, tMsg, true);
                        FireSenderProblem(new TheRequestData() { ErrorDescription = "1306:Heartbeat failure!!" });
                    }
                    else
                    {
                        if (HeartBeatCnt > TheBaseAssets.MyServiceHostInfo.TO.HeartBeatWarning)
                        {
                            TSM tMsg = new ("QueuedSender", $"More than {TheBaseAssets.MyServiceHostInfo.TO.HeartBeatWarning} ({HeartBeatCnt}) Heartbeats from {MyTargetNodeChannel?.ToMLString()} missed", eMsgLevel.l2_Warning) {ORG = MyTargetNodeChannel.cdeMID.ToString()};
                            TheBaseAssets.MySYSLOG.WriteToLog(248, tMsg, true);
                        }
                        if (MyTargetNodeChannel != null) // && MyTargetNodeChannel.References > 0)  legacy
                            SendPickupMessage();
                        HeartBeatCnt++;
                    }
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(248, new TSM("QueuedSender", $"Exception in Healthtimer for {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l1_Error, e.ToString()), true);
            }
            finally
            {
                TheBaseAssets.MySYSLOG.WriteToLog(248, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("QueuedSender", $"Leave HeartBeatTick for {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l6_Debug), false);
                mInHeartBeatTimer = false;
            }
        }

        #endregion
    }
}
