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
using System.Reflection;
using System.Text;

namespace nsCDEngine.Communication
{
    internal static class cdeStatus
    {
        public static Action<Action<string>> eventEventLogRequested;

        private static string sLastCustomText = "";
        private static void sinkAddCustomText(string pText)
        {
            sLastCustomText = pText;
        }

        private static string AddHTMLHeader()
        {
            string outText = "<html><head>";
            outText += TheBaseAssets.MyServiceHostInfo.GetMeta("");
            if (TheThingRegistry.IsEngineStarted("NMIService.TheNMIHtml5RT", false))
            {
                outText += "<link rel=\"stylesheet\" type=\"text/css\" href=\"css/cdeStyles.min.css\" />";
                outText += "<link rel=\"stylesheet\" type=\"text/css\" href=\"css/MyStyles.min.css\" />";
                outText += "<script src=\"cdeSorttable.js\"></script>";
                outText += "<script src=\"excellentexport.js\"></script>";
                outText += "<script>window.jdenticon_config = { replaceMode: \"observe\" }; </script>";
                outText += "<script src=\"jdenticon-2.2.0.min.js\"></script>";
            }
            else
            {
                outText += "<style>";
                outText += "table.cdeHilite {font-family: 'robotothin','Segoe UI',Arial,sans-serif;margin: 1em;border-collapse: collapse;font-weight: normal;text-decoration: none;}";
                outText += ".cdeLogEntry {padding: .1em;border-bottom-style: solid;border-bottom-width: 1px;}";
                outText += ".cdeClip {overflow: hidden;text-overflow: ellipsis;}";
                outText += ".cdeClip:hover { overflow: visible;text-overflow: initial;}";
                outText += "</style>";
            }
            outText += "<title>" + TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false) + " - System Log</title>";
            outText += "</head><body class=\"cdeLogBody\">\r";
            return outText;
        }
        internal static string AddHTMLFooter()
        {
            return "</body></html>";
        }

        internal static string GetScopeHashML(List<string> pScopes)
        {
            if (pScopes?.Count < 1)
                return "";
            string ret = "";
            foreach (var hash in pScopes)
            {
                ret += $"{GetScopeHashML(hash, null)} ";
            }
            return ret;
        }

        internal static string GetScopeHashML(string hash, string AddToSpan = null)
        {
            if (!TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog)
                return hash;
            //return $"<span onclick=\"if (cdeCSOn) cdeCSOn=null; else cdeCSOn=true;\" onmouseenter=\"cdeColorSpan(this, 'red')\" onmouseleave=\"cdeColorSpan(null)\"  class=\"cdeRSHash\" style='color:orange; font-weight:bold; {(string.IsNullOrEmpty(AddToSpan) ? "" : AddToSpan)}'>{hash}</span>";
            return $"<span onclick=\"if (cdeCSOn) cdeCSOn=null; else cdeCSOn=true;\" onmouseenter=\"cdeColorSpan(this, 'red')\" onmouseleave=\"cdeColorSpan(null)\"  class=\"cdeRSHash\" style='color:orange; font-weight:bold; {(string.IsNullOrEmpty(AddToSpan) ? "" : AddToSpan)}'>{hash}<svg width=\"20\" height=\"20\" data-jdenticon-value=\"{hash}\"></svg></span>";
        }
        internal static string GetSScopeHashML(string sScope, string AddToSpan = null)
        {
            var hash = TheBaseAssets.MyScopeManager.GetRealScopeID(sScope).Substring(0, 4).ToUpper();
            if (!TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog)
                return hash;
            //return $"<span onclick=\"if (cdeCSOn) cdeCSOn=null; else cdeCSOn=true;\" onmouseenter=\"cdeColorSpan(this, 'red')\" onmouseleave=\"cdeColorSpan(null)\"  class=\"cdeRSHash\" style='color:orange; font-weight:bold; {(string.IsNullOrEmpty(AddToSpan) ? "" : AddToSpan)}'>{hash}</span>";
            return $"<span onclick=\"if (cdeCSOn) cdeCSOn=null; else cdeCSOn=true;\" onmouseenter=\"cdeColorSpan(this, 'red')\" onmouseleave=\"cdeColorSpan(null)\"  class=\"cdeRSHash\" style='color:orange; font-weight:bold; {(string.IsNullOrEmpty(AddToSpan) ? "" : AddToSpan)}'>{hash}<svg width=\"20\" height=\"20\" data-jdenticon-value=\"{hash}\"></svg></span>";
        }

