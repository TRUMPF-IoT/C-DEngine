// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿namespace nsCDEngine.Engines
{
    /// <summary>
    /// The ICDEPlugin interface contains all methods a C-DEngine Plugin-Service has to provide in order to be hosted in the C-DEngine
    /// </summary>
    public interface ICDEPlugin
    {
        /// <summary>
        /// Returns the IBaseEngine Interface to the C-DEngine when needed. This method
        /// might become obsolete in the future but has to be provided for now
        /// </summary>
        /// <seealso cref="T:nsCDEngine.Engines.IBaseEngine">IBaseEngine Interface</seealso>
        IBaseEngine GetBaseEngine();
        /// <summary>
        /// The C-DEngine will call this function to give a reference to the IBaseEngine Interface of the MyBaseEngine to the plugin-service
        /// The IBaseEngine contains many methods for communication management
        /// Use this method to initialize all your assets that are NOT depending on other Plugins or Engines
        /// You cannot assume that any other engine is running at the execution time of this method
        /// </summary>
        /// <param name="pEngine">The IBaseEngine Interface used by the Plugin-Service. Store this in a private variable and return it in the GetBaseEngine call</param>
        /// <seealso cref="M:nsCDEngine.Engines.IBaseEngine.RegisterEngineStarted(System.Action)">Engine
        /// started event</seealso>
        void InitEngineAssets(IBaseEngine pEngine);
    }
}
