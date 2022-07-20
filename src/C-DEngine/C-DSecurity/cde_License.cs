// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;
#pragma warning disable 1591

namespace nsCDEngine.Activation
{
    /// <summary>
    /// Data structure describing an activated license
    /// </summary>
    public class TheActivatedLicense : TheDataBase
    {
        /// <summary>
        /// The hash of the activation key that was used to activate the license.
        /// </summary>
        public string ActivationKeyHash; // base64 encoded
        /// <summary>
        /// Expiration time of the activation key.
        /// </summary>
        public DateTimeOffset ActivationKeyExpiration;
        /// <summary>
        /// License that was activated using the activation key. Notice that a single license can be activated multiple times.
        /// </summary>
        public TheLicense License;
        /// <summary>
        /// License parameters as activated. Values are the sum of the values in the License and the values in the activation key
        /// </summary>
        public TheLicenseParameter[] ActivatedParameters;
        /// <summary>
        /// Indicates if the license activation is no longer considered valid by the system.
        /// </summary>
        public bool IsExpiredAndRemoved;

        public TheActivatedLicense() { }
        public TheActivatedLicense(TheActivatedLicense al)
        {
            ActivationKeyHash = al.ActivationKeyHash;
            ActivationKeyExpiration = al.ActivationKeyExpiration;
            License = new TheLicense(al.License);
            ActivatedParameters = new TheLicenseParameter[al.ActivatedParameters.Length];
            int index = 0;
            foreach (var parameter in al.ActivatedParameters)
            {
                ActivatedParameters[index] = new TheLicenseParameter { Name = parameter.Name, Value = parameter.Value };
                index++;
            }
            IsExpiredAndRemoved = al.IsExpiredAndRemoved;
        }
    }
    /// <summary>
    /// Name/Value pair for numeric values that can be specified in a license file and increased in an activation key
    /// </summary>
    public class TheLicenseParameter
    {
        /// <summary>
        /// Name of the parameter
        /// </summary>
        public string Name;
        /// <summary>
        /// Value of the parameter
        /// </summary>
        public byte Value;

        /// <summary>
        /// Name of the thing entitlements parameter that is handled by the cdeEngine and shared across all plug-ins
        /// </summary>
        public const string ThingEntitlements = "cdeThings";

        /// <summary>
        /// Clones this LicenseParameter to a new object
        /// </summary>
        /// <returns></returns>
        public TheLicenseParameter Clone()
        {
            return new TheLicenseParameter { Name = this.Name, Value = this.Value, };
        }
    }

    /// <summary>
    /// Definition of which plug-ins and device types within that plug-in a license applies to
    /// </summary>
    public class ThePluginLicense
    {
        /// <summary>
        /// ID if the plug-in to which the license applies
        /// </summary>
        public Guid PlugInId;
        /// <summary>
        /// Device Types to which the license applies. Empty array (or nyull): applies to all device types.
        /// </summary>
        public string[] DeviceTypes; // empty = all device types, non-empty: applies parameters to each device type

        /// <summary>
        /// Indicates if global thing entitlements can be used for this license/device type
        /// </summary>
        public bool AllowGlobalThingEntitlements;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ThePluginLicense()
        {
            DeviceTypes = new string[0];
        }
    }

    /// <summary>
    /// Name/Value pairs that can be specified in a license file and read by plug-ins to change behavior.
    /// </summary>
    public class TheLicenseProperty
    {
        /// <summary>
        /// Name of the property.
        /// </summary>
        public string Name;
        /// <summary>
        /// Value of the property.
        /// </summary>
        public string Value;
    }

