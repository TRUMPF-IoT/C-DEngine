// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.Serialization;

using nsCDEngine.ViewModels;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.StorageService;

#pragma warning disable CS1591    //TODO: Remove and document public methods

namespace nsCDEngine.Engines.ThingService
{
    public class ThePropertyFilter : TheDataBase
    {
        /// <summary>
        /// List of properties for which changes are to be recorded. If null, changes for all properties of the thing will be recorded. If empty, no changes will be recorded.
        /// </summary>
        public List<string> Properties { get; set; }
        /// <summary>
        /// If all properties are included (Properties == null), this flag indicates that only properties marked as IsSensor should be used.
        /// </summary>
        public bool? FilterToSensorProperties { get; set; }
        /// <summary>
        /// If all properties are included (Properties == null), this flag indicates that only properties marked as IsConfig should be used.
        /// </summary>
        public bool? FilterToConfigProperties { get; set; }
        /// <summary>
        /// List of properties for which changes are not to be recorded. Typically used in conjunction with Properties == null.
        /// </summary>
        public List<string> PropertiesToExclude { get; set; } // null indicates don't exclude any properties
        public ThePropertyFilter() { }
        public ThePropertyFilter(ThePropertyFilter r)
        {
            Properties = r?.Properties != null ? new List<string>(r.Properties) : null;
            PropertiesToExclude = r?.PropertiesToExclude != null ? new List<string>(r.PropertiesToExclude) : null;
            FilterToConfigProperties = r?.FilterToConfigProperties;
            FilterToSensorProperties = r?.FilterToSensorProperties;
        }

    }

    public class TheHistoryParameters : ThePropertyFilter
    {
        /// <summary>
        /// Timespan over which property changes are to be sampled or aggregated. At most one history entry per sampling window is generated.
        /// </summary>
        public TimeSpan SamplingWindow { get; set; }
        /// <summary>
        /// Timespan to wait before declaring a sampling windows as closed. This let's callers trade off latency for correct sampling in case of delays from external sources. The cooldown period is started when the first value for a sampling window is received, even if the the timestamp of that value is far in the past or in the future; this allows for clock drift between systems or processing of historical data.
        /// </summary>
        public TimeSpan CooldownPeriod { get; set; } = new TimeSpan(0, 0, 5);
        /// <summary>
        /// Maximum number if history items to keep. 0 means no limit on item count.
        /// </summary>
        public int MaxCount { get; set; }
        /// <summary>
        /// Timespan for which to keep items in the history store. Items with timestamps older the the current time minus the MaxAge will be deleted (lazily). TimeSpan.Zero mean no expiration.
        /// </summary>
        public TimeSpan MaxAge { get; set; }
        /// <summary>
        /// Keep history items across restarts of the C-DEngine host. All changes to the thing during shutdown and startup will be recorded.
        /// </summary>
        public bool Persistent { get; set; }
        /// <summary>
        /// If true, each history entry will contain all property values (as per the Properties/PropertiesToExclude parameters) as current at the end of the sample window, regardless if they were updated or not. If false, only changed properties will be included in history entries.
        /// </summary>
        public bool ReportUnchangedProperties { get; set; }
        /// <summary>
        /// If true, the current values for the properties at the time of registration will be returned before any property updates
        /// </summary>
        public bool? ReportInitialValues { get; set; }
        /// <summary>
        /// By default any available history will be returned. IgnoreExistingHistory = true returns only updates that arrive after the registration
        /// </summary>
        public bool? IgnoreExistingHistory { get; set; }
        /// <summary>
        /// If false, history entries will be recorded internally and can only be retrieved via calls to GetThingHistory: this option is used primarily in conjunction with ClearHistory/RestartUpdateHistory for processing or transmitting streams of property changes with at least once delivery guarantees.
        /// If true, history entries will be recorded into a storage mirror that can be consumed directly by plug-ins. Calls to GetThingHistory will return null.
        /// </summary>
        public bool MaintainHistoryStore { get; set; }
        /// <summary>
        /// If MaintainHistoryStore is true and ExternalHistoryStore is false, these parameters are used to create the storage mirror to receive history entries.
        /// </summary>
        public TheStorageMirrorParameters HistoryStoreParameters { get; set; }
        /// <summary>
        /// If false, a storage mirror containing TheThingStore instances will be created with default storage mirror configurations, or HistoryStoreParameters if specified. The store can be retrieved using the GetHistoryStore method.
        /// If true, a custom storage mirror needs to be created by the caller and passed to RegisterForUpdateHistory/RestartUpdateHistory. The custom storage mirror can contain instances of any class (derived from TheMetaBase) and property values will be set using reflection (class property names must match the thing property names, missing properties are ignored).
        /// </summary>
        public bool ExternalHistoryStore { get; set; }

