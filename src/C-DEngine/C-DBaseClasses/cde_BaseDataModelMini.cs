// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// The Debug Levels of the C-DEngine
    /// </summary>
    public enum eDEBUG_LEVELS
    {
        /// <summary>
        /// No Debug Information
        /// </summary>
        OFF = 0,
        /// <summary>
        /// Only show essential information
        /// </summary>
        ESSENTIALS = 1,
        /// <summary>
        /// Shows more information
        /// </summary>
        VERBOSE = 2,
        /// <summary>
        /// Shows very detailed information
        /// </summary>
        FULLVERBOSE = 3,
        /// <summary>
        /// Very chatty verbose information - use only with caution since the log can slow down a node
        /// </summary>
        EVERYTHING = 4
    }

    /// <summary>
    /// Message Levels used in the LVL parameter of TSM
    /// </summary>
    public enum eMsgLevel
    {
        /// <summary>
        /// Not set, yet
        /// </summary>
        l0_NotSet = 0,
        /// <summary>
        /// Error
        /// </summary>
        l1_Error = 1,
        /// <summary>
        /// Warning
        /// </summary>
        l2_Warning = 2,
        /// <summary>
        /// Important Message
        /// </summary>
        l3_ImportantMessage = 3,
        /// <summary>
        /// Less important message (this is the default)
        /// </summary>
        l4_Message = 4,
        /// <summary>
        /// Message related to the Host
        /// </summary>
        l5_HostMessage = 5,
        /// <summary>
        /// Debug message
        /// </summary>
        l6_Debug = 6,
        /// <summary>
        /// Debug message created on the host
        /// </summary>
        l7_HostDebugMessage = 7,
        /// <summary>
        /// Amount of different Levels
        /// </summary>
        ALL = 8
    }
}

namespace nsCDEngine.ViewModels
{
    /// <summary>
    /// The cdeHostType is defining what kind of hosting application is used for the C-DEngine
    /// </summary>
    public enum cdeHostType
    {
        /// <summary>
        /// Not set, yet
        /// </summary>
        NOTSET = 0,
        /// <summary>
        /// C-DEngine runs inside an Application (.EXE)
        /// Node can relay information and has WebServer + Outbound (cdeHostType.Relay)
        /// </summary>
        Application = 1,
        /// <summary>
        /// C-DEngine runs inside a Windows Service
        /// Node can relay information and has WebServer + Outbound (cdeHostType.Relay)
        /// </summary>
        Service = 2,
        /// <summary>
        /// C-DEngine runs inside IIS either on a server or in the cloud
        /// Node can relay information and has WebServer + Outbound (cdeHostType.Relay)
        /// </summary>
        IIS = 3,
        /// <summary>
        /// C-DEngine runs inside a Application that runs on a device
        /// The device can only to outbound (has no WebServer) = (cdeHostType.Active)
        /// </summary>
        Device = 4,
        /// <summary>
        /// C-DEngine runs inside a browser (uses the Javascript version of the C-DEngine)
        /// The device can only to outbound (has no WebServer) = (cdeHostType.Active)
        /// </summary>
        Browser = 5,
        /// <summary>
        /// C-DEngine runs inside Silverlight   RETIRED - no more Silverlight support
        /// The device can only to outbound (has no WebServer) = (cdeHostType.Active)
        /// </summary>
        Silverlight = 6,
        /// <summary>
        /// C-DEngine runs inside a Phone (Windows Phone, Android or iOS)
        /// The device can only to outbound (has no WebServer) = (cdeHostType.Active)
        /// </summary>
        Phone = 7,
        /// <summary>
        /// C-DEngine runs inside Windows 8 RT (RETIRED: Use Device instead)
        /// The device can only to outbound (has no WebServer) = (cdeHostType.Active)
        /// </summary>
        Metro = 8,

        /// <summary>
        /// C-DEngine runs inside a small Device (i.e. the Phoenix-Contact eCLR Runtime)
        /// The device has only a mini WebServer - no outbound (cdeHostType.Passive)
        /// </summary>
        Mini = 9,

        /// <summary>
        /// Hosted in ASP.net Core
        /// </summary>
        ASPCore = 10,
    }

