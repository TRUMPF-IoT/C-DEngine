// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using System;
using System.Collections.Generic;

namespace nsCDEngine.ViewModels
{
    /// <summary>
    /// TheNMIScreen is a container Thing for dynamic controls. Similar to traditional HMIs, NMIScreens can be customized on the fly by end user
    /// </summary>
    public class TheNMIScreen : TheThingBase
    {
        /// <summary>
        /// Initializes the NMI Screen
        /// </summary>
        /// <returns></returns>
        public override bool Init()
        {
            if (mIsInitCalled) return false;

            mIsInitCalled = true;
            if (string.IsNullOrEmpty(MyBaseThing.ID))
                MyBaseThing.ID = Guid.NewGuid().ToString();
            mIsInitialized = DoInit();
            MyBaseThing.FireEvent("OnInitialized", this, new TSM(MyBaseThing.cdeMID.ToString(), "Was Init"), false);
            return mIsInitialized;
        }

        /// <summary>
        /// Overwrite this function to do a Screen Init
        /// </summary>
        /// <returns></returns>
        public virtual bool DoInit()
        {
            return true;
        }

        /// <summary>
        /// Deletes the NMI screen
        /// </summary>
        /// <returns></returns>
        public override bool Delete()
        {
            DoDelete();
            return base.Delete();
        }

        /// <summary>
        /// Allows to override the delete
        /// </summary>
        /// <returns></returns>
        public virtual bool DoDelete()
        {
            return true;
        }


        /// <summary>
        /// Virtual Function that allows to create custom NMI Screen elements
        /// </summary>
        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;
            return mIsUXInitialized = DoCreateUX();
        }

        /// <summary>
        /// Overwrite this function to create the UX of this screen
        /// </summary>
        /// <returns></returns>
        public virtual bool DoCreateUX()
        {
            return true;
        }


        /// <summary>
        /// Creates a new NMI Screen
        /// </summary>
        /// <param name="pThing"></param>
        public TheNMIScreen(TheThing pThing)
        {
            if (pThing == null)
                MyBaseThing = new TheThing();
            else
                MyBaseThing = pThing;
            MyBaseThing.DeviceType = eKnownDeviceTypes.TheNMIScreen;
            MyBaseThing.EngineName = eEngineName.NMIService;
            MyBaseThing.DeclareNMIProperty("Category", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("ScreenTitle", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("TileWidth", ePropertyTypes.TNumber);
            MyBaseThing.DeclareNMIProperty("TileHeight", ePropertyTypes.TNumber);
        }
    }

    /// <summary>
    /// Live tags showing a tag in an NMI Screen
    /// </summary>
    public class TheNMILiveTag : TheThingBase
    {
        /// <summary>
        /// Initializes the Live Tag
        /// </summary>
        /// <returns></returns>
        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;

            TheThing.SetSafePropertyBool(MyBaseThing, "IsLiveTag", true);
            MyBaseThing.FireEvent("OnInitialized", this, new TSM(MyBaseThing.cdeMID.ToString(), "Was Init"), false);
            cdeP tThrot = GetProperty("Throttle", true);
            tThrot.RegisterEvent(eThingEvents.PropertyChanged, sinkThrottleChanged);
            mIsInitialized = DoInit();
            if (string.IsNullOrEmpty(MyBaseThing.ID))
                MyBaseThing.ID = Guid.NewGuid().ToString();
            return mIsInitialized;
        }

        /// <summary>
        /// Additional inits
        /// </summary>
        /// <returns></returns>
        public virtual bool DoInit()
        {
            return true;
        }

        /// <summary>
        /// Deletes the live tag
        /// </summary>
        /// <returns></returns>
        public override bool Delete()
        {
            mIsInitialized = false;
            return DoDelete();
        }

        /// <summary>
        /// Override for additional action durin delete
        /// </summary>
        /// <returns></returns>
        public virtual bool DoDelete()
        {
            return true;
        }

        /// <summary>
        /// event if a user changed the throttle
        /// </summary>
        /// <param name="pNewValue"></param>
        public virtual void sinkThrottleChanged(cdeP pNewValue)
        {
            MyBaseThing.SetPublishThrottle(TheCommonUtils.CInt(pNewValue));
        }

        /// <summary>
        /// Override for the CreateUX
        /// </summary>
        /// <returns></returns>
        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;
            return mIsUXInitialized = DoCreateUX();
        }

