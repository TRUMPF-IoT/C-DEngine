// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Threading;
#pragma warning disable 1591    //TODO: Remove and document public methods

namespace nsCDEngine.Engines.ThingService
{

    /// <summary>
    /// Triggers for Rules Engine Events
    /// </summary>
    public enum eRuleTrigger
    {
        /// <summary>
        /// Always fire if Trigger object has changed
        /// </summary>
        Fire = 0,
        /// <summary>
        /// Fires if the the Trigger object has be set with the same value
        /// </summary>
        State = 1,
        /// <summary>
        /// Fires if the Trigger Object Value is equal to the Trigger Value
        /// </summary>
        Equals = 2,
        /// <summary>
        /// Fires if the Trigger object Value is larger then the trigger value - but fires only once if its larger!
        /// </summary>
        Larger = 3,
        /// <summary>
        /// Fires if the Trigger object value is smaller then the trigger value - but fires only once if its smaller!
        /// </summary>
        Smaller = 4,
        /// <summary>
        /// Fires if the trigger object value is Boolean NOT the trigger value
        /// </summary>
        Not = 5,
        /// <summary>
        /// For string objects: fires if the string in the trigger object value contains the string in the trigger value
        /// </summary>
        Contains = 6,
        /// <summary>
        /// Always sets the Target Object value with the Source object value
        /// </summary>
        Set = 7,
        /// <summary>
        /// For string objects: fires if the string in the trigger object value starts with the string in the trigger value
        /// </summary>
        StartsWith = 8,
        /// <summary>
        /// For string objects: fires if the string in the trigger object value ends with the string in the trigger value
        /// </summary>
        EndsWith = 9,
        /// <summary>
        /// Fires when a flank is detected. The trigger value has to show the flank separated by , (i.e: 4,5 means a flank from value 4 to value 5)
        /// </summary>
        Flank = 10
    }

    /// <summary>
    /// Event Login class. This class is the main Event Log object used by the ILoggerEngine interface, TheLoggerFactor and the "NewLogEntry" event fired by the SystemLog
    /// </summary>
    public class TheEventLogData : TheDataBase
    {
        /// <summary>
        /// Timestamp of the Event
        /// </summary>
        public DateTimeOffset EventTime { get; set; }

        /// <summary>
        /// A string explaining the trigger of the event
        /// </summary>
        public string EventTrigger { get; set; }

        /// <summary>
        /// A pointer to the object that caused the event
        /// </summary>
        public string ActionObject { get; set; }

        /// <summary>
        /// A category of the event - this can be used by a logger to created different logs for each category (i.e. "NMI Audit", "Cloud Connects" etc) or to filter in the NMI
        /// </summary>
        public string EventCategory { get; set; }

        /// <summary>
        /// Short name of the event. Keep it short for better searching and filtering
        /// </summary>
        public string EventName { get; set; }

        /// <summary>
        /// The ORG or Name of the node/station/host the event happend on
        /// </summary>
        public string StationName { get; set; }

        /// <summary>
        /// If extra data (i.e. an Image, a File Dump or a screenshot) of the event
        /// </summary>
        public byte[] EventData { get; set; }

        /// <summary>
        /// Longer explanation of the event with possible remidies what to do about the event
        /// </summary>
        public string EventString { get; set; }

        /// <summary>
        /// Severity level of the event (see eMsgLevel)
        /// </summary>
        public eMsgLevel EventLevel { get; set; }

        /// <summary>
        /// User that caused/triggered the event. If a thing caused the event the ActionObject should contain the ThingMID
        /// </summary>
        public string UserID { get; set; }

        /// <summary>
        /// Future use: records if an event was acknoledged by a user
        /// </summary>
        public bool IsAck { get; set; }

        /// <summary>
        /// Future Use: The User ID that acknowledged the event
        /// </summary>
        public Guid AckUserID { get; set; }
    }

