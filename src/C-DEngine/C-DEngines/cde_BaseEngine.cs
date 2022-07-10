// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Reflection;

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using System.Collections.Generic;
using System.Globalization;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using nsCDEngine.Activation;
using System.Threading.Tasks;
using System.Linq;
// ReSharper disable DelegateSubtraction
// ReSharper disable UseNullPropagation
// ReSharper disable MergeSequentialChecks
// ReSharper disable ConvertIfStatementToNullCoalescingExpression

//ERROR Range: 400-409

namespace nsCDEngine.Engines
{
    /// <summary>
    /// TheBaseEngine is a class common to all Buidin- and Plugin-Services of the C-DEngine
    /// It contains many important methods common to all services
    /// This class will be hidden in the future. Please always use the IBaseEngine interface to access public members of this class
    /// </summary>
    /// <seealso cref="M:nsCDEngine.Engines.IBaseEngine.RegisterEngineStarted(System.Action)">Engine
    /// started event</seealso>
    public class TheBaseEngine : TheMetaDataBase, IBaseEngine
    {
        /// <summary>
        /// The constructor initializes important internal variables and lists
        /// </summary>
        public TheBaseEngine()
        {
            mEngineState = new TheEngineState();
            mPluginInfo = new ThePluginInfo();
            AddCapability(eThingCaps.BaseEngine);
            mPluginInfo.Platform = TheBaseAssets.MyServiceHostInfo.cdePlatform;
        }
        internal System.Diagnostics.Process ISOLater = null;
        internal void ISOLater_Exited(object sender, EventArgs e)
        {
            StopEngine();
        }

        internal void KillISOLater()
        {
            if (ISOLater!=null && !ISOLater.HasExited)
            {
                ISOLater.Exited -= ISOLater_Exited;
                ISOLater.Kill();
            }
        }

        /// <summary>
        /// Returns this BaseEngine
        /// </summary>
        /// <returns></returns>
        public TheBaseEngine GetBaseEngine()
        {
            return this;
        }


        #region Interface Methods
        /// <summary>
        /// Gets or sets the last message of an engine
        /// </summary>
        public string LastMessage
        {
            get { return EngineState?.LastMessage; }
            set
            {
                if (EngineState != null)
                    EngineState.LastMessage = value;
            }
        }

        /// <summary>
        /// Returns the ISOlater (Isolation Manager)
        /// </summary>
        /// <returns></returns>
        public object GetISOLater()
        { return ISOLater; }
        private ICDEPlugin tAssPlug;
        /// <summary>
        /// Returns the the ICDEPLugin of this TheBaseEngine
        /// </summary>
        public ICDEPlugin AssociatedPlugin
        {
            get { return tAssPlug; }
            set
            {
                tAssPlug = value;
                MyBaseThing = value as ICDEThing;
                try
                {
                    var tPluginPlatform = TheCommonUtils.GetAssemblyPlatform(value.GetType().Assembly, false, out string diagInfo);
                    if (tPluginPlatform != cdePlatform.NOTSET)
                    {
                        mPluginInfo.Platform = tPluginPlatform;
                    }
                }
                catch { }
            }
        }
        internal ICDEThing MyBaseThing;


        /// <summary>
        /// Loads a Resource from the Relay. Load Order:
        /// This method is adding a /L{LCID} to the resource if the requesting User has set a different LCID then zero (Browser Default)
        /// 1 - ClientBin Folder under Executable Folder
        /// 2 - Embedded Resources of Plug-in/Engine
        /// 3 - ClientBin Folder with no /L{LCID}
        /// 4 - Embedded Resources of Plug-in/Engine without L{LCID}
        /// The method also adds sets the ResponseMimeType on TheRequestData for js, css, html and pngs
        /// If the requested resource is an HTML File and it cannot be located, the method returns a "Resource not found" error in the ResponseBuffer
        /// AllowStatePush will be set to true and StatusCode will be set to 200.
        /// </summary>
        /// <param name="pRequest"></param>
        /// <returns>True if the resource was found</returns>
        public bool GetPluginResource(TheRequestData pRequest)
        {
            if (pRequest == null || TheCommonUtils.IsNullOrWhiteSpace(pRequest.cdeRealPage) || pRequest.cdeRealPage.Length < 2) return false;
            string tResourceName = pRequest.cdeRealPage.Substring(1);
            if (tResourceName.IndexOf('?') > -1)
                tResourceName = tResourceName.Substring(0, tResourceName.IndexOf('?'));
            Assembly ass = Assembly.GetAssembly(EngineState.EngineType);
            string tFinalGetStr = tResourceName;
            if (pRequest.SessionState != null && pRequest.SessionState.LCID > 0)
                tFinalGetStr = string.Format("L{0}\\{1}", pRequest.SessionState.LCID, tResourceName);
            byte[] astream = TheCommonUtils.GetSystemResource(ass, tFinalGetStr);
            if (astream == null && tFinalGetStr.Length!=tResourceName.Length)
                astream=TheCommonUtils.GetSystemResource(ass, tResourceName);

            if (pRequest.cdeRealPage.EndsWith("js", StringComparison.OrdinalIgnoreCase))
                pRequest.ResponseMimeType = "application/javascript";
            else
            {
                if (pRequest.cdeRealPage.EndsWith("css", StringComparison.OrdinalIgnoreCase))
                    pRequest.ResponseMimeType = "text/css";
                else
                {
                    if (pRequest.cdeRealPage.EndsWith("html", StringComparison.OrdinalIgnoreCase))
                    {
                        pRequest.ResponseMimeType = "text/html";
                        pRequest.ResponseBuffer = TheCommonUtils.CUTF8String2Array("<h1>Resource not Found</h1>");
                    }
                    else
                        pRequest.ResponseMimeType = "image/png";
                }
            }
            pRequest.AllowStatePush = true;
            pRequest.StatusCode = 200;
            if (astream != null)
            {
                pRequest.ResponseBuffer = astream;
                return true;
            }
            return false;
        }


        /// <summary>
        /// Retrieves an embedded Resource from the plugin by name.
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public byte [] GetPluginResource(string pName)
        {
            if (EngineState?.EngineType == null)
            {
                return null;
            }
            string[] pParts = pName.Split('/');
            if (pParts.Length<3)
                pName = pParts[pParts.Length - 1];
            if (pName.IndexOf('?') > -1)
                pName = pName.Substring(0, pName.IndexOf('?'));
            return TheCommonUtils.GetSystemResource(Assembly.GetAssembly(EngineState.EngineType), pName);
        }
        /// <summary>
        /// Fires the eventMessageProcess to all listeners
        /// </summary>
        /// <param name="pMessage">TSM to be sent to listeners</param>
        public void MessageProcessed(TSM pMessage)
        {
            if (MyBaseThing != null)
                MyBaseThing.GetBaseThing().FireEvent(eEngineEvents.MessageProcessed, MyBaseThing, pMessage, false);
        }

        /// <summary>
        /// Fires the "CostingWasUpdated" event to add new credits to a TSM
        /// </summary>
        /// <param name="pMessage"></param>
        public void UpdateCosting(TSM pMessage)
        {
            if (MyBaseThing != null && TheBaseAssets.MyServiceHostInfo.EnableCosting)
                MyBaseThing.GetBaseThing().FireEvent(eEngineEvents.CostingWasUpdated,MyBaseThing, pMessage, false);
        }

