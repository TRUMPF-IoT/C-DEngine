// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using nsCDEngine.BaseClasses;
using System.Threading;
using nsCDEngine.Engines;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using nsCDEngine.ISM;
using System.Threading.Tasks;
using nsCDEngine.Engines.NMIService;
using System.Text;

namespace nsCDEngine.Communication
{
    /// <summary>
    /// TheQueuedSenderRegistry is a class that manages all Sending and Receive connections to other nodes.
    /// In the heart of TheQueuedSenderRegistry is a StorageMirror that contains all current active connections.
    /// </summary>
    public static class TheQueuedSenderRegistry
    {
        /// <summary>
        /// This Event if fired when a CloudServiceRoute is established and up and running
        /// </summary>
        internal static Action<ICDEThing, TheChannelInfo> eventCloudIsBackUp;
        /// <summary>
        /// This Event if fired when a CloudServiceRoute is disconnected or not available anymore that was active in the past.
        /// </summary>
        internal static Action<ICDEThing, TheChannelInfo> eventCloudIsDown;

        internal static void Shutdown()
        {
            if (mMyHealthTimer != null)
                mMyHealthTimer.Dispose();
            mMyHealthTimer = null;
            if (MyTSMHistorySetTimer != null)
                MyTSMHistorySetTimer.Dispose();
            MyTSMHistorySetTimer = null;
        }
        internal static void Startup()
        {
            if (MyQueuedSenderList == null)
            {
                MyQueuedSenderList = new TheMirrorCache<TheQueuedSender>(0);
                MyTSMHistorySet1 = new HashSet<TheSentRegistryItemHS>();
                MyTSMHistorySet2 = new HashSet<TheSentRegistryItemHS>();
                MyTSMHistorySet3 = new HashSet<TheSentRegistryItemHS>();
                MyTSMHistorySetTimer = new Timer(MyTSMHistorySetRemoveExpired, null, 0, TheBaseAssets.MyServiceHostInfo.TO.QSenderDejaSentTime * 3000);
                mMyHealthTimer = new Timer(sinkHealthTimer, null, 0, TheBaseAssets.MyServiceHostInfo.TO.QSenderHealthTime);
            }
        }

        #region QS Heartbeat Timer
        internal static void RegisterHBTimer(Action<long> eventHBTimer)
        {
            if (eventHBTimer == null) return;
            lock (HBTimerLock)
            {
                if (mMyHBTimer == null)
                    mMyHBTimer = new Timer(sinkHBTimer, null, 0, TheBaseAssets.MyServiceHostInfo.TO.QSenderHealthTime);
                eventHBTimerFired -= eventHBTimer;
                eventHBTimerFired += eventHBTimer;
            }
        }
        internal static void UnregisterHBTimer(Action<long> eventHBTimer)
        {
            lock (HBTimerLock)
            {
                eventHBTimerFired -= eventHBTimer;
            }
        }

        private static void sinkHBTimer(object NOTUSED)
        {
            if (TheCommonUtils.cdeIsLocked(HBTimerLock))
            {
                mHBLockCounter++;
                TheBaseAssets.MySYSLOG.WriteToLog(280, TSM.L(eDEBUG_LEVELS.ESSENTIALS) && mHBLockCounter < 3 ? null : new TSM("QSRegistry", $"HeartBeat Skipped - Lock Counter: {mHBLockCounter}", eMsgLevel.l7_HostDebugMessage));
                return;
            }
            lock (HBTimerLock)
            {
                try
                {
                    mHBLockCounter = 0;
                    mHBTicker++;
                    TheDiagnostics.SetThreadName("HBTimer", true);
                    TheBaseAssets.MySYSLOG.WriteToLog(2803, TSM.L(eDEBUG_LEVELS.EVERYTHING) ? null : new TSM("QSRegistry", "Enter HBTimer", eMsgLevel.l7_HostDebugMessage));

                    if (eventHBTimerFired != null)
                    {
                        if (!TheBaseAssets.MyServiceHostInfo.FireGlobalTimerSync)
                            TheCommonUtils.DoFireEvent<long>(eventHBTimerFired, mHBTicker, true, 1000);
                        else
                        {
                            var handlers = eventHBTimerFired.GetInvocationList();
                            TheCDEKPIs.SetKPI(eKPINames.HTCallbacks, handlers.Length);
                            foreach (Delegate handler in handlers)
                            {
                                TheCommonUtils.DoFireEvent<long>(handler as Action<long>, mHBTicker, false, 1000);
                            }
                        }
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(2822, TSM.L(eDEBUG_LEVELS.EVERYTHING) ? null : new TSM("QSRegistry", "Leave HeartbeatTimer", eMsgLevel.l7_HostDebugMessage));
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2821, new TSM("QSRegistry", "Error in HBTimer", eMsgLevel.l1_Error, e.ToString()));
                }
            }
        }

        private static Action<long> eventHBTimerFired;
        private static Timer mMyHBTimer;
        private static long mHBLockCounter = 0;
        private static long mHBTicker;
        private static readonly object HBTimerLock = new object();
        #endregion

        #region Health Timer now doing all timer function in one
        /// <summary>
        /// TheQueuedSenderRegistry manages all Serice Health related methods a timer that is fired every TheBaseAssets.MyServiceHostInfo.TO.QSenderHealthTime milliseconds (by default 1 second)
        /// This function allows to register a custom callback that will be called everytime the Timer is fired.
        /// Make sure you unregister the callback when its no longer needed to avoid memory leaks.
        /// </summary>
        /// <param name="eventHealthTimer">A callback that the HealthTimer will call every QSenderHealthTime interval. The long parameter is a counter that is increased every time the timer was fired.</param>
        public static void RegisterHealthTimer(Action<long> eventHealthTimer)
        {
            eventHealthTimerFired -= eventHealthTimer;
            eventHealthTimerFired += eventHealthTimer;
        }
        /// <summary>
        /// This function unregisters a previously registered callback.
        /// </summary>
        /// <param name="eventHealthTimer"></param>
        public static void UnregisterHealthTimer(Action<long> eventHealthTimer)
        {
            eventHealthTimerFired -= eventHealthTimer;
        }
        private static Action<long> eventHealthTimerFired;
        private static Timer mMyHealthTimer;
        private static long mHealthTicker;
        private static long mLockCounter = 0;
        private static readonly object HealthTimerLock = new object();
        private static void sinkHealthTimer(object NOTUSED)
        {
            if (TheCommonUtils.cdeIsLocked(HealthTimerLock))
            {
                mLockCounter++;
                TheBaseAssets.MySYSLOG.WriteToLog(280, TSM.L(eDEBUG_LEVELS.VERBOSE) && mLockCounter < 3 ? null : new TSM("QSRegistry", $"HealthTimer Skipped - Lock Counter: {mLockCounter}", eMsgLevel.l7_HostDebugMessage));
                return;
            }
            lock (HealthTimerLock)
            {
                try
                {
                    mLockCounter = 0;
                    mHealthTicker++;
                    TheDiagnostics.SetThreadName("HealthTimer", true);
                    TheBaseAssets.MySYSLOG.WriteToLog(2803, TSM.L(eDEBUG_LEVELS.EVERYTHING) ? null : new TSM("QSRegistry", "Enter HealthTimer", eMsgLevel.l7_HostDebugMessage));
                    TheTimeouts tTO = TheBaseAssets.MyServiceHostInfo.TO;

                    if (!TheCommonUtils.cdeIsLocked(lockUserCheck) && (mHealthTicker % TheBaseAssets.MyServiceHostInfo.TO.UserCheckRate) == 0)
                        TheCommonUtils.DoFireEvent<long>(UserCheck, mHealthTicker, true, 0);
                    if ((mHealthTicker % TheBaseAssets.MyServiceHostInfo.SessionTimeout) == 0)
                        TheCommonUtils.DoFireEvent<long>(TheFormsGenerator.CleanupKnownNMISubscriptions, mHealthTicker, true, 0);

                    if (eventHealthTimerFired != null)
                    {
                        if (!TheBaseAssets.MyServiceHostInfo.FireGlobalTimerSync)
                            TheCommonUtils.DoFireEvent<long>(eventHealthTimerFired, mHealthTicker, true, 5000);
                        else
                        {
                            var handlers = eventHealthTimerFired.GetInvocationList();
                            TheCDEKPIs.SetKPI(eKPINames.HTCallbacks, handlers.Length);
                            foreach (Delegate handler in handlers)
                            {
                                TheCommonUtils.DoFireEvent<long>(handler as Action<long>, mHealthTicker, false, 5000);
                            }
                        }
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(2822, TSM.L(eDEBUG_LEVELS.EVERYTHING) ? null : new TSM("QSRegistry", "Leave HealthTimer", eMsgLevel.l7_HostDebugMessage));
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2821, new TSM("QSRegistry", "Error in HealthTimer", eMsgLevel.l1_Error, e.ToString()));
                }
            }
        }

