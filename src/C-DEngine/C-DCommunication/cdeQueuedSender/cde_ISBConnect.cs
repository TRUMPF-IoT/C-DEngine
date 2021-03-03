// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;

namespace nsCDEngine.Communication
{
    /// <summary>
    /// Base class of ISBRequest information
    /// </summary>
    public class TheISBRequest : TheMetaDataBase
    {
        /// <summary>
        /// Scrambled Scope ID
        /// </summary>
        public string SID { get; set; }
        /// <summary>
        /// Device ID of the node
        /// </summary>
        public Guid DeviceID { get; set; }
        /// <summary>
        /// Sender Type of the node
        /// </summary>
        public cdeSenderType SenderType { get; set; }
    }

    /// <summary>
    /// ISB Connect is used for Multi-Scoping connection to other meshes
    /// </summary>
    public class TheISBConnect : TheMetaDataBase
    {
        /// <summary>
        /// NPA = Next Possible Address - contains the Crypted Session ID and in case of HTTP is the next URL Path
        /// </summary>
        public string NPA { get; set; }

        /// <summary>
        /// First Node ID: DeviceID of the node providing this ISBConnect Block
        /// </summary>
        public string FNI { get; set; }

        /// <summary>
        /// NMI Main Configuration Screen (not implemented yet)
        /// </summary>
        public string MCS { get; set; }

        /// <summary>
        /// NMI Portal/Main Dashboard Screen
        /// </summary>
        public string PS { get; set; }

        /// <summary>
        /// NMI Home Screen
        /// </summary>
        public string SSC { get; set; }

        /// <summary>
        /// Application Title
        /// </summary>
        public string AT { get; set; }

        /// <summary>
        /// Scrambled Scope ID for the connection
        /// </summary>
        public string SID { get; set; }

        /// <summary>
        /// Language ID (LCID) for the connection
        /// </summary>
        public int LCI { get; set; }

        /// <summary>
        /// Username - no longer used
        /// </summary>
        public string UNA { get; set; }

        /// <summary>
        /// User Preferences as JSON String
        /// </summary>
        public string UPRE { get;set; }
        /// <summary>
        /// Email or username
        /// </summary>
        public string QUI { get; set; }

        /// <summary>
        /// Password (hash) currently not used
        /// </summary>
        public string PWD { get; set; }
        /// <summary>
        /// Any Error message if applicable
        /// </summary>
        public string ERR { get; set; }

        /// <summary>
        /// WebSockets port. Zero if no websockets supported
        /// </summary>
        public int WSP { get; set; }

        /// <summary>
        /// If true, TLS 1.2 is required
        /// </summary>
        public bool TLS { get; set; }

        /// <summary>
        /// Custom Message Content sent during Provisioning;
        /// </summary>
        public string CUS { get; set; }

        /// <summary>
        /// Easy Scope: this is no longer supported and now a ScrambledScope ID
        /// </summary>
        public string ES { get; set; }

        /// <summary>
        /// Version of the ISBConnect
        /// </summary>
        public string VER { get; set; }

        /// <summary>
        /// Initial Subscriptions
        /// </summary>
        public string IS { get; set; }
        /// <summary>
        /// Creates a new ISB Connect Object
        /// </summary>
        public TheISBConnect()
        {
            VER = "4.2";
        }

        private string mSScope;
        private string mURL;
        private string mInitSubs;

        /// <summary>
        /// Reconnects this route with the same parameter set in "Connect"
        /// </summary>
        /// <returns>returns and error if reconnect failed. Null if success</returns>
        public string Reconnect()
        {
            return Connect(mURL, mSScope,mInitSubs, true);
        }

