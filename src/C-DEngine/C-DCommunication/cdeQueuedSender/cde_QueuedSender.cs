// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;

using nsCDEngine.BaseClasses;
using System.Linq;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Security;

//LOG RANGE: 230 - 249

namespace nsCDEngine.Communication
{
    internal partial class TheQueuedSender : TheDataBase
    {
        public TheQueuedSender()
        {
            MySubscriptions = new TheMirrorCache<TheSubscriptionInfo>();
            MyCoreQueue = new TheMirrorCache<TheCoreQueueContent>();
            MyJSKnownThings = new cdeConcurrentDictionary<Guid, byte>();
            MyJSKnownFields = new TheMirrorCache<TheMetaDataBase>();
            IsAlive = true;
            EnableKPIs = TheCommonUtils.CBool(TheThingRegistry.GetHostProperty("EnableKPIs"));
        }

        public bool StartSender(TheChannelInfo pChannelInfo, string pInitialSubscriptions, bool IsIncoming)
        {
            if (MyTargetNodeChannel != null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2300, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"This should never happen: QueuedSender {pChannelInfo.ToMLString()} has already started", eMsgLevel.l1_Error), true);
                return true;
            }
            if (TheBaseAssets.MyServiceHostInfo.NodeBlacklist.Contains(pChannelInfo.cdeMID))
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2300, new TSM("QueuedSender", $"Node {pChannelInfo} is blacklisted and will be rejected", eMsgLevel.l1_Error));
                TheLoggerFactory.LogEvent(eLoggerCategory.NodeConnect, "QSender not started; DeviceID is blacklisted", eMsgLevel.l4_Message, $"{MyTargetNodeChannel} ({MyTargetNodeChannel?.RealScopeID?.Substring(0, 4).ToUpper()})");
                return false;
            }
            MyTargetNodeChannel = pChannelInfo;
            var myTargetNodeChannel = MyTargetNodeChannel;
            if (MyTargetNodeChannel.SenderType == cdeSenderType.NOTSET)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2301, new TSM("CoreComm", "Sender Type for the QSender is not set! WE SHOULD NEVER GET HERE", eMsgLevel.l1_Error));//ORG-OK
                return false;
            }
            MyTargetNodeChannel.CreationTime = DateTimeOffset.Now;
            if (!string.IsNullOrEmpty(pInitialSubscriptions))
            {
                if (pInitialSubscriptions.StartsWith("{"))
                {
                    try
                    {
                        MyISBlock = TheCommonUtils.DeserializeJSONStringToObject<TheISBConnect>(pInitialSubscriptions);
                        if (!string.IsNullOrEmpty(MyISBlock?.IS))
                            Subscribe(MyISBlock?.IS, true);
                        TheBaseAssets.MySYSLOG.WriteToLog(2301, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", "Custom ISBConnect found!", eMsgLevel.l6_Debug), true);
                    }
                    catch
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2301, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", "illegal ISBConnect found", eMsgLevel.l6_Debug), true);
                    }
                }
                else
                    Subscribe(pInitialSubscriptions, true); //4.106: these are the only subscriptions "owned" by a QS
            }
            switch (TheQueuedSenderRegistry.AddQueuedSender(this))
            {
                case 0: return false;
                case 1:
                    //New in 4.207: Sender did Exist already we need to add the new ScopeID to the Senders "AlternativeScopeIDs"
                    var tS = TheQueuedSenderRegistry.GetSenderByGuid(pChannelInfo.cdeMID);
                    if (tS != null && tS.MyTargetNodeChannel?.ContainsAltScope(pChannelInfo.RealScopeID) == false)    //RScope-OK: If Realscope is set and Sender was already there, New scope is added to AltScopes
                    {
                        tS.MyTargetNodeChannel.AddAltScopes(new List<string> { pChannelInfo.RealScopeID }); //RScope-OK: If Realscope is set and Sender was already there, New scope is added to AltScopes
                        TheBaseAssets.MySYSLOG.WriteToLog(2301, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"QueuedSender already existed for for {pChannelInfo} - AltScope:{pChannelInfo.RealScopeID.ToUpper().Substring(0, 4)} added", eMsgLevel.l4_Message), true);
                    }
                    return true;
            }
            if (TheCommonUtils.IsLocalhost(MyTargetNodeChannel.cdeMID))
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2301, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", $"Create QueuedSender for LOCAL-HOST ({pChannelInfo.ToMLString()})", eMsgLevel.l6_Debug), true);
                MyTargetNodeChannel.SenderType = cdeSenderType.CDE_LOCALHOST;
                MyTargetNodeChannel.MySessionState = TheBaseAssets.MySession.CreateSession(new TheRequestData { DeviceID = MyTargetNodeChannel.cdeMID }, Guid.Empty);
                IsConnected = true;
                MyNodeInfo = new TheNodeInfoClone
                {
                    MyServiceInfo = TheCommonUtils.DeserializeJSONStringToObject<TheServiceHostInfoClone>(TheCommonUtils.SerializeObjectToJSONString(TheBaseAssets.MyServiceHostInfo)),
                    MyNodeID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                };
                SetTrustLevel();
            }
            else
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2302, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", $"Created new QueuedSender {MyTargetNodeChannel.ToMLString()}", eMsgLevel.l3_ImportantMessage), true);
                if (IsIncoming && TheCommonUtils.IsDeviceSenderType(MyTargetNodeChannel.SenderType))  //IDST-OK now: was Should Record Devices on incoming QS - but not on outgoing QS
                {
                    if (MyTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON)
                    {
                        TheFormsGenerator.RegisterNewNMINode(MyTargetNodeChannel.cdeMID, Guid.Empty, Guid.Empty, null, null);
                        IsTrusted = true; //SECURITY-REVIEW: Do we want to default to True of False here? So far this only colors the node red in the cdeStatus page. In the future there might be auto-throtteling of messages
                    }
                    TheDeviceRegistryData tDev = new TheDeviceRegistryData
                    {
                        DeviceID = MyTargetNodeChannel.cdeMID
                    };
                    if (TheBaseAssets.MyApplication.MyUserManager != null)
                        TheBaseAssets.MyApplication.MyUserManager.RegisterNewDevice(tDev);
                }
                if ((!TheCommonUtils.IsDeviceSenderType(MyTargetNodeChannel.SenderType) || !IsIncoming) && MyTargetNodeChannel.SenderType != cdeSenderType.CDE_BACKCHANNEL && MyWebSocketProcessor == null)   //NEW:RC3.7 Test if WS IsActive  IDST-OK Now was bug here: if this is on a device the sender threads are not created! but its required on nodes when the devices are connecting
                {
                    if (eventSenderThreadRunning != null)
                        TheBaseAssets.MySYSLOG.WriteToLog(2302, new TSM("QueuedSender", $"Should not get here! {MyTargetNodeChannel.ToMLString()} - MyWebSocketProcessor={MyWebSocketProcessor} but no WS Required??", eMsgLevel.l3_ImportantMessage), true);
                    eventSenderThreadRunning = null;
                    eventSenderThreadRunning += sinkSTRunning;
                    if (!TheBaseAssets.MyServiceHostInfo.DisableWebSockets && MyTargetNodeChannel.TargetUrl.StartsWith("ws", StringComparison.CurrentCultureIgnoreCase))
                    {
#if CDE_USEWSS8
                        if (TheBaseAssets.MyServiceHostInfo.IsWebSocket8Active)
                        {
                            TheWSProcessor8 tWS = new TheWSProcessor8(null);
                            tWS.eventConnected += sinkWSconnected;
                            tWS.SetRequest(null);
                            SetWebSocketProcessor(tWS);
                            if (!tWS.Connect(this))
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(2302, new TSM("QueuedSender", $"Could not create WS8 QS:{myTargetNodeChannel.ToMLString()}", eMsgLevel.l1_Error), true);
                                return false;
                            }
                        }
                        else
#endif
                        {
                            TheWSProcessor tWS = new TheWSProcessor(null);
                            tWS.eventConnected += sinkWSconnected;
                            tWS.SetRequest(new TheRequestData());
                            SetWebSocketProcessor(tWS);
                            if (!tWS.Connect(this))
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(2302, new TSM("QueuedSender", $"Could not create WSCS QS:{myTargetNodeChannel.ToMLString()}", eMsgLevel.l1_Error), true);
                                return false;
                            }
                        }
                    }
                    else
                    {
                        TheCommonUtils.cdeRunAsync($"QSender for ORG:{MyTargetNodeChannel}", false, o =>
                        {
                            Uri tTargetUri = new Uri(MyTargetNodeChannel.TargetUrl);
                            tTargetUri = tTargetUri.SetHTTPInfo(tTargetUri.Port, "");
                            MyTargetNodeChannel.TargetUrl = tTargetUri.ToString();
                            TheRESTSenderThread();
                            TheBaseAssets.MySYSLOG.WriteToLog(2303, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"RESTQSenderThread was closed (In RunAync) for {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l6_Debug));
                        });
                        if (TheCommCore.eventNewRelayConnection != null)
                        {
                            TheCommonUtils.cdeRunAsync("New REST-Relay Connected", true, (o) =>
                            {
                                TheCommCore.eventNewRelayConnection(TheCDEngines.MyContentEngine, MyTargetNodeChannel);
                            });
                        }
                    }
                }
                else
                {
                    if (TheBaseAssets.MyServiceHostInfo.IsCloudService && MyTargetNodeChannel?.SenderType == cdeSenderType.CDE_BACKCHANNEL)
                    {
                        TSM tInfoGet = new TSM(eEngineName.ContentService, "CDE_GET_SERVICEINFO")
                        {
                            SID = TheBaseAssets.MyScopeManager.GetScrambledScopeID(MyTargetNodeChannel.RealScopeID, true)
                        };
                        tInfoGet.SetNoDuplicates(true);
                        SendQueued(TheBaseAssets.MyScopeManager.AddScopeID("CDE_SYSTEMWIDE;" + MyTargetNodeChannel.cdeMID,true, MyTargetNodeChannel.RealScopeID), tInfoGet, false, MyTargetNodeChannel.cdeMID, null, MyTargetNodeChannel.RealScopeID, null);
                    }
                    StartHeartBeat();
                    IsConnecting = true;
                }
                TheLoggerFactory.LogEvent(eLoggerCategory.NodeConnect, "New QSender Started", eMsgLevel.l4_Message, $"{MyTargetNodeChannel} ({MyTargetNodeChannel?.RealScopeID?.Substring(0, 4).ToUpper()})");
                TheCDEKPIs.IncrementKPI(eKPINames.QSConnects);
            }
            return true;
        }

        void sinkSTRunning(TheQueuedSender qSender)
        {
            StartHeartBeat();
        }

        void sinkWSconnected(TheWSProcessorBase sender, bool bSuccess, string pReason)
        {
            if (bSuccess)
            {
                if (TheCommCore.eventNewRelayConnection != null)
                {
                    TheCommonUtils.cdeRunAsync("New Relay Connected", true, (o) =>
                    {
                        TheCommCore.eventNewRelayConnection(TheCDEngines.MyContentEngine, MyTargetNodeChannel);
                    });
                }
                MyISBlock?.FireEvent("InitialConnect");
            }
            else
            {
                LastError = pReason;
                TheBaseAssets.MySYSLOG.WriteToLog(2303, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"WSConnect Failed for {MyTargetNodeChannel?.ToMLString()} with error: {LastError}", eMsgLevel.l6_Debug));
            }
        }

        internal void ChangeSenderType(cdeSenderType pSenderType)
        {
            if (MyTargetNodeChannel.SenderType != pSenderType)
                MyTargetNodeChannel.SenderType = pSenderType;
        }

        internal long QueueSerial;
        internal static object StartSenderLock = new object();
        internal Action<TheQueuedSender, string> eventErrorDuringUpload;
        internal Action<TheQueuedSender, TheChannelInfo> eventConnected;
        internal Action<TheQueuedSender> eventSenderThreadRunning;
        internal TheChannelInfo MyTargetNodeChannel;
        private Timer mMyHeartBeatTimer = null;
        internal bool IsSenderThreadRunning;
        public bool IsAlive;
        private string m_LastError;
        private string LastError {
            get { return m_LastError; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    m_LastError = null;
                    LastErrorCode = 0;
                    return;
                }
                var t = value.Split(':');
                if (t.Length > 1 && TheCommonUtils.CInt(t[0]) > 0)
                {
                    LastErrorCode = TheCommonUtils.CInt(t[0]);
                    m_LastError = value.Substring(value.IndexOf(':') + 1);
                }
                else
                {
                    LastErrorCode = 999; //unknown internal code
                    m_LastError = value;
                }
            }
        }
        private int LastErrorCode;
        internal DateTimeOffset MyLastConnectTime=DateTimeOffset.MinValue;
        private TheNodeInfoClone MyNodeInfo = null;
        static private List<string> ThumbList = null;   //NodeWide App.config "cache"
        public bool IsTrusted { get; private set; }
        internal void SetNodeInfo(TheNodeInfoClone pInfo)
        {
            MyNodeInfo = pInfo;
            SetTrustLevel();
        }

        internal TheNodeDiagInfo GetNodeDiagInfo()
        {
            TheNodeDiagInfo nodeDiagInfo = null;
            if (!(MyNodeInfo.MyServices?.Count>0) && MyTargetNodeChannel!=null && TheCommonUtils.IsLocalhost(MyTargetNodeChannel.cdeMID))
            {
                List<IBaseEngine> tList3 = TheThingRegistry.GetBaseEngines(false);
                foreach (IBaseEngine tBase in tList3)
                {
                    try
                    {
                        //this can crash in newton if the engine state is changed during serialization - since this is called only once during startup its non-critical
                        MyNodeInfo.MyServices.Add(TheCommonUtils.DeserializeJSONStringToObject<TheEngineStateClone>(TheCommonUtils.SerializeObjectToJSONString(tBase.GetEngineState())));
                    }
                    catch (Exception)
                    {
                        //ignored
                    }
                }
            }
            nodeDiagInfo = new TheNodeDiagInfo
            {
                CDEVersion = MyNodeInfo?.MyServiceInfo?.Version,
                ServiceCount = MyNodeInfo?.MyServices != null ? MyNodeInfo.MyServices.Count : 0,
                Telegrams = MySubscriptions?.TheValues != null ? MySubscriptions.TheValues.Sum(s => s.Hits) : 0,
                StartupTime = cdeCTIM,
                UsesWS = HasWebSockets(),
                LastHB = (GetLastHeartBeat() == DateTimeOffset.MinValue ? "not yet" : $"{TheCommonUtils.GetDateTimeString(GetLastHeartBeat(), -1)}"),
                Services = MyNodeInfo?.MyServices.Select(s => $"{s.FriendlyName} ({s.QuickStatus} / {s.LastMessage})").ToList()
            };
            return nodeDiagInfo;
        }

        private void SetTrustLevel()
        {
            IsTrusted = MyTargetNodeChannel?.SenderType == cdeSenderType.CDE_LOCALHOST ? !TheBaseAssets.MyServiceHostInfo.DontVerifyTrust : MyNodeInfo?.MyServiceInfo?.DontVerifyTrust == false;
            if (IsTrusted)
            {
                //New after Security Campus 1910: Verify that CodeSign Cert Thumb is known and valid. App.Config needs to have entry:
                //<add key="AllowedCodeSignThumbs" value="" />
                if (ThumbList == null)
                {
                    var temp = TheBaseAssets.MySettings.GetSetting("AllowedCodeSignThumbs");
                    if (!string.IsNullOrEmpty(temp))
                        ThumbList = TheCommonUtils.CStringToList(temp.ToLower(), ';');
                    else
                        ThumbList = new List<string>();
                }
                if (ThumbList?.Count > 0 && !ThumbList.Contains(MyTargetNodeChannel?.SenderType == cdeSenderType.CDE_LOCALHOST ? TheBaseAssets.MyServiceHostInfo.CodeSignThumb : MyNodeInfo?.MyServiceInfo?.CodeSignThumb))
                    IsTrusted = false;
            }
        }

        readonly bool EnableKPIs;
        private ManualResetEvent mre = null;
        internal TheISBConnect MyISBlock = null;
        internal volatile bool _IsConnected;
        internal bool IsConnected
        {
            get { return _IsConnected; }
            set
            {
                var previous = _IsConnected;
                _IsConnected = value;
                if (previous != value && MyTargetNodeChannel != null)
                {
                    var tMyTargetNodeChannel = MyTargetNodeChannel;
                    var senderType = tMyTargetNodeChannel.SenderType;
                    eDEBUG_LEVELS dbgLevel = (senderType == cdeSenderType.CDE_CLOUDROUTE || senderType == cdeSenderType.CDE_SERVICE || senderType == cdeSenderType.CDE_BACKCHANNEL) ? eDEBUG_LEVELS.ESSENTIALS : eDEBUG_LEVELS.VERBOSE;
                    // Note: reserve LogID 2330-2349 / 2350-2369 for future senderTypes
                    if (value)
                    {
                        IsConnecting = false;
                        ConnectRetries = 0;
                        MyLastConnectTime = DateTimeOffset.Now;
                        TheBaseAssets.MySYSLOG.WriteToLog(2330 + (int)senderType, TSM.L(dbgLevel) ? null : new TSM("QueuedSender", $"Route connected: {tMyTargetNodeChannel}", eMsgLevel.l4_Message), false);
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2350 + (int)senderType, TSM.L(dbgLevel) ? null : new TSM("QueuedSender", $"Route disconnected: {tMyTargetNodeChannel}", eMsgLevel.l4_Message), false);
                        TheBaseAssets.MySession.RemoveSessionsByDeviceID(tMyTargetNodeChannel.cdeMID, Guid.Empty);
                        tMyTargetNodeChannel.MySessionState = null;
                    }
                }
                else
                {
                    if (previous!=value)
                        TheBaseAssets.MySYSLOG.WriteToLog(2399, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"In Set IsConnected to {_IsConnected}: {MyTargetNodeChannel}", eMsgLevel.l4_Message), false);
                }
            }
        }
        internal volatile bool _IsConnecting;
        internal bool IsConnecting
        {
            get { return _IsConnecting; }
            set
            {
                if (_IsConnecting != value)
                    TheBaseAssets.MySYSLOG.WriteToLog(2304, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QSender", $"Setting IsConnecting from:{_IsConnecting} to:{value}", eMsgLevel.l3_ImportantMessage
#if JC_DEBUGCOMM  //No Stacktrace on Platforms other than windows
, TheCommonUtils.GetStackInfo(new System.Diagnostics.StackTrace(true))
#endif
                    ));
                _IsConnecting = value;
            }
        }
        private int ConnectRetries;
        //private long SendCounter = 1;

        public bool HasWebSockets()
        {
            return MyWebSocketProcessor!=null;
        }

        internal string GetLastError()
        {
            return LastError;
        }

        internal int GetLastErrorCode()
        {
            return LastErrorCode;
        }

        public int GetQueLength()
        {
            return MyCoreQueue.Count;
        }
        internal string GetQueDiag(bool ShowQueue)
        {
            if (MyTargetNodeChannel == null) return "No channel, yet";
            DateTimeOffset lastConnectTime= MyLastConnectTime;
            string ret = $"{MyTargetNodeChannel} QL=<span style='color:green;font-weight:bold;'>{MyCoreQueue.Count}</span> Seen:{MyTSMHistoryCount} LastHB:{GetLastHeartBeat()} ConnectedSince:{(lastConnectTime==DateTimeOffset.MinValue?"not yet":$"{lastConnectTime}")} ConnectedFor: {(lastConnectTime == DateTimeOffset.MinValue ? 0: (DateTimeOffset.Now - lastConnectTime).TotalSeconds)}s InPost:{IsInPost} UsesWS:<span style='color:green;font-weight:bold;'>{HasWebSockets()}</span> IsInWSPost:{IsInWSPost} IsConnecting:{IsConnecting} IsConnected:<span style='color:green;font-weight:bold;'>{IsConnected}</span> IsAlive:<span style='color:green;font-weight:bold;'>{IsAlive}</span> IsSenderThreadAlive:<span style='color:green;font-weight:bold;'>{IsSenderThreadRunning}</span> SEID:{(MyTargetNodeChannel.MySessionState == null ? "not set" : MyTargetNodeChannel.MySessionState.cdeMID.ToString())}<ul>";
            if (ShowQueue)
            {
                foreach (TheCoreQueueContent t in MyCoreQueue.TheValues.OrderBy(s => s.QueueIdx).ThenBy(s => s.EntryTime))  //LOCK-REVIEW: TheValues is already a ToList
                {
                    string tColr = "black";
                    if (t.QueueIdx < 3) tColr = "orange";
                    if (t.QueueIdx > 5) tColr = "purple";
                    if (t.OrgMessage == null)
                        ret += $"<li style='color:{tColr}'>{t.QueueIdx} {TheCommonUtils.GetDateTimeString(t.cdeCTIM, -1)} cdePICKUP Message</li>";
                    else
                        ret += $"<li style='color:{tColr}'>{t.QueueIdx} {TheCommonUtils.GetDateTimeString(t.OrgMessage.TIM, -1)} {t.OrgMessage.ENG} ORG:{TheCommonUtils.GetDeviceIDML(t.OrgMessage.ORG)}</br>TOPIC:{StripTopic(t.Topic)}</br><span  style='color:green'>{t.HashID} / {t.OrgMessage.TXT}</span></li>";
                }
            }
            ret += "</ul>";
            return ret;
        }
        internal string GetQueDiagML(bool ShowQueue, bool IsOdd)
        {
            if (MyTargetNodeChannel == null) return "No channel, yet";
            DateTimeOffset lastConnectTime = MyLastConnectTime;
            StringBuilder ret = new StringBuilder($"<tr><td class=\"cdeLogEntry\" style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);text-align:left \">{MyTargetNodeChannel.ToMLString(true)}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{(string.IsNullOrEmpty(MyTargetNodeChannel?.RealScopeID) ? "<span style='color:purple'>not Scoped</span>" : cdeStatus.GetScopeHashML(TheCommonUtils.CStringToList(MyTargetNodeChannel.GetScopes(),' ')))}</td>");
            if (TheBaseAssets.MyServiceHostInfo.IsCloudService)
                ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{(MyNodeInfo?.MyServiceInfo?.DontVerifyTrust==true? "<span style='color:red; font-weight:bold;'>NO</span>" : (MyNodeInfo?.MyServiceInfo==null?"Unknown":"Yes"))}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{(MyNodeInfo?.MyServiceInfo?.ClientCertificateThumb?.Length > 3 ? MyNodeInfo?.MyServiceInfo?.ClientCertificateThumb?.Substring(0, 4) : "<span style='color:purple'>No cert</span>")}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{MyCoreQueue.Count}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{MyTSMHistoryCount}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{(IsAlive ? "<span style='color:green; font-weight:bold;'>YES</span>" : "<span style='color:red; font-weight:bold;'>NO</span>")}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{(IsConnecting ? "<span style='color:orange; font-weight:bold;'>YES</span>" : "no")}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{(IsConnected ? "<span style='color:green; font-weight:bold;'>YES</span>" : "<span style='color:red; font-weight:bold;'>NO</span>")}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{GetLastError()}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{(lastConnectTime == DateTimeOffset.MinValue ? "not yet" : $"{TheCommonUtils.GetDateTimeString(lastConnectTime, -1)}")}</td>");
#if CDE_NET35
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{(lastConnectTime == DateTimeOffset.MinValue ? "not yet" : $"{(DateTimeOffset.Now - lastConnectTime)}")}</td>");
#else
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{(lastConnectTime == DateTimeOffset.MinValue ? "not yet" : (DateTimeOffset.Now - lastConnectTime).ToString(@"dd\.hh\:mm\:ss"))}</td>");
#endif
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{TheCommonUtils.GetDateTimeString(GetLastHeartBeat(), -1)}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{HasWebSockets()}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{IsInPost}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{IsInWSPost}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{IsSenderThreadRunning}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:left;\">{(MyTargetNodeChannel.MySessionState == null ? "not set" : MyTargetNodeChannel.MySessionState.cdeMID.ToString())}</td>");
            ret.Append($"<td class=\"cdeLogEntry\" style=\"text-align:center;\">{MyNodeInfo?.MyServiceInfo?.CurrentVersion}</td></tr>");
            if (ShowQueue)
            {
                int count = 0;
                foreach (TheCoreQueueContent t in MyCoreQueue.TheValues.OrderBy(s => s.QueueIdx).ThenBy(s => s.EntryTime))  //LOCK-REVIEW: TheValues is already a ToList
                {
                    if (count == 0)
                    {
                        ret.Append($"<tr><td>Current Queue content:</td><td style=\"border-bottom:1px solid rgba(90, 90, 90, 0.25);text-align:left; \" colspan=\"14\"><table><TR><th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; \">QDX</th>");
                        ret.Append($"<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; \">TIM</th><th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; \">ENG</th><th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; \">ORG</th><th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; \">Topic</th></tr>");
                    }
                    count++;
                    string tColr = "black";
                    if (t.QueueIdx < 3) tColr = "orange";
                    if (t.QueueIdx > 5) tColr = "purple";
                    if (t.OrgMessage == null)
                        ret.Append($"<tr style='color:{tColr}'><td>{t.QueueIdx}</td><td>{TheCommonUtils.GetDateTimeString(t.cdeCTIM)}</td><td></td><td></td><td>cdePICKUP Message</td><tr>");
                    else
                        ret.Append($"<tr style='color:{tColr}'><td>{t.QueueIdx}</td><td>{TheCommonUtils.GetDateTimeString(t.OrgMessage.TIM)}</td><td>{t.OrgMessage.ENG}</td><td>{t.OrgMessage.ORG}</td><td>{StripTopic(t.Topic)}</td><tr>");
                }
                if (count > 0)
                    ret.Append($"</table></td></tr>");
            }
            return ret.ToString();
        }

        private string StripTopic(string pTopic)
        {
            if (string.IsNullOrEmpty(pTopic)) return "";
            string tRes = pTopic;
            string tTarget = "";
            string tScope="";
            if (pTopic.Contains(";"))
                tTarget = ";"+ pTopic.Split(';')[1];
            if (pTopic.Contains("@"))
            {
                try
                {
                    tRes = pTopic.Split('@')[0];
                    tScope = TheBaseAssets.MyScopeManager.GetRealScopeID(TheCommonUtils.cdeSplit(pTopic.Split('@')[1], ":,:", false, false)[0]); //GRSI: rare
                    if (tScope.Length >= 4)
                    {
                        tScope = tScope.Substring(0, 4).ToUpper();
                    }
                    else
                    {
                        tScope = "unavailable";
                    }
                }
                catch (Exception)
                {
                    tScope = "unavailable";
                }
            }
            tRes += tTarget + $" {cdeStatus.GetScopeHashML(tScope)}";
            return tRes;
        }
        internal void FlushQueue()
        {
            MyCoreQueue.RemoveAllItems();
        }
        internal void RemoveOrphanFromQueue(Guid pUri)
        {
            List<TheCoreQueueContent> tErasers = MyCoreQueue.TheValues.Where(s => s == null || s.OrgMessage == null || s.OrgMessage.GetOriginator().Equals(pUri)).ToList();
            MyCoreQueue.RemoveItems(tErasers, null);
        }
        private readonly TheMirrorCache<TheCoreQueueContent> MyCoreQueue;
        private readonly TheMirrorCache<TheMetaDataBase> MyJSKnownFields;
        private readonly cdeConcurrentDictionary<Guid,byte> MyJSKnownThings;

