// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.Security;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Communication;
using System.Reflection;

namespace nsCDEngine.Engines.NMIService
{
    internal static class TheFormsGenerator
    {
        #region NEW-NMI4.1 Element Subscription
        private static readonly cdeConcurrentDictionary<Guid, cdeConcurrentDictionary<Guid, TheNMISubscription>> MyNMIElements = new cdeConcurrentDictionary<Guid, cdeConcurrentDictionary<Guid, TheNMISubscription>>();
        private static readonly cdeConcurrentDictionary<Guid, TheNMISubscription> MyAliveNMINodes = new cdeConcurrentDictionary<Guid, TheNMISubscription>();
        private static readonly cdeConcurrentDictionary<Guid, TheNMISubscription> MyAliveThings = new cdeConcurrentDictionary<Guid, TheNMISubscription>();
        internal static int GetNMINodeCount()
        {
            return MyAliveNMINodes.Count;
        }

        internal static bool RegisterNewNMINode(Guid pNodeID, Guid pID, Guid pOwner, string pDataItem, bool? pHasLiveSubs)
        {
            if (MyAliveNMINodes.ContainsKey(pNodeID))
            {
                if (pID != Guid.Empty)
                    MyAliveNMINodes[pNodeID].cdeMID = pID;
                if (pOwner != Guid.Empty)
                    MyAliveNMINodes[pNodeID].cdeO = pOwner;
                if (!string.IsNullOrEmpty(pDataItem))
                    MyAliveNMINodes[pNodeID].DataItem = pDataItem;
                if (pHasLiveSubs != null)
                    MyAliveNMINodes[pNodeID].HasLiveSub = TheCommonUtils.CBool(pHasLiveSubs);
                return true;
            }
            else
            {
                var tsub = new TheNMISubscription() { cdeN = pNodeID, cdeO = pOwner, DataItem = pDataItem, HasLiveSub = TheCommonUtils.CBool(pHasLiveSubs), cdeMID = pID };
                MyAliveNMINodes.TryAdd(pNodeID, tsub);
                if (pOwner != Guid.Empty)
                    MyAliveThings[pOwner] = tsub;
            }
            return false;
        }
        internal static void UpdateRegisteredNMINode(Guid pNodeID)
        {
            if (TheBaseAssets.MyServiceHostInfo.DisableNMI || MyAliveNMINodes == null)
                return;
            if (MyAliveNMINodes.ContainsKey(pNodeID))
                MyAliveNMINodes[pNodeID].cdeCTIM = DateTimeOffset.Now;
        }

        internal static List<Guid> GetKnownNMINodes()
        {
            return MyAliveNMINodes.Keys.ToList();
        }

        internal static bool IsNMINodeKnown(Guid pNodeID)
        {
            return (MyAliveNMINodes?.ContainsKey(pNodeID) == true);
        }
        internal static bool IsUXElementKnown(Guid pUXID)
        {
            if (MyNMIElements?.ContainsKey(pUXID) == true)
            {
                return MyNMIElements[pUXID].Any(s => s.Value.HasLiveSub);
            }
            return false;
        }

        internal static bool IsOwnerKnown(Guid pThingID)
        {
            return (MyAliveThings?.ContainsKey(pThingID) == true);
        }

        internal static TheThing RegisterNMISubscription(TheClientInfo pClientInfo, string pDataItem, TheMetaDataBase tFld)
        {
            if (!string.IsNullOrEmpty(pDataItem))
            {
                TheThing tThing = null;
                lock (MyNMIElements.MyLock) //This can cause jams of threads here! Highly optimized to get out of lock as fast as possible
                {
                    if (!MyNMIElements.ContainsKey(tFld.cdeMID))
                    {
                        MyNMIElements.TryAdd(tFld.cdeMID, new cdeConcurrentDictionary<Guid, TheNMISubscription>());
                    }
                    if (!MyNMIElements[tFld.cdeMID].ContainsKey(pClientInfo.NodeID))
                    {
                        tThing = TheThingRegistry.GetThingByMID(tFld.cdeO); //Most expensive call in here
                        if (tThing != null)
                        {
                            var tNewEle = new TheNMISubscription() { cdeN = pClientInfo.NodeID, cdeO = tThing.cdeMID, HasLiveSub = true, DataItem = pDataItem, cdeMID = tFld.cdeMID };
                            MyNMIElements[tFld.cdeMID].TryAdd(pClientInfo.NodeID, tNewEle);
                        }
                    }
                }
                if (tThing != null)
                {
                    if (tFld is TheFieldInfo && (tFld as TheFieldInfo).Type == eFieldType.Table)    //fix for Bug#1195
                        return tThing;
                    RegisterNewNMINode(pClientInfo.NodeID, tFld.cdeMID, tThing.cdeMID, pDataItem, true);
                    var OnUpdateName = pDataItem;
                    if (OnUpdateName.StartsWith("MyPropertyBag."))  //TODO: Test this with Prop of Prop
                        OnUpdateName = OnUpdateName.Split('.')[1];
                    tThing.GetProperty(OnUpdateName, true).SetPublication(true, Guid.Empty); //Guid.Empty uses PublishCentral - a specific node would use SYSTEMWIDE
                    return tThing;
                }
            }
            return null;
        }

