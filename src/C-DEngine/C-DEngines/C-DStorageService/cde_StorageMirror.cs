// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;

using System.ComponentModel;
using System.Linq;

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.Communication;
using nsCDEngine.Engines.StorageService.Model;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using nsCDEngine.ISM;
using System.IO;
using System.Threading.Tasks;
using System.Text;
// ReSharper disable UseNullPropagation
// ReSharper disable DelegateSubtraction
// ReSharper disable MergeSequentialChecks
// ReSharper disable ConvertIfStatementToNullCoalescingExpression
#pragma warning disable 1591


//ERROR Range 470-479

namespace nsCDEngine.Engines.StorageService
{

    /// <summary>
    /// Event Names of Events fired by TheStorageService
    /// </summary>
    public static class eStoreEvents
    {
        /// <summary>
        /// Fired when the store is ready to be used
        /// </summary>
        public const string StoreReady = "StoreReady";
        /// <summary>
        /// Fired when a client requested an update to the Store
        /// </summary>
        public const string ReloadRequested = "ReloadRequested";

        /// <summary>
        /// A delete of a record was requested. The parameter contains the ID of the record to be deleted
        /// </summary>
        public const string DeleteRequested = "DeleteRequested";

        /// <summary>
        /// Fires if the StoreMirror has updates - The paramter contains the updated record(s)
        /// </summary>
        public const string HasUpdates = "HasUpdates";
        /// <summary>
        /// An insert into a store was requested. The parameter contains the new record to be inserted
        /// </summary>
        public const string InsertRequested = "InsertRequested";
        /// <summary>
        /// An update to a store was requested. The parameter contains the updating record
        /// </summary>
        public const string UpdateRequested = "UpdateRequested";
        /// <summary>
        /// Fires when a new record was inserted successfully
        /// </summary>
        public const string Inserted = "Inserted";
    }
    /// <summary>
    /// TheStorageMirror is a class representing data storage of the C-DEngine. It abstracts the underlying data Store from the use in The C-DEngine
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TheStorageMirror<T> : IDisposable where T : TheDataBase, INotifyPropertyChanged, new()
    {
#pragma warning disable 67
        //[Obsolete("This is no longer fired. Please remove your handler. We will remove this in a later Version")]
        //public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 67
        /// <summary>
        /// Identifies a response from a store after a query or other operation (read, write, update, delete, etc.) has been executed.
        /// </summary>
        public class StoreResponse
        {
            public List<T> MyRecords;
            public bool HasErrors;
            public int PageNumber;      
            public string ErrorMsg = "";
            public string SQLFilter = "";
            public string SQLOrder = "";
            public string ColumnFilter = "";
            public object Cookie;
        }

#region Timed Store-Request Management
        private sealed class TimedRequest
        {
            public Action<StoreResponse> MyRequest;
            public Timer MyTimer;
            public Dictionary<string, T> MyRecords;
            public Guid MagicID;
            public object Cookie;
        }
        private readonly cdeConcurrentDictionary<string, TimedRequest> MyStoreRequests = new ();
        private readonly object MyStoreRequestsLock = new ();
        private string AddTimedRequest(Action<StoreResponse> pCallBack, object pCookie, Dictionary<string, T> pDetails)
        {
            TimedRequest tReq = new()
            {
                MyRequest = pCallBack,
                MagicID = Guid.NewGuid(),
                Cookie = pCookie
            };
            string Magix = tReq.MagicID.ToString();
            SetFailureTimer(tReq);
            tReq.MyRecords = pDetails;
            lock (MyStoreRequestsLock)
            {
                if (MyStoreRequests.ContainsKey(tReq.MagicID.ToString()))
                    MyStoreRequests[tReq.MagicID.ToString()] = tReq;
                else
                    MyStoreRequests.TryAdd(tReq.MagicID.ToString(), tReq);
            }
            TheBaseAssets.MyServiceHostInfo.TO.MakeHeartPump();
            return Magix;
        }

        private TimedRequest GetTimedRequest(string LastID)
        {
            TimedRequest tReq = null;
            lock (MyStoreRequestsLock)
            {
                MyStoreRequests.TryGetValue(LastID, out tReq);
                if (tReq != null)
                {
                    if (tReq.MyTimer != null)
                        tReq.MyTimer = null;
                    MyStoreRequests.RemoveNoCare(LastID);
                }
            }
            TheBaseAssets.MyServiceHostInfo.TO.MakeHeartNormal();
            return tReq;
        }

        private void SetFailureTimer(TimedRequest tReq)
        {
            Timer tTimer = null;
            if (TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout > 0)
                tTimer = new Timer(sinkRequestFailed, tReq, TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout * 1000, Timeout.Infinite);
            tReq.MyTimer = tTimer;
        }

        private void sinkRequestFailed(object state)
        {
            // CODE REVIEW: If this method throws an exception, it will be unhandled and the process will terminate and verify it's indeed safe
            TheDiagnostics.SetThreadName("Storage-RequestFailed", true);
            if (state is TimedRequest req)
            {
                TimedRequest tReq = req;
                lock (MyStoreRequestsLock)
                {
                    if (!MyStoreRequests.ContainsKey(tReq.MagicID.ToString())) return;
                }

                TheBaseAssets.MySYSLOG.WriteToLog(472, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("StorageMirror", "Storage-Request Timed Out", eMsgLevel.l2_Warning));
                StoreResponse tResponse = new ()
                {
                    ErrorMsg = "Call Timed out",
                    HasErrors = true
                };
                if (tReq.MyRecords != null)
                    tResponse.MyRecords = tReq.MyRecords.Values.ToList();
                lock (MyStoreRequestsLock)
                    MyStoreRequests.RemoveNoCare(tReq.MagicID.ToString());
                tReq.MyRequest(tResponse);

            }
        }

#endregion

#region Event Handling

        private readonly TheCommonUtils.RegisteredEventHelper<StoreEventArgs> MyRegisteredEvents = new ();
        public Action<StoreEventArgs> RegisterEvent(string pEventName, Action<StoreEventArgs> pCallback)
        {
            return MyRegisteredEvents.RegisterEvent(pEventName, pCallback);
        }
        public void UnregisterEvent(string pEventName, Action<StoreEventArgs> pCallback)
        {
            MyRegisteredEvents.UnregisterEvent(pEventName, pCallback);
        }
        public void FireEvent(string pEventName, StoreEventArgs pPara, bool FireAsync)
        {
            MyRegisteredEvents.FireEvent(pEventName, pPara, FireAsync);
        }

        public bool HasRegisteredEvents(string pEventName)
        {
            return MyRegisteredEvents.HasRegisteredEvents(pEventName);
        }
#endregion
        public bool IsReady { get; internal set; } // CODE REVIEW: Should this be internal set? CM: YES!
        public bool AllowFireUpdates { get; set; }
        public bool IsCached { get; set; }
        public bool IsCachePersistent
        {
            get { return MyMirrorCache.IsCachePersistent; }
            set { MyMirrorCache.IsCachePersistent = value; }
        }
        public bool UseSafeSave
        {
            get { return MyMirrorCache.UseSafeSave;  }
            set { MyMirrorCache.UseSafeSave = value; }
        }
        public bool IsCacheEncrypted { get; set; }

        private int mCacheStoreInterval = 15;

        /// <summary>
        /// Sets a timespan for the Cache to be flushed to disk
        /// </summary>
        public int CacheStoreInterval
        {
            get {return mCacheStoreInterval; }
            set
            {
                mCacheStoreInterval = value;
                if (MyMirrorCache != null)
                    MyMirrorCache.CacheStoreInterval = mCacheStoreInterval;
            }
        }

        private bool mBlockWriteIfIsolated = false;
        public bool BlockWriteIfIsolated
        {
            get {
                return mBlockWriteIfIsolated;
            }
            set
            {
                mBlockWriteIfIsolated = value;
                if (MyMirrorCache != null)
                {
                    MyMirrorCache.BlockWriteIfIsolated = mBlockWriteIfIsolated;
                }
            }
        }

        private bool mIsStoreIntervalInSeconds = false;
        public bool IsStoreIntervalInSeconds
        {
            get { return mIsStoreIntervalInSeconds; }
            set
            {
                mIsStoreIntervalInSeconds = value;
                if (MyMirrorCache != null)
                {
                    MyMirrorCache.IsStoreIntervalInSeconds = mIsStoreIntervalInSeconds;
                }
            }
        }

        private bool WasInitCalled = false;
        private bool WasCreateCalled = false;
        private bool DontRegisterInStorageRegistry;
        internal bool CanBeFlushed { get; set; }
        internal string FriendlyName { get; set; }
        public string Description { get; internal set; }
        public TheMirrorCache<T> MyMirrorCache = new ();
        private IStorageService engineStorageServer;

#region Init and Active Properties
        public TheStorageMirror(IStorageService pStorageServer)
        {
            engineStorageServer = pStorageServer;
            CacheStoreInterval = 10;
            MyMirrorCache.CacheStoreInterval = CacheStoreInterval;
        }
        ~TheStorageMirror()
        {
            Dispose(false);
        }
        public void SetStorageService(IStorageService pStorageServer)
        {
            engineStorageServer = pStorageServer;
        }

        public string CacheTableName
        {
            get { return MyMirrorCache.MyStoreID; }
            set {
                MyMirrorCache.MyStoreID = value;
                if (!DontRegisterInStorageRegistry)
                    TheCDEngines.RegisterMirrorRepository(value, this);
            }
        }
        private string m_StoreID = "";
        public string MyStoreID
        {
            get { return m_StoreID; }
            private set
            {
                m_StoreID = value;
                NotifyOfUpdate(null);
            }
        }
        private readonly Guid m_StoreMID = Guid.NewGuid();
        public Guid StoreMID
        {
            get { return m_StoreMID; }
        }

        private bool mIsUpdateSubscriptionEnabled ;
        public bool IsUpdateSubscriptionEnabled
        {
            get { return mIsUpdateSubscriptionEnabled; }
            set
            {
                if (engineStorageServer == null || IsRAMStore || TheCDEngines.MyIStorageService == null) return;
                if (!mIsUpdateSubscriptionEnabled && value)
                {
                    TSM msgSub = new(TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineName(), "CDE_SUBSCRIBE", TheBaseAssets.MyScopeManager.AddScopeID(m_StoreID));
                    msgSub.SetNoDuplicates(true);
                    msgSub.SetToServiceOnly(true);
                    TheCommCore.PublishCentral(msgSub);
                }
                if (mIsUpdateSubscriptionEnabled && !value)
                {
                    TSM msgSub = new (TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineName(), "CDE_UNSUBSCRIBE", TheBaseAssets.MyScopeManager.AddScopeID(m_StoreID));
                    msgSub.SetNoDuplicates(true);
                    msgSub.SetToServiceOnly(true);
                    TheCommCore.PublishCentral(msgSub);
                }
                mIsUpdateSubscriptionEnabled = value;
            }
        }
        private Timer mMyHealthTimer = null;

        public bool IsRAMStore { get; set; } = true;

        public Task<bool> WaitForStoreReadyAsync()
        {
            if (IsReady)
            {
                return TheCommonUtils.TaskFromResult(true);
            }
            TaskCompletionSource<bool> tcs = new ();
            void callback(StoreEventArgs args)
            {
                tcs.TrySetResult(IsReady);
                UnregisterEvent(eStoreEvents.StoreReady, callback);
            }

            RegisterEvent(eStoreEvents.StoreReady, callback);
            if (IsReady)
            {
                callback(null);
            }
            return tcs.Task;
        }

        public bool AppendOnly
        {
            get { return MyMirrorCache.AppendOnly; }
            set
            {
                MyMirrorCache.AppendOnly = value;
                if(value)
                {
                    IsRAMStore = false;
                    IsCached = true;
                    IsCachePersistent = false;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            DisposeExpireTimer();
            Reset();
        }
#endregion

#region Expiration Management

        /// <summary>
        /// Sets a time in seconds after which Record entries will be removed from the Database
        /// If a callback is specified, the callback will be called for each record that will be deleted.
        /// </summary>
        /// <param name="tSeconds"></param>
        /// <param name="pEventRecordExpired"></param>
        public void SetRecordExpiration(int tSeconds,Action<T> pEventRecordExpired)
        {
            MyRecordExpiration = tSeconds;
            StartOrStopExpireTimer(tSeconds);
            if (MyMirrorCache != null)
            {
                //CODE REVIEW: MyMirrorCache.MyExpirationTest == 0 - This did not update if the expiration test had been set before: was there a reason? Need this ability for historian with multiple consumers
                MyMirrorCache.SetExpireTime(tSeconds);
                if (pEventRecordExpired != null)
                {
                    MyMirrorCache.eventRecordExpired += pEventRecordExpired;
                }
            }
        }
        void StartOrStopExpireTimer(int expireSeconds)
        {
            if (expireSeconds > 0)
            {
                int expireTimer;
                if (expireSeconds < 24 * 60 * 60)
                {
                    expireTimer = Math.Max(expireSeconds * 1000, TheBaseAssets.MyServiceHostInfo.TO.StorageCleanCycle * TheBaseAssets.MyServiceHostInfo.TO.QSenderHealthTime);
                }
                else
                {
                    expireTimer = 24 * 60 * 60 * 1000;
                }


                if (mMyHealthTimer == null)
                {
                    var newHealthTimer = new Timer(sinkExpireTimer, null, 0, expireTimer);
                    var previousTimer = Interlocked.CompareExchange(ref mMyHealthTimer, newHealthTimer, null);
                    if (previousTimer != null)
                    {
                        try
                        {
                            newHealthTimer?.Dispose();
                        }
                        catch 
                        { 
                            //intentionally blank
                        }
                    }
                }
                else
                {
                    try
                    {
                        mMyHealthTimer?.Change(0, expireTimer);
                    }
                    catch 
                    {
                        // Could get an ObjectDisposedException here, but just ignore all exceptions
                    }
                }
            }
            else
            {
                DisposeExpireTimer();
            }
        }

        void DisposeExpireTimer()
        {
            var healthTimer = Interlocked.Exchange(ref mMyHealthTimer, null);
            try
            {
                healthTimer?.Dispose();
            }
            catch
            {
                //intentionally blank
            }
        }

        private int MyRecordExpiration;
        private void sinkExpireTimer(object NOTUSED)
        {
            // Now handled inside SaveCacheToDisk
            if (MyRecordExpiration==0) return;
            if ((IsRAMStore || IsCached) && MyMirrorCache!=null)
                MyMirrorCache.RemoveRetired(MyRecordExpiration);
            if (!IsRAMStore && engineStorageServer!=null)
                engineStorageServer.EdgeStorageExecuteSql(typeof(T), "delete from {0} where cdeCTIM < '"+ DateTime.Now.Subtract(new TimeSpan(0,0,MyRecordExpiration)).ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture) +"'", "", "", null, CacheTableName); 
        }
        public void SetMaxStoreSize(int pRecords)
        {
            if (IsRAMStore || IsCached)
               MyMirrorCache.SetMaxStoreSize(pRecords);
        }

#endregion

#region Store Init, Notify and Reset
        public void InitializeStore(bool ResetContent)
        {
            InitializeStore(false, ResetContent, false, true);
        }
        public void InitializeStore(bool pCanBeFlushed, bool ResetContent)
        {
            InitializeStore(pCanBeFlushed, ResetContent, false, true);
        }

        /// <summary>
        /// Initializes a StorageMirror
        /// </summary>
        /// <param name="pCanBeFlushed">Mirror Allows to be flushed</param>
        /// <param name="ResetContent">if true, existing content will be removed</param>
        /// <param name="ImportFile">If set ot a valid path, the StorageMirror copies this file to the cache folder and tries to load it. If there is an existing mirror with the same name, the ImportFile will be IGNORED</param>
        public void InitializeStore(bool pCanBeFlushed, bool ResetContent, string ImportFile)
        {
            InitializeStore(pCanBeFlushed, ResetContent, false, true, ImportFile);
        }


        internal void InitializeStore(bool pCanBeFlushed, bool ResetContent, bool LoadSync, bool registerInMirrorRepository, string importFile = null)
        {
            InitializeStore(new TheStorageMirrorParameters { CanBeFlushed = pCanBeFlushed, ResetContent = ResetContent, LoadSync = LoadSync, DontRegisterInMirrorRepository = !registerInMirrorRepository, ImportFile = importFile });
        }

        public void InitializeStore(TheStorageMirrorParameters storeParams)
        {
            if (storeParams == null || IsReady || WasInitCalled) //Dont call again if store is already Ready
                return;
            WasInitCalled = true;
            if (!string.IsNullOrEmpty(storeParams.CacheTableName))
            {
                CacheTableName = storeParams.CacheTableName;
            }
            if (!string.IsNullOrEmpty(storeParams.FriendlyName))
            {
                FriendlyName = storeParams.FriendlyName;
            }
            if (storeParams.CanBeFlushed.HasValue)
            {
                CanBeFlushed = storeParams.CanBeFlushed.Value;
                MyMirrorCache.CanBeFlushed = storeParams.CanBeFlushed.Value;
            }
            if (storeParams.MaxAge.HasValue)
            {
                var totalSeconds = storeParams.MaxAge.Value.TotalSeconds;
                SetRecordExpiration(totalSeconds > int.MaxValue ? int.MaxValue : (int)totalSeconds, storeParams.ExpirationCallback as Action<T>);
                IsStoreIntervalInSeconds = true;
                if (storeParams.SaveCount.HasValue)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(473, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("StorageMirror", $"Ignoring Save Count {storeParams.SaveCount} for {FriendlyName ?? CacheTableName} because MaxAge has been specified.", eMsgLevel.l1_Error));
                }
                if (storeParams.ExpirationCallback!= null && storeParams.ExpirationCallback is not Action<T> )
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(473, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("StorageMirror", $"Invalid ExpirationCallback - must be of Type Action<T> for {FriendlyName ?? CacheTableName}. Callback will not be called.", eMsgLevel.l1_Error));
                }
            }
            if (storeParams.CacheStoreInterval.HasValue)
            {
                if (storeParams.SaveCount.HasValue)
                {
                    // Error!! Log?
                }
                CacheStoreInterval = (int) storeParams.CacheStoreInterval.Value.TotalSeconds;
                IsStoreIntervalInSeconds = true;
            }
            else if (storeParams.SaveCount.HasValue)
            {
                CacheStoreInterval = storeParams.SaveCount.Value;
                IsStoreIntervalInSeconds = false;
            }
            DontRegisterInStorageRegistry = storeParams.DontRegisterInMirrorRepository;
            if (storeParams.MaxCount.HasValue)
            {
                SetMaxStoreSize(storeParams.MaxCount.Value);
            }
            if (storeParams.TrackInsertionOrder.HasValue)
            {
                MyMirrorCache.TrackInsertionOrder();
            }
            if (storeParams.AllowFireUpdates.HasValue)
            {
                AllowFireUpdates = storeParams.AllowFireUpdates.Value;
            }
            if (storeParams.BlockWriteIfIsolated.HasValue)
            {
                BlockWriteIfIsolated = storeParams.BlockWriteIfIsolated.Value;
            }
            if (storeParams.IsCached.HasValue)
            {
                IsCached = storeParams.IsCached.Value;
            }
            if (storeParams.IsCacheEncrypted.HasValue)
            {
                IsCacheEncrypted = storeParams.IsCacheEncrypted.Value;
            }
            if (storeParams.IsCachePersistent.HasValue)
            {
                IsCachePersistent = storeParams.IsCachePersistent.Value;
            }
            if (storeParams.IsUpdateSubscriptionEnabled.HasValue)
            {
                IsUpdateSubscriptionEnabled = storeParams.IsUpdateSubscriptionEnabled.Value;
            }
            if (storeParams.UseSafeSave.HasValue)
            {
                UseSafeSave = storeParams.UseSafeSave.Value;
            }
            if (storeParams.FastSaveLock.HasValue)
            {
                MyMirrorCache.FastSaveLock = storeParams.FastSaveLock.Value;
            }

            if (MyStoreID.Length == 0)
            {
                MyStoreID = TheStorageUtilities.GenerateUniqueIDFromType(typeof(T), CacheTableName);
                TheBaseAssets.MyServiceTypes.Add(typeof(T));
                if (!DontRegisterInStorageRegistry)
                {
                    TheCDEngines.RegisterMirrorRepository(TheStorageUtilities.GenerateUniqueIDFromType(typeof(T), null), this);
                    TheCDEngines.RegisterMirrorRepository(MyStoreID, this);
                    if (!string.IsNullOrEmpty(CacheTableName))
                        TheCDEngines.RegisterMirrorRepository(CacheTableName, this);    //We also need to register just the cachetablename or we break plugins that were using the cachetablename to connect the NMI Table
                    TheCDEngines.RegisterMirrorRepository(StoreMID.ToString(), this);
                }
                if (AppendOnly)
                {
                    MyMirrorCache.IsCacheEncrypted = IsCacheEncrypted;
                    if (storeParams.MaxCacheFileCount.HasValue)
                        MyMirrorCache.MaxCacheFileCount = storeParams.MaxCacheFileCount.Value;
                    if (storeParams.MaxCacheFileSize.HasValue)
                        MyMirrorCache.MaxCacheFileSize = storeParams.MaxCacheFileSize.Value;
                }
                if ((IsCachePersistent || (AppendOnly && MyMirrorCache.TracksInsertionOrder)) && !TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID)
                {
                    MyMirrorCache.IsCachePersistent = IsCachePersistent;
                    MyMirrorCache.IsCacheEncrypted = IsCacheEncrypted;
                    MyMirrorCache.IsStoreIntervalInSeconds = IsStoreIntervalInSeconds;
                    MyMirrorCache.CacheStoreInterval = CacheStoreInterval;
                    if (storeParams.ResetContent)
                    {
                        MyMirrorCache.Clear(false);
                        FireStoreReady();
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(storeParams.ImportFile) && !File.Exists(TheCommonUtils.cdeFixupFileName(string.Format("cache\\{0}", MyStoreID.Replace('&', 'n')),true)))
                        {
                            try
                            {
                                File.Copy(TheCommonUtils.cdeFixupFileName(storeParams.ImportFile), TheCommonUtils.cdeFixupFileName(string.Format("cache\\{0}", MyStoreID.Replace('&', 'n')),true));
                            }
                            catch (Exception e)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(471, new TSM("StorageMirror", $"File Import for {storeParams.ImportFile} failed", eMsgLevel.l1_Error,TSM.L(eDEBUG_LEVELS.VERBOSE)?"": e.ToString()));
                            }
                        }
                        //if (HasRegisteredEvents(eStoreEvents.StoreReady))
                        MyMirrorCache.eventCachedReady += CreateStoreCallback; //4.1094: Must always fire to set "IsReady"
                        MyMirrorCache.LoadCacheFromDisk(storeParams.LoadSync);
                    }
                }
                else
                {
                    FireStoreReady();
                }
            }
        }

        public Task<bool> InitializeStoreAsync(TheStorageMirrorParameters storeParams)
        {
            return CreateStoreAsync(storeParams, null);
        }

        public void CreateStore(string ShortName, string Description, T pDefaults, bool pCanBeFlushed, bool ResetContent)
        {
            CreateStore(new TheStorageMirrorParameters { FriendlyName = ShortName, Description = Description, CanBeFlushed = pCanBeFlushed, ResetContent = ResetContent }, pDefaults);
        }

        public void CreateStore(TheStorageMirrorParameters storeParams, T pDefaults)
        {
            if (storeParams == null || IsReady || WasCreateCalled)
                return;
            WasCreateCalled = true;
            CanBeFlushed = storeParams.CanBeFlushed ?? false;
            DontRegisterInStorageRegistry = storeParams.DontRegisterInMirrorRepository;
            MyMirrorCache.CanBeFlushed = storeParams.CanBeFlushed ?? false;
            if (storeParams.IsRAMStore.HasValue)
            {
                IsRAMStore = storeParams.IsRAMStore.Value;
            }
            if (!string.IsNullOrEmpty(storeParams.CacheTableName))
            {
                CacheTableName = storeParams.CacheTableName;
            }
            if (storeParams.AllowFireUpdates.HasValue)
            {
                AllowFireUpdates = storeParams.AllowFireUpdates.Value;
            }
            if (storeParams.MaxCount.HasValue)
            {
                SetMaxStoreSize(storeParams.MaxCount.Value);
            }
            if (storeParams.AppendOnly.HasValue)
            {
                AppendOnly = storeParams.AppendOnly.Value;
            }
            FriendlyName = storeParams.FriendlyName;
            Description = storeParams.Description;
            if (MyStoreID.Length == 0)
            {
                if ((IsRAMStore || IsCached) && (IsRAMStore || AppendOnly))
                    InitializeStore(storeParams);
                if (!IsRAMStore && engineStorageServer != null)
                    engineStorageServer.EdgeDataCreateStore(typeof(T), pDefaults, storeParams.FriendlyName, storeParams.Description, storeParams.ResetContent, CreateStoreCallback, CacheTableName);
                TheBaseAssets.MyServiceTypes.Add(typeof(T));
                if (!DontRegisterInStorageRegistry)
                {
                    TheCDEngines.RegisterMirrorRepository(TheStorageUtilities.GenerateUniqueIDFromType(typeof(T), null), this);
                    TheCDEngines.RegisterMirrorRepository(StoreMID.ToString(), this);
                    if (!string.IsNullOrEmpty(CacheTableName))
                        TheCDEngines.RegisterMirrorRepository(CacheTableName, this);
                    TheCDEngines.RegisterMirrorRepository(TheStorageUtilities.GenerateUniqueIDFromType(typeof(T), CacheTableName), this);
                }
            }
        }

        public Task<bool> CreateStoreAsync(TheStorageMirrorParameters storeParams, T pDefaults)
        {
            var taskCS = new TaskCompletionSource<bool>();
            RegisterEvent(eStoreEvents.StoreReady, (pStoreID) => {
                taskCS.TrySetResult(true);
            });
            CreateStore(storeParams, pDefaults);
            TheCommonUtils.TaskWaitTimeout(taskCS.Task, new TimeSpan(0, 0, 15)).ContinueWith(t => taskCS.TrySetResult(false)); //Make 15Secs configurable??
            return taskCS.Task;
        }

        private void CreateStoreCallback(TSM pMsg)
        {
            if (MyStoreID.Length == 0)
            {
                if (pMsg?.PLS?.StartsWith("ERROR:") == true)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4811, new TSM("TheStorageMirror", $"Error during Store Creation", eMsgLevel.l1_Error, pMsg.PLS));
                    return;
                }
                else
                    MyStoreID = pMsg?.PLS;
            }
            if (IsCached && !IsRAMStore && !AppendOnly)
                engineStorageServer.RequestEdgeStorageData(MyStoreID, "", 0, 0, "", "cdeCTIM", "", "", RetrieveRecordsCallback, false);
            else
                FireStoreReady();
        }

        private void RetrieveRecordsCallback(TSM pMsg)
        {
            TheDataRetrievalRecord tRec = TheCommonUtils.DeserializeJSONStringToObject<TheDataRetrievalRecord>(pMsg.PLS);
            if (tRec != null)
            {
                if (tRec.ERR.Length > 0)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(472, new TSM("StorageMirror", tRec.ERR, eMsgLevel.l2_Warning));
                }
                else
                {
                    Task<TheDataRetrievalRecord> tTask = RetrieveEdgeStorageProperties(tRec);
                    if (tTask != null)
                    {
                        tTask.Wait();
                        tRec = tTask.Result;
                    }
                    List<T> tList = TheStorageUtilities.ConvertFromStoreRecord<T>(tRec, false, out string tError);
                    if (string.IsNullOrEmpty(tError))
                    {
                        MyMirrorCache.AddItems(tList, null);
                    }
                }
            }
            FireStoreReady();
        }

        private void FireStoreReady()
        {
            IsReady = true; //this is the ONLY location IsReady can be set to true
            FireEvent(eStoreEvents.StoreReady, new StoreEventArgs(MyStoreID), true);
        }

        public void NotifyOfUpdate(StoreResponse pUpdatedRecord)
        {
            if (AllowFireUpdates)
                FireEvent(eStoreEvents.HasUpdates, new StoreEventArgs(pUpdatedRecord), true);
        }

        public void ForceSave()
        {
            if (MyMirrorCache != null)
                MyMirrorCache.ForceSave();
        }

        public void Reset()
        {
            IsReady = false;
            AllowFireUpdates = false;
            if (engineStorageServer != null && TheCDEngines.MyIStorageService != null)
            {
                if (IsUpdateSubscriptionEnabled)
                {
                    TSM msgSub = new (TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineName(), "CDE_UNSUBSCRIBE", TheBaseAssets.MyScopeManager.AddScopeID(m_StoreID));
                    msgSub.SetNoDuplicates(true);
                    msgSub.SetToServiceOnly(true);
                    TheCommCore.PublishCentral(msgSub);
                }
            }
            else
                IsRAMStore = true;
            m_StoreID = "";
            if (MyMirrorCache != null)
            {
                MyMirrorCache.Reset();
            }
        }

        public void Cleanup()
        {
            TheCDEngines.UnregisterMirrorRepository(TheStorageUtilities.GenerateUniqueIDFromType(typeof(T), CacheTableName));
            TheCDEngines.UnregisterMirrorRepository(TheStorageUtilities.GenerateUniqueIDFromType(typeof(T), null));
            TheCDEngines.UnregisterMirrorRepository(StoreMID.ToString());
            Dispose();
        }

        /// <summary>
        /// New in 4.106: "logic" lock - should not be used for just read-write but for synchronization of multiple operations
        /// </summary>
        public IReaderWriterLock MyRecordsRWLock {  get { return MyMirrorCache?.MyRecordsRWLock; } }

        public long GetCount()
        {
            return Count;
        }

        public long Count
        {
            get
            {
                if (MyMirrorCache != null)
                    return MyMirrorCache.MyRecords.Count;
                return 0;
            }
        }

        public string GetFriendlyName()
        {
            if (TheCommonUtils.IsNullOrWhiteSpace(FriendlyName))
                return typeof(T).ToString();
            return FriendlyName;
        }

        public long GetSize()
        {
            if (MyMirrorCache == null || MyMirrorCache.MyRecords == null || MyMirrorCache.MyRecords.Count == 0) return 0;
            return MyMirrorCache.MyRecords.GetSize();
        }

        public bool FlushCache(bool pBackupFirst)
        {
            return MyMirrorCache.FlushCache(pBackupFirst);
        }

        public string GetCacheStoreCount()
        {
            return string.Format("{0}/{1}/{2}", MyMirrorCache.mCacheCounter,MyMirrorCache.CacheStoreInterval,MyMirrorCache.mTotalCacheCounter);
        }

        public DateTimeOffset GetLastCacheStore()
        {
            return MyMirrorCache.mLastStore;
        }

        public bool StoreCanBeFlushed()
        {
            return CanBeFlushed;
        }

        /// <summary>
        /// Currently only supported for RAM and Cached Mirrors
        /// </summary>
        /// <param name="pKey"></param>
        /// <returns></returns>
        public bool ContainsID(Guid pKey)
        {
            if (IsRAMStore || IsCached)
                return MyMirrorCache!=null && MyMirrorCache.MyRecords.ContainsKey(pKey.ToString());
            else
            {
                return false;
            }
        }
