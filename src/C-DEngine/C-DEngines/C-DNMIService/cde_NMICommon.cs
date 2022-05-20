// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using System.Linq;
using nsCDEngine.Communication;
// ReSharper disable UseNullPropagation

namespace nsCDEngine.Engines.NMIService
{
    public partial class TheNMIEngine : ThePluginBase
    {
        #region ICDEPlugin Methods
        internal ICDENMIPlugin MyRenderEngine;
        /// <summary>
        /// Initializes the internal NMI Engine
        /// </summary>
        /// <param name="pBase">The C-DEngine is creating a Host for this engine and hands it to the Plugin Service</param>
        public override void InitEngineAssets(IBaseEngine pBase)
        {
            base.InitEngineAssets(pBase);
            MyBaseEngine.SetEngineName(eEngineName.NMIService);        //Can be any arbitrary name - recommended is the class name
            MyBaseEngine.SetFriendlyName("The NMI Model Service");
            MyBaseEngine.AddCapability(eThingCaps.Internal);
            MyBaseEngine.SetPluginInfo("This service manages the NMI Model of the C-DEngine", 0, null, "toplogo-150.png", "C-Labs", "https://www.c-labs.com", null); //TODO: Describe your plugin - this will later be used in the Plugin-Store

            MyBaseEngine.GetEngineState().IsAllowedUnscopedProcessing = TheBaseAssets.MyServiceHostInfo.IsCloudService;
            MyBaseEngine.GetEngineState().IsAcceptingFilePush = true;
            MyBaseEngine.SetEngineID(new Guid("{4D6E5FE7-338E-4B3E-B98D-0FFFEB62FE63}"));
            MyBaseEngine.SetVersion(TheBaseAssets.BuildVersion);
        }
        #endregion