        private readonly ThePluginInfo mPluginInfo;
        private TheEngineState mEngineState;
        /// <summary>
        /// Gets the Engine State
        /// </summary>
        public TheEngineState EngineState { get { return mEngineState;} internal set { mEngineState=value;} }
        /// <summary>
        /// Returns the current Engine State
        /// </summary>
        /// <returns></returns>
        public TheEngineState GetEngineState() { return mEngineState; }

        /// <summary>
        /// Updates all list-properties of Service State with the current values
        /// </summary>
        /// <param name="pScramScopeID"></param>
        public void UpdateEngineState(string pScramScopeID)
        {
            TheQueuedSenderRegistry.UpdateServiceState(ref mEngineState);
        }

        /// <summary>
        /// Sets TheBaseEngine as a Service (will be Added to the StationRoles)
        /// </summary>
        /// <param name="pIsService"></param>
        public void SetEngineService(bool pIsService)
        {
            EngineState.IsService = pIsService;
        }

        /// <summary>
        /// Sets the Isolation flags
        /// </summary>
        /// <param name="AllowIsolation">If true, this plugin is allowed to be isolated</param>
        /// <param name="AllowNodeHopp">If True, this plugin is allowed to hopp to other nodes</param>
        public void SetIsolationFlags(bool AllowIsolation,bool AllowNodeHopp=false)
        {
            EngineState.IsAllowedForIsolation = AllowIsolation;
            if (AllowIsolation)
                EngineState.IsAllowedToNodeHopp = AllowNodeHopp;
        }

        /// <summary>
        /// RETIRED IN V4: All Engines now NOT MultiChannel
        /// </summary>
        /// <param name="mIsMulti"></param>
        [Obsolete("Retired in V4 - will be removed in V5")]
        public void SetMultiChannel(bool mIsMulti) { EngineState.IsMultiChannel = false;  /*mIsMulti;*/ }

        /// <summary>
        /// Defines TheBaseEngine as a Mini Relay (Pub/Sub Relaying Only - no Application Code)
        /// </summary>
        /// <param name="pIsRelay"></param>
        public void SetIsMiniRelay(bool pIsRelay) { EngineState.IsMiniRelay = pIsRelay; }


        /// <summary>
        /// Sets the Statuslevel of TheBaseEngine to a new level
        /// If you use -1, the StatusLevel is calculated from all the Things TheBaseEngine is managing
        /// </summary>
        /// <param name="pLevel"></param>
        public void SetStatusLevel(int pLevel)
        {
            if (MyBaseThing != null && MyBaseThing.GetBaseThing() != null)
            {
                int HighestLevel = 0;
                mEngineState.HighStatusThing = GetDashboardGuid(); // MyBaseThing.GetBaseThing().cdeMID;
                if (pLevel >= 0)
                {
                    HighestLevel = pLevel;
                }
                else
                {
                    //int CombinedCode = 0;
                    List<TheThing> tDeviceList = TheThingRegistry.GetThingsOfEngine(GetEngineName());
                    foreach (TheThing tDevice in tDeviceList)
                    {
                        if (tDevice.HasLiveObject && tDevice != MyBaseThing.GetBaseThing())
                        {
                            //if (tDevice.StatusLevel > 1)
                            //    CombinedCode++;
                            if (tDevice.StatusLevel > HighestLevel)
                            {
                                HighestLevel = tDevice.StatusLevel;
                                if (HighestLevel > 1)
                                    mEngineState.HighStatusThing = tDevice.cdeMID;
                            }
                        }
                    }
                }
                if (MyBaseThing.GetBaseThing().StatusLevel != HighestLevel)
                    MyBaseThing.GetBaseThing().StatusLevel = HighestLevel;
                if (mEngineState.StatusLevel != HighestLevel)
                {
                    mEngineState.StatusLevel = HighestLevel;
                }
                TheBaseAssets.MyServiceHostInfo.StatusLevel = 0;
            }
        }

        /// <summary>
        /// Is set when TheBaseEngine is initializing
        /// </summary>
        public void SetIsInitializing()
        {
            //if (EngineState.IsMultiChannel) return;
            EngineState.IsInitializing = true;
            EngineState.InitWaitCounter = 0;
        }
        private void ResetInitialization()
        {
            EngineState.IsInitializing = false;
            EngineState.InitWaitCounter = 0;
        }

        /// <summary>
        /// Call this Method to tell the C-DEngine and "EngineInitialized" registrars that the service has been successfully initialized
        /// The Event is fired synchronous and fires BEFORE the EngineState.IsInitialized is set to true. This allows event recceivers to check if the Engine has been initialized before
        /// </summary>
        /// <param name="pMessage"></param>
        public void SetInitialized(TSM pMessage)
        {
            ResetInitialization();
            if (MyBaseThing != null)
                MyBaseThing.GetBaseThing().FireEvent(eEngineEvents.EngineInitialized, MyBaseThing, null, false);
            EngineState.IsInitialized = true;
            SetEngineReadiness(true, null);
        }

        /// <summary>
        /// RETIRED IN V4: returns always FALSE
        /// </summary>
        /// <returns></returns>
        public bool HasChannels()
        {
            return false;
        }

        /// <summary>
        /// Returns the Version of TheBaseEngine
        /// </summary>
        /// <returns></returns>
        public string GetVersion()
        {
            return EngineState.Version;
        }

        /// <summary>
        /// Sets the Version of TheBaseEngine
        /// </summary>
        /// <param name="pVersion"></param>
        public void SetVersion(double pVersion)
        {
            EngineState.Version = pVersion.ToString(CultureInfo.InvariantCulture);
            mPluginInfo.CurrentVersion = pVersion;
            AddManifestFiles(null);
        }

        /// <summary>
        /// If a new Version was found it can be set here
        /// </summary>
        /// <param name="pVersion"></param>
        public void SetNewVersion(double pVersion)
        {
            EngineState.NewVersion = pVersion;
        }