    [DeviceType(DeviceType = eKnownDeviceTypes.TheThingRule, Description = "Rule", Capabilities = new[] { eThingCaps.ConfigManagement })]
    public class TheThingRule: TheThingBase
    {
        public string Parent
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "Parent"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "Parent", value); }
        }

        public bool IsRuleActive
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsRuleActive"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsRuleActive", value); }
        }
        public bool IsRuleWaiting
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsRuleWaiting"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsRuleWaiting", value); }
        }
        public bool IsRuleRunning
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsRuleRunning"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsRuleRunning", value); }
        }
        public bool IsIllegal
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsIllegal"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsIllegal", value); }
        }
        public bool IsTriggerObjectAlive
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsTriggerObjectAlive"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsTriggerObjectAlive", value); }
        }

        [ConfigProperty(Generalize = true)]
        public string TriggerObjectType
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "TriggerObjectType"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "TriggerObjectType", value); }
        }
        [ConfigProperty(Generalize = true)]
        public string TriggerObject
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "TriggerObject"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "TriggerObject", value); }
        }

        [ConfigProperty(Generalize = true)]
        public string TriggerProperty
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "TriggerProperty"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "TriggerProperty", value); }
        }

        [ConfigProperty(Generalize = true)]
        public eRuleTrigger TriggerCondition
        {
            get { return (eRuleTrigger)TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "TriggerCondition")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "TriggerCondition", ((int)value)); }
        }
        [ConfigProperty(Generalize = true)]
        public string TriggerValue
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "TriggerValue"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "TriggerValue", value); }
        }
        public string OldValue
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "OldValue"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "OldValue", value); }
        }

        [ConfigProperty(Generalize = true)]
        public string ActionObjectType
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "ActionObjectType"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "ActionObjectType", value); }
        }
        [ConfigProperty(Generalize = true)]
        public string ActionObject
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "ActionObject"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "ActionObject", value); }
        }
        [ConfigProperty(Generalize = true)]
        public string ActionProperty
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "ActionProperty"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "ActionProperty", value); }
        }
        [ConfigProperty(Generalize =true)]
        public string ActionValue
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "ActionValue"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "ActionValue", value); }
        }
        [ConfigProperty(Generalize = true)]
        public string FriendlyName
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "FriendlyName"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "FriendlyName", value); }
        }
        [ConfigProperty(Generalize = true)]
        public int ActionDelay
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "ActionDelay")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "ActionDelay", value); }
        }

        [ConfigProperty(Generalize = true)]
        public int TriggerActiveTime
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyString(MyBaseThing, "TriggerActiveTime")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "TriggerActiveTime", value); }
        }

        [ConfigProperty(Generalize = true)]
        public DateTimeOffset TriggerStartTime
        {
            get { return TheCommonUtils.CDate(TheThing.GetSafePropertyString(MyBaseThing, "TriggerStartTime")); }
            set { TheThing.SetSafePropertyDate(MyBaseThing, "TriggerStartTime", value); }
        }
        [ConfigProperty(Generalize = true)]
        public DateTimeOffset TriggerEndTime
        {
            get { return TheCommonUtils.CDate(TheThing.GetSafePropertyString(MyBaseThing, "TriggerEndTime")); }
            set { TheThing.SetSafePropertyDate(MyBaseThing, "TriggerEndTime", value); }
        }

        [ConfigProperty(Generalize = true)]
        public bool IsRuleLogged
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsRuleLogged"); }
            set { TheThing.SetSafePropertyBool(MyBaseThing, "IsRuleLogged", value); }
        }

        [ConfigProperty(Generalize =true)]
        public bool IsEVTLogged
        {
            get { return TheThing.MemberGetSafePropertyBool(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        public string TriggerOldValue
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "TriggerOldValue"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "TriggerOldValue", value); }
        }
        public void SetDelayTimer(Timer pTimer)
        {
            mDelayTimer = pTimer;
        }
        public Timer GetDelayTimer()
        {
            return mDelayTimer;
        }

        private Timer mDelayTimer ;

        public TheThingRule(TheThing pBaseThing)
        {
            if (pBaseThing == null)
                pBaseThing = new TheThing();
            MyBaseThing = pBaseThing;

            TriggerStartTime = DateTimeOffset.MinValue;
            TriggerEndTime = DateTimeOffset.MaxValue;

            if (string.IsNullOrEmpty(MyBaseThing.EngineName))
                MyBaseThing.EngineName = eEngineName.ThingService;
            MyBaseThing.DeviceType = eKnownDeviceTypes.TheThingRule;
            MyBaseThing.ID = MyBaseThing.cdeMID.ToString();
            RegisterEvent("RuleFired", null);
            MyBaseThing.SetIThingObject(this);
        }

        public TheThingRule(Guid pKey, string pFriendlyName,TheThing pTriggerThing,string pTriggerProperty,eRuleTrigger pTriggerCondition,string pTriggerValue, bool pDoLogRule, bool bIsRuleActive)
        {
            if (pTriggerThing == null) return;
            MyBaseThing = new TheThing() { cdeMID = pKey };

            CreateRule(pFriendlyName, pTriggerThing, pTriggerProperty, pTriggerCondition, pTriggerValue, pDoLogRule, bIsRuleActive);
        }
        public TheThingRule(Guid pKey, string pFriendlyName, TheThing pTriggerThing, string pTriggerValue, bool pDoLogRule, bool bIsRuleActive)
        {
            if (pTriggerThing == null) return;
            MyBaseThing = new TheThing() { cdeMID = pKey };

            CreateRule(pFriendlyName, pTriggerThing, null, eRuleTrigger.Equals, pTriggerValue, pDoLogRule, bIsRuleActive);
        }

        private void CreateRule(string pFriendlyName, TheThing pTriggerThing, string pTriggerProperty, eRuleTrigger pTriggerCondition, string pTriggerValue, bool pDoLogRule, bool bIsRuleActive)
        {
            TriggerStartTime = DateTimeOffset.MinValue;
            TriggerEndTime = DateTimeOffset.MaxValue;

            //TriggerObjectType = "CDE_THING";
            TriggerObject = pTriggerThing.cdeMID.ToString();
            if (!TheCommonUtils.IsNullOrWhiteSpace(pTriggerProperty))
                TriggerProperty = pTriggerProperty;
            else
                pTriggerProperty = "Value";
             TriggerCondition = pTriggerCondition;
            TriggerValue = pTriggerValue;


            FriendlyName = pFriendlyName;
            IsRuleLogged = pDoLogRule;
            IsRuleActive = bIsRuleActive;

            if (string.IsNullOrEmpty(MyBaseThing.EngineName))
                MyBaseThing.EngineName = eEngineName.ThingService;
            MyBaseThing.DeviceType = eKnownDeviceTypes.TheThingRule;
            MyBaseThing.ID = MyBaseThing.cdeMID.ToString();
            RegisterEvent("RuleFired", null);
            MyBaseThing.SetIThingObject(this);
        }
    }
}
