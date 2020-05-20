// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;
using System.Security.Cryptography;

namespace nsCDEngine.Interfaces
{
    /// <summary>
    /// Interface ICDECrypto
    /// </summary>
    public interface ICDECrypto
    {
        /// <summary>
        /// Determines whether [has buffer correct length] [the specified org buffer].
        /// </summary>
        /// <param name="OrgBuffer">The org buffer.</param>
        /// <param name="pBufferType">Type of the buffer.</param>
        /// <returns><c>true</c> if [has buffer correct length] [the specified org buffer]; otherwise, <c>false</c>.</returns>
        bool HasBufferCorrectLength(string OrgBuffer, string pBufferType);
        //AES
        /// <summary>
        /// Encrypts the specified to encrypt.
        /// </summary>
        /// <param name="toEncrypt">To encrypt.</param>
        /// <param name="AK">The ak.</param>
        /// <param name="AI">The ai.</param>
        /// <returns>System.Byte[].</returns>
        byte[] Encrypt(byte[] toEncrypt, byte[] AK, byte[] AI);
        /// <summary>
        /// Decrypts the specified to decrypt.
        /// </summary>
        /// <param name="toDecrypt">To decrypt.</param>
        /// <param name="AK">The ak.</param>
        /// <param name="AI">The ai.</param>
        /// <returns>System.Byte[].</returns>
        byte[] Decrypt(byte[] toDecrypt, byte[] AK, byte[] AI);

        /// <summary>
        /// Decrypts to string.
        /// </summary>
        /// <param name="pInBuffer">The in buffer.</param>
        /// <param name="pType">Type of the buffer.</param>
        /// <returns>System.String.</returns>
        string DecryptToString(string pInBuffer, string pType);

        /// <summary>
        /// Encrypts to string.
        /// </summary>
        /// <param name="pInBuffer">The inbuffer.</param>
        /// <param name="pType">Type of the buffer.</param>
        /// <returns>System.String.</returns>
        string EncryptToString(string pInBuffer, string pType);
        //RSA
        /// <summary>
        /// RSAs the encrypt.
        /// </summary>
        /// <param name="toEncrypt">To encrypt string.</param>
        /// <param name="pRSAPublic">The RSA public key.</param>
        /// <returns>System.Byte[].</returns>
        byte[] RSAEncrypt(string toEncrypt, string pRSAPublic);
        /// <summary>
        /// RSAs the decrypt.
        /// </summary>
        /// <param name="toDecrypt">To decrypt.</param>
        /// <param name="rsa">The RSA.</param>
        /// <param name="RSAKey">The RSA key.</param>
        /// <param name="RSAPublic">The RSA public.</param>
        /// <returns>System.String.</returns>
        string RSADecrypt(byte[] toDecrypt, RSACryptoServiceProvider rsa, string RSAKey = null, string RSAPublic = null);
        /// <summary>
        /// RSAs the decrypt with private key.
        /// </summary>
        /// <param name="toDecrypt">To decrypt.</param>
        /// <param name="pPrivateKey">The private key.</param>
        /// <returns>System.String.</returns>
        string RSADecryptWithPrivateKey(byte[] toDecrypt, string pPrivateKey);
        /// <summary>
        /// Creates the RSA keys.
        /// </summary>
        /// <param name="RSAKey">The RSA key.</param>
        /// <param name="RSAPublic">The RSA public.</param>
        /// <param name="createXMLKeyAlways">if set to <c>true</c> [create XML key always].</param>
        /// <returns>RSACryptoServiceProvider.</returns>
        RSACryptoServiceProvider CreateRSAKeys(out string RSAKey, out string RSAPublic, bool createXMLKeyAlways = false);
        /// <summary>
        /// Gets the RSA key as XML.
        /// </summary>
        /// <param name="rsa">The RSA.</param>
        /// <param name="includePrivateParameters">if set to <c>true</c> [include private parameters].</param>
        /// <returns>System.String.</returns>
        string GetRSAKeyAsXML(RSACryptoServiceProvider rsa, bool includePrivateParameters);

        //KV (AES)
        /// <summary>
        /// Decrypts the kv.
        /// </summary>
        /// <param name="pInbuffer">The inbuffer.</param>
        /// <returns>Dictionary&lt;System.String, System.String&gt;.</returns>
        Dictionary<string, string> DecryptKV(byte[] pInbuffer);
        /// <summary>
        /// Encrypts the kv.
        /// </summary>
        /// <param name="pVal">The value.</param>
        /// <returns>System.Byte[].</returns>
        byte[] EncryptKV(Dictionary<string, string> pVal);
    }
}
