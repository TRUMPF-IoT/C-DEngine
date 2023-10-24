// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System.Collections.Generic;
using nsCDEngine.Engines.ThingService;

namespace nsCDEngine.Engines
{
    /// <summary>
    /// This is the main Interface for Plugin-Service management.
    /// It abstracts the MyBaseEngine and should always be used from Plugin-Services instead of directly calling the MyBaseEngine methods.
    /// </summary>
    /// <remarks></remarks>
    public interface IBaseEngine
    {

        /// <summary>
        /// Call this function to tell the C-DEngine that the plugin-service is ready to take connection (if its a service and live) or that it has received a working connection.
        /// </summary>
        /// <param name="pReady">If set to <see langword="true"/>, if the plugin-service is ready.</param>
        /// <param name="pOriginator">reserved for future use</param>
        /// <remarks></remarks>
        void SetEngineReadiness(bool pReady, TheChannelInfo pOriginator);
        /// <summary>
        /// Tells the C-DEngine if this Plugin-Service is a Service/Data-Provider or a Data-Consumer
        /// </summary>
        /// <param name="pIsService">Set to true if the plugin-service provides data</param>
        void SetEngineService(bool pIsService);
        /// <summary>
        /// Tells the C-DEngine that this Plugin-Service has been initialized.
        /// In most cases of loosly coupled service, this is not really necessary, but some data consumer require a tighter connection to their data-provider and want to make sure the connection is properly established and initialized.
        /// The StorageService is such as more tighter connected service.
        /// </summary>
        void SetIsInitializing();
        /// <summary>
        /// If a Plugin-Service sets this to true, it can establish one-to-many connection.
        /// This allows for mutliple data-provider to talk to one data consumer and one data-provider can have multiple data consumers connected at the same time.
        /// If set to false, the connection will always be a one-to-one connection and additional provider or consumer will be put in a redundancy list.
        /// If the current connection fails, the service will automatically connect to the next service in the redundancy list.
        /// Be aware: MultiChannels in a Mesh Based environment with many nodes can cause a lot of traffic between the nodes
        /// </summary>
        /// <param name="pIsMultiChannel">Set to true is you want to designate this plugin-service to have multi-channel</param>
        //void SetMultiChannel(bool pIsMultiChannel);
        /// <summary>
        /// 	<para>Setting this flag to true tells the C-DEngine that this plugin does not
        /// contain any code and only relays properly scoped telegrams between nodes. This
        /// is very useful in scenarios where you have multiple relays on-premise that need
        /// to just relay telegrams but do not add any value/services to the telegrams. The
        /// C-DEngine supports a configuration setting in the APP.CONFIG that tells the
        /// C-DEngine just to relay telegrams with a certain topic: </para>
        /// 	<para>&lt;add key="RelayOnly" value="<font color="green">Topics
        /// Separated with</font>;" /&gt;</para>
        /// 	<para>For Example:</para>
        /// 	<para>&lt;add key="RelayOnly" value="<font color="green">CDMyInformation.TheInfoService</font>" /&gt;</para>
        /// 	<para>This setting would tell the C-DEngine to relay all telegrams tagged with
        /// "CDMyInformation.TheInfoService" to all other connected node. </para>
        /// </summary>
        /// <param name="pIsRelay">If set to true, the C-DEngine will relay all telegrams
        /// tagged with the ClassName to connected nodes</param>
        void SetIsMiniRelay(bool pIsRelay);

        /// <summary>
        /// Sets the isolation permission for a plugin Service. ALL services in one Plugin DLL have to use the same setting
        /// </summary>
        /// <param name="AllowIsolation">Allows the plugin to be isolated in a child process</param>
        /// <param name="AllowNodeHopp">Allows the C-DEngine to move a plugin to a different node. If AllowIsolation is false, this parameter is ignored</param>
        void SetIsolationFlags(bool AllowIsolation, bool AllowNodeHopp = false);
        /// <summary>
        /// RETIRED IN V4: DO NOT USE - ALWAYS RETURNS FALSE
        /// </summary>
        /// <returns></returns>
        bool HasChannels();
        /// <summary>
        /// Updates an interal object containing the complete state of the plugin-service.
        /// This is used by the ISM service for service health monitoring
        /// The state contains a list of all valid subscriptions to this service.
        /// Since subscriptions can be scoped a valid scope ID has to be provided.
        /// </summary>
        /// <param name="pScrambledScopeID">A scrambled ScopeID to list corresponding subscriptions. If empty, only anonymous and generic subscriptions are recorded</param>
        void UpdateEngineState(string pScrambledScopeID);

