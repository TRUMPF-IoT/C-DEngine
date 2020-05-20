// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿#if CDE_USEWSS8
using System;
using System.Net;
using System.Threading.Tasks;

using nsCDEngine.ISM;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;

namespace nsCDEngine.Communication
{
    internal class TheWSServer8
    {
        private HttpListener mHttpListener;
        private string MyHttpUrl;
        public bool IsActive;

        internal bool Startup()
        {
            if (TheBaseAssets.MyServiceHostInfo.MyStationWSPort == 0 || TheBaseAssets.MyServiceHostInfo.MyStationWSPort == TheBaseAssets.MyServiceHostInfo.MyStationPort) return false;
            mHttpListener = new HttpListener();
            Uri tUri = new Uri(TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false));
            MyHttpUrl = tUri.Scheme + "://*"; // +tUri.Host;
            MyHttpUrl += ":" + TheBaseAssets.MyServiceHostInfo.MyStationWSPort;
            MyHttpUrl += "/";
            try
            {
                mHttpListener.Prefixes.Add(MyHttpUrl);
                mHttpListener.Start();
                TheBaseAssets.MySYSLOG.WriteToLog(4370, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheWSServer", "New WebSockets8 HttpListener started ", eMsgLevel.l4_Message, $"Port: {TheBaseAssets.MyServiceHostInfo.MyStationWSPort}"));
                IsActive = true;
                TheCommonUtils.cdeRunAsync("WebSocketServer - Processing Thread", true, async o =>
                    {
                        while (IsActive && TheBaseAssets.MasterSwitch)
                        {
                            try
                            {
                                if (mHttpListener != null)
                                {
                                    HttpListenerContext context = await mHttpListener.GetContextAsync();
                                    if (!context.Request.IsWebSocketRequest)
                                    {
                                        context.Response.StatusCode = 400;
                                        context.Response.Close();
                                        continue;
                                    }
                                    TheCommonUtils.cdeRunAsync("WSWait4AcceptThread", false, async oo =>
                                    {
                                        try
                                        {
                                            await WaitForWSAccept(context);
                                        }
                                        catch (Exception e)
                                        {
                                            TheBaseAssets.MySYSLOG.WriteToLog(4371, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSServer", "Error During WSAccept", eMsgLevel.l1_Error, e.ToString()));
                                            IsActive = false;
                                        }
                                    });
                                }
                            }
                            catch (Exception e)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(4372, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSServer", "WebSocketServer:Failed - Will Stop!", eMsgLevel.l1_Error, e.ToString()));
                                IsActive = false;
                            }
                        }
                        ShutDown();
                    });

            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4373, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSServer", "Error During Startup", eMsgLevel.l1_Error, $"Port: {TheBaseAssets.MyServiceHostInfo?.MyStationWSPort} {e.ToString()}"));
                IsActive = false;
            }
            return IsActive;
        }

        public static async Task WaitForWSAccept(HttpListenerContext pContext)
        {
            TheDiagnostics.SetThreadName("WSWait4Accept");
            var wsc = await pContext.AcceptWebSocketAsync(null);
            if (wsc != null)
            {
                TheRequestData tRequestData = new TheRequestData
                {
                    RequestUri = pContext.Request.Url,
                    UserAgent = pContext.Request.UserAgent,
                    HttpMethod = pContext.Request.HttpMethod,
                    Header = TheCommonUtils.cdeNameValueToDirectory(pContext.Request.Headers),
                    ResponseMimeType = pContext.Request.ContentType,
                    ServerTags = null,
                    ClientCert = pContext.Request.GetClientCertificate()
                };
                if (TheCommCore.MyHttpService != null && TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage > 1) //If CDE requires a certificate, terminate all incoming requests before any processing
                {
                    var err = TheCommCore.MyHttpService.ValidateCertificateRoot(tRequestData);
                    if (!string.IsNullOrEmpty(err))
                    {
                        pContext.Response.StatusCode = (int)eHttpStatusCode.AccessDenied;
                        pContext.Response.OutputStream.Close();
                        return;
                    }
                }
                TheWSProcessor8 tProcessor = new TheWSProcessor8(wsc.WebSocket);
                tProcessor.SetRequest(tRequestData);
                await tProcessor.ProcessWS();
            }
        }

        internal void ShutDown()
        {
            IsActive = false;
            if (mHttpListener != null)
            {
                try
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4345, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheWSServer", "Stopping Http Listener", eMsgLevel.l6_Debug));
                    mHttpListener.Stop();
                    mHttpListener.Prefixes.Remove(MyHttpUrl);
                    mHttpListener = null;
                }
                catch (NullReferenceException)
                {
                    //ignored
                }
            }
        }
    }
}
#endif