// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using nsCDEngine.BaseClasses;
using System.Net;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.ISM;
using nsCDEngine.Security;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Engines;

using nsCDEngine.Discovery.DeviceSearch;
using System.Linq;
//ERROR Range: 100- 199

namespace nsCDEngine.Discovery
{
    internal class TheCommonDisco : ICommonDisco, IDisposable
    {
        private readonly int DISCOExpireTime = 3600;
        private TheUPnPDeviceInfo MyDeviceUPnPInfo;

        private readonly cdeConcurrentDictionary<string, Action<TheUPnPDeviceInfo>> mUPnpUIDs = new cdeConcurrentDictionary<string, Action<TheUPnPDeviceInfo>>();    //DIC-Allowed   STRING
        private readonly cdeConcurrentDictionary<string, Action<TheUPnPDeviceInfo>> mUPnpLost = new cdeConcurrentDictionary<string, Action<TheUPnPDeviceInfo>>();    //DIC-Allowed   STRING

        private readonly cdeConcurrentDictionary<Guid, ICDESniffer> MyDiscoScanners = new cdeConcurrentDictionary<Guid, ICDESniffer>();
        private readonly cdeConcurrentDictionary<Guid, ICDEDiscoService> MyDiscoServices = new cdeConcurrentDictionary<Guid, ICDEDiscoService>();
        internal static TheStorageMirror<TheUPnPDeviceInfo> MyUPnPDiscoveryPast;
        private Action<bool> eventDiscoReady;
        private static readonly object LockNotify = new object();

        internal TheCommonDisco()
        {
            string temp = TheBaseAssets.MySettings.GetSetting("DISCO_ScanRate");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DISCOScanRate = TheCommonUtils.CInt(temp);
            temp = TheBaseAssets.MySettings.GetSetting("DISCO_ExpireTime");
            if (!string.IsNullOrEmpty(temp))
                DISCOExpireTime = TheCommonUtils.CInt(temp);
        }

        public void StartDiscoDevice()
        {
            if (TheBaseAssets.MyServiceHostInfo.IsIsolated) return;
            TheBaseAssets.MySYSLOG.WriteToLog(100, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CommonDISCO", "Starting DISCO...", eMsgLevel.l7_HostDebugMessage));
            StartUPnPDiscovery(TheBaseAssets.MyServiceHostInfo.MyLiveServices);
            TheBaseAssets.MySYSLOG.WriteToLog(101, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CommonDISCO", "...DISCO Started", eMsgLevel.l3_ImportantMessage));
        }

        private void StartUPnPDiscovery(string pLiveEngines)
        {
            if (MyUPnPDiscoveryPast != null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(109, new TSM("UPnP", "DISCO aready running!"));
                return;
            }
            MyUPnPDiscoveryPast = new TheStorageMirror<TheUPnPDeviceInfo>(Engines.TheCDEngines.MyIStorageService) { IsRAMStore = true, BlockWriteIfIsolated = true };
            MyUPnPDiscoveryPast.SetRecordExpiration(DISCOExpireTime, sinkDeviceExpired);
            //MyUPnPDiscoveryPast.CacheStoreInterval = 60;
            //MyUPnPDiscoveryPast.IsStoreIntervalInSeconds = true;
            //MyUPnPDiscoveryPast.IsCachePersistent = true;
            MyUPnPDiscoveryPast.InitializeStore(true, true);

            var tCDEConnectUrl = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false);
            TheBaseAssets.MySYSLOG.WriteToLog(107, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("UPnP", "Starting Device", eMsgLevel.l3_ImportantMessage));
            MyDeviceUPnPInfo = new TheUPnPDeviceInfo()
            {
                FriendlyName = TheBaseAssets.MyServiceHostInfo.ApplicationTitle + " at: " + tCDEConnectUrl + " - Services:" + pLiveEngines,// pStationEngines;
                Manufacturer = TheBaseAssets.MyServiceHostInfo.VendorName,
                ManufacturerUrl = TheBaseAssets.MyServiceHostInfo.VendorUrl,
                ModelName = TheBaseAssets.MyServiceHostInfo.ApplicationName,
                ModelNumber = string.Format("V{0:0.0000}", TheBaseAssets.MyServiceHostInfo.CurrentVersion),
                SerialNumber = TheBaseAssets.CurrentVersionInfo,
                PacketString="",
                RawMetaXml="",
                LocationURL = tCDEConnectUrl,
                ST="C-DEngine",
                CDEConnectUrl = tCDEConnectUrl,
                CDENodeID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                UUID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString(),
                USN= TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString(),
                IsCDEngine = true
            };
            if (!TheBaseAssets.MyServiceHostInfo.RequiresConfiguration)
                MyDeviceUPnPInfo.CDEContextID = TheBaseAssets.MyScopeManager.GetScrambledScopeID();     //GRSI: rare

            if (TheCommonUtils.IsHostADevice())
                MyDeviceUPnPInfo.HasPresentation = false;
            else
            {
                if (!TheBaseAssets.MyServiceHostInfo.DisableWebSockets && TheBaseAssets.MyServiceHostInfo.MyStationWSPort > 0)
                {
                    var builder = new UriBuilder(TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false));
                    builder.Scheme = builder.Scheme == "https" ? "wss" : "ws";
                    builder.Port = TheBaseAssets.MyServiceHostInfo.MyStationWSPort;
                    MyDeviceUPnPInfo.CDEConnectWsUrl = TheCommonUtils.TruncTrailingSlash(builder.Uri.ToString());
                }
                MyDeviceUPnPInfo.HasPresentation = true;
            }
            eventDiscoReady?.Invoke(true);

            TheQueuedSenderRegistry.RegisterHealthTimer(DoScan4Devices);
            TheBaseAssets.MySYSLOG.WriteToLog(108, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("UPnP", "DISCO engine ready..."));
        }