#endregion

        public void sinkProcessServiceMessages(TSM pMessage)
        {
            string[] Command = pMessage.TXT.Split(':');
            switch (Command[0])
            {
                case "DATASTOREMSG":
                    TheBaseAssets.MySYSLOG.WriteToLog(470, pMessage);
                    break;
                case "SQLEXECUTED":
                    break;
                case "DATAWASUPDATED":
                    if (pMessage.PLS.Length > 0)
                        ItemWasAddedOrUpdated(pMessage);
                    return;
                case "DATARETREIVED":
                    break;
                case "STORECREATED":
                    MyStoreID = pMessage.PLS;
                    break;
            }
        }

#region DELETE Items
        /// <summary>
        /// Removes the store from the StorageMirror repository and removes the table from the IStorageService if applicable.
        /// </summary>
        /// <param name="BackupFirst">If true, backs up the store before removing</param>
        public void RemoveStore(bool BackupFirst)
        {
            IsReady = false;
            Cleanup();
            if (IsRAMStore || IsCached)
                MyMirrorCache.RemoveStore(BackupFirst);
            if (!IsRAMStore && engineStorageServer != null)
                engineStorageServer.EdgeStorageExecuteSql(null, "Delete from {0} where UniqueID = '" + TheStorageUtilities.GenerateUniqueIDFromType(typeof(T), CacheTableName) + "'", "", "", null, CacheTableName);

        }

        /// <summary>
        /// Removes all items from the StorageMirror's underlying MirrorCache.
        /// </summary>
        public void RemoveAllItems()
        {
            if (IsRAMStore || IsCached)
                MyMirrorCache.Reset();
        }

        /// <summary>
        /// Removes multiple items from the StorageMirror's underlying MirrorCache and/or IStorageService.
        /// If IsRAMStore is false or IsCached is true, the items will be removed from the IStorageService.  If either IsRAMStore or IsCached is true, the items will be removed from the MirrorCache.
        /// </summary>
        /// <param name="pList">The List of items to remove.</param>
        /// <param name="pCallBack">The callback that will be invoked upon error or completion of the removal.</param>
        public void RemoveItems(List<T> pList, Action<StoreResponse> pCallBack)
        {
            MyMirrorCache.MyRecordsRWLock.RunUnderWriteLock(() =>   //LOCK-REVIEW: is this necessary for consitency as in RemoveAnItem is another lock?
            {
                foreach (T tItem in pList)
                    RemoveAnItem(tItem, pCallBack);
            });
        }

        /// <summary>
        /// Removes a single item from the StorageMirror's underlying MirrorCache and/or IStorageService.
        /// If IsRAMStore is false or IsCached is true, the item will be removed from the IStorageService.  If either IsRAMStore or IsCached is true, the item will be removed from the MirrorCache.
        /// </summary>
        /// <param name="item">The item to be removed.</param>
        /// <param name="pCallBack">The callback that will be invoked upon error or completion of the removal.</param>
        public void RemoveAnItem(T item, Action<StoreResponse> pCallBack)
        {
            if (item == null) return;
            if (!IsReady)
            {
                if (pCallBack != null)
                {
                    StoreResponse tResponse = new ()
                    {
                        HasErrors = true,
                        ErrorMsg = "StorageService not ready"
                    };
                    pCallBack(tResponse);
                }
                return;
            }
            if (!IsRAMStore || IsCached)
            {
                string Magix = "";
                if (pCallBack != null)
                    Magix = AddTimedRequest(pCallBack, null, null);
                if (engineStorageServer != null)
                    engineStorageServer.EdgeStorageExecuteSql(typeof(T), "delete from {0} where cdeMID='" + item.cdeMID + "'", "", Magix, (pMsg) =>
                    {
                        RemoveEdgeStorageProperties(item.cdeMID, pMsg, sinkExecuteSql);
                    }, CacheTableName); 
            }
            if (IsRAMStore || IsCached)
                MyMirrorCache.RemoveAnItemByID(item.cdeMID, null);
        }

        /// <summary>
        /// Removes a single item from the StorageMirror's underlying MirrorCache and/or IStorageService using the ID of the item.
        /// If IsRAMStore is false or IsCached is true, the item will be removed from the IStorageService.  If either IsRAMStore or IsCached is true, the item will be removed from the MirrorCache.
        /// </summary>
        /// <param name="ItemId">The ID of the item to be removed.</param>
        /// <param name="pCallBack">The callback that will be invoked upon error or completion of the removal.</param>
        public void RemoveAnItemByID(Guid ItemId, Action<StoreResponse> pCallBack)
        {
            if (ItemId == Guid.Empty) return;
            if (!IsReady)
            {
                if (pCallBack != null)
                {
                    StoreResponse tResponse = new()
                    {
                        HasErrors = true,
                        ErrorMsg = "StorageService not ready"
                    };
                    pCallBack(tResponse);
                }
                return;
            }
            //lock (MyMirrorCache.MyRecordsLock) //LOCK-REVIEW: There is a deeper lock in MirrorCache.RemoveAnItemByID...is this lock really necessary?
            if (!IsRAMStore || IsCached)
            {
                string Magix = "";
                if (pCallBack != null)
                    Magix = AddTimedRequest(pCallBack, null, null);
                if (engineStorageServer != null)
                    engineStorageServer.EdgeStorageExecuteSql(typeof(T), "delete from {0} where cdeMID='" + ItemId + "'", "", Magix, (pMsg) =>
                    {
                        RemoveEdgeStorageProperties(ItemId, pMsg, sinkExecuteSql);
                    }, CacheTableName);
            }
            if ((IsRAMStore || IsCached))
                MyMirrorCache.RemoveAnItemByID(ItemId, null);
        }

        /// <summary>
        /// Removes all the properties associated with a record signaled by the OwnerId from storage.
        /// Used as a callback once the record itself has been removed
        /// </summary>
        /// <param name="OwnerId">The cdeMID of the record that owns these properties (the cdeO of the properties)</param>
        /// <param name="ReturnedMsg">The message returned after first removing the record itself</param>
        /// /// <param name="CallBack">The method to be called after returning from attempted deletion of properties</param>
        internal void RemoveEdgeStorageProperties(Guid OwnerId, TSM ReturnedMsg, Action<TSM> CallBack)
        {
            if (typeof(T) == typeof(TheThing) && engineStorageServer != null)
            {
                engineStorageServer.EdgeStorageExecuteSql(typeof(cdeP), "delete from {0} where cdeO='" + OwnerId + "'", "", "", (pMsg) =>
                {
                    CallBack?.Invoke(ReturnedMsg);
                }, CacheTableName);
            }
        }

        /// <summary>
        /// Removes a single property record from storage
        /// </summary>
        /// <param name="OwnerId">The cdeMID of the record that owns this property (the cdeO of the property)</param>
        /// <param name="pName">The name of the property</param>
        /// /// <param name="CallBack">The method to be called after returning from attempted deletion</param>
        internal void RemoveEdgeStorageProperty(Guid OwnerId, string pName, Action<TSM> CallBack)
        {
            if (typeof(T) == typeof(TheThing) && engineStorageServer != null)
            {
                engineStorageServer.EdgeStorageExecuteSql(typeof(cdeP), "delete from {0} where cdeO='" + OwnerId + "' and Name='" + pName + "'", "", "", (pMsg) =>
                {
                    CallBack?.Invoke(pMsg);
                }, CacheTableName);
            }
        }
