// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Communication;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

#pragma warning disable CS1591    //TODO: Remove and document public methods

namespace nsCDEngine.BaseClasses
{
    public class TheKPIs : ICDEKpis
    {
        public void IncrementKPI(string name, bool dontReset = false)
        {
            TheCDEKPIs.IncrementKPI(name, dontReset);
        }

        /// <summary>
        /// Increments an existing KPI by 1 using an eKPINames enum.
        /// </summary>
        /// <param name="eKPI">Enum that determines which KPI to increment</param>
        public void IncrementKPI(eKPINames eKPI)
        {
            TheCDEKPIs.IncrementKPI(eKPI);
        }
    }

    /// <summary>
    /// All KPIs (Key Performance Indicators) of the C-DEngine
    /// KPI1 -10 can be used for an application
    /// </summary>
    public static class TheCDEKPIs
    {
        // Array of KPI metrics
        private static long[] KPIs;

        // Maps the name of the KPI to the index in the KPI array
        private static cdeConcurrentDictionary<string, int> KPIIndexes = null;

        // Number of elements to increase the KPI array by when full
        private const int arrayIncreaseSize = 10;

        #region Legacy KPI Properties
        public static double QSenders
        {
            get { return GetKPI(eKPINames.QSenders); }
            set { SetKPI(eKPINames.QSenders, (long)value); }
        }
        public static long WSTestClients
        {
            get { return GetKPI(eKPINames.WSTestClients); }
            set { SetKPI(eKPINames.WSTestClients, value); }
        }
        public static double QSenderInRegistry
        {
            get { return GetKPI(eKPINames.QSenderInRegistry); }
            set { SetKPI(eKPINames.QSenderInRegistry, (long)value); }
        }
        public static double QSReceivedTSM
        {
            get { return GetKPI(eKPINames.QSReceivedTSM); }
            set { SetKPI(eKPINames.QSReceivedTSM, (long)value); }
        }
        public static double QSSendErrors
        {
            get { return GetKPI(eKPINames.QSSendErrors); }
            set { SetKPI(eKPINames.QSSendErrors, (long)value); }
        }
        public static double QSInserted
        {
            get { return GetKPI(eKPINames.QSInserted); }
            set { SetKPI(eKPINames.QSInserted, (long)value); }
        }
        public static double QSQueued
        {
            get { return GetKPI(eKPINames.QSQueued); }
            set { SetKPI(eKPINames.QSQueued, (long)value); }
        }
        public static double QSRejected
        {
            get { return GetKPI(eKPINames.QSRejected); }
            set { SetKPI(eKPINames.QSRejected, (long)value); }
        }
        public static double QSSETPRejected
        {
            get { return GetKPI(eKPINames.QSSETPRejected); }
            set { SetKPI(eKPINames.QSSETPRejected, (long)value); }
        }
        public static double QSSent
        {
            get { return GetKPI(eKPINames.QSSent); }
            set { SetKPI(eKPINames.QSSent, (long)value); }
        }
        public static double QSLocalProcessed
        {
            get { return GetKPI(eKPINames.QSLocalProcessed); }
            set { SetKPI(eKPINames.QSLocalProcessed, (long)value); }
        }
        public static double QSConnects
        {
            get { return GetKPI(eKPINames.QSConnects); }
            set { SetKPI(eKPINames.QSConnects, (long)value); }
        }
        public static double QSDisconnects
        {
            get { return GetKPI(eKPINames.QSDisconnects); }
            set { SetKPI(eKPINames.QSDisconnects, (long)value); }
        }
        public static double QSNotRelayed
        {
            get { return GetKPI(eKPINames.QSNotRelayed); }
            set { SetKPI(eKPINames.QSNotRelayed, (long)value); }
        }
        public static double QKBSent
        {
            get { return GetKPI(eKPINames.QKBSent); }
            set { SetKPI(eKPINames.QKBSent, (long)value); }
        }
        public static double QKBReceived
        {
            get { return GetKPI(eKPINames.QKBReceived); }
            set { SetKPI(eKPINames.QKBReceived, (long)value); }
        }
        public static double QSCompressedPLS
        {
            get { return GetKPI(eKPINames.QSCompressedPLS); }
            set { SetKPI(eKPINames.QSCompressedPLS, (long)value); }
        }
        public static double TotalEngineErrors
        {
            get { return GetKPI(eKPINames.TotalEngineErrors); }
            set { SetKPI(eKPINames.TotalEngineErrors, (long)value); }
        }
        public static double EngineErrors
        {
            get { return GetKPI(eKPINames.EngineErrors); }
            set { SetKPI(eKPINames.EngineErrors, (long)value); }
        }
        public static double EventTimeouts
        {
            get { return GetKPI(eKPINames.EventTimeouts); }
            set { SetKPI(eKPINames.EventTimeouts, (long)value); }
        }
        public static double TotalEventTimeouts
        {
            get { return GetKPI(eKPINames.TotalEventTimeouts); }
            set { SetKPI(eKPINames.TotalEventTimeouts, (long)value); }
        }
        public static double SeenBeforeCount
        {
            get { return GetKPI(eKPINames.SeenBeforeCount); }
            set { SetKPI(eKPINames.SeenBeforeCount, (long)value); }
        }
        public static long HTCallbacks
        {
            get { return GetKPI(eKPINames.HTCallbacks); }
            set { SetKPI(eKPINames.HTCallbacks, value); }
        }
        public static double CCTSMsRelayed
        {
            get { return GetKPI(eKPINames.CCTSMsRelayed); }
            set { SetKPI(eKPINames.CCTSMsRelayed, (long)value); }
        }
        public static double CCTSMsReceived
        {
            get { return GetKPI(eKPINames.CCTSMsReceived); }
            set { SetKPI(eKPINames.CCTSMsReceived, (long)value); }
        }
        public static double CCTSMsEvaluated
        {
            get { return GetKPI(eKPINames.CCTSMsEvaluated); }
            set { SetKPI(eKPINames.CCTSMsEvaluated, (long)value); }
        }
        public static long UniqueMeshes
        {
            get { return GetKPI(eKPINames.UniqueMeshes); }
            set { SetKPI(eKPINames.UniqueMeshes, value); }
        }
        public static long BruteDelay
        {
            get { return GetKPI(eKPINames.BruteDelay); }
            set { SetKPI(eKPINames.BruteDelay, value); }
        }
        public static long SessionCount
        {
            get { return GetKPI(eKPINames.SessionCount); }
            set { SetKPI(eKPINames.SessionCount, value); }
        }
        public static double KPI1
        {
            get { return GetKPI(eKPINames.KPI1); }
            set { SetKPI(eKPINames.KPI1, (long)value); }
        }
        public static double KPI2
        {
            get { return GetKPI(eKPINames.KPI2); }
            set { SetKPI(eKPINames.KPI2, (long)value); }
        }
        public static double KPI3
        {
            get { return GetKPI(eKPINames.KPI3); }
            set { SetKPI(eKPINames.KPI3, (long)value); }
        }
        public static double KPI4
        {
            get { return GetKPI(eKPINames.KPI4); }
            set { SetKPI(eKPINames.KPI4, (long)value); }
        }
        public static double KPI5
        {
            get { return GetKPI(eKPINames.KPI5); }
            set { SetKPI(eKPINames.KPI5, (long)value); }
        }
        public static double KPI6
        {
            get { return GetKPI(eKPINames.KPI6); }
            set { SetKPI(eKPINames.KPI6, (long)value); }
        }
        public static double KPI7
        {
            get { return GetKPI(eKPINames.KPI7); }
            set { SetKPI(eKPINames.KPI7, (long)value); }
        }
        public static double KPI8
        {
            get { return GetKPI(eKPINames.KPI8); }
            set { SetKPI(eKPINames.KPI8, (long)value); }
        }
        public static double KPI9
        {
            get { return GetKPI(eKPINames.KPI9); }
            set { SetKPI(eKPINames.KPI9, (long)value); }
        }
        public static double KPI10
        {
            get { return GetKPI(eKPINames.KPI10); }
            set { SetKPI(eKPINames.KPI10, (long)value); }
        }
        public static double SetPsFired
        {
            get { return GetKPI(eKPINames.SetPsFired); }
            set { SetKPI(eKPINames.SetPsFired, (long)value); }
        }
        public static void IncWSTestClients()
        {
            IncrementKPI(eKPINames.WSTestClients);
        }
        public static void DecWSTestClients()
        {
            DecrementKPI(eKPINames.WSTestClients);
        }
        #endregion

