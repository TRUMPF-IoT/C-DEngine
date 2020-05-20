// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
// ReSharper disable DelegateSubtraction
// ReSharper disable ConditionIsAlwaysTrueOrFalse

namespace nsCDEngine.Communication
{
    /// <summary>
    /// TheCommCore class is the static base class for communication related functions
    /// </summary>
    public static class TheCommCore
    {
        internal static List<string> GetQueueInformation(bool ShowQueueDetails)
        {
            return TheQueuedSenderRegistry.ShowQSenderDetails(ShowQueueDetails);
        }
        //internal static List<string> GetUrlForTopic(string pTopic)
        //{
        //    return TheQueuedSenderRegistry.GetUrlForTopic(pTopic, false);
        //}
        internal static void RegisterDebugLogCallback(Action<Action<string>> pCallback)
        {
            cdeStatus.eventEventLogRequested -= pCallback;
            cdeStatus.eventEventLogRequested += pCallback;
        }
        internal static void UnregisterDebugLogCallback(Action<Action<string>> pCallback)
        {
            cdeStatus.eventEventLogRequested -= pCallback;
        }

        /// <summary>
        /// This Event if fired when a CloudServiceRoute is established and up and running
        /// </summary>
        internal static Action<ICDEThing, TheChannelInfo> eventNewRelayConnection;
        /// <summary>
        /// This Event if fired when a CloudServiceRoute is disconnected or not available anymore that was active in the past.
        /// </summary>
        internal static Action<ICDEThing, TheChannelInfo> eventRelayConnectionDropped;

        internal static Action<TheSessionState> eventNewSessionCreated;
        /// <summary>
        /// Register callbacks for relay connection events
        /// </summary>
        /// <param name="psinkNewRelayConnection">Event is fired when a new connection was established to the Relay</param>
        /// <param name="psinkConnectionDropped">Event is fired when a connection was dropped from the Relay</param>
        /// <param name="psinkNewSession">Event is fired when a new session for a connection was created</param>
        public static void RegisterRelayEvents(Action<ICDEThing, TheChannelInfo> psinkNewRelayConnection, Action<ICDEThing, TheChannelInfo> psinkConnectionDropped=null,Action<TheSessionState> psinkNewSession=null)
        {
            if (psinkConnectionDropped != null)
            {
                eventRelayConnectionDropped -= psinkConnectionDropped; //fired if the cloud route is no longer active (cloud service not responding/down/or in maintenance)
                eventRelayConnectionDropped += psinkConnectionDropped; //fired if the cloud route is no longer active (cloud service not responding/down/or in maintenance)
            }
            if (psinkNewRelayConnection != null)
            {
                eventNewRelayConnection -= psinkNewRelayConnection; //fired when cloud route is back up
                eventNewRelayConnection += psinkNewRelayConnection; //fired when cloud route is back up
            }
            if (psinkNewSession != null)
            {
                eventNewSessionCreated -= psinkNewSession;
                eventNewSessionCreated += psinkNewSession;
            }
        }


        /// <summary>
        /// Unregister Relay Connection Events
        /// </summary>
        /// <param name="psinkNewRelayConnection">Event is fired when a new connection was established to the Relay</param>
        /// <param name="psinkConnectionDropped">Event is fired when a connection was dropped from the Relay</param>
        public static void UnregisterRelayEvents(Action<ICDEThing, TheChannelInfo> psinkNewRelayConnection, Action<ICDEThing, TheChannelInfo> psinkConnectionDropped)
        {
            if (psinkConnectionDropped != null)
                eventRelayConnectionDropped -= psinkConnectionDropped;   //fired if the cloud route is no longer active (cloud service not responding/down/or in maintenance)
            if (psinkNewRelayConnection != null)
                eventNewRelayConnection -= psinkNewRelayConnection;   //fired when cloud route is back up
        }

        /// <summary>
        /// Http Service Interface allowing to register "Interceptors" that allow to server http requests coming in to a node by a plugin
        /// </summary>
        public static IHttpInterceptor MyHttpService;
#if CDE_MINIHTTP
        internal static HttpService.cdeHTTPService MyWebService;
#else
        internal static HttpService.cdeMidiHttpService MyWebService;
#if CDE_USECSWS
        internal static TheWSServer MyWebSockets;
#endif
#if CDE_USEWSS8  //WebSockets Server on Windows 8 with Admin required
        internal static TheWSServer8 MyWebSockets8 ;
#endif
#endif
        internal static bool ValidateServerCertificate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate,
                                                                    System.Security.Cryptography.X509Certificates.X509Chain chain,
                                                                    System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            if (TheBaseAssets.MyServiceHostInfo.IgnoreServerCertificateErrors)
            {
                return true;
            }
            if ((sslPolicyErrors & System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable) != 0 && !TheBaseAssets.MyServiceHostInfo.IgnoreServerCertificateNotAvailable)
            {
                return false;
            }
            else if ((sslPolicyErrors & System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch) != 0 && !TheBaseAssets.MyServiceHostInfo.IgnoreServerCertificateNameMismatch)
            {
                return false;
            }
            else if ((sslPolicyErrors & System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors) != 0 && !TheBaseAssets.MyServiceHostInfo.IgnoreServerCertificateChainErrors)
            {
                return false;
            }
            var remainingPolicyErrors = sslPolicyErrors & ~(System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable | System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch | System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors);
            return remainingPolicyErrors == System.Net.Security.SslPolicyErrors.None;
       }

