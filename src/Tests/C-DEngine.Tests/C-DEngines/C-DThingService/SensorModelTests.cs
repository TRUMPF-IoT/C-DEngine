// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿#define LOCAL_OPCSERVER
//extern alias MyNUnit;
//using MyNUnit.NUnit.Framework;
using NUnit.Framework;

using System;
using nsCDEngine.Engines.ThingService;
using C_DEngine.Tests.TestCommon;
using System.Collections.Generic;
using nsCDEngine.BaseClasses;
using System.Linq;

using nsCDEngine.ViewModels;

#if !CDE_NET35
namespace CDEngine.ThingService.SensorPipeline.Net45.Tests
#else
namespace CDEngine.ThingService.SensorPipeline.Net35.Tests
#endif
{
    [TestFixture]
    public class SensorModelTests : TestHost
    {

        class TestInfo
        {
            public TheThing.TheThingConfiguration Config;
            public int MinimumBrowseTags;
            public int MaximumBrowseTags;
            public Dictionary<string, object> SpecializationValues;
            public List<TheThing.TheSensorSubscription> PreTestSubscriptions;
        }

        // Default configs for plug-ins to be tested
        // TODO put these into external config files?
        Dictionary<string, TestInfo> configs = new Dictionary<string, TestInfo>
        {
            { "CDMyOPCUAClient.cdeOPCUaClient/OPC-UA Remote Server", new TestInfo
                {
                    MinimumBrowseTags = 10,
                    MaximumBrowseTags = 100,
                    Config = new TheThing.TheThingConfiguration
                    {
                        ConfigurationValues = new List<TheThing.TheConfigurationProperty>
                        {
                            new TheThing.TheConfigurationProperty { Name = "Address", Value = "opc.tcp://localhost:4840/c-labs/DataAccessServer" },
                            new TheThing.TheConfigurationProperty { Name = "DisableSecurity", Value = true },
                            new TheThing.TheConfigurationProperty { Name = "AcceptInvalidCertificate", Value = true },
                            new TheThing.TheConfigurationProperty { Name = "AcceptUntrustedCertificate", Value = true },
                            new TheThing.TheConfigurationProperty { Name = "DisableDomainCheck", Value = true },
                            new TheThing.TheConfigurationProperty { Name = "AutoConnect", Value = true },
                        }
                    },
                    SpecializationValues = new Dictionary<string, object>
                    {
                        { "Address", "opc.tcp://localhost:4840/c-labs/DataAccessServer" },
                    },
                }
            },
            { "Modbus.ModbusService/Modbus Device", new TestInfo
                {
                    MinimumBrowseTags = 0,
                    MaximumBrowseTags = 100,
                    Config = new TheThing.TheThingConfiguration
                    {
                        ConfigurationValues = new List<TheThing.TheConfigurationProperty>
                         {
                             new TheThing.TheConfigurationProperty { Name = "Address", Value = "192.168.1.80" },
                             new TheThing.TheConfigurationProperty { Name = "CustomPort", Value = 502 },
                             new TheThing.TheConfigurationProperty { Name = "SlaveAddress", Value = 1 },
                             new TheThing.TheConfigurationProperty { Name = "Offset", Value = 0 },
                             new TheThing.TheConfigurationProperty { Name = "Interval", Value = 1000 },
                             new TheThing.TheConfigurationProperty { Name = "KeepOpen", Value = false },
                             new TheThing.TheConfigurationProperty { Name = "ConnectionType", Value = 3 }
                         }
                    },
                    SpecializationValues = new Dictionary<string, object>
                    {
                        { "Address", "192.168.1.80" },
                    },
                    PreTestSubscriptions = new List<TheThing.TheSensorSubscription>
                    {
                        new TheThing.TheSensorSubscription { SensorId = TheCommonUtils.CStr(Guid.NewGuid()), TargetProperty = "Voltage1", ExtensionData = new Dictionary<string, object> { { "SourceOffset", 1 }, { "SourceType", "float" }, { "SourceSize", 2 } } },
                        new TheThing.TheSensorSubscription { SensorId = TheCommonUtils.CStr(Guid.NewGuid()), TargetProperty = "Current1", ExtensionData = new Dictionary<string, object> { { "SourceOffset", 13 }, { "SourceType", "float" }, { "SourceSize", 2 } } }
                    }
                }
            }
        };

        [Test]
        public void TestSensorProviderModel()
        {
            var deviceTypes = TheThingRegistry.GetAllDeviceTypesByCap(nsCDEngine.ViewModels.eThingCaps.SensorProvider, true);
            Assert.That(deviceTypes, Is.Not.Empty, "No sensor provider device types found");
            foreach (var deviceType in deviceTypes)
            {
                TestInfo testInfo;
                var  provider = CreateThingForTest(deviceType, out testInfo);
                WriteLine($"{deviceType}: Provider created");

                if (testInfo.PreTestSubscriptions != null)
                {
                    var subscribeResponse = provider.SubscribeSensorsAsync(new TheThing.MsgSubscribeSensors { SubscriptionRequests = testInfo.PreTestSubscriptions, ReplaceAll = false, DefaultTargetThing = provider }).Result;
                    Assert.That(subscribeResponse, Is.Not.Null, "Timeout or subscribe message not implemented by plug-in");
                    Assert.That(string.IsNullOrEmpty(subscribeResponse.Error), $"Error from SubscribeSensors: {subscribeResponse.Error}");
                }

                int subscriptionCount = BrowseAndSubscribeAllSensors(provider, null, testInfo.MaximumBrowseTags, testInfo.MinimumBrowseTags, out var browseResponse, out var sensorSubscriptionResponse, out var successfullSubscriptions);

                var getSubscriptionResponse = provider.GetSensorProviderSubscriptionsAsync().Result;
                Assert.That(getSubscriptionResponse, Is.Not.Null, "Timeout or get subscriptions message not implemented by plug-in");
                Assert.That(string.IsNullOrEmpty(getSubscriptionResponse.Error), $"Error from GetSensorSubscriptions: {getSubscriptionResponse.Error}");

                Assert.That(getSubscriptionResponse.Subscriptions, Has.Count.EqualTo(subscriptionCount), $"Subscription count doesn't match for {deviceType}");

                if (sensorSubscriptionResponse.SubscriptionStatus.Count > 0)
                {
                    var subscriptionIdToUnsubscribe = sensorSubscriptionResponse.SubscriptionStatus[0].Subscription.SubscriptionId ?? Guid.Empty;
                    var unsubscribeRequest = new TheThing.MsgUnsubscribeSensors
                    {
                        SubscriptionIds = new List<Guid> { subscriptionIdToUnsubscribe },
                    };
                    var unsubscribeResponse = provider.UnsubscribeSensorsAsync(unsubscribeRequest).Result;

                    Assert.That(unsubscribeResponse, Is.Not.Null, "Timeout or unsubscribe message not implemented by plug-in");
                    Assert.That(string.IsNullOrEmpty(unsubscribeResponse.Error), $"{deviceType}: Error from UnsubscribeSensors for {subscriptionIdToUnsubscribe}: {unsubscribeResponse.Error}");
                    Assert.That(unsubscribeResponse.Failed?.Count ?? 0, Is.EqualTo(0));
                }
                else
                {
                    WriteLine($"{deviceType}: No subscriptions found after subscribe request");
                }
                TheThingRegistry.DeleteThing(provider);
            }
        }

        [Test]
        public void TestSensorConsumerModel()
        {
            var deviceTypes = TheThingRegistry.GetAllDeviceTypesByCap(nsCDEngine.ViewModels.eThingCaps.SensorConsumer, true);
            Assert.That(deviceTypes, Is.Not.Empty, "No sensor consumer device types found");

            var sensorThing = new TheThing { EngineName = "CDMyVThings.TheVThings", DeviceType = "Memory Tag", ID="TestSensorThing01" };
            TheThingRegistry.RegisterThing(sensorThing);
            sensorThing.DeclareSensorProperty("Sensor01_1", ePropertyTypes.TNumber, new cdeP.TheSensorMeta { }).Value = 12345.67;
            sensorThing.DeclareSensorProperty("Sensor01_2", ePropertyTypes.TString, new cdeP.TheSensorMeta { }).Value = "Hello World!";
            sensorThing.DeclareSensorProperty("Sensor01_3", ePropertyTypes.TDate, new cdeP.TheSensorMeta { }).Value = DateTimeOffset.Now;

            var sensorThing2 = new TheThing { EngineName = "CDMyVThings.TheVThings", DeviceType = "Memory Tag", ID = "TestSensorThing02" };
            TheThingRegistry.RegisterThing(sensorThing);
            sensorThing2.DeclareSensorProperty("Sensor02_1", ePropertyTypes.TNumber, new cdeP.TheSensorMeta { }).Value = 12345.67;
            sensorThing2.DeclareSensorProperty("Sensor02_2", ePropertyTypes.TString, new cdeP.TheSensorMeta { }).Value = "Hello World!";
            sensorThing2.DeclareSensorProperty("Sensor02_3", ePropertyTypes.TDate, new cdeP.TheSensorMeta { }).Value = DateTimeOffset.Now;

            foreach (var deviceType in deviceTypes)
            {
                TestInfo testInfo;
                var consumer = CreateThingForTest(deviceType, out testInfo);
                WriteLine($"{deviceType}: Consumer created");

                var thingSubscriptions = new List<TheThing.TheThingSubscription>
                {
                        new TheThing.TheThingSubscription { ThingReference = sensorThing, },
                        new TheThing.TheThingSubscription { ThingReference = sensorThing2, },
                };

                {
                    var subscribeToThingsResponse = consumer.SubscribeToThingsAsync(thingSubscriptions).Result;

                    Assert.That(subscribeToThingsResponse, Is.Not.Null, $"Timeout or subscribe to things message not implemented by consumer {deviceType}");
                    Assert.That(string.IsNullOrEmpty(subscribeToThingsResponse.Error), $"Error from consumer {deviceType}: {subscribeToThingsResponse.Error}");
                    Assert.That(thingSubscriptions, Has.Count.GreaterThanOrEqualTo(subscribeToThingsResponse.SubscriptionStatus.Count), $"Not enough status reports for {deviceType}");
                    foreach (var subStatus in subscribeToThingsResponse.SubscriptionStatus)
                    {
                        Assert.That(string.IsNullOrEmpty(subStatus.Error), $"Error from consumer {deviceType}: {subStatus.Error}");
                        Assert.That(subStatus.Subscription.SubscriptionId.HasValue && subStatus.Subscription.SubscriptionId != Guid.Empty, $"No subscriptionid from consumer {deviceType}: {subStatus.Subscription.SubscriptionId}");
                    }
                }
                // TODO verify that subscriptions are actually getting consumed (at least for some consumer plug-ins)
                {
                    var getSubscriptionsResponse = consumer.GetThingSubscriptionsAsync().Result;

                    Assert.That(getSubscriptionsResponse, Is.Not.Null, $"Timeout or get things message not implemented by consumer {deviceType}");
                    Assert.That(string.IsNullOrEmpty(getSubscriptionsResponse.Error), $"Error from consumer {deviceType}: {getSubscriptionsResponse.Error}");
                    Assert.That(getSubscriptionsResponse.ThingSubscriptions, Has.Count.EqualTo(thingSubscriptions.Count), $"Missing subscriptions for {deviceType}");
                    foreach (var sub in getSubscriptionsResponse.ThingSubscriptions)
                    {
                        Assert.That(sub.SubscriptionId.HasValue && sub.SubscriptionId != Guid.Empty, $"No subscriptionid from consumer {deviceType}: {sub.SubscriptionId}");
                    }

                    var unsubscribeResponse = consumer.UnsubscribeFromThingsAsync(getSubscriptionsResponse.ThingSubscriptions.Select(sub => sub.SubscriptionId ?? Guid.Empty).Take(1)).Result;
                    Assert.That(unsubscribeResponse, Is.Not.Null, $"Timeout or unsubscribe things message not implemented by consumer {deviceType}");
                    Assert.That(string.IsNullOrEmpty(unsubscribeResponse.Error), $"Error from consumer {deviceType}: {unsubscribeResponse.Error}");
                    Assert.That(unsubscribeResponse.Failed != null && unsubscribeResponse.Failed.Count == 0, $"Errors during unsubscribe from consumer {deviceType}: {unsubscribeResponse.Failed.Aggregate("", (s, us) => $"{s} {us.Subscription.SubscriptionId}:{us.Error}")}");
                }

                {
                    var getSubscriptionsResponse = consumer.GetThingSubscriptionsAsync().Result;

                    Assert.That(getSubscriptionsResponse, Is.Not.Null, $"Timeout or get things message not implemented by consumer {deviceType}");
                    Assert.That(string.IsNullOrEmpty(getSubscriptionsResponse.Error), $"Error from consumer {deviceType}: {getSubscriptionsResponse.Error}");
                    Assert.That(getSubscriptionsResponse.ThingSubscriptions, Has.Count.EqualTo(thingSubscriptions.Count - 1), $"Missing subscriptions for {deviceType}");
                    foreach (var sub in getSubscriptionsResponse.ThingSubscriptions)
                    {
                        Assert.That(sub.SubscriptionId.HasValue && sub.SubscriptionId != Guid.Empty, $"No subscriptionid from consumer {deviceType}: {sub.SubscriptionId}");
                    }

                    var unsubscribeResponse = consumer.UnsubscribeFromThingsAsync(getSubscriptionsResponse.ThingSubscriptions.Select(sub => sub.SubscriptionId ?? Guid.Empty)).Result;
                    Assert.That(unsubscribeResponse, Is.Not.Null, $"Timeout or unsubscribe things message not implemented by consumer {deviceType}");
                    Assert.That(string.IsNullOrEmpty(unsubscribeResponse.Error), $"Error from consumer {deviceType}: {unsubscribeResponse.Error}");
                    Assert.That(unsubscribeResponse.Failed != null && unsubscribeResponse.Failed.Count == 0, $"Errors during unsubscribe from consumer {deviceType}: {unsubscribeResponse.Failed.Aggregate("", (s, us) => $"{s} {us.Subscription.SubscriptionId}:{us.Error}")}");
                }
                {
                    var getSubscriptionsResponse = consumer.GetThingSubscriptionsAsync().Result;

                    Assert.That(getSubscriptionsResponse, Is.Not.Null, $"Timeout or get things message not implemented by consumer {deviceType}");
                    Assert.That(string.IsNullOrEmpty(getSubscriptionsResponse.Error), $"Error from consumer {deviceType}: {getSubscriptionsResponse.Error}");
                    Assert.That(getSubscriptionsResponse.ThingSubscriptions, Is.Empty, $"Leaked subscriptions for {deviceType}");
                }

                if (consumer.DeviceType != eKnownDeviceTypes.IBaseEngine)
                {
                    TheThingRegistry.DeleteThing(consumer);
                }
            }
            TheThingRegistry.DeleteThing(sensorThing);
            TheThingRegistry.DeleteThing(sensorThing2);
        }


        [Test]
        public void TestConfigModel()
        {
            var deviceTypes = TheThingRegistry.GetAllDeviceTypesByCap(nsCDEngine.ViewModels.eThingCaps.ConfigManagement, true);

            foreach (var deviceType in deviceTypes)
            {
                WriteLine($"{deviceType}: Starting test.");
                TestInfo testInfo;
                var configThing = CreateThingForTest(deviceType, out testInfo);
                WriteLine($"{deviceType}: Provider created");

                int subscriptionCount;
                if (configThing.Capabilities.Contains(nsCDEngine.ViewModels.eThingCaps.SensorProvider))
                {
                    subscriptionCount = BrowseAndSubscribeAllSensors(configThing, null, testInfo.MaximumBrowseTags, testInfo.MinimumBrowseTags, out var browseResponse, out var sensorSubscriptionResponse, out var successfullSubscriptions);
                    WriteLine($"{deviceType}: Browsed and subscribed to {subscriptionCount} source sensors");
                }
                else
                {
                    WriteLine($"{deviceType}: Not a sensor provider: skipping browse and subscribe");
                    subscriptionCount = 0;
                }

                {
                    var exportedConfig = configThing.GetThingConfigurationAsync(false).Result;

                    Assert.That(exportedConfig, Is.Not.Null, $"Timeout or subscribe message not implemented by plug-in: {deviceType}");
                    //Assert.IsTrue(string.IsNullOrEmpty(exportedConfig.Error)); // TODO Add an error property to TheThingConfiguration?
                    Assert.That(exportedConfig.SensorSubscriptions?.Count ?? 0, Is.EqualTo(subscriptionCount));
                    Assert.That(exportedConfig.ConfigurationValues, Is.Not.Empty, $"No config properties returned: {deviceType}");
                    var configAsJson = TheCommonUtils.SerializeObjectToJSONString(exportedConfig);
                    WriteLine($"{deviceType}: Generated configuration export with {exportedConfig.ConfigurationValues.Count} settings, {exportedConfig.SensorSubscriptions?.Count ?? 0} sensor subscriptions: " + "{0}", configAsJson);
                }
                {
                    var exportedGeneralizedConfig = configThing.GetThingConfigurationAsync(true).Result;

                    Assert.That(exportedGeneralizedConfig, Is.Not.Null, $"Timeout or subscribe message not implemented by plug-in: {deviceType}");
                    //Assert.IsTrue(string.IsNullOrEmpty(exportedConfig.Error)); // TODO Add an error property to TheThingConfiguration?
                    Assert.That(exportedGeneralizedConfig.SensorSubscriptions?.Count ?? 0, Is.EqualTo(subscriptionCount));
                    Assert.That(exportedGeneralizedConfig.ConfigurationValues, Is.Not.Empty, $"No config properties returned: {deviceType}");
                    var configAsJson = TheCommonUtils.SerializeObjectToJSONString(exportedGeneralizedConfig);
                    WriteLine($"{deviceType}: Generated generalized configuration with {exportedGeneralizedConfig.ConfigurationValues.Count} settings, {exportedGeneralizedConfig.SensorSubscriptions?.Count ?? 0} sensor subscriptions: " + "{0}", configAsJson);
                }

                TheThingRegistry.DeleteThing(configThing);
            }
        }

        [Test]
        public void TestSensorPipeline()
        {
            var sensorThing = new TheThing { EngineName = "CDMyVThings.TheVThings", DeviceType = "Memory Tag", ID = "TestSensorThing03" };
            TheThingRegistry.RegisterThing(sensorThing);
            //sensorThing = TheThingRegistry.GetThingByMID(sensorThing.cdeMID);

            // Make all providers send properties into the memory thing
            var providers = new List<TheThing>();
            {
                var deviceTypes = TheThingRegistry.GetAllDeviceTypesByCap(nsCDEngine.ViewModels.eThingCaps.SensorProvider, true);
                Assert.That(deviceTypes, Is.Not.Empty, "No sensor provider device types found");
                foreach (var deviceType in deviceTypes)
                {
                    TestInfo testInfo;
                    var provider = CreateThingForTest(deviceType, out testInfo);
                    WriteLine($"{deviceType}: Provider created");

                    int subscriptionCount = BrowseAndSubscribeAllSensors(provider, sensorThing, testInfo.MaximumBrowseTags, testInfo.MinimumBrowseTags, out var browseResponse, out var sensorSubscriptionResponse, out var successfullSubscriptions);

                    var getSubscriptionResponse = provider.GetSensorProviderSubscriptionsAsync().Result;
                    Assert.That(getSubscriptionResponse, Is.Not.Null, "Timeout or get subscriptions message not implemented by plug-in");
                    Assert.That(string.IsNullOrEmpty(getSubscriptionResponse.Error), $"Error from GetSensorSubscriptions: {getSubscriptionResponse.Error}");

                    Assert.That(getSubscriptionResponse.Subscriptions, Has.Count.EqualTo(subscriptionCount));

                    if (sensorSubscriptionResponse.SubscriptionStatus.Count <= 0)
                    {
                        WriteLine($"{deviceType}: No subscriptions found after subscribe request");
                    }
                    providers.Add(provider);
                }
            }

            var consumers = new List<TheThing>();
            {
                var deviceTypes = TheThingRegistry.GetAllDeviceTypesByCap(nsCDEngine.ViewModels.eThingCaps.SensorConsumer, true);
                Assert.That(deviceTypes, Is.Not.Empty, "No sensor consumer device types found");

                foreach (var deviceType in deviceTypes)
                {
                    TestInfo testInfo;
                    var consumer = CreateThingForTest(deviceType, out testInfo);
                    WriteLine($"{deviceType}: Consumer created");

                    var thingSubscriptions = new List<TheThing.TheThingSubscription>
                    {
                        new TheThing.TheThingSubscription { ThingReference = sensorThing, },
                    };

                    {
                        var subscribeToThingsResponse = consumer.SubscribeToThingsAsync(thingSubscriptions).Result;

                        Assert.That(subscribeToThingsResponse, Is.Not.Null, $"Timeout or subscribe to things message not implemented by consumer {deviceType}");
                        Assert.That(string.IsNullOrEmpty(subscribeToThingsResponse.Error), $"Error from consumer {deviceType}: {subscribeToThingsResponse.Error}");
                        Assert.That(thingSubscriptions, Has.Count.GreaterThanOrEqualTo(subscribeToThingsResponse.SubscriptionStatus.Count), $"Not enough status reports for {deviceType}");
                        foreach (var subStatus in subscribeToThingsResponse.SubscriptionStatus)
                        {
                            Assert.That(string.IsNullOrEmpty(subStatus.Error), $"Error from consumer {deviceType}: {subStatus.Error}");
                            Assert.That(subStatus.Subscription.SubscriptionId.HasValue && subStatus.Subscription.SubscriptionId != Guid.Empty, $"No subscriptionid from consumer {deviceType}: {subStatus.Subscription.SubscriptionId}");
                        }
                    }
                    // TODO verify that subscriptions are actually getting consumed (at least for some consumer plug-ins)
                    {
                        var getSubscriptionsResponse = consumer.GetThingSubscriptionsAsync().Result;

                        Assert.That(getSubscriptionsResponse, Is.Not.Null, $"Timeout or get things message not implemented by consumer {deviceType}");
                        Assert.That(string.IsNullOrEmpty(getSubscriptionsResponse.Error), $"Error from consumer {deviceType}: {getSubscriptionsResponse.Error}");
                        Assert.That(getSubscriptionsResponse.ThingSubscriptions, Has.Count.EqualTo(thingSubscriptions.Count), $"Missing or too many subscriptions for {deviceType}");
                        foreach (var sub in getSubscriptionsResponse.ThingSubscriptions)
                        {
                            Assert.That(sub.SubscriptionId.HasValue && sub.SubscriptionId != Guid.Empty, $"No subscriptionid from consumer {deviceType}: {sub.SubscriptionId}");
                        }
                    }

                    consumers.Add(consumer);
                }
            }

            var pipelineConfig = sensorThing.GetThingPipelineConfigurationAsync(true).Result;

            foreach (var thing in consumers)
            {
                if(thing.DeviceType != eKnownDeviceTypes.IBaseEngine)
                    TheThingRegistry.DeleteThing(thing);
            }

            foreach (var thing in providers)
            {
                if (thing.DeviceType != eKnownDeviceTypes.IBaseEngine)
                    TheThingRegistry.DeleteThing(thing);
            }
            // TODO Verify that provider properties are gone

            TheThingRegistry.DeleteThing(sensorThing);

            foreach (var thingConfig in pipelineConfig.ThingConfigurations)
            {
                if (configs.TryGetValue($"{thingConfig.ThingIdentity.EngineName}/{thingConfig.ThingIdentity.DeviceType}", out var testInfo))
                {
                    if (testInfo.SpecializationValues != null)
                    {
                        foreach (var propKV in testInfo.SpecializationValues)
                        {
                            thingConfig.ThingSpecializationParameters[propKV.Key] = propKV.Value;
                        }
                    }
                }
            }

            var owner = TheThingRegistry.GetBaseEngineAsThing(eEngineName.ContentService);
            var things = TheThing.CreateThingPipelineFromConfigurationAsync(owner, pipelineConfig).Result;

            Assert.That(things, Has.Count.EqualTo(pipelineConfig.ThingConfigurations.Count), "Not all pipeline things were created.");
            Assert.That(things, Has.Count.EqualTo(pipelineConfig.ThingConfigurations.Count(cfg => cfg.ThingSpecializationParameters != null)), "Not all specialization parameters were created.");
            int i = 0;
            foreach (var thing in things)
            {
                Assert.That(thing, Is.Not.Null, $"Thing {i} not created: {pipelineConfig.ThingConfigurations[i].ThingIdentity}");
                i++;
            }
            var memoryThing = things.FirstOrDefault(t => t.EngineName == sensorThing.EngineName);
            Assert.That(memoryThing, Is.Not.Null);
            var minimumTagCount = configs.Sum(c => c.Value.MinimumBrowseTags);
            Assert.That(memoryThing.MyPropertyBag, Has.Count.GreaterThan(minimumTagCount)); // TODO adjust this to actual sensors, not just the OPC UA client subscription count

            foreach (var thing in things)
            {
                if (thing.DeviceType != eKnownDeviceTypes.IBaseEngine)
                    TheThingRegistry.DeleteThing(thing);
            }
        }


        private TheThing CreateThingForTest(TheEngineDeviceTypeInfo deviceType, out TestInfo testInfo)
        {
            if (!configs.TryGetValue($"{deviceType.EngineName}/{deviceType.DeviceType}", out testInfo))
            {
                WriteLine($"{deviceType}: No config found. Using default.");
                // No test config found: just create the instance without any configuration
                testInfo = new TestInfo
                {
                    MinimumBrowseTags = 0,
                    MaximumBrowseTags = 100,
                    Config = new TheThing.TheThingConfiguration
                    {
                        ThingIdentity = new TheThingIdentity
                        {
                            EngineName = deviceType.EngineName,
                            DeviceType = deviceType.DeviceType,
                        },
                        ConfigurationValues = new List<TheThing.TheConfigurationProperty>
                        {
                        }
                    }
                };
            }

            var config = testInfo.Config;
            if (string.IsNullOrEmpty(config.ThingIdentity?.EngineName))
            {
                config.ThingIdentity = new TheThingIdentity
                {
                    EngineName = deviceType.EngineName,
                    DeviceType = deviceType.DeviceType,
                };
                var addressValue = config.ConfigurationValues.FirstOrDefault(c => c.Name == "Address");
                if (addressValue != null)
                {
                    config.ThingIdentity.ID = TheCommonUtils.CStr(addressValue);
                }
            }

            var owner = TheThingRegistry.GetBaseEngineAsThing(eEngineName.ContentService);
            var testThing = TheThing.CreateThingFromConfigurationAsync(owner, config).Result;
            Assert.That(testThing, Is.Not.Null, $"Error creating thing for {config.ThingIdentity.EngineName} / {config.ThingIdentity.DeviceType}");
            return testThing;
        }


        private static int BrowseAndSubscribeAllSensors(TheThing provider, TheThing targetThing, int maxSubscriptions, int minimumBrowseTags, out TheThing.MsgBrowseSensorsResponse browseResponse,
            out TheThing.MsgSubscribeSensorsResponse sensorSubscriptionResponse,
            out List<TheThing.TheSensorSubscriptionStatus> successfullSubscriptions)
        {
            browseResponse = provider.ProviderBrowseSensorsAsync().Result;
            Assert.That(browseResponse, Is.Not.Null, $"Timeout or browse message not implemented by plug-in: {new TheThingReference(provider)}");
            Assert.That(string.IsNullOrEmpty(browseResponse.Error), $"Browse error: {browseResponse.Error}");
            Assert.That(browseResponse.Sensors, Has.Count.GreaterThanOrEqualTo(minimumBrowseTags));

            var subscribeRequest = new TheThing.MsgSubscribeSensors
            {
                DefaultTargetThing = provider,
                ReplaceAll = true,
                SubscriptionRequests = browseResponse.Sensors.Take(maxSubscriptions).Select(s =>
                    new TheThing.TheSensorSubscription
                    {
                        TargetThing = targetThing,
                        TargetProperty = TheCommonUtils.CListToString(s.DisplayNamePath, "."),
                        SensorId = s.SensorId,
                        SampleRate = 1000, // TODO make the test configurable for custom subscription parameters
                        //CustomSubscriptionInfo = new Dictionary<string, object>
                        //{
                        //    { "QueueSize", 50 },
                        //},
                        ExtensionData = s.ExtensionData,
                    }
                ).ToList(),
            };
            int subscriptionCount = subscribeRequest.SubscriptionRequests.Count;
            sensorSubscriptionResponse = provider.SubscribeSensorsAsync(subscribeRequest).Result;

            Assert.That(sensorSubscriptionResponse, Is.Not.Null, "Timeout or subscribe message not implemented by plug-in");
            Assert.That(string.IsNullOrEmpty(sensorSubscriptionResponse.Error), $"Error from SubscribeSensors: {sensorSubscriptionResponse.Error}. {new TheThingReference(provider)}");
            Assert.That(subscriptionCount, Is.EqualTo(sensorSubscriptionResponse.SubscriptionStatus.Count));
            foreach (var status in sensorSubscriptionResponse.SubscriptionStatus)
            {
                Assert.That(string.IsNullOrEmpty(status.Error) || status.Error.Contains("BadUserAccessDenied"), $"{status.Subscription.SensorId}: {status.Error}");
            }

            successfullSubscriptions = sensorSubscriptionResponse.SubscriptionStatus.Where(s => string.IsNullOrEmpty(s.Error)).ToList();
            return subscriptionCount;
        }

#if! OPCUASERVER
        [SetUp]
        public void InitTests()
        {
            StartHost();
        }
#else
        static private TheThing myOPCServer;

        [SetUp]
        public void InitTests()
        {
            StartHost();

            if (myOPCServer == null)
            {
                myOPCServer = StartOPCServer(true);
            }
            Assert.IsNotNull(myOPCServer);
        }
#endif

        [TearDown]
        public void ShutdownHost()
        {
            StopHost();
        }

        void WriteLine(string format, params object[] args)
        {
            System.Diagnostics.Debug.WriteLine(String.Format($"{DateTimeOffset.Now:O}: {format}", args));
            Console.WriteLine(format, args);
            TestContext.WriteLine(format, args);
        }
    }
}
