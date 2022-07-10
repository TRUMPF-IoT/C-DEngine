// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
#pragma warning disable 1591

//#pragma warning disable CS1591    //TODO: Remove and document public methods


namespace nsCDEngine.ViewModels
{
    #region ENUMS
    /// <summary>
    /// Predefined User Roles. Instances of this class can be use to create new roles.
    /// </summary>
    /// <threadsafety static="false" instance="true"/>
    public class eUserRoles
    {
        /// <summary>
        /// Administrator Role allowed to make configuration changes
        /// </summary>
        public const string Administrator = "Administrator";
        /// <summary>
        /// Guest Role for anonymous access
        /// </summary>
        public const string Guest = "Guest";
        /// <summary>
        /// Custom Name for new Roles
        /// </summary>
        public string Name;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:nsCDEngine.ViewModels.eUserRoles">eUserRoles</see> class.
        /// </summary>
        /// <remarks></remarks>
        public eUserRoles()
        {
            Name = "";
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="T:nsCDEngine.ViewModels.eUserRoles"/> class.
        /// </summary>
        /// <param name="pName">Name of a new Role</param>
        public eUserRoles(string pName)
        {
            Name = pName;
        }
    }

    public enum FileListStyle
    {
        UnixStyle,
        WindowsStyle,
        Unknown
    }

    public enum eThingCaps
    {
        /// <summary>
        /// Internal Plugins - cannot be stopped or isolated and will be loaded first
        /// </summary>
        Internal = 1,
        UpgradeOnly = 2,
        /// <summary>
        /// Plugin contains NMI controls that need to be loaded before other plugins that might depend on these controls
        /// </summary>
        NMIControls = 3,
        /// <summary>
        /// Skin provider plugins are used by the NMI to present styles and important Javascript addons
        /// </summary>
        SkinProvider = 4,

        HardwareAccess = 30,
        EnergyMeter = 31,
        ComputerHealth = 32,
        Host = 33,
        BaseEngine = 34,
        FileServices = 35,
        ProtocolTransformer = 36,
        InputDevices = 37,
        CameraSource = 38,
        RulesEngine = 39,
        DistributedStorage = 40,
        NMIEngine = 41,
        MustBePresent = 42,
        DoNotIsolate = 43,
        /// <summary>
        /// Thing (or some things of this engine) contains properties that conform to the contracts for sensor values.
        /// </summary>
        SensorContainer = 44,
        /// <summary>
        /// Thing (or at least some things of this engine) can write sensor values into other things/properties; conforms to the sensor provider contract.
        /// </summary>
        SensorProvider = 45,
        /// <summary>
        /// Thing (or at least some things of this engine) can read sensor values from other things/properties and send them to other systems; conforms to the sensor consumer contract.
        /// </summary>
        SensorConsumer = 46,
        /// <summary>
        /// Thing (or at least some things of this engine) can read sensor values from other things/properties and write them to another thing/property; conforms to the sensor consumer and sensor provider contracts.
        /// </summary>
        SensorProcessor = 47,
        /// <summary>
        /// Thing (or at least some things of this engine) can export and import their configuration/settings; conforms to the configuration management contract.
        /// </summary>
        ConfigManagement = 48,
        /// <summary>
        /// Contains support for ICDELoggerEngine to provide logging support
        /// </summary>
        LoggerEngine = 49
    }
    #endregion

    /// <summary>
    /// TheDataBase is the Base Class of all Classes in the C-DEngine that want to participate in the StorageService communication
    /// It is always good practice to derive from this class. Many C-DEngine functions do rely on these base properties
    /// </summary>
    public class TheDataBase : TheBindableBase, ICDEEvents
    {
        /// <summary>
        /// If called from a derived class and the class was stored in a StorageMirror, this method will call the "NotifyOfUpdate" method in the StorageMirror to inform all Notification Clients that this record has changed.
        /// This is VERY Expensive! Do not call too often!
        /// This does not work with StorageMirrors that require a PostSalt (unique table name)
        /// </summary>
        public void NotifyMirror()
        {
            TheCDEngines.MyStorageMirrorRepository.TryGetValue(TheStorageUtilities.GenerateUniqueIDFromType(GetType(), null), out object tStorageMirror);
            if (tStorageMirror != null)
            {
                Type magicType = tStorageMirror.GetType();
                MethodInfo magicMethod = magicType.GetMethod("NotifyOfUpdate");
                if (magicMethod != null)
                    magicMethod.Invoke(tStorageMirror, new object[] { null }); //object magicValue =
            }
        }

        #region NEW 3.121: Register and Fire Event on any Element Derived from TheMetaBase - new in 4.110 moved here for better low level support of telegrams
        // CODE REVIEW: Do we need this still? Has been obsolete for a long time now
        private TheCommonUtils.RegisteredEventHelper<object, TheProcessMessage> TMDBRegisteredEventsOLD;

        /// <summary>
        /// Register a callback that will be fired on a Property Event
        /// </summary>
        /// <param name="pEventName">Using eThingEvents.XXX to register a callback</param>
        /// <param name="pCallback">A Callback that will be called when the eThingEvents.XXX fires</param>
        [Obsolete("Do not use anymore! Please us RegisterEvent2(string pEventName, Action<TheProcessMessage,object> pCallback) instead")]
        public virtual Action<object, TheProcessMessage> RegisterEvent(string pEventName, Action<object, TheProcessMessage> pCallback)
        {
            if (pCallback == null || string.IsNullOrEmpty(pEventName)) return null;
            if (TMDBRegisteredEventsOLD == null)
                TMDBRegisteredEventsOLD = new TheCommonUtils.RegisteredEventHelper<object, TheProcessMessage>();

            return TMDBRegisteredEventsOLD.RegisterEvent(pEventName, pCallback);
        }

        /// <summary>
        /// Unregister a previously registered callback
        /// </summary>
        /// <param name="pEventName">eThingEvents that holds the callback </param>
        /// <param name="pCallback">The callback to unregister</param>
        [Obsolete("Do not use anymore! Please us UnregisterEvent2(string pEventName, Action<TheProcessMessage,object> pCallback) instead")]
        public virtual bool UnregisterEvent(string pEventName, Action<object, TheProcessMessage> pCallback)
        {
            return TMDBRegisteredEventsOLD?.UnregisterEvent(pEventName, pCallback) ?? false;
        }

        /// <summary>
        /// Fire an Event on a property
        /// </summary>
        /// <param name="pEventName">eThingEvent to be fired</param>
        /// <param name="pMsg"></param>
        /// <param name="FireAsync">Set to true if this event should fire async</param>
        /// <param name="pFireEventTimeout"></param>
        public virtual void FireEvent(string pEventName, TheProcessMessage pMsg = null, bool FireAsync = true, int pFireEventTimeout = 0)
        {
            if (string.IsNullOrEmpty(pEventName)) return;

            bool HasFired = false;
            if (TMDBRegisteredEventsOLD != null)
            {
                try
                {
                    TMDBRegisteredEventsOLD.FireEvent(pEventName, this, pMsg, FireAsync, pFireEventTimeout);
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2352, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheThing", string.Format("Error during Event Fire:{0}", pEventName), e.ToString()));
                }
                HasFired = true;
            }
            if (TMDBRegisteredEvents != null && !HasFired)
            {
                TMDBRegisteredEvents.FireEvent(pEventName, pMsg, this, FireAsync, pFireEventTimeout);
            }
        }

