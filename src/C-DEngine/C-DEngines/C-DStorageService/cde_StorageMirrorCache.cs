// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.Communication;
using nsCDEngine.Security;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using nsCDEngine.Engines.StorageService.Model;

#if CDE_INTNEWTON
using cdeNewtonsoft.Json;
#else
using Newtonsoft.Json;
#endif

#pragma warning disable 1591

namespace nsCDEngine.Engines.StorageService
{
    public class StoreEventArgs
    {
        public bool Handled;
        public object Para;
        public TheClientInfo ClientInfo;

        public StoreEventArgs(object pPara)
        {
            Para = pPara;
        }

        internal StoreEventArgs(object pPara, TheClientInfo pClient)
        {
            Para = pPara;
            if (pClient == null)
                return;
            ClientInfo = pClient;
        }
    }

    /// <summary>
    /// Class TheMirrorCache.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TheMirrorCache<T> : IDisposable where T : TheDataBase, INotifyPropertyChanged, new()
    {
        /// <summary>
        /// The event once cache is ready
        /// </summary>
        public Action<TSM> eventCachedReady;
        /// <summary>
        /// Event if the Store has updates
        /// </summary>
        public Action<List<T>> HasUpdates;

        /// <summary>
        /// My records
        /// </summary>
        internal cdeConcurrentDictionary<string, T> MyRecords = new ();
        private List<T> recordsBySequenceNumber;
        private long sequenceNumberOffset = 1;

        // For AppendOnly and TrackInsertionOrder StorageMirrors
        // Keeps track of the starting offsets for each file for "deletion from start"
        private readonly cdeConcurrentDictionary<string, long> fileOffsets = new ();

        #region MyRecordsHelpers
        // Helper functions to manipulate MyRecords while keeping the recordsBySequenceNumber in sync
        // These private functions assume that the MyRecordsLock has been taken by the caller
        private bool TryAddInternal(string pStrKey, T item)
        {
            bool addToSequence = AppendOnly || MyRecords.TryAdd(pStrKey, item);
            if (addToSequence)
            {
                if (recordsBySequenceNumber != null)
                {
                    if (item is TheMetaDataBase itemMDB && itemMDB.cdeSEQ == null)
                    {
                        itemMDB.cdeSEQ = sequenceNumberOffset + recordsBySequenceNumber.Count;
                    }
                    recordsBySequenceNumber.Add(item);
                }
                return true;
            }
            return false;
        }

        private bool TryRemoveItemInternal(string pStrKey, out T o)
        {
            if (MyRecords.TryRemove(pStrKey, out o))
            {
                // TODO: If we store the sequence number with the item, we can make this an O(1) operation instead of O(n)
                if (recordsBySequenceNumber != null)
                {
                    T o1 = o;
                    int index = recordsBySequenceNumber.FindIndex((i) => i == o1);
                    if (index != 0)
                    {
                        recordsBySequenceNumber[index] = null;
                    }
                    else
                    {
                        recordsBySequenceNumber.RemoveAt(0);
                        sequenceNumberOffset++;
                    }
                    int count = 0;
                    while (recordsBySequenceNumber.Count > count && recordsBySequenceNumber[count] == null)
                    {
                        count++;
                    }
                    if (count > 0)
                    {
                        recordsBySequenceNumber.RemoveRange(0, count);
                        sequenceNumberOffset += count;
                    }
                }
                return true;
            }
            return false;
        }

        private bool TryUpdateItemInternal(string pStrKey, T pRecord)
        {
            if (MyRecords.TryGetValue(pStrKey, out T tOld))
            {
                if (recordsBySequenceNumber != null)
                {
                    // TODO If we store the sequence number with each item, this becomes an O(1) operations
                    int index = recordsBySequenceNumber.FindIndex((r) => r == tOld);
                    if (pRecord is TheMetaDataBase pRecordMDB && pRecordMDB.cdeSEQ == null)
                    {
                        pRecordMDB.cdeSEQ = index + sequenceNumberOffset;
                    }
                    recordsBySequenceNumber[index] = pRecord;
                }
                MyRecords[pStrKey] = pRecord;
                return true;
            }
            else
            {
                return false;
            }
        }

        internal IEnumerable<T> GetItemsByInsertionOrderInternal(long previousSequenceNumber)
        {
            return GetItemsByInsertionOrderInternal(previousSequenceNumber, out _);
        }

        internal IEnumerable<T> GetItemsByInsertionOrderInternal(long previousSequenceNumber, out long firstSequenceNumberReturned)
        {
            if (recordsBySequenceNumber == null)
            {
                firstSequenceNumberReturned = 0;
                return null;
            }

            int index = GetIndexForSequenceNumber(previousSequenceNumber, out firstSequenceNumberReturned);
            return recordsBySequenceNumber.Skip(index);
        }

        private int GetIndexForSequenceNumber(long sequenceNumber, out long firstSequenceNumberReturned)
        {
            if (recordsBySequenceNumber == null)
            {
                firstSequenceNumberReturned = 0;
                return 0;
            }
            int index;
            if (sequenceNumber == 0 || sequenceNumber < sequenceNumberOffset) // TODO Deal with wrap around of sequence numbers
            {
                index = 0;
                firstSequenceNumberReturned = sequenceNumberOffset;
            }
            else
            {
                index = (int)(sequenceNumber - sequenceNumberOffset);
                firstSequenceNumberReturned = sequenceNumber;
            }
            return index;
        }

        internal T FindLastItemAtOrBeforeSequenceNumber(long sequenceNumber, Func<T, bool> predicate, out long foundItemSequenceNumber)
        {
            foundItemSequenceNumber = 0;
            if (recordsBySequenceNumber == null) return null;

            int index = GetIndexForSequenceNumber(sequenceNumber, out var ignored);

            T item = null;
            long foundNumber = 0;
            MyRecordsRWLock.RunUnderReadLock(() =>
            {
                while (index >= 0)
                {
                    if (index < recordsBySequenceNumber.Count)
                    {
                        var tempItem = recordsBySequenceNumber[index];
                        if (predicate(tempItem))
                        {
                            item = tempItem;
                            foundNumber = sequenceNumberOffset + index;
                            break;
                        }
                    }
                    index--;
                }
            });
            foundItemSequenceNumber = foundNumber;
            return item;
        }

        internal long GetOffsetByTimestamp(DateTimeOffset startTime)
        {
            if (recordsBySequenceNumber == null) return 0;

            var index = recordsBySequenceNumber.FindIndex(item => item.cdeCTIM >= startTime);
            if (index < 0)
            {
                return 0;
            }
            return sequenceNumberOffset + index;
        }
        internal long GetLastSequenceNumber()
        {
            if (recordsBySequenceNumber == null) return 0;
            return sequenceNumberOffset + recordsBySequenceNumber.LongCount();
        }

        internal void RemoveUpToSequenceNumberInternal(long LastSequenceNumberToRemove)
        {
            if (recordsBySequenceNumber == null) return;

            if (LastSequenceNumberToRemove == 0)
            {
                return;
            }

            List<T> itemsToRemove = null;
            bool doSave = false;
            MyRecordsRWLock.RunUnderUpgradeableReadLock( () =>
            {
                int index = (int)(LastSequenceNumberToRemove - sequenceNumberOffset);
                if (index > recordsBySequenceNumber.Count)
                {
                    index = recordsBySequenceNumber.Count - 1;
                }
                if (index >= 0 && index < recordsBySequenceNumber.Count)
                {
                    MyRecordsRWLock.RunUnderWriteLock(() =>
                    {
                        int countToRemove = index + 1;
                        itemsToRemove = recordsBySequenceNumber.Take(countToRemove).ToList();
                        RemoveCacheFilesUpTo(LastSequenceNumberToRemove, countToRemove);
                        foreach (var item in itemsToRemove)
                        {
                            // item == null indicates a previously deleted item: it was already removed from MyRecords and just in the recordsBySequenceNumber ordered list to preserve sequence numbers
                            if (item != null)
                            {
                                MyRecords.RemoveNoCare(item.cdeMID.ToString());
                            }
                        }
                        recordsBySequenceNumber.RemoveRange(0, countToRemove);
                        sequenceNumberOffset = AddToSequenceNumber(sequenceNumberOffset, countToRemove);
                        doSave = true;
                    });
                }
            });
            if (doSave && IsCachePersistent)
            {
                SaveCacheToDisk(false, false);
            }
            if (itemsToRemove != null)
            {
                NotifyOfUpdate(itemsToRemove);
            }
        }

        private void RemoveCacheFilesUpTo(long LastSequenceNumberToRemove, int totalCountToRemove)
        {
            if (AppendOnly && !TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID)
            {
                string fileToRewrite = "";
                if (MaxCacheFileSize > 0)
                {
                    DirectoryInfo di = new (TheCommonUtils.cdeFixupFileName(cacheFileFolder, true));
                    FileInfo[] fileInfo = di.GetFiles($"{MyStoreID.Replace('&', 'n')}*");
                    if (fileInfo.Length > 0)
                    {
                        List<FileInfo> orderedFileInfo = fileInfo.OrderBy(s => s.CreationTime).ToList();
                        for(int i = 0; i < orderedFileInfo.Count; i++)
                        {
                            var fInfo = orderedFileInfo[i];
                            if (i < orderedFileInfo.Count - 1)
                            {
                                if (fileOffsets[orderedFileInfo[i + 1].FullName] <= LastSequenceNumberToRemove)
                                {
                                    File.Delete(fInfo.FullName);
                                    fileOffsets.RemoveNoCare(fInfo.FullName);
                                }
                                else
                                {
                                    fileToRewrite = fInfo.FullName;
                                    break;
                                }
                            }
                            else
                                fileToRewrite = fInfo.FullName;
                        }
                    }
                }
                // Rewrite the one file that contains the record w/ LastSequenceNumberToRemove
                lock (saveLock)
                {
                    // Do we need anything additional here (locks, checks, etc.) for rewriting?
                    int countToRemove = TheCommonUtils.CInt(totalCountToRemove - (fileOffsets[fileToRewrite] - sequenceNumberOffset + 1));
                    if (countToRemove > 0)
                    {
                        string content = File.ReadAllText(fileToRewrite);
                        if (IsCacheEncrypted)
                        {
                            List<string> encryptedItems = TheCommonUtils.DeserializeJSONStringToObject<List<string>>(content);
                            encryptedItems.RemoveRange(0, countToRemove);
                            File.WriteAllText(fileToRewrite, TheCommonUtils.SerializeObjectToJSONString(encryptedItems));
                        }
                        else
                        {
                            List<T> items = TheCommonUtils.DeserializeJSONStringToObject<List<T>>(content);
                            items.RemoveRange(0, countToRemove);
                            File.WriteAllText(fileToRewrite, TheCommonUtils.SerializeObjectToJSONString(items));
                        }
                    }
                }
            }
        }

        internal long GetNextSequenceNumber(long sequenceNumber)
        {
            if (sequenceNumber == 0)
            {
                return sequenceNumberOffset;
            }
            var newSequenceNumber = sequenceNumber + 1;
            if (newSequenceNumber < sequenceNumber || newSequenceNumber == 0)
            {
                // TODO Handle Sequence number wrap around!!
                throw new NotImplementedException();
            }
            return newSequenceNumber;
        }

        internal long GetPreviousSequenceNumber(long sequenceNumber)
        {
            if (sequenceNumber == 0)
            {
                // TODO Handle Sequence number wrap around!!
                throw new NotImplementedException();
            }

            var newSequenceNumber = sequenceNumber - 1;
            if (newSequenceNumber == long.MaxValue)
            {
                // TODO Handle Sequence number wrap around!!
                throw new NotImplementedException();
            }
            return newSequenceNumber;
        }


        internal long AddToSequenceNumber(long sequenceNumber, int count)
        {
            if (sequenceNumber == 0)
            {
                sequenceNumber = sequenceNumberOffset;
            }
            var newSequenceNumber = sequenceNumber + count;
            if (newSequenceNumber == long.MaxValue || newSequenceNumber < sequenceNumber)
            {
                // TODO Handle Sequence number wrap around!!
                throw new NotImplementedException();
            }
            return newSequenceNumber;
        }

        #endregion

        // CODE REVIEW MH: Need to coordinate with RW lock. Also need to figuer out how this relates to the StorageMirror.MyRecordsLock
        internal IReaderWriterLock MyRecordsRWLock;
        /// <summary>
        /// My store identifier
        /// </summary>
        internal string MyStoreID;
        /// <summary>
        /// The maximum store size
        /// </summary>
        private int MaxStoreSize;
        /// <summary>
        /// My expiration test
        /// </summary>
        internal long MyExpirationTest;

        /// <summary>
        /// The event record expired
        /// </summary>
        internal Action<T> eventRecordExpired;
        /// <summary>
        /// The default maximum rows
        /// </summary>
        private int DefaultMaxRows;
        /// <summary>
        /// The initial cache SQL filter
        /// </summary>
        private string InitialCacheSQLFilter = "";
        /// <summary>
        /// The associated mirror
        /// </summary>
        private TheStorageMirror<T> AssociatedMirror;
        /// <summary>
        /// Keeps track of how many save's have been requested for this cache (used when IsStoreIntervalInSeconds == false)
        /// </summary>
        internal int mCacheCounter;
        /// <summary>
        /// KPI
        /// </summary>
        internal long mTotalCacheCounter;
        internal DateTimeOffset mLastStore = DateTimeOffset.MinValue;

#pragma warning disable 67
        //[Obsolete("This is no longer fired. Please remove your handler. We will remove this in a later version.")]
        //public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 67

        public bool BlockWriteIfIsolated
        {
            get;
            set;
        }
        /// <summary>
        /// Gets or sets a value indicating whether this instance is ready.
        /// </summary>
        /// <value><c>true</c> if this instance is ready; otherwise, <c>false</c>.</value>
        public bool IsReady { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this instance is cache encrypted.
        /// </summary>
        /// <value><c>true</c> if this instance is cache encrypted; otherwise, <c>false</c>.</value>
        public bool IsCacheEncrypted { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is cache persistent.
        /// </summary>
        /// <value><c>true</c> if this instance is cache persistent; otherwise, <c>false</c>.</value>
        public bool IsCachePersistent { get; set; }
        /// <summary>
        /// For persistent caches, gets or sets a value indicating whether to keep a backup copy during each save.
        /// </summary>
        /// <value><c>true</c> if this instance is cache persistent; otherwise, <c>false</c>.</value>
        public bool UseSafeSave { get; set; }
        /// <summary>
        /// For persistent mirror caches, indicates if save operations should minimize locking by serializing to memory instead of disk (trades off higher memory usage against performance)
        /// </summary>
        /// <value><c>true</c> if this instance uses in-memory serialization; otherwise, <c>false</c>.</value>
        public bool FastSaveLock { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this instance can be flushed.
        /// </summary>
        /// <value><c>true</c> if this instance can be flushed; otherwise, <c>false</c>.</value>
        internal bool CanBeFlushed { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether [allow fire updates].
        /// </summary>
        /// <value><c>true</c> if [allow fire updates]; otherwise, <c>false</c>.</value>
        public bool AllowFireUpdates { get; set; }

        public int CacheStoreInterval { get; set; }

        /// <summary>
        /// If set to true the store will save the content very CacheStoreInterval seconds. If set to false it requries CacheStoreInterval Changes to the store to write to disk
        /// </summary>
        public bool IsStoreIntervalInSeconds { get; set; }

        /// <summary>
        /// If set to true the store will only append records to disk and not store any in RAM (MyRecords of the MirrorCache)
        /// </summary>
        public bool AppendOnly { get; set; }

        /// <summary>
        /// Maximum size (in KB) allowed for a single cache file. Used only for AppendOnly StorageMirrors.
        /// </summary>
        public int MaxCacheFileSize { get; set; }

        /// <summary>
        /// Maxmimum number of cache files allowed. Once this number is reached, older files will be deleted. Used only for AppendOnly StorageMirrors.
        /// </summary>
        public int MaxCacheFileCount { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TheMirrorCache{T}"/> class.
        /// </summary>
        public TheMirrorCache()
        {
            MyStoreID = TheStorageUtilities.GenerateUniqueIDFromType(typeof(T), null);  //null is save...will be overwritten by MirrorCache with cachetablename
            MyRecordsRWLock = new TheMirrorCacheReaderWriterLock<T>(this);
            IsReady = true;
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="TheMirrorCache{T}"/> class.
        /// </summary>
        /// <param name="pExpirationTime">The p expiration time.</param>
        public TheMirrorCache(int pExpirationTime)
        {
            MyStoreID = TheStorageUtilities.GenerateUniqueIDFromType(typeof(T), null);  //null is save...will be overwritten by MirrorCache with cachetablename
            MyRecordsRWLock = new TheMirrorCacheReaderWriterLock<T>(this);
            SetExpireTime(pExpirationTime);
            IsReady = true;
        }

        private Timer mMyExpireTimer = null;
        internal void SetExpireTime(int pExpirationTime)
        {
            if (pExpirationTime > 0)
            {
                MyExpirationTest = pExpirationTime;
                if (mMyExpireTimer == null)
                {
                    var newTimer = new Timer(RemoveExpired, null, 0, pExpirationTime <= 24*60*60 ? pExpirationTime * 1000 : 24*60*60*1000);
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
            catch { 
                //ignored
            }
            try
            {
                previousTimer?.Dispose();
            }
            catch { 
                //ignored
            }
        }

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            DisposeExpireTimer();
        }
        /// <summary>
        /// Sets the maximum size of the store.
        /// </summary>
        /// <param name="pRecords">The p records.</param>
        public void SetMaxStoreSize(int pRecords)
        {
            MaxStoreSize = pRecords;
        }

        /// <summary>
        /// Enables APIs for retrieving items in the order in which they were inserted, including by sequence number. Can only be set on an empty cache.
        /// </summary>
        public bool TrackInsertionOrder()
        {
            if (MyRecords.Count > 0)
            {
                return false;
            }
            recordsBySequenceNumber ??= new List<T>();
            return true;
        }

        internal bool TracksInsertionOrder { get { return recordsBySequenceNumber != null; } }


        /// <summary>
        /// Sets the automatic update on change.
        /// </summary>
        /// <param name="pMirror">The p mirror.</param>
        /// <param name="BaseSQLFilter">The base SQL filter.</param>
        /// <param name="MaxRows">The maximum rows.</param>
        public void SetAutoUpdateOnChange(TheStorageMirror<T> pMirror, string BaseSQLFilter, int MaxRows)
        {
            InitialCacheSQLFilter = BaseSQLFilter;
            DefaultMaxRows = MaxRows;
            AssociatedMirror = pMirror;
            AssociatedMirror.AllowFireUpdates = true;
            AssociatedMirror.RegisterEvent(eStoreEvents.HasUpdates, sinkAutoUpdate);
        }

        /// <summary>
        /// Loads the cache from disk.
        /// </summary>
        /// <param name="LoadSync">if set to <c>true</c> load synchronized.</param>
        /// <returns><c>true</c> if load failed, <c>false</c> otherwise.</returns>
        public bool LoadCacheFromDisk(bool LoadSync)
        {
            if (IsCachePersistent || (AppendOnly && TracksInsertionOrder))
            {
                if (LoadSync)
                    DoLoadFromCache();
                else
                {
                    TheCommonUtils.cdeRunAsync("MirrorCacheLoad", true, (o) =>
                    {
                        DoLoadFromCache();
                    });
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Does the load from cache.
        /// </summary>
        private void DoLoadFromCache()
        {
            if (!TheBaseAssets.MasterSwitch || TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID) return;
            bool bLoaded = false;
            MyRecordsRWLock.RunUnderWriteLock(() => //LOCK-REVIEW: Here we need both locks: Write AND MyRecordsLock that no reading and Saving happens until this is done
            {
                //lock (MyRecordsLock)                //LOCK-REVIEW: Here we need both locks: Write AND MyRecordsLock that no reading and Saving happens until this is done
                {
                    // In case records are split among many files
                    List<string> filesToReturn = new ();
                    if (AppendOnly && MaxCacheFileSize > 0)
                    {
                        DirectoryInfo di = new (TheCommonUtils.cdeFixupFileName(cacheFileFolder, true));
                        FileInfo[] fileInfo = di.GetFiles($"{MyStoreID.Replace('&', 'n')}*");
                        if (fileInfo.Length > 0)
                        {
                            List<FileInfo> orderedFileInfo = fileInfo.OrderBy(s => s.CreationTime).ToList();
                            foreach (FileInfo f in orderedFileInfo)
                                filesToReturn.Add(f.FullName);
                        }
                        else
                            bLoaded = true;
                    }
                    else
                        filesToReturn.Add(TheCommonUtils.cdeFixupFileName(string.Format("cache\\{0}", MyStoreID.Replace('&', 'n')), true));
                    int i = 0;
                    foreach (string FileToReturn in filesToReturn)
                    {
                        try
                        {
                            if (System.IO.File.Exists(FileToReturn + ".0")) // new file written by a previous save operation, but not renamed (= crash or failure during save)
                            {
                                // Better to not use the .new file, as it is likely not written completely and the JSON deserializer may not recognize a corruption
                                // Trade off is that we lose a generation of Saves (since last flush), but it's better to lost data than to risk data corruption
                                // Instead, rename the potentially corrupt .0 file (.new) to .corrupt so we can do forensics on what kind of corruption we see
                                try
                                {
                                    var fileCorrupt = TheCommonUtils.cdeFixupFileName(string.Format("cache\\LostFound\\{0}", MyStoreID.Replace('&', 'n')), true);
                                    TheCommonUtils.CreateDirectories(fileCorrupt);
                                    if (TheBaseAssets.MyServiceHostInfo != null && TheBaseAssets.MyServiceHostInfo.DebugLevel >= eDEBUG_LEVELS.VERBOSE)
                                    {
                                        // For debuglevel verbose or higher: keep all potentially corrupt files
                                        System.IO.File.Move(FileToReturn + ".0", fileCorrupt + DateTimeOffset.Now.ToString("o").Replace(":", "").Replace(".", "") + ".corrupt");
                                    }
                                    else
                                    {
                                        // only keep the last potentially corrupt file
                                        try
                                        {
                                            System.IO.File.Delete(fileCorrupt + ".corrupt");
                                        }
                                        catch
                                        {
                                            // ignored
                                        }
                                        System.IO.File.Move(FileToReturn + ".0", fileCorrupt + ".corrupt");
                                    }
                                    // Log - detected failure during storage mirror save - data may have been lost
                                    TheBaseAssets.MySYSLOG.WriteToLog(4807, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheMirrorCache", string.Format("Detected earlier failure during save/shutdown while loading Cache file {0} for Mirror {1}.", FileToReturn, typeof(T)), eMsgLevel.l1_Error));
                                }
                                catch (Exception e)
                                {
                                    // Log - detected failure during storage mirror save - data may have been lost, unable to clean up .0 file
                                    TheBaseAssets.MySYSLOG.WriteToLog(4808, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheMirrorCache", string.Format("Detected earlier failure during save/shutdown while loading Cache file {0} for Mirror {1}. Unable to cleanup.", FileToReturn, typeof(T)), eMsgLevel.l1_Error, e.ToString()));
                                }
                            }

                            if (AppendOnly && TracksInsertionOrder)
                                fileOffsets.TryAdd(FileToReturn, recordsBySequenceNumber.Count);
                            if (LoadFile(FileToReturn))
                            {
                                bLoaded = i == 0 || bLoaded;
                            }
                            else
                            {
                                if (LoadFile(FileToReturn + ".1")) // Previous copy
                                {
                                    // Log: encountered corrupted storage mirror, falling back to latest snapshot (data may have been lost)
                                    TheBaseAssets.MySYSLOG.WriteToLog(4809, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheMirrorCache", string.Format("Loading of Cache file {0} for Mirror {1} failed, but recovered from previous backup copy. Some data may have been lost.", FileToReturn, typeof(T)), eMsgLevel.l2_Warning));
                                    bLoaded = i == 0 || bLoaded;
                                }
                                else
                                {
                                    if (System.IO.File.Exists(FileToReturn) || System.IO.File.Exists(FileToReturn + ".1"))
                                    {
                                        if (UseSafeSave)
                                            TheBaseAssets.MySYSLOG.WriteToLog(4810, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheMirrorCache", string.Format("Loading of Cache file {0} for Mirror {1} failed. Unable to recover from previous backup copies but maybe no file existed before.", FileToReturn, typeof(T)), eMsgLevel.l1_Error));
                                    }
                                    else
                                    {
                                        // No Store files exists: consider the store loaded
                                        bLoaded = i == 0 || bLoaded;
                                    }
                                    // Remove record of offset if loading failed
                                    if (AppendOnly && TracksInsertionOrder)
                                        fileOffsets.RemoveNoCare(FileToReturn);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4811, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheMirrorCache", string.Format("Loading of Cache file {0} for Mirror {1} failed", FileToReturn, typeof(T)), eMsgLevel.l1_Error, e.ToString()));
                        }
                        i++;
                    }
                }
                eventCachedReady?.Invoke(new TSM("TheMirrorCache", MyStoreID, bLoaded ? eMsgLevel.l4_Message : eMsgLevel.l1_Error, bLoaded ? MyStoreID : "ERROR: Failed to load store"));
            });
        }

        internal bool LoadFile(string FileToReturn)
        {
            bool bSuccess = false;
            if (System.IO.File.Exists(FileToReturn))
            {
                try
                {
                    var FileBytes = File.ReadAllBytes(FileToReturn);
                    bSuccess = ProcessLoad(FileBytes, null);
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4812, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheMirrorCache", string.Format("Loading of Cache file {0} for Mirror {1} failed", FileToReturn, typeof(T)), eMsgLevel.l1_Error, e.ToString()));
                }
            }
            return bSuccess;
        }

        /// <summary>
        /// Processes the load.
        /// </summary>
        /// <param name="FileBytes">The file bytes.</param>
        /// <param name="pLoadContent">Content of the p load.</param>
        private bool ProcessLoad(byte[] FileBytes, string pLoadContent)
        {
            bool bSuccess = false;
            if (FileBytes != null)
            {
                if (IsCacheEncrypted)
                {
                    if(AppendOnly)
                        pLoadContent = TheCommonUtils.CArray2UTF8String(FileBytes);
                    else
                        pLoadContent = TheCommonUtils.Decrypt(FileBytes, TheBaseAssets.MySecrets.GetNodeKey()); 
                }
                else
                    pLoadContent = TheCommonUtils.CArray2UTF8String(FileBytes);
            }
            if (!string.IsNullOrEmpty(pLoadContent))
            {
                try
                {
                    var storeData = new StoreData<T>();
                    // Requires special record-by-record decryption
                    if(IsCacheEncrypted && AppendOnly)
                    {
                        storeData.Records = new List<T>();
                        List<string> encryptedItems = TheCommonUtils.DeserializeJSONStringToObject<List<string>>(pLoadContent);
                        foreach (string item in encryptedItems)
                        {
                            byte[] itemAsBytes = Convert.FromBase64String(item);
                            string decryptedItem = TheCommonUtils.Decrypt(itemAsBytes, TheBaseAssets.MySecrets.GetNodeKey());
                            T record = TheCommonUtils.DeserializeJSONStringToObject<T>(decryptedItem);
                            storeData.Records.Add(record);
                        }
                    }
                    else if (!pLoadContent.StartsWith("{\"SequenceNumberOffset")) // Optimization: try ordered format first if header matches
                    {
                        try
                        {
                            storeData.Records = TheCommonUtils.DeserializeJSONStringToObject<List<T>>(pLoadContent);
                        }
                        catch
                        {
                            storeData = TheCommonUtils.DeserializeJSONStringToObject<StoreData<T>>(pLoadContent);
                        }
                    }
                    else
                    {
                        try
                        {
                            storeData = TheCommonUtils.DeserializeJSONStringToObject<StoreData<T>>(pLoadContent);
                        }
                        catch
                        {
                            storeData.Records = TheCommonUtils.DeserializeJSONStringToObject<List<T>>(pLoadContent);
                        }
                    }
                    if (storeData != null && storeData.Records != null)
                    {
                        MyRecords.Clear();
                        if (recordsBySequenceNumber != null && MaxCacheFileCount <= 0) // if loading multiple files, don't clear every time
                        {
                            recordsBySequenceNumber.Clear();
                        }

                        sequenceNumberOffset = storeData.SequenceNumberOffset;
                        foreach (T rec in storeData.Records)
                        {
                            if (rec != null)
                            {
                                if (!TryAddInternal(rec.cdeMID.ToString(), rec))
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(4813, new TSM("TheMirrorCache", $"Failed to Add Record {rec.cdeMID} to Mirror {typeof(T)} {MyStoreID}", eMsgLevel.l1_Error));
                                }
                            }
                            else
                            {
                                // Can't add a null record via TryAddInternal, but need to keep the slot in the ordered list
                                if (recordsBySequenceNumber != null)
                                {
                                    recordsBySequenceNumber.Add(null); // Captures deleted items which matter for insertion order/sequence numbers
                                }
                            }
                        }
                        bSuccess = true;
                    }
                    else
                    {
                        // Unable to find data in the file: consider it corrupt!
                        bSuccess = false;
                    }
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4814, new TSM("TheMirrorCache", $"Process Load failed during Deserialize for Mirror {typeof(T)} {MyStoreID}", eMsgLevel.l1_Error, e.ToString()));
                }
            }
            return bSuccess;
        }

        /// <summary>
        /// Saves the cache to disk.
        /// </summary>
        /// <param name="SaveSync">If set to true, the call does not return until the data has been saved to disk.<c>true</c> [save synchronize].</param>
        /// <param name="WaitForSave">if set to true, a save will be initiated even if another save is currently ongoing. Otherwise, no additional save will be performed.<c>true</c> [wait for save].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public bool SaveCacheToDisk(bool SaveSync, bool WaitForSave)
        {
            if (!IsCachePersistent || TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID || (TheBaseAssets.MyServiceHostInfo.IsIsolated && BlockWriteIfIsolated))
            {
                return false;
            }

            mCacheCounter++;
            if (!SaveSync)
            {
                if (!IsStoreIntervalInSeconds && mCacheCounter < CacheStoreInterval)
                {
                    return false;
                }
                if (!UseSafeSave)
                {
                    bool bIsLocked = MyRecordsRWLock.IsWriteLocked() || MyRecordsRWLock.IsUpgradeableReadLocked();
                    if (bIsLocked)
                    {
                        // Don't write non-critical stores if they are locked by other writers (performance over correctness)
                        TheBaseAssets.MySYSLOG.WriteToLog(4801, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Skipping Save of {typeof(T)} {MyStoreID} as it is non-critical store", eMsgLevel.l4_Message));
                        return false;
                    }
                }
                var waitCount = Interlocked.Increment(ref _saveLockWaitCount);
                if (waitCount > 1)
                {
                    // One other thread is already waiting to save the contents of the cache. Any changes up to this point will be saved by that thread, so we can avoid taking another lock and attempting another write for this thread
                    TheBaseAssets.MySYSLOG.WriteToLog(4816, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Skipping Save of {typeof(T)} {MyStoreID} as another write is already queued", eMsgLevel.l4_Message, $"Count: {waitCount}"));
                    return false;
                }
            }
            else
            {
                Interlocked.Increment(ref _saveLockWaitCount);
            }

            mTotalCacheCounter++;
            if (SaveSync)
                DoSaveCacheToDisk(WaitForSave, true);
            else
                TheCommonUtils.cdeRunAsync("MirrorCacheSave", true, (o) =>
                {
                    DoSaveCacheToDisk(WaitForSave, false);
                });
            return true;
        }

        public void ForceSave()
        {
            if (IsCachePersistent)
            {
                SaveCacheToDisk(true, true);
            }
        }

        class StoreData<T1>
        {
            public long SequenceNumberOffset;
            public List<T1> Records;
        }

        long _saveLockWaitCount = 0; // Counts how many threads have attempted to take the saveLock
        readonly object saveLock = new (); // This lock must be taken while writing to the cache files
        /// <summary>
        /// Does the save cache to disk.
        /// </summary>
        /// <param name="waitForSave">if true, the function blocks until save was completed</param>
        /// <param name="bForce">Force Saving.</param>
        private void DoSaveCacheToDisk(bool waitForSave, bool bForce)
        {
            if (TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID || (TheBaseAssets.MyServiceHostInfo.IsIsolated && BlockWriteIfIsolated) || TheBaseAssets.MyServiceHostInfo.IsBackingUp)
                return;

            if (!TheBaseAssets.MasterSwitch && TheBaseAssets.ForceShutdown)
            {
                return;
            }

            // Prevent the engine from shutting down while we save
            Interlocked.Increment(ref TheBaseAssets.DelayShutDownCount);
            lock (saveLock) // Ensure we do at most one file write at a time (per cache)
            {
                // Check how long ago the last save finished
                var timeSinceLastSave = DateTimeOffset.Now - mLastStore;
                var timeBetweenSaves = (IsStoreIntervalInSeconds ? CacheStoreInterval * 1000 : 10000);

                if (!bForce && timeSinceLastSave.TotalMilliseconds < timeBetweenSaves)
                {
                    if (!waitForSave)
                    {
                        var waitCount = Interlocked.Decrement(ref _saveLockWaitCount);
                        if (waitCount > 0)
                        {
                            waitForSave = true;
                        }
                    }
                    if (waitForSave)
                    {
                        var delay = timeBetweenSaves - (int)timeSinceLastSave.TotalMilliseconds + 15; // 15ms: timer precision in .Net/Windows - adding this to ensure that the measured time on the re-save will be outside of the time window
                        TheBaseAssets.MySYSLOG.WriteToLog(4818, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Scheduled Save for {typeof(T)} {MyStoreID} after skipped saves", eMsgLevel.l4_Message, $"Delay: {delay}"));
                        // Don't save too often: wait until the store interval or 10s
                        TheCommonUtils.TaskDelayOneEye(delay, 100).ContinueWith(t =>
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4818, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Running Scheduled Save for {typeof(T)} {MyStoreID} after skipped saves", eMsgLevel.l4_Message));
                            DoSaveCacheToDisk(waitForSave, false);
                            Interlocked.Decrement(ref TheBaseAssets.DelayShutDownCount);
                        });
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4818, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Not Scheduling Save for {typeof(T)} {MyStoreID} because wait for save was not specified and no other save was pending", eMsgLevel.l4_Message));
                    }
                }
                else
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    long waitCountOnEnter = 0;
                    try
                    {
                        string fileToReturn = TheCommonUtils.cdeFixupFileName(string.Format("cache\\{0}", MyStoreID.Replace('&', 'n')), true);
                        TheCommonUtils.CreateDirectories(fileToReturn);
                        var newFileName = UseSafeSave ? fileToReturn + ".0" : fileToReturn;

                        object storeData = null;
                        string tJSON = null;

                        MyRecordsRWLock.RunUnderReadLock(() => // Lock out any writers while we save so that we get a consistent snapshot
                        {
                            // Reset the wait counter. Any threads coming in after this are not guaranteed that the content of the cache will be saved so need to actually take the lock
                            waitCountOnEnter = Interlocked.Exchange(ref _saveLockWaitCount, 0);

                            if (recordsBySequenceNumber != null)
                            {
                                storeData = new StoreData<T> { SequenceNumberOffset = sequenceNumberOffset, Records = recordsBySequenceNumber };
                            }
                            else
                            {
                                storeData = MyRecords.Values; //LOCK-REVIEW: starting here, internal store and list can diviate, but no lock required
                            }

                            if ((IsReady && recordsBySequenceNumber != null) || MyRecords.Count > 0)
                            {
                                if (IsCacheEncrypted || FastSaveLock)
                                {
                                    tJSON = TheCommonUtils.SerializeObjectToJSONString(storeData);
                                }
                                else
                                {
                                    TheCommonUtils.SerializeObjectToJSONFileInternal(newFileName, storeData);
                                }
                            }
                            else
                            {
                                storeData = null;
                            }
                        });
                        if (storeData != null)
                        {
                            if (!string.IsNullOrEmpty(tJSON))
                            {
                                if (IsCacheEncrypted)
                                {
                                    var tBytes = TheBaseAssets.MyCrypto.Encrypt(TheCommonUtils.CUnicodeString2Array(tJSON), TheBaseAssets.MySecrets.GetAK(), TheBaseAssets.MySecrets.GetNodeKey());
                                    System.IO.File.WriteAllBytes(newFileName, tBytes);
                                }
                                else
                                {
                                    System.IO.File.WriteAllText(newFileName, tJSON);
                                }
                            }
                            tJSON = null;

                            if (UseSafeSave)
                            {
                                try
                                {
                                    System.IO.File.Delete(fileToReturn + ".1");
                                }
                                catch
                                {
                                    // ignored
                                }

                                try
                                {
                                    System.IO.File.Move(fileToReturn, fileToReturn + ".1");
                                }
                                catch
                                {
                                    // ignored
                                }

                                System.IO.File.Move(fileToReturn + ".0", fileToReturn);
                            }
                        }
                        else
                        {
                            if (System.IO.File.Exists($"{fileToReturn}"))
                                System.IO.File.Delete(fileToReturn);
                            if (System.IO.File.Exists($"{fileToReturn}.1"))
                                System.IO.File.Delete($"{fileToReturn}.1");
                            if (System.IO.File.Exists($"{fileToReturn}.0"))
                                System.IO.File.Delete($"{fileToReturn}.0");
                        }
                        mLastStore = DateTimeOffset.Now;
                        mCacheCounter = 0;
                    }
                    catch (OutOfMemoryException e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4802, new TSM("TheMirrorCache", $"Fatal Error: Saving of Cache file for Mirror {typeof(T)} {MyStoreID} failed with out of Memory", eMsgLevel.l1_Error, e.ToString()));
                        if (this.CanBeFlushed)
                        {
                            Clear(true);
                        }
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4803, TSM.L(eDEBUG_LEVELS.OFF) && TheBaseAssets.MyServiceHostInfo.IsCloudService ? null : new TSM("TheMirrorCache", $"Saving of Cache file for Mirror {typeof(T)} {MyStoreID} failed", eMsgLevel.l1_Error, e.ToString()));
                    }
                    finally
                    {
                        var elapsed = sw.ElapsedMilliseconds;
                        sw.Stop();
                        if (elapsed > (IsStoreIntervalInSeconds ? CacheStoreInterval * 1000 : 10000))
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4817, TSM.L(eDEBUG_LEVELS.OFF) && TheBaseAssets.MyServiceHostInfo.IsCloudService ? null : new TSM("TheMirrorCache", $"Saving of Cache file for Mirror {typeof(T)} {MyStoreID}  took longer than expected", eMsgLevel.l2_Warning, $"Time: {elapsed}"));
                        }
                        if (waitCountOnEnter > 1)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4818, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Completed Save of {typeof(T)} {MyStoreID} after skipped saves", eMsgLevel.l4_Message, $"Count: {waitCountOnEnter}. Time: {elapsed}"));
                        }
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4819, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Completed Save of {typeof(T)} {MyStoreID}", eMsgLevel.l4_Message, $"Count: {waitCountOnEnter}. Time: {elapsed}"));
                        }
                    }
                }
            }
            Interlocked.Decrement(ref TheBaseAssets.DelayShutDownCount);
        }

        /// <summary>
        /// Writes the records to the end of the storage cache file on disk without adding to TheMirrorCache.MyRecords in RAM
        /// </summary>
        /// <param name="items">The List of items to append to disk</param>
        /// <param name="saveSync">If set to true, the call does not return until the data has been appended to disk</param>
        /// <param name="waitForSave">If set to true, an append will be initiated even if another append is currently ongoing. Otherwise, no additional append will be performed</param>
        /// <returns>True if the append was successful and false otherwise</returns>
        internal bool AppendRecordsToDisk(Dictionary<string, T> items, bool saveSync, bool waitForSave)
        {
            if (TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID || (TheBaseAssets.MyServiceHostInfo.IsIsolated && BlockWriteIfIsolated))
            {
                return false;
            }
            //
            // Future code for checking "force save" and "wait for save", etc. here
            //
            if (saveSync)
            {
                Task<bool> appendTask = DoAppendRecordsToDisk(items, waitForSave, true);
                appendTask.Wait();
                if (appendTask.Result && TracksInsertionOrder)
                {
                    foreach (T item in items.Values)
                        TryAddInternal(item.cdeMID.ToString(), item);
                }
            }
            else
            {
                TheCommonUtils.cdeRunAsync("MirrorCacheAppend", true, (o) =>
                {
                    Task<bool> appendTask = DoAppendRecordsToDisk(items, waitForSave, false);
                    appendTask.Wait();
                    if (appendTask.Result && TracksInsertionOrder)
                    {
                        foreach (T item in items.Values)
                            TryAddInternal(item.cdeMID.ToString(), item);
                    }
                });
            }
            return true;
        }

        /// <summary>
        /// Called after a new cache file is created.
        /// Occurs for AppendOnly StorageMirrors that have defined a MaxCacheFileSize
        /// </summary>
        public Func<object, List<T>> NewCacheFileCallback;
        private const string cacheFileFolder = "cache\\";

        private Task<bool> DoAppendRecordsToDisk(Dictionary<string, T> items, bool waitForSave, bool bForce)
        {
            if (items == null || items.Count == 0 || TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID || (TheBaseAssets.MyServiceHostInfo.IsIsolated && BlockWriteIfIsolated) || TheBaseAssets.MyServiceHostInfo.IsBackingUp)
                return TheCommonUtils.TaskFromResult(false);

            if (!TheBaseAssets.MasterSwitch && TheBaseAssets.ForceShutdown)
            {
                return TheCommonUtils.TaskFromResult(false);
            }
            bool completedAppend = false;
            Interlocked.Increment(ref TheBaseAssets.DelayShutDownCount);
            lock (saveLock)
            {
                // Taken from DoSaveCacheToDisk - handles waiting for the store interval
                var timeSinceLastSave = DateTimeOffset.Now - mLastStore;
                var timeBetweenSaves = (IsStoreIntervalInSeconds ? CacheStoreInterval * 1000 : 10000);

                if (!bForce && timeSinceLastSave.TotalMilliseconds < timeBetweenSaves)
                {
                    if (!waitForSave)
                    {
                        var waitCount = Interlocked.Decrement(ref _saveLockWaitCount);
                        if (waitCount > 0)
                        {
                            waitForSave = true;
                        }
                    }
                    if (waitForSave)
                    {
                        var delay = timeBetweenSaves - (int)timeSinceLastSave.TotalMilliseconds + 15;
                        TheBaseAssets.MySYSLOG.WriteToLog(4830, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Scheduled Append for {typeof(T)} {MyStoreID} after skipped appends", eMsgLevel.l2_Warning, $"Delay: {delay}"));

                        return TheCommonUtils.TaskDelayOneEye(delay, 100).ContinueWith(t =>
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4831, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Running Scheduled Append for {typeof(T)} {MyStoreID} after skipped appends", eMsgLevel.l2_Warning));
                            Task<bool> appendTask = DoAppendRecordsToDisk(items, waitForSave, false);
                            appendTask.Wait();
                            if (appendTask.Result)
                                completedAppend = true;
                            Interlocked.Decrement(ref TheBaseAssets.DelayShutDownCount);
                            return completedAppend;
                        });
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4832, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Not Scheduling Append for {typeof(T)} {MyStoreID} because wait for save was not specified and no other save was pending", eMsgLevel.l4_Message));
                    }
                }
                else
                {

                    var stopWatch = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        // Find the correct file to append to
                        string fileToReturn = "";
                        DirectoryInfo di = new (TheCommonUtils.cdeFixupFileName(cacheFileFolder, true));
                        List<T> baseLineItems = null; // Any records that need to be inserted at the beginning of a new file
                        if (MaxCacheFileSize > 0)
                        {
                            FileInfo[] fileInfo = di.GetFiles($"{MyStoreID.Replace('&', 'n')}*");
                            if (fileInfo.Length > 0)
                            {
                                List<FileInfo> orderedFileInfo = fileInfo.OrderBy(s => s.CreationTime).ToList();
                                FileInfo newestFile = orderedFileInfo.Last();
                                if ((1.0 * newestFile.Length / 1000) > MaxCacheFileSize)
                                {
                                    string fileName = $"{MyStoreID.Replace('&', 'n')}_{DateTime.Now:yyyMMdd_HHmmss}";
                                    fileToReturn = TheCommonUtils.cdeFixupFileName(cacheFileFolder + fileName, true);
                                    baseLineItems = NewCacheFileCallback?.Invoke(null);
                                }
                                else
                                    fileToReturn = newestFile.FullName;
                            }
                            else
                            {
                                string fileName = $"{MyStoreID.Replace('&', 'n')}_{DateTime.Now:yyyMMdd_HHmmss}";
                                fileToReturn = TheCommonUtils.cdeFixupFileName(cacheFileFolder + fileName, true);
                                baseLineItems = NewCacheFileCallback?.Invoke(null);
                            }
                        }
                        else
                        {
                            fileToReturn = TheCommonUtils.cdeFixupFileName(cacheFileFolder + MyStoreID.Replace('&', 'n'), true);
                        }
                        TheCommonUtils.CreateDirectories(fileToReturn);
                        FileStream fileStream;
                        // Create the file if nonexistent
                        if (!File.Exists(fileToReturn))
                        {
                            fileStream = File.Create(fileToReturn);
                            FileInfo[] cacheFiles = di.GetFiles($"{MyStoreID.Replace('&', 'n')}*");
                            if (MaxCacheFileCount > 0 && cacheFiles.Length > MaxCacheFileCount)
                            {
                                List<FileInfo> orderedFiles = cacheFiles.OrderBy(s => s.CreationTime).ToList();
                                string toDelete = orderedFiles.First().FullName;
                                if (TracksInsertionOrder)
                                {
                                    long lastSequenceNumberToRemove = fileOffsets.cdeSafeGetValue(orderedFiles[1]?.FullName);
                                    int index = (int)(lastSequenceNumberToRemove - sequenceNumberOffset);
                                    if (index > recordsBySequenceNumber.Count)
                                        index = recordsBySequenceNumber.Count - 1;
                                    if (index >= 0 && index < recordsBySequenceNumber.Count)
                                    {
                                        int countToRemove = index + 1;
                                        recordsBySequenceNumber.RemoveRange(0, countToRemove);
                                        sequenceNumberOffset = AddToSequenceNumber(sequenceNumberOffset, countToRemove);
                                    }
                                    fileOffsets.RemoveNoCare(toDelete);
                                }
                                File.Delete(toDelete);
                            }
                            if(TracksInsertionOrder)
                                fileOffsets.TryAdd(fileToReturn, recordsBySequenceNumber.Count + sequenceNumberOffset - 1);
                        }
                        else
                            fileStream = new FileStream(fileToReturn, FileMode.Open, FileAccess.ReadWrite);
                        if (IsCacheEncrypted)
                        {
                            // Uses special method to append records as an array of encrypted strings
                            AppendEncryptedRecords(items.Values.ToList(), fileStream, baseLineItems);
                            completedAppend = true;
                        }
                        else
                        {
                            using (fileStream)
                            {
                                // Build up JSON string to append
                                string tJSON = "";
                                if (fileStream.Length == 0)
                                {
                                    List<T> itemsToAppend = new ();
                                    if (baseLineItems?.Count > 0)
                                    {
                                        itemsToAppend.AddRange(baseLineItems);
                                        if (TracksInsertionOrder)
                                            recordsBySequenceNumber.AddRange(baseLineItems);
                                    }
                                    itemsToAppend.AddRange(items.Values.ToList());
                                    string serializedItems = TheCommonUtils.SerializeObjectToJSONString(itemsToAppend);
                                    tJSON += serializedItems;
                                }
                                else
                                {
                                    fileStream.Seek(-1, SeekOrigin.End);
                                    if (fileStream.ReadByte() == ']')
                                    {
                                        fileStream.Seek(-1, SeekOrigin.Current);
                                        string serializedItems = TheCommonUtils.SerializeObjectToJSONString(items.Values.ToList());
                                        if (serializedItems.Length >= 4) // List with empty object, e.g. [{}]
                                            tJSON += "," + serializedItems.Substring(1);
                                    }
                                }
                                byte[] content = System.Text.Encoding.Default.GetBytes(tJSON);
                                fileStream.Write(content, 0, content.Length);
                                completedAppend = true;
                            }
                        }
                        mLastStore = DateTimeOffset.Now;
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4821, TSM.L(eDEBUG_LEVELS.OFF) && TheBaseAssets.MyServiceHostInfo.IsCloudService ? null : new TSM("TheMirrorCache", $"Appending of records to Cache file for Mirror {typeof(T)} {MyStoreID} failed", eMsgLevel.l1_Error, e.ToString()));
                    }
                    finally
                    {
                        var elapsed = stopWatch.ElapsedMilliseconds;
                        stopWatch.Stop();
                        if (elapsed > (IsStoreIntervalInSeconds ? CacheStoreInterval * 1000 : 10000))
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4822, TSM.L(eDEBUG_LEVELS.OFF) && TheBaseAssets.MyServiceHostInfo.IsCloudService ? null : new TSM("TheMirrorCache", $"Appending of records to Cache file for Mirror {typeof(T)} {MyStoreID}  took longer than expected", eMsgLevel.l2_Warning, $"Time: {elapsed}"));
                        }
                        TheBaseAssets.MySYSLOG.WriteToLog(4823, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", $"Completed append of {typeof(T)} {MyStoreID}", eMsgLevel.l2_Warning, $"Time: {elapsed}"));
                    }
                }
            }
            Interlocked.Decrement(ref TheBaseAssets.DelayShutDownCount);
            return TheCommonUtils.TaskFromResult(completedAppend);
        }

        // Helper method for specifically appending encrypted records
        // Serializes each record, encrypts it, then converts to a base 64 string
        private void AppendEncryptedRecords(List<T> items, FileStream fileStream, List<T> baseLineItems = null)
        {
            using (fileStream)
            {
                string tJSON = "";
                if (fileStream.Length == 0)
                {
                    byte[] arr = System.Text.Encoding.Default.GetBytes("[]");
                    fileStream.Write(arr, 0, arr.Length);
                    if(baseLineItems?.Count > 0)
                    {
                        if (TracksInsertionOrder)
                            recordsBySequenceNumber.AddRange(baseLineItems);
                        baseLineItems.AddRange(items);
                        items = baseLineItems;
                    }
                }
                else
                    tJSON += ",";
                fileStream.Seek(-1, SeekOrigin.End);
                if (fileStream.ReadByte() == ']')
                {
                    fileStream.Seek(-1, SeekOrigin.Current);
                    int i = 0;
                    foreach (T item in items)
                    {
                        string serializedItem = TheCommonUtils.SerializeObjectToJSONString(item);
                        byte[] encryptedItem = TheBaseAssets.MyCrypto.Encrypt(TheCommonUtils.CUnicodeString2Array(serializedItem), TheBaseAssets.MySecrets.GetAK(), TheBaseAssets.MySecrets.GetNodeKey());
                        string base64Item = Convert.ToBase64String(encryptedItem);
                        tJSON += $"\"{base64Item}\"";
                        if (++i < items.Count)
                            tJSON += ",";
                    }
                }
                byte[] content = System.Text.Encoding.Default.GetBytes(tJSON + "]");
                fileStream.Write(content, 0, content.Length);
            }
        }

        /// <summary>
        /// Removes all items.
        /// </summary>
        public void RemoveAllItems()
        {
            FlushCache(false);
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        public void Reset()
        {
            MyRecordsRWLock.RunUnderWriteLock(() =>  //lock (MyRecordsLock)
            {
                MyRecords.Clear();
                if (recordsBySequenceNumber != null)
                {
                    sequenceNumberOffset += recordsBySequenceNumber.Count;
                    recordsBySequenceNumber.Clear();
                }
            });
            SaveCacheToDisk(false, false);
        }

        /// <summary>
        /// Deletes all cache files for this MirrorCache.
        /// Used for AppendOnly StorageMirrors that are broken up into multiple files.
        /// </summary>
        private void DeleteAllCacheFiles()
        {
            string directoryPath = TheCommonUtils.cdeFixupFileName(cacheFileFolder, true);
            string[] files = Directory.GetFiles(directoryPath, $"{MyStoreID.Replace('&', 'n')}*");
            foreach (string file in files)
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Flushes the cache.
        /// </summary>
        /// <param name="BackupFirst">if set to <c>true</c> backup the existing cache first.</param>
        /// <returns><c>true</c> if not successful, <c>false</c> otherwise.</returns>
        public bool FlushCache(bool BackupFirst = false)
        {
            return FlushCache(BackupFirst, false);
        }
        public bool FlushCache(bool BackupFirst, bool ForceFlush=false)
        {
            if (IsCachePersistent && (CanBeFlushed || ForceFlush) && !TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID && (!TheBaseAssets.MyServiceHostInfo.IsIsolated || !BlockWriteIfIsolated))
            {
                string FileToReturn = TheCommonUtils.cdeFixupFileName(string.Format("cache\\{0}", MyStoreID.Replace('&', 'n')), true);
                try
                {
                    if (File.Exists(FileToReturn) && BackupFirst)
                    {
                        //Creates a new, blank zip file to work with - the file will be
                        //finalized when the using statement completes
                        string BackupFile = $"{FileToReturn}_{TheCommonUtils.GetTimeStamp()}.CDEB";
                        int i = 0;
                        while (File.Exists(BackupFile))
                            BackupFile = $"{FileToReturn}_{TheCommonUtils.GetTimeStamp()}_{i++}.CDEB";
#if !CDE_NET35 && !CDE_NET4
                        using (System.IO.Compression.ZipArchive newFile = System.IO.Compression.ZipFile.Open(BackupFile, System.IO.Compression.ZipArchiveMode.Create))
                        {
                            newFile.CreateEntry(FileToReturn);
                        }
#else
                            System.IO.File.Move(FileToReturn, $"{BackupFile}AK");
#endif
                    }
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4804,
                        new TSM("TheMirrorCache",
                            string.Format("FlushCache of Cache file {0} for Mirror {1} could not create backup first", FileToReturn,
                                typeof(T)), eMsgLevel.l2_Warning, e.ToString()));
                }
            }
            Reset();
            return true;
        }

        public bool RemoveStore(bool BackupFirst)
        {
            IsReady = false;
            FlushCache(BackupFirst, true);
            return false;
        }

        /// <summary>
        /// Clears the specified zip before.
        /// </summary>
        /// <param name="ZipBefore">if set to <c>true</c> [zip before].</param>
        public void Clear(bool ZipBefore)
        {
            FlushCache(ZipBefore, false);
        }

        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>The count.</value>
        public int Count
        {
            get
            {
                return MyRecords.Count;
            }
        }
        internal bool IsEmpty
        {
            get
            {
                return MyRecords.IsEmpty;
            }
        }

        /// <summary>
        /// Sinks the automatic update.
        /// </summary>
        /// <param name="NOTUSED">The notused.</param>
        private void sinkAutoUpdate(StoreEventArgs NOTUSED)
        {
            Fill(AssociatedMirror, InitialCacheSQLFilter, DefaultMaxRows, false);
        }

        /// <summary>
        /// Fills the specified p response.
        /// </summary>
        /// <param name="pResponse">The p response.</param>
        public void Fill(TheStorageMirror<T>.StoreResponse pResponse)
        {
            MyRecordsRWLock.RunUnderWriteLock(() => //LOCK-REVIEW: This is encapulating two writelocks - is logic lock required?
            {
                Reset();
                if (!pResponse.HasErrors && pResponse.MyRecords != null && pResponse.MyRecords.Count > 0)
                    ReturnFill(pResponse);
            });
        }

        /// <summary>
        /// Fills the specified p mirror.
        /// </summary>
        /// <param name="pMirror">The p mirror.</param>
        /// <param name="LocalCallBackOnly">if set to <c>true</c> [local call back only].</param>
        public void Fill(TheStorageMirror<T> pMirror, bool LocalCallBackOnly)
        {
            pMirror.GetRecords(ReturnFill, LocalCallBackOnly);
        }
        /// <summary>
        /// Fills the specified p mirror.
        /// </summary>
        /// <param name="pMirror">The p mirror.</param>
        /// <param name="SQLFilter">The SQL filter.</param>
        /// <param name="MaxRows">The maximum rows.</param>
        /// <param name="LocalCallBackOnly">if set to <c>true</c> [local call back only].</param>
        public void Fill(TheStorageMirror<T> pMirror, string SQLFilter, int MaxRows, bool LocalCallBackOnly)
        {
            TheBaseAssets.MyServiceHostInfo.TO.MakeHeartPump();
            pMirror.GetRecords(SQLFilter, MaxRows, ReturnFill, LocalCallBackOnly);
        }

        /// <summary>
        /// Returns the fill.
        /// </summary>
        /// <param name="pNewRecords">The p new records.</param>
        void ReturnFill(TheStorageMirror<T>.StoreResponse pNewRecords)
        {
            List<T> resList = null;
            MyRecordsRWLock.RunUnderWriteLock(() => // lock (MyRecordsLock)
            {
                Reset();
                if (!pNewRecords.HasErrors)
                {
                    foreach (T rec in pNewRecords.MyRecords)
                        DoAddItem(rec.cdeMID, rec, false);
                    RemoveRetired();
                    if (IsCachePersistent)
                        SaveCacheToDisk(false, false);
                }
                resList = MyRecords.Values.ToList();
            });
            TheBaseAssets.MyServiceHostInfo.TO.MakeHeartNormal();
            NotifyOfUpdate(resList);
        }

        /// <summary>
        /// Gets the values.
        /// </summary>
        /// <value>The values.</value>
        public List<T> TheValues
        {
            get
            {
                List<T> items = null;
                MyRecordsRWLock.RunUnderReadLock(() =>
                {
                    try
                    {
                        items = MyRecords?.Select(kv => kv.Value)?.ToList(); // Calls ConcurrentDictionary.GetEnumerator() which takes no lock but the snapshot is only consistent if no concurrent writes happen
                    }
                    catch { 
                        //ignored
                    }
                });
                return items;
            }
        }
        /// <summary>
        /// Gets the keys.
        /// </summary>
        /// <value>The keys.</value>
        public List<string> TheKeys
        {
            get
            {
                List<string> keys = null;
                MyRecordsRWLock.RunUnderReadLock(() =>
                {
                    try
                    {
                        keys = MyRecords?.Select(kv => kv.Key)?.ToList();
                    }
                    catch { 
                        //ignored
                    }
                });
                return keys;
            }
        }

        /// <summary>
        /// Blob and MyEngines
        /// </summary>
        /// <param name="pStrKey">The p string key.</param>
        /// <returns><c>true</c> if the specified p string key contains identifier; otherwise, <c>false</c>.</returns>
        public bool ContainsID(string pStrKey)
        {
            return MyRecords.ContainsKey(pStrKey);
        }
        /// <summary>
        /// Determines whether the specified p key contains identifier.
        /// </summary>
        /// <param name="pKey">The p key.</param>
        /// <returns><c>true</c> if the specified p key contains identifier; otherwise, <c>false</c>.</returns>
        public bool ContainsID(Guid pKey)
        {
            return MyRecords.ContainsKey(pKey.ToString());
        }
        /// <summary>
        /// Allowed for MyBlobData
        /// </summary>
        /// <param name="pKey">The p key.</param>
        /// <returns><c>true</c> if [contains identifier string] [the specified p key]; otherwise, <c>false</c>.</returns>
        public bool ContainsIDStr(string pKey)
        {
            return MyRecords.ContainsKey(pKey);
        }

        /// <summary>
        /// Shutdowns this instance.
        /// </summary>
        public void Shutdown()
        {
            if (MyExpirationTest > 0)
                Dispose();
        }

        /// <summary>
        /// Adds an item.
        /// </summary>
        /// <param name="pDetails">The p details.</param>
        /// <param name="CallBack">The call back.</param>
        public void AddAnItem(T pDetails, Action<List<T>> CallBack)
        {
            List<T> tList = new ()
            {
                pDetails
            };
            AddItems(tList, CallBack);
        }
        /// <summary>
        /// Adds an item.
        /// </summary>
        /// <param name="pID">The p identifier.</param>
        /// <param name="pDetail">The p detail.</param>
        /// <param name="CallBack">The call back.</param>
        public void AddAnItem(Guid pID, T pDetail, Action<T> CallBack)
        {
            if (pID != Guid.Empty)
                pDetail.cdeMID = pID;
            DoAddItem(pDetail.cdeMID, pDetail, false);  //Must not remove items as its not under a lock
            if (IsCachePersistent)
                SaveCacheToDisk(false, true);
            CallBack?.Invoke(pDetail);
        }
        /// <summary>
        /// Adds the items.
        /// </summary>
        /// <param name="pDetails">The p details.</param>
        /// <param name="CallBack">The call back.</param>
        public void AddItems(List<T> pDetails, Action<List<T>> CallBack)
        {
            List<T> retList = new ();
            MyRecordsRWLock.RunUnderWriteLock( () =>  //LOCK-REVIEW: is this ok for consitency? -> Possible optimization: Could only use write lock around DoAddItem
            {
                foreach (T detail in pDetails)
                {
                    if (detail.cdeMID == Guid.Empty)
                        detail.cdeMID = Guid.NewGuid();
                    if (DoAddItem(detail.cdeMID, detail, false))
                        retList.Add(detail);
                }
                RemoveRetired();
            });
            if (IsCachePersistent)
                SaveCacheToDisk(false, true);
            CallBack?.Invoke(retList);
        }
        /// <summary>
        /// New in V2.5: Adds a dictionary of T to the StorageMirrorCache
        /// The Guid in the Key will be copied to the cdeMID (IndexKey) of T
        /// If the key already exists in the store, the record will not be added.
        /// </summary>
        /// <param name="pDetails">List of Records to be added to the Mirror</param>
        /// <param name="CallBack">Callback with a list of all successfully added items</param>
        public void AddItems(Dictionary<Guid, T> pDetails, Action<List<T>> CallBack)
        {
            List<T> retList = new ();
            MyRecordsRWLock.RunUnderWriteLock(() => //LOCK-REVIEW: is this ok for consitency?
            {
                foreach (Guid tID in pDetails.Keys)
                {
                    T detail = pDetails[tID];
                    detail.cdeMID = tID;
                    if (!MyRecords.ContainsKey(tID.ToString()) && DoAddItem(tID, detail, false))
                        retList.Add(detail);
                }
                RemoveRetired();
            });
            if (IsCachePersistent)
                SaveCacheToDisk(false, true);
            CallBack?.Invoke(retList);
        }

        /// <summary>
        /// Removes the items.
        /// </summary>
        /// <param name="pDetails">The p details.</param>
        /// <param name="CallBack">The call back.</param>
        public void RemoveItems(List<T> pDetails, Action<List<T>> CallBack)
        {
            if (pDetails?.Count > 0)
            {
                MyRecordsRWLock.RunUnderWriteLock(() => // lock (MyRecordsLock)
                {
                    foreach (T detail in pDetails)
                    {
                        if (detail.cdeMID != Guid.Empty)
                        {
                            TryRemoveItemInternal(detail.cdeMID.ToString(), out _);
                        }
                    }
                });
                //LOCK-REVIEW: Should this include SaveCacheToDisk for consitency? -> No because SaveCacheToDisk takes it's own lock to ensure consistency
                if (IsCachePersistent)
                    SaveCacheToDisk(false, true);
            }
            CallBack?.Invoke(pDetails);
        }

        /// <summary>
        /// Does the add item.
        /// </summary>
        /// <param name="pKey">The p key.</param>
        /// <param name="pValue">The p value.</param>
        /// <param name="DoRemove">if set to <c>true</c> [do remove].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private bool DoAddItem(Guid pKey, T pValue, bool DoRemove)
        {
            return DoAddItem(pKey.ToString(), pValue, DoRemove);
        }
        /// <summary>
        /// Blob and MyEngines
        /// </summary>
        /// <param name="pStringKey">The p string key.</param>
        /// <param name="pValue">The p value.</param>
        /// <param name="DoRemove">if set to <c>true</c> [do remove].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private bool DoAddItem(string pStringKey, T pValue, bool DoRemove)
        {
            bool Success = TryAddInternal(pStringKey, pValue);
            if (Success && DoRemove)
                RemoveRetired();
            return Success;
        }

        /// <summary>
        /// Updates the item.
        /// </summary>
        /// <param name="pDetails">The p details.</param>
        /// <param name="CallBack">The call back.</param>
        public void UpdateItem(T pDetails, Action<List<T>> CallBack)
        {
            if (pDetails == null) return;
            List<T> tList = new () { pDetails };
            UpdateItems(tList, CallBack);
        }
        /// <summary>
        /// Updates the items.
        /// </summary>
        /// <param name="pItems">The p items.</param>
        /// <param name="CallBack">The call back.</param>
        public void UpdateItems(List<T> pItems, Action<List<T>> CallBack)
        {
            bool success = false;
            MyRecordsRWLock.RunUnderWriteLock( () =>
            {
                foreach (T t in pItems)
                {
                    success = TryUpdateItemInternal(t.cdeMID.ToString(), t);
                    if (!success)
                    {
                        success = DoAddItem(t.cdeMID, t, false);
                    }
                }
                RemoveRetired();
            });
            if (IsCachePersistent)
                SaveCacheToDisk(false, true);
            if (success)
            {
                CallBack?.Invoke(pItems);
                NotifyOfUpdate(pItems);
            }
        }

        /// <summary>
        /// Blob and MyEngines
        /// </summary>
        /// <param name="pStrKey">The p string key.</param>
        /// <param name="pDetails">The p details.</param>
        /// <param name="CallBack">The call back.</param>
        public void AddOrUpdateItemKey(string pStrKey, T pDetails, Action<T> CallBack)
        {
            bool success = false;
            MyRecordsRWLock.RunUnderWriteLock(() =>  //  lock (MyRecordsLock)
            {
                success = TryUpdateItemInternal(pStrKey, pDetails);
                if (!success)
                {
                    success = DoAddItem(pStrKey, pDetails, true);
                }
            });
            if (success)
            {
                if (IsCachePersistent)
                    SaveCacheToDisk(false, true);
                CallBack?.Invoke(pDetails);
                NotifyOfUpdate(new List<T> { pDetails });
            }
        }


        /// <summary>
        /// Updates the Guid identifier.
        /// </summary>
        /// <param name="pOld">The p old.</param>
        /// <param name="pNew">The p new.</param>
        /// <returns>T.</returns>
        public T UpdateID(Guid pOld, Guid pNew)
        {
            T item = GetEntryByID(pOld);
            if (pOld == pNew) return item;
            if (item != null)
            {
                item.cdeMID = pNew;
                AddAnItem(item, null);
                RemoveAnItemByID(pOld, null);
            }
            return item;
        }

        /// <summary>
        /// Updates the string identifier.
        /// </summary>
        /// <param name="pOld">The p old.</param>
        /// <param name="pNew">The p new.</param>
        /// <returns>T.</returns>
        public T UpdateID(string pOld, string pNew)
        {
            T item = GetEntryByID(pOld);
            if (pOld == pNew) return item;
            if (item != null)
            {
                AddOrUpdateItemKey(pNew, item, null);
                RemoveAnItemByKey(pOld, null);
            }
            return item;
        }

        /// <summary>
        /// Blob and MyEngines
        /// </summary>
        /// <param name="pKey">The p key.</param>
        /// <param name="CallBack">The call back.</param>
        public void RemoveAnItemByID(Guid pKey, Action<T> CallBack)
        {
            RemoveAnItemByKey(pKey.ToString(), CallBack);
        }

        /// <summary>
        /// Removes the items by key list.
        /// </summary>
        /// <param name="pStrKeys">The p string keys.</param>
        /// <param name="CallBack">The call back.</param>
        /// <returns>T.</returns>
        public List<T> RemoveItemsByKeyList(List<string> pStrKeys, Action<T> CallBack)
        {
            T o = null;
            var removedItems = new List<T>();
            MyRecordsRWLock.RunUnderWriteLock(() =>
            {
                foreach (string tKey in pStrKeys)
                {
                    if (TryRemoveItemInternal(tKey, out o))
                    {
                        removedItems.Add(o);
                    }
                }
            });
            if (IsCachePersistent)
                SaveCacheToDisk(false, true);
            if (removedItems.Count > 0)
            {
                if (CallBack != null)
                {
                    foreach (var item in removedItems)
                    {
                        CallBack(item);
                    }
                }
                NotifyOfUpdate(removedItems);
            }
            return removedItems;
        }

        /// <summary>
        /// Removes an item by key.
        /// </summary>
        /// <param name="pStrKey">The p string key.</param>
        /// <param name="CallBack">The call back.</param>
        /// <returns>T.</returns>
        public T RemoveAnItemByKey(string pStrKey, Action<T> CallBack)
        {
            T o = null;
            bool success = false;
            MyRecordsRWLock.RunUnderWriteLock(() =>
            {
                success = TryRemoveItemInternal(pStrKey, out o);
            });
            if (IsCachePersistent && success)
                SaveCacheToDisk(false, true);
            if (success)
            {
                CallBack?.Invoke(o);
                NotifyOfUpdate(new List<T> { o });
            }
            else
                success = false;
            return o;
        }

        private readonly object lockRemoveExpired = new();
        /// <summary>
        /// Removes an item.
        /// </summary>
        /// <param name="pDetails">The p details.</param>
        /// <param name="CallBack">The call back.</param>
        public void RemoveAnItem(T pDetails, Action<List<T>> CallBack)
        {
            List<T> tList = new()
            {
                pDetails
            };
            RemoveItems(tList, CallBack);
        }
        /// <summary>
        /// Removes an item.
        /// </summary>
        /// <param name="pSelector">The p selector.</param>
        public void RemoveAnItem(T pSelector)
        {
            MyRecordsRWLock.RunUnderUpgradeableReadLock(() =>
            {
                List<string> keysToRemove = MyRecords.Where(s => s.Value == pSelector)
                            .Select(kvp => kvp.Key)
                            .ToList();
                foreach (string key in keysToRemove)
                {
                    MyRecordsRWLock.RunUnderWriteLock(() =>
                    {
                        TryRemoveItemInternal(key, out T tRemoved);
                    });
                }
            });
            if (IsCachePersistent)
                SaveCacheToDisk(false, true);
        }

        /// <summary>
        /// Removes the expired.
        /// </summary>
        /// <param name="notused">unused state</param>
        internal void RemoveExpired(object notused)
        {
            if (MyRecords!=null && !TheCommonUtils.cdeIsLocked(lockRemoveExpired) && MyRecords.Any())
            {
                lock (lockRemoveExpired)
                {
                    var tNow = DateTimeOffset.Now;//Otherwise it will be called for every record in Linq again
                    try
                    {
                        var tList = MyRecords.Where(p => (p.Value.cdeEXP>0 && tNow.Subtract(p.Value.cdeCTIM).TotalSeconds>p.Value.cdeEXP)).ToList();
                        if (tList.Count == 0) return;
                        TheBaseAssets.MySYSLOG.WriteToLog(4805, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", string.Format("SQL {2} Expired Record Removed: {0} of {1}", tList.Count, MyRecords.Count, typeof(T)), eMsgLevel.l6_Debug));
                        if (eventRecordExpired != null)
                        {
                            foreach (var s in tList)
                            {
                                eventRecordExpired?.Invoke(s.Value);
                            }
                        }
                        RemoveItemsByKeyList(tList.Select(x => x.Key).ToList(), null);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }
        /// <summary>
        /// Removes Retired/Stale records older then the given amount in Seconds
        /// </summary>
        /// <param name="pExpiredSeconds">The p expired seconds.</param>
        internal void RemoveRetired(int pExpiredSeconds)
        {
            if (MyRecords != null)
            {
                try
                {
                    DateTimeOffset tNow = DateTimeOffset.Now; //Otherwise it will be called for every record in Linq again
                    var tList = MyRecords.Where(p => p.Value.cdeCTIM < tNow.Subtract(new TimeSpan(0, 0, pExpiredSeconds))).ToList();
                    if (tList.Count == 0) return;
                    TheBaseAssets.MySYSLOG.WriteToLog(4806, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("TheMirrorCache", string.Format("SQL {2} Retired Record Removed: {0} of {1}", tList.Count, MyRecords.Count, typeof(T)), eMsgLevel.l6_Debug));
                    if (eventRecordExpired != null)
                    {
                        foreach (var s in tList)
                        {
                            eventRecordExpired?.Invoke(s.Value);
                        }
                    }
                    TheCommonUtils.cdeRunAsync("ExpireRecords", true, (o) => RemoveItemsByKeyList(tList.Select(x => x.Key).ToList(), null));
                }
                catch
                {
                    // ignored
                }
            }
        }
        /// <summary>
        /// TODO: Support for real stores as well - Currently only for RAMStores
        /// </summary>
        internal void RemoveRetired()
        {
            if (MaxStoreSize == 0) return; //LOCK-REVIEW: We do not want to do this if there is a lock aready here (logic rather then read/write) MUST NOT CHECK FOR:  || MyRecordsRWLock.IsLocked() <= thi is NEVER false!
            if (Count > MaxStoreSize)
            {
                int toDel = Count - MaxStoreSize;
                List<T> tList;
                MyRecordsRWLock.RunUnderUpgradeableReadLock(() =>  //LOCK-REVIEW: Used for consitency not read/write. write-Lock in RemoveItems. but created here as Crash was seen in linq on slow IPL Win7.NET4 devices
                {
                    if (recordsBySequenceNumber != null)
                    {
                        tList = recordsBySequenceNumber.Where(item => item != null && item.cdeEXP >= 0).Take(toDel).ToList(); // CODE REVIEW if cdeEXP < 0 the item is not removed from the cache due to size or time expiration (needed for Historian BaseLine snapshots)
                    }
                    else
                    {
                        // CODE REVIEW: This is inefficient for large caches, especially since it gets called with each newly added item once the threshold is reached
                        tList = MyRecords.Values.OrderBy(s => s.cdeCTIM).Take(toDel).ToList();
                    }
                    RemoveItems(tList, null);
                });
            }
        }

        /// <summary>
        /// Adds the or update item.
        /// </summary>
        /// <param name="pKey">The p key.</param>
        /// <param name="pDetails">The p details.</param>
        /// <param name="CallBack">The call back.</param>
        public void AddOrUpdateItem(Guid pKey, T pDetails, Action<T> CallBack)
        {
            AddOrUpdateItemKey(pKey.ToString(), pDetails, CallBack);
        }
        /// <summary>
        /// Adds the or update item.
        /// </summary>
        /// <param name="pDetails">The p details.</param>
        public void AddOrUpdateItem(T pDetails)
        {
            MyRecordsRWLock.RunUnderWriteLock(() =>
            {
                if (MyRecords.ContainsKey(pDetails.cdeMID.ToString()))
                    MyRecords[pDetails.cdeMID.ToString()] = pDetails;
                else
                    AddAnItem(pDetails, null);
            });
            if (IsCachePersistent)
                SaveCacheToDisk(false, true);
        }

        ///// <summary>
        ///// Gets the first item.
        ///// </summary>
        ///// <param name="key">The key.</param>
        ///// <returns>T.</returns>
        //[Obsolete("There is no notion of a first item in a storage mirror cache.")]
        //public T GetFirstItem(out Guid key)
        //{
        //    //CODE-REVIEW: This function assumes that the key is a GUID which is NOT garanteed!
        //    T tRes = default;
        //    var keyLocal = Guid.Empty;
        //    MyRecordsRWLock.RunUnderReadLock(() =>
        //    {
        //        if (MyRecords.Values.Any())
        //        {
        //            var t = MyRecords.First();
        //            tRes = t.Value;
        //            keyLocal = TheCommonUtils.CGuid(t.Key);
        //        }
        //    });
        //    key = keyLocal;
        //    return tRes;
        //}

        /// <summary>
        /// Gets the entry by identifier.
        /// </summary>
        /// <param name="strID">The string identifier.</param>
        /// <returns>T.</returns>
        public T GetEntryByID(string strID)
        {
            T tRes = null; 
            MyRecordsRWLock.RunUnderReadLock(() =>
            {
                MyRecords.TryGetValue(strID, out tRes);
            });
            return tRes;
        }
        /// <summary>
        /// Gets the entry by identifier.
        /// </summary>
        /// <param name="ID">The identifier.</param>
        /// <returns>T.</returns>
        public T GetEntryByID(Guid ID)
        {
            T tRes = null; 
            MyRecordsRWLock.RunUnderReadLock(() => //LOCK-REVIEW: This is a highly contentious lock (i.e. TheQueudSender.SendQueued). Consider a reader-writer lock here!
            {
                MyRecords.TryGetValue(ID.ToString(), out tRes);
            });
            return tRes;
        }

        /// <summary>
        /// Determines whether [contains by function] [the specified p selector].
        /// </summary>
        /// <param name="pSelector">The p selector.</param>
        /// <returns><c>true</c> if [contains by function] [the specified p selector]; otherwise, <c>false</c>.</returns>
        public bool ContainsByFunc(Func<T, bool> pSelector)
        {
            return MyRecords.Values.Any(pSelector);
        }

        /// <summary>
        /// Gets the entry by function.
        /// </summary>
        /// <param name="pSelector">The p selector.</param>
        /// <returns>T.</returns>
        public T GetEntryByFunc(Func<T, bool> pSelector)
        {
            T tRes = default;
            MyRecordsRWLock.RunUnderReadLock(() =>
            {
                try
                {
                    tRes = MyRecords.Where(kv => pSelector(kv.Value))?.Select(kv => kv.Value)?.FirstOrDefault();
                }
                catch (Exception)
                {
                    // ignored
                }
            });
            return tRes;
        }
        /// <summary>
        /// Gets the entries by function.
        /// </summary>
        /// <param name="pSelector">The p selector.</param>
        /// <returns>List&lt;T&gt;.</returns>
        public List<T> GetEntriesByFunc(Func<T, bool> pSelector)
        {
            List<T> tRes = null; 
            MyRecordsRWLock.RunUnderReadLock(() =>
            {
                if (pSelector == null)
                    tRes = MyRecords.Select(v => v.Value)?.ToList();
                else
                    tRes = MyRecords.Where(kv => pSelector(kv.Value))?.Select(kv => kv.Value)?.ToList();
            });
            return tRes;
        }
        /// <summary>
        /// Notifies the of update.
        /// </summary>
        /// <param name="pUpdatedRecord">The p updated record.</param>
        public void NotifyOfUpdate(List<T> pUpdatedRecord)
        {
            if (HasUpdates != null && AllowFireUpdates)
                HasUpdates(pUpdatedRecord);
        }

        /// <summary>
        /// Retrieves the appropriate records from the cache file on disk. This is used for AppendOnly StorageMirrors.
        /// </summary>
        /// <returns>StoreReponse object containing the records retrieved from disk</returns>
        internal TheStorageMirror<T>.StoreResponse GetRecordsFromDisk(string filter, string orderBy, int pageNumber, int topRecords)
        {
            TheStorageMirror<T>.StoreResponse tResponse = new ();
            MyRecordsRWLock.RunUnderReadLock(() =>
            {
                try
                {
                    List<string> cacheFiles = new ();
                    if (MaxCacheFileSize > 0)
                    {
                        DirectoryInfo di = new (TheCommonUtils.cdeFixupFileName("cache\\", true));
                        FileInfo[] fileInfo = di.GetFiles($"{MyStoreID.Replace('&', 'n')}*");
                        if (fileInfo.Length > 0)
                        {
                            List<FileInfo> orderedFileInfo = fileInfo.OrderBy(s => s.CreationTime).ToList();
                            foreach (var info in orderedFileInfo)
                                cacheFiles.Add(info.FullName);
                        }
                    }
                    else
                        cacheFiles.Add(TheCommonUtils.cdeFixupFileName(string.Format("cache\\{0}", MyStoreID.Replace('&', 'n')), true));
                    List<T> records = new ();
                    foreach (string cacheFile in cacheFiles)
                    {
                        if (!TheCommonUtils.IsNullOrWhiteSpace(filter))
                        {
                            List<SQLFilter> filterList = TheStorageUtilities.CreateFilter(filter);
                            Func<T, bool> deleg = ExpressionBuilder.GetExpression<T>(filterList).Compile();

                            // Avoids loading entire file into memory at once if possible
                            using (FileStream fileStream = new (cacheFile, FileMode.Open, FileAccess.Read))
                            using (StreamReader streamReader = new (fileStream))
                            using (JsonTextReader jsonReader = new (streamReader))
                            {
                                if (fileStream.Length > 0)
                                {
                                    JsonSerializer serializer = new ();
                                    jsonReader.Read();
                                    if (jsonReader.TokenType == JsonToken.StartArray)
                                    {
                                        while (jsonReader.Read())
                                        {
                                            T record = null;
                                            if (jsonReader.TokenType == JsonToken.StartObject)
                                            {
                                                record = serializer.Deserialize<T>(jsonReader);

                                            }
                                            else if (jsonReader.TokenType == JsonToken.String && IsCacheEncrypted)
                                            {
                                                string encryptedItem = serializer.Deserialize<string>(jsonReader);
                                                byte[] itemAsBytes = Convert.FromBase64String(encryptedItem);
                                                string decryptedItem = TheCommonUtils.Decrypt(itemAsBytes, TheBaseAssets.MySecrets.GetNodeKey());
                                                record = TheCommonUtils.DeserializeJSONStringToObject<T>(decryptedItem);
                                            }
                                            if (deleg != null && record != null)
                                            {
                                                if (deleg.Invoke(record))
                                                    records.Add(record);
                                                else
                                                    jsonReader.Skip();
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    tResponse.HasErrors = true;
                                    tResponse.ErrorMsg = "Cache file is empty";
                                }
                            }
                        }
                        else
                        {
                            byte[] content = File.ReadAllBytes(cacheFile);
                            string jsonContent = System.Text.Encoding.Default.GetString(content);
                            if (IsCacheEncrypted)
                            {
                                List<string> encryptedItems = TheCommonUtils.DeserializeJSONStringToObject<List<string>>(jsonContent);
                                foreach (string item in encryptedItems)
                                {
                                    byte[] itemAsBytes = Convert.FromBase64String(item);
                                    string decryptedItem = TheCommonUtils.Decrypt(itemAsBytes, TheBaseAssets.MySecrets.GetNodeKey());
                                    T record = TheCommonUtils.DeserializeJSONStringToObject<T>(decryptedItem);
                                    records.Add(record);
                                }
                            }
                            // No filter defined, so load and deserialize the file
                            else
                                records.AddRange(TheCommonUtils.DeserializeJSONStringToObject<List<T>>(jsonContent));
                        }
                    }

                    if (records.Count > 0)
                    {
                        switch (orderBy)
                        {
                            case "cdeCTIM desc":
                                records = records.OrderByDescending(s => s.cdeCTIM).ToList();
                                break;
                            case "cdeCTIM":
                            case "cdeCTIM asc":
                                records = records.OrderBy(s => s.cdeCTIM).ToList();
                                break;
                        }
                        if (topRecords > 0 && records.Count > topRecords)
                        {
                            if (pageNumber > 0 && topRecords * pageNumber > records.Count)
                                pageNumber--;
                            if (pageNumber < 0)
                            {
                                pageNumber = TheCommonUtils.CInt(records.Count / topRecords);
                            }
                            records = pageNumber > 0 ? records.Skip(topRecords * pageNumber).Take(topRecords).ToList() : records.Take(topRecords).ToList();
                        }
                        tResponse.MyRecords = records;
                    }
                    else
                    {
                        tResponse.HasErrors = true;
                        tResponse.ErrorMsg = "No records found that match the criteria in SQL Filter :" + filter;
                    }
                }
                catch (OutOfMemoryException e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(478, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("StorageMirror", "GetRecords failed - Cache file is too large to load", eMsgLevel.l2_Warning, e.ToString()));
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(479, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("StorageMirror", "GetRecords failed", eMsgLevel.l2_Warning, e.ToString()));
                }
            });
            tResponse.SQLOrder = orderBy;
            tResponse.SQLFilter = filter;
            tResponse.PageNumber = pageNumber;
            return tResponse;
        }

    }

    public interface IReaderWriterLock
    {
        void RunUnderReadLock(Action action);
        void RunUnderWriteLock(Action action);
        void RunUnderUpgradeableReadLock(Action action);
        void RunUnderUpgradeableReadLock(Func<object> action, Action<object> writeAction);
        bool IsLocked();
        bool IsReadLocked();
        bool IsUpgradeableReadLocked();
        bool IsWriteLocked();
    }

    internal class TheMirrorCacheReaderWriterLock<T> : IReaderWriterLock where T : TheDataBase, INotifyPropertyChanged, new()
    {
        public static readonly TimeSpan defaultTimeout = new (0, 0, 30);
        readonly ReaderWriterLockSlim _rwLock;
        readonly TheMirrorCache<T> _cache;
        TimeSpan _timeout;
        bool _hasUsedUpgradebleMode;
        readonly bool DoLog = false;
        public TheMirrorCacheReaderWriterLock(TheMirrorCache<T> cache)
        {
            _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            _cache = cache;
            _timeout = defaultTimeout;
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
            if (_rwLock.TryEnterUpgradeableReadLock(0)) //NOSONAR There is no code here that could throw, so no need to add a try/finally around the ExitUpgradeableReadLock call
            {
                _rwLock.ExitUpgradeableReadLock();
                return false;
            }
            return true;
        }
        public bool IsWriteLocked()
        {
            if (_rwLock.IsWriteLockHeld)
            {
                return false; // This thread is holding the write lock
            }
            if (_rwLock.TryEnterWriteLock(0))  //NOSONAR There is no code here that could throw, so no need to add a try/finally around the ExitWriteLock call
            {
                _rwLock.ExitWriteLock();
                return false;
            }
            return true;
        }
    }
}
