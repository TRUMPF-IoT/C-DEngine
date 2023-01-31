// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ISM;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace nsCDEngine.Communication.HttpService
{
    internal class TheHttpService : IHttpInterceptor
    {
        public List<string> GetScopesFromClientCertificate(TheRequestData pReq)
        {
            #region TEST CODE for cert exploration
            string error = ValidateCertificateRoot(pReq);
            var scopeIds = new List<string>();
            if (string.IsNullOrEmpty(error))
            {
                if (pReq.ClientCert != null)
                {
                    if (pReq.ClientCert is X509Certificate2 x509cert)
                    {
                        try
                        {
                            scopeIds = TheCertificates.GetScopesFromCertificate(x509cert, ref error);
                        }
                        catch
                        {
                            error = $"Exception during cert processing for {pReq.RequestUri}";
                        }
                    }
                    else
                    {
                        error = $"Unexpected client certificate type for {pReq.RequestUri}";
                    }
                }
                else
                {
                    error = $"No client cerfificate provided for {pReq.RequestUri}";
                }
            }
            #endregion
            if (error.Length > 0)
                TheBaseAssets.MySYSLOG.WriteToLog(2351, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpService", $"ERROR during Client Cert Processing. Valid Scopes found :{scopeIds?.Count}", eMsgLevel.l1_Error, error));
            return scopeIds;
        }

        public string ValidateCertificateRoot(TheRequestData pReq)
        {
            if (pReq == null)
                return "No Request data given";
            if (pReq.TrustedRoot != null)
            {
                if (pReq.TrustedRoot.Length > 0)
                    return "";
                return "Root Not Trusted per previous check";
            }
            #region CERT CHAIN validation
            string error = "";
            if (TheBaseAssets.MyServiceHostInfo.RequiredClientCertRootThumbprints?.Count > 0) //Dont verify if no Thumbs set
            {
                pReq.TrustedRoot = "";
                if (pReq.ClientCert != null)
                {
                    if (pReq.ClientCert is X509Certificate2 x509cert)
                    {

                        bool isValid = false;
                        var certChainInfo = new List<string>();
                        try
                        {
                            X509Chain chain = new ();
                            X509ChainPolicy chainPolicy = new ()
                            {
                                RevocationMode = X509RevocationMode.NoCheck,
                                RevocationFlag = X509RevocationFlag.EntireChain,
                                //RevocationFlag = X509RevocationFlag.EntireChain,
                                //VerificationFlags= X509VerificationFlags.AllFlags,
                            };
                            chain.ChainPolicy = chainPolicy;
                            if (!chain.Build(x509cert))
                            {
                                error += "Failed to build cerficiate chain.";
                                foreach (var status in chain.ChainStatus)
                                {
                                    error += $"{status.StatusInformation},";
                                }
                                foreach (X509ChainElement chainElement in chain.ChainElements)
                                {
                                    foreach (X509ChainStatus chainStatus in chainElement.ChainElementStatus)
                                    {
                                        error += chainStatus.StatusInformation;
                                    }
                                }
                            }
                            else
                            {
                                var tMsg = $"Chain created with {chain.ChainElements.Count} elements";
                                X509ChainElement lastChainElement = chain.ChainElements.Count > 0 ? chain.ChainElements[chain.ChainElements.Count - 1] : null;
                                foreach (X509ChainElement chainElement in chain.ChainElements)
                                {
                                    string certInfo = "";
                                    certInfo += chainElement.Certificate.Subject;
                                    foreach (X509ChainStatus chainStatus in chainElement.ChainElementStatus)
                                    {
                                        certInfo += $" {chainStatus.StatusInformation}";
                                    }
                                    certChainInfo.Add(certInfo);
                                }
                                if (!string.IsNullOrEmpty(lastChainElement?.Certificate.Thumbprint))
                                {
                                    var rootThumbPrint = lastChainElement.Certificate.Thumbprint;
                                    tMsg += $"Thumbprint: {rootThumbPrint}. List: {TheCommonUtils.CListToString(TheBaseAssets.MyServiceHostInfo.RequiredClientCertRootThumbprints, ";")}";
                                    if (TheBaseAssets.MyServiceHostInfo.RequiredClientCertRootThumbprints?.Contains(lastChainElement.Certificate.Thumbprint.ToUpperInvariant()) == true)
                                    {
                                        isValid = true;
                                        pReq.TrustedRoot = rootThumbPrint;
                                    }
                                    else
                                    {
                                        tMsg += $" Thumbprint {rootThumbPrint} not in configured root thumbprint list.";
                                    }
                                }
                                error += tMsg;
                                TheBaseAssets.MySYSLOG.WriteToLog(2351, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("HttpService", "Certificate Chain status", eMsgLevel.l1_Error, tMsg));
                            }
                        }
                        catch
                        {
                            error = $"Exception during cert processing for {pReq.RequestUri}";
                        }
                        if (!isValid)
                            error = $"Certificate Root not valid for {pReq.RequestUri}";
                    }
                    else
                    {
                        error = $"Unexpected client certificate type for {pReq.RequestUri}";
                    }
                }
                else
                {
                    error = $"No client cerfificate provided for {pReq.RequestUri}";
                }
            }

            #endregion
            return error;
        }

        internal string cdeGetEnginePage(TheRequestData pRequestData)
        {
            string RealPage = pRequestData.RequestUri.LocalPath;
            if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.RootDir) && RealPage.Length > TheBaseAssets.MyServiceHostInfo.RootDir.Length && RealPage.ToUpper().StartsWith(TheBaseAssets.MyServiceHostInfo.RootDir.ToUpper()))
                RealPage = RealPage.Substring(TheBaseAssets.MyServiceHostInfo.RootDir.Length);
            return RealPage;
        }

        public bool cdeProcessPost(TheRequestData tReq)
        {
            string cookieHeader = null;
            if (tReq.Header != null)
            {
                tReq.UserAgent = tReq.Header.cdeSafeGetValue("User-Agent");
                tReq.ResponseMimeType = tReq.Header.cdeSafeGetValue("Content-Type");
                tReq.DeviceID = TheCommonUtils.CGuid(tReq.Header.cdeSafeGetValue("cdeDeviceID"));
                cookieHeader = tReq.Header.cdeSafeGetValue("Cookie");
            }
            if (string.IsNullOrEmpty(tReq.RemoteAddress))
                tReq.RemoteAddress = tReq.DeviceID.ToString();
            tReq.cdeRealPage = cdeGetEnginePage(tReq);
            string cdeRealPageUpper = tReq.cdeRealPage.ToUpperInvariant();
            cdeConcurrentDictionary<string, string> inCookies = new ();
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                List<string> tCookies = TheCommonUtils.CStringToList(cookieHeader, ';');
                foreach (string t in tCookies)
                {
                    string[] tc = t.Split('=');
                    string tVal = (tc.Length > 1) ? tc[1].Trim():"";
                    string tCName = tc[0].Trim();
                    bool DoAdd = true;
                    if (tCName.Equals($"{TheBaseAssets.MyServiceHostInfo.cdeMID}CDESEID"))
                    {
                        Guid ttSID = TheCommonUtils.CGuid(TheCommonUtils.cdeDecrypt(TheCommonUtils.cdeUnescapeString(tVal), TheBaseAssets.MySecrets.GetAI()));  //REMARK: Sometimes an old cookie of CDESEID was not removed from the browser leading the cdeDecrypt to fail (and write an error message to the log...can be ignored)
                        if (tReq.SessionState == null && (tReq.SessionState=TheBaseAssets.MySession.ValidateSEID(ttSID)) == null)   //Measure Frequency !!
                            DoAdd = false;
                    }
                    if (DoAdd)
                        inCookies.TryAdd(tCName, tVal);
                }
            }
            if (!tReq.cdeRealPage.StartsWith("/ISB") && !cdeRealPageUpper.StartsWith("/DEVICE.XML") && !cdeRealPageUpper.StartsWith("/FAVICON.ICO") && !cdeRealPageUpper.StartsWith("/ESP"))
            {
                if (tReq.SessionState == null && TheBaseAssets.MySession.GetSessionState(Guid.Empty, tReq, false)) // IsAB4Interceptor(cdeRealPageUpper)))
                {
                    if (TheBaseAssets.MyServiceHostInfo.ForceWebPlatform != 0)
                        // ReSharper disable once PossibleNullReferenceException
                        tReq.SessionState.WebPlatform = TheBaseAssets.MyServiceHostInfo.ForceWebPlatform;
                    else
                    {
                        if (tReq.SessionState.WebPlatform != 0)
                            tReq.WebPlatform = tReq.SessionState.WebPlatform;
                        else
                            tReq.WebPlatform = TheCommonUtils.GetWebPlatform(tReq.UserAgent);
                        // ReSharper disable once PossibleNullReferenceException
                        tReq.SessionState.WebPlatform = tReq.WebPlatform;
                    }
                    if (tReq.SessionState.StateCookies == null) tReq.SessionState.StateCookies = new cdeConcurrentDictionary<string, string>();
                    foreach (KeyValuePair<String, String> kvp in inCookies.GetDynamicEnumerable())
                    {
                        if (!tReq.SessionState.StateCookies.TryGetValue(kvp.Key, out _))
                            tReq.SessionState.StateCookies.TryAdd(kvp.Key, kvp.Value);
                        else
                            tReq.SessionState.StateCookies[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    if (tReq.SessionState != null)
                    {
                        TheQueuedSenderRegistry.GetSenderByGuid(tReq.SessionState.MyDevice)?.SetLastHeartbeat(tReq.SessionState);
                    }
                }
                if (TheBaseAssets.MyServiceHostInfo.ForceWebPlatform!=0)
                    tReq.WebPlatform = TheBaseAssets.MyServiceHostInfo.ForceWebPlatform;  //REMOVE AFTER MOBILE TESTING
                else
                {
                    if (tReq.SessionState != null)
                        tReq.WebPlatform = tReq.SessionState.WebPlatform;
                }
            }
            tReq.StatusCode = 0;

            ProcessRequest(tReq);
            if (tReq.SessionState != null)
            {
                if (tReq.EndSessionOnResponse)
                    TheBaseAssets.MySession.EndSession(tReq);
                else
                    TheBaseAssets.MySession.SetVer(tReq, false, tReq.AllowStatePush, Guid.Empty);
            }
            if (tReq.StatusCode == 0)
                tReq.StatusCode = (int)eHttpStatusCode.NotFound;
            return true;
        }

        internal static byte[] RenderBrowserConfig()
        {
            string tRet = "<?xml version=\"1.0\" encoding=\"utf-8\"?><browserconfig><msapplication><tile>";
            tRet += string.Format("<TileColor>{0}</TileColor>", TheBaseAssets.MyServiceHostInfo.BaseBackgroundColor);

            tRet += "<TileImage src=\"Images/toplogo-150.png\"/>";
            tRet += "</tile></msapplication></browserconfig>";
            return TheCommonUtils.CUTF8String2Array(tRet);
        }

        internal static void ProcessRequest(TheRequestData pRequestData)
        {
            if (pRequestData == null || pRequestData.RequestUri == null) return;
            try
            {
                pRequestData.AllowStatePush = true;
                bool IsValidISB = false;

                if (pRequestData.HttpMethod == "OPTIONS" && (pRequestData.Header.ContainsKey("Access-Control-Request-Method") || pRequestData.Header.ContainsKey("Access-Control-Request-Headers"))) //New for 4.301: many modern browsers check for preflight premissions see: https://dev.to/effingkay/cors-preflighted-requests--options-method-3024
                {
                    pRequestData.StatusCode = (int)eHttpStatusCode.OK;
                    pRequestData.AllowedMethods = (string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.AccessControlAllowMethods) ? "*" : TheBaseAssets.MyServiceHostInfo.AccessControlAllowMethods);
                    pRequestData.AllowedHeaders = (string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.AccessControlAllowHeaders) ? "*" : TheBaseAssets.MyServiceHostInfo.AccessControlAllowHeaders);
                    pRequestData.AllowStatePush = false;
                    pRequestData.DontCompress = true;
                    return;
                }
                if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.AccessControlAllowMethods) && TheBaseAssets.MyServiceHostInfo.AccessControlAllowMethods.IndexOf(pRequestData.HttpMethod, StringComparison.InvariantCultureIgnoreCase)<0)
                {
                    //All other options are not allowed
                    TheBaseAssets.MySYSLOG.WriteToLog(236, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpService", $"Illegal {pRequestData?.HttpMethod} call to CDE - access was refused", eMsgLevel.l5_HostMessage));
                    pRequestData.StatusCode = (int)eHttpStatusCode.NotAcceptable;
                    pRequestData.AllowStatePush = false;
                    pRequestData.DontCompress = true;
                    return;
                }

                switch (pRequestData.cdeRealPage.ToUpperInvariant())
                {
                    case "/NONE":
                        pRequestData.ResponseBuffer = new byte[1];
                        pRequestData.ResponseBuffer[0] = 0;
                        pRequestData.ResponseMimeType = "text/html";
                        pRequestData.StatusCode = (int)eHttpStatusCode.OK;
                        pRequestData.DontCompress = true;
                        pRequestData.AllowStatePush = false;
                        return;
                    case "/DEVICE.XML": //UPnP Description
                        if (TheBaseAssets.MyApplication.MyCommonDisco != null)
                        {
                            pRequestData.ResponseBuffer = TheBaseAssets.MyApplication.MyCommonDisco.GetDeviceInfo();
                            if (pRequestData.ResponseBuffer == null)
                            {
                                pRequestData.StatusCode = (int)eHttpStatusCode.NotFound;
                                pRequestData.HttpVersionStr = "HTTP/1.0";
                                pRequestData.DontCompress = true;
                                pRequestData.AllowStatePush = false;
                                return;
                            }
                            pRequestData.ResponseMimeType = "text/xml";
                            pRequestData.StatusCode = (int)eHttpStatusCode.OK;
                            pRequestData.HttpVersionStr = "HTTP/1.0";
                            pRequestData.DontCompress = true;
                            pRequestData.AllowStatePush = false;
                            return;
                        }
                        break;
                    case "/BROWSERCONFIG.XML": //UPnP Description
                        if (TheCDEngines.MyNMIService != null)
                        {
                            pRequestData.ResponseBuffer = RenderBrowserConfig();
                            pRequestData.ResponseMimeType = "text/xml";
                            pRequestData.StatusCode = (int)eHttpStatusCode.OK;
                            pRequestData.HttpVersionStr = "HTTP/1.0";
                            pRequestData.DontCompress = true;
                            pRequestData.AllowStatePush = false;
                            return;
                        }
                        break;
                    case "/DEVICEREG.JSON":
                        if (pRequestData.PostData == null || pRequestData.PostData.Length == 0 || TheBaseAssets.MyApplication == null || TheBaseAssets.MyApplication.MyCommonDisco == null) break;
                        TheUPnPDeviceInfo tDeviceData = TheCommonUtils.DeserializeJSONStringToObject<TheUPnPDeviceInfo>(TheCommonUtils.CArray2UTF8String(pRequestData.PostData));
                        if (TheBaseAssets.MyApplication.MyCommonDisco.RegisterDevice(tDeviceData, true))
                        {
                            pRequestData.StatusCode = (int)eHttpStatusCode.OK;
                            pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(TheCommonUtils.SerializeObjectToJSONString(TheBaseAssets.MyApplication.MyCommonDisco.GetTheUPnPDeviceInfo()));
                            pRequestData.ResponseMimeType = "application/json";
                            pRequestData.HttpVersionStr = "HTTP/1.0";
                            pRequestData.DontCompress = true;
                            pRequestData.AllowStatePush = false;
                        }
                        else
                            TheCommCore.SendError("1800:Failed to Register", pRequestData, eHttpStatusCode.NotAcceptable);
                        return;
                    case "/IPXIBOOT.PXI":
                        string pxi = TheBaseAssets.MySettings.GetSetting("PXI" + pRequestData.RemoteAddress);
                        pRequestData.StatusCode = (int)eHttpStatusCode.NotFound;
                        if (!string.IsNullOrEmpty(pxi))
                        {
                            string[] pxiParts = pxi.Split(';');
                            if (pxiParts.Length > 1)
                            {
                                int LanNo = 0; if (pxiParts.Length > 2) LanNo = TheCommonUtils.CInt(pxiParts[2]);
                                pRequestData.ResponseBufferStr = "#!ipxe\n";
                                pRequestData.ResponseBufferStr += string.Format("dhcp net{0}\n", LanNo);
                                pRequestData.ResponseBufferStr += "set initiator-iqn " + pxiParts[0]; 
                                pRequestData.ResponseBufferStr += "\nset root-path iscsi:" + pxiParts[1]; 
                                pRequestData.ResponseBufferStr += "\nset keep-san 1\n";
                                pRequestData.ResponseBufferStr += "sanboot ${root-path}\n";
                                pRequestData.ResponseMimeType = "text/plain";
                                pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(pRequestData.ResponseBufferStr);
                                pRequestData.SessionState = null;
                                pRequestData.StatusCode = (int)eHttpStatusCode.OK;
                                pRequestData.AllowStatePush = false;
                                return;
                            }
                            else
                                return;
                        }
                        else
                            return;
                    case "/CDECLEAN.ASPX":
                        string Query = pRequestData.RequestUri.Query;
                        if (Query.StartsWith("?"))
                            Query = Query.Substring(1);
                        if (string.IsNullOrEmpty(Query)) Query = "NMIPortal";
                        TheBaseAssets.MySession.EndSession(pRequestData);
                        pRequestData.AllowStatePush = false;
                        pRequestData.ResponseMimeType = "text/html";
                        string tRefsh = "";
                        var ts = TheCommonUtils.CInt(TheBaseAssets.MySettings.GetSetting("ReloadAfterLogout"));
                        if (ts > 0)
                            tRefsh = $"<meta http-equiv=\"refresh\" content=\"{ts};URL='{Query}'\" /> ";
                        pRequestData.ResponseBufferStr = $"<html><head><meta http-equiv=\"Expires\" content=\"0\" />{tRefsh}<meta http-equiv=\"Cache-Control\" content=\"no-cache\" /><meta http-equiv=\"Pragma\" content=\"no-cache\" /></html><body style=\"background-color: {TheBaseAssets.MyServiceHostInfo.BaseBackgroundColor};\">";
                        pRequestData.ResponseBufferStr += $"<table width=\"100%\" style=\"height:100%;\" border=\"0\" cellspacing=\"0\" cellpadding=\"0\"><tr><td style=\"text-align:center;\"><p style=\"color: {TheBaseAssets.MyServiceHostInfo.BaseForegroundColor}; font-family: Arial; font-size: 36px\">Your Session has ended</p><p style=\"color: {TheBaseAssets.MyServiceHostInfo.BaseForegroundColor}; font-family: Arial; font-size: 36px\">";
                        pRequestData.ResponseBufferStr += $"<a style=\"color: {TheBaseAssets.MyServiceHostInfo.BaseForegroundColor};\" href=\"{Query}\">Touch here to get back to the Portal</a>";  //TODO: Make Customizable
                        pRequestData.ResponseBufferStr += "</p></td></tr></table></body></HTML>";

                        pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(pRequestData.ResponseBufferStr);
                        pRequestData.SessionState = null;
                        pRequestData.StatusCode = (int)eHttpStatusCode.OK;
                        return;
                }
                if (pRequestData.ResponseBuffer == null)
                    IsValidISB = TheCommCore.ProcessISBRequest(pRequestData);
                if (pRequestData.ResponseBuffer != null && pRequestData.ResponseBuffer.Length > 0)
                {
                    if (string.IsNullOrEmpty(pRequestData.ResponseMimeType))
                        pRequestData.ResponseMimeType = "application/zlib";   //x-gzip
                    if (pRequestData.StatusCode == 0)
                        pRequestData.StatusCode = (int)eHttpStatusCode.OK;
                }
                else if (!string.IsNullOrEmpty(pRequestData.ResponseBufferStr))
                {
                    pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(pRequestData.ResponseBufferStr);
                    if (string.IsNullOrEmpty(pRequestData.ResponseMimeType))
                        pRequestData.ResponseMimeType = "text/html";
                    if (pRequestData.StatusCode == 0)
                        pRequestData.StatusCode = (int)eHttpStatusCode.OK;
                }
                else
                {
                    if (!IsValidISB && pRequestData.StatusCode == 0)
                        GetAnyFile(pRequestData, true);
                    else if (IsValidISB && pRequestData.WebSocket != null && pRequestData.StatusCode == 0)
                        pRequestData.StatusCode = (int)eHttpStatusCode.OK;
                }

                if (pRequestData.ResponseBuffer == null && pRequestData.StatusCode==0)
                    pRequestData.AllowStatePush = true;
                if (string.IsNullOrEmpty(pRequestData.ResponseMimeType))
                    pRequestData.ResponseMimeType = "application/x-gzip";
            }
            catch (Exception ee)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(235, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpService", "ERROR", eMsgLevel.l1_Error, ee.ToString()));
            }
        }


        private static List<IBaseEngine> MySkinProviders = null;
        private static DateTimeOffset LastCdeStatusTime = DateTimeOffset.MinValue;
        private static string LastCdeStatus = null;

        internal static void GetAnyFile(TheRequestData pRequestData, bool ProcessWRIntercept)
        {
            if (pRequestData == null) return; 
            if (pRequestData.cdeRealPage.Length == 1 && !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.DefHomePage))
            {
                pRequestData.cdeRealPage = $"{(TheBaseAssets.MyServiceHostInfo.DefHomePage.StartsWith("/") ? "" : "/")}{TheBaseAssets.MyServiceHostInfo.DefHomePage}";
            }
            if (pRequestData.StatusCode == 0)
                ProcessB4Interceptors(pRequestData, ProcessWRIntercept);
            if (pRequestData.StatusCode == 0 || pRequestData.StatusCode == 404)
            {
                pRequestData.ResponseMimeType = "text/html";
                string Query = pRequestData.RequestUri?.Query;
                if (Query!=null && Query.StartsWith("?"))
                    Query = Query.Substring(1);
                Dictionary<string, string> tQ = TheCommonUtils.ParseQueryString(Query); //DIC-Allowed STRING

                switch (pRequestData.cdeRealPage.ToUpperInvariant())
                {
                    case "/MYISBCONNECT":
                        if (pRequestData.SessionState == null || pRequestData.RequestUri == null || (pRequestData.RequestUri.Host.ToUpperInvariant() != "LOCALHOST" && !TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("AllowRemoteISBConnect"))))
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(235, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpService", $"Illegal access to ISB - access was denied - AllowISBConnect:{TheBaseAssets.MySettings.GetSetting("AllowRemoteISBConnect")}", eMsgLevel.l1_Error));
                            pRequestData.ResponseMimeType = "text/json";
                            pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(TheCommonUtils.SerializeObjectToJSONString<TheISBConnect>(new TheISBConnect() { ERR = "Access Denied" }));
                            pRequestData.AllowStatePush = false;
                            break;
                        }

                        var tRes = TheQueuedSenderRegistry.GetMyISBConnect(pRequestData, tQ,cdeSenderType.CDE_JAVAJASON);
                        if (tRes==null)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(236, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpService", "Illegal access to ISB - access was denied, GetMyISBConnect denied", eMsgLevel.l1_Error));
                            pRequestData.ResponseMimeType = "text/json";
                            pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(TheCommonUtils.SerializeObjectToJSONString<TheISBConnect>(new TheISBConnect() { ERR = "Access Denied" }));
                            pRequestData.AllowStatePush = false;
                            break;
                        }
                        pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(TheCommonUtils.SerializeObjectToJSONString<TheISBConnect>(tRes));
                        pRequestData.ResponseMimeType = "text/json";
                        pRequestData.AllowCaching = false;
                        pRequestData.AllowStatePush = true;
                        break;
                    case "/CDESETPARA.ASPX":
                        if (!IsTokenValid(pRequestData, tQ))
                            break;
                        //Feedback from AxSol Must be
                        if (TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("AllowOnlineHSIChanges")))
                        {
                            TheCDESettings.ParseSettings(tQ, false, true);
                        }
                        pRequestData.AllowStatePush = false;
                        break;
                    case "/CDEDIAG.ASPX":
                        if (!RemoteLoad(pRequestData, tQ))
                        {
                            if (!IsTokenValid(pRequestData, tQ))
                                break;
                            if (tQ != null)
                            {
                                tQ.TryGetValue("FLUSH", out string InKey);
                                if (!string.IsNullOrEmpty(InKey))
                                {
                                    InKey = TheCommonUtils.cdeUnescapeString(InKey);
                                    object MyStorageMirror = TheCDEngines.GetStorageMirror(InKey);
                                    if (MyStorageMirror != null)
                                    {
                                        Type magicType = MyStorageMirror.GetType();
                                        var magicMethod = magicType.GetMethod("FlushCache");
                                        if (magicMethod != null)
                                            magicMethod.Invoke(MyStorageMirror, new object[] { true });

                                    }
                                }
                            }
                            pRequestData.ResponseBufferStr = cdeStatus.GetDiagReport(true);
                            pRequestData.ResponseBufferStr += cdeStatus.AddHTMLFooter;
                            pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(pRequestData.ResponseBufferStr);
                        }
                        pRequestData.ResponseBufferStr = "";
                        pRequestData.ResponseMimeType = "text/html";
                        pRequestData.AllowStatePush = false;
                        break;
                    case "/CDEHSI.ASPX":
                        if (!RemoteLoad(pRequestData, tQ))
                        {
                            if (!IsTokenValid(pRequestData, tQ))
                                break;
                            pRequestData.ResponseBufferStr = cdeStatus.RenderHostServiceInfo(true);
                            pRequestData.ResponseBufferStr += cdeStatus.AddHTMLFooter;
                            pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(pRequestData.ResponseBufferStr);
                        }
                        pRequestData.ResponseBufferStr = "";
                        pRequestData.ResponseMimeType = "text/html";
                        pRequestData.AllowStatePush = false;
                        break;
                    case "/CDEKPIS.ASPX":
                        bool DoReset = false;
                        if (!RemoteLoad(pRequestData, tQ))
                        {
                            if (!IsTokenValid(pRequestData, tQ))
                                break;
                            if (tQ != null)
                            {
                                DoReset = tQ.TryGetValue("RESET", out string InTopicRes);
                            }
                            pRequestData.ResponseBufferStr = cdeStatus.ShowKPIs(DoReset);
                            pRequestData.ResponseBufferStr += cdeStatus.AddHTMLFooter;
                            pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(pRequestData.ResponseBufferStr);
                        }
                        pRequestData.ResponseBufferStr = "";
                        pRequestData.ResponseMimeType = "text/html";
                        pRequestData.AllowStatePush = false;
                        break;
                    case "/CDESTATUS.JSON":
                        {
                            TSM tgetServiceInfo = new (eEngineName.ContentService, "CDE_GET_SERVICEINFO", Query);

                            if (!RemoteLoad(pRequestData, tQ))
                            {
                                if (!IsTokenValid(pRequestData, tQ))
                                    break;
                                TheCDEngines.MyContentEngine.HandleMessage(null, new TheProcessMessage(tgetServiceInfo, (r) =>
                                     {
                                         pRequestData.ResponseBufferStr = r.PLS;

                                     }));
                                pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(pRequestData?.ResponseBufferStr);
                            }
                            pRequestData.ResponseBufferStr = "";
                            pRequestData.ResponseMimeType = "text/json";
                            pRequestData.AllowStatePush = false;
                            break;
                        }
                    case "/CDEKILLNODE.ASPX":
                        {
                            if (!IsTokenValid(pRequestData, tQ))
                                break;
                            if (tQ.ContainsKey("N"))
                            {
                                var t = TheQueuedSenderRegistry.GetSenderByGuid(TheCommonUtils.CGuid(tQ["N"]));
                                if (t != null)
                                    t.DisposeSender(true);
                            }
                            pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array("OK");
                            pRequestData.ResponseBufferStr = "";
                            pRequestData.ResponseMimeType = "text/html";
                            pRequestData.AllowStatePush = false;
                        }
                        break;
                    case "/CDESTATUS.ASPX":
                        {
                            bool Force = false;
                            cdeStatus.TheCdeStatusOptions statusOptions = new ();
                            if (tQ != null)
                            {
                                statusOptions.ShowManyDetails = tQ.ContainsKey("SUBDET");
                                statusOptions.ShowDetails = tQ.ContainsKey("SUBSUM");
                                Force = tQ.ContainsKey("FORCE");
                                statusOptions.ShowQueueContent = tQ.ContainsKey("QUEUE");
                            }
                            if (!RemoteLoad(pRequestData, tQ))
                            {
                                if (!IsTokenValid(pRequestData, tQ))
                                    break;
                                if (TheBaseAssets.MyServiceHostInfo.IsCloudService || Force || DateTimeOffset.Now.Subtract(LastCdeStatusTime).TotalSeconds > 30)
                                {
                                    string InTopic = "";
                                    if (tQ != null)
                                    {
                                        tQ.TryGetValue("FILTER", out InTopic);
                                        statusOptions.Filter = InTopic;
                                        statusOptions.ShowHSI = tQ.ContainsKey("ALL") || tQ.ContainsKey("HSI");
                                        statusOptions.ShowDiag = tQ.ContainsKey("ALL") || tQ.ContainsKey("DIAG");
                                        statusOptions.ShowSesLog = tQ.ContainsKey("ALL") || tQ.ContainsKey("SESLOG");
                                        statusOptions.ShowSysLog = tQ.ContainsKey("ALL") || tQ.ContainsKey("SYSLOG") || !string.IsNullOrEmpty(InTopic);
                                    }

                                    LastCdeStatusTime = DateTimeOffset.Now;
                                    LastCdeStatus = cdeStatus.ShowSubscriptionsStatus(true, statusOptions);
                                    if (statusOptions.ShowHSI)
                                        LastCdeStatus += cdeStatus.RenderHostServiceInfo(false);
                                    if (statusOptions.ShowDiag)
                                    {
                                        sinkGetStatus?.Invoke(pRequestData);
                                        LastCdeStatus += cdeStatus.GetDiagReport(false);
                                    }
                                    if(statusOptions.ShowSesLog)
                                        LastCdeStatus += TheBaseAssets.MySession.GetSessionLog();
                                    if(statusOptions.ShowSysLog)
                                        LastCdeStatus += TheBaseAssets.MySYSLOG.GetNodeLog(pRequestData.SessionState, TheCommonUtils.cdeStripHTML(InTopic), true);
                                    LastCdeStatus += cdeStatus.AddHTMLFooter;
                                }
                                if (LastCdeStatus!=null)
                                    pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(LastCdeStatus);
                            }
                            pRequestData.ResponseBufferStr = "";
                            pRequestData.ResponseMimeType = "text/html";
                            pRequestData.AllowStatePush = false;
                            break;
                        }
                    case "/LOG.ASPX":
                        if (!IsTokenValid(pRequestData, tQ))
                            break;
                        pRequestData.ResponseMimeType = "text/html";
                        pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array("<html><body>" + TheBaseAssets.MySYSLOG.GetNodeLog(pRequestData.SessionState, "", false) + "</body></html>");
                        pRequestData.AllowStatePush = false;
                        break;
                    default:
                        MySkinProviders ??= TheThingRegistry.GetBaseEnginesByCap(eThingCaps.SkinProvider);
                        if (pRequestData.cdeRealPage.Equals("/apple-touch-icon-precomposed.png", StringComparison.OrdinalIgnoreCase))
                            pRequestData.cdeRealPage = "Images/UPNPICON.PNG"; //UPnP Icon
                        else if (pRequestData.cdeRealPage.Equals("/ICON.PNG", StringComparison.OrdinalIgnoreCase))
                            pRequestData.cdeRealPage = "Images/UPNPICON.PNG"; //UPnP Icon
                        if (pRequestData.cdeRealPage.Contains(".") && (pRequestData.cdeRealPage.Length < 4 || (!pRequestData.cdeRealPage.ToUpperInvariant().Contains(".ASPX") && !pRequestData.cdeRealPage.ToUpperInvariant().Contains(".SVC"))))
                        {
                            string htmlname = pRequestData.cdeRealPage.Substring(1, pRequestData.cdeRealPage.Length - 1);
                            pRequestData.ResponseMimeType = "application/x-gzip";
                            bool DoSynchron = false;
                            long tCacheTime = TheBaseAssets.MyServiceHostInfo.TO.GetBlobTimeout;
                            if (htmlname.LastIndexOf('.') >= 0)
                            {
                                string fileEx = htmlname.Substring(htmlname.LastIndexOf('.'), htmlname.Length - htmlname.LastIndexOf('.')).ToUpperInvariant();
                                pRequestData.ResponseMimeType=TheCommonUtils.GetMimeTypeFromExtension(fileEx);
                                switch (fileEx)
                                {
                                    case ".OBJ":
                                    case ".MTL":
                                    case ".STP": //3D Modeling Binary
                                    case ".STEP": //3D Modeling Binary
                                    case ".STL": //3D Modeling Binary
                                        DoSynchron = true;
                                        pRequestData.AllowCaching = true;
                                        tCacheTime = TheBaseAssets.MyServiceHostInfo.CacheMaxAge;
                                        break;
                                    case ".XAML":
                                        pRequestData.ResponseMimeType = "text/xaml";
                                        DoSynchron = true;
                                        tCacheTime = TheBaseAssets.MyServiceHostInfo.CacheMaxAge;
                                        break;
                                    case ".XAP":
                                        pRequestData.ResponseMimeType = "application/x-silverlight-app";
                                        tCacheTime = TheBaseAssets.MyServiceHostInfo.CacheMaxAge;
                                        break;
                                    case ".GIF":
                                    case ".ICO":
                                    case ".JPG":
                                    case ".JPEG":
                                    case ".SVG":
                                    case ".CSS":
                                    case ".PDF":
                                    case ".PNG":
                                    case ".EOT": //Web Fonts
                                    case ".TTF":
                                    case ".OTF":
                                    case ".ZIP":
                                    case ".WAV":
                                    case ".MP3":
                                    case ".WOFF2":
                                    case ".WOFF":
                                        DoSynchron = true;
                                        pRequestData.AllowStatePush = true;
                                        pRequestData.AllowCaching = true;
                                        tCacheTime = TheBaseAssets.MyServiceHostInfo.CacheMaxAge;
                                        break;
                                    case ".JS":
                                        DoSynchron = true;
                                        pRequestData.AllowStatePush = true;
                                        pRequestData.AllowCaching = false;
                                        tCacheTime = TheBaseAssets.MyServiceHostInfo.CacheMaxAge;
                                        break;
                                    case ".MAP":
                                        DoSynchron = true;
                                        pRequestData.AllowStatePush = false;
                                        pRequestData.AllowCaching = false;
                                        pRequestData.StatusCode = (int)eHttpStatusCode.NotFound;
                                        return;
                                    case ".XML":
                                    case "":
                                    case ".JSON":
                                        DoSynchron = true;
                                        tCacheTime = -1;
                                        break;
                                    default:
                                        pRequestData.ResponseMimeType = "text/html";
                                        pRequestData.AllowCaching = true;
                                        tCacheTime = TheBaseAssets.MyServiceHostInfo.CacheMaxAge;
                                        DoSynchron = true;
                                        break;
                                }
                            }
                            if (TheBaseAssets.MyServiceHostInfo.DisableCache) tCacheTime = -1;
                            if (ProcessWRIntercept)
                            {
                                if (MySkinProviders.Any())
                                {
                                    foreach (var tBase in MySkinProviders)
                                    {
                                        pRequestData.ResponseBuffer = tBase?.GetPluginResource(pRequestData.cdeRealPage);
                                        if (pRequestData.ResponseBuffer != null)
                                            break;
                                    }
                                }
                                if (pRequestData.ResponseBuffer == null)
                                    cdeStreamFile(pRequestData, DoSynchron, tCacheTime, Guid.Empty);
                            }
                        }
                        else
                        {
                            if (pRequestData.cdeRealPage.Length > 1 && pRequestData.cdeRealPage.Length < 4)
                            {
                                pRequestData.ResponseMimeType = "text/html";
                                cdeStreamFile(pRequestData, true, TheBaseAssets.MyServiceHostInfo.CacheMaxAge, Guid.Empty);
                            }
                        }
                        break;
                }
                if (pRequestData.ResponseBuffer != null)
                    pRequestData.StatusCode = (int)eHttpStatusCode.OK;
            }
            if (pRequestData.StatusCode == 0 || pRequestData.StatusCode == 408)
                ProcessAfterInterceptors(pRequestData, ProcessWRIntercept);
        }

        internal static bool IsTokenValid(TheRequestData pData, Dictionary<string, string> tQ)
        {
            string token = TheBaseAssets.MySettings.GetSetting("StatusToken");
            if ((string.IsNullOrEmpty(token) && !TheBaseAssets.MyServiceHostInfo.IsCloudService) || (tQ != null && tQ.ContainsKey(token?.ToUpperInvariant()))) //new in 4.0121 - StatusToken in cdeAppConfig blocks cdestatus.aspx access if token was not added to URL
                return true;
            pData.StatusCode = 404;
            pData.ResponseBuffer = TheCommonUtils.CUTF8String2Array("Access denied");
            return false;
        }

        internal static bool RemoteLoad(TheRequestData pRequestData, Dictionary<string, string> tQ)
        {
            if (tQ != null && tQ.ContainsKey("NID") && TheCommonUtils.CGuid(tQ["NID"]) != TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)
            {
                if (tQ.ContainsKey("SID"))
                    pRequestData.SessionState.SScopeID = TheBaseAssets.MyScopeManager.GetScrambledScopeIDFromEasyID(tQ["SID"]);
                else
                    pRequestData.SessionState.SScopeID = TheBaseAssets.MyScopeManager.GetScrambledScopeID();     //GRSI: rare
                cdeStreamFile(pRequestData, true, 10, TheCommonUtils.CGuid(tQ["NID"]),true);
                pRequestData.ResponseBuffer ??= TheCommonUtils.CUTF8String2Array("Node did not respond");
                return true;
            }
            return false;
        }

        internal static void ProcessAfterInterceptors(TheRequestData pRequestData, bool ProcessWRIntercept)
        {
            foreach (string tKey in MyHttpInterceptorsAfter.Keys)
            {
                if ((ProcessWRIntercept || tKey != "/") && pRequestData.cdeRealPage.ToUpperInvariant().StartsWith(tKey.ToUpperInvariant()))
                {
                    MyHttpInterceptorsAfter[tKey]?.Invoke(pRequestData);
                    if (pRequestData.StatusCode != 0) break; //new V3.1 - Verify!
                }
            }
            if (pRequestData.StatusCode == 0 && MyHttpInterceptorsAfter.ContainsKey("/*"))
            {
                MyHttpInterceptorsAfter["/*"]?.Invoke(pRequestData);    //Special case for universal processing after all other pages are done
            }
        }

        internal static void ProcessB4Interceptors(TheRequestData pRequestData, bool ProcessWRIntercept)
        {
            foreach (string tKey in MyHttpInterceptorsBefore.Keys)
            {
                var tPs = TheCommonUtils.cdeSplit(tKey, ":;:", false, false);
                if ((ProcessWRIntercept || tPs[0] != "/") && pRequestData.cdeRealPage.ToUpperInvariant().StartsWith(tPs[0].ToUpperInvariant()))
                {
                    if (tPs.Length > 1)
                    {
                        cdeStreamFile(pRequestData, true, -1, TheCommonUtils.CGuid(tPs[1]), true);
                    }
                    else
                    {
                        MyHttpInterceptorsBefore[tKey]?.Invoke(pRequestData);
                        if (pRequestData.StatusCode != 0) break;
                    }
                }
            }
        }


        #region BLOBLOADERS
        internal static cdeConcurrentDictionary<string, DateTimeOffset> IsStreaming = new ();
        internal static void cdeStreamFile(TheRequestData pRequest, bool IsSynchron, long pCacheTime, Guid pTargetNode, bool IncludeTRD = false)
        {
            if (pRequest.cdeRealPage.Length < 2) return;
            string htmlFile = pRequest.cdeRealPage.Substring(1);
            if (htmlFile.StartsWith("cache", StringComparison.InvariantCultureIgnoreCase)) //No access to cache folder for http streaming
            {
                pRequest.StatusCode = 404;
                return;
            }
            bool JustFetchDontPublish = false;
            bool WasFound = true; 
            var tLastDate = IsStreaming.GetOrAdd(htmlFile, (key) => { WasFound = false; return DateTimeOffset.Now; });
            if (WasFound) 
            {
                if (DateTimeOffset.Now.Subtract(tLastDate).TotalSeconds > TheBaseAssets.MyServiceHostInfo.SessionTimeout)
                    IsStreaming.RemoveNoCare(htmlFile);
                else
                {
                    JustFetchDontPublish = true;  //return; //4.209: return would immediately return 404 to any request coming too fast after the first while its fetched from other nodes. this way this request is waiting for the first to complete and both return
                }
            }
            int SyncFailCount = 0;
            if (TheCDEngines.MyContentEngine == null)
            {
                pRequest.StatusCode = 408;
                return;
            }
            string tSScopeID = "";
            if (pRequest.SessionState?.SScopeID==null && pRequest.RequestUri?.Query?.StartsWith("?SID=")==true)
            {
                var tSessionID=TheBaseAssets.MyScopeManager.GetSessionIDFromISB(pRequest.RequestUri.Query.Substring(5));
                TheBaseAssets.MySession.GetSessionState(TheCommonUtils.CGuid(tSessionID), pRequest, true);
            }
            if (pRequest.SessionState != null) tSScopeID = pRequest.SessionState.SScopeID;
            if (IncludeTRD && string.IsNullOrEmpty(tSScopeID))
                tSScopeID = TheBaseAssets.MyScopeManager.GetScrambledScopeID();     //GRSI: rare
            do
            {
                TheBlobData xapBlob = TheCDEngines.MyContentEngine.GetBlob(htmlFile, tSScopeID,(!JustFetchDontPublish || pCacheTime<0) && (SyncFailCount % TheBaseAssets.MyServiceHostInfo.TO.SFRetryMod) == 0, pCacheTime, pTargetNode, IncludeTRD?pRequest:null);
                if (xapBlob?.BlobData != null)
                {
                    pRequest.ResponseBuffer = xapBlob.BlobData;
                    if (!string.IsNullOrEmpty(xapBlob.MimeType))
                        pRequest.ResponseMimeType = xapBlob.MimeType;
                    IsStreaming.RemoveNoCare(htmlFile);
                    return;
                }
                if (pRequest.StatusCode == 404 || xapBlob?.HasErrors==true)
                {
                    IsStreaming.RemoveNoCare(htmlFile); //4.209: Should release wait for other requests
                    pRequest.StatusCode = 404;
                    TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpService", $"Requesting Blob for ({htmlFile}) was denied: {xapBlob?.ErrorMsg}", eMsgLevel.l2_Warning), true);
                    return;
                }

                if (IsSynchron && pRequest.ResponseBuffer == null)
                {
#if NO_AMAZONDEBUG
                    pRequest.ResponseBuffer = TheCommonUtils.CUTF8String2Array(string.Format("<h1>Failed to find:{0} Request Was:{1}</h1>",pRequest.cdeRealPage,pRequest?.RequestUri));
                    pRequest.ResponseMimeType = "text/html";
                    pRequest.StatusCode = (int)eHttpStatusCode.OK;
#else
                    SyncFailCount++;
                    if (SyncFailCount > TheBaseAssets.MyServiceHostInfo.TO.SFFailCount)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpService", $"Requesting Blob for ({htmlFile}) was not found in mesh", eMsgLevel.l2_Warning), true);
                        break;
                    }
                    TheCommonUtils.SleepOneEye(TheBaseAssets.MyServiceHostInfo.TO.SFWaitTime, 100);
#endif
                }
            } while (IsSynchron && pRequest.ResponseBuffer == null && TheBaseAssets.MasterSwitch);
            IsStreaming.RemoveNoCare(htmlFile); //4.209: Should release wait for other requests
            if (pRequest.ResponseBuffer == null && !IsSynchron)
                pRequest.StatusCode = 408;
        }

        #endregion

        public List<string> GetGlobalScripts()
        {
            return GlobalScripts;
        }

        #region events
        private static Action<TheRequestData> sinkGetStatus;
        private static readonly cdeConcurrentDictionary<string, Action<TheRequestData>> MyHttpInterceptorsBefore = new ();  //DIC-Allowed   STRING
        private static readonly cdeConcurrentDictionary<string, Action<TheRequestData>> MyHttpInterceptorsAfter = new ();   //DIC-Allowed   STRING
        internal static List<string> GlobalScripts = new ();
        public void RegisterGlobalScriptInterceptor(string pUrl, Action<TheRequestData> sinkInterceptHttp)
        {
            if (String.IsNullOrEmpty(pUrl) || sinkInterceptHttp == null)
                return;

            if (MyHttpInterceptorsBefore.ContainsKey(pUrl))
                MyHttpInterceptorsBefore[pUrl] = sinkInterceptHttp;
            else
            {
                if (!MyHttpInterceptorsBefore.TryAdd(pUrl, sinkInterceptHttp))
                    TheBaseAssets.MySYSLOG.WriteToLog(241, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpService", "Error: Adding global script interceptor.", eMsgLevel.l1_Error, pUrl));

                if (!TheBaseAssets.MyServiceHostInfo.IsIsolated)
                {
                    var tPs = TheCommonUtils.cdeSplit(pUrl, ":;:", false, false);
                    var tGSUrl = tPs[0];
                    if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.RootDir))
                        tGSUrl = TheBaseAssets.MyServiceHostInfo.RootDir + tGSUrl;
                    GlobalScripts.Add(tGSUrl);
                }
            }
            if (TheBaseAssets.MyServiceHostInfo.IsIsolated)
                TheQueuedSenderRegistry.PublishToMasterNode(new TSM(eEngineName.ContentService, "CDE_RGSI", pUrl));
        }
        public void RegisterHttpInterceptorB4(string pUrl, Action<TheRequestData> sinkInterceptHttp)
        {
            if (String.IsNullOrEmpty(pUrl) || sinkInterceptHttp == null)
                return;

            // Check for potential conflicts between new URL and existing list of URLs.
            // For exammple: (1) /foo already exists, and (2) /foo/bar is being registered.
            foreach (string strKey in MyHttpInterceptorsBefore.Keys)
            {
                if (IsThereAnyOverlapInURLs(pUrl, strKey))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(239, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpService", "Warning: Possible conflict in URLs pass to http intercept before.", eMsgLevel.l2_Warning, $"InputUrl:{pUrl} ExistingUrl:{strKey}"));
                }
            }

            if (MyHttpInterceptorsBefore.ContainsKey(pUrl))
            {
                MyHttpInterceptorsBefore[pUrl] = sinkInterceptHttp;
                TheBaseAssets.MySYSLOG.WriteToLog(237, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("HttpService", "Warning: Replaced existing http intercept before.", eMsgLevel.l2_Warning, pUrl));
            }
            else
            {
                if (!MyHttpInterceptorsBefore.TryAdd(pUrl, sinkInterceptHttp))
                    TheBaseAssets.MySYSLOG.WriteToLog(238, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpService", "Error: Adding http intercept before.", eMsgLevel.l1_Error, pUrl));
            }
            if (TheBaseAssets.MyServiceHostInfo.IsIsolated)
                TheQueuedSenderRegistry.PublishToMasterNode(new TSM(eEngineName.ContentService, "CDE_RHIB4", pUrl));
        }

        public void UnregisterHttpInterceptorB4(string pUrl)
        {
            if (string.IsNullOrEmpty(pUrl))
                return;

            if (MyHttpInterceptorsBefore.ContainsKey(pUrl))
            {
                MyHttpInterceptorsBefore.TryRemove(pUrl, out _);
            }
            if (TheBaseAssets.MyServiceHostInfo.IsIsolated)
                TheQueuedSenderRegistry.PublishToMasterNode(new TSM(eEngineName.ContentService, "CDE_URHIB4", pUrl));
        }
        public void RegisterHttpInterceptorAfter(string pUrl, Action<TheRequestData> sinkInterceptHttp)
        {
            if (String.IsNullOrEmpty(pUrl) || sinkInterceptHttp == null)
                return;

            // Check for potential conflicts between new URL and existing list of URLs.
            // For exammple: (1) /foo already exists, and (2) /foo/bar is being registered.
            foreach (string strKey in MyHttpInterceptorsAfter.Keys)
            {
                if (IsThereAnyOverlapInURLs(pUrl, strKey))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(242, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpService", "Warning: Possible conflict in URLs pass to http intercept after.", eMsgLevel.l2_Warning, $"InputUrl:{pUrl} ExistingUrl:{strKey}"));
                }
            }

            if (MyHttpInterceptorsAfter.ContainsKey(pUrl))
            {
                MyHttpInterceptorsAfter[pUrl] = sinkInterceptHttp;
                TheBaseAssets.MySYSLOG.WriteToLog(240, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("HttpService", "Warning: Replaced existing http intercept after.", eMsgLevel.l2_Warning, pUrl));
            }
            else
            {
                if (!MyHttpInterceptorsAfter.TryAdd(pUrl, sinkInterceptHttp))
                    TheBaseAssets.MySYSLOG.WriteToLog(241, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpService", "Error: Adding http intercept after.", eMsgLevel.l1_Error, pUrl));
            }
            if (TheBaseAssets.MyServiceHostInfo.IsIsolated)
                TheQueuedSenderRegistry.PublishToMasterNode(new TSM(eEngineName.ContentService, "CDE_RHIA", pUrl));
        }
        public void UnregisterHttpInterceptorAfter(string pUrl)
        {
            if (string.IsNullOrEmpty(pUrl))
                return;

            if (MyHttpInterceptorsAfter.ContainsKey(pUrl))
            {
                MyHttpInterceptorsAfter.TryRemove(pUrl, out _);
            }
            if (TheBaseAssets.MyServiceHostInfo.IsIsolated)
                TheQueuedSenderRegistry.PublishToMasterNode(new TSM(eEngineName.ContentService, "CDE_URHIA", pUrl));
        }

        private static bool IsThereAnyOverlapInURLs(string strPath1, string strPath2)
        {
            bool b1 = false;
            bool b2 = false;

            if (strPath1 != null && strPath2 != null && strPath1 != "/" && strPath2 != "/")
            {
                // Make sure the paths end in underscore,
                // otherwise Uri.IsBaseOf returns false positives.
                if (!strPath1.EndsWith("/")) strPath1 += "/";
                if (!strPath2.EndsWith("/")) strPath2 += "/";

                try
                {
                    Uri u1 = new("http://localhost" + strPath1);
                    Uri u2 = new("http://localhost" + strPath2);

                    b1 = u1.IsBaseOf(u2);
                    b2 = u2.IsBaseOf(u1);
                }
                catch
                { 
                    //ignored
                }
            }

            return (b1 || b2);
        }

        public void RegisterStatusRequest(Action<TheRequestData> sinkGetStat)
        {
            if (sinkGetStat == null)
                return;

            sinkGetStatus += sinkGetStat;
        }
        #endregion

        #region Utils
        public string CreateHttpHeader(TheRequestData pRequestData)
        {
            if (pRequestData == null) return String.Empty;

            string status = " 404 Not Found";
            string injectHeader = "";
            switch ((eHttpStatusCode)pRequestData.StatusCode)
            {
                case eHttpStatusCode.Continue: status = " 100 CONTINUE";
                    break;
                case eHttpStatusCode.OK: status = " 200 OK";
                    break;
                case eHttpStatusCode.NotAcceptable: status = " 406 NOTACCEPTABLE";
                    break;
                case eHttpStatusCode.NotFound:
                    pRequestData.ResponseMimeType = "text/html";
                    break;
                case eHttpStatusCode.PermanentMoved:
                    status = " 302 Redirect";
                    injectHeader = !string.IsNullOrEmpty(pRequestData.NewLocation) ? string.Format("Location: {0}\r\n", pRequestData.NewLocation) : string.Format("Location: {0}\r\n", pRequestData.RequestUri);
                    break;
                default:
                    status = string.Format(" {0} Error", pRequestData.StatusCode.ToString());
                    break;
            }

            if (string.IsNullOrEmpty(pRequestData.ResponseMimeType)) pRequestData.ResponseMimeType = "text/html";
            if (string.IsNullOrEmpty(pRequestData.HttpVersionStr)) pRequestData.HttpVersionStr = "HTTP/1.1";
            string tFormat = "{0}{1}\r\n"
                             + $"Server: C-Labs.C-DEngine/{TheBaseAssets.MyServiceHostInfo.CurrentVersion}\r\n"
                             + "{3}"
                             + "X-Powered-By: C-DEngine\r\n"
                             + "cdeDeviceID: {4}\r\n"
                             + "Content-Type: {2}\r\n";
            if (pRequestData.DontCompress)
                tFormat = "{0}{1}\r\n"
                             + $"Server: C-Labs.C-DEngine/{TheBaseAssets.MyServiceHostInfo.CurrentVersion}\r\n"
                             + "{3}"
                    //+ "X-Powered-By: C-DEngine\r\n"
                             + "cdeDeviceID: {4}\r\n"
                             + "Content-Type: {2}\r\n";
            string response = string.Format(tFormat,
                             pRequestData.HttpVersionStr, status, pRequestData.ResponseMimeType, injectHeader, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID);
            if (pRequestData.ResponseBuffer != null && pRequestData.ResponseBuffer.Length > 0)
                response += string.Format("Content-Length: {0}\r\n", pRequestData.ResponseBuffer.Length);
            if (pRequestData.AllowCaching && !TheBaseAssets.MyServiceHostInfo.DisableCache)
                response += $"Cache-Control: max-age={TheBaseAssets.MyServiceHostInfo.CacheMaxAge}, public\r\n";
            else
                response += "Cache-Control: no-cache\r\n";
            if (!pRequestData.DontCompress && (TheBaseAssets.MyServiceHostInfo.IsOutputCompressed || (pRequestData.Header?.ContainsKey("Accept-Encoding") == true && pRequestData.Header["Accept-Encoding"].Contains("gzip"))))
                response += "Content-Encoding: gzip\r\n";

            var cors = TheBaseAssets.MySettings.GetSetting("Access-Control-Allow-Origin");
            if (!string.IsNullOrEmpty(cors))
                response += $"Access-Control-Allow-Origin: {cors}\r\n";

            if (pRequestData.HttpVersion > 1.0)
                response += "Connection: close\r\n";
            if ((pRequestData.StatusCode == (int)eHttpStatusCode.OK || pRequestData.StatusCode == (int)eHttpStatusCode.PermanentMoved) && pRequestData.SessionState != null && pRequestData.SessionState.StateCookies != null && pRequestData.SessionState.StateCookies.Count > 0)
            {
                string cookieHeader = pRequestData.Header?.cdeSafeGetValue("Cookie");
                List<string> inCookies = new();
                if (!string.IsNullOrEmpty(cookieHeader))
                {
                    inCookies = TheCommonUtils.CStringToList(cookieHeader, ';');
                    for (int i = 0; i < inCookies.Count; i++)
                        inCookies[i] = inCookies[i].Split('=')[0].Trim();
                }
                foreach (string tKey in pRequestData.SessionState.StateCookies.Keys)
                {
                    if (inCookies.Contains(tKey.Trim(), StringComparer.OrdinalIgnoreCase)) continue;
                    string[] cp = pRequestData.SessionState.StateCookies[tKey.Trim()].Split(';');
                    var outCookies = tKey.Trim();
                    outCookies += "=" + cp[0].Trim();
                    response += string.Format("Set-Cookie: {0}\r\n", outCookies);
                }
            }
            response += "\r\n";
            return response;
        }

        #endregion

    }
}