        /// <summary>
        /// Sets the minimal required version of the C-DEngine for TheBaseEngine
        /// </summary>
        /// <param name="pVersion"></param>
        public void SetCDEMinVersion(double pVersion)
        {
            EngineState.CDEMinVersion = pVersion.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the minimal required Version of the C-DEngine
        /// </summary>
        /// <returns></returns>
        public string GetCDEMinVersion()
        {
            return EngineState.CDEMinVersion;
        }

        internal string[] LicenseAuthorities;

        /// <summary>
        /// Plug-in call this from their InitEngineAssets() method to indicate that they require an activated license
        /// </summary>
        /// <param name="createDefaultLicense"></param>
        /// <param name="licenseAuthorities">Indicates which additional signatures are required on a license file.</param>
        public void SetIsLicensed(bool createDefaultLicense = true, string[] licenseAuthorities = null)
        {
            if (!EngineState.IsInitialized)
            {
                EngineState.IsLicensed = true;
                LicenseAuthorities = licenseAuthorities;
                if (createDefaultLicense)
                {
                    TheBaseAssets.MyActivationManager.CreateDefaultLicense(GetEngineID());
                }
            }
        }

        /// <summary>
        /// Sets the main Dashboard of TheBaseEngine
        /// </summary>
        /// <param name="pDash"></param>
        public void SetDashboard(string pDash)
        {
            EngineState.Dashboard = pDash;
        }

        /// <summary>
        /// Gets the main Dashboard of TheBaseEngine
        /// </summary>
        /// <returns></returns>
        public string GetDashboard()
        {
            return EngineState.Dashboard;
        }

        /// <summary>
        /// Gest the Guid of the Dashboard of TheBaseEngine
        /// </summary>
        /// <returns></returns>
        public Guid GetDashboardGuid()
        {
            string[] tGuidParts = TheCommonUtils.cdeSplit(EngineState.Dashboard, ";:;", false, false);
            return TheCommonUtils.CGuid(tGuidParts[0]);
        }

        /// <summary>
        /// Sets the Engine and ClassName of TheBaseEngine.. Recommended is to use "GetType().FullName"
        /// Subscriptions for TheBaseEngine will use this name
        /// </summary>
        /// <param name="pName"></param>
        public void SetEngineName(string pName)
        {
            EngineState.ClassName = pName;
            mPluginInfo.ServiceName = pName;

        }

        /// <summary>
        /// Sets the .NET Type of TheBase Engine. Use "GetType()"
        /// </summary>
        /// <param name="pType"></param>
        public void SetEngineType(Type pType)
        {
            EngineState.EngineType = pType;
            TheBaseAssets.MyServiceTypes.Add(pType);
        }

        /// <summary>
        /// Declares a list of DeviceTypes for this plugins
        /// </summary>
        /// <param name="pDeviceTypeInfo"></param>
        public void SetDeviceTypes(List<TheDeviceTypeInfo> pDeviceTypeInfo)
        {
            mPluginInfo.DeviceTypes = pDeviceTypeInfo;
        }

        /// <summary>
        /// Returns a list of TheDeviceTypeInfos of this plugin
        /// </summary>
        /// <returns></returns>
        public List<TheDeviceTypeInfo> GetDeviceTypes()
        {
            return mPluginInfo.DeviceTypes;
        }

        /// <summary>
        /// Gets the Name of TheBaseEngine - used for the ENG parameter TSM and corresponding subscriptions
        /// </summary>
        /// <returns></returns>
        public string GetEngineName()
        {
            return EngineState.ClassName;
        }

        /// <summary>
        /// Gets the Friendly Name of TheBaseEngine
        /// </summary>
        /// <returns></returns>
        public string GetFriendlyName()
        {
            if (string.IsNullOrEmpty(EngineState.FriendlyName)) return EngineState.ClassName;
            return EngineState.FriendlyName;
        }

        /// <summary>
        /// Sets the Friendly Name of TheBaseEngine
        /// </summary>
        /// <param name="pName"></param>
        public void SetFriendlyName(string pName)
        {
            EngineState.FriendlyName = pName;
            mPluginInfo.ServiceDescription = pName;
        }

        /// <summary>
        /// Sets the EngineID Of TheBaseEngine - this is required by the system.
        /// By default, the Engine will also use this Guid for its main Dashboard ID
        /// </summary>
        /// <param name="pGuid"></param>
        public void SetEngineID(Guid pGuid)
        {
            EngineState.EngineID = pGuid;
            mPluginInfo.cdeMID = pGuid;
            EngineState.Dashboard = pGuid.ToString();
        }

        /// <summary>
        /// Returns the Plugin GUID of TheBaseEngine
        /// </summary>
        /// <returns></returns>
        public Guid GetEngineID()
        {
            return EngineState.EngineID;
        }

        /// <summary>
        /// Sets the Information of the Plugin
        /// </summary>
        /// <param name="pLongDescription"></param>
        /// <param name="pPrice"></param>
        /// <param name="pHomeUrl"></param>
        /// <param name="pIconUrl"></param>
        /// <param name="pDeveloper"></param>
        /// <param name="pDeveloperUrl"></param>
        /// <param name="pCategories"></param>
        /// <param name="Copyrights"></param>
        public void SetPluginInfo(string pLongDescription, double pPrice,string pHomeUrl,string pIconUrl,string pDeveloper,string pDeveloperUrl,List<string> pCategories,string Copyrights=null)
        {
            mPluginInfo.LongDescription = pLongDescription;
            mPluginInfo.Price = pPrice;
            mPluginInfo.HomeUrl = pHomeUrl;
            mPluginInfo.IconUrl = pIconUrl;
            mPluginInfo.Developer = pDeveloper;
            if (string.IsNullOrEmpty(Copyrights))
                mPluginInfo.Copyrights = cdeCTIM.Year + " " + pDeveloper;
            mPluginInfo.DeveloperUrl = pDeveloperUrl;
            mPluginInfo.Categories = pCategories;
        }

        /// <summary>
        /// Returns ThePluginInfo of TheBaseEngine
        /// </summary>
        /// <returns></returns>
        public ThePluginInfo GetPluginInfo()
        {
            return mPluginInfo;
        }

        /// <summary>
        /// Adds a capability to the Engine
        /// V4: You can send Discovery Messages across a mesh to find nodes with certain Plugins and capabilities
        /// </summary>
        /// <param name="pCapa"></param>
        public void AddCapability(eThingCaps pCapa)
        {
            if (mPluginInfo.Capabilities == null)
                mPluginInfo.Capabilities = new List<eThingCaps>();
            if (!mPluginInfo.Capabilities.Contains(pCapa))
                mPluginInfo.Capabilities.Add(pCapa);
        }

        /// <summary>
        /// TheBaseEngine can be asked if a certain Capability is supported by the it
        /// </summary>
        /// <param name="pCapa"></param>
        /// <returns></returns>
        public bool HasCapability(eThingCaps pCapa)
        {
            if (mPluginInfo.Capabilities == null || mPluginInfo.Capabilities.Count == 0) return false;
            return mPluginInfo.Capabilities.Contains(pCapa);
        }

        /// <summary>
        /// Returns the lowest capability
        /// </summary>
        /// <returns></returns>
        internal int GetLowestCapability()
        {
            if (mPluginInfo.Capabilities == null || mPluginInfo.Capabilities.Count == 0) return 9999;
            return (int)mPluginInfo.Capabilities.OrderBy(s => s).FirstOrDefault();
        }

        /// <summary>
        /// Adds a list of all required DLLs and other files that need to be present for the plugin to work
        /// The Plugin-Store will combine all the files when it creates the installer package
        /// </summary>
        /// <param name="pList"></param>
        public void AddManifestFiles(List<string> pList)
        {
            if (mPluginInfo != null)
            {
                if (mPluginInfo.FilesManifest == null)
                    mPluginInfo.FilesManifest = new List<string>();
                if (GetEngineState() == null || GetEngineState().IsMiniRelay || GetEngineState().EngineType==null) return;
                Assembly a = Assembly.GetAssembly(GetEngineState().EngineType);
                if (!mPluginInfo.FilesManifest.Contains(a.ManifestModule.Name))
                    mPluginInfo.FilesManifest.Add(a.ManifestModule.Name);
                if (pList != null)
                    mPluginInfo.FilesManifest.AddRange(pList);
            }
        }

        /// <summary>
        /// Adds a list of supported platforms
        /// </summary>
        /// <param name="pPlatformList">List of platforms that are supported by this plugin</param>
        public void AddPlatforms(List<cdePlatform> pPlatformList)
        {
            if (mPluginInfo != null && pPlatformList != null)
            {
                if (mPluginInfo.AllPlatforms == null)
                    mPluginInfo.AllPlatforms = new List<cdePlatform>();
                if (GetEngineState() == null || GetEngineState().IsMiniRelay || GetEngineState().EngineType == null) return;
                if (pPlatformList != null)
                    mPluginInfo.AllPlatforms.AddRange(pPlatformList);
            }
        }


        /// <summary>
        /// Returns the Service Node ID if set. This only applies to Non-Service Plugins (i.e. StorageService Client)
        /// </summary>
        /// <returns></returns>
        public Guid GetFirstNode()
        {
            if (EngineState == null) return Guid.Empty;
            return EngineState.ServiceNode;
        }

        /// <summary>
        /// Returns the cost of the Engine Communication in C-Credits
        /// </summary>
        /// <returns></returns>
        public long GetCommunicationCosts()
        {
            return EngineState.CommunicationCosts;
        }

        /// <summary>
        /// Returns MyBaseThing of TheBaseEngine
        /// </summary>
        /// <returns></returns>
        public TheThing GetBaseThing()
        {
            if (MyBaseThing != null)
                return MyBaseThing.GetBaseThing();
            return null;
        }

        /// <summary>
        /// Return the ICDEThing interface of TheBaseThing
        /// </summary>
        /// <returns></returns>
        public ICDEThing GetThingInterface()
        {
            return MyBaseThing;
        }

        /// <summary>
        /// Register a callback with an event of TheBaseEngine
        /// </summary>
        /// <param name="pEventName"></param>
        /// <param name="pCallback"></param>
        /// <returns></returns>
        public Action<ICDEThing, object> RegisterEvent(string pEventName, Action<ICDEThing, object> pCallback)
        {
            if (MyBaseThing != null)
            {
                MyBaseThing.RegisterEvent(pEventName, pCallback);
                return pCallback;
            }
            return null;
        }

        /// <summary>
        /// Unregisters the call back with an event. If the callback is null, all callbacks of the event will be removed
        /// </summary>
        /// <param name="pEventName"></param>
        /// <param name="pCallback"></param>
        public void UnregisterEvent(string pEventName, Action<ICDEThing, object> pCallback)
        {
            if (MyBaseThing != null)
                MyBaseThing.UnregisterEvent(pEventName, pCallback);
        }

        /// <summary>
        /// Fires an event with the given name
        /// </summary>
        /// <param name="pEventName"></param>
        /// <param name="pPara"></param>
        /// <param name="FireAsync"></param>
        public void FireEvent(string pEventName,object pPara,bool FireAsync)
        {
            if (MyBaseThing != null)
                MyBaseThing.FireEvent(pEventName, MyBaseThing, pPara, FireAsync);
        }

        /// <summary>
        /// Registers a JavaScript Engine with the HttpService.
        /// The JavaScript File must have the same name as returned by "GetEngineName()"
        /// </summary>
        /// <param name="sinkInterceptHttp"></param>
        public void RegisterJSEngine(Action<TheRequestData> sinkInterceptHttp)
        {
            if (sinkInterceptHttp == null)
                sinkInterceptHttp =sinkReturnEngineResource;
            GetEngineState().HasJSEngine = true;
            TheCommCore.MyHttpService.RegisterHttpInterceptorB4(string.Format("{0}.js",GetEngineName()), sinkInterceptHttp);
        }

        void sinkReturnEngineResource(TheRequestData pReq)
        {
            GetPluginResource(pReq);
        }

        /// <summary>
        /// Registers a CSS File with the Engine
        /// </summary>
        /// <param name="cssDarkPath"></param>
        /// <param name="cssLitePath"></param>
        /// <param name="sinkInterceptHttp"></param>
        public void RegisterCSS(string cssDarkPath, string cssLitePath, Action<TheRequestData> sinkInterceptHttp)
        {
            if (!TheBaseAssets.MasterSwitch) return;
            if (sinkInterceptHttp == null)
                sinkInterceptHttp = sinkReturnEngineResource;
            GetEngineState().HasCustomCSS = true;
            GetEngineState().CSS = cssDarkPath;
            TheCommCore.MyHttpService?.RegisterHttpInterceptorB4(cssDarkPath, sinkInterceptHttp);
            if (!string.IsNullOrEmpty(cssLitePath))
            {
                GetEngineState().CSS += ";" + cssLitePath;
                TheCommCore.MyHttpService?.RegisterHttpInterceptorB4(cssLitePath, sinkInterceptHttp);
            }
        }
#endregion

        internal static bool IsInternalEngine(string pName)
        {
            if (string.IsNullOrEmpty(pName)) return false;
            return (pName.Equals(eEngineName.ContentService) || pName.Equals(eEngineName.NMIService) || pName.Equals(eEngineName.ThingService));
        }

        /// <summary>
        /// Returns a friendly view of the current Engine State
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{GetEngineName()} (V{GetVersion()}{(EngineState.ServiceNodeText=="THIS-NODE"?"": " "+EngineState.ServiceNodeText)}{(EngineState.IsEngineReady?" IsReady":"")}){(EngineState.IsSimulated?" SIMULATED":"")}"; // {(EngineState.IsLiveEngine?" IsLive":"")}
        }

