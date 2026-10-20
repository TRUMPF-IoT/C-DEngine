// SPDX-FileCopyrightText: Copyright (c) 2009-2026 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
//LOG RANGE: 250 -259

namespace nsCDEngine.Communication
{
    /// <summary>
    /// C-DEngine REST Methods
    /// </summary>
    public partial class TheREST
    {
        private sealed class TheInternalRequestState : IDisposable
        {
            public TheRequestData MyRequestData;
            public HttpRequestMessage request;
            public HttpResponseMessage response;
            public Action<TheRequestData> ResultCallback;
            public Action<TheRequestData> ErrorCallback;
            internal CancellationTokenSource cts;
            internal TheREST mRest;

            public TheInternalRequestState()
            {
                MyRequestData = new TheRequestData();
            }

            public void Dispose()
            {
                if (cts != null)
                {
                    try
                    {
                        cts.Dispose();
                    }
                    catch
                    {
                        //intentionally blank
                    }
                    cts = null;
                }
                mRest = null;
                if (request != null)
                {
                    try
                    {
                        request.Dispose();
                    }
                    catch
                    {
                        //intentionally blank
                    }
                }
                request = null;
                if (response != null)
                {
                    try
                    {
                        response.Dispose();
                    }
                    catch
                    {
                        //intentionally blank
                    }
                }
                response = null;
                ResultCallback = null;
                ErrorCallback = null;
                MyRequestData = null;
            }
        }

        /// <summary>
        /// HttpContent that streams the post data of a TheRequestData into the request body
        /// </summary>
        private sealed class TheRequestDataContent : HttpContent
        {
            private readonly TheRequestData mData;

            public TheRequestDataContent(TheRequestData pData)
            {
                mData = pData;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                mData.WritePostDataToStream(stream);
                return Task.CompletedTask;
            }

            protected override bool TryComputeLength(out long length)
            {
                length = mData.GetContentLength();
                return true;
            }
        }

        #region HttpClient management

        private static readonly ConcurrentDictionary<string, HttpClient> MyHttpClients = new();

        /// <summary>
        /// Returns a cached HttpClient whose handler matches the handler-level settings (proxy, credentials, client certificate) of the request
        /// </summary>
        private static HttpClient GetHttpClient(TheRequestData pData)
        {
            string tKey = BuildClientKey(pData);
            return MyHttpClients.GetOrAdd(tKey, _ => CreateHttpClient(pData));
        }

        private static string BuildClientKey(TheRequestData pData)
        {
            string tCert = "";
            if (pData.ClientCert is X509Certificate crt)
                tCert = crt.GetCertHashString();
            else if (pData.ClientCert is X509CertificateCollection x509)
                tCert = string.Join(",", x509.Cast<X509Certificate>().Select(c => c.GetCertHashString()));
            string tCred = "";
            if (!string.IsNullOrEmpty(pData.UID) && !string.IsNullOrEmpty(pData.PWD))
                tCred = $"{pData.UID}|{pData.PWD}|{pData.DOM}";
            return $"{TheBaseAssets.MyServiceHostInfo?.ProxyUrl}|{TheBaseAssets.MyServiceHostInfo?.ProxyUID}|{tCred}|{tCert}";
        }