    /// <summary>
    /// Web Platform the current Request is coming from
    /// </summary>
    public enum eWebPlatform
    {
        /// <summary>
        /// Desktop PC or other standard browsers
        /// </summary>
        Desktop = 0,
        /// <summary>
        /// Mobile devices (Android, IOS, Windows phone, Blackberry etc)
        /// </summary>
        Mobile = 1,
        /// <summary>
        /// Microsoft Hololens
        /// </summary>
        HoloLens = 2,
        /// <summary>
        /// Microsoft XBox One
        /// </summary>
        XBox = 3,
        /// <summary>
        /// Windows IoT
        /// </summary>
        IoT = 4,
        /// <summary>
        /// Telsa X and S
        /// </summary>
        TeslaXS = 5,
        /// <summary>
        /// Samsung TV
        /// </summary>
        TizenTV=6,
        /// <summary>
        /// Samsung Fridge Family Hub
        /// </summary>
        TizenFamilyHub=7,
        /// <summary>
        /// Bots and other scraper
        /// </summary>
        Bot = 8,
        /// <summary>
        /// Undefined or all platforms
        /// </summary>
        Any = 99,
    }

    /// <summary>
    /// The cdeSenderType impacts how data is transmitted between two nodes
    /// </summary>
    public enum cdeSenderType
    {
        /// <summary>
        /// Not set, yet
        /// </summary>
        NOTSET = 0,
        /// <summary>
        /// Node is a Service and can receive binary zipped messages
        /// </summary>
        CDE_SERVICE = 1,
        /// <summary>
        /// NEW in V4: CustomISB connection
        /// </summary>
        CDE_CUSTOMISB = 2,
        /// <summary>
        /// Node is a phone and can only receive UUEncoded strings
        /// </summary>
        CDE_PHONE = 3,
        /// <summary>
        /// Node is a device and can receive binary zipped messages
        /// </summary>
        CDE_DEVICE = 4,
        /// <summary>
        /// Node is a cloud service and can receive binary zipped messages
        /// </summary>
        CDE_CLOUDROUTE = 5,
        /// <summary>
        /// Node is a backchannel (Response returning after it has made an active REST POST) that can be binary Zipped
        /// </summary>
        CDE_BACKCHANNEL = 6,
        /// <summary>
        /// Node s a Javascript based HTML5 browser and can only receive JSON
        /// </summary>
        CDE_JAVAJASON = 7,
        /// <summary>
        /// Node is the local host and will not send any messages to or from
        /// </summary>
        CDE_LOCALHOST = 8,
        /// <summary>
        /// Node is a mini Node (i.e. PxC eCLR) and has only Limited Functionality
        /// </summary>
        CDE_MINI = 9,
        /// <summary>
        /// Node is simulated only and does not send any messages to or from
        /// </summary>
        CDE_SIMULATION = 10

    }

    /// <summary>
    /// Enum for all supported platform the C-DEngine is running on
    /// </summary>
    public enum cdePlatform
    {
        /// <summary>
        /// Unknown platform
        /// </summary>
        NOTSET = 0,
        /// <summary>
        /// .NET 3.5 32 Bit only
        /// </summary>
        X32_V3 = 1,
        /// <summary>
        /// .NET 4.5 64 Bit
        /// </summary>
        X64_V3 = 2,
        /// <summary>
        /// Any V3 platform
        /// </summary>
        ANY_V3 = 3,
        /// <summary>
        /// Arm V3 platform
        /// </summary>
        ARM_V3 = 4,
        /// <summary>
        /// Mono Runtime
        /// </summary>
        MONO_V3 = 5,
        /// <summary>
        /// Metro Windows 8.1
        /// </summary>
        METRO_V81 = 6,
        /// <summary>
        /// Silverlight
        /// </summary>
        SILVERLIGHT_V5 = 7,
        /// <summary>
        /// Windows Phone 8
        /// </summary>
        WPHONE_V8 = 8,
        /// <summary>
        /// IOS Xamarin Runtime
        /// </summary>
        IOS_V6 = 9,
        /// <summary>
        /// Androd Xamarin Runtime
        /// </summary>
        ANDROID_V4 = 10,
        /// <summary>
        /// Phoenix Contact eCLR Runtime
        /// </summary>
        ECLR_V1 = 11,
        /// <summary>
        /// .NET Core 2.0 and 2.1 Runtime
        /// </summary>
        NETCORE_V20 = 12, // Docker/Linux (not used anymore)
        /// <summary>
        /// .NE Standard 2.0
        /// </summary>
        NETSTD_V20 = 13,
        /// <summary>
        /// .Net 4.0 32 Bit
        /// </summary>
        NETV4_32 = 14,
        /// <summary>
        /// .Net 4.0 64 Bit
        /// </summary>
        NETV4_64 = 15,
        /// <summary>
        /// .NET 4.5 32 Bit
        /// </summary>
        X32_V4 = 16,
        /// <summary>
        /// Max Terminator
        /// </summary>
        MAX = 17
    }

