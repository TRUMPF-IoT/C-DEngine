// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Collections.Generic;
using nsCDEngine.ViewModels;
using nsCDEngine.Discovery.DeviceSearch;

namespace nsCDEngine.Discovery
{
    /// <summary>
    /// The ICommonDisco interface allows access to the UPnP subsystem of the C-DEngine.
    /// It exposes all publicly available interfaces
    /// </summary>
    public interface ICommonDisco
    {
        /// <summary>
        /// This method registers a callback for the event that the UPnP subsystem found another node that is compatible with the current node.
        /// In order for nodes to be compatible they have to be
        /// a) in the same Solution (same ApplicationID)
        /// b) use the same ScopeID
        /// c) have the same Application Name (TheBaseAssets.MyServiceHostInfo.ApplicationName)
        /// </summary>
        /// <param name="pMatch">A callback that will receive the NodeID in the Guid, the URL in the string and a list of supported plugins/services in the List</param>
        void RegisterNewMatch(Action<Guid, string, List<string>> pMatch);

        /// <summary>
        /// Unregisters a previously registered callback
        /// </summary>
        /// <param name="pUnMatch"></param>
        void UnRegisterNewMatch(Action<Guid, string, List<string>> pUnMatch);

        /// <summary>
        /// This method allows to register for any arbitrary UPnP device.
        ///
        /// </summary>
        /// <param name="pUid">either a UUID of the device to look for or a search pattern with the syntax "UPnPTag;:;Instring Match of Tag Content"</param>
        /// <param name="CallBack">A callback with the full TheUPnPDeviceInfo of the discovered device</param>
        void RegisterUPnPUID(string pUid, Action<TheUPnPDeviceInfo> CallBack);

        /// <summary>
        /// Tiggers a scan for a specific Device UID (i.e. USN for UPnP or Domain on mDNS)
        /// Results will be fired to the callbacks registered with RegisterUPnPUID
        /// </summary>
        /// <param name="pUid"></param>
        void ScanForDevice(string pUid);

        /// <summary>
        /// Scans already registered devices for a given pUid
        /// </summary>
        /// <param name="pUid"></param>
        void ScanKnownDevices(string pUid);
        /// <summary>
        /// Unregisters a previously registered search pattern
        /// </summary>
        /// <param name="pUid">UUID or pattern match of the callback</param>
        void UnregisterUPnPUID(string pUid);

        /// <summary>
        /// Register a callback for an event that fires when a device is no longer reachable
        /// </summary>
        /// <param name="pUid"></param>
        /// <param name="CallBack"></param>
        void RegisterDeviceLost(string pUid, Action<TheUPnPDeviceInfo> CallBack);

        /// <summary>
        /// Unregisters the callback for lost/unreachable devices
        /// </summary>
        /// <param name="pUid"></param>
        void UnregisterDeviceLost(string pUid);

        /// <summary>
        /// Fires a callback if the device is in the DeviceRegistry
        /// </summary>
        /// <param name="tHistory"></param>
        /// <param name="pLocationUrl">Location URL of the Device. If set to zero, this function will not do anything</param>
        void CheckForDeviceMatch(TheUPnPDeviceInfo tHistory, Uri pLocationUrl);

        /// <summary>
        /// Call to shutdown the UPnP Discovery service.
        /// </summary>
        void ShutdownDiscoService();

        /// <summary>
        /// Call to Start the UPnP Discovery service
        /// </summary>
        void StartDiscoDevice();

        /// <summary>
        /// Updates the UPnPDevice Information of the current node
        /// </summary>
        void UpdateDiscoDevice();


        /// <summary>
        /// Updates the Security Context of the UPnP Device Description required to match Nodes in a mesh
        /// </summary>
        void UpdateContextID();

        /// <summary>
        /// Triggers a scan for UPnP devices on the network. (Sends M-SEARCH Broadcast).
        /// The C-DEngine UPnP subsystem is doing this on a regular interval but if immediate results are required, the subsystem can be forced to trigger the scan.
        /// </summary>
        void Scan4Devices();

        /// <summary>
        /// Returns TheUPnPDeviceInfo for a given USN/UUID
        /// </summary>
        /// <param name="USN">The USN/UUID of the requested Device</param>
        /// <returns>Null if the device could not be found - otherwise returns all UPnP information gathered</returns>
        TheUPnPDeviceInfo GetUPnPDeviceInfo(Guid USN);