        private TheCommonUtils.RegisteredEventHelper<TheProcessMessage, object> TMDBRegisteredEvents;

        /// <summary>
        /// Register a callback that will be fired on a Property Event
        /// </summary>
        /// <param name="pEventName">Using eThingEvents.XXX to register a callback</param>
        /// <param name="pCallback">A Callback that will be called when the eThingEvents.XXX fires</param>
        public virtual Action<TheProcessMessage, object> RegisterEvent2(string pEventName, Action<TheProcessMessage, object> pCallback)
        {
            if (pCallback == null || string.IsNullOrEmpty(pEventName)) return null;
            if (TMDBRegisteredEvents == null)
                TMDBRegisteredEvents = new TheCommonUtils.RegisteredEventHelper<TheProcessMessage, object>();

            return TMDBRegisteredEvents.RegisterEvent(pEventName, pCallback);
        }

        /// <summary>
        /// Unregister a previously registered callback
        /// </summary>
        /// <param name="pEventName">eThingEvents that holds the callback </param>
        /// <param name="pCallback">The callback to unregister</param>
        public virtual bool UnregisterEvent2(string pEventName, Action<TheProcessMessage, object> pCallback)
        {
            return TMDBRegisteredEvents?.UnregisterEvent(pEventName, pCallback) ?? false;
        }

        /// <summary>
        /// Returns true if the requested eThingEvents has registered callbacks
        /// </summary>
        /// <param name="pEventName">Event to be checked</param>
        /// <returns>True if the event has callbacks</returns>
        public bool IsEventRegistered(string pEventName)
        {
            return TMDBRegisteredEvents?.HasRegisteredEvents(pEventName) ?? false;
        }
        #endregion
        /// <summary>
        /// Unique Key for the class derived from TheDataBase. this will be used in the StorageService as the Unique Index in the SQL Tables
        /// </summary>
        public Guid cdeMID { get; set; }
        /// <summary>
        /// Timestamp of this class. Will be set to DateTimeOffset.Now on creation of the Class
        /// </summary>
        public DateTimeOffset cdeCTIM { get; set; }
        /// <summary>
        /// Expiration in Seconds of the class
        /// If a derived class is stored in a StorageMirror and cdeEXP is set to>0, the StorageMirror will automatically delete this record after the time has passed
        /// </summary>
        public long cdeEXP { get; set; }
        /// <summary>
        /// Priority of this record.
        /// A lower number means a higher priority.
        /// Data with higher priority will be retrieved faster than those with lower prio
        /// </summary>
        public byte cdePRI { get; set; }
        /// <summary>
        /// Availability of this record
        /// The higher this number the higher the availability of this record
        /// The Storage Service might store the data in multiple location to ensure maximum availability
        /// </summary>
        public byte cdeAVA { get; set; }

        /// <summary>
        /// Guid of Hosting Node - new in 4.108: Moved from MetaDataBase to here
        /// </summary>
        public Guid cdeN { get; set; }  //CODE-REVIEW: Can we do this without breaking compat?

        /// <summary>
        /// Initialization of TheDataBase
        /// Sets the cdeMID to a NewGuid and the cdeCTIM Timestamp to the current time
        /// cdeN will be set to the incoming parameter
        /// </summary>
        public TheDataBase(Guid pcdeN)
        {
            cdeMID = Guid.NewGuid();
            cdeCTIM = DateTimeOffset.Now;
            cdeN = pcdeN;
        }

        /// <summary>
        /// Clonse an incoming object into this object
        /// </summary>
        /// <param name="t">Incoming object to be cloned</param>
        /// <returns></returns>
        public TheDataBase CloneBase(TheDataBase t)
        {
            if (t == null) return null;
            t.cdeAVA = cdeAVA;
            t.cdeCTIM = cdeCTIM;
            t.cdeEXP = cdeEXP;
            t.cdeMID = cdeMID;
            t.cdePRI = cdePRI;
            t.cdeN = cdeN;
            return t;
        }

        /// <summary>
        /// Initialization of TheDataBase
        /// Sets the cdeMID to a NewGuid and the cdeCTIM Timestamp to the current time
        /// </summary>
        public TheDataBase()
        {
            cdeMID = Guid.NewGuid();
            cdeCTIM = DateTimeOffset.Now;
            cdeN = TheBaseAssets.MyServiceHostInfo?.MyDeviceInfo != null ? TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID : Guid.Empty;
        }
    }

    /// <summary>
    /// TheMetaDataBase extends TheDataBase by a string of Meta data that can be passed along with the data
    /// You can use this as a cookie
    /// The HTML5 C-DEngine can use this for table and form metadata
    /// </summary>
    public class TheMetaDataBase : TheDataBase
    {
        /// <summary>
        /// The Cookie or Meta information for this class
        /// </summary>
        public string cdeM { get; set; }

        /// <summary>
        /// Owner Guid
        /// </summary>
        public Guid cdeO { get; set; }

        /// <summary>
        /// Access Level Mask
        /// 0=Everyone;
        /// 1=Untrusted Guest;
        /// 2=Trusted Guest;
        /// 4=Trusted Member1;
        /// 8=Trusted Member2;
        /// 16=Truested Member3;
        /// 32=Senior Member 1;
        /// 64=Senior Member 2;
        /// 128=Admin;
        /// </summary>
        public int cdeA { get; set; }

        /// <summary>
        /// User ID owning this resource
        /// </summary>
        public Guid? cdeU { get; set; }
        /// <summary>
        /// Feature ID
        /// </summary>
        public int cdeF { get; set; }

        /// <summary>
        /// Sequence number
        /// </summary>
        public long? cdeSEQ { get; set; }

        /// <summary>
        /// TheMetaDataBase contains extra Meta information about a class managed by the C-DEngine like security-, node- and owner-specific data.
        /// </summary>
        public TheMetaDataBase()
        {
            if (TheBaseAssets.MyServiceHostInfo != null && TheBaseAssets.MyServiceHostInfo.MyDeviceInfo != null)
            {
                cdeO = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
            }
        }

        /// <summary>
        /// Clones the metadata of the current class to a new class provided to the method
        /// </summary>
        /// <param name="pMeta"></param>
        public void CloneTo(TheMetaDataBase pMeta)
        {
            if (pMeta == null) return;
            pMeta.cdeA = cdeA;
            pMeta.cdeAVA = cdeAVA;
            pMeta.cdeCTIM = cdeCTIM;
            pMeta.cdeEXP = cdeEXP;
            pMeta.cdeF = cdeF;
            pMeta.cdeM = cdeM;
            pMeta.cdeMID = cdeMID;
            pMeta.cdeO = cdeO;
            pMeta.cdeN = cdeN;
            pMeta.cdePRI = cdePRI;
            pMeta.cdeSEQ = cdeSEQ;
        }
    }