        private static readonly object lockUserCheck = new object();
        private static void UserCheck(long NOTUSED)
        {
            if (TheCDEngines.MyContentEngine != null && TheBaseAssets.MyServiceHostInfo.IsCloudService && TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper && !TheCommonUtils.cdeIsLocked(lockUserCheck))
            {
                lock (lockUserCheck)
                {
                    //var tLogTime = DateTimeOffset.Now;
                    foreach (TheQueuedSender tQ in GetCloudNodes())
                    {
                        if (tQ.MyTargetNodeChannel != null && !TheUserManager.HasScopeUsers(tQ.MyTargetNodeChannel.RealScopeID))    //RScope-OK: Users only supported on primary ScopeID
                            TheCommCore.PublishToNode(tQ.MyTargetNodeChannel.cdeMID, TheBaseAssets.MyScopeManager.GetScrambledScopeID(tQ.MyTargetNodeChannel.RealScopeID, true), new TSM(eEngineName.ContentService, "CDE_INIT_SYNC"));       //GRSI: medium fequency on Cloud // RScope-OK
                    }
                    //var tim = DateTimeOffset.Now.Subtract(tLogTime).TotalMilliseconds;
                    //if (tim > 1)
                    //    TheSystemMessageLog.ToCo($"Out UserCheck time:{tim}");
                }
            }
        }
        #endregion

        #region Was TSM Seen Before

