// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.Engines;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using System.Globalization;
using System.IO;
using nsCDEngine.PluginManagement;

namespace cdePackager
{
    public interface ILogger
    {
        void WriteLine(string text);
    }

    public static class ThePackager
    {
        static Dictionary<string, Type> MyCDEPluginTypes = new Dictionary<string, Type>();       //DIC-Allowed   STRING

        public static int PackagePlugIn(string pluginFilePath, string binDirectoryPath, string storeDirectoryPath,string pPlatform, ILogger logger, bool bDiagnostics, bool bCheckForFileOverwrite, out string error)
        {
            if (!binDirectoryPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                binDirectoryPath += Path.DirectorySeparatorChar;
            }
            if (storeDirectoryPath != null &&  !storeDirectoryPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                storeDirectoryPath += Path.DirectorySeparatorChar;
            }


            TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo(cdeHostType.NOTSET, cdeNodeType.NOTSET, binDirectoryPath)
            {

            };

            cdePlatform tPluginPlatform = cdePlatform.NOTSET;
            if (TheCommonUtils.CInt(pPlatform)>0)
                Enum.TryParse<cdePlatform>(pPlatform, out tPluginPlatform);
            else
            {
                switch (pPlatform)
                {
                    case "net35":
                        tPluginPlatform = cdePlatform.X32_V3;
                        break;
                    case "net45":
                        tPluginPlatform = cdePlatform.X64_V3;
                        break;
                    case "net40":
                        tPluginPlatform = cdePlatform.NETV4_64;
                        break;
                    case "netstandard2.0":
                        tPluginPlatform = cdePlatform.NETSTD_V20;
                        break;
                }
            }

            var packageStr = $"Packaging {pluginFilePath} from {binDirectoryPath}";
            if (!String.IsNullOrEmpty(storeDirectoryPath))
            {
                packageStr += $" to {storeDirectoryPath}";
            }

            logger.WriteLine(packageStr);

            if (File.Exists(Path.ChangeExtension(pluginFilePath, ".cdes")))
                pluginFilePath = Path.ChangeExtension(pluginFilePath, ".cdes");

            error = "";
            if (pluginFilePath.ToLower().EndsWith(".cdes"))
            {
                try
                {
                    var tInfo = TheCommonUtils.DeserializeJSONStringToObject<ThePluginInfo>(File.ReadAllText(pluginFilePath));
                    if (tPluginPlatform > 0)
                        tInfo.Platform = tPluginPlatform;
                    error = CreateCDEX(storeDirectoryPath, tInfo, logger, bCheckForFileOverwrite, out var cdesPath);
                    if (!String.IsNullOrEmpty(error))
                    {
                        logger.WriteLine($"Error creating package: {error}");
                        return 7;
                    }
                    else
                    {
                        logger.WriteLine($"Created CDES at {cdesPath}");
                    }
                }
                catch (Exception e)
                {
                    logger.WriteLine($"Error packaging CDES file {pluginFilePath}: {e} ");
                    error = e.ToString();
                    return 6;
                }
            }
            else
            {
                try
                {
                    AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (o, args) =>
                    {
                        string name = null;
                        Assembly ass = null;
                        try
                        {
                            name = AppDomain.CurrentDomain.ApplyPolicy(args.Name);
                            try
                            {
                                ass = System.Reflection.Assembly.ReflectionOnlyLoad(name);
                            }
                            catch (Exception e)
                            {
                                var assName = new AssemblyName(name).Name;
                                var assPath = Path.Combine(binDirectoryPath, assName + ".dll");
                                if (bDiagnostics)
                                {
                                    logger.WriteLine($"Error resolving {args.Name} {name}, trying {assPath}: {e.Message}");
                                }
                                ass = System.Reflection.Assembly.ReflectionOnlyLoadFrom(assPath);
                            }
                        }
                        catch (Exception e)
                        {
                            logger.WriteLine($"Error resolving {args.Name} {name}: {e.Message}");
                            throw;
                        }
                        if (bDiagnostics)
                        {
                            logger.WriteLine($"Resolving {args.Name} {name}: {ass?.CodeBase}");
                        }
                        return ass;
                    };
                    Assembly tAss;
                    try
                    {
                        // load for reflection only first (to support cross-platform tooling). But needs to be in separate app domain like in the engine host. For now only do reflection when LoadFrom fails (i.e. native x86 assembly in x64 packager process).
                        tAss = Assembly.LoadFrom(pluginFilePath);
                    }
                    catch
                    {
                        tAss = Assembly.ReflectionOnlyLoadFrom(pluginFilePath);
                    }
                    tPluginPlatform = TheCommonUtils.GetAssemblyPlatformHostAgnostic(tAss, bDiagnostics, out string diagInfo);
                    if (!string.IsNullOrEmpty(diagInfo))
                    {
                        logger.WriteLine(diagInfo);
                    }

                    var CDEPlugins = from t in tAss.GetTypes() 
                                     let ifs = t.GetInterfaces()
                                     where ifs != null && ifs.Length > 0 && (ifs.FirstOrDefault(i => i.AssemblyQualifiedName == typeof(ICDEPlugin).AssemblyQualifiedName) != null)
                                     select new { Type = t, t.Namespace, t.Name, t.FullName };
                    // ReSharper disable once PossibleMultipleEnumeration
                    if (CDEPlugins == null || !CDEPlugins.Any())
                    {
                        string tPls = "";
                        foreach (Type ttt in tAss.GetTypes())
                        {
                            if (tPls.Length > 0) tPls += ",\n\r";
                            tPls += ttt.FullName;
                        }
                        error = $"File {pluginFilePath} - found but did not contain C-DEngine Plug-In Interface. Types found: \n\r{tPls}";
                        logger.WriteLine(error);
                        return 2;
                    }
                    else
                    {
                        // ReSharper disable once PossibleMultipleEnumeration
                        foreach (var Plugin in CDEPlugins)
                        {
                            if (Plugin.Type.GetInterfaces().FirstOrDefault(itf => itf.FullName == typeof(ICDEPlugin).FullName) != null && !MyCDEPluginTypes.ContainsKey(Plugin.FullName))
                            {
                                MyCDEPluginTypes.Add(Plugin.FullName, Plugin.Type);
                                logger.WriteLine(string.Format("Plugin {1} in file {0} - found and added", pluginFilePath, Plugin.FullName));
                            }
                        }
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
                        res += "</br>Loader Exception: " + e;
                    }
                    logger.WriteLine($"LoadAssembly {pluginFilePath} failed with ReflectionTypeLoadException: {eeee.StackTrace} {eeee.LoaderExceptions.Aggregate("", (s,e) => $"{s}\r\n{e.Message}")}");
                    if (bOldICDE)
                    {
                        error = $"Assembly {pluginFilePath} appears to be a plug-in for an earlier C-DEngine and is no longer supported.";
                        logger.WriteLine(error);
                        return 3;
                    }
                    if (MyCDEPluginTypes.Count == 0)
                    {
                        return 4;
                    }
                }
                catch (Exception eee)
                {
                    error = $"ProcessDirectory:LoadAssembly {pluginFilePath} failed: {eee}";
                    logger.WriteLine(error);
                    return 4;
                }

                foreach (var tType in MyCDEPluginTypes.Values)
                {
                    string cdexPath = "";
                    try
                    {
                        if (tType.IsAbstract)
                            continue;
                        var tBase = new ThePackagerBaseEngine();
                        if (!EngineAssetInfoAttribute.ApplyEngineAssetAttributes(tType, tBase))
                        {
                            //Assembly tAssRunning = Assembly.LoadFrom(pluginFilePath)
                            if (!(Activator.CreateInstance(tType) is ICDEPlugin tIPlugin))
                            {
                                error = "Could not activate Plugin-Service:" + tType;
                                logger.WriteLine(error);
                                return 5;
                            }
                            tIPlugin.InitEngineAssets(tBase);
                        }

                        // Ensure the file list is populate with at least the main module
                        tBase.AddManifestFiles(null);

                        var tInfo = tBase.GetPluginInfo();

                        if (tInfo.CurrentVersion <= 0)
                        {
                            tInfo.CurrentVersion = TheCommonUtils.GetAssemblyVersion(tType);
                        }
                        if (tPluginPlatform != cdePlatform.NOTSET)
                            tInfo.Platform = tPluginPlatform;

                        error = CreateCDEX(storeDirectoryPath, tInfo, logger, bCheckForFileOverwrite, out cdexPath);
                    }
                    catch (Exception e)
                    {
                        error = $"Plugin failed to start: {e}";
                    }
                    if (!String.IsNullOrEmpty(error))
                    {
                        logger.WriteLine($"Error creating package: {error}");
                        return 7;
                    }
                    else
                    {
                        logger.WriteLine($"Created CDEX at {cdexPath}");
                    }
                }
            }

            return 0;
        }