    /// <summary>
    /// TheProcessMessage contains all the information that was received from another node
    /// </summary>
    public class TheProcessMessage
    {
        /// <summary>
        /// NEWV4: Allows to add a cookie object to the TPM
        /// </summary>
        public object Cookie { get; set; }
        /// <summary>
        /// Publication Topic. Most plugins use the Plugin-Service ClassName
        /// </summary>
        public string Topic { get; set; }
        /// <summary>
        /// ID of the Current user associated with this Message
        /// </summary>
        //internal TheUserDetails CurrentUser { get; set; }
        public Guid CurrentUserID { get; set; }
        /// <summary>
        /// new in 4.2: contains the complete ClientInfo of an incoming request
        /// </summary>
        public TheClientInfo ClientInfo { get; set; }
        /// <summary>
        /// The Message received
        /// </summary>
        public TSM Message { get; set; }
        /// <summary>
        /// If this message was called from a hosting application, this host can specify a callback to be called on completion of the message processing
        /// </summary>
        public Action<TSM> LocalCallback { get; set; }

        /// <summary>
        /// Creates a default Process Message with empty Topic and Empty Message
        /// </summary>
        public TheProcessMessage()
        {
        }
        /// <summary>
        /// Creates a new TheProcessMessage from TheClientInfo - mainly used for local TPM processing
        /// </summary>
        /// <param name="pInfo"></param>
        public TheProcessMessage(TheClientInfo pInfo)
        {
            ClientInfo = pInfo;
            if (pInfo != null)
                CurrentUserID = pInfo.UserID;
        }

        /// <summary>
        /// Creates a ProcessMessage with the giving TSM as the Message
        /// The Topic will be generated automatically using a scoped version of the pMessage.ENG
        /// </summary>
        /// <param name="pMessage">TSM to be used by TheProcessMessage</param>
        public TheProcessMessage(TSM pMessage)
        {
            if (pMessage != null)
            {
                Message = pMessage;
                Topic = TheBaseAssets.MyScopeManager?.AddScopeID(pMessage.ENG);
            }
        }

        /// <summary>
        /// Creates a ProcessMessage with the giving TSM as the Message
        /// The Topic will be generated automatically using a scoped version of the pMessage.ENG
        /// </summary>
        /// <param name="pMessage">TSM to be used by TheProcessMessage</param>
        /// <param name="pLocalCallback">Local Callback to be called when this processing is completed</param>
        public TheProcessMessage(TSM pMessage, Action<TSM> pLocalCallback)
        {
            if (pMessage != null)
            {
                Message = pMessage;
                Topic = TheBaseAssets.MyScopeManager?.AddScopeID(pMessage.ENG);
                LocalCallback = pLocalCallback;
            }
        }
    }

    /// <summary>
    /// The Device Message is the main message block sent to a JSON based JavaScript client
    /// </summary>
    public class TheDeviceMessage
    {
        /// <summary>
        /// Reserved for future use
        /// </summary>
        public int MET { get; set; }
        /// <summary>
        /// The Topic that the JSON Client can use to prefilter TSMs coming down
        /// </summary>
        public string TOP { get; set; }
        /// <summary>
        /// The Message to be handled by the JSON Client
        /// </summary>
        public TSM MSG { get; set; }
        /// <summary>
        /// Amount of messages still left on the node for this JSON Client
        /// </summary>
        public int CNT { get; set; }
        /// <summary>
        /// Device ID of the JSON Client
        /// </summary>
        public string DID { get; set; }
        /// <summary>
        /// Floating ID (Serial Number)
        /// </summary>
        public string FID { get; set; }
        /// <summary>
        /// Scambled ScopeID of the sending Node
        /// </summary>
        public string SID { get; set; }
        /// <summary>
        /// URL of the next call to be made.
        /// </summary>
        public string NPA { get; set; }
        /// <summary>
        /// Current Public Key for this request
        /// </summary>
        public string RSA { get; set; }

        /// <summary>
        /// Serializes TheDeviceMessage to a JSON Byte Array
        /// </summary>
        /// <returns>Byte Array of the TheDeviceMessage</returns>
        internal static byte[] SerToJSON(List<TheDeviceMessage> tDevList)
        {
            if (tDevList == null || tDevList.Count == 0) return null;
#if !CDE_FASTTSMSERIALIZE
            string retstr = TheCommonUtils.SerializeObjectToJSONString(tDevList);
#else
			string retstr = "[";
			foreach (TheDeviceMessage tDev in tDevList)
			{
				if (retstr.Length > 1) retstr += ",";
				string fstr = "{{\"CNT\":{0},\"DID\":\"{1}\",\"MET\":{2},\"FID\":{17},\"SID\":\"{18}\",\"UID\":\"{20}\",\"NPA\":{19},\"MSG\":{{\"CST\":\"{3}\",\"ENG\":\"{4}\",\"FID\":\"{6}\",\"FLG\":{7},\"LVL\":{8},\"ORG\":\"{9}\",\"OWN\":\"{5}\",\"PLB\":null,\"PLS\":\"{10}\",\"QDX\":{11},\"SEID\":\"{12}\",\"SID\":\"{13}\",\"TIM\":\"/{14}/\",\"TXT\":\"{15}\",\"PLB\":\"{21}\"}},\"TOP\":\"{16}\"}}";
				retstr += string.Format(fstr, tDev.CNT, tDev.DID, tDev.MET, tDev.MSG.CST, tDev.MSG.ENG, tDev.MSG.OWN,
									tDev.MSG.FID, tDev.MSG.FLG, (int)tDev.MSG.LVL, tDev.MSG.ORG, tDev.MSG.PLS, tDev.MSG.QDX, tDev.MSG.SEID, tDev.MSG.SID, TheCommonUtils.CDateTimeToJSONDate(tDev.MSG.TIM), tDev.MSG.TXT,     //ORG-OK
									tDev.TOP, tDev.FID, tDev.SID, tDev.NPA, tDev.MSG.UID, tDev.MSG.PLB == null ? "" : Convert.ToBase64String(tDev.MSG.PLB));
			}
			retstr += "]";
#endif
            return TheCommonUtils.CUTF8String2Array(retstr);
        }

        /// <summary>
        /// Deserializes a JSON string to a TSM
        /// PLB will be ignored
        /// WARNING: this is not used in the C-DEngine and migh
        /// </summary>
        /// <param name="pPayload"></param>
        /// <returns></returns>
        internal static List<TheDeviceMessage> DeserializeJSONToObject(string pPayload)
        {
            return TheCommonUtils.DeserializeJSONStringToObject<List<TheDeviceMessage>>(pPayload);
        }

#if CDE_FASTTSMSERIALIZE
		private static void ValSetter(ref TheDeviceMessage tTSM, string ptr, object tval)
		{
			switch (ptr) //int
			{
				case "MET":
					tTSM.MET = TheCommonUtils.CInt(tval);
					break;
				case "CNT":
					tTSM.CNT = TheCommonUtils.CInt(tval);
					break;
				case "DID":
					tTSM.DID = TheCommonUtils.CStr(tval);
					break;
				case "FID":
					tTSM.FID = TheCommonUtils.CStr(tval);//ORG-OK
					break;
				case "SID":
					tTSM.SID = TheCommonUtils.CStr(tval);//ORG-OK
					break;
				case "NPA":
					tTSM.NPA = TheCommonUtils.CStr(tval);//ORG-OK
					break;
				case "RSA":
					tTSM.RSA = TheCommonUtils.CStr(tval);//ORG-OK
					break;
				case "TOP":
					tTSM.TOP = TheCommonUtils.CStr(tval);//ORG-OK
					break;
				case "MSG":
					tTSM.MSG = TSM.DeserializeJStringToObjectTSM(tval);
					break;
				default:
					break;
			}
		}
#endif
    }

