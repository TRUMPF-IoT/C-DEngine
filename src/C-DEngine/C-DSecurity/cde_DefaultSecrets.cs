// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;

namespace nsCDEngine.Security
{
    internal class TheDefaultSecrets : ICDESecrets
    {
        private static byte[] cdeAK = { 0xd5, 0x95, 0xf0, 0xea, 0xc4, 0xa2, 0x41, 0x1c, 0x96, 0xdb, 0x41, 0xd3, 0x6d, 0xdb, 0xbf, 0x6d };
        public byte[] GetAK()
        {
            return cdeAK;
        }
        private static byte[] cdeAI = { 0x77, 0x0d, 0x66, 0x7b, 0xa7, 0x7e, 0x41, 0x3a, 0x88, 0x2d, 0xf2, 0xb9, 0xa, 0x3c, 0xdc, 0xae };
        public byte[] GetAI()
        {
            return cdeAI;
        }

        internal static byte[] cdeK = { 0xaf,0xca,0xb6,0xa4, 0x58,0xe8, 0x4c,0xed, 0x89, 0x7a, 0x6, 0xc8, 0xad, 0xa5, 0x8, 0xcb };
        internal static byte[] cdeI = { 0xd2,0xe5,0xe3,0x5b, 0x7c,0x4a, 0x4c,0x5b, 0xb2, 0xf3, 0x95, 0x8c, 0x99, 0xcf, 0xca, 0x11 };

