// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// TheThingEngine of the C-DEngine. Manages and communication with Things
    /// </summary>
    public class TheThingEngine : ThePluginBase
    {
#region ICDEPlugin Methods
        /// <summary>
        /// Sets plugin information
        /// </summary>
        /// <param name="pBase">The C-DEngine is creating a Host for this engine and hands it to the Plugin Service</param>
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
            MyBaseEngine.SetEngineName(eEngineName.ThingService);        //Can be any arbitrary name - recommended is the class name
            MyBaseEngine.SetFriendlyName("The Thing Service");

            MyBaseEngine.GetEngineState().IsAllowedUnscopedProcessing = TheBaseAssets.MyServiceHostInfo.IsCloudService;
            MyBaseEngine.SetEngineID(new Guid("{BDCD0A12-37CD-4F8A-A96A-EEC7117C9863}"));

            MyBaseEngine.SetVersion(TheBaseAssets.BuildVersion);

            MyThingRegistry = new TheThingRegistry();
            MyThingRegistry.Init(); //This is now synchron. sinkThingsReady was never doing anything as mIsInitialized is set much later
        }
#endregion

        /// <summary>
        /// Initializes The Thing Engine
        /// </summary>
        /// <returns></returns>
        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);        //Event when C-DEngine has new Telegram for this service as a subscriber (Client Side)
            MyBaseEngine.SetInitialized(null);
            mIsInitialized = true;
            return true;
        }

        private sealed class thingSendStatus
        {
            public DateTimeOffset lastSend;
            public bool Pending;
        }

        private static readonly cdeConcurrentDictionary<Guid, thingSendStatus> lastGlobalThingSendPerNode = new ();
        private static void SendGlobalThings(Guid targetNodeId)
        {
            // Do not send more often than every 60 seconds
            if (lastGlobalThingSendPerNode.TryGetValue(targetNodeId, out thingSendStatus timeAndPending) && DateTimeOffset.Now.Subtract(timeAndPending.lastSend).TotalSeconds < 59)
                return;
            var globalThings = TheThingRegistry.GetThingsByFunc("*", (t) => TheThing.GetSafePropertyBool(t, "IsRegisteredGlobally") && t.IsOnLocalNode());
            if (globalThings != null)
            {
                TSM tTSM = new (eEngineName.ContentService, "CDE_SYNC_THINGS", TheCommonUtils.SerializeObjectToJSONString(globalThings));
                tTSM.SetToServiceOnly(true);
                if (targetNodeId == Guid.Empty)
                    TheCommCore.PublishCentral(tTSM);
                else
                    TheCommCore.PublishToNode(targetNodeId, tTSM);
                lastGlobalThingSendPerNode[targetNodeId] = new thingSendStatus { lastSend = DateTimeOffset.Now, Pending = false };
            }
        }

#region Private Variables
        internal TheThingRegistry MyThingRegistry;
#endregion

        /// <summary>
        /// Message Handler of TheThingEngine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="pIncoming"></param>
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            if (pIncoming is not TheProcessMessage pMsg || pMsg.Message == null) return;
            string[] tCmd = pMsg.Message.TXT.Split(':');
            switch (tCmd[0])   //string 2 cases
            {
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);
                    break;
                case "CDE_INITIALIZE":
                        if (!MyBaseEngine.GetEngineState().IsEngineReady)
                            MyBaseEngine.SetInitialized(pMsg.Message);
                        MyBaseEngine.ReplyInitialized(pMsg.Message);
                    break;

                case "CDE_REGISTERPROPERTY":
                    if (tCmd.Length > 1)
                    {
                        TheThing tThing2 = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(tCmd[1]));
                        if (tThing2 != null)
                        {
                            cdeP tProp = tThing2.GetProperty(pMsg.Message.PLS);
                            if (tProp == null) return;
                            tProp.SetPublication(true,pMsg.Message.GetOriginator());    //OK - Very late "Binding"
                        }
                    }
                    break;

                case "CDE_REGISTERTHING":
                    if (MyThingRegistry != null)
                    {
                        //if (TheScopeManager.IsNodeTrusted(pMsg.Message.GetOriginator())) // CODE-REVIEW: This security enhancement will not allow Global Things to work anymore. WE need to bring this back when we have the meshmanager working
                        //{
                        //    TheBaseAssets.MySYSLOG.WriteToLog(7678, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ContentService, String.Format("Register Thing from untrusted node received {0} - disallowed", pMsg.Message.GetOriginator()), eMsgLevel.l3_ImportantMessage));
                        //    return;
                        //}
                        TheThing tThing = TheCommonUtils.DeserializeJSONStringToObject<TheThing>(pMsg.Message.PLS);
                        if (tThing != null)
                            TheThingRegistry.RegisterThing(tThing);
                    }
                    break;
                case "CDE_UNREGISTERTHING":
                    if (MyThingRegistry != null)
                    {
                        //if (TheScopeManager.IsNodeTrusted(pMsg.Message.GetOriginator()))
                        //{
                        //    TheBaseAssets.MySYSLOG.WriteToLog(7678, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ContentService, String.Format("Unregister Thing from untrusted node received {0} - disallowed", pMsg.Message.GetOriginator()), eMsgLevel.l3_ImportantMessage));
                        //    return;
                        //}
                        TheThingRegistry.DeleteThingByID(TheCommonUtils.CGuid(pMsg.Message.PLS));
                    }
                    break;
                case "CDE_SETP":
                    if (!string.IsNullOrEmpty(pMsg.Message.PLS) && tCmd.Length>1)
                    {
                        TheThing tG = TheThingRegistry.GetThingByMID(TheCommonUtils.CGuid(tCmd[1]),true);
                        if (tG!=null)
                        {
                            cdeP p = TheCommonUtils.DeserializeJSONStringToObject<cdeP>(pMsg.Message.PLS);
                            if (p != null)
                                tG.UpdatePropertyInBag(p, true, true);
                        }
                    }
                    break;
                case "CDE_SYNC_THINGS":
                    if (pMsg.Message.PLS.Length > 2)
                    {
                        List<TheThing> tList = TheCommonUtils.DeserializeJSONStringToObject<List<TheThing>>(pMsg.Message.PLS);
                        TheBaseAssets.MySYSLOG.WriteToLog(7678, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, String.Format("CDE_SYNC_THINGS: Received for node {0}", pMsg.Message.GetOriginator()), eMsgLevel.l3_ImportantMessage)); 
                        TheThingRegistry.SyncGlobalThings(pMsg.Message.GetOriginator(), tList);
                    }
                    break;
                case "CDE_SEND_GLOBAL_THINGS":
                    SendGlobalThings(pMsg.Message.GetOriginator());
                    break;
                case "CDE_REGISTERRULE":
                    var tEngs=TheThingRegistry.GetBaseEnginesByCap(eThingCaps.RulesEngine);
                    foreach (var t in tEngs)
                        t.GetBaseThing()?.HandleMessage(sender, pMsg);
                    break;
            }
        }

    }
}