        /// <summary>
        /// For each property, add a property [name].Avg that contains the average of the values in the SamplingWindow.
        /// The computed property must be requested in the list of properties.
        /// </summary>
        public bool ComputeAvg { get; set; }
        /// <summary>
        /// For each property, add a property [name].N that contains the count of the values in the SamplingWindow.
        /// The computed property must be requested in the list of properties.
        /// </summary>
        public bool ComputeN { get; set; }
        /// <summary>
        /// For each property, add a property [name].Min that contains the minimum of the values in the SamplingWindow.
        /// The computed property must be requested in the list of properties.
        /// </summary>
        public bool ComputeMin { get; set; }
        /// <summary>
        /// For each property, add a property [name].Max that contains the maximum of the values in the SamplingWindow.
        /// The computed property must be requested in the list of properties.
        /// </summary>
        public bool ComputeMax { get; set; }
        /// <summary>
        /// If the token is not accessed for this time, it will be deleted and become invalid
        /// </summary>
        public TimeSpan? TokenExpiration { get; set; }

        /// <summary>
        /// Friendly Name of the consumer, for diagnostics purposes. This name will also be used as part of the file name of any buffers/stores
        /// </summary>
        public string OwnerName {get;set;}

        /// <summary>
        /// The cdeMID of a TheThing that owns this history registration: used for diagnostics/management purposes and cleaning up registration when things are deleted
        /// </summary>
        public Guid? OwnerThingMID { get; set; }

        /// <summary>
        /// The Thing that owns this history registration: used for diagnostics/management purposes and cleaning up registration when things are deleted
        /// </summary>
        [IgnoreDataMember]
        public TheThing OwnerThing
        {
            get
            {
                return TheThingRegistry.GetThingByMID(OwnerThingMID ?? Guid.Empty);
            }
            set
            {
                OwnerThingMID = value?.cdeMID;
            }
        }

        internal bool ComputeAggregates { get { return ComputeAvg || ComputeMax || ComputeMin || ComputeN;  } }

        public TheHistoryParameters() { }
        public TheHistoryParameters(TheHistoryParameters r) : base(r)
        {
            ReportUnchangedProperties = r.ReportUnchangedProperties;
            ReportInitialValues = r.ReportInitialValues;
            CooldownPeriod = r.CooldownPeriod;
            SamplingWindow = r.SamplingWindow;
            MaxCount = r.MaxCount;
            MaxAge = r.MaxAge;
            Persistent = r.Persistent;
            MaintainHistoryStore = r.MaintainHistoryStore;
            ExternalHistoryStore = r.ExternalHistoryStore;

            ComputeAvg = r.ComputeAvg;
            ComputeN = r.ComputeN;
            ComputeMin = r.ComputeMin;
            ComputeMax = r.ComputeMax;
            TokenExpiration = r.TokenExpiration;
            OwnerThingMID = r.OwnerThingMID;
            OwnerName = r.OwnerName;
            HistoryStoreParameters = r.HistoryStoreParameters;
        }

        public TheHistoryParameters(ThePropertyFilter r) : base(r)
        {
        }

