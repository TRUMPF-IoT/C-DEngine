// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.PluginManagement;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;

//ERROR Range 500-509

namespace nsCDEngine.ISM
{
    /// <summary>
    /// Interface for the Intelligent Service Manager
    /// </summary>
    public interface ICDEISManager
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Initializes the Intelligent Service Manager. </summary>
        ///
        /// <param name="pUpdateDir">       The update dir. </param>
        /// <param name="pMainExec">        The main executable. </param>
        /// <param name="pExtension">       The ISM extension. </param>
        /// <param name="pCurrentVersion">  The current version. </param>
        /// <param name="UseUSBMonitoring"> True to use USB monitoring. </param>
        /// <param name="ScanAtStartup">    True to scan at startup. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        void InitISM(string pUpdateDir, string pMainExec, string pExtension, double pCurrentVersion, bool UseUSBMonitoring, bool ScanAtStartup);

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Gets the ISM file extension. </summary>
        ///
        /// <returns>   The ism file extension. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        string GetISMExt();

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Backup the cache folder. </summary>
        ///
        /// <param name="pTitle">   The title of the backup file. </param>
        /// <param name="pRetries"> Allowed retries if cache is busy. </param>
        ///
        /// <returns>   The filename of the backup. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        string BackupCacheFolder(string pTitle, int pRetries);

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Restore the cache folder. </summary>
        ///
        /// <param name="pFileName">   The filename of the backup file. </param>
        ///
        /// <returns>   True if it succeeds, false if it fails. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        bool RestoreCacheFolder(string pFileName);

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Executes the updater operation. </summary>
        ///
        /// <param name="pSourceFile">      Source file. </param>
        /// <param name="pUdateDir">        The update dir. </param>
        /// <param name="pVersion">         The version. </param>
        /// <param name="ShutdownRequired"> True if shutdown required. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        void LaunchUpdater(string pSourceFile, string pUdateDir, string pVersion, bool ShutdownRequired);

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Restarts the node </summary>
        ///
        /// <param name="ForceQuit">    True to force quit without waiting for running things or services. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        void Restart(bool ForceQuit=false);

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Scans for ism updates. </summary>
        ///
        /// <param name="IncludeUSB">   True to include, false to exclude the USB. </param>
        /// <param name="ReturnAll">    True to return all (false to only return first). </param>
        /// <param name="IncludeOldF">  True to include .OLD File, false to exclude .old files</param>
        ///
        /// <returns>   A string with a list of updates separated with ;:; </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        string ScanForISMUpdate(bool IncludeUSB, bool ReturnAll, bool IncludeOldF);

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Queries if an update is available. </summary>
        ///
        /// <returns>   True if the update is available, false if not. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        bool IsUpdateAvailable();

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Gets all available updates as string with a list of updates separated with ;:; </summary>
        ///
        /// <returns>   The available updates. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        string GetAvailableUpdates();

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Adds an a plug-in installation file to the pending update list. </summary>
        ///
        /// <param name="packageFilePath"> Path to the installation package file.</param>
        /// <param name="forceUpdate">  (Optional) True to force update even if installed version is newer. </param>
        /// <param name="returnAll">    (Optional) True to return all updates (false to only return this file). </param>
        ///
        /// <returns>   A string. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        string AddPluginPackageForInstall(string packageFilePath, bool forceUpdate = false, bool returnAll = false);

        /// <summary>
        /// Creates an installation package for a plugin.
        /// </summary>
        /// <param name="tInfo">Meta information about the plug-in. (can be null, but then tPlace has to be specified)</param>
        /// <param name="tPlace">Additional information about the plug-in's presence in a market place (can be null, but then tInfo has to be specified)</param>
        /// <param name="outputDirectory">Path to a directory that will be used for temporary files as well as the generated installation package</param>
        /// <param name="bForce">Overwrites output files if they already exists.</param>
        /// <param name="packageFilePath">Path to the installation package within the outputDirectory.</param>
        /// <returns>null or empty string if successful. Error information on failure.</returns>
        string CreatePluginPackage(ThePluginInfo tInfo, TheServicesMarketPlace tPlace, string outputDirectory, bool bForce, out string packageFilePath);

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Get a callback when the ISM is started and ready </summary>
        ///
        /// <param name="pOnISMStarted">    Callback to be called. </param>
        ///////////////////////////////////////////////////////////////////////////////////////////////////
        void WaitForISMIsStarted(Action pOnISMStarted);

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Query if this node is provisioned. </summary>
        ///
        /// <returns>   True if provisioned, false if not. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        bool IsNodeProvisioned();

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Creates a token to talk with the ProSe </summary>
        ///
        /// <param name="pTargetNode">  Target node of the token. </param>
        ///
        /// <returns>   The token as byte array. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        byte[] CreateProSeToken(Guid pTargetNode);

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Sends a message (TSM) to the provisioning service. </summary>
        ///
        /// <param name="pTSM">  Message to sent to ProSe. </param>
        ///
        /// <returns>   True if it succeeds, false if it fails. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        bool SendToProSe(TSM pTSM);

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Verify if the ProSe Token is valid. </summary>
        ///
        /// <param name="pToken">           The token. </param>
        /// <param name="pTokenVersion">    The token version. </param>
        ///
        /// <returns>   True if it succeeds, false if it fails. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        bool VerifyProSeToken(byte[] pToken, string pTokenVersion);

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Sends an encrypted settings buffer to prose </summary>
        ///
        /// <param name="pBuffer">  The buffer. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        void SendSettingsToProse(byte[] pBuffer);


