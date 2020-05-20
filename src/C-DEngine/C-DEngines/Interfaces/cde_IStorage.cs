// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.StorageService.Model;

namespace nsCDEngine.Engines.StorageService
{
    /// <summary>
    /// Storage Commands
    /// </summary>
    public enum eSCMD
    {
        /// <summary>
        /// Reads from Storage
        /// </summary>
        Read = 0,
        /// <summary>
        /// Update Storage
        /// </summary>
        Update = 1,
        /// <summary>
        /// Inserts into Storage
        /// </summary>
        Insert = 2,
        /// <summary>
        /// Insert if it cannot update
        /// </summary>
        InsertOrUpdate = 3,
        /// <summary>
        /// Executes
        /// </summary>
        Execute = 4,
        /// <summary>
        /// Creates the Store if it does not exist and then inserts the record
        /// </summary>
        CreateInsert =5,
    }

    /// <summary>
    /// Main Interface for Storage Service
    /// This allows interaction with the Storage Service directly without using the StorageMirror container using light-weight calls
    /// </summary>
    public interface IStorageService
    {
        /// <summary>
        /// Gets the IBaseEngine Interface of the Storage service for Base-Engine calls
        /// </summary>
        /// <returns></returns>
        IBaseEngine GetBaseEngine();


        /// <summary>
        /// Initializes the EdgeStore
        /// </summary>
        /// <returns></returns>
        bool InitEdgeStore();

