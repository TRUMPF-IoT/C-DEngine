// SPDX-FileCopyrightText: Copyright (c) 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

#pragma warning disable CS1591    //TODO: Remove and document public methods

namespace nsCDEngine.Engines.NMIService
{
    public partial class TheNMIEngine
    {
        internal static readonly Guid eNMIPortalDashboard = new("{E7DA71A1-496F-4B15-A8AB-969526341C7B}");
        internal static readonly Guid eNMIDashboard = new("{FAFA22FF-96AC-42CF-B1DB-7C073053FC39}");
        internal static readonly Guid eActivationAndStatusDashGuid = new("{1CF9A525-0126-4189-AF41-18C3609E5743}");

        #region SmartControl / TheFieldInfo related methods (HTML free)

        /// <summary>
        /// Adds a list of TheFieldInfo to a TheFormInfo (adds fields to a form)
        /// </summary>
        /// <param name="pForm"></param>
        /// <param name="pFieldInfos"></param>
        /// <returns></returns>
        public static bool AddFields(TheFormInfo pForm, List<TheFieldInfo> pFieldInfos)
        {
            if (!IsInitialized() || pFieldInfos == null || pForm == null || pForm.cdeMID == Guid.Empty) return false;
            bool bSuccess = false;
            foreach (TheFieldInfo pInfo in pFieldInfos)
            {
                if (AddField(pForm, pInfo))
                    bSuccess = true;
            }
            return bSuccess;
        }

        /// <summary>
        /// Adds a single field to a form and sets the FormID of the field to the Form.cdeMID
        /// </summary>
        /// <param name="pForm"></param>
        /// <param name="pFieldInfo"></param>
        /// <returns></returns>
        public static bool AddField(TheFormInfo pForm, TheFieldInfo pFieldInfo)
        {
            if (!IsInitialized() || pFieldInfo == null || pForm == null || pForm.cdeMID == Guid.Empty) return false;
            if (TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache.ContainsID(pFieldInfo.cdeMID)) return false;
            if (TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache.ContainsByFunc(s => s.FldOrder == pFieldInfo.FldOrder && s.FormID == pForm.cdeMID)) return false;
            pFieldInfo.FormID = pForm.cdeMID;
            TheCDEngines.MyNMIService.MyNMIModel.MyFields.AddAnItem(pFieldInfo);
            return true;
        }

        /// <summary>
        /// Returns a list of Fields by a given function
        /// </summary>
        /// <param name="pSelector"></param>
        /// <returns></returns>
        public static List<TheFieldInfo> GetFieldsByFunc(Func<TheFieldInfo, bool> pSelector)
        {
            if (!IsInitialized() || pSelector == null ||
                TheCDEngines.MyNMIService.MyNMIModel.MyFields == null || TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache == null)
                return new List<TheFieldInfo>();
            return TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache.GetEntriesByFunc(pSelector);
        }

        /// <summary>
        /// Returns a specific field of a form by its FldOrder
        /// </summary>
        /// <param name="pForm"></param>
        /// <param name="pFldOrder"></param>
        /// <returns></returns>
        public static TheFieldInfo GetFieldByFldOrder(TheFormInfo pForm, int pFldOrder)
        {
            if (!IsInitialized() || pForm == null ||
                TheCDEngines.MyNMIService.MyNMIModel.MyFields == null || TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache == null)
                return null;
            return TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache.GetEntryByFunc(s => s.FormID == pForm.cdeMID && s.FldOrder == pFldOrder);
        }


        /// <summary>
        /// Adds a spacer to a Form to create padding/margin in forms
        /// </summary>
        /// <param name="pBaseThing">Owning Thing</param>
        /// <param name="pForm">Form to insert the spacer in</param>
        /// <param name="pFldOrder">Fld Order of the spacer</param>
        /// <param name="width">Width in Tiles - TileFactorX default is 2. Resulting spacer default is TileSize/2 (39px)</param>
        /// <param name="height">Height in Tiles - TileFactorY default is 2. Resulting spacer default is TileSize/2 (39px)</param>
        /// <param name="pPropertyBag">Additional Propertybag for the spacer</param>
        /// <returns></returns>
        public static TheFieldInfo AddSpacer(TheThing pBaseThing, TheFormInfo pForm, int pFldOrder, int width=1, int height=1, ThePropertyBag pPropertyBag=null)
        {
            var fld = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, pFldOrder, 0, 0, null, null, new nmiCtrlTileGroup { TileWidth = width, TileHeight = height, TileFactorX=2, TileFactorY=2 });
            fld.PropertyBag = pPropertyBag;
            return fld;
        }