        internal static bool WasTSMSeenBefore(TSM pTSM, Guid pSessionID, string pTopic, string pRealScope)
        {
            Guid tOrg = pTSM.GetOriginator();
            if (tOrg == Guid.Empty) return false;

            if (MyTSMHistorySet1 == null)
            {
                MyTSMHistorySet1 = new HashSet<TheSentRegistryItemHS>();
                MyTSMHistorySet2 = new HashSet<TheSentRegistryItemHS>();
                MyTSMHistorySet3 = new HashSet<TheSentRegistryItemHS>();
                MyTSMHistorySetTimer = new Timer(MyTSMHistorySetRemoveExpired, null, 0, TheBaseAssets.MyServiceHostInfo.TO.QSenderDejaSentTime * 1000);
            }
            TheQueuedSender tQ = GetSenderByGuid(tOrg);
            if (tQ != null)
            {
                if (tQ.WasTSMSeenBefore(pTSM, pRealScope, false))
                    return true;
            }
            double cFID = TheCommonUtils.CDbl(pTSM.FID);
            if (cFID == 0) return false;
            int tHas = 0;
            if (pTSM.PLB != null && pTSM.PLB.Length > 0)
            {
                tHas = pTSM.PLB.Length;
                //CODE_REVIEW: Although this is super fast, it will not work in all circumstances and might lead to loss of telegrams if they have the same length in the PLB...we cannot go to a much slower algorithm as this function is already very slow.
                //How about XOR the first 5 and last 5 bytes of the array. It gives at least a higher confidence that the telegrams are different
            }
            else
            {
                if (!string.IsNullOrEmpty(pTSM.PLS))
                    tHas = pTSM.PLS.GetHashCode();  //This also does not garantee uniquness :(
            }


            var topicHash = (pTSM.TXT + pTopic).GetHashCode();
            var tCnt = 0;   //local variable is faster then Static Property Lookup
            TheSentRegistryItemHS tS = new TheSentRegistryItemHS() { ORG = tOrg, SentTime = pTSM.TIM, FID = cFID, Engine = pTSM.ENG, IsOutgoing = false, SessionID = pSessionID, PLSHash = tHas, TopicHash = topicHash, cdeEXP = TheBaseAssets.MyServiceHostInfo.TO.QSenderDejaSentTime };
            lock (MyTSMHistorySetLock)
            {
                HashSet<TheSentRegistryItemHS> ActHistorySet = null;
                switch (CurrentSecondMyTSMHistory)
                {
                    case 0: ActHistorySet = MyTSMHistorySet1; break;
                    case 1: ActHistorySet = MyTSMHistorySet2; break;
                    case 2: ActHistorySet = MyTSMHistorySet3; break;
                }
                if (MyTSMHistorySet1.Contains(tS) || MyTSMHistorySet2.Contains(tS) || MyTSMHistorySet3.Contains(tS))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QSRegistry", $"Global TSMHistory dropping message {tCnt}! TIM:{pTSM.TIM} ENG:{pTSM.ENG}", eMsgLevel.l2_Warning));
                    return true;
                }
                ActHistorySet?.Add(tS);
                tCnt = MyTSMHistorySet1.Count + MyTSMHistorySet2.Count + MyTSMHistorySet3.Count;
            }
            TheCDEKPIs.SetKPI(eKPINames.SeenBeforeCount, tCnt);
            TheCDEKPIs.IncTSMByEng(pTSM.ENG);
            if (tCnt > 0 && ((tCnt % 25000) == 0))
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QSRegistry", $"Global TSMHistory > {tCnt}! TIM:{pTSM.TIM} ENG:{pTSM.ENG}", eMsgLevel.l2_Warning));
            }
            return false;
        }


        private static void MyTSMHistorySetRemoveExpired(object notused)
        {
            if (TheCommonUtils.cdeIsLocked(MyTSMHistorySetLock))
            {
                if (MyTSMHistorySetMoreThanOneOnLock)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4801, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("WasTSMSeenBeforeTimer", "Skipping Save exporation as another expire-timer is already queued", eMsgLevel.l2_Warning));
                    return;
                }
                MyTSMHistorySetMoreThanOneOnLock = true;
            }
            lock (MyTSMHistorySetLock)
            {
                MyTSMHistorySetMoreThanOneOnLock = false;
                switch (CurrentSecondMyTSMHistory)
                {
                    case 0: MyTSMHistorySet3.Clear(); break;
                    case 1: MyTSMHistorySet2.Clear(); break;
                    case 2: MyTSMHistorySet1.Clear(); break;
                }
                CurrentSecondMyTSMHistory++;
                if (CurrentSecondMyTSMHistory > 2)
                    CurrentSecondMyTSMHistory = 0;
            }
        }
        //private static TheMirrorCache<TheSentRegistryItem> MyTSMSenderHistory;
        private static HashSet<TheSentRegistryItemHS> MyTSMHistorySet1 = null;
        private static HashSet<TheSentRegistryItemHS> MyTSMHistorySet2 = null;
        private static HashSet<TheSentRegistryItemHS> MyTSMHistorySet3 = null;
        private static readonly object MyTSMHistorySetLock = new object();
        private static bool MyTSMHistorySetMoreThanOneOnLock = false;
        private static int CurrentSecondMyTSMHistory = 0;
        private static Timer MyTSMHistorySetTimer = null;
        #endregion

        #region QueuedSender List Management
        private static TheMirrorCache<TheQueuedSender> MyQueuedSenderList;

        internal static TheQueuedSender GetSenderByGuid(Guid pGuid)
        {
            return MyQueuedSenderList.GetEntryByID(pGuid);
        }
        internal static TheQueuedSender GetSenderByUrl(string pUrl)
        {
            if (string.IsNullOrEmpty(pUrl)) return null;
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ.MyTargetNodeChannel != null && tQ.MyTargetNodeChannel.TargetUrl != null && tQ.MyTargetNodeChannel.TargetUrl.Equals(pUrl, StringComparison.OrdinalIgnoreCase))
                    return tQ;
            }
            return null;
        }

        internal static void RemoveOrphan(Guid pUri)
        {
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ.IsAlive) tQ.RemoveOrphanFromQueue(pUri);
            }
        }

        /// <summary>
        /// New in 3.083 - Returns a QSender by SessionID
        /// </summary>
        /// <param name="pSEID"></param>
        /// <returns></returns>
        internal static TheQueuedSender GetSenderBySEID(Guid pSEID)
        {
            if (Guid.Empty == pSEID) return null;
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ.MyTargetNodeChannel != null && tQ.MyTargetNodeChannel.MySessionState != null && pSEID.Equals(tQ.MyTargetNodeChannel.MySessionState.cdeMID))
                    return tQ;
            }
            return null;
        }

        internal static TheChannelInfo GetChannelInfoByUrl(string pUrl)
        {
            if (string.IsNullOrEmpty(pUrl)) return null;
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ.MyTargetNodeChannel != null && tQ.MyTargetNodeChannel.TargetUrl != null &&
                    (TheCommonUtils.IsUrlLocalhost(pUrl) ? TheCommonUtils.IsUrlLocalhost(tQ.MyTargetNodeChannel.TargetUrl) : tQ.MyTargetNodeChannel.TargetUrl.Equals(pUrl, StringComparison.OrdinalIgnoreCase)))
                    return tQ.MyTargetNodeChannel;
            }
            return null;
        }
        internal static ICollection<string> GetSubscriptionsByUri(Guid pUri)
        {
            TheQueuedSender tSender = MyQueuedSenderList.GetEntryByID(pUri);
            if (tSender != null)
            {
                return tSender.GetSubscriptions();
            }
            return null;
        }

        internal static List<Guid> GetSendersBySenderType(cdeSenderType pType)
        {
            List<Guid> tL = new List<Guid>();
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ.MyTargetNodeChannel != null && tQ.MyTargetNodeChannel.SenderType == pType) // && (!IgnoreServiceNodes || string.IsNullOrEmpty(tQ.MyTargetNodeChannel.RealScopeID))) //New: Dont sync with ServiceNodes (DMZ nodes) - only clouds with no Scoping - There is no way to detect if a connected node is unscoped. Need to be added in later version then brought back
                    tL.Add(tQ.MyTargetNodeChannel.cdeMID);
            }
            return tL;
        }
        internal static List<Guid> GetSendersByBackChannelAndCloudRoute()
        {
            List<Guid> tL = new List<Guid>();
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ.MyTargetNodeChannel != null && (tQ.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_BACKCHANNEL || tQ.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE))
                    tL.Add(tQ.MyTargetNodeChannel.cdeMID);
            }
            return tL;
        }

        internal static List<string> GetKnownScopes()
        {
            if (!TheBaseAssets.MyServiceHostInfo.IsCloudService) return null;

            List<string> tData = new List<string>();
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (!string.IsNullOrEmpty(tQ.MyTargetNodeChannel.RealScopeID) && !tData.Contains(tQ.MyTargetNodeChannel.RealScopeID))   //RScope-NOK: Only collects primary scopes
                    tData.Add(TheBaseAssets.MyScopeManager.GetScrambledScopeID(tQ.MyTargetNodeChannel.RealScopeID, true));       //GRSI: rare RScope-NOK: Only collects primary scopes
            }
            return tData;
        }

        /// <summary>
        /// Check if a Scope is know on this node. All QSender will be investigated if at least one has the requested Scope
        /// </summary>
        /// <param name="pScope">Real Scope or Easy Scope depending on next parameter</param>
        /// <param name="IsRealScope"></param>
        /// <returns></returns>
        internal static bool IsScopeKnown(string pScope, bool IsRealScope)
        {
            string tRealScope = pScope;
            if (!IsRealScope)
                tRealScope = TheBaseAssets.MyScopeManager.GetRealScopeID(TheBaseAssets.MyScopeManager.GetScrambledScopeIDFromEasyID(pScope));     //GRSI: rare
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ.MyTargetNodeChannel != null && tQ.MyTargetNodeChannel.HasRScope(tRealScope))
                    return true;
            }
            return false;
        }

        internal static List<TheChannelInfo> GetSendersByRealScope(string tRealScope)
        {
            List<TheChannelInfo> tLst = new List<TheChannelInfo>();
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ.MyTargetNodeChannel != null && tQ.MyTargetNodeChannel.HasRScope(tRealScope))
                    //((!string.IsNullOrEmpty(tQ.MyTargetNodeChannel.RealScopeID) && tQ.MyTargetNodeChannel.RealScopeID.Equals(tRealScope)) ||
                     //(string.IsNullOrEmpty(tRealScope) && string.IsNullOrEmpty(tQ.MyTargetNodeChannel.RealScopeID))))
                    tLst.Add(tQ.MyTargetNodeChannel);
            }
            return tLst;
        }

        private static readonly cdeConcurrentDictionary<string,int> LastCountByRealScope = new cdeConcurrentDictionary<string, int>();
        private static int LastQSenderCount = 0;
        private static readonly object LastQSenderCountLock = new object();
        internal static int CountRelaySendersByRealScope(string tRealScope)
        {
            if (string.IsNullOrEmpty(tRealScope))
                return 0; //unscoped telegrams/senders should not even come here!
            int qsCount = MyQueuedSenderList.Count; //expensive but still faster than the processing below
            if (TheCommonUtils.cdeIsLocked(LastQSenderCountLock) || qsCount == LastQSenderCount)
            {
                if (LastCountByRealScope.ContainsKey(tRealScope))
                    return LastCountByRealScope[tRealScope];
                return 0;
            }
            lock (LastQSenderCountLock)
            {
                LastCountByRealScope[tRealScope] = 0;
                LastQSenderCount = qsCount;
                foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues.ToList())
                {
                    if (tQ.MyTargetNodeChannel != null && !TheCommonUtils.IsDeviceSenderType(tQ.MyTargetNodeChannel.SenderType) && tQ.MyTargetNodeChannel.HasRScope(tRealScope))    //IDST-OK: Dont count Devices (only relays)
                        //((!string.IsNullOrEmpty(tQ.MyTargetNodeChannel.RealScopeID) && tQ.MyTargetNodeChannel.RealScopeID.Equals(tRealScope)) ||
                        // (string.IsNullOrEmpty(tRealScope) && string.IsNullOrEmpty(tQ.MyTargetNodeChannel.RealScopeID))))
                        LastCountByRealScope[tRealScope]++;
                }
            }
            return LastCountByRealScope[tRealScope];
        }

        /// <summary>
        /// Updates the local host Scope with the latest ScopeID
        /// </summary>
        public static void UpdateLocalHostScope()
        {
            if (MyQueuedSenderList == null || MyQueuedSenderList.Count == 0) return;
            TheQueuedSender tQLH = GetSenderByGuid(TheBaseAssets.LocalHostQSender.MyTargetNodeChannel.cdeMID);
            if (tQLH != null)
                tQLH.UpdateSubscriptionScope(TheBaseAssets.MyScopeManager.ScopeID);
        }

        internal static int Count()
        {
            if (MyQueuedSenderList == null) return 0;
            return MyQueuedSenderList.Count;
        }

        #region CDEPUBSUB Hack - DO NOT SHIP EXCEPT IN CLOUDGATE

        internal static List<Guid> hackMachineGateIDs = new List<Guid>();

        internal static void hackAddMachineGates()
        {

            var tCustomerGates = GetNodesBySubscriptionFragment("TLD_CustomerGate_");
            //if (pSender.MySubscriptionContainsFragment("TLD_CustomerGate_"))
            foreach (TheQueuedSender pSender in tCustomerGates)
            {
                //TheBaseAssets.MySYSLOG.WriteToLog(2824, new TSM("QSRegistry", $"TLD_CustomerGate: {pSender.MyTargetNodeChannel}"));
                if (!pSender.IsAlive)
                    continue;
                var nodes = GetNodesByRScope(pSender.MyTargetNodeChannel.RealScopeID);  //RScope-NOK: Only collects nodes from primary scope but for hack ok
                foreach (Guid Node in nodes)
                {
                    if (!hackMachineGateIDs.Contains(Node) && Node != pSender.MyTargetNodeChannel.cdeMID)
                        hackMachineGateIDs.Add(Node);
                }
            }
            TheBaseAssets.MyServiceHostInfo.TLDCGs = hackMachineGateIDs.Count;
        }

        internal static bool hackIsMachineGate(Guid? DeviceID)
        {
            if (DeviceID == null)
                return false;
            return hackMachineGateIDs.Contains(TheCommonUtils.CGuid(DeviceID));
        }
        internal static List<TheQueuedSender> GetNodesBySubscriptionFragment(string fragment)
        {
            List<TheQueuedSender> tData = new List<TheQueuedSender>();
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ != null && tQ.MySubscriptionContainsFragment(fragment))
                    tData.Add(tQ);
            }
            return tData;
        }

        internal static List<Guid> GetNodesByRScope(string RScope)
        {
            List<Guid> tData = new List<Guid>();
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ != null && tQ.MyTargetNodeChannel != null && tQ.MyTargetNodeChannel.HasRScope(RScope))
                    tData.Add(tQ.MyTargetNodeChannel.cdeMID);
            }
            return tData;
        }
        internal static List<string> GetNodeNamesByRScope(string RScope)
        {
            List<string> tData = new List<string>();
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ != null && tQ.MyTargetNodeChannel != null && tQ.MyTargetNodeChannel.HasRScope(RScope))
                    tData.Add(tQ.GetNodeName());
            }
            return tData;
        }

        internal static string GetNodeNameByNodeID(Guid pNodeID)
        {
            return MyQueuedSenderList.GetEntryByID(pNodeID)?.GetNodeName();
        }
        #endregion



        internal static List<string> GetSenderListNodes()
        {
            List<string> tData = new List<string>();
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ != null && tQ.MyTargetNodeChannel != null)
                    tData.Add(tQ.MyTargetNodeChannel.ToString());
            }
            return tData;
        }
        internal static List<TheQueuedSender> GetISBConnectSender(string pRScopeID)
        {
            //if (!MyQueuedSenderList.TheValues.Any(s => (s.MyISBlock!=null && s.IsAlive==true && s.MyTargetNodeChannel.HasRScope(pRScopeID))))
              //  return null;
            return MyQueuedSenderList.TheValues.Where(s => (s.MyISBlock != null && s.IsAlive == true && s.MyTargetNodeChannel.HasRScope(pRScopeID)))?.ToList();
        }

        internal static List<TheQueuedSender> GetCloudNodes()
        {
            return GetCloudNodes(false);
        }
        internal static List<TheQueuedSender> GetCloudNodes(bool ActivesOnly)
        {
            if (!MyQueuedSenderList.TheValues.Any(s => s.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE || (!ActivesOnly && s.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_BACKCHANNEL)))
                return new List<TheQueuedSender>();
            return MyQueuedSenderList.TheValues.Where(s => s.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE || (!ActivesOnly && s.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_BACKCHANNEL)).ToList();
        }

        internal static bool IsNodeIdInSenderList(Guid pGuid)
        {
            return MyQueuedSenderList.ContainsID(pGuid);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="pSend"></param>
        /// <returns>
        /// 0=False with error
        /// 1=Already exists
        /// 2=New Sender registered
        /// </returns>
        internal static int AddQueuedSender(TheQueuedSender pSend)
        {
            if (pSend == null || pSend.MyTargetNodeChannel == null || pSend.MyTargetNodeChannel.cdeMID == Guid.Empty) return 0;
            try
            {
                //MyQueuedSenderList.MyRecordsLockSlim.EnterUpgradeableReadLock(); // lock (MyQueuedSenderList.MyRecordsLock) //LOCK-REVIEW: Is this look really neccesary here?
                {
                    if (MyQueuedSenderList.ContainsID(pSend.MyTargetNodeChannel.cdeMID)) return 1;
                    MyQueuedSenderList.AddOrUpdateItem(pSend.MyTargetNodeChannel.cdeMID, pSend, null);
                }
                //MyQueuedSenderList.MyRecordsLockSlim.ExitUpgradeableReadLock();
                return 2;
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2824, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QSRegistry", $"Exception during AddQueuedSender for {pSend?.MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l1_Error, e.ToString()));
                return 0;
            }
        }
        internal static bool RemoveQueuedSender(TheChannelInfo pNodeChannel)
        {
            lock (MyQueuedSenderList)
            {
                if (pNodeChannel != null)
                {
                    TheFormsGenerator.DisableNMISubscriptions(pNodeChannel.cdeMID);
                    if (MyQueuedSenderList.ContainsID(pNodeChannel.cdeMID))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2825, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QSRegistry", $"Removed QSender for DeviceID:{TheCommonUtils.GetDeviceIDML(pNodeChannel.cdeMID)}", eMsgLevel.l2_Warning
#if JC_DEBUGCOMM  //No Stacktrace on Platforms other than windows
, TheCommonUtils.GetStackInfo(new System.Diagnostics.StackTrace(true))
#endif
                            ));
                        MyQueuedSenderList.RemoveAnItemByID(pNodeChannel.cdeMID, null);
                        return true;
                    }
                }
                return false;
            }
        }

        internal static TheQueuedSender UpdateQSenderID(Guid oldGuid, Guid newGuid)
        {
            if (MyQueuedSenderList.ContainsID(oldGuid))
            {
                return MyQueuedSenderList.UpdateID(oldGuid, newGuid);
            }
            return null;
        }

        internal static void UpdateServiceState(ref TheEngineState tState)
        {
            if (tState.ConnectedClientsList == null) tState.ConnectedClientsList = new List<Guid>();
            tState.ConnectedClientsList.Clear();
            if (tState.ConnectedNodesList == null) tState.ConnectedNodesList = new List<Guid>();
            tState.ConnectedNodesList.Clear();
            tState.ConnectedClientNodes = 0;
            tState.ConnectedServiceNodes = 0;
            foreach (TheQueuedSender QS in MyQueuedSenderList.TheValues)
            {
                foreach (var t in QS.GetSubscriptionValues())
                {
                    if (t?.RScopeID?.Length > 0)
                    {
                        if (t.Topic.Equals(tState.ClassName) && QS?.MyTargetNodeChannel!=null)
                        {
                            if (TheCommonUtils.IsDeviceSenderType(QS.MyTargetNodeChannel.SenderType))   //IDST-ok: Count clients
                            {
                                tState.ConnectedClientsList.Add(QS.MyTargetNodeChannel.cdeMID);
                                tState.ConnectedClientNodes++;
                            }
                            else
                            {
                                tState.ConnectedNodesList.Add(QS.MyTargetNodeChannel.cdeMID);
                                tState.ConnectedServiceNodes++;
                            }
                        }
                    }
                }
            }
            TheBaseAssets.MySYSLOG.WriteToLog(2826, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QSRegistry", $"Updating Service State for LH:{TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false)} for SubService {tState.ClassName} ", eMsgLevel.l3_ImportantMessage));
        }

        internal static List<string> ShowQSenderDetails(bool ShowQueueDetails)
        {
            List<string> res = new List<string>();
            foreach (TheQueuedSender tQ in MyQueuedSenderList.TheValues)
            {
                if (tQ == null)
                    res.Add("LOCALHOST");
                else
                {
                    string tstree = $"{tQ.MyTargetNodeChannel}={tQ.GetQueLength()} ({tQ.GetLastHeartBeat()}) {tQ.MyTargetNodeChannel.SenderType}";
                    tstree += tQ.GetQueDiag(ShowQueueDetails);
                    res.Add(tstree);
                }
            }
            return res;
        }
        internal static string GetNodesStats(cdeSenderType pNodeType, bool ShowDetails, bool ShowQueueContent)
        {
            string tPrefix = GetPrefixForSenderType(pNodeType);
            string tW = "initial";

            if(tPrefix == "All known Nodes")
                tW = "750px";
            string tableID = tPrefix.Replace(' ', '-').Replace('=', '-');
            StringBuilder outText = new StringBuilder($"<div class=\"cdeInfoBlock\" style=\"max-width:1570px; width:{tW}; min-height:initial;\"><div class=\"cdeInfoBlockHeader cdeInfoBlockHeaderText\" id={tableID}>{tPrefix}" + ": {0} ");
            if(tPrefix != "All known Nodes")
                outText.Append($"<a download=\"{tableID}_{TheCommonUtils.GetMyNodeName()}.csv\" href=\"#\" class=\'cdeExportLink\' onclick=\"return ExcellentExport.csv(this, '{tableID}-table');\">(Export as CSV)</a>");
            outText.Append("</div>");

            int count = 0;
            foreach (KeyValuePair<string, TheQueuedSender> mkey in MyQueuedSenderList.MyRecords)
            {
                if (pNodeType == cdeSenderType.NOTSET && ShowDetails)
                {
                    outText.Append(mkey.Value?.ShowHeader());
                    count++;
                }
                else
                {
                    if (mkey.Value?.MyTargetNodeChannel.SenderType == pNodeType)
                    {
                        if (count == 0)
                        {
                            outText.Append($"<table class=\"cdeHilite\" id=\"{tableID}-table\" style=\"margin-left:1%; \">");
                            outText.Append("<tr><th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:350px; min-width:350px\">Node</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">Scope(s)</th>");
                            if (TheBaseAssets.MyServiceHostInfo.IsCloudService)
                                outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">Signed</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:150px;\">Client Cert Thumb</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">QL</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">SeenB4</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">Alive</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">Connecting</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">Connected</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:150px;\">Last Connection Error</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:150px;\">Connected Since</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:100px;\">Connected For</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:150px;\">Last HB</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">Uses WS</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">In Post</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">In WSPost</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">ST Alive:</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:300px;\">SEID</th>");
                            outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:150px;\">Version</th><tr>");
                        }
                        outText.Append(mkey.Value?.GetQueDiagML(ShowQueueContent, count % 2 == 0));
                        count++;
                    }
                }
            }
            if (count > 0)
            {
                outText.Append("</table></div>");
                try
                {
                    return string.Format(outText.ToString(), count);
                }
                catch { }
            }
            return "";
        }

        internal static string GetPrefixForSenderType(cdeSenderType pSenderType)
        {
            string tPrefix = "Nodes with SenderType=";
            switch (pSenderType)
            {
                case cdeSenderType.CDE_BACKCHANNEL:
                    tPrefix += "BackChannel";
                    break;
                case cdeSenderType.CDE_CLOUDROUTE:
                    tPrefix += "CloudRoutes";
                    break;
                case cdeSenderType.CDE_PHONE:
                    tPrefix += "Phone";
                    break;
                case cdeSenderType.CDE_CUSTOMISB:
                    tPrefix += "Custom ISB";
                    break;
                case cdeSenderType.CDE_DEVICE:
                    tPrefix += "Device";
                    break;
                case cdeSenderType.CDE_JAVAJASON:
                    tPrefix += "JavaJson";
                    break;
                case cdeSenderType.CDE_SERVICE:
                    tPrefix += "Service";
                    break;
                case cdeSenderType.CDE_LOCALHOST:
                    tPrefix += "LocalHost";
                    break;
                default:
                    tPrefix = "All known Nodes";
                    break;
            }
            return tPrefix;
        }

        internal static string AssembleBackChannelSubscriptions(string pTopics, string pRealScopeID)
        {
            string sendTopics = "";
            if (!string.IsNullOrEmpty(pTopics))
                sendTopics = pTopics;
            else
            {
                if (string.IsNullOrEmpty(pRealScopeID)) //No unscoped Subscriptions allowed to be sent to Backchannels or clouds
                    return "";
                List<TheSubscriptionInfo> tTopics = GetCurrentKnownTopics(true, pRealScopeID);
                if (TheBaseAssets.MyScopeManager.IsScopingEnabled && TheBaseAssets.MyScopeManager.IsInScope(pRealScopeID, true))    //4.106 New: only send own BaseEngines as subscriptions to Backchannels if scope of the backchannel matches the current node scope
                {
                    foreach (IBaseEngine tBase in TheThingRegistry.GetBaseEngines(true))
                    {

                        if (tBase.GetEngineState().IsLiveEngine && !ContainsOwnBase(tTopics, tBase.GetEngineName()))
                            tTopics.Add(new TheSubscriptionInfo { Topic = tBase.GetEngineName(), RScopeID = TheBaseAssets.MyScopeManager.GetScrambledScopeID(pRealScopeID, true) }); // TheBaseAssets.MyScopeManager.AddScopeID(tBase.GetEngineName(), null, true)); //BUG here: null should be pRealScopeiD!
                    }
                }
                if (tTopics != null)
                {
                    string tScramble = null;
                    foreach (var topic in tTopics)
                    {
                        if (topic != null && !TheCommonUtils.IsGuid(topic?.Topic))
                        {
                            if (sendTopics.Length > 0) sendTopics += ";";
                            sendTopics += topic.Topic;
                            if (!string.IsNullOrEmpty(pRealScopeID) && !string.IsNullOrEmpty(topic.RScopeID))
                            {
                                if (tScramble == null || !TheBaseAssets.MyServiceHostInfo.EnableFastSecurity)
                                    tScramble = TheBaseAssets.MyScopeManager.GetScrambledScopeID(pRealScopeID, true);   //GRSI: medium freq
                                sendTopics += $"@{tScramble}";
                            }
                        }
                    }
                }
            }
            return sendTopics;
        }

        private static bool ContainsOwnBase(List<TheSubscriptionInfo> tScopedTopic, string pEngineName)
        {
            foreach (var t in tScopedTopic)
            {
                if (t.Topic == pEngineName)
                    return true;
            }
            return false;
        }

        private static readonly List<TSM> MyMasterQueue = new List<TSM>();
        private static readonly object MyMasterQueueLock = new object();
        private static TheQueuedSender MyMasterNodeQS = null;
        internal static bool PublishToMasterNode(TSM pMsg, bool DontQueue=false)
        {
            if (TheBaseAssets.MyServiceHostInfo.MasterNode == Guid.Empty)
            {
                if (!DontQueue)
                {
                    lock (MyMasterQueueLock)
                        MyMasterQueue.Add(pMsg);
                }
                return false;
            }
            if (MyMasterNodeQS==null)
                MyMasterNodeQS = GetSenderByGuid(TheBaseAssets.MyServiceHostInfo.MasterNode);
            if (MyMasterNodeQS == null && !DontQueue)
            {
                lock (MyMasterQueueLock)
                    MyMasterQueue.Add(pMsg);
                return false;
            }
            string tMSGSID = null;
            return TheCommonUtils.CBool(MyMasterNodeQS?.SendQueued(TheBaseAssets.MyScopeManager.AddScopeID($"CDE_SYSTEMWIDE;{TheBaseAssets.MyServiceHostInfo.MasterNode}", TheBaseAssets.MyScopeManager.ScopeID, ref tMSGSID, true, true), pMsg.SetNodeScope(), true, TheBaseAssets.MyServiceHostInfo.MasterNode, "CDE_SYSTEMWIDE", TheBaseAssets.MyScopeManager.ScopeID, null));       //GRSI: rare
        }
        internal static bool SendMasterNodeQueue()
        {
            if (TheBaseAssets.MyServiceHostInfo.MasterNode == Guid.Empty)
                return false;
            TheQueuedSender tSend = GetSenderByGuid(TheBaseAssets.MyServiceHostInfo.MasterNode);
            if (tSend != null)
            {
                lock (MyMasterQueueLock)
                {
                    string tMSGSID = null;
                    foreach (var pMsg in MyMasterQueue)
                        tSend.SendQueued(TheBaseAssets.MyScopeManager.AddScopeID($"CDE_SYSTEMWIDE;{TheBaseAssets.MyServiceHostInfo.MasterNode}", TheBaseAssets.MyScopeManager.ScopeID,ref tMSGSID, true, true), pMsg.SetNodeScope(), true, TheBaseAssets.MyServiceHostInfo.MasterNode, "CDE_SYSTEMWIDE", TheBaseAssets.MyScopeManager.ScopeID, null);     //GRSI: rare
                    MyMasterQueue.Clear();
                }
            }
            return true;
        }

        internal static bool DoPublish(string pTopic, TSM pMsg,bool AddScopeToTopic, Action<TSM> pLocalCallback, bool IsTrustedSender)
        {
            if (string.IsNullOrEmpty(pTopic))
                pTopic = string.Empty;
            bool ScopedAdded = false;
            string tTopicNameOnly = pTopic;
            Guid tDirectGuid = Guid.Empty;
            bool hasDirectGuid = pTopic.Contains(";");
            string tTopicRScope = null;
            if (hasDirectGuid)
            {
                var tTParts = pTopic.Split(';');
                tDirectGuid = TheCommonUtils.CGuid(tTParts[1]);
                tTopicRScope = TheBaseAssets.MyScopeManager.GetRealScopeIDFromTopic(tTParts[0], out tTopicNameOnly);
            }
            else
                tTopicRScope= TheBaseAssets.MyScopeManager.GetRealScopeIDFromTopic(pTopic, out tTopicNameOnly);
            if (string.IsNullOrEmpty(pMsg.SID) && (!string.IsNullOrEmpty(tTopicRScope) || TheBaseAssets.MyScopeManager.IsScopingEnabled))
            {
                if (!string.IsNullOrEmpty(tTopicRScope))
                    pMsg.SID = TheBaseAssets.MyScopeManager.GetScrambledScopeID(tTopicRScope, true);
                else
                    pMsg.SID = TheBaseAssets.MyScopeManager.GetScrambledScopeID();
                TheCDEKPIs.IncrementKPI("ScopeUpdated");
                TheBaseAssets.MySYSLOG.WriteToLog(2827, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QSRegistry", $"No Scope Found in TSM - SID updated with TopicScope:{!string.IsNullOrEmpty(tTopicRScope)}", eMsgLevel.l7_HostDebugMessage));
            }
            if (!MyQueuedSenderList.IsEmpty)
            {
                List<TheQueuedSender> targetQueues = null;
                //NEWV4: Route Optimization if GRO parameter contains known route to originator - even with CDE_SYSTEMWIDE this is more effective and prevents checking all nodes for a CDE_SYSTEMWIDEs target
                if (pMsg != null && !string.IsNullOrEmpty(pMsg.GRO))
                {
                    var tNextNode = pMsg.GetNextNode();
                    if (tNextNode != Guid.Empty)
                    {
                        TheQueuedSender tSend = MyQueuedSenderList.GetEntryByID(tNextNode);
                        if (tSend != null && tSend.IsAlive)
                        {
                            targetQueues = new List<TheQueuedSender> { tSend };
                            TheBaseAssets.MySYSLOG.WriteToLog(2827, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QSRegistry", $"GRO Node Found - Message only sent to {tSend.MyTargetNodeChannel}", eMsgLevel.l7_HostDebugMessage));
                        }
                    }
                }

                if (targetQueues == null && pTopic.StartsWith("CDE_SYSTEMWIDE") && hasDirectGuid)
                {
                    TheQueuedSender tSend = MyQueuedSenderList.GetEntryByFunc(s => s.IsAlive && s.MyTargetNodeChannel?.cdeMID == tDirectGuid);
                    if (tSend != null)
                    {
                        targetQueues = new List<TheQueuedSender> { tSend };
                        TheBaseAssets.MySYSLOG.WriteToLog(2827, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("QSRegistry", $"DirectPub of TXT:{pMsg.TXT} to Target Node {tSend?.MyTargetNodeChannel?.ToMLString()} is connected to this Node!", eMsgLevel.l7_HostDebugMessage));
                    }
                }
                bool MightnotRelay;
                bool IsFromJS;
                if (targetQueues == null)
                {
                    targetQueues = MyQueuedSenderList.TheValues;
                    MightnotRelay = (TheBaseAssets.MyServiceHostInfo.IsCloudService && TheBaseAssets.MyServiceHostInfo.DontRelayNMI && pTopic.StartsWith(eEngineName.NMIService));
                    IsFromJS = MightnotRelay && pMsg.GetOriginator().ToString().Substring(29, 1) == "7";
                }
                else
                {
                    // CODE REVIEW: Should we do the test also for GRO and hadDirectGuid cases? Keeping like this for compatibility for now
                    MightnotRelay = false;
                    IsFromJS = false;
                }

                foreach (TheQueuedSender tSend in targetQueues)
                {
                    if (tSend != null && tSend.IsAlive)
                    {
                        var targetNodeChannel = tSend.MyTargetNodeChannel;
                        if (targetNodeChannel?.IsOneWay == true)
                        {
                            if (!IsTrustedSender || targetNodeChannel.OneWayTSMFilter == null || !MatchTSMFilter(pMsg, targetNodeChannel.OneWayTSMFilter))
                            {
                                // Don't send to this channel
                                continue;
                            }
                        }
                        if (MightnotRelay && !tSend.MyTargetNodeChannel.IsDeviceType && !IsFromJS)
                        {
                            TheCDEKPIs.IncrementKPI(eKPINames.QSNotRelayed);
                            continue;
                        }
                        if (!ScopedAdded && AddScopeToTopic)
                        {
                            ScopedAdded = true;
                            pTopic = TheBaseAssets.MyScopeManager.AddScopeID(pTopic, ref pMsg.SID, true);
                            tTopicRScope = TheBaseAssets.MyScopeManager.ScopeID;
                        }
                        var res=tSend.SendQueued(pTopic, pMsg, true, tDirectGuid, tTopicNameOnly, tTopicRScope, pLocalCallback);
                        if (!TheBaseAssets.MasterSwitch)
                            return res;   //Get out and no more processing
                    }
                }
            }
            return false;
        }

        static char[] wildcard = {'*'};
        private static bool MatchTSMFilter(TSM pMsg, string[] TSMFilter)
        {
            bool match = false;
            foreach(var filter in TSMFilter)
            {
                var filterParts = filter.Split(wildcard, 2);
                if (pMsg.TXT.StartsWith(filterParts[0], StringComparison.Ordinal) && (filterParts.Length<1 || pMsg.TXT.EndsWith(filterParts[1], StringComparison.Ordinal)))
                {
                    match = true;
                    break;
                }
            }
            return match;
        }

        private static DateTimeOffset LastMeshCount = DateTimeOffset.MinValue;
        private static int uniqueMeshes = 0;
        internal static int GetUniqueMeshCount()
        {
            if (DateTimeOffset.Now.Subtract(LastMeshCount).TotalSeconds > 30)
            {
                LastMeshCount = DateTimeOffset.Now;
                uniqueMeshes = MyQueuedSenderList.TheValues.GroupBy(s => s?.MyTargetNodeChannel?.RealScopeID).Distinct().Count();    //RScope-NOK: Does not count AltScopes!
            }
            return uniqueMeshes;
        }

        internal static int GetUnsignedNodeCount()
        {
            int unsignedNodes = MyQueuedSenderList.TheValues.Where(sender => { return !sender.IsTrusted; }).Count();
            return unsignedNodes;
        }

        internal static List<TheSubscriptionInfo> GetCurrentKnownTopics(bool bExcludeCloudRoutes, string pRealScopeID)
        {
            List<TheSubscriptionInfo> retList = new List<TheSubscriptionInfo>();
            foreach (TheQueuedSender srv in MyQueuedSenderList.TheValues)
            {
                if (!bExcludeCloudRoutes || srv.MyTargetNodeChannel.SenderType != cdeSenderType.CDE_BACKCHANNEL)
                    srv.GetUniqueSubscriptions(ref retList, pRealScopeID);
            }
            return retList;
        }

        internal static List<TheNodeTopics> GetNodeTopics(bool ReturnAll=false)
        {
            List<TheNodeTopics> retList = new List<TheNodeTopics>();
            foreach (TheQueuedSender srv in MyQueuedSenderList.TheValues)
            {
                if (srv?.MyTargetNodeChannel == null)
                    continue;
                var t = new TheNodeTopics() { DeviceID=srv.MyTargetNodeChannel.cdeMID, NodeType=srv.MyTargetNodeChannel.SenderType, Topics=new List<string>() };
                List<TheSubscriptionInfo> retSList = new List<TheSubscriptionInfo>();
                srv.GetUniqueSubscriptions(ref retSList, "");
                foreach (var s in retSList)
                {
                    if (s.ToServiceOnly || ReturnAll)
                        t.Topics.Add(s.Topic);
                }
                retList.Add(t);
            }
            return retList;
        }

        //internal static List<string> GetUrlForTopic(string pTopic, bool bExcludeCloudRoutes)
        //{
        //    string tTopicName;
        //    string tTopicRealScope = TheBaseAssets.MyScopeManager.GetRealScopeIDFromTopic(pTopic, out tTopicName);
        //    List<string> retList = new List<string>();
        //    foreach (TheQueuedSender srv in MyQueuedSenderList.TheValues)
        //    {
        //        if ((!bExcludeCloudRoutes || srv.MyTargetNodeChannel.SenderType != cdeSenderType.CDE_BACKCHANNEL) && srv.MySubscriptionContains(tTopicName,tTopicRealScope))
        //        {
        //            retList.Add(srv.MyTargetNodeChannel + (string.IsNullOrEmpty(tTopicRealScope) ? "" : string.Format(" <span style='color:orange'>({0})</span>", tTopicRealScope.Substring(0, 4).ToUpper())));
        //        }
        //    }
        //    return retList;
        //}
        internal static List<string> GetUrlForTopic(TheSubscriptionInfo pTopic, bool bExcludeCloudRoutes)
        {
            List<string> retList = new List<string>();
            foreach (TheQueuedSender srv in MyQueuedSenderList.TheValues)
            {
                if ((!bExcludeCloudRoutes || srv.MyTargetNodeChannel.SenderType != cdeSenderType.CDE_BACKCHANNEL) && srv.MySubscriptionContains(pTopic?.Topic, pTopic?.RScopeID, false))
                {
                    retList.Add(srv.MyTargetNodeChannel?.ToMLString()); // + (string.IsNullOrEmpty(pTopic?.RScopeID) ? "" : $" <span style='color:orange'>({0})</span>", pTopic?.RScopeID.Substring(0, 4).ToUpper())));
                }
            }
            return retList;
        }

        internal static List<TheChannelInfo> GetPublicChannelInfoForTopic(TheSubscriptionInfo pTopic, bool bExcludeCloudRoutes)
        {
            var retList = new List<TheChannelInfo>();
            foreach (TheQueuedSender srv in MyQueuedSenderList.TheValues)
            {
                if ((!bExcludeCloudRoutes || srv.MyTargetNodeChannel.SenderType != cdeSenderType.CDE_BACKCHANNEL) && srv.MySubscriptionContains(pTopic?.Topic,pTopic?.RScopeID, false))    //very low frequency
                {
                    srv.MyTargetNodeChannel.QStatus = srv.GetQueDiag(false);
                    retList.Add(srv.MyTargetNodeChannel.GetPublicChannelInfo());
                }
            }
            return retList;
        }

        private static DateTimeOffset LastMeshInfoCall = DateTimeOffset.MinValue;
        /// <summary>
        /// Returns information about the mesh of the node with the given pID parameter.
        /// If the method has been called too frequently (less than 30 seconds since last call) or
        /// an invalid token or ID is used, the StatusMessage of TheMeshInfoStatus will contain an explanation.
        /// </summary>
        /// <param name="pID">The node ID for the node whose mesh info will be returned.</param>
        /// <param name="token">The token used to access the mesh info</param>
        public static TheMeshInfoStatus GetMeshInfoForNodeID(Guid pID, string token)
        {
            TheMeshInfoStatus meshInfoStatus = new TheMeshInfoStatus();
            if (DateTimeOffset.Now.Subtract(LastMeshInfoCall).TotalSeconds > 5)
            {
                LastMeshInfoCall = DateTimeOffset.Now;
                string meshInfoToken = TheBaseAssets.MySettings.GetSetting("MeshInfoToken");
                if ((string.IsNullOrEmpty(meshInfoToken) && !TheBaseAssets.MyServiceHostInfo.IsCloudService) || (!string.IsNullOrEmpty(meshInfoToken) && token != null && token.Equals(meshInfoToken, StringComparison.OrdinalIgnoreCase)))
                {
                    meshInfoStatus.MeshInfo = new TheMeshInfo();
                    var qs = MyQueuedSenderList.GetEntryByID(pID);
                    if (qs != null)
                    {
                        try
                        {
                            List<TheQueuedSender> allQSInMesh = MyQueuedSenderList.TheValues.Where(sender => sender.MyTargetNodeChannel?.RealScopeID == qs.MyTargetNodeChannel?.RealScopeID).ToList();
                            meshInfoStatus.MeshInfo.NodeIDs = allQSInMesh.Where(s => s.MyTargetNodeChannel != null).Select(s => s.MyTargetNodeChannel.cdeMID).ToList();
                            meshInfoStatus.MeshInfo.TotalMeshSize = allQSInMesh.Count();
                            meshInfoStatus.MeshInfo.MeshSize = allQSInMesh.Where(sender => sender.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_BACKCHANNEL).Count();
                            meshInfoStatus.MeshInfo.ConnectedBrowsers = allQSInMesh.Where(sender => sender.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON).Count();
                            meshInfoStatus.MeshInfo.ScopeIDHash = qs.MyTargetNodeChannel.ScopeIDHash ?? (qs.MyTargetNodeChannel.RealScopeID == null ? "unscoped" : qs.MyTargetNodeChannel.RealScopeID.ToUpper().Substring(0, 4));
                            meshInfoStatus.MeshInfo.NodeDiagInfo = qs.GetNodeDiagInfo();
                        }
                        catch(Exception)
                        {
                            meshInfoStatus.StatusMessage = $"Errors during calculation of Mesh Info for NodeID {pID}.";
                            meshInfoStatus.StatusCode = (int)eHttpStatusCode.Busy;
                        }
                    }
                    else
                        meshInfoStatus.StatusMessage = $"No matching node found for NodeID {pID}.";
                }
                else
                {
                    meshInfoStatus.StatusMessage = "Invalid MeshInfoToken used in request.";
                    meshInfoStatus.StatusCode = (int)eHttpStatusCode.AccessDenied;
                }
            }
            else
            {
                meshInfoStatus.StatusMessage = "Mesh info requested too frequently. Try again in 5 seconds.";
                meshInfoStatus.StatusCode=(int)eHttpStatusCode.Busy;
            }
            return meshInfoStatus;
        }

        #endregion

        #region Cloud Handling

#if CDE_USEWSS8

        /// <summary>
        /// Processes requests coming from IIS/Cloud based nodes
        /// </summary>
        /// <param name="wsSocket">WebSocket used by the request</param>
        /// <param name="pRequestData">Data of the Request</param>
        /// <returns></returns>
        public static async Task ProcessCloudRequest(System.Net.WebSockets.WebSocket wsSocket, TheRequestData pRequestData)
        {
            TheWSProcessor8 tProcessor = null;
            try
            {
                if (pRequestData?.RequestUri?.PathAndQuery?.StartsWith("/ISB")!=true)
                    return;
                TheCDEKPIs.IncrementKPI(eKPINames.KPI3);
                tProcessor = new TheWSProcessor8(wsSocket);
                tProcessor.SetRequest(pRequestData);
                if (pRequestData?.StatusCode==200)
                    await tProcessor.ProcessWS();
            }
            catch (Exception ex)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2828, new TSM("ASHXHandler", "Processing Error", ex.ToString()));
                if (tProcessor != null)
                    tProcessor.Shutdown(true, "ASHX Handler encoutered Error");
            }
            TheCDEKPIs.DecrementKPI(eKPINames.KPI3);
        }