        public void WaitForDiscoReady(Action<bool> bIsReady)
        {
            if (MyDeviceUPnPInfo != null)
                bIsReady?.Invoke(true);
            eventDiscoReady += bIsReady;
        }

        public void Dispose()
        {
            Dispose(true);
        }
        protected virtual void Dispose(bool disposing)
        {
            ShutdownDiscoService();
        }
        public void ShutdownDiscoService()
        {
            TheBaseAssets.MySYSLOG.WriteToLog(102, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CommonDISCO", "STOPPING Disco", eMsgLevel.l7_HostDebugMessage));

            foreach (ICDESniffer tSniff in MyDiscoScanners.Values.ToList())
            {
                tSniff?.StopScan();
            }
            if (MyDiscoServices.Count > 0)
            {
                foreach (var key in MyDiscoServices.Keys.ToList())
                {
                    MyDiscoServices[key]?.StopService();
                }
                TheBaseAssets.MySYSLOG.WriteToLog(103, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CommonDISCO", "Disco STOPPED", eMsgLevel.l3_ImportantMessage));
            }
            eventDiscoReady?.Invoke(false);
        }

        #region DISCO Events
        internal static Action<Guid, string, List<string>> eventWSDMatchFound;
        public void RegisterNewMatch(Action<Guid, string, List<string>> pMatch)
        {
            // ReSharper disable once DelegateSubtraction
            eventWSDMatchFound -= pMatch;
            eventWSDMatchFound += pMatch;
        }
        public void UnRegisterNewMatch(Action<Guid, string, List<string>> pMatch)
        {
            // ReSharper disable once DelegateSubtraction
            eventWSDMatchFound -= pMatch;
        }

        public void RegisterUPnPUID(string pUid, Action<TheUPnPDeviceInfo> pCallback)
        {
            if (!mUPnpUIDs.ContainsKey(pUid))
            {
                mUPnpUIDs.TryAdd(pUid, pCallback);
                ScanKnownDevices(pUid);
            }
        }
        public void UnregisterUPnPUID(string pUid)
        {
            if (mUPnpUIDs.ContainsKey(pUid))
            {
                mUPnpUIDs.TryRemove(pUid, out _);
            }
        }

        public void RegisterDeviceLost(string pUid, Action<TheUPnPDeviceInfo> pCallback)
        {
            if (!mUPnpLost.ContainsKey(pUid))
            {
                mUPnpLost.TryAdd(pUid, pCallback);
            }
        }
        public void UnregisterDeviceLost(string pUid)
        {
            if (mUPnpLost.ContainsKey(pUid))
            {
                mUPnpLost.TryRemove(pUid, out _);
            }
        }
        #endregion


        public bool RegisterDISCOService(ICDEDiscoService pScanner)
        {
            if (pScanner == null || pScanner.GetBaseThing() == null)
                return false;
            if (MyDiscoScanners.Any(s => s.Key == pScanner.GetBaseThing().cdeMID))
                return false;
            pScanner.StartService(this);
            MyDiscoServices.TryAdd(pScanner.GetBaseThing().cdeMID, pScanner);
            return true;
        }

        public bool UnregisterDISCOService(ICDEDiscoService pScanner)
        {
            if (pScanner == null || pScanner.GetBaseThing() == null)
                return false;
            if (MyDiscoScanners.Any(s => s.Key != pScanner.GetBaseThing().cdeMID))
                return false;
            pScanner.StopService();
            MyDiscoServices.RemoveNoCare(pScanner.GetBaseThing().cdeMID);
            return true;
        }

        public void UpdateDiscoDevice()
        {
            if (MyDiscoServices.Count == 0) return;

            foreach (var key in MyDiscoServices.Keys.ToList())
            {
                if (MyDiscoServices[key] != null)
                    MyDiscoServices[key].UpdateDiscoService(TheBaseAssets.MyServiceHostInfo.ApplicationTitle + " at: " + TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false) + " - Services:" + TheBaseAssets.MyServiceHostInfo.MyLiveServices);
            }
            return;
        }
        public void UpdateContextID()
        {
            if (MyDiscoServices.Count == 0) return;

            foreach (var key in MyDiscoServices.Keys.ToList())
            {
                if (MyDiscoServices[key] != null)
                    MyDiscoServices[key].UpdateContextID(TheBaseAssets.MyScopeManager.GetScrambledScopeID());     //GRSI: rare
            }
            return;
        }



        public bool RegisterScanner(ICDESniffer pScanner)
        {
            if (pScanner == null || pScanner.GetBaseThing() == null)
                return false;
            if (MyDiscoScanners.Any(s => s.Key == pScanner.GetBaseThing().cdeMID))
                return false;

            pScanner.RegisterDeviceLost(sinkByeBye);
            pScanner.StartScan(this);
            MyDiscoScanners.TryAdd(pScanner.GetBaseThing().cdeMID, pScanner);
            return true;
        }

        public bool UnregisterScanner(ICDESniffer pScanner)
        {
            if (pScanner == null || pScanner.GetBaseThing() == null)
                return false;
            if (MyDiscoScanners.Any(s => s.Key != pScanner.GetBaseThing().cdeMID))
                return false;

            pScanner.UnregisterDeviceLost(sinkByeBye);
            pScanner.StopScan();
            MyDiscoScanners.RemoveNoCare(pScanner.GetBaseThing().cdeMID);
            return true;
        }

        private void DoScan4Devices(long tTimer)
        {
            if (TheBaseAssets.MyServiceHostInfo.DISCOScanRate == 0) return;

            if ((tTimer % TheBaseAssets.MyServiceHostInfo.DISCOScanRate) == 0)
                Scan4Devices();
        }


        public void Scan4Devices()
        {
            if (MyDiscoScanners.Count == 0) return;
            TheCommonUtils.cdeRunAsync("UPnP Scan", true, (o) =>
            {
                foreach (ICDESniffer tSniff in MyDiscoScanners.Values.ToList())
                {
                    tSniff?.Scan4Devices();
                }
            });
        }


        void sinkByeBye(string pUSN)
        {
            TheUPnPDeviceInfo tInfo = MyUPnPDiscoveryPast?.MyMirrorCache?.GetEntryByFunc(s => s.USN == pUSN);
            if (tInfo != null)
            {
                sinkDeviceExpired(tInfo);
                MyUPnPDiscoveryPast.RemoveAnItem(tInfo, null);
            }
        }


        #region Registered Device Information
        public byte[] GetDeviceInfo()
        {
            if (MyDiscoServices.Count > 0)
                return MyDiscoServices.Values.FirstOrDefault(s=>s.HasDeviceXMLInfo())?.GetDeviceInfo();
            return null;
        }

        public TheUPnPDeviceInfo GetTheUPnPDeviceInfo()
        {
            return MyDeviceUPnPInfo;
        }

        public TheUPnPDeviceInfo GetUPnPDeviceInfo(Guid USN)
        {
            TheUPnPDeviceInfo tInfo = null;
            if (MyUPnPDiscoveryPast?.MyMirrorCache?.Count > 0)
                tInfo = MyUPnPDiscoveryPast.MyMirrorCache.GetEntryByID(USN);
            return tInfo;
        }

        public List<TheUPnPDeviceInfo> GetAllUPnPDeviceInfo()
        {
            if (MyUPnPDiscoveryPast?.MyMirrorCache?.Count > 0)
                return MyUPnPDiscoveryPast.MyMirrorCache.MyRecords.Values.ToList();
            return null;
        }
        public List<TheUPnPDeviceInfo> GetAllUPnPDeviceInfo(Func<TheUPnPDeviceInfo, bool> pFunc)
        {
            if (MyUPnPDiscoveryPast?.MyMirrorCache?.Count > 0)
            {
                return MyUPnPDiscoveryPast.MyMirrorCache.GetEntriesByFunc(pFunc);
            }
            return null;
        }
        public TheUPnPDeviceInfo GetUPnPDeviceInfoByFunc(Func<TheUPnPDeviceInfo, bool> pFunc)
        {
            if (MyUPnPDiscoveryPast?.MyMirrorCache?.Count > 0)
            {
                var t=MyUPnPDiscoveryPast.MyMirrorCache.GetEntriesByFunc(pFunc);
                if (t != null && t.Any())
                    return t.FirstOrDefault();
            }
            return null;
        }

        public void RegisterDevice(TheUPnPDeviceInfo pNewDevice)
        {
            RegisterDevice(pNewDevice, false);
        }

        public bool RegisterDevice(TheUPnPDeviceInfo pNewDevice, bool ForceUpdate)
        {
            TheDiagnostics.SetThreadName("UPnP-Notify", true);
            if (!TheBaseAssets.MasterSwitch || (TheCommonUtils.cdeIsLocked(LockNotify) && !ForceUpdate) || string.IsNullOrEmpty(pNewDevice?.LocationURL)) return false;
            lock (LockNotify)
            {
                TheUPnPDeviceInfo tHistory = null;
                //tHistory = MyUPnPDiscoveryPast.MyMirrorCache.GetEntryByFunc((s) => s.LocationURL.Equals(pNewDevice.LocationURL, StringComparison.OrdinalIgnoreCase)); //
                string[] tUsn = pNewDevice.USN.Split(':');
                string tNewUsn = pNewDevice.USN;
                if (tUsn.Length > 2)
                    tNewUsn = tUsn[0] + ':' + tUsn[1];
                tHistory = MyUPnPDiscoveryPast.MyMirrorCache.GetEntryByFunc((s) => s.USN.StartsWith(tNewUsn, StringComparison.OrdinalIgnoreCase)); //
                if (tHistory != null) //gUSN
                {
                    try
                    {
                        tHistory.SourceIP = IPAddress.TryParse(pNewDevice.LocationURL.Split(':')[0], out IPAddress tIp) ? pNewDevice.LocationURL : new Uri(pNewDevice.LocationURL).Host;
                        tHistory.LocationURL = pNewDevice.LocationURL;
                        if (DateTimeOffset.Now.Subtract(tHistory.LastSeenAt).TotalSeconds < tHistory.MaxAge)
                        {
                            MyUPnPDiscoveryPast.MyMirrorCache.AddOrUpdateItem(tHistory); //gUSN
                            if (tHistory.IsCDEngine)
                                ProcessCDEngine(tHistory, TheCommonUtils.CUri(tHistory.LocationURL, false));
                            else
                                CheckForDeviceMatch(tHistory, null);
                            return true;
                        }
                        tHistory.LastSeenAt = DateTimeOffset.Now;
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(111, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("UPnP", string.Format("UPnP {0} Invalid Device URL", pNewDevice), eMsgLevel.l1_Error, e.ToString()));
                        return false;
                    }
                }
                if (tHistory == null)
                    tHistory = pNewDevice;
                MyUPnPDiscoveryPast.MyMirrorCache.AddOrUpdateItem(tHistory); //gUSN
                Uri tLocationUrl;
                try
                {
                    tLocationUrl = TheCommonUtils.CUri(tHistory.LocationURL, false);
                    TheBaseAssets.MySYSLOG.WriteToLog(111, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("UPnP", string.Format("UPnP {0} Device Found", tHistory), eMsgLevel.l3_ImportantMessage));
                    CheckForDeviceMatch(tHistory, tLocationUrl);
                }
                catch (Exception eee)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(111, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("UPnP", string.Format("Non UPnP conform device found: {0}", tHistory), eMsgLevel.l7_HostDebugMessage, eee.ToString()));
                    return false;
                }
                if (tHistory.IsCDEngine)
                    ProcessCDEngine(tHistory, tLocationUrl);
            }
            return true;
        }