        /// <summary>
        /// Adds a Group for Controls
        /// </summary>
        /// <param name="pBaseThing">Owner Thing</param>
        /// <param name="pForm">Form to insert the group in</param>
        /// <param name="pFldOrder">ld Order of the group. Use this as the ParentFld for controls that need to go into the group</param>
        /// <param name="width">Tilewidth of the Group</param>
        /// <param name="height">TileHeight of the Group. If not set, the group has a dynamic height</param>
        /// <param name="pParent">ParentFld for the Group. If not set, the group has no parent</param>
        /// <returns></returns>
        public static TheFieldInfo AddGroup(TheThing pBaseThing, TheFormInfo pForm,int pFldOrder,int width, int height=0, int pParent=0)
        {
            var tT = new nmiCtrlTileGroup { TileWidth = width, TileHeight = height };
            if (pParent > 0)
                tT.ParentFld = pParent;
            if (height > 0)
                tT.TileHeight = height;
            return AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, pFldOrder, 0, 0, null, null, tT);
        }

        /// <summary>
        /// Adds a smart Icon to a Form
        /// </summary>
        /// <param name="pBaseThing">BaseThing</param>
        /// <param name="pMyForm">Target Form</param>
        /// <param name="pFldOrder">Start Field Order</param>
        /// <param name="pParentFld">Parent Field</param>
        /// <param name="pTileHeight">Height of the icon</param>
        /// <param name="pBackIcon">Front Icon...should be FontAwesome or similar</param>
        /// <param name="pFrontIcon">Back Icon...not needed if Front Icon is opqaue</param>
        /// <param name="pDefaultColor">Base Color for the back icon. Set to transparent if not set</param>
        /// <returns>List of TheFieldInfo: Group, InnerGroup, BackIcon and FrontIcon</returns>
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddSmartIcon(TheThing pBaseThing, TheFormInfo pMyForm, int pFldOrder, int pParentFld, int pTileHeight, string pBackIcon, string pFrontIcon, string pDefaultColor = "transparent")
        {
            cdeConcurrentDictionary<string, TheFieldInfo> tFlds = new ();

            if (string.IsNullOrEmpty(pDefaultColor))
                pDefaultColor = "gray";
            tFlds["Group"] = TheNMIEngine.AddSmartControl(pBaseThing, pMyForm, eFieldType.TileGroup, pFldOrder, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = pParentFld, TileHeight = pTileHeight });
            tFlds["InnerGroup"] = TheNMIEngine.AddSmartControl(pBaseThing, pMyForm, eFieldType.TileGroup, pFldOrder + 1, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = pFldOrder, TileHeight = pTileHeight });
            tFlds["BackIcon"] = TheNMIEngine.AddSmartControl(pBaseThing, pMyForm, eFieldType.SmartLabel, pFldOrder + 2, 256, 0, null, null, new nmiCtrlSmartLabel { Value = pBackIcon, FontSize = (56 * pTileHeight), Foreground = pDefaultColor, ParentFld = pFldOrder + 1, TileHeight = pTileHeight, NoTE = true });
            tFlds["FrontIcon"] = TheNMIEngine.AddSmartControl(pBaseThing, pMyForm, eFieldType.SmartLabel, pFldOrder + 3, 256, 0, null, null, new nmiCtrlSmartLabel { Value = pFrontIcon, IsAbsolute = true, FontSize = (56 * pTileHeight), Foreground = "black", ParentFld = pFldOrder + 1, TileHeight = pTileHeight, NoTE = true });
            return tFlds;
        }

        /// <summary>
        /// Adds a control to a Form
        /// </summary>
        /// <param name="MyBaseThing"></param>
        /// <param name="pMyForm"></param>
        /// <param name="tType"></param>
        /// <param name="fldOrder"></param>
        /// <param name="flags">
        /// Binary Flags for the Field:
        /// 1= Show content as * (Password);
        /// 2= Write Enabled Field (Sends update to Plugin)
        /// 4 =Hide from Mobile (RETIRED: Use AddPlatBag(eWebPlatform.Mobile,new nmiPlatBag { Hide=true });
        /// 8 =Hide From Table (RETIRED: Use AddPlatBag(eWebPlatform.Mobile,new nmiPlatBag { HideInTable=true });
        /// 16=Hide From Form (RETIRED: Use AddPlatBag(eWebPlatform.Mobile,new nmiPlatBag { HideInForm=true });
        /// 32=Advanced Editior
        /// 64=The OnUpdateName is not on the MyBaseThing but on a Thing referenced by the cdeMID in the records of the corresponding MirrorCache Data Table
        /// 128=Do not show this control after second node (i.e. via Cloud)
        /// 256=Allow HTML5 Markup in PropertyBag and Value
        /// </param>
        /// <param name="pACL">User Access Level of the control - only users with a matching level can see this control</param>
        /// <param name="pHeader">Title/Label of the Control</param>
        /// <param name="OnUpdateName">Property to wach for changes. The content of the property will be displayed in this control in the NMI</param>
        /// <returns></returns>
        public static TheFieldInfo AddSmartControl(TheThing MyBaseThing, TheFormInfo pMyForm, eFieldType tType, int fldOrder, int flags, int pACL, string pHeader, string OnUpdateName)
        {
            return AddSmartControl(MyBaseThing, pMyForm, tType, fldOrder, flags, pACL, pHeader, false, OnUpdateName, null, null);
        }
        /// <summary>
        /// Adds a control to a Form
        /// </summary>
        /// <param name="MyBaseThing"></param>
        /// <param name="pMyForm"></param>
        /// <param name="tType"></param>
        /// <param name="fldOrder"></param>
        /// <param name="flags">
        /// Binary Flags for the Field:
        /// 1= Show content as * (Password);
        /// 2= Write Enabled Field (Sends update to Plugin)
        /// 4 =Hide from Mobile (RETIRED: Use AddPlatBag(eWebPlatform.Mobile,new nmiPlatBag { Hide=true });
        /// 8 =Hide From Table (RETIRED: Use AddPlatBag(eWebPlatform.Mobile,new nmiPlatBag { HideInTable=true });
        /// 16=Hide From Form (RETIRED: Use AddPlatBag(eWebPlatform.Mobile,new nmiPlatBag { HideInForm=true });
        /// 32=Advanced Editior
        /// 64=The OnUpdateName is not on the MyBaseThing but on a Thing referenced by the cdeMID in the records of the corresponding MirrorCache Data Table
        /// 128=Do not show this control after second node (i.e. via Cloud)
        /// 256=Allow HTML5 Markup in PropertyBag and Value
        /// </param>
        /// <param name="pACL">User Access Level of the control - only users with a matching level can see this control</param>
        /// <param name="pHeader">Title/Label of the Control</param>
        /// <param name="OnUpdateName">Property to wach for changes. The content of the property will be displayed in this control in the NMI</param>
        /// <param name="BagItems">List of Properties to be set on the control</param>
        /// <returns></returns>
        public static TheFieldInfo AddSmartControl(TheThing MyBaseThing, TheFormInfo pMyForm, eFieldType tType, int fldOrder, int flags, int pACL, string pHeader, string OnUpdateName, ThePropertyBag BagItems)
        {
            return AddSmartControl(MyBaseThing, pMyForm, tType, fldOrder, flags, pACL, pHeader, false, OnUpdateName, null, BagItems);
        }
        /// <summary>
        /// Adds a control to a Form
        /// </summary>
        /// <param name="MyBaseThing"></param>
        /// <param name="pMyForm"></param>
        /// <param name="tType"></param>
        /// <param name="fldOrder"></param>
        /// <param name="flags">
        /// Binary Flags for the Field:
        /// 1= Show content as * (Password);
        /// 2= Write Enabled Field (Sends update to Plugin)
        /// 4 =Hide from Mobile (RETIRED: Use AddPlatBag(eWebPlatform.Mobile,new nmiPlatBag { Hide=true });
        /// 8 =Hide From Table (RETIRED: Use AddPlatBag(eWebPlatform.Mobile,new nmiPlatBag { HideInTable=true });
        /// 16=Hide From Form (RETIRED: Use AddPlatBag(eWebPlatform.Mobile,new nmiPlatBag { HideInForm=true });
        /// 32=Advanced Editior
        /// 64=The OnUpdateName is not on the MyBaseThing but on a Thing referenced by the cdeMID in the records of the corresponding MirrorCache Data Table
        /// 128=Do not show this control after second node (i.e. via Cloud)
        /// 256=Allow HTML5 Markup in PropertyBag and Value
        /// </param>
        /// <param name="pACL">User Access Level of the control - only users with a matching level can see this control</param>
        /// <param name="pHeader">Title/Label of the Control</param>
        /// <param name="RedOnFalse">NO LONGER SUPPORTED:Please use SetUXProperty instead. OLD: If the content of the Property in the OnUpdateName is true, the background color of this control will be set to green, else the background will be set to red</param>
        /// <param name="OnUpdateName">Property to watch for changes. The content of the property will be displayed in this control in the NMI</param>
        /// <param name="OnUpdateCallback">Fires when the property in the OnUpdateName changes</param>
        /// <param name="BagItems">List of Properties to be set on the control</param>
        /// <returns></returns>
        public static TheFieldInfo AddSmartControl(TheThing MyBaseThing, TheFormInfo pMyForm, eFieldType tType, int fldOrder, int flags, int pACL, string pHeader, bool RedOnFalse, string OnUpdateName, Action<TheFieldInfo, cdeP> OnUpdateCallback, ThePropertyBag BagItems)
        {
            if (pMyForm == null) return null;
            string tDefaultValue = "{0}";
            cdeP tProp = null;
            if (!string.IsNullOrEmpty(OnUpdateName) && (flags & 0x42) == 2 && tType != eFieldType.Table)
            {
                if (pMyForm.DefaultView == 0 || MyBaseThing == null)
                {
                    //flags |= 64; //Needs to be set manually if a column should auto-update
                    tDefaultValue = "";
                }
                else
                {
                    tProp = MyBaseThing.GetProperty(OnUpdateName, true);
                    //tProp.SetPublication(true, Guid.Empty); //4.1: Moved to Late Binding in TheFormGenerator.GetPermittedFields
                    if (tProp.Value == null)
                        tDefaultValue = string.Format(tDefaultValue, "");
                    else
                        tDefaultValue = string.Format(tDefaultValue, tProp.Value);
                }
            }
            else
                tDefaultValue = "";
            if (pACL < 0 && MyBaseThing != null)
                pACL = MyBaseThing.cdeA;
            TheFieldInfo tFldInfo = new ()
            {
                cdeO = MyBaseThing != null ? MyBaseThing.cdeMID : pMyForm.cdeMID,
                cdeA = pACL,
                FldOrder = fldOrder,
                Type = tType,
                Flags = flags,
                PropertyBag = new ThePropertyBag(),
                Header = pHeader
            };
            var tMID = ThePropertyBag.PropBagGetValue(BagItems, "MID", "=");
            if (!string.IsNullOrEmpty(tMID))
            {
                tFldInfo.cdeMID = TheCommonUtils.CGuid(tMID);
            }
            if ((flags & 4) != 0)
                tFldInfo.AddOrUpdatePlatformBag(eWebPlatform.Mobile, new nmiPlatBag { Hide = true });
            if ((flags & 8) != 0)
                tFldInfo.AddOrUpdatePlatformBag(eWebPlatform.Any, new nmiPlatBag { HideInTable = true });
            if ((flags & 16) != 0)
                tFldInfo.AddOrUpdatePlatformBag(eWebPlatform.Any, new nmiPlatBag { HideInForm = true });
            if (!string.IsNullOrEmpty(tDefaultValue))
                ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, "DefaultValue", "=", tDefaultValue); 

            if (tType == eFieldType.UserControl && MyBaseThing != null)
            {
                if (!ThePropertyBag.PropBagHasValue(BagItems, "EngineName", "="))
                    ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, "EngineName", "=", MyBaseThing.EngineName);
                if (tProp != null && TheNMIEngine.IsModelReady)
                    TheFormsGenerator.RegisterFieldEvents(MyBaseThing, tProp, true, tFldInfo, ThePropertyBag.PropBagGetValue(BagItems, "ControlType", "="));
            }
            if (tType == eFieldType.SmartLabel)
                ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, "ClassName", "=", "ctrlSmartLabel");
            if (!string.IsNullOrEmpty(OnUpdateName))
            {
                if (tType == eFieldType.Table)
                    tFldInfo.DataItem = OnUpdateName;
                else
                {
                    if (pMyForm.defDataSource?.StartsWith("TheThing;:;")==true)
                        tFldInfo.DataItem = "MyPropertyBag." + OnUpdateName + ".Value";
                    else
                        tFldInfo.DataItem = OnUpdateName;
                    if ((flags & 64) != 0)
                        ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, "OnThingEvent", "=", string.Format("%cdeMID%;{0}", OnUpdateName));
                    else
                    {
                        if (pMyForm.DefaultView == eDefaultView.Form && MyBaseThing!=null)
                        {
                            ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, "OnThingEvent", "=", string.Format("{0};{1}", MyBaseThing.cdeMID, OnUpdateName));
                            tProp ??= MyBaseThing.GetProperty(OnUpdateName, true);
                            if ((tProp.cdeE & 0x40) != 0)
                                tProp.RegisterEvent(eThingEvents.PropertyChanged, tFldInfo.sinkUpdate);
                            if ((flags & 1) != 0 || tType == eFieldType.Password)   //Automatically encrypt all UX Elements that are flaged with FieldType Passwword or have the Flag=1 on.
                            {
                                tProp.cdeE |= 1;
                                flags |= 1;
                            }
                            tProp.RegisterEvent(eThingEvents.PropertyChangedByUX, tFldInfo.sinkUXUpdate);
                            if (OnUpdateCallback != null)
                            {
                                tFldInfo.RegisterPropertyChangedByUX(OnUpdateCallback);
                            }
                        }
                    }
                }
            }
            if (BagItems != null && BagItems.Count > 0)
                tFldInfo.PropertyBag.AddRange(BagItems);
            //NEW 4.0080 Databinding to UX Properties
            if (MyBaseThing != null)
            {
                foreach (var t in tFldInfo.PropertyBag.ToList())
                {
                    var ts = t.Split('=');
                    if (ts.Length > 1 && ts[1].StartsWith("$"))
                    {
                        if (ts[1].Length == 2 && ts[1].EndsWith("$"))
                        {
                            ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, ts[0], "=", "$");
                            continue;
                        }
                        var tPN = ts[1].Substring(1);
                        MyBaseThing.GetProperty(tPN, true).RegisterEvent(eThingEvents.PropertyChanged, (p) =>
                        {
                            if (p != null)
                            {
                                var pSafe = p?.ToString();
                                if ((flags & 256) == 0)
                                    pSafe = TheCommonUtils.cdeStripHTML(pSafe);
                                tFldInfo.SetUXProperty(Guid.Empty, $"{ts[0]}={pSafe}");
                            }
                        });
                    }
                }
            }
            ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, "UXID", "=", tFldInfo.cdeMID.ToString());

            if (AddField(pMyForm, tFldInfo))
                return tFldInfo;
            else
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2345, new TSM(eEngineName.NMIService, string.Format("SmartControl (header=\"{0}\") of {2}-{3} could not be added, possible FldOrder({1}) conflict? ", pHeader, fldOrder, MyBaseThing?.EngineName, MyBaseThing?.FriendlyName), eMsgLevel.l1_Error));
                return null;
            }
        }

        internal static void RegisterAllFieldEvents()
        {
            var tList =TheNMIEngine.GetFieldsByFunc(s => s.Type == eFieldType.UserControl);
            foreach (var tEle in tList)
            {
                TheThing tThing = TheThingRegistry.GetThingByMID(tEle.cdeO, true);
                if (tThing != null)
                {
                    cdeP tP = null;
                    if (!string.IsNullOrEmpty(tEle.DataItem))
                    {
                        if (tEle.DataItem.StartsWith("MyPropertyBag."))
                        {
                            var ts = tEle.DataItem.Split('.');
                            if (ts.Length>1)
                                tP = tThing.GetProperty(ts[1]);
                        }
                        else
                            tP = tThing.GetProperty(tEle.DataItem);
                    }
                    if (tP!=null)
                        TheFormsGenerator.RegisterFieldEvents(tThing, tP, true, tEle, ThePropertyBag.PropBagGetValue(tEle.PropertyBag, "ControlType", "="));
                }
            }
        }

        /// <summary>
        /// Returns a field by a given Field ID
        /// </summary>
        /// <param name="pFieldGuid"></param>
        /// <returns></returns>
        public static TheFieldInfo GetFieldById(Guid pFieldGuid)
        {
            if (!IsInitialized() || pFieldGuid == Guid.Empty ||
                TheCDEngines.MyNMIService.MyNMIModel.MyFields == null || TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache == null)
                return null;
            if (TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache.ContainsID(pFieldGuid))
                return TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache.GetEntryByID(pFieldGuid);
            return null;
        }

        internal static List<TheFieldInfo> GetFieldsByOwner(Guid pFieldGuid)
        {
            if (!IsInitialized() || pFieldGuid == Guid.Empty ||
                TheCDEngines.MyNMIService.MyNMIModel.MyFields == null || TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache == null)
                return new List<TheFieldInfo>();
            return TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache.GetEntriesByFunc(s => s.cdeO == pFieldGuid);
        }

        /// <summary>
        /// Deletes a Field by a given Fld ID
        /// </summary>
        /// <param name="pGuid"></param>
        /// <returns></returns>
        public static bool DeleteFieldById(Guid pGuid)
        {
            if (!IsInitialized() || pGuid == Guid.Empty ||
                TheCDEngines.MyNMIService.MyNMIModel.MyFields == null || TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache == null)
                return false;
            TheCDEngines.MyNMIService.MyNMIModel.MyFields.RemoveAnItemByID(pGuid, null);
            return true;
        }
        #endregion

        #region FacePlates (HTML free)
        /// <summary>
        /// Parses a HTML5 FacePlate for Property References
        /// </summary>
        /// <param name="pBaseThing">TheThing that owns the FacePlace</param>
        /// <param name="pFaceplateURL">URL To the FacePlate</param>
        public static string ParseFacePlateUrl(TheThing pBaseThing, string pFaceplateURL)
        {
            return ParseFacePlateUrl(pBaseThing, pFaceplateURL, false, Guid.Empty);
        }
        /// <summary>
        /// Parses a HTML5 FacePlate for Property References
        /// </summary>
        /// <param name="pBaseThing">TheThing that owns the FacePlace</param>
        /// <param name="pFaceplateURL">URL To the FacePlate</param>
        /// <param name="pUrlIsHTML"></param>
        public static string ParseFacePlateUrl(TheThing pBaseThing, string pFaceplateURL, bool pUrlIsHTML)
        {
            return ParseFacePlateUrl(pBaseThing, pFaceplateURL, pUrlIsHTML, Guid.Empty);
        }

        /// <summary>
        /// Parses a HTML5 FacePlate for Property References
        /// </summary>
        /// <param name="pBaseThing">TheThing that owns the FacePlace</param>
        /// <param name="pFaceplateURL">URL To the FacePlate</param>
        /// <param name="pUrlIsHTML"></param>
        /// <param name="pRequestingNode">NodeID requesting the parsing. No longer needed as this is called at late binding</param>
        public static string ParseFacePlateUrl(TheThing pBaseThing, string pFaceplateURL, bool pUrlIsHTML, Guid pRequestingNode)
        {
            if (pBaseThing == null || string.IsNullOrEmpty(pFaceplateURL)) return pFaceplateURL;
            IBaseEngine tBase = pBaseThing.GetBaseEngine();
            if (tBase != null)
            {
                TheRequestData tData = new () { cdeRealPage = pFaceplateURL };
                if (!pUrlIsHTML)
                {
                    if (tBase.GetPluginResource(tData))
                        pFaceplateURL = TheCommonUtils.CArray2UTF8String(tData.ResponseBuffer);
                    else
                    {
                        byte[] tRes = TheCommonUtils.GetSystemResource(null, pFaceplateURL);
                        if (tRes != null)
                        {
                            pFaceplateURL = TheCommonUtils.CArray2UTF8String(tRes);
                        }
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(2343,TSM.L(eDEBUG_LEVELS.ESSENTIALS)?null: new TSM(eEngineName.NMIService, $"HTML Url {pFaceplateURL} of FacePlate not found on Relay!", eMsgLevel.l1_Error));
                            return pFaceplateURL;
                        }
                    }
                }
            }
            return pFaceplateURL;
        }

        internal static bool ParseFacePlateUrlInternal(TheThing pBaseThing, string pFaceplateURL, bool pUrlIsHTML, Guid pRequestingNode)
        {
            if (pBaseThing == null || string.IsNullOrEmpty(pFaceplateURL)) return false;
            IBaseEngine tBase = pBaseThing.GetBaseEngine();
            if (tBase != null)
            {
                TheRequestData tData = new () { cdeRealPage = pFaceplateURL };
                if (!pUrlIsHTML)
                {
                    if (tBase.GetPluginResource(tData))
                        pFaceplateURL = TheCommonUtils.CArray2UTF8String(tData.ResponseBuffer);
                    else
                    {
                        byte[] tRes = TheCommonUtils.GetSystemResource(null, pFaceplateURL);
                        if (tRes != null)
                        {
                            pFaceplateURL = TheCommonUtils.CArray2UTF8String(tRes);
                        }
                        else
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(2343, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.NMIService, $"HTML Url {pFaceplateURL} of FacePlate not found on Relay!", eMsgLevel.l1_Error));
                            return false;
                        }
                    }
                }
                ParseFacePlateInternal(pBaseThing, pFaceplateURL, pRequestingNode);
                return true;
            }
            return false;
        }



        private static void RegisterPublication(TheThing tThing, string pDataItem, Guid pRequestingNode)
        {
            if (tThing != null && !string.IsNullOrEmpty(pDataItem))
            {
                TheFormsGenerator.RegisterNewNMINode(pRequestingNode, Guid.Empty, tThing.cdeMID, pDataItem, true);
                var OnUpdateName = pDataItem;
                if (OnUpdateName.StartsWith("MyPropertyBag."))  
                    OnUpdateName = OnUpdateName.Split('.')[1];
                tThing.GetProperty(OnUpdateName, true).SetPublication(true, pRequestingNode); //Guid.Empty uses PublishCentral - a specific node would use SYSTEMWIDE
            }
        }

        private static List<string> FaceMacros = null;
        internal static string ParseFacePlateInternal(TheThing pBaseThing, string pHTML, Guid pRequestingNode)
        {
            //4.1: Only called from TheFormGenerator.GetPermittedFields
            if (pBaseThing == null || string.IsNullOrEmpty(pHTML)) return pHTML;
            if (FaceMacros == null)
            {
                var tFaceMacros = TheBaseAssets.MySettings.GetSetting("FaceMacros");
                try
                {
                    FaceMacros = TheCommonUtils.CStringToList(tFaceMacros, ',');
                }
                catch (Exception)
                {
                    //intended
                }
                if (!(FaceMacros?.Count > 0))
                    FaceMacros=new List<string> { "<%C20:", "<%C12:", "<%C21:", "<%V:", "<%S:", "<%C:", "<%I:" };
            }
            foreach (var tM in FaceMacros)
            {
                int posEnd = 0;
                while (posEnd >= 0)
                {
                    string tname = TheCommonUtils.GetStringSection(pHTML, ref posEnd, tM, "%>", false);
                    if (tname == null)
                        continue;
                    var tParts=tname?.Split(':');
                    if (tParts?.Length>1)
                        tname = tParts[1];
                    RegisterPublication(pBaseThing, tname, pRequestingNode);
                }
            }
            return pHTML;
        }

        #endregion

        #region Dashboard, Forms and Dashpanels (HTML free)
        /// <summary>
        /// Refreshes the specified dashboard.
        /// If pMessage is set, the dashboard is sent to the originator in the TSM.
        /// If pMessage is null, a Request to refresh is sent to the clients. They can then request the Dashboard but only if they have the premission to see the specified dashboard
        /// </summary>
        /// <param name="pMessage">Originator Message</param>
        /// <param name="pDashboard">Dashboard to be refreshed</param>
        /// <param name="ForceReload">If set to true, the dashboard will overwrite the existing dashboard -NOT merge with the existing. This can be trouble with Agents</param>
        /// <returns></returns>
        public static bool RefreshDashboard(TheProcessMessage pMessage, string pDashboard, bool ForceReload)
        {
            if (string.IsNullOrEmpty(pDashboard)) return false;
            if (pMessage == null || pMessage.CurrentUserID == Guid.Empty)
            {
                TheCommCore.PublishCentral(new TSM(eEngineName.NMIService, "NMI_REQ_DASH:" + TheCommonUtils.GenerateFinalStr(pDashboard), string.Format("{0}:CMyDashboard:{0}:{1}", TheCommonUtils.GenerateFinalStr(pDashboard), "")));
                return false;
            }
            else
                return SendNMIData(pMessage.Message, TheCommonUtils.GetClientInfo(pMessage), TheCommonUtils.GenerateFinalStr(pDashboard), "CMyDashboard", TheCommonUtils.GenerateFinalStr(pDashboard), "", ForceReload, false);
        }

        /// <summary>
        /// Returns TheFormInfo of the NMI Editor if it does exist
        /// </summary>
        /// <returns></returns>
        public static TheFormInfo GetNMIEditorForm()
        {
            var tFormID = TheCommonUtils.CGuid(TheCDEngines.MyNMIService?.GetProperty("NMIEditorFormID", false));
            if (tFormID == Guid.Empty)
                return null;
            return GetFormById(tFormID);
        }

        /// <summary>
        /// Returns TheThing of the NMI Editor
        /// </summary>
        /// <returns></returns>
        public static TheThing GetNMIEditorThing()
        {
            var tFormID = TheCommonUtils.CGuid(TheCDEngines.MyNMIService?.GetProperty("NMIEditorFormID", false));
            if (tFormID == Guid.Empty)
                return null;
            var form=GetFormById(tFormID);
            return TheThingRegistry.GetThingByMID(form.cdeO);
        }

        /// <summary>
        /// Reloads the content of the NMI Editor
        /// </summary>
        public static void ReloadNMIEditor()
        {
            var tFormID = TheCommonUtils.CGuid(TheCDEngines.MyNMIService?.GetProperty("NMIEditorFormID", false));
            if (tFormID == Guid.Empty)
                return;
            TheCommCore.PublishCentral(new TSM(eEngineName.NMIService, $"NMI_REQ_DASH:",
    $"{TheCommonUtils.cdeGuidToString(tFormID)}:CMyForm:{TheCommonUtils.cdeGuidToString(tFormID)}:{TheCommonUtils.cdeGuidToString(TheNMIEngine.eNMIDashboard)}:true:true"));
        }


        /// <summary>
        /// Adds a new NMI Screen to a Thing that allows adding/removing and changing of FacePlates from other Things
        /// PREVIEW! This API is still in Preview and could change until final publication
        /// </summary>
        /// <param name="pBaseThing">Owner Thing</param>
        /// <param name="pFormGuid">Target Form MID</param>
        /// <param name="XL">Width in Tiles</param>
        /// <param name="FormModelID">ModelID for the Form</param>
        /// <param name="beforeMetaLoad">Optional Event fired when the Form is loaded</param>
        /// <param name="pBag">Optional PropertyBag</param>
        /// <returns></returns>
        public static Dictionary<string, TheMetaDataBase> AddFlexibleNMIScreen(TheThing pBaseThing, Guid pFormGuid, int XL, string FormModelID, Action<TheFormInfo> beforeMetaLoad = null, ThePropertyBag pBag = null)
        {
            var tFlds = new Dictionary<string, TheMetaDataBase>();
            var MyNMIForm = new TheFormInfo(pFormGuid, eEngineName.NMIService, null, $"TheThing;:;0;:;True;:;cdeMID={pBaseThing.cdeMID}")
            {
                DefaultView = eDefaultView.Form,
                PropertyBag = new nmiCtrlFormView { TileWidth = XL, FitToScreen = true, HideCaption = true, UseMargin = false },
                ModelID = FormModelID
            };
            if (pBag != null)
                MyNMIForm.PropertyBag = pBag;
            TheThing.SetSafePropertyBool(pBaseThing, $"Form_{pFormGuid}_IsVisible", false);
            MyNMIForm.RegisterEvent2(eUXEvents.OnBeforeMeta, (pmsg, sender) =>
            {
                if (!TheThing.GetSafePropertyBool(pBaseThing, $"Form_{pFormGuid}_IsVisible"))
                {
                    lock (MyNMIForm.UpdateLock)
                    {
                        AddFlexibleNMIControls(pBaseThing, pFormGuid, MyNMIForm);
                        beforeMetaLoad?.Invoke(null);
                        RefreshFlexibleNMIScreen(pBaseThing, MyNMIForm);
                        TheThing.SetSafePropertyBool(pBaseThing, $"Form_{pFormGuid}_IsVisible", true);
                    }
                }
            });
            tFlds.Add("Form", MyNMIForm);
            tFlds.Add("DashIcon", AddFormToThingUX(pBaseThing, MyNMIForm, "CMyForm", $"{pBaseThing.FriendlyName}", 1, 3, 0, $"{pFormGuid}", null, new ThePropertyBag() { "RenderTarget=HomeCenterStage" }));
            return tFlds;
        }

        /// <summary>
        /// Requests a reload of the Flexible NMI Screen
        /// </summary>
        /// <param name="pBaseThing">Owner Thing</param>
        /// <param name="MyNMIForm">The Flexible NMI Screen to refresh</param>
        public static bool RefreshFlexibleNMIScreen(TheThing pBaseThing, TheFormInfo MyNMIForm)
        {
            if (MyNMIForm != null && pBaseThing!=null)
            {
                Guid ModelGuid = pBaseThing.GetBaseEngine().GetEngineID();
                TheCommCore.PublishCentral(new TSM(eEngineName.NMIService, $"NMI_REQ_DASH:", $"{TheCommonUtils.cdeGuidToString(MyNMIForm.cdeMID)}:CMyForm:{TheCommonUtils.cdeGuidToString(MyNMIForm.cdeMID)}:{TheCommonUtils.cdeGuidToString(ModelGuid)}:true:true"));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a Canvas that allows dynamic add/remove/change of new controls and Faceplates
        /// PREVIEW! This API is still in Preview and could change until final publication
        /// </summary>
        /// <param name="pBaseThing">Owner Thing</param>
        /// <param name="MyNMIForm">Target Form</param>
        /// <param name="pFormGuid">Form ID</param>
        /// <param name="FldNo">FldOrder of the Canvas Control</param>
        /// <param name="ACL">Access Permissions</param>
        /// <param name="XL">Width of the Control in Tiles</param>
        /// <param name="YL">Height of the Control in Tiles</param>
        /// <param name="pBag">Optional Property Bag</param>
        /// <returns></returns>
        public static Dictionary<string, TheMetaDataBase> AddFlexibleNMICanvas(TheThing pBaseThing, TheFormInfo MyNMIForm, Guid pFormGuid, int FldNo, int ACL, int XL, int YL, ThePropertyBag pBag = null)
        {
            var tFlds = new Dictionary<string, TheMetaDataBase>();
            Guid ModelGuid = pBaseThing.GetBaseEngine().GetEngineID();
            var tFL = AddSmartControl(pBaseThing, MyNMIForm, eFieldType.TileGroup, FldNo, 0, ACL, null, null, new nmiCtrlTileGroup { NoTE = true, IsAbsolute = true, Top = 0, Left = 0, TileWidth = XL, TileHeight = YL });
            if (pBag != null)
                tFL.PropertyBag = pBag;
            tFlds.Add("CANVAS", tFL);
            if (!TheBaseAssets.MyServiceHostInfo.IsCloudService)
            {
                tFL.RegisterEvent2(eUXEvents.OnShowEditor, (pMsg, para) =>
                {
                    var tNMIEditorForm = GetNMIEditorForm();
                    if (tNMIEditorForm != null)
                    {
                        var tThings = TheThingRegistry.GetThingsByFunc("*", s => !string.IsNullOrEmpty($"{s.GetProperty("FaceTemplate", false)?.GetValue()}"));
                        string tOpt = "";
                        foreach (var tThing in tThings)
                        {
                            if (tOpt.Length > 0)
                                tOpt += ";";
                            tOpt += $"{tThing.FriendlyName}:{tThing.cdeMID}";
                        }
                        AddSmartControl(pBaseThing, tNMIEditorForm, eFieldType.SmartLabel, 2004, 0xA2, 0x80, null, null, new nmiCtrlSmartLabel() { NoTE = true, FontSize = 24, TileFactorY = 2, Text = "Select a Thing to Add", TileWidth = 5 });
                        AddSmartControl(pBaseThing, tNMIEditorForm, eFieldType.ComboBox, 2005, 0xA2, 0x80, null, $"newthing", new nmiCtrlComboBox() { NoTE = true, Options = tOpt, TileWidth = 5 });
                        var ttt = AddSmartControl(pBaseThing, tNMIEditorForm, eFieldType.TileButton, 2010, 2, 0x80, "Add Thing to Screen", null, new nmiCtrlTileButton { NoTE = true, TileWidth = 5, ClassName = "cdeGoodActionButton" });
                        ttt.RegisterUXEvent(pBaseThing, eUXEvents.OnClick, "add", (sender, para) =>
                        {
                            var tMsg = para as TheProcessMessage;
                            var tEdThing = GetNMIEditorThing();
                            if (tMsg != null && tEdThing != null)
                            {
                                var tNewCtrl = $"{tEdThing?.GetProperty("newthing")}";
                                if (!string.IsNullOrEmpty(tNewCtrl))
                                {
                                    var tCtrlList = TheThing.GetSafePropertyString(pBaseThing, $"Form_{pFormGuid}_Plates");
                                    var tL = TheCommonUtils.CStringToList(tCtrlList, ',');
                                    tL ??= new List<string>();
                                    tL.Add(tNewCtrl);
                                    TheThing.SetSafePropertyString(pBaseThing, $"Form_{pFormGuid}_Plates", TheCommonUtils.CListToString(tL, ","));
                                    if (AddFlexibleNMIControls(pBaseThing, pFormGuid, MyNMIForm))
                                    {
                                        TheCommCore.PublishCentral(new TSM(eEngineName.NMIService, $"NMI_REQ_DASH:", $"{TheCommonUtils.cdeGuidToString(MyNMIForm.cdeMID)}:CMyForm:{TheCommonUtils.cdeGuidToString(MyNMIForm.cdeMID)}:{TheCommonUtils.cdeGuidToString(ModelGuid)}:true:true"));
                                    }
                                }
                            }
                        });
                        pMsg.Cookie = true;
                    }
                });
            }
            return tFlds;
        }

        private static bool AddFlexibleNMIControls(TheThing pBaseThing, Guid pFormGuid, TheFormInfo pNMIForm)
        {
            DeleteFieldsByRange(pNMIForm, 100, 9999);
            var tCtrlList = TheThing.GetSafePropertyString(pBaseThing, $"Form_{pFormGuid}_Plates");
            if (string.IsNullOrEmpty(tCtrlList))
                return false;
            var tL = TheCommonUtils.CStringToList(tCtrlList, ',');
            pNMIForm.FldStart = 100;
            foreach (var t in tL)
            {
                var tFaceThing = TheThingRegistry.GetThingByMID(TheCommonUtils.CGuid(t));
                tFaceThing?.MyThingBase?.ShowDeviceFace(pNMIForm, 78, 78);
            }
            return true;
        }

        /// <summary>
        /// Removes all Field Definitions of a Form
        /// </summary>
        /// <param name="tF"></param>
        public static void CleanForm(TheFormInfo tF)
        {
            if (tF == null) return;
            List<TheFieldInfo> tList = GetFieldsByFunc(s => s.FormID == tF.cdeMID);
            foreach (TheFieldInfo tInfo in tList)
                TheCDEngines.MyNMIService.MyNMIModel.MyFields.RemoveAnItem(tInfo, null);
        }

        /// <summary>
        /// This method creates an NMI Form and adds it to the Dashboard specified in the first parameter
        /// </summary>
        /// <param name="pOwnerThing">The Onwer Thing of this Form</param>
        /// <param name="pForm">All the Form Parameter</param>
        /// <param name="pClassName">Style ClassName</param>
        /// <param name="pFormTitle">Caption of the Dashboard Icon</param>
        /// <param name="pFlags">
        /// 1=Show On Local Relay
        /// 2=Show on Cloud Relay
        /// 4=Hide on Mobile
        /// 8=Do not include with ShowAll
        /// </param>
        /// <param name="pOrder">If the Dashpanel has multiple forms, this order sorts the forms</param>
        /// <param name="pACL">Access Level for this Form</param>
        /// <param name="pCategory">Category in the Dashboard this Form will be in</param>
        /// <param name="OnChangeName">Name of a Thing-Property this form is monitoring</param>
        /// <param name="pPropertyBag">Propertybag with custom properties of the form</param>
        /// <returns>Returns a GUID of the new Form. Guid.Empty is returned if this function failed</returns>
        public static TheDashPanelInfo AddFormToDashboard(TheThing pOwnerThing, TheFormInfo pForm, int pOrder, string pClassName = "CMyForm", string pFormTitle = null, int pFlags = 0, int pACL = 0, string pCategory = null, string OnChangeName = null, ThePropertyBag pPropertyBag = null)
        {
            return AddFormToThingUX(pOwnerThing, pForm, pClassName, pFormTitle, pOrder, pFlags, pACL, pCategory, OnChangeName, pPropertyBag);
        }

        /// <summary>
        /// This method creates an NMI Form and adds it to the Dashboard specified in the first parameter
        /// </summary>
        /// <param name="tDash">DashPanel this Form will be added to</param>
        /// <param name="pOwnerThing">The Onwer Thing of this Form</param>
        /// <param name="pForm">All the Form Parameter</param>
        /// <param name="pClassName">Style ClassName</param>
        /// <param name="pFormTitle">Caption of the Dashboard Icon</param>
        /// <param name="pFlags">
        /// 1=Show On Local Relay
        /// 2=Show on Cloud Relay
        /// 4=Hide on Mobile
        /// 8=Do not include with ShowAll
        /// </param>
        /// <param name="pOrder">If the Dashpanel has multiple forms, this order sorts the forms</param>
        /// <param name="pACL">Access Level for this Form</param>
        /// <param name="pCategory">Category in the Dashboard this Form will be in</param>
        /// <param name="OnChangeName">Name of a Thing-Property this form is monitoring</param>
        /// <param name="pPropertyBag">Propertybag with custom properties of the form</param>
        /// <returns>Returns a GUID of the new Form. Guid.Empty is returned if this function failed</returns>
        public static TheDashPanelInfo AddFormToDashboard(TheDashboardInfo tDash, TheThing pOwnerThing, TheFormInfo pForm, int pOrder, string pClassName = "CMyForm", string pFormTitle = null, int pFlags = 0, int pACL = 0, string pCategory = null, string OnChangeName = null, ThePropertyBag pPropertyBag = null)
        {
            return AddFormToThingUX(tDash, pOwnerThing, pForm, pClassName, pFormTitle, pOrder, pFlags, pACL, pCategory, OnChangeName, pPropertyBag);
        }

        /// <summary>
        /// This method creates an NMI Form and adds it to the Dashboard specified in the first parameter
        /// </summary>
        /// <param name="pOwnerThing">The Onwer Thing of this Form</param>
        /// <param name="pForm">All the Form Parameter</param>
        /// <param name="pClassName">Style ClassName</param>
        /// <param name="pFormTitle">Caption of the Dashboard Icon</param>
        /// <param name="pOrder">If the Dashpanel has multiple forms, this order sorts the forms</param>
        /// <param name="pFlags">
        /// 1=Show On Local Relay
        /// 2=Show on Cloud Relay
        /// 4=Hide on Mobile
        /// 8=Do not include with ShowAll
        /// </param>
        /// <returns>Returns a GUID of the new Form. Guid.Empty is returned if this function failed</returns>
        public static TheDashPanelInfo AddFormToThingUX(TheThing pOwnerThing, TheFormInfo pForm, string pClassName, string pFormTitle, int pOrder, int pFlags)
        {
            return AddFormToThingUX(pOwnerThing, pForm, pClassName, pFormTitle, pOrder, pFlags, -1, null, null, null);
        }
        /// <summary>
        /// This method creates an NMI Form and adds it to the Dashboard specified in the first parameter
        /// </summary>
        /// <param name="pOwnerThing">The Onwer Thing of this Form</param>
        /// <param name="pForm">All the Form Parameter</param>
        /// <param name="pClassName">Style ClassName</param>
        /// <param name="pFormTitle">Caption of the Dashboard Icon</param>
        /// <param name="pFlags">
        /// 1=Show On Local Relay
        /// 2=Show on Cloud Relay
        /// 4=Hide on Mobile
        /// 8=Do not include with ShowAll
        /// </param>
        /// <param name="pOrder">If the Dashpanel has multiple forms, this order sorts the forms</param>
        /// <param name="pACL">Access Level for this Form</param>
        /// <param name="pCategory">Category in the Dashboard this Form will be in</param>
        /// <param name="OnChangeName">Name of a Thing-Property this form is monitoring</param>
        /// <param name="pPropertyBag">Propertybag with custom properties of the form</param>
        /// <returns>Returns a GUID of the new Form. Guid.Empty is returned if this function failed</returns>
        public static TheDashPanelInfo AddFormToThingUX(TheThing pOwnerThing, TheFormInfo pForm, string pClassName, string pFormTitle, int pOrder, int pFlags, int pACL, string pCategory, string OnChangeName, ThePropertyBag pPropertyBag)
        {
            if (pForm == null || pOwnerThing == null || !IsInitialized()) return null;
            TheDashboardInfo tDash = GetEngineDashBoardByThing(pOwnerThing);
            return AddFormToThingUX(tDash, pOwnerThing, pForm, pClassName, pFormTitle, pOrder, pFlags, pACL, pCategory, OnChangeName, pPropertyBag);
        }

        /// <summary>
        /// This method creates an NMI Form and adds it to the Dashboard specified in the first parameter
        /// </summary>
        /// <param name="tDash">DashPanel this Form will be added to</param>
        /// <param name="pOwnerThing">The Onwer Thing of this Form</param>
        /// <param name="pForm">All the Form Parameter</param>
        /// <param name="pClassName">Style ClassName</param>
        /// <param name="pFormTitle">Title of the Form</param>
        /// <param name="pFlags">
        /// 1=Show On Local Relay
        /// 2=Show on Cloud Relay
        /// 4=Hide on Mobile
        /// 8=Do not include with ShowAll
        /// </param>
        /// <param name="pOrder">If the Dashpanel has multiple forms, this order sorts the forms</param>
        /// <param name="pACL">Access Level for this Form</param>
        /// <param name="pCategory">Category in the Dashboard this Form will be in</param>
        /// <param name="OnChangeName">Name of a Thing-Property this form is monitoring</param>
        /// <param name="pPropertyBag">Propertybag with custom properties of the form</param>
        /// <returns>Returns a GUID of the new Form. Guid.Empty is returned if this function failed</returns>
        public static TheDashPanelInfo AddFormToThingUX(TheDashboardInfo tDash, TheThing pOwnerThing, TheFormInfo pForm, string pClassName, string pFormTitle, int pOrder, int pFlags, int pACL, string pCategory, string OnChangeName, ThePropertyBag pPropertyBag)
        {
            if (tDash == null)
            {
                tDash = GetEngineDashBoardByThing(pOwnerThing);
                if (tDash == null)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(2343, new TSM(eEngineName.NMIService, string.Format("Dashboard for Thing {0} not found!", pOwnerThing), eMsgLevel.l1_Error));
                    return null;
                }
            }
            pForm.cdeA = pACL < 0 ? pOwnerThing.cdeA : pACL;
            pForm.cdeO = pOwnerThing.cdeMID;
            AddForm(pForm);
            string tThumb = ThePropertyBag.PropBagGetValue(pPropertyBag, "TileThumbnail", "=");
            TheDashPanelInfo MyDashPanelInfo = new (pOwnerThing)
            {
                cdeMID = pForm.cdeMID,  
                cdeA = pForm.cdeA,
                Flags = pFlags,
                PanelTitle = pFormTitle,
                FldOrder = pOrder,
                Category = pCategory,
                PropertyBag = string.IsNullOrEmpty(tThumb)?null: new ThePropertyBag { $"Thumbnail={tThumb}" },
                Description = ThePropertyBag.PropBagGetValue(pPropertyBag, "FriendlyName", "="),
                ControlClass = string.Format("{1}:{{{0}}}", pForm.cdeMID.ToString(), pClassName),
            };
            if (pPropertyBag != null)
                MyDashPanelInfo.PropertyBag = pPropertyBag;

            if (!string.IsNullOrEmpty(OnChangeName))
            {
                ThePropertyBag.PropBagUpdateValue(MyDashPanelInfo.PropertyBag, "OnThingEvent", "=", string.Format("{0};{1}", pOwnerThing.cdeMID, OnChangeName));
                ThePropertyBag.PropBagUpdateValue(MyDashPanelInfo.PropertyBag, "DataItem", "=", OnChangeName);
                cdeP tProp = pOwnerThing.GetProperty(OnChangeName, true);
                if (tProp != null)
                {
                    tProp.RegisterEvent(eThingEvents.PropertyChanged, MyDashPanelInfo.sinkUpdate);
                }
                else
                    pOwnerThing.SetProperty(OnChangeName, null, 0x11, MyDashPanelInfo.sinkUpdate);
                ThePropertyBag.PropBagUpdateValue(MyDashPanelInfo.PropertyBag, eUXEvents.OnPropertyChanged, "=", $"SEV:{OnChangeName}");
            }
            ThePropertyBag.PropBagUpdateValue(MyDashPanelInfo.PropertyBag, "UXID", "=", MyDashPanelInfo.cdeMID.ToString());
            AddDashPanel(tDash, MyDashPanelInfo);
            return MyDashPanelInfo;
        }

        /// <summary>
        /// Adds multiple forms to the NMI Model
        /// </summary>
        /// <param name="pFormInfos"></param>
        /// <returns></returns>
        public static bool AddForms(List<TheFormInfo> pFormInfos)
        {
            if (!IsInitialized() || pFormInfos == null) return false;
            bool bSuccess = false;
            foreach (TheFormInfo pInfo in pFormInfos)
            {
                if (AddForm(pInfo) != null)
                    bSuccess = true;
            }
            return bSuccess;
        }

        /// <summary>
        /// Creates a new Form in the NMI Model - same as AddForm!
        /// </summary>
        /// <param name="pFormInfo"></param>
        /// <returns></returns>
        public static TheFormInfo CreateForm(TheFormInfo pFormInfo)
        {
            return AddForm(pFormInfo);
        }

        /// <summary>
        /// Add a new Form to the NMI Model
        /// </summary>
        /// <param name="pFormInfo"></param>
        /// <returns></returns>
        public static TheFormInfo AddForm(TheFormInfo pFormInfo)
        {
            if (!IsInitialized() || pFormInfo == null) return null;
            if (!TheCDEngines.MyNMIService.MyNMIModel.MyForms.MyMirrorCache.ContainsID(pFormInfo.cdeMID))
                TheCDEngines.MyNMIService.MyNMIModel.MyForms.AddAnItem(pFormInfo);
            return pFormInfo;
        }

        /// <summary>
        /// Returns a Form but a given Form ID
        /// </summary>
        /// <param name="pDashGuid"></param>
        /// <returns></returns>
        public static TheFormInfo GetFormById(Guid pDashGuid)
        {
            if (!IsInitialized() || pDashGuid == Guid.Empty ||
                TheCDEngines.MyNMIService.MyNMIModel.MyForms == null || TheCDEngines.MyNMIService.MyNMIModel.MyForms.MyMirrorCache == null)
                return null;
            if (TheCDEngines.MyNMIService.MyNMIModel.MyForms.MyMirrorCache.ContainsID(pDashGuid))
                return TheCDEngines.MyNMIService.MyNMIModel.MyForms.MyMirrorCache.GetEntryByID(pDashGuid);
            return null;
        }


        internal static List<TheFormInfo> GetFormsByOwner(Guid pFormGuid)
        {
            if (!IsInitialized() || pFormGuid == Guid.Empty ||
                TheCDEngines.MyNMIService.MyNMIModel.MyForms == null || TheCDEngines.MyNMIService.MyNMIModel.MyForms.MyMirrorCache == null)
                return new List<TheFormInfo>();
            return TheCDEngines.MyNMIService.MyNMIModel.MyForms.MyMirrorCache.GetEntriesByFunc(s => s.cdeO == pFormGuid);
        }


        /// <summary>
        /// Adds a new Dashbard to the NMI Model
        /// </summary>
        /// <param name="pOwnerThing"></param>
        /// <param name="pDashInfo"></param>
        /// <returns></returns>
        public static TheDashboardInfo AddDashboard(TheThing pOwnerThing, TheDashboardInfo pDashInfo)
        {
            if (!IsInitialized() || pDashInfo == null || pOwnerThing == null) return null;
            try
            {
                pDashInfo.cdeO = pOwnerThing.cdeMID;
                if (pDashInfo.FldOrder == 0)
                    pDashInfo.FldOrder = 100;
                if (!TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.MyMirrorCache.ContainsID(pDashInfo.cdeMID))
                    TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.AddAnItem(pDashInfo);
            }
            catch (Exception e)
            {
                TheCDEngines.MyNMIService.GetBaseThing().StatusLevel = 3;
                TheCDEngines.MyNMIService.GetBaseThing().LastMessage = "AddDashboard failed!";
                TheBaseAssets.MySYSLOG.WriteToLog(2345, new TSM(eEngineName.NMIService, TheCDEngines.MyNMIService.GetBaseThing().LastMessage, eMsgLevel.l1_Error, e.ToString()));
            }
            return pDashInfo;
        }


        /// <summary>
        /// Returns a list of all defined Dashboard
        /// </summary>
        /// <returns></returns>
        public static List<TheDashboardInfo> GetAllDashboards()
        {
            if (!IsInitialized() || TheCDEngines.MyNMIService.MyNMIModel.MyDashboards == null || TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.MyMirrorCache == null)
                return new List<TheDashboardInfo>();
            return TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.MyMirrorCache.TheValues.ToList();
        }


        /// <summary>
        /// Returns a Dashboard by a given Dashboard ID
        /// </summary>
        /// <param name="pDashGuid"></param>
        /// <returns></returns>
        public static TheDashboardInfo GetDashboardById(Guid pDashGuid)
        {
            if (!IsInitialized() || pDashGuid == Guid.Empty ||
                TheCDEngines.MyNMIService.MyNMIModel.MyDashboards == null || TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.MyMirrorCache == null)
                return null;
            if (TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.MyMirrorCache.ContainsID(pDashGuid))
                return TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.MyMirrorCache.GetEntryByID(pDashGuid);
            return null;
        }

        internal static TheDashboardInfo GetDashboardByOwner(Guid pDashGuid)
        {
            if (!IsInitialized() || pDashGuid == Guid.Empty ||
                TheCDEngines.MyNMIService.MyNMIModel.MyDashboards == null || TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.MyMirrorCache == null)
                return null;
            return TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.MyMirrorCache.GetEntryByFunc(s => s.cdeO == pDashGuid);
        }
        internal static List<TheDashboardInfo> GetDashboardsByOwner(Guid pDashGuid)
        {
            if (!IsInitialized() || pDashGuid == Guid.Empty ||
                TheCDEngines.MyNMIService.MyNMIModel.MyDashboards == null || TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.MyMirrorCache == null)
                return new List<TheDashboardInfo>();
            return TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.MyMirrorCache.GetEntriesByFunc(s => s.cdeO == pDashGuid);
        }



        /// <summary>
        /// Deletes a form by a given FormID
        /// </summary>
        /// <param name="pGuid"></param>
        /// <returns></returns>
        public static bool DeleteFormById(Guid pGuid)
        {
            if (!IsInitialized() || pGuid == Guid.Empty ||
                TheCDEngines.MyNMIService.MyNMIModel.MyForms == null || TheCDEngines.MyNMIService.MyNMIModel.MyForms.MyMirrorCache == null)
                return false;
            TheCDEngines.MyNMIService.MyNMIModel.MyForms.RemoveAnItemByID(pGuid, null);
            return true;
        }

        /// <summary>
        /// Delets a Dashboard Component by a given ID
        /// </summary>
        /// <param name="pGuid"></param>
        /// <returns></returns>
        public static bool DeleteDashComponentById(Guid pGuid)
        {
            if (!IsInitialized() || pGuid == Guid.Empty ||
                TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents == null || TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents.MyMirrorCache == null)
                return false;
            TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents.RemoveAnItemByID(pGuid, null);
            return true;
        }

        /// <summary>
        /// Deletes a list of fields from a form between a range of FldOrders
        /// </summary>
        /// <param name="pFormInfo"></param>
        /// <param name="pStart"></param>
        /// <param name="pEnd"></param>
        /// <returns></returns>
        public static bool DeleteFieldsByRange(TheFormInfo pFormInfo, int pStart, int pEnd=-1)
        {
            if (pFormInfo == null)
                return false;
            List<TheFieldInfo> tLst = TheNMIEngine.GetFieldsByFunc(s => s.FormID == pFormInfo.cdeMID);
            foreach (TheFieldInfo tInfo in tLst)
            {
                if (tInfo.FldOrder >= pStart && (pEnd<0 || tInfo.FldOrder <= pEnd))
                    TheNMIEngine.DeleteFieldById(tInfo.cdeMID);
            }
            return true;
        }

        /// <summary>
        /// Delets all fields of a given ParentFld starting at a specific Field Order
        /// </summary>
        /// <param name="pFormInfo"></param>
        /// <param name="pParentFld"></param>
        /// <param name="pStartNo"></param>
        /// <returns></returns>
        public static bool DeleteFieldsOfParent(TheFormInfo pFormInfo, int pParentFld, int pStartNo = 0)
        {
            if (pFormInfo == null)
                return false;
            List<TheFieldInfo> tLst = TheNMIEngine.GetFieldsByFunc(s => s.FormID == pFormInfo.cdeMID);
            foreach (TheFieldInfo tInfo in tLst)
            {
                if ((pStartNo == 0 || tInfo.FldOrder >= pStartNo) && TheCommonUtils.CInt(tInfo.PropBagGetValue("ParentFld")) == pParentFld)
                    TheNMIEngine.DeleteFieldById(tInfo.cdeMID);
            }
            return true;
        }

        /// <summary>
        /// Adds new DashPanels to a Dashboard
        /// </summary>
        /// <param name="pDashBoard"></param>
        /// <param name="pFormInfos"></param>
        /// <returns></returns>
        public static bool AddDashPanels(TheDashboardInfo pDashBoard, List<TheDashPanelInfo> pFormInfos)
        {
            if (!IsInitialized() || pFormInfos == null || pDashBoard == null) return false;
            bool bSuccess = false;
            foreach (TheDashPanelInfo pInfo in pFormInfos)
            {
                if (AddDashPanel(pDashBoard, pInfo) != Guid.Empty)
                    bSuccess = true;
            }
            return bSuccess;
        }

        /// <summary>
        /// Add a new Dashpanel to a Dashboard
        /// </summary>
        /// <param name="pDash"></param>
        /// <param name="pDashInfo"></param>
        /// <returns></returns>
        public static Guid AddDashPanel(TheDashboardInfo pDash, TheDashPanelInfo pDashInfo)
        {
            if (!IsInitialized() || pDash == null || pDashInfo == null)
                return Guid.Empty;
            if (!TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents.MyMirrorCache.ContainsID(pDashInfo.cdeMID))
                TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents.AddAnItem(pDashInfo);
            if (pDash.colPanels != null && pDash.colPanels.Contains(pDashInfo.cdeMID.ToString()))
                return pDashInfo.cdeMID;
            pDash.colPanels ??= new List<string>();
            pDash.colPanels.Add(pDashInfo.cdeMID.ToString());
            return pDashInfo.cdeMID;
        }

        /// <summary>
        /// Gets a Dashpanel by a given Panel ID
        /// </summary>
        /// <param name="pDashGuid"></param>
        /// <returns></returns>
        public static TheDashPanelInfo GetDashPanelById(Guid pDashGuid)
        {
            if (!IsInitialized() || pDashGuid == Guid.Empty ||
                TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents == null || TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents.MyMirrorCache == null)
                return null;
            if (TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents.MyMirrorCache.ContainsID(pDashGuid))
                return TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents.MyMirrorCache.GetEntryByID(pDashGuid);
            return null;
        }
        internal static List<TheDashPanelInfo> GetDashPanelsByOwner(Guid pDashGuid)
        {
            if (!IsInitialized() || pDashGuid == Guid.Empty ||
                TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents == null || TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents.MyMirrorCache == null)
                return new List<TheDashPanelInfo>();
            return TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents.MyMirrorCache.GetEntriesByFunc(s => s.cdeO == pDashGuid);
        }

        /// <summary>
        /// Returns the Dashboard of a given ICDEThing
        /// </summary>
        /// <param name="pThing">ICDEThing of the Dashboard to be found</param>
        /// <returns></returns>
        public static TheDashboardInfo GetEngineDashBoardByThing(ICDEThing pThing)
        {
            if (pThing == null) return null;
            return GetDashboardById(GetEngineDashBoardGuidByThing(pThing));
        }

        /// <summary>
        /// Return the GUID of a Dashboard of a given ICDEThing
        /// </summary>
        /// <param name="pThing"></param>
        /// <returns></returns>
        public static Guid GetEngineDashBoardGuidByThing(ICDEThing pThing)
        {
            Guid tDash = Guid.Empty;
            if (pThing == null || pThing.GetBaseThing() == null) return tDash;
            IBaseEngine tBase = pThing.GetBaseThing().GetBaseEngine();
            if (tBase != null)
            {
                if (tBase.GetBaseThing() != null && !tBase.GetBaseThing().IsUXInit())
                    tBase.GetBaseThing().CreateUX();
                tDash = tBase.GetDashboardGuid();
            }
            return tDash;
        }
        #endregion

        #region Pages and Content of Pages (HTML free)

        /// <summary>
        /// Updates the Main Frame-Template for all pages
        /// </summary>
        /// <param name="pTemplateID"></param>
        public static void UpdateMainFrameTemplate(Guid pTemplateID)
        {
            ICDENMIPlugin tRenderEngine = TheCDEngines.MyNMIService?.MyRenderEngine;
            tRenderEngine?.RenderMainFrameTemplate(pTemplateID);
        }

        /// <summary>
        /// Adds a new PageDefinition to the NMI Model
        /// </summary>
        /// <param name="pList"></param>
        /// <returns></returns>
        public static bool AddPageDefinitions(List<ThePageDefinition> pList)
        {
            if (!IsInitialized() || pList == null) return false;
            bool bSuccess = false;
            foreach (ThePageDefinition pInfo in pList)
            {
                if (AddPageDefinition(pInfo) != Guid.Empty)
                    bSuccess = true;
            }
            return bSuccess;
        }

        /// <summary>
        /// Adds a new smart page to the system. In ThePageDefinition the parameter pPageName can be directly addressed in the browser
        /// ATTENTION: Changing ThePageDefinition after the first start of the Relay will NOT use the latest parameter. You have to delete the Cache File 604n...
        /// If you delete the file, all hit counter will be reset.
        /// </summary>
        /// <param name="pDashInfo"></param>
        /// <returns></returns>
        public static Guid AddPageDefinition(ThePageDefinition pDashInfo)
        {
            if (!IsInitialized() || pDashInfo == null || TheCDEngines.MyNMIService.MyNMIModel == null || TheCDEngines.MyNMIService.MyNMIModel.MyPageDefinitionsStore == null) return Guid.Empty;
            ThePageDefinition tPage = TheCDEngines.MyNMIService.MyNMIModel.MyPageDefinitionsStore.GetEntryByID(pDashInfo.cdeMID);
            if (tPage == null)
                TheCDEngines.MyNMIService.MyNMIModel.MyPageDefinitionsStore.AddAnItem(pDashInfo);
            else
            {
                pDashInfo.LastUpdate = tPage.LastUpdate;
                pDashInfo.Errors = tPage.Errors;
                pDashInfo.Hits = tPage.Hits;
                TheCDEngines.MyNMIService.MyNMIModel.MyPageDefinitionsStore.UpdateItem(pDashInfo);
            }
            return pDashInfo.cdeMID;
        }

        /// <summary>
        /// Returns true when the PageStore is ready
        /// </summary>
        /// <returns></returns>
        public static bool IsPageStoreReady()
        {
            return TheCDEngines.MyNMIService?.MyNMIModel != null && TheCDEngines.MyNMIService.MyNMIModel.MyPageDefinitionsStore != null && TheCDEngines.MyNMIService.MyNMIModel.MyPageDefinitionsStore.IsReady;
        }

        /// <summary>
        /// Returns a Page by its Content ID
        /// </summary>
        /// <param name="pID"></param>
        /// <returns></returns>
        public static List<ThePageContent> GetPageContentByID(Guid pID)
        {
            if (!IsPageStoreReady()) return new List<ThePageContent>();
            return TheCDEngines.MyNMIService.MyNMIModel.MyPageContentStore.MyMirrorCache.TheValues.Where(s => s.ContentID == pID).OrderBy(s => s.SortOrder).ToList();
        }

        /// <summary>
        /// Gets ThePageDefinition by a given RealPage name (i.e. /NMIPortal)
        /// </summary>
        /// <param name="pRealPage"></param>
        /// <returns></returns>
        public static ThePageDefinition GetPageByRealPage(string pRealPage)
        {
            if (!IsPageStoreReady()) return null;
            return TheCDEngines.MyNMIService.MyNMIModel?.MyPageDefinitionsStore?.MyMirrorCache?.TheValues?.Find(s => !string.IsNullOrEmpty(s.PageName) && s.PageName.ToUpperInvariant().Contains(pRealPage.ToUpperInvariant()));
        }

        /// <summary>
        /// Return ThePageDefinition by a given Page ID
        /// </summary>
        /// <param name="pID"></param>
        /// <returns></returns>
        public static ThePageDefinition GetPageByID(Guid pID)
        {
            if (!IsPageStoreReady()) return null;
            return TheCDEngines.MyNMIService.MyNMIModel.MyPageDefinitionsStore.MyMirrorCache.TheValues.First(s => s.cdeMID == pID);
        }

        /// <summary>
        /// Returns ThePageBlock by a given ID
        /// </summary>
        /// <param name="pID"></param>
        /// <returns></returns>
        public static ThePageBlocks GetBlockByID(Guid pID)
        {
            if (!IsPageStoreReady()) return null;
            return TheCDEngines.MyNMIService.MyNMIModel.MyPageBlocksStore.MyMirrorCache.GetEntryByID(pID);
        }

        /// <summary>
        /// Returns all ThePageBlock by a given Type Guid
        /// </summary>
        /// <param name="pID"></param>
        /// <param name="pUserID">User ID Requesting the Blocks</param>
        /// <param name="pNodeID">Node ID requesting the block</param>
        /// <returns></returns>
        internal static List<ThePageBlocks> GetBlocksByType(Guid pID, Guid pUserID, Guid pNodeID)
        {
            if (!IsPageStoreReady()) return new List<ThePageBlocks>();
            return TheCDEngines.MyNMIService.MyNMIModel.MyPageBlocksStore.MyMirrorCache.GetEntriesByFunc(s=>s.BlockType==pID && s.cdeN== pNodeID && ((s.cdeO!=Guid.Empty && s.cdeO==pUserID) || Security.TheUserManager.HasUserAccess(pUserID,(int)s.AccessLevel)));
        }

        internal static List<ThePageBlocks> GetBlocksToSync()
        {
            if (!IsPageStoreReady()) return new List<ThePageBlocks>();
            return TheCDEngines.MyNMIService.MyNMIModel.MyPageBlocksStore.MyMirrorCache.GetEntriesByFunc(s => s.IsMeshSynced);
        }

        /// <summary>
        /// Increases the Hit Counter on a given ThePageDefinition
        /// </summary>
        /// <param name="tPageDefinition"></param>
        /// <param name="IsHit"></param>
        public static void PageHitCounter(ThePageDefinition tPageDefinition, bool IsHit)
        {
            TheCommonUtils.cdeRunAsync("NMI HitCounter", true, (o) =>
            {
                if (TheCDEngines.MyNMIService.MyNMIModel.MyPageDefinitionsStore.IsRAMStore)
                {
                    tPageDefinition.LastAccess = DateTimeOffset.Now;
                    if (IsHit)
                        tPageDefinition.Hits++;
                    else
                        tPageDefinition.Errors++;
                    TheCDEngines.MyNMIService.MyNMIModel.MyPageDefinitionsStore.UpdateItem(tPageDefinition);
                }
                else
                {
                    if (IsHit)
                        TheCDEngines.MyNMIService.MyNMIModel.MyPageDefinitionsStore.ExecuteSql("update {0} set Hits=Hits+1,LastAccess='" + DateTimeOffset.Now + "'", "cdeMID='" + tPageDefinition.cdeMID + "'", null); // CODE REVIEW: Can we send DateTimeOffset here, or who interprets the SQL string besides SQL Server?
                    else
                        TheCDEngines.MyNMIService.MyNMIModel.MyPageDefinitionsStore.ExecuteSql("update {0} set Errors=Errors+1", "cdeMID='" + tPageDefinition.cdeMID + "'", null);
                }
            });
        }

        /// <summary>
        /// Creats a direct access Page for a given Thing
        /// </summary>
        /// <param name="pBaseThing">The Thing and its dashboard or Form that wants to have a direct access page</param>
        /// <param name="pBaseEngine">The engine that owns the form</param>
        /// <param name="pPath">The path to the new page from the root of the Relay</param>
        /// <param name="pTitle">A title of the new page</param>
        /// <param name="pIncludeCDE">The page requires the C-DEngine runtime. You can also specify static pages that do not require the C-DEngine</param>
        /// <param name="pRequireLogin">if set, the page will require a login</param>
        /// <param name="pIsPublic">The page can be accessed from anywhere.</param>
        /// <param name="pNMIPortalTemplate">By default a smart page is using the NMIPortal Template for the header and footer of the Page. This parameter can be overwritten to point at any other template</param>
        /// <param name="DoNotIncludeHeaderButtons">If set to true, there will be no header navigation button on top of the page. By default the buttons are visible</param>
        public static ThePageDefinition AddSmartPage(TheThing pBaseThing, IBaseEngine pBaseEngine, string pPath, string pTitle, bool pIncludeCDE, bool pRequireLogin, bool pIsPublic, string pNMIPortalTemplate = "nmiportal.html", bool DoNotIncludeHeaderButtons = false)
        {
            if (pBaseEngine == null || pBaseThing == null || string.IsNullOrEmpty(pPath)) return null;
            ThePageDefinition tPage = new (pBaseThing.cdeMID, pPath, pTitle, "", Guid.Empty)
            {
                StartScreen = pBaseEngine.GetDashboardGuid(), //pBaseThing.cdeMID
                IncludeCDE = pIncludeCDE,
                RequireLogin = pRequireLogin,
                IsPublic = pIsPublic,
                PageTemplate = pNMIPortalTemplate,
                IncludeHeaderButtons = !DoNotIncludeHeaderButtons,
                PortalGuid = pBaseEngine.GetDashboardGuid()
            };
            if (AddPageDefinition(tPage) != Guid.Empty)
                return tPage;
            else
                return null;
        }

        /// <summary>
        /// Creats a direct access Page for a given Form
        /// </summary>
        /// <param name="pFormInfo">The form that wants to have a direct access page</param>
        /// <param name="pBaseEngine">The engine that owns the form</param>
        /// <param name="pPath">The path to the new page from the root of the Relay</param>
        /// <param name="pTitle">A title of the new page</param>
        /// <param name="pIncludeCDE">The page requires the C-DEngine runtime. You can also specify static pages that do not require the C-DEngine</param>
        /// <param name="pRequireLogin">if set, the page will require a login</param>
        /// <param name="pIsPublic">The page can be accessed from anywhere.</param>
        /// <returns></returns>
        public static ThePageDefinition AddSmartPage(TheFormInfo pFormInfo, IBaseEngine pBaseEngine, string pPath, string pTitle, bool pIncludeCDE, bool pRequireLogin, bool pIsPublic)
        {
            if (pBaseEngine == null || pFormInfo == null || string.IsNullOrEmpty(pPath)) return null;
            ThePageDefinition tPage = new (pFormInfo.cdeMID, pPath, pTitle, "", Guid.Empty)
            {
                StartScreen = pFormInfo.cdeMID,
                IncludeCDE = pIncludeCDE,
                RequireLogin = pRequireLogin,
                IsPublic = pIsPublic,
                PortalGuid = pBaseEngine.GetDashboardGuid()
            };
            if (AddPageDefinition(tPage) != Guid.Empty)
                return tPage;
            else
                return null;
        }

        /// <summary>
        /// Adds a list of ThePageContent to the NMI Model - Page Content can be listed in ThePageDefinition
        /// </summary>
        /// <param name="pContentGuid"></param>
        /// <param name="pList"></param>
        /// <returns></returns>
        public static bool AddPageContent(Guid pContentGuid, List<ThePageContent> pList)
        {
            if (!IsInitialized() || pList == null) return false;
            bool bSuccess = false;
            foreach (ThePageContent pInfo in pList)
            {
                if (!TheCDEngines.MyNMIService.MyNMIModel.MyPageContentStore.MyMirrorCache.ContainsID(pInfo.cdeMID))
                {
                    if (pContentGuid != Guid.Empty)
                        pInfo.ContentID = pContentGuid;
                    TheCDEngines.MyNMIService.MyNMIModel.MyPageContentStore.AddAnItem(pInfo);
                    bSuccess = true;
                }
            }
            return bSuccess;
        }

        /// <summary>
        /// Adds a list of ThePageBlocks to the NMI Model - blocks are referenced by ThePageContent in the WBID Field
        /// </summary>
        /// <param name="pList"></param>
        /// <returns></returns>
        public static bool AddPageBlocks(List<ThePageBlocks> pList)
        {
            if (!IsInitialized() || pList == null) return false;
            bool bSuccess = false;
            foreach (ThePageBlocks pInfo in pList)
            {
                if (!TheCDEngines.MyNMIService.MyNMIModel.MyPageBlocksStore.MyMirrorCache.ContainsID(pInfo.cdeMID))
                {
                    TheCDEngines.MyNMIService.MyNMIModel.MyPageBlocksStore.AddAnItem(pInfo);
                    bSuccess = true;
                }
            }
            return bSuccess;
        }

        /// <summary>
        /// Update blocks in the PageBlock store
        /// </summary>
        /// <param name="pList"></param>
        /// <returns></returns>
        public static bool UpdatePageBlocks(List<ThePageBlocks> pList)
        {
            if (!IsInitialized() || pList == null) return false;
            TheCDEngines.MyNMIService.MyNMIModel.MyPageBlocksStore.UpdateItems(pList);
            return true;
        }


        #endregion

        #region Controls related Methods (HTML free)

        /// <summary>
        /// Register a new Control with the NMI Engine. Registered controls can be used in Live Screens and any Plug-In InitUX() function
        /// </summary>
        /// <param name="pBaseEngine"></param>
        /// <param name="pControlName"></param>
        /// <param name="pControlType"></param>
        /// <param name="pSmartControlType"></param>
        /// <returns></returns>
        public static bool RegisterControlType(IBaseEngine pBaseEngine, string pControlName, string pControlType, string pSmartControlType)
        {
            if (TheCDEngines.MyNMIService != null && pBaseEngine != null && !string.IsNullOrEmpty(pControlType))
            {
                TheCDEngines.MyNMIService.MyNMIModel.MyControlRegistry.AddAnItem(new TheControlType() { BaseEngineName = pBaseEngine.GetEngineName(), ControlName = pControlName, ControlType = pControlType, SmartControlType = pSmartControlType });
                return true;
            }
            return false;
        }

        /// <summary>
        /// Register a new Control with the NMI Engine. Registered controls can be used in Live Screens and any Plug-In InitUX() function
        /// </summary>
        /// <param name="pBaseEngine"></param>
        /// <param name="pControlName"></param>
        /// <param name="pControlType"></param>
        public static bool RegisterControlType(IBaseEngine pBaseEngine, string pControlName, string pControlType)
        {
            return RegisterControlType(pBaseEngine, pControlName, pControlType, null);
        }

        /// <summary>
        /// Return the optimal NMI Control Type for a given PropertyType
        /// </summary>
        /// <param name="tType"></param>
        /// <returns></returns>
        public static eFieldType GetCtrlTypeFromCDET(ePropertyTypes tType)
        {
            eFieldType tt = eFieldType.SingleEnded;
            switch (tType)
            {
                case ePropertyTypes.TBoolean:
                    tt = eFieldType.SingleCheck;
                    break;
                case ePropertyTypes.TBinary:
                    tt = eFieldType.Picture;
                    break;
                case ePropertyTypes.TDate:
                    tt = eFieldType.DateTime;
                    break;
                case ePropertyTypes.TNumber:
                    tt = eFieldType.Number;
                    break;
            }
            return tt;
        }

        /// <summary>
        /// This function returns the optimal TheControlType of a given Property Type
        /// </summary>
        /// <param name="pType"></param>
        /// <returns></returns>
        public static TheControlType GetControlTypeByPType(ePropertyTypes pType)
        {
            TheControlType tType = new ()
            {
                BaseEngineName = eEngineName.NMIService,
                ControlType = ((int)eFieldType.SingleEnded).ToString()
            };
            switch (pType)
            {
                case ePropertyTypes.TBinary:
                    tType.ControlType = ((int)eFieldType.Picture).ToString();
                    break;
                case ePropertyTypes.TDate:
                    tType.ControlType = ((int)eFieldType.DateTime).ToString();
                    break;
                case ePropertyTypes.TNumber:
                    tType.ControlType = ((int)eFieldType.Number).ToString();
                    break;
                case ePropertyTypes.TBoolean:
                    tType.ControlType = ((int)eFieldType.SingleCheck).ToString();
                    break;
            }
            return tType;
        }

        /// <summary>
        /// Returns true if Control is registered
        /// </summary>
        /// <param name="pControlName">Name of the control to look for. </param>
        /// <returns></returns>
        public static bool IsControlRegistered(string pControlName)
        {
            if (TheCDEngines.MyNMIService != null && !string.IsNullOrEmpty(pControlName) && TheCDEngines.MyNMIService.MyNMIModel.MyControlRegistry != null && TheCDEngines.MyNMIService.MyNMIModel.MyControlRegistry.MyMirrorCache != null)
            {
                return TheCDEngines.MyNMIService.MyNMIModel.MyControlRegistry.MyMirrorCache.GetEntryByFunc(s => s.ControlName.StartsWith(pControlName))!=null;
            }
            return false;
        }

        /// <summary>
        /// Returns true if Control is registered
        /// </summary>
        /// <param name="pControlType">Type of the control to look for. </param>
        /// <returns></returns>
        public static bool IsControlTypeRegistered(string pControlType)
        {
            if (TheCDEngines.MyNMIService != null && !string.IsNullOrEmpty(pControlType) && TheCDEngines.MyNMIService.MyNMIModel.MyControlRegistry != null && TheCDEngines.MyNMIService.MyNMIModel.MyControlRegistry.MyMirrorCache != null)
            {
                return TheCDEngines.MyNMIService.MyNMIModel.MyControlRegistry.MyMirrorCache.GetEntryByFunc(s => s.ControlType.StartsWith(pControlType)) != null;
            }
            return false;
        }

        /// <summary>
        /// Returns the TheControlType by a given Control Type name
        /// </summary>
        /// <param name="pType"></param>
        /// <returns></returns>
        public static TheControlType GetControlTypeByType(string pType)
        {
            if (TheCDEngines.MyNMIService != null && !string.IsNullOrEmpty(pType) && TheCDEngines.MyNMIService.MyNMIModel.MyControlRegistry != null && TheCDEngines.MyNMIService.MyNMIModel.MyControlRegistry.MyMirrorCache != null)
            {
                return TheCDEngines.MyNMIService.MyNMIModel.MyControlRegistry.MyMirrorCache.GetEntryByFunc(s => s.ControlType == pType);
            }
            return null;
        }

        /// <summary>
        /// returns true if the given property is a CDE Intrinsyc and internal property
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static bool IsCDEProperty(cdeP prop)
        {
            if (prop == null)
                return false;
            if (IsCDEProperty(prop.Name)) return true;
            return false;
        }


        /// <summary>
        /// Returns true if the given property name is a well known C-DEngine property that should not be translated
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool IsCDEProperty(string name)
        {
            if (name == "DataItem" || name == "EngineName" || name == "DeviceType" || name == "Address" || name == "StatusLevel")
                return true;
            return false;
        }

        #endregion

        #region Charts (HTML Free)
        /// <summary>
        /// Adds a new Chart Definition to the NMI Model
        /// </summary>
        /// <param name="pChart"></param>
        /// <returns></returns>
        public static TheChartDefinition AddChartDefinition(TheChartDefinition pChart)
        {
            if (!IsInitialized() || pChart == null) return null;
            if (!TheCDEngines.MyNMIService.MyNMIModel.MyChartScreens.MyMirrorCache.ContainsID(pChart.cdeMID))
            {
                TheCDEngines.MyNMIService.MyNMIModel.MyChartScreens.AddAnItem(pChart);
                return pChart;
            }
            return null;
        }

        /// <summary>
        /// Adds a new Chart Screen to the NMI. To render this screen the CMyCharts plugin is required on a node in the mesh
        /// </summary>
        /// <param name="pMyBaseThing">Owner Thing of the Chart Screen. The screen will be placed in the Dashboard of the plugin of the ownerthing</param>
        /// <param name="pChart">Chart Definition for the screen</param>
        /// <param name="pOrder">fld order in the dashboard</param>
        /// <param name="pFlags">Flags for the Screen</param>
        /// <param name="pACL">User AccessLevel for the screen</param>
        /// <param name="pCategory">Category in the Dashbboard</param>
        /// <param name="UseCustomData">if true, the data will be coming from the Engine of the ownerthing (rare!)</param>
        /// <param name="pPropertyBag">Propertybag for the screen</param>
        /// <returns></returns>
        public static TheFormInfo AddChartScreen(TheThing pMyBaseThing, TheChartDefinition pChart, int pOrder, int pFlags, int pACL, string pCategory, bool UseCustomData, ThePropertyBag pPropertyBag)
        {
            var tFlds = AddChartScreen(pMyBaseThing, pChart, pOrder, null, pFlags, pACL, pCategory, UseCustomData, pPropertyBag);
            return tFlds?.cdeSafeGetValue("Form") as TheFormInfo;
        }

        /// <summary>
        /// Create a new Chart Screen returning a dictionary of TheMetaDataBase that can be mapped:
        /// "Form" TheFormInfo; "DashIcon" TheDashPanelInfo of the icon in the Dashboard;  "Group" TheFieldInfo of the Collapsible Header group; "Chart" TheFieldInfo of the Chart Control
        /// </summary>
        /// <param name="pMyBaseThing">Owner Thing of the Chart Screen. The screen will be placed in the Dashboard of the plugin of the ownerthing</param>
        /// <param name="pChart">Chart Definition for the screen</param>
        /// <param name="pOrder">fld order in the dashboard</param>
        /// <param name="DashIconHeader">Text for the tile in the Dashboard. If empty the pCharts.TitleText will be used</param>
        /// <param name="pFlags">Flags for the Screen</param>
        /// <param name="pACL">User AccessLevel for the screen</param>
        /// <param name="pCategory">Category in the Dashbboard</param>
        /// <param name="UseCustomData">if true, the data will be coming from the Engine of the ownerthing (rare!)</param>
        /// <param name="pPropertyBag">Propertybag for the screen</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddChartScreen(TheThing pMyBaseThing, TheChartDefinition pChart, int pOrder, string DashIconHeader = null, int pFlags = 0, int pACL = 0, string pCategory = "", bool UseCustomData = false, ThePropertyBag pPropertyBag = null)
        {
            if (!IsInitialized() || pChart == null) return new cdeConcurrentDictionary<string, TheMetaDataBase>();
            AddChartDefinition(pChart);

            var tFlds = new cdeConcurrentDictionary<string, TheMetaDataBase>();
            TheFormInfo tMyConfForm = new(pMyBaseThing)
            {
                cdeMID = pChart.cdeMID,
                FormTitle = null,
                DefaultView = eDefaultView.Form,
                PropertyBag = new ThePropertyBag { "MaxTileWidth=18", "Background=rgba(255, 255, 255, 0.04)", "HideCaption=true" },
                ModelID = "ChartScreen"
            };
            tMyConfForm.AddOrUpdatePlatformBag(eWebPlatform.Mobile, new nmiPlatBag { MaxTileWidth = 6 });
            tMyConfForm.AddOrUpdatePlatformBag(eWebPlatform.HoloLens, new nmiPlatBag { MaxTileWidth = 12 });
            tFlds["Form"] = tMyConfForm;
            tFlds["DashIcon"] = AddFormToThingUX(pMyBaseThing, tMyConfForm, eScreenClass.CMyChart, DashIconHeader ?? pChart.TitleText, pOrder, pFlags, pACL, pCategory, null, ThePropertyBag.GetSubBag(pPropertyBag, 0));

            string tHeader = ThePropertyBag.PropBagGetValue(pPropertyBag, "Header", "=");
            if (string.IsNullOrEmpty(tHeader))
                tHeader=pChart.TitleText;
            var tL=AddChartControl(pMyBaseThing, pChart, tMyConfForm, 10, tHeader, pChart.TitleText,UseCustomData, pPropertyBag);
            if (tL != null && tL.Count > 0)
            {
                foreach (var tt in tL.Keys)
                    tFlds[tt] = tL[tt];
            }
            return tFlds;
        }

        /// <summary>
        /// Adds a new Chart control to a Form
        /// </summary>
        /// <typeparam name="T">Class Name of the storage Mirror to be added</typeparam>
        /// <param name="pMyBaseThing">Owner thing</param>
        /// <param name="sensorHistory">StorageMirror for the chart data</param>
        /// <param name="pForm">Form of the chart to be inserted</param>
        /// <param name="pFldOrder">FldOrder for the Chart Control</param>
        /// <param name="pGroupTitle">Name for the Collapsible Group</param>
        /// <param name="pChartTitle">Title of the Chart</param>
        /// <param name="pLabels">List of values separated by ; to be shown in the chart. Must be set to at least one Value</param>
        /// <param name="pPropertyNames">List of Propertys to be shown in the chart separated by ;. Must be set to at least one Name.</param>
        /// <param name="pChartBag">Property Bag for the Chart Control</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddChartControl<T>(TheThing pMyBaseThing, TheStorageMirror<T> sensorHistory, TheFormInfo pForm, int pFldOrder, string pGroupTitle, string pChartTitle, string pLabels, string pPropertyNames, ThePropertyBag pChartBag = null) where T : TheDataBase, INotifyPropertyChanged, new()
        {
            if (pMyBaseThing == null || sensorHistory == null)
                return new cdeConcurrentDictionary<string, TheMetaDataBase>();
            var tList = new cdeConcurrentDictionary<string, TheMetaDataBase>();

            bool OnlyShowValue = TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pChartBag, "OnlyShowValue", "="));

            var tListCharts = new List<TheChartValueDefinition>();
            var tProps = pPropertyNames.Split(';');
            if (!OnlyShowValue)
            {
                var tLabels = pLabels?.Split(';');
                for (int i = 0; i < tProps.Length; i++)
                {
                    tListCharts.Add(new TheChartValueDefinition(Guid.NewGuid(), tProps[i]) { Label = string.IsNullOrEmpty(pLabels) ? tProps[i] : (i < tLabels.Length ? tLabels[i] : tProps[i]) });
                }
            }
            else
            {
                tListCharts.Add(new TheChartValueDefinition(Guid.NewGuid(), tProps[0]) { Label = pLabels });
            }

            string tDefSource = $"{sensorHistory.StoreMID};:;0;:;{TheThing.GetSafePropertyString(pMyBaseThing, "ScaleFactor")}";
            int blocksize = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pChartBag, "BlockSize", "="));
            if (blocksize == 0)
                blocksize = 2000;
            var MyChart = new TheChartDefinition(TheThing.GetSafeThingGuid(pMyBaseThing, $"SHIS{pFldOrder}"), pChartTitle, blocksize, tDefSource, true, "", "", "", tListCharts)
            {
                SubTitleText = "",
                PropertyBag = new ThePropertyBag { "LatestRight=true" }
            };
            AddChartDefinition(MyChart);
            var tL = AddChartControl(pMyBaseThing, MyChart, pForm, pFldOrder, pGroupTitle, pChartTitle,false, pChartBag);
            if (tL != null && tL.Count>0)
            {
                foreach (var t in tL.Keys)
                    tList[t] = tL[t];
            }
            return tList;
        }

        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddChartControl(TheThing pMyBaseThing, TheChartDefinition MyChart, TheFormInfo pForm, int pFldOrder, string pGroupTitle, string pChartTitle, bool UseCustomData = false, ThePropertyBag pChartBag = null)
        {
            if (pMyBaseThing == null || MyChart==null)
                return new cdeConcurrentDictionary<string, TheMetaDataBase>();
            AddChartDefinition(MyChart);
            var tList = new cdeConcurrentDictionary<string, TheMetaDataBase>();

            int pParent = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pChartBag, "ParentFld", "="));
            bool HideScale = TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pChartBag, "HideScale", "="));
            bool OnlyShowValue = TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pChartBag, "OnlyShowValue", "="));
            bool tDoClose = TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pChartBag, "DoClose", "="));
            int ForceTileWidth= TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pChartBag, "TileWidth", "="));
            string ScaleFactorPName=TheCommonUtils.CStr(ThePropertyBag.PropBagGetValue(pChartBag, "ScaleFactorProperty", "="));
            if (string.IsNullOrEmpty(ScaleFactorPName))
                ScaleFactorPName = "ScaleFactor";

            var tGroup = TheNMIEngine.AddSmartControl(pMyBaseThing, pForm, eFieldType.CollapsibleGroup, pFldOrder, 6, 0, pGroupTitle, null, new nmiCtrlCollapsibleGroup()
            { ParentFld = pParent, MaxTileWidth = 18, TileWidth=ForceTileWidth, IsSmall = true, NoTE = true, FontSize = 24, HorizontalAlignment = "left", DoClose = tDoClose });
            if (tGroup == null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(2345, new TSM(eEngineName.NMIService, $"Chart Group could not be created - FldOrder conflict? ({pFldOrder})", eMsgLevel.l1_Error));
                return new cdeConcurrentDictionary<string, TheMetaDataBase>();
            }
            tGroup.AddOrUpdatePlatformBag(eWebPlatform.Any, new nmiPlatBag { Hide = true });
            tGroup.AddOrUpdatePlatformBag(eWebPlatform.Desktop, new nmiPlatBag { Show = true });
            tGroup.AddOrUpdatePlatformBag(eWebPlatform.TeslaXS, new nmiPlatBag { Show = true });
            tList["Group"] = tGroup;

            ThePropertyBag tPropertyBag = new () { "NoTE=true", $"ParentFld={pFldOrder}", "HideRefresh=true", "Colors=['#52CFEA', '#AAAAAA', '#CCCCCC', '#CCCCCC', '#1aadce','#492970', '#f28f43', '#77a1e5', '#c42525', '#a6c96a']",
                    $"TileWidth={(ForceTileWidth<18?ForceTileWidth:18)}", "TileHeight=4", "ControlType=Line Chart", OnlyShowValue?"HideSeries=QValue_Min,QValue_Max":"",
                    $"DataSource={MyChart.cdeMID}" };
            if (pChartBag != null)
            {
                var tnbag = ThePropertyBag.GetSubBag(pChartBag, 1);
                if (tnbag != null && tnbag.Count > 0)
                {
                    foreach (var key in tnbag)
                    {
                        ThePropertyBag.PropBagUpdateValue(tPropertyBag, key, "=");
                    }
                }
            }
            if (!ThePropertyBag.PropBagHasValue(tPropertyBag, "ControlType", "="))
                ThePropertyBag.PropBagUpdateValue(tPropertyBag, "ControlType", "=", "Line Chart");
            if (!ThePropertyBag.PropBagHasValue(tPropertyBag, "Title", "="))
                ThePropertyBag.PropBagUpdateValue(tPropertyBag, "Title", "=", MyChart.TitleText);
            if (!ThePropertyBag.PropBagHasValue(tPropertyBag, "DataSource", "="))
                ThePropertyBag.PropBagUpdateValue(tPropertyBag, "DataSource", "=", (UseCustomData ? pMyBaseThing.EngineName + ";" : "") + MyChart.cdeMID.ToString()); //+
            var MyChartControl = AddSmartControl(pMyBaseThing, pForm, eFieldType.UserControl, pFldOrder + 10, 0, 0, pChartTitle, null, tPropertyBag);
            MyChartControl.AddOrUpdatePlatformBag(eWebPlatform.Desktop, new nmiPlatBag { Show = true });
            MyChartControl.AddOrUpdatePlatformBag(eWebPlatform.Any, new nmiPlatBag { TileWidth = (ForceTileWidth < 18 ? ForceTileWidth : 18) });
            MyChartControl.AddOrUpdatePlatformBag(eWebPlatform.Mobile, new nmiPlatBag { TileWidth = 6 });
            MyChartControl.AddOrUpdatePlatformBag(eWebPlatform.HoloLens, new nmiPlatBag { TileWidth = (ForceTileWidth < 12 ? ForceTileWidth : 12) });
            MyChartControl.AddOrUpdatePlatformBag(eWebPlatform.TeslaXS, new nmiPlatBag { TileWidth = (ForceTileWidth < 12 ? ForceTileWidth : 12) });
            if (!HideScale)
            {
                var tScale = AddSmartControl(pMyBaseThing, pForm, eFieldType.ComboBox, pFldOrder + 2, 2, 0, "Select Scale", ScaleFactorPName, new nmiCtrlComboBox() { ParentFld = pFldOrder, TileWidth = 5, FldWidth = 4, TileHeight = 1, Options = "Last Minute:LMI;Last Hour:LHO;Last 3 Hours:LH3;Last 6 Hours:LH6;Last 12 Hours:LHC;Last Day:LDA;Last Week:LWE;Last Month:LMO;Last Year:LYE;Last 15 Minutes:LSP900", DefaultValue = "LDA" });
                tScale.RegisterPropertyChangedByUX((tld, p) =>
                {
                    var tP = TheCommonUtils.cdeSplit(MyChart.DataSource, ";:;", false, false);
                    int tPageCount = 0;
                    if (tP.Length > 1) tPageCount = TheCommonUtils.CInt(tP[1]);
                    MyChart.DataSource = $"{tP[0]};:;{tPageCount};:;{p}";
                    MyChartControl?.SetUXProperty(Guid.Empty, "RefreshData=true");
                });
                tList.TryAdd("Scale", tScale);
                var tBut5 = AddSmartControl(pMyBaseThing, pForm, eFieldType.TileButton, pFldOrder + 5, 2, 0, null, null, new nmiCtrlTileButton() { ParentFld = pFldOrder, TileWidth = 1, TileHeight = 1, Value = "Refresh" });
                tBut5.RegisterUXEvent(pMyBaseThing, eUXEvents.OnClick, "refresh", (sender, para) =>
                {
                    TheProcessMessage pMsg = para as TheProcessMessage;
                    if (pMsg?.Message == null) return;
                    var tP = TheCommonUtils.cdeSplit(MyChart.DataSource, ";:;", false, false);
                    int tPageCount = 0;
                    if (tP.Length > 1) tPageCount = TheCommonUtils.CInt(tP[1]);
                    MyChart.DataSource = $"{tP[0]};:;{tPageCount};:;{TheThing.GetSafePropertyString(pMyBaseThing, ScaleFactorPName)}";
                    MyChartControl.SetUXProperty(pMsg.Message.GetOriginator(), "RefreshData=true");
                });
                tList["Refresh"]=tScale;
            }
            tList["Chart"]=MyChartControl;
            return tList;
        }


        /// <summary>
        /// Returns a Chart Definition by ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static TheChartDefinition GetChartByID(Guid id)
        {
            if (!IsInitialized() || id == Guid.Empty) return null;
            try
            {
                if (TheCDEngines.MyNMIService.MyNMIModel.MyChartScreens != null && TheCDEngines.MyNMIService.MyNMIModel.MyChartScreens.MyMirrorCache.ContainsID(id))
                    return TheCDEngines.MyNMIService.MyNMIModel.MyChartScreens.GetEntryByID(id);
            }
            catch
            {
                //ignored
            }
            return null;
        }
        #endregion

        #region Convenience Creators (HTML free)
        /// <summary>
        /// Adds a new custom wizard to the NMI Model
        /// </summary>
        /// <typeparam name="T">A class that will contain the input from the Wizard when the user clicks "Finish". A developer will have to use the callbacks to use these inputs</typeparam>
        /// <param name="pTemplateGuid">A guid for the Wizard. Can be a hardcoded guid</param>
        /// <param name="pTableReference">Guid of the Table that contains the defDataSource for the Wizard. This can be Guid.Empty if the T is new class not yet used in any existing StorageMirror</param>
        /// <param name="pDashboardID">Guid of the Dashboard the wizard should be added to. It can be the dashboard of a Thing or any other dashboard</param>
        /// <param name="pTitle">Title of the wizard</param>
        /// <param name="pPropertyBag">Control Properties of the Wizard</param>
        /// <param name="pB4ShowCallback">Callback for events before the Wizard is loaded</param>
        /// <param name="pB4InsertCallback">A callback that is called right before the new record would be inserted in to the StorageMirror of the Table referenced by the TableReference. If the cdeMID of the first parameter is set to Guid.Empty, the new record will NOT be inserted to the StorageMirror</param>
        /// <param name="pAfterInsertCallback">A callback that is called after the new record was written to the StorageMirror</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddNewTemplate<T>(Guid pTemplateGuid, Guid pTableReference, Guid pDashboardID, string pTitle, ThePropertyBag pPropertyBag = null,Action<T, TheClientInfo> pB4ShowCallback=null, Action<T, TheClientInfo> pB4InsertCallback = null, Action<T, TheClientInfo> pAfterInsertCallback = null) where T : TheDataBase, INotifyPropertyChanged, new()
        {
            TheDashboardInfo tDash = GetDashboardById(pDashboardID);
            cdeConcurrentDictionary<string, TheMetaDataBase> flds = new();
            if (tDash == null)
                return flds;

            TheFormInfo tForm = null;
            TheStorageMirror<T> SM = null;
            if (typeof(T) == typeof(TheThing))
                SM = TheCDEngines.GetStorageMirror($"{typeof(T)}") as TheStorageMirror<T>;
            else
                SM = TheCDEngines.GetStorageMirror($"{TheStorageUtilities.GenerateUniqueIDFromType(typeof(T), pTemplateGuid.ToString())}") as TheStorageMirror<T>;
            if (SM == null)
            {
                SM = new TheStorageMirror<T>(TheCDEngines.MyIStorageService)
                {
                    CacheTableName = pTemplateGuid.ToString()
                };
                SM.InitializeStore(true);
            }
            if (pB4InsertCallback != null)
            {
                SM.RegisterEvent(eStoreEvents.InsertRequested, (args) =>
                {
                    if (args?.ClientInfo?.FormID == pTemplateGuid)
                    {
                        pB4InsertCallback(args.Para as T, args.ClientInfo);
                    }
                });
            }
            SM.RegisterEvent(eStoreEvents.Inserted, (args) =>
            {
                if (args?.ClientInfo?.FormID == pTemplateGuid)
                {
                    pAfterInsertCallback?.Invoke(args.Para as T, args.ClientInfo);
                }
            });

            pPropertyBag ??= new ThePropertyBag();
            bool pIsAlwaysEmpty = !TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pPropertyBag, "AllowReuse", "="));
            ThePropertyBag.PropBagUpdateValue(pPropertyBag, "IsTemplate", "=", "true");

            ThePropertyBag.PropBagUpdateValue(pPropertyBag, "HideFromSideBar", "=", "true");
            tForm = new TheFormInfo(pTemplateGuid, eEngineName.NMIService, pTitle, $"{typeof(T).Name};:;") { DefaultView = eDefaultView.Form, IsPostingOnSubmit = true, IsAlwaysEmpty = pIsAlwaysEmpty, TileWidth = 12, TableReference = pTableReference == Guid.Empty ? pTemplateGuid.ToString() : pTableReference.ToString(), PropertyBag = pPropertyBag };
            flds["Form"] = tForm;
            AddForm(tForm);
            tForm.RegisterEvent2(eUXEvents.OnShow, (pmsg, args) =>
            {
                pB4ShowCallback?.Invoke(null, new TheClientInfo() { UserID=pmsg.CurrentUserID });
            });

            var tTitle = ThePropertyBag.PropBagGetValue(pPropertyBag, "PanelTitle", "=");
            if (string.IsNullOrEmpty(tTitle))
                tTitle = pTitle;
            int tFlags = 0x89;
            if (!string.IsNullOrEmpty(ThePropertyBag.PropBagGetValue(pPropertyBag, "Flags", "=")))
                tFlags = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pPropertyBag, "Flags", "="));
            int tACL = 0x80;
            if (!string.IsNullOrEmpty(ThePropertyBag.PropBagGetValue(pPropertyBag, "ACL", "=")))
                tACL = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pPropertyBag, "ACL", "="));
            var Cate = "Wizards";
            if (!string.IsNullOrEmpty(ThePropertyBag.PropBagGetValue(pPropertyBag, "Category", "=")))
                Cate = ThePropertyBag.PropBagGetValue(pPropertyBag, "Category", "=");
            TheDashPanelInfo MyDashPanelInfo = new (pTemplateGuid)
            {
                cdeMID = tForm.cdeMID, 
                cdeA = tACL,
                Flags = tFlags,
                PanelTitle = tTitle,
                FldOrder = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pPropertyBag, "FldOrder", "=")),
                Category = Cate,
                Description = ThePropertyBag.PropBagGetValue(pPropertyBag, "Description", "="),
                ControlClass = $"CMyForm:{{{tForm.cdeMID}}}",
            };
            MyDashPanelInfo.PropertyBag = pPropertyBag;

            ThePropertyBag.PropBagUpdateValue(MyDashPanelInfo.PropertyBag, "UXID", "=", MyDashPanelInfo.cdeMID.ToString());
            AddDashPanel(tDash, MyDashPanelInfo);
            flds["DashIcon"] = MyDashPanelInfo;

            return flds;
        }


        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddNewTemplate(TheThing pBaseThing, Guid pTableReference,string pDataSource, string pTitle, bool pIsAlwaysEmpty = true, ThePropertyBag pPropertyBag=null)
        {
            cdeConcurrentDictionary<string, TheMetaDataBase> flds = new ();
            int pTileWidth = 12;
            if (!string.IsNullOrEmpty(ThePropertyBag.PropBagGetValue(pPropertyBag, "TileWidth", "=")))
                pTileWidth = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pPropertyBag, "TileWidth", "="));
            var tForm = new TheFormInfo(TheThing.GetSafeThingGuid(pBaseThing, $"TEMPLATE:{pTableReference}"), eEngineName.NMIService, pTitle, pDataSource) { FormTitle = pTitle, DefaultView = eDefaultView.Form, IsPostingOnSubmit = true, IsAlwaysEmpty = pIsAlwaysEmpty, TileWidth = pTileWidth, TableReference = TheCommonUtils.CStr(pTableReference), PropertyBag=pPropertyBag };
            flds["Form"] = tForm;
            var tProp = new nmiDashboardTile { Visibility = false, HideFromSideBar=true, HidePinPins=true, IsPopup=TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pPropertyBag, "IsPopup", "=")) };
            flds["DashIcon"] = AddFormToThingUX(pBaseThing, tForm, "CMyForm", null, 3, 3, 0, null, null, tProp);

            return flds;
        }

        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddNewTemplate(TheThing pBaseThing, Guid pTableReference, string pTitle, bool pIsAlwaysEmpty=true, ThePropertyBag pPropertyBag = null)
        {
            return AddNewTemplate(pBaseThing, pTableReference, "TheThing;:;", pTitle, pIsAlwaysEmpty, pPropertyBag);
        }
        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddNewTemplate(Guid pTemplateGuid, TheThing pOwnerThing, Guid pTableReference, string pTitle, bool pIsAlwaysEmpty = true, int pTileWidth = 12)
        {
            return AddNewTemplate(pTemplateGuid, Guid.Empty, pOwnerThing, pTableReference, pTitle, pIsAlwaysEmpty, pTileWidth);
        }
        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddNewTemplate(Guid pTemplateGuid, Guid pDashBoardID, TheThing pOwnerThing, Guid pTableReference, string pTitle, bool pIsAlwaysEmpty = true, int pTileWidth = 12)
        {
            cdeConcurrentDictionary<string, TheMetaDataBase> flds = new ();
            var tForm = new TheFormInfo(pTemplateGuid, eEngineName.NMIService, pTitle, null) { FormTitle = pTitle, DefaultView = eDefaultView.Form, IsPostingOnSubmit = true, IsAlwaysEmpty = pIsAlwaysEmpty, TileWidth = pTileWidth, TableReference = TheCommonUtils.CStr(pTableReference) };
            flds["Form"] = tForm;
            if (pDashBoardID != Guid.Empty)
            {
                var TDash = GetDashboardById(pDashBoardID);
                if (TDash!=null)
                    flds["DashIcon"] = AddFormToThingUX(TDash, pOwnerThing, tForm, "CMyForm", null, 3, 3, 0, null, null, new nmiDashboardTile { Visibility = false });
            }
            else
                flds["DashIcon"]=AddFormToThingUX(pOwnerThing, tForm, "CMyForm", null, 3, 3, 0, null, null, new nmiDashboardTile { Visibility = false });

            return flds;
        }
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddTemplateButtons(TheThing pBaseThing, TheFormInfo pForm, int pPageNumber)
        {
            return AddTemplateButtons(pBaseThing, pForm, -1, pPageNumber, -1, null);
        }

        public static cdeConcurrentDictionary<string, TheFieldInfo> AddTemplateButtons(TheThing pBaseThing, TheFormInfo pForm, int pPrevPage, int pPageNumber, int pNextPage, string pDisplayCondition = null)
        {
            if (pForm == null || pPageNumber == 0)
                return new cdeConcurrentDictionary<string, TheFieldInfo>();
            cdeConcurrentDictionary<string, TheFieldInfo> tFlds = new ();

            int pFldWidth= TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pForm.PropertyBag, "TileWidth", "="));
            if (pFldWidth == 0)
                pFldWidth = 12;

            int grpFld = 100 * pPageNumber;
            int tParent = grpFld;
            if (GetFieldByFldOrder(pForm, grpFld) == null)
                tParent = 0;
            tFlds["LowerSpacerLine"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 89, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = tParent, TileWidth = pFldWidth, TileHeight = 1, TileFactorY = 12, ClassName = "cdeWizardSpacer" });
            tFlds["ButtonGroup"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 90, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = tParent, TileWidth = pFldWidth, TileHeight = 1 });

            var tCancel = ThePropertyBag.PropBagGetValue(pForm.PropertyBag, "CancelScreenID", "=");
            if (string.IsNullOrEmpty(tCancel))
                tCancel = $"TTS:{pForm.TableReference}";
            else
                tCancel = $"TTS:{tCancel}";
            tFlds["CancelButton"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileButton, grpFld + 92, 2, 0, "Cancel", null, new nmiCtrlTileButton { ParentFld = grpFld + 90, TileWidth = 2, OnClick = tCancel, TileHeight = 1, NoTE = true });

            tFlds["Spacer"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 94, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = grpFld + 90, TileWidth = (pPrevPage >= 0 ? (pFldWidth-6) : (pFldWidth-4)), TileHeight = 1 });
            if (pPrevPage >= 0)
                tFlds["BackButton"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileButton, grpFld + 96, pPrevPage > 0 ? 2 : 0, 0, "Back", null, new nmiCtrlTileButton { ParentFld = grpFld + 90, OnClick = (pPageNumber > 1 ? $"GRP:WizarePage:{pPrevPage}" : ""), TileWidth = 2, TileHeight = 1, NoTE = true });
            tFlds["NextButton"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileButton, grpFld + 98, 2, 0, pNextPage > 0 ? "Next" : (pNextPage<0? "Save" : "Finish"), null, new nmiCtrlTileButton { ParentFld = grpFld + 90, IsSubmit = (pNextPage <= 0), OnClick = (pNextPage > 0 ? $"GRP:WizarePage:{pNextPage}:{pDisplayCondition}" : ""), TileWidth = 2, TileHeight = 1, NoTE = true });

            return tFlds;
        }

        /// <summary>
        /// Adds a new Wizard to the NMI Model
        /// </summary>
        /// <param name="pBaseThing">Owner Thing of the Wizard</param>
        /// <param name="pTableReference">Guid of the Table that contains the defDataSource for the Wizard</param>
        /// <param name="pTitle">Title of the wizard</param>
        /// <param name="pPropertyBag">Control Properties of the Wizard</param>
        /// <param name="pB4InsertCallback">A callback that is called right before the new record would be inserted in to the StorageMirror of the Table referenced by the TableReference. If the cdeMID of the "TheThing" parameter is set to Guid.Empty, the new record will NOT be inserted to the StorageMirror</param>
        /// <param name="pAfterInsertCallback">A callback that is called after the new record was written to the StorageMirror and RegisterThing was called. This does NOT garantee that the instance of TheThing object was completely initialized!</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddNewWizard(TheThing pBaseThing, Guid pTableReference, string pTitle, ThePropertyBag pPropertyBag = null, Action<TheThing, TheClientInfo> pB4InsertCallback=null, Action<TheThing, TheClientInfo> pAfterInsertCallback = null)
        {
            if (pBaseThing == null || pTableReference == Guid.Empty)
                return new cdeConcurrentDictionary<string, TheMetaDataBase>();
            TheDashboardInfo tDash = GetEngineDashBoardByThing(pBaseThing);
            if (tDash == null)
                return new cdeConcurrentDictionary<string, TheMetaDataBase>();
            return AddNewWizard<TheThing>(TheThing.GetSafeThingGuid(pBaseThing, $"WIZ:{pTableReference}"),pTableReference,tDash.cdeMID, pTitle, pPropertyBag, pB4InsertCallback, pAfterInsertCallback);
        }

        /// <summary>
        /// Adds a new custom wizard to the NMI Model
        /// </summary>
        /// <typeparam name="T">A class that will contain the input from the Wizard when the user clicks "Finish". A developer will have to use the callbacks to use these inputs</typeparam>
        /// <param name="pWizGuid">A guid for the Wizard. Can be a hardcoded guid</param>
        /// <param name="pTableReference">Guid of the Table that contains the defDataSource for the Wizard. This can be Guid.Empty if the T is new class not yet used in any existing StorageMirror</param>
        /// <param name="pDashboardID">Guid of the Dashboard the wizard should be added to. It can be the dashboard of a Thing or any other dashboard</param>
        /// <param name="pTitle">Title of the wizard</param>
        /// <param name="pPropertyBag">Control Properties of the Wizard</param>
        /// <param name="pB4InsertCallback">A callback that is called right before the new record would be inserted in to the StorageMirror of the Table referenced by the TableReference. If the cdeMID of the first parameter is set to Guid.Empty, the new record will NOT be inserted to the StorageMirror</param>
        /// <param name="pAfterInsertCallback">A callback that is called after the new record was written to the StorageMirror</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddNewWizard<T>(Guid pWizGuid, Guid pTableReference, Guid pDashboardID, string pTitle, ThePropertyBag pPropertyBag = null, Action<T, TheClientInfo> pB4InsertCallback = null, Action<T, TheClientInfo> pAfterInsertCallback = null) where T : TheDataBase, INotifyPropertyChanged, new()
        {
            TheDashboardInfo tDash = GetDashboardById(pDashboardID);
            cdeConcurrentDictionary<string, TheMetaDataBase> flds = new ();
            if (tDash == null)
                return flds;

            TheFormInfo tForm = null;
            TheStorageMirror<T> SM = null;
            if (typeof(T) == typeof(TheThing))
                SM = TheCDEngines.GetStorageMirror($"{typeof(T)}") as TheStorageMirror<T>;
            else
                SM = TheCDEngines.GetStorageMirror($"{TheStorageUtilities.GenerateUniqueIDFromType(typeof(T), pWizGuid.ToString())}") as TheStorageMirror<T>;
            if (SM == null)
            {
                SM = new TheStorageMirror<T>(TheCDEngines.MyIStorageService)
                {
                    CacheTableName = pWizGuid.ToString()
                };
                SM.InitializeStore(true);
            }
            if (pB4InsertCallback != null)
            {
                SM?.RegisterEvent(eStoreEvents.InsertRequested, (args) =>
                {
                    if (args?.ClientInfo?.FormID == pWizGuid)
                    {
                        int tPage = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(tForm.PropertyBag, "ProcessingPage", "="));
                        if (tPage > 0)
                            tForm.SetUXProperty(args.ClientInfo.NodeID, $"SetGroup=GRP:WizarePage:{tPage}");
                        pB4InsertCallback(args.Para as T, args.ClientInfo);
                        tPage = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(tForm.PropertyBag, "FinishPage", "="));
                        if (tPage>0)
                            tForm.SetUXProperty(args.ClientInfo.NodeID, $"SetGroup=GRP:WizarePage:{tPage}");
                    }
                });
            }
            SM?.RegisterEvent(eStoreEvents.Inserted, (args) =>
            {
                if (args?.ClientInfo?.FormID == pWizGuid)
                {
                    pAfterInsertCallback?.Invoke(args.Para as T, args.ClientInfo);
                    if (((T)args.Para)?.cdeMID != Guid.Empty && args.ClientInfo != null)
                        TheCommCore.PublishToNode(args.ClientInfo.NodeID, new TSM(eEngineName.NMIService, "NMI_TTS", $"{pDashboardID}"));
                }
            });

            pPropertyBag ??= new ThePropertyBag();
            bool pIsAlwaysEmpty = !TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pPropertyBag, "AllowReuse", "="));
            ThePropertyBag.PropBagUpdateValue(pPropertyBag, "IsTemplate", "=", "true");
            ThePropertyBag.PropBagUpdateValue(pPropertyBag, "InDashboard", "=", tDash.cdeMID.ToString());
            bool bIsPopup = TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pPropertyBag, "IsPopup", "="));
            if (bIsPopup)
            {
                ThePropertyBag.PropBagUpdateValue(pPropertyBag, "HidePinPins", "=", "true");
                ThePropertyBag.PropBagUpdateValue(pPropertyBag, "CancelScreenID", "=", "CLOSE");
                ThePropertyBag.PropBagUpdateValue(pPropertyBag, "FinishScreenID", "=", "CLOSE");
                ThePropertyBag.PropBagUpdateValue(pPropertyBag, "PanelTitle", "=", "-HIDE");
            }
            else
            {
                if (string.IsNullOrEmpty(ThePropertyBag.PropBagGetValue(pPropertyBag, "CancelScreenID", "=")))
                    ThePropertyBag.PropBagUpdateValue(pPropertyBag, "CancelScreenID", "=", tDash.cdeMID.ToString());
            }

            ThePropertyBag.PropBagUpdateValue(pPropertyBag, "SideBarIconFA", "=", "&#xf0d0;");
            tForm = new TheFormInfo(pWizGuid, eEngineName.NMIService, pTitle, $"{typeof(T).Name};:;") { DefaultView = eDefaultView.Form, IsPostingOnSubmit = true, IsAlwaysEmpty = pIsAlwaysEmpty, TileWidth = 12, TableReference = pTableReference == Guid.Empty ? pWizGuid.ToString() : pTableReference.ToString(), PropertyBag = pPropertyBag };
            flds["Form"] = tForm;
            tForm.RegisterEvent2(eUXEvents.OnShow, (pmsg, para) =>
            {
                tForm.SetUXProperty(pmsg.Message.GetOriginator(), "SetGroup=GRP:WizarePage:1");
            });
            AddForm(tForm);

            var tTitle = ThePropertyBag.PropBagGetValue(pPropertyBag, "PanelTitle", "=");
            if (string.IsNullOrEmpty(tTitle))
                tTitle = pTitle;
            int tFlags = 0x89;
            if (!string.IsNullOrEmpty(ThePropertyBag.PropBagGetValue(pPropertyBag, "Flags", "=")))
                tFlags = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pPropertyBag, "Flags", "="));
            int tACL = 0x80;
            if (!string.IsNullOrEmpty(ThePropertyBag.PropBagGetValue(pPropertyBag, "ACL", "=")))
                tACL = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pPropertyBag, "ACL", "="));
            var Cate = "Wizards";
            if (!string.IsNullOrEmpty(ThePropertyBag.PropBagGetValue(pPropertyBag, "Category", "=")))
                Cate = ThePropertyBag.PropBagGetValue(pPropertyBag, "Category", "=");
            if (string.IsNullOrEmpty(ThePropertyBag.PropBagGetValue(pPropertyBag, "RenderTarget", "=")))
                ThePropertyBag.PropBagUpdateValue(pPropertyBag, "RenderTarget", "=", "HomeCenterStage");
            TheDashPanelInfo MyDashPanelInfo = new (pWizGuid)
            {
                cdeMID = tForm.cdeMID, 
                cdeA = tACL,
                Flags = tFlags,
                PanelTitle = tTitle,
                FldOrder = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pPropertyBag, "FldOrder", "=")),
                Category = Cate,
                Description = ThePropertyBag.PropBagGetValue(pPropertyBag, "Description", "="),
                ControlClass = $"CMyForm:{{{tForm.cdeMID}}}",
            };
            if (pPropertyBag != null)
                MyDashPanelInfo.PropertyBag = pPropertyBag;

            ThePropertyBag.PropBagUpdateValue(MyDashPanelInfo.PropertyBag, "UXID", "=", MyDashPanelInfo.cdeMID.ToString());
            AddDashPanel(tDash, MyDashPanelInfo);
            flds["DashIcon"] = MyDashPanelInfo;

            return flds;
        }

        /// <summary>
        /// Adds a Finish page to a wizard. The finish page will be displayed when the InsertB4 Callback is finished. in order for the finish page to show, the B4InsertCallback on a wizard has to be set
        /// </summary>
        /// <param name="pBaseThing">Base Thing</param>
        /// <param name="pForm">Form of the Wizard</param>
        /// <param name="pPageNumber">Page number of the Finish Page- should be larger than the last page of the wizard</param>
        /// <param name="pPageTitle">An optional Page title</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddWizardFinishPage(TheThing pBaseThing, TheFormInfo pForm, int pPageNumber, string pPageTitle=null)
        {
            if (pForm == null || pPageNumber == 0)
                return new cdeConcurrentDictionary<string, TheFieldInfo>();
            cdeConcurrentDictionary<string, TheFieldInfo> tFlds = AddNewWizardPage(pBaseThing, pForm, -1, pPageNumber, -1, pPageTitle);
            pForm.PropertyBag = new ThePropertyBag { $"FinishPage={pPageNumber}" };
            return tFlds;
        }

        /// <summary>
        /// Add a processing page to the Wizard. A processing page should show the progress of the work a wizard is doing. At least a "processing please wait" and possibly a waiting icon or progress bar
        /// </summary>
        /// <param name="pBaseThing">Base Thing</param>
        /// <param name="pForm">Wizard Form</param>
        /// <param name="pPageNumber">Page of the processing page. Should be larger than the last page of the wizard</param>
        /// <param name="pPageTitle">An optional Page title</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddWizardProcessPage(TheThing pBaseThing, TheFormInfo pForm, int pPageNumber, string pPageTitle=null)
        {
            if (pForm == null || pPageNumber == 0)
                return new cdeConcurrentDictionary<string, TheFieldInfo>();
            cdeConcurrentDictionary<string, TheFieldInfo> tFlds = AddNewWizardPage(pBaseThing, pForm, -1, pPageNumber, -1, pPageTitle);
            pForm.PropertyBag = new ThePropertyBag { $"ProcessingPage={pPageNumber}" };
            return tFlds;
        }


        /// <summary>
        /// Adds a new wizard page to the Form
        /// </summary>
        /// <param name="pBaseThing">Owner Thing</param>
        /// <param name="pForm">Form the Wizard page will be added to</param>
        /// <param name="pPrevPage">Back button will show this page</param>
        /// <param name="pPageNumber">Aktual Page number</param>
        /// <param name="pNextPage">Next button will show this page. If zero, the finish button will show</param>
        /// <param name="pPageTitle">Title of the Page</param>
        /// <param name="pDisplayCondition">A jump condition. "NPage:Condition" . If Condition is true, the page on next will be "NPage"</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddNewWizardPage(TheThing pBaseThing, TheFormInfo pForm, int pPrevPage, int pPageNumber, int pNextPage, string pPageTitle, string pDisplayCondition = null)
        {
            cdeConcurrentDictionary<string, TheFieldInfo> tFlds = new();
            if (pForm == null || pPageNumber == 0)
                return tFlds;

            int grpFld = 100 * pPageNumber;
            tFlds["Group"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld, 0, 0, null, null, new nmiCtrlTileGroup { TileWidth = 12, Visibility = (pPageNumber == 1), Group = $"WizarePage:{pPageNumber}" });
            if (pPageTitle != null)
                tFlds["Caption"] = AddSmartControl(pBaseThing, pForm, eFieldType.SmartLabel, grpFld + 1, 0, 0, null, null, new nmiCtrlSmartLabel { ParentFld = grpFld, NoTE = true, Text = pPageTitle, TileWidth = 12, TileHeight = 1, ContainerClassName = "cdeWizardCaption" });

            tFlds["LowerSpacerLine"] = AddWizardSpacer(pBaseThing, pForm, pPageNumber, 2, 12, 1, new nmiCtrlTileGroup { TileFactorY = 12, ClassName = "cdeWizardSpacer" });

            tFlds["LeftSide"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 10, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = grpFld, TileWidth = 4, TileHeight = 9, TileFactorY = 2, ClassName = "cdeWizardLeft" });
            tFlds["SpacerTL"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 11, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = grpFld + 10, TileWidth = 4, TileHeight = 1, TileFactorY = 4 });
            tFlds["SpacerTLL"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 12, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = grpFld + 10, TileWidth = 1, TileHeight = 9, TileFactorY = 2, TileFactorX=4 });
            tFlds["RightSide"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 50, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = grpFld, TileWidth = 8, TileHeight = 9, TileFactorY = 2, ClassName = "cdeWizardRight" });
            tFlds["SpacerTR"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 51, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = grpFld + 50, TileWidth = 8, TileHeight = 1, TileFactorY = 4 });
            tFlds["SpacerTRL"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 52, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = grpFld + 50, TileWidth = 1, TileHeight = 9, TileFactorY = 2, TileFactorX = 4 });

            //horizontal line

            tFlds["BottomSpacerLine"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 89, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = grpFld, TileWidth = 12, TileHeight = 1, TileFactorY = 12, ClassName= "cdeWizardSpacer" });

            tFlds["ButtonGroup"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 90, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = grpFld, TileWidth = 12, TileHeight = 1 });
            if (pPrevPage < 0 && pNextPage < 0)
            {
                tFlds["Spacer"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 94, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = grpFld + 90, TileWidth = 8, TileHeight = 1 });
            }
            else
            {
                var tCancel = ThePropertyBag.PropBagGetValue(pForm.PropertyBag, "CancelScreenID", "=");
                if (!string.IsNullOrEmpty(tCancel))
                    tCancel = $"TTS:{tCancel}";
                else
                    tCancel = $"TTS:{pForm.TableReference}";
                tFlds["CancelButton"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileButton, grpFld + 92, 2, 0, "Cancel", null, new nmiCtrlTileButton { ParentFld = grpFld + 90, TileWidth = 2, OnClick = tCancel, TileHeight = 1, NoTE = true });

                tFlds["Spacer"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + 94, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = grpFld + 90, TileWidth = 6, TileHeight = 1 });
                tFlds["BackButton"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileButton, grpFld + 96, pPrevPage > 0 ? 2 : 0, 0, "Back", null, new nmiCtrlTileButton { ParentFld = grpFld + 90, OnClick = (pPageNumber > 1 ? $"GRP:WizarePage:{pPrevPage}" : ""), TileWidth = 2, TileHeight = 1, NoTE = true });
                var tFinish = ThePropertyBag.PropBagGetValue(pForm.PropertyBag, "FinishScreenID", "=");
                if (!string.IsNullOrEmpty(tFinish))
                    tFinish = $"TTS:{tFinish}";
                tFlds["NextButton"] = AddSmartControl(pBaseThing, pForm, eFieldType.TileButton, grpFld + 98, 2, 0, pNextPage > 0 ? "Next" : "Finish", null, new nmiCtrlTileButton { ParentFld = grpFld + 90, IsSubmit = (pNextPage <= 0), OnClick = (pNextPage > 0 ? $"GRP:WizarePage:{pNextPage}:{pDisplayCondition}" : tFinish), TileWidth = 2, TileHeight = 1, NoTE = true });
            }
            return tFlds;
        }

        /// <summary>
        /// Adds a new control to a Wizard
        /// </summary>
        /// <param name="pMyBaseThing">Owner of the Wizard - can be null if the wizard is not owned by a Thing</param>
        /// <param name="pForm">Wizard TheFormInfo</param>
        /// <param name="tType">Control type to be added</param>
        /// <param name="WizardPage">Page of the wizard to add this Explainer to</param>
        /// <param name="fldOrder">Position in the control area (1-4)</param>
        /// <param name="flags">Flags of the control</param>
        /// <param name="pACL">Visibility ACL</param>
        /// <param name="pHeader">Label of the Control</param>
        /// <param name="OnUpdateName">Property Name for Data Binding</param>
        /// <param name="BagItems">Additional Property Items</param>
        /// <param name="ExplainerBag">Additional Property Bag for the Explainer Items</param>
        /// <returns></returns>
        public static TheFieldInfo AddWizardControl(TheThing pMyBaseThing, TheFormInfo pForm, eFieldType tType,int WizardPage, int fldOrder, int flags, int pACL, string pHeader, string OnUpdateName, ThePropertyBag BagItems=null, ThePropertyBag ExplainerBag=null)
        {
            var tBag = new ThePropertyBag();
            if (BagItems!=null)
                tBag= BagItems;
            ThePropertyBag.PropBagUpdateValue(tBag, "ParentFld", "=", (WizardPage * 100 + 50).ToString());
            if (!string.IsNullOrEmpty(ThePropertyBag.PropBagGetValue(tBag, "Explainer", "=")))
                AddSmartControl(pMyBaseThing, pForm, eFieldType.SmartLabel, WizardPage * 100 + 13 + fldOrder, 0, pACL, null, null, new nmiCtrlSmartLabel { ParentFld= WizardPage * 100 + 10, Text = ThePropertyBag.PropBagGetValue(tBag, "Explainer", "="), ClassName= "cdeWizardExplainer", TileWidth=7, TileFactorX=2, NoTE=true, MergeBag=ExplainerBag });
            return AddSmartControl(pMyBaseThing, pForm, tType, WizardPage * 100 + 53 + fldOrder, flags, pACL, pHeader, OnUpdateName, tBag);
        }

        /// <summary>
        /// Adds a small blank Spacer Tile
        /// </summary>
        /// <param name="pBaseThing">Thing owning the spacer</param>
        /// <param name="pForm">Target Form of the spacer</param>
        /// <param name="pPageNumber">Page number the spacer should be put on</param>
        /// <param name="FldOrder">FldNumber of the spacer</param>
        /// <param name="width">Width of the spacer</param>
        /// <param name="height">Height of the spacer</param>
        /// <param name="pPropertyBag">Additional properties for the spacer</param>
        /// <returns></returns>
        public static TheFieldInfo AddWizardSpacer(TheThing pBaseThing, TheFormInfo pForm, int pPageNumber, int FldOrder, int width, int height, ThePropertyBag pPropertyBag)
        {
            int grpFld = 100 * pPageNumber;
            var fld = AddSmartControl(pBaseThing, pForm, eFieldType.TileGroup, grpFld + FldOrder, 0, 0, null, null, new nmiCtrlTileGroup { ParentFld = grpFld, TileWidth = width, TileHeight = height });
            fld.PropertyBag = pPropertyBag;
            return fld;
        }

        /// <summary>
        /// Adds an explainer Text to a Wizard
        /// </summary>
        /// <param name="pMyBaseThing">Owner of the Wizard - can be null if the wizard is not owned by a Thing</param>
        /// <param name="pForm">Wizard TheFormInfo</param>
        /// <param name="WizardPage">Page of the wizard to add this Explainer to</param>
        /// <param name="fldOrder">Position in the explainer (1-4)</param>
        /// <param name="pACL">Visibility ACL</param>
        /// <param name="pText">Text of the Explainer</param>
        /// <param name="BagItems">Additional Property Items</param>
        /// <returns></returns>
        public static TheFieldInfo AddWizardExplainer(TheThing pMyBaseThing, TheFormInfo pForm, int WizardPage, int fldOrder, int pACL, string pText, ThePropertyBag BagItems = null)
        {
            var tBag = new nmiCtrlSmartLabel { ParentFld = WizardPage * 100 + 10, Text = pText, ClassName = "cdeWizardExplainer", TileWidth = 7, TileFactorX=2, NoTE = true };
            if (BagItems != null)
                BagItems.MergeBag(tBag);
            else
                BagItems = tBag;
            return AddSmartControl(pMyBaseThing, pForm, eFieldType.SmartLabel, WizardPage * 100 + 13 + fldOrder, 0, pACL, null, null, BagItems);
        }

        public static TheFormInfo AddLiveTagTable(TheThing pMyBaseThing, string pDeviceType, string pTitle)
        {
            return AddLiveTagTable(pMyBaseThing, pDeviceType, pTitle, "");
        }
        /// <summary>
        /// Creates a new CMyForm of Live Tags for a given Device Type
        /// </summary>
        /// <param name="pMyBaseThing"></param>
        /// <param name="pDeviceType"></param>
        /// <param name="pTitle"></param>
        /// <param name="pCategory">Header of the Group</param>
        /// <returns></returns>
        public static TheFormInfo AddLiveTagTable(TheThing pMyBaseThing, string pDeviceType, string pTitle, string pCategory)
        {
            var tFld = AddLiveTagTable(pMyBaseThing, pDeviceType, pTitle, pCategory, 0xF0);
            return tFld?["Form"] as TheFormInfo;
        }

        /// <summary>
        /// Creates a new default LiveTag Table and returns a dictionary of string, TheMetaDataBase that can be casted:
        /// "Form" TheFormInfo; "DashIcon" TheDashPanelInfo of the icon in the Dashboard. All columns are mapped via "COL:FldOrder" as TheFieldInfo
        /// </summary>
        /// <param name="pMyBaseThing"></param>
        /// <param name="pDeviceType"></param>
        /// <param name="pTitle"></param>
        /// <param name="pCategory"></param>
        /// <param name="pACL"></param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddLiveTagTable(TheThing pMyBaseThing, string pDeviceType, string pTitle, string pCategory, int pACL)
        {
            var tFlds = new cdeConcurrentDictionary<string, TheMetaDataBase>();
            string tSource = string.Format("TheThing;:;0;:;True;:;EngineName={0}", pMyBaseThing.EngineName);
            if (!string.IsNullOrEmpty(pDeviceType))
                tSource = $"TheThing;:;0;:;True;:;DeviceType={pDeviceType};EngineName={pMyBaseThing.EngineName}";
            else
                pDeviceType = pMyBaseThing.EngineName;

            TheFormInfo tLiveTagForm = new (TheThing.GetSafeThingGuid(pMyBaseThing, pDeviceType + "_ID"), eEngineName.NMIService, pTitle, tSource) { IsNotAutoLoading = true, TileWidth = 12 };
            tFlds["Form"] = tLiveTagForm;
            tFlds["DashIcon"] = AddFormToThingUX(pMyBaseThing, tLiveTagForm, "CMyTable", pTitle, 1, 3, pACL, pCategory, null, new ThePropertyBag { "TileThumbnail=FA5:f0ce" });
            var tCols = new List<TheFieldInfo> {
                {  new TheFieldInfo() { FldOrder=2,DataItem="MyPropertyBag.DontMonitor.Value",Flags=2,Type=eFieldType.SingleCheck,Header="###Disable###",FldWidth=1,TileWidth=3, TileHeight=1 }},
                {  new TheFieldInfo() { FldOrder=3,DataItem="MyPropertyBag.IsActive.Value",Flags=0,Type=eFieldType.SingleCheck,Header="###Is Active###",FldWidth=1,TileWidth=3, TileHeight=1 }},
                {  new TheFieldInfo() { FldOrder=4,DataItem="MyPropertyBag.FriendlyName.Value",Flags=0,Type=eFieldType.SingleEnded,Header="###Name###",FldWidth=3,  TileWidth=12, TileHeight=1 }},
                {  new TheFieldInfo() { FldOrder=6,DataItem="MyPropertyBag.SampleRate.Value",Flags=2,Type=eFieldType.Number,Header="###Samplerate###",FldWidth=2,TileWidth=6, TileHeight=1  }},

                {  new TheFieldInfo(pMyBaseThing,"Value",13,0x42,0) { Type=eFieldType.SingleEnded,Header="###Value###",FldWidth=2 }},
                {  new TheFieldInfo(pMyBaseThing,"SourceTimeStamp",14, 0x40,0) { Type=eFieldType.DateTime,Header="###Time-Stamp###",FldWidth=2 }},
                {  new TheFieldInfo() { FldOrder=21,DataItem="MyPropertyBag.ServerName.Value",Flags=0x10,Type=eFieldType.SingleEnded,Header="###Server Name###",FldWidth=2 }},
            };
            foreach (var t in tCols)
                tFlds[$"COL:{t.FldOrder}"] = t;
            AddFields(tLiveTagForm, tCols);

            tCols = AddTableButtons(tLiveTagForm, true, 1000, 0xA2);
            foreach (var t in tCols)
                tFlds[$"COL:{t.FldOrder}"] = t;

            return tFlds;
        }
        /// <summary>
        /// Creates the default NMI Style Form for a Thing. Returns a Dictionary of TheMetaDataBase items that can be casted to:
        /// "Form" (TheFormInfo), "DashIcon" (TheDashPanelInfo), "Header" (TheFieldInfo)
        /// </summary>
        /// <param name="pMyBaseThing">Owning Thing</param>
        /// <param name="pDashTileCaption">Title of the Dashboard</param>
        /// <param name="pMaxFormSize"></param>
        /// <param name="pUniqueStringID"></param>
        /// <param name="pDashIconUpdateName"></param>
        /// <param name="pACL"></param>
        /// <param name="pCate"></param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddStandardForm(TheThing pMyBaseThing, string pDashTileCaption, int pMaxFormSize = 18, string pUniqueStringID = null, string pDashIconUpdateName = null, int pACL = 0, string pCate = null)
        {
            return AddStandardForm(pMyBaseThing, pDashTileCaption, pUniqueStringID, pACL, new nmiStandardForm { IconUpdateName = pDashIconUpdateName, Category = pCate, MaxTileWidth = pMaxFormSize });
        }
        /// <summary>
        /// Creates the default NMI Style Form for a Thing. Returns a Dictionary of TheMetaDataBase items that can be casted to:
        /// "Form" (TheFormInfo), "DashIcon" (TheDashPanelInfo), "Header" (TheFieldInfo)
        /// </summary>
        /// <param name="pMyBaseThing">Owning Thing</param>
        /// <param name="pDashTileCaption">Title of the Dashboard</param>
        /// <param name="pUniqueStringID">A unique ID that will be stored in TheThing to retreive the form</param>
        /// <param name="pACL">Access Level for the form</param>
        /// <param name="pProperties">Property Bag with additional properties for the AddStandardForm</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddStandardForm(TheThing pMyBaseThing, string pDashTileCaption, string pUniqueStringID, int pACL,ThePropertyBag pProperties=null)
        {
            if (pUniqueStringID == null)
                pUniqueStringID = pMyBaseThing.cdeMID.ToString();
            else
            {
                if (TheCommonUtils.CGuid(pUniqueStringID) == Guid.Empty)
                    pUniqueStringID = TheThing.GetSafeThingGuid(pMyBaseThing, pUniqueStringID).ToString();
            }
            bool IsFacePlate = false;
            string tFormTitle = pDashTileCaption;
            string pSensorFace = "/pages/ThingFace.html";
            if (string.IsNullOrEmpty(pDashTileCaption))
            {
                pDashTileCaption = $"{pMyBaseThing.DeviceType}: {pMyBaseThing.FriendlyName}";
                tFormTitle = pDashTileCaption;
            }
            else
            {
                if (pDashTileCaption.StartsWith("FACEPLATE"))
                {
                    IsFacePlate = true;
                    if (pDashTileCaption.Split(':').Length > 1)
                        pSensorFace = pDashTileCaption.Split(':')[1];
                    pDashTileCaption = "<$Loading$>";
                    tFormTitle = $"{pMyBaseThing.DeviceType}: {pMyBaseThing.FriendlyName}";

                    if (string.IsNullOrEmpty(TheThing.GetSafePropertyString(pMyBaseThing, "StateSensorIcon")))
                        TheThing.SetSafePropertyString(pMyBaseThing, "StateSensorIcon", "/Images/iconToplogo.png");
                }
            }
            bool bUseMargin = TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pProperties, "UseMargin"));
            string addDots = IsFacePlate ? ".." : "";
            string pCate = ThePropertyBag.PropBagGetValue(pProperties, "Category");
            if (pCate == null)
            {
                if (pMyBaseThing.DeviceType == "IBaseEngine")
                    pCate = TheNMIEngine.GetNodeForCategory();
                else
                    pCate = $"{addDots}{pMyBaseThing.DeviceType}";
            }
            else
            {
                if (!string.IsNullOrEmpty(pCate))
                    pCate = $"{addDots}{pCate}";
            }
            int pFormSize = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pProperties, "TileWidth"));
            int pMaxFormSize = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pProperties, "MaxTileWidth"));
            if (pMaxFormSize < 6 && pMaxFormSize > 0)
                pMaxFormSize = 6;
            else if (pMaxFormSize < 0)
                pMaxFormSize = 0;
            cdeConcurrentDictionary<string, TheMetaDataBase> block = new ();
            TheFormInfo tMyLiveForm = new(TheCommonUtils.CGuid(pUniqueStringID), eEngineName.NMIService, null, string.Format("TheThing;:;0;:;True;:;cdeMID={0}", pMyBaseThing.cdeMID))
            {
                DefaultView = eDefaultView.Form,
                PropertyBag = new nmiCtrlFormView {/* MaxTileWidth = pMaxFormSize, */Background = "rgba(255, 255, 255, 0.04)", HideCaption = true, UseMargin = bUseMargin },
                ModelID = "StandardForm"
            };
            if (pMaxFormSize > 6)
            {
                tMyLiveForm.AddOrUpdatePlatformBag(eWebPlatform.Mobile, new nmiPlatBag { MaxTileWidth = 6 });
                tMyLiveForm.AddOrUpdatePlatformBag(eWebPlatform.HoloLens, new nmiPlatBag { MaxTileWidth = 12 });
            }
            block["Form"] = tMyLiveForm;
            block["DashIcon"] = TheNMIEngine.AddFormToThingUX(pMyBaseThing, tMyLiveForm, "CMyForm", pDashTileCaption, 1, 3, pACL, pCate, ThePropertyBag.PropBagGetValue(pProperties, "IconUpdateName"), new ThePropertyBag() { "RenderTarget=HomeCenterStage" });

            var tF = TheNMIEngine.AddSmartControl(pMyBaseThing, tMyLiveForm, eFieldType.CollapsibleGroup, 1, 0, pACL, tFormTitle, null, new nmiCtrlCollapsibleGroup { IsSmall = true, AllowHorizontalExpand = (pMaxFormSize > 0), LabelClassName = "cdeTileGroupHeaderSmall SensorGroupLabel", MaxTileWidth = pMaxFormSize, TileWidth=pFormSize, LabelForeground = "white", UseMargin = bUseMargin });
            block["Header"] = tF;
            if (pMaxFormSize > 6)
            {
                tF?.AddOrUpdatePlatformBag(eWebPlatform.Mobile, new nmiPlatBag { MaxTileWidth = 6 });
                tF?.AddOrUpdatePlatformBag(eWebPlatform.HoloLens, new nmiPlatBag { MaxTileWidth = 12 });
            }
            if (IsFacePlate)
            {
                (block["DashIcon"] as TheDashPanelInfo).PropertyBag = new nmiDashboardTile { TileWidth = 4, TileHeight = 3, HTMLUrl = pSensorFace };   
                block["StatusLevel"] = TheNMIEngine.AddSmartControl(pMyBaseThing, tMyLiveForm, eFieldType.StatusLight, 99999, 0, 0, null, "StatusLevel", new TheNMIBaseControl { NoTE = true, TileWidth = 1, TileHeight = 1, TileFactorX = 2, TileFactorY = 2, RenderTarget = "VSSTATLGHT%cdeMID%" });
            }
            return block;
        }

        /// <summary>
        /// This method creates an essential dashboard for plugins that have multiple sub-devices and a table that allows to add and remove new devices.
        /// </summary>
        /// <param name="pBaseThing">TheThing that owns this Dashboard</param>
        /// <param name="pID">A unique ID for the Dashboard. Can be the pBaseThing.cdeMID if its the only dashboard of the owner plugin</param>
        /// <param name="pTitle">A title for the dashboard</param>
        /// <param name="pFilter">A custom filter for the plugin. If omitted (set to zero) all items of the owner plugin will be used</param>
        /// <param name="pFldOrder">A ordering idex for the items in the plugin</param>
        /// <param name="pFlag">Flags of the Dashboard (see TheFormInfo Flags)</param>
        /// <param name="pACL">Access Level for the Dashboard</param>
        /// <param name="category">a Category for the dashboard</param>
        /// <param name="pCustomCommand">if this is set, the Refresh Dashboard fires this command to the owner plugin</param>
        /// <returns></returns>
        public static TheFormInfo CreateEngineForms(ICDEThing pBaseThing, Guid pID, string pTitle, string pFilter, int pFldOrder, int pFlag, int pACL, string category, string pCustomCommand)
        {
            var tFlds=CreateEngineForms(pBaseThing?.GetBaseThing(), pID, pTitle, pFilter, pFldOrder, pFlag, pACL, category, pCustomCommand, null);
            return tFlds?.cdeSafeGetValue("Form") as TheFormInfo;
        }

        /// <summary>
        /// This method creates an essential dashboard for plugins that have multiple sub-devices and a table that allows to add and remove new devices.
        /// </summary>
        /// <param name="pBaseThing">TheThing that owns this Dashboard</param>
        /// <param name="pID">A unique ID for the Dashboard. Can be the pBaseThing.cdeMID if its the only dashboard of the owner plugin</param>
        /// <param name="pTitle">A title for the dashboard</param>
        /// <param name="pFilter">A custom filter for the plugin. If omitted (set to zero) all items of the owner plugin will be used</param>
        /// <param name="pFldOrder">A ordering idex for the items in the plugin</param>
        /// <param name="pFlag">Flags of the Dashboard (see TheFormInfo Flags)</param>
        /// <param name="pACL">Access Level for the Dashboard</param>
        /// <param name="category">a Category for the dashboard</param>
        /// <param name="pCustomCommand">if this is set, the Refresh Dashboard fires this command to the owner plugin</param>
        /// <param name="AddAddress">if true the Table of things contains an address field</param>
        /// <param name="pDeviceTypeOptions">List of device types for the table of things. Semicolon separated.</param>
        /// <param name="pDeviceTypeDefault">Default Device Type for new entries in the table of things</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string,TheMetaDataBase> CreateEngineForms(TheThing pBaseThing, Guid pID, string pTitle, string pFilter, int pFldOrder, int pFlag, int pACL, string category, string pCustomCommand, bool AddAddress, string pDeviceTypeOptions = null, string pDeviceTypeDefault = null)
        {
            return CreateEngineForms(pBaseThing, pID, pTitle, pFilter, pFldOrder, pFlag, pACL, category, pCustomCommand, new TheTableParameters
            {
                AddAddress = AddAddress,
                DeviceTypeOptions = pDeviceTypeOptions,
                DeviceTypeDefault = pDeviceTypeDefault,
            }); 
        }

        /// <summary>
        /// This method creates an essential dashboard for plugins that have multiple sub-devices and a table that allows to add and remove new devices.
        /// </summary>
        /// <param name="pBaseThing">TheThing that owns this Dashboard</param>
        /// <param name="pID">A unique ID for the Dashboard. Can be the pBaseThing.cdeMID if its the only dashboard of the owner plugin</param>
        /// <param name="pTitle">A title for the dashboard</param>
        /// <param name="pFilter">A custom filter for the plugin. If omitted (set to zero) all items of the owner plugin will be used</param>
        /// <param name="pFldOrder">A ordering idex for the items in the plugin</param>
        /// <param name="pFlag">Flags of the Dashboard (see TheFormInfo Flags)</param>
        /// <param name="pACL">Access Level for the Dashboard</param>
        /// <param name="category">a Category for the dashboard</param>
        /// <param name="pCustomCommand">if this is set, the Refresh Dashboard fires this command to the owner plugin</param>
        /// <param name="tableParams">Additional parameters to customize the table that lists all things for this plugin. Can be null to use defaults.</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheMetaDataBase> CreateEngineForms(TheThing pBaseThing, Guid pID, string pTitle, string pFilter, int pFldOrder, int pFlag, int pACL, string category, string pCustomCommand, TheTableParameters tableParams)
        {
            var tRes = new cdeConcurrentDictionary<string, TheMetaDataBase>();
            TheDashboardInfo tDash = GetEngineDashBoardByThing(pBaseThing);
            if (tDash == null || pBaseThing == null) return tRes;
            string tEngineName = TheThing.GetSafePropertyString(pBaseThing, "EngineName");
            if (string.IsNullOrEmpty(pFilter))
            {
                if (string.IsNullOrEmpty(tEngineName))
                    return tRes;
                pFilter = "EngineName=" + tEngineName;
            }
            if (pACL < 0)
                pACL = pBaseThing.cdeA;
            tRes["DashInfo"] = tDash;
            TheFormInfo tAllDevices = new () { cdeMID = pID, FormTitle = pTitle, defDataSource = "TheThing;:;0;:;True;:;" + pFilter, DefaultView = 0, cdeO = pBaseThing.cdeMID, cdeA = pACL };
            tRes["Form"] = tAllDevices;
            tRes["DashIcon"] = AddFormToThingUX(pBaseThing, tAllDevices, "CMyTable",pTitle, pFldOrder, pFlag, pACL, category, null, pTitle.StartsWith("<i")?null:new ThePropertyBag { "TileThumbnail=FA5:f0ce" });
            var tCols=AddCommonTableColumns(pBaseThing, tAllDevices, tableParams);
            foreach (var t in tCols.Keys)
                tRes[t] = tCols[t];
            tRes["About"]= AddAboutButton(pBaseThing, tDash, null, true, pCustomCommand, pACL);
            return tRes;
        }


        /// <summary>
        /// Specifies optional parameters to AddCommonTableColumns.
        /// </summary>
        public class TheTableParameters
        {
            /// <summary>
            /// List of device types for the table of things.
            /// </summary>
            public string DeviceTypeOptions { get; set; }
            /// <summary>
            /// Default Device Type for new entries in the table of things
            /// </summary>
            public string DeviceTypeDefault { get; set; }
            /// <summary>
            /// If true the Table of things contains an address field.
            /// </summary>
            public bool AddAddress { get; set; }
            /// <summary>
            /// If true, adds a column with the name of the node on which the things in the table are located. Useful primarily in conjunction with global/remote things.
            /// </summary>
            public bool AddNodeName { get; set; }
            /// <summary>
            /// Access Level for the Dashboard
            /// </summary>
            public int ACL { get; set; } = 0x80;
            /// <summary>
            /// If true, the table will have a details button that leads to the thing's form/dashboard
            /// </summary>
            public bool AddDetailsButton { get; set; } = true;
            /// <summary>
            /// A ordering idex for the items in the plugin
            /// </summary>
            public int FldOrder { get; set; } = 1000;
            /// <summary>
            /// Flags of the Dashboard (see TheFormInfo Flags)
            /// </summary>
            public int Flags { get; set; } = 0xA2;
        }

        /// <summary>
        /// Adds the common table columns to a table: "Details (if not disabled), FriendlyName, StatusLevel,DeviceType (if set), Address (if not disabled), Delete Button"
        /// Only tables based on TheThings are supported
        /// </summary>
        /// <param name="MyBaseThing">>TheThing that owns this Dashboard</param>
        /// <param name="pTargetForm">The form that holds the table.</param>
        /// <param name="pDeviceTypeOptions">List of device types for the table of things. Semicolon separated.</param>
        /// <param name="pDeviceTypeDefault">Default Device Type for new entries in the table of things</param>
        /// <param name="AddAddress">If False, Address column will not be included</param>
        /// <param name="AddDetailsButton">If False, a Details button will not be included</param>
        /// <param name="pFldOrder">A ordering idex for the items in the plugin</param>
        /// <param name="pFlags">Flags of the Dashboard (see TheFormInfo Flags)</param>
        /// <param name="pACL">Access Level for the Dashboard</param>
        /// <returns>A dictionary with the fields that have been added.</returns>
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddCommonTableColumns(TheThing MyBaseThing, TheFormInfo pTargetForm, string pDeviceTypeOptions = null, string pDeviceTypeDefault = null, bool AddAddress = true, bool AddDetailsButton = true, int pFldOrder = 1000, int pFlags = 0xA2, int pACL = 0x80)
        {
            return AddCommonTableColumns(MyBaseThing, pTargetForm, new TheTableParameters
            {
                DeviceTypeOptions = pDeviceTypeOptions,
                DeviceTypeDefault = pDeviceTypeDefault,
                AddAddress = AddAddress,
                AddDetailsButton = AddDetailsButton,
                FldOrder = pFldOrder,
                Flags = pFlags,
                ACL = pACL,
            });
        }

        /// <summary>
        /// Adds the common table columns to a table: "Details (if not disabled), FriendlyName, StatusLevel,DeviceType (if set), Address (if not disabled), Delete Button"
        /// Only tables based on TheThings are supported
        /// </summary>
        /// <param name="MyBaseThing">>TheThing that owns this Dashboard</param>
        /// <param name="pTargetForm">The form that holds the table.</param>
        /// <param name="tableParams">Additional parameters to customize the table. Can be null to use defaults.</param>
        /// <returns>A dictionary with the fields that have been added.</returns>
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddCommonTableColumns(TheThing MyBaseThing, TheFormInfo pTargetForm, TheTableParameters tableParams)
        {
            var tList = new cdeConcurrentDictionary<string, TheFieldInfo>();
            if (pTargetForm == null || MyBaseThing == null) return tList;
            tableParams ??= new TheTableParameters();
            tList["Status"] = AddSmartControl(MyBaseThing, pTargetForm, eFieldType.StatusLight, 20, 0x40, 0x0, "Status", "StatusLevel", new TheNMIBaseControl() { FldWidth = 1 });

            tList["Name"] = AddSmartControl(MyBaseThing, pTargetForm, eFieldType.SingleEnded, 10, 2, 0, "###Friendly Name###", "FriendlyName", new nmiCtrlSingleEnded { FldWidth = 3 });
            if (!string.IsNullOrEmpty(tableParams.DeviceTypeOptions) || !string.IsNullOrEmpty(tableParams.DeviceTypeDefault))
                tList["DeviceType"] = AddSmartControl(MyBaseThing, pTargetForm, eFieldType.ComboBox, 30, !string.IsNullOrEmpty(tableParams.DeviceTypeOptions) ? 2 : 0, tableParams.ACL, "###Device Type###", "DeviceType", new nmiCtrlComboBox { WriteOnce = true, Options = tableParams.DeviceTypeOptions, DefaultValue = tableParams.DeviceTypeDefault, FldWidth = 2 });
            if (tableParams.AddAddress)
                tList["Address"] = AddSmartControl(MyBaseThing, pTargetForm, eFieldType.SingleEnded, 40, 2, tableParams.ACL, "###Address###", "Address", new nmiCtrlSingleEnded { FldWidth = 2 });
            if (tableParams.AddNodeName)
            {
                tList["NodeName"] = AddSmartControl(MyBaseThing, pTargetForm, eFieldType.SingleEnded, 41, 0, 0xFE, "###Managed on Node###", null, new nmiCtrlSingleEnded { DataItem = "cdeN", FldWidth = 2 });
            }
            var tFlds = AddTableButtons(pTargetForm, tableParams.AddDetailsButton, tableParams.FldOrder, tableParams.Flags, tableParams.ACL);
            if (tFlds != null)
            {
                foreach (TheFieldInfo tf in tFlds)
                    tList[$"COL:{tf.FldOrder}"] = tf;
            }
            return tList;
        }


        /// <summary>
        /// Adds a Delete/Safe Button to a given Form/Table
        /// </summary>
        /// <param name="pTargetForm">The form to add the button to</param>
        /// <param name="AddDetailsButton">Add the details button to the Table. BEWARE: It will use FldOrder 1</param>
        /// <param name="pFldOrder">FldOrder of the Button. Default is 100</param>
        /// <param name="pFlags">Flags of the Button - default is 0x82 (Only show on First Node)</param>
        /// <param name="pACL">Access Level of the Button - default is 0x80 (Administrator)</param>
        /// <returns>TheFieldInfo if the button was successfully added</returns>
        public static List<TheFieldInfo> AddTableButtons(TheFormInfo pTargetForm, bool AddDetailsButton = false, int pFldOrder = 100, int pFlags = 0x82, int pACL = 0x80)
        {
            List<TheFieldInfo> tList = new ();
            if (AddDetailsButton)
            {
                tList.Add((pFlags & 0x20) != 0 ?
                    new TheFieldInfo() { FldOrder = 1, Flags = pFlags, Type = eFieldType.FormButton, FldWidth=1, PropertyBag = new nmiCtrlTileButton { ClassName = "cdeTableButton", Thumbnail="FA3:f022", OnClick = "TTS:<%cdeMID%>", TileHeight = 1, TileWidth = 1 } } :
                    new TheFieldInfo() { FldOrder = 1, DataItem = "CDE_DETAILS", Flags = pFlags, Type = eFieldType.FormButton, FldWidth=1, PropertyBag = new nmiCtrlTileButton { ClassName = "cdeTableButton", TileHeight = 1, TileWidth = 1 } });
            }
            var t = new TheFieldInfo() { cdeA = pACL, FldOrder = pFldOrder, DataItem = "CDE_DELETE", Flags = pFlags, Type = eFieldType.FormButton, Header = " ", TileWidth = 1, TileHeight = 1, FldWidth=1 };
            t.AddOrUpdatePlatformBag(eWebPlatform.HoloLens, new nmiPlatBag { Hide = true });
            tList.Add(t);
            if (AddFields(pTargetForm, tList))
                return tList;
            return new List<TheFieldInfo>();
        }

        /// <summary>
        /// New V4.01: Adds a Page Break in the Dashboard Tiles
        /// </summary>
        /// <param name="pBaseThing">Owner of the Dashboard</param>
        /// <param name="pDashboard">Dashboard ID - if null the Dashboard od the BaseThing is used</param>
        /// <param name="pCategoryOveride">Default is ".A" to create a Break between Thing Instance Tiles and the node tiles. By overwriting this, breaks can be entered between any Caterories</param>
        /// <returns></returns>
        public static TheDashPanelInfo AddTileBreak(TheThing pBaseThing, TheDashboardInfo pDashboard=null, string pCategoryOveride = ".A")
        {
            if (pBaseThing == null) return null;
            pDashboard ??= GetDashboardByOwner(pBaseThing.cdeMID);
            var t = new TheDashPanelInfo(pBaseThing)
            {
                PanelTitle = "-HIDE",
                FldOrder = 5,
                Category = $"{pCategoryOveride}-NONE",
                ControlClass = "CMyInfo",
                PropertyBag = new ThePropertyBag() { "TileHeight=0", "TileWidth=0", "CategoryClassName=cdeTileBreak" },
            };
            var tg = TheNMIEngine.AddDashPanel(pDashboard, t);
            if (Guid.Empty != tg)
                return t;
            return null;
        }

        private static int StatusFldOrder = 10;

        /// <summary>
        /// Adds the standard about button to a plugin
        /// </summary>
        /// <param name="pBaseThing"></param>
        /// <param name="bIncludeRefreshDash"></param>
        /// <param name="pCustomCommand"></param>
        /// <param name="pACL"></param>
        /// <param name="pRefreshText"></param>
        /// <returns></returns>
        public static TheFieldInfo AddAboutButton(TheThing pBaseThing, bool bIncludeRefreshDash = false, string pCustomCommand = "", int pACL = 0, string pRefreshText = "Refresh Dashboard")
        {
            return AddAboutButton(pBaseThing, null, null, bIncludeRefreshDash, pCustomCommand, pACL, pRefreshText);
        }
        /// <summary>
        /// Adds the standard about button to a plugin
        /// </summary>
        /// <param name="pBaseThing"></param>
        /// <param name="pDash"></param>
        /// <param name="pCate"></param>
        /// <param name="bIncludeRefreshDash"></param>
        /// <param name="pCustomCommand"></param>
        /// <param name="pACL">Access Level for the Refresh Button</param>
        /// <param name="pRefreshText"></param>
        /// <returns></returns>
        public static TheFieldInfo AddAboutButton(TheThing pBaseThing, TheDashboardInfo pDash, string pCate = null, bool bIncludeRefreshDash = false, string pCustomCommand = "", int pACL = 0, string pRefreshText = "###Refresh Dashboard###")
        {
            var tBlock = AddAboutButton4(pBaseThing,pDash,pCate, bIncludeRefreshDash,false, pCustomCommand,pACL,pRefreshText);
            return tBlock?.cdeSafeGetValue("AboutButton") as TheFieldInfo;
        }
        public static cdeConcurrentDictionary<string, TheMetaDataBase> AddAboutButton4(TheThing pBaseThing, TheDashboardInfo pDash, string pCate = null, bool bIncludeRefreshDash = false, bool bHideRefreshButton = false, string pCustomCommand = "", int pACL = 0, string pRefreshText = "###Refresh Dashboard###")
        {
            cdeConcurrentDictionary<string, TheMetaDataBase> tFld = new();
            if (pBaseThing == null) 
                return tFld;
            try
            {
                IBaseEngine tBase = pBaseThing.GetBaseEngine();
                if (tBase == null) return new cdeConcurrentDictionary<string, TheMetaDataBase>();
                ThePluginInfo tInfo = tBase.GetPluginInfo();

                string homeUrl = TheBaseAssets.MyServiceHostInfo.SiteName + "/" + tInfo.ServiceName;
                if (!string.IsNullOrEmpty(tInfo.HomeUrl))
                    homeUrl = tInfo.HomeUrl;

                string tTitle = string.IsNullOrEmpty(tInfo.ServiceDescription) ? tInfo.ServiceName : tInfo.ServiceDescription;
                nmiCtrlAboutButton tAba = new ()
                {
                    Description = tInfo.LongDescription,
                    Version = TheCommonUtils.CStr(tInfo.CurrentVersion),
                    NodeText=GetNodeForCategory(),
                    AdText = homeUrl,
                    Author = tInfo.DeveloperUrl,
                    Copyright = string.Format("(C) {0}", tInfo.Copyrights),
                    Title = tTitle,
                    LastMessage = (tBase.GetEngineState() != null ? tBase.GetEngineState().LastMessage : "###Not loaded###"),
                };

                if (!string.IsNullOrEmpty(tInfo.IconUrl))
                {
                    if (tInfo.IconUrl.EndsWith(".PNG", StringComparison.OrdinalIgnoreCase))
                        tAba.Icon = tInfo.IconUrl;
                    else
                        tAba.IconText = tInfo.IconUrl;
                }

                byte[] tGu = pBaseThing.cdeMID.ToByteArray();
                if (GetFormById(pBaseThing.cdeMID) != null)
                    tGu[tGu.Length - 1] = (byte)((tGu[tGu.Length - 1] + 3) % 255);
                TheFormInfo tAboutForm = AddForm(new TheFormInfo() { cdeMID = TheCommonUtils.CGuid(tGu), defDataSource = string.Format("TheThing;:;0;:;True;:;cdeMID={0}", pBaseThing.cdeMID), DefaultView = eDefaultView.Form });
                tFld["Form"] = tAboutForm;
                TheDashPanelInfo tAboutDPInfo = AddFormToThingUX(pDash, pBaseThing, tAboutForm, "CMyForm", tTitle, 1000, 0x0B, 0, pCate ?? ".###Status###", null, new ThePropertyBag() { $"SubTitle={TheCommonUtils.GetMyNodeName()}", "TileThumbnail=FA3:F05a", "TileWidth=2", "TileHeight=2", "ClassName=cde1PM cdeGlassButton", "Background=grey", "Foreground=white" });
                tFld["DashIcon"] = tAboutDPInfo;

                TheFieldInfo tInfoButInStatus = AddSmartControl(pBaseThing, GetFormById(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID), eFieldType.TileButton, StatusFldOrder += 10, 2, 0, null, null,
                    new nmiCtrlTileButton { OnClick = $"TTS:{tAboutForm.cdeMID}", Thumbnail="FA5:f05a",Caption= tTitle, TileHeight = 2, TileWidth = 2, Foreground = "white", Background = "gray", ClassName = "cdeGlassButton" });
                tFld["InfoButton"] = tInfoButInStatus;

                tFld["AboutButton"] = AddSmartControl(pBaseThing, tAboutForm, eFieldType.UserControl, 10, 0, 0, null, "StatusLevel", ThePropertyBag.Create(tAba));
                var tABut = tFld["AboutButton"] as TheFieldInfo;

                //Ok to register here - only once per Plugin
                pBaseThing.GetProperty("LastMessage", true).RegisterEvent(eThingEvents.PropertyChanged, (prop) =>
                {
                    tABut.SetUXProperty(Guid.Empty, $"LastMessage={prop}");
                });
                tABut.SetUXProperty(Guid.Empty, $"LastMessage={pBaseThing.LastMessage}");
                pBaseThing.GetProperty("StatusLevel", true).RegisterEvent(eThingEvents.PropertyChanged, (prop) =>
                {
                    SetStatusInfoButtons(pBaseThing, tABut, tAboutDPInfo, tInfoButInStatus);
                });
                SetStatusInfoButtons(pBaseThing, tABut, tAboutDPInfo, tInfoButInStatus);

                pDash ??= GetEngineDashBoardByThing(pBaseThing);
                if (pDash!=null)
                {
                    tFld["TileBreak"] = AddTileBreak(pBaseThing, pDash);
                }
                if (bIncludeRefreshDash && pDash!=null)
                {
                    tGu[tGu.Length - 1] = (byte)((tGu[tGu.Length - 1] + 6) % 255);
                    TheDashPanelInfo tDashInfo = new (pBaseThing)
                    {
                        cdeMID = TheCommonUtils.CGuid(tGu),
                        cdeA = pACL,
                        Flags = 3,
                        FldOrder = 1010,
                        Category = pCate ?? GetNodeForCategory(),
                        PanelTitle = pRefreshText,
                        ControlClass = !string.IsNullOrEmpty(pCustomCommand) ? $"jsAction:PTS:{pBaseThing.EngineName}:{pCustomCommand}:{pDash.cdeMID}" : $"jsAction:PTS:cdeNMI.eTheNMIEngine:NMI_GET_SCREENMETA:{pDash.cdeMID}",
                        PropertyBag = new ThePropertyBag() { "IsRefresh=true", "Thumbnail=FA4:f021" },
                    };
                    if (bHideRefreshButton || (!string.IsNullOrEmpty(pRefreshText) && pRefreshText.EndsWith("-HIDE")))
                        tDashInfo.PropertyBag.Add("Visibility=false");
                    else
                        tDashInfo.ControlClass += ":" + pRefreshText + " ###sent to Relay###";

                    AddDashPanel(pDash, tDashInfo);
                    tFld["RefreshButton"] = tDashInfo;

                    //OK to register here...only done once for each plugin
                    tBase.RegisterEvent(eEngineEvents.ThingRegistered, (pSender, pPara) =>
                    {
                        if (!string.IsNullOrEmpty(pCustomCommand))
                            pSender.HandleMessage(pSender, new TheProcessMessage() { Message = new TSM(tBase.GetEngineName(), pCustomCommand + ":REG") });
                        else
                            pDash.Reload(null, false);
                    });
                    tBase.RegisterEvent(eEngineEvents.ThingDeleted, (pSender, pPara) =>
                    {
                        if (!string.IsNullOrEmpty(pCustomCommand))
                            pSender.HandleMessage(pSender, new TheProcessMessage() { Message = new TSM(tBase.GetEngineName(), pCustomCommand + ":DEL") });
                        else
                            pDash.Reload(null, false);
                    });
                }
            }
            catch (Exception e)
            {
                TheCDEngines.MyNMIService.GetBaseThing().StatusLevel = 3;
                TheCDEngines.MyNMIService.GetBaseThing().LastMessage = "###AddAboutButton failed!###";
                TheBaseAssets.MySYSLOG.WriteToLog(2345, new TSM(eEngineName.NMIService, TheCDEngines.MyNMIService.GetBaseThing().LastMessage, eMsgLevel.l1_Error, e.ToString()));
            }

            return tFld;
        }

        private static void SetStatusInfoButtons(TheThing pBaseThing, TheFieldInfo tFld, TheDashPanelInfo tAboutDPInfo, TheFieldInfo tInfoButInStatus)
        {
            if (pBaseThing == null) return;
            string tColo = GetStatusColor(pBaseThing.StatusLevel);
            tAboutDPInfo?.SetUXProperty(Guid.Empty, $"Background={tColo}");
            tInfoButInStatus?.SetUXProperty(Guid.Empty, $"Background={tColo}");
            if (TheBaseAssets.MasterSwitch)
            {
                Guid tGuid = pBaseThing.GetBaseEngine().GetEngineState().HighStatusThing;
                if (tGuid == Guid.Empty)
                    tGuid = pBaseThing.GetBaseEngine().GetDashboardGuid();
                tFld?.SetUXProperty(Guid.Empty, $"TargetLink={tGuid}");
            }
            TheBaseAssets.MyServiceHostInfo.StatusLevel = 0;
        }

        /// <summary>
        /// This adds the default Connectivity Block to the NMI Form. A connectivity block consists of AutoConnect, Is Connected, Address Field, Connect/Disconnect Buttons
        /// </summary>
        /// <param name="pBaseThing"></param>
        /// <param name="pTargetForm"></param>
        /// <param name="pBaseFldNumber"></param>
        /// <param name="eventConnect"></param>
        /// <param name="IncludeUIDPWD"></param>
        /// <param name="pAcl"></param>
        /// <param name="pConnectedProp"></param>
        /// <param name="pAutoConnect"></param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddConnectivityBlock(TheThing pBaseThing, TheFormInfo pTargetForm, int pBaseFldNumber, Action<TheProcessMessage, bool> eventConnect = null, bool IncludeUIDPWD = false, int pAcl = 0xC0, string pConnectedProp = "IsConnected", string pAutoConnect = "AutoConnect")
        {
            return AddConnectivityBlock(pBaseThing, pTargetForm, pBaseFldNumber,pAcl, eventConnect, IncludeUIDPWD, new nmiConnectivityBlock { ConnectedPropertyName=pConnectedProp, AutoConnectPropertyName=pAutoConnect  });
        }


        /// <summary>
        /// This adds the default Connectivity Block to the NMI Form. A connectivity block consists of AutoConnect, Is Connected, Address Field, Connect/Disconnect Buttons
        /// </summary>
        /// <param name="pBaseThing">Thing that owns the connectivity block</param>
        /// <param name="pTargetForm">Target Form of the Block</param>
        /// <param name="pBaseFldNumber">Start FldNumber for the block</param>
        /// <param name="pAcl">Access Level of the block</param>
        /// <param name="eventConnect">Callback when user clicks connect or disconnect</param>
        /// <param name="IncludeUIDPWD">Include Username and Password in the connectivity block (default:false)</param>
        /// <param name="pProperties">Property Bag of Add Connectivity Block</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddConnectivityBlock(TheThing pBaseThing, TheFormInfo pTargetForm, int pBaseFldNumber, int pAcl, Action<TheProcessMessage, bool> eventConnect = null, bool IncludeUIDPWD = false, ThePropertyBag pProperties = null)
        {
            cdeConcurrentDictionary<string, TheFieldInfo> tFlds = new ();
            if (pBaseThing == null || pTargetForm == null) return tFlds;
            TheFieldInfo tGroup = AddSmartControl(pBaseThing, pTargetForm, eFieldType.CollapsibleGroup, pBaseFldNumber, 2, pAcl, "###Connectivity###", true, null, null, new nmiCtrlCollapsibleGroup { TileWidth = 6, IsSmall = true });
            if (tGroup == null) return tFlds;
            tFlds["Group"] = tGroup;

            int tPar = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pProperties, "ParentFld", "="));
            if (tPar>0)
                tGroup.SetParent(tPar);
            int tTFY = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pProperties, "TileFactorY", "="));
            if (tTFY < 1)
                tTFY = 1;
            var tDoClose = TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pProperties, "DoClose", "="));
            if (tDoClose)
                tGroup.PropertyBag = new nmiStatusBlock { DoClose = tDoClose };
            else
                tGroup.AddOrUpdatePlatformBag(eWebPlatform.Mobile, new nmiCtrlCollapsibleGroup { DoClose = true });

            string pAutoConnect = ThePropertyBag.PropBagGetValue(pProperties, "AutoConnectPropertyName", "=");
            if (!string.IsNullOrEmpty(pAutoConnect))
                tFlds["Auto"] = AddSmartControl(pBaseThing, pTargetForm, eFieldType.SingleCheck, pBaseFldNumber + 2, 2, pAcl, "###Auto Connect###", pAutoConnect, new nmiCtrlSingleCheck() { TileFactorY = tTFY, TileWidth = 3, ParentFld = pBaseFldNumber });
            string pConnectedProp = ThePropertyBag.PropBagGetValue(pProperties, "ConnectedPropertyName", "=");
            if (string.IsNullOrEmpty(pConnectedProp))
                pConnectedProp = "IsConnected";
            tFlds["IsConnected"] = AddSmartControl(pBaseThing, pTargetForm, eFieldType.SingleCheck, pBaseFldNumber + 3, 0, pAcl, "###Is Connected###", pConnectedProp, new nmiCtrlSingleCheck() { TileFactorY = tTFY, TileWidth = 3, ParentFld = pBaseFldNumber });
            tFlds["Address"] = AddSmartControl(pBaseThing, pTargetForm, eFieldType.SingleEnded, pBaseFldNumber + 4, 2, pAcl, "###Address###", "Address", new nmiCtrlSingleEnded() { TileFactorY = tTFY, ParentFld = pBaseFldNumber, PlaceHolder = "###Address of service###" });
            if (IncludeUIDPWD)
            {
                tFlds["UID"] = AddSmartControl(pBaseThing, pTargetForm, eFieldType.SingleEnded, pBaseFldNumber + 5, 2, pAcl, "###Username###", "UserName", new nmiCtrlSingleCheck() { TileFactorY = tTFY, ParentFld = pBaseFldNumber });
                tFlds["PWD"] = AddSmartControl(pBaseThing, pTargetForm, eFieldType.Password, pBaseFldNumber + 6, 3, pAcl, "###Password###", "Password", new nmiCtrlSingleCheck() { TileFactorY = tTFY, ParentFld = pBaseFldNumber });
            }
            if (eventConnect != null)
            {
                TheFieldInfo tBut1 = AddSmartControl(pBaseThing, pTargetForm, eFieldType.TileButton, pBaseFldNumber + 7, 2, pAcl, "###Connect###", null, new nmiCtrlTileButton { NoTE = true, ClassName = "cdeGoodActionButton", TileWidth = 3, ParentFld = pBaseFldNumber });
                tFlds["ConnectButton"] = tBut1;
                tBut1.RegisterUXEvent(pBaseThing, eUXEvents.OnClick, "CONNECT", (pThing, pObj) =>
                {
                    if (pObj is not TheProcessMessage pMsg || pMsg.Message == null || pThing == null) return;
                    bool IsConnected = TheThing.GetSafePropertyBool(pThing, pConnectedProp);
                    if (IsConnected)
                        TheCommCore.PublishToOriginator(pMsg.Message, LocNMI(pMsg, new TSM(eEngineName.NMIService, "NMI_TOAST", "###Service already connected###")));
                    else
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, LocNMI(pMsg, new TSM(eEngineName.NMIService, "NMI_TOAST", "###Connecting###")));
                        eventConnect(pMsg, true);
                    }
                });
                TheFieldInfo tBut2 = AddSmartControl(pBaseThing, pTargetForm, eFieldType.TileButton, pBaseFldNumber + 8, 2, pAcl, "###Disconnect###", null, new nmiCtrlTileButton() { NoTE = true, ClassName = "cdeBadActionButton", TileWidth = 3, ParentFld = pBaseFldNumber });
                tFlds["DisconnectButton"] = tBut2;
                tBut2.RegisterUXEvent(pBaseThing, eUXEvents.OnClick, "DISCONNECT", (pThing, pObj) =>
                {
                    if (pObj is not TheProcessMessage pMsg || pMsg.Message == null || pThing == null) return;
                    bool IsConnected = TheThing.GetSafePropertyBool(pThing, pConnectedProp);
                    if (!IsConnected)
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, LocNMI(pMsg, new TSM(eEngineName.NMIService, "NMI_TOAST", "###Service not connected###")));
                    }
                    else
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, LocNMI(pMsg, new TSM(eEngineName.NMIService, "NMI_TOAST", "###Disconnecting###")));
                        eventConnect(pMsg, false);
                    }
                });
            }

            return tFlds;
        }
        /// <summary>
        /// This adds the default Starting Block to the NMI Form.
        /// </summary>
        /// <param name="pBaseThing">Thing that owns the start block</param>
        /// <param name="pTargetForm">Target form for the block</param>
        /// <param name="pBaseFldNumber">Start FldNumber of the block</param>
        /// <param name="eventStartStop">Callback fired when user clicks on start/stop</param>
        /// <param name="pAcl">Access Level of the block</param>
        /// <param name="pConnectedProp">Default property for IsStarted</param>
        /// <param name="pAutoConnect">Default property for AutoStart</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddStartingBlock(TheThing pBaseThing, TheFormInfo pTargetForm, int pBaseFldNumber, Action<TheProcessMessage, bool> eventStartStop = null, int pAcl = 0xC0, string pConnectedProp = "IsStarted", string pAutoConnect = "AutoStart")
        {
            return AddStartingBlock(pBaseThing, pTargetForm, pBaseFldNumber,pAcl, eventStartStop, new nmiStartingBlock { StartedPropertyName = pConnectedProp, AutoStartPropertyName = pAutoConnect });
        }
        /// <summary>
        /// This adds the default Starting Block to the NMI Form.
        /// </summary>
        /// <param name="pBaseThing">Thing that owns the start block</param>
        /// <param name="pTargetForm">Target form for the block</param>
        /// <param name="pBaseFldNumber">Start FldNumber of the block</param>
        /// <param name="pAcl">Access Level of the block</param>
        /// <param name="eventStartStop">Callback that gets fired when user clicks start or stop</param>
        /// <param name="pProperties">Property bag for the block</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddStartingBlock(TheThing pBaseThing, TheFormInfo pTargetForm, int pBaseFldNumber, int pAcl, Action<TheProcessMessage, bool> eventStartStop = null, ThePropertyBag pProperties = null)
        {
            cdeConcurrentDictionary<string, TheFieldInfo> tFlds = new ();
            if (pBaseThing == null || pTargetForm == null) return tFlds;
            TheFieldInfo tGroup = AddSmartControl(pBaseThing, pTargetForm, eFieldType.CollapsibleGroup, pBaseFldNumber, 2, pAcl, "###Start/Stop###", true, null, null, new nmiCtrlCollapsibleGroup { TileWidth = 6, IsSmall = true });
            if (tGroup == null) return tFlds;
            tFlds["Group"] = tGroup;
            int tPar = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pProperties, "ParentFld", "="));
            if (tPar > 0)
                tGroup.SetParent(tPar);
            int tTFY = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pProperties, "TileFactorY", "="));
            if (tTFY < 1)
                tTFY = 1;
            var tDoClose = TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pProperties, "DoClose", "="));
            if (tDoClose)
                tGroup.PropertyBag = new nmiStatusBlock { DoClose = tDoClose };
            else
                tGroup.AddOrUpdatePlatformBag(eWebPlatform.Mobile, new nmiCtrlCollapsibleGroup { DoClose = true });

            string pAutoConnect = ThePropertyBag.PropBagGetValue(pProperties, "AutoStartPropertyName", "=");
            if (!string.IsNullOrEmpty(pAutoConnect))
                tFlds["Auto"] = AddSmartControl(pBaseThing, pTargetForm, eFieldType.SingleCheck, pBaseFldNumber + 2, 2, pAcl, "###Auto Start###", pAutoConnect, new nmiCtrlSingleCheck() { TileWidth=3, TileFactorY = tTFY, ParentFld =pBaseFldNumber });

            string pConnectedProp = ThePropertyBag.PropBagGetValue(pProperties, "StartedPropertyName", "=");
            if (string.IsNullOrEmpty(pConnectedProp))
                pConnectedProp = "IsStarted";
            tFlds["IsStarted"] = AddSmartControl(pBaseThing, pTargetForm, eFieldType.SingleCheck, pBaseFldNumber + 3, 0, pAcl, "###Is Started###", pConnectedProp, new nmiCtrlSingleCheck() { TileWidth=3, TileFactorY = tTFY, ParentFld =pBaseFldNumber });
            if (eventStartStop != null)
            {
                TheFieldInfo tBut1 = AddSmartControl(pBaseThing, pTargetForm, eFieldType.TileButton, pBaseFldNumber + 7, 2, pAcl, "###Start###", null, new nmiCtrlTileButton { NoTE = true, ClassName = "cdeGoodActionButton", TileWidth = 3, ParentFld = pBaseFldNumber });
                tFlds["StartButton"] = tBut1;
                tBut1.RegisterUXEvent(pBaseThing, eUXEvents.OnClick, "START", (pThing, pObj) =>
                {
                    if (pObj is not TheProcessMessage pMsg || pMsg.Message == null || pThing == null) return;
                    bool IsConnected = TheThing.GetSafePropertyBool(pThing, pConnectedProp);
                    if (IsConnected)
                        TheCommCore.PublishToOriginator(pMsg.Message, LocNMI(pMsg, new TSM(eEngineName.NMIService, "NMI_TOAST", "###Service already started###")));
                    else
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, LocNMI(pMsg, new TSM(eEngineName.NMIService, "NMI_TOAST", "###starting###")));
                        eventStartStop(pMsg, true);
                    }
                });
                TheFieldInfo tBut2 = AddSmartControl(pBaseThing, pTargetForm, eFieldType.TileButton, pBaseFldNumber + 8, 2, pAcl, "###Stop###", null, new nmiCtrlTileButton() { NoTE = true, ClassName = "cdeBadActionButton", TileWidth = 3, ParentFld = pBaseFldNumber });
                tFlds["StopButton"] = tBut2;
                tBut2.RegisterUXEvent(pBaseThing, eUXEvents.OnClick, "STOP", (pThing, pObj) =>
                {
                    if (pObj is not TheProcessMessage pMsg || pMsg.Message == null || pThing == null) return;
                    bool IsConnected = TheThing.GetSafePropertyBool(pThing, pConnectedProp);
                    if (!IsConnected)
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, LocNMI(pMsg, new TSM(eEngineName.NMIService, "NMI_TOAST", "###Service not started###")));
                    }
                    else
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, LocNMI(pMsg, new TSM(eEngineName.NMIService, "NMI_TOAST", "###stopping###")));
                        eventStartStop(pMsg, false);
                    }
                });
            }

            return tFlds;
        }

        /// <summary>
        /// Adds a Status Block to a Form. It returns a Dictionary of created fields for Group,FriendlyName,StatusLevel,LastMessage,Value and NodeName
        /// </summary>
        /// <param name="pBaseThing">TheThing owning the Form</param>
        /// <param name="tMyForm">TheFormInfo of the Form</param>
        /// <param name="StartFldOrder">FldOrder the Status Block should start with</param>
        /// <param name="pParentFld">Parent Fld of the Block</param>
        /// <param name="UseBigStatus">Shows a big StatusLevel Light if tru</param>
        /// <returns>a Dictionary of created fields for Group,FriendlyName,StatusLevel,LastMessage,Value and NodeName</returns>
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddStatusBlock(TheThing pBaseThing, TheFormInfo tMyForm, int StartFldOrder, int pParentFld = 0, bool UseBigStatus = false)
        {
            return AddStatusBlock(pBaseThing, tMyForm, StartFldOrder, pParentFld, UseBigStatus, null);
        }
        /// <summary>
        /// Adds a Status Block to a Form. It returns a Dictionary of created fields for Group,FriendlyName,StatusLevel,LastMessage,Value and NodeName
        /// </summary>
        /// <param name="pBaseThing">TheThing owning the Form</param>
        /// <param name="tMyForm">TheFormInfo of the Form</param>
        /// <param name="StartFldOrder">FldOrder the Status Block should start with</param>
        /// <param name="pParentFld">Parent Fld of the Block</param>
        /// <param name="UseBigStatus">Shows a big StatusLevel Light if tru</param>
        /// <param name="pProperties">PropertyBag of the Add Status Block</param>
        /// <returns>a Dictionary of created fields for Group,FriendlyName,StatusLevel,LastMessage,Value and NodeName</returns>
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddStatusBlock(TheThing pBaseThing, TheFormInfo tMyForm, int StartFldOrder, int pParentFld, bool UseBigStatus, ThePropertyBag pProperties)
        {
            int tTFY = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pProperties, "TileFactorY", "="));
            if (tTFY < 1)
                tTFY = 1;
            cdeConcurrentDictionary<string, TheFieldInfo> tFlds = new ();
            if (pParentFld == 0)
            {
                tFlds["Group"] = TheNMIEngine.AddSmartControl(pBaseThing, tMyForm, eFieldType.CollapsibleGroup, StartFldOrder, 2, 0, "###Device Status###", null, new nmiCtrlCollapsibleGroup { TileWidth = 6, IsSmall = true });
                pParentFld = StartFldOrder;
                int tPar = TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(pProperties, "ParentFld", "="));
                if (tPar > 0)
                    tFlds["Group"].SetParent(tPar);
                var tDoClose = TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pProperties, "DoClose", "="));
                if (tDoClose)
                    tFlds["Group"].PropertyBag = new nmiStatusBlock { DoClose = tDoClose };
                else
                    tFlds["Group"].AddOrUpdatePlatformBag(eWebPlatform.Mobile, new nmiCtrlCollapsibleGroup { DoClose = true });
            }
            tFlds["FriendlyName"] = TheNMIEngine.AddSmartControl(pBaseThing, tMyForm, eFieldType.SingleEnded, StartFldOrder + 1, 2, 0xFE, "###Device Name###", "FriendlyName", new nmiCtrlSingleEnded { TileFactorY = tTFY, ParentFld = pParentFld });
            tFlds["StatusLevel"] = TheNMIEngine.AddSmartControl(pBaseThing, tMyForm, eFieldType.StatusLight, StartFldOrder + 2, 0, 0, null, "StatusLevel", new nmiCtrlSingleEnded() { NoTE = true, ParentFld = pParentFld, TileWidth = 2, TileHeight = UseBigStatus ? 2 : 1 });
            tFlds["LastMessage"] = TheNMIEngine.AddSmartControl(pBaseThing, tMyForm, eFieldType.TextArea, StartFldOrder + 3, 0, 0, null, "LastMessage", new nmiCtrlSingleEnded() { NoTE = true, ParentFld = pParentFld, TileWidth = 4, TileHeight = UseBigStatus ? 2 : 1 });
            tFlds["LastUpdate"] = TheNMIEngine.AddSmartControl(pBaseThing, tMyForm, eFieldType.DateTime, StartFldOrder + 4, 0, 0, "###Last Update###", "LastUpdate", new nmiCtrlDateTime() { ParentFld = pParentFld, TileFactorY = tTFY, TileWidth = 6, TileHeight = 1 });
            tFlds["Value"] = TheNMIEngine.AddSmartControl(pBaseThing, tMyForm, eFieldType.Number, StartFldOrder + 5, 0, 0xFE, "###Current Value###", "Value", new nmiCtrlNumber() { ParentFld = pParentFld, TileFactorY = tTFY, TileWidth = 6, TileHeight = 1 });
            tFlds["NodeName"] = TheNMIEngine.AddSmartControl(pBaseThing, tMyForm, eFieldType.SingleEnded, StartFldOrder + 6, 0, 0, "###Managed on Node###", null, new nmiCtrlSingleEnded() { DataItem="cdeN", TileFactorY = tTFY, ParentFld = pParentFld, TileWidth = 6, TileHeight = 1 });
            return tFlds;
        }

        #endregion

        #region Dynamic Properties (HTML free)
        public static cdeConcurrentDictionary<string, TheFieldInfo> AddDynamicPropertySection(TheThing MyBaseThing, TheFormInfo MyStatusForm, string pPropPrefix, int StartFldOrder, int pParentFld = 0, int pACL = 0, bool IsLocked=false, ThePropertyBag pProperties = null)
        {
            cdeConcurrentDictionary<string, TheFieldInfo> tFlds = new ();

            var bNoPropAddDelete = TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pProperties, nameof(nmiDynamicProperty.NoPropertyAddDelete)));
            if (!bNoPropAddDelete)
            {
                var tTitle = ThePropertyBag.PropBagGetValue(pProperties, "ToAddName", "=");
                if (string.IsNullOrEmpty(tTitle))
                    tTitle = "Property";
                tFlds["Group"] = AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.CollapsibleGroup, StartFldOrder, 2, pACL, $"Add new {tTitle}...", null, new nmiCtrlCollapsibleGroup { TileWidth = 6, IsSmall = true, DoClose = true, ParentFld = pParentFld });

                var tOptions = ThePropertyBag.PropBagGetValue(pProperties, "Options", "=");
                if (!string.IsNullOrEmpty(tOptions))
                    tFlds["Scratch"] = AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.ComboOption, StartFldOrder + 1, IsLocked ? 0 : 2, pACL, $"New {tTitle}", $"ScratchName_{pPropPrefix}", new nmiCtrlComboBox() { ParentFld = StartFldOrder, Options = tOptions });
                else
                    tFlds["Scratch"] = AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.SingleEnded, StartFldOrder + 1, IsLocked ? 0 : 2, pACL, $"New {tTitle}", $"ScratchName_{pPropPrefix}", new nmiCtrlSingleEnded() { ParentFld = StartFldOrder });

                TheFieldInfo tBut = AddSmartControl(MyBaseThing, MyStatusForm, eFieldType.TileButton, StartFldOrder + 2, IsLocked ? 0 : 2, pACL, $"Add {tTitle}", false, null, null, new nmiCtrlTileButton() { ParentFld = StartFldOrder, TileFactorY = 2, NoTE = true, ClassName = "cdeGoodActionButton" });
                tBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, "AddProp", (pThing, pObj) =>
                            {
                                TheProcessMessage pMsg = pObj as TheProcessMessage;
                                if (pMsg?.Message == null) return;
                                TheThing tOrg = pThing.GetBaseThing();
                                string tNewPropName = TheThing.GetSafePropertyString(tOrg, $"ScratchName_{pPropPrefix}");
                                if (string.IsNullOrEmpty(tNewPropName))
                                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"Please specify a new {tTitle}"));
                                else
                                {
                                    if (tOrg.GetProperty($"{tNewPropName}") != null)
                                    {
                                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"{tTitle} already exists"));
                                    }
                                    else
                                    {
                                        cdeP p = tOrg.GetProperty($"{tNewPropName}", true);
                                        p.cdeM = pPropPrefix;
                                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", $"{tTitle} added"));
                                        UpdateDynamicSection(MyBaseThing, MyStatusForm, pPropPrefix, StartFldOrder + 10, pParentFld, IsLocked, pProperties);
                                        MyStatusForm.Reload(pMsg, true);
                                    }
                                    tOrg.SetProperty($"ScratchName_{pPropPrefix}", "");
                                }
                            });
                tFlds["AddButton"] = tBut;
            }
            UpdateDynamicSection(MyBaseThing, MyStatusForm, pPropPrefix, StartFldOrder + 10, pParentFld,IsLocked, pProperties);
            return tFlds;
        }

        internal static void UpdateDynamicSection(TheThing MyBaseThing, TheFormInfo pForm, string pPropPrefix, int StartFldOrder, int pParentFld = 0, bool IsLocked=false, ThePropertyBag pProperties = null)
        {
            if (MyBaseThing == null)
                return;
            if (pForm != null)
            {
                var bNoPropAddDelete = TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(pProperties, nameof(nmiDynamicProperty.NoPropertyAddDelete)));

                List<TheFieldInfo> tLst = GetFieldsByFunc(s => s.FormID == pForm.cdeMID);
                foreach (TheFieldInfo tInfo in tLst)
                {
                    if (tInfo.FldOrder >= StartFldOrder && tInfo.FldOrder < StartFldOrder+100 && TheCommonUtils.CInt(tInfo.PropBagGetValue("ParentFld")) == pParentFld)
                    {
                        if (tInfo.Type == eFieldType.TileButton)
                            tInfo.UnregisterUXEvent(MyBaseThing, eUXEvents.OnClick, tInfo.PropBagGetValue("Cookie"), null);
                        DeleteFieldById(tInfo.cdeMID);
                    }
                }

                List<cdeP> props = pPropPrefix switch
                {
                    "[cdeSensor]" => MyBaseThing.GetSensorProperties(),
                    "[cdeConfig]" => MyBaseThing.GetConfigProperties(),
                    _ => MyBaseThing.GetPropertiesMetaStartingWith(pPropPrefix).OrderBy(s => s.Name).ToList(),
                };
                int fldCnt = StartFldOrder;
                List<string> tProtectedEntries = new ();
                tProtectedEntries.AddRange(TheCommonUtils.cdeSplit(ThePropertyBag.PropBagGetValue(pProperties, "SecureOptions", "="), ";", true, true));
                foreach (var name in props.Select(p=>p.Name))
                {
                    int flags = IsLocked ? 0 : 2;
                    if (tProtectedEntries.Contains(name))
                        flags |= 1;
                    AddSmartControl(MyBaseThing, pForm, eFieldType.SingleEnded, fldCnt++, flags, 0, name, name, new nmiCtrlSingleEnded() { TileWidth = 5, ParentFld = pParentFld, TileFactorY = 2 });
                    if (!bNoPropAddDelete)
                    {
                        var tDelBut = AddSmartControl(MyBaseThing, pForm, eFieldType.TileButton, fldCnt++, IsLocked ? 0 : 2, 0, "", null, new nmiCtrlTileButton() { Thumbnail = "FA5:f2ed", Cookie = name, TileWidth = 1, TileFactorX = 1, TileFactorY = 1, ClassName = "cdeBadActionButton", TileHeight = 1, ParentFld = pParentFld });
                        tDelBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, name, (sender, pObj) =>
                        {
                            TheProcessMessage pMsg = pObj as TheProcessMessage;
                            if (pMsg?.Message == null) return;
                            string tP = pMsg.Message.PLS.Split(':')[1];
                            MyBaseThing.RemoveProperty(tP);
                            UpdateDynamicSection(MyBaseThing, pForm, pPropPrefix, StartFldOrder, pParentFld, IsLocked, pProperties);
                            pForm.Reload(pMsg, true);
                        });
                    }
                }
            }
        }

        #endregion

        #region Miscellanous Function (HTML Free)

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Gets all tracked pages. </summary>
        ///
        /// <remarks>   Chris, 3/17/2020. </remarks>
        ///
        /// <returns>   The tracked pages. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public static List<ThePageDefinition> GetPages()
        {
            return TheCDEngines.MyNMIService?.MyNMIModel?.MyPageDefinitionsStore?.MyMirrorCache?.TheValues;
        }

        /// <summary>
        /// Allows to save Screen Options for a ModelID.
        /// The ModelID of a screen has to be stored in the tScene.ID parameter;
        /// </summary>
        /// <param name="tScene"></param>
        public static bool SaveScreenOptions(TheFOR tScene)
        {
            return SaveScreenOptions(tScene.ID, tScene);
        }

        /// <summary>
        /// Saves the Screen Overides by ModelID (see TheFormInfo.ModelID)
        /// </summary>
        /// <param name="pModelID"></param>
        /// <param name="tScene"></param>
        /// <returns></returns>
        public static bool SaveScreenOptions(string pModelID, TheFOR tScene)
        {
            if (tScene == null || string.IsNullOrEmpty(pModelID) || TheBaseAssets.MyServiceHostInfo.UseRandomDeviceID || (TheBaseAssets.MyServiceHostInfo.IsCloudService && TheBaseAssets.MyScopeManager.IsScopingEnabled))
                return false;
            string tTargetDir = $"FormORs\\{TheCommonUtils.CGuid(pModelID)}.cdeFOR";
            TSM tTSM = new(eEngineName.NMIService, "", TheCommonUtils.SerializeObjectToJSONString(tScene));
            TheCommonUtils.SaveBlobToDisk(tTSM, new[] { "", tTargetDir }, null);
            return true;
        }

        /// <summary>
        /// Fills a ComboBox with the FriendlyName/Guid of things with a particular Boolean Property (bModelBool) set to true
        /// </summary>
        /// <param name="pMsg">Message coming in from a Client</param>
        /// <param name="pTargetFld">Target Field of the CommboBox</param>
        /// <param name="pModelBool">Property that has to be set to true of TheThing to be included in Option List</param>
        /// <param name="pModelName">Friendly Name of the Device that should be listed</param>
        public static string FillFieldPicker(TheProcessMessage pMsg, TheFieldInfo pTargetFld, string pModelBool, string pModelName)
        {
            return FillFieldPicker(pMsg, pTargetFld, pModelBool, pModelName, null);
        }
        public static string FillFieldPicker(TheProcessMessage pMsg, TheFieldInfo pTargetFld, string pModelBool, string pModelName, string AdditionalOption)
        {
            List<TheThing> tLst = TheThingRegistry.GetThingsByProperty("*", Guid.Empty, pModelBool, "True", true);
            string tOpt;
            if (tLst.Count > 0)
            {
                var tList = new List<TheComboOptions>();
                foreach (var tSens in tLst)
                {
                    if (!tSens.HasLiveObject && tSens.cdeN == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID) continue;
                    tList.Add(new TheComboOptions { N = tSens.FriendlyName, V = tSens.cdeMID.ToString() });
                }
                if (!string.IsNullOrEmpty(AdditionalOption))
                {
                    var tP = TheCommonUtils.cdeSplit(AdditionalOption, ";:;", false, false);
                    tList.Add(new TheComboOptions { N = tP[0], V = tP.Length > 1 ? tP[1] : Guid.Empty.ToString() });
                }
                tOpt = TheCommonUtils.SerializeObjectToJSONString<List<TheComboOptions>>(tList);
            }
            else
                tOpt = string.Format(TheNMIEngine.LocNMI(pMsg, "###No {0} Found - please create a new {0} first###"), pModelName);
            if (pMsg == null)
                pTargetFld?.SetUXProperty(Guid.Empty, $"Options={tOpt}");
            else
                pTargetFld?.SetUXProperty(pMsg.Message.GetOriginator(), $"Options={tOpt}");
            return tOpt;
        }

        internal static bool UpdateUXItemACLFromThing(TheThing pThing)
        {
            if (!IsInitialized() || pThing == null || pThing.cdeA == 0) return false;
            List<TheDashboardInfo> tDashs = TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.MyMirrorCache.GetEntriesByFunc(s => s.cdeO == pThing.cdeMID);
            foreach (TheDashboardInfo tDash in tDashs)
            {
                tDash.cdeA = pThing.cdeA;
                TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.UpdateItem(tDash);
            }
            List<TheDashPanelInfo> tDashPs = TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents.MyMirrorCache.GetEntriesByFunc(s => s.cdeO == pThing.cdeMID);
            foreach (TheDashPanelInfo tDash in tDashPs)
            {
                tDash.cdeA = pThing.cdeA;
                TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents.UpdateItem(tDash);
            }
            List<TheFormInfo> tForms = TheCDEngines.MyNMIService.MyNMIModel.MyForms.MyMirrorCache.GetEntriesByFunc(s => s.cdeO == pThing.cdeMID);
            foreach (TheFormInfo tForm in tForms)
            {
                tForm.cdeA = pThing.cdeA;
                TheCDEngines.MyNMIService.MyNMIModel.MyForms.UpdateItem(tForm);
            }
            List<TheFieldInfo> tFields = TheCDEngines.MyNMIService.MyNMIModel.MyFields.MyMirrorCache.GetEntriesByFunc(s => s.cdeO == pThing.cdeMID);
            foreach (TheFieldInfo tField in tFields)
            {
                tField.cdeA = pThing.cdeA;
                TheCDEngines.MyNMIService.MyNMIModel.MyFields.UpdateItem(tField);
            }
            return true;
        }

        /// <summary>
        /// Returns an HTML5 Color for each StatusLevel.
        /// The colors can be customized by changing the "MyServiceHostInfo.StatusColors" list of colors
        /// </summary>
        /// <param name="StatusLevel"></param>
        /// <returns></returns>
        public static string GetStatusColor(int StatusLevel)
        {
            string tCol = "inherit";
            if (TheBaseAssets.MyServiceHostInfo.mStatusColors != null && StatusLevel < TheBaseAssets.MyServiceHostInfo.mStatusColors.Length)
                tCol = TheBaseAssets.MyServiceHostInfo.mStatusColors[StatusLevel];
            return tCol;
        }

        /// <summary>
        /// Returns the localized version of the StationName for Categories
        /// </summary>
        /// <returns></returns>
        public static string GetNodeForCategory()
        {
            return "###Node###: " + (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.NodeName) ? TheBaseAssets.MyServiceHostInfo.NodeName : TheBaseAssets.MyServiceHostInfo.MyStationName);
        }

        /// <summary>
        /// Returns a list of all known and Alive NMI Nodes connected to this node
        /// </summary>
        /// <returns></returns>
        public static List<Guid> GetAliveNMINodes()
        {
            return TheFormsGenerator.GetKnownNMINodes();
        }

        /// <summary>
        /// Returns true if the DeviceID given is known to the NMI Engine (has requested the Meta Information and sent Heartbeat Pings recently)
        /// </summary>
        /// <param name="pNodeID"></param>
        /// <returns></returns>
        public static bool IsNMINodeAlive(Guid pNodeID)
        {
            return TheFormsGenerator.IsNMINodeKnown(pNodeID);
        }

        /// <summary>
        /// Registers an Engine with the NMI Service in order to provide resources
        /// </summary>
        /// <param name="pEngine"></param>
        public static bool RegisterEngine(IBaseEngine pEngine)
        {
            if (TheCDEngines.MyNMIService != null)
            {
                if (TheCDEngines.MyNMIService.MyRegisteredEngines.ContainsKey(pEngine.GetEngineName())) return false;
                TheCDEngines.MyNMIService.MyNMIModel.UpdatePageStore(null);
                TheCDEngines.MyNMIService.MyNMIModel.UpdateContentStore(null);
                TheCDEngines.MyNMIService.MyNMIModel.UpdateBlockStore(null);
                TheCDEngines.MyNMIService.MyRegisteredEngines.TryAdd(pEngine.GetEngineName(), pEngine);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets a Property on a NMI Control
        /// </summary>
        /// <param name="pOrg">Specifies a node on which this property shoul be set. If Guid.Empty it will be send to all connected nodes</param>
        /// <param name="pUXElement">The cdeMID of the target control i.e. a TheFieldInfo.cdeMID</param>
        /// <param name="pProps">Property or properties to be set. Syntax: Name=Value:;:Name=Value...</param>
        public static void SetUXProperty(Guid pOrg, Guid pUXElement, string pProps)
        {
            SetUXProperty(pOrg, pUXElement, pProps, null, null);
        }

        /// <summary>
        /// Sets a Property on a NMI Control
        /// </summary>
        /// <param name="pOrg">Specifies a node on which this property shoul be set. If Guid.Empty it will be send to all connected nodes</param>
        /// <param name="pUXElement">The cdeMID of the target control i.e. a TheFieldInfo.cdeMID</param>
        /// <param name="pProps">Property or properties to be set. Syntax: Name=Value:;:Name=Value...</param>
        /// <param name="pThingMID"></param>
        public static void SetUXProperty(Guid pOrg, Guid pUXElement, string pProps, string pThingMID)
        {
            SetUXProperty(pOrg, pUXElement, pProps, pThingMID, null);
        }

        /// <summary>
        /// Sets a Property on a NMI Control belonging to a Thing. SubControl defines "DASH", "FORM" or a column's FldOrder in a Table
        /// </summary>
        /// <param name="pOrg">Specifies a node on which this property shoul be set. If Guid.Empty it will be send to all connected nodes</param>
        /// <param name="pUXElement">The cdeMID of the target control (TheFieldInfo.cdeMID)</param>
        /// <param name="pProps">Property or properties to be set. Syntax: Name=Value:;:Name=Value...</param>
        /// <param name="pThingMID">Owning Thing of the control (important for Tables as all rows have the same pUXElement Guid)</param>
        /// <param name="pSubControl">Sometimes there are multiple fields with the same guid (i.e. in tables), Subcontrol Field Type can then tell what you want to change: the smartlable (20) or the underlying Edit field (i.e. 63 for ThingPicker)</param>
        public static void SetUXProperty(Guid pOrg, Guid pUXElement, string pProps, string pThingMID, string pSubControl)
        {
            if (!TheBaseAssets.MasterSwitch || string.IsNullOrEmpty(pProps)) return;    //Prevent sending anthing after or during Shutdown
            TSM tTSM = new (eEngineName.NMIService, $"SET{(!string.IsNullOrEmpty(pThingMID) ? "F" : "")}NP", pProps) { OWN = pUXElement.ToString() };    //UX Properties


            if (!string.IsNullOrEmpty(pThingMID))
            {
                tTSM.TXT += $":{pThingMID}";
                if (!string.IsNullOrEmpty(pSubControl)) //New in 4.1 to address DASH Icons, FORMs and Table Columns specfically. If omitted, the property will be set all ALL controls with the same ThingMID. The old 4.0 NMI always do that
                    tTSM.TXT += $":{pSubControl}";
            }
            else
            {
                if (!string.IsNullOrEmpty(pSubControl)) //New in 4.1 to address DASH Icons, FORMs and Table Columns specfically. If omitted, the property will be set all ALL controls with the same ThingMID. The old 4.0 NMI always do that
                    tTSM.TXT += $"::{pSubControl}";
            }
            //Sends: "SET(F)NP:<ThingMid>:<SubControl>;<FldOrder?>:<First propertyName>"


            if (!pProps.Contains(":;:"))
            {
                tTSM.TXT += $":{pProps.Split('=')[0]}"; //Only filters out SET Properties of the same property
                tTSM.SetNoDuplicates(true);
            }

            if (pOrg == Guid.Empty)
            {
                tTSM.SetToNodesOnly(true);
                //if (TheFormsGenerator.IsUXElementKnown(pUXElement))   //We need to store all the META_INFO going to Browser...this publishcentral can go against any visible control that has no databinding
                if (!TheBaseAssets.MyServiceHostInfo.IsCloudService || TheBaseAssets.MyScopeManager.IsScopingEnabled) //Tuning to disable blank shooter telegrams from unscoped clouds
                    TheCommCore.PublishCentral(tTSM);
            }
            else
            {
                if (TheFormsGenerator.IsNMINodeKnown(pOrg))
                    TheCommCore.PublishToNode(pOrg, tTSM);
            }
        }

        /// <summary>
        /// returns true of the NMI Model and the service is initialized
        /// </summary>
        /// <returns></returns>
        public static bool IsInitialized()
        {
            if (TheCDEngines.MyNMIService != null && TheCDEngines.MyNMIService.IsInit()) return true;
            return false;
        }

        /// <summary>
        /// Returns true if the NMI Model is ready
        /// </summary>
        public static bool IsModelReady
        {
            get { return TheCommonUtils.CBool(TheCDEngines.MyNMIService?.MyNMIModel?.IsModelReady()); }
        }



        /// <summary>
        /// Register a new Transform File for a given user. If pUserID is Guid.Empty
        /// </summary>
        /// <param name="pResourceFileName"></param>
        /// <param name="pUserID"></param>
        /// <returns></returns>
        public static bool RegisterSyncedTransform(string pResourceFileName, Guid pUserID)
        {
            ThePageBlocks tBlock = new ()
            {
                BlockType= new Guid("{8C8E1291-3C50-4855-A8C9-27EC203C1F0E}"),
                Template =pResourceFileName,
                cdeO=pUserID,
                IsMeshSynced=true
            };
            tBlock.RawData= TheCommonUtils.CArray2UTF8String(TheCommonUtils.GetSystemResource(null, tBlock.Template));
            return AddPageBlocks(new List<ThePageBlocks> { tBlock });
        }

        internal static string GetTransforms(TheUserDetails pUser)
        {
            if (pUser == null)
                return null;
            StringBuilder res = new ();
            foreach (var tPageBlock in GetBlocksByType(new Guid("{8C8E1291-3C50-4855-A8C9-27EC203C1F0E}"), pUser.cdeMID, pUser.HomeNode))  //cdeNMITransform block Type Guid: {8C8E1291-3C50-4855-A8C9-27EC203C1F0E}
            {
                string tTemplate = tPageBlock.RawData;
                if (string.IsNullOrEmpty(tTemplate))
                    tTemplate=TheCommonUtils.CArray2UTF8String(TheCommonUtils.GetSystemResource(null, tPageBlock.Template));
                if (!string.IsNullOrEmpty(tTemplate))
                    res.Append(tTemplate);
            }
            return res.ToString();
        }

        /// <summary>
        /// Updates the PLS with a localized version of the PLS according to an LCID. ATTENTION: If ### is in PLS and the message was NOT meant to be translated, the content of PLS will change!
        /// ENG must be set to the engine holding the strings
        /// </summary>
        /// <param name="LCID"></param>
        /// <param name="pOrg"></param>
        /// <returns></returns>
        public static TSM LocNMI(int LCID, TSM pOrg)
        {
            if (pOrg == null || string.IsNullOrEmpty(pOrg.PLS) || !pOrg.PLS.StartsWith("###")) return pOrg;
            pOrg.PLS = TheBaseAssets.MyLoc.GetLocalizedStringByKey(LCID, pOrg?.ENG == null ? "cdeEngine" : pOrg.ENG, pOrg.PLS);
            return pOrg;
        }
        /// <summary>
        /// Updates the PLS with a localized version of the PLS. ATTENTION: If ### is in PLS and the message was NOT meant to be translated, the content of PLS will change!
        /// The LCID is processed using the Session State in TheProcessMessage
        /// </summary>
        /// <param name="pMsg"></param>
        /// <param name="pOrg"></param>
        /// <returns></returns>
        public static TSM LocNMI(TheProcessMessage pMsg, TSM pOrg)
        {
            if (pOrg == null || string.IsNullOrEmpty(pOrg.PLS) || !pOrg.PLS.StartsWith("###")) return pOrg;
            pOrg.PLS = TheBaseAssets.MyLoc.GetLocalizedStringByKey(TheCommonUtils.GetLCID(pMsg), pMsg?.Message?.ENG == null ? "cdeEngine" : pMsg.Message.ENG, pOrg.PLS);
            return pOrg;
        }
        /// <summary>
        /// Updates the string with a localized version of the PLS.
        /// The LCID is processed using the Session State in TheProcessMessage
        /// </summary>
        /// <param name="pMsg"></param>
        /// <param name="pOrg"></param>
        /// <returns></returns>
        public static string LocNMI(TheProcessMessage pMsg, string pOrg)
        {
            if (string.IsNullOrEmpty(pOrg)) return pOrg;
            pOrg = TheBaseAssets.MyLoc.GetLocalizedStringByKey(TheCommonUtils.GetLCID(pMsg), pMsg?.Message?.ENG == null ? "cdeEngine" : pMsg.Message.ENG, pOrg);
            return pOrg;
        }

        /// <summary>
        /// Sets the scope ID on a specific request
        /// </summary>
        /// <param name="pRequest">Request Information to receive the ScopeID in its session state</param>
        /// <param name="tPageDefinition">Page definition defining if the page requires scoping or not</param>
        public static void SetScopeID(TheRequestData pRequest, ThePageDefinition tPageDefinition)
        {
            if (pRequest.SessionState == null || TheBaseAssets.MyScopeManager == null || !string.IsNullOrEmpty(pRequest.SessionState.SScopeID)) return;
            if (tPageDefinition.AllowScopeQuery && !string.IsNullOrEmpty(pRequest.RequestUri.Query))
                pRequest.SessionState.SScopeID = TheBaseAssets.MyScopeManager.GetScrambledScopeIDFromEasyID(pRequest.RequestUri.Query.Substring(1));
            else if (!tPageDefinition.RequireLogin)
                pRequest.SessionState.SScopeID = TheBaseAssets.MyScopeManager.GetScrambledScopeID();     //GRSI: rare
        }
        #endregion
    }
}
