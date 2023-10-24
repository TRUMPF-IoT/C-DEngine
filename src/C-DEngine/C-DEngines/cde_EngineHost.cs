// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.ContentService;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.StorageService.Model;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

#pragma warning disable CS1591    //TODO: Remove and document public methods

//ERROR Range: 410 - 419

namespace nsCDEngine.Engines
{
    /// <summary>
    /// TheCDEngines is the Plug-in Service manager for all services hosted inside the C-DEngine
    /// </summary>
    public static class TheCDEngines
    {
        #region Storage Mirror Interfaces
        /// <summary>
        /// Interface to the Current StorageService
        /// NOTE: This will be replace by MyIStorageService in V5- please migrate your code to use MyIStorageService
        /// </summary>
        //[Obsolete("Please use MyIStorageService instead. This will be removed in V5")]
        //public static IStorageService MyStorageService;

        private static IStorageService _MyIStorageService = null;
        /// <summary>
        /// Interface to the Current StorageService
        /// </summary>
        public static IStorageService MyIStorageService
        {
            get { return _MyIStorageService; }
        }
        /// <summary>
        /// Sets the default Storage Service for the C-DEngine.
        /// </summary>
        /// <param name="pService">Pointer to an IStorage Interface to the Storage Service</param>
        /// <returns></returns>
        public static bool SetStorageService(IStorageService pService)
        {
            if (pService == null)
                return false;
#pragma warning disable CS0618 // Type or member is obsolete
            //MyStorageService = pService;
#pragma warning restore CS0618 // Type or member is obsolete
            if (MyIStorageService == null)
            {
                _MyIStorageService = pService;
                bool Succes = _MyIStorageService.InitEdgeStore();
                if (!Succes)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4138, new TSM("TheCDEngines", "Distributed StorageService could not be started. Node falls back to local storage only", eMsgLevel.l2_Warning));
                    _MyIStorageService = null;
#pragma warning disable CS0618 // Type or member is obsolete
                    //MyStorageService = null;
#pragma warning restore CS0618 // Type or member is obsolete
                }
                return Succes;
            }
            return false;
        }
        /// <summary>
        /// My nmi service
        /// BREAKING Change in V4: Plugins with this field HAVE to be recompiled
        /// </summary>
        public static TheNMIEngine MyNMIService { get; internal set; }
        /// <summary>
        /// My service states
        /// </summary>
        internal static TheStorageMirror<TheEngineState> MyServiceStates;
        /// <summary>
        /// My storage mirror repository
        /// </summary>
        internal static cdeConcurrentDictionary<string, object> MyStorageMirrorRepository = new ();      //DIC-Allowed STRING
        /// <summary>
        /// Registers a new Storage Mirror in the Center Storage Mirror Repository
        /// </summary>
        /// <param name="IDTag">UniqueID of the Store</param>
        /// <param name="pStorageMirror">StorageMirror object</param>
        internal static void RegisterMirrorRepository(string IDTag, object pStorageMirror)
        {
            lock (MyStorageMirrorRepository.MyLock)
            {
                if (!MyStorageMirrorRepository.ContainsKey(IDTag))
                    MyStorageMirrorRepository.TryAdd(IDTag, pStorageMirror);
                else
                    MyStorageMirrorRepository[IDTag] = pStorageMirror;
            }
        }
        /// <summary>
        /// Unregisters a Storage Mirror from the Center Storage Mirror Repository
        /// </summary>
        /// <param name="IDTag">The identifier tag.</param>
        internal static void UnregisterMirrorRepository(string IDTag)
        {
            lock (MyStorageMirrorRepository.MyLock)
            {
                MyStorageMirrorRepository.TryRemove(IDTag, out _);
            }
        }

        /// <summary>
        /// NEWV4: Returns a StorageMirror from the Central Storage Mirror Registry
        /// </summary>
        /// <param name="pDataSource">The p data source.</param>
        /// <returns>System.Object.</returns>
        public static object GetStorageMirror(string pDataSource)
        {
            if (string.IsNullOrEmpty(pDataSource)) return null;
            object retObj = null;
            lock (MyStorageMirrorRepository.MyLock)
            {
                MyStorageMirrorRepository.TryGetValue(pDataSource, out retObj);
                if (retObj == null)
                {
                    string[] t = pDataSource.Split(new[] { ";:;" }, StringSplitOptions.None);
                    MyStorageMirrorRepository.TryGetValue(t[0], out retObj);
                    if (retObj == null)
                    {
                        TheCommonUtils.GetCustomClassType(out Type TemplateType, t[0]);
                        if (TemplateType != null)
                            MyStorageMirrorRepository.TryGetValue(TheStorageUtilities.GenerateUniqueIDFromType(TemplateType, null), out retObj);
                    }
                }
            }
            return retObj;
        }

        /// <summary>
        /// Returns a List of StorageMirrors from the Central Storage Mirror Repository
        /// </summary>
        /// <returns></returns>
        public static List<object> GetStorageDefinitions()
        {
            return MyStorageMirrorRepository?.Values?.ToList().FindAll(sm => sm != null && sm is TheStorageMirror<StorageDefinition>);
        }

        /// <summary>
        /// Returns a list of all Storage Mirrors with their key and friendly name
        /// </summary>
        /// <returns></returns>
        public static List<KeyValuePair<string, string>> EnumerateStorageMirror()
        {
            List<KeyValuePair<string, string>> tPairList = new ();
            foreach (string key in MyStorageMirrorRepository.Keys)
            {
                object MyStorageMirror = GetStorageMirror(key);
                if (MyStorageMirror != null)
                {
                    Type magicType = MyStorageMirror.GetType();
                    MethodInfo magicMethod = magicType.GetMethod("GetFriendlyName");
                    if (magicMethod != null)
                    {
                        string tUpdated = magicMethod.Invoke(MyStorageMirror, null).ToString();
                        tPairList.Add(new KeyValuePair<string, string>(key, tUpdated));
                    }
                }
            }
            return tPairList.GroupBy(o => o.Value).Select(g => g.First()).ToList();
        }

        /// <summary>
        /// My access stats
        /// </summary>
        internal static TheStorageMirror<TheAccessStatistics> MyAccessStats;
        #endregion
        /// <summary>
        /// First plugin class instantiated in the Host application - used for UWP, iOS and other locked down device types
        /// </summary>
        public static IBaseEngine MyPluginEngine;
        /// <summary>
        /// First plugin class instantiated in the Host application - used for UWP, iOS and other locked down device types
        /// </summary>
        public static List<IBaseEngine> MyPluginEngines;
        /// <summary>
        /// My content engine - quick access to the ContentEngine
        /// BREAKING CHAGE: Plugins using this Property have to be recompiled
        /// </summary>
        public static TheContentServiceEngine MyContentEngine { get; internal set; }
        /// <summary>
        /// My thing engine
        /// BREAKING CHAGE: Plugins using this Property have to be recompiled
        /// </summary>
        public static TheThingEngine MyThingEngine { get; internal set; }

        /// <summary>
        /// My custom engine
        /// </summary>
        internal static List<ICDEPlugin> MyCustomEngines;
        /// <summary>
        /// My custom type
        /// </summary>
        internal static List<Type> MyCustomTypes;

        /// <summary>
        /// Fires when a new engine was installed and started
        /// </summary>
        public static Action<string> eventNewPluginInstalled;
        /// <summary>
        /// Fires when a new engine was installed and started
        /// </summary>
        public static Action<string> eventPluginStarted;

        /// <summary>
        /// Fires when All Engines have been started
        /// RETIRED in V4: please use TheBaseEngine.WaitForEnginesStarted(). Will be removed in V5
        /// </summary>
        public static Action eventAllEnginesStarted;

        /// <summary>
        /// Fires when all Engines are Ready
        /// RETIRED in V4: please do not use anymore. Will be removed in V5
        /// </summary>
        public static Action<bool> eventAllEnginesReady;
        /// <summary>
        /// Will be called if an Engine was successfully started with a new URL
        /// </summary>
        public static Action<TSM> eventNewURLReceived;

        /// <summary>
        /// Fires when the C-DEngine stops all engines
        /// </summary>
        public static Action eventEngineShutdown;

        /// <summary>
        /// Returns true of all Engines are ready
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public static bool AreAllEnginesReady()
        {
            bool tAreAllReady = true;
            List<IBaseEngine> tList = TheThingRegistry.GetBaseEngines(true);
            foreach (IBaseEngine tbase in tList)
            {
                if (tbase == null || !tbase.GetEngineState().IsEngineReady)
                {
                    tAreAllReady = false;
                    break;
                }
            }
            return tAreAllReady;
        }

        /// <summary>
        /// Starts the engines.
        /// </summary>
        /// <param name="CustomEngines">List of custom engine.</param>
        /// <param name="pTypes">Type of the plugin (unused as of now)</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        internal static bool StartEngines(List<ICDEPlugin> CustomEngines, List<Type> pTypes)
        {
            if (!TheBaseAssets.MasterSwitch) return false;

            MyCustomEngines = CustomEngines;
            MyCustomTypes = pTypes;
            if (MyServiceStates == null)
            {
                MyServiceStates = new(null)
                {
                    IsRAMStore = true,
                    CacheTableName = "HostEngineStates"
                };
                MyServiceStates.InitializeStore(true);
            }
            FindPlugins();

            if (TheBaseAssets.MyServiceHostInfo.TO.HeadlessStartupDelay > 0)
                TheCommonUtils.SleepOneEye((uint)(TheBaseAssets.MyServiceHostInfo.TO.HeadlessStartupDelay * 1000), 100);

            TheBaseEngine tStorageEngine = null;
            if (TheBaseAssets.MyCDEPlugins.Any(s => s.Value.Categories != null && s.Value.Categories.Contains("IStorageService")))
            {
                var tStore = TheBaseAssets.MyCDEPlugins.FirstOrDefault(s => s.Value.Categories != null && s.Value.Categories.Contains("IStorageService"));
                tStorageEngine = CreatePlugin(tStore.Key);
                if (tStorageEngine != null && !SetStorageService((IStorageService)tStorageEngine?.AssociatedPlugin))
                    tStorageEngine = null;
            }
            TheBaseAssets.MyApplication?.MyUserManager?.Init(!TheBaseAssets.MyServiceHostInfo.IsUserManagerInStorage || MyIStorageService == null, false);
            TheBaseAssets.MySYSLOG.WriteToLog(4143, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseApplication", "UserManager Started", eMsgLevel.l3_ImportantMessage));

            TheBaseEngine tBase = new ();
            ICDEPlugin tIBase = new TheThingEngine();
            MyThingEngine = (TheThingEngine)tIBase;
            tBase.SetEngineID(new Guid("{CDECCA18-E24C-418F-8CDB-C0889709D3FD}"));
            tBase.SetFriendlyName("The Thing Service");
            tBase.AddCapability(eThingCaps.Internal);
            tBase.AssociatedPlugin = tIBase;
            tIBase.InitEngineAssets(tBase);
            if (!TheThingRegistry.RegisterEngine(tBase))
            {
                return false;
            }
            TheThing tThing = tBase.MyBaseThing.GetBaseThing();
            if (tThing != null)
                tThing.RegisterEvent(eEngineEvents.EngineIsReady, StartAllEngines);
            StartPluginService(tIBase.GetBaseEngine(), false, false);
            if (tBase.EngineState.IsEngineReady)
            {
                if (tStorageEngine != null)
                {
                    TheThingRegistry.RegisterEngine(tStorageEngine.GetBaseEngine());
                    StartPluginService(tStorageEngine, true, TheBaseAssets.MyServiceHostInfo.IsIsolated); //Thing Registry is ready - now start Storage Service (UX/Init/BaseThing)
                }
                TheCommonUtils.cdeRunAsync("Starting engines...", true, (o) =>
                  {
                      StartAllEngines(null, null);
                  });
            }
            return true;
        }

        internal static bool IsHostReady = false;
        private static readonly object LockStartup = new ();
        /// <summary>
        /// Starts all engines.
        /// </summary>
        /// <param name="sender">The sender (Should be TheThingEngine)</param>
        /// <param name="pReady">true if TheThingEngine was ready</param>
        private static void StartAllEngines(ICDEThing sender, object pReady)
        {
            if (TheCommonUtils.cdeIsLocked(LockStartup))
                return;
            lock (LockStartup)
            {
                if (TheBaseAssets.MyServiceHostInfo.AllEnginesAreStarting || TheBaseAssets.MyServiceHostInfo.AllSystemsReady)
                    return;
                TheBaseAssets.MyServiceHostInfo.AllEnginesAreStarting = true;
            }
            if (!TheBaseAssets.MasterSwitch)
            {
                TheBaseAssets.MyApplication.Shutdown(true);
                return;
            }

            if (MyCustomEngines?.Count > 0)
            {
                foreach (var mEng in MyCustomEngines)
                {
                    if (!TheBaseAssets.MyCDEPlugins.ContainsKey(mEng.GetType().FullName))
                    {
                        TheBaseAssets.MyCDEPlugins.Add(mEng.GetType().FullName, new ThePluginInfo()
                        {
                            PluginType = mEng.GetType(),
                            ServiceName = mEng.GetType().FullName,
#if CDE_NET35 || CDE_NET4
                            PluginPath = mEng.GetType().Assembly.Location
#else
                            PluginPath = mEng.GetType().GetTypeInfo().Assembly.Location
#endif
                        });
                    }
                }
            }

            if (TheBaseAssets.MyServiceHostInfo.IsIsolated && TheBaseAssets.MyCDEPlugins.Count == 0)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4138, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Isolated Plugin Could not be found- ISOLater will exit", eMsgLevel.l1_Error));
                TheBaseAssets.MyApplication.Shutdown(true);
                return;
            }

#if !CDE_NET4
            if (!TheCommonUtils.IsFeather())
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4172, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheCDEngines", "Applying .cdeconfig files", eMsgLevel.l7_HostDebugMessage));
                TheThing.ApplyConfigurationFilesAsync().ContinueWith(t =>
                {
                    if (t.IsCompleted)
                    {
                        var result = t.Result;
                        if (result.Success)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4173, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Applying .cdeconfig files - Done", eMsgLevel.l3_ImportantMessage, $"Files: {result.NumberOfFiles} Failed: {result.NumberOfFailedFiles}"));
                        }
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4174, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Applying .cdeconfig files: One or more errors occurred. See previous log entries for additional details.", eMsgLevel.l1_Error, $"Files: {result.NumberOfFiles} Failed: {result.NumberOfFailedFiles}. {TheCommonUtils.GetAggregateExceptionMessage(t.Exception, !TSM.L(eDEBUG_LEVELS.ESSENTIALS))}"));
                        }
                    }
                    else
                    {
                        if (t.Exception != null)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4174, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Applying .cdeconfig files: One or more errors occurred. See previous log entries for additional details.", eMsgLevel.l1_Error, TheCommonUtils.GetAggregateExceptionMessage(t.Exception, !TSM.L(eDEBUG_LEVELS.ESSENTIALS))));
                        }
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4174, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Applying .cdeconfig files: One or more errors occurred. See previous log entries for details.", eMsgLevel.l1_Error));
                        }
                    }
                });
            }