        bool ProcessCDEngine(TheUPnPDeviceInfo tHistory, Uri tLocationUrl)
        {
            if ((tLocationUrl.Host.Equals(TheBaseAssets.MyServiceHostInfo.MyStationIP) && tLocationUrl.Port == TheBaseAssets.MyServiceHostInfo.MyStationPort) || !tHistory.PacketString.Contains(TheBaseAssets.MyServiceHostInfo.ApplicationName) || tLocationUrl.Host.Contains("127.0.0.1"))
                return true;
            TheBaseAssets.MySYSLOG.WriteToLog(111, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("UPnP", string.Format("C-DEngine Root Device Found: {0}", tHistory), eMsgLevel.l7_HostDebugMessage));
            if (!TheBaseAssets.MyServiceHostInfo.DisableUPnPAutoConnect && TheBaseAssets.MyServiceHostInfo.ApplicationName.Equals(tHistory.ModelName))
            {
                if (!string.IsNullOrEmpty(tHistory.CDEContextID))
                {
                    if (!string.IsNullOrEmpty(tHistory.CDEProvisionUrl))
                        TheBaseAssets.MySettings.SetSetting("AutoProvisioningUrl",tHistory.CDEProvisionUrl);
                    if (!TheBaseAssets.MyScopeManager.IsScopingEnabled ||
                        (TheBaseAssets.MyScopeManager.IsScopingEnabled && !TheBaseAssets.MyScopeManager.ScopeID.Equals(TheBaseAssets.MyScopeManager.GetRealScopeID(tHistory.CDEContextID))))    //GRSI: rare
                        return false;
                }
                else
                {
                    if (TheBaseAssets.MyScopeManager.IsScopingEnabled || !TheBaseAssets.MyServiceHostInfo.AllowUnscopedMesh)
                        return false;
                }

                if (!TheCommonUtils.DoesContainLocalhost(tHistory.CDEConnectUrl) && !TheCommonUtils.DoesContainLocalhost(tHistory.CDEConnectWsUrl))
                {
                    string strStationRoles = tHistory.FriendlyName;
                    int pos = strStationRoles.LastIndexOf(':') + 1;
                    if (!string.IsNullOrEmpty(strStationRoles) && pos >= 0)
                    {
                        strStationRoles = strStationRoles.Substring(pos, strStationRoles.Length - pos);
                        string[] sRoles = strStationRoles.Split(';');
                        List<string> discoveredStationRoles = new List<string>();
                        foreach (string gt in sRoles)
                            discoveredStationRoles.Add(TheBaseAssets.MyScopeManager.AddScopeID(gt));
                        string[] apps = TheCommonUtils.cdeSplit(tHistory.FriendlyName, " at:", false, false);
                        string appName = "Unknown App"; if (apps != null && apps.Length > 0) appName = apps[0].Trim();
                        string tConnectUrl = tHistory.CDEConnectUrl;
                        if (!string.IsNullOrEmpty(tHistory.CDEConnectWsUrl))
                            tConnectUrl = tHistory.CDEConnectWsUrl;
                        TheBaseAssets.MySYSLOG.WriteToLog(112, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("UPnP", $"Compatible Service Found: {tHistory.FriendlyName} {tHistory.Manufacturer} {tHistory.ModelName} {tHistory.ModelNumber} {tHistory.SerialNumber} {tConnectUrl}", eMsgLevel.l7_HostDebugMessage));
                        eventWSDMatchFound?.Invoke(tHistory.CDENodeID, tConnectUrl, discoveredStationRoles);
                        TheBaseAssets.MySYSLOG.WriteToLog(113, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("UPnP", "New " + appName + " Station found at: " + tConnectUrl, eMsgLevel.l3_ImportantMessage));
                    }
                }
            }
            return true;
        }

