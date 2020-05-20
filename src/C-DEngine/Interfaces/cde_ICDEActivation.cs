// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Activation;
using System;
using System.Collections.Generic;

namespace nsCDEngine.Interfaces
{
    /// <summary>
    /// Interface ICDEActivation
    /// </summary>
    public interface ICDEActivation
    {
        /// <summary>
        /// Initializes the licenses.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool InitializeLicenses();
        /// <summary>
        /// Checks for pinned license.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool CheckForPinnedLicense();
        /// <summary>
        /// Checks the license.
        /// </summary>
        /// <param name="pluginId">The plugin identifier.</param>
        /// <param name="deviceType">Type of the device.</param>
        /// <param name="licenseAuthorities">The license authorities.</param>
        /// <param name="requestedConnectionCount">The requested connection count.</param>
        /// <param name="useThingEntitlement">if set to <c>true</c> [use thing entitlement].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool CheckLicense(Guid pluginId, string deviceType, string[] licenseAuthorities, int requestedConnectionCount, bool useThingEntitlement = false);
        /// <summary>
        /// Creates the default license.
        /// </summary>
        /// <param name="pEngineID">The p engine identifier.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool CreateDefaultLicense(Guid pEngineID);
        /// <summary>
        /// Releases the license.
        /// </summary>
        /// <param name="pluginId">The plugin identifier.</param>
        /// <param name="deviceType">Type of the device.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool ReleaseLicense(Guid pluginId, string deviceType);
        /// <summary>
        /// Gets the activated licenses.
        /// </summary>
        /// <param name="plugInId">The plug in identifier.</param>
        /// <param name="includeExpired">if set to <c>true</c> [include expired].</param>
        /// <returns>A friendly description of activated licenses</returns>
        List<TheActivatedLicense> GetActivatedLicenses(Guid plugInId, bool includeExpired = false);

        //These must be public as they are used in Plugins
        /// <summary>
        /// Determines whether [is sku activated] [the specified sku].
        /// </summary>
        /// <param name="sku">The sku.</param>
        /// <returns><c>true</c> if [is sku activated] [the specified sku]; otherwise, <c>false</c>.</returns>
        bool IsSKUActivated(uint sku);
        /// <summary>
        /// Gets the activation request key.
        /// </summary>
        /// <param name="SkuId">The sku identifier.</param>
        /// <returns>System.String.</returns>
        string GetActivationRequestKey(uint SkuId);
        /// <summary>
        /// Applies the activation key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="licenseId">The license identifier.</param>
        /// <param name="expirationDate">The expiration date.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool ApplyActivationKey(string key, Guid licenseId, out DateTimeOffset expirationDate);

        /// <summary>
        /// Gets the installed licenses.
        /// </summary>
        /// <returns>List&lt;TheLicense&gt;.</returns>
        List<TheLicense> GetInstalledLicenses();
        /// <summary>
        /// Gets the license activation status.
        /// </summary>
        /// <param name="licenseId">The license identifier.</param>
        /// <param name="activationExpiration">The activation expiration.</param>
        /// <returns>TheActivatedLicense.</returns>
        TheActivatedLicense GetLicenseActivationStatus(Guid licenseId, out DateTimeOffset activationExpiration);
        /// <summary>
        /// Gets the activation parameter.
        /// </summary>
        /// <param name="plugInId">The plug in identifier.</param>
        /// <param name="parameterName">Name of the parameter.</param>
        /// <param name="nextExpiration">The next expiration.</param>
        /// <returns>System.Int32.</returns>
        int GetActivationParameter(Guid plugInId, string parameterName, out DateTimeOffset nextExpiration);

        /// <summary>
        /// Waits for license activated.
        /// </summary>
        /// <param name="licenseId">The license identifier.</param>
        /// <param name="callback">The callback.</param>
        void WaitForLicenseActivated(Guid licenseId, Action<object, object> callback);
        /// <summary>
        /// Registers the event.
        /// </summary>
        /// <param name="pEventName">Name of the event.</param>
        /// <param name="pCallback">The callback.</param>
        void RegisterEvent(string pEventName, Action<object, object> pCallback);
        /// <summary>
        /// Unregisters the event.
        /// </summary>
        /// <param name="pEventName">Name of the event.</param>
        /// <param name="pCallback">The callback.</param>
        void UnregisterEvent(string pEventName, Action<object, object> pCallback);
        /// <summary>
        /// Clears all events.
        /// </summary>
        void ClearAllEvents();
    }
}
