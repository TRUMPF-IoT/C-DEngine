// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace nsCDEngine
{
    internal class TheStorageMirrorHistorian : IHistorian
    {
        //TheStorageMirror<TheThingStore> _storageMirror;
        IThingStream _thingStream;
        private static TheStorageMirror<ConsumerRegistration> _permanentConsumers; // Shared across all historians

        internal class ConsumerRegistrationWithStore<T> : ConsumerRegistration where T : TheDataBase, INotifyPropertyChanged, new()
        {
            private TheStorageMirror<T> MyHistoryStore;

            internal override void Init()
            {
                base.Init();
                Init(null);
            }
            internal void Init(TheStorageMirror<T> store)
            {
                if (MaintainHistoryStore && MyHistoryStore == null)
                {
                    if (store == null && !ExternalHistoryStore)
                    {
                        var storeParams = HistoryStoreParameters;
                        if (storeParams == null)
                        {
                            storeParams = new TheStorageMirrorParameters
                            {
                                CacheTableName = "ThingHistory" + TheCommonUtils.cdeGuidToString(cdeMID), // CODE REVIEW: Should we rename this to ThingHistoryStore to distinguish it better from the history queue? Right now only the GUIDs are different (history token for the store vs. thing cdeMID for the queue). Existing data would be lost on upgrade from <4.104, though
                                IsRAMStore = true,
                                IsCached = true,
                                CacheStoreInterval = new TimeSpan(0, 0, 0, 10),
                                IsCachePersistent = Persistent,
                                UseSafeSave = true,
                                MaxCount = MaxCount,
                                MaxAge = MaxAge,
                                LoadSync = Persistent,
                                TrackInsertionOrder = true,
                                FriendlyName = "cdeThingHistory",
                                Description = "Thing History"
                            };
                        }
                        else
                        {
                            if (String.IsNullOrEmpty(storeParams.CacheTableName))
                            {
                                storeParams.CacheTableName = "ThingHistory" + TheCommonUtils.cdeGuidToString(cdeMID);
                            }
                        }
                        var mirrorThingStore = new TheStorageMirror<T>(TheCDEngines.MyIStorageService);
                        // Not needed here: mirrorThingStore.MyMirrorCache.TrackInsertionOrder();
                        mirrorThingStore.CreateStore(storeParams, null);
                        //mirrorThingStore.InitializeStore(storeParams);
                        MyHistoryStore = mirrorThingStore;
                    }
                    else
                    {
                        MyHistoryStore = store;
                    }
                }
            }

            public ConsumerRegistrationWithStore(TheThing tThing, Guid token, TheHistoryParameters param, TheStorageMirror<T> store) : base(param)
            {
                Token = token;
                cdeMID = token;
                ThingMid = tThing.cdeMID;
                Init(store);
            }
            public ConsumerRegistrationWithStore(ConsumerRegistration param, TheStorageMirror<T> store) : base(param)
            {
                Token = param.Token;
                cdeMID = param.Token;
                ThingMid = param.ThingMid;
                Init(store);
            }


            override internal void Delete()
            {
                if (MyHistoryStore != null && !ExternalHistoryStore)
                {
                    MyHistoryStore.RemoveStore(false);
                }
                MyHistoryStore = null;
            }

            readonly object updateLock = new object();
            TimeSpan updateWaitTimeout = new TimeSpan(0, 0, 30);
            volatile bool bSkippedLock = false;
            override internal void HandleUpdates(TheStorageMirrorHistorian globalHistorian)
            {
                if (!TheCommonUtils.cdeIsLocked(updateLock))
                {
                    lock (updateLock)
                    {
                        List<TheThingStore> updates = null;
                        bool bUpdatesRead = false;
                        do
                        {
                            bSkippedLock = false;
                            updates = globalHistorian.GetHistoryInternalAsync(this, 100, 1, updateWaitTimeout, null, true).Result.HistoryItems;
                            if (updates?.Count > 0)
                            {
                                bUpdatesRead = true;
                                if (typeof(T) == typeof(TheThingStore))
                                {
                                    MyHistoryStore.AddItems(updates as List<T>, null);
                                }
                                else
                                {
                                    MyHistoryStore.AddItems(updates.Select(item =>
                                        {
                                            var newItem = new T();
                                            foreach(var prop in item.PB)
                                            {
                                                TheCommonUtils.SetPropValue(newItem, prop.Key, prop.Value);
                                            }
                                            return newItem;
                                        }).ToList(), null);
                                }
                            }
                        } while (!bUpdatesRead || updates?.Count > 0 && bSkippedLock);
                    }
                }
                else
                {
                    bSkippedLock = true;
                }
            }

            internal TheStorageMirror<T> GetStorageMirror()
            {
                if (!MaintainHistoryStore)
                {
                    return null;
                }
                return MyHistoryStore;
            }
        }

        readonly cdeConcurrentDictionary<Guid, ConsumerRegistration> _consumerRegistrations = new cdeConcurrentDictionary<Guid, ConsumerRegistration>();

        TheHistoryParameters _combinedRegistration = new TheHistoryParameters();

        // Using factory pattern so we can use a historian instance across multiple things in the future
        public static TheStorageMirrorHistorian Create(TheThing tThing)
        {
            TheStorageMirrorHistorian historian;
            if (thingsWithHistorian.TryGetValue(tThing.cdeMID, out var existingThing))
            {
                historian = existingThing.Historian as TheStorageMirrorHistorian;
            }
            else
            {
                historian = new TheStorageMirrorHistorian();
            }
            return historian;
        }

        private static void InitializeHistorians()
        {
            if (_permanentConsumers == null)
            {
                lock (thingsWithHistorian)
                {
                    if (_permanentConsumers == null)
                    {
                        var permanentConsumers = new TheStorageMirror<ConsumerRegistration>(TheCDEngines.MyIStorageService)
                        {
                            CacheTableName = "ThingHistoryConsumers",
                            IsRAMStore = true,
                            IsCached = true,
                            CacheStoreInterval = 10,
                            IsStoreIntervalInSeconds = true,
                            IsCachePersistent = true,
                            UseSafeSave = true,
                            BlockWriteIfIsolated=true
                        };
                        permanentConsumers.SetMaxStoreSize(0);
                        permanentConsumers.MyMirrorCache.FastSaveLock = TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("HistorianBufferFastSave"));
                        permanentConsumers.InitializeStore(false, false, true, true);
                        foreach (var consumer in permanentConsumers.TheValues)
                        {
                            consumer.Init();
                        }
                        TheCDEngines.eventEngineShutdown += sinkShutdown;

                        _permanentConsumers = permanentConsumers;
                    }
                }
            }
        }

        private static void sinkShutdown()
        {
            if (_permanentConsumers != null)
            {
                _permanentConsumers.ForceSave();
            }
            if (thingsWithHistorian != null)
            {
                foreach (var thingKV in thingsWithHistorian.GetDynamicEnumerable())
                {
                    thingKV.Value.Historian.Close(); //._storageMirror.ForceSave();
                }
            }
        }

        internal static cdeConcurrentDictionary<Guid, TheThing> thingsWithHistorian = new cdeConcurrentDictionary<Guid, TheThing>();

        internal static void RegisterThingWithHistorian(TheThing tThing, bool CreateHistorianIfNoConsumers)
        {
            InitializeHistorians();

            var consumers = _permanentConsumers.MyMirrorCache.GetEntriesByFunc((c) => tThing.cdeMID == c.ThingMid);
            if ((consumers != null && consumers.Count > 0) || CreateHistorianIfNoConsumers)
            {
                if (tThing.Historian == null)
                {
                    lock (thingsWithHistorian)
                    {
                        if (tThing.Historian == null)
                        {
                            var historian = Create(tThing);

                            if (consumers != null)
                            {
                                foreach (var consumer in consumers)
                                {
                                    historian.AddOrUpdateRegistration(consumer);
                                }
                            }
                            tThing.Historian = historian;
                        }
                    }
                }
            }
        }

        public Guid RegisterConsumer<T>(TheThing tThing, TheHistoryParameters registrationParameters, TheStorageMirror<T> store) where T : TheDataBase, INotifyPropertyChanged, new()
        {
            var token = Guid.NewGuid();
            ConsumerRegistration registration;
            registration = ConsumerRegistration.Create(tThing, token, registrationParameters, store);
            if (registrationParameters.IgnoreExistingHistory == true)
            {
                registration.SequenceNumberTruncated = _thingStream?.GetLastSequenceNumber() ?? 0;
                registration.LastSequenceNumberRead = registration.SequenceNumberTruncated;
            }
            AddOrUpdateRegistration(registration);
            if (registration.Persistent)
            {
                _permanentConsumers.AddAnItem(registration, null);
            }
            if (registrationParameters.ReportInitialValues == true)
            {
                var initialProps = tThing.GetAllProperties(10);
                foreach (var prop in initialProps)
                {
                    AddPropertySnapshotInternal(tThing, cdeP.GetPropertyPath(prop), prop.Value, prop.cdeCTIM, prop.cdeSEQ, true);
                }
            }
            return token;
        }

        private void AddOrUpdateRegistration(ConsumerRegistration registration)
        {
            lock (this)
            {
                _consumerRegistrations[registration.Token] = registration;
                if (_thingStream == null)
                {
                    lock (thingsWithHistorian)
                    {
                        if (_thingStream == null)
                        {
                            _combinedRegistration = registration;
                            _pendingSnapShots.UpdateIntervals(_combinedRegistration.SamplingWindow, _combinedRegistration.SamplingWindow + _combinedRegistration.CooldownPeriod, _combinedRegistration.CooldownPeriod);
                            _thingStream = new StorageMirrorThingUpdateStream(registration.Persistent, registration.ThingMid, registration.MaxCount, registration.MaxAge);
                                //registration.HistoryStoreParameters?.IsRAMStore == null ? true : registration.HistoryStoreParameters.IsRAMStore.Value,
                                //registration.HistoryStoreParameters?.IsCached == null ? true: registration.HistoryStoreParameters.IsCached.Value,
                                //registration.HistoryStoreParameters?.FriendlyName, registration.HistoryStoreParameters?.Description);
                            //_thingStream = new MultiFileThingStream(registration.Persistent, registration.ThingMid, registration.MaxCount, registration.MaxAge);
                        }
                    }
                }
                else
                {
                    // Compute new combined registration
                    var registrations = _consumerRegistrations.Values;
                    var newCombinedRegistration = new TheHistoryParameters(registrations.First());
                    foreach (var r in registrations.Skip(1))
                    {
                        newCombinedRegistration.MergeRegistration(r);
                    }

                    // Update storage mirror
                    {
                        if (newCombinedRegistration.Persistent)
                        {
                            _thingStream.IsPersistent = true;
                        }

                        _thingStream.MaxCount = newCombinedRegistration.MaxCount;
                        _thingStream.MaxAge = newCombinedRegistration.MaxAge;

                        // TODO:
                        // Update StorageMirror configuration (if necessary)
                        // Update StorageMirror data (if necessary)
                    }
                    _combinedRegistration = newCombinedRegistration;
                    _pendingSnapShots.UpdateIntervals(_combinedRegistration.SamplingWindow, _combinedRegistration.SamplingWindow + _combinedRegistration.CooldownPeriod, _combinedRegistration.CooldownPeriod);
                    foreach (var r in registrations)
                    {
                        lock (r)
                        {
                            var remainingCooldown = (r.SamplingWindow - _combinedRegistration.SamplingWindow) + r.CooldownPeriod - _combinedRegistration.CooldownPeriod;
                            if (remainingCooldown < _minimumConsumerCooldown)
                            {
                                remainingCooldown = _minimumConsumerCooldown;
                            }

                            r.PendingSnapshots?.UpdateIntervals(r.SamplingWindow, remainingCooldown, r.CooldownPeriod);
                        }
                    }
                }

                if (registration.ReportUnchangedProperties)
                {
                    EnsureBaseline(TheThingRegistry.GetThingByMID(registration.ThingMid, true), null);
                }

                if (registration.MaintainHistoryStore)
                {
                    _thingStream.RegisterEvent(eStoreEvents.HasUpdates, args => registration.HandleUpdates(this));
                }
            }
        }

        public TheHistoryParameters GetHistoryParameters(Guid historyToken)
        {
            if (!_consumerRegistrations.TryGetValue(historyToken, out ConsumerRegistration consumer) || consumer == null)
            {
                return null;
            }

            return new TheHistoryParameters(consumer);
        }

        public IEnumerable<ConsumerRegistration> GetConsumerRegistrations()
        {
            var consumers = new List<ConsumerRegistration>();
            foreach (var registration in _consumerRegistrations.GetDynamicEnumerable())
            {
                consumers.Add(new ConsumerRegistration(registration.Value));
            }
            return consumers;
        }

        public void UnregisterConsumer(Guid token)
        {
            ClearHistory(token);
            UnregisterConsumerInternal(token);
        }

        public void UnregisterConsumerInternal(Guid token)
        {
            lock (this)
            {
                _permanentConsumers.RemoveAnItemByID(token, null);
                if (_consumerRegistrations.TryGetValue(token, out ConsumerRegistration registration))
                {
                    registration.Delete();
                }
                _consumerRegistrations.RemoveNoCare(token);
                if (_consumerRegistrations.Count == 0)
                {
                    _thingStream?.RemoveStore(false);
                    _thingStream = null;
                }
            }
        }

        private TheStorageMirrorHistorian()
        {
        }

        public bool Disabled { get; set; }

        public void ClearAllHistory()
        {
            _thingStream?.RemoveAllItems();
        }

        public Task<bool> UnregisterAllConsumersForOwnerAsync(TheThing tOwner)
        {
            bool success = false;
            try
            {
                foreach (var consumer in _consumerRegistrations.Where(r => r.Value.OwnerThingMID == tOwner.cdeMID))
                {
                    UnregisterConsumer(consumer.Key);
                }
                success = true;
            }
            catch { }
            return TheCommonUtils.TaskFromResult(success);
        }

        public Task<bool> DeleteAsync()
        {
            bool success = false;
            try
            {
                ClearAllHistory();
                foreach (var consumer in _consumerRegistrations.GetDynamicEnumerable())
                {
                    UnregisterConsumerInternal(consumer.Key);
                }

                ClearAllHistory();
                _thingStream?.RemoveStore(false);
                success = true;
            }
            catch { }
            return TheCommonUtils.TaskFromResult(success);
        }

        public bool RestartHistory<T>(Guid token, TheHistoryParameters historyParameters, TheStorageMirror<T> store) where T : TheDataBase, INotifyPropertyChanged, new()
        {
            if (!_consumerRegistrations.TryGetValue(token, out ConsumerRegistration consumer) || consumer == null)
            {
                return false;
            }

            if (historyParameters != null)
            {
                if (!consumer.IsCompatible(historyParameters))
                {
                    UnregisterConsumer(token);
                    return false;
                }
                // Future: Update existing registration once we allow changes. Assumption for now IsCompatible() checks for equality
            }

            // Go back to the last truncated item so it will be re-offerd on the next GetHistory call
            consumer.LastSequenceNumberRead = consumer.SequenceNumberTruncated;
            consumer.LastAccess = DateTimeOffset.Now;
            if (consumer.MaintainHistoryStore)
            {
                var consumerWithStore = new ConsumerRegistrationWithStore<T>(consumer, store);
                _permanentConsumers.UpdateItem(consumerWithStore);
                AddOrUpdateRegistration(consumerWithStore);
            }
            else if (consumer.Persistent)
            {
                _permanentConsumers.UpdateItem(consumer);
            }

            var crWithStore = consumer as ConsumerRegistrationWithStore<T>;
            crWithStore?.Init(store);

            return true;
        }

        public void ClearHistory(Guid token)
        {
            if (!_consumerRegistrations.TryGetValue(token, out ConsumerRegistration consumer) || consumer == null)
            {
                return;
            }

            ClearHistoryInternal(consumer);
        }

        private void ClearHistoryInternal(ConsumerRegistration consumer)
        {
            lock (consumer)
            {
                consumer.SequenceNumberTruncated = consumer.LastSequenceNumberRead;
                if (consumer.ReportInitialValues == true && consumer.InitialValuesReported != true && consumer._highestInitialValueSequenceNumber < consumer.SequenceNumberTruncated && consumer._highestInitialValueSequenceNumber != 0)
                {
                    // Ensure that this consumer doesn't get initial values from other consumers (this can race if a consumer didn't consumer their initial values before another consumer came in, but duplicate are ok with at least once deliveray semantics)
                    consumer.InitialValuesReported = true;
                }
                consumer.LastAccess = DateTimeOffset.Now;
                if (consumer.Persistent)
                {
                    _permanentConsumers.UpdateItem(consumer);
                }
                ClearUnusedHistory();
            }
        }
        static TimeSpan _defaultTokenExpiration = new TimeSpan(7, 0, 0, 0);
        DateTimeOffset _lastHistoryClear;
        private void ClearUnusedHistory()
        {
            var thingStream = _thingStream;
            if (thingStream == null)
            {
                return;
            }
            var mirrorRWLock = thingStream.MyRecordsRWLock;
            if (mirrorRWLock == null)
            {
                return;
            }
            var now = DateTimeOffset.Now;
            _lastHistoryClear = now;
            lock (_consumerRegistrations)
            {
                foreach (var consumer in _consumerRegistrations.Values)
                {
                    var tokenExpiration = consumer.TokenExpiration ?? _defaultTokenExpiration;
                    if (tokenExpiration <= TimeSpan.Zero)
                    {
                        tokenExpiration = _defaultTokenExpiration;
                    }
                    if (consumer.LastAccess.Add(tokenExpiration) < now && TheBaseAssets.MyServiceHostInfo.cdeCTIM.AddSeconds(5 * 60) < now)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7692, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "History Token expired", eMsgLevel.l2_Warning, $"Consumer: {consumer.Token}. Expiration: {consumer.TokenExpiration}. Last Access: {consumer.LastAccess}"));
                        UnregisterConsumerInternal(consumer.Token);
                    }
                }
            }
            long lowestSequenceNumberTruncated = _consumerRegistrations.Any() ? _consumerRegistrations.Min((a) => a.Value.SequenceNumberTruncated) : 0;
            if (lowestSequenceNumberTruncated != 0)
            {
                mirrorRWLock.RunUnderUpgradeableReadLock(() =>
                //lock (mirrorLock)
                {
                    if (_combinedRegistration.ReportUnchangedProperties)
                    {
                        // Make sure we leave a full snapshot
                        TheThingStore baseLine = thingStream.FindLastItemAtOrBeforeSequenceNumber(lowestSequenceNumberTruncated, item => item?.IsFullSnapshot == true, out long baselineSequenceNumber);
                        if (baselineSequenceNumber == 0 || baseLine == null)
                        {
                            // TODO: No full baseline: unexpected, unless a more demanding consumer was registered later?? Need to avoid this
                            // Mitigation: use first item as baseline
                            TheBaseAssets.MySYSLOG.WriteToLog(7679, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Internal error: no baseline with consumer with ReportUnchangedProperties during clear history", eMsgLevel.l1_Error, $"First consumer: {GetCombinedConsumerInfoForLog()}"));
                            baseLine = thingStream.GetItems(lowestSequenceNumberTruncated, out var baselineSequenceNumberReturned).FirstOrDefault();
                            if (baselineSequenceNumberReturned != baselineSequenceNumber)
                            {
                                // Data loss detected: but will be reported to consumers when retrieving history, not during clearhistory
                            }
                            baselineSequenceNumber = baselineSequenceNumberReturned;
                        }

                        // Compute a new baseline
                        bool bBaseLineUpdated = false;
                        var itemsToProcess = thingStream.GetItems(thingStream.GetNextSequenceNumber(baselineSequenceNumber), out var firstSequenceNumberReturned);
                        if (firstSequenceNumberReturned != thingStream.GetNextSequenceNumber(baselineSequenceNumber))
                        {
                            // Data loss detected: but will be reported to consumers when retrieving history, not during clearhistory
                        }
                        foreach (var item in itemsToProcess)
                        {
                            if (baselineSequenceNumber >= lowestSequenceNumberTruncated)
                            {
                                break;
                            }
                            if (item != null)
                            {
                                var newBaseLine = item.CloneForThingSnapshot(_combinedRegistration.ReportUnchangedProperties ? baseLine : null, false, _combinedRegistration.Properties, _combinedRegistration.PropertiesToExclude, false);
                                if (newBaseLine != null)
                                {
                                    bBaseLineUpdated = true;
                                    baseLine = newBaseLine;
                                }
                            }
                            baselineSequenceNumber = thingStream.GetNextSequenceNumber(baselineSequenceNumber);
                        }
                        if (bBaseLineUpdated)
                        {
                            if (baseLine.cdeEXP >= 0)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(7680, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Internal error: baseline for consumer with ReportUnchangedProperties was not marked as cdeEXP == -1", eMsgLevel.l1_Error, $"First consumer: {GetCombinedConsumerInfoForLog()}"));
                                baseLine.cdeEXP = -1;
                            }
                            thingStream.SetBaseLine(baseLine);
                        }
                        lowestSequenceNumberTruncated = thingStream.GetPreviousSequenceNumber(baselineSequenceNumber); // Keep the baseline item
                    }
                    thingStream.DeleteItemsUpTo(lowestSequenceNumberTruncated);
                });
            }
            //_permanentConsumers.MyMirrorCache.SaveCacheToDisk(false, false);
        }

        private string GetCombinedConsumerInfoForLog()
        {
            try
            {
                return _consumerRegistrations?.FirstOrDefault().Value?.ToString();
            }
            catch { }
            return "error";
        }

        public bool SetHistoryCursor(Guid token, DateTimeOffset start)
        {
            if (!_consumerRegistrations.TryGetValue(token, out ConsumerRegistration consumer) || consumer == null)
            {
                return false;
            }

            // Reset the consumer's read cursor
            var sequenceNumber = _thingStream?.GetOffsetByTimestamp(start);
            if (sequenceNumber == 0 || sequenceNumber == null)
            {
                // No such timestamp found: can't set the cursor
                return false;
            }
            consumer.LastSequenceNumberRead = sequenceNumber.Value;
            consumer.LastAccess = DateTimeOffset.Now;
            if (consumer.Persistent)
            {
                _permanentConsumers.UpdateItem(consumer);
            }
            //_permanentConsumers.MyMirrorCache.SaveCacheToDisk(false, false);
            return true;
        }

        public Task<TheHistoryResponse> GetHistoryAsync(Guid token, int maxCount, int minCount, TimeSpan timeout, CancellationToken? cancelToken, bool clearHistory)
        {
            if (!_consumerRegistrations.TryGetValue(token, out ConsumerRegistration consumer) || consumer == null || consumer.MaintainHistoryStore)
            {
                return TheCommonUtils.TaskFromResult<TheHistoryResponse>(null);
            }
            return GetHistoryInternalAsync(consumer, maxCount, minCount, timeout, cancelToken, clearHistory);
        }

        TimeSpan _minimumConsumerCooldown = TimeSpan.Zero;