    /// <summary>
    /// The cdeNodeType tells the C-DEngine what licensing Type is used for this node.
    /// Depending on the cdeNodeType certain features of the C-DEngine will be used.
    /// This can be for security and for footprint optimization purposes
    /// </summary>
    public enum cdeNodeType
    {
        /// <summary>
        /// Not set, yet
        /// </summary>
        NOTSET = 0,
        /// <summary>
        /// This Node is a Relay Service and can actively send REST calls and Passively receive Http Requests
        /// </summary>
        Relay = 1,
        /// <summary>
        /// This Node is a Client only. It has no Http Service but can send REST calls to other nodes
        /// </summary>
        Active = 2,
        /// <summary>
        /// This Node is a Device only. It has an Http Service and can receive calls but it cannot sent REST calls to other nodes
        /// </summary>
        Passive = 3,
        /// <summary>
        /// This node is a device and has both active and passive capabilities but it cannot relay to other devices
        /// </summary>
        ActPass = 4
    }

    /// <summary>
    /// Information about the client making a request
    /// </summary>
    public class TheClientInfo
    {
        /// <summary>
        /// LanguageID of the requesting Node/Client
        /// </summary>
		public int LCID { get; set; }
        /// <summary>
        /// User ID Issuing the Request
        /// </summary>
		public Guid UserID { get; set; }
        /// <summary>
        /// True if this request was issued on the first node
        /// </summary>
		public bool IsFirstNode { get; set; }
        /// <summary>
        /// True if the request came over the cloud
        /// </summary>
		public bool IsOnCloud { get; set; }

        /// <summary>
        /// True if the Originator Node or its Security Proxy is trusted
        /// </summary>
		public bool IsTrusted { get; set; }
        /// <summary>
        /// True if the user in this ClientInfo is trusted
        /// </summary>
        public bool IsUserTrusted { get; set; }
        /// <summary>
        /// Needs to be retired
        /// </summary>
        public bool IsMobile { get; set; }
        /// <summary>
        /// WebPlatform of the requesting Client
        /// </summary>
		public eWebPlatform WebPlatform { get; set; }

        /// <summary>
        /// Originating Node ID
        /// </summary>
        public Guid NodeID { get; set; }
        /// <summary>
        /// Originating Form ID (can be Guid.Empty if the request is not coming from the NMI)
        /// </summary>
        public Guid FormID { get; set; }

        /// <summary>
        /// Friendly Output
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{LCID} {UserID} OC:{IsOnCloud} IFN:{IsFirstNode} IsT:{IsTrusted} IsUT:{IsUserTrusted} IsM:{IsMobile}".ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Bare minimum details about TheNodeInfo
    /// </summary>
    public class TheNodeDiagInfo
    {
        /// <summary>
        /// C-DEngine version used by the node
        /// </summary>
        public string CDEVersion { get; set; }
        /// <summary>
        /// Number of services installed on the node
        /// </summary>
        public int ServiceCount { get; set; }

        /// <summary>
        /// Telegrams to subscriptions of that node
        /// </summary>
        public long Telegrams { get; set; }
        /// <summary>
        /// Time the node first started up
        /// </summary>
        public DateTimeOffset StartupTime { get; set; }

        /// <summary>
        /// true if the node uses WebSockets
        /// </summary>
        public bool UsesWS { get; set; }

        /// <summary>
        /// Last HeartBeat
        /// </summary>
        public string LastHB { get; set; }

