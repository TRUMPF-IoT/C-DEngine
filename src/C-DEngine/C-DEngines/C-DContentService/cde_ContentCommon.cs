// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Activation;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ISM;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
// ReSharper disable MergeSequentialChecks
// ReSharper disable UseNullPropagation

//ERROR Range: 420- 429

namespace nsCDEngine.Engines.ContentService
{
    /// <summary>
    /// Main Content Communication Engine. Is always present on all nodes
    /// </summary>
    public class TheContentServiceEngine : ThePluginBase
    {
        #region ICDEPlugin Interfaces
        /// <summary>
        /// Mandatory call to set the base engine parameter
        /// </summary>
        /// <param name="pBase">Interface to the MyBaseEngine</param>
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
            MyBaseEngine.SetEngineName(eEngineName.ContentService);
            MyBaseEngine.SetEngineService(!TheCommonUtils.IsHostADevice());
            MyBaseEngine.SetFriendlyName("Content Relay Service");
            MyBaseEngine.SetEngineID(new Guid("{A5FD8A57-C4B9-4EDB-8965-082D3F466E31}"));
            MyBaseEngine.AddCapability(eThingCaps.Internal);

            MyBaseEngine.GetEngineState().IsAcceptingFilePush = true;
            MyBaseEngine.GetEngineState().IsAllowedUnscopedProcessing = TheBaseAssets.MyServiceHostInfo.IsCloudService;
            MyBaseEngine.SetMultiChannel(!TheBaseAssets.MyServiceHostInfo.IsConnectedToCloud);

            MyBaseEngine.SetVersion(TheBaseAssets.BuildVersion);
        }
        #endregion