        internal static TheNodeInfoHeader GetInfoHeaderJSON()
        {
            var tHead = new TheNodeInfoHeader {
                CloudRoutes = new List<TheServiceRouteInfo>(),
                NodeID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                NodeName = TheBaseAssets.MyServiceHostInfo.NodeName,
                BuildVersion = TheBaseAssets.BuildVersion,
                CurrentVersion = TheBaseAssets.CurrentVersionInfo,
                ScopeHash = TheBaseAssets.MyScopeManager.IsScopingEnabled ? TheBaseAssets.MyScopeManager.ScopeID.Substring(0, 4).ToUpper() : "unscoped",
                OSInfo = TheBaseAssets.MyServiceHostInfo.OSInfo
            };
            var CNs = TheQueuedSenderRegistry.GetCloudNodes(true);
            if (CNs.Count > 0)
            {
                foreach (var tQ in CNs)
                {
                    var tInfo = new TheServiceRouteInfo
                    {
                        Url = tQ.MyTargetNodeChannel.TargetUrl,
                        IsConnected = tQ.IsConnected,
                        IsConnecting = tQ.IsConnecting,
                        LastError = tQ.GetLastError(),
                        ErrorCode = tQ.GetLastErrorCode(),
                        LastHB=(tQ.GetLastHeartBeat() == DateTimeOffset.MinValue ? "not yet" : $"{TheCommonUtils.GetDateTimeString(tQ.GetLastHeartBeat(), -1)}"),
                        ConnectedSince = (tQ.MyLastConnectTime == DateTimeOffset.MinValue ? "not yet" : $"{TheCommonUtils.GetDateTimeString(tQ.MyLastConnectTime, -1)}"),
                        IsAlive=tQ.IsAlive,
                        UsesWS=tQ.HasWebSockets()
                    };
                    tInfo.Status = $"{tInfo.Url} (Connected: {(tInfo.IsConnected ? "Yes, all is good" : (tInfo.IsConnecting ? "Not, yet - trying to connect" : "No, and not yet trying to connect again!"))}{(!string.IsNullOrEmpty(tInfo.LastError) ? $" Last Error: {tInfo.LastError}" : "")})";
                    tHead.CloudRoutes.Add(tInfo);
                }
            }
            return tHead;
        }
        internal static string InfoHeader(bool IsSmall)
        {
            //string ret = $"<h3>{TheBaseAssets.MyServiceHostInfo.MyAppPresenter}</h3><h1>{TheBaseAssets.MyServiceHostInfo.ApplicationTitle} V{TheBaseAssets.MyServiceHostInfo.CurrentVersion}</h1><h3>Hosted at: {TheBaseAssets.LocalHostQSender.MyTargetNodeChannel}<br/>NodeName: {TheBaseAssets.MyServiceHostInfo.NodeName}</br>AppName:{TheBaseAssets.MyServiceHostInfo.ApplicationName}<br/>lcd: {TheBaseAssets.MyServiceHostInfo.BaseDirectory}</br>CDE:V{TheBaseAssets.CurrentVersionInfo} Drop: {TheBaseAssets.BuildVersion}</br>Started:{TheCommonUtils.GetDateTimeString(TheBaseAssets.MyServiceHostInfo.cdeCTIM, -1)}";
            string ret2 = $"<h3>{TheBaseAssets.MyServiceHostInfo.MyAppPresenter}</h3>"; //<div id=\"mainBox\" style=\"width-fixed:90%;display:flex; flex-flow: column wrap\">
            ret2 += $"<h1 id=\"title\">{TheBaseAssets.MyServiceHostInfo.ApplicationTitle} V{TheBaseAssets.MyServiceHostInfo.CurrentVersion}</h1>";
            if (TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog)
                ret2 += $"<script>var cdeCSOn; function cdeColorSpan(pContent, pColor) {{ var x = document.getElementsByClassName(\"cdeRSHash\");var i;for (i = 0; i < x.length; i++){{ if (!pContent) {{ if (!cdeCSOn) x[i].style.backgroundColor = \"transparent\"; }} else {{ if (pContent.innerText==x[i].innerText && !cdeCSOn) x[i].style.backgroundColor = pColor; }}}}}}</script>";
            ret2 += $"<div class=\"cdeInfoBlock\" style=\"width:750px;\"><div class=\"cdeInfoBlockHeader cdeInfoBlockHeaderText\" id=\"nodeInfo\">Node Info</div><table style=\"margin-left:2%\">";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right; min-width:200px;\">Hosted at:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{TheBaseAssets.LocalHostQSender.MyTargetNodeChannel.ToMLString(true)}</td></tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">Node ID:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{TheCommonUtils.GetDeviceIDML(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)}</td><tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">Node Name:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{TheBaseAssets.MyServiceHostInfo.NodeName}</td><tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">Station Name:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{TheBaseAssets.MyServiceHostInfo.MyStationName}</td><tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">Application Name:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{TheBaseAssets.MyServiceHostInfo.ApplicationName}</td><tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">OS Info:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left;word-wrap: break-word;max-width:540px \">{TheBaseAssets.MyServiceHostInfo.OSInfo}</td><tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">Local Dir:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{TheBaseAssets.MyServiceHostInfo.BaseDirectory}</td><tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">CDE Info:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{TheBaseAssets.CurrentVersionInfo}</td><tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">CDE Drop:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">V{TheBaseAssets.BuildVersion}</td><tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">CDE Crypto:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{TheBaseAssets.MySecrets.CryptoVersion()}</td><tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">Unique Meshes Connected:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{TheQueuedSenderRegistry.GetUniqueMeshCount()}</td><tr>";
            string tScope = "";
            if (TheBaseAssets.MyScopeManager.IsScopingEnabled)
            {
                tScope = GetScopeHashML(TheBaseAssets.MyScopeManager.GetScopeHash(null));
            }
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">Local Node-Scope:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{(TheBaseAssets.MyScopeManager.IsScopingEnabled ? $"A local Scope is set. Hash: {tScope}" : "Scoping is disabled")}</td><tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">Client Cert Thumbprint:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{(TheBaseAssets.MyServiceHostInfo.ClientCertificateThumb?.Length > 3 ? $"{TheBaseAssets.MyServiceHostInfo.ClientCertificateThumb.Substring(0, 4)}" : $"No client certificate specified")}</td><tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">Node Started at:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{TheCommonUtils.GetDateTimeString(TheBaseAssets.MyServiceHostInfo.cdeCTIM, -1)}</td><tr>";
            ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">Last Update at:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{TheCommonUtils.GetDateTimeString(DateTimeOffset.Now, -1)}</td><tr>";
            ret2 += "</table>";

            if (!IsSmall)
            {
                ret2 += $"<h2>Service Routes:</h2><table style=\"margin-left:2%\">";
                ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right; width:200px;\">Configured Routes:</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">{(string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ServiceRoute) ? "No Routes are configured" : $"{TheBaseAssets.MyServiceHostInfo.ServiceRoute}")}</td></tr>";

                var CNs = TheQueuedSenderRegistry.GetCloudNodes(true);
                if (CNs.Count > 0)
                {
                    foreach (var tQ in CNs)
                        ret2 += $"<tr><th scope=\"rowgroup;\" style=\"background-color:rgba(90,90,90, 0.15); padding:3px; text-align:right\">{tQ.MyTargetNodeChannel.TargetUrl}</th><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);padding:3px;text-align:left \">Connected: {(tQ.IsConnected ? "Yes, all is good" : (tQ.IsConnecting ? "Not, yet - trying to connect" : "No, and not trying to connect!"))}{(!string.IsNullOrEmpty(tQ.GetLastError())?$" Last Error: {tQ.GetLastError()}":"")}</td><tr>";
                }
                ret2 += "</table>";
            }
            ret2 += "</div>";
            return ret2;
        }

        internal static string ShowKPIs(bool DoReset)
        {
            string outText = AddHTMLHeader();

            outText += InfoHeader(true);
            outText += "<br>" + TheCDEKPIs.GetKPIs(DoReset).Replace(" ", "</br>") + "</hr>";
            return outText;
        }

        private static readonly object lockSubscribers = new object();

        internal static bool GetSubscriptionStatusWithPublicChannelInfo(out Dictionary<string, List<Guid>> channelsByTopic, out List<TheChannelInfo> channels)
        {
            channelsByTopic = null;
            channels = null;
            if (TheCommonUtils.cdeIsLocked(lockSubscribers))
            {
                //outText += "<h2>Subscription Status currently locked...try again in a couple seconds</h2><ul>";
                return false;
            }
            channelsByTopic = new Dictionary<string, List<Guid>>();
            channels = new List<TheChannelInfo>();
            lock (lockSubscribers)
            {
                List<TheSubscriptionInfo> allTopics = TheQueuedSenderRegistry.GetCurrentKnownTopics(false, "");
                //allTopics.Sort();

                foreach (var mkey in allTopics)
                {
                    var tCurTopic = mkey.Topic;
                    var tLst = TheQueuedSenderRegistry.GetPublicChannelInfoForTopic(mkey, false);
                    foreach (var channel in tLst)
                    {
                        if (!channelsByTopic.ContainsKey(tCurTopic))
                        {
                            channelsByTopic.Add(tCurTopic, new List<Guid> { channel.cdeMID });
                        }
                        else
                        {
                            channelsByTopic[tCurTopic].Add(channel.cdeMID);
                        }
                        if (!channels.Contains(channel))
                        {
                            channels.Add(channel);
                        }
                    }
                }
            }
            return true;
        }

        internal static string ShowSubscriptionsStatus(bool AddHeader, TheCdeStatusOptions statusOptions)
        {
            string headerInfo = "";
            if (AddHeader)
                headerInfo += AddHTMLHeader();
            if (TheCommonUtils.cdeIsLocked(lockSubscribers))
            {
                headerInfo += "<h2>Subscription Status currently locked...try again in a couple seconds</h2><ul>";
                return headerInfo;
            }
            lock (lockSubscribers)
            {
                headerInfo += InfoHeader(false);
                if (eventEventLogRequested != null)
                {
                    eventEventLogRequested(sinkAddCustomText);
                    if (!string.IsNullOrEmpty(sLastCustomText))
                        headerInfo += sLastCustomText;
                }

                StringBuilder outText = new StringBuilder("");
                outText.Append($"<div class=\"cdeInfoBlock\" style=\"width:750px;\"><div class=\"cdeInfoBlockHeader cdeInfoBlockHeaderText\" id=\"engineStatuses\">Status of Engines on this Node</div><table class=\"cdeHilite\" style=\"border:1px solid gray;margin-left:2%\">");
                outText.Append("<tr><th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:350px; min-width:350px\">Plugin Name</th>");
                outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">Version</th>");
                outText.Append("<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; width:80px;\">Status</th></tr>");

                int cnt = 0;
                foreach (TheThing tThing in TheThingRegistry.GetBaseEnginesAsThing(false, true))
                {
                    outText.Append($"<tr {(cnt % 2 == 0 ? "style=\"background-color:rgba(90,90,90, 0.15);\"" : "")}>");
                    if (tThing.GetObject() is ICDEPlugin tPlugin)
                    {
                        IBaseEngine tBase = tPlugin.GetBaseEngine();
                        string col = "orange";
                        var tState = tBase.GetEngineState();
                        if (tState?.IsInitialized == true) col = "black";
                        if (tState?.IsEngineReady == true) col = "green";
                        if (tState?.IsMiniRelay == true) col = "purple";
                        if (tState?.IsDisabled == true) col = "lightgray";
                        outText.Append($"<th style=\"padding:3px; text-align:right; width:400px;\"><span style='color:{col}'>{tBase.GetEngineName()}</span></th>");
                        outText.Append($"<td style=\"padding:3px; text-align:left; width:80px;\"><span style='color:{col}'>V{tBase.GetVersion()}</span></td>");
                        outText.Append($"<td style=\"padding:3px; text-align:left; width:150px;\"><span style='color:{col}'>{(!string.IsNullOrEmpty(tState?.LastMessage) ? tState?.LastMessage : (tState?.IsDisabled == true ? "Disabled" : (tState?.IsMiniRelay == true ? "Relay Only" : (col == "orange" ? "Not Ready!" : "ok"))))}</span></td>");
                    }
                    else
                    {
                        outText.Append($"<th style=\"padding:3px; text-align:right; width:200px;\"><span style='color:red'>{tThing.FriendlyName}</span></th>");
                        outText.Append($"<td style=\"padding:3px; text-align:left;\" colspan=\"2\"><span style='color:red'>plugin missing!</span></td>");
                    }
                    cnt++;
                    outText.Append("</tr>");
                }
                outText.Append("</table></div>");
                if (statusOptions.ShowDetails || statusOptions.ShowManyDetails)
                {
                    var curSubByTopicStr = "<div class=\"cdeInfoBlock\" style=\"clear:left; width:750px;\"><div class=\"cdeInfoBlockHeader cdeInfoBlockHeaderText\" id=\"subscribersByTopic\">Current Subscribers by Topic</div><ul>";
                    List<TheSubscriptionInfo> allTopics = TheQueuedSenderRegistry.GetCurrentKnownTopics(false, "").OrderBy(s => s.Topic).ToList();

                    List<string> tScopes = new List<string> { "" };
                    string tLastTopic = "";
                    var c1 = 0;
                    foreach (var mkey in allTopics)
                    {
                        List<string> tLst = TheQueuedSenderRegistry.GetUrlForTopic(mkey, false);
                        var tCurTopic = mkey.Topic;
                        if (tCurTopic != tLastTopic)
                        {
                            if (tLastTopic != "")
                                curSubByTopicStr += "</li>";
                            curSubByTopicStr += "<li>" + tCurTopic;
                            tLastTopic = tCurTopic;
                        }
                        if (string.IsNullOrEmpty(mkey.RScopeID))
                            curSubByTopicStr += "<ul><li><span style='color:purple'>Not Scoped</span>";
                        else
                        {
                            string tScop = mkey.RScopeID;
                            if (!tScopes.Contains(tScop))
                                tScopes.Add(tScop);
                            if (tScop.Length < 4)
                                curSubByTopicStr += $"<ul><li><span style='color:red'>ILLEGAL-Scope: {tScop.ToUpper()}</span>";
                            else
                                curSubByTopicStr += $"<ul><li>{GetScopeHashML(tScop.Substring(0, 4).ToUpper())}";
                        }
                        if (tLst != null && tLst.Count > 0)
                        {
                            curSubByTopicStr += "<ul>";
                            foreach (string tUrl in tLst)
                            {
                                curSubByTopicStr += "<li>" + tUrl + "</li>";
                            }
                            curSubByTopicStr += "</ul>";
                        }
                        curSubByTopicStr += "</li></ul>";
                        c1++;
                    }
                    if (c1 > 0)
                    {
                        curSubByTopicStr += "</ul></div>";
                        outText.Append(curSubByTopicStr);
                    }

                    var curScopesAndSubs = "<div class=\"cdeInfoBlock\" style=\"width:750px; \"><div class=\"cdeInfoBlockHeader cdeInfoBlockHeaderText\" id=\"scopesAndSubscribers\">Current Scopes and its subscribers</div><ul>";
                    int c2 = 0;
                    foreach (string mkey in tScopes)
                    {
                        curScopesAndSubs += $"<li><span style='color:orange'>{(string.IsNullOrEmpty(mkey) ? "Unscoped Nodes" : GetScopeHashML(mkey.Substring(0, 4).ToUpper()))}</span>";
                        List<TheChannelInfo> tlst = TheQueuedSenderRegistry.GetSendersByRealScope(mkey);
                        if (tlst.Count > 0)
                        {
                            curScopesAndSubs += "<ul>";
                            foreach (TheChannelInfo tChan in tlst)
                                curScopesAndSubs += "<li>" + tChan.ToMLString() + "</li>";
                            curScopesAndSubs += "</ul>";
                        }
                        c2++;
                    }
                    if (c2 > 0)
                    {
                        curScopesAndSubs += "</ul></div>";
                        outText.Append(curScopesAndSubs);
                    }
                }

                statusOptions.SenderTypes = new List<cdeSenderType>(); // Sender Types with at least one connected node of this type
                foreach(cdeSenderType senderType in senderTypes)
                {
                    string nodes = TheQueuedSenderRegistry.GetNodesStats(senderType, statusOptions.ShowDetails, statusOptions.ShowQueueContent);
                    if(!string.IsNullOrEmpty(nodes))
                    {
                        statusOptions.SenderTypes.Add(senderType);
                        outText.Append(nodes);
                    }
                }
                outText.Insert(0, headerInfo + CreateTableLinks(statusOptions));
                return outText.ToString();
            }
        }

        // Possible Sender Types to display in cdeStatus tables (does not include all enum values)
        private static List<cdeSenderType> senderTypes = new List<cdeSenderType>
        {
            cdeSenderType.NOTSET, cdeSenderType.CDE_LOCALHOST, cdeSenderType.CDE_CLOUDROUTE,
            cdeSenderType.CDE_BACKCHANNEL, cdeSenderType.CDE_DEVICE, cdeSenderType.CDE_JAVAJASON,
            cdeSenderType.CDE_PHONE, cdeSenderType.CDE_SERVICE, cdeSenderType.CDE_CUSTOMISB
        };

        // Wrapper class for all of the cdeStatus options
        internal class TheCdeStatusOptions
        {
            public bool ShowDetails { get; set; }
            public bool ShowManyDetails { get; set; }
            public bool ShowQueueContent { get; set; }
            public bool ShowHSI { get; set; }
            public bool ShowDiag { get; set; }
            public bool ShowSesLog { get; set; }
            public bool ShowSysLog { get; set; }
            public List<cdeSenderType> SenderTypes { get; set; }
            public string Filter { get; set; }
        }

        internal static string CreateTableLinks(TheCdeStatusOptions statusOptions)
        {
            StringBuilder outText = new StringBuilder("<div class=\"cdeInfoBlock\" style=\"width:750px; min-height:initial;\"><div class=\"cdeInfoBlockHeader cdeInfoBlockHeaderText\">Table Links</div>");
            outText.Append("<ul><li><a href=\"#nodeInfo\">Node Info</a></li>");
            outText.Append("<li><a href=\"#engineStatuses\">Status of Engines on this Node</a></li>");
            if (statusOptions.ShowDetails || statusOptions.ShowManyDetails)
            {
                outText.Append("<li><a href=\"#subscribersByTopic\">Current Subscribers by Topic</a></li>");
                outText.Append("<li><a href=\"#scopesAndSubscribers\">Current Scopes and its Subscribers</a></li>");
            }
            foreach(cdeSenderType senderType in statusOptions.SenderTypes)
            {
                string prefix = TheQueuedSenderRegistry.GetPrefixForSenderType(senderType);
                outText.Append($"<li><a href=\"#{prefix.Replace(' ', '-').Replace('=', '-')}\">{prefix}</a></li>");
            }
            if(statusOptions.ShowHSI)
                outText.Append("<li><a href=\"#hostServiceInfo\">HostServiceInfo (HSI) of this Node</a></li>");
            if (statusOptions.ShowDiag)
            {
                outText.Append("<li><a href=\"#threadInfo\">Thread Info</a></li>");
                outText.Append("<li><a href=\"#loadedModules\">Loaded Modules</a></li>");
                outText.Append("<li><a href=\"#storageMirrors\">Storage Mirrors</a></li>");
            }
            if(statusOptions.ShowSesLog)
                outText.Append("<li><a href=\"#sessionLog\">Session Log</a></li>");
            if(statusOptions.ShowSysLog)
                outText.Append("<li><a href=\"#systemLog\">Current SystemLog</a></li>");

            outText.Append("</ul></div>");
            return outText.ToString();
        }

        internal static string GetDiagReport(bool ShowHeader)
        {
            string outText = "";

            if (ShowHeader)
            {
                outText += AddHTMLHeader();
                outText += InfoHeader(false);
            }

            string tColor = "black";
            outText += "<div class=\"cdeInfoBlock\" style=\"width:1570px;\"><div class=\"cdeInfoBlockHeader cdeInfoBlockHeaderText\" id=\"threadInfo\">Thread Info ";
            outText += $"<a download =\"ThreadInfo_{TheCommonUtils.GetMyNodeName()}.csv\" href=\"#\" class=\'cdeExportLink\' onclick=\"return ExcellentExport.csv(this, 'threadInfoTable');\">(Export as CSV)</a></div>";
            outText += "<table class=\"cdeHilite\" id=\"threadInfoTable\" width=\"95%\"><tbody>";
            List<TheThreadInfo> tThreads = TheDiagnostics.GetThreadInfo();
            outText += "<tr><td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Thread Name</td>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Thread ID</td>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>State</td>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Wait Reason</td>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Start Time</td>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>User time in ms</td>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Core time in ms</td>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Priority</td>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Is Background</td>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Is Pooled</td></tr>";
            foreach (TheThreadInfo t in tThreads.OrderByDescending(s => s.UserTimeInMs))
            {
                switch (t.State.ToLower())
                {
                    case "waiting":
                        tColor = "Green";
                        break;
                    case "running":
                        tColor = "red";
                        break;
                    case "ready":
                        tColor = "purple";
                        break;
                    default:
                        tColor = "Black";
                        break;
                }
                string tn = t.Name; if (string.IsNullOrEmpty(tn)) tn = "Unknown";
                if (tn.ToLower().Equals("cdemain thread")) tColor = "pink";
                outText += string.Format("<tr><td class='cdeLogEntry' style='color:{0}'>{1}</td>", tColor, tn);
                outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1}</td>", tColor, t.ID);
                outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1}</td>", tColor, t.State);
                outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1}</td>", tColor, t.WaitReason);
                outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1}</td>", tColor, t.StartTime);
                outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1:0}</td>", tColor, t.UserTimeInMs);
                outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1:0}</td>", tColor, t.CoreTimeInMs);
                outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1}</td>", tColor, t.Priority);
                outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1}</td>", tColor, t.IsBackground);
                outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1}</td></tr>", tColor, t.IsPooled);
                outText += string.Format("<tr><td colspan=\"10\" class='cdeLogEntry' style='color:{0}'><SMALL>{1}</SMALL></td></tr>", tColor, t.StackFrame);
                outText += "</tr>";
            }
            outText += "</tbody></table></div>";

            List<TheModuleInfo> tList = TheDiagnostics.GetLoadedModules(0);
            outText += "<div class=\"cdeInfoBlock\"  style=\"width:750;\"><div class=\"cdeInfoBlockHeader cdeInfoBlockHeaderText\" id=\"loadedModules\">Loaded Modules ";
            outText += $"<a download =\"LoadedModules_{TheCommonUtils.GetMyNodeName()}.csv\" href=\"#\" class=\'cdeExportLink\' onclick=\"return ExcellentExport.csv(this, 'loadedModulesTable');\">(Export as CSV)</a></div>";
            outText += "<table class=\"cdeHilite\" id=\"loadedModulesTable\" width=\"95%\"><tbody>";
            outText += "<tr><th class='cdeLogEntryHeader' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Module Name</th>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Memory Size</td></tr>";
            foreach (TheModuleInfo t in tList)
            {
                outText += string.Format("<tr><th class='cdeLogEntryHeader' style='color:{0}'>{1}</th>", tColor, t.Name);
                outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1}</td>", tColor, t.MemorySize);
                outText += "</tr>";
            }
            outText += "</tbody></table></div>";

            outText += "<div class=\"cdeInfoBlock\" style=\"width:1550;\"><div class=\"cdeInfoBlockHeader cdeInfoBlockHeaderText\" id=\"storageMirrors\">Storage Mirrors ";
            outText += $"<a download =\"StorageMirrors_{TheCommonUtils.GetMyNodeName()}.csv\" href=\"#\" class=\'cdeExportLink\' onclick=\"return ExcellentExport.csv(this, 'storageMirrorsTable');\">(Export as CSV)</a></div>";
            outText += "<table class=\"cdeHilite\" id=\"storageMirrorsTable\" width=\"95%\"><tbody>";
            outText += "<tr><th class='cdeLogEntryHeader' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;font-color:black'>Mirror ID</th>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black;text-align:center;max-width:100px'>Row count</td>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Size in Bytes</td>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Cache Store Cnt</td>";
            outText += "<td class='cdeLogEntry' style='font-weight:bold;font-variant:small-caps;text-decoration:underline;color:black'>Last Cache Store</td></tr>";
            foreach (string key in TheCDEngines.MyStorageMirrorRepository.Keys)
            {
                outText += string.Format("<tr><th class='cdeLogEntryHeader' style='color:{0}'>{1}", tColor, key);


                object MyStorageMirror = TheCDEngines.GetStorageMirror(key);
                if (MyStorageMirror != null)
                {
                    Type magicType = MyStorageMirror.GetType();
                    MethodInfo magicMethod = null;
                    bool StoreCanBeFlushed = false;
                    magicMethod = magicType.GetMethod("StoreCanBeFlushed");
                    if (magicMethod != null)
                        StoreCanBeFlushed = TheCommonUtils.CBool(magicMethod.Invoke(MyStorageMirror, null));
                    magicMethod = magicType.GetMethod("GetFriendlyName");
                    if (magicMethod != null)
                    {
                        string tUpdated = magicMethod.Invoke(MyStorageMirror, null).ToString();
                        if (!string.IsNullOrEmpty(tUpdated))
                            outText += string.Format("{0}<br><span style='color:lightgray;font-size:xx-small'>{1}</span>{2}", StoreCanBeFlushed ? string.Format("<a href=/cdediag.aspx?FLUSH={0}>", TheCommonUtils.cdeEscapeString(key)) : "", tUpdated, StoreCanBeFlushed ? "</a>" : "");
                    }
                    outText += "</th>";
                    magicMethod = magicType.GetMethod("GetCount");
                    if (magicMethod != null)
                    {
                        string tUpdated = magicMethod.Invoke(MyStorageMirror, null).ToString();
                        if (!string.IsNullOrEmpty(tUpdated))
                            outText += string.Format("<td class='cdeLogEntry' style='color:{0};text-align:center;max-width:100px'>{1}</td>", tColor, tUpdated);
                    }
                    magicMethod = magicType.GetMethod("GetSize");
                    if (magicMethod != null)
                    {
                        string tUpdated = magicMethod.Invoke(MyStorageMirror, null).ToString();
                        if (!string.IsNullOrEmpty(tUpdated))
                            outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1}</td>", tColor, tUpdated);
                    }
                    magicMethod = magicType.GetMethod("GetCacheStoreCount");
                    if (magicMethod != null)
                    {
                        string tUpdated = magicMethod.Invoke(MyStorageMirror, null).ToString();
                        outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1}</td>", tColor, tUpdated);
                    }
                    magicMethod = magicType.GetMethod("GetLastCacheStore");
                    if (magicMethod != null)
                    {
                        DateTimeOffset tUpdated = TheCommonUtils.CDate(magicMethod.Invoke(MyStorageMirror, null));
                        if (tUpdated > DateTimeOffset.MinValue)
                            outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1}</td>", tColor, tUpdated);
                        else
                            outText += string.Format("<td class='cdeLogEntry' style='color:{0}'>{1}</td>", tColor, "Not stored, yet");
                    }
                }
                else
                    outText += "</th>";
                outText += "</tr>";
            }
            outText += "</tbody></table></div>";
            return outText;
        }

        internal static string RenderHostServiceInfo(bool ShowHeader)
        {
            string outText = "";

            if (ShowHeader)
            {
                outText += AddHTMLHeader();
                outText += InfoHeader(false);
                if (TheBaseAssets.MyScopeManager.IsScopingEnabled)
                    outText += " A ScopeID is SET!";
                outText += "<br>Last Update at :" + TheCommonUtils.GetDateTimeString(DateTimeOffset.Now) + "</h3>";
            }

            outText += $"<div class=\"cdeInfoBlock\" style=\"clear:initial; width:750px\"><div class=\"cdeInfoBlockHeader cdeInfoBlockHeaderText\" id=\"hostServiceInfo\">HostServiceInfo (HSI) of this Node ";
            string tableID = "hostServiceInfoTable";
            outText += $"<a download =\"HSI_{TheCommonUtils.GetMyNodeName()}.csv\" href=\"#\" class=\'cdeExportLink\' onclick=\"return ExcellentExport.csv(this, '{tableID}');\">(Export as CSV)</a></div>";
            outText += htmlClassToForm(TheBaseAssets.MyServiceHostInfo, tableID);
            outText += "</div>";
            return outText;
        }

        private static string htmlClassToForm<T>(T pSource, string tableID = "")
        {
            string outText = $"<table class=\"cdeHilite\" id=\"{tableID}\"><tbody>";
            List<FieldInfo> Fieldsinfoarray = typeof(T).GetFields().OrderBy(x => x.Name).ToList();
            List<PropertyInfo> PropInfoArray = typeof(T).GetProperties().OrderBy(x => x.Name).ToList();
            foreach (FieldInfo finfo in Fieldsinfoarray)
            {
                if (finfo.ToString().StartsWith("System.Collection") || finfo.Name.StartsWith("cde")) continue;
                if (TheBaseAssets.MySettings.GetHiddenKeyList()?.Contains(finfo.Name)==true) continue;
                outText += $"<tr><th class='cdeLogEntryHeader'>{finfo.Name}</th>";
                object tObj = finfo.GetValue(pSource);
                string tFld = tObj != null ? TheCommonUtils.CStr(tObj) : "Not set (NULL)";
                outText += $"<td><td class='cdeLogEntry'>{tFld}</td></tr>";
            }

            foreach (PropertyInfo finfo in PropInfoArray)
            {
                if (finfo.ToString().StartsWith("System.Collection")) continue;
                if (TheBaseAssets.MySettings.GetHiddenKeyList()?.Contains(finfo.Name)==true) continue;
                outText += $"<tr><th class='cdeLogEntryHeader'>{finfo.Name}</th>";
                object tObj = finfo.GetValue(pSource, null);
                string tFld = tObj != null ? TheCommonUtils.CStr(tObj) : "Not set (NULL)";
                outText += $"<td><td class='cdeLogEntry cdeClip' style='width:500px;word-break: break-all;'>{tFld}</td></tr>";
            }
            outText += "</tbody></table>";
            return outText;
        }
    }
}
