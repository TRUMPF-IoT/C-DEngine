// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.Activation;
using nsCDEngine.BaseClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
#pragma warning disable 1591

namespace nsCDEngine.ActivationKey
{
    public class TheActivationKeyGenerator
    {
        //private static byte[] clabsLicensePublicRSAKeyCSPBlob = new byte[] { 7, 2, 0, 0, 0, 36, 0, 0, 82, 83, 65, 50, 0, 4, 0, 0, 1, 0, 1, 0, 21, 17, 123, 167, 63, 93, 243, 184, 12, 109, 232, 229, 15, 154, 130, 63, 171, 242, 129, 156, 33, 47, 93, 236, 161, 94, 27, 145, 129, 214, 104, 73, 192, 224, 213, 189, 96, 209, 38, 39, 218, 167, 198, 34, 110, 90, 247, 14, 152, 46, 48, 67, 89, 20, 198, 220, 136, 149, 7, 5, 223, 53, 239, 113, 192, 76, 157, 90, 114, 71, 54, 2, 171, 128, 254, 96, 9, 109, 124, 197, 161, 158, 242, 92, 127, 135, 141, 149, 123, 127, 4, 84, 189, 22, 10, 191, 110, 108, 178, 190, 73, 162, 75, 36, 160, 23, 73, 184, 170, 195, 180, 80, 86, 79, 74, 97, 185, 223, 233, 221, 116, 40, 177, 128, 54, 73, 17, 173, 249, 31, 64, 15, 125, 18, 197, 136, 150, 66, 98, 91, 38, 118, 192, 167, 246, 239, 49, 193, 161, 168, 183, 228, 180, 27, 33, 8, 132, 28, 69, 2, 4, 134, 82, 135, 234, 40, 216, 175, 139, 227, 58, 55, 227, 118, 158, 44, 173, 113, 33, 177, 212, 171, 92, 122, 129, 142, 129, 100, 216, 155, 58, 215, 253, 56, 69, 112, 108, 253, 69, 171, 231, 131, 99, 133, 97, 93, 166, 59, 12, 241, 207, 5, 238, 77, 7, 5, 188, 236, 190, 240, 15, 86, 124, 219, 98, 152, 45, 209, 93, 169, 66, 89, 116, 113, 131, 104, 129, 187, 37, 68, 216, 82, 225, 139, 82, 120, 229, 67, 199, 10, 94, 240, 2, 22, 218, 205, 161, 112, 155, 226, 165, 71, 105, 53, 254, 148, 44, 84, 247, 136, 176, 235, 81, 23, 166, 114, 95, 220, 172, 169, 145, 50, 134, 144, 61, 190, 37, 66, 215, 205, 130, 149, 35, 94, 104, 154, 8, 44, 150, 85, 255, 141, 105, 1, 252, 28, 128, 125, 104, 85, 44, 106, 246, 165, 215, 180, 153, 219, 132, 28, 21, 225, 202, 83, 122, 53, 138, 211, 208, 87, 112, 241, 200, 223, 130, 195, 165, 178, 234, 14, 20, 38, 35, 213, 87, 108, 89, 147, 198, 188, 192, 109, 236, 146, 57, 109, 150, 149, 75, 247, 100, 67, 171, 26, 212, 221, 125, 117, 2, 131, 7, 134, 14, 225, 101, 41, 199, 142, 36, 181, 127, 180, 10, 129, 234, 142, 66, 16, 18, 220, 77, 53, 32, 188, 124, 1, 73, 227, 142, 2, 186, 42, 85, 173, 35, 252, 247, 143, 196, 246, 209, 120, 245, 223, 108, 188, 24, 16, 138, 27, 39, 83, 105, 155, 158, 6, 29, 203, 165, 89, 240, 154, 199, 199, 151, 250, 65, 63, 249, 36, 127, 42, 66, 199, 57, 126, 138, 152, 249, 173, 203, 108, 129, 220, 242, 15, 69, 119, 248, 26, 147, 234, 231, 116, 223, 60, 93, 30, 197, 97, 201, 153, 161, 131, 236, 36, 191, 222, 237, 51, 23, 53, 176, 10, 13, 238, 231, 52, 44, 6, 241, 130, 119, 7, 255, 86, 184, 179, 201, 157, 32, 34, 50, 169, 204, 16, 8, 102, 178, 153, 106, 198, 191, 3, 162, 153, 101, 235, 198, 245, 193, 87, 180, 34, 70, 117, 162, 201, 109, 106, 149, 167, 227, 128, 203, 225, 36, 103, 197, 235, 212, 94, 242, 106, 209, 80, 4, 175, 229, 182, 71, 95, 18, 8, 141, 210, 171, 135, 111, 40, 199, 97, 102, 185, 234, 91, 60, 232, 215, 218, 66, 169, 37, 200, 255, 5 };
#if !CDE_NET35 && !CDE_NET4
        /// <summary>
        /// This function reads (deserializes) licenses from existing license files in a directory for each skuid.
        /// </summary>
        /// <param name="licenseDirectory"></param>
        /// <param name="publicKeyCSPBlobs"></param>
        /// <param name="licenses"></param>
        /// <returns></returns>
        public static bool ReadLicensesForActivationKey(string licenseDirectory, List<byte[]> publicKeyCSPBlobs, out List<TheLicense> licenses)
        {
            licenses = new List<TheLicense>();
            try
            {
                var licenseFileContainer = Directory.EnumerateFiles(licenseDirectory, "*.cdex"); // TODO Review license file deployment mechanism and naming
                foreach (var containerFile in licenseFileContainer)
                {
                    using (var zipFileStream = new FileStream(containerFile, FileMode.Open, FileAccess.Read))
                    {
                        using (var zipArchive = new ZipArchive(zipFileStream, ZipArchiveMode.Read))
                        {
                            foreach (var licenseFile in zipArchive.Entries)
                            {
                                if (licenseFile.Name.EndsWith(".cdel"))
                                {
                                    using (var fileStream = licenseFile.Open())
                                    {
                                        var licenseBytes = new byte[fileStream.Length];
                                        if (fileStream.Read(licenseBytes, 0, licenseBytes.Length) == licenseBytes.Length)
                                        {
                                            var licenseJson = Encoding.UTF8.GetString(licenseBytes);
                                            var license = TheCommonUtils.DeserializeJSONStringToObject<TheLicense>(licenseJson);
                                            if (!license.ValidateSignature(publicKeyCSPBlobs))
                                            {
                                                //Console.WriteLine("Invalid or unrecognized signature on license {0}", licenseFile);
                                                return false;
                                            }
                                            licenses.Add(license);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                var licenseFiles = Directory.EnumerateFiles(licenseDirectory, "*.cdel"); // TODO Review license file deployment mechanism and naming
                foreach (var licenseFile in licenseFiles)
                {
                    var licenseJson = File.ReadAllText(licenseFile);
                    var license = TheCommonUtils.DeserializeJSONStringToObject<TheLicense>(licenseJson);
                    if (!license.ValidateSignature(publicKeyCSPBlobs))
                    {
                        //Console.WriteLine("Invalid or unrecognized signature on license {0}", licenseFile);
                        return false;
                    }
                    licenses.Add(license);

                }
            }
            catch (Exception)
            {
                //Console.WriteLine("Failed to extract license:" + ee.ToString());
                return false;
            }

            return true;
        }
#endif
        public static TheLicense ParseLicense(string licenseText, List<byte[]> publicKeyCSPBlobs)
        {
            var license = TheCommonUtils.DeserializeJSONStringToObject<TheLicense>(licenseText);
            if (!license.ValidateSignature(publicKeyCSPBlobs))
            {
                //Console.WriteLine("Invalid or unrecognized signature on license {0}", licenseFile);
                return null;
            }
            return license;
        }

        /// <summary>
        /// This function generates a list of LicenseParameters for all the licenses, with all values set to 0.
        /// </summary>
        /// <param name="licenses"></param>
        /// <param name="activationKeyParameters"></param>
        /// <param name="licenseForParameters"></param>
        /// <returns></returns>
        public static bool GetParametersForActivationKey(IEnumerable<TheLicense> licenses, out List<TheLicenseParameter> activationKeyParameters, out List<TheLicense> licenseForParameters)
        {
            licenseForParameters = new List<TheLicense>();
            activationKeyParameters = new List<TheLicenseParameter>();
            var sortedLicenses = licenses.OrderBy((l) => l.LicenseId.ToString());
            foreach (var license in sortedLicenses)
            {
                foreach (var param in license.Parameters)
                {
                    licenseForParameters.Add(license);
                    var newParam = new TheLicenseParameter { Name = param.Name, Value = 0 };
                    activationKeyParameters.Add(newParam);
                }
            }
            return true;
        }

        const int MaxLicensesInActivationKey = 255;

        /// <summary>
        /// Generated an activation key for the target deviceid.
        /// </summary>
        /// <param name="deviceId">Identifier of the cdeEngine node that is to be activated with the activation key.</param>
        /// <param name="signingKey">Symmetric key (arbitrary string) used to sign the activation key. The same key must be provided on the runtime node.</param>
        /// <param name="Expiration">Expiration date of the activation key. Licenses and entitlements activated using this key will cease to be valid after this date.</param>
        /// <param name="licenses">Note: values in the License.LicenseParameters property will be included in the activation key and added to the values in the license at activation time.
        /// This means that often you will want to set these values to 0 if the license already contains non-zero default paremeters.</param>
        /// <param name="activationKeyParameters">Additional parameters (typically entitlements for thing instances) to be unlocked with this activation key.</param>
        /// <param name="flags">Flags requesting additional activation key behavior.</param>
        /// <returns></returns>
        public static string GenerateActivationKey(Guid deviceId, string signingKey, DateTime Expiration, TheLicense[] licenses, List<TheLicenseParameter> activationKeyParameters, ActivationFlags flags)
        {
            if (licenses.Length > MaxLicensesInActivationKey || licenses.Length == 0)
            {
                return null;
            }
            var sortedLicenses = licenses.OrderBy((l) => l.LicenseId.ToString());

            byte[] licenseSignature;
            byte[] licenseParams = GenerateLicenseParameters(sortedLicenses);
            if (licenseParams == null || licenseParams.Length > TheActivationUtils.MaxLicenseParameters)
            {
                return null;
            }
            int index = 0;
            foreach (var param in activationKeyParameters)
            {
                licenseParams[index] = param.Value;
                index++;
            }


            double expirationTemp = Math.Ceiling((Expiration.ToUniversalTime() - new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays);
            if (expirationTemp < 0 || expirationTemp > 0xFFFF)
            {
                return null;
            }
            uint expirationInDays = (uint)expirationTemp;

            if (!TheActivationUtils.GenerateLicenseSignature(deviceId, signingKey, expirationInDays, sortedLicenses.ToArray(), licenseParams, flags, out licenseSignature))
            {
                return null;
            }
            byte[] activationKey = new byte[23];
            // 0-7: HMACSHA1 (appId, licenseAuthorization) -> truncate to 8 bytes (64 bits)
            licenseSignature.Take(8).ToArray().CopyTo(activationKey, 0);
            // 8-9: Expiration: Days since January 2016. Resulting Range: 2016 - 2195
            activationKey[8] = (byte)(expirationInDays & 0xFF);
            activationKey[9] = (byte)((expirationInDays >> 8) & 0xFF);
            // 10: Flags
            activationKey[10] = (byte)flags;
            // 11: Reserved (0)
            activationKey[11] = 0;
            // 12: License count
            activationKey[12] = (byte)sortedLicenses.Count();
            // 13-21: License parameters (
            licenseParams.CopyTo(activationKey, 13);
            // 22: Padding
            activationKey[22] = 15; // Ensure we always get 6x6 characters in base32

            var key = TheActivationUtils.Base32Encode(activationKey);
            //var testKey = TheActivationUtils.Base32Decode(key.Replace("-",""));
            return key;
        }

        static byte[] GenerateLicenseParameters(IEnumerable<TheLicense> licenses)
        {
            var licenseParams = new byte[TheActivationUtils.MaxLicenseParameters];
            int paramOffset = 0;
            foreach (var license in licenses)
            {
                if (paramOffset + license.Parameters.Length > TheActivationUtils.MaxLicenseParameters)
                {
                    return null;
                }
                foreach (var parameter in license.Parameters)
                {
                    licenseParams[paramOffset] = parameter.Value;
                    paramOffset++;
                }
            }
            while (paramOffset < licenseParams.Length)
            {
                licenseParams[paramOffset] = 123;
                paramOffset++;
            }
            return licenseParams;
        }
    }
}
