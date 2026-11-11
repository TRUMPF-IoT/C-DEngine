// SPDX-FileCopyrightText: Copyright (c) 2009-2026 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using NUnit.Framework;
using nsCDEngine.ISM;
using System.IO;

namespace CDEngine.ISM.Tests
{
    [TestFixture]
    public class TheCDESettingsTests
    {
        private static readonly string BaseDirectory = Path.Combine(Path.GetTempPath(), "cdeApp") + Path.DirectorySeparatorChar;
        private static readonly string DataDirectory = Path.Combine(Path.GetTempPath(), "cdeData") + Path.DirectorySeparatorChar;

        [Test]
        public void ResolveClientBinRootDirectory_UsesStartArgument()
        {
            var directory = TheCDESettings.ResolveClientBinRootDirectory(DataDirectory, BaseDirectory, BaseDirectory);

            Assert.That(directory, Is.EqualTo(DataDirectory));
        }

        [Test]
        public void ResolveClientBinRootDirectory_UsesDirectoryOfConstructorWithoutStartArgument()
        {
            var directory = TheCDESettings.ResolveClientBinRootDirectory(null, DataDirectory, BaseDirectory);

            Assert.That(directory, Is.EqualTo(DataDirectory));
        }

        [Test]
        public void ResolveClientBinRootDirectory_UsesBaseDirectoryWithoutStartArgumentAndConstructor()
        {
            var directory = TheCDESettings.ResolveClientBinRootDirectory("", null, BaseDirectory);

            Assert.That(directory, Is.EqualTo(BaseDirectory));
        }

        [Test]
        public void ResolveClientBinRootDirectory_ResolvesRelativePathAgainstBaseDirectory()
        {
            var directory = TheCDESettings.ResolveClientBinRootDirectory(Path.Combine("..", "cdeData"), null, BaseDirectory);

            Assert.That(directory, Is.EqualTo(DataDirectory));
        }

        [Test]
        public void ResolveClientBinRootDirectory_AppendsDirectorySeparator()
        {
            var directory = TheCDESettings.ResolveClientBinRootDirectory(Path.TrimEndingDirectorySeparator(DataDirectory), null, BaseDirectory);

            Assert.That(directory, Is.EqualTo(DataDirectory));
        }
    }
}
