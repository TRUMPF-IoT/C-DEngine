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
    internal class TheMiniRelayEngine : ThePluginBase
    {
        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            mIsInitialized = true;
            return true;
        }

        public override void InitEngineAssets(IBaseEngine pBase)
        {
            MyBaseEngine = pBase;
            MyBaseEngine.SetEngineName(GetType().FullName);
            MyBaseEngine.SetFriendlyName(GetType().FullName);
            MyBaseEngine.SetIsMiniRelay(true);
            MyBaseEngine.SetVersion(TheBaseAssets.BuildVersion);
        }

        /// <summary>
        /// Handles Messages sent from a host sub-engine to its clients
        /// </summary>
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            if (pIncoming is not TheProcessMessage pMsg) return;

            switch (pMsg.Message.TXT)
            {
                case "CDE_INITIALIZED":
                    TheBaseAssets.MySYSLOG.WriteToLog(888, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("MiniRelayService", $"BackChannel Updated - ORG:{TheCommonUtils.GetDeviceIDML(pMsg.Message.GetLastRelay())}", eMsgLevel.l3_ImportantMessage));
                    break;
                default:
                    if (pMsg.Message.TXT.Equals("CDE_INITIALIZE"))
                    {
                        TSM tRelayMsg = new (MyBaseEngine.GetEngineName(), "CDE_INITIALIZED")
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