        /// <summary>   Request updates from ProSe. </summary>
        void RequestUpdates();

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Restart or wipe this node </summary>
        ///
        /// <param name="ForceQuitt">   True to force quitt and dont wait for open services or thigns. </param>
        /// <param name="doWipe">       (Optional) True to do wipe. </param>
        /// <param name="pToken">       (Optional) The current ProSe token. </param>
        /// <param name="pVer">         (Optional) The current ProSe Token version. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        void RestartOrWipe(bool ForceQuitt, bool doWipe = false, byte[] pToken = null, string pVer = null);
    }


    /// <summary>
    /// The ISM (Intelligent Service Management) is used for updating and monitoring of C-DEngine plug-ins
    /// </summary>
    public class TheISMManager : TheDataBase, ICDEISManager
    {
        internal TheISMManager()
        {
        }

        /// <summary>
        /// Fires when the ISM requires a restart
        /// </summary>
        public Action<bool, string> eventShutdownRequired;  //True=Restart
        /// <summary>
        /// Fires when the ISM has updates that can be installed
        /// </summary>
        public Action<string, string> eventUpdateFound;
        /// <summary>
        /// Retired!
        /// </summary>
        [Obsolete("Please use WaitForISMStarted")]
        public static Action eventISMStarted;
        private static Action eventOnISMStarted = null;

        /// <summary>
        /// Calls the callback when the ISM is started. If it has been started already, it will fire immediatly but asynchronous
        /// </summary>
        /// <param name="pOnISMStarted"></param>
        public static void WaitForISMStarted(Action pOnISMStarted)
        {
            if (pOnISMStarted == null) return;
            if (IsReady)
            {
                TheCommonUtils.cdeRunAsync("ISM STarted", true, (o) => pOnISMStarted?.Invoke());
                return;
            }
            eventOnISMStarted += pOnISMStarted;
        }
        /// <summary>
        /// Calls the callback when the ISM is started. If it has been started already, it will fire immediatly but asynchronous
        /// </summary>
        /// <param name="pOnISMStarted"></param>
        public void WaitForISMIsStarted(Action pOnISMStarted)
        {
            if (pOnISMStarted == null) return;
            if (IsReady)
            {
                TheCommonUtils.cdeRunAsync("ISM STarted", true, (o) => pOnISMStarted?.Invoke());
                return;
            }
            eventOnISMStarted += pOnISMStarted;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Initializes the Intelligent Service Manager. </summary>
        ///
        /// <param name="pUpdateDir">       The update dir. </param>
        /// <param name="pMainExec">        The main executable. </param>
        /// <param name="pExtension">       The ISM extension. </param>
        /// <param name="pCurrentVersion">  The current version. </param>
        /// <param name="UseUSBMonitoring"> True to use USB monitoring. </param>
        /// <param name="ScanAtStartup">    True to scan at startup. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public void InitISM(string pUpdateDir, string pMainExec, string pExtension, double pCurrentVersion, bool UseUSBMonitoring, bool ScanAtStartup)
        {
            if (IsEnabled) return;
            try
            {
                IsEnabled = true;

                ISMCurrentVersion = pCurrentVersion.ToString(CultureInfo.InvariantCulture);

                ISMExtension = pExtension;
                if (string.IsNullOrEmpty(ISMExtension))
                {
                    ISMExtension = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("ISMExtension"));
                    if (string.IsNullOrEmpty(ISMExtension)) ISMExtension = TheBaseAssets.MyServiceHostInfo.ISMExtension;
                    if (string.IsNullOrEmpty(ISMExtension)) ISMExtension = ".CDEX";
                }
                else
                    ISMExtension = ISMExtension.ToUpper();

                ISMUpdateDirectory = pUpdateDir;
                if (string.IsNullOrEmpty(ISMUpdateDirectory))
                {
                    ISMUpdateDirectory = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("ISMUpdateDirectory"));
                    if (string.IsNullOrEmpty(ISMUpdateDirectory)) ISMUpdateDirectory = TheBaseAssets.MyServiceHostInfo.ISMUpdateDirectory;
                    if (string.IsNullOrEmpty(ISMUpdateDirectory))
                    {
                        ISMUpdateDirectory = TheBaseAssets.MyServiceHostInfo.BaseDirectory.Substring(0, TheBaseAssets.MyServiceHostInfo.BaseDirectory.Length - 1);
                        TheBaseAssets.MyServiceHostInfo.ISMUpdateDirectory = ISMUpdateDirectory;
                    }
                }

                ISMMainExecutable = pMainExec;
                if (string.IsNullOrEmpty(ISMMainExecutable))
                {
                    ISMMainExecutable = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("ISMMainExecutable"));
                    if (string.IsNullOrEmpty(ISMMainExecutable)) ISMMainExecutable = TheBaseAssets.MyServiceHostInfo.ISMMainExecutable;
                    if (string.IsNullOrEmpty(ISMMainExecutable)) ISMMainExecutable = AppDomain.CurrentDomain.FriendlyName;
                }
                if (ISMMainExecutable.Substring(ISMMainExecutable.Length - 4, 4).ToUpper().Equals(".EXE"))
                    ISMMainExecutable = ISMMainExecutable.Substring(0, ISMMainExecutable.Length - 4);

                ISMNameStart = TheCommonUtils.CStr(TheBaseAssets.MySettings.GetSetting("ISMNameStart"));
                if (string.IsNullOrEmpty(ISMNameStart))
                    ISMNameStart = TheBaseAssets.MyServiceHostInfo.ApplicationName;

                CleanupOldFiles();
                bool FirstBootInstall = (TheBaseAssets.MyServiceHostInfo.RequiresConfiguration && TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("InstallUpdatesOnFirstBoot")));
                if (ScanAtStartup || FirstBootInstall)
                {
                    ScanForISMUpdate(true, true, false);
                    if (TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("InstallUpdatesOnStart")) || FirstBootInstall)
                        LaunchUpdater(null, null, false);
                }
                IsReady = true;
                TheBaseEngine.WaitForEnginesStarted(sinkEnginesReady); //Must wait for ContentService to be ready

                TheCommonUtils.cdeRunAsync("ISM STarted", true, (o) => eventOnISMStarted?.Invoke());
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", "ISM Manager could not be started at " + TheCommonUtils.GetDateTimeString(DateTimeOffset.Now), eMsgLevel.l1_Error, e.ToString())); //Log Entry that service has been started
            }
        }

        void sinkEnginesReady(ICDEThing sender, object para)
        {
            ConnectToProvisioningService();
        }

        internal void ShutdownISM()
        {
            if (!IsEnabled) return;
            IsEnabled = false;
        }

        private static bool IsReady = false;
        internal bool IsEnabled;
        private string ISMExtension;
        private string ISMUpdateDirectory = "";
        private string ISMCurrentVersion = "";
        private string ISMMainExecutable = "";
        private string ISMLastFirmwareFileName;
        private string ISMLastFirmwareVersion = "";
        private string ISMNameStart = "";

        /// <summary>
        /// Probes a file if it has the ISM File Extension
        /// </summary>
        /// <param name="pFile">File to probe</param>
        /// <returns></returns>
        public static bool HasISMExtension(string pFile)
        {
            return HasISMExtension(pFile, false);
        }

        /// <summary>
        /// Probes a file if it has the ISM File Extension
        /// </summary>
        /// <param name="pFile">File to probe</param>
        /// <param name="UpdateOnly">If true only the update extension is probed</param>
        /// <returns></returns>
        public static bool HasISMExtension(string pFile, bool UpdateOnly)
        {
            return (!string.IsNullOrEmpty(pFile) && !string.IsNullOrEmpty(GetISMExtension()) &&
                (pFile.EndsWith(GetISMExtension(), StringComparison.OrdinalIgnoreCase) ||
                (!UpdateOnly && pFile.EndsWith(".CDEF", StringComparison.OrdinalIgnoreCase)))); //Additional Files
        }

        public string GetISMExt()
        {
            if (string.IsNullOrEmpty(ISMExtension)) return "";
            return ISMExtension;
        }
        /// <summary>
        /// Return the current file extension for updates
        /// </summary>
        /// <returns></returns>
        public static string GetISMExtension()
        {
            return TheBaseAssets.MyApplication?.MyISMRoot?.GetISMExt();
        }

        /// <summary>
        /// Asks the ISM to check for updates in the given file
        /// </summary>
        /// <param name="PFile"></param>
        /// <returns></returns>
        [Obsolete("Retired")]
        public static string CheckForUpdates(string PFile)
        {
            string tClientBinUpdateAt = "";
            //clientbin Update Check
            if (File.Exists(TheCommonUtils.GetTargetFolder(true, true) + Path.DirectorySeparatorChar + PFile))
                tClientBinUpdateAt = TheCommonUtils.GetTargetFolder(true, true) + Path.DirectorySeparatorChar + PFile;
            if (tClientBinUpdateAt == "" && !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ISMUpdateDirectory) && File.Exists(TheBaseAssets.MyServiceHostInfo.ISMUpdateDirectory + Path.DirectorySeparatorChar + PFile))
                tClientBinUpdateAt = TheBaseAssets.MyServiceHostInfo.ISMUpdateDirectory + Path.DirectorySeparatorChar + PFile;
            if (tClientBinUpdateAt == "" && File.Exists(TheBaseAssets.MyServiceHostInfo.BaseDirectory + PFile))
                tClientBinUpdateAt = TheBaseAssets.MyServiceHostInfo.BaseDirectory + PFile;
            return tClientBinUpdateAt;
        }

        /// <summary>
        /// Asks the ISM to scan for updates in the local clienbin/updates folder
        /// </summary>
        /// <param name="pFallback">if true, it also scans the MyDocuments folder</param>
        /// <returns></returns>
        public static string ScanForUpdates(bool pFallback)
        {
            if (TheBaseAssets.MyApplication.MyISMRoot != null)
            {
                return TheBaseAssets.MyApplication.MyISMRoot.ScanForISMUpdate(pFallback, true, false);
            }
            return "ISM Manager not running";
        }

        /// <summary>
        /// Asks the ISM to scan for updates in the local clienbin/updates folder
        /// </summary>
        /// <param name="pFallbacktoHD">if true, it also scans the MyDocuments folder</param>
        /// <returns></returns>
        public string ScanForISMUpdate(bool pFallbacktoHD)
        {
            return ScanForISMUpdate(pFallbacktoHD, false, false);
        }
        public string ScanForISMUpdate(bool pFallbacktoHD, bool ReturnAll, bool IncludeOldF)
        {
            ISMLastFirmwareFileName = "";
            ISMLastFirmwareVersion = "";
            List<string> FilesFound = ScanForUpdateFile(pFallbacktoHD, IncludeOldF);
            TheBaseAssets.MySYSLOG.WriteToLog(2, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("ISMManager", $"ISM Scan Found {FilesFound?.Count} Files")); //Log Entry that service has been started

            if (FilesFound == null) return null;
            bool FoundUpdate = false;
            foreach (string FileFound in FilesFound)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("ISMManager", $"ISM Found {FileFound}")); //Log Entry that service has been started
                try
                {
                    var ISMFileName = AddPluginPackageForInstall(FileFound, false, ReturnAll);
                    if (ISMFileName == null)
                    {
                        return null;
                    }
                    if (!string.IsNullOrEmpty(ISMFileName))
                    {
                        // Regular update with higher version
                        FoundUpdate = true;
                    }
                }
                catch (Exception eee)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", $"ISM FileForInternal failed on {FileFound}", eMsgLevel.l1_Error, eee.ToString())); //Log Entry that service has been started
                }
                // Empty ISMFileName: update had lower version, keep going
            }
            if (FoundUpdate)
            {
                if (!IncludeOldF)   //Do not fire event for each Update check run triggered by The ProvServ
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("ISMManager", $"ISM FileForInternal Success {ISMLastFirmwareFileName}")); //Log Entry that service has been started
                    eventUpdateFound?.Invoke(ISMLastFirmwareFileName, ISMLastFirmwareVersion);
                    FireEvent("ISMUpdatesFound", new TheProcessMessage { Message = new TSM(eEngineName.ContentService, "ISMUpdates", TheCommonUtils.SerializeObjectToJSONString(FilesFound)) }); //Prep for new NMI notification of all updates
                }
                return ISMLastFirmwareFileName;
            }
            return null;
        }


        internal List<ThePluginInfo> FoundUpdatesToPluginInfo()
        {
            List<ThePluginInfo> tList = new List<ThePluginInfo>();
            if (string.IsNullOrEmpty(ISMLastFirmwareFileName))
            {
                return tList;
            }

            var tFplugins = TheCommonUtils.cdeSplit(ISMLastFirmwareFileName, ";:;", true, true);
            foreach (string FileFound in tFplugins)
            {
                string tMainName = FileFound.Substring(FileFound.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                string tName = GetServiceNameFromCDEX(tMainName, out double tVersion);
                ThePluginInfo tInfo = new ThePluginInfo()
                {
                    CurrentVersion = tVersion,
                    Platform = TheBaseAssets.MyServiceHostInfo.cdePlatform,
                    ServiceName = tName
                };
                tList.Add(tInfo);
            }
            return tList;
        }

        private static string GetServiceNameFromCDEX(string pCDEXName, out double Version)
        {
            Version = 0;
            if (string.IsNullOrEmpty(pCDEXName)) return null;
            string tJustName;
            if (pCDEXName.EndsWith(".old", StringComparison.OrdinalIgnoreCase))
                tJustName = pCDEXName.Substring(0, pCDEXName.Length - (GetISMExtension().Length + 4));
            else
                tJustName = pCDEXName.Substring(0, pCDEXName.Length - (GetISMExtension().Length));
            string[] tFileVersion = tJustName.Split(' ');
            if (tFileVersion.Length < 2) return null;
            Version = TheCommonUtils.CDbl(tFileVersion[tFileVersion.Length - 1].Substring(1));
            return pCDEXName.Substring(0, tJustName.Length - (tFileVersion[tFileVersion.Length - 1].Length + 1));
        }

        // Results:
        //  ISM:
        //   higher version: return filename to caller
        //  lower version: return null to caller
        // Not ISM:
        //   higher: send update event, keep going - return "1"
        //   lower: keep going (empty string)
        public string AddPluginPackageForInstall(string FileFound, bool ForceUpdate = false, bool ReturnAll = false)
        {
            if (!string.IsNullOrEmpty(FileFound))
            {
                string tMainName = FileFound.Substring(FileFound.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                string tName = GetServiceNameFromCDEX(tMainName, out double tVersion);
                if (tMainName.StartsWith(ISMNameStart, StringComparison.OrdinalIgnoreCase) && tMainName.Length > ISMNameStart.Length + GetISMExtension().Length + 2)
                {
                    if (tVersion > TheCommonUtils.CDbl(ISMCurrentVersion) || ForceUpdate)
                    {
                        if (ReturnAll)
                        {
                            if (!string.IsNullOrEmpty(ISMLastFirmwareFileName))
                                ISMLastFirmwareFileName += ";:;";
                            ISMLastFirmwareFileName += FileFound;
                        }
                        else
                            ISMLastFirmwareFileName = FileFound;
                        ISMLastFirmwareVersion = TheCommonUtils.CStr(tVersion);
                        TheBaseAssets.MyServiceHostInfo.ISMUpdateVersion = tVersion;
                        eventUpdateFound?.Invoke(FileFound, TheCommonUtils.CStr(tVersion));
                        return FileFound;
                    }
                }
                else
                {
                    IBaseEngine tEng = TheThingRegistry.GetBaseEngine(tName.Replace('-', '.'));
                    if (tEng != null)
                    {
                        if (tVersion <= TheCommonUtils.CDbl(tEng.GetEngineState().Version) || ForceUpdate)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(2, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("ISMManager", $"no update: New Version {tVersion} and Plugin Version: {tEng.GetEngineState().Version}")); //Log Entry that service has been started
                            return "";
                        }
                        tEng.SetNewVersion(tVersion);
                    }
                    if (!string.IsNullOrEmpty(ISMLastFirmwareFileName))
                        ISMLastFirmwareFileName += ";:;";
                    ISMLastFirmwareFileName += FileFound;
                    ISMLastFirmwareVersion = TheCommonUtils.CStr(tVersion);
                    TheBaseAssets.MyServiceHostInfo.ISMUpdateVersion = tVersion;
                    return "1";
                }
            }
            return "";
        }

        /// <summary>
        /// Creates the plugin package for the Store
        /// </summary>
        /// <param name="tInfo">Plugin Info</param>
        /// <param name="tPlace">Marketplace Info</param>
        /// <param name="outputDirectory">Directory where plugin should be posted</param>
        /// <param name="bForce">if true, the package will be created even if it exists</param>
        /// <param name="packageFilePath">Resulting path to the package created</param>
        /// <returns></returns>
        public string CreatePluginPackage(ThePluginInfo tInfo, TheServicesMarketPlace tPlace, string outputDirectory, bool bForce, out string packageFilePath)
        {
            return ThePluginPackager.CreatePluginPackage(tInfo, tPlace, outputDirectory, bForce, out packageFilePath);
        }


        //internal void AddUpdateFileForInstall(string fileWithFullPath)
        //{
        //    AddUpdateFileForInstall(fileWithFullPath);
        //}

        /// <summary>
        /// Returns true if updates are available
        /// </summary>
        /// <returns></returns>
        public bool IsUpdateAvailable()
        {
            return !string.IsNullOrEmpty(ISMLastFirmwareFileName);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Gets all available updates. </summary>
        ///
        /// <remarks>   Chris, 3/20/2020. </remarks>
        ///
        /// <returns>   The available updates. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public string GetAvailableUpdates()
        {
            return ISMLastFirmwareFileName;
        }

        /// <summary>
        /// Gets the version of the update
        /// </summary>
        /// <returns></returns>
        public string GetNewVersionInfo()
        {
            if (!string.IsNullOrEmpty(ISMLastFirmwareFileName))
                return ISMLastFirmwareVersion;
            return "";
        }

        /// <summary>
        /// Launches the updater to install the updates
        /// </summary>
        /// <param name="pSourceFile">What updates should be installed</param>
        /// <param name="pUpdateDir">Where are the updates located</param>
        /// <param name="ShutdownRequired">If true, the node will be shutdown before updates are installed</param>
        public void LaunchUpdater(string pSourceFile, string pUpdateDir, bool ShutdownRequired)
        {
            if (string.IsNullOrEmpty(pSourceFile))
            {
                if (!string.IsNullOrEmpty(ISMLastFirmwareFileName))
                    LaunchUpdater(ISMLastFirmwareFileName, pUpdateDir, ISMLastFirmwareVersion, ShutdownRequired);
            }
            else
                LaunchUpdater(pSourceFile, pUpdateDir, ISMCurrentVersion, ShutdownRequired);
        }
        bool UpdaterStarted = false;
        /// <summary>
        /// Launches the updater to install the updates
        /// </summary>
        /// <param name="pSourceFile">What updates should be installed</param>
        /// <param name="pUpdateDir">Where are the updates located</param>
        /// <param name="pVersion">Version to update</param>
        /// <param name="ShutdownRequired">If true, the node will be shutdown before updates are installed</param>
        public void LaunchUpdater(string pSourceFile, string pUpdateDir, string pVersion, bool ShutdownRequired)
        {
            if (UpdaterStarted) return;
            UpdaterStarted = true;

            if (string.IsNullOrEmpty(pSourceFile) && !string.IsNullOrEmpty(ISMLastFirmwareFileName))
            {
                pSourceFile = ISMLastFirmwareFileName;
                pVersion = ISMLastFirmwareVersion;
            }

            string uDir = ISMUpdateDirectory;
            if (!string.IsNullOrEmpty(pUpdateDir)) uDir += pUpdateDir;
            if (TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.IIS) uDir += "\\bin";

#if !CDE_NET4 && !CDE_NET35    //TODO: Need to dynamically load the required ZipArchive dependencies
            if (TheCommonUtils.IsOnLinux() || !ShutdownRequired)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", $"Updating files: {pSourceFile} to: {uDir}"));

                string[] NewFiles = TheCommonUtils.cdeSplit(pSourceFile, ";:;", true, true);
                List<string> MyNewFiles = new List<string>();
                foreach (string tFile in NewFiles)
                {
                    if (File.Exists(tFile))
                        MyNewFiles.Add(tFile);
                }
                if (MyNewFiles.Count == 0)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", $"No Files found to update"));
                    UpdaterStarted = false;
                    return;
                }
                foreach (string tFile in MyNewFiles)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", $"Extracting Zip: {tFile} to: {uDir}"));
                    try
                    {
                        string tFinalTargetDir = uDir;
                        if (!tFile.ToUpper().EndsWith(GetISMExtension()))
                            tFinalTargetDir += $"{Path.DirectorySeparatorChar}ClientBin";

                        using (ZipArchive archive = ZipFile.OpenRead(tFile))
                        {
                            int THRESHOLD_ENTRIES = 10000;
                            int THRESHOLD_SIZE = 1000000000; // 1 GB
                            double THRESHOLD_RATIO = 10;
                            int totalSizeArchive = 0;
                            int totalEntryArchive = 0;
                            bool IsSuspicious = false;
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                totalEntryArchive++;

                                using (Stream st = entry.Open())
                                {
                                    byte[] buffer = new byte[1024];
                                    int totalSizeEntry = 0;
                                    int numBytesRead = 0;

                                    do
                                    {
                                        numBytesRead = st.Read(buffer, 0, 1024);
                                        totalSizeEntry += numBytesRead;
                                        totalSizeArchive += numBytesRead;
                                        double compressionRatio = entry.CompressedLength > 0 ? (double)totalSizeEntry / (double)entry.CompressedLength : 0;

                                        if (compressionRatio > THRESHOLD_RATIO)
                                        {
                                            TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", $"LaunchUpdater: Ratio ({compressionRatio}) between compressed {totalSizeEntry} and uncompressed data {entry.CompressedLength} is highly suspicious, looks like a Zip Bomb Attack", eMsgLevel.l1_Error));
                                            IsSuspicious = true;
                                            break;
                                        }
                                    }
                                    while (numBytesRead > 0);
                                }

                                if (totalSizeArchive > THRESHOLD_SIZE)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", $"LaunchUpdater: the uncompressed data size ({totalSizeArchive}) is too much for the application resource capacity. Allowed max: {THRESHOLD_SIZE}", eMsgLevel.l1_Error));
                                    IsSuspicious = true;
                                    break;
                                }

                                if (totalEntryArchive > THRESHOLD_ENTRIES)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", $"LaunchUpdater: too much entries ({totalEntryArchive}) in this archive, can lead to inodes exhaustion of the system. Allowed max: {THRESHOLD_ENTRIES}", eMsgLevel.l1_Error));
                                    
                                    IsSuspicious = true;
                                    break;
                                }
                            }
                            if (!IsSuspicious)
                            {
                                foreach (ZipArchiveEntry file in archive.Entries)
                                {
                                    string completeFileName = Path.Combine(tFinalTargetDir, file.FullName);
                                    var destinationFullPath = Path.GetFullPath(completeFileName);
                                    if (destinationFullPath.StartsWith(tFinalTargetDir))
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", $"Disallowed unzipping of File '{destinationFullPath}' because it would write outside allowed boundary '{tFinalTargetDir}'"));
                                    }
                                    else
                                    {
                                        string directory = Path.GetDirectoryName(destinationFullPath);
                                        if (!Directory.Exists(directory))
                                            Directory.CreateDirectory(directory);
                                        if (file.Name != "")
                                            file.ExtractToFile(destinationFullPath, true); //NOSONAR - Check for zipbomb is done above
                                    }
                                }
                            }
                        }
                        File.Delete(tFile + ".old");
                        File.Move(tFile, tFile + ".old");
                    }
                    catch (Exception ee)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", $"File: {tFile} failed to upgrade: {ee.ToString()}"));
                    }
                }
                if (ShutdownRequired)
                    Restart(true);
                UpdaterStarted = false;
            }
            else