        /// <summary>
        /// All Plugin Services of the node
        /// </summary>
        public List<string> Services { get; set; }
    }

    /// <summary>
    /// Information about a specific mesh along with details about the node
    /// used to request this information.
    /// </summary>
    public class TheMeshInfo
    {
        /// <summary>
        /// This number includes all nodes in a mesh including localhost and cloudroutes
        /// </summary>
        public int TotalMeshSize { get; set; }
        /// <summary>
        /// Total number of connected "backchannel" nodes in the mesh
        /// </summary>
        public int MeshSize { get; set; }
        /// <summary>
        /// Total number of connected browser nodes in the mesh
        /// </summary>
        public int ConnectedBrowsers { get; set; }
        /// <summary>
        /// Hash of the scope ID used to identify the mesh
        /// </summary>
        public string ScopeIDHash { get; set; }
        /// <summary>
        /// Brief details about the node used to retrieve this mesh info
        /// </summary>
        public TheNodeDiagInfo NodeDiagInfo { get; set; }

        /// <summary>
        /// List of all node IDs in the mesh
        /// </summary>
        public List<Guid> NodeIDs { get; set; }
    }

    /// <summary>
    /// Helper class for returning a status/error message along with TheMeshInfo
    /// </summary>
    public class TheMeshInfoStatus
    {
        /// <summary>
        /// Information about the mesh
        /// </summary>
        public TheMeshInfo MeshInfo { get; set; }
        /// <summary>
        /// Status of TheMeshInfo retrieval - can be used for error, additional information, etc.
        /// </summary>
        public string StatusMessage { get; set; }

        /// <summary>
        /// Return code of the call
        /// </summary>
        public int StatusCode { get; set; } = 200;
    }

    /// <summary>
    /// Typical error response used to reply to a web request with JSON
    /// </summary>
    public class TheErrorResponse
    {
        /// <summary>
        /// Message describing the error
        /// </summary>
        public string Error { get; set; }
        /// <summary>
        /// Timestamp at which the error occured
        /// </summary>
        public DateTimeOffset CTIM { get; set; }
    }

    /// <summary>
    /// Status information of a Service Route
    /// </summary>
    public class TheServiceRouteInfo
    {
        /// <summary>
        /// Url of the Service route
        /// </summary>
        public string Url { get; set; }
        /// <summary>
        /// Last error in Clear Text
        /// </summary>
        public string LastError { get; set; }
        /// <summary>
        /// Error code of the last error
        /// </summary>
        public int ErrorCode { get; set; }
        /// <summary>
        /// True if service route is connected
        /// </summary>
        public bool IsConnected { get; set; }
        /// <summary>
        /// True if service route trues to connect
        /// </summary>
        public bool IsConnecting { get; set; }
        /// <summary>
        /// Combined status
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// true if the connection is alive
        /// </summary>
        public bool IsAlive { get; set; }

        /// <summary>
        /// true if the connection is using WebSockets
        /// </summary>
        public bool UsesWS { get; set; }

        /// <summary>
        /// Shows the date when the connection was established
        /// </summary>
        public string ConnectedSince { get; set; }

        /// <summary>
        /// Time/Date of the last heartbeat
        /// </summary>
        public string LastHB { get; set; }
    }

    /// <summary>
    /// Mini Info header of a node just telling the most important information
    /// </summary>
    public class TheNodeInfoHeader
    {
        /// <summary>
        /// Current configured cloud routes and their connectivity status
        /// </summary>
        public List<TheServiceRouteInfo> CloudRoutes { get; set; }
        /// <summary>
        /// Node Name of the Node in question
        /// </summary>
        public string NodeName { get; set; }
        /// <summary>
        /// DeviceID/NodeID of the node in question
        /// </summary>
        public Guid NodeID { get; set; }
        /// <summary>
        /// Current Version string of the node
        /// </summary>
        public string CurrentVersion { get; set; }
        /// <summary>
        /// Current build number of the node
        /// </summary>
        public double BuildVersion { get; set; }

        /// <summary>
        /// The scope hash of the active scope of the node
        /// </summary>
        public string ScopeHash { get; set; }

