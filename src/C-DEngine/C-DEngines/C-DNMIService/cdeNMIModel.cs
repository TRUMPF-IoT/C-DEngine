// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

#pragma warning disable CS1591    //TODO: Remove and document public methods
namespace nsCDEngine.ViewModels
{
    #region NMI Classes
    /// <summary>
    /// Blob of an image
    /// </summary>
    public class ThePlanarImage
    {
        /// <summary>
        /// Binary Data of the Image
        /// </summary>
        public byte[] Bits;
        /// <summary>
        /// Bytes pre pixel
        /// </summary>
        public int BytesPerPixel;
        /// <summary>
        /// Width of the Image
        /// </summary>
        public int Height;
        /// <summary>
        /// Height of the Image
        /// </summary>
        public int Width;
        /// <summary>
        /// The Source where this image came from
        /// </summary>
        public string ImageSource;
    }
    /// <summary>
    /// Class defining the overrides for controls in a TheFormInfo (Thing Form)
    /// </summary>
    public class TheFLDOR
    {
        /// <summary>
        /// FldOrder of the Control to be overwritten
        /// </summary>
        public int FldOrder { get; set; }
        /// <summary>
        /// PropertyBag that will be merged with the existing Propertybag of the Control
        /// </summary>
        public ThePropertyBag PO { get; set; }

        /// <summary>
        /// New FldOrder for the control. If set to 0 no change will be applied. If set to -1 the Control will not be shown in the Form
        /// </summary>
        public int NewFldOrder { get; set; }
    }

    /// <summary>
    /// Class that allows to override Controls in a TheFormInfo
    /// </summary>
    public class TheFOR : TheMetaDataBase
    {
        /// <summary>
        /// ID Of the form. If a ModelID of a form is used the override is for all Things using the same TheFormInfo ModelID
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// Changes the TileWidth of the Form
        /// </summary>
        public int TileWidth { get; set; }

        /// <summary>
        /// contains the current active group if any is present
        /// </summary>
        public string StartGroup { get; set; }
        /// <summary>
        /// List of all Controls to be overwritten with a new PropertyBag
        /// </summary>
        public List<TheFLDOR> Flds { get; set; }
    }

    public class TheScreenTrans
    {
        public string ID { get; set; }

        public string DashID { get; set; }
        public bool IsVisible { get; set; }
        public bool IsPinned { get; set; }
        public int FldOrder { get; set; }
    }

    public class TheNMIScene : TheMetaDataBase
    {
        public string FriendlyName { get; set; }
        public bool IsPublic { get; set; }
        public List<TheScreenTrans> Screens { get; set; }
    }
    #endregion
}

namespace nsCDEngine.Engines.NMIService
{
    /// <summary>
    /// Enums for flags on Controls
    /// </summary>
    public enum eFlag
    {
        /// <summary>
        /// Creates * instead of text for the entry - only works with InputControls
        /// </summary>
        ShowAsPassword = 1,
        /// <summary>
        /// If not set, the control cannot be edited
        /// </summary>
        IsReadWrite = 2,
        /// <summary>
        /// If set, this control will not show on mobile devices
        /// </summary>
        HideFromMobile = 4,
        /// <summary>
        /// If set, this control will not show in Tables
        /// </summary>
        HideFromTable = 8,
        /// <summary>
        /// If set, this control will not show in Forms
        /// </summary>
        HideFromForm = 16,
        /// <summary>
        /// If set, the control will shown an advanced mode, if available
        /// </summary>
        AdvancedEditor = 32,
        /// <summary>
        /// Instead of pointing to a Property of the MyBaseThing, the OnUpdateName is referencing a field in the StorageMirror behind the control referenced by the cdeMID of the table row
        /// </summary>
        OnUpdateIsInRow = 64,
        /// <summary>
        /// If set, this control will not show on any other node then the FirstNode
        /// </summary>
        ShowOnFirstNodeOnly = 128
    }

    internal class TheNMISubscription
    {
        public Guid cdeMID { get; set; }

        public Guid cdeO { get; set; }
        public Guid cdeN { get; set; }

        public DateTimeOffset cdeCTIM { get; set; }
        public bool HasLiveSub { get; set; }
        public string DataItem { get; set; }

        public override string ToString()
        {
            return $"OT:{cdeO} Node:{cdeN}";
        }
    }

    internal class TheThingPicker : TheDataBase
    {
        public string DeviceType { get; set; }
        public string FriendlyName { get; set; }
        public string EngineName { get; set; }

        public bool IsRemote { get; set; }

        public ThePropertyBag PB { get; set; }
    }

    /// <summary>
    /// NMI Localization information
    /// </summary>
    public class TheNMILocationInfo : TheDataBase
    {
        /// <summary>
        /// Location description
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Longitude
        /// </summary>
        public double Longitude { get; set; }
        /// <summary>
        /// Latitude
        /// </summary>
        public double Latitude { get; set; }
        /// <summary>
        /// Accuracy of the Long/Lat location
        /// </summary>
        public double Accuracy { get; set; }
        /// <summary>
        /// client info this location was measure for
        /// </summary>
        public TheClientInfo ClientInfo { get; set; }
    }

    internal class NUITracking : TheDataBase
    {
        public string NUIArea { get; set; }
        public int GestureType { get; set; }
        public int FingersUsed { get; set; }

        public NUITracking(string pNui, int pGType)
        {
            NUIArea = pNui;
            GestureType = pGType;
        }
    }

    /// <summary>
    /// Predefined screen classes.
    /// </summary>
    public class eScreenClass
    {
        /// <summary>
        /// Declares a screen containing a form
        /// </summary>
        public const string CMyForm = "CMyForm";
        /// <summary>
        /// Declares a screen containing a Table
        /// </summary>
        public const string CMyTable = "CMyTable";
        /// <summary>
        /// Declares a screen containing a Chart (Requires the C-MyChart Plugin!)
        /// </summary>
        public const string CMyChart = "CMyChart";
        /// <summary>
        /// Declares a screen containing a Dashboard
        /// </summary>
        public const string CMyDashboard = "CMyDashboard";
        /// <summary>
        /// declares a dynamically created live Screen
        /// </summary>
        public const string CMyLiveScreen = "CMyLiveScreen";
    }

    /// <summary>
    /// Displays a "Form" either as Table or as Form
    /// </summary>
    public enum eDefaultView
    {
        /// <summary>
        /// Table View
        /// </summary>
        Table = 0,
        /// <summary>
        /// Form View
        /// </summary>
        Form = 1,
        /// <summary>
        /// IFrame View
        /// </summary>
        IFrame = 2
    }

    /// <summary>
    /// Options for the NMI Combo Box
    /// </summary>
    public class TheComboOptions
    {
        /// <summary>
        /// Name
        /// </summary>
        public string N { get; set; }
        /// <summary>
        /// Value
        /// </summary>
        public string V { get; set; }
        /// <summary>
        /// Group Name
        /// </summary>
        public string G { get; set; }
        /// <summary>
        /// HTML Markup
        /// </summary>
        public string H { get; set; }
        /// <summary>
        /// Disabled
        /// </summary>
        public string D { get; set; }

        /// <summary>
        /// Super Group (coming soon for ThingPicker: S=EngineName G=DeviceType N=FriendlyName V=cdeMID)
        /// </summary>
        public string S { get; set; }
    }

    public class eNMIEvents
    {
        /// <summary>
        /// This event is called when a NMIControl has set the "OnLoaded" property
        /// The cdeMID of TheFieldInfo will be added to the Event Name (i.e: "NMI_FLD_LOADED:{some Guid}"
        /// </summary>
        /// <seealso cref="T:nsCDEngine.ViewModels.TheProcessMessage">The Process Message</seealso>
        public const string FieldLoaded = "NMI_FLD_LOADED";
    }

    /// <summary>
    /// TRF= Table/Row/Field - a precise location of a Data Item inside a relational Table
    /// </summary>
    public class TheTRF
    {
        /// <summary>
        /// The Table Name of the source table
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// The Row Number in the source Table
        /// </summary>
        public int RowNo { get; set; }

        /// <summary>
        /// TheFieldInfo Meta Data of the Field - describes how the field has to be rendered
        /// </summary>
        public TheFieldInfo FldInfo { get; set; }

        /// <summary>
        /// Contains a filter the Table has to apply a first before the RowNo is used
        /// </summary>
        public string RowFilter { get; set; }

        /// <summary>
        /// Name of the Field
        /// </summary>
        public string FldName { get; set; }

        /// <summary>
        /// If multiple Models are used (Each Plug-In Engine/Service has an own Data Model) this points at the ModelGuid(ID)
        /// </summary>
        public Guid ModelID { get; set; }

        /// <summary>
        /// The RowID can be used to reference a specific cdeMID of the row instead of the RowNo
        /// </summary>
        public string RowID { get; set; }

        public TheTRF(string pTN, int pRN, TheFieldInfo pFldInfo)
        {
            TableName = pTN;
            RowNo = pRN;
            FldInfo = pFldInfo;
        }

        public string GetHash()
        {
            string ret = TableName + RowNo;
            if (FldInfo != null)
                ret += FldInfo.FldOrder;
            return ret;
        }
    }

    internal class TheNMIModel
    {
        public bool IsRootVisualSet { get; set; }

        public long ScreenTimeout { get; set; }
        public string AdditionalSLParameter { get; set; }
        public string DefaultDashpanelIcon { get; set; }
        public string DefaultDashpanelBackground { get; set; }

        public Guid StartScreen = Guid.Empty;
        public Guid MainDashboardScreen = TheNMIEngine.eNMIPortalDashboard;
        public Guid AdminPortal1 = TheNMIEngine.eNMIPortalDashboard;
        public Guid AdminPortal2 = TheNMIEngine.eNMIPortalDashboard;

        internal string MyCurrentScreen { get; set; }
        internal string MyLastScreen { get; set; }
        public Dictionary<string, Type> MyCDEPluginUXTypes;

        internal TheStorageMirror<TheDashboardInfo> MyDashboards;
        internal TheStorageMirror<TheDashPanelInfo> MyDashComponents;
        internal TheStorageMirror<TheFormInfo> MyForms;
        internal TheStorageMirror<TheFieldInfo> MyFields;
        internal TheStorageMirror<TheChartDefinition> MyChartScreens;
        internal TheStorageMirror<TheControlType> MyControlRegistry;

        internal TheStorageMirror<ThePageBlockTypes> MyPageBlockTypes;
        internal TheStorageMirror<ThePageContent> MyPageContentStore;
        internal TheStorageMirror<ThePageBlocks> MyPageBlocksStore;
        internal TheStorageMirror<ThePageDefinition> MyPageDefinitionsStore;

        internal Action eventModelIsReady;

        internal List<string> ResourceNames;
        internal Assembly ExeAssembly;
        private bool ModelIsReady = false;
        private bool HasModelReadyFired = false;
        public TheNMIModel()
        {
            SetDefaults();
        }

        public void SetDefaults()
        {
            ScreenTimeout = 30;
            AdditionalSLParameter = "";
            DefaultDashpanelIcon = "";
            DefaultDashpanelBackground = "";

            StartScreen = Guid.Empty;
            MainDashboardScreen = TheNMIEngine.eNMIPortalDashboard;
            AdminPortal1 = TheNMIEngine.eNMIPortalDashboard;
            AdminPortal2 = TheNMIEngine.eNMIPortalDashboard;
        }

        public void Init()
        {
            MyCDEPluginUXTypes = new Dictionary<string, Type>();     //DIC-Allowed  STRING

            TheBaseEngine.WaitForEnginesStarted(EngineHasStarted);
        }

        private bool IsStorageRegistered;

        private void EngineHasStarted(ICDEThing pthing, object para)
        {
            if (!IsStorageRegistered)
            {
                IsStorageRegistered = true;
                TheBaseEngine.WaitForStorageReadiness(sinkStorageStationIsReadyFired, true);
            }
        }

