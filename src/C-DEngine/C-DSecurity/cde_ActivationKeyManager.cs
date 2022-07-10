// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Activation;
using nsCDEngine.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
#pragma warning disable 1591

namespace nsCDEngine.Activation
{
    public class TheActivationKeyManager
    {
        public static string CreateActivationRequestKey(Guid deviceId, uint SkuId)
        {
            if (deviceId == Guid.Empty)
            {
                return null;
            }
            byte[] tGu = deviceId.ToByteArray();
            uint tenMinuteIntervalsSinceUnixEpoch = (uint)((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMinutes) / 10;

            // Base 32 encoded, 22 Bytes:
            // 0,2,4 - Creation Time (10 Minute intervals since Unix Epoch, little endian)
            // 6,8 - SkuId (little endian)
            // 1,3,5,7,9-20 - Device Id (16 byte GUID)
            // 21 - Checksum
            // 22 - Padding (to ensure 6x6 chars in base32)
            byte[] activationRequestKey = new byte[3 + tGu.Length + 2 + 1 + 1];

            int j = 0;
            //activationRequestKey[0] = (byte) (DateTime.UtcNow.Ticks & 0xff);
            //j++;

            byte checksum = 0;// activationRequestKey[0];
            for (int i = 0; i < tGu.Length; i++)
            {
                if (i < 3)
                {
                    activationRequestKey[j] = (byte)((tenMinuteIntervalsSinceUnixEpoch & 0xff) ^ checksum);
                    tenMinuteIntervalsSinceUnixEpoch = tenMinuteIntervalsSinceUnixEpoch >>= 8;
                    checksum += activationRequestKey[j];
                    j++;
                }
                else if (i < 5)
                {
                    activationRequestKey[j] = (byte)((SkuId & 0xff) ^ checksum);
                    SkuId = SkuId >>= 8;
                    checksum += activationRequestKey[j];
                    j++;
                }
                activationRequestKey[j] = (byte)(tGu[i] ^ checksum);
                checksum += activationRequestKey[j];
                j++;
            }
            activationRequestKey[j] = (byte)(checksum & 0xff);
            j++;
            //activationRequestKey[j + 1] = (byte) ((checksum >> 8) + activationRequestKey[0]);
            //activationRequestKey[j + 2] =  (byte) (((activationRequestKey[0]) | 0x02) & 0x03); // ensure positive big int and 36 chars in output

            activationRequestKey[j] = 15; // padding
            return TheActivationUtils.Base32Encode(activationRequestKey);
        }


        const int MaxLicensesInActivationKey = 255;
        public static string[] ValidateActivationkey(ICDESecrets mySecrets, string activationKeyString, Guid deviceId, List<TheLicense> allLicenses, out List<TheLicenseActivationInformation> activatedLicenses, out DateTimeOffset expirationDate)
        {
            expirationDate = DateTimeOffset.MinValue;
            activatedLicenses = null;

            string signingKey = mySecrets.GetActivationKeySignatureKey();

            var normalizedKey = activationKeyString.Trim().Replace("-", "").ToUpper().Replace('O', '0').Replace('U', 'V').Replace('I', 'J').Replace('L', 'J');

            if (normalizedKey.Length != 36)
            {
                return new string[] { "Invalid activation key: not the proper length", String.Format("{0}", activationKeyString) };
            }

            byte[] activationKey = TheActivationUtils.Base32Decode(normalizedKey);
            if (activationKey == null || activationKey.Length != 23 || activationKey[22] != 15)
            {
                return new string[] { "Invalid activation key: failed to decode.", String.Format("{0}", activationKeyString) };
            }
            string activationKeyHash = Convert.ToBase64String((SHA1.Create().ComputeHash(activationKey))); //NOSONAR - not sensitive context

            byte[] signature = new byte[8];
            activationKey.Take(8).ToArray().CopyTo(signature, 0);

            int expirationInDays = (activationKey[8] + (activationKey[9] << 8));
            if (expirationInDays < 0)
            {
                return new string[] { "Invalid activation key: invalid expiration date.", String.Format("{0}. Expiration: {1}", activationKeyString, expirationInDays) };
            }
            expirationDate = new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc) + new TimeSpan(expirationInDays, 0, 0, 0);
            if (expirationDate < DateTime.Now)
            {
                return new string[] { "Invalid activation key: key expired.", String.Format("{0}. Expiration: {1}", activationKeyString, expirationDate.ToString()) };
            }

            ActivationFlags flags = (ActivationFlags)activationKey[10];
            if ((flags & ActivationFlags.RequireOnline) != 0)
            {
                return new string[] { "Invalid activation key: online activation required but not supported in this cdeEngine version.", String.Format("{0}", activationKeyString) };
            }
            byte licenseCount = activationKey[12];
            if (licenseCount > MaxLicensesInActivationKey)
            {
                return new string[] { "Invalid activation key: too many licenses specified.", String.Format("{0}. License Count: {1}", activationKeyString, licenseCount) };
            }
            if (licenseCount == 0)
            {
                return new string[] { "Invalid activation key: no licenses specified.", String.Format("{0}. License Count: {1}", activationKeyString, 0) };
            }
            if (licenseCount > allLicenses.Count)
            {
                return new string[] { "Unable to apply activation key: some licenses not available on the system.", String.Format("{0}. License Count in key: {1}. Total valid licenses on system: {2}", activationKeyString, licenseCount, allLicenses.Count) };
            }

            byte[] parameters = new byte[TheActivationUtils.MaxLicenseParameters];
            int paramOffset = 13;
            for (int j = 0; j < TheActivationUtils.MaxLicenseParameters; j++)
            {
                parameters[j] = activationKey[paramOffset];
                paramOffset++;
            }

            int[] candidateIndices = new int[licenseCount];
            TheLicense[] candidates = new TheLicense[licenseCount];
            bool done = false;
            do
            {
                bool validCombination = true;
                for (int i = 0; i < licenseCount; i++)
                {
                    candidates[i] = allLicenses[candidateIndices[i]];
                    if (i > 0 && String.CompareOrdinal(candidates[i].LicenseId.ToString(), candidates[i - 1].LicenseId.ToString()) <= 0)
                    {
                        validCombination = false;
                        break;
                    }
                }

                if (validCombination && TheActivationUtils.GenerateLicenseSignature(deviceId, signingKey, (uint)expirationInDays, candidates.ToArray(), parameters, flags, out byte[] candidateSignature))
                {
                    if (signature.SequenceEqual(candidateSignature.Take(8)))
                    {
                        activatedLicenses = new List<TheLicenseActivationInformation>();
                        int parameterOffset = 0;
                        for (int i = 0; i < licenseCount; i++)
                        {
                            var activatedParameters = new List<TheLicenseParameter>();
                            foreach (var parameter in candidates[i].Parameters)
                            {
                                activatedParameters.Add(new TheLicenseParameter
                                {
                                    Name = parameter.Name,
                                    Value = (byte)(parameter.Value + parameters[parameterOffset]),
                                });
                            }

                            activatedLicenses.Add(new TheLicenseActivationInformation
                            {
                                ActivationKeyHash = activationKeyHash,
                                ActivationKeyExpiration = expirationDate <= candidates[i].Expiration ? expirationDate : candidates[i].Expiration,
                                License = candidates[i],
                                ActivatedParameters = activatedParameters.ToArray(),
                            });
                        }
                        return null;
                    }
                }
                for (int i = 0; i < licenseCount; i++)
                {
                    candidateIndices[i]++;
                    if (candidateIndices[i] < allLicenses.Count)
                    {
                        break;
                    }
                    else
                    {
                        if (i == licenseCount - 1)
                        {
                            done = true;
                        }
                        candidateIndices[i] = 0;
                    }
                }
            } while (!done);
            return new string[] { "Unable to apply activation key: mismatched device id or some licenses not available on the system.", String.Format("{0}. Device Id: {1}. License Count in key: {2}. Total valid licenses on system: {3}", activationKeyString, deviceId, licenseCount, allLicenses.Count) };
        }

    }

}
