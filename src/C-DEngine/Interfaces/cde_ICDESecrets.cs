// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;

namespace nsCDEngine.Interfaces
{
    /// <summary>
    /// Interface ICDESecrets
    /// </summary>
    public interface ICDESecrets
    {
        /// <summary>
        /// Cryptoes the version.
        /// </summary>
        /// <returns>System.String.</returns>
        string CryptoVersion(); //used

        /// <summary>
        /// Determines whether [is in application scope] [the specified p application identifier].
        /// </summary>
        /// <param name="pAppID">The p application identifier.</param>
        /// <returns><c>true</c> if [is in application scope] [the specified p application identifier]; otherwise, <c>false</c>.</returns>
        bool IsInApplicationScope(string pAppID); //used
        /// <summary>
        /// Determines whether [is application identifier valid].
        /// </summary>
        /// <returns><c>true</c> if [is application identifier valid]; otherwise, <c>false</c>.</returns>
        bool IsApplicationIDValid();
        /// <summary>
        /// Sats the kays.
        /// </summary>
        /// <param name="pMySecretKey">The p my secret key.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool SatKays(string pMySecretKey);
        /// <summary>
        /// Gets the ai.
        /// </summary>
        /// <returns>System.Byte[].</returns>
        byte[] GetAI();
        /// <summary>
        /// Gets the ak.
        /// </summary>
        /// <returns>System.Byte[].</returns>
        byte[] GetAK();
        /// <summary>
        /// Gets the node key.
        /// </summary>
        /// <returns>System.Byte[].</returns>
        byte[] GetNodeKey();
        /// <summary>
        /// Gets the a five digits AppID.
        /// </summary>
        /// <returns>System.String.</returns>
        string GetApID5();
        /// <summary>
        /// Gets the code array.
        /// </summary>
        /// <returns>List&lt;System.Char&gt;.</returns>
        List<char> GetCodeArray();
        /// <summary>
        /// Sets the node key.
        /// </summary>
        /// <param name="pKey">The p key.</param>
        void SetNodeKey(Guid pKey);
        /// <summary>
        /// Gets the pinned device identifier.
        /// </summary>
        /// <param name="currentDeviceId">The current device identifier.</param>
        /// <param name="secretKey">The secret key.</param>
        /// <returns>Guid.</returns>
        Guid GetPinnedDeviceId(Guid currentDeviceId, string secretKey);

        /// <summary>
        /// Creates the password hash.
        /// </summary>
        /// <param name="inPW">The in pw.</param>
        /// <returns>System.String.</returns>
        string CreatePasswordHash(string inPW); //used
        /// <summary>
        /// Does the passwords match.
        /// </summary>
        /// <param name="pPWHash">The p pw hash.</param>
        /// <param name="pRealPWIncoming">The p real pw incoming.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool DoPasswordsMatch(string pPWHash, string pRealPWIncoming); //used
        /// <summary>
        /// Determines whether [is valid password] [the specified p real pw].
        /// </summary>
        /// <param name="pRealPW">The real pw.</param>
        /// <returns><c>true</c> if [is valid password] [the specified p real pw]; otherwise, <c>false</c>.</returns>
        bool IsValidPassword(string pRealPW);
        /// <summary>
        /// Determines whether [is hash length correct] [the specified p hash].
        /// </summary>
        /// <param name="pHash">The hash.</param>
        /// <returns><c>true</c> if [is hash length correct] [the specified p hash]; otherwise, <c>false</c>.</returns>
        bool IsHashLengthCorrect(string pHash); //used
        /// <summary>
        /// Returns a list of RSA public keys that are used to validate any .cdel license files. If the license file is signed by at least one of these public key holders, it is considered valid.
        /// </summary>
        /// <returns></returns>
        List<byte[]> GetLicenseSignerPublicKeys();
        /// <summary>
        /// Returns the key (arbitrary string) that is used to verify the licenses covered by an activation key
        /// </summary>
        /// <returns></returns>
        string GetActivationKeySignatureKey();
    }
}