#endif

        /// <summary>
        /// Register Events to determine if a cloud node is up or down
        /// </summary>
        /// <param name="psinkCloudUp">Will be called if the Cloud is up</param>
        /// <param name="psinkCloudDown">Will be called if the cloud is down</param>
        public static void RegisterCloudEvents(Action<ICDEThing, TheChannelInfo> psinkCloudUp, Action<ICDEThing, TheChannelInfo> psinkCloudDown)
        {
            if (psinkCloudDown != null)
            {
                eventCloudIsDown -= psinkCloudDown; //fired if the cloud route is no longer active (cloud service not responding/down/or in maintenance)
                eventCloudIsDown += psinkCloudDown; //fired if the cloud route is no longer active (cloud service not responding/down/or in maintenance)
            }
            if (psinkCloudUp != null)
            {
                eventCloudIsBackUp -= psinkCloudUp; //fired when cloud route is back up
                eventCloudIsBackUp += psinkCloudUp; //fired when cloud route is back up
            }
        }
        /// <summary>
        /// Unregister the cloud events
        /// </summary>
        /// <param name="psinkCloudUp">UP event</param>
        /// <param name="psinkCloudDown">Down Event</param>
        public static void UnregisterCloudEvents(Action<ICDEThing, TheChannelInfo> psinkCloudUp, Action<ICDEThing, TheChannelInfo> psinkCloudDown)
        {
            if (psinkCloudDown != null)
                eventCloudIsDown -= psinkCloudDown;   //fired if the cloud route is no longer active (cloud service not responding/down/or in maintenance)
            if (psinkCloudUp != null)
                eventCloudIsBackUp -= psinkCloudUp;   //fired when cloud route is back up
        }
        /// <summary>
        /// Updates the current cloud routes. If the parameter is zero all cloud connectivity will be discontinued
        /// </summary>
        /// <param name="pCloudUrls">One or more URLs pointing at cloud services separated with ;</param>
        public static void UpdateCloudRoutes(string pCloudUrls)
        {
            UnregisterCloudRoutes();
            TheBaseAssets.MyServiceHostInfo.ServiceRoute = pCloudUrls;
            RegisterCloudRoutes(TheBaseAssets.MyServiceHostInfo.ServiceRoute);
            TheBaseAssets.MySettings.UpdateLocalSettings();
        }

        /// <summary>
        /// Register Cloud Service Routes
        /// </summary>
        /// <param name="pCloudUrls"></param>
        internal static bool RegisterCloudRoutes(string pCloudUrls)
        {
            if (TheBaseAssets.MyServiceHostInfo.IsCloudDisabled)
                return false;
            if (string.IsNullOrEmpty(pCloudUrls))
                return false;

            string[] tCloudRoutes = TheCommonUtils.cdeSplit(pCloudUrls,";",true,true);
            if (tCloudRoutes.Length > 0)
            {
                if (!TheBaseAssets.MyServiceHostInfo.IsCloudService && !TheBaseAssets.MyScopeManager.IsScopingEnabled)  //NEW 3.123: No longer allow Unscoped Access to Cloud
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2829, new TSM("QSRegistry", $"Registering of CloudRoute(s):{pCloudUrls} not allowed for unscoped Relays. Relay has to be scoped first", eMsgLevel.l1_Error));
                    return false;
                }
                foreach (string tCloudUrl in tCloudRoutes)
                {
                    if (TheCommonUtils.IsNullOrWhiteSpace(tCloudUrl))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2829, new TSM("QSRegistry", "Empty ServiceRoute entry found. Will be ignored", eMsgLevel.l2_Warning));
                        continue;
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(2829, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QSRegistry", $"Trying to establish CloudRoute to {tCloudUrl}", eMsgLevel.l4_Message));
                    TheQueuedSender tSender = GetSenderByUrl(tCloudUrl);
                    if (tSender == null || tSender.MyTargetNodeChannel?.SenderType!=cdeSenderType.CDE_CLOUDROUTE)    //There might be an ISBConnect already established to the cloud
                    {
                        //Copied here from HSI to make sure Scope is set before a cloud route is established
                        if (TheBaseAssets.MyServiceHostInfo.RequiresConfiguration && TheBaseAssets.MyServiceHostInfo.ClientCerts?.Count > 0)
                        {
                            string error = "";
                            var tScopes = TheCertificates.GetScopesFromCertificate(TheBaseAssets.MyServiceHostInfo.ClientCerts[0], ref error);
                            if (tScopes?.Count > 0)
                            {
                                TheBaseAssets.MyScopeManager.SetScopeIDFromEasyID(tScopes[0]);
                                TheBaseAssets.MyServiceHostInfo.RequiresConfiguration = false;
                                TheBaseAssets.MySYSLOG.WriteToLog(2823, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommonUtils", $"Scope {tScopes[0]} found in Certificate and used for node", eMsgLevel.l3_ImportantMessage));
                            }
                        }
                        TheCommonUtils.cdeRunAsync("Init Cloud", true, (o) =>
                        {
                            tSender = new TheQueuedSender();    //Has Connected Event
                            tSender.eventConnected -= sinkCloudUp;
                            tSender.eventConnected += sinkCloudUp;
                            tSender.StartSender(new TheChannelInfo(TheBaseAssets.MyScopeManager.GenerateNewAppDeviceID(cdeSenderType.CDE_CLOUDROUTE), TheBaseAssets.MyScopeManager.IsScopingEnabled ? TheBaseAssets.MyScopeManager.ScopeID : null, cdeSenderType.CDE_CLOUDROUTE,tCloudUrl),
                                                TheBaseAssets.MyScopeManager.AddScopeID(TheBaseAssets.MyServiceHostInfo.MyLiveServices, false), false);  //ALLOWED   tSubs must be subscribed during StartSender otherwise a racing condition can happen
                        });
                    }
                }
                return true;
            }
            return false;
        }

        private static void sinkCloudUp(TheQueuedSender pQS, TheChannelInfo pChannel)
        {
            eventCloudIsBackUp?.Invoke(TheCDEngines.MyContentEngine, pChannel);
        }

        /// <summary>
        /// Checks the current status of the configured service route.
        /// </summary>
        /// <param name="checkAll">If multiple routes are configured, only returns true if all routes are connected. Otherwise returns true if at least one route is connected.</param>
        /// <returns>true if connected, false if not connected.</returns>
        public static bool IsServiceRouteConnected(bool checkAll)
        {
            List<TheQueuedSender> tlst = MyQueuedSenderList.TheValues;
            foreach (TheQueuedSender tSender in tlst)
            {
                if (tSender != null && tSender.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE)
                {
                    if (tSender.IsConnected && !tSender.IsConnecting)
                    {
                        if (!checkAll)
                        {
                            return true;
                        }
                    }
                    else if (checkAll)
                    {
                        return false;
                    }
                }
            }
            return checkAll;
        }

        /// <summary>
        /// Re-Initializes the defined Cloud Routes
        /// </summary>
        /// <returns>True if successful</returns>
        public static bool ReinitCloudRoutes()
        {
            UnregisterCloudRoutes();    //Existing Clouds must be unregisted if ServiceRoute entry is (now) empty
            if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ServiceRoute))
            {
                RegisterCloudRoutes(TheBaseAssets.MyServiceHostInfo.ServiceRoute);
                return true;
            }
            return false;
        }


        /// <summary>
        /// Allows to stop all cloud routes
        /// </summary>
        public static void UnregisterCloudRoutes()
        {
            List<TheQueuedSender> tlst = MyQueuedSenderList?.TheValues;
            if (tlst?.Count > 0)
            {
                foreach (TheQueuedSender tSender in tlst)
                {
                    if (tSender != null && tSender.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(28210, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QSRegistry", "Forced Removal of QSender for UnregisterCloudRoutes", eMsgLevel.l4_Message));
                        tSender.DisposeSender(true);
                        tSender.eventConnected -= sinkCloudUp;
                    }
                }
            }
        }

        internal static void UpdateSubscriptionsOfConnectedNodes()
        {
            try
            {
                foreach (TheQueuedSender mkey in GetCloudNodes())
                {
                    mkey.UpdateConnectedNodeWithThisNodeSubscriptions();
                }
            }
            catch
            {
                //ignored
            }
        }
        #endregion

        #region MeshManager (temporary) CODE-REVIEW: How do we make this permanent?

        static readonly cdeConcurrentDictionary<Guid, TheQueuedSender> sessionToSenderDict = new cdeConcurrentDictionary<Guid, TheQueuedSender>();

        /// <summary>
        /// Connects to an unconfigured Node
        /// </summary>
        /// <param name="nodeId">DeviceID of the node to connect to</param>
        /// <param name="targetUrl">URL of the Node to connect to</param>
        /// <param name="token">Cancellation token to stop connection if running to long</param>
        /// <returns></returns>
        public static Task<Guid> ConnectToUnconfiguredRelayAsync(Guid nodeId, string targetUrl, CancellationToken token)
        {
            var channel = new TheChannelInfo(nodeId, cdeSenderType.CDE_SERVICE, targetUrl);
            TheQueuedSender tSend = new TheQueuedSender();
            if (tSend.StartSender(channel, null, false))    //CODEREVIEW: @Markus: you might have to subscribe to the MeshManager Topic?
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2352, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("MeshManager", $"Started Sender for node:{targetUrl} - {TheCommonUtils.GetDeviceIDML(nodeId)}"));
                //var nodeId = TheCommonUtils.CGuid(tSend.ToString().Split(';')[1]); // TODO: Turn this into an API...

                var r = new TheRequestData
                {
                    DeviceID = nodeId,
                    RemoteAddress = targetUrl,
                };

                var session = TheBaseAssets.MySession.CreateSession(r, Guid.Empty);
                if (string.IsNullOrEmpty(session.RSAKey))
                    TheCommonUtils.CreateRSAKeys(session);
                TSM tTsmRsa = new TSM(eEngineName.ContentService, "CDE_REQUEST_RSA:" + session.RSAPublic);
                TheCommCore.PublishToNode(nodeId, tTsmRsa);

                if (!token.IsCancellationRequested)
                {

                    return Task<Guid>.Factory.StartNew(() =>
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2352, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("MeshManager", $"Waiting for RSA public key from node:{targetUrl} - {TheCommonUtils.GetDeviceIDML(nodeId)}"));
                        while (tSend.MyTargetNodeChannel.MySessionState == null || String.IsNullOrEmpty(tSend.MyTargetNodeChannel.MySessionState.RSAPublicSend))
                        {
                            if (token.IsCancellationRequested || !TheBaseAssets.MasterSwitch)
                            {
                                return Guid.Empty;
                            }
                            TheCommonUtils.SleepOneEye(100, 100);
                            // TODO improve this: use a signaling mechanism etc.
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(2352, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("MeshManager", $"Received RSA public key from node:{targetUrl} - {TheCommonUtils.GetDeviceIDML(nodeId)}"));
                        while (tSend?.MyTargetNodeChannel?.MySessionState == null)
                        {
                            if (token.IsCancellationRequested || !TheBaseAssets.MasterSwitch)
                            {
                                return Guid.Empty;
                            }
                            TheCommonUtils.SleepOneEye(100, 100);
                            // TODO improve this: use a signaling mechanism etc.
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(2352, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("MeshManager", $"SEID is valid for node:{targetUrl} - {TheCommonUtils.GetDeviceIDML(nodeId)}. Session Id: {tSend?.MyTargetNodeChannel?.MySessionState?.cdeMID}"));

                        sessionToSenderDict.TryAdd(tSend.MyTargetNodeChannel.MySessionState.cdeMID, tSend);
                        return tSend.MyTargetNodeChannel.MySessionState.cdeMID;
                    });
                }
            }
            TheBaseAssets.MySYSLOG.WriteToLog(2352, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("MeshManager", $"Failed to create sender for node:{targetUrl} - {TheCommonUtils.GetDeviceIDML(nodeId)}"));

            return Task<Guid>.Factory.StartNew(() => Guid.Empty);
        }

        /// <summary>
        /// Disconnects a unconfigured Node
        /// </summary>
        /// <param name="sessionId">Session ID of the Node </param>
        public static void DisconnectUnconfiguredRelay(Guid sessionId)
        {
            if (sessionToSenderDict.TryGetValue(sessionId, out TheQueuedSender sender))
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2352, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("MeshManager", $"Closing sender for unconfigured relay with Session Id: {sessionId}", ""));
                sender.DisposeSender(true);
                sessionToSenderDict.RemoveNoCare(sessionId);
            }
        }
        #endregion

        /// <summary>
        /// new V4: Gets a ISB Connect Class to establish a new Connection to the CDE
        /// </summary>
        /// <param name="pRequestData">Incoming Request Data</param>
        /// <param name="tQ">List of Command Line Options</param>
        /// <param name="pSenderType">Target Sender Type</param>
        /// <returns></returns>
        public static TheISBConnect GetMyISBConnect(TheRequestData pRequestData, Dictionary<string, string> tQ, cdeSenderType pSenderType)
        {
            var tRes = new TheISBConnect();
            if (pRequestData.PostData != null && pRequestData.PostData.Length>0)
            {
                try
                {
                    TSM tSM = TheCommonUtils.DeserializeJSONStringToObject<TSM>(TheCommonUtils.CArray2UTF8String(pRequestData.PostData));
                    if (tSM?.PLB==null)
                    {
                        tRes.ERR = "No ISBConnect Request found";
                        return tRes;
                    }
                    string tPLS = TheCommonUtils.Decrypt(tSM.PLB, TheBaseAssets.MySecrets.GetNodeKey());    //Only Post encrypted with this deviceID will be accepted;
                    if (string.IsNullOrEmpty(tPLS))
                    {
                        tRes.ERR = "Decryption failed - illegal request";
                        return tRes;
                    }
                    var tReq = TheCommonUtils.DeserializeJSONStringToObject<TheISBRequest>(tPLS);
                    if (tReq == null || !TheBaseAssets.MyScopeManager.IsValidScopeID(tReq.SID))
                    {
                        tRes.ERR = "Request was not negotiated from this node.";
                        return tRes;
                    }
                    pSenderType = tReq.SenderType;
                    pRequestData.SessionState.MyDevice = tReq.DeviceID;
                    tRes.AT = TheBaseAssets.MyServiceHostInfo.ApplicationTitle;
                    tRes.MCS = TheBaseAssets.MyServiceHostInfo.MainConfigScreen;
                    tRes.PS = tQ?.ContainsKey("PS") == true ? tQ["PS"] : TheCDEngines.MyNMIService.MyNMIModel.MainDashboardScreen.ToString();
                    tRes.SID= TheBaseAssets.MyScopeManager.GetScrambledScopeID();     //GRSI: rare
                    tRes.FNI = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString();
                    tRes.WSP = TheBaseAssets.MyServiceHostInfo.MyStationWSPort;
                    tRes.TLS = TheCommonUtils.CUri(TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false),true)?.IsUsingTLS(tRes.WSP)==true;
                    return tRes;
                }
                catch
                {
                    tRes.ERR = "GetMyISBConnect failed";
                    return tRes;
                }
            }
            pRequestData.SessionState.MyDevice = TheBaseAssets.MyScopeManager.GenerateNewAppDeviceID(pSenderType);
            TheQueuedSender tSend = new TheQueuedSender();
            if (tSend.StartSender(new TheChannelInfo(pRequestData.SessionState.MyDevice, pSenderType, pRequestData.SessionState), null, true))
            {
                tSend.eventErrorDuringUpload += TheCommCore.OnCommError;
                tRes.NPA = TheBaseAssets.MyScopeManager.GetISBPath(TheBaseAssets.MyServiceHostInfo.RootDir, pSenderType, TheCommonUtils.GetOriginST(tSend.MyTargetNodeChannel), pRequestData.SessionState.GetNextSerial(), pRequestData.SessionState.cdeMID, pRequestData.WebSocket != null);
                if (TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.IIS && TheBaseAssets.MyServiceHostInfo.MyStationWSPort != 0 && !tRes.NPA.EndsWith(".ashx"))
                    tRes.NPA += ".ashx";
                tRes.FNI = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString();
                if (pSenderType != cdeSenderType.CDE_JAVAJASON)
                {
                    if (string.IsNullOrEmpty(pRequestData.SessionState.SScopeID)) //pSenderType!=cdeSenderType.CDE_JAVAJASON : TROUBLE: If SScopeID is null, JS cannot login; if SScopeID is ! Null, PublishCentral mesages go already to JS
                        pRequestData.SessionState.SScopeID = TheBaseAssets.MyScopeManager.GetScrambledScopeID();     //GRSI: rare
                    tRes.SID = pRequestData.SessionState.SScopeID;
                }
                else
                {
                    var tUriPath = TheCommonUtils.CUri(pRequestData?.SessionState?.InitReferer, true);
                    ThePageDefinition tPageDefinition = tUriPath == null ? null : TheNMIEngine.GetPageByRealPage(tUriPath.PathAndQuery);
                    if (tPageDefinition != null && tPageDefinition.IncludeCDE && (TheUserManager.HasSessionValidUser(pRequestData?.SessionState) || (tPageDefinition.IsPublic && !tPageDefinition.RequireLogin)))
                    {
                        pRequestData.SessionState.SScopeID = TheBaseAssets.MyScopeManager.GetScrambledScopeID();     //GRSI: rare
                        tRes.SID = TheBaseAssets.MyScopeManager.GetScrambledScopeID();
                    }
                    else
                        pRequestData.SessionState.SScopeID = null;
                }
                tRes.AT = TheBaseAssets.MyServiceHostInfo.ApplicationTitle;
                tRes.MCS = TheBaseAssets.MyServiceHostInfo.MainConfigScreen;
                tRes.PS = tQ?.ContainsKey("PS") == true ? tQ["PS"] : TheCDEngines.MyNMIService.MyNMIModel.MainDashboardScreen.ToString();
                tRes.WSP = TheBaseAssets.MyServiceHostInfo.DisableWebSockets ? 0: TheBaseAssets.MyServiceHostInfo.MyStationWSPort;
                tRes.TLS = TheCommonUtils.CUri(TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false), true)?.IsUsingTLS(tRes.WSP) == true;
                if (TheUserManager.HasSessionValidUser(pRequestData?.SessionState))
                {
                    TheUserDetails tUser = TheUserManager.GetUserByID(pRequestData.SessionState.CID);
                    if (tUser != null)
                    {
                        tRes.UNA = tUser.Name;
                        tRes.LCI = tUser.LCID;
                        var tHomeScreen = tQ?.ContainsKey("HS") == true ? tQ["HS"] : TheUserManager.GetUserHomeScreen(pRequestData, tUser);
                        if (!string.IsNullOrEmpty(tHomeScreen))
                        {
                            tRes.SSC = tHomeScreen.Split(';')[0];
                            tRes.PS = tHomeScreen.Split(';').Length > 1 ? tHomeScreen.Split(';')[1] : "";
                        }
                    }
                }
            }
            else
            {
                tRes.ERR = "Qsender could not be created";
                TheBaseAssets.MySYSLOG.WriteToLog(299, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"GetMyISBConnect: QueuedSender could not be created for DeviceID:{TheCommonUtils.GetDeviceIDML(pRequestData.DeviceID)}", eMsgLevel.l7_HostDebugMessage));
            }
            return tRes;
        }
    }
}
