// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using NUnit.Framework;

using System;
using nsCDEngine.Engines.ThingService;
using C_DEngine.Tests.TestCommon;
using DataLogUtilities;
using System.Collections.Generic;
using System.Collections.Concurrent;
using nsCDEngine.BaseClasses;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
using System.Threading;

#if !CDE_NET35
namespace CDEngine.ThingService.Net45.Tests
#else
namespace CDEngine.ThingService.Net35.Tests
#endif
{
    [TestFixture]
    public class HistorianTests : TestHost
    {
        ConcurrentQueue<HistoryEntry> HistoryInputEntries { get; set; }

        [SetUp]
        public void InitTests()
        {
            StartHost();
            HistoryInputEntries = new ConcurrentQueue<HistoryEntry>();
        }

        [TearDown]
        public void ShutdownHost()
        {
            StopHost();
        }

        [Test]
        public void TestHistorian(
            [Values(0,60)] int samplingWindowMs,
            [Values(1000)] int cooldownPeriodMs,
#if !ALLTESTS // TODO find better way to do this (test category etc.)
            [Values(2)]int iterations,
            [Values(3)]int itemsPerIteration,
            [Values(true)] bool concurrent,
#else
            [Values(2, 10)]int iterations,
            [Values(3, 100)]int itemsPerIteration,
            [Values(false, true)] bool concurrent,
#endif
            [Values(false, true)] bool reportAllProps)
        {
            var propValueSenderList = new List<InputPropertySender>
            {
                PropValueSender,
            };

            TestHistorian(TimeSpan.FromMilliseconds(samplingWindowMs), TimeSpan.FromMilliseconds(cooldownPeriodMs), propValueSenderList, propValueSenderList, null, iterations, itemsPerIteration, concurrent, false, true, false, false, false, true, reportAllProps).Wait();
        }

        [Test]
        public void TestHistorianWithAdditionalProps(
            [Values(0, 60)] int samplingWindowMs,
            [Values(1000)] int cooldownPeriodMs,
#if !ALLTESTS
            [Values(2)]int iterations,
            [Values(3)]int itemsPerIteration,
            [Values(true)] bool concurrent,
#else
            [Values(2, 10)]int iterations,
            [Values(3, 100)]int itemsPerIteration,
            [Values(false, true)] bool concurrent,
#endif
            [Values(false, true)] bool reportAllProps)
        {
            var propValueSenderList = new List<InputPropertySender>
            {
                PropValueSender,
            };
            TestHistorian(TimeSpan.FromMilliseconds(samplingWindowMs), TimeSpan.FromMilliseconds(cooldownPeriodMs), propValueSenderList, propValueSenderList, new List<InputPropertySender> { AdditionalPropSender }, iterations, itemsPerIteration, concurrent, false, true, false, false, false, true, reportAllProps).Wait();
        }

        Task PropValueSender(TheThing tThing, HashSet<string> propertyNames, bool addToExpectedOutput, bool addToPropNames, int itemsToSend, int iteration, DateTimeOffset baseTime, TimeSpan timeBetweenIterations)
        {
            var propTime = baseTime.AddTicks(iteration * timeBetweenIterations.Ticks);
            WriteLine($"Iteration {iteration}: Timestamp {propTime:O}. Base Time: {baseTime:O}");
            for (int j = 1; j <= itemsToSend; j++)
            {
                var name = $"Prop{j:D3}";
                var value = $"Value{j:D3}";
                //if (addToExpectedOutput)
                {
                    AddProperty(tThing, name, $"{value}.{iteration}", propTime, timeBetweenIterations, addToExpectedOutput);
                }
                //else
                //{
                    if (addToPropNames)
                    {
                        propertyNames.Add(name);
                //        tThing.SetProperty(name, value, propTime);
                    }
                //    else
                //    {
                //        tThing.SetProperty(name, $"{value}.{iteration}", propTime);
                //    }
                //}
            }
            return TheCommonUtils.TaskFromResult(true);
        }


        static int additionalCounter = 0;
        /// <summary>
        /// Not currently being used. Work in progress.
        /// </summary>
        /// <param name="tThing"></param>
        /// <param name="propertyNames"></param>
        /// <param name="addToExpectedOutput"></param>
        /// <param name="itemsToSend"></param>
        /// <param name="iteration"></param>
        /// <param name="baseTime"></param>
        /// <param name="timeBetweenIterations"></param>
        /// <returns></returns>
        Task AdditionalPropSender(TheThing tThing, HashSet<string> propertyNames, bool addToExpectedOutput, bool addToPropNames, int itemsToSend, int iteration, DateTimeOffset baseTime, TimeSpan timeBetweenIterations)
        {
            var propTime = baseTime.AddTicks(iteration * timeBetweenIterations.Ticks);
            WriteLine($"Iteration {iteration}: Timestamp {propTime:O}. Base Time: {baseTime:O}");
            for (int j = 1; j <= itemsToSend; j++)
            {

                var guid = Interlocked.Increment(ref additionalCounter); //Guid.NewGuid();
                var name = $"Prop{j:D3}_{guid}";
                var value = $"Value{j:D3}_{guid}";
                //if (addToExpectedOutput)
                {
                    AddProperty(tThing, name, $"{value}.{iteration}", propTime, timeBetweenIterations, addToExpectedOutput);
                }
                //else
                //{
                    if (addToPropNames)
                    {
                        propertyNames.Add(name);
                //        tThing.SetProperty(name, value, propTime);
                    }
                //    else
                //    {
                //        tThing.SetProperty(name, $"{value}.{iteration}", propTime);
                //    }
                //}
            }
            return TheCommonUtils.TaskFromResult(true);
        }

        [Test]
        [Ignore("Test not stable")]
        public void TestHistorianOPCData()
        {
            var opcDataSenderList = new List<InputPropertySender>
            {
                OpcDataSender,
            };
            TestHistorian(TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(1000), opcDataSenderList, opcDataSenderList, null, 1, 1, false, false, true, false, false, false, true, false).Wait();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task OpcDataSender(TheThing tThing, HashSet<string> propertyNames, bool addToExpectedOutput, bool addToPropNames, int itemsToSend, int iteration, DateTimeOffset baseTime, TimeSpan minWaitBetweenProps)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            DateTimeOffset lastTimeStamp = DateTimeOffset.MaxValue;
            var opcDataItems = DataLogParser.ReadOpcClientDataLog(Path.Combine(TestContext.CurrentContext.TestDirectory, @"C-DEngines\C-DThingService\TestData\opcclientdata.log"));
            foreach (var opcEntry in opcDataItems)
            {
                if (opcEntry.TagId == "integer Array")
                {
                    opcEntry.value = "some array";
                }
                //if (addToExpectedOutput)
                {
                    // Shorten times for logs from real machines so they don't take forever (manual test)
                    //if (opcEntry.PropertyTime > lastTimeStamp)
                    //{
                    //    var delay = (int) (opcEntry.PropertyTime - lastTimeStamp).TotalMilliseconds;
                    //    if (delay > 600)
                    //    {
                    //        delay = 600;
                    //    }
                    //    await TheCommonUtils.TaskDelayOneEye(delay, 50);
                    //}
                    //lastTimeStamp = opcEntry.PropertyTime;
                    AddProperty(tThing, opcEntry.TagId, opcEntry.value, opcEntry.PropertyTime, minWaitBetweenProps, addToExpectedOutput);
                }
                //else
                //{
                //    if (addToPropNames)
                //    {
                        if (!propertyNames.Contains(opcEntry.TagId))
                        {
                            propertyNames.Add(opcEntry.TagId);
                        }
                //    }
                //}
            }
        }

        internal delegate Task InputPropertySender(TheThing tThing, HashSet<string> propertyNames, bool addToExpectedOutput, bool addToPropNames, int itemsToSend, int iteration, DateTimeOffset baseTime, TimeSpan minWaitBetweenProps);

        internal async Task TestHistorian(TimeSpan samplingWindow, TimeSpan cooldownPeriod, List<InputPropertySender> inputsBefore, List<InputPropertySender> inputsDuring, List<InputPropertySender> inputsBetween, int iterations, int itemsPerIteration, bool concurrent, bool reportUnchanged1, bool reportUnchanged2, bool reportInitialValues1, bool reportInitialValues2, bool reportAggregates1, bool reportAggregates2, bool reportAllProps)
        {
            additionalCounter = 0;
            var testThing = new TheThing();
            var tThing = TheThingRegistry.RegisterThing(testThing);
            Assert.IsNotNull(tThing);

            // Track the properties for which results are to be retrieved
            var props = new HashSet<string>();

            var baseTime = DateTimeOffset.Now;

            {
                var tempThing = testThing; // new TheThing();
                foreach (var input in inputsBefore)
                {
                    // Prime the thing with properties and values (optional), capture the properties to be retrieved and verified
                    await input(tempThing, props, false, true, itemsPerIteration, 0, baseTime, TimeSpan.Zero);
                }
            }
            //var aggregationWindow = new TimeSpan(0, 0, 0, 0, 0);
            //var cooldownPeriod = new TimeSpan(0, 0, 1);

            TimeSpan timeBetweenIterations = samplingWindow == TimeSpan.Zero ? new TimeSpan(1) : samplingWindow;

            Guid token;
            Guid token2;

            var props2 = props.Union(props.Select(p => $"[{p}].[N]")).Union(props.Select(p => $"[{p}].[Avg]")).Union(props.Select(p => $"[{p}].[Min]")).Union(props.Select(p => $"[{p}].[Max]")).ToList();
            var historyParams = new TheHistoryParameters
            {
                SamplingWindow = samplingWindow,
                ReportUnchangedProperties = reportUnchanged1,
                ReportInitialValues = reportInitialValues1,
                Persistent = true,
                MaintainHistoryStore = false,
                CooldownPeriod = cooldownPeriod,
                Properties = reportAllProps ? null : props.ToList(),
            };
            token = tThing.RegisterForUpdateHistory(historyParams);
            // Testing unregister and re-register (common pattern for Axoom IoT Sender)
            tThing.UnregisterUpdateHistory(token);
            token = tThing.RegisterForUpdateHistory(historyParams);

            var historyParams2 = new TheHistoryParameters
            {
                // TODO make a test for aggregation
                Properties = reportAllProps ? null : props2,
                SamplingWindow = samplingWindow,
                ReportUnchangedProperties = reportUnchanged2,
                ReportInitialValues = reportInitialValues2,
                Persistent = true,
                MaintainHistoryStore = true,
                ComputeAvg = reportAggregates2,
                ComputeN = reportAggregates2,
                ComputeMax = reportAggregates2,
                ComputeMin = reportAggregates2,
                CooldownPeriod = cooldownPeriod,
            };
            token2 = tThing.RegisterForUpdateHistory(historyParams2);
            Assert.AreNotEqual(Guid.Empty, token);
            await TheCommonUtils.TaskDelayOneEye(((int)cooldownPeriod.TotalMilliseconds + 15), 50); // Wait for cooldown period
            {
                var pendingSnapshotCount = (tThing.Historian as nsCDEngine.TheStorageMirrorHistorian).TestHookGetPendingSnapShotCount();
                int retryCount = 0;
                while (pendingSnapshotCount > 0 && retryCount < 10)
                {
                    WriteLine($"Pending Snapshots: {pendingSnapshotCount}. Waiting 500ms. {retryCount}");
                    await TheCommonUtils.TaskDelayOneEye(500, 50);
                    retryCount++;
                    pendingSnapshotCount = (tThing.Historian as nsCDEngine.TheStorageMirrorHistorian).TestHookGetPendingSnapShotCount();
                }
            }
            var history = tThing.GetThingHistory(token, itemsPerIteration + 50, false);
            var initialPropCount = history.Aggregate(0, (s, item) => s + item.PB.Count);
            if (reportInitialValues1)
            {
                Assert.IsTrue(itemsPerIteration < initialPropCount); // TODO check the exact number and items
            }
            else
            {
                Assert.AreEqual(0, initialPropCount);
            }

            //history = tThing.GetThingHistory(token, 25, false);
            //Assert.AreEqual(1, history.Count); // BUG: When a property does not already exist, the snapshot is not properly picked up (cdeP is not yet added to the property bag)

            bool bInputsDone = false;

            baseTime = baseTime.Add(timeBetweenIterations);
            var inputTask = TheCommonUtils.cdeRunTaskChainAsync("", async o =>
            {
                async Task iteration(int i, List<InputPropertySender> inputsForIteration, int itemCount, bool reportAllPropsForIteration, bool reportDoubleProps)
                {
                    WriteLine($"Starting iteration {i}");
                    foreach (var input in inputsForIteration)
                    {
                        if (samplingWindow > TimeSpan.Zero)
                        {
                            await input(tThing, props, reportAllPropsForIteration && reportDoubleProps, reportDoubleProps, itemCount, i, baseTime, timeBetweenIterations);  // These should be ignored
                            await input(tThing, props, reportAllPropsForIteration, true, itemCount, i, baseTime.AddTicks(1), timeBetweenIterations);
                        }
                        else
                        {
                            await input(tThing, props, reportAllPropsForIteration, true, itemCount, i, baseTime, timeBetweenIterations);
                        }
                    }
                    WriteLine($"Done with iteration {i}");
                }
                var tasks = new List<Task>();
                for (int i = 1; i <= iterations; i++)
                {
                    if (concurrent)
                    {
                        int it = i;
                        tasks.Add(Task.Factory.StartNew(async () => await iteration(it, inputsDuring, itemsPerIteration, true, false)));
                    }
                    else
                    {
                        await iteration(i, inputsDuring, itemsPerIteration, true, false);
                    }
                    if (true && inputsBetween != null) // test not stable yet
                    {
                        WriteLine($"Starting between iteration {i}");
                        if (concurrent)
                        {
                            int it = i;
                            tasks.Add(Task.Factory.StartNew(async () => await iteration(it, inputsBetween, itemsPerIteration, reportAllProps, true)));
                        }
                        else
                        {
                            await iteration(i, inputsBetween, itemsPerIteration, reportAllProps, true);
                        }
                        WriteLine($"Done with between iteration {i}");
                        //if (inputsBetween != null)
                        //{
                        //    WriteLine($"Starting between iteration {i}");
                        //    foreach (var input in inputsBetween)
                        //    {
                        //        await input(tThing, props, reportAllProps, true, 1, i, baseTime, timeBetweenIterations);
                        //    }
                        //    WriteLine($"Done with between iteration {i}");
                        //}
                    }
                }
                if (concurrent)
                {
                    await TheCommonUtils.TaskWhenAll(tasks);
                }

                {
                    var pendingSnapshotCount = (tThing.Historian as nsCDEngine.TheStorageMirrorHistorian).TestHookGetPendingSnapShotCount();
                    //int retryCount = 0;
                    //while (pendingSnapshotCount > 0 && retryCount < 10)
                    //{
                    //    WriteLine($"Pending Snapshots: {pendingSnapshotCount }. Waiting 100ms. {retryCount}");
                    //    await TheCommonUtils.TaskDelayOneEye(100, 50);
                    //    retryCount++;
                    //    pendingSnapshotCount = (tThing.Historian as nsCDEngine.TheStorageMirrorHistorian).TestHookGetPendingSnapShotCount();
                    //}
                    WriteLine($"Done with inputs. Pending Snapshots: {pendingSnapshotCount}");
                }
                bInputsDone = true;
            });

            //while (!bInputsDone)
            //{
            //    await TheCommonUtils.TaskDelayOneEye(100, 50);
            //    WriteLine($"Waiting for inputs. History Items: {(tThing.Historian as nsCDEngine.TheStorageMirrorHistorian).TestHookGetHistoryItemCount()}. Pending Snapshots: {(tThing.Historian as nsCDEngine.TheStorageMirrorHistorian).TestHookGetPendingSnapShotCount()}");
            //}

            Random r = new Random((int)DateTime.Now.Ticks);
            WriteLine($"Starting with result loop. Pending Snapshots: {(tThing.Historian as nsCDEngine.TheStorageMirrorHistorian).TestHookGetPendingSnapShotCount()}");

            int duplicates = 0;
            int noItemCount = 0;
            int propCount = 0;
            var reportedEntries = new HashSet<HistoryEntry>();
            TimeSpan waitTime = new TimeSpan(0, 0, 0, 0, 100);
            TheHistoryResponse historyResponse = null;
            {
                int pendingSnapshotCount;
                long historyQueueItemCount;
                bool bInputsDoneBefore;
                do
                {
                    bInputsDoneBefore = bInputsDone;
                    historyResponse = await tThing.GetThingHistoryAsync(token, 25, 1, waitTime, null, false);

                    history = historyResponse.HistoryItems;
                    if (history.Count == 0)
                    {
                        noItemCount++;
                        //TheCommonUtils.TaskDelayOneEye(!bInputsDone ? 500 : 100, 100).Wait();
                    }

                    foreach (var reportedItem in history)
                    {
                        foreach (var prop in reportedItem.PB.OrderBy(p => p.Key))
                        {
                            propCount++;
                            var reportedEntry = new HistoryEntry { Name = prop.Key, Value = prop.Value, Time = reportedItem.cdeCTIM, InOutput = true };
                            if (!reportedEntries.Contains(reportedEntry))
                            {
                                reportedEntries.Add(reportedEntry);
                            }
                            else
                            {
                                duplicates++;
                            }
                        }
                    }
                    if (r.Next(4) == 5)
                    {
                        tThing.RestartUpdateHistory(token);
                        WriteLine($"Restarted last history read. Inputs Done: {bInputsDone}Items read: {history.Count}. History Items: {(tThing.Historian as nsCDEngine.TheStorageMirrorHistorian).TestHookGetHistoryItemCount()}. Pending Snapshots: {(tThing.Historian as nsCDEngine.TheStorageMirrorHistorian).TestHookGetPendingSnapShotCount()}");
                    }
                    else
                    {
                        tThing.ClearUpdateHistory(token);
                    }
                    pendingSnapshotCount = (tThing.Historian as nsCDEngine.TheStorageMirrorHistorian).TestHookGetPendingSnapShotCount();
                    historyQueueItemCount = (tThing.Historian as nsCDEngine.TheStorageMirrorHistorian).TestHookGetHistoryItemCount();
                    WriteLine($"Reading results. Inputs Done: {bInputsDone} Items read: {history.Count}. History Items: {historyQueueItemCount}. Pending Snapshots: {pendingSnapshotCount}");

                }
                while (history.Count > 0 || !bInputsDoneBefore || historyResponse.PendingItemCount > 0 || pendingSnapshotCount > 0 || historyQueueItemCount > 0);

                WriteLine($"Done with result loop. Inputs Done: {bInputsDone} PropCount: {propCount}. Duplicates: {duplicates}. Pending Snapshots: {pendingSnapshotCount}");
            }
            {
                var orderedInputEntries = HistoryInputEntries.Where(e => e.InOutput).OrderBy(e => e.Name).ThenBy(e => e.Value).ThenBy(e => e.Time);
                var orderedReportedEntries = reportedEntries.OrderBy(e => e.Name).ThenBy(e => e.Value).ThenBy(e => e.Time);
                ValidateEntries(orderedInputEntries, orderedReportedEntries, reportAggregates1, reportInitialValues1, reportUnchanged1, reportAllProps, samplingWindow);
            }


            var storageMirror = tThing.GetHistoryStore(token2);
            var mirrorEntries = new HashSet<HistoryEntry>();
            foreach (var mirrorItem in storageMirror.TheValues)
            {
                foreach (var prop in mirrorItem.PB.OrderBy(p => p.Key))
                {
                    propCount++;
                    var mirrorEntry = new HistoryEntry { Name = prop.Key, Value = prop.Value, Time = mirrorItem.cdeCTIM, InOutput = true };
                    if (!mirrorEntries.Contains(mirrorEntry))
                    {
                        mirrorEntries.Add(mirrorEntry);
                    }
                    else
                    {
                        duplicates++;
                    }
                }
            }

            var orderedInputEntries2 = HistoryInputEntries.Where(e => e.InOutput)./*Where(e => props2.Contains(e.Name)).*/OrderBy(e => e.Time).ThenBy(e => e.Name.Trim('[', ']')).ThenBy(e => e.Value);

            var orderedMirrorEntries = mirrorEntries.OrderBy(e => e.Time).ThenBy(e => e.Name.Trim('[', ']')).ThenBy(e => e.Value);
            ValidateEntries(orderedInputEntries2, orderedMirrorEntries, reportAggregates2, /*reportInitialValues2*/true, reportUnchanged2, reportAllProps, samplingWindow); // TODO investigate initial values being reported for reportunchanged (or is this the baseline, one first registration only?)
            var aggregates = orderedMirrorEntries.Where(e => e.Name.StartsWith("["));
            int expectedAggregateCount;
            if (!reportAggregates2)
            {
                expectedAggregateCount = 0;
            }
            else
            {
                if (reportAllProps)
                {
                    var orderedInputEntries3 = HistoryInputEntries.Where(e => !e.Value.ToString().EndsWith(".0"))./*Where(e => props2.Contains(e.Name)).*/OrderBy(e => e.Time).ThenBy(e => e.Name).ThenBy(e => e.Value);
                    expectedAggregateCount = 0;
                    var time = DateTimeOffset.MinValue;
                    HashSet<string> propertiesInBucket = new HashSet<string>();
                    foreach (var item in orderedInputEntries3)
                    {
                        if (!HistoryEntry.InSameSamplingWindow(item.Time, time, samplingWindow))
                        {
                            expectedAggregateCount += propertiesInBucket.Count;
                            //propertiesInBucket.Clear();
                            time = item.Time;
                        }
                        propertiesInBucket.Add(item.Name);
                    }
                    expectedAggregateCount += propertiesInBucket.Count;
                    expectedAggregateCount =
                        //( /*orderedInputEntries2.Count() + */numberOfAggregations // ((iterations - 2)* (iterations - 1) /2 ) // 4 aggregates per input value
                        (expectedAggregateCount
                        + (reportInitialValues2 ? itemsPerIteration : 0)) // 4 additional ones for each initial value
                        * 4;
                }
                else
                {
                    expectedAggregateCount =
                        (orderedInputEntries2.Count() // 4 aggregates per input value
                        + (reportInitialValues2 ? itemsPerIteration : 0)) // 4 additional ones for each initial value
                        * 4;
                }
            }

            //File.WriteAllText(Path.Combine(TestContext.CurrentContext.WorkDirectory, "orderedInputs2"), cdeNewtonsoft.Json.JsonConvert.SerializeObject(orderedInputEntries2, cdeNewtonsoft.Json.Formatting.None, TheBaseAssets.cdeNewtonJSONConfig));
            //File.WriteAllText(Path.Combine(TestContext.CurrentContext.WorkDirectory, "aggregates2"), cdeNewtonsoft.Json.JsonConvert.SerializeObject(aggregates, cdeNewtonsoft.Json.Formatting.None, TheBaseAssets.cdeNewtonJSONConfig));

            Assert.AreEqual(expectedAggregateCount, aggregates.Count(), "Mismatched aggregate count");
            tThing.UnregisterUpdateHistory(token);
            tThing.UnregisterUpdateHistory(token2);
        }

        private void ValidateEntries(IOrderedEnumerable<HistoryEntry> orderedInputEntries, IOrderedEnumerable<HistoryEntry> orderedReportedEntries, bool bIgnoreAggregates, bool bIgnoreInitialValues, bool bIgnoreMismatchForReportUnchangedValues, bool bReportAllValues, TimeSpan samplingWindow)
        {
            File.WriteAllText(Path.Combine(TestContext.CurrentContext.WorkDirectory, "orderedInputs"), Newtonsoft.Json.JsonConvert.SerializeObject(orderedInputEntries, Newtonsoft.Json.Formatting.None));
            File.WriteAllText(Path.Combine(TestContext.CurrentContext.WorkDirectory, "orderedResults"), Newtonsoft.Json.JsonConvert.SerializeObject(orderedReportedEntries, Newtonsoft.Json.Formatting.None));
            var mismatchedItems = new List<List<HistoryEntry>>();
            var extraOutputs = new List<HistoryEntry>();

            var enumOrderedInputEntries = orderedInputEntries.GetEnumerator();
            int counter = 0;
            var propValuesSeen = new List<HistoryEntry>();
            int ignoredDuplicateCount = 0;
            foreach (var reportedEntry in orderedReportedEntries)
            {
                if ((bIgnoreAggregates || bReportAllValues) && reportedEntry.Name.StartsWith("["))
                {
                    // TODO properly handle props of props and aggregates: for now, just ignore
                    continue;
                }
                if (bReportAllValues && reportedEntry.Name == "cdeStartupTime")
                {
                    continue;
                }
                if (bIgnoreInitialValues && reportedEntry.Name.StartsWith("Prop") && reportedEntry.Value.ToString().EndsWith(".0"))
                {
                    // TODO properly verify initial values
                    continue;
                }
                if (bIgnoreMismatchForReportUnchangedValues && propValuesSeen.Any(p => p.Name == reportedEntry.Name && p.Value == reportedEntry.Value))
                {
                    // With reportunchangedvalues and short cooldown periods, an update may miss it's timebucket and the previous update gets reported again
                    ignoredDuplicateCount++;
                    continue;
                }
                propValuesSeen.Add(reportedEntry);
                var moreAvailable = enumOrderedInputEntries.MoveNext();
                if (!moreAvailable)
                {
                    extraOutputs.Add(reportedEntry);
                }
                else
                {
                    var inputEntry = enumOrderedInputEntries.Current;
                    if (!inputEntry.Matches(reportedEntry, samplingWindow))
                    {
                        mismatchedItems.Add(new List<HistoryEntry> { inputEntry, reportedEntry });
                    }
                }
                counter++;
            }

            if (ignoredDuplicateCount > 0)
            {
                WriteLine($"{ignoredDuplicateCount} duplicate items found. Ignored due to expected behavior with reportunchanged.");
            }

            var unreportedItems = new List<HistoryEntry>();
            while (enumOrderedInputEntries.MoveNext())
            {
                unreportedItems.Add(enumOrderedInputEntries.Current);
            }
            if (!bIgnoreMismatchForReportUnchangedValues)
            {
                // With reportunchangedvalues and short cooldown periods, an update may miss it's timebucket and the previous update gets reported again: these show up as mismatched due to artificial timestamps
                Assert.AreEqual(0, mismatchedItems.Count, "Some reported items did not match or items were missing");
            }
            else
            {
                if (mismatchedItems.Count > 0)
                {
                    WriteLine($"{mismatchedItems.Count} mismatched items found. Ignored due to expected behavior with reportunchanged.");
                }
            }
            Assert.AreEqual(0, unreportedItems.Count, $"One or more items were not reported");
            Assert.AreEqual(0, extraOutputs.Count, $"Extra items were reported that did not match an input");
        }

        internal struct HistoryEntry
        {
            public string Name;
            public object Value;
            public DateTimeOffset Time;
            public bool InOutput;

            public override string ToString()
            {
                return $"{Time:o}:{Name}={Value}".ToString(CultureInfo.InvariantCulture);
            }

            internal bool Matches(HistoryEntry reportedEntry, TimeSpan samplingWindow)
            {
                if (samplingWindow == TimeSpan.Zero)
                {
                    return this.Equals(reportedEntry);
                }
                if (!InSameSamplingWindow(this.Time, reportedEntry.Time, samplingWindow))
                {
                    return false;
                }
                return Name == reportedEntry.Name && Value.Equals(reportedEntry.Value);
            }
            internal static bool InSameSamplingWindow(DateTimeOffset time1, DateTimeOffset time2, TimeSpan samplingWindow)
            {
                return samplingWindow == TimeSpan.Zero ? time1 == time2 : Math.Abs((time1 - time2).Ticks) < samplingWindow.Ticks;
            }
        }

        void AddProperty(TheThing tThing, string name, object value, DateTimeOffset timestamp, TimeSpan minWaitBetweenProps, bool willAppearInOutput)
        {
            var entry = new HistoryEntry { Name = name, Value = value, Time = timestamp, InOutput = willAppearInOutput };
            tThing.SetProperty(name, value, timestamp);
            HistoryInputEntries.Enqueue(entry);
        }

        void WriteLine(string format, params object[] args)
        {
            System.Diagnostics.Debug.WriteLine(String.Format($"{DateTimeOffset.Now:O}: {format}", args));
            Console.WriteLine(format, args);
            TestContext.WriteLine(format, args);
        }
    }
}