        /// <summary>
        /// Increments an existing KPI by name by 1.  IF the KPI does not exist,
        /// a new one will be created with a value of 1.
        /// </summary>
        /// <param name="name">Name (key) of the KPI to increment</param>
        /// <param name="dontReset">If true and the KPI does not exist, the new KPI created will never be reset to zero and always increase.
        /// This means that a separatae "Total" property will not be calculated when harvested by the NodeHost.</param>
        public static void IncrementKPI(string name, bool dontReset = false)
        {
            if (KPIIndexes != null && KPIs != null)
            {
                bool exists = KPIIndexes.TryGetValue(name, out int index);
                if (exists)
                    Interlocked.Increment(ref KPIs[index]);
                else
                    CreateNewEntry(name, 1, dontReset);
            }
        }

        /// <summary>
        /// Increments an existing KPI by 1 using an eKPINames enum.
        /// </summary>
        /// <param name="eKPI">Enum that determines which KPI to increment</param>
        public static void IncrementKPI(eKPINames eKPI)
        {
            if (KPIs != null)
                Interlocked.Increment(ref KPIs[(int)eKPI]);
        }

        /// <summary>
        /// Increments an existing KPI by name by a given amount.  If the KPI does not exist,
        /// a new one will be created with the given value.
        /// </summary>
        /// <param name="name">Name (key) of the KPI to increment</param>
        /// <param name="value">The value to add to the KPI</param>
        /// <param name="dontReset">If true and the KPI does not exist, the new KPI created will never be reset to zero and always increase.
        /// This means that a separatae "Total" property will not be calculated when harvested by the NodeHost.</param>
        public static void IncrementKPI(string name, long value, bool dontReset = false)
        {
            if (KPIIndexes != null && KPIs != null)
            {
                bool exists = KPIIndexes.TryGetValue(name, out int index);
                if (exists)
                    Interlocked.Add(ref KPIs[index], value);
                else
                    CreateNewEntry(name, value, dontReset);
            }
        }

