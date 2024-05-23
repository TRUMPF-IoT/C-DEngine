// SPDX-FileCopyrightText: Copyright (c) 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using static nsCDEngine.Engines.ThingService.ThePin;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;
using TT = nsCDEngine.Engines.ThingService.TheThing;

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// OPC UA Range Type "nsu=http://opcfoundation.org/UA/;i=884"
    /// </summary>
    public struct UARange
    {
        public double Low { get; set; }
        public double High { get; set; }

        public override string ToString()
        {
            return $"{Low}, {High}";
        }
    }
    /// <summary>
    /// Default Pin Names - Derive to add new Pin Types. 
    /// PREVIEW: This API might still change until it is finalized
    /// </summary>
#pragma warning disable S1118 // Utility classes should not have public constructors
    public class ePinTypeName
#pragma warning restore S1118 // Utility classes should not have public constructors
    {
        public const string Generic = "nsu=http://c-labs.com/UA/Energy;i=2001"; //OPC UA: once OPCF standardized Pins replace with Standard
    }
    public class ThePinOR
    {
        public int PinOverrides { get; set; } = 0;
        public ePinLocation NMIPinLocation { get; set; } = 0;
        public int NMIPinPosition { get; set; } = 0;
        public int NMIPinWidth { get; set; } = 0;
        public int NMIPinHeight { get; set; } = 0;
        public int NMIxDelta { get; set; } = 0;
        public int NMIyDelta { get; set; } = 0;
        public string PinProperty { get; set; } = null;

        public ePinLocation oNMIPinLocation { get; set; } = 0;
        public int oNMIPinPosition { get; set; } = 0;
        public int oNMIPinWidth { get; set; } = 0;
        public int oNMIPinHeight { get; set; } = 0;
        public int oNMIxDelta { get; set; } = 0;
        public int oNMIyDelta { get; set; } = 0;
        public string oPinProperty { get; set; } = null;

    }
    public class ThePin : TheMetaDataBase
    {
        public enum ePinLocation
        {
            Left=0, Right=1, Top=2, Bottom=3
        }

        public object PinValue
        {
            get
            {
                if (!string.IsNullOrEmpty(PinProperty) && cdeO != Guid.Empty)
                {
                    var tThing = TheThingRegistry.GetThingByMID(cdeO);
                    return tThing?.GetProperty(PinProperty)?.GetValue();
                }
                return null;
            }
            set
            {
                TheThingRegistry.GetThingByMID(cdeO)?.SetProperty(PinProperty, value);
                if (IsEventRegistered(eThingEvents.PinValueUpdated))
                    FireEvent(eThingEvents.PinValueUpdated, new TheProcessMessage { Message = new TSM(eEngineName.ThingService, $"PIN_UPDATED:{cdeMID}") { PLS = $"{value}" } }, true);
            }
        }
        public string Units { get; set; }
        public UARange EURange { get; set; } = new UARange();
        public string PinName { get; set; }
        public string PinType { get; set; } = ePinTypeName.Generic;
        public bool IsInbound { get; set; } = false;
        public int PinNumber { get; set; } 
        public string PinConfig { get; set; }
        public bool PollsFromPin { get; set; } = false;
        public bool AllowsPolling { get; set; } = false;
        public int MaxConnections { get; set; } = 1;
        private List<ThePin> IsConnectedTo { get; set; } = new List<ThePin>();
        public List<string> CanConnectToPinType { get; set; } = new List<string>();

        public List<ThePin> CompatiblePins { get; set; }= new List<ThePin>();

        public List<ThePin> MyPins { get; set; } = new List<ThePin>();
        public string PinProperty { get; set; } = null;

        /// <summary>
        /// Pin Location describes on what side of the FacePlace the pin will be (left=0, right=1)
        /// In the future top=2, bottom=3 will be added
        /// </summary>
        public ePinLocation NMIPinLocation { get; set; } = 0;
        public int NMIPinPosition { get; set; } = 0;
        public int NMIPinWidth { get; set; } = 78;
        public int NMIPinHeight { get; set; } = 39;
        public int NMIxDelta { get; set; } = 0;
        public int NMIyDelta { get; set; } = 0;

        internal Guid MyPinMapper;

        public bool AddPin(ThePin pin)
        {
            if (string.IsNullOrEmpty(pin?.PinName))
                return false;
            pin.cdeO = this.cdeO;
            MyPins.Add(pin);
            return true;
        }
        public bool AddPins(List<ThePin> pIns)
        {
            if (pIns?.Any() == true)
            {
                foreach (var pin in pIns)
                    AddPin(pin);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Updates the pin Value
        /// </summary>
        /// <param name="newValue">New Value to Set</param>
        /// <returns></returns>
        public virtual bool UpdatePinValue(object newValue)
        {
            if (!IsInbound)
                return false;
            PinValue = newValue;
            return true;
        }

        /// <summary>
        /// Gets the NMI Pin Face
        /// </summary>
        /// <returns></returns>
        public virtual string NMIGetPinLineFace(string flowStyle)
        {
            if (NMIPinPosition < 0)
                return "";
            string dire = "left";
            string fdire = "right";
            if (NMIPinLocation==ePinLocation.Right)
            {
                dire = "right";
                if (IsInbound)
                    fdire = "left";
            }
            else
            {
                if (!IsInbound)
                    fdire = "left";
            }

            flowStyle = GetMapperStyle(flowStyle);
            var ot = TheThingRegistry.GetThingByMID(cdeO);
            if (ot != null)
            {
                ot.GetProperty(PinProperty, true).cdeE |= 8;
                ot.GetProperty($"{PinProperty}_css", true).cdeE |= 8;
                UpdatePinFlow("", true);
            }

            ThePin tP2 = null;
            if (MyPins?.Count > 0)
                tP2 = MyPins.Find(s => s.NMIPinPosition >= 0);
            return $"""
                 <div class="cdeFacePinDiv">
                    <div cdeTAG="<%C:{PinProperty}_css%>">
                        <div class="cde{flowStyle}flow{fdire}" style="animation-duration: 2s;"></div>
                    </div>
                    {(PinProperty == null ? "" : $"""<div class="cdePinTopLabel_{dire}"><%C12:1:{PinProperty}%> {Units}</div>""")}
                    {(tP2?.PinProperty == null ? "" : $"""<div class="cdePinBottomLabel_{dire}"><%C12:1:{tP2.PinProperty}%> {tP2.Units}</div>""")}
                </div>
                """;
        }

        /// <summary>
        /// Add a List of Pins
        /// </summary>
        /// <param name="pins"></param>
        /// <param name="ResetFirst"></param>
        /// <returns></returns>
        internal bool AddPinConnections(List<ThePin> pins, bool ResetFirst=false)
        {
            if (ResetFirst)
                IsConnectedTo.Clear();
            bool ret = true;
            foreach (var pin in pins)
            {
                var t=AddPinConnection(pin);
                if (!t) ret = t;
            }
            return ret;
        }

        public static bool ConnectPins(ThePin pin1, ThePin pin2, bool Reset1First=false, bool Reset2First = false)
        {
            if (pin1 == null || pin2 == null)
                return false;
            bool ret1=pin1.AddPinConnection(pin2, Reset1First);
            bool ret2=pin2.AddPinConnection(pin1, Reset2First);
            return ret1 || ret2;
        }

        private readonly object lockPinConnection = new object();
        /// <summary>
        /// Adds a new Pin Connection
        /// </summary>
        /// <param name="pPin">The Target Pin</param>
        /// <param name="ResetFirst">Remove existing connections first</param>
        /// <returns>True if it was successfully added. False if the pin was already connected or the pin has already reached max connections</returns>
        public bool AddPinConnection(ThePin pPin, bool ResetFirst = false)
        {
            lock (lockPinConnection)
            {
                if (ResetFirst)
                    IsConnectedTo.Clear();
                if (pPin!=null && !IsConnectedToPin(pPin) && (MaxConnections == 0 || IsConnectedTo.Count < MaxConnections) && CanConnectToPinType.Contains(pPin.PinType))
                {
                    IsConnectedTo.Add(pPin);
                    var tThing = TheThingRegistry.GetThingByMID(cdeO);
                    if (tThing == null) return true;
                    tThing.UpdatePinProperty(this);
                    return true;
                }
            }
            return false;
        }

        internal bool RemoveConnection(ThePin pPin)
        {
            lock (lockPinConnection)
            {
                if (IsConnectedTo.Exists(s => s.cdeO == pPin.cdeO && s.PinName == pPin.PinName))
                {
                    IsConnectedTo.Remove(pPin);
                    return true;
                }
            }
            return false;
        }

        internal bool IsConnectedToPin(ThePin pPin)
        {
            lock (lockPinConnection)
            {
                if (IsConnectedTo.Exists(s => s.cdeO == pPin.cdeO && s.PinName == pPin.PinName))
                {
                    return true;
                }
            }
            return false;
        }

        public bool HasConnectedPins()
        {
            return IsConnectedTo.Any();
        }

        public int PinConnectionCnt()
        {
            return IsConnectedTo.Count;
        }

        public List<ThePin> GetConnectedPins()
        {
            return IsConnectedTo.ToList();
        }

        public override string ToString()
        {
            return $"{cdeO}:{PinName}";
        }

        public string GetResolvedName()
        {
            return $"{PinName} ({TheThingRegistry.GetThingByMID(cdeO)?.FriendlyName})";
        }

        public virtual void UpdatePinFlow(string flowStyle, bool ForceOff)
        {
            if (PinProperty == null) return;
            var tThing = TheThingRegistry.GetThingByMID(cdeO);
            if (tThing == null) return;
            flowStyle = GetMapperStyle(flowStyle);
            SetPinValue(tThing);
            if (!ForceOff && CU.CDbl(PinValue) > 0)
                TT.SetSafePropertyString(tThing, $"{PinProperty}_css", $"cdehori{flowStyle}line");
            else
                TT.SetSafePropertyString(tThing, $"{PinProperty}_css", $"cdehori{flowStyle}linenf");
        }

        public virtual void SetPinValue(TT pDevs)
        {
            if (PollsFromPin && IsConnectedTo?.Count > 0)
            {
                double pinv = 0;
                foreach (var pin in IsConnectedTo)
                    pinv += CU.CDbl(pin.PinValue);
                PinValue = pinv;
            }
            else
                PinValue = pDevs?.GetProperty(PinProperty, false)?.GetValue();
        }

        internal static Dictionary<string, string> StyleMapper = new Dictionary<string, string>();
        public static void UpdateStyleMapper(Dictionary<string, string> pMap)
        {
            if (pMap == null) return;
            foreach (var key in pMap.Keys)
            {
                StyleMapper[key] = pMap[key];
            }
        }
        public virtual string GetMapperStyle(string pStyle)
        {
            if (StyleMapper.ContainsKey(PinType))
                return StyleMapper[PinType];
            return pStyle;
        }
    }

    /// <summary>
    /// Extension for FacePlates of TheThingBase.
    /// PREVIEW: This is still in preview and code could change!
    /// </summary>
    public class TheNMIFaceModel
    {
        int _xpos = 0;
        public int XPos
        {
            get
            {
                int ret = _xpos;
                if (OwnerFormInfoId != Guid.Empty)
                {
                    var f = TheNMIEngine.GetFieldById(OwnerFormInfoId);
                    if (f != null)
                    {
                        var dx = CU.CInt(ThePropertyBag.PropBagGetValue(f?.PropertyBag, "DragX"));
                        ret += dx;
                    }
                }
                return ret;
            }
            set { _xpos = value; }
        }

        int _ypos = 0;
        public int YPos
        {
            get
            {
                int ret = _ypos;
                if (OwnerFormInfoId != Guid.Empty)
                {
                    var f = TheNMIEngine.GetFieldById(OwnerFormInfoId);
                    if (f != null)
                    {
                        var dy = CU.CInt(ThePropertyBag.PropBagGetValue(f?.PropertyBag, "DragY"));
                        ret += dy;
                    }
                }
                return ret;
            }
            set { _ypos = value; }
        }
        public int XLen { get; set; } = 0;
        public int YLen { get; set; } = 0;
        public string Prefix { get; set; } = "NOP";
        public bool AddTTS { get; set; } = false;

        public string FaceTemplate { get; set; }
        public void SetPos(int x, int y)
        {
            XPos = x;
            YPos = y;
        }

        public void SetNMIModel(int xl, int yl, string pFix)
        {
            XLen = xl;
            YLen = yl;
            Prefix = pFix;
        }

        internal Guid OwnerFormInfoId;
    }
}
