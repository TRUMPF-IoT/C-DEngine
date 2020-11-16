// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace nsCDEngine.ViewModels
{
    /// <summary>
    /// Various System Timeouts that can be configured
    /// </summary>
    public class TheTimeouts
    {
        #region Timer and Heartbeat CONTROL
        /// <summary>
        /// Timeout for Initialization call to Service
        /// </summary>
        public int InitializeTimeout { get; set; }
        /// <summary>
        /// Timeout after which an unresponsive device will be removed from subscription lists
        /// </summary>
        public int DeviceCleanSweepTimeout { get; set; }
        /// <summary>
        /// Heartbeat rete for Service communication
        /// </summary>
        public int HeartBeatRate { get; set; }

        /// <summary>
        /// Amount of Health-Ticks to check if a relay has synced users in the cloud
        /// </summary>
        public int UserCheckRate { get; set; }
        /// <summary>
        /// Minimum Heartbeat allowed
        /// </summary>
        public int MinHeartBeatRate { get; set; }
        /// <summary>
        /// Heartbeat reate if Node is "On Adrenalin" in ms
        /// </summary>
        public int HeartOnAdrenalin { get; set; }
        /// <summary>
        /// Hearbeat of sleeping node (No subscriptions active)
        /// </summary>
        public int HeartSleeping { get; set; }
        /// <summary>
        /// Normal Heartbeat rate
        /// </summary>
        public int HeartNormal { get; set; }

        /// <summary>
        /// Amount of Heartbeats that can be missed before node is declared unresponsive
        /// </summary>
        public int HeartBeatMissed { get; set; }

        /// <summary>
        /// Time in MS when a connection to a server is considered in failure
        /// </summary>
        public int WsTimeOut { get; set; }
        /// <summary>
        /// Put node to sleep...very slow heartbeat rate. Minimum is 10 seconds
        /// </summary>

        /// <summary>
        /// Define a Trigger level for HearBeat Failure warnings in Log
        /// </summary>
        public int HeartBeatWarning { get; set; }
        /// <summary>
        /// Tells if the node is in Adrenalin mode
        /// </summary>
        public bool OnAdrenalin { get; set; }
        /// <summary>
        /// Tells if the ndoe is sleeping
        /// </summary>
        public bool IsSleeping { get; set; }

        /// <summary>
        /// Default timer period for the QSenderRegistry Health Timer
        /// </summary>
        public int QSenderHealthTime
        {
            get;
            private set;
        }

        internal int QSenderDejaSentTime
        { get; set; }

        /// <summary>
        /// Default timeout for Store Requests
        /// </summary>
        public int StoreRequestTimeout
        { get; set; }
        /// <summary>
        /// Default timeout for Chunks
        /// </summary>
        public int ReceivedChunkTimeout
        { get; set; }
        /// <summary>
        /// Default cycle for storage pruning
        /// </summary>
        public int StorageCleanCycle
        { get; set; }

        internal int GetBlobTimeout
        { get; set; }

        /// <summary>
        /// Stream File Retry Period
        /// </summary>
        public int SFRetryMod
        {
            get;
            private set;
        }

        /// <summary>
        /// Stream File Failure Count
        /// </summary>
        public int SFFailCount
        {
            get;
            private set;
        }

        /// <summary>
        /// Stream File Wait time
        /// </summary>
        public uint SFWaitTime
        {
            get;
            private set;
        }
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public TheTimeouts()
        {
            Reset();
        }

        internal int BINGScanRate
        { get; set; }
        internal int InitializeWaitPeriod
        { get; set; }
        internal int HeadlessStartupDelay
        { get; set; }

        internal void Reset()
        {
            InitializeTimeout = 30;
            DeviceCleanSweepTimeout = 30;
            UserCheckRate = 32;

            SFWaitTime = 500;
            SFRetryMod = 20;
            SFFailCount = 50;

            HeartBeatRate = 3;
            MinHeartBeatRate = 1;
            HeartOnAdrenalin = 1;
            HeartNormal = 3;

            HeartSleeping = 10;
            HeartBeatMissed = 6;

            HeartBeatWarning = 4;
            OnAdrenalin = false;
            IsSleeping = false;

            QSenderHealthTime = 1000;
            QSenderDejaSentTime = 8;

            StoreRequestTimeout = 60;
            ReceivedChunkTimeout = 180;
            StorageCleanCycle = 5;
            BINGScanRate = 960;
            InitializeWaitPeriod = 2;
            HeadlessStartupDelay = 0;

            GetBlobTimeout = 120;

            WsTimeOut = 5000;
        }

        /// <summary>
        /// Returns all current Heartbeat Settings
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "HBC:{0} HBA:{1} HBN:{2} HBS:{3} HBC:{4} HBM:{5} DCST:{6} GBT:{7} ISA:{8} ISS:{9}", MinHeartBeatRate, HeartOnAdrenalin, HeartNormal, HeartSleeping, MinHeartBeatRate, HeartBeatMissed, DeviceCleanSweepTimeout, GetBlobTimeout, OnAdrenalin, IsSleeping);
        }

        #region Timer and Heartbeat CONTROL
        public void MakeHeartSleep()
        {
            if (HeartSleeping < 10) HeartSleeping = 10;
            HeartBeatRate = HeartSleeping;
            OnAdrenalin = false;
            IsSleeping = true;
        }
        /// <summary>
        /// Puts Node in Adrenaline mode.
        /// cannot be smaller than MinHeartBeatRate in ms
        /// </summary>
        public void MakeHeartPump()
        {
            if (HeartOnAdrenalin < MinHeartBeatRate) HeartOnAdrenalin = MinHeartBeatRate;
            HeartBeatRate = HeartOnAdrenalin;
            OnAdrenalin = true;
            IsSleeping = false;
        }
        /// <summary>
        /// put node back to normal Heartbeat
        /// </summary>
        public void MakeHeartNormal()
        {
            if (HeartNormal < MinHeartBeatRate) HeartNormal = MinHeartBeatRate;
            HeartBeatRate = HeartNormal;
            OnAdrenalin = false;
            IsSleeping = false;
        }

        /// <summary>
        /// Sets the FileSync Timeouts
        /// </summary>
        /// <param name="WaitTime">Time to wait for a file to arrive from a remote node</param>
        /// <param name="pFailCount">Amount of times the FileSync waits for a response from the node.</param>
        /// <param name="pRetryMod">Amount of WaitTimes the waiting node sends the Sync request to all nodes</param>
        public void SetSyncFileTO(uint WaitTime, int pFailCount, int pRetryMod)
        {
            if (WaitTime < 500) WaitTime = 500;
            SFWaitTime = WaitTime;
            if (pFailCount < 1) pFailCount = 1;
            SFFailCount = pFailCount;
            if (pRetryMod < pFailCount) pRetryMod = pFailCount;
            SFRetryMod = pRetryMod;
        }

        #endregion
    }

    /// <summary>
    /// Information about a given Device the Node runs on
    /// </summary>
    public class TheDeviceRegistryData : TheDataBase
    {
        /// <summary>
        /// Host Name of the Device
        /// </summary>
        public string DeviceName { get; set; }
        /// <summary>
        /// Type of Device
        /// </summary>
        public string DeviceType { get; set; }
        /// <summary>
        /// Guid of the Device
        /// </summary>
        public Guid DeviceID { get; set; }
        /// <summary>
        /// Primary Device User ID
        /// </summary>
        public Guid PrimaryUserID { get; set; }
        /// <summary>
        /// Last Access to this device
        /// </summary>
        public DateTimeOffset LastAccess { get; set; }
        /// <summary>
        /// Sender Type of this device
        /// </summary>
        public cdeSenderType SenderType { get; set; }

        /// <summary>
        /// Returns a friendly description of the device
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Name:{DeviceName} ID:{DeviceID.ToString().Substring(0, 8)} SenderType:{SenderType}";
        }
    }

    /// <summary>
    /// This is the main configuration class of the C-DEngine hosted as a service inside an application.
    /// </summary>
    /// <remarks></remarks>
    public class TheServiceHostInfo : TheDataBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:nsCDEngine.ViewModels.TheServiceHostInfo">TheServiceHostInfo</see> class.
        /// </summary>
        /// <remarks></remarks>
        public TheServiceHostInfo()
        {
            SetDefaults();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:nsCDEngine.ViewModels.TheServiceHostInfo"/> class.
        /// </summary>
        /// <param name="pType">Sets the cdeHostType for the Hosting Application</param>
        public TheServiceHostInfo(cdeHostType pType)
        {
            SetDefaults();
            cdeHostingType = pType;
            if (pType == cdeHostType.Device || pType == cdeHostType.Metro || pType == cdeHostType.Browser || pType == cdeHostType.Phone || pType == cdeHostType.Silverlight)
                cdeNodeType = cdeNodeType.Active;
            else
            {
                cdeNodeType = pType == cdeHostType.Mini ? cdeNodeType.Passive : cdeNodeType.Relay;
            }
        }

        public TheServiceHostInfo(cdeHostType pType, cdeNodeType pNodeType)
        {
            SetDefaults();
            cdeHostingType = pType;
            cdeNodeType = pNodeType;
        }
        public TheServiceHostInfo(cdeHostType pType, cdeNodeType pNodeType, string baseDirectory)
        {
            SetDefaults();
            cdeHostingType = pType;
            cdeNodeType = pNodeType;
            BaseDirectory = baseDirectory;
        }

        /// <summary>
        /// Sets all the Default values for the C-DEngine
        /// </summary>
        private void SetDefaults()
        {
            MiniHostInfo = new TheServiceHostInfoMini();
            SenderQueueSize = 1000;
            RootDir = "";
            ProtocolVersion = "40";
            MinPluginVersion = "3.200";
            TO = new TheTimeouts();
            Robots_txt = "User-agent: *\nAllow: /";
            SessionTimeout = 100;
            EventTimeout = 3000; // 60000; //Thing Fire Event Timeout
            MaxBatchedTelegrams = 20;
            MyStationPort = 0;
            MyStationURL = "NOT SET YET";   //MSU-OK
            MyDeviceMoniker = "CDEV://";
            MyStationMoniker = "HTTP://";
            //LocalServiceRoute = ""; //LOCALHOST";
            MyAppPresenter = "C-Labs presents: ";
            VendorName = "C-Labs";
            favicon_ico = "favicon.ico";
            BaseBackgroundColor = "#141414";
            BaseForegroundColor = "#E5FFFE";
            navbutton_color = "#EBDEC7";
            TileColor = "#EBDEC7";
            TileImage = "Images/toplogo-150.png";
            UPnPIcon = "Images/UPNPICON.png";
            OptScrRes4by3 = "width=1024;height=768";
            OptScrRes16by9 = "width=1376;height=768";
            DefaultLCID = 1033;
            PortalPage = "NMIPortal";
            EntryDate = DateTimeOffset.Now;
            LastUpdate = DateTimeOffset.Now;
            CurrentVersion = 1.0;
            StartISM = true;
            ISMScanForUpdatesOnUSB = false;
            ISMScanOnStartup = false;
            AllowLocalHost = false;
            CSSMain = "MYSTYLES.MIN.CSS";
            CSSDlg = "MYSTYLES.MIN.CSS";
            UPnPDeviceType = "DimmableLight"; // "SensorManagement"; // "InternetGatewayDevice";
            DISCOScanRate = 60;
            DISCOMX = 3;
            DISCOSubnet = "";
            HeartbeatDelay = 5;
            StatusColors = "gray;green;yellow;red;blue;brown;purple;black";
            CacheMaxAge = 10;
            TokenLifeTime = 3; //By default Token Lifetime is only 3 seconds

#pragma warning disable CS0618 // Type or member is obsolete
            StationRoles = new List<string>();
            ApplicationRoles = new List<string>();
#pragma warning restore CS0618 // Type or member is obsolete
            StartupEngines = new List<string>();
            IgnoredEngines = new List<string>();
            IsoEngines = new List<string>();
            IsoEnginesToLoad = new List<string>();
            SimulatedEngines = new List<string>();
            RelayEngines = new List<string>();
            PermittedUnscopedNodesIDs = new List<string>();
            MyAltStationURLs = new List<string>();
            NodeBlacklist = new HashSet<Guid>();
            ConnToken = Guid.NewGuid();
            // CODE REVIEW: Why read this from config in this constructor? Shouldn't we read it in StartApplication when CMDArgs have been parsed to allow ENV override?
            //CM: I think we can move this. It was required very early for UPnP Disco..which is now initialized much later - moved to parsecmdline

            ThingRegistryStoreInterval = 5;
        }

        public string GetMeta(string pUrl)
        {
            if (string.IsNullOrEmpty(pUrl)) pUrl = "/default.aspx";
            string tMeta = "<link rel=\"shortcut icon\" type=\"image/ico\" href=\"/" + favicon_ico + "\" />";
            tMeta += "<meta name=\"application-name\" content=\"" + ApplicationTitle + "\" />";
            tMeta += "<meta name=\"msapplication-starturl\" content=\"" + GetPrimaryStationURL(false) + pUrl + "\" />";
            tMeta += "<meta name=\"msapplication-navbutton-color\" content=\"" + ApplicationTitle + "\" />";
            tMeta += "<meta name=\"msapplication-window\" content=\"" + OptScrRes4by3 + "\" />";
            tMeta += "<meta name=\"msapplication-tooltip\" content=\"" + ApplicationTitle + "\" />";
            tMeta += "<meta name=\"msapplication-TileColor\" content=\"" + TileColor + "\"/> ";
            tMeta += "<meta name=\"msapplication-TileImage\" content=\"" + TileImage + "\"/> ";
            tMeta += "<meta name=\"msapplication-task\" content=\"name=" + ApplicationTitle + ";action-uri=" + GetPrimaryStationURL(false) + pUrl + ";icon-uri=" + favicon_ico + "\" />";
            tMeta += "<meta name=\"msapplication-task\" content=\"name=C-DEngine Status Log;action-uri=" + GetPrimaryStationURL(false) + "/cdeStatus.aspx;icon-uri=" + favicon_ico + "\" />";
            return tMeta;
        }

        public string GetPrimaryStationURL(bool GetAllUrls)
        {
            string ret = MyStationURL;  //MSU-OK
            if (GetAllUrls)
            {
                ret += ";" + TheCommonUtils.CListToString(MyAltStationURLs, ";");
            }
            return ret;
        }

        internal TheServiceHostInfoMini MiniHostInfo { get; set; }

        /// <summary>
        /// If set to True, the Browser will not be able to RSA Encrypt, ScopeID, UID or Password
        /// In the future we might use our own security algorithm here since the RSA Implementation of .NET varies on all platforms!
        /// </summary>
        public bool DisableRSAToBrowser
        {
            get;
            internal set;
        }
        /// <summary>
        /// If true, trades off slightly faster communication for a
        /// less secure connection (fewer changes of security tokens).
        /// </summary>
        public bool EnableFastSecurity
        {
            get { return MiniHostInfo.EnableFastSecurity; }
            internal set { MiniHostInfo.EnableFastSecurity = value; }
        }
        /// <summary>
        /// If set to true, the current node is a Cloud Service.
        /// </summary>
        public bool IsCloudService
        {
            get { return MiniHostInfo.IsCloudService; }
            set { MiniHostInfo.IsCloudService = value; }
        }

        /// <summary>
        /// The DeviceInfo contains all the information about the device the current instance is running on:
        /// MyDeviceInfo = Hardware Device - Where is the CDE running on?
        /// cdePlatform = OS Version - Are we on Mono, 64Bit or 32Bit?
        /// cdeHostingType = Application Type - Where is the CDE running in?
        /// cdeNodeType = Communication Type - How can this node communicate with other nodes?
        /// </summary>
        public TheDeviceRegistryData MyDeviceInfo
        {
            get { return mRedData; }
            internal set
            {
                if (value == null) return;
                mRedData = value;
                MiniHostInfo.MyDeviceSenderType = value.SenderType;
            }
        }
        TheDeviceRegistryData mRedData;

        /// <summary>
        /// If this is set to true, the C-DEngine is not completely configured, yet. For example no ScopeID
        /// has been issued for the current node. This is not critical for the run-time of the C-DEngine and
        /// is user mainly by Application Configuration Plugins such as the Factory-Relay.
        /// </summary>
        public bool RequiresConfiguration
        {
            get { return MiniHostInfo.RequiresConfiguration; }
            internal set { MiniHostInfo.RequiresConfiguration = value; }
        }

        /// <summary>
        /// The allowed unscoped nodes set in the App.config. This is used to allow Cloud to Cloud connections. 
        /// </summary>
        internal List<Guid> AllowedUnscopedNodes = new List<Guid>();

        /// <summary>
        /// Gets or sets a value indicating whether to cloud-to-cloud upstream only aka "Diode Traffic in one direction only". 
        /// </summary>
        /// <value><c>true</c> if [cloud to cloud upstream only]; otherwise, <c>false</c>.</value>
        public bool CloudToCloudUpstreamOnly
        {
            internal set;
            get;
        }
        /// <summary>
        /// A list of node IDs that are allowed to participate in the mesh communication although they do not have a proper ScopeID.
        /// This is useful for unscoped small devices such as sensors that need to send information to the mesh but do not have the proper OS to participate in the encrypted security of C-DEngine nodes
        /// </summary>
        public List<string> PermittedUnscopedNodesIDs
        {
            get;
            internal set;
        }
        internal bool IsNodePermitted(Guid pNodeId)
        {
            //if (MyServiceHostInfo.IsCloudService && TheCommonUtils.IsScopingEnabled()) return true;
            if (pNodeId == Guid.Empty || PermittedUnscopedNodesIDs == null || PermittedUnscopedNodesIDs.Count == 0)
                return false;
            foreach (string t in PermittedUnscopedNodesIDs)
            {
                if (pNodeId.Equals(TheCommonUtils.CGuid(t)))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Timeout for Thing Events (in milliseconds).
        /// </summary>
        public int EventTimeout { get; set; }

        /// <summary>
        /// use new 16 digits EasyScopeID
        /// </summary>
        public bool UseEasyScope16
        {
            get { return MiniHostInfo.UseEasyScope16; }
            internal set { MiniHostInfo.UseEasyScope16 = value; }
        }

        /// <summary>
        /// If true the Tasks will measure KPIs
        /// </summary>
        public bool EnableTaskKPIs
        {
            get;
            internal set;
        }

        /// <summary>
        /// Every TSM(...) constructor will set the SID (expensive envryption)
        /// if not set (default) the two WriteToLog constructors will NOT set the SID during construction
        /// </summary>
        public bool DisableFastTSMs { get; set; }

        /// <summary>
        /// Debug Level of the Application
        /// The higher the level - the more verbose the output
        /// </summary>
        public eDEBUG_LEVELS DebugLevel { get; set; }

        /// <summary>
        /// Set if Mono Runtime Detection was run...dont use to detect MONO Runtime - please use MonoRTActive of MonoActive
        /// </summary>
        public bool MonoRTDetected
        {
            get;
            internal set;
        }
        /// <summary>
        /// Same as MonoActive
        /// </summary>
        public bool MonoRTActive
        {
            get;
            internal set;
        }

        public List<string> ActivationKeysToAdd { get; internal set; }  //CODE-REVIEW: Markus: can we do this? or do they have to be internal for Get AND Set?

        internal string ProxyUrl { get; set; }
        internal string ProxyUID { get; set; }
        internal string ProxyPWD { get; set; }

        public bool AllowSetScopeWithSetAdmin { get; set; }

        internal string GetProxyToken()
        {
            return TheCommonUtils.cdeEncrypt(TheCommonUtils.CStr(ProxyUrl) + ";:;" + TheCommonUtils.CStr(ProxyUID) + ";:;" + TheCommonUtils.CStr(ProxyPWD), TheBaseAssets.MySecrets.GetNodeKey());
        }
        internal bool SetProxy(string encSettings)
        {
            if (string.IsNullOrEmpty(encSettings) || TheBaseAssets.MyServiceHostInfo.IsIsolated) return false;
            try
            {
                string t = TheCommonUtils.cdeDecrypt(encSettings, TheBaseAssets.MySecrets.GetNodeKey());
                if (string.IsNullOrEmpty(t)) return false;

                string[] tt = TheCommonUtils.cdeSplit(t, ";:;", false, false);
                if (tt.Length < 1) return false;
                if (!string.IsNullOrEmpty(tt[0])) //token might be empty but proxy was set in app.config and therefore must not override given proxy
                    ProxyUrl = tt[0];
                if (tt.Length > 1 && !string.IsNullOrEmpty(tt[1])) //token might be empty but proxy was set in app.config and therefore must not override given proxy
                    ProxyUID = tt[1];
                if (tt.Length > 2 && !string.IsNullOrEmpty(tt[2])) //token might be empty but proxy was set in app.config and therefore must not override given proxy
                    ProxyPWD = tt[2];
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2352, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("SetProxy", $"Failure to set Proxy", eMsgLevel.l1_Error, e.ToString()));
            }
            return true;
        }
        public bool SetProxy(string pUrl, string pUID, string pPWD)
        {
            bool IsDirty = false;
            if (ProxyUrl != pUrl)
            {
                ProxyUrl = pUrl;
                IsDirty = true;
            }
            if (ProxyUID != pUID)
            {
                ProxyUID = pUID;
                IsDirty = true;
            }

            if (ProxyPWD != pPWD)
            {
                ProxyPWD = pPWD;
                IsDirty = true;
            }
            return IsDirty;
        }

        private int mStatusLevel;
        /// <summary>
        /// The current StatusLevel of the Relay - aggregates all Engine Status Levels to the highest level
        /// 0=Idle
        /// 1=Active / ok
        /// 2=Warning
        /// 3=Error
        /// 4=Setup/RampUp
        /// 5=Design / Engineering / Configuration
        /// 6=Shutdown
        /// 7=Unknown/Unreachable
        /// </summary>
        public int StatusLevel
        {
            // ReSharper disable once ValueParameterNotUsed
            internal set
            {
                int tNewLevel = StatusLevel;
                if (mStatusLevel != tNewLevel)
                {
                    mStatusLevel = tNewLevel;
                    TheNMIEngine.SetUXProperty(Guid.Empty, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID, // Engines.NMIService.TheNMIEngine.eOverallStatusButtonGuid,
                        $"Background={TheNMIEngine.GetStatusColor(tNewLevel)}");
                }
            }
            get
            {
                if (!TheBaseAssets.MasterSwitch) return 6; //New: Do not calculate on shutdown to reduce Shutdown times
                List<IBaseEngine> tList = TheThingRegistry.GetBaseEngines(true);
                if (tList != null && tList.Count > 0)
                {
                    //int CombinedCode = 0;
                    int HighestLevel = 0;
                    foreach (IBaseEngine tDevice in tList)
                    {
                        if (tDevice.GetBaseThing() != null && tDevice.GetBaseThing().GetBaseThing() != null)
                        {
                            int tstat = tDevice.GetBaseThing().GetBaseThing().StatusLevel;
                            //if (tstat > 1)
                            //    CombinedCode++;
                            if (tstat > HighestLevel)
                                HighestLevel = tstat;
                        }
                    }
                    return HighestLevel;
                }
                return 5;
            }
        }

        internal string[] mStatusColors;

        /// <summary>
        /// Sets Custom Status Colors for a Relay - Separate the values with ;
        /// 0=Idle
        /// 1=Active / ok
        /// 2=Warning
        /// 3=Error
        /// 4=Setup/RampUp
        /// 5=Design / Engineering / Configuration
        /// 6=Shutdown
        /// 7=Unknown/unreachable
        /// </summary>
        public string StatusColors
        {
            set
            {
                if (value == null) return;
                string[] tStatusColors = TheCommonUtils.cdeSplit(value, ';', false, false);
                if (mStatusColors == null)
                {
                    mStatusColors = tStatusColors;
                    return;
                }
                string[] toldColor = mStatusColors;
                mStatusColors = new string[toldColor.Length > tStatusColors.Length ? toldColor.Length : tStatusColors.Length];
                for (int i = 0; i < (toldColor.Length > tStatusColors.Length ? toldColor.Length : tStatusColors.Length); i++)
                {
                    if (i < tStatusColors.Length && !string.IsNullOrEmpty(tStatusColors[i]))
                        mStatusColors[i] = tStatusColors[i];
                    else
                    {
                        if (i < toldColor.Length)
                            mStatusColors[i] = toldColor[i];
                    }
                }
            }
            get { return TheCommonUtils.CListToString(mStatusColors, ";"); }
        }

        /// <summary>
        /// Allows to change the AllowRemoteAdministration Setting if the request came from a user with Administrator rights
        /// </summary>
        /// <param name="pAllow"></param>
        /// <param name="pUID"></param>
        /// <returns></returns>
        public bool SetAllowRemoteAdministration(bool pAllow, Guid pUID)
        {
            if (TheUserManager.HasUserAccess(pUID, 128))
            {
                AllowRemoteAdministration = pAllow;
                return true;
            }
            return false;
        }

        /// <summary>
        /// This list contains all files required for the current application to run.
        /// The Plugin-Store Service will collect all these files and create an installer from it.
        /// </summary>
        internal List<string> ManifestFiles { get; set; }

        /// <summary>
        /// An object with certificate information. The specific format is dependent on the type of web server.
        /// For example, in Asp.Net the objects are of type System.Web.HttpClientCertificate. In an HttpListener
        /// type web server, the certificates are of type System.Security.Cryptography. X509Certificates.X509Certificate2.
        /// </summary>
        internal X509Certificate2Collection ClientCerts { get; set; }

        /// <summary>
        /// Secret Provisioning Token from Provisioning Service
        /// </summary>
        internal string ProvToken { get; set; }

        /// <summary>
        /// Contains the current code signing Thumbprint
        /// </summary>
        public string CodeSignThumb { get; internal set; }
        /// <summary>
        /// Token published to MeshManager
        /// </summary>
        internal string NodeToken { get; set; }

        internal string ProvisioningService { get; set; }

        internal string ProvisioningScope { get; set; }

        internal Guid ProvisioningServiceNodeID { get; set; }

        internal Guid MasterNode { get; set; }

        /// <summary>
        /// Add a list of Files required for the current application to run.
        /// The Plugin-Store service will combine all these files into an update file.
        /// </summary>
        /// <param name="pList"></param>
        public void AddManifestFiles(List<string> pList)
        {
            if (ManifestFiles == null)
                ManifestFiles = new List<string>();
            ManifestFiles.AddRange(pList);

        }

        /// <summary>
        /// if true and the internal webserver cannot be created due to a conflict with other installed servers, the node will terminate.
        /// if false (Default) the node will disable the internal webserver and just run as a "Device" changing the cdeHostingType to "Device"
        /// </summary>
        public bool DontFallbackToDevice
        {
            get;
            internal set;
        }

        /// <summary>
        /// The cdeHostType of the node running the current instance of the C-DEngine
        /// </summary>
        public cdeHostType cdeHostingType
        {
            get;
            internal set;
        }
        /// <summary>
        /// The cdeNodeType defines the communication architecture used for this node.
        /// </summary>
        public cdeNodeType cdeNodeType
        {
            get;
            internal set;
        }

        /// <summary>
        /// Return the platform the current instance if running on
        /// </summary>
        public cdePlatform cdePlatform
        {
            get;
            internal set;
        }

        /// <summary>
        /// if set to >0 the shutdown will be delayed by these ms. Plugins can set this value if necessary
        /// </summary>
        public int PreShutDownDelay { get; set; }

        /// <summary>
        /// True if the system is currently backing up the cache folder
        /// </summary>
        public bool IsBackingUp { internal set; get; }

        /// <summary>
        /// If set to true, no Log output will go the the console. Logger plugins are not affected
        /// </summary>
        public bool DisableConsole { get; set; }

        /// <summary>
        /// Sets the GELF Loggin Format as the standard format for Console Output
        /// </summary>
        public bool UseGELFLoggingFormat { get; set; }

        /// <summary>
        /// if true, All Log Entries with DeviceIDs/NodeIDs and ScopeHashes will add Markup for better debugging in cdeSTatus.aspx
        /// </summary>
        public bool ShowMarkupInLog
        {
            get { return m_ShowMInLog | DebugLevel > eDEBUG_LEVELS.ESSENTIALS; }
            set { m_ShowMInLog = value; }
        }
        private bool m_ShowMInLog = false;

        List<string> _requiredClientCertRootThumbprints;
        /// <summary>
        /// List of Thumbprints of allowed client certificates
        /// </summary>
        public List<string> RequiredClientCertRootThumbprints
        {
            get { return _requiredClientCertRootThumbprints?.ToList(); }
            internal set { _requiredClientCertRootThumbprints = value; }
        }

        /// <summary>
        /// CORS Settings that allows access from other domains
        /// </summary>
        public string AccessControlAllowOrigin
        {
            get;
            set;
        }
        /// <summary>
        /// CORS Settings that allows only certain methods. Default *
        /// </summary>
        public string AccessControlAllowMethods
        {
            get;
            set;
        }
        /// <summary>
        /// CORS Settings that allows only certain headers. Default *
        /// </summary>
        public string AccessControlAllowHeaders
        {
            get;
            set;
        }

        /// <summary>
        /// If set to 0 or does not exist, a client certificate is not used and not required (default mode of previous CDE versions)
        /// If Set to 1, client can present a client certificate. If no scopes are in the cert, scopes will be used as before  (Mix operation between new and old security mode)
        /// If Set to 2, client must present a valid client certificate. If no scopes are in the cert, scopes will be used as before (was previous "RequireClientCertificate=true") (previous default mode but with Client Certificate turned on)
        /// If set to 3, all clients (including NMI browsers) need to present a client certificate (new enforced Client Certificate Mode only - older CDE Nodes will not able to connect)
        /// If the client certificate contains one or more SAN URLs of the scheme "com.c-labs.cdescope://" the client will only participate in scopes with that scopeID. The first cdescope is used as the ScopeID if no scope is presented in the message or connection
        /// In IIS, settings 2 and 3 require the "Client Certificates" setting in "SSL Settings" to be set as "Accept" or "Require" for NMI access
        /// </summary>
        public int ClientCertificateUsage { get; internal set; }

        public bool OneWayRelayMode { get; internal set; }

        /// <summary>
        /// Filter indicating which outgoing TSMs should be allowed into a OneWay channel. Each filter entry is a string that is matched against the TSM.TXT field. 
        /// The filter entry can contain at most one wildcard ('*' character).
        /// </summary>
        public string[] OneWayTSMFilter { get; internal set; }

        /// <summary>
        /// Thumbprint of a client certificate to use for mesh communication
        /// </summary>
        public string ClientCertificateThumb { get; internal set; }
        /// <summary>
        /// If true, all changes initiated from the NMI will be logged
        /// </summary>
        public bool AuditNMIChanges
        {
            get { return m_auditNMIChanges & TheLoggerFactory.HasEngine(); }
            internal set { m_auditNMIChanges = true; }
        }
        private bool m_auditNMIChanges = false;


        public Guid ConnToken
        {
            get;
            set;
        }

        /// <summary>
        /// If true, the node tries to be as memory optimized as possible. Performance might be slightly decreased and logging and other storage based options might be unavailable
        /// </summary>
        public bool IsMemoryOptimized
        {
            get;
            internal set;
        }

        /// <summary>
        /// Set to true if the Node was run for the first time
        /// </summary>
        public bool IsNewDevice
        {
            get;
            internal set;
        }

        /// <summary>
        /// Create User on first start will only be executed if IsNewDevice=true
        /// </summary>
        public string Coufs { get; internal set; }

        /// <summary>
        /// Is true if Costing per telegram is enabled on this node
        /// </summary>
        public bool EnableCosting
        {
            get;
            internal set;
        }

        /// <summary>
        /// If set to true, the CDEngine is running embedded/integrated in another web solution
        /// </summary>
        public bool EnableIntegration
        {
            get;
            internal set;
        }

        /// <summary>
        /// If set to true, the CDEngine is will process device messages in the initial CDE_CONNECT message. Note that, while secure, this will bypass the additional security measures provided by the CDEngine sessions like protection against replay attacks.
        /// </summary>
        public bool AllowMessagesInConnect
        {
            get;
            internal set;
        }

        /// <summary>
        /// Silverlight Only: Tells the plugins if TheBaseApplication is hosted our of browser (SLLauncher)
        /// </summary>
        public static bool IsOutOfBrowser
        {
            get;
            internal set;
        }

        /// <summary>
        /// Is set to true when all Engines (Plugin-Service) have been started
        /// </summary>
        public bool AreAllEnginesStarted
        {
            get;
            internal set;
        }

        /// <summary>
        /// Set when the C-DEngine starts all Plugin Services
        /// </summary>
        public bool AllEnginesAreStarting
        {
            get;
            internal set;
        }
        public bool AllSystemsReady
        {
            get;
            internal set;
        }

        /// <summary>
        /// If True, the QueuedSenderHealth Timer will fire synchronously. Default is async
        /// </summary>
        public bool FireGlobalTimerSync
        {
            get; set;
        }

        /// <summary>
        /// This structure allows to set all important timeouts of the C-DEngine
        /// </summary>
        public TheTimeouts TO { get; set; }
        /// <summary>
        /// Retired
        /// </summary>
        public string MyDeviceMoniker
        {
            get;
            internal set;
        }
        /// <summary>
        /// Retired
        /// </summary>
        public string MyStationMoniker
        {
            get;
            internal set;
        }

        /// <summary>
        /// Enables the Access Tracker for User Activities
        /// </summary>
        public bool TrackAccess
        {
            get;
            set;
        }

        /// <summary>
        /// Amount of Seconds Delay to check on Heartbeat
        /// </summary>
        public int HeartbeatDelay
        {
            get;
            set;
        }

        private string _MyStationName;

        /// <summary>
        /// Friendly name of the current Node/Station. Uses the MyStationURL if not set
        /// </summary>
        public string MyStationName
        {
            get
            {
                if (string.IsNullOrEmpty(_MyStationName))
                {
                    if (!string.IsNullOrEmpty(MyStationURL) && MyStationURL != "NOT SET YET")
                        _MyStationName = MyStationURL;
                    else
                        return "Not Set Yet!";
                }
                return _MyStationName;
            }
            set { _MyStationName = value; }
        }
        /// <summary>
        /// The main URL of this Node
        /// </summary>
        public string MyStationURL
        {
            get;
            internal set;
        }    //MSU-OK
        /// <summary>
        /// A list of alternative URLs that this node might have and allows connection to.
        /// Any URL not specified in MyStationURL and MyAltStationURLs will not allow inbound communication
        /// </summary>
        public List<string> MyAltStationURLs
        {
            get;
            internal set;
        }

        /// <summary>
        /// NodeIDs in this list will be prevented from connecting to this node.
        /// </summary>
        public HashSet<Guid> NodeBlacklist
        {
            get;
            internal set;
        }
        /// <summary>
        /// If known, this holds the IP of the current instance of the C-DEngine
        /// </summary>
        public string MyStationIP
        {
            get;
            internal set;
        }
        /// <summary>
        /// The inbound port of the current instance for HTTP connections. If set to zero, this node will not accept incoming connections
        /// </summary>
        public ushort MyStationPort { get; set; }
        /// <summary>
        /// In inbound port for WebSockets connections. If set to zero, this node will not accept inbound WebSockets connections
        /// </summary>
        public ushort MyStationWSPort { get; set; }

        /// <summary>
        /// OS the node is running on
        /// </summary>
        public string OSInfo { get; internal set; }

        /// <summary>
        /// Allows to give the node a friendly name.
        /// </summary>
        public string NodeName { get; set; }
        /// <summary>
        /// If this is set to true, the C-DEngine will not accept inbound connection on HTTP but requires HTTPS
        /// </summary>
        public bool IsSSLEnforced
        {
            get;
            internal set;
        }

        /// <summary>
        /// Live time for User Refresh Tokens
        /// </summary>
        public int TokenLifeTime
        {
            get;
            internal set;
        }

        /// <summary>
        /// NEW:3.2 If set to true, the User Information is no longer replicated via the cloud and users cannot login to the NMI via the cloud
        /// To set from a plugin use "TheCommCore.SetCloudNMIBlock(true|false, GUID pUser); only Admins on the "FirstNode" can change this setting
        /// </summary>
        public bool IsCloudNMIBlocked
        {
            get;
            internal set;
        }
        /// <summary>
        /// if set to true, unscoped nodes can create their own mesh
        /// </summary>
        public bool AllowUnscopedMesh
        {
            get;
            internal set;
        }
        /// <summary>
        /// retired
        /// </summary>
        public bool FallbackToSimulation
        {
            get;
            internal set;
        }

        public bool DisableNMI
        {
            get;
            internal set;
        }

        public bool DisableNMIMessages
        {
            get;
            set;
        }

        /// <summary>
        /// If Set to true, the C-DEngine will accept "http://localhost" as an additional inbound URL
        /// </summary>
        public bool AllowLocalHost { get; set; }
        /// <summary>
        /// If set to true, the C-DEngine will create a new random DeviceID every time it starts up.
        /// Benefit: Great for debugging and ad-hoc devices
        /// Be aware: no data is made persitant! The ThingRegistry and other databasees will be cleared with every restart
        /// </summary>
        public bool UseRandomDeviceID
        {
            get;
            internal set;
        }

        /// <summary>
        /// If True, all plugins/services/engines will be registered, Init and CreateUX asynchronously. InitEngineAssets will be called synchronously before
        /// </summary>
        public bool AsyncEngineStartup
        {
            get;
            internal set;
        }

        /// <summary>
        /// If True, the thing history system will log various diagnostics logs
        /// </summary>
        public bool EnableHistorianDataLog
        {
            get;
            internal set;
        }


        /// <summary>
        /// If True, this instance is only hosting one plugin isloated. It connects to a master Node
        /// </summary>
        public bool IsIsolated
        {
            get;
            internal set;
        }
        /// <summary>
        /// RETIRED in V4: LocalServiceRoutes and CloudServiceRoutes will be mapped to ServiceRoute - no differnce between Cloud and Local route in V4
        /// Entry will be removed in V5
        /// </summary>
        public string LocalServiceRoute
        {
            get { return ServiceRoute; }
            set
            {
                if (TheCommonUtils.IsNullOrWhiteSpace(value) || "LOCALHOST".Equals(value, StringComparison.OrdinalIgnoreCase)) return;
                if (!string.IsNullOrEmpty(ServiceRoute))
                    ServiceRoute += $";{value}";
                else
                    ServiceRoute = value;
            }
        }
        /// <summary>
        /// RETIRED in V4: LocalServiceRoutes and CloudServiceRoutes will be mapped to ServiceRoute - no differnce between Cloud and Local route in V4
        /// Entry will be removed in V5
        /// </summary>
        public string CloudServiceRoute
        {
            get { return ServiceRoute; }
            set
            {
                if (TheCommonUtils.IsNullOrWhiteSpace(value) || "LOCALHOST".Equals(value, StringComparison.OrdinalIgnoreCase)) return;
                if (!string.IsNullOrEmpty(ServiceRoute))
                    ServiceRoute += $";{value}";
                else
                    ServiceRoute = value;
            }
        }

        private List<Uri> CurrentRoutes = new List<Uri>();
        internal bool HasServiceRoute(Uri pUri)
        {
            if (pUri == null)
                return false;
            return CurrentRoutes.Contains(pUri);
        }

        private string mServiceRoute;
        /// <summary>
        /// Url and Path to a designated next node with the same ApplicationID. This can point to multiple nodes by separating the URLs with a semicolon (;)
        /// </summary>
        public string ServiceRoute
        {
            get { return mServiceRoute; }
            set
            {
                mServiceRoute = value;
                if (string.IsNullOrEmpty(value))
                {
                    CurrentRoutes = new List<Uri>();
                    return;
                }
                var tR = TheCommonUtils.cdeSplit(mServiceRoute, ";", true, true);
                var currentRoutes = new List<Uri>();
                foreach (var r in tR)
                {
                    var tUri = TheCommonUtils.CUri(r, true);
                    if (tUri != null && !currentRoutes.Contains(tUri))
                        currentRoutes.Add(tUri);
                }
                CurrentRoutes = currentRoutes;
                if (TheBaseAssets.MyServiceHostInfo!=null && TheBaseAssets.MyServiceHostInfo.RequiresConfiguration && TheBaseAssets.MyServiceHostInfo.ClientCerts?.Count > 0)
                {
                    string error = "";
                    var tScopes = TheCertificates.GetScopesFromCertificate(TheBaseAssets.MyServiceHostInfo.ClientCerts[0], ref error);
                    if (tScopes?.Count > 0)
                    {
                        TheBaseAssets.MyScopeManager.SetScopeIDFromEasyID(tScopes[0]);
                        TheBaseAssets.MyServiceHostInfo.RequiresConfiguration = false;
                        TheBaseAssets.MySYSLOG.WriteToLog(2822, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommonUtils", $"Scope {tScopes[0]} found in Certificate and used for node", eMsgLevel.l3_ImportantMessage));
                    }
                }
            }
        }
        /// <summary>
        /// If this value is greater than zero and a telegram comes in with a higher node count in the telegram, it will be rejected.
        ///
        /// </summary>
        public int MaximumHops { get; set; }


        /// <summary>
        /// If set to higher than 3 every telegram will include a new RSA Key. On Devices with slow or no TPMs creation of an RSA Key can take multiple seconds
        /// If set to lower than 3 the C-DEngine will accept untrusted SSL Certificates. This might be important for debugging against a self-signed certificate.
        /// Default is 3
        /// </summary>
        public int SecurityLevel { get; internal set; }

        /// <summary>
        /// If true, each scoped node in a mesh can provide resources for any other node without the need for a browser to have a valid scoped session.
        /// This is a (relative uncritical) security downgrade: normally a browser session has to be authenticated against a scope first. But since resources are considered "static" or "outside NMI Scope" they would need their own user management anyway.
        /// </summary>
        public bool AllowDistributedResourceFetch { get; internal set; }

        /// <summary>
        /// If set to true, the connection URL for an initial connection is always the same. Set this flag to true, if your firewall needs to ensure the first connection is going to am approved URL
        /// We might use this internal only - not right now for customers to set
        /// </summary>
        internal bool UseFixedConnectionUrl
        {
            get { return MiniHostInfo.UseFixedConnectionUrl; }
            set { MiniHostInfo.UseFixedConnectionUrl = value; }
        }
        /// <summary>
        /// Base directory of the current C-DEngine instance
        /// </summary>
        public string BaseDirectory
        {
            get;
            internal set;
        }

        /// <summary>
        /// Application Name - has to match all nodes in mesh for UPnP auto-connect
        /// </summary>
        public string ApplicationName { get; set; }
        /// <summary>
        /// Application title for the Portal screen - this can be any arbitrary string
        /// </summary>
        public string ApplicationTitle { get; set; }
        /// <summary>
        /// should contain "companyXYZ presents..."
        /// Will be printed on Default about us pages
        /// </summary>
        public string MyAppPresenter { get; set; }

        /// <summary>
        /// Main SKU of the current node
        /// </summary>
        public uint SKUID { get; internal set; }
        /// <summary>
        /// Cloud Blob Address
        /// </summary>
        public string CloudBlobURL { get; set; }
        /// <summary>
        /// Current version of the C-DEngine or your application
        /// Use this format:
        /// FullVersion.MinorVersion
        /// </summary>
        public double CurrentVersion { get; set; }
        /// <summary>
        /// Guid of the Application Owner
        /// This guid will allow easy access to vendor information in the Plugin-Store
        /// </summary>
        public Guid VendorID { get; set; }
        /// <summary>
        /// Custom Vendor Data that will be included in the UPnP metadata
        /// You will get this ID assigned from from C-Labs once you have licensed the C-DEngine
        /// </summary>
        public string VendorData { get; set; }
        /// <summary>
        /// Human Readable name of the Vendor creating this Application
        /// </summary>
        public string VendorName { get; set; }
        /// <summary>
        /// A URl pointing at the Vendors WebSite
        /// </summary>
        public string VendorUrl { get; set; }

        /// <summary>
        /// Copyright statement for relay
        /// </summary>
        public string Copyrights { get; set; }
        /// <summary>
        /// Set to enable Windows Azure cloud analytics if you host the C-DEngine in Windows Azure
        /// </summary>
        public Guid AzureAnalytics
        {
            get;
            internal set;
        }

        /// <summary>
        /// If set to true, the ISM - Intelligent Service Management will be activated.
        /// This has to be set before call ing StartBaseApplication()
        /// </summary>
        public bool StartISM { get; set; }
        /// <summary>
        /// If set to true, the UPnP Discovery System will be enabled.
        /// </summary>
        public bool StartISMDisco { get; set; }

        /// <summary>
        /// If set to true, the C-DEngine will listen to USB ports for USB sticks containing Updates and new Plugins
        /// </summary>
        public bool ISMScanForUpdatesOnUSB { get; set; }
        /// <summary>
        /// If set to true, the C-DEngine will scan for new updates during the startup
        /// </summary>
        public bool ISMScanOnStartup { get; set; }
        /// <summary>
        /// Defines the file extension for updates
        /// </summary>
        public string ISMExtension { get; set; }
        /// <summary>
        /// Specifies a specific directory to look for updates.
        /// Default is: BaseDirectory/ClientBin/Updates
        /// </summary>
        public string ISMUpdateDirectory { get; set; }
        /// <summary>
        /// Contains the name of the running Application hosting the C-DEngine
        /// </summary>
        public string ISMMainExecutable { get; set; }
        /// <summary>
        /// Will contain the version of an update found in the directory or a stick
        /// </summary>
        public double ISMUpdateVersion { get; set; }

        /// <summary>
        /// if true, the C-DEngine is running in "Unsafe" mode and custom plugins can be installed
        /// </summary>
        public bool DontVerifyTrust
        {
            get;
            internal set;
        }

        /// <summary>
        /// if true, the C-DEngine is running in "Unsafe" mode: plug-ins can be tampered with
        /// </summary>
        public bool DontVerifyIntegrity
        {
            get;
            internal set;
        }

        /// <summary>
        /// if true, the C-DEngine requires an activated license (plugin id 5737240C-AA66-417C-9B66-3919E18F9F4A)
        /// </summary>
        public bool RequireCDEActivation
        {
            get;
            internal set;
        }

        /// <summary>
        /// if true, each QSender will get its own Heartbeat timer
        /// </summary>
        public bool UseHBTimerPerSender
        {
            get;
            internal set;
        }

        /// <summary>
        /// If true, the C-DEngine will verify the Trust path of the code-signing certificate. Internet connection is REQUIRED
        /// </summary>
        public bool VerifyTrustPath
        {
            get;
            internal set;
        }

        /// <summary>
        /// A list of Plugins that will be running as services on the current node
        /// </summary>
        [Obsolete("No Longer used in V4 - Will be removed in V5")]
        public List<string> StationRoles
        {
            get;
            internal set;
        }

        /// <summary>
        /// A list of known plugins to this node. Any plugin found in the BaseDirectory will be added to this list automatically
        /// </summary>
        [Obsolete("No Longer used in V4 - will be removed in V5")]
        public List<string> ApplicationRoles
        {
            get;
            internal set;
        }
        /// <summary>
        /// retired
        /// </summary>
        public List<string> StartupEngines
        {
            get;
            internal set;
        }

        /// <summary>
        /// the current ISO Engine of the node. Only this engine/plugin is loaded
        /// </summary>
        public string IsoEngine
        {
            get;
            internal set;
        }
        /// <summary>
        /// A list of plugins that should not be started even if they were found in the BaseDirectory
        /// </summary>
        public List<string> IgnoredEngines
        {
            get;
            internal set;
        }
        /// <summary>
        /// A list of plugins that will be hosted isolated in their own process
        /// </summary>
        public List<string> IsoEngines
        {
            get;
            internal set;
        }
        /// <summary>
        /// A list of confirmed plugins that will be hosted isolated in their own process
        /// </summary>
        public List<string> IsoEnginesToLoad
        {
            get;
            internal set;
        }
        /// <summary>
        /// retired
        /// </summary>
        public List<string> SimulatedEngines { get; set; }
        /// <summary>
        /// A list of virtual plugins not running on the current node but are allowed to route telegrams through this node.
        /// </summary>
        public List<string> RelayEngines { get; set; }


        int mJSThrottle;

        /// <summary>
        /// Allows to throttle WebSocket Messages to JavaScript clients. Messages will not be sent faster than this number
        /// Default is zero (No Throttling)
        /// </summary>
        public int WsJsThrottle
        {
            get { return mJSThrottle; }
            set
            {
                if (value > 500) value = 500;
                mJSThrottle = value;
            }
        }

        int _parallelPosts;
        public int ParallelPosts
        {
            get { return _parallelPosts; }
            internal set
            {
                if (value <= 0)
                    _parallelPosts = 0;
                else
                    _parallelPosts = value;
            }
        }

        /// <summary>
        /// New in V3.200: Maximum amount of telegrams batched together
        /// </summary>
        public int MaxBatchedTelegrams
        {
            get;
            set;
        }

        /// <summary>
        /// The time interval the ThingRegistry will be saved to Disk
        /// </summary>
        public int ThingRegistryStoreInterval { get; set; }


        /// <summary>
        /// Minimum Version of plugin required to run
        /// </summary>
        public string MinPluginVersion
        {
            get;
            internal set;
        }

        /// <summary>
        /// Current CDE-T Version
        /// </summary>
        public string ProtocolVersion
        {
            get { return MiniHostInfo.ProtocolVersion; }
            internal set { MiniHostInfo.ProtocolVersion = value; }
        }

        /// <summary>
        /// The NMI Screen with the main Relay Configuration Page
        /// </summary>
        public string MainConfigScreen { get; set; }

        /// <summary>
        /// RETIRED in V4: will be replaced in V5. Use MyLiveServices instead
        /// </summary>
        public string MyAppServices
        {
            get;
            internal set;
        }
        public string MyLiveServices
        {
            get;
            internal set;
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public string SiteName { get; set; }
        public string TrgtSrv { get; set; }
        public string DebTrgtSrv { get; set; }
        public string SrvSec { get; set; }

        /// <summary>
        /// The Root Path on the current node.
        /// </summary>
        public string RootDir
        {
            get;
            internal set;
        }
        public string LoginReferrers { get; set; }
        public string DefLoginPage { get; set; }
        public string DefAccountPage { get; set; }
        public string DefSignupPage { get; set; }
        public string Version
        {
            get { return TheBaseAssets.CurrentVersionInfo; }
        }
        public string Revision
        {
            get;
            internal set;
        }
        public DateTimeOffset EntryDate
        {
            get;
            internal set;
        }
        public DateTimeOffset LastUpdate
        {
            get;
            internal set;
        }
        public Guid ContentTemplate { get; set; }
        public string SealID { get; set; }
        public string CSSMain { get; set; }
        public string CSSDlg { get; set; }
        public Guid TopLogo { get; set; }
        public Guid TopLogoURL { get; set; }
        public int SessionTimeout { get; set; }
        public string DefHomePage { get; set; }
        public string Robots_txt { get; set; }

        /// <summary>
        /// The UserAgent used by the REST commands that can be overwritten for special Firewall policies.
        /// </summary>
        public string CustomUA { get; set; }

        /// <summary>
        /// Path for JavaScript resources if Engine is embedded in other cloud solution
        /// </summary>
        public string ResourcePath { get; set; }
        public string P3PPolicy_XML { get; set; }
        public string Redirect404 { get; set; }
        public string favicon_ico { get; set; }
        public string TileImage { get; set; }
        public string TileColor { get; set; }
        public string UPnPIcon { get; set; }
        public string OptScrRes4by3 { get; set; }
        public string OptScrRes16by9 { get; set; }
        public string navbutton_color { get; set; }
        public int DefaultLCID { get; set; }

        /// <summary>
        /// If set to True, the system will store Message in a Distributed Storage Mirror
        /// </summary>
        public bool StoreLoggedMessages { get; set; }
        /// <summary>
        /// If this is not set, the node will not start if it was not launched with Administrator Previledges
        /// </summary>
        [Obsolete("This flag was never used and has been replaced by FailOnAdminCheck")]
        public bool IgnoreAdminCheck { get; set; }

        /// <summary>
        /// If true, the host will exit if the HttpLIstener (that requires Admin Rights) could not be started and "DontFallbackToDevice" is true. If DontFallbackToDevice is true, the WebServer of the CDE will be disable and the node runs as a "Device"
        /// </summary>
        public bool FailOnAdminCheck { get; set; }
        /// <summary>
        /// retired
        /// </summary>
        public bool AllowAnonymousAccess
        {
            get;
            internal set;
        }

        /// <summary>
        /// If true, remote nodes can force an automatic update of this node
        /// </summary>
        public bool AllowAutoUpdate
        {
            get;
            internal set;
        }



        public int TLDCGs
        {
            get;
            internal set;
        }
        public long CDEPUBSUBrejects
        {
            get;
            internal set;
        }
        public long CDEPUBSUBSrejects
        {
            get;
            internal set;
        }
        public long CDEPUBSUBsent
        {
            get;
            internal set;
        }
        public long CDEPUBSUBrecv
        {
            get;
            internal set;
        }
        /// <summary>
        /// Disables the TLS 1.2 Requirement
        /// </summary>
        public bool DisableTls12
        {
            get;
            internal set;
        }
        /// <summary>
        /// Ignores any SSL certificate validation errors (default behavior before 4.208)
        /// </summary>
        public bool IgnoreServerCertificateErrors
        {
            get;
            internal set;
        }
        /// <summary>
        /// Allows SSL certificates for which the trust chain could not be validated (including expired certificates)
        /// </summary>
        public bool IgnoreServerCertificateChainErrors
        {
            get;
            internal set;
        }
        /// <summary>
        /// Allows SSL connections to servers that do not provide an SSL certificate.
        /// </summary>
        public bool IgnoreServerCertificateNotAvailable
        {
            get;
            internal set;
        }
        /// <summary>
        /// Allows SSL connections to servers for which the provided certificate does not match the host name.
        /// </summary>
        public bool IgnoreServerCertificateNameMismatch
        {
            get;
            internal set;
        }


        /// <summary>
        /// If set to true, many administrative operations can be performed from any FR in the mesh, not just from the FR that is to be managed ("FirstNode").
        /// </summary>
        public bool AllowRemoteAdministration
        {
            get;
            internal set;
        }

        /// <summary>
        /// If set to true, plug-ins on trusted nodes can create things on this nodes via TSM messages
        /// </summary>
        public bool AllowRemoteThingCreation
        {
            get;
            internal set;
        }

        /// <summary>
        /// If set to true, the node has access to the public internet.
        /// </summary>
        public bool HasInternetAccess
        {
            get;
            internal set;
        }
        /// <summary>
        /// If set to true, the built-in User Manager will be used instead of plain Easy-ScopeIDs
        /// </summary>
        public bool IsUsingUserMapper
        {
            get;
            internal set;
        }

        /// <summary>
        /// If set to true, the UPnP discovery system will not automatically connect to other nodes if a compatible node was found.
        /// </summary>
        public bool DisableUPnPAutoConnect { get; set; }
        /// <summary>
        /// If set to true, the WebSockets communication stack will be disabled even if a MyWSStationPort was defined
        /// </summary>
        public bool DisableWebSockets { get; set; }
        /// <summary>
        /// If set to true, a TCP Listener will be used without first attempting to create an HttpListener.
        /// </summary>
        public bool UseTcpListenerInsteadOfHttpListener { get; set; }

        /// <summary>
        /// Enabled this to disable the WebSocket Heartbeat. If the Hearbeat is off and no traffic is flowing between a browser and the first-node, the browser will time out when the session expires
        /// </summary>
        public bool IsWSHBDisabled
        {
            get;
            internal set;
        }
        /// <summary>
        /// If set to False, the UPnP discovery system was disabled
        /// </summary>
        [Obsolete("UPnP has been moved into a plugin")]
        public bool IsUsingUPnP { get; set; }

        /// <summary>
        /// This defines the "StandardDeviceType" for UPnP. Default is "InternetGatewayDevice"
        /// </summary>
        public string UPnPDeviceType { get; set; }

        /// <summary>
        /// If true, the C-DEngine HSI Parameters can be set via Environment variables
        /// </summary>
        public bool AllowEnvironmentVars { get; internal set; }
        /// <summary>
        /// If true, the C-DEngine environment variables will take precedence over settings specified via command line or app.config
        /// /// </summary>
        public bool AllowEnvironmentVarsToOverrideConfig { get; internal set; }
        /// <summary>
        /// Sets the Scan Rate for Discovery. If set to zero, No periodic scanning will take place. Default is 60 seconds
        /// </summary>
        public int DISCOScanRate { get; set; }

        /// <summary>
        /// Sets the UPnP MX record for M-SEARCH broadcasts to a specific value. Should be between 0 and 5. Default is 3
        /// </summary>
        public int DISCOMX { get; set; }

        /// <summary>
        /// Limits the SSDP/UPnP M-SEARCH scans to the defined subnet. can be up to three of the 4 IP segments. i.e.: 10 or 10.1 or 192.168.1
        /// </summary>
        public string DISCOSubnet { get; set; }

        /// <summary>
        /// If true, incoming SETP will not be processed - only use for High throughput Nodes in meshes where the NMI is not needed
        /// </summary>
        public bool RejectIncomingSETP { get; set; }
        /// <summary>
        /// Prevents NMI telegrams from being routed across the Cloud - cloud connected browsers will get telegrams
        /// </summary>
        public bool DontRelayNMI { get; internal set; }

        /// <summary>
        /// Is true if the current node is currently connected to the cloud
        /// </summary>
        public bool IsConnectedToCloud
        {
            get;
            internal set;
        }

        /// <summary>
        /// Allows to create Browser based AdHoc scopes...allowed only on CloudNodes
        /// </summary>
        public bool AllowAdhocScopes
        {
            get;
            internal set;
        }

        /// <summary>
        /// If set to true and the StorageService is alive, the User Information will be stored/retreived via TheStorageService
        /// </summary>
        public bool IsUserManagerInStorage
        {
            get;
            internal set;
        }
        /// <summary>
        /// For debugging purposes, indicate whether message parameters (PLS) can be compressed.
        /// </summary>
        public bool IsOutputCompressed { get; set; }
        /// <summary>
        /// For debugging purposes, this flag can be set to always force output on browsers to a specific platform
        /// </summary>
        public eWebPlatform ForceWebPlatform { get; set; }

        /// <summary>
        /// If true, the relay does support isolation
        /// </summary>
        public bool EnableIsolation { get; internal set; }
        /// <summary>
        /// Set this to true to disable the cloud temporarily
        /// </summary>
        public bool IsCloudDisabled { get; set; }
        /// <summary>
        /// If set, Mobile device will not autologin the last user even if there is a record of it
        /// </summary>
        public bool EnableAutoLogin { get; set; }
        /// <summary>
        /// If set, the cdeEngine will shutdown when it encounters no activated license
        /// </summary>
        public bool ShutdownOnLicenseFailure
        {
            get;
            internal set;
        }
        /// <summary>
        /// Set to true if the current C-DEngine Application Host (TheBaseApplication) is a Viewer (only ONE user logged in at any give time)
        /// </summary>
        public bool IsViewer
        {
            get;
            internal set;
        }


        /// <summary>
        /// If set to true during startup, this node will try to find Cloud nodes using Bing Search.
        /// </summary>
        public bool EnableBingScan { get; set; }
        /// <summary>
        /// True of the C-DEngine is running inside the Mono-Runtime
        /// </summary>
        [Obsolete("Consider using TheCommonUtils.cdeIsFileSystemCaseSensitive or IsOnLinux instead", false)] // CODE REVIEW Is this really still needed/encouraged?
        public bool MonoDetected
        {
            get;
            internal set;
        }
        /// <summary>
        /// True if the C-Dengine is running on Mono
        /// </summary>
        public bool MonoActive
        {
            get { return MonoRTActive; }
        }

        /// <summary>
        /// Set to true if the C-DEngine is using Windows8/Server 2012 WebSockets (http.sys)
        /// </summary>
        public bool IsWebSocket8Active
        {
            get;
            internal set;
        }


        /// <summary>
        /// Defines the time in seconds that triggers a Queue Priority Inversion. If a message is longer in the queue as the specified value in this parameter, the message will be sent next. If this value is zero, the QueuedSender does not perform Priority Inversion
        /// </summary>
        public int PrioInversionTime { get; set; }
        /// <summary>
        /// If set to true, packages/telegrams within a QDX will always be send in FIFO mode. If False, QDX priority smaller 3 will be sent as FILO
        /// </summary>
        public bool EnforceFifoQueue { get; set; }

        /// <summary>
        /// Maximum size of the Sender Queue
        /// </summary>
        public int SenderQueueSize { get; internal set; }
        /// <summary>
        /// If true, the SmartQueue will not change any set priority
        /// </summary>
        public bool DisablePriorityInversion { get; set; }

        /// <summary>
        /// Disables PLS compression (does not compress PLS to PLB if PLB is empty and PLS.Length>512)
        /// </summary>
        public bool DisablePLSCompression { get; set; }
        /// <summary>
        /// If true, the SmartQueue will chunk any message. ATTENTION: If a device cannot handle
        /// large messages, because it ran out of memory or the message is larger than the buffer
        /// a device can handle, the large telegram will be lost!
        /// </summary>
        public bool DisableChunking { get; set; }

        /// <summary>
        /// If set, the current node will route telegrams through that do have different ScopeIDs
        /// from the current node
        /// </summary>
        public bool AllowForeignScopeIDRouting
        {
            get;
            internal set;
        }
        /// <summary>
        /// Set this Guid if you want your device to have a predefined DeviceID. If no DeviceID is
        /// specified, the C-DEngine will create a random DeviceID and stores it at the very first startup.
        /// Any subsequent system starts use this same DeviceID.
        /// If UseRandomDeviceID is set, the C-DEngine will not store this ID.
        /// </summary>
        public Guid PresetDeviceID
        {
            get;
            internal set;
        }
        /// <summary>
        /// Defines the default portal page (see AddSmartPage). By default this should be "NMIPORTAL".
        /// </summary>
        public string PortalPage { get; set; }



        /// <summary>
        /// Sets the base Background color for the "Clean" screen.
        /// </summary>
        public string BaseBackgroundColor { get; set; }
        /// <summary>
        /// Sets the base Foreground color for the Clean screen.
        /// </summary>
        public string BaseForegroundColor { get; set; }

        /// <summary>
        /// If true, the C-DEngine will not cache any resources.
        /// </summary>
        public bool DisableCache
        {
            get;
            internal set;
        }
        /// <summary>
        /// MaxAge of HTTP Cache in Seconds
        /// </summary>
        public int CacheMaxAge
        {
            get;
            internal set;
        }
    }

    /// <summary>
    /// TheChannelInfo contains all important information of a Channel (Connection Definition) for the communication
    /// </summary>
    public class TheChannelInfo : TheDataBase
    {
        /// <summary>
        /// Url of the Node this channel is pointing to
        /// </summary>
        public string TargetUrl { get; set; }
        /// <summary>
        /// Sender Type of the node this channel is pointing to
        /// </summary>
        public cdeSenderType SenderType { get; set; }

        /// <summary>
        /// Messages from this channel will go to channels marked IsOneWay if they match the OneWayTSMFilter
        /// </summary>
        public bool IsTrustedSender { get; set; }
        /// <summary>
        /// Channel does not allow outgoing messages (except for optional TSM filter)
        /// </summary>
        public bool IsOneWay { get; set; }
        /// <summary>
        /// Filter indicating which outgoing TSMs should be allowed into a OneWay channel. Each filter entry is a string that is matched against the TSM.TXT field. 
        /// The filter entry can contain at most one wildcard ('*' character).
        /// </summary>
        public string[] OneWayTSMFilter { get; set; }

        /// <summary>
        /// Timestamp when this Channel was created
        /// </summary>
        public DateTimeOffset CreationTime { get; set; }
        /// <summary>
        /// Timestamp of the last Channel activity
        /// </summary>
        public DateTimeOffset LastInsertTime { get; set; }
        /// <summary>
        /// Number of live References
        /// </summary>
        public int References { get; set; }

        /// <summary>
        /// True if this channels points at a WebSocket Node
        /// </summary>
        public bool IsWebSocket { get; set; }

        /// <summary>
        /// Returns a hash of the scopeid for diagnostics purposes
        /// </summary>
        public string ScopeIDHash { get; set; }
        /// <summary>
        /// Tells if this Channel is for a Device (Phone, Device, HTML5/JS)
        /// </summary>
        public bool IsDeviceType
        {
            get
            {
                if (SenderType == cdeSenderType.CDE_DEVICE || SenderType == cdeSenderType.CDE_JAVAJASON || SenderType == cdeSenderType.CDE_PHONE)
                    return true;
                else
                    return false;
            }
        }

        public string QStatus { get; set; }

        /// <summary>
        /// Returns a friendly representation of TheChannelInfo
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (TargetUrl != null && TargetUrl.StartsWith("file"))
                return $"{SenderType};{cdeMID}".ToString(CultureInfo.InvariantCulture);
            else
                return $"{SenderType};{cdeMID};{TargetUrl}".ToString(CultureInfo.InvariantCulture) + (TruDID != Guid.Empty ? $"({TruDID.ToString().Substring(0, 8)})".ToString(CultureInfo.InvariantCulture) : "");
        }

        /// <summary>
        /// Returns a Markup Version of ToString()
        /// </summary>
        /// <returns></returns>
        public string ToMLString(bool BrAll = false)
        {
            if (!TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog)
                BrAll = false;
            if (TargetUrl != null && TargetUrl.StartsWith("file"))
                return $"{SenderType}{(BrAll ? "<br>" : " ")}{TheCommonUtils.GetDeviceIDML(cdeMID)}";
            else
                return $"{SenderType}{(BrAll ? "<br>" : " ")}{TheCommonUtils.GetDeviceIDML(cdeMID)}{(BrAll ? "<br>" : " ")}({TargetUrl}){(BrAll ? "<br>" : " ")}{(TruDID != Guid.Empty ? $"TruDID:{TheCommonUtils.GetDeviceIDML(TruDID)}" : "")}";
        }

        internal TheSessionState MySessionState { get; set; }
        internal string RealScopeID { get; private set; }       //Primary RScopeID of the QSender
        private readonly List<string> AltScopes = new List<string>();
        internal int RedIdx { get; set; }
        internal Guid TruDID { get; set; }
        private string mClientCertificateThumb;
        internal string ClientCertificateThumb
        {
            get
            {
                if (!string.IsNullOrEmpty(mClientCertificateThumb))
                    return mClientCertificateThumb;
                return TheBaseAssets.MyServiceHostInfo?.ClientCertificateThumb; //use global Thumb if not set specifically for a Channel/QSender
            }
            set
            {
                mClientCertificateThumb = value;
            }
        }

        public TheChannelInfo()
        {
            IsOneWay = TheBaseAssets.MyServiceHostInfo.OneWayRelayMode;
            if (IsOneWay)
            {
                OneWayTSMFilter = TheBaseAssets.MyServiceHostInfo.OneWayTSMFilter;
            }
        }

        public TheChannelInfo(TheChannelInfo pc)
        {
            this.cdeMID = pc.cdeMID;
            this.cdeCTIM = pc.cdeCTIM;
            this.TargetUrl = pc.TargetUrl;
            this.SenderType = pc.SenderType;
            this.CreationTime = pc.CreationTime;
            this.LastInsertTime = pc.LastInsertTime;
            this.References = pc.References;
            this.IsWebSocket = pc.IsWebSocket;
            this.ScopeIDHash = pc.ScopeIDHash;
            this.IsOneWay = pc.IsOneWay;
            this.OneWayTSMFilter = pc.OneWayTSMFilter.ToArray();
        }

        internal TheChannelInfo(Guid pDeviceID)
        {
            cdeMID = pDeviceID;
        }

        /// <summary>
        /// Creates an unscoped channel
        /// </summary>
        /// <param name="pcdeN"></param>
        /// <param name="pSenderType"></param>
        /// <param name="targetUrl"></param>
        internal TheChannelInfo(Guid pDeviceID, cdeSenderType pSenderType, string targetUrl) : this(pDeviceID, null, pSenderType, targetUrl)
        {
        }

        public TheChannelInfo(Guid pDeviceID, cdeSenderType pSenderType, TheSessionState sessionState) : this(pDeviceID, null, pSenderType, null)
        {
            this.MySessionState = sessionState;
        }

        internal TheChannelInfo(Guid pDeviceID, string pScopeID, cdeSenderType pSenderType, string pTargetUrl) : this()
        {
            cdeMID = pDeviceID;
            SenderType = pSenderType;
            TargetUrl = pTargetUrl;
            //TheChannelInfo tC = nsCDEngine.Communication.TheQueuedSenderRegistry.GetChannelInfoByUrl(TargetUrl);
            if (cdeMID == Guid.Empty)
            {
                if (string.IsNullOrEmpty(TargetUrl) || TheCommonUtils.IsUrlLocalhost(TargetUrl))
                {
                    cdeMID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
                }
                else
                {
                    cdeMID = TheBaseAssets.MyScopeManager.GenerateNewAppDeviceID(pSenderType);
                }
            }
            if (pSenderType == cdeSenderType.CDE_BACKCHANNEL && string.IsNullOrEmpty(pScopeID) && TheBaseAssets.MyServiceHostInfo.AllowedUnscopedNodes.Contains(pDeviceID))
            {
                TheLoggerFactory.LogEvent(eLoggerCategory.NodeConnect, $"Allowed Cloud-2-Cloud unscoped Node connected: {pDeviceID}!", eMsgLevel.l3_ImportantMessage);
                TheCDEKPIs.IncrementKPI("UnscopedNodeConnects");
            }
            else
            {
                SetRealScopeID(pScopeID);
            }
        }

        internal void SetRealScopeID(string pRealScopeID)
        {
            RealScopeID = pRealScopeID; //RScope-OK: Update Primary RScope
            if (string.IsNullOrEmpty(RealScopeID))
            {
                TheLoggerFactory.LogEvent(eLoggerCategory.NodeConnect, "Illegal Connect by Unscoped Node Detected!", eMsgLevel.l1_Error, $"{this}");
                //throw new Exception("Illegal Connect by Unscoped Node Detected!"); This would prevent connection of unscoped nodes
            }
            else
                TheLoggerFactory.LogEvent(eLoggerCategory.NodeConnect, "QSender Scope Changed", eMsgLevel.l4_Message, $"{this} ({this?.RealScopeID?.Substring(0, 4).ToUpper()})");
        }

        internal bool AddAltScopes(List<string> pAlternateScopes)
        {
            if (pAlternateScopes?.Count < 1)
                return false;
            bool FoundAtLeastOne = false;
            foreach (var ts in pAlternateScopes)
            {
                if (RealScopeID != ts && !AltScopes.Contains(ts))
                {
                    AltScopes.Add(ts);
                    FoundAtLeastOne = true;
                }
            }
            return FoundAtLeastOne;
        }

        internal bool ContainsAltScope(string pInScope)
        {
            return AltScopes?.Contains(pInScope) == true;
        }

        internal bool HasRScope(string pInScope)
        {
            if ((string.IsNullOrEmpty(RealScopeID) && string.IsNullOrEmpty(pInScope)) || RealScopeID?.Equals(pInScope) == true || (AltScopes.Count > 0 && AltScopes.Contains(pInScope)))    //RScope-OK: New way of verifying Correct RScope
                return true;
            return false;
        }

        internal string GetScopes()
        {
            string ret = "";
            if (!string.IsNullOrEmpty(RealScopeID)) //RScope-OK
                ret += RealScopeID.Substring(0, 4).ToUpper();   //RScope-OK
            foreach (var s in AltScopes)
            {
                ret += $" {s.Substring(0, 4).ToUpper()}";
            }
            return ret;
        }

        /// <summary>
        /// Creates a new Channel Info from cdeSenderType and Target URL
        /// </summary>
        /// <param name="pSenderType">Requested Sender Type of the Channel</param>
        /// <param name="pTargetUrl">Requested URL of the Sender</param>
        public TheChannelInfo(cdeSenderType pSenderType, string pTargetUrl) : this(Guid.Empty, pSenderType, pTargetUrl)
        {
        }
        /// <summary>
        /// Creates a new Channel Info from a Target URL
        /// </summary>
        /// <param name="pTargetUrl">Requested URL of the Sender</param>
        public TheChannelInfo(string pTargetUrl) : this()
        {
            if (pTargetUrl == null)
            {
                SenderType = cdeSenderType.CDE_LOCALHOST;
                TargetUrl = null;
                cdeMID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
                return;
            }
            TargetUrl = pTargetUrl;
            if (TheCommonUtils.IsUrlLocalhost(TargetUrl))
            {
                cdeMID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
                SenderType = cdeSenderType.CDE_LOCALHOST;
            }
            else
            {
                if (!pTargetUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !pTargetUrl.StartsWith("ws", StringComparison.OrdinalIgnoreCase)) //NEW:V3B3 probe for WS!
                    SenderType = cdeSenderType.CDE_BACKCHANNEL;
                else
                    SenderType = cdeSenderType.CDE_SERVICE;
                cdeMID = TheBaseAssets.MyScopeManager.GenerateNewAppDeviceID(SenderType);
            }
        }

        /// <summary>
        /// Returns a public view of the Channel
        /// </summary>
        /// <returns></returns>
        public TheChannelInfo GetPublicChannelInfo()
        {
            return new TheChannelInfo
            {
                CreationTime = this.CreationTime,
                IsWebSocket = this.IsWebSocket,
                LastInsertTime = this.LastInsertTime,
                SenderType = this.SenderType,
                TargetUrl = this.TargetUrl,
                cdeMID = this.cdeMID,
                ScopeIDHash = this.RealScopeID?.Substring(0, 4).ToUpper(),
                References = this.References,
                QStatus = this.QStatus,
                IsOneWay = this.IsOneWay,
            };
        }

        /// <summary>
        /// Compares two TheChannelInfos or this TheChannelInfo to a given TargetUrl
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is string)
            {
                if (!string.IsNullOrEmpty(TargetUrl) && TargetUrl.Equals(obj.ToString(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else
            {
                if (obj is TheChannelInfo tO)
                {
                    if (tO.cdeMID != Guid.Empty && cdeMID != Guid.Empty && tO.cdeMID == cdeMID)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// gets the hash of the channel
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            // ReSharper disable once BaseObjectGetHashCodeCallInGetHashCode
            return base.GetHashCode();
        }

    }

    /// <summary>
    /// Collected information about a given Node.
    /// </summary>
    public class TheNodeInfo : TheDataBase
    {
        /// <summary>
        /// Basic information about the node
        /// </summary>
        public TheNodeInfoHeader MyNodeHeader { get; set; }
        /// <summary>
        /// All Node Parameter
        /// </summary>
        public TheServiceHostInfo MyServiceInfo { get; set; }
        /// <summary>
        /// DeviceID of the Node (Same as in MyServiceInfo.MyDeviceInfo.DeviceID)
        /// </summary>
        public Guid MyNodeID { get; set; }
        /// <summary>
        /// State of all services running on that Node
        /// </summary>
        public List<TheEngineState> MyServices { get; set; }
        /// <summary>
        /// Last HTML-formatted Log.
        /// </summary>
        public string LastHTMLLog { get; set; }
        /// <summary>
        /// List of all channels by topic
        /// </summary>
        public Dictionary<string, List<Guid>> ChannelsByTopic { get; set; }
        /// <summary>
        /// Current known Channels
        /// </summary>
        public List<TheChannelInfo> Channels
        {
            get;
            set;
        }

        /// <summary>
        /// Current CDE Kpis
        /// </summary>
        public Dictionary<string, object> MyCDEKpis { get; set; }

        /// <summary>
        /// Current active license
        /// </summary>
        public string ActiveLicense { get; set; }
        /// <summary>
        /// Instantiates TheNodeInfo
        /// </summary>
        public TheNodeInfo()
        {
            MyServices = new List<TheEngineState>();
        }
    }

    /// <summary>
    /// States of a Built- or Plugin-Service
    /// </summary>
    public class TheEngineState : TheMetaDataBase
    {
        /// <summary>
        /// Friendly Name of the Service
        /// </summary>
        public string FriendlyName { get; set; }
        /// <summary>
        /// Class Name of the Service (recommended is the FullName of the class that runs this service)
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// Full Type name of the Plugin Type - Required to load resources from the Plugin
        /// </summary>
        public Type EngineType { get; set; }  //This should not be public or deserializing of this property will crash during CDE_GET_SYSTEMINFO. Alternative TheEngineStateClone has to be used for deserialization
        /// <summary>
        /// Station URL of this service
        /// </summary>
        public string MyStationUrl { get; set; }
        /// <summary>
        /// Version of this Service
        /// </summary>
        public string Version { get; set; }
        /// <summary>
        /// New Version of an available Update the ISM service has found
        /// </summary>
        public double NewVersion { get; set; }
        /// <summary>
        /// Minimum version of the C-DEngine required by the plug-in
        /// </summary>
        public string CDEMinVersion { get; set; }

        /// <summary>
        /// Indicates that the plug-in requires an activated license
        /// </summary>
        public bool IsLicensed
        {
            get;
            internal set;
        }

        /// <summary>
        /// Guid of the Default Dashboard
        /// </summary>
        public string Dashboard { get; set; }

        /// <summary>
        /// Current StatusLevel of the Engine
        /// </summary>
        public int StatusLevel
        {
            get;
            internal set;
        }
        /// <summary>
        /// Contains the Guid of the Thing in the Engine with the highest StatusLevel
        /// </summary>
        public Guid HighStatusThing { get; set; }

        internal bool mIsConnected = false;
        /// <summary>
        /// True if the engine has a connection to another node
        /// </summary>
        public bool IsConnected { get { return mIsConnected; } }
        /// <summary>
        /// Engine ID
        /// </summary>
        public Guid EngineID { get; set; }
        /// <summary>
        /// Guid of the Home screen of this Engine
        /// </summary>
        public string EngineHomeScreen { get; set; }

        /// <summary>
        /// Amount of client-nodes connected to this Service
        /// </summary>
        public int ConnectedClientNodes { get; set; }
        /// <summary>
        /// Amount of Services connected to this node (In case the current node is a relay)
        /// </summary>
        public int ConnectedServiceNodes { get; set; }
        /// <summary>
        /// Amount of Services this node is connected to
        /// </summary>
        public int ConnectedToServices { get; set; }

        /// <summary>
        /// Current Index into the RedundancyURLs list of existing failover Channels
        /// </summary>
        public int RedundancyIndex { get; set; }

        /// <summary>
        /// A color representation of this Service for UX purposes
        /// </summary>
        public string EngineColor { get; set; }
        /// <summary>
        /// Is set to True if the ISM found an update for this Service
        /// </summary>
        public bool IsUpdateAvailable { get; set; }
        /// <summary>
        /// Is set to True if this Service is actively communicating. IsLiveEngine is FALSE if the plugin is suspended/unloaded
        /// </summary>
        public bool IsLiveEngine { get; internal set; }
        /// <summary>
        /// Is set to True if this service is just running in Simulation Mode
        /// </summary>
        public bool IsSimulated { get; set; }
        /// <summary>
        /// If an Engine is a Service it can provide data
        /// If an Engine is NOT a Services it can consume data (Mainly used in end devices such as Mobile Phones and Browsers)
        /// </summary>
        public bool IsService { get; set; }
        /// <summary>
        /// RETIRED IN V4: Always FALSE
        /// Is set to True if this channel can talk to multiple services at the same time
        /// If set to False, this service only talks to ONE service specified in the "Active Channel"
        /// </summary>
        /// <seealso cref="M:nsCDEngine.Engines.IBaseEngine.SetMultiChannel(System.Boolean)">Setting
        /// Multi-Channel Mode</seealso>
        [Obsolete("No Longer used in V4 - will be removed in V5")]
        public bool IsMultiChannel { get; set; }

        /// <summary>
        /// Is true if the plugin is running in an isolated host process
        /// </summary>
        public bool IsIsolated { get; internal set; }
        /// <summary>
        /// If set to True, this service is not doing anything except relaying information between other nodes
        /// In order to define a Mini (Relay) Service use the key "RelayOnly" in the App.Config/Web.Config and the string defined in the "ClassName"
        /// </summary>
        /// <seealso cref="M:nsCDEngine.Engines.IBaseEngine.SetIsMiniRelay(System.Boolean)">Setting
        /// MiniRelay Mode</seealso>
        public bool IsMiniRelay { get; set; }
        /// <summary>
        /// Returns true if the plugin-service is initialized.
        /// This will be set if the service calls "SetInitialized()" and cannot be changed
        /// </summary>
        /// <returns></returns>
        public bool IsInitialized
        {
            get;
            internal set;
        }

        /// <summary>
        /// If true, the Plugin will no longer participate in any communication - it is virtually unloaded and non-functioning
        /// </summary>
        public bool IsUnloaded
        {
            get;
            internal set;
        }
        /// <summary>
        /// Returns true if the plugin-service is trying to initialize
        /// Data-Consumers can call InitAndSubscribe to establish a direct connection to the data-provider.
        /// The Data-Provider will return a CDE_INITIALIZED message that tells the consumer that the service is initialized and ready to receive telegrams
        /// During this asynchronous process the data-consumers IsInitializing will be true. Once CDE_INITIALIZED was received, IsInitializing will be set to false.
        /// </summary>
        /// <returns></returns>
        /// <seealso cref="M:nsCDEngine.Engines.IBaseEngine.InitAndSubscribe(heCommunicationChannel pChannel)">Initializing a connection to a data provider
        /// </seealso>
        public bool IsInitializing { get; set; }
        /// <summary>
        /// A Style Sheet that has to be loaded for the Engines HMTL or FacePlates
        /// If lite and Dark schemes need to be supported, separate the two styles with a semicolon (i.e. "myDark.css;myLite.css")
        /// RETIRED: Please use RegisterCSS(...) instead - CSS only works on the local node
        /// </summary>
        public string CSS { get; set; }

        /// <summary>
        /// if set to true, the Service has a custom stylesheet that needs to be injected into the browser
        /// </summary>
        public bool HasCustomCSS { get; set; }
        /// <summary>
        /// Set to True if this engine provides a JavaScript engine as well
        /// </summary>
        public bool HasJSEngine { get; set; }
        /// <summary>
        /// The Engine is currently Resetting channels
        /// </summary>
        public bool InReset { get; set; }


        /// <summary>
        /// True if the engine was started properly.
        /// </summary>
        public bool IsStarted { get; set; }
        /// <summary>
        /// A channel of an Engine is currently starting up
        /// </summary>
        public bool IsInChannelStartup { get; set; }

        /// <summary>
        /// An Engine is starting up
        /// </summary>
        public bool IsInEngineStartup { get; set; }

        /// <summary>
        /// 	<para>If set to true, the plugin can receive scoped messages ...but only
        /// if:</para>
        /// 	<list type="bullet">
        /// 		<item>
        /// 			<description>The Plugin-Service is a LiveService</description>
        /// 		</item>
        /// 		<item>
        /// 			<description>or The Plugin-Service is hosted on a Cloud-Node</description>
        /// 		</item>
        /// 		<item>
        /// 			<description>and if the Hosting Node is NOT scoped</description>
        /// 		</item>
        /// 	</list>
        /// </summary>
        public bool IsAllowedUnscopedProcessing { get; set; }

        /// <summary>
        /// If true, the plugin can be isolated and running in a child process
        /// </summary>
        public bool IsAllowedForIsolation { get; internal set; }

        /// <summary>
        /// If true, the plugin can be moved to another node by the C-DEngine
        /// </summary>
        public bool IsAllowedToNodeHopp { get; internal set; }
        /// <summary>
        /// If this parameter is set, a plugin is allowed to process foreign ScopeIDs
        /// This parameter ONLY works on RELAY nodes with "AllowForeignScopeIDRouting" set to true
        /// </summary>
        public bool IsAllowedForeignScopeProcessing { get; set; }
        /// <summary>
        /// If true, the Plugin-Service allows to receive FilePush Telegrams
        /// </summary>
        public bool IsAcceptingFilePush { get; set; }
        /// <summary>
        /// Is set to True when the service is about to stop
        /// </summary>
        public bool IsEngineStopping { get; set; }
        /// <summary>
        /// Is set to True when the service was Ready (connected to a Data Providing Service) before
        /// </summary>
        public bool WasEngineReadyBefore { get; set; }
        /// <summary>
        /// Shows the last Readiness State. Has to be used and set by the UX
        /// </summary>
        public bool LastEngineReadiness { get; set; }

        /// <summary>
        /// Date and Time of the last Initialization Success that was sent back to a requestion Service
        /// </summary>
        public DateTimeOffset LastSentInitialized { get; set; }

        /// <summary>
        /// Date and Time of the last Initialization that was sent to the data providing Service
        /// </summary>
        public DateTimeOffset LastSentInitialize { get; set; }


        /// <summary>
        /// Current Counter delaying the next try to CDE_INITIALIZE
        /// </summary>
        public int InitWaitCounter { get; set; }

        /// <summary>
        /// Last time Subscribe was sent
        /// </summary>
        public DateTimeOffset LastSentSubscribe { get; set; }

        /// <summary>
        /// List of DeviceID Guids of clients connected to this service
        /// </summary>
        public List<Guid> ConnectedClientsList { get; set; }
        /// <summary>
        /// List of DeviceID Guis of all nodes connected to this service (Clients and Services if a relay node)
        /// </summary>
        public List<Guid> ConnectedNodesList { get; set; }
        /// <summary>
        /// RETIRED IN V4: Always returns null
        ///
        /// </summary>
        public List<Guid> ConnectedToServicesList { get; set; }

        /// <summary>
        /// List of all known current endpoints. Requires "UpdateEngineState()" to be filled in
        /// </summary>
        [Obsolete("Retired in V4 - will be removed in V5")]
        public List<TheChannelInfo> CurrentEndpoints { get; set; }

        /// <summary>
        /// RETIRED IN V4: No more channels
        /// </summary>
        [Obsolete("Retired in V4 - will be removed in V5")]
        public TheChannelInfo ActiveChannelInfo { get; set; }

        /// <summary>
        /// Amount of credits for usage of this Service accumulated so far
        /// </summary>
        public long CommunicationCosts { get; set; }

        /// <summary>
        /// The NodeID of the node providing the data to !IsServices (i.e Storage Client Service)
        /// </summary>
        public Guid ServiceNode { get; set; }

        /// <summary>
        /// List of Cookies a Plugin can add to this Service Information
        /// </summary>
        public List<string> CustomValues { get; set; }

        /// <summary>
        /// Friendly output
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {

            return string.Format(CultureInfo.InvariantCulture, "{0} = {1} / {2} / {3} / {4}", FriendlyName, ServiceNodeText, IsLiveEngine, IsSimulated, IsEngineReady/*, IsMultiChannel*/);
        }

        private string mLastMessage;
        /// <summary>
        /// New: Cache its engine thing for performance optimization
        /// </summary>
        private TheThing mEngineThing;
        /// <summary>
        /// Return the last message set for this service
        /// </summary>
        public string LastMessage
        {
            get { return mLastMessage; }
            internal set
            {
                mLastMessage = value;
                if (mEngineThing == null)
                    mEngineThing = TheThingRegistry.GetBaseEngineAsThing(ClassName);
                if (mEngineThing != null)
                    mEngineThing.LastMessage = mLastMessage;
            }
        }
        public DateTimeOffset UpdateDate
        {
            get
            {
                if (mEngineThing == null)
                    mEngineThing = TheThingRegistry.GetBaseEngineAsThing(ClassName);
                if (mEngineThing != null)
                    return TheThing.GetSafePropertyDate(mEngineThing, "UpdateDate");
                return DateTimeOffset.MinValue;
            }
        }
        public DateTimeOffset RegisterDate
        {
            get
            {
                if (mEngineThing == null)
                    mEngineThing = TheThingRegistry.GetBaseEngineAsThing(ClassName);
                if (mEngineThing != null)
                    return TheThing.GetSafePropertyDate(mEngineThing, "RegisterDate");
                return DateTimeOffset.MinValue;
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Gets or sets a value indicating whether this Engine is disabled. </summary>
        ///
        /// <value> True if this engine is disabled, false if not. </value>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public bool IsDisabled
        {
            get
            {
                if (mEngineThing == null)
                    mEngineThing = TheThingRegistry.GetBaseEngineAsThing(ClassName);
                if (mEngineThing == null)
                    return true;
                return mEngineThing.IsDisabled;
            }
            set
            {
                if (mEngineThing == null)
                    mEngineThing = TheThingRegistry.GetBaseEngineAsThing(ClassName);
                if (mEngineThing != null)
                    mEngineThing.IsDisabled = value;
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Gets the control text. To toggle an engine </summary>
        ///
        /// <value> The control text. </value>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public string ControlText
        {
            get
            {
                if (!IsDisabled)
                {
                    if (!IsUnloaded)
                        return "<$IsLoaded$>";
                    else
                        return "<$IsUnloaded>";
                }
                else
                {
                    return "<$NoBreak$>";
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Gets the ISO text to toggle an engine to be isolated or not </summary>
        ///
        /// <value> The ISO text. </value>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public string IsoText
        {
            get
            {
                if (IsIsolated)
                    return "<$IsIso$>";
                else
                    return "<$IsoNot$>";
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Gets the disable text to be shown if an engine is disabled </summary>
        ///
        /// <value> The disable text. </value>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public string DisableText
        {
            get
            {
                //if (IsIsolated)
                //{
                if (!IsDisabled)
                    return "<$IsEnabled$>";
                else
                    return "<$IsDisabled$>";
                //}
                //else
                //{
                //    return "<i class='fa fa-3x' style='color:gray; opacity:0.1'>&#xf05E;</i>";
                //}
            }
        }

        /// <summary>
        /// True if the Engine has a live object
        /// </summary>
        public bool IsAlive
        {
            get
            {
                TheThing tThing = TheThingRegistry.GetBaseEngineAsThing(ClassName);
                if (tThing != null && tThing.GetObject() != null) return true;
                return false;
            }
        }

        private bool mIsEngineReady;
        /// <summary>
        /// Gets and sets the ready state of this Service
        /// </summary>
        public bool IsEngineReady
        {
            get { return mIsEngineReady; }
            set
            {
                mIsEngineReady = value;
                if (mIsEngineReady)
                {
                    EngineColor = IsLiveEngine ? "green" : "yellow";
                }
                else
                    EngineColor = "red";
            }
        }


        /// <summary>
        /// Returns the current connection context to a ServiceNode (for !IsService)
        /// </summary>
        public string ServiceNodeText
        {

            get
            {
                string aURL = "";
                if (IsService)
                    aURL = "THIS-NODE"; // TheBaseAssets.MyServiceHostInfo.MyServiceURL;
                else
                {
                    if (ServiceNode != Guid.Empty)
                    {
                        aURL = ServiceNode == TheBaseAssets.LocalHostQSender.MyTargetNodeChannel.cdeMID ? "THIS-NODE" : ServiceNode.ToString();
                    }
                    if (string.IsNullOrEmpty(aURL))
                    {
                        if (IsMiniRelay)
                            aURL = "Relay-Only";
                        else
                            aURL = "waiting...";
                    }
                }
                return aURL;
            }
        }

        /// <summary>
        /// Returns a small status reply of the current Engine State
        /// </summary>
        public string QuickStatus
        {
            get
            {
                return $"{ServiceNodeText} / {IsLiveEngine} / {IsSimulated} / {IsEngineReady}";
            }
        }

        /// <summary>
        /// RETIRED in V4: no more channels
        /// </summary>
        public string RegisteredNodeIDs
        {

            get
            {
                return null;
            }
        }

        /// <summary>
        /// RETIRED in V4: no more channels
        /// </summary>
        /// <returns></returns>
        public string QueueLength
        {

            get
            {
                return "0";
            }
        }


        /// <summary>
        /// RETIRED IN V4: No more channels
        /// </summary>
        internal string ChannelInfo
        {
            get
            {
                return "";
            }
        }

        /// <summary>
        /// Initializes TheServiceState
        /// </summary>
        public TheEngineState()
        {
            EngineColor = "white";
            IsEngineReady = false;
            LastMessage = "All OK";
            IsInChannelStartup = true;
        }

    }

    public class TheNodeTopics : TheMetaDataBase
    {
        public cdeSenderType NodeType { get; set; }
        public Guid DeviceID { get; set; }
        public List<string> Topics { get; set; }
    }

    #region Serialization Clones
    /// <summary>
    /// This is the clone of the main configuration class of the C-DEngine hosted as a service inside an application used for serialization
    /// </summary>
    /// <remarks></remarks>
    public class TheServiceHostInfoClone : TheDataBase
    {
        /// <summary>
        /// The cdeHostType of the node running the current instance of the C-DEngine
        /// </summary>
        public cdeHostType cdeHostingType
        {
            get;
            set;
        }
        /// <summary>
        /// The cdeNodeType defines the communication architecture used for this node.
        /// </summary>
        public cdeNodeType cdeNodeType
        {
            get;
            set;
        }

        /// <summary>
        /// Return the platform the current instance if running on
        /// </summary>
        public cdePlatform cdePlatform
        {
            get;
            set;
        }

        /// <summary>
        /// Creates a SID with every TSM constructor
        /// </summary>
        public bool DisableFastTSMs
        {
            get; set;
        }

        /// <summary>
        /// OS the node is running on
        /// </summary>
        public string OSInfo { get; set; }
        /// <summary>
        /// Current Code Signing Thumb Print
        /// </summary>
        public string CodeSignThumb
        {
            get;
            set;
        }

        /// <summary>
        /// Current client certificate thumb print
        /// </summary>
        public string ClientCertificateThumb
        {
            get;
            set;
        }

        /// <summary>
        /// Ignores any SSL certificate validation errors (default behavior before 4.208)
        /// </summary>
        public bool IgnoreServerCertificateErrors
        {
            get;
            set;
        }
        /// <summary>
        /// Allows SSL certificates for which the trust chain could not be validated (including expired certificates)
        /// </summary>
        public bool IgnoreServerCertificateChainErrors
        {
            get;
            set;
        }
        /// <summary>
        /// Allows SSL connections to servers that do not provide an SSL certificate.
        /// </summary>
        public bool IgnoreServerCertificateNotAvailable
        {
            get;
            set;
        }
        /// <summary>
        /// Allows SSL connections to servers for which the provided certificate does not match the host name.
        /// </summary>
        public bool IgnoreServerCertificateNameMismatch
        {
            get;
            set;
        }

        /// <summary>
        /// The DeviceInfo contains all the information about the device the current instance is running on:
        /// MyDeviceInfo = Hardware Device - Where is the CDE running on?
        /// cdePlatform = OS Version - Are we on Mono, 64Bit or 32Bit?
        /// cdeHostingType = Application Type - Where is the CDE running in?
        /// cdeNodeType = Communication Type - How can this node communicate with other nodes?
        /// </summary>
        public TheDeviceRegistryData MyDeviceInfo
        {
            get;
            set;
        }

        /// <summary>
        /// Set to true if the Node was run for the first time
        /// </summary>
        public bool IsNewDevice
        {
            get;
            set;
        }

        /// <summary>
        /// Is true if Costing per telegram is enabled on this node
        /// </summary>
        public bool EnableCosting
        {
            get;
            set;
        }

        /// <summary>
        /// If set to true, the CDEngine is running embedded/integrated in another web solution
        /// </summary>
        public bool EnableIntegration
        {
            get;
            set;
        }

        /// <summary>
        /// If set to true, the CDEngine is will process device messages in the initial CDE_CONNECT message. Note that, while secure, this will bypass the additional security measures provided by the CDEngine sessions like protection against replay attacks.
        /// </summary>
        public bool AllowMessagesInConnect
        {
            get;
            set;
        }

        /// <summary>
        /// Is set to true when all Engines (Plugin-Service) have been started
        /// </summary>
        public bool AreAllEnginesStarted
        {
            get;
            set;
        }

        /// <summary>
        /// Set when the C-DEngine starts all Plugin Services
        /// </summary>
        public bool AllEnginesAreStarting
        {
            get;
            set;
        }
        public bool AllSystemsReady
        {
            get;
            set;
        }

        /// <summary>
        /// This structure allows to set all important timeouts of the C-DEngine
        /// </summary>
        public TheTimeouts TO { get; set; }
        /// <summary>
        /// Retired
        /// </summary>
        public string MyDeviceMoniker
        {
            get;
            set;
        }
        /// <summary>
        /// Retired
        /// </summary>
        public string MyStationMoniker
        {
            get;
            set;
        }

        /// <summary>
        /// Enables the Access Tracker for User Activities
        /// </summary>
        public bool TrackAccess
        {
            get;
            set;
        }

        /// <summary>
        /// Amount of Seconds Delay to check on Heartbeat
        /// </summary>
        public int HeartbeatDelay
        {
            get;
            set;
        }

        /// <summary>
        /// Friendly name of the current Node/Station. Uses the MyStationURL if not set
        /// </summary>
        public string MyStationName
        {
            get;
            set;
        }
        /// <summary>
        /// The main URL of this Node
        /// </summary>
        public string MyStationURL
        {
            get;
            set;
        }    //MSU-OK
        /// <summary>
        /// A list of alternative URLs that this node might have and allows connection to.
        /// Any URL not specified in MyStationURL and MyAltStationURLs will not allow inbound communication
        /// </summary>
        public List<string> MyAltStationURLs
        {
            get;
            set;
        }
        /// <summary>
        /// If known, this holds the IP of the current instance of the C-DEngine
        /// </summary>
        public string MyStationIP
        {
            get;
            set;
        }
        /// <summary>
        /// The inbound port of the current instance for HTTP connections. If set to zero, this node will not accept incoming connections
        /// </summary>
        public ushort MyStationPort { get; set; }
        /// <summary>
        /// In inbound port for WebSockets connections. If set to zero, this node will not accept inbound WebSockets connections
        /// </summary>
        public ushort MyStationWSPort { get; set; }

        /// <summary>
        /// Allows to give the node a friendly name.
        /// </summary>
        public string NodeName { get; set; }
        /// <summary>
        /// If this is set to true, the C-DEngine will not accept inbound connection on HTTP but requires HTTPS
        /// </summary>
        public bool IsSSLEnforced
        {
            get;
            set;
        }

        public Guid ConnToken { get; set; }

        /// <summary>
        /// NEW:3.2 If set to true, the User Information is no longer replicated via the cloud and users cannot login to the NMI via the cloud
        /// To set from a plugin use "TheCommCore.SetCloudNMIBlock(true|false, GUID pUser); only Admins on the "FirstNode" can change this setting
        /// </summary>
        public bool IsCloudNMIBlocked
        {
            get;
            set;
        }
        /// <summary>
        /// if set to true, unscoped nodes can create their own mesh
        /// </summary>
        public bool AllowUnscopedMesh
        {
            get;
            set;
        }
        /// <summary>
        /// retired
        /// </summary>
        public bool FallbackToSimulation
        {
            get;
            set;
        }

        /// <summary>
        /// If set to True, the Browser will not be able to RSA Encrypt, ScopeID, UID or Password
        /// In the future we might use our own security algorithm here since the RSA Implementation of .NET varies on all platforms!
        /// </summary>
        public bool DisableRSAToBrowser
        {
            get;
            set;
        }
        /// <summary>
        /// If Set to true, the C-DEngine will accept "http://localhost" as an additional inbound URL
        /// </summary>
        public bool AllowLocalHost { get; set; }
        /// <summary>
        /// If set to true, the C-DEngine will create a new random DeviceID every time it starts up.
        /// Benefit: Great for debugging and ad-hoc devices
        /// Be aware: no data is made persitant! The ThingRegistry and other databasees will be cleared with every restart
        /// </summary>
        public bool UseRandomDeviceID
        {
            get;
            set;
        }

        /// <summary>
        /// If True, all plugins/services/engines will be registered, Init and CreateUX asynchronously. InitEngineAssets will be called synchronously before
        /// </summary>
        public bool AsyncEngineStartup
        {
            get;
            set;
        }

        /// <summary>
        /// If True, this instance is only hosting one plugin isloated. It connects to a master Node
        /// </summary>
        public bool IsIsolated
        {
            get;
            set;
        }
        /// <summary>
        /// RETIRED in V4: LocalServiceRoutes and CloudServiceRoutes will be mapped to ServiceRoute - no differnce between Cloud and Local route in V4
        /// Entry will be removed in V5
        /// </summary>
        public string LocalServiceRoute
        {
            get;
            set;
        }
        /// <summary>
        /// RETIRED in V4: LocalServiceRoutes and CloudServiceRoutes will be mapped to ServiceRoute - no differnce between Cloud and Local route in V4
        /// Entry will be removed in V5
        /// </summary>
        public string CloudServiceRoute
        {
            get;
            set;
        }

        /// <summary>
        /// Url and Path to a designated next node with the same ApplicationID. This can point to multiple nodes by separating the URLs with a semicolon (;)
        /// </summary>
        public string ServiceRoute { get; set; }
        /// <summary>
        /// If this value is greater than zero and a telegram comes in with a higher node count in the telegram, it will be rejected.
        ///
        /// </summary>
        public int MaximumHops { get; set; }


        /// <summary>
        /// If set to higher than 3 every telegram will include a new RSA Key. On Devices with slow or no TPMs creation of an RSA Key can take multiple seconds
        /// If set to lower than 3 the C-DEngine will accept untrusted SSL Certificates. This might be important for debugging against a self-signed certificate.
        /// Default is 3
        /// </summary>
        public int SecurityLevel { get; set; }

        /// <summary>
        /// If true, each scoped node in a mesh can provide resources for any other node without the need for a browser to have a valid scoped session.
        /// This is a (relative uncritical) security downgrade: normally a browser session has to be authenticated against a scope first. But since resources are considered "static" or "outside NMI Scope" they would need their own user management anyway.
        /// </summary>
        public bool AllowDistributedResourceFetch { get; set; }

        /// <summary>
        /// Base directory of the current C-DEngine instance
        /// </summary>
        public string BaseDirectory
        {
            get;
            set;
        }

        /// <summary>
        /// Application Name - has to match all nodes in mesh for UPnP auto-connect
        /// </summary>
        public string ApplicationName { get; set; }
        /// <summary>
        /// Application title for the Portal screen - this can be any arbitrary string
        /// </summary>
        public string ApplicationTitle { get; set; }
        /// <summary>
        /// should contain "companyXYZ presents..."
        /// Will be printed on Default about us pages
        /// </summary>
        public string MyAppPresenter { get; set; }

        /// <summary>
        /// Main SKU of the current node
        /// </summary>
        public uint SKUID { get; set; }
        /// <summary>
        /// Cloud Blob Address
        /// </summary>
        public string CloudBlobURL { get; set; }
        /// <summary>
        /// Current version of the C-DEngine or your application
        /// Use this format:
        /// FullVersion.MinorVersion
        /// </summary>
        public double CurrentVersion { get; set; }
        /// <summary>
        /// Guid of the Application Owner
        /// This guid will allow easy access to vendor information in the Plugin-Store
        /// </summary>
        public Guid VendorID { get; set; }
        /// <summary>
        /// Custom Vendor Data that will be included in the UPnP metadata
        /// You will get this ID assigned from from C-Labs once you have licensed the C-DEngine
        /// </summary>
        public string VendorData { get; set; }
        /// <summary>
        /// Human Readable name of the Vendor creating this Application
        /// </summary>
        public string VendorName { get; set; }
        /// <summary>
        /// A URl pointing at the Vendors WebSite
        /// </summary>
        public string VendorUrl { get; set; }

        /// <summary>
        /// Copyright statement for relay
        /// </summary>
        public string Copyrights { get; set; }
        /// <summary>
        /// Set to enable Windows Azure cloud analytics if you host the C-DEngine in Windows Azure
        /// </summary>
        public Guid AzureAnalytics
        {
            get;
            set;
        }

        /// <summary>
        /// If set to true, the ISM - Intelligent Service Management will be activated.
        /// This has to be set before call ing StartBaseApplication()
        /// </summary>
        public bool StartISM { get; set; }
        /// <summary>
        /// If set to true, the UPnP Discovery System will be enabled.
        /// </summary>
        public bool StartISMDisco { get; set; }

        /// <summary>
        /// If set to true, the C-DEngine will listen to USB ports for USB sticks containing Updates and new Plugins
        /// </summary>
        public bool ISMScanForUpdatesOnUSB { get; set; }
        /// <summary>
        /// If set to true, the C-DEngine will scan for new updates during the startup
        /// </summary>
        public bool ISMScanOnStartup { get; set; }
        /// <summary>
        /// Defines the file extension for updates
        /// </summary>
        public string ISMExtension { get; set; }
        /// <summary>
        /// Specifies a specific directory to look for updates.
        /// Default is: BaseDirectory/ClientBin/Updates
        /// </summary>
        public string ISMUpdateDirectory { get; set; }
        /// <summary>
        /// Contains the name of the running Application hosting the C-DEngine
        /// </summary>
        public string ISMMainExecutable { get; set; }
        /// <summary>
        /// Will contain the version of an update found in the directory or a stick
        /// </summary>
        public double ISMUpdateVersion { get; set; }

        /// <summary>
        /// if true, the C-DEngine is running in "Unsafe" mode and custom plugins can be installed
        /// </summary>
        public bool DontVerifyTrust
        {
            get;
            set;
        }

        /// <summary>
        /// If true, the C-DEngine will verify the Trust path of the code-signing certificate. Internet connection is REQUIRED
        /// </summary>
        public bool VerifyTrustPath
        {
            get;
            set;
        }

        /// <summary>
        /// retired
        /// </summary>
        public List<string> StartupEngines
        {
            get;
            set;
        }

        /// <summary>
        /// the current ISO Engine of the node. Only this engine/plugin is loaded
        /// </summary>
        public string IsoEngine
        {
            get;
            set;
        }
        /// <summary>
        /// A list of plugins that should not be started even if they were found in the BaseDirectory
        /// </summary>
        public List<string> IgnoredEngines
        {
            get;
            set;
        }
        /// <summary>
        /// A list of plugins that will be hosted isolated in their own process
        /// </summary>
        public List<string> IsoEngines
        {
            get;
            set;
        }
        /// <summary>
        /// A list of confirmed plugins that will be hosted isolated in their own process
        /// </summary>
        public List<string> IsoEnginesToLoad
        {
            get;
            set;
        }
        /// <summary>
        /// retired
        /// </summary>
        public List<string> SimulatedEngines { get; set; }
        /// <summary>
        /// A list of virtual plugins not running on the current node but are allowed to route telegrams through this node.
        /// </summary>
        public List<string> RelayEngines { get; set; }
        /// <summary>
        /// A list of node IDs that are allowed to participate in the mesh communication although they do not have a proper ScopeID.
        /// This is useful for unscoped small devices such as sensors that need to send information to the mesh but do not have the proper OS to participate in the encrypted security of C-DEngine nodes
        /// </summary>
        public List<string> PermittedUnscopedNodesIDs
        {
            get;
            set;

        }

        /// <summary>
        /// Live time for User Refresh Tokens
        /// </summary>
        public int TokenLiveTime
        {
            get;
            set;
        }

        /// <summary>
        /// Allows to throttle WebSocket Messages to JavaScript clients. Messages will not be sent faster than this number
        /// Default is zero (No Throttling)
        /// </summary>
        public int WsJsThrottle
        {
            get;
            set;

        }

        /// <summary>
        /// New in V3.200: Maximum amount of telegrams batched together
        /// </summary>
        public int MaxBatchedTelegrams
        {
            get;
            set;
        }

        /// <summary>
        /// The time interval the ThingRegistry will be saved to Disk
        /// </summary>
        public int ThingRegistryStoreInterval { get; set; }


        /// <summary>
        /// Minimum Version of plugin required to run
        /// </summary>
        public string MinPluginVersion
        {
            get;
            set;

        }

        /// <summary>
        /// Current CDE-T Version
        /// </summary>
        public string ProtocolVersion
        {
            get;
            set;

        }

        /// <summary>
        /// The NMI Screen with the main Relay Configuration Page
        /// </summary>
        public string MainConfigScreen { get; set; }

        /// <summary>
        /// RETIRED in V4: will be replaced in V5. Use MyLiveServices instead
        /// </summary>
        public string MyAppServices
        {
            get;
            set;

        }
        public string MyLiveServices
        {
            get;
            set;

        }

        public string Title { get; set; }
        public string Description { get; set; }
        public string SiteName { get; set; }
        public string TrgtSrv { get; set; }
        public string DebTrgtSrv { get; set; }
        public string SrvSec { get; set; }

        /// <summary>
        /// The Root Path on the current node.
        /// </summary>
        public string RootDir
        {
            get;
            set;

        }
        public string LoginReferrers { get; set; }
        public string DefLoginPage { get; set; }
        public string DefAccountPage { get; set; }
        public string DefSignupPage { get; set; }
        public string Version
        {
            get;
            set;
        }
        public string Revision
        {
            get;
            set;
        }
        public DateTimeOffset EntryDate
        {
            get;
            set;

        }
        public DateTimeOffset LastUpdate
        {
            get;
            set;

        }
        public Guid ContentTemplate { get; set; }
        public string SealID { get; set; }
        public string CSSMain { get; set; }
        public string CSSDlg { get; set; }
        public Guid TopLogo { get; set; }
        public Guid TopLogoURL { get; set; }
        public int SessionTimeout { get; set; }
        public string DefHomePage { get; set; }
        public string Robots_txt { get; set; }

        /// <summary>
        /// The UserAgent used by the REST commands that can be overwritten for special Firewall policies.
        /// </summary>
        public string CustomUA { get; set; }

        /// <summary>
        /// Path for JavaScript resources if Engine is embedded in other cloud solution
        /// </summary>
        public string ResourcePath { get; set; }
        public string P3PPolicy_XML { get; set; }
        public string Redirect404 { get; set; }
        public string favicon_ico { get; set; }
        public string TileImage { get; set; }
        public string TileColor { get; set; }
        public string UPnPIcon { get; set; }
        public string OptScrRes4by3 { get; set; }
        public string OptScrRes16by9 { get; set; }
        public string navbutton_color { get; set; }
        public int DefaultLCID { get; set; }

        /// <summary>
        /// If set to True, the system will store Message in a Distributed Storage Mirror
        /// </summary>
        public bool StoreLoggedMessages { get; set; }
        /// <summary>
        /// If this is not set, the node will not start if it was not launched with Administrator Previledges
        /// </summary>
        public bool IgnoreAdminCheck { get; set; }
        /// <summary>
        /// retired
        /// </summary>
        public bool AllowAnonymousAccess
        {
            get;
            set;
        }

        /// <summary>
        /// If true, remote nodes can force an automatic update of this node
        /// </summary>
        public bool AllowAutoUpdate
        {
            get;
            set;

        }


        /// <summary>
        /// Disables the TLS 1.2 Requirement
        /// </summary>
        public bool DisableTls12
        {
            get;
            set;

        }


        /// <summary>
        /// If set to true, many administrative operations can be performed from any FR in the mesh, not just from the FR that is to be managed ("FirstNode").
        /// </summary>
        public bool AllowRemoteAdministration
        {
            get;
            set;

        }

        /// <summary>
        /// If set to true, plug-ins on trusted nodes can create things on this nodes via TSM messages
        /// </summary>
        public bool AllowRemoteThingCreation
        {
            get;
            set;
        }

        public bool HasInternetAccess
        {
            get;
            set;
        }
        /// <summary>
        /// If set to true, the built-in User Manager will be used instead of plain Easy-ScopeIDs
        /// </summary>
        public bool IsUsingUserMapper
        {
            get;
            set;

        }

        /// <summary>
        /// If set to true, the UPnP discovery system will not automatically connect to other nodes if a compatible node was found.
        /// </summary>
        public bool DisableUPnPAutoConnect { get; set; }
        /// <summary>
        /// If set to true, the WebSockets communication stack will be disabled even if a MyWSStationPort was defined
        /// </summary>
        public bool DisableWebSockets { get; set; }

        /// <summary>
        /// Enabled this to disable the WebSocket Heartbeat. If the Hearbeat is off and no traffic is flowing between a browser and the first-node, the browser will time out when the session expires
        /// </summary>
        public bool IsWSHBDisabled
        {
            get;
            set;

        }
        /// <summary>
        /// If set to False, the UPnP discovery system was disabled
        /// </summary>
        public bool IsUsingUPnP { get; set; }

        /// <summary>
        /// This defines the "StandardDeviceType" for UPnP. Default is "InternetGatewayDevice"
        /// </summary>
        public string UPnPDeviceType { get; set; }


        /// <summary>
        /// Sets the Scan Rate for Discovery. If set to zero, No periodic scanning will take place. Default is 60 seconds
        /// </summary>
        public int DISCOScanRate { get; set; }

        /// <summary>
        /// Sets the UPnP MX record for M-SEARCH broadcasts to a specific value. Should be between 0 and 5. Default is 3
        /// </summary>
        public int DISCOMX { get; set; }

        /// <summary>
        /// Limits the SSDP/UPnP M-SEARCH scans to the defined subnet. can be up to three of the 4 IP segments. i.e.: 10 or 10.1 or 192.168.1
        /// </summary>
        public string DISCOSubnet { get; set; }
        /// <summary>
        /// If set to true, the current node is a Cloud Service.
        /// </summary>
        public bool IsCloudService { get; set; }
        /// <summary>
        /// Is true if the current node is currently connected to the cloud
        /// </summary>
        public bool IsConnectedToCloud
        {
            get;
            set;

        }
        /// <summary>
        /// If set to true and the StorageService is alive, the User Information will be stored/retreived via TheStorageService
        /// </summary>
        public bool IsUserManagerInStorage
        {
            get;
            set;

        }
        public bool IsOutputCompressed { get; set; }
        /// <summary>
        /// For debugging purposes, this flag can be set to always force output on browsers to a specific platform
        /// </summary>
        public eWebPlatform ForceWebPlatform { get; set; }

        /// <summary>
        /// If true, the relay does not support isolation and will try to load all plugins into the host process
        /// </summary>
        public bool DisableIsolation { get; set; }
        /// <summary>
        /// Set this to true to disable the cloud temporarily
        /// </summary>
        public bool IsCloudDisabled { get; set; }
        /// <summary>
        /// If set, Mobile device will not autologin the last user even if there is a record of it
        /// </summary>
        public bool EnableAutoLogin { get; set; }
        /// <summary>
        /// If set, the cdeEngine will shutdown when it encounters no activated license
        /// </summary>
        public bool ShutdownOnLicenseFailure
        {
            get;
            set;

        }
        /// <summary>
        /// Set to true if the current C-DEngine Application Host (TheBaseApplication) is a Viewer (only ONE user logged in at any give time)
        /// </summary>
        public bool IsViewer
        {
            get;
            set;

        }

        //If set to true during startup, this node will try to find Cloud nodes using Bing Search
        public bool EnableBingScan { get; set; }
        /// <summary>
        /// True of the C-DEngine is running inside the Mono-Runtime
        /// </summary>
        public bool MonoDetected
        {
            get;
            set;

        }
        /// <summary>
        /// True if the C-Dengine is running on Mono in Linux (false if running in Mono on Windows)
        /// </summary>
        public bool MonoActive
        {
            get;
            set;

        }
        /// <summary>
        /// same as Mono detected
        /// </summary>
        public bool MonoRTDetected
        {
            get;
            set;

        }
        /// <summary>
        /// Same as MonoActive
        /// </summary>
        public bool MonoRTActive
        {
            get;
            set;

        }

        /// <summary>
        /// Set to true if the C-DEngine is using Windows8/Server 2012 WebSockets (http.sys)
        /// </summary>
        public bool IsWebSocket8Active
        {
            get;
            set;

        }


        /// <summary>
        /// Defines the time in seconds that triggers a Queue Priority Inversion. If a message is longer in the queue as the specified value in this parameter, the message will be sent next. If this value is zero, the QueuedSender does not perform Priority Inversion
        /// </summary>
        public int PrioInversionTime { get; set; }
        /// <summary>
        /// If set to true, packages/telegrams within a QDX will always be send in FIFO mode. If False, QDX priority smaller 3 will be sent as FILO
        /// </summary>
        public bool EnforceFifoQueue { get; set; }
        /// <summary>
        /// If true, the SmartQueue will not change any set priority
        /// </summary>
        public bool DisablePriorityInversion { get; set; }
        /// <summary>
        /// If true, the SmartQueue will chunk any message. ATTENTION: If a device cannot handle large messages, because it ran out of memory or the message is larger than the buffer a device can handle, the large telegram will be lost!
        /// </summary>
        public bool DisableChunking { get; set; }

        /// <summary>
        /// If set, the current node will route telegrams through that do have different ScopeIDs from the current node
        /// </summary>
        public bool AllowForeignScopeIDRouting
        {
            get;
            set;

        }
        /// <summary>
        /// Set this Guid if you want your device to have a predefined DeviceID. If no DeviceID is specified, the C-DEngine will create a random DeviceID and stores it at the very first startup. Any sequencial Startup will use this DeviceID.
        /// If UseRandomDeviceID is set, the C-DEngine will not store this ID.
        /// </summary>
        public Guid PresetDeviceID
        {
            get;
            set;

        }
        /// <summary>
        /// Defines the default portal page (see AddSmartPage). By default this should be "NMIPORTAL"
        /// </summary>
        public string PortalPage { get; set; }

        /// <summary>
        /// If this is set to true, the C-DEngine is not completely configured, yet. For example no ScopeID has been issued for the current node.
        /// This is not critical for the run-time of the C-DEngine and is user mainly by Application Configuration Plugins such as the Factory-Relay
        /// </summary>
        public bool RequiresConfiguration
        {
            get;
            set;

        }

        /// <summary>
        /// Sets the base Background color for the "Clean" screen
        /// </summary>
        public string BaseBackgroundColor { get; set; }
        /// <summary>
        /// Sets the base Foreground color for the Clean screen
        /// </summary>
        public string BaseForegroundColor { get; set; }

        /// <summary>
        /// If true, the C-DEngine will not cache any resources
        /// </summary>
        public bool DisableCache
        {
            get;
            set;

        }
        /// <summary>
        /// MaxAge of HTTP Cache in Seconds
        /// </summary>
        public int CacheMaxAge
        {
            get;
            set;

        }

        /// <summary>
        /// Timeout for Thing Events.
        /// </summary>
        public int EventTimeout { get; set; }
    }

    /// <summary>
    /// This is a clone of TheEngineStates of all plugins used for serialization
    /// </summary>
    public class TheEngineStateClone : TheMetaDataBase
    {
        public DateTimeOffset UpdateDate
        {
            get; set;
        }
        public DateTimeOffset RegisterDate
        {
            get; set;
        }
        public string LastMessage
        {
            get; set;
        }

        public bool IsDisabled
        {
            get; set;
        }

        public string ControlText
        {
            get; set;
        }

        public string IsoText
        {
            get; set;
        }

        public string DisableText
        {
            get; set;
        }

        /// <summary>
        /// True if the Engine has a live object
        /// </summary>
        public bool IsAlive
        {
            get; set;
        }

        public bool IsEngineReady
        {
            get; set;
        }


        /// <summary>
        /// Returns the current connection context to a ServiceNode (for !IsService)
        /// </summary>
        public string ServiceNodeText
        {
            get; set;
        }

        /// <summary>
        /// Returns a small status reply of the current Engine State
        /// </summary>
        public string QuickStatus
        {
            get; set;
        }
        /// <summary>
        /// Friendly Name of the Service
        /// </summary>
        public string FriendlyName { get; set; }
        /// <summary>
        /// Class Name of the Service (recommended is the FullName of the class that runs this service)
        /// </summary>
        public string ClassName { get; set; }

        public string EngineType { get; set; }
        /// <summary>
        /// Station URL of this service
        /// </summary>
        public string MyStationUrl { get; set; }
        /// <summary>
        /// Version of this Service
        /// </summary>
        public string Version { get; set; }
        /// <summary>
        /// New Version of an available Update the ISM service has found
        /// </summary>
        public double NewVersion { get; set; }
        /// <summary>
        /// Minimum version of the C-DEngine required by the plug-in
        /// </summary>
        public string CDEMinVersion { get; set; }

        /// <summary>
        /// Indicates that the plug-in requires an activated license
        /// </summary>
        public bool IsLicensed
        {
            get;
            set;
        }

        /// <summary>
        /// Guid of the Default Dashboard
        /// </summary>
        public string Dashboard { get; set; }

        /// <summary>
        /// Current StatusLevel of the Engine
        /// </summary>
        public int StatusLevel
        {
            get;
            set;
        }
        /// <summary>
        /// Contains the Guid of the Thing in the Engine with the highest StatusLevel
        /// </summary>
        public Guid HighStatusThing { get; set; }

        public bool IsConnected { get; set; }
        /// <summary>
        /// Engine ID
        /// </summary>
        public Guid EngineID { get; set; }
        /// <summary>
        /// Guid of the Home screen of this Engine
        /// </summary>
        public string EngineHomeScreen { get; set; }

        /// <summary>
        /// Amount of client-nodes connected to this Service
        /// </summary>
        public int ConnectedClientNodes { get; set; }
        /// <summary>
        /// Amount of Services connected to this node (In case the current node is a relay)
        /// </summary>
        public int ConnectedServiceNodes { get; set; }
        /// <summary>
        /// Amount of Services this node is connected to
        /// </summary>
        public int ConnectedToServices { get; set; }

        /// <summary>
        /// Current Index into the RedundancyURLs list of existing failover Channels
        /// </summary>
        public int RedundancyIndex { get; set; }

        /// <summary>
        /// A color representation of this Service for UX purposes
        /// </summary>
        public string EngineColor { get; set; }
        /// <summary>
        /// Is set to True if the ISM found an update for this Service
        /// </summary>
        public bool IsUpdateAvailable { get; set; }
        /// <summary>
        /// Is set to True if this Service is actively communicating. IsLiveEngine is FALSE if the plugin is suspended/unloaded
        /// </summary>
        public bool IsLiveEngine { get; set; }
        /// <summary>
        /// Is set to True if this service is just running in Simulation Mode
        /// </summary>
        public bool IsSimulated { get; set; }
        /// <summary>
        /// If an Engine is a Service it can provide data
        /// If an Engine is NOT a Services it can consume data (Mainly used in end devices such as Mobile Phones and Browsers)
        /// </summary>
        public bool IsService { get; set; }

        /// <summary>
        /// Is true if the plugin is running in an isolated host process
        /// </summary>
        public bool IsIsolated { get; set; }
        /// <summary>
        /// If set to True, this service is not doing anything except relaying information between other nodes
        /// In order to define a Mini (Relay) Service use the key "RelayOnly" in the App.Config/Web.Config and the string defined in the "ClassName"
        /// </summary>
        /// <seealso cref="M:nsCDEngine.Engines.IBaseEngine.SetIsMiniRelay(System.Boolean)">Setting
        /// MiniRelay Mode</seealso>
        public bool IsMiniRelay { get; set; }
        /// <summary>
        /// Returns true if the plugin-service is initialized.
        /// This will be set if the service calls "SetInitialized()" and cannot be changed
        /// </summary>
        /// <returns></returns>
        public bool IsInitialized
        {
            get;
            set;
        }

        /// <summary>
        /// If true, the Plugin will no longer participate in any communication - it is virtually unloaded and non-functioning
        /// </summary>
        public bool IsUnloaded
        {
            get;
            set;
        }
        /// <summary>
        /// Returns true if the plugin-service is trying to initialize
        /// Data-Consumers can call InitAndSubscribe to establish a direct connection to the data-provider.
        /// The Data-Provider will return a CDE_INITIALIZED message that tells the consumer that the service is initialized and ready to receive telegrams
        /// During this asynchronous process the data-consumers IsInitializing will be true. Once CDE_INITIALIZED was received, IsInitializing will be set to false.
        /// </summary>
        /// <returns></returns>
        /// <seealso cref="M:nsCDEngine.Engines.IBaseEngine.InitAndSubscribe(heCommunicationChannel pChannel)">Initializing a connection to a data provider
        /// </seealso>
        public bool IsInitializing { get; set; }

        /// <summary>
        /// if set to true, the Service has a custom stylesheet that needs to be injected into the browser
        /// </summary>
        public bool HasCustomCSS { get; set; }
        /// <summary>
        /// Set to True if this engine provides a JavaScript engine as well
        /// </summary>
        public bool HasJSEngine { get; set; }
        /// <summary>
        /// The Engine is currently Resetting channels
        /// </summary>
        public bool InReset { get; set; }

        /// <summary>
        /// True if the engine was started properly.
        /// </summary>
        public bool IsStarted { get; set; }
        /// <summary>
        /// A channel of an Engine is currently starting up
        /// </summary>
        public bool IsInChannelStartup { get; set; }

        /// <summary>
        /// An Engine is starting up
        /// </summary>
        public bool IsInEngineStartup { get; set; }

        /// <summary>
        /// 	<para>If set to true, the plugin can receive scoped messages ...but only
        /// if:</para>
        /// 	<list type="bullet">
        /// 		<item>
        /// 			<description>The Plugin-Service is a LiveService</description>
        /// 		</item>
        /// 		<item>
        /// 			<description>or The Plugin-Service is hosted on a Cloud-Node</description>
        /// 		</item>
        /// 		<item>
        /// 			<description>and if the Hosting Node is NOT scoped</description>
        /// 		</item>
        /// 	</list>
        /// </summary>
        public bool IsAllowedUnscopedProcessing { get; set; }

        /// <summary>
        /// If this parameter is set, a plugin is allowed to process foreign ScopeIDs
        /// This parameter ONLY works on RELAY nodes with "AllowForeignScopeIDRouting" set to true
        /// </summary>
        public bool IsAllowedForeignScopeProcessing { get; set; }
        /// <summary>
        /// If true, the Plugin-Service allows to receive FilePush Telegrams
        /// </summary>
        public bool IsAcceptingFilePush { get; set; }
        /// <summary>
        /// Is set to True when the service is about to stop
        /// </summary>
        public bool IsEngineStopping { get; set; }
        /// <summary>
        /// Is set to True when the service was Ready (connected to a Data Providing Service) before
        /// </summary>
        public bool WasEngineReadyBefore { get; set; }
        /// <summary>
        /// Shows the last Readiness State. Has to be used and set by the UX
        /// </summary>
        public bool LastEngineReadiness { get; set; }

        /// <summary>
        /// Date and Time of the last Initialization Success that was sent back to a requestion Service
        /// </summary>
        public DateTimeOffset LastSentInitialized { get; set; }

        /// <summary>
        /// Date and Time of the last Initialization that was sent to the data providing Service
        /// </summary>
        public DateTimeOffset LastSentInitialize { get; set; }


        /// <summary>
        /// Current Counter delaying the next try to CDE_INITIALIZE
        /// </summary>
        public int InitWaitCounter { get; set; }

        /// <summary>
        /// Last time Subscribe was sent
        /// </summary>
        public DateTimeOffset LastSentSubscribe { get; set; }

        /// <summary>
        /// List of DeviceID Guids of clients connected to this service
        /// </summary>
        public List<Guid> ConnectedClientsList { get; set; }
        /// <summary>
        /// List of DeviceID Guis of all nodes connected to this service (Clients and Services if a relay node)
        /// </summary>
        public List<Guid> ConnectedNodesList { get; set; }

        /// <summary>
        /// Amount of credits for usage of this Service accumulated so far
        /// </summary>
        public long CommunicationCosts { get; set; }

        /// <summary>
        /// The NodeID of the node providing the data to !IsServices (i.e Storage Client Service)
        /// </summary>
        public Guid ServiceNode { get; set; }

        /// <summary>
        /// List of Cookies a Plugin can add to this Service Information
        /// </summary>
        public List<string> CustomValues { get; set; }
    }

    /// <summary>
    /// This is a clone of TheChannelInfo used for serialization
    /// </summary>
    public class TheChannelInfoClone : TheDataBase
    {
        /// <summary>
        /// Url of the Node this channel is pointing to
        /// </summary>
        public string TargetUrl { get; set; }
        /// <summary>
        /// Sender Type of the node this channel is pointing to
        /// </summary>
        public cdeSenderType SenderType { get; set; }

        /// <summary>
        /// Timestamp when this Channel was created
        /// </summary>
        public DateTimeOffset CreationTime { get; set; }
        /// <summary>
        /// Timestamp of the last Channel activity
        /// </summary>
        public DateTimeOffset LastInsertTime { get; set; }
        /// <summary>
        /// Number of live References
        /// </summary>
        public int References { get; set; }

        /// <summary>
        /// True if this channels points at a WebSocket Node
        /// </summary>
        public bool IsWebSocket { get; set; }

        /// <summary>
        /// Returns a hash of the scopeid for diagnostics purposes
        /// </summary>
        public string ScopeIDHash { get; set; }
        /// <summary>
        /// Tells if this Channel is for a Device (Phone, Device, HTML5/JS)
        /// </summary>
        public bool IsDeviceType
        {
            get; set;
        }
    }

    /// <summary>
    /// Clone of TheNodeInfo class used for serialization
    /// </summary>
    public class TheNodeInfoClone : TheDataBase
    {
        /// <summary>
        /// All Node Parameter
        /// </summary>
        public TheServiceHostInfoClone MyServiceInfo { get; set; }
        /// <summary>
        /// DeviceID of the Node (Same as in MyServiceInfo.MyDeviceInfo.DeviceID)
        /// </summary>
        public Guid MyNodeID { get; set; }
        /// <summary>
        /// State of all services running on that Node
        /// </summary>
        public List<TheEngineStateClone> MyServices { get; set; }
        /// <summary>
        /// Last HTML-formated Log
        /// </summary>
        public string LastHTMLLog { get; set; }
        /// <summary>
        /// Channels by Topic
        /// </summary>
        public Dictionary<string, List<Guid>> ChannelsByTopic { get; set; }
        /// <summary>
        /// Active Channels
        /// </summary>
        public List<TheChannelInfoClone> Channels
        {
            get;
            set;
        }

        /// <summary>
        /// Current active License
        /// </summary>
        public string ActiveLicense { get; set; }
        /// <summary>
        /// Instantiates TheNodeInfo
        /// </summary>
        public TheNodeInfoClone()
        {
            MyServices = new List<TheEngineStateClone>();
        }
    }
    #endregion

}
