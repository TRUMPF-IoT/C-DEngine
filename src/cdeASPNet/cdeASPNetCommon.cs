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
        internal static TheRequestData CreateRequest(HttpContext pContext, bool BypassCertCheck=false)
        {
            var tCon = pContext.Connection;
            TheRequestData tReq = new ()
            {
                RequestUri = TheCommonUtils.CUri(UriHelper.GetDisplayUrl(pContext.Request), false),
                HttpMethod = pContext.Request.Method,
                UserAgent = pContext.Request.Headers[HeaderNames.UserAgent],
                ResponseMimeType = pContext.Request.ContentType,
                ClientCert = tCon.ClientCertificate,
                RemoteAddress=tCon.RemoteIpAddress.ToString()
            };
            if (!BypassCertCheck && TheCommCore.MyHttpService != null && TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage > 1) //If CDE requires a certificate, terminate all incoming requests before any processing
            {
                var err = TheCommCore.MyHttpService.ValidateCertificateRoot(tReq);
                if (TheBaseAssets.MyServiceHostInfo.DisableNMI && !string.IsNullOrEmpty(err))
                {
                    var terr = $"WebSocket Access with wrong Client Certificate root - access is denied: {err}";
                    TheBaseAssets.MySYSLOG.WriteToLog(423, new TSM("TheCloudWSockets", terr, eMsgLevel.l1_Error));
                    pContext.Response.StatusCode = (int)eHttpStatusCode.AccessDenied;
                    return null;
                }
            }
            tReq.Header = new cdeConcurrentDictionary<string, string>();
            foreach (var t in pContext.Request.Headers)
            {
                tReq.Header[t.Key] = t.Value;
            }
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
                        var tcooki = new CookieOptions { Path = pat, Domain = dom, Expires = DateTimeOffset.MinValue, HttpOnly = true, Secure=true };
                        if (!tReq.RequestUri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase) && !tReq.RequestUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                            tcooki.Secure = false; //NOSONAR we do have to support local non-tls clients
                        Response.Cookies.Append(nam, val, tcooki);
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
            sectok ??= TheBaseAssets.MySettings.GetSetting("StatusToken");
            if (!string.IsNullOrEmpty(sectok) && pRequestData.Query.ContainsKey(sectok))
                return true;
            return false;
        }
    }
}
