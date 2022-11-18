// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Discovery;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Interfaces;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;

namespace nsCDEngine.ISM
{
    /// <summary>
    /// The C-DEngine internal Settings class.
    /// </summary>
    public class TheCDESettings : TheDataBase, ICDESettings
    {
        /// <summary>
        /// Sets a new App Setting that will be stored encrypted in the local cdeTPI
        /// </summary>
        /// <param name="pSetting">Name of the key</param>
        /// <param name="pValue">Value to be stored</param>
        /// <param name="bIsHidden">If true, the parameter will be treated as secure and cannot be recalled except by the CDE</param>
        /// <param name="pOwner">Owner of the setting</param>
        /// <param name="pType">Type of the property as a hint for NMI Settings</param>
        /// <returns></returns>
        public virtual bool SetSetting(string pSetting, string pValue, bool bIsHidden = false, Guid? pOwner = null, ePropertyTypes pType = ePropertyTypes.TString)
        {
            return UpdateLocalSettings(new List<aCDESetting> { new aCDESetting { Name = pSetting, Value = pOwner != null ? CU.cdeEncrypt(pValue, TheBaseAssets.MySecrets.GetNodeKey()) : pValue, IsHidden = bIsHidden, ValueType = pType, cdeO = pOwner } });
        }

        /// <summary>
        /// Stores a list of new settings in the local cdeTPI
        /// </summary>
        /// <param name="pSettings">Structure for storing values</param>
        /// <returns></returns>
        public virtual bool SetSettings(List<aCDESetting> pSettings)
        {
            return UpdateLocalSettings(pSettings);
        }