        /// <summary>
        /// Call this method to set the station readiness. It will be called by the system automatically once a channel is up and running
        /// </summary>
        /// <param name="IsReady">Tru= station is ready; false it is not ready</param>
        /// <param name="pOriginator">RETIRED IN V4. Possible Future Use</param>
        public void SetEngineReadiness(bool IsReady, TheChannelInfo pOriginator)
        {
            if (IsReady == EngineState.IsEngineReady) return;
            ResetInitialization();
            EngineState.IsEngineReady = IsReady;
            if (MyBaseThing != null)
                MyBaseThing.GetBaseThing().FireEvent(eEngineEvents.EngineIsReady,MyBaseThing, GetEngineState().IsEngineReady ? GetEngineName() : null, true);
            if (IsReady)
                EngineState.LastMessage = "Service is Ready!";
        }

        /// <summary>
        /// This is the main start routine for the engine.
        /// Depending on pURLS and the way the Engine is configured a channel is opened to the first URL in the pURLs
        /// </summary>
        /// <param name="pChannelInfo">A Channel Definition including the target URL</param>
        /// <returns></returns>
        public bool StartEngine(TheChannelInfo pChannelInfo)
        {
            //V4: If Enging is not a service the Engine will not Load or get any Messages - its bascially unloaded
            if (EngineState.IsUnloaded)
                return false;

            if (pChannelInfo == null)
                pChannelInfo = TheBaseAssets.LocalHostQSender.MyTargetNodeChannel;

            EngineState.IsLiveEngine = true; //Engine is started and alive

            if (this.AssociatedPlugin is ICDENMIPlugin && TheCDEngines.MyNMIService != null)
            {
                TheCDEngines.MyNMIService.MyRenderEngine = this.AssociatedPlugin as ICDENMIPlugin;
            }

            //Check if Engine is simulated
            if (EngineState.IsSimulated || TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false).Equals("SIMULATION") || pChannelInfo.SenderType == cdeSenderType.CDE_SIMULATION)
            {
                EngineState.IsSimulated = true;
                EngineState.LastMessage = "Simulation Active";
                return true;
            }
            else
                EngineState.IsSimulated = false;

