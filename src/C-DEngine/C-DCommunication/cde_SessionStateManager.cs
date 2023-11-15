// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines;
using nsCDEngine.Security;
using System.Linq;
using System.Text;

namespace nsCDEngine.Communication
{
    internal class TheSessionStateManager
    {
        public TheSessionStateManager(TheServiceHostInfo pSiteID)
        {
            MySite = pSiteID;
            MySessionStates = new TheStorageMirror<TheSessionState>(TheCDEngines.MyIStorageService) { IsRAMStore = true, BlockWriteIfIsolated = true };
            MySessionStates.SetRecordExpiration(TheBaseAssets.MyServiceHostInfo.SessionTimeout, sinkExpired);
            MySessionStates.InitializeStore(true);
        }

        private readonly TheServiceHostInfo MySite;
        private readonly TheStorageMirror<TheSessionState> MySessionStates;
        private bool IsStoreInitialized;

        public void InitStateManager()
        {
            if (!IsStoreInitialized && TheCDEngines.MyIStorageService != null)
            {
                IsStoreInitialized = true;
                MySessionStates.SetStorageService(TheCDEngines.MyIStorageService);
                MySessionStates.CreateStore(TheBaseAssets.MyServiceHostInfo.ApplicationName + ": Session State", "Log of all Session Activities", null, true, false);
            }
        }

        internal long GetSessionCount()
        {
            if (MySessionStates == null)
                return 0;
            return MySessionStates.Count;
        }


        internal TheSessionState CreateSession(TheRequestData pRequest, Guid pSessionID)
        {
            string strAcceptLanguage = null;
            int strLCID = 0;
            string localIP = "";
            try
            {
                if (pRequest != null && pRequest.ServerTags != null)
                    strAcceptLanguage = pRequest.ServerTags.cdeSafeGetValue("HTTP_ACCEPT_LANGUAGE");
                if (string.IsNullOrEmpty(strAcceptLanguage))
                    strAcceptLanguage = "en-us";
            }
            catch //Exception e)
            {
                strAcceptLanguage = "en-us";
            }


            try
            {
                localIP = Discovery.TheNetworkInfo.GetIPAddress(false).ToString();
            }
            catch {
                //ignored
            }
            string tTRGTSRV = MySite.TrgtSrv;
            try
            {
                string debRGTSRV = MySite.DebTrgtSrv;
                if (!string.IsNullOrEmpty(debRGTSRV) && !string.IsNullOrEmpty(localIP) && (localIP.Substring(0, 7).Equals("192.168") || localIP.Substring(0, 5).Equals("127.0")))
                    tTRGTSRV = debRGTSRV;
            }
            catch {
                //ignored
            }

            if (strAcceptLanguage.Length > 10) strAcceptLanguage = strAcceptLanguage.Substring(0, 10);

            TheSessionState pSession = new ();
            if (pSessionID != Guid.Empty)
                pSession.cdeMID = pSessionID;
            try
            {
                if (pRequest != null)
                {
                    if (pRequest.ServerTags != null)
                    {
                        pSession.InitReferer = pRequest.ServerTags.cdeSafeGetValue("HTTP_REFERER");
                        pSession.UserAgent = pRequest.ServerTags.cdeSafeGetValue("ALL_HTTP");
                        foreach (string t in s_HeaderItems)
                        {
                            pSession.SiteName = pRequest.ServerTags.cdeSafeGetValue(t);
                            if (!string.IsNullOrEmpty(pSession.SiteName))
                                break;
                        }
                    }
                    pSession.Browser = pRequest.BrowserType;
                    pSession.BrowserDesc = pRequest.BrowserPlatform;
                    pSession.ScreenWidth = pRequest.BrowserScreenWidth;
                    if (!string.IsNullOrEmpty(pRequest.RemoteAddress))
                        pSession.RemoteAddress = pRequest.RemoteAddress;
                    else
                        pSession.RemoteAddress = pRequest.DeviceID.ToString();
                    if (string.IsNullOrEmpty(pSession.SiteName) && pRequest.DeviceID != Guid.Empty)
                        pSession.SiteName = pRequest.DeviceID.ToString();
                    pSession.MyDevice = pRequest.DeviceID;
                    if (string.IsNullOrEmpty(pSession.InitReferer) && pRequest.Header != null)
                    {
                        pSession.InitReferer = pRequest.Header.cdeSafeGetValue("Referer");
                        if (string.IsNullOrEmpty(pSession.TETO) && pSession.InitReferer?.Length>1)
                        {
                            string[] tP = pSession.InitReferer.Split('?');
                            if (tP?.Length > 1 && tP[1].StartsWith("TETO="))
                            {
                                var qs = tP[1].Substring(5).Split('&');
                                pSession.TETO = qs[0];
                            }
                        }
                    }
                }
                pSession.EntryTime = DateTimeOffset.Now;
                pSession.LastAccess = DateTimeOffset.Now;
                pSession.PageHits = 1;
                pSession.TRGTSRV = tTRGTSRV;
                pSession.LCID = strLCID;
                pSession.BrowserLanguage = strAcceptLanguage.ToLower();
                TheCommCore.eventNewSessionCreated?.Invoke(pSession);
            }
            catch (Exception obErr)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(1232, new TSM("SSM", "<HR>Create-Session: Exception occured:", obErr.ToString()));
            }
            return pSession;
        }

