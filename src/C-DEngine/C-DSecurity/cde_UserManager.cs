// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using nsCDEngine.Communication;
using nsCDEngine.Engines.ThingService;
using System.Globalization;
using nsCDEngine.ISM;

// ReSharper disable MergeSequentialChecks
// ReSharper disable UseNullPropagation

namespace nsCDEngine.Security
{
    /// <summary>
    /// The UserManager class is managing users currently know to the local node.
    /// If the C-DEngine is hosted in a Viewer - the user manager is managing the currently logged on user
    /// </summary>
    public class TheUserManager
    {
        internal TheUserManager()
        {
            string temp = TheBaseAssets.MySettings.GetSetting("ScopeUserLevel"); //Default is "anonymous" (0)
            if (!string.IsNullOrEmpty(temp))
                mScopedUserLevel = TheCommonUtils.CInt(temp);
        }

        internal void Init(bool StoreUsersInRam, bool ForceReinit)
        {
            if (MyUserRoles == null)
            {
                MyUserRoles = new TheStorageMirror<TheUserRole>(TheCDEngines.MyIStorageService)
                {
                    IsRAMStore = StoreUsersInRam,
                    IsCached = true,
                    CacheStoreInterval = 0,
                    IsCachePersistent = !TheBaseAssets.MyServiceHostInfo.IsCloudService && !TheCommonUtils.IsHostADevice(), // TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay,
                UseSafeSave = true
                };
                MyUserRoles.InitializeStore(false, TheBaseAssets.MyServiceHostInfo.IsNewDevice);
                MyUserRoles.AddItems(new List<TheUserRole>
                {
                    {new TheUserRole(new Guid("F1F472AD-5608-468D-A9E3-223BFDA6C582"), eUserRoles.Administrator, 128, 128, Guid.Empty, true, "Application Administrator")}
                }, null);
                TheBaseAssets.MySYSLOG.WriteToLog(431, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheUserManager", "Administrator role added to UserRegistry", eMsgLevel.l3_ImportantMessage));
            }
            if (MyUserRegistry == null || ForceReinit)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4310, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheUserManager", "Creating UserRegistry", eMsgLevel.l3_ImportantMessage));
                MyUserRegistry = new TheStorageMirror<TheUserDetails>(TheCDEngines.MyIStorageService)
                {
                    IsRAMStore = StoreUsersInRam,
                    CacheTableName = "256n12948216669115511161111",
                    IsCached = true,
                    CacheStoreInterval = 0,
                    IsCachePersistent = !TheBaseAssets.MyServiceHostInfo.IsCloudService && !TheCommonUtils.IsHostADevice(), // (TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay || TheBaseAssets.MyServiceHostInfo.IsIsolated),
                    UseSafeSave = true,
                    AllowFireUpdates = false
                };
                MyUserRegistry.RegisterEvent(eStoreEvents.StoreReady, sinkUserRegistryIsUp);
                if (TheCDEngines.MyIStorageService != null && TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineState().IsService)
                {
                    MyUserRegistry.CreateStore(TheBaseAssets.MyServiceHostInfo.ApplicationName + ": UserRegistry", "Registry of all Users for application " + TheBaseAssets.MyServiceHostInfo.ApplicationTitle, null, false, false);
                }
                else
                {
                    MyUserRegistry.InitializeStore(false, TheBaseAssets.MyServiceHostInfo.IsNewDevice, true, true);
                }
            }
            if (MyDeviceRegistry == null || ForceReinit)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4311, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheUserManager", "Creating DeviceRegistry", eMsgLevel.l3_ImportantMessage));
                MyDeviceRegistry = new TheStorageMirror<TheDeviceRegistryData>(TheCDEngines.MyIStorageService) { IsRAMStore = StoreUsersInRam };
                //if (!StoreUsersInRam)
                //{
                MyDeviceRegistry.RegisterEvent(eStoreEvents.StoreReady, sinkDeviceRegistryIsUp);
                MyDeviceRegistry.IsCachePersistent = !TheBaseAssets.MyServiceHostInfo.IsCloudService && !TheCommonUtils.IsHostADevice(); // TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay;
                MyDeviceRegistry.AllowFireUpdates = false;
                if (TheCDEngines.MyIStorageService != null && TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineState().IsService)
                    MyDeviceRegistry.CreateStore(TheBaseAssets.MyServiceHostInfo.ApplicationName + ": DeviceRegistry", "Registry of all Devices for application " + TheBaseAssets.MyServiceHostInfo.ApplicationTitle, null, false, false);
                else
                    MyDeviceRegistry.InitializeStore(false, TheBaseAssets.MyServiceHostInfo.IsNewDevice);
                //}
            }
        }

        internal void Shutdown()
        {
            if (MyUserRegistry != null)
            {
                MyUserRegistry.Reset();
                MyUserRegistry = null;
            }
            if (MyDeviceRegistry != null)
            {
                MyDeviceRegistry.Reset();
                MyDeviceRegistry = null;
            }
        }

        private void sinkUserRegistryIsUp(StoreEventArgs _)
        {
            LoadFromConfig();
            mIsInitialized = true;
            if (TheBaseAssets.MyServiceHostInfo.IsViewer)
            {
                if (LoggedOnUser != null)
                    UpdateUserRecord(LoggedOnUser); // MyUserRegistry.UpdateItem(LoggedOnUser, null);
                AddRegisteredRoles();
            }
            AddRegisteredUser();
            AddRegisteredUserByRole();
            //string tL = TheCommonUtils.SerializeObjectToJSONString(MyUserRegistry.MyMirrorCache.MyRecords.Values);
            if (TheBaseAssets.MyServiceHostInfo.IsNewDevice && !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.Coufs))
            {
                try
                {
                    CreateUserOnFirstStart(TheBaseAssets.MyServiceHostInfo.Coufs,false, true);
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(431, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheUserManager", "Error during Coufs create", eMsgLevel.l1_Error,e.ToString()));
                }
            }
            TheBaseAssets.MyServiceHostInfo.Coufs = "";
            mIsCompletelyInitialized = true;
            eventUserManagerInitialized?.Invoke();
            TheQueuedSenderRegistry.RegisterHealthTimer(sinkUpdateUserCycle);
            TheQueuedSenderRegistry.eventCloudIsBackUp += sinkUpdateUserCycle2;
        }

