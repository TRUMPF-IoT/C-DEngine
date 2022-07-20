// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿#if CDE_USECSWS
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using WebSocketSharp;
using System;
using nsCDEngine.ISM;
using nsCDEngine.Security;
using System.Security.Cryptography.X509Certificates;

namespace nsCDEngine.Communication
{
    internal class TheWSProcessor : TheWSProcessorBase
    {
        internal TheWSProcessor(object wsc)
        {
            webSocketSession = wsc as CDEWSBehavior;
        }

        //SERVER SIDE
        internal CDEWSBehavior webSocketSession;
        WebSocket websocket;
        bool ConnectSuccess;

        internal override bool Connect(TheQueuedSender pSender)
        {
            if (pSender == null) // || pSender.IsConnecting)
                return false;
            if (pSender.IsConnected)
                return true;

            IsClient = true;
            if (MyQSender == null) MyQSender = pSender;
            Uri TargetUri = TheCommonUtils.CUri(pSender.MyTargetNodeChannel.TargetUrl, true);
            string TargetUriPath = TargetUri.AbsolutePath;
            OwnerNodeID = MyQSender.MyTargetNodeChannel.cdeMID;
            Uri tTarget = new Uri(TargetUri, TheBaseAssets.MyScopeManager.GetISBPath(TargetUriPath, TheCommonUtils.GetOriginST(pSender.MyTargetNodeChannel), pSender.MyTargetNodeChannel.SenderType, 1, Guid.Empty, true));
            tTarget = tTarget.SetWSInfo(tTarget.Port, "");

            try
            {
                IsActive = true;
                CloseFired = false;
                ConnectSuccess = true;
                if (websocket != null && !websocket.IsAlive)
                {
                    try
                    {
                        websocket.OnError -= websocket_OnError;
                        websocket.OnOpen -= websocket_Opened;
                        websocket.OnMessage -= websocket_OnMessage;
                        websocket.OnClose -= websocket_OnClose;
                        websocket.Close();
                        TheBaseAssets.MySYSLOG.WriteToLog(8812, new TSM("WSProcessorConnect", string.Format("Websockets were still valid but not alive: closed and will recreate! IsConnecting:{0}", pSender.IsConnecting), eMsgLevel.l4_Message));
                    }
                    catch
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(8812, new TSM("WSProcessorConnect", string.Format("Websockets were still valid but not alive: Error while closing, will recreate. Possible socket leak! IsConnecting:{0}", pSender.IsConnecting), eMsgLevel.l4_Message));
                    }
                    finally
                    {
                        websocket = null;
                    }
                }
                if (websocket == null)
                {
                    websocket = new WebSocket(tTarget.ToString());
                    if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUrl))
                    {
                        try
                        {
                            websocket.SetProxy(TheBaseAssets.MyServiceHostInfo.ProxyUrl, TheBaseAssets.MyServiceHostInfo.ProxyUID, TheBaseAssets.MyServiceHostInfo.ProxyPWD);
                            TheBaseAssets.MySYSLOG.WriteToLog(8812, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("WSProcessorCSWS", $"Websockets proxy set: {TheBaseAssets.MyServiceHostInfo.ProxyUrl} UID: {!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUID)} PWD: {!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyPWD)}", eMsgLevel.l4_Message));
                        }
                        catch (Exception e)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("WSProcessor", "Error setting Proxy credentials:", eMsgLevel.l1_Error, e.ToString()));
                        }
                    }
                    websocket.Log.Level = LogLevel.Fatal;
                    websocket.Log.Output = sinkLog;
                    websocket.SslConfiguration.ServerCertificateValidationCallback += TheCommCore.ValidateServerCertificate;

                    var myTargetNodeChannel = MyQSender?.MyTargetNodeChannel;
                    if (!string.IsNullOrEmpty(myTargetNodeChannel?.ClientCertificateThumb))
                    {
                        try
                        {
                            X509Certificate2Collection certs = TheCertificates.GetClientCertificatesByThumbprint(myTargetNodeChannel?.ClientCertificateThumb);
                            if (certs?.Count < 1)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("WSProcessor", $"Error setting Client Certificate - Requested Cert with Thumbprint {myTargetNodeChannel?.ClientCertificateThumb} not found", eMsgLevel.l1_Error));
                                return false;
                            }
                            websocket.SslConfiguration.ClientCertificates = certs;
                            websocket.SslConfiguration.ClientCertificateSelectionCallback = wsSharpClientCertSelector;
                        }
                        catch (Exception certex)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("WSProcessor", "Error setting Client Certificate", eMsgLevel.l1_Error, certex.ToString()));
                            return false;
                        }
                    }

                    websocket.OnError += websocket_OnError;
                    websocket.OnOpen += websocket_Opened;
                    websocket.OnMessage += websocket_OnMessage;
                    websocket.OnClose += websocket_OnClose;
                    websocket.Connect();
                }
                else
                    TheBaseAssets.MySYSLOG.WriteToLog(8812, new TSM("WSProcessorConnect", string.Format("Websockets are still valid and will be recycled! IsConnecting:{0}",pSender.IsConnecting),eMsgLevel.l4_Message));
            }
            catch (Exception ex)
            {
                Shutdown(true, "1650:Connect Failed: " + ex);
            }
            return ConnectSuccess;
        }

        private X509Certificate wsSharpClientCertSelector(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            if (localCertificates.Count == 1)
            {
                return localCertificates[0];
            }
            return null;
        }

        void sinkLog(LogData tData, string pString)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(8812, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("WSProcessor", pString, (eMsgLevel)tData.Level, tData.Message));
        }
        void websocket_OnClose(object sender, CloseEventArgs e)
        {
            ConnectSuccess = false;
            Shutdown(true,$"1651:WebSockets were closed ({e.Reason} {e.Code})");
        }
        void websocket_OnError(object sender, ErrorEventArgs e)
        {
            ConnectSuccess = false;
            websocket = null;
            eventClosed?.Invoke("1652:Connection was Closed by Remote Server - error:" + (e != null ? e.Message : "no error given"));
        }


        private void websocket_Opened(object sender, EventArgs e)
        {
            //MyQSender.IsConnected = true;
            eventConnected?.Invoke(this, true,null);
            byte[] tSendBuffer = TheCommCore.SetConnectingBuffer(MyQSender);
            PostToSocket(null, tSendBuffer, false, true);
        }


        void websocket_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.IsText)
                ProcessIncomingData(e.Data, null,0);
            else if (e.IsBinary)
                ProcessIncomingData(null, e.RawData,0);
        }






        internal override void PostToSocket(TheDeviceMessage pMsg, byte[] pPostBuffer, bool pSendAsBinary, bool IsInitialConnect)
        {
            TheDiagnostics.SetThreadName("WSPostToSocketCSWS:" + (MyQSender.MyTargetNodeChannel?.ToString() ?? "DEAD"));

            if (MyQSender != null && !MyQSender.IsConnected && !IsInitialConnect)
            {
                Shutdown(true, "1653:QSenderCSWS not connected but Posting in illegal state");
                return;
            }

            if (!ProcessingAllowed)         //NEW:V3BETA2: New Waiting Algorythm
                WaitUntilProcessingAllowed();

            if (!IsActive || !TheBaseAssets.MasterSwitch)
                return;

            if (websocket == null && webSocketSession==null)
            {
                eventClosed?.Invoke("1654:WebSockets are down");
                return;
            }
            ProcessingAllowed = false;

            try
            {
                if (pPostBuffer != null)
                {
                    TheCDEKPIs.IncrementKPI(eKPINames.QKBSent, pPostBuffer.Length);
                    if (pSendAsBinary)
                    {
                        if (webSocketSession != null)
                            webSocketSession.SendB(pPostBuffer);
                        else
                        {
                            websocket?.Send(pPostBuffer);
                        }
                    }
                    else
                    {
                        if (webSocketSession != null)
                            webSocketSession.SendB(TheCommonUtils.CArray2UTF8String(pPostBuffer));
                        else
                        {
                            websocket?.Send(TheCommonUtils.CArray2UTF8String(pPostBuffer));
                        }
                    }
                }
                else
                {
                    string toSend = TheCommonUtils.SerializeObjectToJSONString(pMsg);
                    if (pSendAsBinary)
                    {
                        byte[] toSendb = TheCommonUtils.CUTF8String2Array(toSend);
                        TheCDEKPIs.IncrementKPI(eKPINames.QKBSent, toSendb.Length);
                        if (webSocketSession != null)
                            webSocketSession.SendB(toSendb);
                        else
                        {
                            websocket?.Send(toSendb);
                        }
                    }
                    else
                    {
                        if (webSocketSession != null)
                            webSocketSession.SendB(toSend);
                        else
                        {
                            websocket?.Send(toSend);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Shutdown(true, "1655:DoPostToSocketCSWS had a fault: "+e);
            }

            if (mre != null)
                ProcessingAllowed = true;
        }





        public override void Shutdown(bool IsAsync, string pReason)
        {
            base.Shutdown(IsAsync, pReason);
            try
            {
                TheBaseAssets.MySYSLOG.WriteToLog(43611, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheWSServer", $"WS Server for {OwnerNodeID} CloseFired:{CloseFired} Reason: {pReason}", eMsgLevel.l3_ImportantMessage));
                if (websocket != null)
                {
                    websocket.CloseAsync(1000, pReason);
                    websocket = null;
                    if (!CloseFired && IsAsync && eventClosed != null)
                    {
                        CloseFired = true;
                        eventClosed(pReason);
                    }
                }
                if (webSocketSession != null)
                {
                    webSocketSession = null;
                    if (!CloseFired && IsAsync && eventClosed != null)
                    {
                        CloseFired = true;
                        eventClosed(pReason);
                    }
                }
                try
                {
                    Dispose();
                }
                catch
                {
                    //ignored
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4343, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSServerCSWS", "WebSocketServer-Shutdown Error", eMsgLevel.l1_Error, e.ToString()));
            }

        }

    }
}
#endif