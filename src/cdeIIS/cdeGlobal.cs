// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.Activation;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Web;
using System.Web.Hosting;

namespace CDEngineCloud
{
    public class TheCDECloudGlobal : HttpApplication
    {
        static bool WasHereBefore;
        static readonly object lockStart = new object();
        static string MySite = "";
        static string ExpiredText = null;

        public static bool StartTheSite(TheBaseApplication pApplication, HttpContext pContext)
        {
            if (TheCommonUtils.cdeIsLocked(lockStart)) return false;
            lock (lockStart)
            {
                if (!WasHereBefore && !TheBaseAssets.MasterSwitch)
                {
                    WasHereBefore = true;
                }
                else
                    return false;
            }
            Dictionary<string, string> ArgList = new Dictionary<string, string>();
            MySite = TheCommonUtils.CStr(System.Configuration.ConfigurationManager.AppSettings["MyStationURL"]);  //MSU-OK
            if (string.IsNullOrEmpty(MySite) && pContext != null)
            {
                MySite = pContext.Request.Url.Scheme + "://" + pContext.Request.Url.Host; //.Authority;
                if (pContext.Request.Url.Port > 0 && pContext.Request.Url.Port != TheBaseAssets.MyServiceHostInfo.MyStationPort)
                {
                    TheBaseAssets.MyServiceHostInfo.MyStationPort = (ushort)pContext.Request.Url.Port;
                    if ((pContext.Request.Url.Scheme.Equals("https") && pContext.Request.Url.Port != 443) ||
                        (pContext.Request.Url.Scheme.Equals("http") && pContext.Request.Url.Port != 80))
                        MySite += ":" + pContext.Request.Url.Port;
                }
            }
            ArgList["MyStationURL"] = MySite;

            if (!pApplication.StartBaseApplication(null, ArgList))
                return false;

            if (!TheBaseAssets.MyActivationManager.CheckLicense(new Guid("{5737240C-AA66-417C-9B66-3919E18F9F4A}"), "", null, 1))
            {
                ExpiredText="...Cloud has Expired - contact C-Labs for updated version";
                return false;
            }
            return true;
        }

        private string sectok=null;
        private bool IsTokenValid(HttpRequest pRequestData)
        {
            if (sectok == "")
                return false; //Default no access
            if (sectok == null)
            {
                sectok = TheBaseAssets.MySettings.GetSetting("StatusToken");
            }
            if (!string.IsNullOrEmpty(sectok) && pRequestData.QueryString[sectok.ToUpper()]!=null)
                return true;
            return false;
        }

        public void CloudApplicationBeginRequest(object sender, EventArgs e, TheBaseApplication pApplication)
        {
            StartTheSite(pApplication, Context);
            if (TheBaseAssets.CryptoLoadMessage != null)
            {
                Response.Write($"...Cloud security initializing failed: {TheBaseAssets.CryptoLoadMessage}");
                Response.End();
                return;
            }

            if (TheBaseAssets.MyServiceHostInfo == null || !TheBaseAssets.MyServiceHostInfo.AllSystemsReady)  //&& Request.Url.ToString().EndsWith("cdestatus.aspx", StringComparison.OrdinalIgnoreCase))
            {
                Response.Write("...Cloud initializing, please wait");
                Response.End();
                return;
            }

            if (Request.Url.ToString().EndsWith("cdeRestart.aspx") && IsTokenValid(Request))
            {
                HttpRuntime.UnloadAppDomain();
                return;
            }
            if (Request.Url.ToString().EndsWith("ashx", StringComparison.CurrentCultureIgnoreCase))
                return;

            if (ExpiredText != null)
            {
                Response.Write(ExpiredText);
                Response.End();
                return;
            }

            if (Context.Request.Url.Scheme == "http" && TheBaseAssets.MyServiceHostInfo.MyStationPort == 443)
            {
                Response.Write(
                    $"<html><head><meta http-equiv=\"refresh\" content=\"0; url={TheBaseAssets.MyServiceHostInfo.MyStationURL}{Context.Request.Url.PathAndQuery}\"></head></html>");
                Response.End();
                return;
            }


            TheRequestData tReq = new TheRequestData
            {
                RequestUri = Request.Url,
                HttpMethod= HttpContext.Current.Request.HttpMethod,
                UserAgent = HttpContext.Current.Request.UserAgent,
                ServerTags = TheCommonUtils.cdeNameValueToDirectory(HttpContext.Current.Request.ServerVariables),
                Header = TheCommonUtils.cdeNameValueToDirectory(HttpContext.Current.Request.Headers),
                ResponseMimeType = Request.ContentType,
                //ClientCert = HttpContext.Current.Request.ClientCertificate,
                ClientCert = HttpContext.Current.Request.ClientCertificate?.Count>0 ? new System.Security.Cryptography.X509Certificates.X509Certificate2(HttpContext.Current.Request.ClientCertificate?.Certificate):null,
                //Verified that this works with no private key. Since C-DEngineCloud only works on Windows in IIS and MUST run as Administrator there is no linux check required here
            };

            if (TheCommCore.MyHttpService != null && TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage > 1) //If CDE requires a certificate, terminate all incoming requests before any processing
            {
                var err = TheCommCore.MyHttpService.ValidateCertificateRoot(tReq);
                if (TheBaseAssets.MyServiceHostInfo.DisableNMI && !string.IsNullOrEmpty(err))
                {
                    Response.StatusCode = (int)eHttpStatusCode.AccessDenied;
                    CompleteRequest();
                    return;
                }
            }

            using (MemoryStream ms = new MemoryStream())
            {
                Request.InputStream.CopyTo(ms);
                tReq.PostData = ms.ToArray();
            }
            tReq.PostDataLength = tReq.PostData.Length;
            if (Request.Browser != null)
            {
                tReq.BrowserType = Request.Browser.Browser + " " + Request.Browser.Version;
                tReq.BrowserPlatform = Request.Browser.Platform;
                tReq.BrowserScreenWidth = Request.Browser.ScreenPixelsWidth;
            }

            //TheHttpService.ProcessGlobalAsax(Request, Response);
            if (TheCommCore.MyHttpService != null && TheCommCore.MyHttpService.cdeProcessPost(tReq))
            {
                if (tReq.StatusCode != 0)
                {
                    Response.StatusCode = tReq.StatusCode;
                    AddCookiesToHeader(tReq);
                    Response.AddHeader("Cache-Control", tReq.AllowCaching ? "max-age=60, public" : "no-cache");
                    if (tReq.StatusCode > 300 && tReq.StatusCode < 400 && tReq.Header != null)
                        Response.AddHeader("Location", tReq.Header.cdeSafeGetValue("Location"));
                    if (tReq.ResponseBuffer != null)
                    {
                        Response.AddHeader("cdeDeviceID", TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString());
                        Response.ContentType = tReq.ResponseMimeType;
                        Response.BinaryWrite(tReq.ResponseBuffer);
                        CompleteRequest();
                    }
                }
            }
        }