        /// <summary>
        /// Connect to a Custom URL via ISB Connect and a custom Scope
        /// </summary>
        /// <param name="pUrl">URL to connect to</param>
        /// <param name="pEasyScope">Easy ScopeID generator for the connection</param>
        /// <returns></returns>
        public string Connect(string pUrl, string pEasyScope)
        {
            return Connect(pUrl, pEasyScope,null, false);
        }
        /// <summary>
        /// Connect to a Custom URL via ISB Connect and a custom Scope
        /// </summary>
        /// <param name="pUrl">URL to connect to</param>
        /// <param name="pEasyScope">Easy ScopeID generator for the connection. If next parameter is true, this parameter will be interpreted as a scrambled ScopeID</param>
        /// <param name="pIsScrambledID">If true, the EasyScopeID is a scrambled ScopeID</param>
        /// <returns></returns>
        public string Connect(string pUrl, string pEasyScope, bool pIsScrambledID)
        {
            return Connect(pUrl, pEasyScope, null, pIsScrambledID);
        }
        /// <summary>
        /// Connect to a Custom URL via ISB Connect and a custom Scope
        /// </summary>
        /// <param name="pUrl">URL to connect to</param>
        /// <param name="pEasyScope">Easy ScopeID generator for the connection. If next parameter is true, this parameter will be interpreted as a scrambled ScopeID</param>
        /// <param name="pInitialSubscriptions">List of initial Subscriptions separated by ;</param>
        /// <param name="pIsScrambledID">If true, the EasyScopeID is a scrambled ScopeID</param>
        /// <returns></returns>
        public string Connect(string pUrl, string pEasyScope, string pInitialSubscriptions, bool pIsScrambledID)
        {
            if (string.IsNullOrEmpty(pUrl))
            {
                ERR = "no url set!";
                return ERR;
            }
            if (string.IsNullOrEmpty(pEasyScope))
            {
                ERR = "No scope set - unscoped connection not allowed";
                return ERR;
            }
            mURL = pUrl;
            mInitSubs = pInitialSubscriptions;
            TheSessionState tSessionState = TheBaseAssets.MySession.CreateSession(null, Guid.Empty);
            if (!string.IsNullOrEmpty(pEasyScope))
            {
                if (pIsScrambledID)
                {
                    mSScope = tSessionState.SScopeID = pEasyScope;
                    RS = TheBaseAssets.MyScopeManager.GetRealScopeID(tSessionState.SScopeID);   //GRSI: rare
                }
                else
                {
                    RS = TheBaseAssets.MyScopeManager.GetRealScopeIDFromEasyID(pEasyScope);
                    mSScope= tSessionState.SScopeID = TheBaseAssets.MyScopeManager.GetScrambledScopeID(RS, true);    //GRSI: rare
                }
            }
            MyQSender = new TheQueuedSender
            {
                MyISBlock = this
            };
            IS = TheBaseAssets.MyScopeManager.AddScopeID(pInitialSubscriptions, false, RS);
            if (MyQSender.StartSender(new TheChannelInfo(Guid.Empty,RS, cdeSenderType.CDE_CUSTOMISB, pUrl)
            {
                MySessionState = tSessionState
            }, null, false))
            {
                FNI = MyQSender.MyTargetNodeChannel.cdeMID.ToString();
                SID = tSessionState.SScopeID;
            }
            else
            {
                ERR = "Qsender could not be created";
                TheBaseAssets.MySYSLOG.WriteToLog(299, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"CreateCustomSender: QueuedSender could not be created for Target:{pUrl}", eMsgLevel.l7_HostDebugMessage));
                MyQSender = null;
            }
            return ERR;
        }

        /// <summary>
        /// Encrypts the ISBConnect Class Properties against the local node's ScopeID
        /// </summary>
        /// <returns></returns>
        public byte[] EncryptedBuffer()
        {
            this.ES = TheBaseAssets.MyScopeManager.GetScrambledScopeID(); //SECURITY-REVIEW: No more ScopeID in clear Text
            string tPLS = TheCommonUtils.SerializeObjectToJSONString(this);
            byte [] tEncry= TheBaseAssets.MyCrypto.Encrypt(TheCommonUtils.CUnicodeString2Array(tPLS), TheBaseAssets.MySecrets.GetAK(), TheBaseAssets.MySecrets.GetNodeKey());
            this.ES = null;
            return tEncry;
        }