        public void CheckForDeviceMatch(TheUPnPDeviceInfo tHistory, Uri pLocationUrl)
        {
            if (mUPnpUIDs.Count <= 0) return;
            if (pLocationUrl == null || TheCommonUtils.IsGuid(pLocationUrl.Host))
                pLocationUrl = null;
            bool sendOnce = false;
            bool hasAllFlag = false;

            foreach (string tUID in mUPnpUIDs.Keys)
            {
                if (tUID.Equals("*") && mUPnpUIDs[tUID] == null)
                {
                    if (pLocationUrl == null && mUPnpUIDs[tUID] != null)
                        mUPnpUIDs[tUID](tHistory);
                    hasAllFlag = true;
                }
                else
                {
                    string[] stParts = TheCommonUtils.cdeSplit(tUID, ";:;", true, true);
                    if (stParts.Length == 1)
                    {
                        if (!string.IsNullOrEmpty(tHistory?.ST) && (tUID == "*" || tHistory?.ST?.ToUpper().Contains(tUID.ToUpper())==true))
                        {
                            if ((pLocationUrl == null || !string.IsNullOrEmpty(tHistory.USN)) && mUPnpUIDs[tUID] != null)
                                mUPnpUIDs[tUID](tHistory);
                            else
                            {
                                var t = TheThingRegistry.GetThingByMID(tHistory.ScannerID);
                                (t.GetObject() as ICDEDiscoService)?.GetDeviceDetails(tHistory, mUPnpUIDs[tUID]);
                                //if (pLocationUrl!=null)
                                //    TheREST.GetRESTAsync(pLocationUrl, 0, sinkUPnPCustomDeviceFound, mUPnpUIDs[tUID]);
                            }
                            sendOnce = true;
                        }
                    }
                    else
                    {
                        if (CompareUPnPField(tHistory, stParts[0], stParts[1]))
                        {
                            if ((pLocationUrl == null || !string.IsNullOrEmpty(tHistory.USN)) && mUPnpUIDs[tUID] != null)
                                mUPnpUIDs[tUID](tHistory);
                            else
                            {
                                var t = TheThingRegistry.GetThingByMID(tHistory.ScannerID);
                                (t.GetObject() as ICDEDiscoService)?.GetDeviceDetails(tHistory, mUPnpUIDs[tUID]);
                                //if (pLocationUrl != null)
                                //    TheREST.GetRESTAsync(pLocationUrl, 0, sinkUPnPCustomDeviceFound, mUPnpUIDs[tUID]);
                            }
                            sendOnce = true;
                        }
                    }
                }
            }
            if (!sendOnce && hasAllFlag && pLocationUrl != null)
            {
                //TheSystemMessageLog.ToCo(string.Format("UPnP Get Meta From: {0}", pLocationUrl));
                var t = TheThingRegistry.GetThingByMID(tHistory.ScannerID);
                (t?.GetObject() as ICDEDiscoService)?.GetDeviceDetails(tHistory,null);
                //TheREST.GetRESTAsync(pLocationUrl, 0, sinkUPnPCustomDeviceFound, null);
            }
        }