        // order is in trust/use order top to bottom
        private static readonly List<string> s_HeaderItems = new () {
            "REMOTE_ADDR",
            "HTTP_CLIENT_IP",
            "HTTP_X_FORWARDED_FOR",
            "HTTP_X_FORWARDED",
            "HTTP_X_CLUSTER_CLIENT_IP",
            "HTTP_FORWARDED_FOR",
            "HTTP_FORWARDED",
            "HTTP_VIA",
            };


        internal TheSessionState ValidateSEID(Guid pSEID)
        {
            if (pSEID == Guid.Empty) return null;
            var tS=MySessionStates.MyMirrorCache.GetEntryByID(pSEID);
            if (tS?.HasExpired==true)
                tS = null;
            return tS;
        }

        internal bool UpdateSessionLCID(Guid pSessionID, int pLCID)
        {
            lock (GetLock()) //LOCK-REVIEW: This is more a logic lock then read/write RecordsLock)    //low impact
            {
                TheSessionState tSess = ValidateSEID(pSessionID);   //Low Frequency
                if (tSess != null)
                {
                    tSess.LCID = pLCID;
                    MySessionStates.UpdateItem(tSess, null);
                    return true;
                }
            }
            return false;
        }

        internal TheSessionState GetSessionIDByDeviceID(Guid pDeviceID)
        {
            if (pDeviceID == Guid.Empty) return null;
            List<TheSessionState> tList = MySessionStates.MyMirrorCache.GetEntriesByFunc(a => a.MyDevice.Equals(pDeviceID));
            if (tList.Count > 1)
                TheBaseAssets.MySYSLOG.WriteToLog(1236, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("SSM", $"GetSession By Device Too Many Found {tList.Count}", eMsgLevel.l6_Debug));
            if (tList.Count > 0)
                return tList[0];
            return null;
        }
        internal TheSessionState[] GetSessionsIDByDeviceID(Guid pDeviceID)
        {
            if (pDeviceID == Guid.Empty) return null;
            return MySessionStates.MyMirrorCache.GetEntriesByFunc(a => a.MyDevice.Equals(pDeviceID)).ToArray();
        }
        internal List<TheSessionState> ExpireSessionByDeviceID(Guid pDeviceID, Guid pExceptID)
        {
            return MySessionStates?.MyMirrorCache?.GetEntriesByFunc(a => a.MyDevice.Equals(pDeviceID) && (pExceptID == Guid.Empty || a.cdeMID!=pExceptID)).Select(c => { c.HasExpired = true; return c; }).ToList();
        }
        internal TheSessionState GetOrCreateSessionState(Guid pSessionID, TheRequestData pRequest)
        {
            TheSessionState tS = ValidateSEID(pSessionID);  //Measure Frequency!!
            tS ??= CreateSession(pRequest, pSessionID);
            return tS;
        }

