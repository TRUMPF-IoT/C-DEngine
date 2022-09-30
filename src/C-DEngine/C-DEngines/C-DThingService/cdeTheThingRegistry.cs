// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

#pragma warning disable CS1591    //TODO: Remove and document public methods

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// A class definition for the Property Mapper
    /// </summary>
    internal class ThePropertyMapperInfo : TheDataBase
    {
        public Guid SourceThing { get; set; }
        public string SourceProperty { get; set; }
        public Guid TargetThing { get; set; }
        public string TargetProperty { get; set; }
        public bool IsBidirectional { get; set; }

        public DateTimeOffset LastUpdate { get; set; }
        public Action<cdeP> SourceCallback { get; set; }
        public Action<cdeP> TargetCallback { get; set; }
    }
    /// <summary>
    /// TheThingRegistry is the central management database for all Things known to this node
    /// </summary>
    public class TheThingRegistry
    {
        internal Action<bool> eventEngineReady;
        private TheStorageMirror<TheThing> MyThings;
        private TheStorageMirror<ThePropertyMapperInfo> MyPropertyMaps;
        private readonly cdeConcurrentDictionary<string, TheThing> mEngineCache = new ();
        internal TheThingRegistry()
        {
        }

        internal void Init()
        {
            MyPropertyMaps = new TheStorageMirror<ThePropertyMapperInfo>(TheCDEngines.MyIStorageService)
            {
                CacheTableName = "ThePropertyMaps",
                IsRAMStore = true,
                BlockWriteIfIsolated = true
            };
            MyPropertyMaps.InitializeStore(false, false, true, true);

            TheCDEngines.eventEngineShutdown += sinkShutdown;

            bool Success = true;
            MyThings = (TheStorageMirror<TheThing>)TheCDEngines.GetStorageMirror("TheThingRegistry");
            if (MyThings == null)
            {
                MyThings = new TheStorageMirror<TheThing>(TheCDEngines.MyIStorageService)
                {
                    CacheTableName = "TheThingRegistry",
                    IsRAMStore = true,
                    CacheStoreInterval = TheBaseAssets.MyServiceHostInfo.ThingRegistryStoreInterval,
                    IsStoreIntervalInSeconds = true,
                    IsCachePersistent = true, // New in V4: All nodes can load ThingRegistry - some can store  TheBaseAssets.MyServiceHostInfo.cdeNodeType == cdeNodeType.Relay,
                    UseSafeSave = true,
                    BlockWriteIfIsolated = true
                };
                TheStorageMirrorParameters tStoreParams = new ()
                {
                    ResetContent = TheBaseAssets.MyServiceHostInfo.IsNewDevice,
                    LoadSync = true
                };
                MyThings.MyMirrorCache.FastSaveLock = TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("ThingRegistryFastSave"));
                Task<bool> tRes = MyThings.InitializeStoreAsync(tStoreParams);
                tRes.Wait();
                Success = tRes.Result;
                if (!Success)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(13424, new TSM(eEngineName.ThingService, "Thing Registry Store could not be initialized in time! Node is in unstable state and will shutdown", eMsgLevel.l1_Error));
                    TheBaseAssets.MyApplication.Shutdown(true);
                    return;
                }
            }
            MyThings.RegisterEvent(eStoreEvents.HasUpdates, sinkHasUpdates); //Can come after InitStore as only new updates should cause this event to fire
            MyThings.AllowFireUpdates = true;   //can come after register event
            eventEngineReady?.Invoke(Success);
        }

        void sinkShutdown()
        {
            MyThings?.ForceSave();
        }

        void sinkHasUpdates(StoreEventArgs pArgs)
        {
            if (pArgs.Para is TheStorageMirror<TheThing>.StoreResponse pUpdates && !pUpdates.HasErrors && pUpdates.MyRecords.Count > 0)
            {
                TheNMIEngine.UpdateUXItemACLFromThing(pUpdates.MyRecords[0]);
                TheCDEngines.MyThingEngine.FireEvent(eThingEvents.ThingUpdated, pUpdates.MyRecords[0], null, true);    //For plugins to register for ThingUpdates.
            }
        }

        #region Static Helper Methods
        internal static bool DeleteThingByID(Guid pID)
        {
            if (Guid.Empty == pID) return false;
            if (TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.ContainsID(pID))
            {
                TheThing tThing = TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.GetEntryByID(pID);
                if (tThing != null)
                {
                    if (TheBaseAssets.MyServiceHostInfo.IsIsolated)
                        TheQueuedSenderRegistry.PublishToMasterNode(new TSM(eEngineName.ContentService, "CDE_UNREGISTERTHING", pID.ToString()));
                    return DeleteThing(tThing.EngineName, tThing);
                }
            }
            return false;
        }

        /// <summary>
        /// Deletes the first TheThing found with the given Property Name and Value. I.e: DeleteThingByProperty("MyEngine",Guid.empty,"DeviceType","MyThingType"); will delete the first things of the MyEngine with the DeviceType "MyThingType"
        /// </summary>
        /// <param name="pEngineName">Engine that owns TheThing to be deleted</param>
        /// <param name="pUID">UID of the user requesting the Deletion - if TheThing has a Acces Level, the user has to have the same access level in order to be allowed to delete TheThing</param>
        /// <param name="pPropName">Name of the Property</param>
        /// <param name="pPropValue">Value of the Property</param>
        /// <returns>Return the deleted Thing</returns>
        public static TheThing DeleteThingByProperty(string pEngineName, Guid pUID, string pPropName, string pPropValue)
        {
            TheThing t = GetThingByProperty(pEngineName, pUID, pPropName, pPropValue);
            if (t != null && DeleteThing(pEngineName, t))
                    return t;
            return null;
        }

        /// <summary>
        /// Deletes all TheThings found with the given Property Name and Value. I.e: DeleteThingByProperty("MyEngine",Guid.empty,"DeviceType","MyThingType"); will delete all things of the MyEngine with the DeviceType "MyThingType"
        /// </summary>
        /// <param name="pEngineName">Engine that owns TheThing to be deleted</param>
        /// <param name="pUID">UID of the user requesting the Deletion - if TheThing has a Acces Level, the user has to have the same access level in order to be allowed to delete TheThing</param>
        /// <param name="pPropName">Name of the Property</param>
        /// <param name="pPropValue">Value of the Property</param>
        /// <returns>Returns true if successful and false if no Things matching the Prop/Val were found</returns>
        public static bool DeleteThingsByProperty(string pEngineName, Guid pUID, string pPropName, string pPropValue)
        {
            List<TheThing> tList = GetThingsByProperty(pEngineName, pUID, pPropName, pPropValue);
            if (tList != null && tList.Count > 0)
            {
                foreach (TheThing t in tList)
                    DeleteThing(pEngineName, t);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Delets the given thing from TheThingRegistry
        /// </summary>
        /// <param name="pThing">ICDEThing of the Thing to be deleted</param>
        /// <returns></returns>
        public static bool DeleteThing(ICDEThing pThing)
        {
            if (pThing == null)
            {
                return false;
            }
            return DeleteThing(pThing.GetBaseThing());

        }
        /// <summary>
        /// Delets the given thing from TheThingRegistry
        /// </summary>
        /// <param name="pThing">TheThing to be deleted</param>
        /// <returns></returns>
        public static bool DeleteThing(TheThing pThing)
        {
            if (pThing == null)
            {
                return false;
            }
            return DeleteThingByID(pThing.cdeMID);
        }

        /// <summary>
        /// Deletes the given ICDEThing of the given Engine
        /// </summary>
        /// <param name="pEngineName">Engine Name owning TheThing</param>
        /// <param name="pThing">ICDEThing interface of TheThing</param>
        /// <returns></returns>
        public static bool DeleteThing(string pEngineName, ICDEThing pThing)
        {
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null || pThing == null || string.IsNullOrEmpty(pEngineName) ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null) return false;

            TheThing tThing = pThing.GetBaseThing();
            if (tThing == null) return false;

            tThing.Delete();

            if (TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.ContainsID(tThing.cdeMID))
            {
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.RemoveAnItemByID(tThing.cdeMID, null);
                _thingByIdCache = null;
                IBaseEngine tBase = tThing.GetBaseEngine();
                if (tBase != null)
                {
                    var engine = tBase as TheBaseEngine;
                    engine?.ReleaseLicense(tThing);
                    tBase.FireEvent(eEngineEvents.ThingDeleted, tThing, true);
                }
                TheCDEngines.MyNMIService.FireEvent(eThingEvents.ThingDeleted, TheCDEngines.MyNMIService, tThing, true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Updates the given Thing in TheThingRegistry- if Fire Update is true, a ThingUpdated event on TheThingRegistry will be fired for TheRulesEngine. If set to "False" TheRulesEngine will not be notified.
        /// TheThingRegistry's StorageMirror will also NOT fire HasUpdates if FireUpdate is set to false.
        /// TheBaseEngine of TheThing will always get eEngineEvents.ThingUpdated if this function is called.
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="FireUpdate"></param>
        /// <returns></returns>
        public static TheThing UpdateThing(ICDEThing pThing, bool FireUpdate)
        {
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null || pThing == null ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null) return null;

            TheThing tThing = pThing.GetBaseThing();
            if (tThing == null) return null;

            var existingThing = GetThingByMID(tThing.cdeMID);
            if (existingThing != null && existingThing != tThing)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(13424, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"A different TheThing instance was already registered: {tThing} - Existing: {existingThing}", eMsgLevel.l2_Warning));
            }

            bool AllowFireOld = TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.AllowFireUpdates;
            if (!FireUpdate) TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.AllowFireUpdates = false;
            TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.UpdateItem(tThing, null);
            _thingByIdCache = null;

            if (!FireUpdate) TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.AllowFireUpdates = AllowFireOld;
            if (FireUpdate) // && eventThingUpdated != null)
            {
                TheCDEngines.MyThingEngine.FireEvent(eThingEvents.ThingUpdated, tThing, null, true);
                IBaseEngine tBase = tThing.GetBaseEngine();
                tBase?.FireEvent(eEngineEvents.ThingUpdated, tThing, true);
            }
            TheNMIEngine.UpdateUXItemACLFromThing(tThing);
            return tThing;
        }

        /// <summary>
        /// Registers a Thing with TheThingRegistry
        /// Properties of TheThing that is referenced in ICDEThing will be persisted in TheThingRegistry and "Init()" and "CreateUX()" will be called
        /// </summary>
        /// <param name="pThing">Pointer to the ICDEThing interface to be persisted</param>
        /// <returns></returns>
        public static TheThing RegisterThing(ICDEThing pThing)
        {
            return RegisterThing(pThing, false);
        }

        /// <summary>
        /// Registers a Thing with TheThingRegistry
        /// Properties of TheThing that is referenced in ICDEThing will be persisted in TheThingRegistry and "Init()" and "CreateUX()" will be called
        /// </summary>
        /// <param name="pThing">Pointer to the ICDEThing interface to be persisted</param>
        /// <param name="RegisterGlobally">If set to true, changed to TheThing Properties will be published across the mesh to allow for distributed rules</param>
        /// <returns></returns>
        public static TheThing RegisterThing(ICDEThing pThing, bool RegisterGlobally)
        {
            if (pThing == null || TheCDEngines.MyThingEngine?.MyThingRegistry?.MyThings == null)
            {
                TheBaseAssets.MySYSLOG?.WriteToLog(13424, new TSM(eEngineName.ThingService, "RegisterThing: Invalid arguments or system not initialized", eMsgLevel.l1_Error, ""));
                return null;
            }


            TheThing tThing = pThing?.GetBaseThing();
            if (tThing == null)
            {
                TheBaseAssets.MySYSLOG?.WriteToLog(13424, new TSM(eEngineName.ThingService, string.Format("No base thing found for {0}", pThing), eMsgLevel.l1_Error)); // Nothing much we can log at this point: get at least the type or default ToString()
                return null;
            }

            IBaseEngine tBase = tThing.GetBaseEngine();

            TheThing tThingToReturn = tThing;
            bool IsNewThing = false;
            TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.MyRecordsRWLock.RunUnderUpgradeableReadLock(() =>
            {
                var existingThing = TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.GetEntryByID(tThing.cdeMID);
                if (existingThing != null && existingThing != tThing)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(13424, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Thing already registered: {pThing} - Existing: {existingThing}", eMsgLevel.l1_Error));
                    tThingToReturn = existingThing;
                    return;
                }
                IsNewThing = existingThing == null;

                if (tBase == null)
                {
                    // CODE REVIEW: tBase == null can allow callers to bypass license checks, but is required for scratch things (in NMI?)
                    TheBaseAssets.MySYSLOG.WriteToLog(13424, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ThingService, string.Format("No IBaseEngine set for {0} - {1} - {2}", tThing, tThing.EngineName, tThing.DeviceType), eMsgLevel.l2_Warning, String.Format("{0}, {1}, {2}, {3}", tThing?.cdeMID, tThing?.Address, tThing?.FriendlyName, tThing?.ID)));
                }
                else
                {
                    if (IsNewThing) // && !(pThing is TheRulesEngine) && !(pThing is TheThingRule))
                    {
                        if (tBase is not TheBaseEngine baseEngine)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(13424, new TSM(eEngineName.ThingService, string.Format("No base engine found for {0} - {1} - {2}", tThing, tThing.EngineName, tThing.DeviceType), eMsgLevel.l1_Error, String.Format("{0}, {1}, {2}, {3}", tThing?.cdeMID, tThing?.Address, tThing?.FriendlyName, tThing?.ID)));
                            tThingToReturn = null;
                            return;
                        }
                        if (!string.IsNullOrEmpty(tThing.EngineName) && baseEngine.GetEngineName() != tThing.EngineName)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(13424, new TSM(eEngineName.ThingService, string.Format("Engine name mismatch found for {0} - {1} - {2}", tThing, tThing.EngineName, baseEngine.GetEngineName()), eMsgLevel.l1_Error, String.Format("{0}, {1}, {2}, {3}", tThing?.cdeMID, tThing?.Address, tThing?.FriendlyName, tThing?.ID)));
                            tThingToReturn = null;
                            return;
                        }

                        // perform licensing check
                        if (!baseEngine.CheckAndAcquireLicense(tThing)) // CODE REVIEW: Should we add this to IBaseEngine? Define a better way to get at TheBaseEngine?
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(13424, new TSM(eEngineName.ThingService, string.Format("No valid license or thing entitlement for {0} - {1}", tBase.GetEngineName(), tThing.DeviceType), eMsgLevel.l1_Error, String.Format("{0}, {1}, {2}, {3}", tThing?.cdeMID, tThing?.Address, tThing?.FriendlyName, tThing?.ID)));
                            tThingToReturn = null;
                            return;
                        }
                    }

                    // Declare any config properties specified via the ConfigPropertyAttribute
                    var deviceTypes = tBase?.GetDeviceTypes();
                    var deviceTypeInfo = deviceTypes?.FirstOrDefault(dt => dt.DeviceType == tThing.DeviceType);
                    if (deviceTypeInfo?.ConfigProperties?.Count > 0)
                    {
                        foreach (var configProperty in deviceTypeInfo.ConfigProperties)
                        {
                            tThing.DeclareConfigProperty(configProperty);
                        }
                    }
                    if (deviceTypeInfo?.SensorProperties?.Count > 0)
                    {
                        foreach (var sensorProperty in deviceTypeInfo.SensorProperties)
                        {
                            tThing.DeclareSensorProperty(sensorProperty);
                        }
                    }
                    if (deviceTypeInfo?.Capabilities?.Length > 0)
                    {
                        foreach (var cap in deviceTypeInfo.Capabilities)
                        {
                            tThing.AddCapability(cap);
                        }
                    }
                    OPCUATypeAttribute.ApplyUAAttributes(tThing.GetObject()?.GetType(), tThing);
                }

                // Make sure the historin gets wired up before any updates are made to the thing: important for permanent consumers
                //bool NoSafeSave = TheThing.GetSafePropertyBool(tThing, "HistorianNoSafeSave"); //This Needs to be set on the target Thing...
                TheStorageMirrorHistorian.RegisterThingWithHistorian(tThing, false/*, NoSafeSave*/); //CODE-REVIEW: at this point its not clear if a Historian has to use SafeSave! Can this be changed later on? It looks like at this point no store is created correct?
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.UpdateItem(tThing, null);
                _thingByIdCache = null;
            });

            if (tThingToReturn != null)
            {
                if (TheCDEngines.MyThingEngine != null)
                {
                    try
                    {
                        if ((pThing.GetBaseThing()?.IsDisabled != true) && !pThing.IsInit())
                        {
                            DateTime tStart = DateTime.Now;
                            tThing.Init(); // Must call TheThing.Init() as locking is done there
                            if (tThing.IsOnLocalNode()) //Must not be set and send back to remote node
                                TheThing.SetSafePropertyNumber(tThing, "cdeStartupTime", Math.Round(DateTime.Now.Subtract(tStart).TotalSeconds, 2));
                        }
                        tBase?.FireEvent(eEngineEvents.ThingInitCalled, tThing, true);
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(13424, new TSM(eEngineName.ThingService, string.Format("ThingInit for {0} Failed", pThing), eMsgLevel.l1_Error, e.ToString()));
                    }
                }
                if (TheNMIEngine.IsInitialized() && (pThing.GetBaseThing()?.IsDisabled != true) && !pThing.IsUXInit())
                {
                    try
                    {
                        tThing.CreateUX(); // Must call through TheThing.CreateUx() because that's where locking occurs
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(13424, new TSM(eEngineName.ThingService, string.Format("ThingUXInit for {0} Failed", pThing), eMsgLevel.l1_Error, e.ToString()));
                    }
                }
                if (RegisterGlobally || (tThing.IsOnLocalNode() && TheThing.GetSafePropertyBool(tThing, "IsRegisteredGlobally")))
                    RegisterThingGlobally(tThing);

                if (IsNewThing)
                {
                    tBase?.FireEvent(eEngineEvents.ThingRegistered, tThing, true);

                    if (TheBaseAssets.MyServiceHostInfo.IsIsolated && !TheThing.GetSafePropertyBool(pThing, "IsRegisteredGlobally") && tThing.DeviceType != "IBaseEngine")
                        RegisterThingWithMaster(tThing);
                }
                else
                {
                    TheCDEngines.MyThingEngine.FireEvent(eThingEvents.ThingUpdated, tThing, null, true);
                    tBase?.FireEvent(eEngineEvents.ThingUpdated, tThing, true);
                }
            }
            return tThingToReturn;
        }

        internal static bool RegisterThingWithMaster(TheThing pThing)
        {
            if (pThing == null) return false;
            TSM tTSM = new (eEngineName.ContentService, "CDE_REGISTERTHING", TheCommonUtils.SerializeObjectToJSONString(pThing));
            TheQueuedSenderRegistry.PublishToMasterNode(tTSM);
            return true;
        }

        /// <summary>
        /// Sends a publication out to all Nodes in the Mesh and registers this Thing in other TheThingRegistry of all nodes
        /// Property changes are then sent to all nodes as well and TheRulesEngine of remote notes can be triggered by Properties of the Origin Thing or set Actions on a remote thing.
        /// </summary>
        /// <param name="pThing">The ICDEThing to be registered globally</param>
        /// <returns></returns>
        public static bool RegisterThingGlobally(ICDEThing pThing)
        {
            TheThing tThing = pThing.GetBaseThing();
            if (tThing == null) return false;
            return RegisterThingGlobally(tThing);
        }
        /// <summary>
        /// Sends a publication out to all Nodes in the Mesh and registers this Thing in other TheThingRegistry of all nodes
        /// Property changes are then sent to all nodes as well and TheRulesEngine of remote notes can be triggered by Properties of the Origin Thing or set Actions on a remote thing.
        /// </summary>
        /// <param name="pThing">TheThing to be registered globally</param>
        /// <returns></returns>
        public static bool RegisterThingGlobally(TheThing pThing)
        {
            if (pThing == null) return false;
            TheThing.SetSafePropertyBool(pThing, "IsRegisteredGlobally", true);
            TSM tTSM = new (eEngineName.ContentService, "CDE_REGISTERTHING", TheCommonUtils.SerializeObjectToJSONString(pThing));
            tTSM.SetToServiceOnly(true);
            TheCommCore.PublishCentral(tTSM);
            return true;
        }

        /// <summary>
        /// Unregisters a Thing globally
        /// </summary>
        /// <param name="pThing">TheThing to unregister</param>
        /// <returns></returns>
        public static bool UnregisterThingGlobally(TheThing pThing)
        {
            if (pThing == null) return false;
            TheThing.SetSafePropertyBool(pThing, "IsRegisteredGlobally", false);
            TSM tTSM = new (eEngineName.ContentService, "CDE_UNREGISTERTHING", pThing.cdeMID.ToString());
            tTSM.SetToServiceOnly(true);
            TheCommCore.PublishCentral(tTSM);
            return true;
        }


        static DateTimeOffset _lastGlobalThingRequest = DateTimeOffset.MinValue;
        /// <summary>
        /// Requests that other nodes in the mesh send any things that are marked as global. Resulting things will arrive asynchronously from this call.
        /// </summary>
        /// <returns>true if request was sent. false if called more once per minute.</returns>
        public static bool RequestGlobalThings()
        {
            if (DateTimeOffset.Now.Subtract(_lastGlobalThingRequest).TotalSeconds < 60000)
            {
                return false;
            }
            TheCommCore.PublishCentral(new TSM(eEngineName.ContentService, "CDE_SEND_GLOBAL_THINGS"));
            return true;
        }

        /// <summary>
        /// Synchronizes all Global Things
        /// </summary>
        /// <param name="nodeId">Device/NodeID to sync global things with</param>
        /// <param name="things">List of things to sync</param>
        /// <param name="OnlyMerge">If true, things not listed in "things" will not be deleted from current ThingRegistry</param>
        /// <returns></returns>
        internal static bool SyncGlobalThings(Guid nodeId, List<TheThing> things, bool OnlyMerge = false)
        {
            if (things == null || nodeId == TheBaseAssets.MyServiceHostInfo?.MyDeviceInfo?.DeviceID || TheCDEngines.MyThingEngine?.MyThingRegistry?.MyThings == null)
                return false;

            if (things.Any((t) => t.cdeN != nodeId)) // Don't take things from nodes that are not the originator: assume malicious sender and reject entire list
            {
                TheBaseAssets.MySYSLOG.WriteToLog(13424, new TSM(eEngineName.ThingService, $"Received invalid global thing list for node {nodeId}. Rejecting list.", eMsgLevel.l1_Error, ""));
                return false;
            }

            if (!OnlyMerge)
            {
                var currentThingsForNode = GetThingsByFunc("*", (t) => t.cdeN == nodeId, true);
                foreach (var thing in currentThingsForNode)
                {
                    if (things.Find((t) => t.cdeMID == thing.cdeMID) == null)
                    {
                        DeleteThing(thing.EngineName, thing);
                    }
                }
            }
            foreach (var thing in things)
            {
                if (thing.cdeN == nodeId && GetThingByMID(thing.cdeMID, true) == null)
                {
                    // Do not re-register existing things. Otherwise we introduce race conditions between thing sync and property updates
                    RegisterThing(thing);
                }
                else
                {
                    TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.UpdateItem(thing, null);    //New 3.215: updates all properties
                    _thingByIdCache = null;
                    TheCDEngines.MyThingEngine.FireEvent(eThingEvents.ThingUpdated, thing, null, true);    //For plugins to register for ThingUpdates.
                }
            }
            return true;
        }

        private static TheNodeHost MyNodeHost = null;
        internal static bool RegisterHost()
        {
            TheThing tThing = GetThingByProperty("*", "ID", TheBaseAssets.MyAppInfo.cdeMID.ToString());
            MyNodeHost = new TheNodeHost(tThing);
            return RegisterThing(MyNodeHost) != null;
        }

        /// <summary>
        /// Returns the value for a Property of the Hosting Node.
        /// </summary>
        /// <param name="pName">Property name.</param>
        /// <returns>Value of the property, or null if not found.</returns>
        public static object GetHostProperty(string pName)
        {
            return MyNodeHost?.GetProperty(pName, false)?.Value;
        }

        /// <summary>
        /// Sets a Host Property
        /// </summary>
        /// <param name="pName">Property name.</param>
        /// <param name="pValue">Property value</param>
        public static void SetHostProperty(string pName, object pValue)
        {
            MyNodeHost?.SetProperty(pName, pValue);
        }

        internal static bool RegisterEngine(TheBaseEngine tBase, bool IsIsolated = false, bool WillBeIsolated = false)
        {
            // CODE REVIEW: Better way to allow Mini Relays? Or do we want to have a license check here as well?
            if (tBase.AssociatedPlugin is not TheMiniRelayEngine && !tBase.CheckEngineLicense())
            {
                TheBaseAssets.MySYSLOG.WriteToLog(13424, new TSM(eEngineName.ThingService, string.Format("No valid license for {0}", tBase.GetEngineName()), eMsgLevel.l1_Error, ""));
                return false;
            }
            if (tBase.AssociatedPlugin is ICDEThing tEngineThing && !tEngineThing.IsInit())
            {
                if (TheCommonUtils.CDbl(tBase.GetVersion()) <= 0)
                {
                    tBase.SetVersion(TheCommonUtils.GetAssemblyVersion(tEngineThing));
                }
                TheThing tThing = GetBaseEngineAsThing(tBase.GetEngineName(), IsIsolated);
                if (tThing == null)
                {
                    tThing = new TheThing();
                    TheThing.SetSafePropertyDate(tThing, "RegisterDate", DateTimeOffset.Now);
                }
                else
                {
                    if (IsIsolated && tThing.cdeN != TheBaseAssets.MyServiceHostInfo.MyDeviceInfo?.DeviceID)
                    {
                        tThing.cdeN = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
                        tThing.cdeO = tThing.cdeN;
                    }
                    if (tThing.IsDisabled && !WillBeIsolated)
                        tBase.GetEngineState().IsUnloaded = true;
                    if (TheThing.GetSafePropertyDate(tThing, "RegisterDate") == DateTimeOffset.MinValue)
                        TheThing.SetSafePropertyDate(tThing, "RegisterDate", tThing.cdeCTIM);
                }
                if (WillBeIsolated && TheThing.GetSafePropertyGuid(tThing, "ISONode") == Guid.Empty)
                    TheThing.SetSafePropertyGuid(tThing, "ISONode", Guid.NewGuid());
                tThing.ID = tBase.GetEngineID().ToString();
                tThing.DeviceType = eKnownDeviceTypes.IBaseEngine;
                tThing.StatusLevel = 4;
                tThing.FriendlyName = tBase.GetFriendlyName();
                tThing.EngineName = tBase.GetEngineName();
                tThing.MyBaseEngine = tBase;
                double tOldVersion = TheCommonUtils.CDbl(tThing.Value);
                tThing.Version = tBase.GetVersion();
                if (tOldVersion != TheCommonUtils.CDbl(tThing.Version))
                    TheThing.SetSafePropertyDate(tThing, "UpdateDate", DateTimeOffset.Now);
                tThing.RegisterEvent(eEngineEvents.BlobReceived, null);
                tThing.RegisterEvent(eEngineEvents.CostingWasUpdated, null);
                tThing.RegisterEvent(eEngineEvents.CustomTSMMessage, null);
                tThing.RegisterEvent(eEngineEvents.EngineHasStarted, null);
                tThing.RegisterEvent(eEngineEvents.EngineHasStopped, null);
                tThing.RegisterEvent(eEngineEvents.EngineIsReady, null);
                tThing.RegisterEvent(eEngineEvents.IncomingMessage, null);
                tThing.RegisterEvent(eEngineEvents.MessageProcessed, null);
                tThing.RegisterEvent(eEngineEvents.NewSubscription, null);
                tThing.RegisterEvent(eEngineEvents.ShutdownEvent, null);

                tEngineThing.SetBaseThing(tThing);
                tThing.SetIThingObject(tEngineThing);
                return RegisterThing(tThing) != null;
            }
            return false;
        }

        /// <summary>
        /// NOT IMPLEMENTED YET: Returns Owner Thing from a remote Thing
        /// </summary>
        /// <param name="thingAddress"></param>
        /// <returns></returns>
        public static Task<TheThing> GetOwnedThingAsync(TheMessageAddress thingAddress)
        {
            return GetOwnedThingAsync(thingAddress, createDefaultTimeout);
        }

        /// <summary>
        /// NOT IMPLEMENTED YET: Will retreive a thing from a different node
        /// </summary>
        /// <param name="thingAddress"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public static Task<TheThing> GetOwnedThingAsync(TheMessageAddress thingAddress, TimeSpan timeout)
        {
            var thing = GetThingByMID(thingAddress.ThingMID);
            if (thing == null)
            {
                return TheCommonUtils.TaskFromResult<TheThing>(null);
            }
            return WaitForInitializeAsync(thing, timeout);
        }

        static readonly TimeSpan createDefaultTimeout = new(0, 1, 0);

        public static class eOwnerProperties
        {
            public const string cdeOwner = "cdeOwner";
            public const string cdeOwnerInstance = "cdeOwnerInstance";
            public const string cdeHidden = "cdeHidden";
            public const string cdeReadOnly = "cdeReadOnly";
        }
        /// <summary>
        /// Parameters for creating a thing instance of another plug-in.
        /// </summary>
        public class MsgCreateThingRequestV1
        {
            /// <summary>
            /// Indicates if the thing instance is to be created if it does not exist.
            /// </summary>
            public bool CreateIfNotExist;
            /// <summary>
            /// Indicates if the thing instance is to be updated with the address and other properties if it already exists
            /// </summary>
            public bool? DoNotModifyIfExists;
            /// <summary>
            /// The address of the owner, typically the owning Thing.
            /// </summary>
            public TheMessageAddress OwnerAddress;
            /// <summary>
            /// Indicates if the thing is to be visible to the user/administrator, or if it is to be kept internal to programmatic use only (CreateUX will not be called if Hidden == true).
            /// </summary>
            public bool Hidden;
            /// <summary>
            /// Indicates if the thing is to be readonly for all users and administrators, or if it is to be usable like a manually created thing.
            /// </summary>
            public bool ReadOnly;
            /// <summary>
            /// Advanced use only (i.e. import of a backup): forces the cdeMID to be matched or created exactly
            /// </summary>
            public Guid? ThingMID;
            /// <summary>
            /// The name of the plug-in of which an instance is to be created.
            /// </summary>
            public string EngineName;
            /// <summary>
            /// The DeviceType of the thing instance to be created.
            /// </summary>
            public string DeviceType;
            /// <summary>
            /// The ID property for the thing instance to be created.
            /// </summary>
            public string ID;
            /// <summary>
            /// The Address property for the thing instance to be created.
            /// </summary>
            public string Address;
            /// <summary>
            /// An identifier that can be used by the owner to distinguish between multiple instances of the same DeviceType/Address.
            /// </summary>
            public string InstanceId;
            /// <summary>
            /// The FriendlyName property for the thing instance to be created.
            /// </summary>
            public string FriendlyName;
            /// <summary>
            /// A dictionary containing additional properties to be set on the thing instance to be created.
            /// </summary>
            public Dictionary<string, object> Properties;
            /// <summary>
            /// A list of cdeP properties to be set on the thing instance. Use in addition to Properties when cdeP.cdeT or cdeP.cdeE need to be set.
            /// </summary>
            public List<cdeP> cdeProperties;
        }

        public class MsgCreateThingResponseV1
        {
            public TheMessageAddress ThingAddress;
            public string Error;
        }
        public static Task<TheThing> CreateOwnedThingAsync(bool createIfNotExist, TheThing owner, bool hidden, string engineName, string deviceType, string address, string instanceId, string friendlyName, Dictionary<string, object> properties)
        {
            return CreateOwnedThingAsync(createIfNotExist, owner, hidden, engineName, deviceType, address, instanceId, friendlyName, properties, createDefaultTimeout);
        }

        public static Task<TheThing> CreateOwnedThingAsync(bool createIfNotExist, TheThing owner, bool hidden, string engineName, string deviceType, string address, string instanceId, string friendlyName, Dictionary<string, object> properties, TimeSpan timeout)
        {
            var createParams = new MsgCreateThingRequestV1
            {
                CreateIfNotExist = createIfNotExist,
                OwnerAddress = new TheMessageAddress(owner),
                Hidden = hidden,
                ReadOnly = false,
                EngineName = engineName,
                DeviceType = deviceType,
                Address = address,
                InstanceId = instanceId,
                FriendlyName = friendlyName,
                Properties = properties,
            };
            return CreateOwnedThingAsync(createParams, timeout);
        }

        public static Task<TheThing> CreateOwnedThingAsync(MsgCreateThingRequestV1 createParams)
        {
            return CreateOwnedThingAsync(createParams, createDefaultTimeout);
        }
        public static Task<TheThing> CreateOwnedThingAsync(MsgCreateThingRequestV1 createParams, TimeSpan timeout)
        {
            var target = new TheMessageAddress { EngineName = createParams.EngineName };
            return CreateOwnedThingAsync(createParams, target, timeout);
        }

        public static Task<TheThing> CreateOwnedThingAsync(MsgCreateThingRequestV1 createParams, TheMessageAddress target)
        {
            return CreateOwnedThingAsync(createParams, target, createDefaultTimeout);
        }
        public static Task<TheThing> CreateOwnedThingAsync(MsgCreateThingRequestV1 createParams, TheMessageAddress target, TimeSpan timeout)
        {
            return
                 CreateOwnedThingForAddressAsync(createParams, target, timeout)
                 .ContinueWith((responseTask) =>
                 {
                     if (responseTask == null)
                     {
                         return TheCommonUtils.TaskFromResult<TheThing>(null);
                     }

                     var response = responseTask.Result;
                     if (response == null)
                     {
                         return TheCommonUtils.TaskFromResult<TheThing>(null);
                     }

                     return GetOwnedThingAsync(response, timeout);
                 }).ContinueWith((t) => t?.Unwrap()?.Result);
        }

        public static Task<TheMessageAddress> CreateOwnedThingForAddressAsync(MsgCreateThingRequestV1 createParams, TheMessageAddress target, TimeSpan timeout)
        {
            return
                 TheCommRequestResponse.PublishRequestJSonAsync<MsgCreateThingRequestV1, MsgCreateThingResponseV1>(createParams.OwnerAddress, target, createParams, timeout)
                 .ContinueWith((responseTask) =>
                 {
                     if (responseTask == null)
                     {
                         return null;
                     }

                     var response = responseTask.Result;
                     if (response == null)
                     {
                         return null;
                     }

                     return response.ThingAddress;
                 });
        }
        static readonly object createOwnedThingLock = new ();
        internal static Task<TheThing> CreateOwnedThingLocalAsync(MsgCreateThingRequestV1 createParams, TimeSpan timeout)
        {
            if (createParams == null)
            {
                return Task.FromResult<TheThing>(null);
            }
            lock (createOwnedThingLock)
            {
                TheThing ownedThing = null;
                if (createParams.ThingMID != null)
                {
                    ownedThing = TheThingRegistry.GetThingByMID(createParams.ThingMID.Value, true);
                }
                if (ownedThing == null)
                {
                    List<TheThing> ownedThings;
                    if (createParams.OwnerAddress != null)
                    {
                        ownedThings = GetThingsByProperty(createParams.EngineName, eOwnerProperties.cdeOwner, TheCommonUtils.cdeGuidToString(createParams.OwnerAddress.ThingMID), true);
                    }
                    else
                    {
                        ownedThings = GetThingsOfEngine(createParams.EngineName, true);
                    }

                    if (!string.IsNullOrEmpty(createParams.DeviceType))
                    {
                        ownedThings = ownedThings.Where(t => t.DeviceType == createParams.DeviceType).ToList();
                    }

                    if (!string.IsNullOrEmpty(createParams.InstanceId))
                    {
                        ownedThing = ownedThings.FirstOrDefault(t => TheThing.GetSafePropertyString(t, eOwnerProperties.cdeOwnerInstance) == createParams.InstanceId);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(createParams.ID))
                        {
                            ownedThing = ownedThings.FirstOrDefault(t => t.ID == createParams.ID);
                        }
                        else
                        {
                            ownedThing ??= ownedThings.FirstOrDefault(t => t.Address == createParams.Address || (string.IsNullOrEmpty(t.Address) && string.IsNullOrEmpty(createParams.Address)));
                            if (ownedThing == null)
                            {
                                var matchingThings = GetThingsOfEngine(createParams.EngineName, true, true)
                                    .Where(t => t.DeviceType == createParams.DeviceType || string.IsNullOrEmpty(createParams.DeviceType));
                                if (matchingThings.Count() == 1)
                                {
                                    ownedThing = matchingThings.First();
                                }
                            }
                        }
                    }
                }

                if (ownedThing == null && createParams.DeviceType == eKnownDeviceTypes.IBaseEngine)
                {
                    ownedThing = TheThingRegistry.GetBaseEngineAsThing(createParams.EngineName);
                }

                if (ownedThing != null)
                {
                    if (createParams.DoNotModifyIfExists != true)
                    {
                        SetOwnedThingProperties(ownedThing, createParams);
                    }
                    return WaitForInitializeAsync(ownedThing, timeout);
                }

                if (!createParams.CreateIfNotExist || createParams.DeviceType == eKnownDeviceTypes.IBaseEngine)
                {
                    return TheCommonUtils.TaskFromResult<TheThing>(null);
                }

                TheThing tThing = new ()
                {
                    DeviceType = createParams.DeviceType,
                    EngineName = createParams.EngineName,
                };
                if (createParams.ThingMID != null)
                {
                    tThing.cdeMID = createParams.ThingMID.Value;
                }
                SetOwnedThingProperties(tThing, createParams);

                var tRegisteredThing = RegisterThing(tThing);
                if (tRegisteredThing != tThing)
                {
                    // A thing with the same cdeMID already existed
                    return TheCommonUtils.TaskFromResult<TheThing>(null);
                }
                return WaitForInitializeAsync(tThing, timeout);
            }
        }
        private static void SetOwnedThingProperties(TheThing tThing, MsgCreateThingRequestV1 createParams)
        {
            tThing.Address = createParams.Address;

            if (!string.IsNullOrEmpty(createParams.ID))
            {
                tThing.ID = createParams.ID;
            }

            if (createParams.OwnerAddress != null)
            {
                TheThing.SetSafePropertyString(tThing, eOwnerProperties.cdeOwner, TheCommonUtils.cdeGuidToString(createParams.OwnerAddress.ThingMID));
            }
            if (!String.IsNullOrEmpty(createParams.InstanceId))
            {
                TheThing.SetSafePropertyString(tThing, eOwnerProperties.cdeOwnerInstance, createParams.InstanceId);
            }
            else
            {
                tThing.RemoveProperty(eOwnerProperties.cdeOwnerInstance);
            }
            if (createParams.Hidden)
            {
                TheThing.SetSafePropertyBool(tThing, eOwnerProperties.cdeHidden, createParams.Hidden);
            }
            else
            {
                tThing.RemoveProperty(eOwnerProperties.cdeHidden);
            }
            if (createParams.ReadOnly)
            {
                TheThing.SetSafePropertyBool(tThing, eOwnerProperties.cdeReadOnly, createParams.ReadOnly);
            }
            else
            {
                tThing.RemoveProperty(eOwnerProperties.cdeReadOnly);
            }
            if (createParams.Properties != null)
            {
                tThing.SetProperties(createParams.Properties, DateTimeOffset.Now);
            }
            if (createParams.cdeProperties != null)
            {
                foreach (var cdeProp in createParams.cdeProperties)
                {
                    if (cdeProp.Name != nameof(createParams.ID) || string.IsNullOrEmpty(createParams.ID))
                    {
                        tThing.SetProperty(cdeProp.Name, cdeProp.Value, (ePropertyTypes)cdeProp.cdeT, DateTimeOffset.Now);
                    }
                }
            }

            if (!string.IsNullOrEmpty(createParams.FriendlyName))
            {
                tThing.FriendlyName = createParams.FriendlyName;
            }
            else
            {
                if (string.IsNullOrEmpty(tThing.FriendlyName))
                {
                    tThing.FriendlyName = tThing.Address;
                }
                if (string.IsNullOrEmpty(tThing.FriendlyName) && createParams.OwnerAddress != null)
                {
                    tThing.FriendlyName = createParams.OwnerAddress.ToString();
                }
            }

        }


        public static Task<TheThing> WaitForInitializeAsync(TheMessageAddress thingAddress)
        {
            return WaitForInitializeAsync(GetThingByMID(thingAddress.ThingMID));
        }
        public static Task<TheThing> WaitForInitializeAsync(TheThing tThing)
        {
            return WaitForInitializeAsync(tThing, createDefaultTimeout);
        }

        public static Task<TheThing> WaitForInitializeAsync(TheThing tThing, TimeSpan timeout)
        {
            if (tThing == null)
            {
                return TheCommonUtils.TaskFromResult<TheThing>(null);
            }

            TheThing thingToReturn = tThing;
            if (!tThing.IsInit())
            {
                var tcsInitialized = new TaskCompletionSource<TheThing>();
                var initializeCallback = new Action<ICDEThing, object>((thing, param) =>
                {
                    var created = TheCommonUtils.CBool(param);
                    if (created)
                    {
                        tcsInitialized.TrySetResult(tThing);
                    }
                    else
                    {
                        TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("Thing Registry", $"Thing not initialized in WaitForInitializeAsync: timeout or failure from thing.", eMsgLevel.l2_Warning, $"{tThing}"), false);
                        tcsInitialized.TrySetResult(null);
                    }
                });
                tThing.RegisterEvent(eThingEvents.Initialized, initializeCallback);
                if (!tThing.IsInit())
                {
                    TheCommonUtils.TaskWaitTimeout(tcsInitialized.Task, timeout).ContinueWith((t) =>
                    {
                        tThing.UnregisterEvent(eThingEvents.Initialized, initializeCallback);
                        bool bInit = tThing.IsInit();
                        if (bInit)
                        {
                            if (!tcsInitialized.Task.IsCompleted)
                            {
                                TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("Thing Registry", $"Thing found initialized during timeout of WaitForInitializeAsync. No Initialized event fired or received.", eMsgLevel.l2_Warning, $"{tThing}"), false);
                            }
                        }
                        else
                        {
                            TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("Thing Registry", $"Thing not initialized in timeout of WaitForInitializeAsync.", eMsgLevel.l2_Warning, $"{tThing}"), false);
                        }
                        initializeCallback(tThing, bInit);
                    });
                }
                else
                {
                    tThing.UnregisterEvent(eThingEvents.Initialized, initializeCallback);
                    tcsInitialized.TrySetResult(tThing);
                }
                return tcsInitialized.Task;
            }
            return TheCommonUtils.TaskFromResult(thingToReturn);
        }

        public static Task<bool> DeleteOwnedThingAsync(TheMessageAddress thingAddress)
        {
            var tThing = GetThingByMID(thingAddress.ThingMID);
            return TheCommonUtils.TaskFromResult<bool>(TheThingRegistry.DeleteThing(tThing));
        }



        /// <summary>
        /// Checks if TheThingRegistry has at least one TheThing that fits the pSelector function
        /// </summary>
        /// <param name="pEngineName">Engine that owns TheThings to look for</param>
        /// <param name="pSelector">Selector for the function</param>
        /// <returns></returns>
        public static bool HasThingsWithFunc(string pEngineName, Func<TheThing, bool> pSelector)
        {
            List<TheThing> tList = GetThingsOfEngine(pEngineName);
            if (tList == null) return false;
            return tList.Any(pSelector);
        }

        /// <summary>
        /// Returns all IBaseEngines known in TheThingRegistry
        /// </summary>
        /// <param name="AlivesOnly">If set to true, only running (instantiated) and alive engines are returned</param>
        /// <returns></returns>
        internal static List<string> GetBaseEngineNamesWithJSEngine(bool AlivesOnly)
        {
            List<string> resList = new ();
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null) return resList;

            List<IBaseEngine> tList = GetBaseEngines(AlivesOnly);
            if (tList != null && tList.Count > 0)
            {
                foreach (var t in tList)
                {
                    if (t.GetBaseThing().GetObject() is ICDEPlugin tPlug && !resList.Contains(t.GetEngineName()) && t.GetEngineState().HasJSEngine && !(AlivesOnly && tPlug.GetBaseEngine()?.GetEngineState()?.IsUnloaded == true))
                        resList.Add(t.GetEngineName());
                }
            }
            return resList;
        }

        /// <summary>
        /// Returns all IBaseEngines known in TheThingRegistry
        /// </summary>
        /// <param name="AlivesOnly">If set to true, only running (instantiated) and alive engines are returned</param>
        /// <returns></returns>
        public static List<IBaseEngine> GetBaseEngines(bool AlivesOnly)
        {
            List<IBaseEngine> resList = new ();
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null) return resList;

            List<TheThing> tList = GetBaseEnginesAsThing(AlivesOnly);
            if (tList != null && tList.Count > 0)
            {
                foreach (TheThing t in tList)
                {
                    if (t.GetObject() is ICDEPlugin tPlug && !resList.Contains(tPlug.GetBaseEngine()) && !(AlivesOnly && tPlug.GetBaseEngine()?.GetEngineState()?.IsUnloaded == true))
                        resList.Add(tPlug.GetBaseEngine());
                }
            }
            return resList;
        }

        /// <summary>
        /// Gets a base engine by a Capability
        /// </summary>
        /// <param name="pCap">Capability to look for</param>
        /// <param name="IncludeRemote">if true, all engine even stopped and remote engines will be returned</param>
        /// <returns></returns>
        public static List<IBaseEngine> GetBaseEnginesByCap(eThingCaps pCap, bool IncludeRemote = false)
        {
            List<IBaseEngine> resList = new ();
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null) return resList;

            List<IBaseEngine> tList = GetBaseEngines(!IncludeRemote);
            if (tList != null && tList.Count > 0)
            {
                resList.AddRange(from IBaseEngine t in tList
                                 where t.HasCapability(pCap)
                                 select t);
            }
            return resList;
        }

        /// <summary>
        /// Returns all DeviceTypes of engine with the given capability
        /// </summary>
        /// <param name="pCap">Requested capability</param>
        /// <param name="IncludeRemote">if true, all engine even stopped and remote engines will be returned</param>
        /// <returns></returns>
        public static List<TheEngineDeviceTypeInfo> GetAllDeviceTypesByCap(eThingCaps pCap, bool IncludeRemote)
        {
            var engines = GetBaseEnginesByCap(pCap, IncludeRemote);
            var deviceTypes = new List<TheEngineDeviceTypeInfo>();
            foreach (var engine in engines)
            {
                deviceTypes.AddRange(engine.GetDeviceTypes().Where(dt => dt.Capabilities.Contains(pCap)).Select(dt => new TheEngineDeviceTypeInfo(dt, engine.GetEngineName())));
            }
            return deviceTypes;
        }

        /// <summary>
        /// Returns a list of all Engine Names known
        /// </summary>
        /// <param name="AlivesOnly">If set, only names of Engines that are running and alive will be returned</param>
        /// <returns></returns>
        public static List<string> GetEngineNames(bool AlivesOnly)
        {
            List<string> resList = new ();
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null) return resList;

            List<IBaseEngine> tList = GetBaseEngines(AlivesOnly);
            if (tList != null && tList.Count > 0)
            {
                foreach (var t in tList)
                {
                    if (t.GetBaseThing().GetObject() is ICDEPlugin tPlug && !resList.Contains(t.GetEngineName()) && !(AlivesOnly && tPlug.GetBaseEngine()?.GetEngineState()?.IsUnloaded == true))
                        resList.Add(t.GetEngineName());
                }
            }
            return resList;
        }

        /// <summary>
        /// Returns the IBaseEngine of TheThing
        /// After the first call the IBaseEngine is cached in TheThing and subsequent calls are faster
        /// Alternative you can call pThing.GetBaseEngine();
        /// </summary>
        /// <param name="pThing">TheThing that needs its IBaseEngine</param>
        public static IBaseEngine GetBaseEngine(TheThing pThing)
        {
            return GetBaseEngine(pThing, false);
        }

        /// <summary>
        /// Returns the IBaseEngine of TheThing
        /// This call is NOT cached if AllowRemoteEngine is true
        /// </summary>
        /// <param name="pThing">TheThing that needs its IBaseEngine</param>
        /// <param name="AllowRemoteEngine">If set to true, the seach includes engine not hosted on the local node</param>
        /// <returns></returns>
        public static IBaseEngine GetBaseEngine(TheThing pThing, bool AllowRemoteEngine)
        {
            if (pThing == null || string.IsNullOrEmpty(pThing.EngineName)) return null;
            if (!AllowRemoteEngine)
            {
                if (pThing.MyBaseEngine != null) return pThing.MyBaseEngine;
                return (pThing.MyBaseEngine = GetBaseEngine(pThing.EngineName, false));
            }
            return GetBaseEngine(pThing.EngineName, false);
        }


        /// <summary>
        /// Return the IBaseEngine interface for a given EngineName.
        /// This call is quite expensive and the result should be cached if possible
        /// </summary>
        /// <param name="pEngineName">Name of the Engine to be found</param>
        /// <returns></returns>
        public static IBaseEngine GetBaseEngine(string pEngineName)
        {
            return GetBaseEngine(pEngineName, false);
        }

        /// <summary>
        /// Return the IBaseEngine interface for a given EngineName.
        /// This call is quite expensive and the result should be cached if possible
        /// </summary>
        /// <param name="pEngineName">Name of the Engine to be found</param>
        /// <param name="AllowRemoteEngine">If set to true, the seach includes engine not hosted on the local node</param>
        /// <returns></returns>
        public static IBaseEngine GetBaseEngine(string pEngineName, bool AllowRemoteEngine)
        {
            if (string.IsNullOrEmpty(pEngineName)) return null;
            TheThing tThing = GetBaseEngineAsThing(pEngineName, AllowRemoteEngine);
            if (tThing?.GetObject() is ICDEPlugin tPLug)
                return tPLug.GetBaseEngine();
            return null;
        }

        /// <summary>
        /// Return true of the engine is registered in TheThingRegistry. Attention: it also returns true if the Engine is registered but is currently NOT running! For example if the Plugin was running at some time but removed at a later time.
        /// Use "IsEngineAlive"
        /// </summary>
        /// <param name="pEngineName"></param>
        /// <returns></returns>
        public static bool IsEngineRegistered(string pEngineName)
        {
            return IsEngineRegistered(pEngineName, false);
        }


        /// <summary>
        /// Checks if a given EngineName is registered in TheThingRegistry
        /// </summary>
        /// <param name="pEngineName">EngineName of the engine to be found</param>
        /// <param name="AllowRemoteEngine">Is set, the result include remote engines on other nodes</param>
        /// <returns></returns>
        public static bool IsEngineRegistered(string pEngineName, bool AllowRemoteEngine)
        {
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null || string.IsNullOrEmpty(pEngineName) ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null) return false;
            if (GetBaseEngineAsThing(pEngineName, AllowRemoteEngine) != null) return true;
            return false;
        }

        internal static string GetEngineNameByMID(Guid pMid)
        {
            var tOwnerThing = TheThingRegistry.GetThingByMID(pMid);
            return tOwnerThing?.EngineName;
        }

        /// <summary>
        /// Checks if a given EngineName is registered in TheThingRegistry and
        /// Started (InitEngineAssets() was completed and Engine HasLiveObject==true)
        /// </summary>
        /// <param name="pEngineName">EngineName of the engine to be found.</param>
        /// <param name="AllowRemoteEngine">If set, results include engines on other nodes.</param>
        /// <returns></returns>
        public static bool IsEngineStarted(string pEngineName, bool AllowRemoteEngine)
        {
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null || string.IsNullOrEmpty(pEngineName) ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null) return false;
            IBaseEngine tThing = GetBaseEngine(pEngineName, AllowRemoteEngine);
            if (tThing == null) return false;
            if (tThing.GetBaseThing().GetObject() != null)
            {
                if (tThing.GetEngineState().IsUnloaded || tThing.GetEngineState().IsDisabled)
                    return false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if a given EngineName is registered in TheThingRegistry and
        /// Started (SetInitialized() was called by the engine).
        /// </summary>
        /// <param name="pEngineName">EngineName of the engine to be found.</param>
        /// <param name="AllowRemoteEngine">If set, include engines on other nodes.</param>
        /// <returns></returns>
        public static bool IsEngineInitialized(string pEngineName, bool AllowRemoteEngine)
        {
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null || string.IsNullOrEmpty(pEngineName) ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null) return false;
            IBaseEngine tBase = GetBaseEngine(pEngineName, AllowRemoteEngine);
            if (tBase == null || tBase.GetEngineState() == null) return false;
            return tBase.GetEngineState().IsInitialized;
        }

        /// <summary>
        /// Return TheThing object of a given EngineName
        /// </summary>
        /// <param name="pEngineName"></param>
        /// <returns></returns>
        public static TheThing GetBaseEngineAsThing(string pEngineName)
        {
            return GetBaseEngineAsThing(pEngineName, false);
        }

        /// <summary>
        /// Returns TheThing of a given EngineName
        /// </summary>
        /// <param name="pEngineName">EngineName of the IBaseEngine to be found</param>
        /// <param name="AllowRemoteEngine">If set to true, the result includes remote instances of the Engine</param>
        /// <returns></returns>
        public static TheThing GetBaseEngineAsThing(string pEngineName, bool AllowRemoteEngine)
        {
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null || string.IsNullOrEmpty(pEngineName) ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null) return null;
            if (TheCDEngines.MyThingEngine.MyThingRegistry.mEngineCache.ContainsKey(pEngineName))
                return TheCDEngines.MyThingEngine.MyThingRegistry.mEngineCache[pEngineName];
            TheCDEKPIs.IncrementKPI(eKPINames.KPI6);
            TheThing tResEngine = null;
            tResEngine = TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.MyMirrorCache.GetEntryByFunc(s => (AllowRemoteEngine || s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo?.DeviceID) && eKnownDeviceTypes.IBaseEngine.Equals(TheThing.GetSafePropertyString(s, "DeviceType")) && pEngineName.Equals(TheThing.GetSafePropertyString(s, "EngineName"), StringComparison.OrdinalIgnoreCase));
            if (tResEngine != null)
                TheCDEngines.MyThingEngine.MyThingRegistry.mEngineCache[pEngineName] = tResEngine;
            return tResEngine;
        }

        /// <summary>
        /// Returns all Base Engines
        /// </summary>
        /// <param name="AlivesOnly">If True, only alive Engines are returned</param>
        /// <returns></returns>
        public static List<TheThing> GetBaseEnginesAsThing(bool AlivesOnly)
        {
            return GetBaseEnginesAsThing(AlivesOnly, false);
        }
        /// <summary>
        /// Returns all Base Engines
        /// </summary>
        /// <param name="AlivesOnly">If True, only alive Engines are returned</param>
        /// <param name="AllowRemoteEngine">If True, Engine on other nodes are returned as well</param>
        /// <returns></returns>
        public static List<TheThing> GetBaseEnginesAsThing(bool AlivesOnly, bool AllowRemoteEngine)
        {
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null) return new List<TheThing>();
            return TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.MyMirrorCache.GetEntriesByFunc(s => (AllowRemoteEngine || s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo?.DeviceID) && eKnownDeviceTypes.IBaseEngine.Equals(TheThing.GetSafePropertyString(s, "DeviceType")) && (!AlivesOnly || (s.GetObject() != null && !s.IsDisabled)));
        }

        /// <summary>
        /// Gets all Things of a given Engine
        /// </summary>
        /// <param name="pEngineName">Engine Name to get Things of.
        /// Wildcard "*" retrieves all Things of all local Engines.</param>
        /// <returns></returns>
        public static List<TheThing> GetThingsOfEngine(string pEngineName)
        {
            return GetThingsOfEngine(pEngineName, false, false);
        }

        /// <summary>
        /// Gets all Things of a given Engine
        /// </summary>
        /// <param name="pEngineName">Engine Name to get Things of... wildcard "*" retreives all Things of all Engines</param>
        /// <param name="AllowRemoteEngine">If True, the list includes TheThings on remote Nodes</param>
        /// <returns></returns>
        public static List<TheThing> GetThingsOfEngine(string pEngineName, bool AllowRemoteEngine)
        {
            return GetThingsOfEngine(pEngineName, false, AllowRemoteEngine);
        }

        /// <summary>
        /// Gets all Things of a given Engine
        /// </summary>
        /// <param name="pEngineName">Engine Name to get Things of... wildcard "*" retreives all Things of all Engines</param>
        /// <param name="IncludeEngine">Includes TheBaseEngine</param>
        /// <param name="AllowRemoteEngine">If True, the list includes TheThings on remote Nodes</param>
        /// <returns></returns>
        public static List<TheThing> GetThingsOfEngine(string pEngineName, bool IncludeEngine, bool AllowRemoteEngine)
        {
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null) return new List<TheThing>();

            if (string.IsNullOrEmpty(pEngineName) || "*".Equals(pEngineName))
                return TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.MyMirrorCache.GetEntriesByFunc(s => (AllowRemoteEngine || s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo?.DeviceID));
            else
                return TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.MyMirrorCache.GetEntriesByFunc(s => (AllowRemoteEngine || s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo?.DeviceID) && TheThing.GetSafePropertyString(s, "EngineName") == pEngineName && (IncludeEngine || !eKnownDeviceTypes.IBaseEngine.Equals(TheThing.GetSafePropertyString(s, "DeviceType"))));
        }

        /// <summary>
        /// Get things of an Engine with a given capability
        /// </summary>
        /// <param name="pEngineName">Engine of the things</param>
        /// <param name="capability">Capability to look for</param>
        /// <returns></returns>
        public static List<TheThing> GetThingsOfEngine(string pEngineName, eThingCaps capability)
        {
            var things = GetThingsOfEngine(pEngineName, true, true);
            return things.Where(t => t.Capabilities.Contains(capability)).ToList();
        }

        private static TheThing GetThingByProperty(string pEngineName, string pPropName, string pPropValue)
        {
            TheThing tThing = null;
            if (TheCDEngines.MyThingEngine != null)
                tThing = GetThingByFunc(pEngineName, s => s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo?.DeviceID && TheThing.GetSafePropertyString(s, pPropName) == pPropValue);
            return tThing;
        }

        /// <summary>
        /// Returns a TheThings matching the PropertyName/Value combination in a given Engine
        /// </summary>
        /// <param name="pEngineName">Engine owning TheThing</param>
        /// <param name="pUID">UserID with permission to retrieve TheThing</param>
        /// <param name="pPropName">Property Name to look for</param>
        /// <param name="pPropValue">Property Value to look for</param>
        /// <returns></returns>
        public static TheThing GetThingByProperty(string pEngineName, Guid pUID, string pPropName, string pPropValue)
        {
            return GetThingByProperty(pEngineName, pUID, pPropName, pPropValue, false);
        }

        /// <summary>
        /// Returns a TheThings matching the PropertyName/Value combination in a given Engine
        /// </summary>
        /// <param name="pEngineName">Engine owning TheThing</param>
        /// <param name="pUID">UserID with permission to retrieve TheThing</param>
        /// <param name="pPropName">Property Name to look for</param>
        /// <param name="pPropValue">Property Value to look for</param>
        /// <param name="AllowRemoteEngine">Includes TheThings from other nodes</param>
        /// <returns></returns>
        public static TheThing GetThingByProperty(string pEngineName, Guid pUID, string pPropName, string pPropValue, bool AllowRemoteEngine)
        {
            TheThing tThing = null;
            if (TheCDEngines.MyThingEngine != null)
                tThing = GetThingByFunc(pEngineName, s => (AllowRemoteEngine || s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo?.DeviceID) && TheThing.GetSafePropertyString(s, pPropName) == pPropValue && (s.UID == Guid.Empty || s.UID == pUID));
            return tThing;
        }

        /// <summary>
        /// Returns a list of TheThings matching the PropertyName/Value combination in a given Engine
        /// </summary>
        /// <param name="pEngineName">Engine owning TheThings</param>
        /// <param name="pPropName">Property Name to look for</param>
        /// <param name="pPropValue">Property Value to look for</param>
        /// <param name="AllowRemoteEngine">Includes TheThings from other nodes</param>
        /// <returns></returns>
        private static List<TheThing> GetThingsByProperty(string pEngineName, string pPropName, string pPropValue, bool AllowRemoteEngine)
        {
            List<TheThing> tThings = null;
            if (TheCDEngines.MyThingEngine != null)
            {
                tThings = GetThingsByFunc(pEngineName, s => (AllowRemoteEngine || s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo?.DeviceID) && TheThing.GetSafePropertyString(s, pPropName) == pPropValue);
            }
            else
                tThings = new List<TheThing>();
            return tThings;
        }
        /// <summary>
        /// Returns a list of TheThings matching the PropertyName/Value combination in a given Engine
        /// </summary>
        /// <param name="pEngineName">Engine owning TheThings</param>
        /// <param name="pUID">UserID with permission to retrieve TheThings</param>
        /// <param name="pPropName">Property Name to look for</param>
        /// <param name="pPropValue">Property Value to look for</param>
        /// <returns></returns>
        public static List<TheThing> GetThingsByProperty(string pEngineName, Guid pUID, string pPropName, string pPropValue)
        {
            return GetThingsByProperty(pEngineName, pUID, pPropName, pPropValue, false);
        }

        /// <summary>
        /// Returns a list of TheThings matching the PropertyName/Value combination in a given Engine
        /// </summary>
        /// <param name="pEngineName">Engine owning TheThings</param>
        /// <param name="pUID">UserID with permission to retrieve TheThings</param>
        /// <param name="pPropName">Property Name to look for</param>
        /// <param name="pPropValue">Property Value to look for</param>
        /// <param name="AllowRemoteEngine">Includes TheThings from other nodes</param>
        /// <returns></returns>
        public static List<TheThing> GetThingsByProperty(string pEngineName, Guid pUID, string pPropName, string pPropValue, bool AllowRemoteEngine)
        {
            List<TheThing> tThings = null;
            if (TheCDEngines.MyThingEngine != null)
            {
                tThings = GetThingsByFunc(pEngineName, s => (AllowRemoteEngine || s.cdeO == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo?.DeviceID) && TheThing.GetSafePropertyString(s, pPropName) == pPropValue && (s.UID == Guid.Empty || s.UID == pUID), AllowRemoteEngine);
            }
            else
                tThings = new List<TheThing>();
            return tThings;
        }

        /// <summary>
        /// Returs a list of TheThings matching the Selector
        /// </summary>
        /// <param name="pEngineName">Engine Name owning TheThings</param>
        /// <param name="pSelector">Selector function</param>
        /// <param name="AllowRemoteEngine">If True, the list includes TheThings on remote Nodes</param>
        /// <returns></returns>
        public static List<TheThing> GetThingsByFunc(string pEngineName, Func<TheThing, bool> pSelector, bool AllowRemoteEngine = false)
        {
            List<TheThing> tList = GetThingsOfEngine(pEngineName, AllowRemoteEngine);
            if (tList == null || tList.Count == 0) return tList;
            return tList.Where(pSelector).ToList();
        }

        /// <summary>
        /// Returns all Things that have a UATypeNodeId attribute set
        /// </summary>
        /// <param name="pEngineName">Engine Name owning TheThings</param>
        /// <param name="AllowRemoteEngine">If True, the list includes TheThings on remote Nodes</param>
        /// <returns></returns>
        public static List<TheThing> GetThingsWithUATypeNodeId(string pEngineName, bool AllowRemoteEngine = false)
        {
            List<TheThing> tList = GetThingsByFunc(pEngineName, s => s.GetProperty(nameof(OPCUATypeAttribute.UATypeNodeId)) != null, AllowRemoteEngine);
            return tList;
        }

        /// <summary>
        /// Returns all Things with a specific OPC UA Node Type ID
        /// </summary>
        /// <param name="pEngineName">Engine Name owning TheThings</param>
        /// <param name="pUATypeNodeId">nsu style OPC UA NodeID</param>
        /// <param name="AllowRemoteEngine">If True, the list includes TheThings on remote Nodes</param>
        /// <returns></returns>
        public static List<TheThing> GetThingsByUATypeNodeId(string pEngineName, string pUATypeNodeId, bool AllowRemoteEngine = false)
        {
            List<TheThing> tList = GetThingsByFunc(pEngineName, s => $"{s.GetProperty(nameof(OPCUATypeAttribute.UATypeNodeId))}" == pUATypeNodeId, AllowRemoteEngine);
            return tList;
        }


        /// <summary>
        /// Returs TheThings matching the Selector
        /// </summary>
        /// <param name="pEngineName">Engine Name owning TheThing</param>
        /// <param name="pSelector">Selector function</param>
        public static TheThing GetThingByFunc(string pEngineName, Predicate<TheThing> pSelector)
        {
            List<TheThing> tList = GetThingsOfEngine(pEngineName);
            if (tList == null) return null;
            return tList.Find(pSelector);
        }

        /// <summary>
        /// Return TheThing with the specified "ID" property
        /// </summary>
        /// <param name="pEngineName">Engine owning TheThing</param>
        /// <param name="pID">Value expected in the "ID" Property</param>
        /// <returns></returns>
        public static TheThing GetThingByID(string pEngineName, string pID)
        {
            return GetThingByID(pEngineName, pID, false);
        }

        /// <summary>
        /// Return TheThing with the specified "ID" property
        /// </summary>
        /// <param name="pEngineName">Engine owning TheThing</param>
        /// <param name="pID">Value expected in the "ID" Property</param>
        /// <param name="allowRemoteEngine">If True, considers TheThings on remote Nodes</param>
        /// <returns></returns>
        public static TheThing GetThingByID(string pEngineName, string pID, bool allowRemoteEngine)
        {
            if (string.IsNullOrEmpty(pID)) return null;
            string cacheKey = pEngineName + pID + allowRemoteEngine.ToString();
            if (_thingByIdCache?.TryGetValue(cacheKey, out var tCachedThing) == true)
            {
                if (tCachedThing == null || tCachedThing == TheThingRegistry.GetThingByMID(tCachedThing.cdeMID, allowRemoteEngine))
                {
                    return tCachedThing;
                }
                _thingByIdCache?.RemoveNoCare(cacheKey);
            }
            List<TheThing> tList = GetThingsOfEngine(pEngineName, allowRemoteEngine);
            var tThing = tList?.Find(s => TheThing.GetSafePropertyString(s, "ID") == pID);
            _thingByIdCache ??= new cdeConcurrentDictionary<string, TheThing>();
            _thingByIdCache?.TryAdd(cacheKey, tThing);
            return tThing;
        }
        static cdeConcurrentDictionary<string, TheThing> _thingByIdCache;

        /// <summary>
        /// Return TheThing with the specified unique Identifier cdeMID
        /// </summary>
        /// <param name="pID">cdeMID of TheThing to look for</param>
        /// <param name="AllowRemoteEngine">If True, the list includes TheThings on remote Nodes</param>
        /// <returns></returns>
        public static TheThing GetThingByMID(Guid pID, bool AllowRemoteEngine = false) // public since v4.0.11.0
        {
            if (TheCDEngines.MyThingEngine == null || TheCDEngines.MyThingEngine.MyThingRegistry == null ||
                TheCDEngines.MyThingEngine.MyThingRegistry.MyThings == null)
                return null;

            var tThing = TheCDEngines.MyThingEngine.MyThingRegistry.MyThings.MyMirrorCache.GetEntryByID(pID);
            if (!AllowRemoteEngine && tThing != null && tThing.cdeO != TheBaseAssets.MyServiceHostInfo.MyDeviceInfo?.DeviceID)
            {
                tThing = null;
            }
            return tThing;
        }

        /// <summary>
        /// Return TheThing with the specified unique Identifier cdeMID
        /// </summary>
        /// <param name="pEngineName">Engine owning TheThing</param>
        /// <param name="pID">cdeMID of TheThing to look for</param>
        /// <returns></returns>
        public static TheThing GetThingByMID(string pEngineName, Guid pID)
        {
            return GetThingByMID(pEngineName, pID, false, false);
        }

        /// <summary>
        /// Return TheThing with the specified unique Identifier cdeMID
        /// </summary>
        /// <param name="pEngineName">Engine owning TheThing</param>
        /// <param name="pID">cdeMID of TheThing to look for</param>
        /// <param name="AllowRemoteEngine">If True, the list includes TheThings on remote Nodes</param>
        /// <returns></returns>
        public static TheThing GetThingByMID(string pEngineName, Guid pID, bool AllowRemoteEngine)
        {
            return GetThingByMID(pEngineName, pID, false, AllowRemoteEngine);
        }

        /// <summary>
        /// Return TheThing with the specified unique Identifier cdeMID
        /// </summary>
        /// <param name="pEngineName">Engine owning TheThing</param>
        /// <param name="pID">cdeMID of TheThing to look for</param>
        /// <param name="IncludeEngine">Includes TheBaseEngine if the given pID is the cdeMID of TheBaseEngine</param>
        /// <param name="AllowRemoteEngine">If True, the list includes TheThings on remote Nodes</param>
        /// <returns></returns>
        internal static TheThing GetThingByMID(string pEngineName, Guid pID, bool IncludeEngine, bool AllowRemoteEngine)
        {
            if (pID == Guid.Empty) return null;
            List<TheThing> tList = GetThingsOfEngine(pEngineName, IncludeEngine, AllowRemoteEngine);
            if (tList == null) return null;
            return FindMID(tList, pID); 
        }

        /// <summary>
        /// Return TheLiveObject of TheThing in the Engine with the cdeMID given.
        /// </summary>
        /// <param name="pEngineName">Engine owning TheThing</param>
        /// <param name="pID">cdeMID of TheThing to look for</param>
        /// <returns></returns>
        public static object GetThingObjectByMID(string pEngineName, Guid pID)
        {
            if (pID == Guid.Empty) return null;
            List<TheThing> tList = GetThingsOfEngine(pEngineName);
            if (tList == null) return null;
            TheThing tThing = FindMID(tList, pID); 
            if (tThing == null) return null;
            return tThing.GetObject();
        }

        private static TheThing FindMID(List<TheThing> pList, Guid pID)
        {
            return pList.Find(s => s.cdeMID == pID);
        }

        /// <summary>
        /// Updates the NMI Model for all Things in the Given Engine by calling the "CreateUX()" function of all TheThings
        /// </summary>
        /// <param name="pEngineName">Engine Name for all TheThings to find</param>
        public static void UpdateEngineUX(string pEngineName)
        {
            List<TheThing> tDevList = GetThingsOfEngine(pEngineName);
            if (tDevList.Count > 0)
            {
                foreach (TheThing tDev in tDevList)
                    tDev.CreateUX();
            }
        }

        /// <summary>
        /// Short for tThing.RegisterEvent(eventname,callback)
        /// </summary>
        /// <param name="tThing">Thing to register the event on</param>
        /// <param name="pEventName">Name the event (eThingEvents)</param>
        /// <param name="pCallBack">a callback used for the Event</param>
        public static void RegisterEventOfThing(ICDEThing tThing, string pEventName, Action<ICDEThing, object> pCallBack)
        {
            if (tThing != null)
                tThing.RegisterEvent(pEventName, pCallBack);
        }
        /// <summary>
        /// Short for tThing.UnregisterEvent(eventname,callback)
        /// </summary>
        /// <param name="tThing">Thing to register the event on</param>
        /// <param name="pEventName">Name the event (eThingEvents)</param>
        /// <param name="pCallBack">a callback used for the Event</param>
        public static void UregisterEventOfThing(ICDEThing tThing, string pEventName, Action<ICDEThing, object> pCallBack)
        {
            if (tThing != null)
                tThing.UnregisterEvent(pEventName, pCallBack);
        }

        /// <summary>
        /// Helper Function that allows registering for a special event on all Things of a given DeviceType
        /// This allows for Node Wide registration of certain known events of special DeviceTypes
        /// </summary>
        /// <param name="pEngineName">Engine of the Devices to look for</param>
        /// <param name="pDeviceType">The Device type that will fire the event</param>
        /// <param name="pEventName">The Event Name</param>
        /// <param name="AllowRemoteThings">Include Things on other Nodes</param>
        /// <param name="pCallBack">The callback to be called when the event fires</param>
        public static void RegisterEventOfDeviceType(string pEngineName, string pDeviceType, string pEventName, bool AllowRemoteThings, Action<ICDEThing, object> pCallBack)
        {
            List<TheThing> tList = GetThingsByProperty(pEngineName, "DeviceType", pDeviceType, AllowRemoteThings);
            if (tList != null)
            {
                foreach (TheThing tThing in tList)
                {
                    tThing.RegisterEvent(pEventName, pCallBack);
                }
            }
        }

        /// <summary>
        /// Helper Function that allows to unregister a callback previously registered with RegisterEventOfDeviceType
        /// </summary>
        /// <param name="pEngineName">Engine of the Devices to look for</param>
        /// <param name="pDeviceType">The Device type that will fire the event</param>
        /// <param name="pEventName">The Event Name</param>
        /// <param name="AllowRemoteThings">Include Things on other Nodes</param>
        /// <param name="pCallBack">The callback to be called when the event fires</param>
        public static void UnregisterEventOfDeviceType(string pEngineName, string pDeviceType, string pEventName, bool AllowRemoteThings, Action<ICDEThing, object> pCallBack)
        {
            List<TheThing> tList = GetThingsByProperty(pEngineName, "DeviceType", pDeviceType, AllowRemoteThings);
            if (tList != null)
            {
                foreach (TheThing tThing in tList)
                {
                    tThing.UnregisterEvent(pEventName, pCallBack);
                }
            }
        }
        #endregion


        /// <summary>
        /// Direcly connects two properties of two things together
        /// Use "UnmapProperty" to remove an existing connection
        /// </summary>
        /// <param name="pSourceThing">cdeMID of the Thing with the source property</param>
        /// <param name="pSourcePropertyName">Name of the Source Property</param>
        /// <param name="pTargetThing">cdeMID of the Target Thing</param>
        /// <param name="pTargetPropertyName">Naqme of the Target property</param>
        /// <param name="pIsBiDirectional">If set to true, changes on both sides are replicated to the other side</param>
        /// <returns></returns>
        public static Guid PropertyMapper(Guid pSourceThing, string pSourcePropertyName, Guid pTargetThing, string pTargetPropertyName, bool pIsBiDirectional)
        {
            if (TheCDEngines.MyThingEngine.MyThingRegistry == null || TheCDEngines.MyThingEngine.MyThingRegistry.MyPropertyMaps == null)
                return Guid.Empty;
            TheThing tThing = GetThingByMID("*", pSourceThing, true);
            if (tThing != null)
            {
                cdeP tSourceProp = tThing.GetProperty(pSourcePropertyName, true); //New 5.143.2: Mapper will create the source property if it did not exist yet.
                if (tSourceProp != null)
                {
                    TheThing tTargetThing = GetThingByMID("*", pTargetThing, true);
                    if (tTargetThing != null)
                    {
                        cdeP tTargetProp = tTargetThing.GetProperty(pTargetPropertyName, true);
                        if (tTargetProp != null)
                        {
                            tTargetThing.SetProperty(tTargetProp.Name, tThing.GetProperty(tSourceProp.Name).GetValue());
                            ThePropertyMapperInfo tInfo = new ()
                            {
                                SourceThing = pSourceThing,
                                SourceProperty = pSourcePropertyName,
                                TargetThing = pTargetThing,
                                TargetProperty = pTargetPropertyName,
                                IsBidirectional = pIsBiDirectional,
                                LastUpdate = DateTimeOffset.Now
                            };
                            tInfo.SourceCallback = (prop) =>
                            {
                                if (prop.Value is byte[] || TheThing.GetSafePropertyString(tTargetThing, pTargetPropertyName) != TheCommonUtils.CStr(prop.Value))
                                {
                                    tTargetThing.SetProperty(pTargetPropertyName, prop.Value);
                                    tInfo.LastUpdate = DateTimeOffset.Now;
                                }
                            };
                            tSourceProp.RegisterEvent(eThingEvents.PropertyChanged, tInfo.SourceCallback);
                            if (TheThing.GetSafePropertyString(tTargetThing, pTargetPropertyName) != TheCommonUtils.CStr(tSourceProp.Value))
                                TheThing.SetSafePropertyString(tTargetThing, pTargetPropertyName, TheCommonUtils.CStr(tSourceProp.Value));
                            if (pIsBiDirectional)
                            {
                                tInfo.TargetCallback = (prop) =>
                                {
                                    if (prop.Value is byte[] || TheThing.GetSafePropertyString(tThing, pSourcePropertyName) != TheCommonUtils.CStr(prop.Value))
                                        tThing.SetProperty(pSourcePropertyName, prop.Value);
                                };
                                tTargetProp.RegisterEvent(eThingEvents.PropertyChanged, tInfo.TargetCallback);
                            }
                            TheCDEngines.MyThingEngine.MyThingRegistry.MyPropertyMaps.AddAnItem(tInfo);
                            return tInfo.cdeMID;
                        }
                    }
                }
            }
            return Guid.Empty;
        }
        /// <summary>
        /// Unregisters the event of a Property Mapper
        /// </summary>
        /// <param name="tMappingGuid">Guid of the map to be unregistered</param>
        /// <returns></returns>
        public static bool UnmapPropertyMapper(Guid tMappingGuid)
        {
            if (tMappingGuid == Guid.Empty)
                return false;

            ThePropertyMapperInfo tInfo = TheCDEngines.MyThingEngine.MyThingRegistry.MyPropertyMaps.GetEntryByID(tMappingGuid);
            if (tInfo == null) return false;
            TheThing tThing = GetThingByMID("*", tInfo.SourceThing);
            if (tThing != null)
            {
                cdeP tProp = tThing.GetProperty(tInfo.SourceProperty);
                if (tProp != null)
                    tProp.UnregisterEvent(eThingEvents.PropertyChanged, tInfo.SourceCallback);
            }
            if (tInfo.IsBidirectional)
            {
                tThing = GetThingByMID("*", tInfo.TargetThing);
                if (tThing != null)
                {
                    cdeP tProp = tThing.GetProperty(tInfo.TargetProperty);
                    if (tProp != null)
                        tProp.UnregisterEvent(eThingEvents.PropertyChanged, tInfo.TargetCallback);
                }

            }
            return true;
        }

        /// <summary>
        /// Returns the last Target Update of the Property Mapper
        /// </summary>
        /// <param name="tMappingGuid"></param>
        /// <returns></returns>
        public static DateTimeOffset GetPropertyMapperLastUpdate(Guid tMappingGuid)
        {
            if (tMappingGuid == Guid.Empty)
                return DateTimeOffset.MinValue;

            ThePropertyMapperInfo tInfo = TheCDEngines.MyThingEngine.MyThingRegistry.MyPropertyMaps.GetEntryByID(tMappingGuid);
            if (tInfo == null) return DateTimeOffset.MinValue;
            return tInfo.LastUpdate;
        }

        /// <summary>
        /// Converts fields and properties of a class into Thing Properties
        /// </summary>
        /// <param name="pBaseThing">Target Thing to create the properties for</param>
        /// <param name="MyValue">Class containing the fields and properties</param>
        /// <param name="pNamePrefix">Allows to add a prefix to all Thing Properties</param>
        /// <param name="pBaseProperty">If not null, all new properties will be sub-properties of this given property</param>
        public static void ClassPropertiesToThingProperties(TheThing pBaseThing, object MyValue, string pNamePrefix = "", cdeP pBaseProperty = null)
        {
            if (pBaseThing == null || MyValue == null) return;
            object orgValue = null;
            Type fType;
            List<PropertyInfo> PropInfoArray = MyValue.GetType().GetProperties().OrderBy(x => x.Name).ToList();
            foreach (PropertyInfo finfo in PropInfoArray)
            {
                if (finfo.Name.StartsWith("cde")) //our internal variables must not be put into the propertybag
                    continue;
                fType = finfo.PropertyType;
                try
                {
                    var nest = string.IsNullOrEmpty(pNamePrefix) ? "" : $"{pNamePrefix}";
                    orgValue = finfo.GetValue(MyValue, null);
                    if (fType == Type.GetType("System.DateTime") || fType == Type.GetType("System.DateTimeOffset"))
                    {
                        if (pBaseProperty != null)
                            cdeP.SetSafePropertyDate(pBaseProperty, pNamePrefix + finfo.Name, TheCommonUtils.CDate(orgValue));
                        else
                            TheThing.SetSafePropertyDate(pBaseThing, pNamePrefix + finfo.Name, TheCommonUtils.CDate(orgValue));
                    }
                    else if (fType == Type.GetType("System.Boolean"))
                    {
                        if (pBaseProperty != null)
                            cdeP.SetSafePropertyBool(pBaseProperty, pNamePrefix + finfo.Name, TheCommonUtils.CBool(orgValue));
                        else
                            TheThing.SetSafePropertyBool(pBaseThing, pNamePrefix + finfo.Name, TheCommonUtils.CBool(orgValue));
                    }
                    else if (IsNumeric(fType))
                    {
                        if (pBaseProperty != null)
                            cdeP.SetSafePropertyNumber(pBaseProperty, pNamePrefix + finfo.Name, TheCommonUtils.CDbl(orgValue));
                        else
                            TheThing.SetSafePropertyNumber(pBaseThing, pNamePrefix + finfo.Name, TheCommonUtils.CDbl(orgValue));
                    }
                    else if (fType == Type.GetType("System.String"))
                    {
                        if (pBaseProperty != null)
                            cdeP.SetSafePropertyString(pBaseProperty, pNamePrefix + finfo.Name, TheCommonUtils.CStr(orgValue));
                        else
                            TheThing.SetSafePropertyString(pBaseThing, pNamePrefix + finfo.Name, TheCommonUtils.CStr(orgValue));
                    }
                    else if (fType == Type.GetType("System.Byte[]") && orgValue != null)
                    {
                        if (pBaseProperty != null)
                            pBaseProperty.SetProperty(finfo.Name, pNamePrefix + orgValue, ePropertyTypes.TBinary);
                        else
                            pBaseThing.SetProperty(finfo.Name, pNamePrefix + orgValue, ePropertyTypes.TBinary);
                    }
                    else if (fType.Namespace == "System.Collections.Generic")
                    {
                        if (orgValue is IEnumerable collection)
                        {
                            int no = 0;
                            foreach (var item in collection)
                            {
                                ClassPropertiesToThingProperties(pBaseThing, item, pBaseProperty == null ? $"{nest}{finfo.Name}[{no}]_" : "", pBaseProperty);
                                no++;
                            }
                        }
                    }
                    else if (fType.Namespace != "System")
                    {
                        ClassPropertiesToThingProperties(pBaseThing, orgValue, pBaseProperty == null ? $"{nest}{finfo.Name}_" : "", pBaseProperty);
                    }
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(466, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("ThingRegistry", "ClassPropertiesToThingProperties", eMsgLevel.l1_Error, e.ToString()));
                }
            }
        }

        /// <summary>
        /// Checks if the given FieldInfo is a numeric type
        /// </summary>
        /// <param name="type">type of the field to check if numeric</param>
        /// <returns></returns>
        internal static bool IsNumeric(Type type)
        {
            if (type == null)
                return false;
            TypeCode typeCode = Type.GetTypeCode(type);

            return typeCode switch
            {
                TypeCode.Byte or TypeCode.Decimal or TypeCode.Double or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.SByte or TypeCode.Single or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 => true,
                _ => false,
            };
        }
    }
}
