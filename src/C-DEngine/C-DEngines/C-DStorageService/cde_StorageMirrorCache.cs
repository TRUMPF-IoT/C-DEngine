// SPDX-FileCopyrightText: Copyright (c) 2009-2025 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.StorageService.Model;
using nsCDEngine.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;


#pragma warning disable 1591

namespace nsCDEngine.Engines.StorageService
{
    public class TheMirrorCache<T> where T : TheDataBase, INotifyPropertyChanged, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TheMirrorCache{T}"/> class.
        /// </summary>
        public TheMirrorCache()
        {
            mCore = new TheMirrorCacheCore<T>();
            MyRecordsRWLock = new TheMirrorCacheReaderWriterLock<T>(this);
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="TheMirrorCache{T}"/> class.
        /// </summary>
        /// <param name="pExpirationTime">The p expiration time.</param>
        public TheMirrorCache(int pExpirationTime)
        {
            mCore = new TheMirrorCacheCore<T>(pExpirationTime);
            MyRecordsRWLock = new TheMirrorCacheReaderWriterLock<T>(this);
        }

        readonly TheMirrorCacheCore<T> mCore;

        #region DB Cache Init
        private bool mPreferDBCache = false;
        /// <summary>
        /// New in 6.116: If true, the cache will be stored in a DB rather than in a JSON File.
        /// It can only be turned on and not off. Once its on, it will always use the a DB cache store.
        /// </summary>
        internal bool PreferDBCache
        {
            get { return mPreferDBCache; }
            set
            {
                if (!mPreferDBCache && value && TheCDEngines.MyICacheStore != null)
                {
                    mPreferDBCache = true;
                }
            }
        }
        #endregion
        public int Count
        {
            get
            {
                if (PreferDBCache)
                    return (int)TheCDEngines.MyICacheStore.CountRecordsByFilter<T>(mCore.MyStoreID, null).GetAwaiter().GetResult();
                return mCore.Count;
            }
        }

        internal bool IsEmpty
        {
            get
            {
                if (PreferDBCache)
                    return TheCDEngines.MyICacheStore.CountRecordsByFilter<T>(mCore.MyStoreID, null).GetAwaiter().GetResult() == 0;
                return mCore.IsEmpty;
            }
        }

        public void RemoveStore(bool BackupFirst)
        {
            if (PreferDBCache)
            {
                // If we are using a DB cache, we cannot remove the store in the same way as with a file-based cache.
                // Instead, we should clear the records from the DB.
                TheCDEngines.MyICacheStore.RemoveStore<T>(mCore.MyStoreID, BackupFirst).GetAwaiter().GetResult();
                return;
            }
            mCore.RemoveStore(BackupFirst);
        }

        public bool LoadCacheFromDisk(bool LoadSync)
        {
            if (PreferDBCache)
            {
                var bLoaded=UpdateCacheRecords();
                eventCachedReady?.Invoke(new TSM("TheMirrorCache", MyStoreID, bLoaded ? eMsgLevel.l4_Message : eMsgLevel.l1_Error, bLoaded ? MyStoreID : "ERROR: Failed to load store"));
                return bLoaded;
            }
            return mCore.LoadCacheFromDisk(LoadSync);
        }

        internal cdeConcurrentDictionary<string, T> MyRecords
        {
            get
            {
                if (PreferDBCache && !(mCore.MyRecords?.Count>0))
                    UpdateCacheRecords();
                return mCore.MyRecords;
            }
        }
        public List<T> TheValues
        {
            get
            {
                if (PreferDBCache && !(mCore.MyRecords?.Count > 0))
                    UpdateCacheRecords();
                return mCore.TheValues;
            }
        }

        private bool UpdateCacheRecords()
        {
            var tRes = TheCDEngines.MyICacheStore.GetRecordsByFilter<T>(mCore.MyStoreID, null,null,0,0).GetAwaiter().GetResult();
            cdeConcurrentDictionary<string, T> myDictionary = new cdeConcurrentDictionary<string, T>();
            bool bLoaded = false;
            if (tRes?.Count > 0)
            {
                foreach (var item in tRes)
                {
                    myDictionary[$"{item.cdeMID}"] = item; // May overwrite existing value
                }
                bLoaded = true;
            }
            mCore.MyRecords = myDictionary; // Update the core's cache record dictionary
            return bLoaded;
        }

        public void UpdateItems(List<T> pItems, Action<List<T>> CallBack)
        {
            if (pItems == null || pItems.Count == 0) return;
            if (PreferDBCache)
            {
                // If we are using a DB cache, we should update the records in the DB.
                TheCDEngines.MyICacheStore.AddOrUpdateRecords<T>(mCore.MyStoreID, pItems).GetAwaiter().GetResult();
            }
            mCore.UpdateItems(pItems, CallBack);
        }

        public void RemoveAnItem(T pSelector)
        {
            if (PreferDBCache)
                TheCDEngines.MyICacheStore.RemoveRecord<T>(mCore.MyStoreID, pSelector).GetAwaiter().GetResult();
            mCore.RemoveAnItem(pSelector);
        }
        public void RemoveAnItemByID(Guid pKey, Action<T> CallBack)
        {
            if (PreferDBCache)
                TheCDEngines.MyICacheStore.RemoveRecordByID<T>(mCore.MyStoreID, pKey).GetAwaiter().GetResult();
            mCore.RemoveAnItemByKey(pKey.ToString(), CallBack);
        }
        public void RemoveItems(List<T> pDetails, Action<List<T>> CallBack)
        {
            if (PreferDBCache)
                TheCDEngines.MyICacheStore.RemoveRecordsByID<T>(mCore.MyStoreID, pDetails.Select(x => x.cdeMID).ToList());
            mCore.RemoveItems(pDetails, CallBack);
        }
        public void AddOrUpdateItem(T pDetails)
        {
            if (PreferDBCache)
                TheCDEngines.MyICacheStore.AddOrUpdateRecord<T>(mCore.MyStoreID, pDetails).GetAwaiter().GetResult();
            mCore.AddOrUpdateItem(pDetails);
        }
        public void AddItems(List<T> pDetails, Action<List<T>> CallBack)
        {
            if (PreferDBCache)
                TheCDEngines.MyICacheStore.AddOrUpdateRecords<T>(mCore.MyStoreID, pDetails).GetAwaiter().GetResult();
            mCore.AddItems(pDetails, CallBack);
        }

        public void AddOrUpdateItem(Guid pKey, T pDetails, Action<T> CallBack)
        {
            if (PreferDBCache)
                TheCDEngines.MyICacheStore.AddOrUpdateRecord<T>(mCore.MyStoreID, pDetails).GetAwaiter().GetResult();
            mCore.AddOrUpdateItemKey(pKey.ToString(), pDetails, CallBack);
        }
        internal bool AppendRecordsToDisk(Dictionary<string, T> items, bool saveSync, bool waitForSave)
        {
            if (PreferDBCache)
            {
                TheCDEngines.MyICacheStore.AddOrUpdateRecords<T>(mCore.MyStoreID, items.Values.ToList()).GetAwaiter().GetResult();
                return true;
            }
            return mCore.AppendRecordsToDisk(items, saveSync, waitForSave);
        }
        public bool FlushCache(bool BackupFirst, bool ForceFlush = false)
        {
            if (PreferDBCache)
            {
                RemoveStore(BackupFirst);
                return true;
            }
            return mCore.FlushCache(BackupFirst, ForceFlush);
        }

        internal TheStorageMirror<T>.StoreResponse GetRecordsFromDisk(string filter, string orderBy, int pageNumber, int topRecords)
        {
            if (PreferDBCache)
            {
                Dictionary<string, bool> orderList = null; 
                if (!string.IsNullOrEmpty(orderBy))
                {
                    orderList=new Dictionary<string, bool>();
                    var orderParts = orderBy.Split(' ');
                    if (orderParts.Length>1 && orderParts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase))
                    {
                        orderList[orderParts[0]] = false; // DESC
                    }
                    else
                    {
                        orderList[orderParts[0]] = true; // ASC
                    }
                }
                List<SQLFilter> filterList = null;
                if (!string.IsNullOrEmpty(filter))
                {
                    filterList = TheStorageUtilities.CreateFilter(filter);
                }
                var records = TheCDEngines.MyICacheStore.GetRecordsByFilter<T>(mCore.MyStoreID, filterList, orderList, pageNumber, topRecords).GetAwaiter().GetResult();
                return new TheStorageMirror<T>.StoreResponse
                {
                    MyRecords = records,
                     PageNumber = pageNumber,
                      SQLFilter = filter,
                      SQLOrder = orderBy
                };
            }
            return mCore.GetRecordsFromDisk(filter, orderBy, pageNumber, topRecords);
        }