        /// <summary>
        /// Call to Create a new store (a Table in the SQL Server) in the Storage Service
        /// </summary>
        /// <param name="MyType">The Type of the class you want to store</param>
        /// <param name="pDefaults">An instance of the MyType with default data to store in the table if a new table is created</param>
        /// <param name="pDeviceName">The Name of the Device that owns this store</param>
        /// <param name="StoreDescription">A friendly description of the data to be stored</param>
        /// <param name="ResetContent"></param>
        /// <param name="CallBack">This callback is called when the call is finished and contains a TSM with the result. The TXT parameter contains "DATARETREIVED:" and the PLS contains a serialized "TheDataRetreivalRecord"</param>
        /// <param name="pTableName">Custom TableName if required</param>
        void EdgeDataCreateStore(Type MyType, object pDefaults, string pDeviceName, string StoreDescription,bool ResetContent, Action<TSM> CallBack, string pTableName);
        /// <summary>
        /// Very light-weight call to store a class into the storage service.
        /// If the store does not exist, this call will not do anything. EdgeDataCreateStore has to be called first with the type of this class
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pMyValue">Class Type of the store that will be stored</param>
        /// <param name="pTableName">Custom TableName if required</param>
        void EdgeDataStoreOnly<T>(T pMyValue, string pTableName);
        /// <summary>
        /// This call gives a bit more control over the storing than "EdgeCreateStore".
        /// </summary>
        /// <typeparam name="T">The Type (class) of data to be stored</typeparam>
        /// <param name="MyValue">A Dictionary of all values to be stored. The key is a string that will be used as the unique identifier of the record. The key but must be in a valid Guid format. Currently we cannot use GUID as the key in a generic dictionary due to a limitation of Mono on IOS</param>
        /// <param name="pCMD">Tells the StorageService what to do with the data</param>
        /// <param name="MagicID">Some Cookie information to be passed with the call</param>
        /// <param name="CallBack">This callback is called when the call is finished and contains a TSM with the result. The TXT parameter contains "DATARETREIVED:" and the PLS contains a serialized "TheDataRetreivalRecord"</param>
        /// <param name="pTableName">Custom TableName if required</param>
        void EdgeDataStore<T>(Dictionary<string, T> MyValue, eSCMD pCMD, string MagicID, Action<TSM> CallBack, string pTableName);
        /// <summary>
        /// This is the main function to retrieve data from the StorageService.
        /// </summary>
        /// <param name="MyClass">Type of class to be expected</param>
        /// <param name="pColFilter">Filter (Separated by ;) of colums (Class-Fields) that are expected back</param>
        /// <param name="pTopRows">TOP Clause for SQL Servers</param>
        /// <param name="PageNumber">Page of data requested. i.e TOP=30 and Page=4 returns recordes 150-179 according to filter and ordering</param>
        /// <param name="pSQLFilter">SQL Filter for the query. Each class field can be used in this filter</param>
        /// <param name="pSQLOrder">Fieldname (or names separated by ,) to specify the order for the returning data</param>
        /// <param name="pGrouping">Group by clause for the query</param>
        /// <param name="pMagicID">Cookie to be passed with the Query</param>
        /// <param name="pCallBack">This callback is called when the call is finished and contains a TSM with the result. The TXT parameter contains "DATARETREIVED:" and the PLS contains a serialized "TheDataRetreivalRecord"</param>
        /// <param name="LocalCallBackOnly">If this value is set to true, no request is sent to a remote StorageService.</param>
        /// <param name="pTableName">Custom TableName if required</param>
        void RequestEdgeStorageData(Type MyClass, string pColFilter, int pTopRows, int PageNumber, string pSQLFilter, string pSQLOrder, string pGrouping, string pMagicID, Action<TSM> pCallBack, bool LocalCallBackOnly, string pTableName);
        /// <summary>
        /// This is the main function to retrieve data from the StorageService.
        /// </summary>
        /// <param name="pUniqueID">A string representation of the class calculated by the StorageService - if you use this function with a StorageMirror that has a fixed TableName, you need to set it here</param>
        /// <param name="ColFilter">Filter (Separated by ;) of colums (Class-Fields) that are expected back</param>
        /// <param name="pTopRows">TOP Clause for SQL Servers</param>
        /// <param name="PageNumber">Page of data requested. i.e TOP=30 and Page=4 returns recordes 150-179 according to filter and ordering</param>
        /// <param name="pSQLFilter">SQL Filter for the query. Each class field can be used in this filter</param>
        /// <param name="pSQLOrder">Fieldname (or names separated by ,) to specify the order for the returning data</param>
        /// <param name="pGrouping">Group by clause for the query</param>
        /// <param name="pMagicID">Cookie to be passed with the Query</param>
        /// <param name="pCallBack">This callback is called when the call is finished and contains a TSM with the result. The TXT parameter contains "DATARETREIVED:" and the PLS contains a serialized "TheDataRetreivalRecord"</param>
        /// <param name="LocalCallBackOnly">If this value is set to true, no request is sent to a remote StorageService.</param>
        void RequestEdgeStorageData(string pUniqueID, string ColFilter, int pTopRows, int PageNumber, string pSQLFilter, string pSQLOrder, string pGrouping, string pMagicID, Action<TSM> pCallBack, bool LocalCallBackOnly);
        /// <summary>
        /// Executes a SQL Command against the StorageService SQL Server. BE VERY CAREFULL! Wrong calls can cause data loss. Currently this call only allowes to be called against a table that was created and owned by this node
        /// </summary>
        /// <param name="MyClass">Class specifying the table to be addressed</param>
        /// <param name="SQLExec">Valid TSQL code that can be "Executed" against the SQL Server</param>
        /// <param name="ColFilter"></param>
        /// <param name="MagicID">Cookie to be passed with the Query</param>
        /// <param name="CallBack">This callback is called when the call is finished and contains a TSM with the result. The TXT parameter contains "DATARETREIVED:" and the PLS contains a serialized "TheDataRetreivalRecord"</param>
        /// <param name="pTableName">Custom TableName if required</param>
        void EdgeStorageExecuteSql(Type MyClass, string SQLExec,string ColFilter, string MagicID, Action<TSM> CallBack, string pTableName);
        /// <summary>
        /// Updates a store definition in the Storage Service
        /// </summary>
        /// <param name="MyClass">Class specifying the store definition to be updated</param>
        /// <param name="pDeviceName">The new name of the Device that owns this store</param>
        /// <param name="StoreDescription">Friendly description of the store</param>
        /// <param name="CallBack">This callback is called when the call is finished and contains a TSM with the result. The TXT parameter contains "DATAUPDATED:" and the PLS contains a serialized "TheDataRetrievalRecord"</param>
        /// <param name="pTableName">Table name specifying the store definition to be updatee</param>
        void EdgeUpdateStore(Type MyClass, string pDeviceName, string StoreDescription, Action<TSM> CallBack, string pTableName);
        /// <summary>
        /// Requests the Storage Capabilities of a remote StorageService
        /// </summary>
        /// <param name="pMagicID">Cookie to be passed with the Query</param>
        /// <param name="pCallBack">This callback is called when the call is finished and contains a TSM with the result. The TXT parameter contains "CDE_GETEDGECAPSRESULTS:" and the PLS contains a serialized "TheStorageCaps"</param>
        void EdgeStorageServerRequestCaps(string pMagicID, Action<TSM> pCallBack);
        /// <summary>
        /// Returns the Storage Capabilities of the this StorageService
        /// </summary>
        /// <returns></returns>
        TheStorageCaps GetStorageCaps();
    }
}