        /// <summary>
        /// Field to the Form Info
        /// </summary>
        protected TheFormInfo MyStatusForm;
        /// <summary>
        /// Field to the Dashboard Icon of the form
        /// </summary>
        protected TheDashPanelInfo SummaryForm;

        /// <summary>
        /// NMI of the Live Tag that can be overritten
        /// </summary>
        /// <returns></returns>
        public virtual bool DoCreateUX()
        {
            var tFlds = TheNMIEngine.AddStandardForm(MyBaseThing, "FACEPLATE", 12, null, "Value");
            MyStatusForm = tFlds["Form"] as TheFormInfo;
            SummaryForm = tFlds["DashIcon"] as TheDashPanelInfo;
            SummaryForm.PropertyBag = new nmiDashboardTile() { Category = "A-NONE", Caption = "-HIDE", RenderTarget="HomeCenterScreen" };

            var ts = TheNMIEngine.AddStatusBlock(MyBaseThing, MyStatusForm, 10);
            ts["Group"].SetParent(1);
            ts["FriendlyName"].Type = eFieldType.TextArea;
            ts["FriendlyName"].Header = "###Friendly Name###";

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 500, 2, 0x80, "Default Properties", null, new nmiCtrlCollapsibleGroup { DoClose = true, IsSmall = true,TileWidth=6, ParentFld = 1 });
            TheNMIEngine.AddFields(MyStatusForm, new List<TheFieldInfo>
                {
                    {new TheFieldInfo() { FldOrder = 20, DataItem = "cdeMID", Flags = 0, Type = eFieldType.SmartLabel, Header = "MID/Owner", PropertyBag=new TheNMIBaseControl{ ParentFld=10, TileFactorY=2 }}},
                    {new TheFieldInfo() { FldOrder = 21, DataItem = "cdeO", Flags = 0, Type = eFieldType.SmartLabel,Header = "", PropertyBag=new TheNMIBaseControl{ ParentFld=10, TileFactorY=2 }}},

                    { new TheFieldInfo() { FldOrder = 513, DataItem = "MyPropertyBag.EngineName.Value", Flags = 0, Type = eFieldType.TextArea, Header = "Engine Name", PropertyBag=new TheNMIBaseControl{ ParentFld=500 }  }},
                    { new TheFieldInfo() { FldOrder = 514, DataItem = "MyPropertyBag.DeviceType.Value", Flags = 0, Type = eFieldType.SingleEnded, Header = "DeviceType", PropertyBag=new TheNMIBaseControl{ ParentFld=500 } }},
                    { new TheFieldInfo() { FldOrder = 515, DataItem = "MyPropertyBag.ID.Value", Flags = 0, Type = eFieldType.TextArea, Header = "ID", PropertyBag=new TheNMIBaseControl{ ParentFld=500 } }},
                    { new TheFieldInfo() { FldOrder = 516, DataItem = "MyPropertyBag.Address.Value", Flags = 0, Type = eFieldType.TextArea, Header = "Address", PropertyBag=new TheNMIBaseControl{ ParentFld=500 }} },

                    { new TheFieldInfo() { FldOrder = 518, DataItem = "MyPropertyBag.cdeStartupTime.Value", Flags = 0, Type = eFieldType.Number, Header = "Startup Time", TileWidth=3, PropertyBag=new TheNMIBaseControl{ ParentFld=500 } }},
                    { new TheFieldInfo() { FldOrder = 520, DataItem = "HasLiveObject", Flags = 0, Type = eFieldType.SingleCheck, Header = "Is Alive", TileWidth = 3, PropertyBag=new TheNMIBaseControl{ ParentFld=500 } }},
                    { new TheFieldInfo() { FldOrder = 521, DataItem = "IsInitialized", Flags = 0, Type = eFieldType.SingleCheck, Header = "Is Init", TileWidth = 3, PropertyBag=new TheNMIBaseControl{ ParentFld=500 } }},
                    { new TheFieldInfo() { FldOrder = 522, DataItem = "IsUXInitialized", Flags = 0, Type = eFieldType.SingleCheck, Header = "UX Init", TileWidth = 3, PropertyBag=new TheNMIBaseControl{ ParentFld=500 } }},
            });
            TheFieldInfo tDl = TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, 2000, 2, 0x0, "", null, new nmiCtrlTileButton() { Thumbnail="FA3:f019", TileWidth=1, TileHeight=1, ParentFld=1 });
            tDl.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "DOWNLOAD", OnDownloadClick);

            TheNMIEngine.AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, 1000, 2, 0x80, "All Properties", null, new nmiCtrlCollapsibleGroup { DoClose = true, IsSmall = true,MaxTileWidth=12, ParentFld = 1 });
            TheNMIEngine.AddField(MyStatusForm, new TheFieldInfo() { FldOrder = 1010, DataItem = "mypropertybag", Flags = 8, Type = eFieldType.Table, TileWidth = 12, TileHeight = 7, PropertyBag = new ThePropertyBag() { "NoTE=true", "ParentFld=1000" } });

            return true;
        }

        private void OnDownloadClick(ICDEThing pThing, object pPara)
        {
            if (pPara is not TheProcessMessage pMSG || pMSG.Message == null) return;

            string[] cmd = pMSG.Message.PLS.Split(':');
            if (cmd.Length > 2)
            {
                TheThing tThing = TheThingRegistry.GetThingByMID("*", TheCommonUtils.CGuid(cmd[2]), true);
                if (tThing == null) return;

                TSM tFilePush = new (eEngineName.ContentService, string.Format("CDE_FILE:{0}.JSON:application/zip", tThing.FriendlyName))
                {
                    SID = pMSG.Message.SID,
                    PLS = "bin",
                    PLB = TheCommonUtils.CUTF8String2Array(TheCommonUtils.SerializeObjectToJSONString(tThing))
                };
                TheCommCore.PublishToOriginator(pMSG.Message, tFilePush);
            }
        }

        /// <summary>
        /// Constructor of the LiveTag
        /// </summary>
        /// <param name="pThing"></param>
        public TheNMILiveTag(TheThing pThing)
        {
            if (pThing == null)
                MyBaseThing = new TheThing();
            else
                MyBaseThing = pThing;

            MyBaseThing.DeclareNMIProperty("ControlType",ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("FormTitle",ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("Caption", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("TileLeft", ePropertyTypes.TNumber);
            MyBaseThing.DeclareNMIProperty("TileTop", ePropertyTypes.TNumber);
            MyBaseThing.DeclareNMIProperty("TileWidth", ePropertyTypes.TNumber);
            MyBaseThing.DeclareNMIProperty("TileHeight", ePropertyTypes.TNumber);
            MyBaseThing.DeclareNMIProperty("Flags", ePropertyTypes.TNumber);
            MyBaseThing.DeclareNMIProperty("FldOrder", ePropertyTypes.TNumber);
            MyBaseThing.DeclareNMIProperty("ClassName", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("Style", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("IsAbsolute",ePropertyTypes.TBoolean);
            MyBaseThing.DeclareNMIProperty("IsVertical", ePropertyTypes.TBoolean);
            MyBaseThing.DeclareNMIProperty("IsInverted", ePropertyTypes.TBoolean);
            MyBaseThing.DeclareNMIProperty("MinValue", ePropertyTypes.TNumber);
            MyBaseThing.DeclareNMIProperty("MaxValue", ePropertyTypes.TNumber);
            MyBaseThing.DeclareNMIProperty("SeriesNames", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("Title", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("NoTE", ePropertyTypes.TBoolean);
            MyBaseThing.DeclareNMIProperty("Units", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("Format", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("Options", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("MainBackground", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("Background", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("Foreground", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("ForegroundOpacity", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("Opacity", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("Disabled", ePropertyTypes.TBoolean);
            MyBaseThing.DeclareNMIProperty("Visibility", ePropertyTypes.TBoolean);
            MyBaseThing.DeclareNMIProperty("Speed", ePropertyTypes.TNumber);
            MyBaseThing.DeclareNMIProperty("Delay", ePropertyTypes.TNumber);
            MyBaseThing.DeclareNMIProperty("Throttle", ePropertyTypes.TNumber);
            MyBaseThing.DeclareNMIProperty("Group", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("NUITags", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("Label", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("LabelClassName", ePropertyTypes.TString);
            MyBaseThing.DeclareNMIProperty("LabelForeground", ePropertyTypes.TString);
            MyBaseThing.SetIThingObject(this);
        }

        /// <summary>
        /// If false the Tag will not be monitored
        /// </summary>
        public bool DontMonitor
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "DontMonitor"); }
            set
            {
                MyBaseThing.SetProperty("DontMonitor", value.ToString(), ePropertyTypes.TBoolean);
            }
        }

        /// <summary>
        /// True if the tag is active
        /// </summary>
        public bool IsActive
        {
            get { return TheThing.GetSafePropertyBool(MyBaseThing, "IsActive"); }
            set
            {
                MyBaseThing.SetProperty("IsActive", value.ToString(), ePropertyTypes.TBoolean);
            }
        }

        /// <summary>
        /// Last message of the live tag
        /// </summary>
        public string LastMessage
        {
            get { return MyBaseThing.LastMessage; }
            set { MyBaseThing.LastMessage=value; }
        }

        /// <summary>
        /// Constrol type of the value field
        /// </summary>
        public string ControlType
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "ControlType"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "ControlType", value); }
        }

        /// <summary>
        /// Title of the Form
        /// </summary>
        public string FormTitle
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "FormTitle"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "FormTitle", value); }
        }

        /// <summary>
        /// Position left of the value field
        /// </summary>
        public int TileLeft
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "TileLeft")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "TileLeft", value); }
        }
        /// <summary>
        /// Position to of the value field
        /// </summary>
        public int TileTop
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "TileTop")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "TileTop", value); }
        }
        /// <summary>
        /// Width of the value field
        /// </summary>
        public int TileWidth
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "TileWidth")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "TileWidth", value); }
        }
        /// <summary>
        /// Height of the value field
        /// </summary>
        public int TileHeight
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "TileHeight")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "TileHeight", value); }
        }
        /// <summary>
        /// Flags for the value field control
        /// </summary>
        public int Flags
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "Flags")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "Flags", value); }
        }
        /// <summary>
        /// FldOrder of the value control
        /// </summary>
        public int FldOrder
        {
            get { return TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(MyBaseThing, "FldOrder")); }
            set { TheThing.SetSafePropertyNumber(MyBaseThing, "FldOrder", value); }
        }
        /// <summary>
        /// Server name if applicable
        /// </summary>
        public string ServerName
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "ServerName"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "ServerName", value); }
        }
        /// <summary>
        /// Server ID if applicable
        /// </summary>
        public string ServerID
        {
            get { return TheThing.GetSafePropertyString(MyBaseThing, "ServerID"); }
            set { TheThing.SetSafePropertyString(MyBaseThing, "ServerID", value); }
        }
    }

    internal class TheNMITagLogEntry : TheDataBase
    {
        public double Value { get; set; }
        public string ID { get; set; }
        public string EngineName { get; set; }

        public string EventLabel { get; set; }

        public string Comment { get; set; }
    }

    internal class TheNMITagLog: TheThingBase
    {
        public override bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;
            if (string.IsNullOrEmpty(MyBaseThing.ID))
                MyBaseThing.ID = Guid.NewGuid().ToString();
            MyBaseThing.FireEvent("OnInitialized", this, new TSM(MyBaseThing.cdeMID.ToString(), "Was Init"), false);

            TheBaseEngine.WaitForStorageReadiness(sinkStorageStationIsReadyFired, true);
            return false;
        }

        private void sinkStorageStationIsReadyFired(ICDEThing sender, object pReady)
        {
            if (pReady != null)
            {
                if (MyTagLog == null)
                {
                    MyTagLog = new TheStorageMirror<TheNMITagLogEntry>(TheCDEngines.MyIStorageService) {IsRAMStore = true};
                    MyTagLog.SetRecordExpiration(600, null);
                    MyTagLog.IsCachePersistent = true;
                    MyTagLog.CacheStoreInterval = 50;
                    MyTagLog.IsStoreIntervalInSeconds = true;
                    MyTagLog.CacheTableName = "TagLog" + MyBaseThing.ID;
                    if (!MyTagLog.IsRAMStore)
                        MyTagLog.CreateStore("Live Tag Log", "Historian of Live Tags", null, true, false);
                    else
                        MyTagLog.InitializeStore(true, false);
                }
                mIsInitialized = true;
                FireEvent(eThingEvents.Initialized, this, true, true);
            }
        }

        TheStorageMirror<TheNMITagLogEntry> MyTagLog;   //The Storage Container for data to store

        public Guid StoreMID { get { return MyTagLog.StoreMID; } }
        public void Write(TheNMILiveTag pTag,string pEventLabel,string pComment)
        {
            TheNMITagLogEntry tEntry = new ()
            {
                ID = pTag.GetBaseThing().ID,
                Value = TheCommonUtils.CDbl(pTag.GetBaseThing().Value),
                EngineName = pTag.GetBaseThing().EngineName,
                EventLabel = pEventLabel,
                Comment = pComment
            };
            MyTagLog.AddAnItem(tEntry);
        }
    }
}
