// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Net.Http.Headers;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using System;

namespace cdeASPNetMiddleware
{
    internal static class cdeASPNetCommon
    {
        internal static TheRequestData CreateRequest(HttpContext pContext)
        {
            var tCon = pContext.Connection;
            TheRequestData tReq = new TheRequestData
            {
                RequestUri = TheCommonUtils.CUri(UriHelper.GetDisplayUrl(pContext.Request), false),
                HttpMethod = pContext.Request.Method,
                UserAgent = pContext.Request.Headers[HeaderNames.UserAgent],
                ResponseMimeType = pContext.Request.ContentType,
                ClientCert = tCon.ClientCertificate,
            };
            if (TheCommCore.MyHttpService != null && TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage > 1) //If CDE requires a certificate, terminate all incoming requests before any processing
            {
                var err = TheCommCore.MyHttpService.ValidateCertificateRoot(tReq);
                if (TheBaseAssets.MyServiceHostInfo.DisableNMI && !string.IsNullOrEmpty(err))
                {
                    var terr = $"WebSocket Access with worng Client Certificate root - access is denied: {err}";
                    TheBaseAssets.MySYSLOG.WriteToLog(423, new TSM("TheCloudWSockets", terr, eMsgLevel.l1_Error));
                    pContext.Response.StatusCode = (int)eHttpStatusCode.AccessDenied;
                    return null;
                }
            }
            tReq.Header = new cdeConcurrentDictionary<string, string>();
            foreach (var t in pContext.Request.Headers)
            {
                tReq.Header[t.Value] = t.Value;
            }
            //if (Request.Browser != null)
            //{
            //    tReq.BrowserType = Request.Browser.Browser + " " + Request.Browser.Version;
            //    tReq.BrowserPlatform = Request.Browser.Platform;
            //    tReq.BrowserScreenWidth = Request.Browser.ScreenPixelsWidth;
            //}
            return tReq;
        }

        internal static void AddCookiesToHeader(HttpResponse Response, TheRequestData tReq)
        {
            if (tReq?.SessionState?.StateCookies?.Count > 0 && tReq?.SessionState?.HasExpired == false)
            {
                if (!tReq.AllowStatePush)
                {
                    return;
                }

                foreach (string nam in tReq.SessionState.StateCookies.Keys)
                {
                    try
                    {
                        string tVal = tReq.SessionState.StateCookies[nam];
                        if (string.IsNullOrEmpty(tVal)) continue;
                        string[] co = tVal.Split(';');
                        string val = co[0];
                        string pat = "/"; if (co.Length > 1) pat = co[1];
                        string dom = null; if (co.Length > 2) dom = co[2];
                        Response.Cookies.Append(nam, val, new CookieOptions { Path = pat, Domain = dom, Expires = DateTime.MinValue, HttpOnly = true });
                    }
                    catch
                    {
                        //Ignored
                    }
                }
            }
        }

        private static string sectok = null;
        internal static bool IsTokenValid(HttpRequest pRequestData)
        {
            if (sectok == "")
                return false; //Default no access
            if (sectok == null)
            {
                sectok = TheBaseAssets.MySettings.GetSetting("StatusToken");
            }
            if (!string.IsNullOrEmpty(sectok) && pRequestData.Query.ContainsKey(sectok))
                return true;
            return false;
        }
    }
}
