// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
// ReSharper disable StringIndexOfIsCultureSpecific.1

//LOG RANGE: 280- 319

namespace nsCDEngine.Communication
{
    internal static class TheCorePubSub
    {
        /// <summary>
        /// Processes incoming Messages
        /// If a Batch comes in, only the first message must have a valid NPA Path
        /// </summary>
        internal static void ProcessClientDeviceMessage(TheQueuedSender MyQSender, TheRequestData pRequestData, List<TheDeviceMessage> tDevList)
        {
            var toRemove = new List<TheDeviceMessage>();
            foreach (TheDeviceMessage tDev in tDevList)
            {
                if (string.IsNullOrEmpty(tDev.NPA))
                    return; // CODE REVIEW: Really stop processing all subsequent TDMs here (malformed TDM -> security)?
                string NPA = tDev.NPA.Substring(4);
                if (NPA.EndsWith(".ashx", StringComparison.OrdinalIgnoreCase))
                    NPA = NPA.Substring(0, NPA.Length - 5);
                if (TheBaseAssets.MyScopeManager.ParseISBPath(NPA, out Guid? ttSessionID,out cdeSenderType tSenderType, out long tFID, out string tVersion))
                {
                    Guid tSessionID = TheCommonUtils.CGuid(ttSessionID);
                    Guid tDeviceID = TheCommonUtils.CGuid(tDev.DID);
                    lock (TheBaseAssets.MySession.GetLock())    //CODE-REVIEW: VERY expensive lock! Really necessary?
                    {
                        var myTargetNodeChannel = MyQSender.MyTargetNodeChannel;
                        if (MyQSender.IsConnected && myTargetNodeChannel.MySessionState != null) //!=null new in 3.2
                        {
                            if (!tSessionID.Equals(myTargetNodeChannel.MySessionState.cdeMID))
                            {
                                Guid tOldID = myTargetNodeChannel.MySessionState.cdeMID;
                                myTargetNodeChannel.MySessionState = TheBaseAssets.MySession.ValidateSEID(tSessionID);    //Measure Frequency
                                if (myTargetNodeChannel.MySessionState == null)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(243, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", $"No exiting session for remote node found. Using new session (possible reason: fast cloud reconnect) ...new: {tSessionID} old: {tOldID}", eMsgLevel.l2_Warning), true);
                                    myTargetNodeChannel.MySessionState = pRequestData.SessionState;
                                }
                                else
                                    TheBaseAssets.MySession.RemoveSessionByID(tOldID);
                            }
                        }
                        if (tDeviceID != Guid.Empty && tDeviceID != myTargetNodeChannel.cdeMID)
                        {
                            if (myTargetNodeChannel.SenderType == cdeSenderType.CDE_CUSTOMISB)
                            {
                                if (myTargetNodeChannel.TruDID != tDeviceID)
                                {
                                    myTargetNodeChannel.TruDID = tDeviceID;
                                    TheBaseAssets.MySYSLOG.WriteToLog(243, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"Custom ISB sets TruDID to: {TheCommonUtils.GetDeviceIDML(tDeviceID)} for MTNC: {myTargetNodeChannel.ToMLString()}", eMsgLevel.l4_Message), true);
                                }
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(243, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"Different DeviceIDs received ...new: {TheCommonUtils.GetDeviceIDML(tDeviceID)} old: {TheCommonUtils.GetDeviceIDML(myTargetNodeChannel.cdeMID)} {(myTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE ? "For CloudRoute and Service normal" : "")}", eMsgLevel.l2_Warning), true);
                                TheBaseAssets.MySession.RemoveSessionsByDeviceID(myTargetNodeChannel.cdeMID, Guid.Empty);
                                TheQueuedSenderRegistry.UpdateQSenderID(myTargetNodeChannel.cdeMID, tDeviceID);
                                myTargetNodeChannel.cdeMID = tDeviceID;
                                if (pRequestData.DeviceID != tDeviceID)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(243, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"Different DeviceIDs in DID and Request ...DID: {TheCommonUtils.GetDeviceIDML(tDeviceID)} Request: {TheCommonUtils.GetDeviceIDML(myTargetNodeChannel.cdeMID)}", eMsgLevel.l2_Warning), true);
                                    pRequestData.DeviceID = tDeviceID;
                                }
                                switch (pRequestData.ResponseMimeType)
                                {
                                    case "cde/mini":
                                        myTargetNodeChannel.SenderType = cdeSenderType.CDE_MINI;
                                        break;
                                }
                            }
                        }
                        if (myTargetNodeChannel.MySessionState == null)
                            myTargetNodeChannel.MySessionState = TheBaseAssets.MySession.GetOrCreateSessionState(tSessionID, pRequestData);
                        myTargetNodeChannel.MySessionState.SiteVersion = tVersion;
                        myTargetNodeChannel.MySessionState.cdeMID = tSessionID;
                        myTargetNodeChannel.MySessionState.CurrentURL = myTargetNodeChannel.TargetUrl;
                        // Keep the RSA public key of the other party, so we can use it to encrypt data when sending to that party in the future
                        if (!String.IsNullOrEmpty(tDev.RSA))
                            myTargetNodeChannel.MySessionState.RSAPublicSend = tDev.RSA;
                        if (MyQSender.IsConnecting && !MyQSender.IsConnected)
                        {
                            MyQSender.IsConnected = true;
                            if (myTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE)
                            {
                                if (TheBaseAssets.MyServiceHostInfo.IsIsolated && TheBaseAssets.MyServiceHostInfo.MasterNode == Guid.Empty)
                                {
                                    TheBaseAssets.MyServiceHostInfo.MasterNode = myTargetNodeChannel.cdeMID;
                                    TheQueuedSenderRegistry.SendMasterNodeQueue();
                                }
                                MyQSender.StartHeartBeat();
                                TheBaseAssets.MySYSLOG.WriteToLog(23055, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", $"Cloud Route is back up to {myTargetNodeChannel.ToMLString()}", eMsgLevel.l4_Message));
                                if (TheQueuedSenderRegistry.eventCloudIsBackUp != null)
                                    TheCommonUtils.cdeRunAsync("QS-StartEngineNU", true, o =>
                                    {
                                        TheQueuedSenderRegistry.eventCloudIsBackUp(TheCDEngines.MyContentEngine, myTargetNodeChannel);
                                    });
                            }
                            if (MyQSender.eventConnected != null)
                                TheCommonUtils.cdeRunAsync("QueueConnected", true, (p) => { MyQSender.eventConnected(MyQSender, myTargetNodeChannel); });
                            MyQSender.MyISBlock?.FireEvent("Connected");
                        }
                        pRequestData.SessionState = myTargetNodeChannel.MySessionState;
                        MyQSender?.SetLastHeartbeat(myTargetNodeChannel.MySessionState);
                    }
                    if (!string.IsNullOrEmpty(tDev.TOP) && tDev.TOP.StartsWith("CDE_CONNECT"))
                    {
                        string[] tP = TheCommonUtils.cdeSplit(tDev.TOP, ";:;", true, true);
                        if (tP.Length > 1)
                        {
                            string tEngs = tP[1];
                            TheCommonUtils.cdeRunAsync("QS-StartEngineNU", false, o =>
                            {
                                TheCDEngines.StartEngineWithNewUrl(MyQSender?.MyTargetNodeChannel, tEngs);
                            });
                        }
                        if (tDevList.Count > 1)
                        {
                            if (toRemove == null)
                            {
                                toRemove = new List<TheDeviceMessage>();
                            }
                            toRemove.Add(tDev);
                            continue;
                        }
                        else
                        {
                            return;
                        }
                    }
                    if (string.IsNullOrEmpty(tDev.TOP) || tDev.TOP.StartsWith("CDE_PICKUP") || tDev.TOP.StartsWith("CDE_WSINIT"))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(23056, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("CoreComm", $"Heartbeat received {MyQSender?.MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l4_Message));
                        if (tDevList.Count > 1)
                        {
                            if (toRemove == null)
                            {
                                toRemove = new List<TheDeviceMessage>();
                            }
                            toRemove.Add(tDev);
                            continue;
                        }
                        else
                        {
                            return;
                        }
                    }
                    break; //Found valid path and can go out
                }
            }
            if (toRemove != null)
            {
                foreach(var tDev in toRemove)
                {
                    tDevList.Remove(tDev);
                }
            }
            TheCommonUtils.cdeRunAsync("QS-UpCo2Exec", false, o =>
            {
                pRequestData.ResponseBuffer = null;
                DoExecuteCommand(null, MyQSender, false, pRequestData, tDevList);
            }, tDevList);
        }


        internal static void ExecuteCommand(string pInTopic, TheQueuedSender pQSender, bool DoSendBackBuffer, TheRequestData pRequestData, List<TheDeviceMessage> pDevMessageList)
        {
            if (pRequestData == null) return;

            #region Precheck on Execute Conditions
            if (pQSender == null || pQSender.MyTargetNodeChannel == null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(285, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", "Enter Execute Command: OrgChannel is Null - not allowed ", eMsgLevel.l2_Warning));
                pRequestData.ResponseBufferStr = "ERR: Illegal Request";
                pRequestData.StatusCode = 400;// (int)nsCDEngine.Communication.HttpService.eHttpStatusCode.NotAcceptable;
                return;
            }
            if (!TheBaseAssets.MasterSwitch) { pRequestData.ResponseBufferStr = "ERR: Service is shutting Down..."; return; }
            if (pRequestData.SessionState == null && TheBaseAssets.MySession != null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(285, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", "Enter Execute Command: Session no longer alive - Topic:" + pInTopic, eMsgLevel.l2_Warning));
                pRequestData.ResponseBufferStr = "ERR: Session no longer alive";
                pRequestData.StatusCode = 401; //(int)nsCDEngine.Communication.HttpService.eHttpStatusCode.NotAcceptable;
                return;
            }
            #endregion

            try
            {
                if (pDevMessageList == null)
                {
                    #region Received BINARY Data in Post Data - Parsing
                    //REVIEW: This should no longer be used in the future...all telegrams should come in as HTTP Bodies
                    if (pRequestData.PostData != null && pRequestData.PostData.Length > 0 && pRequestData.PostDataIdx >= 0)
                    {
                        int tPostDataLength = pRequestData.PostData.Length;
                        if (pRequestData.PostDataLength > 0) tPostDataLength = pRequestData.PostDataLength;
                        if ((tPostDataLength - pRequestData.PostDataIdx) == 1)
                        {
                            if (pQSender.MyTargetNodeChannel.SenderType == cdeSenderType.NOTSET)
                            {
                                pQSender.MyTargetNodeChannel.SenderType = (cdeSenderType)(pRequestData.PostData[0] - 0x30);
                                TheBaseAssets.MySYSLOG.WriteToLog(285, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", $"ChannelChange: Was NOTSET and now is now: {pQSender.MyTargetNodeChannel.SenderType}", eMsgLevel.l3_ImportantMessage));
                            }
                        }
                        else
                        {
                            try
                            {
                                string tStr = null;
                                bool isBin = false;
                                if (pRequestData.ResponseMimeType.ToLower().Contains("zip"))
                                {
                                    tStr = TheCommonUtils.cdeDecompressToString(pRequestData.PostData, pRequestData.PostDataIdx, tPostDataLength - pRequestData.PostDataIdx);
                                    isBin = true;
                                }
                                else
                                    tStr = TheCommonUtils.CArray2UTF8String(pRequestData.PostData, pRequestData.PostDataIdx, tPostDataLength - pRequestData.PostDataIdx);

                                pDevMessageList = TheDeviceMessage.DeserializeJSONToObject(tStr);

                                if (pQSender.MyTargetNodeChannel.SenderType == cdeSenderType.NOTSET)
                                {
                                    pQSender.MyTargetNodeChannel.SenderType = isBin ? cdeSenderType.CDE_SERVICE : cdeSenderType.CDE_JAVAJASON;
                                    TheBaseAssets.MySYSLOG.WriteToLog(285, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", $"ChannelChange: Was NOTSET and now is now: {pQSender.MyTargetNodeChannel.SenderType}", eMsgLevel.l3_ImportantMessage));
                                }

                            }
                            catch (Exception eee)
                            {
                                string remot = "unkown";
                                if (pRequestData.SessionState != null && pRequestData.SessionState.MyDevice != Guid.Empty)
                                    remot = pRequestData.SessionState.MyDevice.ToString();
                                TheBaseAssets.MySYSLOG.WriteToLog(285, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", "Execute Command received JSON package failed", eMsgLevel.l2_Warning, $"{pRequestData.RequestUri} {remot} {eee}"));
                            }
                        }
                    }
                    #endregion
                }
                DoExecuteCommand(pInTopic, pQSender, DoSendBackBuffer, pRequestData, pDevMessageList);
            }
            catch (Exception eeee)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(285, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", "Execute Command failed", eMsgLevel.l2_Warning, $"{pRequestData.RequestUri} {eeee}"));
            }
        }

        internal static void DoExecuteCommand(string pInTopicBatch, TheQueuedSender pQSender, bool DoSendBackBuffer, TheRequestData pRequestData, List<TheDeviceMessage> pDevMessageList)
        {
            bool SendPulse = false;

            if (pDevMessageList != null && pDevMessageList.Count > 0)
            {
                foreach (TheDeviceMessage pDevMessage in pDevMessageList)
                {
                    TSM recvMessage = null;
                    string tTopic = "";
                    try
                    {
                        string pInTopic = pInTopicBatch;
                        if (string.IsNullOrEmpty(pInTopic))
                            pInTopic = pDevMessage.TOP;
                        recvMessage = pDevMessage.MSG;
                        if (pQSender != null && pQSender.MyTargetNodeChannel != null && pQSender.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON && recvMessage != null && string.IsNullOrEmpty(recvMessage.ORG))
                            recvMessage.ORG = pQSender.MyTargetNodeChannel.cdeMID.ToString();

                        #region ChunkResassembly
                        string[] CommandParts = null;
                        if (!string.IsNullOrEmpty(pInTopic))
                        {
                            CommandParts = TheCommonUtils.cdeSplit(pInTopic, ":,:", false, false);
                            tTopic = CommandParts[0];
                        }
                        TheCDEKPIs.IncrementKPI(eKPINames.CCTSMsReceived);
                        if (CommandParts != null && CommandParts.Length == 4 && !CommandParts[2].Equals("1"))
                        {
                            if (!TheCommonUtils.ProcessChunkedMessage(CommandParts, recvMessage))
                            {
                                SendPulse = true;
                                continue;
                            }
                        }
                        #endregion

                        if (recvMessage != null)
                        {
                            if (((TheBaseAssets.MyServiceHostInfo.RejectIncomingSETP && recvMessage.TXT?.StartsWith("SETP:") == true) || TheBaseAssets.MyServiceHostInfo.DisableNMIMessages) && recvMessage.ENG?.StartsWith(eEngineName.NMIService) == true)
                            {
                                TheCDEKPIs.IncrementKPI(eKPINames.QSSETPRejected);
                                continue;
                            }
                            var tTopicSens = tTopic.Split('@')[0]; //only topic - no ScopeID
                            var tTopicParts = tTopic.Split(';');
                            if (pQSender.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON)
                            {
                                //4.209: JavaJason does no longer get the scope ID - hence telegrams coming from the browser have to be ammmended with SID here
                                if (string.IsNullOrEmpty(recvMessage.SID) && !string.IsNullOrEmpty(pRequestData?.SessionState?.SScopeID))
                                {
                                    recvMessage.SID = pRequestData?.SessionState?.SScopeID; //Set the ScopeID in the SID of the message
                                    if (tTopic.StartsWith("CDE_SYSTEMWIDE"))
                                    {
                                        tTopic = $"{tTopicParts[0]}@{recvMessage.SID}";
                                        if (tTopicParts.Length > 1)
                                            tTopic += $";{tTopicParts[1]}"; //if a direct address is added use this too
                                    }
                                    else if (!tTopic.Contains('@'))
                                    {
                                        tTopic += $"@{recvMessage.SID}";
                                    }
                                    if (recvMessage.TXT == "CDE_SUBSCRIBE" || recvMessage.TXT == "CDE_INITIALIZE")
                                    {
                                        string MsgNoSID = null;
                                        recvMessage.PLS = TheBaseAssets.MyScopeManager.AddScopeID(recvMessage.PLS, recvMessage.SID, ref MsgNoSID, false, false);
                                    }
                                }
                            }
                            if (tTopicParts.Length > 1)
                                tTopicSens += $";{tTopicParts[1]}"; //if a direct address is added use this too
                            if (TheQueuedSenderRegistry.WasTSMSeenBefore(recvMessage, pRequestData.SessionState.cdeMID, tTopicSens, pQSender?.MyTargetNodeChannel?.RealScopeID))    //ATTENTION: RScope should come from pDevMessage
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(285, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"EnterExecuteCommand: Message was seen before ORG:{TheCommonUtils.GetDeviceIDML(recvMessage?.ORG)} Topic:{tTopicSens}", eMsgLevel.l2_Warning));//ORG-OK
                                TheCDEKPIs.IncrementKPI(eKPINames.QSRejected);
                                continue;
                            }
                            if (pQSender.MyTargetNodeChannel.cdeMID == Guid.Empty)
                                pQSender.MyTargetNodeChannel.cdeMID = recvMessage.GetLastRelay();
                            if (pQSender.MyTargetNodeChannel.SenderType == cdeSenderType.NOTSET)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(285, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", "Sender Type for the QSender is not set! WE SHOULD NEVER GET HERE", eMsgLevel.l1_Error));//ORG-OK
                                //3.218: Processing no longer allowed!!!
                                return;
                            }
                            // Enable upper layers to do RSA decryption. Force overwrite for Browser even if SEID already set (may have been initialized to some other value)
                            if (pQSender.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON || String.IsNullOrEmpty(recvMessage.SEID))
                                recvMessage.SEID = pRequestData.SessionState.cdeMID.ToString();

                            if (pRequestData.SessionState != null && string.IsNullOrEmpty(pRequestData.SessionState.RemoteAddress))
                                pRequestData.SessionState.RemoteAddress = pQSender.MyTargetNodeChannel.cdeMID.ToString();

                            //NEW: User ID Management in Message after first node
                            if (string.IsNullOrEmpty(recvMessage.UID) && pRequestData.SessionState != null && pRequestData.SessionState.CID != Guid.Empty)
                            {
                                recvMessage.UID = TheCommonUtils.cdeEncrypt(pRequestData.SessionState.CID.ToByteArray(), TheBaseAssets.MySecrets.GetAI());     //3.083: Must be cdeAI
                            }
                        }
                    }
                    catch (Exception ee)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(319, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", "Execute Command Pre-Parsing Error", eMsgLevel.l1_Error, ee.ToString()));
                    }

                    if (recvMessage == null)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(286, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("CoreComm", $"No Message for Parsing: Topic:{tTopic} - {pRequestData.ResponseBufferStr}", eMsgLevel.l7_HostDebugMessage));
                    }
                    else
                    {
                        if (recvMessage.PLB != null && recvMessage.PLB.Length > 0 && string.IsNullOrEmpty(recvMessage.PLS))
                        {
                            try
                            {
                                recvMessage.PLS = TheCommonUtils.cdeDecompressToString(recvMessage.PLB);
                                recvMessage.PLB = null;
                            }
                            catch (Exception)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(286, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"PLB to PLS decompress failed - Topic:{tTopic} Node:{pQSender?.MyTargetNodeChannel?.ToMLString()} ORG:{TheCommonUtils.GetDeviceIDML(recvMessage?.ORG)}", eMsgLevel.l7_HostDebugMessage, $"TXT:{recvMessage.TXT}"));//ORG-OK
                            }
                        }

                        TheBaseAssets.MySYSLOG.WriteToLog(286, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("CoreComm", $"PLB to PLS parsing done - Topic:{tTopic} Node:{pQSender?.MyTargetNodeChannel?.ToMLString()} ORG:{TheCommonUtils.GetDeviceIDML(recvMessage?.ORG)}", eMsgLevel.l7_HostDebugMessage, $"TXT:{recvMessage.TXT}"));//ORG-OK
                        if (!SendPulse)
                            SendPulse = recvMessage.SendPulse();
                        TheCDEKPIs.IncrementKPI(eKPINames.CCTSMsEvaluated);
                    }

                    try
                    {
                        if (recvMessage != null && !string.IsNullOrEmpty(tTopic)) // tTopic != null)
                        {
                            if (!ParseSimplex(tTopic, recvMessage, pQSender))//NEW:2.06 - No More Local Host processing here
                            {
                                if (TheBaseAssets.MyServiceHostInfo.MaximumHops == 0 || recvMessage.HobCount() < TheBaseAssets.MyServiceHostInfo.MaximumHops)
                                {
                                    if (!tTopic.StartsWith("CDE_CONNECT") || !TheBaseAssets.MyServiceHostInfo.AllowMessagesInConnect) // Should never get here if AllowMessagesInConnect is false, but avoid global publish just in case...
                                    {
                                        TheCDEKPIs.IncrementKPI(eKPINames.CCTSMsRelayed);
                                        TheCommCore.PublishCentral(tTopic, recvMessage, false, null);
                                    }
                                    else
                                    {
                                        // Message is part of a CDE_CONNECT: Republish it to enable single-post message sending (i.e. MSB/Service Gateway scenario)
                                        TheCDEKPIs.IncrementKPI(eKPINames.CCTSMsRelayed); // TODO SHould we have a separate KPI for this
                                        TheCommCore.PublishCentral(recvMessage, true);
                                    }
                                }
                            }
                            else
                            {
                                if (pQSender?.MyTargetNodeChannel != null && pQSender?.MyTargetNodeChannel?.SenderType != cdeSenderType.CDE_JAVAJASON)
                                    SendPulse = true;
                            }
                        }
                    }
                    catch (Exception ee)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(319, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", "Execute Command Parsing Error", eMsgLevel.l1_Error, ee.ToString()));
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(286, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("CoreComm", $"Done with Do Execute Command for Topic:{tTopic}", eMsgLevel.l7_HostDebugMessage));
                }
            }
            if (pRequestData.WebSocket == null)
            {
                if (DoSendBackBuffer)
                    SetResponseBuffer(pRequestData, pQSender.MyTargetNodeChannel, SendPulse, "");
                else if (SendPulse)
                    pQSender.SendPickupMessage(); //Pickup next message right away if pulse mode is on
            }
            TheBaseAssets.MySYSLOG.WriteToLog(286, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("CoreComm", "Leave Do Execute Command", eMsgLevel.l7_HostDebugMessage));
        }



















        internal static void SetResponseBuffer(TheRequestData pRequestData, TheChannelInfo pChannelInfo, bool pSendPulse, string NopTopic)
        {
            SetResponseBuffer(pRequestData, pChannelInfo, pSendPulse, NopTopic, Guid.Empty,null);
        }

        internal static void SetResponseBuffer(TheRequestData pRequestData, TheChannelInfo pChannelInfo, bool pSendPulse, string NopTopic, Guid owner, string pRefreshToken)
        {
            if (string.IsNullOrEmpty(NopTopic))
            {
                TheQueuedSender tQ = null;
                if (pChannelInfo != null && pChannelInfo.cdeMID != Guid.Empty)    //Send to Device First
                    tQ = TheQueuedSenderRegistry.GetSenderByGuid(pChannelInfo.cdeMID);
                if (tQ != null)
                {
                    if (pRequestData.WebSocket != null)
                    {
                        if (tQ.GetQueLength() == 0)
                            tQ.SendPickupMessage();
                        return;
                    }
                    tQ.GetNextBackChannelBuffer(pRequestData);
                }
            }
            if (string.IsNullOrEmpty(NopTopic) && pRequestData.WebSocket != null) return; //NEW:3.084  && !pSendPulse removed          NEW:V3B3:2014-7-22 removed && pRequestData.ResponseBuffer != null
            if (pRequestData.ResponseBuffer == null && pChannelInfo != null && (pSendPulse || (pChannelInfo.cdeMID != Guid.Empty || TheQueuedSenderRegistry.IsNodeIdInSenderList(pChannelInfo.cdeMID))))
            {
                TheDeviceMessage tDev = new TheDeviceMessage {CNT = 0};
                //tDev.MET = 0;
                if (!string.IsNullOrEmpty(NopTopic))
                {
                    tDev.MSG = new TSM();   //Can be set without ORG and SID
                    if (owner != Guid.Empty)
                    {
                        tDev.MSG.OWN = owner.ToString();
                    }
                    if (NopTopic=="CDE_WSINIT")
                    {
                        NopTopic = TheCommCore.SetConnectingBufferStr(pChannelInfo, null);
                    }
                }
                if (pChannelInfo.SenderType != cdeSenderType.CDE_JAVAJASON) //4.209: No longer sending SID to Browser;
                {
                    if (TheBaseAssets.MyServiceHostInfo.EnableFastSecurity)
                        tDev.SID = pRequestData.SessionState.SScopeID;  //SECURITY: All responses will have same Scrambled ScopeID - but ok because this is init telegram or HB
                    else
                        tDev.SID = TheBaseAssets.MyScopeManager.GetScrambledScopeID(pRequestData.SessionState.SScopeID, false); //GRSI: high frequency
                }
                else
                {
                    if (!string.IsNullOrEmpty(pRefreshToken))
                        tDev.SID = pRefreshToken;
                }
                tDev.FID = pRequestData.SessionState.GetNextSerial().ToString();
                if (TheCommonUtils.IsDeviceSenderType(pChannelInfo.SenderType)) //IDST-OK: Must create RSA for Devices
                {
                    TheCommonUtils.CreateRSAKeys(pRequestData.SessionState);
                    tDev.RSA = pRequestData.SessionState.RSAPublic;
                }
                tDev.NPA = TheBaseAssets.MyScopeManager.GetISBPath(TheBaseAssets.MyServiceHostInfo.RootDir, pChannelInfo.SenderType, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType, pRequestData.SessionState.FID, pRequestData.SessionState.cdeMID, pRequestData.WebSocket != null);
                tDev.CNT = pSendPulse ? 1 : 0;
                tDev.TOP = NopTopic;
                tDev.DID = pChannelInfo.SenderType == cdeSenderType.CDE_JAVAJASON ? pChannelInfo.cdeMID.ToString() : TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString();

                //There will be only one Message here - single poke or Mini Command or NOP Pickup
                List<TheDeviceMessage> tDevList = new List<TheDeviceMessage> {tDev};
                if (pChannelInfo.SenderType == cdeSenderType.CDE_JAVAJASON || TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType == cdeSenderType.CDE_MINI || pChannelInfo.SenderType == cdeSenderType.CDE_MINI || pRequestData.WebSocket != null)
                {
                    pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(TheCommonUtils.SerializeObjectToJSONString(tDevList));
                    pRequestData.ResponseMimeType = "application/json";
                }
                else
                {
                    pRequestData.ResponseBuffer = TheCommonUtils.cdeCompressString(TheCommonUtils.SerializeObjectToJSONString(tDevList));
                    pRequestData.ResponseMimeType = "application/x-gzip";
                }
            }
            if (pRequestData.ResponseBuffer == null && pChannelInfo != null && pChannelInfo.cdeMID != Guid.Empty)
                TheBaseAssets.MySYSLOG.WriteToLog(290, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("CoreComm", $"Nothing to Send Back to {pChannelInfo?.ToMLString()}", eMsgLevel.l7_HostDebugMessage));
        }

        internal static bool ParseSimplex(string pScopedTopic, TSM pMessage, TheQueuedSender pQSender) //, TheSessionState pSessionState, Action<TSM> pLocalCallback)
        {
            if (pMessage == null)
                return false;
#if !JC_COMMDEBUG
            try
            {
#endif
                if (pMessage?.TXT?.Equals("CDE_DELETEORPHAN")==true)
                {
                    TheQueuedSenderRegistry.RemoveOrphan(TheCommonUtils.CGuid(pMessage.PLS));
                    return false;
                }

                if (pQSender == null)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(291, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"QSender not found! Received from ORG:{TheCommonUtils.GetDeviceIDML(pMessage?.ORG)}", eMsgLevel.l1_Error, pMessage?.PLS));
                    return false;
                }

                //SECURITY-REVIEW: This cannot be permitted without extra tokens and Encryption! otherwise it can be used to change a nodes scope on the fly!
                //if ("CDE_UPDATESCOPE".Equals(pMessage.TXT))
                //{
                //    pQSender.UpdateSubscriptionScope(TheBaseAssets.MyScopeManager.GetRealScopeID(pMessage.SID));     //GRSI: rare
                //    return true;
                //}
                if (pQSender != null && pMessage.ENG?.StartsWith(eEngineName.ContentService) == true && pMessage?.TXT == "CDE_SERVICE_INFO" && pQSender.MyTargetNodeChannel?.RealScopeID==TheBaseAssets.MyScopeManager.GetRealScopeID(pMessage.SID))
                {
                    try
                    {
                        pQSender.SetNodeInfo(TheCommonUtils.DeserializeJSONStringToObject<TheNodeInfoClone>(pMessage?.PLS));
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(23056, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("CoreComm", $"Error decoding SystemInfo {pQSender?.MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l1_Error, e.ToString()));
                    }
                }

                if (pMessage.TXT?.Equals("CDE_SUBSCRIBE")==true || pMessage.TXT?.Equals("CDE_INITIALIZE")==true) //9-9-2012 CDEC Did not work right on CDE_INIT
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(292, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"Parse-Simplex Subscribe from {pQSender?.MyTargetNodeChannel?.ToMLString()} Parsed: {pMessage?.PLS}", eMsgLevel.l7_HostDebugMessage));
                    if (pQSender?.MyISBlock!=null && !TheBaseAssets.MyServiceHostInfo.IsCloudService)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(292, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", "Parse-Simplex Subscribe rejected for Custom ISBConnect", eMsgLevel.l7_HostDebugMessage));
                        return true;
                    }
                    ParseSubscribe(pMessage.PLS, pQSender);
                    if (pMessage.ENG.Equals("CLOUDSYNC"))
                    {
                        string[] tTopics = pMessage.PLS.Split(';');
                        foreach (string t in tTopics)
                        {
                            TSM.GetScrambledIDFromTopic(t, out string tEng);
                            if (TheThingRegistry.IsEngineRegistered(tEng))
                            {
                                IBaseEngine tsBase = TheThingRegistry.GetBaseEngine(tEng);
                                tsBase?.GetBaseThing()?.FireEvent(eEngineEvents.NewSubscription, tsBase.GetBaseThing(), pQSender.MyTargetNodeChannel, true);
                            }
                        }
                        return true;
                    }
                    else
                    {
                        TheThing tBase2 = TheThingRegistry.GetBaseEngineAsThing(pMessage.ENG);
                        if (tBase2 != null)
                            tBase2.FireEvent(eEngineEvents.NewSubscription, tBase2, pQSender.MyTargetNodeChannel, true);
                    }
                    if (pMessage.TXT.Equals("CDE_SUBSCRIBE"))   //NEW:2.06 Make sure Subscribe and Unsubscribe only go to first node
                        return true;
                    else
                        return false;
                }
                if (pMessage.TXT?.Equals("CDE_UNSUBSCRIBE")==true)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(292, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"Parse-Simplex Unsubscribe from {pQSender.MyTargetNodeChannel?.ToMLString()} Parsed: {pMessage.PLS}", eMsgLevel.l7_HostDebugMessage));
                    ParseUnsubscribe(pMessage.PLS, pQSender);
                    return true;    //NEW:2.06 Make sure Subscribe and Unsubscribe only go to first node
                }
