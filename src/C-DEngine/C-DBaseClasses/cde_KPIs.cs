// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using cdeNewtonsoft.Json.Linq;
using nsCDEngine.Communication;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

#pragma warning disable CS1591    //TODO: Remove and document public methods

namespace nsCDEngine.BaseClasses
{
    public class TheKPIs : ICDEKpis
    {
        public void IncrementKPI(string pName, bool dontReset = false)
        {
            TheCDEKPIs.IncrementKPI(pName, dontReset);
        }

        /// <summary>
        /// Increments an existing KPI by 1 using an eKPINames enum.
        /// </summary>
        /// <param name="pName">Enum that determines which KPI to increment</param>
        public void IncrementKPI(eKPINames pName)
        {
            TheCDEKPIs.IncrementKPI(pName);
        }
    }

    /// <summary>
    /// All KPIs (Key Performance Indicators) of the C-DEngine
    /// KPI1 -10 can be used for an application
    /// </summary>
    public static class TheCDEKPIs
    {
        // Dictionary of the KPI metrics
        private static cdeConcurrentDictionary<string, long> KPIs = new();

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
        /// <summary>
        /// Counter of current WebSocket connections
        /// </summary>
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

        /// <summary>
        /// Max amount of SETP messages 
        /// </summary>
        public static double KPI7
        {
            get { return GetKPI(eKPINames.KPI7); }
            set { SetKPI(eKPINames.KPI7, (long)value); }
        }
        /// <summary>
        /// Max amount of SETNP messages
        /// </summary>
        public static double KPI8
        {
            get { return GetKPI(eKPINames.KPI8); }
            set { SetKPI(eKPINames.KPI8, (long)value); }
        }
        /// <summary>
        /// Max amount of Properties set by SETNP 
        /// </summary>
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
            => IncrementKPI(name, 1, dontReset);

        /// <summary>
        /// Increments an existing KPI by 1 using an eKPINames enum.
        /// </summary>
        /// <param name="eKPI">Enum that determines which KPI to increment</param>
        public static void IncrementKPI(eKPINames eKPI)
            => IncrementKPI(eKPI.ToString(), 1);

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
            KPIs.AddOrUpdate(name,
                key => CreateNewEntry(key, value, dontReset),
                (_, oldValue) => oldValue + value);
        }

        /// <summary>
        /// Increments an existing KPI by a given amount using an eKPINames enum.
        /// </summary>
        /// <param name="eKPI">Enum that determines which KPI to increment</param>
        /// <param name="value">The value to add to the KPI</param>
        public static void IncrementKPI(eKPINames eKPI, long value)
            => IncrementKPI(eKPI.ToString(), value);

        /// <summary>
        /// Decrements an existing KPI by name by 1.  If the KPI does not exist,
        /// a new one will be created with a value of 0.
        /// </summary>
        /// <param name="name">Name (key) of the KPI to decrement</param>
        /// <param name="dontReset">If true and the KPI does not exist, the new KPI created will never be reset to zero and always increase.
        /// This means that a separatae "Total" property will not be calculated when harvested by the NodeHost.</param>
        public static void DecrementKPI(string name, bool dontReset = false)
        {
            KPIs.AddOrUpdate(name,
                key => CreateNewEntry(key, 0, dontReset),
                (_, oldValue) => --oldValue);
        }