        /// <summary>
        /// Returns a setting either known in the current Settings array or set as an environment variable or in App.Config
        /// First it looks into App.config and if this does not contain the setting it falls back to the existing TheBaseAssets.MyCmdArgs and if that does not have the entry it looks in the environment variables.
        /// </summary>
        /// <param name="pSetting">Key of the setting</param>
        /// <param name="pOwner">Owner Guid of the setting. gives access to a private setting</param>
        /// <returns></returns>
        public virtual string GetSetting(string pSetting, Guid? pOwner = null)
        {
            var tres = GetArgOrEnv(TheBaseAssets.MyCmdArgs, pSetting, pOwner);
            if (pOwner != null)
            {
                try
                {
                    var tres2 = CU.cdeDecrypt(tres, TheBaseAssets.MySecrets.GetNodeKey(), true);
                    if (!string.IsNullOrEmpty(tres2))
                        tres = tres2;
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            return tres;
        }

        /// <summary>
        /// Gets a setting from the Settings Factory of the C-DEngine
        /// </summary>
        /// <param name="pSetting">Key of the setting</param>
        /// <param name="alt">If not found, this function returns this alternative</param>
        /// <param name="IsEncrypted">True if the key is encrypted in the app.config</param>
        /// <param name="IsAltDefault">returns alt if key exists but is null or empty</param>
        /// <param name="pOwner">Owner Guid of the setting. gives access to a private setting</param>
        /// <returns></returns>
        public virtual string GetAppSetting(string pSetting, string alt, bool IsEncrypted, bool IsAltDefault = false, Guid? pOwner = null)
        {
            var settingsValue = GetArgOrEnv(TheBaseAssets.MyCmdArgs, pSetting, pOwner);
            if (settingsValue != null)
            {
                return settingsValue;
            }

#if !CDE_STANDARD //No App.Config
            string tUID = CU.CStr(ConfigurationManager.AppSettings[pSetting]);
#else
            string tUID = alt;
            var appSettings = GetAppSettingsObject();
            if (appSettings != null)
            {
                tUID = CU.CStr(appSettings[pSetting]);
            }
#endif
            if (IsEncrypted && !string.IsNullOrEmpty(tUID))
            {
                tUID = CU.cdeDecrypt(tUID, TheBaseAssets.MySecrets.GetAI());
            }
            if (IsAltDefault && string.IsNullOrEmpty(tUID))
            {
                tUID = alt;
            }
            return tUID;
        }

        /// <summary>
        /// Returns a list of hidden keys for NMI and other plugins
        /// </summary>
        /// <returns></returns>
        public virtual List<string> GetHiddenKeyList()
        {
            return HiddenSettings.ToList();
        }

        /// <summary>
        /// Determines whether the specified p setting has setting.
        /// </summary>
        /// <param name="pSetting">The p setting.</param>
        /// <param name="pOwner">The p owner.</param>
        /// <returns><c>true</c> if the specified p setting has setting; otherwise, <c>false</c>.</returns>
        public virtual bool HasSetting(string pSetting, Guid? pOwner = null)
        {
            return !string.IsNullOrEmpty(GetArgOrEnv(TheBaseAssets.MyCmdArgs, pSetting, pOwner));
        }

        /// <summary>
        /// Deletes an app.config setting if the C-DEngine can write to the config file
        /// </summary>
        /// <param name="pKeyname">Key to be deleted</param>
        /// <param name="pOwner">Owner of the setting</param>
        public virtual void DeleteAppSetting(string pKeyname, Guid? pOwner = null)
        {
            if (TheBaseAssets.MyServiceHostInfo?.IsCloudService == true || TheBaseAssets.MyServiceHostInfo?.UseRandomDeviceID == true || TheBaseAssets.MyServiceHostInfo?.IsIsolated == true) //We must not change settings on Read-Only nodes
                return;
            try
            {
                if (CU.IsMeadowFeather()) return;
                var tConfig = TheBaseAssets.MyApplication?.GetApplicationConfig();
                if (tConfig == null) return;

                var tSettings = tConfig.AppSettings;
                if (tSettings != null)
                {
                    tSettings.Settings.Remove(pKeyname);
#if !CDE_STANDARD
                    tConfig.Save(ConfigurationSaveMode.Modified);
#else
                    tConfig.Save(0);
#endif
                }
                if (pOwner != null && TheBaseAssets.MyCmdArgs.ContainsKey(pKeyname))
                {
                    SetSetting(pKeyname, TheBaseAssets.MyCmdArgs[pKeyname], true, pOwner);
                    TheBaseAssets.MyCmdArgs.Remove(pKeyname);
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(7678, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CommonUtils", "Delete AppSetting Failed for: " + pKeyname, eMsgLevel.l1_Error, e.ToString()));
            }
        }

        /// <summary>
        /// Updates the local cdeTPI file with the current HSI settings
        /// </summary>
        /// <param name="pSettings">Any Additional Settings to be updated</param>
        /// <returns></returns>
        public virtual bool UpdateLocalSettings(List<aCDESetting> pSettings = null)
        {
            if (TheBaseAssets.MyServiceHostInfo.IsCloudService || TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID)
                return false;
            if (TheBaseAssets.MyServiceHostInfo.IsIsolated)
            {
                TheBaseAssets.MyServiceHostInfo.ServiceRoute = $"ws://localhost:{TheBaseAssets.MyServiceHostInfo.MyStationWSPort}";
                TheBaseAssets.MyServiceHostInfo.IsCloudDisabled = false;
                TheBaseAssets.MyServiceHostInfo.RequiresConfiguration = false;
                TheBaseAssets.MyServiceHostInfo.MyStationName = $"{TheBaseAssets.MyServiceHostInfo.MyStationName} {TheBaseAssets.MyCmdArgs["ISOEN"]}";
                return false;
            }

            try
            {
                //step 1: Read from disk previous settings
                Dictionary<string, string> tSettings= new Dictionary<string, string>();
                if (!TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID)
                {
                    var tpiFile = CU.cdeFixupFileName("cache\\TheProvInfo.cdeTPI", true);
                    if (File.Exists(tpiFile))    
                    {
                        byte[] tBuf = File.ReadAllBytes(tpiFile);
                        tSettings = TheBaseAssets.MyCrypto.DecryptKV(tBuf);
                    }
                }

                //step 2: update settings with the latest configured settings
                if (tSettings.ContainsKey("ServiceRoute") || !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ServiceRoute))
                    tSettings["ServiceRoute"] = TheBaseAssets.MyServiceHostInfo.ServiceRoute;
                tSettings["IsCloudDisabled"] = TheBaseAssets.MyServiceHostInfo.IsCloudDisabled.ToString();
                if (!tSettings.ContainsKey("IsUsingUserMapper") || !CU.CBool(tSettings.ContainsKey("IsUsingUserMapper"))) //If IsUsingUserMapper was previously set to true - do not allow reset!
                    tSettings["IsUsingUserMapper"] = TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper.ToString();
                if (!tSettings.ContainsKey("DontVerifyTrust") || !CU.CBool(tSettings.ContainsKey("DontVerifyTrust"))) //If DontVerifyTrust was previously set to true - do not allow reset!
                    tSettings["DontVerifyTrust"] = TheBaseAssets.MyServiceHostInfo.DontVerifyTrust.ToString();
                if (!tSettings.ContainsKey(nameof(TheServiceHostInfo.RequireCDEActivation)) || !CU.CBool(tSettings.ContainsKey(nameof(TheServiceHostInfo.RequireCDEActivation)))) //If DontVerifyTrust was previously set to true - do not allow reset!
                    tSettings[nameof(TheServiceHostInfo.RequireCDEActivation)] = TheBaseAssets.MyServiceHostInfo.RequireCDEActivation.ToString();
                tSettings["IsTLSEnforced"] = TheBaseAssets.MyServiceHostInfo.IsSSLEnforced.ToString();
                tSettings["AllowRemoteAdministration"] = TheBaseAssets.MyServiceHostInfo.AllowRemoteAdministration.ToString();
                tSettings["AllowRemoteThingCreation"] = TheBaseAssets.MyServiceHostInfo.AllowRemoteThingCreation.ToString();
                tSettings["BlockCloudNMI"] = TheBaseAssets.MyServiceHostInfo.IsCloudNMIBlocked.ToString();
                tSettings["RequiresConfiguration"] = TheBaseAssets.MyServiceHostInfo.RequiresConfiguration.ToString();
                tSettings["STATIONPORT"] = TheBaseAssets.MyServiceHostInfo.MyStationPort.ToString();
                tSettings["STATIONWSPORT"] = TheBaseAssets.MyServiceHostInfo.MyStationWSPort.ToString();
                tSettings["MyStationName"] = TheBaseAssets.MyServiceHostInfo.MyStationName;
                tSettings["MyStationURL"] = TheBaseAssets.MyServiceHostInfo.MyStationURL;
                tSettings["ScrambledScope"] = TheBaseAssets.MyScopeManager.GetScrambledScopeID();
                tSettings["ProxyToken"] = TheBaseAssets.MyServiceHostInfo.GetProxyToken();
                tSettings["TrustedNodesToken"] = GetTrustedNodesToken();
                tSettings["ClientCertificateThumb"] = TheBaseAssets.MyServiceHostInfo.ClientCertificateThumb;
                tSettings["MyDeviceInfo"] = CU.SerializeObjectToJSONString(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo);
                if (tSettings.ContainsKey("FederationID") || !string.IsNullOrEmpty(TheBaseAssets.MyScopeManager?.FederationID))
                    tSettings["FederationID"] = TheBaseAssets.MyScopeManager?.FederationID;

                //Step 3: copy all locally stored settings to the Private Settings cache
                tSettings.ToList().ForEach(x => MyPrivateSettings[x.Key] = MyPrivateSettings.ContainsKey(x.Key) ?
                        new aCDESetting
                        {
                            Value = x.Value,
                            IsHidden = MyPrivateSettings[x.Key].IsHidden,
                            ValueType = MyPrivateSettings[x.Key].ValueType,
                            cdeO = MyPrivateSettings[x.Key].cdeO
                        } :
                        new aCDESetting
                        {
                            Value = x.Value,
                            Name = x.Key,
                            IsHidden = HiddenSettings.Contains(x.Key),
                        });

                //Step 4: add all incoming settings to either the private or public settings store
                if (pSettings?.Count > 0)
                {
                    foreach (var t in pSettings)
                    {
                        var tObfKey = t.Name;
                        if (t.cdeO != null)
                            tObfKey = TheBaseAssets.MySecrets.CreatePasswordHash(t.cdeO.ToString());
                        tSettings[tObfKey] = t.Value;
                        if (t.IsHidden || t.cdeO != null)
                        {
                            if (!HiddenSettings.Contains(tObfKey))
                                HiddenSettings.Add(tObfKey);
                            MyPrivateSettings[tObfKey] = MyPrivateSettings.ContainsKey(tObfKey) ?
                                new aCDESetting
                                {
                                    Value = t.Value,
                                    Name = tObfKey,
                                    IsHidden = t.IsHidden,
                                    ValueType = MyPrivateSettings[tObfKey].ValueType,
                                    cdeO = t.cdeO
                                } :
                                new aCDESetting
                                {
                                    Value = t.Value,
                                    Name = tObfKey,
                                    IsHidden = t.IsHidden,
                                    ValueType = t.ValueType,
                                    cdeO = t.cdeO
                                };
                            if (MyPrivateSettings.ContainsKey(t.Name))
                                MyPrivateSettings.Remove(t.Name);
                            if (TheBaseAssets.MyCmdArgs.ContainsKey(t.Name))
                                TheBaseAssets.MyCmdArgs.Remove(t.Name);
                            if (tSettings.ContainsKey(t.Name))
                                tSettings.Remove(t.Name);
                        }
                        else
                            TheBaseAssets.MyCmdArgs[t.Name] = t.Value;
                    }
                }

                //step 5: Remove private keys from MyCmdArgs to disallow access via public TheBaseAssets.MyCmdArgs
                foreach (var k in MyPrivateSettings.Keys)
                {
                    if (MyPrivateSettings[k].cdeO != null)
                    {
                        if (TheBaseAssets.MyCmdArgs.ContainsKey(k))
                            TheBaseAssets.MyCmdArgs.Remove(k);
                    }
                    else
                        TheBaseAssets.MyCmdArgs[k] = MyPrivateSettings[k].Value;
                    tSettings[k] = MyPrivateSettings[k].Value; //making sure all private settings are stored in cdeTPI
                }

                //step 6: remove all incoming temporary settings  //[{"Name":"Administrator","UID":"Admin","Role":"NMIADMIN","HS":"","EMail":"z@z.zz","PWD":"zzzzzzzz","ACL":"255"}]
                bool NoDelete = CU.CBool(GetArgOrEnv(TheBaseAssets.MyCmdArgs, "DontDeleteEasyScope"));    //Not a great setting name but has history
                foreach (var s in RemovedSettings)
                {
                    if (tSettings.ContainsKey(s))
                        tSettings.Remove(s);
                    if (TheBaseAssets.MyCmdArgs.ContainsKey(s))
                        TheBaseAssets.MyCmdArgs.Remove(s);
                    if (!NoDelete)
                        TheBaseAssets.MySettings.DeleteAppSetting(s);
                }
                //sample "Coufs" [{{\"Name\":\"Administrator\",\"UID\":\"Admin\",\"Role\":\"NMIADMIN\",\"HS\":\"\",\"EMail\":\"z@z.zz\",\"PWD\":\"zzzzzzzz\",\"ACL\":\"255\"}}]
                //step 6: Encrypt and store in cache folder
                if (!TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID)
                {
                    byte[] pBuffer = TheBaseAssets.MyCrypto.EncryptKV(tSettings);
                    var tpiFile = CU.cdeFixupFileName("cache\\TheProvInfo.cdeTPI", true);
                    CU.CreateDirectories(tpiFile);
                    File.WriteAllBytes(tpiFile, pBuffer);
                    TheBaseAssets.MyApplication?.MyISMRoot?.SendSettingsToProse(pBuffer);
                }
                TheBaseAssets.MySettings.FireEvent("SettingsChanged", null, true);
                return true;
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2821, new TSM("TheCDESettings", $"Provisioning Infor Update failed", eMsgLevel.l1_Error, e.ToString()));
            }
            return false;
        }

        /// <summary>
        /// Reads all App Settings in the given argList
        /// </summary>
        /// <param name="argList">reference to the existing arg list. Must be set</param>
        /// <returns></returns>
        public virtual bool ReadAllAppSettings(ref IDictionary<string, string> argList)
        {
            if (string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.BaseDirectory))
            {
                TheBaseAssets.MyServiceHostInfo.BaseDirectory = CU.GetCurrentAppDomainBaseDirWithTrailingSlash();
            }
            if (!TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID)
            {
                var tpiFile = CU.cdeFixupFileName("cache\\TheProvInfo.cdeTPI", true);
                if (tpiFile == null)
                    return false;
                if (File.Exists(tpiFile))    //Does not exist with RandomDeviceID=true
                {
                    byte[] tBuf = File.ReadAllBytes(tpiFile);
                    Dictionary<string, string> tSettings = TheBaseAssets.MyCrypto.DecryptKV(tBuf); //This just ensures the settings can be read otherwise terminate app here
                    if (tBuf.Length > 0 && !(tSettings?.Count > 0))
                    {
                        TheSystemMessageLog.ToCo("Local cdeTPI file has no entries. Most likely crypto lib is not matching, node will terminate"); //Syslog is not initiated at this point
                        TheBaseAssets.IsStarting = false;
                        TheBaseAssets.MyApplication?.Shutdown(true);
                        TheBaseAssets.MasterSwitch = false;
                        return false;
                    }
                }
            }

            if (CU.IsMeadowFeather())
                return false;
#if !CDE_STANDARD
            var appSettings = ConfigurationManager.AppSettings;
#else
            dynamic appSettings = GetAppSettingsObject();
            if (appSettings == null)
            {
                return false;
            }
#endif
            string[] Keys = appSettings.AllKeys;
            for (int i = 0; i < appSettings.Count; i++)
            {
                if (!argList.ContainsKey(Keys[i]))
                    argList.Add(Keys[i], appSettings[i]);
            }
            return true;
        }

        /**********************************************
         *
         * Internal and private Only methods
         *
         **********************************************/

        //Settings that are consider security relavant and should not be logged or visible in the NMI
        private static readonly List<string> HiddenSettings = new () { "PrometheusUrl", "StatusToken", "ProxyUID", "ProxyPWD", "TrustedNodes", "NodeToken", "ProvToken", "EasyScope", "ScrambledScope", "ActivationKeys", "SQLUserName", "SQLPassword", "ProvScope", "ProxyToken", "TrustedNodesToken" };
        //Settings that must not be stored and therefore will be removed from cdeTPI and App.Config (if write permission is given to app.config)
        private static readonly List<string> RemovedSettings = new () { "EasyScope", "Coufs", "ProxyUrl", "ProxyPWD", "ProxyUID", "TrustedNodes", "RemoveTrustedNodes" };

        //new store for settings hidden from direct access by plugin (in comparison to the public MyCmdArgs dictionary
        private static readonly Dictionary<string, aCDESetting> MyPrivateSettings = new ();

        internal static string GetArgOrEnv(IDictionary<string, string> CmdArgs, string name, Guid? pOwner = null)
        {
            string temp;

            if (CmdArgs != null)
            {
                if (CmdArgs.TryGetValue(name, out temp) && !string.IsNullOrEmpty(temp) && TheBaseAssets.MyServiceHostInfo?.AllowEnvironmentVarsToOverrideConfig != true)
                {
                    return temp;
                }
            }
            else
            {
                temp = null;
            }

            if (TheBaseAssets.MyServiceHostInfo?.AllowEnvironmentVars == true)
            {
                try
                {
                    var temp2 = Environment.GetEnvironmentVariable($"{TheBaseAssets.MyServiceHostInfo.EnvVarPrefix}{name.ToUpperInvariant()}");
                    if (!string.IsNullOrEmpty(temp2))
                    {
                        temp = temp2;
                        if (CmdArgs != null)
                            CmdArgs[name] = temp2;
                    }
                }
                catch { 
                    //intent
                }
            }
            if (MyPrivateSettings?.Count > 0)
            {
                if (temp == null && MyPrivateSettings?.ContainsKey(name) == true)
                {
                    temp = MyPrivateSettings[name].Value;
                }
                if (temp == null && pOwner != null)
                {
                    var tObfKey = TheBaseAssets.MySecrets.CreatePasswordHash(pOwner.ToString());
                    if (MyPrivateSettings?.ContainsKey(tObfKey) == true)
                        temp = MyPrivateSettings[tObfKey].Value;
                }
            }
            return temp;
        }

#if CDE_STANDARD
        static dynamic GetAppSettingsObject()
        {
            dynamic appSettings = null;
            try
            {
                Assembly configManagerAssembly = null;
                try
                {
                    configManagerAssembly = Assembly.Load("System.Configuration.ConfigurationManager, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }
                catch { 
                    //intent
                }
                if (configManagerAssembly != null)
                {
                    var configManagerType = configManagerAssembly.GetType("System.Configuration.ConfigurationManager", false);
                    if (configManagerType != null)
                    {
                        try
                        {
                            appSettings = configManagerType.GetProperty("AppSettings")?.GetValue(null);
                        }
                        catch { 
                            //intent
                        }
                    }
                }
                if (CU.IsMeadowFeather())
                    return null;
                if (appSettings == null && Assembly.GetEntryAssembly()?.Location != null) //android or ios dont have this
                {
                    var appSettingsCollection = new System.Collections.Specialized.NameValueCollection();
                    // Read settings ourselves
                    var appConfig = new System.Xml.XmlDocument();
                    var exePath = Assembly.GetEntryAssembly().Location;
                    appConfig.Load($"{exePath}.config");
                    var appSettingsNodes = appConfig.SelectNodes("/configuration/appSettings/*");
                    foreach (System.Xml.XmlElement appSettingNode in appSettingsNodes)
                    {
                        if (appSettingNode.Name == "add")
                        {
                            var key = appSettingNode.GetAttribute("key");
                            var value = appSettingNode.GetAttribute("value");
                            if (!string.IsNullOrEmpty(key))
                            {
                                try
                                {
                                    appSettingsCollection.Add(key, value);
                                }
                                catch
                                {
                                    // ignore
                                }
                            }
                        }
                    }
                    appSettings = appSettingsCollection;
                }
            }
            catch { 
                //intent
            }
            return appSettings;
        }
#endif
        internal static void SetDeviceInfo()
        {
            TheBaseAssets.MyServiceHostInfo.IsNewDevice = !TheBaseAssets.MyServiceHostInfo.IsIsolated;
            TheBaseAssets.MyServiceHostInfo.MyDeviceInfo = new TheDeviceRegistryData();
            if (TheBaseAssets.MyServiceHostInfo.cdeNodeType != cdeNodeType.Relay)
                TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType = TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.Mini ? cdeSenderType.CDE_MINI : cdeSenderType.CDE_DEVICE;
            else
                TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType = TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.Mini ? cdeSenderType.CDE_MINI : (TheBaseAssets.MyServiceHostInfo.IsCloudService ? cdeSenderType.CDE_CLOUDROUTE : cdeSenderType.CDE_SERVICE);
            TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID = TheBaseAssets.MyServiceHostInfo.PresetDeviceID != Guid.Empty ? TheBaseAssets.MyServiceHostInfo.PresetDeviceID : TheBaseAssets.MyScopeManager.GenerateNewAppDeviceID(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType);
            Guid pinnedDeviceId;
            if (TheBaseAssets.MyActivationManager.CheckForPinnedLicense() && (pinnedDeviceId = TheBaseAssets.MySecrets.GetPinnedDeviceId(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID, Discovery.TheNetworkInfo.GetMACAddress(false))) != Guid.Empty)
            {
                TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID = pinnedDeviceId;
            }
            TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceType = TheBaseAssets.MyServiceHostInfo.cdeHostingType.ToString() + ":" + TheBaseAssets.CurrentVersionInfo;
            TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceName = TheBaseAssets.MyServiceHostInfo.cdeHostingType.ToString(); 

            TheBaseAssets.MySecrets.SetNodeKey(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID);
        }

        private static void SetupStationUrl(IDictionary<string, string> ArgList)
        {
            if (TheBaseAssets.MyServiceHostInfo.IsIsolated)
            {
                AddTrustedNodes(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo?.DeviceID.ToString(), false);
                TheBaseAssets.MyServiceHostInfo.MyStationURL = $"CDEI://{{{ArgList["ISOEN"]}}}";
                return;
            }
            Uri tStationURI = null;
            if (!string.IsNullOrEmpty(TheBaseAssets.MySettings.GetAppSetting("MyStationURL", "", false, false))) // ArgList != null && ArgList.ContainsKey("MyStationURL"))
                tStationURI = CU.CUri(TheBaseAssets.MySettings.GetAppSetting("MyStationURL", "", false, false), true);    //MSU-OK
            bool StatioUrlWasSetBefore = false;
            if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.MyStationURL) && !TheBaseAssets.MyServiceHostInfo.MyStationURL.Equals("NOT SET YET"))   //MSU-OK
            {
                tStationURI = CU.CUri(TheBaseAssets.MyServiceHostInfo.MyStationURL, true);  //MSU-OK
                StatioUrlWasSetBefore = true;
            }

            if (tStationURI == null)  //MSY-OK
            {
                tStationURI = CU.CUri(TheBaseAssets.MyServiceHostInfo.MyStationMoniker + TheNetworkInfo.cdeGetHostName(), false);
            }
            if (string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.RootDir) && tStationURI.AbsolutePath.Length > 1)
                TheBaseAssets.MyServiceHostInfo.RootDir = tStationURI.AbsolutePath;

            if (TheBaseAssets.MyServiceHostInfo.MyStationPort == 0)
            {
                if (StatioUrlWasSetBefore)
                    TheBaseAssets.MyServiceHostInfo.MyStationPort = (ushort)tStationURI.Port;
                else
                {
                    var tSP = TheBaseAssets.MySettings.GetSetting("STATIONPORT");
                    if (!string.IsNullOrEmpty(tSP))
                        TheBaseAssets.MyServiceHostInfo.MyStationPort = CU.CUShort(tSP);  //Overrides the Station Port with a value coming from the app.config
                }
                var tWSP = TheBaseAssets.MySettings.GetSetting("STATIONWSPORT");
                if (!string.IsNullOrEmpty(tWSP))
                    TheBaseAssets.MyServiceHostInfo.MyStationWSPort = CU.CUShort(tWSP);  //Overrides the Station Port with a value coming from the app.config
            }
            if (TheBaseAssets.MyServiceHostInfo.MyStationPort == 0)
                TheBaseAssets.MyServiceHostInfo.MyStationPort = CU.CUShort(tStationURI.Port); //If we want to not start the web server in case MyStationPort==0 then we need to not set the MyStationURL and switch to TheBaseAssets.MyServiceHostInfo.cdeNodeType = cdeNodeType.Active which disables the Web Server

            if (TheBaseAssets.MyServiceHostInfo.MyStationPort != 80 && tStationURI.Port != TheBaseAssets.MyServiceHostInfo.MyStationPort)
            {
                var builder = new UriBuilder(tStationURI)
                { Port = TheBaseAssets.MyServiceHostInfo.MyStationPort };
                tStationURI = builder.Uri;
            }
            if (TheBaseAssets.MyServiceHostInfo.IsSSLEnforced)
            {
                var builder = new UriBuilder(tStationURI) { Scheme = "https" };
                tStationURI = builder.Uri;
            }

            TheBaseAssets.MyServiceHostInfo.MyStationURL = tStationURI.ToString();  //MSU-OK
            if (tStationURI.AbsolutePath.Length == 1)
                TheBaseAssets.MyServiceHostInfo.MyStationURL = TheBaseAssets.MyServiceHostInfo.MyStationURL.Substring(0, TheBaseAssets.MyServiceHostInfo.MyStationURL.Length - 1); //MSU-OK
        }

