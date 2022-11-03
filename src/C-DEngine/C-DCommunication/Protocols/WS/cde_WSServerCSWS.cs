// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿#if CDE_USECSWS
using System;

using WebSocketSharp;
using WebSocketSharp.Server;

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.ISM;

namespace nsCDEngine.Communication
{
    internal class TheWSServer
    {
        internal bool IsActive;
        private WebSocketServer appServer;

        internal void Startup()
        {
            TheDiagnostics.SetThreadName("WSStartup");
            if (TheBaseAssets.MyServiceHostInfo.MyStationWSPort == 0) return;
            try
            {
                if (TheBaseAssets.MyServiceHostInfo.MyStationPort == TheBaseAssets.MyServiceHostInfo.MyStationWSPort)
                    TheBaseAssets.MyServiceHostInfo.MyStationWSPort =(ushort)(TheBaseAssets.MyServiceHostInfo.MyStationPort + 1);
                appServer = new WebSocketServer(TheBaseAssets.MyServiceHostInfo.MyStationWSPort) { ReuseAddress = true }; 
                appServer.AddWebSocketService<CDEWSBehavior>(TheBaseAssets.MyServiceHostInfo.RootDir + "/ISB");
                appServer.Log.Level = LogLevel.Fatal;
                appServer.Log.Output = sinkLog;
                appServer.Start();
                IsActive = true;
                TheBaseAssets.MySYSLOG.WriteToLog(4378, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheWSServer", "New WebSocket-sharp Server started ", eMsgLevel.l4_Message, $"Port: {appServer.Port}"));
            }
            catch (Exception ee)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4378, new TSM("TheWSServer", "WebSocket-sharp could not start!", eMsgLevel.l1_Error, $"Port: {TheBaseAssets.MyServiceHostInfo.MyStationWSPort} {ee}"));
            }
        }

        internal void ShutDown()
        {
            IsActive = false;
            if (appServer != null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4378, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheWSServer", "Stopping WebSocket-sharp Server!", eMsgLevel.l6_Debug));
                appServer.Stop();
                appServer = null;
            }
        }

        void sinkLog(LogData tData, string pString)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(4379, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("WSServer", pString, (eMsgLevel)tData.Level, tData.Message));
        }
    }

    internal class CDEWSBehavior : WebSocketBehavior
    {
        internal TheWSProcessor mProcessor;

        public CDEWSBehavior()
        {
            IgnoreExtensions = true;
        }

        internal void SendB(byte [] pBytes)
        {
            Send(pBytes);
        }
        internal void SendB(string pBytes)
        {
            Send(pBytes);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (e.IsBinary)
                mProcessor.ProcessIncomingData(null, e.RawData,0);
            else
                mProcessor.ProcessIncomingData(e.Data, null,0);
        }

        protected override void OnOpen()
        {
            TheBaseAssets.MySYSLOG.WriteToLog(43710, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCSWSServer", string.Format("New Session: {0} URI:{1}", Context.ServerEndPoint,Context.RequestUri)));
            try
            {
                mProcessor = new TheWSProcessor(this);

                TheRequestData tRequestData = new ()
                {
                    RequestUri = Context.RequestUri,
                    HttpMethod = "GET",
                    Header = new cdeConcurrentDictionary<string, string>()
                };
                foreach (string tKey in Context.Headers.AllKeys)
                {
                    switch (tKey.ToLower())
                    {
                        case "user-agent": 
                            tRequestData.UserAgent = Context.Headers[tKey];
                            break;
                        case "content-type":
                            tRequestData.ResponseMimeType = Context.Headers[tKey];
                            break;
                    }
                    tRequestData.Header.TryAdd(tKey, Context.Headers[tKey]);
                }
                tRequestData.ServerTags = null;
                mProcessor.SetRequest(tRequestData);
                if (tRequestData?.StatusCode!=200)
                    mProcessor?.Shutdown(true, $"{tRequestData?.StatusCode}:OnOpen resulted in not-ok StatusCode {tRequestData?.StatusCode} - Closing Connection");
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(43711, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheSWSServer", "Error during new Session-Connect", eMsgLevel.l1_Error, e.ToString()));
            }
        }

        protected override void OnError(ErrorEventArgs e)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(43712, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheSWSServer", "Session-Closed"));
            mProcessor?.Shutdown(true,$"1656:CWS Fired an error: {e?.Message}");
        }

        protected override void OnClose(CloseEventArgs e)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(43713, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheSWSServer", string.Format("Session-Closed URI:{0}", Context.RequestUri)));
            mProcessor?.Shutdown(true,"1657:CWS were Closed");
        }
    }
}
#endif