// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
#if !CDE_STANDARD // PKCS on .Net Standard requires additional nuget package: must allow running without dependency
using System.Security.Cryptography.Pkcs;
#endif

namespace nsCDEngine.Security
{
    /// <summary>
    /// Certificate manageing functions
    /// </summary>
    public class TheCertificates
    {
        /// <summary>
        /// Sets a new client Certificate. If it contains a scopeid and ApplyScope is true, the node will be scoped with that ScopeID
        /// </summary>
        /// <param name="pCertThumb">Thumbprint of the new client certificate</param>
        /// <param name="ApplyScope">if true, the scope in the certificate will be used to scope the node</param>
        /// <returns></returns>
        public static string SetClientCertificate(string pCertThumb, bool ApplyScope = false)
        {
            if (!string.IsNullOrEmpty(pCertThumb))
            {
                TheBaseAssets.MyServiceHostInfo.ClientCertificateThumb = pCertThumb;
                TheBaseAssets.MyServiceHostInfo.ClientCerts = GetClientCertificatesByThumbprint(pCertThumb);
                if (TheBaseAssets.MyServiceHostInfo.ClientCerts == null || TheBaseAssets.MyServiceHostInfo.ClientCerts?.Count == 0) //No valid client certifcate found
                {
                    TheBaseAssets.MyServiceHostInfo.ClientCertificateThumb = null;
                    TheBaseAssets.MyServiceHostInfo.ClientCerts = null;
                    TheBaseAssets.MySYSLOG.WriteToLog(4151, new TSM("TheBaseAssets", $"Client Certificate with Thumbprint {pCertThumb} required but not found! Connection to Cloud not possible", eMsgLevel.l1_Error));
                }
                else
                {
                    string error = "";
                    var tScopes = GetScopesFromCertificate(TheBaseAssets.MyServiceHostInfo.ClientCerts[0], ref error);
                    if (tScopes?.Count > 0)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommonUtils", $"Scope {tScopes[0]} found in Certificate {(ApplyScope ? "and applied" : "")}", eMsgLevel.l3_ImportantMessage));
                        if (ApplyScope)
                            TheBaseAssets.MyScopeManager.SetScopeIDFromEasyID(tScopes[0]);
                        return tScopes[0];
                    }
                    else
                        TheBaseAssets.MySYSLOG.WriteToLog(4151, new TSM("TheBaseAssets", $"Client Certificate with Thumbprint {pCertThumb} found and will be applied as soon as cloud is connected"));
                }
            }
            return null;
        }

