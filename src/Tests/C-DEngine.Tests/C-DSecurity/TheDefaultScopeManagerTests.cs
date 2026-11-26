// SPDX-FileCopyrightText: Copyright (c) 2009-2026 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using C_DEngine.Tests.TestCommon;
using NUnit.Framework;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Text;

namespace CDEngine.Security.Net45.Tests
{
    [TestFixture]
    public class TheDefaultScopeManagerTests : TestHost
    {
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

        [Test]
        public void AnIsbPath_NeedsNoEscaping()
        {
            var session = Guid.NewGuid();
            foreach (var counter in Counters())
            {
                var path = TheBaseAssets.MyScopeManager.GetISBPath("/", cdeSenderType.CDE_BACKCHANNEL, cdeSenderType.CDE_BACKCHANNEL, counter, session, false);

                Assert.That(path, Does.Not.Contain("%"), $"counter {counter}");
            }
        }

        // Nodes up to 7.111 read the NPA of a telegram without unescaping it
        [Test]
        public void AnIsbPath_ReadWithoutUnescaping_YieldsItsCounterAndSession()
        {
            var session = Guid.NewGuid();
            foreach (var counter in Counters())
            {
                var path = TheBaseAssets.MyScopeManager.GetISBPath("/", cdeSenderType.CDE_BACKCHANNEL, cdeSenderType.CDE_BACKCHANNEL, counter, session, false);

                var parts = Encoding.UTF8.GetString(Convert.FromBase64String(path.Substring("/ISB".Length))).Split('_');
                Assert.That(TheCommonUtils.CLng(parts[1]), Is.EqualTo(counter), $"counter {counter}");
                Assert.That(TheCommonUtils.CGuid(parts[3]), Is.EqualTo(session), $"counter {counter}");
            }
        }

        // A relay up to 7.111 escapes the Base64 padding of a path in the NPA of its telegrams
        [Test]
        public void AnEscapedIsbPath_YieldsItsSession()
        {
            var session = Guid.NewGuid();
            var escaped = Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes($"1234540_10_6_{session}")));
            Assert.That(escaped, Does.Contain("%3D"));

            TheBaseAssets.MyScopeManager.ParseISBPath(escaped, out var parsedSession, out var senderType, out var counter, out _);

            Assert.That(parsedSession, Is.EqualTo(session));
            Assert.That(counter, Is.EqualTo(10));
            Assert.That(senderType, Is.EqualTo(cdeSenderType.CDE_BACKCHANNEL));
            Assert.That(TheBaseAssets.MyScopeManager.GetSessionIDFromISB(escaped), Is.EqualTo(session.ToString()));
        }

        private static long[] Counters()
        {
            var counters = new long[1002];
            for (var i = 0; i < 1000; i++)
                counters[i] = i + 1;
            counters[1000] = 123456789;
            counters[1001] = 9_999_999_999;
            return counters;
        }
    }
}
