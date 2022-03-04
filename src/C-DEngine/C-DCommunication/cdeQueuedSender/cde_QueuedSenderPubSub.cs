// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;
using System.Linq;

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.StorageService;
using System;

namespace nsCDEngine.Communication
{
    internal partial class TheQueuedSender
    {
        private readonly TheMirrorCache<TheSubscriptionInfo> MySubscriptions;

        #region Subscription Management

        /// <summary>
        /// Returns true if new subscriptions were added
        /// </summary>
        /// <param name="pScopedTopics"></param>
        /// <param name="OwnedSubs"></param>
        /// <returns></returns>
        internal bool Subscribe(string pScopedTopics, bool OwnedSubs=false)
        {
            int len = MySubscriptions.Count;
            CombineSubscriptions(pScopedTopics, out bool WasUpdated, OwnedSubs);
            if (WasUpdated) //len != MySubscriptions.Count ||
            {
                TheBaseAssets.MySYSLOG.WriteToLog(296, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"New # of Subscriptions {MySubscriptions?.Count} - Previously {len} for {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l4_Message));
                return true;
            }
            return false;
        }

        internal bool Unsubscribe(string pTopics, bool keepAlive=false)
        {
            int len = MySubscriptions.Count;
            ReduceSubscriptions(pTopics); //, false);
            if (!keepAlive && MyTargetNodeChannel.SenderType != cdeSenderType.CDE_LOCALHOST && len > 0 && MySubscriptions.Count == 0)
            {
                FireSenderProblem(new TheRequestData() { ErrorDescription = "1307:No more subscriptions - QSender will be removed" });
                return true;
            }
            if (len != MySubscriptions.Count) return true;
            return false;
        }





        internal ICollection<string> CombineSubscriptions(string pTopics, out bool WasUpdated, bool AreOwnedSubs=false)
        {
            WasUpdated = false;
            if (string.IsNullOrEmpty(pTopics)) return MySubscriptions.TheKeys;
            lock (CombineSubscriptionLock)
            {
                List<string> subs = TheCommonUtils.CStringToList(pTopics, ';');
                return CombineSubscriptions(subs, out WasUpdated, AreOwnedSubs);
            }
        }


        private readonly object CombineSubscriptionLock = new object();

        internal ICollection<string> CombineSubscriptions(List<string> pTopics, out bool WasUpdated, bool AreOwnedSubs = false)
        {
            WasUpdated = false;
            if (pTopics == null) //pTopics = new List<string>();
                return MySubscriptions.TheKeys; //4.1042: Tuning
            lock (CombineSubscriptionLock)
            {
                for (int i = 0; i < pTopics.Count; i++)
                {
                    string tSubTopicRealScope = TheBaseAssets.MyScopeManager.GetRealScopeIDFromTopic(pTopics[i], out string tTopicName);       //Medium Frequency
                    if (string.IsNullOrEmpty(tTopicName)) continue;
                    if (MyTargetNodeChannel?.HasRScope(tSubTopicRealScope)!=true)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(296, string.IsNullOrEmpty(tTopicName) || TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"New Subscription to Topic={pTopics[i]} not allowed due to different Scope", eMsgLevel.l2_Warning));
                        continue;
                    }
                    if (string.IsNullOrEmpty(tSubTopicRealScope) && !TheBaseAssets.MyServiceHostInfo.AllowUnscopedMesh)
                    {
                        if (TheBaseAssets.MyScopeManager.IsScopingEnabled && !string.IsNullOrEmpty(MyTargetNodeChannel.RealScopeID) && !TheCommonUtils.IsDeviceSenderType(MyTargetNodeChannel.SenderType))  //RScope-OK //IDST-??: check of unscoped telegrams - might need to go to unscoped devices?
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(296, string.IsNullOrEmpty(tTopicName) || TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"New Subscription to UNSCOPED Topic={pTopics[i]} not allowed as current node is Scoped!", eMsgLevel.l2_Warning));
                            continue;
                        }
                        //Coming later for more security: No more Unscoped subscriptions except the plugin service has "IsAllowedUnscopedProcessing" enabled
                        //if (!TheBaseAssets.MyScopeManager.IsScopingEnabled && string.IsNullOrEmpty(tChannelRealScope) && pTopics[i] != eEngineName.ContentService && pTopics[i] != eEngineName.NMIService)
                        //{
                        //    TheBaseAssets.MySYSLOG.WriteToLog(296, string.IsNullOrEmpty(tTopicName) || TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"New Subscription to UNSCOPED Topic={pTopics[i]} not allowed on unscoped Node!", eMsgLevel.l2_Warning));
                        //    continue;
                        //}
                    }
                    bool WasFound = false;
                    foreach (string t in MySubscriptions.TheKeys)
                    {
                        if (!string.IsNullOrEmpty(tSubTopicRealScope))
                        {
                            if (tSubTopicRealScope.Equals(MySubscriptions.MyRecords[t].RScopeID) && tTopicName.Equals(MySubscriptions.MyRecords[t].Topic))
                            {
                                WasFound = true;
                                break;
                            }
                        }
                        else
                        {
                            if (pTopics[i].Equals(t))
                            {
                                WasFound = true;
                                break;
                            }
                        }
                    }
                    if (!WasFound)
                    {
                        MySubscriptions.AddOrUpdateItemKey(pTopics[i], new TheSubscriptionInfo() { RScopeID = tSubTopicRealScope, Topic = tTopicName, ToServiceOnly=AreOwnedSubs }, null);
                        WasUpdated = true;
                    }
                }
                return MySubscriptions.TheKeys;
            }
        }

        private void ReduceSubscriptions(string pTopics)
        {
            if (string.IsNullOrEmpty(pTopics)) return;

            TheBaseAssets.MySYSLOG.WriteToLog(296, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"Unsubscribe request of Topics={pTopics} from {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l4_Message));

            lock (CombineSubscriptionLock)
            {
                string[] subs = pTopics.Split(';');
                for (int i = 0; i < subs.Length; i++)
                {
                    string tRealScope = TheBaseAssets.MyScopeManager.GetRealScopeIDFromTopic(subs[i], out string tTopicName);  //Low Frequency
                    if (string.IsNullOrEmpty(tTopicName)) continue;
                    foreach (string t in MySubscriptions.TheKeys)
                    {
                        if (!string.IsNullOrEmpty(tRealScope))
                        {
                            if (tRealScope.Equals(MySubscriptions.MyRecords[t]?.RScopeID) && tTopicName.Equals(MySubscriptions.MyRecords[t]?.Topic))
                            {
                                MySubscriptions.RemoveAnItemByKey(t, null);
                                break;
                            }
                        }
                        else
                        {
                            if (subs[i].Equals(t))
                            {
                                MySubscriptions.RemoveAnItemByKey(t, null);
                                break;
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// this is the ONLY method that is allowed to update the RealScopeID of MyTargetNodeChannel
        /// </summary>
        /// <param name="pRealScopeID"></param>
        internal void UpdateSubscriptionScope(string pRealScopeID)
        {
            MyTargetNodeChannel.SetRealScopeID(pRealScopeID); // TheBaseAssets.ScopeID;
            lock (CombineSubscriptionLock)
            {
                foreach (string t in MySubscriptions.TheKeys)
                {
                    MySubscriptions.MyRecords[t].RScopeID = pRealScopeID;
                    MySubscriptions.UpdateID(t, TheBaseAssets.MyScopeManager.AddScopeID(MySubscriptions.MyRecords[t].Topic,true, pRealScopeID));     //GRSI: rare
                }
            }
        }


        /// <summary>
        /// probes if the given subscription is owned by this QSender
        /// </summary>
        /// <param name="pTopic"></param>
        /// <returns></returns>
        internal bool MySubscriptionContains(string pTopic)
        {
            string tTopicRealScope = TheBaseAssets.MyScopeManager.GetRealScopeIDFromTopic(pTopic, out string tTopicName);  //no reference anymore
            return MySubscriptionContains(tTopicName, tTopicRealScope, false);
        }

        internal bool MySubscriptionContains(string pRealTopic, string pRealScope, bool RecordMatch)
        {
            if (string.IsNullOrEmpty(pRealTopic))
                return false;
            if (RecordMatch)
            {
                var t=MySubscriptions.MyRecords.Values.FirstOrDefault(s => s.Topic == pRealTopic && s.RScopeID == pRealScope);
                if (t!=null)
                {
                    t.Hits++;
                }
                return t != null;
            }
            return MySubscriptions.MyRecords.Any(s => s.Value.Topic == pRealTopic && s.Value.RScopeID == pRealScope);
        }

        internal bool MySubscriptionContainsFragment(string pRealTopic)
        {
            return MySubscriptions.MyRecords.Any(s => s.Value?.Topic?.StartsWith(pRealTopic)==true && MyTargetNodeChannel.HasRScope(s.Value?.RScopeID));
        }

        #region Return Subsciptions

        internal bool GetUniqueSubscriptions(ref List<TheSubscriptionInfo> pTopics, string pRealScopeID)
        {
            //lock (CombineSubscriptionLock)
            //{
                //string tQS = "";
                foreach (var t in MySubscriptions.TheValues)
                {
                    bool WasFound = false;
                    foreach (var tInSub in pTopics)
                    {
                        //string tTopicName = "";
                        //tQS = TheBaseAssets.MyScopeManager.GetRealScopeIDFromTopic(pTopics[i], out tTopicName); //Low Frequency
                        if (string.IsNullOrEmpty(tInSub.Topic)) continue;
                        if (!string.IsNullOrEmpty(tInSub.RScopeID))
                        {
                            if (tInSub.RScopeID.Equals(t.RScopeID) && tInSub.Topic.Equals(t.Topic))
                            {
                                WasFound = true;
                                break;
                            }
                        }
                        else
                        {
                            if (t.Topic.Equals(tInSub.Topic) && string.IsNullOrEmpty(t.RScopeID))
                            {
                                WasFound = true;
                                break;
                            }
                        }
                    }
                    if (!WasFound && (string.IsNullOrEmpty(pRealScopeID) || pRealScopeID.Equals(t.RScopeID)))
                        pTopics.Add(t);
                }
                return true;
            //}
        }

        internal ICollection<string> GetSubscriptions()
        {
            return MySubscriptions.TheKeys;
        }
        internal ICollection<TheSubscriptionInfo> GetSubscriptionValues()
        {
            return MySubscriptions.TheValues;
        }

        internal string GetNodeName()
        {
            string tNodeName;
            if (MyNodeInfo?.MyServiceInfo != null)
            {
                tNodeName = MyNodeInfo.MyServiceInfo.NodeName;
                if (string.IsNullOrEmpty(tNodeName))
                    tNodeName = MyNodeInfo.MyServiceInfo.MyStationName;
            }
            else
                tNodeName = $"{MyTargetNodeChannel?.cdeMID}";
            return tNodeName;
        }

        public string ShowHeader()
        {
            string AllTopics = "";
            foreach (var t in MySubscriptions.TheValues)
            {
                if (t==null) continue;
                string tColor = "black";
                string tVer = "";
                if (TheBaseAssets.MyServiceHostInfo.IsCloudService)
                {
                    var tS = MyNodeInfo?.MyServices?.FirstOrDefault(s => s?.ClassName == t.Topic);
                    if (tS != null)
                    {
                        tVer = $" V{tS.Version}";
                        if (tS.IsMiniRelay)
                            tColor = "purple";
                    }
                }
                if (!t.ToServiceOnly)
                    tColor = "lightgray";
                string tt = $"<li style='color:{tColor}'>{t.Topic}{tVer}";
                if (!string.IsNullOrEmpty(t.RScopeID))
                {
                    string tScop = t.RScopeID;
                    if (tScop.Length < 4)
                        tt += $" <span style='color:red'>ILLEGAL-Scope: {tScop.ToUpper()}</span>";
                    else
                        tt += $" {cdeStatus.GetScopeHashML(tScop.Substring(0, 4).ToUpper())}";
                }


                int tMinAlive = TheCommonUtils.CInt((DateTimeOffset.Now - t.cdeCTIM).TotalMinutes);
                tt += $" ({Math.Round(t.Hits>0 && tMinAlive>0?t.Hits/tMinAlive:0.0,2)} , {t.Hits})</li>";
                AllTopics += tt;
            }
            return $"<li {(!IsTrusted? "style='color:red'" : "")}>{MyTargetNodeChannel.ToMLString()} ({GetNodeName()}) {(MyNodeInfo?.MyServiceInfo?.ClientCertificateThumb?.Length > 3 ? MyNodeInfo?.MyServiceInfo?.ClientCertificateThumb?.Substring(0, 4) : "No cert")}<ul>{AllTopics}</ul></li>";
        }

        #endregion

        internal void UpdateConnectedNodeWithThisNodeSubscriptions()
        {
            if (MyISBlock != null) return; //Dont do this for Custom Senders
            string pTopics= TheBaseAssets.MyScopeManager.AddScopeID(TheBaseAssets.MyServiceHostInfo.MyLiveServices,false,MyTargetNodeChannel.RealScopeID); //RScope-OK: Engine Topic subscriptions on primary RScope only
            List<string> AllTopics = CombineSubscriptions(pTopics, out bool WasUpdated).ToList();
            if (WasUpdated)
            {
                TSM tTsm = new TSM("CLOUDSYNC", "CDE_SUBSCRIBE", TheCommonUtils.CListToString(AllTopics, ";")) { SID = TheBaseAssets.MyScopeManager.GetScrambledScopeID(MyTargetNodeChannel.RealScopeID, true) };       //GRSI: rare
                tTsm.SetNoDuplicates(true);
                SendQueued(TheBaseAssets.MyScopeManager.AddScopeID(eEngineName.ContentService,true, MyTargetNodeChannel.RealScopeID), tTsm,false, Guid.Empty, eEngineName.ContentService, MyTargetNodeChannel.RealScopeID, null);     //RScope-OK: Engine Topic subscriptions on primary RScope only
            }
        }
        #endregion

    }
}