        /// <summary>
        /// Returns the current state of this Engine (Service Plugin)
        /// </summary>
        /// <returns></returns>
        TheEngineState GetEngineState();

        /// <summary>
        /// Gets/Sets the last message in the Engine State
        /// </summary>
        string LastMessage { get; set; }

        /// <summary>
        /// Starts the plugin-service with a new ChannelDefinition. If the engine was
        /// already started, the new channel will be added to the redundancy list. If the
        /// engine is a multi-channel the channel will be immediately activated
        /// </summary>
        /// <param name="pURLs"></param>
        /// <seealso cref="T:nsCDEngine.ViewModels.TheChannelInfo">The Channel
        /// Information</seealso>
        bool StartEngine(TheChannelInfo pURLs);
        /// <summary>
        /// This function will stop a plugin-service and stop all communication channels currently active
        /// </summary>
        void StopEngine();

        /// <summary>
        /// Returns the Guid of the main NMI Dashboard of the plugin-service
        /// </summary>
        /// <returns>Guid (cdeMID) of the DashBoard</returns>
        Guid GetDashboardGuid();

        /// <summary>
        /// Returns the string of the main NMI Dashboard of the plugin-service
        /// </summary>
        /// <returns>string of the Guid (cdeMID) of the DashBoard - can have launch parameters separated by ;:;</returns>
        string GetDashboard();
        /// <summary>
        /// Sets the guid of the main NMI Dashboard. Although a GUID syntax is required, the parameter will be run through the "GenerateFinalStr()" method in TheCommonUtils class.
        /// This allows for marcro definition in the string that will be resolved at runtime.
        /// </summary>
        /// <param name="pDash">Guid (cdeMID) pointing at the main TheDashboardInfo in TheBaseAssets.MyApplication.MyNMIModel.MyDashboards</param>
        void SetDashboard(string pDash);
        /// <summary>
        /// Sets the Engine Name (ClassName) for the Plugin-Service This call is MANDATORY
        /// and should be called in the "SetBaseEngine()" function of the plugin
        /// </summary>
        /// <param name="pName">ClassName of the plugin-service</param>
        /// <seealso cref="M:nsCDEngine.Engines.ICDEPlugin.SetBaseEngine(nsCDEngine.Engines.IBaseEngine)">SetBaseEngine
        /// Call</seealso>
        void SetEngineName(string pName);
        /// <summary>
        /// Sets the Type of the Plugin-Service
        /// This is required if resources need to be loaded by the C-DEngine that are located in the Plugin
        /// </summary>
        /// <param name="pType"></param>
        void SetEngineType(Type pType);
        /// <summary>
        /// Indicates which device types this Plugin-Service supports
        /// </summary>
        /// <param name="pDeviceTypeInfo"></param>
        void SetDeviceTypes(List<TheDeviceTypeInfo> pDeviceTypeInfo);
        /// <summary>
        /// Gets the device types supported by this Plugin-Service
        /// </summary>
        /// <returns></returns>
        List<TheDeviceTypeInfo> GetDeviceTypes();
        /// <summary>
        /// Returns the ClassName of the plugin-service set with the "SetEngineName" call.
        /// </summary>
        /// <returns></returns>
        string GetEngineName();
        /// <summary>
        /// Optionally this call can be used to give a friendly name to the Engine.
        /// This name is used by the ISM service if its set, otherwise the ClassName is used.
        /// </summary>
        /// <param name="pName">Human readable name for the plugin-service</param>
        void SetFriendlyName(string pName);
        /// <summary>
        /// Retrieves the friendly name of the Plugin-Service
        /// </summary>
        /// <returns>The Friendly name given to the Plugin</returns>
        string GetFriendlyName();
        /// <summary>
        /// Unique Guid for the Engine. This can be used for versioning management and plugin-identification in case two plugins with the same ClassName exist in one solution.
        /// </summary>
        /// <param name="pID">Unique Guid for the plugin-service</param>
        void SetEngineID(Guid pID);
        /// <summary>
        /// retrieves the unique ID of the Engine given by the SetEngineID call
        /// </summary>
        /// <returns>The Guid of the Engine</returns>
        Guid GetEngineID();

