// SPDX-FileCopyrightText: Copyright (c) 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;
using NMI = nsCDEngine.Engines.NMIService.TheNMIEngine;
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
    public class ThePin : TheMetaDataBase
    {
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
        public bool PollsFromPin { get; set; } = false;
        public bool AllowsPolling { get; set; } = false;
        public int MaxConnections { get; set; } = 1;
        private List<ThePin> IsConnectedTo { get; set; } = new List<ThePin>();
        public List<string> CanConnectToPinType { get; set; } = new List<string>();

        public List<ThePin> CompatiblePins { get; set; }= new List<ThePin>();

        public List<ThePin> MyPins { get; set; } = new List<ThePin>();
        public string PinProperty { get; set; } = null;
        public bool NMIIsPinRight { get; set; } = false;
        public int NMIPinTopPosition { get; set; } = 0;
        public int NMIPinWidth { get; set; } = 78;
        public int NMIPinHeight { get; set; } = 39;
        public int NMIxDelta { get; set; } = 0;
        public int NMIyDelta { get; set; } = 0;

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
            if (NMIPinTopPosition < 0)
                return "";
            string dire = "left";
            string fdire = "right";
            if (NMIIsPinRight)
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

            if (StyleMapper.ContainsKey(PinType))
                flowStyle = StyleMapper[PinType];
            var ot = TheThingRegistry.GetThingByMID(cdeO);
            if (ot != null)
            {
                ot.GetProperty(PinProperty, true).cdeE |= 8;
                ot.GetProperty($"{PinProperty}_css", true).cdeE |= 8;
                UpdatePinFlow("", true);
            }

            ThePin tP2 = null;
            if (MyPins?.Count > 0)
                tP2 = MyPins.Find(s => s.NMIPinTopPosition >= 0);
            return $"""
                 <div class="cdeFacePinDiv">
                    {(PinProperty == null ? "" : $"""<div class="cdePinTopLabel_{dire}"><%C12:1:{PinProperty}%> {Units}</div>""")}
                    <div cdeTAG="<%C:{PinProperty}_css%>">
                        <div class="cde{flowStyle}flow{fdire}" style="animation-delay: 0s;"></div>
                        <div class="cde{flowStyle}flow{fdire}" style="animation-delay: 2s;"></div>
                        <div class="cde{flowStyle}flow{fdire}" style="animation-delay: 4s;"></div>
                    </div>
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
                if (pPin!=null && !IsConnectedTo.Contains(pPin) && (MaxConnections == 0 || IsConnectedTo.Count < MaxConnections) && CanConnectToPinType.Contains(pPin.PinType))
                {
                    IsConnectedTo.Add(pPin);
                    return true;
                }
            }
            return false;
        }

        internal bool RemoveConnection(ThePin pPin)
        {
            lock (lockPinConnection)
            {
                if (IsConnectedTo.Contains(pPin))
                {
                    IsConnectedTo.Remove(pPin);
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
            return $"{PinName}:{cdeO}";
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
            if (StyleMapper.ContainsKey(PinType))
                flowStyle = StyleMapper[PinType];
            else
                flowStyle = "missing";
            SetPinValue(tThing);
            if (!ForceOff && CU.CDbl(PinValue) > 0)
                TT.SetSafePropertyString(tThing, $"{PinProperty}_css", $"cdehori{flowStyle}line");
            else
                TT.SetSafePropertyString(tThing, $"{PinProperty}_css", $"cdehori{flowStyle}linenf");
        }

        private static Dictionary<string, string> StyleMapper = new Dictionary<string, string>();

        public static void UpdateStyleMapper(Dictionary<string, string> pMap)
        {
            if (pMap == null) return;
            foreach (var key in pMap.Keys)
            {
                StyleMapper[key] = pMap[key];
            }
        }
        public static void DrawPinLines(TT pGroupBaseThing, TheFormInfo MyLiveForm, List<TheThingBase> pDevs)
        {
            if (pDevs?.Count > 0)
            {
                for (int i = 0; i < pDevs.Count; i++)
                {
                    if (pDevs[i] == null) continue;

                    var allSourcePins = pDevs[i].GetBaseThing().GetBaseThing().GetAllPins();
                    var sourceOutPins = allSourcePins.Where(s => !s.IsInbound).ToList();
                    foreach (var sourcePin in sourceOutPins)
                    {
                        //if (sourcePin.PinType != eEmsPinTypeName.ACPowerFlow) continue
                        var targetInPins = sourcePin?.GetConnectedPins();
                        if (targetInPins?.Count > 0)
                        {
                            bool sourceHasRightPin = allSourcePins.Exists(p => p.NMIIsPinRight);
                            bool sourcehasLeftPin = allSourcePins.Exists(p => !p.NMIIsPinRight);
                            foreach (var targetPin in targetInPins)
                            {
                                var targetT = TheThingRegistry.GetThingByMID(targetPin.cdeO);
                                var targetTB = targetT?.GetObject() as TheThingBase;
                                var targetFace = targetTB?.MyNMIFaceModel;
                                if (targetFace == null) continue;
                                var sourceFace = pDevs[i].MyNMIFaceModel;

                                var sLeft = sourcePin.NMIIsPinRight ?
                                    sourceFace.XPos + sourceFace.XLen + (sourcePin.NMIPinWidth * ((sourcehasLeftPin ? 1 : 0) + (sourceHasRightPin ? 1 : 0))) + sourcePin.NMIxDelta :
                                    sourceFace.XPos - ((sourcePin.NMIPinWidth * (sourceHasRightPin ? 1 : 0)) + sourcePin.NMIxDelta);
                                var tLeft = targetPin.NMIIsPinRight ?
                                    targetFace.XPos + targetFace.XLen + targetPin.NMIPinWidth /*+ targetPin.NMIxDelta*/ :
                                    targetFace.XPos - targetPin.NMIxDelta;

                                var left = sLeft > tLeft ? tLeft : sLeft;
                                var xl = Math.Abs(tLeft - sLeft);
                                if (xl < 10) xl = 10;

                                var sTop = sourceFace.YPos + ((sourcePin.NMIPinTopPosition * 39) + 15) + sourcePin.NMIyDelta;
                                var tTop = targetFace.YPos + ((targetPin.NMIPinTopPosition * 39) + 15) + targetPin.NMIyDelta;
                                string dir = "up";
                                var top = tTop;
                                if (sTop < tTop)
                                {
                                    top = sTop;
                                    dir = "down";
                                }
                                var yl = Math.Abs(sTop - tTop);
                                if (yl < 10)
                                    yl = 10;
                                else
                                {
                                    if (xl > 10)
                                    {
                                        xl = 10;
                                        left = tLeft;
                                    }
                                }

                                var flowStyle = "";
                                if (StyleMapper.ContainsKey(sourcePin.PinType))
                                    flowStyle = StyleMapper[sourcePin.PinType];
                                StringBuilder moveData = new();
                                for (int n = 0; n < 3; n++)
                                    moveData.Append($"<div class=\"cde{flowStyle}flow{dir}\" style=\"animation-delay: {n * 2}s\"></div>");
                                if (xl > 10 || yl > 10)
                                {
                                    AddPinLine(pGroupBaseThing, MyLiveForm, $"line{sourcePin.PinName.Replace(' ', '_')}_{pDevs[i].GetBaseThing().cdeMID}",
                                        left,
                                        top + 21,
                                        xl,
                                        yl,
                                        moveData.ToString());
                                }
                            }
                        }
                    }
                }
                UpdateLine(pGroupBaseThing, pDevs);
            }
        }

        public static void UpdateLine(TT pGroupBaseThing, List<TheThingBase> pDevs)
        {
            if (pDevs?.Count > 0)
            {
                for (int i = 0; i < pDevs.Count; i++)
                {
                    if (pDevs[i] == null) continue;
                    foreach (var outpin in pDevs[i].GetBaseThing().GetBaseThing().GetAllPins())
                    {
                        if (outpin == null) continue;
                        outpin.SetPinValue(pDevs[i].GetBaseThing());
                        var flowStyle = "";
                        if (StyleMapper.ContainsKey(outpin.PinType))
                            flowStyle = StyleMapper[outpin.PinType];
                        var style = $"cdevert{flowStyle}line";
                        if (CU.CDbl(outpin.PinValue) == 0)
                            style += "nf";
                        else
                            style = style;
                        pGroupBaseThing.SetProperty($"line{outpin.PinName.Replace(' ', '_')}_{pDevs[i].GetBaseThing().cdeMID}", style);
                    }
                }
                TheThingRegistry.UpdateThing(pGroupBaseThing, true);
            }
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

        public static void AddPinLine(TT MyBaseThing, TheFormInfo MyLiveForm, string pDataName, int x, int y, int xl, int yl, string moveData, string pClassname = null)
        {
            NMI.AddSmartControl(MyBaseThing, MyLiveForm, eFieldType.FacePlate, MyLiveForm.FldPos, 0, 0, null, pDataName,
                    new nmiCtrlFacePlate
                    {
                        NoTE = true,
                        PixelWidth = xl,
                        PixelHeight = yl,
                        IsAbsolute = true,
                        Left = x,
                        Top = y,
                        FaceOwner = MyLiveForm.cdeMID.ToString(),
                        HTML = string.IsNullOrEmpty(pDataName) ? $"<div class=\"{pClassname}\">{moveData}</div>" : $"<div cdeTAG=\"<%C:{pDataName}%>\">{moveData}</div>",
                        AllowDrag = true
                    });
        }
    }

    /// <summary>
    /// Extension for FacePlates of TheThingBase.
    /// PREVIEW: This is still in preview and code could change!
    /// </summary>
    public class TheNMIFaceModel
    {
        public int XPos { get; set; } = 0;
        public int YPos { get; set; } = 0;
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
    }
}