        private static string CreateCDEX(string storeDirectoryPath, ThePluginInfo tInfo, ILogger logger, bool bCheckForFileOverwrite, out string cdexPath)
        {
            var pluginTempDirectory = Path.Combine(storeDirectoryPath, $"newCDEX_{Guid.NewGuid()}"); // ensure each plug-in/packging operation gets it's own directory if packaging overlaps for different platforms/flavors of the same plug-in
            var error = ThePluginPackager.CreatePluginPackage(tInfo, null, pluginTempDirectory, false, out cdexPath);
            byte[] oldContent = null;
            if (string.IsNullOrEmpty(error))
            {
                // Package it into a .CDEP for use with mesh manager
                string cdepFileName = $"{tInfo.ServiceName.Replace('.', '-')}{(tInfo.Platform > 0 ? $" {tInfo.Platform}" : "")} V{tInfo.CurrentVersion:0.0000}.CDEP";
                string cdepFilePath = Path.Combine(storeDirectoryPath, cdepFileName);
                if (File.Exists(cdepFilePath))
                {
                    if (bCheckForFileOverwrite)
                    {
                        logger.WriteLine($"WARNING: File conflict: {cdepFilePath} already existed. Replacing.");
                        oldContent = File.ReadAllBytes(cdepFilePath);
                    }
                    File.Delete(cdepFilePath);
                }
                else
                {
                    logger.WriteLine($"Creating file: {cdepFilePath}.");
                }
                System.IO.Compression.ZipFile.CreateFromDirectory(Path.Combine(pluginTempDirectory, "store"), cdepFilePath, System.IO.Compression.CompressionLevel.Optimal, false);
                if (oldContent != null)
                {
                    var newContent = File.ReadAllBytes(cdepFilePath);
                    if (oldContent.SequenceEqual(newContent))
                    {
                        logger.WriteLine($"WARNING: Overwritten file was identical: {cdepFilePath}.");
                    }
                    else
                    {
                        logger.WriteLine($"WARNING: Overwritten file was different: {cdepFilePath}.");
                    }
                }
            }
            else
            {
                logger.WriteLine($"ERROR: {error}");
                return error;
            }

            var finalPluginPath = Path.Combine(storeDirectoryPath, Path.GetFileName(cdexPath));

            oldContent = null;
            try
            {
                if (File.Exists(finalPluginPath))
                {
                    if (bCheckForFileOverwrite)
                    {
                        logger.WriteLine($"WARNING: File conflict: {finalPluginPath} already existed.");
                        oldContent = File.ReadAllBytes(finalPluginPath);
                    }
                    File.Delete(finalPluginPath);
                }
                else
                {
                    logger.WriteLine($"Creating file: {finalPluginPath}.");
                }
            }
            catch
            {
                //not needed
            }
            File.Move(cdexPath, finalPluginPath);

            if (oldContent != null)
            {
                var newContent = File.ReadAllBytes(finalPluginPath);
                if (oldContent.SequenceEqual(newContent))
                {
                    logger.WriteLine($"WARNING: Overwritten file was identical: {finalPluginPath}.");
                }
                else
                {
                    logger.WriteLine($"WARNING: Overwritten file was different: {finalPluginPath}.");
                }
            }
            Directory.Delete(pluginTempDirectory, true);
            cdexPath = finalPluginPath;
            return error;
        }
    }