        /// <summary>
        /// Sets the StatusLevel property on the Engine and colors the status light in the "Status" Box.
        /// 0 = Idle (not active/Gray)
        /// 1 = All ok (Green)
        /// 2 = Warning (Yellow)
        /// 3 = Error (Red)
        /// 4 = Rampup (Blue)
        /// 5 = Design / Engineering / Configuration (Brown)
        /// 6 = Shutdown (Violett)
        /// 7 = Unknown/Unreachable (black)
        /// </summary>
        /// <param name="pLevel"></param>
        void SetStatusLevel(int pLevel);
        /// <summary>
        /// Returns the Guid (DeviceID) of currently active Channel the Engine is connected to
        /// If the plugin-service is a Multi-Channel one active channels is returned
        /// </summary>
        /// <returns>Guid of all active channels separated by ;</returns>
        Guid GetFirstNode();
        /// <summary>
        /// The C-DEngine is tracking the communication costs of all telegrams going through a plugin-service.
        /// This methods allows to retrieve the currently recorded communication costs since the start of the service.
        /// </summary>
        /// <returns>Amount of communication credits</returns>
        long GetCommunicationCosts();
        /// <summary>
        /// Sets a string representation of the Version of the Plugin-Service
        /// This version is represented in the ISM
        /// </summary>
        /// <param name="pVersion">Readable Version number of the plugin-service</param>
        void SetVersion(double pVersion);
        /// <summary>
        /// Returns the version set with "SetVersion"
        /// </summary>
        /// <returns>current version number of the plugin-service</returns>
        string GetVersion();
        /// <summary>
        /// Sets the minimum version of the C-DEngine required by the plug-in
        /// </summary>
        /// <param name="pVersion"></param>
        void SetCDEMinVersion(double pVersion);
        /// <summary>
        /// Returns the minimim version of the C-DEngine required by the plug-in as set with SetCDEMinVersion.
        /// </summary>
        /// <returns></returns>
        string GetCDEMinVersion();
        /// <summary>
        /// Plug-in call this from their InitEngineAssets() method to indicate that they require an activated license
        /// </summary>
        void SetIsLicensed(bool generateDefaultLicense = true, string[] LicenseAuthorities = null);

        /// <summary>
        /// Callled when a new version of this plugin is available
        /// </summary>
        /// <param name="pVersion">New Version Number</param>
        void SetNewVersion(double pVersion);

        /// <summary>
        /// Returns a stream for the requested Resource
        /// The Resource Name can be case insensitive
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        byte[] GetPluginResource(string pName);

        /// <summary>
        /// Returns a stream for the requested Resource and inserts it into TheRequestData.
        /// If TheRequestData contains a SessionState and its LCID is not zero, the function tries to load it first from the /ClientBin/{LCID}/... branch
        /// This function also sets the MimeType and the StatePush variable.
        /// The Resource Name is case insensitive
        /// </summary>
        /// <param name="pName"></param>
        /// <returns>True if the resource was found</returns>
        bool GetPluginResource(TheRequestData pName);

        /// <summary>
        /// Defines all Plug-in Information for the plugin Store
        /// </summary>
        /// <param name="LongDescription"></param>
        /// <param name="pPrice">Retail Price - Default ZERO</param>
        /// <param name="pHomeUrl">Custom Home Page - default /ServiceID</param>
        /// <param name="pIconUrl">Custom Icon - default /ServiceID/icon.png</param>
        /// <param name="pDeveloper">Name of the developer of the plug-in</param>
        /// <param name="pDeveloperUrl">URL to the developer homepage</param>
        /// <param name="pCategories">Search categories for the plugin</param>
        /// <param name="CopyRights"></param>
        void SetPluginInfo(string LongDescription, double pPrice, string pHomeUrl, string pIconUrl, string pDeveloper, string pDeveloperUrl, List<string> pCategories, string CopyRights = null);