        private static bool CompareUPnPField(TheUPnPDeviceInfo tHistory, string pUPnpField, string pMatch)
        {
            bool tSendNow = false;
            switch (pUPnpField)
            {
                case "VendorData":
                    if (!string.IsNullOrEmpty(tHistory.VendorData) && tHistory.VendorData.ToUpper().Contains(pMatch.ToUpper()))
                        tSendNow = true;
                    break;
                case "ModelName":
                    if (!string.IsNullOrEmpty(tHistory.ModelName) && tHistory.ModelName.ToUpper().Contains(pMatch.ToUpper()))
                        tSendNow = true;
                    break;
                case "DeviceType":
                    if (!string.IsNullOrEmpty(tHistory.DeviceName) && tHistory.DeviceName.ToUpper().Contains(pMatch.ToUpper()))
                        tSendNow = true;
                    break;
                case "FriendlyName":
                    if (!string.IsNullOrEmpty(tHistory.FriendlyName) && tHistory.FriendlyName.ToUpper().Contains(pMatch.ToUpper()))
                        tSendNow = true;
                    break;
                case "Manufacturer":
                    if (!string.IsNullOrEmpty(tHistory.Manufacturer) && tHistory.Manufacturer.ToUpper().Contains(pMatch.ToUpper()))
                        tSendNow = true;
                    break;
            }
            return tSendNow;
        }