        /// <summary>
        /// Increments an existing KPI by a given amount using an eKPINames enum.
        /// </summary>
        /// <param name="eKPI">Enum that determines which KPI to increment</param>
        /// <param name="value">The value to add to the KPI</param>
        public static void IncrementKPI(eKPINames eKPI, long value)
        {
            if (KPIs != null)
                Interlocked.Add(ref KPIs[(int)eKPI], value);
        }

        /// <summary>
        /// Decrements an existing KPI by name by 1.  If the KPI does not exist,
        /// a new one will be created with a value of 0.
        /// </summary>
        /// <param name="name">Name (key) of the KPI to decrement</param>
        /// <param name="dontReset">If true and the KPI does not exist, the new KPI created will never be reset to zero and always increase.
        /// This means that a separatae "Total" property will not be calculated when harvested by the NodeHost.</param>
        public static void DecrementKPI(string name, bool dontReset = false)
        {
            if (KPIIndexes != null && KPIs != null)
            {
                bool exists = KPIIndexes.TryGetValue(name, out int index);
                if (exists)
                    Interlocked.Decrement(ref KPIs[index]);
                else
                    CreateNewEntry(name, 0, dontReset);
            }
        }

        /// <summary>
        /// Decrements an existing KPI by 1 using an eKPINames enum.
        /// </summary>
        /// <param name="eKPI">Enum that determines which KPI to decrement</param>
        public static void DecrementKPI(eKPINames eKPI)
        {
            if (KPIs != null)
                Interlocked.Decrement(ref KPIs[(int)eKPI]);
        }

        /// <summary>
        /// Sets an existing KPI to a new value.  If the KPI does not exist,
        /// a new one will be created with the given value.
        /// </summary>
        /// <param name="name">Name (key) of the KPI to set</param>
        /// <param name="value">Value of the KPI to set</param>
        /// <param name="dontReset">If true and the KPI does not exist, the new KPI created will never be reset to zero and always increase.
        /// This means that a separatae "Total" property will not be calculated when harvested by the NodeHost.</param>
        public static void SetKPI(string name, long value, bool dontReset = false)
        {
            if (KPIIndexes != null && KPIs != null)
            {
                bool exists = KPIIndexes.TryGetValue(name, out int index);
                if (exists)
                    Interlocked.Exchange(ref KPIs[index], value);
                else
                    CreateNewEntry(name, value, dontReset);
            }
        }

        /// <summary>
        /// Sets an existing KPI to a new value using an eKPINames enum
        /// </summary>
        /// <param name="eKPI">Enum that determines which KPI to set</param>
        /// <param name="value">Value of the KPI to set</param>
        public static void SetKPI(eKPINames eKPI, long value)
        {
            if (KPIs != null)
                Interlocked.Exchange(ref KPIs[(int)eKPI], value);
        }

