// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Linq;

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// Public interface for integrated rules
    /// </summary>
    public static class TheRulesFactory
    {
        private static ICDERulesEngine MyRulesRegistry = null;

        private static void InitRR()
        {
            if (MyRulesRegistry==null)
            {
                var tbases = TheThingRegistry.GetBaseEnginesByCap(eThingCaps.RulesEngine);
                if (tbases?.Count>0)
                    MyRulesRegistry = tbases?.First()?.GetBaseThing()?.GetObject() as ICDERulesEngine;
            }
        }

        /// <summary>
        /// Creates a new rule or updates an existing one
        /// </summary>
        /// <param name="pRule">Rule to be created or updated</param>
        /// <returns></returns>
        public static bool CreateOrUpdateRule(TheThingRule pRule)
        {
            InitRR();
            if (MyRulesRegistry != null && pRule != null && pRule.GetBaseThing() != null)
            {
                var t = TheThingRegistry.GetThingByMID(pRule.GetBaseThing().cdeMID);
                if (t != null)
                    pRule.SetBaseThing(t);
                pRule.Parent = MyRulesRegistry.GetBaseThing().ID;
                MyRulesRegistry.RegisterRule(pRule);
                if (pRule.IsRuleActive)
                    MyRulesRegistry.ActivateRules();
                return true;
            }
            return false;
        }
        /// <summary>
        /// Probes if a rule with the given ID exists
        /// </summary>
        /// <param name="pRuleID">ID to probe for</param>
        /// <returns></returns>
        public static bool HasRuleID(Guid pRuleID)
        {
            InitRR();
            if (MyRulesRegistry != null)
            {
                ICDEThing pRule = TheThingRegistry.GetThingByID(eEngineName.ThingService, pRuleID.ToString());
                if (pRule != null)
                {
                    try
                    {
                        if (pRule.GetBaseThing().GetObject() is TheThingRule tRule)
                            return true;
                    }
                    catch
                    {
                        //ignored
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Activates a given rule
        /// </summary>
        /// <param name="pRuleID">ID To activate</param>
        /// <param name="pTurnActive">if true, the rule will be activated otherwise deactivated</param>
        /// <returns></returns>
        public static bool ActivateRule(Guid pRuleID, bool pTurnActive)
        {
            InitRR();
            if (MyRulesRegistry != null)
            {
                ICDEThing pRule = TheThingRegistry.GetThingByID(eEngineName.ThingService, pRuleID.ToString());
                if (pRule != null)
                {
                    try
                    {
                        if (pRule.GetBaseThing().GetObject() is TheThingRule tRule)
                        {
                            tRule.IsRuleActive = pTurnActive;
                            MyRulesRegistry.ActivateRules();
                            return true;
                        }
                    }
                    catch
                    {
                        //ignored
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// RETIRED IN V4: Use override!
        /// </summary>
        /// <param name="pEventName"></param>
        /// <param name="tTrigger"></param>
        /// <param name="tAction"></param>
        [Obsolete("Please use other override")]
        public static void LogEvent(string pEventName, string tTrigger, string tAction)
        {
            InitRR();
            if (MyRulesRegistry != null)
            {
                MyRulesRegistry.LogEvent(pEventName, tTrigger, tAction);
            }
        }

        /// <summary>
        /// Log any aribrary event to the EventLog
        /// </summary>
        /// <param name="pEventName">Short Title of the Event</param>
        /// <param name="pEventLevel">Severity of the Event</param>
        /// <param name="pEventText">Optional: Long text of the event</param>
        /// <param name="pEventTrigger">Optional: Who/What triggered the Event</param>
        /// <param name="pEventAction">Optional: What action was triggered by the Event</param>
        public static void LogEvent(string pEventName, eMsgLevel pEventLevel, string pEventText = null, string pEventTrigger = null, string pEventAction = null)
        {
            InitRR();
            if (MyRulesRegistry != null)
            {
                TheEventLogData tSec = new TheEventLogData
                {
                    EventTime = DateTimeOffset.Now,
                    StationName = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false),
                    EventName = pEventName,
                    EventString = pEventText,
                    EventTrigger = pEventTrigger,
                    EventLevel=pEventLevel,
                    ActionObject = pEventAction
                };
                MyRulesRegistry.LogEvent(tSec);
            }
        }

        /// <summary>
        /// Returns true if a RulesRegistry
        /// </summary>
        /// <returns></returns>
        public static bool HasEngine()
        {
            return MyRulesRegistry != null;
        }
    }
}
