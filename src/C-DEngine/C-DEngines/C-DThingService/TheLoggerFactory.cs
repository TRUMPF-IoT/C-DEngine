// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Linq;

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// TheLoggerFactory logs events to
    /// </summary>
    public static class TheLoggerFactory
    {
        private static ICDELoggerEngine MyLoggerEngine = null;

        private static void InitRR()
        {
            if (MyLoggerEngine == null)
            {
                var tbases = TheThingRegistry.GetBaseEnginesByCap(eThingCaps.LoggerEngine);
                if (tbases?.Count > 0)
                    MyLoggerEngine = tbases?.First()?.GetBaseThing()?.GetObject() as ICDELoggerEngine;
            }
        }

        /// <summary>
        /// Log any aribrary event to the EventLog
        /// </summary>
        /// <param name="pCategory">Event Category</param>
        /// <param name="pEventName">Short Title of the Event</param>
        /// <param name="pEventLevel">Severity of the Event</param>
        /// <param name="pEventText">Optional: Long text of the event</param>
        /// <param name="pEventTrigger">Optional: Who/What triggered the Event</param>
        /// <param name="pEventAction">Optional: What action was triggered by the Event</param>
        public static void LogEvent(string pCategory, string pEventName, eMsgLevel pEventLevel, string pEventText = null, string pEventTrigger = null, string pEventAction = null)
        {
            InitRR();
            if (MyLoggerEngine != null)
            {
                TheEventLogData tSec = new TheEventLogData
                {
                    EventTime = DateTimeOffset.Now,
                    StationName = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false),
                    EventName = pEventName,
                    EventString = pEventText,
                    EventTrigger = pEventTrigger,
                    EventLevel = pEventLevel,
                    ActionObject = pEventAction,
                    EventCategory = pCategory
                };
                MyLoggerEngine.LogEvent(tSec);
            }
        }

        /// <summary>
        /// Logs an NMI or other User Triggered event
        /// </summary>
        /// <param name="pCategory">Event Category</param>
        /// <param name="pEventName">Name of the event</param>
        /// <param name="pEventLevel">Seveity of the event</param>
        /// <param name="pUserID">User that triggered the event</param>
        /// <param name="pEventText">Long text of the event</param>
        /// <param name="pActionObject">Optional: What object/thing was impacted by the Event</param>
        public static void LogEvent(string pCategory, string pEventName, eMsgLevel pEventLevel, Guid pUserID, string pEventText = null, string pActionObject = null)
        {
            InitRR();
            if (MyLoggerEngine != null)
            {
                TheEventLogData tSec = new TheEventLogData
                {
                    EventTime = DateTimeOffset.Now,
                    StationName = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false),
                    EventName = pEventName,
                    EventString = pEventText,
                    UserID = pUserID.ToString(),
                    EventLevel = pEventLevel,
                    EventCategory = pCategory
                };
                MyLoggerEngine.LogEvent(tSec);
            }
        }

        /// <summary>
        /// Logs a TheEventLogData object
        /// </summary>
        /// <param name="pLogData"></param>
        public static void LogEvent(TheEventLogData pLogData)
        {
            InitRR();
            if (MyLoggerEngine != null)
            {
                MyLoggerEngine.LogEvent(pLogData);
            }
        }

        /// <summary>
        /// Returns true if a RulesRegistry
        /// </summary>
        /// <returns></returns>
        public static bool HasEngine()
        {
            return MyLoggerEngine != null;
        }
    }
}
