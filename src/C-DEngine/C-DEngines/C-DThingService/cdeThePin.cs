// SPDX-FileCopyrightText: Copyright (c) 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// Default Pin Names - Derived to add new Pin Types
    /// </summary>
    public class ePinTypeName
    {
        public const string Generic = "Generic Pin";
    }
    public class ThePin : TheMetaDataBase
    {
        public string Units { get; set; }
        public string PinName { get; set; }
        public string PinType { get; set; } = ePinTypeName.Generic;
        public bool IsOutbound { get; set; } = false;
        public List<string> IsConnectedTo { get; set; } = new List<string>();
        public List<string> CanConnectTo { get; set; } = new List<string>();
        public List<string> PinProperties { get; set; } = new List<string>();

        public bool NMIIsPinRight { get; set; } = false;
        public int NMIPinTopPosition { get; set; } = 0;
        public string NMIClass { get; set; } = "";

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

        public string FacePlateUrl { get; set; }
    }
}
