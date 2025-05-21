// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using nsCDEngine.Security;
using nsCDEngine.BaseClasses;

using System.Numerics;
#pragma warning disable 1591

namespace nsCDEngine.Activation
{
    /// <summary>
    /// Activation Flags
    /// </summary>
    [Flags]
    public enum ActivationFlags : byte
    {
        /// <summary>
        /// Require online registration of the activation key. Future: Enables moving the activation key to a different node, by de-registering it fist. Currently fails activation.
        /// </summary>
        RequireOnline = 1,
        /// <summary>
        /// Include expiration date in license signature
        /// </summary>
        VerifyExpiration = 2,
    }

    public static class TheActivationUtils
    {
        /// <summary>
        /// Flags requesting additional activation behaviors.
        /// </summary>

        public const int MaxLicenseParameters = 9;

        /// <summary>
        /// For use by C-Labs activation tooling only.
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="licenseSigningKey"></param>
        /// <param name="expirationInDaysSinceJan2016"></param>
        /// <param name="licenses"></param>
        /// <param name="licenseParams"></param>
        /// <param name="flags"></param>
        /// <param name="licenseSignature"></param>
        /// <returns></returns>
        // CODE REVIEW: This would ideally go into TheActivationUtils so we can keep it private/internal, but it needs access to TheBaseAssets.cdeAI. Should we duplicate that as well into TheActivationUtils?
        public static bool GenerateLicenseSignature(Guid deviceId, string licenseSigningKey, uint expirationInDaysSinceJan2016, TheLicense[] licenses, byte[] licenseParams, ActivationFlags flags, out byte[] licenseSignature)
        {
            //   License Authorization: DeviceId, Flags, Reserved, LicencesCount, LicenseIds, parameters
            byte[] licenseAuth = new byte[16 + ((flags & ActivationFlags.VerifyExpiration) != 0 ? 2 : 0) + 1 + 1 + 1 + licenses.Length * 16 + MaxLicenseParameters];
            licenseSignature = null;
            if (licenseParams.Length > MaxLicenseParameters)
            {
                return false;
            }

            deviceId.ToByteArray().CopyTo(licenseAuth, 0);
            licenseAuth[16] = (byte)flags;
            licenseAuth[17] = 0; // reserved
            int i = 18;
            if ((flags & ActivationFlags.VerifyExpiration) !=0)
            {
                licenseAuth[18] = (byte)(expirationInDaysSinceJan2016 & 0xFF);
                licenseAuth[19] = (byte)((expirationInDaysSinceJan2016 >> 8) & 0xFF);
                i += 2;
            }

            licenseAuth[i] = (byte)licenses.Length;
            i++;
            foreach (var license in licenses)
            {
                license.LicenseId.ToByteArray().CopyTo(licenseAuth, i);
                i += 16;
            }
            licenseParams.CopyTo(licenseAuth, i);

            byte[] signingKey = Encoding.UTF8.GetBytes(licenseSigningKey); //SECURITY-REVIEW: Please do not use appID here!starting 4.106 this will be only 5 digits the cdeAK is (will be) the hashed version of the AppID
            string additionalSigningKeyString = TheLicense.GetAdditionalSigningKeyString(licenses);
            if (additionalSigningKeyString.Length > 0)
            {
                byte[] additionalSigningKey = Encoding.UTF8.GetBytes(additionalSigningKeyString);
                byte[] signingKeyTemp = new byte[signingKey.Length + additionalSigningKey.Length];
                signingKey.CopyTo(signingKeyTemp, 0);
                additionalSigningKey.CopyTo(signingKeyTemp, signingKey.Length);
                signingKey = signingKeyTemp;
            }

            HMAC hmac;
            try
            {
                hmac = HMACSHA1.Create();   //NOSONAR  - not security sensitive context
                hmac.Key = signingKey;
            }
            catch (PlatformNotSupportedException)
            {
                try
                {
                    var sha1 = new HMACSHA1(signingKey);  //NOSONAR  - not security sensitive context
                    hmac = sha1;
                }
                catch (PlatformNotSupportedException)
                {
                    return false;
                }
            }
            licenseSignature = hmac.ComputeHash(licenseAuth);
            hmac.Dispose();
            return true;
        }

        /// <summary>
        /// Encodes the byte array into base32 encoding, using the character set specified by the ICDESecret crypto provider (default: "Crockford's base32")
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string Base32Encode(byte[] bytes)
        {
            // Pad with 0 to ensure positive number
            var bytesTemp = new byte[bytes.Length + 1];
            bytes.CopyTo(bytesTemp, 0);
            var x = new BigInteger(bytesTemp);
            StringBuilder output = new ();
            var base32CodeArray = TheBaseAssets.MySecrets.GetCodeArray();
            do
            {
                if ((output.Length + 1) % 7 == 0)
                {
                    output.Append("-");
                }
                int digit = (int)(x % 32);
                output.Append(base32CodeArray[digit]);
                x /= 32;
            } while (x != 0);
            return output.ToString().Reverse().Aggregate("", (s, c) => s + c);
        }

        /// <summary>
        /// Decodes a base32 encoded string into a byte array, using "Crockford's base32" character set
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static byte[] Base32Decode(string text)
        {
            var base32CodeArray = TheBaseAssets.MySecrets.GetCodeArray();

            BigInteger x = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char digit = text[i];
                var digitValue = base32CodeArray.IndexOf(digit);
                if (digitValue < 0)
                {
                    return null;
                }
                x = x * 32 + digitValue;
            }
            var bytes = x.ToByteArray();
            if (bytes[bytes.Length - 1] == 0)
            {
                bytes = bytes.Take(bytes.Length - 1).ToArray();
            }
            return bytes;
        }

    }
}