        internal void MergeRegistration(TheHistoryParameters r)
        {
            if (r.ReportUnchangedProperties)
            {
                ReportUnchangedProperties = true;
            }
            if (r.ReportInitialValues == true)
            {
                ReportInitialValues = true;
            }
            if (r.SamplingWindow != SamplingWindow)
            {
                SamplingWindow = GCD(SamplingWindow, r.SamplingWindow);
            }
            if (r.CooldownPeriod < CooldownPeriod)
            {
                CooldownPeriod = r.CooldownPeriod;
            }

            if ((r.MaxCount > MaxCount && MaxCount != 0) || r.MaxCount == 0)
            {
                MaxCount = r.MaxCount;
            }
            if ((r.MaxAge > MaxAge && MaxAge != TimeSpan.Zero) || r.MaxAge == TimeSpan.Zero)
            {
                MaxAge = r.MaxAge;
            }
            if (r.Persistent)
            {
                Persistent = true;
            }
            if (r.Properties == null)
            {
                Properties = null;
            }
            else if (Properties != null)
            {
                Properties = new List<string>(Properties.Union(r.Properties));
                if (PropertiesToExclude != null)
                {
                    foreach (var propName in r.Properties)
                    {
                        PropertiesToExclude.Remove(propName);
                    }
                }
            }
            if (PropertiesToExclude != null)
            {
                if (r.PropertiesToExclude == null)
                {
                    PropertiesToExclude = null;
                }
                else
                {
                    // Remove any properties from the exclusion list that are not also excluded by the new registration (intersection)
                    PropertiesToExclude = new List<string>(PropertiesToExclude.Intersect(r.PropertiesToExclude));
                }
            }
            if (r.FilterToSensorProperties != true)
            {
                FilterToSensorProperties = r.FilterToSensorProperties;
            }
            if (r.FilterToConfigProperties != true)
            {
                FilterToConfigProperties = r.FilterToConfigProperties;
            }

            if (r.ComputeN) ComputeN = true;
            if (r.ComputeAvg) ComputeAvg = true;
            if (r.ComputeMin) ComputeMin = true;
            if (r.ComputeMax) ComputeMax = true;
            if (r.TokenExpiration > TokenExpiration) TokenExpiration = r.TokenExpiration;
        }

        private TimeSpan GCD(TimeSpan aggregationWindow1, TimeSpan aggregationWindow2)
        {
            var n1 = aggregationWindow1.Ticks;
            var n2 = aggregationWindow2.Ticks;
            if (n2 == 0)
            {
                return aggregationWindow2;
            }
            if (n1 == 0)
            {
                return aggregationWindow1;
            }

            var gcd = GCD(n1, n2);
            if (gcd == n1)
            {
                return aggregationWindow1;
            }
            else if (gcd == n2)
            {
                return aggregationWindow2;
            }
            return new TimeSpan(gcd);
        }

        private long GCD(long n1, long n2)
        {
            if (n1 < n2)
            {
                (n1, n2) = (n2, n1);
            }
            long mod;
            while ((mod = n1 % n2) > 0)
            {
                n1 = n2;
                n2 = mod;
            }
            return n2;
        }

        internal bool IsEqual(TheHistoryParameters param)
        {
            return
                   param.ComputeAggregates == this.ComputeAggregates
                && param.ComputeAvg == this.ComputeAvg
                && param.ComputeMax == this.ComputeMax
                && param.ComputeN == this.ComputeN
                && param.CooldownPeriod == this.CooldownPeriod
                && param.MaintainHistoryStore == this.MaintainHistoryStore
                && param.ExternalHistoryStore == this.ExternalHistoryStore
                && param.MaxAge == this.MaxAge
                && param.MaxCount == this.MaxCount
                && param.Persistent == this.Persistent
                && IsListEquivalent(param.Properties, this.Properties)
                && IsListEquivalent(param.PropertiesToExclude, this.PropertiesToExclude)
                && (param.FilterToSensorProperties ?? false) == (this.FilterToSensorProperties ?? false)
                && (param.FilterToConfigProperties??false) == (this.FilterToConfigProperties??false)
                && param.ReportUnchangedProperties == this.ReportUnchangedProperties
                && param.ReportInitialValues == this.ReportInitialValues
                && param.SamplingWindow == this.SamplingWindow
                && param.TokenExpiration == this.TokenExpiration
                && param.OwnerThingMID == this.OwnerThingMID
                && param.OwnerName == this.OwnerName
                ;
        }
        private bool IsListEquivalent(List<string> list1, List<string> list2)
        {
            if (list1 != null && list1.Count == 0)
            {
                list1 = null;
            }
            if (list2 != null && list2.Count == 0)
            {
                list2 = null;
            }
            if (list1 == list2)
                return true;
            if (list1 == null || list2 == null)
                return false;
            return list1.OrderBy(s => s).SequenceEqual(list2.OrderBy(s => s));
        }
    }

    public class TheHistoryResponse
    {
        public List<TheThingStore> HistoryItems;
        public int PendingItemCount;
        public bool DatalossDetected;
    }

    internal interface IHistorian
    {
        bool Disabled { get; set; }