#endregion

#region EXECUTE Sql
        public void ExecuteSql(string tSql,string tColFilter, Action<StoreResponse> pCallBack)
        {
            if (!IsRAMStore)
            {
                string Magix = "";
                if (pCallBack != null)
                    Magix = AddTimedRequest(pCallBack, null, null);
                if (engineStorageServer != null)
                    engineStorageServer.EdgeStorageExecuteSql(typeof(T), tSql, tColFilter, Magix, sinkExecuteSql, CacheTableName);
            }
        }
        private void sinkExecuteSql(TSM pMsg)
        {
            if (pMsg == null || string.IsNullOrEmpty(m_StoreID)) return;

            TimedRequest tReq = null;
            string[] cmds = pMsg.TXT.Split(':');
            if (cmds.Length > 1)
                tReq = GetTimedRequest(cmds[1]);
            if (tReq != null)
            {
                StoreResponse tResponse = new ()
                {
                    HasErrors = false,
                    ErrorMsg = pMsg.PLS
                };
                tReq.MyRequest(tResponse);
            }
        }
#endregion

#region ADD and UPDATE Items
        /// <summary>
        /// Adds a single item to the StorageMirror's underlying MirrorCache and/or IStorageService.
        /// </summary>
        /// <param name="pDetails">The item to be added.</param>
        public void AddAnItem(T pDetails)
        {
            AddAnItem(pDetails, null);
        }

        /// <summary>
        /// Adds a single item to the StorageMirror's underlying MirrorCache and/or IStorageService.
        /// </summary>
        /// <param name="pKey">The ID for the item that will be used to identify it in the StorageMirror.</param>
        /// <param name="pDetails">The item to be added.</param>
        public void AddAnItem(string pKey,T pDetails)
        {
            Dictionary<string, T> tList = new () {{pKey, pDetails}};
            AddItems(tList, null);
        }

        /// <summary>
        /// Adds a single item to the StorageMirror's underlying MirrorCache and/or IStorageService and hands the inserted item back to the callback.
        /// </summary>
        /// <param name="pDetails">The item to be added. The cdeMID of the item will be used as its ID in the StorageMirror.</param>
        /// <param name="pCallBack">The callback method that will be invoked upon error or completed insertion of the item.</param>
        public void AddAnItem(T pDetails, Action<StoreResponse> pCallBack)
        {
            Dictionary<string, T> tList = new () {{Guid.Empty.ToString(), pDetails}};
            AddItems(tList, pCallBack);
        }

        /// <summary>
        /// Adds new items to the StorageMirror's underlying MirrorCache and/or IStorageService.
        /// </summary>
        /// <param name="pDetails">The Dictionary of items to be added.  If a key is null or Guid.Empty, a new ID will be assigned to the item.</param>
        public void AddItems(Dictionary<string, T> pDetails)
        {
            AddItems(pDetails, null);
        }

        /// <summary>
        /// Adds new items to the StorageMirror's underlying MirrorCache and/or IStorageService and hands the inserted items back to the callback.
        /// </summary>
        /// <param name="pDetails">The List of items to be added.  The cdeMID of each item will be used as its ID in the StorageMirror.</param>
        /// <param name="pCallBack">The callback method that will be invoked upon error or completed insertion of items.</param>
        public void AddItems(List<T> pDetails, Action<StoreResponse> pCallBack)
        {
            if (pDetails == null)
                return;
            Dictionary<string, T> tList = new ();
            foreach (T pKey in pDetails)
            {
                tList.Add(pKey.cdeMID.ToString(), pKey);
            }
            AddItems(tList, pCallBack);
        }

        /// <summary>
        /// Adds new items to the StorageMirror's underlying MirrorCache and/or IStorageService and hands the inserted items back to the callback.
        /// </summary>
        /// <param name="pDetails">The Dictionary of items to be added.  If a key is null or Guid.Empty, a new ID will be assigned to the item.</param>
        /// <param name="pCallBack">The callback method that will be invoked upon error or completed insertion of items.</param>
        public void AddItems(Dictionary<string, T> pDetails, Action<StoreResponse> pCallBack)
        {
            if (!IsReady)
            {
                if (pCallBack != null)
                {
                    StoreResponse tResponse = new ()
                    {
                        HasErrors = true,
                        ErrorMsg = "StorageService not ready"
                    };
                    pCallBack(tResponse);
                }
                return;
            }
            if ((IsRAMStore || IsCached) && !AppendOnly)
            {
                StoreResponse tResponse = new ();
                Dictionary<string, T> ResList = new ();
                MyMirrorCache.MyRecordsRWLock.RunUnderWriteLock(() =>
                {
                    foreach (string tKey in pDetails.Keys)
                    {
                        T tItem = pDetails[tKey];
                        if (tKey != Guid.Empty.ToString())
                            tItem.cdeMID = TheCommonUtils.CGuid(tKey);
                        if (tItem.cdeMID == Guid.Empty)
                            tItem.cdeMID = Guid.NewGuid();
                        MyMirrorCache.AddOrUpdateItem(tItem.cdeMID, tItem, null);
                        ResList.Add(tItem.cdeMID.ToString(), tItem);
                    }
                    MyMirrorCache.RemoveRetired();
                    tResponse.MyRecords = ResList.Values.ToList();
                });
                pCallBack?.Invoke(tResponse);
                NotifyOfUpdate(tResponse);
            }
            if (!IsRAMStore)
            {
                string Magix = "";
                if (pCallBack != null)
                    Magix = AddTimedRequest(pCallBack, null, pDetails);
                if (engineStorageServer != null)
                    engineStorageServer.EdgeDataStore(pDetails, eSCMD.Insert, Magix, (pMsg) =>
                    {
                        InsertUpdateEdgeStorageProperties(pDetails, pMsg, ItemWasAddedOrUpdated);
                    }, CacheTableName);
            }
            else if (AppendOnly) //only for RAM stores
            {
                MyMirrorCache.MyRecordsRWLock.RunUnderWriteLock(() =>
                {
                    MyMirrorCache.AppendRecordsToDisk(pDetails, true, false);
                });
                // Should anything happen after this? Callbacks, etc.
            }
        }

        public void WriteCache(Action<StoreResponse> pCallBack)
        {
            if (!IsReady || IsRAMStore)
                return;
            Dictionary<string, T> ResList = new ();
            foreach (T tItem in MyMirrorCache.TheValues)
                ResList.Add(tItem.cdeMID.ToString(), tItem);

            string Magix = "";
            if (pCallBack != null)
                Magix = AddTimedRequest(pCallBack, null, ResList);
            if (engineStorageServer != null)
                engineStorageServer.EdgeDataStore(ResList, eSCMD.Insert, Magix, (pMsg) =>
                {
                    InsertUpdateEdgeStorageProperties(ResList, pMsg, ItemWasAddedOrUpdated);
                }, CacheTableName);
        }

        /// <summary>
        /// Callback from Server
        /// </summary>
        /// <param name="pMsg"></param>

        private void ItemWasAddedOrUpdated(TSM pMsg)
        {
            if (pMsg == null || string.IsNullOrEmpty(pMsg.PLS) || string.IsNullOrEmpty(m_StoreID)) return;
            if (pMsg.PLS.StartsWith(m_StoreID))
            {
                string[] tResult = pMsg.PLS.Split(':');
                StoreResponse tResp = new ()
                {
                    HasErrors = false,
                    ErrorMsg = "StoreHasUpdates"
                };
                if (tResult.Length>1)
                    tResp.SQLFilter = tResult[1];
                NotifyOfUpdate(tResp);
                return;
            }
            TheDataRetrievalRecord tRecords = null;
            TimedRequest tReq = null;
            try
            {
                tRecords = TheCommonUtils.DeserializeJSONStringToObject<TheDataRetrievalRecord>(pMsg.PLS);
            }
            catch (Exception)
            {
                //intentionally blank
            }
            if (tRecords?.UID == null || !tRecords.UID.Equals(m_StoreID))
                return;

            string[] cmds = pMsg.TXT.Split(':');
            if (cmds.Length > 1)
                tReq = GetTimedRequest(cmds[1]);
            if (tReq == null && !AllowFireUpdates)
                return;
            StoreResponse tResponse = new ();
            if (!string.IsNullOrEmpty(tRecords.ERR))
            {
                tResponse.HasErrors = true;
                tResponse.ErrorMsg = tRecords.ERR;
                TheBaseAssets.MySYSLOG.WriteToLog(471, new TSM("StorageMirror", "ItemWasAddedOrReceived Error", eMsgLevel.l1_Error, pMsg.PLS));
            }
            else
            {
                tResponse.MyRecords = TheStorageUtilities.ConvertFromStoreRecord<T>(tRecords, true, out string tError);
                if (!string.IsNullOrEmpty(tError))
                {
                    tResponse.HasErrors = true;
                    tResponse.ErrorMsg = tError;
                }
            }
            if (tResponse.MyRecords == null || tResponse.MyRecords.Count == 0)
            {
                tResponse.HasErrors = true;
                tResponse.ErrorMsg = "No Records Returned";
            }
            if (tReq != null)
                tReq.MyRequest(tResponse);
            NotifyOfUpdate(tResponse);
        }

        /// <summary>
        /// Updates the item with the <paramref name="pOld"/> cdeMID to a new cdeMID of <paramref name="pNew"/>.
        /// </summary>
        /// <param name="pOld"></param>
        /// <param name="pNew"></param>
        public void UpdateID(Guid pOld, Guid pNew)
        {
            if (IsRAMStore || IsCached)
            {
                MyMirrorCache.MyRecordsRWLock.RunUnderWriteLock(() =>
                {
                    T item = MyMirrorCache.GetEntryByID(pOld);
                    if (item != null)
                    {
                        RemoveAnItem(item, null);
                        item.cdeMID = pNew;
                        AddAnItem(item);
                    }
                });
            }
        }
        /// <summary>
        /// Updates a single item in the StorageMirror's underlying MirrorCache and/or IStorageService.
        /// </summary>
        /// <param name="pDetails">The item to be updated. If the item's cdeMID is Guid.Empty, the item will be added with a new ID.</param>
        public void UpdateItem(T pDetails)
        {
            UpdateItem(pDetails, null);
        }

        /// <summary>
        /// Updates a single item in the StorageMirror's underlying MirrorCache and/or IStorageService and hands the updated item back to the callback.
        /// </summary>
        /// <param name="pDetails">The item to be updated. If the item's cdeMID is Guid.Empty, the item will be added with a new ID.</param>
        /// <param name="CallBack">The callback method that will be invoked upon error or completed update of the item.</param>
        public void UpdateItem(T pDetails, Action<StoreResponse> CallBack)
        {
            if (pDetails == null) return;   //SHOULD NEVER HAPPEN..but did:(
            Dictionary<string, T> tList = new () {{pDetails.cdeMID.ToString(), pDetails}};
            UpdateItems(tList, CallBack);
        }

        /// <summary>
        /// Updates the first item that matches the <paramref name="pSelector"/> with the item <paramref name="pItem"/> in the StorageMirror's underlying MirrorCache and/or IStorageService.
        /// </summary>
        /// <param name="pItem">The item used to update the existing item in the StorageMirror.</param>
        /// <param name="pSelector">The Func to identify the item that will be updated.</param>
        public void UpdateItemByFunc(T pItem, Func<T, bool> pSelector)
        {
            Dictionary<string, T> tList = new();
            if ((IsRAMStore || IsCached) && pSelector != null)
            {
                T ttt = MyMirrorCache.GetEntryByFunc(pSelector);
                if (ttt != null)
                    pItem.cdeMID = ttt.cdeMID;
            }
            tList.Add(pItem.cdeMID.ToString(), pItem);
            UpdateItems(tList, null);
        }

        /// <summary>
        /// Updates items in the StorageMirror's underlying MirrorCache and/or IStorageService.
        /// </summary>
        /// <param name="pDetails">The Dictionary of items to be updated. If an item's cdeMID is Guid.Empty, the item will be added with a new ID.</param>
        public void UpdateItems(Dictionary<string, T> pDetails)
        {
            UpdateItems(pDetails, null);
        }

        /// <summary>
        /// Updates items in the StorageMirror's underlying MirrorCache and/or IStorageService.
        /// </summary>
        /// <param name="pDetails">The List of items to be updated. If an item's cdeMID is Guid.Empty, the item will be added with a new ID.</param>
        public void UpdateItems(List<T> pDetails)
        {
            Dictionary<string, T> tList = new ();
            foreach (T t in pDetails)
                tList.Add(t.cdeMID.ToString(), t);
            UpdateItems(tList, null);
        }

        /// <summary>
        ///  Updates items in the StorageMirror's underlying MirrorCache and/or IStorageService and hands the updated items back to the callback.
        /// </summary>
        /// <param name="pDetails">The Dictionary of items to be updated.  If an item's cdeMID is Guid.Empty, the item will be added with a new ID.</param>
        /// <param name="CallBack">The callback method that will be invoked upon error or completed update of items.</param>
        public void UpdateItems(Dictionary<string,T> pDetails, Action<StoreResponse> CallBack)
        {
            StoreResponse tResponse = new ();
            if (!IsReady)
            {
                if (CallBack != null)
                {
                    tResponse.HasErrors = true;
                    tResponse.ErrorMsg = "StorageService not ready";
                    CallBack(tResponse);
                }
                return;
            }

            if (IsRAMStore || IsCached)
            {
                Dictionary<string, T> ResList = new ();
                foreach (T tItem in pDetails.Values)
                {
                    if (tItem.cdeMID == Guid.Empty)
                        tItem.cdeMID = Guid.NewGuid();
                    MyMirrorCache.AddOrUpdateItem(tItem.cdeMID, tItem, null);
                    ResList.Add(tItem.cdeMID.ToString(), tItem);
                }
                tResponse.MyRecords = ResList.Values.ToList();
                CallBack?.Invoke(tResponse);
                NotifyOfUpdate(tResponse);
            }
            if (!IsRAMStore)
            {
                string Magix = "";
                if (CallBack != null)
                    Magix = AddTimedRequest(CallBack, null, pDetails);
                if (engineStorageServer != null)
                    engineStorageServer.EdgeDataStore(pDetails, eSCMD.InsertOrUpdate, Magix, (pMsg) =>
                    {
                        InsertUpdateEdgeStorageProperties(pDetails, pMsg, ItemWasAddedOrUpdated);
                    }, CacheTableName);   //.update
            }
        }

        /// <summary>
        /// Updates a store in the underlying IStorageService with a new FriendlyName and/or Description.
        /// </summary>
        /// <param name="storeParameters"></param>
        public void UpdateStore(TheStorageMirrorParameters storeParameters)
        {
            if(storeParameters != null && engineStorageServer != null)
            {
                engineStorageServer.EdgeUpdateStore(typeof(T), storeParameters.FriendlyName, storeParameters.Description, null, CacheTableName);
            }
        }

        /// <summary>
        /// Inserts or updates a record's property bag into storage after the record itself has been inserted
        /// </summary>
        /// <param name="pDetails">Classes of type T that contain property bags</param>
        /// <param name="ReturnedMsg">The message returned after first inserting the records themselves</param>
        /// /// <param name="CallBack">Method that should be called after inserting the property bags</param>
        public void InsertUpdateEdgeStorageProperties(Dictionary<string, T> pDetails, TSM ReturnedMsg, Action<TSM> CallBack)
        {
            if (typeof(T) == typeof(TheThing))
            {
                TheDataRetrievalRecord tRecords = TheCommonUtils.DeserializeJSONStringToObject<TheDataRetrievalRecord>(ReturnedMsg.PLS);
                if (string.IsNullOrEmpty(tRecords.ERR))
                {
                    List<T> tList = pDetails.Values.ToList();
                    foreach (T entry in tList)
                    {
                        if (entry is TheThing tThing)
                        {
                            engineStorageServer.EdgeDataStore(tThing.MyPropertyBag.GetDictionarySnapshot(), eSCMD.InsertOrUpdate, "", (pMsg2) =>
                            {
                                TheDataRetrievalRecord tRecords2 = TheCommonUtils.DeserializeJSONStringToObject<TheDataRetrievalRecord>(pMsg2.PLS);
                                if (string.IsNullOrEmpty(tRecords2.ERR))
                                {
                                    List<cdeP> propertyRecords = TheStorageUtilities.ConvertFromStoreRecord<cdeP>(tRecords2, true, out string tError);
                                    if (string.IsNullOrEmpty(tError))
                                    {
                                        TFD PBDefinition = new ()
                                        {
                                            N = "MyPropertyBag",
                                            T = TheCommonUtils.CStr(typeof(cdeConcurrentDictionary<string, cdeP>)),
                                            C = tRecords.FLDs[tRecords.FLDs.Count - 1].C + 1
                                        };
                                        tRecords.FLDs.Add(PBDefinition);
                                        cdeConcurrentDictionary<string, cdeP> propertyBag = new ();
                                        foreach (cdeP p in propertyRecords)
                                        {
                                            if (!propertyBag.TryAdd(p.Name, p))
                                            {
                                                TheBaseAssets.MySYSLOG.WriteToLog(477, new TSM("StorageMirror", "Error adding retrieved property", eMsgLevel.l1_Error, p.Name + ":" + p.cdeMID));
                                            }
                                        }
                                        int LastColumn = TheCommonUtils.CInt(tRecords.RECs[0][tRecords.RECs[0].Count - 1].Substring(0, 3));
                                        string SerializedPB = TheCommonUtils.SerializeObjectToJSONString(propertyBag);
                                        tRecords.RECs[0].Add(string.Format("{0:000}{1}", LastColumn + 1, SerializedPB));
                                        ReturnedMsg.PLS = TheCommonUtils.SerializeObjectToJSONString(tRecords);
                                    }
                                }
                            }, CacheTableName);
                        }
                    }
                }
            }
            CallBack?.Invoke(ReturnedMsg);
        }