        /// <summary>
        /// Sets a client Certificate on TheRequestData
        /// </summary>
        /// <param name="pData"></param>
        /// <param name="pThumbPrint"></param>
        /// <returns></returns>
        internal static bool SetClientCert(TheRequestData pData, string pThumbPrint)
        {
            if (!string.IsNullOrEmpty(pThumbPrint))
            {
                try
                {
                    X509Certificate2Collection certs = null;
                    if (pThumbPrint == TheBaseAssets.MyServiceHostInfo.ClientCertificateThumb)
                        certs = TheBaseAssets.MyServiceHostInfo.ClientCerts; //Use cached value if master cache
                    else
                        certs = GetClientCertificatesByThumbprint(pThumbPrint);  //load a custom cert for ISB Connect and other client cert scenarios
                    if (certs?.Count > 0)
                    {
                        pData.ClientCert = certs[0];
                        return true; //Client cert could be set to TheRequestData
                    }
                }
                catch (Exception certex)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommonUtils", "Error setting Client Certificate", eMsgLevel.l1_Error, certex.ToString()));
                }
                return false; //ClientThumb is set but could not be added to TheRequestData...let caller know
            }
            return true; //Not set..all is good
        }

        /// <summary>
        /// Returns a X509Certificate2Collection for the given thumbprint.
        /// The collection should contain exactly one certificate.
        /// If the call fails the function return null.
        /// If no certificate was found, the Collection contains a zero count
        /// </summary>
        /// <param name="pThumbPrint"></param>
        /// <returns></returns>
        internal static X509Certificate2Collection GetClientCertificatesByThumbprint(string pThumbPrint)
        {
            if (string.IsNullOrEmpty(pThumbPrint))
            {
                // Load .pfx given file name and password from config? Might be required on Linux to avoid unencrypted .pfx's in the dotNet Core User Cert store (current dotNet Core behavior).
                // Best practice with k8s seems to be to place .pfx and password into separate secrets and load in code
                return null;
            }

            X509Certificate2Collection certs;
            try
            {
                try
                {
                    X509Store store = new (StoreName.My, StoreLocation.LocalMachine);
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    // OK to accept invalid certs here, as the thumbprint prevents use of certs that may have been tampered with etc. This allows
                    // - Use of certs without a matching root cert / intermediate certs on the client
                    // - Use of expired certs (the server may still chose to reject)
                    certs = store.Certificates.Find(X509FindType.FindByThumbprint, pThumbPrint, false);
                }
                catch (System.Security.Cryptography.CryptographicException ex) when (ex.InnerException is PlatformNotSupportedException)
                {
                    certs = null;
                    #region Linux experiments
                    ////X509Store caStore = new X509Store(StoreName.CertificateAuthority, StoreLocation.CurrentUser);
                    ////caStore.Open(OpenFlags.ReadWrite);

                    ////var fileName4 = "/usr/local/share/ca-certificates/MyTestCARoot04.crt";
                    ////var newCert = new X509Certificate2(fileName4);
                    ////caStore.Add(newCert);
                    ////caStore.Close();

                    ////This populates the .Net Core cert store with a preinstalled PFX from the image (useful for local testing)
                    ////X509Store userStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                    ////userStore.Open(OpenFlags.ReadWrite);
                    ////var fileName5 = Path.Combine("/etc/ssl/certs/private", $"TestClient05_02.pfx");
                    ////var password5 = "supersecret";
                    ////var newCert2 = new X509Certificate2(fileName5, password5);
                    ////userStore.Add(newCert2);
                    ////userStore.Close();

                    ////X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                    ////X509Store store = new X509Store(StoreName.CertificateAuthority, StoreLocation.LocalMachine);
                    ////store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    ////var certs = store.Certificates.Find(X509FindType.FindByThumbprint, pThumbPrint, true);
                    ////var firstCert = certs?.Count > 0 ? certs[0] : null;
                    ////if (firstCert?.HasPrivateKey == false)
                    ////{
                    ////    var cert1 = certs[0];
                    ////    var cert = new X509Certificate2(cert1.RawData, "clabs1234", X509KeyStorageFlags.DefaultKeySet);
                    ////    var fileName2 = Path.Combine("/etc/ssl/certs/private", $"{cert1.Subject.Substring(3)}.pem");
                    ////    var password2 = "supersecret";
                    ////    var cert2 = new X509Certificate2(fileName2, password2, X509KeyStorageFlags.DefaultKeySet);
                    ////    var fileName3 = Path.Combine("/etc/ssl/certs/private", $"{cert1.Subject.Substring(3)}.pfx");
                    ////    var password3 = "supersecret";
                    ////    var cert3 = new X509Certificate2(fileName3, password3);
                    ////    //var cert4 = cert1.CopyWithPrivateKey(); // NetStandard 2.1 only (or NetCoreApp 2.x)
                    ////    certs = new X509Certificate2Collection(cert);
                    ////}
                    #endregion
                }
                if (certs == null || certs.Count == 0)
                {
                    // Look for cert in user store:
                    // - On Linux/dotNet Core (as of V2.2 and v3.0), only certs (*.pfx's) in the dotNet Core "user" store are loaded with private keys. For global certs, dotNet Core only loads the public key.
                    //   https://github.com/dotnet/corefx/blob/master/Documentation/architecture/cross-platform-cryptography.md
                    // - Useful on Windows for developer scenarios or advanced scenarios where a service runs with a specific user account
                    X509Store store = new (StoreName.My, StoreLocation.CurrentUser);
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    certs = store.Certificates.Find(X509FindType.FindByThumbprint, pThumbPrint, false);
                }
                TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheCommonUtils", $"Found {certs?.Count} certificate with Thumbprint {pThumbPrint} found", eMsgLevel.l4_Message));
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommonUtils", $"Error during retreival of Certificate with Thumbprint {pThumbPrint} not found", eMsgLevel.l1_Error, e.ToString()));
                certs = null;
            }
            if (certs != null)
            {
                foreach(var cert in certs)
                {
                    try
                    {
                        if (cert.PrivateKey == null)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommonUtils", $"Certificate with Thumbprint {cert.Thumbprint} has no PrivateKey", eMsgLevel.l1_Error));
                            continue;
                        }
#if !CDE_STANDARD
                        var rsa = cert.PrivateKey as RSACryptoServiceProvider;
                        if (rsa != null)
                        {
                            var signed = rsa.SignData(System.Text.Encoding.UTF8.GetBytes("Hello World"), SHA1.Create());
                        }
#else
                        if (cert.PrivateKey is RSA rsa)
                        {
                            rsa.SignData(System.Text.Encoding.UTF8.GetBytes("Hello World"), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
                        }

#endif
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommonUtils", $"Certificate with Thumbprint {cert.Thumbprint}: unable to verify if private key is usable", eMsgLevel.l1_Error));
                            continue;
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCommonUtils", $"Certificate with Thumbprint {cert.Thumbprint} has usable private key: (HasPrivateKey: {cert.HasPrivateKey})", eMsgLevel.l3_ImportantMessage));
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommonUtils", $"Certificate with Thumbprint {cert.Thumbprint} error using private key (HasPrivateKey: {cert.HasPrivateKey}) ", eMsgLevel.l1_Error, e.ToString()));
                    }
                }
            }
            return certs;
        }

        internal static List<string> GetScopesFromCertificate(X509Certificate2 x509cert, ref string error)
        {
            string extensions = "";
            List<string> scopeIds = new ();
            foreach (X509Extension extension in x509cert.Extensions)
            {
                // Create an AsnEncodedData object using the extensions information.
                AsnEncodedData asndata = new (extension.Oid, extension.RawData);
                extensions += $"{extension.Oid.FriendlyName} {asndata.Oid.Value} {asndata.RawData.Length} {asndata.Format(false)}";
                try
                {
                    if (extension.Oid.Value == "2.5.29.17") // SAN
                    {
                        string strSANEntry = asndata.Format(true);
                        var lines = strSANEntry.Split(new[] { "\r\n", "\n\r", "\n", "\r" }, StringSplitOptions.None);
                        foreach (var line in lines)
                        {
                            try
                            {
                                if (line.StartsWith("URL="))
                                {
                                    var scopeidUriStr = line.Substring("URL=".Length);
                                    var scopeidUri = TheCommonUtils.CUri(scopeidUriStr, true);
                                    if (scopeidUri.Scheme.ToLowerInvariant() == "com.c-labs.cdescope")
                                    {
                                        var scopeid = scopeidUri.Host;
                                        scopeIds.Add(scopeid);
                                    }
                                }
                                else if (line.StartsWith("URI:")) // check why .Net Core on Linux returns a different prefix (openssl?) - are there platform agnostic ways of doing this? Are there other platforms with different behavior?
                                {
                                    var scopeidUriStr = line.Substring("URI:".Length);
                                    var scopeidUri = TheCommonUtils.CUri(scopeidUriStr, true);
                                    if (scopeidUri.Scheme.ToLowerInvariant() == "com.c-labs.cdescope")
                                    {
                                        var scopeid = scopeidUri.Host;
                                        scopeIds.Add(scopeid);
                                    }

                                }
                            }
                            catch (Exception e)
                            {
                                error += e.ToString();
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    error += e.ToString();
                }
            }
            TheBaseAssets.MySYSLOG.WriteToLog(2351, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpService", $"{DateTimeOffset.Now}: {x509cert?.Subject} ScopeIds: {TheCommonUtils.CListToString(scopeIds, ",")} {error} [ Raw: {extensions} ] [Cert: {Convert.ToBase64String(x509cert.RawData)}]"));
            return scopeIds;
        }
    }

}
