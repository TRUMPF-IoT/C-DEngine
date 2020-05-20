// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Text;
using System.Net;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System.IO;

//LOG RANGE: 250 -259

//NETCF Cookies: http://piao8163.blog.163.com/blog/static/969724782011101091723145/

namespace nsCDEngine.Communication
{
    public partial class TheREST
    {
        /// <summary>
        /// Issues a REST GET call to a given URI
        /// </summary>
        /// <param name="pUri">Target URI for the REST call</param>
        /// <param name="tTimeOut">Timeout of the call.If no return was received by this time (in seconds) the call will fail</param>
        /// <param name="tCallback">Callback when the REST call is finished</param>
        /// <param name="pCookie">Cookie to be handed back with the callback</param>
        public static void GetRESTAsync(Uri pUri, int tTimeOut, Action<TheRequestData> tCallback, object pCookie)
        {
            GetRESTAsync(pUri, tTimeOut, null, null, null, tCallback, pCookie, null);
        }
        /// <summary>
        /// Issues a REST GET call to a given URI
        /// </summary>
        /// <param name="sURL">Target URI for the REST call</param>
        /// <param name="LocalCallback">Callback when the REST call is finished</param>
        public static void GetRESTAsync(Uri sURL, Action<TheRequestData> LocalCallback)
        {
            GetRESTAsync(sURL, 0, LocalCallback);
        }
        /// <summary>
        /// Issues a REST GET call to a given URI
        /// </summary>
        /// <param name="sURL">Target URI for the REST call</param>
        /// <param name="tTimeOut">Timeout of the call.If no return was received by this time (in seconds) the call will fail</param>
        /// <param name="LocalCallback">Callback when the REST call is finished</param>
        public static void GetRESTAsync(Uri sURL, int tTimeOut, Action<TheRequestData> LocalCallback)
        {
            GetRESTAsync(sURL, tTimeOut, null, null, null, LocalCallback, null, null);
        }
        /// <summary>
        /// Issues a REST GET call to a given URI
        /// </summary>
        /// <param name="pUri">Target URI for the REST call</param>
        /// <param name="tTimeOut">Timeout of the call.If no return was received by this time (in seconds) the call will fail</param>
        /// <param name="UID">Username for calls with authentication</param>
        /// <param name="PWD">Password for calls with authentication</param>
        /// <param name="Domain">Domain if required for cookies</param>
        /// <param name="tCallback">Callback when the REST call is finished</param>
        /// <param name="pCookie">Cookie to be handed back with the callback</param>
        /// <param name="pCookies">Cookies to send to the REST server</param>
        public static void GetRESTAsync(Uri pUri, int tTimeOut, string UID, string PWD, string Domain, Action<TheRequestData> tCallback, object pCookie, cdeConcurrentDictionary<string, string> pCookies)
        {
            TheRequestData pData = new TheRequestData();
            if (TheBaseAssets.MyServiceHostInfo != null)
                pData.RemoteAddress = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false);
            pData.RequestUri = pUri;
            pData.TimeOut = tTimeOut;
            pData.UID = UID;
            pData.PWD = PWD;
            pData.DOM = Domain;
            pData.CookieObject = pCookie;
            pData.RequestCookies = pCookies;
            GetRESTAsync(pData, tCallback, null);
        }

        /// <summary>
        /// Amount of POST currently issued
        /// </summary>
        public int IsPosting = 0;