        internal static bool ParseSettings(IDictionary<string, string> CmdArgs, bool IsCalledAtStartup, bool SafeLocalSettings)
        {
            var cmdArgsClone = new Dictionary<string, string>(CmdArgs);

            //This secion has to be before loading settings from cdeTPI as this information is needed to fetch the cdeTPI from a provisioning Service
            string temp = GetArgOrEnv(CmdArgs, "ClientCertificateThumb");
            string HSIClientCertScope = TheCertificates.SetClientCertificate(temp);

            if (IsCalledAtStartup) //Called only once during startup of CDE - subsequent ParseSettings calls will not go in here
            {
                //step 1: Migrate Old AppParameter to cdeTPI if exists
                MigrateAppParameterToTPI(HSIClientCertScope);

                //step 2: read parameter that the CDE only allows to be set during startup
                temp = GetArgOrEnv(CmdArgs, "DeviceToken");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.NodeToken = CU.GenerateFinalStr(temp);
                temp = GetArgOrEnv(CmdArgs, "CustomerToken");
                if (!string.IsNullOrEmpty(temp) && !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.NodeToken))
                    TheBaseAssets.MyServiceHostInfo.NodeToken += $";{CU.GenerateFinalStr(temp)}";
                TheBaseAssets.MyServiceHostInfo.RequiresConfiguration = !TheBaseAssets.MyServiceHostInfo.IsCloudService;
                temp = GetArgOrEnv(CmdArgs, "UseUserMapper");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper = CU.CBool(temp);
                temp = GetArgOrEnv(CmdArgs, "CloudToCloudUpstreamOnly");    //new in 5.108: if set, only upstream cloud-to-cloud traffic allowd (diode mode)
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.CloudToCloudUpstreamOnly = CU.CBool(temp);
                temp = GetArgOrEnv(CmdArgs, "AllowedUnscopedNodes");    //New in 5.108 - NodeIDs allowed to connect even if unscoped
                if (!string.IsNullOrEmpty(temp))
                {
                    var tl = temp.Split(';');
                    foreach (string t in tl)
                    {
                        Guid tBL = CU.CGuid(t);
                        if (tBL != Guid.Empty)
                            TheBaseAssets.MyServiceHostInfo.AllowedUnscopedNodes.Add(tBL);
                    }
                }
                //Proxy has to be read before provisioning service kicks in to use the proxy
                //We need to remove these settings to make it more secure
                temp = GetArgOrEnv(CmdArgs, "ProxyUrl");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.ProxyUrl = temp;
                temp = GetArgOrEnv(CmdArgs, "ProxyUID");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.ProxyUID = temp;
                temp = GetArgOrEnv(CmdArgs, "ProxyPWD");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.ProxyPWD = temp;
                //Disable Trust can only be set once to true - then stored in the
                temp = GetArgOrEnv(CmdArgs, "DontVerifyTrust");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.DontVerifyTrust = CU.CBool(temp);
                //RequireCDEActivation can only be set once to true - then stored in the
                temp = GetArgOrEnv(CmdArgs, nameof(TheServiceHostInfo.RequireCDEActivation));
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.RequireCDEActivation = CU.CBool(temp);
                temp = GetArgOrEnv(CmdArgs, "PresetDeviceID");
                if (!string.IsNullOrEmpty(temp) && Guid.Empty != CU.CGuid(temp))
                    TheBaseAssets.MyServiceHostInfo.PresetDeviceID = CU.CGuid(temp);
                //step 3: Load the local settings if they exist - set by MSI or previous starts of the node
                if (File.Exists(CU.cdeFixupFileName("cache\\TheProvInfo.cdeTPI", true)))
                {
                    byte[] tBuf = File.ReadAllBytes(CU.cdeFixupFileName("cache\\TheProvInfo.cdeTPI", true));
                    ParseProvisioning(tBuf, true, false);
                }
                if (TheBaseAssets.MyServiceHostInfo.MyDeviceInfo == null) //It no cdeTPI
                    SetDeviceInfo();

                var ProSeUrl = GetArgOrEnv(CmdArgs, "ProvisioningService");
                if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.NodeToken) && !string.IsNullOrEmpty(ProSeUrl) && !CU.CBool(TheBaseAssets.MySettings.GetSetting("HasContactedProSe", new Guid("{03EE660D-1C90-4D17-891D-47932E06E5B4}"))))
                {
                    TheBaseAssets.IsInAgentStartup = true;
                    bool Success = false;
                    while (!Success)
                    {
                        string error = "";
                        TheBaseAssets.MySYSLOG.WriteToLog(2821, new TSM("TheCDESettings", $"Contacting Provisioning Service ({ProSeUrl}) ...", eMsgLevel.l3_ImportantMessage));
                        var task = GetProvisioningInfo(ProSeUrl, TheBaseAssets.MyServiceHostInfo.NodeToken);
                        if (task.Wait(16000) && task.Status == TaskStatus.RanToCompletion)
                        {
                            if (string.IsNullOrEmpty(task.Result))
                            {
                                Success = true;
                            }
                            else
                            {
                                error = task.Result;
                                Success = false;
                            }
                        }
                        else
                        {
                            Success = false;
                            error = "Timeout";
                        }
                        //Decide what to do by default - my suggestion: (0 being the default
                        //Option 2: just fail through...leaves the Node in a none-configured state as the node will start normally
                        //Option 1: Wait here for a certain amount of time then repeat
                        //Option 0: Shutdown the relay here before it writes anything to cache. Than an external "watchdog" can decide what to do next (restart or nothing)
                        if (!Success)
                        {
                            switch (CU.CInt(GetArgOrEnv(CmdArgs, "ProvisioningFailureOption")))
                            {
                                case 2:
                                    TheBaseAssets.IsInAgentStartup = false;
                                    return true;
                                case 1:
                                    TheBaseAssets.MySYSLOG.WriteToLog(2821, new TSM("TheCDESettings", $"...failed. Trying again in 15sec", eMsgLevel.l2_Warning, error));
                                    CU.SleepOneEye(15000, 1000);
                                    if (!TheBaseAssets.MasterSwitch)
                                    {
                                        TheBaseAssets.MyApplication.Shutdown(false);
                                        return false;
                                    }
                                    break;
                                default:
                                    TheBaseAssets.MySYSLOG.WriteToLog(2821, new TSM("TheCDESettings", $"...failed. Shutting down", eMsgLevel.l1_Error, error));
                                    TheBaseAssets.IsInAgentStartup = false;
                                    TheBaseAssets.MyApplication.Shutdown(false);
                                    return false;
                            }
                        }
                        if (Success)
                        {
                            TheBaseAssets.MySettings.SetSetting("HasContactedProSe", "true", true, new Guid("{03EE660D-1C90-4D17-891D-47932E06E5B4}"));
                            if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProvisioningService) && !ProSeUrl.Equals(TheBaseAssets.MyServiceHostInfo.ProvisioningService, StringComparison.OrdinalIgnoreCase))
                            {
                                //New in 4.203.1: allows to redirect to a different ProvService for load-balancing and geo-distribution
                                ProSeUrl = TheBaseAssets.MyServiceHostInfo.ProvisioningService;
                                Success = false;
                            }
                            else
                                TheBaseAssets.MyServiceHostInfo.ProvisioningService = ProSeUrl;
                        }
                    }
                    TheBaseAssets.IsInAgentStartup = false;
                    return Success;
                }
            }
            TheBaseAssets.MyServiceHostInfo.SetProxy(GetArgOrEnv(CmdArgs, "ProxyToken"));
            if (TheBaseAssets.MyScopeManager.IsScopingEnabled)
                TheBaseAssets.MyServiceHostInfo.RequiresConfiguration = false;

            temp = GetArgOrEnv(CmdArgs, "IsTLSEnforced");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.IsSSLEnforced = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "RootDir");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.RootDir = CU.CStr(temp);
            SetupStationUrl(CmdArgs);

            if (CmdArgs.Count == 0) return true;

            if (TheBaseAssets.MyServiceHostInfo.RequiresConfiguration || TheBaseAssets.MyServiceHostInfo.IsCloudService)
            {
                if (HSIClientCertScope == null) //Dont allow legacy Scope ID settings if the specified Client Cert has a scope
                {
                    temp = GetArgOrEnv(CmdArgs, "EasyScope");
                    if (!string.IsNullOrEmpty(temp))
                    {
                        TheBaseAssets.MyScopeManager.SetScopeIDFromEasyID(temp);
                    }
                    temp = GetArgOrEnv(CmdArgs, "ScrambledScope");
                    if (!string.IsNullOrEmpty(temp))
                        TheBaseAssets.MyScopeManager.SetScopeIDFromScrambledID(temp);
                }
                temp = GetArgOrEnv(CmdArgs, "FederationID");
                if (!string.IsNullOrEmpty(temp) && TheBaseAssets.MySecrets.IsInApplicationScope(temp))
                    TheBaseAssets.MyScopeManager.FederationID = temp;
            }
            temp = GetArgOrEnv(CmdArgs, "SKUID");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.SKUID = CU.CUInt(temp);


            temp = GetArgOrEnv(CmdArgs, "ShutdownOnLicenseFailure");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.ShutdownOnLicenseFailure = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "ActivationKeys");
            if (!string.IsNullOrEmpty(temp))
            {
                TheBaseAssets.MyServiceHostInfo.ActivationKeysToAdd ??= new List<string>();
                TheBaseAssets.MyServiceHostInfo.ActivationKeysToAdd.AddRange(temp.Split(';'));
            }
            temp = GetArgOrEnv(CmdArgs, "AllowRemoteAdministration");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AllowRemoteAdministration = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "AllowRemoteThingCreation");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AllowRemoteThingCreation = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "AllowAutoUpdate");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AllowAutoUpdate = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "DontFallbackToDevice");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DontFallbackToDevice = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "DisableTls12");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DisableTls12 = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, nameof(TheServiceHostInfo.IgnoreServerCertificateErrors));
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.IgnoreServerCertificateErrors = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, nameof(TheServiceHostInfo.IgnoreServerCertificateNotAvailable));
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.IgnoreServerCertificateNotAvailable = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, nameof(TheServiceHostInfo.IgnoreServerCertificateNameMismatch));
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.IgnoreServerCertificateNameMismatch = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, nameof(TheServiceHostInfo.IgnoreServerCertificateChainErrors));
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.IgnoreServerCertificateChainErrors = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "ClientCertificateUsage");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.ClientCertificateUsage = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, nameof(TheBaseAssets.MyServiceHostInfo.OneWayRelayMode));
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.OneWayRelayMode = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, nameof(TheBaseAssets.MyServiceHostInfo.OneWayTSMFilter));
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.OneWayTSMFilter = CU.CStringToList(temp, ';').ToArray();


            temp = GetArgOrEnv(CmdArgs, "Access-Control-Allow-Origin");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AccessControlAllowOrigin = temp;

            temp = GetArgOrEnv(CmdArgs, "Access-Control-Allow-Methods");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AccessControlAllowMethods = temp;

            temp = GetArgOrEnv(CmdArgs, "Access-Control-Allow-Headers");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AccessControlAllowHeaders = temp;

            temp = GetArgOrEnv(CmdArgs, "RequiredClientCertRootThumbprints");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.RequiredClientCertRootThumbprints = CU.CStringToList(temp.ToUpperInvariant(), ';');

            temp = GetArgOrEnv(CmdArgs, "EnableFastSecurity");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.EnableFastSecurity = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "DisableFastTSMs");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DisableFastTSMs = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "SenderQueueSize");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.SenderQueueSize = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "DisablePriorityInversion");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DisablePriorityInversion = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "DisablePLSCompression");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DisablePLSCompression = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "DisableConsole");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DisableConsole = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "UseGELFLoggingFormat");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.UseGELFLoggingFormat = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "AllowAnonymousAccess");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AllowAnonymousAccess = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "AllowSetScopeWithSetAdmin");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AllowSetScopeWithSetAdmin = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "EnableIntegration");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.EnableIntegration = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "AllowMessagesInConnect");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AllowMessagesInConnect = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "AllowUnscopedMesh");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AllowUnscopedMesh = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "EnableCosting");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.EnableCosting = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "FireGlobalTimerSync");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.FireGlobalTimerSync = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "EnableIsolation");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.EnableIsolation = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "MyStationIP");
            if (string.IsNullOrEmpty(temp))
            {
                System.Net.IPAddress pAddrr = Discovery.TheNetworkInfo.GetIPAddress(false);
                if (pAddrr != null)
                    TheBaseAssets.MyServiceHostInfo.MyStationIP = pAddrr.ToString();
            }
            else
            {
                System.Net.IPAddress pAddrr = Discovery.TheNetworkInfo.GetIPAddress(temp);
                if (pAddrr != null)
                    TheBaseAssets.MyServiceHostInfo.MyStationIP = pAddrr.ToString();
            }
            if (string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.MyStationIP))
                TheBaseAssets.MyServiceHostInfo.MyStationIP = "127.0.0.1";

            if (!TheBaseAssets.MyServiceHostInfo.IsIsolated)
            {
                temp = GetArgOrEnv(CmdArgs, "CloudServiceRoute");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.CloudServiceRoute = temp.ToUpper();//For compatibility to V3 must remain CloudServiceRoute here
                temp = GetArgOrEnv(CmdArgs, "ServiceRoute");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.ServiceRoute = temp.ToUpper();//This is the main route. if its set, it overrides all others
                temp = GetArgOrEnv(CmdArgs, "LocalServiceRoute");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.CloudServiceRoute = temp.ToUpper(); //For compatibility to V3 must remain LocalServiceRoute here

                temp = GetArgOrEnv(CmdArgs, "NodeName");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.NodeName = temp.ToUpper();

                temp = GetArgOrEnv(CmdArgs, "UPnPDeviceType");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.UPnPDeviceType = temp;

                temp = GetArgOrEnv(CmdArgs, "DISCO_Subnet");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.DISCOSubnet = temp;
                temp = GetArgOrEnv(CmdArgs, "DISCO_MX");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.DISCOMX = CU.CInt(temp);
                temp = GetArgOrEnv(CmdArgs, "DontRelayNMI");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.DontRelayNMI = CU.CBool(temp);
                temp = GetArgOrEnv(CmdArgs, "RejectIncomingSETP");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.RejectIncomingSETP = CU.CBool(temp);

                temp = GetArgOrEnv(CmdArgs, "IsCloudNMIBlocked");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.IsCloudNMIBlocked = CU.CBool(temp);

                temp = GetArgOrEnv(CmdArgs, "IsCloudDisabled");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.IsCloudDisabled = CU.CBool(temp);

                if (!CU.IsMeadowFeather())
                {
                    CU.cdeRunAsync("PingForInternet", true, (o) =>
                    {
                        TheNetworkInfo.IsConnectedToInternet();
                    });
                }
                temp = GetArgOrEnv(CmdArgs, "MyAltStationURLs");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.MyAltStationURLs.AddRange(temp.Split(';'));

                temp = GetArgOrEnv(CmdArgs, "NodeBlacklist");
                if (!string.IsNullOrEmpty(temp))
                {
                    var tl = temp.Split(';');
                    foreach (string t in tl)
                    {
                        Guid tBL = CU.CGuid(t);
                        if (tBL != Guid.Empty)
                            TheBaseAssets.MyServiceHostInfo.NodeBlacklist.Add(tBL);
                    }
                }

                temp = GetArgOrEnv(CmdArgs, "MyStationName");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.MyStationName = temp;

                temp = GetArgOrEnv(CmdArgs, "IsolatedPlugins");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.IsoEngines.AddRange(temp.Split(';'));

                temp = GetArgOrEnv(CmdArgs, "AllowAdhocScopes");
                if (!string.IsNullOrEmpty(temp))
                    TheBaseAssets.MyServiceHostInfo.AllowAdhocScopes = CU.CBool(temp);

                //new in 4.1032 - Coufs is now allowed in App.Config if the relay was just installed and no ClientBin/cache folder exists
                temp = GetArgOrEnv(CmdArgs, "Coufs");
                if (!string.IsNullOrEmpty(temp) && (TheBaseAssets.MyServiceHostInfo.IsNewDevice || IsCalledAtStartup) && string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.Coufs))
                    TheBaseAssets.MyServiceHostInfo.Coufs = temp;
            }

            temp = GetArgOrEnv(CmdArgs, "DisableNMI");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DisableNMI = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "DisableNMIMessages");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DisableNMIMessages = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "CustomUA");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.CustomUA = temp;

            temp = GetArgOrEnv(CmdArgs, "ResourcePath");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.ResourcePath = temp;

            temp = GetArgOrEnv(CmdArgs, "StartISM");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.StartISM = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "StartISMDisco");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.StartISMDisco = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "ISMScanOnStartup");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.ISMScanOnStartup = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "ISMScanForUpdatesOnUSB");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.ISMScanForUpdatesOnUSB = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "ISMUpdateVersion");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.ISMUpdateVersion = CU.CDbl(temp);

            temp = GetArgOrEnv(CmdArgs, nameof(TheServiceHostInfo.DontVerifyIntegrity));
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DontVerifyIntegrity = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "VerifyTrustPath");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.VerifyTrustPath = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "AllowDistributedResourceFetch");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AllowDistributedResourceFetch = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "ForceWebPlatform");
            if (!string.IsNullOrEmpty(temp)) TheBaseAssets.MyServiceHostInfo.ForceWebPlatform = (eWebPlatform)CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "UseHBTimerPerSender");
            if (!string.IsNullOrEmpty(temp)) TheBaseAssets.MyServiceHostInfo.UseHBTimerPerSender = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "DisableCache");
            if (!string.IsNullOrEmpty(temp)) TheBaseAssets.MyServiceHostInfo.DisableCache = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "CacheMaxAge");
            if (!string.IsNullOrEmpty(temp)) TheBaseAssets.MyServiceHostInfo.CacheMaxAge = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "TokenLifeTime");
            if (!string.IsNullOrEmpty(temp)) TheBaseAssets.MyServiceHostInfo.TokenLifeTime = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "MaximumHops");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.MaximumHops = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "WsTimeOut");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.TO.WsTimeOut = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "WsJsThrottle");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.WsJsThrottle = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "ParallelPosts");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.ParallelPosts = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "DefaultEventTimeout");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.EventTimeout = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "PreShutDownDelay");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.PreShutDownDelay = CU.CInt(temp);


            temp = GetArgOrEnv(CmdArgs, "ThingRegistryStoreInterval");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.ThingRegistryStoreInterval = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "NodeType");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.cdeNodeType = (cdeNodeType)CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "DEBUGLEVEL");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DebugLevel = (eDEBUG_LEVELS)CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "SIMROLE");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.SimulatedEngines.AddRange(temp.Split(';'));
            temp = GetArgOrEnv(CmdArgs, "SIGNORE");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.IgnoredEngines.AddRange(temp.Split(';'));

            temp = GetArgOrEnv(CmdArgs, "StartStation");
            if (!string.IsNullOrEmpty(temp)) TheBaseAssets.MyServiceHostInfo.StartupEngines.AddRange(temp.Split(';'));

            temp = GetArgOrEnv(CmdArgs, "DefaultHomePage");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DefHomePage = temp.ToUpper();
            else
            {
                if (string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.DefHomePage))
                    TheBaseAssets.MyServiceHostInfo.DefHomePage = "/NMIPortal";
            }

            temp = GetArgOrEnv(CmdArgs, "RelayOnly");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.RelayEngines.AddRange(temp.Split(';'));

            temp = GetArgOrEnv(CmdArgs, "PermittedUnscopedNodeIDs");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.PermittedUnscopedNodesIDs.AddRange(temp.Split(';'));

            temp = GetArgOrEnv(CmdArgs, "AllowLocalHost");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AllowLocalHost = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "DisableRSAToBrowser");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DisableRSAToBrowser = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "IsViewer");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.IsViewer = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "DisableWebSockets");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DisableWebSockets = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, nameof(TheServiceHostInfo.UseTcpListenerInsteadOfHttpListener));
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.UseTcpListenerInsteadOfHttpListener = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "IsOutputCompressed");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.IsOutputCompressed = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "IsUserManagerInStorage");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.IsUserManagerInStorage = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "IsConnectedToCloud");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.IsConnectedToCloud = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "FailOnAdminCheck");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.FailOnAdminCheck = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "CloudBlobURL");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.CloudBlobURL = temp.ToUpper();

            temp = GetArgOrEnv(CmdArgs, "DefaultLCID");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.DefaultLCID = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "AzureAnalytics");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AzureAnalytics = CU.CGuid(temp);

            temp = GetArgOrEnv(CmdArgs, "StoreLoggedMessages");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.StoreLoggedMessages = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "IsMemoryOptimized");
            TheBaseAssets.MyServiceHostInfo.IsMemoryOptimized = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "FallbackToSimulation");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.FallbackToSimulation = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "UseSysLogQueue");
            if (!string.IsNullOrEmpty(temp) && CU.CBool(temp))
            {
                TheBaseAssets.MySYSLOG.SwitchToQueue();
            }
            temp = GetArgOrEnv(CmdArgs, "ShowMarkupInLog");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "AuditNMIChanges");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AuditNMIChanges = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "MaxLogEntries");
            if (!string.IsNullOrEmpty(temp))
            {
                TheBaseAssets.MySYSLOG.SetMaxLogEntries(CU.CInt(temp));
            }
            temp = GetArgOrEnv(CmdArgs, "LogFilter");
            if (!string.IsNullOrEmpty(temp))
            {
                TheBaseAssets.MySYSLOG.LogFilter.Clear();
                TheBaseAssets.MySYSLOG.LogFilter.AddRange(temp.Split(';'));
            }
            temp = GetArgOrEnv(CmdArgs, "LogOnly");
            if (!string.IsNullOrEmpty(temp))
            {
                TheBaseAssets.MySYSLOG.LogOnly.Clear();
                TheBaseAssets.MySYSLOG.LogOnly.AddRange(temp.Split(';'));
            }

            temp = GetArgOrEnv(CmdArgs, "LogIgnore");
            if (!string.IsNullOrEmpty(temp))
            {
                TheBaseAssets.MySYSLOG.LogIgnore.Clear();
                TheBaseAssets.MySYSLOG.LogIgnore.AddRange(temp.Split(';'));
            }
            temp = GetArgOrEnv(CmdArgs, "LogFilePath");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MySYSLOG.LogFilePath = temp;

            temp = GetArgOrEnv(CmdArgs, "MaxLogFiles");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MySYSLOG.MaxLogFiles = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "MaxLogFileSize");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MySYSLOG.MaxLogFileSize = CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "LogFilterLevel");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MySYSLOG.LogFilterLevel = (eMsgLevel)CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "LogWriteFilterLevel");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MySYSLOG.LogWriteFilterLevel = (eMsgLevel)CU.CInt(temp);
            temp = GetArgOrEnv(CmdArgs, "LogWriteBuffer");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MySYSLOG.LogWriteBuffer = CU.CInt(temp);
            temp = GetArgOrEnv(CmdArgs, "MessageFilterLevel");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MySYSLOG.MessageFilterLevel = (eMsgLevel)CU.CInt(temp);

            temp = GetArgOrEnv(CmdArgs, "AllowForeignScopeIDRouting");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AllowForeignScopeIDRouting = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "AsyncEngineStartup");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.AsyncEngineStartup = CU.CBool(temp);
            temp = GetArgOrEnv(CmdArgs, "EnableHistorian");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.EnableHistorianDataLog = CU.CBool(temp);

            temp = GetArgOrEnv(CmdArgs, "HeadlessStartupDelay");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.TO.HeadlessStartupDelay = CU.CInt(temp);

            #region Client Application
            temp = GetArgOrEnv(CmdArgs, "EnableAutoLogin");
            if (!string.IsNullOrEmpty(temp))
                TheBaseAssets.MyServiceHostInfo.EnableAutoLogin = CU.CBool(temp);
            #endregion

            if (TheBaseAssets.MyServiceHostInfo.DebugLevel > eDEBUG_LEVELS.OFF)
            {
                TSM tMsg = new ("BaseAssets", "CommandLine Parsed", eMsgLevel.l3_ImportantMessage);
                var logArgs = new Dictionary<string, string>();
                if (CmdArgs != null)
                {
                    foreach (var kv in CmdArgs)
                    {
                        if (!cmdArgsClone.TryGetValue(kv.Key, out var cmdArgBefore))
                        {
                            cmdArgBefore = null;
                        }
                        if (HiddenSettings.Contains(kv.Key) || (MyPrivateSettings.ContainsKey(kv.Key) && MyPrivateSettings[kv.Key].IsHidden))
                            logArgs.Add(kv.Key, "SetButHidden");
                        else
                            logArgs.Add(kv.Key, kv.Value + (cmdArgBefore != null && kv.Value != cmdArgBefore ? $"(Cmd:{cmdArgBefore})" : ""));
                    }
                }
                tMsg.PLS = CU.SerializeObjectToJSONString(logArgs);
                TheBaseAssets.MySYSLOG.WriteToLog(1, tMsg, true);
            }

            temp = GetArgOrEnv(CmdArgs, "TrustedNodes");
            if (!string.IsNullOrEmpty(temp))
            {
                AddTrustedNodes(temp, false);
                TheBaseAssets.MySettings.DeleteAppSetting("TrustedNodes");
            }
            temp = GetArgOrEnv(CmdArgs, "RemoveAllTrustedNodes");
            if (!string.IsNullOrEmpty(temp))
            {
                RemoveAllTrustedNodes(false);
                TheBaseAssets.MySettings.DeleteAppSetting("RemoveAllTrustedNodes");
            }
            if (!TheBaseAssets.MyScopeManager.IsScopingEnabled && TheBaseAssets.MyServiceHostInfo.IsCloudService)
                TheBaseAssets.MyServiceHostInfo.RequiresConfiguration = false;

            if (SafeLocalSettings)
                TheBaseAssets.MySettings.UpdateLocalSettings();
            return true;
        }

        ///New in V4.2 = Remote Provisioning
        ///

        private static