        internal static void CleanupKnownNMISubscriptions(long _)
        {
            if (TheBaseAssets.MyServiceHostInfo.DisableNMI || MyAliveNMINodes == null || TheCommonUtils.cdeIsLocked(MyAliveNMINodes.MyLock))
                return;
            lock (MyAliveNMINodes.MyLock)
            {
                List<Guid> tToDel = new List<Guid>();
                foreach (Guid tK in MyAliveNMINodes.Keys)
                {
                    if (DateTimeOffset.Now.Subtract(MyAliveNMINodes[tK].cdeCTIM).TotalSeconds > TheBaseAssets.MyServiceHostInfo.SessionTimeout * 2)
                        tToDel.Add(tK);
                }
                if (tToDel.Count > 0)
                {
                    foreach (var tKdel in tToDel)
                    {
                        MyAliveNMINodes.RemoveNoCare(tKdel);
                        DisableNMISubscriptions(tKdel);
                    }
                }
            }
        }

        internal static void DisableNMISubscriptions(Guid NodeID)
        {
            List<TheNMISubscription> tToUnregister = new List<TheNMISubscription>();
            lock (MyNMIElements.MyLock) //This can cause jams of threads here! Highly optimized to get out of lock as fast as possible - could use a reader lock as we dont delete/add to the array- just change a prop
            {
                foreach (var k in MyNMIElements.Keys)
                {
                    TheNMISubscription tSub = null;
                    bool IsOneStillAlive = false;
                    foreach (var tK in MyNMIElements[k].Keys)
                    {
                        if (MyNMIElements[k][tK].cdeN == NodeID)
                        {
                            MyNMIElements[k][tK].HasLiveSub = false;
                            tSub = MyNMIElements[k][tK];
                            continue;
                        }
                        if (MyNMIElements[k][tK].HasLiveSub == true)
                        {
                            IsOneStillAlive = true;
                            break;
                        }
                    }
                    if (!IsOneStillAlive && tSub != null)
                        tToUnregister.Add(tSub);
                }
            }
            if (tToUnregister.Count > 0)
            {
                foreach (var tG in tToUnregister)
                {
                    var tThing = TheThingRegistry.GetThingByMID(tG.cdeO);
                    if (tThing == null)
                        continue;
                    var OnUpdateName = tG.DataItem;
                    if (OnUpdateName.StartsWith("MyPropertyBag."))  //TODO: Test this with Prop of Prop
                        OnUpdateName = OnUpdateName.Split('.')[1];
                    tThing.GetProperty(OnUpdateName, true).SetPublication(false, Guid.Empty); //Guid.Empty uses PublishCentral - a specific node would use SYSTEMWIDE
                    MyNMIElements.RemoveNoCare(tG.cdeMID);
                    MyAliveThings.RemoveNoCare(tThing.cdeMID);
                }
            }
        }
        #endregion

        internal static TheFOR GetScreenOptions(Guid FormId, TheClientInfo pClientInfo, TheFormInfo pInfo)
        {
            TheFOR tso = null;

            if (pInfo != null && !string.IsNullOrEmpty(pInfo.ModelID))
            {
                var tMods = pInfo.ModelID.Split(';');
                foreach (string tM in tMods)
                {
                    string tPlS1 = TheCommonUtils.LoadStringFromDisk($"FormORs\\{tM}.cdeFOR", null);
                    if (!string.IsNullOrEmpty(tPlS1))
                    {
                        TheFOR Ttso = TheCommonUtils.DeserializeJSONStringToObject<TheFOR>(tPlS1);
                        tso = SetTSO(tso, Ttso);
                        if (!string.IsNullOrEmpty(Ttso?.StartGroup))
                            pInfo.PropertyBag = new ThePropertyBag { $"StartGroup={Ttso.StartGroup}" };
                    }
                }
            }

            string tPlS = TheCommonUtils.LoadStringFromDisk($"{pClientInfo.UserID}\\{FormId}.cdeFOR", null);
            if (!string.IsNullOrEmpty(tPlS))
            {
                TheFOR Ttso = TheCommonUtils.DeserializeJSONStringToObject<TheFOR>(tPlS);
                tso = SetTSO(tso, Ttso, true);
                if (pInfo!=null && !string.IsNullOrEmpty(Ttso?.StartGroup))
                    pInfo.PropertyBag = new ThePropertyBag { $"StartGroup={Ttso.StartGroup}" };
            }
            return tso;
        }

        private static TheFOR SetTSO(TheFOR tso, TheFOR Ttso, bool SetID = false)
        {
            if (Ttso != null)
            {
                if (tso == null)
                    tso = Ttso;
                else
                {
                    if (SetID)
                        tso.ID = Ttso.ID;
                    if (Ttso.TileWidth > 0)
                        tso.TileWidth = Ttso.TileWidth;
                    if (tso.Flds == null)
                        tso.Flds = Ttso.Flds;
                    else
                        tso.Flds.AddRange(Ttso.Flds);
                }
            }

            return tso;
        }