        /// <summary>
        /// OS the node is running on
        /// </summary>
        public string OSInfo { get; set; }
    }

    #region TheServiceHost Info
    /// <summary>
    /// Information about a given Device the Node runs on
    /// </summary>
    public class TheDeviceRegistryMini
    {
        /// <summary>
        /// Host Name of the Device
        /// </summary>
        public string DeviceName { get; set; }
        /// <summary>
        /// Type of Device
        /// </summary>
        public string DeviceType { get; set; }
        /// <summary>
        /// Guid of the Device
        /// </summary>
        public Guid DeviceID { get; set; }
        /// <summary>
        /// Primary Device User ID
        /// </summary>
        public Guid PrimaryUserID { get; set; }
        /// <summary>
        /// Last Access to this device
        /// </summary>
        public DateTimeOffset LastAccess { get; set; }
        /// <summary>
        /// Sender Type of this device
        /// </summary>
        public cdeSenderType SenderType { get; set; }

        /// <summary>
        /// Returns a friendly description of the device
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Name:{DeviceName} ID:{DeviceID.ToString().Substring(0, 8)} SenderType:{SenderType}";
        }
    }

    /// <summary>
    /// This is the main configuration class of the C-DEngine hosted as a service inside an application.
    /// </summary>
    /// <remarks></remarks>
    public class TheServiceHostInfoMini
    {
        /// <summary>
        /// ICDESystemLog interface for logging
        /// </summary>
        public ICDESystemLog MySYSLOG { get; set; }

        /// <summary>
        /// ICDEKpis Interface for tracking of KPIs
        /// </summary>
        public ICDEKpis MyKPIs { get; set; }
        /// <summary>
        /// If set to true, the current node is a Cloud Service.
        /// </summary>
        public bool IsCloudService { get; set; }

        /// <summary>
        /// If true, trades off slightly faster communication for a
        /// less secure connection (fewer changes of security tokens).
        /// </summary>
        public bool EnableFastSecurity
        {
            get;
            internal set;
        }

        /// <summary>
        /// Communication Type of this node
        /// </summary>
        public cdeSenderType MyDeviceSenderType
        {
            get;
            internal set;
        }

        /// <summary>
        /// If this is set to true, the C-DEngine is not completely configured, yet. For example no ScopeID
        /// has been issued for the current node. This is not critical for the run-time of the C-DEngine and
        /// is user mainly by Application Configuration Plugins such as the Factory-Relay.
        /// </summary>
        public bool RequiresConfiguration
        {
            get;
            internal set;
        }

        /// <summary>
        /// use new 16 digits EasyScopeID
        /// </summary>
        public bool UseEasyScope16
        {
            get;
            internal set;
        }

        /// <summary>
        /// If set to true, the connection URL for an initial connection is always the same. Set this flag to true, if your firewall needs to ensure the first connection is going to am approved URL
        /// </summary>
        public bool UseFixedConnectionUrl
        {
            get;
            set;
        }

        /// <summary>
        /// Current CDE-T Version
        /// </summary>
        public string ProtocolVersion
        {
            get;
            internal set;
        }
    }

    #endregion



    /// <summary>
    /// Defines a Thread-Safe Concurrent Directory
    /// On Silverlight and .NET35 this uses a normal Directory that can be locked with the MyLock object inside this class
    /// </summary>
    /// <typeparam name="TKey">Type of the Key</typeparam>
    /// <typeparam name="TValue">Type of the Value</typeparam>
#if !SAFECONCURRENTDICT
    public class cdeConcurrentDictionary<TKey, TValue> : ConcurrentDictionary<TKey, TValue>
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public cdeConcurrentDictionary()
        {
        }

        /// <summary>
        /// Constructor with incoming dictionary
        /// </summary>
        /// <param name="dictionary"></param>
        public cdeConcurrentDictionary(IDictionary<TKey, TValue> dictionary)
            : base(dictionary)
        {
        }

        /// <summary>
        /// constructor with incoming comparer
        /// </summary>
        /// <param name="comparer"></param>
        public cdeConcurrentDictionary(IEqualityComparer<TKey> comparer)
            : base(comparer)
        {
        }

