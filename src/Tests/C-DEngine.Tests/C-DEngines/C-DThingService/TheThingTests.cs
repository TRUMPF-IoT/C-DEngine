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
    public class TheThingTests : TestHost
    {
        [Test]
        public void SetPropertyCloningTest()
        {
            var testThing = new TheThing();
            {
                // This test illustrates some of the corner cases with property values:
                // 1) object values are not cloned on set property; with strings this is mostly irrelevant
                string x = "123";
                testThing.SetProperty("Prop1", x);
                var prop1Value = testThing.GetProperty("Prop1").Value;
                Assert.AreEqual(prop1Value, x);
                Assert.AreSame(prop1Value, x); // Strings can't be (and don't need to be) cloned because they are immutable
                Assert.AreSame(testThing.GetProperty("Prop1").Value, x);

                x = "456";
                Assert.AreEqual(prop1Value, "123");
                Assert.AreNotSame(prop1Value, x);

                testThing.SetProperty("Prop1", "789");
                Assert.IsTrue(String.Equals(x, "456"));
                Assert.AreEqual(prop1Value, "123");

                var prop1Value_2 = testThing.GetProperty("Prop1").Value;
                Assert.AreEqual(prop1Value_2, "789");
                Assert.AreEqual(prop1Value, "123");
                Assert.AreNotSame(prop1Value_2, prop1Value);
            }

            // 2) value types
            {
                // This test illustrates some of the corner cases with property values:
                // 1) object values are not cloned on set property; with strings this is mostly irrelevant
                double x = 1.23;
                testThing.SetProperty("Prop1", x);
                var prop1Value = testThing.GetProperty("Prop1").Value;
                Assert.AreEqual(prop1Value, x);
                Assert.AreNotSame(prop1Value, x); // Value types are automatically cloned
                Assert.AreNotSame(testThing.GetProperty("Prop1").Value, x); // Value types are automatically cloned

                x = 4.56;
                Assert.AreEqual(prop1Value, 1.23);
                Assert.AreNotSame(prop1Value, x);

                testThing.SetProperty("Prop1", 7.89);
                Assert.IsTrue(double.Equals(x, 4.56));
                Assert.AreEqual(prop1Value, 1.23);

                var prop1Value_2 = testThing.GetProperty("Prop1").Value;
                Assert.AreEqual(prop1Value_2, 7.89);
                Assert.AreEqual(prop1Value, 1.23);
                Assert.AreNotSame(prop1Value_2, prop1Value);
            }

            // 3) with mutable objects, the differences are more substantial
            {
                // 3a) non-cloneable
                MyTestClass xObj = new MyTestClass { x = "123" };
                testThing.SetProperty("Prop3", xObj);
                var prop2Value = testThing.GetProperty("Prop3").Value;
                Assert.AreEqual(prop2Value, xObj);
                Assert.AreSame(prop2Value, xObj); // This would be different if we cloned

                xObj.x = "456";
                Assert.AreEqual(prop2Value, new MyTestClass { x = "456" }); // modifying the original object modifies the value of the property
                Assert.AreSame(prop2Value, xObj);

                testThing.SetProperty("Prop3", new MyTestClass { x = "789" });
                Assert.IsTrue(String.Equals(xObj, new MyTestClass { x = "456" }));
                Assert.AreEqual(prop2Value, new MyTestClass { x = "456" });

                var prop2Value_2 = testThing.GetProperty("Prop3").Value;
                Assert.AreEqual(prop2Value_2, new MyTestClass { x = "789" });
                Assert.AreEqual(prop2Value, new MyTestClass { x = "456" });
                Assert.AreNotSame(prop2Value_2, prop2Value);
            }

            {
                // 3b) cloneable
                MyTestClassCloneable xObj = new MyTestClassCloneable { x = "123" };
                testThing.SetProperty("Prop4", xObj);
                var prop2Value = testThing.GetProperty("Prop4").Value;
                Assert.AreEqual(prop2Value, xObj);
                Assert.AreSame(prop2Value, xObj); // This would be different if we cloned

                xObj.x = "456";
                Assert.AreEqual(prop2Value, new MyTestClassCloneable { x = "456" }); // modifying the original object modifies the value of the property: this would be different if we cloned
                Assert.AreSame(prop2Value, xObj); // this would be different if we cloned

                testThing.SetProperty("Prop4", new MyTestClassCloneable { x = "789" });
                Assert.IsTrue(String.Equals(xObj, new MyTestClassCloneable { x = "456" }));
                Assert.AreEqual(prop2Value, new MyTestClassCloneable { x = "456" });

                var prop2Value_2 = testThing.GetProperty("Prop4").Value;
                Assert.AreEqual(prop2Value_2, new MyTestClassCloneable { x = "789" });
                Assert.AreEqual(prop2Value, new MyTestClassCloneable { x = "456" });
                Assert.AreNotSame(prop2Value_2, prop2Value);
            }

        }
        class MyTestClass
        {
            public string x;

            public override bool Equals(object obj)
            {
                if (!(obj is MyTestClass))
                {
                    return false;
                }
                return ((MyTestClass)obj).x == x;
            }
            public override int GetHashCode()
            {
                return x.GetHashCode();
            }
        }

        class MyTestClassCloneable : ICloneable
        {
            public string x;

            public override bool Equals(object obj)
            {
                if (!(obj is MyTestClassCloneable))
                {
                    return false;
                }
                return ((MyTestClassCloneable) obj).x == x;
            }
            public override int GetHashCode()
            {
                return x.GetHashCode();
            }
            public object Clone()
            {
                return new MyTestClassCloneable { x = this.x };
            }
        }


        [Test]
        public void SetPropertyRoundtripTest()
        {
            var testThing = new TheThing();

            var date = new DateTimeOffset(2017, 12, 21, 11, 12, 13, 456, new TimeSpan(1, 0, 0));
            var dateExpected = new DateTimeOffset(2017, 12, 21, 11, 12, 13, 456, new TimeSpan(1, 0, 0));
            TheThing.SetSafePropertyDate(testThing, "TestDatePropName", date);
            date = DateTimeOffset.MinValue;
            var dateReturned = TheThing.GetSafePropertyDate(testThing, "TestDatePropName");
            Assert.AreEqual(dateExpected, dateReturned);
        }

        [Test]
        public void DeclareSecurePropertyTest()
        {
            var testPW = "testpassword123YYY###$$$";
            var testThing = new TheThing();
            TheThing.SetSafePropertyString(testThing, "Password", testPW);

            var testPWReadNotEncrypted = TheThing.GetSafePropertyString(testThing, "Password");

            Assert.AreSame(testPW, testPWReadNotEncrypted, "Failed to read ununecrypted property");

            testThing.DeclareSecureProperty("Password", ePropertyTypes.TString);

            var testPWReadNotEncryptedObject = testThing.GetProperty("Password").Value;
            Assert.IsTrue(testPWReadNotEncryptedObject is string, $"Encrypted password is not of type string. Type: {testPWReadNotEncryptedObject.GetType()}");
            var testPWReadObjectString = testPWReadNotEncryptedObject as string;

            Assert.AreNotSame(testPW, testPWReadObjectString, "Password not encrypted after transition from unsecure to secure property");

            Assert.IsTrue((testPWReadNotEncryptedObject as string).StartsWith("&^CDESP1^&:"), "Encrypted string does not have expected encryption prefix");
            var testPWReadDecryptedString = TheThing.GetSafePropertyString(testThing, "Password");
            Assert.AreEqual(testPW, testPWReadDecryptedString, "Password not preserved after transition from unsecure to secure property");
        }

        [Test]
        public void GetAllPropertiesTest()
        {
            var tThing = new TheThing();
            tThing.SetProperty("Prop1", "v1");
            var prop2 = tThing.SetProperty("Prop2", "v2");
            var prop2_1 = prop2.SetProperty("Prop2_1", "v2_1");
            prop2_1.SetProperty("Prop2_1_1", "v2_1_1");
            prop2.SetProperty("Prop2_2", "v2_2");
            var allProps = tThing.GetAllProperties(10);
            var expectedProps = new List<string> { "Prop1", "Prop2", "[Prop2].[Prop2_1]", "[Prop2].[Prop2_1].[Prop2_1_1]", "[Prop2].[Prop2_2]" }.OrderBy(n => n);
            Assert.IsTrue(allProps.Select(p => cdeP.GetPropertyPath(p)).OrderBy(n => n).SequenceEqual(expectedProps));
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
