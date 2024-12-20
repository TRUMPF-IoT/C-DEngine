// SPDX-FileCopyrightText: Copyright (c) 2009-2024 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using C_DEngine.Tests.TestCommon;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

#if !CDE_NET35
namespace CDEngine.BaseClasses.Net45.Tests
#else
namespace CDEngine.BaseClasses.Net35.Tests
#endif
{
    [TestFixture]
    class TheSystemMessagesTests : TestHost
    {
        // Tests the eEngineEvents.IncomingMessage
        [Test]
        public void IncomingMessageEventTest()
        {
            var contentServiceThing = TheThingRegistry.GetBaseEngineAsThing(eEngineName.ContentService);
            var contentServiceEng = contentServiceThing.GetBaseEngine();
            int numberOfMessages = 0;
            string txt = "TEST_TXT";
            string payload = "TestPayload";
            TSM testMsg = new TSM(eEngineName.ContentService, txt, payload);
            contentServiceEng?.RegisterEvent(eEngineEvents.IncomingMessage, (t, o) =>
            {
                numberOfMessages++;
                if (o is TheProcessMessage msg)
                {
                    Assert.That(txt, Is.EqualTo(msg.Message.TXT));
                    Assert.That(payload, Is.EqualTo(msg.Message.PLS));
                    Assert.That(contentServiceEng.GetEngineName(), Is.EqualTo(msg.Message.ENG));
                    testMsg.PLS = "TestPayload2";
                    //Assert.AreNotEqual(msg.Message.PLS, testMsg.PLS); // This fails
                }
                if (t is ICDEThing thing)
                {
                    Assert.That(contentServiceThing.cdeMID, Is.EqualTo(thing.GetBaseThing().cdeMID));
                }
            });
            TheCommCore.PublishCentral(testMsg, true);
            TheCommonUtils.SleepOneEye(5000, 1000);
            Assert.That(1, Is.EqualTo(numberOfMessages));
        }

        // Tests the eEngineEvents.IncomingMessage2 - fired for both Thing and Engine messages
        [Test]
        public void IncomingMessage2EventTest()
        {
            var contentServiceThing = TheThingRegistry.GetBaseEngineAsThing(eEngineName.ContentService);
            var contentServiceEng = contentServiceThing.GetBaseEngine();
            int numberOfMessages = 0;
            string txt = "CDE_GET_SERVICEINFO";
            string payload = "CHNL";
            TSM testMsg = new TSM(eEngineName.ContentService, txt, payload);
            contentServiceThing?.RegisterEvent(eEngineEvents.IncomingEngineMessage, (t, o) =>
            {
                // Two messages should be received here - The first intercepts the CDE_GET_SERVICEINFO
                // The second intercepts the CDE_SERVICEINFO response (since originator Thing was set to ContentService)
                numberOfMessages++;
            });
            testMsg.SetOriginatorThing(contentServiceThing);
            TheCommCore.PublishCentral(testMsg, true);
            TheCommonUtils.SleepOneEye(5000, 1000);
            Assert.That(2, Is.EqualTo(numberOfMessages));
        }

        [SetUp]
        public void InitTests()
        {
            StartHost();
        }

        [TearDown]
        public void ShutdownHost()
        {
            StopHost();
        }
    }
}