#endif
            {
                if (ExtractUpdater(uDir))
                {
                    try
                    {
                        Process mainProcess = new Process();
                        if (TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.IIS)
                        {
                            mainProcess.StartInfo.FileName = Path.Combine(uDir, "C-DEngine.exe");
                            mainProcess.StartInfo.WorkingDirectory = uDir;
                        }
                        else
                        {
                            mainProcess.StartInfo.FileName = Path.ChangeExtension(TheCommonUtils.cdeGetExecutingAssembly().Location, ".exe");
                            mainProcess.StartInfo.WorkingDirectory = TheBaseAssets.MyServiceHostInfo.BaseDirectory;
                        }
                        Process currentProcess = Process.GetCurrentProcess();
                        int pid = currentProcess.Id;
                        string tHost = ISMMainExecutable;
                        if (pid > 0) tHost = $"{pid}:{ISMMainExecutable}";
                        mainProcess.StartInfo.Arguments = $"\"{pSourceFile}\" \"{uDir}\" \"{tHost}\" \"{(int)TheBaseAssets.MyServiceHostInfo.cdeHostingType}\"";
                        var tRes = mainProcess.Start();
                        TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", $"Updater started ({tRes}): ({mainProcess.StartInfo.FileName})  with \"{pSourceFile}\" \"{uDir}\" \"{tHost}\" \"{(int)TheBaseAssets.MyServiceHostInfo.cdeHostingType}\"")); //Log Entry that service has been started
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", "Start of updater failed " + TheCommonUtils.GetDateTimeString(DateTimeOffset.Now), eMsgLevel.l1_Error, e.ToString())); //Log Entry that service has been started
                    }
                    if (eventShutdownRequired != null && ShutdownRequired && (TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.Application || TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.Device))
                        eventShutdownRequired(false, pVersion);
                }
                else
                    TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", "Extraction of updater failed " + TheCommonUtils.GetDateTimeString(DateTimeOffset.Now), eMsgLevel.l1_Error));
            }
        }

        /// <summary>
        /// Restarts the current node
        /// </summary>
        public void Restart()
        {
            Restart(false);
        }
        /// <summary>
        /// Restarts the Node. If ForceQuitt is set, no data is flushed to disk on restart
        /// </summary>
        /// <param name="ForceQuit"></param>
        public void Restart(bool ForceQuit)
        {
            RestartOrWipe(ForceQuit);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Restart or wipe this node. </summary>
        ///
        /// <remarks>   Chris, 3/25/2020. </remarks>
        ///
        /// <param name="ForceQuitt">   True to force quitt and dont wait for open services or thigns. </param>
        /// <param name="doWipe">       (Optional) True to do wipe. </param>
        /// <param name="pToken">       (Optional) The current ProSe token. </param>
        /// <param name="pVer">         (Optional) The current ProSe Token version. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public void RestartOrWipe(bool ForceQuitt, bool doWipe = false, byte[] pToken = null, string pVer = null)
        {
            if (doWipe)
            {
                if (!TheBaseAssets.MyServiceHostInfo.AllowRemoteAdministration) //Wipe is only allowed with enabled RemoteAdmin
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", "!!! Remote Wipe not permitted !!!", eMsgLevel.l2_Warning));
                    return;
                }
                TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", "!!! Remote Wipe Requested !!!", eMsgLevel.l2_Warning));
                if (!VerifyProSeToken(pToken, pVer))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", "!!! Remote Wipe NOT AUTHORIZED - Token invalid !!!", eMsgLevel.l1_Error));
                    return;
                }
                TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", "!!! Remote Wipe IS AUTHORIZED - Token Valid---wiping starting... !!!", eMsgLevel.l3_ImportantMessage));
            }
            if (eventShutdownRequired != null)
            {
                //New 5.112: We are now recommending to use PM2 for autostart and auto - restart
                bool FireShutdown = false;
                if (TheCommonUtils.IsOnLinux())
                {
                    FireShutdown = true;
                }
                else
                {
                    ExtractUpdater(ISMUpdateDirectory);

                    try
                    {
                        Process mainProcess = new Process();
                        mainProcess.StartInfo.FileName = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".exe");
                        mainProcess.StartInfo.WorkingDirectory = TheBaseAssets.MyServiceHostInfo.BaseDirectory;
                        switch (TheBaseAssets.MyServiceHostInfo.cdeHostingType)
                        {
                            case cdeHostType.IIS:
                                mainProcess.StartInfo.Arguments = $"\"{(doWipe ? "WIPENODE" : "RESTART")}\" \"{ISMUpdateDirectory}\" \"{ISMMainExecutable}\" \"3\"";
                                break;
                            case cdeHostType.Service:
                                mainProcess.StartInfo.Arguments = $"\"{(doWipe ? "WIPENODE" : "RESTART")}\" \"{ISMUpdateDirectory}\" \"{ISMMainExecutable}\" \"2\"";
                                break;
                            default:
                                Process currentProcess = Process.GetCurrentProcess();
                                int pid = currentProcess.Id;
                                mainProcess.StartInfo.Arguments = $"\"{(doWipe ? "WIPENODE" : "START")}\" \"{ISMUpdateDirectory}\" \"{(pid > 0 ? $"{pid}:" : "")}{ISMMainExecutable}\"";
                                FireShutdown = true;
                                break;
                        }
                        mainProcess.Start();
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", " Start if updater failed " + TheCommonUtils.GetDateTimeString(DateTimeOffset.Now), eMsgLevel.l1_Error, e.ToString())); //Log Entry that service has been started

                    }
                }
                if (FireShutdown)
                    eventShutdownRequired?.Invoke(ForceQuitt, ISMCurrentVersion);
            }
        }

        private List<string> ScanForUpdateFile(bool pFallBack, bool IncludeOldF)
        {
            List<string> tList = new List<string>();
            var tDocFolder = TheCommonUtils.GetTargetFolder(true, true);
            DirectoryInfo di = null;
            if (tDocFolder != null)
            {
                di = new DirectoryInfo(tDocFolder);
                ProcessDirectory(di, ref tList, "", GetISMExtension(), false, false);
                ProcessDirectory(di, ref tList, "", ".CDEF", false, false);
            }
            if (tList.Count == 0 && pFallBack)
            {
                string FileToReturn1 = $"{ISMUpdateDirectory}{Path.DirectorySeparatorChar}ClientBin{Path.DirectorySeparatorChar}updates";
                TheBaseAssets.MySYSLOG.WriteToLog(2, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("ISMManager", $"No files in MyDoc found Now scanning {FileToReturn1}"));
                di = new DirectoryInfo(FileToReturn1);
                ProcessDirectory(di, ref tList, "", GetISMExtension(), false, true);
                ProcessDirectory(di, ref tList, "", ".CDEF", false, true);
                if (IncludeOldF)
                {
                    ProcessDirectory(di, ref tList, "", ".CDEF.old", false, false);
                    List<string> OldCDEX = new List<string>();
                    ProcessDirectory(di, ref OldCDEX, "", ".CDEX.old", false, false); //this will prevent updates to 99999 plugins V4.208 if this is removed CDEX in the extra file folder will no longer work (i.e. License file as CDEX)
                    if (OldCDEX?.Count > 0)
                    {
                        foreach (var told in OldCDEX)
                        {
                            var tOP = Path.GetFileName(told).Split(' ');
                            if (!tList.Any(s => Path.GetFileName(s).Split(' ')[0] == tOP[0]))
                            {
                                tList.Add(told);
                            }
                        }
                    }
                }
            }

            if (tList.Count > 0)
            {
                tList.Sort();
                return tList;
            }
            return null;
        }

        private void CleanupOldFiles()
        {
            try
            {
                List<string> tList = new List<string>();
                DirectoryInfo di = new DirectoryInfo(TheBaseAssets.MyServiceHostInfo.BaseDirectory);
                ProcessDirectory(di, ref tList, "", ".tmp", false, false);
                if (tList.Count > 0)
                {
                    foreach (string tL in tList)
                        File.Delete(tL);
                }
                ProcessDirectory(di, ref tList, "", ".pendingoverwrite", false, false);
                if (tList.Count > 0)
                {
                    foreach (string tL in tList)
                        File.Delete(tL);
                }
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Processes a directory recursive for a given file extension
        /// </summary>
        /// <param name="di">The directory info to search</param>
        /// <param name="tw">A list of found results</param>
        /// <param name="relativeCurrentDir">Relative directory to the DI</param>
        /// <param name="pExtension">Extension to look for</param>
        /// <param name="IsUsb">Include USB drives</param>
        /// <param name="DoProcessSubDirs">Recursively look in all subdirectories, too</param>
        public static void ProcessDirectory(DirectoryInfo di, ref List<string> tw, string relativeCurrentDir, string pExtension, bool IsUsb, bool DoProcessSubDirs)
        {
            try
            {
                if (!System.IO.Directory.Exists(di.FullName))
                    return;
                FileInfo[] fileInfo = di.GetFiles();

                string[] tExtParts = pExtension.Substring(1).Split('.');
                foreach (FileInfo fiNext in fileInfo)
                {
                    if (tExtParts.Length > 1)
                    {
                        if (!fiNext.Extension.Equals($".{tExtParts[1]}", StringComparison.OrdinalIgnoreCase) || !fiNext.Name.Substring(0, fiNext.Name.Length - (tExtParts[1].Length + 1)).EndsWith(tExtParts[0], StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    else
                    {
                        if (!fiNext.Extension.Equals(pExtension, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    tw.Add(fiNext.FullName);
                }

                if (!DoProcessSubDirs) return;
                DirectoryInfo[] dirs = di.GetDirectories("*.*");
                foreach (DirectoryInfo dir in dirs)
                {
                    ProcessDirectory(dir, ref tw, relativeCurrentDir + dir.Name + "\\", pExtension, IsUsb, DoProcessSubDirs);
                }
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Processes a directory recursive for a given file extension
        /// </summary>
        /// <param name="di">The directory info to search</param>
        /// <param name="tw">A list of found results as TheFileInfo</param>
        /// <param name="relativeCurrentDir">Relative directory to the DI</param>
        /// <param name="pExtension">Extension to look for</param>
        /// <param name="IsUsb">Include USB drives</param>
        /// <param name="DoProcessSubDirs">Recursively look in all subdirectories, too</param>
        public static void ProcessDirectory(DirectoryInfo di, ref List<TheFileInfo> tw, string relativeCurrentDir, string pExtension, bool IsUsb, bool DoProcessSubDirs)
        {
            try
            {
                if (!di.Exists)
                    return;
                FileInfo[] fileInfo = di.GetFiles();

                foreach (FileInfo fiNext in fileInfo)
                {
                    if (!fiNext.Extension.ToUpperInvariant().Equals(pExtension.ToUpperInvariant())) continue;
                    tw.Add(new TheFileInfo() { FileName = fiNext.FullName, CreateTime = fiNext.CreationTime, FileAttr = fiNext.Attributes.ToString(), Owner = fiNext.DirectoryName, Name = fiNext.Name, FileSize = fiNext.Length });
                }

                if (!DoProcessSubDirs) return;
                DirectoryInfo[] dirs = di.GetDirectories("*.*");
                foreach (DirectoryInfo dir in dirs)
                {
                    ProcessDirectory(dir, ref tw, relativeCurrentDir + dir.Name + "\\", pExtension, IsUsb, DoProcessSubDirs);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }


        private string GetTempFileName()
        {
            // if this failed, we try to create one in the application folder
            string fileName = Path.ChangeExtension(TheCommonUtils.cdeGetExecutingAssembly().Location, ".exe");
            try
            {
                using (File.Create(fileName))
                {
                    return fileName;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            return null;
        }
        private bool ExtractUpdater(string pTargetDir)
        {
            string resourceName = "cdeUpdater.exe";
            string fileName = GetTempFileName();
            if (TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.IIS)
                fileName = pTargetDir + "\\C-DEngine.EXE";
            if (string.IsNullOrEmpty(fileName)) return false;

            string[] names = TheCommonUtils.cdeGetExecutingAssembly().GetManifestResourceNames();
            byte[] buffer = null;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].EndsWith(resourceName))
                {
                    using (Stream stream = TheCommonUtils.cdeGetExecutingAssembly().GetManifestResourceStream(names[i]))
                    {
                        if (stream != null)
                        {
                            buffer = new byte[stream.Length];
                            stream.Read(buffer, 0, buffer.Length);
                        }
                    }
                    break;
                }
            }

            if (buffer == null)
                return false;

            try
            {
                using (FileStream target = new FileStream(fileName, FileMode.Create))
                {
                    target.Write(buffer, 0, buffer.Length);
                }
            }
            catch (IOException)
            {
                // for example there is not enough space on the disk
                return false;
            }

            return true;
        }

        //Test with: ISOPORT=8709 ISOEN=C-DMyNetwork.dll PresetDeviceID=3623adcf-f9fb-44e7-aa36-352e818bf30a ISOSCOPE=4DG8H9BR
        internal static Process LaunchIsolator(string pSourceFile, Guid pDeviceID)
        {
            try
            {
                Process mainProcess = new Process();
                mainProcess.StartInfo.FileName = Path.ChangeExtension(Assembly.GetEntryAssembly().Location, ".exe");
                mainProcess.StartInfo.WorkingDirectory = TheBaseAssets.MyServiceHostInfo.BaseDirectory;
                mainProcess.StartInfo.Arguments = $"ISOEN={pSourceFile} PresetDeviceID={pDeviceID}";
                mainProcess.EnableRaisingEvents = true;
                mainProcess.Start();
                return mainProcess;
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", " ISOlator failed " + TheCommonUtils.GetDateTimeString(DateTimeOffset.Now), eMsgLevel.l1_Error, e.ToString())); //Log Entry that service has been started

            }
            return null;
        }

        #region Auto Provisioning

        /// <summary>
        /// Creates a secure ProSe token for the given NodeID
        /// </summary>
        /// <param name="pTargetID"></param>
        /// <returns></returns>
        public byte[] CreateProSeToken(Guid pTargetID)
        {
            var tDic = new Dictionary<string, string>
            {
                { "ProSeScope", TheBaseAssets.MyScopeManager.GetScrambledScopeID() },
                { "DID", pTargetID.ToString() }
            };
            return TheBaseAssets.MyCrypto.EncryptKV(tDic);
        }

        /// <summary>
        /// Verifies a ProSe Token against the current settings
        /// </summary>
        /// <param name="pToken"></param>
        /// <param name="pTokenVersion"></param>
        /// <returns></returns>
        public bool VerifyProSeToken(byte[] pToken, string pTokenVersion)
        {
            if (pToken == null || string.IsNullOrEmpty(pTokenVersion))
                return false;
            if (pTokenVersion == "KVE10")
            {
                var tSecu = TheBaseAssets.MyCrypto.DecryptKV(pToken);
                return (tSecu?.ContainsKey("DID") == true && tSecu["DID"] == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString() &&
                        tSecu?.ContainsKey("ProSeScope") == true && TheBaseAssets.MyScopeManager.IsValidScopeID(tSecu["ProSeScope"], TheBaseAssets.MyServiceHostInfo.ProvisioningScope));
            }
            return false;
        }

        bool IsRequesting = false;

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Request updates from ProSe. </summary>
        ///
        /// <remarks>   Chris, 3/25/2020. </remarks>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public void RequestUpdates()
        {
            if (!TheBaseAssets.MasterSwitch || IsRequesting || TheCommonUtils.IsDeviceSenderType(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType)) //IDST-OK: No Updates supported on Device Nodes
                return;
            IsRequesting = true;
            List<ThePluginInfo> mList = new List<ThePluginInfo>();
            List<IBaseEngine> tBases = TheThingRegistry.GetBaseEngines(false); //4.301: We must include offline plugins otherwise they will get sent to the node everytime an update push from ProSe happens
            foreach (IBaseEngine tBase in tBases)
            {
                var tinf = tBase.GetPluginInfo();
                if (tinf.Capabilities.Contains(eThingCaps.Internal))
                    continue;
                tinf.Platform = TheBaseAssets.MyServiceHostInfo.cdePlatform;
                mList.Add(tBase.GetPluginInfo());
            }
            mList.Add(TheBaseAssets.MyAppInfo);
            var tUpdatesReady = TheBaseAssets.MyApplication?.MyISMRoot?.ScanForISMUpdate(true, true, true);
            if (!string.IsNullOrEmpty(tUpdatesReady))
            {
                var tFounds = FoundUpdatesToPluginInfo();
                foreach (var tF in tFounds)
                {
                    var tP = mList.FirstOrDefault(s => CompareServiceNames(s.ServiceName, tF.ServiceName));
                    if (tP != null)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4010, new TSM(eEngineName.ContentService, $"Found updated plugin {tF.ServiceName} with Version {tF.CurrentVersion} higher then installed {tP.CurrentVersion}", eMsgLevel.l3_ImportantMessage));
                        //tP.CurrentVersion = tF.CurrentVersion;
                        mList.Remove(tP);
                    }
                    //else
                    mList.Add(tF);
                }
                //mList.AddRange(ISM.TheISMManager.FoundUpdatesToPluginInfo());
            }
            string toSend = TheCommonUtils.SerializeObjectToJSONString(mList);
            if (MyRoute != null)
                MyRoute.SendTSM(new TSM(eEngineName.ContentService, "CDE_CHECK4_UPDATES", toSend));
            else
            {
                if (TheBaseAssets.MyServiceHostInfo.ProvisioningServiceNodeID != Guid.Empty)
                    TheCommCore.PublishToNode(TheBaseAssets.MyServiceHostInfo.ProvisioningServiceNodeID, null, new TSM(eEngineName.ContentService, "CDE_CHECK4_UPDATES", toSend));
            }
            IsRequesting = false;
        }

        public void SendSettingsToProse(byte[] pBuffer)
        {
            if (MyRoute != null)
                MyRoute.SendTSM(new TSM(eEngineName.ContentService, "CDE_SYNC_SETTINGS") { PLB = pBuffer });
            else
            {
                if (TheBaseAssets.MyServiceHostInfo.ProvisioningServiceNodeID != Guid.Empty)
                    TheCommCore.PublishToNode(TheBaseAssets.MyServiceHostInfo.ProvisioningServiceNodeID, null, new TSM(eEngineName.ContentService, "CDE_SYNC_SETTINGS") { PLB = pBuffer });
            }
        }

        static bool CompareServiceNames(string sServiceName, string tStorePluginServiceName)
        {
            if (string.IsNullOrEmpty(sServiceName) || string.IsNullOrEmpty(tStorePluginServiceName))
                return false;
            return sServiceName.Replace("-", "").Replace(".", "").ToLower() == tStorePluginServiceName.Replace("-", "").Replace(".", "").ToLower();
        }

#region ISB (Federation Connection) to the Mesh Manager Node
        private TheISBConnect MyRoute;
        internal bool ConnectToProvisioningService()
        {
            if (string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProvisioningService) || string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProvisioningScope))
                return false;
            MyRoute = new TheISBConnect();
            MyRoute.RegisterEvent2("TSMReceived", sinkTSMReceived);
            MyRoute.RegisterEvent2("Connected", sinkConnected);
            MyRoute.RegisterEvent2("Disconnected", sinkDisconnected);
            MyRoute.Connect(TheBaseAssets.MyServiceHostInfo.ProvisioningService, TheBaseAssets.MyServiceHostInfo.ProvisioningScope, true);
            return true;
        }


        public bool IsNodeProvisioned()
        {
            return MyRoute != null;
        }
        /// <summary>
        /// Returns true if this node is provisioned
        /// </summary>
        /// <returns></returns>
        public static bool IsProvisioned()
        {
            return TheBaseAssets.MyApplication?.MyISMRoot?.IsNodeProvisioned() == true;
        }

        public bool SendToProSe(TSM pToSend)
        {
            if (MyRoute == null)
                return false;

            TSM rRealTSM = TSM.Clone(pToSend, true);
            rRealTSM.SID = MyRoute.SID;
            MyRoute.SendToFirstNode(rRealTSM);
            return true;
        }

        /// <summary>
        /// Sends a TSM to the Provisioning service
        /// </summary>
        /// <param name="pToSend">Message to send to the Provisioning Service</param>
        /// <returns></returns>
        public static bool SendToProvisioningService(TSM pToSend)
        {
            return TheBaseAssets.MyApplication?.MyISMRoot?.SendToProSe(pToSend) == true;
        }

        private void sinkDisconnected(TheProcessMessage pMsg, object sender)
        {
            //MyBaseThing.LastMessage = $"{DateTimeOffset.Now} Disconnected";
            //TheThing.SetSafePropertyString(MyBaseThing, "FedID", "");
            MyRoute = null;
            if (TheBaseAssets.MasterSwitch)
            {
                TheCommonUtils.TaskDelayOneEye(10000, 100).ContinueWith(t =>
                {
                    if (TheBaseAssets.MasterSwitch)
                        ConnectToProvisioningService();
                });
            }
        }

        private void sinkConnected(TheProcessMessage pMsg, object sender)
        {
            //MyBaseThing.LastMessage = $"{DateTimeOffset.Now} Connected";
            MyRoute.Subscribe($"{eEngineName.ContentService};CDMyMeshManager.TheMeshEngine");
        }

        private void sinkTSMReceived(TheProcessMessage pMsg, object sender)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("ISMManager", $"Provisioning Message Received ENG={pMsg?.Message?.ENG} TXT={pMsg?.Message?.TXT}", eMsgLevel.l5_HostMessage));
            if (pMsg?.Message?.ENG == eEngineName.ContentService)
            {
                var tCmd = pMsg?.Message?.TXT?.Split(':');
                if (tCmd?.Length > 0)
                {
                    switch (tCmd[0])
                    {
                        //Any setting that must only go via ISM Provisioning sErvice
                        default:
                            if (TheCDEngines.MyContentEngine == null)
                                TheBaseAssets.MySYSLOG.WriteToLog(2821, new TSM("ISMManager", $"ContentService Not ready! This should not happen!", eMsgLevel.l2_Warning));
                            else
                                TheCDEngines.MyContentEngine?.GetBaseEngine()?.ProcessMessage(pMsg);
                            break;
                    }
                }
            }
        }
#endregion
#endregion

        private static bool BackupRunning = false;
        private static void timerAutoBackup(object state)
        {
            if (BackupRunning)
                return;
            BackupRunning = true;
            BackupCache($"AutoBackup {TheCommonUtils.GetTimeStamp()}");
            BackupRunning = false;
        }

        internal static void StartAutoBackup()
        {
            if (!TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID && TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("EnableAutoBackup")))
            {
                TimeSpan startFromNow = TheCommonUtils.CDate(TheBaseAssets.MySettings.GetSetting("BackupStart")).Subtract(DateTimeOffset.Now);
                if (startFromNow.Ticks < 0)
                    startFromNow = new TimeSpan();
                TimeSpan period = new TimeSpan(TheCommonUtils.CLng(TheBaseAssets.MySettings.GetSetting("BackupFrequency")) * 10000);
                if (period.Ticks < TimeSpan.TicksPerHour * 15)    //Shortest AutoBackup frequency is 15minutes
                {
                    if (period.Ticks == 0)  //If period is 0 default to once per day
                        period = new TimeSpan(TimeSpan.TicksPerDay);
                    else
                        period = new TimeSpan(TimeSpan.TicksPerMinute * 15);
                }
                if (TheCommonUtils.CDate(TheThingRegistry.GetHostProperty("LastAutoBackup")) > DateTimeOffset.MinValue)
                {
                    startFromNow = DateTimeOffset.Now.Subtract(TheCommonUtils.CDate(TheThingRegistry.GetHostProperty("LastAutoBackup")));
                    startFromNow = period.Subtract(startFromNow);
                }
                if (startFromNow.TotalMinutes < 0)
                    startFromNow = new TimeSpan();
                Timer AutoBackupTimer = new Timer(timerAutoBackup, null, startFromNow, period);
                TheBaseAssets.MySYSLOG.WriteToLog(466, new TSM("ISMManager", $"Auto Backup Started every {period.TotalMinutes} minutes", eMsgLevel.l4_Message));
            }
        }

        /// <summary>
        /// Makes a backup of the current Cache Folder to ClientBin\Backups\(pTitle).CDEB
        /// </summary>
        /// <param name="pTitle">name of the backup file</param>
        /// <returns>Full path to the backup file</returns>
        public static string BackupCache(string pTitle)
        {
            return TheBaseAssets.MyApplication?.MyISMRoot?.BackupCacheFolder(pTitle, 5);
        }

        /// <summary>
        /// Makes a backup of the current Cache Folder to ClientBin\Backups\(pTitle).CDEB
        /// </summary>
        /// <param name="pTitle">name of the backup file</param>
        /// <param name="pRetries">amount of allowed retries</param>
        /// <returns>Full path to the backup file</returns>
        public string BackupCacheFolder(string pTitle, int pRetries)
        {
            if (TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID)
                return null;
            string FileToReturn1 = null;
            try
            {
                FileToReturn1 = TheCommonUtils.cdeFixupFileName("backups\\" + pTitle + ".CDEB");
#if !CDE_NET35 && !CDE_NET4
                TheCommonUtils.CreateDirectories(FileToReturn1);
                var SourceDir = TheCommonUtils.cdeFixupFileName("cache", true);
                TheCommonUtils.CreateDirectories(SourceDir, true);
                TheBaseAssets.MyServiceHostInfo.IsBackingUp = true;
                int tries = pRetries;
                while (TheBaseAssets.MasterSwitch && TheBaseAssets.MyServiceHostInfo.IsBackingUp && tries > 0)
                {
                    try
                    {
                        ZipFile.CreateFromDirectory(SourceDir, FileToReturn1);
                        TheThingRegistry.SetHostProperty("LastAutoBackup", DateTimeOffset.Now.ToString());

                        if (IsProvisioned() && TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("BackupToCloud")))
                        {
                            var tToSend = new TSM(eEngineName.ContentService, $"CDE_BACKUPFILE:{TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID}:{pTitle}.CDEB");
                            var tNow = DateTimeOffset.Now;
                            tToSend.PLS = TheCommonUtils.GetDateTimeString(tNow, -1);
                            var imgStream = TheCommonUtils.cdeOpenFile(FileToReturn1, FileMode.Open, FileAccess.Read);
                            if (imgStream != null)
                            {
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    imgStream.CopyTo(ms);
                                    tToSend.PLB = ms.ToArray();
                                }
                                SendToProvisioningService(tToSend);
                                TheBackupDefinition tBackup = new TheBackupDefinition { BackupSize = tToSend.PLB.Length, BackupTime = tNow, FileName = FileToReturn1, Title = pTitle };
                                TheCDEngines.MyContentEngine?.FireEvent("BackupCreated", TheCDEngines.MyContentEngine, tBackup, true);
                                TheBaseAssets.MySYSLOG.WriteToLog(466, new TSM("ISMManager", $"Backup {pTitle} created", eMsgLevel.l4_Message));
                            }
                            //Todo: Send Backup to cloud
                        }
                        int Keepers = TheCommonUtils.CInt(TheBaseAssets.MySettings.GetSetting("BackupKeepLast"));
                        if (Keepers > 0)
                        {
                            DirectoryInfo di = new DirectoryInfo(Path.GetDirectoryName(FileToReturn1));
                            List<TheFileInfo> tList = new List<TheFileInfo>();
                            TheISMManager.ProcessDirectory(di, ref tList, "", ".CDEB", false, true);
                            if (tList != null && tList.Count > Keepers)
                            {
                                var tSorted = tList.OrderBy(s => s.CreateTime).ToList();
                                for (int i = 0; i < tList.Count - Keepers; i++)
                                {
                                    File.Delete(tSorted[i].FileName);
                                    TheCDEngines.MyContentEngine?.FireEvent("BackupDeleted", TheCDEngines.MyContentEngine, tSorted[i].FileName, true);
                                }
                            }
                        }
                        TheBaseAssets.MyServiceHostInfo.IsBackingUp = false;
                    }
                    catch (Exception eee)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(466, new TSM("ISMManager", $"Backup Creation for {pTitle} delayed due to open files - trying again in 3 seconds", eMsgLevel.l2_Warning, eee.ToString()));
                        TheCommonUtils.SleepOneEye(3000, 100);
                        tries--;
                    }
                }
                if (tries < 1)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(466, new TSM("ISMManager", $"Repeated Backup Creation for {pTitle} failed - giving up now, trying again at next interval", eMsgLevel.l2_Warning));
                }
#endif
                return FileToReturn1;
            }
            catch (Exception e)
            {
                if (!string.IsNullOrEmpty(FileToReturn1) && File.Exists(FileToReturn1))
                    File.Delete(FileToReturn1);
                TheBaseAssets.MySYSLOG.WriteToLog(466, new TSM("ISMManager", $"Backup Creation for {pTitle} failed", eMsgLevel.l1_Error, e.ToString()));
            }
            return null;
        }

        /// <summary>
        /// Restores the cache from a given backup folder
        /// </summary>
        /// <param name="pTitle">Folder name to restore</param>
        public bool RestoreCacheFolder(string pTitle)
        {
            try
            {
#if CDE_NET45
                string tRestoreFile = TheCommonUtils.cdeFixupFileName("backups\\" + pTitle);
                string SourceDir = TheCommonUtils.cdeFixupFileName("__CacheToRestore"); // Write to separate location, then put in place during startup do avoid overwrite
                TheCommonUtils.CreateDirectories(SourceDir, true);
                var downloadedMessageInfo = new DirectoryInfo(SourceDir);
                foreach (FileInfo file in downloadedMessageInfo.GetFiles())
                    file.Delete();
                ZipFile.ExtractToDirectory(tRestoreFile, SourceDir);
                TheBaseAssets.MyApplication.MyISMRoot.Restart(true);
#endif
                return true;
            }
            catch (Exception)
            {

            }
            return false;
        }

        /// <summary>
        /// Restores the cache from a given backup folder
        /// </summary>
        /// <param name="pTitle">Folder name to restore</param>
        public static void RestoreCache(string pTitle)
        {
            TheBaseAssets.MyApplication?.MyISMRoot?.RestoreCacheFolder(pTitle);
        }

    }
}
