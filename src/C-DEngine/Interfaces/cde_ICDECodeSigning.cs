// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

namespace nsCDEngine.Interfaces
{
    /// <summary>
    /// Interface ICDECodeSigning
    /// </summary>
    public interface ICDECodeSigning
    {
        /// <summary>
        /// Gets the application cert.
        /// </summary>
        /// <param name="bDontVerifyTrust">if set to <c>true</c> [b the CDE will not verify the certificate].</param>
        /// <param name="pFromFile">The file from where to load the certificate.</param>
        /// <param name="bVerifyTrustPath">if set to <c>true</c> [b verify trust path].</param>
        /// <param name="bDontVerifyIntegrity">if set to <c>true</c> [b dont verify integrity].</param>
        /// <returns>System.String.</returns>
        string GetAppCert(bool bDontVerifyTrust = false, string pFromFile = null, bool bVerifyTrustPath = true, bool bDontVerifyIntegrity = false);
        /// <summary>
        /// Determines whether the specified file is trusted.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns><c>true</c> if the specified file name is trusted; otherwise, <c>false</c>.</returns>
        bool IsTrusted(string fileName);
    }
}