#if !CDE_NET4
            async
#endif
        Task<string> GetProvisioningInfo(string pProvService, string pDeviceToken)
        {
            if (string.IsNullOrEmpty(pDeviceToken))
            {
                return CU.TaskOrResult("");
            }
            var tDevInfo = new TheUPnPDeviceInfo()
            {
                UUID = pDeviceToken,
                IsCDEngine = true,
                ModelName = TheBaseAssets.MyServiceHostInfo.ApplicationName,
                ModelNumber = TheBaseAssets.CurrentVersion.ToString(),
                PresentationUrl = !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.NodeName) ?
                TheBaseAssets.MyServiceHostInfo.NodeName : !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.MyStationName) ?
                TheBaseAssets.MyServiceHostInfo.MyStationName : TheBaseAssets.MyServiceHostInfo.MyStationURL
            };

            TheRequestData tRequest = new ()
            {
                RequestUri = CU.CUri($"{pProvService}/register-node", true),
                PostData = TheBaseAssets.MyCrypto.Encrypt(CU.CUnicodeString2Array(CU.SerializeObjectToJSONString(tDevInfo)), TheBaseAssets.MySecrets.GetAK(), TheBaseAssets.MySecrets.GetAI()),
                ResponseMimeType = "application/json",
                TimeOut = 15000
            };
            TheBaseAssets.MySYSLOG.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDESettings", $"Contacting Provisioning Service ({pProvService})...", eMsgLevel.l3_ImportantMessage));
            var myRest = new Communication.TheREST();
            var postCS = new TaskCompletionSource<string>();
            myRest?.PostRESTAsync(tRequest, r =>
            {
                postCS.TrySetResult(null);
            }, r =>
            {
                postCS.TrySetResult(r.ErrorDescription);
            });
