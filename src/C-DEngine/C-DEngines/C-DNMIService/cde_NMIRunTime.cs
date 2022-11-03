// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace nsCDEngine.Engines.NMIService
{
    /// <summary>
    /// The NMI Engine Main Class
    /// </summary>
    public partial class TheNMIEngine
    {
        #region Private and Internal Methods
        private readonly cdeConcurrentDictionary<string, IBaseEngine> MyRegisteredEngines = new ();
        private bool InPrefUpd;
        private bool InManUpdate;

        private void ProcessServiceMessage(TheProcessMessage pMsg)
        {
            if (pMsg?.Message?.TXT == null)
                return;
            string[] cmd = pMsg.Message.TXT.Split(':');
            TheClientInfo tClientInfo = TheCommonUtils.GetClientInfo(pMsg);
            switch (cmd[0])
            {
                case "NMI_MY_LOCATION":
                    if (!string.IsNullOrEmpty(pMsg.Message?.PLS))
                    {
                        try
                        {
                            var tLocParts = pMsg.Message?.PLS.Split(';');
                            if (tLocParts?.Length > 2)
                            {
                                var tLocInfo = new TheNMILocationInfo { cdeN = pMsg.Message.GetOriginatorSecurityProxy(), Accuracy = TheCommonUtils.CDbl(tLocParts[2]), Latitude = TheCommonUtils.CDbl(tLocParts[1]), Longitude = TheCommonUtils.CDbl(tLocParts[0]), ClientInfo = tClientInfo, Description = tClientInfo != null && tClientInfo.UserID != Guid.Empty ? TheUserManager.GetUserFullName(tClientInfo.UserID) : "Unknown User" };
                                FireEvent(cmd[0], this, tLocInfo, true);
                            }
                        }
                        catch (Exception) 
                        { 
                            //ignored
                        }
                    }
                    break;
                case "NMI_NODEPING":
                    TheFormsGenerator.UpdateRegisteredNMINode(pMsg.Message.GetOriginator());
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_NODEPONG"));
                    break;
                case "GET_CHARTDATA":
                    nsCDEngine.Engines.StorageService.TheStorageUtilities.PushChartsData(TheCommonUtils.CGuid(pMsg.Message.PLS), pMsg.Message.GetOriginator());
                    break;
                case "NMI_GET_UIDACL":
                    TheUserManager.SendACLToNMISilent(pMsg.Message.GetOriginator(), TheCommonUtils.CGuid(pMsg.Message.PLS), tClientInfo);
                    break;
                case "NMI_SHOW_SCREEN":
                    {
                        if (string.IsNullOrEmpty(pMsg.Message?.PLS)) return;
                        TheFormInfo tForm = TheNMIEngine.GetFormById(TheCommonUtils.CGuid(pMsg.Message.PLS));
                        tForm?.FireEvent(eUXEvents.OnShow, new TheProcessMessage() { CurrentUserID = tClientInfo.UserID, ClientInfo = tClientInfo, Message = pMsg.Message });
                        if (tForm == null)
                        {
                            TheDashboardInfo tDB = TheNMIEngine.GetDashboardById(TheCommonUtils.CGuid(pMsg.Message.PLS));
                            tDB?.FireEvent(eUXEvents.OnShow, new TheProcessMessage() { CurrentUserID = tClientInfo.UserID, ClientInfo = tClientInfo, Message = pMsg.Message });
                            if (tDB == null)
                            {
                                TheDashPanelInfo tDP = TheNMIEngine.GetDashPanelById(TheCommonUtils.CGuid(pMsg.Message.PLS));
                                tDP?.FireEvent(eUXEvents.OnShow, new TheProcessMessage() { CurrentUserID = tClientInfo.UserID, ClientInfo = tClientInfo, Message = pMsg.Message });
                            }
                        }
                    }
                    break;
                case "NMI_FIELD_EVENT":
                    if (cmd.Length > 1)
                    {
                        var tTgd = TheCommonUtils.CGuid(cmd[1]);
                        GetFieldById(tTgd)?.FireEvent(cmd[0], pMsg);
                    }
                    break;
                case "NMI_UPD_DATA":
                case "NMI_DEL_ID": // "NMI_DEL_DATA": retired because not used
                    if (cmd.Length > 1)
                    {
                        if (cmd[1].StartsWith("PB"))
                        {
                            Guid tThingGuid = TheCommonUtils.CGuid(cmd[1].Substring(2));
                            TheThing tMything = TheThingRegistry.GetThingByMID("*", tThingGuid);
                            if (tMything != null && tMything.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)
                            {
                                try
                                {
                                    cdeP tProperties = TheCommonUtils.DeserializeJSONStringToObject<cdeP>(pMsg.Message.PLS);
                                    if (tProperties != null)
                                    {
                                        cdeP tP = tMything.SetProperty(tProperties.Name, tProperties.ToString());
                                        if ((tP.cdeE & 0x40) != 0)
                                            TheThingRegistry.UpdateThing(tMything, false);
                                    }
                                }
                                catch
                                {
                                    //Supress
                                }
                            }
                            break;
                        }
                        string tTableName = TheCommonUtils.CGuid(cmd[1]).ToString();
                        if (cmd[0].Equals("NMI_UPD_DATA") && (!string.IsNullOrEmpty(UserPrefID) && UserPrefID == tTableName))
                        {
                            if (InPrefUpd)
                                return;
                            InPrefUpd = true;
                            TheUserDetails tUpdUser = TheUserManager.GetUserByID(TheCommonUtils.CGuid(cmd[2]));
                            if (tUpdUser != null)
                            {
                                tUpdUser = TheCommonUtils.DeserializeJSONStringToObject<TheUserDetailsI>(pMsg.Message.PLS).CloneToUser(pMsg.CurrentUserID);
                                if (tUpdUser.IsOnCurrentNode())
                                {
                                    int tLCID = TheUserManager.UpdateUserI(tUpdUser);
                                    if (tLCID >= 0)
                                        TheBaseAssets.MySession.UpdateSessionLCID(TheCommonUtils.CGuid(pMsg.Message.SEID), tLCID);
                                }
                                else
                                {
                                    TSM tTsm = LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", "###User cannot be edited on this node!###"));
                                    TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                    InPrefUpd = false;
                                    return;
                                }
                            }
                            InPrefUpd = false;
                            return;
                        }
                        if (!string.IsNullOrEmpty(UserManID) && UserManID == tTableName)
                        {
                            if (InManUpdate || !pMsg.Message.IsFirstNode() || !TheUserManager.CloudCheck(pMsg, 128, true, tClientInfo)) return;
                            InManUpdate = true;
                            try
                            {
                                Guid tUserId = TheCommonUtils.CGuid(cmd[2]);
                                if (tUserId != Guid.Empty)
                                {
                                    TheUserDetails tUpdUser = TheUserManager.GetUserByID(TheCommonUtils.CGuid(cmd[2]));
                                    if (tUpdUser != null)
                                    {
                                        if (cmd[0].Equals("NMI_DEL_ID"))
                                        {
                                            if (tUpdUser.IsOnCurrentNode())
                                            {
                                                if (tUpdUser.IsReadOnly)
                                                {
                                                    TSM tTsm = LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", "###User cannot be deleted!###"));
                                                    TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                                    InManUpdate = false;
                                                    return;
                                                }
                                                TheUserManager.RemoveUser(tUpdUser);
                                                TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_SET_DATA:" + tTableName, TheUserManager.SendUserList(pMsg.CurrentUserID)));  //Sends The user List to only a first node connection browser on premise - NOT via the cloud
                                                TheBaseAssets.MySYSLOG.WriteToLog(7678, new TSM(eEngineName.NMIService, cmd[0], eMsgLevel.l6_Debug, pMsg.Message.PLS));
                                            }
                                            else
                                            {
                                                TSM tTsm = LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", "###User cannot be deleted on this node!###"));
                                                TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                                InManUpdate = false;
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            //NewtonSoft Fix
                                            tUpdUser = TheCommonUtils.DeserializeJSONStringToObject<TheUserDetailsI>(pMsg.Message.PLS).CloneToUser(pMsg.CurrentUserID);
                                            if (tUpdUser.IsOnCurrentNode())
                                            {
                                                TheUserManager.UpdateUserI(tUpdUser);
                                            }
                                            else
                                            {
                                                TSM tTsm = LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", "###User cannot be edited on this node!"));
                                                TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                                InManUpdate = false;
                                                return;
                                            }
                                        }
                                        TheUserManager.ResetUserScopes(true, true);
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                TSM tTsm = LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", "###Error during User Update - no update done!###"));
                                TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                TheBaseAssets.MySYSLOG.WriteToLog(7678, new TSM(eEngineName.NMIService, "User Manager Update error", eMsgLevel.l1_Error));
                            }
                            InManUpdate = false;
                            return;
                        }
                        TheFormInfo tTable = MyNMIModel.MyForms.MyMirrorCache.GetEntryByID(tTableName);
                        if (tTable != null)
                        {
                            if (!eEngineName.ContentService.Equals(tTable.OwnerEngine) && !eEngineName.NMIService.Equals(tTable.OwnerEngine))
                            {
                                IBaseEngine tBase = TheThingRegistry.GetBaseEngine(tTable.OwnerEngine);
                                if (tBase != null)
                                {
                                    TSM tForward = TSM.Clone(pMsg.Message, true);
                                    tForward.ENG = tTable.OwnerEngine;
                                    tBase.ProcessMessage(tForward);
                                }
                                //return; //No processing here
                            }
                            else
                            {
                                if (tTable.GetFromFirstNodeOnly && !pMsg.Message.IsFirstNode()) return;
                                if (tTable.GetFromServiceOnly && !MyBaseEngine.GetEngineState().IsService) return;
                                try
                                {
                                    object MyStorageMirror = TheCDEngines.GetStorageMirror(tTable.defDataSource);
                                    if (MyStorageMirror != null)
                                    {
                                        Type magicType = MyStorageMirror.GetType();
                                        MethodInfo magicMethod;
                                        switch (cmd[0])
                                        {
                                            case "NMI_UPD_DATA":
                                                {
#if CDE_STANDARD  //Metro Style Reflection
                                                    magicMethod = magicType.GetTypeInfo().GetDeclaredMethod("UpdateFromJSON");
#else
                                                    magicMethod = magicType.GetMethod("UpdateFromJSON", BindingFlags.NonPublic | BindingFlags.Instance);
#endif
                                                    if (magicMethod != null)
                                                    {
                                                        string tDirtyMask = "*";
                                                        if (cmd.Length > 4)
                                                            tDirtyMask = cmd[4];
                                                        string tUpdated = magicMethod.Invoke(MyStorageMirror, new[] { tTable, pMsg.Message.PLS, tDirtyMask, (object)pMsg.Message.GetOriginator(), tClientInfo, TheCommonUtils.CGuid(pMsg.Message.SEID), null }).ToString();
                                                        if (!string.IsNullOrEmpty(tUpdated))
                                                        {
                                                            TSM tTsm = new (eEngineName.NMIService, "NMI_UPD_DATA_RET:"); 
                                                            if (!tTable.IsAlwaysEmpty)
                                                                tTsm.SetOriginator(pMsg.Message.GetOriginator());
                                                            tTsm.TXT += tTableName + ":" + cmd[3];
                                                            tTsm.PLS = tUpdated;
                                                            TheCommCore.PublishCentral(tTsm);
                                                            foreach (var (tIn, sTSM) in from TheFormInfo tIn in MyNMIModel.MyForms.MyMirrorCache.TheValues.Where(s => s.defDataSource == tTable.defDataSource)
                                                                                        where !tIn.cdeMID.ToString().Equals(tTableName)
                                                                                        let sTSM = TSM.Clone(tTsm, false)
                                                                                        select (tIn, sTSM))
                                                            {
                                                                sTSM.TXT = "NMI_UPD_DATA_RET:" + tIn.cdeMID + ":" + cmd[3];
                                                                TheCommCore.PublishCentral(sTSM);
                                                            }
                                                        }
                                                    }
                                                }
                                                break;
                                            case "NMI_DEL_ID":
                                                {
#if CDE_STANDARD  //Metro Style Reflection
                                                    magicMethod = magicType.GetTypeInfo().GetDeclaredMethod("DeleteByID"); 
#else
                                                    magicMethod = magicType.GetMethod("DeleteByID", BindingFlags.NonPublic | BindingFlags.Instance);
#endif
                                                    if (magicMethod != null)
                                                    {
                                                        object tRes = magicMethod.Invoke(MyStorageMirror, new[] { tTable, (object)cmd[2], tClientInfo, null });
                                                        if (tRes != null && tRes.ToString() != true.ToString())
                                                        {
                                                            TSM tTsm = LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", $"{tRes.ToString().Substring(4)} {cmd[2]}"));
                                                            TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                                        }
                                                    }
                                                }
                                                break;
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(7678, new TSM(eEngineName.NMIService, cmd[0] + " error", eMsgLevel.l1_Error, e.ToString()));
                                }
                            }
                        }
                    }
                    break;
                case "NMI_INS_DATA":
                    {
                        string tTableName2 = TheCommonUtils.CGuid(cmd[1]).ToString();
                        if (!string.IsNullOrEmpty(UserManID) && UserManID == tTableName2)
                        {
                            if (!pMsg.Message.IsFirstNode() || !TheUserManager.CloudCheck(pMsg, 128, true, tClientInfo)) return;
                            try
                            {
                                TheUserDetailsI tRemUser = TheCommonUtils.DeserializeJSONStringToObject<TheUserDetailsI>(pMsg.Message.PLS);
                                TheUserDetails tNewUser = tRemUser.CloneToUser(pMsg.CurrentUserID);
                                if (string.IsNullOrEmpty(tNewUser.EMail))
                                {
                                    TSM tTsm = LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", "###A valid and unique email is required!###"));
                                    TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                    break;
                                }
                                tNewUser.cdeMID = Guid.NewGuid();
                                tNewUser.cdeCTIM = DateTimeOffset.Now;
                                tNewUser.NodeScope = "ALL";
                                if (string.IsNullOrEmpty(tNewUser.AssignedEasyScope)) //At this point this should aways be null
                                    tNewUser.AssignedEasyScope = "*";   //OK
                                TheUserManager.AddNewUser(tNewUser);
                                string modelId2 = "";
                                if (cmd.Length > 2) modelId2 = cmd[2];
                                TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, string.Format("NMI_SET_DATA:{0}:{1}:{0}", UserManID, modelId2), TheUserManager.SendUserList(pMsg.CurrentUserID))); //Sends The user List to only a first node connection browser on premise - NOT via the cloud
                                TheUserManager.ResetUserScopes(true, true);
                            }
                            catch (Exception ee)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(7678, new TSM(eEngineName.NMIService, "WRA-INS-DATA: TheUserDetails ERROR", eMsgLevel.l1_Error, ee.ToString()));
                                TSM tTsm = LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", "###Could not insert error. Check Fields!###"));
                                TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                            }
                            return;
                        }
                        TheFormInfo tTableInsert = MyNMIModel.MyForms.MyMirrorCache.GetEntryByID(tTableName2);
                        if (tTableInsert != null)
                        {
                            if (!eEngineName.ContentService.Equals(tTableInsert.OwnerEngine) && !eEngineName.NMIService.Equals(tTableInsert.OwnerEngine))
                            {
                                IBaseEngine tBase = TheThingRegistry.GetBaseEngine(tTableInsert.OwnerEngine);
                                if (tBase != null)
                                {
                                    TSM tForward = TSM.Clone(pMsg.Message, true);
                                    tForward.ENG = tTableInsert.OwnerEngine;
                                    tBase.ProcessMessage(tForward);
                                }
                                //return; //No processing here
                            }
                            else
                            {
                                if (tTableInsert.GetFromFirstNodeOnly && !pMsg.Message.IsFirstNode()) return;
                                if (tTableInsert.GetFromServiceOnly && !MyBaseEngine.GetEngineState().IsService) return;
                                try
                                {
                                    object MyStorageMirror = TheCDEngines.GetStorageMirror(tTableInsert.defDataSource);
                                    if (MyStorageMirror != null)
                                    {
                                        if (cmd.Length > 3)
                                            tClientInfo.FormID = TheCommonUtils.CGuid(cmd[3]);
                                        Type magicType = MyStorageMirror.GetType();
#if CDE_STANDARD  //Metro Style Reflection
                                        var magicMethod = magicType.GetTypeInfo().GetDeclaredMethod("InsertFromJSON");
#else
                                        var magicMethod = magicType.GetMethod("InsertFromJSON", BindingFlags.NonPublic | BindingFlags.Instance);
#endif
                                        if (magicMethod != null)
                                        {
                                            var tRes = magicMethod.Invoke(MyStorageMirror, new[] { tTableInsert, (object)pMsg.Message.PLS, tClientInfo, null });
                                            if (cmd.Length > 2 && TheCommonUtils.CBool(tRes))
                                                SendNMIData(pMsg.Message, tClientInfo, (cmd.Length > 3 && TheCommonUtils.CGuid(cmd[3]) != TheCommonUtils.CGuid(tTableInsert.AddTemplateType)) ? cmd[3] : tTableName2, "CMyTable", tTableName2, cmd[2], false, false);
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(7678, new TSM(eEngineName.NMIService, cmd[0] + " error", eMsgLevel.l1_Error, e.ToString()));
                                }
                            }
                        }
                    }
                    break;
                case "NMI_GET_GLOBAL_IMAGE":
                    {
                        List<IBaseEngine> tBases2 = TheThingRegistry.GetBaseEngines(true);
                        byte[] astream = null;
                        if (tBases2 != null && tBases2.Count > 0)
                        {
                            foreach (IBaseEngine tBase in tBases2)
                            {
                                astream = tBase.GetPluginResource(pMsg.Message.PLS);
                                if (astream != null)
                                    break;
                            }
                        }
                        astream ??= TheCommonUtils.GetSystemResource(null, pMsg.Message.PLS);
                        if (astream != null)
                        {
                            var tTopic = $"NMI_GLOBAL_{cmd[0].Substring(15)}:{pMsg.Message.PLS}";
                            ThePlanarImage tImg = new ()
                            {
                                ImageSource = pMsg.Message.PLS,
                                Bits = astream
                            };
                            TSM tTsm = new (eEngineName.NMIService, tTopic) { PLS = TheCommonUtils.SerializeObjectToJSONString(tImg) };
                            TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                            return;
                        }
                    }
                    break;
                case "NMI_GET_GLOBAL_RESOURCE":
                case "NMI_GET_GLOBAL_STYLE":
                case "NMI_GET_GLOBAL_SCRIPT":
                    {
                        List<IBaseEngine> tBases2 = TheThingRegistry.GetBaseEngines(true);
                        if (tBases2 != null && tBases2.Count > 0)
                        {
                            foreach (IBaseEngine tBase in tBases2)
                            {
                                byte[] astream = tBase.GetPluginResource(pMsg.Message.PLS);
                                if (astream != null)
                                {
                                    var tTopic = $"NMI_GLOBAL_{cmd[0].Substring(15)}:{pMsg.Message.PLS}";
                                    TSM tTsm = new (eEngineName.NMIService, tTopic) { PLS = TheCommonUtils.CArray2UTF8String(astream) };
                                    TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                    return;
                                }
                            }
                        }
                        byte[] bstream = TheCommonUtils.GetSystemResource(null, pMsg.Message.PLS);
                        if (bstream != null)
                        {
                            var tTopic = $"NMI_GLOBAL_{cmd[0].Substring(15)}:{pMsg.Message.PLS}";
                            TSM tTsm = new (eEngineName.NMIService, tTopic) { PLS = TheCommonUtils.CArray2UTF8String(bstream) };
                            TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                            return;
                        }
                    }
                    break;
                case "NMI_GET_ENGINEJS":    //4.107: Moved to Content Engine - remains here for compatibility for older mesh nodes
                    {
                        List<IBaseEngine> tBases2 = TheThingRegistry.GetBaseEngines(true);
                        if (tBases2 != null && tBases2.Count > 0)
                        {
                            foreach (IBaseEngine tBase in tBases2)
                            {
                                if ((string.IsNullOrEmpty(pMsg.Message.PLS) || pMsg.Message.PLS == tBase.GetEngineName()))
                                {
                                    if (tBase.GetEngineState().HasJSEngine)
                                    {
                                        string teng = tBase.GetEngineName();
                                        byte[] astream = tBase.GetPluginResource(teng + ".js");
                                        if (astream == null)
                                        {
                                            string[] tEp = teng.Split('.');
                                            if (tEp.Length > 1)
                                                astream = tBase.GetPluginResource(tEp[1] + ".js");
                                        }
                                        if (astream != null)
                                        {
                                            TSM tTsm = new (eEngineName.NMIService, "NMI_ENGINEJS:" + teng) { PLS = TheCommonUtils.CArray2UTF8String(astream) };
                                            TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                        }
                                    }
                                    if (!string.IsNullOrEmpty(tBase.GetEngineState().CSS))
                                    {
                                        TSM tTsm = new (eEngineName.NMIService, "NMI_CUSTOM_CSS", tBase.GetEngineState().CSS);
                                        TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                    }
                                }
                            }
                        }
                    }
                    break;
                case "NMI_GET_DATA":
                    if (cmd.Length > 1)
                    {
                        switch (cmd[1])
                        {
                            case "7888BBC6A1A849F3B5668057EE69318F":    //Simplified ThingRegistry
                                if (!pMsg.Message.IsFirstNode() || !TheUserManager.HasUserAccess(pMsg.CurrentUserID, 0xF0) || cmd.Length < 4) return;
                                {
                                    TSM tTSM = new (eEngineName.NMIService, "NMI_SET_DATA:7888BBC6A1A849F3B5668057EE69318F:4F925FF043EF456395181391F7D179AB:noview");
                                    List<TheThing> tThings = TheThingRegistry.GetThingsOfEngine("*", true, true);
                                    List<TheThingPicker> tLst = new ();
                                    if (tThings != null)
                                    {
                                        foreach (var tT in tThings)
                                        {
                                            if (string.IsNullOrEmpty(tT.DeviceType) && !tT.IsInit())
                                                continue;
                                            var tFN = tT.FriendlyName;
                                            if (string.IsNullOrEmpty(tFN))
                                                tFN = $"no Name:{tT.cdeMID}";
                                            List<cdeP> tLstP = tT.GetAllProperties();
                                            TheThingPicker tPick = new (){ EngineName = tT.EngineName, DeviceType = tT.DeviceType, FriendlyName = tFN, cdeMID = tT.cdeMID, IsRemote = (tT.cdeN != TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID), PB = new ThePropertyBag() };
                                            foreach (cdeP tP in tLstP)
                                            {
                                                if ((tP.cdeE & 1) != 0) //No encrypted properties are sent
                                                    continue;
                                                tPick.PB.Add($"{tP.Name}={tP.Value}");
                                            }
                                            tLst.Add(tPick);
                                        }
                                    }
                                    tTSM.PLS = TheCommonUtils.SerializeObjectToJSONString(tLst);
                                    TheCommCore.PublishToOriginator(pMsg.Message, tTSM);
                                }
                                return;
                            case "SCREENRESOLVE":
                                {
                                    if (!pMsg.Message.IsFirstNode() || cmd.Length < 4) return;
                                    var tForm = GetFormById(TheCommonUtils.CGuid(cmd[4]));
                                    if (tForm != null)
                                    {
                                        var tSP = cmd[3].Split(';');
                                        SetUXProperty(pMsg.Message.GetOriginator(), TheCommonUtils.CGuid(tSP[0]), $"ScreenFriendlyName={tForm.FormTitle}:{cmd[4]}", cmd[2], tSP.Length > 1 ? $"{tSP[1]};-1" : null);
                                    }
                                }
                                break;
                            case "THINGRESOLVE":
                                {
                                    if (!pMsg.Message.IsFirstNode() || cmd.Length < 4) return;
                                    var tSP = cmd[3].Split(';');
                                    TheFieldInfo tField = GetFieldById(TheCommonUtils.CGuid(tSP[0]));
                                    string ValueProperty = TheCommonUtils.CStr(tField?.PropBagGetValue("ValueProperty"));
                                    TheThing tThing = null;
                                    if (string.IsNullOrEmpty(ValueProperty))
                                        tThing = TheThingRegistry.GetThingByMID(TheCommonUtils.CGuid(cmd[4]), true);
                                    else
                                    {
                                        if (ValueProperty != "EngineName")
                                            tThing = TheThingRegistry.GetThingByProperty("*", Guid.Empty, ValueProperty, cmd[4]);
                                    }
                                    if (tThing == null && TheCommonUtils.CGuid(cmd[4]) == Guid.Empty)
                                    {
                                        var tThings = TheThingRegistry.GetThingsByProperty("*", Guid.Empty, "DeviceType", "IBaseEngine");
                                        if (tThings != null && tThings.Count > 0)
                                        {
                                            foreach (var tt in tThings)
                                            {
                                                if (tt.EngineName == cmd[4])
                                                {
                                                    tThing = tt;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    string friendlyName = "";
                                    if (tThing != null)
                                        friendlyName = tThing.cdeN == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID ? tThing.FriendlyName : $"{tThing.FriendlyName} on ({tThing.cdeN})";
                                    SetUXProperty(pMsg.Message.GetOriginator(), TheCommonUtils.CGuid(tSP[0]), $"ThingFriendlyName={friendlyName}", cmd[2], tSP.Length > 1 ? $"{tSP[1]};-1" : null);
                                }
                                break;
                            case "CERTPICKER":
                                if (!pMsg.Message.IsFirstNode() || !TheUserManager.HasUserAccess(pMsg.CurrentUserID, 0x80) || cmd.Length < 4) return;
                                {
                                    List<TheComboOptions> tLst = new ()
                                    {
                                        new TheComboOptions { N = "Refresh Picker", V = "CDE_PPP" }
                                    };
                                    try
                                    {
                                        X509Store userCaStore = new (StoreName.My, StoreLocation.LocalMachine);
                                        userCaStore.Open(OpenFlags.ReadOnly);
                                        X509Certificate2Collection certificatesInStore = userCaStore.Certificates; 

                                        foreach (X509Certificate2 cert in certificatesInStore)
                                        {
                                            if (string.IsNullOrEmpty(cert.FriendlyName) && string.IsNullOrEmpty(cert.Subject))
                                                continue;
                                            bool foundOne = false;
                                            foreach (X509Extension extension in cert.Extensions)
                                            {
                                                if (extension.Oid.Value == "2.5.29.37")
                                                {
                                                    X509EnhancedKeyUsageExtension ext = (X509EnhancedKeyUsageExtension)extension;
                                                    OidCollection oids = ext?.EnhancedKeyUsages;
                                                    if (oids?.Count > 0)
                                                    {
                                                        foreach (Oid oid in oids)
                                                        {
                                                            if (oid.Value == "1.3.6.1.5.5.7.3.2")
                                                            {
                                                                foundOne = true;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            if (!foundOne)
                                                continue;
                                            X509Chain chain = new ();
                                            string rootName = "";
                                            X509ChainPolicy chainPolicy = new ()
                                            {
                                                RevocationMode = X509RevocationMode.NoCheck,
                                                RevocationFlag = X509RevocationFlag.EntireChain,
                                            };
                                            chain.ChainPolicy = chainPolicy;
                                            if (chain.Build(cert))
                                            {
                                                X509ChainElement lastChainElement = chain.ChainElements.Count > 0 ? chain.ChainElements[chain.ChainElements.Count - 1] : null;
                                                if (lastChainElement != null)
                                                    rootName = lastChainElement.Certificate.FriendlyName;
                                            }
                                            var t = new TheComboOptions { N = $"{(string.IsNullOrEmpty(cert.FriendlyName) ? cert.Subject : cert.FriendlyName)}{(string.IsNullOrEmpty(rootName) ? "" : $" ({rootName})")}", V = cert.Thumbprint };
                                            tLst.Add(t);
                                        }
                                        userCaStore.Close();
                                    }
                                    catch (Exception ee)
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(7678, new TSM(eEngineName.NMIService, "CertPicker error (certpicker is not supported in user-mode)", eMsgLevel.l1_Error, ee.ToString()));
                                    }
                                    var tList = TheCommonUtils.SerializeObjectToJSONString(tLst);
                                    var tSP = cmd[3].Split(';');
                                    SetUXProperty(pMsg.Message.GetOriginator(), TheCommonUtils.CGuid(tSP[0]), $"LiveOptions={tList}", cmd[2], tSP.Length > 1 ? $"{tSP[1]};-1" : null);
                                }
                                break;
                            case "THINGPICKER": //Syntax of TXT: "NMI_GET_DATA:THINGPICKER:<OwnerMID>:<ControlMID>;<FldOrder>:<includeEngines>:<includeRemotes>:<filter>"
                                if (!pMsg.Message.IsFirstNode() || !TheUserManager.HasUserAccess(pMsg.CurrentUserID, 0xF0) || cmd.Length < 4) return;
                                {
                                    bool bInclEngines = false;
                                    if (cmd.Length > 4)
                                        bInclEngines = TheCommonUtils.CBool(cmd[4]);
                                    bool bInclRemotes = false;
                                    if (cmd.Length > 5)
                                        bInclRemotes = TheCommonUtils.CBool(cmd[5]);
                                    string tPN = null;
                                    string tPV = null;
                                    if (cmd.Length > 6)
                                    {
                                        tPN = cmd[6].Split('=')[0];
                                        if (cmd[6].IndexOf('=') > -1)
                                            tPV = cmd[6].Substring(cmd[6].IndexOf('=') + 1);
                                    }

                                    if (bInclRemotes)
                                    {
                                        TheThingRegistry.RequestGlobalThings();
                                    }

                                    List<TheThing> tThings = TheThingRegistry.GetThingsOfEngine("*", bInclEngines, bInclRemotes).OrderBy(s => s.FriendlyName).ToList();
                                    var tSP = cmd[3].Split(';');
                                    TheFieldInfo tField = GetFieldById(TheCommonUtils.CGuid(tSP[0]));
                                    string ValueProperty = TheCommonUtils.CStr(tField?.PropBagGetValue("ValueProperty"));
                                    List<TheComboOptions> tLst = new ()
                                    {
                                        new TheComboOptions { G = "Refresh", N = "Refresh Picker", V = "CDE_PPP" },
                                        new TheComboOptions { G = "Refresh", N = "Empty Entry", V = "" }
                                    };
                                    if (tThings != null)
                                    {
                                        foreach (var tT in tThings)
                                        {
                                            if (string.IsNullOrEmpty(tT.DeviceType) && !tT.IsInit())
                                                continue;
                                            var tFN = tT.FriendlyName;
                                            if (string.IsNullOrEmpty(tFN))
                                                tFN = $"no Name:{tT.cdeMID}";
                                            if (bInclRemotes)
                                            {
                                                tFN += $" on ({tT.cdeN})";
                                            }
                                            var tVal = $"{tT.cdeMID}";
                                            if (!string.IsNullOrEmpty(ValueProperty))
                                            {
                                                var tpVal = TheCommonUtils.CStr(tT.GetAllProperties().FirstOrDefault(s => s.Name == ValueProperty)?.GetValue());
                                                if (tpVal != null)
                                                    tVal = tpVal;
                                            }
                                            if (tPN == "EngineNames")
                                            {
                                                if (tT.DeviceType != "IBaseEngine")
                                                    continue;
                                                tVal = tT.EngineName;
                                            }
                                            else
                                            {
                                                if (!string.IsNullOrEmpty(tPN))
                                                {
                                                    List<cdeP> tLstP = tT.GetAllProperties().OrderBy(s => s.Name).ToList();
                                                    bool bFound = false;
                                                    foreach (cdeP tP in tLstP)
                                                    {
                                                        if (tP.Name == tPN)
                                                        {
                                                            if (!string.IsNullOrEmpty(tPV) && !$"{tP.GetValue()}".StartsWith(tPV))
                                                                continue;
                                                            bFound = true;
                                                            break;
                                                        }
                                                    }
                                                    if (!bFound)
                                                        continue;
                                                }
                                            }
                                            tLst.Add(new TheComboOptions { S = tT.EngineName, G = tT.DeviceType, N = tFN, V = tVal });
                                        }
                                    }
                                    var tList = TheCommonUtils.SerializeObjectToJSONString(tLst);
                                    SetUXProperty(pMsg.Message.GetOriginator(), TheCommonUtils.CGuid(tSP[0]), $"LiveOptions={tList}", cmd[2], tSP.Length > 1 ? $"{tSP[1]};-1" : null);
                                }
                                return;
                            case "DEVICETYPEPICKER": //Syntax of TXT: "NMI_GET_DATA:DEVICETYPEPICKER:<OwnerMID>:<ControlMID>;<FldOrder>"
                                if (!pMsg.Message.IsFirstNode() || !TheUserManager.HasUserAccess(pMsg.CurrentUserID, 0xF0) || cmd.Length < 4) return;
                                {
                                    bool bInclRemotes = false;
                                    if (cmd.Length > 4)
                                        bInclRemotes = TheCommonUtils.CBool(cmd[4]);
                                    string tPN = null;
                                    string tPV = null;
                                    if (cmd.Length > 5)
                                    {
                                        tPN = cmd[5].Split('=')[0];
                                        if (cmd[5].IndexOf('=') > -1)
                                            tPV = cmd[5].Substring(cmd[5].IndexOf('=') + 1);
                                    }
                                    List<TheThing> tThings = TheThingRegistry.GetThingsOfEngine("*", true, bInclRemotes).OrderBy(s => s.FriendlyName).ToList();
                                    var tSP = cmd[3].Split(';');
                                    List<TheComboOptions> tLst = new ()
                                    {
                                        new TheComboOptions { N = "Refresh Picker", V = "CDE_PPP" },
                                        new TheComboOptions { N = "Empty Entry", V = "" }
                                    };
                                    if (tThings != null)
                                    {
                                        foreach (var tT in tThings)
                                        {
                                            if (string.IsNullOrEmpty(tT.DeviceType) && !tT.IsInit())
                                                continue;
                                            if (tLst.Any(s => s.N == tT.DeviceType))
                                                continue;
                                            var tFN = tT.DeviceType;
                                            var tVal = tT.DeviceType;
                                            if (!string.IsNullOrEmpty(tPN))
                                            {
                                                List<cdeP> tLstP = tT.GetAllProperties().OrderBy(s => s.Name).ToList();
                                                bool bFound = false;
                                                foreach (cdeP tP in tLstP)
                                                {
                                                    if (tP.Name == tPN)
                                                    {
                                                        if (!string.IsNullOrEmpty(tPV) && !$"{tP.GetValue()}".StartsWith(tPV))
                                                            continue;
                                                        bFound = true;
                                                        break;
                                                    }
                                                }
                                                if (!bFound)
                                                    continue;
                                            }
                                            tLst.Add(new TheComboOptions { S = tT.EngineName, N = tFN, V = tVal });
                                        }
                                    }
                                    var tList = TheCommonUtils.SerializeObjectToJSONString(tLst);
                                    SetUXProperty(pMsg.Message.GetOriginator(), TheCommonUtils.CGuid(tSP[0]), $"LiveOptions={tList}", cmd[2], tSP.Length > 1 ? $"{tSP[1]};-1" : null);
                                }
                                return;
                            case "PROPERTYPICKER":
                                if (!pMsg.Message.IsFirstNode() || !TheUserManager.HasUserAccess(pMsg.CurrentUserID, 0xF0) || cmd.Length < 4) return;
                                Guid ttGuid = TheCommonUtils.CGuid(cmd[4]);
                                if (ttGuid != Guid.Empty)
                                {
                                    var tSP = cmd[3].Split(';');
                                    TheFieldInfo tField = GetFieldById(TheCommonUtils.CGuid(tSP[0]));
                                    bool AddSystemProps = TheCommonUtils.CBool(tField?.PropBagGetValue("SystemProperties"));
                                    List<TheComboOptions> tLst = new ()
                                    {
                                        new TheComboOptions { N = "Refresh Picker", V = "CDE_PPP" }
                                    };
                                    TheThing tThing = TheThingRegistry.GetThingByMID("*", ttGuid, true);
                                    if (tThing != null)
                                    {
                                        List<cdeP> tLstP = tThing.GetAllProperties().OrderBy(s => s.Name).ToList();
                                        tLst.AddRange(from cdeP tP in tLstP
                                                      select new TheComboOptions { N = tP.Name, V = tP.Name });
                                        if (AddSystemProps)
                                        {
                                            tLst.Add(new TheComboOptions { N = "cde TimeStamp", V = "cdeCTIM" });
                                            tLst.Add(new TheComboOptions { N = "cde MID", V = "cdeMID" });
                                            tLst.Add(new TheComboOptions { N = "cde NodeID", V = "cdeN" });
                                            tLst.Add(new TheComboOptions { N = "cde MetaTag", V = "cdeM" });
                                            tLst.Add(new TheComboOptions { N = "cde Owner", V = "cdeO" });
                                        }
                                    }
                                    else
                                        tLst.Add(new TheComboOptions { N = "Thing not Found - select a valid Thing first", V = "CDE_NOP" });
                                    var tLO = TheCommonUtils.SerializeObjectToJSONString(tLst);

                                    SetUXProperty(pMsg.Message.GetOriginator(), TheCommonUtils.CGuid(tSP[0]), $"LiveOptions={tLO}", cmd[2], tSP.Length > 1 ? $"{tSP[1]};-1" : null);
                                }
                                return;
                        }
                    }
                    if (cmd.Length < 4) return;
                    string modelId = "";
                    if (cmd.Length > 4)
                        modelId = cmd[4];
                    bool IsInitialLoad = false;
                    if (cmd.Length > 5)
                        IsInitialLoad = TheCommonUtils.CBool(cmd[5]);
                    bool ForceLoad = false;
                    if (cmd.Length > 6)
                        ForceLoad = TheCommonUtils.CBool(cmd[6]);
                    SendNMIData(pMsg.Message, tClientInfo, cmd[1], cmd[2], cmd[3], modelId, ForceLoad, IsInitialLoad);
                    break;
                case "NMI_CHECK4_UPDATE":
                    TheBaseAssets.MyApplication?.MyISMRoot?.RequestUpdates();
                    RefreshDashboard(pMsg, "%MAINDASHBOARD%", false);
                    break;
                case "NMI_GET_SCENE":
                    if (!string.IsNullOrEmpty(pMsg.Message.PLS) && pMsg.CurrentUserID != Guid.Empty)
                    {
                        string tTargetDir = "";
                        if (TheCommonUtils.CGuid(pMsg.Message.PLS) == TheCommonUtils.CGuid("{40AB04FA-BF1D-4EEE-CDE9-DF1E74CC69A7}"))
                        {
                            tTargetDir = "scenes\\MyHome.cdescene";
                        }
                        else
                        {
                            TheThing tSceneThing = TheThingRegistry.GetThingByMID(eEngineName.NMIService, TheCommonUtils.CGuid(pMsg.Message.PLS));
                            if (tSceneThing != null)
                            {
                                if (tSceneThing.DeviceType != eKnownDeviceTypes.TheNMIScene)
                                    return;
                                if (!(TheThing.GetSafePropertyBool(tSceneThing, "IsPublic") || (pMsg.CurrentUserID != Guid.Empty && tSceneThing.UID == pMsg.CurrentUserID)))
                                {
                                    TSM ErrTsm = LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", "###You dont have access to this Scene###"));
                                    TheCommCore.PublishToOriginator(pMsg.Message, ErrTsm);
                                    return;
                                }
                                if (TheThing.GetSafePropertyBool(tSceneThing, "IsPublic"))
                                    tTargetDir = tSceneThing.UID == Guid.Empty ? $"scenes\\{tSceneThing.cdeMID}.cdescene" : $"scenes\\{tSceneThing.UID}\\{tSceneThing.cdeMID}.cdescene";
                                else
                                    tTargetDir = $"scenes\\{pMsg.CurrentUserID}\\{tSceneThing.cdeMID}.cdescene";
                            }
                            else //If Home Screen is not a scene but a Thing Dashboard or Screen
                            {
                                var tDash = GetDashboardById(TheCommonUtils.CGuid(pMsg.Message.PLS));
                                if (tDash != null)
                                {
                                    TheCommCore.PublishToOriginator(pMsg.Message, LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_TTS", pMsg.Message.PLS)));
                                    return;
                                }
                                TheFormInfo tForm = GetFormById(TheCommonUtils.CGuid(pMsg.Message.PLS));
                                if (tForm != null)
                                {
                                    TheCommCore.PublishToOriginator(pMsg.Message, LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_TTS", pMsg.Message.PLS)));
                                    return;
                                }
                                var tDashPanel = GetDashPanelById(TheCommonUtils.CGuid(pMsg.Message.PLS));
                                if (tDashPanel != null)
                                {
                                    TheCommCore.PublishToOriginator(pMsg.Message, LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_TTS", pMsg.Message.PLS)));
                                    return;
                                }
                                TSM ErrTsm = LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", "###Requested Scene, Dashboard or Form not found###"));
                                TheCommCore.PublishToOriginator(pMsg.Message, ErrTsm);
                                return;
                            }
                        }
                        string tPlS = TheCommonUtils.LoadStringFromDisk(tTargetDir, null);
                        if (!string.IsNullOrEmpty(tPlS))
                        {
                            TSM tSceneTsm = new (eEngineName.NMIService, "NMI_SET_SCENE", tPlS);
                            TheCommCore.PublishToOriginator(pMsg.Message, tSceneTsm);
                        }
                    }
                    break;
                case "NMI_SAVE_USERDATA":
                    if (cmd.Length > 1 && pMsg.CurrentUserID != Guid.Empty)
                    {
                        string tTargetDir = $"{pMsg.CurrentUserID}\\{cmd[1]}.cdeUserData";
                        if (string.IsNullOrEmpty(pMsg.Message.PLS))
                            TheCommonUtils.DeleteFromDisk(tTargetDir, "");
                        else
                            TheCommonUtils.SaveBlobToDisk(pMsg.Message, new[] { "", tTargetDir }, null);
                    }
                    break;
                case "NMI_GET_USERDATA":
                    if (cmd.Length > 1 && pMsg.CurrentUserID != Guid.Empty)
                    {
                        string tTargetDir = $"{pMsg.CurrentUserID}\\{cmd[1]}.cdeUserData";
                        string tPlS = TheCommonUtils.LoadStringFromDisk(tTargetDir, null);
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_USERDATA", tPlS));
                    }
                    break;
                case "NMI_SAVE_SCREEN":
                    if (cmd.Length > 1 && pMsg.CurrentUserID != Guid.Empty)
                    {
                        TheFOR tScene = TheCommonUtils.DeserializeJSONStringToObject<TheFOR>(pMsg.Message.PLS);
                        if (tScene == null) return;
                        string tTargetDir = $"{pMsg.CurrentUserID}\\{TheCommonUtils.CGuid(tScene.ID)}.cdeFOR";
                        TheCommonUtils.SaveBlobToDisk(pMsg.Message, new[] { "", tTargetDir }, null);
                    }
                    break;
                case "NMI_CLEAR_SCREEN":
                    if (cmd.Length > 1 && pMsg.CurrentUserID != Guid.Empty)
                    {
                        string tTargetDir = $"{pMsg.CurrentUserID}\\{TheCommonUtils.CGuid(cmd[1])}.cdeFOR";
                        TheCommonUtils.DeleteFromDisk(tTargetDir, "");
                    }
                    break;
                case "NMI_SAVE_HOMESCENE":
                    if (pMsg.CurrentUserID != Guid.Empty)
                    {
                        if (pMsg.Message.PLS == "RESET")
                        {
                            TheThing tScTh = TheThingRegistry.GetThingByFunc(eEngineName.NMIService, s => s.UID == pMsg.CurrentUserID && TheThing.GetSafePropertyBool(s, "IsHomeScene"));
                            if (tScTh != null)
                            {
                                string tTDir = TheCommonUtils.cdeFixupFileName($"Scenes\\{pMsg.CurrentUserID}\\{tScTh.cdeMID}.cdescene");
                                if (System.IO.File.Exists(tTDir))
                                    System.IO.File.Delete(tTDir);
                            }
                            return;
                        }
                        TheNMIScene tScene = TheCommonUtils.DeserializeJSONStringToObject<TheNMIScene>(pMsg.Message.PLS);
                        if (tScene == null) return;
                        TheThing tSceneThing = TheThingRegistry.GetThingByFunc(eEngineName.NMIService, s => s.UID == pMsg.CurrentUserID && TheThing.GetSafePropertyBool(s, "IsHomeScene"));
                        bool WasMerged = false;
                        if (tSceneThing == null)
                        {
                            tSceneThing = new TheThing
                            {
                                EngineName = eEngineName.NMIService,
                                DeviceType = eKnownDeviceTypes.TheNMIScene,
                                FriendlyName = $"Home Scene: {tScene.FriendlyName}",
                                UID = pMsg.CurrentUserID
                            };
                            tSceneThing.SetProperty("IsHomeScene", true);
                        }
                        else
                        {
                            if (tScene.Screens[0].FldOrder == -1)
                            {
                                string tt = string.Format("scenes\\{0}\\{1}.cdescene", pMsg.CurrentUserID, tSceneThing.cdeMID);
                                string tPls = TheCommonUtils.LoadStringFromDisk(tt, null);
                                if (!string.IsNullOrEmpty(tPls))
                                {
                                    TheNMIScene tScene2 = TheCommonUtils.DeserializeJSONStringToObject<TheNMIScene>(tPls);
                                    if (tScene2 != null && tScene2.Screens?.Count > 0 && !tScene2.Screens.Any(s => s.ID == tScene.Screens[0].ID))
                                    {
                                        tScene.Screens[0].FldOrder = tScene2.Screens.OrderByDescending(s => s.FldOrder).First().FldOrder + 10;
                                        tScene2.Screens.Add(tScene.Screens[0]);
                                        pMsg.Message.PLS = TheCommonUtils.SerializeObjectToJSONString(tScene2);
                                        WasMerged = true;
                                    }
                                }
                            }
                        }
                        if (!WasMerged && tScene.Screens[0].FldOrder == -1)
                        {
                            tScene.Screens[0].FldOrder = 10;
                            pMsg.Message.PLS = TheCommonUtils.SerializeObjectToJSONString(tScene);
                        }
                        TheThingRegistry.RegisterThing(tSceneThing);
                        string tTargetDir = $"{pMsg.CurrentUserID}\\{tSceneThing.cdeMID}.cdescene";
                        TheCommonUtils.SaveBlobToDisk(pMsg.Message, new[] { "SCENE", tTargetDir }, null);
                    }
                    break;
                case "NMI_SAVE_SCENE":
                    if (cmd.Length > 1 && pMsg.CurrentUserID != Guid.Empty)
                    {
                        TheNMIScene tScene = TheCommonUtils.DeserializeJSONStringToObject<TheNMIScene>(pMsg.Message.PLS);
                        if (tScene == null) return;
                        TheThing tSceneThing = TheThingRegistry.GetThingByProperty(eEngineName.NMIService, pMsg.CurrentUserID, "FriendlyName", cmd[1]);
                        if (tSceneThing == null)
                        {
                            tSceneThing = new TheThing
                            {
                                EngineName = eEngineName.NMIService,
                                DeviceType = eKnownDeviceTypes.TheNMIScene,
                                FriendlyName = cmd[1]
                            };
                        }
                        else
                        {
                            if (tSceneThing.UID != pMsg.CurrentUserID)
                            {
                                TSM failureTsm = LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", "###Choose different name. This name has been used by a different user###"));
                                TheCommCore.PublishToOriginator(pMsg.Message, failureTsm);
                                return;
                            }
                        }
                        tSceneThing.UID = tScene.IsPublic ? Guid.Empty : pMsg.CurrentUserID;
                        tSceneThing.SetProperty("IsPublic", tScene.IsPublic);
                        TheThingRegistry.RegisterThing(tSceneThing);
                        string tTargetDir = "";
                        tTargetDir = tScene.IsPublic ? string.Format("{0}.cdescene", tSceneThing.cdeMID) : string.Format("{0}\\{1}.cdescene", pMsg.CurrentUserID, tSceneThing.cdeMID);

                        TheCommonUtils.SaveBlobToDisk(pMsg.Message, new[] { "SCENE", tTargetDir }, null);
                        RefreshDashboard(pMsg, "7f4b6e0f-70c6-4c6a-b820-4d57b2c35e33", true);
                    }
                    break;
                case "NMI_GET_SCREEN":
                    {
                        var tThing = TheThingRegistry.GetThingByMID(TheCommonUtils.CGuid(pMsg.Message.PLS));
                        if (tThing == null)
                        {
                            var tForm = GetFormById(TheCommonUtils.CGuid(pMsg.Message.PLS));
                            if (tForm != null)
                            {
                                tThing = TheThingRegistry.GetThingByMID(tForm.cdeO);
                                if (tThing == null)
                                {
                                    var tDash = TheCommonUtils.CGuid(ThePropertyBag.PropBagGetValue(tForm.PropertyBag, "InDashboard"));
                                    if (tDash != Guid.Empty)
                                    {
                                        SendScreenMeta(cmd[0], tDash, tClientInfo, pMsg, TheCommonUtils.CGuid(pMsg.Message.PLS));
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                var tDashPanel = TheNMIEngine.GetDashPanelById(TheCommonUtils.CGuid(pMsg.Message.PLS));
                                if (tDashPanel != null)
                                {
                                    if (!TheUserManager.HasUserAccess(tClientInfo.UserID, tDashPanel.cdeA))
                                        return;
                                    var tP = tDashPanel.ControlClass.Split(':');
                                    if (tP.Length > 1)
                                    {
                                        IBaseEngine tBase = TheThingRegistry.GetBaseEngine(tP[0]);
                                        byte[] astream = tBase.GetPluginResource(tP[1] + ".js");
                                        if (astream != null)
                                        {
                                            TSM tTsm = new (eEngineName.NMIService, $"NMI_CUSTOM_SCRIPT:{(cmd.Length > 1 ? cmd[1] : pMsg.Message.PLS)}")
                                            {
                                                PLS = TheCommonUtils.GenerateFinalStr(TheCommonUtils.CArray2UTF8String(astream)).Replace("%=CONTENT%", cmd.Length > 1 ? cmd[1] : pMsg.Message.PLS)
                                            };
                                            TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                            return;
                                        }
                                    }
                                }
                                else
                                {
                                    var tDashboard = GetDashboardById(TheCommonUtils.CGuid(pMsg.Message.PLS));
                                    if (tDashboard != null)
                                        SendScreenMeta(cmd[0], tDashboard.cdeMID, tClientInfo, pMsg, null);
                                }
                            }
                        }
                        if (tThing != null)
                        {
                            var tEng = TheThingRegistry.GetBaseEngine(tThing);
                            if (tEng != null)
                                SendScreenMeta(cmd[0], tEng.GetDashboardGuid(), tClientInfo, pMsg, TheCommonUtils.CGuid(pMsg.Message.PLS));
                        }
                    }
                    break;
                case "NMI_GET_SCREENMETAF":
                case "NMI_GET_SCREENMETA":
                    SendScreenMeta(cmd[0], TheCommonUtils.CGuid(pMsg.Message.PLS), tClientInfo, pMsg, null);
                    break;
                case "NMI_SYNCBLOCKS":
                    try
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7678, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.NMIService, $"NMI_SYNCBLOCKS: Received from {pMsg.Message.GetOriginator()}", eMsgLevel.l3_ImportantMessage)); 
                        List<ThePageBlocks> tList = TheCommonUtils.DeserializeJSONStringToObject<List<ThePageBlocks>>(pMsg.Message.PLS);
                        UpdatePageBlocks(tList);
                    }
                    catch (Exception nsbe)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7678, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.NMIService, $"NMI_SYNCBLOCKS: failed to sync", eMsgLevel.l2_Warning, nsbe.ToString())); 
                    }
                    break;
                default:
                    FireEvent(pMsg.Message.TXT, MyBaseThing, pMsg, true);
                    break;
            }
        }

        private void SendScreenMeta(string cmd, Guid pDashboardID, TheClientInfo tClientInfo, TheProcessMessage pMsg, Guid? pSubScreenID)
        {
            TSM tScreenTsm;
            try
            {
                TheFormsGenerator.RefreshLiveScreens(pMsg.Message, tClientInfo);
                TheScreenInfo tInfo = AssembleScreenMeta(pDashboardID, tClientInfo, cmd == "NMI_GET_SCREENMETAF", pMsg);
                if (tInfo == null)
                {
                    return;
                }
                else
                {
                    if (pSubScreenID != null)
                    {
                        foreach (var t in tInfo.MyDashPanels)
                        {
                            if (t.cdeMID == TheCommonUtils.CGuid(pSubScreenID))
                            {
                                t.PropertyBag = new ThePropertyBag { "ForceLoad=true" };
                                break;
                            }
                        }
                    }
                    tScreenTsm = new TSM(MyBaseEngine.GetEngineName(), "NMI_SCREENMETA", TheCommonUtils.GenerateFinalStr(TheCommonUtils.SerializeObjectToJSONString(tInfo)));
                }
            }
            catch (Exception e)
            {
                tScreenTsm = LocNMI(tClientInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", "###NMI-GET-SCREENMETA Failed:###" + e));
            }
            if (tScreenTsm != null)
                TheCommCore.PublishToOriginator(pMsg.Message, tScreenTsm);
        }

        internal static bool SendNMIData(TSM pMsg, TheClientInfo pClientInfo, string pTargetElement, string pDataType, string pStrDataID, string pModelID, bool ForceReload, bool IsInitialLoad)
        {
            TSM tTsm = new (eEngineName.NMIService, "NMI_SET_DATA:");
            Guid pDataID = TheCommonUtils.CGuid(pStrDataID);
            string tTableName3 = pDataID.ToString().ToLower();
            tTsm.TXT += tTableName3;
            if (!string.IsNullOrEmpty(UserManID) && UserManID == tTableName3)
            {
                if (!pMsg.IsFirstNode() || !TheCDEngines.MyNMIService.GetBaseEngine().GetEngineState().IsService) return false;
                if (TheBaseAssets.MyServiceHostInfo.IsCloudService || !TheUserManager.HasUserAccess(pClientInfo.UserID, 128))
                {
                    tTsm.PLS = "Access to User-List Denied!";
                    tTsm.TXT += ":ERR";
                    return false;
                }
                tTsm.TXT += ":" + pModelID + ":" + UserManID;
                tTsm.PLS = TheUserManager.SendUserList(pClientInfo.UserID);//Sends The user List to only a first node connection browser on premise - NOT via the cloud
                TheFormInfo tTable = GetFormById(pDataID);
                tTable?.FireEvent(eUXEvents.OnBeforeLoad, new TheProcessMessage() { ClientInfo = pClientInfo, CurrentUserID = pClientInfo.UserID }, false);
                if (ForceReload && tTable != null)
                {
                    TheFormInfo tToSend = tTable.Clone(pClientInfo.WebPlatform);
                    CheckAddButtonPermission(pClientInfo, tToSend);
                    var tso = TheFormsGenerator.GetScreenOptions(tTable.cdeMID, pClientInfo, ForceReload ? tTable : null);
                    tToSend.FormFields = TheFormsGenerator.GetPermittedFields(tTable.cdeMID, pClientInfo, tso, true);
                    if (tso != null && tso.TileWidth > 0)
                        tToSend.TileWidth = tso.TileWidth;
                    tTsm.PLS += ":-MODELUPDATE-:" + TheCommonUtils.GenerateFinalStr(TheCommonUtils.SerializeObjectToJSONString(tToSend.GetLocalizedForm(pClientInfo.LCID)));
                }
                TheCommCore.PublishToOriginator(pMsg, tTsm);
                return true;
            }
            switch (tTableName3)
            {
                case "7f4b6e0f-70c6-4c6a-b820-4d57b2c35e33":    //My Scenes
                    if (pClientInfo.UserID == Guid.Empty || !pMsg.IsFirstNode() || !TheCDEngines.MyNMIService.GetBaseEngine().GetEngineState().IsService) return false;
                    List<TheThing> tList = TheThingRegistry.GetThingsByProperty(eEngineName.NMIService, pClientInfo.UserID, "DeviceType", eKnownDeviceTypes.TheNMIScene);
                    if (tList != null)
                    {
                        TheScreenInfo tScreenInfo = new ()
                        {
                            MyDashboard = new TheDashboardInfo() { DashboardTitle = "My Scenes", cdeMID = TheCommonUtils.CGuid(tTableName3), PropertyBag = new ThePropertyBag { "Category=NMI" } },
                            MyDashPanels = new List<TheDashPanelInfo>()
                        };

                        TheDashPanelInfo tInfo;
                        if (tList.Count > 0)
                        {
                            foreach (TheThing tApp in tList)
                            {
                                tInfo = new TheDashPanelInfo(TheCDEngines.MyNMIService.GetBaseThing())
                                {
                                    cdeMID = tApp.cdeMID,
                                    PanelTitle = tApp.FriendlyName,
                                    ControlClass = $"jsAction:GS:{tApp.cdeMID}"
                                };
                                tScreenInfo.MyDashPanels.Add(tInfo);
                            }
                            tInfo = new TheDashPanelInfo(TheCDEngines.MyNMIService.GetBaseThing())
                            {
                                cdeMID = new Guid("{C7C9B38D-3DD3-43AA-A7AB-6FA8E4E5C719}"),
                                PanelTitle = "Clear Scene and go Home",
                                ControlClass = "jsAction:cdeNMI.TheMainPage.ClearAndGoHome();"
                            };
                            tScreenInfo.MyDashPanels.Add(tInfo);
                        }
                        else
                        {
                            tInfo = new TheDashPanelInfo(TheCDEngines.MyNMIService.GetBaseThing())
                            {
                                cdeMID = new Guid("{BADDA359-2518-42A7-91CE-852E6A524A1C}"),
                                PanelTitle = "How to create New Scene",
                                ControlClass = "jsAction:POP:Move the logo to the right and click on the save icon."
                            };
                            tScreenInfo.MyDashPanels.Add(tInfo);
                        }
                        tTsm.PLS = TheCommonUtils.SerializeObjectToJSONString(tScreenInfo);
                        tTsm.TXT = "NMI_SCREENMETA:";
                        if (pTargetElement.Equals("AUTO"))
                            tTsm.TXT += "7F4B6E0F-70C6-4C6A-B820-4D57B2C35E33";
                        else
                            tTsm.TXT += pTargetElement;
                    }
                    break;
                default:
                    if (pDataType == "auto")
                    {
                        var t = GetFormById(pDataID);
                        if (t != null)
                        {
                            pDataType = (t.DefaultView == eDefaultView.Form ? eScreenClass.CMyForm : eScreenClass.CMyTable);
                        }
                        else
                        {
                            var t1 = TheNMIEngine.GetDashboardById(pDataID);
                            if (t1 != null)
                                pDataType = eScreenClass.CMyDashboard;
                            else
                                return false;
                        }
                    }
                    switch (pDataType)
                    {
                        case "Data":
                        case eScreenClass.CMyForm:
                        case eScreenClass.CMyChart:
                        case eScreenClass.CMyTable:
                        case eScreenClass.CMyData:
                            try
                            {
                                TheFormInfo tTable = GetFormById(pDataID);
                                if (tTable != null)
                                {
                                    if (!eEngineName.ContentService.Equals(tTable.OwnerEngine) && !eEngineName.NMIService.Equals(tTable.OwnerEngine))
                                    {
                                        IBaseEngine tBase = TheThingRegistry.GetBaseEngine(tTable.OwnerEngine);
                                        if (tBase != null)
                                        {
                                            TSM tForward = TSM.Clone(pMsg, true);
                                            tForward.ENG = tTable.OwnerEngine;
                                            tBase.ProcessMessage(tForward);
                                            return true;
                                        }
                                        return false; //No processing here
                                    }
                                    else
                                    {
                                        if (tTable.GetFromFirstNodeOnly && !pMsg.IsFirstNode()) return false;
                                        if (tTable.GetFromServiceOnly && !TheCDEngines.MyNMIService.GetBaseEngine().GetEngineState().IsService) return false;
                                        object MyStorageMirror = TheCDEngines.GetStorageMirror(tTable.defDataSource);
                                        string tPaging = ":0:0";
                                        string tWildContent = "";
                                        if (MyStorageMirror != null && !(tTable.IsNotAutoLoading && IsInitialLoad))
                                        {
                                            Type magicType = MyStorageMirror.GetType();
#if CDE_STANDARD //Metro Style Reflection
                                            MethodInfo magicMethod = magicType.GetTypeInfo().GetDeclaredMethod("SerializeToJSON");
#else
                                            MethodInfo magicMethod = magicType.GetMethod("SerializeToJSON", BindingFlags.NonPublic | BindingFlags.Instance);
#endif
                                            if (magicMethod != null)
                                            {
                                                TheJSONLoaderDefinition tJSON = new ()
                                                {
                                                    ORG = pMsg.GetOriginator(),
                                                    TargetElement = pTargetElement,
                                                    TableName = tTable.cdeMID,
                                                    SID = pMsg.SID
                                                };
                                                string[] t = TheCommonUtils.cdeSplit(tTable.defDataSource, ";:;", false, false);

                                                tJSON.TopRecords = 0;
                                                if (t.Length > 1) tJSON.TopRecords = TheCommonUtils.CInt(t[1]);
                                                tJSON.SQLOrder = "";
                                                if (t.Length > 2) tJSON.SQLOrder = t[2];
                                                tJSON.SQLFilter = "";
                                                if (t.Length > 3)
                                                    tJSON.SQLFilter = t[3].Replace("%SESS:CID%", pClientInfo.UserID.ToString());
                                                if (string.IsNullOrEmpty(pMsg.PLS) && t.Length > 4)
                                                    tJSON.PageNumber = TheCommonUtils.CInt(t[4]);
                                                else
                                                {
                                                    if (!string.IsNullOrEmpty(pMsg.PLS))
                                                    {
                                                        string[] tPLSParts = TheCommonUtils.cdeSplit(pMsg.PLS, ":;:", false, false);
                                                        tJSON.PageNumber = TheCommonUtils.CInt(tPLSParts[0]);
                                                        if (tPLSParts.Length > 1)
                                                        {
                                                            if (tJSON.SQLFilter.Length > 0) tJSON.SQLFilter += ";";
                                                            tWildContent = tPLSParts[1];
                                                            tJSON.SQLFilter += $"WildContent={tWildContent}";
                                                        }
                                                    }
                                                }
                                                tJSON.IsEmptyRecord = tTable.IsAlwaysEmpty;
                                                tJSON.defDataSource = tTable.defDataSource;
                                                tJSON.RequestingUserID = pClientInfo.UserID;
                                                tJSON.ForceReload = ForceReload;
                                                tJSON.ClientInfo = pClientInfo;
                                                tJSON.ModelID = pModelID;
                                                tTable.FireEvent(eUXEvents.OnBeforeLoad, new TheProcessMessage() { ClientInfo = pClientInfo, CurrentUserID = pClientInfo.UserID, Message = pMsg }, false);
                                                var tso = TheFormsGenerator.GetScreenOptions(tTable.cdeMID, pClientInfo, ForceReload ? tTable : null);
                                                tJSON.FieldInfo = TheFormsGenerator.GetPermittedFields(tTable.cdeMID, pClientInfo, tso, true);
                                                if (pDataType == eScreenClass.CMyData)
                                                {
                                                    MethodInfo csvmagicMethod = magicType.GetTypeInfo().GetDeclaredMethod("SerializeToCSV");
                                                    tTsm.PLS = csvmagicMethod.Invoke(MyStorageMirror, new[] { (object)tJSON }).ToString();
                                                    if (tTsm.PLS == "WAITING")
                                                        return false;
                                                    tTsm.ENG = eEngineName.ContentService;
                                                    tTsm.TXT = $"CDE_FILE:{tTable.FormTitle}.csv:text/csv";
                                                    break;
                                                }
                                                tTsm.PLS = magicMethod.Invoke(MyStorageMirror, new[] { (object)tJSON }).ToString();
                                                if (tTsm == null || tTsm.PLS.Equals("WAITING") || tTsm.PLS.Equals("ASYNC"))
                                                {
                                                    if (tTsm == null || tTsm.PLS.Equals("ASYNC"))
                                                        return false;
                                                    ForceReload = true;
                                                    tTsm.PLS = "[]";
                                                }
                                                if (tJSON.TopRecords > 0)
                                                {
                                                    tPaging = string.Format(":{0}:{1}", tJSON.TopRecords, tJSON.PageNumber);
                                                }
                                                if (ForceReload)
                                                {
                                                    TheFormInfo tToSend = tTable.Clone(pClientInfo.WebPlatform);
                                                    CheckAddButtonPermission(pClientInfo, tToSend);
                                                    if (tso != null && tso.TileWidth > 0)
                                                        tToSend.TileWidth = tso.TileWidth;
                                                    tToSend.FormFields = tJSON.FieldInfo;
                                                    tTsm.PLS += ":-MODELUPDATE-:" + TheCommonUtils.GenerateFinalStr(TheCommonUtils.SerializeObjectToJSONString(tToSend.GetLocalizedForm(pClientInfo.LCID)));
                                                }
                                                if (tTable.IsEventRegistered(eUXEvents.OnLoad))
                                                {
                                                    var tEV = TSM.Clone(tTsm, false);
                                                    tEV.ORG = pMsg.GetOriginator().ToString();
                                                    tTable.FireEvent(eUXEvents.OnLoad, new TheProcessMessage() { ClientInfo = pClientInfo, CurrentUserID = pClientInfo.UserID, Message = tEV }, false);
                                                }
                                            }
                                        }
                                        else
                                            tTsm.PLS = "[]";
                                        if (tTsm.PLS == "[]" && pDataType.Equals("CMyForm"))
                                            tTsm.PLS = "[" + TheCommonUtils.CreateListFromDataSource(tTable.defDataSource) + "]";
                                        tTsm.TXT += $":{pModelID}:{pTargetElement}{tPaging}{(ForceReload ? ":true" : ":false")}:{tWildContent}";
                                    }
                                }
                                else
                                    return false;
                                if (string.IsNullOrEmpty(tTsm.PLS))
                                {
                                    tTsm.PLS = "Something went wrong for Data Definition: " + pDataID;
                                    tTsm.TXT += ":ERR";
                                }
                            }
                            catch (Exception eTable)
                            {
                                tTsm.PLS = "Error During Storage Retrieval: " + eTable;
                                tTsm.TXT += ":ERR";
                            }
                            break;
                        case eScreenClass.CMyLiveScreen:
                            if (TheBaseAssets.MyServiceHostInfo.IsCloudService) return false;
                            try
                            {
                                TheScreenInfo tI = TheFormsGenerator.GenerateLiveScreen(pDataID, pClientInfo);
                                if (tI != null)
                                {
                                    tI.ForceReload = ForceReload;
                                    tTsm.PLS = TheCommonUtils.SerializeObjectToJSONString(tI);
                                    tTsm.TXT = "NMI_LIVESCREENMETA:" + pTargetElement;

                                }
                                else
                                    return false;
                            }
                            catch (Exception e)
                            {
                                tTsm.PLS = "CMyLiveScreen Data Retrieval Failed:" + e;
                                tTsm.TXT = "NMI_CUSTOM_HTML:" + pTargetElement;
                            }
                            break;
                        case eScreenClass.CMyDashboard:
                            try
                            {
                                TheScreenInfo tI = AssembleScreenMeta(pDataID, pClientInfo, false, new TheProcessMessage() { Message = pMsg, ClientInfo = pClientInfo, CurrentUserID = pClientInfo.UserID });
                                if (tI != null)
                                {
                                    tI.ForceReload = ForceReload;
                                    tTsm.PLS = TheCommonUtils.GenerateFinalStr(TheCommonUtils.SerializeObjectToJSONString(tI));
                                    tTsm.TXT = "NMI_SCREENMETA:" + pTargetElement;
                                    var tForm = TheNMIEngine.GetDashboardById(pDataID);
                                    if (tForm?.IsEventRegistered(eUXEvents.OnLoad) == true)
                                    {
                                        var tEV = TSM.Clone(tTsm, false);
                                        tEV.ORG = pMsg.GetOriginator().ToString();
                                        tForm.FireEvent(eUXEvents.OnLoad, new TheProcessMessage() { ClientInfo = pClientInfo, CurrentUserID = pClientInfo.UserID, Message = tEV }, false);
                                    }
                                }
                                else
                                    return false;
                            }
                            catch (Exception e)
                            {
                                tTsm.PLS = "Exception: CMyDashboard Data Retrieval Failed. Click Refresh to try again";
                                tTsm.TXT = "NMI_CUSTOM_HTML:" + pTargetElement;
                                TheBaseAssets.MySYSLOG.WriteToLog(7678, new TSM(eEngineName.NMIService, "Dashboard Retreival failed", eMsgLevel.l1_Error, e.ToString()));
                            }
                            break;
                        default:
                            if (pDataType.Equals(eEngineName.NMIService))
                            {
                                byte[] tRetBuffer = TheCDEngines.MyNMIService.GetBaseEngine().GetPluginResource(pStrDataID + ".js");
                                if (tRetBuffer != null)
                                {
                                    tTsm.PLS = TheCommonUtils.GenerateFinalStr(TheCommonUtils.CArray2UTF8String(tRetBuffer)).Replace("%=CONTENT%", pTargetElement);
                                    tTsm.TXT = "NMI_CUSTOM_SCRIPT:" + pTargetElement;
                                }
                                else
                                    return false;
                            }
                            else
                            {
                                IBaseEngine tBase = TheThingRegistry.GetBaseEngine(pDataType);
                                if (tBase != null)
                                {
                                    TSM tForward = TSM.Clone(pMsg, true);
                                    tForward.ENG = pDataType;
                                    tBase.ProcessMessage(tForward);
                                    return true;
                                }
                                else
                                    return false;
                            }
                            break;
                    }
                    break;
            }
            TheCommCore.PublishToOriginator(pMsg, tTsm);
            return true;
        }

        internal static void CheckAddButtonPermission(TheClientInfo pClientInfo, TheFormInfo tToSend)
        {
            if (!(TheUserManager.HasUserAccess(pClientInfo.UserID, tToSend.AddACL) || tToSend.AddFlags == 0 ||
                ((tToSend.AddFlags & 128) != 0 && pClientInfo.IsFirstNode) ||
                ((tToSend.AddFlags & 2) != 0 && pClientInfo.IsOnCloud)) ||
                !TheFormsGenerator.CheckAddPermission(tToSend.PlatBag, pClientInfo))
                tToSend.AddButtonText = "";
        }

        private static bool IsDashboardEmpty(TheDashboardInfo tDash, TheClientInfo pClientInfo)
        {
            if (tDash?.colPanels == null || tDash.colPanels.Count == 0)   //Fix crash if plugin has only dashboard but no content
                return false;
            List<TheDashPanelInfo> tMyDashPanels = new ();
            foreach (string tDID in tDash.colPanels.ToList())
            {
                TheDashPanelInfo tD = TheFormsGenerator.ParsePanelInfo(tDID);
                if (tD == null || tD.Category?.EndsWith("-HIDE") == true || tD.PanelTitle?.EndsWith("-HIDE") == true) continue;
                tMyDashPanels.Add(tD);
            }
            return !tMyDashPanels.Any((s) => TheUserManager.HasUserAccess(pClientInfo.UserID, s.cdeA) &&
                                        (s.Flags == 0 ||
                                         (((s.Flags & 2) != 0 || !pClientInfo.IsOnCloud || pClientInfo.IsUserTrusted) &&
                                          ((s.Flags & 4) == 0 || !pClientInfo.IsMobile) &&
                                          ((s.Flags & 128) == 0 || pClientInfo.IsFirstNode || pClientInfo.IsUserTrusted)))
                                         );
        }

        internal static TheScreenInfo AssembleScreenMeta(Guid ScreenID, TheClientInfo pClientInfo, bool pForceLoad, TheProcessMessage pMsg)
        {
            if (TheCDEngines.MyNMIService == null || pClientInfo == null)
                return null;

            if (ScreenID == eNMIPortalDashboard && TheCDEngines.MyNMIService.MyNMIModel.MainDashboardScreen != eNMIPortalDashboard)
                ScreenID = TheCDEngines.MyNMIService.MyNMIModel.MainDashboardScreen;

            var tInfo = TheFormsGenerator.GenerateScreen(ScreenID, pClientInfo);
            if (tInfo == null)
                return null;
            tInfo.ForceReload = pForceLoad;
            tInfo.NodeName = !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.NodeName) ? TheBaseAssets.MyServiceHostInfo.NodeName : TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false);

            if (ScreenID == TheCDEngines.MyNMIService.MyNMIModel.MainDashboardScreen)
            {
                TheFormsGenerator.AssembleDynamicScreens(tInfo, pClientInfo);

                List<TheDashPanelInfo> tMyDashPanels = new ();
                if (tInfo.MyDashPanels != null && tInfo.MyDashPanels.Count > 0)
                    tMyDashPanels.AddRange(tInfo.MyDashPanels);

                List<TheDashboardInfo> tDashs = GetAllDashboards();
                List<IBaseEngine> tLst = TheThingRegistry.GetBaseEngines(true);
                foreach (IBaseEngine tBase in tLst)
                {
                    if (tBase != null && tBase.GetEngineState().IsService && tBase.GetEngineState().IsService && !tBase.GetEngineState().IsDisabled && !tBase.GetEngineState().IsUnloaded)
                    {
                        string tDashID = tBase.GetDashboard();
                        if (tDashID == null) continue;
                        TheDashboardInfo tDash = GetDashboardById(TheCommonUtils.CGuid(tDashID));
                        if (tDash != null) tDashs.Remove(tDash);
                        if (tDash != null && TheUserManager.HasUserAccess(pClientInfo.UserID, tDash.cdeA) && tBase.GetBaseThing() != null)
                        {
                            if (IsDashboardEmpty(tDash, pClientInfo))
                                continue;
                            TheThing tOwnerEngine = tBase.GetBaseThing().GetBaseThing();
                            var tCate = ThePropertyBag.PropBagGetValue(tDash.PropertyBag, "Category", "=");
                            if (string.IsNullOrEmpty(tCate))
                                tCate = GetNodeForCategory();
                            TheDashPanelInfo tD = new (tOwnerEngine)
                            {
                                cdeMID = tDash.cdeMID,
                                cdeA = tDash.cdeA,
                                Flags = 3,
                                FldOrder = tDash.FldOrder,
                                Description = ThePropertyBag.PropBagGetValue(tDash.PropertyBag, "FriendlyName", "="),
                                PanelTitle = tDash.DashboardTitle,
                                ControlClass = "CMyDashboard:" + tDash.cdeMID.ToString(),
                                Category = tCate
                            };
                            var tFace = ThePropertyBag.PropBagGetValue(tDash.PropertyBag, "HTMLUrl", "=");
                            if (!string.IsNullOrEmpty(tFace))
                                ParseFacePlateUrl(tOwnerEngine, tFace, false, pClientInfo.NodeID);
                            if (string.IsNullOrEmpty(tD.Description))
                                tD.Description = TheCommonUtils.cdeStripHTML(tD.PanelTitle);
                            if (tDash.PropertyBag != null && tDash.PropertyBag.Count > 0)
                            {
                                foreach (string tPro in tDash.PropertyBag)
                                    tD.PropertyBag.Add(tPro);
                            }
                            tDash.FireUpdate(tD, tOwnerEngine, pMsg); //OK - late binding
                            tMyDashPanels.Add(tD);
                        }
                    }
                }

                //NEWV4: Allow Things to have Dashboards!
                foreach (TheDashboardInfo tDash in tDashs)
                {
                    if (tDash.cdeMID == TheCDEngines.MyNMIService.MyNMIModel.MainDashboardScreen || tDash.cdeMID == eNMIPortalDashboard) continue;
                    TheThing tOwnerEngine = TheThingRegistry.GetThingByMID(tDash.cdeO);
                    if (TheUserManager.HasUserAccess(pClientInfo.UserID, tDash.cdeA) && tOwnerEngine != null)
                    {
                        IBaseEngine tBas = TheThingRegistry.GetBaseEngine(tOwnerEngine);
                        if (tBas == null || tBas.GetEngineState().IsDisabled || tBas.GetEngineState().IsUnloaded)
                            continue;
                        var tCate = ThePropertyBag.PropBagGetValue(tDash.PropertyBag, "Category", "=");
                        if (string.IsNullOrEmpty(tCate))
                            tCate = GetNodeForCategory();
                        var tControl = ThePropertyBag.PropBagGetValue(tDash.PropertyBag, "ControlClass", "=");
                        TheDashPanelInfo tD = new (tOwnerEngine)
                        {
                            cdeMID = tDash.cdeMID,
                            cdeA = tDash.cdeA,
                            Flags = 3,
                            FldOrder = tDash.FldOrder,
                            Description = ThePropertyBag.PropBagGetValue(tDash.PropertyBag, "FriendlyName", "="),
                            PanelTitle = tDash.DashboardTitle,
                            ControlClass = string.IsNullOrEmpty(tControl) ? "CMyDashboard:" + tDash.cdeMID.ToString() : tControl,
                            Category = tCate
                        };
                        if (string.IsNullOrEmpty(tD.Description))
                            tD.Description = TheCommonUtils.cdeStripHTML(tD.PanelTitle);
                        var tFace = ThePropertyBag.PropBagGetValue(tDash.PropertyBag, "HTMLUrl", "=");
                        if (!string.IsNullOrEmpty(tFace))
                            ParseFacePlateUrl(tOwnerEngine, tFace, false, pClientInfo.NodeID);
                        if (tDash.PropertyBag != null && tDash.PropertyBag.Count > 0)
                        {
                            foreach (string tPro in tDash.PropertyBag)
                                tD.PropertyBag.Add(tPro);
                        }
                        tDash.FireUpdate(tD, tOwnerEngine, pMsg);//OK - late binding
                        tMyDashPanels.Add(tD);
                    }
                }

                if (!TheBaseAssets.MyServiceHostInfo.IsIsolated)
                {
                    tMyDashPanels.Add(new TheDashPanelInfo(TheCDEngines.MyNMIService.GetBaseThing())
                    {
                        cdeMID = new Guid("{7F4B6E0F-70C6-4C6A-B820-4D57B2C35E33}"),
                        cdeA = 0,
                        Flags = 3,
                        FldOrder = 9080,
                        PanelTitle = "My Scenes",
                        ControlClass = "CMyDashboard:7F4B6E0F-70C6-4C6A-B820-4D57B2C35E33",
                        PropertyBag = new ThePropertyBag { "Thumbnail=FA5:f03e" },
                        Category = GetNodeForCategory(),
                    });
                    TheDashPanelInfo tOverallStatus = new (TheCDEngines.MyNMIService.GetBaseThing())
                    {
                        cdeMID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                        cdeA = 0,
                        Flags = 3,
                        FldOrder = 9070,
                        PanelTitle = "Overall Node Status",
                        ControlClass = $"jsAction:TTS:{TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID}",
                        Category = GetNodeForCategory(),
                        PropertyBag = new ThePropertyBag() { "Thumbnail=FA5:f05a", "Foreground=white", "ClassName=cde1PM cdeGlassButton", "Background=" + GetStatusColor(TheBaseAssets.MyServiceHostInfo.StatusLevel) }
                    };
                    ThePropertyBag.PropBagUpdateValue(tOverallStatus.PropertyBag, "UXID", "=", tOverallStatus.cdeMID.ToString());
                    tMyDashPanels.Add(tOverallStatus);
                    if (!TheCommonUtils.IsDeviceSenderType(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType) && pClientInfo.IsFirstNode && TheBaseAssets.MyApplication.MyISMRoot != null)
                    {
                        TheBaseAssets.MyApplication.MyISMRoot.ScanForISMUpdate(true, true, false);
                        if (TheBaseAssets.MyApplication.MyISMRoot.IsUpdateAvailable())
                        {
                            //TODO: Show a list of all updates available...maybe as a toast?
                            tMyDashPanels.Add(new TheDashPanelInfo(TheCDEngines.MyNMIService.GetBaseThing(), new Guid("{6193A416-2511-4ECA-BFC4-B328D48E06F8}"),
                                $"Updates ready to install!", "cdeUpdater")
                            {
                                PropertyBag = new nmiDashboardTile { Thumbnail = "FA5:f3a5", ClassName = "cdeUpdaterTile", TileWidth = 3, TileHeight = 3, Caption = "Updates/New Installs ready" },
                                FldOrder = 9900,
                                Category = "Updates"
                            });
                        }
                        tMyDashPanels.Add(
                    new TheDashPanelInfo(TheCDEngines.MyNMIService.GetBaseThing())
                    {
                        cdeMID = new Guid("{0A3F93CE-4C1A-457A-811A-6679AF4DEE9E}"),
                        cdeA = 128,
                        Flags = 3,
                        FldOrder = 9901,
                        PanelTitle = "Check for Updates",
                        ControlClass = "jsAction:CFU",
                        Category = "Updates",
                        PropertyBag = new nmiCtrlTileButton { Thumbnail = "FA5:f01c" }
                    });
                    }
                }
                if (tMyDashPanels.Count > 0)
                    tInfo.MyDashPanels = tMyDashPanels.OrderBy(s => s.Category).ThenBy(s => s.FldOrder).ToList();
            }
            return tInfo.GetLocalizedScreen(pClientInfo.LCID);
        }

        internal byte[] GetPluginResourceBlob(string pPageTemplate)
        {
            byte[] astream = null;
            lock (MyRegisteredEngines.MyLock)
            {
                foreach (IBaseEngine tBase in MyRegisteredEngines.Values.Where(s => !s.HasCapability(eThingCaps.NMIEngine)))
                {
                    astream = tBase.GetPluginResource(pPageTemplate);
                    if (astream != null)
                    {
                        return astream;
                    }
                }
                foreach (IBaseEngine tBase in MyRegisteredEngines.Values.Where(s => s.HasCapability(eThingCaps.NMIEngine)))
                {
                    astream = tBase.GetPluginResource(pPageTemplate);
                    if (astream != null)
                    {
                        return astream;
                    }
                }
            }
            return astream;
        }

        internal static string StoreToList(string pTableName, string pName, string pItem)
        {
            string res = null;
            object MyStorageMirror = TheCDEngines.GetStorageMirror(pTableName);
            if (MyStorageMirror != null)
            {
                Type magicType = MyStorageMirror.GetType();
#if CDE_STANDARD  //Metro Style Reflection
                var magicMethod = magicType.GetTypeInfo().GetDeclaredMethod("ReturnAsString");
#else
                var magicMethod = magicType.GetMethod("ReturnAsString");
#endif
                if (magicMethod != null)
                    res = magicMethod.Invoke(MyStorageMirror, new[] { pName, (object)pItem }).ToString();
            }
            return res;
        }
        #endregion
    }
}
