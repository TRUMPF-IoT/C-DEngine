// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Activation;
using nsCDEngine.Communication;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Interfaces;
using nsCDEngine.ISM;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// TheBaseAssets is a collection of important static properties shared between all Plugin-Services and the hosting Application
    /// </summary>
    public static partial class TheBaseAssets
    {
        #region CryptoLib Loader
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Loads a crypto library for all Security related methods. </summary>
        ///
        /// <remarks>   Chris, 3/27/2020. </remarks>
        ///
        /// <param name="pDLLName"> Name of the crypto DLL. </param>
        /// <param name="bDontVerifyTrust"></param>
        /// <param name="pFromFile"></param>
        /// <param name="bVerifyTrustPath"></param>
        /// <param name="bDontVerifyIntegrity"></param>
        /// <param name="pMySYSLOG"></param>
        ///
        /// <returns>   Error string if load failed - then the default security in the CDE is used. Null if succeeded </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public static string LoadCrypto(string pDLLName, ICDESystemLog pMySYSLOG = null, bool bDontVerifyTrust = false, string pFromFile = null, bool bVerifyTrustPath = true, bool bDontVerifyIntegrity = false)
        {
            if (MasterSwitch)
                return CryptoLoadMessage = "LoadCrypto is not allowed after the Node has been started";
            try
            {
                if (string.IsNullOrEmpty(pDLLName))
                    pDLLName = "cdeCryptoLib.dll";
                Dictionary<string, string> tL = new();
                Assembly tCryptoAssembly = null;
                string codeSignThumb;
                var types = AppDomain.CurrentDomain.GetAssemblies();
                bool wasCryptoLoadedAlready = (types.FirstOrDefault(g => g.Location.Contains(pDLLName)) != null);//new way of detecting mobile devices
                if (!wasCryptoLoadedAlready) // AppDomain.CurrentDomain?.FriendlyName != "RootDomain" && AppDomain.CurrentDomain?.FriendlyName != "MonoTouch" && AppDomain.CurrentDomain?.FriendlyName != "Meadow.dll") //Android and IOS
                {
                    TheSystemMessageLog.ToCo($"Starting CodeSign-Verifier in ({pDLLName}) with DVT:{bDontVerifyTrust} VTP:{bVerifyTrustPath} DVI:{bDontVerifyIntegrity}");
                    MyCodeSigner ??= new TheDefaultCodeSigning(MySecrets, pMySYSLOG);
                    codeSignThumb = MyCodeSigner.GetAppCert(bDontVerifyTrust, pFromFile, bVerifyTrustPath, bDontVerifyIntegrity);
                    if (!bDontVerifyTrust && string.IsNullOrEmpty(codeSignThumb))
                    {
                        return CryptoLoadMessage = $"No code-signing certificate found but required";
                    }
                    if (!pDLLName.Contains(Path.DirectorySeparatorChar))
                        pDLLName = Path.Combine(AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory, pDLLName);
                    AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(CurrentDomain_ReflectionOnlyAssemblyResolve);
                    var pLoader = new CryptoReferenceLoader();

                    tL = pLoader.ScanLibrary(pDLLName, out TSM tTSM, out tCryptoAssembly);
                    TheSystemMessageLog.ToCo($"Domain:{AppDomain.CurrentDomain?.FriendlyName} DLL:{pFromFile} Services:{tL?.Count} Cert Found:{codeSignThumb} {tTSM?.TXT} {tTSM?.PLS}");
                }
                else
                {
                    tCryptoAssembly = types.FirstOrDefault(g => g.Location.Contains(pDLLName));
                    if (tCryptoAssembly != null)
                    {
                        var CDEPlugins = from t in tCryptoAssembly.GetTypes()
                                         let ifs = t.GetInterfaces()
                                         where ifs != null && ifs.Length > 0 && (ifs.Any(s => _KnownInterfaces.Contains(s.Name)))
                                         select new { Type = t, t.Namespace, t.Name, t.FullName };
                        foreach (var Plugin in CDEPlugins)
                        {
                            if (Plugin?.Type?.IsAbstract == false)
                            {
                                var ints = Plugin.Type.GetInterfaces();
                                foreach (var tI in ints)
                                {
                                    if (_KnownInterfaces.Contains(tI.Name))
                                    {
                                        tL[tI.Name] = Plugin.FullName;
                                    }
                                }
                            }
                        }
                    }
                }
                if ((bDontVerifyTrust || MyCodeSigner?.IsTrusted(pDLLName) == true) && tL?.Count > 0)
                {
                    if (tCryptoAssembly == null)
                        tCryptoAssembly = Assembly.LoadFrom(pDLLName);
                    if (tL.ContainsKey("ICDESecrets"))
                    {
                        var tSecType = tCryptoAssembly.GetTypes().First(s => s.FullName == tL["ICDESecrets"]);
                        MySecrets = Activator.CreateInstance(tSecType) as ICDESecrets;
                    }
                    else
                        MySecrets = new TheDefaultSecrets();
                    foreach (string tInter in _KnownInterfaces)
                    {
                        if (tL?.ContainsKey(tInter) == true)
                        {
                            var tNType = tCryptoAssembly.GetTypes().First(s => s.FullName == tL[tInter]);
                            try
                            {
                                switch (tInter)
                                {
                                    case "ICDEScopeManager": MyScopeManager = Activator.CreateInstance(tNType, new object[] { MySecrets, pMySYSLOG }) as ICDEScopeManager; break;
                                    case "ICDECodeSigning": MyCodeSigner = Activator.CreateInstance(tNType, new object[] { MySecrets, pMySYSLOG }) as ICDECodeSigning; break;
                                    case "ICDEActivation": MyActivationManager = Activator.CreateInstance(tNType, new object[] { MySecrets, pMySYSLOG }) as ICDEActivation; break;
                                    case "ICDECrypto": MyCrypto = Activator.CreateInstance(tNType, new object[] { MySecrets, pMySYSLOG }) as ICDECrypto; break;
                                }
                            }
                            catch (Exception e)
                            {
                                pMySYSLOG?.WriteToLog(0, 1, "LoadCrypto", $"Failed to create implementation of {tInter}. Most likely constructor not implemented correctly (must take ICDESecrets as first parameter): {e}", eMsgLevel.l1_Error);
                            }
                        }
                    }
                }
                else
                    return CryptoLoadMessage = $"The {pDLLName} is not trusted. Don't Trust:{bDontVerifyTrust} Interfaces Found:{tL?.Count}";
            }
            catch (Exception e)
            {
                return CryptoLoadMessage = $"Errors During Load: {e}";
            }
            return null;
        }
        static Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = AppDomain.CurrentDomain.ApplyPolicy(args.Name);
            return Assembly.ReflectionOnlyLoad(name);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Gets or sets a message describing the crypto load status. </summary>
        ///
        /// <value> A message describing the crypto load. if null loading was successful </value>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public static string CryptoLoadMessage { private set; get; } = null; //if not null, loading of crypto failed and node must decide what to do
        static bool _platformDoesNotSupportReflectionOnlyLoadFrom;
        static readonly List<string> _KnownInterfaces = new () { "ICDECrypto", "ICDESecrets", "ICDEScopeManager", "ICDECodeSigning", "ICDEActivation" };
        internal class CryptoReferenceLoader : MarshalByRefObject
        {
            public Dictionary<string, string> ScanLibrary(string assemblyPath, out TSM resTSM)
            {
                return ScanLibrary(assemblyPath, out resTSM, out _);
            }
            public Dictionary<string, string> ScanLibrary(string assemblyPath, out TSM resTSM, out Assembly tAss)
            {
                resTSM = null;
                Dictionary<string, string> mList = new ();

                tAss = null;
                if (!_platformDoesNotSupportReflectionOnlyLoadFrom)
                {
                    try
                    {
                        tAss = Assembly.ReflectionOnlyLoadFrom(assemblyPath);
                    }
                    catch (PlatformNotSupportedException) //No Assembly.ReflectionOnlyLoadFrom
                    {
                        _platformDoesNotSupportReflectionOnlyLoadFrom = true;
                    }
                }
                if (_platformDoesNotSupportReflectionOnlyLoadFrom)
                {
                    try
                    {
                        tAss = Assembly.LoadFrom(assemblyPath);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        //If we end up here, the assembly was compiled with the Native Tool Chain (i.e. XBox Store)
                        //all names are stripped out and identification of a assembly can only be made by its "interface footprint"
                        var t = AppDomain.CurrentDomain.GetAssemblies();
                        foreach (var assembly in t)
                        {
                            try
                            {
                                var CDEPlugins = from tt in assembly.GetTypes()
                                                 let ifs = tt.GetInterfaces()
                                                 where ifs != null && ifs.Length > 0 && (ifs.Any(s => _KnownInterfaces.Contains(s.Name)))
                                                 select new { Type = t, tt.Namespace, tt.Name, tt.FullName };
                                if (CDEPlugins?.Any() == true)
                                {
                                    var IsCDEAss = from tt in assembly.GetTypes()
                                                   let ifs = tt.GetInterfaces()
                                                   where ifs != null && ifs.Length > 0 && (ifs.Any(s => "IBaseEngine".Equals(s.Name)))
                                                   select new { Type = t, tt.Namespace, tt.Name, tt.FullName };

                                    if (!IsCDEAss.Any()) //Contains Crypto interfaces but no IBaseEngines 
                                    {
                                        tAss = assembly;
                                        break;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                //ignore
                            }
                        }
                    }
                }

                if (tAss != null)
                {
                    var CDEPlugins = from t in tAss.GetTypes()
                                     let ifs = t.GetInterfaces()
                                     where ifs != null && ifs.Length > 0 && (ifs.Any(s => _KnownInterfaces.Contains(s.Name)))
                                     select new { Type = t, t.Namespace, t.Name, t.FullName };
                    if (CDEPlugins == null || !CDEPlugins.Any())
                    {
                        if (!assemblyPath.ToLower().Contains(".resources."))
                        {
                            resTSM = new TSM("TheBaseAssets", $"File {assemblyPath} - found but did not contain C-DEngine Crypto Interface", eMsgLevel.l2_Warning);
                        }
                    }
                    else
                    {
                        resTSM = new TSM("TheCDEngines", $"Assembly {assemblyPath} found and added Crypto", "");
                        foreach (var Plugin in CDEPlugins)
                        {
                            if (Plugin?.Type?.IsAbstract == false)
                            {
                                var ints = Plugin.Type.GetInterfaces();
                                foreach (var tI in ints)
                                {
                                    if (_KnownInterfaces.Contains(tI.Name))
                                    {
                                        resTSM.PLS += $"{Plugin.FullName};";
                                        mList[tI.Name] = Plugin.FullName;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    resTSM = new TSM("TheCDEngines", $"Failed to load assembly {assemblyPath}", eMsgLevel.l1_Error);
                }
                return mList;
            }
        }
        #endregion


        /// <summary>
        /// C-DEngine is shutting down if MasterSwitch is FALSE
        /// </summary>
        public static bool MasterSwitch
        {
            get
            {
                return _masterSwitch;
            }
            internal set
            {
                _masterSwitch = value;
                if (!value)
                {
                    MasterSwitchCancelationTokenSource.Cancel();
                }
            }
        }
        private static bool _masterSwitch;

        /// <summary>
        /// Most Important Property Bag containing all parameter of the Hosting Service and Plugin-Services
        /// </summary>
        public static TheServiceHostInfo MyServiceHostInfo = null;

        private static readonly CancellationTokenSource MasterSwitchCancelationTokenSource = new ();
        /// <summary>
        /// When C-DEngine is shutting down all tokens obtained from MasterSwitchCancelationToken get canceled. You can use this when waiting for long running Tasks.
        /// </summary>
        public static CancellationToken MasterSwitchCancelationToken { get { return MasterSwitchCancelationTokenSource.Token; } }

        /// <summary>
        /// Allows to use an object that was created during the STAThread.Main in Plugins
        /// </summary>
        public static object STAObject;
        /// <summary>
        /// FUTURE USE: List of all Types Known to the C-DEngine
        /// </summary>
        public static List<Type> MyServiceTypes = new ();
        /// <summary>
        /// Lists all known Plugins currently running in this instance of the C-DEngine
        /// </summary>
        public static Dictionary<string, Type> MyCDEPluginTypes = new ();       //DIC-Allowed   STRING

        internal static readonly Dictionary<string, string> ServiceURLs = new ();     //DIC-Allowed    STRING
        internal static bool ForceShutdown = false;
        internal static long DelayShutDownCount = 0;
        internal static bool IsInAgentStartup = false;
        internal static bool IsJSONConfigured = false;
        internal static int[] MAX_MessageSize = { 64000, //NOTSET=0
            400000, // CDE_SERVICE = 1
            500000, // CDE_CUSTOMISB = 2
            64000, // CDE_PHONE = 3
            64000, // CDE_DEVICE = 4 (was 30000)
            250000, // CDE_CLOUDROUTE = 5
            250000, // CDE_BACKCHANNEL = 6
            4000000, // CDE_JAVAJASON =7
            5000000, // CDE_LOCALHOST =8
            64000, // CDE_MINI=9
            1000000, //  CDE_SIMULATION =10
        };

        /// <summary>
        /// Use this method to change the maximum message sizes allowed for the various cdeSenderTypes
        /// </summary>
        /// <param name="pType"></param>
        /// <param name="pSize"></param>
        public static void SetMessageSize(cdeSenderType pType, int pSize)
        {
            if (pSize < MAX_MessageSize.Length && pSize > 1024)
                MAX_MessageSize[(int)pType] = pSize;
        }

        /// <summary>
        /// XAML only: Tells the components if the C-DEngine was instantiated in Blend
        /// For all other hosts this always returns False
        /// </summary>
        public static bool IsInDesignMode = false;

        internal static ICDEScopeManager MyScopeManager { get; set; } = null;
        internal static ICDESecrets MySecrets { get; set; } = null;
        internal static ICDECrypto MyCrypto { get; set; } = null;
        internal static ICDECodeSigning MyCodeSigner { get; set; } = null;

        /// <summary>   License Activation Manager. </summary>
        public static ICDEActivation MyActivationManager { get; set; } = null;
        /// <summary>
        /// Static reference to the Current TheBaseApplication Instance
        /// </summary>
        public static TheBaseApplication MyApplication;

        /// <summary>
        /// The System Log
        /// </summary>
        public static TheSystemMessageLog MySYSLOG;
        /// <summary>
        /// Locatization Factory
        /// </summary>
        public static ILocalizationHooks MyLoc { get; set; } = new TheDefaultLocalizationUtils();
        /// <summary>
        /// Main Settings class for all Settings
        /// </summary>
        public static ICDESettings MySettings { get; set; } = new TheCDESettings();

        /// <summary>
        /// Version of the C-DEngine in Major.Minor format.
        /// </summary>
        public static double CurrentVersion { get { return BuildVersion; } }

        /// <summary>
        /// Returns the Application Information of the hosting App
        /// Plugins only have read-access to this information.
        /// </summary>
        public static ThePluginInfo GetAppInfo
        {
            get { return MyAppInfo.Clone(); }
        }

        /// <summary>
        /// Returns the current Assembly Version of the C-DEngine
        /// </summary>
        public static string CurrentVersionInfo
        {
            get
            {
                string ret = "V";
                try
                {
                    if (MyServiceHostInfo == null) return "Unknown";

                    System.Reflection.Assembly assembly = TheCommonUtils.cdeGetExecutingAssembly(); 
                    String version = assembly.FullName.Split(',')[1];
                    String fullversion = version.Split('=')[1];
                    int build = int.Parse(fullversion.Split('.')[2]);
                    int revision = int.Parse(fullversion.Split('.')[3]);
                    if (revision > 0)
                    {
                        DateTime buildDate = new DateTime(2000, 1, 1).AddDays(build).AddSeconds(revision * 2);  //CM: DateTime OK - zone irellevant
                        ret += $"{fullversion} ({buildDate})";
                    }
                    else
                    {
                        AssemblyInformationalVersionAttribute attribute = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyInformationalVersionAttribute));
                        if (attribute?.InformationalVersion != null)
                        {
                            var value = attribute.InformationalVersion;
                            // Formats:
                            // <3 or 4 part version>-<preReleaseTag>+<YYYYMMDD>.<buildMeta>
                            // <3 or 4 part version>-<preReleaseTag>+<buildMeta>
                            // <3 part version>+<quality>-YYYMMDD-<branch>-<...>-<revision>
                            // <3 part version>+YYYMMDD-<branch>-<...>-<revision>
                            var match = Regex.Match(value, @"^(?<SemVer>(?<Major>\d+)(\.(?<Minor>\d+))(\.(?<Patch>\d+))?)(\.(?<FourthPart>\d+))?(-(?<Tag>[^\+]*))?(\+((?<buildDate>\d\d\d\d\d\d\d\d)[\-\.])?(?<BuildMetaData>.*))?$");
                            if (match.Success)
                            {
                                string buildDate = "";
                                var buildDateMatch = match.Groups["buildDate"];
                                if (DateTime.TryParseExact(buildDateMatch?.Value, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, DateTimeStyles.None, out var buildDateTime))
                                {
                                    buildDate = buildDateTime.ToShortDateString();
                                    string preRelease = match.Groups["Tag"]?.Value;
                                    string buildMeta = match.Groups["BuildMetaData"]?.Value;
                                    ret += $"{fullversion.Split('.')[0]}.{fullversion.Split('.')[1]}.{build} {preRelease}({buildDate}) - {buildMeta}";
                                }
                                else
                                    ret = value;
                            }
                        }
                    }
                }
                catch (Exception e) { ret += e.Message; }
                return ret;
            }
        }

        internal static TheSessionStateManager MySession;
        internal static Dictionary<string, ThePluginInfo> MyCDEPlugins = new ();       //DIC-Allowed   STRING
        internal static TheMirrorCache<TheReceivedParts> MyReceivedParts;
        internal static ThePluginInfo MyAppInfo;
        internal static bool IsStarting;
        internal static bool IsInitialized;
        internal static TheMirrorCache<TheBlobData> MyBlobCache;
        internal static TheQueuedSender LocalHostQSender;

        class TheDummyClass { } // For version check only
        internal static double BuildVersion = TheCommonUtils.GetAssemblyVersion(new TheDummyClass());
        internal static void RecordServices()
        {
            MyServiceHostInfo.MyLiveServices = TheCommonUtils.CListToString(TheThingRegistry.GetEngineNames(true), ";");
        }

        private static void localEventScopeChanged(bool SetReq)
        {
            MyServiceHostInfo.RequiresConfiguration = SetReq;
        }

        internal static void InitAssets(TheBaseApplication pApp)
        {
            if (IsInitialized) return;
            IsInitialized = true;
            if (MyServiceHostInfo == null || MySecrets?.IsApplicationIDValid() != true)
            {
                MasterSwitch = false;
                return;
            }
            MyApplication = pApp;

            #region step 1: Moved from StartApplication to here as all the following code is updating TheBaseAssets
            string tPrefix = MySettings.GetSetting("EnvVarPrefix");
            if (!string.IsNullOrEmpty(tPrefix))
                MyServiceHostInfo.EnvVarPrefix = tPrefix;
            //The following section are "app.config" only settings required before cdeTPI is loaded. These settings cannot be set via the Provisioning Service
            int delay = TheCommonUtils.CInt(MySettings.GetSetting("StartupDelay"));
            if (delay > 0)
                TheCommonUtils.SleepOneEye((uint)(delay * 1000), 100);

            string temp = MySettings.GetSetting("AllowEnvironmentVarsToOverrideConfig");
            if (!string.IsNullOrEmpty(temp))
                MyServiceHostInfo.AllowEnvironmentVarsToOverrideConfig = TheCommonUtils.CBool(temp);
            temp = MySettings.GetSetting("AllowEnvironmentVars");
            if (!string.IsNullOrEmpty(temp))
                MyServiceHostInfo.AllowEnvironmentVars = TheCommonUtils.CBool(temp);
            temp = MySettings.GetSetting("DEBUGLEVEL");
            if (!string.IsNullOrEmpty(temp))
                MyServiceHostInfo.DebugLevel = (eDEBUG_LEVELS)TheCommonUtils.CInt(temp);
            //Clouds have special behavior and the CDE needs to know this upfront
            temp = MySettings.GetSetting("IsCloudService");
            if (!string.IsNullOrEmpty(temp))
                MyServiceHostInfo.IsCloudService = TheCommonUtils.CBool(temp);
            temp = MySettings.GetSetting("ISOEN");
            if (!string.IsNullOrEmpty(temp))
            {
                MyServiceHostInfo.IsIsolated = true;
                MyServiceHostInfo.IsoEngine = temp;
                MyServiceHostInfo.cdeNodeType = cdeNodeType.Active;
            }
            else
            {
                temp = MySettings.GetSetting("UseRandomDeviceID");
                if (!string.IsNullOrEmpty(temp))
                    MyServiceHostInfo.UseRandomDeviceID = TheCommonUtils.CBool(temp);
            }

            //Step 2: Restore from backup if exists
            // To ensure that restored backups are not overwritten during shutdown, we write them to a temp directory and move them into place on start up
            if (!MyServiceHostInfo.UseRandomDeviceID && !MyServiceHostInfo.IsIsolated)
            {
                string cacheParentPath = TheCommonUtils.GetCurrentAppDomainBaseDirWithTrailingSlash() + "ClientBin"+ Path.DirectorySeparatorChar;
                if (Directory.Exists(cacheParentPath + "__cachetorestore"))
                {
                    // Keep the old Cache around just in case
                    try
                    {
                        // First make space for the old copy, in case there was one already
                        if (Directory.Exists(cacheParentPath + "__cacheold"))
                        {
                            Directory.Delete(cacheParentPath + "__cacheold", true);
                        }
                    }
                    catch
                    {
                        //ignored
                    }
                    try
                    {
                        Directory.Move(cacheParentPath + "cache", cacheParentPath + "__cacheold");
                    }
                    catch
                    {
                        //ignored
                    }

                    // Now move the restored cache into place
                    try
                    {
                        Directory.Move(cacheParentPath + "__cachetorestore", cacheParentPath + "cache");
                    }
                    catch
                    {
                        TheSystemMessageLog.ToCo("Failure while restoring backup");
                    }
                }
            }

            MyServiceHostInfo.EnableTaskKPIs = TheCommonUtils.CBool(MySettings.GetSetting("EnableTaskKPIs"));
            if (MyServiceHostInfo.EnableTaskKPIs)
            {
                var taskKpiThread = new Thread(() =>
                {
                    do
                    {
                        var kpis = TheCommonUtils.GetTaskKpis(null);
                        TheSystemMessageLog.ToCo($"Tasks {DateTime.Now}: {TheCommonUtils.SerializeObjectToJSONString(kpis)}");
                        Thread.Sleep(1000); // Keeping it simple here, to minimize interference on task scheduler/thread scheduler etc. (Assumption: not used on production systems) 
                    } while (MasterSwitch && !Engines.TheCDEngines.IsHostReady && MyServiceHostInfo.EnableTaskKPIs);
                    TheSystemMessageLog.ToCo($"Tasks {DateTime.Now}: KPIs Handoff to NodeHost");
                });
                taskKpiThread.Start();
            }
            #endregion

            MyActivationManager ??= new TheDefaultActivationManager(MySecrets, MySYSLOG);


            #region step 3: analyse os environment information
            OperatingSystem os = Environment.OSVersion;
            var osInfoForLog = $"C-DEngine version: {BuildVersion} OS:{Environment.OSVersion} OS Version:{os.VersionString} OS Version Numbers: {os.Version.Major}.{os.Version.Minor}.{os.Version.Build}.{os.Version.Revision} Platform:{os.Platform} SP:{os.ServicePack} Processor Count: {Environment.ProcessorCount} IsMono:{(Type.GetType("Mono.Runtime") != null)} IsNetCore:{TheCommonUtils.IsNetCore()} Product: {TheCommonUtils.GetAssemblyProduct(typeof(TheBaseAssets))} ";
            TheSystemMessageLog.ToCo(osInfoForLog);
            MyServiceHostInfo.OSInfo = osInfoForLog;
            string dotNetInfoForLog = string.Empty;
#if !CDE_NET35 && !CDE_NET4
            try
            {
                string frameworkDescription = null;
                string osDescription = null;
                string processArchitecture = null;
                string osArchitecture = null;

#if CDE_STANDARD
                frameworkDescription = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
                osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
                processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
                osArchitecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
#else
                var rtInfoType = Type.GetType("System.Runtime.InteropServices.RuntimeInformation, System.Runtime.InteropServices.RuntimeInformation, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                if (rtInfoType != null)
                {
                    frameworkDescription = rtInfoType.GetProperty("FrameworkDescription").GetValue(null).ToString();
                    osDescription = rtInfoType.GetProperty("OSDescription").GetValue(null).ToString();
                    processArchitecture = rtInfoType.GetProperty("ProcessArchitecture").GetValue(null).ToString();
                    osArchitecture = rtInfoType.GetProperty("OSArchitecture").GetValue(null).ToString();
                }
#endif
                if (!string.IsNullOrEmpty(frameworkDescription))
                {
                    dotNetInfoForLog = $"NetFrameWork:{frameworkDescription} Processor:{processArchitecture} OS:{osDescription } OS Arch:{osArchitecture}";
                    TheSystemMessageLog.ToCo(dotNetInfoForLog);
                    MyServiceHostInfo.OSInfo += $" {dotNetInfoForLog}";
                }
            }
            catch
            {
                //intentionally blank
            }
#endif
            if (TheCommonUtils.IsMono()) // CODE REVIEW: Need to clean this up - do we mean Mono or Linux or case-insensitive file systems? CM: No- here we need this to find out if we are running in the MONO Runtime
            {
                MyServiceHostInfo.cdePlatform = cdePlatform.MONO_V3;
            }
            else
            {
#if CDE_NET35
                MyServiceHostInfo.cdePlatform = cdePlatform.X32_V3;
#elif CDE_NET4
                MyServiceHostInfo.cdePlatform = Environment.Is64BitProcess ? cdePlatform.NETV4_64 : cdePlatform.NETV4_32;
#elif CDE_STANDARD
                MyServiceHostInfo.cdePlatform = cdePlatform.NETSTD_V21;
#else
                MyServiceHostInfo.cdePlatform = TheCommonUtils.GetAssemblyPlatform(Assembly.GetEntryAssembly(), false, out var empty);// old: Environment.Is64BitProcess ? cdePlatform.X64_V3 : cdePlatform.X32_V4;
#endif
            }
            TheSystemMessageLog.ToCo("BaseDir: " + MyServiceHostInfo.BaseDirectory);
            #endregion

            #region step 4: Prepare essential Subsystems (Syslog, ScopeManager, Diagnostics, Caches, AppInfo)
            MySYSLOG = new TheSystemMessageLog(500); // No more ToCo after this point (will be ignored)
            TheCDEKPIs.Reset();

            MyServiceHostInfo.MiniHostInfo.MySYSLOG = MySYSLOG;
            MyServiceHostInfo.MiniHostInfo.MyKPIs = new TheKPIs();
            MyScopeManager ??= new TheDefaultScopeManager();
            MyScopeManager.SetMiniHSI(MyServiceHostInfo.MiniHostInfo);
            MyScopeManager.RegisterScopeChanged(localEventScopeChanged);

            TheDiagnostics.InitDiagnostics();
            TheDiagnostics.SetThreadName("MAIN THREAD");
            TheQueuedSenderRegistry.Startup();

            MyBlobCache = new TheMirrorCache<TheBlobData>(MyServiceHostInfo.TO.StorageCleanCycle);
            MyReceivedParts = new TheMirrorCache<TheReceivedParts>(MyServiceHostInfo.TO.StorageCleanCycle);
            MyServiceTypes.Add(typeof(TheBaseAssets));

            MyServiceHostInfo.TO.MakeHeartNormal();

            MyAppInfo = new ThePluginInfo
            {
                cdeMID = MyServiceHostInfo.cdeMID,
                CurrentVersion = MyServiceHostInfo.CurrentVersion,
                Developer = MyServiceHostInfo.VendorName,
                DeveloperUrl = MyServiceHostInfo.VendorUrl,
                HomeUrl = MyServiceHostInfo.SiteName,
                IconUrl = MyServiceHostInfo.UPnPIcon,
                LongDescription = MyServiceHostInfo.Description,
                Platform = MyServiceHostInfo.cdePlatform,
                Copyrights = MyServiceHostInfo.Copyrights,
                Price = 0,
                ServiceDescription = MyServiceHostInfo.Title,
                ServiceName = MyServiceHostInfo.ApplicationName,
                Capabilities = new List<eThingCaps> { eThingCaps.Host },
                Categories = new List<string>(),
                FilesManifest = new List<string>()
            };
            if (MyServiceHostInfo.ManifestFiles != null)
                MyAppInfo.FilesManifest.AddRange(MyServiceHostInfo.ManifestFiles);
            #endregion

            //Now load provisioning and local settings then parse them
            if (!TheCDESettings.ParseSettings(MyCmdArgs, true, false))
            {
                MasterSwitch = false;
                return;
            }

            MyCodeSigner ??= new TheDefaultCodeSigning(MySecrets, MySYSLOG);
            string tBaseDir = MyServiceHostInfo.BaseDirectory;
            if (MyServiceHostInfo.cdeHostingType == cdeHostType.IIS) tBaseDir += "bin\\";
            string uDir = tBaseDir;
            if (!string.IsNullOrEmpty(MyServiceHostInfo.ISMMainExecutable))
            {
                uDir += MyServiceHostInfo.ISMMainExecutable;
                if (MyServiceHostInfo.cdeHostingType != cdeHostType.IIS)
                {
                    var hostPath = uDir + ".exe";
                    if (!File.Exists(hostPath))
                    {
                        // We may be running under .Net Core with a .dll-based host
                        hostPath = uDir + ".dll";
                    }
                    uDir = hostPath;
                }
            }
            else
                uDir += "C-DEngine.dll";
            if (!TheCommonUtils.IsFeather())
                MyServiceHostInfo.CodeSignThumb = MyCodeSigner.GetAppCert(MyServiceHostInfo.DontVerifyTrust, uDir, MyServiceHostInfo.VerifyTrustPath, MyServiceHostInfo.DontVerifyIntegrity);  //Must be loaded here to set trust level in HSI
            MySYSLOG.WriteToLog(4153, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheBaseAssets", $"CodeSign ({MyServiceHostInfo.DontVerifyTrust}) Thumb:{MyServiceHostInfo.CodeSignThumb}", eMsgLevel.l4_Message));

            if (MyLoc is TheDefaultLocalizationUtils)
            {
                (MyLoc as TheDefaultLocalizationUtils).DoPseudoLoc = TheCommonUtils.CBool(MySettings.GetSetting("DoPseudoLoc"));
            }

            #region step 5: output some system information
            if (MyServiceHostInfo.IsMemoryOptimized)
                MySYSLOG.SetMaxLogEntries(10);
            MySYSLOG.WriteToLog(4151, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseAssets", osInfoForLog, eMsgLevel.l4_Message));

            if (!string.IsNullOrEmpty(dotNetInfoForLog))
            {
                MySYSLOG.WriteToLog(4152, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseAssets", dotNetInfoForLog, eMsgLevel.l4_Message));
            }
            MySYSLOG.WriteToLog(4153, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheBaseAssets", "BaseDir: " + MyServiceHostInfo.BaseDirectory, eMsgLevel.l4_Message));
            #endregion

            #region step 6: determine WebSocket8 vs. WebSocketsSharp
            var ws8FlagTemp = MySettings.GetSetting("DisallowWebSocket8");
            if (!string.IsNullOrEmpty(ws8FlagTemp))
            {
                MyServiceHostInfo.IsWebSocket8Active = !TheCommonUtils.CBool(ws8FlagTemp);
                MySYSLOG.WriteToLog(4154, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseAssets", $"Setting IsWebSocket8Active to {MyServiceHostInfo.IsWebSocket8Active} due to config", eMsgLevel.l4_Message));
            }
            else
            {
                MyServiceHostInfo.IsWebSocket8Active = true;
            }
            if ((os.Platform != PlatformID.Win32NT && !TheCommonUtils.IsNetCore()) || (os.Platform == PlatformID.Win32NT && (os.Version.Major < 6 || (os.Version.Major == 6 && os.Version.Minor < 2))))
            {
                MyServiceHostInfo.IsWebSocket8Active = false;
                string tWSMsg = $"Older Windows ({os.Version.Major}.{os.Version.Minor}) or non-Windows OS ({os.Platform} IsNetCore:{TheCommonUtils.IsNetCore()}) Detected - No OS Support for WebSockets Found: ";
#if !CDE_USECSWS //OK
                TheBaseAssets.MyServiceHostInfo.DisableWebSockets = true;
                tWSMsg += "WebSockets are turned Off";
#else
                tWSMsg += "Switching to WebSockets-Sharp";
#endif
                MySYSLOG.WriteToLog(4155, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseAssets", tWSMsg, eMsgLevel.l4_Message));
            }
            #endregion

            #region step 7: Initialize Localization subsystem (ILocalizationHooks)
            bool bLocInitialized = false;
            try
            {
                var entryAssembly = Assembly.GetEntryAssembly();    //This does not work in IIS!
                if (entryAssembly != null)
                {
                    var CDEPlugins = from t in entryAssembly.GetTypes()
                                     let ifs = t.GetInterfaces()
                                     where ifs != null && ifs.Length > 0 && (ifs.Contains(typeof(ILocalizationHooks)))
                                     select new { Type = t, t.Namespace, t.Name, t.FullName };
                    if (CDEPlugins != null && CDEPlugins.Any())
                    {
                        MyLoc = Activator.CreateInstance(CDEPlugins.First().Type) as ILocalizationHooks;
                    }
                    bLocInitialized = true;
                }
            }
            catch
            {
                //intentionally blank
            }
            if (!bLocInitialized)
            {
                MySYSLOG.WriteToLog(4156, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseAssets", "3rd Party ILocalization Hooks not found - using built-in", eMsgLevel.l4_Message));
            }
            #endregion

            #region step 8: Set MyStationUrl
            switch (MyServiceHostInfo.cdeHostingType)
            {
                case cdeHostType.Phone:
                case cdeHostType.Metro:
                    MyServiceHostInfo.MyDeviceMoniker = "CDEV://";
                    break;
                case cdeHostType.Silverlight:
                    MyServiceHostInfo.MyDeviceMoniker = "CDES://";
                    break;
                case cdeHostType.Device:
                    MyServiceHostInfo.MyDeviceMoniker = "CDEC://";
                    break;
                case cdeHostType.Mini:
                    MyServiceHostInfo.MyDeviceMoniker = "CDEM://";
                    break;
                default: //Services
                    MyServiceHostInfo.MyDeviceMoniker = "CDEB://";
                    break;
            }
            if (TheCommonUtils.IsHostADevice())
                MyServiceHostInfo.MyStationURL = MyServiceHostInfo.MyDeviceMoniker + "{" + MyServiceHostInfo.MyDeviceInfo.DeviceID + "}".ToUpper(); //MSU-OK
            else
            {
                if (MyServiceHostInfo.MyAltStationURLs.Any(s => s.StartsWith("CDEB")))
                    MyServiceHostInfo.MyAltStationURLs.Add("CDEB://{" + MyServiceHostInfo.MyDeviceInfo.DeviceID + "}"); 
            }
            #endregion

            #region step 9: Start Localhost QSender and SessionStateManager
            MySession = new TheSessionStateManager(MyServiceHostInfo);
            LocalHostQSender = new TheQueuedSender();   //NO connected or Error Event necessary
            LocalHostQSender.StartSender(new TheChannelInfo(MyServiceHostInfo.MyDeviceInfo.DeviceID, MyScopeManager.ScopeID, MyServiceHostInfo.MyDeviceInfo.SenderType, MyServiceHostInfo.GetPrimaryStationURL(false)), null, false, null); //NEW:2.06 Setting Realscope here ///null correct - no subs on LocalhostQS in beginning //RScope-OK
            if ((MyServiceHostInfo.DisableWebSockets || MyServiceHostInfo.MyStationWSPort == 0) && !TheCommonUtils.IsHostADevice())
            {
                MySYSLOG.WriteToLog(4157, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseAssets", $"WebSockets Disabled - no WSPort specified {MyServiceHostInfo.MyStationWSPort} or DisableWebSocket={MyServiceHostInfo.DisableWebSockets}", eMsgLevel.l4_Message));
            }
            #endregion

            // Post Device ID settings

            //Step 10: Save all settings to local cdeTPI
            MySettings.UpdateLocalSettings();
        }

        
        /// <summary>
        /// All public Parameter/Settings handed to the C-DEngine during Startup
        /// These settings are visible, unencrypted AND changable by plugins.
        /// Please use TheBaseAssets.MySettings.Set|GetSetting("settingname") to access the content
        /// In future versions of the CDE this dictionary might be read only or even internal only
        /// </summary>
        internal static IDictionary<string, string> MyCmdArgs;
    }
}