#if !CDE_NET4
            if (!string.IsNullOrEmpty(await postCS.Task.ConfigureAwait(false)))
#else
            if (!string.IsNullOrEmpty(postCS.Task.Result))
#endif
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDESettings", $"Provisioning Service ({pProvService}) did not respond", eMsgLevel.l1_Error, postCS.Task.Result));
                return CU.TaskOrResult(postCS.Task.Result);
            }
            string error = "Internal error";
            try
            {
                return CU.TaskOrResult(ParseProvisioning(tRequest?.ResponseBuffer, true, true));
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDESettings", $"Provisioning Service ({pProvService}) responded but data parsing had an error", eMsgLevel.l1_Error, e.ToString()));
                error = e.Message;
            }
            return CU.TaskOrResult(error);
        }

        /// <summary>
        /// Parses a new set of settings read from cache folder or coming in via Provisioning Service
        /// </summary>
        /// <param name="pBuffer">Encrypted binary buffer of the settings</param>
        /// <param name="IsInitial">Indicator if this is the first time the settings are loaded</param>
        /// <param name="SaveSettings">If true the settings will be saved to disk</param>
        /// <returns></returns>
        internal static string ParseProvisioning(byte[] pBuffer, bool IsInitial, bool SaveSettings)
        {
            if (pBuffer == null)
                return "Invalid buffer";
            try
            {
                //step 1: Decrypt buffer and check if valid
                var tSettings = TheBaseAssets.MyCrypto.DecryptKV(pBuffer);
                if (tSettings == null || tSettings.Count == 0 || (tSettings.ContainsKey("TryLater") && CU.CBool(tSettings["TryLater"])))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDESettings", $"Provisioning Service responded but had no data", eMsgLevel.l1_Error));
                    return "Provisioning Service responded but had no data";
                }

                //step 2: if initial Parse, check for Provisioning Token and Scope
                if (IsInitial)
                {
                    if (tSettings.ContainsKey("UseUserMapper"))
                        TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper = CU.CBool(tSettings["UseUserMapper"]);
                    if (tSettings.ContainsKey("DontVerifyTrust"))
                        TheBaseAssets.MyServiceHostInfo.DontVerifyTrust = CU.CBool(tSettings["DontVerifyTrust"]);
                    if (tSettings.ContainsKey(nameof(TheServiceHostInfo.RequireCDEActivation)))
                        TheBaseAssets.MyServiceHostInfo.RequireCDEActivation = CU.CBool(tSettings[nameof(TheServiceHostInfo.RequireCDEActivation)]);
                    if (tSettings.ContainsKey("ProvToken"))
                        TheBaseAssets.MyServiceHostInfo.ProvToken = tSettings["ProvToken"];
                    if (tSettings.ContainsKey("ProvScope"))
                    {
                        TheBaseAssets.MyServiceHostInfo.ProvisioningScope = tSettings["ProvScope"];
                        TheBaseAssets.MyScopeManager.SetProSeScope(TheBaseAssets.MyServiceHostInfo.ProvisioningScope);
                    }
                }
                if (tSettings.ContainsKey("CDE_PSNI"))
                {
                    TheBaseAssets.MyServiceHostInfo.ProvisioningServiceNodeID = CU.CGuid(tSettings["CDE_PSNI"]);
                    tSettings.Remove("CDE_PSNI");
                }

                //step 3: Set Device Info (Required for UserManager and Coufs)
                if (TheBaseAssets.MyServiceHostInfo.MyDeviceInfo == null && tSettings.ContainsKey("MyDeviceInfo"))
                {
                    try
                    {
                        TheBaseAssets.MyServiceHostInfo.MyDeviceInfo = CU.DeserializeJSONStringToObject<TheDeviceRegistryData>(tSettings["MyDeviceInfo"]);
                    }
                    catch (Exception)
                    {
                        //intent
                    }
                }
                if (TheBaseAssets.MyServiceHostInfo.MyDeviceInfo == null)
                    SetDeviceInfo();
                if (TheBaseAssets.MyServiceHostInfo.MyDeviceInfo != null)
                    TheBaseAssets.MySecrets.SetNodeKey(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID);

                //step 4: Set Scope
                if (tSettings.ContainsKey("EasyScope"))
                {
                    var tS = tSettings["EasyScope"];
                    if (!string.IsNullOrEmpty(tS))
                        TheBaseAssets.MyScopeManager.SetScopeIDFromEasyID(tSettings["EasyScope"]);
                }
                else if (tSettings.ContainsKey("ScrambledScope"))
                {
                    TheBaseAssets.MyScopeManager.SetScopeIDFromScrambledID(tSettings["ScrambledScope"]);
                }
                TheBaseAssets.MyServiceHostInfo.RequiresConfiguration = !TheBaseAssets.MyScopeManager.IsScopingEnabled;
                //step 5: process the Coufs coming from ProSe or MSI
                if (tSettings.ContainsKey("Coufs") && TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper)
                {
                    TheUserManager.CreateUserOnFirstStart(tSettings["Coufs"], true, tSettings.ContainsKey("FUDB") && CU.CBool(tSettings["FUDB"]));
                }

                //Step 6: copy all locally stored settings to the Private Settings cache
                tSettings.ToList().ForEach(x => MyPrivateSettings[x.Key] = MyPrivateSettings.ContainsKey(x.Key) ?
                        new aCDESetting
                        {
                            Value = x.Value,
                            IsHidden = MyPrivateSettings[x.Key].IsHidden,
                            ValueType = MyPrivateSettings[x.Key].ValueType,
                            cdeO = MyPrivateSettings[x.Key].cdeO
                        } :
                        new aCDESetting
                        {
                            Value = x.Value,
                            Name = x.Key,
                            IsHidden = HiddenSettings.Contains(x.Key),
                        });

                // step 7: copy settings in parseable dictionary and parse the persisted settings
                Dictionary<string, string> tSetArgs = new ();
                tSettings.ToList().ForEach(x => tSetArgs[x.Key] = x.Value); //was cmdargs but must be isolated from public settings
                ParseSettings(tSetArgs, false, SaveSettings);
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCDESettings", $"Provisioning Parsing failed", eMsgLevel.l1_Error, e.ToString()));
            }
            return null;
        }

        #region Trusted Nodes
        /// <summary>
        /// Returns true if trusted nodes are configured
        /// </summary>
        public bool HasTrustedNodes { get { return MyTrustedNodes?.Count > 0; } }

        /// <summary>
        /// Checks if a node (ID) is trustd
        /// </summary>
        /// <param name="pNodeID">Source NodeID</param>
        /// <returns></returns>
        public bool IsNodeTrusted(Guid pNodeID)
        {
            if (pNodeID == Guid.Empty || !TheBaseAssets.MyScopeManager.IsScopingEnabled) return false;

            if (MyTrustedNodes == null)
            {
                MyTrustedNodes = new List<string>();
                //4.202.2: added caching to avoid re-decrypting for each NMI telegram!
                string tNodes = CU.cdeDecrypt(TheBaseAssets.MySettings.GetSetting("TrustedNodesToken"), TheBaseAssets.MySecrets.GetNodeKey());
                MyTrustedNodes.AddRange(CU.cdeSplit(tNodes, ";", true, true));
            }
            return MyTrustedNodes?.Contains(pNodeID.ToString()) == true;
        }
        internal static string GetTrustedNodesToken()
        {
            if (MyTrustedNodes?.Count < 1)
                return null;
            return CU.cdeEncrypt(CU.CListToString(MyTrustedNodes, ";"), TheBaseAssets.MySecrets.GetNodeKey());
        }

        /// <summary>
        /// Adds nodes to the list of Trusted Nodes
        /// </summary>
        /// <param name="pNodeIDs"></param>
        /// <param name="UpdateSettings">Update the local settings file</param>
        /// <returns></returns>
        internal static bool AddTrustedNodes(string pNodeIDs, bool UpdateSettings)
        {
            string tNodes = "";
            if (MyTrustedNodes == null)
            {
                MyTrustedNodes = new List<string>();
                tNodes = CU.cdeDecrypt(TheBaseAssets.MySettings.GetSetting("TrustedNodesToken"), TheBaseAssets.MySecrets.GetNodeKey());
                if (!string.IsNullOrEmpty(tNodes))
                    MyTrustedNodes.AddRange(TheCommonUtils.cdeSplit(tNodes, ";", true, true));
            }
            if (string.IsNullOrEmpty(pNodeIDs))
                return false;
            int tLen = MyTrustedNodes.Count;
            if (tNodes?.Length > 0)
                tNodes += ";";
            else
                tNodes = "";
            tNodes += pNodeIDs;
            MyTrustedNodes.Clear();
            MyTrustedNodes.AddRange(CU.cdeSplit(tNodes, ";", true, true));
            if (MyTrustedNodes.Count != tLen)
            {
                if (UpdateSettings)
                    TheBaseAssets.MySettings.UpdateLocalSettings();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes all trusted Nodes
        /// </summary>
        /// <returns></returns>
        internal static bool RemoveAllTrustedNodes(bool UpdateSettings)
        {
            if (MyTrustedNodes?.Count > 0)
            {
                MyTrustedNodes = new List<string>();    //Reset the trusted nodes
                if (UpdateSettings)
                    TheBaseAssets.MySettings.UpdateLocalSettings();
                return true;
            }
            return false;
        }
        internal static List<string> MyTrustedNodes = null; //Needed by ScopeManager and UserManager
        #endregion


















        /// <summary>
        /// This is a migration function only to move old nodes to new format
        /// </summary>
        private static bool MigrateAppParameterToTPI(string CertScope)
        {
            if (TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID) return false;
            if (!File.Exists(CU.cdeFixupFileName("cache\\266n948691115001005155", true)))
                return false;
            Engines.StorageService.TheMirrorCache<TheAppParameter> MyAppParameterBase = new ()
            {
                MyStoreID = "266n948691115001005155",
                IsCachePersistent = true,
                UseSafeSave = true
            };
            MyAppParameterBase.LoadCacheFromDisk(true);
            TheAppParameter MyAppParameter = null;
            if (MyAppParameterBase.Count > 0)
                MyAppParameter = MyAppParameterBase.GetEntryByID(TheBaseAssets.MyServiceHostInfo.cdeMID);

            if (!(MyAppParameter == null || TheBaseAssets.MyServiceHostInfo.IsCloudService || TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID))
            {
                if (!string.IsNullOrEmpty(MyAppParameter.EasyScope))
                {
                    if (MyAppParameter.EasyScope.Length < 10) //4.106: Backwards compatible switch
                        TheBaseAssets.MyScopeManager.SetScopeIDFromEasyID(MyAppParameter.EasyScope);
                    else
                        TheBaseAssets.MyScopeManager.SetScopeIDFromScrambledID(MyAppParameter.EasyScope);
                    if (TheBaseAssets.MySecrets.IsInApplicationScope(MyAppParameter.FederationID))
                        TheBaseAssets.MyScopeManager.FederationID = MyAppParameter.FederationID;
                    if (!string.IsNullOrEmpty(CertScope) && !TheBaseAssets.MyScopeManager.IsValidScopeID(MyAppParameter.EasyScope, TheBaseAssets.MyScopeManager.GetScrambledScopeIDFromEasyID(CertScope)))
                        TheBaseAssets.MySYSLOG.WriteToLog(2829, new TSM("TheCDESettings", $"Scope in 266 ({TheBaseAssets.MyScopeManager.GetScopeHash(MyAppParameter.EasyScope, true)}) and Cert ({TheBaseAssets.MyScopeManager.GetScopeHash(TheScopeManager.GetScrambledScopeIDFromEasyID(CertScope), true)}) do not match! you might not be able to correct to cloud correctly", eMsgLevel.l2_Warning));
                }
                TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper = MyAppParameter.UseUserMapper;
                TheBaseAssets.MyServiceHostInfo.AllowRemoteAdministration = MyAppParameter.AllowRemoteAdministration;
                TheBaseAssets.MyServiceHostInfo.MyStationPort = MyAppParameter.MyStationPort;
                TheBaseAssets.MyServiceHostInfo.MyStationWSPort = MyAppParameter.MyStationWSPort;
                TheBaseAssets.MyServiceHostInfo.IsCloudNMIBlocked = MyAppParameter.BlockCloudNMI;
                TheBaseAssets.MyServiceHostInfo.IsSSLEnforced = MyAppParameter.EnforceTLS;
                TheBaseAssets.MyServiceHostInfo.MyDeviceInfo = MyAppParameter.MyDeviceInfo;
                if (TheBaseAssets.MyServiceHostInfo.MyDeviceInfo != null)
                    TheBaseAssets.MySecrets.SetNodeKey(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID);
                TheBaseAssets.MyServiceHostInfo.ServiceRoute = MyAppParameter.CloudServiceRoute;
                TheBaseAssets.MyServiceHostInfo.SetProxy(MyAppParameter.ProxyToken);
                TheBaseAssets.MyServiceHostInfo.IsCloudDisabled = MyAppParameter.IsCloudDisabled;
                TheBaseAssets.MyServiceHostInfo.RequiresConfiguration = MyAppParameter.RequiresConfiguration;
                TheBaseAssets.MyServiceHostInfo.MyStationName = MyAppParameter.MyStationName;
                if (TheBaseAssets.MyServiceHostInfo.EnableAutoLogin && MyAppParameter.MyUserInfo != null) //Only relevant for CDE in client Applications like Phone or Tablet apps
                    TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser = MyAppParameter.MyUserInfo;
                List<aCDESetting> tSettings = null;
                if (MyAppParameter.CustomParameter?.Count > 0)
                {
                    tSettings = new List<aCDESetting>();
                    foreach (var s in MyAppParameter.CustomParameter.GetDynamicEnumerable())
                    {
                        var tVal = CU.cdeDecrypt(s.Value, TheBaseAssets.MySecrets.GetNodeKey());
                        tSettings.Add(new aCDESetting { Name = s.Key, Value = tVal });
                    }
                }

                TheBaseAssets.MySettings.UpdateLocalSettings(tSettings);
                MyAppParameterBase.RemoveStore(false);
                return true;
            }
            return false;
        }
    }
}