#endif
            TheBaseAssets.MySYSLOG.WriteToLog(4138, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Starting Engines", eMsgLevel.l7_HostDebugMessage));

            IsHostReady = TheThingRegistry.RegisterHost();

            TheBaseEngine tBase = new ();
            ICDEPlugin tIBase = new TheContentServiceEngine();
            tIBase.InitEngineAssets(tBase);
            tBase.AssociatedPlugin = tIBase;
            MyContentEngine = (TheContentServiceEngine)tIBase;
            TheThingRegistry.RegisterEngine(tBase);
            StartPluginService(tIBase.GetBaseEngine(), false, false);

            if (!TheBaseAssets.MyServiceHostInfo.DisableNMI)
            {
                tBase = new TheBaseEngine();
                tIBase = new TheNMIEngine();
                tIBase.InitEngineAssets(tBase);
                tBase.AssociatedPlugin = tIBase;
                MyNMIService = (TheNMIEngine)tIBase;
                TheThingRegistry.RegisterEngine(tBase);
                StartPluginService(tIBase.GetBaseEngine(), false, false);
            }

            TheBaseAssets.MySYSLOG.WriteToLog(4137, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCDEngines", "Initializing Plugins (Create instance and call InitEngineAssets)", eMsgLevel.l7_HostDebugMessage));
            List<IBaseEngine> tStartList = new ();
            foreach (string tTargetStation in TheBaseAssets.MyCDEPlugins.Keys.ToList()) //KEY
            {
                if (string.IsNullOrEmpty(tTargetStation)) continue;
                var tE = CreatePlugin(tTargetStation);
                if (tE != null)
                    tStartList.Add(tE);
            }
            //Ensures that internal plugins are loaded first and then NMI enhancements before all others
            var tSortedList = tStartList.OrderBy(s => s.GetBaseEngine().GetLowestCapability());
            TheBaseAssets.MySYSLOG.WriteToLog(4137, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCDEngines", $"Starting Plugins (RegisterEngine/Init/CreateUX) {(TheBaseAssets.MyServiceHostInfo.AsyncEngineStartup ? "async" : "sync")}", eMsgLevel.l7_HostDebugMessage));
            foreach (IBaseEngine tBase2 in tSortedList)
            {
                if (tBase2 != null)
                {
                    switch (tBase2.GetEngineName())
                    {
                        case eEngineName.ContentService:
                        case eEngineName.NMIService:
                        case eEngineName.ThingService:
                            StartedEngines++;
                            continue;
                        default:
                            if (TheBaseAssets.MyServiceHostInfo.AsyncEngineStartup && !TheBaseAssets.MyServiceHostInfo.IsIsolated)  //Dont start isolated plugins async ever
                            {
                                TheCommonUtils.cdeRunAsync($"EngineStartup{tBase2.GetEngineName()}", true, (o) =>
                                {
                                    try
                                    {
                                        StartPluginService(tBase2, true, TheBaseAssets.MyServiceHostInfo.IsIsolated);
                                    }
                                    catch (Exception ee)
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(4137, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", $"Plugin failed to start {tBase2?.GetEngineName()}", eMsgLevel.l1_Error, ee.ToString()));
                                    }
                                    StartedEngines++;
                                    if (StartedEngines == tStartList.Count)
                                        AllEnginesHaveStarted();
                                });
                            }
                            else
                            {
                                StartPluginService(tBase2, true, TheBaseAssets.MyServiceHostInfo.IsIsolated);
                                StartedEngines++;
                            }
                            break;
                    }
                }
                else
                    StartedEngines++;
            }

            TheBaseAssets.MySYSLOG.WriteToLog(4137, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCDEngines", "Starting Mini-Relay-Engines", eMsgLevel.l7_HostDebugMessage));
            foreach (string tEngine in TheBaseAssets.MyServiceHostInfo.RelayEngines)
            {
                if (string.IsNullOrEmpty(tEngine) || TheThingRegistry.IsEngineStarted(tEngine, false)) continue;
                tBase = new TheBaseEngine();
                tIBase = new TheMiniRelayEngine();
                tIBase.InitEngineAssets(tBase);
                tBase.AssociatedPlugin = tIBase;
                tBase.SetEngineName(tEngine);
                tBase.SetFriendlyName(tEngine);
                TheThingRegistry.RegisterEngine(tBase);
                StartPluginService(tBase, false, false);
            }

            if (!TheBaseAssets.MyServiceHostInfo.AsyncEngineStartup)
                AllEnginesHaveStarted();
        }

        private static int StartedEngines = 0;

        private static void AllEnginesHaveStarted()
        {
            TheBaseAssets.RecordServices();

            TheBaseAssets.MySYSLOG.WriteToLog(4130, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Starting Communication", eMsgLevel.l7_HostDebugMessage));
            if (!TheCommCore.StartCommunications())
            {
                StopAllEngines();
                TheBaseAssets.MySYSLOG.WriteToLog(4131, new TSM("TheCDEngines", "Communication could not be started! All Engines stopped", eMsgLevel.l1_Error));
                TheBaseAssets.MyApplication.Shutdown(true);
                return;
            }
            TheBaseAssets.MySYSLOG.WriteToLog(4132, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Communication Started", eMsgLevel.l3_ImportantMessage));
            StartIsolatedPlugins();
            TheBaseAssets.MyServiceHostInfo.AreAllEnginesStarted = true;
            TheBaseAssets.MySYSLOG.WriteToLog(4133, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "All Engines Started", eMsgLevel.l3_ImportantMessage));
            if (eventAllEnginesStarted != null)
            {
                try
                {
                    eventAllEnginesStarted();
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4134, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCDEngines", "Some Plugin Crashed during AllengineStarted event", eMsgLevel.l2_Warning, e.ToString()));
                }
            }
            if (TheBaseAssets.MyApplication.MyCommonDisco != null)
                TheBaseAssets.MyApplication.MyCommonDisco.RegisterNewMatch(NewStationFound);
            TheBaseAssets.MyServiceHostInfo.AllSystemsReady = true;
            TheBaseAssets.MyServiceHostInfo.AllEnginesAreStarting = false;
            if (!TheBaseAssets.MasterSwitch)
                TheBaseAssets.MyApplication.Shutdown(true);

            //AutoBackup
            ISM.TheISMManager.StartAutoBackup();
        }

        internal static void StartIsolatedPlugins()
        {
            if (!TheBaseAssets.MyServiceHostInfo.IsIsolated && TheBaseAssets.MyScopeManager.IsScopingEnabled)
            {
                //New in V4 - IsoEngines
                foreach (string tEngine in TheBaseAssets.MyServiceHostInfo.IsoEnginesToLoad)
                {
                    IsolatePlugin(tEngine);
                }
            }
        }

        internal static bool IsolatePlugin(string tEngine)
        {
            if (TheCommonUtils.IsDeviceSenderType(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType)) //IDST-OK: Devices do not support Plugin Isolation
                return false;
            var tP = TheCommonUtils.cdeSplit(tEngine, ':', false, false);
            if (tP.Length < 2) return false;
            if (string.IsNullOrEmpty(tP[1]) || TheThingRegistry.IsEngineStarted(tP[1], false)) return false;
            TheBaseEngine tBase = InitMiniRelay(tP[1], true);
            if (!tBase.GetBaseThing().IsDisabled)
            {
                ISM.TheCDESettings.AddTrustedNodes(tBase.GetBaseThing().cdeO.ToString(), true);
                tBase.ISOLater = (ISM.TheISMManager.LaunchIsolator(tP[0], TheThing.GetSafePropertyGuid(tBase.GetBaseThing(), "ISONode")));
                tBase.ISOLater.Exited += tBase.ISOLater_Exited;
            }
            else
                tBase.EngineState.IsUnloaded = true;
            tBase.EngineState.IsIsolated = true;
            return true;
        }



        static TheBaseEngine CreatePlugin(string pPluginName)
        {
            if (TheBaseAssets.MyCDEPlugins.ContainsKey(pPluginName) && TheBaseAssets.MyCDEPlugins[pPluginName].PluginType != null
                && !pPluginName.Equals(MyCustomEngines?.GetType().FullName)
                )
                return null;    //new V4.106: Dont ever start a plugin twice! (StorageService already started)
            TheBaseEngine tBase = new ();
            ICDEPlugin tIBase = null;
            Type tNType = null;
            try
            {
                if (TheBaseAssets.MyServiceHostInfo.IgnoredEngines.Contains(pPluginName))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4110, new TSM("TheCDEngines", "StartEngines", eMsgLevel.l3_ImportantMessage, "Plugin-Service ignored by setting and will not start:" + pPluginName));
                    return null;
                }
                bool IsCustomEngine = false;
                if (MyCustomEngines?.Count > 0)
                {
                    foreach (var tEng in MyCustomEngines)
                    {
                        if (tEng == null)
                            continue;
                        if (pPluginName.Equals(tEng.GetType().FullName))
                        {
                            tIBase = tEng;
                            tNType = tEng.GetType();
                            TheBaseAssets.MyCDEPluginTypes[pPluginName] = tNType;
                            IsCustomEngine = true;
                            break;
                        }
                    }
                }
                if (!IsCustomEngine)
                {
                    if (!TheBaseAssets.MyCDEPlugins.ContainsKey(pPluginName)) return null;
                    if (!TheBaseAssets.MyServiceHostInfo.IsIsolated)
                    {
                        TheThing tEngThing = TheThingRegistry.GetBaseEngineAsThing(pPluginName, true);
                        if (tEngThing != null)
                        {
                            if (TheThing.GetSafePropertyBool(tEngThing, "IsIsolated"))
                            {
                                string tEngIso = $"{Path.GetFileName(TheBaseAssets.MyCDEPlugins[pPluginName].PluginPath)}:{pPluginName}";
                                if (!TheBaseAssets.MyServiceHostInfo.IsoEnginesToLoad.Contains(tEngIso))
                                    TheBaseAssets.MyServiceHostInfo.IsoEnginesToLoad.Add(tEngIso);
                                if (TheThing.GetSafePropertyGuid(tEngThing, "ISONode") == Guid.Empty)
                                    TheThing.SetSafePropertyGuid(tEngThing, "ISONode", Guid.NewGuid());
                                return null;
                            }
                            if (tEngThing.IsDisabled)
                            {
                                tBase = InitMiniRelay(pPluginName, false);
                                tBase.EngineState.IsUnloaded = true;
                                return null;
                            }
                        }
                    }
                    string tPP = $"{Path.GetDirectoryName(TheBaseAssets.MyCDEPlugins[pPluginName].PluginPath)}\\{Path.GetFileNameWithoutExtension(TheBaseAssets.MyCDEPlugins[pPluginName].PluginPath)}{(TheBaseAssets.MyServiceHostInfo.cdePlatform == cdePlatform.X64_V3 ? "64" : "32")}{Path.GetExtension(TheBaseAssets.MyCDEPlugins[pPluginName].PluginPath)}";
                    if (File.Exists(tPP))
                        TheBaseAssets.MyCDEPlugins[pPluginName].PluginPath = tPP;
                    Assembly tAss = Assembly.LoadFrom(TheBaseAssets.MyCDEPlugins[pPluginName].PluginPath);
                    tNType = tAss.GetTypes().First(s => s.FullName == pPluginName);
                    var asa = Activator.CreateInstance(tNType); 
                    tIBase = asa as ICDEPlugin;
                    TheBaseAssets.MyCDEPlugins[pPluginName].PluginType = tNType;
                    TheBaseAssets.MyCDEPluginTypes[pPluginName] = tNType;

                    string temp = TheBaseAssets.MySettings.GetSetting(pPluginName);
                    if (!string.IsNullOrEmpty(temp))
                        TheBaseAssets.ServiceURLs.Add(pPluginName, temp);
                }
                if (tIBase == null)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4110, new TSM("TheCDEngines", "StartEngines", eMsgLevel.l1_Error, $"Could not activate Plugin-Service: {pPluginName}"));
                    return null;
                }
                EngineAssetInfoAttribute.ApplyEngineAssetAttributes(tNType, tBase);
                tIBase.InitEngineAssets(tBase);
                EngineAssetInfoAttribute.ApplyDeviceTypeAttributes(tNType, tBase);
                if (tIBase.GetBaseEngine() == null)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4110, new TSM("TheCDEngines", "StartEngines", eMsgLevel.l1_Error, "Plugin-Service refused to start:" + pPluginName));
                    return null;
                }
                if (TheCommonUtils.CDbl(tIBase.GetBaseEngine().GetCDEMinVersion()) > TheBaseAssets.BuildVersion)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4110, new TSM("TheCDEngines", "StartEngines", eMsgLevel.l1_Error, string.Format("Plugin-Service {0} requires a newer version of the C-DEngine! Requires at least {1}, version is {2}", pPluginName, tIBase.GetBaseEngine().GetCDEMinVersion(), TheBaseAssets.BuildVersion)));
                    return null;
                }
                tBase.AssociatedPlugin = tIBase;
                if (TheBaseAssets.MyCDEPluginTypes[pPluginName].GetInterfaces().Contains(typeof(ICDENMIPlugin)))
                {
                    tBase.AddCapability(eThingCaps.NMIEngine);
                    TheNMIEngine.RegisterEngine(tBase);
                }
                if (TheBaseAssets.MyCDEPluginTypes[pPluginName].GetInterfaces().Contains(typeof(ICDERulesEngine)))
                {
                    tBase.AddCapability(eThingCaps.RulesEngine);
                }
                if (TheBaseAssets.MyCDEPluginTypes[pPluginName].GetInterfaces().Contains(typeof(IStorageService)))
                {
                    tBase.AddCapability(eThingCaps.DistributedStorage);
                }
                if (IsCustomEngine)
                {
                    MyPluginEngine ??= tIBase.GetBaseEngine();
                    MyPluginEngines ??= new List<IBaseEngine>();
                    MyPluginEngines.Add(tIBase.GetBaseEngine());
                }
            }
            catch (Exception eee)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4111, new TSM("TheCDEngines", "StartEngines", eMsgLevel.l1_Error, string.Format("Initializing Plugin-Service {0} crashed. Error: {1} - Plugin will be ignored", pPluginName, eee)));
                TheBaseAssets.MyCDEPlugins.Remove(pPluginName);
                TheBaseAssets.MyCDEPluginTypes.Remove(pPluginName);
                return null;
            }
            // TheThingRegistry.RegisterEngine(tBase, IsIsolated); //NEW 4.0083: Moved to StartNewEngine...possibly Put on a Thread for better startup speed
            return tBase;
        }


        /// <summary>
        /// Registers a new PubSub Topic with the current node. This creates a new "RelayOnly" topic in the C-DEngine Pub/Sub System
        /// </summary>
        /// <param name="pTopicName">Name of the Pub/Sub Topic</param>
        /// <returns>Return TheBaseEngine of the new Topic</returns>
        public static TheBaseEngine RegisterPubSubTopic(string pTopicName)
        {
            if (string.IsNullOrEmpty(pTopicName) || TheThingRegistry.IsEngineStarted(pTopicName, false))
                return null;
            if (TheBaseAssets.MyServiceHostInfo.RelayEngines.Contains(pTopicName)) return null;
            TheBaseAssets.MyServiceHostInfo.RelayEngines.Add(pTopicName);
            var tRes = InitMiniRelay(pTopicName, false);
            TheQueuedSenderRegistry.UpdateSubscriptionsOfConnectedNodes();
            return tRes;
        }


        /// <summary>
        /// Registers a new MiniRelay with the current node
        /// </summary>
        /// <param name="pEngineName">Name of the MiniRelay</param>
        /// <returns>Returns false if the Relay could not be registered</returns>
        public static bool RegisterNewMiniRelay(string pEngineName)
        {
            return RegisterPubSubTopic(pEngineName) != null;
        }

        private static TheBaseEngine InitMiniRelay(string pEngineName, bool WillBeIsolated)
        {
            TheBaseEngine tBase = new ();
            ICDEPlugin tIBase = new TheMiniRelayEngine();
            tIBase.InitEngineAssets(tBase);
            tBase.AssociatedPlugin = tIBase;
            tBase.SetEngineName(pEngineName);
            tBase.SetFriendlyName(pEngineName);
            TheThingRegistry.RegisterEngine(tBase, false, WillBeIsolated);
            StartPluginService(tBase, false, false);
            TheBaseAssets.RecordServices();
            if (TheBaseAssets.MyApplication.MyCommonDisco != null)
                TheBaseAssets.MyApplication.MyCommonDisco.UpdateDiscoDevice();
            return tBase;
        }

        /// <summary>
        /// Starts the new engine.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        internal static bool StartNewEngines()
        {
            bool bRetVal = false;
            FindPlugins();
            TheBaseAssets.MySYSLOG.WriteToLog(4139, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Engines: Starting New Engines", eMsgLevel.l7_HostDebugMessage));

            foreach (string tTargetStation in TheBaseAssets.MyCDEPluginTypes.Keys)
            {
                if (StartNewEngine(tTargetStation))
                    bRetVal = true;
            }
            if (bRetVal)
            {
                TheBaseAssets.RecordServices();
                if (TheBaseAssets.MyApplication.MyCommonDisco != null)
                    TheBaseAssets.MyApplication.MyCommonDisco.UpdateDiscoDevice();
            }
            return bRetVal;
        }

        internal static bool StartNewEngine(string tEngineName)
        {
            if (string.IsNullOrEmpty(tEngineName) || (TheBaseAssets.MyServiceHostInfo.IgnoredEngines != null && TheBaseAssets.MyServiceHostInfo.IgnoredEngines.Contains(tEngineName)) || TheThingRegistry.IsEngineStarted(tEngineName, false))
                return false;
            TheBaseEngine tBase = CreatePlugin(tEngineName);
            if (tBase != null)
            {
                StartPluginService(tBase, true, false);
                eventNewPluginInstalled?.Invoke(tEngineName);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Starts the plugin service.
        /// </summary>
        /// <param name="tBase">The t base.</param>
        /// <param name="RemoteConnect"></param>
        /// <param name="IsIsolated">if true the plugin will be islated</param>
        internal static void StartPluginService(IBaseEngine tBase, bool RemoteConnect, bool IsIsolated)
        {
            if (tBase == null)
                return;
            if (tBase.GetBaseThing() == null || !tBase.GetBaseThing().HasLiveObject)
                TheThingRegistry.RegisterEngine(tBase.GetBaseEngine(), IsIsolated);
            if (tBase.GetBaseThing() == null || !tBase.GetBaseThing().HasLiveObject || tBase.GetEngineState() == null)
                return;
            if (MyServiceStates != null)
                MyServiceStates.AddAnItem(tBase.GetEngineState());
            tBase.GetEngineState().ClassName = tBase.GetEngineName();
            tBase.GetEngineState().FriendlyName = TheBaseAssets.MyLoc.GetLocalizedStringByKey(0, null, tBase.GetFriendlyName(), false);
            tBase.GetEngineState().MyStationUrl = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false);

            if (tBase.GetEngineState().IsUnloaded || tBase.GetEngineState().IsDisabled)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(412, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", $"Plugin {tBase.GetEngineName()} marked unloaded and will not be started", eMsgLevel.l2_Warning));
                return;
            }
            TheBaseAssets.MySYSLOG.WriteToLog(412, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCDEngines", "Engines: Starting " + tBase.GetEngineName(), eMsgLevel.l7_HostDebugMessage));
            tBase.GetEngineState().IsInEngineStartup = true;
            TheBaseAssets.LocalHostQSender.Subscribe(TheBaseAssets.MyScopeManager.AddScopeID(tBase.GetEngineName())); //NEW:2.06 LocalHost subscribes to all Plugins-Services

            if ((TheBaseAssets.MyServiceHostInfo.FallbackToSimulation && !TheBaseAssets.MyServiceHostInfo.SimulatedEngines.Contains(tBase.GetEngineName())) || (!TheBaseAssets.MyServiceHostInfo.FallbackToSimulation && TheBaseAssets.MyServiceHostInfo.SimulatedEngines.Contains(tBase.GetEngineName())))
                tBase.StartEngine(new TheChannelInfo(TheBaseAssets.MyScopeManager.GenerateNewAppDeviceID(cdeSenderType.CDE_SIMULATION), cdeSenderType.CDE_SIMULATION, "SIMULATION"));  //ALLOWED ONLY-ONE
            else
            {
                tBase.StartEngine(null);
                //else //NEW:V3B3
                if (RemoteConnect)
                {
                    TheBaseAssets.ServiceURLs.TryGetValue(tBase.GetEngineName(), out string turl);
                    if (!string.IsNullOrEmpty(turl))
                    {
                        string[] ts = turl.Split(';');
                        foreach (string t in ts)
                        {
                            TheChannelInfo tInfo = TheQueuedSenderRegistry.GetChannelInfoByUrl(t);
                            tInfo ??= new TheChannelInfo(TheBaseAssets.MyServiceHostInfo.IsConnectedToCloud ? cdeSenderType.CDE_CLOUDROUTE : cdeSenderType.CDE_SERVICE, t);
                            tBase.StartEngine(tInfo);
                        }
                    }
                }
            }

            ICDEThing tThing = tBase.GetBaseThing();
            if (tThing != null)
                tThing.GetBaseThing().RegisterEvent(eEngineEvents.EngineIsReady, sinkEngineIsReady);
            sinkEngineIsReady(tThing, tBase.GetEngineState().IsEngineReady ? tBase.GetEngineName() : null);
            tBase.GetEngineState().IsInEngineStartup = false;
            tBase.GetEngineState().IsStarted = true;
            if (tThing != null && tThing.GetBaseThing().StatusLevel == 4)
                tThing.GetBaseThing().StatusLevel = 0;
            eventPluginStarted?.Invoke(tBase.GetEngineName());
        }

        private static readonly object lockStartEngine = new ();
        /// <summary>
        /// Starts the engine with new URL.
        /// </summary>
        /// <param name="pChannel">The p channel.</param>
        /// <param name="pEngines">The p engines.</param>
        internal static void StartEngineWithNewUrl(TheChannelInfo pChannel, string pEngines)
        {
            lock (lockStartEngine)
            {
                List<string> DiscoveredStationRoles = TheCommonUtils.CStringToList(pEngines, ';');
                string tJustSubscribe = "";
                foreach (string tStationRole in DiscoveredStationRoles)
                {
                    bool FoundEngine = false;
                    string tTargetStation = TheBaseAssets.MyScopeManager.RemoveScopeID(tStationRole, false, TheBaseAssets.MyServiceHostInfo.AllowForeignScopeIDRouting || TheBaseAssets.MyServiceHostInfo.IsCloudService);

                    if (!"SCOPEVIOLATION".Equals(tTargetStation))
                    {
                        if ((TheBaseAssets.MyServiceHostInfo.IgnoredEngines == null || !TheBaseAssets.MyServiceHostInfo.IgnoredEngines.Contains(tTargetStation)) &&
                        !((TheBaseAssets.MyServiceHostInfo.FallbackToSimulation && !TheBaseAssets.MyServiceHostInfo.SimulatedEngines.Contains(tTargetStation)) || (!TheBaseAssets.MyServiceHostInfo.FallbackToSimulation && TheBaseAssets.MyServiceHostInfo.SimulatedEngines.Contains(tTargetStation))))
                        {
                            IBaseEngine tBase = TheThingRegistry.GetBaseEngine(tTargetStation);
                            if (tBase != null && !tBase.GetEngineState().IsLiveEngine)
                            {
                                FoundEngine = true;
                                if (tBase.StartEngine(pChannel))
                                    eventNewURLReceived?.Invoke(new TSM(tTargetStation, pChannel.TargetUrl));
                                tBase.Subscribe();
                            }
                        }
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4171, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCDEngines", $"Subscription to {tStationRole} rejected as part of Ignored or Simulation setting", eMsgLevel.l2_Warning));
                            continue;
                        }
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4171, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCDEngines", $"Foreign Scope rejected for incoming Engine Subscription {tStationRole}", eMsgLevel.l2_Warning));
                        continue;
                    }
                    if (!FoundEngine && TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay)
                    {
                        if (tJustSubscribe.Length > 0)
                            tJustSubscribe += ";";
                        tJustSubscribe += tStationRole; //was tTargetStation which is without scope! scope must be there
                    }
                }
                if (TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay && !string.IsNullOrEmpty(tJustSubscribe) && pChannel != null)
                {
                    TheQueuedSender tQ = TheQueuedSenderRegistry.GetSenderByGuid(pChannel.cdeMID);
                    if (tQ != null)
                        tQ.Subscribe(tJustSubscribe);
                }
            }
        }

        /// <summary>
        /// News the station found.
        /// </summary>
        /// <param name="pOriginator">The p originator.</param>
        /// <param name="pURL">The p URL.</param>
        /// <param name="DiscoveredStationRoles">The discovered station roles.</param>
        private static void NewStationFound(Guid pOriginator, string pURL, List<string> DiscoveredStationRoles)
        {
            List<string> tEngs = TheThingRegistry.GetEngineNames(true);
            TheQueuedSender tQ = TheQueuedSenderRegistry.GetSenderByGuid(pOriginator);
            TheChannelInfo tInfo = null;
            string tTargetUrl = null;
            foreach (string aStationRole in DiscoveredStationRoles)
            {
                var tStationRole = TheCommonUtils.cdeSplit(aStationRole, ":;:", false, false);
                string tTargetStation = TheBaseAssets.MyScopeManager.RemoveScopeID(tStationRole[0], false, true);
                if ((tEngs.Contains(tTargetStation) && (TheBaseAssets.MyServiceHostInfo.IgnoredEngines == null || !TheBaseAssets.MyServiceHostInfo.IgnoredEngines.Contains(tTargetStation))) &&
                    !((TheBaseAssets.MyServiceHostInfo.FallbackToSimulation && !TheBaseAssets.MyServiceHostInfo.SimulatedEngines.Contains(tTargetStation)) || (!TheBaseAssets.MyServiceHostInfo.FallbackToSimulation && TheBaseAssets.MyServiceHostInfo.SimulatedEngines.Contains(tTargetStation))))
                {
                    IBaseEngine tBase = TheThingRegistry.GetBaseEngine(tTargetStation);
                    if (tBase != null && !tBase.GetEngineState().IsUnloaded)
                    {
                        if (tTargetUrl == null)
                        {
                            Uri tUri = TheCommonUtils.CUri(pURL, false);
                            tTargetUrl = tUri.Scheme + "://" + tUri.Host;
                            if (tUri.Port != 80)
                                tTargetUrl += ":" + tUri.Port;
                        }
                        if (tInfo == null)
                        {
                            cdeSenderType tS = cdeSenderType.CDE_SERVICE; //Found UPnP Devices are ALWAYS Services!
                            tInfo = new TheChannelInfo(pOriginator, tS, tTargetUrl);
                            tInfo.SetRealScopeID(tStationRole.Length > 1 ? TheBaseAssets.MyScopeManager.GetRealScopeIDFromEasyID(tStationRole[1]) : TheBaseAssets.MyScopeManager.ScopeID);        //GRSI: rare
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(4171, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("TheCDEngines", string.Format("Starting/Adding URL: {0} for Engine {1}", tTargetUrl, tTargetStation), eMsgLevel.l7_HostDebugMessage));
                        tBase.StartEngine(tInfo);
                    }
                }
            }
            if (TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay && tQ != null)
                tQ.CombineSubscriptions(DiscoveredStationRoles, out _);
        }

        /// <summary>
        /// Stops all engines.
        /// </summary>
        internal static void StopAllEngines()
        {
            if (TheBaseAssets.MySYSLOG != null)
                TheBaseAssets.MySYSLOG.WriteToLog(416, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCDEngines", "STOPPING all Engines", eMsgLevel.l7_HostDebugMessage));

            TheBaseAssets.MasterSwitch = false;
            if (TheBaseAssets.MyApplication != null && TheBaseAssets.MyApplication.MyCommonDisco != null)
                TheBaseAssets.MyApplication.MyCommonDisco.UnRegisterNewMatch(NewStationFound);
            foreach (IBaseEngine tBase in TheThingRegistry.GetBaseEngines(true))
            {
                if (tBase != null)
                    tBase.StopEngine();
            }

            if (eventEngineShutdown != null)
            {
                try
                {
                    eventEngineShutdown();
                }
                catch (Exception e)
                {
                    if (TheBaseAssets.MySYSLOG != null) TheBaseAssets.MySYSLOG.WriteToLog(4135, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCDEngines", "Some Plugin Crashed during eventEngineShutdown event", eMsgLevel.l2_Warning, e.ToString()));
                }
            }
            if (TheBaseAssets.MySYSLOG != null)
                TheBaseAssets.MySYSLOG.WriteToLog(4136, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCDEngines", "All Engines STOPPED", eMsgLevel.l7_HostDebugMessage));
        }

        /// <summary>
        /// Sinks the engine is ready.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="pReady">The p ready.</param>
        private static void sinkEngineIsReady(ICDEThing sender, object pReady)
        {
            if (pReady != null)
            {
                if (MyIStorageService != null && MyAccessStats == null && TheBaseAssets.MyServiceHostInfo.TrackAccess)
                {
                    MyAccessStats = new TheStorageMirror<TheAccessStatistics>(MyIStorageService)
                    {
                        AllowFireUpdates = false,
                        IsRAMStore = false,
                        IsCachePersistent = false
                    };
                    MyAccessStats.CreateStore("AccessStatistics", "Shows statistics of C-DEngine Usage", null, true, TheBaseAssets.MyServiceHostInfo.IsNewDevice);
                    MyAccessStats.InitializeStore(TheBaseAssets.MyServiceHostInfo.IsNewDevice);
                }
                if (TheBaseAssets.MySession != null)
                    TheBaseAssets.MySession.InitStateManager();
                if (TheBaseAssets.MyServiceHostInfo.AreAllEnginesStarted)
                {
                    bool tAreAllReady = AreAllEnginesReady();
                    if (LastAllEngineReadiness != tAreAllReady && eventAllEnginesReady != null)
                        eventAllEnginesReady(tAreAllReady);
                    LastAllEngineReadiness = tAreAllReady;
                }
            }
        }
        /// <summary>
        /// The last all engine readiness
        /// </summary>
        private static bool LastAllEngineReadiness;

        /// <summary>
        /// Updates the costing.
        /// </summary>
        /// <param name="pMessage">The p message.</param>
        internal static void updateCosting(TSM pMessage)
        {
            if (TheBaseAssets.MyServiceHostInfo.EnableCosting && !string.IsNullOrEmpty(pMessage.ENG))
            {
                IBaseEngine tBase = TheThingRegistry.GetBaseEngine(pMessage.ENG);
                if (tBase != null)
                    tBase.UpdateCosting(pMessage);
            }
        }

        /// <summary>
        /// Finds the plugins.
        /// </summary>
        private static void FindPlugins()
        {
            string UpdateDirectory = TheBaseAssets.MyServiceHostInfo.BaseDirectory;
            if (string.IsNullOrEmpty(UpdateDirectory)) return;
            DirectoryInfo di = new (UpdateDirectory);
            try
            {
#if !CDE_NET35 && !CDE_STANDARD //Child Domains not supported on NET35
                //Create separate AppDomain to probe for Plugin Types
                var settings = new AppDomainSetup
                {
                    ApplicationBase = TheCommonUtils.GetCurrentAppDomainBaseDirWithTrailingSlash() // AppDomain.CurrentDomain.BaseDirectory,
                };
                if (!TheBaseAssets.MyServiceHostInfo.IsCloudService && TheBaseAssets.MyServiceHostInfo.cdeHostingType!=cdeHostType.IIS)
                {
                    System.Security.Policy.Evidence adevidence = AppDomain.CurrentDomain.Evidence;
                    var childDomain = AppDomain.CreateDomain(Guid.NewGuid().ToString(), adevidence, settings);
                    childDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(CurrentDomain_ReflectionOnlyAssemblyResolve);
                    var handle = Activator.CreateInstance(childDomain,
                    typeof(ReferenceLoader).Assembly.FullName,
                    typeof(ReferenceLoader).FullName,
                    false, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, null, CultureInfo.CurrentCulture, new object[0]);
                    var loader = (ReferenceLoader)handle.Unwrap();
                    ProcessDirectory(di, "", UpdateDirectory, loader);
                    AppDomain.Unload(childDomain);
                }
                else
#endif
                {
                    AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(CurrentDomain_ReflectionOnlyAssemblyResolve);
                    ProcessDirectory(di, "", UpdateDirectory, new ReferenceLoader());
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(418, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCDEngines", "DeserializeStationInfo:FindPlugins", eMsgLevel.l1_Error, e.ToString()));
            }
        }

        static Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = AppDomain.CurrentDomain.ApplyPolicy(args.Name);
            return System.Reflection.Assembly.ReflectionOnlyLoad(name);
        }

        static bool _platformDoesNotSupportReflectionOnlyLoadFrom;
        internal class ReferenceLoader : MarshalByRefObject
        {
            public List<string> ScanForCDEPlugins(string assemblyPath, out TSM resTSM) //,ref Dictionary<string, Type> pCDEPlugins)
            {
                resTSM = null;
                List<string> mList = new ();

                Assembly tAss = null;
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
                    tAss = Assembly.LoadFrom(assemblyPath);
                }

                if (tAss != null)
                {
                    var CDEPlugins = from t in tAss.GetTypes()
                                     let ifs = t.GetInterfaces()
                                     where ifs != null && ifs.Length > 0 && (ifs.Any(s => s.Name == "ICDEPlugin"))
                                     select new { Type = t, t.Namespace, t.Name, t.FullName };
                    if (CDEPlugins == null || !CDEPlugins.Any())
                    {
                        if (!assemblyPath.ToLower().Contains(".resources."))
                        {
                            string tPls = "";
                            foreach (Type ttt in tAss.GetTypes())
                            {
                                if (tPls.Length > 0) tPls += "\n\r,";
                                tPls += ttt.FullName;
                            }
                            resTSM = new TSM("TheCDEngines", $"ProcessDirectory: File {assemblyPath} - found but did not contain C-DEngine Plug-In Interface", eMsgLevel.l2_Warning, tPls);
                        }
                    }
                    else
                    {
                        resTSM = new TSM("TheCDEngines", $"ProcessDirectory: Assembly {assemblyPath} found and added Plugins:", "");
                        foreach (var Plugin in CDEPlugins)
                        {
                            if (!Plugin.Type.IsAbstract)
                            {
                                var ints = Plugin.Type.GetInterfaces();
                                string inters = "";
                                foreach (var tI in ints)
                                {
                                    if (tI.Name != "ICDEThing" && tI.Name != "ICDEPlugin")
                                    {
                                        if (inters.Length > 0)
                                            inters += ":";
                                        inters += tI.Name;
                                    }
                                }
                                mList.Add($"{Plugin.FullName};{assemblyPath}{(string.IsNullOrEmpty(inters) ? "" : $";{inters}")}");
                                resTSM.PLS += $"{Plugin.FullName};";
                            }
                        }
                    }
                }
                else
                {
                    resTSM = new TSM("TheCDEngines", $"ProcessDirectory: File {assemblyPath} - failed to load assembly", eMsgLevel.l1_Error, "");
                }
                return mList;
            }
        }
        private static void ProcessDirectory(DirectoryInfo di, string relativeCurrentDir, string BaseDir, ReferenceLoader pLoader)
        {
            if (relativeCurrentDir.ToLower().Contains("clientbin"))
                return;
            FileInfo[] fileInfo = di.GetFiles();



            foreach (FileInfo fiNext in fileInfo)
            {
                if (fiNext.Extension.ToUpper().Equals(".CDL")
                    || (
                            (
                                (fiNext.Name.Length >= 4 && fiNext.Name.Substring(0, 4).Equals("CDMy"))
                                || (fiNext.Name.Length >= 5 && fiNext.Name.Substring(0, 5).Equals("C-DMy"))
                            )
                            && fiNext.Extension.ToUpper().Equals(".DLL")
                       ))
                {
                    try
                    {
                        if (fiNext.Name.ToLower().Contains(".resources."))
                            continue;
                        if (Path.DirectorySeparatorChar == '/')//TheCommonUtils.IsMono())
                        {
                            // CODE REVIEW: Can we switch this to use Path.Combine etc. function to avoid this manipulation entirely? Or are the heterogeneous mesh scenarios where we still need to do this regardless?
                            BaseDir = BaseDir.Replace('\\', '/');
                            relativeCurrentDir = relativeCurrentDir.Replace('\\', '/');
                            if (!BaseDir.EndsWith("/") && !relativeCurrentDir.StartsWith("/"))
                                relativeCurrentDir = "/" + relativeCurrentDir;

                        }
                        if (TheBaseAssets.MyServiceHostInfo.IsIsolated)
                        {
                            if (!fiNext.Name.Equals(TheBaseAssets.MyServiceHostInfo.IsoEngine, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                        else
                        {
                            if (TheBaseAssets.MyServiceHostInfo.IsoEngines.Any(s => s.ToLower().StartsWith(fiNext.Name.ToLower())))
                            {
                                TheBaseAssets.MyServiceHostInfo.IsoEnginesToLoad.Add(TheBaseAssets.MyServiceHostInfo.IsoEngines.First(s => s.ToLower().StartsWith(fiNext.Name.ToLower())));
                                continue;
                            }
                        }
                        if (!TheCommonUtils.IsDeviceSenderType(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType) && !TheBaseAssets.MyCodeSigner.IsTrusted(BaseDir + relativeCurrentDir + fiNext.Name)) // CODE REVIEW: Use Path.Combine here? //IDST-OK: No CodeSigning Trust Check on Devices
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(418, new TSM("TheCDEngines", $"Auth Failed on Plugin {fiNext.Name} - ({BaseDir + relativeCurrentDir + fiNext.Name}) it does not have correct signature", eMsgLevel.l1_Error));
                            continue;
                        }
                        List<string> tL = pLoader.ScanForCDEPlugins(BaseDir + relativeCurrentDir + fiNext.Name, out TSM tTSM); 
                        foreach (var tLt in tL)
                        {
                            var tPs = tLt.Split(';');
                            if (!TheBaseAssets.MyCDEPlugins.ContainsKey(tPs[0]))
                                TheBaseAssets.MyCDEPlugins.Add(tPs[0], new ThePluginInfo() { PluginPath = tPs[1], ServiceName = tPs[0], Categories = tPs.Length > 2 ? (tPs[2].Split(':').ToList()) : null });
                        }
                        if (tTSM != null)
                        {
                            tTSM.ORG = "AppDomainLoader";
                            TheBaseAssets.MySYSLOG.WriteToLog(418, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : tTSM);
                        }
                    }
                    catch (ReflectionTypeLoadException eeee)
                    {
                        Exception[] tEx = eeee.LoaderExceptions;
                        string res = eeee.ToString();
                        bool bOldICDE = false;
                        foreach (Exception e in tEx)
                        {
                            if (e.Message.Contains("'Init'"))
                            {
                                bOldICDE = true;
                            }
                            res += $" (Loader Exception: {e})";
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(418, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", string.Format("ProcessDirectory:LoadAssembly {0} failed with ReflectionTypeLoadException", fiNext.Name), eMsgLevel.l1_Error, res));
                        if (bOldICDE)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4110, new TSM("TheCDEngines", "StartEngines", eMsgLevel.l1_Error, string.Format("Assembly {0} appears to be a plug-in for an earlier C-DEngine and is no longer supported.", fiNext.Name)));
                        }
                    }
                    catch (Exception eee)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(418, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", string.Format("ProcessDirectory:LoadAssembly {0} failed", fiNext.Name), eMsgLevel.l1_Error, eee.ToString()));
                    }
                }
            }

            DirectoryInfo[] dirs = di.GetDirectories("*.*");
            foreach (DirectoryInfo dir in dirs)
            {
                if (dir.Name == "obj") continue;
                ProcessDirectory(dir, relativeCurrentDir + dir.Name + "\\", BaseDir, pLoader);
            }
        }

        #region Access Tracking
        /// <summary>
        /// The lock sink update stats
        /// </summary>
        private static readonly object lockSinkUpdateStats = new ();
        /// <summary>
        /// Sinks the update stats.
        /// </summary>
        /// <param name="pResponse">The p response.</param>
        private static void sinkUpdateStats(TheStorageMirror<TheAccessStatistics>.StoreResponse pResponse)
        {
            if (TheCommonUtils.cdeIsLocked(lockSinkUpdateStats)) return;

            if (pResponse.HasErrors && !pResponse.ErrorMsg.Contains("returned: NULL") && !pResponse.ErrorMsg.Contains("No Records Found")) return;
            lock (lockSinkUpdateStats)
            {
                bool IsTrackingUser = pResponse.SQLFilter.Contains("UserID");

                if (pResponse.MyRecords == null || pResponse.MyRecords.Count == 0)
                {
                    TheAccessStatistics tStat = new () { RelayNode = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false) };
                    if (IsTrackingUser)
                    {
                        int pos = pResponse.SQLFilter.IndexOf("UserID", StringComparison.Ordinal);
                        int pos2 = pResponse.SQLFilter.IndexOf("'", pos + 8, StringComparison.Ordinal);
                        tStat.UserID = pResponse.SQLFilter.Substring(pos + 8, pos2 - 8 - pos);
                        tStat.RequestCounter = 1;
                    }
                    else
                    {
                        tStat.UserID = "portal";
                        tStat.RestartCounter = 1;
                    }
                    MyAccessStats.AddAnItem(tStat);
                }
                else
                {
                    if (IsTrackingUser)
                        pResponse.MyRecords[0].RequestCounter++;
                    else
                        pResponse.MyRecords[0].RestartCounter++;
                    MyAccessStats.UpdateItem(pResponse.MyRecords[0]);
                }
            }
        }

        /// <summary>
        /// Tracks the request.
        /// </summary>
        /// <param name="pReq">Returns records for RelayNode=cdeRealPage</param>
        internal static void TrackRequest(TheRequestData pReq)
        {
            if (MyAccessStats != null)
                MyAccessStats.GetRecords("RelayNode='" + pReq.cdeRealPage + "'", 1, sinkUpdateStats, false);
        }

        /// <summary>
        /// Tracks the node by the primary station URL
        /// </summary>
        internal static void TrackNode()
        {
            if (MyAccessStats != null)
                MyAccessStats.GetRecords("RelayNode='" + TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false) + "'", 1, sinkUpdateStats, false);
        }

        /// <summary>
        /// Tracks the user by primary station url and userid
        /// </summary>
        /// <param name="pUserID">The p user identifier.</param>
        internal static void TrackUser(string pUserID)
        {
            if (MyAccessStats != null)
                MyAccessStats.GetRecords("RelayNode='" + TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false) + "' and UserID='" + pUserID + "'", 1, sinkUpdateStats, false);
        }
        #endregion
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class EngineAssetInfoAttribute : Attribute
    {
        public string EngineName;
        public string FriendlyName;
        public Type EngineType;
        public string EngineID;
        public double Version;
        public double CDEMinVersion;

        public string[] ManifestFiles;
        public cdePlatform[] Platforms;

        public bool AcceptsFilePush;
        public eThingCaps[] Capabilities;
        public bool IsService;

        public string LongDescription;
        public double Price;
        public string HomeUrl;
        public string IconUrl;
        public string Developer;
        public string DeveloperUrl;
        public string[] Categories;
        public string CopyRights;

        public bool AllowIsolation;
        public bool AllowNodeHop;

        public static bool ApplyEngineAssetAttributes(Type pluginType, IBaseEngine tIBase)
        {
            bool bResult = false;

            EngineAssetInfoAttribute info = null;
            try
            {
                info = (EngineAssetInfoAttribute)pluginType.GetCustomAttributes(typeof(EngineAssetInfoAttribute), true).FirstOrDefault();
            }
            catch { 
                //ignored
            }

            if (info != null)
            {
                if (!string.IsNullOrEmpty(info.EngineName))
                {
                    tIBase.SetEngineName(info.EngineName);
                }
                else
                {
                    tIBase.SetEngineName(pluginType.FullName);
                }
                if (!string.IsNullOrEmpty(info.FriendlyName))
                {
                    tIBase.SetFriendlyName(info.FriendlyName);
                }
                if (info.EngineType != null)
                {
                    tIBase.SetEngineType(info.EngineType);
                }
                else
                {
                    tIBase.SetEngineType(pluginType);
                }
                var engineId = TheCommonUtils.CGuid(info.EngineID);
                if (engineId != Guid.Empty)
                {
                    tIBase.SetEngineID(engineId);
                }

                tIBase.SetVersion(info.Version);
                tIBase.SetCDEMinVersion(info.CDEMinVersion);


                if (info.AcceptsFilePush)
                {
                    tIBase.GetEngineState().IsAcceptingFilePush = true;
                }
                if (info.Capabilities?.Length > 0)
                {
                    foreach (var cap in info.Capabilities)
                    {
                        tIBase.AddCapability(cap);
                    }
                }

                tIBase.SetIsolationFlags(info.AllowIsolation, info.AllowNodeHop);

                if (info.ManifestFiles?.Length > 0)
                {
                    tIBase.AddManifestFiles(info.ManifestFiles.ToList());
                }
                if (info.Platforms?.Length > 0)
                {
                    tIBase.AddPlatforms(info.Platforms.ToList());
                }

                if (info.IsService)
                {
                    tIBase.SetEngineService(true);
                }

                tIBase.SetPluginInfo(info.LongDescription, info.Price, info.HomeUrl, info.IconUrl, info.Developer, info.DeveloperUrl, info.Categories?.ToList(), info.CopyRights);

                bResult = true;
            }
            return bResult;
        }

        internal static bool ApplyDeviceTypeAttributes(Type pluginType, IBaseEngine tIBase)
        {
            var deviceTypeInfos = new Dictionary<string, TheDeviceTypeInfo>();

            tIBase.GetDeviceTypes()?.Select(dt => deviceTypeInfos[dt.DeviceType] = dt);

            // Now read device type attributes from all classes that implement ICDEThing
            try
            {
                var thingTypes = pluginType.Assembly.GetTypes().Where(t => !t.IsAbstract && t.GetInterfaces().FirstOrDefault(itf => itf == typeof(ICDEThing)) != null);
                foreach (var thingType in thingTypes)
                {
                    foreach (var dta in thingType.GetCustomAttributes(typeof(DeviceTypeAttribute), true).Select(dt => dt as DeviceTypeAttribute))
                    {
                        if (string.IsNullOrEmpty(dta?.DeviceType))
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4138, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Plug-in has no DeviceType in the device type attribute declaration", eMsgLevel.l1_Error, $"{thingType.FullName}: {dta?.ToString()}"));

                            continue;
                        }
                        var dt = new TheDeviceTypeInfo(dta);
                        var thingTypeProps = thingType.GetProperties();
                        ApplyConfigPropertyAttributes(thingType, dt, thingTypeProps);
                        ApplySensorPropertyAttributes(thingType, dt, thingTypeProps);
                        deviceTypeInfos[dt.DeviceType] = dt;
                    }
                }
            }
            catch { 
                //ignored
            }
            if (deviceTypeInfos.Any())
            {
                tIBase.SetDeviceTypes(deviceTypeInfos.Values.ToList());
                return true;
            }
            return false;
        }

        private static void ApplyConfigPropertyAttributes(Type thingType, TheDeviceTypeInfo dt, PropertyInfo[] thingTypeProps)
        {
            foreach (var prop in thingTypeProps)
            {
                ConfigPropertyAttribute configAttribute = null;
                try
                {
                    configAttribute = prop.GetCustomAttributes(typeof(ConfigPropertyAttribute), true).FirstOrDefault() as ConfigPropertyAttribute;
                }
                catch { 
                    //ignored
                }
                if (configAttribute != null)
                {
                    dt.ConfigProperties ??= new List<TheThing.TheConfigurationProperty>();
                    dt.ConfigProperties.Add(new TheThing.TheConfigurationProperty(prop.Name, prop.PropertyType, configAttribute));
                }
            }
            try
            {
                foreach (var configAttribute in thingType.GetCustomAttributes(typeof(ConfigPropertyAttribute), true).Select(a => a as ConfigPropertyAttribute))
                {
                    if (configAttribute != null)
                    {
                        if (!string.IsNullOrEmpty(configAttribute.NameOverride))
                        {
                            if (configAttribute.cdeT != ePropertyTypes.NOCHANGE)
                            {
                                dt.ConfigProperties ??= new List<TheThing.TheConfigurationProperty>();
                                dt.ConfigProperties.Add(new TheThing.TheConfigurationProperty(configAttribute.NameOverride, null, configAttribute));
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(4138, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Plug-in has ConfigProperty attribute without cdeT", eMsgLevel.l1_Error, $"{thingType.FullName}: {configAttribute.NameOverride}"));
                            }
                        }
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4138, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Plug-in has ConfigProperty attribute without NameOverride", eMsgLevel.l1_Error, thingType.FullName));
                        }
                    }
                }
            }
            catch { 
                //ignored
            }
            foreach (var prop in thingTypeProps)
            {
                SensorPropertyAttribute sensorAttribute = null;
                try
                {
                    sensorAttribute = prop.GetCustomAttributes(typeof(SensorPropertyAttribute), true).FirstOrDefault() as SensorPropertyAttribute;
                }
                catch { 
                    //ignored
                }
                if (sensorAttribute != null)
                {
                    dt.SensorProperties ??= new List<TheThing.TheSensorPropertyMeta>();
                    dt.SensorProperties.Add(new TheThing.TheSensorPropertyMeta(prop.Name, prop.PropertyType, sensorAttribute));
                }
            }
        }

        private static void ApplySensorPropertyAttributes(Type thingType, TheDeviceTypeInfo dt, PropertyInfo[] thingTypeProps)
        {
            foreach (var prop in thingTypeProps)
            {
                SensorPropertyAttribute sensorAttribute = null;
                try
                {
                    sensorAttribute = prop.GetCustomAttributes(typeof(SensorPropertyAttribute), true).FirstOrDefault() as SensorPropertyAttribute;
                }
                catch { 
                    //ignored
                }
                if (sensorAttribute != null)
                {
                    dt.SensorProperties ??= new List<TheThing.TheSensorPropertyMeta>();
                    dt.SensorProperties.Add(new TheThing.TheSensorPropertyMeta(prop.Name, prop.PropertyType, sensorAttribute));
                }
            }
            try
            {
                foreach (var sensorAttribute in thingType.GetCustomAttributes(typeof(SensorPropertyAttribute), true).Select(a => a as SensorPropertyAttribute))
                {
                    if (sensorAttribute != null)
                    {
                        if (!string.IsNullOrEmpty(sensorAttribute.NameOverride))
                        {
                            if (sensorAttribute.cdeT != ePropertyTypes.NOCHANGE)
                            {
                                dt.SensorProperties ??= new List<TheThing.TheSensorPropertyMeta>();
                                dt.SensorProperties.Add(new TheThing.TheSensorPropertyMeta(sensorAttribute.NameOverride, null, sensorAttribute));
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(4138, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Plug-in has SensorProperty attribute without cdeT", eMsgLevel.l1_Error, $"{thingType.FullName}: {sensorAttribute.NameOverride}"));
                            }
                        }
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4138, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Plug-in has SensorProperty attribute without NameOverride", eMsgLevel.l1_Error, thingType.FullName));
                        }
                    }
                }
            }
            catch { 
                //ignored
            }
        }

        private static void ApplySensorPropertyExtensionAttributes(Type thingType, TheDeviceTypeInfo dt, PropertyInfo[] thingTypeProps)
        {
            foreach (var prop in thingTypeProps)
            {
                SensorPropertyExtensionAttribute sensorExtensionAttribute = null;
                try
                {
                    sensorExtensionAttribute = prop.GetCustomAttributes(typeof(SensorPropertyExtensionAttribute), true).FirstOrDefault() as SensorPropertyExtensionAttribute;
                }
                catch { 
                    //ignored
                }
                if (sensorExtensionAttribute != null)
                {
                    var sensorProp = dt.SensorProperties.FirstOrDefault(sp => sp.Name == prop.Name);
                    if (sensorProp != null)
                    {
                        sensorProp.ExtensionData ??= new Dictionary<string, object>();
                        sensorProp.ExtensionData[sensorExtensionAttribute.Name] = sensorExtensionAttribute.Value;
                    }
                }
            }
            try
            {
                foreach (var sensorExtensionAttribute in thingType.GetCustomAttributes(typeof(SensorPropertyExtensionAttribute), true).Select(a => a as SensorPropertyExtensionAttribute))
                {
                    if (sensorExtensionAttribute != null)
                    {
                        if (!string.IsNullOrEmpty(sensorExtensionAttribute.NameOverride))
                        {
                            var sensorProp = dt.SensorProperties.FirstOrDefault(sp => sp.Name == sensorExtensionAttribute.NameOverride);
                            if (sensorProp != null)
                            {
                                sensorProp.ExtensionData ??= new Dictionary<string, object>();
                                sensorProp.ExtensionData[sensorExtensionAttribute.Name] = sensorExtensionAttribute.Value;
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(4138, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Plug-in has SensorPropertyExtension attribute without a SensorProperty attribute", eMsgLevel.l1_Error, $"{thingType.FullName}: {sensorExtensionAttribute.NameOverride}"));
                            }
                        }
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4138, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDEngines", "Plug-in has SensorPropertyExtension attribute without NameOverride", eMsgLevel.l1_Error, thingType.FullName));
                        }
                    }
                }
            }
            catch { 
                //ignored
            }
        }

    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class DeviceTypeAttribute : Attribute
    {
        public string DeviceType;
        public eThingCaps[] Capabilities;
        public string Description;
        public string EngineName;
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
    public class ConfigPropertyAttribute : Attribute
    {
        public ePropertyTypes cdeT = ePropertyTypes.NOCHANGE;
        public object DefaultValue;
        public bool Secure;
        public bool Generalize;
        public bool IsThingReference { get; set; }
        public bool Required;
        public string Description;
        public string FriendlyName;
        public string SemanticTypes;
        public string Units;
        public double RangeMin;
        public double RangeMax;
        public string NameOverride;
    }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
    public class SensorPropertyAttribute : Attribute
    {
        public ePropertyTypes cdeT = ePropertyTypes.NOCHANGE;
        public string SourceType;
        public string Units;
        public string SourceUnits;
        public double? RangeMin;
        public double? RangeMax;
        public string Description;
        public string FriendlyName;
        public string SemanticTypes;
        public string NameOverride;
    }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
    public class SensorPropertyExtensionAttribute : Attribute
    {
        public string Name;
        public string Value;
        public string NameOverride;
    }
}