        private void AddCookiesToHeader(TheRequestData tReq)
        {
            if (tReq?.SessionState?.StateCookies?.Count > 0 && tReq?.SessionState?.HasExpired==false)
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
                        var tCo = new HttpCookie(nam, val)
                        {
                            Expires = DateTime.MinValue,
                            Domain = dom,
                            Path = pat,
                            //Secure=true, //TODO: Required for Chrome Future! needs .NET 4.72
                            //SameSite=SameSiteMode.None
                            HttpOnly = true
                        };
                        Response.Cookies.Add(tCo);
                    }
                    catch
                    {
                        //Ignored
                    }
                }
            }
        }

        public static void ShutDown()
        {
            if (!TheBaseAssets.MasterSwitch) return;
            if (TheBaseAssets.MyApplication != null)
                TheBaseAssets.MyApplication.Shutdown(false);
            WasHereBefore = false;
            if (TheCommonUtils.CBool(System.Configuration.ConfigurationManager.AppSettings["AutoStartSite"]))
            {
                var client = new WebClient();
                var url = MySite + "/cdeclean.aspx";
                client.DownloadString(url);
            }
        }

        static EventWaitHandle newAppStartedGlobalEvent;

        //public static object App { get; private set; }

        public static void InitRecycleSync()
        {
            //Trace.WriteLine(DateTime.Now + " " + Process.GetCurrentProcess().Id + " Application_Start");
            //System.Diagnostics.Trace.Flush();

            try
            {
                newAppStartedGlobalEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "clabs.cdeW3WPAppStarted", out bool createdNew);
                if (!createdNew)
                {
                    // We found the event, so some other app instance is already running on this machine
                    // Signal the event, so that the other app can shutdown
                    newAppStartedGlobalEvent.Set();
                    newAppStartedGlobalEvent.Reset();
                    //Trace.WriteLine(DateTime.Now + " " + Process.GetCurrentProcess().Id + " Found previous app: signaling");
                    //System.Diagnostics.Trace.Flush();
                }

                ThreadPool.RegisterWaitForSingleObject(newAppStartedGlobalEvent,
                    (o, timedOut) =>
                    {
                    //Trace.WriteLine(DateTime.Now + " " + Process.GetCurrentProcess().Id + " Signaled New App: Shutting Down");
                    //System.Diagnostics.Trace.Flush();

                    // Another app has started on this machine and signaled the event: let's shut down
                    // Do our own shutdown first in case IIS terminates the process too aggressively
                    ShutDown();
                        HostingEnvironment.InitiateShutdown();

                    // Event handle gets cleaned up for us by CLR/OS at process shutdown
                },
                null, Timeout.Infinite, true);
            }
            catch (Exception)
            {
                //ingore
            }
        }
    }
}