        /// <summary>
        /// Creates the basic NMI User Interface
        /// </summary>
        /// <returns></returns>
        public override bool CreateUX()
        {
            if (mIsUXInitCalled) return false;

            mIsUXInitCalled = true;
            if (!MyBaseEngine.GetEngineState().IsService)
            {
                mIsUXInitialized = true;
                return true;
            }

            UserPrefID = new Guid("{E15AE1F2-69F3-42DC-97E8-B0CC2A8526A6}").ToString().ToLower();
            UserManID = TheThing.GetSafeThingGuid(MyBaseThing, "USEMAN").ToString().ToLower();
            AddDashboard(MyBaseThing, new TheDashboardInfo(eNMIPortalDashboard, "-HIDE") { FldOrder = 99999, PropertyBag = { "Category=NMI" } }); //Main Portal of Relay
            TheDashboardInfo tDash = AddDashboard(MyBaseThing, new TheDashboardInfo(eNMIDashboard, "NMI Admin Portal") { cdeA = 0x0, FldOrder = 9090, PropertyBag = new nmiDashboard() { Category = "NMI", ForceLoad = true, Thumbnail="FA5:f06c", Caption = "###NMI Admin###" } });

            if (!TheBaseAssets.MyServiceHostInfo.IsIsolated)
            {
                TheFormInfo tInf = new TheFormInfo(new Guid("{6EE8AC31-7395-4A80-B01C-D49BE174CFC0}"), eEngineName.NMIService, "###Service Overview###", "HostEngineStates") { IsNotAutoLoading = true, PropertyBag=new nmiCtrlTableView { ShowFilterField=true } };
                AddFormToThingUX(tDash, MyBaseThing, tInf, "CMyTable", "###Service Overview###", 1, 0x0F, 128, "###NMI Administration###", null, new ThePropertyBag { "TileThumbnail=FA5:f05a" });

                var tDisButName = "DISPLUG";
                var tDisBut = AddSmartControl(MyBaseThing, tInf, eFieldType.TileButton, 1, 0x42, 0x80, null, "DisableText", new nmiCtrlTileButton() { MID=new Guid("{7C67925E-7C2D-4460-9E61-6166494E9328}"), TableHeader = "###Is Enabled###", TileHeight = 1, TileWidth = 1, FldWidth = 1, ClassName = "cdeTableButton" });
                tDisBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, tDisButName, (sender, para) =>
                {
                    if (para is TheProcessMessage pMsg && pMsg.Message != null)
                    {
                        var tCmd = pMsg.Message.PLS?.Split(':');
                        if (tCmd != null && tCmd.Length > 2)
                        {
                            if (tCmd[1] != tDisButName) return;
                            var estate = TheCDEngines.MyServiceStates.GetEntryByID(tCmd[2]);
                            if (estate != null)
                            {
                                var tBase = TheThingRegistry.GetBaseEngine(estate.ClassName);
                                if (tBase != null)
                                {
                                    if (tBase.HasCapability(eThingCaps.Internal))
                                    {
                                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", TheNMIEngine.LocNMI(pMsg, "###Not allowed on internal engines###")));
                                        return;
                                    }
                                    if (tBase.HasCapability(eThingCaps.MustBePresent))
                                    {
                                        TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", TheNMIEngine.LocNMI(pMsg, "###Not allowed on engines that must be present###")));
                                        return;
                                    }
                                }
                                estate.IsDisabled = !estate.IsDisabled;
                                estate.IsUnloaded = estate.IsDisabled;
                                SetUXProperty(Guid.Empty, tDisBut.cdeMID, $"Caption={estate.DisableText}", pMsg.Message.PLS.Split(':')[2]);
                                TheBaseAssets.RecordServices();
                            }
                        }
                    }
                });
                var tKillBut = AddSmartControl(MyBaseThing, tInf, eFieldType.TileButton, 3, 2, 0x80, null, "ControlText", new nmiCtrlTileButton() { MID = new Guid("{7C67925E-7C2D-4460-9E61-6166494E9329}"), TableHeader = "###Start/Stop Plugin###", TileHeight = 1, TileWidth = 1, FldWidth = 1, ClassName = "cdeTableButton", GreyCondition = "cdeCommonUtils.CBool('%IsDisabled%')==true" });
                var tKillName = $"KLLPLUG";
                tKillBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, tKillName, (sender, para) =>
                {
                    if (para is TheProcessMessage pMsg && pMsg.Message != null)
                    {
                        var tCmd = pMsg.Message.PLS?.Split(':');
                        if (tCmd != null && tCmd.Length > 2)
                        {
                            if (tCmd[1] != tKillName) return;
                            var estate = TheCDEngines.MyServiceStates.GetEntryByID(tCmd[2]);
                            if (estate != null)
                            {
                                var tBase = TheThingRegistry.GetBaseEngine(estate.ClassName);
                                if (estate.IsUnloaded)
                                {
                                    if (estate.IsIsolated)
                                    {
                                        if (TheBaseAssets.MyCDEPlugins.TryGetValue(estate.ClassName, out ThePluginInfo tPlugType))
                                        {
                                            string tEngIso = $"{System.IO.Path.GetFileName(tPlugType?.PluginPath)}:{estate.ClassName}";
                                            TheCDEngines.IsolatePlugin(tEngIso);
                                        }
                                    }
                                    else
                                    {
                                        estate.IsUnloaded = false;
                                        TheCDEngines.StartPluginService(tBase, false, false);
                                    }
                                }
                                else
                                {
                                    if (tBase != null)
                                    {
                                        if (tBase.HasCapability(eThingCaps.Internal))
                                        {
                                            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", TheNMIEngine.LocNMI(pMsg, "###Not allowed on internal engines###")));
                                            return;
                                        }
                                        if (tBase.HasCapability(eThingCaps.MustBePresent))
                                        {
                                            TheCommCore.PublishToOriginator(pMsg.Message, new TSM(eEngineName.NMIService, "NMI_TOAST", TheNMIEngine.LocNMI(pMsg, "###Not allowed on engines that must be present###")));
                                            return;
                                        }
                                        tBase.StopEngine();
                                    }
                                }
                                TheNMIEngine.SetUXProperty(Guid.Empty, tKillBut.cdeMID, $"Caption={estate.ControlText}", pMsg.Message.PLS.Split(':')[2]);
                            }
                        }
                    }
                });
                if (TheBaseAssets.MyServiceHostInfo.EnableIsolation && TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("RedPill")))
                {

                    var tIsoButName = $"ISO{TheCommonUtils.GetRandomUInt(0, 1000)}";
                    var tIsoBut = TheNMIEngine.AddSmartControl(MyBaseThing, tInf, eFieldType.TileButton, 2, 0x42, 0x80, null, "IsoText", new nmiCtrlTileButton() { TableHeader = "###Is Isolated###", TileHeight = 1, TileWidth = 1, FldWidth = 1, ClassName = "cdeTableButton" , GreyCondition = "cdeCommonUtils.CBool('%IsAllowedForIsolation%')==false" });
                    tIsoBut.RegisterUXEvent(MyBaseThing, eUXEvents.OnClick, tIsoButName, (sender, para) =>
                    {
                        if (para is TheProcessMessage pMsg && pMsg.Message != null)
                        {
                            var tCmd = pMsg.Message.PLS?.Split(':');
                            if (tCmd != null && tCmd.Length > 2)
                            {
                                if (tCmd[1] != tIsoButName) return;
                                var estate = TheCDEngines.MyServiceStates.GetEntryByID(tCmd[2]);
                                if (estate != null)
                                {
                                    estate.IsIsolated = !estate.IsIsolated;
                                    TheThing tEngThing = TheThingRegistry.GetBaseEngineAsThing(estate.ClassName);
                                    if (tEngThing != null)
                                        TheThing.SetSafePropertyBool(tEngThing, "IsIsolated", estate.IsIsolated);
                                    SetUXProperty(Guid.Empty, tIsoBut.cdeMID, $"Caption={estate.IsoText}", pMsg.Message.PLS.Split(':')[2]);
                                    SetUXProperty(Guid.Empty, tKillBut.cdeMID, $"Caption={estate.ControlText}", pMsg.Message.PLS.Split(':')[2]);
                                }
                            }
                        }
                    });


                }
                AddField(tInf, new TheFieldInfo() { FldOrder = 10, DataItem = "FriendlyName", Type = eFieldType.SingleEnded, Header = "Friendly Name", FldWidth = 3 });
            }

            //Added to createUX on plugins first - then things of plugins
            List<IBaseEngine> tEList = TheThingRegistry.GetBaseEngines(true);
            tEList=tEList.OrderBy(s => s.GetBaseEngine().GetLowestCapability()).ToList();
            foreach (var tThing in tEList)
            {
                if (!(tThing.GetBaseThing()?.IsUXInit()==true))
                    tThing.GetBaseThing().CreateUX();
            }

            List<TheThing> tList = TheThingRegistry.GetThingsOfEngine("*",TheBaseAssets.MyServiceHostInfo.IsIsolated).OrderBy(s=>s.GetLowestCapability()).ToList();
            foreach (TheThing tThing in tList)
            {
                if (!tThing.IsUXInit())
                    tThing.CreateUX();
            }
            RegisterEngine(MyBaseEngine);
            mIsUXInitialized = true;
            return true;
        }

        /// <summary>
        /// The complete NMI (Natural Machine Interface) Meta Data is stored in the MyNMIModel
        /// </summary>
        internal TheNMIModel MyNMIModel;
        private static string UserPrefID;
        private static string UserManID;

        /// <summary>
        /// Initializes the NMI Engine
        /// </summary>
        /// <returns></returns>
        public override bool Init()
        {
            if (MyNMIModel == null)
            {
                MyBaseThing.StatusLevel = 4;
                MyBaseEngine.GetEngineState().LastMessage = "NMI Model starting...";
                MyNMIModel = new TheNMIModel();
                MyNMIModel.eventModelIsReady += sinkModelReady;
                MyNMIModel.Init();

                Guid UMID = TheCommonUtils.CGuid(TheBaseAssets.MySettings.GetSetting("UserManagerGuid"));
                if (UMID != Guid.Empty)
                    TheThing.SetSafePropertyGuid(MyBaseThing, "USEMAN_ID", UMID);
            }
            return false;
        }


        void sinkModelReady()
        {
            if (mIsInitCalled) return;
            {
                mIsInitCalled = true;
                MyBaseThing.RegisterEvent(eEngineEvents.IncomingMessage, HandleMessage);

                // CODE REVIEW: Needs to be marked initialized or CreateUX fails due to check of TheNMIEngine.Initialized()
                mIsInitialized = true;
                FireEvent(eThingEvents.Initialized, this, true, true);

                CreateUX();
                TheUserManager.AddNewUserByRole(new TheUserDetails() { PrimaryRole = "NMIADMIN", UserName = "admin", Password = "", Name = "Administrator", AssignedEasyScope = "*", AccessMask = 255, NodeScope = "ALL", IsReadOnly = true }); //* Scope OK AES OK ; PW must be empty
                RegisterControlType(MyBaseEngine, "Single Line Text", "1");
                RegisterControlType(MyBaseEngine, "eMail Address", "16");
                RegisterControlType(MyBaseEngine, "Drop Down Box", "2");
                RegisterControlType(MyBaseEngine, "Multi-Line Text", "5");
                RegisterControlType(MyBaseEngine, "Single Checkbox", "4");
                RegisterControlType(MyBaseEngine, "Checkbox Field", "26");
                RegisterControlType(MyBaseEngine, "Touch-Draw Area", "36");
                RegisterControlType(MyBaseEngine, "Bar Chart", "34");
                RegisterControlType(MyBaseEngine, "MuTLock", "47");
                RegisterControlType(MyBaseEngine, "Smart Label", "20");
                RegisterControlType(MyBaseEngine, "Number", "12");
                RegisterControlType(MyBaseEngine, "Date Time", "21");
                RegisterControlType(MyBaseEngine, "Time", "8");
                RegisterControlType(MyBaseEngine, "Month", "18");
                RegisterControlType(MyBaseEngine, "Password", "10");
                RegisterControlType(MyBaseEngine, "Time Span", "9");
                RegisterControlType(MyBaseEngine, "URL", "31");
                RegisterControlType(MyBaseEngine, "Tile Button", "22");
                RegisterControlType(MyBaseEngine, "Image", "29");
                RegisterControlType(MyBaseEngine, "Draw Canvas", "36");
                RegisterControlType(MyBaseEngine, "Endless Slider", "33");
                RegisterControlType(MyBaseEngine, "Progress Bar", "48");
                RegisterControlType(MyBaseEngine, "Circular Gauge", $"{(int)eFieldType.CircularGauge}");
                RegisterControlType(MyBaseEngine, "Smart Gauge", $"{(int)eFieldType.SmartGauge}");
                RegisterControlType(MyBaseEngine, "Status Light", $"{(int)eFieldType.StatusLight}");
                RegisterControlType(MyBaseEngine, "Image Slider", $"{(int)eFieldType.ImageSlider}");
                RegisterControlType(MyBaseEngine, "Logo Button", $"{(int)eFieldType.LogoButton}");
                RegisterControlType(MyBaseEngine, "Collapsible Group", $"{(int)eFieldType.CollapsibleGroup}");
                RegisterControlType(MyBaseEngine, "Tile Group", $"{(int)eFieldType.TileGroup}");

                RegisterEvent(eEngineEvents.ThingDeleted, sinkThingDeleted);
                MyBaseEngine.ProcessInitialized();
                MyBaseThing.StatusLevel = 1;
                MyBaseThing.LastMessage = "NMI Model ready";
                RegisterAllFieldEvents();
            }
        }

        private void sinkThingDeleted(ICDEThing pEngine, object pDelThing)
        {
            if (!(pDelThing is TheThing tThing)) return;
            try
            {

                if (!string.IsNullOrEmpty(tThing.DeviceType) && tThing.DeviceType == eKnownDeviceTypes.TheNMIScene)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(10004, new TSM(MyBaseEngine.GetEngineName(), "Delete of Scene Intercepted", eMsgLevel.l3_ImportantMessage));
                    TheCommonUtils.DeleteFromDisk(string.Format("scenes\\{0}\\{1}.cdescene", tThing.UID, tThing.cdeMID), null);
                    return;
                }

                TheDashboardInfo tDash = GetDashboardById(tThing.cdeMID);
                if (tDash != null)
                    TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.RemoveAnItem(tDash, null);
                List<TheDashboardInfo> tDashs = GetDashboardsByOwner(tThing.cdeMID);
                if (tDashs != null)
                    TheCDEngines.MyNMIService.MyNMIModel.MyDashboards.RemoveItems(tDashs, null);
                List<TheDashPanelInfo> tDP = GetDashPanelsByOwner(tThing.cdeMID);
                if (tDP != null)
                    TheCDEngines.MyNMIService.MyNMIModel.MyDashComponents.RemoveItems(tDP, null);
                List<TheFormInfo> tForms = GetFormsByOwner(tThing.cdeMID);
                if (tForms != null)
                {
                    foreach (TheFormInfo tF in tForms)
                    {
                        TheCDEngines.MyNMIService.MyNMIModel.MyForms.RemoveAnItem(tF, null);
                        CleanForm(tF);
                    }
                }
                List<TheFieldInfo> tflds = GetFieldsByOwner(tThing.cdeMID);
                if (tflds != null)
                    TheCDEngines.MyNMIService.MyNMIModel.MyFields.RemoveItems(tflds, null);
            }
            catch
            {
                //ignored
            }
        }

        /// <summary>
        /// Basic Message Handler of the NMI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="pIncoming"></param>
        public override void HandleMessage(ICDEThing sender, object pIncoming)
        {
            if (!(pIncoming is TheProcessMessage pMsg)) return;
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
            ProcessServiceMessage(pMsg);
        }
    }
}
