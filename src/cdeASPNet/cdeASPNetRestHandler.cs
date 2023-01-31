// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using System;
using System.IO;
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
        public cdeASPNetRestHandler(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await ProcessRESTRequest(context);
        }

        public async Task ProcessRESTRequest(HttpContext pContext)
        {
            var Response = pContext.Response;
            var Request = pContext.Request;
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
                await Response.WriteAsync($"<html><head><meta http-equiv=\"refresh\" content=\"10; url={TheBaseAssets.MyServiceHostInfo.MyStationURL}{Request.Path}{Request.QueryString}\"></head><body>...Cloud initializing, please wait</body></html>");
                return;
            }

            if (Request.Path.ToString().EndsWith("cdeRestart.aspx") && cdeASPNetCommon.IsTokenValid(Request))
            {
                TheBaseAssets.MyApplication?.Shutdown(true);
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

            using (MemoryStream ms = new ())
            {
                await Request.Body.CopyToAsync(ms);
                tReq.PostData = ms.ToArray();
            }
            tReq.PostDataLength = tReq.PostData.Length;

            if (TheCommCore.MyHttpService != null && TheCommCore.MyHttpService.cdeProcessPost(tReq) && tReq.StatusCode != 0)
            {
                Response.StatusCode = tReq.StatusCode;
                cdeASPNetCommon.AddCookiesToHeader(Response, tReq);
                Response.Headers.Add("Cache-Control", tReq.AllowCaching ? "max-age=60, public" : "no-cache");
                if (tReq.StatusCode > 300 && tReq.StatusCode < 400 && tReq.Header != null)
                    Response.Headers.Add("Location", tReq.Header.cdeSafeGetValue("Location"));
                if (tReq.ResponseBuffer != null)
                {
                    Response.Headers.Add("cdeDeviceID", TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString());
                    Response.ContentType = tReq.ResponseMimeType;
                    await Response.Body.WriteAsync(tReq.ResponseBuffer);
                    return;
                }
            }
            await _next.Invoke(pContext);
        }
    }

}