        /// <summary>
        /// Adds a capability to the plugin. Can be requested by another plugin to see if compatible behavior is avaiable.
        /// </summary>
        /// <param name="pCapa">An Integer with the pluginCapa. C-Labs will define capabilities smaller 1000; above 1000 is open for custom capas</param>
        void AddCapability(eThingCaps pCapa);

        /// <summary>
        /// Checks if a Plugin has the requested eThingsCapability
        /// </summary>
        /// <param name="pCapa">Capability to test for</param>
        /// <returns>True if the Plugin has this capability</returns>
        bool HasCapability(eThingCaps pCapa);

        /// <summary>
        /// Adds a list of all files that belong to a plugin.
        /// </summary>
        /// <param name="pList"></param>
        void AddManifestFiles(List<string> pList);
        /// <summary>
        /// Adds a platforms supported by the plugin
        /// </summary>
        /// <param name="pPlatformList"></param>
        void AddPlatforms(List<cdePlatform> pPlatformList);
        /// <summary>
        /// Returns the plugin info of this Plugin/Service
        /// </summary>
        /// <returns></returns>
        ThePluginInfo GetPluginInfo();
        /// <summary>
        /// This function will be called by the C-DEngine before a telegram is sent to the next nodes.
        /// </summary>
        /// <param name="pMessage"></param>
        void UpdateCosting(TSM pMessage);

        /// <summary>
        /// Quick shortcut for the plugin-service or the application host to process a TSM localy.
        /// The Topic parameter of the TheProcessMessage will be automatically set to the ENG parameter of the TSM
        /// If the application is scoped, the message will be automatically scoped with the internal ScopeID
        /// </summary>
        /// <param name="pMessage">TSM to be processed by the plugin-service.</param>
        void ProcessMessage(TSM pMessage);
        /// <summary>
        /// Quick shortcut for the plugin-service or the application host to process a TSM localy.
        /// If the application is scoped, the message will be automatically scoped with the internal ScopeID
        /// Additionally a local callback can be specified in TheProcessMessage that will be called when processing is done
        /// The returning TSM in the callback is not necessarily the same as the incoming TSM. This can be overwritten by the plugin-service during processing.
        /// </summary>
        /// <param name="pMessage">TheProcess Message to be processed by the plugin-service</param>
        /// <seealso cref="T:nsCDEngine.ViewModels.TheProcessMessage">The Process
        /// Message</seealso>
        void ProcessMessage(TheProcessMessage pMessage);

        /// <summary>
        /// This function allows to send a TSM only to the attached nodes of a plugin-service
        /// In general we recommend using the TheCoreComm.PublishCentral() or TheCoreCommPublishToFirstNode() calls
        ///
        /// </summary>
        /// <param name="pMessage">TSM to be sent to the nodes</param>
        /// <param name="pLocalCallback">A local callback that can return a result TSM. This TSM is coming ONLY from the local node not from remote nodes</param>
        /// <returns></returns>
        bool PublishToChannels(TSM pMessage, Action<TSM> pLocalCallback);
        /// <summary>
        /// Sends CDE_INITIALIZE either to all nodes in order to find the proper service to handle Data Requests
        /// </summary>
        void InitAndSubscribe();
        /// <summary>
        /// Sends CDE-SUBSCRIBE to a given channel.
        /// A subscription to a topic tells the connected node to relay information regarding this topic to the subscriber.
        /// This subscribe function will send the ClassName of the plugin-service to the designated channel and expects publications to it.
        /// If the Application has an active scope the subscription will be scoped as well. Preventing unwanted and unauthorized telegrams to be sent to this plugin-service
        /// </summary>
        void Subscribe();
        /// <summary>
        /// LiveEngines/Data providing services can reply "CDE_INITIALIZED" to a node that requested "CDE_INITIALIZE" using this convenience method.
        /// The TSM is necessary to identify the originator of the CDE_INITIALIZE message.
        /// </summary>
        /// <param name="pMessage">Only the ORG parameter of the TSM is used</param>
        void ReplyInitialized(TSM pMessage);
        /// <summary>
        /// Call this method to tell the C-DEngine that the service was initialized.
        /// </summary>
        void ProcessInitialized();
        /// <summary>
        /// If you handle the CDE_INITIALIZE message, tell the C-DEngine when you are done with the process.
        /// ReplyInitialized is calling this method internally. This also sets the Engine Readiness to "true"
        /// </summary>
        /// <param name="pMessage">Contain the ORG (Originator) of the Request to Initialize</param>
        void SetInitialized(TSM pMessage);

