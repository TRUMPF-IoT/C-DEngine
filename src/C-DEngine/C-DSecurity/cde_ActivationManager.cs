// SPDX-FileCopyrightText: Copyright (c) 2017-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Interfaces;
using nsCDEngine.ISM;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace nsCDEngine.Activation
{
    public static class eActivationEvents
    {
        public const string LicenseActivated = nameof(LicenseActivated);
        public const string LicenseActivationExpired = nameof(LicenseActivationExpired);
        public const string LicenseExpired = nameof(LicenseExpired);
    }

    /// <summary>
    /// Data structure describing an activated license
    /// </summary>
    public class TheLicenseActivationInformation
    {
        /// <summary>
        /// The hash of the activation key that was used to activate the license.
        /// </summary>
        public string ActivationKeyHash; // base64 encoded
        /// <summary>
        /// Expiration time of the activation key.
        /// </summary>
        public DateTimeOffset ActivationKeyExpiration;
        /// <summary>
        /// License that was activated using the activation key. Notice that a single license can be activated multiple times.
        /// </summary>
        public TheLicense License;
        /// <summary>
        /// License parameters as activated. Values are the sum of the values in the License and the values in the activation key
        /// </summary>
        public TheLicenseParameter[] ActivatedParameters;
    }

    internal class TheDefaultActivationManager : ICDEActivation
    {
        private ICDESecrets MySecrets = null;
        private ICDESystemLog MySYSLOG = null;
        public TheDefaultActivationManager(ICDESecrets pSecrets, ICDESystemLog pSysLog = null)
        {
            MySecrets = pSecrets;
            MySYSLOG = pSysLog;
        }

        static Guid GlobalThingEntitlementId = new Guid("0F86DFC7-4332-41E5-AD87-693A48F5B6F3");

        /// <summary>
        /// Generates a human-typable token (6x6 characters) that contains information required for generating an activation key.
        /// The information can be retrieved using the ParseActivationKey method
        /// </summary>
        /// <param name="SkuId"></param>
        /// <returns></returns>
        public string GetActivationRequestKey(uint SkuId)
        {
            return GetActivationRequestKey(TheBaseAssets.MyServiceHostInfo?.MyDeviceInfo?.DeviceID ?? Guid.Empty, SkuId);
        }

        /// <summary>
        /// Gets the aggregate value of a license parameter from all activated licenses for a plug-in. The value is the sum of all parameter values specified in any matching license or activation key.
        /// For more advanced logic and finegrained control, use the TheActivationManager.GetActivatedLicenses method.
        /// </summary>
        /// <param name="plugInId">Id of the plug-in for which the aggregate parameter value is to be retrieved.</param>
        /// <param name="parameterName">Name of the parameter for which the aggregate value is to be retrieved.</param>
        /// <param name="nextExpiration">Date when an activation key or license for this plug-in expires.</param>
        /// <returns>The aggregate value of the license parameter across all activated, non-expired licenses and activation keys.</returns>
        public int GetActivationParameter(Guid plugInId, string parameterName, out DateTimeOffset nextExpiration)
        {
            nextExpiration = DateTimeOffset.MaxValue;
            if (String.IsNullOrEmpty(parameterName))
                return 0;
            CheckLicenseExpiration(true);
            int parameterValue = 0;
            foreach (var activatedLicense in MyActivatedLicenses.TheValues.Where((al) =>
             !al.IsExpiredAndRemoved
             && al.License.PluginLicenses.FirstOrDefault((pl) => pl.PlugInId == plugInId) != null))
            {
                if (activatedLicense.ActivationKeyExpiration < nextExpiration)
                {
                    nextExpiration = activatedLicense.ActivationKeyExpiration;
                }
                var activatedParameter = activatedLicense.ActivatedParameters.FirstOrDefault((p) => p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
                if (activatedParameter != null)
                {
                    parameterValue += activatedParameter.Value;
                }
            }
            return parameterValue;
        }
        /// <summary>
        /// Gets all activated licenses and activation keys for a plug-in.
        /// </summary>
        /// <param name="plugInId">Id of the plug-in for which the activated licenses are to be returned.</param>
        /// <param name="includeExpired">Include expired licenses and activation keys. Typically used for troubleshooting.</param>
        /// <returns>The activated licenses for the plug-in.</returns>
        public List<TheActivatedLicense> GetActivatedLicenses(Guid plugInId, bool includeExpired = false)
        {
            CheckLicenseExpiration(true);
            if (MyActivatedLicenses == null) return null;
            var activatedLicenses = new List<TheActivatedLicense>();
            foreach (var activatedLicense in MyActivatedLicenses.TheValues.Where((al) =>
             !al.IsExpiredAndRemoved
             && al.License.PluginLicenses.FirstOrDefault((pl) => pl.PlugInId == plugInId) != null))
            {
                activatedLicenses.Add(new TheActivatedLicense(activatedLicense));
            }
            return activatedLicenses;
        }

        /// <summary>
        /// Returns true if a given SKU has an activated License
        /// </summary>
        /// <param name="SKUID"></param>
        /// <returns></returns>
        public bool IsSKUActivated(uint SKUID)
        {
            if (SKUID == 0) return false;
            CheckLicenseExpiration(true);
            return MyActivatedLicenses.TheValues.Any(s => s.License.SkuId == SKUID.ToString(CultureInfo.InvariantCulture));
        }

        public TheActivatedLicense GetLicenseActivationStatus(Guid licenseId, out DateTimeOffset activationExpiration)
        {
            activationExpiration = DateTimeOffset.MinValue;
            //bool bActive = false;
            TheActivatedLicense tAl = null;
            foreach (var activatedLicense in MyActivatedLicenses.TheValues.Where((al) => !al.IsExpiredAndRemoved && al.License.LicenseId == licenseId))
            {
                if (activatedLicense.ActivationKeyExpiration > activationExpiration)
                {
                    activationExpiration = activatedLicense.ActivationKeyExpiration;
                    tAl = activatedLicense;
                }
                //bActive = true;
            }
            return (tAl != null ? new TheActivatedLicense(tAl) : null);
        }

        static string GetActivationRequestKey(Guid deviceId, uint SkuId)
        {
            if (deviceId == Guid.Empty)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Unable to generate activation key: device id not configured.", eMsgLevel.l2_Warning, ""));
                return null;
            }
            return TheActivationKeyManager.CreateActivationRequestKey(deviceId, SkuId);
        }


        /// <summary>
        /// Checks if a plugin has an activated license and entitlements and optionally marks some entitlements as consumed.
        /// </summary>
        /// <param name="pluginId"></param>
        /// <param name="deviceType"></param>
        /// <param name="licenseAuthorities"></param>
        /// <param name="requestedThingCount"></param>
        /// <param name="useThingEntitlement"></param>
        /// <returns></returns>
        public bool CheckLicense(Guid pluginId, string deviceType, string[] licenseAuthorities, int requestedThingCount, bool useThingEntitlement = false)
        {
            if (MyLicensesPerPlugin == null) return false;
            bool result = false;
            MyLicensesPerPlugin.MyRecordsRWLock.RunUnderUpgradeableReadLock(() => // Need to block all other writers because we may update license state (entitlements) and that needs to be atomic
            {
                PluginLicenseStatus pluginStatus = MyLicensesPerPlugin.GetEntryByID(pluginId);
                if (pluginStatus != null)//LicensesPerPlugin.TryGetValue(pluginId, out pluginStatus))
                {
                    pluginStatus.ValidatePendingLicenses(this, licenseAuthorities);

                    DeviceTypeLicenseStatus deviceTypeStatus = null;

                    deviceTypeStatus = pluginStatus.DeviceTypeLicenseStatus.Find((ds) => ds.DeviceType == deviceType);
                    if (deviceTypeStatus == null)
                    {
                        deviceTypeStatus = pluginStatus.DeviceTypeLicenseStatus.Find((ds) => String.IsNullOrEmpty(ds.DeviceType));
                    }
                    if (deviceTypeStatus != null)
                    {
                        if (deviceTypeStatus.ThingEntitlements == -1)
                        {
                            result = true;
                        }
                        else if (deviceTypeStatus.ThingEntitlements >= deviceTypeStatus.ThingEntitlementsUsed + requestedThingCount)
                        {
                            if (useThingEntitlement)
                            {
                                deviceTypeStatus.ThingEntitlementsUsed += requestedThingCount;
                                MyLicensesPerPlugin.UpdateItem(pluginStatus);
                            }
                            result = true;
                        }
                        if (!result && !deviceTypeStatus.AllowGlobalThingEntitlements)
                        {
                            return;
                        }
                    }

                    if (!result)
                    {
                        if (GetGlobalThingEntitlements() >= GetGlobalThingEntitlementsUsed() + requestedThingCount)
                        {
                            if (deviceTypeStatus != null)
                            {
                                if (useThingEntitlement)
                                {
                                    deviceTypeStatus.GlobalThingEntitlementsUsed += requestedThingCount;
                                    MyLicensesPerPlugin.UpdateItem(pluginStatus);
                                }
                                result = true;
                            }
                        }
                    }
                    if (result)
                    {
                        var pinnedLicense = deviceTypeStatus?.ActivatedParameters?.FirstOrDefault(lp => lp.Name == "PinnedLicense");
                        if (pinnedLicense != null)
                        {
                            var pinnedDeviceId = MySecrets.GetPinnedDeviceId(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID, nsCDEngine.Discovery.TheNetworkInfo.GetMACAddress(false));
                            if (pinnedDeviceId != TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(4158, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheBaseApplication", "Licensing error: Activation Key was pinned to a specific machine. Detected machine as different.", eMsgLevel.l1_Error, $"Expected: {pinnedDeviceId}. Current: {TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID}"));
                                result = false;
                            }
                        }
                    }
                }
            });

            if (!TheBaseAssets.MyServiceHostInfo.RequireCDEActivation && pluginId == new Guid("{5737240C-AA66-417C-9B66-3919E18F9F4A}")) // If no license for the C-DEngine was specified, allow use of C-DEngine (change with OSS release)
                return true;

            return result;
        }

        public bool ReleaseLicense(Guid pluginId, string deviceType)
        {
            lock (MyLicensesPerPlugin)
            {
                PluginLicenseStatus pluginStatus = MyLicensesPerPlugin.GetEntryByID(pluginId);
                if (pluginStatus != null)//LicensesPerPlugin.TryGetValue(pluginId, out pluginStatus))
                {
                    DeviceTypeLicenseStatus deviceTypeStatus = null;
                    DeviceTypeLicenseStatus anyDeviceTypeStatus = null;

                    deviceTypeStatus = pluginStatus.DeviceTypeLicenseStatus.Find((ds) => ds.DeviceType == deviceType);
                    if (deviceTypeStatus != null)
                    {
                        if (deviceTypeStatus.ThingEntitlements == -1)
                        {
                            return true;
                        }
                        if (deviceTypeStatus.ThingEntitlementsUsed > 0)
                        {
                            deviceTypeStatus.ThingEntitlementsUsed--;
                            MyLicensesPerPlugin.UpdateItem(pluginStatus);
                            return true;
                        }
                    }
                    else
                    {
                        anyDeviceTypeStatus = pluginStatus.DeviceTypeLicenseStatus.Find((ds) => String.IsNullOrEmpty(ds.DeviceType));
                        if (anyDeviceTypeStatus != null)
                        {
                            if (anyDeviceTypeStatus.ThingEntitlements == -1)
                            {
                                return true;
                            }
                            if (anyDeviceTypeStatus.ThingEntitlementsUsed > 0)
                            {
                                anyDeviceTypeStatus.ThingEntitlementsUsed--;
                                MyLicensesPerPlugin.UpdateItem(pluginStatus);
                                return true;
                            }
                        }
                    }

                    if (deviceTypeStatus != null && deviceTypeStatus.GlobalThingEntitlementsUsed > 0)
                    {
                        deviceTypeStatus.GlobalThingEntitlementsUsed--;
                        MyLicensesPerPlugin.UpdateItem(pluginStatus);
                        return true;
                    }
                    if (anyDeviceTypeStatus != null && anyDeviceTypeStatus.GlobalThingEntitlementsUsed > 0)
                    {
                        anyDeviceTypeStatus.GlobalThingEntitlementsUsed--;
                        MyLicensesPerPlugin.UpdateItem(pluginStatus);
                        return true;
                    }
                }
            }
            return false;
        }

        class DeviceTypeLicenseStatus
        {
            public string DeviceType;
            public int ThingEntitlements; // -1 = no limit on number of things
            public int ThingEntitlementsUsed;
            public int GlobalThingEntitlementsUsed;
            public bool AllowGlobalThingEntitlements;
            public List<TheLicenseParameter> ActivatedParameters;
        }

        class PluginLicenseStatus : TheDataBase
        {
            public Guid PluginId;
            public List<DeviceTypeLicenseStatus> DeviceTypeLicenseStatus;
            public List<Guid> LicensesPendingSignatureCheck = new List<Guid>();

            public int GetGlobalThingEntitlementsUsed()
            {
                int globalThingEntitlementsUsed = 0;
                foreach (var deviceTypeStatus in DeviceTypeLicenseStatus)
                {
                    globalThingEntitlementsUsed += deviceTypeStatus.GlobalThingEntitlementsUsed;
                }
                return globalThingEntitlementsUsed;
            }

            public static bool ApplyActivatedLicense(TheDefaultActivationManager actMgr, TheActivatedLicense theActivatedLicense)
            {
                foreach (var pluginLicense in theActivatedLicense.License.PluginLicenses)
                {
                    PluginLicenseStatus pluginStatus = actMgr.MyLicensesPerPlugin.GetEntryByID(pluginLicense.PlugInId);
                    if (pluginStatus == null) //!LicensesPerPlugin.TryGetValue(pluginLicense.PlugInId, out pluginStatus))
                    {
                        pluginStatus = new PluginLicenseStatus { PluginId = pluginLicense.PlugInId, DeviceTypeLicenseStatus = new List<DeviceTypeLicenseStatus>() };
                        actMgr.MyLicensesPerPlugin.AddAnItem(TheCommonUtils.cdeGuidToString(pluginStatus.PluginId), pluginStatus);
                    }

                    lock (pluginStatus.LicensesPendingSignatureCheck)
                    {
                        if (!pluginStatus.LicensesPendingSignatureCheck.Contains(theActivatedLicense.cdeMID))
                        {
                            pluginStatus.LicensesPendingSignatureCheck.Add(theActivatedLicense.cdeMID);
                        }
                    }
                }
                actMgr.FireEvent(eActivationEvents.LicenseActivated, null, new TheActivatedLicense(theActivatedLicense), true);
                return true;
            }

            public static bool RemoveActivatedLicense(TheDefaultActivationManager actMgr, TheActivatedLicense theActivatedLicense)
            {
                bool bWasActivated = !theActivatedLicense.IsExpiredAndRemoved;
                theActivatedLicense.IsExpiredAndRemoved = true;
                actMgr.MyActivatedLicenses.UpdateItem(theActivatedLicense);
                foreach (var pluginLicense in theActivatedLicense.License.PluginLicenses)
                {
                    PluginLicenseStatus pluginStatus = actMgr.MyLicensesPerPlugin.GetEntryByID(pluginLicense.PlugInId);
                    if (pluginStatus != null)
                    {
                        var bActivatedForPlugIn = bWasActivated;
                        lock (pluginStatus.LicensesPendingSignatureCheck)
                        {
                            if (pluginStatus.LicensesPendingSignatureCheck.Contains(theActivatedLicense.cdeMID))
                            {
                                pluginStatus.LicensesPendingSignatureCheck.Remove(theActivatedLicense.cdeMID);
                                bActivatedForPlugIn = false;
                            }
                        }
                        if (bActivatedForPlugIn)
                        {
                            if (pluginLicense.DeviceTypes != null && pluginLicense.DeviceTypes.Count() > 0)
                            {
                                foreach (var deviceTypeToRemove in pluginLicense.DeviceTypes)
                                {
                                    pluginStatus.RemoveDeviceTypeLicense(actMgr, deviceTypeToRemove, theActivatedLicense, pluginLicense);
                                }
                            }
                            else
                            {
                                pluginStatus.RemoveDeviceTypeLicense(actMgr, "", theActivatedLicense, pluginLicense);
                            }
                            actMgr.MyLicensesPerPlugin.UpdateItem(pluginStatus);
                        }
                    }
                }
                if (actMgr.MyActivatedLicenses.MyMirrorCache.ContainsByFunc((al) => !al.IsExpiredAndRemoved))
                {
                    actMgr.FireEvent(eActivationEvents.LicenseActivationExpired, null, new TheActivatedLicense(theActivatedLicense), true);
                }
                else
                {
                    actMgr.FireEvent(eActivationEvents.LicenseExpired, null, new TheActivatedLicense(theActivatedLicense), true);
                }
                return true;
            }

            public void ValidatePendingLicenses(TheDefaultActivationManager actMgr, string[] licenseAuthorities)
            {
                if (LicensesPendingSignatureCheck == null || LicensesPendingSignatureCheck.Count == 0)
                {
                    return;
                }
                lock (LicensesPendingSignatureCheck)
                {
                    foreach (var pendingLicenseId in LicensesPendingSignatureCheck.ToList())
                    {
                        var pendingLicense = actMgr.MyActivatedLicenses.MyMirrorCache.GetEntryByID(pendingLicenseId);
                        if (pendingLicense != null)
                        {
                            bool licenseAuthorityMissing = false;
                            foreach (var plugin in pendingLicense.License.PluginLicenses)
                            {
                                if (plugin.PlugInId == PluginId)
                                {
                                    if (licenseAuthorities != null)
                                    {
                                        foreach (var licenseAuthority in licenseAuthorities)
                                        {
                                            if (pendingLicense.License.Signatures.FirstOrDefault((s) => s == licenseAuthority) == null)
                                            {
                                                licenseAuthorityMissing = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                                if (licenseAuthorityMissing)
                                {
                                    break;
                                }
                            }
                            if (!licenseAuthorityMissing)
                            {
                                LicensesPendingSignatureCheck.Remove(pendingLicenseId);
                                foreach (var pluginLicense in pendingLicense.License.PluginLicenses)
                                {
                                    if (pluginLicense.PlugInId == PluginId)
                                    {
                                        if (pluginLicense.DeviceTypes != null && pluginLicense.DeviceTypes.Count() > 0)
                                        {
                                            foreach (var deviceTypeToAdd in pluginLicense.DeviceTypes)
                                            {
                                                AddDeviceTypeLicense(deviceTypeToAdd, pendingLicense, pluginLicense);
                                            }
                                        }
                                        else
                                        {
                                            AddDeviceTypeLicense("", pendingLicense, pluginLicense);
                                        }
                                    }
                                }
                                actMgr.MyLicensesPerPlugin.UpdateItem(this);
                            }
                        }
                    }
                }
            }

            void AddDeviceTypeLicense(string deviceType, TheActivatedLicense license, ThePluginLicense pluginLicense)
            {
                if (!String.IsNullOrEmpty(deviceType) && PluginId == GlobalThingEntitlementId)
                {
                    return; // Can't add device types to the global license
                }
                DeviceTypeLicenseStatus deviceStatus = DeviceTypeLicenseStatus.Find((ds) => ds.DeviceType == deviceType);
                if (deviceStatus == null)
                {
                    deviceStatus = new DeviceTypeLicenseStatus
                    {
                        DeviceType = deviceType,
                        ThingEntitlements = -1,
                        ThingEntitlementsUsed = 0,
                        GlobalThingEntitlementsUsed = 0,
                        ActivatedParameters = new List<TheLicenseParameter>(license.ActivatedParameters.Select(lp => lp.Clone())),
                    };
                    DeviceTypeLicenseStatus.Add(deviceStatus);
                }
                else
                {
                    if (deviceStatus.ActivatedParameters == null)
                        deviceStatus.ActivatedParameters = new List<TheLicenseParameter>();
                    deviceStatus.ActivatedParameters.AddRange(license.ActivatedParameters.Select(lp => lp.Clone()));
                }
                var thingEntitlementParameter = license.ActivatedParameters.FirstOrDefault((p) => String.Equals(p.Name, TheLicenseParameter.ThingEntitlements, StringComparison.OrdinalIgnoreCase));
                if (thingEntitlementParameter != null)
                {
                    if (deviceStatus.ThingEntitlements == -1)
                    {
                        deviceStatus.ThingEntitlements = 0;
                    }
                    deviceStatus.ThingEntitlements += thingEntitlementParameter.Value; // TODO Use naming convention here to map parameters to the global connection mechanism?
                }
                if (pluginLicense.AllowGlobalThingEntitlements)
                {
                    deviceStatus.AllowGlobalThingEntitlements = true;
                }
            }
            void RemoveDeviceTypeLicense(TheDefaultActivationManager actMgr, string deviceType, TheActivatedLicense license, ThePluginLicense pluginLicense)
            {
                if (!String.IsNullOrEmpty(deviceType) && PluginId == GlobalThingEntitlementId)
                {
                    return; // Can't add device types to the global license
                }
                DeviceTypeLicenseStatus deviceStatus = DeviceTypeLicenseStatus.Find((ds) => ds.DeviceType == deviceType);
                if (deviceStatus == null)
                {
                    return;
                }
                var thingEntitlementParameter = license.ActivatedParameters.FirstOrDefault((p) => String.Equals(p.Name, TheLicenseParameter.ThingEntitlements, StringComparison.OrdinalIgnoreCase));
                if (thingEntitlementParameter != null)
                {
                    if (deviceStatus.ThingEntitlements == -1)
                    {
                        deviceStatus.ThingEntitlements = 0;
                    }
                    if (deviceStatus.ThingEntitlements > thingEntitlementParameter.Value)
                    {
                        deviceStatus.ThingEntitlements -= thingEntitlementParameter.Value;
                    }
                    else
                    {
                        deviceStatus.ThingEntitlements = 0;
                    }
                    if (deviceStatus.ThingEntitlementsUsed > deviceStatus.ThingEntitlements)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(10000, new TSM(eEngineName.ThingService, "License expired, entitlements exceeded!", eMsgLevel.l1_Error, String.Format("Entitlement for {0}/{1}: used {2}, allowed {3}. Due to expired activation key for License: {4}. Expiration: {5}. Key Hash: {6}",
                            pluginLicense.PlugInId, deviceType, deviceStatus.ThingEntitlementsUsed, deviceStatus.ThingEntitlements,
                            license.License.LicenseId, license.ActivationKeyExpiration, license.ActivationKeyHash)));
                    }
                }
                if (pluginLicense.AllowGlobalThingEntitlements)
                {
                    // Find all other licenses and reset only if no other license allows global entitlements
                    deviceStatus.AllowGlobalThingEntitlements = false;
                    foreach (var l2 in actMgr.MyActivatedLicenses.TheValues)
                    {
                        if (!l2.IsExpiredAndRemoved)
                        {
                            foreach (var pl in l2.License.PluginLicenses)
                            {
                                if (pl.DeviceTypes.FirstOrDefault((d) => d == deviceType) != null && pl.AllowGlobalThingEntitlements)
                                {
                                    deviceStatus.AllowGlobalThingEntitlements = true;
                                }
                            }
                        }
                    }
                    if (!deviceStatus.AllowGlobalThingEntitlements && deviceStatus.GlobalThingEntitlementsUsed > 0)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(10000, new TSM(eEngineName.ThingService, "License expired, entitlements exceeded!", eMsgLevel.l1_Error, String.Format("Entitlement for {0}/{1}: global entitlements used {2}, no longer allowed. Due to expired activation key for License: {3}. Expiration: {4}. Key Hash: {5}",
                            pluginLicense.PlugInId, deviceType, deviceStatus.GlobalThingEntitlementsUsed,
                            license.License.LicenseId, license.ActivationKeyExpiration, license.ActivationKeyHash)));
                    }
                }
                foreach (var lpToRemove in license.ActivatedParameters)
                {
                    var itemToRemove = deviceStatus.ActivatedParameters.FirstOrDefault(lp => lp.Name == lpToRemove.Name && lp.Value == lpToRemove.Value);
                    deviceStatus.ActivatedParameters.Remove(itemToRemove);
                }
            }
        }

        // Global licenses have plugin Id == GlobalThingEntitlementId
        int GetGlobalThingEntitlements()
        {
            int globalThingEntitlements = 0;

            PluginLicenseStatus globalLicenseStatus = MyLicensesPerPlugin.GetEntryByID(GlobalThingEntitlementId);

            if (globalLicenseStatus != null) //.TryGetValue(Guid.Empty, out globalLicenseStatus))
            {
                if (globalLicenseStatus.DeviceTypeLicenseStatus != null && globalLicenseStatus.DeviceTypeLicenseStatus.Count > 0)
                {
                    globalThingEntitlements += globalLicenseStatus.DeviceTypeLicenseStatus[0].ThingEntitlements;
                }
            }
            return globalThingEntitlements;
        }


        int GetGlobalThingEntitlementsUsed()
        {
            int globalThingsEntitlementsUsed = 0;
            lock (MyLicensesPerPlugin)
            {
                foreach (var pluginStatus in MyLicensesPerPlugin.TheValues)
                {
                    if (pluginStatus.PluginId != GlobalThingEntitlementId)
                    {
                        globalThingsEntitlementsUsed += pluginStatus.GetGlobalThingEntitlementsUsed();
                    }
                }
            }
            return globalThingsEntitlementsUsed;
        }


        public List<TheLicense> GetInstalledLicenses()
        {
            LoadAvailableLicenses();
            return allLicenses.Select((l) => new TheLicense(l)).ToList();
        }

        readonly List<TheLicense> allLicenses = new List<TheLicense>(); // TODO Turn this into storage mirror/initialize it etc.

        /// <summary>
        ///
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        bool ValidateAndAddLicense(string path)
        {
            try
            {
                var license = TheCommonUtils.DeserializeJSONStringToObject<TheLicense>(File.ReadAllText(path));    //CODE-REVIEW: path must be validated with cdeFixupFileName
                if (license != null)
                {
                    var previousLicense = allLicenses.Find((l) => l.LicenseId == license.LicenseId);
                    if (previousLicense == null || TheCommonUtils.CDbl(previousLicense.LicenseVersion) < TheCommonUtils.CDbl(license.LicenseVersion))
                    {
                        if (license.ValidateSignature(MySecrets.GetLicenseSignerPublicKeys()))
                        {
                            if (previousLicense != null)
                            {
                                allLicenses.Remove(previousLicense);
                            }
                            allLicenses.Add(license);
                            TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ThingService, "Found valid license", eMsgLevel.l4_Message, String.Format("{0}", license.LicenseId)));

                            var supercededLicenses = MyActivatedLicenses.MyMirrorCache.GetEntriesByFunc((al) => al.License.LicenseId == license.LicenseId && TheCommonUtils.CDbl(al.License.LicenseVersion) < TheCommonUtils.CDbl(license.LicenseVersion));
                            foreach (var supercededLicense in supercededLicenses)
                            {
                                if (supercededLicense.ActivationKeyHash == "eval")
                                {
                                    PluginLicenseStatus.RemoveActivatedLicense(this, supercededLicense);
                                    MyActivatedLicenses.MyMirrorCache.RemoveAnItem(supercededLicense);
                                }
                                else
                                {
                                    PluginLicenseStatus.RemoveActivatedLicense(this, supercededLicense);

                                    supercededLicense.License = license;
                                    MyActivatedLicenses.UpdateItem(supercededLicense);

                                    PluginLicenseStatus.ApplyActivatedLicense(this, supercededLicense);

                                    //licenseCount++;
                                    TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "License activated", eMsgLevel.l4_Message, String.Format("Applied superceded license {0} with previously applied activation key hash {1}", supercededLicense.License.LicenseId, supercededLicense.ActivationKeyHash)));
                                }
                            }

                            var evalPeriod = license.Properties?.FirstOrDefault((p) => p.Name == "Eval Period");
                            if (evalPeriod != null && TheCommonUtils.CInt(evalPeriod.Value) > 0)
                            {
                                if (MyActivatedLicenses.MyMirrorCache.GetEntryByFunc((al) => al.License.LicenseId == license.LicenseId && al.ActivationKeyHash == "eval") == null)
                                {
                                    var expiration = DateTimeOffset.Now + new TimeSpan(TheCommonUtils.CInt(evalPeriod.Value), 0, 0, 0);
                                    if (expiration > license.Expiration)
                                    {
                                        expiration = license.Expiration;
                                    }
                                    var activatedLicense = new TheActivatedLicense
                                    {
                                        ActivatedParameters = license.Parameters,
                                        ActivationKeyExpiration = expiration,
                                        ActivationKeyHash = "eval",
                                        License = license,
                                    };
                                    MyActivatedLicenses.AddAnItem(activatedLicense);
                                    PluginLicenseStatus.ApplyActivatedLicense(this, activatedLicense);
                                    TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Auto License activated", eMsgLevel.l4_Message, String.Format("Activated license {0} with auto license for {1} days.", activatedLicense.License.LicenseId, TheCommonUtils.CInt(evalPeriod.Value))));
                                }
                            }
                            return true;
                        }
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Found license with invalid signature", eMsgLevel.l2_Warning, String.Format("{0}", license.LicenseId)));
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }
            return false;
        }

        bool LoadAvailableLicenses()
        {
            // CODEREVIEW/TODO Revisit licensing deployment and discovery mechanism; for now just load all CDEL files; deploy using CDEX mechanism
            //CODEREVIEW: Better Stubbing for Mobile Devices <= dont need Activation due to Store Logic
            try
            {
                var licenseFiles = GetAllLicenseFileNames();
                foreach (var licenseFile in licenseFiles)
                {
                    ValidateAndAddLicense(licenseFile);
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    CheckLicenseExpiration();
                }
                catch { }
            }
            return true;
        }

        public bool CheckForPinnedLicense()
        {
            try
            {
                var licenseFiles = GetAllLicenseFileNames();
                foreach (var licenseFile in licenseFiles)
                {
                    var license = TheCommonUtils.DeserializeJSONStringToObject<TheLicense>(File.ReadAllText(licenseFile));    //CODE-REVIEW: path must be validated with cdeFixupFileName
                    if (license.Parameters.FirstOrDefault(p => p.Name == "PinnedLicense") != null)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private List<string> GetAllLicenseFileNames()
        {
            var baseDir = TheBaseAssets.MyServiceHostInfo.BaseDirectory;
            var licenseFiles = new List<string>();
            TheISMManager.ProcessDirectory(new DirectoryInfo(baseDir), ref licenseFiles, "", ".CDEL", false, TheBaseAssets.MyServiceHostInfo.IsCloudService);
            return licenseFiles;
        }

#if false
        #region duplicate from ISM Manager
        private static void ProcessDirectory(DirectoryInfo di, ref List<string> tw, string relativeCurrentDir, string pExtension, bool IsUsb, bool DoProcessSubDirs)
        {
            try
            {
                if (!Directory.Exists(di.FullName))
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

        private static void ProcessDirectory(DirectoryInfo di, ref List<TheFileInfo> tw, string relativeCurrentDir, string pExtension, bool IsUsb, bool DoProcessSubDirs)
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
        #endregion
#endif
        public bool ApplyActivationKey(string activationKeyString)
        {
            return ApplyActivationKey(activationKeyString, Guid.Empty, out _);
        }

        public bool ApplyActivationKey(string activationKeyString, Guid licenseId, out DateTimeOffset expirationDate)
        {
            expirationDate = DateTimeOffset.MinValue;
            LoadAvailableLicenses();

            //SECURITY-REVIEW: remain backwards compat missing APPID
            var errors = TheActivationKeyManager.ValidateActivationkey(MySecrets, activationKeyString, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID, allLicenses, out List<TheLicenseActivationInformation> activatedLicenseInfos, out expirationDate);
            if (errors == null)
            {
                if (licenseId != Guid.Empty && activatedLicenseInfos.FirstOrDefault((al) => al.License.LicenseId == licenseId) == null)
                {
                    return false;
                }
                List<TheActivatedLicense> activatedLicenses = new List<TheActivatedLicense>();
                foreach (var licenseInfo in activatedLicenseInfos)
                {
                    activatedLicenses.Add(new TheActivatedLicense
                    {
                        ActivatedParameters = licenseInfo.ActivatedParameters,
                        ActivationKeyExpiration = licenseInfo.ActivationKeyExpiration,
                        ActivationKeyHash = licenseInfo.ActivationKeyHash,
                        License = licenseInfo.License,
                    });
                }
                int licenseCount = 0;
                foreach (var activatedLicense in activatedLicenses)
                {
                    if (MyActivatedLicenses.MyMirrorCache.GetEntryByFunc((al) => al.License.LicenseId == activatedLicense.License.LicenseId && al.ActivationKeyHash == activatedLicense.ActivationKeyHash) == null)
                    {
                        MyActivatedLicenses.AddAnItem(activatedLicense);
                        PluginLicenseStatus.ApplyActivatedLicense(this, activatedLicense);
                        licenseCount++;
                        TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "License activated", eMsgLevel.l4_Message, String.Format("Activated license {0} with activation key {1}", activatedLicense.License.LicenseId, activationKeyString)));
                    }
                }
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Activation key applied", eMsgLevel.l3_ImportantMessage, String.Format("Activated {0} additional licenses with activation key {1}", licenseCount, activationKeyString)));
                return true;
            }
            if (errors.Length > 1)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, errors[0], eMsgLevel.l2_Warning, errors[1]));
            }
            else
            {
                TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Internal error applying activation key", eMsgLevel.l1_Error, "No orincomplete error information"));
            }
            return false;
        }


        TheStorageMirror<TheActivatedLicense> MyActivatedLicenses;
        TheStorageMirror<PluginLicenseStatus> MyLicensesPerPlugin;

        public bool InitializeLicenses()
        {
            {
                var activatedLicenses = new TheStorageMirror<TheActivatedLicense>(TheCDEngines.MyIStorageService)
                {
                    CacheTableName = "TheActivatedLicenses",
                    IsRAMStore = true,
                    CacheStoreInterval = TheBaseAssets.MyServiceHostInfo.ThingRegistryStoreInterval,
                    IsStoreIntervalInSeconds = true,
                    IsCachePersistent = !TheCommonUtils.IsHostADevice(), // TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay,
                    UseSafeSave = true,
                    AllowFireUpdates = false,
                    IsCacheEncrypted = true,
                    BlockWriteIfIsolated = true
                };
                // CODE REVIEW: Which nodes need licenseing?
                TheStorageMirrorParameters tParams = new TheStorageMirrorParameters { CanBeFlushed = false, ResetContent = TheBaseAssets.MyServiceHostInfo.IsNewDevice, LoadSync = true, DontRegisterInMirrorRepository = false };
                activatedLicenses.InitializeStore(tParams); // false, TheBaseAssets.MyServiceHostInfo.IsNewDevice, true, false);

                MyActivatedLicenses = activatedLicenses;
            }
            {
                var licensesPerPlugin = new TheStorageMirror<PluginLicenseStatus>(TheCDEngines.MyIStorageService)
                {
                    CacheTableName = "TheLicensesByPlugin",
                    IsRAMStore = true,
                    CacheStoreInterval = TheBaseAssets.MyServiceHostInfo.ThingRegistryStoreInterval,
                    IsStoreIntervalInSeconds = true,
                    IsCachePersistent = !TheCommonUtils.IsHostADevice(), // TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay,
                    UseSafeSave = true,
                    AllowFireUpdates = false,
                    IsCacheEncrypted = true,
                    BlockWriteIfIsolated = true
                };
                // CODE REVIEW: Which nodes need licenseing?
                TheStorageMirrorParameters tParams = new TheStorageMirrorParameters { CanBeFlushed = false, ResetContent = TheBaseAssets.MyServiceHostInfo.IsNewDevice, LoadSync = true, DontRegisterInMirrorRepository = false };
                licensesPerPlugin.InitializeStore(tParams); // false, TheBaseAssets.MyServiceHostInfo.IsNewDevice, true, false);

                MyLicensesPerPlugin = licensesPerPlugin;
            }

            if (MyLicensesPerPlugin.Count == 0 && MyActivatedLicenses.Count > 0)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(10000, new TSM(eEngineName.ThingService, "Error ", eMsgLevel.l2_Warning, $"Found no license index, but {MyActivatedLicenses.Count} activated licenses. Reapplying activated licenses."));
                foreach (var activatedLicense in MyActivatedLicenses.TheValues)
                {
                    PluginLicenseStatus.ApplyActivatedLicense(this, activatedLicense);
                }
            }

            if (TheBaseAssets.MyServiceHostInfo.ActivationKeysToAdd != null)
            {
                foreach (var activationKey in TheBaseAssets.MyServiceHostInfo.ActivationKeysToAdd)
                {
                    ApplyActivationKey(activationKey);
                }
            }

            LoadAvailableLicenses();

            var distinctActivatedLicenses = MyActivatedLicenses.TheValues.Where(l => !l.IsExpiredAndRemoved).Select(l => l.License.LicenseId).Distinct();
            if (allLicenses.Count > distinctActivatedLicenses.Count())
            {
                var unactivatedLicenses = allLicenses.Where(l => !distinctActivatedLicenses.Contains(l.LicenseId));

                foreach (var unactivatedLicense in unactivatedLicenses)
                {
                    var activationRequestKey = GetActivationRequestKey((uint)TheCommonUtils.CInt(unactivatedLicense.SkuId));
                    if (activationRequestKey != null)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(10000, new TSM(eEngineName.ThingService, "Found unactivated License", eMsgLevel.l2_Warning, $"Request activation key for this device using Request Key: {activationRequestKey}. [License: {unactivatedLicense.Description} - {unactivatedLicense.LicenseId}]"));
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(10000, new TSM(eEngineName.ThingService, "Found unactivated License", eMsgLevel.l1_Error, $"Unable to generate Request Key. License {unactivatedLicense.Description} - {unactivatedLicense.LicenseId}"));
                    }
                }
                if (TheBaseAssets.MyServiceHostInfo.ShutdownOnLicenseFailure)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(10000, new TSM(eEngineName.ThingService, "Shutting down as configured, due to unactivated licenses", eMsgLevel.l1_Error));
                    return false;
                }
            }
            return true;
        }
        static readonly TimeSpan ExpirationWarningInterval = new TimeSpan(30, 0, 0, 0);

        bool CheckLicenseExpiration(bool bSupressLog = false)
        {
            if (MyActivatedLicenses == null)
                return false;
            foreach (var activeLicense in MyActivatedLicenses.TheValues)
            {
                if (activeLicense.ActivationKeyExpiration < DateTimeOffset.Now)
                {
                    PluginLicenseStatus.RemoveActivatedLicense(this, activeLicense);
                    TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) || bSupressLog ? null : new TSM(eEngineName.ThingService, "License expired!", eMsgLevel.l2_Warning, $"Expired activation key for License '{activeLicense.License.Description}' ({activeLicense.License.LicenseId}): Expiration: {activeLicense.ActivationKeyExpiration}. Key Hash: {activeLicense.ActivationKeyHash}. [ Request Key: {GetActivationRequestKey(TheCommonUtils.CUInt(activeLicense.License.SkuId))} ]"));
                }
                else
                {
                    var cdeVer = TheCommonUtils.CDbl(activeLicense.License.CDEVersion);
                    var cdeVerMin = TheCommonUtils.CDbl(activeLicense.License.CDEVersionMin);

                    if (cdeVer != 0 && (TheBaseAssets.CurrentVersion > cdeVer || (cdeVerMin == 0 && TheBaseAssets.CurrentVersion < cdeVer)))
                    {
                        PluginLicenseStatus.RemoveActivatedLicense(this, activeLicense);
                        TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) || bSupressLog ? null : new TSM(eEngineName.ThingService, "License not valid for this C-DEngine version", eMsgLevel.l2_Warning, $"License '{activeLicense.License.Description}' ({activeLicense.License.LicenseId}) allows maximum C-DEngine version: {cdeVer}. Running version: {TheBaseAssets.CurrentVersion}"));
                        continue;
                    }

                    if (cdeVerMin != 0 && TheBaseAssets.CurrentVersion < cdeVerMin)
                    {
                        PluginLicenseStatus.RemoveActivatedLicense(this, activeLicense);
                        TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) || bSupressLog ? null : new TSM(eEngineName.ThingService, "License not valid for this C-DEngine version: C-DEngine too old", eMsgLevel.l2_Warning, $"License '{activeLicense.License.Description}' ({activeLicense.License.LicenseId}) requires minimum C-DEngine version: {cdeVerMin}. Running version: {TheBaseAssets.CurrentVersion}"));
                        continue;
                    }
                    if (activeLicense.ActivationKeyExpiration < DateTimeOffset.Now + ExpirationWarningInterval)
                    {
                        // about to expire
                        TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) || bSupressLog ? null : new TSM(eEngineName.ThingService, "License will expire soon!", eMsgLevel.l2_Warning, $"Activation key for License '{activeLicense.License.Description}' ({activeLicense.License.LicenseId}): Expiration: {activeLicense.ActivationKeyExpiration}. Key Hash: {activeLicense.ActivationKeyHash}. [ Request Key: {GetActivationRequestKey(TheCommonUtils.CUInt(activeLicense.License.SkuId))} ]"));
                    }

                    if (activeLicense.IsExpiredAndRemoved)
                    {
                        activeLicense.IsExpiredAndRemoved = false;
                        MyActivatedLicenses.UpdateItem(activeLicense);
                        TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) || bSupressLog ? null : new TSM(eEngineName.ThingService, "License reinstantiated!", eMsgLevel.l2_Warning, $"Activation key for License '{activeLicense.License.Description}' ({activeLicense.License.LicenseId}): Expiration: {activeLicense.ActivationKeyExpiration}. Key Hash: {activeLicense.ActivationKeyHash} marked as valid."));
                        // Was previously found to be expired (likely due to clock erroneously set to the future): reinstantiate
                        PluginLicenseStatus.ApplyActivatedLicense(this, activeLicense);
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.OFF) || bSupressLog ? null : new TSM(eEngineName.ThingService, "Activated License found", eMsgLevel.l3_ImportantMessage, $"License '{activeLicense.License.Description}' ({activeLicense.License.LicenseId}): Expiration: {activeLicense.ActivationKeyExpiration} {activeLicense.ActivationKeyExpiration.Year}. Key Hash: {activeLicense.ActivationKeyHash}. [ Request Key: {GetActivationRequestKey(TheCommonUtils.CUInt(activeLicense.License.SkuId))} ]"));
                }
            }
            return false;
        }

        public bool CreateDefaultLicense(Guid pluginId)
        {
            if (allLicenses?.Find((l) => l.PluginLicenses.FirstOrDefault((pl) => pl.PlugInId == pluginId) != null) != null)
            {
                return false;
            }
            TheBaseAssets.MySYSLOG.WriteToLog(10000, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ThingService, "Created default license", eMsgLevel.l4_Message, String.Format("Plug-in ID {0}", pluginId)));
            allLicenses?.Add(GetDefaultLicense(pluginId));
            return true;
        }

        static TheLicense GetDefaultLicense(Guid pluginId)
        {
            string pluginName;

            var engine = TheThingRegistry.GetBaseEngines(false).Find((e) => e.GetEngineID() == pluginId);
            if (engine != null)
            {
                pluginName = engine.GetEngineName();
            }
            else
            {
                pluginName = pluginId.ToString();
            }

            return new TheLicense
            {
                LicenseId = pluginId,
                Description = String.Format("Default license for {0}", pluginName),
                PluginLicenses = new[]
                {
                    new ThePluginLicense
                    {
                        PlugInId = pluginId,
                    },
                },
                Expiration = DateTimeOffset.MaxValue,
                Parameters = new TheLicenseParameter[]
                {
                    //new LicenseParameter { Name = "cdeConn", Value = 0 },
                },
            };
        }


        public void WaitForLicenseActivated(Guid licenseId, Action<object, object> callback)
        {
            void localCallback(object sender, object licenseObj)
            {
                if (licenseObj is TheActivatedLicense al && (licenseId == Guid.Empty || al.License.LicenseId == licenseId)) //REVIEW: Du hattest : (al != null && licenseId == Guid.Empty || al.License.LicenseId == licenseId) das macht keinen sinn...
                {
                    callback(null, al); //REVIEW: Alle Callbacks brauchen AL
                    UnregisterEvent(eActivationEvents.LicenseActivated, localCallback);
                }
            }

            RegisterEvent(eActivationEvents.LicenseActivated, localCallback);

            var al2 = GetLicenseActivationStatus(licenseId, out DateTimeOffset expiration);
            if (al2 != null)
            {
                localCallback(null, al2); // new TheActivatedLicense { License = new TheLicense { LicenseId = licenseId } });    //TODO-MARKUS: Hier muss eine komplette AL zurueckkommen nicht nur eine leere AL mit LicenseID
            }
        }

        /// <summary>
        /// Returns a task that will be completed when the license has been activated or is already activated.
        /// </summary>
        /// <param name="licenseId">ID of the license on which to wait. Guid.Empty waits for any license.</param>
        /// <returns></returns>
        public Task<TheActivatedLicense> WaitForLicenseActivatedAsync(Guid licenseId)
        {
            var tcs = new TaskCompletionSource<TheActivatedLicense>();  //REVIEW: Alle Callbacks brauchen AL

            WaitForLicenseActivated(licenseId, (sender, lid) =>
            {
                tcs.SetResult(lid as TheActivatedLicense);
            });
            return tcs.Task;
        }


        #region Event Handling

        private readonly TheCommonUtils.RegisteredEventHelper<object, object> MyRegisteredEvents = new TheCommonUtils.RegisteredEventHelper<object, object>();

        /// <summary>
        /// Removes all Events from TheThing
        /// </summary>
        public void ClearAllEvents()
        {
            MyRegisteredEvents.ClearAllEvents();
        }
        /// <summary>
        /// Registers a new Event with TheThing
        /// New Events can be registerd and Fired at Runtime
        /// </summary>
        /// <param name="pEventName">Name of the Event to Register</param>
        /// <param name="pCallback">Callback called when the event fires</param>
        public void RegisterEvent(string pEventName, Action<object, object> pCallback)
        {
            MyRegisteredEvents.RegisterEvent(pEventName, pCallback);
        }

        /// <summary>
        /// Unregisters a callback for a given Event Name
        /// </summary>
        /// <param name="pEventName">Name of the Event to unregister</param>
        /// <param name="pCallback">Callback to unregister</param>
        public void UnregisterEvent(string pEventName, Action<object, object> pCallback)
        {
            MyRegisteredEvents.UnregisterEvent(pEventName, pCallback);
        }

        /// <summary>
        /// Fires the given Event.
        /// Every TheThing can register and Fire any Event on any event.
        /// New Events can be defined at Runtime, registered and fired
        /// </summary>
        /// <param name="pEventName">Name of the Event to Fire</param>
        /// <param name="sender">this pointer or any other ICDETHing that will be handed to the callback. If set to null, "this" will be used </param>
        /// <param name="pPara">Parameter to be handed with the Event</param>
        /// <param name="FireAsync">If set to true, the callback is running on a new Thread</param>
        public void FireEvent(string pEventName, ICDEThing sender, object pPara, bool FireAsync)
        {
            MyRegisteredEvents.FireEvent(pEventName, sender, pPara, FireAsync);
        }

        /// <summary>
        /// Returns true if the event specified exists in the Eventing System of TheThing
        /// </summary>
        /// <param name="pEventName"></param>
        /// <returns></returns>
        public bool HasRegisteredEvents(string pEventName)
        {
            return MyRegisteredEvents.HasRegisteredEvents(pEventName);
        }
        #endregion


    }


}
