// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.ViewModels;
using System;

namespace nsCDEngine.Interfaces
{
    /// <summary>
    /// Interface for Event based classes (i.e. TheDataBase)
    /// </summary>
    public interface ICDEEvents
    {
        /// <summary>
        /// Fires an event
        /// </summary>
        /// <param name="pEventName"></param>
        /// <param name="pMsg"></param>
        /// <param name="FireAsync"></param>
        /// <param name="pFireEventTimeout"></param>
        void FireEvent(string pEventName, TheProcessMessage pMsg = null, bool FireAsync = true, int pFireEventTimeout = 0);

        /// <summary>
        /// Registers a callback for an event
        /// </summary>
        /// <param name="pEventName"></param>
        /// <param name="pCallback"></param>
        /// <returns></returns>
        Action<TheProcessMessage, object> RegisterEvent2(string pEventName, Action<TheProcessMessage, object> pCallback);

        /// <summary>
        /// Unregisters a callback from an event
        /// </summary>
        /// <param name="pEventName"></param>
        /// <param name="pCallback"></param>
        /// <returns></returns>
        bool UnregisterEvent2(string pEventName, Action<TheProcessMessage, object> pCallback);

        /// <summary>
        /// Probes if an event has registered callbacks
        /// </summary>
        /// <param name="pEventName"></param>
        /// <returns></returns>
        bool IsEventRegistered(string pEventName);
    }
}
