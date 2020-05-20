// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using cdePackager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;

namespace cdePackager.Tests
{
    [TestClass()]
    public class ProgramTests
    {
        class TestLogger : ILogger
        {
            TestContext myTestContext;
            public TestLogger(TestContext context)
            {
                myTestContext = context;
            }

            public void WriteLine(string text)
            {
                myTestContext.WriteLine(text);
            }
        }

        [TestMethod()]
        [TestCategory("ExcludeOfficialBuild")]
        //[Ignore]
        public void PackageFileTestCDES()
        {
            string error;
            var result = cdePackager.ThePackager.PackagePlugIn(
                @"..\..\C-DEngine\C-DEngine.CDES",
                @"..\..\bin\Debug\C-DEngine\net45",
                null,
                "Platform", // TODO fix this for real
                new TestLogger(TestContext),
                true, out error);
            Assert.AreEqual(0, result, $"Error packaging CDES: result {result}. Error '{error}'");
            Assert.IsTrue(result == 0 && String.IsNullOrEmpty(error), $"Error text '{error}' with success return code {result}.");
            // TODO Validate CDEX exists and is valid
        }
        [TestMethod()]
        [TestCategory("ExcludeOfficialBuild")]
        //[Ignore]
        public void PackageFileTestCDESNoFinalSlash()
        {
            string error;
            var result = cdePackager.ThePackager.PackagePlugIn(
                @"..\..\..\..\..\CustomerPlugins\091 - TRUMPF Laser\CDES\LaserClient.CDES",
                @"..\..\..\..\..\052 - Factory-Relay\Axoom-Relay.Service\bin\Debug",
                null,
                "Platform", // TODO fix this for real
                new TestLogger(TestContext),
                true, out error);
            Assert.AreEqual(0, result, $"Error packaging CDES: result {result}. Error '{error}'");
            Assert.IsTrue(result == 0 && String.IsNullOrEmpty(error), $"Error text '{error}' with success return code {result}.");
            // TODO Validate CDEX exists and is valid
        }

        [TestMethod()]
        [TestCategory("ExcludeOfficialBuild")]
        //[Ignore]
        public void PackageFileTestDLL()
        {
            var binDir = @"..\..\..\..\..\052 - Factory-Relay\Axoom-Relay.Service\bin\Debug\";
            var inputFile = @"..\..\..\..\..\078 - CDMyOPCUAClient\CDMyOPCUAClient\bin\Debug\CDMyOPCUAClient.dll";
            var storeDir = @"Plugins\";
            var dllVersionString = (Assembly.LoadFrom(inputFile).GetCustomAttribute(typeof(AssemblyFileVersionAttribute)) as AssemblyFileVersionAttribute).Version;

            var versionParts = dllVersionString.Split(new char[] { '.' }, 2);
            double dllVersion = double.Parse($"{versionParts[0]}.{versionParts[1].Replace(".", "")}");

            var outputFileDefault = Path.Combine(binDir, $@"ClientBin\store\3c1d53ae-e932-4d11-b1f9-f12428dec27c\X64_V3\{dllVersion}\CDMyOPCUAClient-cdeOPCUaClient V{dllVersion}.CDEX");
            var outputFileStore = Path.Combine(storeDir, $@"CDMyOPCUAClient-cdeOPCUaClient V{dllVersion}.CDEX");

            PackageFileTestDLL(inputFile, binDir, null, outputFileDefault);
            PackageFileTestDLL(inputFile, binDir, storeDir, outputFileStore);

        }
        void PackageFileTestDLL(string inputFile, string binDir, string storeDir, string outputFile)
        {

            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            string error;
            var result = cdePackager.ThePackager.PackagePlugIn(
                inputFile,
                binDir,
                storeDir,
                "Platform", // TODO fix this for real
                new TestLogger(TestContext),
                true, out error);
            Assert.AreEqual(0, result, $"Error packaging DLL: result {result}. Error '{error}'");
            Assert.IsTrue(result == 0 && String.IsNullOrEmpty(error), $"Error text '{error}' with success return code {result}.");
            // TODO Validate CDEX exists and is valid
            Assert.IsTrue(File.Exists(outputFile), $"Generated CDEX not found at {outputFile}");
        }

        [TestMethod()]
        [TestCategory("ExcludeOfficialBuild")]
        //[Ignore]
        public void PackageFileTestDLLNoICDEItf()
        {
            string error;
            var result = cdePackager.ThePackager.PackagePlugIn(
                @"..\..\..\..\..\\078 - CDMyOPCUAClient\CDMyOPCUAClient\bin\Debug\Opc.Ua.Client.dll",
                @"..\..\..\..\..\052 - Factory-Relay\Axoom-Relay.Service\bin\Debug\",
                null,
                "Platform", // TODO fix this for real
                new TestLogger(TestContext),
                true, out error);
            Assert.AreEqual(2, result, $"Not the correct error code packaging DLL without an ICDEPlugin: result {result}. Error '{error}'");
            Assert.IsTrue(result == 2 && !String.IsNullOrEmpty(error) && error.Contains("did not contain C-DEngine Plug-In Interface"), $"CDES without ICDEPlugin: Correct error code {result} but wrong error text '{error}'.");
            // TODO Validate CDEX exists and is valid
        }

        [ClassInitialize()]
        public static void InitTests(TestContext context)
        {
            //_testContext = context;
        }

        public TestContext TestContext { get; set; }

    }
}