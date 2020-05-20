// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;

namespace nsCDEngine.Engines
{
    internal class TheMiniRelayEngine : ICDEPlugin, ICDEThing
    {
        #region ICDEThing Methods
        public void SetBaseThing(TheThing pThing)
        {
            MyBaseThing = pThing;
        }
        public TheThing GetBaseThing()
        {
            return MyBaseThing;
        }
        public cdeP GetProperty(string pName, bool DoCreate)
        {
            if (MyBaseThing != null)
                return MyBaseThing.GetProperty(pName, DoCreate);
            return null;
        }
        public cdeP SetProperty(string pName, object pValue)
        {
            if (MyBaseThing != null)
                return MyBaseThing.SetProperty(pName, pValue);
            return null;
        }
        public void RegisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            if (MyBaseThing != null)
                MyBaseThing.RegisterEvent(pName, pCallBack);
        }
        public void UnregisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            if (MyBaseThing != null)
                MyBaseThing.UnregisterEvent(pName, pCallBack);
        }
        public void FireEvent(string pEventName, ICDEThing sender, object pPara, bool FireAsync)
        {
            if (MyBaseThing != null)
                MyBaseThing.FireEvent(pEventName, sender, pPara, FireAsync);
        }
        public bool HasRegisteredEvents(string pEventName)
        {
            if (MyBaseThing != null)
                return MyBaseThing.HasRegisteredEvents(pEventName);
            return false;
        }
        protected TheThing MyBaseThing;

        protected bool mIsUXInitialized;
        protected bool mIsInitCalled;
        protected bool mIsInitialized;
        public bool IsUXInit()
        { return mIsUXInitialized; }
        public bool IsInit()
        { return mIsInitialized; }

        public bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            //MyBaseThing.RegisterEvent(eEngineEvents.NewChannelActive, ChannelHasStarted);
            //MyBaseThing.RegisterEvent(eEngineEvents.ChannelIsUpAgain, ChannelHasStarted);
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            mIsInitialized = true;
            return true;
        }

        public bool Delete()
        {
            mIsInitialized = false;
            // TODO Properly implement delete
            return true;
        }


        public bool CreateUX()
        {
            mIsUXInitialized = true;
            return true;
        }

        #endregion

        #region ICDEPlugin Interfaces
        public IBaseEngine GetBaseEngine()
        {
            return MyBaseEngine;
        }
        private IBaseEngine MyBaseEngine;

        public void InitEngineAssets(IBaseEngine pBase)
        {
            MyBaseEngine = pBase;
            MyBaseEngine.SetEngineName(GetType().FullName);
            MyBaseEngine.SetFriendlyName(GetType().FullName);
            MyBaseEngine.SetIsMiniRelay(true);
            MyBaseEngine.SetVersion(TheBaseAssets.BuildVersion);
        }
        #endregion

        /// <summary>
        /// Handles Messages sent from a host sub-engine to its clients
        /// </summary>
        public void HandleMessage(ICDEThing sender, object pIncoming)
        {
            if (!(pIncoming is TheProcessMessage pMsg)) return;

            switch (pMsg.Message.TXT)
            {
                case "CDE_INITIALIZED":
                    TheBaseAssets.MySYSLOG.WriteToLog(888, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("MiniRelayService", $"BackChannel Updated - ORG:{TheCommonUtils.GetDeviceIDML(pMsg.Message.GetLastRelay())}", eMsgLevel.l3_ImportantMessage));
                    break;
                default:
                    if (pMsg.Message.TXT.Equals("CDE_INITIALIZE"))
                    {
                        TSM tRelayMsg = new TSM(MyBaseEngine.GetEngineName(), "CDE_INITIALIZED")
                        {
                            QDX = 3,
                            SID = pMsg.Message.SID
                        };
                        tRelayMsg.SetNoDuplicates(true);
                        TheBaseAssets.MySYSLOG.WriteToLog(888, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("MiniRelayService", $"Message Text {tRelayMsg.TXT} relayed - ORG:{TheCommonUtils.GetDeviceIDML(tRelayMsg.ORG)}", eMsgLevel.l3_ImportantMessage));//ORG-OK
                        TheCommCore.PublishCentral(MyBaseEngine.GetEngineName() + pMsg.Message?.AddScopeIDFromTSM(), tRelayMsg);
                    }
                    break;
            }
        }
    }
}