        private void sinkDeviceExpired(TheUPnPDeviceInfo tHistory)
        {
            foreach (string tUID in mUPnpLost.Keys)
            {
                if (mUPnpLost[tUID] == null) continue;

                if (tUID.Equals("*"))
                    mUPnpLost[tUID](tHistory);
                else
                {
                    string[] stParts = TheCommonUtils.cdeSplit(tUID, ";:;", true, true);
                    if (stParts.Length == 1)
                    {
                        if (tUID == "*" || tHistory?.ST?.ToUpper().Contains(tUID.ToUpper())==true)
                            mUPnpLost[tUID](tHistory);
                    }
                    else
                    {
                        if (CompareUPnPField(tHistory, stParts[0], stParts[1]))
                            mUPnpLost[tUID](tHistory);
                    }
                }
            }
        }

        public void RegisterDeviceWithServer(Uri pTargetServer, Action<string, TheUPnPDeviceInfo> pCallback)
        {
            TheREST tRest = new TheREST();
            var builder = new UriBuilder(pTargetServer) {Path = "DEVICEREG.JSON"};
            tRest.PostRESTAsync(builder.Uri, sinkRegResult, TheCommonUtils.SerializeObjectToJSONString(MyDeviceUPnPInfo), pCallback, sinkRegResult);
        }
        void sinkRegResult(TheRequestData pData)
        {
            if (pData.CookieObject is Action<string, TheUPnPDeviceInfo> tResultCallback)
            {
                switch (pData.StatusCode)
                {
                    case (int)eHttpStatusCode.OK:
                        TheUPnPDeviceInfo tInfo = null;
                        try
                        {
                            if (pData.PostData != null && pData.PostData.Length > 0)
                                tInfo = TheCommonUtils.DeserializeJSONStringToObject<TheUPnPDeviceInfo>(TheCommonUtils.CArray2UTF8String(pData.ResponseBuffer));
                            tResultCallback("OK", tInfo);
                        }
                        catch
                        {
                            tResultCallback("FAILED-No valid TheUPnPDeviceInfo received", null);
                        }
                        break;
                    case (int)eHttpStatusCode.ServerError:
                        tResultCallback("FAILED-Server Not found or has errors", null);
                        break;
                    case (int)eHttpStatusCode.NotAcceptable:
                        tResultCallback("FAILED-Server failed to register device", null);
                        break;
                    default:
                        tResultCallback("FAILED-Unkown error", null);
                        break;
                }
            }
        }

