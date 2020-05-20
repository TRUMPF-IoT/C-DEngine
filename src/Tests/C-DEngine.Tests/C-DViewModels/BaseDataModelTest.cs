// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
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
            Assert.AreNotSame(pluginInfo.DeviceTypes, pluginInfoClone.DeviceTypes);
            Assert.AreEqual(pluginInfo.DeviceTypes.Count, pluginInfoClone.DeviceTypes.Count);
            int i = 0;
            foreach(var dt in pluginInfo.DeviceTypes)
            {
                Assert.IsTrue(dt.Description == pluginInfoClone.DeviceTypes[i].Description);
                Assert.IsTrue(dt.DeviceType == pluginInfoClone.DeviceTypes[i].DeviceType);
                Assert.AreNotSame(dt.Capabilities, pluginInfoClone.DeviceTypes[i].Capabilities);
                Assert.AreEqual(dt.Capabilities.Length, pluginInfoClone.DeviceTypes[i].Capabilities.Length);
                for (int j = 0; j < dt.Capabilities.Length; j++)
                {
                    Assert.IsTrue(dt.Capabilities[j] == pluginInfoClone.DeviceTypes[i].Capabilities[j]);
                }
            }
        }
    }
}
