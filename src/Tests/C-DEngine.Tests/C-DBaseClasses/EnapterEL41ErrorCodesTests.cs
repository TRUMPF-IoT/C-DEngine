// SPDX-FileCopyrightText: Copyright (c) 2009-2024 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using NUnit.Framework;
using nsCDEngine.BaseClasses;
using System.Linq;

#if !CDE_NET35
namespace CDEngine.BaseClasses.Net45.Tests
#else
namespace CDEngine.BaseClasses.Net35.Tests
#endif
{
    [TestFixture]
    public class EnapterEL41ErrorCodesTests
    {
        [Test]
        public void ErrorCodeDescriptions_IsNotNull()
        {
            Assert.That(EnapterEL41ErrorCodes.ErrorCodeDescriptions, Is.Not.Null);
        }

        [Test]
        public void ErrorCodeDescriptions_IsReadOnly()
        {
            Assert.That(EnapterEL41ErrorCodes.ErrorCodeDescriptions, Is.InstanceOf<System.Collections.Generic.IReadOnlyDictionary<string, string>>());
        }

        [Test]
        public void GetDescription_WithNullCode_ReturnsNull()
        {
            var result = EnapterEL41ErrorCodes.GetDescription(null);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetDescription_WithEmptyCode_ReturnsNull()
        {
            var result = EnapterEL41ErrorCodes.GetDescription(string.Empty);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetDescription_WithNonExistentCode_ReturnsNull()
        {
            var result = EnapterEL41ErrorCodes.GetDescription("NONEXISTENT");
            Assert.That(result, Is.Null);
        }

        [Test]
        public void IsValidCode_WithNull_ReturnsFalse()
        {
            var result = EnapterEL41ErrorCodes.IsValidCode(null);
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsValidCode_WithEmptyString_ReturnsFalse()
        {
            var result = EnapterEL41ErrorCodes.IsValidCode(string.Empty);
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsValidCode_WithNonExistentCode_ReturnsFalse()
        {
            var result = EnapterEL41ErrorCodes.IsValidCode("NONEXISTENT");
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetAllCodes_ReturnsCollection()
        {
            var result = EnapterEL41ErrorCodes.GetAllCodes();
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void GetAllCodes_ReturnsKeysFromDictionary()
        {
            var allCodes = EnapterEL41ErrorCodes.GetAllCodes();
            var dictionaryKeys = EnapterEL41ErrorCodes.ErrorCodeDescriptions.Keys;
            
            Assert.That(allCodes.Count(), Is.EqualTo(dictionaryKeys.Count));
        }

        // Note: When actual error codes are added to the dictionary, add tests like:
        // [Test]
        // public void GetDescription_WithValidCode_ReturnsDescription()
        // {
        //     var result = EnapterEL41ErrorCodes.GetDescription("W1");
        //     Assert.That(result, Is.Not.Null);
        //     Assert.That(result, Is.Not.Empty);
        // }
        //
        // [Test]
        // public void IsValidCode_WithValidCode_ReturnsTrue()
        // {
        //     var result = EnapterEL41ErrorCodes.IsValidCode("W1");
        //     Assert.That(result, Is.True);
        // }
    }
}
