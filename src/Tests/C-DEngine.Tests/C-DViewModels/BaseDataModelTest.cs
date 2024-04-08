// SPDX-FileCopyrightText: Copyright (c) 2009-2024 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using NUnit.Framework;

using nsCDEngine.ViewModels;
using System.Collections.Generic;

#if !CDE_NET35
namespace CDEngine.BaseDataModel.Net45.Tests
#else
namespace CDEngine.BaseDataModel.Net35.Tests
#endif
{
    [TestFixture]
    public class BaseDataModelTest
    {
        [Test]
        public void PlugInInfoCloneTest()
        {
            var pluginInfo = new ThePluginInfo
            {
                 DeviceTypes = new List<TheDeviceTypeInfo>
                 {
                     new TheDeviceTypeInfo { Description = "DT1", Capabilities = new eThingCaps[] { eThingCaps.CameraSource, eThingCaps.EnergyMeter }, DeviceType = "TestDeviceType" },
                 },
            };
            var pluginInfoClone = pluginInfo.Clone();
            Assert.That(pluginInfoClone.DeviceTypes, Is.Not.SameAs(pluginInfo.DeviceTypes));
            Assert.That(pluginInfoClone.DeviceTypes.Count, Is.EqualTo(pluginInfo.DeviceTypes.Count));
            int i = 0;
            foreach(var dt in pluginInfo.DeviceTypes)
            {
                Assert.That(dt.Description, Is.EqualTo(pluginInfoClone.DeviceTypes[i].Description));
                Assert.That(dt.DeviceType, Is.EqualTo(pluginInfoClone.DeviceTypes[i].DeviceType));
                Assert.That(pluginInfoClone.DeviceTypes[i].Capabilities, Is.Not.SameAs(dt.Capabilities));
                Assert.That(pluginInfoClone.DeviceTypes[i].Capabilities.Length, Is.EqualTo(dt.Capabilities.Length));
                for (int j = 0; j < dt.Capabilities.Length; j++)
                {
                    Assert.That(dt.Capabilities[j], Is.EqualTo(pluginInfoClone.DeviceTypes[i].Capabilities[j]));
                }
            }
        }
    }
}