    class ThePackagerBaseEngine : IBaseEngine
    {
        readonly ThePluginInfo mPluginInfo = new ThePluginInfo { Platform = cdePlatform.X64_V3 }; 
        readonly TheEngineState EngineState = new TheEngineState();


        public void SetEngineService(bool pIsService)
        {
            EngineState.IsService = pIsService;
        }
        public void SetMultiChannel(bool pIsMultiChannel) 
        {  
            // Not needed
        }
        public void SetIsMiniRelay(bool pIsRelay) { EngineState.IsMiniRelay = pIsRelay; }

        public void SetStatusLevel(int pLevel)
        {
            // Not needed
        }

        public TheBaseEngine GetBaseEngine()
        {
            return null;
        }

        public string LastMessage { get; set; }

        public object GetISOLater() { return null;  }

        public void SetIsolationFlags(bool AllowIsolation, bool AllowNodeHopp = false)
        {
            // Not needed
        }

        public void SetIsInitializing()
        {
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
            if (EngineState.ServiceNode == Guid.Empty || EngineState.IsInitializing)
            {
                EngineState.ServiceNode = pMessage?.GetOriginator() ?? TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
            }
            ResetInitialization();
            SetEngineReadiness(true, null);
        }

        public bool HasChannels()
        {
            return false;
        }

