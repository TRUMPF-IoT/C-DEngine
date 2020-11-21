// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
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

namespace nsCDEngine.PluginManagement
{
    public class TheServicesMarketPlace : TheDataBase
    {
        /// <summary>
        /// Plugin ID
        /// </summary>
        public Guid PluginID { get; set; }
        public string ServiceName { get; set; }
        public string ServiceDescription { get; set; }

        public string LongDescription { get; set; }
        public double CurrentVersion { get; set; }
        public double Price { get; set; }
        public string HomeUrl { get; set; }
        public string IconUrl { get; set; }
        public string Developer { get; set; }
        public string DeveloperUrl { get; set; }
        public List<string> Categories { get; set; }
        public List<eThingCaps> Capabilities { get; set; }

        public cdePlatform Platform { get; set; }

        public DateTime AvailableSince { get; set; }
        public int DownloadCounter { get; set; }
        public double Rating { get; set; }
        public List<int> RaterCount { get; set; }
        public long Size { get; set; }

        public string RollbackDir { get; set; }
        public DateTime LastUpdate { get; set; }
        public string FileName { get; set; }
        public string AccessCode { get; set; }

        public bool Required { get; set; }
        public bool AllowUpdate { get; set; }
        public bool ForceUpdate { get; set; }

        public override string ToString()
        {
            return $"{ServiceName} {CurrentVersion} {ForceUpdate} {Required}".ToString(CultureInfo.InvariantCulture);
        }

        public TheServicesMarketPlace Clone()
        {
            var t = new TheServicesMarketPlace();
            t.Initialize(this);
            return t;
        }