#endregion

#region GET Records

        public List<T> TheValues
        {
            get
            {
                if ((IsCached || IsRAMStore) && MyMirrorCache.MyRecords != null)
                    return MyMirrorCache.TheValues;
                return new List<T>();
            }
        }

        public List<string> TheKeys
        {
            get
            {
                if ((IsCached || IsRAMStore) && MyMirrorCache.MyRecords != null)
                    return MyMirrorCache.TheKeys;
                return new List<string>();
            }
        }

        /// <summary>
        /// Retrieves records from the StorageMirror's underlying MirrorCache or IStorageService and hands them to the callback.
        /// If IsRAMStore or IsCached, the records will be retrieved from the MirrorCache.
        /// If IsRAMStore is false, the records will be retrieved from the IStorageService.
        /// </summary>
        /// <param name="pCallBack">The callback method that will be invoked upon error or completed retrieval of records.</param>
        /// <param name="LocalCallBackOnly">If set to true, no request is sent to a remote StorageService.</param>
        /// <returns>True if the request was successfully made and false otherwise (if the StorageMirror is not ready yet and was not initialized).</returns>
        public bool GetRecords(Action<StoreResponse> pCallBack, bool LocalCallBackOnly)
        {
            return GetRecords("", pCallBack,LocalCallBackOnly);
        }

        /// <summary>
        /// Retrieves records from the StorageMirror's underlying MirrorCache or IStorageService and hands them to the callback.
        /// If IsRAMStore or IsCached, the records will be retrieved from the MirrorCache.
        /// If IsRAMStore is false, the records will be retrieved from the IStorageService.
        /// </summary>
        /// <param name="SQLFilter">The "WHERE" clause to filter the records returned. Generally the filter should consist of the form [PropertyName] [Operation] [Value], e.g. FriendlyName = "MyThing"</param>
        /// <param name="pCallBack">The callback method that will be invoked upon error or completed retrieval of records.</param>
        /// <param name="LocalCallBackOnly">If set to true, no request is sent to a remote StorageService.</param>
        /// <returns>True if the request was successfully made and false otherwise (if the StorageMirror is not ready yet and was not initialized).</returns>
        public bool GetRecords(string SQLFilter, Action<StoreResponse> pCallBack, bool LocalCallBackOnly)
        {
            return GetRecords(SQLFilter, 0,0, "", pCallBack, LocalCallBackOnly,false);
        }

        /// <summary>
        /// Retrieves records from the StorageMirror's underlying MirrorCache or IStorageService and hands them to the callback.
        /// If IsRAMStore or IsCached, the records will be retrieved from the MirrorCache.
        /// If IsRAMStore is false, the records will be retrieved from the IStorageService.
        /// </summary>
        /// <param name="TopRows">The maximum number of records to return from the query</param>
        /// <param name="pCallBack">The callback method that will be invoked upon error or completed retrieval of records.</param>
        /// <param name="LocalCallBackOnly">If set to true, no request is sent to a remote StorageService.</param>
        /// <returns>True if the request was successfully made and false otherwise (if the StorageMirror is not ready yet and was not initialized).</returns>
        public bool GetRecords(int TopRows, Action<StoreResponse> pCallBack, bool LocalCallBackOnly)
        {
            return GetRecords("", TopRows,0, "", pCallBack, LocalCallBackOnly,false);
        }

        /// <summary>
        /// Retrieves records from the StorageMirror's underlying MirrorCache or IStorageService and hands them to the callback.
        /// If IsRAMStore or IsCached, the records will be retrieved from the MirrorCache.
        /// If IsRAMStore is false, the records will be retrieved from the IStorageService.
        /// </summary>
        /// <param name="SQLFilter">The "WHERE" clause to filter the records returned. Generally the string should consist of the form [PropertyName] [Operation] [Value], e.g. FriendlyName = "MyThing"</param>
        /// <param name="TopRows">The maximum number of records to return from the query</param>
        /// <param name="pCallBack">The callback method that will be invoked upon error or completed retrieval of records.</param>
        /// <param name="LocalCallBackOnly">If set to true, no request is sent to a remote StorageService.</param>
        /// <returns>True if the request was successfully made and false otherwise (if the StorageMirror is not ready yet and was not initialized).</returns>
        public bool GetRecords(string SQLFilter, int TopRows, Action<StoreResponse> pCallBack, bool LocalCallBackOnly)
        {
            return GetRecords(SQLFilter, TopRows,0, "", pCallBack, LocalCallBackOnly,false);
        }

        /// <summary>
        /// Retrieves records from the StorageMirror's underlying MirrorCache or IStorageService and hands them to the callback.
        /// If IsRAMStore or IsCached, the records will be retrieved from the MirrorCache.
        /// If IsRAMStore is false, the records will be retrieved from the IStorageService.
        /// </summary>
        /// <param name="SQLFilter">The "WHERE" clause to filter the records returned. Generally the filter should consist of the form [PropertyName] [Operation] [Value], e.g. FriendlyName = "MyThing"</param>
        /// <param name="TopRows">The maximum number of records to return from the query</param>
        /// <param name="PageNumber">The page number for the query, if the IStorageService supports a paging mechanism.  Note that this is only used for retrieval from the IStorageService and not the MirrorCache.</param>
        /// <param name="SQLOrder">The "ORDER BY" clause to order the records returned.  Generally the string should consist of the form [PropertyName] [desc | asc], e.g. cdeCTIM desc</param>
        /// <param name="pCallBack">The callback method that will be invoked upon error or completed retrieval of records.</param>
        /// <param name="LocalCallBackOnly">If set to true, no request is sent to a remote StorageService.</param>
        /// <param name="InitIfNotInit">If the StorageMirror is not ready yet and this value is true, InitializeStore will be called before proceeding with the request.</param>
        /// <returns>True if the request was successfully made and false otherwise (if the StorageMirror is not ready yet and was not initialized).</returns>
        public bool GetRecords(string SQLFilter, int TopRows, int PageNumber, string SQLOrder, Action<StoreResponse> pCallBack, bool LocalCallBackOnly, bool InitIfNotInit)
        {
            return GetRecords(SQLFilter, TopRows, PageNumber, SQLOrder,"", pCallBack, LocalCallBackOnly,InitIfNotInit, null);
        }

        /// <summary>
        /// Retrieves records from the StorageMirror's underlying MirrorCache or IStorageService and hands them to the callback.
        /// If IsRAMStore or IsCached, the records will be retrieved from the MirrorCache.
        /// If IsRAMStore is false, the records will be retrieved from the IStorageService.
        /// </summary>
        /// <param name="SQLFilter">The "WHERE" clause to filter the records returned. Generally the filter should consist of the form [PropertyName] [Operation] [Value], e.g. FriendlyName = "MyThing"</param>
        /// <param name="TopRows">The maximum number of records to return from the query</param>
        /// <param name="PageNumber">The page number for the query, if the IStorageService supports a paging mechanism.  Note that this is only used for retrieval from the IStorageService and not the MirrorCache.</param>
        /// <param name="SQLOrder">The "ORDER BY" clause to order the records returned.  Generally the string should consist of the form [PropertyName] [desc | asc], e.g. cdeCTIM desc</param>
        /// <param name="pCallBack">The callback method that will be invoked upon error or completed retrieval of records.</param>
        /// <param name="LocalCallBackOnly">If set to true, no request is sent to a remote StorageService.</param>
        /// <param name="InitIfNotInit">If the StorageMirror is not ready yet and this value is true, InitializeStore will be called before proceeding with the request.</param>
        /// <param name="pCookie">A cookie that will later be returned with the StoreResponse in the callback.</param>
        /// <returns>True if the request was successfully made and false otherwise (if the StorageMirror is not ready yet and was not initialized).</returns>
        public bool GetRecords(string SQLFilter, int TopRows, int PageNumber, string SQLOrder, Action<StoreResponse> pCallBack, bool LocalCallBackOnly, bool InitIfNotInit, object pCookie)
        {
            return GetRecords(SQLFilter, TopRows, PageNumber, SQLOrder,"", pCallBack, LocalCallBackOnly, InitIfNotInit, pCookie);
        }

        /// <summary>
        /// Retrieves records from the StorageMirror's underlying MirrorCache or IStorageService and hands them to the callback.
        /// If IsRAMStore or IsCached, the records will be retrieved from the MirrorCache.
        /// If IsRAMStore is false, the records will be retrieved from the IStorageService.
        /// </summary>
        /// <param name="SQLFilter">The "WHERE" clause to filter the records returned. Generally the filter should consist of the form [PropertyName] [Operation] [Value], e.g. FriendlyName = "MyThing"</param>
        /// <param name="TopRows">The maximum number of records to return from the query</param>
        /// <param name="PageNumber">The page number for the query, if the IStorageService supports a paging mechanism.  Note that this is only used for retrieval from the IStorageService and not the MirrorCache.</param>
        /// <param name="SQLOrder">The "ORDER BY" clause to order the records returned.  Generally the string should consist of the form [PropertyName] [desc | asc], e.g. cdeCTIM desc</param>
        /// <param name="ColFilter">The "SELECT" clause to specify which columns (properties) will be contained in the records returned.  Note that this is only used for retrieval from the IStorageService and not the MirrorCache.
        ///                         Generally the string should consist of the form [PropertyName1], [PropertyName2], ... e.g. cdeMID, cdeN, ...etc.</param>
        /// <param name="pCallBack">The callback method that will be invoked upon error or completed retrieval of records.</param>
        /// <param name="LocalCallBackOnly">If set to true, no request is sent to a remote StorageService.</param>
        /// <param name="InitIfNotInit">If the StorageMirror is not ready yet and this value is true, InitializeStore will be called before proceeding with the request.</param>
        /// <param name="pCookie">A cookie that will later be returned with the StoreResponse in the callback.</param>
        /// <returns>True if the request was successfully made and false otherwise (if the StorageMirror is not ready yet and was not initialized).</returns>
        public bool GetRecords(string SQLFilter, int TopRows, int PageNumber, string SQLOrder,string ColFilter, Action<StoreResponse> pCallBack, bool LocalCallBackOnly, bool InitIfNotInit, object pCookie)
        {
            StoreResponse tResponse = new ();
            if (!IsReady && InitIfNotInit)
                InitializeStore(false);
            if (!IsReady)
            {
                if (pCallBack != null)
                {
                    tResponse.HasErrors = true;
                    tResponse.Cookie = pCookie;
                    tResponse.SQLFilter = SQLFilter;
                    tResponse.ErrorMsg = "StorageService not ready";
                    pCallBack(tResponse);
                }
                return false;
            }
            if(AppendOnly && engineStorageServer == null)
            {
                tResponse = MyMirrorCache.GetRecordsFromDisk(SQLFilter, SQLOrder, PageNumber, TopRows);
                tResponse.SQLOrder = SQLOrder;
                tResponse.SQLFilter = SQLFilter;
                tResponse.Cookie = pCookie;
                pCallBack(tResponse);
                return true;
            }
            if ((IsRAMStore || IsCached) && !AppendOnly)
            {
                MyMirrorCache.MyRecordsRWLock.RunUnderReadLock(() =>    //LOCK-REVIEW: New reader lock for result consitency
                {
                    try
                    {
                        int reccnt = MyMirrorCache.MyRecords.Values.Count;
                        if (TopRows > 0 && reccnt > TopRows) reccnt = TopRows;
                        if (reccnt > 0)
                        {
                            ICollection<T> tRecords = null;
                            if (!TheCommonUtils.IsNullOrWhiteSpace(SQLFilter))
                            {
                                try
                                {
                                    List<SQLFilter> filter = TheStorageUtilities.CreateFilter(SQLFilter);
                                    var deleg = ExpressionBuilder.GetExpression<T>(filter).Compile();
                                    tRecords = MyMirrorCache.MyRecords.Values.Where(deleg).ToList();
                                }
                                catch
                                {
                                    tRecords = null;
                                    tResponse.HasErrors = true;
                                    tResponse.ErrorMsg = "Error in Filter: " + SQLFilter;
                                }
                            }
                            else
                                tRecords = MyMirrorCache.MyRecords.Values;
                            if (tRecords != null && tRecords.Count > 0)
                            {
                                if (reccnt > tRecords.Count) reccnt = tRecords.Count;
                                if (reccnt < tRecords.Count)
                                {
                                    int fact = (tRecords.Count / reccnt) + 1;
                                    tResponse.MyRecords = "cdeCTIM desc".Equals(SQLOrder) ? tRecords.OrderByDescending(s => s.cdeCTIM).Where((x, i) => i % fact == 0).ToList() : tRecords.Where((x, i) => i % fact == 0).ToList();
                                }
                                else
                                {
                                    tResponse.MyRecords = "cdeCTIM desc".Equals(SQLOrder) ? tRecords.OrderByDescending(s => s.cdeCTIM).ToList().GetRange(0, reccnt) : tRecords.ToList().GetRange(0, reccnt);
                                }
                            }
                            else
                            {
                                tResponse.HasErrors = true;
                                tResponse.ErrorMsg = "No records found that match the criterial in SQL Filter :" + SQLFilter;
                            }
                        }
                        else
                        {
                            tResponse.HasErrors = true;
                            tResponse.ErrorMsg = "Storage is empty";
                        }
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(472, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("StorageMirror", "GetRecords failed", eMsgLevel.l2_Warning, e.ToString()));
                    }
                });
                tResponse.SQLOrder = SQLOrder;
                tResponse.SQLFilter = SQLFilter;
                tResponse.Cookie = pCookie;
                pCallBack(tResponse);
            }
            if (!IsRAMStore)
            {
                string Magix = "";
                if (pCallBack != null)
                    Magix = AddTimedRequest(pCallBack, pCookie, null);
                if (engineStorageServer != null)
                {
                    if (typeof(T) == typeof(TheThingStore))
                        ColFilter = "";
                    engineStorageServer.RequestEdgeStorageData(MyStoreID, ColFilter, TopRows, PageNumber, SQLFilter, SQLOrder, "", Magix, sinkJustReturn, LocalCallBackOnly);
                }
            }
            return true;
        }

        /// <summary>
        /// Retrieves the property bag for data records from storage
        /// </summary>
        /// <param name="pRec">Record with property bags that have not yet been retrieved</param>
        /// <returns>New record with property bags filled in the appropriate REC fields</returns>
        public Task<TheDataRetrievalRecord> RetrieveEdgeStorageProperties(TheDataRetrievalRecord pRec)
        {
            var taskCS = new TaskCompletionSource<TheDataRetrievalRecord>();
            if (typeof(T) == typeof(TheThing))
            {
                int cdeMIDIndex= -1;
                if (string.IsNullOrEmpty(pRec.ERR))
                {
                    for(int i = 0; i < pRec.FLDs.Count; i++)
                    {
                        if(pRec.FLDs[i].N == "cdeMID")
                        {
                            cdeMIDIndex = i;
                            break;
                        }
                    }
                }
                if (cdeMIDIndex >= 0)
                {
                    engineStorageServer.RequestEdgeStorageData(typeof(cdeP), "", 0, 0, "", "", "", "", (pMsg) =>
                    {
                        TheDataRetrievalRecord tPropertyRec = TheCommonUtils.DeserializeJSONStringToObject<TheDataRetrievalRecord>(pMsg.PLS);
                        if(!string.IsNullOrEmpty(tPropertyRec.ERR))
                            TheBaseAssets.MySYSLOG.WriteToLog(475, new TSM("StorageMirror", "RetrieveEdgeStorageProperties Error", eMsgLevel.l1_Error, pMsg.PLS));
                        else
                        {
                            List<cdeP> PropertyRecords = TheStorageUtilities.ConvertFromStoreRecord<cdeP>(tPropertyRec, true, out string tError);
                            if (!string.IsNullOrEmpty(tError))
                                TheBaseAssets.MySYSLOG.WriteToLog(476, new TSM("StorageMirror", "Error converting property records", eMsgLevel.l1_Error, pMsg.PLS));
                            else
                            {
                                TFD PBDefinition = new ()
                                {
                                    N = "MyPropertyBag",
                                    T = TheCommonUtils.CStr(typeof(cdeConcurrentDictionary<string, cdeP>)),
                                    C = pRec.FLDs[pRec.FLDs.Count - 1].C + 1
                                };
                                pRec.FLDs.Add(PBDefinition);
                                foreach (List<string> tList in pRec.RECs)
                                {
                                    cdeConcurrentDictionary<string, cdeP> propertyBag = new ();
                                    for(int j = PropertyRecords.Count - 1; j > -1; j--)
                                    {
                                        if(tList[cdeMIDIndex].Substring(3).Equals(TheCommonUtils.CStr(PropertyRecords[j].cdeO)))
                                        {
                                            if (!propertyBag.TryAdd(PropertyRecords[j].Name, PropertyRecords[j]))
                                            {
                                                TheBaseAssets.MySYSLOG.WriteToLog(477, new TSM("StorageMirror", "Error adding retrieved property for " + PropertyRecords[j].cdeO, eMsgLevel.l1_Error, PropertyRecords[j].Name + ":" + PropertyRecords[j].cdeMID));
                                            }
                                            else
                                                PropertyRecords.RemoveAt(j);
                                        }
                                    }
                                    int LastColumn = TheCommonUtils.CInt(tList[tList.Count - 1].Substring(0, 3));
                                    string SerializedPB = TheCommonUtils.SerializeObjectToJSONString(propertyBag);
                                    tList.Add(string.Format("{0:000}{1}", LastColumn + 1, SerializedPB));
                                }
                                taskCS.TrySetResult(pRec);
                            }
                        }
                    }, false, CacheTableName);
                }
                TheCommonUtils.TaskWaitTimeout(taskCS.Task, new TimeSpan(0, 0, 15)).ContinueWith(t => taskCS.TrySetResult(pRec));
                return taskCS.Task;
            }
            return Task.FromResult<TheDataRetrievalRecord>(pRec);
        }

        /// <summary>
        /// Retrieves groups of records from the StorageMirror's underlying MirrorCache or IStorageService and hands it to the callback.
        /// If IsRAMStore or IsCached, the groups will be retrieved from the MirrorCache.
        /// If IsRAMStore is false, the groups will be retrieved from the IStorageService.
        /// </summary>
        /// <param name="pColumFilter">The "SELECT" clause to specify which columns (properties) will be contained in the records returned.  Note that this is only used for retrieval from the IStorageService and not the MirrorCache.
        ///                         Generally the string should consist of the form [PropertyName1], [PropertyName2], ... e.g. EngineName, DeviceType, ...etc.
        ///                         If <paramref name="pGrouping"/> is specified, this should typically contain an aggregate function and only columns also in the <paramref name="pGrouping"/>.</param>
        /// <param name="pGrouping">The "GROUP BY" clause to specify how to group the records.  Note that for retrieval from the MirrorCache, this should only contain one property name.
        ///                         Otherwise, the string should generally consist of the form [PropertyName1], [PropertyName2], ... e.g. EngineName, DeviceType, ... etc.  </param>
        /// <param name="CallBack">The callback method that will be invoked upon error or completed retrieval of records.</param>
        /// <param name="LocalCallBackOnly">If set to true, no request is sent to a remote StorageService.</param>
        /// <param name="pCookie">A cookie that will later be returned with the StoreResponse in the callback.</param>
        public void GetGroupResult(string pColumFilter, string pGrouping, Action<StoreResponse> CallBack, bool LocalCallBackOnly, object pCookie)
        {
            StoreResponse tResponse = new ();
            if (!IsReady)
            {
                if (CallBack != null)
                {
                    tResponse.HasErrors = true;
                    tResponse.SQLFilter = pColumFilter;
                    tResponse.ErrorMsg = "StorageService not ready";
                    tResponse.Cookie = pCookie;
                    CallBack(tResponse);
                }
                return;
            }
            if (IsRAMStore || IsCached)
            {
                if (MyMirrorCache.Count > 0)
                {
                    BuildGroups(MyMirrorCache.TheValues, pGrouping, tResponse);
                }
                else
                {
                    tResponse.HasErrors = true;
                    tResponse.ErrorMsg = "No records found";
                }
                tResponse.Cookie = pCookie;
                CallBack(tResponse);
            }
            if (!IsRAMStore)
            {
                string Magix = "";
                if (CallBack != null)
                    Magix = AddTimedRequest(CallBack, pCookie, null);
                if (engineStorageServer != null)
                    engineStorageServer.RequestEdgeStorageData(MyStoreID, pColumFilter, 0, 0, "", "", pGrouping, Magix, sinkGroupResults, LocalCallBackOnly);
            }
        }

        private void BuildGroups(List<T> mRecords, string pGrouping, StoreResponse tResponse)
        {
            List<string> tNames = new ();
            foreach (var t in mRecords)
            {
                object tName = TheCommonUtils.GetPropValue(t, pGrouping);
                if (tName != null && !tNames.Contains(tName.ToString().ToLower()))
                    tNames.Add(tName.ToString().ToLower());
            }
            tResponse.ColumnFilter = TheCommonUtils.CListToString(tNames, ";");
        }

        private void sinkGroupResults(TSM pMsg)
        {
            TheDataRetrievalRecord tRec = TheCommonUtils.DeserializeJSONStringToObject<TheDataRetrievalRecord>(pMsg.PLS);
            if (tRec == null) return;
            string LastID = tRec.MID;
            StoreResponse tResponse = new ()
            {
                SQLOrder = tRec.SOR,
                SQLFilter = tRec.SFI,
                ColumnFilter = tRec.CFI,
                PageNumber = tRec.PAG
            };
            if (tRec.ERR.Length > 0)
            {
                tResponse.HasErrors = true;
                tResponse.ErrorMsg = tRec.ERR;
                TheBaseAssets.MySYSLOG.WriteToLog(472, new TSM("StorageMirror", tRec.ERR, eMsgLevel.l2_Warning));
            }
            else
            {
                Task<TheDataRetrievalRecord> tTask = RetrieveEdgeStorageProperties(tRec);
                if (tTask != null)
                {
                    tTask.Wait();
                    tRec = tTask.Result;
                }
                tResponse.MyRecords = TheStorageUtilities.ConvertFromStoreRecord<T>(tRec, false, out string tError);
                if (!string.IsNullOrEmpty(tError))
                {
                    tResponse.HasErrors = true;
                    tResponse.ErrorMsg = tError;
                }
                else
                    BuildGroups(tResponse.MyRecords, tRec.GRP, tResponse);
            }
            TimedRequest tReq = GetTimedRequest(LastID);
            if (tReq != null)
            {
                tResponse.Cookie = tReq.Cookie;
                tReq.MyRequest(tResponse);
            }
        }

        /// <summary>
        /// This only works with RAM or Cached StorageMirrors for SQL Based use callback based method
        /// </summary>
        /// <param name="pMagicID"></param>
        /// <returns></returns>
        public T GetEntryByID(Guid pMagicID)
        {
            if (IsRAMStore || IsCached)
                return MyMirrorCache.GetEntryByID(pMagicID);
            return null;
        }
        /// <summary>
        /// This only works with RAM or Cached StorageMirrors for SQL Based use callback based method
        /// </summary>
        /// <param name="pMagicID"></param>
        /// <returns></returns>
        public T GetEntryByID(string pMagicID)
        {
            if (IsRAMStore || IsCached)
                return MyMirrorCache.GetEntryByID(pMagicID);
            return null;
        }

        /// <summary>
        /// Retrieves a single record by ID from the StorageMirror's underlying MirrorCache or IStorageService and hands it to the callback.
        /// If IsRAMStore or IsCached, the record will be retrieved from the MirrorCache.
        /// If IsRAMStore is false, the record will be retrieved from the IStorageService.
        /// </summary>
        /// <param name="pMagicID">The cdeMID (or record ID in the StorageMirror) of the record to retrieve.</param>
        /// <param name="CallBack">The callback method that will be invoked upon error or completed retrieval of the record.</param>
        /// <param name="LocalCallBackOnly">If set to true, no request is sent to a remote StorageService.</param>
        /// <param name="pCookie">A cookie that will later be returned with the StoreResponse in the callback.</param>
        public void GetEntryByID(Guid pMagicID, Action<StoreResponse> CallBack, bool LocalCallBackOnly,object pCookie)
        {
            StoreResponse tResponse = new ();
            if (!IsReady)
            {
                if (CallBack != null)
                {
                    tResponse.HasErrors = true;
                    tResponse.ErrorMsg = "StorageService not ready";
                    CallBack(tResponse);
                }
                return;
            }
            if (IsRAMStore || IsCached)
            {
                if (!MyMirrorCache.MyRecords.TryGetValue(pMagicID.ToString(), out T tRes))
                {
                    tResponse.HasErrors = true;
                    tResponse.Cookie = pCookie;
                    tResponse.ErrorMsg = string.Format("cdeMID({0}) Not Found", pMagicID);
                    CallBack(tResponse);
                }
                else
                {
                    tResponse.SQLFilter = "cdeMID='" + pMagicID + "'";
                    tResponse.Cookie = pCookie;
                    tResponse.MyRecords = new List<T> { tRes };
                    CallBack(tResponse);
                }
            }
            if (!IsRAMStore)
            {
                string Magix = "";
                if (CallBack != null)
                    Magix = AddTimedRequest(CallBack, pCookie,null);
                if (engineStorageServer != null)
                    engineStorageServer.RequestEdgeStorageData(MyStoreID, "", 1, 0, "cdeMID='" + pMagicID + "'", "", "", Magix, sinkJustReturn, LocalCallBackOnly);
            }
        }

        private void sinkJustReturn(TSM pMsg)
        {
            TheDataRetrievalRecord tRec = TheCommonUtils.DeserializeJSONStringToObject<TheDataRetrievalRecord>(pMsg.PLS);
            if (tRec == null) return;
            string LastID = tRec.MID;
            StoreResponse tResponse = new ()
            {
                SQLOrder = tRec.SOR,
                SQLFilter = tRec.SFI,
                ColumnFilter = tRec.CFI,
                PageNumber = tRec.PAG
            };
            if (tRec.ERR.Length > 0)
            {
                tResponse.HasErrors = true;
                tResponse.ErrorMsg = tRec.ERR;
                TheBaseAssets.MySYSLOG.WriteToLog(472, new TSM("StorageMirror", tRec.ERR, eMsgLevel.l2_Warning));
            }
            else
            {
                var tRec2 = tRec;
                Task<TheDataRetrievalRecord> tTask = RetrieveEdgeStorageProperties(tRec);
                if (tTask != null)
                {
                    tTask.Wait();
                    tRec = tTask.Result;
                }
                if (tRec == null)
                {
                    tResponse.HasErrors = true;
                    tResponse.ErrorMsg = $"EdgeStore Properties could not be retrieved for {tRec2?.UID}";
                }
                else
                {
                    tRec = RebuildThingStorePB(tRec);
                    if (tRec == null)
                    {
                        tResponse.HasErrors = true;
                        tResponse.ErrorMsg = "ThingStorePB could not be rebuilt";
                    }
                    else
                    {
                        tResponse.MyRecords = TheStorageUtilities.ConvertFromStoreRecord<T>(tRec, false, out string tError);
                        if (!string.IsNullOrEmpty(tError))
                        {
                            tResponse.HasErrors = true;
                            tResponse.ErrorMsg = tError;
                        }
                    }
                }
            }
            TimedRequest tReq = GetTimedRequest(LastID);
            if (tReq != null)
            {
                tResponse.Cookie = tReq.Cookie;
                tReq.MyRequest(tResponse);
            }
        }

        private TheDataRetrievalRecord RebuildThingStorePB(TheDataRetrievalRecord pRec)
        {
            if (typeof(T).Equals(typeof(TheThingStore)) && pRec!=null)
            {
                List<TFD> pbDefinitions = pRec.FLDs.Where(fld => !typeof(TheThingStore).GetProperties().Any(prop => prop.Name.Equals(fld.N)) && !TheStorageUtilities.defaultRows.Contains(fld.N)).ToList();
                Dictionary<string, object> PB = new ();
                foreach (TFD tDef in pbDefinitions)
                {
                    PB[tDef.N] = null;
                    pRec.FLDs.Remove(tDef);
                }
                foreach (List<string> record in pRec.RECs)
                {
                    foreach (TFD tfd in pbDefinitions)
                    {
                        PB[tfd.N] = TheStorageUtilities.GetValueByCol(record, tfd.C);
                    }
                    record.RemoveAll(val => pbDefinitions.Any(def => TheCommonUtils.CInt(val.Substring(0, 3)).Equals(def.C)));
                    int LastColumn = TheCommonUtils.CInt(record[record.Count - 1].Substring(0, 3));
                    record.Add(string.Format("{0:000}{1}", LastColumn + 1, TheCommonUtils.SerializeObjectToJSONString(PB)));
                }
                TFD pbTFD = new ()
                {
                    N = "PB",
                    T = TheCommonUtils.CStr(typeof(Dictionary<string, object>)),
                    C = pRec.FLDs[pRec.FLDs.Count - 1].C + 1
                };
                pRec.FLDs.Add(pbTFD);
            }
            return pRec;
        }
