// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
#pragma warning disable 1591 //TODO-B4-OSS: Remove and document


namespace nsCDEngine.Engines.StorageService.Model
{
    /// <summary>
    /// SQL EdgeCommunication
    /// </summary>
    /// <returns></returns>
    public struct TheStorageCaps
    {
        public long StorageSpeed;
        public eRedundancyFactor RedundancyFactor;
        public long StorageCapacity;
        public string ServerURL;

        public static TheStorageCaps GetDefaults()
        {
            TheStorageCaps tCaps = new TheStorageCaps
            {
                RedundancyFactor = eRedundancyFactor.RF_NOREDUNDANCY,
                StorageCapacity = 60,
                StorageSpeed = 1000
            };
            return tCaps;
        }
    }

    public enum eRedundancyFactor
    {
        RF_NOREDUNDANCY = 0,
        RF_RAID1 = 1,
        RF_RAID5 = 2,
        RF_RAID10 = 3,
        RF_MEGARAID = 4
    };

    public class SQLFilter
    {
        public string PropertyName { get; set; }
        public FilterOp Operation { get; set; }
        public object Value { get; set; }
    }

    public enum FilterOp
    {
        Equals,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Contains,
        StartsWith,
        EndsWith
    }

    public class SQLDefinition
    {
        public string ServerName = ".";
        public string UserName = "CDEsa";
        public string Password = "";
        public string DatabaseName = "CDEStorage";
        public int ConnectionRetryWaitSeconds = 50;
        public int ConnectionRetries = 3;
    }

    public class StorageDefinition : TheDataBase
    {
        public string DES;  //Description
        public string UID;  //UniqueID Fingerprint
        public string NAM;  //Store Name
        public int CNT;         // Field Count
        public int[] FTY;    // Field Type
        public string[] FNA;    // Field Name
        public string[] FDE;    // Field Default
        public bool DEF;        // Has Defaults
        public Guid DID;        // DeviceID/NodeID
        public long SID;        // SQL Storage ID - created by SQL Server
    }

    public struct StorageGetRequest
    {
        public string UID;      // UniqueID Fingerprint
        public string CFI;      // Column Filter
        public string SFI;      // SQL Filter
        public string SOR;      // SQL Order
        public string MID;      // Magic ID
        public string GRP;      // Grouping //NEW AFTER Beta1
        public int TOP;         // Top Rows
        public int PAG;         // Page Number  NEW:Changeset 26 - Paging for optimized data viewing
        public Guid DID;        // DeviceID
    }

    /// <summary>
    /// The Field Description class
    /// </summary>
    public class TFD
    {
        /// <summary>
        /// FieldName
        /// </summary>
        public string N;
        /// <summary>
        /// FieldType
        /// </summary>
        public string T;
        /// <summary>
        /// Column Number
        /// </summary>
        public int C;
    }

    /// <summary>
    /// Generic structure to store and retrieve data from the StorageService
    /// </summary>
    public class TheDataRetrievalRecord
    {
        /// <summary>
        /// UniqueID
        /// </summary>
        public string UID;
        /// <summary>
        /// Error Message
        /// </summary>
        public string ERR;
        /// <summary>
        /// Cookie ID
        /// </summary>
        public string MID;
        /// <summary>
        /// SQL Filter (used in a "Where clause")
        /// </summary>
        public string SFI;
        /// <summary>
        /// SQL "Order By" clause
        /// </summary>
        public string SOR;      // SQL Order
        /// <summary>
        /// Column filter to only return/write certain fields in a record
        /// </summary>
        public string CFI;

        /// <summary>
        /// DeviceID/NodeID
        /// </summary>
        public Guid DID;
        /// <summary>
        /// Grouping Setting (use by "Group By")
        /// </summary>
        public string GRP;
        /// <summary>
        /// Page number requested. -1 is the last page
        /// </summary>
        public int PAG;
        /// <summary>
        /// Command to be executed by the StorageService
        /// </summary>
        public eSCMD CMD;       // Storage Commmand
        /// <summary>
        /// List of Field Definitions to store/retrieve
        /// </summary>
        public List<TFD> FLDs;  // Fields
        /// <summary>
        /// List of strings with the records to be retrieved or stored in a generic format
        /// </summary>
        public List<List<string>> RECs; //Records
        /// <summary>
        /// Constructor of the RetrievalRecord
        /// </summary>
        public TheDataRetrievalRecord()
        {
            RECs = new List<List<string>>();
            FLDs = new List<TFD>();
            DID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
        }
    }

    public class TheInsertQueueItem : TheDataBase
    {
        public long RealStoreID { get; set; }
        public TheDataRetrievalRecord DataStoreGramm { get; set; }
        public Action<TSM> LocalCallback { get; set; }
        public string SScopeID { get; set; }
        public bool LocalCallbackOnly { get; set; }
        public Guid DirectTarget { get; set; }
    }
}
