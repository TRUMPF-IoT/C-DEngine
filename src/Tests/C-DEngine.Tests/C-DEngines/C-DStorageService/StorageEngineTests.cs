// SPDX-FileCopyrightText: Copyright (c) 2009-2024 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

using C_DEngine.Tests.TestCommon;
using nsCDEngine.Engines;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.ViewModels;

#if !CDE_NET35
namespace CDEngine.StorageService.Net45.Tests
#else
namespace CDEngine.StorageService.Net35.Tests
#endif
{
    [TestFixture]
    public class StorageEngineTests : TestHost
    {
        #region PROPERTIES

        #endregion

        #region METHODS

        [Test]
        [Description("This test simply asserts whether or not an instance of TheStorageMirror can be created.")]
        [Category("Build")]
        public void InstantiateTheStorageMirrorTest()
        {
            #region ASSEMBLE


            TheStorageMirror<TheStorageEngineTSM> mirror;

            #endregion

            #region ACT

            mirror = new TheStorageMirror<TheStorageEngineTSM>(TheCDEngines.MyIStorageService)
            {
                IsRAMStore = true,
                CacheStoreInterval = 1,
                IsStoreIntervalInSeconds = true,
                IsCachePersistent = true,
                UseSafeSave = true,
                AllowFireUpdates = true,
            };

            #endregion

            #region ASSERT

            Assert.That(mirror, Is.Not.EqualTo(null));
            mirror?.Dispose();
            mirror = null;

            #endregion
        }

        [Test]
        [Description("This test simply asserts whether or not an instance of TheMirrorCache can be created.")]
        [Category("Build")]
        public void InstantiateTheMirrorCacheTest()
        {
            #region ASSEMBLE

            TheMirrorCache<TheStorageEngineTSM> mirror;

            #endregion

            #region ACT

            mirror = new TheMirrorCache<TheStorageEngineTSM>(10)
            {
                CacheStoreInterval = 1,
                IsStoreIntervalInSeconds = true,
                IsCachePersistent = true,
                UseSafeSave = true,
                AllowFireUpdates = true,
            };

            #endregion

            #region ASSERT

            Assert.That(mirror, Is.Not.EqualTo(null));
            mirror?.Dispose();
            mirror = null;

            #endregion
        }

        [Test]
        [Description("This test creates a collection of TSMs and inserts it into an instance of TheStorageMirror.")]
        [Category("Build")]
        public void AddItemsToTheStorageMirrorTest()
        {
            #region ASSEMBLE

            TheStorageMirror<TheStorageEngineTSM>.StoreResponse response = null;
            int totalCandidates = 5000;
            var random = new Random();
            var data = Enumerable.Range(1, totalCandidates).OrderBy(i => random.Next(1, totalCandidates));
            ManualResetEventSlim gate = new ManualResetEventSlim();
            TheStorageMirror<TheStorageEngineTSM> mirror;
            List<TheStorageEngineTSM> TSMs = new List<TheStorageEngineTSM>();


            // Build the collection of TSMs
            foreach (var payload in data)
            {
                TSMs.Add(new TheStorageEngineTSM() { TXTPattern = payload.ToString() });
            }

            // Spin up your mirror
            mirror = new TheStorageMirror<TheStorageEngineTSM>(TheCDEngines.MyIStorageService)
            {
                IsRAMStore = true,
                CacheStoreInterval = 1,
                IsStoreIntervalInSeconds = true,
                IsCachePersistent = true,
                UseSafeSave = true,
                AllowFireUpdates = true,
            };
            mirror.RegisterEvent(eStoreEvents.StoreReady, e => { gate.Set(); });
            mirror.InitializeStore(true);

            // Wait for mirror to initialize...
            gate.Wait(30000);

            #endregion

            #region ACT

            // Add your items
            Task.Factory.StartNew(() =>
            {
                mirror.AddItems(TSMs, payload =>
                {
                    response = payload;
                    gate.Set();
                });
            });

            //Wait for response
            gate.Reset();
            gate.Wait(30000);

            mirror?.Dispose();

            #endregion

            #region ASSERT

            Assert.That(response.HasErrors, Is.False);

            #endregion
        }

        [Test]
        [Description("This test creates, inserts, and reads a collection of TSMs from an instance of TheStorageMirror.")]
        [Category("Build")]
        public void GetRecordsFromTheStorageMirrorTest(
            [Values(0, 500, 5000)]int maxCount,
            [Values(50, 5000)]int totalCandidates
        )
        {
            #region ASSEMBLE

            TheStorageMirror<TheStorageEngineTSM>.StoreResponse response = null;
            var random = new Random();
            var data = Enumerable.Range(1, totalCandidates).OrderBy(i => random.Next(1, totalCandidates));
            ManualResetEventSlim gate = new ManualResetEventSlim();
            TheStorageMirror<TheStorageEngineTSM> mirror;
            List<TheStorageEngineTSM> TSMs = new List<TheStorageEngineTSM>();

            // Build the collection of TSMs
            foreach (var payload in data)
            {
                TSMs.Add(new TheStorageEngineTSM()
                {
                    cdeMID = Guid.NewGuid(),
                    TXTPattern = payload.ToString()
                });
            }

            // Spin up your mirror
            mirror = new TheStorageMirror<TheStorageEngineTSM>(TheCDEngines.MyIStorageService)
            {
                IsRAMStore = true,
                CacheStoreInterval = 1,
                IsStoreIntervalInSeconds = true,
                IsCachePersistent = true,
                UseSafeSave = true,
                AllowFireUpdates = true,
            };
            if (maxCount > 0)
            {
                mirror.SetMaxStoreSize(maxCount);
            }
            mirror.RegisterEvent(eStoreEvents.StoreReady, e => { gate.Set(); });
            mirror.InitializeStore(true);

            // Wait for mirror to initialize...
            gate.Wait(30000);

            // Add your items
            Task.Factory.StartNew(() =>
            {
                mirror.AddItems(TSMs, payload =>
                {
                    response = payload;
                    gate.Set();
                });
            });

            //Wait for response
            gate.Reset();
            gate.Wait(30000);
            if ((response != null) && response.HasErrors) Assert.Fail($"Unable to add test collection items! Reason: {response.ErrorMsg}");

            #endregion

            #region ACT

            // Attempt to retrieve your items
            Task.Factory.StartNew(() =>
            {
                mirror.GetRecords(payload =>
                {
                    response = payload;
                    gate.Set();
                },
                true);
            });

            // Wait for response
            gate.Reset();
            gate.Wait(30000);
            if ((response != null) && response.HasErrors) Assert.Fail($"Unable to retrieve items! Reason: {response.ErrorMsg}");

            mirror?.Dispose();

            #endregion

            #region ASSERT

            var expectedCount = maxCount == 0 ? totalCandidates : Math.Min(maxCount, totalCandidates);

            Assert.That(response.MyRecords.Count, Is.EqualTo(expectedCount), "Not all test records were not added successfully.");

            #endregion
        }

        [Test]
        [Description("This test creates and inserts a collection of TSMs from an instance of TheStorageMirror, then deletes a specific item by ID.")]
        [Category("Build")]
        public void RemoveAnItemByIDFromTheStorageMirrorTest()
        {
            #region ASSEMBLE

            TheStorageMirror<TheStorageEngineTSM>.StoreResponse response = null;
            int totalCandidates = 5000;
            int indexMiddle = totalCandidates / 2;
            int indexCurrent = 0;
            var random = new Random();
            var data = Enumerable.Range(1, totalCandidates).OrderBy(i => random.Next(1, totalCandidates));
            ManualResetEventSlim gate = new ManualResetEventSlim();
            TheStorageMirror<TheStorageEngineTSM> mirror;
            TheStorageEngineTSM tsmCurrent = null;
            TheStorageEngineTSM tsmMiddle = null;
            TheStorageEngineTSM tsmMatch = null;
            List<TheStorageEngineTSM> TSMs = new List<TheStorageEngineTSM>();
            List<TheStorageEngineTSM> myRecords = new List<TheStorageEngineTSM>();

            // Build the collection of TSMs and cache the middle one
            foreach (var payload in data)
            {
                tsmCurrent = new TheStorageEngineTSM()
                {
                    cdeMID = Guid.NewGuid(),
                    TXTPattern = payload.ToString()
                };
                TSMs.Add(tsmCurrent);
                if ((indexCurrent++ >= indexMiddle) && (tsmMiddle == null))
                {
                    tsmMiddle = tsmCurrent;
                }
            }
            if (tsmMiddle == null) Assert.Fail("Unable to cache the middle TSM!");

            // Spin up your mirror
            mirror = new TheStorageMirror<TheStorageEngineTSM>(TheCDEngines.MyIStorageService)
            {
                IsRAMStore = true,
                CacheStoreInterval = 1,
                IsStoreIntervalInSeconds = true,
                IsCachePersistent = true,
                UseSafeSave = true,
                AllowFireUpdates = true,
            };
            mirror.RegisterEvent(eStoreEvents.StoreReady, e => { gate.Set(); });
            mirror.InitializeStore(true);

            // Wait for mirror to initialize...
            gate.Wait(30000);

            // Add your items
            Task.Factory.StartNew(() =>
            {
                mirror.AddItems(TSMs, payload =>
                {
                    response = payload;
                    gate.Set();
                });
            });

            //Wait for response
            gate.Reset();
            gate.Wait(30000);
            if ((response != null) && response.HasErrors) Assert.Fail($"Unable to add test collection items! Reason: {response.ErrorMsg}");

            #endregion

            #region ACT

            // Attempt to remove your middle item
            Task.Factory.StartNew(() =>
            {
                mirror.RemoveAnItemByID(tsmMiddle.cdeMID, payload =>
                {
                    response = payload;
                    gate.Set();
                });
            });

            // Wait for response
            gate.Reset();
            gate.Wait(30000);
            if ((response != null) && response.HasErrors) Assert.Fail($"Unable to add test collection items! Reason: {response.ErrorMsg}");

            // Attempt to retrieve your middle item
            tsmMatch = mirror.GetEntryByID(tsmMiddle.cdeMID);

            mirror?.Dispose();

            #endregion

            #region ASSERT

            Assert.That(tsmMatch, Is.EqualTo(null));

            #endregion
        }

        [Test]
        [Description("This test creates a collection of TSMs and inserts it into an instance of TheMirrorCache.")]
        [Category("Build")]
        public void AddItemsToTheMirrorCacheTest()
        {
            #region ASSEMBLE

            int totalCandidates = 5000;
            int totalInserts = 0;
            var random = new Random();
            var data = Enumerable.Range(1, totalCandidates).OrderBy(i => random.Next(1, totalCandidates));
            CountdownEvent countdown = new CountdownEvent(1);
            TheMirrorCache<TheStorageEngineTSM> mirror;
            List<TheStorageEngineTSM> TSMs = new List<TheStorageEngineTSM>();

            // Build the collection of TSMs
            foreach (var payload in data)
            {
                TSMs.Add(new TheStorageEngineTSM() { TXTPattern = payload.ToString() });
            }

            // Spin up your mirror
            mirror = new TheMirrorCache<TheStorageEngineTSM>(10)
            {
                CacheStoreInterval = 1,
                IsStoreIntervalInSeconds = true,
                IsCachePersistent = true,
                UseSafeSave = true,
                AllowFireUpdates = true,
            };

            #endregion

            #region ACT

            mirror.AddItems(TSMs, response =>
            {
                totalInserts = response.Count();
                countdown.Signal();
            });

            countdown.Wait();
            countdown?.Dispose();
            mirror?.Dispose();

            #endregion

            #region ASSERT

            Assert.That(totalCandidates, Is.EqualTo(totalInserts));

            #endregion
        }

        [Test]
        [Description("This test creates, inserts, and reads a collection of TSMs from an instance of TheMirrorCache.")]
        [Category("Build")]
        public void GetRecordsFromTheMirrorCache(
            [Values(0, 500, 5000)]int maxCount,
            [Values(50, 5000)]int totalCandidates
            )
        {
            #region ASSEMBLE
            bool entryNotFound = false;
            var random = new Random();
            var data = Enumerable.Range(1, totalCandidates).OrderBy(i => random.Next(1, totalCandidates));
            ManualResetEventSlim gate = new ManualResetEventSlim();
            CountdownEvent countdown = new CountdownEvent(1);
            TheMirrorCache<TheStorageEngineTSM> mirror;
            List<TheStorageEngineTSM> TSMs = new List<TheStorageEngineTSM>();
            List<TheStorageEngineTSM> myRecords = new List<TheStorageEngineTSM>();

            // Build the collection of TSMs
            var baseTime = DateTimeOffset.Now;
            int counter = 0;
            foreach (var payload in data)
            {
                TSMs.Add(new TheStorageEngineTSM()
                {
                    cdeMID = Guid.NewGuid(),
                    TXTPattern = payload.ToString(),
                    cdeCTIM = baseTime.AddTicks(counter++),
                });
            }

            // Spin up your mirror
            mirror = new TheMirrorCache<TheStorageEngineTSM>(10)
            {
                CacheStoreInterval = 1,
                IsStoreIntervalInSeconds = true,
                IsCachePersistent = true,
                UseSafeSave = true,
                AllowFireUpdates = true,
            };
            if (maxCount != 0)
            {
                mirror.SetMaxStoreSize(maxCount);
            }

            // Add your items...
            mirror.AddItems(TSMs, response =>
            {
                myRecords = response;
                countdown.Signal();
            });

            countdown.Wait();

            Assert.That(myRecords.Count, Is.EqualTo(totalCandidates), "Not all test records were added successfully.");

            #endregion

            #region ACT

            var expectedCount = maxCount == 0 ? totalCandidates : Math.Min(maxCount, totalCandidates);
            Assert.That(mirror.Count, Is.EqualTo(expectedCount), "Not all test records were not added successfully.");

            // Retrieve your items...
            counter = 0;
            foreach (var tsm in TSMs)
            {
                TheStorageEngineTSM match = mirror.GetEntryByID(tsm.cdeMID);
                if (counter >= TSMs.Count - expectedCount)
                {
                    if (match == null)
                    {
                        entryNotFound = true;
                        break;
                    }
                }
                else
                {
                    if (match != null)
                    {
                        Assert.That(match, Is.Null, $"Item found that was supposed to have been removed due to max count limit");
                    }
                }
                counter++;
            }

            mirror?.Dispose();

            #endregion

            #region ASSERT

            Assert.That(entryNotFound, Is.False, "TheMirrorCache was missing one or more test entries!");

            #endregion
        }



        [Test]
        [Description("This test creates and inserts a collection of TSMs from an instance of TheMirrorCache, then deletes a specific item by ID.")]
        [Category("Build")]
        public void RemoveAnItemByKeyFromTheMirrorCacheTest()
        {
            #region ASSEMBLE

            int totalCandidates = 5000;
            int indexMiddle = totalCandidates / 2;
            int indexCurrent = 0;
            var random = new Random();
            var data = Enumerable.Range(1, totalCandidates).OrderBy(i => random.Next(1, totalCandidates));
            ManualResetEventSlim gate = new ManualResetEventSlim();
            CountdownEvent countdown = new CountdownEvent(1);
            TheMirrorCache<TheStorageEngineTSM> mirror;
            TheStorageEngineTSM tsmCurrent = null;
            TheStorageEngineTSM tsmMiddle = null;
            TheStorageEngineTSM tsmRemoved = null;
            TheStorageEngineTSM tsmMatch = null;
            List<TheStorageEngineTSM> TSMs = new List<TheStorageEngineTSM>();
            List<TheStorageEngineTSM> myRecords = new List<TheStorageEngineTSM>();

            // Build the collection of TSMs and cache the middle one
            foreach (var payload in data)
            {
                tsmCurrent = new TheStorageEngineTSM()
                {
                    cdeMID = Guid.NewGuid(),
                    TXTPattern = payload.ToString()
                };
                TSMs.Add(tsmCurrent);
                if ((indexCurrent++ >= indexMiddle) && (tsmMiddle == null))
                {
                    tsmMiddle = tsmCurrent;
                }
            }
            if (tsmMiddle == null) Assert.Fail("Unable to cache the middle TSM!");

            // Spin up your mirror
            mirror = new TheMirrorCache<TheStorageEngineTSM>(10)
            {
                CacheStoreInterval = 1,
                IsStoreIntervalInSeconds = true,
                IsCachePersistent = true,
                UseSafeSave = true,
                AllowFireUpdates = true,
            };

            // Add your items...
            mirror.AddItems(TSMs, response =>
            {
                myRecords = response;
                countdown.Signal();
            });

            countdown.Wait();
            if (TSMs.Count != myRecords.Count) Assert.Fail("Not all test records were not added successfully.");

            #endregion

            #region ACT

            // Attempt to remove your middle item
            Task.Factory.StartNew(() =>
            {
                mirror.RemoveAnItemByKey(tsmMiddle.cdeMID.ToString(), payload =>
                {
                    tsmRemoved = payload;
                    gate.Set();
                });
            });

            // Wait for response
            gate.Reset();
            gate.Wait(30000);
            if (tsmRemoved == null) Assert.Fail("Unable to remove item by ID!");

            // Attempt to retrieve your middle item
            tsmMatch = mirror.GetEntryByID(tsmMiddle.cdeMID);

            mirror?.Dispose();

            #endregion

            #region ASSERT

            Assert.That(tsmMatch, Is.EqualTo(null));

            #endregion
        }

        private string buildSummary(int totalCandidates, long elaspedTime, float avgCPU, float maxCPU, float avgRAM, float maxRAM)
        {
            string summary = string.Empty;
            try
            {
                summary = Environment.NewLine +
                    $"Total candidates = {totalCandidates}" +
                    Environment.NewLine +
                    $"Duration(ms) = {elaspedTime.ToString()}" +
                    Environment.NewLine +
                    $"Avg CPU(%) = {avgCPU.ToString("n2")} (max = {maxCPU.ToString("n2")})" +
                    Environment.NewLine +
                    $"Avg RAM(MB) = {avgRAM.ToString("n2")} (max = {maxRAM.ToString("n2")})";
            }
            catch (Exception)
            {
                summary = "Summary could not be built!";
            }
            return summary;
        }

        [SetUp]
        public void InitTests()
        {
            StartHost();
        }

        [TearDown]
        public static void ShutdownHost()
        {
            StopHost();
        }

        #endregion

    }

    #region HELPER CLASSES

    internal class TheStorageEngineTSM : TheDataBase
    {
        public TheStorageEngineTSM() : base() { }

        public string SourceEngineName { get; set; }
        public string TargetEngineName { get; set; }
        public string TXTPattern { get; set; }
        public bool Disable { get; set; }
        public bool SerializeTSM { get; set; }

        // TODO Factor these out into a TheMQTTSenderTSM or TheMSBSenderTSM classes
        public bool SendAsFile { get; set; }
        public string MQTTTopicTemplate { get; set; }

        // override if new properties in a derived class need to be considered in comparisons
        internal virtual bool IsEqual(TheStorageEngineTSM senderThingToAdd)
        {
            return
                senderThingToAdd != null
                && cdeMID == senderThingToAdd.cdeMID
                && Disable == senderThingToAdd.Disable
                && SourceEngineName == senderThingToAdd.SourceEngineName
                && TargetEngineName == senderThingToAdd.TargetEngineName
                && SerializeTSM == senderThingToAdd.SerializeTSM
                && SendAsFile == senderThingToAdd.SendAsFile
                && MQTTTopicTemplate == senderThingToAdd.MQTTTopicTemplate
                && TXTPattern == senderThingToAdd.TXTPattern
                ;
        }

        // Override this if you add new properties to a derivedclass that need to be initialized from a TheTSMToPublish
        internal virtual void Initialize(TheTSMToPublish senderTSMToAdd)
        {
            cdeMID = senderTSMToAdd.cdeMID;
            SourceEngineName = senderTSMToAdd.SourceEngineName;
            TargetEngineName = senderTSMToAdd.TargetEngineName;
            TXTPattern = senderTSMToAdd.TXTPattern;
            SerializeTSM = senderTSMToAdd.SerializeTSM;
            SendAsFile = senderTSMToAdd.SendAsFile;
            MQTTTopicTemplate = senderTSMToAdd.MQTTTopicTemplate;
            Disable = false;
        }

    }

    internal class TheTSMToPublish
    {
        public Guid cdeMID { get; set; }
        public string SourceEngineName { get; set; }
        public string TargetEngineName { get; set; }
        public string TXTPattern { get; set; }
        public bool SerializeTSM { get; set; }
        public bool SendAsFile { get; set; }
        public string MQTTTopicTemplate { get; set; }
    }

    public class Parameters
    {
        public Parameters() { }
        public int TotalCandidates { get; set; }
        public float MaxCPU { get; set; }
        public float MaxRAM { get; set; }
    }

    #endregion
}