            TheQueuedSender tQS = TheQueuedSenderRegistry.GetSenderByGuid(pChannelInfo.cdeMID);
            if (tQS != null)
                tQS.Subscribe(TheBaseAssets.MyScopeManager.AddScopeID(GetEngineName()));
            else
            {
                tQS = new TheQueuedSender();
                if (!tQS.StartSender(pChannelInfo, null, false)) // Subs must be empty here
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4171, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("TheCDEngines", $"Failed to create QSender for {pChannelInfo} UPnP Device", eMsgLevel.l2_Warning));
                    return false;
                }
            }
            if (string.IsNullOrEmpty(EngineState.LastMessage))
                EngineState.LastMessage = "Service started";
            return true;
        }

        /// <summary>
        /// Stops all channels and than the engine
        /// Removes all channels from the list - should not be used to stop a particular channel
        /// </summary>
        public void StopEngine()
        {
            if (EngineState.IsEngineStopping || IsInternalEngine(GetEngineName())) return;
            EngineState.IsEngineStopping = true;
            EngineState.IsUnloaded = true;
            ResetInitialization();
            if (EngineState.IsIsolated)
                KillISOLater();
            try
            {
                if (MyBaseThing != null)
                {
                    MyBaseThing.GetBaseThing().FireEvent(eEngineEvents.EngineHasStopped, MyBaseThing, null, false);
                    MyBaseThing.GetBaseThing().FireEvent(eEngineEvents.ShutdownEvent, MyBaseThing, null, false);
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(400, new TSM(GetEngineName(), string.Format("Error in Plugin-Service:{0} StopEngine", GetEngineName()), eMsgLevel.l1_Error, e.ToString()));
            }

            EngineState.IsLiveEngine = false;
            //EngineState = new TheEngineState { LastMessage = "Service was Shutdown!" }; //TODO: Review...not sure why this has to be reset so hard
            //MyBaseThing.GetBaseThing().SetIThingObject(null);
            EngineState.LastMessage = "Service was Shutdown!";
            EngineState.IsStarted = false;
            EngineState.StatusLevel = 0;
            EngineState.IsEngineStopping = false;
        }
#region Licensing Related Functions
        internal bool CheckEngineLicense()
        {
            if (!EngineState.IsLicensed)
            {
                return true;
            }
            return TheBaseAssets.MyActivationManager.CheckLicense(GetEngineID(), eKnownDeviceTypes.IBaseEngine, LicenseAuthorities, 0, false);
        }

        internal bool CheckAndAcquireLicense(TheThing tThing)
        {
            if (!EngineState.IsLicensed)
            {
                return true;
            }
            return TheBaseAssets.MyActivationManager.CheckLicense(GetEngineID(), tThing.DeviceType, LicenseAuthorities, 1, true);
        }

        internal bool ReleaseLicense(TheThing tThing)
        {
            if (!EngineState.IsLicensed)
            {
                return true;
            }
            return TheBaseAssets.MyActivationManager.ReleaseLicense(GetEngineID(), tThing.DeviceType);
        }
#endregion


#region Communication Related Methods
        /// <summary>
        /// Publishes a TSM to the Active Channel
        /// </summary>
        /// <param name="pMessage"></param>
        /// <param name="pLocalCallback"></param>
        /// <returns></returns>
        public bool PublishToChannels(TSM pMessage, Action<TSM> pLocalCallback)
        {
            if (EngineState.ServiceNode==Guid.Empty)
                TheCommCore.PublishCentral(GetEngineName(), pMessage, true, pLocalCallback);
            else
                TheCommCore.PublishCentral("CDE_SYSTEMWIDE;" + EngineState.ServiceNode, pMessage, true, pLocalCallback);
            return true;
        }

        /// <summary>
        /// Sends CDE_INITIALIZE to the Service Node. Only for "Client" Engines
        /// </summary>
        public void InitAndSubscribe()
        {
            if (EngineState == null || EngineState.ServiceNode == Guid.Empty || EngineState.IsService) return;
            EngineState.LastSentInitialize = DateTimeOffset.Now;

            TSM tTSM = new TSM(GetEngineName(), "CDE_INITIALIZE", TheBaseAssets.MyScopeManager.AddScopeID(GetEngineName()));
            tTSM.SetSendPulse(true);
            tTSM.SetNoDuplicates(true);
            tTSM.SetToServiceOnly(true);
            tTSM.QDX = 2;
            TheCommCore.PublishToNode(EngineState.ServiceNode, tTSM);
            SetIsInitializing();
            TheBaseAssets.MySYSLOG.WriteToLog(266, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(GetEngineName(), $"Sending Channel InitAndSubscribe to ORG:{TheCommonUtils.GetDeviceIDML(EngineState.ServiceNode)}"));
        }

        /// <summary>
        /// If a service/engine gets a call to Initialize, it can reply to the sender that is has been initialized.
        /// </summary>
        public void ReplyInitialized(TSM pMessage)
        {
            EngineState.LastSentInitialized = DateTimeOffset.Now;
            TSM tInitialize = new TSM(GetEngineName(), "CDE_INITIALIZED") { QDX = 3 };
            tInitialize.SetNoDuplicates(true);
            TheCommCore.PublishToOriginator(pMessage, tInitialize);
        }

        /// <summary>
        /// A service/engine has to call this method when Init() is done and the service/engine is ready to get to work.
        /// </summary>
        public void ProcessInitialized()
        {
            ProcessMessage(new TheProcessMessage() { Topic = TheBaseAssets.MyScopeManager.AddScopeID(GetEngineName()), Message = new TSM(GetEngineName(), "CDE_INITIALIZED") });
            SetStatusLevel(-1);
        }


        /// <summary>
        /// Sends a CDE-SUBSCRIBE telegram to the Active Channel of this engine
        /// </summary>
        public void Subscribe()
        {
            if (EngineState == null || EngineState.ServiceNode == Guid.Empty || EngineState.IsService) return;
            EngineState.LastSentSubscribe = DateTimeOffset.Now;

            string strSubs = TheBaseAssets.MyScopeManager.AddScopeID(GetEngineName());
            TSM tTSM = new TSM(GetEngineName(), "CDE_SUBSCRIBE", strSubs);
            tTSM.SetToServiceOnly(true);    //NEW@2012-10-27: Remove Chatter to scondary clouds
            tTSM.SetNoDuplicates(true);
            tTSM.QDX = 2;
            TheCommCore.PublishToNode(EngineState.ServiceNode, tTSM);
        }

        internal void Unsubscribe()
        {
            if (EngineState == null || EngineState.ServiceNode == Guid.Empty || EngineState.IsService) return;

            string strSubs = TheBaseAssets.MyScopeManager.AddScopeID(GetEngineName());
            TSM tTSM = new TSM(GetEngineName(), "CDE_UNSUBSCRIBE", strSubs);
            tTSM.SetToServiceOnly(true);
            tTSM.SetNoDuplicates(true);
            tTSM.QDX = 2;
            TheCommCore.PublishToNode(EngineState.ServiceNode, tTSM);
        }

#endregion



#region Message Processing
        /// <summary>
        /// Incoming Publish Notification from Service
        /// Handled on the Client
        /// </summary>
        /// <param name="pMessage">Published Message</param>
        public void ProcessMessage(TSM pMessage)
        {
            if (pMessage == null)
                return;
            ProcessMessage(new TheProcessMessage() { Topic = TheBaseAssets.MyScopeManager.AddScopeID(pMessage.ENG,ref pMessage.SID, true), Message = pMessage });
        }
        /// <summary>
        /// Incoming Publish Notification from Service
        /// Handled on the Client
        /// </summary>
        /// <param name="pMessage">Published Message</param>
        /// <param name="pLocalCallback">A local callback that is handed to the Engine Message Handler to allow local callbacks</param>
        public void ProcessMessage(TSM pMessage, Action<TSM> pLocalCallback)
        {
            ProcessMessage(new TheProcessMessage() { Topic = TheBaseAssets.MyScopeManager.AddScopeID(pMessage.ENG,ref pMessage.SID, true), Message = pMessage, LocalCallback = pLocalCallback });
        }
        /// <summary>
        /// Incoming Publish Notification from Service
        /// Handled on the Client
        /// </summary>
        /// <param name="pMessage">Published Message</param>
        public void ProcessMessage(TheProcessMessage pMessage)
        {
            if (pMessage == null || pMessage.Message == null || string.IsNullOrEmpty(pMessage.Topic)|| EngineState.IsUnloaded) return;
            string nCommand = pMessage.Topic;
            //if (!(GetEngineName().Equals(eEngineName.StorageService) && EngineState.IsService))   //This is for neutral storage REVISIT IN V4 - should be covered by IsAllowedForeinScopeProcessing and IsAllowedUnscopedProcessing Storage needs to have this set in app.config
                nCommand = TheBaseAssets.MyScopeManager.RemoveScopeID(pMessage.Topic,
                    EngineState.IsAllowedUnscopedProcessing ||
                        //Allow Unscoped processing ONLY for Update of Admin Account if request comes from FirstNode subscribers and not into cloud nodes
                        (pMessage.Message != null && !TheBaseAssets.MyServiceHostInfo.IsCloudService && pMessage.Message.IsFirstNode() && eEngineName.ContentService.Equals(pMessage.Message.ENG) && pMessage.Message.TXT?.StartsWith("CDE_UPD_ADMIN")==true),
                    EngineState.IsAllowedForeignScopeProcessing);
            if (nCommand.Equals("SCOPEVIOLATION"))
            {
                if (!TheBaseAssets.MyServiceHostInfo.IsCloudService && pMessage.Message.TXT.StartsWith("CDE_SYNCUSER"))  //SYNC_USER Not allwed between non-cloud nodes. ScopeViolation is normal in that case
                    return;
                TheBaseAssets.MySYSLOG.WriteToLog(420, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(GetEngineName(), $"Scope Violation on Command: {pMessage.Topic} from ORG:{TheCommonUtils.GetDeviceIDML(pMessage.Message?.ORG)}", eMsgLevel.l1_Error));
                return;
            }
            if (nCommand.Equals("HOSTDEBUG"))
            {
                TheBaseAssets.MySYSLOG.WriteToLog(401, pMessage.Message); return;
            }
            bool tIsProcessed = false;
            TheUserDetails tCurrentUser = TheUserManager.GetCurrentUser(TheCommonUtils.CGuid(pMessage.Message.SEID));
            if (tCurrentUser == null)
            {
                if (!string.IsNullOrEmpty(pMessage.Message.UID))
                    tCurrentUser = TheUserManager.GetUserByID(TheCommonUtils.CSCDecrypt2GUID(pMessage.Message.UID, TheBaseAssets.MySecrets.GetAI()));
            }
            if (tCurrentUser!=null)
                pMessage.CurrentUserID = tCurrentUser.cdeMID;

            if (!string.IsNullOrEmpty(pMessage.Message.TXT))
            {
                string[] Command = pMessage.Message.TXT.Split(':');
                switch (Command[0])
                {
                    case "CDE_SYSTEMMSG":
                        EngineState.LastMessage = pMessage.Message.PLS;
                        TheBaseAssets.MySYSLOG.WriteToLog(302, new TSM(pMessage.Message.ENG, pMessage.Message.PLS, eMsgLevel.l3_ImportantMessage) { ORG = pMessage.Message.ORG, TIM = pMessage.Message.TIM });//ORG-OK
                        if (TheBaseAssets.MyApplication != null)
                            TheBaseAssets.MyApplication.ShowMessageToast(new TSM(pMessage.Message.ENG, pMessage.Message.PLS, eMsgLevel.l3_ImportantMessage), null);
                        break;
                    case "CDE_GET_SERVICELOG":
                        try
                        {
                            Guid tOrg = pMessage.Message.GetOriginator();
                            TheBaseAssets.MySYSLOG.WriteToLog(401, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(GetEngineName(), string.Format("Engine {0} sending Get_ServiceLog back to: {1}", GetEngineName(), tOrg), eMsgLevel.l3_ImportantMessage));
                            var responseMsg = new TSM(eEngineName.ContentService, "CDE_SERVICE_LOG", TheBaseAssets.MySYSLOG.GetNodeLog(null, GetEngineName()==eEngineName.ContentService?"":GetEngineName(), false));
                            if (pMessage.LocalCallback != null)
                            {
                                pMessage.LocalCallback(responseMsg);
                            }
                            else
                            {
                                TheCommCore.PublishToOriginator(pMessage.Message, responseMsg );
                            }
                        }
                        catch (Exception e)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(401, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(GetEngineName(), string.Format("Engine {0} FAILED to respond to Get_ServiceInfo from : {1}", GetEngineName(), pMessage.Message.GetOriginator()), eMsgLevel.l3_ImportantMessage, e.ToString()));
                        }
                        break;
                    case "CDE_PING":
                        TheCommCore.PublishToOriginator(pMessage.Message, new TSM(GetEngineName(), $"CDE_PONG{(Command.Length>1?$":{Command[1]}":"")}",$"{pMessage.Message?.PLS}:{MyBaseThing?.GetBaseThing()?.LastMessage}"),true);
                        break;
                    case "CDE_UPDPUSH":
                    case "CDE_FILEPUSH":
                        if (!EngineState.IsAcceptingFilePush || (TheBaseAssets.MyServiceHostInfo.IsCloudService && !TheBaseAssets.MyScopeManager.IsScopingEnabled) || Command.Length < 2) break;
                        string tFileName=TheCommonUtils.SaveBlobToDisk(this, pMessage.Message, Command);
                        bool IsThing = false;
                        if (Command.Length>2)
                        {
                            TheThing tThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(Command[2]));
                            if (tThing!=null)
                            {
                                IsThing = true;
                                tThing.FireEvent(eEngineEvents.FileReceived, GetBaseThing(), new TheProcessMessage() { Topic = Command[0], Cookie=(Command.Length>3?Command[3]:null), CurrentUserID = pMessage.CurrentUserID, Message = new TSM(pMessage.Message.ENG, tFileName) { ORG = pMessage.Message.ORG } }, true);
                            }
                        }
                        if (!IsThing)
                            FireEvent(eEngineEvents.FileReceived, new TheProcessMessage() { Topic = Command[0], CurrentUserID = pMessage.CurrentUserID, Message = new TSM(pMessage.Message.ENG, tFileName) { ORG = pMessage.Message.ORG } }, true);
                        break;
                }
            }
            if (pMessage != null && pMessage.Message.ENG == GetEngineName())
            {
                TSMTotalCost tCosts = pMessage.Message.GetTotalCosts();
                if (tCosts != null)
                    EngineState.CommunicationCosts += tCosts.TotalCredits;

                Guid tOrg = pMessage.Message.GetOriginator();
                if (!string.IsNullOrEmpty(pMessage.Message.PLS) && pMessage.Message.IsPLSEncrypted())
                {
                    pMessage.Message.PLS = TheCommonUtils.cdeDecrypt(pMessage.Message.PLS, TheBaseAssets.MySecrets.GetAI());
                    pMessage.Message.ResetPLSEncryption();
                }
                switch (pMessage.Message.TXT)
                {
                    case "CDE_NOT-INITIALIZED":
                        if (!EngineState.IsService && !EngineState.IsEngineReady && EngineState.IsInitializing && !string.IsNullOrEmpty(pMessage.Message.PLS))
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(401, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(GetEngineName(), string.Format("Engine {0} received suggestion for better service node at : {1}", GetEngineName(), tOrg), eMsgLevel.l3_ImportantMessage));
                            if (EngineState.ServiceNode == Guid.Empty)
                            {
                                if (!pMessage.Message.PLS.Equals("NOTSUPPORTED"))
                                    EngineState.ServiceNode = TheCommonUtils.CGuid(pMessage.Message.PLS);
                                else
                                    return;
                            }
                            else
                                return;
                        }
                        else
                            return;
                        break;
                    case "CDE_INITIALIZE":
                        string tAlternative = "";
                        if (!EngineState.IsService && !TheBaseAssets.MyServiceHostInfo.IsCloudService && !EngineState.IsMiniRelay)
                        {
                            if (EngineState.IsEngineReady)
                               tAlternative = EngineState.ServiceNode.ToString();  //V4: Storage Service willuse a "Service" node for Route Optimization
                            if (string.IsNullOrEmpty(tAlternative))
                                tAlternative = "NOTSUPPORTED";
                            TheBaseAssets.MySYSLOG.WriteToLog(401, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(GetEngineName(), string.Format("Engine {0} cannot be initialized on this service! Suggesting better node at : {1}", GetEngineName(), tAlternative), eMsgLevel.l6_Debug));
                            TSM tInitialize = new TSM(GetEngineName(), "CDE_NOT-INITIALIZED", tAlternative) {QDX = 3};
                            tInitialize.SetNoDuplicates(true);
                            tInitialize.SID = pMessage.Message.SID;
                            tInitialize.SetDoNotRelay(true);
                            TheCommCore.PublishToOriginator(pMessage.Message, tInitialize);
                            return;
                        }
                        break;
                    default:
                        if (EngineState.IsMiniRelay || string.IsNullOrEmpty(pMessage.Message.TXT)) break;
                        string[] cmd = pMessage.Message.TXT.Split(':');
                        switch (cmd[0]) //string 2 cases
                        {
                            case "NMI_GET_DATA":    //Request for Controls from Web-Relay for HTML5 Interface
                                if (cmd.Length > 3)
                                {
                                    TSM tTsm = new TSM(eEngineName.NMIService, "NMI_CUSTOM_HTML:" + cmd[1]);
                                    if (EngineState.EngineType == null || EngineState.EngineType.AssemblyQualifiedName == null)
                                        tTsm.PLS = string.Format("<h1>Plugin Assembly for {0} on node {1} not found...did you specify SetEngineTypeName() in the SetBaseEngine() call?</h1>", EngineState.ClassName, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID);
                                    else
                                    {
                                        byte [] astream = GetPluginResource(cmd[3] + ".js");
                                        if (astream != null)
                                        {
                                            tTsm.PLS = TheCommonUtils.GenerateFinalStr(TheCommonUtils.CArray2UTF8String(astream)).Replace("%=CONTENT%", cmd[1]);
                                            tTsm.TXT = "NMI_CUSTOM_SCRIPT:" + cmd[1];
                                        }
                                        else
                                            tTsm = null;
                                    }
                                    if (tTsm != null)
                                    {
                                        TheCommCore.PublishToOriginator(pMessage.Message, tTsm);
                                        tIsProcessed = true;
                                    }
                                }
                                break;
                            case nameof(TheThingRegistry.MsgCreateThingRequestV1):
                                {
                                    var originator  = pMessage.Message.GetOriginator();
                                    if (TheCommonUtils.IsLocalhost(originator)
                                        || (TheBaseAssets.MyServiceHostInfo.AllowRemoteThingCreation && (TheBaseAssets.MySettings.IsNodeTrusted(originator) || TheUserManager.HasUserAccess(tCurrentUser?.cdeMID ?? Guid.Empty, 255, true)))
                                        )
                                    {
                                        var createParams = TheCommRequestResponse.ParseRequestMessageJSON<TheThingRegistry.MsgCreateThingRequestV1>(pMessage.Message);

                                        string error = null;
                                        if (createParams != null)
                                        {
                                            try
                                            {
                                                TheThingRegistry.CreateOwnedThingLocalAsync(createParams, new TimeSpan(0, 1, 0)).ContinueWith((t) =>
                                                {
                                                    try
                                                    {
                                                        var tThing = t?.Result;
                                                        var response = new TheThingRegistry.MsgCreateThingResponseV1();
                                                        if (tThing != null)
                                                        {
                                                            response.ThingAddress = new TheMessageAddress(tThing);
                                                        }
                                                        if (!TheCommRequestResponse.PublishResponseMessageJson(pMessage.Message, response))
                                                        {
                                                            TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(GetEngineName(), "Unable to publish response message to CreateThing Request", eMsgLevel.l1_Error, ""), false);
                                                        }
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(GetEngineName(), "Internal error while processing CreateThing Response", eMsgLevel.l1_Error, e.ToString()), false);
                                                    }
                                                }, TaskContinuationOptions.ExecuteSynchronously);
                                            }
                                            catch (Exception e)
                                            {
                                                error = String.Format("Exception: {0}", e.Message);
                                                TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(GetEngineName(), "Internal error while processing CreateThing Request", eMsgLevel.l1_Error, e.ToString()), false);
                                            }
                                        }
                                        else
                                        {
                                            error = "Invalid Message Format";
                                        }
                                        if (error != null)
                                        {
                                            var response = new TheThingRegistry.MsgCreateThingResponseV1
                                            {
                                                Error = error
                                            };
                                            if (!TheCommRequestResponse.PublishResponseMessageJson(pMessage.Message, response))
                                            {
                                                TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(GetEngineName(), "Unable to publish response message to CreateThing Request", eMsgLevel.l1_Error, ""), false);
                                            }
                                        }
                                    }
                                }
                                break;