        internal static void CreateUserOnFirstStart(string pInStr,bool UpdateIfExist, bool FlushAllUsers)
        {
            if (FlushAllUsers)
                TheBaseAssets.MyApplication.MyUserManager.MyUserRegistry.RemoveAllItems();
            try
            {
                var tNewUser = TheCommonUtils.DeserializeJSONStringToObject<List<TheUserCreator>>(pInStr);
                foreach (var tu in tNewUser)
                {
                    var tnewuser = new TheUserDetails(tu.UID, TheCommonUtils.IsNullOrWhiteSpace(tu.PWD) ? TheBaseAssets.MyScopeManager.CalculateRegisterCode(Guid.NewGuid()) : tu.PWD, tu.Role, tu.EMail, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)
                    {
                        AssignedEasyScope = "*",    //OK
                        NodeScope = (tu.LNO ? $"{TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID}" : "ALL"),
                        AccessMask = tu.ACL,
                        HomeScreen = tu.HS,
                        IsReadOnly = tu.RO,
                        Name = tu.Name
                    };
                    if (UpdateIfExist && TheBaseAssets.MyApplication.MyUserManager.mIsInitialized)
                    {
                        if (!string.IsNullOrEmpty(tnewuser.PrimaryRole))
                        {
                            var t = GetUserByRole(tnewuser.PrimaryRole);
                            tnewuser.cdeMID = t.cdeMID;
                        }
                        UpdateUser(tnewuser);
                    }
                    else
                        AddNewUser(tnewuser);
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(431, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheUserManager", "Error during Coufs create on first startup", eMsgLevel.l1_Error, e.ToString()));
            }
        }

        /// <summary>
        /// This event fires when the User Manager is initialized and ready
        /// </summary>
        public static Action eventUserManagerInitialized = null;
        internal Action<bool, List<TheDeviceRegistryData>> eventDeviceRegistration=null;
        private bool mIsInitialized;
        private bool mIsCompletelyInitialized;
        private readonly int mScopedUserLevel = 1;
        internal TheStorageMirror<TheUserDetails> MyUserRegistry;
        private TheStorageMirror<TheDeviceRegistryData> MyDeviceRegistry;
        private static TheStorageMirror<TheUserRole> MyUserRoles;
        private static readonly List<TheUserDetails> listAddUsers = new List<TheUserDetails>();
        private static readonly List<TheUserRole> listAddRoles = new List<TheUserRole>();
        private static readonly List<TheUserDetails> listAddUsersByRole = new List<TheUserDetails>();
        private static readonly List<TheDeviceRegistryData> MyDevices = new List<TheDeviceRegistryData>();

        internal TheUserDetails LoggedOnUser;
        internal void UserHasLoggedIn(Guid pUserID)
        {
            if (TheBaseAssets.MyServiceHostInfo.IsViewer)
            {
                TheUserDetails pUser = GetUserByID(pUserID);
                if (TheBaseAssets.MyServiceHostInfo.AllowAnonymousAccess && pUser == null)
                {
                    if (LoggedOnUser == null || !LoggedOnUser.PrimaryRole.Equals(eUserRoles.Guest))
                    {
                        pUser = new TheUserDetails("AnonymousUser", TheCommonUtils.MOTLockGenerator(), eUserRoles.Guest, "", Guid.Empty);
                        pUser.EMail = pUser.cdeMID + "@" + TheBaseAssets.MyServiceHostInfo.SiteName;
                        if (TheCDEngines.MyNMIService.IsInit())
                        {
                            TheCDEngines.MyNMIService.MyNMIModel.MyCurrentScreen = "";
                            TheCDEngines.MyNMIService.MyNMIModel.MyLastScreen = "";
                        }
                    }
                    else
                        pUser = LoggedOnUser;
                }

                if (pUser != null && MyUserRegistry.IsReady && (LoggedOnUser == null || !LoggedOnUser.cdeMID.Equals(pUser.cdeMID)))
                    UpdateUserRecord(pUser); // MyUserRegistry.UpdateItem(pUser, null);
                LoggedOnUser = pUser;
                UpdateDevice(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo, pUser == null);
            }
        }

        private void UpdateUserRecord(TheUserDetails pUser)
        {
            MyUserRegistry.UpdateItem(pUser, null);
        }

        internal string GetUsersHomeScreen()
        {
            foreach (TheUserRole role in MyUserRoles.TheValues)
            {
                if (IsCurrentUserRole(role.RoleName))
                    return role.HomeScreen.ToString();
            }
            return null;
        }

        /// <summary>
        /// Brower or App login by Easy Scope ID
        /// </summary>
        /// <param name="pData">Web Request to be checkd for permissions</param>
        /// <param name="pScope">Easy Scope ID to login</param>
        /// <returns></returns>
        internal TheUserDetails LoginUserByScope(TheRequestData pData, string pScope)
        {
            TheUserDetails tUser = null;
            if (((!TheBaseAssets.MyScopeManager.IsScopingEnabled && TheQueuedSenderRegistry.IsScopeKnown(pScope, false)) || (TheBaseAssets.MyScopeManager.IsValidScopeID(TheBaseAssets.MyScopeManager.GetScrambledScopeIDFromEasyID(pScope))))
                || (TheBaseAssets.MyServiceHostInfo.IsCloudService && TheBaseAssets.MyServiceHostInfo.AllowAdhocScopes && !TheBaseAssets.MyScopeManager.IsScopingEnabled))
            {
                tUser = new TheUserDetails
                {
                    NodeScope = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString(),
                    AssignedEasyScope = TheBaseAssets.MyScopeManager.GetScrambledScopeIDFromEasyID(pScope), //SECURITY-REVIEW: starting 4.106 AES contains SScopeID
                    IsTempUser = true,
                    AccessMask = mScopedUserLevel,
                    Password = TheBaseAssets.MySecrets.CreatePasswordHash(TheCommonUtils.MOTLockGenerator())  //PW-SECURITY-REVIEW: create Hash here
                };
                tUser.EMail = tUser.cdeMID + "@" + TheBaseAssets.MyServiceHostInfo.SiteName;
                if (pData.SessionState != null)
                {
                    pData.SessionState.SScopeID = tUser.AssignedEasyScope; //SECURITY-REVIEW: 4.106 AssignedEasyScope either contains * or a SScopeID
                    AddNewUser(tUser);
                    pData.SessionState.CID = tUser.cdeMID;
                }
            }
            if (tUser == null)
            {
                TheCDEKPIs.IncrementKPI(eKPINames.BruteDelay);
                //Security Fix: ID#770 - wait 200 ms before returning anything with error code to limit BruteForce
                TheCommonUtils.SleepOneEye(200, 100);
            }
            return tUser;
        }

        internal TheUserDetails GetOrCreateUser(string email, string name, string pRealPassword)
        {
            TheUserDetails tUser= MyUserRegistry.TheValues.FirstOrDefault(s => TheBaseAssets.MySecrets.DoPasswordsMatch(s.Password, pRealPassword)); //Hash to Hash Compare
            if (tUser != null)
                return tUser;
            tUser = new TheUserDetails
            {
                NodeScope = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString(),
                AssignedEasyScope = "*", // TheBaseAssets.MyScopeManager.EasyScope, //SECURITY-REVIEW: This is not used anyway at this point
                AccessMask = 0,
                Password = TheBaseAssets.MySecrets.CreatePasswordHash(pRealPassword), //PW-SECURITY-REVIEW: set Hash here
                EMail = email,
                Name = name
            };
            AddNewUser(tUser);
            return tUser;
        }


        internal List<TheUserDetails> LoginUserByPin(TheRequestData pData, string pPin)
        {
            List<TheUserDetails> tUsers = null;
            if (MyUserRegistry.Count > 0)
            {
                string Teto = pData?.SessionState?.TETO;
                if (!string.IsNullOrEmpty(Teto) && !string.IsNullOrEmpty(pPin))
                {
                    string HashToken = TheBaseAssets.MySecrets.CreatePasswordHash($"{Teto.ToUpper()}@@{pPin}");

                    //TheBaseAssets.MySYSLOG.WriteToLog(4313, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheUserManager", $"Teto:{Teto} Pin:{pPin} Hash:{HashToken}", eMsgLevel.l3_ImportantMessage));

                    try
                    {
                            tUsers = MyUserRegistry.MyMirrorCache.GetEntriesByFunc(
                                s => (!s.IsTempUser &&
                                        s.PinHash == HashToken &&
                                        !string.IsNullOrEmpty(s.NodeScope) &&
                                        (s.NodeScope.ToUpper().Equals("ALL") || TheCommonUtils.IsLocalhost(TheCommonUtils.CGuid(s.NodeScope)))
                                    ));
                    }
                    catch (Exception)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4313, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheUserManager", "Error during Pin Login", eMsgLevel.l1_Error));
                    }
                    FinalizeUserLogin(pData, tUsers, Guid.Empty);
                }
            }
            if (tUsers?.Count == 0)
            {
                TheCDEKPIs.IncrementKPI(eKPINames.BruteDelay);
                //Security Fix: ID#770 - wait 200 ms before returning anything with error code to limit BruteForce
                TheCommonUtils.SleepOneEye(200, 100);
                tUsers = null;
            }
            return tUsers;
        }

        internal List<TheUserDetails> LoginByRefreshToken(TheRequestData pData, string pToken)
        {
            List<TheUserDetails> tUsers = null;
            if (MyUserRegistry.Count > 0)
            {
                if (pToken?.StartsWith("UT")==true)
                {
                    Guid tToken=Guid.Empty;
                    try
                    {
                        var tReTok = TheCommonUtils.cdeDecrypt(pToken.Substring(2), TheBaseAssets.MySecrets.GetNodeKey());  //token cryped against deviceID
                        if (tReTok?.Split(':')?.Length > 1)
                            tToken = TheCommonUtils.CGuid(tReTok?.Split(':')[1]);
                        if (tToken != Guid.Empty)
                        {
                            tUsers = MyUserRegistry.MyMirrorCache.GetEntriesByFunc(
                                s => (!s.IsTempUser &&
                                        s.RefreshTokens?.GetEntryByID(tToken) != null &&
                                        !string.IsNullOrEmpty(s.NodeScope) &&
                                        (s.NodeScope.ToUpper().Equals("ALL") || TheCommonUtils.IsLocalhost(TheCommonUtils.CGuid(s.NodeScope)))
                                    ));
                        }
                    }
                    catch (Exception)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4313,TSM.L(eDEBUG_LEVELS.ESSENTIALS)?null: new TSM("TheUserManager", "Error during Token Login", eMsgLevel.l1_Error));
                    }
                    FinalizeUserLogin(pData, tUsers, tToken);
                }
            }
            if (tUsers?.Count == 0)
            {
                TheCDEKPIs.IncrementKPI(eKPINames.BruteDelay);
                //Security Fix: ID#770 - wait 200 ms before returning anything with error code to limit BruteForce
                TheCommonUtils.SleepOneEye(200, 100);
                tUsers = null;
            }
            return tUsers;
        }

        private static void FinalizeUserLogin(TheRequestData pData, List<TheUserDetails> tUsers, Guid tToken)
        {
            if (tUsers?.Count > 0)
            {
                List<TheUserDetails> toDelet = new List<TheUserDetails>();
                foreach (var tUser in tUsers)
                {
                    if (tToken!=Guid.Empty)
                        tUser.RefreshTokens.RemoveAnItemByID(tToken, null); //Refresh Tokens can only be used once!
                    if (!string.IsNullOrEmpty(tUser.AssignedEasyScope))
                    {
                        string tScope = "*".Equals(tUser.AssignedEasyScope) ? TheBaseAssets.MyScopeManager.ScopeID : TheBaseAssets.MyScopeManager.GetRealScopeID(tUser.AssignedEasyScope);
                        if (!string.IsNullOrEmpty(TheBaseAssets.MyScopeManager.ScopeID) && !string.IsNullOrEmpty(tScope) && !TheQueuedSenderRegistry.IsScopeKnown(tScope, true))    //NEW:2.06 If Relay is down for the Scope - Login will fail
                        {
                            toDelet.Add(tUser);
                        }
                    }
                    else
                        toDelet.Add(tUser); //NEW:2.06 No longer allow unscoped Users
                }
                foreach (var tU in toDelet)
                {
                    tUsers.Remove(tU);
                }
                if (pData != null && pData.SessionState != null && tUsers?.Count > 0)
                {
                    if (tUsers?.Count == 1)
                    {
                        var tUser = tUsers[0];  //Only one mesh found - login user
                        SetUserInSessionState(pData, tUser);
                    }
                    else if (tUsers?.Count > 1)
                    {
                        List<TheMeshPicker> tMeshes = new List<TheMeshPicker>();
                        foreach (var tUser in tUsers)
                        {
                            string tScope = "*".Equals(tUser.AssignedEasyScope) ? TheBaseAssets.MyScopeManager.ScopeID : TheBaseAssets.MyScopeManager.GetRealScopeID(tUser.AssignedEasyScope);      //SECURITY-REVIEW: starting 4.106 AES contains SScopeID
                            if (tScope.Length < 4)
                                continue; //This should not happen (i.e. an unscoped user) but better check
                            TheMeshPicker tPick = new TheMeshPicker() { UserID = tUser.cdeMID, MeshHash = tScope.Substring(0, 4).ToUpper() };
                            tPick.NodeNames = TheQueuedSenderRegistry.GetNodeNamesByRScope(tScope);
                            tPick.HomeNode = TheQueuedSenderRegistry.GetNodeNameByNodeID(tUser.HomeNode);
                            tMeshes.Add(tPick);
                        }
                        pData.SessionState.Meshes = tMeshes;
                    }
                }
            }
        }

        internal static string AddTokenToUser(TheUserDetails tUser)
        {
            var tNewToken = new TheRefreshToken
            {
                cdeEXP = TheBaseAssets.MyServiceHostInfo.TokenLifeTime
            };
            var LastToken = $"UT{TheCommonUtils.cdeEncrypt($"{TheCommonUtils.GetRandomUInt(100,999)}:{tNewToken.cdeMID}", TheBaseAssets.MySecrets.GetNodeKey())}";  //token cryped against deviceID
            tUser.RefreshTokens.AddAnItem(tNewToken, null);
            return LastToken;
        }

        internal List<TheUserDetails> LoginUser(TheRequestData pData, string pUID, string pRealPW)
        {
            List<TheUserDetails> tUsers = null;
            if (MyUserRegistry.Count > 0)
            {
                if (!string.IsNullOrEmpty(pUID) && !string.IsNullOrEmpty(pRealPW))
                {
                    tUsers = MyUserRegistry.MyMirrorCache.GetEntriesByFunc(
                        s => ((!s.IsTempUser &&
                              (!string.IsNullOrEmpty(s.EMail) && s.EMail.Equals(pUID, StringComparison.CurrentCultureIgnoreCase))) &&
                              TheBaseAssets.MySecrets.DoPasswordsMatch(s.Password, pRealPW) &&   //Hash to RealPW Compare
                                !string.IsNullOrEmpty(s.NodeScope) &&
                                (s.NodeScope.ToUpper().Equals("ALL") ||
                                TheCommonUtils.IsLocalhost(TheCommonUtils.CGuid(s.NodeScope))
                            )));
                    FinalizeUserLogin(pData, tUsers, Guid.Empty);
                }
            }
            if (tUsers?.Count == 0)
            {
                TheCDEKPIs.IncrementKPI(eKPINames.BruteDelay);
                //Security Fix: ID#770 - wait 200 ms before returning anything with error code to limit BruteForce
                TheCommonUtils.SleepOneEye(200, 100);
                tUsers = null;
            }
            return tUsers;
        }

        internal static void SetUserInSessionState(TheRequestData pData, TheUserDetails tUser)
        {
            if (pData == null || pData.SessionState == null)
                return;
            pData.SessionState.SScopeID = "*".Equals(tUser.AssignedEasyScope) ? TheBaseAssets.MyScopeManager.GetScrambledScopeID() : tUser.AssignedEasyScope;      //SECURITY-REVIEW: starting 4.106 AES contains SScopeID
            pData.SessionState.CID = tUser.cdeMID;
            pData.SessionState.LCID = tUser.LCID;
        }

        internal List<string> GetKnownRoles()
        {
            List<string> tRoles = new List<string>();
            foreach (string key in MyUserRoles.TheKeys)
                tRoles.Add(MyUserRoles.GetEntryByID(key).RoleName);
            return tRoles;
        }

        /// <summary>
        /// Allows to add a new UserRole to the system
        /// </summary>
        /// <param name="tRole"></param>
        internal void AddNewRole(TheUserRole tRole)
        {
            MyUserRoles.AddAnItem(tRole,null); //SECURITY-REVIEW: Might be a backdoor - Review in V4
        }

        /// <summary>
        /// UX Viewer can use this function to send credentials to a relay node
        /// This call is only allowed if the usermanager is turned ON
        /// </summary>
        /// <param name="pMsg">TSM must contain a valid SEID pointing to a Session that has valid RSAPublic Keys</param>
        /// <param name="pUID">Username to login</param>
        /// <param name="pRealPW">Password to login</param>
        /// <returns></returns>
        public static bool SentLoginCredentials(TSM pMsg, string pUID, string pRealPW)
        {
            if (!TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper || pMsg == null || string.IsNullOrEmpty(pMsg.SEID)) return false;
            TSM tSend = new TSM(eEngineName.ContentService, "")
            {
                TXT = "CDE_LOGIN",
                PLB = TheCommonUtils.cdeRSAEncrypt(TheCommonUtils.CGuid(pMsg.SEID), string.Format("{0}:;:{1}", pUID, pRealPW))
            };
            if (tSend.PLB == null) return false;
            tSend.PLS = "BIN";
            TheCommCore.PublishToOriginator(pMsg, tSend);
            return true;
        }

        /// <summary>
        /// Sends the current AccessLevel of the given user to an NMI Target Node as a Popup. It will only be sent to a locally connected node and not via the cloud.
        /// </summary>
        /// <param name="pUser"></param>
        /// <param name="pTargetNode"></param>
        /// <param name="pInfo"></param>
        /// <param name="SilentOnly">Dont show Info Screen - just send ACL via NMI_UIDACL</param>
        public static bool SendACLToNMI(Guid pTargetNode, Guid pUser, TheClientInfo pInfo, bool SilentOnly=false)
        {
            if (pUser == Guid.Empty || pTargetNode == Guid.Empty || pInfo==null)
                return false;
            if (HasUserAccess(pInfo.UserID, 128))
            {
                TheUserDetails tUser = TheBaseAssets.MyApplication.MyUserManager?.MyUserRegistry?.GetEntryByID(pUser);
                if (tUser != null)
                {
                    if (!SilentOnly)
                    {
                        var tTSM = new TSM(eEngineName.NMIService, "NMI_INFO", $"User AccessLevel is: {tUser.AccessMask} ({ShowBitField(tUser.AccessMask, 8, "Service Guest;IT Guest;OT User;Service User;IT User;OT Admin; Service Admin;IT Admin")})");
                        tTSM.SetToNodesOnly(true);
                        tTSM.QDX = 0;
                        TheCommCore.PublishToNode(pTargetNode, tTSM);
                    }
                    SendACLToNMISilent(pTargetNode, pUser, pInfo);
                    return true;
                }
            }
            return false;
        }

        internal static bool SendACLToNMISilent(Guid pTargetNode, Guid pUser, TheClientInfo pInfo)
        {
            if (pUser == Guid.Empty || pTargetNode == Guid.Empty || pInfo == null)
                return false;
            if (HasUserAccess(pInfo.UserID,128))
            {
                TheUserDetails tUser = TheBaseAssets.MyApplication.MyUserManager?.MyUserRegistry?.GetEntryByID(pUser);
                if (tUser != null)
                {
                    var tTSM = new TSM(eEngineName.NMIService, "NMI_UIDACL", tUser.AccessMask.ToString());
                    tTSM.SetToNodesOnly(true);
                    tTSM.QDX = 0;
                    tTSM.OWN = "DD3DF621-ACAC-4B77-9856-87165138B028";
                    TheCommCore.PublishToNode(pTargetNode, tTSM);
                    return true;
                }
            }
            return false;
        }

        private static string ShowBitField(int pMask, int bits, string Labels)
        {
            if (bits < 1)
                return "0";
            string res = "";
            int j = 1;
            var tLbls = Labels.Split(';');
            for (int i=0;i<bits; i++)
            {
                res = $"{(tLbls.Length>i?tLbls[i]:"Level")} {j}: {((pMask&j)!=0?"yes":"no")}</br>" +res;
                j <<= 1;
            }
            return $"</br>{res}";
        }

        #region Device Registry Stuff

        internal string RegisterNewDevice(string DeviceDescription,cdeSenderType tSenderType)
        {
            TheDeviceRegistryData tData = new TheDeviceRegistryData
            {
                DeviceID = TheBaseAssets.MyScopeManager.GenerateNewAppDeviceID(tSenderType),
                DeviceType = DeviceDescription,
                LastAccess = DateTimeOffset.Now
            };
            MyDeviceRegistry.AddAnItem(tData);
            return tData.DeviceID.ToString();
        }

        private void sinkDeviceRegistryIsUp(StoreEventArgs nouse)
        {
            if (TheBaseAssets.MyServiceHostInfo.MyDeviceInfo != null)
                RegisterNewDevice(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo);
        }
        internal void RegisterNewDevice(TheDeviceRegistryData pDevice)
        {
            if (MyDeviceRegistry == null) return;
            MyDeviceRegistry.GetRecords("DeviceID='" + pDevice.DeviceID + "'", 1, sinkRegisterDevice, false);
        }

        internal void UpdateDevice(TheDeviceRegistryData pDevice, bool ResetUser)
        {
            if (MyDeviceRegistry == null) return;
            if (ResetUser)
                MyDeviceRegistry.GetRecords("DeviceID='" + pDevice.DeviceID + "'", 1, sinkRegisterDeviceAndReset, false);
            else
                MyDeviceRegistry.GetRecords("DeviceID='" + pDevice.DeviceID + "'", 1, sinkUpdateDevice, false);
        }
        private void sinkRegisterDevice(TheStorageMirror<TheDeviceRegistryData>.StoreResponse tRes)
        {
            InsertOrUpdateDevice(tRes, false);
        }

        private void sinkRegisterDeviceAndReset(TheStorageMirror<TheDeviceRegistryData>.StoreResponse tRes)
        {
            if (tRes != null && !tRes.HasErrors && tRes.MyRecords.Count > 0)
            {
                TheDeviceRegistryData tData = tRes.MyRecords[0];
                tData.LastAccess = DateTimeOffset.Now;
                tData.PrimaryUserID = tData.DeviceID;
                MyDeviceRegistry.UpdateItem(tData);
            }
        }
        private void sinkUpdateDevice(TheStorageMirror<TheDeviceRegistryData>.StoreResponse tRes)
        {
            InsertOrUpdateDevice(tRes, true);
        }


        private void InsertOrUpdateDevice(TheStorageMirror<TheDeviceRegistryData>.StoreResponse tRes, bool UpdateOnly)
        {
            if (tRes != null)
            {
                bool SetDeviceInfo = false;
                TheDeviceRegistryData tData = null;
                if (!tRes.HasErrors && tRes.MyRecords.Count > 0)
                {
                    tData = tRes.MyRecords[0];
                    tData.LastAccess = DateTimeOffset.Now;
                    if (TheBaseAssets.MyServiceHostInfo.IsViewer && (!tData.PrimaryUserID.Equals(Guid.Empty)))
                    {
                        if (TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser == null || (TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser != null && TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser.cdeMID.Equals(tData.PrimaryUserID)))
                        {
                            eventDeviceRegistration?.Invoke(false, new List<TheDeviceRegistryData> { tData });
                        }
                    }
                    if (TheBaseAssets.MyServiceHostInfo.MyDeviceInfo != null && tData.DeviceID.Equals(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID))
                        SetDeviceInfo = true;
                }
                else
                {
                    if (UpdateOnly)
                    {
                        eventDeviceRegistration?.Invoke(false, null);
                        return;
                    }
                    tData = new TheDeviceRegistryData();
                    if (string.IsNullOrEmpty(tRes.SQLFilter)) return;
                    tData.DeviceID = TheCommonUtils.CGuid(tRes.SQLFilter.Substring(10, tRes.SQLFilter.Length - 11));
                    tData.LastAccess = DateTimeOffset.Now;
                    SetDeviceInfo = true;
                }

                if (TheBaseAssets.MyServiceHostInfo.IsViewer && (TheCommonUtils.IsHostADevice() && TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser != null))
                    tData.PrimaryUserID = TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser.cdeMID;
                else
                    tData.PrimaryUserID = tData.DeviceID;
                if (TheBaseAssets.MyServiceHostInfo.MyDeviceInfo != null && SetDeviceInfo)
                {
                    if (string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceName)) TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceName = "Unknown";
                    tData.DeviceName = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceName;
                    if (TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceName.Length > 255)
                        tData.DeviceName = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceName.Substring(0, 255);
                    tData.DeviceType = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceType;
                }
                if (TheCommonUtils.IsHostADevice())
                    MyDeviceRegistry.UpdateItem(tData, sinkDeviceUpdated);
                else
                    MyDeviceRegistry.UpdateItem(tData, null);
            }
        }

        private void sinkDeviceUpdated(TheStorageMirror<TheDeviceRegistryData>.StoreResponse tRes)
        {
            if (TheBaseAssets.MyServiceHostInfo.IsViewer && (LoggedOnUser != null && !LoggedOnUser.cdeMID.Equals(Guid.Empty)))
                MyDeviceRegistry.GetRecords("PrimaryUserID='" + TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser.cdeMID.ToString() + "'", 0, sinkUpdateDeviceList, false);
            else
                if (eventDeviceRegistration != null && tRes != null && !tRes.HasErrors && tRes.MyRecords.Count > 0)
                    eventDeviceRegistration(true, tRes.MyRecords);
        }
        private void sinkUpdateDeviceList(TheStorageMirror<TheDeviceRegistryData>.StoreResponse tRes)
        {
            if (tRes != null && !tRes.HasErrors && tRes.MyRecords.Count > 0)
            {
                lock (MyDevices)
                {
                    MyDevices.Clear();
                    foreach (TheDeviceRegistryData tReg in tRes.MyRecords)
                    {
                        MyDevices.Add(tReg);
                    }
                }
            }
            if (eventDeviceRegistration != null && tRes != null && !tRes.HasErrors && tRes.MyRecords.Count > 0)
                eventDeviceRegistration(true, tRes.MyRecords);
        }

        internal List<TheDeviceRegistryData> GetMyDevices()
        {
            return MyDevices;
        }
        #endregion



        #region UserManagement Helpers
        internal static string GetUserHomeScreen(TheRequestData pRequestData, TheUserDetails tUser)
        {
            if (tUser == null)
                return "";
            string tHomeScreen = tUser.HomeScreen;
            //if (pRequestData == null || pRequestData.SessionState == null) return tHomeScreen;
            if (!string.IsNullOrEmpty(pRequestData?.SessionState?.HS))
                tHomeScreen = pRequestData.SessionState.HS;
            else
            {
                bool HSSet = false;
                TheThing tT = TheThingRegistry.GetThingByFunc(eEngineName.NMIService, s => s.UID == tUser.cdeMID && TheThing.GetSafePropertyBool(s, "IsHomeScene"));
                if (tT != null)
                {
                    string tTargetDir = $"scenes\\{tUser.cdeMID}\\{tT.cdeMID}.cdescene";
                    if (TheCommonUtils.LoadStringFromDisk(tTargetDir, null) != null)
                    {
                        tHomeScreen = tT.cdeMID.ToString();
                        HSSet = true;
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(pRequestData?.SessionState?.InitReferer))
                    {
                        string[] tP = pRequestData.SessionState.InitReferer.Split('?');
                        if (tP.Length > 1)
                        {
                            if (tP[1].StartsWith("CDEDL"))
                            {
                                var qs = tP[1].Substring(5).Split('&');
                                tHomeScreen = qs[0]; // tP[1].Substring(5);
                                HSSet = true;
                            }
                        }
                    }
                }
                if (!HSSet)
                {
                    string tTargetDir = $"scenes\\MyHome.cdescene";
                    if (TheCommonUtils.LoadStringFromDisk(tTargetDir, null) != null)
                        tHomeScreen = TheCommonUtils.CGuid("{40AB04FA-BF1D-4EEE-CDE9-DF1E74CC69A7}").ToString();
                }
            }
            return tHomeScreen;
        }

        internal bool UpdateUserHomeScreen(Guid pUserID, Guid HomeID)
        {
            if (pUserID == Guid.Empty || HomeID == Guid.Empty) return false;
            TheUserDetails pUser = GetUserByID(pUserID);
            if (pUser != null && string.IsNullOrEmpty(pUser.HomeScreen))
            {
                pUser.HomeScreen = HomeID.ToString();
                MyUserRegistry.UpdateItem(pUser, null);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the users Scrambled ScopeID
        /// </summary>
        /// <param name="pUserID">User to look for</param>
        /// <returns></returns>
        public static string GetScrambledUserScope(Guid pUserID)
        {
            string UserSScope = "";
            TheUserDetails tCurrentUser = GetUserByID(pUserID);
            if (tCurrentUser != null && !string.IsNullOrEmpty(tCurrentUser.AssignedEasyScope)) //AES OK
            {
                UserSScope = "*".Equals(tCurrentUser.AssignedEasyScope) ? TheBaseAssets.MyScopeManager.GetScrambledScopeID() : tCurrentUser.AssignedEasyScope;     //SECURITY-REVIEW: starting 4.106 AES contains SScopeID
            }
            return UserSScope;
        }
        /// <summary>
        /// New V4: Returns true if the given Role does not have an admin account or the admin account has not password
        /// </summary>
        /// <param name="pUserRole"></param>
        /// <returns></returns>
        public static bool DoesAdminRequirePWD(string pUserRole)
        {
            if (TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper && !TheBaseAssets.MyServiceHostInfo.IsCloudService)
            {
                TheUserDetails tAdmin = GetUserByRole(pUserRole);
                if ((tAdmin != null && string.IsNullOrEmpty(tAdmin.Password)) ||
                    (tAdmin == null))
                    return true;
            }
            return false;
        }


        /// <summary>
        /// The cloud calls this to check if a node has replicated its User database already.
        /// </summary>
        /// <param name="pRealScope"></param>
        /// <returns></returns>
        internal static bool HasScopeUsers(string pRealScope)
        {
            if (string.IsNullOrEmpty(pRealScope) || !TheUserManager.IsInitialized() || TheBaseAssets.MyApplication.MyUserManager.MyUserRegistry.Count == 0) return false;
            foreach (TheUserDetails tUser in TheBaseAssets.MyApplication.MyUserManager.MyUserRegistry.TheValues)
            {
                if (TheBaseAssets.MyScopeManager.GetRealScopeID(tUser.AssignedEasyScope).Equals(pRealScope))    //SECURITY-REVIEW: starting 4.106 AES contains SScopeID ; * is here not allowed - only concrete scopes
                    return true;
            }
            return false;
        }

        /// <summary>
        /// returns true of false pending if the user has access to a resource with the ACL provided
        /// </summary>
        /// <param name="pUID">Username</param>
        /// <param name="pRealPW">Password</param>
        /// <param name="ACL">The ACL of the resource the user wants to have access to</param>
        /// <returns></returns>
        public static bool HasUserAccess(string pUID, string pRealPW, int ACL)
        {
            if (!TheUserManager.IsInitialized()) return false;
            int tAcl = GetUserAccessLevel(pUID, pRealPW);
            if (tAcl < 0) return false;
            return ACL == 0 || ((tAcl & ACL) != 0);

        }

        /// <summary>
        /// returns true if a session contains a valid user
        /// </summary>
        /// <param name="pSession"></param>
        /// <returns></returns>
        public static bool HasSessionValidUser(TheSessionState pSession)
        {
            return pSession!=null && pSession.CID != Guid.Empty;
        }

        /// <summary>
        /// Return public information about a user that can be used in the NMI
        /// </summary>
        /// <param name="pSession"></param>
        /// <returns></returns>
        public static TheISBConnect GetUserDataForNMI(TheSessionState pSession)
        {
            if (pSession == null || pSession.CID==Guid.Empty) return null;
            return GetUserDataForNMI(pSession.CID, false);
        }
        internal static TheISBConnect GetUserDataForNMI(Guid pUserID, bool IncludeSecrets)
        {
            if (pUserID == Guid.Empty)
                return null;

            var tRes = new TheISBConnect();
            TheUserDetails tUser = TheUserManager.GetUserByID(pUserID);
            if (tUser != null)
            {
                tRes.LCI = tUser.LCID;
                tRes.UNA = tUser.Name;
                tRes.UPRE = tUser.GetUserPrefString();
                var tHomeScreen = GetUserHomeScreen(null, tUser);
                if (!string.IsNullOrEmpty(tHomeScreen))
                {
                    tRes.SSC = tHomeScreen.Split(';')[0];
                    tRes.PS = tHomeScreen.Split(';').Length > 1 ? tHomeScreen.Split(';')[1] : "";
                }
                if (IncludeSecrets)
                {
                    tRes.QUI = tUser.EMail;
                    tRes.PWD = tUser.Password;  //SECURITY-REVIEW: This will no longer work as the Password field only contains a hash now...Use Cases for this have to be revalidated! At this point there is no active Use Case!
                }
                else
                {
                    tRes.PWD= TheUserManager.AddTokenToUser(tUser);
                }
            }
            return tRes;
        }

        /// <summary>
        /// Return the Access Level of a given UID/PWD combination
        /// </summary>
        /// <param name="pUID"></param>
        /// <param name="pRealPW"></param>
        /// <returns>Return -1 if the user is unknown</returns>
        public static int GetUserAccessLevel(string pUID, string pRealPW)
        {
            if (!TheUserManager.IsInitialized()) return -1;

            List<TheUserDetails> tDet = TheBaseAssets.MyApplication.MyUserManager.LoginUser(null, pUID, pRealPW);
            if (tDet == null) return -1;
            return GetUserAccessLevel(tDet.First());    //This might not return the correct user. But with only UID/PWD we have only two choices: return the ACL of the first user or "OR" all ACLs of all found users (i.e. user (1) on scope1 and admin (128) on scope2 would return admin+user (129))
        }

        private static int GetUserAccessLevel(TheUserDetails tUser)
        {
            if (tUser == null) return 0;
            int accessMask = tUser.AccessMask;
            if (!(tUser.IsOnCurrentNode() || TheBaseAssets.MySettings.IsNodeTrusted(TheCommonUtils.CGuid(tUser.HomeNode))))
                accessMask = 0;
            if (tUser.Roles?.Count > 0)
            {
                foreach (var roleId in tUser.Roles)
                {
                    var role = MyUserRoles.GetEntryByID(roleId);
                    if (role != null)
                    {
                        accessMask |= (int) (role.AccessLevel);
                    }
                }
            }
            return accessMask;
        }

        /// <summary>
        /// Checks if the user with the gived ID has the access permission defined in ACL.
        ///
        /// </summary>
        /// <param name="pCurrentUserID">User ID to be checked</param>
        /// <param name="ACL">Returns true if the user has this access Level</param>
        public static bool HasUserAccess(Guid pCurrentUserID, int ACL)
        {
            TheUserDetails tCurrentUser = GetUserByID(pCurrentUserID);
            return ACL == 0 || (tCurrentUser != null && (GetUserAccessLevel(tCurrentUser) & ACL) != 0); //TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper &&
        }

        /// <summary>
        /// Returns true if the User of the ID is coming from a trusted node
        /// </summary>
        /// <param name="pCurrentUserID"></param>
        /// <returns></returns>
        public static bool IsUserTrusted(Guid pCurrentUserID)
        {
            TheUserDetails tUser = GetUserByID(pCurrentUserID);
            return tUser != null && TheBaseAssets.MySettings.IsNodeTrusted(TheCommonUtils.CGuid(tUser.HomeNode));
        }

        /// <summary>
        /// Checks if the user with the gived ID has the access permission defined in ACL.
        ///
        /// </summary>
        /// <param name="pCurrentUserID">User ID to be checked</param>
        /// <param name="ACL">Returns true if the user has this access Level</param>
        /// <param name="AdminHasAccess">if this is true, the function only returns true of the user has Admin rights</param>
        /// <returns></returns>
        public static bool HasUserAccess(Guid pCurrentUserID, int ACL, bool AdminHasAccess)
        {
            TheUserDetails tCurrentUser = GetUserByID(pCurrentUserID);
            //int accessLevel = GetUserAccessLevel(tCurrentUser);
            return
                   ACL == 0                                                          // Anybody can see ACL 0
                || (tCurrentUser != null
                     && ((AdminHasAccess && (tCurrentUser.AccessMask & 128) != 0) // Admins get access regardless of ACL
                          || (tCurrentUser.AccessMask & ACL) != 0                    // User has at least one of the flags in the ACL
                        )
                    ); //TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper &&

        }

        /// <summary>
        /// Returns true if the given user has either permission to a given ACL or the Required Permission given
        /// </summary>
        /// <param name="pCurrentUserID">User to check for</param>
        /// <param name="ACL">Accesslevel</param>
        /// <param name="requiredPermission">Required Permission</param>
        /// <returns></returns>
        public static bool HasUserAccess(Guid pCurrentUserID, int ACL, string requiredPermission)
        {
            if (HasUserAccess(pCurrentUserID, ACL))
            {
                return true;
            }
            return HasUserPermission(pCurrentUserID, requiredPermission);
        }

        /// <summary>
        /// Returns true if the given user has permission to a Required Permission given
        /// </summary>
        /// <param name="pUser">User to check for</param>
        /// <param name="requiredPermission">Required Permission</param>
        /// <returns></returns>
        public static bool HasUserPermission(Guid pUser, string requiredPermission)
        {
            return HasUserPermissions(pUser, new List<string> { requiredPermission });
        }

        /// <summary>
        /// Returns the name of a user
        /// </summary>
        /// <param name="pUser">UID of the user to request name from</param>
        /// <returns></returns>
        public static string GetUserFullName(Guid pUser)
        {
            TheUserDetails tCurrentUser = GetUserByID(pUser);
            if (tCurrentUser != null)
            {
                return tCurrentUser.Name;
            }
            return null;
        }

        private static List<PermissionGrant> GetUserPermissions(Guid pUser)
        {
            // TODO Cache the permissions
            var permissionGrants = new List<PermissionGrant>();
            TheUserDetails tCurrentUser = GetUserByID(pUser);
            if (tCurrentUser != null)
            {
                permissionGrants.AddRange(tCurrentUser.Permissions.Select(p => new PermissionGrant(p)));
                if (tCurrentUser.Roles?.Count > 0)
                {
                    foreach (var roleId in tCurrentUser.Roles)
                    {
                        var role = MyUserRoles.GetEntryByID(roleId);
                        if (role?.Permissions?.Count > 0)
                        {
                            permissionGrants.AddRange(role.Permissions.Select(p => new PermissionGrant(p)));
                        }
                    }
                }
            }
            return permissionGrants;
        }

        /// <summary>
        /// Returns true if the given user has permission to one of Required Permissions given
        /// </summary>
        /// <param name="pUser">User to check for</param>
        /// <param name="requiredPermissions">List of Required Permissions</param>
        /// <returns></returns>
        public static bool HasUserPermissions(Guid pUser, List<string> requiredPermissions)
        {
            return HasUserPermissions(pUser, requiredPermissions.Select(p => new PermissionGrant(p)));
        }

        private static bool HasUserPermissions(Guid pUser, IEnumerable<PermissionGrant> requiredPermissions)
        {
            var permissionGrants = GetUserPermissions(pUser);

            bool bMatch = true;
            foreach (var requiredPermission in requiredPermissions)
            {
                var requiredParts = requiredPermission.Parts;

                foreach (var permissionGrant in permissionGrants)
                {
                    var grantParts = permissionGrant.Parts;

                    int i = 0;
                    while (bMatch && i < grantParts.Length && i < requiredParts.Length)
                    {
                        if (!requiredParts[i].Equals(grantParts[i], StringComparison.Ordinal) && grantParts[i] != "*")
                        {
                            bMatch = false;
                        }
                        i++;
                    }
                    if (i < requiredParts.Length)
                    {
                        bMatch = false;
                    }
                    if (!bMatch)
                    {
                        break;
                    }
                }
                if (!bMatch)
                {
                    break;
                }
            }

            return bMatch;
        }


        bool GetClaimAsString(Dictionary<string, object> idTokenClaims, string claimName, out string claimValue)
        {
            if (idTokenClaims.TryGetValue(claimName, out object value) && value is string)
            {
                claimValue = value as string;
                return true;
            }
            claimValue = null;
            return false;
        }

#if !CDE_NET35 && !CDE_NET4
        readonly bool bMapUsersByRole = false; // TODO Decide if we want this functionality, and if so expose as setting etc.
#endif
        internal TheUserDetails GetUserFromClaims(Dictionary<string, object> idTokenClaims)
        {
            TheUserDetails tUser = null;
            {
                if (GetClaimAsString(idTokenClaims, "unique_name", out string userName)
                    || GetClaimAsString(idTokenClaims, "upn", out userName)
                    || GetClaimAsString(idTokenClaims, "sub", out userName)
                    || GetClaimAsString(idTokenClaims, "name", out userName)
                    )
                {
                    tUser = MyUserRegistry.MyMirrorCache.GetEntryByFunc(u => String.Equals(u.Name, userName, StringComparison.Ordinal));
                    if (tUser != null)
                    {
                        return tUser;
                    }
                }
            }
            {
                if (GetClaimAsString(idTokenClaims, "email", out string email))
                {
                    tUser = MyUserRegistry.MyMirrorCache.GetEntryByFunc(u => String.Equals(u.EMail, email, StringComparison.Ordinal));
                    if (tUser != null)
                    {
                        return tUser;
                    }
                }
            }

#if !CDE_NET35 && !CDE_NET4
            if (idTokenClaims.TryGetValue("role", out dynamic roles))
            {
                if (roles is string)
                {
                    roles = new List<string> { roles as string };
                }
            }

            if (roles != null && bMapUsersByRole)
            {
                // TODO Make this mapping configurable

                foreach (var role in roles)
                {
                    string cdeRole = null;
                    if (role.ToString().EndsWith("Domain Admins"))
                    {
                        cdeRole = "NMIADMIN";
                    }
                    else if (role.ToString().EndsWith("Corp FTEs"))
                    {
                        cdeRole = "MMSUP";
                    }
                    else if (role.ToString().EndsWith("Domain Users"))
                    {
                        cdeRole = "MMOPP";
                    }
                    if (cdeRole != null)
                    {
                        var tUserTemp = TheUserManager.GetUserByRole(cdeRole);
                        if (tUserTemp != null)
                        {
                            if (tUser == null || tUserTemp.AccessMask > tUser.AccessMask)
                            {
                                tUser = tUserTemp;
                            }
                        }
                    }
                }

                if (tUser != null)
                {
                    return tUser;
                }
            }
#endif

            tUser = new TheUserDetails();

            if (!idTokenClaims.ContainsKey("cdeIsPermaUser") || !TheCommonUtils.CBool(idTokenClaims["cdeIsPermaUser"]))
                tUser.IsTempUser = true;

            {
                if (GetClaimAsString(idTokenClaims, "email", out string email))
                {
                    tUser.EMail = email;
                }
                else
                {
                    tUser.EMail = "none";
                }
            }
            if (GetClaimAsString(idTokenClaims, "name", out string name)
                || GetClaimAsString(idTokenClaims, "email", out name)
                || GetClaimAsString(idTokenClaims, "unique_name", out name)
                || GetClaimAsString(idTokenClaims, "upn", out name)
                )
            {
                tUser.Name = name;
            }
            else
            {
                tUser.Name = $"External{tUser.cdeMID}";
            }

#if !CDE_NET35 && !CDE_NET4
            if (roles != null)
            {
                foreach (var role in roles)
                {
                    string roleString = role.ToString();
                    var userRole = MyUserRoles.MyMirrorCache.GetEntryByFunc(r => r.RoleName == roleString);
                    if (userRole == null)
                    {
                        var roleParts = roleString.Split(new char[] { '\\' }, 2);
                        if (roleParts.Length > 1)
                        {
                            userRole = MyUserRoles.MyMirrorCache.GetEntryByFunc(r => r.RoleName == roleParts[1]);
                        }
                    }
                    if (userRole != null)
                    {
                        if (tUser.Roles == null)
                        {
                            tUser.Roles = new List<Guid>();
                        }
                        if (!tUser.Roles.Contains(userRole.cdeMID))
                        {
                            tUser.Roles.Add(userRole.cdeMID);
                        }
                    }
                }
            }

            if (idTokenClaims.TryGetValue("permissions", out dynamic permissions))
            {
                if (permissions is string)
                {
                    permissions = new List<string> { permissions };
                }
                foreach (var permission in permissions)
                {
                    var permissionString = permission as string;
                    if (!String.IsNullOrEmpty(permissionString))
                    {
                        if (tUser.Permissions == null)
                        {
                            tUser.Permissions = new List<string>();
                        }
                        if (!tUser.Permissions.Contains(permissionString))
                        {
                            tUser.Permissions.Add(permissionString);
                        }
                        switch (permissionString)
                        {
                            case "cdeAdmin":
                                tUser.AccessMask |= 0x80;
                                break;
                            case "cdeUser":
                                tUser.AccessMask |= 0x01;
                                break;
                                // TODO: add all other access levels
                        }
                    }
                }
            }
#endif

            if (GetClaimAsString(idTokenClaims, "locale", out string locale))
            {
                try
                {
                    tUser.LCID = new CultureInfo(locale).LCID;
                }
                catch { }
            }

            var existingUser = MyUserRegistry.MyMirrorCache.GetEntryByFunc(u => u.AccessMask == tUser.AccessMask && u.EMail == tUser.EMail && u.Name.StartsWith("External")
               && u.Permissions.SequenceEqual(tUser.Permissions) && u.Roles.SequenceEqual(tUser.Roles) && u.LCID == tUser.LCID && u.IsTempUser == tUser.IsTempUser);

            if (existingUser == null)
            {
                MyUserRegistry.AddAnItem(tUser);
            }
            else
            {
                tUser = existingUser;
            }

            return tUser;
        }

        //static Dictionary<Guid, List<string>> TheExternalPermissionRequests = new Dictionary<Guid, List<string>>();

        class PermissionGrant
        {
            readonly string[] parts;
            public PermissionGrant(string grant)
            {
                parts = grant.Split(new string[] { ".*.", "*.", ".*", "*" }, StringSplitOptions.None);
            }

            public string[] Parts { get { return parts; } }
        }

        private class AccessToken
        {
            public List<PermissionGrant> PermissionGrants { get; set; }
            public string UserName { get; set; }
            public string EMail { get; set; }
            public List<string> Roles { get; set; }
            public DateTimeOffset Expiration { get; set; }
        }

        /// <summary>
        /// Login by username/password
        /// </summary>
        /// <param name="pData"></param>
        /// <param name="pUID"></param>
        /// <param name="pRealPW"></param>
        /// <returns></returns>
        internal static List<TheUserDetails> PerformLogin(TheRequestData pData, string pUID, string pRealPW)
        {
            if (!IsInitialized()) return null;

            return TheBaseAssets.MyApplication.MyUserManager.LoginUser(pData,pUID, pRealPW);
        }

        /// <summary>
        /// Browser or APP Login by RefreshToken
        /// </summary>
        /// <param name="pData">Incoming Web Request with Login</param>
        /// <param name="pToken">Token to login</param>
        /// <returns></returns>
        internal static List<TheUserDetails> PerformLoginByRefreshToken(TheRequestData pData, string pToken)
        {
            if (!IsInitialized()) return null;

            return TheBaseAssets.MyApplication.MyUserManager.LoginByRefreshToken(pData, pToken);
        }
        /// <summary>
        /// Browser or APP Login by EasyScopeID
        /// </summary>
        /// <param name="pData">Incoming Web Request with Login</param>
        /// <param name="pScope">Easy Scope ID to login</param>
        /// <returns></returns>
        internal static TheUserDetails PerformLoginByScope(TheRequestData pData, string pScope)
        {
            if (!IsInitialized()) return null;

            return TheBaseAssets.MyApplication.MyUserManager.LoginUserByScope(pData, pScope);
        }

        internal static List<TheUserDetails> PerformLoginByPin(TheRequestData pData, string pPin)
        {
            if (!IsInitialized()) return null;

            return TheBaseAssets.MyApplication.MyUserManager.LoginUserByPin(pData, pPin);
        }

        internal static TheUserDetails GetUserByID(Guid pCID)
        {
            if (!IsInitialized() || pCID==Guid.Empty) return null;
            return TheBaseAssets.MyApplication.MyUserManager.MyUserRegistry.GetEntryByID(pCID);
        }
        internal static TheUserDetails GetCurrentUser(Guid pSEID)
        {
            TheUserDetails tCurrentUser = null;
            if (pSEID!=Guid.Empty)
            {
                var tReqData = new TheRequestData();
                TheBaseAssets.MySession.GetSessionState(pSEID, tReqData, true);
                if (tReqData.SessionState != null && tReqData.SessionState.CID != Guid.Empty)
                {
                    tCurrentUser = GetUserByID(tReqData.SessionState.CID);
                    if (tCurrentUser!=null)
                        tCurrentUser.CurrentSessionID = pSEID;
                }
            }
            return tCurrentUser;
        }
        internal static TheUserDetails GetUserByRole(string pRole)
        {
            if (!IsInitialized() || string.IsNullOrEmpty(pRole)) return null;
            return GetUserByRole(pRole, true);
        }
        internal static TheUserDetails GetUserByRole(string pRole, bool LocalNodeOnly)
        {
            if (!IsInitialized() || string.IsNullOrEmpty(pRole)) return null;
            return TheBaseAssets.MyApplication.MyUserManager.MyUserRegistry.MyMirrorCache.GetEntryByFunc(
                    s => !string.IsNullOrEmpty(s.PrimaryRole) && pRole.ToUpper().Equals(s.PrimaryRole.ToUpper()) && (!LocalNodeOnly || s.HomeNode==TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID) &&
                    ("*".Equals(s.AssignedEasyScope) || TheBaseAssets.MyScopeManager.IsValidScopeID(s.AssignedEasyScope))); //SECURITY-REVIEW: starting 4.106 AES contains SScopeID
        }
        internal static bool DoesUserExist(string userName)
        {
            if (string.IsNullOrEmpty(userName)) return false;

            var user = TheBaseAssets.MyApplication.MyUserManager.MyUserRegistry.MyMirrorCache.GetEntryByFunc(
                                s => (s.HomeNode == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)
                                && string.Equals(s.EMail, userName, StringComparison.OrdinalIgnoreCase)
                                && ("*".Equals(s.AssignedEasyScope) || TheBaseAssets.MyScopeManager.IsValidScopeID(s.AssignedEasyScope))); //SECURITY-REVIEW: starting 4.106 AES contains SScopeID
            return user != null;
        }
        internal static bool IsUserNameValid(string userName)
        {
            return TheCommonUtils.Check4ValidEmail(userName);
        }
        internal static List<TheUserDetails> GetAllUsers()
        {
            if (!IsInitialized()) return null;
            return TheBaseAssets.MyApplication.MyUserManager.MyUserRegistry.TheValues;
        }
        internal static string GetUserStatus()
        {
            string res="";
            List<TheUserDetails> tL = GetAllUsers();
            foreach (TheUserDetails u in tL)
                res += "<li>" + u + "</li>"; //V7H3FMTW  dx06jp5z
            return res;
        }
        internal static void MergeUsers(List<TheUserDetails> pUsers)
        {
            if (!IsInitialized()) return;
            TheBaseAssets.MyApplication.MyUserManager.DoMergeUsers(pUsers);
        }

        /// <summary>
        /// Returns true if the UserManager is completely initialized and ready to manage user
        /// </summary>
        /// <returns></returns>
        public static bool IsInitialized()
        {
            return TheBaseAssets.MyApplication!=null && TheBaseAssets.MyApplication.MyUserManager!=null && TheBaseAssets.MyApplication.MyUserManager.mIsCompletelyInitialized;
        }


        /// <summary>
        /// Allows to register a new User Role
        /// </summary>
        /// <param name="pRole"></param>
        public static void RegisterNewRole(TheUserRole pRole)
        {
            if (pRole != null)
            {
                if (!listAddRoles.Contains(pRole))
                    listAddRoles.Add(pRole);
            }
            if (TheBaseAssets.MyApplication.MyUserManager != null && TheBaseAssets.MyApplication.MyUserManager.mIsInitialized)
                TheBaseAssets.MyApplication.MyUserManager.AddRegisteredRoles();
        }

        internal static void AddNewUser(TheUserDetails pUser)
        {
            if (pUser != null)
            {
                if (!listAddUsers.Contains(pUser))
                    listAddUsers.Add(pUser);
            }
            if (TheBaseAssets.MyApplication.MyUserManager != null && TheBaseAssets.MyApplication.MyUserManager.mIsInitialized)
                TheBaseAssets.MyApplication.MyUserManager.AddRegisteredUser();
        }
        internal static void AddNewUserByRole(TheUserDetails pUser)
        {
            if (pUser != null)
            {
                if (!listAddUsersByRole.Contains(pUser))
                    listAddUsersByRole.Add(pUser);
            }
            if (TheBaseAssets.MyApplication.MyUserManager != null && TheBaseAssets.MyApplication.MyUserManager.mIsInitialized)
                TheBaseAssets.MyApplication.MyUserManager.AddRegisteredUserByRole();
        }
        internal static void RemoveUser(TheUserDetails pUser)
        {
            if (!TheUserManager.IsInitialized() || pUser==null) return;
            TheBaseAssets.MyApplication.MyUserManager.MyUserRegistry.RemoveAnItem(pUser, null);
        }
        internal static void UpdateUser(TheUserDetails pUser)
        {
            if (!TheUserManager.IsInitialized()) return;
            TheBaseAssets.MyApplication.MyUserManager.UpdateUserRecord(pUser);
            //TheBaseAssets.MyApplication.MyUserManager.MyUserRegistry.UpdateItem(pUser,null);
        }
        internal static int UpdateUserI(TheUserDetails pUser)
        {
            if (!TheUserManager.IsInitialized()) return -1;
            TheUserDetails tOldUser = TheBaseAssets.MyApplication.MyUserManager.MyUserRegistry.GetEntryByID(pUser.cdeMID);
            if (tOldUser != null)
            {
                if (pUser.AccessMask >= 0)
                    tOldUser.AccessMask = pUser.AccessMask;
                if (!string.IsNullOrEmpty(pUser.Name) && pUser.Name != tOldUser.Name)
                    tOldUser.Name = pUser.Name;
                if (!string.IsNullOrEmpty(pUser.Password))  //PW is hash
                    tOldUser.Password = pUser.Password;     //PW is hash
                if (!string.IsNullOrEmpty(pUser.PinHash))  //PW is hash
                    tOldUser.PinHash = pUser.PinHash;     //PW is hash
                if (!string.IsNullOrEmpty(pUser.TeTo))
                    tOldUser.TeTo = pUser.TeTo;
                if (!string.IsNullOrEmpty(pUser.EMail))
                    tOldUser.EMail = pUser.EMail;
                if (!string.IsNullOrEmpty(pUser.HomeScreen))
                    tOldUser.HomeScreen = pUser.HomeScreen;
                if (pUser.LCID != tOldUser.LCID)
                    tOldUser.LCID = pUser.LCID;
                if (pUser.ThemeName != tOldUser.ThemeName)
                    tOldUser.ThemeName = pUser.ThemeName;
                if (pUser.ShowClassic != tOldUser.ShowClassic)
                    tOldUser.ShowClassic = pUser.ShowClassic;
                if (pUser.ShowToolTipsInTable != tOldUser.ShowToolTipsInTable)
                    tOldUser.ShowToolTipsInTable = pUser.ShowToolTipsInTable;
                if (pUser.SpeakToasts != tOldUser.SpeakToasts)
                    tOldUser.SpeakToasts = pUser.SpeakToasts;
                if (pUser.HomeNodeName != tOldUser.HomeNodeName)
                    tOldUser.HomeNodeName = pUser.HomeNodeName;
                tOldUser.SecToken = TheCommonUtils.cdeEncrypt(tOldUser.Password + ";:;" + tOldUser.AssignedEasyScope + ";:;" + tOldUser.AccessMask, TheBaseAssets.MySecrets.GetAI());  //AES OK PW is Hash
                TheBaseAssets.MyApplication.MyUserManager.UpdateUserRecord(tOldUser);
                //TheBaseAssets.MyApplication.MyUserManager.MyUserRegistry.UpdateItem(tOldUser,null);
                return tOldUser.LCID;
            }
            return 0;
        }

        internal static void RemoveUsersByScope(Guid NodeID, string pRealScope)
        {
            if (!TheUserManager.IsInitialized() || string.IsNullOrEmpty(pRealScope)) return;
            List<TheUserDetails> tL = GetAllUsers();
            TheBaseAssets.MySYSLOG.WriteToLog(4312, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheUserManager", $"Remove User By Scope for pScope: {pRealScope.Substring(0, 4).ToUpper()}", eMsgLevel.l3_ImportantMessage));
            foreach (TheUserDetails u in tL)
            {
                string uScope = TheBaseAssets.MyScopeManager.GetRealScopeID(u.AssignedEasyScope);
                if (Guid.Empty == NodeID)
                {
                    if (pRealScope==uScope)     //4.209: less re-encrypting per iteration
                        RemoveUser(u);
                }
                else
                {
                    if (pRealScope == uScope && u.HomeNode == NodeID) //4.209: less re-encrypting per iteration
                        RemoveUser(u);
                }
            }
        }

        private readonly object IsMerging = new object();

        /// <summary>
        /// TODO: Remove users that belong to a Node but have been removed from that node
        /// </summary>
        /// <param name="pUsers"></param>
        private void DoMergeUsers(List<TheUserDetails> pUsers)
        {
            if (mIsInitialized && pUsers != null)
            {
                if (TheCommonUtils.cdeIsLocked(IsMerging)) return;
                lock (IsMerging)
                {
                    try
                    {
                        for (int i = pUsers.Count - 1; i >= 0; i--)
                        {
                            var theUserDetails = pUsers[i];
                            string[] tSec = TheCommonUtils.cdeSplit(TheCommonUtils.cdeDecrypt(theUserDetails.SecToken, TheBaseAssets.MySecrets.GetAI()), ";:;", false, false);
                            if (tSec != null)
                            {
                                if (string.IsNullOrEmpty(tSec[0]) || TheBaseAssets.MySecrets.IsHashLengthCorrect(tSec[0]))
                                    theUserDetails.Password = tSec[0];  //SECURITY: PW is HashPW
                                else
                                    theUserDetails.Password = TheBaseAssets.MySecrets.CreatePasswordHash(tSec[0]);  //SECURITY: Incoming PW is legacy and needs to be converted to hash
                                if (tSec.Length > 1 && tSec[1].Length > 0)
                                {
                                    if (tSec[1] != "*" && tSec[1].Length < 10) //V4.106 backwards compatibility: Old Easy Scope ID found
                                        theUserDetails.AssignedEasyScope = TheBaseAssets.MyScopeManager.GetScrambledScopeIDFromEasyID(tSec[1]);      //pre 4.106 lecacy convert
                                    else
                                        theUserDetails.AssignedEasyScope = tSec[1]; //4.106 AES contains SScopeID
                                }
                                if (tSec.Length > 2) theUserDetails.AccessMask = TheCommonUtils.CInt(tSec[2]);
                            }
                            var tExistingUser = MyUserRegistry.GetEntryByID(theUserDetails.cdeMID);
                            if (tExistingUser == null)
                            {
                                if (theUserDetails.AssignedEasyScope == null)   //AES OK
                                    theUserDetails.AssignedEasyScope = "*";     //AES OK
                                MyUserRegistry.AddAnItem(theUserDetails, null);
                            }
                            else
                            {
                                theUserDetails.RefreshTokens = tExistingUser.RefreshTokens; //New in 4.210: Tokens must survive Merge
                                UpdateUserRecord(theUserDetails); //MyUserRegistry.UpdateItem(pUsers[i],null);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4313, new TSM("TheUserManager", "Error during user merging", eMsgLevel.l1_Error, e.ToString()));
                    }
                }
            }
        }

        private void AddRegisteredRoles()
        {

            if (mIsInitialized)
            {
                while (listAddRoles.Any())
                {
                    var roleToAdd = listAddRoles.Last();
                    if (!MyUserRoles.ContainsID(roleToAdd.cdeMID) && !MyUserRoles.MyMirrorCache.TheValues.Any(s => s.RoleName == roleToAdd.RoleName))
                    {
                        MyUserRoles.AddAnItem(roleToAdd, null);
                    }
                    listAddRoles.Remove(roleToAdd);
                }
            }
        }

        private bool IsAdding;
        private void AddRegisteredUser()
        {

            if (mIsInitialized && !IsAdding)
            {
                IsAdding = true;
                try
                {
                    for (int i = listAddUsers.Count - 1; i >= 0; i--)
                    {
                        var theUserDetails = listAddUsers[i];
                        if (theUserDetails.AssignedEasyScope == null)   //AES OK
                            theUserDetails.AssignedEasyScope = "*";     //AES OK
                        if (!MyUserRegistry.ContainsID(theUserDetails.cdeMID) && !MyUserRegistry.MyMirrorCache.ContainsByFunc(s => s.EMail == theUserDetails.EMail))
                        {
                            theUserDetails.SecToken = TheCommonUtils.cdeEncrypt(theUserDetails.Password + ";:;" + theUserDetails.AssignedEasyScope + ";:;" + theUserDetails.AccessMask, TheBaseAssets.MySecrets.GetAI());  //AES OK PW is hash
                            MyUserRegistry.AddAnItem(theUserDetails, null);
                        }
                        listAddUsers.RemoveAt(i);
                    }
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4314, new TSM("TheUserManager", "Error during AddRegisteredUser", eMsgLevel.l1_Error, e.ToString()));
                }
                IsAdding = false;
            }
        }
        private void AddRegisteredUserByRole()
        {
            if (mIsInitialized && !IsAdding)
            {
                IsAdding = true;
                try
                {
                    for (int i = listAddUsersByRole.Count - 1; i >= 0; i--)
                    {
                        var theUserDetails = listAddUsersByRole[i];
                        if (theUserDetails.AssignedEasyScope == null)       //AES OK
                            theUserDetails.AssignedEasyScope = "*";         //AES OK
                        if (!MyUserRegistry.MyMirrorCache.ContainsByFunc(s =>
                                    (("*".Equals(theUserDetails.AssignedEasyScope) || TheBaseAssets.MyScopeManager.IsValidScopeID(theUserDetails.AssignedEasyScope,s.AssignedEasyScope)) && //4.106 AES contains SScopeID
                                    !string.IsNullOrEmpty(s.PrimaryRole) && s.PrimaryRole.Equals(theUserDetails.PrimaryRole, StringComparison.CurrentCultureIgnoreCase))
                                    || (!string.IsNullOrEmpty(s.EMail) && !string.IsNullOrEmpty(theUserDetails.EMail) && s.EMail == theUserDetails.EMail)
                                    ))
                        {
                            theUserDetails.SecToken = TheCommonUtils.cdeEncrypt(theUserDetails.Password + ";:;" + theUserDetails.AssignedEasyScope + ";:;" + theUserDetails.AccessMask, TheBaseAssets.MySecrets.GetAI()); //AES OK OW is Hash
                            MyUserRegistry.AddAnItem(theUserDetails, null);
                        }
                        listAddUsersByRole.RemoveAt(i);
                    }
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4315, new TSM("TheUserManager", "Error during AddRegisteredUser", eMsgLevel.l1_Error, e.ToString()));
                }
                IsAdding = false;
            }
        }

        private void LoadFromConfig()
        {
            if (!TheBaseAssets.MyServiceHostInfo.IsCloudService)
            {
                if (MyUserRegistry.Count > 0)
                {
                    foreach (TheUserDetails tUser in MyUserRegistry.TheValues)
                    {
                        string[] tSec = TheCommonUtils.cdeSplit(TheCommonUtils.cdeDecrypt(tUser.SecToken, TheBaseAssets.MySecrets.GetAI()), ";:;", false, false);
                        if (tSec != null)
                        {
                            if (string.IsNullOrEmpty(tSec[0]) || TheBaseAssets.MySecrets.IsHashLengthCorrect(tSec[0]))
                                tUser.Password = tSec[0];  //SECURITY: pw is HashPW
                            else
                                tUser.Password = TheBaseAssets.MySecrets.CreatePasswordHash(tSec[0]);  //SECURITY: Incoming PW is legacy and needs to be converted to hash
                            if (tSec.Length > 1 && tSec[1].Length > 0)
                            {
                                if (tSec[1] != "*" && tSec[1].Length < 10) //V4.106 backwards compatibility: Old Easy Scope ID found
                                    tUser.AssignedEasyScope = TheBaseAssets.MyScopeManager.GetScrambledScopeIDFromEasyID(tSec[1]);      //pre 4.106 lecacy convert
                                else
                                    tUser.AssignedEasyScope = tSec[1]; //4.106 AES contains SScopeID
                            }
                            if (tSec.Length > 2) tUser.AccessMask = TheCommonUtils.CInt(tSec[2]);
                        }
                    }
                }
            }
        }

        internal static void RemoveTempUser(Guid pUserID)
        {
            if (TheBaseAssets.MyApplication.MyUserManager != null)
            {
                TheUserDetails pUser = GetUserByID(pUserID);
                if (pUser != null && pUser.IsTempUser)
                    RemoveUser(pUser);
            }
        }

        /// <summary>
        /// Checks if the User attached to a TPM has permission to access a resource and the this request was checked on a cloud node
        /// </summary>
        /// <param name="pMsg"></param>
        /// <param name="pACL"></param>
        /// <param name="SendNACK"></param>
        /// <returns></returns>
        public static bool CloudCheck(TheProcessMessage pMsg, int pACL, bool SendNACK)
        {
            if (TheBaseAssets.MyServiceHostInfo.IsCloudService || !HasUserAccess(pMsg.CurrentUserID, pACL))
            {
                if (SendNACK)
                {
                    TSM tTsm = Engines.NMIService.TheNMIEngine.LocNMI(TheCommonUtils.GetLCID(pMsg), new TSM(eEngineName.NMIService, "NMI_ERROR", "###Access Denied!###"));
                    TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                }
                return false;
            }
            return true;
        }

        internal static bool CloudCheck(TheProcessMessage pMsg, int pACL, bool SendNACK, TheClientInfo pInfo)
        {
            if (TheBaseAssets.MyServiceHostInfo.IsCloudService || !HasUserAccess(pMsg.CurrentUserID, pACL))
            {
                if (SendNACK)
                {
                    TSM tTsm = Engines.NMIService.TheNMIEngine.LocNMI(pInfo.LCID, new TSM(eEngineName.NMIService, "NMI_ERROR", "###Access Denied!###"));
                    TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                }
                return false;
            }
            return true;
        }
        /// <summary>
        /// Returns true if the current user has the given role (VIEWER ONLY)
        /// </summary>
        /// <param name="pRole">Role Name to check for</param>
        /// <returns></returns>
        public static bool IsCurrentUserRole(string pRole)
        {
            return (TheBaseAssets.MyServiceHostInfo.IsViewer && (TheBaseAssets.MyApplication.MyUserManager != null &&
                TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser != null &&
                TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser.PrimaryRole != null &&
                TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser.PrimaryRole.Equals(pRole)));
        }

        /// <summary>
        /// Returns the Primary Role of the current logged on user
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentUserRole()
        {
            if (TheBaseAssets.MyServiceHostInfo.IsViewer && (TheBaseAssets.MyApplication.MyUserManager != null &&
                TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser != null))
                return TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser.PrimaryRole;
            else
                return "";
        }

        internal static string GetCurrentUserName()
        {
            if (TheBaseAssets.MyServiceHostInfo.IsViewer && (TheBaseAssets.MyApplication.MyUserManager != null && TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser != null))
                return TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser.UserName;
            else
                return "";
        }

        #endregion

        #region Local Storage for User

        /// <summary>
        /// Sends The user List to only a first node connection browser on premise - NOT via the cloud - must not be encrypted as Browser cannot decrypt
        /// </summary>
        /// <param name="pUserID"></param>
        /// <returns></returns>
        internal static string SendUserList(Guid pUserID)
        {
            string ret = "";
            try
            {
                TheUserDetails tCurrentUser = GetUserByID(pUserID);
                if (tCurrentUser == null) return ret;
                List<TheUserDetails> tList = GetAllUsers();
                tList = tList.Where(s => !string.IsNullOrEmpty(s.AssignedEasyScope) &&
                    ("*".Equals(s.AssignedEasyScope) || TheBaseAssets.MyScopeManager.IsValidScopeID(s.AssignedEasyScope, tCurrentUser.AssignedEasyScope))).ToList(); //Starting 4.106 AES contains SScopeID
                ret = TheCommonUtils.SerializeObjectToJSONString(tList);
            }
            catch (Exception e)
            {
                ret = "Getting User List: Exception occured:";
                TheBaseAssets.MySYSLOG.WriteToLog(4316, new TSM("TheUserManager", ret, eMsgLevel.l1_Error, e.ToString()));
            }
            return ret;
        }

        /// <summary>
        /// Resets the users Scopes to the Current Active Scope
        /// </summary>
        /// <param name="PushToTrustedNodes">If true changes will be published to the cloud as well</param>
        public static void ResetUserScopes(bool PushToTrustedNodes)
        {
            ResetUserScopes(true, PushToTrustedNodes);
        }
        private readonly static object lockResetUserScopes = new object();
        internal static void ResetUserScopes(bool SetCurrent, bool PushToTrustedNodes)
        {
            if (!IsInitialized() || TheCommonUtils.cdeIsLocked(lockResetUserScopes)) return;
            lock (lockResetUserScopes)
            {
                string tScope = TheBaseAssets.MyScopeManager.GetScrambledScopeID(); //starting 4.106 AES contains SScopeID no longer Easy Scope ID = TheBaseAssets.MyScopeManager.GetCurrentEasyScope();
                if (string.IsNullOrEmpty(tScope))
                    tScope = "*";
                List<TheUserDetails> tL = GetAllUsers();
                if (tL == null) return;
                if (!string.IsNullOrEmpty(tScope) && !tScope.Equals("*"))   //Only fixup Scopes if the Node is scoped (i.e. not on unscoped clouds)
                {
                    bool WriteUserFile = false;
                    foreach (TheUserDetails u in tL)
                    {
                        if (SetCurrent)
                        {
                            if (string.IsNullOrEmpty(u.AssignedEasyScope) || "*".Equals(u.AssignedEasyScope))   //AES OK
                            {
                                u.AssignedEasyScope = tScope;   //AES OK
                                WriteUserFile = true;
                            }
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(u.AssignedEasyScope) || TheBaseAssets.MyScopeManager.IsValidScopeID(u.AssignedEasyScope,tScope))  //starting 4.106 AES contains SScopeID
                                u.AssignedEasyScope = "*";  //AES OK
                        }
                        u.SecToken = TheCommonUtils.cdeEncrypt(u.Password + ";:;" + u.AssignedEasyScope + ";:;" + u.AccessMask, TheBaseAssets.MySecrets.GetAI()); //starting 4.106 AES contains SScopeID ; PW contains hash
                    }
                    if (WriteUserFile)
                        TheBaseAssets.MyApplication.MyUserManager.MyUserRegistry.MyMirrorCache.SaveCacheToDisk(true, false);
                }
                if (PushToTrustedNodes)
                    SendSyncUser(tL);
            }
        }

        // ReSharper disable once UnusedMethodReturnValue.Local
        private static bool SendSyncUser(List<TheUserDetails> tL)
        {
            if (TheBaseAssets.MyServiceHostInfo.IsCloudNMIBlocked && !TheBaseAssets.MySettings.HasTrustedNodes)
                return false;
            List<Guid> tRouts = TheQueuedSenderRegistry.GetSendersBySenderType(cdeSenderType.CDE_CLOUDROUTE);
            if (TheBaseAssets.MyServiceHostInfo.IsCloudService && !TheBaseAssets.MyServiceHostInfo.CloudToCloudUpstreamOnly && TheBaseAssets.MyServiceHostInfo.AllowedUnscopedNodes.Count > 0)
                tRouts.AddRange(TheBaseAssets.MyServiceHostInfo.AllowedUnscopedNodes);
            if (tRouts == null || tRouts.Count == 0)
                return false;
            for (int i = tL.Count - 1; i >= 0; i--)
            {
                if (tL[i].NodeScope != "ALL" || string.IsNullOrEmpty(tL[i].EMail) || !TheBaseAssets.MySecrets.IsValidPassword(tL[i].Password))    //CM: Never sync users with invalid Password
                    tL.RemoveAt(i);
            }
            if (tL.Count == 0) return false;
            TSM uSync = new TSM(eEngineName.ContentService, "CDE_SYNCUSER", TheCommonUtils.SerializeObjectToJSONString(tL));
            uSync.SetNoDuplicates(true);
            uSync.EncryptPLS();
            TSM tBlockSync = null;
            if (!TheBaseAssets.MyServiceHostInfo.IsCloudNMIBlocked || TheBaseAssets.MySettings.HasTrustedNodes)
            {
                var tBlocks = Engines.NMIService.TheNMIEngine.GetBlocksByType(new Guid("{8C8E1291-3C50-4855-A8C9-27EC203C1F0E}"), Guid.Empty, TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID);
                if (tBlocks?.Count > 0)
                {
                    tBlockSync = new TSM(eEngineName.NMIService, "NMI_SYNCBLOCKS")
                    {
                        PLS = TheCommonUtils.SerializeObjectToJSONString(tBlocks)
                    };
                    tBlockSync.SetNoDuplicates(true);
                    tBlockSync.EncryptPLS();
                }
            }
            if (!TheBaseAssets.MyServiceHostInfo.IsCloudNMIBlocked)
            {

                foreach (Guid tRoute in tRouts)
                {
                    var tToSend = TSM.Clone(uSync, true);
                    tToSend.TXT += $":{tRoute}";
                    TheCommCore.PublishToNode(tRoute, "", tToSend);
                    if (tBlockSync!=null)
                    {
                        var tToSendBl = TSM.Clone(tBlockSync, true);
                        tToSendBl.TXT += $":{tRoute}";
                        TheCommCore.PublishToNode(tRoute, "", tToSendBl);
                    }
                }
            }
            if (TheBaseAssets.MySettings.HasTrustedNodes)
            {
                foreach (string tN in TheCDESettings.MyTrustedNodes)
                {
                    if (TheCommonUtils.CGuid(tN) != TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)
                    {
                        var tToSend = TSM.Clone(uSync, true);
                        tToSend.TXT += $":{tN}";
                        TheCommCore.PublishToNode(TheCommonUtils.CGuid(tN), tToSend);
                        if (tBlockSync != null)
                        {
                            var tToSendBl = TSM.Clone(tBlockSync, true);
                            tToSendBl.TXT += $":{tN}";
                            TheCommCore.PublishToNode(TheCommonUtils.CGuid(tN), "", tToSendBl);
                        }
                    }
                }
            }

            return true;
        }

        private void sinkUpdateUserCycle2(ICDEThing pThing, TheChannelInfo pNotUsed)
        {
            ResetUserScopes(true, true);
        }
        private void sinkUpdateUserCycle(long timer)
        {
            if ((timer % TheBaseAssets.MyServiceHostInfo.TO.UserCheckRate) == 0)  //
                ResetUserScopes(true, true);
        }
#endregion
    }
}
