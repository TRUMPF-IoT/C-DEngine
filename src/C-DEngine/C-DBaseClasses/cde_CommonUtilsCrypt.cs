// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

namespace nsCDEngine.BaseClasses
{
    public static partial class TheCommonUtils
    {
        #region RSA Encryption
        /// <summary>
        /// Returns a decrypted string agains the RSA Key stored in the session
        /// </summary>
        /// <param name="pSessionID">SEID of the session the RSA Key is used</param>
        /// <param name="val">Envrypted string to be decrypted</param>
        /// <returns></returns>
        public static string cdeRSADecrypt(Guid pSessionID, string val)
        {
            TheSessionState tSession = TheBaseAssets.MySession?.ValidateSEID(pSessionID);    //Low Frequency
            if (tSession == null)
                return "";
            try
            {
                return TheBaseAssets.MyCrypto.RSADecrypt(ToHexByte(val), tSession.MyRSA, tSession.RSAKey, tSession.RSAPublic);
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG?.WriteToLog(new TSM("TheCommonUtils", "Error During cdeRSADecrypt", eMsgLevel.l1_Error) { PLS = e.ToString() }, 5015);
            }
            return "";
        }
        /// <summary>
        /// Returns a decrypted string agains the RSA Key stored in the session
        /// </summary>
        /// <param name="pSessionID">SEID of the session the RSA Key is used</param>
        /// <param name="val">Envrypted byte array to be decrypted</param>
        /// <returns></returns>
        public static string cdeRSADecrypt(Guid pSessionID, byte[] val)
        {
            TheSessionState tSession = TheBaseAssets.MySession?.ValidateSEID(pSessionID);    //Low Frequency
            if (tSession == null)
                return "";
            try
            {
                return TheBaseAssets.MyCrypto.RSADecrypt(val, tSession.MyRSA, tSession.RSAKey, tSession.RSAPublic);
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG?.WriteToLog(new TSM("TheCommonUtils", "Error During cdeRSADecrypt", eMsgLevel.l1_Error) { PLS = e.ToString() }, 5015);
            }
            return "";
        }

        /// <summary>
        /// Encrypts a string against the RSA Key inside a session
        /// </summary>
        /// <param name="pSessionID">SEID of the session the RSA Key is used</param>
        /// <param name="val">string value to be encrypted</param>
        /// <returns></returns>
        public static byte[] cdeRSAEncrypt(Guid pSessionID, string val)
        {
            TheSessionState pSession = TheBaseAssets.MySession?.ValidateSEID(pSessionID);    //Low Frequency
            if (pSession == null)
                return null;
            if (string.IsNullOrEmpty(pSession.RSAKey))
                CreateRSAKeys(pSession);
            if (pSession == null || (string.IsNullOrEmpty(pSession.RSAPublic) && string.IsNullOrEmpty(pSession.RSAPublicSend))) return null;
            try
            {
                return TheBaseAssets.MyCrypto.RSAEncrypt(val, string.IsNullOrEmpty(pSession.RSAPublicSend) ? pSession.RSAPublic : pSession.RSAPublicSend);
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG?.WriteToLog(new TSM("TheCommonUtils", "Error During cdeRSAEncrypt", eMsgLevel.l1_Error) { PLS = e.ToString() }, 5016);
            }
            return null;
        }
        /// <summary>
        /// Creates new RSA Keys
        /// </summary>
        /// <param name="PrivateKey"></param>
        /// <returns></returns>
        public static string cdeRSACreateKeys(out string PrivateKey)
        {
            TheSessionState tSess = new ();
            CreateRSAKeys(tSess, true);
            PrivateKey = tSess.RSAKey;
            return tSess.RSAPublic;
        }

        /// <summary>
        /// decrypts byte array with given string key
        /// </summary>
        /// <param name="pPrivateKey">RSA Key used for decryption</param>
        /// <param name="val">Buffer value to be decrypted</param>
        /// <returns></returns>
        public static string cdeRSADecryptWithKey(string pPrivateKey, byte[] val)
        {
            if (string.IsNullOrEmpty(pPrivateKey)) return "";
            try
            {
                return TheBaseAssets.MyCrypto.RSADecryptWithPrivateKey(val, pPrivateKey);
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG?.WriteToLog(new TSM("TheCommonUtils", "Error During RSADecryptWithKey", eMsgLevel.l1_Error) { PLS = e.ToString() }, 5012);
            }
            return "";
        }