        private void sinkStorageStationIsReadyFired(ICDEThing sender, object pReady)
        {
            IBaseEngine tBase = TheCDEngines.MyNMIService.GetBaseEngine();
            if (tBase == null)
                return;
            if (pReady != null)
            {
                if (MyPageDefinitionsStore == null)
                {
                    bool DoStore = TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("StoreNMI"));
                    MyControlRegistry = new TheStorageMirror<TheControlType>(TheCDEngines.MyIStorageService) { IsRAMStore = true, BlockWriteIfIsolated = true };
                    MyControlRegistry.InitializeStore(false);

                    MyChartScreens = new TheStorageMirror<TheChartDefinition>(TheCDEngines.MyIStorageService) { IsRAMStore = true, IsCachePersistent = DoStore, BlockWriteIfIsolated = true, CacheTableName = "NMIChartScreens" };
                    MyChartScreens.InitializeStore(false);

                    MyFields = new TheStorageMirror<TheFieldInfo>(TheCDEngines.MyIStorageService) { IsRAMStore = true, IsCachePersistent = DoStore, BlockWriteIfIsolated = true, CacheTableName = "NMIFields" };
                    MyFields.InitializeStore(false);

                    MyForms = new TheStorageMirror<TheFormInfo>(TheCDEngines.MyIStorageService) { IsRAMStore = true, IsCachePersistent = DoStore, BlockWriteIfIsolated = true, CacheTableName = "NMIForms" };
                    MyForms.InitializeStore(false);

                    MyDashboards = new TheStorageMirror<TheDashboardInfo>(TheCDEngines.MyIStorageService) { IsRAMStore = true, IsCachePersistent = DoStore, BlockWriteIfIsolated = true, CacheTableName = "NMIDashboards" };
                    MyDashboards.InitializeStore(false);

                    MyDashComponents = new TheStorageMirror<TheDashPanelInfo>(TheCDEngines.MyIStorageService) { IsRAMStore = true, IsCachePersistent = DoStore, BlockWriteIfIsolated = true, CacheTableName = "NMIDashPanels" };
                    MyDashComponents.InitializeStore(false);

                    MyPageBlockTypes = new TheStorageMirror<ThePageBlockTypes>(TheCDEngines.MyIStorageService) { BlockWriteIfIsolated = true };
                    MyPageDefinitionsStore = new TheStorageMirror<ThePageDefinition>(TheCDEngines.MyIStorageService) { BlockWriteIfIsolated = true };
                    MyPageContentStore = new TheStorageMirror<ThePageContent>(TheCDEngines.MyIStorageService) { BlockWriteIfIsolated = true };
                    MyPageBlocksStore = new TheStorageMirror<ThePageBlocks>(TheCDEngines.MyIStorageService) { BlockWriteIfIsolated = true };

                    if (tBase.GetEngineState().IsSimulated || "RAM".Equals(pReady.ToString()))
                    {
                        MyPageDefinitionsStore.IsCachePersistent = !TheCommonUtils.IsHostADevice(); // TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay;
                        MyPageDefinitionsStore.CacheStoreInterval = 5;
                        MyPageDefinitionsStore.IsStoreIntervalInSeconds = true;
                        MyPageDefinitionsStore.IsRAMStore = true;
                        MyPageContentStore.IsRAMStore = true;
                        MyPageBlocksStore.IsRAMStore = true;
                        MyPageBlockTypes.IsRAMStore = true;
                    }
                    else
                    {
                        MyPageDefinitionsStore.IsCached = true;
                        MyPageDefinitionsStore.CacheStoreInterval = 5;
                        MyPageDefinitionsStore.IsStoreIntervalInSeconds = true;
                        MyPageContentStore.IsCached = true;
                        MyPageBlocksStore.IsCached = true;
                        MyPageBlockTypes.IsCached = true;
                    }
                    MyPageDefinitionsStore.RegisterEvent(eStoreEvents.StoreReady, UpdatePageStore);
                    MyPageContentStore.RegisterEvent(eStoreEvents.StoreReady, UpdateContentStore);
                    MyPageBlocksStore.RegisterEvent(eStoreEvents.StoreReady, UpdateContentStore);
                    MyPageBlockTypes.RegisterEvent(eStoreEvents.StoreReady, UpdateBlockTypeStore);
                    if (!MyPageDefinitionsStore.IsRAMStore)
                    {
                        MyPageDefinitionsStore.CreateStore("MyPageDefinitionsStore", "Definitions of NMI Pages", null, false, false);
                        MyPageContentStore.CreateStore("MyPageContentStore", "Definitions of Page Content", null, false, false);
                        MyPageBlocksStore.CreateStore("MyPageBlocksStore", "Definitions Page Blocks", null, false, false);
                        MyPageBlockTypes.CreateStore("MyPageBlockTypes", "Definitions Blocks Types", null, false, false);
                    }
                    else
                    {
                        MyPageDefinitionsStore.InitializeStore(false);
                        MyPageContentStore.InitializeStore(false);
                        MyPageBlocksStore.InitializeStore(false);
                        MyPageBlockTypes.InitializeStore(false);
                    }

                    // ReSharper disable once SuspiciousTypeConversion.Global
                    if (TheCDEngines.MyCustomType is ICDEPlugin)
                    {
                        string tTempType = TheCDEngines.MyCustomType.FullName;
                        MyCDEPluginUXTypes.Add(tTempType, TheCDEngines.MyCustomType);
                    }

                    string temp = TheBaseAssets.MySettings.GetSetting("StartScreen");
                    if (!string.IsNullOrEmpty(temp)) StartScreen = TheCommonUtils.CGuid(temp);
                    temp = TheBaseAssets.MySettings.GetSetting("MainDashboardScreen");
                    if (!string.IsNullOrEmpty(temp)) MainDashboardScreen = TheCommonUtils.CGuid(temp);
                    temp = TheBaseAssets.MySettings.GetSetting("ScreenTimeout");
                    if (!string.IsNullOrEmpty(temp)) ScreenTimeout = TheCommonUtils.CInt(temp);

                    if (ResourceNames == null)
                    {
                        ExeAssembly = Assembly.GetCallingAssembly();
                        if (ExeAssembly != null)
                            ResourceNames = ExeAssembly.GetManifestResourceNames().ToList();
                    }

                    IsModelReady();
                }
            }
        }

        internal bool IsModelReady()
        {
            if (ModelIsReady) return ModelIsReady;
            ModelIsReady = TheCommonUtils.CBool(MyDashboards?.IsReady) && TheCommonUtils.CBool(MyDashComponents?.IsReady) && TheCommonUtils.CBool(MyPageBlocksStore?.IsReady) && TheCommonUtils.CBool(MyPageBlockTypes?.IsReady) &&
                            TheCommonUtils.CBool(MyPageDefinitionsStore?.IsReady) && TheCommonUtils.CBool(MyPageContentStore?.IsReady) && TheCommonUtils.CBool(MyControlRegistry?.IsReady) &&
                            TheCommonUtils.CBool(MyChartScreens?.IsReady) && TheCommonUtils.CBool(MyFields?.IsReady) && TheCommonUtils.CBool(MyForms?.IsReady);
            if (ModelIsReady && !HasModelReadyFired)
            {
                HasModelReadyFired = true;
                eventModelIsReady?.Invoke();
            }
            return ModelIsReady;
        }

        internal string GetLiveScreens()
        {
            StringBuilder tStr = new StringBuilder();
            bool IsFirst = true;
            List<TheThing> tList = TheThingRegistry.GetThingsByFunc(eEngineName.NMIService, s => s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID && TheThing.GetSafePropertyString(s, "DeviceType") == eKnownDeviceTypes.TheNMIScreen);
            if (tList != null)
            {
                foreach (TheThing tApp in tList)
                {
                    if (!IsFirst)
                        tStr.Append(";");
                    tStr.Append(tApp.FriendlyName);
                    IsFirst = false;
                }
            }
            return tStr.ToString();
        }

        internal string GetNMIViews()
        {
            StringBuilder tStr = new StringBuilder();
            bool IsFirst = true;
            //List<TheThing> tList = TheThingRegistry.GetThingsByProperty(eEngineName.NMIService, Guid.Empty, "DeviceType", eKnownDeviceTypes.TheNMIScene);
            List<TheThing> tList = TheThingRegistry.GetThingsByFunc(eEngineName.NMIService, s => s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID && (TheThing.GetSafePropertyString(s, "DeviceType") == eKnownDeviceTypes.TheNMIScene || (TheThing.GetSafePropertyString(s, "DeviceType") == eKnownDeviceTypes.TheNMIScreen && (s.UID == Guid.Empty || TheThing.GetSafePropertyBool(s, "IsPublic")))));
            if (tList != null)
            {
                foreach (TheThing tApp in tList)
                {
                    if (tApp.FriendlyName.EndsWith("-HIDE"))
                        continue;
                    if (!IsFirst)
                        tStr.Append(";");
                    tStr.Append($"{tApp.FriendlyName} (View)");
                    tStr.Append(":");
                    tStr.Append(tApp.cdeMID);
                    IsFirst = false;
                }
            }
            return tStr.ToString();
        }

        internal string GetControlTypeList()
        {
            StringBuilder tStr = new StringBuilder();
            //tStr.Append("Unknown or not found:");
            foreach (TheControlType tType in MyControlRegistry.TheValues)
            {
                tStr.Append(";");
                tStr.Append(tType.ControlName);
                tStr.Append(":");
                tStr.Append(tType.ControlType);
            }
            return tStr.ToString();
        }

        private void UpdateBlockTypeStore(object NOTUSED)
        {
            if (MyPageBlockTypes == null || MyPageBlockTypes.MyMirrorCache.Count > 0) return;
            MyPageBlockTypes.AddItems(new List<ThePageBlockTypes>()
                {
             new ThePageBlockTypes() { cdeMID=ThePageBlockTypes.HTML, BlockTypeName="HTML", Description="An HTML Block" },
             new ThePageBlockTypes() { cdeMID=new Guid("{4EF9B2CB-5D6D-471C-A164-4B3F026E9A8C}"), BlockTypeName="SIDEBLOCK", Description="A Side block" },
             new ThePageBlockTypes() { cdeMID=new Guid("{E02CB607-BCC6-439D-B0C4-F69B10982E39}"),  BlockTypeName="BULLETS", Description="An List of bullets" },
             new ThePageBlockTypes() { cdeMID=ThePageBlockTypes.IMAGE,  BlockTypeName="IMAGE", Description="Image" },
             new ThePageBlockTypes() { cdeMID=ThePageBlockTypes.CONTENT,  BlockTypeName="CONTENT", Description="A list of Content Blocks" }
                }, null);
            IsModelReady();
        }
        internal void UpdatePageStore(object pID)
        {
            if (MyPageDefinitionsStore == null || MyPageDefinitionsStore.MyMirrorCache.Count == 0) return;
            MyPageDefinitionsStore.WriteCache(sinkValidatePages);
            IsModelReady();
        }
        private void sinkValidatePages(TheStorageMirror<ThePageDefinition>.StoreResponse tRes)
        {
            if (tRes == null || tRes.HasErrors || tRes.MyRecords.Count == 0)
                timerPageReinit = new Timer(UpdatePageStore, tRes, 10000, Timeout.Infinite);
            else
            {
                if (timerPageReinit != null)
                {
                    timerPageReinit.Dispose();
                    timerPageReinit = null;
                }
                TheBaseAssets.MySYSLOG.WriteToLog(10005, new TSM(eEngineName.NMIService, "Pages Verified!", eMsgLevel.l3_ImportantMessage));
                IsModelReady();
            }
        }
        private Timer timerPageReinit;

        internal void UpdateContentStore(object pID)
        {
            if (MyPageContentStore == null || MyPageContentStore.MyMirrorCache.Count == 0) return;
            MyPageContentStore.WriteCache(sinkValidateContent);
        }
        private void sinkValidateContent(TheStorageMirror<ThePageContent>.StoreResponse tRes)
        {
            if (tRes == null || tRes.HasErrors || tRes.MyRecords.Count == 0)
                timerContentReinit = new Timer(UpdateContentStore, tRes, 10000, Timeout.Infinite);
            else
            {
                if (timerContentReinit != null)
                {
                    timerContentReinit.Dispose();
                    timerContentReinit = null;
                }
                TheBaseAssets.MySYSLOG.WriteToLog(10005, new TSM(eEngineName.NMIService, "Content Verified!", eMsgLevel.l3_ImportantMessage));
                IsModelReady();
            }
        }
        private Timer timerContentReinit;