        /// <summary>
        /// Return the value of an existing KPI by name
        /// </summary>
        /// <param name="name">The name of the KPI whose value will be returned</param>
        /// <returns>The value of the KPI</returns>
        public static long GetKPI(string name)
        {
            long kpi = 0;
            if (KPIIndexes != null && KPIs != null)
            {
                bool exists = KPIIndexes.TryGetValue(name, out int index);
                if (exists)
                    kpi = Interlocked.Read(ref KPIs[index]);
            }
            return kpi;
        }

        /// <summary>
        /// Return the value of an existing KPI using an eKPINames enum
        /// </summary>
        /// <param name="eKPI">Enum that determines which KPI value to return</param>
        /// <returns>The value of the KPI</returns>
        public static long GetKPI(eKPINames eKPI)
        {
            long kpi = 0;
            if (KPIs != null)
                kpi = Interlocked.Read(ref KPIs[(int)eKPI]);
            return kpi;
        }

        /// <summary>
        /// Increments the KPI for a TSMByENG with the given engine name by 1.
        /// If the KPI does not exist, a new one will be created with a value of 1.
        /// </summary>
        /// <param name="pEngine">The engine name corresponding to the KPI</param>
        /// <param name="dontReset">If true and the KPI does not exist, the new KPI created will never be reset to zero and always increase.
        /// This means that a separatae "Total" property will not be calculated when harvested by the NodeHost.</param>
        public static void IncTSMByEng(string pEngine, bool dontReset = false)
        {
            if (KPIIndexes != null && KPIs != null)
            {
                bool exists = KPIIndexes.TryGetValue($"TSMbyENG-{pEngine}", out int index);
                if (exists)
                    Interlocked.Increment(ref KPIs[index]);
                else
                    CreateNewEntry($"TSMbyENG-{pEngine}", 1, dontReset);
            }
        }

