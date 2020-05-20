// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Collections.Generic;
using System.Text;

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// Interface that has to be implemented by a Plugin that wants to create an event logger
    /// </summary>
    public interface ICDELoggerEngine
    {
        /// <summary>
        /// Logs a new event
        /// </summary>
        /// <param name="pItem"></param>
        bool LogEvent(TheEventLogData pItem);

        /// <summary>
        /// Returns a list of THeEventLogData from the event Log
        /// </summary>
        /// <param name="PageNo"></param>
        /// <param name="PageSize"></param>
        /// <param name="LatestFirst"></param>
        /// <returns></returns>
        List<TheEventLogData> GetEvents(int PageNo, int PageSize, bool LatestFirst);
    }
}