    /// <summary>
    /// Describes a license. A license unlocks functionality in one or more plug-ins. A license must be activated using at least one activation key before it takes effect.
    /// Licenses can specify numeric parameters that can be increased in activation keys, and that can be read by the plug-in to change behavior (i.e. limit the number of connections).
    /// Licenses have an expiration date, after which they can no longer be used to unlock functionality.
    /// Licenses are signed by the issuer (typically just C-Labs). Plug-ins can require that licenses and activation keys be signed by additional authorities (specified by the plug-in in the IBaseEngine.SetIsLicensed call in their ICDEPlugib.InitEngineAssets implementation).
    /// </summary>
    public class TheLicense
    {
        /// <summary>
        /// Identified of the license.
        /// </summary>
        public Guid LicenseId;
        /// <summary>
        /// Human readable description of the license
        /// </summary>
        public string Description;
        /// <summary>
        /// Version of the license. Used to supercede old version of the license on a system. Only the highest version takes effect.
        /// </summary>
        public string LicenseVersion;
        /// <summary>
        /// Version of the C-DEngine for which this license is valid. If not specified, valid on any C-DEngine version, unless CDEVersionMin is specified.
        /// </summary>
        public string CDEVersion;
        /// <summary>
        /// Minimum Version of the C-DEngine for which this license is valid. If not specified or empty: valid on any C-DEngine version or only on the version specified by CDEVersion.
        /// </summary>
        public string CDEVersionMin;
        /// <summary>
        /// A 16 bit integer, used to identify this license in activation request keys. Typically a SkuId will map to exactly one license, but it is possible for a SKU to consist of multiple licenses.
        /// </summary>
        public string SkuId;
        /// <summary>
        /// The plug-ins to which the license applies.
        /// </summary>
        public ThePluginLicense[] PluginLicenses;
        /// <summary>
        /// Expiration date of this license.
        /// </summary>
        public DateTimeOffset Expiration;
        /// <summary>
        /// The public keys (base64 encoded) of additional signatures required on the license and the activation keys used to unlock the license.
        /// </summary>
        public string ActivationKeyValidator;
        /// <summary>
        /// Numeric parameters that can be used to limit functionality in a plug-in, and that can be increased in activation keys. An activation key can contain at most 9 parameters.
        /// </summary>
        public TheLicenseParameter[] Parameters;
        /// <summary>
        /// Name/Value pairs that can be read by a plug-in to modify it's behavior, i.e. only allow access to a specific URL or kind of URL.
        /// </summary>
        public TheLicenseProperty[] Properties;
        /// <summary>
        /// One or more signatures that are used validate the license and prevent tampering. Signatures are RSA SHA1 hashes, base 64 encoded.
        /// </summary>
        public string[] Signatures;