        internal void RemoveExpired(object notused)
        {
            if (PreferDBCache)
            {
                // If we are using a DB cache, we should remove expired records from the DB.
                if (MyRecords.Any())
                {
                    var tNow = DateTimeOffset.Now;//Otherwise it will be called for every record in Linq again
                    try
                    {
                        var tList = MyRecords.Where(p => (p.Value.cdeEXP > 0 && tNow.Subtract(p.Value.cdeCTIM).TotalSeconds > p.Value.cdeEXP)).ToList();
                        if (tList.Count == 0) return;
                        TheBaseAssets.MySYSLOG.WriteToLog(4805, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Storage {tList.Count} Expired Record Removed: {mCore.MyRecords.Count} of {typeof(T)}", eMsgLevel.l6_Debug));
                        if (eventRecordExpired != null)
                        {
                            foreach (var s in tList)
                            {
                                eventRecordExpired?.Invoke(s.Value);
                            }
                        }
                        TheCDEngines.MyICacheStore.RemoveRecordsByID<T>(mCore.MyStoreID, tList.Select(x => x.Value.cdeMID).ToList());
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            mCore.RemoveExpired(notused);
        }
        internal void RemoveRetired()
        {
            if (mCore.MaxStoreSize == 0 || !MyRecords.Any()) return; //LOCK-REVIEW: We do not want to do this if there is a lock already here (logic rather then read/write) MUST NOT CHECK FOR:  || MyRecordsRWLock.IsLocked() <= thi is NEVER false!
            if (PreferDBCache)
            {
                if (mCore.Count > mCore.MaxStoreSize)
                {
                    int toDel = Count - mCore.MaxStoreSize;
                    MyRecordsRWLock.RunUnderUpgradeableReadLock(() =>  //LOCK-REVIEW: Used for consistency not read/write. write-Lock in RemoveItems. but created here as Crash was seen in linq on slow IPL Win7.NET4 devices
                    {
                        // CODE REVIEW: This is inefficient for large caches, especially since it gets called with each newly added item once the threshold is reached
                        var tList = MyRecords.Values.OrderBy(s => s.cdeCTIM).Take(toDel).ToList();
                        if (eventRecordExpired != null)
                        {
                            foreach (var s in tList)
                                eventRecordExpired.Invoke(s);
                        }
                        RemoveItems(tList, null);
                    });
                }
            }
            mCore.RemoveRetired();
        }
        internal void RemoveRetired(int pExpiredSeconds)
        {
            if (PreferDBCache && MyRecords.Any())
            {
                try
                {
                    DateTimeOffset tNow = DateTimeOffset.Now; //Otherwise it will be called for every record in Linq again
                    var tList = MyRecords.Where(p => p.Value.cdeCTIM < tNow.Subtract(new TimeSpan(0, 0, pExpiredSeconds))).ToList();
                    if (tList.Count == 0) return;
                    TheBaseAssets.MySYSLOG.WriteToLog(4806, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Storage {tList.Count} Retired Record Removed: {mCore.MyRecords.Count} of {typeof(T)}", eMsgLevel.l6_Debug));
                    if (eventRecordExpired != null)
                    {
                        foreach (var s in tList)
                        {
                            eventRecordExpired.Invoke(s.Value);
                        }
                    }
                    TheCDEngines.MyICacheStore.RemoveRecordsByID<T>(mCore.MyStoreID, tList.Select(x => x.Value.cdeMID).ToList());
                }
                catch
                {
                    // ignored
                }
            }
            mCore.RemoveRetired(pExpiredSeconds);
        }

        #region ReadThrough Methods
        public List<string> TheKeys
        {
            get
            {
                return mCore.TheKeys;
            }
        }

        public bool ContainsID(string pStrKey)
        {
            return mCore.ContainsID(pStrKey);
        }
        public bool ContainsID(Guid pKey)
        {
            return mCore.ContainsID(pKey);
        }
        public bool ContainsByFunc(Func<T, bool> pSelector)
        {
            return mCore.ContainsByFunc(pSelector);
        }


        public T GetEntryByID(Guid ID)
        {
            return mCore.GetEntryByID(ID);
        }
        public T GetEntryByID(string strID)
        {
            return mCore.GetEntryByID(strID);
        }
        public T GetEntryByFunc(Func<T, bool> pSelector)
        {
            return mCore.GetEntryByFunc(pSelector);
        }
        public List<T> GetEntriesByFunc(Func<T, bool> pSelector)
        {
            return mCore.GetEntriesByFunc(pSelector);
        }
        public void ForceSave()
        {
            if (!PreferDBCache)
                mCore.ForceSave();
        }

        public bool SaveCacheToDisk(bool SaveSync, bool WaitForSave)
        {
            if (PreferDBCache)
                return true;
            return mCore.SaveCacheToDisk(SaveSync, WaitForSave);
        }
        #endregion

        #region Simplifications

        private Timer mMyExpireTimer = null;
        internal void SetExpireTime(int pExpirationTime)
        {
            if (pExpirationTime > 0)
            {
                mCore.MyExpirationTest = pExpirationTime;
                if (mMyExpireTimer == null)
                {
                    var newTimer = new Timer(RemoveExpired, null, 0, pExpirationTime <= 24 * 60 * 60 ? pExpirationTime * 1000 : 24 * 60 * 60 * 1000);
                    var previousTimer = Interlocked.CompareExchange(ref mMyExpireTimer, newTimer, null);
                    if (previousTimer != null)
                    {
                        newTimer.Dispose();
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
            var previousTimer = Interlocked.Exchange(ref mMyExpireTimer, null);
            mMyExpireTimer = null;
            try
            {
                previousTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch
            {
                //ignored
            }
            try
            {
                previousTimer?.Dispose();
            }
            catch
            {
                //ignored
            }
        }
        public bool FlushCache(bool BackupFirst = false)
        {
            return FlushCache(BackupFirst,false);
        }



        public void UpdateItem(T pDetails, Action<List<T>> CallBack)
        {
            if (pDetails == null) return;
            List<T> tList = new() { pDetails };
            UpdateItems(tList, CallBack);
        }

        public void RemoveAnItem(T pDetails, Action<List<T>> CallBack)
        {
            List<T> tList = new()
            {
                pDetails
            };
            RemoveItems(tList, CallBack);
        }

        public void AddAnItem(T pDetails, Action<List<T>> CallBack)
        {
            List<T> tList = new()
            {
                pDetails
            };
            AddItems(tList, CallBack);
        }
        public void Clear(bool ZipBefore)
        {
            FlushCache(ZipBefore);
        }
        #endregion

        #region Store Settings in MirrorCache
        public Action<TSM> eventCachedReady
        {
            get { return mCore.eventCachedReady; }
            set { mCore.eventCachedReady = value; }
        }
        internal Action<T> eventRecordExpired
        {
            get { return mCore.eventRecordExpired; }
            set { mCore.eventRecordExpired = value; }
        }

        internal string MyStoreID { get => mCore.MyStoreID; set => mCore.MyStoreID = value; }

        public bool FastSaveLock { get { return mCore.FastSaveLock; } set { mCore.FastSaveLock = value; } }
        public bool AppendOnly { get { return mCore.AppendOnly; } set { mCore.AppendOnly = value; } }
        public bool UseSafeSave { get { return mCore.UseSafeSave; } set { mCore.UseSafeSave = value; } }
        public bool IsCachePersistent { get { return mCore.IsCachePersistent; } set { mCore.IsCachePersistent = value; } }
        public bool AllowFireUpdates { get { return mCore.AllowFireUpdates; } set { mCore.AllowFireUpdates = value; } }
        public bool CanBeFlushed { get { return mCore.CanBeFlushed; } set { mCore.CanBeFlushed = value; } }
        public bool IsCacheEncrypted { get { return mCore.IsCacheEncrypted; } set { mCore.IsCacheEncrypted = value; } }
        public int CacheStoreInterval { get { return mCore.CacheStoreInterval; } set { mCore.CacheStoreInterval = value; } }
        public bool IsStoreIntervalInSeconds { get { return mCore.IsStoreIntervalInSeconds; } set { mCore.IsStoreIntervalInSeconds = value; } }
        public int MaxCacheFileCount { get { return mCore.MaxCacheFileCount; } set { mCore.MaxCacheFileCount = value; } }
        public int MaxCacheFileSize { get { return mCore.MaxCacheFileSize; } set { mCore.MaxCacheFileSize = value; } }
        public bool BlockWriteIfIsolated { get { return mCore.BlockWriteIfIsolated; } set { mCore.BlockWriteIfIsolated = value; } }
        internal IReaderWriterLock MyRecordsRWLock;
        internal DateTimeOffset LastCacheStoreTime
        {
            get { return mCore.LastCacheStoreTime; }
        }
        internal string GetCacheStatistics()
        {
            return $"{mCore.CacheCounter}/{CacheStoreInterval}/{mCore.TotalCacheCounter}";
        }
        public void SetMaxStoreSize(int pRecords)
        {
            mCore.SetMaxStoreSize(pRecords);
        }
        #endregion

        #region DB Cache unsupported calls
        #region Historian Functions
        public void Reset()
        {
            mCore.Reset();
        }
        public bool TrackInsertionOrder()
        {
            return mCore.TrackInsertionOrder();
        }
        public bool TracksInsertionOrder { get { return mCore.TracksInsertionOrder; } }

        internal long GetPreviousSequenceNumber(long sequenceNumber)
        {
            return mCore.GetPreviousSequenceNumber(sequenceNumber);
        }
        internal IEnumerable<T> GetItemsByInsertionOrderInternal(long previousSequenceNumber)
        {
            return GetItemsByInsertionOrderInternal(previousSequenceNumber, out _);
        }
        internal IEnumerable<T> GetItemsByInsertionOrderInternal(long previousSequenceNumber, out long firstSequenceNumberReturned)
        {
            return mCore.GetItemsByInsertionOrderInternal(previousSequenceNumber, out firstSequenceNumberReturned);
        }
        internal void RemoveUpToSequenceNumberInternal(long LastSequenceNumberToRemove)
        {
            mCore.RemoveUpToSequenceNumberInternal(LastSequenceNumberToRemove);
        }
        internal long GetLastSequenceNumber()
        {
            return mCore.GetLastSequenceNumber();
        }
        internal long GetOffsetByTimestamp(DateTimeOffset startTime)
        {
            return mCore.GetOffsetByTimestamp(startTime);
        }
        internal long GetNextSequenceNumber(long sequenceNumber)
        {
            return mCore.GetNextSequenceNumber(sequenceNumber);
        }
        internal T FindLastItemAtOrBeforeSequenceNumber(long sequenceNumber, Func<T, bool> predicate, out long foundItemSequenceNumber)
        {
            return mCore.FindLastItemAtOrBeforeSequenceNumber(sequenceNumber, predicate, out foundItemSequenceNumber);
        }
        #endregion
        #endregion

    }

    internal class TheMirrorCacheReaderWriterLock<T> : IReaderWriterLock where T : TheDataBase, INotifyPropertyChanged, new()
    {
        readonly ReaderWriterLockSlim _rwLock;
        readonly TheMirrorCache<T> _cache;
        TimeSpan _timeout;
        bool _hasUsedUpgradebleMode;
        readonly bool DoLog = false;
        public TheMirrorCacheReaderWriterLock(TheMirrorCache<T> cache)
        {
            _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            _cache = cache;
            _timeout = new(0, 0, 30);
            DoLog = TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("EnableReaderWriterLockLog"));
            Log("Created RWLock");
        }

        void Log(string msg)
        {
            if (DoLog && _cache.MyStoreID != "EventLog")
            {
                TheBaseAssets.MySYSLOG?.WriteToLog(4820, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheMirrorCache", msg, eMsgLevel.l6_Debug, GetLockNameAndThread()));
            }
        }

        void LogTimeout(string msg, int count)
        {
            if (typeof(T) != typeof(TheEventLogEntry))
                TheBaseAssets.MySYSLOG?.WriteToLog(4819, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheMirrorCache", $"Blocked on lock for {_timeout.TotalMilliseconds * count} ms: {msg}", eMsgLevel.l6_Debug, GetLockNameAndThread()));
        }

        string GetLockNameAndThread()
        {
            return $"{_cache.MyStoreID} {Thread.CurrentThread.ManagedThreadId}";
        }

        public void RunUnderReadLock(Action action)
        {
            Log("Entering ReadLock");
            int count = 1;
            while (!_rwLock.TryEnterReadLock(_timeout))
            {
                LogTimeout("ReadLock", count++);
            }
            Log("Entered ReadLock");
            try
            {
                action();
            }
            finally
            {
                _rwLock.ExitReadLock();
                Log("Exited ReadLock");
            }
        }
        public void RunUnderWriteLock(Action action)
        {
            // This prevents writers from blocking other readers while another thread holds an upgradeable lock: avoids deadlock if the upgradeable writer assumes that other readers can make progress (i.e. SenderBase while merging senderthings, and canceling a sender loop)
            if (!_hasUsedUpgradebleMode)
            {
                Log("Entering WriteLock");
                int count = 1;
                while (!_rwLock.TryEnterWriteLock(_timeout))
                {
                    LogTimeout("WriteLock", count++);
                }

                Log("Entered WriteLock");
                try
                {
                    action();
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                    Log("Exited WriteLock");
                }
            }
            else
            {
                RunUnderUpgradeableReadLock(() => { return true; }, (o) => action());
            }
        }

        public void RunUnderUpgradeableReadLock(Action action)
        {
            RunUnderUpgradeableReadLock(() => { action(); return null; }, null);
        }

        public void RunUnderUpgradeableReadLock(Func<object> action, Action<object> writeAction)
        {
            _hasUsedUpgradebleMode = true;
            Log("Entering UpgradeableReadLock");
            int count = 1;
            while (!_rwLock.TryEnterUpgradeableReadLock(_timeout))
            {
                LogTimeout("UpgradeableReadLock", count++);
            }

            Log("Entered UpgradeableReadLock");
            try
            {
                var result = action();
                if (result != null)
                {
                    Log("Entering write lock under UpgradeableReadLock");
                    int count2 = 1;
                    while (!_rwLock.TryEnterWriteLock(_timeout))
                    {
                        LogTimeout("WriteLockUnderUpgradeableReadLock", count2++);
                    }
                    Log("Entered write lock under UpgradeableReadLock");
                    try
                    {
                        writeAction(result);
                    }
                    finally
                    {
                        _rwLock.ExitWriteLock();
                        Log("Exited write lock under UpgradeableReadLock");
                    }
                }
            }
            finally
            {
                _rwLock.ExitUpgradeableReadLock();
                Log("Exited UpgradeableReadLock");
            }
        }


        public bool IsLocked()
        {
            return IsReadLocked()
                || IsWriteLocked();
        }

        public bool IsReadLocked()
        {
            if (_rwLock.IsReadLockHeld)
            {
                return false;
            }
            if (_rwLock.CurrentReadCount == 0)
            {
                return false;
            }
            return true;
        }

        public bool IsUpgradeableReadLocked()
        {
            if (_rwLock.IsUpgradeableReadLockHeld)
            {
                return false;
            }
#pragma warning disable S2222 // Locks should be released on all paths
            if (_rwLock.TryEnterUpgradeableReadLock(0)) //NOSONAR There is no code here that could throw, so no need to add a try/finally around the ExitUpgradeableReadLock call
            {
                _rwLock.ExitUpgradeableReadLock();
                return false;
            }
#pragma warning restore S2222 // Locks should be released on all paths
            return true;
        }
        public bool IsWriteLocked()
        {
            if (_rwLock.IsWriteLockHeld)
            {
                return false; // This thread is holding the write lock
            }
#pragma warning disable S2222 // Locks should be released on all paths
            if (_rwLock.TryEnterWriteLock(0))  //NOSONAR There is no code here that could throw, so no need to add a try/finally around the ExitWriteLock call
            {
                _rwLock.ExitWriteLock();
                return false;
            }
#pragma warning restore S2222 // Locks should be released on all paths
            return true;
        }
    }

}
