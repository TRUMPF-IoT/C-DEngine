// SPDX-FileCopyrightText: Copyright (c) 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;
using NMI = nsCDEngine.Engines.NMIService.TheNMIEngine;

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// Thing Groups are collections of Things that can be displayed together in a NMI Screen
    /// PREVIEW: This new Class is still in Preview and might have changes in the API before release
    /// </summary>
    public class TheThingGroup : TheThingBase
    {
        /// <summary>
        /// Constructs a new ThingGroup
        /// </summary>
        /// <param name="pThing">TheThing reference in TheThingRegistry</param>
        /// <param name="pID">ID of the new Thing (will be stored in MyBaseThing.ID)</param>
        /// <param name="pConnector">A IBaseEngine Interface pointer to the plugin that owns the group</param>
        public TheThingGroup(TheThing pThing, string pID, IBaseEngine pConnector)
        {
            MyBaseThing = pThing ?? new TheThing();
            if (string.IsNullOrEmpty(MyBaseThing.ID))
                MyBaseThing.ID = pID;
            MyBaseThing.SetIThingObject(this);
            MyScreenGuid = TheThing.GetSafeThingGuid(MyBaseThing, "MyGroupMID");
            if (pConnector != null)
            {
                MyBaseEngine = pConnector;
                if (string.IsNullOrEmpty(MyBaseThing.EngineName))
                    MyBaseThing.EngineName = MyBaseEngine.GetEngineName();
                ModelGuid = MyBaseEngine.GetEngineID();
            }
        }
        private cdeConcurrentDictionary<Guid, TheThingBase> MyGroupThings { get; set; } = new cdeConcurrentDictionary<Guid, TheThingBase>();
        /// <summary>
        /// The GUID of the NMI Screen
        /// </summary>
        public Guid MyScreenGuid { get; protected set; }
        private Guid ModelGuid;

        /// <summary>
        /// The Control of the NMI Editor of the Group
        /// </summary>
        public TheFieldInfo NMIEditorField { get; private set; }
        /// <summary>
        /// Owner plugin of this ThingGroup
        /// </summary>
        protected IBaseEngine MyBaseEngine;
        /// <summary>
        /// TheForm info of the Group
        /// </summary>
        protected TheFormInfo MyGroupForm;

        /// <summary>
        /// if true, the Group has been loaded in the NMI at least once
        /// </summary>
        public bool IsGroupVisible { get; protected set; } = false;

        /// <summary>
        /// Returns a list of all ThingBases in the Group
        /// </summary>
        /// <returns></returns>
        public ICollection<TheThingBase> GetAllGroupThings() { return MyGroupThings.Values; }

        /// <summary>
        /// Clears the group
        /// </summary>
        public void RemoveAllThingsInGroup()
        {
            MyGroupThings.Clear();
        }

        /// <summary>
        /// Returns all cdeMIDs of the ThingBases in the group
        /// </summary>
        /// <returns></returns>
        public List<Guid> GetAllGroupThingMIDs()
        {
            return MyGroupThings.Keys.ToList();
        }

        private bool UpdateGTP()
        {
            var tOld = CU.CStr(GetProperty("GroupFlds", false));
            StringBuilder tRes = new StringBuilder();
            foreach (var thing in MyGroupThings.Values)
            {
                if (tRes.Length>0) tRes.Append(";");
                tRes.Append($"{thing.GetBaseThing().cdeMID}");
            }
            SetProperty("GroupFlds",tRes.ToString());
            return tOld.Length!=tRes.Length;
        }

        private void InitGTP()
        {
            lock (MyGroupThings.MyLock)
            {
                MyGroupThings.Clear();
                var tGFP = CU.CStr(GetProperty("GroupFlds", false));
                if (!string.IsNullOrEmpty(tGFP))
                {
                    var flds = tGFP.Split(';');
                    foreach (var flds2 in flds)
                    {
                        var tN = TheThingRegistry.GetThingByMID(CU.CGuid(flds2));
                        TheThingBase tB = tN?.GetObject() as TheThingBase;
                        if (tB != null)
                            AddOrUpdateThingInGroup(tB);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a new ThingBase to the Group
        /// </summary>
        /// <param name="thing">ThingBase to add</param>
        /// <returns></returns>
        public virtual bool AddOrUpdateThingInGroup(TheThingBase thing)
        {
            if (thing.GetBaseThing() != null)
            {
                MyGroupThings[thing.GetBaseThing().cdeMID] = thing;
                thing.SetProperty("cdeGroupID", MyScreenGuid);
                return UpdateGTP();
            }
            return false;
        }

        /// <summary>
        /// Removes a ThingBase from the Group by its cdeMID
        /// </summary>
        /// <param name="thingID">cdeMID of the Thing to remove</param>
        /// <returns></returns>
        public virtual bool RemoveThingMIDFromGroup(Guid? thingID)
        {
            if (thingID.Value != Guid.Empty)
            {
                MyGroupThings.RemoveNoCare(CU.CGuid(thingID));
                TheThingRegistry.GetThingByMID(CU.CGuid(thingID))?.SetProperty("cdeGroupID", Guid.Empty);
                return UpdateGTP();
            }
            return false;
        }

        /// <summary>
        /// Removes a ThingBase from this group
        /// </summary>
        /// <param name="thing">Thing to remove</param>
        /// <returns></returns>
        public virtual bool RemoveThingFromGroup(TheThingBase thing)
        {
            return RemoveThingMIDFromGroup(thing?.GetBaseThing()?.cdeMID);
        }

        /// <summary>
        /// Counts ThingBases of a specific type in the group
        /// </summary>
        /// <param name="pType">Type to look for</param>
        /// <returns></returns>
        public int CountThingsInGroupOfType(Type pType)
        {
            return MyGroupThings.Values.Count(s => s?.GetType() == pType || s?.GetType()?.IsSubclassOf(pType) == true);
        }

        /// <summary>
        /// Return the count of a specific Type with function 
        /// </summary>
        /// <param name="pType">Type to look for</param>
        /// <param name="mF">Additional filter function</param>
        /// <returns></returns>
        public int CountThingsInGroupOfTypeByFunc(Type pType, Func<TheThingBase, bool> mF)
        {
            return MyGroupThings.Values.Count(s => s?.GetType() == pType || s?.GetType()?.IsSubclassOf(pType) == true && mF(s));
        }
        /// <summary>
        /// Returns all ThingBases of a specific Type
        /// </summary>
        /// <param name="pType">Type to look for</param>
        /// <returns></returns>
        public List<TheThingBase> GetThingsOfType(Type pType)
        {
            return MyGroupThings.Values.Where(s => s?.GetType() == pType || s?.GetType()?.IsSubclassOf(pType) == true).ToList();
        }

        /// <summary>
        /// Returns TheThingBases by function in the current ThingGroup
        /// </summary>
        /// <param name="pType">Type to Look for</param>
        /// <param name="mF">Additional filter function</param>
        /// <returns></returns>
        public List<TheThingBase> GetThingsOfTypeByFunc(Type pType, Func<TheThingBase, bool> mF)
        {
            return MyGroupThings.Values.Where(s => s?.GetType() == pType || s?.GetType()?.IsSubclassOf(pType) == true && mF(s)).ToList();
        }

        /// <summary>
        /// Updates the NMI of all pins
        /// </summary>
        /// <returns></returns>
        public virtual bool UpdateAllPins()
        {
            if (MyGroupThings?.Values?.Any() == true)
            {
                foreach (var t in MyGroupThings.Values)
                {
                    try
                    {
                        t.UpdatePinNMI();
                    }
                    catch (Exception e)
                    {
                        SetMessage(TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : $"Exception during Pin Update: {e}", DateTimeOffset.Now, 0, eMsgLevel.l2_Warning);
                    }
                }
                return true;
            }
            return false;
        }

        #region NMI Part
        /// <summary>
        /// Override of the standard CreateUX Function
        /// </summary>
        /// <returns></returns>
        public override bool CreateUX()
        {
            if (!mIsUXInitCalled)
            {
                mIsUXInitCalled = true;

                cdeConcurrentDictionary<string, TheMetaDataBase> block = new();
                MyGroupForm = new(MyScreenGuid, eEngineName.NMIService, null, $"TheThing;:;0;:;True;:;cdeMID={MyBaseThing.cdeMID}")
                {
                    DefaultView = eDefaultView.Form,
                    PropertyBag = new nmiCtrlFormView { FitToScreen = true, HideCaption = true, UseMargin = false, InnerClassName = "enFormInner" },
                    ModelID = "StandardForm"
                };
                MyGroupForm.RegisterEvent2(eUXEvents.OnBeforeMeta, (pmsg, sender) =>
                {
                    if (!IsGroupVisible)
                    {
                        ScanThings();
                        InitDynamicNMI();
                    }
                    IsGroupVisible = true;
                });
                MyGroupForm.RegisterEvent2(eUXEvents.OnShow, (pmsg, sender) =>
                {
                    CalculateFormSize();
                });
                block["Form"] = MyGroupForm;
                block["DashIcon"] = NMI.AddFormToThingUX(MyBaseThing, MyGroupForm, "CMyForm", MyBaseThing.FriendlyName, 1, 3, 0, "..Overview", null, new ThePropertyBag() { "RenderTarget=HomeCenterStage" });
                mIsUXInitialized = DoCreateUX(block);
                CalculateFormSize();
            }
            return true;
        }

        /// <summary>
        /// Allows to add new NMI controls to the Group NMI
        /// </summary>
        /// <param name="pUXFlds">Dictionary of Fields that have been added. Add your controls to the dictionary for best practice
        /// If the group can adjust the following properties:
        /// NMI_DisallowAdd: If true, the NMI Editor will not allow adding of new Things
        /// NMI_ShowAllProperties: If true, the form will show all a table with all properties
        /// NMI_AddRefresh: If true, a small refresh button is added to the top left of the Group
        /// </param>
        /// <returns>If true, the NMI assumes the Form NMI was completely initialized</returns>
        public virtual bool DoCreateUX(cdeConcurrentDictionary<string, TheMetaDataBase> pUXFlds)
        {
            bool DisallowAdd = CU.CBool(MyBaseThing.GetProperty("NMI_DisallowAdd", false)?.GetValue());
            bool ShowAllProperties = CU.CBool(MyBaseThing.GetProperty("NMI_ShowAllProperties", false)?.GetValue());
            bool AddRefresh = CU.CBool(MyBaseThing.GetProperty("NMI_AddRefresh", false)?.GetValue());

            NMIEditorField = NMI.AddSmartControl(MyBaseThing, MyGroupForm, eFieldType.TileGroup, MyGroupForm.FldPos, 0, 0, null, null, new nmiCtrlTileGroup { NoTE = true });
            if (!DisallowAdd && !TheBaseAssets.MyServiceHostInfo.IsCloudService)
            {
                NMIEditorField.RegisterEvent2(eUXEvents.OnShowEditor, (pMsg, para) =>
                {
                    var tMyForm = NMI.GetNMIEditorForm();
                    if (tMyForm != null)
                    {
                        var tThings = TheThingRegistry.GetThingsByFunc("*", s => !string.IsNullOrEmpty($"{s.GetProperty("FaceTemplate", false)?.GetValue()}"));
                        StringBuilder tOpt = new StringBuilder();
                        foreach (var tThing in tThings)
                        {
                            var tG = tThing.GetProperty("cdeGroupID", false);
                            if (!MyGroupThings.Keys.Contains(tThing.cdeMID) && (tG == null || CU.CGuid(tG) == Guid.Empty) && tThing.StatusLevel < 4)
                            {
                                if (tOpt.Length > 0) tOpt.Append(";");
                                tOpt.Append($"{tThing.FriendlyName}:{tThing.cdeMID}");
                            }
                        }
                        if (tOpt.Length == 0)
                            tOpt.Append($"No Thing Available:{Guid.Empty}");
                        NMI.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SmartLabel, 2005, 0xA2, 0x80, "Select Thing to Add", null, new nmiCtrlSmartLabel() { NoTE = true, TileWidth = 5 });
                        NMI.GetNMIEditorThing()?.SetProperty("newthing", Guid.Empty);
                        MyBaseThing.SetProperty("newthing", Guid.Empty);
                        NMI.AddSmartControl(MyBaseThing, tMyForm, eFieldType.ComboBox, 2006, 0xA2, 0x80, null, $"newthing", new nmiCtrlComboBox() { Options = tOpt.ToString(), EnableSearch = true, NoTE = true, TileWidth = 5 });
                        var ttt = NMI.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, 2010, 2, 0x80, "Add Thing to Screen", null, new nmiCtrlTileButton { NoTE = true, TileWidth = 5, ClassName = "cdeGoodActionButton" });
                        ttt.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "add", (sender, para) =>
                        {
                            var tMsg = para as TheProcessMessage;
                            if (tMsg != null)
                            {
                                var tN = TheThingRegistry.GetThingByMID(CU.CGuid(MyBaseThing.GetProperty("newthing")));
                                TheThingBase tB = tN?.GetObject() as TheThingBase;
                                if (tB != null && AddOrUpdateThingInGroup(tB))
                                {
                                    ReloadForm();
                                    TheCommCore.PublishToOriginator(tMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Control added: {tN.FriendlyName}"));
                                }
                            }
                        });
                        var RemBut = NMI.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, 2011, 2, 0x80, "Clear Screen", null, new nmiCtrlTileButton { NoTE = true, TileWidth = 2, AreYouSure = "This removes all Controls, are you sure?", ClassName = "cdeBadActionButton" });
                        RemBut?.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "remove", (sender, para) =>
                        {
                            var tMsg = para as TheProcessMessage;
                            if (tMsg != null)
                            {
                                ResetAllThings();
                                ReloadForm();
                                TheCommCore.PublishToOriginator(tMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"All Controls Removed"));
                            }
                        });
                        var RefreshBut = NMI.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, 2015, 2, 0x80, "Refresh", null, new nmiCtrlTileButton { NoTE = true, TileWidth = 1, ClassName = "cdeGoodActionButton" });
                        RefreshBut?.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "refresh", (sender, para) =>
                        {
                            var tMsg = para as TheProcessMessage;
                            if (tMsg != null)
                                ReloadForm();
                        });
                        NMI.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, 2013, 0xA2, 0x80, "X", "GroupSizeX", new nmiCtrlNumber() { NoTE = true, TileWidth = 1 });
                        NMI.AddSmartControl(MyBaseThing, tMyForm, eFieldType.Number, 2014, 0xA2, 0x80, "Y", "GroupSizeY", new nmiCtrlNumber() { NoTE = true, TileWidth = 1 });

                        pMsg.Cookie = true;
                    }
                });
            }
            pUXFlds["NMIEditor"] = NMIEditorField;

            if (AddRefresh)
            {
                var tBut = NMI.AddSmartControl(MyBaseThing, MyGroupForm, eFieldType.TileButton, 10020, 2, 0x0, null, null, new nmiCtrlTileButton() { IsAbsolute = true, TileWidth = 1, TileHeight = 1, Left = 0, Top = 0, NoTE = true, ClassName = "enTransBut" });
                tBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "refresh", (sender, pmsg) =>
                {
                    ReloadForm();
                });
                pUXFlds["RefreshButton"] = tBut;
            }

            if (ShowAllProperties)
            {
                pUXFlds["PropTableGroup"] = NMI.AddSmartControl(MyBaseThing, MyGroupForm, eFieldType.CollapsibleGroup, 10000, 2, 0x80, "All Properties", null, new nmiCtrlCollapsibleGroup { DoClose = true, IsSmall = true, TileWidth = 12 });
                pUXFlds["PropTable"] = NMI.AddSmartControl(MyBaseThing, MyGroupForm, eFieldType.Table, 10010, 8, 0x80, null, "mypropertybag", new nmiCtrlTableView() { TileWidth = 12, TileHeight = 7, NoTE = true, ParentFld = 10000, ShowFilterField = true });
            }
            return true;
        }

        /// <summary>
        /// Updates all Fld Positions
        /// </summary>
        /// <param name="pScene"></param>
        public virtual void UpdateFldPositions(TheFOR pScene)
        {
            foreach (var tf in pScene.Flds)
            {
                foreach (var t in MyGroupThings.Values)
                {
                    var tn = CU.CInt(t.GetBaseThing().GetProperty($"FldStart_{CU.CGuid(pScene.ID)}", false)?.GetValue());
                    if (tn==tf.FldOrder)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Delets all Fld Positions
        /// </summary>
        public virtual void DeleteAllFldPositions()
        {
            foreach (var t in MyGroupThings.Values)
            {
                t.GetBaseThing().SetProperty($"FldStart_{CU.CGuid(MyScreenGuid)}", "0");
            }
            ReloadForm();
        }

        /// <summary>
        /// Reloads the Form with updated Dynamic Fields
        /// </summary>
        public virtual void ReloadForm()
        {
            InitDynamicNMI();
            if (MyGroupForm != null)
                TheCommCore.PublishCentral(new TSM(eEngineName.NMIService, $"NMI_REQ_DASH:", $"{CU.cdeGuidToString(MyGroupForm.cdeMID)}:CMyForm:{CU.cdeGuidToString(MyGroupForm.cdeMID)}:{CU.cdeGuidToString(ModelGuid)}:true:true"));
        }
        /// <summary>
        /// Initializes the dynamic part of the form
        /// </summary>
        public virtual void InitDynamicNMI()
        {
            InitGTP();
            UpdatePinConnections(false);
            NMI.DeleteFieldsByRange(MyGroupForm, 100, 9999);
            MyGroupForm.FldStart = 100;
            if (MyGroupThings?.Any() == true)
            {
                foreach (var t in MyGroupThings.Values)
                {
                    t?.AddDeviceFace(MyGroupForm, 0, 0);
                }
                DrawPinLines(MyGroupForm, MyGroupThings.Values.ToList());
                CalculateFormSize();
            }
        }

        private void ResetAllThings()
        {
            SetProperty("GroupFlds", ";");
            var tThings = TheThingRegistry.GetThingsByFunc("*", s => CU.CGuid(s.GetProperty("cdeGroupID", false)?.GetValue()) == MyScreenGuid || CU.CInt(s.GetProperty($"FldStart_{MyScreenGuid}", false)?.GetValue())>0);
            if (tThings?.Any() == true)
            {
                foreach (var tT in tThings)
                {
                    tT?.SetProperty($"FldStart_{MyScreenGuid}", 0);
                    tT?.SetProperty("cdeGroupID", Guid.Empty);
                }
            }
        }

        private void ScanThings()
        {
            var tThings = TheThingRegistry.GetThingsByFunc("*", s => CU.CGuid(s.GetProperty("cdeGroupID", false)?.GetValue()) == MyScreenGuid);
            if (tThings?.Any() == true)
            {
                bool FoundOne = false;
                var tGFP = CU.CStr(GetProperty("GroupFlds", false));
                List<string> lFields = new List<string>();
                if (!string.IsNullOrEmpty(tGFP))
                {
                    lFields = tGFP.Split(';').ToList();
                }
                foreach (var tTcdeMID in tThings.Select(t=>t.cdeMID))
                {
                    if (!lFields.Contains($"{tTcdeMID}"))
                    {
                        lFields.Add($"{tTcdeMID}");
                        FoundOne = true;
                    }
                }
                if (FoundOne)
                {
                    StringBuilder tRes = new StringBuilder();
                    foreach (var fID in lFields)
                    {
                        if (tRes.Length > 0) tRes.Append(";");
                        tRes.Append(fID);
                    }
                    SetProperty("GroupFlds", tRes.ToString());
                    InitGTP();
                }
            }
        }

        #endregion

        /// <summary>
        /// Finds Compatible Pins of a given Pin in the current ThingGroup
        /// </summary>
        /// <param name="firstPin"></param>
        /// <returns></returns>
        public virtual List<ThePin> FindCompatiblePins(ThePin firstPin)
        {
            var ret = new List<ThePin>();
            var AllDevs = GetAllGroupThings();
            firstPin.CompatiblePins.Clear();
            foreach (var secondRound in AllDevs)
            {
                if (firstPin.cdeO == secondRound.GetBaseThing().cdeMID)
                    continue;
                foreach (var secondPin in secondRound.GetBaseThing().GetAllPins())
                {
                    foreach (var firstPinType in firstPin.CanConnectToPinType)
                    {
                        if (secondPin.CanConnectToPinType.Contains(firstPinType) && firstPin.IsInbound != secondPin.IsInbound &&
                                (firstPin.MaxConnections == 0 || firstPin.PinConnectionCnt() < firstPin.MaxConnections) &&
                                (secondPin.MaxConnections == 0 || secondPin.PinConnectionCnt() < secondPin.MaxConnections))
                        {
                            firstPin.CompatiblePins.Add(secondPin);
                            ret.Add(secondPin);
                        }
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// Calculates all Compatible Pin Connections
        /// </summary>
        /// <param name="AutoSetConnections">if true, compatible pins will be auto-connected. Beware, if there are multiple pin combination possible, this migth not do what you expect</param>
        /// <param name="pPinTypes">Limit the Pin Connections to a specific type</param>
        /// <returns></returns>
        public virtual bool UpdatePinConnections(bool AutoSetConnections, List<string> pPinTypes=null)
        {
            var AllDevs = GetAllGroupThings();
            foreach (var firstRound in AllDevs)
            {
                foreach (var firstPin in firstRound.GetBaseThing().GetAllPins())
                {
                    var res = FindCompatiblePins(firstPin);
                    if (AutoSetConnections && res.Count == 1 && (pPinTypes?.Any()!=true || pPinTypes.Contains(firstPin.PinType)))
                    {
                        var secondPin = res[0];
                        firstRound.GetBaseThing().AddPinConnections(firstPin, new List<ThePin> { secondPin });
                        TheThingRegistry.GetThingByMID(secondPin.cdeO)?.AddPinConnections(secondPin, new List<ThePin> { firstPin });
                    }
                    TheThingRegistry.GetThingByMID(firstPin.cdeO)?.UpdatePinMapper($"PIN_{firstPin.PinName}");
                }
            }
            return true;
        }

        public virtual bool UpdateSavedPinConnections()
        {
            try
            {
                var AllDevs = GetAllGroupThings();
                foreach (var gThing in AllDevs)
                {
                    foreach (var firstPin in gThing.GetBaseThing().GetAllPins())
                    {
                        var tConnList = $"{gThing.GetProperty($"PINCON_{firstPin.PinName}", false).GetValue()}";
                        if (!string.IsNullOrEmpty(tConnList))
                        {
                            var tList = tConnList.Split(';');
                            foreach (var t in tList)
                            {
                                var p = t.Split(':');
                                var tt = TheThingRegistry.GetThingByMID(CU.CGuid(p[0]));
                                if (tt != null)
                                {
                                    var secondPin = tt.GetPin(p[1]);
                                    if (secondPin != null)
                                    {
                                        gThing.GetBaseThing().AddPinConnections(firstPin, new List<ThePin> { secondPin });
                                        tt?.AddPinConnections(firstPin, new List<ThePin> { firstPin });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) 
            {
                //
            }
            return true;
        }

        /// <summary>
        /// Calculates the form size required for the screen
        /// </summary>
        public virtual void CalculateFormSize()
        {
            int maxh = 0;
            int maxw = 0;
            foreach (var t in GetAllGroupThings())
            {
                if (t?.MyNMIFaceModel == null)
                    continue;
                var tPins = t.GetBaseThing().GetAllPins();
                bool hasRightPin = tPins.Exists(p => p.NMIIsPinRight);
                bool hasLeftPin = tPins.Exists(p => !p.NMIIsPinRight);
                var tFaceWidth = (t.MyNMIFaceModel.XLen + (78 * ((hasLeftPin ? 1 : 0) + (hasRightPin ? 1 : 0))));
                if (t.MyNMIFaceModel.XPos + tFaceWidth > maxw)
                    maxw = t.MyNMIFaceModel.XPos + tFaceWidth;

                if (t.MyNMIFaceModel.YPos + t.MyNMIFaceModel.YLen > maxh)
                    maxh = t.MyNMIFaceModel.YPos + t.MyNMIFaceModel.YLen;
            }
            int th = ((maxh + 39) / 78)+1;
            int tw = ((maxw + 39) / 78)+1;
            if (tw < 6) tw = 6;
            if (GetProperty("GroupSizeX", false) != null)
            {
                var gsx = CU.CInt(GetProperty("GroupSizeX", true)?.GetValue());
                if (gsx >= tw)
                    tw = gsx;
                else
                    SetProperty("GroupSizeX", tw);
            }
            if (GetProperty("GroupSizeY", false) != null)
            {
                var gsy = CU.CInt(GetProperty("GroupSizeY", true)?.GetValue());
                if (gsy >= th)
                    th = gsy;
                else
                    SetProperty("GroupSizeY", tw);
            }

            MyGroupForm.PropertyBag = new nmiCtrlFormView { TileWidth = tw, TileHeight = th };

            if (NMIEditorField != null)
                NMIEditorField.PropertyBag = new nmiCtrlTileGroup { TileWidth = tw, TileHeight = th };
        }

        /// <summary>
        /// Automatically draws all Lines between connected Pins
        /// </summary>
        /// <param name="MyLiveForm">Target Group Form</param>
        /// <param name="pDevs">Devices with Pins</param>
        /// <param name="pProperties">Some properties that can be set on the lines</param>
        public virtual void DrawPinLines(TheFormInfo MyLiveForm, List<TheThingBase> pDevs, ThePropertyBag pProperties = null)
        {
            if (pDevs?.Count > 0)
            {
                int lineWidth = CU.CInt(ThePropertyBag.PropBagGetValue(pProperties, "LineWidth"));
                if (lineWidth == 0) lineWidth = 6;
                for (int i = 0; i < pDevs.Count; i++)
                {
                    if (pDevs[i] == null) continue;

                    var sourceT = pDevs[i].GetBaseThing().GetBaseThing();
                    var allSourcePins = sourceT.GetAllPins();
                    var sourceOutPins = allSourcePins.Where(s => !s.IsInbound).ToList(); //Starting from "Out" pins
                    foreach (var sourcePin in sourceOutPins)
                    {
                        string PinTypeFilter = ThePropertyBag.PropBagGetValue(pProperties, "PinTypeFilter");
                        if (!string.IsNullOrEmpty(PinTypeFilter) && sourcePin.PinType != PinTypeFilter) continue;
                        var targetInPins = sourcePin?.GetConnectedPins();   //To their connected Pins
                        if (targetInPins?.Count > 0)
                        {
                            bool sourceHasRightPin = allSourcePins.Exists(p => p.NMIIsPinRight);
                            bool sourceHasLeftPin = allSourcePins.Exists(p => !p.NMIIsPinRight);
                            foreach (var targetPin in targetInPins)
                            {
                                var targetT = TheThingRegistry.GetThingByMID(targetPin.cdeO);
                                var targetTB = targetT?.GetObject() as TheThingBase;
                                var targetFace = targetTB?.MyNMIFaceModel;
                                if (targetFace == null) continue;
                                var sourceFace = pDevs[i].MyNMIFaceModel;
                                var flowStyle = sourcePin.GetMapperStyle("");
                                bool targetHasRightPin = targetT.GetAllPins().Exists(p => p.NMIIsPinRight);
                                bool targetHasLeftPin = targetT.GetAllPins().Exists(p => !p.NMIIsPinRight);

                                var sLeft = sourcePin.NMIIsPinRight ?
                                    sourceFace.XPos + (sourceFace.XLen + (78 * ((sourceHasLeftPin ? 1 : 0) + (sourceHasRightPin ? 1 : 0)))) - (78- sourcePin.NMIPinWidth) :
                                    sourceFace.XPos + (78 - targetPin.NMIPinWidth) + targetPin.NMIxDelta; 
                                var tLeft = targetPin.NMIIsPinRight ?
                                    targetFace.XPos + (targetFace.XLen + (78 * ((targetHasLeftPin ? 1 : 0) + (targetHasRightPin ? 1 : 0)))) - (78 - sourcePin.NMIPinWidth) :
                                    targetFace.XPos + (78 - targetPin.NMIPinWidth) + targetPin.NMIxDelta;

                                var left = sLeft > tLeft ? tLeft : sLeft;

                                var sTop = sourceFace.YPos + ((sourcePin.NMIPinTopPosition * 39) + 15) + sourcePin.NMIyDelta;
                                var tTop = targetFace.YPos + ((targetPin.NMIPinTopPosition * 39) + 15) + targetPin.NMIyDelta;

                                string dir = "up";
                                var top = tTop;
                                if (sTop < tTop)
                                {
                                    top = sTop;
                                    dir = "down";
                                }
                                int x = left;
                                int y = top;
                                int xl = Math.Abs(tLeft - sLeft);
                                if (xl < lineWidth) xl = lineWidth;
                                int yl = Math.Abs(sTop - tTop);
                                if (yl < lineWidth) yl = lineWidth;
                                if (yl > lineWidth)
                                {
                                    if (!sourcePin.DrawLineAtTarget)
                                        x = sLeft;
                                    StringBuilder moveData = new();
                                    int movecnt = ((yl + lineWidth) / 100) + 1;
                                    for (int n = 0; n < movecnt; n++)
                                        moveData.Append($"<div class=\"cde{flowStyle}flow{dir}\" style=\"animation-delay: {n * 2}s; animation-duration: {movecnt * 2}s;\"></div>");
                                    AddPinLine(MyLiveForm, $"line{targetT}v_{sourcePin.PinName.Replace(' ', '_')}",
                                        x + (sourcePin.DrawLineAtTarget ? xl : 0),
                                        y,
                                        lineWidth,
                                        yl + lineWidth,
                                        moveData.ToString());
                                    GetProperty($"line{targetT}v_{sourcePin.PinName.Replace(' ', '_')}", true).cdeE |= 8;
                                }
                                if (xl > lineWidth)
                                {
                                    //y = top;
                                    x = sLeft;
                                    if (tLeft > sLeft)
                                    {
                                        dir = "right";
                                    }
                                    else
                                    {
                                        dir = "left";
                                        if (sourcePin.DrawLineAtTarget)
                                            dir = "right";
                                        x = tLeft;
                                    }
                                    StringBuilder moveData = new();
                                    int movecnt = (xl / 100) + 1;
                                    for (int n = 0; n < movecnt; n++)
                                        moveData.Append($"<div class=\"cde{flowStyle}flow{dir}\" style=\"animation-delay: {n * 2}s; animation-duration: {movecnt * 2}s;\"></div>");
                                    AddPinLine(MyLiveForm, $"line{targetT}h_{sourcePin.PinName.Replace(' ', '_')}",
                                        x,
                                        y,
                                        xl,
                                        lineWidth,
                                        moveData.ToString());
                                    GetProperty($"line{targetT}h_{sourcePin.PinName.Replace(' ', '_')}", true).cdeE |= 8;
                                }
                            }
                        }
                    }
                }
                UpdateLines(pDevs);
            }
        }

        /// <summary>
        /// Updates the Line style between pins if there is anything "flowing" along the lines
        /// </summary>
        /// <param name="pDevs">Devices with connected pin to update</param>
        public virtual void UpdateLines(List<TheThingBase> pDevs)
        {
            if (pDevs?.Count > 0)
            {
                for (int i = 0; i < pDevs.Count; i++)
                {
                    if (pDevs[i] == null) continue;
                    foreach (var sourcePin in pDevs[i].GetBaseThing().GetBaseThing().GetPinsByFunc(s => !s.IsInbound))
                    {
                        if (sourcePin == null) continue;
                        var flowStyle = sourcePin.GetMapperStyle("");

                        var targetInPins = sourcePin.GetConnectedPins();
                        if (targetInPins?.Count > 0)
                        {
                            foreach (var targetPin in targetInPins)
                            {
                                var stylev = $"cdevert{flowStyle}line";
                                var styleh = $"cdehori{flowStyle}line";
                                if (CU.CDbl(targetPin.PinValue) == 0)
                                {
                                    stylev += "nf";
                                    styleh += "nf";
                                }
                                else
                                    stylev += "";
                                var targetThing = TheThingRegistry.GetThingByMID(targetPin.cdeO);
                                if (targetThing == null)
                                    continue;
                                SetProperty($"line{targetThing}v_{sourcePin.PinName.Replace(' ', '_')}", stylev);
                                SetProperty($"line{targetThing}h_{sourcePin.PinName.Replace(' ', '_')}", styleh);
                            }
                        }
                    }
                }
                TheThingRegistry.UpdateThing(MyBaseThing, true);
            }
        }

        public virtual void AddPinLine(TheFormInfo MyLiveForm, string pDataName, int x, int y, int xl, int yl, string moveData, string pClassname = null)
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

}
