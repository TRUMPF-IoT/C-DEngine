// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.ViewModels;
using System;

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// The main interface for Things in the C-DEngine. All Things the C-DEngine should know about has to have the ICDEThing Interface
    /// </summary>
    public interface ICDEThing
    {
        /// <summary>
        /// Add a reference to TheThing, that will be used for serialization and communication.
        /// Basically TheThing is all the Mesh and storage persistant data of the class with the ICDEThing interface
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
        /// It is possible that TheThingRegistry calls this method multiple times. In fact when ever a new TheThing is registerd with TheThingRegistry, all Things that reply "IsInit()=false" will be called again.
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