#if !CDE_NET4
                            case nameof(TheThing.MsgApplyPipelineConfig):
                                {
                                    var originator = pMessage.Message.GetOriginator();
                                    if (TheCommonUtils.IsLocalhost(originator)
                                        || (TheBaseAssets.MyServiceHostInfo.AllowRemoteThingCreation && (TheBaseAssets.MySettings.IsNodeTrusted(originator) || TheUserManager.HasUserAccess(tCurrentUser?.cdeMID ?? Guid.Empty, 255, true)))
                                        )
                                    {
                                        ((Action) (async () =>
                                        {
                                            var request = TheCommRequestResponse.ParseRequestMessageJSON<TheThing.MsgApplyPipelineConfig>(pMessage.Message);
                                            TheThing.MsgApplyPipelineConfigResponse response;
                                            if (request != null)
                                            {
                                                try
                                                {
                                                    response = await TheThing.ApplyPipelineConfigAsync(request);
                                                }
                                                catch (Exception e)
                                                {
                                                    response = new TheThing.MsgApplyPipelineConfigResponse { Error = $"Error processing message: {e.Message}", };
                                                }
                                            }
                                            else
                                            {
                                                response = new TheThing.MsgApplyPipelineConfigResponse { Error = "Invalid Message Format", };
                                            }
                                            if (!TheCommRequestResponse.PublishResponseMessageJson(pMessage.Message, response))
                                            {
                                                TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(GetEngineName(), "Unable to publish response message to MsgApplyPipelineConfig Request", eMsgLevel.l1_Error, ""), false);
                                            }
                                        })).Invoke();
                                    }
                                }
                                break;
