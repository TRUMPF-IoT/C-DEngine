// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace cdeASPNetMiddleware
{
    public static class cdeAspNetRestExtensions
    {
        public static IApplicationBuilder UseCDEAspNetRestHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<cdeASPNetRestHandler>();
        }
    }

    public class cdeASPNetRestHandler
    {
        private readonly RequestDelegate _next;
        public static string ExpiredText;
        private List<string> IgnoredPaths = null; //New in 6.131.0 (January 2026): required for private paths used by other middleware
        public cdeASPNetRestHandler(RequestDelegate next)
        {
            _next = next;
            var tp=TheBaseAssets.MySettings?.GetSetting("CDEIgnorePath");
            if (!string.IsNullOrEmpty(tp))
            {
                IgnoredPaths = new List<string>(tp.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Method == "CONNECT" || IgnoredPaths?.Any(s => context.Request?.Path.Value.ToLower().StartsWith(s.ToLower()) == true) == true)
                await _next.Invoke(context);
            else
                await ProcessRESTRequest(context);
        }

        public async Task ProcessRESTRequest(HttpContext pContext)
        {
            var Request = pContext.Request;
            var Response = pContext.Response;
            if (ExpiredText != null)
            {
                await Response.WriteAsync(ExpiredText);
                return;
            }

            if (TheBaseAssets.CryptoLoadMessage != null)
            {
                await Response.WriteAsync($"...Cloud security initializing failed: {TheBaseAssets.CryptoLoadMessage}");
                return;
            }

            if (TheBaseAssets.MyServiceHostInfo == null || !TheBaseAssets.MyServiceHostInfo.AllSystemsReady)  //&& Request.Url.ToString().EndsWith("cdestatus.aspx", StringComparison.OrdinalIgnoreCase))
            {
                await Response.WriteAsync($"<html><head><meta http-equiv=\"refresh\" content=\"10; url={TheBaseAssets.MyServiceHostInfo.MyStationURL}{Request.Path}{Request.QueryString}\"></head><body>...Relay initializing, please wait</body></html>");
                return;
            }

            if (Request.Path.ToString().EndsWith("cdeRestart.aspx") && cdeASPNetCommon.IsTokenValid(Request))
            {
                TheBaseAssets.MyApplication?.Shutdown("Restart Requested", true);
                return;
            }
            if (Request.Path.ToString().EndsWith("ashx", StringComparison.CurrentCultureIgnoreCase))
                return;

            if (Request.Scheme == "http" && TheBaseAssets.MyServiceHostInfo.MyStationPort == 443)
            {
                await Response.WriteAsync($"<html><head><meta http-equiv=\"refresh\" content=\"0; url={TheBaseAssets.MyServiceHostInfo.MyStationURL}{Request.Path}{Request.QueryString}\"></head></html>");
                return;
            }

            TheRequestData tReq = cdeASPNetCommon.CreateRequest(pContext);
            if (tReq == null)
                return;

            using (MemoryStream ms = new())
            {
                await Request.Body.CopyToAsync(ms);
                tReq.PostData = ms.ToArray();
            }
            tReq.PostDataLength = tReq.PostData.Length;

            if (TheCommCore.MyHttpService != null && TheCommCore.MyHttpService.cdeProcessPost(tReq) &&
                tReq.StatusCode != 0 && tReq.StatusCode != (int)eHttpStatusCode.NotFound)
            {
                Response.StatusCode = tReq.StatusCode;
                cdeASPNetCommon.AddCookiesToHeader(Response, tReq);
                Response.Headers.Append("Cache-Control", tReq.AllowCaching ? "max-age=60, public" : "no-cache");
                if (tReq.StatusCode > 300 && tReq.StatusCode < 400 && tReq.Header != null)
                    Response.Headers.Append("Location", tReq.Header.cdeSafeGetValue("Location"));
                if (tReq.ResponseBuffer != null)
                {
                    Response.Headers.Append("cdeDeviceID", TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString());
                    Response.ContentType = tReq.ResponseMimeType;
                    await Response.Body.WriteAsync(tReq.ResponseBuffer);
                    return;
                }
            }
            await _next.Invoke(pContext);
        }
    }

}
