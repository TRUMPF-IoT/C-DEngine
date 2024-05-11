// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace cdeASPNetMiddleware
{
    public static class cdeAspNetWsExtensions
    {
        public static IApplicationBuilder UseCDEAspNetWsHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<cdeAspNetWsHandler>();
        }
    }

    public class cdeAspNetWsHandler
    {
        private readonly RequestDelegate _next;

        public cdeAspNetWsHandler(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                if (await ProcessWSRequest(context, webSocket, context.Request.Path.ToString().StartsWith("/ISB")) == null)
                    return;
            }
            await _next(context);
        }

        /// <summary>
        /// Processes the WebSockets request.
        /// </summary>
        /// <param name="context">The http context.</param>
        /// <param name="ws">The incoming WebSockets.</param>
        public static async Task<string> ProcessWSRequest(HttpContext context, WebSocket ws, bool IsISB)
        {
            try
            {
                TheRequestData tRequestData = cdeASPNetCommon.CreateRequest(context);
                if (tRequestData == null)
                    return null;
                tRequestData.ResponseMimeType = "cde/ws";

                if (!IsISB)
                {
                    return await TheQueuedSenderRegistry.ProcessWSRequest(ws, tRequestData);
                }
                else
                {
                    await TheQueuedSenderRegistry.ProcessCloudRequest(ws, tRequestData);
                    return null;
                }
            }
            catch (Exception ex)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(423, new TSM("TheCloudWSockets", "Processing Error 500", ex.ToString()));
                context.Response.StatusCode = 500;
            }
            return null;
        }
    }
}