#endif
                        }
                        break;
                }
                if (!tIsProcessed)
                {
                    if (!string.IsNullOrEmpty(pMessage.Message.OWN))
                    {
                        TheThing tThing = TheThingRegistry.GetThingByMID(TheCommonUtils.CGuid(pMessage.Message.OWN));
                        if (tThing != null)
                        {
                            TheThing.HandleByThing(tThing, pMessage.Topic, pMessage.Message, pMessage.LocalCallback);
                            tIsProcessed = true;
                        }
                    }
                }
                if (!tIsProcessed)
                {
                    //RegisterInit(pMessage); //NEW: 2012-05-10 - Fast init if a valid packet comes in from the engine; TODO: Needs new INitialization Events!
                    TheThing baseThing;
                    if (MyBaseThing != null && (baseThing = MyBaseThing.GetBaseThing()) != null) //.HasRegisteredEvents(eEngineEvents.IncomingMessage))
                    {
                        baseThing.FireEvent(eEngineEvents.IncomingMessage, MyBaseThing, pMessage, true);
                        FireEvent(eEngineEvents.IncomingEngineMessage, pMessage, true);
                    }
                    else
                    {
                        if (pMessage.Message.TXT.Equals("CDE_INITIALIZED"))
                        {
                            SetInitialized(pMessage.Message);
                        }
                    }
                }
            }
        }