#if !JC_COMMDEBUG
            }
            catch (Exception ee)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(316, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", "Parse-Simplex", eMsgLevel.l1_Error, ee.ToString()));
            }
#endif
            return false;
        }

        internal static void ParseSubscribe(string pScopedTopics, TheQueuedSender pQSender)
        {
            if (string.IsNullOrEmpty(pScopedTopics) || pQSender == null)
                return;

            TheBaseAssets.MySYSLOG.WriteToLog(295, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"DoParseSubscribe for Topics={pScopedTopics} of {pQSender.MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l7_HostDebugMessage));

            bool HasSubscribedBefore = !pQSender.Subscribe(pScopedTopics, true); //Incoming Subs are native to connected node
            TheBaseAssets.MySYSLOG.WriteToLog(300, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"DoParseSubscribe: {pQSender.MyTargetNodeChannel?.ToMLString()} updates Subscription Topis: {pScopedTopics} Was Sub'ed before: {HasSubscribedBefore}", eMsgLevel.l7_HostDebugMessage));
        }

        internal static void ParseUnsubscribe(string pTopics, TheQueuedSender pQSender)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(301, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm",$"ParseUnsubscribe for Topics={pTopics} of {pQSender.MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l7_HostDebugMessage));

            if (string.IsNullOrEmpty(pTopics) || pQSender == null)
                return;

            bool WasSubscribed = pQSender.Unsubscribe(pTopics);
            TheBaseAssets.MySYSLOG.WriteToLog(302, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"ParseUnsubscribe: Removing Topic Topics={pTopics} of {pQSender.MyTargetNodeChannel?.ToMLString()} Was Sub'ed: {WasSubscribed}", eMsgLevel.l7_HostDebugMessage));

            #region DebugInfo

            if (TheBaseAssets.MyServiceHostInfo.DebugLevel > eDEBUG_LEVELS.FULLVERBOSE)
            {
                string tContent = "ParseUnsubscribe - Unsubscribed from: " + pTopics;
                eMsgLevel tLev = eMsgLevel.l4_Message;
                if (WasSubscribed)
                {
                    tLev = eMsgLevel.l2_Warning;
                    tContent = "A Subscription was not Found for: " + pTopics;
                }
                TheBaseAssets.MySYSLOG.WriteToLog(304, new TSM("CoreComm", tContent, tLev));
            }

            #endregion
        }
    }
}