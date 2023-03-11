// SPDX-FileCopyrightText: Copyright (c) 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

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
    /// 
    /// </summary>
    public class ePinTypeName
    {
        public const string Generic = "nsu=http://c-labs.com/UA/EnergyDevices;i=2001"; //OPC UA: once OPCF standardized Pins replace with Standard
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
                if (IsEventRegistered(eThingEvents.PinValueUpdated))
                    FireEvent(eThingEvents.PropertyChanged, new TheProcessMessage { Message = new BaseClasses.TSM(eEngineName.ThingService, $"PIN_UPDATED:{cdeMID}") { PLS = $"{value}" } }, true);
            }
        }
        public string Units { get; set; }
        public UARange EURange { get; set; } = new UARange();
        public string PinName { get; set; }
        public string PinType { get; set; } = ePinTypeName.Generic;
        public bool IsInbound { get; set; } = false;
        public bool PollsFromPin { get; set; } = false;
        public bool AllowsPolling { get; set; } = false;
        public List<string> IsConnectedTo { get; set; } = new List<string>();
        public List<string> CanConnectTo { get; set; } = new List<string>();

        public List<ThePin> MyPins { get; set; } = new List<ThePin>();
        public string PinProperty { get; set; } = null;
        public bool NMIIsPinRight { get; set; } = false;
        public int NMIPinTopPosition { get; set; } = 0;
        public int NMIPinWidth { get; set; } = 78;
        public int NMIPinHeight { get; set; } = 39;
        public int NMIxDelta { get; set; } = 0;
        public int NMIyDelta { get; set; } = 0;
        public string NMIClass { get; set; } = "";

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
        public virtual bool UpdatePin(object newValue)
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
        public virtual string NMIGetPinLineFace()
        {
            return "";
        }
    }

    /// <summary>
    /// Extension for FacePlates of TheThingBase.
    /// </summary>
    public class TheNMIFaceModel
    {
        public int XPos { get; set; } = 0;
        public int YPos { get; set; } = 0;
        public int XLen { get; set; } = 0;
        public int YLen { get; set; } = 0;
        public string Prefix { get; set; } = "NOP";
        int mfldStart = 0;
        internal TheThing MyBaseThing;
        public int FldStart
        {
            get { return mfldStart; }
            set { mfldStart = value; mFldPos = value; }
        }
        int mFldPos = 0;
        public int FldPos
        {
            get { return ++mFldPos; }
        }
        public void SetPos(int startFld, int x, int y)
        {
            FldStart = startFld;
            XPos = x;
            YPos = y;
        }

        public void SetNMIModel(int xl, int yl, string pFix, string pFaceUrl)
        {
            XLen = xl;
            YLen = yl;
            Prefix = pFix;
            FacePlateUrl = pFaceUrl;
        }

        public string FacePlateUrl 
        {
            get { return TheThing.MemberGetSafePropertyString(MyBaseThing); }
            set { 
                TheThing.MemberSetSafePropertyString(MyBaseThing, value); 
            }
        }
    }
}
