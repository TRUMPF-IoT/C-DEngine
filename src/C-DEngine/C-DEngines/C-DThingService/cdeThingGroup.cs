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
using NMI = nsCDEngine.Engines.NMIService.TheNMIEngine;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;

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
                    MyBaseThing.EngineName = MyBaseEngine?.GetEngineName();
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
                thing.SetProperty("cdeGroupID", MyBaseThing.cdeMID);
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
                    PropertyBag = new nmiCtrlFormView { TileWidth = 18, FitToScreen = true, HideCaption = true, UseMargin = false, InnerClassName = "enFormInner" },
                    ModelID = "StandardForm"
                };
                MyGroupForm.RegisterEvent2(eUXEvents.OnBeforeMeta, (pmsg, sender) =>
                {
                    if (!IsGroupVisible)
                        InitDynamicNMI();
                    IsGroupVisible = true;
                });
                block["Form"] = MyGroupForm;
                block["DashIcon"] = NMI.AddFormToThingUX(MyBaseThing, MyGroupForm, "CMyForm", MyBaseThing.FriendlyName, 1, 3, 0, "..Overview", null, new ThePropertyBag() { "RenderTarget=HomeCenterStage" });

                var tFL = NMI.AddSmartControl(MyBaseThing, MyGroupForm, eFieldType.TileGroup, MyGroupForm.FldPos, 0, 0, null, null, new nmiCtrlTileGroup { NoTE = true, TileWidth = 18, TileHeight = 9 });
                tFL.RegisterEvent2(eUXEvents.OnShowEditor, (pMsg, para) =>
                {
                    var tMyForm = NMI.GetNMIEditorForm();
                    if (tMyForm != null)
                    {
                        var tThings = TheThingRegistry.GetThingsByFunc("*", s => !string.IsNullOrEmpty($"{s.GetProperty("FacePlateUrl", false)?.GetValue()}"));
                        string tOpt = "";
                        foreach (var tThing in tThings)
                        {
                            if (tOpt.Length > 0)
                                tOpt += ";";
                            tOpt += $"{tThing.FriendlyName}:{tThing.cdeMID}";
                        }
                        NMI.AddSmartControl(MyBaseThing, tMyForm, eFieldType.SmartLabel, 2005, 0xA2, 0x80, "Select Thing to Add", null, new nmiCtrlSmartLabel() { NoTE = true, TileWidth = 5 });
                        NMI.AddSmartControl(MyBaseThing, tMyForm, eFieldType.ComboBox, 2006, 0xA2, 0x80, null, $"newthing", new nmiCtrlComboBox() { Options = tOpt, NoTE = true, TileWidth = 5 });
                        var ttt = NMI.AddSmartControl(MyBaseThing, tMyForm, eFieldType.TileButton, 2010, 2, 0x80, "Add Thing to Screen", null, new nmiCtrlTileButton { NoTE = true, TileWidth = 5, ClassName = "cdeGoodActionButton" });
                        ttt.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "add", (sender, para) =>
                        {
                            var tMsg = para as TheProcessMessage;
                            if (tMsg != null)
                            {
                                var tN = TheThingRegistry.GetThingByMID(CU.CGuid(MyBaseThing.GetProperty("newthing")));
                                TheThingBase tB = tN.GetObject() as TheThingBase;
                                if (tB != null && AddOrUpdateThingInGroup(tB))
                                {
                                    ReloadForm();
                                    TheCommCore.PublishToOriginator(tMsg?.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Control added: {tN.FriendlyName}"));
                                }
                            }
                        });
                        pMsg.Cookie = true;
                    }
                });
                block["NMIEditor"] = tFL;
                mIsUXInitialized = DoCreateUX(block);
            }
            return true;
        }

        /// <summary>
        /// Allows to add new NMI controls to the Group NMI
        /// </summary>
        /// <param name="pUXFlds">Dictionary of Fields that have been added. Add your controls to the dictionary for best practice</param>
        /// <returns>If true, the NMI assumes the Form NMI was completely initialized</returns>
        public virtual bool DoCreateUX(cdeConcurrentDictionary<string, TheMetaDataBase> pUXFlds)
        {
            var tBut = NMI.AddSmartControl(MyBaseThing, MyGroupForm, eFieldType.TileButton, 10020, 2, 0x0, null, null, new nmiCtrlTileButton() { IsAbsolute = true, TileWidth = 1, TileHeight = 1, Left = 0, Top = 0, NoTE = true, ClassName = "enTransBut" });
            tBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "refresh", (sender, pmsg) =>
            {
                ReloadForm();
            });
            pUXFlds["RefreshButton"] = tBut;
            pUXFlds["PropTableGroup"]= NMI.AddSmartControl(MyBaseThing, MyGroupForm, eFieldType.CollapsibleGroup, 10000, 2, 0x80, "All Properties", null, new nmiCtrlCollapsibleGroup { DoClose = true, IsSmall = true, TileWidth = 12 });
            pUXFlds["PropTable"] = NMI.AddSmartControl(MyBaseThing, MyGroupForm, eFieldType.Table, 10010, 8, 0x80, null, "mypropertybag", new nmiCtrlTableView() { TileWidth=12, TileHeight=7, NoTE = true, ParentFld = 10000, ShowFilterField = true });
            //                NMI.AddField(MyGroupForm, new TheFieldInfo() { FldOrder = 10010, DataItem = "mypropertybag", Flags = 8, Type = eFieldType.Table, TileWidth = 12, TileHeight = 7, PropertyBag = new nmiCtrlTableView() { NoTE = true, ParentFld = 4000, ShowFilterField = true } });
            return true;
        }

        /// <summary>
        /// Reloads the Form with updated Dynamic Fields
        /// </summary>
        public virtual void ReloadForm()
        {
            InitDynamicNMI();
            if (MyGroupForm != null)
                TheCommCore.PublishCentral(new TSM(eEngineName.NMIService, $"NMI_REQ_DASH:", $"{TheCommonUtils.cdeGuidToString(MyGroupForm.cdeMID)}:CMyForm:{TheCommonUtils.cdeGuidToString(MyGroupForm.cdeMID)}:{TheCommonUtils.cdeGuidToString(ModelGuid)}:true:true"));
        }

        /// <summary>
        /// Initializes the dynamic part of the form
        /// </summary>
        public virtual void InitDynamicNMI()
        {
            InitGTP();
            NMI.DeleteFieldsByRange(MyGroupForm, 100, 9999);
            if (MyGroupThings?.Any() == true)
            {
                MyGroupForm.FldStart = 100;
                int cnt = 0;
                foreach (var t in MyGroupThings.Values) t?.ShowDeviceFace(MyGroupForm, 78 + (cnt++ * 234), 100);
            }
        }
        #endregion
    }

}