        /// <summary>
        /// Decrements an existing KPI by 1 using an eKPINames enum.
        /// </summary>
        /// <param name="eKPI">Enum that determines which KPI to decrement</param>
        public static void DecrementKPI(eKPINames eKPI)
            => DecrementKPI(eKPI.ToString());

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
            KPIs.AddOrUpdate(name,
                key => CreateNewEntry(key, value, dontReset),
                (_, _) => value);
        }

        /// <summary>
        /// Sets an existing KPI to a new value using an eKPINames enum
        /// </summary>
        /// <param name="eKPI">Enum that determines which KPI to set</param>
        /// <param name="value">Value of the KPI to set</param>
        public static void SetKPI(eKPINames eKPI, long value)
            => SetKPI(eKPI.ToString(), value);

        /// <summary>
        /// Return the value of an existing KPI by name
        /// </summary>
        /// <param name="name">The name of the KPI whose value will be returned</param>
        /// <returns>The value of the KPI</returns>
        public static long GetKPI(string name)
            => KPIs.TryGetValue(name, out var kpi) ? kpi : 0;

        /// <summary>
        /// Return the value of an existing KPI using an eKPINames enum
        /// </summary>
        /// <param name="eKPI">Enum that determines which KPI value to return</param>
        /// <returns>The value of the KPI</returns>
        public static long GetKPI(eKPINames eKPI)
            => GetKPI(eKPI.ToString());

        /// <summary>
        /// Increments the KPI for a TSMByENG with the given engine name by 1.
        /// If the KPI does not exist, a new one will be created with a value of 1.
        /// </summary>
        /// <param name="pEngine">The engine name corresponding to the KPI</param>
        /// <param name="dontReset">If true and the KPI does not exist, the new KPI created will never be reset to zero and always increase.
        /// This means that a separate "Total" property will not be calculated when harvested by the NodeHost.</param>
        public static void IncTSMByEng(string pEngine, bool dontReset = false)
            => IncrementKPI($"TSMbyENG-{pEngine}", dontReset);

        // Adds KPI by its name to the dontReset and doNotComputeTotals lists depending on the dontReset parameter.
        // Returns the given value for the new entry.
        private static long CreateNewEntry(string name, long value, bool dontReset)
        {
            if (dontReset)
            {
                doNotReset.Add(name);
                doNotComputeTotals.Add(name);
            }

            return value;
        }

        /// <summary>
        /// Most recent time the KPIs were reset to zero
        /// </summary>
        public static DateTimeOffset LastReset { get; set; }

        // Array of KPI names that should not be reset
        private static readonly List<string> doNotReset = new ()
        {
            nameof(eKPINames.TotalEngineErrors), nameof(eKPINames.TotalEventTimeouts), nameof(eKPINames.UnsignedNodes), nameof(eKPINames.UnsignedPlugins),
            nameof(eKPINames.UniqueMeshes), nameof(eKPINames.SeenBeforeCount), nameof(eKPINames.KnownNMINodes), nameof(eKPINames.StreamsNotFound),
            nameof(eKPINames.BlobsNotFound), nameof(eKPINames.WSTestClients), nameof(eKPINames.QSenders), nameof(eKPINames.QSenderInRegistry),
            nameof(eKPINames.SessionCount), nameof(eKPINames.KPI3)
        };


        // Array of KPI names that should not have a total computed when harvesting to TheThing
        private static readonly List<string> doNotComputeTotals = new ()
        {
            nameof(eKPINames.UnsignedNodes), nameof(eKPINames.UnsignedPlugins), nameof(eKPINames.UniqueMeshes), nameof(eKPINames.BruteDelay),
            nameof(eKPINames.QSenders), nameof(eKPINames.QSenderInRegistry), nameof(eKPINames.SetPsFired), nameof(eKPINames.TotalEngineErrors),
            nameof(eKPINames.EngineErrors), nameof(eKPINames.EventTimeouts), nameof(eKPINames.TotalEventTimeouts), nameof(eKPINames.QSNotRelayed),
            nameof(eKPINames.SessionCount), nameof(eKPINames.WSTestClients), nameof(eKPINames.KPI3), nameof(eKPINames.KPI7), nameof(eKPINames.KPI8), nameof(eKPINames.KPI9)
        };

        internal static Dictionary<string, object> GetDictionary()
        {
            TheThing tn = new ();
            ToThingProperties(tn, !TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("EnableKPIs")));
            return tn.GetPBAsDictionary();
        }

        /// <summary>
        /// Resets all the KPIs to zero
        /// </summary>
        public static void Reset()
        {
            // Intentionally left blank.

            // Reason: Reset did NOT reset all KPIs.
            // Instead it was initializing the KPIs if not yet done which is not necessary anymore.
        }
            
        /// <summary>
        /// Returns a string of all KPIs
        /// </summary>
        /// <param name="doReset">if true, the KPIs are reset after the string was constructed.</param>
        /// <returns></returns>
        public static string GetKPIs(bool doReset)
        {
            StringBuilder tRes = new ();
            tRes.Append($"C-DEngine-KPIs: LR:{LastReset:MM/dd/yyyy hh:mm:ss.fff--tt} ");
            List<string> orderedKeys = KPIs.Keys.OrderBy(s => s).ToList();
            foreach (string key in orderedKeys)
            {
                tRes.Append($"{key}:{GetKPI(key)} ");
            }
            if (doReset) Reset();
            return tRes.ToString();
        }

        private static readonly object thingHarvestLock = new ();
        internal static void ToThingProperties(TheThing pThing, bool bReset)
        {
            if (pThing == null) return;

            lock (thingHarvestLock)
            {
                TimeSpan timeSinceLastReset = DateTimeOffset.Now.Subtract(LastReset);
                bool resetReady = timeSinceLastReset.TotalMilliseconds >= 1000;
                foreach (var keyVal in KPIs)
                {
                    bool dontReset = doNotReset.Contains(keyVal.Key);

                    long kpiValue = keyVal.Value;
                    if (bReset && !dontReset && resetReady)
                        SetKPI(keyVal.Key, 0);

                    // LastReset not set yet - shouldn't happen since it is set in first call to Reset (TheBaseAssets.InitAssets)
                    if (LastReset == DateTimeOffset.MinValue || timeSinceLastReset.TotalSeconds <= 1 || dontReset)
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
