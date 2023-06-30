// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// The main interface for Things in the C-DEngine. All Things the C-DEngine should know about has to have the ICDEThing Interface
    /// </summary>
    public interface ICDEProperty
    {
        /// <summary>
        /// Get a property of TheThing associated with this class
        /// </summary>
        /// <param name="pName">Name of the property to get</param>
        /// <param name="CreateIfNotExist">The property will be created in TheThing if it does not exist</param>
        /// <returns></returns>
        cdeP GetProperty(string pName, bool CreateIfNotExist);

        /// <summary>
        /// Sets a property of TheThing associated with this class
        /// </summary>
        /// <param name="pName">Name of the property to set</param>
        /// <param name="Value">The value to set in the property</param>
        /// <returns></returns>
        cdeP SetProperty(string pName, object Value);

        /// <summary>
        /// Returns all Properties of the object with the ICDEProperty interface
        /// </summary>
        /// <returns></returns>
        List<cdeP> GetAllProperties();
        /// <summary>
        /// Returns all Properties starting with a prefix of the object with the ICDEProperty interface
        /// </summary>
        /// <param name="pName">Prefix of properties to look for</param>
        /// <returns></returns>
        List<cdeP> GetPropertiesStartingWith(string pName);
        /// <summary>
        /// Returns all Properties starting with a prefix in the cdeM field of the property to return of the object with the ICDEProperty interface
        /// </summary>
        /// <param name="pName">Prefix in the cdeM to return</param>
        /// <returns></returns>
        List<cdeP> GetPropertiesMetaStartingWith(string pName);

        /// <summary>
        /// Removes a property from TheThing at Runtime
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        cdeP RemoveProperty(string pName);

        /// <summary>
        /// Removes all properties with a given prefix from TheThing at Runtime
        /// Only properties directly attached to a this Property will be deleted.
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        bool RemovePropertiesStartingWith(string pName);

        /// <summary>
        /// Registers a new property with TheThing at runtime
        /// </summary>
        /// <param name="pName">Name of the Property to be registered.</param>
        /// <returns>The registered property</returns>
        cdeP RegisterProperty(string pName);

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="pType">The Type of the Property</param>
        /// <returns></returns>
        cdeP SetProperty(string pName, object pValue, ePropertyTypes pType);
        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="EventAction">Defines what events should be fired</param>
        /// <param name="pOnChangeEvent">a callback for a local (current node) onChangeEvent</param>
        /// <returns></returns>
        cdeP SetProperty(string pName, object pValue, int EventAction, Action<cdeP> pOnChangeEvent = null);

        /// <summary>
        /// Sets multiple properties at once. Use with the Historian feature for a consistent snapshot that has all these property changes.
        /// If any of the properties do not exist, they will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="nameValueDictionary"></param>
        /// <param name="sourceTimestamp"></param>
        /// <returns></returns>
        bool SetProperties(IDictionary<string, object> nameValueDictionary, DateTimeOffset sourceTimestamp);

        /// <summary>
        /// Registers a new OnPropertyChange Event Callback
        /// </summary>
        /// <param name="OnUpdateName">Name of the property to monitor</param>
        /// <param name="oOnChangeCallback">Callback to be called when the property changes</param>
        /// <returns></returns>
        bool RegisterOnChange(string OnUpdateName, Action<cdeP> oOnChangeCallback);

        /// <summary>
        /// This function allows to declare a secure Property
        /// Secure Properties are stored encrypted and can only be decrypted on nodes with the same ApplicationID and SecurityID.
        /// These properties are sent encrypted via the mesh. JavaScript clients CANNOT decrypt the value of the property!
        /// </summary>
        /// <param name="pName"></param>
        /// <param name="pType">In V3.2 only STRING can be declared secure.</param>
        /// <returns></returns>
        cdeP DeclareSecureProperty(string pName, ePropertyTypes pType);

        /// <summary>
        /// Return the property type of a given property name. Returns ZERO if the property does not exist or the name is null/""
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        ePropertyTypes GetPropertyType(string pName);
    }

        /// <summary>
        /// The main interface for Things in the C-DEngine. All Things the C-DEngine should know about has to have the ICDEThing Interface
        /// </summary>
    public interface ICDEThing 
    {
        /// <summary>
        /// Add a reference to TheThing, that will be used for serialization and communication.
        /// Basically TheThing is all the Mesh and storage persistent data of the class with the ICDEThing interface
        /// </summary>
        /// <param name="pThing"></param>
        void SetBaseThing(TheThing pThing);

        /// <summary>
        /// Returns the current TheThing belonging to this class
        /// </summary>
        /// <returns></returns>
        TheThing GetBaseThing();

        /// <summary>
        /// Get a property of TheThing associated with this class
        /// </summary>
        /// <param name="pName">Name of the property to get</param>
        /// <param name="CreateIfNotExist">The property will be created in TheThing if it does not exist</param>
        /// <returns></returns>
        cdeP GetProperty(string pName, bool CreateIfNotExist);

        /// <summary>
        /// Sets a property of TheThing associated with this class
        /// </summary>
        /// <param name="pName">Name of the property to set</param>
        /// <param name="Value">The value to set in the property</param>
        /// <returns></returns>
        cdeP SetProperty(string pName, object Value);

        /// <summary>
        /// This function will be called by TheThingRegistry when "RegisterThing" is called
        /// It is possible that TheThingRegistry calls this method multiple times. In fact when ever a new TheThing is registered with TheThingRegistry, all Things that reply "IsInit()=false" will be called again.
        /// </summary>
        /// <returns>true if initialization has completed and the Thing is ready to be used.
        /// false if initialization is continuing asynchronously, in which case the Thing must fire the eThingEvents.Initialized or eEngineEvents.EngineIntialized for plug-ins) once initialization completes.</returns>
        bool Init();

        /// <summary>
        /// The method needs to return true, if the class has already been initialized.
        /// </summary>
        /// <returns></returns>
        bool IsInit();

        /// <summary>
        /// Called by the cdeEngine when the Thing has been deleted from the Thing Registry.
        /// </summary>
        /// <returns>true if the deletion is done. false is deletion is proceeding asynchronous, in which case the Thing must fire the eThingEvents.Deleted event once deletion completes.</returns>
        bool Delete();


        /// <summary>
        /// This function is called by the NMI Engine and allows TheThing to create its UX Model
        /// The NMI Model is calling this method until IsUXInit returns true
        /// </summary>
        bool CreateUX();

        /// <summary>
        /// Similar to the IsInit Function IsUXInit should return true if the UX has been created.
        /// </summary>
        /// <returns></returns>
        bool IsUXInit();

        /// <summary>
        /// This is the main message Handler of TheThing. All incoming messages directed at this thing will be routed through this handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="pMsg"></param>
        void HandleMessage(ICDEThing sender, object pMsg);

        /// <summary>
        /// New in V2.1 - Allows for other Plugins and Hosting Applications to listen to certain event.
        /// </summary>
        /// <param name="pEventName">Name of the event you want to listen to</param>
        /// <param name="pCallback">Callback of the event.</param>
        void RegisterEvent(string pEventName, Action<ICDEThing, object> pCallback);
        /// <summary>
        /// Unregisters a previously registered event
        /// </summary>
        /// <param name="pEventName">Event Name</param>
        /// <param name="pCallback">Previously registered callback</param>
        void UnregisterEvent(string pEventName, Action<ICDEThing, object> pCallback);
        /// <summary>
        /// Fires a specific event and calls all registered callbacks
        /// </summary>
        /// <param name="pEventName">EventName</param>
        /// <param name="sender">Sender of this Event- Should be in most cases "this"</param>
        /// <param name="parameter">Parameter as an object. The fire Event can decide on the object type and the callback has to </param>
        /// <param name="FireAsync">if true, the event is fired asynchronously and returns right away</param>
        void FireEvent(string pEventName,ICDEThing sender, object parameter, bool FireAsync);
        /// <summary>
        /// To optimize runtime performance, this call can be used to check if there are any events registered under the EventName
        /// </summary>
        /// <param name="pEventName">EventName to check for</param>
        /// <returns>returns true if there are registered events</returns>
        bool HasRegisteredEvents(string pEventName);

    }
}
