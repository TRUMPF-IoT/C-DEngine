// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using nsCDEngine.BaseClasses;
using System.Reflection;
#if !CDE_STANDARD // PKCS on .Net Standard requires additional nuget package: must allow running without dependency
using System.Security.Cryptography.Pkcs;
#endif

namespace nsCDEngine.Security
{
    #region helper classes
    internal struct WINTRUST_FILE_INFO : IDisposable
    {
        public WINTRUST_FILE_INFO(string fileName, Guid subject)
        {
            cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO));
            pcwszFilePath = fileName;

            if (subject != Guid.Empty)
            {
                pgKnownSubject = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Guid)));
                Marshal.StructureToPtr(subject, pgKnownSubject, true);
            }
            else
            {
                pgKnownSubject = IntPtr.Zero;
            }
            hFile = IntPtr.Zero;
        }

        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;

        #region IDisposable Members
        public void Dispose()
        {
            Dispose(true);
        }
        private void Dispose(bool _)
        {
            if (pgKnownSubject != IntPtr.Zero)
            {
                Marshal.DestroyStructure(this.pgKnownSubject, typeof(Guid));
                Marshal.FreeHGlobal(this.pgKnownSubject);
            }
        }
        #endregion
    }
    enum AllocMethod
    {
        HGlobal,
        CoTaskMem
    };
    enum UnionChoice
    {
        File = 1,
        Catalog,
        Blob,
        Signer,
        Cert
    };
    enum UiChoice
    {
        All = 1,
        NoUI,
        NoBad,
        NoGood
    };
    enum RevocationCheckFlags
    {
        None = 0,
        WholeChain
    };
    enum StateAction
    {
        Ignore = 0,
        Verify,
        Close,
        AutoCache,
        AutoCacheFlush
    };
    enum TrustProviderFlags
    {
        UseIE4Trust = 1,
        NoIE4Chain = 2,
        NoPolicyUsage = 4,
        RevocationCheckNone = 16,
        RevocationCheckEndCert = 32,
        RevocationCheckChain = 64,
        RecovationCheckChainExcludeRoot = 128,
        Safer = 256,
        HashOnly = 512,
        UseDefaultOSVerCheck = 1024,
        LifetimeSigning = 2048
    };
    enum UIContext
    {
        Execute = 0,
        Install
    };

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINTRUST_DATA : IDisposable
    {
        public WINTRUST_DATA(WINTRUST_FILE_INFO fileInfo)
        {
            this.cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA));
            pInfoStruct = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)));
            Marshal.StructureToPtr(fileInfo, pInfoStruct, false);
            this.dwUnionChoice = UnionChoice.File;
            pPolicyCallbackData = IntPtr.Zero;
            pSIPCallbackData = IntPtr.Zero;
            dwUIChoice = UiChoice.NoUI;
            fdwRevocationChecks = RevocationCheckFlags.None;
            dwStateAction = StateAction.Ignore;
            hWVTStateData = IntPtr.Zero;
            pwszURLReference = IntPtr.Zero;
            dwProvFlags = TrustProviderFlags.Safer;
            dwUIContext = UIContext.Execute;
        }

        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPCallbackData;
        public UiChoice dwUIChoice;
        public RevocationCheckFlags fdwRevocationChecks;
        public UnionChoice dwUnionChoice;
        public IntPtr pInfoStruct;
        public StateAction dwStateAction;
        public IntPtr hWVTStateData;
        private IntPtr pwszURLReference;
        public TrustProviderFlags dwProvFlags;
        public UIContext dwUIContext;

        #region IDisposable Members
        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool _)
        {
            if (dwUnionChoice == UnionChoice.File)
            {
                WINTRUST_FILE_INFO info = new WINTRUST_FILE_INFO();
                Marshal.PtrToStructure(pInfoStruct, info);
                info.Dispose();
                Marshal.DestroyStructure(pInfoStruct, typeof(WINTRUST_FILE_INFO));
            }
            Marshal.FreeHGlobal(pInfoStruct);
        }
        #endregion
    }
    internal sealed class UnmanagedPointer : IDisposable
    {
        private IntPtr m_ptr;
        private AllocMethod m_meth;
        internal UnmanagedPointer(IntPtr ptr, AllocMethod method)
        {
            m_meth = method;
            m_ptr = ptr;
        }
        ~UnmanagedPointer()
        {
            Dispose(false);
        }
        #region IDisposable Members
        private void Dispose(bool disposing)
        {
            if (m_ptr != IntPtr.Zero)
            {
                if (m_meth == AllocMethod.HGlobal)
                {
                    Marshal.FreeHGlobal(m_ptr);
                }
                else if (m_meth == AllocMethod.CoTaskMem)
                {
                    Marshal.FreeCoTaskMem(m_ptr);
                }
                m_ptr = IntPtr.Zero;
            }
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

        public static implicit operator IntPtr(UnmanagedPointer ptr)
        {
            return ptr.m_ptr;
        }
    }
    #endregion

    internal class TheDefaultCodeSigning : ICDECodeSigning
    {
        private static ICDESecrets MySecrets = null;
        public TheDefaultCodeSigning(ICDESecrets pSecrets, ICDESystemLog pSysLog = null)
        {
            MySecrets = pSecrets;
            MySYSLOG = pSysLog;
        }

        #region private helpers
        private static X509Certificate2 MyAppHostCert = null;
        private static string MyAppCertThumb = null;

        [DllImport("Wintrust.dll", PreserveSig = true, SetLastError = false)]
        private static extern uint WinVerifyTrust(IntPtr hWnd, IntPtr pgActionID, IntPtr pWinTrustData);
        private static uint WinVerifyTrust(string fileName)
        {
            Guid wintrust_action_generic_verify_v2 = new Guid("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");
            uint result = 0;
            using (WINTRUST_FILE_INFO fileInfo = new WINTRUST_FILE_INFO(fileName, Guid.Empty))
            using (UnmanagedPointer guidPtr = new UnmanagedPointer(Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Guid))), AllocMethod.HGlobal))
            using (UnmanagedPointer wvtDataPtr = new UnmanagedPointer(Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_DATA))), AllocMethod.HGlobal))
            {
                WINTRUST_DATA data = new WINTRUST_DATA(fileInfo);
                IntPtr pGuid = guidPtr;
                IntPtr pData = wvtDataPtr;
                Marshal.StructureToPtr(wintrust_action_generic_verify_v2, pGuid, true);
                Marshal.StructureToPtr(data, pData, true);
                result = WinVerifyTrust(new IntPtr(-1), pGuid, pData);
            }
            return result;

        }

        private static X509Certificate2 VerifyPEAndReadSignerCert(string filePath, bool verifyCertificateTrust, bool verifyIntegrity)
        {
            if (verifyCertificateTrust)
            {
                try
                {
                    bool trusted = (WinVerifyTrust(filePath) == 0);
                    if (!trusted)
                    {
                        return null;
                    }
                }
                catch (System.DllNotFoundException)
                {
                    // No WinVerifyTrust: assume we are on a non-Windows OS: verify PE and certificate integrity ourselves
                    return ReadPECertificateFromBinaryImage(filePath, true, true);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            // Trust was verified, or trust verification was not required

            X509Certificate2 cert;
            try
            {
                cert = new X509Certificate2(filePath);
            }
            catch (Exception) // Unable to read certificate using X509Certificate2 class: read ourselves if on Linux
            {
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    try
                    {
                        return ReadPECertificateFromBinaryImage(filePath, false, verifyIntegrity);
                    }
                    catch
                    {
                        return null;
                    }
                }
                return null;
            }
            if (!verifyCertificateTrust && verifyIntegrity)
            {
                try
                {
                    if (ReadPECertificateFromBinaryImage(filePath, false, verifyIntegrity) == null)
                    {
                        return null;
                    }
                }
                catch
                {
                    return null;
                }
            }
            return cert;
        }

        private static X509Certificate2 ReadPECertificateFromBinaryImage(string filePath, bool verifyCertificateTrust, bool verifyIntegrity)
        {
            X509Certificate2 firstSignerCert = null;
            // PE info from https://docs.microsoft.com/en-us/windows/win32/debug/pe-format
            using (var fileStream = File.OpenRead(filePath))
            {
                // Read the PESignature Offset
                UInt32 peSignatureOffset;
                {
                    if (fileStream.Seek(0x3c, SeekOrigin.Begin) != 0x3c)
                    {
                        return null;
                    }

                    if (!ReadUInt32LittleEndian(fileStream, out peSignatureOffset))
                    {
                        return null;
                    }
                    if (peSignatureOffset < 0x3c + 4)
                    {
                        return null;
                    }

                }
                // Read and verify the PESignature ("PE")
                {
                    if (fileStream.Seek(peSignatureOffset, SeekOrigin.Begin) != peSignatureOffset)
                    {
                        return null;
                    }

                    var peSignatureBytes = new byte[4];
                    if (fileStream.Read(peSignatureBytes, 0, peSignatureBytes.Length) != peSignatureBytes.Length)
                    {
                        return null;
                    }
                    if (!peSignatureBytes.SequenceEqual(new Byte[] { (byte)'P', (byte)'E', 0, 0 }))
                    {
                        return null;
                    }
                }
                UInt32 headerOffset = peSignatureOffset + 4;

                // Verify SizeOfOptionalHeader field
                {
                    UInt32 sizeofOptionalHeaderOffset = headerOffset + 16;
                    if (fileStream.Seek(sizeofOptionalHeaderOffset, SeekOrigin.Begin) != sizeofOptionalHeaderOffset)
                    {
                        return null;
                    }
                    if (!ReadUInt16LittleEndian(fileStream, out ushort sizeOfOptionalHeader))
                    {
                        return null;
                    }
                    if (sizeOfOptionalHeader < 136)
                    {
                        return null;
                    }
                }

                UInt32 optionalHeaderOffset = headerOffset + 20;

                // Determine PE vs. PE+
                bool isPEPlus;
                {
                    if (fileStream.Seek(optionalHeaderOffset, SeekOrigin.Begin) != optionalHeaderOffset)
                    {
                        return null;
                    }

                    var magicNumber = new byte[2];
                    if (fileStream.Read(magicNumber, 0, magicNumber.Length) != magicNumber.Length)
                    {
                        return null;
                    }
                    if (magicNumber.SequenceEqual(new Byte[] { 0xb, 1 }))
                    {
                        isPEPlus = false;
                    }
                    else if (magicNumber.SequenceEqual(new Byte[] { 0xb, 2 }))
                    {
                        isPEPlus = true;
                    }
                    else
                    {
                        return null;
                    }
                }
                UInt32 dataDirectoryOffset;
                if (isPEPlus)
                {
                    dataDirectoryOffset = optionalHeaderOffset + 112;
                }
                else
                {
                    dataDirectoryOffset = optionalHeaderOffset + 96;
                }


                // Read Certificate Table offset and size
                UInt32 certificateTableOffset;
                UInt32 certificateTableSize;
                UInt32 certificateTableEntryOffset = dataDirectoryOffset + 4 * 8; // Table entry 4, each entry is 8 bytes
                {
                    if (fileStream.Seek(certificateTableEntryOffset, SeekOrigin.Begin) != certificateTableEntryOffset)
                    {
                        return null;
                    }

                    if (!ReadUInt32LittleEndian(fileStream, out certificateTableOffset))
                    {
                        return null;
                    }
                    if (!ReadUInt32LittleEndian(fileStream, out certificateTableSize))
                    {
                        return null;
                    }
                }

                if (certificateTableSize == 0)
                {
                    // no certs/signatures
                    return null;
                }
                if (certificateTableSize > 10 * 1024 * 1024) // 10 MB limit for certs
                {
                    // protect against overly large (presumably malicious) certificate table size
                    return null;
                }
                if (certificateTableOffset < dataDirectoryOffset + 120)
                {
                    // sanity check that offset doesn't point before or into the data table
                    return null;
                }

                UInt32 currentCertificateTableOffset = certificateTableOffset;
                UInt32 remainingCertificateTableSize = certificateTableSize;

                while (remainingCertificateTableSize >= 8)
                {
                    if (fileStream.Seek(currentCertificateTableOffset, SeekOrigin.Begin) != currentCertificateTableOffset)
                    {
                        return null;
                    }

                    if (!ReadUInt32LittleEndian(fileStream, out var certEntryLength))
                    {
                        return null;
                    }
                    if (certEntryLength <= 8)
                    {
                        // Empty/incomplete cert entry
                        return null;
                    }
                    if (certEntryLength > remainingCertificateTableSize)
                    {
                        return null;
                    }

                    if (!ReadUInt16LittleEndian(fileStream, out var certRevision))
                    {
                        return null;
                    }
                    if (!ReadUInt16LittleEndian(fileStream, out var certType))
                    {
                        return null;
                    }

                    var certificateBytes = new byte[certEntryLength - 8];
                    if (fileStream.Read(certificateBytes, 0, certificateBytes.Length) != certificateBytes.Length)
                    {
                        return null;
                    }
                    if (verifyIntegrity)
                    {
#if !CDE_STANDARD
                        var signedCms = new SignedCms();
#else
                        // .Net Standard requires System.Security.Cryptography.Pkcs NuGet package for this functionality (also part of Microsoft.Windows.Compatibility): make this optional
                        dynamic signedCms = null;
                        System.Reflection.Assembly pkcsAssembly = null;
                        try
                        {
                            pkcsAssembly = System.Reflection.Assembly.Load("System.Security.Cryptography.Pkcs, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                        } // {System.Security.Cryptography.Pkcs, Version=4.0.3.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a}
                        catch (Exception e) when (e is FileLoadException || e is FileNotFoundException)
                        {
                            try
                            {
                                pkcsAssembly = System.Reflection.Assembly.Load("System.Security.Cryptography.Pkcs");
                            }
                            catch (Exception e2) when (e2 is FileLoadException || e2 is FileNotFoundException)
                            {
                                // No PKCS crypto installed: degrade to not verify integrity
                                DontVerifyIntegrity = true;
                                verifyIntegrity = false;
                            }
                        }
                        catch { }
                        if (pkcsAssembly != null)
                        {
                            var cmsType = pkcsAssembly.GetType("System.Security.Cryptography.Pkcs.SignedCms", false);
                            if (cmsType != null)
                            {
                                try
                                {
                                    signedCms = Activator.CreateInstance(cmsType);
                                }
                                catch { }
                            }
                        }
                        if (signedCms == null && verifyIntegrity)
                        {
                            return null;
                        }
#endif
                        if (verifyIntegrity)
                        {
                            try
                            {
                                signedCms.Decode(certificateBytes);
                            }
                            catch
                            {
                                return null;
                            }
                            try
                            {
                                signedCms.CheckSignature(!verifyCertificateTrust);
                            }
                            catch
                            {
                                return null;
                            }

                            if (signedCms.SignerInfos.Count < 1)
                            {
                                return null;
                            }
                            if (firstSignerCert == null)
                            {
                                firstSignerCert = signedCms.SignerInfos[0].Certificate;
                            }

                            // TODO Propertly parse the SpcIndirectDataContent structure per Authenticode Spec
                            // For SHA1 the hash is always in the last 20 bytes of the content info: if another algorithm is used we will currently reject the signature
                            var signedHash = new byte[20];
                            byte[] contentBytes = signedCms.ContentInfo.Content;
                            signedHash = contentBytes.Skip(contentBytes.Length - 20).ToArray();

                            // Compute the image hash, ignoring certain sections per Authenticode spec
                            var fileHash = ComputePEHash(fileStream, optionalHeaderOffset, headerOffset, dataDirectoryOffset, certificateTableSize, certificateTableEntryOffset);
                            //var monoHash = new Mono.Security.Authenticode.AuthenticodeBase().HashFile(filePath, "SHA1");

                            if (fileHash?.SequenceEqual(signedHash) != true)
                            {
                                // File hash doesn't match signature: file was tampered with!
                                return null;
                            }
                        }
                    }
                    if (!verifyIntegrity && firstSignerCert == null)
                    {
                        try
                        {
                            var certs = new X509Certificate2Collection();
                            certs.Import(certificateBytes);
                            firstSignerCert = certs[0];
                        }
                        catch
                        {
                            firstSignerCert = null;
                        }
                    }

                    remainingCertificateTableSize -= certEntryLength;
                    currentCertificateTableOffset += certEntryLength;
                }
                return firstSignerCert;
            }
        }

        class SectionInfo
        {
            public UInt32 Pointer;
            public UInt32 Size;
        }

        private static byte[] ComputePEHash(FileStream fileStream, UInt32 optionalHeaderOffset, UInt32 headerOffset, UInt32 dataDirectoryOffset, UInt32 certificateTableSize, UInt32 certificateTableEntryOffset)
        {
            // Get SizeOfHeaders
            fileStream.Seek(optionalHeaderOffset + 60, SeekOrigin.Begin);
            if (!ReadUInt32LittleEndian(fileStream, out var sizeOfHeaders)) return null;

            UInt32 sumOfBytesHashed = sizeOfHeaders;

            fileStream.Seek(headerOffset + 2, SeekOrigin.Begin);
            if (!ReadUInt16LittleEndian(fileStream, out var numberOfSections)) return null;

            UInt32 sectionTableOffset = dataDirectoryOffset + 16 * 8; // 16 data directory entries of 8 bytes each
            UInt32 sectionTableEnd = sectionTableOffset;
            fileStream.Seek(sectionTableOffset, SeekOrigin.Begin);
            var sectionInfos = new List<SectionInfo>();
            while (numberOfSections > 0)
            {
                fileStream.Seek(16, SeekOrigin.Current);
                if (!ReadUInt32LittleEndian(fileStream, out var sizeOfRawData)) return null;
                if (!ReadUInt32LittleEndian(fileStream, out var pointerToRawData)) return null;
                if (pointerToRawData > 0)
                {
                    sectionInfos.Add(new SectionInfo { Pointer = pointerToRawData, Size = sizeOfRawData });
                    sumOfBytesHashed += sizeOfRawData;
                }
                fileStream.Seek(16, SeekOrigin.Current);
                sectionTableEnd += 40;
                numberOfSections--;
            }

            var fileSize = fileStream.Length;
            var extraDataSize = fileSize - certificateTableSize - sumOfBytesHashed;
            if (extraDataSize > int.MaxValue) return null;
            var signedImageContent = new byte[sumOfBytesHashed - (4 + 8) + extraDataSize];

            int bytesToCheckSumField = (int)headerOffset + 20 + 64; // size of standard header + offset of checksum field in optional header

            int currentImageContentIndex = 0;
            fileStream.Seek(0, SeekOrigin.Begin);
            if (fileStream.Read(signedImageContent, 0, bytesToCheckSumField) != bytesToCheckSumField) return null;
            currentImageContentIndex += bytesToCheckSumField;

            fileStream.Seek(4, SeekOrigin.Current); // Skip the checksum field

            // Read up to the beginning of the certificate table entry
            int remainingbytesToBeginningOfCertTableEntry = (int)certificateTableEntryOffset - bytesToCheckSumField - 4;
            if (fileStream.Read(signedImageContent, currentImageContentIndex, remainingbytesToBeginningOfCertTableEntry) != remainingbytesToBeginningOfCertTableEntry) return null;
            currentImageContentIndex += remainingbytesToBeginningOfCertTableEntry;

            fileStream.Seek(8, SeekOrigin.Current); // skip the cert table entry

            // Read up to the end of the image header (including / section table)
            int remainingBytesToEndOfHeader = (int)sizeOfHeaders - (currentImageContentIndex + (4 + 8));
            if (fileStream.Read(signedImageContent, currentImageContentIndex, remainingBytesToEndOfHeader) != remainingBytesToEndOfHeader) return null;
            currentImageContentIndex += (int)remainingBytesToEndOfHeader;

            var orderedSections = sectionInfos.OrderBy(section => section.Pointer);
            foreach (var section in orderedSections)
            {
                fileStream.Seek(section.Pointer, SeekOrigin.Begin);
                var sectionSize = section.Size;
                if (sectionSize > int.MaxValue) return null; // integer overflow!
                if (fileStream.Read(signedImageContent, currentImageContentIndex, (int)sectionSize) != sectionSize) return null;
                currentImageContentIndex += (int)sectionSize;
            }

            if (extraDataSize > 0)
            {
                fileStream.Seek(extraDataSize, SeekOrigin.End);
                if (fileStream.Read(signedImageContent, currentImageContentIndex, (int)extraDataSize) != extraDataSize) return null;
                currentImageContentIndex += (int)extraDataSize;
            }
            if (currentImageContentIndex != signedImageContent.Length)
            {
                return null;
            }

            byte[] fileHash;
            using (var sha1 = new System.Security.Cryptography.SHA1Managed())
            {
                fileHash = sha1.ComputeHash(signedImageContent);
            }
            return fileHash;
        }

        private static bool ReadUInt16LittleEndian(FileStream fileStream, out UInt16 value)
        {
            var valueBytes = new byte[2];
            if (fileStream.Read(valueBytes, 0, valueBytes.Length) != valueBytes.Length)
            {
                value = 0;
                return false;
            }
            value = (UInt16)(valueBytes[0] + (valueBytes[1] << 8));
            return true;
        }
        private static bool ReadUInt32LittleEndian(FileStream fileStream, out UInt32 value)
        {
            var valueBytes = new byte[4];
            if (fileStream.Read(valueBytes, 0, valueBytes.Length) != valueBytes.Length)
            {
                value = 0;
                return false;
            }
            value = valueBytes[0] + ((UInt32)valueBytes[1] << 8) + ((UInt32)valueBytes[2] << 16) + ((UInt32)valueBytes[3] << 24);
            return true;
        }
        #endregion

        private static bool DontVerifyTrust;
        private static bool VerifyTrustPath;
        private static bool DontVerifyIntegrity;
        private static ICDESystemLog MySYSLOG = null;

        public string GetAppCert(bool bDontVerifyTrust=false, string pFromFile = null, bool bVerifyTrustPath=true, bool bDontVerifyIntegrity=false)
        {
            if (!string.IsNullOrEmpty(MyAppCertThumb) && string.IsNullOrEmpty(pFromFile))
                return MyAppCertThumb;
            if (pFromFile == null)
            {
                pFromFile = Assembly.GetExecutingAssembly().Location; // C-DEngine.dll
            }
            DontVerifyTrust = bDontVerifyTrust;
            VerifyTrustPath = bVerifyTrustPath;
            DontVerifyIntegrity = bDontVerifyIntegrity;

            if (pFromFile == null)
                return null;
            //No BaseAssets access and MyServiceHostInfo not set at this point
            //string tBaseDir = TheBaseAssets.MyServiceHostInfo.BaseDirectory;
            //if (TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.IIS) tBaseDir += "bin\\";
            //string uDir = tBaseDir;
            //if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ISMMainExecutable))
            //{
            //    uDir += TheBaseAssets.MyServiceHostInfo.ISMMainExecutable;
            //    if (TheBaseAssets.MyServiceHostInfo.cdeHostingType != cdeHostType.IIS)
            //    {
            //        var hostPath = uDir + ".exe";
            //        if (!File.Exists(hostPath))
            //        {
            //            // We may be running under .Net Core with a .dll-based host
            //            hostPath = uDir + ".dll";
            //        }
            //        uDir = hostPath;
            //    }
            //}
            //else
            //    uDir += "C-DEngine.dll";
            MySYSLOG?.WriteToLog(eDEBUG_LEVELS.ESSENTIALS, 418, "Diagnostics", $"Reading Cert on ISM {pFromFile} ");
            try
            {
                MyAppHostCert = VerifyPEAndReadSignerCert(pFromFile, VerifyTrustPath, !DontVerifyIntegrity);
            }
            catch (Exception)
            {
                MyAppHostCert = null;
            }
            if (MyAppHostCert == null)
            {
                MySYSLOG?.WriteToLog(eDEBUG_LEVELS.ESSENTIALS, 418, "Diagnostics", $"Base Application {pFromFile} is not signed - C-DEngine is used", eMsgLevel.l1_Error);
                pFromFile = Assembly.GetExecutingAssembly().Location;
                try
                {
                    MyAppHostCert = VerifyPEAndReadSignerCert(pFromFile, VerifyTrustPath, !DontVerifyIntegrity);
                }
                catch (Exception)
                {
                    MyAppHostCert = null;
                }
            }
            if (MyAppHostCert == null)
            {
                MySYSLOG?.WriteToLog(eDEBUG_LEVELS.ESSENTIALS, 418, "Diagnostics", $"C-DEngine is not signed or signature could not be read", eMsgLevel.l1_Error);
            }
            return MyAppCertThumb = MyAppHostCert?.Thumbprint?.ToLower();
        }

        public bool IsTrusted(string fileName)
        {
            if (DontVerifyTrust)
                return true;
            // Create certificates for c-labs dll and file being verified
            X509Certificate2 cert;
            try
            {
                if (MyAppHostCert == null)
                {
                    //GetAppCert();   //4.211.0: This will not return any new information as this is called now in InitAssets
                    return false; //4.211.0: We did try to load the AppCert earlier. If there is no AppCert we are "not trusted"
                }
                // Note: we only check one certificate, so plug-ins can not have more than one signature for this check for work reliably
                fileName=Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                cert = VerifyPEAndReadSignerCert(fileName, VerifyTrustPath, !DontVerifyIntegrity);
            }
            catch (Exception)
            {
                cert = null;
            }

            if (cert == null)
            {
                MySYSLOG?.WriteToLog(eDEBUG_LEVELS.ESSENTIALS, 418, "Diagnostics", $"Cert on Plugin {fileName} not correct or does not exist", eMsgLevel.l1_Error);
                //TheCDEKPIs.IncrementKPI(eKPINames.UnsignedPlugins);
                return false;
            }

            // Check to see if thumbprints on file and c-labs file are the same
            bool match = MyAppHostCert?.Thumbprint.Equals(cert?.Thumbprint, StringComparison.OrdinalIgnoreCase) == true || "7106454E5B20D063623F2BAA26B3A62BBE1249C3".Equals(cert.Thumbprint, StringComparison.OrdinalIgnoreCase);
            return match;
        }
    }
}
