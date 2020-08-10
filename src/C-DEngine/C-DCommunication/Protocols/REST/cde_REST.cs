// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using System.Net;
using nsCDEngine.BaseClasses;
using System.Collections.Generic;
using System.Threading;
using nsCDEngine.ViewModels;
using System.Security.Cryptography.X509Certificates;

//LOG RANGE: 250 -259

//NETCF Cookies: http://piao8163.blog.163.com/blog/static/969724782011101091723145/

namespace nsCDEngine.Communication
{
    /// <summary>
    /// C-DEngine REST Methods
    /// </summary>
    public partial class TheREST
    {
        private class TheInternalRequestState : IDisposable
        {
            public TheRequestData MyRequestData;
            public List<byte[]> ResultData;
            public byte[] BufferRead;
            public int ResultDataPos;
            public static int BUFFER_SIZE = 16000;
            public HttpWebRequest request;
            public HttpWebResponse response;
            public Stream streamResponse;
            public Action<TheRequestData> ResultCallback;
            public Action<TheRequestData> ErrorCallback;
            internal RegisteredWaitHandle waitHandle;
            internal TheREST mRest;

            public TheInternalRequestState()
            {
                MyRequestData = new TheRequestData();
            }

            public void Dispose()
            {
                if (waitHandle != null)
                {
                    try
                    {
                        if (!waitHandle.Unregister(null))
                        {

                        }
                    }
                    catch { }
                    waitHandle = null;
                }
                BufferRead = null;
                ResultData = null;
                mRest = null;
                ResultDataPos = 0;
                if (request != null)
                {
                    try
                    {
                        request.Abort();
                    }
                    catch { }
                }
                request = null;
                if (response != null)
                {
                    response.Close();
#if !CDE_NET35 && ! CDE_NET4
                    response.Dispose();
#endif
                }
                if (streamResponse != null)
                {
                    streamResponse.Dispose();
                    streamResponse = null;
                }
                response = null;
                ResultCallback = null;
                ErrorCallback = null;
                if (MyRequestData != null)
                {
                    //MyRequestData.PostData = null;
                    //MyRequestData.CookieObject = null;
                    MyRequestData = null;
                }
            }
        }

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
            TheInternalRequestState myRequestState = new TheInternalRequestState
            {
                MyRequestData = pData
            };
            try
            {
                if (tCallback == null)
                {
                    WebRequest wrGETURL;
                    wrGETURL = WebRequest.Create(pData.RequestUri);
                    wrGETURL.BeginGetResponse((a) =>
                    {
                    }, wrGETURL);
                }
                else
                {
                    myRequestState.ResultCallback = tCallback;
                    myRequestState.ErrorCallback = pErrorCallback;
                    myRequestState.request = (HttpWebRequest) WebRequest.Create(myRequestState.MyRequestData.RequestUri);
                    if (!string.IsNullOrEmpty(pData.HttpMethod))
                        myRequestState.request.Method = pData.HttpMethod;
                    if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUrl))
                    {
                        NetworkCredential tNet = null;
                        if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUID))
                        {
                            try
                            {
                                tNet = new NetworkCredential(TheBaseAssets.MyServiceHostInfo.ProxyUID, TheBaseAssets.MyServiceHostInfo.ProxyPWD);
                            }
                            catch (Exception e)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheREST", "Error setting Proxy credentials:", eMsgLevel.l1_Error, e.ToString()));
                            }
                        }
                        myRequestState.request.Proxy = new WebProxy(TheBaseAssets.MyServiceHostInfo.ProxyUrl, true, null, tNet);
                        TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", $"Web Proxy set: {TheBaseAssets.MyServiceHostInfo.ProxyUrl} UID: {!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUID)} PWD: {!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyPWD)}", eMsgLevel.l4_Message));
                    }
                    if (pData.ClientCert != null)
                    {
                        if (pData.ClientCert is X509Certificate)
                        {
                            myRequestState.request.ClientCertificates.Add((X509Certificate)pData.ClientCert);
                        }
                        else if (pData.ClientCert is X509CertificateCollection)
                        {
                            myRequestState.request.ClientCertificates.AddRange((X509CertificateCollection)pData.ClientCert);
                        }
                    }

                    if (myRequestState.MyRequestData.HttpVersion == 1)
                        myRequestState.request.ProtocolVersion = HttpVersion.Version10;
                    myRequestState.request.AllowAutoRedirect = false;
                    if (!string.IsNullOrEmpty(pData.UserAgent))
                        myRequestState.request.UserAgent = pData.UserAgent;
                    else
                        myRequestState.request.UserAgent = !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.CustomUA) ? TheBaseAssets.MyServiceHostInfo.CustomUA : "C-DEngine " + TheBaseAssets.CurrentVersionInfo; // "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.22 (KHTML, like Gecko) Chrome/25.0.1364.152 Safari/537.22";

                    if (pData.Header != null && pData.Header.Count > 0) //TODO: Verify with All Systems
                    {
                        foreach (string key in pData.Header.Keys)
                        {
                            switch (key)
                            {
                                case "Content-Length":
                                case "User-Agent":
                                case "Connection":
                                    //myRequestState.request.Connection = pData.Header[key];
                                    break;
                                case "Host":
#if !CDE_NET35
                                    myRequestState.request.Host = pData.Header[key];
#endif
                                    break;
                                case "Accept":
                                    myRequestState.request.Accept = pData.Header[key];
                                    break;
                                case "ContentType":
                                    myRequestState.request.ContentType = pData.Header[key];
                                    break;
                                case "Referer":
                                    myRequestState.request.Referer = pData.Header[key];
                                    break;
                                default:
                                    myRequestState.request.Headers.Add(key, pData.Header[key]);
                                    break;
                            }
                        }
                    }
                    //TheSystemMessageLog.ToCo(string.Format("GetREST: {0}",pData));
                    myRequestState.request.Accept = "*/*";

                    if (myRequestState.MyRequestData.TimeOut > 0)
                    {
                        myRequestState.request.ReadWriteTimeout = pData.TimeOut;
                        myRequestState.request.Timeout = pData.TimeOut;
                    }
                    else
                    {
                        if (TheBaseAssets.MyServiceHostInfo != null)
                        {
                            myRequestState.MyRequestData.TimeOut = TheBaseAssets.MyServiceHostInfo.TO.HeartBeatRate*1000;
                            myRequestState.request.ReadWriteTimeout = TheBaseAssets.MyServiceHostInfo.TO.HeartBeatRate*1000;
                            myRequestState.request.Timeout = TheBaseAssets.MyServiceHostInfo.TO.HeartBeatRate*1000;
                        }
                    }
                    if (!string.IsNullOrEmpty(myRequestState.MyRequestData.UID) && !string.IsNullOrEmpty(myRequestState.MyRequestData.PWD))
                    {
                        if (string.IsNullOrEmpty(myRequestState.MyRequestData.DOM)) myRequestState.MyRequestData.DOM = "";
                        NetworkCredential networkCredential = new NetworkCredential(myRequestState.MyRequestData.UID, myRequestState.MyRequestData.PWD, myRequestState.MyRequestData.DOM);
                        myRequestState.request.Credentials = networkCredential;
                    }

                    //if (myRequestState.MyRequestData.RequestCookies != null)
                    //{
                    SetCookies(myRequestState);
                    myRequestState.request.CookieContainer = myRequestState.MyRequestData.TempCookies;
                    //}

                    myRequestState.request.BeginGetResponse(GETRespCallback, myRequestState);
                }
            }
            catch (WebException e)
            {
                //TheSystemMessageLog.ToCo(string.Format("GetREST: E1"));
                TheSystemMessageLog.WriteLog(250, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", "GetRESTAsync WebException", eMsgLevel.l2_Warning, e.Message), false);
                if (pErrorCallback != null && myRequestState.MyRequestData != null)
                {
                    myRequestState.MyRequestData.ErrorDescription = e.ToString();
                    myRequestState.MyRequestData.ResponseMimeType = "text/html"; //OK
                    myRequestState.MyRequestData.StatusCode = 500;
                    pErrorCallback(myRequestState.MyRequestData);
                }
                if (myRequestState != null)
                    myRequestState.Dispose();
            }
            catch (Exception e)
            {
                //TheSystemMessageLog.ToCo(string.Format("GetREST: E2"));
                TheSystemMessageLog.WriteLog(251, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", "GetRESTAsync Exception", eMsgLevel.l2_Warning, e.Message), false);
                if (pErrorCallback != null && myRequestState.MyRequestData != null)
                {
                    myRequestState.MyRequestData.ErrorDescription = e.ToString();
                    myRequestState.MyRequestData.ResponseMimeType = "text/html"; //OK
                    myRequestState.MyRequestData.StatusCode = 500;
                    pErrorCallback(myRequestState.MyRequestData);
                }
                if (myRequestState != null)
                    myRequestState.Dispose();
            }
        }

        private static void GETRespCallback(IAsyncResult asynchronousResult)
        {
            TheInternalRequestState myRequestState = null;
            try
            {
                //TheSystemMessageLog.ToCo(string.Format("GetREST: 3"));
                myRequestState = (TheInternalRequestState) asynchronousResult.AsyncState;
                //TheSystemMessageLog.ToCo(string.Format("GetREST: 3aa {0} {1}",myRequestState.request.ContentLength,myRequestState.request.ContentType));
                myRequestState.response = (HttpWebResponse) myRequestState.request.EndGetResponse(asynchronousResult);
                //TheSystemMessageLog.ToCo(string.Format("GetREST: 3a"));
                //myRequestState.MyRequestData.StatusCode = TheCommonUtils.CInt(((HttpWebResponse)myRequestState.response).StatusCode); //NEW V3.200 - Move to the end
                //TheSystemMessageLog.ToCo(string.Format("GetREST: 3b {0}", myRequestState.MyRequestData.StatusCode));
                myRequestState.streamResponse = myRequestState.response.GetResponseStream();
                if (myRequestState.streamResponse == null)
                {
                    // CODE REVIEW: Should there an error callback here, or is this always the consequence of a cancel/timeout? Can this even happen (i.e. GetResponseStream() either throws or returns a stream)
                    myRequestState.Dispose();
                    myRequestState = null;
                    TheCommonUtils.CloseOrDispose(asynchronousResult?.AsyncWaitHandle);
                    return;
                }
                myRequestState.BufferRead = new byte[TheInternalRequestState.BUFFER_SIZE];
#if NEW_REST_ASYNC
                TheCommonUtils.cdeRunAsync("ReceiveAsync", true, async (o) =>
                {
                    int read = 0;
                    do
                    {
                        read = await myRequestState.streamResponse.ReadAsync(myRequestState.BufferRead, 0, TheInternalRequestState.BUFFER_SIZE);
                        if (read > 0)
                        {
                            byte[] tBuf = new byte[read];
                            TheCommonUtils.cdeBlockCopy(myRequestState.BufferRead, 0, tBuf, 0, read);
                            if (myRequestState.ResultData == null) myRequestState.ResultData = new List<byte[]>();
                            myRequestState.ResultData.Add(tBuf);
                            myRequestState.ResultDataPos += read;
                        }
                    }
                    while (read > 0);
                    ProcessResponse(myRequestState);
                    myRequestState.Dispose();
                });
#else
                myRequestState.streamResponse.BeginRead(myRequestState.BufferRead, 0, TheInternalRequestState.BUFFER_SIZE, GETReadCallBack, myRequestState);
#endif
                //TheSystemMessageLog.ToCo(string.Format("GetREST: 4"));
            }
            catch (WebException e)
            {
                string turl = "Unknown";
                try
                {
                    if (myRequestState != null)
                    {
                        if (myRequestState.MyRequestData != null)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(251, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", string.Format("GETRespCallback E4 Exception for {0}", myRequestState.MyRequestData), eMsgLevel.l2_Warning, e.ToString()));
                            turl = myRequestState.MyRequestData.RequestUri?.ToString() ?? turl;
                            if (myRequestState.ErrorCallback != null)
                            {
                                myRequestState.MyRequestData.ErrorDescription = e.ToString();
                                myRequestState.MyRequestData.ResponseMimeType = "text/html"; //OK
                                if (e.Response != null)
                                    myRequestState.MyRequestData.StatusCode = TheCommonUtils.CInt(((HttpWebResponse)e.Response).StatusCode);
                                else
                                    myRequestState.MyRequestData.StatusCode = 404;
                                myRequestState.ErrorCallback(myRequestState.MyRequestData);
                            }
                        }
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(252, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", "RespCallback Exception - ORG:" + turl, eMsgLevel.l2_Warning, e.ToString()));
                }
                catch (Exception e2)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(252, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", "RespCallback Exception - ORG:" + turl, eMsgLevel.l2_Warning, e2.ToString()));
                }
                finally
                {
                    myRequestState?.Dispose();
                    TheCommonUtils.CloseOrDispose(asynchronousResult.AsyncWaitHandle);
                }
            }
            catch (Exception e)
            {
                string turl = "Unknown";
                try
                {
                    if (myRequestState != null)
                    {
                        if (myRequestState.MyRequestData != null)
                        {
                            turl = myRequestState.MyRequestData.RequestUri?.ToString() ?? turl;
                            if (myRequestState.ErrorCallback != null)
                            {
                                myRequestState.MyRequestData.ErrorDescription = e.ToString();
                                myRequestState.MyRequestData.ResponseMimeType = "text/html"; //OK
                                myRequestState.MyRequestData.StatusCode = 500;
                                myRequestState.ErrorCallback(myRequestState.MyRequestData);
                            }
                        }
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(252, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", "RespCallback Exception - ORG:" + turl, eMsgLevel.l2_Warning, e.ToString()));
                }
                catch (Exception e2)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(252, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", "RespCallback Exception - ORG:" + turl, eMsgLevel.l2_Warning, e2.ToString()));
                }
                finally
                {
                    myRequestState?.Dispose();
                    TheCommonUtils.CloseOrDispose(asynchronousResult.AsyncWaitHandle);
                }

            }
        }

#if !NEW_REST_ASYNC
        private static void GETReadCallBack(IAsyncResult asyncResult)
        {
            TheInternalRequestState myRequestState = null;
            try
            {
                myRequestState = (TheInternalRequestState)asyncResult.AsyncState;
                int read = myRequestState.streamResponse.EndRead(asyncResult);
                if (read > 0)
                {
                    byte[] tBuf = new byte[read];
                    TheCommonUtils.cdeBlockCopy(myRequestState.BufferRead, 0, tBuf, 0, read);
                    if (myRequestState.ResultData == null) myRequestState.ResultData = new List<byte[]>();
                    myRequestState.ResultData.Add(tBuf);
                    myRequestState.ResultDataPos += read;
                    myRequestState.streamResponse.BeginRead(myRequestState.BufferRead, 0, TheInternalRequestState.BUFFER_SIZE, GETReadCallBack, myRequestState);
                }
                else
                {
                    ProcessResponse(myRequestState);
                    myRequestState.Dispose();
                    TheCommonUtils.CloseOrDispose(asyncResult.AsyncWaitHandle);
                }
            }
            catch (Exception e)
            {
                //TheSystemMessageLog.ToCo(string.Format("GetREST: E5"));
                string ats = "state not valid";
                try
                {
                    if (myRequestState != null && myRequestState.MyRequestData.RequestUri != null) ats = myRequestState.MyRequestData.RequestUri.ToString();
                    TheBaseAssets.MySYSLOG.WriteToLog(252, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", "ReadCallBack Exception: " + ats, eMsgLevel.l2_Warning, e.ToString()));
                    if (myRequestState != null && myRequestState.ErrorCallback != null && myRequestState.MyRequestData != null)
                    {
                        myRequestState.MyRequestData.ErrorDescription = ats + e;
                        myRequestState.MyRequestData.ResponseMimeType = "text/html"; //OK
                        myRequestState.MyRequestData.StatusCode = 500;
                        myRequestState.ErrorCallback(myRequestState.MyRequestData);
                    }
                }
                catch { }
                finally
                {
                    if (myRequestState != null)
                        myRequestState.Dispose();
                    TheCommonUtils.CloseOrDispose(asyncResult.AsyncWaitHandle);
                }
            }
        }
#endif

        private static void ProcessResponse(TheInternalRequestState pRequestState)
        {
            if (pRequestState != null && pRequestState.ResultCallback != null)
            {
                pRequestState.MyRequestData.ResponseMimeType = pRequestState.response.ContentType;
                pRequestState.MyRequestData.ResponseEncoding = pRequestState.response.ContentEncoding;
                ProcessCookies(pRequestState);
                //TheSystemMessageLog.ToCo(string.Format("GetREST: 6 {0}", pRequestState.ResultDataPos));
                if (pRequestState.ResultDataPos > 0)
                {
                    pRequestState.MyRequestData.ResponseBuffer = new byte[pRequestState.ResultDataPos];
                    int pos = 0;
                    foreach (byte[] tBuf in pRequestState.ResultData)
                    {
                        TheCommonUtils.cdeBlockCopy(tBuf, 0, pRequestState.MyRequestData.ResponseBuffer, pos, tBuf.Length);
                        pos += tBuf.Length;
                    }
                    pRequestState.ResultData.Clear();
                    //pRequestState.MyRequestData.ResponseBufferStr = TheCommonUtils.CArray2UTF8String(pRequestState.MyRequestData.ResponseBuffer);
                }
                pRequestState.MyRequestData.StatusCode = TheCommonUtils.CInt((pRequestState.response).StatusCode);
                if (pRequestState.MyRequestData.StatusCode > 300 && pRequestState.MyRequestData.StatusCode < 400 && !pRequestState.MyRequestData.DisableRedirect)
                {
                    string tTarget = pRequestState.response.Headers["Location"];
                    if (ProcessRedirect(pRequestState.MyRequestData, tTarget))
                    {
                        //TheSystemMessageLog.ToCo(string.Format("GetREST: Redir {0} URI:{1}", tTarget, pRequestState.MyRequestData.RequestUri));
                        pRequestState.MyRequestData.StatusCode = (int) pRequestState.response.StatusCode;
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
            if (myRequestState.MyRequestData.SessionState == null)
                myRequestState.MyRequestData.SessionState = new TheSessionState();
            if (myRequestState.MyRequestData.SessionState.StateCookies == null)
                myRequestState.MyRequestData.SessionState.StateCookies = new cdeConcurrentDictionary<string, string>();
            if (myRequestState.response.Headers != null)
            {
                myRequestState.MyRequestData.Header = new cdeConcurrentDictionary<string, string>();
                if (!string.IsNullOrEmpty(myRequestState.response.Headers["cdeDeviceID"]))
                    myRequestState.MyRequestData.DeviceID = TheCommonUtils.CGuid(myRequestState.response.Headers["cdeDeviceID"]);
                int max = myRequestState.response.Headers.AllKeys.Length;
                for (int i = 0; i < max; i++)
                {
                    myRequestState.MyRequestData.Header.TryAdd(myRequestState.response.Headers.AllKeys[i], myRequestState.response.Headers[myRequestState.response.Headers.AllKeys[i]]);
                }
            }
            if (myRequestState.MyRequestData.TempCookies != null)
            {
                foreach (Cookie c in myRequestState.MyRequestData.TempCookies.GetCookies(myRequestState.request.RequestUri))
                {
                    if (!myRequestState.MyRequestData.SessionState.StateCookies.ContainsKey(c.Name.Trim()))
                        myRequestState.MyRequestData.SessionState.StateCookies.TryAdd(c.Name.Trim(), c.Value.Trim() + ";" + c.Path + ";" + c.Domain);
                    else
                        myRequestState.MyRequestData.SessionState.StateCookies[c.Name.Trim()] = c.Value.Trim() + ";" + c.Path + ";" + c.Domain;
                }
            }
            //VERIFY: Is this realy necessary?
            if (myRequestState.response.Cookies != null)
            {
                foreach (Cookie c in myRequestState.response.Cookies)
                {
                    if (myRequestState.MyRequestData.SessionState.StateCookies.ContainsKey(c.Name.Trim()))
                        myRequestState.MyRequestData.SessionState.StateCookies[c.Name.Trim()] = c.Value.Trim() + ";" + c.Path + ";" + c.Domain;
                    else
                        myRequestState.MyRequestData.SessionState.StateCookies.TryAdd(c.Name.Trim(), c.Value.Trim() + ";" + c.Path + ";" + c.Domain);
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
                    Interlocked.Decrement(ref pState.mRest.IsPosting); //= false;
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
            Interlocked.Increment(ref IsPosting); // = true;
            TheInternalRequestState myRequestState = new TheInternalRequestState
            {
                MyRequestData = pRequest,
                mRest=this
            };
            IAsyncResult tResult = null;
            try
            {
                myRequestState.request = (HttpWebRequest) WebRequest.Create(myRequestState.MyRequestData.RequestUri);
                if (!string.IsNullOrEmpty(pRequest.HttpMethod))
                    myRequestState.request.Method = pRequest.HttpMethod;
                else
                    myRequestState.request.Method = "POST";
                if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUrl))
                {
                    NetworkCredential tNet = null;
                    try
                    {
                        if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUID))
                        {
                            tNet = new NetworkCredential(TheBaseAssets.MyServiceHostInfo.ProxyUID, TheBaseAssets.MyServiceHostInfo.ProxyPWD);
                        }
                        myRequestState.request.Proxy = new WebProxy(TheBaseAssets.MyServiceHostInfo.ProxyUrl, true, null, tNet);
                        TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", $"Web Proxy set: {TheBaseAssets.MyServiceHostInfo.ProxyUrl} UID: {!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUID)} PWD: {!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyPWD)}", eMsgLevel.l4_Message));
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheREST", "Error setting Proxy credentials:", eMsgLevel.l1_Error, e.ToString()));
                    }
                    // TheBaseAssets.MySYSLOG.WriteToLog(253, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", string.Format("Setting Proxy to URL:{0} U:{1} P:{2}", TheBaseAssets.MyServiceHostInfo.ProxyUrl, TheBaseAssets.MyServiceHostInfo.ProxyUID, TheBaseAssets.MyServiceHostInfo.ProxyPWD), eMsgLevel.l3_ImportantMessage));
                }
                if (myRequestState.MyRequestData.HttpVersion == 1)
                    myRequestState.request.ProtocolVersion = HttpVersion.Version10;
                myRequestState.request.AllowAutoRedirect = false;
                myRequestState.request.Accept = "*/*";
                if (pRequest.Disable100)
                    myRequestState.request.ServicePoint.Expect100Continue = false;

                if (pRequest.ClientCert != null)
                {
                    if (pRequest.ClientCert is X509Certificate)
                    {
                        myRequestState.request.ClientCertificates.Add((X509Certificate) pRequest.ClientCert);
                    }
                    else if (pRequest.ClientCert is X509CertificateCollection)
                    {
                        myRequestState.request.ClientCertificates.AddRange((X509CertificateCollection) pRequest.ClientCert);
                    }
                }

                myRequestState.ResultCallback = pCallback;
                myRequestState.ErrorCallback = pErrorCallback;

                //if (myRequestState.MyRequestData.RequestCookies != null)
                //{
                SetCookies(myRequestState);
                myRequestState.request.CookieContainer = myRequestState.MyRequestData.TempCookies;
                //}
                //myRequestState.request.KeepAlive = false;//TODO: NEW!
                //myRequestState.request.Pipelined = false;   //TODO: NEW!
                if (myRequestState.MyRequestData.TimeOut > 0)
                {
                    myRequestState.request.ReadWriteTimeout = myRequestState.MyRequestData.TimeOut;
                    myRequestState.request.Timeout = myRequestState.MyRequestData.TimeOut;
                }
                else
                {
                    myRequestState.MyRequestData.TimeOut = TheBaseAssets.MyServiceHostInfo.TO.HeartBeatRate*1000;
                    myRequestState.request.ReadWriteTimeout = TheBaseAssets.MyServiceHostInfo.TO.HeartBeatRate*1000;
                    myRequestState.request.Timeout = TheBaseAssets.MyServiceHostInfo.TO.HeartBeatRate*1000;
                }
                if (myRequestState.MyRequestData.DeviceID != Guid.Empty)
                    myRequestState.request.Headers.Add("cdeDeviceID", myRequestState.MyRequestData.DeviceID.ToString());
                if (pRequest.Header != null && pRequest.Header.Count > 0) //TODO: Verify with All Systems
                {
                    foreach (string key in pRequest.Header.Keys)
                    {
                        switch (key)
                        {
                            case "Content-Length":
                            case "User-Agent":
                            case "Connection":
                                //myRequestState.request.Connection = pData.Header[key];
                                break;
                            case "Host":
#if !CDE_NET35
                                myRequestState.request.Host = pRequest.Header[key];
#endif
                                break;
                            case "Accept":
                                myRequestState.request.Accept = pRequest.Header[key];
                                break;
                            case "Content-Type":
                                myRequestState.request.ContentType = pRequest.Header[key];
                                break;
                            case "Referer":
                                myRequestState.request.Referer = pRequest.Header[key];
                                break;
                            default:
                                myRequestState.request.Headers.Add(key, pRequest.Header[key]);
                                break;
                        }
                    }
                }
                if (myRequestState.MyRequestData.RequestUri.LocalPath.StartsWith("/ISB"))
                    myRequestState.request.Referer = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false);
                if (!string.IsNullOrEmpty(myRequestState.MyRequestData.UserAgent))
                    myRequestState.request.UserAgent = myRequestState.MyRequestData.UserAgent;
                else
                    myRequestState.request.UserAgent = !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.CustomUA) ? TheBaseAssets.MyServiceHostInfo.CustomUA : "C-DEngine " + TheBaseAssets.CurrentVersionInfo; // "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.22 (KHTML, like Gecko) Chrome/25.0.1364.152 Safari/537.22";
                if (!string.IsNullOrEmpty(myRequestState.MyRequestData.UID) && !string.IsNullOrEmpty(myRequestState.MyRequestData.PWD))
                {
                    if (string.IsNullOrEmpty(myRequestState.MyRequestData.DOM)) myRequestState.MyRequestData.DOM = "";
                    NetworkCredential networkCredential = new NetworkCredential(myRequestState.MyRequestData.UID, myRequestState.MyRequestData.PWD, myRequestState.MyRequestData.DOM);
                    myRequestState.request.Credentials = networkCredential;
                }
                if (!string.IsNullOrEmpty(myRequestState.MyRequestData.ResponseMimeType))
                    myRequestState.request.ContentType = myRequestState.MyRequestData.ResponseMimeType;
                else
                    myRequestState.request.ContentType = "application/x-gzip";

                myRequestState.request.ContentLength = myRequestState.MyRequestData.GetContentLength();

                tResult = myRequestState.request.BeginGetRequestStream(PostRequestStreamCallback, myRequestState);
                if (tResult == null)
                {
                    myRequestState.MyRequestData.ErrorDescription = "1401:Begin Request Stream Failed - missing in Assembly?";
                    pErrorCallback?.Invoke(myRequestState.MyRequestData);
                    CleanUp(myRequestState);
                    myRequestState = null;
                    return;
                }
                try
                {
                    myRequestState.waitHandle = ThreadPool.RegisterWaitForSingleObject(tResult.AsyncWaitHandle,new WaitOrTimerCallback(TimeoutCallback), myRequestState, myRequestState.MyRequestData.TimeOut, true);
                }
                catch (ObjectDisposedException) { } // This can happen in races with BeginGetRequestStream error cleanup: swallow to avoid a second error callback
            }
            catch (WebException e)
            {
                try
                {
                    if (TheBaseAssets.MySYSLOG != null)
                        TheBaseAssets.MySYSLOG.WriteToLog(253, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", "Post-RESTAsync WebException: " + myRequestState?.MyRequestData?.RequestUri, eMsgLevel.l2_Warning, e.Message));
                    if (pErrorCallback != null)
                    {
                        myRequestState.MyRequestData.ErrorDescription = $"1402:{e}";
                        myRequestState.MyRequestData.ResponseMimeType = "text/plain"; //OK
                        myRequestState.MyRequestData.StatusCode = ((int?)(((e as WebException)?.Response) as HttpWebResponse)?.StatusCode) ?? 500; ;
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
                    myRequestState = null;
                    TheCommonUtils.CloseOrDispose(tResult?.AsyncWaitHandle);
                }
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
                    myRequestState = null;
                    TheCommonUtils.CloseOrDispose(tResult?.AsyncWaitHandle);
                }
            }
        }

        private void PostRequestStreamCallback(IAsyncResult asynchronousResult)
        {
            TheInternalRequestState tRequestState = null;
            try
            {
                tRequestState = (TheInternalRequestState) asynchronousResult.AsyncState;

                using (Stream postStream = tRequestState.request.EndGetRequestStream(asynchronousResult))
                {
                    tRequestState.MyRequestData.WritePostDataToStream(postStream);
                }
                tRequestState.request.BeginGetResponse(POSTRespCallback, tRequestState);
            }
            catch (Exception e)
            {
                string rReq = " Request State invalid";
                if (tRequestState != null)
                {
                    try
                    {
                        if (tRequestState.request != null)
                            tRequestState.request.Abort();
                        if (tRequestState.ErrorCallback != null)
                        {
                            if (tRequestState.MyRequestData != null)
                            {
                                rReq = string.Format("ORG:{0}", tRequestState.MyRequestData.RequestUri);
                                tRequestState.MyRequestData.ErrorDescription = $"1404:{e}";
                                tRequestState.MyRequestData.ResponseMimeType = "text/plain"; //OK
                                tRequestState.MyRequestData.StatusCode = ((int?)(((e as WebException)?.Response) as HttpWebResponse)?.StatusCode) ?? 500; ;
                                tRequestState.ErrorCallback(tRequestState.MyRequestData);
                            }
                            else
                                tRequestState.ErrorCallback(new TheRequestData() { ErrorDescription = $"1405:{e}" });
                        }
                    }
                    catch (Exception e2)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(255, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", "PostRequestStreamCallback Exception for " + rReq, eMsgLevel.l1_Error, e2.Message));
                    }
                    finally
                    {
                        CleanUp(tRequestState);
                        tRequestState = null;
                        TheCommonUtils.CloseOrDispose(asynchronousResult?.AsyncWaitHandle);
                    }
                }
                TheBaseAssets.MySYSLOG.WriteToLog(255, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", "PostRequestStreamCallback Exception for " + rReq, eMsgLevel.l2_Warning, e.Message));
            }
        }

        private void POSTRespCallback(IAsyncResult asynchronousResult)
        {
            TheInternalRequestState tRequestState = null;
            try
            {
                tRequestState = (TheInternalRequestState) asynchronousResult.AsyncState;
                tRequestState.response = (HttpWebResponse) tRequestState.request.EndGetResponse(asynchronousResult);
                //tRequestState.MyRequestData.StatusCode = TheCommonUtils.CInt(((HttpWebResponse)tRequestState.response).StatusCode); //NEW 3.200 - Moved to the End of Processing
                tRequestState.streamResponse = tRequestState.response.GetResponseStream();
                if (tRequestState.streamResponse == null)
                {
                    CleanUp(tRequestState);
                    tRequestState = null;
                    TheCommonUtils.CloseOrDispose(asynchronousResult?.AsyncWaitHandle);
                    return;
                }
                tRequestState.BufferRead = new byte[TheInternalRequestState.BUFFER_SIZE];
#if NEW_REST_ASYNC
                TheCommonUtils.cdeRunAsync("POSTRespCallback", true, async (o) =>
                {
                    int read = 0;
                    do
                    {
                        read = await tRequestState.streamResponse.ReadAsync(tRequestState.BufferRead, 0, TheInternalRequestState.BUFFER_SIZE);
                        if (read > 0)
                        {
                            byte[] tBuf = new byte[read];
                            TheCommonUtils.cdeBlockCopy(tRequestState.BufferRead, 0, tBuf, 0, read);
                            if (tRequestState.ResultData == null) tRequestState.ResultData = new List<byte[]>();
                            tRequestState.ResultData.Add(tBuf);
                            tRequestState.ResultDataPos += read;
                        }
                    }
                    while (read > 0);
                    ProcessResponse(tRequestState);
                    CleanUp(tRequestState);
                    tRequestState = null;
                });
#else
                tRequestState.streamResponse.BeginRead(tRequestState.BufferRead, 0, TheInternalRequestState.BUFFER_SIZE, POSTReadCallBack, tRequestState); //IAsyncResult asynchronousInputRead =
#endif
            }
            catch (Exception e)
            {
                string rReq = " !Request State invalid!";
                try
                {
                    if (tRequestState != null)
                    {
                        try
                        {
                            if (tRequestState.request != null)
                                tRequestState.request.Abort();
                        }
                        catch { }
                        if (tRequestState.ErrorCallback != null)
                        {
                            if (tRequestState.MyRequestData != null)
                            {
                                rReq = string.Format("ORG:{0}", tRequestState.MyRequestData.RequestUri);
                                tRequestState.MyRequestData.ErrorDescription = $"1406:{e}";
                                tRequestState.MyRequestData.ResponseMimeType = "text/plain"; //OK
                                tRequestState.MyRequestData.StatusCode = ((int?)(((e as WebException)?.Response) as HttpWebResponse)?.StatusCode) ?? 500;
                                try
                                {
                                    if (e is WebException we)
                                    {
                                        if (we.Response != null)
                                        {
                                            var stm = we.Response.GetResponseStream();
                                            var buffer = new byte[we.Response.ContentLength];
                                            if (stm.Read(buffer, 0, buffer.Length) == buffer.Length)
                                            {
                                                tRequestState.MyRequestData.ResponseBuffer = buffer;
                                                if (!string.IsNullOrEmpty(we.Response.ContentType))
                                                {
                                                    tRequestState.MyRequestData.ResponseMimeType = we.Response.ContentType;
                                                }
                                            }
                                        }
                                    }
                                }
                                catch { }

                                tRequestState.ErrorCallback(tRequestState.MyRequestData);
                            }
                            else
                                tRequestState.ErrorCallback(new TheRequestData() { ErrorDescription = $"1407:{e}" });
                        }
                    }
                }
                catch (Exception e2)
                {
                    if (TheBaseAssets.MySYSLOG != null)
                        TheBaseAssets.MySYSLOG.WriteToLog(256, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", $"POSTRespCallback Exception crash for {rReq} {IsPosting}", eMsgLevel.l1_Error, e2.Message));
                }
                finally
                {
                    CleanUp(tRequestState);
                    tRequestState = null;
                    TheCommonUtils.CloseOrDispose(asynchronousResult?.AsyncWaitHandle);
                }
                if (TheBaseAssets.MySYSLOG != null)
                    TheBaseAssets.MySYSLOG.WriteToLog(256, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", $"POSTRespCallback Exception for {rReq} {IsPosting}", eMsgLevel.l2_Warning, e.Message));
            }
        }

#if !NEW_REST_ASYNC
        private void POSTReadCallBack(IAsyncResult asyncResult)
        {
            TheInternalRequestState tRequestState = null;
            try
            {
                tRequestState = asyncResult.AsyncState as TheInternalRequestState;
                if (tRequestState == null) return;
                int read = tRequestState.streamResponse.EndRead(asyncResult);
                if (read > 0)
                {
                    byte[] tBuf = new byte[read];
                    TheCommonUtils.cdeBlockCopy(tRequestState.BufferRead, 0, tBuf, 0, read);
                    if (tRequestState.ResultData == null) tRequestState.ResultData = new List<byte[]>();
                    tRequestState.ResultData.Add(tBuf);
                    tRequestState.ResultDataPos += read;
                    tRequestState.streamResponse.BeginRead(tRequestState.BufferRead, 0, TheInternalRequestState.BUFFER_SIZE, POSTReadCallBack, tRequestState);
                }
                else
                {
                    ProcessResponse(tRequestState);
                    CleanUp(tRequestState);
                    tRequestState = null;
                    TheCommonUtils.CloseOrDispose(asyncResult?.AsyncWaitHandle);
                }
            }
            catch (Exception e)
            {
                string tReq = " !Request State invalid!";
                try
                {
                    if (tRequestState != null)
                    {
                        tRequestState.request.Abort();
                        if (tRequestState.ErrorCallback != null)
                        {
                            if (tRequestState.MyRequestData != null)
                            {
                                tReq = string.Format("ORG:{0}", tRequestState.MyRequestData.RequestUri);
                                tRequestState.MyRequestData.ErrorDescription = $"1408:{e}";
                                tRequestState.MyRequestData.ResponseMimeType = "text/plain"; //OK
                                tRequestState.MyRequestData.StatusCode = ((int?)(((e as WebException)?.Response) as HttpWebResponse)?.StatusCode) ?? 500; ;
                                tRequestState.ErrorCallback(tRequestState.MyRequestData);
                            }
                            else
                                tRequestState.ErrorCallback(new TheRequestData() { ErrorDescription = $"1409:{e}" });
                        }
                    }
                }
                catch (Exception e2)
                {
                    if (TheBaseAssets.MySYSLOG != null)
                        TheBaseAssets.MySYSLOG.WriteToLog(257, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", "POSTReadCallBack Exception for " + tReq, eMsgLevel.l1_Error, e2.ToString()));
                }
                finally
                {
                    CleanUp(tRequestState);
                    if (TheBaseAssets.MySYSLOG != null)
                        TheBaseAssets.MySYSLOG.WriteToLog(257, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", "POSTReadCallBack Exception for " + tReq, eMsgLevel.l2_Warning, e.ToString()));
                    tRequestState = null;
                    TheCommonUtils.CloseOrDispose(asyncResult?.AsyncWaitHandle);
                }
            }
        }
#endif

        private static void TimeoutCallback(object pState, bool timedOut)
        {
            if (timedOut)
            {
                if (pState is TheInternalRequestState tPostState)
                {
                    string tReg = "Unknow Org";
                    try
                    {
                        if (tPostState.MyRequestData != null)
                        {
                            tReg = tPostState.MyRequestData.RequestUri?.ToString() ?? tReg;
                            tPostState.MyRequestData.ErrorDescription = "1410:Timeout Error";
                            tPostState.ErrorCallback?.Invoke(tPostState.MyRequestData);
                            TheBaseAssets.MySYSLOG.WriteToLog(257, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheREST", "HttpWebRequest timed out and was aborted to ORG:" + tReg, eMsgLevel.l2_Warning));
                        }
                    }
                    catch (Exception e2)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(257, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheREST", "TimeoutCallback: timed out and was aborted to ORG:" + tReg, eMsgLevel.l1_Error, e2.ToString()));
                    }
                    finally
                    {
                        CleanUp(tPostState);
                    }
                }
            }
        }

#endregion

    }
}