        /// <summary>
        /// REST POST Call to a server address with the URI
        /// </summary>
        /// <param name="pUri">Target URI of the server</param>
        /// <param name="pCallback">Callback with Response when POST returns</param>
        /// <param name="stmPostBuffer">Post-buffer stream to be posted to the server</param>
        /// <param name="pContentType">Content type of the POST Call</param>
        /// <param name="pCookie">Cookie to be handed back with the response</param>
        /// <param name="pErrorCallback">Callback in case POST fails</param>
        public void PostRESTAsync(Uri pUri, Action<TheRequestData> pCallback, Stream stmPostBuffer, string pContentType, object pCookie, Action<TheRequestData> pErrorCallback)
        {
            PostRESTAsync(pUri, pCallback, stmPostBuffer, pContentType, Guid.Empty, pCookie, pErrorCallback, null);
        }
        /// <summary>
        /// REST POST Call to a server address with the URI
        /// </summary>
        /// <param name="pUri">Target URI of the server</param>
        /// <param name="pCallback">Callback with Response when POST returns</param>
        /// <param name="pPostBuffer">Post-buffer to be posted to the server</param>
        /// <param name="pCookie">Cookie to be handed back with the response</param>
        /// <param name="pErrorCallback">Callback in case POST fails</param>
        public void PostRESTAsync(Uri pUri, Action<TheRequestData> pCallback, string pPostBuffer, object pCookie, Action<TheRequestData> pErrorCallback)
        {
            PostRESTAsync(pUri, pCallback, Encoding.UTF8.GetBytes(pPostBuffer), Guid.Empty, pCookie, pErrorCallback);
        }
        /// <summary>
        /// REST POST Call to a server address with the URI
        /// </summary>
        /// <param name="pUri">Target URI of the server</param>
        /// <param name="pCallback">Callback with Response when POST returns</param>
        /// <param name="pPostBuffer">Post-buffer to be posted to the server</param>
        /// <param name="pContentType">Content type of the POST Call</param>
        /// <param name="pCookie">Cookie to be handed back with the response</param>
        /// <param name="pErrorCallback">Callback in case POST fails</param>
        public void PostRESTAsync(Uri pUri, Action<TheRequestData> pCallback, string pPostBuffer, string pContentType, object pCookie, Action<TheRequestData> pErrorCallback)
        {
            PostRESTAsync(pUri, pCallback, Encoding.UTF8.GetBytes(pPostBuffer), pContentType, Guid.Empty, pCookie, pErrorCallback, null);
        }
        /// <summary>
        /// REST POST Call to a server address with the URI
        /// </summary>
        /// <param name="pUri">Target URI of the server</param>
        /// <param name="pCallback">Callback with Response when POST returns</param>
        /// <param name="pPostBuffer">Post-buffer string to be posted to the server</param>
        /// <param name="pRequestor">Requestor ID</param>
        /// <param name="pCookie">Cookie to be handed back with the response</param>
        /// <param name="pErrorCallback">Callback in case POST fails</param>
        public void PostRESTAsync(Uri pUri, Action<TheRequestData> pCallback, string pPostBuffer, Guid pRequestor, object pCookie, Action<TheRequestData> pErrorCallback)
        {
            PostRESTAsync(pUri, pCallback, Encoding.UTF8.GetBytes(pPostBuffer), null, pRequestor, pCookie, pErrorCallback, null);
        }
        /// <summary>
        /// REST POST Call to a server address with the URI
        /// </summary>
        /// <param name="pUri">Target URI of the server</param>
        /// <param name="pCallback">Callback with Response when POST returns</param>
        /// <param name="pPostBuffer">Post-buffer to be posted to the server</param>
        /// <param name="pRequestor">Requestor ID</param>
        /// <param name="pCookie">Cookie to be handed back with the response</param>
        /// <param name="pErrorCallback">Callback in case POST fails</param>
        public void PostRESTAsync(Uri pUri, Action<TheRequestData> pCallback, byte[] pPostBuffer, Guid pRequestor, object pCookie, Action<TheRequestData> pErrorCallback)
        {
            PostRESTAsync(pUri, pCallback, pPostBuffer, null, pRequestor, pCookie, pErrorCallback, null);
        }

        /// <summary>
        /// REST POST Call to a server address with the URI
        /// </summary>
        /// <param name="pUri">Target URI of the server</param>
        /// <param name="pCallback">Callback with Response when POST returns</param>
        /// <param name="pPostBuffer">Post-buffer string to be posted to the server</param>
        /// <param name="pContentType">Content type of the POST Call</param>
        /// <param name="pRequestor">Requestor ID</param>
        /// <param name="pCookie">Cookie to be handed back with the response</param>
        /// <param name="pErrorCallback">Callback in case POST fails</param>
        /// <param name="pCookies">Cookies to send with the POST</param>
        public void PostRESTAsync(Uri pUri, Action<TheRequestData> pCallback, string pPostBuffer, string pContentType, Guid pRequestor, object pCookie, Action<TheRequestData> pErrorCallback, cdeConcurrentDictionary<string, string> pCookies)
        {
            PostRESTAsync(pUri, pCallback, Encoding.UTF8.GetBytes(pPostBuffer), pContentType, pRequestor, pCookie, pErrorCallback, pCookies);
        }