        /// <summary>
        /// constructor with incoming comparer and dictionary
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="comparer"></param>
        public cdeConcurrentDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
            : base(dictionary, comparer)
        {
        }

        /// <summary>
        /// Lock Object that can be used to test if this class is in use by another thread
        /// </summary>
        public object MyLock = new ();

        /// <summary>
        /// Removes the record with the corresponding key and does not care if it was successful
        /// </summary>
        /// <param name="pIncomingKey"></param>
        public bool RemoveNoCare(TKey pIncomingKey)
        {
            if (pIncomingKey != null)
            {
                return TryRemove(pIncomingKey, out _);
            }
            return false;
        }

        /// <summary>
        /// Returns the value of the dictionary using the given key.
        /// </summary>
        /// <param name="pIncomingKey"></param>
        /// <threadsafety static="true" instance="true"/>
        public TValue cdeSafeGetValue(TKey pIncomingKey)
        {
            lock (MyLock)
            {
                TValue OutValue = default;
                if (ContainsKey(pIncomingKey))
                {
                    TryGetValue(pIncomingKey, out OutValue);
                }
                return OutValue;
            }
        }

        /// <summary>
        /// Tries to estimate the size of the cdeConcurrentDictionary in bytes
        /// </summary>
        /// <returns></returns>
        public long GetSize()
        {
            long size = 0;
            foreach (TKey Key in Keys)
            {
                try
                {
                    size += GetSizeOfObject(this[Key]);
                }
                catch
                {
                    //ignored
                }
            }
            return size;
        }
        /// <summary>
        /// This returns a consistent snapshot of the dictionary
        /// </summary>
        /// <returns></returns>
        public Dictionary<TKey, TValue> GetDictionarySnapshot() => new (this);

        /// <summary>
        /// This enumerator does not take a lock/snapshot and may reflect changes to the dictionary after it was obtained
        /// </summary>
        /// <returns></returns>
        public IEnumerable<KeyValuePair<TKey, TValue>> GetDynamicEnumerable() => this;

        internal static int GetSizeOfObject(object obj)
        {
            int size = 0;
            Type type = obj.GetType();
            PropertyInfo[] info = type.GetProperties();
            foreach (PropertyInfo property in info)
            {
                object Value = property.GetValue(obj, null);
                if (Value == null) continue;
                switch (property.PropertyType.FullName)
                {
                    case "System.String":
                        size += Value.ToString().Length;
                        break;
                    case "System.DateTime":
                        size += 8;
                        break;
                    case "System.DateTimeOffset":
                        size += 8 + 2;
                        break;
                    case "System.Boolean":
                        size += 1;
                        break;
                    case "System.Guid":
                        size += 16;
                        break;
                    case "System.Double":
                        size += 8;
                        break;
                    case "System.Int32":
                    case "System.Int":
                        size += 4;
                        break;
                    case "System.Int64":
                    case "System.Long":
                        size += 8;
                        break;
                    case "System.Byte[]":
                        byte[] array = (byte[])property.GetValue(obj, null);
                        size += array.GetLength(0);
                        break;
                    case "System.Byte":
                        size++;
                        break;
                }
            }
            return size;
        }
    }