        private static HttpClient CreateHttpClient(TheRequestData pData)
        {
            HttpClientHandler tHandler = new()
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.None,
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => TheCommCore.ValidateServerCertificate(msg, cert, chain, errors)
            };
            if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo?.ProxyUrl))
            {
                NetworkCredential tNet = null;
                try
                {
                    if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUID))
                    {
                        tNet = new NetworkCredential(TheBaseAssets.MyServiceHostInfo.ProxyUID, TheBaseAssets.MyServiceHostInfo.ProxyPWD);
                    }
                    tHandler.Proxy = new WebProxy(TheBaseAssets.MyServiceHostInfo.ProxyUrl, true, null, tNet);
                    tHandler.UseProxy = true;
                    TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", $"Web Proxy set: {TheBaseAssets.MyServiceHostInfo.ProxyUrl} UID: {!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUID)} PWD: {!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyPWD)}", eMsgLevel.l4_Message));
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheREST", "Error setting Proxy credentials:", eMsgLevel.l1_Error, e.ToString()));
                }
            }
            if (pData.ClientCert != null)
            {
                if (pData.ClientCert is X509Certificate crt)
                {
                    tHandler.ClientCertificates.Add(crt);
                }
                else if (pData.ClientCert is X509CertificateCollection x509)
                {
                    tHandler.ClientCertificates.AddRange(x509);
                }
            }
            if (!string.IsNullOrEmpty(pData.UID) && !string.IsNullOrEmpty(pData.PWD))
            {
                tHandler.Credentials = new NetworkCredential(pData.UID, pData.PWD, pData.DOM ?? "");
            }
            return new HttpClient(tHandler)
            {
                Timeout = Timeout.InfiniteTimeSpan // Timeouts are handled per request via CancellationTokenSource
            };
        }

        private static void AddHeader(HttpRequestMessage pRequest, string pKey, string pValue)
        {
            if (!pRequest.Headers.TryAddWithoutValidation(pKey, pValue) && pRequest.Content != null)
                pRequest.Content.Headers.TryAddWithoutValidation(pKey, pValue);
        }

        private static void SetUserAgent(HttpRequestMessage pRequest, TheRequestData pData)
        {
            pRequest.Headers.Remove("User-Agent");
            if (!string.IsNullOrEmpty(pData.UserAgent))
                pRequest.Headers.TryAddWithoutValidation("User-Agent", pData.UserAgent);
            else
                pRequest.Headers.TryAddWithoutValidation("User-Agent", !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.CustomUA) ? TheBaseAssets.MyServiceHostInfo.CustomUA : "C-DEngine " + TheBaseAssets.CurrentVersionInfo);
        }

        private static void ApplyCookieHeader(TheInternalRequestState pState)
        {
            if (pState.MyRequestData.TempCookies == null) return;
            string tCookieHeader = pState.MyRequestData.TempCookies.GetCookieHeader(pState.MyRequestData.RequestUri);
            if (!string.IsNullOrEmpty(tCookieHeader))
            {
                pState.request.Headers.Remove("Cookie");
                pState.request.Headers.TryAddWithoutValidation("Cookie", tCookieHeader);
            }
        }

        private static void ApplyTimeout(TheInternalRequestState pState)
        {
            if (pState.MyRequestData.TimeOut <= 0 && TheBaseAssets.MyServiceHostInfo != null)
                pState.MyRequestData.TimeOut = TheBaseAssets.MyServiceHostInfo.TO.HeartBeatRate * 1000;
            pState.cts = new CancellationTokenSource(pState.MyRequestData.TimeOut > 0 ? pState.MyRequestData.TimeOut : Timeout.Infinite);
        }

        /// <summary>
        /// Sends the prepared request asynchronously and dispatches the result to the callbacks of the request state
        /// </summary>
        private static void SendRequest(TheInternalRequestState pState, HttpClient pClient)
        {
            ApplyTimeout(pState);
            string tErrPrefix = pState.mRest != null ? "1406:" : "";
            string tErrMime = pState.mRest != null ? "text/plain" : "text/html";
            TheCommonUtils.cdeRunAsync("TheREST.SendRequest", true, async (o) =>
            {
                string turl = "Unknown";
                try
                {
                    turl = pState.MyRequestData?.RequestUri?.ToString() ?? turl;
                    pState.response = await pClient.SendAsync(pState.request, HttpCompletionOption.ResponseHeadersRead, pState.cts.Token).ConfigureAwait(false);
                    byte[] tBody = pState.response.Content != null ? await pState.response.Content.ReadAsByteArrayAsync(pState.cts.Token).ConfigureAwait(false) : null;
                    if ((int)pState.response.StatusCode >= 400)
                        ProcessErrorResponse(pState, tBody, tErrPrefix, tErrMime);
                    else
                        ProcessResponse(pState, tBody);
                }
                catch (OperationCanceledException) when (pState.cts?.IsCancellationRequested == true)
                {
                    try
                    {
                        if (pState.MyRequestData != null)
                        {
                            pState.MyRequestData.ErrorDescription = "1410:Timeout Error";
                            pState.MyRequestData.ResponseMimeType = tErrMime; //OK
                            pState.MyRequestData.StatusCode = 500;
                            pState.ErrorCallback?.Invoke(pState.MyRequestData);
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(257, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", "HttpClient request timed out and was aborted to ORG:" + turl, eMsgLevel.l2_Warning));
                    }
                    catch (Exception e2)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(257, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", "TimeoutCallback: timed out and was aborted to ORG:" + turl, eMsgLevel.l1_Error, e2.ToString()));
                    }
                }
                catch (Exception e)
                {
                    try
                    {
                        if (pState.MyRequestData != null && pState.ErrorCallback != null)
                        {
                            pState.MyRequestData.ErrorDescription = $"{tErrPrefix}{e}";
                            pState.MyRequestData.ResponseMimeType = tErrMime; //OK
                            pState.MyRequestData.StatusCode = 500;
                            pState.ErrorCallback(pState.MyRequestData);
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(252, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", "RespCallback Exception - ORG:" + turl, eMsgLevel.l2_Warning, e.ToString()));
                    }
                    catch (Exception e2)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(252, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", "RespCallback Exception - ORG:" + turl, eMsgLevel.l2_Warning, e2.ToString()));
                    }
                }
                finally
                {
                    CleanUp(pState);
                }
            });
        }

        #endregion

        #region Complex RESTGet

        /// <summary>
        /// Requests data from a REST service asynchronously
        /// </summary>
        /// <param name="pData">data defining the request - most important is the RequestUri</param>
        /// <param name="tCallback">Callback with the sucessfully returned REST Data (Response)</param>
        /// <param name="pErrorCallback">Error callback in case something went wrong during the call</param>
        public static void GetRESTAsync(TheRequestData pData, Action<TheRequestData> tCallback, Action<TheRequestData> pErrorCallback)
        {
            if (pData == null)
            {
                pErrorCallback?.Invoke(null);
                return;
            }
            if (!TheBaseAssets.MasterSwitch)
            {
                pData.ErrorDescription = "C-DEngine is shutting down";
                pData.StatusCode = 500;
                pErrorCallback?.Invoke(pData);
                return;
            }
            TheInternalRequestState myRequestState = new()
            {
                MyRequestData = pData,
                ResultCallback = tCallback,
                ErrorCallback = pErrorCallback
            };
            try
            {
                HttpClient tClient = GetHttpClient(pData);
                myRequestState.request = new HttpRequestMessage(new HttpMethod(!string.IsNullOrEmpty(pData.HttpMethod) ? pData.HttpMethod : "GET"), pData.RequestUri);
                if (pData.HttpVersion == 1.0)
                    myRequestState.request.Version = HttpVersion.Version10;
                SetUserAgent(myRequestState.request, pData);

                if (pData.Header != null && pData.Header.Count > 0)
                {
                    foreach (string key in pData.Header.Keys)
                    {
                        switch (key)
                        {
                            case "Content-Length":
                            case "User-Agent":
                            case "Connection":
                                break;
                            case "Host":
                                myRequestState.request.Headers.Host = pData.Header[key];
                                break;
                            case "Accept":
                            case "ContentType":
                            case "Referer":
                            default:
                                AddHeader(myRequestState.request, key == "ContentType" ? "Content-Type" : key, pData.Header[key]);
                                break;
                        }
                    }
                }
                myRequestState.request.Headers.Remove("Accept");
                myRequestState.request.Headers.TryAddWithoutValidation("Accept", "*/*");

                SetCookies(myRequestState);
                ApplyCookieHeader(myRequestState);

                SendRequest(myRequestState, tClient);
            }
            catch (Exception e)
            {
                TheSystemMessageLog.WriteLog(251, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", "GetRESTAsync Exception", eMsgLevel.l2_Warning, e.Message), false);
                if (pErrorCallback != null && myRequestState.MyRequestData != null)
                {
                    myRequestState.MyRequestData.ErrorDescription = e.ToString();
                    myRequestState.MyRequestData.ResponseMimeType = "text/html"; //OK
                    myRequestState.MyRequestData.StatusCode = 500;
                    pErrorCallback(myRequestState.MyRequestData);
                }
                myRequestState.Dispose();
            }
        }

        private static void ProcessErrorResponse(TheInternalRequestState pState, byte[] pBody, string pErrPrefix, string pErrMime)
        {
            if (pState?.MyRequestData == null) return;
            var tResponse = pState.response;
            TheBaseAssets.MySYSLOG.WriteToLog(251, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", $"Response error {(int)tResponse.StatusCode} for {pState.MyRequestData.RequestUri}", eMsgLevel.l2_Warning, tResponse.ReasonPhrase));
            if (pState.ErrorCallback != null)
            {
                pState.MyRequestData.ErrorDescription = $"{pErrPrefix}The remote server returned an error: ({(int)tResponse.StatusCode}) {tResponse.ReasonPhrase}.";
                string tContentType = tResponse.Content?.Headers?.ContentType?.ToString();
                pState.MyRequestData.ResponseMimeType = !string.IsNullOrEmpty(tContentType) ? tContentType : pErrMime; //OK
                pState.MyRequestData.StatusCode = (int)tResponse.StatusCode;
                if (pBody != null && pBody.Length > 0)
                    pState.MyRequestData.ResponseBuffer = pBody;
                pState.ErrorCallback(pState.MyRequestData);
            }
        }

        private static void ProcessResponse(TheInternalRequestState pRequestState, byte[] pBody)
        {
            if (pRequestState != null && pRequestState.ResultCallback != null)
            {
                var tResponse = pRequestState.response;
                pRequestState.MyRequestData.ResponseMimeType = tResponse.Content?.Headers?.ContentType?.ToString();
                pRequestState.MyRequestData.ResponseEncoding = tResponse.Content?.Headers?.ContentEncoding != null ? string.Join(", ", tResponse.Content.Headers.ContentEncoding) : "";
                ProcessCookies(pRequestState);
                if (pBody != null && pBody.Length > 0)
                {
                    pRequestState.MyRequestData.ResponseBuffer = pBody;
                }
                pRequestState.MyRequestData.StatusCode = (int)tResponse.StatusCode;
                if (pRequestState.MyRequestData.StatusCode > 300 && pRequestState.MyRequestData.StatusCode < 400 && !pRequestState.MyRequestData.DisableRedirect)
                {
                    string tTarget = tResponse.Headers.TryGetValues("Location", out var tLocs) ? tLocs.FirstOrDefault() : null;
                    if (!string.IsNullOrEmpty(tTarget) && ProcessRedirect(pRequestState.MyRequestData, tTarget))
                    {
                        pRequestState.MyRequestData.StatusCode = (int)tResponse.StatusCode;
                        pRequestState.MyRequestData.RequestCookies = pRequestState.MyRequestData.SessionState.StateCookies;
                        pRequestState.MyRequestData.Header = null;
                        pRequestState.MyRequestData.HttpMethod = "GET";
                        GetRESTAsync(pRequestState.MyRequestData, pRequestState.ResultCallback, pRequestState.ErrorCallback);
                    }
                    else
                        pRequestState.ResultCallback(pRequestState.MyRequestData);
                }
                else
                    pRequestState.ResultCallback(pRequestState.MyRequestData);
            }
        }

        private static void ProcessCookies(TheInternalRequestState myRequestState)
        {
            myRequestState.MyRequestData.SessionState ??= new TheSessionState();
            if (myRequestState.MyRequestData.SessionState.StateCookies == null)
                myRequestState.MyRequestData.SessionState.StateCookies = new cdeConcurrentDictionary<string, string>();
            var tResponse = myRequestState.response;
            if (tResponse.Headers != null)
            {
                myRequestState.MyRequestData.Header = new cdeConcurrentDictionary<string, string>();
                foreach (var tHeader in tResponse.Headers)
                {
                    myRequestState.MyRequestData.Header.TryAdd(tHeader.Key, string.Join(", ", tHeader.Value));
                }
                if (tResponse.Content?.Headers != null)
                {
                    foreach (var tHeader in tResponse.Content.Headers)
                    {
                        myRequestState.MyRequestData.Header.TryAdd(tHeader.Key, string.Join(", ", tHeader.Value));
                    }
                }
                if (myRequestState.MyRequestData.Header.TryGetValue("cdeDeviceID", out string tDeviceID) && !string.IsNullOrEmpty(tDeviceID))
                    myRequestState.MyRequestData.DeviceID = TheCommonUtils.CGuid(tDeviceID);

                if (myRequestState.MyRequestData.TempCookies != null && tResponse.Headers.TryGetValues("Set-Cookie", out var tSetCookies))
                {
                    foreach (string tSetCookie in tSetCookies)
                    {
                        try
                        {
                            myRequestState.MyRequestData.TempCookies.SetCookies(myRequestState.MyRequestData.RequestUri, tSetCookie);
                        }
                        catch (Exception e)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(254, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", "ProcessCookies: invalid Set-Cookie header for " + myRequestState.MyRequestData.RequestUri, eMsgLevel.l2_Warning, e.Message));
                        }
                    }
                }
            }
            if (myRequestState.MyRequestData.TempCookies != null)
            {
                foreach (Cookie c in myRequestState.MyRequestData.TempCookies.GetCookies(myRequestState.MyRequestData.RequestUri))
                {
                    if (!myRequestState.MyRequestData.SessionState.StateCookies.ContainsKey(c.Name.Trim()))
                        myRequestState.MyRequestData.SessionState.StateCookies.TryAdd(c.Name.Trim(), c.Value.Trim() + ";" + c.Path + ";" + c.Domain);
                    else
                        myRequestState.MyRequestData.SessionState.StateCookies[c.Name.Trim()] = c.Value.Trim() + ";" + c.Path + ";" + c.Domain;
                }
            }
        }

        #endregion

        #region RESTPost


        private static void CleanUp(TheInternalRequestState pState)
        {
            if (pState != null)
            {
                if (pState.mRest?.IsPosting > 0)
                    Interlocked.Decrement(ref pState.mRest.IsPosting);
                pState.Dispose();
            }
        }

        /// <summary>
        /// This is the main Function all other PostREST Functions are calling in the end.
        /// </summary>
        /// <param name="pRequest">All necessary data to issue a POST via REST are in this class</param>
        /// <param name="pCallback">Once the POST has returned the pRequest structure is filled with the results and the calllback at this parameter is called</param>
        /// <param name="pErrorCallback">If something unexpected happened during the POST, this callback will called with the intermediate results of the POST</param>
        public void PostRESTAsync(TheRequestData pRequest, Action<TheRequestData> pCallback, Action<TheRequestData> pErrorCallback)
        {
            if (pRequest == null)
            {
                pErrorCallback?.Invoke(null);
                return;
            }
            if (!TheBaseAssets.MasterSwitch)
            {
                pRequest.ErrorDescription = "1400:C-DEngine is shutting down";
                pRequest.StatusCode = 500;
                pErrorCallback?.Invoke(pRequest);
                return;
            }
            if (IsPosting > 10 && TheBaseAssets.MySYSLOG != null)
                TheBaseAssets.MySYSLOG.WriteToLog(253, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", $"Posting risky if other posts {IsPosting} are still running ORG:{pRequest.RequestUri}", eMsgLevel.l2_Warning));
            Interlocked.Increment(ref IsPosting);
            TheInternalRequestState myRequestState = new()
            {
                MyRequestData = pRequest,
                mRest = this,
                ResultCallback = pCallback,
                ErrorCallback = pErrorCallback
            };
            try
            {
                HttpClient tClient = GetHttpClient(pRequest);
                myRequestState.request = new HttpRequestMessage(new HttpMethod(!string.IsNullOrEmpty(pRequest.HttpMethod) ? pRequest.HttpMethod : "POST"), pRequest.RequestUri)
                {
                    Content = new TheRequestDataContent(pRequest)
                };
                if (pRequest.HttpVersion == 1.0)
                    myRequestState.request.Version = HttpVersion.Version10;
                myRequestState.request.Headers.TryAddWithoutValidation("Accept", "*/*");
                if (pRequest.Disable100)
                    myRequestState.request.Headers.ExpectContinue = false;

                SetCookies(myRequestState);
                ApplyCookieHeader(myRequestState);

                if (pRequest.DeviceID != Guid.Empty)
                    myRequestState.request.Headers.TryAddWithoutValidation("cdeDeviceID", pRequest.DeviceID.ToString());
                if (pRequest.Header != null && pRequest.Header.Count > 0)
                {
                    foreach (string key in pRequest.Header.Keys)
                    {
                        switch (key)
                        {
                            case "Content-Length":
                            case "User-Agent":
                            case "Connection":
                                break;
                            case "Host":
                                myRequestState.request.Headers.Host = pRequest.Header[key];
                                break;
                            case "Accept":
                                myRequestState.request.Headers.Remove("Accept");
                                AddHeader(myRequestState.request, key, pRequest.Header[key]);
                                break;
                            default:
                                AddHeader(myRequestState.request, key, pRequest.Header[key]);
                                break;
                        }
                    }
                }
                if (pRequest.RequestUri.LocalPath.StartsWith("/ISB"))
                {
                    myRequestState.request.Headers.Remove("Referer");
                    myRequestState.request.Headers.TryAddWithoutValidation("Referer", TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false));
                }
                SetUserAgent(myRequestState.request, pRequest);
                if (!string.IsNullOrEmpty(pRequest.ResponseMimeType))
                {
                    myRequestState.request.Content.Headers.Remove("Content-Type");
                    myRequestState.request.Content.Headers.TryAddWithoutValidation("Content-Type", pRequest.ResponseMimeType);
                }
                else if (myRequestState.request.Content.Headers.ContentType == null)
                {
                    myRequestState.request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/x-gzip");
                }

                SendRequest(myRequestState, tClient);
            }
            catch (Exception e)
            {
                try
                {
                    if (TheBaseAssets.MySYSLOG != null)
                        TheBaseAssets.MySYSLOG.WriteToLog(254, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", "Post-RESTAsync Exception: " + myRequestState?.MyRequestData?.RequestUri, eMsgLevel.l2_Warning, e.Message));
                    if (pErrorCallback != null)
                    {
                        myRequestState.MyRequestData.ErrorDescription = $"1403:{e}";
                        myRequestState.MyRequestData.ResponseMimeType = "text/plain"; //OK
                        myRequestState.MyRequestData.StatusCode = 500;
                        pErrorCallback(myRequestState.MyRequestData);
                    }
                }
                catch (Exception e2)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(252, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", "PostRESTAsync Exception - ORG:", eMsgLevel.l1_Error, e2.ToString()));
                }
                finally
                {
                    CleanUp(myRequestState);
                }
            }
        }

        #endregion

    }
}