        /// <summary>
        /// Returns devices found by a function query
        /// </summary>
        /// <param name="pFunc"></param>
        /// <returns></returns>
        TheUPnPDeviceInfo GetUPnPDeviceInfoByFunc(Func<TheUPnPDeviceInfo, bool> pFunc);
        /// <summary>
        /// Allows to register a new TheUPnPDeviceInfo with the UPnP subsystem. This is helpful if any other means of discovery was used to find devices and the new device should participate in the UpnP notification system.
        /// </summary>
        /// <param name="pInfo">The description of the new devics</param>
        void RegisterDevice(TheUPnPDeviceInfo pInfo);

        /// <summary>
        /// Allows to register a new TheUPnPDeviceInfo with the DISCO subsystem. This is helpful if any other means of discovery was used to find devices and the new device should participate in the UpnP notification system.
        /// </summary>
        /// <param name="pInfo">The description of the new devics</param>
        /// <param name="ForceUpdate">If this is set to true, the UPnP Subsystem waits until any previous write attempts to the UPnP Database were completed and then updates the record. The default (False) will ignore the registration if the UPnP Database is busy to avoid congestions and lookups.</param>
        /// <returns>Return true if registration was successful</returns>
        bool RegisterDevice(TheUPnPDeviceInfo pInfo, bool ForceUpdate);
        /// <summary>
        /// Registers the current device with a C-DEngine UPnP subsystem at the given Uri
        /// </summary>
        /// <param name="pTargetServer">Uri of the target node with a C-DEngine UPnP subsystem</param>
        /// <param name="pCallback">If this callback is specified, the caller can check if the registration was successful (OK=Was successful; FAILED=registeration failed). If it was successful, TheUPnPDeviceInfo of the Server is returned in the callback</param>
        void RegisterDeviceWithServer(Uri pTargetServer, Action<string, TheUPnPDeviceInfo> pCallback);

        /// <summary>
        /// Gets a list of all UPnP devices currently recognized by the UPnP subsystem.
        /// </summary>
        /// <returns>List of all UPnP Devices in the subsystem</returns>
        List<TheUPnPDeviceInfo> GetAllUPnPDeviceInfo();

        /// <summary>
        /// Allows to retrieve a list of UPnP devices
        /// </summary>
        /// <param name="pFunc">a function that allows to filter on all discovered UPnP Devices</param>
        /// <returns>List of all UPnP Devices in the subsystem that matched the filter</returns>
        List<TheUPnPDeviceInfo> GetAllUPnPDeviceInfo(Func<TheUPnPDeviceInfo, bool> pFunc);

        /// <summary>
        /// Gets a byte[] of the UPnP device info of the current device.
        /// </summary>
        /// <returns>byte[] of the UPnP device</returns>
        byte[] GetDeviceInfo();

        /// <summary>
        /// returns TheUPnPDeviceInfo of the current node
        /// </summary>
        /// <returns>TheUPnPDeviceInfo of the current node</returns>
        TheUPnPDeviceInfo GetTheUPnPDeviceInfo();

        /// <summary>
        /// Registers a new sniffer
        /// </summary>
        /// <param name="pScanner"></param>
        /// <returns></returns>
        bool RegisterScanner(ICDESniffer pScanner);

        /// <summary>
        /// Unregisters a sniffer
        /// </summary>
        /// <param name="pScanner"></param>
        /// <returns></returns>
        bool UnregisterScanner(ICDESniffer pScanner);

        /// <summary>
        /// Registers a new discovery service
        /// </summary>
        /// <param name="pScanner"></param>
        /// <returns></returns>
        bool RegisterDISCOService(ICDEDiscoService pScanner);

        /// <summary>
        /// Unregisters a discovery service
        /// </summary>
        /// <param name="pScanner"></param>
        /// <returns></returns>
        bool UnregisterDISCOService(ICDEDiscoService pScanner);

        /// <summary>
        /// Registers for the event of "Disco Service Ready"
        /// </summary>
        /// <param name="bIsReady">true if service is ready, false it disco was shutdown</param>
        void WaitForDiscoReady(Action<bool> bIsReady);

    }
}
