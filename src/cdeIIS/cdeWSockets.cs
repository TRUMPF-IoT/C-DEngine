// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.WebSockets;

namespace CDEngineCloud
{
    public class TheCloudWSockets : IHttpHandler
    {
        #region Dont change anything below - this will be simplified before RTM
        public void ProcessRequest(HttpContext context)
        {
            try
            {
                var tRequestUri = HttpContext.Current.Request.Url;
                if (tRequestUri.PathAndQuery.ToLower().StartsWith("/wstest"))
                {
                    ProcessRequest(new HttpContextWrapper(context));
                    return;
                }

                context.AcceptWebSocketRequest(async wsContext =>
                {
                    TheRequestData tRequestData = new TheRequestData
                    {
                        RequestUri = HttpContext.Current.Request.Url,
                        HttpMethod=HttpContext.Current.Request.HttpMethod,
                        UserAgent = HttpContext.Current.Request.UserAgent,
                        ServerTags = TheCommonUtils.cdeNameValueToDirectory(HttpContext.Current.Request.ServerVariables),
                        Header = TheCommonUtils.cdeNameValueToDirectory(HttpContext.Current.Request.Headers),
                        ResponseMimeType = "cde/ws",
                        ClientCert = HttpContext.Current.Request.ClientCertificate?.Count > 0 ? new System.Security.Cryptography.X509Certificates.X509Certificate2(HttpContext.Current.Request.ClientCertificate?.Certificate) : null
                        //Verified that this works with no private key. Since C-DEngineCloud only works on Windows in IIS and MUST run as Administrator there is no linux check required here
                    };
                    if (TheCommCore.MyHttpService != null && TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage > 1) //If CDE requires a certificate, terminate all incoming requests before any processing
                    {
                        var err = TheCommCore.MyHttpService.ValidateCertificateRoot(tRequestData);
                        if (TheBaseAssets.MyServiceHostInfo.DisableNMI && !string.IsNullOrEmpty(err))
                        {
                            var terr = $"WebSocket Access with worng Client Certificate root - access is denied: {err}";
                            TheBaseAssets.MySYSLOG.WriteToLog(423, new TSM("TheCloudWSockets", terr, eMsgLevel.l1_Error));
                            return;
                        }
                    }
                    await TheQueuedSenderRegistry.ProcessCloudRequest(wsContext.WebSocket, tRequestData);
                });
            }
            catch (Exception ex)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(423, new TSM("TheCloudWSockets", "Processing Error 500", ex.ToString()));
                context.Response.StatusCode = 500;
                context.Response.StatusDescription = ex.Message;
                context.Response.End();
            }
        }

        public bool IsReusable => false;
        private static string AccessCode = null;

        public void ProcessRequest(HttpContextBase context)
        {
            if (context.IsWebSocketRequest)
            {
                if (AccessCode == null)
                    AccessCode = TheBaseAssets.MySettings.GetSetting("WSTestCode");
                if (!string.IsNullOrEmpty(AccessCode))
                {
                    var RequestUri = HttpContext.Current.Request.Url;
                    if (!RequestUri.PathAndQuery.Contains(AccessCode))
                        return;
                }
                context.AcceptWebSocketRequest(ProcessSocketRequest);
            }
        }

        private async Task ProcessSocketRequest(AspNetWebSocketContext context)
        {
            var socket = context.WebSocket;
            TheCDEKPIs.IncWSTestClients();
            TheCommonUtils.cdeRunAsync("DeadCheck", true, (o) =>
            {
                TheCommonUtils.TaskDelayOneEye(10 * 1000, 100).ContinueWith((t) =>
                {
                    socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure,"timeout", CancellationToken.None);
                });
            });
            // maintain socket
            while (true)
            {
                var buffer = new ArraySegment<byte>(new byte[100]);
                // async wait for a change in the socket
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    TheCDEKPIs.DecWSTestClients();
                    break;
                }
            }
        }
        #endregion
    }
}