        /// <summary>
        /// Disposes the underlying QueuedSender. Don't use this if you have multiple ISBConnects to the same URL as they share the same QueuedSender
        /// </summary>
        /// <returns></returns>
        public bool Disconnect()
        {
            MyQSender?.DisposeSender(true);
            MyQSender = null;
            return true;
        }

        /// <summary>
        /// Subscribes to Custom topics on this ISB Connection. The Topics will be scoped with the scope used during connect
        /// </summary>
        /// <param name="pTopics">List of topics to subscribe to separated by ;</param>
        /// <returns></returns>
        public bool Subscribe(string pTopics)
        {
            string noMSG = null;
            string strSubs = TheBaseAssets.MyScopeManager.AddScopeID(pTopics, RS,ref noMSG, false,true);     //GRSI: rare
            TSM tTSM = new TSM(eEngineName.ContentService, "CDE_SUBSCRIBE", strSubs);
            tTSM.SetNoDuplicates(true);
            tTSM.QDX = 2;
            MyQSender.Subscribe(strSubs);
            return SendTSM(tTSM);
        }

        /// <summary>
        /// Unsubscribes from Custom topics on this ISB Connection. The Topics will be scoped with the scope used during connect
        /// </summary>
        /// <param name="pTopics">List of topics to subscribe to separated by ;</param>
        /// <returns></returns>
        public bool Unsubscribe(string pTopics)
        {
            string noMSG = null;
            string strSubs = TheBaseAssets.MyScopeManager.AddScopeID(pTopics, RS, ref noMSG, false, true);     //GRSI: rare
            TSM tTSM = new TSM(eEngineName.ContentService, "CDE_UNSUBSCRIBE", strSubs);
            tTSM.SetNoDuplicates(true);
            tTSM.QDX = 2;
            MyQSender.Unsubscribe(strSubs);
            return SendTSM(tTSM);
        }

        /// <summary>
        /// Sends a TSM to via the ISB Connection. The ENG must contain the topic the message should be sent to
        /// </summary>
        /// <param name="pSend"></param>
        /// <returns></returns>
        public bool SendTSM(TSM pSend)
        {
            if (MyQSender == null || pSend == null) return false;
            return SendTSM(pSend.ENG, pSend);
        }

        /// <summary>
        /// Sends a TSM with a custom topic with a possible target and Source Sender - in Par with ICDECommChannel
        /// </summary>
        /// <param name="tTSM"></param>
        /// <param name="pTopic"></param>
        /// <param name="pTarget"></param>
        /// <param name="pSender"></param>
        /// <returns></returns>
        public bool SendTSM(TSM tTSM, string pTopic=null, Guid? pTarget=null, Guid? pSender=null)
        {
            if (MyQSender == null || tTSM == null) return false;
            if (!tTSM.ORG.Contains(":") && pSender!=null)
                tTSM.ORG += $":{TheCommonUtils.CGuid(pSender)}";
            if (string.IsNullOrEmpty(tTSM.SID))
                tTSM.SID = mSScope;
            return MyQSender.SendQueued(TheBaseAssets.MyScopeManager.AddScopeID(pTopic, RS,ref tTSM.SID, false, true), tTSM, false, TheCommonUtils.CGuid(pTarget), pTopic, RS, null);     //GRSI: rare
        }

        /// <summary>
        /// Sends a TSM via the ISB Connection to a custom Topic specified in the first parameter
        /// </summary>
        /// <param name="pTopic">Subscription topic to send to</param>
        /// <param name="pSend">TSM to send</param>
        /// <returns></returns>
        public bool SendTSM(string pTopic, TSM pSend)
        {
            if (MyQSender == null || pSend == null) return false;
            if (string.IsNullOrEmpty(pSend.SID))
                pSend.SID = mSScope;
            return MyQSender.SendQueued(TheBaseAssets.MyScopeManager.AddScopeID(pTopic, RS,ref pSend.SID, false, true), pSend,false,Guid.Empty,pTopic,RS, null);     //GRSI: rare
        }

