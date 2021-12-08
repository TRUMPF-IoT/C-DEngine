// SPDX-FileCopyrightText: Copyright (c) 2017-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;

namespace nsCDEngine.Security
{
    /// <summary>
    /// Class TheCoreCrypto.
    /// Implements the <see cref="nsCDEngine.Interfaces.ICDECrypto" />
    /// </summary>
    /// <seealso cref="nsCDEngine.Interfaces.ICDECrypto" />
    internal class TheDefaultCrypto : ICDECrypto
    {
        private ICDESecrets MySecrets = null;
        private ICDESystemLog MySYSLOG = null;
        public TheDefaultCrypto(ICDESecrets pSecrets=null, ICDESystemLog pSysLog=null)
        {
            if (pSecrets != null)
                MySecrets = pSecrets;
            else
                MySecrets = TheBaseAssets.MySecrets;
            MySYSLOG = pSysLog;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Encrypts a buffer using AES with an AK and AI. </summary>
        ///
        /// <remarks>   Chris, 4/2/2020. </remarks>
        ///
        /// <param name="toEncrypt">    to encrypt. </param>
        /// <param name="AK">           The ak. </param>
        /// <param name="AI">           The ai. </param>
        ///
        /// <returns>   A byte[]. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public byte[] Encrypt(byte[] toEncrypt, byte[] AK, byte[] AI)
        {
            if (toEncrypt == null || AK == null || AI == null)
                return null;
            AesManaged myRijndael = new AesManaged();
            ICryptoTransform encryptor = myRijndael.CreateEncryptor(AK, AI);
            MemoryStream msEncrypt = new MemoryStream();
            CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            csEncrypt.Write(toEncrypt, 0, toEncrypt.Length);
            csEncrypt.FlushFinalBlock();
            return msEncrypt.ToArray();
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Decrypts a buffer using AES with AI and AK </summary>
        ///
        /// <remarks>   Chris, 4/2/2020. </remarks>
        ///
        /// <param name="toDecrypt">    to decrypt. </param>
        /// <param name="AK">           The ak. </param>
        /// <param name="AI">           The ai. </param>
        ///
        /// <returns>   A byte[]. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public byte[] Decrypt(byte[] toDecrypt, byte[] AK, byte[] AI)
        {
            if (toDecrypt == null || AK == null || AI == null)
                return null;
            AesManaged myRijndael = new AesManaged();
            ICryptoTransform decryptor = myRijndael.CreateDecryptor(AK, AI);
            byte[] resultArray = decryptor.TransformFinalBlock(toDecrypt, 0, toDecrypt.Length);
            myRijndael.Clear();
            return resultArray;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Query if 'OrgBuffer' has buffer correct length according to the buffer type. </summary>
        ///
        /// <remarks>   Chris, 4/2/2020. </remarks>
        ///
        /// <param name="OrgBuffer">    Buffer for organisation data. </param>
        /// <param name="pBufferType">  Type of the buffer. </param>
        ///
        /// <returns>   True if buffer correct length, false if not. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public bool HasBufferCorrectLength(string OrgBuffer, string pBufferType)
        {
            switch (pBufferType)
            {
                case "GUID":
                    return (OrgBuffer.Length == 46 || OrgBuffer.Length == 44);
            }
            return false;
        }

        /// <summary>
        /// decrypts byte array with given string key
        /// </summary>
        /// <param name="pPrivateKey">RSA Key used for decryption</param>
        /// <param name="val">Buffer value to be decrypted</param>
        /// <returns></returns>
        public string RSADecryptWithPrivateKey(byte[] val, string pPrivateKey)
        {
            if (string.IsNullOrEmpty(pPrivateKey)) return "";
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            try
            {
                rsa.FromXmlString(pPrivateKey);
            }
            catch (PlatformNotSupportedException)
            {
                // TODO parse XML
                throw;
            }
            byte[] tBytes = rsa.Decrypt(val, false);
            return Encoding.UTF8.GetString(tBytes, 0, tBytes.Length);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Rsa decrypt a buffer using a custom RSA provider or Private/Public key pair. </summary>
        ///
        /// <remarks>   Chris, 4/2/2020. </remarks>
        ///
        /// <param name="val">          Buffer value to be decrypted. </param>
        /// <param name="rsa">          (Optional) The rsa. </param>
        /// <param name="RSAKey">       (Optional) The rsa key. </param>
        /// <param name="RSAPublic">    (Optional) The rsa public. </param>
        ///
        /// <returns>   A string. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public string RSADecrypt(byte[] val, RSACryptoServiceProvider rsa = null, string RSAKey = null, string RSAPublic = null)
        {
            if (rsa == null)
            {
                if (string.IsNullOrEmpty(RSAKey))
                {
                    CreateRSAKeys(out RSAKey, out RSAPublic);
                    if (string.IsNullOrEmpty(RSAKey))
                        return "";
                }
                rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(RSAKey);
            }
#if (!CDE_STANDARD) // RSA Decrypt parameter different (padding enum vs. bool)
                byte[] tBytes = rsa.Decrypt(val, false);
#else
            byte[] tBytes = rsa.Decrypt(val, RSAEncryptionPadding.Pkcs1);
#endif
            return Encoding.UTF8.GetString(tBytes, 0, tBytes.Length);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Rsa encrypt a string with a public key </summary>
        ///
        /// <remarks>   Chris, 4/2/2020. </remarks>
        ///
        /// <param name="val">              String value to be encrypted. </param>
        /// <param name="pRSAPublic">       The rsa public key</param>
        ///
        /// <returns>   An encrypted byte[] buffer. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public byte[] RSAEncrypt(string val, string pRSAPublic)
        {
            if (string.IsNullOrEmpty(pRSAPublic) || string.IsNullOrEmpty(val)) return null;
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            string[] rsaP = pRSAPublic.Split(',');
            RSAParameters tP = new RSAParameters()
            {
                Modulus = ToHexByte(rsaP[1]),
                Exponent = ToHexByte(rsaP[0])
            };
            rsa.ImportParameters(tP);
            byte[] tBytes = rsa.Encrypt(CUTF8String2Array(val), false);
            return tBytes;
        }

        static bool _platformDoesNotSupportRSAXMLExportImport;

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Creates rsa keys and a RSA provider. </summary>
        ///
        /// <remarks>   Chris, 4/2/2020. </remarks>
        ///
        /// <param name="RSAKey">               [out] The rsa key. </param>
        /// <param name="RSAPublic">            [out] The rsa public. </param>
        /// <param name="createXMLKeyAlways">   (Optional) True to create XML key always. </param>
        ///
        /// <returns>   The new rsa keys. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public RSACryptoServiceProvider CreateRSAKeys(out string RSAKey, out string RSAPublic, bool createXMLKeyAlways = false)
        {
            RSACryptoServiceProvider rsa;
            RSAKey = null;
            if (Environment.OSVersion.Platform == PlatformID.Unix) // CODE REVIEW: Is this really the right check/condition? CM: Better would be to find out if we create this in SW or HW
                rsa = new RSACryptoServiceProvider(512);
            else
                rsa = new RSACryptoServiceProvider();
            if (!_platformDoesNotSupportRSAXMLExportImport)
            {
                try
                {
                    // TODO Decide if we want to switch to MyRSA object for the private key in general, or just for Std/Core
                    RSAKey = rsa.ToXmlString(true);
                }
                catch (PlatformNotSupportedException)
                {
                    _platformDoesNotSupportRSAXMLExportImport = true;
                }
            }
            if (_platformDoesNotSupportRSAXMLExportImport)
            {
                if (!createXMLKeyAlways)
                {
                    RSAKey = "no private key in string format on this platform";
                }
                else
                {
                    RSAKey = GetRSAKeyAsXML(rsa, true);
                }
            }
            RSAParameters param = rsa.ExportParameters(false);
            RSAPublic = ToHexString(param.Exponent) + "," + ToHexString(param.Modulus);
            return rsa;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Gets rsa keys as XML </summary>
        ///
        /// <remarks>   Chris, 4/2/2020. </remarks>
        ///
        /// <param name="rsa">                      The rsa. </param>
        /// <param name="includePrivateParameters"> True to include, false to exclude the private
        ///                                         parameters. </param>
        ///
        /// <returns>   The rsa key as XML. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public string GetRSAKeyAsXML(RSACryptoServiceProvider rsa, bool includePrivateParameters)
        {
            var paramsPrivate = rsa.ExportParameters(includePrivateParameters);

            var sb = new StringBuilder("<RSAKeyValue>");
            sb.Append(cdeCreateXMLElement(nameof(paramsPrivate.Modulus), paramsPrivate.Modulus));
            sb.Append(cdeCreateXMLElement(nameof(paramsPrivate.Exponent), paramsPrivate.Exponent));
            if (includePrivateParameters)
            {
                sb.Append(cdeCreateXMLElement(nameof(paramsPrivate.P), paramsPrivate.P));
                sb.Append(cdeCreateXMLElement(nameof(paramsPrivate.Q), paramsPrivate.Q));
                sb.Append(cdeCreateXMLElement(nameof(paramsPrivate.DP), paramsPrivate.DP));
                sb.Append(cdeCreateXMLElement(nameof(paramsPrivate.DQ), paramsPrivate.DQ));
                sb.Append(cdeCreateXMLElement(nameof(paramsPrivate.InverseQ), paramsPrivate.InverseQ));
                sb.Append(cdeCreateXMLElement(nameof(paramsPrivate.D), paramsPrivate.D));
            }
            sb.Append("</RSAKeyValue>");
            return sb.ToString();
        }

        /// <summary>
        /// Decrypt incoming buffer array to a Dictionary.
        /// </summary>
        /// <param name="pInbuffer">incoming byte arrey to be decrypted</param>
        /// <returns></returns>
        public Dictionary<string, string> DecryptKV(byte[] pInbuffer)
        {
            try
            {
                var fromEncrypt = Decrypt(pInbuffer, MySecrets?.GetAK(), MySecrets?.GetAI());
                string dec = CU.CArray2UnicodeString(fromEncrypt, 0, fromEncrypt.Length);
                dec = dec.TrimEnd('\0');
                var pos = dec.IndexOf('@');
                if (pos < 0)
                    return null;
                var tPref = dec.Substring(0, pos);
                var tP = tPref.Split(':');
                if (tP.Length < 2 || CU.CInt(tP[1]) != (dec.Substring(pos + 1)).Length)
                    return null;
                return CU.DeserializeJSONStringToObject<Dictionary<string, string>>(dec.Substring(pos + 1));
            }
            catch (Exception)
            {
                MySYSLOG?.WriteToLog(0,5015,"ICDECrypto", $"Error during KV decrypt...", eMsgLevel.l1_Error);
                return null;
            }
        }

        /// <summary>
        /// Encryptes a dictionary to a byte array
        /// </summary>
        /// <param name="pVal">Dictionary to be encrypted</param>
        /// <returns></returns>
        public byte[] EncryptKV(Dictionary<string, string> pVal)
        {
            try
            {
                string t = CU.SerializeObjectToJSONString(pVal);
                var tFinal = $"{CU.GetRandomUInt(0, 10000)}:{t.Length}@" + t;
                return Encrypt(CU.CUnicodeString2Array(tFinal), MySecrets?.GetAK(), MySecrets?.GetAI());
            }
            catch (Exception)
            {
                MySYSLOG?.WriteToLog(0, 5016, "ICDECrypto", $"Error during KV encrypt...", eMsgLevel.l1_Error);
                return null;
            }
        }

        /// <summary>
        /// Decrypts a byte array to a string using internal AES encryption
        /// </summary>
        /// <param name="OrgBuffer">Buffer to be decrypted</param>
        /// <param name="pType">Current only supported type: "AIV1"</param>
        /// <returns></returns>
        public string DecryptToString(string OrgBuffer, string pType)
        {
            switch (pType)
            {
                case "CDEP":
                    if (OrgBuffer.StartsWith("&^CDESP1^&:"))
                    {
                        return CU.cdeDecrypt(OrgBuffer.Substring(11), MySecrets?.GetNodeKey());
                    }
                    break;
            }
            return null;
        }

        /// <summary>
        /// Encrypts an string to an encrypted string.
        /// </summary>
        /// <param name="pInBuffer">The inbuffer.</param>
        /// <param name="pType">Type of the buffer.</param>
        /// <returns>System.String.</returns>
        public string EncryptToString(string pInBuffer, string pType)
        {
            switch (pType)
            {
                case "CDEP":
                    if (!pInBuffer.StartsWith("&^CDESP1^&:"))
                    {
                        return "&^CDESP1^&:" + CU.cdeEncrypt(pInBuffer, MySecrets?.GetNodeKey());
                    }
                    break;
            }
            return pInBuffer;
        }

        #region private static Helpers

        private static byte[] CUTF8String2Array(string strIn)
        {
            UTF8Encoding enc = new UTF8Encoding();
            return enc.GetBytes(strIn);
        }

        private static string cdeCreateXMLElement(string strInput, byte[] pTag)
        {
            // ToDo: Need parameter checking for strInput. But -- should we return null or ""??
            if (string.IsNullOrEmpty(strInput)) return null;

            return pTag == null ? string.Format("<{0} />\n", strInput) : string.Format("<{0}>{1}</{0}>", strInput, Convert.ToBase64String(pTag));
        }

        /// <summary>
        /// Convert a byte array into a hex string.
        /// </summary>
        /// <param name="byteValue">Input byte array.</param>
        /// <returns>A string.</returns>
        private static string ToHexString(byte[] byteValue)
        {
            if (byteValue == null || byteValue.Length == 0)
                return "";
            char[] lookup = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
            int i = 0, p = 0, l = byteValue.Length;
            char[] c = new char[l * 2];
            while (i < l)
            {
                byte d = byteValue[i++];
                c[p++] = lookup[d / 0x10];
                c[p++] = lookup[d % 0x10];
            }
            return new string(c, 0, c.Length);
        }

        /// <summary>
        /// Convert a hex string into a byte array.
        /// </summary>
        /// <param name="str">An input string.</param>
        /// <returns>A byte array.</returns>
        private static byte[] ToHexByte(string str)
        {
            if (string.IsNullOrEmpty(str))
                return null;
            // To Do -- Handle arrays with an odd number of bytes (which cause an exception).
            // Example: byte[] aOut = TheCommonUtils.ToHexByte("FAFAEEE");
            if (str.Length % 2 == 1) //Fix here
                str += "0";
            byte[] b = new byte[str.Length / 2];
            for (int y = 0, x = 0; x < str.Length; ++y, x++)
            {
                byte c1 = (byte)str[x];
                if (c1 > 0x60) c1 -= 0x57;
                else if (c1 > 0x40) c1 -= 0x37;
                else c1 -= 0x30;
                byte c2 = (byte)str[++x];
                if (c2 > 0x60) c2 -= 0x57;
                else if (c2 > 0x40) c2 -= 0x37;
                else c2 -= 0x30;
                b[y] = (byte)((c1 << 4) + c2);
            }
            return b;
        }
        #endregion
    }
}