        internal void UpdateBlockStore(object pID)
        {
            if (MyPageBlocksStore == null || MyPageBlocksStore.MyMirrorCache.Count == 0) return;
            MyPageBlocksStore.WriteCache(sinkValidateBlocks);
        }
        private void sinkValidateBlocks(TheStorageMirror<ThePageBlocks>.StoreResponse tRes)
        {
            if (tRes == null || tRes.HasErrors || tRes.MyRecords.Count == 0)
                timerBlocksReinit = new Timer(UpdateBlockStore, tRes, 10000, Timeout.Infinite);
            else
            {
                if (timerBlocksReinit != null)
                {
                    timerBlocksReinit.Dispose();
                    timerBlocksReinit = null;
                }
                TheBaseAssets.MySYSLOG.WriteToLog(10005, new TSM(eEngineName.NMIService, "Blocks Verified!", eMsgLevel.l3_ImportantMessage));
                IsModelReady();
            }
        }
        private Timer timerBlocksReinit;


    }

    public enum ePaymentTypes
    {
        NotSet = 0,
        CreditCard = 1,
        Subscription = 2,
        PayPerUse = 3,
        Max = 4
    }

    public class ThePaymentOptions : TheDataBase
    {
        public ePaymentTypes PaymentType { get; set; }
        public string FriendlyName { get; set; }
        public string AccountID { get; set; }
        public string CreditCardNumber { get; set; }
    }

    public class TheAccessStatistics : TheDataBase
    {
        public string RelayNode { get; set; }
        public string UserID { get; set; }
        public DateTimeOffset LastUpdate { get; set; }
        public long LoginCounter { get; set; }
        public long PageCounter { get; set; }
        public long RequestCounter { get; set; }
        public long RestartCounter { get; set; }
    }

    public class TheDrawingPoint
    {
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
        public double t { get; set; }
    }
    public class ThePointer
    {
        public TheDrawingPoint Position { get; set; }
        public TheDrawingPoint StartPosition { get; set; }
        public TheDrawingPoint AdjPosition { get; set; }
        public TheDrawingPoint Ele2DocPosition { get; set; }
        public TheDrawingPoint Shift { get; set; }
        public int Pressure { get; set; }
        public int Identifier { get; set; }
        public int pointerType { get; set; }
        public int pointerEvent { get; set; }
        public bool IsOnObject { get; set; }

        public int Buttons { get; set; }

        public override string ToString()
        {
            return string.Format("x:{0} y:{1} but:{2} PE:{3}", AdjPosition.x, AdjPosition.y, Buttons, pointerEvent);
        }
    }

    public class TheKey
    {
        public bool altKey { get; set; }
        public string Cchar { get; set; }
        public int charCode { get; set; }
        public bool ctrlKey { get; set; }
        public string key { get; set; }
        public int keyCode { get; set; }
        public string locale { get; set; }
        public int location { get; set; }
        public bool metaKey { get; set; }
        public bool repeat { get; set; }
        public bool shiftKey { get; set; }
        public int which { get; set; }
        public int JOYSTICK { get; set; }
        public int LEFT { get; set; }
        public int MOBILE { get; set; }
        public int NUMPAD { get; set; }
        public int RIGHT { get; set; }
        public int STANDARD { get; set; }

        public int eventPhase { get; set; }
        public bool isTrusted { get; set; }
        public bool returnValue { get; set; }
        public double timeStamp { get; set; }
        public string type { get; set; }
    }
    public class TheDrawingObject
    {
        public int Type { get; set; }   ///1=Rectangle; 2=PolyLine ; 3=Text
        public int StrokeThickness { get; set; }
        public string Fill { get; set; }
        public string Foreground { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Visibility { get; set; }
        public object ComplexData { get; set; }
        public bool HasEnded { get; set; }
        public bool IsTemp { get; set; }
        public string ID { get; set; }
    }

    ///C-DMyForms Definitions

    public class TheFieldInfo : TheMetaDataBase
    {
        public Guid FormID { get; set; }
        public int FldOrder { get; set; }
        public string DataItem { get; set; }
        /// <summary>
        /// Binary Flags for the Field:
        /// 1= Show content as * (Password);
        /// 2= Write Enabled Field (Sends update to Plugin)
        /// 4 =Hide from Mobile
        /// 8 =Hide From Table
        /// 16=Hide From Form
        /// 32=Advanced Editior
        /// 64=Live Update
        /// 128=Only Show if user is on first node
        /// </summary>
        public int Flags { get; set; }
        public eFieldType Type { get; set; }


        [IgnoreDataMember]
        public int FldWidth
        {
            get
            {
                return TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(PropertyBag, "FldWidth", "="));
            }
            set
            {
                if (value > 0)
                    ThePropertyBag.PropBagUpdateValue(PropertyBag, "FldWidth", "=", TheCommonUtils.CStr(value));
            }
        }

        [IgnoreDataMember]
        public string DefaultValue
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "DefaultValue", "=");
            }
            set
            {
                if (!TheCommonUtils.IsNullOrWhiteSpace(value))
                    ThePropertyBag.PropBagUpdateValue(PropertyBag, "DefaultValue", "=", value);
            }
        }

        [IgnoreDataMember]
        public string Format
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "Format", "=");
            }
            set
            {
                if (!TheCommonUtils.IsNullOrWhiteSpace(value))
                    ThePropertyBag.PropBagUpdateValue(PropertyBag, "Format", "=", value);
            }
        }

        [IgnoreDataMember]
        public string Header
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "Title", "=");
            }
            set
            {
                if (!TheCommonUtils.IsNullOrWhiteSpace(value))
                    ThePropertyBag.PropBagUpdateValue(PropertyBag, "Title", "=", value);
            }
        }

        [IgnoreDataMember]
        public int TileLeft
        {
            get
            {
                return TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(PropertyBag, "TileLeft", "="));
            }
            set
            {
                if (value > 0)
                    ThePropertyBag.PropBagUpdateValue(PropertyBag, "TileLeft", "=", TheCommonUtils.CStr(value));
            }
        }

        [IgnoreDataMember]
        public int TileTop
        {
            get
            {
                return TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(PropertyBag, "TileTop", "="));
            }
            set
            {
                if (value > 0)
                    ThePropertyBag.PropBagUpdateValue(PropertyBag, "TileTop", "=", TheCommonUtils.CStr(value));
            }
        }

        [IgnoreDataMember]
        public int TileWidth
        {
            get
            {
                return TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(PropertyBag, "TileWidth", "="));
            }
            set
            {
                if (value > 0)
                    ThePropertyBag.PropBagUpdateValue(PropertyBag, "TileWidth", "=", TheCommonUtils.CStr(value));
            }
        }

        [IgnoreDataMember]
        public int TileHeight
        {
            get
            {
                return TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(PropertyBag, "TileHeight", "="));
            }
            set
            {
                if (value > 0)
                    ThePropertyBag.PropBagUpdateValue(PropertyBag, "TileHeight", "=", TheCommonUtils.CStr(value));
            }
        }

        private ThePropertyBag _propertyBag;
        public ThePropertyBag PropertyBag
        {
            get
            {
                if (_propertyBag == null)
                    _propertyBag = new ThePropertyBag();
                return _propertyBag;
            }
            set
            {
                if (_propertyBag == null)
                {
                    _propertyBag = value;
                }
                else
                {
                    _propertyBag.MergeBag(value); // TODO flag duplicates and fail or update
                }
            }
        }

        internal cdeConcurrentDictionary<eWebPlatform, ThePropertyBag> PlatBag;

        private Action<TheFieldInfo, cdeP> eventPropertyChanged;
        private Action<TheFieldInfo, cdeP> eventPropertyChangedByUX;

        /// <summary>
        /// Register for general property changes (Backend and FrontEnd)
        /// </summary>
        /// <param name="pEventPropertyChanged"></param>
        public void RegisterPropertyChanged(Action<TheFieldInfo, cdeP> pEventPropertyChanged)
        {
            eventPropertyChanged += pEventPropertyChanged;
        }

        /// <summary>
        /// Register for just frontend changes to the field (by user through NMI)
        /// </summary>
        /// <param name="pEventPropertyChangedByUX"></param>
        public void RegisterPropertyChangedByUX(Action<TheFieldInfo, cdeP> pEventPropertyChangedByUX)
        {
            eventPropertyChangedByUX += pEventPropertyChangedByUX;
        }

        /// <summary>
        /// Registers a UX Event with the control described in this TheFieldInfo.
        /// </summary>
        /// <param name="pBaseThing">Thing that owns this control.</param>
        /// <param name="pEvent">eUXEvent enum can be used to specify the event type.</param>
        /// <param name="pCookie">When the Event occurs the callback "object" parameter will contain a TSM.
        /// In the PLS of the TSM you will find the following syntax (TouchPoints:pCookie:StorageRecordID).
        /// This cookie will be in the second location</param>
        /// <param name="pCallback">Callback to be called when the event is fired.</param>
        public void RegisterUXEvent(TheThing pBaseThing, string pEvent, string pCookie, Action<ICDEThing, object> pCallback)
        {
            RegisterUXEvent(pBaseThing, pEvent, pCookie, pCallback, false);
        }

        /// <summary>
        /// Registers a UX Event with the control described in this TheFieldInfo
        /// </summary>
        /// <param name="pBaseThing">Owner (form) of this control</param>
        /// <param name="pEvent">eUXEvent enum can be used to specify the event type</param>
        /// <param name="pCookie">When the Event occurs the callback "object" parameter will contain a TSM. In the PLS of the TSM you will find the following syntax (TouchPoints:pCookie:StorageRecordID). this cookie will be in the second location</param>
        /// <param name="pCallback">Callback to be called when the event was fired</param>
        /// <param name="rowsAreThings">Route event to the thing being clicked, if the control contains rows that are Things (i.e. a table showing a storage mirror of things)</param>
        public void RegisterUXEvent(TheThing pBaseThing, string pEvent, string pCookie, Action<ICDEThing, object> pCallback, bool rowsAreThings)
        {
            if (pCallback == null)
                return;
            if (PropertyBag == null)
                PropertyBag = new ThePropertyBag();
            string tVal;
            switch (pEvent)
            {
                case eUXEvents.OnClick:
                    if (!rowsAreThings)
                    {
                        //tVal = string.Format($"cdeCommCore.PublishToOwner('{pBaseThing.cdeMID}','{pBaseThing.EngineName}', '{pEvent}:'+TargetControl.GetProperty('ID') +':{pCookie}',Parameter +':{pCookie}:'+ (TargetControl.MyTRF && TargetControl.MyTRF.RowID?TargetControl.MyTRF.RowID:0),'{pBaseThing.cdeN}'); "); //TargetControl.MyTRF?TargetControl.MyTRF.GetNodeID(): only when rowarethings
                        tVal = $"PTOT:;:;{pBaseThing.cdeMID};:;{pBaseThing.EngineName};:;{pEvent};:;{pCookie};:;{pBaseThing.cdeN}";   //4.11
                    }
                    else
                    {
                        // CODE REVIEW: the TRF's MID is not always a thing mid (i.e. OPC Tag lists are not things and have a "Monitor as Property" button, so can't be used as the owner. What are the scenarios where it is a thing mid, different from the baseThing? Is there a way to dermine when it is, either here or in the browser?
                        // Making this opt-in via rowsAreThings parameter, unclear which callers need this
                        //tVal = string.Format($"cdeCommCore.PublishToOwner(TargetControl.MyTRF?TargetControl.MyTRF.GetMID():'{pBaseThing.cdeMID}','{pBaseThing.EngineName}', '{pEvent}:'+TargetControl.GetProperty('ID') +':{pCookie}',Parameter +':{pCookie}:'+ (TargetControl.MyTRF && TargetControl.MyTRF.RowID?TargetControl.MyTRF.RowID:0),TargetControl.MyTRF?TargetControl.MyTRF.GetNodeID():'{pBaseThing.cdeN}'); ");
                        tVal = $"PTOR:;:;{pBaseThing.cdeMID};:;{pBaseThing.EngineName};:;{pEvent};:;{pCookie};:;{pBaseThing.cdeN}";
                    }
                    break;
                default:
                    if (pCookie != null)
                    {
                        //tVal = $"if (PropertyName=='{pCookie}') {{{{ cdeCommCore.PublishToOwner(TargetControl.MyTRF?TargetControl.MyTRF.GetMID():'{pBaseThing.cdeMID}', '{pBaseThing.EngineName}', '{pEvent}:'+TargetControl.GetProperty('ID') +':{pCookie}',Parameter,TargetControl.MyTRF?TargetControl.MyTRF.GetNodeID():'{pBaseThing.cdeN}'); }}}}";
                        tVal = $"PTOR:{pCookie};:;{pBaseThing.cdeMID};:;{pBaseThing.EngineName};:;{pEvent};:;{pCookie};:;{pBaseThing.cdeN}";
                    }
                    else
                    {
                        //tVal = $"cdeCommCore.PublishToOwner(TargetControl.MyTRF?TargetControl.MyTRF.GetMID():'{pBaseThing.cdeMID}', '{pBaseThing.EngineName}', '{pEvent}:'+TargetControl.GetProperty('ID'),Parameter,TargetControl.MyTRF?TargetControl.MyTRF.GetNodeID():'{pBaseThing.cdeN}');";
                        tVal = $"PTOR:;:;{pBaseThing.cdeMID};:;{pBaseThing.EngineName};:;{pEvent};:;;:;{pBaseThing.cdeN}";
                    }
                    break;
            }
            ThePropertyBag.PropBagUpdateValue(PropertyBag, pEvent, "=", tVal, true);
            ThePropertyBag.PropBagUpdateValue(PropertyBag, "ID", "=", cdeMID.ToString());
            pBaseThing.RegisterEvent($"{pEvent}:{cdeMID}" + (pCookie != null ? $":{pCookie}" : ""), pCallback);
            if (pBaseThing.cdeMID != TheCDEngines.MyNMIService?.GetBaseThing().cdeMID && rowsAreThings)
                TheCDEngines.MyNMIService?.RegisterEvent($"{pEvent}:{cdeMID}" + (pCookie != null ? $":{pCookie}" : ""), pCallback);
        }

        /// <summary>
        /// Unregisters a UX event
        /// </summary>
        /// <param name="pBaseThing">Owner of the Control</param>
        /// <param name="pEvent">Event to unregister</param>
        /// <param name="pCookie">Cookie for the event</param>
        /// <param name="pCallback">Callback to unregister if null all callbacks are unregistered</param>
        /// <param name="rowsAreThings">Set if the event is on a table control that contains things</param>
        public void UnregisterUXEvent(TheThing pBaseThing, string pEvent, string pCookie, Action<ICDEThing, object> pCallback, bool rowsAreThings = false)
        {
            pBaseThing.UnregisterEvent($"{pEvent}:{cdeMID}" + (pCookie != null ? $":{pCookie}" : ""), pCallback);
            if (pBaseThing.cdeMID != TheCDEngines.MyNMIService?.GetBaseThing().cdeMID && rowsAreThings)
                TheCDEngines.MyNMIService?.UnregisterEvent($"{pEvent}:{cdeMID}" + (pCookie != null ? $":{pCookie}" : ""), pCallback);
        }

        public bool PropBagUpdateValue(string pName, string Seperator, string pValue)
        {
            return ThePropertyBag.PropBagUpdateValue(PropertyBag, pName, Seperator, pValue, false);
        }

        public bool PropBagRemoveName(string pName)
        {
            return ThePropertyBag.PropBagRemoveName(PropertyBag, pName, "=");
        }

        public bool PropBagRemoveName(string pName, string Seperator)
        {
            return ThePropertyBag.PropBagRemoveName(PropertyBag, pName, Seperator);
        }

        public string PropBagGetValue(string pName)
        {
            return ThePropertyBag.PropBagGetValue(PropertyBag, pName, "=");
        }

        public void SetUXProperty(Guid pOrg, string pProps)
        {
            SetUXProperty(pOrg, pProps, false);
        }

        public void SetUXProperty(Guid pOrg, string pProps, bool DisableModelUpdate)
        {
            if (!DisableModelUpdate)
                ThePropertyBag.PropBagUpdateValues(PropertyBag, pProps, "=");
            TheNMIEngine.SetUXProperty(pOrg, cdeMID, pProps, null, $"{((int)Type)};{this.FldOrder}");
        }

        public void UpdateUXProperties(Guid pOrg)
        {
            List<string> tNes = new List<string>();
            foreach (string a in PropertyBag)
            {
                if (!a.StartsWith("OnThingEvent"))
                    tNes.Add(a);
            }
            string tList = TheCommonUtils.CListToString(tNes, ":;:");
            TheNMIEngine.SetUXProperty(pOrg, cdeMID, tList, null, $"{((int)Type)};{this.FldOrder}");
        }

        public bool SetParent(TheFieldInfo pParentNode)
        {
            if (pParentNode == null) return false;
            ThePropertyBag.PropBagUpdateValue(PropertyBag, "ParentID", "=", pParentNode.cdeMID.ToString(), false);
            ThePropertyBag.PropBagUpdateValue(PropertyBag, "ParentFld", "=", pParentNode.FldOrder.ToString(), false);
            return true;
        }
        public bool SetParent(int pParentNode)
        {
            //if (pParentNode ==0) return false;
            ThePropertyBag.PropBagUpdateValue(PropertyBag, "ParentFld", "=", pParentNode.ToString(), false);
            return true;
        }

        private void Reset()
        {
            PropertyBag = new ThePropertyBag();
            PlatBag = new cdeConcurrentDictionary<eWebPlatform, ThePropertyBag>();
        }
        public TheFieldInfo()
        {
            Reset();
        }
        public TheFieldInfo(TheThing pBaseThing, string OnUpdateName, int pFldOrder, int pFlag, int pACL)
        {
            Reset();
            FldOrder = pFldOrder;
            Flags = pFlag;
            if (pBaseThing != null)
            {
                if (pACL < 0)
                    pACL = pBaseThing.cdeA;
                cdeO = pBaseThing.cdeMID;
            }
            cdeA = pACL;
            if (!string.IsNullOrEmpty(OnUpdateName))
            {
                if ((Flags & 64) != 0)
                    ThePropertyBag.PropBagUpdateValue(PropertyBag, "OnThingEvent", "=", string.Format("%cdeMID%;{0}", OnUpdateName));
                else
                {
                    if (pBaseThing != null)
                        ThePropertyBag.PropBagUpdateValue(PropertyBag, "OnThingEvent", "=", string.Format("{0};{1}", pBaseThing.cdeMID, OnUpdateName));
                }
                DataItem = string.Format("MyPropertyBag.{0}.Value", OnUpdateName);
            }
        }
        public TheFieldInfo(int pFldOrder)
        {
            Reset();
            FldOrder = pFldOrder;
        }

        public override string ToString()
        {
            return string.Format("{2} {0} {1}", DataItem, Type.ToString(), Header);
        }

        internal void sinkUXUpdate(cdeP pProp)
        {
            eventPropertyChangedByUX?.Invoke(this, pProp);
        }

        public bool AddOrUpdatePlatformBag(eWebPlatform pPlat, ThePropertyBag pBagItem)
        {
            if (!PlatBag.ContainsKey(pPlat))
                PlatBag[pPlat] = pBagItem;
            else
                PlatBag[pPlat].MergeBag(pBagItem);
            return true;
        }

        internal void sinkUpdate(cdeP pProp)
        {
            if (pProp == null) return;
            if ((pProp.cdeE & 0x40) != 0)
            {
                //ThePropertyBag.PropBagUpdateValue(PropertyBag, pProp.Name, "=", pProp.ToString());
                SetUXProperty(Guid.Empty, $"{pProp.Name}={pProp.ToString()}");
            }
            if (eventPropertyChanged != null)
                eventPropertyChanged(this, pProp);
            else
            {
                switch (pProp.Name.ToLower())
                {
                    case "text":
                        Header = pProp.ToString();
                        break;
                }
            }
        }

        internal TheFieldInfo Clone(bool CreateFastCache = false)
        {
            var locField = new TheFieldInfo
            {
                DataItem = this.DataItem,
                Type = this.Type,
                Flags = this.Flags,
                FldOrder = this.FldOrder,
                FormID = this.FormID,
                PropertyBag = this.PropertyBag.Clone(CreateFastCache)
            };
            if (PlatBag != null)
            {
                foreach (eWebPlatform t in PlatBag.Keys)
                    locField.PlatBag[t] = this.PlatBag[t].Clone(CreateFastCache);
            }
            this.CloneTo(locField);
            return locField;
        }

        internal TheFieldInfo GetLocalizedField(int lcid, string pOwnerEngine)
        {
            var locField = new TheFieldInfo
            {
                DataItem = this.DataItem,
                Type = this.Type,
                Flags = this.Flags,
                FldOrder = this.FldOrder,
                FormID = this.FormID,
                PropertyBag = ThePropertyBag.GetLocalizedBag(this.PropertyBag, lcid, pOwnerEngine),
            };
            if (PlatBag != null)
            {
                foreach (eWebPlatform t in PlatBag.Keys)
                    locField.PlatBag[t] = ThePropertyBag.GetLocalizedBag(PlatBag[t], lcid, pOwnerEngine);
            }
            this.CloneTo(locField);
            return locField;
        }
    }

    /// <summary>
    /// List of all Field(Control)Types of the NMI Engine
    /// </summary>
    public enum eFieldType
    {
        //Dummy = 0,

        /// <summary>
        /// nmiCtrlSingledEnded
        /// </summary>
        SingleEnded = 1,
        /// <summary>
        /// Creates a dropdown combo box for choices
        /// </summary>
        ComboBox = 2,
        //Radio = 3,
        /// <summary>
        /// creates a single check box
        /// </summary>
        SingleCheck = 4,
        /// <summary>
        /// Creates a text area input field
        /// </summary>
        TextArea = 5,
        /// <summary>
        /// creates a Yes/No choice box
        /// </summary>
        YesNo = 6,
        //YesNoNa = 7,
        /// <summary>
        /// Creates an input box for Time
        /// </summary>
        Time = 8,
        /// <summary>
        /// Creates an input box for a timespan
        /// </summary>
        TimeSpan = 9,
        /// <summary>
        /// Creates an input box for a password showing * during input. The corresponding dataitem will be encrypted
        /// </summary>
        Password = 10,
        //SubmitButton = 11,

        /// <summary>
        /// Creates a number input field
        /// </summary>
        Number = 12,
        //Region = 13,
        /// <summary>
        /// Creates a dropdown box for countries
        /// </summary>
        Country = 14,
        /// <summary>
        /// Creates an combo box with True and False as options
        /// </summary>
        TrueFalse = 15,
        /// <summary>
        /// Creats an input field for an email address
        /// </summary>
        eMail = 16,
        /// <summary>
        /// Creates a combo box with an optional input field
        /// </summary>
        ComboOption = 17,
        /// <summary>
        /// Creates a selector box for picking a month
        /// </summary>
        Month = 18,
        /// <summary>
        /// Button with special meaning in forms
        /// </summary>
        FormButton = 19,
        /// <summary>
        /// Creates an output field for text
        /// </summary>
        SmartLabel = 20,
        /// <summary>
        /// Creates in input field for Date and Time via a friendly input box
        /// </summary>
        DateTime = 21,
        /// <summary>
        /// Creates a button
        /// </summary>
        TileButton = 22,    //ctrlTileButton
        /// <summary>
        /// Creates an inline Table
        /// </summary>
        Table = 23,
        /// <summary>
        /// Creates an field of checkboxes representing each bit in a number
        /// </summary>
        CheckField = 24,
        /// <summary>
        /// Coming Soon: A grid of Radio Options
        /// </summary>
        //RadioGrid = 25,
        Screen = 26,
        //TableCell = 27,

        //TileEntry = 28,
        /// <summary>
        /// Creates a image field
        /// </summary>
        Picture = 29,   //ctrlZoomImage
        /// <summary>
        /// Creates a blank canvasdraw (Base for several other controls)
        /// </summary>
        CanvasDraw = 30,
        /// <summary>
        /// Creates an input field for a URL. The syntax of the Url is checked
        /// </summary>
        URL = 31,
        /// <summary>
        /// Creates in input field for currency
        /// </summary>
        Currency = 32,

        /// <summary>
        /// Creates an endless slider to increase decrease values
        /// </summary>
        Slider = 33,
        /// <summary>
        /// Creates a single BarChart
        /// </summary>
        BarChart = 34,
        //Toast = 35,
        /// <summary>
        /// Creates an ink/touch receiving area
        /// </summary>
        TouchDraw = 36,
        //Popup = 37,
        //DrawOverlay = 38,
        //Accordion = 39,      //TODO: Use 39 for next control i.e. ColorPicker
        /// <summary>
        /// Creates an area to drop a file on
        /// </summary>
        DropUploader = 40,
        /// <summary>
        /// Creates a 1x1 Button that can hide other buttons underneath
        /// </summary>
        ReveilButton = 41,
        /// <summary>
        /// Creates a small quarter tile Pin Button
        /// </summary>
        PinButton = 42,
        //CenteredTable = 43,   //cdeNMI Only
        Dashboard = 44,
        /// <summary>
        /// Creates a Form view
        /// </summary>
        FormView = 45,
        //TouchOverlay = 46,
        /// <summary>
        /// Display the MuTLock (TM)
        /// </summary>
        MuTLock = 47,
        /// <summary>
        /// Displays a progressbar
        /// </summary>
        ProgressBar = 48,
        /// <summary>
        /// Greates a group that can be used to group elements together
        /// </summary>
        TileGroup = 49,
        /// <summary>
        /// Displays a Video element
        /// </summary>
        VideoViewer = 50,
        /// <summary>
        /// Defines the entry point for user controls in other NMI libraries
        /// </summary>
        UserControl = 51,
        //TableRow=52,  //cdeNNI Oonly
        /// <summary>
        /// Input of IP Addresses
        /// </summary>
        IPAddress = 53,
        /// <summary>
        /// Creates an area for shapes
        /// </summary>
        Shape = 54,
        /// <summary>
        /// Create a tilegroup that can be collapsed
        /// </summary>
        CollapsibleGroup = 55,
        /// <summary>
        /// nmiCtrlAboutButton / cdeNMI.ctrlAboutButton
        /// </summary>
        AboutButton = 56,
        /// <summary>
        /// nmiCtrlStatusLight / cdeNMI.ctrlStatusLight
        /// </summary>
        StatusLight = 57,
        /// <summary>
        /// nmiCtrlFacePlate    / cdeNMI.ctrlFacePlate
        /// </summary>
        FacePlate = 58,

        //New controls in 4.1
        LoginScreen = 59,               // TheLoginScreen
        ShapeRecognizer = 60,               // TheShapeRecognizer
        ScreenManager = 61,       //ScreenManager
        LogoButton = 62,            //The Logo Button
        ThingPicker = 63,           //TheThing Picker...old based on ComboBox - soon new control!
        ImageSlider = 64,
        CircularGauge = 65,
        SmartGauge = 66,
        IFrameView = 67,
        PropertyPicker = 68,
        PropertyPickerCtrl = 69,
        TheToolTip = 70,
        ComboLookup = 71,

        UserMenu = 72,
        MeshPicker = 73,
        /// <summary>
        /// Shows an icon created from a hash in the "Value" property
        /// </summary>
        HashIcon = 74,
        /// <summary>
        /// Shows the client-certificate picker
        /// </summary>
        CertPicker = 75
    }

    public class TheFieldType : TheMetaDataBase
    {
        public eFieldType ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool PayFor { get; set; }
        public string HelpText { get; set; }
        public long SampleForm { get; set; }
        public int TimeCostInSec { get; set; }
        public long DesignForm { get; set; }
        public bool IsMultipleChoice { get; set; }
        public string SQLType { get; set; }
        public int FieldLength { get; set; }
    }


    public class TheDashPanelInfo : TheMetaDataBase
    {
        public string ControlClass { get; set; }

        public bool IsFullSceen { get; set; }

        public int FldOrder { get; set; }

        /// <summary>
        /// 1=Show On Local Relay
        /// 2=Show on Cloud Relay
        /// 4=Hide on Mobile
        /// 8=Do not include with ShowAll
        /// </summary>
        public int Flags { get; set; }

        public string HtmlContent { get; set; }
        public string HtmlSource { get; set; }

        [IgnoreDataMember]
        public string Category
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "Category", "=");
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "Category", "=", value);
            }
        }

        [IgnoreDataMember]
        public string PanelTitle
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "Caption", "=");
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "Caption", "=", value);
            }
        }

        [IgnoreDataMember]
        public bool IsControl
        {
            get
            {
                return TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(PropertyBag, "IsControl", "="));
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "IsControl", "=", value.ToString());
            }
        }


        [IgnoreDataMember]
        public bool IsMTBlocked
        {
            get
            {
                return TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(PropertyBag, "IsMTBlocked", "="));
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "IsMTBlocked", "=", value.ToString());
            }
        }

        [IgnoreDataMember]
        public bool IsPinned
        {
            get
            {
                return TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(PropertyBag, "IsPinned", "="));
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "IsPinned", "=", value.ToString());
            }
        }

        [IgnoreDataMember]
        public string Navigator
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "Navigator", "=");
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "Navigator", "=", value);
            }
        }

        [IgnoreDataMember]
        public string Description
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "Description", "=");
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "Description", "=", value);
            }
        }

        private ThePropertyBag _propertyBag;
        public ThePropertyBag PropertyBag
        {
            get
            {
                if (_propertyBag == null)
                    _propertyBag = new ThePropertyBag();
                return _propertyBag;
            }
            set
            {
                if (_propertyBag == null)
                {
                    _propertyBag = value;
                }
                else
                {
                    _propertyBag.MergeBag(value); // TODO flag duplicates and fail or update
                }
            }
        }
        internal cdeConcurrentDictionary<eWebPlatform, ThePropertyBag> PlatBag;
        public bool AddOrUpdatePlatformBag(eWebPlatform pPlat, ThePropertyBag pBagItem)
        {
            if (!PlatBag.ContainsKey(pPlat))
                PlatBag[pPlat] = pBagItem;
            else
                PlatBag[pPlat].MergeBag(pBagItem);
            return true;
        }

        private void Reset()
        {
            PropertyBag = new ThePropertyBag();
            PlatBag = new cdeConcurrentDictionary<eWebPlatform, ThePropertyBag>();
        }
        private Action<TheDashPanelInfo, cdeP> eventPropertyChanged;

        public void RegisterPropertyChanged(Action<TheDashPanelInfo, cdeP> pEventPropertyChanged)
        {
            eventPropertyChanged += pEventPropertyChanged;
        }

        [Obsolete("Use with MyBaseThing")]
        public TheDashPanelInfo()
        {
            Reset();
        }
        public TheDashPanelInfo(TheThing pBaseThing)
        {
            if (pBaseThing == null) return;
            Reset();
            cdeO = pBaseThing.cdeMID;
        }
        public TheDashPanelInfo(Guid pOwnerMID)
        {
            Reset();
            cdeO = pOwnerMID;
        }

        [Obsolete("Use with MyBaseThing")]
        public TheDashPanelInfo(Guid pKey, string pPanelTitle, string pControlClass)
        {
            Reset();
            cdeMID = pKey;
            PanelTitle = pPanelTitle;
            ControlClass = pControlClass;
        }
        public TheDashPanelInfo(TheThing pBaseThing, Guid pKey, string pPanelTitle, string pControlClass)
        {
            Reset();
            cdeO = pBaseThing.cdeMID;
            cdeMID = pKey;
            PanelTitle = pPanelTitle;
            ControlClass = pControlClass;
        }


        [Obsolete] // CODE REVIEW: This seems to be only used internally/by only two of our plug-ins: Problem is that it doesn't set the cdeO by default, which can break loc string lookup. Can we just remove it?
        public TheDashPanelInfo(Guid pKey, string pPanelTitle, string pControlClass, int pPanelColor, string pSmallBackground, string pMediumBackground, string pFullscreenBackground, bool bIsFullscreen, bool bIsControl)
        {
            PropertyBag = new ThePropertyBag();
            cdeMID = pKey;
            PanelTitle = pPanelTitle;
            ControlClass = pControlClass;
            ThePropertyBag.PropBagUpdateValue(PropertyBag, "BackgroundSmall", "=", pSmallBackground);
            ThePropertyBag.PropBagUpdateValue(PropertyBag, "BackgroundMedium", "=", pMediumBackground);
            ThePropertyBag.PropBagUpdateValue(PropertyBag, "BackgroundFull", "=", pFullscreenBackground);
            ThePropertyBag.PropBagUpdateValue(PropertyBag, "PanelColor", "=", pPanelColor.ToString());
            IsFullSceen = bIsFullscreen;
            IsControl = bIsControl;
        }
        [Obsolete] // CODE REVIEW: This seems to be only not be used in any of our plug-ins: Problem is that it doesn't set the cdeO by default, which can break loc string lookup. Can we just remove it?
        public TheDashPanelInfo(Guid pKey, string pPanelTitle, string pControlClass, bool pIsPinned, int pPanelColor, string pSmallBackground, string pMediumBackground, string pFullscreenBackground, bool pMTBlocked, bool bIsFullscreen, bool bIsControl)
        {
            PropertyBag = new ThePropertyBag();
            cdeMID = pKey;
            PanelTitle = pPanelTitle;
            ControlClass = pControlClass;
            IsPinned = pIsPinned;
            ThePropertyBag.PropBagUpdateValue(PropertyBag, "BackgroundSmall", "=", pSmallBackground);
            ThePropertyBag.PropBagUpdateValue(PropertyBag, "BackgroundMedium", "=", pMediumBackground);
            ThePropertyBag.PropBagUpdateValue(PropertyBag, "BackgroundFull", "=", pFullscreenBackground);
            ThePropertyBag.PropBagUpdateValue(PropertyBag, "PanelColor", pPanelColor.ToString(), "=");
            IsMTBlocked = pMTBlocked;
            IsFullSceen = bIsFullscreen;
            IsControl = bIsControl;
        }

        internal void FireUpdate()
        {
            eventPropertyChanged?.Invoke(this, null);
        }

        public void SetUXProperty(Guid pOrg, string pProps)
        {
            ThePropertyBag.PropBagUpdateValues(PropertyBag, pProps, "=");
            TheNMIEngine.SetUXProperty(pOrg, cdeMID, pProps, null, $"{(int)eFieldType.TileButton};{this.FldOrder}");
        }
        public void SetUXProperty(Guid pOrg, string pProps, bool DisableModelUpdate)
        {
            if (!DisableModelUpdate)
                ThePropertyBag.PropBagUpdateValues(PropertyBag, pProps, "=");
            TheNMIEngine.SetUXProperty(pOrg, cdeMID, pProps, null, $"{(int)eFieldType.TileButton};{this.FldOrder}");
        }

        internal void sinkUpdate(cdeP pProp)
        {
            if (pProp == null) return;
            ThePropertyBag.PropBagUpdateValue(PropertyBag, pProp.Name, "=", pProp.ToString());
            if (eventPropertyChanged != null)
                eventPropertyChanged(this, pProp);
            else
            {
                PanelTitle = pProp.ToString();
            }
        }

        internal TheDashPanelInfo GetLocalizedPanel(int lcid, string dashboardEngine)
        {
            var panelEngine = TheThingRegistry.GetEngineNameByMID(cdeO);
            if ((panelEngine != dashboardEngine && dashboardEngine != eEngineName.NMIService) || String.IsNullOrEmpty(dashboardEngine) || String.IsNullOrEmpty(panelEngine))
            {
                TheBaseAssets.MySYSLOG.WriteToLog(10005, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.NMIService, "GetLocalizedPanel: engine mismatch or engine not specified", eMsgLevel.l2_Warning, $"Dashboard engine: '{dashboardEngine}'. Dash Panel Engine: '{panelEngine}'"));
            }
            if (String.IsNullOrEmpty(panelEngine))
            {
                panelEngine = dashboardEngine;
            }
            var tDp = new TheDashPanelInfo(cdeO)
            {
                ControlClass = TheBaseAssets.MyLoc.GetLocalizedStringByKey(lcid, panelEngine, this.ControlClass),
                Flags = this.Flags,
                FldOrder = this.FldOrder,
                IsFullSceen = this.IsFullSceen,

                HtmlContent = TheBaseAssets.MyLoc.GetLocalizedStringByKey(lcid, panelEngine, this.HtmlContent),
                PropertyBag = ThePropertyBag.GetLocalizedBag(this.PropertyBag, lcid, panelEngine),
            };
            if (PlatBag != null && PlatBag.Count > 0)
            {
                foreach (eWebPlatform t in PlatBag.Keys)
                    tDp.PlatBag[t] = ThePropertyBag.GetLocalizedBag(PlatBag[t], lcid, panelEngine);
            }
            this.CloneTo(tDp);
            return tDp;
        }
    }

    public class TheDashboardInfo : TheMetaDataBase
    {
        internal TheDashboardInfo GetLocalizedDashboard(int lcid)
        {
            var tdpi = new TheDashboardInfo
            {
                FldOrder = this.FldOrder,
                OnChangeName = this.OnChangeName,
                PropertyBag = ThePropertyBag.GetLocalizedBag(this.PropertyBag, lcid, TheThingRegistry.GetEngineNameByMID(cdeO)),
            };
            this.CloneTo(tdpi);
            return tdpi;
        }

        [IgnoreDataMember]
        public string DashboardTitle
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "Caption", "=");
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "Caption", "=", value);
            }
        }

        [IgnoreDataMember]
        public string InitialState
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "InitialState", "=");
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "InitialState", "=", value);
            }
        }

        [IgnoreDataMember]
        public bool IsPinned
        {
            get
            {
                return TheCommonUtils.CBool(ThePropertyBag.PropBagGetValue(PropertyBag, "IsPinned", "="));
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "IsPinned", "=", value.ToString());
            }
        }
        /// <summary>
        /// Sets the Order of the Icon in the Dashboard this DashBoardInfo is part of
        /// </summary>
        public int FldOrder { get; set; }
        public string OnChangeName { get; set; }
        public List<string> colPanels { get; set; }

        private ThePropertyBag _propertyBag;
        public ThePropertyBag PropertyBag
        {
            get
            {
                if (_propertyBag == null)
                    _propertyBag = new ThePropertyBag();
                return _propertyBag;
            }
            set
            {
                if (_propertyBag == null)
                {
                    _propertyBag = value;
                }
                else
                {
                    _propertyBag.MergeBag(value); // TODO flag duplicates and fail or update
                }
            }
        }

        internal cdeConcurrentDictionary<eWebPlatform, ThePropertyBag> PlatBag;
        public bool AddOrUpdatePlatformBag(eWebPlatform pPlat, ThePropertyBag pBagItem)
        {
            if (!PlatBag.ContainsKey(pPlat))
                PlatBag[pPlat] = pBagItem;
            else
                PlatBag[pPlat].MergeBag(pBagItem);
            return true;
        }

        private void Reset()
        {
            PropertyBag = new ThePropertyBag();
            PlatBag = new cdeConcurrentDictionary<eWebPlatform, ThePropertyBag>();
        }
        public TheDashboardInfo()
        {
            Reset();
        }
        public TheDashboardInfo(Guid pGuid, string pDashboardTitle)
        {
            Reset();
            cdeMID = pGuid;
            DashboardTitle = pDashboardTitle;
        }
        public TheDashboardInfo(IBaseEngine pBase, string pDashboardTitle)
        {
            Reset();
            if (pBase != null)
                cdeMID = pBase.GetDashboardGuid();
            DashboardTitle = pDashboardTitle;
        }
        public TheDashboardInfo(IBaseEngine pBase, string pDashboardTitle, List<string> pPanelInfos)
        {
            Reset();
            if (pBase != null)
                cdeMID = pBase.GetDashboardGuid();
            DashboardTitle = pDashboardTitle;
            if (pPanelInfos != null)
                colPanels = pPanelInfos;
        }

        /// <summary>
        /// Reloads the current Dashboard
        /// </summary>
        /// <param name="pMsg">Originator of the Request- if its not null, the Reload will only be sent to the originator</param>
        /// <param name="pForceReload">If set to true, the dashboard will cause a complete redraw on the NMI - beware of Agents! If the NMI is consolidated from multiple agents, the ForceReload will only show the last Dashboard</param>
        /// <returns>True if successful</returns>
        public bool Reload(TheProcessMessage pMsg, bool pForceReload)
        {
            return TheNMIEngine.RefreshDashboard(pMsg, cdeMID.ToString(), pForceReload);
        }

        internal void FireUpdate(TheDashPanelInfo pDash, TheThing pOwnerThing, TheProcessMessage pMsg)
        {
            if (!string.IsNullOrEmpty(OnChangeName) && pOwnerThing != null)
            {
                ThePropertyBag.PropBagUpdateValue(pDash.PropertyBag, "OnThingEvent", "=", string.Format("{0};{1}", pOwnerThing.cdeMID, OnChangeName));
                cdeP tProp = pOwnerThing.GetProperty(OnChangeName, true);
                if (tProp != null)
                {
                    if (pMsg?.Message != null)
                        tProp.SetPublication(true, pMsg.Message.GetOriginator());    ////OK - late binding
                    tProp.UnregisterEvent(eThingEvents.PropertyChanged, pDash.sinkUpdate);
                    tProp.RegisterEvent(eThingEvents.PropertyChanged, pDash.sinkUpdate);
                }
                else
                    tProp = pOwnerThing.SetProperty(OnChangeName, null, 0x11, pDash.sinkUpdate);
                ThePropertyBag.PropBagUpdateValue(pDash.PropertyBag, "OnPropertyChanged", "=", $"SEV:{OnChangeName}");
            }
            ThePropertyBag.PropBagUpdateValue(pDash.PropertyBag, "UXID", "=", pDash.cdeMID.ToString());
            FireEvent(eUXEvents.OnLoad, pMsg, true);
            eventOnLoad?.Invoke(pDash);
        }

        private Action<TheDashPanelInfo> eventOnLoad;

        /// <summary>
        /// RETIRED IN V4: please use ".RegisterEvent(eUXEvents.OnLoad,...)
        /// Registers a callback that will be called when this Dashboard is loaded in the NMI
        /// </summary>
        /// <param name="pReloadCallback">Callback receiving TheDashPanelInfo</param>
        public void RegisterOnLoad(Action<TheDashPanelInfo> pReloadCallback)
        {
            eventOnLoad += pReloadCallback;
        }
    }

    /// <summary>
    /// TheScreenInfo contains the meta data of a Dashboard (aka Screen) used in the NMI
    /// </summary>
    public class TheScreenInfo : TheMetaDataBase
    {

        /// <summary>
        /// Meta information of the Dashboard itself. Each dashboard can have one or more DashPanels (TheDashPanelInfo)
        /// </summary>
        public TheDashboardInfo MyDashboard { get; set; }
        /// <summary>
        /// Each DashPanel points to a subscreen which can be a Table, Form or another Dashboard. It represents the "Button" to get to the sub-Screen
        /// </summary>
        public List<TheDashPanelInfo> MyDashPanels { get; set; }

        /// <summary>
        /// TheFormInfo is a container of Meta information of a Form or a Table. If the sub-screen is a Dashboard, this collection is emptyu\
        /// </summary>
        public List<TheFormInfo> MyStorageInfo { get; set; }
        /// <summary>
        /// This list contains the raw data of the Form or table without any meta data
        /// </summary>
        public List<object> MyStorageMirror { get; set; }
        public cdeConcurrentDictionary<string, TheFormInfo> MyStorageMeta { get; set; }
        /// <summary>
        /// If set to true, the screen has to be reloaded (deleted and recreated) in the NMI
        /// </summary>
        public bool ForceReload { get; set; }

        public string NodeName { get; set; }
        /// <summary>
        /// new in 3.115: a list of all required JavaScript engine for this Dashboard/Screen.
        /// </summary>
        public List<string> RequiredEngines { get; set; }

        internal TheScreenInfo GetLocalizedScreen(int lcid)
        {
            var newStorageMeta = new cdeConcurrentDictionary<string, TheFormInfo>();
            foreach (var item in MyStorageMeta.GetDynamicEnumerable())
            {
                newStorageMeta.TryAdd(item.Key, item.Value.GetLocalizedForm(lcid));
            }
            var tSi = new TheScreenInfo
            {
                ForceReload = this.ForceReload,
                NodeName = this.NodeName,
                MyDashboard = this.MyDashboard?.GetLocalizedDashboard(lcid),
                MyDashPanels = this.MyDashPanels?.Select(dp => dp.GetLocalizedPanel(lcid, MyDashboard == null ? eEngineName.NMIService : TheThingRegistry.GetEngineNameByMID(MyDashboard.cdeO))).ToList(),
                MyStorageInfo = this.MyStorageInfo?.Select(formInfo => formInfo.GetLocalizedForm(lcid)).ToList(),
                MyStorageMeta = newStorageMeta,
                MyStorageMirror = this.MyStorageMirror?.Select(o => o).ToList(),
            };
            this.CloneTo(tSi);
            return tSi;
        }
    }

    public class TheJSONLoaderDefinition : TheDataBase
    {
        public string defDataSource { get; set; }
        public Guid TableName { get; set; }
        public string TargetElement { get; set; }
        public string SID { get; set; }
        public Guid ORG { get; set; }

        public Guid RequestingUserID { get; set; }

        public List<TheFieldInfo> FieldInfo { get; set; }
        public bool IsEmptyRecord { get; set; }

        public int PageNumber { get; set; }

        public int TopRecords { get; set; }

        public string SQLOrder { get; set; }

        public string SQLFilter { get; set; }

        public bool ForceReload { get; set; }

        internal TheClientInfo ClientInfo { get; set; }

        public string ModelID { get; set; }
    }

    public class TheFormInfo : TheMetaDataBase
    {
        //public string FormTitle { get; set; }
        [IgnoreDataMember]
        public string FormTitle
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "Caption", "=");
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "Caption", "=", value);
            }
        }



        [IgnoreDataMember]
        public string AddTemplateType
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "AddTemplateType", "=");
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "AddTemplateType", "=", value);
            }
        }

        [IgnoreDataMember]
        public string TableReference
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "TableReference", "=");
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "TableReference", "=", value);
            }
        }


        [IgnoreDataMember]
        public string RowTemplateType
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "TemplateID", "=");
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "TemplateID", "=", value);
            }
        }
        [IgnoreDataMember]
        public string HeadlineImagePath
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "HeadlineImagePath", "=");
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "HeadlineImagePath", "=", value);
            }
        }
        [IgnoreDataMember]
        public string AddButtonText
        {
            get
            {
                return ThePropertyBag.PropBagGetValue(PropertyBag, "AddButtonText", "=");
            }
            set
            {
                ThePropertyBag.PropBagUpdateValue(PropertyBag, "AddButtonText", "=", value);
            }
        }

        public int AddACL { get; set; }
        /// <summary>
        /// Retired - User AddOrUpdatePlatBag() instead
        /// Binary Flags for the Add Button:
        /// 1=Show On Relay (HideAddButton)
        /// 2=Show in Cloud (AllowAddOnAllNodes)
        /// 4 =Hide from Mobile (HideAdd)
        /// 8 =
        /// 16=
        /// 32=
        /// 64=
        /// 128=Only Show if user is on first node(RequireFirstNodeForAdd)
        /// </summary>
        public int AddFlags { get; set; }

        /// <summary>
        /// Define a ModelID for a Form to apply a PropertyBag Transform using TheFOR (The FormOverRide)
        /// </summary>
        public string ModelID { get; set; }
        public string AssociatedClassName { get; set; }
        public string TargetElement { get; set; }


        /// <summary>
        /// SourceType;:;MaxRecords;:;SortOrder (true=descending);:;Filter;:;PageNumber
        /// </summary>
        public string defDataSource { get; set; }
        public eDefaultView DefaultView { get; set; }
        public bool IsReadOnly { get; set; }

        public string OrderBy { get; set; }
        public bool IsLiveData { get; set; }
        public bool IsAlwaysEmpty { get; set; }
        public bool IsNotAutoLoading { get; set; }
        public bool IsUsingAbsolute { get; set; }

        [IgnoreDataMember]
        public int TileWidth
        {
            get
            {
                return TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(PropertyBag, "TileWidth", "="));
            }
            set
            {
                if (value > 0)
                    ThePropertyBag.PropBagUpdateValue(PropertyBag, "TileWidth", "=", TheCommonUtils.CStr(value));
            }
        }

        [IgnoreDataMember]
        public int TileHeight
        {
            get
            {
                return TheCommonUtils.CInt(ThePropertyBag.PropBagGetValue(PropertyBag, "TileHeight", "="));
            }
            set
            {
                if (value > 0)
                    ThePropertyBag.PropBagUpdateValue(PropertyBag, "TileHeight", "=", TheCommonUtils.CStr(value));
            }
        }

        public bool IsPostingOnSubmit { get; set; }
        public bool GetFromFirstNodeOnly { get; set; }
        public bool GetFromServiceOnly { get; set; }
        public List<TheFieldInfo> FormFields { get; set; }
        public string OwnerEngine { get; set; }

        private ThePropertyBag _propertyBag;
        public ThePropertyBag PropertyBag
        {
            get
            {
                if (_propertyBag == null)
                    _propertyBag = new ThePropertyBag();
                return _propertyBag;
            }
            set
            {
                if (_propertyBag == null)
                {
                    _propertyBag = value;
                }
                else
                {
                    _propertyBag.MergeBag(value); // TODO flag duplicates and fail or update
                }
            }
        }
        internal cdeConcurrentDictionary<eWebPlatform, ThePropertyBag> PlatBag;
        public bool AddOrUpdatePlatformBag(eWebPlatform pPlat, ThePropertyBag pBagItem)
        {
            if (!PlatBag.ContainsKey(pPlat))
                PlatBag[pPlat] = pBagItem;
            else
                PlatBag[pPlat].MergeBag(pBagItem);
            return true;
        }

        private void Reset()
        {
            PropertyBag = new ThePropertyBag();
            PlatBag = new cdeConcurrentDictionary<eWebPlatform, ThePropertyBag>();
        }
        public TheFormInfo()
        {
            OwnerEngine = eEngineName.NMIService;
            Reset();
        }
        public TheFormInfo(TheThing pThing)
        {
            OwnerEngine = eEngineName.NMIService;
            Reset();
            if (pThing != null)
            {
                cdeMID = pThing.cdeMID;
                cdeA = pThing.cdeA;
                cdeO = pThing.cdeO;
                FormTitle = pThing.FriendlyName;
                defDataSource = string.Format("TheThing;:;0;:;True;:;cdeMID={0}", pThing.cdeMID);
            }
        }
        public TheFormInfo(IBaseEngine pBase)
        {
            OwnerEngine = eEngineName.NMIService;
            Reset();
            if (pBase != null)
            {
                cdeMID = pBase.GetBaseThing().GetBaseThing().cdeMID; //.GetEngineID();
                defDataSource = string.Format("TheThing;:;0;:;True;:;EngineName={0}", pBase.GetEngineName());
            }
        }
        public TheFormInfo(Guid pKey, string pOwner, string pTitle, string pDataSource)
        {
            cdeMID = pKey;
            Reset();
            FormTitle = pTitle;
            defDataSource = pDataSource;
            OwnerEngine = pOwner;
        }
        public TheFormInfo(Guid pKey, string pOwner, string pTitle, string pDataSource, string pAddButton, string pAddTemplate)
        {
            cdeMID = pKey;
            Reset();
            FormTitle = pTitle;
            AddButtonText = pAddButton;
            AddTemplateType = pAddTemplate;
            defDataSource = pDataSource;
            OwnerEngine = pOwner;
        }

        public TheFormInfo Clone(eWebPlatform pWebPlatform, bool CreateFastCache = false)
        {
            TheFormInfo tScroll = new TheFormInfo();
            CloneTo(tScroll);

            tScroll.AssociatedClassName = AssociatedClassName;
            tScroll.TargetElement = TargetElement;
            tScroll.defDataSource = defDataSource;

            tScroll.DefaultView = DefaultView;
            tScroll.IsReadOnly = IsReadOnly;
            tScroll.IsLiveData = IsLiveData;
            tScroll.IsPostingOnSubmit = IsPostingOnSubmit;
            tScroll.GetFromFirstNodeOnly = GetFromFirstNodeOnly;
            tScroll.GetFromServiceOnly = GetFromServiceOnly;
            tScroll.IsUsingAbsolute = IsUsingAbsolute;
            tScroll.IsAlwaysEmpty = IsAlwaysEmpty;

            tScroll.OrderBy = OrderBy;
            tScroll.OwnerEngine = OwnerEngine;
            tScroll.AddACL = AddACL;
            tScroll.FormFields = null;
            tScroll.ModelID = ModelID;

            tScroll.PropertyBag = PropertyBag.Clone(CreateFastCache);
            if (PlatBag.ContainsKey(eWebPlatform.Any))
                tScroll.PropertyBag.MergeBag(PlatBag[eWebPlatform.Any], true, false);
            if (PlatBag.ContainsKey(pWebPlatform))
                tScroll.PropertyBag.MergeBag(PlatBag[pWebPlatform], true, false);

            //foreach (eWebPlatform t in PlatBag.Keys)
            //  tScroll.PlatBag[t]=PlatBag[t].Clone(CreateFastCache);
            return tScroll;
        }

        public TheFormInfo GetLocalizedForm(int lcid)
        {
            TheFormInfo tScroll = new TheFormInfo();
            CloneTo(tScroll);

            tScroll.AssociatedClassName = AssociatedClassName;
            tScroll.TargetElement = TargetElement;
            tScroll.defDataSource = defDataSource;

            tScroll.DefaultView = DefaultView;
            tScroll.IsReadOnly = IsReadOnly;
            tScroll.IsLiveData = IsLiveData;
            tScroll.IsPostingOnSubmit = IsPostingOnSubmit;
            tScroll.GetFromFirstNodeOnly = GetFromFirstNodeOnly;
            tScroll.GetFromServiceOnly = GetFromServiceOnly;
            tScroll.IsUsingAbsolute = IsUsingAbsolute;
            tScroll.IsAlwaysEmpty = IsAlwaysEmpty;

            tScroll.OrderBy = OrderBy;
            tScroll.OwnerEngine = OwnerEngine;
            tScroll.AddACL = AddACL;

            tScroll.FormFields = this.FormFields.Select(f => f.GetLocalizedField(lcid, OwnerEngine)).ToList();

            tScroll.PropertyBag = ThePropertyBag.GetLocalizedBag(this.PropertyBag, lcid, OwnerEngine);
            foreach (eWebPlatform t in PlatBag.Keys)
                tScroll.PlatBag[t] = ThePropertyBag.GetLocalizedBag(PlatBag[t], lcid, OwnerEngine);
            return tScroll;
        }


        /// <summary>
        /// Reloads TheFormInfo if the userID in TheProcessMessage has the permission to the data in TheFormInfo
        /// </summary>
        /// <param name="pMSG">Required to get the requesting UserID. If null the function will return false</param>
        /// <param name="bForceLoad">Sends the full data Set and flags the UX to destroy and recreate the form/table</param>
        /// <returns>true if data was sent to the browser</returns>
        public bool Reload(TheProcessMessage pMSG, bool bForceLoad)
        {
            return Reload(pMSG, bForceLoad, Guid.Empty);
        }

        /// <summary>
        /// Reloads TheFormInfo if the userID in TheProcessMessage has the permission to the data in TheFormInfo
        /// </summary>
        /// <param name="pMSG">Required to get the requesting UserID. If null the function will return false</param>
        /// <param name="bForceLoad">Sends the full data Set and flags the UX to destroy and recreate the form/table</param>
        /// <param name="ContainerControlGuid">Guid of TheFieldInfo that contains the Table if Table is in a Form</param>
        /// <returns>true if data was sent to the browser</returns>
        public bool Reload(TheProcessMessage pMSG, bool bForceLoad, Guid ContainerControlGuid)
        {
            //if (pMSG == null || pMSG.CurrentUserID == Guid.Empty) return false;
            TheThing tTHing = TheThingRegistry.GetThingByMID("*", cdeO);
            if (tTHing == null) return false;
            TheDashboardInfo tDash = TheNMIEngine.GetEngineDashBoardByThing(tTHing);
            if (tDash == null) return false;
            if (pMSG == null || pMSG.CurrentUserID == Guid.Empty)
            {
                //TODO: This should be throttled that we dont have too many back-forth between node and browser
                Communication.TheCommCore.PublishCentral(new TSM(eEngineName.NMIService, $"NMI_REQ_DASH", $"{TheCommonUtils.cdeGuidToString(ContainerControlGuid == Guid.Empty ? cdeMID : ContainerControlGuid)}:{(DefaultView == eDefaultView.Form ? "CMyForm" : "CMyTable")}:{TheCommonUtils.cdeGuidToString(cdeMID)}:{TheCommonUtils.CGuid(tDash.cdeMID)}"));
                return false;
            }

            return TheNMIEngine.SendNMIData(pMSG.Message, TheCommonUtils.GetClientInfo(pMSG), ContainerControlGuid == Guid.Empty ? cdeMID.ToString() : ContainerControlGuid.ToString(), DefaultView == eDefaultView.Form ? "CMyForm" : "CMyTable", cdeMID.ToString(), tDash.cdeMID.ToString(), bForceLoad, false);
        }

        /// <summary>
        /// Reloads a form on a custom Dashboard
        /// </summary>
        /// <param name="pMSG"></param>
        /// <param name="pDashID"></param>
        /// <param name="bForceLoad"></param>
        /// <returns></returns>
        public bool Reload(TheProcessMessage pMSG, Guid pDashID, bool bForceLoad)
        {
            if (pMSG == null || pMSG.CurrentUserID == Guid.Empty) return false;
            TheThing tTHing = TheThingRegistry.GetThingByMID("*", cdeO);
            if (tTHing == null) return false;
            TheDashboardInfo tDash = TheNMIEngine.GetDashboardById(pDashID);
            if (tDash == null) return false;

            return TheNMIEngine.SendNMIData(pMSG.Message, TheCommonUtils.GetClientInfo(pMSG), cdeMID.ToString(), DefaultView == eDefaultView.Form ? "CMyForm" : "CMyTable", cdeMID.ToString(), tDash.cdeMID.ToString(), bForceLoad, false);
        }

        /// <summary>
        /// Removes a field by its order Number
        /// </summary>
        /// <param name="OrderNumber"></param>
        /// <returns></returns>
        public bool DeleteByOrder(int OrderNumber)
        {
            var tFlds = TheNMIEngine.GetFieldsByFunc(s => s.FormID == this.cdeMID && s.FldOrder == OrderNumber);
            if (tFlds != null && tFlds.Count > 0)
            {
                TheCDEngines.MyNMIService.MyNMIModel.MyFields.RemoveItems(tFlds, null);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Return a field of a Form by the order number
        /// </summary>
        /// <param name="OrderNumber"></param>
        /// <returns></returns>
        public TheFieldInfo GetByOrder(int OrderNumber)
        {
            var tFld = TheNMIEngine.GetFieldByFldOrder(this, OrderNumber);
            if (tFld != null)
            {
                return tFld;
            }
            return null;
        }

        public void SetUXProperty(Guid pOrg, string pProps)
        {
            ThePropertyBag.PropBagUpdateValues(PropertyBag, pProps, "=");
            TheNMIEngine.SetUXProperty(pOrg, cdeMID, pProps, null, $"{(int)eFieldType.FormView};-1");
        }

        /// <summary>
        /// return true if the pWildcard is found in the object class
        /// </summary>
        /// <param name="inObj"></param>
        /// <param name="pWildcard"></param>
        /// <returns></returns>
        public bool DoesWildContentMatch(object inObj, string pWildcard)
        {
            return TheStorageUtilities.DoesWildContentMatch(inObj, pWildcard, TheNMIEngine.GetFieldsByFunc(s => s.FormID == this.cdeMID));
        }
    }

    public class TheChartValueDefinition : TheMetaDataBase
    {
        public string ValueName { get; set; }
        public bool IsValueVisible { get; set; }
        public double UpperAlarm { get; set; }
        public double LowerAlarm { get; set; }
        public double GainFactor { get; set; }
        public string DisplayFormat { get; set; }

        public string Label { get; set; }
        public int SeriesNo { get; set; }
        public string GroupFilter { get; set; }

        public TheChartValueDefinition(Guid pKey, string pNameX)
        {
            cdeMID = pKey;
            ValueName = pNameX;
            GainFactor = 1;
            DisplayFormat = "{0}";
            IsValueVisible = true;
        }

        public TheChartValueDefinition(Guid pKey, string pNameX, bool IsXVisible, double pUpperAlarmX, double pLowerAlarmX, double pGainFactorX, string pDisplayFormat)
        {
            cdeMID = pKey;
            ValueName = pNameX;
            IsValueVisible = IsXVisible;
            UpperAlarm = pUpperAlarmX;
            LowerAlarm = pLowerAlarmX;
            GainFactor = pGainFactorX;
            DisplayFormat = string.IsNullOrEmpty(pDisplayFormat) ? "{0}" : pDisplayFormat;
        }
    }

    internal class TheChartCookie
    {
        public TheChartDefinition ChartDefinition { get; set; }
        public Guid TargetNode { get; set; }

        public List<string> Groups { get; set; }

        public ICDEChartsFactory ChartFactory { get; set; }

        public List<TheChartValueDefinition> ValueDefinitions { get; set; }

        public int Counter { get; set; }

        public int VirtualBlock { get; set; }
        public int HighestBlock { get; set; }

        public bool IsInitialData { get; set; }
    }

    /// <summary>
    /// Defines Custom control types hosted in Plugin-Services
    /// </summary>
    public class TheControlType : TheMetaDataBase
    {
        public string ControlName { get; set; }
        public string ControlType { get; set; }
        public string BaseEngineName { get; set; }
        public string SmartControlType { get; set; }
    }

    public class TheChartDefinition : TheMetaDataBase
    {
        public int BlockSize { get; set; }
        public string DataSource { get; set; }
        public bool InAquireMode { get; set; }
        public string TitleText { get; set; }
        public string SubTitleText { get; set; }
        public List<TheChartValueDefinition> ValueDefinitions { get; set; }
        public string XAxisSource { get; set; }
        public string ColumFilter { get; set; }
        public string Grouping { get; set; }
        public string ValueName { get; set; }
        public int IntervalInMS { get; set; }
        public int GroupMode { get; set; }

        public string IChartFactoryType { get; set; }

        private ThePropertyBag _propertyBag;
        public ThePropertyBag PropertyBag
        {
            get
            {
                if (_propertyBag == null)
                    _propertyBag = new ThePropertyBag();
                return _propertyBag;
            }
            set
            {
                if (_propertyBag == null)
                {
                    _propertyBag = value;
                }
                else
                {
                    _propertyBag.MergeBag(value); // TODO flag duplicates and fail or update
                }
            }
        }

        public TheChartDefinition()
        {
        }
        public TheChartDefinition(Guid pKey, string pTitle, int pBlockSize, string pDataSource, bool pInAquireMode, string pColFilter, string pGrouping, string pValueNameX)
        {
            cdeMID = pKey;
            TitleText = pTitle;
            BlockSize = pBlockSize;
            DataSource = pDataSource;
            InAquireMode = pInAquireMode;
            ValueName = pValueNameX;
            ColumFilter = pColFilter;
            Grouping = pGrouping;
            if (!string.IsNullOrEmpty(pValueNameX))
            {
                var tProps = pValueNameX.Split(';');
                if (tProps.Length > 0)
                {
                    ValueDefinitions = new List<TheChartValueDefinition>();
                    for (int i = 0; i < tProps.Length; i++)
                    {
                        ValueDefinitions.Add(new TheChartValueDefinition(Guid.NewGuid(), tProps[i]) { Label = tProps[i] });
                    }
                    ValueName = null;
                }
            }
        }
        public TheChartDefinition(Guid pKey, string pTitle, int pBlockSize, string pDataSource, bool pInAquireMode, string pColFilter, string pGrouping, string pValueNameX, string pLabels)
        {
            cdeMID = pKey;
            TitleText = pTitle;
            BlockSize = pBlockSize;
            DataSource = pDataSource;
            InAquireMode = pInAquireMode;
            ValueName = pValueNameX;
            ColumFilter = pColFilter;
            Grouping = pGrouping;
            if (!string.IsNullOrEmpty(pValueNameX))
            {
                ValueDefinitions = new List<TheChartValueDefinition>();
                var tProps = pValueNameX.Split(';');
                var tLabels = pLabels?.Split(';');
                for (int i = 0; i < tProps.Length; i++)
                {
                    ValueDefinitions.Add(new TheChartValueDefinition(Guid.NewGuid(), tProps[i]) { Label = string.IsNullOrEmpty(pLabels) ? tProps[i] : (i < tLabels.Length ? tLabels[i] : tProps[i]) });
                }
            }
        }
        public TheChartDefinition(Guid pKey, string pTitle, int pBlockSize, string pDataSource, bool pInAquireMode, string pColFilter, string pGrouping, TheChartValueDefinition pValue)
        {
            cdeMID = pKey;
            TitleText = pTitle;
            BlockSize = pBlockSize;
            DataSource = pDataSource;
            InAquireMode = pInAquireMode;
            ValueDefinitions = new List<TheChartValueDefinition> { pValue };
            ColumFilter = pColFilter;
            Grouping = pGrouping;
        }
        public TheChartDefinition(Guid pKey, string pTitle, int pBlockSize, string pDataSource, bool pInAquireMode, string pXAxis, string pColFilter, string pGrouping, List<TheChartValueDefinition> pValues)
        {
            cdeMID = pKey;
            TitleText = pTitle;
            BlockSize = pBlockSize;
            DataSource = pDataSource;
            InAquireMode = pInAquireMode;
            ValueDefinitions = pValues;
            XAxisSource = pXAxis;

            ColumFilter = pColFilter;
            Grouping = pGrouping;
        }
    }

    public class ThePageDefinition : TheMetaDataBase
    {
        public string Title { get; set; }
        public string PageName { get; set; }
        public string PageTemplate { get; set; }

        public bool IsPublic { get; set; }
        public long AccessLevel { get; set; }
        public DateTimeOffset EntryDate { get; set; }
        public DateTimeOffset LastUpdate { get; set; }
        public DateTimeOffset LastAccess { get; set; }
        public string Version { get; set; }
        public Guid ParentID { get; set; }
        public int Hits { get; set; }
        public int Errors { get; set; }
        public int WPID { get; set; }    //now cdeMID
        public string Commment { get; set; }
        public string AddHeader { get; set; }
        public Guid CSSID { get; set; }
        public int PageFeatures { get; set; }
        public string BatchIDs { get; set; }
        public string AddBody { get; set; }
        public Guid ContentID { get; set; }
        public Guid ContentID2 { get; set; }
        public Guid ContentID3 { get; set; }
        public string RealPage { get; set; }
        public bool BrandPage { get; set; }
        public bool BufferResponse { get; set; }

        /// <summary>
        /// V4: Set the Last Cache Time of a page
        /// </summary>
        public DateTimeOffset LastCacheTime { get; set; }
        public int CachePeriod { get; set; }
        /// <summary>
        /// V4: contains the last Cache of a page
        /// </summary>
        public cdeConcurrentDictionary<eWebPlatform, string> LastCacheWP { get; set; }

        /// <summary>
        /// RETIRED in V4: now using LastCacheWP - will be removed in V5
        /// </summary>
        public string LastCache { get; set; }

        /// <summary>
        /// RETIRED in V4: now using LastCacheWP - will be removed in V5
        /// </summary>
        public eWebPlatform LastCachePlatform { get; set; }
        public bool IsNotCached { get; set; }
        public bool IncludeCDE { get; set; }

        public bool IsLiteTheme { get; set; }
        public bool RequireLogin { get; set; }

        /// <summary>
        /// New in 3.1: Allows to add a EasyScopeID to the Page URL
        /// </summary>
        public bool AllowScopeQuery { get; set; }
        public Guid PortalGuid { get; set; }

        public Guid StartScreen { get; set; }

        public string AdminRole { get; set; }

        public bool IncludeHeaderButtons { get; set; }
        public cdeConcurrentDictionary<string, string> MyProperties { get; set; }

        public string ContentType { get; set; }

        public int MobileConstrains { get; set; }

        public ThePageDefinition()
        {
        }

        public ThePageDefinition(Guid pKey, string pPageName, string pPageTitle, string pTemplate, Guid pContent)
        {
            cdeMID = pKey;
            PageName = pPageName;
            Title = pPageTitle;
            PageTemplate = pTemplate;
            ContentID = pContent;
            EntryDate = DateTimeOffset.Now;
            LastAccess = DateTimeOffset.Now;
            Hits = 0;
        }

        public override string ToString()
        {
            return string.Format("Title:{0} PageName:{1} PageTemplate:{2}", Title, PageName, PageTemplate);
        }

        /// <summary>
        /// Returns a localized version of ThePageDefinition
        /// </summary>
        /// <param name="lcid">LCID of the requested localization</param>
        /// <returns></returns>
        public ThePageDefinition GetLocalizedPage(int lcid)
        {
            // CODE REVIEW/TODO: Clone the page, cache for multiple lcids etc. etc.
            Title = TheBaseAssets.MyLoc.GetLocalizedStringByKey(lcid, eEngineName.NMIService, Title);
            return this;
        }
    }

    public class ThePageContent : TheMetaDataBase
    {
        public Guid ContentID { get; set; }
        public Guid WBID { get; set; }
        public string Comment { get; set; }
        public string Condition { get; set; }
        public DateTimeOffset EntryDate { get; set; }
        public DateTimeOffset LastUpdate { get; set; }
        public string Version { get; set; }
        public int Hits { get; set; }
        public DateTimeOffset LastAccess { get; set; }
        public int SortOrder { get; set; }
        public Guid Device { get; set; }
        public Guid Browser { get; set; }
        public cdeConcurrentDictionary<string, string> MyProperties { get; set; }

        public ThePageContent()
        {
        }
        public ThePageContent(Guid id)
        {
            cdeMID = id;
        }
    }

    public class ThePageBlocks : TheMetaDataBase
    {
        public string Template { get; set; }
        public string Headline { get; set; }
        public string ClickLink { get; set; }
        public string ClickInfo { get; set; }
        public string Bullets { get; set; }

        public bool AddBullets { get; set; }
        public bool ShowTouchFrame { get; set; }
        public int ImgHeight { get; set; }
        public int ImgWidth { get; set; }
        public string ImgLink { get; set; }

        public int SpacingBetweenBullets { get; set; }

        public string FriendlyName { get; set; }
        public Guid BlockType { get; set; }
        public long AccessLevel { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime LastUpdate { get; set; }
        public string Version { get; set; }
        public int XID { get; set; } //????
        public int XFlags { get; set; }
        public int Hits { get; set; }
        public string RawData { get; set; }
        //  public Guid WBID { get; set; } //now cdeMID
        public DateTimeOffset LastAccess { get; set; }
        public bool IsPublic { get; set; }
        public bool IsMeshSynced { get; set; }
        public cdeConcurrentDictionary<string, string> MyProperties { get; set; }

        public ThePageBlocks() { }

        public ThePageBlocks(Guid ID, Guid pBlockType, string pTemplate, string pHead)
        {
            cdeMID = ID;
            BlockType = pBlockType;
            Template = pTemplate;
            Headline = pHead;
        }

        public ThePageBlocks(Guid ID, Guid pBlockType, string pTemplate, string pHead, string pBullets, bool pAddBullets, bool bShowTF, string pLink, string pInfo, int pSpace)
        {
            cdeMID = ID;
            BlockType = pBlockType;
            Template = pTemplate;
            Headline = pHead;
            Bullets = pBullets;
            AddBullets = pAddBullets;
            ShowTouchFrame = bShowTF;
            ClickLink = pLink;
            ClickInfo = pInfo;
            SpacingBetweenBullets = pSpace;
        }

    }

    public class ThePageBlockTypes : TheMetaDataBase
    {
        public string BlockTypeName { get; set; }
        public string Description { get; set; }
        public DateTimeOffset EntryDate { get; set; }
        public DateTimeOffset AvailableSince { get; set; }
        public string Version { get; set; }
        public string BlockTemplate { get; set; }
        public string BlockContent { get; set; }

        public static Guid HTML = new Guid("{3F2D0AD5-9D18-49C5-A6E2-04BE7F10BD89}");
        public static Guid CONTENT = new Guid("{779699EB-2AE9-4EE2-8095-F4BD4A309358}");
        public static Guid IMAGE = new Guid("{2CA1756D-55D9-4EBE-88FC-10A8847EA2DB}");
        public static Guid JAVASCRIPT = new Guid("{E5CC2D30-61EA-45B8-835E-E309DF82C2C4}");
        public static Guid DASHBOARD = new Guid("{14386AA9-D754-4941-AD01-B68081E56AB2}");
        public static Guid STYLESHEET = new Guid("{06A18EF9-9543-4204-BC28-56956BB7C482}");
    }

    /// <summary>
    /// ThePropertyBag is a container for dynamic properties of a Thing
    /// </summary>
    public class ThePropertyBag : List<string>
    {
        /// <summary>
        /// Merges the current bag with the bag given as the parameter
        /// </summary>
        /// <param name="pQ"></param>
        /// <param name="bIgnoreNodeSettings">If true, node settings in the bag will be ignored</param>
        /// <param name="CreateFastCache">if true, the merge creates a new fastcache</param>
        /// <returns></returns>
        internal bool MergeBag(ThePropertyBag pQ, bool bIgnoreNodeSettings, bool CreateFastCache = false)
        {
            if (pQ == null) return false;
            if (CreateFastCache && !HasFastCache())
                _FastCache = new cdeConcurrentDictionary<string, string>();
            foreach (string tEnt in pQ.ToList())
            {
                if (string.IsNullOrEmpty(tEnt))
                    continue;
                string tName = tEnt.Split('=')[0];  //NMI-REDO: Tune with IndexOf - see: https://docs.microsoft.com/en-us/visualstudio/profiling/da0013-high-usage-of-string-split-or-string-substring
                if (bIgnoreNodeSettings && (tName == "EngineName" || TheCommonUtils.IsPropertyOfClass(tName, typeof(nmiPlatBag))))
                    continue;
                if (this.Any(s => s.StartsWith(tName, StringComparison.OrdinalIgnoreCase)))
                {
                    if (CreateFastCache)
                        _FastCache.RemoveNoCare(tName);
                    this.Remove(this.First(s => s.StartsWith(tName, StringComparison.OrdinalIgnoreCase)));
                }
                if (CreateFastCache)
                {
                    if (tEnt.Length == tName.Length)
                        _FastCache.TryAdd(tName, true.ToString());
                    else
                        _FastCache.TryAdd(tName, tEnt.Substring(tName.Length + 1));
                }
                Add(tEnt);
            }
            return true;
        }
        /// <summary>
        /// Merges the current bag with the bag given as the parameter
        /// </summary>
        /// <param name="pQ"></param>
        /// <returns></returns>
        public bool MergeBag(ThePropertyBag pQ)
        {
            return MergeBag(pQ, false, true);
        }

        internal cdeConcurrentDictionary<string, string> _FastCache;    //NMI-REDO: Requires proper locking

        /// <summary>
        /// Creates a new Property Bag from public properties of a given class
        /// Use this for strong typing of NMI Control Properties
        /// </summary>
        /// <param name="pInObj"></param>
        /// <returns></returns>
        public static ThePropertyBag Create(object pInObj)
        {
            if (pInObj == null) return null;

            ThePropertyBag tBag = new ThePropertyBag
            {
                _FastCache = new cdeConcurrentDictionary<string, string>()
            };
            Type type = pInObj.GetType();
            PropertyInfo[] info = type.GetProperties();
            foreach (PropertyInfo property in info)
            {
                var Value = property.GetValue(pInObj, null);
                if (Value == null) continue;
                bool tAddVal = true;
                switch (property.PropertyType.FullName)
                {
                    case "System.String":
                        if (Value.ToString() == "") tAddVal = false;
                        break;
                    case "System.Guid":
                        if (TheCommonUtils.CGuid(Value) == Guid.Empty) tAddVal = false;
                        break;
                    case "System.Double":
                    case "System.Int32":
                    case "System.Int":
                    case "System.Int64":
                    case "System.Long":
                    case "System.Byte":
                        // ReSharper disable once CompareOfFloatsByEqualityOperator
                        if (TheCommonUtils.CDbl(Value) == 0) tAddVal = false;
                        break;
                }
                if (tAddVal)
                {
                    tBag.Add($"{property.Name}={Value}");
                    tBag._FastCache.TryAdd(property.Name, Value.ToString());
                }
            }
            return tBag;
        }


        /// <summary>
        /// Return a sub Bag of the Property Bag. Dots in the front determine the Sub Branch
        /// i.e.: ".TileWidth" is a property for the first branch (1)
        /// "TileWidth" is a property of the root branch (0)
        /// </summary>
        /// <param name="pBag"></param>
        /// <param name="pBranch"></param>
        /// <returns></returns>
        public static ThePropertyBag GetSubBag(ThePropertyBag pBag, int pBranch)
        {
            ThePropertyBag tBag = new ThePropertyBag();
            if (pBag == null) return tBag;
            string tProb = "...............".Substring(0, pBranch);
            foreach (string tEnt in pBag)
            {
                if (pBranch == 0)
                {
                    if (!tEnt.StartsWith("."))
                        tBag.Add(tEnt);
                }
                else
                {
                    if (tEnt.StartsWith(tProb) && tEnt.Length > pBranch && tEnt.Substring(pBranch, 1) != ".")
                        tBag.Add(tEnt.Substring(pBranch));
                }

            }
            return tBag;
        }


        #region PROPERTY BAG - Replace with Serializable Dictionary

        public static ThePropertyBag CreateUXBagFromProperties(TheThing pThing)
        {
            ThePropertyBag tRes = new ThePropertyBag();
            if (pThing == null) return tRes;

            List<cdeP> tProps = pThing.GetNMIProperties();

            tRes._FastCache = new cdeConcurrentDictionary<string, string>();
            foreach (cdeP tP in tProps)
            {
                PropBagUpdateValue(tRes, tP.Name, "=", tP.ToString());
                tRes._FastCache.TryAdd(tP.Name, tP.ToString());
            }
            return tRes;
        }
        public static ThePropertyBag MergeUXBagFromProperties(ThePropertyBag pBag, TheThing pThing)
        {
            if (pThing == null || pBag == null) return null;

            List<cdeP> tProps = pThing.GetNMIProperties();
            foreach (cdeP tP in tProps)
            {
                if (tP.Name == "FldOrder")
                    continue;
                PropBagUpdateValue(pBag, tP.Name, "=", tP.ToString());
            }
            pBag._FastCache = null;
            ThePropertyBag.GetDictionary(pBag, "=");
            return pBag;
        }

        public static bool PropBagHasValue(ThePropertyBag pQ, string pName, string Seperator)
        {
            if (pQ == null || string.IsNullOrEmpty(pName)) return false;
            if (pQ.HasFastCache())
                return pQ._FastCache.ContainsKey(pName);

            lock (pQ)
            {
                string toInser = pName.Trim() + Seperator;
                try
                {
                    if (pQ.Count(s => s.StartsWith(toInser)) > 0)
                        return true;
                }
                catch
                {
                    //ignored
                }
            }
            return false;
        }
        public static string PropBagGetKeyValue(ThePropertyBag pQ, string pName, string Seperator)
        {
            if (pQ == null || string.IsNullOrEmpty(pName)) return "";
            string retStr = "";
            lock (pQ)
            {
                string toInser = pName.Trim() + Seperator;
                try
                {
                    if ((retStr = pQ.FirstOrDefault(s => s.StartsWith(toInser, StringComparison.OrdinalIgnoreCase))) == null)
                        retStr = "";
                }
                catch
                {
                    //ignored
                }
            }
            return retStr;
        }
        public static string PropBagGetValue(ThePropertyBag pQ, string pName)
        {
            return PropBagGetValue(pQ, pName, "=");
        }
        public static string PropBagGetValue(ThePropertyBag pQ, string pName, string Seperator)
        {
            if (pQ == null || string.IsNullOrEmpty(pName)) return "";
            if (pQ.HasFastCache())
            {
                string tOut = "";
                if (pQ?._FastCache?.TryGetValue(pName, out tOut) == false)
                    return "";
                return tOut; // pQ._FastCache.tr.ContainsKey(pName) ? pQ._FastCache[pName] : "";
            }
            string retStr = "";
            lock (pQ)
            {
                try
                {
                    string toInser = pName.Trim() + Seperator;
                    if ((retStr = pQ.FirstOrDefault(s => s.StartsWith(toInser, StringComparison.OrdinalIgnoreCase))) != null)
                    {
                        retStr = PropBagGetValue(retStr, Seperator);
                    }
                }
                catch
                {
                    //ignored
                }
            }
            return retStr;
        }

        private bool HasFastCache()
        {
            if (_FastCache == null)
            {
                return false;
            }
            if (_FastCache.Count != this.Count)
            {
                _FastCache = null;
                GetDictionary(this, "=");
            }
            else
            {

            }
            return true;
        }

        internal static cdeConcurrentDictionary<string, string> GetDictionary(ThePropertyBag pQ, string Seperator)
        {
            if (pQ == null) return null;
            if (pQ.HasFastCache())
                return pQ._FastCache;

            cdeConcurrentDictionary<string, string> retStr = new cdeConcurrentDictionary<string, string>();
            lock (pQ)
            {
                try
                {
                    foreach (string t in pQ)
                    {
                        var tP = t.IndexOf(Seperator);
                        if (tP < 0)
                            retStr.TryAdd(t, true.ToString());
                        else
                        {
                            retStr.TryAdd(t.Substring(0, tP), t.Substring(tP + 1));
                        }
                    }
                }
                catch
                {
                    //ignored
                }
            }
            pQ._FastCache = retStr;
            return retStr;
        }

        internal ThePropertyBag Clone(bool CreateFastCache = false)
        {
            ThePropertyBag tPropertyBag = new ThePropertyBag();
            if (this.Count > 0)
            {
                if (CreateFastCache)
                    tPropertyBag._FastCache = new cdeConcurrentDictionary<string, string>();
                foreach (string t in this)
                {
                    tPropertyBag.Add(t);
                    if (!CreateFastCache) continue;
                    var tP = t.IndexOf('=');
                    if (tP < 0)
                        tPropertyBag._FastCache.TryAdd(t, true.ToString());
                    else
                        tPropertyBag._FastCache.TryAdd(t.Substring(0, tP), t.Substring(tP + 1));
                }
            }
            return tPropertyBag;
        }

        internal static string PropBagGetName(string pBag, string pSep)
        {
            if (string.IsNullOrEmpty(pBag) || string.IsNullOrEmpty(pSep)) return "";
            string retStr = "";
            try
            {
                int pos = pBag.IndexOf(pSep, StringComparison.Ordinal);
                retStr = pos > 0 ? pBag.Substring(0, pos) : pBag;
            }
            catch
            {
                //ignored
            }
            return retStr;
        }
        internal static string PropBagGetValue(string pBag, string pSep)
        {
            if (string.IsNullOrEmpty(pBag) || string.IsNullOrEmpty(pSep)) return "";
            string retStr = "";
            try
            {
                int pos = pBag.IndexOf(pSep, StringComparison.Ordinal);
                //string[] tt = TheCommonUtils.cdeSplit(pBag, pSep, false, false);
                retStr = pos > 0 ? pBag.Substring(pos + pSep.Length) : "";
            }
            catch
            {
                //ignored
            }
            return retStr;
        }
        internal static string PropBagGetValueAndName(string pBag, string pSep, out string name)
        {
            name = "";
            if (string.IsNullOrEmpty(pBag) || string.IsNullOrEmpty(pSep)) return "";
            string retStr = "";
            try
            {
                int pos = pBag.IndexOf(pSep, StringComparison.Ordinal);
                //string[] tt = TheCommonUtils.cdeSplit(pBag, pSep, false, false);
                retStr = pos > 0 ? pBag.Substring(pos + pSep.Length) : "";
                name = pos > 0 ? pBag.Substring(0, pos) : pBag;
            }
            catch
            {
                //ignored
            }
            return retStr;
        }
        /// <summary>
        /// Updates a value of a given key in the propertybag
        /// </summary>
        /// <param name="pQ">Property bag</param>
        /// <param name="pProps">Name, properties list separated with :;:</param>
        /// <param name="Seperator">Bag Name/Value separator</param>
        /// <returns></returns>
        public static bool PropBagUpdateValues(ThePropertyBag pQ, string pProps, string Seperator)
        {
            if (pQ == null || string.IsNullOrEmpty(pProps)) return false;
            bool WasInserted = false;
            lock (pQ)
            {
                string[] plist = TheCommonUtils.cdeSplit(pProps, ":;:", false, false);
                foreach (string pP in plist)
                {
                    bool t = PropBagUpdateValue(pQ, pP, Seperator);
                    if (t && !WasInserted)
                        WasInserted = true;
                }
            }
            return WasInserted;
        }
        /// <summary>
        /// Updates a value of a given key in the propertybag
        /// </summary>
        /// <param name="pQ">Property bag</param>
        /// <param name="pName">Name of the key</param>
        /// <param name="Seperator">Bag Name/Value separator</param>
        /// <param name="pValue">New Valule</param>
        /// <returns></returns>
        public static bool PropBagUpdateValue(ThePropertyBag pQ, string pName, string Seperator, string pValue)
        {
            return PropBagUpdateValue(pQ, pName, Seperator, pValue, false);
        }
        /// <summary>
        /// Updates a value of a given key in the propertybag
        /// </summary>
        /// <param name="pQ">Property bag</param>
        /// <param name="pName">Name of the key</param>
        /// <param name="Seperator">Bag Name/Value separator</param>
        /// <param name="pValue">New Valule</param>
        /// <param name="AllowDuplicates">if true, the bag can have the same key twice</param>
        /// <returns></returns>
        public static bool PropBagUpdateValue(ThePropertyBag pQ, string pName, string Seperator, string pValue, bool AllowDuplicates)
        {
            if (string.IsNullOrEmpty(pName) || pQ == null || string.IsNullOrEmpty(pValue)) return false;
            //if (string.IsNullOrEmpty(pValue)) pValue = "true";
            bool WasInserted = false;
            lock (pQ)
            {
                string toInser = pName.Trim() + Seperator;
                string retStr = "";
                if (!AllowDuplicates && (retStr = pQ.FirstOrDefault(s => s.StartsWith(toInser, StringComparison.OrdinalIgnoreCase))) != null)
                {
                    pQ.Remove(retStr);
                    if (pQ._FastCache != null)
                        pQ._FastCache.TryRemove(pName.Trim(), out string tOldValue);
                }
                else
                    WasInserted = true;
                try
                {
                    pQ.Add(toInser + TheCommonUtils.GenerateFinalStr(pValue));
                    if (pQ._FastCache != null)
                        pQ._FastCache.TryAdd(pName.Trim(), TheCommonUtils.GenerateFinalStr(pValue));
                }
                catch
                {
                    WasInserted = false;
                }
            }
            return WasInserted;
        }
        /// <summary>
        /// Updates a value of a given key in the propertybag
        /// </summary>
        /// <param name="pQ">Property bag</param>
        /// <param name="pFiller">Name=value for the change</param>
        /// <param name="Seperator">Bag Name/Value separator</param>
        /// <returns></returns>
        public static bool PropBagUpdateValue(ThePropertyBag pQ, string pFiller, string Seperator)
        {
            if (string.IsNullOrEmpty(pFiller) || pQ == null) return false;
            string pName = PropBagGetName(pFiller, Seperator);
            string pValue = PropBagGetValue(pFiller, Seperator);
            bool WasInserted = false;
            lock (pQ)
            {
                string toInser = pName.Trim() + Seperator;
                string retStr = "";
                if ((retStr = pQ.FirstOrDefault(s => s.StartsWith(toInser, StringComparison.OrdinalIgnoreCase))) != null)
                {
                    pQ.Remove(retStr);
                    if (pQ._FastCache != null)
                        pQ._FastCache.TryRemove(pName.Trim(), out string tOldValue);
                }
                else
                    WasInserted = true;
                pQ.Add(toInser + pValue);
                if (pQ._FastCache != null)
                    pQ._FastCache.TryAdd(pName.Trim(), pValue);
            }
            return WasInserted;
        }

        /// <summary>
        /// Removes a given key from the propertybag
        /// </summary>
        /// <param name="pQ">Property bag</param>
        /// <param name="pName">Name of they key to delete</param>
        /// <param name="Seperator">Bag Name/Value separator</param>
        /// <returns></returns>
        public static bool PropBagRemoveName(ThePropertyBag pQ, string pName, string Seperator)
        {
            if (string.IsNullOrEmpty(pName) || pQ == null) return false;
            bool WasRemoved = false;
            lock (pQ)
            {
                string toInser = pName.Trim() + Seperator;
                string retStr = "";
                if ((retStr = pQ.FirstOrDefault(s => s.StartsWith(toInser, StringComparison.OrdinalIgnoreCase))) != null)
                {
                    pQ.Remove(retStr);
                    if (pQ._FastCache != null)
                        pQ._FastCache.TryRemove(pName.Trim(), out string tOldValue);
                    WasRemoved = true;
                }
            }
            return WasRemoved;
        }

        internal static ThePropertyBag GetLocalizedBag(ThePropertyBag pQ, int lcid, string engine)
        {
            var newBag = new ThePropertyBag();
            if (pQ == null || pQ.Count == 0) return newBag;

            foreach (string t in pQ.ToList())
            {
                var value = PropBagGetValueAndName(t, "=", out string name).Trim();
                if (TheNMIEngine.IsCDEProperty(name))
                {
                    newBag.Add(t);
                    continue;
                }
                var locValue = TheBaseAssets.MyLoc.GetLocalizedStringByKey(lcid, engine, value);
                if (locValue != value)
                {
                    newBag.Add($"{name}={locValue}");
                }
                else
                {
                    newBag.Add(t);
                }
            }
            return newBag;
        }
        #endregion
    }
}