        public void ScanForDevice(string pUid)
        {
            if (MyDiscoScanners.Count == 0) return;
            TheCommonUtils.cdeRunAsync("UPnP Scan", true, (o) =>
            {
                foreach (ICDESniffer tSniff in MyDiscoScanners.Values.ToList())
                {
                    tSniff?.ScanForDevice(pUid);
                }
            });
        }



        public void ScanKnownDevices(string pUid)
        {
            if (pUid == null) return;

            if ((pUid.Equals("*") && mUPnpUIDs[pUid] == null) || MyUPnPDiscoveryPast?.MyMirrorCache == null) return;
            foreach (TheUPnPDeviceInfo tHistory in MyUPnPDiscoveryPast.MyMirrorCache.TheValues)
            {
                string[] stParts = TheCommonUtils.cdeSplit(pUid, ";:;", true, true);
                if (stParts.Length == 1)
                {
                    if (pUid == "*" || tHistory?.ST?.ToUpper().Contains(pUid.ToUpper())==true)
                    {
                        mUPnpUIDs[pUid]?.Invoke(tHistory);
                    }
                }
                else
                {
                    if (CompareUPnPField(tHistory,stParts[0], stParts[1]))
                    {
                        mUPnpUIDs[pUid]?.Invoke(tHistory);
                    }
                }
            }
        }
#endregion
    }
}