        internal bool GetSessionState(Guid pSessionID, TheRequestData pRequestData, bool NoCreate)
        {
            if (pRequestData == null) return false;
            bool IsNewState = false;
            try
            {
                if (Guid.Empty != pSessionID)
                    pRequestData.SessionState = ValidateSEID(pSessionID);   //Measure Frequency
                if (pRequestData.SessionState == null)
                {
                    pRequestData.SessionState = GetSessionIDByDeviceID(pRequestData.DeviceID);
                    if (pRequestData.SessionState == null && !NoCreate)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(1234, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("SSM", $"SESS: Creating new Session for {pRequestData.cdeRealPage}"));
                        pRequestData.SessionState = CreateSession(pRequestData, pSessionID);
                        if (pRequestData.SessionState.StateCookies == null) pRequestData.SessionState.StateCookies = new cdeConcurrentDictionary<string, string>();
                        if (pRequestData.SessionState.StateCookies.ContainsKey(MySite.cdeMID.ToString() + "CDESEID"))
                            pRequestData.SessionState.StateCookies[MySite.cdeMID.ToString() + "CDESEID"] = TheCommonUtils.cdeEscapeString(TheCommonUtils.cdeEncrypt(TheCommonUtils.CStr(pRequestData.SessionState.cdeMID), TheBaseAssets.MySecrets.GetAI()));   //3.083: Must use SecureID
                        else
                            pRequestData.SessionState.StateCookies.TryAdd(MySite.cdeMID.ToString() + "CDESEID", TheCommonUtils.cdeEscapeString(TheCommonUtils.cdeEncrypt(TheCommonUtils.CStr(pRequestData.SessionState.cdeMID), TheBaseAssets.MySecrets.GetAI()))); //3.083: Must use SecureID
                        WriteSession(pRequestData.SessionState);
                        IsNewState = true;
                    }
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(1233, new TSM("SSM", "<HR>GetSessionState: Exception occured:", e.ToString()));
            }
            return IsNewState;
        }

        internal void EndSession(TheRequestData pRequestData)
        {
            if (pRequestData.SessionState == null || pRequestData.SessionState.HasExpired) return;
            KillSession(pRequestData.SessionState);
        }


        internal int RemoveSessionsByDeviceID(Guid pID, Guid exeptID)
        {
            var tList = ExpireSessionByDeviceID(pID, exeptID);
            if (tList == null) return 0;
            int tCnt = tList.Count;
            if (tCnt > 0)
            {

                MySessionStates.RemoveItems(tList, null);
                if (tCnt > 0)
                    TheBaseAssets.MySYSLOG.WriteToLog(1234, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("SSM", $"Removed {tCnt} Sessions for DeviceID:{pID}", eMsgLevel.l3_ImportantMessage));
                return tList.Count;
            }
            return 0;
        }
        internal void KillSession(TheSessionState SessionState)
        {
            if (SessionState == null) return;
            RemoveSession(SessionState);
            sinkExpired(SessionState);
        }


        internal void RemoveSessionByID(Guid pSessionID)
        {
            if (Guid.Empty == pSessionID) return;
            MySessionStates.RemoveAnItemByID(pSessionID, null);
        }
        internal void RemoveSession(TheSessionState SessionState)
        {
            if (SessionState == null) return;
            SessionState.HasExpired = true;
            SessionState.EndTime = DateTimeOffset.Now;
            SessionState.ARApp = Guid.Empty;
            TheBaseAssets.MySYSLOG.WriteToLog(1234, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("SSM", $"Session killed at {TheCommonUtils.GetDateTimeString(DateTimeOffset.Now)} for RemoteAdr: {TheCommonUtils.GetDeviceIDML(SessionState.MyDevice)}", eMsgLevel.l3_ImportantMessage));
            TheCommonUtils.cdeRunAsync("RemoveSession", true, (o) => MySessionStates.RemoveAnItem(SessionState, null)); //Do not block the main thread...take your time HasExpired is set :)
        }

