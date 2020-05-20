// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.ThingService;

namespace nsCDEngine.Discovery.DeviceSearch
{
    /// <summary>
    /// Interface for network sniffers to auto-detect new devices
    /// </summary>
    public interface ICDESniffer
    {
        /// <summary>
        /// Starts scanning on this sniffer
        /// </summary>
        /// <param name="pDisco"></param>
        void StartScan(ICommonDisco pDisco);

        /// <summary>
        /// Sniffer will stop scanning
        /// </summary>
        void StopScan();

        /// <summary>
        /// Sniffer will do a single pass
        /// </summary>
        void Scan4Devices();

        /// <summary>
        /// Register a callback if a new device was found
        /// </summary>
        /// <param name="pInfo"></param>
        void RegisterFoundDevice(Action<TheUPnPDeviceInfo> pInfo);
        /// <summary>
        /// Unregister a callback if a new device was found
        /// </summary>
        /// <param name="pInfo"></param>
        void UnregisterFoundDevice(Action<TheUPnPDeviceInfo> pInfo);

        /// <summary>
        /// Register a callback that notifies if a device was lost
        /// </summary>
        /// <param name="pCallBack"></param>
        void RegisterDeviceLost(Action<string> pCallBack);
        /// <summary>
        /// unegister a callback that notifies if a device was lost
        /// </summary>
        /// <param name="pCallBack"></param>
        void UnregisterDeviceLost(Action<string> pCallBack);

        /// <summary>
        /// Scans for specific devices with a given UID string
        /// </summary>
        /// <param name="pUid"></param>
        void ScanForDevice(string pUid);

        /// <summary>
        /// Returns the base thing of the sniffer
        /// </summary>
        /// <returns></returns>
        TheThing GetBaseThing();
    }

    /// <summary>
    /// Interface for the Discovery service
    /// </summary>
    public interface ICDEDiscoService
    {
        /// <summary>
        /// Updates the discovery service with a new name
        /// </summary>
        /// <param name="pNewName"></param>
        void UpdateDiscoService(string pNewName);

        /// <summary>
        /// Updates the context (ScopeID) of the discovery service
        /// </summary>
        /// <param name="pScrambledID"></param>
        void UpdateContextID(string pScrambledID);

        /// <summary>
        /// Starts the discovery service
        /// </summary>
        /// <param name="pDisco"></param>
        void StartService(ICommonDisco pDisco);

        /// <summary>
        /// Stops the discovery service
        /// </summary>
        void StopService();

        /// <summary>
        /// Gets the device Information of the hosting node
        /// </summary>
        /// <returns></returns>
        byte[] GetDeviceInfo();

        /// <summary>
        /// Retuns true if the current node has XML based device information
        /// </summary>
        /// <returns></returns>
        bool HasDeviceXMLInfo();

        /// <summary>
        /// Gets the base thing of the DiscoService object
        /// </summary>
        /// <returns></returns>
        TheThing GetBaseThing();

        /// <summary>
        /// Retuns the device details asynchronously
        /// </summary>
        /// <param name="pInfo">Requested information</param>
        /// <param name="pCallback">Callback when requested information was found</param>
        void GetDeviceDetails(TheUPnPDeviceInfo pInfo, Action<TheUPnPDeviceInfo> pCallback);
    }
}