        /// <summary>
        /// Will be called by TheThingRegistry to initialize the ContentService
        /// </summary>
        /// <returns></returns>
        public override bool Init()
        {
            if (mIsInitCalled) return false;

            mIsInitCalled = true;
            MyBaseThing.RegisterEvent(eEngineEvents.EngineHasStarted, sinkEngineHasStarted);
            MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);
            var updateFrequency = TheCommonUtils.CInt(TheBaseAssets.MySettings.GetSetting("ISMUpdateFrequency"));
            if (updateFrequency == 0)
            {
                updateFrequency = (int)TheCommonUtils.GetRandomUInt(30, 60);
            }
            UpdateFrequency = updateFrequency;
            TheThing.SetSafePropertyNumber(MyBaseThing, "UpdateFrequency", UpdateFrequency);
            mIsInitialized = true;
            if (!TheBaseAssets.MyServiceHostInfo.IsIsolated && TheBaseAssets.MyServiceHostInfo.AllowAutoUpdate)
                TheQueuedSenderRegistry.RegisterHealthTimer(CheckUpdatesTimer);
            MyBaseEngine.SetInitialized(null);
            return true;
        }

        void CheckUpdatesTimer(long timer)
        {
            //if ((timer%21600)!=0) return;
            if ((timer % UpdateFrequency) != 0) return;
            TheBaseAssets.MyApplication?.MyISMRoot?.RequestUpdates();
        }

        private void sinkEngineHasStarted(ICDEThing sender, object NOP)
        {
            MyBaseEngine.ProcessInitialized();
        }

        /// <summary>
        /// Sends a registration request to all known nodes
        /// </summary>
        /// <param name="pCallback">Will be called once the registration was successful</param>
        public void RegisterWithServiceNodes(Action<string, TheUPnPDeviceInfo> pCallback)
        {
            string pMagix = TheTimedCallbacks<TheUPnPDeviceInfo>.AddTimedRequest(pCallback, TheBaseAssets.MyServiceHostInfo.TO.DeviceCleanSweepTimeout, null);
            TheCDEngines.MyContentEngine.GetBaseEngine().PublishToChannels(new TSM(eEngineName.ContentService, "CDE_DEVICEREG:" + pMagix, TheCommonUtils.SerializeObjectToJSONString(TheBaseAssets.MyApplication.MyCommonDisco.GetTheUPnPDeviceInfo())), null);
        }

        private DateTimeOffset LastServiceInfo = DateTimeOffset.MinValue;
        private StringBuilder LastSentServiceInfo = null;
        private StringBuilder LastSentSysLog = null;
        private bool IsCollectingState = false;
        private int UpdateFrequency = 60;

        /// <summary>
        /// Called by the Communication framework if a message was received for the ContentService
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="pIncoming"></param>
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            TheProcessMessage pMsg = pIncoming as TheProcessMessage;
            if (pMsg?.Message?.TXT == null) return;
            string[] Command = pMsg.Message.TXT.Split(':');
            switch (Command[0])
            {
                case "CDE_REGISTERRULE":
                case "CDE_REGISTERPROPERTY":
                case "CDE_REGISTERTHING":
                case "CDE_UNREGISTERTHING":
                case "CDE_SYNC_THINGS":
                case "CDE_SETP":
                case "CDE_SEND_GLOBAL_THINGS":
                    if (!TheBaseAssets.MyServiceHostInfo.IsCloudService || TheBaseAssets.MyScopeManager.IsScopingEnabled) //Just to be sure..this should never come here anyway EVER!
                        TheCDEngines.MyThingEngine.HandleMessage(sender, pIncoming);
                    else
                        TheBaseAssets.MySYSLOG.WriteToLog(4013, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, $"Illegal TSM {Command[0]} received from {pMsg.Message.GetOriginator()} will be ignored ({pMsg.Message.FLG})", eMsgLevel.l2_Warning));
                    break;
                case "CDE_GET_ENGINEJS":    //New 4.107: Allow for custom JS NMI engines without our NMI Runtime
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
                                            TSM tTsm = new TSM(eEngineName.ContentService, "CDE_ENGINEJS:" + teng) { PLS = TheCommonUtils.CArray2UTF8String(astream) };
                                            TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                        }
                                    }
                                    if (!string.IsNullOrEmpty(tBase.GetEngineState().CSS))
                                    {
                                        TSM tTsm = new TSM(eEngineName.ContentService, "CDE_CUSTOM_CSS", tBase.GetEngineState().CSS);
                                        TheCommCore.PublishToOriginator(pMsg.Message, tTsm);
                                    }
                                }
                            }
                        }
                    }
                    break;
                case "CDE_INITIALIZED":
                    MyBaseEngine.SetInitialized(pMsg.Message);
                    break;
                case "CDE_INITIALIZE":
                    if (MyBaseEngine.GetEngineState().IsService)
                    {
                        if (!MyBaseEngine.GetEngineState().IsEngineReady)
                            MyBaseEngine.SetEngineReadiness(true, null);
                        MyBaseEngine.ReplyInitialized(pMsg.Message);
                    }
                    break;
                case "CDE_GET_NODETOPICS":
                    {
                        var tL = TheQueuedSenderRegistry.GetNodeTopics(true);
                        var responseMsg = new TSM(eEngineName.ContentService, "CDE_NODETOPICS", TheCommonUtils.SerializeObjectToJSONString(tL));
                        if (pMsg.LocalCallback != null)
                        {
                            pMsg.LocalCallback(responseMsg);
                        }
                        else
                        {
                            TheCommCore.PublishToOriginator(pMsg.Message, responseMsg, true);
                        }
                    }
                    break;
                case "CDE_GET_SYSLOG":
                    TheBaseAssets.MySYSLOG.WriteToLog(4011, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, $"received Get_Syslog from : {pMsg.Message.GetOriginator()}", eMsgLevel.l3_ImportantMessage));
                    try
                    {
                        if (!IsCollectingState && (!TheBaseAssets.MyServiceHostInfo.IsCloudService || DateTimeOffset.Now.Subtract(LastServiceInfo).TotalSeconds > 30 || (Command.Length > 1 && Command[1] == "FORCE")))
                        {
                            IsCollectingState = true;
                            try
                            {
                                LastServiceInfo = DateTimeOffset.Now;
                                LastSentSysLog =new StringBuilder(TheCommonUtils.SerializeObjectToJSONStringM(TheBaseAssets.MySYSLOG.MyMessageLog.TheValues));
                            }
                            catch (Exception)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(4012, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, $"Error during collection of node syslog", eMsgLevel.l1_Error));
                            }
                            finally
                            {
                                IsCollectingState = false;
                            }
                        }
                        if (LastSentSysLog != null)
                        {
                            var responseMsg = new TSM(eEngineName.ContentService, "CDE_SYSLOG", LastSentSysLog.ToString());
                            if (pMsg.LocalCallback != null)
                            {
                                pMsg.LocalCallback(responseMsg);
                            }
                            else
                            {
                                TheCommCore.PublishToOriginator(pMsg.Message, responseMsg, true);
                            }
                            if (TheBaseAssets.MyServiceHostInfo.IsMemoryOptimized)
                                LastSentSysLog = null;
                        }
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4013, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ContentService, $"FAILED to respond to Get_Syslog from : {pMsg.Message.GetOriginator()}", eMsgLevel.l1_Error, e.ToString()));
                    }
                    break;
                case "CDE_GET_SERVICEINFO":
                    TheBaseAssets.MySYSLOG.WriteToLog(4011, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, $"received Get_ServiceInfo from : {pMsg.Message.GetOriginator()}", eMsgLevel.l3_ImportantMessage));
                    try
                    {
                        if (!IsCollectingState && (!TheBaseAssets.MyServiceHostInfo.IsCloudService || DateTimeOffset.Now.Subtract(LastServiceInfo).TotalSeconds > 30 || (Command.Length > 1 && Command[1] == "FORCE")))
                        {
                            IsCollectingState = true;
                            try
                            {
                                LastServiceInfo = DateTimeOffset.Now;
                                TheBaseAssets.MyServiceHostInfo.ConnToken = Guid.NewGuid();
                                TheNodeInfo tState = new TheNodeInfo
                                {
                                    MyNodeHeader = cdeStatus.GetInfoHeaderJSON(),
                                    MyServiceInfo = TheBaseAssets.MyServiceHostInfo,
                                    MyNodeID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                                    ActiveLicense = TheBaseAssets.MyActivationManager.GetActivatedLicenses(new Guid("5737240C-AA66-417C-9B66-3919E18F9F4A")).Select(l => l.License.Description).Aggregate("", (s, d) => $"{s} {d}").Trim(' ')
                                };

                                if (!string.IsNullOrEmpty(pMsg.Message.PLS))
                                {
                                    if (pMsg.Message.PLS.Contains("CHNL"))
                                    {
                                        if (cdeStatus.GetSubscriptionStatusWithPublicChannelInfo(out Dictionary<string, List<Guid>> channelsByTopic, out List<TheChannelInfo> channels))
                                        {
                                            tState.ChannelsByTopic = channelsByTopic;
                                            tState.Channels = channels;
                                        }
                                    }
                                    cdeStatus.TheCdeStatusOptions statusOptions = new cdeStatus.TheCdeStatusOptions();
                                    statusOptions.ShowSysLog = pMsg.Message.PLS.Contains("SYSLOG");
                                    statusOptions.ShowQueueContent = pMsg.Message.PLS.Contains("QUEUE");
                                    statusOptions.ShowDetails = pMsg.Message.PLS.Contains("SUBSUM");
                                    statusOptions.ShowManyDetails = pMsg.Message.PLS.Contains("SUBDET");
                                    statusOptions.ShowHSI = pMsg.Message.PLS.Contains("HSI");
                                    statusOptions.ShowDiag = pMsg.Message.PLS.Contains("DIAG");
                                    statusOptions.ShowSesLog = pMsg.Message.PLS.Contains("SESLOG");

                                    if (statusOptions.ShowSysLog)
                                        tState.LastHTMLLog = TheBaseAssets.MySYSLOG.GetNodeLog(null, "", false);

                                    tState.LastHTMLLog += cdeStatus.ShowSubscriptionsStatus(false, statusOptions);
                                    if (statusOptions.ShowHSI)
                                        tState.LastHTMLLog += cdeStatus.RenderHostServiceInfo(false);
                                    if (statusOptions.ShowDiag)
                                        tState.LastHTMLLog += cdeStatus.GetDiagReport(false);
                                    if (statusOptions.ShowSesLog)
                                        tState.LastHTMLLog += TheBaseAssets.MySession.GetSessionLog();
                                    if (pMsg.Message.PLS.Contains("KPIS"))
                                    {
                                        tState.MyCDEKpis = TheCDEKPIs.GetDictionary();
                                    }
                                }
                                List<IBaseEngine> tList3 = TheThingRegistry.GetBaseEngines(false);
                                foreach (IBaseEngine tBase in tList3)
                                {
                                    tBase.UpdateEngineState(pMsg.Message.SID);
                                    tState.MyServices.Add(tBase.GetEngineState());
                                }
                                TheBaseAssets.MySYSLOG.WriteToLog(4012, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, $"sending Get_ServiceInfo back to: {pMsg.Message.GetOriginator()}", eMsgLevel.l3_ImportantMessage));
                                LastSentServiceInfo =new StringBuilder(TheCommonUtils.SerializeObjectToJSONStringM(tState));
                            }
                            catch (Exception)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(4012, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, $"Error during collection of node state", eMsgLevel.l1_Error));
                            }
                            finally
                            {
                                IsCollectingState = false;
                            }
                        }
                        if (LastSentServiceInfo != null)
                        {
                            var responseMsg = new TSM(eEngineName.ContentService, "CDE_SERVICE_INFO", LastSentServiceInfo.ToString());
                            if (pMsg.LocalCallback != null)
                            {
                                pMsg.LocalCallback(responseMsg);
                            }
                            else
                            {
                                TheCommCore.PublishToOriginator(pMsg.Message, responseMsg, true);
                            }
                            if (TheBaseAssets.MyServiceHostInfo.IsMemoryOptimized)
                                LastSentServiceInfo = null;
                        }
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4013, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ContentService, $"FAILED to respond to Get_ServiceInfo from : {pMsg.Message.GetOriginator()}", eMsgLevel.l1_Error, e.ToString()));
                    }
                    return;
                case "CDE_GETBLOB":
                    if (Command.Length == 1) return;
                    ProcessBlobRequest(pMsg.Message, pMsg.LocalCallback, false);
                    break;
                case "WEBCOMMANDRELAY": //SECURITY: This lets any node in the mesh do probing to arbitrary URLs from this node! Should this be opt-in/off by default? 4.108: Done and Moved to Content service for stricter evaluation
                    if (pMsg.Message.PLS.Length > 0 && TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("AllowWebCommandRelay")))
                        TheREST.GetRESTAsync(new Uri(pMsg.Message.PLS), null);
                    break;
                case "CDE_BLOBREQUESTED":
                    if (Command.Length > 1)
                    {
                        var tCacheName = Command[1] + (string.IsNullOrEmpty(pMsg.Message.SID) ? "" : TheBaseAssets.MyScopeManager.GetRealScopeID(pMsg.Message.SID));    //GRSI: rare
                        TheBlobData tCall = TheBaseAssets.MyBlobCache.GetEntryByID(tCacheName);
                        if (tCall != null && tCall.BlobAction != null)
                            tCall.BlobAction(pMsg.Message);
                        else
                        {
                            if (TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("AllowUnscopedResources")))
                            {
                                tCacheName = Command[1];
                                tCall = TheBaseAssets.MyBlobCache.GetEntryByID(tCacheName);
                                if (tCall != null && tCall.BlobAction != null)
                                {
                                    pMsg.Message.SID = "";
                                    tCall.BlobAction(pMsg.Message);
                                    break;
                                }
                            }
                            TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, string.Format("Blob Received but No longer in BlobCache. Name:{0} Size:{1}", Command[1], pMsg.Message.PLB.Length), eMsgLevel.l2_Warning));
                        }
                    }
                    break;
                case "CDE_RHIB4":
                    TheCommCore.MyHttpService.RegisterHttpInterceptorB4($"{pMsg.Message.PLS}:;:{pMsg.Message.GetOriginator()}", null);
                    break;
                case "CDE_RHIA":
                    TheCommCore.MyHttpService.RegisterHttpInterceptorAfter($"{pMsg.Message.PLS}:;:{pMsg.Message.GetOriginator()}", null);
                    break;
                case "CDE_URHIB4":
                    TheCommCore.MyHttpService.UnregisterHttpInterceptorB4($"{pMsg.Message.PLS}:;:{pMsg.Message.GetOriginator()}");
                    break;
                case "CDE_URHIA":
                    TheCommCore.MyHttpService.UnregisterHttpInterceptorAfter($"{pMsg.Message.PLS}:;:{pMsg.Message.GetOriginator()}");
                    break;
                case "CDE_RGSI":
                    TheCommCore.MyHttpService.RegisterGlobalScriptInterceptor($"{pMsg.Message.PLS}:;:{pMsg.Message.GetOriginator()}", null);
                    break;
                case "CDE_UPD_ADMIN":
                    if (TheBaseAssets.MyServiceHostInfo.IsCloudService || !pMsg.Message.IsFirstNode() || Command.Length < 2 || !TheUserManager.DoesAdminRequirePWD(Command[1]))
                        return;
                    TheUserDetails tAdmin = TheUserManager.GetUserByRole(Command[1]);
                    TSM aTSM = new TSM(eEngineName.NMIService, "NMI_RESET");
                    if (tAdmin != null && string.IsNullOrEmpty(tAdmin.Password))
                    {
                        string tCreds = pMsg.Message.PLS;
                        if (!TheBaseAssets.MyServiceHostInfo.DisableRSAToBrowser)
                            tCreds = TheCommonUtils.cdeRSADecrypt(TheCommonUtils.CGuid(pMsg.Message.SEID), tCreds);
                        string[] tCre = TheCommonUtils.cdeSplit(tCreds, ";:;", true, false);
                        if (tCre.Length > 1 && TheBaseAssets.MySecrets.IsValidPassword(tCre[1]))
                        {
                            if (TheUserManager.DoesUserExist(tCre[0]))
                            {
                                aTSM.TXT += ":FAILED";
                                aTSM.PLS = "Credentials already in use!";
                            }
                            else if (!TheUserManager.IsUserNameValid(tCre[0]))
                            {
                                aTSM.TXT += ":FAILED";
                                aTSM.PLS = "Invalid EMail!";
                            }
                            else
                            {
                                tAdmin.EMail = tCre[0];
                                tAdmin.Password = TheBaseAssets.MySecrets.CreatePasswordHash(tCre[1]);  //PW-SECURITY-REVIEW: create hash here
                                tAdmin.SecToken = TheCommonUtils.cdeEncrypt(tAdmin.Password + ";:;" + tAdmin.AssignedEasyScope + ";:;" + tAdmin.AccessMask, TheBaseAssets.MySecrets.GetAI());     //3.083: Must be cdeAI // AES OK ; PW hashed
                                TheUserManager.UpdateUser(tAdmin);
                                TheCommCore.PublishToOriginator(pMsg.Message, aTSM);
                                if (!TheBaseAssets.MyScopeManager.IsScopingEnabled && TheBaseAssets.MyServiceHostInfo.AllowSetScopeWithSetAdmin && tCre.Length > 2)
                                    TheBaseAssets.MyScopeManager.SetScopeIDFromEasyID(tCre[2]);
                                return;
                            }
                        }
                        else
                        {
                            aTSM.TXT += ":FAILED";
                            aTSM.PLS = "Credentials not valid!";
                        }
                    }
                    else
                    {
                        aTSM.TXT += ":FAILED";
                        aTSM.PLS = "Administrator not found!";
                    }
                    TheCommCore.PublishToOriginator(pMsg.Message, aTSM);
                    break;
                case "CDE_REQ_SCOPEID":
                    if (TheBaseAssets.MyServiceHostInfo.IsCloudService || !pMsg.Message.IsFirstNode() || !TheUserManager.HasUserAccess(pMsg.CurrentUserID, 128)) return;
                    {
                        TheUserManager.ResetUserScopes(false, false);
                        string tScope = null;
                        if (Command.Length > 2 && Command[1] == "CERTTHUMB")
                            tScope = TheCertificates.SetClientCertificate(Command[2]);
                        if (tScope==null && TheBaseAssets.MyServiceHostInfo.ClientCerts?.Count > 0)
                        {
                            string error = "";
                            var tScopes = TheCertificates.GetScopesFromCertificate(TheBaseAssets.MyServiceHostInfo.ClientCerts[0], ref error);
                            if (tScopes?.Count > 0)
                            {
                                tScope=tScopes[0];  //if we have found a scope ID in the cert...use it instead of what the NMI wants or random
                                TheBaseAssets.MySYSLOG.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ContentService, $"Scope {tScopes[0]} found in Certificate and used for node", eMsgLevel.l3_ImportantMessage));
                            }
                        }
                        if (string.IsNullOrEmpty(tScope))
                        {
                            if (!string.IsNullOrEmpty(pMsg.Message.PLS))
                            {
                                tScope = pMsg.Message.PLS;
                                tScope = TheCommonUtils.cdeRSADecrypt(TheCommonUtils.CGuid(pMsg.Message.SEID), tScope);
                            }
                            if (string.IsNullOrEmpty(tScope))
                                tScope = TheBaseAssets.MyScopeManager.CalculateRegisterCode(TheBaseAssets.MyScopeManager.GenerateNewAppDeviceID(cdeSenderType.CDE_SERVICE));
                        }
                        bool StartISOs = TheBaseAssets.MyServiceHostInfo.RequiresConfiguration;
                        TheBaseAssets.MyScopeManager.SetScopeIDFromEasyID(tScope);
                        TheBaseAssets.MyServiceHostInfo.RequiresConfiguration = false;
                        TheBaseAssets.MySettings.UpdateLocalSettings();

                        TheBaseAssets.MySYSLOG.WriteToLog(2352, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, "CDE_REQ_SCOPEID: Received", eMsgLevel.l3_ImportantMessage));

                        TheRequestData tReqData = new TheRequestData();
                        TheBaseAssets.MySession.GetSessionState(TheCommonUtils.CGuid(pMsg.Message.SEID), tReqData, true);
                        tReqData.SessionState.SScopeID = TheBaseAssets.MyScopeManager.GetScrambledScopeID();     //GRSI: rare
                        TheQueuedSender tQ = TheQueuedSenderRegistry.GetSenderByGuid(pMsg.Message.GetOriginator());
                        if (tQ != null && tQ.MyTargetNodeChannel != null && tQ.MyTargetNodeChannel.SenderType == cdeSenderType.CDE_JAVAJASON)
                            tQ.UpdateSubscriptionScope(TheBaseAssets.MyScopeManager.ScopeID);
                        TSM tTsm = new TSM(eEngineName.NMIService, "NMI_SCOPID", tScope);
                        TheCommCore.PublishToNode(pMsg.Message.GetOriginator(), tTsm);  //Cannot use publish to originator as the SID in the Source TSM is no longer valid
                        if (StartISOs)
                            TheCDEngines.StartIsolatedPlugins();
                    }
                    break;
                case "CDE_MESHSELECT":
                    //Login to a mesh - coming soon
                    {
                        string tLogRes = "ERR:CDE_MESHSELECT_FAILURE";
                        if (TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper && pMsg.Message != null && !string.IsNullOrEmpty(pMsg.Message.SEID) && Command.Length>1)
                        {
                            TheRequestData pRequestData = new TheRequestData { SessionState = TheBaseAssets.MySession.ValidateSEID(TheCommonUtils.CGuid(pMsg.Message.SEID)) };    //Low frequency
                            Guid MesID = TheCommonUtils.CGuid(Command[1]);
                            if (MesID != Guid.Empty && pRequestData?.SessionState?.Meshes?.Count > 0)
                            {
                                TheMeshPicker tPic = pRequestData?.SessionState?.Meshes.Find(s => s.cdeMID == MesID);
                                if (tPic != null)
                                {
                                    var tUser = TheUserManager.GetUserByID(tPic.UserID);
                                    if (tUser != null)
                                    {
                                        TheUserManager.SetUserInSessionState(pRequestData, tUser);
                                        tLogRes = string.Format("LOGIN_SUCCESS:{0}:{1}:{2}", TheUserManager.GetUserHomeScreen(pRequestData, tUser), tUser.Name, tUser.GetUserPrefString());
                                    }
                                }
                            }
                            pRequestData.SessionState.Meshes = null;
                        }
                        if (tLogRes.StartsWith("ERR"))
                        {
                            TheCDEKPIs.IncrementKPI(eKPINames.BruteDelay);
                            //Security Fix: ID#770 - wait 200 ms before returning anything with error code to limit BruteForce
                            TheCommonUtils.SleepOneEye(200, 100);
                        }
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.ContentService, tLogRes));
                    }
                    break;
                case "CDE_LOGIN":
                case "CDE_SLOGIN":
                case "CDE_TLOGIN":
                    {
                        string tLogRes = "ERR:CDE_LOGIN_FAILURE";
                        byte[] tPLB = null;
                        if (TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper && pMsg.Message != null && !string.IsNullOrEmpty(pMsg.Message.SEID))
                        {
                            if (pMsg.Message.PLB != null)
                            {
                                TheRequestData pRequestData = new TheRequestData { SessionState = TheBaseAssets.MySession.ValidateSEID(TheCommonUtils.CGuid(pMsg.Message.SEID)) };    //Low frequency
                                if (pRequestData.SessionState != null)
                                {
                                    try
                                    {
                                        string tscope = null;
                                        int IsTLOGIN = Command[0].StartsWith("CDE_TLOGIN") ? 1 : 0;
                                        if (Command[0].StartsWith("CDE_SLOGIN"))
                                            tscope = TheCommonUtils.Decrypt(pMsg.Message.PLB, TheBaseAssets.MySecrets.GetAI());
                                        else
                                        {
                                            if (TheBaseAssets.MyServiceHostInfo.DisableRSAToBrowser || IsTLOGIN == 1)
                                                tscope = TheCommonUtils.CArray2UTF8String(pMsg.Message.PLB);
                                            else
                                                tscope = TheCommonUtils.cdeRSADecrypt(pRequestData.SessionState.cdeMID, pMsg.Message.PLB);
                                        }
                                        string[] tCreds = TheCommonUtils.cdeSplit(tscope, ":;:", true, true);
                                        if (tCreds.Length > (1 - IsTLOGIN))
                                        {
                                            List<TheUserDetails> tUsers = IsTLOGIN == 0 ?
                                                                            TheUserManager.PerformLogin(pRequestData, tCreds[0], tCreds[1]) :
                                                                            TheUserManager.PerformLoginByRefreshToken(pRequestData, tCreds[0]);
                                            if (tUsers?.Count > 0)
                                            {
                                                TheBaseAssets.MySession.WriteSession(pRequestData.SessionState);
                                                if (tUsers.Count == 1)
                                                {
                                                    var tUser = tUsers[0];
                                                    tLogRes = string.Format("LOGIN_SUCCESS:{0}:{1}:{2}", TheUserManager.GetUserHomeScreen(pRequestData, tUser), tUser.Name, tUser.GetUserPrefString());
                                                    var rToken = TheUserManager.AddTokenToUser(tUser);
                                                    tPLB = TheCommonUtils.CUTF8String2Array(rToken);
                                                }
                                                else
                                                {
                                                    //New in 4.209: mesh Selection
                                                    tLogRes = $"SELECT_MESH:{TheCommonUtils.SerializeObjectToJSONString(pRequestData.SessionState.Meshes)}";
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(2352, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, "Login tried by failed with error", eMsgLevel.l1_Error));
                                    }
                                }
                            }
                        }
                        if (tLogRes.StartsWith("ERR"))
                        {
                            TheCDEKPIs.IncrementKPI(eKPINames.BruteDelay);
                            //Security Fix: ID#770 - wait 200 ms before returning anything with error code to limit BruteForce
                            TheCommonUtils.SleepOneEye(200, 100);
                        }
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.ContentService, tLogRes) { PLB = tPLB });
                    }
                    break;
                case "CDE_SETESID":
                case "CDE_SETSSID":
                    string tSidRes = "ERR:CDE_LOGIN_FAILURE";
                    if (!TheBaseAssets.MyServiceHostInfo.IsUsingUserMapper && pMsg.Message != null && !string.IsNullOrEmpty(pMsg.Message.SEID))
                    {
                        if (pMsg.Message.PLB != null) //tUser.PrimaryRole.Equals("ADMIN") &&
                        {
                            TheRequestData pRequestData = new TheRequestData { SessionState = TheBaseAssets.MySession.ValidateSEID(TheCommonUtils.CGuid(pMsg.Message.SEID)) };    //Low Frequency
                            if (pRequestData.SessionState != null)
                            {
                                try
                                {
                                    string tscope = null;
                                    if (Command[0].StartsWith("CDE_SETSSID"))
                                        tscope = TheCommonUtils.Decrypt(pMsg.Message.PLB, TheBaseAssets.MySecrets.GetAI());
                                    else
                                    {
                                        if (TheBaseAssets.MyServiceHostInfo.DisableRSAToBrowser)
                                            tscope = TheCommonUtils.CArray2UTF8String(pMsg.Message.PLB);
                                        else
                                            tscope = TheCommonUtils.cdeRSADecrypt(pRequestData.SessionState.cdeMID, pMsg.Message.PLB);
                                    }
                                    TheUserDetails tUser = TheUserManager.PerformLoginByScope(pRequestData, tscope.Trim('\0'));
                                    if (tUser != null)
                                    {
                                        TheBaseAssets.MySession.WriteSession(pRequestData.SessionState);
                                        tSidRes = string.Format("LOGIN_SUCCESS:{0}:{1}:{2}", TheUserManager.GetUserHomeScreen(pRequestData, tUser), tUser.Name, tUser.GetUserPrefString());
                                    }
                                }
                                catch (Exception)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(2352, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, "Login tried by failed with error", eMsgLevel.l1_Error));
                                }
                            }
                        }
                    }
                    if (tSidRes.StartsWith("ERR"))
                    {
                        TheCDEKPIs.IncrementKPI(eKPINames.BruteDelay);
                        //Security Fix: ID#770 - wait 200 ms before returning anything with error code to limit BruteForce
                        TheCommonUtils.SleepOneEye(200, 100);
                    }
                    TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.ContentService, tSidRes));

                    break;
                case "CDE_INIT_SYNC":
                    TheUserManager.ResetUserScopes(true, true);
                    break;
                case "CDE_SYNCUSER":
                    TheBaseAssets.MySYSLOG.WriteToLog(7678, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, $"CDE_SYNC_USER: Received from {TheCommonUtils.GetDeviceIDML(pMsg.Message.GetOriginator())}", eMsgLevel.l3_ImportantMessage)); //, pMsg.Message.PLS));
                    List<TheUserDetails> tList = TheCommonUtils.DeserializeJSONStringToObject<List<TheUserDetails>>(pMsg.Message.PLS);
                    TheUserManager.MergeUsers(tList);
                    break;

                //Provisioning Calls here

                case "CDE_ENDISPLUGIN":
                    try
                    {
                        var tID = TheCommonUtils.CGuid(TheCommonUtils.DecryptWithConnToken(pMsg?.Message?.PLS, TheBaseAssets.MyServiceHostInfo.ConnToken));
                        if (tID!=Guid.Empty)
                        {
                            var estate = TheCDEngines.MyServiceStates.GetEntryByID(tID);
                            if (estate != null)
                            {
                                var tBase = TheThingRegistry.GetBaseEngine(estate.ClassName);
                                if (tBase != null)
                                {
                                    if (tBase.HasCapability(eThingCaps.Internal))
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(7678, new TSM(eEngineName.ContentService, $"Disable Plugin received from {TheCommonUtils.GetDeviceIDML(pMsg.Message.GetOriginator())} but internal plugin cannot be disabled", eMsgLevel.l1_Error));
                                        return;
                                    }
                                    if (tBase.HasCapability(eThingCaps.MustBePresent))
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(7678, new TSM(eEngineName.ContentService, $"Disable Plugin received from {TheCommonUtils.GetDeviceIDML(pMsg.Message.GetOriginator())} but plugin must be present", eMsgLevel.l1_Error));
                                        return;
                                    }
                                }
                                estate.IsDisabled = !estate.IsDisabled;
                                estate.IsUnloaded = estate.IsDisabled;
                                TheBaseAssets.RecordServices();
                            }
                        }
                    }
                    catch(Exception)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7678, new TSM(eEngineName.ContentService, $"Disable Plugin received from {TheCommonUtils.GetDeviceIDML(pMsg.Message.GetOriginator())} but failed", eMsgLevel.l1_Error));
                    }
                    //TODO: Remove a plugin
                    break;
                case "CDE_REQ_WIPEALL":
                    if (TheBaseAssets.MyServiceHostInfo.IsCloudService
    || (!pMsg.Message.IsFirstNode() && !TheBaseAssets.MyServiceHostInfo.AllowRemoteAdministration && !TheUserManager.HasUserAccess(pMsg.CurrentUserID, 128))
    || TheBaseAssets.MyApplication.MyISMRoot == null) return;
                    TheBaseAssets.MyApplication?.MyISMRoot?.RestartOrWipe(true, true, pMsg?.Message?.PLB, pMsg?.Message?.PLS);
                    break;
                case "CDE_PROVISION_NODE":
                    TheCDESettings.ParseProvisioning(pMsg?.Message?.PLB, false, true);
                    TheQueuedSenderRegistry.ReinitCloudRoutes();
                    break;
                case "CDE_DEVICEREGED":
                    string[] tPara = pMsg.Message.TXT.Split(':');
                    if (tPara[0] == "CDE_DEVICEREGED" && tPara.Length > 1)
                    {
                        Action<string, TheUPnPDeviceInfo> tCallBack = TheTimedCallbacks<TheUPnPDeviceInfo>.GetTimedRequest(tPara[tPara.Length - 1]);
                        if (tCallBack != null)
                        {
                            if (tPara[1] == "OK")
                            {
                                var tInfo = TheCommonUtils.DeserializeJSONStringToObject<TheUPnPDeviceInfo>(pMsg.Message.PLS);
                                tCallBack("OK", tInfo);
                            }
                            else
                                tCallBack(pMsg.Message.PLS, null);
                        }
                    }
                    break;
                case "CDE_DEVICEREG":
                    string HasFailed = null;
                    if (TheCommonUtils.IsNullOrWhiteSpace(pMsg.Message.PLS)) HasFailed = "no Device sent to register";
                    if (HasFailed == null && (TheBaseAssets.MyApplication == null || TheBaseAssets.MyApplication.MyCommonDisco == null)) HasFailed = "UPnP System not active";
                    if (HasFailed == null)
                    {
                        try
                        {
                            TheUPnPDeviceInfo tDeviceData = TheCommonUtils.DeserializeJSONStringToObject<TheUPnPDeviceInfo>(pMsg.Message.PLS);
                            if (tDeviceData != null && TheBaseAssets.MyApplication.MyCommonDisco.RegisterDevice(tDeviceData, true))
                                TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.ContentService, "CDE_DEVICEREGED:OK" + (Command.Length > 1 ? (":" + Command[1]) : ""), TheCommonUtils.SerializeObjectToJSONString(TheBaseAssets.MyApplication.MyCommonDisco.GetTheUPnPDeviceInfo())));
                            else
                                HasFailed = "Could not register device";
                        }
                        catch (Exception e)
                        {
                            HasFailed = "Failed to Register :" + e;
                        }
                    }
                    if (HasFailed != null)
                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.ContentService, "CDE_DEVICEREGED:FAILED" + (Command.Length > 1 ? (":" + Command[1]) : ""), HasFailed));
                    break;
                case "CDE_REQ_UPDATE":
                    TheBaseAssets.MySYSLOG.WriteToLog(4014, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ContentService, $"CDE_REQ_UPDATE received", eMsgLevel.l3_ImportantMessage));
                    if (TheBaseAssets.MyServiceHostInfo.IsCloudService
                        || (!pMsg.Message.IsFirstNode() && !TheBaseAssets.MyServiceHostInfo.AllowRemoteAdministration)  // CODE REVIEW: Removed this check for MeshManager remote push/restart. Is this the right approach? -> Revisit MeshMgr securiyt workitem
                        || (!TheUserManager.HasUserAccess(pMsg.CurrentUserID, 128) && !TheBaseAssets.MyServiceHostInfo.AllowAutoUpdate)
                        || TheBaseAssets.MyApplication.MyISMRoot == null) return;
                    TheBaseAssets.MySYSLOG.WriteToLog(4015, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ContentService, $"Auto Update Allowed - scanning", eMsgLevel.l3_ImportantMessage));
                    if (TheBaseAssets.MyApplication?.MyISMRoot?.ScanForISMUpdate(true, true, false) != null)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4016, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ContentService, $"Launching Updater", eMsgLevel.l3_ImportantMessage));
                        TheBaseAssets.MyApplication?.MyISMRoot?.LaunchUpdater(null, null,null, true);
                    }
                    break;

                case "CDE_REQ_RESTART":
                    if (TheBaseAssets.MyServiceHostInfo.IsCloudService
                        || (!pMsg.Message.IsFirstNode() && !TheBaseAssets.MyServiceHostInfo.AllowRemoteAdministration && !TheUserManager.HasUserAccess(pMsg.CurrentUserID, 128))
                        || TheBaseAssets.MyApplication.MyISMRoot == null) return;
                    TheBaseAssets.MyApplication.MyISMRoot.Restart(false);
                    break;
                case "CDE_FORCEBACKUP":
                    if (TheBaseAssets.MyServiceHostInfo.IsCloudService
                        || (!pMsg.Message.IsFirstNode() && !TheBaseAssets.MyServiceHostInfo.AllowRemoteAdministration && !TheUserManager.HasUserAccess(pMsg.CurrentUserID, 128))) return;
                    TheISMManager.BackupCache($"Req by ProSer at {TheCommonUtils.GetTimeStamp()}");
                    break;
                case "CDE_CHECK4_UPDATES":
                    FireEvent(eEngineEvents.Check4Updates, this, pMsg, true);
                    break;
                case "CDE_ISM_GET_AVAILABLEUPDATES":
                    if (TheBaseAssets.MyServiceHostInfo.IsCloudService
                        || (!pMsg.Message.IsFirstNode() && !TheBaseAssets.MyServiceHostInfo.AllowRemoteAdministration)
                        || (!TheUserManager.HasUserAccess(pMsg.CurrentUserID, 128) && !TheBaseAssets.MyServiceHostInfo.AllowAutoUpdate)
                        || TheBaseAssets.MyApplication.MyISMRoot == null)
                        return;
                    var pendingUpdates = TheBaseAssets.MyApplication.MyISMRoot.GetAvailableUpdates();

                    TSM tTsmUpdates = new TSM(eEngineName.ContentService, "CDE_ISM_AVAILABLEUPDATES", pendingUpdates);
                    if (pMsg.LocalCallback != null)
                    {
                        pMsg.LocalCallback(tTsmUpdates);
                    }
                    else
                    {
                        TheCommCore.PublishToOriginator(pMsg.Message, tTsmUpdates);
                    }
                    break;
                case eEngineEvents.NewEventLogEntry:
                    {
                        var tLog = TheCommonUtils.DeserializeJSONStringToObject<TheEventLogData>(pMsg?.Message?.PLS);
                        if (tLog != null)
                            MyBaseThing.FireEvent(eEngineEvents.NewEventLogEntry, this, tLog, true);
                    }
                    break;
                default:
                    MyBaseThing.FireEvent(eEngineEvents.CustomTSMMessage, this, pMsg, true);
                    break;
            }
            //break;
            //}
        }

        #region Blob Management
        internal static cdeConcurrentDictionary<string, DateTimeOffset> BlobsNotHere = new cdeConcurrentDictionary<string, DateTimeOffset>();
        TheBlobData ProcessBlobRequest(TSM pMessage, Action<TSM> pLocalCallback, bool IsLocalOnly)
        {
            TheBlobData tBlobBuffer = null;
            TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ContentService, string.Format("Process-BlobRequest for Blob {0} received (IsLocalRequest={1})", pMessage.TXT, IsLocalOnly), eMsgLevel.l6_Debug), true);

            string[] Command = pMessage.TXT.Split(':');
            string tFileRequested = ""; if (Command.Length > 1) tFileRequested = Command[1];
            string[] tBlobParts = tFileRequested.Split(';');
            string AssFile = tBlobParts[0];
            if (tBlobParts[0].Contains("?"))
                AssFile = tBlobParts[0].Substring(0, tBlobParts[0].IndexOf("?", StringComparison.Ordinal));
            bool WasFound = true; // BlobsNotHere.TryGetValue(AssFile, out var tLastDate);
            var tLastDate = BlobsNotHere.GetOrAdd(AssFile, (key) => { WasFound = false; return DateTimeOffset.Now; });
            if (WasFound)
            {
                if (DateTimeOffset.Now.Subtract(tLastDate).TotalSeconds > TheBaseAssets.MyServiceHostInfo.SessionTimeout)
                    BlobsNotHere.RemoveNoCare(AssFile);
                else
                    return null;
            }

            TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, string.Format("Process-BlobRequest is looking for Asset:{0}", AssFile), eMsgLevel.l6_Debug), true);
            TheRequestData tRequest = null;
            if (!IsLocalOnly)
            {
                if (!string.IsNullOrEmpty(pMessage.PLS))
                {
                    tRequest = TheCommonUtils.DeserializeJSONStringToObject<TheRequestData>(pMessage.PLS);
                    tRequest.RequestUri = TheCommonUtils.CUri(tRequest.RequestUriString, true);
                }
                else
                    tRequest = new TheRequestData { cdeRealPage = "/" + AssFile };
                Communication.HttpService.TheHttpService.ProcessB4Interceptors(tRequest, false);
                if (tRequest.ResponseBuffer != null)
                {
                    tBlobBuffer = new TheBlobData
                    {
                        BlobData = tRequest.ResponseBuffer,
                        MimeType = tRequest.ResponseMimeType
                    };
                }
            }
            if (tBlobBuffer == null)
            {
                byte[] tBL = TheBaseAssets.MyApplication.cdeBlobLoader(AssFile);
                if (tBL != null)
                {
                    tBlobBuffer = new TheBlobData
                    {
                        BlobData = tBL,
                        MimeType = AssFile.LastIndexOf('.') >= 0 ? TheCommonUtils.GetMimeTypeFromExtension(AssFile.Substring(AssFile.LastIndexOf('.'))) : ""
                    };
                }
            }
            if (tBlobBuffer == null)
            {
                if (!IsLocalOnly)
                {
                    Communication.HttpService.TheHttpService.GetAnyFile(tRequest, false); //.ProcessAfterInterceptors(tRequest, false);
                    if (tRequest.ResponseBuffer != null)
                    {
                        tBlobBuffer = new TheBlobData
                        {
                            BlobData = tRequest.ResponseBuffer,
                            MimeType = tRequest.ResponseMimeType
                        };
                    }
                }
            }
            if (tBlobBuffer == null)
                return null;
            else
                BlobsNotHere.RemoveNoCare(AssFile);

            if (IsLocalOnly && pLocalCallback == null)
                return tBlobBuffer;

            tBlobBuffer.BlobExtension = "";
            tBlobBuffer.BlobExtension = tBlobParts[0].IndexOf('.') >= 0 ? tBlobParts[0].Substring(tBlobParts[0].LastIndexOf('.')).ToUpper() : "NOEXT";

            try
            {
                TSM BlobGetRequest = new TSM(MyBaseEngine.GetEngineName(), "CDE_BLOBREQUESTED:" + tBlobParts[0]);
                tBlobBuffer.BlobName = tBlobParts[0];
                BlobGetRequest.PLB = tBlobBuffer.BlobData;
                BlobGetRequest.PLS = TheCommonUtils.SerializeObjectToJSONString(tBlobBuffer.CloneForSerial());
                if (!IsLocalOnly)
                {
                    BlobGetRequest.SetNotToSendAgain(true);
                    TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, string.Format("Publishing Block Back to {0} BlockSize:{1}", pMessage.GetOriginator(), BlobGetRequest.PLB == null ? 0 : BlobGetRequest.PLB.Length), eMsgLevel.l3_ImportantMessage), true);
                    TheCommCore.PublishToOriginator(pMessage, BlobGetRequest);
                }
                pLocalCallback?.Invoke(BlobGetRequest);
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ContentService, string.Format("Publishing Block Back to {0} FAILED", pMessage.GetOriginator()), eMsgLevel.l1_Error, e.ToString()));
            }
            return tBlobBuffer;
        }

        /// <summary>
        /// Returns a Blob either from the local node or a any mesh connected node in the same security context
        /// </summary>
        /// <param name="pBlobName">Full Path and Name of the blob to retrieve</param>
        /// <param name="pScrambledScopeID">Security Context</param>
        /// <param name="DoPublishRequest">If set to false - only the local node will respond and the request will not be meshed</param>
        /// <returns></returns>
        public TheBlobData GetBlob(string pBlobName, string pScrambledScopeID, bool DoPublishRequest)
        {
            return GetBlob(pBlobName, pScrambledScopeID, DoPublishRequest, TheBaseAssets.MyServiceHostInfo.TO.GetBlobTimeout);
        }
        /// <summary>
        /// Returns a Blob either from the local node or a any mesh connected node in the same security context
        /// </summary>
        /// <param name="pBlobName">Full Path and Name of the blob to retrieve</param>
        /// <param name="pScrambledScopeID">Security Context</param>
        /// <param name="DoPublishRequest">If set to false - only the local node will respond and the request will not be meshed</param>
        /// <param name="BlobTimeout">Each retrieved blob is stored in a temporary cache. This parameter allows to specify the blob timeout in seconds. If set to zero, the cache will NEVER expire</param>
        /// <returns></returns>
        public TheBlobData GetBlob(string pBlobName, string pScrambledScopeID, bool DoPublishRequest, long BlobTimeout)
        {
            return GetBlob(pBlobName, pScrambledScopeID, DoPublishRequest, BlobTimeout, Guid.Empty);
        }
        internal TheBlobData GetBlob(string pBlobName, string pScrambledScopeID, bool DoPublishRequest, long BlobTimeout, Guid pTargetNode, TheRequestData pReqData = null)
        {
            if (string.IsNullOrEmpty(pBlobName) || pBlobName.StartsWith("cache",StringComparison.InvariantCultureIgnoreCase)) //Dont ever load cache files over the mesh!
                return new TheBlobData { HasErrors = true, ErrorMsg = "Access to Cache folder denied" };

            TSM tTSM = new TSM(eEngineName.ContentService, "CDE_GETBLOB:");
            tTSM.SetToServiceOnly(true);
            tTSM.SetNotToSendAgain(true);

            string tTopic = "CDE_SYSTEMWIDE"; // eEngineName.ContentService;
            if (!string.IsNullOrEmpty(pScrambledScopeID))
            {
                tTopic += "@" + pScrambledScopeID;
                tTSM.SID = pScrambledScopeID;
            }
            else
            {
                if (!TheBaseAssets.MyServiceHostInfo.IsCloudService && TheBaseAssets.MyScopeManager.IsScopingEnabled && TheBaseAssets.MyServiceHostInfo.AllowDistributedResourceFetch)
                {
                    pScrambledScopeID= TheBaseAssets.MyScopeManager.GetScrambledScopeID();     //GRSI: rare
                    tTopic += "@" + pScrambledScopeID;
                    tTSM.SID = pScrambledScopeID;
                }
            }
            if (Guid.Empty != pTargetNode)
                tTopic += $";{pTargetNode}";
            tTSM.TXT += pBlobName;
            if (pReqData != null)
            {
                if (string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.RootDir))
                    pReqData.RequestUriString = pReqData.RequestUri.ToString();
                else
                {
                    pReqData.RequestUriString = pReqData.RequestUri.Scheme + "://" + pReqData.RequestUri.Host + ":" + pReqData.RequestUri.Port + pReqData.cdeRealPage;
                    if (!string.IsNullOrEmpty(pReqData.RequestUri.Query))
                        pReqData.RequestUriString += "?" + pReqData.RequestUri.Query;
                }
                tTSM.PLS = TheCommonUtils.SerializeObjectToJSONStringM(pReqData);
            }
            string tCacheBlobName = pBlobName+(string.IsNullOrEmpty(pScrambledScopeID) ? "" : TheBaseAssets.MyScopeManager.GetRealScopeID(pScrambledScopeID));  //GRSI: rare
            //lock (TheBaseAssets.MyBlobCache.MyRecordsLock)    //LOCK-REVIEW: Is this required here? it was removed in last Perf-Push
            {
                TheBlobData tData = TheBaseAssets.MyBlobCache.GetEntryByID(tCacheBlobName);

                if (tData == null || tData.BlobData == null) //NEW Beta3: BlobData Test here
                    tData = ProcessBlobRequest(tTSM, sinkBlobReturn, true);

                if (tData == null && DoPublishRequest)
                {
                    tData = new TheBlobData
                    {
                        BlobAction = sinkBlobReturn,
                        cdeEXP = BlobTimeout
                    };
                    if (!TheBaseAssets.MyBlobCache.ContainsID(tCacheBlobName))
                        TheBaseAssets.MyBlobCache.AddOrUpdateItemKey(tCacheBlobName, tData, null);
                }
                if (tData != null && tData.BlobData == null && DoPublishRequest)
                {
                    if (string.IsNullOrEmpty(pScrambledScopeID))
                    {
                        if (pReqData != null)
                            pReqData.StatusCode = 404;
                        TheBaseAssets.MyBlobCache.RemoveAnItemByKey(tCacheBlobName, null);
                        return new TheBlobData { HasErrors = true, ErrorMsg = "Unscoped request denied" }; //If ther request is not scoped and is not allow to adopt the scope of this node, the resource cannot be fetched and should return 404 right away
                    }
                    if (tData.LastBlobUpdate != DateTimeOffset.MinValue)
                    {
                        tTSM.TXT += ";" + tData.LastBlobUpdate.DateTime.ToFileTimeUtc();
                    }
                    TheCommCore.PublishCentral(tTopic, tTSM);
                    TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, $"Requesting Blob for ({pBlobName}) was Sent to Channels with ScopeHash:({(TheBaseAssets.MyScopeManager.IsScopingEnabled ? TheBaseAssets.MyScopeManager.GetRealScopeID(tTSM.SID)?.Substring(0, 4) : "Scope not set, yet")})", eMsgLevel.l6_Debug, tTSM.SID), true); //TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null :
                }
                if (tData != null && tData.BlobData!=null && tData.cdeEXP < 0)
                    TheBaseAssets.MyBlobCache.RemoveAnItemByKey(tCacheBlobName, null);
                return tData;
            }
        }
        private void sinkBlobReturn(TSM pResult)
        {
            string BlobName = "";
            string[] t = pResult.TXT.Split(':');
            if (t.Length > 1) BlobName = t[1];
            TheBlobData tData = null;
            if (!string.IsNullOrEmpty(BlobName) && pResult.PLB != null && pResult.PLB.Length > 0)
            {
                string res = "";
                if (t.Length > 2)
                {
                    res = t[2];
                    if (TheCommonUtils.CLng(res) > 0)
                        res = DateTime.FromFileTimeUtc(TheCommonUtils.CLng(res)).ToString(CultureInfo.InvariantCulture);
                }

                TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, $"Blob received {BlobName} from ORG:{TheCommonUtils.GetDeviceIDML(pResult?.ORG)} - Result:{res}", eMsgLevel.l6_Debug), true);  //ORG-OK TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null :
                string tCacheBlobName = BlobName + (string.IsNullOrEmpty(pResult.SID) ? "" : TheBaseAssets.MyScopeManager.GetRealScopeID(pResult.SID)); //GRSI: rare
                tData = TheBaseAssets.MyBlobCache.GetEntryByID(tCacheBlobName);
                if (tData == null || tData.BlobData==null)
                {
                    long tExp = 0;
                    if (tData != null)
                        tExp = tData.cdeEXP;
                    tData = TheCommonUtils.DeserializeJSONStringToObject<TheBlobData>(pResult.PLS);
                    tData.BlobData = pResult.PLB;
                    tData.BlobName = BlobName;
                    tData.cdeEXP = tExp;
                    TheBaseAssets.MyBlobCache.AddOrUpdateItemKey(tCacheBlobName,tData, null);
                }
            }
            MyBaseThing.FireEvent(eEngineEvents.BlobReceived,this, tData,true);
        }

        internal void SendFile(TheBlobData tBlob, string target)
        {
            TSM BlobGetRequest = new TSM(MyBaseEngine.GetEngineName(), "CDE_FILEPUSH:" + tBlob.BlobName)
            {
                PLB = tBlob.BlobData,
                PLS = TheCommonUtils.SerializeObjectToJSONStringM(tBlob.CloneForSerial())
            };
            BlobGetRequest.SetNotToSendAgain(true);
            string tPublishTo = "CDE_SYSTEMWIDE";
            if (TheBaseAssets.MyScopeManager.IsScopingEnabled) tPublishTo += "@" + TheBaseAssets.MyScopeManager.GetScrambledScopeID();     //GRSI: rare
            tPublishTo += ";" + target;
            TheCommCore.PublishCentral(tPublishTo, BlobGetRequest);
        }
#endregion
    }

}
