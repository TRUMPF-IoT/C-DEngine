// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace nsCDEngine.Communication.HttpService
{
    internal class cdeHttpProcessor : IDisposable
    {
        private const int MAX_POST_SIZE = 25 * 1024 * 1024; // 25MB
        private const int BUF_SIZE = 4096;

        private Stream mRequestStream;
        private cdeMidiHttpService mHServer;

        private readonly byte[] mContinue100;

        string firstRequestLine = "";

        public cdeHttpProcessor()
        {
            mContinue100 = TheCommonUtils.CUTF8String2Array("HTTP/1.1 100 Continue\r\n\r\n");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (mRequestStream != null)
                mRequestStream.Dispose();
        }

        private string streamReadLine(Stream pInputStream)
        {
            int chRead;
            string strRet = "";
            int cnt = 0;
            bool HadTimeout = false;
            while (TheBaseAssets.MasterSwitch)
            {
                chRead = pInputStream.ReadByte();
                if (chRead == '\n') { break; }
                if (chRead == '\r') { continue; }
                if (chRead == -1)
                {
                    cnt++;
                    if (cnt == 5000)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4350, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("HttpMidiServer", $"streamReadLine waiting for {cnt}ms", eMsgLevel.l2_Warning));
                        HadTimeout = true;
                    }
                    if (cnt > TheBaseAssets.MyServiceHostInfo.TO.HeartBeatRate * 10000)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4350, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("HttpMidiServer", $"streamReadLine waiting for {cnt}ms failing", eMsgLevel.l1_Error));
                        strRet = "";
                        break;
                    }
                    Thread.Sleep(1);
                    continue;
                }
                strRet += Convert.ToChar(chRead);
            }

            if (HadTimeout)
                TheBaseAssets.MySYSLOG.WriteToLog(4350, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("HttpMidiServer", $"streamReadLine had Long Time and was waiting for {cnt}ms", eMsgLevel.l2_Warning));
            return strRet;
        }

        public void ProcessHttpRequest(HttpListenerContext mContext)
        {
            TheRequestData tRequestData = new ();
            try
            {
                tRequestData.RequestUri = mContext.Request.Url;
                tRequestData.UserAgent = mContext.Request.UserAgent;
                tRequestData.ServerTags = null;
                tRequestData.HttpMethod = mContext.Request.HttpMethod;  //NEW 3.200
                tRequestData.Header = TheCommonUtils.cdeNameValueToDirectory(mContext.Request.Headers);
                tRequestData.ResponseMimeType = mContext.Request.ContentType;

                tRequestData.ClientCert = mContext.Request.GetClientCertificate();

                if (TheCommCore.MyHttpService != null && TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage > 1) //If CDE requires a certificate, terminate all incoming requests before any processing
                {
                    var err = TheCommCore.MyHttpService.ValidateCertificateRoot(tRequestData);
                    if (TheBaseAssets.MyServiceHostInfo.DisableNMI && !string.IsNullOrEmpty(err))
                    {
                        mContext.Response.StatusCode = (int)eHttpStatusCode.NotAcceptable;
                        mContext.Response.OutputStream.Close();
                        return;
                    }
                }

                if (mContext.Request.InputStream != null)
                {
#if CDE_NET4 || CDE_NET45
                    using (MemoryStream ms = new MemoryStream())
                    {
                        mContext.Request.InputStream.CopyTo(ms);
                        tRequestData.PostData = ms.ToArray();
                    }
#else
                    byte[] buffer = new byte[TheBaseAssets.MAX_MessageSize[0]];
                    using (MemoryStream ms = new ())
                    {
                        int read;
                        while ((read = mContext.Request.InputStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }
                        tRequestData.PostData = ms.ToArray();
                    }
#endif

                    tRequestData.PostDataLength = tRequestData.PostData.Length;
                }

                if (TheCommCore.MyHttpService != null)
                    TheCommCore.MyHttpService.cdeProcessPost(tRequestData);
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4350, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpMidiServer", "ProcessRequest Error:" + firstRequestLine, eMsgLevel.l1_Error, e.ToString()));
                tRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(e.ToString());
                tRequestData.StatusCode = (int)eHttpStatusCode.ServerError;
            }

            if ((tRequestData.ResponseBuffer == null && tRequestData.StatusCode != 200) || tRequestData.StatusCode == 404)    //NEW:UPNP
            {
                tRequestData.ResponseBufferStr = "<html><head><meta http-equiv=\"Expires\" content=\"0\" /><meta http-equiv=\"Cache-Control\" content=\"no-cache\" /><meta http-equiv=\"Pragma\" content=\"no-cache\" /></html><body style=\"background-color: " + TheBaseAssets.MyServiceHostInfo.BaseBackgroundColor + ";\"><table width=\"100%\" style=\"height:100%;\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\"><tr><td style=\"text-align:center;\"><p style=\"color: " + TheBaseAssets.MyServiceHostInfo.BaseForegroundColor + "; font-family: Arial; font-size: 36px\">";
                tRequestData.ResponseBufferStr += string.Format("Resource {0} not found", tRequestData.RequestUri);
                tRequestData.ResponseBufferStr += "</p></td></tr></table></body></HTML>";
                tRequestData.ResponseMimeType = "text/html";
                tRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(tRequestData.ResponseBufferStr);
                tRequestData.StatusCode = (int)eHttpStatusCode.NotFound;
            }

            try
            {
                if (tRequestData.AllowStatePush && tRequestData.ResponseBuffer != null &&
                    (tRequestData.StatusCode == (int)eHttpStatusCode.OK || tRequestData.StatusCode == (int)eHttpStatusCode.PermanentMoved))
                {
                    if (tRequestData.SessionState != null && tRequestData.SessionState.StateCookies != null && tRequestData.SessionState.StateCookies.Count > 0)
                    {
                        string tCookie = "";
                        foreach (string nam in tRequestData.SessionState.StateCookies.Keys)
                        {
                            try
                            {
                                string[] cp = tRequestData.SessionState.StateCookies[nam].Split(';');
                                tCookie =$"{nam}={cp[0]}; SameSite=none; Secure";
                                mContext.Response.Headers.Add(HttpResponseHeader.SetCookie, tCookie);
                            }
                            catch
                            {
                                //ignored
                            }
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(tRequestData.cdeRealPage) && tRequestData.cdeRealPage.StartsWith("/cdeClean", StringComparison.OrdinalIgnoreCase) &&
                        tRequestData.Header != null && tRequestData.Header.ContainsKey("Cookie"))
                    {
                        string cookieDate = DateTime.UtcNow.AddMilliseconds(100).ToString("ddd, dd-MMM-yyyy H:mm:ss"); //Offset not needed
                        string cookieHeader = tRequestData.Header.cdeSafeGetValue("Cookie");
                        if (!string.IsNullOrEmpty(cookieHeader))
                        {
                            string tCookie = "";
                            List<string> tCookies = TheCommonUtils.CStringToList(cookieHeader, ';');
                            foreach (string t in tCookies)
                            {
                                if (tCookie.Length > 0) tCookie += ";";
                                string[] tc = t.Split('=');
                                tCookie += string.Format("{0}=;Path=/;Expires={1} GMT", tc[0], cookieDate);
                            }
                            mContext.Response.Headers.Add(HttpResponseHeader.SetCookie, tCookie);
                        }
                    }
                }

                if (!tRequestData.DontCompress)
                {
                    if (tRequestData.AllowCaching && !TheBaseAssets.MyServiceHostInfo.DisableCache)
                        mContext.Response.AddHeader("Cache-Control", $"max-age={TheBaseAssets.MyServiceHostInfo.CacheMaxAge}, public");
                    else
                        mContext.Response.AddHeader("Cache-Control", "no-cache");
                    mContext.Response.AddHeader("cdeDeviceID", TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString());
                    if (tRequestData.StatusCode > 300 && tRequestData.StatusCode < 400 && tRequestData.Header != null)
                        mContext.Response.AddHeader("Location", tRequestData.Header.cdeSafeGetValue("Location"));
                }
                else
                    mContext.Response.Headers.Clear();

                mContext.Response.Headers.Set(HttpResponseHeader.Server, "C-DEngine V4");
                if (TheBaseAssets.MyServiceHostInfo.IsSSLEnforced)
                {
                    mContext.Response.Headers.Set("Strict-Transport-Security", "max-age=298000; includeSubDomains; preload");   //HSTS Header for SSL sites...test still required
                    mContext.Response.Headers.Set("X-Frame-Options", "sameorigin");   //iFrame protection Header for SSL sites...test still required
                }

                var tCors = TheBaseAssets.MySettings.GetSetting("Access-Control-Allow-Origin");
                if (!string.IsNullOrEmpty(tCors))
                    mContext.Response.AppendHeader("Access-Control-Allow-Origin", tCors);
                if (!string.IsNullOrEmpty(tRequestData.AllowedMethods))
                    mContext.Response.AppendHeader("Access-Control-Allow-Methods", tRequestData.AllowedMethods);
                if (!string.IsNullOrEmpty(tRequestData.AllowedMethods))
                    mContext.Response.AppendHeader("Access-Control-Allow-Headers", tRequestData.AllowedHeaders);

                mContext.Response.StatusCode = tRequestData.StatusCode;
                if (tRequestData.StatusCode != 200)
                {
                    TheCDEKPIs.IncrementKPI(eKPINames.BruteDelay);
                    //Security Fix: ID#770 - wait 200 ms before returning anything with error code to limit BruteForce
                    TheCommonUtils.SleepOneEye(200, 100);
                }
                mContext.Response.ContentType = tRequestData.ResponseMimeType;

                if (tRequestData.ResponseBuffer != null)
                {
                    if (!tRequestData.DontCompress && (TheBaseAssets.MyServiceHostInfo.IsOutputCompressed || (tRequestData.Header != null && tRequestData.Header.ContainsKey("Accept-Encoding") && tRequestData.Header["Accept-Encoding"].Contains("gzip"))))
                    {
                        byte[] bBuffer = TheCommonUtils.cdeCompressBuffer(tRequestData.ResponseBuffer, 0, tRequestData.ResponseBuffer.Length);
                        mContext.Response.AddHeader("Content-Encoding", "gzip");
                        mContext.Response.ContentLength64 = bBuffer.Length;
                        mContext.Response.OutputStream.Write(bBuffer, 0, bBuffer.Length);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(tRequestData.ResponseEncoding))
                        {
                            mContext.Response.AddHeader("Content-Encoding", tRequestData.ResponseEncoding);
                        }
                        mContext.Response.ContentLength64 = tRequestData.ResponseBuffer.Length;
                        mContext.Response.OutputStream.Write(tRequestData.ResponseBuffer, 0, tRequestData.ResponseBuffer.Length);
                    }
                }
                if (tRequestData.DisableChunking)
                    mContext.Response.SendChunked = false;
                if (tRequestData.DisableKeepAlive)
                    mContext.Response.KeepAlive = false;
                mContext.Response.OutputStream.Close();
            }
            catch (Exception ee)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4351, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("HttpMidiServer", "HttpWriteResponse Error", eMsgLevel.l1_Error, ee.ToString()));
            }
        }

        public void ProcessRequest(TcpClient mSocket, cdeMidiHttpService pService)
        {
            mHServer = pService;
            mRequestStream = new BufferedStream(mSocket.GetStream());

            TheRequestData tRequestData = new ();
            try
            {
                parseRequest(tRequestData);
                readHeaders(tRequestData);
                if (tRequestData.HttpVersion > 1.0 && tRequestData.Header.ContainsKey("Expect") && tRequestData.Header["Expect"].Equals("100-Continue"))
                    mRequestStream.Write(mContinue100, 0, mContinue100.Length);//Per HTTP1.1 Requirement
                if (tRequestData.StatusCode != (int)eHttpStatusCode.NotFound)
                {
                    if (tRequestData.HttpMethod.Equals("GET"))
                        TheCommCore.MyHttpService.cdeProcessPost(tRequestData);
                    else if (tRequestData.HttpMethod.Equals("POST"))
                        handlePOST(tRequestData);
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4352, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpMidiServer", "ProcessRequest Error:" + firstRequestLine, eMsgLevel.l1_Error, e.ToString()));
                tRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(e.ToString());
                tRequestData.StatusCode = (int)eHttpStatusCode.ServerError;
            }

            if ((tRequestData.ResponseBuffer == null && tRequestData.StatusCode != 200) || tRequestData.StatusCode == 404)
            {
                tRequestData.ResponseBufferStr = "<html><head><meta http-equiv=\"Expires\" content=\"0\" /><meta http-equiv=\"Cache-Control\" content=\"no-cache\" /><meta http-equiv=\"Pragma\" content=\"no-cache\" /></html><body style=\"background-color: " + TheBaseAssets.MyServiceHostInfo.BaseBackgroundColor + ";\"><table width=\"100%\" style=\"height:100%;\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\"><tr><td style=\"text-align:center;\"><p style=\"color: " + TheBaseAssets.MyServiceHostInfo.BaseForegroundColor + "; font-family: Arial; font-size: 36px\">";
                tRequestData.ResponseBufferStr += string.Format("Resource {0} not found", tRequestData.RequestUri);
                tRequestData.ResponseBufferStr += "</p></td></tr></table></body></HTML>";
                tRequestData.ResponseMimeType = "text/html";
                tRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(tRequestData.ResponseBufferStr);
                if (tRequestData.StatusCode == 0)
                    tRequestData.StatusCode = (int)eHttpStatusCode.NotFound;
            }

            try
            {
                if (!tRequestData.DontCompress && (TheBaseAssets.MyServiceHostInfo.IsOutputCompressed || (tRequestData.Header != null && tRequestData.Header.ContainsKey("Accept-Encoding") && tRequestData.Header["Accept-Encoding"].Contains("gzip"))))
                {
                    byte[] bBuffer = TheCommonUtils.cdeCompressBuffer(tRequestData.ResponseBuffer, 0, tRequestData.ResponseBuffer.Length);
                    tRequestData.ResponseBuffer = bBuffer;
                }
                else
                    tRequestData.DontCompress = true;

                string tHead = TheCommCore.MyHttpService.CreateHttpHeader(tRequestData);
                byte[] tBHead = TheCommonUtils.CUTF8String2Array(tHead);
                mRequestStream.Write(tBHead, 0, tBHead.Length);
                mRequestStream.Write(tRequestData.ResponseBuffer, 0, tRequestData.ResponseBuffer.Length);
                mRequestStream.Flush();
                mRequestStream = null;
                mSocket.Close();
            }
            catch (Exception ee)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4353, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("HttpMidiServer", "HttpWriteResponse Error", eMsgLevel.l1_Error, ee.ToString()));
            }
        }

        private void parseRequest(TheRequestData pReqData)
        {
            firstRequestLine = streamReadLine(mRequestStream);
            string[] tokens = firstRequestLine.Split(' ');
            if (tokens.Length != 3)
                throw new Exception("HttpMidiServer: invalid http request head-line");
            pReqData.HttpMethod = tokens[0].ToUpper();
            pReqData.RequestUri = tokens[1].StartsWith("http", StringComparison.OrdinalIgnoreCase) ? TheCommonUtils.CUri(tokens[1], false) : TheCommonUtils.CUri(TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false) + tokens[1], false);
            pReqData.HttpVersionStr = tokens[2];
            pReqData.HttpVersion = TheCommonUtils.CDbl(pReqData.HttpVersionStr.Split('/')[1]);
        }

        public void readHeaders(TheRequestData pReqData)
        {
            pReqData.Header = new cdeConcurrentDictionary<string, string>();
            string tHeaderLine;
            while ((tHeaderLine = streamReadLine(mRequestStream)) != null && mHServer.IsActive)
            {
                if (string.IsNullOrEmpty(tHeaderLine))
                    break;

                int separator = tHeaderLine.IndexOf(':');
                if (separator == -1)
                    throw new Exception("HttpMidiServer: invalid http header line: " + tHeaderLine);
                string name = tHeaderLine.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < tHeaderLine.Length) && (tHeaderLine[pos] == ' '))
                    pos++; // strip any spaces

                string value = tHeaderLine.Substring(pos, tHeaderLine.Length - pos);
                pReqData.Header.TryAdd(name, value);
            }
        }

        private void handlePOST(TheRequestData pRequest)
        {
            if (pRequest.Header.ContainsKey("Content-Length"))
            {
                using (MemoryStream ms = new ())
                {
                    var tContentLength = TheCommonUtils.CInt(pRequest.Header["Content-Length"]);
                    if (tContentLength > MAX_POST_SIZE)
                    {
                        throw new Exception(
                            String.Format("POST Content-Length({0}) too big. Max: {1}",
                              tContentLength, MAX_POST_SIZE));
                    }
                    byte[] buf = new byte[BUF_SIZE];
                    int to_read = tContentLength;
                    while (to_read > 0 && mHServer.IsActive)
                    {
                        int numread = mRequestStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                        if (numread == 0)
                        {
                            if (to_read == 0)
                                break;
                            else
                                throw new Exception("client disconnected during post");
                        }
                        to_read -= numread;
                        ms.Write(buf, 0, numread);
                    }
                    ms.Seek(0, SeekOrigin.Begin);
                    pRequest.PostData = ms.ToArray();
                    pRequest.PostDataIdx = 0;
                    pRequest.PostDataLength = pRequest.PostData.Length;
                }
            }
            else
                TheBaseAssets.MySYSLOG.WriteToLog(4354, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpMidiServer", "Post without Content-Length!", eMsgLevel.l2_Warning));
            TheCommCore.MyHttpService.cdeProcessPost(pRequest);
        }
    }

}