    /// <summary>
    /// Session state class of C-DEngine connected sessions
    /// </summary>
    public class TheSessionState : TheDataBase, IDisposable
    {
        /// <summary>
        /// Clears the session state
        /// </summary>
        public void Dispose()
        {
            MyRSA = null;
        }

        private System.Security.Cryptography.RSACryptoServiceProvider _myRSA;
        internal System.Security.Cryptography.RSACryptoServiceProvider MyRSA
        {
            get { return _myRSA; }
            set
            {
                var myRSATemp = _myRSA;
                _myRSA = value;
                if (myRSATemp != null && myRSATemp != value)
                {
                    _myRSA = null;
                    try
                    {
#if !CDE_NET35
                        myRSATemp.Dispose();
#endif
                    }
                    catch { }
                }
            }
        }

        internal string RSAKey { get; set; }
        internal string RSAPublic { get; set; }

        /// <summary>
        /// RSA public key received from other end the channel: to be used for encrypting messages sent to the other end
        /// </summary>
        internal string RSAPublicSend { get; set; }
        /// <summary>
        /// Creation time of the Session State
        /// </summary>
        public DateTimeOffset EntryTime { get; set; }
        /// <summary>
        /// Last update of the session state
        /// </summary>
        public DateTimeOffset LastAccess { get; set; }
        /// <summary>
        /// Time when the session was closed
        /// </summary>
        public DateTimeOffset EndTime { get; set; }
        /// <summary>
        /// Current status of the session
        /// </summary>
        public string Status { get; set; }
        /// <summary>
        /// Current User ID logged in and associated with this Session State
        /// </summary>
        public Guid CID { get; set; }
        internal string CMember { get; set; }
        /// <summary>
        /// Browser definition of the session
        /// </summary>
        public string Browser { get; set; }
        /// <summary>
        /// Long browser description of the session
        /// </summary>
        public string BrowserDesc { get; set; }
        internal long MembLevel { get; set; }
        internal string MembUID { get; set; }
        internal string MembPWD { get; set; }
        internal string MembEMAil { get; set; }
        internal string MembFullName { get; set; }
        internal Guid OrigThing { get; set; }
        internal List<TheMeshPicker> Meshes { get; set; } //New 4.209: contains a list of Meshes available for the user to pick from
        /// <summary>
        /// Custom Debug "Long" to be stored with the session
        /// </summary>
		public long Debug { get; set; }

        /// <summary>
        /// Language ID of the current Session State. Will be set during login according to users setting in TheUserManager
        /// </summary>
        public int LCID { get; set; }
        /// <summary>
        /// Contains the SessionID. Used by Web Relay
        /// </summary>
		public Guid RunID { get; set; }
        /// <summary>
        /// Hit Counter of pages for this session
        /// </summary>
		public int PageHits { get; set; }
        /// <summary>
        /// Root site name of this session
        /// </summary>
		public string SiteName { get; set; }
        /// <summary>
        /// Current URL the user is on using this session
        /// </summary>
		public string CurrentURL { get; set; }
        /// <summary>
        /// Custom data (string) to be stored with this session
        /// </summary>
		public string CustomData { get; set; }
        /// <summary>
        /// Custom Data (Long) stored with this session
        /// </summary>
		public long CustomDataLng { get; set; }
        /// <summary>
        /// Screenwidth of the users browser screen
        /// </summary>
		public int ScreenWidth { get; set; }
        /// <summary>
        /// unused
        /// </summary>
		public int PCID { get; set; }
        /// <summary>
        /// Version of the current host site
        /// </summary>
		public string SiteVersion { get; set; }
        /// <summary>
        /// Browser language as told by the User Agent of the browser
        /// </summary>
		public string BrowserLanguage { get; set; }
        /// <summary>
        /// Passport PUID High
        /// </summary>
		public int PUIDHigh { get; set; }
        /// <summary>
        /// Passport PUID Low
        /// </summary>
		public int PUIDLow { get; set; }
        /// <summary>
        /// String value holding the UserAgent field of an incoming http request.
        /// For example:
        /// "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:69.0) Gecko/20100101 Firefox/69.0"
        /// </summary>
		public string UserAgent { get; set; }

        /// <summary>
        /// HTTP Request Item: "Initial Referer"
        /// </summary>
		public string InitReferer { get; set; }

        /// <summary>
        /// Current Home Screen
        /// </summary>
		public string HS { get; set; }

        /// <summary>
        /// URL Token for pin login
        /// </summary>
        public string TETO { get; set; }
        /// <summary>
        /// Current Web Page ID
        /// </summary>
		public Guid CurrentWPID { get; set; }

        /// <summary>
        /// Client device GUID from User Manager "MyDeviceRegistry"
        /// </summary>
		public Guid MyDevice { get; set; }

        /// <summary>
        /// first part of the site dns name (i.e. "test.c-labs.com" test is the TRGTSRV)
        /// </summary>
		public string TRGTSRV { get; set; }
        private long fid;
        internal long FID
        {
            get
            {
                return Interlocked.Read(ref fid);
            }
            set
            {
                Interlocked.Exchange(ref fid, value);
            }
        }
        /// <summary>
        /// if true, this session state is no longer valid and will be removed shortly
        /// </summary>
        public bool HasExpired { get; set; }

        /// <summary>
        /// Scopes permitted via Client Certificates
        /// </summary>
        internal List<string> CertScopes { get; set; }
        /// <summary>
        /// Scrambled Scope ID the current session
        /// </summary>
        internal string SScopeID { get; set; }

        /// <summary>
        /// Web Relay APP Guid
        /// </summary>
		public Guid ARApp { get; set; }

        /// <summary>
        /// Name/Value pairs of all session state cookies
        /// </summary>
		public cdeConcurrentDictionary<string, string> StateCookies { get; set; }

        //public bool IsMobileDevice { get; set; }
        /// <summary>
        /// Current Browser platform the last request was sent from
        /// </summary>
        public eWebPlatform WebPlatform { get; set; }

        /// <summary>
        /// State Cookie object
        /// </summary>
		public object StateCookie { get; set; }

        /// <summary>
        /// Request entry "RemoteAddress"
        /// </summary>
		public string RemoteAddress { get; set; }