        internal static TheFormInfo GenerateForm(Guid FormId, TheClientInfo pClientInfo)
        {
            if (TheCDEngines.MyNMIService == null)
                return null;
            TheFormInfo tDef = TheNMIEngine.GetFormById(FormId);
            if (tDef == null) return null;

            TheFormInfo tToSend = tDef.Clone(pClientInfo.WebPlatform);
            TheNMIEngine.CheckAddButtonPermission(pClientInfo, tToSend);
            tToSend.AssociatedClassName = FormId.ToString();
            var tso = GetScreenOptions(FormId, pClientInfo, tDef);
            if (tso != null && tso.TileWidth > 0)
                tToSend.TileWidth = tso.TileWidth;

            tToSend.FormFields = GetPermittedFields(FormId, pClientInfo, tso, true);
            return tToSend;
        }

        internal static List<TheFieldInfo> GetPermittedFields(Guid FormId, TheClientInfo pClientInfo, TheFOR tso, bool UpdateSubs)
        {
            List<TheFieldInfo> tReturnLst = new List<TheFieldInfo>();
            try
            {
                Func<TheFieldInfo, bool> pSelector = (s => TheUserManager.HasUserAccess(pClientInfo.UserID, s.cdeA) &&
                        ((s.Flags & 4) == 0 || !pClientInfo.IsMobile) &&
                        ((s.Flags & 128) == 0 || pClientInfo.IsFirstNode || pClientInfo.IsUserTrusted));   //NEW3.105: Only Show from First node is set

                IEnumerable<TheFieldInfo> FormFields = TheNMIEngine.GetFieldsByFunc(s => s.FormID == FormId).Where(pSelector).OrderBy(s => s.FldOrder); //NMI-REDO: this is the main bottleneck Function
                if (FormFields != null)
                {
                    foreach (var tField in FormFields)
                    {

                        if (CheckHidePersmission(tField.PlatBag, pClientInfo))
                        {
                            TheFieldInfo tFld = tField.Clone();
                            if (tFld.PlatBag.ContainsKey(eWebPlatform.Any))
                                tFld.PropertyBag.MergeBag(tFld.PlatBag[eWebPlatform.Any], true, false);
                            if (tFld.PlatBag.ContainsKey(pClientInfo.WebPlatform))
                                tFld.PropertyBag.MergeBag(tFld.PlatBag[pClientInfo.WebPlatform], true, false);
                            bool DeleteFld = false;
                            if (tso != null)
                            {
                                var tfo = tso.Flds.Where(s => s.FldOrder == tFld.FldOrder);
                                if (tfo != null && tfo.Count() > 0)
                                {
                                    foreach (TheFLDOR tF in tfo)
                                    {
                                        if (tF.PO != null)
                                            tFld.PropertyBag = tF.PO;
                                        if (tF.NewFldOrder > 0)
                                            tFld.FldOrder = tF.NewFldOrder;
                                        else if (tF.NewFldOrder < 0)
                                            DeleteFld = true;
                                    }
                                }
                            }
                            if (!DeleteFld)
                            {
                                tReturnLst.Add(tFld);
                                //NEW in 4.1: All subscriptiosn here
                                if (UpdateSubs)
                                {
                                    var tThing = RegisterNMISubscription(pClientInfo, tFld.DataItem, tFld);
                                    if (tThing != null && (tFld.Type == eFieldType.FacePlate || tFld.Type == eFieldType.TileButton))
                                    {
                                        var tsuc = TheNMIEngine.ParseFacePlateUrlInternal(tThing, ThePropertyBag.PropBagGetValue(tFld.PropertyBag, "HTMLUrl", "="), false, pClientInfo.NodeID);
                                        if (!tsuc)
                                            TheNMIEngine.ParseFacePlateInternal(tThing, ThePropertyBag.PropBagGetValue(tFld.PropertyBag, "HTML", "="), pClientInfo.NodeID);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(777, new TSM(eEngineName.NMIService, "Get Permitted fields failed", eMsgLevel.l1_Error, e.ToString()));
            }

            return tReturnLst;
        }

        internal static bool CheckHidePersmission(cdeConcurrentDictionary<eWebPlatform, ThePropertyBag> pPlatBag, TheClientInfo pInfo)
        {
            cdeConcurrentDictionary<string, string> platPlat = null;
            if (pPlatBag.ContainsKey(pInfo.WebPlatform))
                platPlat = ThePropertyBag.GetDictionary(pPlatBag[pInfo.WebPlatform], "=");

            if (pPlatBag.ContainsKey(eWebPlatform.Any))
            {
                cdeConcurrentDictionary<string, string> tPlat = ThePropertyBag.GetDictionary(pPlatBag[eWebPlatform.Any], "=");

                if (tPlat.ContainsKey("Hide") && TheCommonUtils.CBool(tPlat["Hide"]))
                {
                    if (platPlat == null || (platPlat.ContainsKey("Show") && !TheCommonUtils.CBool(platPlat["Show"])))
                        return false;
                }
                if (tPlat.ContainsKey("RequireFirstNode") && TheCommonUtils.CBool(tPlat["RequireFirstNode"]) && !pInfo.IsFirstNode)
                {
                    if (platPlat == null || (platPlat.ContainsKey("AllowAllNodes") && !TheCommonUtils.CBool(platPlat["AllowAllNodes"])))
                        return false;
                }
            }
            else
            {
                if (platPlat != null)
                {
                    if (platPlat.ContainsKey("Hide") && TheCommonUtils.CBool(platPlat["Hide"]))
                        return false;
                    if (platPlat.ContainsKey("RequireFirstNode") && TheCommonUtils.CBool(platPlat["RequireFirstNode"]) && !pInfo.IsFirstNode)
                        return false;
                }
            }
            return true;
        }
        internal static bool CheckAddPermission(cdeConcurrentDictionary<eWebPlatform, ThePropertyBag> pPlatBag, TheClientInfo pInfo)
        {
            cdeConcurrentDictionary<string, string> platPlat = null;
            if (pPlatBag.ContainsKey(pInfo.WebPlatform))
                platPlat = ThePropertyBag.GetDictionary(pPlatBag[pInfo.WebPlatform], "=");

            if (pPlatBag.ContainsKey(eWebPlatform.Any))
            {
                cdeConcurrentDictionary<string, string> tPlat = ThePropertyBag.GetDictionary(pPlatBag[eWebPlatform.Any], "=");

                if (tPlat.ContainsKey("HideAddButton") && TheCommonUtils.CBool(tPlat["HideAddButton"]))
                {
                    if (platPlat == null || (platPlat.ContainsKey("ShowAddButton") && !TheCommonUtils.CBool(platPlat["ShowAddButton"])))
                        return false;
                }
                if (tPlat.ContainsKey("RequireFirstNodeForAdd") && TheCommonUtils.CBool(tPlat["RequireFirstNodeForAdd"]) && !pInfo.IsFirstNode)
                {
                    if (platPlat == null || (platPlat.ContainsKey("AllowAddOnAllNodes") && !TheCommonUtils.CBool(platPlat["AllowAddOnAllNodes"])))
                        return false;
                }
            }
            else
            {
                if (platPlat != null)
                {
                    if (platPlat.ContainsKey("HideAddButton") && TheCommonUtils.CBool(platPlat["HideAddButton"]))
                        return false;
                    if (platPlat.ContainsKey("RequireFirstNodeForAdd") && TheCommonUtils.CBool(platPlat["RequireFirstNodeForAdd"]) && !pInfo.IsFirstNode)
                        return false;
                }
            }
            return true;
        }

        internal static TheScreenInfo GenerateLiveScreen(Guid pScreenId, TheClientInfo tClientInfo) // Guid pUserGuid, int lcid, int pFlag)
        {
            if (TheCDEngines.MyNMIService == null)
                return null;

            TheScreenInfo tInfo = new TheScreenInfo
            {
                cdeMID = pScreenId,
                MyDashboard = null,
                MyStorageInfo = new List<TheFormInfo>(),
                MyStorageMeta = new cdeConcurrentDictionary<string, TheFormInfo>(),
                MyStorageMirror = new List<object>(),
                MyDashPanels = new List<TheDashPanelInfo>()
            };

            TheThing tLiveForm = TheThingRegistry.GetThingByMID("*", pScreenId);
            if (tLiveForm == null || !TheUserManager.HasUserAccess(tClientInfo.UserID, tLiveForm.cdeA))
            {
                return null; //V3.1: BUG 126 - could lead to racing condition. TODO: Revisit later
                //TheFormInfo tI = new TheFormInfo(tLiveForm) { FormTitle = (tLiveForm == null ? "Form not Found!" : "Access Denied!") };
                //tI.TargetElement = pScreenId.ToString();
                //tI.AssociatedClassName = pScreenId.ToString();
                //tInfo.MyStorageInfo.Add(tI);
                //tI.FormFields = new List<TheFieldInfo>();
                //TheFieldInfo tFldInfo = new TheFieldInfo(null, null, 10, 0, 0);
                //tFldInfo.Type = eFieldType.SmartLabel;
                //tFldInfo.Header = (tLiveForm == null ? "This Form was defined but has not Meta-Data associated with it." : "You do not have the required access permissions!");
                //tI.FormFields.Add(tFldInfo);
                //return tInfo;
            }
            string tFormName = TheThing.GetSafePropertyString(tLiveForm, "FriendlyName");
            List<TheThing> tFields = TheThingRegistry.GetThingsByFunc("*", s => s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID && TheThing.GetSafePropertyString(s, "FormName") == tFormName && TheThing.GetSafePropertyBool(s, "IsLiveTag") && (s.UID == Guid.Empty || s.UID == tClientInfo.UserID));
            if (tFields != null && tFields.Any())
            {
                string tFormTitle = TheThing.GetSafePropertyString(tLiveForm, "FormTitle");
                if (string.IsNullOrEmpty(tFormTitle)) tFormTitle = tFormName;
                TheFormInfo tI = new TheFormInfo(tLiveForm)
                {
                    FormTitle = tFormTitle,
                    TargetElement = pScreenId.ToString(),
                    DefaultView = eDefaultView.Form,
                    TileWidth = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(tLiveForm, "TileWidth")),
                    TileHeight = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(tLiveForm, "TileHeight")),
                    IsUsingAbsolute = TheThing.GetSafePropertyBool(tLiveForm, "IsAbsolute"),
                    AssociatedClassName = pScreenId.ToString()
                };
                tInfo.MyStorageInfo.Add(tI);
                tI.FormFields = new List<TheFieldInfo>();
                int fldNo = 10;
                foreach (TheThing tTh in tFields)
                {
                    int tfldNo = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(tTh, "FldOrder"));
                    if (tfldNo == 0)
                        tfldNo = fldNo;
                    int tFlags = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(tTh, "Flags"));
                    cdeP ValProp = tTh.GetProperty("Value");
                    bool IsNewFld = true;
                    TheFieldInfo tFldInfo = TheNMIEngine.GetFieldById(TheThing.GetSafePropertyGuid(tTh, "FldID"));
                    if (tFldInfo == null)
                        tFldInfo = new TheFieldInfo(tTh, "Value", tfldNo, tFlags & 0xFFBF, tTh.GetBaseThing().cdeA);
                    else
                    {
                        tFldInfo.FldOrder = tfldNo;
                        tFldInfo.Flags = tFlags;
                        IsNewFld = false;
                    }
                    if (tFldInfo.PropertyBag == null)
                        tFldInfo.PropertyBag = new ThePropertyBag();
                    ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, "IsOnTheFly", "=", "True");
                    ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, "UXID", "=", $"{tTh.cdeMID}");
                    tFldInfo.Header = tTh.FriendlyName;
                    RegisterNMISubscription(tClientInfo, "Value", tFldInfo);

                    string tControlType = TheThing.GetSafePropertyString(tTh, "ControlType");
                    if (TheCommonUtils.CInt(tControlType) == 0 && !TheCommonUtils.IsNullOrWhiteSpace(tControlType))
                    {
                        tFldInfo.Type = eFieldType.UserControl;
                        RegisterFieldEvents(tTh, ValProp, IsNewFld, tFldInfo, tControlType);
                    }
                    else
                        tFldInfo.Type = (eFieldType)TheCommonUtils.CInt(tControlType);
                    tFldInfo.DefaultValue = ValProp?.ToString();
                    tFldInfo.TileWidth = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(tTh, "TileWidth"));
                    tFldInfo.TileHeight = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(tTh, "TileHeight"));

                    foreach (cdeP prop in tTh.GetNMIProperties())
                    {
                        ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, prop.Name, "=", prop.ToString());
                    }
                    if (tFldInfo.Type == eFieldType.TileButton)
                    {
                        tTh.DeclareNMIProperty("IsDown", ePropertyTypes.TBoolean);
                        ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, "EnableTap", "=", "True");
                        tFldInfo.RegisterUXEvent(tTh, eUXEvents.OnPropertyChanged, "IsDown", (pThing, pObj) =>
                        {
                            if (!(pObj is TheProcessMessage pMsg) || pMsg.Message == null) return;
                            TheThing.SetSafePropertyBool(pThing, "IsDown", TheCommonUtils.CBool(pMsg.Message.PLS));
                        });
                    }
                    tI.FormFields.Add(tFldInfo);
                    TheThing.SetSafePropertyGuid(tTh, "FldID", tFldInfo.cdeMID);
                    TheNMIEngine.AddField(tI, tFldInfo);
                    fldNo += 10;
                }
            }
            return tInfo.GetLocalizedScreen(tClientInfo.LCID);
        }

        internal static void RegisterFieldEvents(TheThing tTh, cdeP ValProp, bool IsNewFld, TheFieldInfo tFldInfo, string tControlType)
        {
            TheControlType tType = TheNMIEngine.GetControlTypeByType(tControlType);
            if (tType == null)
                return;
            ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, "EngineName", "=", tType.BaseEngineName);
            if (IsNewFld && !string.IsNullOrEmpty(tType.SmartControlType) && ValProp != null)
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                {
                    Type type = assembly.GetType(tType.SmartControlType);
                    if (type != null)
                    {
                        MethodInfo info = type.GetMethod("RegisterEvents",
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                        if (info != null)
                        {
                            object pResult = info.Invoke(null, new object[] { tTh, tFldInfo });
                            if (TheCommonUtils.CBool(pResult))
                            {
                                ThePropertyBag.PropBagUpdateValue(tFldInfo.PropertyBag, "OnThingEvent", "=", string.Format("{0};{1}", tTh.cdeMID, "Value"));
                                ValProp.RegisterEvent(eThingEvents.PropertyChanged, tFldInfo.sinkUpdate);
                            }
                            break;
                        }
                    }
                }
            }
        }

        internal static TheScreenInfo GenerateScreen(Guid pScreenId, TheClientInfo pClientInfo)
        {
            if (TheCDEngines.MyNMIService == null)
                return null;

            TheScreenInfo tInfo = new TheScreenInfo
            {
                cdeMID = pScreenId,
                MyDashboard = TheNMIEngine.GetDashboardById(pScreenId),
                MyStorageInfo = new List<TheFormInfo>(),
                MyStorageMeta = new cdeConcurrentDictionary<string, TheFormInfo>(),
                MyStorageMirror = new List<object>(),
                MyDashPanels = new List<TheDashPanelInfo>()
            };
            if (tInfo.MyDashboard != null)
                tInfo.cdeO = tInfo.MyDashboard.cdeO;
            if (tInfo.MyDashboard == null || !TheUserManager.HasUserAccess(pClientInfo.UserID, tInfo.cdeA) || !TheUserManager.HasUserAccess(pClientInfo.UserID, tInfo.MyDashboard.cdeA))
            {
                TheBaseAssets.MySYSLOG.WriteToLog(7678, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.NMIService, $"Generate-Screen {pScreenId} failed: {(tInfo.MyDashboard != null ? "User has no access" : "Dashboard not found")}", eMsgLevel.l2_Warning));
                return null;    //Fix for Dead FR and NMI Icon
            }
            if (tInfo.MyDashboard.colPanels != null)
            {
                foreach (string tDashID in tInfo.MyDashboard.colPanels.ToList())
                {
                    // CODE REVIEW: How do we find the engine with the localized resources for a dashpanel? May need to add this to TheDashPanelInfo
                    TheDashPanelInfo tD = ParsePanelInfo(tDashID);
                    if (tD == null || !TheUserManager.HasUserAccess(pClientInfo.UserID, tD.cdeA)) continue;
                    tInfo.MyDashPanels.Add(tD);
                }
            }

            tInfo.MyDashPanels = tInfo.MyDashPanels.Where((s) => TheUserManager.HasUserAccess(pClientInfo.UserID, s.cdeA) &&
            (s.Flags == 0 ||
             (((s.Flags & 2) != 0 || !pClientInfo.IsOnCloud || pClientInfo.IsUserTrusted) &&
              ((s.Flags & 4) == 0 || !pClientInfo.IsMobile) &&
              ((s.Flags & 128) == 0 || pClientInfo.IsFirstNode || pClientInfo.IsUserTrusted)))
             ).OrderBy(s => s.Category).ThenBy(s => s.FldOrder).ToList();
            if (tInfo.MyDashPanels != null)
            {
                List<Guid> AlreadyFound = new List<Guid>();
                foreach (TheDashPanelInfo tDP in tInfo.MyDashPanels)
                {
                    string tCtrl = "";
                    bool IsInHtml = false;
                    string[] tParts = null;
                    tDP.FireUpdate();
                    if (!string.IsNullOrEmpty(tDP.ControlClass))
                    {
                        tCtrl = tDP.ControlClass;
                        tParts = tCtrl.Split(':');
                    }
                    var tDataItem = ThePropertyBag.PropBagGetValue(tDP.PropertyBag, "DataItem", "=");
                    var tThing = RegisterNMISubscription(pClientInfo, tDataItem, tDP);
                    var tsuc = TheNMIEngine.ParseFacePlateUrlInternal(tThing, ThePropertyBag.PropBagGetValue(tDP.PropertyBag, "HTMLUrl", "="), false, pClientInfo.NodeID);
                    if (!tsuc)
                        TheNMIEngine.ParseFacePlateInternal(tThing, ThePropertyBag.PropBagGetValue(tDP.PropertyBag, "HTML", "="), pClientInfo.NodeID);

                    if (!string.IsNullOrEmpty(tDP.HtmlSource))
                    {
                        tCtrl = FindCmpGuid(tDP.HtmlSource);
                        if ("ControlClass".Equals(tCtrl))
                            IsInHtml = true;
                    }
                    if (!string.IsNullOrEmpty(tCtrl))
                    {
                        if (tParts != null && tParts.Length > 1)
                        {
                            switch (tParts[0])
                            {
                                case "CMyInfo":
                                case "CMyNavigator":
                                case "cdeUpdater":
                                case "jsAction":
                                    break;
                                default:
                                    Guid tMG = TheCommonUtils.CGuid(tParts[1]);
                                    if (tMG.Equals(Guid.Empty)) continue;
                                    if (IsInHtml) //Legacy! Should not be used as it will not work with other NMI Runtime Renderers
                                        tDP.HtmlContent = tDP.HtmlSource.Replace("<%=CMP:ControlClass%>", "<div id=\"Inline_" + TheCommonUtils.cdeGuidToString(TheCommonUtils.CGuid(tParts[1])) + "\" class=\"" + tParts[0] + "\"></div>");
                                    if (AlreadyFound.Contains(tMG)) continue;
                                    AlreadyFound.Add(tMG);
                                    TheFormInfo tI = GenerateForm(tMG, pClientInfo);
                                    if (tI != null && (!tI.GetFromFirstNodeOnly || pClientInfo.IsFirstNode)) //TODO-V4: VERIFY
                                    {
                                        tI.TargetElement = tDP.cdeMID.ToString();
                                        tInfo.MyStorageInfo.Add(tI);
                                    }
                                    break;
                            }
                        }
                    }
                    else
                        tDP.HtmlContent = tDP.HtmlSource;
                }
            }
            return tInfo;
        }

        internal static void RefreshLiveScreens(TSM pMsg, TheClientInfo tClientInfo)
        {
            if (tClientInfo == null) return;
            List<TheThing> tForms = TheThingRegistry.GetThingsByProperty("*", tClientInfo.UserID, "DeviceType", eKnownDeviceTypes.TheNMIScreen);
            if (tForms != null && tForms.Count > 0)
            {
                foreach (TheThing tThing in tForms)
                {
                    TheScreenInfo tI = GenerateLiveScreen(tThing.cdeMID, tClientInfo);
                    if (tI != null)
                    {
                        TSM tTsm = new TSM(eEngineName.NMIService, "NMI_LIVESCREENMETA:" + tThing.cdeMID.ToString());
                        tI.ForceReload = true;
                        tTsm.PLS = TheCommonUtils.SerializeObjectToJSONString(tI);
                        TheCommCore.PublishToOriginator(pMsg, tTsm);
                    }
                }
            }
        }

        internal static void AssembleDynamicScreens(TheScreenInfo pInfo, TheClientInfo tClientInfo)
        {
            List<TheThing> tList = TheThingRegistry.GetThingsByProperty("*", tClientInfo.UserID, "IsLiveTag", "True");
            if (tList!=null && tList.Any())
            {
                string oldForm = "";
                foreach (TheThing tTag in tList.OrderBy(s=>TheThing.GetSafePropertyString(s,"FormName")))
                {
                    string tNewFormTitle = TheThing.GetSafePropertyString(tTag, "FormName");
                    if (!oldForm.Equals(tNewFormTitle))
                    {
                        ICDEThing tFormThing = TheThingRegistry.GetThingByFunc("*", s => s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID && TheThing.GetSafePropertyString(s, "FriendlyName") == tNewFormTitle && TheThing.GetSafePropertyString(s, "DeviceType") == eKnownDeviceTypes.TheNMIScreen && (s.UID == Guid.Empty || s.UID == tClientInfo.UserID));
                        if (tFormThing == null)
                        {
                            tFormThing = new TheNMIScreen(null);
                            TheThing.SetSafePropertyString(tFormThing, "FriendlyName", tNewFormTitle);
                            TheThing.SetSafePropertyString(tFormThing, "Category", "Live Screens");
                            TheThing.SetSafePropertyNumber(tFormThing, "FldOrder", 1000);
                            TheThing.SetSafePropertyNumber(tFormThing, "Flag", 3);
                            TheThingRegistry.RegisterThing(tFormThing);
                        }
                        int tFldOrder = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(tFormThing, "FldOrder"));
                        if (tFldOrder == 0) tFldOrder = 1000;
                        int tFlag = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(tFormThing, "Flag"));
                        TheDashPanelInfo tD = new TheDashPanelInfo(tFormThing.GetBaseThing())
                        {
                            cdeO = tTag.cdeMID, // tFormThing.GetBaseThing().cdeMID, // CODE REVIEW/TODO cdeO indicates the plug-in from which localizaed resources are read: is this sufficient for all live screens?
                            cdeMID = tFormThing.GetBaseThing().cdeMID,
                            cdeA = tFormThing.GetBaseThing().cdeA,
                            Flags = tFlag,
                            FldOrder = tFldOrder,
                            PanelTitle = tNewFormTitle,
                            ControlClass = eScreenClass.CMyLiveScreen + ":" + tFormThing.GetBaseThing().cdeMID.ToString(),
                            Category = TheThing.GetSafePropertyString(tFormThing, "Category") + " : " + TheBaseAssets.MyServiceHostInfo.MyStationName
                        };
                        pInfo.MyDashPanels.Add(tD);
                        oldForm = tNewFormTitle;
                    }
                }
            }

            var tFormThings = TheThingRegistry.GetThingsByFunc("*", s => s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID && TheThing.GetSafePropertyString(s, "DeviceType") == eKnownDeviceTypes.TheNMIScreen && (s.UID == Guid.Empty || s.UID == tClientInfo.UserID));
            foreach (var tFormThing in tFormThings)
            {
                if (pInfo.MyDashPanels.Any(s => s.cdeMID == tFormThing.cdeMID))
                    continue;
                int tFldOrder = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(tFormThing, "FldOrder"));
                if (tFldOrder == 0) tFldOrder = 1000;
                int tFlag = TheCommonUtils.CInt(TheThing.GetSafePropertyNumber(tFormThing, "Flag"));
                TheDashPanelInfo tD = new TheDashPanelInfo(tFormThing.GetBaseThing())
                {
                    cdeMID = tFormThing.GetBaseThing().cdeMID,
                    cdeA = tFormThing.GetBaseThing().cdeA,
                    Flags = tFlag,
                    FldOrder = tFldOrder,
                    PanelTitle = tFormThing.FriendlyName,
                    ControlClass = eScreenClass.CMyLiveScreen + ":" + tFormThing.GetBaseThing().cdeMID.ToString(),
                    Category = TheThing.GetSafePropertyString(tFormThing, "Category") + " : " + TheBaseAssets.MyServiceHostInfo.MyStationName
                };
                pInfo.MyDashPanels.Add(tD);
            }
        }

        private static string FindCmpGuid(string cmpInfo)
        {
            int pos;
            if ((pos = cmpInfo.IndexOf("<%=CMP:", StringComparison.Ordinal)) >= 0)
            {
                int pos2 = cmpInfo.IndexOf("%>", pos, StringComparison.Ordinal);
                if (pos2 >= 0)
                    return cmpInfo.Substring(pos + 7, pos2 - pos - 7);
            }
            return null;
        }

        internal static TheDashPanelInfo ParsePanelInfo(string strPanelInfo)
        {
            if (TheCDEngines.MyNMIService == null)
                return null;

            TheDashPanelInfo tPanelInfo = null;
            string[] DashCommand = TheCommonUtils.cdeSplit(strPanelInfo, ";:;", false, false);
            if (DashCommand != null && DashCommand.Length > 1)
            {
                string cate = ""; if (DashCommand.Length > 1) cate = DashCommand[1];
                string[] tCat = cate.Split(':');
                Guid PanelGuid = Guid.Empty;
                if (tCat.Length > 1)
                {
                    cate = tCat[1];
                    PanelGuid = TheCommonUtils.CGuid(tCat[0]);
                }
                if (PanelGuid == Guid.Empty)
                    PanelGuid = Guid.NewGuid();
                switch (DashCommand[0])
                {
                    case "ACT":
                    case "CMP":
                        string title = ""; if (DashCommand.Length > 2) title = DashCommand[2];
                        string cmd = ""; if (DashCommand.Length > 3) cmd = DashCommand[3];
                        string descr = ""; if (DashCommand.Length > 4) descr = DashCommand[4];
                        bool tIsLocked = false; if (DashCommand.Length > 5) tIsLocked = TheCommonUtils.CBool(DashCommand[5]);
                        int PanelColor = -1; if (DashCommand.Length > 6) PanelColor = TheCommonUtils.CInt(DashCommand[6]);
                        string smallback = "RES(C-Labs-DialogItemBackColor)"; if (DashCommand.Length > 7) smallback = DashCommand[7];
                        string medback = "RES(C-Labs-DialogItemBackColor)"; if (DashCommand.Length > 8) medback = DashCommand[8];
                        string fullback = ""; if (DashCommand.Length > 9) fullback = DashCommand[9];
                        bool bIsControl = DashCommand[0].Equals("ACT");
                        tPanelInfo = new TheDashPanelInfo(TheCDEngines.MyNMIService.GetBaseThing(), PanelGuid, title, cmd) {Description = descr}; // CODE REVIEW/TODO: Is this still used? How can we obtain the owning engine for loc string lookup?
                        ThePropertyBag.PropBagUpdateValue(tPanelInfo.PropertyBag, "BackgroundSmall", "=", smallback);
                        ThePropertyBag.PropBagUpdateValue(tPanelInfo.PropertyBag, "BackgroundMedium", "=", medback);
                        ThePropertyBag.PropBagUpdateValue(tPanelInfo.PropertyBag, "BackgroundFull", "=", fullback);
                        ThePropertyBag.PropBagUpdateValue(tPanelInfo.PropertyBag, "PanelColor", "=", PanelColor.ToString());
                        tPanelInfo.IsControl = bIsControl;
                        if (cmd.Equals("CMyInfo") || tIsLocked) tPanelInfo.IsPinned = true;
                        if (cmd.StartsWith("CMyNavigator"))
                        {
                            string[] tNaviParts = cmd.Split(':');
                            tPanelInfo.Navigator = tNaviParts[1];
                            if (tNaviParts.Length > 2)
                                ThePropertyBag.PropBagUpdateValue(tPanelInfo.PropertyBag, "Thumbnail", "=", tNaviParts[2]);
                            tPanelInfo.IsControl = true;
                        }
                        break;
                    case "EXT":
                        Guid tInfo = Guid.Empty; if (DashCommand.Length > 2) tInfo = TheCommonUtils.CGuid(DashCommand[2]);
                        bool tIsLocked2 = false; if (DashCommand.Length > 3) tIsLocked2 = TheCommonUtils.CBool(DashCommand[3]);
                        if (tInfo == Guid.Empty)
                        {
                            tPanelInfo = TheNMIEngine.GetDashPanelById(tInfo);
                            if (tPanelInfo != null)
                                tPanelInfo.IsPinned = tIsLocked2;
                        }
                        break;
                    default:
                        tPanelInfo = TheNMIEngine.GetDashPanelById(TheCommonUtils.CGuid(DashCommand[0]));
                        break;
                }
                if (tPanelInfo != null)
                    tPanelInfo.Category = cate;
            }
            else
            {
                tPanelInfo = TheNMIEngine.GetDashPanelById(TheCommonUtils.CGuid(strPanelInfo));
            }
            if (tPanelInfo != null && !string.IsNullOrEmpty(tPanelInfo.PanelTitle)) tPanelInfo.PanelTitle = TheCommonUtils.GenerateFinalStr(tPanelInfo.PanelTitle);
            if (tPanelInfo != null)
                tPanelInfo.ControlClass = TheCommonUtils.GenerateFinalStr(tPanelInfo.ControlClass);
            return tPanelInfo;
        }
    }
}