#endregion


#region static helpers
        /// <summary>
        /// Fires the call back when TheStorageService is ready for business
        /// </summary>
        /// <param name="pCallback">Callback that gets called when TheStorageService is ready</param>
        /// <param name="FireRam">Also fire if no the node does not have TheStorageService and stores everything in RAM</param>
        public static void WaitForStorageReadiness(Action<ICDEThing, object> pCallback, bool FireRam)
        {
            if (TheCDEngines.MyIStorageService != null)
            {
                TheThing tStorThing = TheCDEngines.MyIStorageService.GetBaseEngine().GetBaseThing();
                tStorThing.RegisterEvent(eEngineEvents.EngineIsReady, pCallback);
                pCallback(tStorThing, TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineState().IsEngineReady ? TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineName() : null);
            }
            else
                if (FireRam) pCallback(null, "RAM");
        }

        /// <summary>
        /// Unregister the StorageReadiness callbacks
        /// </summary>
        /// <param name="pCallback"></param>
        public static void UnregisterStorageReadiness(Action<ICDEThing, object> pCallback)
        {
            if (TheCDEngines.MyIStorageService != null)
            {
                TheThing tStorThing = TheCDEngines.MyIStorageService.GetBaseEngine()?.GetBaseThing();
                tStorThing?.UnregisterEvent(eEngineEvents.EngineIsReady, pCallback);
            }
        }

        /// <summary>
        /// Async version of WaitForStorageReadiness
        /// </summary>
        /// <param name="FireRam"></param>
        /// <returns></returns>
        public static Task<bool> WaitForStorageReadinessAsync(bool FireRam)
        {
            var tcs = new TaskCompletionSource<bool>();

            void callback(ICDEThing t, object o)
            {
                tcs.TrySetResult(o != null);
                UnregisterStorageReadiness(callback);
            }

            WaitForStorageReadiness(callback, FireRam);
            return tcs.Task;
        }

        /// <summary>
        /// Fires when all Engines are Started
        /// </summary>
        /// <param name="pCallback"></param>
        public static void WaitForEnginesStarted(Action<ICDEThing, object> pCallback)
        {
            void innerCallback()
            {
                pCallback(null, true);
                TheCDEngines.eventAllEnginesStarted -= innerCallback;
            }

            TheCDEngines.eventAllEnginesStarted -= innerCallback;
            TheCDEngines.eventAllEnginesStarted += innerCallback;
            if (TheBaseAssets.MyServiceHostInfo.AreAllEnginesStarted)
            {
                innerCallback();
            }
        }

        /// <summary>
        /// Async Version of WaitForEnginesStarted
        /// </summary>
        /// <returns></returns>
        public static Task<bool> WaitForEnginesStartedAsync()
        {
            if (TheBaseAssets.MyServiceHostInfo.AreAllEnginesStarted)
            {
                return TheCommonUtils.TaskFromResult(true);
            }
            var tcs = new TaskCompletionSource<bool>();

            WaitForEnginesStarted((t, o) =>
            {
                // This must run asynchronously to avoid deadlocks
                TheCommonUtils.cdeRunAsync("", true, ignored =>
                {
                    tcs.TrySetResult(TheCommonUtils.CBool(o));
                });
            });
            return tcs.Task;
        }

        /// <summary>
        /// Fires when an Engine is initialized
        /// </summary>
        /// <param name="pEngineName">Name of the Engine to wait for</param>
        /// <param name="pCallback"></param>
        public static bool WaitForEngineReady(string pEngineName, Action<ICDEThing, object> pCallback)
        {
            IBaseEngine tbase = TheThingRegistry.GetBaseEngine(pEngineName);
            if (tbase == null)
                return false;
            void innerCallback(ICDEThing sender, object obj)
            {
                pCallback(sender, obj);
                tbase.UnregisterEvent(eThingEvents.Initialized, innerCallback);
            }
            tbase.UnregisterEvent(eThingEvents.Initialized, innerCallback);
            tbase.RegisterEvent(eThingEvents.Initialized, innerCallback);
            if (tbase == null || !tbase.GetEngineState().IsEngineReady)
            {
                innerCallback(tbase.GetBaseThing(),null);
            }
            return true;
        }
        #endregion
    }
}