        /// <summary>
        /// User Friendly print of the current session state
        /// </summary>
        /// <returns></returns>
		public override string ToString()
        {
            return $"SID=({cdeMID}) CID:{(CID == Guid.Empty ? "not set" : CID.ToString().Substring(0, 4))} DID:{MyDevice} RemoteAddr:({RemoteAddress}) LastAccess:({LastAccess}) End:{EndTime} Browser:({Browser} {BrowserDesc})</br>CurrentUrl:({CurrentURL}) UA:{UserAgent}".ToString(CultureInfo.InvariantCulture);
        }

        internal object MySessionLock = new object();

        /// <summary>
        /// Increase the FID Serial Number by one
        /// </summary>
        public long GetNextSerial()
        {
            return Interlocked.Increment(ref fid);
        }

        /// <summary>
        /// Get Scrambled ScopeID from Session
        /// </summary>
        /// <returns></returns>
        public string GetSID()
        {
            return SScopeID;
        }
    }

    /// <summary>
    /// This structure hold network request and response data. It is used in several
    /// places within C-DEngine, including for Http interceptor functions.
    /// </summary>
    public class TheRequestData : TheDataBase
    {
        public bool SetSessionState(Guid pSEID)
        {
            return TheBaseAssets.MySession.GetSessionState(pSEID, this, true);
        }

        public object CookieObject;
        public string CookieString;
        /// String value with the UserAgent field of the incoming request.<br/>
        /// For example:
        ///"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:69.0) Gecko/20100101 Firefox/69.0"
		public string UserAgent { get; set; }
        public Guid DeviceID { get; set; }
        /// <summary>
        /// A dictionary field initialized to hold the headers of the incoming request.
        /// </summary>
		public cdeConcurrentDictionary<string, string> Header { get; set; }
        public cdeConcurrentDictionary<string, string> ServerTags { get; set; }
        public string NewLocation { get; set; }
        public cdeConcurrentDictionary<string, string> RequestCookies { get; set; }
        public string RequestCookiesStr { get; set; }

        public int TimeOut { get; set; }
        //public bool IsMobileDevice { get; set; }
        public eWebPlatform WebPlatform { get; set; }

        public string RequestUriString { get; set; }
        public System.Net.CookieContainer TempCookies { get; set; }
        /// <summary>
        /// A Uri type field containing the URL of the incoming request.
        /// For example, all of the details in a URL like this http://localhost:10/my/page?num=12
        /// are available within various members of RequestUri.
        /// </summary>
        public Uri RequestUri { get; set; }
        /// <summary>
        /// A subset of the URL that identifies the requested page. For example,
        /// this URL http://localhost:10/my/page?num=12, generates a value for
        /// cdeRealPage of "/my/page".
        /// </summary>
		public string cdeRealPage { get; set; }

        public string RemoteAddress { get; set; }
        public long GetContentLength()
        {
            if (PostDataLength > 0)
            {
                return PostDataLength;
            }
            if (_postData != null)
            {
                return _postData.Length;
            }
            if (_postDataStream != null)
            {
                return _postDataStream.Length;
            }
            return 0;
        }

        public void WritePostDataToStream(Stream stream)
        {
            if (_postData != null)
            {
                if (PostDataLength > 0)
                    stream.Write(_postData, 0, PostDataLength);
                else
                    stream.Write(_postData, 0, _postData.Length);
            }
            else
            {
                if (_postDataStream != null)
                {
#if !CDE_NET35
                    _postDataStream.CopyTo(stream);
#else
                    var buffer = new byte[4096];
                    int bytesRead = 0;
                    do
                    {
                        bytesRead = _postDataStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            stream.Write(buffer, 0, bytesRead);
                        }
                    } while (bytesRead > 0);
#endif
                }
            }
        }

        /// <summary>
        /// A byte array holding data sent with an incoming post request.
        /// </summary>
        public byte[] PostData
        {
            get
            {
                if (_postData != null)
                {
                    return _postData;
                }
                if (_postDataStream != null)
                {
                    if (_postDataStream is MemoryStream)
                    {
                        return (_postDataStream as MemoryStream).GetBuffer();
                    }
                    var buffer = new byte[_postDataStream.Length];
                    _postDataStream.Read(buffer, 0, (int)_postDataStream.Length);
                    return buffer;
                }
                return null;
            }
            set
            {
                _postData = value;
                _postDataStream = null;
            }
        }
        private byte[] _postData;
        public Stream PostDataStream
        {
            get
            {
                if (_postDataStream != null)
                {
                    return _postDataStream;
                }
                if (_postData != null)
                {
                    return new MemoryStream(_postData);
                }
                return null;
            }
            set
            {
                _postDataStream = value;
                _postData = null;
                PostDataIdx = 0;
                PostDataLength = 0;
            }
        }
        private Stream _postDataStream;
        public int PostDataIdx { get; set; }

        /// <summary>
        /// An integer value indicating the length of the data in the PostData array.
        /// </summary>
		public int PostDataLength { get; set; }
        public string BrowserType { get; set; }
        public string BrowserPlatform { get; set; }
        public int BrowserScreenWidth { get; set; }

        public TheSessionState SessionState { get; set; }

        /// <summary>
        /// A string indicating the format, or "media type", of the returned data. This is initially
        /// set to the value in the ContentType field of the incoming request. But it can be
        /// changed if the response is of a different type.
        /// Examples include "text/html", "text/xml", "text/json", "image/png",
        /// "application/javascript", and "application/json".
        /// </summary>
		public string ResponseMimeType { get; set; }
        /// <summary>
        /// A byte array property for response being returned to the sender. When an interceptor
        /// sends return data to the client, this field holds the data to be returned.
        /// </summary>
		public byte[] ResponseBuffer { get; set; }
        /// <summary>
        /// The response being sent to the client, formatted as a string.
        /// </summary>
		public string ResponseBufferStr { get; set; }
        /// <summary>
        /// A string value used to populate the "Content-Encoding" field in the header of the response.
        /// Examples include "gzip", "compress", "deflate", "identity", and "br".
        /// </summary>
		public string ResponseEncoding { get; set; }

        /// <summary>
        /// Sets the allowed methods for an incoming OPTIONS Preflight checks
        /// </summary>
        public string AllowedMethods { get; set; }
        /// <summary>
        /// Sets the allowed headers for incoming OPTIONS preflight checks
        /// </summary>
        public string AllowedHeaders { get; set; }
        public bool AllowStatePush { get; set; }
        public bool AllowCaching { get; set; }
        public bool DontCompress { get; set; }
        public bool DisableKeepAlive { get; set; }
        public bool DisableChunking { get; set; }
        //Do not follow redirects - just return
        public bool DisableRedirect { get; set; }

        /// <summary>
        /// An integer for the Http Status Code.  Initially set to zero, an interceptor function must
        /// set this value to indicate that a request has been handled. Possible values include:
        /// Not Handled(0), OK(200), NotFound(404), NotAcceptable(406), ServerError(500), and RequestTimeout(408).<br/>
        ///
        /// These values are defined in the eHttpStatusCode enum.
        /// </summary>
        public int StatusCode { get; set; }
        /// <summary>
        /// Provides the Http version from an incoming request, formatted as a string.
        /// For example, "1.0" or "1.1".
        /// </summary>
		public string HttpVersionStr { get; set; }
        /// <summary>
        /// Provides the Http version from an incoming request, formatted as a double value.
        /// For example, 1.0 or 1.1.
        /// </summary>
        public double HttpVersion { get; set; }
        /// <summary>
        /// A string with the name of the HTTP request method. For example, "GET" or "POST".
        /// </summary>
		public string HttpMethod { get; set; }

