// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

#region USING
using nsCDEngine.Communication;
using nsCDEngine.Communication.HttpService;
using nsCDEngine.Discovery;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ISM;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable UseNullPropagation
#endregion


namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// Essential C-DEngine Management function used by ISM
    /// </summary>
    public interface ICDEManagement
    {
        /// <summary>
        /// Requests Shutdown of all internal Threads and the C-DEngine Host
        /// </summary>
        /// <param name="force">If true the shutdown will be forced without any wait-time</param>
        /// <param name="waitIfPending">If true, the shutdown will wait for pending processes</param>
        void Shutdown(bool force, bool waitIfPending);
        /// <summary>
        /// Main Method to start the C-DEngine Based Application on the current Node
        /// </summary>
        /// <param name="pPlug"></param>
        /// <param name="CmdArgs"></param>
        /// <returns></returns>
        bool StartBaseApplication(ICDEPlugin pPlug, IDictionary<string, string> CmdArgs);
    }

    /// <summary>
    /// This class is the essential class hosting all C-DEngine Services.
    /// Every Application, Service or other host has to instantiate this class to Activate the C-DEngine
    /// </summary>
    public class TheBaseApplication : ICDEManagement, IDisposable
    {
        /// <summary>
        /// The UPnP DIscovery Root object of this Node
        /// </summary>
        public ICommonDisco MyCommonDisco;
        /// <summary>
        /// The User Manager Object of the C-DEngine's host
        /// </summary>
        public TheUserManager MyUserManager;
        /// <summary>
        /// The Root of the ISM Manager (Intelligent Service Management)
        /// </summary>
        //public TheISMManager MyISMRoot;
        public ICDEISManager MyISMRoot;
        /// <summary>
        /// Constructor of TheBaseApplication class
        /// It records this Application in TheBaseAssets.MyApplication for future static reference
        /// </summary>
        public TheBaseApplication()
        {
            TheBaseAssets.MyApplication = this;
        }

        /// <summary>
        /// Disposes this object
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }
        /// <summary>
        /// Allows to run code during the dispose function
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
        }

        #region ICDEManagement Interface
        /// <summary>
        /// Override to initalize the app with custom values but still call the base!
        /// </summary>
        /// <param name="pPlug">A pre-instantiated plugin Service</param>
        /// <param name="CmdArgs">Additional Parameters of the Application</param>
        /// <returns>True if all went well during startup</returns>
        public virtual bool StartBaseApplication(ICDEPlugin pPlug, IDictionary<string, string> CmdArgs)
        {
            return StartBaseApplication2(pPlug == null ? null : new List<ICDEPlugin> { pPlug }, CmdArgs);
        }
        /// <summary>
        /// Override to initalize the app with custom values but still call the base!
        /// </summary>
        /// <param name="pPlugInLst">List if ICDEPlugin instances</param>
        /// <param name="ArgList">Additional Parameters of the Application</param>
        /// <returns>True if all went well during startup</returns>
        public virtual bool StartBaseApplication2(List<ICDEPlugin> pPlugInLst, IDictionary<string, string> ArgList)
        {
            if (TheBaseAssets.MasterSwitch) return false;   //SECURITY REVIEW: do not allow to call StartBaseApplication twice
            TheBaseAssets.MasterSwitch = true;
            if (TheBaseAssets.MyServiceHostInfo == null)
            {
                TheBaseAssets.MasterSwitch = false;
                TheSystemMessageLog.ToCo("MyServiceHostInfo is not set! Exiting...");   //Will becalled because if MyServiceHostInfo==null TOCO will be called
                return false;
            }
            if (TheBaseAssets.CryptoLoadMessage != null)
            {
                TheBaseAssets.MasterSwitch = false;
                TheSystemMessageLog.ToCo($"Security initialization failed with {TheBaseAssets.CryptoLoadMessage}. Exiting...");
                return false;
            }
            try
            {
                TheBaseAssets.IsStarting = true;

                //AppDomain.CurrentDomain.UnhandledException += MyUnhandledExceptionHandler;

                if (ArgList == null)
                {
                    ArgList = new Dictionary<string, string>();    //DIC-Allowed!
                }
                if (!TheBaseAssets.MySettings.ReadAllAppSettings(ref ArgList))
                {
                    TheSystemMessageLog.ToCo("Not reading app.config: not supported by platform."); //Will never be called!
                }
                if (!TheBaseAssets.MasterSwitch)
                    return false;
                TheBaseAssets.MyCmdArgs = ArgList; //MyCmdArgs ONLY contains "public" settings coming from app.config or via the host

                MyUserManager = new TheUserManager();
                TheBaseAssets.InitAssets(this);
                if (!TheBaseAssets.MasterSwitch)
                    return false;
                TheBaseAssets.MyScopeManager.RegisterScopeChanged(EventScopeChanged);
                TheBaseAssets.MySYSLOG.WriteToLog(4140, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseApplication", "Assets and SystemLog Initialized", eMsgLevel.l3_ImportantMessage));

                int minWorker = -1, minCompletionPort = -1, maxWorker = -1, maxCompletionPort = -1;
                try
                {
                    System.Threading.ThreadPool.GetMinThreads(out minWorker, out minCompletionPort);
                    System.Threading.ThreadPool.GetMaxThreads(out maxWorker, out maxCompletionPort);
                }
                catch { };

#if CDE_STANDARD
                var largeObjectHeapCompactionMode = System.Runtime.GCSettings.LargeObjectHeapCompactionMode;
#else
                const string largeObjectHeapCompactionMode = "n/a";
#endif
                TheBaseAssets.MySYSLOG.WriteToLog(4144, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ContentService, $"CLR Info", eMsgLevel.l4_Message, $"Server GC: {System.Runtime.GCSettings.IsServerGC}. GC Latency Mode: {System.Runtime.GCSettings.LatencyMode}. LOHC Mode: {largeObjectHeapCompactionMode}. Bitness: {System.Runtime.InteropServices.Marshal.SizeOf(typeof(IntPtr)) * 8}. Threads Min W/CP, Max W/CP: {minWorker}/{minCompletionPort},{maxWorker}/{maxCompletionPort}. GC TotalMemory: {GC.GetTotalMemory(false)}"));

                if (!TheBaseAssets.MyActivationManager.InitializeLicenses())
                {
                    Shutdown(true);
                    TheBaseAssets.MySYSLOG.WriteToLog(4145, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseApplication", "Licensing error", eMsgLevel.l3_ImportantMessage));
                    return false;
                }

                //4.304: Workitem #2289 - Removed from OSS Release (as CheckLicense is always returning true in the CoreCrypto Lib
                if (!TheBaseAssets.MyActivationManager.CheckLicense(new Guid("{5737240C-AA66-417C-9B66-3919E18F9F4A}"), "", null, 1, false))
                {
                    if (TheBaseAssets.MyServiceHostInfo.ShutdownOnLicenseFailure)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4146, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseApplication", "Licensing error: no activated license for Thing Service. Shutting down immediately as configured.", eMsgLevel.l2_Warning));
                        return false;
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4147, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseApplication", "Licensing error: no activated license for Thing Service. Will shut down in 60 minutes if not activated.", eMsgLevel.l2_Warning));
                        TheCommonUtils.cdeRunAsync("LicenseCheck", true, (o) =>
                        {
                            TheCommonUtils.TaskDelayOneEye(60 * 60 * 1000, 100).ContinueWith((t) =>
                                {
                                    if (TheBaseAssets.MasterSwitch)
                                    {
                                        if (!TheBaseAssets.MyActivationManager.CheckLicense(new Guid("{5737240C-AA66-417C-9B66-3919E18F9F4A}"), "", null, 1, false))
                                        {
                                            TheBaseAssets.MySYSLOG.WriteToLog(4148, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseApplication", "Licensing error: no activated license for Thing Service. Shutting down.", eMsgLevel.l1_Error));
                                            if (TheBaseAssets.IsInAgentStartup)
                                                MyISMRoot?.Restart(true);
                                            else
                                                Shutdown(true);
                                        }
                                    }
                                });
                        });
                    }
                }


                TheCommCore.MyHttpService = new TheHttpService();
                TheBaseAssets.MySYSLOG.WriteToLog(4141, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseApplication", "HttpService created", eMsgLevel.l3_ImportantMessage));

                if (TheBaseAssets.MyServiceHostInfo.StartISM && MyISMRoot == null && !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.BaseDirectory))
                {
                    MyISMRoot = new TheISMManager { eventShutdownRequired = sinkAppShutdown };
                    MyISMRoot.InitISM("", "", "", TheBaseAssets.MyServiceHostInfo.CurrentVersion, TheBaseAssets.MyServiceHostInfo.ISMScanForUpdatesOnUSB, TheBaseAssets.MyServiceHostInfo.ISMScanOnStartup);
                    TheBaseAssets.MySYSLOG.WriteToLog(4142, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseApplication", "ISM Started", eMsgLevel.l3_ImportantMessage));
                }
                else
                    TheBaseAssets.MyServiceHostInfo.StartISM = false;
                if (!TheBaseAssets.MasterSwitch) return false;

                if (MyCommonDisco == null)
                {
                    if (!TheBaseAssets.MyServiceHostInfo.IsIsolated && (TheCommonUtils.IsHostADevice() || TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Active))
                        TheBaseAssets.MySettings.SetSetting("DISCO_IsClientOnly", "True");
                    MyCommonDisco = new TheCommonDisco(); // Initialized the Discovery Service
                }

                TheCDEngines.eventAllEnginesStarted += sinkEnginesStarted;
                if (!TheCDEngines.StartEngines(pPlugInLst, null))                                           //Starts all SubEngines and Plugins. MyCmdArgs is a copy of the tParas sent to the Init Assets Function and can be used to set parameters for each engine during startup
                {
                    Shutdown(true);
                    TheBaseAssets.MySYSLOG.WriteToLog(4149, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseApplication", "Failed to start engines", eMsgLevel.l3_ImportantMessage));
                    return false;
                }
                return true;
            }
            finally
            {
                TheBaseAssets.IsStarting = false;
            }
        }

        //private void MyUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        //{
        //    if (e?.IsTerminating == true)
        //    {
        //        TheBaseAssets.MySYSLOG.WriteToLog(4150, new BaseClasses.TSM("TheBaseApplication", "Unhandled exception: Calling Shutdown. Process will terminate.", eMsgLevel.l1_Error, e?.ExceptionObject?.ToString()));
        //        Shutdown(true, true);
        //    }
        //}

        void sinkEnginesStarted()
        {
            if (MyCommonDisco != null)
                MyCommonDisco.StartDiscoDevice();   //MSU-OK Starts the Discovery Service -  as Device AND scanning for other relays/services

            string iis = "";
            switch (TheBaseAssets.MyServiceHostInfo.cdeHostingType)
            {
                case cdeHostType.IIS: iis = " (Hosted in IIS)"; break;
                case cdeHostType.ASPCore: iis = " (Hosted in ASP.NET Core)"; break;
            }
            TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM(TheBaseAssets.MyServiceHostInfo.ApplicationName, TheBaseAssets.MyServiceHostInfo.ApplicationTitle + iis + " Started at : " + TheCommonUtils.GetDateTimeString(DateTimeOffset.Now), eMsgLevel.l3_ImportantMessage)); //Log Entry that service has been started
        }

        void sinkAppShutdown(bool ForceExit, string pVersion)
        {
            Shutdown(ForceExit);
        }
        /// <summary>
        /// Shutsdown the C-DEngine and all plugins
        /// Applications can override this function to add custom shutdown code
        /// </summary>
        /// <param name="force">If True and the C-DEngine will Stop or restart the hosting application. If it is hosted in IIS, the Application Pool hosting the app will be Stopped or Restarted depending on the next parameter</param>
        /// <param name="waitIfPending">Waits in case The shutdown is already initiated</param>
        public virtual void Shutdown(bool force, bool waitIfPending = false)
        {
            if (!waitIfPending)
            {
                if (!TheBaseAssets.MasterSwitch || TheCommonUtils.cdeIsLocked(ShutdownLock)) return;                                   //Make sure we dont do this twice
            }
            lock (ShutdownLock)
            {
                if (TheBaseAssets.MasterSwitch)
                {
                    if (TheBaseAssets.IsStarting)
                    {
                        TheBaseAssets.MySYSLOG?.WriteToLog(3, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(TheBaseAssets.MyServiceHostInfo.ApplicationName, "Shutdown was requested, but startup has not finished. Waiting for startup to finish.", eMsgLevel.l3_ImportantMessage));
                        // Ensure we wait for user manager so CUOFS first-run-only settings are applied
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        while (TheBaseAssets.IsStarting && sw.ElapsedMilliseconds < 60000)
                        {
                            Thread.Sleep(100);
                        }
                        if (TheBaseAssets.IsStarting)
                        {
                            TheBaseAssets.MySYSLOG?.WriteToLog(3, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(TheBaseAssets.MyServiceHostInfo.ApplicationName, "Shutdown was requested, but startup has not finished after wait time (60 seconds). Shutting down regardless. Some first-run-only settings may not have been applied.", eMsgLevel.l1_Error));
                        }
                    }
                    if (TheBaseAssets.MyServiceHostInfo.PreShutDownDelay > 0)
                    {
                        var tcsPreHandlers = new TaskCompletionSource<bool>();
                        TheCommonUtils.cdeRunAsync("PreShutdownHandlers", true, (o) =>
                        {
                            TheCDEngines.MyContentEngine?.FireEvent(eEngineEvents.PreShutdown, null, TheBaseAssets.MyServiceHostInfo.PreShutDownDelay, false);
                            tcsPreHandlers.TrySetResult(true);
                        });
                        tcsPreHandlers.Task.Wait(TheBaseAssets.MyServiceHostInfo.PreShutDownDelay);
                    }
                    while (Interlocked.Read(ref TheBaseAssets.DelayShutDownCount) > 0)
                    {
                        TheCommonUtils.SleepOneEye(100, 100);
                    }
                    TheBaseAssets.MySYSLOG?.WriteToLog(3, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(TheBaseAssets.MyServiceHostInfo.ApplicationName, TheBaseAssets.MyServiceHostInfo.ApplicationTitle + " Initiating shutdown at : " + TheCommonUtils.GetDateTimeString(DateTimeOffset.Now), eMsgLevel.l6_Debug));
                    TheBaseAssets.MasterSwitch = false;
                    TheBaseAssets.ForceShutdown = force;
                    TheCDEngines.StopAllEngines();
                    TheBaseAssets.MySYSLOG?.WriteToLog(3, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(TheBaseAssets.MyServiceHostInfo.ApplicationName, TheBaseAssets.MyServiceHostInfo.ApplicationTitle + " Shutdown: Engines stopped at: " + TheCommonUtils.GetDateTimeString(DateTimeOffset.Now), eMsgLevel.l6_Debug));

                    TheCommCore.StopCommunication();
                    TheBaseAssets.MySYSLOG?.WriteToLog(3, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(TheBaseAssets.MyServiceHostInfo.ApplicationName, TheBaseAssets.MyServiceHostInfo.ApplicationTitle + " Shutdown: Communications stopped at: " + TheCommonUtils.GetDateTimeString(DateTimeOffset.Now), eMsgLevel.l6_Debug));
                    if (MyCommonDisco != null)
                    {
                        MyCommonDisco.ShutdownDiscoService();
                        TheBaseAssets.MySYSLOG?.WriteToLog(3, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(TheBaseAssets.MyServiceHostInfo.ApplicationName, TheBaseAssets.MyServiceHostInfo.ApplicationTitle + " Shutdown: Discovery Service stopped at: " + TheCommonUtils.GetDateTimeString(DateTimeOffset.Now), eMsgLevel.l6_Debug));
                    }
                    if (TheBaseAssets.MySYSLOG != null)
                    {
                        string iis = "";
                        switch (TheBaseAssets.MyServiceHostInfo.cdeHostingType)
                        {
                            case cdeHostType.IIS: iis = " (Hosted in IIS)"; break;
                            case cdeHostType.ASPCore: iis = " (Hosted in ASP.NET Core)"; break;
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(3, new TSM(TheBaseAssets.MyServiceHostInfo.ApplicationName, TheBaseAssets.MyServiceHostInfo.ApplicationTitle + iis + " Stopped at : " + TheCommonUtils.GetDateTimeString(DateTimeOffset.Now), eMsgLevel.l4_Message)); //Log Entry that service has been started
                        TheBaseAssets.MySYSLOG.Shutdown();
                    }
                }
            }
        }
        private readonly object ShutdownLock = new object();
        #endregion

        /// <summary>
        /// In order to comply to the .NET 4 Client Profile, this has to be overritten by
        /// A Website that wants to store data in the web.config
        /// </summary>
        /// <returns></returns>
#if !CDE_STANDARD
        public virtual Configuration GetApplicationConfig()
        {
            Configuration tConfig = null;
            try
            {
                tConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            }
            catch {
                //ignored
            }
            return tConfig;
        }
#else
        public virtual dynamic GetApplicationConfig()
        {
            dynamic tConfig = null;
            try
            {
                var configManagerAssembly = System.Reflection.Assembly.Load("System.Configuration.ConfigurationManager");
                if (configManagerAssembly != null)
                {
                    var configManagerType = configManagerAssembly.GetType("System.Configuration.ConfigurationManager", false);
                    if (configManagerType != null)
                    {
                        var openConfigMethod = configManagerType.GetMethod("OpenExeConfiguration", System.Reflection.BindingFlags.Static);
                        tConfig = openConfigMethod?.Invoke(null, new object[] { (int)0 });
                    }
                }
            }
            catch
            {
                //ignored
            }
            return tConfig;
        }
#endif
        /// <summary>
        /// This event is fired when the scope was changed
        /// </summary>
        public virtual void EventScopeChanged(bool SetReq)
        {
            if (TheBaseAssets.MyApplication != null && TheBaseAssets.MyApplication.MyCommonDisco != null)
                TheBaseAssets.MyApplication.MyCommonDisco.UpdateContextID();
            TheQueuedSenderRegistry.UpdateLocalHostScope();

            if (TheBaseAssets.MyServiceHostInfo.AreAllEnginesStarted)
            {
                TheQueuedSenderRegistry.ReinitCloudRoutes();
                TheUserManager.ResetUserScopes(true);
            }
        }

        /// <summary>
        /// This method can be overwritten to provide a custom Blob Loader
        /// Use this if you want to load certain resources from Disk other than the ClientBin folder
        /// </summary>
        /// <param name="filename">Resource Name to be loaded</param>
        /// <returns></returns>
        public virtual byte[] cdeBlobLoader(string filename)
        {
            byte[] tBlob = TheCommonUtils.GetSystemResource(null, filename);
            return tBlob;
        }

        /// <summary>
        /// Virtual function that determines if a plugin has the rights to push back to a relay server from the cloud
        /// </summary>
        /// <param name="pMessage">Message to be inspected</param>
        /// <returns>True if the cloud can pushback, False prevents cloud from pushing back</returns>
        public virtual bool IsCloudAllowedToPushBack(TSM pMessage)
        {
            IBaseEngine tBase = TheThingRegistry.GetBaseEngine(pMessage.ENG);
            if (tBase == null || !tBase.GetEngineState().IsLiveEngine)
                return false;
            else
                return true;
        }

        /// <summary>
        /// Used to show an Alert. Override to plugin your own custom alert handler
        /// </summary>
        /// <param name="pMsg">Message with the Alert</param>
        /// <param name="pTargetScreen">A TheBaseAssets.MyDashboards ScreenName to jump to</param>
        public virtual void ShowMessageToast(TSM pMsg, string pTargetScreen)
        {
            if (MyMainPage != null)
                MyMainPage.ShowMessageToast(pMsg, pTargetScreen);
        }

        /// <summary>
        /// Transits a Dashboard to a new screen
        /// Override to use in your own navigation system
        /// </summary>
        /// <param name="pTargetScreen"></param>
        public virtual bool TransitToScreen(string pTargetScreen)
        {
            if (MyMainPage != null)
                return MyMainPage.TransitToScreen(pTargetScreen);
            return false;
        }

        /// <summary>
        /// Goes to the station Home Page
        /// Override if you have a custom procedure
        /// </summary>
        public virtual bool GotoStationHome()
        {
            if (MyMainPage != null)
                return MyMainPage.GotoStationHome();
            return false;
        }

        /// <summary>
        /// Represents an Interface the current Main User Interface Page
        /// For Client-Viewer apps, the XCode, Android Root Page, or Windows 10 Root Page (MainPage) can be wrapped as a "TheBaseApplication" and exposes the corresponding methods
        /// </summary>
        public ICDEMainPage MyMainPage = null;
        /// <summary>
        /// Gets the XAML Root page
        /// </summary>
        /// <returns></returns>
        public ICDEMainPage GetRootPage()
        {
            return MyMainPage;
        }

#if FALSE
        private static List<Assembly> GetLoadedAssemblies()
        {
            var list =
                Deployment.Current.Parts.Select(
                    ap => Application.GetResourceStream(new Uri(ap.Source, UriKind.Relative))).Select(
                        stream => new AssemblyPart().Load(stream.Stream)).ToList();
            return list;
        }
#endif
    }
}
