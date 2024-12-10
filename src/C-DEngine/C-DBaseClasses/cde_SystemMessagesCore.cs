// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

#if !CDE_INTNEWTON //JSON.NET from Nuget
using Newtonsoft.Json;
#else
using cdeNewtonsoft.Json;
#endif
using System;
using System.Threading;

namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// This is a read-only enum class for the built-in services
    /// It can be used for plugin-services to as an alternative to the use of the MyBaseEngine.GetEngineName()
    /// </summary>
    public class eEngineName
    {
        /// <summary>
        /// ClassName of the ContentService responsible for all basic communication such as subscription management and blob transfers
        /// </summary>
        public const string ContentService = "ContentService";
        /// <summary>
        /// Classname of the NMI Engine
        /// </summary>
        public const string NMIService = "NMIService";
        /// <summary>
        /// Classname of the Thing Engine
        /// </summary>
        public const string ThingService = "ThingService";
        /// <summary>
        /// Define your own for your plugin: call "eEngineName MyPluginName=new eEngineName(classname);"
        /// instead of using "MyBaseEngine.GetEngineName() you could use MyPluginName.Name;
        /// </summary>
        public string Name;

        /// <summary>
        /// Constructor
        /// </summary>
        public eEngineName()
        {
            Name = "";
        }
        /// <summary>
        /// With new Engine Name
        /// </summary>
        /// <param name="pName"></param>
        public eEngineName(string pName)
        {
            Name = pName;
        }
    }

    /// <summary>
    /// Defines a list of EventNames that are fired by TheBaseEngine for specific events in the system
    /// </summary>
    public static class eLoggerCategory
    {
        /// <summary>
        /// Event fired by an IBaseEngine, Plugin or service
        /// </summary>
        public const string EngineEvent = "Engine Event";
        /// <summary>
        /// Event fired by a Rule
        /// </summary>
        public const string RuleEvent = "Rule Event";
        /// <summary>
        /// Logger for User related events
        /// </summary>
        public const string UserEvent = "User Event";
        /// <summary>
        /// Logger for Thing related events
        /// </summary>
        public const string ThingEvent = "Thing Event";
        /// <summary>
        /// Logger for NMI Changes
        /// </summary>
        public const string NMIAudit = "NMI Audit";

        /// <summary>
        /// Event that logs all new connections and disconnections of nodes the the current node
        /// </summary>
        public const string NodeConnect = "Node Connect";
    }

    /// <summary>
    /// Defines a list of EventNames that are fired by TheBaseEngine for specific events in the system
    /// </summary>
    public static class eEngineEvents
    {
        /// <summary>
        /// Fired sync before a resource is sent to the NMI. Can be used to manipulated the data sent to the NMI
        /// </summary>
        public const string BeforeResourcePush = "BeforeResourcePush";
        /// <summary>
        /// C-DEngine is about to shutdown. The MyServiceHostInfo.PreShutDownDelay sets the delay time for the shutdown. By default this is zero and the event is not fired
        /// </summary>
        public const string PreShutdown = "PreShutdown";
        /// <summary>
        /// Fires when the Engine received a Shut Down request "StopEngine()"
        /// </summary>
        public const string ShutdownEvent = "EngineWasShutdown";
        /// <summary>
        /// A new Subscription was received for the Engine
        /// </summary>
        public const string NewSubscription = "NewSubscription";
        /// <summary>
        /// RETIRED IN V4: No more channels
        /// </summary>
        public const string NewChannelActive = "NewChannelActive";
        /// <summary>
        /// RETIRED IN V4: No more channels
        /// </summary>
        public const string ChannelIsUpAgain = "ChannelIsUpAgain";
        /// <summary>
        /// RETIRED IN V4: No more channels
        /// </summary>
        public const string ChannelDown = "ChannelDown";
        /// <summary>
        /// RETIRED IN V4: No more channels
        /// </summary>
        public const string ChannelConnected = "ChannelConnected";
        /// <summary>
        /// This event is called when a plugin-service was set to Ready using the
        /// SetEngineReadiness() method
        /// </summary>
        public const string EngineIsReady = "EngineIsReady";
        /// <summary>
        /// This event is called when a plugin-service has "ProcessInitialized()"
        /// SetEngineReadiness() method
        /// </summary>
        public const string EngineInitialized = "EngineInitialized";
        /// <summary>
        /// Register a callback that will be called when the engine was started (via Start Engine)
        /// </summary>
        public const string EngineHasStarted = "EngineHasStarted";
        /// <summary>
        /// Register a callback to handle the event that is fired when the engine was stopped.
        /// For LiveEngine/Service data provider this only happens during shutdown of the hosting application
        /// For Not LiveEngines/Data Consumer this happens when the last communication channel was lost and no more channels are active
        /// </summary>
        public const string EngineHasStopped = "EngineHasStopped";
        /// <summary>
        /// Register a callback that will be called when a TSM was processed
        /// This allows for communication between plugin-services
        /// </summary>
        public const string MessageProcessed = "MessageProcessed";

        /// <summary>
        /// A plugin-service can subscribe to this event in order to update the costing for
        /// the telegram. The Event is called synchronously and should be used ONLY to
        /// update the costing of the Telegram
        /// </summary>
        /// <seealso cref="M:nsCDEngine.BaseClasses.TSM.AddCosting(nsCDEngine.BaseClasses.TSMCosting)">Add
        /// costing to a TSM</seealso>
        public const string CostingWasUpdated = "CostingWasUpdated";

        /// <summary>
        /// This is the main event handler for all incoming telegrams. It will be called if
        /// all underlying security and scoping conditions are met.
        /// </summary>
        /// <seealso cref="T:nsCDEngine.ViewModels.TheProcessMessage">The Process Message</seealso>
        public const string IncomingMessage = "IncomingMessage";

        /// <summary>
        /// Any Message sent to the Content-Relay Service that was not handled by the Content Service will be forwarded to this event
        /// </summary>
        public const string CustomTSMMessage = "CustomTSMMessage";
        /// <summary>
        /// Sent by the content Engine if a sever alert is issued. All subscribers should do the appropriate response
        /// </summary>
        public const string RedAlert = "RedAlert";
        /// <summary>
        /// Sent by the content Engine once the alert has been cleared. All subscribers should do the appropriate response
        /// </summary>
        public const string AllClear = "AllClear";

        /// <summary>
        /// Engine received a Blob Object
        /// </summary>
        public const string BlobReceived = "BlobReceived";

        /// <summary>
        /// Fired when a Thing of an Engine was updated
        /// </summary>
        public const string ThingUpdated = "ThingUpdated";

        /// <summary>
        /// NEWV4: Fired when RegisterThing has called the Init() function
        /// </summary>
        public const string ThingInitCalled = "ThingInitCalled";
        /// <summary>
        /// Fired when a Thing of an engine was Deleted from the Registry
        /// </summary>
        public const string ThingDeleted = "ThingDeleted";

        /// <summary>
        /// Fired when a new Thing of an engine was registereed
        /// </summary>
        public const string ThingRegistered = "ThingRegistered";

        /// <summary>
        /// Fired when a file is received by the C-DEngine. a plugin can register for this event and gets notified if the file was meant for the it
        /// </summary>
        public const string FileReceived = "FileReceived";

        /// <summary>
        /// Fires when a new chunk of a larger telegram was received
        /// </summary>
        public const string ChunkReceived = "ChunkReceived";

        /// <summary>
        /// Fires when the ContentService receives a CDE_CHECK4UPDATES request from another node.
        /// </summary>
        public const string Check4Updates = "Check4Updates";

        /// <summary>
        /// Can be fired on the ContentService to announce a new Event Log Events
        /// </summary>
        public const string NewEventLogEntry = "NewEventLogEntry";

        /// <summary>
        /// This event behaves similarly to eEngineEvents.IncomingMessage, but is also fired subsequently to eThingEvents.IncomingMessage.
        /// Any time either eThingEvents.IncomingMessage or eEngineEvents.IncomingMessage is fired, this event will also be fired.
        /// Be aware that subscribing to this event along with one (or more) of the other IncomingMessage events on the same Thing/engine may cause you to receive duplicate messages.
        /// </summary>
        public const string IncomingEngineMessage = "IncomingEngineMessage";
    }


    /// <summary>
    /// TSM = The System Message
    /// This is the frame telegram that is used to send all communication between nodes.
    /// </summary>
    [Serializable()]
    public partial class TSM
    {
        /// <summary>
        /// Text of the Message - used for Commands or other short message strings
        /// Maximum length for transmission is 476 Characters!
        /// </summary>
        public string TXT;
        /// <summary>
        /// Timestamp of the Message. Will be set to DateTimeOffset.Now on creation of the message but can be overwritten
        /// </summary>
        public DateTimeOffset TIM = DateTimeOffset.Now;
        /// <summary>
        /// 	<para>16 Flags for the message each bit has impact on how the message is
        /// transported or interpreted The default is zero (Off) on all flags. </para>
        /// 	<list type="bullet">
        /// 		<item>
        /// 			<description>Bit 1(1) = Do not Relay this message - it wil only be sent to the
        /// next node (use SetDoNotRelay(true/false) and DoNotRelay()) </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 2(2) = Unsubscribe the topics this message was sent with
        /// after the message was published (use SetUnsubscribeAfterPublish() and
        /// UnsubscribeAfterPublish()) </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 3(4) = Send the Message only to the Cloud and devices
        /// connected to the cloud but not inside to any on-premise services (use
        /// SetToCloudOnly() and ToCloudOnly()) </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 4(8) = Send this message to the Services (Data Provider)
        /// only. Messages with this flag are not sent to Client Nodes (Use
        /// SetToServiceOnly() and ToServiceOnly()) </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 5(16) = Invers of Bit 4: Send this message to Client Nodes
        /// only. Messages with this flag are not sent to Service Nodes (use
        /// SetToNodesOnly() and ToNodesOnly())</description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 6(32) = A message can be processed by multiple nodes. You can
        /// set this flag to tell later nodes that this message has been processed already
        /// (Use SetWasProcessed() and WasProcessed()) </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 7(64) = Similar to Bit6, this bit can be used by plug-ins for
        /// any custom use, recommendation is to use it for reply-messages to ack signaling
        /// (use SetAcknowledged() and Acknowledged()) </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 8(128) = Messages with this Flag set are only sent to the
        /// first Relay and not beyond. In most cases this is the FirstNode the current node
        /// is connected to (use SetToRelayOnly() and ToRelayOnly) </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 9(256) = Messages are placed in a Queue to be sent to the
        /// next nodes. If this flag is set, the message will NOT be placed in the Queue if
        /// it is already in the queue. Only the FIRST message found will be sent and later
        /// ones are discarded. The Message Hash is used to check if a message was found in
        /// the Queue. See GetHash() - (use SetNotToSendAgain() and NotToSendAgain())
        /// </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 10(512) = Similar to bit 9 a message this flag is used to
        /// only send one message. In opposite to Bit9, this flag ensures that only the
        /// LATEST message is sent (use SetNoDuplicates() and NoDuplicates()) </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 11(1024) = Enable Pulse Mode. If this bit is enabled, the
        /// Sender and Receiver switch to Adrenalin mode and try to speed up communication.
        /// Active Nodes immediately initiate another request to the connected node (Use
        /// SetSendPulse and SendPulse) </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 12(2048) = If is set if a Message was relayed over a cloud
        /// node. Only ready this bit, it wil be set by the C-DEngine automatically on the
        /// Cloud Node </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 13(4096) = WRITE-ONLY: This Flag tells the C-DEngine to
        /// Encrypt the PLS when the message is sent and automatically decrypted when it is
        /// received and before its sent to the "ProcessIncomingMessage" handler
        /// (use EncryptPLS()) </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 14(8192) = READ-ONLY: This flag can be read to determine if
        /// the PLS is encrypted (Use IsPLSEncrypted()) </description>
        /// 		</item>
        /// 		<item>
        /// 			<description>Bit 15 and 16 are reserved for future use</description>
        /// 		</item>
        /// 	</list>
        /// </summary>
        public ushort FLG;
        /// <summary>
        /// String Payload of the Message. In oposite to the TXT this string has no character limit
        /// </summary>
        public string PLS = "";         // PayLoad String
        /// <summary>
        /// Payload Binary of the Message. If PLB is NULL and PLS is set, the PLS will be compressed to binary and put in PLB for footprint reduction.
        /// If PLB is not NULL and PLS is NULL the C-DEngine will assume that the PLB contains a compressed string and uncompresses PLB to PLS automatically during receiving.
        /// If you want to send a custom PLB binary blob, set PLS to some description of the PLB content.
        /// </summary>
        public byte[] PLB;              // PayLoad Binary
        /// <summary>
        /// List of Originators of the Message separated by ;
        /// The first entry is always the "Birth" place or "Originator" of the message.
        /// The first entry can also optionally include the identifier (cdeMID) of the originating thing, which can be used to send a reply to the originator via TheCommCore.PublishToOriginator.
        /// The Last entry is always the "LastRelay" to transmit the message and is also seen as the "FirstNode" (from the point of view of the receiver)
        /// Note: The ORG field should not be manipulated or parsed directly, but only through the *Originator* methods on the TSM class.
        /// </summary>
        public string ORG = "";         //  Origin

        /// <summary>
        /// The GRO contains the reverse route back to the originator of a message. Nodes will try to send the message back along the route to the originator skipping other nodes in the mesh. If GRO is empty, the normal mesh publishing rules apply.
        /// </summary>
        public string GRO = ""; //Optimized Route to Origin
        /// <summary>
        /// Queue Priority
        /// The Lower the number the higher the priority of the telegram
        /// Default is 5
        /// QDX=0 will NOT be relayed via a cloud node and is reserved for real-time service-to-service communication between on-premise nodes
        /// </summary>
        public int QDX = 5;             // Queue Index: 0-2 Not Queued >0 Priority: Lower Number = Higher Prio; 20= Large Telegram Prio; 10=SQL Prio; 0-2=Expiration Message; 3= highest prio; 5=normal prio; 9=Only Send Last Telegram

        /// <summary>
        /// Message Content Level (see eMsgLevels)
        /// </summary>
        public eMsgLevel LVL = eMsgLevel.l4_Message;
        /// <summary>
        /// Engine (Plugin-Service Class) name that owns this message
        /// </summary>
        public string ENG = "";
        /// <summary>
        /// Serial Number of the Message.
        /// Do NOT set! This is managed by the C-DEngine
        /// </summary>
        public string FID = "";
        /// <summary>
        /// Scrambled Scope ID of this message.
        /// use TheCommonUtils.GetScrambledScopeID() to set this field
        /// This parameter is NEVER sent to a JSON/HTML5 client
        /// </summary>
        public string SID = "";         // Scrambled Scope ID
        /// <summary>
        /// Session ID corresponding to this message.
        /// DO NOT SET! The C-DEngine is managing this value
        /// This parameter is NEVER sent to a JSON/HTML5 client
        /// </summary>
        public string SEID = "";
        /// <summary>
        /// User GUID of this message.
        /// Can be used to validate messages on other nodes against centrally managed users
        /// This parameter is NEVER sent to a JSON/HTML5 client
        /// </summary>
        public string UID = "";
        /// <summary>
        /// Costing History of the Telegram
        /// </summary>
        public string CST = "";         // Costing

        /// <summary>
        /// The Thing Owner of the Message - should match the cdeO Meta Property
        /// </summary>
        public string OWN = "";

        /// <summary>
        /// Returns the amount of hobs this message was relayed through
        /// </summary>
        /// <returns></returns>
        public int HobCount()
        {
            if (string.IsNullOrEmpty(ORG)) return 0;
            return ORG.Split(';').Length;
        }

        /// <summary>
        /// If Set to True, the message will not be routed via the Cloud
        /// Use this for local intranet messages only
        /// </summary>
        /// <param name="how"></param>
        public void SetDoNotRelay(bool how)
        {
            if (how)
                FLG |= 1;
            else
                FLG &= 0xFFFE;
        }
        /// <summary>
        /// Probes if this flag is set
        /// </summary>
        /// <returns></returns>
        public bool DoNotRelay()
        {
            return (FLG & 1) != 0;
        }

        /// <summary>
        /// If Set to True, the subscription will be removed after publication
        /// This allows for Blitz Subscription and one-time-only subscriptions
        /// Once original sender has received the publication, it sends out a global CDE-UNSUBSCRIBE to the message Topic
        /// </summary>
        /// <param name="how"></param>
        public void SetUnsubscribeAfterPublish(bool how)
        {
            if (how)
                FLG |= 2;
            else
                FLG &= 0xFFFD;
        }
        /// <summary>
        /// Probes if this flag is set
        /// </summary>
        /// <returns></returns>
        public bool UnsubscribeAfterPublish()
        {
            return (FLG & 2) != 0;
        }


        /// <summary>
        /// If set to true, this message will only be sent via the cloud. Use this for system messages that only need to be sent to clients connected via the cloud
        /// </summary>
        /// <param name="how"></param>
        public void SetToCloudOnly(bool how)
        {
            if (how)
                FLG |= 4;
            else
                FLG &= 0xFFFB;
        }
        /// <summary>
        /// Probes if this flag is set
        /// </summary>
        /// <returns></returns>
        public bool ToCloudOnly()
        {
            return (FLG & 4) != 0;
        }

        /// <summary>
        /// Messages tagged with this flag will only be routed to a Service not to a Client or End Node
        /// </summary>
        /// <param name="how"></param>
        public void SetToServiceOnly(bool how)
        {
            if (how)
                FLG |= 8;
            else
                FLG &= 0xFFF7;
        }
        /// <summary>
        /// Probes if this flag is set
        /// </summary>
        /// <returns></returns>
        public bool ToServiceOnly()
        {
            return (FLG & 8) != 0;
        }

        /// <summary>
        /// Messages tagged with this flag will only be routed to nodes and not to services
        /// </summary>
        /// <param name="how"></param>
        public void SetToNodesOnly(bool how)
        {
            if (how)
                FLG |= 16;
            else
                FLG &= 0xFFEF;
        }
        /// <summary>
        /// Probes if this flag is set
        /// </summary>
        /// <returns></returns>
        public bool ToNodesOnly()
        {
            return (FLG & 16) != 0;
        }

        /// <summary>
        /// Set this flag if you want to make sure a message is not processed several times
        /// The C-DEngine is not using this flag internally - its for custom purpose only
        /// </summary>
        /// <param name="how"></param>
        public void SetWasProcessed(bool how)
        {
            if (how)
                FLG |= 32;
            else
                FLG &= 0xFFDF;
        }
        /// <summary>
        /// Probes if this flag is set
        /// </summary>
        /// <returns></returns>
        public bool WasProcessed()
        {
            return (FLG & 32) != 0;
        }

        /// <summary>
        /// Similar to the WasProcessed Flag this is for custom use only
        /// </summary>
        /// <param name="how"></param>
        public void SetAcknowledged(bool how)
        {
            if (how)
                FLG |= 64;
            else
                FLG &= 0xFFBF;
        }
        /// <summary>
        /// Probes if this flag is set
        /// </summary>
        /// <returns></returns>
        public bool Acknowledged()
        {
            return (FLG & 64) != 0;
        }


        /// <summary>
        /// If set, the message will not be relayed beyond the first Relay node.
        /// </summary>
        /// <param name="how"></param>
        public void SetToRelayOnly(bool how)
        {
            if (how)
                FLG |= 128;
            else
                FLG &= 0xFF7F;
        }
        /// <summary>
        /// Probes if this flag is set
        /// </summary>
        /// <returns></returns>
        public bool ToRelayOnly()
        {
            return (FLG & 128) != 0;
        }

        /// <summary>
        /// If Set to true, any older package with the same HashID will be sent and the newer one will be ignored
        /// This flag is working the oposite as NoDuplicates.
        /// Use this for large Binary files or result sets.
        /// </summary>
        /// <param name="how"></param>
        public void SetNotToSendAgain(bool how)
        {
            if (how)
                FLG |= 256;
            else
                FLG &= 0xFEFF;
        }
        /// <summary>
        /// Probes if this flag is set
        /// </summary>
        /// <returns></returns>
        public bool NotToSendAgain()
        {
            return (FLG & 256) != 0;
        }

        /// <summary>
        /// If set to true, older messages in the SenderQueue will be removed and only the newer one will be sent.
        /// This flag is working the oposite as NotToSendAgain.
        /// Use this for tiny fast telegrams that occur very often
        /// </summary>
        /// <param name="how"></param>
        public void SetNoDuplicates(bool how)
        {
            if (how)
                FLG |= 512;
            else
                FLG &= 0xFDFF;
        }
        /// <summary>
        /// Probes if this flag is set
        /// </summary>
        /// <returns></returns>
        public bool NoDuplicates()
        {
            return (FLG & 512) != 0;
        }

        /// <summary>
        /// If set to true, the sender expects a very fast response from the reveiver
        /// </summary>
        /// <param name="how"></param>
        public void SetSendPulse(bool how)
        {
            if (how)
                FLG |= 1024;
            else
                FLG &= 0xFBFF;
        }
        /// <summary>
        /// Probes if this flag is set
        /// </summary>
        /// <returns></returns>
        public bool SendPulse()
        {
            return (FLG & 1024) != 0;
        }

        internal void SetSendViaCloud()
        {
            FLG |= 2048;
        }

        /// <summary>
        /// Returns true if this message was sent via a Cloud Node
        /// </summary>
        /// <returns></returns>
        public bool WasSentViaCloud()
        {
            return (FLG & 2048) != 0;
        }

        /// <summary>
        /// Call this to Tell the C-DEngine to Encrypt the PLS automatically
        /// </summary>
        public void EncryptPLS()
        {
            FLG |= 4096;
        }
        internal bool ReadyToEncryptPLS()
        {
            return (FLG & 4096) != 0;
        }
        internal void ResetPLSEncryption()
        {
            FLG &= 0xDFFF;
        }
        internal void PLSWasEncrypted()
        {
            FLG |= 8192;
        }
        /// <summary>
        /// Probe if the PLS is encrypted
        /// </summary>
        /// <returns></returns>
        public bool IsPLSEncrypted()
        {
            return (FLG & 8192) != 0;
        }

        /// <summary>
        /// If true, the telegram must not be chunked
        /// </summary>
        /// <returns></returns>
        public bool DoNotChunk()
        {
            return (FLG & 16384) != 0;
        }

        /// <summary>
        /// Set to tell the Queue not to chunk this telegram
        /// </summary>
        public void SetNotToChunk()
        {
            FLG |= 16384;
        }

        /// <summary>
        /// DONT USE: This is the default JSON Deserialization Constructor
        /// </summary>
        /// <param name="sid"></param>
        [JsonConstructor]
        internal TSM(string sid)
        {
             SID = sid;
        }

        /// <summary>
        /// Add the current NodeScope to the TSM. If the node is unscoped "" will be set
        /// </summary>
        public TSM SetNodeScope()
        {
            SID = TheBaseAssets.MyScopeManager?.GetScrambledScopeID();                //GRSI: super high frequency - not in unscoped cloud
            if (!string.IsNullOrEmpty(TheBaseAssets.MyScopeManager?.FederationID))
                SID += ";" + TheBaseAssets.MyScopeManager?.FederationID;
            return this;
        }

        /// <summary>
        /// Sets the Originator of the TSM to the Local Host DeviceID (TheBaseAssets.MyServiceHostInfo.MyDeviceInfo)
        /// </summary>
        private void SetOrigin(bool DontSetScope = false)
        {
            if (!TheBaseAssets.MasterSwitch) // || TheBaseAssets.MyScopeManager == null) //new 4.302: There might not be a scope Manager
            {
                ORG = "NOT RUNNING";
                return;
            }
            if (TheBaseAssets.MyServiceHostInfo != null && TheBaseAssets.MyServiceHostInfo.MyDeviceInfo != null)
                ORG = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString();
            else
                ORG = "NOT SET YET";
            if (!DontSetScope)
                SetNodeScope();
        }

        /// <summary>
        /// Creates a new TSM with a given Engine, Message Text and Message Level.
        /// ATTENTION: Only use this Constructor for WriteToLog() entries.
        /// ATTENTION: This constructor does NOT set the SID by default to avoid performance hit for SID creation
        /// To change this behavior you have to set "DisableFastTSMs=true" in the App.config
        /// </summary>
        /// <param name="tEngine">Engine Name</param>
        /// <param name="pText">Command Text</param>
        /// <param name="pLevel">Priority Level of the Message</param>
        public TSM(string tEngine, string pText, eMsgLevel pLevel)
        {
            ENG = tEngine;
            TXT = pText;
            LVL = pLevel;
            SetOrigin((TheBaseAssets.MyServiceHostInfo?.DisableFastTSMs) != true);
        }

        private static long SerialSID = 1;
        /// <summary>
        /// Increase the FID Serial Number by one
        /// </summary>
        /// <param name="pSubMsg"></param>
        public void GetNextSerial(int pSubMsg)
        {
            if (string.IsNullOrEmpty(FID))
            {
                var newSerial = Interlocked.Increment(ref SerialSID);
                FID = (newSerial).ToString();
            }
            else
            {
                if (!FID.Contains(".") && pSubMsg != 0)
                    FID += "." + pSubMsg;
            }
        }

        /// <summary>
        /// A Macro testing the LVL of a message
        /// If the pLogLevel is greater or equal than the DebugLevel, the macro return true
        /// </summary>
        /// <param name="pLogLevel"></param>
        /// <returns></returns>
        public static bool L(eDEBUG_LEVELS pLogLevel)
        {
            if (TheBaseAssets.MyServiceHostInfo != null && TheBaseAssets.MyServiceHostInfo.DebugLevel > pLogLevel)
                return false;
            else
                return true;
        }

        internal static string GetScrambledIDFromTopic(string pScopedTopic, out string TopicName)
        {
            TopicName = pScopedTopic;
            if (string.IsNullOrEmpty(pScopedTopic)) return "";
            string[] tt = pScopedTopic.Split('@');
            TopicName = tt[0];
            if (tt.Length > 1)
                return tt[1];
            else
                return "";
        }
    }
}