        internal static bool StartCommunications()
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback += ValidateServerCertificate;
            if (!TheBaseAssets.MyServiceHostInfo.DisableTls12)
            {
                try
                {
#if CDE_NET35 || CDE_NET4
                    System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)0x00000C00;
#else
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
#endif
                }
                catch
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(505, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommCore", $"Setting of SecurityProtocol to TLS1.2 failed. This node will only support {System.Net.ServicePointManager.SecurityProtocol}", eMsgLevel.l1_Error));
                }
            }
            if (TheBaseAssets.MyServiceHostInfo.cdeHostingType != cdeHostType.IIS && TheBaseAssets.MyServiceHostInfo.cdeHostingType != cdeHostType.ASPCore && !TheCommonUtils.IsHostADevice() && TheBaseAssets.MyServiceHostInfo.cdeNodeType != cdeNodeType.Active)
            {
#if CDE_MINIHTTP
                MyWebService = new HttpService.cdeHTTPService();
                TheBaseAssets.MySYSLOG.WriteToLog(2, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommCore", "MiniServer (Sockets-http-Server) starting...",eMsgLevel.l7_HostDebugMessage));
#else
                MyWebService = new HttpService.cdeMidiHttpService();
                TheBaseAssets.MySYSLOG.WriteToLog(2, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommCore", "MidiServer (http.sys-Server) starting...", eMsgLevel.l7_HostDebugMessage));
#endif

                if (!MyWebService.Startup(TheBaseAssets.MyServiceHostInfo.MyStationPort, true))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(505, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommCore", "MyWebService.Startup FAILED", eMsgLevel.l1_Error));
                    if (!TheBaseAssets.MyServiceHostInfo.DontFallbackToDevice)
                    {
                        MyWebService = null;
                        TheBaseAssets.MyServiceHostInfo.MyStationWSPort = 0;    //No WS Server
                        TheBaseAssets.MyServiceHostInfo.cdeNodeType = cdeNodeType.Active; //Fallback to Device
                    }
                    else
                        return false;
                }
                if (!TheBaseAssets.MyServiceHostInfo.DisableWebSockets && TheBaseAssets.MyServiceHostInfo.MyStationWSPort > 0
#if CDE_NET45 || CDE_STANDARD
                    && (TheBaseAssets.MyServiceHostInfo.MyStationWSPort != TheBaseAssets.MyServiceHostInfo.MyStationPort || !MyWebService.IsHttpListener)
#endif
                    )
                {
#if CDE_USEWSS8
                    if (TheBaseAssets.MyServiceHostInfo.IsWebSocket8Active)
                    {
                        MyWebSockets8 = new TheWSServer8();
                        MyWebSockets8.Startup();
                    }
                    if (MyWebSockets8 != null && MyWebSockets8.IsActive)
                    {

                        TheBaseAssets.MySYSLOG.WriteToLog(5050, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommCore", $"WebSocket8 (Windows 8+) Http-sys-Server started on different port ({TheBaseAssets.MyServiceHostInfo.MyStationWSPort}) as http ({TheBaseAssets.MyServiceHostInfo.MyStationPort})", eMsgLevel.l3_ImportantMessage));
                    }
                    else
#endif
                    {
                        TheBaseAssets.MyServiceHostInfo.IsWebSocket8Active = false;
                        if (TheBaseAssets.MyServiceHostInfo.MyStationWSPort == TheBaseAssets.MyServiceHostInfo.MyStationPort)
                        {
                            TheBaseAssets.MyServiceHostInfo.MyStationWSPort++;
                            TheBaseAssets.MySettings.UpdateLocalSettings();
                        }
                        MyWebSockets = new TheWSServer();
                        MyWebSockets.Startup();
                        if (MyWebSockets.IsActive)
                            TheBaseAssets.MySYSLOG.WriteToLog(5051, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommCore", "WebSocketCS Server Started (License see http://sta.github.io/websocket-sharp/)", eMsgLevel.l3_ImportantMessage));
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(5052, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommCore", "WebSocketCS Server failed to Start - WebSockets will be disabled and fallback to http is on", eMsgLevel.l1_Error));
                            TheBaseAssets.MyServiceHostInfo.DisableWebSockets = true;
                        }
                    }
                }
                else
                {
                    if (TheBaseAssets.MyServiceHostInfo.MyStationWSPort != TheBaseAssets.MyServiceHostInfo.MyStationPort || !(MyWebService?.IsHttpListener==true))
                        TheBaseAssets.MySYSLOG.WriteToLog(5053, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommCore", "WebSockets NOT Started", eMsgLevel.l2_Warning));
                }
            }
            TheQueuedSenderRegistry.RegisterCloudRoutes(TheBaseAssets.MyServiceHostInfo.ServiceRoute);
            return true;
        }

        internal static void StopCommunication()
        {
            MyWebService?.ShutDown();
#if CDE_USEWSS8
            if (MyWebSockets8 != null && MyWebSockets8.IsActive)
                MyWebSockets8.ShutDown();
#endif
#if CDE_USECSWS  //WebSockets Server required
            MyWebSockets?.ShutDown();
#endif
            TheBaseAssets.MySYSLOG?.WriteToLog(5053, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheCommCore", "Shutting down QueuedSenderRegistry ", eMsgLevel.l6_Debug));
            TheQueuedSenderRegistry.Shutdown();
            MyHttpService = null;
        }

        /// <summary>
        /// Get a list of all UserIDs (UIDs) of users currently logged in
        /// </summary>
        /// <returns></returns>
        public static List<Guid> GetCurrentUsers()
        {
            return TheBaseAssets.MySession?.GetCurrentUsers();
        }

        /// <summary>
        /// Use this method to publish the TargetMessage to the Originator in the SourceMessage
        /// This method can be used to reply only to the sender of an incoming message
        /// </summary>
        /// <param name="sourceMessage">Incoming request message containing the Originator</param>
        /// <param name="TargetMessage">Message to be sent to the Originator of the SourceMessage</param>
        public static void PublishToOriginator(TSM sourceMessage, TSM TargetMessage)
        {
            PublishToOriginator(sourceMessage, TargetMessage, false);
        }
        /// <summary>
        /// Use this method to publish the TargetMessage to the Originator in the SourceMessage
        /// This method can be used to reply only to the sender of an incoming message
        /// </summary>
        /// <param name="sourceMessage">Incoming request message containing the Originator</param>
        /// <param name="TargetMessage">Message to be sent to the Originator of the SourceMessage</param>
        /// <param name="IncludeLocalNode">If set to true, the current node will by called in its "ProcessServiceMessage"
        /// handler if the ENG is a live engine on the current node</param>
        public static void PublishToOriginator(TSM sourceMessage, TSM TargetMessage, bool IncludeLocalNode)
        {
            if (sourceMessage == null || TargetMessage==null) return;
            TargetMessage.SID = sourceMessage.SID;
            Guid tOrg=sourceMessage.GetOriginator();
            if (tOrg==Guid.Empty) return;
            TargetMessage.GRO = sourceMessage.ORG;
            Guid tOriginatorThing = sourceMessage.GetOriginatorThing();
            if (tOriginatorThing != Guid.Empty)
            {
                TargetMessage.OWN = tOriginatorThing.ToString();
            }

            //PublishCentral(TheBaseAssets.MyScopeManager.AddScopeID("CDE_SYSTEMWIDE;" + tOrg, sourceMessage, true), TargetMessage, false, null, IncludeLocalNode);
            var topicWithScope = TheBaseAssets.MyScopeManager.AddScopeID("CDE_SYSTEMWIDE;" + tOrg, sourceMessage.SID,ref sourceMessage.SID, true, false);
            TheBaseAssets.MySYSLOG.WriteToLog(5060, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("TheCommCore", $"PublishToOriginator: {topicWithScope} {tOrg} {TargetMessage.SID} {sourceMessage.SID} {IncludeLocalNode}", eMsgLevel.l6_Debug, TSM.L(eDEBUG_LEVELS.VERBOSE) ? TargetMessage.TXT : TargetMessage?.ToString()), false);
            PublishCentral(topicWithScope, TargetMessage, false, null, IncludeLocalNode);
        }
        /// <summary>
        /// Use this method to send a message with a given ScrambledScopeID to a specific Node specified in the pTargetGuid
        /// </summary>
        /// <param name="pTargetGuid">Target Node the message should be sent to</param>
        /// <param name="SScopeID">Scrambled ScopeID. Only if the Target Node is of this scope, it can receive the message</param>
        /// <param name="TargetMessage">Message to be sent</param>
        public static void PublishToNode(Guid pTargetGuid, string SScopeID, TSM TargetMessage)
        {
            if (TargetMessage == null) return;
            if (pTargetGuid == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)
            {
                IBaseEngine tBase = TheThingRegistry.GetBaseEngine(TargetMessage.ENG);
                if (tBase != null)
                {
                    tBase.ProcessMessage(new TheProcessMessage(TargetMessage));
                }
                else
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(5060, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommCore", $"PublishToNode with scope: BaseEngine {TargetMessage.ENG} not found. Not delivering message.", eMsgLevel.l1_Error, TSM.L(eDEBUG_LEVELS.VERBOSE) ? TargetMessage.TXT : TargetMessage?.ToString()), false);
                }
            }
            else
            {
                string tNoMsgSid = null;
                PublishCentral(TheBaseAssets.MyScopeManager.AddScopeID("CDE_SYSTEMWIDE;" + pTargetGuid, SScopeID, ref tNoMsgSid, true, false), TargetMessage, false, null);
            }
        }

        /// <summary>
        /// Use this method to send a message to a specific Node specified in the pTargetGuid
        /// The Message will be scoped using the current Node Scope
        /// </summary>
        /// <param name="pTargetGuid">Target Node the message should be sent to</param>
        /// <param name="TargetMessage">Message to be sent</param>
        public static void PublishToNode(Guid pTargetGuid, TSM TargetMessage)
        {
            if (TargetMessage == null || pTargetGuid == Guid.Empty) return;
            if (TheBaseAssets.MyScopeManager?.IsScopingEnabled==true && string.IsNullOrEmpty(TargetMessage.SID))
            {
                TheBaseAssets.MySYSLOG.WriteToLog(5060, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCommCore", $"PublishToNode: No SID on TargetMessage TXT={TargetMessage.TXT}. PublishToOriginator from another node may not work correctly.", eMsgLevel.l2_Warning, ""), false);
            }
            if (pTargetGuid == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)
            {
                IBaseEngine tBase = TheThingRegistry.GetBaseEngine(TargetMessage.ENG);
                if (tBase != null)
                {
                    tBase.ProcessMessage(new TheProcessMessage(TargetMessage));
                }
                else
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(5060, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommCore", $"PublishToNode: BaseEngine {TargetMessage.ENG} not found. Not delivering message.", eMsgLevel.l1_Error, TSM.L(eDEBUG_LEVELS.VERBOSE) ? TargetMessage.TXT : TargetMessage?.ToString()), false);
                }
            }
            else
                PublishCentral(TheBaseAssets.MyScopeManager.AddScopeID("CDE_SYSTEMWIDE;" + pTargetGuid,true, TheBaseAssets.MyScopeManager.ScopeID), TargetMessage, false, null);
        }


        /// <summary>
        /// Allows to subscribe to any arbitrary publication you are interested
        /// </summary>
        /// <param name="pTopics"></param>
        public static void SubscribeCentral(string pTopics)
        {
            TSM TargetMessage = new TSM(eEngineName.ContentService, "CDE_SUBSCRIBE", pTopics);
            PublishCentral(TheBaseAssets.MyScopeManager.AddScopeID("CDE_SYSTEMWIDE",true, TheBaseAssets.MyScopeManager.ScopeID), TargetMessage, false, null);
        }

        /// <summary>
        /// Publishes the TSM only to a service ignoring Client End Nodes
        /// </summary>
        /// <param name="TargetMessage"></param>
        public static void PublishToService(TSM TargetMessage)
        {
            PublishToService(TargetMessage, false);
        }

        /// <summary>
        /// Publishes the TSM only to a service ignoring Client End Nodes
        /// </summary>
        /// <param name="TargetMessage"></param>
        /// <param name="IncludeLocalNode">if set to true, the current node will by called in its "ProcessServiceMessage" handler if the ENG is a live engine on the current node</param>
        public static void PublishToService(TSM TargetMessage, bool IncludeLocalNode)
        {
            if (TargetMessage == null) return;
            TargetMessage.SetToServiceOnly(true);
            PublishCentral(TargetMessage,IncludeLocalNode);
        }

        /// <summary>
        /// Use this method to send a message to the first connected node with the given ScopeID
        /// The ENG parameter in the TargetMessage will determine the FirstNode of this message will be addressed to.
        /// This messsage will NOT be relayed to other nodes
        /// </summary>
        /// <param name="SScopeID">Scrambled Scope ID</param>
        /// <param name="TargetMessage">Message to be sent</param>
        /// <returns></returns>
        public static bool PublishToFirstNode(string SScopeID, TSM TargetMessage)
        {
            if (TargetMessage == null) return false;
            Guid tOrg = Guid.Empty;
            IBaseEngine tBase = TheThingRegistry.GetBaseEngine(TargetMessage.ENG);
            if (tBase != null)
                tOrg = tBase.GetFirstNode();
            if (tOrg != Guid.Empty)
            {
                string tNoMsgSID = null;
                PublishCentral(TheBaseAssets.MyScopeManager.AddScopeID("CDE_SYSTEMWIDE;" + tOrg, SScopeID, ref tNoMsgSID, true, false), TargetMessage, false, null);
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// use the method to send a Message to ALL nodes in the Solution
        /// The ENG parameter of the pMessage will be used as the Topic for the publication
        /// </summary>
        /// <param name="pMessage"></param>
        public static void PublishCentral(TSM pMessage)
        {
            PublishCentral(pMessage.ENG, pMessage, true, null);
        }

        /// <summary>
        /// Use this method to send a Message to ALL nodes in the Solution
        /// The ENG parameter of the pMessage will be used as the Topic for
        /// the publication.
        /// </summary>
        /// <param name="pMessage">TSM to be sent to the subscribing nodes.</param>
        /// <param name="IncludeLocalNode">If set to true, the current node will be called.</param>
        public static void PublishCentral(TSM pMessage, bool IncludeLocalNode)
        {
            PublishCentral(pMessage.ENG, pMessage, true, null, IncludeLocalNode);
        }
        /// <summary>
        /// Use this method to send a message to all nodes in the solution that have an active subscription for the pTopic
        /// </summary>
        /// <param name="pTopic"></param>
        /// <param name="pMessage"></param>
        public static void PublishCentral(string pTopic, TSM pMessage)
        {
            PublishCentral(pTopic, pMessage, true,null);
        }
        /// <summary>
        /// Use this method to send a message to all nodes in the solution that have an active subscription of the custom Scope for the pTopic
        /// </summary>
        /// <param name="pTopic"></param>
        /// <param name="SScopeID">Scrambled ScopeID. Only if the Target Node is of this scope, it can receive the message</param>
        /// <param name="pMessage"></param>
        public static void PublishCentral(string pTopic,string SScopeID, TSM pMessage)
        {
            if (pMessage == null) return;
            pTopic = TheBaseAssets.MyScopeManager.AddScopeID(pTopic,SScopeID,ref pMessage.SID, true,false);
            PublishCentral(pTopic, pMessage, false, null);
        }

        internal static void PublishCentral(string pTopic, TSM pMessage, bool AddScopeToTopic, Action<TSM> pLocalCallback, bool IncludeLocal = false)
        {
            if (!TheBaseAssets.MasterSwitch || pMessage == null) return;
            //if (AddScopeToTopic)
            //    pTopic = TheBaseAssets.MyScopeManager.AddScopeID(pTopic, pMessage, true);
#if !JC_COMMDEBUG
            try
            {
#endif

                var tEng = TheThingRegistry.GetBaseEngineAsThing(pMessage.ENG);
                if (tEng?.HasRegisteredEvents("PublishCentraled")==true)
                    tEng?.FireEvent("PublishCentraled",tEng, new TheProcessMessage { Topic = pTopic, Message = TSM.Clone(pMessage,false) }, true);

                TheQueuedSenderRegistry.DoPublish(pTopic, pMessage,AddScopeToTopic, pLocalCallback);
                //return; // Must go through local as well
#if !JC_COMMDEBUG
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(5055, new TSM("TheCommCore", "Fatal Error in PublishCentral! Publishing to Topic:" + pTopic, eMsgLevel.l1_Error, e.ToString()), false);
            }
#endif
            if (IncludeLocal)
            {
                IBaseEngine tBase = TheThingRegistry.GetBaseEngine(pMessage.ENG);
                tBase?.ProcessMessage(new TheProcessMessage(pMessage));
            }
        }

        internal static void SendError(string ErrorText, TheRequestData pRequestData, eHttpStatusCode tCode)
        {
            string tDeviceID = "unkown";
            if (pRequestData.Header != null && pRequestData.Header.Count > 0)
            {
                tDeviceID = pRequestData.Header.cdeSafeGetValue("cdeDeviceID");
                if (!string.IsNullOrEmpty(tDeviceID))
                {
                    //New 4.0120: Teardown of any QSender that had an illegal request coming in
                    Guid tDID = TheCommonUtils.CGuid(tDeviceID);
                    if (Guid.Empty!=tDID)
                    {
                        var tSend=TheQueuedSenderRegistry.GetSenderByGuid(tDID);
                        tSend?.DisposeSender(true);
                        TheBaseAssets.MySYSLOG.WriteToLog(50561, new TSM("TheCommCore", $"Teardown of QSender for NodeID {tDID} after error {ErrorText}", eMsgLevel.l3_ImportantMessage), false);
                    }
                }
            }
            if (pRequestData.WebSocket != null)
            {
                pRequestData.ResponseBuffer = null;
                var t = pRequestData.WebSocket as TheWSProcessorBase;
                t?.Shutdown(true, ErrorText);
            }
            else
            {
                pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(ErrorText);
                pRequestData.ResponseMimeType = "text/html";
                if (tCode != 0)
                    pRequestData.StatusCode = (int)tCode;
                else
                    pRequestData.StatusCode = (int)eHttpStatusCode.ServerError;
                pRequestData.AllowStatePush = false;
                pRequestData.EndSessionOnResponse = true;
            }
            TheBaseAssets.MySYSLOG.WriteToLog(50562, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpService", $"SendError: {ErrorText} from {TheCommonUtils.GetDeviceIDML(TheCommonUtils.CGuid(tDeviceID))}", eMsgLevel.l1_Error));
        }

        internal static bool ProcessISBRequest(TheRequestData pRequestData)
        {
            bool IsValidISB = false;
            if (pRequestData.cdeRealPage.StartsWith("/ISB"))
            {
                string tRealPage = pRequestData.cdeRealPage.Substring(4);
                if (tRealPage.EndsWith(".ashx"))
                {
                    tRealPage = tRealPage.Substring(0, tRealPage.Length - 5);
                    IsValidISB = true;
                }
                if (TheBaseAssets.MyScopeManager.ParseISBPath(tRealPage, out Guid? tSessionID, out cdeSenderType tSenderType, out long tFID, out string tVersion))
                {
                    bool DoCreateNewSession = false;
                    IsValidISB = true;
                    TheQueuedSender tSend;

                    if ((tSenderType != cdeSenderType.CDE_JAVAJASON || !TheBaseAssets.MyServiceHostInfo.RequiresConfiguration) && !TheBaseAssets.MyServiceHostInfo.IsCloudService && !TheBaseAssets.MyScopeManager.IsScopingEnabled)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(235, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpService", "Incoming ISB Request detected but Scope not set, yet. Will be rejected until scope is set.", eMsgLevel.l2_Warning));
                        pRequestData.StatusCode = (int)eHttpStatusCode.NotAcceptable;
                        return IsValidISB;
                    }
                    if (pRequestData.WebSocket == null || pRequestData.SessionState == null)
                    {
                        if (tSessionID!=null)
                        {
                            if (tSessionID == Guid.Empty)
                            {
                                DoCreateNewSession = true;
                                TheBaseAssets.MySYSLOG.WriteToLog(5056, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"New Session Request: {pRequestData.cdeRealPage} ID:{tSessionID}", eMsgLevel.l3_ImportantMessage));
                                if (pRequestData.DeviceID != Guid.Empty)
                                {
                                    if (TheBaseAssets.MyServiceHostInfo.NodeBlacklist.Contains(pRequestData.DeviceID))
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(2300, new TSM("QueuedSender", $"NodeID {pRequestData.DeviceID} is blacklisted and will be rejected", eMsgLevel.l1_Error));
                                        TheLoggerFactory.LogEvent(eLoggerCategory.NodeConnect, "NodeID is blacklisted", eMsgLevel.l4_Message, $"{pRequestData.DeviceID}");
                                        SendError("1701:Node blacklisted", pRequestData, eHttpStatusCode.AccessDenied);
                                        return IsValidISB;
                                    }
                                    TheBaseAssets.MySession.RemoveSessionsByDeviceID(pRequestData.DeviceID, Guid.Empty);
                                }
                                TheCDEKPIs.IncrementKPI(eKPINames.BruteDelay);
                                //Security Fix: ID#770 - wait 200 ms before returning anything with error code to limit BruteForce
                                TheCommonUtils.SleepOneEye(200, 100);
                            }
                        }
                        else
                        {
                            SendError("1702:Illegal Post - wrong ISB Parameter", pRequestData, eHttpStatusCode.NotAcceptable);
                            return IsValidISB;
                        }

                        if (pRequestData.SessionState == null)
                        {
                            TheBaseAssets.MySession.GetSessionState(TheCommonUtils.CGuid(tSessionID), pRequestData, !DoCreateNewSession);
                        }
                        if (pRequestData.SessionState == null)
                        {
                            SendError($"1703:No Valid Session Found for SessionID:{tSessionID} possibly expired - request terminated.", pRequestData, eHttpStatusCode.AccessDenied);
                            return IsValidISB;
                        }
                        if (pRequestData.WebSocket == null && pRequestData.SessionState.FID > tFID && tSenderType == cdeSenderType.CDE_JAVAJASON)
                        {
                            SendError($"1704Illegal Post - Security Violation - SessionID:{tSessionID} has FID:{pRequestData.SessionState.FID} but incoming telegram has FID:{tFID}", pRequestData, eHttpStatusCode.AccessDenied);
                            return IsValidISB;

                        }
                        pRequestData.SessionState.SiteVersion = tVersion;
                        pRequestData.SessionState.FID = tFID;
                    }
                    if (tSenderType==cdeSenderType.CDE_JAVAJASON && pRequestData.SessionState!=null && pRequestData.SessionState.cdeMID!=tSessionID)
                        TheBaseAssets.MySession.GetSessionState(TheCommonUtils.CGuid(tSessionID), pRequestData, !DoCreateNewSession);
                    if (MyHttpService != null && TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage > 0 && pRequestData.SessionState?.CertScopes==null)
                    {
                        List<string> tEasyScopes = new List<string>();
                        //TODO:KPIs here
                        switch (TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage)
                        {
                            case 4: //Cert MUST contain scope - if that does not match the SCope in the connect TDM connection will be closed
                            case 3: //Cert MUST contain Scopes and can contain multiple scopes
                            case 1: //Cert CAN contain Scopes
                                tEasyScopes = MyHttpService.GetScopesFromClientCertificate(pRequestData);
                                break;
                            case 2: //Cert ROOT Must be valid (
                                if (!string.IsNullOrEmpty(MyHttpService.ValidateCertificateRoot(pRequestData))
                                    && (tSenderType != cdeSenderType.CDE_JAVAJASON || TheBaseAssets.MyServiceHostInfo.DisableNMI)
                                    )
                                {
                                    SendError($"1705:This node requires valid Client Certificates issued by a specific root. The cert provided has no valid root. request denied and terminated.", pRequestData, eHttpStatusCode.AccessDenied);
                                    TheCDEKPIs.IncrementKPI(eKPINames.RejectedClientConnections);
                                    return IsValidISB;
                                }
                                tEasyScopes = MyHttpService.GetScopesFromClientCertificate(pRequestData);
                                break;
                        }
                        pRequestData.SessionState.CertScopes = new List<string>();
                        foreach (string tEZ in tEasyScopes)
                        {
                            pRequestData.SessionState.CertScopes.Add(TheBaseAssets.MyScopeManager.GetRealScopeIDFromEasyID(tEZ));
                        }
                    }
                    if (TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage > 2 && pRequestData.SessionState?.CertScopes?.Count==0
                        && (tSenderType!=cdeSenderType.CDE_JAVAJASON || TheBaseAssets.MyServiceHostInfo.DisableNMI)
                        ) //If no scopes found but was required by UseClientCert policy - connection not permitted
                    {
                        SendError($"1706:This node requires valid Client Certificates with valid scopes but incoming connection request did not provide a proper client certificate - request denied and terminated.", pRequestData, eHttpStatusCode.AccessDenied);
                        TheCDEKPIs.IncrementKPI(eKPINames.RejectedClientConnections);
                        return IsValidISB;
                    }
                    if (pRequestData.SessionState.MyDevice != Guid.Empty)
                        pRequestData.DeviceID = pRequestData.SessionState.MyDevice;
                    else if (pRequestData.DeviceID != Guid.Empty && pRequestData.SessionState.MyDevice != pRequestData.DeviceID)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(5057, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", $"1-DeviceIDs Different: {TheCommonUtils.GetDeviceIDML(pRequestData.SessionState.MyDevice)} != {TheCommonUtils.GetDeviceIDML(pRequestData.DeviceID)}", eMsgLevel.l2_Warning));
                        pRequestData.SessionState.MyDevice = pRequestData.DeviceID;
                    }
                    if (((TheWSProcessorBase)pRequestData.WebSocket)?.MyQSender != null)
                    {
                        tSend = ((TheWSProcessorBase)pRequestData.WebSocket).MyQSender;
                        if (tSend == null || tSend.MyTargetNodeChannel == null)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(299, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"WS: MyTNC not set yet. Sender:{tSend != null} MyTNC:{tSend?.MyTargetNodeChannel != null} ReqSession:{pRequestData.SessionState?.cdeMID}", eMsgLevel.l6_Debug));
                            tSend = null;
                            ((TheWSProcessorBase)pRequestData.WebSocket).MyQSender = null;
                        }
                        else
                        {
                            if (tSend.MyTargetNodeChannel.MySessionState == null && pRequestData.SessionState != null)
                                tSend.MyTargetNodeChannel.MySessionState = pRequestData.SessionState;

                            //New in 3.083: Fix for Bug78: Browser needs to be removed if Session State is gone
                            if (tSend.MyTargetNodeChannel?.MySessionState == null || tSend.MyTargetNodeChannel?.MySessionState.HasExpired == true)
                            //TheBaseAssets.MySession.ValidateSEID(pRequestData.SessionState.cdeMID) == null)   //Target should have session state at this stage.
                            {
                                pRequestData.ErrorDescription = "1707:Session No longer valid";
                                pRequestData.EndSessionOnResponse = true;
                                SendError(pRequestData.ErrorDescription, pRequestData, eHttpStatusCode.ServerError);
                                tSend?.FireSenderProblem(pRequestData);
                                return IsValidISB;
                            }
                            else
                            {
                                if (tSend != null && tSend.MyTargetNodeChannel != null && tSend.MyTargetNodeChannel.MySessionState != null && pRequestData.SessionState != tSend?.MyTargetNodeChannel?.MySessionState)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(299, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"WS: Matching existing QSenderSession to Request Session. QSSession:{tSend.MyTargetNodeChannel.MySessionState?.cdeMID} ReqSession:{pRequestData.SessionState?.cdeMID}", eMsgLevel.l3_ImportantMessage));
                                    TheBaseAssets.MySession.RemoveSession(pRequestData.SessionState);
                                    pRequestData.SessionState = tSend.MyTargetNodeChannel.MySessionState;
                                    //TheBaseAssets.MySession.RemoveSessionsByDeviceID(tSend.MyTargetNodeChannel.cdeMID, tSend.MyTargetNodeChannel.MySessionState.cdeMID);
                                }
                            }
                        }
                    }
                    else
                    {
                        tSend = TheQueuedSenderRegistry.GetSenderByGuid(pRequestData.DeviceID);
                        if (DoCreateNewSession)
                        {
                            var success = CreateSenderFromISB(pRequestData, IsValidISB, ref tSenderType, ref tSend);
                            if (!success || !TheBaseAssets.MyServiceHostInfo.AllowMessagesInConnect || pRequestData.PostDataLength == 0)
                            {
                                // If there's a payload, process it to support single post interactions (i.e. MSB scenario)
                                return success;
                            }
                        }
                        else
                        {
                            if (tSend?.MyTargetNodeChannel!=null && tSend?.MyTargetNodeChannel?.MySessionState?.cdeMID != pRequestData?.SessionState?.cdeMID) //CODE-REVIEW: 4.0112 saw crashes here under stress. Does not look right yet
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(5057, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", $"Session states are Different for {tSend?.MyTargetNodeChannel?.cdeMID}: Req={pRequestData?.SessionState?.cdeMID} != QS={tSend?.MyTargetNodeChannel?.MySessionState?.cdeMID}", eMsgLevel.l2_Warning));
                                TheBaseAssets.MySession.RemoveSessionsByDeviceID(tSend.MyTargetNodeChannel.cdeMID, tSend.MyTargetNodeChannel.MySessionState.cdeMID);
                                pRequestData.SessionState = tSend.MyTargetNodeChannel.MySessionState;
                            }
                        }
                    }
                    TheBaseAssets.MySession.SetVer(pRequestData, false, IsValidISB, Guid.Empty);

                    if (tSend?.MyTargetNodeChannel == null)
                    {
                        if (tSend == null || (tSend != null && tSend.HasWebSockets()))
                            return CreateSenderFromISB(pRequestData, IsValidISB, ref tSenderType, ref tSend);
                        TheBaseAssets.MySYSLOG.WriteToLog(5058, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", $"ProcessISBRequest Intrusion Detected: {TheCommonUtils.GetDeviceIDML(pRequestData.SessionState.MyDevice)}", eMsgLevel.l1_Error));
                        SendError("1708:Illegal Post - Security Violation: QSender Not found", pRequestData, eHttpStatusCode.AccessDenied);
                        return IsValidISB;
                    }

                    string Topic = null;
                    List<TheDeviceMessage> tDevMsg = null;
                    if (!string.IsNullOrEmpty(pRequestData.RequestUri.Query) && tSenderType == cdeSenderType.CDE_JAVAJASON)   //TODO: Check if we can make sure topics are only coming from JS DONE 4.205.1
                    {
                        Topic = TheCommonUtils.cdeUnescapeString(pRequestData.RequestUri.Query.Substring(1));
                        if (CheckSimpleTopics(Topic, pRequestData, tSend))
                            return IsValidISB;
                    }
                    else
                    {
                        if (tSend != null && !tSend.MyTargetNodeChannel.IsWebSocket && pRequestData.WebSocket != null)
                            tSend.SetWebSocketProcessor(pRequestData.WebSocket);
                        if (pRequestData.PostData != null)
                        {
                            string tJSON;
                            if (pRequestData.PostData[0] == 0)
                            {
                                SendError("1709:Illegal Post - (Null Byte) Security Violation. Firefox 38.0.5 WebSockets bug? ", pRequestData, eHttpStatusCode.AccessDenied);
                                return IsValidISB;
                            }
                            if (pRequestData.PostData.Length > 2)
                            {
                                if (pRequestData.PostData.Length > 1 && pRequestData.PostData[0] == (byte)'[' && pRequestData.PostData[1] == (byte)'{' && pRequestData.PostData[2] == (byte)'\"')
                                    tJSON = TheCommonUtils.CArray2UTF8String(pRequestData.PostData, 0, pRequestData.PostDataLength);
                                else
                                    tJSON = TheCommonUtils.cdeDecompressToString(pRequestData.PostData, 0, pRequestData.PostDataLength);
                                if (!string.IsNullOrEmpty(tJSON))
                                {
                                    try
                                    {
                                        tDevMsg = TheDeviceMessage.DeserializeJSONToObject(tJSON);
                                    }
                                    catch (Exception)
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(5059, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpService", "Intrusion detected - Malformated Telegram Envelope - Communication closed", eMsgLevel.l2_Warning));
                                        SendError("1710:Intrusion detected - Malformated Telegram Envelope - Communication closed", pRequestData, eHttpStatusCode.ServerError);
                                        return IsValidISB;
                                    }
                                    List<TheDeviceMessage> toRemove = null;
                                    foreach (TheDeviceMessage tDev in tDevMsg)
                                    {
                                        if (CheckSimpleTopics(tDev.TOP, pRequestData, tSend))
                                        {
                                            if (tDevMsg.Count == 1)
                                            {
                                                return IsValidISB;
                                            }
                                            if (toRemove == null)
                                            {
                                                toRemove = new List<TheDeviceMessage>();
                                            }
                                            toRemove.Add(tDev);
                                        }
                                    }
                                    if (toRemove != null)
                                    {
                                        foreach (var tDev in toRemove)
                                        {
                                            tDevMsg.Remove(tDev);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (pRequestData.SessionState != null)
                    {
                        try
                        {
                            if (tSend.IsConnecting)
                                tSend.IsConnected = true;
                            tSend?.SetLastHeartbeat(pRequestData.SessionState);
                            TheCorePubSub.ExecuteCommand(Topic, tSend, true, pRequestData, tDevMsg);
                        }
                        catch (Exception ee)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(5059, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpService", "Execute Command Error", ee.ToString()));
                            SendError($"1711:{ee}", pRequestData, eHttpStatusCode.ServerError);
                        }
                    }
                    else
                    {
                        // Log?
                    }
                }
                else
                    TheBaseAssets.MySYSLOG.WriteToLog(50510, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpService", "Illegal ISB attempt - License Key Violation"));
            }
            return IsValidISB;
        }

        private static bool CheckSimpleTopics(string Topic, TheRequestData pRequestData,TheQueuedSender tSend)
        {
            if (string.IsNullOrEmpty(Topic) || Topic.StartsWith("CDE_PICKUP"))
            {
                tSend?.SetLastHeartbeat(pRequestData.SessionState);
                TheCorePubSub.SetResponseBuffer(pRequestData, tSend.MyTargetNodeChannel, false, "");
                return true;
            }
            if (Topic.StartsWith("CDE_INITWS"))
            {
                tSend?.SetLastHeartbeat(pRequestData.SessionState);
                TheCorePubSub.SetResponseBuffer(pRequestData, tSend.MyTargetNodeChannel, false, "CDE_WSINIT"); //Must be WSINIT as there are no legal subs for JAVASCRIPT at this point
                return true;
            }
            if (Topic.StartsWith("CDE_MESHSELECT"))
            {
                string tLogRes = "ERR:CDE_MESHSELECT_FAILURE";
                string rToken = null;
                Guid MesID = TheCommonUtils.CGuid(Topic.Substring("CDE_MESHSELECT:".Length));
                if (MesID!=Guid.Empty && pRequestData?.SessionState?.Meshes?.Count>0)
                {
                    TheMeshPicker tPic = pRequestData?.SessionState?.Meshes.Find(s => s.cdeMID == MesID);
                    if (tPic!=null)
                    {
                        var tUser = TheUserManager.GetUserByID(tPic.UserID);
                        if (tUser != null)
                        {
                            TheUserManager.SetUserInSessionState(pRequestData, tUser);
                            tLogRes = SendLoginSuccess(pRequestData, tSend, tUser);
                            rToken = TheUserManager.AddTokenToUser(tUser);
                        }
                    }
                }
                pRequestData.SessionState.Meshes = null; //For security reasons we only give the browser one shot! If CDE_MESHSELECT comes in a second time it will no longer be parsed
                if (tLogRes.StartsWith("ERR"))
                {
                    TheCDEKPIs.IncrementKPI(eKPINames.BruteDelay);
                    //Security Fix: ID#770 - wait 200 ms before returning anything with error code to limit BruteForce
                    TheCommonUtils.SleepOneEye(200, 100);
                }
                tSend?.SetLastHeartbeat(pRequestData.SessionState);
                TheCorePubSub.SetResponseBuffer(pRequestData, tSend.MyTargetNodeChannel, true, tLogRes, pRequestData.SessionState.OrigThing, rToken);
                return true;
            }
            if (Topic.StartsWith("CDE_LOGIN") || Topic.StartsWith("CDE_TLOGIN"))
            {
                string tLogRes = "ERR:CDE_LOGIN_FAILURE";
                Guid originatingThing = Guid.Empty;
                string rToken = null;
                if (Topic.Length > 9)
                {
                    int IsTLOGIN = Topic.StartsWith("CDE_TLOGIN") ? 1 : 0;
                    string[] tCreds = TheCommonUtils.cdeSplit(Topic.Substring(9 + IsTLOGIN, Topic.Length - (9 + IsTLOGIN)), ":,:", true, true);
                    if (TheBaseAssets.MyServiceHostInfo.DisableRSAToBrowser || IsTLOGIN == 1)
                    {
                        tCreds = TheCommonUtils.cdeSplit(tCreds[0], ":;:", true, true);
                    }
                    else
                    {
                        var encryptedCreds = TheCommonUtils.cdeSplit(tCreds[0], ":", true, true)[0]; // The regular QueuedSender adds additional information to the topic, separated with ":" - remove this suffix
                        var decryptedCreds = TheCommonUtils.cdeRSADecrypt(pRequestData.SessionState.cdeMID, encryptedCreds);
                        tCreds = TheCommonUtils.cdeSplit(decryptedCreds, ":;:", true, true);
                    }
                    if (tCreds.Length > (1 - IsTLOGIN))
                    {
                        List<TheUserDetails> tUsers = IsTLOGIN == 0 ?
                            TheUserManager.PerformLogin(pRequestData, tCreds[0], tCreds[1]) :
                            TheUserManager.PerformLoginByRefreshToken(pRequestData, tCreds[0]);
                        if (tUsers?.Count > 0)
                        {
                            TheBaseAssets.MySession.WriteSession(pRequestData.SessionState);
                            if (tUsers?.Count == 1)
                            {
                                var tUser = tUsers[0];
                                tLogRes = SendLoginSuccess(pRequestData, tSend, tUser);
                                rToken = TheUserManager.AddTokenToUser(tUser);
                            }
                            else
                            {
                                //New in 4.209 Mesh Selection
                                tLogRes = $"SELECT_MESH:{TheCommonUtils.SerializeObjectToJSONString(pRequestData.SessionState.Meshes)}";
                            }
                        }
                        if (tCreds.Length > (2 - IsTLOGIN))
                        {
                            originatingThing = TheCommonUtils.CGuid(tCreds[2 - IsTLOGIN]);
                            pRequestData.SessionState.OrigThing = originatingThing;
                        }
                    }
                }
                if (tLogRes.StartsWith("ERR"))
                {
                    TheCDEKPIs.IncrementKPI(eKPINames.BruteDelay);
                    //Security Fix: ID#770 - wait 200 ms before returning anything with error code to limit BruteForce
                    pRequestData.EndSessionOnResponse = true;
                    TheCommonUtils.SleepOneEye(200, 100);
                }
                tSend?.SetLastHeartbeat(pRequestData.SessionState);
                TheCorePubSub.SetResponseBuffer(pRequestData, tSend.MyTargetNodeChannel, true, tLogRes, originatingThing, rToken);
                return true;
            }
            if (Topic.StartsWith("LOGIN_SUCCESS"))
            {
                return true;
            }
            if (Topic.StartsWith("ERR:CDE_LOGIN_FAILURE"))
            {
                return true;
            }

            if (Topic.StartsWith("CDE_SETESID"))
            {
                string tSidRes = "ERR:CDE_LOGIN_FAILURE";
                string rToken = null;
                if (!string.IsNullOrEmpty(pRequestData?.SessionState?.TETO))
                {
                    string tPin = Topic.Substring(11);
                    if (!TheBaseAssets.MyServiceHostInfo.DisableRSAToBrowser)
                        tPin = TheCommonUtils.cdeRSADecrypt(pRequestData.SessionState.cdeMID, tPin);
                    var tUsers=TheUserManager.PerformLoginByPin(pRequestData, tPin);
                    if (tUsers?.Count > 0)
                    {
                        TheBaseAssets.MySession.WriteSession(pRequestData.SessionState);
                        if (tUsers?.Count == 1)
                        {
                            var tUser = tUsers[0];
                            tSidRes = SendLoginSuccess(pRequestData, tSend, tUser);
                            rToken = TheUserManager.AddTokenToUser(tUser);
                        }
                        else
                        {
                            //New in 4.209 Mesh Selection
                            tSidRes = $"SELECT_MESH:{TheCommonUtils.SerializeObjectToJSONString(pRequestData.SessionState.Meshes)}";
                        }
                    }
                }
                else if (!TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper)
                {
                    if (Topic.Length > 11) //tUser.PrimaryRole.Equals("ADMIN") &&
                    {
                        string tscope = Topic.Substring(11, Topic.Length - 11);
                        if (!TheBaseAssets.MyServiceHostInfo.DisableRSAToBrowser)
                            tscope = TheCommonUtils.cdeRSADecrypt(pRequestData.SessionState.cdeMID, tscope);
                        TheUserDetails tUser = TheUserManager.PerformLoginByScope(pRequestData, tscope);
                        if (tUser != null)
                        {
                            TheBaseAssets.MySession.WriteSession(pRequestData.SessionState);
                            tSidRes = string.Format("LOGIN_SUCCESS:{0}:{1}:{2}", TheUserManager.GetUserHomeScreen(pRequestData, tUser), tUser.Name, tUser.GetUserPrefString());
                        }
                    }
                }
                if (tSidRes.StartsWith("ERR"))
                {
                    TheCDEKPIs.IncrementKPI(eKPINames.BruteDelay);
                    //Security Fix: ID#770 - wait 200 ms before returning anything with error code to limit BruteForce
                    pRequestData.EndSessionOnResponse = true;
                    TheCommonUtils.SleepOneEye(200, 100);
                }
                tSend?.SetLastHeartbeat(pRequestData.SessionState);
                TheCorePubSub.SetResponseBuffer(pRequestData, tSend.MyTargetNodeChannel, true, tSidRes, Guid.Empty, rToken);
                return true;
            }
            return false;
        }

        private static string SendLoginSuccess(TheRequestData pRequestData, TheQueuedSender tSend, TheUserDetails tUser)
        {
            string tLogRes = string.Format("LOGIN_SUCCESS:{0}:{1}:{2}", TheUserManager.GetUserHomeScreen(pRequestData, tUser), tUser.Name, tUser.GetUserPrefString());
            if (string.IsNullOrEmpty(tSend.MyTargetNodeChannel.RealScopeID))    //RScope-OK: Users only on primary scope
                tSend.UpdateSubscriptionScope(TheBaseAssets.MyScopeManager.ScopeID);
            if (tSend.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON)
            {
                var tSubs = $"{eEngineName.ContentService}@{TheBaseAssets.MyScopeManager.GetScrambledScopeID(tSend.MyTargetNodeChannel.RealScopeID, true)};{eEngineName.NMIService}@{TheBaseAssets.MyScopeManager.GetScrambledScopeID(tSend.MyTargetNodeChannel.RealScopeID, true)}";   //GRSI: rare //RScope-OK: Users only on primary scope
                var lists = TheThingRegistry.GetBaseEngineNamesWithJSEngine(true);
                tSubs += $";{TheBaseAssets.MyScopeManager.AddScopeID(TheCommonUtils.CListToString(lists, ";"), false)}";
                tSend.Subscribe(tSubs);
            }
            return tLogRes;
        }

        private static bool CreateSenderFromISB(TheRequestData pRequestData,bool IsValidISB, ref cdeSenderType tSenderType, ref TheQueuedSender tSend)
        {
            if (tSend?.MyTargetNodeChannel != null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(299, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"Existing QSender will be used. {tSend.MyTargetNodeChannel} Session:{tSend.MyTargetNodeChannel.MySessionState?.cdeMID} ReqSession:{pRequestData.SessionState?.cdeMID}", eMsgLevel.l3_ImportantMessage));
                if (pRequestData.SessionState != tSend.MyTargetNodeChannel.MySessionState)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(299, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"Matching existing QSenderSession to Request Session. QSSession:{tSend.MyTargetNodeChannel.MySessionState?.cdeMID} ReqSession:{pRequestData.SessionState?.cdeMID}", eMsgLevel.l3_ImportantMessage));
                    TheBaseAssets.MySession.RemoveSession(tSend.MyTargetNodeChannel.MySessionState);
                    tSend.MyTargetNodeChannel.MySessionState = pRequestData.SessionState;
                }
            }
            string tRealScopeID = "";
            if (tSend?.MyTargetNodeChannel == null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2990, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"Create from ISB: creating new Sender {tSend} tNC:{tSend?.MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l4_Message));
                string tServices = null;
                string isbConnect = null;
                if (pRequestData.PostData != null)
                {
                    string tPostDataString = "";
                    if (!string.IsNullOrEmpty(pRequestData.ResponseMimeType) && pRequestData.ResponseMimeType.ToLower().Contains("zip"))
                        tPostDataString = TheCommonUtils.cdeDecompressToString(pRequestData.PostData, pRequestData.PostDataIdx, pRequestData.PostDataLength > 0 ? pRequestData.PostDataLength : pRequestData.PostData.Length);
                    else
                        tPostDataString = TheCommonUtils.CArray2UTF8String(pRequestData.PostData, pRequestData.PostDataIdx, pRequestData.PostDataLength > 0 ? pRequestData.PostDataLength : pRequestData.PostData.Length);
                    if (string.IsNullOrEmpty(tPostDataString))
                    {
                        SendError("1712:Illegal Post - No Body transmitted", pRequestData, eHttpStatusCode.AccessDenied);
                        return IsValidISB;
                    }
                    try
                    {
                        List<TheDeviceMessage> tDev = TheDeviceMessage.DeserializeJSONToObject(tPostDataString);
                        if (tDev == null || tDev.Count == 0 || tDev[0].TOP == null || (!tDev[0].TOP.StartsWith("CDE_CONNECT") && !tDev[0].TOP.StartsWith("CDE_INITWS")))
                        {
                            SendError($"1713:Illegal Post - Invalid or missing TDM {tDev?[0]?.TOP}", pRequestData, eHttpStatusCode.AccessDenied);
                            return IsValidISB;
                        }
                        if (TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage > 2 && pRequestData.SessionState?.CertScopes?.Count > 0 && tSenderType != cdeSenderType.CDE_JAVAJASON)  //Use the ScopeID in the Certificate as the ScopeID for the QSender if Usage>2
                        {
                            tRealScopeID = pRequestData.SessionState?.CertScopes[0];
                            if (TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage > 3 && tRealScopeID != TheBaseAssets.MyScopeManager.GetRealScopeID(tDev[0].SID))
                            {
                                SendError("1714:Illegal connection - scope IDs in Certificate and connection do not match", pRequestData, eHttpStatusCode.AccessDenied);
                                return IsValidISB;
                            }
                        }
                        else
                            tRealScopeID = TheBaseAssets.MyScopeManager.GetRealScopeID(tDev[0].SID);     //GRSI: rare
                        if (!TheBaseAssets.MyServiceHostInfo.IsCloudService && TheBaseAssets.MyScopeManager.IsScopingEnabled && !TheBaseAssets.MyScopeManager.ScopeID.Equals(tRealScopeID))
                        {
                            if (!(TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay && TheBaseAssets.MyServiceHostInfo.AllowForeignScopeIDRouting))
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(2991, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", "ProcessISBRequest: Access from Node with differnet Scope not allowed", eMsgLevel.l2_Warning));
                                return IsValidISB;
                            }
                        }
                        if (pRequestData.DeviceID != TheCommonUtils.CGuid(tDev[0].DID))
                        {
                            if (pRequestData.DeviceID != Guid.Empty)
                                TheBaseAssets.MySYSLOG.WriteToLog(5057, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", $"2-DeviceIDs Different: {tDev[0].DID} != Re:{pRequestData.DeviceID}", eMsgLevel.l2_Warning));
                            pRequestData.DeviceID = TheCommonUtils.CGuid(tDev[0].DID);
                        }
                        string[] tParts = TheCommonUtils.cdeSplit(tDev[0].TOP, ";:;", true, false);
                        if (tParts.Length > 1)
                        {
                            isbConnect = tServices = tParts[1];
                        }
                        else
                        {
                            if (tDev[0].TOP.StartsWith("CDE_CONNECT:;:"))
                                isbConnect = tDev[0].TOP.Substring(14);
                        }
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2992, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", "ProcessISBRequest: Initial Device Message not in proper format - old client? " + pRequestData.ResponseMimeType, eMsgLevel.l1_Error, tPostDataString + " : " + pRequestData.ToString() + ":" + e.ToString()));
                        return IsValidISB;
                    }
                }
                if (tSend == null)
                    tSend = new TheQueuedSender();
                if (pRequestData.DeviceID != Guid.Empty)
                {
                    if (TheBaseAssets.MyServiceHostInfo.NodeBlacklist.Contains(pRequestData.DeviceID))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2300, new TSM("QueuedSender", $"NodeID {pRequestData.DeviceID} is blacklisted and will be rejected", eMsgLevel.l1_Error));
                        TheLoggerFactory.LogEvent(eLoggerCategory.NodeConnect, "NodeID is blacklisted", eMsgLevel.l4_Message, $"{pRequestData.DeviceID}");
                        return IsValidISB;
                    }
                    if (tSenderType == cdeSenderType.NOTSET)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2993, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", "ProcessISBRequest: SenderType is NOT set - defaulting to BACKCHANNEL", eMsgLevel.l2_Warning));
                        tSenderType = cdeSenderType.CDE_BACKCHANNEL;
                    }
                    tSend.eventErrorDuringUpload -= OnCommError;
                    tSend.eventErrorDuringUpload += OnCommError;
                    tSend.eventConnected -= sinkConnected;
                    tSend.eventConnected += sinkConnected;
                    TheBaseAssets.MySYSLOG.WriteToLog(2995, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"Starting new Sender {tSend}", eMsgLevel.l4_Message));
                    var TCI = new TheChannelInfo(pRequestData.DeviceID, tRealScopeID,
                        TheCommonUtils.IsDeviceSenderType(tSenderType) ? tSenderType : cdeSenderType.CDE_BACKCHANNEL,  //IDST-OK: This will record incoming device-nodes with their senderType else "assumes" BackChannel
                        (!TheBaseAssets.MyServiceHostInfo.IsCloudService && tSenderType == cdeSenderType.CDE_SERVICE ? pRequestData.SessionState.InitReferer : null));
                    if (TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage > 0 && pRequestData.SessionState?.CertScopes?.Count > 0)
                        TCI.AddAltScopes(pRequestData.SessionState.CertScopes);
                    if (tSend.StartSender(TCI, isbConnect, true))  //Subscribing here is faster then below with StartEngineWithNewUrl
                    {

                        if (pRequestData.SessionState != null)
                        {
                            if (pRequestData.SessionState.MyDevice != pRequestData.DeviceID)
                            {
                                if (pRequestData.SessionState.MyDevice != Guid.Empty)
                                    TheBaseAssets.MySYSLOG.WriteToLog(5057, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", $"3-DeviceIDs Different: {pRequestData.SessionState.MyDevice} != {pRequestData.DeviceID}", eMsgLevel.l2_Warning));
                                pRequestData.SessionState.MyDevice = pRequestData.DeviceID;
                                TheBaseAssets.MySession.RemoveSessionsByDeviceID(pRequestData.DeviceID, pRequestData.SessionState.cdeMID);
                            }
                            if (tSend.MyTargetNodeChannel.MySessionState == null)
                                tSend.MyTargetNodeChannel.MySessionState = pRequestData.SessionState;
                        }
                    }
                    else
                    {
                        if ((pRequestData.WebSocket as TheWSProcessorBase) != null)
                            (pRequestData.WebSocket as TheWSProcessorBase).OwnerNodeID = pRequestData.DeviceID;
                        tSend.eventErrorDuringUpload -= OnCommError;
                        tSend.eventConnected -= sinkConnected;
                        TheBaseAssets.MySYSLOG.WriteToLog(2994, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"ProcessISBRequest: QueuedSender could not be created for DeviceID:{TheCommonUtils.GetDeviceIDML(pRequestData.DeviceID)} ignored", eMsgLevel.l2_Warning));
                        return IsValidISB;
                        //tSend = null;
                    }
                }
                if (tSend != null)
                {
                    tSend.SetWebSocketProcessor(pRequestData.WebSocket); // CODE REVIEW Markus: This should not be called if a new QueuedSender was created but StartSender failed? CM: if StartSender Failed tSend==null therfore its not getting here; optimize: return IsValidISB above
                    if (!string.IsNullOrEmpty(tServices))
                    {
                        TheQueuedSender newQSender = tSend;
                        TheCommonUtils.cdeRunAsync("Starting Engines from ISB", true, o =>
                        {
                            TheCDEngines.StartEngineWithNewUrl(newQSender.MyTargetNodeChannel, tServices);
                        });
                    }
                }
            }
            if (tSend?.MyTargetNodeChannel != null && (!TheBaseAssets.MyServiceHostInfo.AllowMessagesInConnect || pRequestData.PostDataLength == 0)) // If payload, it will be processed and generate a real response (Enable MSB scenario).
                TheCorePubSub.SetResponseBuffer(pRequestData, tSend.MyTargetNodeChannel, true,SetConnectingBufferStr(tSend?.MyTargetNodeChannel, null));  //FIX for Unscoped Subscriptions coming from Cloud in 4.0110 //RScope-OK: Initial Connect from Primary Scope Only
            return IsValidISB;
        }

        //ATTENTION: This only works with Primary Scope - not with AltScopes.

        internal static byte[] SetConnectingBuffer(TheQueuedSender pSender)
        {
            TheDeviceMessage tDev = new TheDeviceMessage
            {
                SID = pSender?.MyTargetNodeChannel?.RealScopeID != null ? TheBaseAssets.MyScopeManager.GetScrambledScopeID(pSender.MyTargetNodeChannel.RealScopeID, true) : TheBaseAssets.MyScopeManager.GetScrambledScopeID(), //GRSI: rare //RScope-OK: Initial Connect from Primary Scope Only //4.209: Ok no to JavaJson
                NPA = TheBaseAssets.MyScopeManager.GetISBPath(TheBaseAssets.MyServiceHostInfo.RootDir, pSender.MyTargetNodeChannel.SenderType, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType, 1, Guid.Empty, true),
                TOP = pSender?.MyISBlock != null ? $"CDE_CONNECT:;:{TheCommonUtils.SerializeObjectToJSONString<TheISBConnect>(pSender.MyISBlock)}" : SetConnectingBufferStr(pSender.MyTargetNodeChannel, pSender?.MyTargetNodeChannel?.RealScopeID), //RScope-OK: Initial Connect from Primary Scope Only
                DID = pSender.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON || pSender.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CUSTOMISB ? pSender.MyTargetNodeChannel.cdeMID.ToString() : TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString()
            };
            List<TheDeviceMessage> tDevList = new List<TheDeviceMessage> {tDev};
            return TheCommonUtils.CUTF8String2Array(TheCommonUtils.SerializeObjectToJSONString(tDevList));
        }
        internal static string SetConnectingBufferStr(TheChannelInfo pChannel, string pRealScopeID)
        {
            if (pChannel.SenderType == cdeSenderType.CDE_CUSTOMISB)
                return $"CDE_CONNECT;:;ContentService@{TheBaseAssets.MyScopeManager.GetScrambledScopeID(pRealScopeID, true)}"; //4.211: ISB does not automatically get all PLugins from this Node as subscriptions
            else
            {
                string tServices = TheQueuedSenderRegistry.AssembleBackChannelSubscriptions(null, (pChannel != null ? pChannel.RealScopeID : pRealScopeID)); //RScope-OK: Initial Connect from Primary Scope Only
                return $"CDE_CONNECT;:;{tServices}";
            }
        }

        /// <summary>
        /// Disable or Enables the Cloud NMI - Can only be changed with a User Level 128 (Admin)
        /// </summary>
        /// <param name="DoBlockCloudNMI">True blocks the User Manager Replication with the cloud and disables access to the NMI in the cloud</param>
        /// <param name="pUser">ID of the user that requests the change</param>
        /// <returns></returns>
        public static bool SetCloudNMIBlock(bool DoBlockCloudNMI, Guid pUser)
        {
            if (!TheUserManager.HasUserAccess(pUser, 128)) return false;
            TheBaseAssets.MyServiceHostInfo.IsCloudNMIBlocked = DoBlockCloudNMI;
            return true;
        }

        #region Publish Error handling
        internal static void sinkConnected(TheQueuedSender pSend, TheChannelInfo pChannel)
        {
            //TODO JavaScript Connection
        }
        internal static void OnCommError(TheQueuedSender pSend, string pErrorText)
        {
            if (pSend != null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(50511, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"OnPublishError for {pSend?.MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l2_Warning, pErrorText));
                pSend.eventErrorDuringUpload -= OnCommError;
                pSend.eventConnected -= sinkConnected;
            }
        }
        #endregion


    }
}