        /// <summary>
        /// Sends back to the originator
        /// </summary>
        /// <param name="sourceMessage">Original Message to reply to</param>
        /// <param name="TargetMessage">New message to be sent to originator</param>
        /// <param name="IncludeLocalNode">if true the message will be sent to localhost</param>
        /// <returns></returns>
        public bool SendToOriginator(TSM sourceMessage, TSM TargetMessage, bool IncludeLocalNode)
        {
            if (sourceMessage == null || TargetMessage == null) return false;
            TargetMessage.SID = sourceMessage.SID;
            Guid tOrg = sourceMessage.GetOriginator();
            if (tOrg == Guid.Empty) return false;
            TargetMessage.GRO = sourceMessage.ORG;
            Guid tOriginatorThing = sourceMessage.GetOriginatorThing();
            if (tOriginatorThing != Guid.Empty)
            {
                TargetMessage.OWN = tOriginatorThing.ToString();
            }
            if (IncludeLocalNode)
                TheBaseAssets.LocalHostQSender.SendQueued(TheBaseAssets.MyScopeManager.AddScopeID("CDE_SYSTEMWIDE;" + FNI, null,ref sourceMessage.SID, true, false), TargetMessage, false, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID, "CDE_SYSTEMWIDE", RS, null);     //GRSI: rare
            return MyQSender.SendQueued(TheBaseAssets.MyScopeManager.AddScopeID("CDE_SYSTEMWIDE;" + tOrg, null,ref sourceMessage.SID, true,false), TargetMessage, false, tOrg, "CDE_SYSTEMWIDE", RS, null);     //GRSI: rare
        }

        /// <summary>
        /// Sends a message to the first node in the mesh. Is is the Node this ISB Connection is connected to
        /// </summary>
        /// <param name="TargetMessage"></param>
        /// <returns></returns>
        public bool SendToFirstNode(TSM TargetMessage)
        {
            if (TargetMessage == null) return false;
            if (string.IsNullOrEmpty(TargetMessage.SID))
                TargetMessage.SID = mSScope;
            return MyQSender.SendQueued(TheBaseAssets.MyScopeManager.AddScopeID("CDE_SYSTEMWIDE;" + MyQSender.MyTargetNodeChannel.TruDID,true, RS), TargetMessage,false, MyQSender.MyTargetNodeChannel.TruDID, "CDE_SYSTEMWIDE", RS, null);     //GRSI: rare
        }

        /// <summary>
        /// Sends a TSM to a specific node
        /// </summary>
        /// <param name="tOrg">Node to send to</param>
        /// <param name="TargetMessage">Message to be send</param>
        /// <param name="IncludeLocalNode">Not used! If tOrg is localhost the message will be sent to localhost</param>
        /// <returns></returns>
        public bool SendToNode(Guid tOrg, TSM TargetMessage, bool IncludeLocalNode)
        {
            if (tOrg == Guid.Empty || TargetMessage == null) return false;
            if (string.IsNullOrEmpty(TargetMessage.SID))
                TargetMessage.SID = mSScope;
            if (IncludeLocalNode)
                TheBaseAssets.LocalHostQSender.SendQueued(TheBaseAssets.MyScopeManager.AddScopeID("CDE_SYSTEMWIDE;" + FNI,true, RS), TargetMessage, false, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID, "CDE_SYSTEMWIDE", RS, null);     //GRSI: rare
            return MyQSender.SendQueued(TheBaseAssets.MyScopeManager.AddScopeID("CDE_SYSTEMWIDE;" + tOrg,true, RS), TargetMessage,false,tOrg, "CDE_SYSTEMWIDE", RS, null);     //GRSI: rare
        }
        //02X6QJ7E
        private TheQueuedSender MyQSender = null;
        private string RS;
    }
}