        // Crockford's Base32: no I L O U to minimize human reading errors (https://en.wikipedia.org/wiki/Base32#Crockford%27s_Base32)
        private static List<char> cdeCodeArray { get; } = new List<char> { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', /*I */'J', 'K', /*L */ 'M', 'N', /*O*/ 'P', 'Q', 'R', 'S', 'T', /*U*/ 'V', 'W', 'X', 'Y', 'Z' };
        public List<char> GetCodeArray()
        {
            return cdeCodeArray;
        }

        // TODO Update this with official key
        private static readonly byte[] clabsLicensePublicRSAKeyCSPBlob = new byte[] { 7, 2, 0, 0, 0, 36, 0, 0, 82, 83, 65, 50, 0, 4, 0, 0, 1, 0, 1, 0, 21, 17, 123, 167, 63, 93, 243, 184, 12, 109, 232, 229, 15, 154, 130, 63, 171, 242, 129, 156, 33, 47, 93, 236, 161, 94, 27, 145, 129, 214, 104, 73, 192, 224, 213, 189, 96, 209, 38, 39, 218, 167, 198, 34, 110, 90, 247, 14, 152, 46, 48, 67, 89, 20, 198, 220, 136, 149, 7, 5, 223, 53, 239, 113, 192, 76, 157, 90, 114, 71, 54, 2, 171, 128, 254, 96, 9, 109, 124, 197, 161, 158, 242, 92, 127, 135, 141, 149, 123, 127, 4, 84, 189, 22, 10, 191, 110, 108, 178, 190, 73, 162, 75, 36, 160, 23, 73, 184, 170, 195, 180, 80, 86, 79, 74, 97, 185, 223, 233, 221, 116, 40, 177, 128, 54, 73, 17, 173, 249, 31, 64, 15, 125, 18, 197, 136, 150, 66, 98, 91, 38, 118, 192, 167, 246, 239, 49, 193, 161, 168, 183, 228, 180, 27, 33, 8, 132, 28, 69, 2, 4, 134, 82, 135, 234, 40, 216, 175, 139, 227, 58, 55, 227, 118, 158, 44, 173, 113, 33, 177, 212, 171, 92, 122, 129, 142, 129, 100, 216, 155, 58, 215, 253, 56, 69, 112, 108, 253, 69, 171, 231, 131, 99, 133, 97, 93, 166, 59, 12, 241, 207, 5, 238, 77, 7, 5, 188, 236, 190, 240, 15, 86, 124, 219, 98, 152, 45, 209, 93, 169, 66, 89, 116, 113, 131, 104, 129, 187, 37, 68, 216, 82, 225, 139, 82, 120, 229, 67, 199, 10, 94, 240, 2, 22, 218, 205, 161, 112, 155, 226, 165, 71, 105, 53, 254, 148, 44, 84, 247, 136, 176, 235, 81, 23, 166, 114, 95, 220, 172, 169, 145, 50, 134, 144, 61, 190, 37, 66, 215, 205, 130, 149, 35, 94, 104, 154, 8, 44, 150, 85, 255, 141, 105, 1, 252, 28, 128, 125, 104, 85, 44, 106, 246, 165, 215, 180, 153, 219, 132, 28, 21, 225, 202, 83, 122, 53, 138, 211, 208, 87, 112, 241, 200, 223, 130, 195, 165, 178, 234, 14, 20, 38, 35, 213, 87, 108, 89, 147, 198, 188, 192, 109, 236, 146, 57, 109, 150, 149, 75, 247, 100, 67, 171, 26, 212, 221, 125, 117, 2, 131, 7, 134, 14, 225, 101, 41, 199, 142, 36, 181, 127, 180, 10, 129, 234, 142, 66, 16, 18, 220, 77, 53, 32, 188, 124, 1, 73, 227, 142, 2, 186, 42, 85, 173, 35, 252, 247, 143, 196, 246, 209, 120, 245, 223, 108, 188, 24, 16, 138, 27, 39, 83, 105, 155, 158, 6, 29, 203, 165, 89, 240, 154, 199, 199, 151, 250, 65, 63, 249, 36, 127, 42, 66, 199, 57, 126, 138, 152, 249, 173, 203, 108, 129, 220, 242, 15, 69, 119, 248, 26, 147, 234, 231, 116, 223, 60, 93, 30, 197, 97, 201, 153, 161, 131, 236, 36, 191, 222, 237, 51, 23, 53, 176, 10, 13, 238, 231, 52, 44, 6, 241, 130, 119, 7, 255, 86, 184, 179, 201, 157, 32, 34, 50, 169, 204, 16, 8, 102, 178, 153, 106, 198, 191, 3, 162, 153, 101, 235, 198, 245, 193, 87, 180, 34, 70, 117, 162, 201, 109, 106, 149, 167, 227, 128, 203, 225, 36, 103, 197, 235, 212, 94, 242, 106, 209, 80, 4, 175, 229, 182, 71, 95, 18, 8, 141, 210, 171, 135, 111, 40, 199, 97, 102, 185, 234, 91, 60, 232, 215, 218, 66, 169, 37, 200, 255, 5 };
        internal static List<byte[]> TrustedLicenseSignerPublicKeys = new List<byte[]> { clabsLicensePublicRSAKeyCSPBlob }; // TODO add trusted keys and make them configurable?
        internal static string CryptoVersionString = "Core-Crypto OSS";
        private static string ApID5 = null;
        public string GetApID5() { return ApID5; }
        /// <summary>
        /// Create a unique Key for this application (Cloud and Nodes must have the same key)
        /// </summary>
        /// <returns></returns>
        internal static string F()
        {
            return "CreateSomeKeyThatIsUniqueButDoesNotChangePerApplication";
        }

        public string CryptoVersion()
        {
            return CryptoVersionString;
        }

        /// <summary>
        /// Verify if the incoming Application ID is in the Application Scope
        /// </summary>
        /// <param name="pAppID"></param>
        /// <returns></returns>
        public bool IsInApplicationScope(string pAppID)
        {
            return ApID5?.Length > 4;
        }
        /// <summary>
        /// Verify if the set ApplicationID (cdeAK) is valid
        /// </summary>
        /// <returns></returns>
        public bool IsApplicationIDValid()
        {
            return ApID5?.Length > 4;
        }

        /// <summary>
        /// This function allows to override the cdeAK with a custom "key"
        /// TODO: Create your own cdeAK Keyset from the incoming cleartext Key
        /// </summary>
        /// <param name="pMySecretKey"></param>
        public bool SatKays(string pMySecretKey)
        {
            if (string.IsNullOrEmpty(pMySecretKey) || pMySecretKey.Length<5)
                return false;
            ApID5 = pMySecretKey.Substring(0, 5); //SECURITY-REVIEW: We must not store the full AppID in the CDE. We need the first 5 Digits as salt for the connect

            var tKeyBytes = CU.CUTF8String2Array(pMySecretKey);
            for (int i = 0; i < tKeyBytes.Length && i < 16; i++)
                cdeAK[i] = tKeyBytes[i];
            return true;
        }

        private static Guid mNodeSecurityID = Guid.Empty;
        public byte[] GetNodeKey()
        {
            return mNodeSecurityID.ToByteArray();
        }
        public void SetNodeKey(Guid pKey)
        {
            mNodeSecurityID = pKey;
        }
        /// <summary>
        /// Creates a hash for a password
        /// </summary>
        /// <param name="inPW">incoming password</param>
        /// <returns></returns>
        public string CreatePasswordHash(string inPW)
        {
            if (string.IsNullOrEmpty(inPW))
                return "";
            var hashedPassword = HashWithSHA256(CU.CUTF8String2Array(inPW));
            return Convert.ToBase64String(hashedPassword);
        }

        /// <summary>
        /// Returns true if the expected length is correct
        /// </summary>
        /// <returns></returns>
        public bool IsHashLengthCorrect(string pHash)
        {
            return (pHash?.Length == 44); //256Hash always 44 bytes
        }

        /// <summary>
        /// Verifies if the given password is valid
        /// </summary>
        /// <param name="pRealPW"></param>
        /// <returns></returns>
        public bool IsValidPassword(string pRealPW)
        {
            //TODO: you can use regex for more sophisticated checks i.e. var RegExp = new RegExp("((?=.*\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[\W_]).{8,25})");
            if (string.IsNullOrEmpty(pRealPW) || pRealPW.Length < 1) //Min 1 digit
                return false;
            return true;
        }

        /// <summary>
        /// Compares an incoming password with a hash
        /// </summary>
        /// <param name="pPWHash"></param>
        /// <param name="pRealPWIncoming"></param>
        /// <returns></returns>
        public bool DoPasswordsMatch(string pPWHash, string pRealPWIncoming)
        {
            if (string.IsNullOrEmpty(pPWHash)) return false;

            var InHash= CreatePasswordHash(pRealPWIncoming);
            if (pPWHash.Equals(InHash))
                return true;
            return false;
        }

        /// <summary>
        /// Returns a fixed DeviceID for a given hardware allowing to lock licenses or keys to a machine
        /// </summary>
        /// <param name="currentDeviceId"></param>
        /// <param name="secretKey"></param>
        /// <returns></returns>
        public Guid GetPinnedDeviceId(Guid currentDeviceId, string secretKey)
        {
            //TODO: you can Find more stable ways of pinning a deviceid for a machine (i.e. store in regkey or credential store): for now pin to MAC address
            //var macAddress = Discovery.TheNetworkInfo.GetMACAddress(false);
            if (!string.IsNullOrEmpty(secretKey))
            {
                return GetGuidForName(secretKey, currentDeviceId, 5);
            }
            return Guid.Empty;
        }

        internal static string GetcdeMasterKey()
        {
            byte[] v = new byte[] { 176, 145, 147, 134, 188, 179, 158, 157, 140, 183, 158, 140, 171, 151, 150, 140, 180, 154, 134 };
            return F(v);
        }


        #region private functions
        private static Guid GetGuidForName(string name, Guid nsGuid, int version)
        {
            if (version != 3 && version != 5)
            {
                return Guid.Empty;
            }

            var nameAsBytes = Encoding.UTF8.GetBytes(name);

            var nsBytes = nsGuid.ToByteArray();
            CU.GuidSwitchNetworkOrder(nsBytes);

            byte[] canonicalName = new byte[nsBytes.Length + nameAsBytes.Length];
            nsBytes.CopyTo(canonicalName, 0);
            nameAsBytes.CopyTo(canonicalName, nsBytes.Length);
            var hashAlg = version == 5 ? (HashAlgorithm)SHA1.Create() : MD5.Create();// HMACSHA1(Encoding.UTF8.GetBytes(sha1key));
            var machineNumberHash = hashAlg.ComputeHash(canonicalName);

            byte[] nameGuidBytes = machineNumberHash.Take(16).ToArray();
            nameGuidBytes[8] &= 0x3F;   // Clear RFC4122 Variant bits
            nameGuidBytes[8] |= 0x80;   // RFC4122 Standard Variant
            nameGuidBytes[6] &= 0x0F;   // Clear RFC4122 Version bits
            nameGuidBytes[6] |= (byte)(version << 4); // RFC4122 Version 5 - Name-based SHA1

            CU.GuidSwitchNetworkOrder(nameGuidBytes);

            var nameGuid = new Guid(nameGuidBytes);
            return nameGuid;
        }

        private static string F(byte[] v)
        {
            return v.Select(b => (char)(b ^ 0xFF)).ToArray().Aggregate("", (s, c) => s + c);
        }

        private static byte[] HashWithSHA256(byte[] toBeHashed)
        {
            using (var sha256 = new HMACSHA256(cdeAK))
            {
                return sha256.ComputeHash(toBeHashed);
            }
        }

        public List<byte[]> GetLicenseSignerPublicKeys()
        {
            return TrustedLicenseSignerPublicKeys;
        }

        public string GetActivationKeySignatureKey()
        {
            return F() + GetcdeMasterKey();
        }

        #endregion

    }
}