        internal void sinkExpired(TheSessionState pState)
        {
            if (pState != null)
            {
                if (pState.CID != Guid.Empty)
                    TheUserManager.RemoveTempUser(pState.CID);
                TheQueuedSender tQSend = TheQueuedSenderRegistry.GetSenderBySEID(pState.cdeMID);
                if (tQSend != null)
                    tQSend.FireSenderProblem(new TheRequestData() {ErrorDescription = "1500:Session for this QSender expired"});
            }
        }

        internal void SetVer(TheRequestData pRequestData, bool tSessionReset, bool UpdateLog, Guid WPID)
        {
            TheSessionState pSession = pRequestData?.SessionState;
            if (pSession == null || pSession.HasExpired) return;
            try
            {
                if (tSessionReset)
                {
                    lock (pSession.MySessionLock)
                    {
                        pSession.MembUID = "";
                        pSession.MembPWD = "";
                        pSession.MembFullName = "";
                        pSession.Status = "N";
                        pSession.CID = Guid.Empty;
                        pSession.Debug = 0;
                        pSession.MembLevel = 0;
                        pSession.PUIDHigh = 0;
                        pSession.PUIDLow = 0;
                        //<Fix>3.5.202,CM,5/20/2004,Vertical Application must be reset as well</Fix>
                        pSession.CustomDataLng = 0;
                        pSession.LCID = 0;
                        WriteSession(pSession);
                    }
                }
                else
                {
                    if (UpdateLog)
                    {
                        pSession.LastAccess = DateTimeOffset.Now;
                        pSession.cdeCTIM = DateTimeOffset.Now;
                        if (TheCommonUtils.cdeIsLocked(pSession.MySessionLock)) // Is writing already...dont do it again
                            return;
                        lock (pSession.MySessionLock)   //CODE-REVIEW: Is this really necessarry?
                        {
                            pSession.CurrentWPID = WPID;
                            pSession.Status = "A";
                            if (pRequestData.ServerTags != null)
                                pSession.InitReferer = pRequestData.ServerTags.cdeSafeGetValue("HTTP_REFERER");
                            if (string.IsNullOrEmpty(pSession.InitReferer) && pRequestData.Header != null)
                                pSession.InitReferer = pRequestData.Header.cdeSafeGetValue("REFERER");
                            if (string.IsNullOrEmpty(pSession.InitReferer) && !string.IsNullOrEmpty(pSession.CurrentURL))
                                pSession.InitReferer = pSession.CurrentURL;
                            string gstrCurURL = pRequestData.RequestUri.LocalPath + "/" + pRequestData.RequestUri.Query;
                            pSession.CurrentURL = gstrCurURL;
                            if (string.IsNullOrEmpty(pSession.InitReferer))
                            {
                                try
                                {
                                    //CM:4.105 No idea why this exists - must be legacy code
                                    ////if (pSession.StateCookies == null) pSession.StateCookies = new cdeConcurrentDictionary<string, string>()
                                    ////string tref = TheCommonUtils.cdeEncrypt(TheCommonUtils.CStr(gstrCurURL), TheBaseAssets.MySecrets.GetAI())  //3.083: Must use AI
                                    ////pSession.StateCookies.TryAdd(MySite.cdeMID.ToString() + "CDEREF", TheCommonUtils.cdeEscapeString(tref))
                                    pSession.InitReferer = TheCommonUtils.CStr(gstrCurURL);
                                }
                                catch
                                {
                                    //ignored
                                }
                            }
                            WriteSession(pSession);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(1235, new TSM("SSM", "<HR>SetVer: Exception occurred:", e.ToString()));
            }
        }

        internal void UpdateSessionID(TheSessionState pState, Guid pNewID)
        {
            MySessionStates.UpdateID(pState.cdeMID, pNewID);
        }

        private readonly object MySessionStateLock = new ();
        internal object GetLock()
        {
            return MySessionStateLock; //LOCK-REVIEW: no longer in StorageMirror - SessionManager has its own lock now
        }

        public void LogSession(Guid pSessionID, string pUrl, string pBrowser, string pBrowserDesc, string pRef, string pCustomData)
        {
            if (TheCDEngines.MyIStorageService == null) return;
            TheSessionState pSess = new ()
            {
                cdeMID = pSessionID,
                CurrentURL = pUrl,
                Browser = pBrowser,
                LastAccess = DateTimeOffset.Now,
                BrowserDesc = pBrowserDesc,
                CustomData = pCustomData,
                RunID = pSessionID,
                InitReferer = pRef
            };
            pSess.PageHits++;
            TheCDEngines.MyIStorageService.EdgeDataStoreOnly(pSess, null);
        }
        public void WriteSession(TheSessionState pSession)
        {
            if (pSession == null || pSession.HasExpired)    //we should never write an expired Session
                return;
            try
            {
                pSession.LastAccess = DateTimeOffset.Now;
                pSession.cdeCTIM = DateTimeOffset.Now;
                pSession.PageHits++;
                MySessionStates.UpdateItem(pSession, null);
#if CDE_LOGSESSIONS
                if (TheCDEngines.MyStorageService != null)
                {
                    //LogSession(pSession.cdeMID, pSession.CurrentURL, pSession.Browser, pSession.BrowserDesc, pSession.InitReferer, pSession.CustomData);
                    TheBaseAssets.MySYSLOG.WriteToLog(1236, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("SSM", "WriteRession: Writing ", eMsgLevel.l6_Debug, pSession.ToString()));
                }
#endif
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(1237, new TSM("SSM", "WriteRession: Exception occured:", eMsgLevel.l2_Warning, e.ToString()));
            }
        }

        public string GetSessionLog()
        {
            StringBuilder retString =new($"<div class=\"cdeInfoBlock\" style=\"clear:both; max-width:1570px; width:initial; \"><div class=\"cdeInfoBlockHeader cdeInfoBlockHeaderText\" id=\"sessionLog\">Session Log: ({MySessionStates.Count}) ");
            retString.Append($"<a download =\"SessionLog_{TheCommonUtils.GetMyNodeName()}.csv\" href=\"#\" class=\'cdeExportLink\' onclick=\"return ExcellentExport.csv(this, 'sessionLogTable');\">(Export as CSV)</a></div>");
            List<string> tSessions = MySessionStates.TheKeys;
            string tFont;
            int count = 0;
            foreach (string tKey in tSessions)
            {
                if (count == 0)
                {
                    retString.Append($"<table class=\"cdeHilite\" style=\"width:95%; margin-left:1%;\" id=\"sessionLogTable\"><tr>");
                    retString.Append($"<th style=\"background-color:rgba(90,90,90, 0.25);font-size:x-small;min-width:175px; width:300px;\">SEID</th>");
                    retString.Append($"<th style=\"background-color:rgba(90,90,90, 0.25);font-size:x-small;min-width:80px; width:80px;\">DID</th>");
                    retString.Append($"<th style=\"background-color:rgba(90,90,90, 0.25);font-size:x-small;min-width:80px; width:150px;\">Last Access</th>");
                    retString.Append($"<th style=\"background-color:rgba(90,90,90, 0.25);font-size:x-small;min-width:80px; width:150px;\">End</th>");
                    retString.Append($"<th style=\"background-color:rgba(90,90,90, 0.25);font-size:x-small;width:100px;\">Browser</th>");
                    retString.Append($"<th style=\"background-color:rgba(90,90,90, 0.25);font-size:x-small;width:200px;\">Current Url</th>");
                    retString.Append($"<th style=\"background-color:rgba(90,90,90, 0.25);font-size:x-small;width:300px\">Remote Address</th>");
                    retString.Append($"<th style=\"background-color:rgba(90,90,90, 0.25);font-size:x-small;min-width:80px; width:80px;\">CID</th>");
                    retString.Append($"<th style=\"background-color:rgba(90,90,90, 0.25);font-size:x-small;min-width:80px; width:80px;\">SID</th>");
                    retString.Append($"<th style=\"background-color:rgba(90,90,90, 0.25);font-size:x-small;width:400px;\">User Agent</th>");
                    retString.Append("</tr>");
                }
                count++;
                TheSessionState tSession = MySessionStates.MyMirrorCache.MyRecords[tKey];
                if (tSession.MyDevice == Guid.Empty)
                    tFont = " style='color:lightgray'";
                else
                    tFont = "";
                retString.Append($"<tr {tFont}>");
                retString.Append($"<td class=\"cdeClip cdeLogEntry\" style=\"text-align:left;\">{tSession.cdeMID}</td>");
                retString.Append($"<td class=\"cdeClip cdeLogEntry\" style=\"text-align:left;\">{TheCommonUtils.GetDeviceIDML(tSession.MyDevice)}</td>");
                retString.Append($"<td class=\"cdeClip cdeLogEntry\" style=\"text-align:center;\">{TheCommonUtils.GetDateTimeString(tSession.LastAccess,-1)}</td>");
                retString.Append($"<td class=\"cdeClip cdeLogEntry\" style=\"text-align:center;\">{TheCommonUtils.GetDateTimeString(tSession.EndTime,-1)}</td>");
                retString.Append($"<td class=\"cdeClip cdeLogEntry\" style=\"text-align:left;\">{tSession.Browser} {tSession.BrowserDesc}</td>");
                retString.Append($"<td class=\"cdeClip cdeLogEntry\" style=\"max-width:200px; width: 200px;\"><div class=\"cdeClip\" style=\"max-height:100px; overflow-y:auto;\">{tSession.CurrentURL}</div></td>");
                retString.Append($"<td class=\"cdeClip cdeLogEntry\" style=\"text-align:left;\">{tSession.RemoteAddress}</td>");
                retString.Append($"<td class=\"cdeClip cdeLogEntry\" style=\"text-align:left;\">{(tSession.CID==Guid.Empty?"Not Set":tSession.CID.ToString().Substring(0,4))}</td>");
                retString.Append($"<td class=\"cdeClip cdeLogEntry\" style=\"text-align:left;\">{(string.IsNullOrEmpty(tSession.SScopeID) ? "Not Set" : cdeStatus.GetSScopeHashML(tSession.SScopeID))}</td>");
                retString.Append($"<td class=\"cdeClip cdeLogEntry\" style=\"max-width:400px; width: 400px;\"><div class=\"cdeClip\" style=\"max-height:100px; overflow-y:auto;\">{tSession.UserAgent}</div></td>");
                retString.Append("</tr>");
            }
            if (count > 0)
            {
                retString.Append("</table></div>");
                return retString.ToString();
            }
            return "";
        }

        internal List<Guid> GetCurrentUsers()
        {
            List<Guid> tUsers = new ();
            List<string> tSessions = MySessionStates?.TheKeys;
            if (tSessions == null || tSessions.Count == 0) return new();
            foreach (string tKey in tSessions)
            {
                TheSessionState tSession = MySessionStates.MyMirrorCache.MyRecords[tKey];
                if (tSession.CID != Guid.Empty && !tUsers.Contains(tSession.CID))
                {
                    tUsers.Add(tSession.CID);
                }
            }
            return tUsers;
        }

        public List<string> GetSessionLogList()
        {
            List<TheSessionState> tSessions = MySessionStates.TheValues;
            List<string> loglist = new ();
            foreach (TheSessionState tSession in tSessions)
            {
                loglist.Add(tSession.ToString());
            }
            return loglist;
        }
    }
}