        /// <summary>
        /// Encrypt a string to a byte array using a given RSA Key
        /// </summary>
        /// <param name="rsaPublic">Public RSA Key used for encryption</param>
        /// <param name="val">String value to be encrypted</param>
        /// <returns></returns>
        public static byte[] cdeRSAEncryptWithKeys(string rsaPublic, string val)
        {
            if (string.IsNullOrEmpty(rsaPublic)) return null;
            try
            {
                return TheBaseAssets.MyCrypto.RSAEncrypt(val, rsaPublic);
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG?.WriteToLog(new TSM("TheCommonUtils", "Error During cdeRSAEncrypt", eMsgLevel.l1_Error) { PLS = e.ToString() }, 5013);
            }
            return null;
        }

        internal static void CreateRSAKeys(TheSessionState pSession, bool createXMLKeyAlways = false)
        {
            if (TheBaseAssets.MyServiceHostInfo.DisableRSAToBrowser || pSession == null)
                return; //RSA Not working
            lock (pSession)
            {
                if (string.IsNullOrEmpty(pSession.RSAKey) && pSession.MyRSA == null)
                {
                    TheBaseAssets.MySYSLOG?.WriteToLog(TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("SSM", $"CreateRSAKey at {GetDateTimeString(DateTimeOffset.Now)}", eMsgLevel.l6_Debug), 5010, true);
                    pSession.MyRSA = TheBaseAssets.MyCrypto.CreateRSAKeys(out var tRSAKey, out var tRSAPublic, createXMLKeyAlways);
                    pSession.RSAKey = tRSAKey;
                    pSession.RSAPublic = tRSAPublic;
                    TheBaseAssets.MySYSLOG?.WriteToLog(TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("SSM", $"New Session {pSession.cdeMID} established and RSAKey created at {GetDateTimeString(DateTimeOffset.Now, 0)}", eMsgLevel.l6_Debug), 5011, true);
                }
            }
        }
        #endregion

        #region Encrypt
        /********************************************************************************************
         * Encryption Algorytms.
         * ******************************************************************************************/
        /// <summary>
        /// Encrypts a buffer into a base64 string using a provided and internal key
        /// </summary>
        /// <param name="OrgBuffer">buffer to encrypt.</param>
        /// <param name="AI">provided key</param>
        /// <returns>base64 encoded, encrypted string</returns>
        public static string cdeEncrypt(byte [] OrgBuffer, byte[] AI)
        {
            string SIDPost = Convert.ToBase64String(TheBaseAssets.MyCrypto.Encrypt(OrgBuffer, TheBaseAssets.MySecrets.GetAK(), AI));
            SIDPost = SIDPost.Replace("/", "_");
            SIDPost = SIDPost.Replace("+", "-");
            return SIDPost;
        }

        /// <summary>
        /// Encrypts a string into a base64 string using an internal key
        /// </summary>
        /// <param name="OrgBuffer">string to encrypt.</param>
        /// <param name="AI">provided key</param>
        /// <returns>base64 encoded, encrypted string</returns>
        public static string cdeEncrypt(string OrgBuffer, byte[] AI)
        {
            if (string.IsNullOrEmpty(OrgBuffer)) return null;
            return cdeEncrypt(CUnicodeString2Array(OrgBuffer), AI);
        }
        #endregion

        #region decrypt
        /********************************************************************************************
         * Decryption Algorytms.
         * ******************************************************************************************/
        internal static byte[] Decrypt2BA(byte[] encrypted, byte[] AI)
        {
            return TheBaseAssets.MyCrypto.Decrypt(encrypted, TheBaseAssets.MySecrets.GetAK(), AI);
        }

        internal static string Decrypt(byte[] encrypted, byte[] AI)
        {
            byte[] fromEncrypt = TheBaseAssets.MyCrypto.Decrypt(encrypted, TheBaseAssets.MySecrets.GetAK(), AI);
            return CArray2UnicodeString(fromEncrypt, 0, fromEncrypt.Length);
        }

        /// <summary>
        /// Decrypts a string 
        /// </summary>
        /// <param name="OrgBuffer">Encrypted String</param>
        /// <param name="AI">Salt</param>
        /// <param name="bSupressLog">if true, errors will not be logged</param>
        /// <returns></returns>
        public static string cdeDecrypt(string OrgBuffer, byte[] AI, bool bSupressLog = false)
        {
            if (string.IsNullOrEmpty(OrgBuffer)) return OrgBuffer;
            string DeCrypted = "";
            OrgBuffer = OrgBuffer.Replace("%3D", "=");
            OrgBuffer = OrgBuffer.Replace("-", "+");
            OrgBuffer = OrgBuffer.Replace("_", "/");
            byte[] EncBuffer;
            try
            {
                if ((OrgBuffer.Length % 2) != 0)
                    OrgBuffer += "====".Substring(0, 2 - (OrgBuffer.Length % 2));
                EncBuffer = Convert.FromBase64String(OrgBuffer);
                DeCrypted = Decrypt(EncBuffer, AI).Replace("\0", "").Trim();
            }
            catch (Exception e)
            {
                if (!bSupressLog)
                    TheBaseAssets.MySYSLOG?.WriteToLog(new TSM("TheCommonUtils", "Error During Decrypt", eMsgLevel.l1_Error) { PLS = e.ToString() }, 5014);
            }
            return DeCrypted;
        }