        /// <summary>
        /// License representation, to be used only for development/diagnostics
        /// </summary>
        /// <returns></returns>
        override public string ToString()
        {
            var parameterString = "";
            foreach (var parameter in Parameters)
            {
                parameterString += String.Format("{0}:{1} ", parameter.Name, parameter.Value);
            }
            var licenseString = String.Format("{0} : {1} [{2}]", Description, LicenseId, parameterString);

            return licenseString;
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public TheLicense()
        {
            PluginLicenses = new ThePluginLicense[0];
            Parameters = new TheLicenseParameter[0];
            Signatures = new string[0];
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="l"></param>
        public TheLicense(TheLicense l)
        {
            LicenseId = l.LicenseId;
            Description = l.Description;
            LicenseVersion = l.LicenseVersion;
            SkuId = l.SkuId;
            if (l.PluginLicenses != null)
            {
                PluginLicenses = new ThePluginLicense[l.PluginLicenses.Length];
                {
                    int licIndex = 0;

                    foreach (var pluginLicense in l.PluginLicenses)
                    {
                        PluginLicenses[licIndex] = new ThePluginLicense
                        {
                            PlugInId = pluginLicense.PlugInId,
                            DeviceTypes = pluginLicense.DeviceTypes.ToArray(),
                            AllowGlobalThingEntitlements = pluginLicense.AllowGlobalThingEntitlements,
                        };
                        licIndex++;
                    }
                }
            }
            Expiration = l.Expiration;
            ActivationKeyValidator = l.ActivationKeyValidator;

            if (l.Parameters != null)
            {
                Parameters = new TheLicenseParameter[l.Parameters.Length];

                int paramIndex = 0;
                foreach (var parameter in l.Parameters)
                {
                    Parameters[paramIndex] = new TheLicenseParameter { Name = parameter.Name, Value = parameter.Value };
                    paramIndex++;
                }

            }

            if (l.Properties != null)
            {
                Properties = new TheLicenseProperty[l.Properties.Length];
                int propertyIndex = 0;
                foreach (var property in l.Properties)
                {
                    Properties[propertyIndex] = new TheLicenseProperty { Name = property.Name, Value = property.Value };
                    propertyIndex++;
                }
            }

            if (l.Signatures != null)
            {
                Signatures = l.Signatures.ToArray();
            }
        }
        /// <summary>
        /// Validates the signatures of license using the public keys of the signer
        /// </summary>
        /// <param name="rsaPublicKeyCSPBlobs">The public key(s) for each of the signatures.</param>
        /// <returns></returns>
        public bool ValidateSignature(List<byte[]> rsaPublicKeyCSPBlobs)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048); //CM: new in 5.146.0 - will render old licenses unusable
            var payload = CU.CUTF8String2Array(GetCanonicalLicense());
            foreach (var key in rsaPublicKeyCSPBlobs)
            {
                rsa.ImportCspBlob(key);
                string keyAsXml;
                try
                {
                    keyAsXml = rsa.ToXmlString(false);
                }
                catch (PlatformNotSupportedException)
                {
                    keyAsXml = TheBaseAssets.MyCrypto.GetRSAKeyAsXML(rsa, false);
                }
                string header = Convert.ToBase64String(CU.CUTF8String2Array(CU.SerializeObjectToJSONString(new Header { jwk = keyAsXml, alg = "RS256" })));
                foreach (var signature in Signatures)
                {
                    var parts = signature.Split('.');
                    if (parts.Length == 3)
                    {
                        if (parts[0] == header)
                        {
                            if (rsa.VerifyData(payload, "SHA1", Convert.FromBase64String(parts[2])))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        //SECURITY-REVIEW: Is the return of this fuction security critcal?
        public static string GetAdditionalSigningKeyString(TheLicense[] licenses)
        {
            return licenses.Aggregate("", (s, l) =>
            {
                if (!String.IsNullOrEmpty(l.ActivationKeyValidator))
                {
                    string decryptedKeyValidator = CU.Decrypt(Encoding.UTF8.GetBytes(l.ActivationKeyValidator), TheBaseAssets.MySecrets.GetAI());
                    return s + decryptedKeyValidator;
                }
                return s;
            });
        }

        internal string GetCanonicalLicense()
        {
            TheLicense tempLicense = new TheLicense(this);
            // Bug in original License constructor did not copy the Description field, so it was not included in the signature. Only shipped license was this one (P08 Project, OPC Client)
            //CODE-REVIEW: Is this still necessary?
            if (tempLicense.LicenseId == new Guid("6a78a4cb-1d9b-4a53-a41b-c57497085026"))
            {
                tempLicense.Description = null;
            }
            tempLicense.Signatures = new string[0];
            return CU.SerializeObjectToJSONString(tempLicense);
        }

        class Header
        {
#pragma warning disable CS0649
            public string jwk=null; // Public Key of the signature
#pragma warning disable CS0649
            public string kid=null; // Key Identifier: must match the licenseAuthorities specified by the plugin
#pragma warning restore CS0649
            public string alg=null;
        }

        /// <summary>
        /// Adds a signature to the license.
        /// </summary>
        /// <param name="rsaPrivateKeyCSPBlob">Private key to use for generating the signature.</param>
        /// <returns></returns>
        public bool AddSignature(byte[] rsaPrivateKeyCSPBlob)
        {
            bool success = false;
            try
            {
                RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048); //CM: new in 5.146.0 - will render old licenses unusable
                rsa.ImportCspBlob(rsaPrivateKeyCSPBlob);
                if (rsa.PublicOnly)
                {
                    return false;
                }

                // JWS inspired, but
                // 1) jwk is RSA key in XML format
                // 2) Empty payload
                string header = Convert.ToBase64String(CU.CUTF8String2Array(CU.SerializeObjectToJSONString(new Header { jwk = rsa.ToXmlString(false), alg = "RS256" })));

                string payload = GetCanonicalLicense();
                byte[] payloadBytes = CU.CUTF8String2Array(payload);
                byte[] tBytes = rsa.SignData(payloadBytes, "SHA1");
                payload = ""; // Don't store the payload again as would be required by JWS

                string signature = Convert.ToBase64String(tBytes);

                var newSignatures = new string[Signatures.Length + 1];
                Signatures.CopyTo(newSignatures, 0);
                newSignatures[newSignatures.Length - 1] = String.Format("{0}.{1}.{2}", header, "", signature);
                Signatures = newSignatures;
                success = true;
            }
            catch (Exception)
            {
                //ignored?
            }
            return success;
        }

    }

}