        public string ErrorDescription { get; set; }

        /// <summary>
        /// Root Thumb if trusted
        /// empty string if not trusted but was verified
        /// null not yet tested
        /// </summary>
        internal string TrustedRoot { get; set; }

        public bool EndSessionOnResponse { get; set; }
        public string UID { get; set; }
        public string PWD { get; set; }
        public string DOM { get; set; }
        public bool Disable100 { get; set; }
        public object WebSocket { get; set; }
        /// <summary>
        /// An object with certificate information. The specific format is dependent on the type of web server.
        /// For example, in Asp.Net the objects are of type System.Web.HttpClientCertificate. In an HttpListener
        /// type web server, the certificates are of type System.Security.Cryptography. X509Certificates.X509Certificate2.
        /// </summary>
        public object ClientCert { get; set; }
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} : UA:{3}", HttpMethod, HttpVersion, RequestUri, UserAgent);
        }


        internal static TheRequestData CloneForWS(TheRequestData pData)
        {
            TheRequestData tData = new TheRequestData
            {
                AllowCaching = pData.AllowCaching,
                AllowStatePush = pData.AllowStatePush,
                BrowserPlatform = pData.BrowserPlatform,
                BrowserScreenWidth = pData.BrowserScreenWidth,
                BrowserType = pData.BrowserType,
                cdeAVA = pData.cdeAVA,
                cdeCTIM = pData.cdeCTIM,
                cdeEXP = pData.cdeEXP,
                cdePRI = pData.cdePRI,
                cdeRealPage = pData.cdeRealPage,
                DeviceID = pData.DeviceID,
                DOM = pData.DOM,
                DontCompress = pData.DontCompress,
                EndSessionOnResponse = pData.EndSessionOnResponse,
                HttpMethod = pData.HttpMethod,
                HttpVersion = pData.HttpVersion,
                HttpVersionStr = pData.HttpVersionStr,
                WebPlatform = pData.WebPlatform,
                NewLocation = pData.NewLocation,
                PWD = pData.PWD,
                RemoteAddress = pData.RemoteAddress,
                RequestCookiesStr = pData.RequestCookiesStr,
                RequestUri = pData.RequestUri,
                RequestUriString = pData.RequestUriString,
                SessionState = pData.SessionState,
                StatusCode = pData.StatusCode,
                TimeOut = pData.TimeOut,
                UID = pData.UID,
                UserAgent = pData.UserAgent,
                ResponseMimeType = pData.ResponseMimeType,
                WebSocket = pData.WebSocket,
                Header = pData.Header,
                ServerTags = pData.ServerTags,
                DisableChunking = pData.DisableChunking,
                DisableKeepAlive = pData.DisableKeepAlive
            };
            //tData.Header = pData.Header;

            //tData.RequestCookies = pData.RequestCookies;

            return tData;
        }
        /// <summary>
        /// Clonse TheRequestData for Logging
        /// </summary>
        /// <param name="pData"></param>
        /// <returns></returns>
        public static TheRequestData CloneForLog(TheRequestData pData)
        {
            TheRequestData tData = CloneForWS(pData);
            if (pData.PostData != null)
            {
                TheCommonUtils.cdeBlockCopy(pData.PostData, 0, tData.PostData, 0, pData.PostData.Length);
                try
                {
                    if (pData.PostData.Length > 1 && string.IsNullOrEmpty(pData.ErrorDescription))
                    {
                        tData.ErrorDescription = pData.ResponseMimeType.Contains("zip") ? TheCommonUtils.cdeDecompressToString(pData.PostData) : TheCommonUtils.CArray2UTF8String(pData.PostData);
                    }
                }
                catch (Exception)
                {
                    //ignored
                }
            }
            tData.WebSocket = null;
            tData.Header = null;
            tData.ServerTags = null;
            tData.PostDataIdx = pData.PostDataIdx;
            tData.PostDataLength = pData.PostDataLength;
            tData.DisableChunking = pData.DisableChunking;
            tData.DisableKeepAlive = pData.DisableKeepAlive;

            if (pData.ResponseBuffer != null)
            {
                try
                {
                    TheCommonUtils.cdeBlockCopy(pData.ResponseBuffer, 0, tData.ResponseBuffer, 0, pData.ResponseBuffer.Length);
                    if (pData.ResponseMimeType.Contains("zip") || "gzip" == pData.ResponseEncoding)
                        tData.ResponseBufferStr = TheCommonUtils.cdeSubstringMax(TheCommonUtils.cdeDecompressToString(pData.ResponseBuffer), 512);
                    else
                        tData.ResponseBufferStr = TheCommonUtils.cdeSubstringMax(TheCommonUtils.CArray2UTF8String(pData.ResponseBuffer), 512);
                    //TheDeviceMessage tDev = TheCommonUtils.DeserializeJSONStringToObject<TheDeviceMessage>(tData.ResponseBufferStr);
                }
                catch (Exception)
                {
                    //ignored
                }
            }
            else
                tData.ResponseBufferStr = pData.ResponseBufferStr;
            return tData;
        }
    }

    /// <summary>
    /// Callback class for Timed callbacks and TSM parameter
    /// </summary>
    public class TheTimedCallback : TheDataBase
    {
        /// <summary>
        /// Callback function
        /// </summary>
        public Action<TSM> MyCallback { get; set; }
    }

    //NEW in 4.2 - CDE 2019 Edition

    /// <summary>
    /// Description of a backup file
    /// </summary>
    public class TheBackupDefinition : TheDataBase
    {
        /// <summary>
        /// Title of the backup
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// Time and Date of the backup
        /// </summary>
        public DateTimeOffset BackupTime { get; set; }
        /// <summary>
        /// Filename of the backup
        /// </summary>
        public string FileName { get; set; }
        /// <summary>
        /// Size of the backup
        /// </summary>
        public long BackupSize { get; set; }
    }

    /// <summary>
    /// File Description
    /// </summary>
    public class TheFileInfo : TheDataBase
    {
        /// <summary>
        /// Gets the name of the file. (Overrides FileSystemInfo.Name)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the full path of the directory or file.
        /// Equivalent to FullName in System.IO.FileInfo.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets the size, in bytes, of the current file.
        /// Equivalent to the attribute FileSize in System.IO.FileInfo.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Gets or sets the attributes for the current
        /// file or directory.
        /// </summary>
        public string FileAttr { get; set; }

        /// <summary>
        /// UNIX file attribute - Owner permissions.
        /// read (r), write (w), execute (x).
        ///
        /// Represented by the first three characters (2-4)
        /// in the displayed UNIX file information.
        ///
        /// For example, -rwxr-xr-- represents that owner
        /// has read (r), write (w) and execute (x) permission.
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        /// UNIX file attribute - Group permissions.
        /// read (r), write (w), execute (x).
        ///
        /// The group's permissions determine what actions
        /// a user, who is a member of the group that a file
        /// belongs to, can perform on the file.
        ///
        /// A files Group permissions are determined by characters
        /// 5-7 in the displayed UNIX file information.
        /// </summary>
        public string Group { get; set; }
        /// <summary>
        /// Returns a value indicating whether the current
        /// file or directory is of type directory.
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// Gets or sets the creation time of the current
        /// file or directory.
        /// </summary>
        public DateTimeOffset CreateTime { get; set; }

        ///// <summary>
        ///// Gets an instance of the parent directory.
        ///// </summary>
        //public string Directory { get; set; }

        ///// <summary>
        ///// Gets a string representing the directory's
        ///// full path.
        ///// </summary>
        //public string DirectoryName { get; set; }

        ///// <summary>
        ///// Gets the string representing the extension part of the file.
        ///// For example, for a file C:\MyFile.txt, this
        ///// property returns ".txt".
        ///// </summary>
        //public string Extension { get; set; }

        /// <summary>
        /// Gets or sets a value that determines if the
        /// current file is read only.
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the time the current file or
        /// directory was last accessed.
        /// </summary>
        public DateTimeOffset LastAccessTime { get; set; }

        /// <summary>
        /// Gets or sets the time when the current
        /// file or directory was last written to.
        /// </summary>
        public DateTimeOffset LastWriteTime { get; set; }

        /// <summary>
        /// Gets or sets a byte array containing the contents of the
        /// current file or directory.
        /// </summary>
        public byte[] Content { get; set; }

        /// <summary>
        ///
        /// </summary>
        // Q: what does this value get / set ?
        public string Cookie { get; set; }

        /// <summary>
        /// Friendly output
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Name:{0} Size:{1}", FileName, FileSize);
        }
    }

    #region Device Type Classes
    /// <summary>
    /// Definition of a Device Type
    /// </summary>
    public class TheDeviceTypeInfo : ICloneable
    {
        /// <summary>
        /// Device Type Name used in Thing "DeviceType" property
        /// </summary>
        public string DeviceType;
        /// <summary>
        /// Capabilities of the DeviceType
        /// </summary>
        public eThingCaps[] Capabilities;
        /// <summary>
        /// Description of the DeviceType
        /// </summary>
        public string Description;
        /// <summary>
        /// List of configuration properties used by this Device Type
        /// </summary>
        public List<TheThing.TheConfigurationProperty> ConfigProperties;

        /// <summary>
        /// List of sensor properties used by this Device Type
        /// </summary>
        public List<TheThing.TheSensorPropertyMeta> SensorProperties;

        /// <summary>
        /// Default constructor
        /// </summary>
        public TheDeviceTypeInfo() { }

        internal TheDeviceTypeInfo(DeviceTypeAttribute dta)
        {
            DeviceType = dta.DeviceType;
            Capabilities = dta.Capabilities;
            Description = dta.Description;
        }

        /// <summary>
        /// Constructor creating a clone of the incoming device type
        /// </summary>
        /// <param name="deviceType"></param>
        public TheDeviceTypeInfo(TheDeviceTypeInfo deviceType)
        {
            if (deviceType == null)
            {
                return;
            }
            this.DeviceType = deviceType.DeviceType;
            this.Description = deviceType.Description;
            this.Capabilities = (eThingCaps[]) deviceType.Capabilities?.Clone();
            if (deviceType.ConfigProperties != null)
            {
                this.ConfigProperties = deviceType.ConfigProperties.Select(cp => new TheThing.TheConfigurationProperty(cp)).ToList();
            }
            if (deviceType.SensorProperties != null)
            {
                this.SensorProperties = deviceType.SensorProperties.Select(sp => new TheThing.TheSensorPropertyMeta(sp)).ToList();
            }
        }

        /// <summary>
        /// Clone function that can be overwritten
        /// </summary>
        /// <returns></returns>
        virtual public TheDeviceTypeInfo Clone()
        {
            var dt = new TheDeviceTypeInfo(this);
            return dt;
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        /// <summary>
        /// Friendy Output
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return DeviceType;
        }

    }

    /// <summary>
    /// DeviceType of an Engine/Plugin
    /// </summary>
    public class TheEngineDeviceTypeInfo : TheDeviceTypeInfo
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public TheEngineDeviceTypeInfo() { }
        /// <summary>
        /// Constructor with engine name and a DeviceType
        /// </summary>
        /// <param name="deviceType">Device Type</param>
        /// <param name="engineName">Engine Name</param>
        public TheEngineDeviceTypeInfo(TheDeviceTypeInfo deviceType, string engineName) : base(deviceType)
        {
            this.EngineName = engineName;
        }
        /// <summary>
        /// Constructor woth incoming devicetype info
        /// </summary>
        /// <param name="deviceType">Device Type Info</param>
        public TheEngineDeviceTypeInfo(TheEngineDeviceTypeInfo deviceType) : this(deviceType, deviceType.EngineName)
        {
        }
        /// <summary>
        /// Engine Name of this device type
        /// </summary>
        public string EngineName { get; set; }

        /// <summary>
        /// Clone override
        /// </summary>
        /// <returns></returns>
        public override TheDeviceTypeInfo Clone()
        {
            return new TheEngineDeviceTypeInfo(this);
        }
    }

    /// <summary>
    /// Helper Class to get all Const Strings in an enum class as an Options List for ComboBoxes
    /// </summary>
    public class TheDeviceTypeEnum
    {
        /// <summary>
        /// Creates an option list for ComboBox presentation of all existing device types
        /// </summary>
        /// <param name="pInObj"></param>
        /// <returns></returns>
        public static string ToOptionList(TheDeviceTypeEnum pInObj)
        {
            if (pInObj == null) return null;

            string tRes = "";

            Type type = pInObj.GetType();
            FieldInfo[] info = type.GetFields();
            foreach (FieldInfo property in info)
            {
                var Value = property.GetValue(pInObj);
                if (Value == null) continue;
                if (property.FieldType.FullName == "System.String")
                {
                    if (tRes.Length > 0) tRes += ";";
                    tRes += TheCommonUtils.CStr(Value);
                }
            }
            return tRes;
        }

        /// <summary>
        /// Creates an option list if this classed is used with ToString or $"{}"
        /// </summary>
        /// <param name="c"></param>
        public static implicit operator string(TheDeviceTypeEnum c)
        {
            return ToOptionList(c);
        }
    }
    #endregion

    /// <summary>
    /// This class contains a result found through the UPnP subsystem
    /// </summary>
    /// <remarks></remarks>
    public class TheUPnPDeviceInfo : TheMetaDataBase
    {
        /// <summary>
        /// Gets or sets the Device Name of the UPnP Device.
        /// </summary>
        /// <value></value>
        /// <remarks></remarks>
        public string DeviceName { get; set; }
        public string ModelNumber { get; set; }
        public string ModelName { get; set; }
        public string FriendlyName { get; set; }
        public string Manufacturer { get; set; }
        public string ManufacturerUrl { get; set; }
        public string SerialNumber { get; set; }
        public string PresentationUrl { get; set; }
        public string ModelUrl { get; set; }
        public string RawMetaXml { get; set; }
        public string IconUrl { get; set; }

        /// <summary>
        /// C-DEngine applications can store any vendor specific data in this property. This property is not part of the UPnP standard.
        /// </summary>
        public string VendorData { get; set; }

        /// <summary>
        /// The C-DEngine stores the http connection URL in this property. This property is not part of the UPnP standard.
        /// </summary>
        public string CDEConnectUrl { get; set; }
        /// <summary>
        /// The C-DEngine stores the WebSockets connection URL in this property. This property is not part of the UPnP standard.
        /// </summary>
        public string CDEConnectWsUrl { get; set; }

        public string CDEContextID { get; set; }

        public string CDEProvisionUrl { get; set; }

        public Guid CDENodeID { get; set; }

        public DateTimeOffset LastSeenAt { get; set; }

        public string ServerName { get; set; }

        public string UUID { get; set; }
        public string USN { get; set; }
        public string SourceIP { get; set; }
        public string LocalIP { get; set; }
        public string LocationURL { get; set; }
        public bool IsAlive { get; set; }
        public string ST { get; set; }
        public int MaxAge { get; set; }
        public string PacketString { get; set; }
        /// <summary>
        /// This is set to true if the device is running the C-DEngine.
        /// </summary>
        public bool IsCDEngine { get; set; }
        //public string PastRoles { get; set; }

        public string ServiceUrl { get; set; }

        public bool HasPresentation { get; set; }

        public Guid ScannerID { get; set; }


        public override string ToString()
        {
            return string.Format("USN:{6} Name:{0} Location:{1} Manufacturer:{2} Presentation:{3} IP:{4} CDE:{5}", FriendlyName, LocationURL, Manufacturer, PresentationUrl, SourceIP, IsCDEngine, USN);
        }
    }

    public class ThePluginInfo : TheDataBase
    {
        public string ServiceName { get; set; }
        public string ServiceDescription { get; set; }
        public string LongDescription { get; set; }
        public double CurrentVersion { get; set; }
        public double Price { get; set; }
        public string HomeUrl { get; set; }
        public string IconUrl { get; set; }
        public string Developer { get; set; }
        public string Copyrights { get; set; }
        public string DeveloperUrl { get; set; }
        public cdePlatform Platform { get; set; }
        public List<cdePlatform> AllPlatforms { get; set; }
        public List<string> Categories { get; set; }
        public List<eThingCaps> Capabilities { get; set; }
        public List<string> FilesManifest { get; set; }

        internal Type PluginType { get; set; }
        public string PluginPath { get; set; }
        public List<TheDeviceTypeInfo> DeviceTypes { get; set; }

        public override string ToString()
        {
            return $"{ServiceName} {CurrentVersion} {cdeMID}".ToString(CultureInfo.InvariantCulture);
        }

        public ThePluginInfo Clone()
        {
            ThePluginInfo t = new ThePluginInfo();
            CloneBase(t);
            t.ServiceName = ServiceName;
            t.ServiceDescription = ServiceDescription;
            t.LongDescription = LongDescription;
            t.CurrentVersion = CurrentVersion;
            t.Price = Price;
            t.HomeUrl = HomeUrl;
            t.IconUrl = IconUrl;
            t.Developer = Developer;
            t.DeveloperUrl = DeveloperUrl;
            t.Copyrights = Copyrights;
            t.Platform = Platform;
            t.Categories = new List<string>();
            if (Categories != null && Categories.Count > 0)
            {
                foreach (string c in Categories)
                    t.Categories.Add(c);
            }
            t.Capabilities = new List<eThingCaps>();
            if (Capabilities != null && Capabilities.Count > 0)
            {
                foreach (eThingCaps c in Capabilities)
                    t.Capabilities.Add(c);
            }
            t.FilesManifest = new List<string>();
            if (FilesManifest != null && FilesManifest.Count > 0)
            {
                foreach (string c in FilesManifest)
                    t.FilesManifest.Add(c);
            }
            if (AllPlatforms?.Count > 0)
            {
                t.AllPlatforms = new List<cdePlatform>();
                t.AllPlatforms.AddRange(AllPlatforms);
            }
            t.PluginType = PluginType;
            t.PluginPath = PluginPath;
            t.DeviceTypes = DeviceTypes?.Select(dt => dt.Clone()).ToList();
            return t;
        }
    }

    /// <summary>
    /// User Role Definition
    /// </summary>
    public class TheUserRole : TheMetaDataBase
    {
        public string RoleName { get; set; }
        public long AccessLevel { get; set; }  // CODE REVIEW: Do these need to be hidden/encrypted etc.?
        public List<string> Permissions { get; set; } // CODE REVIEW: Do these need to be hidden/encrypted etc.?
        public string Comment { get; set; }
        public string ApplicationName { get; set; }
        public bool IsAdmin { get; set; }
        public long VisibleByACL { get; set; }
        public Guid HomeScreen { get; set; }

        public TheUserRole()
        {
        }

        public TheUserRole(Guid pKey, string pName, long visACL, Guid pHomeScreen, bool pIsAdmin, string pComment)
        {
            cdeMID = pKey;
            RoleName = pName;
            //AccessLevel = ACL;
            Comment = pComment;
            HomeScreen = pHomeScreen;
            IsAdmin = pIsAdmin;
            VisibleByACL = visACL;
            if (TheBaseAssets.MyServiceHostInfo != null)
                ApplicationName = TheBaseAssets.MyServiceHostInfo.ApplicationName;
        }
        internal TheUserRole(Guid pKey, string pName, long ACL, long visACL, Guid pHomeScreen, bool pIsAdmin, string pComment)
        {
            cdeMID = pKey;
            RoleName = pName;
            AccessLevel = ACL;
            Comment = pComment;
            HomeScreen = pHomeScreen;
            IsAdmin = pIsAdmin;
            VisibleByACL = visACL;
            if (TheBaseAssets.MyServiceHostInfo != null)
                ApplicationName = TheBaseAssets.MyServiceHostInfo.ApplicationName;
        }
    }

    public class TheBlobData : TheDataBase
    {
        public byte[] BlobData { get; set; }
        public string BlobName { get; set; }
        public DateTimeOffset LastBlobUpdate { get; set; }
        public bool HasErrors { get; set; }
        public string MimeType { get; set; }
        public string BlobExtension { get; set; }
        public string ErrorMsg { get; set; }

        public object BlobObject { get; set; }
        public Action<TSM> BlobAction { get; set; }

        public TheBlobData CloneForSerial()
        {
            TheBlobData tD = new TheBlobData
            {
                BlobName = BlobName,
                LastBlobUpdate = LastBlobUpdate,
                HasErrors = HasErrors,
                MimeType = MimeType,
                BlobExtension = BlobExtension,
                ErrorMsg = ErrorMsg
            };
            return tD;
        }
    }
}