#if !CDE_NET4
        async
#endif
        private Task<TheHistoryResponse> GetHistoryInternalAsync(ConsumerRegistration consumer, int maxCount, int minCount, TimeSpan timeout, CancellationToken? cancelToken, bool clearHistory)
        {
            TheHistoryResponse response = null;
            TaskCompletionSource<bool> taskCS = null;
            void storeUpdated(StoreEventArgs strevnt)
            {
                taskCS?.TrySetResult(true);
            }

            try
            {
                response = new TheHistoryResponse { HistoryItems = new List<TheThingStore>() };
                long endTimeUtcTicks;

                if (consumer.PendingSnapshots == null)
                {
                    var remainingCooldown = (consumer.SamplingWindow - _combinedRegistration.SamplingWindow) + consumer.CooldownPeriod - _combinedRegistration.CooldownPeriod;
                    if (remainingCooldown < _minimumConsumerCooldown)
                    {
                        remainingCooldown = _minimumConsumerCooldown;
                    }
                    consumer.PendingSnapshots = new SnapshotManager(consumer.SamplingWindow, remainingCooldown, consumer.CooldownPeriod);
                }

                if (timeout == TimeSpan.Zero)
                {
                    endTimeUtcTicks = DateTimeOffset.MinValue.UtcTicks;
                }
                else
                {
                    // Avoid waiting infinitely for store update events, just in case the event handlers get tampered with by some other plug-in etc.
                    var maxTimeout = new TimeSpan(0, 15, 0);
                    if (timeout == TimeSpan.MaxValue || timeout > maxTimeout)
                    {
                        timeout = maxTimeout;
                    }
                    endTimeUtcTicks = DateTimeOffset.Now.UtcTicks + timeout.Ticks;
                }

                var sw = new Stopwatch();
                sw.Start();
                bool bRetry;

                // Keep an internal high water mark to use across retries
                long lastSequenceNumberRead = 0;

                do
                {
                    var thingStream = _thingStream;
                    var mirrorRWLock = thingStream?.MyRecordsRWLock;

                    if (thingStream == null || mirrorRWLock == null)
                    {
                        break;
                    }

                    bRetry = false;
                    long nextSnapshotDueUtcTicks = DateTimeOffset.MaxValue.UtcTicks;
                    lock (consumer) // Ensure this does get run concurrently for the same token/consumer
                    {
                        if (lastSequenceNumberRead ==0)
                        {
                            // Use consumer's high water mark for first iteration
                            lastSequenceNumberRead = consumer.LastSequenceNumberRead;
                        }
                        mirrorRWLock.RunUnderReadLock(() =>
                        {
                            TheThingStore baseItem = null;

                            var firstSequenceNumberToReturn = thingStream.GetNextSequenceNumber(lastSequenceNumberRead);

                            long sequenceNumberToProcess;
                            if (consumer.ReportUnchangedProperties)
                            {
                                // Find the last full snapshot,so that we can process all items since then to get a proper baseline
                                baseItem = thingStream.FindLastItemAtOrBeforeSequenceNumber(lastSequenceNumberRead, item => item?.IsFullSnapshot == true, out sequenceNumberToProcess);
                                if (baseItem == null)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(7681, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Internal error: no baseline for consumer with ReportUnchangedProperties", eMsgLevel.l1_Error, $"{consumer}"));
                                }
                            }
                            else
                            {
                                sequenceNumberToProcess = firstSequenceNumberToReturn;
                            }
                            var items = thingStream.GetItems(sequenceNumberToProcess, out var firstSequenceNumberReturned);
                            if (firstSequenceNumberReturned != sequenceNumberToProcess && sequenceNumberToProcess > 0)
                            {
                                response.DatalossDetected = true;
                                sequenceNumberToProcess = firstSequenceNumberReturned;
                            }
                            response.PendingItemCount = items.Count();

                            int count = response.HistoryItems.Count;
                            bool bHaveBaseline = !consumer.ReportUnchangedProperties;
                            foreach (var item in items)
                            {
                                if (!bHaveBaseline && sequenceNumberToProcess >= firstSequenceNumberToReturn)
                                {
                                    // We have reached the point from which the consumer wants to receive items
                                    bHaveBaseline = true;
                                }

                                var bIsInitialValue = item?.IsInitialValue == true;

                                if (   item != null // Deleted items may appear as NULL
                                    && (item.cdeEXP != -2 || consumer.ReportUnchangedProperties) // Ignore baseline items unless this consumer wants unchanged properties (necessary to send potentially articial property updates with timestamps that did not get generated)
                                    && (!bIsInitialValue || (consumer.ReportInitialValues == true && consumer.InitialValuesReported != true)) // Ignore initial values unless this consumer wants them
                                    )
                                {
                                    if (bIsInitialValue && consumer.ReportInitialValues == true && consumer.InitialValuesReported != true)
                                    {
                                        if (sequenceNumberToProcess > consumer._highestInitialValueSequenceNumber)
                                        {
                                            consumer._highestInitialValueSequenceNumber = sequenceNumberToProcess;
                                        }
                                    }
                                    var itemToReturn = item.CloneForThingSnapshot(consumer.ReportUnchangedProperties ? baseItem : null, false, consumer.Properties, consumer.PropertiesToExclude, true, out var bUpdated);
                                    // Ignore items while we are establishing the snapshot baseline
                                    if (bHaveBaseline)
                                    {
                                        if (itemToReturn != null && itemToReturn.PB.Count > 0 && bUpdated)
                                        {
                                            consumer.PendingSnapshots.RunUnderLock(() =>
                                            {
                                                var pendingSnapshot = consumer.PendingSnapshots.GetSnapshot(itemToReturn.cdeCTIM, out var samplingItem);
                                                // For samplingwindow 0 , we must pass on each and every update to a property, even if the timestamps are identical (OPC alarms/events etc.)
                                                // -> Check if the samplingItem already contains any of the properties in the itemToReturn: if so, don't add to the existing snapshot
                                                if (pendingSnapshot != null && (consumer.SamplingWindow != TimeSpan.Zero || !samplingItem.PB.Keys.Intersect(itemToReturn.PB.Keys).Any()))
                                                {
                                                    // Inside sampling window:
                                                    if (sequenceNumberToProcess > pendingSnapshot.MaxSequenceNumber) // Don't process again if already in the snapshot: this happens when a previous call left incomplete buckets
                                                    {
                                                        // Add to previously gathered data
                                                        Aggregate(consumer, samplingItem, itemToReturn);
                                                        pendingSnapshot.MaxSequenceNumber = sequenceNumberToProcess;
                                                    }
                                                    else
                                                    {
                                                        TheBaseAssets.MySYSLOG.WriteToLog(7682, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(eEngineName.ThingService, "Historian Bucket: skipping item because it was already processed ", eMsgLevel.l6_Debug, $"{consumer}: Seq {sequenceNumberToProcess} MaxSeq {pendingSnapshot.MaxSequenceNumber} {itemToReturn.cdeCTIM.ToString("o")} {itemToReturn.PB.Count()}"));
                                                    }
                                                    if (samplingItem.cdeCTIM < itemToReturn.cdeCTIM)
                                                    {
                                                        samplingItem.cdeCTIM = itemToReturn.cdeCTIM;
                                                    }
                                                }
                                                else
                                                {
                                                    if (samplingItem != null)
                                                    {
                                                        TheBaseAssets.MySYSLOG.WriteToLog(7682, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ThingService, "Historian GetHistory Bucket Overflow: more than one property with same timestamp", eMsgLevel.l6_Debug, $"{consumer}: {samplingItem.cdeCTIM.ToString("o")} {samplingItem.PB.Keys.Intersect(itemToReturn.PB.Keys).Aggregate("", (s, v) => $"{s} {v}")}"));
                                                    }
                                                    // Outside of an existing Sampling Window or rejected for other reasons: start a new one, even if new item time is before current sampling window (i.e. out of order times or clock adjusting backwards on time sync or mix of historical and current data)
                                                    count++;
                                                    consumer.PendingSnapshots.AddSnapshot(itemToReturn, sequenceNumberToProcess);
                                                }
                                            });
                                        }
                                        else
                                        {
                                            var nextTruncated = thingStream.GetNextSequenceNumber(consumer.SequenceNumberTruncated);
                                            if (nextTruncated == sequenceNumberToProcess)
                                            {
                                                consumer.SequenceNumberTruncated = nextTruncated;
                                                if (consumer.LastSequenceNumberRead < consumer.SequenceNumberTruncated)
                                                {
                                                    consumer.LastSequenceNumberRead = consumer.SequenceNumberTruncated;
                                                }
                                            }
                                        }
                                        if (count >= maxCount)
                                        {
                                            break;
                                        }
                                    }
                                    // Use the new item as the base for the next one
                                    if (itemToReturn != null)
                                    {
                                        baseItem = itemToReturn;
                                    }
                                }
                                else
                                {
                                    // TODO Log this...
                                    // Someone tampered with the Historian's Storage Mirror (or there's a bug somewhere)
                                    if (item == null)
                                    {
                                        response.DatalossDetected = true;
                                    }
                                }
                                lastSequenceNumberRead = sequenceNumberToProcess; // Update local high-water mark. The consumer's LastSequenceNumberRead will be updated in ProcessFinalSnapshots below
                                sequenceNumberToProcess = thingStream.GetNextSequenceNumber(sequenceNumberToProcess);
                            }

                            consumer.PendingSnapshots.ProcessFinalSnapshots((t, sequenceNumber) =>
                            {
                                response.HistoryItems.Add(t);
                                response.PendingItemCount--;
                                if (sequenceNumber > consumer.LastSequenceNumberRead) // TODO handle sequence number roll over!
                                {
                                    consumer.LastSequenceNumberRead = sequenceNumber;
                                }
                                else
                                {

                                }
                                consumer.LastAccess = DateTimeOffset.Now;
                                if (consumer.Persistent)
                                {
                                    _permanentConsumers.UpdateItem(consumer);
                                }

                            }, out nextSnapshotDueUtcTicks);

                        });

                        consumer.LastAccess = DateTimeOffset.Now;
                    }

                    if (clearHistory && response.HistoryItems.Count > 0)
                    {
                        ClearHistoryInternal(consumer);
                    }

                    long nowUtcTicks;
                    if (TheBaseAssets.MasterSwitch && cancelToken?.IsCancellationRequested != true && response.HistoryItems.Count < minCount && (nowUtcTicks = DateTimeOffset.Now.UtcTicks) < endTimeUtcTicks)
                    {
                        // we don't have enough items yet: wait for new items to arrive or timeout

                        bRetry = true;

                        TimeSpan waitForNext;
                        if (nextSnapshotDueUtcTicks <= endTimeUtcTicks)
                        {
                            // another snapshot will come due before the timeout: wait until then or until new items arrive
                            waitForNext = new TimeSpan(nextSnapshotDueUtcTicks - nowUtcTicks);
                            if (waitForNext < TimeSpan.Zero)
                            {
                                waitForNext = TimeSpan.Zero;
                            }
                        }
                        else
                        {
                            // adjust the timeout for the time we already spent processing
                            timeout -= sw.Elapsed;
                            sw.Reset();
                            sw.Start();
                            waitForNext = timeout;
                        }

                        if (waitForNext > TimeSpan.Zero)
                        {
                            // Cancel any previously store update callbacks
                            if (taskCS != null)
                            {
                                taskCS.TrySetResult(false);
                            }
                            taskCS = new TaskCompletionSource<bool>();
                            thingStream.RegisterEvent(eStoreEvents.HasUpdates, storeUpdated);
#if !CDE_NET4
                            CancellationTokenRegistration? cancelRegistration = null;
                            try
                            {
                                if (cancelToken.HasValue)
                                {
                                    cancelRegistration = cancelToken.Value.Register(() => { taskCS.TrySetCanceled(); });
                                }
                                if (!thingStream.HasRegisteredEvents(eStoreEvents.HasUpdates))
                                {

                                }
                                thingStream.RegisterEvent(eStoreEvents.HasUpdates, storeUpdated);
                                await TheCommonUtils.TaskWaitTimeout(taskCS.Task, waitForNext, cancelToken).ConfigureAwait(false);
                            }
                            catch (TimeoutException) { } // Expected on timeout
                            catch (TaskCanceledException) { } // Expected on shutdown
                            finally
                            {
                                if (cancelRegistration.HasValue)
                                {
                                    cancelRegistration.Value.Dispose();
                                }
                            }
#else
                            try
                            {
                                TheCommonUtils.TaskWaitTimeout(taskCS.Task, timeout, cancelToken).Wait();
                            }
                            catch (TimeoutException) { } // Expected on timeout
                            catch (TaskCanceledException) { } // Expected on shutdown
#endif
                            //if (taskCS.Task.Status != TaskStatus.RanToCompletion || taskCS.Task.Result != true)
                            //{
                            //    bRetry = false;
                            //}
                        }
                    }
                } while (bRetry && TheBaseAssets.MasterSwitch);
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(7682, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Internal error while getting thing history", eMsgLevel.l1_Error, $"{consumer}:{e.ToString()}"));
                response = null;
            }
            finally
            {
                _thingStream?.UnregisterEvent(eStoreEvents.HasUpdates, storeUpdated);
            }
            LogHistorianOutput(consumer, response);
#if !CDE_NET4
            return response;
#else
            return TheCommonUtils.TaskFromResult(response);
#endif
        }

        public TheStorageMirror<T> GetHistoryStore<T>(Guid token) where T : TheDataBase, INotifyPropertyChanged, new()
        {
            if (!_consumerRegistrations.TryGetValue(token, out ConsumerRegistration registration) || registration == null)
            {
                return null;
            }
            var consumerRegistrationWithStore = registration as ConsumerRegistrationWithStore<T>;
            return consumerRegistrationWithStore?.GetStorageMirror();
        }

        public void Close()
        {
            _thingStream?.ForceSave();
        }

        readonly object _timerLock = new object();
        private void OnSnapshotTimer(object notUsed)
        {
            try
            {
                lock (_timerLock)
                {
                    long nextBucketDueUtcTicks = DateTimeOffset.MaxValue.UtcTicks;
                    lock (_pendingSnapShots)
                    {
                        _pendingSnapShots.ProcessFinalSnapshots((t, seqIgnored) =>
                            {
                                _thingStream?.AddAnItem(t);
                                //LogQueueInput(this._consumerRegistrations?.FirstOrDefault().Value?.ThingMid ?? Guid.Empty, t);
                                TheBaseAssets.MySYSLOG.WriteToLog(7683, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(eEngineName.ThingService, "Historian: first level queue out", eMsgLevel.l6_Debug, $"OUT: {t.cdeCTIM.ToString("o")} {t.PB.Aggregate("", (s, kv) => $"{s}{kv.Key},")}"));
                            },
                            out nextBucketDueUtcTicks);
                        if (_lastHistoryClear.AddSeconds(120) < DateTimeOffset.Now)
                        {
                            ClearUnusedHistory();
                        }
                    }
                    if (nextBucketDueUtcTicks < DateTimeOffset.MaxValue.UtcTicks)
                    {
                        var timetoNextCheck = new TimeSpan(nextBucketDueUtcTicks - DateTimeOffset.Now.UtcTicks);
                        if (timetoNextCheck < minTimerWait)
                        {
                            timetoNextCheck = minTimerWait; // Wait
                        }
                        if (timetoNextCheck > GetTotalSnapshotCooldownPeriod())
                        {
                            timetoNextCheck = GetTotalSnapshotCooldownPeriod();
                        }
                        if (!TheBaseAssets.MasterSwitch)
                        {
                            // Shutting down: keep draining any pending snapshots quickly. ProcessFinalSnapshots will already have drained them in most cases. This handles a race condition where the switch gets set during or after ProcessFinalSnapshots.
                            timetoNextCheck = new TimeSpan(0, 0, 1);
                        }
                        if (_snapshotTimer != null)
                        {
                            try
                            {
                                _snapshotTimer.Change(timetoNextCheck, TimeSpan.Zero);
                            }
                            catch (ObjectDisposedException)
                            {
                                _snapshotTimer = null;
                            }
                        }
                        if (_snapshotTimer == null)
                        {
                            _snapshotTimer = new System.Threading.Timer(OnSnapshotTimer, null, timetoNextCheck, TimeSpan.Zero);
                        }
                    }
                    else
                    {
                        var temp = _snapshotTimer;
                        _snapshotTimer = null;
                        temp.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                // ignore: don't crash the process
                try
                {
                    var temp = _snapshotTimer;
                    _snapshotTimer = null;
                    TheBaseAssets.MySYSLOG.WriteToLog(7683, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ThingService, "Historian: Internal error in OnSnapshotTimer", eMsgLevel.l1_Error, $"Thing: {this.GetCombinedConsumerInfoForLog()}. Error: {e.ToString()}"));
                    temp.Dispose();
                }
                catch { }
            }
        }

        readonly SnapshotManager _pendingSnapShots = new SnapshotManager(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);

        internal int TestHookGetPendingSnapShotCount()
        {
            return _pendingSnapShots.TestHookGetCount() + this.GetConsumerRegistrations().Aggregate(0, (s, cr) => s + cr.PendingSnapshots?.TestHookGetCount() ?? 0);
        }
        internal long TestHookGetHistoryItemCount()
        {
            var count = _thingStream?.CountTestHook ?? 0;
            if (_cacheHasSnapshot && count > 0)
            {
                count--;
            }
            return count;
        }


        private System.Threading.Timer _snapshotTimer;

        private readonly TimeSpan minTimerWait = new TimeSpan(0, 0, 0, 0, 15);

        TimeSpan GetTotalSnapshotCooldownPeriod()
        {
            return _combinedRegistration.CooldownPeriod + _combinedRegistration.SamplingWindow;
        }

        private bool _cacheHasSnapshot; // true: full snapshot has been added, false: snapshot not added or unknown

        // Record property changes to ensure we don't miss changes
        public void AddPropertySnapshot(TheThing tThing, string propertyNamePath, object propValue, DateTimeOffset propTime, long? propSEQ)
        {
            AddPropertySnapshotInternal(tThing, propertyNamePath, propValue, propTime, propSEQ, false);
        }
        internal void AddPropertySnapshotInternal(TheThing tThing, string propertyNamePath, object propValue, DateTimeOffset propTime, long? propSEQ, bool bIsInitialValue)
        {
            if (Disabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(propertyNamePath))
                return;
            // TODO Should we offer a cdeP method for this check?
            if (propertyNamePath.Contains(".[cdeSensor]") || propertyNamePath.Contains(".[cdeSource]") || propertyNamePath.Contains(".[cdeConfig]"))
            {
                return;
            }
            if ((_combinedRegistration.Properties?.Contains(propertyNamePath) == false || _combinedRegistration.PropertiesToExclude?.Contains(propertyNamePath) == true))
            {
                return;
            }

            DateTimeOffset now = propTime;
            if (now == DateTimeOffset.MinValue)
            {
                now = DateTimeOffset.Now;
            }

            lock (_pendingSnapShots)
            {
                var bucket = _pendingSnapShots.GetSnapshot(now, out TheThingStore snapshot);
                if (bucket != null
                    && propertyNamePath != null
                    && (_combinedRegistration.SamplingWindow != TimeSpan.Zero || !snapshot.PB.ContainsKey(propertyNamePath))) // For samplingwindows 0 all updates must be preserved, even if theycarry the same timestamp (i.e. OPC Alarms)
                {
                    // Inside a sampling window: add to the pending snapshot
                    if (!TSM.L(eDEBUG_LEVELS.FULLVERBOSE) && snapshot.PB.ContainsKey(propertyNamePath))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7683, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(eEngineName.ThingService, "Historian aggregated multiple values for property", eMsgLevel.l2_Warning, $"Thing: {tThing.FriendlyName} - {tThing.cdeMID} Property: {propertyNamePath}: {snapshot.cdeCTIM:O} changed to {now:O}. Possible culprits: {snapshot.PB.Aggregate("", (s, kv) => kv.Key != propertyNamePath ? $"{s}{kv.Key}," : s)}"));
                    }

                    Aggregate(_combinedRegistration, snapshot, propertyNamePath, propValue, propSEQ, tThing);
                    TheBaseAssets.MySYSLOG.WriteToLog(7683, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(eEngineName.ThingService, "Historian: first level queue in", eMsgLevel.l6_Debug, $"IN :{propTime.ToString("o")} {propertyNamePath},{propValue}"));

                    if (snapshot.cdeCTIM < now)
                    {
                        snapshot.cdeCTIM = now;
                    }
                }
                else
                {
                    snapshot = TheThingStore.CloneFromTheThingInternal(tThing, true, true,
                            false, // Only take full snapshot for first baseline record. subsequent are deltas only to save memory
                            _combinedRegistration.Properties,
                            _combinedRegistration.PropertiesToExclude);
                    if (bIsInitialValue)
                    {
                        snapshot.IsInitialValue = true;
                    }
                    Aggregate(_combinedRegistration, snapshot, propertyNamePath, propValue, propSEQ, tThing);
                    //thingSnapshot.PB[property.Name] = property.Value;

                    if (snapshot.cdeCTIM != now && snapshot.PB.Count > 1)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7686, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ThingService, "Historian timestamp changed by property", eMsgLevel.l2_Warning, $"Thing: {tThing.FriendlyName} - {tThing.cdeMID} Property: {propertyNamePath}: {snapshot.cdeCTIM:O} changed to {now:O}. Possible victims: {snapshot.PB.Aggregate("", (s, kv) => kv.Key != propertyNamePath ? $"{s}{kv.Key}," : s)}"));
                    }
                    snapshot.cdeCTIM = now; // All other properties are just baseline, so use the changed property's time for the snapshot

                    bucket = _pendingSnapShots.AddSnapshot(snapshot, 0);
                    TheBaseAssets.MySYSLOG.WriteToLog(7683, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(eEngineName.ThingService, "Historian: first level queue in", eMsgLevel.l6_Debug, $"NEW:{propTime.ToString("o")} {propertyNamePath},{propValue}"));

                    // Ensure a timer is running to flushed the snapshot after 10 seconds
                    if (_snapshotTimer == null)
                    {
                        var timerWait = GetTotalSnapshotCooldownPeriod();
                        if (timerWait < minTimerWait)
                        {
                            timerWait = minTimerWait;
                        }
                        _snapshotTimer = new System.Threading.Timer(OnSnapshotTimer, null, timerWait, TimeSpan.Zero);
                    }
                }
                LogHistorianInput(tThing, propertyNamePath, propValue, now, snapshot, bucket);
                if (_combinedRegistration.ReportUnchangedProperties && !_cacheHasSnapshot)
                {
                    EnsureBaseline(tThing, snapshot);
                }
            }
        }

        private static void LogHistorianInput(TheThing tThing, string propertyNamePath, object propValue, DateTimeOffset propTime, TheThingStore snapshot, SnapshotManager.PendingSnapshot bucket)
        {
            if (TheBaseAssets.MyServiceHostInfo.EnableHistorianDataLog)
            {
                var logEntry = new Dictionary<string, object>
                        {
                            { "LogTime", DateTimeOffset.Now },
                            { "PropTime", propTime },
                            { "ThingMID", tThing.cdeMID },
                            { "Name", propertyNamePath },
                            { "Value", propValue },
                            { "SnapshotTime", snapshot.cdeCTIM },
                            { "SnapshotPropCount",  snapshot.PB.Count },
                            { "BucketExpiration", new DateTimeOffset(new DateTime(bucket.SnapshotExpirationTimeUtcTicks, DateTimeKind.Utc).ToLocalTime()) },
                            { "MaxSeq", bucket.MaxSequenceNumber },
                        };
                if (bucket.OverflowSnapshots != null)
                {
                    logEntry.Add("BucketOverflow", bucket.OverflowSnapshots.Count);
                }
                TheCommonUtils.LogDataToFile($"historian_input{tThing.cdeMID.ToString()}.log", logEntry, "Historian", tThing.cdeMID.ToString());
            }
        }
        private static void LogHistorianOutput(ConsumerRegistration consumer, TheHistoryResponse response)
        {
            if (TheBaseAssets.MyServiceHostInfo.EnableHistorianDataLog)
            {
                Dictionary<string, object> logEntry = null;

                if (response.HistoryItems?.Any() == true)
                {
                    foreach (var item in response.HistoryItems)
                    {
                        logEntry = new Dictionary<string, object>
                        {
                            { "LogTime", DateTimeOffset.Now },
                            { "ItemTime", item.cdeCTIM },
                            { "ItemCount", response.HistoryItems.Count },
                            { "ThingMID", consumer.ThingMid },
                            { "Consumer", consumer.Token },
                            { "Item", item},
                            { "Pending", response.PendingItemCount },
                        };
                        if (response.DatalossDetected)
                        {
                            logEntry.Add("Dataloss", response.DatalossDetected);
                        }
                    }
                }
                else
                {
                    logEntry = new Dictionary<string, object>
                        {
                            { "LogTime", DateTimeOffset.Now },
                            { "ThingMID", consumer.ThingMid },
                            { "Consumer", consumer.Token },
                            { "ItemCount", response.HistoryItems.Count },
                            { "Pending", response.PendingItemCount },
                        };
                    if (response.DatalossDetected)
                    {
                        logEntry.Add("Dataloss", response.DatalossDetected);
                    }
                }
                if (logEntry != null)
                {
                    TheCommonUtils.LogDataToFile($"historian_output{consumer.ThingMid.ToString()}_{consumer.Token.ToString()}.log", logEntry, "Historian", consumer.ToString());
                }
            }
        }

        private static void LogQueueInput(Guid thingMID, TheThingStore item)
        {
            if (TheBaseAssets.MyServiceHostInfo.EnableHistorianDataLog)
            {
                Dictionary<string, object> logEntry = new Dictionary<string, object>
                {
                    { "LogTime", DateTimeOffset.Now },
                    { "ItemTime", item.cdeCTIM },
                    { "ThingMID", thingMID },
                    { "Item", item},
                };
                TheCommonUtils.LogDataToFile($"historian_queue_input{thingMID.ToString()}.log", logEntry, "Historian", thingMID.ToString());
            }
        }


        void EnsureBaseline(TheThing tThing, TheThingStore thingSnapshot)
        {
            var thingStream = _thingStream;
            if (thingStream == null)
            {
                return;
            }
            bool addFullSnapshot = true;
            if (!thingStream.IsEmpty())
            {
                addFullSnapshot = false;
                thingStream.MyRecordsRWLock.RunUnderUpgradeableReadLock(() =>
                {
                    var baseLine = thingStream.GetItems(0, out var ignoredSequenceNumber)?.FirstOrDefault(item => item?.IsFullSnapshot == true);
                    if (baseLine == null)
                    {
                        // No full snapshot yet: make sure we add it
                        addFullSnapshot = true;
                    }
                    else
                    {
                        // Make sure we have a pinned snapshot
                        if (baseLine.cdeEXP == 0)
                        {
                            var pinnedBaseLine = thingStream.GetItems(0, out var ignoredSequenceNumber2)?.FirstOrDefault(item => item?.IsFullSnapshot == true && item?.cdeEXP < 0);
                            if (pinnedBaseLine == null)
                            {
                                // No pinned snapshot: pin the first snapshot that we found
                                baseLine.cdeEXP = -1;
                                thingStream.PinBaseLine(baseLine);
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(7685, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Historian baseline item was not the first full snapshot", eMsgLevel.l2_Warning, $"Thing: {tThing.FriendlyName} - {tThing.cdeMID}. First consumer: {GetCombinedConsumerInfoForLog()} Pinned: {pinnedBaseLine.cdeCTIM:O} Full: {baseLine.cdeCTIM:O}."));
                            }
                        }
                        _cacheHasSnapshot = true;
                        addFullSnapshot = false;
                    }
                });
            }
            if (addFullSnapshot)
            {
                if (thingSnapshot == null || !thingSnapshot.IsFullSnapshot)
                {
                    thingSnapshot = TheThingStore.CloneFromTheThingInternal(tThing, true, true,
                            true, // Only take full snapshot for first baseline record. subsequent are deltas only to save memory
                            _combinedRegistration.Properties,
                            _combinedRegistration.PropertiesToExclude);
                }
                thingSnapshot.cdeEXP = -2; // This marks the entry as not subject to expiration due to size nor time constraints, and as to be ignored by consumers with ReportUnchangedProperties = false. This is critical because this snapshot can have properties with timestamps that were never generated
                thingStream.AddAnItem(thingSnapshot);
                _cacheHasSnapshot = true;
            }
        }

        internal interface ISnapshotAggregator
        {
            object CreateStats(string propName, object propValue);
            object ReadStats(TheHistoryParameters registration, TheThingStore thingSnapshot, string propName);
            object Aggregate(object stats, object newStats);
            void WriteStats(object stats, TheHistoryParameters registration, TheThingStore thingSnapshot, string propName);
        }

        class TheSnapshotAggregator : ISnapshotAggregator
        {
            public object Aggregate(object stats, object newStats)
            {
                (stats as TheValueStats)?.Aggregate(newStats as TheValueStats);
                return stats;
            }

            public object CreateStats(string propName, object propValue)
            {
                return new TheValueStats(propValue);
            }

            public object ReadStats(TheHistoryParameters registration, TheThingStore thingSnapshot, string propName)
            {
                return TheValueStats.ReadStats(registration, thingSnapshot, propName);
            }

            public void WriteStats(object stats, TheHistoryParameters registration, TheThingStore thingSnapshot, string propName)
            {
                (stats as TheValueStats)?.WriteStats(registration, thingSnapshot, propName);
            }
        }

        class TheValueStats
        {
            public double? avg;
            public double? min;
            public double? max;
            public long? n;

            public TheValueStats()
            {
            }

            public TheValueStats(object propValue)
            {
                var valueAsDouble = TheCommonUtils.CDbl(propValue);
                avg = valueAsDouble;
                min = valueAsDouble;
                max = valueAsDouble;
                n = 1;
            }

            public void Aggregate(TheValueStats newStats)
            {
                if (newStats == null)
                {
                    return;
                }
                long newN = n.GetValueOrDefault() + newStats.n.GetValueOrDefault();
                avg = (avg.GetValueOrDefault() * n.GetValueOrDefault() + newStats.avg.GetValueOrDefault() * newStats.n.GetValueOrDefault()) / newN;
                n = newN;
                min = Math.Min(min.GetValueOrDefault(long.MaxValue), newStats.min.GetValueOrDefault(long.MaxValue));
                max = Math.Max(max.GetValueOrDefault(long.MinValue), newStats.max.GetValueOrDefault(long.MinValue));
            }

            static string GetEscapedPropName(string propName)
            {
                if (propName.StartsWith("["))
                {
                    if (propName.EndsWith("].[N]") || propName.EndsWith("].[Avg]") || propName.EndsWith("].[Min]") || propName.EndsWith("].[Max]"))
                    {
                        // Don't aggregate the aggregates
                        return null;
                    }
                    return propName;
                }
                return $"[{propName}]";
            }
            public static TheValueStats ReadStats(TheHistoryParameters registration, TheThingStore thingSnapshot, string propName)
            {
                var escapedPropname = GetEscapedPropName(propName);
                if (String.IsNullOrEmpty(escapedPropname))
                    return null;

                var stats = new TheValueStats();
                if (registration.ComputeAvg || registration.ComputeN)
                {
                    if (thingSnapshot.PB.TryGetValue($"{escapedPropname}.[N]", out object value))
                    {
                        stats.n = TheCommonUtils.CLng(value);
                    }

                    if (registration.ComputeAvg)
                    {
                        if (thingSnapshot.PB.TryGetValue($"{escapedPropname}.[Avg]", out value))
                        {
                            stats.avg = TheCommonUtils.CDbl(value);
                        }
                    }
                }

                if (registration.ComputeMin)
                {
                    if (thingSnapshot.PB.TryGetValue($"{escapedPropname}.[Min]", out object value))
                    {
                        stats.min = TheCommonUtils.CDbl(value);
                    }
                }
                if (registration.ComputeMax)
                {
                    if (thingSnapshot.PB.TryGetValue($"{escapedPropname}.[Max]", out object value))
                    {
                        stats.max = TheCommonUtils.CDbl(value);
                    }
                }
                return stats;
            }
            public void WriteStats(TheHistoryParameters registration, TheThingStore thingSnapshot, string propName)
            {
                var escapedPropname = GetEscapedPropName(propName);
                if (String.IsNullOrEmpty(escapedPropname))
                    return;

                if (registration.ComputeAvg || registration.ComputeN)
                {
                    thingSnapshot.PB[$"{escapedPropname}.[N]"] = n;

                    if (registration.ComputeAvg)
                    {
                        thingSnapshot.PB[$"{escapedPropname}.[Avg]"] = avg;
                    }
                }

                if (registration.ComputeMin)
                {
                    thingSnapshot.PB[$"{escapedPropname}.[Min]"] = min;
                }
                if (registration.ComputeMax)
                {
                    thingSnapshot.PB[$"{escapedPropname}.[Max]"] = max;
                }
            }
        }

        static readonly ISnapshotAggregator globalAggregator = new TheSnapshotAggregator();

        private void Aggregate(TheHistoryParameters registration, TheThingStore thingSnapshot, TheThingStore thingSnapshotNew)
        {
            foreach (var prop in thingSnapshotNew.PB)
            {
                object statsNew = null;
                if (registration.ComputeAggregates)
                {
                    statsNew = globalAggregator.ReadStats(registration, thingSnapshotNew, prop.Key);
                }
                AggregateInternal(registration, thingSnapshot, prop.Key, prop.Value, statsNew);
            }
            // Always use the sequence number of the last item to go into a snapshot (usually the highest)
            if (thingSnapshotNew.cdeSEQ.HasValue)
            {
                thingSnapshot.cdeSEQ = thingSnapshotNew.cdeSEQ;
            }
        }

        private void Aggregate(TheHistoryParameters registration, TheThingStore thingSnapshot, string propNamePath, object propValue, long? propSEQ, TheThing propThing)
        {
            object statsNew = null;
            if (_combinedRegistration.ComputeAggregates)
            {
                statsNew = globalAggregator.CreateStats(propNamePath, propValue);
            }
            if (propSEQ.HasValue)
            {
                thingSnapshot.cdeSEQ = propSEQ;
            }
            else
            {
                var thingSEQ = propThing?.cdeSEQ;
                if (thingSEQ != null)
                {
                    thingSnapshot.cdeSEQ = thingSEQ;
                }
            }
            AggregateInternal(registration, thingSnapshot, propNamePath, propValue, statsNew);
        }

        private void AggregateInternal(TheHistoryParameters registration, TheThingStore thingSnapshot, string propName, object lastValue, object statsNew)
        {
            if (_combinedRegistration.ComputeAggregates && statsNew != null)
            {
                var stats = globalAggregator.ReadStats(registration, thingSnapshot, propName);
                var mergedStats = globalAggregator.Aggregate(stats, statsNew);
                globalAggregator.WriteStats(mergedStats, registration, thingSnapshot, propName);
            }
            // Last value wins
            thingSnapshot.PB[propName] = lastValue;
        }
    }

    class SnapshotManager
    {
        private TimeSpan _sampleInterval;
        private TimeSpan _remainingCooldownPeriod;
        private TimeSpan _cooldownWindow;

        public class PendingSnapshot
        {
            public TheThingStore ThingSnapshot;
            public long SnapshotExpirationTimeUtcTicks;
            public long MaxSequenceNumber { get; set; }
            public List<TheThingStore> OverflowSnapshots; // Enables capturing multiple values for the same property and same timestamp (required for complete history, i.e. OPC Alarms)
        }

        readonly Dictionary<long, PendingSnapshot> _pendingSnapShots = new Dictionary<long, PendingSnapshot>();

        public SnapshotManager(TimeSpan sampleInterval, TimeSpan remainingCooldownPeriod, TimeSpan cooldownWindow)
        {
            _sampleInterval = sampleInterval;
            _remainingCooldownPeriod = remainingCooldownPeriod;
            _cooldownWindow = cooldownWindow;
        }

        public void UpdateIntervals(TimeSpan sampleInterval, TimeSpan remainingCooldownPeriod, TimeSpan cooldownWindow)
        {
            if (sampleInterval != _sampleInterval || remainingCooldownPeriod != _remainingCooldownPeriod || cooldownWindow != _cooldownWindow)
            {
                lock (_pendingSnapShots)
                {
                    var oldPendingSnapshots = new Dictionary<long, PendingSnapshot>(_pendingSnapShots);
                    _sampleInterval = sampleInterval;
                    _remainingCooldownPeriod = remainingCooldownPeriod;
                    _cooldownWindow = cooldownWindow;
                    _pendingSnapShots.Clear();
                    foreach (var snapshotKV in oldPendingSnapshots)
                    {
                        AddSnapshot(snapshotKV.Value.ThingSnapshot, snapshotKV.Value.MaxSequenceNumber);
                        if (snapshotKV.Value.OverflowSnapshots != null)
                        {
                            foreach (var snapshot in snapshotKV.Value.OverflowSnapshots)
                            {
                                AddSnapshot(snapshot, snapshotKV.Value.MaxSequenceNumber);
                            }
                        }
                    }
                }
            }
        }

        long GetSamplingWindowStart(DateTimeOffset timestamp)
        {
            return GetSamplingWindowStart(timestamp.UtcTicks, _sampleInterval);
        }

        static long GetSamplingWindowStart(long timestampUtcTicks, TimeSpan sampleInterval)
        {
            var samplingWindowSize = sampleInterval.Ticks;
            long samplingWindowStart;
            if (samplingWindowSize > 0)
            {
                samplingWindowStart = (timestampUtcTicks / samplingWindowSize) * samplingWindowSize;
            }
            else
            {
                samplingWindowStart = timestampUtcTicks;
            }
            return samplingWindowStart;
        }

        static long GetSamplingWindowEnd(long timestampUtcTicks, TimeSpan sampleInterval)
        {
            return GetSamplingWindowStart(timestampUtcTicks, sampleInterval) + sampleInterval.Ticks;
        }

        public PendingSnapshot AddSnapshot(TheThingStore snapshot, long sequenceNumber)
        {
            PendingSnapshot pendingSnapshotToReturn;
            {
                var windowStart = GetSamplingWindowStart(snapshot.cdeCTIM);
                //if (timeRedirects.TryGetValue(windowStart, out var newValue))
                //{
                //    if (newValue > windowStart)
                //    {
                //        windowStart =
                //    }
                //}
                lock (_pendingSnapShots)
                {
                    if (!_pendingSnapShots.TryGetValue(windowStart, out var previousSnapshot))
                    {
                        var pendingSnapshot = new PendingSnapshot
                        {
                            ThingSnapshot = snapshot,
                            SnapshotExpirationTimeUtcTicks = GetExpirationTimeFromNow(),
                            MaxSequenceNumber = sequenceNumber,
                        };
                        _pendingSnapShots.Add(windowStart, pendingSnapshot);
                        pendingSnapshotToReturn = pendingSnapshot;
                    }
                    else
                    {
                        if (previousSnapshot.ThingSnapshot.cdeMID != snapshot.cdeMID && previousSnapshot.OverflowSnapshots?.FirstOrDefault(s => s.cdeMID == snapshot.cdeMID) == null)
                        {
                            if (previousSnapshot.MaxSequenceNumber < sequenceNumber)
                            {
                                previousSnapshot.MaxSequenceNumber = sequenceNumber;
                            }
                            if (previousSnapshot.OverflowSnapshots == null)
                            {
                                previousSnapshot.OverflowSnapshots = new List<TheThingStore>();
                            }
                            previousSnapshot.OverflowSnapshots.Add(snapshot);
                        }
                        pendingSnapshotToReturn = previousSnapshot;
                    }
                }
            }
            return pendingSnapshotToReturn;
        }

        private long GetExpirationTimeFromNow()
        {
            return GetSamplingWindowEnd(DateTimeOffset.Now.UtcTicks + _remainingCooldownPeriod.Ticks, _cooldownWindow);
        }

        public PendingSnapshot GetSnapshot(DateTimeOffset timestamp, out TheThingStore snapshot)
        {
            var windowStart = GetSamplingWindowStart(timestamp);
            lock (_pendingSnapShots)
            {
                if (_pendingSnapShots.TryGetValue(windowStart, out var pendingSnapshot))
                {
                    if (pendingSnapshot.OverflowSnapshots == null || !pendingSnapshot.OverflowSnapshots.Any())
                    {
                        snapshot = pendingSnapshot.ThingSnapshot;
                    }
                    else
                    {
                        snapshot = pendingSnapshot.OverflowSnapshots.Last();
                    }
                    return pendingSnapshot;
                }
            }
            snapshot = null;
            return null;
        }

        public void ProcessFinalSnapshots(Action<TheThingStore,long> callback, out long nextBucketDueUtcTicks)
        {
            nextBucketDueUtcTicks = DateTimeOffset.MaxValue.UtcTicks;
            var snapshotsToRemove = new List<KeyValuePair<long, PendingSnapshot>>();
            lock (_pendingSnapShots)
            {
                var now = DateTimeOffset.Now;
                foreach (var snapshotKV in _pendingSnapShots)
                {
                    var snapshotExpirationTimeUtcTicks = snapshotKV.Value.SnapshotExpirationTimeUtcTicks;
                    if (snapshotExpirationTimeUtcTicks <= now.UtcTicks)
                    {
                        snapshotsToRemove.Add(snapshotKV);
                    }
                    else
                    {
                        if (snapshotExpirationTimeUtcTicks < nextBucketDueUtcTicks)
                        {
                            nextBucketDueUtcTicks = snapshotExpirationTimeUtcTicks;
                        }
                    }
                }

                foreach (var pendingSnapshotKV in snapshotsToRemove.OrderBy(kv => kv.Key))
                {
                    var pendingSnapshot = pendingSnapshotKV.Value;
                    callback?.Invoke(pendingSnapshot.ThingSnapshot, pendingSnapshot.MaxSequenceNumber);
                    if (pendingSnapshot.OverflowSnapshots != null)
                    {
                        foreach (var snapshot in pendingSnapshot.OverflowSnapshots)
                        {
                            callback?.Invoke(snapshot, pendingSnapshot.MaxSequenceNumber);
                        }
                    }
                    _pendingSnapShots.Remove(pendingSnapshotKV.Key);
                }
            }

        }

        public void RunUnderLock(Action action)
        {
            lock (_pendingSnapShots)
            {
                action();
            }
        }

        internal int TestHookGetCount()
        {
            lock (_pendingSnapShots)
            {
                return _pendingSnapShots.Count;
            }
        }
    }



    interface IThingStream
    {
        void AddAnItem(TheThingStore thingSnapshot);
        TheThingStore FindLastItemAtOrBeforeSequenceNumber(long lowestSequenceNumberTruncated, Func<TheThingStore, bool> p, out long baselineSequenceNumber);
        IEnumerable<TheThingStore> GetItems(long startSequenceNumber, out long firstSequenceNumberReturned);
        void DeleteItemsUpTo(long sequenceNumberToTruncate);
        bool IsEmpty();
        long CountTestHook { get; }
        //void UpdateItem(TheThingStore baseLine);

        void SetBaseLine(TheThingStore baseLine);
        void PinBaseLine(TheThingStore baseLine);
        IReaderWriterLock MyRecordsRWLock { get; }
        long MaxCount { get; set; }
        TimeSpan MaxAge { get; set; }
        bool IsPersistent { get; set; }

        long GetOffsetByTimestamp(DateTimeOffset start);
        /// <summary>
        ///  Return the sequence number of the last item
        /// </summary>
        /// <returns></returns>
        long GetLastSequenceNumber();
        long GetNextSequenceNumber(long sequenceNumber);
        long GetPreviousSequenceNumber(long sequenceNumber);

        Action<StoreEventArgs> RegisterEvent(string hasUpdates, Action<StoreEventArgs> storeUpdated);
        bool HasRegisteredEvents(string hasUpdates);
        void UnregisterEvent(string hasUpdates, Action<StoreEventArgs> storeUpdated);

        void ForceSave();
        void RemoveAllItems();
        void RemoveStore(bool backupFirst);
    }

    internal class StorageMirrorThingUpdateStream : IThingStream
    {
        private readonly TheStorageMirror<TheThingStore> _storageMirror;
        private Guid thingMid;
        private long maxCount;
        private TimeSpan maxAge;

        public StorageMirrorThingUpdateStream(bool persistent, Guid thingMid, long maxCount, TimeSpan maxAge) //, bool pIsRAMStore=true, bool pIsCached=true, string pFriendlyName="", string pDescription="") required if store should go to SQL Server some day
        {
            this.thingMid = thingMid;
            this.maxCount = maxCount;
            this.maxAge = maxAge;

            // Migration from previous buffer names
            var tableName = "ThingHistory" + TheCommonUtils.cdeGuidToString(thingMid);
            if (!TheCommonUtils.cdeFileExists(TheCommonUtils.cdeFixupFileName($"ClientBin/cache/{tableName}", true))) // Use legacy names if a file has been found
            {
                tableName = "ThingUpdateStream" + TheCommonUtils.cdeGuidToString(thingMid);
            }
            var mirrorThingStore = new TheStorageMirror<TheThingStore>(TheCDEngines.MyIStorageService)
            {
                CacheTableName = tableName,
                IsRAMStore = true, // pIsRAMStore,
                IsCached = true, //pIsCached,
                CacheStoreInterval = 10,
                IsStoreIntervalInSeconds = true,
                IsCachePersistent = persistent, // !pIsRAMStore? false: persistent,
                UseSafeSave = true,
                AllowFireUpdates = true,
                BlockWriteIfIsolated = true,
            };
            mirrorThingStore.SetMaxStoreSize((int)maxCount);
            mirrorThingStore.SetRecordExpiration(maxAge.TotalSeconds > int.MaxValue ? int.MaxValue : (int)maxAge.TotalSeconds, null);
            mirrorThingStore.MyMirrorCache.TrackInsertionOrder();
            mirrorThingStore.MyMirrorCache.FastSaveLock = TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("StorageMirrorFastSave"));

            //mirrorThingStore.CreateStore(new TheStorageMirrorParameters
            //{
            //    IsRAMStore = pIsRAMStore,
            //    IsCached = pIsCached,
            //    IsCachePersistent = !pIsRAMStore ? false : persistent,
            //    CanBeFlushed = false,
            //    ResetContent = false,
            //    LoadSync = true,
            //    DontRegisterInMirrorRepository = false,
            //    FriendlyName = pFriendlyName,
            //    Description = pDescription
            //}, null);
            mirrorThingStore.InitializeStore(false, false, true, true);

            _storageMirror = mirrorThingStore;
        }

        public long CountTestHook => _storageMirror?.Count ?? 0;

        public IReaderWriterLock MyRecordsRWLock => _storageMirror.MyRecordsRWLock;

        public long MaxCount
        {
            get => maxCount;
            set { maxCount = value; _storageMirror.SetMaxStoreSize((int)maxCount); }
        }
        public TimeSpan MaxAge
        {
            get => maxAge;
            set
            {
                maxAge = value;
                _storageMirror.SetRecordExpiration(value.TotalSeconds > int.MaxValue ? int.MaxValue : (int)value.TotalSeconds, null);
            }
        }
        public bool IsPersistent
        {
            get => _storageMirror.IsCachePersistent;
            set
            {
                _storageMirror.IsCachePersistent = value;
                _storageMirror.MyMirrorCache.IsCachePersistent = value;
            }
        }

        public void AddAnItem(TheThingStore thingSnapshot)
        {
            _storageMirror.AddAnItem(thingSnapshot);
        }

        public TheThingStore FindLastItemAtOrBeforeSequenceNumber(long lowestSequenceNumberTruncated, Func<TheThingStore, bool> p, out long baselineSequenceNumber)
        {
            return _storageMirror.MyMirrorCache.FindLastItemAtOrBeforeSequenceNumber(lowestSequenceNumberTruncated, p, out baselineSequenceNumber);
        }

        public void ForceSave()
        {
            _storageMirror.ForceSave();
        }

        public IEnumerable<TheThingStore> GetItems(long lowestSequenceNumberTruncated, out long firstSequenceNumberReturned)
        {
            return _storageMirror.MyMirrorCache.GetItemsByInsertionOrderInternal(lowestSequenceNumberTruncated, out firstSequenceNumberReturned);
        }

        public long GetNextSequenceNumber(long baselineSequenceNumber)
        {
            return _storageMirror.MyMirrorCache.GetNextSequenceNumber(baselineSequenceNumber);
        }

        public long GetOffsetByTimestamp(DateTimeOffset start)
        {
            return _storageMirror.MyMirrorCache.GetOffsetByTimestamp(start);
        }

        public long GetPreviousSequenceNumber(long baselineSequenceNumber)
        {
            return _storageMirror.MyMirrorCache.GetPreviousSequenceNumber(baselineSequenceNumber);
        }

        public bool IsEmpty()
        {
            if(_storageMirror.AppendOnly && !TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID)
            {
                try
                {
                    string[] files = Directory.GetFiles(TheCommonUtils.cdeFixupFileName("cache\\", true), $"{_storageMirror.MyMirrorCache.MyStoreID.Replace('&', 'n')}*");
                    return files.Length == 0;
                }
                catch(Exception) { }
            }
            return _storageMirror.Count == 0;
        }

        public Action<StoreEventArgs> RegisterEvent(string pEventName, Action<StoreEventArgs> pCallback)
        {
            return _storageMirror.RegisterEvent(pEventName, pCallback);
        }
        public bool HasRegisteredEvents(string pEventName)
        {
            return _storageMirror.HasRegisteredEvents(pEventName);
        }

        public void UnregisterEvent(string pEventName, Action<StoreEventArgs> pCallback)
        {
            _storageMirror.UnregisterEvent(pEventName, pCallback);
        }


        public void RemoveAllItems()
        {
            _storageMirror.RemoveAllItems();
        }

        public void RemoveStore(bool backupFirst)
        {
            _storageMirror.RemoveStore(backupFirst);
        }

        public void DeleteItemsUpTo(long lowestSequenceNumberTruncated)
        {
            _storageMirror.MyMirrorCache.RemoveUpToSequenceNumberInternal(lowestSequenceNumberTruncated);
        }

        public void UpdateItem(TheThingStore baseLine)
        {
            _storageMirror.UpdateItem(baseLine, null);
        }
        public void PinBaseLine(TheThingStore baseLine)
        {
            UpdateItem(baseLine);
        }
        public void SetBaseLine(TheThingStore baseLine)
        {
            UpdateItem(baseLine);
        }

        public long GetLastSequenceNumber()
        {
            return _storageMirror.MyMirrorCache.GetLastSequenceNumber();
        }
    }

    //internal class MultiFileThingStream : IThingStream
    //{
    //    private List<TheThingStore> _items;
    //    private long sequenceNumberOffset;

    //    class ThingFile
    //    {
    //        public long firstOffset;
    //        public string fileName;
    //    }

    //    private Guid thingMid;
    //    private long maxCount;
    //    private TimeSpan maxAge;
    //    private bool persistent;

    //    public MultiFileThingStream(bool persistent, Guid thingMid, long maxCount, TimeSpan maxAge)
    //    {
    //        this.thingMid = thingMid;
    //        this.maxCount = maxCount;
    //        this.maxAge = maxAge;
    //        this.persistent = persistent;

    //        _items = new List<TheThingStore>();
    //        if (persistent)
    //        {
    //            // Find all files and read them into the list
    //        }
    //    }

    //    public long CountTestHook => return _items.Count;

    //    public IReaderWriterLock MyRecordsRWLock => _storageMirror.MyRecordsRWLock;

    //    public long MaxCount
    //    {
    //        get => maxCount;
    //        set { maxCount = value; _storageMirror.SetMaxStoreSize((int)maxCount); }
    //    }
    //    public TimeSpan MaxAge
    //    {
    //        get => maxAge;
    //        set
    //        {
    //            maxAge = value;
    //            _storageMirror.SetRecordExpiration(value.TotalSeconds > int.MaxValue ? int.MaxValue : (int)value.TotalSeconds, null);
    //        }
    //    }
    //    public bool IsPersistent
    //    {
    //        get => _storageMirror.IsCachePersistent;
    //        set
    //        {
    //            _storageMirror.IsCachePersistent = value;
    //            _storageMirror.MyMirrorCache.IsCachePersistent = value;
    //        }
    //    }

    //    public void AddAnItem(TheThingStore thingSnapshot)
    //    {
    //        _storageMirror.AddAnItem(thingSnapshot);
    //    }

    //    public TheThingStore FindLastItemAtOrBeforeSequenceNumber(long lowestSequenceNumberTruncated, Func<TheThingStore, bool> p, out long baselineSequenceNumber)
    //    {
    //        return _storageMirror.MyMirrorCache.FindLastItemAtOrBeforeSequenceNumber(lowestSequenceNumberTruncated, p, out baselineSequenceNumber);
    //    }

    //    public void ForceSave()
    //    {
    //        _storageMirror.ForceSave();
    //    }

    //    public IEnumerable<TheThingStore> GetItems(long lowestSequenceNumberTruncated)
    //    {
    //        return _storageMirror.MyMirrorCache.GetItemsByInsertionOrderInternal(lowestSequenceNumberTruncated);
    //    }

    //    public long GetNextSequenceNumber(long baselineSequenceNumber)
    //    {
    //        return _storageMirror.MyMirrorCache.GetNextSequenceNumber(baselineSequenceNumber);
    //    }

    //    public long GetOffsetByTimestamp(DateTimeOffset start)
    //    {
    //        return _storageMirror.MyMirrorCache.GetOffsetByTimestamp(start);
    //    }

    //    public long GetPreviousSequenceNumber(long baselineSequenceNumber)
    //    {
    //        return _storageMirror.MyMirrorCache.GetPreviousSequenceNumber(baselineSequenceNumber);
    //    }

    //    public bool IsEmpty()
    //    {
    //        return _storageMirror.Count == 0;
    //    }

    //    public Action<StoreEventArgs> RegisterEvent(string pEventName, Action<StoreEventArgs> pCallback)
    //    {
    //        return _storageMirror.RegisterEvent(pEventName, pCallback);
    //    }
    //    public bool HasRegisteredEvents(string pEventName)
    //    {
    //        return _storageMirror.HasRegisteredEvents(pEventName);
    //    }

    //    public void UnregisterEvent(string pEventName, Action<StoreEventArgs> pCallback)
    //    {
    //        _storageMirror.UnregisterEvent(pEventName, pCallback);
    //    }


    //    public void RemoveAllItems()
    //    {
    //        _storageMirror.RemoveAllItems();
    //    }

    //    public void RemoveStore(bool backupFirst)
    //    {
    //        _storageMirror.RemoveStore(backupFirst);
    //    }

    //    public void DeleteItemsUpTo(long lowestSequenceNumberTruncated)
    //    {
    //        _storageMirror.MyMirrorCache.RemoveUpToSequenceNumberInternal(lowestSequenceNumberTruncated);
    //    }

    //    public void UpdateItem(TheThingStore baseLine)
    //    {
    //        _storageMirror.UpdateItem(baseLine, null);
    //    }
    //}

}
