// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.Activation;
using System;
#pragma warning disable 1591

namespace nsCDEngine.ActivationKey
{
    public class TheActivationRequestKeyManager
    {
        /// <summary>
        /// Retrieves information from a token generated using the GetActivationRequestKey method.
        /// Typically used by a cdeEngine applicationId owner to generate activation keys.
        /// </summary>
        /// <param name="activationRequestKey">The token obtained from GetActivationRequestKey.</param>
        /// <param name="creationTime">The time when the token was created (local machine time, truncated to 10 minute boundary)</param>
        /// <param name="skuId">Application owner-defined SKU identifier, that is typically used to identify the set of licenses to included in the activation key.</param>
        /// <param name="deviceId">NodeId of the node where the activationRequestKey was generated. The activation key is typically bound to this node id.</param>
        /// <returns></returns>
        public static bool ParseActivationRequestKey(string activationRequestKey, out DateTime creationTime, out uint skuId, out Guid deviceId)
        {
            creationTime = DateTime.MinValue;
            skuId = 0;
            deviceId = Guid.Empty;

            var normalizedKey = activationRequestKey.Replace("-", "").ToUpper().Replace('O', '0').Replace('U', 'V').Replace('I', 'J').Replace('L', 'J');

            if (normalizedKey.Length != 36)
            {
                return false;
            }

            byte[] activationRequestKeyArray = TheActivationUtils.Base32Decode(normalizedKey);

            if (activationRequestKeyArray.Length != 23)
            {
                return false;
            }

            byte checksum = 0;
            int j = 0;

            byte[] tGu = new byte[16];

            uint tenMinuteIntervalsSinceUnixEpoch = 0;

            for (int i = 0; i < tGu.Length; i++)
            {
                if (i < 3)
                {
                    tenMinuteIntervalsSinceUnixEpoch += (uint)(activationRequestKeyArray[j] ^ checksum) << (i * 8);
                    checksum += activationRequestKeyArray[j];
                    j++;
                }
                else if (i < 5)
                {
                    skuId += (uint)(activationRequestKeyArray[j] ^ checksum) << ((i - 3) * 8);
                    checksum += activationRequestKeyArray[j];
                    j++;
                }
                tGu[i] = (byte)(activationRequestKeyArray[j] ^ checksum);
                checksum += activationRequestKeyArray[j];
                j++;
            }
            if (activationRequestKeyArray[j] != (checksum & 0xff) || activationRequestKeyArray[j + 1] != 15)
            {
                skuId = 0;
                return false;
            }
            creationTime = new DateTime(1970, 1, 1) + new TimeSpan(0, (int)(tenMinuteIntervalsSinceUnixEpoch * 10), 0);
            deviceId = new Guid(tGu);
            return true;
        }

        public static string GetActivationRequestKey(Guid deviceId, uint SkuId)
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

            byte checksum = 0;
            for (int i = 0; i < tGu.Length; i++)
            {
                if (i < 3)
                {
                    activationRequestKey[j] = (byte)((tenMinuteIntervalsSinceUnixEpoch & 0xff) ^ checksum);
                    tenMinuteIntervalsSinceUnixEpoch >>= 8;
                    checksum += activationRequestKey[j];
                    j++;
                }
                else if (i < 5)
                {
                    activationRequestKey[j] = (byte)((SkuId & 0xff) ^ checksum);
                    SkuId >>= 8;
                    checksum += activationRequestKey[j];
                    j++;
                }
                activationRequestKey[j] = (byte)(tGu[i] ^ checksum);
                checksum += activationRequestKey[j];
                j++;
            }
            activationRequestKey[j] = (byte)(checksum & 0xff);
            j++;

            activationRequestKey[j] = 15; // padding
            return TheActivationUtils.Base32Encode(activationRequestKey);
        }

    }
}
