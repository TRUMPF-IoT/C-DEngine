// SPDX-FileCopyrightText: Copyright (c) 2009-2026 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using C_DEngine.Tests.TestCommon;
using NUnit.Framework;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;

namespace CDEngine.Communication.Net45.Tests
{
    [TestFixture]
    public class TheCommCoreTests : TestHost
    {
        private bool wasScopingEnabled;

        [SetUp]
        public void InitTests()
        {
            StartHost();
            // A node without a scope refuses every ISB request before it looks at it.
            var scopeManager = (TheDefaultScopeManager)TheBaseAssets.MyScopeManager;
            wasScopingEnabled = scopeManager.IsScopingEnabled;
            scopeManager.IsScopingEnabled = true;
        }

        [TearDown]
        public void ShutdownHost()
        {
            ((TheDefaultScopeManager)TheBaseAssets.MyScopeManager).IsScopingEnabled = wasScopingEnabled;
            StopHost();
        }

        [Test]
        public void ARejectedRequest_KeepsTheConnectionOfTheNodeItClaimsToComeFrom()
        {
            var nodeId = Guid.NewGuid();
            var sender = StartIncomingSender(nodeId, null);

            // No session and no valid ISB path: the request proves nothing, it only names the node in its header.
            var request = CreateRequest("/ISB", nodeId);
            TheCommCore.MyHttpService.cdeProcessPost(request);

            Assert.That(request.ErrorDescription, Does.StartWith("1702:"));
            Assert.That(TheQueuedSenderRegistry.GetSenderByGuid(nodeId), Is.SameAs(sender));
        }

        [Test]
        public void ARejectedRequest_EndsTheConnectionOfItsOwnSession()
        {
            var nodeId = Guid.NewGuid();
            var session = TheBaseAssets.MySession.CreateSession(new TheRequestData { DeviceID = nodeId }, Guid.Empty);
            session.MyDevice = nodeId;
            TheBaseAssets.MySession.WriteSession(session);
            StartIncomingSender(nodeId, session);

            // A telegram that starts with a null byte is refused with 1709 once the session is known.
            var isbPath = TheBaseAssets.MyScopeManager.GetISBPath("/", cdeSenderType.CDE_BACKCHANNEL, cdeSenderType.CDE_BACKCHANNEL, 2, session.cdeMID, false);
            var request = CreateRequest(isbPath, nodeId);
            request.PostData = new byte[] { 0, 1, 2 };
            request.PostDataLength = request.PostData.Length;
            TheCommCore.MyHttpService.cdeProcessPost(request);

            Assert.That(request.ErrorDescription, Does.StartWith("1709:"));
            Assert.That(TheQueuedSenderRegistry.GetSenderByGuid(nodeId), Is.Null);
        }

        private static TheQueuedSender StartIncomingSender(Guid nodeId, TheSessionState session)
        {
            var sender = new TheQueuedSender();
            var channel = new TheChannelInfo(nodeId, TheBaseAssets.MyScopeManager.ScopeID, cdeSenderType.CDE_BACKCHANNEL, null)
            {
                MySessionState = session,
            };
            Assert.That(sender.StartSender(channel, null, true, null), Is.True);
            return sender;
        }

        private static TheRequestData CreateRequest(string path, Guid claimedNodeId)
        {
            return new TheRequestData
            {
                RequestUri = new Uri($"http://localhost:{TheBaseAssets.MyServiceHostInfo.MyStationPort}{path}"),
                HttpMethod = "POST",
                Header = new cdeConcurrentDictionary<string, string> { ["cdeDeviceID"] = claimedNodeId.ToString() },
            };
        }
    }
}
