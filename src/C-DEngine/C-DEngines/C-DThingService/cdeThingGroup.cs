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

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// Thing Groups are collections of Things that can be displayed together in a NMI Screen
    /// PREVIEW: This new Class is still in Preview and might have changes in the API before release
    /// </summary>
    public class TheThingGroup : TheThingBase
    {
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
        public Guid MyScreenGuid { get; protected set; }
        private Guid ModelGuid;
        protected IBaseEngine MyBaseEngine;
        protected TheFormInfo MyGroupForm;
        public bool IsGroupVisible { get; protected set; } = false;

        public ICollection<TheThingBase> GetAllGroupThings() { return MyGroupThings.Values; }

        public void RemoveAllThingsInGroup()
        {
            MyGroupThings.Clear();
        }

        public List<Guid> GetAllGroupThingMIDs()
        {
            return MyGroupThings.Keys.ToList();
        }

        public bool AddOrUpdateThingInGroup(TheThingBase thing)
        {
            if (thing.GetBaseThing() != null)
            {
                MyGroupThings[thing.GetBaseThing().cdeMID] = thing;
                thing.SetProperty("cdeGroupID", MyBaseThing.cdeMID);
                return true;
            }
            return false;
        }

        public bool RemoveThingFromGroup(TheThingBase thing)
        {
            if (thing?.GetBaseThing() != null)
            {
                MyGroupThings.RemoveNoCare(thing.GetBaseThing().cdeMID);
                return true;
            }
            return false;
        }

        public int CountThingsInGroupOfType(Type pType)
        {
            return MyGroupThings.Values.Count(s => s?.GetType() == pType || s?.GetType()?.IsSubclassOf(pType) == true);
        }

        public int CountThingsInGroupOfTypeByFunc(Type pType, Func<TheThingBase, bool> mF)
        {
            return MyGroupThings.Values.Count(s => s?.GetType() == pType || s?.GetType()?.IsSubclassOf(pType) == true && mF(s));
        }
        public List<TheThingBase> GetThingsOfType(Type pType)
        {
            return MyGroupThings.Values.Where(s => s?.GetType() == pType || s?.GetType()?.IsSubclassOf(pType) == true).ToList();
        }

        public List<TheThingBase> GetThingsOfTypeByFunc(Type pType, Func<TheThingBase, bool> mF)
        {
            return MyGroupThings.Values.Where(s => s?.GetType() == pType || s?.GetType()?.IsSubclassOf(pType) == true && mF(s)).ToList();
        }

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
        public override bool CreateUX()
        {
            if (!mIsUXInitCalled)
            {
                mIsUXInitCalled = true;

                cdeConcurrentDictionary<string, TheMetaDataBase> block = new();
                MyGroupForm = new(MyScreenGuid, eEngineName.NMIService, null, $"TheThing;:;0;:;True;:;cdeMID={MyBaseThing.cdeMID}")
                {
                    DefaultView = eDefaultView.Form,
                    PropertyBag = new nmiCtrlFormView { TileWidth = 24, FitToScreen = true, HideCaption = true, UseMargin = false, InnerClassName = "enFormInner" },
                    ModelID = "StandardForm"
                };
                MyGroupForm.RegisterEvent2(eUXEvents.OnBeforeMeta, (pmsg, sender) =>
                {
                    if (!IsGroupVisible)
                        InitDynamicNMI();
                    IsGroupVisible = true;
                });
                block["Form"] = MyGroupForm;
                block["DashIcon"] = TheNMIEngine.AddFormToThingUX(MyBaseThing, MyGroupForm, "CMyForm", MyBaseThing.FriendlyName, 1, 3, 0, "..Overview", null, new ThePropertyBag() { "RenderTarget=HomeCenterStage" });
                mIsUXInitialized = DoCreateUX(block);
            }
            return true;
        }

        public virtual bool DoCreateUX(cdeConcurrentDictionary<string, TheMetaDataBase> pUXFlds)
        {
            var tBut = TheNMIEngine.AddSmartControl(MyBaseThing, MyGroupForm, eFieldType.TileButton, 10020, 2, 0x0, null, null, new nmiCtrlTileButton() { IsAbsolute = true, TileWidth = 1, TileHeight = 1, Left = 0, Top = 0, NoTE = true, ClassName = "enTransBut" });
            tBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "refresh", (sender, pmsg) =>
            {
                ReloadForm();
            });
            pUXFlds["RefreshButton"] = tBut;
            pUXFlds["PropTableGroup"]= TheNMIEngine.AddSmartControl(MyBaseThing, MyGroupForm, eFieldType.CollapsibleGroup, 10000, 2, 0x80, "All Properties", null, new nmiCtrlCollapsibleGroup { DoClose = true, IsSmall = true, TileWidth = 12 });
            pUXFlds["PropTable"] = TheNMIEngine.AddSmartControl(MyBaseThing, MyGroupForm, eFieldType.Table, 10010, 8, 0x80, null, "mypropertybag", new nmiCtrlTableView() { TileWidth=12, TileHeight=7, NoTE = true, ParentFld = 10000, ShowFilterField = true });
//                TheNMIEngine.AddField(MyGroupForm, new TheFieldInfo() { FldOrder = 10010, DataItem = "mypropertybag", Flags = 8, Type = eFieldType.Table, TileWidth = 12, TileHeight = 7, PropertyBag = new nmiCtrlTableView() { NoTE = true, ParentFld = 4000, ShowFilterField = true } });
            return true;
        }
        public void ReloadForm()
        {
            InitDynamicNMI();
            if (MyGroupForm != null)
                TheCommCore.PublishCentral(new TSM(eEngineName.NMIService, $"NMI_REQ_DASH:", $"{TheCommonUtils.cdeGuidToString(MyGroupForm.cdeMID)}:CMyForm:{TheCommonUtils.cdeGuidToString(MyGroupForm.cdeMID)}:{TheCommonUtils.cdeGuidToString(ModelGuid)}:true:true"));
        }

        public virtual void InitDynamicNMI()
        {
        }
        #endregion
    }

}