#else
    // CODE REVIEW: Do we want to make this change? It's technically a breaking change as we'd be removing unsafe methods/properties, but none of our plug-ins use any of those methods
    // This flavor avoids accidental use of extension methods like .ToList() that are not thread safe on a concurrent dictionary
    // All methods on this class are thread safe, including the enumerators
    public class cdeConcurrentDictionary<TKey, TValue>
    {
        ConcurrentDictionary<TKey, TValue> _dict;
        public cdeConcurrentDictionary()
        {
            _dict = new ConcurrentDictionary<TKey, TValue>();
        }

        public cdeConcurrentDictionary(IDictionary<TKey, TValue> dictionary)
        {
            _dict = new ConcurrentDictionary<TKey, TValue>(dictionary);
        }

        public cdeConcurrentDictionary(IEqualityComparer<TKey> comparer)
        {
            _dict = new ConcurrentDictionary<TKey, TValue>(comparer);
        }

        public cdeConcurrentDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
        {
            _dict = new ConcurrentDictionary<TKey, TValue>(dictionary, comparer);
        }

        /// <summary>
        /// Lock Object that can be used to test if this class is in use by another thread
        /// </summary>
        public object MyLock = new object();

        public ICollection<TKey> Keys => _dict.Keys;

        public ICollection<TValue> Values => _dict.Values;

        public bool IsEmpty => _dict.IsEmpty;

        public int Count => _dict.Count;

        //ublic bool IsReadOnly => _dict.IsReadOnly;

        public TValue this[TKey key] { get => _dict[key]; set => _dict[key] = value; }

        public bool TryRemove(TKey key, out TValue value) => _dict.TryRemove(key, out value);

        public bool TryAdd(TKey key, TValue value) => _dict.TryAdd(key, value);
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory) => _dict.GetOrAdd(key, valueFactory);
        public TValue GetOrAdd(TKey key, TValue value) => _dict.GetOrAdd(key, value);
        public TValue AddOrUpdate(TKey key, Func<TKey, TValue> addValueFactory, Func<TKey, TValue, TValue> updateValueFactory) => _dict.AddOrUpdate(key, addValueFactory, updateValueFactory);
        public TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory) => _dict.AddOrUpdate(key, addValue, updateValueFactory);

        /// <summary>
        /// Removes the record with the corresponding key and does not care if it was successful
        /// </summary>
        /// <param name="pIncomingKey"></param>
        public bool RemoveNoCare(TKey pIncomingKey)
        {
            if (pIncomingKey != null)
            {
                TValue tVal;
                return TryRemove(pIncomingKey, out tVal);
            }
            return false;
        }

        /// <summary>
        /// Returns the value of the dictionary using the given key.
        /// </summary>
        /// <param name="pIncomingKey"></param>
        /// <threadsafety static="true" instance="true"/>
        public TValue cdeSafeGetValue(TKey pIncomingKey)
        {
            lock (MyLock)
            {
                TValue OutValue = default(TValue);
                if (ContainsKey(pIncomingKey))
                {
                    TryGetValue(pIncomingKey, out OutValue);
                }
                return OutValue;
            }
        }

        public long GetSize()
        {
            long size = 0;
            foreach (TKey Key in Keys)
            {
                try
                {
                    size += CU.GetSizeOfObject(this[Key]);
                }
                catch
                {
                    //ignored
                }
            }
            return size;
        }

        //public void Add(TKey key, TValue value)
        //{
        //    _dict.Add(key, value);
        //}

        public bool ContainsKey(TKey key)
        {
            return _dict.ContainsKey(key);
        }

        //public bool Remove(TKey key)
        //{
        //    return _dict.Remove(key);
        //}

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dict.TryGetValue(key, out value);
        }

        //public void Add(KeyValuePair<TKey, TValue> item)
        //{
        //    _dict.Add(item);
        //}

        public void Clear()
        {
            _dict.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return _dict.Contains(item);
        }

        //public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        //{
        //    _dict.CopyTo(array, arrayIndex);
        //}

        //public bool Remove(KeyValuePair<TKey, TValue> item)
        //{
        //    return _dict.Remove(item);
        //}

        public bool Any() => _dict.Any();
        public bool Any(Func<KeyValuePair<TKey, TValue>, bool> predicate) => _dict.Any(predicate);
        public IEnumerable<TResult> Select<TResult>(Func<KeyValuePair<TKey, TValue>, TResult> selector) => _dict.Select(selector);
        public IEnumerable<KeyValuePair<TKey, TValue>> Where(Func<KeyValuePair<TKey, TValue>, bool> predicate) => _dict.Where(predicate);
        public TResult Min<TResult>(Func<KeyValuePair<TKey, TValue>, TResult> selector) => _dict.Min(selector);

        public KeyValuePair<TKey, TValue> FirstOrDefault() => _dict.FirstOrDefault();
        public KeyValuePair<TKey, TValue> First() => _dict.First();

        public Dictionary<TKey,TValue> GetDictionarySnapshot() => new Dictionary<TKey, TValue>(_dict);

        // This enumerator does not take a lock/snapshot and may reflect changes to the dictionary after it was obtained
        public IEnumerable<KeyValuePair<TKey,TValue>> GetDynamicEnumerable() => _dict;
    }

#endif
}