#endregion

#region NMI Table and Form Code

        internal string UpdateFromJSON(TheFormInfo pForm, string pJSON, string pDirtyMask, Guid pOriginator,TheClientInfo pClientInfo, Guid pSEID, Action<StoreResponse> pResponse)
        {
            if (TheCDEngines.MyNMIService == null || string.IsNullOrEmpty(pJSON) || pJSON=="[]" || pForm.defDataSource.StartsWith("TheUserDetails;:;"))
                return "";  //SECURITY-UPDATE: Forbit any updates to TheUserDetails table through generic calls by clients/browsers!
            string tRes = "";
            var tso = TheFormsGenerator.GetScreenOptions(pForm.cdeMID, pClientInfo, pForm);
            List<TheFieldInfo> tFldList = TheFormsGenerator.GetPermittedFields(pForm.cdeMID, pClientInfo,tso, false); 

            T tNewItem = TheCommonUtils.DeserializeJSONStringToObject<T>(pJSON);
            FireEvent(eStoreEvents.UpdateRequested, new StoreEventArgs(tNewItem, pClientInfo), false);
            if (tNewItem is TheMetaDataBase tMeta && (tMeta.cdeO != TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID))
                    return "";

            if (tFldList.Count > 0 && !string.IsNullOrEmpty(pDirtyMask) && !pDirtyMask.Equals("*"))
            {
                T tToUpdate = MyMirrorCache.GetEntryByID(tNewItem.cdeMID);
                TheThing tFormOwnerThing = tToUpdate as TheThing;
                bool FoundOne = false;
                StringBuilder AuditLog = new();
                if (tToUpdate == null)
                {
                    tToUpdate = tNewItem;
                    if (tToUpdate.cdeCTIM == DateTimeOffset.MinValue)
                        tToUpdate.cdeCTIM = DateTimeOffset.Now;
                    FoundOne = true;
                    if (TheBaseAssets.MyServiceHostInfo.AuditNMIChanges)
                        AuditLog.Append("New Instance created");
                }
                else
                {
                    Type MyType = typeof(T);
                    int tCnt = -1;
                    List<string> dataItems= new List<string>();
                    foreach (TheFieldInfo tInfo in tFldList)
                    {
                        if (!TheUserManager.HasUserAccess(pClientInfo.UserID, tInfo.cdeA))
                            continue;
                        tCnt++;
                        if (!string.IsNullOrEmpty(tInfo.DataItem) && pDirtyMask.Length > tCnt && pDirtyMask.Substring(tCnt, 1) == "1" && !TheCommonUtils.CBool(tInfo?.PropBagGetValue("WriteOnce")))
                        {
                            string[] tDataItemParts = tInfo.DataItem.Split('.');
                            if (tDataItemParts.Length > 2 && "MyPropertyBag".Equals(tDataItemParts[0]))
                            {
                                var thingToUpdate = tFormOwnerThing;
                                if (tInfo.cdeO != thingToUpdate.cdeMID && pForm.DefaultView==eDefaultView.Form)
                                {
                                    var tFieldThing = TheThingRegistry.GetThingByMID(tInfo.cdeO);
                                    if (tFieldThing != null)
                                        thingToUpdate = tFieldThing;
                                }
                                if (thingToUpdate != null)
                                {
                                    string tPropName = tDataItemParts[1];
                                    if (tDataItemParts.Length > 3)
                                    {
                                        for (int i = 2; i < tDataItemParts.Length - 1; i++)
                                            tPropName += $".{tDataItemParts[i]}";
                                    }
                                    if (dataItems.Contains(tPropName))
                                        continue;
                                    dataItems.Add(tPropName);
                                    if (tNewItem is TheThing tNewThing)
                                    {
                                        cdeP tSourceP = null;
                                        if (tPropName.StartsWith("["))
                                            tNewThing.MyPropertyBag.TryGetValue(tPropName, out tSourceP);
                                        else
                                            tSourceP = tNewThing.GetProperty(tPropName);
                                        cdeP tTargetP = thingToUpdate.GetProperty(tPropName);

                                        if (tSourceP != null && tTargetP != null)
                                        {
                                            var tOld = tTargetP.GetValue();
                                            var tNewVal = tSourceP.GetValue();
                                            if ((tInfo.Flags & 1) != 0)
                                            {
                                                //TODO: NMI will use RSA to encrypt value. We need the session here to decrypt:
                                                tNewVal = TheCommonUtils.CStr(tSourceP.Value);
                                                if (tNewVal.ToString().StartsWith("&^CDEP5^&:"))
                                                    tNewVal = TheCommonUtils.cdeRSADecrypt(pSEID, tNewVal.ToString());
                                            }
                                            bool tFoundOne = tTargetP.SetValue(tNewVal, pOriginator.ToString(), tSourceP.cdeCTIM);
                                            if (tFoundOne)
                                            {
                                                if (TheBaseAssets.MyServiceHostInfo.AuditNMIChanges)
                                                    AuditLog.Append($"{tPropName}:({tOld})-({tTargetP.GetValue()}) ");
                                                tTargetP.FireEvent(eThingEvents.PropertyChangedByUX, (tTargetP.cdeFOC & 256) == 0, -1);
                                            }
                                            if (tFoundOne && !FoundOne)
                                                FoundOne = true;
                                        }
                                        else
                                        {
                                            if (tTargetP == null && tSourceP != null)
                                            {
                                                PropertyInfo tP = typeof(cdeP).GetProperty(tDataItemParts[2]);
                                                var tVal = tP.GetValue(tSourceP, null);
                                                thingToUpdate.SetProperty(tPropName, tVal);
                                                if (TheBaseAssets.MyServiceHostInfo.AuditNMIChanges)
                                                    AuditLog.Append($"New {tPropName}:({tVal}) ");
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                FieldInfo tI = MyType.GetField(tInfo.DataItem);
                                if (tI != null)
                                {
                                    if (tI.GetValue(tNewItem) != tI.GetValue(tToUpdate))
                                    {
                                        if (TheBaseAssets.MyServiceHostInfo.AuditNMIChanges)
                                            AuditLog.Append($"{tI.Name}:({tI.GetValue(tNewItem)})-({tI.GetValue(tToUpdate)}) ");
                                        tI.SetValue(tToUpdate, tI.GetValue(tNewItem));
                                        FoundOne = true;
                                    }
                                    continue;
                                }
                                PropertyInfo tP = MyType.GetProperty(tInfo.DataItem);
                                if (tP != null && tP.GetValue(tNewItem, null) != tP.GetValue(tToUpdate, null) && tP.CanWrite)
                                {
                                    if (TheBaseAssets.MyServiceHostInfo.AuditNMIChanges)
                                        AuditLog.Append($"{tI.Name}:({tI.GetValue(tNewItem)})-({tI.GetValue(tToUpdate)}) ");
                                    tP.SetValue(tToUpdate, tP.GetValue(tNewItem, null), null);
                                    FoundOne = true;
                                }
                            }
                        }
                    }
                }
                if (FoundOne)
                {
                    UpdateItem(tToUpdate, pResponse);
                    if (tFormOwnerThing != null)
                    {
                        tFormOwnerThing.FireEvent(eEngineEvents.ThingUpdated, tFormOwnerThing, tToUpdate, true);
                        string tParent = TheThing.GetSafePropertyString(tFormOwnerThing, "Parent");
                        if (!string.IsNullOrEmpty(tParent))
                        {
                            TheThing PThing = TheThingRegistry.GetThingByID("*", tParent);
                            if (PThing != null)
                                PThing.FireEvent(eEngineEvents.ThingUpdated, PThing, tToUpdate, true);
                        }
                        if (!string.IsNullOrEmpty(tFormOwnerThing.EngineName))
                        {
                            IBaseEngine tBase = tFormOwnerThing.GetBaseEngine();
                            if (tBase != null)
                                tBase.FireEvent(eEngineEvents.ThingUpdated, tFormOwnerThing, true);
                        }
                    }
                    tRes = TheCommonUtils.SerializeObjectToJSONString(pForm.IsAlwaysEmpty ? new T() : tToUpdate);
                    if (TheBaseAssets.MyServiceHostInfo.AuditNMIChanges)
                        TheLoggerFactory.LogEvent(eLoggerCategory.NMIAudit, "NMI-Update", eMsgLevel.l4_Message, pClientInfo.UserID, $"Mods: {AuditLog}", tNewItem.cdeMID.ToString());
                }
            }
            else
            {
                if (tNewItem != null)
                {
                    UpdateItem(tNewItem, pResponse);
                    tRes = pJSON;
                }
            }
            return tRes;
        }

        internal string DeleteByID(TheFormInfo pForm, string pID, TheClientInfo pClientInfo, Action<StoreResponse> pResponse)
        {
            if (pClientInfo == null || !TheUserManager.HasUserAccess(pClientInfo.UserID, pForm.cdeA))
                return TheBaseAssets.MyLoc.GetLocalizedStringByKey(TheBaseAssets.MyServiceHostInfo.DefaultLCID, eEngineName.NMIService, "ERR: ###You don't have Access###");
            Guid tGuid = TheCommonUtils.CGuid(pID);
            if (tGuid == Guid.Empty)
                return TheBaseAssets.MyLoc.GetLocalizedStringByKey(pClientInfo.LCID, eEngineName.NMIService, "ERR: ###Invalid ID###");
            FireEvent(eStoreEvents.DeleteRequested,new StoreEventArgs(tGuid, pClientInfo), false);
            if (typeof(T) == typeof(TheThing))
                TheThingRegistry.DeleteThingByID(tGuid);
            else
                RemoveAnItemByID(tGuid, pResponse);
            return true.ToString();
        }

        internal string InsertFromJSON(TheFormInfo pForm, string pJSON, TheClientInfo pClientInfo, Action<StoreResponse> pResponse)
        {
            if (pClientInfo == null || !TheUserManager.HasUserAccess(pClientInfo.UserID, pForm.cdeA))
                return "ERR:No Access";
            T tNewItem = TheCommonUtils.DeserializeJSONStringToObject<T>(pJSON);
            FireEvent(eStoreEvents.InsertRequested, new StoreEventArgs(tNewItem, pClientInfo), false);
            if (tNewItem.cdeMID == Guid.Empty)
                return false.ToString();
            string tResult = false.ToString();
            if (tNewItem is TheThing tThing)
            {
                string[] tDefParts = TheCommonUtils.cdeSplit(pForm.defDataSource, ";:;", false, false);
                if (tDefParts.Length > 3)
                {
                    string[] tSets = TheCommonUtils.cdeSplit(tDefParts[3], ';', false, false);
                    foreach (string t1 in tSets)
                    {
                        string[] t = t1.Split('=');
                        if (t.Length > 1 && tThing.GetProperty(t[0]) == null)
                            TheThing.SetSafePropertyString(tThing, t[0], t[1]);
                    }
                }
                if (TheThingRegistry.RegisterThing(tThing) != null)
                {
                    tResult = true.ToString();
                }
            }
            else
            {
                if (tNewItem is TheMetaDataBase tMeta && (tMeta.cdeO != TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID))
                {
                    return "ERR:Not owned by this node"; //Dont fire event here!
                }

                AddAnItem(tNewItem, pResponse);
                tResult = true.ToString();
            }
            if (!TheCommonUtils.CBool(tResult))
                tNewItem = null;
            FireEvent(eStoreEvents.Inserted, new StoreEventArgs(tNewItem, pClientInfo), false);
            return tResult;
        }

        internal string ReturnAsString(string pName, string pItem)
        {
            StringBuilder tStr = new ();
            Type MyType = typeof(T);
            PropertyInfo[] tProps = MyType.GetProperties();
            bool IsFirst = true;
            foreach (T item in MyMirrorCache.TheValues)
            {
                try
                {
                    if (IsFirst)
                    {
                        if (!tProps.Any(s => s.Name == pName)) return "";
                        if (!string.IsNullOrEmpty(pItem) && !tProps.Any(s => s.Name == pItem)) return "";
                    }
                    tStr.Append(tProps.First(s => s.Name == pName).GetValue(item, null));
                    if (!string.IsNullOrEmpty(pItem))
                    {
                        tStr.Append(":");
                        tStr.Append(tProps.First(s => s.Name == pItem).GetValue(item, null));
                    }
                    if (!IsFirst)
                        tStr.Append(";");
                    IsFirst = false;
                }
                catch
                {
                    // ignored
                }
            }
            return tStr.ToString();
        }

        internal string SerializeToCSV(TheJSONLoaderDefinition pDataSource)
        {
            if (pDataSource == null) return "";
            if (pDataSource.IsEmptyRecord)
                return "";

            if ((!IsRAMStore && !IsCached) || AppendOnly) // Set to "if(!IsRAMStore)" to always load from SQL Storage
            {
                string tSQLOrder = pDataSource.SQLOrder;
                if (TheCommonUtils.CBool(tSQLOrder))
                    tSQLOrder = "cdeCTIM desc";
                string tFilter = pDataSource.SQLFilter;
                if (tFilter?.Contains("WildContent") == true)
                    tFilter = "";
                GetRecords(tFilter, pDataSource.TopRecords, pDataSource.PageNumber, tSQLOrder, sinkCSVResponse, false, false, pDataSource);
                return "WAITING";
            }
            else
            {
                bool IsThingBased = typeof(T) == typeof(TheThing);
                var ShowDescending = TheCommonUtils.CBool(pDataSource.SQLOrder);
                List<T> retList = null;
                if (MyMirrorCache.TracksInsertionOrder)
                {
                    retList = MyMirrorCache.GetItemsByInsertionOrderInternal(0).Where(item => item != null).ToList();
                    retList.Reverse();
                }
                else
                {
                    retList = ShowDescending ? MyMirrorCache.MyRecords.Values.OrderByDescending(s => s.cdeCTIM).ToList() : MyMirrorCache.MyRecords.Values.ToList();
                }
                if (!string.IsNullOrEmpty(pDataSource.SQLFilter))
                {
                    retList = FilterResults(pDataSource, retList);
                }
                if (IsThingBased)
                    retList = retList.Where(s => !TheThing.GetSafePropertyBool(s as TheThing, "IsHidden")).ToList();    //Fix for Bug#1189
                if (pDataSource.TopRecords > 0 && retList.Count > pDataSource.TopRecords)
                {
                    if (pDataSource.PageNumber > 0 && pDataSource.TopRecords * pDataSource.PageNumber > retList.Count)
                        pDataSource.PageNumber--;
                    if (pDataSource.PageNumber < 0)
                    {
                        pDataSource.PageNumber = TheCommonUtils.CInt(retList.Count / pDataSource.TopRecords);
                    }
                    retList = pDataSource.PageNumber > 0 ? retList.Skip(pDataSource.TopRecords * pDataSource.PageNumber).Take(pDataSource.TopRecords).ToList() : retList.Take(pDataSource.TopRecords).ToList();
                }
                return ConverToCSV(pDataSource, retList);
            }
        }

        private string ConverToCSV(TheJSONLoaderDefinition pDataSource, List<T> retList)
        {
            bool IsThingBased = typeof(T) == typeof(TheThing);
            StringBuilder tRetStr = new();
            foreach (var tFldInfo in pDataSource.FieldInfo)
            {
                if (NoExportFlds.Contains(tFldInfo.Type) || tFldInfo.DataItem == null)
                    continue;
                if (tRetStr.Length > 0)
                    tRetStr.Append($",");
                tRetStr.Append($"\"{TheBaseAssets.MyLoc.GetLocalizedStringByKey(pDataSource.ClientInfo.LCID, eEngineName.NMIService, tFldInfo.Header).Replace('\"','\'')}\"");
            }
            tRetStr.Append($"\n");
            if (pDataSource.FieldInfo != null && IsThingBased)
            {
                foreach (var tThing in retList)
                {
                    if (tThing == null)
                        continue;
                    int cnt = 0;
                    foreach (var tFldInfo in pDataSource.FieldInfo)
                    {
                        if (NoExportFlds.Contains(tFldInfo.Type) || tFldInfo.DataItem == null)
                            continue;
                        string[] tUpdateName = tFldInfo.DataItem.Split('.');
                        string OnUpdateName = tFldInfo.DataItem;
                        if (tUpdateName.Length > 2)
                        {
                            OnUpdateName = tUpdateName[1];
                            if (tUpdateName.Length > 3)
                            {
                                for (int i = 2; i < tUpdateName.Length - 1; i++)
                                    OnUpdateName += $".{tUpdateName[i]}";
                            }
                        }
                        cdeP tProp = (tThing as ICDEThing)?.GetBaseThing()?.GetProperty(OnUpdateName, true);
                        if (cnt > 0)
                            tRetStr.Append($",");
                        tRetStr.Append($"\"{TheCommonUtils.CStr(tProp).Replace('\"', '\'')}\"");
                        cnt++;
                    }
                    tRetStr.Append($"\n");
                }
                return tRetStr.ToString();
            }
            foreach (var tThing in retList)
            {
                if (tThing == null)
                    continue;
                int cnt = 0;
                foreach (var tFldInfo in pDataSource.FieldInfo)
                {
                    if (NoExportFlds.Contains(tFldInfo.Type) || tFldInfo.DataItem == null)
                        continue;
                    var pInfo = typeof(T).GetProperty(tFldInfo.DataItem);
                    if (pInfo == null)
                        continue;
                    var tVal = pInfo.GetValue(tThing);
                    if (cnt > 0)
                        tRetStr.Append($",");
                    tRetStr.Append($"\"{tVal}\"");
                    cnt++;
                }
                tRetStr.Append($"\n");
            }
            return tRetStr.ToString();
        }

        private void sinkCSVResponse(StoreResponse pResults)
        {
            if (!pResults.HasErrors && pResults.Cookie is TheJSONLoaderDefinition tJSON)
            {
                TheFormInfo tTable = TheNMIEngine.GetFormById(tJSON.TableName);
                List<T> retList = pResults.MyRecords;
                if (tJSON.SQLFilter.Contains("WildContent"))
                {
                    retList = FilterResults(tJSON, retList);
                }
                try
                {
                    TSM tTsm = new(eEngineName.ContentService, $"CDE_FILE:{tTable.FormTitle}.csv:text/csv")
                    {
                        PLS = ConverToCSV(tJSON, retList)
                    };
                    if (tJSON.ORG != Guid.Empty)
                        TheCommCore.PublishToNode(tJSON.ORG, tJSON.SID, tTsm);
                    else
                        TheBaseAssets.MySYSLOG.WriteToLog(474, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("StorageMirror", "Originator request not found", eMsgLevel.l2_Warning));
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(480, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("StorageMirror", "Error during CSV Conversion", eMsgLevel.l2_Warning, e.Message));
                }
            }
        }

        private readonly List<eFieldType> NoExportFlds = new() { eFieldType.AboutButton, eFieldType.BarChart, eFieldType.CanvasDraw, eFieldType.CollapsibleGroup, eFieldType.Dashboard, eFieldType.FacePlate, eFieldType.FormButton, eFieldType.FormView, eFieldType.Picture, eFieldType.PinButton, eFieldType.Screen, eFieldType.Shape, eFieldType.TileButton, eFieldType.TileGroup };

        internal string SerializeToJSON(TheJSONLoaderDefinition pDataSource)
        {
            if (pDataSource == null) return "[]";
            FireEvent(eStoreEvents.ReloadRequested,new StoreEventArgs(pDataSource, pDataSource?.ClientInfo), false);

            if (pDataSource.IsEmptyRecord)
                return TheCommonUtils.SerializeObjectToJSONString(new List<T> { default });

            string tSQLOrder = pDataSource.SQLOrder;
            if ((!IsRAMStore && !IsCached) || AppendOnly) // Set to "if(!IsRAMStore)" to always load from SQL Storage
            {
                if (TheCommonUtils.CBool(pDataSource.SQLOrder))
                    tSQLOrder = "cdeCTIM desc";
                string tFilter = pDataSource.SQLFilter;
                if (tFilter?.Contains("WildContent") == true)
                    tFilter = "";
                GetRecords(tFilter, pDataSource.TopRecords, pDataSource.PageNumber, tSQLOrder, sinkJSONResponse, false, false, pDataSource);
                if (IsRAMStore || AppendOnly)
                    return "ASYNC";
                else
                    return "WAITING";
            }
            else
            {
                bool IsThingBased = typeof(T) == typeof(TheThing);
                var ShowDescending = TheCommonUtils.CBool(pDataSource.SQLOrder);
                List<T> retList = null;
                if (MyMirrorCache.TracksInsertionOrder)
                {
                    retList = MyMirrorCache.GetItemsByInsertionOrderInternal(0).Where(item => item != null).ToList();
                    retList.Reverse();
                }
                else
                {
                    retList = ShowDescending ? MyMirrorCache.MyRecords.Values.OrderByDescending(s => s.cdeCTIM).ToList() : MyMirrorCache.MyRecords.Values.ToList();
                }
                if (!string.IsNullOrEmpty(pDataSource.SQLFilter))
                {
                    retList = FilterResults(pDataSource, retList);
                }
                if (IsThingBased)
                    retList = retList.Where(s => !TheThing.GetSafePropertyBool(s as TheThing, "IsHidden")).ToList();    //Fix for Bug#1189
                if (pDataSource.TopRecords > 0 && retList.Count > pDataSource.TopRecords)
                {
                    if (pDataSource.PageNumber > 0 && pDataSource.TopRecords * pDataSource.PageNumber > retList.Count)
                        pDataSource.PageNumber--;
                    if (pDataSource.PageNumber<0)
                    {
                        pDataSource.PageNumber = TheCommonUtils.CInt(retList.Count / pDataSource.TopRecords);
                    }
                    retList = pDataSource.PageNumber > 0 ? retList.Skip(pDataSource.TopRecords * pDataSource.PageNumber).Take(pDataSource.TopRecords).ToList() : retList.Take(pDataSource.TopRecords).ToList();
                }
                if (pDataSource.FieldInfo !=null && IsThingBased)
                {
                    bool HasLiveFields = false;
                    foreach (TheFieldInfo tFldInfo in pDataSource.FieldInfo)
                    {
                        if ((tFldInfo.Flags & 64) != 0)
                        {
                            HasLiveFields = true;
                            break;
                        }
                    }
                    if (HasLiveFields)
                    {
                        // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
                        foreach (var tThing in retList)
                        {
                            foreach (TheFieldInfo tFldInfo in pDataSource.FieldInfo)
                            {
                                if ((tFldInfo.Flags & 64) == 0) continue;
                                if (!string.IsNullOrEmpty(tFldInfo.DataItem))
                                {
                                    string[] tUpdateName = tFldInfo.DataItem.Split('.');
                                    string OnUpdateName = tFldInfo.DataItem;
                                    if (tUpdateName.Length > 2)
                                    {
                                        OnUpdateName = tUpdateName[1];
                                        if (tUpdateName.Length > 3)
                                        {
                                            for (int i = 2; i < tUpdateName.Length - 1; i++)
                                                OnUpdateName += "." + tUpdateName[i];
                                        }
                                    }
                                    cdeP tProp = (tThing as ICDEThing)?.GetBaseThing()?.GetProperty(OnUpdateName, true);
                                    if (tProp != null)
                                    {
                                        TheFormsGenerator.RegisterNMISubscription(pDataSource?.ClientInfo, tFldInfo.DataItem, tFldInfo);
                                        //tProp.SetPublication(true,pDataSource.ORG);     //Moved to RegisterNMI Subscription
                                        tProp.RegisterEvent(eThingEvents.PropertyChanged, tFldInfo.sinkUpdate);
                                    }
                                }
                            }
                        }
                    }
                }
                return TheCommonUtils.SerializeObjectToJSONString(retList);
            }
        }

        private void sinkJSONResponse(StoreResponse pResults)
        {
            if (!pResults.HasErrors && pResults.Cookie is TheJSONLoaderDefinition tJSON)
            {
                string tPaging = "";
                TheClientInfo pClientInfo = tJSON.ClientInfo;
                TheFormInfo tTable = TheNMIEngine.GetFormById(tJSON.TableName);
                var tso = TheFormsGenerator.GetScreenOptions(tTable.cdeMID, pClientInfo, tJSON.ForceReload ? tTable : null);
                List<T> retList = pResults.MyRecords;
                if (tJSON.SQLFilter.Contains("WildContent"))
                {
                    retList = FilterResults(tJSON, retList);
                }
                TSM tTsm = new(eEngineName.NMIService, $"NMI_SET_DATA:{tJSON.TableName.ToString().ToLower()}")
                {
                    PLS = TheCommonUtils.SerializeObjectToJSONString(retList)
                };

                if (tJSON.TopRecords > 0)
                {
                    tPaging = string.Format(":{0}:{1}", tJSON.TopRecords, tJSON.PageNumber);
                }
                if (tJSON.ForceReload)
                {
                    TheFormInfo tToSend = tTable.Clone(pClientInfo.WebPlatform);
                    TheNMIEngine.CheckAddButtonPermission(pClientInfo, tToSend);
                    if (tso != null && tso.TileWidth > 0)
                        tToSend.TileWidth = tso.TileWidth;
                    tToSend.FormFields = tJSON.FieldInfo;
                    tTsm.PLS += ":-MODELUPDATE-:" + TheCommonUtils.GenerateFinalStr(TheCommonUtils.SerializeObjectToJSONString(tToSend.GetLocalizedForm(pClientInfo.LCID)));
                }
                if (tTable.IsEventRegistered(eUXEvents.OnLoad))
                {
                    var tEV = TSM.Clone(tTsm, false);
                    tEV.ORG = tJSON.ORG.ToString();
                    tTable.FireEvent(eUXEvents.OnLoad, new TheProcessMessage() { ClientInfo = pClientInfo, CurrentUserID = pClientInfo.UserID, Message = tEV }, false);
                }
                tTsm.TXT += string.Format(":{0}:{1}{2}{3}", tJSON.ModelID, tJSON.TargetElement, tPaging, tJSON.ForceReload ? ":true" : "");

                if (tJSON.ORG != Guid.Empty)
                    TheCommCore.PublishToNode(tJSON.ORG, tJSON.SID, tTsm);
                else
                    TheBaseAssets.MySYSLOG.WriteToLog(474, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("StorageMirror", "Originator request not found", eMsgLevel.l2_Warning));
            }
        }

        private static List<T> FilterResults(TheJSONLoaderDefinition tJSON, List<T> retList)
        {
            bool IsThingBased = typeof(T) == typeof(TheThing);
            string[] tFilterParts = TheCommonUtils.cdeSplit(tJSON.SQLFilter, ';', false, false);
            for (int i = 0; i < tFilterParts.Length; i++)
            {
                string[] tSqlParts = TheCommonUtils.cdeSplit(tFilterParts[i], '=', false, false);
                if ((tSqlParts.Length > 1 && !string.IsNullOrEmpty(tSqlParts[0]) && !string.IsNullOrEmpty(tSqlParts[1])))
                {
                    if (IsThingBased)
                    {
                        switch (tSqlParts[0])
                        {
                            case "cdeMID":
                                retList = retList.Where(s =>
                                {
                                    return s is TheThing theThing && (s.cdeMID == TheCommonUtils.CGuid(tSqlParts[1]) && TheUserManager.HasUserAccess(tJSON.RequestingUserID, theThing.cdeA, true));
                                }).ToList();
                                break;
                            case "EngineName":
                                retList = retList.Where(s =>
                                {
                                    return s is TheThing theThing && (TheUserManager.HasUserAccess(tJSON.RequestingUserID, theThing.cdeA, true) && TheThing.GetSafePropertyString(theThing, "DeviceType") != eKnownDeviceTypes.IBaseEngine && TheThing.GetSafePropertyStringObject(s, tSqlParts[0]).StartsWith(tSqlParts[1]));
                                }).ToList();
                                break;
                            case "WildContent":
                                if (string.IsNullOrEmpty(tSqlParts[0]))
                                    continue;
                                retList = retList.Where(s =>
                                {
                                    var theThing = s as TheThing;
                                    var allProps = theThing.GetAllProperties();
                                    foreach (var pR in allProps)
                                    {
                                        if (tJSON?.FieldInfo?.Any(fi => fi.DataItem?.Contains(pR.Name) == true) == true && TheCommonUtils.CStr(pR.GetValue()).IndexOf(tSqlParts[1], StringComparison.OrdinalIgnoreCase) >= 0)
                                            return true;
                                    }
                                    return false;
                                }).ToList();
                                break;
                            default:
                                retList = retList.Where(s =>
                                {
                                    return s is TheThing theThing && (TheUserManager.HasUserAccess(tJSON.RequestingUserID, theThing.cdeA, true) && TheThing.GetSafePropertyStringObject(s, tSqlParts[0]).StartsWith(tSqlParts[1]));
                                }).ToList();
                                break;
                        }
                    }
                    else
                    {
                        switch (tSqlParts[0])
                        {
                            case "WildContent":
                                retList = retList.Where(s => TheStorageUtilities.DoesWildContentMatch(s, tSqlParts[1], tJSON.FieldInfo)).ToList();
                                break;
                            case "cdeMID":
                                retList = retList.Where(s => s.cdeMID == TheCommonUtils.CGuid(tSqlParts[1])).ToList();
                                break;
                            case "cdeN":
                                retList = retList.Where(s => s.cdeN == TheCommonUtils.CGuid(tSqlParts[1])).ToList();
                                break;
                        }
                    }
                }
            }
            return retList;
        }

#endregion

#region Chart Code
        private void sinkChartGroups(StoreResponse tRes)
        {
            if (tRes != null && !tRes.HasErrors)
            {
                if (tRes.Cookie is not TheChartCookie tChartCookie) return;

                tChartCookie.Groups = TheCommonUtils.CStringToList(tRes.ColumnFilter, ';');
                tChartCookie.ValueDefinitions ??= new List<TheChartValueDefinition>();
                string[] tVals = tChartCookie.ChartDefinition.ValueName.Split(',');
                foreach (string tN in tChartCookie.Groups)
                {
                    foreach (string t in tVals)
                    {
                        TheChartValueDefinition tV = new (Guid.NewGuid(), t) {GroupFilter = tN};
                        tChartCookie.ValueDefinitions.Add(tV);
                    }
                }
                PushChartData(tChartCookie.TargetNode, tChartCookie.ChartDefinition.cdeMID, tChartCookie);
            }
        }
        /// <summary>
        /// Pushes the ChartJSON data to the Chart Control
        /// </summary>
        /// <param name="pTargetNode">Node to push the Data To</param>
        /// <param name="pChartID">ID of the TheChartDefinition</param>
        public void PushChartData(Guid pTargetNode, Guid pChartID)
        {
            PushChartData(pTargetNode, pChartID, null);
        }
        internal void PushChartData(Guid pTargetNode, Guid pChartID, object pCookie)
        {
            TheChartDefinition tChartDef = TheNMIEngine.GetChartByID(pChartID);
            if (tChartDef != null)
            {
                if (pCookie is not TheChartCookie tCookie)
                    tCookie = new TheChartCookie() { ChartDefinition = tChartDef, TargetNode = pTargetNode, IsInitialData = true, VirtualBlock = -1 };

                if (tCookie.Groups == null)
                {
                    if (!string.IsNullOrEmpty(tChartDef.Grouping) && !TheCommonUtils.IsNullOrWhiteSpace(tChartDef.ValueName))
                    {
                        GetGroupResult(tChartDef.ColumFilter, tChartDef.Grouping, sinkChartGroups, false, tCookie);
                        return;
                    }
                    else
                    {
                        if (tCookie.ValueDefinitions==null && tChartDef.ValueDefinitions!=null)
                        {
                            tCookie.ValueDefinitions = new List<TheChartValueDefinition>();
                            foreach (var tVal in tChartDef.ValueDefinitions)
                                tCookie.ValueDefinitions.Add(tVal);
                        }
                    }
                }
                string[] t = TheCommonUtils.GenerateFinalStr(tChartDef.DataSource).Split(new[] { ";:;" }, StringSplitOptions.None);
                string SQLFilter = "";
                string SQLOrder = "";
                int PageNumber = 0;

                if (t.Length > 1)
                    PageNumber = TheCommonUtils.CInt(t[1]);
                if (t.Length > 2)
                    SQLFilter = t[2];
                else
                {
                    if (!TheCommonUtils.IsNullOrWhiteSpace(tChartDef.ColumFilter))
                        SQLFilter = tChartDef.ColumFilter;
                }
                if (t.Length > 3)
                    SQLOrder = t[3];
                else
                {
                    if (tChartDef.InAquireMode)
                        SQLOrder = "cdeCTIM desc";
                }

                string tColumns = "";
                if (tChartDef?.ValueDefinitions?.Count > 0)
                {
                    foreach (var tValDef in tChartDef.ValueDefinitions)
                    {
                        if (tColumns.Length > 0) tColumns += ",";
                        tColumns += tValDef.ValueName;
                    }
                }

                if (tCookie.VirtualBlock >= 0)
                {
                    GetRecords(SQLFilter, tChartDef.BlockSize, tCookie.VirtualBlock, SQLOrder,tColumns, sinkChartUpdates, false, true, tCookie);
                }
                else
                {
                    if (tCookie.IsInitialData)
                    {
                        GetRecords(SQLFilter, tChartDef.BlockSize, PageNumber, SQLOrder, tColumns, sinkChartUpdates, false, true, tCookie);
                    }
                    else
                    {
                        tCookie.Counter++;
                        if ((tCookie.Counter % tChartDef.BlockSize) == 0) tCookie.HighestBlock++;
                        GetRecords(SQLFilter, 1, 0, "cdeCTIM desc",tColumns, sinkUpdatesOne, false, true, tCookie);  
                    }
                }
                tCookie.VirtualBlock = -1;
            }
        }

        private void sinkUpdatesOne(StoreResponse tRes)
        {
            if (tRes == null || tRes.HasErrors || tRes.MyRecords == null || tRes.MyRecords.Count == 0)
                return;

            if (tRes.Cookie is not TheChartCookie tCookie) return;

            if (tCookie.ChartFactory == null)
            {
                if (!string.IsNullOrEmpty(tCookie?.ChartDefinition?.IChartFactoryType))
                {
                    try
                    {
                        Type tNType = Type.GetType(tCookie.ChartDefinition.IChartFactoryType);
                        if (tNType != null)
                            tCookie.ChartFactory = (ICDEChartsFactory)Activator.CreateInstance(tNType, null);
                    }
                    catch (Exception)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(472, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("StorageMirror", $"Charts Factory ({tCookie.ChartDefinition.IChartFactoryType}) could be created. Using Default", eMsgLevel.l2_Warning));
                    }
                }
                tCookie.ChartFactory ??= new TheChartFactory();
            }
            tCookie.ChartFactory.CreateChartInfo(tCookie.ChartDefinition, "x","linear");
            foreach (var tRec in tRes.MyRecords)
            {
                if (tCookie.ValueDefinitions != null)
                {
                    foreach (TheChartValueDefinition tVal in tCookie.ValueDefinitions)
                    {
                        tCookie.ChartFactory.AddPointToSeries(tVal.ValueName,null, TheCommonUtils.CDbl(TheCommonUtils.GetPropValue(tRec, tVal?.ValueName)));
                    }
                }
                else
                {
                    if (!TheCommonUtils.IsNullOrWhiteSpace(tCookie.ChartDefinition.ValueName))
                        tCookie.ChartFactory.AddPointToSeries(tCookie.ChartDefinition.ValueName,null, TheCommonUtils.CDbl(TheCommonUtils.GetPropValue(tRec, tCookie.ChartDefinition.ValueName)));
                }
            }

            SendChartData(tCookie);
        }

        private void SendChartData(TheChartCookie tCookie)
        {
            tCookie.ChartFactory.SendChartData(new TheDataBase { cdeMID = tCookie.ChartDefinition.cdeMID, cdeN = tCookie.TargetNode });
        }

        private void sinkChartUpdates(StoreResponse tRes)
        {
            TheChartCookie tCookie = tRes.Cookie as TheChartCookie;
            if (tCookie == null || tRes.HasErrors)
            {
                if (tCookie!=null && tCookie.TargetNode != Guid.Empty)
                    TheCommCore.PublishToNode(tCookie.TargetNode, new TSM(eEngineName.NMIService, "NMI_TOAST", tRes.ErrorMsg));
                if (tCookie==null || !(tRes.MyRecords?.Count>0))
                    return;
            }

            if (tCookie.ChartFactory == null)
            {
                if (!string.IsNullOrEmpty(tCookie?.ChartDefinition?.IChartFactoryType))
                {
                    try
                    {
                        Type tNType = Type.GetType(tCookie.ChartDefinition.IChartFactoryType);
                        if (tNType!=null)
                            tCookie.ChartFactory = (ICDEChartsFactory)Activator.CreateInstance(tNType, null);
                    }
                    catch (Exception)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(472, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("StorageMirror", $"Charts Factory ({tCookie.ChartDefinition.IChartFactoryType}) could be created. Using Default", eMsgLevel.l2_Warning));
                    }
                }
                tCookie.ChartFactory ??= new TheChartFactory();
            }
            tCookie.ChartFactory.CreateChartInfo(tCookie.ChartDefinition, "x", "datetime");

            if (tCookie.ValueDefinitions == null)
            {
                sinkUpdatesOne(tRes);
                return;
            }
            DateTimeOffset tLastStamp = DateTimeOffset.MinValue;
            TheChartData tChart = null;
            IEnumerable tEnum = tRes.MyRecords.OrderBy(s => s.cdeCTIM);
            foreach (T tt in tEnum)
            {
                if (tCookie.ChartDefinition.IntervalInMS == 0 || tt.cdeCTIM.Subtract(tLastStamp).TotalMilliseconds > tCookie.ChartDefinition.IntervalInMS)
                {
                    if (tChart != null)
                        UpdateChart(tCookie, tChart, (tChart.TimeStamp.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds).ToString(CultureInfo.InvariantCulture));
                    tLastStamp = tt.cdeCTIM;
                    tChart = new TheChartData(tCookie.ValueDefinitions.Count) {BlockNo = tRes.PageNumber};
                    if (tRes.PageNumber > tCookie.HighestBlock) tCookie.HighestBlock = tRes.PageNumber;
                }
                if (tChart != null)
                {
                    tChart.TimeStamp = tt.cdeCTIM;
                    int cnt = 0;
                    foreach (TheChartValueDefinition tValDef in tCookie.ValueDefinitions)
                    {
                        string tGroup = null;
                        if (!TheCommonUtils.IsNullOrWhiteSpace(tCookie.ChartDefinition.Grouping))
                        {
                            object tObj = TheCommonUtils.GetPropValue(tt, tCookie.ChartDefinition.Grouping);
                            if (tObj != null)
                                tGroup = TheCommonUtils.CStr(tObj).ToLower();
                        }
                        if (string.IsNullOrEmpty(tValDef.GroupFilter) || (tGroup != null && tGroup.Equals(tValDef.GroupFilter)))
                        {
                            tChart.Name[cnt] = (TheCommonUtils.IsNullOrWhiteSpace(tGroup) ? "" : (tGroup + ".")) + (string.IsNullOrEmpty(tValDef.Label) ? tValDef.ValueName : tValDef.Label);
                            object tObjX = TheCommonUtils.GetPropValue(tt, tValDef.ValueName);
                            if (tObjX != null)
                            {
                                tChart.MyValue[cnt] += (TheCommonUtils.CDbl(tObjX) * tValDef.GainFactor); 
                                tChart.ValCnt[cnt]++;
                                tChart.IsValidValue[cnt] = true;
                            }
                        }
                        cnt++;
                    }
                }
            }
            if (tChart != null)
                UpdateChart(tCookie, tChart, (tChart.TimeStamp.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds).ToString(CultureInfo.InvariantCulture));
            SendChartData(tCookie);
        }

        private static void UpdateChart(TheChartCookie tCookie, TheChartData tChart, string pX)
        {
            switch (tCookie.ChartDefinition.GroupMode)
            {
                case 1:     //Average
                    for (int i = 0; i < tCookie.ValueDefinitions.Count; i++)
                        tChart.MyValue[i] /= tChart.ValCnt[i];
                    break;
            }
            if (!tCookie.ChartFactory.AddChartData(tChart))
            {
                for (int i = 0; i < tChart.MyValue.Length; i++)
                    tCookie.ChartFactory.AddPointToSeries(tChart.Name[i], pX, tChart.MyValue[i]);
            }
        }
#endregion
    }

    /// <summary>
    /// Parameters passed to a StorageMirror upon creation to specify and enable certain behaviors.
    /// </summary>
    public class TheStorageMirrorParameters
    {
        public string FriendlyName;
        public string Description;
        public string CacheTableName;
        public bool ResetContent;
        public bool? IsRAMStore;
        public bool? CanBeFlushed;
        public string ImportFile;
        public bool? AllowFireUpdates;
        public bool? IsUpdateSubscriptionEnabled;
        public bool? IsCached;
        public bool? IsCachePersistent;
        public bool? UseSafeSave;
        public bool? FastSaveLock;
        public bool? IsCacheEncrypted;
        public TimeSpan? CacheStoreInterval;
        public int? SaveCount;
        public bool? BlockWriteIfIsolated;
        public TimeSpan? MaxAge;
        public Action ExpirationCallback;
        public int? MaxCount;
        public bool? TrackInsertionOrder;
        public bool? AppendOnly;
        public int? MaxCacheFileSize;
        public int? MaxCacheFileCount;

        public bool LoadSync;
        public bool DontRegisterInMirrorRepository; // CODE REVIEW: Is this used anywhere/anymore? CM: YES for Licenses! Though shalt not access the licenses via StorageMirro Repository
    }

    internal static class ExpressionBuilder
    {
        private static readonly MethodInfo containsMethod = typeof(string).GetMethod("Contains");
        private static readonly MethodInfo startsWithMethod =
        typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
        private static readonly MethodInfo endsWithMethod =
        typeof(string).GetMethod("EndsWith", new[] { typeof(string) });


        public static Expression<Func<T, bool>> GetExpression<T>(IList<SQLFilter> filters)
        {
            if (filters.Count == 0)
                return null;

            ParameterExpression param = Expression.Parameter(typeof(T), "t");
            Expression exp = null;

            if (filters.Count == 1)
                exp = GetExpression(param, filters[0]);
            else if (filters.Count == 2)
                exp = GetExpression(param, filters[0], filters[1]);
            else
            {
                while (filters.Count > 0)
                {
                    var f1 = filters[0];
                    var f2 = filters[1];

                    exp = exp == null ? GetExpression(param, filters[0], filters[1]) : Expression.AndAlso(exp, GetExpression(param, filters[0], filters[1]));

                    filters.Remove(f1);
                    filters.Remove(f2);

                    if (filters.Count == 1)
                    {
                        exp = Expression.AndAlso(exp, GetExpression(param, filters[0]));
                        filters.RemoveAt(0);
                    }
                }
            }

            // ReSharper disable once AssignNullToNotNullAttribute
            return Expression.Lambda<Func<T, bool>>(exp, param);
        }

        // ReSharper disable once UnusedTypeParameter
        private static Expression GetExpression(ParameterExpression param, SQLFilter filter)
        {
            MemberExpression member = Expression.Property(param, filter.PropertyName);
            ConstantExpression constant = Expression.Constant(filter.Value);

            return filter.Operation switch
            {
                FilterOp.Equals => Expression.Equal(member, constant),
                FilterOp.GreaterThan => Expression.GreaterThan(member, constant),
                FilterOp.GreaterThanOrEqual => Expression.GreaterThanOrEqual(member, constant),
                FilterOp.LessThan => Expression.LessThan(member, constant),
                FilterOp.LessThanOrEqual => Expression.LessThanOrEqual(member, constant),
                FilterOp.Contains => Expression.Call(member, containsMethod, constant),
                FilterOp.StartsWith => Expression.Call(member, startsWithMethod, constant),
                FilterOp.EndsWith => Expression.Call(member, endsWithMethod, constant),
                _ => null,
            };
        }

        private static BinaryExpression GetExpression
        (ParameterExpression param, SQLFilter filter1, SQLFilter filter2)
        {
            Expression bin1 = GetExpression(param, filter1);
            Expression bin2 = GetExpression(param, filter2);

            return Expression.AndAlso(bin1, bin2);
        }
    }


}
