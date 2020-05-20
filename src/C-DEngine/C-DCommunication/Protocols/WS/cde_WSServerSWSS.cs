// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿#if CDE_USESWSS
using nsCDEngine.BaseClasses;
using nsCDEngine.CDMiscs;
using nsCDEngine.Communication.HttpService;
using nsCDEngine.ViewModels;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Config;
using SuperWebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace nsCDEngine.Communication
{
    public class TheWSServer
    {
        private string MyHttpUrl = null;
        internal bool IsActive = false;

        private WebSocketServer appServer = null;

        internal void Startup()
        {
            TheDiagnostics.SetThreadName("WSStartup");
            if (TheBaseAssets.MyServiceHostInfo.MyStationWSPort == 0) return;
            Uri tUri = new Uri(TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false));
            MyHttpUrl = tUri.Scheme + "://*"; // +tUri.Host;
            MyHttpUrl += ":" + TheBaseAssets.MyServiceHostInfo.MyStationWSPort;
            MyHttpUrl += "/";
            appServer = new WebSocketServer();

            //Setup the appServer
            if (!appServer.Setup(new ServerConfig() { Port = TheBaseAssets.MyServiceHostInfo.MyStationWSPort, MaxRequestLength = 400000 })) //Setup with listening port
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4343, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSServer", "Error During Startup", eMsgLevel.l1_Error));
                return;
            }

            appServer.NewMessageReceived += new SessionHandler<WebSocketSession, string>(appServer_NewMessageReceived);
            appServer.NewDataReceived += appServer_NewDataReceived;
            appServer.NewSessionConnected += appServer_NewSessionConnected;
            appServer.SessionClosed += appServer_SessionClosed;
            //Try to start the appServer
            if (!appServer.Start())
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4343, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSServer", "Failed to start Super-Web-Socket Server", eMsgLevel.l1_Error));
                IsActive = false;
                return;
            }
            IsActive = true;
            TheBaseAssets.MySYSLOG.WriteToLog(4343, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheWSServer", "New Super-Web-Socket Server started ", eMsgLevel.l4_Message));
        }

        internal void ShutDown()
        {
            IsActive = false;
            if (appServer != null)
            {
                appServer.Stop();
                appServer = null;
            }
        }


        void appServer_SessionClosed(WebSocketSession session, CloseReason value)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(4343, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheSWSServer", string.Format("Session-Closed: {0} ID:{1}", session.RemoteEndPoint.ToString(), session.SessionID)));
            TheWSProcessor tPross = null;
            if (mSessionList.TryRemove(session.SessionID, out tPross))
            {
                if (tPross!=null)
                    tPross.Shutdown(true);
            }
        }

        void appServer_NewSessionConnected(WebSocketSession session)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(4343, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheSWSServer", string.Format("New Session: {0} ID:{1}", session.RemoteEndPoint.ToString(), session.SessionID)));
            try
            {
                TheWSProcessor tProcessor = new TheWSProcessor(session);
                mSessionList.TryAdd(session.SessionID, tProcessor);

                TheRequestData tRequestData = new TheRequestData();
                tRequestData.RequestUri = new Uri(session.UriScheme + "://" + session.Host + session.Path); // pContext.Request.Url;
                tRequestData.Header = new cdeConcurrentDictionary<string, string>();
                foreach (var tKey in session.Items)
                {
                    switch (tKey.Key.ToString().ToLower())
                    {
                        case "user-agent": //tRequestData.UserAgent = pContext.Request.UserAgent;
                            tRequestData.UserAgent = tKey.Value.ToString();
                            break;
                        case "content-type": //tRequestData.ResponseMimeType = pContext.Request.ContentType;
                            tRequestData.ResponseMimeType = tKey.Value.ToString();
                            break;
                    }
                    tRequestData.Header.TryAdd(tKey.Key.ToString(), tKey.Value.ToString());
                }
                tRequestData.ServerTags = null;
                tProcessor.SetRequest(tRequestData);
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4343, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheSWSServer", "Error during new Session-Connect", eMsgLevel.l1_Error,e.ToString()));
            }
        }

        private cdeConcurrentDictionary<string, TheWSProcessor> mSessionList = new cdeConcurrentDictionary<string, TheWSProcessor>();

        void appServer_NewDataReceived(WebSocketSession session, byte[] value)
        {
            if (mSessionList.Count == 0) return;
            TheWSProcessor tPross = null;
            if (mSessionList.TryGetValue(session.SessionID, out tPross) && tPross!=null)
            {
                tPross.ProcessIncomingData(null, value);
            }
        }

        void appServer_NewMessageReceived(WebSocketSession session, string message)
        {
            if (mSessionList.Count == 0) return;
            TheWSProcessor tPross = null;
            if (mSessionList.TryGetValue(session.SessionID, out tPross) && tPross != null)
            {
                tPross.ProcessIncomingData(message, null);
            }
        }
    }
}
#endif