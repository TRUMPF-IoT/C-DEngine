// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Engines.ThingService;
using System;
using System.Collections.Generic;

namespace nsCDEngine.Interfaces
{
    /// <summary>
    /// Setting information to be stored in cdeTPI
    /// </summary>
    public class aCDESetting
    {
        /// <summary>
        /// Key of the Setting
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Value of the Setting
        /// </summary>
        public string Value { get; set; }
        /// <summary>
        /// If true, the setting will not be displayed as a password in the NMI and hidden in logs and will not be added to MyCmdArgs
        /// </summary>
        public bool IsHidden { get; set; }
        /// <summary>
        /// Hint for the NMI what control to use for input (CheckBox for Boolean, Number for Number, DateTime etc)
        /// </summary>
        public ePropertyTypes ValueType { get; set; }

        /// <summary>
        /// Owner of the Setting
        /// </summary>
        public Guid? cdeO { get; set; }
        /// <summary>
        /// Friendly output
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"({Name}={Value} ({ValueType}";
        }
    }

    /// <summary>
    /// Interface for a Settings managing class
    /// </summary>
    public interface ICDESettings : ICDEEvents
    {
        /// <summary>
        /// Add/Set a local presisted Setting
        /// </summary>
        /// <param name="pSetting">Name of the setting</param>
        /// <param name="pValue">Value of the setting</param>
        /// <param name="bIsHidden">Do not show this in clear text in the NMI</param>
        /// <param name="pOwner">Owner Guid of the setting. If set this setting will be encrypted</param>
        /// <param name="pType">Type hint for the NMI</param>
        /// <returns></returns>
        bool SetSetting(string pSetting, string pValue, bool bIsHidden = false, Guid? pOwner = null, ePropertyTypes pType = ePropertyTypes.TString);

        /// <summary>
        /// Add/Set multiple local settings at once
        /// </summary>
        /// <param name="pSettings">List of all settings to be updated/set</param>
        /// <returns></returns>
        bool SetSettings(List<aCDESetting> pSettings);

        /// <summary>
        /// Gets a setting. The following order is applied:
        /// 1) Public setting coming from either the App.Config or coming from the host
        /// 2) if an environment variable with the setting name is specified it will be returned
        /// 3) if an Owner was specified the setting will be loaded from the secure settings store
        /// </summary>
        /// <param name="pSetting">Name of the setting</param>
        /// <param name="pOwner">Owner of the setting</param>
        /// <returns></returns>
        string GetSetting(string pSetting, Guid? pOwner=null);

        /// <summary>
        /// Determines whether the specified setting exists.
        /// </summary>
        /// <param name="pSetting">The setting.</param>
        /// <param name="pOwner">Owner of the setting if required</param>
        /// <returns><c>true</c> if the specified setting exists; otherwise, <c>false</c>.</returns>
        bool HasSetting(string pSetting, Guid? pOwner = null);

        /// <summary>
        /// Gets a setting and allows to specify if the setting was encryped in the app.config or private settings store
        /// </summary>
        /// <param name="pSetting">Name of the setting</param>
        /// <param name="alt">Alternative return if Setting was not found</param>
        /// <param name="IsEncrypted">if true, the setting is encrypted in the settings store (i.e. app.config)</param>
        /// <param name="IsAltDefault">Returns the alt even if the setting is set but empty</param>
        /// <param name="pOwner">Owner Guid of the setting. gives access to a private setting </param>
        /// <returns></returns>
        string GetAppSetting(string pSetting, string alt, bool IsEncrypted, bool IsAltDefault=false, Guid? pOwner = null);

        /// <summary>
        /// Deletes a setting from the settings store (app.config and/or private store)
        /// If an owner is specified, the setting will be moved from the public settings store to the private secure store
        /// </summary>
        /// <param name="pKeyname"></param>
        /// <param name="pOwner">Owner Guid of the setting. If set, the setting will be moved to the private store</param>
        void DeleteAppSetting(string pKeyname, Guid? pOwner = null);

        /// <summary>
        /// Tells the settings engine to update the local settings, save them securely to disk and fire the "SettingsUpdated" event
        /// </summary>
        /// <param name="pSettings"></param>
        /// <returns></returns>
        bool UpdateLocalSettings(List<aCDESetting> pSettings = null);

        /// <summary>
        /// Tells the settings engine to load all public settings from the settings store (i.e. app.config) and add them to the given dictionary
        /// </summary>
        /// <param name="argList">Dictionary of incoming settings that will be extended with the stored settings</param>
        /// <returns></returns>
        bool ReadAllAppSettings(ref IDictionary<string, string> argList);

        /// <summary>
        /// Returns a list of hidden keys for NMI and other plugins
        /// </summary>
        /// <returns></returns>
        List<string> GetHiddenKeyList();

        /// <summary>
        /// Checks if a node/device (ID) is trustd
        /// </summary>
        /// <param name="pNodeID">Source NodeID</param>
        /// <returns></returns>
        bool IsNodeTrusted(Guid pNodeID);

        /// <summary>
        /// Returns true if trusted nodes are configured
        /// </summary>
        bool HasTrustedNodes { get; }
    }
}
