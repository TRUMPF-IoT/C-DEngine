// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using DataLogUtilities;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataLogUtilities.Tests
{
    [TestFixture]
    public class DataLogParserTest
    {
        [Test]
        //[Ignore("ExcludeOfficialBuild")]
        public void CompareTDS01DataLogs()
        {
            var tzOffset = new TimeSpan(0, 0, 0);
            TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo();
            TestDataLogParser<TheThingStore>(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData\\TDS01", "opcclientdata.log"), Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData\\TDS01", "meshsenderdata.log"), DataLogParser.ConvertTDS01MeshSenderDataToOpcTags, tzOffset, 0, -1, 0, 0);
        }

        [Test]
        //[Ignore("ExcludeOfficialBuild")]
        public void TestDataLogParser()
        {
            var tzOffset = new TimeSpan(0, 0, 0);
            TestDataLogParser<JSonOpcArrayElement[]>(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "opcclientdata.log"), Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "meshsenderdata.log"), DataLogParser.ConvertMeshSenderDataToOpcTags, tzOffset, 0, - 1, 0, 0);
            TestDataLogParser<JSonOpcArrayElement[]>(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "opcclientdata.log"), Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "meshsenderdata_miss2.log"), DataLogParser.ConvertMeshSenderDataToOpcTags, tzOffset, 0, 0, 2, 0);
            TestDataLogParser<JSonOpcArrayElement[]>(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "opcclientdata.log"), Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "meshsenderdata_timebad.log"), DataLogParser.ConvertMeshSenderDataToOpcTags, tzOffset, 0, 0, 1, 1);
            TestDataLogParser<JSonOpcArrayElement[]>(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "opcclientdata.log"), Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "meshsenderdata_duplicate.log"), DataLogParser.ConvertMeshSenderDataToOpcTags, tzOffset, 0, 1, 0, 0);
        }

        internal void TestDataLogParser<T>(string opcClientDataPath, string meshDataPath, Func<List<MeshSenderDataLogEntry<T>>, TimeSpan, List<OpcClientDataLogEntry>>converter, TimeSpan timeZoneOffset, int toleranceInTicks = 0, int expectedDuplicates = -1, int expectedMissing = 0, int expectedExtras = 0, Action<List<OpcClientDataLogEntry>, List<OpcClientDataLogEntry>> sanitizeDataCallback = null)
        {
            var stats = DataLogComparer.CompareDataLogs(opcClientDataPath, meshDataPath, TestContext.CurrentContext.WorkDirectory, converter, timeZoneOffset, toleranceInTicks, sanitizeDataCallback);
            TestContext.WriteLine($"Found {stats.duplicateOPCTagCount} duplicate opc tags");

            if (stats.duplicateMeshTagCount != expectedDuplicates)
            {
                TestContext.WriteLine($"Found {stats.duplicateMeshTagCount} duplicate mesh tags");
            }
            if (expectedDuplicates >= 0)
            {
                Assert.AreEqual(expectedDuplicates, stats.duplicateMeshTagCount);
            }

            TestContext.WriteLine($"Found {stats.missingOpcTags} missing OPC tags");
            TestContext.WriteLine($"Found {stats.extraMeshTags} unexpected mesh tags");

            Assert.AreEqual(expectedMissing, stats.missingOpcTags, "Some OPC Tags were not sent");
            Assert.AreEqual(expectedExtras, stats.extraMeshTags, "Some Mesh Tags were received that had not been sent (or were altered on the way)");
        }

    }
}