        public void Initialize(TheServicesMarketPlace mp)
        {
            if (mp == null) return;
            ServiceDescription = mp.ServiceDescription;
            ServiceName = mp.ServiceName;
            PluginID = mp.PluginID;

            LongDescription = mp.LongDescription;
            CurrentVersion = mp.CurrentVersion;
            Price = mp.Price;
            HomeUrl = mp.HomeUrl;
            IconUrl = mp.IconUrl;
            Developer = mp.Developer;
            DeveloperUrl = mp.DeveloperUrl;

            Platform = mp.Platform;
            AvailableSince = mp.AvailableSince;
            DownloadCounter = mp.DownloadCounter;
            Size = mp.Size;
            RollbackDir = mp.RollbackDir;
            LastUpdate = mp.LastUpdate;
            FileName = mp.FileName;
            AccessCode = mp.AccessCode;
            Required = mp.Required;
            ForceUpdate = mp.ForceUpdate;
            AllowUpdate = mp.AllowUpdate;

            if (mp.Capabilities != null)
            {
                this.Capabilities = new List<eThingCaps>();
                foreach (var tt in mp.Capabilities)
                    this.Capabilities.Add(tt);
            }
            if (mp.Categories != null)
            {
                this.Categories = new List<string>();
                foreach (string tt in mp.Categories)
                    this.Categories.Add(tt);
            }
            if (mp.RaterCount != null)
            {
                this.RaterCount = new List<int>();
                foreach (int tt in mp.RaterCount)
                    this.RaterCount.Add(tt);
            }
        }
    }
#if !CDE_NET35 && !CDE_NET4
    public class ThePluginPackager
    {
        /// <summary>
        /// Creates an installation package for a plugin.
        /// </summary>
        /// <param name="tInfo">Meta information about the plug-in. (can be null, but then tPlace has to be specified)</param>
        /// <param name="tPlace">Additional information about the plug-in's presence in a market place (can be null, but then tInfo has to be specified)</param>
        /// <param name="outputDirectory">Path to a directory that will be used for temporary files as well as the generated installation package</param>
        /// <param name="bForce">Overwrites output files if they already exists.</param>
        /// <param name="packageFilePath">Path to the installation package within the outputDirectory.</param>
        /// <returns>null or empty string if successful. Error information on failure.</returns>
        public static string CreatePluginPackage(ThePluginInfo tInfo, TheServicesMarketPlace tPlace, string outputDirectory, bool bForce, out string packageFilePath)
        {
            packageFilePath = null;
            string tMetaFile = "";
            if (tInfo != null)
            {
                if (tInfo.Capabilities != null && tInfo.Capabilities.Contains(eThingCaps.Internal))
                    return $"{tInfo.ServiceName} Is Internal Only";
                if (String.IsNullOrEmpty(outputDirectory))
                {
                    tMetaFile = TheCommonUtils.cdeFixupFileName($"store\\{tInfo.cdeMID}\\{tInfo.Platform}\\");
                }
                else
                {
                    tMetaFile = $"{outputDirectory}\\store\\{tInfo.cdeMID}\\{tInfo.Platform}\\";
                }
                if (!Directory.Exists(tMetaFile))
                    TheCommonUtils.CreateDirectories(tMetaFile);
                WriteServiceInfo(tInfo, outputDirectory);

                string tgr = tMetaFile + "new\\";
                if (TheCommonUtils.IsOnLinux())
                    tgr = tgr.Replace('\\', '/');
                if (Directory.Exists(tgr))
                {
                    Directory.Delete(tgr, true);
                }

                if (tInfo.FilesManifest != null && tInfo.FilesManifest.Count > 0)
                {
                    foreach (string tFile in tInfo.FilesManifest)
                    {
                        if (tFile == null) continue;
                        string tFinalFile = tFile;
                        try
                        {
                            if (tFile.StartsWith("@"))  //If a Manifest file starts with @ this function expects this syntax: "@<platform>@<FileName>"
                            {
                                var tC = tFile.Split('@');
                                if (tC.Length < 3)
                                    continue;
                                switch (tC[1])
                                {
                                    case "net35":
                                        if (tInfo.Platform != cdePlatform.X32_V3)
                                            continue;
                                        break;
                                    case "net40":
                                        if (tInfo.Platform != cdePlatform.NETV4_32 && tInfo.Platform != cdePlatform.NETV4_64)
                                            continue;
                                        break;
                                    case "net45":
                                        if (tInfo.Platform != cdePlatform.X64_V3 && tInfo.Platform != cdePlatform.X32_V4)
                                            continue;
                                        break;
                                    case "netstandard2.0":
                                        break;
                                    default:
                                        if ((TheCommonUtils.CInt(tC[1]) > 0 && TheCommonUtils.CInt(tC[1]) != TheCommonUtils.CInt(tInfo.Platform)))
                                            continue;
                                        break;
                                }
                                tFinalFile = tC[2];
                            }
                            string src = TheBaseAssets.MyServiceHostInfo.BaseDirectory;
                            if (TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.IIS) src += "bin\\";
                            src += tFinalFile;
                            if (TheCommonUtils.IsOnLinux())
                                src = src.Replace('\\', '/');
                            tgr = tMetaFile + "new\\" + tFinalFile;
                            if (TheCommonUtils.IsOnLinux())
                                tgr = tgr.Replace('\\', '/');
                            TheCommonUtils.CreateDirectories(tgr);
                            File.Copy(src, tgr, true);
                        }
                        catch (Exception e)
                        {
                            return string.Format("Manifest File {0} could not be copied: {1}", tFile, e.ToString());
                            //TheBaseAssets.MySYSLOG.WriteToLog(2, new TSM("ISMManager", string.Format("Manifest Filer {0} could not be copied.", tFile), eMsgLevel.l1_Error, e.ToString())); //Log Entry that service has been started
                        }

                    }
                }
            }
            else
            {
                tMetaFile = TheCommonUtils.cdeFixupFileName(string.Format("store\\{0}\\{1}\\", tPlace.PluginID, tPlace.Platform));  //now PluginID which contains ThePluginInfo.cdeMID
            }

            if (tPlace != null && File.Exists(tMetaFile + "META.CDEM"))
            {
                TheServicesMarketPlace tPl = null;
                using (StreamReader sr = new StreamReader(tMetaFile + "META.CDEM"))
                {
                    String line = sr.ReadToEnd();
                    tPl = TheCommonUtils.DeserializeJSONStringToObject<TheServicesMarketPlace>(line);
                }
                if ((tInfo != null && tInfo.CurrentVersion <= tPl.CurrentVersion) || (tPl != null && tPlace.CurrentVersion <= tPl.CurrentVersion))
                    return $"{tPlace?.ServiceName} has existing Version in Store: PInfo:{tInfo?.CurrentVersion} vs Meta:{tPl?.CurrentVersion} vs Place:{tPlace?.CurrentVersion}";
            }
            if (tPlace == null)
            {
                tPlace = new TheServicesMarketPlace();
                tPlace.Capabilities = tInfo.Capabilities;
                tPlace.Categories = tInfo.Categories;
                tPlace.PluginID = tInfo.cdeMID; //PluginID now contains ThePluginInfo.cdeMID
                tPlace.CurrentVersion = tInfo.CurrentVersion;
                tPlace.LongDescription = tInfo.LongDescription;
                tPlace.Developer = tInfo.Developer;
                tPlace.DeveloperUrl = tInfo.DeveloperUrl;
                tPlace.HomeUrl = tInfo.HomeUrl;
                tPlace.Platform = tInfo.Platform;
                tPlace.Price = tInfo.Price;
                tPlace.ServiceName = tInfo.ServiceName;
                tPlace.AvailableSince = DateTime.Now;
                tPlace.RaterCount = new List<int>() { 0, 0, 0, 0, 0 };
                tPlace.Rating = 0;
                tPlace.DownloadCounter = 0;
                tPlace.ServiceDescription = string.IsNullOrEmpty(tInfo.ServiceDescription) ? tInfo.ServiceName : tInfo.ServiceDescription;
                if (TheBaseAssets.MyServiceHostInfo != null)
                {
                    if (string.IsNullOrEmpty(tPlace.HomeUrl))
                        tPlace.HomeUrl = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false) + "/%PluginID%";
                    tPlace.IconUrl = tInfo.IconUrl;
                    if (string.IsNullOrEmpty(tPlace.IconUrl) || tPlace.IconUrl == "toplogo-150.png")
                        tPlace.IconUrl = "<i class=\"cl-font cl-Logo cl-4x\"></i>"; // TheBaseAssets.MyServiceHostInfo.UPnPIcon;
                    if (tPlace.IconUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || tPlace.IconUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                        tPlace.IconUrl = $"<img src='{tPlace.IconUrl}' class='Blocked' width='78' height='78'></img>";
                    else if (tPlace.IconUrl.StartsWith("FA"))
                    {
                        tPlace.IconUrl = "<i class='fa faIcon " + (tPlace.IconUrl.Substring(3, 1) == "S" ? "fa-spin " : "") + "fa-" + tPlace.IconUrl.Substring(2, 1) + "x'>&#x" + tPlace.IconUrl.Substring(4, tPlace.IconUrl.Length - 4) + ";</i>";
                    }
                }
                if (Directory.Exists(tMetaFile + "new"))
                {
                    var res = CreateCDEX(tPlace, tMetaFile, true, out packageFilePath);
                    if (res != null)
                        return res;
                    //Directory.Move(tMetaFile + "new", tMetaFile + "DONE_" + TheCommonUtils.GetTimeStamp());
                }
                else
                {
                    return "Error: Output directory does not exists";
                }
            }
            else
            {
                if (tInfo.CurrentVersion > tPlace.CurrentVersion)
                {
                    tPlace.CurrentVersion = tInfo.CurrentVersion;
                }
                var res = CreateCDEX(tPlace, tMetaFile, bForce, out packageFilePath);
                if (res != null)
                    return res;
            }
            tPlace.LastUpdate = DateTime.Now;
            using (StreamWriter sr = new StreamWriter(tMetaFile + "META.CDEM"))
                sr.Write(TheCommonUtils.SerializeObjectToJSONString<TheServicesMarketPlace>(tPlace));
            return "";
        }
        internal static string CreateCDEX(TheServicesMarketPlace tPlace, string tMetaFile, bool bForce, out string tCDEXFilePath)
        {
            tCDEXFilePath = null;
            try
            {
                tPlace.FileName = string.Format(CultureInfo.InvariantCulture, "{1} V{0:0.0000}.CDEX", tPlace.CurrentVersion, tPlace.ServiceName.Replace('.', '-'));
                string tTarget = Path.Combine(tMetaFile, tPlace.FileName);
                tCDEXFilePath = tTarget;
                if (!bForce && File.Exists(tTarget) && !TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("AlwaysCreateCDEXOnStartup")))
                    return "Target Exists";
                TheCommonUtils.CreateDirectories(tTarget);
                if (File.Exists(tTarget))
                    File.Delete(tTarget);
                System.IO.Compression.ZipFile.CreateFromDirectory(tMetaFile + "new", tTarget, System.IO.Compression.CompressionLevel.Optimal, false);
                FileInfo f = new FileInfo(tTarget);
                tPlace.Size = f.Length;
                Directory.Delete(tMetaFile + "new", true);
            }
            catch (Exception e)
            {
                return $"Plugin {tPlace.ServiceName} with ID:{tPlace.PluginID} could not be created: {e}";
            }
            return null;
        }

        //Who uses the .CDES File in the store? Moved into platform folder
        private static void WriteServiceInfo(ThePluginInfo tInfo, string storePath)
        {
            string tInfoFile;
            if (String.IsNullOrEmpty(storePath))
            {
                tInfoFile = TheCommonUtils.cdeFixupFileName($"store\\{tInfo.cdeMID}\\{tInfo.Platform}\\{tInfo.ServiceName}.CDES");
            }
            else
            {
                tInfoFile = Path.Combine(storePath, $"{tInfo.ServiceName}.CDES");
            }
            using (StreamWriter sr = new StreamWriter(tInfoFile))
                sr.Write(TheCommonUtils.SerializeObjectToJSONString<ThePluginInfo>(tInfo));
        }

    }
#endif
}
