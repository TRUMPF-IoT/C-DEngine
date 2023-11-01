// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿//extern alias MyNUnit;
//using MyNUnit.NUnit.Framework;
using NUnit.Framework;
using nsCDEngine.BaseClasses;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

#if !CDE_NET35
namespace CDEngine.BaseClasses.Net45.Tests
#else
namespace CDEngine.BaseClasses.Net35.Tests
#endif
{

    [TestFixture]
    public class TheCommonUtilsTests
    {
        class A
        {
            public string AName { get; set; }
            public B BRef { get; set; }
        }
        class B
        {
            public string BName { get; set; }
            public A ARef { get; set; }
        }
        [Test]
        public void SerializeObjectToJSONStringWithCycleTest()
        {
            var a1 = new A { AName = "A1" };
            var b1 = new B { BName = "B1" };
            var a2 = new A { AName = "A2" };
            a1.BRef = b1;
            b1.ARef = a2;
            var json = TheCommonUtils.SerializeObjectToJSONString(a1);
            var expectedJson = "{\"AName\":\"A1\",\"BRef\":{\"BName\":\"B1\",\"ARef\":{\"AName\":\"A2\"}}}";
            Assert.AreEqual(expectedJson, json);

            if (TheCommonUtils.cdeNewtonJSONConfig.ReferenceLoopHandling != cdeNewtonsoft.Json.ReferenceLoopHandling.Serialize)
            {
                a2.BRef = b1; // Loop A1 -> B1 -> A2 => B1

                // This will crash the process with stack overflow with option ReferenceLoopHandling.Serialize
                var jsonCycle = TheCommonUtils.SerializeObjectToJSONString(a1);
                var expectedJsonCycle = "{\"AName\":\"A1\",\"BRef\":{\"BName\":\"B1\",\"ARef\":{\"AName\":\"A2\"}}}";
                Assert.AreEqual(expectedJsonCycle, jsonCycle);
            }
        }

        [Test]
        public void CStrDateTimeOffsetTest()
        {
            var date = new DateTimeOffset(2017, 12, 21, 11, 12, 13, 456, new TimeSpan(1, 0, 0));
            var dateString = TheCommonUtils.CStr(date);
            var expectedString = "2017-12-21T11:12:13.4560000+01:00";
            Assert.AreEqual(expectedString, dateString);
        }

        [Test]
        public void DateTimeOffsetMinMaxValueTest()
        {
            {
                var date = DateTime.MinValue;
                var dateTimeOffsetValue = TheCommonUtils.CDate(date);
                var expectedValue = DateTimeOffset.MinValue;
                Assert.AreEqual(expectedValue, dateTimeOffsetValue);
            }
            {
                var date = DateTime.MaxValue;
                var dateTimeOffsetValue = TheCommonUtils.CDate(date);
                var expectedValue = DateTimeOffset.MaxValue;
                Assert.AreEqual(expectedValue, dateTimeOffsetValue);
            }
            // TODO Figure out a way to run these tests in interesting timezones as they depend on the local machines timezone:
            // UTC, Berlin (UTC+1 DST), UTC-1, Casablanca (UTC with DST), UTC-12 (International dateline West), UTC+12, UTC+14 (Kiritimati Island)
            // For now: run one of the build machines in UTC+1, the other in PST.
            {
                var date = new DateTime(1, 1, 1, 0, 10, 0, DateTimeKind.Local);
                var dateTimeOffsetValue = TheCommonUtils.CDate(date);
                var expectedTimeZone = TimeZoneInfo.Local.GetUtcOffset(date);
                DateTimeOffset expectedValue;
                if (expectedTimeZone > TimeSpan.Zero)
                {
                    expectedValue = DateTimeOffset.MinValue;
                }
                else
                {
                    expectedValue = new DateTimeOffset(date);
                }
                Assert.AreEqual(expectedValue, dateTimeOffsetValue);
            }
            {
                var date = new DateTime(9999, 12, 31, 23, 59, 50, DateTimeKind.Local);
                var dateTimeOffsetValue = TheCommonUtils.CDate(date);
                DateTimeOffset expectedValue;
                if (TimeZoneInfo.Local.GetUtcOffset(date) < TimeSpan.Zero)
                {
                    expectedValue = DateTimeOffset.MaxValue;
                }
                else
                {
                    expectedValue = new DateTimeOffset(date);
                }
                Assert.AreEqual(expectedValue, dateTimeOffsetValue);
            }
        }

        public void GetRandomUIntTest(int factor)
        {
            Thread.Sleep(100);
            var valueDistribution = new Dictionary<int, int>();
            int count = 300000 * factor;
            for (int i = 0; i < count; i++)
            {
                uint result;
                result = TheCommonUtils.GetRandomUInt(1, 1000);

                Assert.IsTrue(result >= 1 && result < 1000);
                if (result != 1)
                {

                }
                int iResult = (int)result;
                if (valueDistribution.ContainsKey(iResult))
                {
                    valueDistribution[iResult] = valueDistribution[iResult] + 1;
                }
                else
                {
                    valueDistribution.Add(iResult, 1);
                }
            }
            int acceptableCount = (int)((count / 1000) * 0.70);
            for (int i = 1; i < 1000; i++)
            {
                int valueCount = 0;
                valueDistribution.TryGetValue(i, out valueCount);
                if (valueCount < acceptableCount)
                {
                    count++;
                }
                Assert.IsFalse(valueCount < acceptableCount, $"Count for {i} not acceptable: {valueCount} should be {acceptableCount}");
            }
        }

        [Test]
        public void TestGetSafeFileName()
        {
            const string fileName = "a+b:c/d";
            string safeFileName = TheCommonUtils.GetSafeFileName(fileName, "json", true);
            var dateTimePart = safeFileName.Substring(fileName.Length + 1, 17);
            var timestamp = DateTime.ParseExact(dateTimePart, "yyyyMMddHHmmfffff", CultureInfo.InvariantCulture);
            Assert.AreEqual(safeFileName, $"a+b_c_d_{timestamp:yyyyMMddHHmmfffff}.json", "Actual File Name: " + safeFileName);
        }

        [Test]
        public void TestGetSafeFileNameNoTimeStamp()
        {
            string safeFileName = TheCommonUtils.GetSafeFileName("a+b:c/d", "json", false);
            Assert.IsTrue(safeFileName.Equals("a+b_c_d.json"), "Actual File Name: " + safeFileName);
        }

        [Test]
        public void TestGetSafeFileNameInvalidExtension()
        {
            string safeFileName = TheCommonUtils.GetSafeFileName("a+b:c/d", "jso", false);
            Assert.IsTrue(safeFileName.Equals("a+b_c_d.jso"), "Actual File Name: " + safeFileName);
        }
    }
}