        public string GetVersion()
        {
            return EngineState.Version;
        }
        public void SetVersion(double pVersion)
        {
            EngineState.Version = pVersion.ToString(CultureInfo.InvariantCulture);
            mPluginInfo.CurrentVersion = pVersion;
            AddManifestFiles(null);
        }
        public void SetNewVersion(double pVersion)
        {
            EngineState.NewVersion = pVersion;
        }

        public void SetCDEMinVersion(double pVersion)
        {
            EngineState.CDEMinVersion = pVersion.ToString(CultureInfo.InvariantCulture);
        }

        public string GetCDEMinVersion()
        {
            return EngineState.CDEMinVersion;
        }

        //internal string[] LicenseAuthorities;

        /// <summary>
        /// Plug-in call this from their InitEngineAssets() method to indicate that they require an activated license
        /// </summary>
        /// <param name="createDefaultLicense"></param>
        /// <param name="licenseAuthorities">Indicates which additional signatures are required on a license file.</param>
        public void SetIsLicensed(bool generateDefaultLicense = true, string[] LicenseAuthorities = null)
        {
            //not needed
        }

        public int GetLowestCapability()
        {
            return 0;
        }

        public void SetDashboard(string pDash)
        {
            EngineState.Dashboard = pDash;
        }
        public string GetDashboard()
        {
            return EngineState.Dashboard;
        }
        public Guid GetDashboardGuid()
        {
            string[] tGuidParts = TheCommonUtils.cdeSplit(EngineState.Dashboard, ";:;", false, false);
            return TheCommonUtils.CGuid(tGuidParts[0]);
        }

        public void SetEngineName(string pName)
        {
            EngineState.ClassName = pName;
            mPluginInfo.ServiceName = pName;

        }
        public void SetEngineType(Type pType)
        {
            EngineState.EngineType = pType;
            TheBaseAssets.MyServiceTypes.Add(pType);
        }
        public string GetEngineName()
        {
            return EngineState.ClassName;
        }
        public string GetFriendlyName()
        {
            if (string.IsNullOrEmpty(EngineState.FriendlyName)) return EngineState.ClassName;
            return EngineState.FriendlyName;
        }
        public void SetFriendlyName(string pName)
        {
            EngineState.FriendlyName = pName;
            mPluginInfo.ServiceDescription = pName;
        }
        public void SetEngineID(Guid pID)
        {
            EngineState.EngineID = pID;
            mPluginInfo.cdeMID = pID;
            EngineState.Dashboard = pID.ToString();
        }
        public void SetPluginInfo(string LongDescription, double pPrice, string pHomeUrl, string pIconUrl, string pDeveloper, string pDeveloperUrl, List<string> pCategories, string CopyRights = null)
        {
            mPluginInfo.LongDescription = LongDescription;
            mPluginInfo.Price = pPrice;
            mPluginInfo.HomeUrl = pHomeUrl;
            mPluginInfo.IconUrl = pIconUrl;
            mPluginInfo.Developer = pDeveloper;
            if (string.IsNullOrEmpty(CopyRights))
                mPluginInfo.Copyrights = DateTimeOffset.Now.Year + " " + pDeveloper;
            mPluginInfo.DeveloperUrl = pDeveloperUrl;
            mPluginInfo.Categories = pCategories;
        }
        public void AddCapability(eThingCaps pCapa)
        {
            if (mPluginInfo.Capabilities == null)
                mPluginInfo.Capabilities = new List<eThingCaps>();
            if (!mPluginInfo.Capabilities.Contains(pCapa))
                mPluginInfo.Capabilities.Add(pCapa);
        }