        void Close();
        void AddPropertySnapshot(TheThing tThing, string propertyNamePath, object propValue, DateTimeOffset propTime, long? propSEQ);
        Guid RegisterConsumer<T>(TheThing theThing, TheHistoryParameters consumerRegistrationParameters, TheStorageMirror<T> store) where T : TheDataBase, INotifyPropertyChanged, new();
        TheHistoryParameters GetHistoryParameters(Guid historyToken);
        void UnregisterConsumer(Guid historyToken);
        void UnregisterConsumer(Guid historyToken, bool keepHistoryStore);
        void ClearHistory(Guid historyToken);
        bool RestartHistory<T>(Guid token, TheHistoryParameters historyParameters, TheStorageMirror<T> store) where T : TheDataBase, INotifyPropertyChanged, new();
        Task<TheHistoryResponse> GetHistoryAsync(Guid token, int maxCount, int minCount, TimeSpan timeout, CancellationToken? cancelToken, bool clearHistory);
        TheStorageMirror<T> GetHistoryStore<T>(Guid historyToken) where T : TheDataBase, INotifyPropertyChanged, new();
        Task<bool> DeleteAsync();
        Task<bool> UnregisterAllConsumersForOwnerAsync(TheThing owner);
        IEnumerable<ConsumerRegistration> GetConsumerRegistrations();
    }

    internal class ConsumerRegistration : TheHistoryParameters
    {
        public Guid Token;
        public Guid ThingMid;
        /// <summary>
        /// Sequence Number of the last item returned. 0 = return first item.
        /// </summary>
        public long LastSequenceNumberRead;
        public long SequenceNumberTruncated;
        public DateTimeOffset LastAccess;

        // Tracks if a consumer has already received initial values (used to prevent from other consumers triggering more initial values to be reported)
        public bool? InitialValuesReported { get; set; }
        public long _highestInitialValueSequenceNumber;
        public bool DataLossDetected { get; internal set; }

        [NonSerialized]
        internal SnapshotManager PendingSnapshots;

        public ConsumerRegistration()
        {
            LastAccess = DateTimeOffset.Now;
        }
        public ConsumerRegistration(TheHistoryParameters param) : base(param)
        {
            LastAccess = DateTimeOffset.Now;
        }
        public ConsumerRegistration(TheThing tThing, Guid token, TheHistoryParameters param) : base(param)
        {
            Token = token;
            cdeMID = token;
            ThingMid = tThing.cdeMID;
            LastAccess = DateTimeOffset.Now;
        }
        public ConsumerRegistration(ConsumerRegistration param) : base(param)
        {
            Token = param.Token;
            cdeMID = param.Token;
            ThingMid = param.ThingMid;
            LastAccess = param.LastAccess;
            InitialValuesReported = param.InitialValuesReported;
            DataLossDetected = param.DataLossDetected;

            LastSequenceNumberRead = param.LastSequenceNumberRead;
            SequenceNumberTruncated = param.SequenceNumberTruncated;
            DataLossDetected = param.DataLossDetected;
        }
        public bool IsCompatible(TheHistoryParameters param)
        {
            return this.IsEqual(param);
        }

        internal static ConsumerRegistration Create<T>(ConsumerRegistration registration, TheStorageMirror<T> store) where T : TheDataBase, INotifyPropertyChanged, new()
        {
            if (registration.MaintainHistoryStore && !registration.ExternalHistoryStore)
            {
                registration = new TheStorageMirrorHistorian.ConsumerRegistrationWithStore<T>(registration, store);
            }
            else
            {
                registration = new ConsumerRegistration(registration);
            }
            return registration;
        }

        internal static ConsumerRegistration Create<T>(TheThing tThing, Guid token, TheHistoryParameters registrationParameters, TheStorageMirror<T> store) where T : TheDataBase, INotifyPropertyChanged, new()
        {
            ConsumerRegistration registration;
            if (registrationParameters.MaintainHistoryStore)
            {
                registration = new TheStorageMirrorHistorian.ConsumerRegistrationWithStore<T>(tThing, token, registrationParameters, store);
            }
            else
            {
                registration = new ConsumerRegistration(tThing, token, registrationParameters);
            }
            return registration;
        }

        internal virtual void Init()
        {
        }

        internal virtual void HandleUpdates(TheStorageMirrorHistorian theStorageMirrorHistorian)
        {
        }

        internal virtual void Delete()
        {
        }
        public override string ToString()
        {
            return $"{Token} {ThingMid} {OwnerName} {OwnerThing?.FriendlyName ?? TheCommonUtils.cdeGuidToString(OwnerThingMID ?? Guid.Empty) ?? string.Empty}";
        }

        TheThing _thing;
        internal TheThing GetThing()
        {
            _thing ??= TheThingRegistry.GetThingByMID(ThingMid, true);
            return _thing;
        }
    }

}
