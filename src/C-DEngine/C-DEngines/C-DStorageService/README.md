<!--
SPDX-FileCopyrightText: Copyright (c) 2013-2020 TRUMPF Laser GmbH, authors: C-Labs

SPDX-License-Identifier: MPL-2.0
-->

﻿# TheStorageMirror & TheMirrorCache
## The AppendOnly flag
When creating a `TheStorageMirror` instance, you can assign to a boolean property titled `AppendOnly` to disable storage of any records in RAM and simply append to the cache file.
This means that instead of "saving" and rewriting the entire cache to disk at specified intervals, each write will add a record (as a JSON object) onto the end of the JSON array in the file.

### Creation
Use the following template to create a `TheStorageMirror` instance and enable `AppendOnly`:

```
// Where T : TheDataBase
TheStorageMirror<T> myStorageMirror = new TheStorageMirror<T>(TheCDEngines.MyIStorageService) { };
TheStorageMirrorParameters parameters = new TheStorageMirrorParameters()
{
    Description = "Test StorageMirror for AppendOnly",
    CacheTableName = "MyStorageMirrorCache",
    FriendlyName = "My Storage Mirror",
    AppendOnly = true,
    MaxCacheFileSize = 2000,
    MaxCacheFileCount = 5,
    IsCacheEncrypted = false,
    TrackInsertionOrder = false
};
myStorageMirror.CreateStore(parameters, null);
```

Note that setting `AppendOnly` to true will also set `IsCached` to true, `IsCachePersistent` to false, and `IsRAMStore` to false, as these are the necessary conditions to allow appending.
In addition, setting the `MaxCacheFileSize` and `MaxCacheFileCount` will place limits on the storage of records.  In the case above, cache files can only grow to 2000 KB, or 2 MB, before a new one is created.
Our limits here also only allow 5 cache files in place at a time.  Once a 6th file is created, the oldest file will be deleted.

**Encryption**

If you wish to encrypt the contents of your cache, you can do so by setting the `IsCacheEncrypted` flag to true.  (In the example above, it is set to false).
With `AppendOnly`, the cache file is then written as a JSON array of encrypted strings.  Note that encrypting the cache may have certain performance impacts when writing or reading a large number of records.

**TrackInsertionOrder**

In order to keep track of the sequence in which records were inserted to the StorageMirror, `TrackInsertionOrder` can be enabled.  The `StorageMirrorThingUpdateStream` uses this
to keep a running order order of property updates on a Thing.  Enabling this will now keep records in the `TheMirrorCache.recordsBySequenceNumber` but not `TheMirrorCache.MyRecords`.
This provides more functionality than just `AppendOnly` by itself - see `StorageMirrorThingUpdateStream` and `IThingStream` in the C-DEngine.
To turn this on, set the `TrackInsertionOrder` flag to true when creating the StorageMirror, or do the following before calling `CreateStore`:
```
myStorageMirror.TrackInsertionOrder();
```

### Writing
To write records and begin appending to the cache file, you can call either the `AddItem` or `AddItems` method as follows:
```
// Where myItem, item1, item2, and item3 are of some type T : TheDataBase
myStorageMirror.AddAnItem(myItem);
// OR
myStorageMirror.AddItems(new List<T> { item1, item2, item3});
```
These methods will ultimately check if `AppendOnly` is true, and if so, call the `TheMirrorCache.AppendRecordToDisk` method.  This then calls `TheMirrorCache.DoAppendRecordToDisk` which completely bypasses adding any records in memory to `TheMirrorCache.MyRecords`.

**WaitForSave/SaveSync**
When calling `AppendRecordToDisk` directly, you can specify two additional parameters called `waitForSave` and `saveSync`.  If `waitForSave` is set to true, a new append will be initiated even if a current append is ongoing.  The append will wait for the `CacheStoreInterval` before proceeding. Otherwise, no additional append will be performed.
If `saveSync` is set to true, the call will not return until the data has been appended to disk. Otherwise, the append will happen asynchronously.
The default for `waitForSave` is false, while the default for `saveSync` is true.

### Reading
Reading records from storage can still be done if `AppendOnly` is true.  However, as the cache files grow in size, it is important to fine tune the queries when possible with filters, as the results must be loaded into memory.
This can be achieved through the `TheStorageMirror.GetRecords` method.  For example:
```
// Queries the top 50 records with FriendlyName equal to "Test" in descending order by cdeCTIM
myStorageMirror.GetRecords("FriendlyName=Test", 50, 0, "cdeCTIM desc", StoreCallback, false, false, null);
```
Note that if the `SQLFilter` parameter is provided, `TheStorageMirror` will iterate through the records in the file(s) and only add to memory those that match the filter.
If a filter is not provided, all of the records must be deserialized from the file into memory first.  After one of these steps occurs, the List will then be ordered and the "top" number of records will be returned, with paging taken into account.

### Deleting

If `TrackInsertionOrder` is enabled, records can be deleted from an `AppendOnly` StorageMirror from the beginning using the internal method `RemoveUpToSequenceNumberInternal`.  You can pass in a sequence number as a parameter, and all of the records up until that sequence number will be removed from `TheMirrorCache.recordsBySequenceNumber` and the corresponding cache files.  If `MaxCacheFileSize` and `MaxCacheFileCount` is set, all of the files before the file containing the sequence number are deleted.

In order for this to happen, the StorageMirrorCache keeps track of the starting offset (sequence number) for each cache file that is created in the dictionary `fileOffsets`.