        public bool HasCapability(eThingCaps pCapa)
        {
            if (mPluginInfo.Capabilities == null || mPluginInfo.Capabilities.Count == 0) return false;
            return mPluginInfo.Capabilities.Contains(pCapa);
        }
        public void AddManifestFiles(List<string> pList)
        {
            if (mPluginInfo != null)
            {
                if (mPluginInfo.FilesManifest == null)
                    mPluginInfo.FilesManifest = new List<string>();
                if (GetEngineState() == null || GetEngineState().IsMiniRelay || GetEngineState().EngineType == null) return;
                Assembly a = Assembly.GetAssembly(GetEngineState().EngineType);
                if (!mPluginInfo.FilesManifest.Contains(a.ManifestModule.Name))
                    mPluginInfo.FilesManifest.Add(a.ManifestModule.Name);
                if (pList != null)
                    mPluginInfo.FilesManifest.AddRange(pList);
            }
        }

        public void AddPlatforms(List<cdePlatform> pPlatformList)
        {
            if (mPluginInfo != null && pPlatformList != null)
            {
                if (mPluginInfo.AllPlatforms == null)
                    mPluginInfo.AllPlatforms = new List<cdePlatform>();
                if (GetEngineState() == null || GetEngineState().IsMiniRelay || GetEngineState().EngineType == null) return;
                mPluginInfo.AllPlatforms.AddRange(pPlatformList);
            }
        }

        public ThePluginInfo GetPluginInfo()
        {
            return mPluginInfo;
        }

        public void FireEvent(string pEventName, object pPara, bool FireAsync)
        {
            // Not needed
        }

        public TheThing GetBaseThing()
        {
            return null;
        }

        public long GetCommunicationCosts()
        {
            return 0;
        }

        public Guid GetEngineID()
        {
            return EngineState.EngineID;
        }

        public TheEngineState GetEngineState()
        {
            return EngineState;
        }

        public Guid GetFirstNode()
        {
            return Guid.Empty;
        }

        public bool GetPluginResource(TheRequestData pName)
        {
            return true;
        }

        public byte[] GetPluginResource(string pName)
        {
            return null;
        }

        public ICDEThing GetThingInterface()
        {
            return null;
        }

        public void InitAndSubscribe()
        {
            // Not needed

        }

        public void MessageProcessed(TSM pMessage)
        {
            // Not needed

        }

        public void ProcessInitialized()
        {
            // Not needed
        }

        public void ProcessMessage(TheProcessMessage pMessage)
        {
            // Not needed
        }

        public void ProcessMessage(TSM pMessage)
        {
            // Not needed
        }

        public bool PublishToChannels(TSM pMessage, Action<TSM> pLocalCallback)
        {
            return true;
        }

        public void RegisterCSS(string cssDarkPath, string cssLitePath, Action<TheRequestData> sinkInterceptHttp)
        {
            // Not needed
        }

        public Action<ICDEThing, object> RegisterEvent(string pEventName, Action<ICDEThing, object> pCallback)
        {
            return pCallback;
        }

        public void RegisterJSEngine(Action<TheRequestData> sinkInterceptHttp)
        {
            // Not needed
        }

        public void ReplyInitialized(TSM pMessage)
        {
            // Not needed
        }

        public void SetEngineReadiness(bool pReady, TheChannelInfo pOriginator)
        {
            // Not needed
        }


        public bool StartEngine(TheChannelInfo pURLs)
        {
            return true;
        }

        public void StopEngine()
        {
            // Not needed
        }

        public void Subscribe()
        {
            // Not needed
        }

        public void UnregisterEvent(string pEventName, Action<ICDEThing, object> pCallback)
        {
            // Not needed
        }

        public void UpdateCosting(TSM pMessage)
        {
            // Not needed
        }

        public void UpdateEngineState(string pScrambledScopeID)
        {
            // Not needed
        }

        public void SetDeviceTypes(List<TheDeviceTypeInfo> pDeviceTypeInfo)
        {
            mPluginInfo.DeviceTypes = pDeviceTypeInfo;
        }

        public List<TheDeviceTypeInfo> GetDeviceTypes()
        {
            return mPluginInfo.DeviceTypes;
        }

    }

}