#region Cleanup and Excption Handling


        /// <summary>
        /// Changed to Internal in order to allow other C-DEngine parts to kill a QSender
        /// </summary>
        /// <param name="pCause"></param>
        internal void FireSenderProblem(TheRequestData pCause)
        {
            DoFireSenderProblem(pCause.ErrorDescription);
        }
        private int InFireProblem;
        private bool CloudInRetry;
        private int CloudRetryCount = 0; // Must access with Interlocked.*
        private void DoFireSenderProblem(string pCause)
        {
            if (Interlocked.Exchange(ref InFireProblem, 1) != 0)
            {
                return;
            }
            LastError = pCause;
            try
            {
                if (MyTargetNodeChannel != null && MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE) // && !TheCommonUtils.IsHostADevice()) NEW3.211: Even Devices should try reconnect
                {
                    StopHeartBeat();
                    //SendCounter = 0;
                    IsConnected = false;
                    IsConnecting = false;
                    MyTSMHistory?.Reset();
                    if (MyWebSocketProcessor != null)
                    {
                        if (MyWebSocketProcessor.IsActive)
                            MyWebSocketProcessor.IsActive = false; //3-1.0.1 This was not set to false on certain conditions like HB failure.
                    }
                    TheBaseAssets.MySession.RemoveSessionsByDeviceID(MyTargetNodeChannel.cdeMID,Guid.Empty);
                    if (!CloudInRetry)
                    {
                        CloudInRetry = true;
                        Interlocked.Exchange(ref CloudRetryCount, 0);
                        if (TheQueuedSenderRegistry.eventCloudIsDown != null)
                        {
                            TheCommonUtils.cdeRunAsync("CloudDown", true, o => { TheQueuedSenderRegistry.eventCloudIsDown(TheCDEngines.MyContentEngine, MyTargetNodeChannel); });
                        }
                        if (TheBaseAssets.MyServiceHostInfo.IsIsolated)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(23051, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QSRegistry", $"Route to Master-Node is down - ISOLater will close {pCause}", eMsgLevel.l3_ImportantMessage));
                            TheBaseAssets.MyApplication.Shutdown(true);
                            return;
                        }
                        else
                            TheBaseAssets.MySYSLOG.WriteToLog(23052, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QSRegistry", $"Cloud Route is down - trying to re-establish route. Cause: {pCause}", eMsgLevel.l4_Message));
                    }
                }
                else //RC1.2: Cloud has to be restarted as well
                {
                    try
                    {
                        eventErrorDuringUpload?.Invoke(this, pCause);
                        MyISBlock?.FireEvent("CommError", new TheProcessMessage() { Cookie = pCause });
                        //else  //NEW:V3BETA2: No more DisposeSender event handler
                        TheBaseAssets.MySYSLOG.WriteToLog(2306, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QSRegistry", $"Forced Removal of QSender - Due to SenderProblem. Cause: {pCause}", eMsgLevel.l4_Message));
                        DisposeSender(true);
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2307, new TSM("QueuedSender", $"Error in ErrorDuringUpload/Dispose Sender {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l1_Error, e.ToString()));
                    }
                    IsConnected = false;
                    IsConnecting = false;
                }
                TheCDEKPIs.IncrementKPI(eKPINames.QSSendErrors);
            }
            catch (Exception eee)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2307, new TSM("QueuedSender", $"Unexpected Error in DoFireSenderProblem Sender {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l1_Error, eee.ToString()));
            }
            finally
            {
                if (Interlocked.Exchange(ref InFireProblem, 0) != 1)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2307, new TSM("QueuedSender", $"Very unexpected Error in DoFireSenderProblem Sender {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l1_Error));
                }
            }
        }

        private readonly object MyReconnectCloudLock = new object();

        internal void ReconnectCloud()
        {
            if (((IsConnecting || IsConnected) && IsAlive) || !TheBaseAssets.MasterSwitch || TheCommonUtils.cdeIsLocked(MyReconnectCloudLock))
                return;
            if (HasWebSockets())
            {
                if (TheCommonUtils.cdeIsLocked(MyReconnectCloudLock))
                    return;
                lock (MyReconnectCloudLock)
                {
                    if (((IsConnecting || IsConnected) && IsAlive) || !TheBaseAssets.MasterSwitch)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2310, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", $"Found QueuedSender already connected or reconnecting while restarting Cloud {CloudCounter} WS Connection to {MyTargetNodeChannel} IsAlive:{IsAlive} IsConnected:{IsConnected} IsConnecting:{IsConnecting} MyWSProccIsActive:{MyWebSocketProcessor?.IsActive}", eMsgLevel.l1_Error)); // TODO assign unique logid
                        return;
                    }

                    var registeredSender = TheQueuedSenderRegistry.GetSenderByGuid(this.MyTargetNodeChannel.cdeMID);
                    if (registeredSender != this)
                    {
                        if (this.MyTargetNodeChannel.cdeMID==registeredSender.MyTargetNodeChannel?.cdeMID)
                        {
                            //This should not happen! We will always print an error here. One reason for this is that the ServiceRoute contains two entries with the same url as target
                            TheBaseAssets.MySYSLOG.WriteToLog(2310, new TSM("QueuedSender", $"For some unknown reason is the current (this) QeueSender not in the Registry but a different instance with same TargetMid is. QSender={MyTargetNodeChannel} (this) will be disposed! IsAlive:{IsAlive} IsConnected:{IsConnected} IsConnecting:{IsConnecting} MyWSProccIsActive:{MyWebSocketProcessor?.IsActive}", eMsgLevel.l1_Error)); // TODO assign unique logid
                            this.Dispose();
                            return;
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(2310, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", $"Found duplicate QueuedSender while restarting Cloud {CloudCounter} WS Connection to {MyTargetNodeChannel} IsAlive:{IsAlive} IsConnected:{IsConnected} IsConnecting:{IsConnecting} MyWSProccIsActive:{MyWebSocketProcessor?.IsActive}", eMsgLevel.l1_Error)); // TODO assign unique logid
                        return;
                    }

                    TheBaseAssets.MySYSLOG.WriteToLog(2310, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", $"Trying to restart Cloud {CloudCounter} WS Connection to {MyTargetNodeChannel} IsAlive:{IsAlive} IsConnected:{IsConnected} IsConnecting:{IsConnecting} MyWSProccIsActive:{MyWebSocketProcessor?.IsActive}", eMsgLevel.l3_ImportantMessage));
                    IsAlive = true;
                    SetWebSocketProcessor(MyWebSocketProcessor);
                    if (MyWebSocketProcessor != null)
                    {
                        MyWebSocketProcessor.eventConnected -= sinkCloudConnectedToWS;
                        MyWebSocketProcessor.eventConnected += sinkCloudConnectedToWS;
                        MyWebSocketProcessor.Connect(this);
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2311, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"Cloud Channel could not be started to: {MyTargetNodeChannel} WS:{MyWebSocketProcessor != null}"));
                        IsAlive = false;
                    }
                }
            }
            else
            {
                if (!IsSenderThreadRunning)
                {
                    TheCommonUtils.cdeRunAsync("QS-UpCo2Exec", false, o =>
                    {
                        TheRESTSenderThread();
                        TheBaseAssets.MySYSLOG.WriteToLog(2303, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"RESTQSenderThread in ReconnectCloud was closed (In RunAync) for {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l6_Debug));
                    });
                }
            }
        }

        void sinkCloudConnectedToWS(TheWSProcessorBase pSender, bool bSuccess,string pReason)
        {
            if (bSuccess)
            {
                CloudInRetry = false;
                if (MyISBlock == null)
                {
                    string tSubs = TheBaseAssets.MyScopeManager.AddScopeID(TheBaseAssets.MyServiceHostInfo.MyLiveServices, false);
                    Subscribe(tSubs);
                }
                TheBaseAssets.MySYSLOG.WriteToLog(23053, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QSRegistry", $"WS Cloud Route is back up to {MyTargetNodeChannel.ToMLString()}", eMsgLevel.l4_Message));
                StartHeartBeat();
                if (TheQueuedSenderRegistry.eventCloudIsBackUp != null)
                    TheCommonUtils.cdeRunAsync("CloudUpEvent", true, (p) =>
                    {
                        TheQueuedSenderRegistry.eventCloudIsBackUp(TheCDEngines.MyContentEngine, MyTargetNodeChannel);
                    });
                MyISBlock?.FireEvent("CloudConnected");
            }
            else
            {
                LastError = pReason;
                TheBaseAssets.MySYSLOG.WriteToLog(23054, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"Cloud Channel could not be started to: {MyTargetNodeChannel.ToMLString()} WS:{pSender != null} Error:{LastError}"));
                IsAlive = false;
            }
        }

        public void Dispose()
        {
            eventConnected = null;
            eventSenderThreadRunning = null;
            eventErrorDuringUpload = null;
            if (TheBaseAssets.MyServiceHostInfo.UseHBTimerPerSender)
            {
                Timer timer = Interlocked.CompareExchange(ref mMyHeartBeatTimer, null, mMyHeartBeatTimer);
                if (timer != null)
                {
                    mMyHeartBeatTimer = null;
                    try
                    {
                        timer.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                    catch { }
                    try
                    {
                        timer.Dispose();
                    }
                    catch { }
                }
            }
            else
            {
                TheQueuedSenderRegistry.UnregisterHBTimer(sinkHeartBeatTimer);
            }
        }

        public void DisposeSender(bool ForceExit)
        {
            Dispose();
            IsAlive = false;
            try
            {
                MyTSMHistory?.Dispose();
                if (MyTargetNodeChannel == null)
                    return;
                TheLoggerFactory.LogEvent(eLoggerCategory.NodeConnect, "QSender Disposed", eMsgLevel.l4_Message, $"{MyTargetNodeChannel} ({MyTargetNodeChannel?.RealScopeID?.Substring(0, 4).ToUpper()})");
                TheCDEKPIs.IncrementKPI(eKPINames.QSDisconnects);
                //TheQueuedSenderRegistry.RemoveSubscriptionsFromBackChannels(MySubscriptions.Keys);    //VERIFY: 2013-03-01 Removed because it canceled out too many subscriptions
                TheQueuedSenderRegistry.RemoveQueuedSender(MyTargetNodeChannel);
                MyWebSocketProcessor = null;
                if (TheBaseAssets.MyServiceHostInfo.IsCloudService && !TheBaseAssets.MyScopeManager.IsScopingEnabled && !string.IsNullOrEmpty(MyTargetNodeChannel?.RealScopeID) && MyTargetNodeChannel.SenderType == cdeSenderType.CDE_BACKCHANNEL)  //RScope-OK: Users on Primary Scope only
                    TheUserManager.RemoveUsersByScope(MyTargetNodeChannel.TruDID == Guid.Empty ? MyTargetNodeChannel.cdeMID : MyTargetNodeChannel.TruDID, MyTargetNodeChannel?.RealScopeID); //RScope-OK: Users on Primary Scope only //4.209: fix for correct removal of users using the correct NodeID
                IsConnected = false;
                if (MyTargetNodeChannel?.MySessionState != null)
                {
                    TheBaseAssets.MySession.RemoveSessionsByDeviceID(MyTargetNodeChannel.cdeMID, Guid.Empty);
                    if (MyTargetNodeChannel != null && MyTargetNodeChannel.MySessionState != null)
                        TheBaseAssets.MySession.RemoveSessionByID(MyTargetNodeChannel.MySessionState.cdeMID);
                    MyTargetNodeChannel.MySessionState = null;
                }
                TheBaseAssets.MySYSLOG.WriteToLog(2308, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", $"QSender Last Reference removed - QSender will dispose for {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l3_ImportantMessage
#if JC_DEBUGCOMM  //No Stacktrace on Platforms other than windows
, TheCommonUtils.GetStackInfo(new System.Diagnostics.StackTrace(true))
#endif
));
                if (TheCommCore.eventRelayConnectionDropped != null)
                {
                    TheCommonUtils.cdeRunAsync("Relay Connection Dropped", true, (o) =>
                    {
                        TheCommCore.eventRelayConnectionDropped(TheCDEngines.MyContentEngine, MyTargetNodeChannel);
                    });
                }
                MyISBlock?.FireEvent("Disconnected");
            }
            catch (Exception ee)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2309, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", $"Fatal Error during QSender Dispose for {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l1_Error, ee.ToString()), true);
            }
            finally
            {
                MyTargetNodeChannel = null;
            }
        }
#endregion

        /// <summary>
        /// Only used for "pickup messages" (no Topic, no TSM)
        /// </summary>
        /// <returns></returns>
        public bool SendPickupMessage()
        {
            return SendQueued(null,null, false, Guid.Empty, null,null, null);
        }
        //public bool SendQueued(string pTopic, TSM pMessage, bool PublishToTopicsOnly, Action<TSM> pLocalCallback) //Retired in 4.212: Not in use anywhere. Public is misleading as the class is "internal" and cannot be called from outside the CDE
        //{
        //    return SendQueued(pTopic, pMessage, false, Guid.Empty, null, null, pLocalCallback);
        //}
        internal bool SendQueued(string pTopic, TSM pMessage, bool PublishToTopicsOnly, Guid pDirectAddress, string pTopicOnly, string pTopicScopeID, Action<TSM> pLocalCallback)
        {
            if (string.IsNullOrEmpty(pTopic))
            {
                if (MyCoreQueue.Count > 0) return false; // || (MyWebSocketProcessor!=null && !TheBaseAssets.MyServiceHostInfo.IsCloudService && MyTargetNodeChannel.SenderType!=cdeSenderType.CDE_BACKCHANNEL)) return false; //always send HB Back
                TheCoreQueueContent tQue = new TheCoreQueueContent
                {
                    Topic = "",
                    EntryTime = DateTimeOffset.Now,
                    QueueIdx = 1
                };
                MyCoreQueue.AddAnItem(tQue,null);
                return true;
            }
            var myTargetNodeChannel = MyTargetNodeChannel;
            if (myTargetNodeChannel == null)
            {
                return false;
            }
            if (pMessage == null || ((myTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE || myTargetNodeChannel.SenderType == cdeSenderType.CDE_BACKCHANNEL) && pMessage.QDX == 0))
                return false;  //This prevents off all High speed telegrams reserved for service-to-service communication from being relayed
            if (TheBaseAssets.MyServiceHostInfo.DisableNMIMessages && pMessage?.ENG == eEngineName.NMIService)
            {
                TheCDEKPIs.IncrementKPI(eKPINames.QSSETPRejected);
                return false;
            }

            //Block all telegrams that dont have the correct scope ID as specified in the Client Certificate
            if (!string.IsNullOrEmpty(pTopicScopeID) && myTargetNodeChannel.MySessionState?.CertScopes?.Count > 0 && myTargetNodeChannel.MySessionState?.CertScopes?.Contains(pTopicScopeID)==false)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2320, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"Message to {myTargetNodeChannel.ToMLString()} rejected - scope on message does not match scopes in certificate", eMsgLevel.l1_Error), true);
                TheCDEKPIs.IncrementKPI(eKPINames.QSNotRelayed); //TODO:KPI New KPI here
                return false;
            }

            TheCDEKPIs.IncrementKPI(eKPINames.QSInserted);

            if (myTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON)
            {
                if (string.IsNullOrEmpty(myTargetNodeChannel.RealScopeID) && TheBaseAssets.MyScopeManager.IsScopingEnabled) //RScope-OK: JavaScript browser on primary scope only
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2320, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("QueuedSender", $"Message to {myTargetNodeChannel.ToMLString()} reject - JavaScript Node not logged on, yet", eMsgLevel.l6_Debug), true);
                    return false;
                }
            }

            if (!(TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay || TheBaseAssets.MyServiceHostInfo.IsIsolated || TheCommonUtils.IsLocalhost(myTargetNodeChannel.cdeMID) ||
                (pMessage.HobCount() == 1 && TheCommonUtils.IsLocalhost(pMessage.GetOriginator()))))   //No Relay from any node except relay nodes
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2321, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("QueuedSender", $"Message to {myTargetNodeChannel.ToMLString()} reject - Local Node is not a relay node", eMsgLevel.l6_Debug), true);
                return false;
            }


            bool tHasDirectAddress = pDirectAddress!=Guid.Empty || pTopic.Contains(";");
            Guid tDirectGuid = pDirectAddress;
            string tTopic = pTopic;
            if (tHasDirectAddress)
            {
                if (pDirectAddress == Guid.Empty)
                    tDirectGuid = TheCommonUtils.CGuid(pTopic.Split(';')[1]);
                tTopic = pTopic.Split(';')[0];
            }
            string tTopicNameOnly = pTopicOnly;
            string tTopicRealScope = pTopicScopeID;
            if (tTopicRealScope==null)
                tTopicRealScope = TheBaseAssets.MyScopeManager.GetRealScopeIDFromTopic(tTopic, out tTopicNameOnly);

            if (PublishToTopicsOnly)
            {
                if (!tHasDirectAddress && !tTopic.StartsWith("CDE_SYSTEMWIDE"))
                {
                    if (!MySubscriptionContains(tTopicNameOnly, tTopicRealScope, true))    //Super high frequency!
                    {
                        //Allows for Unscoped StorageService to receive and send Scoped Commands
                        if (TheCommonUtils.IsLocalhost(myTargetNodeChannel.cdeMID))
                        {
                            IBaseEngine tBaseE = TheThingRegistry.GetBaseEngine(tTopicNameOnly);
                            if (tBaseE != null && tBaseE.GetEngineState().IsAllowedUnscopedProcessing && !TheBaseAssets.MyScopeManager.IsScopingEnabled)
                                TheBaseAssets.MySYSLOG.WriteToLog(2322, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("QueuedSender", $"Message to {myTargetNodeChannel.ToMLString()} ACCEPTED - target has different scope but is allowed for unscoped Relay", eMsgLevel.l6_Debug), true);
                            else
                            {
                                if (tBaseE != null && tBaseE.GetEngineState().IsAllowedForeignScopeProcessing && (TheBaseAssets.MyServiceHostInfo.IsCloudService || TheBaseAssets.MyScopeManager.IsScopingEnabled))  //NEW3.124: Allow Foreign Scope Processing for certain plugins in the cloud which is unscoped!
                                    TheBaseAssets.MySYSLOG.WriteToLog(2323, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Message to {myTargetNodeChannel.ToMLString()} ACCEPTED - target has different scope but is allowed for Foreign-Scope Relay", eMsgLevel.l6_Debug), true);
                                else
                                {
                                    if (tTopic.StartsWith("LOGIN_SUCCESS"))
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(2323, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Message to {myTargetNodeChannel.ToMLString()} ACCEPTED - target has different scope but LOGIN_SUCESS message is allowed anyway", eMsgLevel.l6_Debug), true);
                                    }
                                    else
                                    {
                                        if (myTargetNodeChannel.SenderType == cdeSenderType.CDE_LOCALHOST)
                                        {
                                            bool bFoundISB = false;
                                            List<TheQueuedSender> tISBSender = TheQueuedSenderRegistry.GetISBConnectSender(tTopicRealScope);
                                            if (tISBSender != null && tISBSender.Count > 0)
                                            {
                                                foreach (TheQueuedSender tSend in tISBSender)
                                                {
                                                    if (tSend.MySubscriptionContains(tTopicNameOnly, tTopicRealScope, true))
                                                    {
                                                        tSend?.MyISBlock?.FireEvent("TSMReceived", new TheProcessMessage() { Topic = tTopic, Message = TSM.Clone(pMessage, true), LocalCallback = pLocalCallback });
                                                        bFoundISB = true;
                                                    }
                                                }
                                            }
                                            if (bFoundISB)
                                                return true;
                                        }
                                        if (tBaseE == null)
                                            TheBaseAssets.MySYSLOG.WriteToLog(2324, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Message to {myTargetNodeChannel.ToMLString()} Message REJECTED - Topic not found on this (Localhost) node", eMsgLevel.l2_Warning), true);
                                        else
                                            TheBaseAssets.MySYSLOG.WriteToLog(2324, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Message to {myTargetNodeChannel.ToMLString()} Message REJECTED - target has different scope and is not allowed for this node", eMsgLevel.l2_Warning), true);
                                        return false;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (!TheBaseAssets.MyServiceHostInfo.IsNodePermitted(myTargetNodeChannel.cdeMID))
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(2325, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Message with TXT ({pMessage.TXT}) to {myTargetNodeChannel.ToMLString()} REJECTED - target does not have the subscription ({tTopicNameOnly}) required sent by {TheCommonUtils.GetDeviceIDML(pMessage.GetOriginator())}", eMsgLevel.l2_Warning), true);
                                return false;
                            }
                        }
                    }
                }
                else
                {
                    if (tHasDirectAddress)
                    {
                        if (string.IsNullOrEmpty(tTopicRealScope))
                        {
                            if (TheBaseAssets.MyServiceHostInfo.IsCloudService && pMessage.GetOriginator() == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)
                                tTopicRealScope = myTargetNodeChannel.RealScopeID;  //RScope-OK: If Scope is not set on a topic but sent from the cloud to this target node, the target RScope has to be added
                        }
                        if (pMessage.GetOriginator() != TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID && tDirectGuid == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)
                        {
                            List<TheQueuedSender> tISBSender = TheQueuedSenderRegistry.GetISBConnectSender(tTopicRealScope);
                            if (tISBSender != null && tISBSender.Count > 0)
                            {
                                foreach (TheQueuedSender tSend in tISBSender)
                                {
                                    tSend?.MyISBlock?.FireEvent("TSMReceived", new TheProcessMessage() { Topic = tTopic, Message = TSM.Clone(pMessage, true), LocalCallback = pLocalCallback });
                                }
                                return true;
                            }
                        }
                        if (!string.IsNullOrEmpty(tTopicRealScope) &&
                            (!(TheBaseAssets.MyServiceHostInfo.IsCloudService && TheCommonUtils.IsLocalhost(myTargetNodeChannel.cdeMID))) &&
                            (!(myTargetNodeChannel.HasRScope(tTopicRealScope) || myTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE))
                            )
                        {
                            //Check if other QSender have ISB Connect with the scope in question
                            if (!TSM.L(eDEBUG_LEVELS.FULLVERBOSE))
                            {
                                if (!string.IsNullOrEmpty(tTopicRealScope))
                                    tTopicRealScope = tTopicRealScope.Substring(0, 4);
                                TheBaseAssets.MySYSLOG.WriteToLog(2325, new TSM("QueuedSender", $"DirectPub-Message to ({tDirectGuid}) via Relay-Type={ myTargetNodeChannel.SenderType} REJECTED - relay {myTargetNodeChannel} has different scope ENG:{pMessage.ENG} - {pMessage.TXT} RScope:{myTargetNodeChannel.GetScopes()} tScope:{tTopicRealScope?.Substring(0,4).ToUpper()}", eMsgLevel.l2_Warning), true);
                            }
                            return false;
                        }
                    }
                }
            }

            //Last Change before Finalizing for July 2012 Detroit trip
            if (
                    !(
                        ((TheBaseAssets.MyServiceHostInfo.IsCloudService && TheBaseAssets.MyServiceHostInfo.IsNodePermitted(pMessage.GetOriginator())) || myTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE) && string.IsNullOrEmpty(myTargetNodeChannel.RealScopeID)
                    )
                    && !string.IsNullOrEmpty(myTargetNodeChannel.RealScopeID)   //TODO: If the Localhost IsCloudService and its NOT scoped, the telegram is not blocked. That results in all Meshes get to see the Cloud Plugin NMI. If we remove this, No Cloud NMI is visible except if "AllowForeignScopeIDRouting" is true
                    && !myTargetNodeChannel.HasRScope(tTopicRealScope)
                )
            {
                if (!TheBaseAssets.MyServiceHostInfo.AllowForeignScopeIDRouting)
                {
                    if (!
                                (eEngineName.ContentService.Equals(pMessage.ENG) && pMessage.TXT.StartsWith("CDE_UPD_ADMIN") && !TheBaseAssets.MyServiceHostInfo.IsCloudService && pMessage.IsFirstNode())
                            &&
                                !tTopicNameOnly.StartsWith("LOGIN_SUCCESS")
                            && // TODO Code Review - additional checks? Better ways of getting success notifications to Mesh Manager?
                                !(tDirectGuid == myTargetNodeChannel.cdeMID && (pMessage.TXT.StartsWith("CDE_INIT_SYNC") || pMessage.TXT.StartsWith("CDE_SYNCUSER") || pMessage.TXT.StartsWith("NMI_SYNCBLOCKS")))
                       )    //TODO:SECVAL
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2326, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Message from {TheCommonUtils.GetDeviceIDML(pMessage.ORG)} to {myTargetNodeChannel} (type={myTargetNodeChannel.SenderType}) rejected - target has different scope", eMsgLevel.l2_Warning, $"{pMessage.TXT}"), true);
                        return false;
                    }
                }
                else
                {
                    if (tHasDirectAddress)
                        pTopic = TheBaseAssets.MyScopeManager.AddScopeID(pTopic, myTargetNodeChannel.RealScopeID,ref pMessage.SID, true, true);    //RScope-OK: Scope Change to Primary RScope
                }
            }

            if (!IsAlive)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2327, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"QSender to {myTargetNodeChannel.ToMLString()} is dead! Fireing SenderProblem", eMsgLevel.l1_Error), true);
                FireSenderProblem(new TheRequestData() { ErrorDescription = $"1301:QSender to {myTargetNodeChannel.ToMLString()} is dead! Fireing SenderProblem" });
                return false;
            }

            if (!((!pMessage.DoesORGContain(myTargetNodeChannel.cdeMID) && !pMessage.DoesORGContain(MyTargetNodeChannel.TruDID)) || (tHasDirectAddress && tDirectGuid== myTargetNodeChannel.cdeMID)))
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2328, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("CoreComm", $"Target {myTargetNodeChannel.ToMLString()} found in Hubs ORG:{pMessage.ORG} - {pMessage.TXT} Will be ignored", eMsgLevel.l7_HostDebugMessage), true);//ORG-OK
                return false;
            }

            if (pMessage.DoNotRelay() && (myTargetNodeChannel.SenderType == cdeSenderType.CDE_CLOUDROUTE || myTargetNodeChannel.SenderType == cdeSenderType.CDE_BACKCHANNEL))  //NEW:2012-5-10 no NEWURL via the cloud
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2329, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Topic({tTopicNameOnly} from ORG:{TheCommonUtils.GetDeviceIDML(pMessage.ORG)} will not be relayed to {myTargetNodeChannel.ToMLString()} due to TSM setting: DoNotRelay", eMsgLevel.l2_Warning), true);//ORG-OK
                return false;
            }

            if (pMessage.ToCloudOnly() && myTargetNodeChannel.SenderType != cdeSenderType.CDE_CLOUDROUTE)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(23210, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Topic({tTopicNameOnly} from ORG:{TheCommonUtils.GetDeviceIDML(pMessage.ORG)} will not be relayed to {myTargetNodeChannel.ToMLString()} due to TSM setting: ToCloudOnly", eMsgLevel.l2_Warning), true);//ORG-OK
                return false;
            }
            if (pMessage.ToRelayOnly() && myTargetNodeChannel.SenderType != cdeSenderType.CDE_BACKCHANNEL)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(23211, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Topic({tTopicNameOnly} from ORG:{TheCommonUtils.GetDeviceIDML(pMessage.ORG)} will not be relayed to {myTargetNodeChannel.ToMLString()} due to TSM setting: ToRelayOnly", eMsgLevel.l2_Warning), true);//ORG-OK
                return false;
            }

            if (pMessage.ToServiceOnly() && TheCommonUtils.IsDeviceSenderType(myTargetNodeChannel.SenderType))  //IDST-OK: dont send to devices if ToServiceOnly is set
            {
                TheBaseAssets.MySYSLOG.WriteToLog(23212, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("QueuedSender", $"Topic({tTopicNameOnly} from ORG:{TheCommonUtils.GetDeviceIDML(pMessage.ORG)} will not be relayed to {myTargetNodeChannel.ToMLString()} due to TSM setting: ToServiceOnly", eMsgLevel.l7_HostDebugMessage, "TXT:" + pMessage.TXT), true);//ORG-OK
                return false;
            }

            if (tHasDirectAddress && TheCommonUtils.IsDeviceSenderType(myTargetNodeChannel.SenderType))     //IDST-OK: Quick check if the TSM with a direct address is for this QS since it will not relay
            {
                if (!tDirectGuid.Equals(myTargetNodeChannel.cdeMID) && !tDirectGuid.Equals(myTargetNodeChannel.TruDID))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(23213, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("QueuedSender", $"Topic({tTopicNameOnly} from ORG:{TheCommonUtils.GetDeviceIDML(pMessage.ORG)} will not be relayed to {myTargetNodeChannel.ToMLString()} due to designated Target in Topic", eMsgLevel.l2_Warning), true);//ORG-OK
                    return false;
                }
            }

            if (WasTSMSeenBefore(pMessage, tTopicRealScope,true))
            {
                TheBaseAssets.MySYSLOG.WriteToLog(23214, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Topic({tTopicNameOnly} from ORG:{TheCommonUtils.GetDeviceIDML(pMessage.ORG)} will not be relayed to {myTargetNodeChannel.ToMLString()} Was sent before!", eMsgLevel.l2_Warning), true);//ORG-OK
                return false;
            }
            try
            {
                if (myTargetNodeChannel.SenderType == cdeSenderType.CDE_LOCALHOST)
                {
                    if (pMessage.ToNodesOnly() && !TheCommonUtils.IsDeviceSenderType(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType)) //IDST-OK: if this node is not a device and ToNodesOnly is set, the TSM should not get to this node
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(23215, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("QueuedSender", $"Topic({tTopicNameOnly} from ORG:{TheCommonUtils.GetDeviceIDML(pMessage.ORG)} will not be relayed to {myTargetNodeChannel} due to TSM setting: ToNodesOnly", eMsgLevel.l7_HostDebugMessage), true);//ORG-OK
                        return false;
                    }
                    if (tHasDirectAddress && !TheCommonUtils.IsLocalhost(tDirectGuid))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(23216, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"Message has designated address and is not for this LOCAL-HOST. (Topic:{tTopicNameOnly} from ORG:{TheCommonUtils.GetDeviceIDML(pMessage.ORG)}) to Relay", eMsgLevel.l7_HostDebugMessage), true);//ORG-OK
                        return false;
                    }
                    if (pMessage.DoesORGContainLocalHost())
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(23217, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"Message Originating from LOCAL-HOST - not sent to LH again. (Topic:{tTopicNameOnly} from ORG:{TheCommonUtils.GetDeviceIDML(pMessage.ORG)}) to Relay", eMsgLevel.l7_HostDebugMessage), true);//ORG-OK
                        return false;
                    }

                    // CODE REVIEW Markus: This overlaps with the processing in TheBaseEngine.ProcessMessage: should we refactor?
                    IBaseEngine tBase = TheThingRegistry.GetBaseEngine(pMessage.ENG);
                    if (tBase != null && pMessage.ToServiceOnly() && !(tBase.GetEngineState().IsService))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(23218, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CoreComm", $"Message with topic:{tTopicNameOnly} from ORG:{TheCommonUtils.GetDeviceIDML(pMessage.ORG)} not meant for this localhost because it is not a LiveEngine", eMsgLevel.l7_HostDebugMessage), true);//ORG-OK
                        return false;
                    }
                    TSM tSendMessage = TSM.Clone(pMessage, true);
                    if (tSendMessage.ORG != TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString())   //Performance Boost
                        tSendMessage.AddHopToOrigin();
                    TheCDEKPIs.IncrementKPI(eKPINames.QSLocalProcessed);
                    TheCommonUtils.cdeRunAsync("QS-SQ-PMLocal", true, o =>
                    {
                    bool WasThing = false;
                    //NEW in V3: If Owner is set, message is only routed to Thing
                    if (!string.IsNullOrEmpty(tSendMessage.OWN))
                    {
                        TheThing tThing;
                        if (tSendMessage.TXT != null && tSendMessage.TXT.StartsWith("SETP"))    //ThingProperties:SETP
                        {
                            tThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(tSendMessage.OWN), true, true);
                            if (tThing != null) // && tThing.cdeO != TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)
                            {
                                string[] tProps = TheCommonUtils.cdeSplit(tSendMessage.PLS, ":;:", false, false);
                                for (int i = 0; i < tProps.Length; i++)
                                {
                                    int pos = tProps[i].IndexOf('=');
                                    WasThing = true;
                                        if (pos < 0)
                                        {
                                            if (tThing.HasLiveObject)
                                                (tThing.GetObject() as ICDEThing).SetProperty(cdeP.GetPropName(tProps[i]), true); //DANGER: Could result in Feedback loop as tSendMessage.ORG is not propagated. TFS Bug 1377
                                            else
                                                tThing.SetProperty(cdeP.GetPropName(tProps[i]), true, ePropertyTypes.TBoolean, DateTimeOffset.MinValue, -1, null, tSendMessage.ORG);
                                        }
                                        else
                                        {
                                            if (pos > 0 && pos < tProps[i].Length - 1)
                                            {
                                                if (tThing.HasLiveObject)
                                                    (tThing.GetObject() as ICDEThing).SetProperty(cdeP.GetPropName(tProps[i].Substring(0, pos)), tProps[i].Substring(pos + 1));
                                                else
                                                    tThing.SetProperty(cdeP.GetPropName(tProps[i].Substring(0, pos)), tProps[i].Substring(pos + 1), ePropertyTypes.NOCHANGE, DateTimeOffset.MinValue, -1, null, tSendMessage.ORG);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // TODO Code Review - Is this an appropriate way to allow cross-engine messages?
                                // CM: yes, Code moved here as SET P has to be handled first and not processed by HandleMessage of things
                                //TheThing tThing = TheThingRegistry.GetThingByMID(tSendMessage.ENG,TheCommonUtils.CGuid(tSendMessage.OWN),true, true);
                                tThing = TheThingRegistry.GetThingByMID(TheCommonUtils.CGuid(tSendMessage.OWN));
                                if (tThing != null)
                                {
                                    WasThing = true;
                                    TheThing.HandleByThing(tThing, tTopic, tSendMessage, pLocalCallback);
                                    //DANGER: too many events - needs improvement: TFS Bug 1375
                                    //tThing.FireEvent(tSendMessage.TXT, tThing, new TheProcessMessage() { Topic = tTopic, Message = tSendMessage, LocalCallback = pLocalCallback, CurrentUserID=TheCommonUtils.CGuid(tSendMessage.UID) }, true); //In HandleByThing
                                    if (tThing != TheCDEngines.MyNMIService?.GetBaseThing())
                                        TheCDEngines.MyNMIService?.FireEvent(tSendMessage.TXT, tThing,
                                        new TheProcessMessage() { Topic = tTopic, Message = tSendMessage, LocalCallback = pLocalCallback, CurrentUserID = TheCommonUtils.CGuid(tSendMessage.UID) }, true);
                                }
                            }
                        }
                        if (!WasThing)
                        {
                            if (tBase == null)
                            {
                                if (TheCDEngines.MyContentEngine != null)
                                    TheCDEngines.MyContentEngine.GetBaseEngine().ProcessMessage(new TheProcessMessage() { Topic = tTopic, Message = tSendMessage, LocalCallback = pLocalCallback });
                            }
                            else
                                tBase.ProcessMessage(new TheProcessMessage() { Topic = tTopic, Message = tSendMessage, LocalCallback = pLocalCallback });
                        }
                        MyISBlock?.FireEvent("TSMReceived", new TheProcessMessage() { Topic = tTopic, Message = tSendMessage, LocalCallback = pLocalCallback });
                    });
                    return true;
                }
                if (!tHasDirectAddress && MyISBlock != null && TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID != pMessage.GetOriginator())
                {
                    MyISBlock?.FireEvent("TSMReceived", new TheProcessMessage() { Topic = tTopic, Message = TSM.Clone(pMessage, true), LocalCallback = pLocalCallback });
                    //return true; This prevents relaying of ISB Messages messages
                }
                int tHash = pMessage.GetHash(tTopicNameOnly + tDirectGuid.ToString());

                //NEWV4: NMIService SETP: and SETNP: Optimization
                if (pMessage.ENG == eEngineName.NMIService)
                {
                    bool IsSetpWithTHing = (pMessage.TXT.StartsWith("SETP:") && pMessage.TXT.Length > 40);
                    if (myTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON)
                    {
                        if (pMessage.TXT.StartsWith("NMI_SCREENMETA") || pMessage.TXT.StartsWith("NMI_LIVESCREENMETA"))// && !string.IsNullOrEmpty(pMessage.PLS) && pMessage.PLS.Length>2)
                        {
                            var ttt = TheCommonUtils.DeserializeJSONStringToObject<TheScreenInfo>(pMessage.PLS);
                            if (ttt?.MyStorageInfo != null && ttt.MyStorageInfo.Count > 0)
                            {
                                foreach (var tForm in ttt.MyStorageInfo)
                                {
                                    if (!MyJSKnownThings.ContainsKey(tForm.cdeO))
                                        MyJSKnownThings.TryAdd(tForm.cdeO,0);
                                    foreach (var tFld in tForm.FormFields)
                                    {
                                        if (!MyJSKnownThings.ContainsKey(tFld.cdeO))
                                            MyJSKnownThings.TryAdd(tFld.cdeO,0);
                                        if (!MyJSKnownFields.ContainsID(tFld.cdeMID))
                                            MyJSKnownFields.UpdateItem(tFld, null);
                                    }
                                }
                            }
                            if (ttt?.MyDashPanels != null && ttt.MyDashPanels.Count > 0)
                            {
                                foreach (var tDashPanel in ttt.MyDashPanels)
                                {
                                    if (!MyJSKnownThings.ContainsKey(tDashPanel.cdeO))
                                        MyJSKnownThings.TryAdd(tDashPanel.cdeO,0);
                                    if (!MyJSKnownFields.ContainsID(tDashPanel.cdeMID))
                                        MyJSKnownFields.UpdateItem(tDashPanel, null);
                                }
                            }
                        }
                        if (IsSetpWithTHing)
                        {
                            var town = TheCommonUtils.CGuid(pMessage.TXT.Split(':')[1]);
                            if (!MyJSKnownThings.ContainsKey(town) && !TheFormsGenerator.IsOwnerKnown(town))
                                return false;
                        }
                        if (pMessage.TXT.StartsWith("SETNP"))
                        {
                            if (!MyJSKnownFields.ContainsID(TheCommonUtils.CGuid(pMessage.OWN)) && pMessage.TXT.Split(':').Length<3)
                                return false;
                        }
                    }

                    if (MyCoreQueue.MyRecords.Any())
                    {
                        if (IsSetpWithTHing)
                        {
                            bool bExit = false;
                            MyCoreQueue.MyRecordsRWLock.RunUnderUpgradeableReadLock(() =>
                            //lock (MyCoreQueue.MyRecordsLock)    //LOCK-REVIEW: This is a logic lock. Consitency of the queue has to be maintained here but only between this function and the GetNextMessage function
                            {
                                if (EnableKPIs)
                                {
                                    var spmax = MyCoreQueue.MyRecords.Values.Count(s => !s.IsSent && s.OrgMessage != null && s.OrgMessage.TXT != null && s.OrgMessage.TXT.StartsWith("SETP"));
                                    if (spmax > TheCDEKPIs.KPI7) TheCDEKPIs.SetKPI(eKPINames.KPI7, spmax);
                                }

                                var tM = MyCoreQueue.MyRecords.Values.FirstOrDefault(s => !s.IsSent && s.OrgMessage?.TXT?.StartsWith(pMessage.TXT.Substring(0, 41)) == true);
                                if (tM != null && tM.OrgMessage != null)
                                {
                                    tM.IsLocked = true;
                                    var tNP = TheCommonUtils.cdeSplit(pMessage.PLS, ":;:", false, false);
                                    var tSP = TheCommonUtils.cdeSplit(tM.OrgMessage.PLS, ":;:", false, false).ToList();
                                    if (EnableKPIs && tSP.Count > TheCDEKPIs.KPI9) TheCDEKPIs.SetKPI(eKPINames.KPI9, tSP.Count);
                                    string tName = "";
                                    foreach (string tP in tNP)
                                    {
                                        tName = tP.Split('=')[0];
                                        if (tName.Length == tP.Length) continue;
                                        if (tSP.Any(s => s.StartsWith(tName)))
                                        {
                                            tSP.RemoveAll(s => s.StartsWith(tName));
                                            tSP.Add(tP);
                                        }
                                        else
                                        {
                                            tSP.Add(tP);
                                        }
                                    }
                                    tM.OrgMessage.PLS = TheCommonUtils.CListToString(tSP, ":;:");
                                    tM.IsLocked = false;
                                    //TheSystemMessageLog.ToCo($"New:{tM.OrgMessage.PLS}  NewMsg:{pMessage.PLS}");
                                    bExit = true;
                                }
                                else
                                {
                                    if (EnableKPIs) TheCDEKPIs.IncrementKPI(eKPINames.KPI10);
                                }
                            });
                            if (bExit)
                            {
                                return false;
                            }
                        }
                        if (pMessage.TXT.StartsWith("SETNP"))
                        {
                            bool bExit = false;
                            MyCoreQueue.MyRecordsRWLock.RunUnderUpgradeableReadLock(() =>
                            //lock (MyCoreQueue.MyRecordsLock)    //LOCK-REVIEW: This is a logic lock. Consitency of the queue has to be maintained here but only between this function and the GetNextMessage function
                            {
                                if (EnableKPIs)
                                {
                                    var spnmax = MyCoreQueue.MyRecords.Values.Count(s => !s.IsSent && s.OrgMessage != null && s.OrgMessage.TXT != null && s.OrgMessage.TXT.StartsWith("SETNP"));
                                    if (spnmax > TheCDEKPIs.KPI8) TheCDEKPIs.SetKPI(eKPINames.KPI8, spnmax);
                                }

                                TheCoreQueueContent tM = MyCoreQueue.MyRecords.Values.FirstOrDefault(s => !s.IsSent && s.OrgMessage != null && s.OrgMessage.TXT?.StartsWith("SETNP") == true && s.OrgMessage.OWN == pMessage.OWN);
                                if (tM != null && tM.OrgMessage != null)
                                {
                                    tM.IsLocked = true;
                                    var tNP = TheCommonUtils.cdeSplit(pMessage.PLS, ":;:", false, false);
                                    var tSP = TheCommonUtils.cdeSplit(tM.OrgMessage.PLS, ":;:", false, false).ToList();
                                    string tName = "";
                                    foreach (string tP in tNP)
                                    {
                                        var Parts = tP.Split('=');
                                        tName = Parts[0];
                                        var tVal = Parts[1];
                                        if (tName.Length == tP.Length)
                                            continue;
                                        if (!tSP.Any(s => s.StartsWith(tName)) || tVal.StartsWith("{") || tVal.StartsWith("["))
                                            tSP.Add(tP);
                                        else
                                        {
                                            tSP.RemoveAll(s => s.StartsWith(tName));
                                            tSP.Add(tP);
                                        }
                                    }
                                    tM.OrgMessage.PLS = TheCommonUtils.CListToString(tSP, ":;:");
                                    tM.IsLocked = false;
                                    if (tM.IsSent)
                                        TheBaseAssets.MySYSLOG.WriteToLog(23219, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Message already Sent - this should NOT EVER HAPPEN {tM}", eMsgLevel.l1_Error), true);
                                    bExit = true;
                                }
                            });
                            if (bExit)
                            {
                                return false;
                            }
                        }
                    }
                }

                    DateTimeOffset tOldStamp = DateTimeOffset.MinValue;
                if (pMessage.NotToSendAgain() || pMessage.NoDuplicates())
                {
                    List<TheCoreQueueContent> tQ = MyCoreQueue.TheValues;
                    if (pMessage.NotToSendAgain())
                    {
                        if (tQ.Any(s => s.HashID == tHash))
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(23219, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Message already Queued for sending to {myTargetNodeChannel.ToMLString()} Q-Length:{tQ.Count}", eMsgLevel.l5_HostMessage, $"TXT:{pMessage.TXT} will be ignored"), true);
                            return false;
                        }
                    }
                    if (pMessage.NoDuplicates())
                    {
                        var tQi = tQ.FirstOrDefault(s => s.HashID == tHash);
                        if (tQi!=null)
                        {
                            tOldStamp = tQi.EntryTime;
                            TheBaseAssets.MySYSLOG.WriteToLog(23219, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"Message already Queued for sending to {myTargetNodeChannel.ToMLString()} Q-Length:{tQ.Count}", eMsgLevel.l5_HostMessage, $"TXT:{pMessage.TXT} will be replaced with new TSM"), true);
                            MyCoreQueue.RemoveAnItem(tQi, null);
                        }
                    }
                }
                if (MyCoreQueue.Count > TheBaseAssets.MyServiceHostInfo.SenderQueueSize) // Count is an expensive call under stress (takes concurrentdictionary lock)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(23220, new TSM("QueuedSender", $"Queue for {myTargetNodeChannel} too full (>{TheBaseAssets.MyServiceHostInfo.SenderQueueSize} entries), will be flushed", eMsgLevel.l1_Error), true);
                    FlushQueue();
                    FireSenderProblem(new TheRequestData() { ErrorDescription = $"1302:Queue for {myTargetNodeChannel} too full (>{TheBaseAssets.MyServiceHostInfo.SenderQueueSize} entries) - QSender will close" });
                    IsInPost = false;
                    IsInWSPost = false;
                    return false;
                }
                if (string.IsNullOrEmpty(pMessage.FID))
                    TheCDEKPIs.IncTSMByEng(pMessage.ENG);

                TSM MsgToQueue = TSM.Clone(pMessage, true);
                if (MsgToQueue.UnsubscribeAfterPublish() && MsgToQueue.QDX > 4 && !TheBaseAssets.MyServiceHostInfo.DisablePriorityInversion)
                    MsgToQueue.QDX = 3;

                byte[] PayLoadBytes = null;
                int PayLoadBytesLength = 0;
                bool IsPLSCompressed = false;
                if (!string.IsNullOrEmpty(MsgToQueue.PLS) && (MsgToQueue.ReadyToEncryptPLS() && !MsgToQueue.IsPLSEncrypted()))
                {
                    MsgToQueue.PLS = TheCommonUtils.cdeEncrypt(MsgToQueue.PLS, TheBaseAssets.MySecrets.GetAI());     //3.083: Must be cdeAI //4.210: We could use the scopeID as key here if we dont decrypt/encrypt on relaying nodes but not sure if this adds value. Also cloud would have to unecrypt when receiving as it has no scopeID
                    MsgToQueue.PLSWasEncrypted();
                }
                if (MsgToQueue.PLB == null || MsgToQueue.PLB.Length == 0)                   //NEW: New Architecture allow to send raw bytes...need to have "CDEBIN" in PLS
                {
                    if (MsgToQueue !=null && TheBaseAssets.MyServiceHostInfo.DisablePLSCompression && !string.IsNullOrEmpty(MsgToQueue.PLS) && MsgToQueue.PLS.Length > 512 && myTargetNodeChannel != null && myTargetNodeChannel.SenderType != cdeSenderType.CDE_JAVAJASON)
                    {
                        PayLoadBytes = TheCommonUtils.cdeCompressString(MsgToQueue.PLS);
                        IsPLSCompressed = true;
                        PayLoadBytesLength = PayLoadBytes.Length;
                        TheCDEKPIs.IncrementKPI(eKPINames.QSCompressedPLS);
                    }
                }
                else
                {
                    PayLoadBytes = MsgToQueue.PLB;
                    if (myTargetNodeChannel.SenderType != cdeSenderType.CDE_JAVAJASON)
                        PayLoadBytesLength = PayLoadBytes.Length;
                    else
                        PayLoadBytesLength = PayLoadBytes.Length;
                }
                int PayLen = PayLoadBytesLength;
                int tTeles = 1;
                int curLen = PayLen;
                if (!TheBaseAssets.MyServiceHostInfo.DisableChunking && !pTopic.Contains(":,:") && !MsgToQueue.DoNotChunk())
                {
                    tTeles = ((PayLen / TheBaseAssets.MAX_MessageSize[(int)myTargetNodeChannel.SenderType]) + 1);
                    curLen = TheBaseAssets.MAX_MessageSize[(int)myTargetNodeChannel.SenderType];
                }
                if (tTeles > 1)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(23221, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QueuedSender", $"VERY Large Message - will be chunked! Len={PayLen} but CurrentMax={TheBaseAssets.MAX_MessageSize[(int)myTargetNodeChannel.SenderType]} with Topic:{tTopicNameOnly} with TXT:{pMessage.TXT} added for :{myTargetNodeChannel}", eMsgLevel.l2_Warning), true);
                    if (!TheBaseAssets.MyServiceHostInfo.DisablePriorityInversion)
                        MsgToQueue.QDX += tTeles % 3;
                }
                int curPos = 0;
                int PackCnt = 0;
                string tGuid = Guid.NewGuid().ToString();
                TSM tFinalTSM = MsgToQueue;
                while (curPos < PayLen || PayLen == 0)
                {
                    if (curPos + curLen > PayLen)
                        curLen = PayLen - curPos;
                    TheCoreQueueContent tQue = new TheCoreQueueContent();
                    if (tTeles > 1)
                    {
                        tQue.IsChunked = true;
                        tFinalTSM = TSM.Clone(MsgToQueue, false);
                    }
                    if (tTeles == 1 && pTopic?.Contains(":,:") == true)
                    {
                        tQue.Topic = pTopic; // We are resending a previously chunked message (i.e. MSBGateWay republishing a chunked TheDeviceMessage): the optic already contains chunk information, so we must not add on another
                    }
                    else
                    {
                        tQue.Topic = pTopic + ":,:" + PackCnt.ToString() + ":,:" + tTeles.ToString() + ":,:" + tGuid;
                    }
                    if (tOldStamp == DateTimeOffset.MinValue)
                        tQue.EntryTime = DateTimeOffset.Now;
                    else
                        tQue.EntryTime = tOldStamp;
                    tQue.QueueIdx = MsgToQueue.QDX;
                    //tQue.Method = pMethod;
                    tQue.HashID = tHash;
                    tQue.SubMsgCnt = PackCnt;
                    tQue.sn = Interlocked.Increment(ref QueueSerial);
                    tQue.OrgMessage = tFinalTSM;
                    if (tQue.OrgMessage.ORG == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString())   //Performance Boost
                    {
                        //if (MyTargetNodeChannel.SenderType == cdeSenderType.CDE_CUSTOMISB)  //If ISB we need to use the cdeMID of the TNC instead of the DeviceID
                        //{
                        //    tQue.OrgMessage.ORG = MyTargetNodeChannel.cdeMID.ToString();
                        //    if (TheBaseAssets.MyServiceHostInfo.EnableCosting)
                        //        TheCDEngines.updateCosting(tQue.OrgMessage);
                        //}
                    }
                    else
                        tQue.OrgMessage.AddHopToOrigin();
                    if (TheBaseAssets.MyServiceHostInfo.IsCloudService)
                        tQue.OrgMessage.SetSendViaCloud();
                    if (curLen > 0 && PayLoadBytes != null)
                    {
                        // CODE REVIEW - MH: Avoid this copy, i.e.
                        if (curPos == 0 && curLen == PayLoadBytes.Length)
                        {
                            tFinalTSM.PLB = PayLoadBytes;
                        }
                        else
                        {
                            tFinalTSM.PLB = new byte[curLen];
                            TheCommonUtils.cdeBlockCopy(PayLoadBytes, curPos, tFinalTSM.PLB, 0, curLen);
                        }
                        if (IsPLSCompressed)
                            tFinalTSM.PLS = "";
                    }

                    MyCoreQueue.AddAnItem(tQue, null);
                    TheCDEKPIs.IncrementKPI(eKPINames.QSQueued);
                    curPos += curLen;
                    if (PayLen == 0) break;
                    PackCnt++;
                }
                myTargetNodeChannel.LastInsertTime = DateTimeOffset.Now;

                if (MsgToQueue.UnsubscribeAfterPublish())
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(23222, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"Topic ({tTopicNameOnly}) will auto-unsubscribe UAP={MsgToQueue.UnsubscribeAfterPublish()}", eMsgLevel.l7_HostDebugMessage));
                    Unsubscribe(pTopic);
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(23223, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", $"Fatal Error in Send-Queued! to {myTargetNodeChannel} - Topic:{tTopicNameOnly} ORG:{TheCommonUtils.GetDeviceIDML(pMessage.ORG)}", eMsgLevel.l1_Error, $"TXT:{pMessage.TXT} Error:{e}"));//ORG-OK
            }
            return true;
        }

        private TheCoreQueueContent GetNextMessage(out int pQCount)
        {
            TheCoreQueueContent tMsg = null;
            int pQCountRet = 0;
            MyCoreQueue.MyRecordsRWLock.RunUnderUpgradeableReadLock(() =>
            //lock (MyCoreQueue.MyRecordsLock)    //LOCK-REVIEW: Must remain main lock - this is a Logic Lock - not a database lock
            {
            var tQKV = MyCoreQueue.MyRecords.GetDynamicEnumerable(); // MH: .Values; also too expensive - makes a copy //.TheValues.ToList();  //CM: To expensive: Lock already on the block
            if (tQKV.Any())
            {
                    try
                    {
                        if (TheBaseAssets.MyServiceHostInfo.EnforceFifoQueue)
                            tMsg = tQKV.Where(s => !s.Value.IsLocked).OrderBy(s => s.Value.QueueIdx).ThenBy(x => x.Value.sn).First().Value;
                        else
                        {
                            DateTimeOffset tNow = DateTimeOffset.Now; //Otherwise it will be called for every record in Linq again
                            if (TheBaseAssets.MyServiceHostInfo.PrioInversionTime > 0 && tQKV.Where(s => tNow.Subtract(s.Value.EntryTime).TotalSeconds > TheBaseAssets.MyServiceHostInfo.PrioInversionTime).Any())
                                tMsg = tQKV.Where(s => !s.Value.IsLocked && tNow.Subtract(s.Value.EntryTime).TotalSeconds > TheBaseAssets.MyServiceHostInfo.PrioInversionTime).OrderBy(s => s.Value.EntryTime).First().Value;
                            else
                            {
                                TheCoreQueueContent tMsgQ3 = null;
                                foreach (var qcKV in tQKV)
                                {
                                    var qc = qcKV.Value;
                                    if (qc.IsLocked) continue;
                                    if (tMsg == null || tMsg.QueueIdx > qc.QueueIdx || tMsg.EntryTime < qc.EntryTime)
                                    {
                                        tMsg = qc;
                                    }
                                    if (qc.QueueIdx > 2 && (tMsgQ3 == null || tMsgQ3.QueueIdx > qc.QueueIdx || tMsgQ3.EntryTime > qc.EntryTime))
                                    {
                                        tMsgQ3 = qc;
                                    }
                                }
                                if (tMsg.QueueIdx > 2)
                                {
                                    tMsg = tMsgQ3;
                                }
                            }
                        }
                        tMsg.IsSent = true;
                        if (tMsg.IsLocked)
                            TheBaseAssets.MySYSLOG.WriteToLog(23224, new TSM("QueuedSender", $"Get Next Message - message is locked but ready to be sent - THIS SHOULD NEVER HAPPEN! {tMsg}", eMsgLevel.l1_Error));
                        MyCoreQueue.RemoveAnItem(tMsg, null);
                    }
                    catch (Exception ee)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(23224, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("QueuedSender", $"Get Next Message failed for channel {MyTargetNodeChannel?.ToMLString()}", eMsgLevel.l2_Warning, ee.ToString()));//ORG-OK
                    }
                }
                pQCountRet = MyCoreQueue.Count;
            });
            pQCount = pQCountRet;
            return tMsg;
        }

        internal void GetNextBackChannelBuffer(TheRequestData pRequestData)
        {
            pRequestData.ResponseBuffer = null;
            if (MyWebSocketProcessor != null)
            {
                // CODE REVIEW MH: Why return silently here? CM: This function does not do anything with WS...its for HTTP Protocol only
                pRequestData.WebSocket = MyWebSocketProcessor;
                return;
            }
            if (MyCoreQueue != null && MyCoreQueue.Count>0)
            {
                StringBuilder tSendBufferStr = CreateDeviceMessageBuffer(pRequestData);

                if (tSendBufferStr.Length > 2)
                {
                    if (MyTargetNodeChannel?.SenderType == cdeSenderType.CDE_JAVAJASON || MyTargetNodeChannel?.SenderType == cdeSenderType.CDE_MINI || TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType == cdeSenderType.CDE_MINI)
                    {
                        pRequestData.ResponseBuffer = TheCommonUtils.CUTF8String2Array(tSendBufferStr.ToString());
                        pRequestData.ResponseMimeType = "application/json";
                    }
                    else
                    {
                        pRequestData.ResponseBuffer = TheCommonUtils.cdeCompressString(tSendBufferStr.ToString());
                        pRequestData.ResponseMimeType = "application/x-gzip";
                    }
                }
            }
        }

        private StringBuilder CreateDeviceMessageBuffer(TheRequestData pRequestData)
        {
            int IsBatchOn = 0;
            var myTargetNodeChannel = MyTargetNodeChannel;
            StringBuilder tSendBufferStr = new StringBuilder(TheBaseAssets.MyServiceHostInfo?.IsMemoryOptimized == true ? 1024 : TheBaseAssets.MAX_MessageSize[(int)myTargetNodeChannel.SenderType] * 2);
            tSendBufferStr.Append("[");
            do
            {
                TheCoreQueueContent tMsg = GetNextMessage(out int MCQCount);

                if (tMsg != null && tMsg.OrgMessage != null)
                {
                    TheCDEKPIs.IncrementKPI(eKPINames.QSSent);
                    tMsg.OrgMessage.GetNextSerial(tMsg.SubMsgCnt);
                    TheBaseAssets.MySYSLOG.WriteToLog(23225, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("QueuedSender", $"GetNextBackChannelBuffer to {myTargetNodeChannel.ToMLString()} relayed from ORG:{TheCommonUtils.GetDeviceIDML(tMsg.OrgMessage.ORG)} FID:{tMsg.OrgMessage.FID}", eMsgLevel.l7_HostDebugMessage, $"T={tMsg.Topic} P={tMsg.OrgMessage.TXT}"));//ORG-OK
                    TheDeviceMessage tDev = new TheDeviceMessage();
                    //tDev.CNT = MCQCount;
                    if (myTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON)
                        tDev.DID = myTargetNodeChannel.cdeMID.ToString();
                    else
                    {
                        tDev.DID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString();
                        if (!string.IsNullOrEmpty(tMsg?.OrgMessage?.SID))
                            tDev.SID = tMsg?.OrgMessage?.SID;
                        else
                        {
                            if (TheBaseAssets.MyServiceHostInfo.EnableFastSecurity)
                                tDev.SID = pRequestData.SessionState.SScopeID;     //SECURITY: All Reply Telegrams will have same ScrambledSCopeID! - new in 4.209: SID from TSM if set there
                            else
                                tDev.SID = TheBaseAssets.MyScopeManager.GetScrambledScopeID(pRequestData.SessionState.SScopeID, false);     //GRSI: very high frequency!
                        }
                    }
                    tDev.FID = pRequestData.SessionState.GetNextSerial().ToString();
                    tDev.TOP = tMsg.Topic;
                    tDev.MSG = tMsg.OrgMessage;
                    if (TheCommonUtils.IsDeviceSenderType(myTargetNodeChannel.SenderType))  //IDST-OK: Must create RSA for Devices
                    {
                        TheCommonUtils.CreateRSAKeys(pRequestData.SessionState);
                        tDev.RSA = pRequestData.SessionState.RSAPublic;
                    }
                    tDev.NPA = TheBaseAssets.MyScopeManager.GetISBPath(TheBaseAssets.MyServiceHostInfo.RootDir, myTargetNodeChannel.SenderType, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType, pRequestData.SessionState.FID, pRequestData.SessionState.cdeMID, false);
                    if (myTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON)
                    {
                        tMsg.OrgMessage.SEID = null; //SECURITY: Browser does/must not need the SEID for anything
                        tMsg.OrgMessage.UID = null;//SECURITY: Browser does not get nor need the UID
                        tMsg.OrgMessage.SID = null;//SECURITY: Browser does not get nor need the SID
                    }

                    #region Batch Serialization
                    IsBatchOn++;
                    if (MCQCount == 0 || IsBatchOn > TheBaseAssets.MyServiceHostInfo.MaxBatchedTelegrams || tMsg.IsChunked)
                    {
                        if (MCQCount != 0)
                            tDev.CNT = MCQCount;
                        IsBatchOn = 0;
                    }
                    else
                    {
                        if (tSendBufferStr.Length > TheBaseAssets.MAX_MessageSize[(int)myTargetNodeChannel.SenderType])
                        {
                            tDev.CNT = MCQCount;
                            IsBatchOn = 0;
                        }
                    }
                    if (tSendBufferStr.Length > 1) tSendBufferStr.Append(",");
                    tSendBufferStr.Append(TheCommonUtils.SerializeObjectToJSONString(tDev));
#endregion
                }
                else
                    IsBatchOn = 0;
            } while (IsBatchOn > 0 && TheBaseAssets.MasterSwitch);
            tSendBufferStr.Append("]");


            return tSendBufferStr;
        }

        public override string ToString()
        {
            return $"<li>{GetQueDiag(false)}</li>";
        }