        /// <summary>
        /// Decrypts a string to a GUID
        /// </summary>
        /// <param name="OrgBuffer">Encrypted GUID</param>
        /// <param name="AI">Salt</param>
        /// <returns></returns>
        public static Guid CSCDecrypt2GUID(string OrgBuffer, byte[] AI)
        {
            if (!TheBaseAssets.MyCrypto.HasBufferCorrectLength(OrgBuffer,"GUID")) return Guid.Empty;
            OrgBuffer = OrgBuffer.Replace("%3D", "=");
            OrgBuffer = OrgBuffer.Replace("-", "+");
            OrgBuffer = OrgBuffer.Replace("_", "/");
            byte[] EncBuffer;
            byte[] DeCrypted;
            try
            {
                EncBuffer = Convert.FromBase64String(OrgBuffer);
                DeCrypted = Decrypt2BA(EncBuffer, AI);
            }
            catch
            {
                return Guid.Empty;
            }
            byte[] GuidArray = new byte[16];
            GuidArray.Initialize();
            for (int i = 0; i < 16 && i < DeCrypted.Length; i++)
                GuidArray[i] = DeCrypted[i];
            Guid tGUID = new Guid(GuidArray);
            return tGUID;
        }
        #endregion

        #region token and kv encryption
        /// <summary>
        /// Encrypts a string against a token GUID
        /// </summary>
        /// <param name="pToEncrypt">Text to be encrypted</param>
        /// <param name="pToken">Token to be used to encrypt</param>
        /// <returns></returns>
        public static string EncryptWithConnToken(string pToEncrypt, Guid pToken)
        {
            try
            {
                return cdeEncrypt(pToEncrypt, pToken.ToByteArray());
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG?.WriteToLog(TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCommonUtils", "Error During EncryptWithConnToken", eMsgLevel.l1_Error) { PLS = e.ToString() }, 5101);
            }
            return null;
        }

        /// <summary>
        /// Decrypts an encrypted string against a given token GUID
        /// </summary>
        /// <param name="pToDecrypt">Encrypted text</param>
        /// <param name="pToken">Guid token</param>
        /// <returns></returns>
        public static string DecryptWithConnToken(string pToDecrypt, Guid pToken)
        {
            try
            {
                return cdeDecrypt(pToDecrypt, pToken.ToByteArray());
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG?.WriteToLog(TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCommonUtils", "Error During EncryptWithConnToken", eMsgLevel.l1_Error) { PLS = e.ToString() }, 5102);
            }
            return null;
        }

        /// <summary>
        /// Decrypts a byte array to a string using internal AES encryption
        /// </summary>
        /// <param name="pInBuffer">Buffer to be decrypted</param>
        /// <param name="pType">Current only supported type: "AIV1"</param>
        /// <returns></returns>
        public static string DecryptToString(byte[] pInBuffer, string pType)
        {
            switch (pType)
            {
                case "AIV1":
                    byte[] fromEncrypt = TheBaseAssets.MyCrypto.Decrypt(pInBuffer, TheBaseAssets.MySecrets?.GetAK(), TheBaseAssets.MySecrets?.GetAI());
                    if (fromEncrypt == null)
                        return null;
                    return CArray2UnicodeString(fromEncrypt, 0, fromEncrypt.Length);
            }
            return null;
        }

        /// <summary>
        /// Decrypt incoming buffer array to a Dictionary.
        /// </summary>
        /// <param name="pInbuffer">incoming byte arrey to be decrypted</param>
        /// <returns></returns>
        public static Dictionary<string, string> DecryptKV(byte[] pInbuffer)
        {
            return TheBaseAssets.MyCrypto.DecryptKV(pInbuffer);
        }

        /// <summary>
        /// Encryptes a dictionary to a byte array
        /// </summary>
        /// <param name="pVal">Dictionary to be encrypted</param>
        /// <returns></returns>
        public static byte[] EncryptKV(Dictionary<string, string> pVal)
        {
            return TheBaseAssets.MyCrypto.EncryptKV(pVal);
        }

        #endregion
    }
}