        /// <summary>
        /// call this method to trigger the MessageProcessed events.
        /// </summary>
        /// <param name="pMessage">TSM to be send via the MessageRegistered event</param>
        void MessageProcessed(TSM pMessage);

        /// <summary>
        /// New in V3.0: Returns the ICDEThing of this Engine
        /// </summary>
        /// <returns></returns>
        ICDEThing GetThingInterface();
        /// <summary>
        /// New in V3.0: Returns TheThing of this Engine
        /// </summary>
        /// <returns></returns>
        TheThing GetBaseThing();

        /// <summary>
        /// New in V3.0: Registers an Event with this Engines Base Thing (short for this.GetBaseThing().RegisterEvent() but verifies that the BaseThing is valid and active)
        /// </summary>
        /// <param name="pEventName"></param>
        /// <param name="pCallback"></param>
        Action<ICDEThing, object> RegisterEvent(string pEventName, Action<ICDEThing, object> pCallback);
        /// <summary>
        /// New in V3.0: Unregisters an Event with this Engines Base Thing (short for this.GetBaseThing().UnregisterEvent() but verifies that the BaseThing is valid and active)
        /// </summary>
        /// <param name="pEventName"></param>
        /// <param name="pCallback"></param>
        void UnregisterEvent(string pEventName, Action<ICDEThing, object> pCallback);
        /// <summary>
        /// New in V3.0: allows to fire an event on this engine (short for this.GetBaseThing().FireEvent() but verifies that the BaseThing is valid and active)
        /// </summary>
        /// <param name="pEventName"></param>
        /// <param name="pPara"></param>
        /// <param name="FireAsync"></param>
        void FireEvent(string pEventName, object pPara, bool FireAsync);

        /// <summary>
        /// Used to register a JavaScript Engine for this Base Engine. The callback requires to deliver back the javascript engine. The engine can decide how to deliver back the engine.
        /// The C-DEngine will help with "MyBaseEngine.GetPluginResource(pRequest)". This function can be called in the callback if the JavaScript engine is located in the ClientBin Folder and named exactly like the name given with "SetEngineName()"
        /// </summary>
        /// <param name="sinkInterceptHttp"></param>
        void RegisterJSEngine(Action<TheRequestData> sinkInterceptHttp);


        /// <summary>
        /// New 3.2: Allows to register a CSS File for a plugin. The function can be called multiple times to register more than one CSS File
        /// </summary>
        /// <param name="cssDarkPath">Path to the Dark Scheme CSS</param>
        /// <param name="cssLitePath">Path to the Lite Scheme CSS if null the Dark will be used for both schemes</param>
        /// <param name="sinkInterceptHttp">Callback to provide the CSS</param>
        void RegisterCSS(string cssDarkPath, string cssLitePath, Action<TheRequestData> sinkInterceptHttp);


        /// <summary>
        /// Return the current ISOLater Object of the Base Engine.
        /// </summary>
        /// <returns></returns>
        object GetISOLater();

        /// <summary>
        /// New in V4.0083: Returns TheBaseEngine Object assosicated with this Interface
        /// </summary>
        /// <returns></returns>
        TheBaseEngine GetBaseEngine();
    }
}
