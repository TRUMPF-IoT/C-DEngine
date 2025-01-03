// SPDX-FileCopyrightText: Copyright (c) 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

#if CDE_INTNEWTON
using cdeNewtonsoft.Json;
#else
using Newtonsoft.Json;
#endif

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;
using NMI = nsCDEngine.Engines.NMIService.TheNMIEngine;
using TT = nsCDEngine.Engines.ThingService.TheThing;

#pragma warning disable CS1591    //TODO: Remove and document public methods

#if CDE_NET4
// .Net 4 does not have this, while Net 3.5 does: workaround to make this compile-time only feature work:
namespace System.Runtime.CompilerServices {
    sealed class CallerMemberNameAttribute : Attribute { }
}
#endif

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// Base class for C-DEngine plugins
    /// </summary>
    public class ThePluginBase : TheThingBase, ICDEPlugin
    {
        /// <summary>
        /// Pointer to the IBaseEngine of this Plugin
        /// </summary>
        protected IBaseEngine MyBaseEngine;
        /// <summary>
        /// Returns the BaseEngine of this plugin (called by the C-DEngine during startup)
        /// </summary>
        /// <returns></returns>
        public virtual IBaseEngine GetBaseEngine()
        {
            return MyBaseEngine;
        }
        /// <summary>
        /// This method is called by The C-DEngine during initialization in order to register this service
        /// You must add several calls to this method for the plugin to work correctly:
        /// MyBaseEngine.SetFriendlyName("Friendly name of your plugin");
        /// MyBaseEngine.SetEngineID(new Guid("{a fixed guid you create with the GUID Tool}"));
        /// MyBaseEngine.SetPluginInfo(...All Information of the plugin for the Store...);
        /// </summary>
        /// <param name="pBase">The C-DEngine is creating a Host for this engine and hands it to the Plugin Service</param>
        public virtual void InitEngineAssets(IBaseEngine pBase)
        {
            MyBaseEngine = pBase;
            MyBaseEngine.SetEngineName(GetType().FullName);
            MyBaseEngine.SetEngineType(GetType());
            MyBaseEngine.SetEngineService(true);
        }

        /// <summary>
        /// Default handler for Plugins. Override if necessary
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="pIncoming"></param>
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            if (pIncoming is not TheProcessMessage pMsg) return;
            switch (pMsg.Message.TXT)   //string 2 cases
            {
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);
                    break;
                //If the Service receives an "INITIALIZE" it fires up all its code and sends INITIALIZED back
                case "CDE_INITIALIZE":
                    if (MyBaseEngine.GetEngineState().IsService)
                    {
                        if (!MyBaseEngine.GetEngineState().IsEngineReady)
                            MyBaseEngine.SetEngineReadiness(true, null);
                        MyBaseEngine.ReplyInitialized(pMsg.Message);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Base Class for all Things Derived from ICDEThing
    /// </summary>
    public class TheThingBase : ICDEThing
    {
        #region ICDEThing Methods
        /// <summary>
        /// Sets the Base Thing (MyBaseThing)
        /// </summary>
        /// <param name="pThing"></param>
        public virtual void SetBaseThing(TheThing pThing)
        {
            MyBaseThing = pThing;
        }
        /// <summary>
        /// Gets the Base Thing (MyBaseThing)
        /// </summary>
        /// <returns></returns>
        public virtual TheThing GetBaseThing()
        {
            return MyBaseThing;
        }
        /// <summary>
        /// Gets a property by name
        /// </summary>
        /// <param name="pName">Name of the property</param>
        /// <param name="DoCreate">if true, the property will be created</param>
        /// <returns></returns>
        public virtual cdeP GetProperty(string pName, bool DoCreate)
        {
            if (MyBaseThing != null)
                return MyBaseThing.GetProperty(pName, DoCreate);
            return null;
        }
        /// <summary>
        /// Sets a property with a value
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Property Value</param>
        /// <returns></returns>
        public virtual cdeP SetProperty(string pName, object pValue)
        {
            if (MyBaseThing != null)
                return MyBaseThing.SetProperty(pName, pValue);
            return null;
        }

        /// <summary>
        /// Register a callback with an event of this Thing
        /// </summary>
        /// <param name="pName">Name of the event</param>
        /// <param name="pCallBack">Callback to register</param>
        public virtual void RegisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            if (MyBaseThing != null)
                MyBaseThing.RegisterEvent(pName, pCallBack);
        }
        /// <summary>
        /// Unregister a callback from an event of this thing
        /// </summary>
        /// <param name="pName">Event Name</param>
        /// <param name="pCallBack">Callback to unregister</param>
        public virtual void UnregisterEvent(string pName, Action<ICDEThing, object> pCallBack)
        {
            if (MyBaseThing != null)
                MyBaseThing.UnregisterEvent(pName, pCallBack);
        }
        /// <summary>
        /// Fire an event on this thing
        /// </summary>
        /// <param name="pEventName">Event Name</param>
        /// <param name="sender">initiator of the event fire</param>
        /// <param name="pPara">Parameters of the event fire</param>
        /// <param name="FireAsync">True if the event will fire asynch</param>
        public virtual void FireEvent(string pEventName, ICDEThing sender, object pPara, bool FireAsync)
        {
            if (MyBaseThing != null)
                MyBaseThing.FireEvent(pEventName, sender, pPara, FireAsync);
        }
        /// <summary>
        /// Returns true if the given event has registered callbacks
        /// </summary>
        /// <param name="pEventName">Name of the event</param>
        /// <returns></returns>
        public virtual bool HasRegisteredEvents(string pEventName)
        {
            if (MyBaseThing != null)
                return MyBaseThing.HasRegisteredEvents(pEventName);
            return false;
        }

        /// <summary>
        /// Base Thing of this class. All Persistable properties will be stored with this BaseThing to TheThingRegistry
        /// </summary>
        protected TheThing MyBaseThing;
        /// <summary>
        /// True if the NMI was called
        /// </summary>
        protected bool mIsUXInitCalled;
        /// <summary>
        /// True if the NMI is initialized
        /// </summary>
        protected bool mIsUXInitialized;
        /// <summary>
        /// True if IsInit was called already
        /// </summary>
        protected bool mIsInitCalled;
        /// <summary>
        /// True if the Thing is initialized
        /// </summary>
        protected bool mIsInitialized;
        /// <summary>
        /// Returns true if this thing's NMI is initialized (mIsUxInitialized)
        /// </summary>
        /// <returns></returns>
        public virtual bool IsUXInit()
        { return mIsUXInitialized; }
        /// <summary>
        /// Returns true if this thing is initialized (mIsInitialized)
        /// </summary>
        /// <returns></returns>
        public virtual bool IsInit()
        { return mIsInitialized; }

        /// <summary>
        /// Will be called if this thing is deleted from TheThingRegistry
        /// </summary>
        /// <returns></returns>
        public virtual bool Delete()
        {
            mIsInitialized = false;
            return true;
        }

        /// <summary>
        /// Will be called by the NMI Model to create the Thing's NMI
        /// </summary>
        /// <returns></returns>
        public virtual bool CreateUX()
        {
            if (mIsUXInitCalled) return false;
            mIsUXInitCalled = true;
            mIsUXInitialized = true;
            return true;
        }

        /// <summary>
        /// Will be called by TheThingRegistry to initialized this thing
        /// </summary>
        /// <returns></returns>
        public virtual bool Init()
        {
            if (mIsInitCalled) return false;
            mIsInitCalled = true;
            mIsInitialized = true;
            return true;
        }

        /// <summary>
        /// Will be called by the communication engine if a message for this thing has been received
        /// </summary>
        /// <param name="sender">Sender of the message</param>
        /// <param name="pIncoming">Incoming message</param>
        public virtual void HandleMessage(ICDEThing sender, object pIncoming)
        {
        }


        /// <summary>
        /// Convenience Function to set LastMessage UX Property and allows to automatically set LastUpdate and WriteToLog
        /// </summary>
        /// <param name="pMessage">Message to show in LastMessage of the DeviceStatus</param>
        /// <param name="SetLastUpdate">Shows the current TimeStamp</param>
        /// <param name="LogID">If this message is also going to the systemlog, add a log ID here</param>
        /// <param name="pMsgLevel">If a log ID is set, a Message Level can be set, too</param>
        public virtual void SetMessage(string pMessage, DateTimeOffset? SetLastUpdate = null, int LogID = 0, eMsgLevel pMsgLevel = eMsgLevel.l4_Message)
        {
            SetMessage(pMessage, null, -1, SetLastUpdate, LogID, pMsgLevel);
        }

        /// <summary>
        /// Convenience Function to set LastMessage UX Property and allows to automatically set LastUpdate and WriteToLog
        /// </summary>
        /// <param name="pMessage">Message to show in LastMessage of the DeviceStatus</param>
        /// <param name="pStatusLevel">Sets the status Level of MyBaseThing if set to >=0</param>
        /// <param name="SetLastUpdate">Shows the current TimeStamp</param>
        /// <param name="LogID">If this message is also going to the systemlog, add a log ID here</param>
        /// <param name="pMsgLevel">If a log ID is set, a Message Level can be set, too</param>
        public virtual void SetMessage(string pMessage, int pStatusLevel = -1, DateTimeOffset? SetLastUpdate = null, int LogID = 0, eMsgLevel pMsgLevel = eMsgLevel.l4_Message)
        {
            SetMessage(pMessage, null, pStatusLevel, SetLastUpdate, LogID, pMsgLevel);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pMessage">Message to show in LastMessage of the DeviceStatus</param>
        /// <param name="EventLogEntryName">If set, the pmessage is also added to the Event Log</param>
        /// <param name="pStatusLevel">Sets the status Level of MyBaseThing if set to >=0</param>
        /// <param name="SetLastUpdate">Shows the current TimeStamp</param>
        /// <param name="LogID">If this message is also going to the systemlog, add a log ID here</param>
        /// <param name="pMsgLevel">If a log ID is set, a Message Level can be set, too</param>
        public virtual void SetMessage(string pMessage, string EventLogEntryName = null, int pStatusLevel = -1, DateTimeOffset? SetLastUpdate = null, int LogID = 0, eMsgLevel pMsgLevel = eMsgLevel.l4_Message)
        {
            if (string.IsNullOrEmpty(pMessage) || MyBaseThing == null)
                return;
            if (SetLastUpdate != null)
            {
                MyBaseThing.LastUpdate = CU.CDate(SetLastUpdate);
                pMessage = $"{CU.GetDateTimeString(MyBaseThing.LastUpdate, -1)} - {pMessage}";
            }
            if (pStatusLevel >= 0)
                MyBaseThing.SetStatus(pStatusLevel, pMessage);
            else
                MyBaseThing.LastMessage = pMessage;
            if (LogID > 0)
            {
                TSM toLog = new(MyBaseThing.EngineName, pMessage, pMsgLevel);
                if (SetLastUpdate != null)
                    toLog.TIM = CU.CDate(SetLastUpdate);
                TheBaseAssets.MySYSLOG?.WriteToLog(LogID, toLog);
            }
            if (!string.IsNullOrEmpty(EventLogEntryName))
            {
                TheLoggerFactory.LogEvent(eLoggerCategory.ThingEvent, EventLogEntryName, pMsgLevel, pMessage, MyBaseThing.FriendlyName);
            }
        }
        #endregion

        #region NMI FacePlates for Group-Screens
        public virtual int AddDeviceFace(TheFormInfo MyLiveForm, int pLeft, int pTop, ThePropertyBag pProperties = null)
        {
            if (!IsUXInit())
            {
                if (!MyBaseThing.IsDisabled && !HasRegisteredEvents(eUXEvents.OnUXCreated))
                {
                    RegisterEvent(eUXEvents.OnUXCreated, (sender, obj) =>
                    {
                        ShowDeviceFace(MyLiveForm, pLeft, pTop, pProperties);
                    });
                }
                return -1;
            }
            return ShowDeviceFace(MyLiveForm, pLeft, pTop, pProperties);
        }
        /// <summary>
        /// Shows the Device Faceplate that can be used in Group Screens
        /// THIS IS IN PREVIEW - API Can Change - use with caution!
        /// </summary>
        /// <param name="MyLiveForm">Target Form to show the FacePlate in</param>
        /// <param name="pLeft">Left (in Pixels) to position the FacePlate</param>
        /// <param name="pTop">Top (in Pixels) to position the FacePlate</param>
        /// <param name="pProperties">Set Properties on the DeviceFace</param>
        /// <returns></returns>
        protected virtual int ShowDeviceFace(TheFormInfo MyLiveForm, int pLeft, int pTop, ThePropertyBag pProperties = null)
        {
            if (MyNMIFaceModel == null)
            {
                return -1;
            }
            MyNMIFaceModel.SetPos(pLeft, pTop);
            cdeP FldStartProp = MyBaseThing.GetProperty($"FldStart_{MyLiveForm.cdeMID}", false);
            int startFld = 100;
            if (FldStartProp == null || CU.CInt(FldStartProp.GetValue()) == 0)
            {
                var group = TheThingRegistry.GetThingByProperty("*", Guid.Empty, "MyGroupMID_ID", MyLiveForm.cdeMID.ToString());
                if (group != null)
                {
                    var tGS = group.GetObject() as TheThingGroup;
                    int max = 100;
                    foreach (var t in tGS.GetAllGroupThings())
                    {
                        var tn = CU.CInt(t.GetBaseThing().GetProperty($"FldStart_{MyLiveForm.cdeMID}", false)?.GetValue());
                        if (tn > max)
                        {
                            max = tn;
                        }
                    }
                    startFld = 25 * (int)Math.Round((max + 13) / 25.0);
                }
            }
            else
                startFld = CU.CInt(FldStartProp.GetValue());
            MyBaseThing.SetProperty($"FldStart_{MyLiveForm.cdeMID}", startFld);
            MyLiveForm.FldStart = startFld;
            var tPins = MyBaseThing.GetAllPins();
            bool hasRightPin = tPins.Exists(p => p.NMIPinLocation == ThePin.ePinLocation.Right);
            bool hasLeftPin = tPins.Exists(p => p.NMIPinLocation == ThePin.ePinLocation.Left);
            var tFaceWidth = (MyNMIFaceModel.XLen + (78 * ((hasLeftPin ? 1 : 0) + (hasRightPin ? 1 : 0))));
            int zindex = CU.CInt(ThePropertyBag.PropBagGetValue(pProperties, "ZIndex", "="));
            var tFrameFld = NMI.AddSmartControl(MyBaseThing, MyLiveForm, eFieldType.TileGroup, startFld, 0, 0, null, null, new nmiCtrlTileGroup { IsAbsolute = true, DisallowEdit = !CU.CBool(TheBaseAssets.MySettings.GetSetting("RedPill")), AllowDrag = true, Left = pLeft, Top = pTop, PixelWidth = tFaceWidth, PixelHeight = MyNMIFaceModel.YLen, Style = $"touch-action: none; z-index:{20+zindex};" });
            if (!TheBaseAssets.MyServiceHostInfo.IsCloudService && CU.CBool(TheBaseAssets.MySettings.GetSetting("RedPill")))
            {
                tFrameFld?.RegisterEvent2(eUXEvents.OnShowEditor, (pMsg, obj) =>
                {
                    pMsg.Cookie = OnShowEditor(NMI.GetNMIEditorForm(), $"THING_{MyBaseThing.cdeMID}", pMsg);
                });
                tFrameFld?.RegisterEvent2(eUXEvents.OnHideEditor, (pMsg, obj) =>
                {
                    pMsg.Cookie = OnHideEditor(NMI.GetNMIEditorForm(), $"THING_{MyBaseThing.cdeMID}", pMsg);
                });
            }
            if (tFrameFld != null)
            {
                MyNMIFaceModel.OwnerFormInfoId = tFrameFld.cdeMID;
                var tso = TheFormsGenerator.GetScreenOptions(MyLiveForm.cdeMID, null, MyLiveForm);
                var po = tso?.Flds?.Find(s => s.FldOrder == startFld);
                if (po?.PO!=null)
                {
                    tFrameFld.PropertyBag = po.PO;
                }
            }
            //Pin Render
            bool hideBorder = ThePropertyBag.PropBagHasValue(pProperties, "HideBorder", "=");
            foreach (var pin in tPins)
            {
                TheFieldInfo tfld = null;
                if (pin.NMIPinLocation == ThePin.ePinLocation.Left)
                    tfld = NMI.AddSmartControl(MyBaseThing, MyLiveForm, eFieldType.FacePlate, MyLiveForm.FldPos, 0, 0, null, "FriendlyName",
                        new nmiCtrlFacePlate { NoTE = true, ParentFld = startFld, PixelWidth = pin.NMIPinWidth, PixelHeight = pin.NMIPinHeight, IsAbsolute = true, Left = 78 - pin.NMIPinWidth, Top = (39 * pin.NMIPinPosition) + 15, HTML = pin.NMIGetPinLineFace("") });
                else
                    tfld = NMI.AddSmartControl(MyBaseThing, MyLiveForm, eFieldType.FacePlate, MyLiveForm.FldPos, 0, 0, null, "FriendlyName",
                        new nmiCtrlFacePlate { NoTE = true, ParentFld = startFld, PixelWidth = pin.NMIPinWidth /*- (hideBorder ? 0 : 4)*/, PixelHeight = pin.NMIPinHeight, IsAbsolute = true, Left = tFaceWidth - ((78 - (hideBorder ? 0 : 4)) - pin.NMIxDelta), Top = (39 * pin.NMIPinPosition) + 15 + pin.NMIyDelta, HTML = pin.NMIGetPinLineFace("") });
                if (!TheBaseAssets.MyServiceHostInfo.IsCloudService && CU.CBool(TheBaseAssets.MySettings.GetSetting("RedPill")))
                {
                    tfld?.RegisterEvent2(eUXEvents.OnShowEditor, (pMsg, obj) =>
                    {
                        pMsg.Cookie = OnShowEditor(NMI.GetNMIEditorForm(), $"PIN_{pin.PinName}", pMsg);
                    });
                    tfld?.RegisterEvent2(eUXEvents.OnHideEditor, (pMsg, obj) =>
                    {
                        pMsg.Cookie = OnHideEditor(NMI.GetNMIEditorForm(), $"PIN_{pin.PinName}", pMsg);
                    });
                }
            }
            //FacePlate Render
            var tg = new nmiCtrlTileGroup { ParentFld = startFld, NoTE = true, PixelWidth = MyNMIFaceModel.XLen, DisallowEdit = true, PixelHeight = MyNMIFaceModel.YLen, IsAbsolute = true, Left = hasLeftPin ? 78 : 0, Top = 0 };
            startFld = MyLiveForm.FldPos;
            while (NMI.GetFieldByFldOrder(MyLiveForm, startFld) != null)
                startFld = MyLiveForm.FldPos;
            var gfld = NMI.AddSmartControl(MyBaseThing, MyLiveForm, eFieldType.TileGroup, startFld, 0, 0, null, null, tg);
            if (gfld != null)
            {
                if (pProperties != null)
                    gfld.PropertyBag = pProperties;
                if (!hideBorder)
                {
                    string tClassName = ThePropertyBag.PropBagGetValue(pProperties, "ClassName");
                    string tBorderName = ThePropertyBag.PropBagGetValue(pProperties, "BorderClass");
                    if (!string.IsNullOrEmpty(tBorderName))
                    {
                        if (string.IsNullOrEmpty(tClassName)) tClassName = ""; else tClassName += " ";
                        tClassName += tBorderName;
                        gfld.PropertyBag = new nmiCtrlTileGroup { ClassName = tClassName };
                    }
                    else
                        gfld.PropertyBag = new nmiCtrlTileGroup { Style = "border: outset;" };
                }
            }
            if (!string.IsNullOrEmpty(MyNMIFaceModel.FaceTemplate))
            {
                var tp = new nmiCtrlFacePlate { ParentFld = startFld, NoTE = true, DisallowEdit = true, IsAbsolute = true, PixelWidth = MyNMIFaceModel.XLen, PixelHeight = MyNMIFaceModel.YLen };
                if (MyNMIFaceModel.FaceTemplate.StartsWith('/'))
                    tp.HTMLUrl = MyNMIFaceModel.FaceTemplate;
                else
                    tp.HTML = $"<div class=\"{MyNMIFaceModel.FaceTemplate}\"></div>";
                NMI.AddSmartControl(MyBaseThing, MyLiveForm, eFieldType.FacePlate, MyLiveForm.FldPos, 0, 0, null, "FriendlyName", tp);
                if (MyNMIFaceModel.AddTTS)
                    NMI.AddSmartControl(MyBaseThing, MyLiveForm, eFieldType.TileButton, MyLiveForm.FldPos, 2, 0x0, null, null, new nmiCtrlTileButton() { ParentFld = startFld, OnClick = $"TTS:{MyBaseThing.cdeMID}", DisallowEdit = true, IsAbsolute = true, PixelWidth = MyNMIFaceModel.XLen, PixelHeight = MyNMIFaceModel.YLen, NoTE = true, ClassName = "enTransBut" });
            }
            return startFld;
        }

        public virtual bool OnHideEditor(TheFormInfo pForm, string pTarget, TheProcessMessage pMsg)
        {
            if (pTarget.StartsWith("PIN_"))
            {
                MyBaseThing.RemoveProperty($"{pTarget}_PinLocation");
                MyBaseThing.RemoveProperty($"{pTarget}_PinPosition");
                MyBaseThing.RemoveProperty($"{pTarget}_PinWidth");
                MyBaseThing.RemoveProperty($"{pTarget}_PinXDelta");
                MyBaseThing.RemoveProperty($"{pTarget}_PinProperty");
                MyBaseThing.RemoveProperty($"{pTarget}_NewConnection");
                MyPinOverrideUpdate?.UnregisterUXEvent(MyBaseThing, eUXEvents.OnClick, pTarget, null);
                MyPinOverrideClear?.UnregisterUXEvent(MyBaseThing, eUXEvents.OnClick, pTarget, null);
                MyConnectionAdded?.UnregisterUXEvent(MyBaseThing, eUXEvents.OnClick, pTarget, null);
                MyPinMapperButton?.UnregisterUXEvent(MyBaseThing, eUXEvents.OnClick, pTarget, null);
                return true;
            }
            return false;
        }

        public virtual bool OnShowEditor(TheFormInfo pForm, string pTarget, TheProcessMessage pMsg)
        {
            if (pTarget.StartsWith("PIN_"))
            {
                var pinID = pTarget.Split('_')[1];
                var pin = MyBaseThing.GetPin(pinID);
                string tCompatPins = "";
                foreach (var tC in pin.GetConnectedPins())
                {
                    if (tCompatPins.Length > 0) tCompatPins += ";";
                    tCompatPins += $"Disconnect ({tC.GetResolvedName()}):DC,{tC.cdeO},{tC.PinName}";
                }
                foreach (var tC in pin.CompatiblePins)
                {
                    if (tCompatPins.Length > 0) tCompatPins += ";";
                    tCompatPins += $"{tC.GetResolvedName()}:{tC.cdeO},{tC.PinName}";
                }
                string tIC = "";
                foreach (var tC in pin.GetConnectedPins())
                {
                    if (tIC.Length > 0) tIC += ";";
                    tIC += tC.GetResolvedName();
                }

                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.SingleEnded, 10, 0, 0, "Pin Name", null, new nmiCtrlSingleEnded { NoTE = true, Value = pin.PinName, FontSize = 20, TileWidth=3 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.SingleCheck, 11, 0, 0, "Inbound", null, new nmiCtrlSingleCheck { NoTE = true, Value = pin.IsInbound,Title="Inbound", TileWidth = 1 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.SingleEnded, 12, 0, 0, "Pin Value", null, new nmiCtrlSingleEnded { NoTE = true, Value = $"{pin.PinValue} {pin.Units}", FontSize = 20, TileWidth=2 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.CollapsibleGroup, 15, 2, 0, "Pin Overrides", null, new nmiCtrlCollapsibleGroup { IsSmall=true, DoClose=true, TileWidth=6 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.ComboBox, 16, 2, 0, "Location", $"{pTarget}_PinLocation", new nmiCtrlComboBox{ ParentFld = 15, NoTE = true, Value = $"{(int)pin.NMIPinLocation}", Options="Left:0;Right:1", TileWidth=1 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.Number, 17, 2, 0, "Position", $"{pTarget}_PinPosition", new nmiCtrlNumber { ParentFld = 15, NoTE = true, Value = $"{pin.NMIPinPosition}", TileWidth=1 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.Number, 18, 2, 0, "Width", $"{pTarget}_PinWidth", new nmiCtrlNumber { ParentFld = 15, NoTE = true, Value = $"{pin.NMIPinWidth}", TileWidth = 1 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.Number, 19, 2, 0, "Height", $"{pTarget}_PinHeight", new nmiCtrlNumber { ParentFld = 15, NoTE = true, Value = $"{pin.NMIPinHeight}", TileWidth = 1 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.Number, 20, 2, 0, "XDelta", $"{pTarget}_PinXDelta", new nmiCtrlNumber { ParentFld = 15, NoTE = true, Value = $"{pin.NMIxDelta}", TileWidth = 1 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.Number, 21, 2, 0, "YDelta", $"{pTarget}_PinYDelta", new nmiCtrlNumber { ParentFld = 15, NoTE = true, Value = $"{pin.NMIyDelta}", TileWidth = 1 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.SingleEnded, 22, 0, 0, "TID", null, new nmiCtrlSingleEnded{ ParentFld = 15, NoTE = true, Value = $"{MyBaseThing.cdeMID}", TileWidth=1 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.PropertyPicker, 23, 2, 0, "Pin Property", $"{pTarget}_PinProperty", new nmiCtrlPropertyPicker { ParentFld = 15, NoTE = true, Value = $"{pin.PinProperty}", TileWidth = 5, ThingFld=18 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.SmartLabel, 24, 0, 0, "Pin Type", null, new nmiCtrlSmartLabel { ParentFld=15, NoTE = true, Value = $"UA: {pin.PinType}", FontSize = 20, TileFactorY = 2 });
                MyPinOverrideUpdate = NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.TileButton, 25, 2, 0, "Update Pin", null, new nmiCtrlTileButton { ParentFld=15, TileWidth = 4, NoTE = true, ClassName = "cdeGoodActionButton" });
                MyPinOverrideUpdate.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, pTarget, (sender, obj) =>
                {
                    try
                    {
                        var pmsg = obj as TheProcessMessage;
                        if (pmsg == null) return;

                        var pinID = pTarget.Split('_')[1];
                        var pin = MyBaseThing.GetPin(pinID);

                        var tP = new ThePinOR();
                        tP.PinOverrides=0;
                        if (MyBaseThing.GetProperty($"{pTarget}_PinOverrides", false) == null)
                        {
                            tP.oNMIPinLocation = pin.NMIPinLocation;
                            tP.oNMIPinPosition = pin.NMIPinPosition;
                            tP.oNMIPinWidth = pin.NMIPinWidth;
                            tP.oNMIxDelta = pin.NMIxDelta;
                            tP.oPinProperty = pin.PinProperty;
                        }

                        object tprop = MyBaseThing.GetProperty($"{pTarget}_PinLocation", false)?.GetValue();
                        if (tprop != null && pin.NMIPinLocation != (ThePin.ePinLocation)CU.CInt(tprop))
                        {
                            tP.NMIPinLocation = (ThePin.ePinLocation)CU.CInt(tprop);
                            pin.NMIPinLocation = tP.NMIPinLocation;
                            tP.PinOverrides |= 1;
                        }
                        tprop = $"{MyBaseThing.GetProperty($"{pTarget}_PinPosition", false)?.GetValue()}";
                        if (tprop!=null && pin.NMIPinPosition != CU.CInt(tprop))
                        {
                            tP.NMIPinPosition = CU.CInt(tprop);
                            pin.NMIPinPosition = tP.NMIPinPosition;
                            tP.PinOverrides |= 2;
                        }
                        tprop = $"{MyBaseThing.GetProperty($"{pTarget}_PinWidth", false)?.GetValue()}";
                        if (tprop!=null && pin.NMIPinWidth != CU.CInt(tprop) && CU.CInt(tprop)>0)
                        {
                            tP.NMIPinWidth = CU.CInt(tprop);
                            pin.NMIPinWidth = tP.NMIPinWidth;
                            tP.PinOverrides |= 4;
                        }
                        tprop = $"{MyBaseThing.GetProperty($"{pTarget}_PinXDelta", false)?.GetValue()}";
                        if (tprop!=null && pin.NMIxDelta != CU.CInt(tprop))
                        {
                            tP.NMIxDelta = CU.CInt(tprop);
                            pin.NMIxDelta = tP.NMIxDelta;
                            tP.PinOverrides |= 8;
                        }
                        tprop = $"{MyBaseThing.GetProperty($"{pTarget}_PinHeight", false)?.GetValue()}";
                        if (tprop != null && pin.NMIPinWidth != CU.CInt(tprop) && CU.CInt(tprop) > 0)
                        {
                            tP.NMIPinHeight = CU.CInt(tprop);
                            pin.NMIPinHeight = tP.NMIPinHeight;
                            tP.PinOverrides |= 16;
                        }
                        tprop = $"{MyBaseThing.GetProperty($"{pTarget}_PinYDelta", false)?.GetValue()}";
                        if (tprop != null && pin.NMIyDelta != CU.CInt(tprop))
                        {
                            tP.NMIyDelta = CU.CInt(tprop);
                            pin.NMIyDelta = tP.NMIyDelta;
                            tP.PinOverrides |= 32;
                        }
                        tprop = $"{MyBaseThing.GetProperty($"{pTarget}_PinProperty", false)?.GetValue()}";
                        if (tprop!=null && pin.PinProperty!=$"{tprop}")
                        {
                            tP.PinProperty = $"{tprop}";
                            pin.PinProperty = tP.PinProperty;
                            tP.PinOverrides |= 64;
                        }

                        if (tP.PinOverrides!=0)
                        {
                            var t = CU.SerializeObjectToJSONString(tP);
                            MyBaseThing.SetProperty($"{pTarget}_PinOverrides", t);
                            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Pin ({pTarget}) overrides updated."));
                            (MyBaseThing.GetObject() as ICDEThing)?.FireEvent(eThingEvents.PinUpdated, MyBaseThing, pin, true);
                        }
                    }
                    catch
                    {
                        //indended for now
                    }
                });
                MyPinOverrideClear = NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.TileButton, 26, 2, 0, "Clear", null, new nmiCtrlTileButton { ParentFld=15, TileWidth = 2, NoTE = true, ClassName = "cdeBadActionButton", AreYouSure="Are you sure to reset the overrides?" });
                MyPinOverrideClear.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, pTarget, (sender, obj) =>
                {
                    try
                    {
                        var pOver = $"{MyBaseThing.GetProperty($"{pTarget}_PinOverrides", false)?.GetValue()}";
                        if (!string.IsNullOrEmpty(pOver))
                        {
                            var pinID = pTarget.Split('_')[1];
                            var firstPin = MyBaseThing.GetPin(pinID);
                            ThePinOR tp = CU.DeserializeJSONStringToObject<ThePinOR>(pOver);
                            if ((tp.PinOverrides & 1) != 0)
                                firstPin.NMIPinLocation = tp.oNMIPinLocation;
                            if ((tp.PinOverrides & 2) != 0)
                                firstPin.NMIPinPosition = tp.oNMIPinPosition;
                            if ((tp.PinOverrides & 4) != 0)
                                firstPin.NMIPinWidth = tp.oNMIPinWidth;
                            if ((tp.PinOverrides & 8) != 0)
                                firstPin.NMIxDelta = tp.oNMIxDelta;
                            if ((tp.PinOverrides & 16) != 0)
                                firstPin.NMIPinHeight = tp.oNMIPinHeight;
                            if ((tp.PinOverrides & 32) != 0)
                                firstPin.NMIyDelta = tp.oNMIyDelta;
                            if ((tp.PinOverrides & 64) != 0)
                                firstPin.PinProperty = tp.oPinProperty;
                            MyBaseThing.RemoveProperty($"{pTarget}_PinOverrides");
                            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Pin ({pTarget}) overrides removed."));
                            (MyBaseThing.GetObject() as ICDEThing)?.FireEvent(eThingEvents.PinUpdated, MyBaseThing, firstPin, true);
                        }
                    }
                    catch
                    {
                        //indended for now
                    }
                });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.TextArea, 30, 0, 0, "Is Connected to", null, new nmiCtrlTextArea { NoTE = true, Value = $"{tIC}", TileHeight = 2, FontSize = 12 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.ComboBox, 32, 2, MyBaseThing.cdeA, "Select a compatible Pin to Connect to", $"{pTarget}_NewConnection", new nmiCtrlComboBox { TileWidth = 5, NoTE = true, Options = tCompatPins });
                MyConnectionAdded = NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.TileButton, 33, 2, 0, null, null, new nmiCtrlTileButton { TileWidth = 1, Thumbnail = "FA2:f0c1", Foreground="white", NoTE = true, ClassName = "cdeGoodActionButton" });
                MyConnectionAdded.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, pTarget, (sender, obj) =>
                {
                    try
                    {
                        var pmsg = obj as TheProcessMessage;
                        if (pmsg == null) return;

                        var tPin = pmsg.Message.PLS.Split(':')[1];
                        var pinID = tPin.Split('_')[1];
                        var tSourcePin = MyBaseThing.GetPin(pinID);
                        if (tSourcePin == null) return;

                        var targetPin = $"{MyBaseThing.GetProperty($"{pTarget}_NewConnection").GetValue()}";
                        if (string.IsNullOrEmpty(targetPin)) return;
                        var tPinParts = targetPin.Split(',');
                        if (tPinParts.Length > 2 && tPinParts[0] == "DC")
                        {
                            var tTargetPin = tSourcePin.GetConnectedPins().Find(s => s.cdeO == CU.CGuid(tPinParts[1]) && s.PinName == tPinParts[2]);
                            MyBaseThing.RemovePinConnections(pin, [tTargetPin]);
                            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Pin ({pTarget}) connection removed."));
                            MyBaseThing.SetProperty($"{pTarget}_NewConnection", null);
                            (MyBaseThing.GetObject() as ICDEThing)?.FireEvent(eThingEvents.PinUpdated, MyBaseThing, tSourcePin, true);
                            return;
                        }
                        if (tPinParts.Length > 1)
                        {
                            var tTargetPin = tSourcePin.CompatiblePins.Find(s => s.cdeO == CU.CGuid(tPinParts[0]) && s.PinName == tPinParts[1]);
                            if (tTargetPin == null) return;
                            ThePin.ConnectPins(tTargetPin, tSourcePin);
                            MyBaseThing.SetProperty($"{pTarget}_NewConnection",null);
                            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Pin ({pTarget}) connected."));
                            (MyBaseThing.GetObject() as ICDEThing)?.FireEvent(eThingEvents.PinUpdated, MyBaseThing, tSourcePin, true);
                        }
                    }
                    catch { 
                        //indended for now
                    }
                });

                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.ThingPicker, 35, 0x2, 0x0, "Pin Source-Thing", $"{pTarget}_ThgSource", new nmiCtrlThingPicker() { NoTE = true, Value=$"{MyBaseThing.GetProperty($"{pTarget}_ThgSource", false)}", TileWidth=3 });
                NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.PropertyPicker, 36, 0x2, 0x0, "Pin Source-Property", $"{pTarget}_ThgProp", new nmiCtrlPropertyPicker() { TileWidth = 2, Value= $"{MyBaseThing.GetProperty($"{pTarget}_ThgProp", false)}", NoTE = true, ThingFld = 35 });
                MyPinMapperButton = NMI.AddSmartControl(MyBaseThing, pForm, eFieldType.TileButton, 37, 2, 0, null, null, new nmiCtrlTileButton { TileWidth = 1, Thumbnail = "FA2:f021", NoTE = true, ClassName = "cdeGoodActionButton" });
                MyPinMapperButton.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, pTarget, (sender, obj) =>
                {
                    var pmsg=(obj as TheProcessMessage);
                    if (pmsg?.Message?.PLS == null)
                        return;
                    var tPin = pmsg.Message.PLS.Split(':')[1];
                    MyBaseThing.UpdatePinMapper(tPin);
                });
                return true;
            }
            return false;
        }

        protected TheFieldInfo MyPinOverrideUpdate = null;
        protected TheFieldInfo MyPinOverrideClear = null;
        protected TheFieldInfo MyConnectionAdded = null;
        protected TheFieldInfo MyPinMapperButton = null;

        /// <summary>
        /// Device Face Model - if null the Thing has no model
        /// </summary>
        public TheNMIFaceModel MyNMIFaceModel { get; private set; } = null;

        /// <summary>
        /// Sets the NMI Face PlateModel required by the Group Screen to set dimensions of the FacePlate
        /// This call has be called at least once before the FacNMIModel can be used
        /// </summary>
        /// <param name="pModelCode">A Code for the faceplate used to tag the plat in the group</param>
        /// <param name="xLen">Length of the FacePlate in pixels</param>
        /// <param name="yLen">Height of the FacePlate in pixels</param>
        /// <param name="pFaceTemplate">Template for the FacePlate HTML</param>
        /// <param name="bDisableTTS">No TTS button</param>
        /// <returns></returns>
        public virtual bool InitNMIDeviceFaceModel(string pModelCode = null, int xLen = 0, int yLen = 0, string pFaceTemplate=null, bool bDisableTTS=false)
        {
            if (MyBaseThing!=null && MyBaseThing.MyThingBase == null)
                MyBaseThing.MyThingBase = this;
            if (!string.IsNullOrEmpty(pModelCode))
            {
                if (string.IsNullOrEmpty(pFaceTemplate))
                    pFaceTemplate = "nmiFace";
                MyBaseThing?.SetProperty("FaceTemplate", pFaceTemplate);
                MyNMIFaceModel ??= new TheNMIFaceModel() { AddTTS = !bDisableTTS, FaceTemplate = pFaceTemplate };
                MyNMIFaceModel.SetNMIModel(xLen, yLen, pModelCode);
                return true;
            }
            return false;
        }
        #endregion

        #region NMI Log
        public bool EnableNMILog
        {
            get { return TT.MemberGetSafePropertyBool(MyBaseThing); }
            set { TT.MemberSetSafePropertyBool(MyBaseThing, value); }
        }

        public virtual Dictionary<string, TheFieldInfo> AddNMILog(TT pBaseThing, TheFormInfo pMyForm, int StartFld, int ParentFld, string pTitle = null, int pWidth = 6, int pHeight = 5, bool EnableOnStartup = false)
        {
            var tFlds = new Dictionary<string, TheFieldInfo>();
            if (!EnableOnStartup)
            {
                EnableNMILog = false;
                ClearNMILog();
            }
            tFlds["Group"] = NMI.AddSmartControl(pBaseThing, pMyForm, eFieldType.CollapsibleGroup, StartFld, 2, 0, string.IsNullOrEmpty(pTitle) ? "Debug Info" : pTitle, null, new nmiCtrlCollapsibleGroup { ParentFld = ParentFld, TileWidth = pWidth, DoClose = true, IsSmall = true });
            tFlds["Check"] = NMI.AddSmartControl(pBaseThing, pMyForm, eFieldType.SingleCheck, StartFld + 10, 2, 0, "Enable Log", nameof(EnableNMILog), new nmiCtrlSingleCheck { ParentFld = StartFld, TileWidth = 3 });
            tFlds["ClearButton"] = NMI.AddSmartControl(pBaseThing, pMyForm, eFieldType.TileButton, StartFld + 20, 2, 0, "Clear Log", null, new nmiCtrlTileButton { NoTE = true, TileWidth = 3, ParentFld = StartFld });
            tFlds["ClearButton"].RegisterUXEvent(pBaseThing, eUXEvents.OnClick, "clear", (a, b) =>
            {
                ClearNMILog();
            });
            tFlds["NMILog"] = NMI.AddSmartControl(pBaseThing, pMyForm, eFieldType.TextArea, StartFld + 30, 0, 0, "Log", "NMILog", new nmiCtrlTextArea { ParentFld = StartFld, NoTE = true, TileHeight = pHeight, TileWidth = pWidth });
            return tFlds;
        }

        public virtual void ClearNMILog()
        {
            MyNMILog.Clear();
            TT.SetSafePropertyString(MyBaseThing, "NMILog", "");
        }

        private readonly StringBuilder MyNMILog = new();
        public void WriteToNMILog(string pMessage, int pStatusLevel, DateTimeOffset? SetLastUpdate = null, int LogID = 0, eMsgLevel pMsgLevel = eMsgLevel.l4_Message)
        {
            WriteToNMILog(pMessage, true, null, pStatusLevel, SetLastUpdate, 0, pMsgLevel);
        }
        public void WriteToNMILog(string pMessage, DateTimeOffset? SetLastUpdate, int LogID = 0, eMsgLevel pMsgLevel = eMsgLevel.l4_Message)
        {
            WriteToNMILog(pMessage, true, null, -1, SetLastUpdate, 0, pMsgLevel);
        }
        public void WriteToNMILog(string pMessage, string EventLogEntryName, int pStatusLevel, DateTimeOffset? SetLastUpdate = null, int LogID = 0, eMsgLevel pMsgLevel = eMsgLevel.l4_Message)
        {
            WriteToNMILog(pMessage, true, EventLogEntryName, pStatusLevel, SetLastUpdate, 0, pMsgLevel);
        }
        public virtual void WriteToNMILog(string txt, bool AddToLog = false, string EventLogEntryName = null, int pStatusLevel = -1, DateTimeOffset? SetLastUpdate = null, int LogID = 0, eMsgLevel pMsgLevel = eMsgLevel.l4_Message)
        {
            if (string.IsNullOrEmpty(txt)) return;
            if (EnableNMILog)
            {
                if (MyNMILog.Length > 0)
                    MyNMILog.Insert(0, $"{CU.GetDateTimeString(DateTimeOffset.Now, Guid.Empty)}: {txt}\n");
                else
                    MyNMILog.Append($"{CU.GetDateTimeString(DateTimeOffset.Now, Guid.Empty)}: {txt}\n");
                TT.SetSafePropertyString(MyBaseThing, "NMILog", MyNMILog.ToString());
            }
            if (AddToLog)
                SetMessage(txt, EventLogEntryName, pStatusLevel, SetLastUpdate, LogID, pMsgLevel);
        }
        #endregion
        public virtual bool AddPinsAndUpdate(List<ThePin> pIns)
        {
            if (MyBaseThing?.AddPins(pIns)==true)
            {
                UpdatePinNMI();
                return true;
            }
            return false;
        }

        /// <summary>Accp
        /// Updates the pins with NMI changes
        /// </summary>
        /// <returns></returns>
        public virtual bool UpdatePinNMI()
        {
            var pins = MyBaseThing.GetAllPins();
            foreach (var pin in pins)
                pin.UpdatePinFlow("", false);
            return false;
        }

    }

    internal class cdePjson
    {
        public Guid cdeMID { get; set; }
        public Guid cdeO { get; set; }
        public Guid cdeN { get; set; }

        public DateTimeOffset cdeCTIM { get; set; }
        public string Name { get; set; }
        public object Value { get; set; }

        public string cdeM { get; set; }
        public int? cdeA { get; set; }
        public int? cdeT { get; set; }
        public int? cdeE { get; set; }
        public int? cdeF { get; set; }
        public long? cdeEXP { get; set; }
        public int? cdeAVA { get; set; }
        public int? cdePRI { get; set; }

        public cdeConcurrentDictionary<string, cdePjson> cdePB { get; set; }

        internal static cdePjson CloneTo(cdeP inP)
        {
            cdePjson tP = new ()
            {
                cdeMID = inP.cdeMID,
                cdeO = inP.cdeO,
                cdeN = inP.cdeN,
                Name = inP.Name,
                Value = inP.Value,
                cdeCTIM = inP.cdeCTIM
            };
            if (!string.IsNullOrEmpty(inP.cdeM)) tP.cdeM = inP.cdeM;
            if (inP.cdeA > 0) tP.cdeA = inP.cdeA;
            if (inP.cdeT > 0) tP.cdeT = inP.cdeT;
            if (inP.cdeE > 0) tP.cdeE = inP.cdeE;
            if (inP.cdeF > 0) tP.cdeF = inP.cdeF;
            if (inP.cdeEXP > 0) tP.cdeEXP = inP.cdeEXP;
            if (inP.cdeAVA > 0) tP.cdeAVA = inP.cdeAVA;
            if (inP.cdePRI > 0) tP.cdePRI = inP.cdePRI;
            if (inP.cdePB != null && inP.cdePB.Count > 0)
            {
                tP.cdePB = new cdeConcurrentDictionary<string, cdePjson>();
                foreach (var t in inP.cdePB.Keys)
                {
                    if ((inP.cdePB[t].cdeE & 0x10) == 0)
                        tP.cdePB.TryAdd(t, cdePjson.CloneTo(inP.cdePB[t]));
                }
            }
            return tP;
        }
    }

    internal class ThingPropertyBagConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize(reader, objectType);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var propBagToWrite = new Dictionary<string, cdePjson>();
            foreach (var prop in (value as cdeConcurrentDictionary<string, cdeP>).GetDynamicEnumerable())
            {
                if ((prop.Value.cdeE & 0x10) == 0)
                {
                    propBagToWrite.Add(prop.Key, cdePjson.CloneTo(prop.Value));
                }
            }
            serializer.Serialize(writer, propBagToWrite);
        }
    }

    /// <summary>
    /// Specifies the algorithm for Throttling of SETP calls
    /// </summary>
    public enum eThrottleKind 
    {
        /// <summary>
        /// Only the last property change will be used
        /// </summary>
        Last = 0,

        /// <summary>
        /// Only the first change will be used
        /// </summary>
        First = 1,

        /// <summary>
        /// An average will be calculated over all changes will be used
        /// </summary>
        Average = 2,

        /// <summary>
        /// Only the minimum value will be used
        /// </summary>
        MinValue = 3,

        /// <summary>
        /// Only the maximum value wil be used
        /// </summary>
        MaxValue = 4
    }


    /// <summary>
    /// Definition of the built-in Thing Events
    /// </summary>
    public static class eThingEvents
    {
        /// <summary>
        /// Fired when a Pin was updated
        /// </summary>
        public const string PinUpdated = "PinUpdated";
        /// <summary>
        /// Fired when a Pin Value was updated
        /// </summary>
        public const string PinValueUpdated = "PinValueUpdated";
        /// <summary>
        /// Fired when a property changes by a user entry in the NMI
        /// </summary>
        public const string PropertyChangedByUX = "PropertyChangedByUX";
        /// <summary>
        /// Fired when a property changes
        /// </summary>
        public const string PropertyChanged = "PropertyChanged";
        /// <summary>
        /// Fires when a property is set (even if it has not changed)
        /// </summary>
        public const string PropertySet = "PropertySet";
        /// <summary>
        /// Fired with the property "Value" has changed
        /// </summary>
        public const string ValueChanged = "ValueChanged";
        /// <summary>
        /// Fired when a property type (cdeT) has changed
        /// </summary>
        public const string PropertyTypeChanged = "PropertyTypeChanged";
        /// <summary>
        /// Fired when the Thing is initialized and ready
        /// </summary>
        public const string Initialized = "Initialized";
        /// <summary>
        /// Fired when the Thing was updated
        /// </summary>
        public const string ThingUpdated = "ThingUpdated";
        /// <summary>
        /// Not Fired on TheThing, use eEngineEvents.ThingRegistered on IBaseEngine. [ Fired when a new thing was registered ]
        /// </summary>
        public const string ThingRegistered = "ThingRegistered";
        /// <summary>
        /// Fired when a property was deleted
        /// </summary>
        public const string PropertyDeleted = "PropertyDeleted";
        /// <summary>
        /// Fired when a property was added
        /// </summary>
        public const string PropertyAdded = "PropertyAdded";
        /// <summary>
        /// Fired when a live object was added or removed from the thing via TheThing.SetIThingObject.
        /// </summary>
        public const string ThingLive = "ThingLive";
        /// <summary>
        /// Fired when a message for the thing was received.
        /// </summary>
        public const string IncomingMessage = "ThingIncomingMessage";
        /// <summary>
        /// Fired when a thing was deleted
        /// </summary>
        public const string ThingDeleted = "ThingDeleted";
    }

    /// <summary>
    /// NMI UX Events fired on TheFieldInfo objects
    /// </summary>
    public static class eUXEvents
    {
        /// <summary>
        /// Fired when a UX property was changed
        /// </summary>
        public const string OnPropertyChanged = "OnPropertyChanged";
        /// <summary>
        /// Fired when a UX property was set even if it has not changed
        /// </summary>
        public const string OnPropertySet = "OnPropertySet";
        /// <summary>
        /// Fired when the iValue (internal Value) property has changed
        /// </summary>
        public const string OniValueChanged = "OniValueChanged";
        /// <summary>
        /// Fired if any other thing event happened
        /// </summary>
        public const string OnThingEvent = "OnThingEvent";
        /// <summary>
        /// Fired on Button elements once the user clicks/taps on it
        /// </summary>
        public const string OnClick = "OnClick";

        /// <summary>
        /// Fires if an NMI Element has loaded. Not all NMI Element fire this event, yet. TheFormInfo is the main element to fire this event
        /// </summary>
        public const string OnLoad = "OnLoaded";

        /// <summary>
        /// Fires when the UX of a Thing has been created
        /// </summary>
        public const string OnUXCreated = "OnUXCreated";

        /// <summary>
        /// Fires when a Screen is visible in the NMI
        /// </summary>
        public const string OnShow = "OnShow";

        /// <summary>
        /// Fires before Screen Meta is sent to a Browser
        /// </summary>
        public const string OnBeforeMeta = "OnBeforeMeta";
        /// <summary>
        /// Fires before the data of a form is Retreived. Can be used to set new values before a form is displayed to a user
        /// </summary>
        public const string OnBeforeLoad = "OnBeforeLoad";

        /// <summary>
        /// Event fired when a user right clicks an NMI control in Edit Mode
        /// </summary>
        public const string OnShowEditor = "OnShowEditor";
        /// <summary>
        /// Event fired when a user closes the NMI Editor
        /// </summary>
        public const string OnHideEditor = "OnHideEditor";
    }

    /// <summary>
    /// List of internally known Things
    /// </summary>
    public static class eKnownDeviceTypes
    {
        /// <summary>
        /// The base type for all Plugins
        /// </summary>
        public const string IBaseEngine = "IBaseEngine";
        /// <summary>
        /// The base type for Rules in the Rules Engine
        /// </summary>
        public const string TheThingRule = "TheThingRule";
        /// <summary>
        /// The type of a Screen in the NMI
        /// </summary>
        public const string TheNMIScreen = "cdeNMIScreen";
        /// <summary>
        /// The type of a Scene in the NMI
        /// </summary>
        public const string TheNMIScene = "NMIScene";
        /// <summary>
        /// The base type for the Application Host (Node)
        /// </summary>
        public const string ApplicationHost = "ApplicationHost";
        /// <summary>
        /// The base type for the Rules Engine
        /// </summary>
        public const string TheRulesEngine = "TheRulesEngine";
    }


    /// <summary>
    /// TheThing is the main control object of the C-DEngine. It is a virtual representation of physical objects, functions, services or storage locations
    /// All "Things" in the C-DEngine are managed as TheThings
    /// </summary>
    public sealed partial class TheThing : TheMetaDataBase, ICDEThing, ICDEProperty
    {

        internal IHistorian Historian { get; set; }
        internal IBaseEngine MyBaseEngine = null; //Cached BaseEngine owned by TheThing
        /// <summary>
        /// Returns the BaseThing of a Thing. TheBaseThing is a serializable representation of a Thing.
        /// the ICDEThing interface requires the return of TheBaseThing as other classes can be inherited from ICDEThing.
        /// In case of TheThing, this method just returns "this"
        /// </summary>
        /// <returns></returns>
        public TheThing GetBaseThing()
        {
            return this;
        }

        /// <summary>
        /// Gets the BaseEngine of TheThing and caches it in TheThing - this is a performance optimization
        /// Tradeoff: TheThing will hold on to the Engine reference
        /// </summary>
        /// <returns></returns>
        public IBaseEngine GetBaseEngine()
        {
            if (MyBaseEngine != null)
                return MyBaseEngine;
            if (string.IsNullOrEmpty(EngineName))
                return null;
            MyBaseEngine = TheThingRegistry.GetBaseEngine(EngineName);
            return MyBaseEngine;
        }

        /// <summary>
        /// If this thing has a ThingBase, a reference will be stored here during creation of TheThingBase
        /// </summary>
        [IgnoreDataMember]
        public TheThingBase MyThingBase { get; internal set; }

        /// <summary>
        /// Sets the BaseThing - only if the given pThing does not match "this"
        /// </summary>
        /// <param name="pThing"></param>
        public void SetBaseThing(TheThing pThing)
        {
            if (ThingObject is ICDEThing tThing && tThing != this)
                tThing.SetBaseThing(pThing);
        }

        /// <summary>
        /// All messages sent to TheThing will routed through this message handler
        /// The handle Message of TheBaseThing will be excuted SYNCHRON
        /// </summary>
        /// <param name="sender">Sender of the message handle request</param>
        /// <param name="pMsg">The detailed incoming message</param>
        public void HandleMessage(ICDEThing sender, object pMsg)
        {
            if (ThingObject is ICDEThing tThing && tThing != this)
            {
                try
                {
                    FireEvent(eThingEvents.IncomingMessage, this, pMsg, true);
                    tThing.HandleMessage(sender, pMsg);
                    MyBaseEngine?.FireEvent(eEngineEvents.IncomingEngineMessage, pMsg, true);
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(7687, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("ThingService", "Failed to Handle Message of Thing", eMsgLevel.l1_Error, e.ToString()));
                }
            }
        }

        /// <summary>
        /// This method is called by the NMI Service of the C-DEngine to initialize the NMI UX
        /// </summary>
        public bool CreateUX()
        {
            bool result = false;
            if (ThingObject is ICDEThing tThing && tThing != this)
            {

                bool bLocked = false;
                try
                {
                    if (TheThing.GetSafePropertyBool(this, TheThingRegistry.eOwnerProperties.cdeHidden) || IsDisabled || IsHidden)
                    {
                        return false;
                    }
                    lock (initOrUxLock)
                    {
                        if (createUxLockTime != DateTimeOffset.MinValue || tThing.IsUXInit())
                        {
                            return false;
                        }
                        createUxLockTime = DateTimeOffset.Now;
                        bLocked = true;
                    }
                    if (tThing.CreateUX())
                    {
                        createUxLockTime = DateTimeOffset.MinValue;
                        result = true;
                    }
                    else
                    {
                        // CreateUx is asynchronous: V4TODO fire off a timer to catch bad plug-ins?
                    }
                    FireEvent(eUXEvents.OnUXCreated, this, null, false);
                }
                catch (Exception e)
                {
                    if (bLocked)
                    {
                        createUxLockTime = DateTimeOffset.MinValue;
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(7688, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("ThingService", "Failed to Create UX of Thing", eMsgLevel.l1_Error, e.ToString())); //Log Entry that service has been started
                }
            }
            return result;
        }

        /// <summary>
        /// Must return true if the UX was already initialized
        /// </summary>
        /// <returns></returns>
        public bool IsUXInit()
        {
            if (ThingObject is ICDEThing tThing && tThing != this)
                return tThing.IsUXInit();
            return false;
        }

        readonly object initOrUxLock = new ();
        DateTimeOffset initLockTime = DateTimeOffset.MinValue;
        DateTimeOffset createUxLockTime = DateTimeOffset.MinValue;
        /// <summary>
        /// This method is called by TheThingService of the C-DEngine when the Thing is loaded or registered in TheThingRegistry
        /// </summary>
        public bool Init()
        {
            bool result = false;
            if (ThingObject is ICDEThing tThing && tThing != this)
            {
                bool bLocked = false;
                try
                {
                    lock (initOrUxLock)
                    {
                        if (initLockTime != DateTimeOffset.MinValue || tThing.IsInit() || IsDisabled)
                        {
                            // CODE REVIEW: Should we consider the lock expired after certain time (10 minutes? 1 hour?)
                            return false;
                        }
                        initLockTime = DateTimeOffset.Now;
                        bLocked = true;
                    }
                    tThing.RegisterEvent(eThingEvents.Initialized, sinkThingInitialized);
                    if (tThing.Init())
                    {
                        tThing.UnregisterEvent(eThingEvents.Initialized, sinkThingInitialized);
                        initLockTime = DateTimeOffset.MinValue;
                        result = true;

                        bool bIsInit = tThing.IsInit();
                        tThing.FireEvent(eThingEvents.Initialized, tThing, bIsInit, true);
                        if (!bIsInit)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(7689, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("ThingService", "Failed to Init Thing", eMsgLevel.l1_Error, $"{this.FriendlyName} / {this.cdeMID}: Thing not initialized after Init() call.")); //Log Entry that service has been started
                        }
                    }
                    else
                    {
                        // init is asynchronous: Event will be fired (or was already fired) by the thing itself
                        // V4TODO fire off a timer to catch bad plug-ins?
                    }
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(7690, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("ThingService", "Failed to Init Thing", eMsgLevel.l1_Error, $"{this.FriendlyName} / {this.cdeMID}: {e}")); //Log Entry that service has been started
                    if (bLocked)
                    {
                        initLockTime = DateTimeOffset.MinValue;
                    }
                }
            }
            return result;
        }

        void sinkThingInitialized(ICDEThing thing, object param)
        {
            initLockTime = DateTimeOffset.MinValue;
            thing.UnregisterEvent(eThingEvents.Initialized, sinkThingInitialized);
            if (!thing.IsInit())
            {
                TheBaseAssets.MySYSLOG.WriteToLog(7691, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("ThingService", "Failed to Init Thing", eMsgLevel.l1_Error, "Thing not initialized after eThingEvent.Initialized event.")); //Log Entry that service has been started
            }

        }

        /// <summary>
        /// Deletes this thing and all its resources
        /// </summary>
        /// <returns></returns>
        public bool Delete()
        {
            if (ThingObject is ICDEThing tThing && tThing != this)
            {
                var deleteAction = new Action<ICDEThing, object>((t, p) =>
                {
                    this.ClearAllEvents();
                });

                Historian?.DeleteAsync().Wait();
                foreach (var thingWithHistory in EnumerateThingsWithHistory())
                {
                    thingWithHistory.Historian?.UnregisterAllConsumersForOwnerAsync(this).Wait();
                }
                RemoveAllPinConnections();
                tThing.RegisterEvent(eThingEvents.ThingDeleted, deleteAction);

                if (tThing.Delete())
                {
                    tThing.FireEvent(eThingEvents.ThingDeleted, tThing, true, true);
                    return true;
                }
                return false;
            }
            return false;
        }

        /// <summary>
        /// Must return true if TheThing was already initialized
        /// </summary>
        /// <returns></returns>
        public bool IsInit()
        {
            if (ThingObject is ICDEThing tThing && tThing != this)
                return tThing.IsInit();
            return false;
        }

        /// <summary>
        /// A property representation of IsUXInit() in order for easy serialization and UX display
        /// </summary>
        public bool IsUXInitialized
        {
            get { return IsUXInit(); }
        }

        /// <summary>
        /// A property representation of IsInit() in order for easy serialization and UX display
        /// </summary>
        public bool IsInitialized
        {
            get { return IsInit(); }
        }

        /// <summary>
        /// Indicates if Init() has been called, but not completed.
        /// </summary>
        public bool IsInitializing
        {
            get
            {
                return InitializePendingStartTime != DateTimeOffset.MinValue;
            }
        }

        /// <summary>
        /// Indicates since when an Init() called has been started but not completed.
        /// Returns DateTimeOffset.MinValue if Init() has not been called or has completed.
        /// </summary>
        public DateTimeOffset InitializePendingStartTime
        {
            get
            {
                lock (initOrUxLock)
                {
                    return initLockTime;
                }
            }
        }

        #region Custom Object Handling
        private object ThingObject;

        /// <summary>
        /// This methods provides thread-safe access to the IThingObject.
        /// </summary>
        /// <typeparam name="T">Type of the desired IThingObject. If the existing object is of a different type, it will be updated to a new instance of this type.</typeparam>
        /// <param name="createThingObjectAction">Function that returns the desired IThingObject. Typically callers invoke the constructor of the class in this function. This function is run under a lock, so no long running operations should be performed here.</param>
        /// <param name="bCreated">Indicates if a new IThingObject was created or if an existing one was returned. Useful for performing one-time initializations on the IThingObject.</param>
        /// <returns>An existing or newly created IThingObject of type T, or null if another caller </returns>
        public T GetOrCreateIThingObject<T>(Func<TheThing, T> createThingObjectAction, out bool bCreated) where T : class
        {
            T thingObject = null;
            bCreated = false;
            lock (initOrUxLock)
            {
                if (ThingObject is T tob)
                {
                    return tob;
                }
                if (!HasLiveObject)
                {
                    try
                    {
                        thingObject = createThingObjectAction(this);
                        SetIThingObject(thingObject);
                        bCreated = true;
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7691, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("ThingService", "Failed to created IThingObject", eMsgLevel.l2_Warning, $"{this.EngineName} {this.FriendlyName}: callback exception '{e.Message}'."));
                    }
                }
            }
            if (thingObject != null)
            {
                TheThingRegistry.RegisterThing(this);
            }
            return thingObject;
        }

        /// <summary>
        /// This method sets an internal "object" in TheThing. The object is NOT serialized in TheThingRegistry but can be used to store any arbitrary class/object associated with TheThing
        /// </summary>
        /// <param name="pObj"></param>
        public void SetIThingObject(object pObj)
        {
            lock (initOrUxLock)
            {
                if (pObj == null && ThingObject != null)
                {
                    FireEvent(eThingEvents.ThingLive, this, false, true);
                }
                if (ThingObject == null && pObj != null)
                {
                    FireEvent(eThingEvents.ThingLive, this, true, true);
                }
                if (ThingObject != null && pObj != ThingObject)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(7691, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("ThingService", "Switching IThingObject", eMsgLevel.l2_Warning, $"{this.EngineName} {this.FriendlyName}: IThingObject replaced with a different one. Race condition in plug-in instance creation?"));
                }
                ThingObject = pObj;
            }
        }

        /// <summary>
        /// Return the internal "object" associated with this TheThing
        /// </summary>
        /// <returns></returns>
        public object GetObject()
        {
            return ThingObject;
        }


        /// <summary>
        /// A property representation of IsAlive() in order for easy serialization and UX display
        /// </summary>
        public bool HasLiveObject
        {
            get { return ThingObject != null; }// IsAlive(); } //New in 3.200 - allows to use IsAlive for other Purposes
        }
        #endregion

        #region Property Management

        /// <summary>
        /// The Main PropertyBag.
        /// ATTENTION: Direct Manipulation of MyPropertyBag will not fire any events and circumvents encryption and other API based management.
        /// Only access this Bag if you know exactly what you are doing.
        /// NOTE: It is possible that we remove access to the Bag in a later version of the C-DEngine - do not rely on its access availability
        /// </summary>
        [JsonConverter(typeof(ThingPropertyBagConverter))]
        public cdeConcurrentDictionary<string, cdeP> MyPropertyBag { get; set; }

        internal Dictionary<string, object> GetPBAsDictionary()
        {
            Dictionary<string, object> t = new ();
            foreach (cdeP p in MyPropertyBag.Values)
            {
                t[p.Name] = p.GetValue();
            }
            return t;
        }

        /// <summary>
        /// Removes a property from TheThing at Runtime
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public cdeP RemoveProperty(string pName)
        {
            cdeP t = GetProperty(pName);
            if (t != null)
            {
                if (t.cdeOP == null)
                    MyPropertyBag.TryRemove(t.Name, out t);
                else
                    t.cdeOP.cdePB?.TryRemove(t.Name, out t);
                TheStorageMirror<TheThing> MyStorageMirror = (TheStorageMirror<TheThing>)TheCDEngines.GetStorageMirror("TheThingRegistry");
                MyStorageMirror?.RemoveEdgeStorageProperty(cdeMID, pName, null);
                t.ClearAllEvents();
                FireEvent(eThingEvents.PropertyDeleted, this, t, true);
            }
            return t;
        }

        /// <summary>
        /// Removes all properties with a given prefix from TheThing at Runtime
        /// Only properties directly attached to a thing will be deleted.
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public bool RemovePropertiesStartingWith(string pName)
        {
            bool retVal = false;
            List<string> Keys = new ();
            lock (MyPropertyBag.MyLock)
            {
                Keys.AddRange(from string key in MyPropertyBag.Keys
                              where key.StartsWith(pName)
                              select key);
                foreach (string key in Keys)
                {
                    RemoveProperty(key);
                    retVal = true;
                }
            }
            return retVal;
        }

        /// <summary>
        /// Returns a count of all current Properties
        /// Only properties on TheThing will be counted.
        /// </summary>
        [IgnoreDataMember]
        public int PropertyCount
        {
            get { return MyPropertyBag.Count; }
        }

        #region Property Change Throtteling
        private int PublishThrottle = 0;
        private eThrottleKind PublishThrottleKind = eThrottleKind.Last;
        private DateTimeOffset PubLastSend = DateTimeOffset.MinValue;
        private cdeConcurrentDictionary<string, cdeP> PubList = null;

        /// <summary>
        /// sets a throttle time in milliseconds for all properties of TheThing. Changes to properties will only be sent at a maximum sample rate given in pThrottleInMs
        /// TheThing will consolidate all changes in one SETP call.
        /// If a property changes multiple times only the last value will be used.2
        /// </summary>
        /// <param name="pThrottleInMs">Snapshot Throttle time in MS</param>
        /// <param name="pKind">Algorithm to be used for the throttle (in V3.1 only LAST is supported)</param>
        /// <returns></returns>
        public int SetPublishThrottle(int pThrottleInMs, eThrottleKind pKind = eThrottleKind.Last)
        {
            if (pThrottleInMs >= 0)
            {
                PublishThrottle = pThrottleInMs;
                PublishThrottleKind = pKind;
                PubList ??= new cdeConcurrentDictionary<string, cdeP>();
            }
            else
            {
                PublishThrottle = 0;
                DoThrottlePub(null);
                PubList = null;
            }
            return PublishThrottle;
        }

        /// <summary>
        /// Forces to Flush all Property Changes currently in the Throttle Queue
        /// </summary>
        public void FlushThrottle()
        {
            DoThrottlePub(null);
        }

        internal void ThrottledSETP(cdeP pProperty, Guid pOriginator, bool ForcePublish)
        {
            if (pProperty == null || TheBaseAssets.MyServiceHostInfo.DisableNMIMessages || TheBaseAssets.MyServiceHostInfo.DisableNMI || ((pProperty.cdeFOC & 1) == 0 && !ForcePublish))
                return;
            if (PublishThrottle <= 0 || (pProperty.cdeE & 2) != 0 || pProperty.cdeO != cdeMID)    
            {
                pProperty.PublishChange(CU.cdeGuidToString(pOriginator), Guid.Empty, null, eEngineName.NMIService, ForcePublish);
                return;
            }
            switch (PublishThrottleKind)
            {
                case eThrottleKind.First:
                    if (!PubList.ContainsKey(cdeP.GetPropertyPath(pProperty)))
                    {
                        PubList[cdeP.GetPropertyPath(pProperty)] = new cdeP(pProperty);
                    }
                    break;
                case eThrottleKind.MaxValue:
                    if (!PubList.ContainsKey(cdeP.GetPropertyPath(pProperty)) || CU.CDbl(PubList[cdeP.GetPropertyPath(pProperty)].ToString()) < CU.CDbl(pProperty.ToString()))
                    {
                        PubList[cdeP.GetPropertyPath(pProperty)] = new cdeP(pProperty);
                    }
                    break;
                case eThrottleKind.MinValue:
                    if (!PubList.ContainsKey(cdeP.GetPropertyPath(pProperty)) || CU.CDbl(PubList[cdeP.GetPropertyPath(pProperty)].ToString()) > CU.CDbl(pProperty.ToString()))
                    {
                        PubList[cdeP.GetPropertyPath(pProperty)] = new cdeP(pProperty);
                    }
                    break;
                default:
                    PubList[cdeP.GetPropertyPath(pProperty)] = pProperty;
                    break;
            }
            if (DateTimeOffset.Now.Subtract(PubLastSend).TotalMilliseconds > PublishThrottle)
            {
                PubLastSend = DateTimeOffset.Now;
                DoThrottlePub(null);
                if (ThrottleTimer != null)
                    ThrottleTimer.Change(PublishThrottle, Timeout.Infinite);
            }
            else
            {
                ThrottleTimer ??= new Timer(DoThrottlePub, null, PublishThrottle, Timeout.Infinite);
            }
        }

        private Timer ThrottleTimer = null;

        private void DoThrottlePub(object pState)
        {
            if (PubList == null || PubList.Count == 0) return;
            TSM tFireTSM = new (eEngineName.NMIService, null) { LVL = eMsgLevel.l3_ImportantMessage, OWN = cdeMID.ToString() }; //Must set ORG and SID
            if (cdeN != Guid.Empty)
                tFireTSM.SetOriginator(cdeN);
            tFireTSM.AddHopToOrigin();

            lock (PubList.MyLock)
            {
                var tpls = new StringBuilder();
                foreach (cdeP tP in PubList.Values)
                {
                    tFireTSM.TXT = $"SETP:{cdeO}";   //ThingProperties
                    if (!string.IsNullOrEmpty(tFireTSM.PLS))
                        tpls.Append(":;:");
                    var tName = tP.Name;
                    if (tP.cdeOP != null)
                        tName = cdeP.GetPropertyPath(tP);
                    tpls.Append($"{tName}={tP}");
                }
                tFireTSM.PLS = tpls.ToString();
                PubList.Clear();
            }
            if ((cdeF & 1) != 0)
                tFireTSM.ENG = $"NMI{tFireTSM.OWN}";
            TheBaseAssets.MySYSLOG.WriteToLog(7678, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(eEngineName.ThingService, $"Do ThrottlePub of {cdeMID} {tFireTSM.PLS}", eMsgLevel.l3_ImportantMessage)); 
            TheCommCore.PublishCentral(tFireTSM);
            TheCDEKPIs.IncrementKPI(eKPINames.SetPsFired);
        }
        #endregion

        /// <summary>
        /// returns a list of all properties of TheThing
        /// Only top-level properties on TheThing will be returned. To return properties of properties, use the GetAllProperties(maxSubPropertylevel) override.
        /// </summary>
        /// <returns></returns>
        public List<cdeP> GetAllProperties()
        {
            return MyPropertyBag.Values.ToList();
        }

        /// <summary>
        /// returns a list of all properties of TheThing and their sub-properties, up to the specified nesting level
        /// </summary>
        /// <param name="maxSubPropertyLevel">Indicates the maximum nesting level of sub-properties. 0 = only top-level properties, 1 = only top-level properties and their immediate sub-properties etc.</param>
        /// <returns></returns>
        public List<cdeP> GetAllProperties(int maxSubPropertyLevel)
        {
            var tProps = MyPropertyBag.Values.ToList();
            var props = new List<cdeP>(tProps);
            if (maxSubPropertyLevel > 0)
            {
                foreach (var prop in tProps)
                {
                    var tl = prop.GetAllProperties(maxSubPropertyLevel - 1);
                    if (tl != null)
                        props.AddRange(tl);
                }
            }
            return props;
        }

        /// <summary>
        /// Returns a list of Properties starting with pName in the Name
        /// Only properties directly attached to TheThing will be returned
        /// </summary>
        /// <param name="pName">Start String of pName. case is ignored</param>
        /// <returns>List of properties matching the condition</returns>
        public List<cdeP> GetPropertiesStartingWith(string pName)
        {
            List<cdeP> Keys = new ();
            lock (MyPropertyBag.MyLock)
            {
                Keys.AddRange(from string key in MyPropertyBag.Keys
                              where key.StartsWith(pName, StringComparison.OrdinalIgnoreCase)
                              select MyPropertyBag[key]);
            }
            return Keys;
        }

        /// <summary>
        /// Returns the names of properties in this thing that match the provided inclusion and exclusion lists.
        /// </summary>
        /// <param name="propertiesToInclude">List of property names to include. If a name starts with "!" it is matched using a regular expression. If the parameter is null all properties are included.</param>
        /// <param name="propertiesToExclude">List of property names to exclude. If a name start with "!" it is matched using a regular experssion. If the parameters is null no property is excluded.</param>
        /// <returns></returns>
        public IEnumerable<string> GetMatchingProperties(IEnumerable<string> propertiesToInclude, IEnumerable<string> propertiesToExclude)
        {
            var filter = new ThePropertyFilter { Properties = propertiesToInclude.ToList(), PropertiesToExclude = propertiesToExclude.ToList() };
            return GetMatchingProperties(filter);
        }
        /// <summary>
        /// Returns the names of properties in this thing that match the property filter.
        /// </summary>
        /// <param name="propertyFilter">Indicate which properties of a thing should be returned.</param>
        /// <returns></returns>
        public IEnumerable<string> GetMatchingProperties(ThePropertyFilter propertyFilter)
        {
            IEnumerable<string> propertiesToInclude;
            if (propertyFilter?.Properties == null)
            {
                propertiesToInclude = this.GetAllProperties(10)
                    .Where(p => (propertyFilter.FilterToConfigProperties != true && propertyFilter.FilterToSensorProperties != true)
                        || (propertyFilter.FilterToSensorProperties == true && p.IsSensor)
                        || (propertyFilter.FilterToConfigProperties == true && p.IsConfig))
                    .Select(p => cdeP.GetPropertyPath(p)).Where(propertyNamePath => !cdeP.IsMetaProperty(propertyNamePath)
                );
            }
            else
            {
                propertiesToInclude = propertyFilter.Properties;
                IEnumerable<string> allProps = null;
                List<string> propsToAdd = null;
                foreach (var prop in propertiesToInclude.Where(prop => prop.StartsWith("!")))
                {
                    allProps ??= GetAllProperties(10).Select(p => cdeP.GetPropertyPath(p));
                    var regex = new System.Text.RegularExpressions.Regex(prop.Substring(1));
                    foreach (var newProp in allProps)
                    {
                        if (regex.IsMatch(newProp) && !propertiesToInclude.Contains(newProp))
                        {
                            if (propsToAdd == null)
                            {
                                propsToAdd = new List<string> { newProp };
                            }
                            else
                            {
                                propsToAdd.Add(newProp);
                            }
                        }
                    }
                }

                if (propsToAdd != null)
                {
                    propertiesToInclude = propertiesToInclude.Where(p => !p.StartsWith("!")).Union(propsToAdd);
                }
            }
            if (propertyFilter?.PropertiesToExclude?.Any() == true)
            {
                var tempList = new List<string>(propertiesToInclude);
                foreach (var prop in propertyFilter.PropertiesToExclude)
                {
                    if (!prop.StartsWith("!"))
                    {
                        tempList.Remove(prop);
                    }
                    else
                    {
                        var regex = new System.Text.RegularExpressions.Regex(prop.Substring(1));
                        tempList.RemoveAll(p => regex.IsMatch(p));
                    }
                }
                propertiesToInclude = tempList;
            }

            return propertiesToInclude;
        }

        /// <summary>
        /// Returns all properties where the cdeM Meta Field starts with pName
        /// Only properties directly attached to TheThing will be returned
        /// </summary>
        /// <param name="pName">Start Sequence of the MetaData. case is ignored</param>
        /// <returns>List of properties matching the condition</returns>
        public List<cdeP> GetPropertiesMetaStartingWith(string pName)
        {
            List<cdeP> Keys = new ();
            lock (MyPropertyBag.MyLock)
            {
                foreach (cdeP key in MyPropertyBag.Values)
                {
                    if (!string.IsNullOrEmpty(key.cdeM) && key.cdeM.StartsWith(pName, StringComparison.OrdinalIgnoreCase))
                        Keys.Add(key);
                }
            }
            return Keys;
        }

        /// <summary>
        /// Returns all properties starting with a specific prefix
        /// </summary>
        /// <param name="pName">prefix to look for</param>
        /// <param name="includeSubProperties">include subproperties if true</param>
        /// <returns></returns>
        public List<cdeP> GetPropertiesMetaStartingWith(string pName, bool includeSubProperties)
        {
            List<cdeP> Keys = new ();
            lock (MyPropertyBag.MyLock)
            {
                foreach (cdeP key in GetAllProperties(includeSubProperties ? 10 : 0))
                {
                    if (!string.IsNullOrEmpty(key.cdeM) && key.cdeM.StartsWith(pName, StringComparison.OrdinalIgnoreCase))
                        Keys.Add(key);
                }
            }
            return Keys;
        }

        /// <summary>
        /// Returns true if TheThing is managed on the current node
        /// </summary>
        /// <returns></returns>
        public bool IsOnLocalNode()
        {
            return cdeN == TheBaseAssets.MyServiceHostInfo?.MyDeviceInfo?.DeviceID;
        }

        /// <summary>
        /// Returns true if originating node is the node that owns the Thing
        /// </summary>
        /// <returns></returns>
        public bool IsFromOwningRemoteNode(string originator)
        {
            if (String.IsNullOrEmpty(originator))
            {
                return false;
            }
            return cdeN == TSM.GetOriginator(originator) && !IsOnLocalNode();
        }


        /// <summary>
        /// retrieves all properties that have bit 7 (64) set in cdeF
        /// </summary>
        /// <returns></returns>
        public List<cdeP> GetNMIProperties()
        {
            List<cdeP> Keys = new ();
            lock (MyPropertyBag.MyLock)
            {
                foreach (string key in MyPropertyBag.Keys)
                {
                    if ((MyPropertyBag[key].cdeE & 0x40) != 0)
                        Keys.Add(MyPropertyBag[key]);
                }
            }
            return Keys;
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <returns></returns>
        public cdeP SetSecureProperty(string pName, object pValue)
        {
            DeclareSecureProperty(pName, ePropertyTypes.TString);
            return SetProperty(pName, pValue, ePropertyTypes.NOCHANGE, DateTimeOffset.MinValue, -1, null, null);
        }

        /// <summary>
        /// Registers a new property with TheThing at runtime
        /// </summary>
        /// <param name="pName">Name of the Property to be registered.</param>
        /// <returns></returns>
        public cdeP RegisterProperty(string pName)
        {
            // Until 4.203.1 RegisterProperty would set the property value to null. Most caller calling this in their Init() method don't expect the value to get lost
            return GetProperty(pName, true);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// The Property Change Events and Set Property events are NOT Fired
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <returns></returns>
        public cdeP SetPropertyNoEvents(string pName, object pValue)
        {
            return SetProperty(pName, pValue, ePropertyTypes.NOCHANGE, DateTimeOffset.MinValue, 0x8, null, null);
        }
        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <returns></returns>
        public cdeP SetPropertyForceEvents(string pName, object pValue)
        {
            return SetProperty(pName, pValue, ePropertyTypes.NOCHANGE, DateTimeOffset.MinValue, 0x20, null, null);
        }



        /// <summary>
        /// Sets the type of a property
        /// If the property does not exist, it will be created
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pType">Type of the property</param>
        /// <returns></returns>
        public cdeP SetPropertyTypeOnly(string pName, ePropertyTypes pType)
        {
            return SetProperty(pName, null, pType, DateTimeOffset.MinValue, 0x10, null, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <returns></returns>
        public cdeP SetProperty(string pName, object pValue)
        {
            return SetProperty(pName, pValue, ePropertyTypes.NOCHANGE, DateTimeOffset.MinValue, -1, null, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="pType">The Type of the Property</param>
        /// <returns></returns>
        public cdeP SetProperty(string pName, object pValue, ePropertyTypes pType)
        {
            return SetProperty(pName, pValue, pType, DateTimeOffset.MinValue, -1, null, null);
        }



        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="EventAction">Defines what events should be fired</param>
        /// <param name="pOnChangeEvent">a callback for a local (current node) onChangeEvent</param>
        /// <returns></returns>
        public cdeP SetProperty(string pName, object pValue, int EventAction, Action<cdeP> pOnChangeEvent = null)
        {
            return SetProperty(pName, pValue, ePropertyTypes.NOCHANGE, DateTimeOffset.MinValue, EventAction, pOnChangeEvent, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="pType">The Type of the Property</param>
        /// <param name="EventAction">Defines what events should be fired</param>
        /// <param name="pOnChangeEvent">a callback for a local (current node) onChangeEvent</param>
        /// <returns></returns>
        public cdeP SetProperty(string pName, object pValue, ePropertyTypes pType, int EventAction, Action<cdeP> pOnChangeEvent = null)
        {
            return SetProperty(pName, pValue, pType, DateTimeOffset.MinValue, EventAction, pOnChangeEvent, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="sourceTimeStamp">Overwrite the CTIM (Creation Time Stamp). If set to DateTimeOffset.MinValue the CTIM will be set to the current system time.</param>
        public cdeP SetProperty(string pName, object pValue, DateTimeOffset sourceTimeStamp)
        {
            return SetProperty(pName, pValue, ePropertyTypes.NOCHANGE, sourceTimeStamp, -1, null, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="pType">Type of the property</param>
        /// <param name="sourceTimeStamp">Overwrite the CTIM (Creation Time Stamp). If set to DateTimeOffset.MinValue the CTIM will be set to the current system time.</param>
        public cdeP SetProperty(string pName, object pValue, ePropertyTypes pType, DateTimeOffset sourceTimeStamp)
        {
            return SetProperty(pName, pValue, pType, sourceTimeStamp, -1, null, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="pType">The Type of the Property</param>
        /// <param name="sourceTimeStamp">Overwrite the CTIM (Creation Time Stamp). If set to DateTimeOffset.MinValue the CTIM will be set to the current system time.</param>
        /// <param name="EventAction">Defines what events should be fired</param>
        /// <param name="pOnChangeEvent">a callback for a local (current node) onChangeEvent</param>
        /// <returns></returns>
        public cdeP SetProperty(string pName, object pValue, ePropertyTypes pType, DateTimeOffset sourceTimeStamp, int EventAction, Action<cdeP> pOnChangeEvent)
        {
            return SetProperty(pName, pValue, pType, sourceTimeStamp, EventAction, pOnChangeEvent, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="pType">The Type of the Property</param>
        /// <param name="sourceTimeStamp">Overwrite the CTIM (Creation Time Stamp). If set to DateTimeOffset.MinValue the CTIM will be set to the current system time.</param>
        /// <param name="EventAction">Defines what events should be fired</param>
        /// <param name="pOnChangeEvent">a callback for a local (current node) onChangeEvent</param>
        /// <param name="originator">Originating node of the change when processing changes from a Remote Thing: used to prevent re-publishing of changes and ensure local processing</param>
        /// <returns></returns>
        internal cdeP SetProperty(string pName, object pValue, ePropertyTypes pType, DateTimeOffset sourceTimeStamp, int EventAction, Action<cdeP> pOnChangeEvent, string originator)
        {
            return SetPropertyInternal(pName, pValue, pType, sourceTimeStamp, EventAction, pOnChangeEvent, originator);
        }
        internal cdeP SetPropertyInternal(string pName, object pValue, ePropertyTypes pType, DateTimeOffset sourceTimeStamp, int EventAction, Action<cdeP> pOnChangeEvent, string originator)
        {
            if (string.IsNullOrEmpty(pName)) return null;

            object tInternalValue = pValue;

            bool WasOk = true;
            if ("iValue".Equals(pName))
            {
                pName = "Value";
                if (EventAction < 0)
                    EventAction = 8;
                else
                    EventAction |= 8;
            }
            cdeP tProp = GetPropertyInternal(pName, true, ref WasOk);

            // Code Review: Why is at least some of this logic not in cdeP.SetValue? This creates more entry points with subtly different semantics
            tProp.RemoveTempFlags();

            if (pType != ePropertyTypes.NOCHANGE)
                tProp.cdeT = (int)pType;
            if (EventAction >= 0)
                tProp.SetEventAction(EventAction);
            if (pOnChangeEvent != null)
            {
                tProp.UnregisterEvent(eThingEvents.PropertyChanged, pOnChangeEvent);
                tProp.RegisterEvent(eThingEvents.PropertyChanged, pOnChangeEvent);
            }
            if (EventAction < 0 || (EventAction & 0x10) == 0)
            {
                tProp.SetValue(tInternalValue, originator, sourceTimeStamp);
            }
            UpdatePropertyInBag(tProp, WasOk);

            return tProp;
        }

        internal void UpdatePropertyInBag(cdeP pProp, bool NoFireAdd, bool MustUpdate = false)
        {
            if (pProp == null) return;
            cdeConcurrentDictionary<string, cdeP> tPB;
            if (pProp.cdeOP == null)
                tPB = MyPropertyBag;
            else
            {
                pProp.cdeOP.cdePB ??= new cdeConcurrentDictionary<string, cdeP>();
                tPB = pProp.cdeOP.cdePB;
            }
            if (MustUpdate && tPB.ContainsKey(pProp.Name) && tPB[pProp.Name].cdeMID != pProp.cdeMID)
                    return;
            cdeP tOldProp = null;
            if (tPB.ContainsKey(pProp.Name))
                tOldProp = tPB[pProp.Name];
            if (NoFireAdd)
                tPB[pProp.Name] = pProp;
            else
            {
                tPB.TryAdd(pProp.Name, pProp);
                FireEvent(eThingEvents.PropertyAdded, this, pProp, true);
            }
            if (tOldProp != null && tOldProp != pProp && tOldProp.HasRegisteredEvents())
            {
                tOldProp.MoveEventsTo(pProp);
                if (tOldProp.Value != pProp.Value)
                    pProp.FireEvent(eThingEvents.PropertyChanged, true);
                pProp.FireEvent(eThingEvents.PropertySet, true);
            }
        }

        /// <summary>
        /// Sets multiple properties at once. Use with the Historian feature for a consistent snapshot that has all these property changes.
        /// If any of the properties do not exist, they will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="nameValueDictionary"></param>
        /// <param name="sourceTimestamp"></param>
        /// <returns></returns>
        public bool SetProperties(IDictionary<string, object> nameValueDictionary, DateTimeOffset sourceTimestamp)
        {
            if (nameValueDictionary == null) return false;
            foreach (var nv in nameValueDictionary)
            {
                SetPropertyInternal(nv.Key, nv.Value, ePropertyTypes.NOCHANGE, sourceTimestamp, 0, null, null);
            }
            return true;
        }

        /// <summary>
        /// Registers a new OnPropertyChange Event Callback
        /// </summary>
        /// <param name="OnUpdateName">Name of the property to monitor</param>
        /// <param name="oOnChangeCallback">Callback to be called when the property changes</param>
        /// <returns></returns>
        public bool RegisterOnChange(string OnUpdateName, Action<cdeP> oOnChangeCallback)
        {
            if (oOnChangeCallback == null || string.IsNullOrEmpty(OnUpdateName)) return false;

            cdeP tProp = GetProperty(OnUpdateName, true);
            if (tProp != null)
            {
                tProp.SetEventAction(4);
                tProp.UnregisterEvent(eThingEvents.PropertyChanged, oOnChangeCallback);
                tProp.RegisterEvent(eThingEvents.PropertyChanged, oOnChangeCallback);
            }
            else
                SetProperty(OnUpdateName, null, 4, oOnChangeCallback);
            return true;
        }

        private cdeP GetPropertyRoot(string pName, bool DoCreate, ref bool NoFireAdded)
        {
            if (string.IsNullOrEmpty(pName)) return null;
            if (!MyPropertyBag.TryGetValue(pName, out cdeP tProp))
            {
                if (DoCreate)
                {
#pragma warning disable S6612 // The lambda parameter should be used instead of capturing arguments in "ConcurrentDictionary" methods
                    tProp = MyPropertyBag.GetOrAdd(pName, name =>
                    {
                        return new cdeP(this) { Name = pName, cdeO = cdeMID };
                    });
#pragma warning restore S6612 // The lambda parameter should be used instead of capturing arguments in "ConcurrentDictionary" methods
                    OPCUATypeAttribute.ApplyUAPropertyAttributes(this, tProp);
                    if (!NoFireAdded)
                    {
                        FireEvent(eThingEvents.PropertyAdded, this, tProp, true);
                    }
                    NoFireAdded = false;
                }
            }
            else
            {
                if (!tProp.HasThing())
                {
                    tProp.SetThing(this);
                    tProp.cdeO = cdeMID;
                }
            }
            return tProp;
        }

        /// <summary>
        /// Returns a property of a given Name. If the property does not exist the method returns NULL
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public cdeP GetProperty(string pName)
        {
            return GetProperty(pName, false);
        }

        /// <summary>
        /// Returns a cdeP Property Object
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="DoCreate">If true this method creates the property if it did not exist</param>
        /// <returns></returns>
        public cdeP GetProperty(string pName, bool DoCreate)
        {
            bool tFire = false;
            return GetPropertyInternal(pName, DoCreate, ref tFire);
        }
        private cdeP GetPropertyInternal(string pName, bool DoCreate, ref bool NoFireAdded)
        {
            if (string.IsNullOrEmpty(pName)) return null;

            if (!pName.StartsWith("["))
                return GetPropertyRoot(pName, DoCreate, ref NoFireAdded);

            cdeP tP = null;
            TheDataBase tParent = this;
            var tPath = CU.cdeSplit(pName, "].[", false, false);
            if (MyPropertyBag == null)
                MyPropertyBag = new();
            var tResBag = MyPropertyBag;
            for (int i = 0; i < tPath.Length; i++)
            {
                if (tResBag == null)
                    return null;

                var tName = (i == 0 ? tPath[i].Substring(1) : tPath[i]);
                if (i == tPath.Length - 1)
                    tName = tName.Substring(0, tName.Length - 1);
                if (!tResBag.TryGetValue(tName, out tP))
                {
                    if (DoCreate)
                    {
                        tP = tResBag.GetOrAdd(tName, tName =>
                        {
                            return new cdeP(this) { Name = tName, cdeO = tParent.cdeMID };
                        });
                        if (i == 0) //Only apply to root Property
                            OPCUATypeAttribute.ApplyUAPropertyAttributes(this, tP);
                        if (!NoFireAdded)
                        {
                            FireEvent(eThingEvents.PropertyAdded, this, tP, true);
                        }

                        if (i < tPath.Length - 1)
                            tP.cdePB = new cdeConcurrentDictionary<string, cdeP>();
                        tResBag = tP.cdePB;
                    }
                    else
                        return null;
                }
                else
                {
                    if (!tP.HasThing())
                        tP.SetThing(this);
                    tP.cdeO = tParent.cdeMID;
                    if (i < tPath.Length - 1 && tP.cdePB == null && DoCreate)
                        tP.cdePB = new cdeConcurrentDictionary<string, cdeP>();
                    tResBag = tP.cdePB;
                }
                if (i > 0)
                    tP.cdeOP = tParent as cdeP;
                tParent = tP;
            }
            return tP;
        }

        /// <summary>
        /// This function allows to declare a secure Property
        /// Secure Properties are stored encrypted and can only be decrypted on nodes with the same ApplicationID and SecurityID.
        /// These properties are sent encrypted via the mesh. JavaScript clients CANNOT decrypt the value of the property!
        /// </summary>
        /// <param name="pName"></param>
        /// <param name="pType">In V3.2 only STRING can be declared secure.</param>
        /// <returns></returns>
        public cdeP DeclareSecureProperty(string pName, ePropertyTypes pType)
        {
            cdeP tProp = GetProperty(pName, true);
            if (tProp == null || (tProp.cdeE & 0x01) != 0) return tProp;

            string tOldValue = tProp.ToString();
            if (tProp.cdeO != cdeMID)
                tProp.cdeO = cdeMID;
            tProp.cdeE |= 0x1;
            tProp.cdeT = (int)pType;
            if (!CU.IsNullOrWhiteSpace(tOldValue))
                tProp.Value = tOldValue;
            return tProp;
        }


        /// <summary>
        /// Properties declared NMI properties will show up in the Property Table on NMI controls and Elements
        /// If the property does not exist, it will be created at runtime
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pType">Property Type</param>
        /// <returns></returns>
        public cdeP DeclareNMIProperty(string pName, ePropertyTypes pType)
        {
            cdeP tProp = GetProperty(pName, true);
            if (tProp.cdeO != cdeMID)
                tProp.cdeO = cdeMID;    //NEW:RC3.1 Correct Ownership
            tProp.cdeE |= 0x40;
            tProp.cdeT = (int)pType;
            return tProp;
        }


        /// <summary>
        /// Return the property type of a given property name. Returns ZERO if the property does not exist or the name is null/""
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public ePropertyTypes GetPropertyType(string pName)
        {
            if (string.IsNullOrEmpty(pName)) return 0;

            var tP = GetProperty(pName, false);
            if (tP != null)
                return (ePropertyTypes)tP.cdeT;
            return 0;
        }

        /// <summary>
        /// Clones all existing properties and sub-properties in to the given OutThing
        /// </summary>
        /// <param name="OutThing">Target TheThing to clone the properties to</param>
        public void ClonePropertyValues(TheThing OutThing)
        {
            if (OutThing == null) return;
            foreach (string key in MyPropertyBag.Keys)
            {
                cdeP tP = null;
                if (MyPropertyBag[key].Value != null)
                    tP = OutThing.SetProperty(key, CU.CStr(MyPropertyBag[key].GetValue()), (ePropertyTypes)MyPropertyBag[key].cdeT, MyPropertyBag[key].cdeFOC, null);
                else
                    tP = OutThing.SetProperty(key, "", (ePropertyTypes)MyPropertyBag[key].cdeT, MyPropertyBag[key].cdeFOC, null);
                tP.cdeE = MyPropertyBag[key].cdeE;
                MyPropertyBag[key].Clone(tP);
            }
        }

        /// <summary>
        /// Clones all existing properties and sub-properties in to the given OutThing without setting any values (non-existent properties get a null value)
        /// </summary>
        /// <param name="OutThing">Target TheThing to clone the properties to</param>
        /// <param name="cloneThingBase">if true, the outthing is a deep clone of this</param>
        public void CloneThingAndPropertyMetaData(TheThing OutThing, bool cloneThingBase)
        {
            if (OutThing == null) return;
            if (cloneThingBase)
            {
                this.CloneTo(OutThing);
                OutThing.Capabilities = new List<eThingCaps>(Capabilities);
            }
            foreach (string key in MyPropertyBag.Keys)
            {
                var prop = MyPropertyBag[key];

                var targetProp = OutThing.GetProperty(key);
                targetProp ??= OutThing.SetPropertyTypeOnly(key, (ePropertyTypes)prop.cdeT);
                prop.ClonePropertyMetaData(targetProp);
            }
        }

        /// <summary>
        /// registers a function that is called when the StatusLevel of this Thing has changed
        /// </summary>
        /// <param name="sinkStatusHasChanged"></param>
        public void RegisterStatusChanged(Action<cdeP> sinkStatusHasChanged)
        {
            if (sinkStatusHasChanged != null)
                GetProperty("StatusLevel", true).RegisterEvent(eThingEvents.PropertyChanged, sinkStatusHasChanged);
        }

        /// <summary>
        /// Unregisters a function that is called when the StatusLevel of this Thing has changed
        /// </summary>
        /// <param name="sinkStatusHasChanged"></param>
        public void UnregisterStatusChanged(Action<cdeP> sinkStatusHasChanged)
        {
            if (sinkStatusHasChanged != null)
                GetProperty("StatusLevel", true).UnregisterEvent(eThingEvents.PropertyChanged, sinkStatusHasChanged);
        }
        #endregion


        /// <summary>
        /// Shortcut for "SetProperty("EngineName",value);
        /// </summary>
        [IgnoreDataMember]
        public string EngineName
        {
            get { return TheThing.GetSafePropertyString(this, "EngineName"); }
            set { TheThing.SetSafePropertyString(this, "EngineName", value); }
        }

        /// <summary>
        /// Can be used to Quickly Disable a Thing. Shortcut for "SetProperty("IsDisable", value).
        /// </summary>
        [IgnoreDataMember]
        public bool IsDisabled
        {
            get { return TheThing.GetSafePropertyBool(this, "IsDisabled"); }
            set { TheThing.SetSafePropertyBool(this, "IsDisabled", value); }
        }

        /// <summary>
        /// The Thing will be running but does not show on the NMI ever. Shortcut for "SetProperty("IsHidden", value).
        /// </summary>
        [IgnoreDataMember]
        public bool IsHidden
        {
            get { return TheThing.GetSafePropertyBool(this, "IsHidden"); }
            set { TheThing.SetSafePropertyBool(this, "IsHidden", value); }
        }

        /// <summary>
        /// Shortcut for "SetProperty("Status",value);
        /// 0=Not Running
        /// 1=OK
        /// 2=warning
        /// 3=Failure
        /// 4=Ramp Up
        /// 5=Engineering
        /// 6=Shutdown
        /// 7=Unknown/not visible
        /// </summary>
        [IgnoreDataMember]
        public int StatusLevel
        {
            get { return CU.CInt(TT.GetSafePropertyNumber(this, "StatusLevel")); }
            set
            {
                TT.SetSafePropertyNumber(this, "StatusLevel", value);
            }
        }

        /// <summary>
        /// Sets the last message of the Thing. This can be used for status, error or other messages. Shortcut for "SetProperty("LastMessage", value).
        /// </summary>
        [IgnoreDataMember]
        public string LastMessage
        {
            get { return TheThing.GetSafePropertyString(this, "LastMessage"); }
            set
            {
                TheThing.SetSafePropertyString(this, "LastMessage", value);
            }
        }

        /// <summary>
        /// Convenience method to set StatusLevel and LastMessage in one call. LastMessage will be prefixed with the current date/time. The message will also be logged in the C-DEngine EventLog.
        /// </summary>
        /// <param name="statusLevel"></param>
        /// <param name="lastMessage"></param>
        public void SetStatus(int statusLevel, string lastMessage)
        {
            SetStatus(statusLevel, lastMessage, Guid.Empty);
        }

        /// <summary>
        /// Convenience method to set StatusLevel and LastMessage in one call. LastMessage will be prefixed with the current date/time, using the session (user's) locale settings. The message will also be logged in the C-DEngine EventLog.
        /// </summary>
        /// <param name="statusLevel"></param>
        /// <param name="lastMessage"></param>
        /// <param name="pSEID"></param>
        public void SetStatus(int statusLevel, string lastMessage, Guid pSEID)
        {
            try
            {
                if (MyBaseEngine?.GetBaseThing() != this)
                {
                    StatusLevel = statusLevel;
                }
                else
                {
                    MyBaseEngine.SetStatusLevel(statusLevel);
                }
                LastMessage = CU.GetDateTimeString(DateTimeOffset.Now, pSEID) + $" {lastMessage}";
            }
            catch 
            { 
                //intentionally blank
            }
        }

        /// <summary>
        /// Shortcut for "SetProperty("Version",value);
        /// </summary>
        [IgnoreDataMember]
        public string Version
        {
            get { return GetSafePropertyString(this, "Version"); }
            set { SetSafePropertyString(this, "Version", value); }
        }

        /// <summary>
        /// Shortcut for "SetProperty("Address",value);
        /// </summary>
        [IgnoreDataMember]
        public string Address
        {
            get { return GetSafePropertyString(this, "Address"); }
            set { SetSafePropertyString(this, "Address", value); }
        }

        /// <summary>
        /// Shortcut for "SetProperty("Value",value);
        /// </summary>
        [IgnoreDataMember]
        public string Value
        {
            get { return GetSafePropertyString(this, "Value"); }
            set
            {
                SetSafePropertyString(this, "Value", value);
            }
        }

        /// <summary>
        /// Shortcut for "SetProperty("FriendlyName",value);
        /// </summary>
        [IgnoreDataMember]
        public string FriendlyName
        {
            get { return GetSafePropertyString(this, "FriendlyName"); }
            set { SetSafePropertyString(this, "FriendlyName", value); }
        }

        /// <summary>
        /// Shortcut for "SetProperty("DeviceType",value);
        /// </summary>
        [IgnoreDataMember]
        public string DeviceType
        {
            get { return GetSafePropertyString(this, "DeviceType"); }
            set { SetSafePropertyString(this, "DeviceType", value); }
        }

        /// <summary>
        /// Shortcut for "SetProperty("Parent",value);
        /// </summary>
        [IgnoreDataMember]
        public string Parent
        {
            get { return GetSafePropertyString(this, "Parent"); }
            set { SetSafePropertyString(this, "Parent", value); }
        }

        /// <summary>
        /// Shortcut for "SetProperty("ID",value);
        /// </summary>
        [IgnoreDataMember]
        public string ID
        {
            get { return GetSafePropertyString(this, "ID"); }
            set { SetSafePropertyString(this, "ID", value); }
        }

        /// <summary>
        /// Shortcut for "SetProperty("UID",value);
        /// </summary>
        [IgnoreDataMember]
        public Guid UID
        {
            get { return GetSafePropertyGuid(this, "UID"); }
            set { SetSafePropertyGuid(this, "UID", value); }
        }

        /// <summary>
        /// Shortcut for "SetProperty("LastUpdate",value);
        /// </summary>
        [IgnoreDataMember]
        // Code Review: Do we really need/want this as a manually maintained value, or should this be the highest modified time of any property?
        public DateTimeOffset LastUpdate
        {
            get { return CU.CDate(GetSafePropertyString(this, "LastUpdate")); }
            set { SetSafePropertyDate(this, "LastUpdate", value); }
        }

        /// <summary>
        /// Returns a list of capabilities of TheThing. Can be used to Build "Thing Models"
        /// </summary>
        [IgnoreDataMember]
        public List<eThingCaps> Capabilities
        {
            get
            {
                string[] tCaps = GetSafePropertyString(this, "Capabilities").Split(',');
                List<eThingCaps> t = new ();
                if (tCaps.Length > 0)
                {
                    foreach (string c in tCaps)
                    {
                        if (string.IsNullOrEmpty(c))
                            continue;
                        t.Add((eThingCaps)CU.CInt(c));
                    }
                }
                return t;
            }
            set
            {
                if (value != null && value.Count > 0)
                {
                    string final = "";
                    foreach (eThingCaps t in value)
                    {
                        if (final.Length > 0) final += ",";
                        final += ((int)t).ToString();
                    }
                    SetSafePropertyString(this, "Capabilities", final);
                }
            }
        }

        internal int GetLowestCapability()
        {
            int res = (int)Capabilities.OrderBy(s => s).FirstOrDefault();
            if (res == 0)
                res = 9999;
            return res;
        }

        /// <summary>
        /// Adds a Thing Capability to TheThing
        /// </summary>
        /// <param name="tCap"></param>
        public void AddCapability(eThingCaps tCap)
        {
            List<eThingCaps> t = Capabilities;
            if (!t.Contains(tCap))
            {
                t.Add(tCap);
                Capabilities = t;
            }
        }
        internal List<string> GetKnownEvents()
        {
            return MyRegisteredEvents.GetKnownEvents();
        }
        #region Event Handling

        private readonly CU.RegisteredEventHelper<ICDEThing, object> MyRegisteredEvents = null;

        /// <summary>
        /// Removes all Events from TheThing
        /// </summary>
        public void ClearAllEvents()
        {
            MyRegisteredEvents.ClearAllEvents();
        }
        /// <summary>
        /// Registers a new Event with TheThing
        /// New Events can be registerd and Fired at Runtime
        /// </summary>
        /// <param name="pEventName">Name of the Event to Register</param>
        /// <param name="pCallback">Callback called when the event fires</param>
        public void RegisterEvent(string pEventName, Action<ICDEThing, object> pCallback)
        {
            MyRegisteredEvents.RegisterEvent(pEventName, pCallback);
        }

        /// <summary>
        /// Unregisters a callback for a given Event Name
        /// </summary>
        /// <param name="pEventName">Name of the Event to unregister</param>
        /// <param name="pCallback">Callback to unregister</param>
        public void UnregisterEvent(string pEventName, Action<ICDEThing, object> pCallback)
        {
            MyRegisteredEvents.UnregisterEvent(pEventName, pCallback);
        }

        /// <summary>
        /// Fires the given Event.
        /// Every TheThing can register and Fire any Event on any event.
        /// New Events can be defined at Runtime, registered and fired
        /// </summary>
        /// <param name="pEventName">Name of the Event to Fire</param>
        /// <param name="sender">this pointer or any other ICDETHing that will be handed to the callback. If set to null, "this" will be used </param>
        /// <param name="pPara">Parameter to be handed with the Event</param>
        /// <param name="FireAsync">If set to true, the callback is running on a new Thread</param>
        public void FireEvent(string pEventName, ICDEThing sender, object pPara, bool FireAsync)
        {
            MyRegisteredEvents.FireEvent(pEventName, sender, pPara, FireAsync);
        }

        /// <summary>
        /// Returns true if the event specified exists in the Eventing System of TheThing
        /// </summary>
        /// <param name="pEventName"></param>
        /// <returns></returns>
        public bool HasRegisteredEvents(string pEventName)
        {
            return MyRegisteredEvents.HasRegisteredEvents(pEventName);
        }
        #endregion

        #region Historian

        public Guid RegisterForUpdateHistory(TheHistoryParameters registration)
        {
            return RegisterForUpdateHistory<TheThingStore>(registration, null);
        }

        public Guid RegisterForUpdateHistory<T>(TheHistoryParameters registration) where T : TheDataBase, INotifyPropertyChanged, new()
        {
            return RegisterForUpdateHistory<T>(registration, null);
        }

        public Guid RegisterForUpdateHistory<T>(TheHistoryParameters registration, TheStorageMirror<T> store) where T : TheDataBase, INotifyPropertyChanged, new()
        {
            TheStorageMirrorHistorian.RegisterThingWithHistorian(this, true); 

            Guid token = this.Historian.RegisterConsumer<T>(this, registration, store);
            // TODO Determine if we want history things to be auto created
            //if (createHistoryThing)
            //
            //    var historyThing = new TheThing()
            //    historyThing.EngineName = eEngineName.ThingService
            //    historyThing.DeviceType = "ThingHistory"
            //    historyThing.SetIThingObject(this.Historian)
            //    historyThing.FriendlyName = "History of " + this.FriendlyName
            //    TheThingRegistry.RegisterThing(historyThing)
            //
            //return true
            //
            //else
            //
            //return false
            //
            return token;
        }

        public TheHistoryParameters GetHistoryParameters(Guid historyToken)
        {
            if (Historian != null)
            {
                return Historian.GetHistoryParameters(historyToken);
            }
            return null;
        }

        /// <summary>
        /// Removes the history filter for the historyToken. Call this to free up system resources. Subsequent calls to any APIs with this token will fail.
        /// </summary>
        /// <param name="historyToken">The token returned by RegisterForUpdateHistory.</param>
        public void UnregisterUpdateHistory(Guid historyToken)
        {
            if (Historian != null)
            {
                Historian.UnregisterConsumer(historyToken);
            }
        }
        /// <summary>
        /// Removes the history filter for the historyToken. Call this to free up system resources. Subsequent calls to any APIs with this token will fail.
        /// </summary>
        /// <param name="historyToken">The token returned by RegisterForUpdateHistory.</param>
        /// <param name="keepHistoryStore">Preserves the history store if one was requested using TheHistoryParameters.MaintainHistoryStore</param>
        public void UnregisterUpdateHistory(Guid historyToken, bool keepHistoryStore)
        {
            if (Historian != null)
            {
                Historian.UnregisterConsumer(historyToken, keepHistoryStore);
            }
        }

        internal static List<TheThing> EnumerateThingsWithHistory()
        {
            return new List<TheThing>(TheStorageMirrorHistorian.thingsWithHistorian.GetDynamicEnumerable().Select(kv => kv.Value));
        }

        /// <summary>
        /// Stops history recording for all consumers on this thing.
        /// </summary>
        /// <returns></returns>
        internal bool PauseAllUpdateHistory()
        {
            if (Historian != null)
            {
                Historian.Disabled = false;
            }
            return true;
        }

        /// <summary>
        /// Restarts history recording for all consumers on this thing.
        /// </summary>
        /// <returns></returns>
        internal bool ResumeAllUpdateHistory()
        {
            if (Historian != null)
            {
                Historian.Disabled = true;
            }
            return true;
        }

        /// <summary>
        /// Frees up any history items that have already been returned at least once by a call to GetThingHistory. Call this when complete history is required and the history items have been successfully processed.
        /// </summary>
        /// <param name="historyToken">The token returned by RegisterForUpdateHistory.</param>
        /// <returns></returns>
        public bool ClearUpdateHistory(Guid historyToken)
        {
            if (Historian != null)
            {
                Historian.ClearHistory(historyToken);
            }
            return true;
        }

        /// <summary>
        /// Restarts history retrieval from the point when ClearUpdateHistory was last called. Call this when complete history is required, and a failure during processing requires a retry. For durable history tokens, call this after a restart.
        /// </summary>
        /// <param name="historyToken">The token returned by RegisterForUpdateHistory.</param>
        /// <returns>true if the history token was valid and has been restarted. false if the token was invalid.</returns>
        public bool RestartUpdateHistory(Guid historyToken)
        {
            return RestartUpdateHistory(historyToken, null);
        }

        /// <summary>
        /// Restarts history retrieval from the point when ClearUpdateHistory was last called. Call this when complete history is required, and a failure during processing requires a retry. For durable history tokens, call this after a restart.
        /// </summary>
        /// <param name="historyToken">The token returned by RegisterForUpdateHistory.</param>
        /// <param name="historyParameters">History parameters expected to be returned. If the parameters can not be satisfied with existing history, the token is invalidated (and the method returns false)</param>
        /// <returns>true if the history token was valid and has been restarted. false if the token was invalid or the historyParameters were incompatible with existing history data.</returns>
        public bool RestartUpdateHistory(Guid historyToken, TheHistoryParameters historyParameters)
        {
            return RestartUpdateHistory<TheThingStore>(historyToken, historyParameters, null);
        }

        /// <summary>
        /// Restarts history retrieval from the point when ClearUpdateHistory was last called. Call this when complete history is required, and a failure during processing requires a retry. For durable history tokens, call this after a restart.
        /// </summary>
        /// <param name="historyToken">The token returned by RegisterForUpdateHistory.</param>
        /// <param name="historyParameters">History parameters expected to be returned. If the parameters can not be satisfied with existing history, the token is invalidated (and the method returns false)</param>
        /// <param name="store">Storage Mirror to place the history items into.</param>
        /// <returns>true if the history token was valid and has been restarted. false if the token was invalid or the historyParameters were incompatible with existing history data.</returns>
        public bool RestartUpdateHistory<T>(Guid historyToken, TheHistoryParameters historyParameters, TheStorageMirror<T> store) where T : TheDataBase, INotifyPropertyChanged, new()
        {
            TheStorageMirrorHistorian.RegisterThingWithHistorian(this, false); 
            if (Historian != null)
            {
                return Historian.RestartHistory(historyToken, historyParameters, store);
            }
            return false;
        }


        /// <summary>
        /// Retrieves any available history items since the last time GetThingHistory was called.
        /// </summary>
        /// <param name="token">The token returned by RegisterForUpdateHistory.</param>
        /// <param name="maxCount">The maximum number of history items to be returned by this call.</param>
        /// <param name="bClearHistory">Delete any consumed history items.
        /// Set to false if you need to restart history retrieval at a later point (using RestartUpdateHistory) in order to retry/recover from failures. Call ClearUpdateHistory at a later point when retries are no longer required to free up system resources.</param>
        /// Set to true if full history is not critical in the case of failure or restarts.
        /// <returns></returns>
        public List<TheThingStore> GetThingHistory(Guid token, int maxCount, bool bClearHistory = false)
        {
            return GetThingHistory(token, maxCount, bClearHistory, out _);
        }

        /// <summary>
        /// Retrieves any available history items since the last time GetThingHistory was called.
        /// </summary>
        /// <param name="token">The token returned by RegisterForUpdateHistory.</param>
        /// <param name="maxCount">The maximum number of history items to be returned by this call.</param>
        /// <param name="bClearHistory">Delete any consumed history items.
        /// Set to false if you need to restart history retrieval at a later point (using RestartUpdateHistory) in order to retry/recover from failures. Call ClearUpdateHistory at a later point when retries are no longer required to free up system resources.
        /// Set to true if full history is not required in case of failure or restarts.</param>
        /// <param name="dataLossDetected">true if data loss was detected</param>
        /// <returns></returns>
        public List<TheThingStore> GetThingHistory(Guid token, int maxCount, bool bClearHistory, out bool dataLossDetected)
        {
            if (Historian != null)
            {
                var historyResponse = Historian.GetHistoryAsync(token, maxCount, 0, TimeSpan.Zero, null, bClearHistory).Result;
                if (historyResponse != null)
                {
                    dataLossDetected = historyResponse.DatalossDetected;
                    return historyResponse.HistoryItems;
                }
            }
            dataLossDetected = false;
            return new ();
        }

        public System.Threading.Tasks.Task<TheHistoryResponse> GetThingHistoryAsync(Guid token, int maxCount, int minCount, TimeSpan timeout, CancellationToken? cancelToken, bool bClearHistory)
        {
            if (Historian != null)
            {
                return Historian.GetHistoryAsync(token, maxCount, minCount, timeout, cancelToken, bClearHistory);
            }
            return CU.TaskFromResult<TheHistoryResponse>(null);
        }

        public StorageService.TheStorageMirror<TheThingStore> GetHistoryStore(Guid historyToken)
        {
            return GetHistoryStore<TheThingStore>(historyToken);
        }

        public StorageService.TheStorageMirror<T> GetHistoryStore<T>(Guid historyToken) where T : TheDataBase, INotifyPropertyChanged, new()
        {
            return Historian?.GetHistoryStore<T>(historyToken);
        }

        #endregion

        #region Pin Management for Thing-Group Interactions

        private cdeConcurrentDictionary<string, ThePin> MyPins { get; set; } = new cdeConcurrentDictionary<string, ThePin>();

        /// <summary>
        /// Adds Pins to TheThingBase
        /// </summary>
        /// <param name="pIns">List of Pins to add</param>
        /// <returns></returns>
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
        /// Adds one pin to TheThingBase
        /// </summary>
        /// <param name="pin">Pin to Add</param>
        /// <returns></returns>
        public bool AddPin(ThePin pin)
        {
            if (string.IsNullOrEmpty(pin?.PinName))
                return false;
            pin.cdeO = this.cdeMID;
            UpdatePinListOwner(pin);
            MyPins[pin.PinName] = pin;
            return true;
        }

        private void UpdatePinListOwner(ThePin pPin)
        {
            if (pPin?.MyPins?.Any() == true)
            {
                foreach (var p in pPin.MyPins)
                {
                    p.cdeO = pPin.cdeO;
                    UpdatePinListOwner(p);
                }
            }
        }

        public ThePin GetPin(string pID)
        {
            if (!MyPins.ContainsKey(pID))
                return null;
            return MyPins[pID];
        }

        /// <summary>
        /// Updates an existing Pin
        /// </summary>
        /// <param name="pin"></param>
        /// <returns></returns>
        public bool UpdatePin(ThePin pin)
        {
            if (!MyPins.ContainsKey(pin.PinName))
            {
                AddPin(pin);
                return true;
            }
            if (!string.IsNullOrEmpty(pin.Units))
                MyPins[pin.PinName].Units = pin.Units;
            if (!string.IsNullOrEmpty(pin.PinName))
                MyPins[pin.PinName].PinName = pin.PinName;
            if (!string.IsNullOrEmpty(pin.PinType))
                MyPins[pin.PinName].PinType = pin.PinType;
            if (!string.IsNullOrEmpty(pin.PinProperty))
                MyPins[pin.PinName].PinProperty = pin.PinProperty;
            MyPins[pin.PinName].MaxConnections = pin.MaxConnections;
            MyPins[pin.PinName].IsInbound = pin.IsInbound;
            if (pin.CanConnectToPinType?.Count > 0)
                MyPins[pin.PinName].CanConnectToPinType = pin.CanConnectToPinType;
            if (pin.HasConnectedPins())
                MyPins[pin.PinName].AddPinConnections(pin.GetConnectedPins());
            if (pin.MyPins?.Count > 0)
                MyPins[pin.PinName].MyPins = pin.MyPins;
            return true;
        }

        /// <summary>
        /// Replaces the existing connection with new connections
        /// </summary>
        /// <param name="pPinID">Pin to Update</param>
        /// <param name="pConnectedTo">List of Target Pins</param>
        /// <returns></returns>
        public bool UpdatePinConnection(ThePin pPinID, List<ThePin> pConnectedTo)
        {
            if (string.IsNullOrEmpty(pPinID?.PinName) || !MyPins.ContainsKey(pPinID.PinName))
                return false;
            if (pConnectedTo?.Count > 0 && MyPins[pPinID.PinName].AddPinConnections(pConnectedTo, true))
            {
                UpdatePinProperty(MyPins[pPinID.PinName]);
                return true;
            }
            return false;
        }

        internal void UpdatePinProperty(ThePin pin)
        {
            StringBuilder tIC = new();
            foreach (var tC in pin.GetConnectedPins())
            {
                if (tIC.Length > 0) tIC.Append(";");
                tIC.Append(tC);
            }
            SetProperty($"PINCON_{pin.PinName}", $"{tIC}");
        }

        public void UpdatePinMapper(string tPin)
        {
            try
            {
                var pinID = tPin.Split('_')[1];
                var pin = GetPin(pinID);
                if (pin == null)
                    return;
                if (GetProperty($"{tPin}_ThgSource", false) != null && GetProperty($"{tPin}_ThgProp", false) != null)
                {
                    TheThingRegistry.UnmapPropertyMapper(pin.MyPinMapper);
                    pin.MyPinMapper = TheThingRegistry.PropertyMapper(TT.GetSafePropertyGuid(this, $"{tPin}_ThgSource"), TT.GetSafePropertyString(this, $"{tPin}_ThgProp"), this.cdeMID, pin.PinProperty, false);
                }
            }
            catch
            {
                //intended for now
            }
        }

        public bool RemoveAllPinConnections()
        {
            foreach (var tPin2Remove in MyPins.Values)
            {
                TheThingRegistry.UnmapPropertyMapper(tPin2Remove.MyPinMapper);
                if (!tPin2Remove.HasConnectedPins())
                    continue;
                foreach (var tConPin in tPin2Remove.GetConnectedPins())
                {
                    var tt = TheThingRegistry.GetThingByMID(tConPin.cdeO);
                    var targetPin = tt?.GetPin(tConPin.PinName);
                    tt?.RemovePinConnections(targetPin, [tConPin]);
                }
            }
            return false;
        }

        /// <summary>
        /// Adds new connections to a pin
        /// </summary>
        /// <param name="pPinID"></param>
        /// <param name="pConnectedTo"></param>
        /// <returns></returns>
        public bool AddPinConnections(ThePin pPinID, List<ThePin> pConnectedTo)
        {
            if (string.IsNullOrEmpty(pPinID?.PinName) || !MyPins.ContainsKey(pPinID.PinName))
                return false;
            if (pConnectedTo?.Count > 0 && MyPins[pPinID.PinName].AddPinConnections(pConnectedTo))
            {
                UpdatePinProperty(MyPins[pPinID.PinName]);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes existing pin connections
        /// </summary>
        /// <param name="pPinID"></param>
        /// <param name="pConnectedTo"></param>
        /// <returns></returns>
        public bool RemovePinConnections(ThePin pPinID, List<ThePin> pConnectedTo)
        {
            if (string.IsNullOrEmpty(pPinID?.PinName) || !MyPins.ContainsKey(pPinID.PinName))
                return false;
            TheThingRegistry.UnmapPropertyMapper(pPinID.MyPinMapper);
            if (!MyPins[pPinID.PinName].HasConnectedPins())
                return false;
            if (pConnectedTo?.Count > 0)
            {
                foreach (var t in pConnectedTo)
                {
                    MyPins[pPinID.PinName].RemoveConnection(t);
                    TT targetThing = TheThingRegistry.GetThingByMID(MyPins[pPinID.PinName].cdeO);
                    if (targetThing!=null)
                        targetThing.UpdatePinProperty(MyPins[pPinID.PinName]);
                }
                UpdatePinProperty(MyPins[pPinID.PinName]);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns all pins
        /// </summary>
        /// <returns></returns>
        public List<ThePin> GetAllPins()
        {
            return MyPins.Values.ToList();
        }

        /// <summary>
        /// Returns a Pin with a given ePinTypeName, a string defining the Pin Type
        /// </summary>
        /// <param name="pType"></param>
        /// <param name="bIsInbound"></param>
        /// <returns></returns>
        public ThePin GetPinOfType(string pType, bool bIsInbound = false)
        {
            return MyPins.Values.FirstOrDefault(s => s?.PinType == pType && s?.IsInbound == bIsInbound);
        }

        /// <summary>
        /// Allows for a custom function to find a specific pin
        /// </summary>
        /// <param name="pType"></param>
        /// <param name="mF"></param>
        /// <returns></returns>
        public ThePin GetPinOfTypeByFunc(string pType, Func<ThePin, bool> mF)
        {
            return MyPins.Values.FirstOrDefault(s => s?.PinType == pType && mF(s));
        }

        /// <summary>
        /// Return a list of Pins by function
        /// </summary>
        /// <param name="mF"></param>
        /// <returns></returns>
        public List<ThePin> GetPinsByFunc(Func<ThePin, bool> mF)
        {
            return MyPins.Values.Where(s => mF(s)).ToList();
        }
        #endregion


        /// <summary>
        /// Constructor of TheThing
        /// Creates the MyPropertyBag and Registered Event Tables
        /// </summary>
        public TheThing()
        {
            MyPropertyBag = new cdeConcurrentDictionary<string, cdeP>();
            MyRegisteredEvents = new CU.RegisteredEventHelper<ICDEThing, object>();
            RegisterEvent(eThingEvents.ThingUpdated, null);
        }

        /// <summary>
        /// Returns a string representation of TheThing cdeMID - The unique ID of TheThing
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("{0}", this.cdeMID);
        }

        #region Static Methods

        /// <summary>
        /// Hands a given TSM to a given Thing
        /// </summary>
        /// <param name="tThing">TheThing to handle the message</param>
        /// <param name="tTopic">Topic for the Message</param>
        /// <param name="tSendMessage">TSM to be handled by TheThing</param>
        /// <param name="pLocalCallback">A local callback that TheThing can call at the end of the message handling</param>
        public static void HandleByThing(TheThing tThing, string tTopic, TSM tSendMessage, Action<TSM> pLocalCallback)
        {
            TheUserDetails tCurrentUser = TheUserManager.GetCurrentUser(CU.CGuid(tSendMessage.SEID));
            if (tCurrentUser == null && !string.IsNullOrEmpty(tSendMessage.UID))
                    tCurrentUser = TheUserManager.GetUserByID(CU.CSCDecrypt2GUID(tSendMessage.UID, TheBaseAssets.MySecrets.GetAI()));
            var tProgMsg = new TheProcessMessage() { Topic = tTopic, Message = tSendMessage, LocalCallback = pLocalCallback, CurrentUserID = ((tCurrentUser == null) ? Guid.Empty : tCurrentUser.cdeMID) };
            tProgMsg.ClientInfo = CU.GetClientInfo(tProgMsg);
            if (tThing.MyRegisteredEvents.HasRegisteredEvents(tSendMessage.TXT))
            {
                if (tSendMessage.TXT.StartsWith(eUXEvents.OnClick)) CU.SleepOneEye(200, 200); //REVIEW: Bug#1098 this will make "TAP and HOLD" impossible...but that should be a different event anyway...shall we keep it here or force plugins to add this individually?
                tThing.MyRegisteredEvents.FireEvent(tSendMessage.TXT, tThing, tProgMsg, false); 
            }
            else
                tThing.HandleMessage(tThing, tProgMsg);
        }

        /// <summary>
        /// Sets a property from a "BagItem" - a "Name=Value" representation of a property
        /// </summary>
        /// <param name="tSPLI">TheThing to set the property on</param>
        /// <param name="tItem">Name=Value Item to be set on TheThing</param>
        /// <param name="DoCreate">If Set to true, the new property will be created if it did not exist</param>
        public static void SetPropertyFromBagItem(ICDEThing tSPLI, string tItem, bool DoCreate = false)
        {
            int pos = tItem.IndexOf('=');
            if (pos > 0 && pos < tItem.Length - 1)
            {
                if (!DoCreate && tSPLI.GetProperty(tItem.Substring(0, pos), false) == null) return;
                tSPLI.SetProperty(tItem.Substring(0, pos), tItem.Substring(pos + 1));
            }
        }

        /// <summary>
        /// Returns the proper DataSource String for the given Thing
        /// </summary>
        /// <param name="pThing"></param>
        /// <returns></returns>
        public static string GetThingDataSource(TheThing pThing)
        {
            if (pThing == null) return "";
            return string.Format("TheThing;:;0;:;True;:;cdeMID={0}", pThing.cdeMID);
        }

        /// <summary>
        /// Sets the ID of a TheThing (SetProperty(pGuidName+"_ID",newGuid>);
        /// </summary>
        /// <param name="pThing">Target TheThing</param>
        /// <param name="pGuidName">Prefix of a new and Unique ID Property</param>
        /// <returns></returns>
        public static Guid GetSafeThingGuid(ICDEThing pThing, string pGuidName)
        {
            if (string.IsNullOrEmpty(pGuidName) || pGuidName == "ID")
                pGuidName = "ID";
            else
                pGuidName += "_ID";
            string tGuid = GetSafePropertyString(pThing, pGuidName);
            if (string.IsNullOrEmpty(tGuid))
            {
                tGuid = Guid.NewGuid().ToString();
                SetSafePropertyString(pThing, pGuidName, tGuid);
            }
            return CU.CGuid(tGuid);
        }



        #region SetSafeProperty...Functions


        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TNumber"
        /// </summary>
        /// <param name="pThing">Target TheThing</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Numeric (double) Property Value</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafePropertyNumber(ICDEThing pThing, string pName, double pValue, bool UpdateThing = false)
        {
            return SetSafeProperty(pThing, pName, pValue, ePropertyTypes.TNumber, UpdateThing);
        }

        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TBoolean"
        /// </summary>
        /// <param name="pThing">Target TheThing</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Boolean Property Value</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafePropertyBool(ICDEThing pThing, string pName, bool pValue, bool UpdateThing = false)
        {
            return SetSafeProperty(pThing, pName, pValue, ePropertyTypes.TBoolean, UpdateThing);
        }

        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TDate"
        /// </summary>
        /// <param name="pThing">Target TheThing</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">DateTimeOffset Property Value</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafePropertyDate(ICDEThing pThing, string pName, DateTimeOffset pValue, bool UpdateThing = false)
        {
            return SetSafeProperty(pThing, pName, pValue, ePropertyTypes.TDate, UpdateThing);
        }
        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TDate"
        /// </summary>
        /// <param name="pThing">Target TheThing</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Timespan Property Value</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafePropertyTime(ICDEThing pThing, string pName, TimeSpan pValue, bool UpdateThing = false)
        {
            return SetSafeProperty(pThing, pName, pValue, ePropertyTypes.TDate, UpdateThing);
        }
        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TGuid"
        /// </summary>
        /// <param name="pThing">Target TheThing</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Guid Property Value</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafePropertyGuid(ICDEThing pThing, string pName, Guid pValue, bool UpdateThing = false)
        {
            return SetSafeProperty(pThing, pName, pValue, ePropertyTypes.TGuid, UpdateThing);
        }

        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TString"
        /// </summary>
        /// <param name="pThing">Target TheThing</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">String Property Value</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafePropertyString(ICDEThing pThing, string pName, string pValue, bool UpdateThing = false)
        {
            return SetSafeProperty(pThing, pName, pValue, ePropertyTypes.TString, UpdateThing);
        }

        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TString"
        /// </summary>
        /// <param name="pThing">Target TheThing</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">A value to be put in the Property. No Conversion will take place - it will be stored in the Property with its original Type but Serialized with ToString()</param>
        /// <param name="pType">Sets the Type of the Property</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafeProperty(ICDEThing pThing, string pName, object pValue, ePropertyTypes pType, bool UpdateThing = false)
        {
            if (pThing == null) return null;
            cdeP ret = pThing.GetProperty(pName, true);
            ret.cdeT = (int)pType;
            ret = pThing.SetProperty(pName, pValue);
            if (UpdateThing)
                TheThingRegistry.UpdateThing(pThing, true, ret.IsHighSpeed);
            return ret;
        }

        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TString"
        /// </summary>
        /// <param name="pThing">Target TheThing</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">A value to be put in the Property. No Conversion will take place - it will be stored in the Property with its original Type but Serialized with ToString()</param>
        /// <param name="pType">Sets the Type of the Property</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <param name="ForceUpdate">Forces an update to the Thing - even if the value of the property is the same as already store in the property</param>
        /// <returns></returns>
        public static cdeP SetSafeProperty(TheThing pThing, string pName, object pValue, ePropertyTypes pType, bool UpdateThing = false, bool ForceUpdate = false)
        {
            if (pThing == null) return null;
            cdeP ret = pThing.SetProperty(pName, pValue, pType, ForceUpdate ? 0x20 : -1, null); //NEW3.1: 0x20 was 8
            if (UpdateThing)
                TheThingRegistry.UpdateThing(pThing, true, ret.IsHighSpeed);
            return ret;
        }

        /// <summary>
        /// The MemberSet/Get functions are identical to the SetSafeProperty/GetSafeProperty functions, except that they get the property name from the calling member.
        /// They are used typically in the get{}/set{} methods of .Net Property declarations, for example:
        /// <code>
        /// public bool MyProperty
        /// {
        ///   get { return MemberGetSafePropertyBool(MyBaseThing); }
        /// }
        /// </code>
        /// In this example, the cdeP property name will be "MyProperty".
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pValue"></param>
        /// <param name="UpdateThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static cdeP MemberSetSafePropertyNumber(ICDEThing pThing, double pValue, bool UpdateThing = false, [CallerMemberName] string pName = null)
        {
            return SetSafePropertyNumber(pThing, pName, pValue, UpdateThing);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pValue"></param>
        /// <param name="UpdateThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static cdeP MemberSetSafePropertyBool(ICDEThing pThing, bool pValue, bool UpdateThing = false, [CallerMemberName] string pName = null)
        {
            return SetSafePropertyBool(pThing, pName, pValue, UpdateThing);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pValue"></param>
        /// <param name="UpdateThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static cdeP MemberSetSafePropertyTime(ICDEThing pThing, TimeSpan pValue, bool UpdateThing = false, [CallerMemberName] string pName = null)
        {
            return SetSafePropertyTime(pThing, pName, pValue, UpdateThing);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pValue"></param>
        /// <param name="UpdateThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static cdeP MemberSetSafePropertyDate(ICDEThing pThing, DateTimeOffset pValue, bool UpdateThing = false, [CallerMemberName] string pName = null)
        {
            return SetSafePropertyDate(pThing, pName, pValue, UpdateThing);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pValue"></param>
        /// <param name="UpdateThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static cdeP MemberSetSafePropertyGuid(ICDEThing pThing, Guid pValue, bool UpdateThing = false, [CallerMemberName] string pName = null)
        {
            return SetSafePropertyGuid(pThing, pName, pValue, UpdateThing);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pValue"></param>
        /// <param name="UpdateThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static cdeP MemberSetSafePropertyString(ICDEThing pThing, string pValue, bool UpdateThing = false, [CallerMemberName] string pName = null)
        {
            return SetSafePropertyString(pThing, pName, pValue, UpdateThing);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pValue"></param>
        /// <param name="pType"></param>
        /// <param name="UpdateThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static cdeP MemberSetSafeProperty(ICDEThing pThing, object pValue, ePropertyTypes pType, bool UpdateThing = false, [CallerMemberName] string pName = null)
        {
            return SetSafeProperty(pThing, pName, pValue, pType, UpdateThing);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pValue"></param>
        /// <param name="pType"></param>
        /// <param name="UpdateThing"></param>
        /// <param name="ForceUpdate"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static cdeP MemberSetSafeProperty(TheThing pThing, object pValue, ePropertyTypes pType, bool UpdateThing = false, bool ForceUpdate = false, [CallerMemberName] string pName = null)
        {
            return SetSafeProperty(pThing, pName, pValue, pType, UpdateThing, ForceUpdate);
        }

        #endregion

        #region GetSafeProperty...Functions
        /// <summary>
        /// Returns a safe value of a given property as an object. Return can be null
        /// </summary>
        /// <param name="pThing">TheThing to get the value of</param>
        /// <param name="pName">Property Name</param>
        /// <returns></returns>
        public static object GetSafeProperty(ICDEThing pThing, string pName)
        {
            cdeP tProp;
            if (pThing == null || (tProp = pThing.GetProperty(pName, false)) == null || tProp.Value == null) return null;
            var value = tProp.GetValue();
            //CODE REVIEW Markus: Should we force cloning here by converting to string? Otherwise the caller can modify the property directly, or future changes to the property will be seen by the caller
            //Should we move this into cdeP.GetValue() in the next bigger release?
            //    value = CU.CStr(value)
            //if (!value.GetType().IsValueType)
            //
            //    if (value is ICloneable)
            //    
            //        value = ((ICloneable)value).Clone()
            return value;
        }
        /// <summary>
        /// Returns a (null) safe value of a given property as string
        /// </summary>
        /// <param name="pThing">TheThing to get the value of</param>
        /// <param name="pName">Property Name</param>
        /// <returns></returns>
        public static string GetSafePropertyString(ICDEThing pThing, string pName)
        {
            cdeP tProp;
            if (pThing == null || (tProp = pThing.GetProperty(pName, false)) == null || tProp.Value == null) return "";
            return CU.CStr(tProp.GetValue());
        }
        /// <summary>
        /// Returns a (null) safe value of a given property as double
        /// </summary>
        /// <param name="pThing">TheThing to get the value of</param>
        /// <param name="pName">Property Name</param>
        /// <returns></returns>
        public static double GetSafePropertyNumber(ICDEThing pThing, string pName)
        {
            cdeP tProp;
            if (pThing == null || (tProp = pThing.GetProperty(pName, false)) == null || tProp.Value == null) return 0;
            return CU.CDbl(tProp.GetValue());
        }
        /// <summary>
        /// Returns a (null) safe value of a given property as a boolean
        /// </summary>
        /// <param name="pThing">TheThing to get the value of</param>
        /// <param name="pName">Property Name</param>
        /// <returns></returns>
        public static bool GetSafePropertyBool(ICDEThing pThing, string pName)
        {
            cdeP tProp;
            if (pThing == null || (tProp = pThing.GetProperty(pName, false)) == null || tProp.Value == null) return false;
            return CU.CBool(tProp.GetValue());
        }
        /// <summary>
        /// Returns a (null) safe value of a given property as a Guid
        /// </summary>
        /// <param name="pThing">TheThing to get the value of</param>
        /// <param name="pName">Property Name</param>
        /// <returns></returns>
        public static Guid GetSafePropertyGuid(ICDEThing pThing, string pName)
        {
            cdeP tProp;
            if (pThing == null || (tProp = pThing.GetProperty(pName, false)) == null || tProp.Value == null) return Guid.Empty;
            return TheCommonUtils.CGuid(tProp.GetValue());
        }
        /// <summary>
        /// Returns a (null) safe value of a given property as DateTimeOffset
        /// </summary>
        /// <param name="pThing">TheThing to get the value of</param>
        /// <param name="pName">Property Name</param>
        /// <returns></returns>
        public static TimeSpan GetSafePropertyTime(ICDEThing pThing, string pName)
        {
            cdeP tProp;
            if (pThing == null || (tProp = pThing.GetProperty(pName, false)) == null || tProp.Value == null) return TimeSpan.Zero;
            var dateObj = tProp.GetValue();
            var date = CU.CTimeSpan(dateObj); 
            return date;
        }
        /// <summary>
        /// Returns a (null) safe value of a given property as DateTimeOffset
        /// </summary>
        /// <param name="pThing">TheThing to get the value of</param>
        /// <param name="pName">Property Name</param>
        /// <returns></returns>
        public static DateTimeOffset GetSafePropertyDate(ICDEThing pThing, string pName)
        {
            cdeP tProp;
            if (pThing == null || (tProp = pThing.GetProperty(pName, false)) == null || tProp.Value == null) return DateTimeOffset.MinValue;
            var dateObj = tProp.GetValue();
            var date = CU.CDate(dateObj); // CODE REVIEW: Why did we convert to string here first? Only difference is CJSONDateToDateTime
            return date;
        }
        /// <summary>
        /// Returns a (null) safe value of a given property as string
        /// </summary>
        /// <param name="pThing">TheThing to get the value of. If the object is not of TheThing, an empty string is returned</param>
        /// <param name="pName">Property Name</param>
        /// <returns></returns>
        public static string GetSafePropertyStringObject(object pThing, string pName)
        {
            if (pThing is not TT tThing) return "";
            cdeP tProp;
            // CODE REVIEW Markus: Should we do GetProperty(pName, false) to avoid creating the property if it doesn't exist? Would technically be a breaking change, but consistent with all other GetSafe* functions
            if ((tProp = tThing.GetProperty(pName)) == null || tProp.Value == null) return "";
            return CU.CStr(tProp.GetValue());
        }

        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static string MemberGetSafePropertyString(ICDEThing pThing, [CallerMemberName] string pName = null)
        {
            return GetSafePropertyString(pThing, pName);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static double MemberGetSafePropertyNumber(ICDEThing pThing, [CallerMemberName] string pName = null)
        {
            return GetSafePropertyNumber(pThing, pName);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static bool MemberGetSafePropertyBool(ICDEThing pThing, [CallerMemberName] string pName = null)
        {
            return GetSafePropertyBool(pThing, pName);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static Guid MemberGetSafePropertyGuid(ICDEThing pThing, [CallerMemberName] string pName = null)
        {
            return GetSafePropertyGuid(pThing, pName);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static DateTimeOffset MemberGetSafePropertyDate(ICDEThing pThing, [CallerMemberName] string pName = null)
        {
            return GetSafePropertyDate(pThing, pName);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static TimeSpan MemberGetSafePropertyTime(ICDEThing pThing, [CallerMemberName] string pName = null)
        {
            return GetSafePropertyTime(pThing, pName);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static object MemberGetSafeProperty(ICDEThing pThing, [CallerMemberName] string pName = null)
        {
            return GetSafeProperty(pThing, pName);
        }
        /// <summary>
        /// See <see cref="MemberSetSafePropertyNumber"/>
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pName"></param>
        /// <returns></returns>
        public static string MemberGetSafePropertyStringObject(object pThing, [CallerMemberName] string pName = null)
        {
            return GetSafePropertyStringObject(pThing, pName);
        }

        #endregion

        #endregion
    }
}
