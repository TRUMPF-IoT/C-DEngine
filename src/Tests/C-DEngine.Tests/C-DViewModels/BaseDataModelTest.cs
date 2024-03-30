// SPDX-FileCopyrightText: Copyright (c) 2009-2024 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using NUnit.Framework;

using nsCDEngine.ViewModels;
using System.Collections.Generic;
using NUnit.Framework.Legacy;

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
            ClassicAssert.AreNotSame(pluginInfo.DeviceTypes, pluginInfoClone.DeviceTypes);
            ClassicAssert.AreEqual(pluginInfo.DeviceTypes.Count, pluginInfoClone.DeviceTypes.Count);
            int i = 0;
            foreach(var dt in pluginInfo.DeviceTypes)
            {
                ClassicAssert.IsTrue(dt.Description == pluginInfoClone.DeviceTypes[i].Description);
                ClassicAssert.IsTrue(dt.DeviceType == pluginInfoClone.DeviceTypes[i].DeviceType);
                ClassicAssert.AreNotSame(dt.Capabilities, pluginInfoClone.DeviceTypes[i].Capabilities);
                ClassicAssert.AreEqual(dt.Capabilities.Length, pluginInfoClone.DeviceTypes[i].Capabilities.Length);
                for (int j = 0; j < dt.Capabilities.Length; j++)
                {
                    ClassicAssert.IsTrue(dt.Capabilities[j] == pluginInfoClone.DeviceTypes[i].Capabilities[j]);
                }
            }
        }
    }
}