#region TSM History Management
        internal TheMirrorCache<TheSentRegistryItem> MyTSMHistory = null;
        internal int MyTSMHistoryCount = 0;

        internal bool WasTSMSeenBefore(TSM pTSM,string pRealSID, bool pIsOutgoing)
        {
            if (MyTSMHistory == null) return false;
            double cFID = TheCommonUtils.CDbl(pTSM.FID);
            if (cFID == 0) return false;
            Guid tOrg = pTSM.GetOriginator();
            if (tOrg == Guid.Empty) return false;

            //int count = TheQueuedSenderRegistry.CountSendersByRealScope(pRealSID);
            if (TheQueuedSenderRegistry.CountRelaySendersByRealScope(pRealSID) < 3) return false;

            TheQueuedSender tQ = TheQueuedSenderRegistry.GetSenderByGuid(tOrg);
            Guid tSessionID = tQ?.MyTargetNodeChannel?.MySessionState?.cdeMID ?? Guid.Empty;

            try
            {
                if (MyTSMHistory.TheValues.Any(s => s.IsOutgoing == pIsOutgoing && s.Engine == pTSM.ENG && tSessionID == s.SessionID && s.FID == cFID && s.ORG == tOrg))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("QSRegistry", $"TSMSeenHistory Found Duplicate! Out:{pIsOutgoing} TXT:{pTSM.TXT} TIM:{pTSM.TIM} ENG:{pTSM.ENG} SEID:{tSessionID}  FID:{cFID} ORG:{tOrg}", eMsgLevel.l6_Debug)); // full  TSM: {pTSM.ToString()}
                    return true;
                }
                else
                {
                    MyTSMHistory.AddAnItem(new TheSentRegistryItem() { ORG = tOrg, Engine = pTSM.ENG, SentTime = pTSM.TIM, FID = cFID, IsOutgoing = pIsOutgoing, SessionID = tSessionID, cdeEXP = TheBaseAssets.MyServiceHostInfo.TO.QSenderDejaSentTime }, null);
                    MyTSMHistoryCount = MyTSMHistory.Count; // This is expensive (takes a global concurrentdictionary lock)
                    TheCDEKPIs.IncTSMByEng(pTSM.ENG);

                    TheBaseAssets.MySYSLOG.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.ESSENTIALS) | MyTSMHistoryCount != 3000 ? null : new TSM("QSRegistry", $"TSMSeenHistory QS very full! Cnt:{MyTSMHistoryCount} Out:{pIsOutgoing} TXT:{pTSM.TXT} TIM:{pTSM.TIM} ENG:{pTSM.ENG} SEID:{tSessionID}  FID:{cFID} ORG:{tOrg}", eMsgLevel.l6_Debug)); // full  TSM: {pTSM.ToString()}
                    if (MyTSMHistoryCount > 6000)
                    {
                        MyTSMHistory.Reset();
                    }
                }

            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(23226, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("QueuedSender", "Exception in WasTSMSeenBefore.", eMsgLevel.l1_Error, "Error: " + e));
            }
            return false;
        }

#endregion

    }
}
