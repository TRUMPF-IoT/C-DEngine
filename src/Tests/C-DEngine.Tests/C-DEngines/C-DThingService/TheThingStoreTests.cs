// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using NUnit.Framework;

using C_DEngine.Tests.TestCommon;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if !CDE_NET35
namespace CDEngine.ThingService.Net45.Tests
#else
namespace CDEngine.ThingService.Net35.Tests
#endif
{
    [TestFixture]
    public class TheThingStoreTests : TestHost
    {
        [Test]
        public void CloneTests()
        {
            var thingStore = new TheThingStore();
            thingStore.PB.Add("prop1", "value1");
            thingStore.PB.Add("prop2", "value2");
            thingStore.PB.Add("prop3", (double) 3);

            var clone = thingStore.CloneForThingSnapshot(null, false, null, true);
            Assert.That(clone, Is.Not.SameAs(thingStore));
            Assert.That(clone.PB, Has.Count.EqualTo(thingStore.PB.Count));
            foreach(var prop in thingStore.PB)
            {
                Assert.That(clone.PB.ContainsKey(prop.Key) && clone.PB[prop.Key] == prop.Value);
            }
            thingStore.PB["prop1"] = "modifiedvalue1";
            foreach (var prop in thingStore.PB)
            {
                object expectedValue = prop.Value;
                if (prop.Key == "prop1")
                {
                    expectedValue = "value1";
                    Assert.That(prop.Value, Is.Not.EqualTo(expectedValue));
                }
                Assert.That(clone.PB.ContainsKey(prop.Key) && clone.PB[prop.Key] == expectedValue);
            }
        }
        public TestContext TestContext;

        //static int activeHosts = 0;
        [SetUp]
        public void InitTests()
        {
            //if (System.Threading.Interlocked.Increment(ref activeHosts) != 1)
            //{
            //    return;
            //}
            StartHost();
        }

        [TearDown]
        public void ShutdownHost()
        {
            //if (System.Threading.Interlocked.Decrement(ref activeHosts) <= 0)
            {
                StopHost();
            }
        }
    }
}