        /// <summary>
        /// REST POST Call to a server address with the URI
        /// </summary>
        /// <param name="pUri">Target URI of the server</param>
        /// <param name="pCallback">Callback with Response when POST returns</param>
        /// <param name="pPostBuffer">Post-buffer to be posted to the server</param>
        /// <param name="pContentType">Content type of the POST Call</param>
        /// <param name="pRequestor">Requestor ID</param>
        /// <param name="pCookie">Cookie to be handed back with the response</param>
        /// <param name="pErrorCallback">Callback in case POST fails</param>
        /// <param name="pCookies">Cookies to send with the POST</param>
        public void PostRESTAsync(Uri pUri, Action<TheRequestData> pCallback, byte[] pPostBuffer, string pContentType, Guid pRequestor, object pCookie, Action<TheRequestData> pErrorCallback, cdeConcurrentDictionary<string, string> pCookies)
        {
            TheRequestData pData = new TheRequestData
            {
                RemoteAddress = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false),
                RequestUri = pUri,
                CookieObject = pCookie,
                RequestCookies = pCookies,
                PostData = pPostBuffer,
                ResponseMimeType = pContentType,
                DeviceID = pRequestor,
                HttpMethod = "POST"
            };
            PostRESTAsync(pData, pCallback, pErrorCallback);
        }

        /// <summary>
        /// REST POST Call to a server address with the URI
        /// </summary>
        /// <param name="pUri">Target URI of the server</param>
        /// <param name="pCallback">Callback with Response when POST returns</param>
        /// <param name="stmPostBuffer">Stream of a buffer to be posted to the server</param>
        /// <param name="pContentType">Content type of the POST Call</param>
        /// <param name="pRequestor">Requestor ID</param>
        /// <param name="pCookie">Cookie to be handed back with the response</param>
        /// <param name="pErrorCallback">Callback in case POST fails</param>
        /// <param name="pCookies">Cookies to send with the POST</param>
        public void PostRESTAsync(Uri pUri, Action<TheRequestData> pCallback, Stream stmPostBuffer, string pContentType, Guid pRequestor, object pCookie, Action<TheRequestData> pErrorCallback, cdeConcurrentDictionary<string, string> pCookies)
        {
            TheRequestData pData = new TheRequestData
            {
                RemoteAddress = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false),
                RequestUri = pUri,
                CookieObject = pCookie,
                RequestCookies = pCookies,
                PostDataStream = stmPostBuffer,
                ResponseMimeType = pContentType,
                DeviceID = pRequestor,
                HttpMethod = "POST"
            };
            PostRESTAsync(pData, pCallback, pErrorCallback);
        }

        private static void SetCookies(TheInternalRequestState myRequestState)
        {
            myRequestState.MyRequestData.TempCookies = new CookieContainer();
            if (myRequestState.MyRequestData.RequestCookies != null && myRequestState.MyRequestData.RequestCookies.Count > 0)
            {
                foreach (string nam in myRequestState.MyRequestData.RequestCookies.Keys)
                {
                    try
                    {
                        string tVal = myRequestState.MyRequestData.RequestCookies[nam];
                        if (string.IsNullOrEmpty(tVal)) continue;
                        string[] co = tVal.Split(';');
                        string val = co[0];
                        string pat = "/"; //if (co.Length > 1) pat = co[1];
                        //string dom = ""; if (co.Length > 2) dom = co[2];
                        string dom = myRequestState.MyRequestData.RequestUri.Host.Trim(); //if (string.IsNullOrEmpty(dom))
                        //TheSystemMessageLog.ToCo(string.Format("GetREST: Cookie: ({0}) ({1}) ({2}) ({3})", nam,val,pat,dom));
                        myRequestState.MyRequestData.TempCookies.Add(myRequestState.MyRequestData.RequestUri, new Cookie(nam.Trim(), val.Trim(), pat, dom));
                    }
                    catch (Exception e)
                    {
                        if (TheBaseAssets.MySYSLOG != null)
                            TheBaseAssets.MySYSLOG.WriteToLog(254, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", "SetCookies Exception: " + myRequestState.MyRequestData.RequestUri, eMsgLevel.l1_Error, e.Message));
                    }
                }
                //TheSystemMessageLog.ToCo(string.Format("GetREST: CookieCont: ({0})", myRequestState.MyRequestData.TempCookies.GetCookieHeader(myRequestState.MyRequestData.RequestUri)));
            }
        }
        private static bool ProcessRedirect(TheRequestData pRequestData, string tTarget)
        {
            if (tTarget.StartsWith(pRequestData.RequestUri.Scheme))  //TODO:SSL to non SSL Scheme Mapping
            {
#if !CDE_NET35
                Uri tTargetUri = new Uri(tTarget);
                Uri tUrl = pRequestData.RequestUri;
#else
                Uri tTargetUri = TheCommonUtils.CUri(tTarget, false);
                Uri tUrl = TheCommonUtils.CUri(pRequestData.RequestUri, false);
#endif

                if (!string.IsNullOrEmpty(pRequestData.RequestUriString))
                {
#if !CDE_NET35
                    tUrl = new Uri(pRequestData.RequestUriString);
#else
                    tUrl = TheCommonUtils.CUri(pRequestData.RequestUriString, false);
#endif
                }
                Uri tCloudUri = new Uri(tUrl.Scheme + "://" + tUrl.Host + ":" + tUrl.Port + tTargetUri.LocalPath + tTargetUri.Query);
                if (pRequestData.Header == null)
                    pRequestData.Header = new cdeConcurrentDictionary<string, string>();
                pRequestData.Header.TryAdd("Location", tCloudUri.ToString());
                pRequestData.NewLocation = tCloudUri.ToString();

                string tTgtHost = tTargetUri.Scheme + "://" + tTargetUri.Host;
                if ((tTargetUri.Scheme == "http" && tTargetUri.Port != 80) || (tTargetUri.Scheme == "https" && tTargetUri.Port != 443))
                    tTgtHost += ":" + tTargetUri.Port;

                string tReplUri = tUrl.Scheme + "://" + tUrl.Host;
                if ((tUrl.Scheme == "http" && tUrl.Port != 80) || (tUrl.Scheme == "https" && tUrl.Port != 443))
                    tReplUri += ":" + tUrl.Port;

                if (pRequestData.ResponseBuffer == null)
                {
                    pRequestData.ResponseBufferStr = "<html><head><style>";
                    pRequestData.ResponseBufferStr += "div.cdeLiveTile {font-size: 24px; font-family: Arial,sans-serif; font-weight: bold; width:145px; height:145px; text-align:center;  float: left; cursor: pointer; margin:5px; color: #2B8EFB; background-color: #EBDEC7; overflow: hidden; background-image: url('../Images/glasoverlay.png'); } ";
                    pRequestData.ResponseBufferStr += "table.MyFullTableCentered { border: 0px; border-collapse: collapse; padding: 0px; width: 100%; height:100%; margin-left: auto; margin-right: auto; text-align: center; } ";
                    pRequestData.ResponseBufferStr += "div.cdeTileText { margin: 5%; cursor: pointer; }";
                    pRequestData.ResponseBufferStr += "</style></head><body><div class='cdeLiveTile' onclick='location.href=\"" + tTargetUri + "\"'><table class='MyFullTableCentered'><tbody><tr><td><div class='cdeTileText'>Redirected!<br>Touch to go to App</div></td></tr></tbody></table></div></body></html>";
                    //pRequestData.ResponseBufferStr = "<html><a href='" + tTargetUri + "'>Click to see result</a><p>" + pRequestData.RequestUri + "<br>" + pRequestData.RequestUriString + "</html>";
                    pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(pRequestData.ResponseBufferStr.Replace(tTgtHost, tReplUri));
                    pRequestData.ResponseMimeType = "text/html";
                    //pRequestData.StatusCode = 200;
                }
                pRequestData.RequestUri = tTargetUri;
                if (!string.IsNullOrEmpty(tTargetUri.AbsolutePath) && tTargetUri.AbsolutePath.Length > 1 && tTargetUri.AbsolutePath.Substring(1).Contains("/"))
                    return false;
            }
            else
            {
#if !CDE_NET35
                pRequestData.RequestUri = new Uri(pRequestData.RequestUri.Scheme + "://" + pRequestData.RequestUri.Host + ":" + pRequestData.RequestUri.Port + tTarget);
#else
                var requestUri =  TheCommonUtils.CUri(pRequestData.RequestUri, false);
                pRequestData.RequestUri = new Uri(requestUri.Scheme + "://" + requestUri.Host + ":" + requestUri.Port + tTarget);
#endif
                if (tTarget.Length > 1 && tTarget.Substring(1).Contains("/"))
                    return false;
            }
            return true;
        }
    }
}