        // Creates a new entry in the KPIIndexes and KPIs.  If the KPIs array is full, it will be recreated.
        private static void CreateNewEntry(string name, long value, bool dontReset)
        {
            try
            {
                lock (KPIIndexes)
                {
                    lock (KPIs)
                    {
                        int entries = KPIIndexes.Count;
                        if (entries == KPIs.Length)
                        {
                            long[] newKPIs = new long[entries + arrayIncreaseSize];
                            Array.Copy(KPIs, newKPIs, entries);
                            KPIs = newKPIs;
                        }
                        if (KPIs.Length > entries)
                        {
                            if (KPIIndexes.TryAdd(name, entries))
                                Interlocked.Exchange(ref KPIs[entries], value);
                        }
                        if (dontReset)
                        {
                            doNotReset.Add(name);
                            doNotComputeTotals.Add(name);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Log exception?
            }
        }

        /// <summary>
        /// Most recent time the KPIs were reset to zero
        /// </summary>
        public static DateTimeOffset LastReset { get; set; }

        // Array of KPI names that should not be reset
        private static readonly List<string> doNotReset = new List<string>
        {
            nameof(eKPINames.TotalEngineErrors), nameof(eKPINames.TotalEventTimeouts), nameof(eKPINames.UnsignedNodes), nameof(eKPINames.UnsignedPlugins),
            nameof(eKPINames.UniqueMeshes), nameof(eKPINames.SeenBeforeCount), nameof(eKPINames.KnownNMINodes), nameof(eKPINames.StreamsNotFound),
            nameof(eKPINames.BlobsNotFound), nameof(eKPINames.WSTestClients), nameof(eKPINames.QSenders), nameof(eKPINames.QSenderInRegistry),
            nameof(eKPINames.SessionCount)
        };


        // Array of KPI names that should not have a total computed when harvesting to TheThing
        private static readonly List<string> doNotComputeTotals = new List<string>
        {
            nameof(eKPINames.UnsignedNodes), nameof(eKPINames.UnsignedPlugins), nameof(eKPINames.UniqueMeshes), nameof(eKPINames.BruteDelay),
            nameof(eKPINames.QSenders), nameof(eKPINames.QSenderInRegistry), nameof(eKPINames.SetPsFired), nameof(eKPINames.TotalEngineErrors),
            nameof(eKPINames.EngineErrors), nameof(eKPINames.EventTimeouts), nameof(eKPINames.TotalEventTimeouts), nameof(eKPINames.QSNotRelayed),
            nameof(eKPINames.SessionCount), nameof(eKPINames.WSTestClients)
        };




        internal static Dictionary<string, object> GetDictionary()
        {
            TheThing tn = new TheThing();
            ToThingProperties(tn, !TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("EnableKPIs")));
            return tn.GetPBAsDictionary();
        }

        private static void CreateKPIIndexes()
        {
            // We could wait here for engines, but then other KPIs will not be written in meantime
            // await TheBaseEngine.WaitForEnginesStartedAsync();
            KPIIndexes = new cdeConcurrentDictionary<string, int>();
            lock (KPIIndexes)
            {
                Array indexes = Enum.GetValues(typeof(eKPINames));
                foreach (var index in indexes)
                {
                    KPIIndexes.TryAdd(Enum.GetName(typeof(eKPINames), index), (int)index);
                }
                List<string> engineNames = TheThingRegistry.GetEngineNames(false);
                foreach (string engineName in engineNames)
                {
                    KPIIndexes.TryAdd($"TSMbyENG-{engineName}", KPIIndexes.Count);
                }
            }
        }

        /// <summary>
        /// Resets all the KPIs to zero
        /// </summary>
        public static void Reset()
        {
            if (KPIIndexes == null)
                CreateKPIIndexes();
            if (KPIs == null)
                KPIs = new long[KPIIndexes.Count];
            return;
        }

        /// <summary>
        /// Returns a string of all KPIs
        /// </summary>
        /// <param name="DoReset">if try, the KPIs are reset after the string was constructed</param>
        /// <returns></returns>
        public static string GetKPIs(bool DoReset)
        {
            string tRes = $"C-DEngine-KPIs: LR:{LastReset:MM/dd/yyyy hh:mm:ss.fff--tt} ";
            List<string> orderedKeys = KPIIndexes.Keys.OrderBy(s => s).ToList();
            foreach (string key in orderedKeys)
            {
                tRes += $"{key}:{GetKPI(key)} ";
            }
            if (DoReset) Reset();
            return tRes;
        }

        private static readonly object thingHarvestLock = new object();
        internal static void ToThingProperties(TheThing pThing, bool bReset)
        {
            lock(thingHarvestLock)
            {
                if (pThing == null) return;
                if (KPIIndexes == null)
                    CreateKPIIndexes();
                if (KPIs == null)
                    KPIs = new long[KPIIndexes.Count];

                if (KPIs != null && KPIIndexes != null)
                {
                    TimeSpan timeSinceLastReset = DateTimeOffset.Now.Subtract(LastReset);
                    bool resetReady = timeSinceLastReset.TotalMilliseconds >= 1000;
                    foreach (var keyVal in KPIIndexes.GetDynamicEnumerable())
                    {
                        bool donres = doNotReset.Contains(keyVal.Key);
                        long kpiValue;
                        if (bReset && !donres && resetReady)
                            kpiValue = Interlocked.Exchange(ref KPIs[keyVal.Value], 0);
                        else
                            kpiValue = Interlocked.Read(ref KPIs[keyVal.Value]);

                        // LastReset not set yet - shouldn't happen since it is set in first call to Reset (TheBaseAssets.InitAssets)
                        if (LastReset == DateTimeOffset.MinValue || timeSinceLastReset.TotalSeconds <= 1 || donres)
                            pThing.SetProperty(keyVal.Key, kpiValue);
                        else
                            pThing.SetProperty(keyVal.Key, kpiValue / timeSinceLastReset.TotalSeconds); // Normalize value to "per second"
                        if (!doNotComputeTotals.Contains(keyVal.Key))
                        {
                            pThing.SetProperty(keyVal.Key + "Total", TheThing.GetSafePropertyNumber(pThing, keyVal.Key + "Total") + kpiValue);
                        }
                    }
                    if (bReset && resetReady)
                        LastReset = DateTimeOffset.Now;
                    // Grab some KPIs from sources - Workaround, this should be computed in the source instead
                    SetKPI(eKPINames.QSenders, TheQueuedSenderRegistry.GetSenderListNodes().Count);
                    SetKPI(eKPINames.QSenderInRegistry, TheQueuedSenderRegistry.Count());
                    SetKPI(eKPINames.SessionCount, TheBaseAssets.MySession.GetSessionCount());
                    SetKPI(eKPINames.UniqueMeshes, TheQueuedSenderRegistry.GetUniqueMeshCount());
                    SetKPI(eKPINames.UnsignedNodes, TheQueuedSenderRegistry.GetUnsignedNodeCount());
                    SetKPI(eKPINames.KnownNMINodes, Engines.NMIService.TheFormsGenerator.GetNMINodeCount());
                    SetKPI(eKPINames.StreamsNotFound, Communication.HttpService.TheHttpService.IsStreaming.Count);
                    SetKPI(eKPINames.BlobsNotFound, Engines.ContentService.TheContentServiceEngine.BlobsNotHere.Count);
                }
            }
        }
    }
}
