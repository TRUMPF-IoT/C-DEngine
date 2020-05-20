// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using System;
using System.Collections.Generic;
using System.Linq;

namespace nsCDEngine.ViewModels
{
    /// <summary>
    /// Storage Representation of a TheThing. Can be used in history and log tables
    /// </summary>
    public class TheThingStore : TheMetaDataBase
    {
        /// <summary>
        /// Property Bag of all known properties
        /// </summary>
        // Can not use ConcurrentDictionary here because Newtonsoft does not properly deserialize.
        // It is really not needed for TheThingStore as is meant to be for static snapshots. TheThing is for dynamically changing entities
        public Dictionary<string, object> PB { get; set; }

        //public Guid OwnerThingMID { get; set; }   //CODE-REVIEW: If we cannot use cdeO for the "owning Thing cdeMID" we have to add this...does this break stuff???

        /// <summary>
        /// Constructor for serialization and deserialization
        /// </summary>
        public TheThingStore()
        {
            PB = new Dictionary<string, object>();
        }

        public TheThingStore CloneForThingSnapshot(TheThingStore baseItem, bool ResetBase, IEnumerable<string> propertiesToInclude, List<string> propertiesToExclude, bool forExternalConsumption)
        {
            return CloneForThingSnapshot(baseItem, ResetBase, propertiesToInclude, propertiesToExclude, forExternalConsumption, out _);
        }

        internal TheThingStore CloneForThingSnapshot(TheThingStore baseItem, bool ResetBase, IEnumerable<string> propertiesToInclude, List<string> propertiesToExclude, bool forExternalConsumption, out bool bUpdated)
        {
            TheThingStore tThing = new TheThingStore();
            if (ResetBase)
            {
                tThing.cdeCTIM = DateTimeOffset.Now;
                tThing.cdeMID = Guid.NewGuid();
            }
            else
            {
                tThing.cdeCTIM = cdeCTIM;
                tThing.cdeMID = cdeMID;
            }
            tThing.cdeAVA = cdeAVA;
            tThing.cdeA = cdeA;
            if (baseItem != null && baseItem.cdeEXP != 0 && cdeEXP == 0)
            {
                tThing.cdeEXP = baseItem.cdeEXP;
            }
            else
            {
                tThing.cdeEXP = cdeEXP;
            }
            tThing.cdePRI = cdePRI;
            tThing.cdeF = cdeF;
            tThing.cdeM = cdeM;
            tThing.cdeN = cdeN;
            tThing.cdeO = cdeO;
            tThing.cdeSEQ = cdeSEQ;

            if (propertiesToInclude == null)
            {
                propertiesToInclude = baseItem != null ? baseItem.PB.Keys.Union(PB.Keys) : PB.Keys;
            }

            if (propertiesToExclude != null && propertiesToExclude.Count > 0)
            {
                var tempList = new List<string>(propertiesToInclude);
                foreach (var prop in propertiesToExclude)
                {
                    tempList.Remove(prop);
                }
                propertiesToInclude = tempList;
            }

            bUpdated = false;

            foreach (string key in propertiesToInclude)
            {
                if (PB.ContainsKey(key))
                {
                    bUpdated = true;
                    tThing.PB[key] = PB[key];
                }
                else if (baseItem != null && baseItem.PB.ContainsKey(key))
                {
                    try
                    {
                        tThing.PB[key] = baseItem.PB[key];
                    }
                    catch (Exception)
                    {
                        //ignored
                    }
                }
            }

            if (forExternalConsumption)
            {
                tThing.IsFullSnapshot = false;
                tThing.IsInitialValue = false;
            }
            else
            {
                if (IsFullSnapshot || (baseItem != null && baseItem.IsFullSnapshot))
                {
                    tThing.IsFullSnapshot = true;
                }
            }

            if (!bUpdated && baseItem != null)
            {
                return null; // all properties came from the baseItem, which means the thing update didn't have any matching property changes
            }
            return tThing;
        }

        /// <summary>
        /// Clones all public properties and returns a TheThingStore class
        /// </summary>
        /// <param name="iThing">The Thing to clone.</param>
        /// <param name="ResetBase">Use current time instead of Thing timestamp</param>
        /// <returns></returns>
        public static TheThingStore CloneFromTheThing(ICDEThing iThing, bool ResetBase)
        {
            return CloneFromTheThingInternal(iThing, ResetBase, false, true);
        }

        //internal static TheThingStore CloneFromTheThing(ICDEThing iThing, bool ResetBase, bool cloneOnlyChangedPropertiesForHistorian)
        //{
        //    return CloneFromTheThing(iThing, ResetBase, cloneOnlyChangedPropertiesForHistorian);
        //}

        /// <summary>
        /// Clones all public properties and returns a TheThingStore class
        /// </summary>
        /// <param name="iThing">The Thing to clone.</param>
        /// <param name="ResetBase">Use current time instead of Thing timestamp</param>
        /// <param name="bUsePropertyTimestamp"></param>
        /// <param name="cloneProperties"></param>
        /// <param name="propertiesToInclude"></param>
        /// <param name="propertiesToExclude"></param>
        /// <returns></returns>
        public static TheThingStore CloneFromTheThing(ICDEThing iThing, bool ResetBase, bool bUsePropertyTimestamp, bool cloneProperties, IEnumerable<string> propertiesToInclude = null, IEnumerable<string> propertiesToExclude = null)
        {
            var tStore = CloneFromTheThingInternal(iThing, ResetBase, bUsePropertyTimestamp, cloneProperties, propertiesToInclude, propertiesToExclude);
            tStore.IsFullSnapshot = false;
            return tStore;
        }


        /// <summary>
        /// Clones all public properties and returns a TheThingStore class
        /// </summary>
        /// <param name="iThing">The Thing to clone.</param>
        /// <param name="ResetBase">Use current time instead of Thing timestamp</param>
        /// <param name="bUsePropertyTimestamp"></param>
        /// <param name="cloneProperties"></param>
        /// <param name="propertiesToInclude"></param>
        /// <param name="propertiesToExclude"></param>
        /// <returns></returns>
        internal static TheThingStore CloneFromTheThingInternal(ICDEThing iThing, bool ResetBase, bool bUsePropertyTimestamp, bool cloneProperties, IEnumerable<string> propertiesToInclude = null, IEnumerable<string> propertiesToExclude = null)
        {
            TheThingStore tThing = new TheThingStore();
            if (iThing == null) return tThing;
            TheThing pThing = iThing.GetBaseThing();
            if (pThing != null)
            {
                if (ResetBase)
                {
                    tThing.cdeCTIM = !bUsePropertyTimestamp ? DateTimeOffset.Now : DateTimeOffset.MinValue;
                    tThing.cdeMID = Guid.NewGuid();
                }
                else
                {
                    tThing.cdeCTIM = pThing.cdeCTIM;
                    tThing.cdeMID = pThing.cdeMID;
                }
                tThing.cdeAVA = pThing.cdeAVA;
                tThing.cdeA = pThing.cdeA;
                tThing.cdeEXP = pThing.cdeEXP;
                tThing.cdePRI = pThing.cdePRI;
                tThing.cdeF = pThing.cdeF;
                tThing.cdeM = pThing.cdeM;
                tThing.cdeN = pThing.cdeN;
                //tThing.cdeO = pThing.cdeO;
                tThing.cdeO = pThing.cdeMID;    //CODE-REVIEW: Since tThing is a "TheThingStore" and is "owned" by the pThing, the cdeO (Owner) property should be updated. Does this break anything for TDS01 etc???
                //tThing.OwnerThingMID = pThing.cdeMID; //Alternative we need to add a OwnerThingMID to TheThingStore to be able to segregate retrieval of TheThingStore records from the SQL Server
                tThing.cdeSEQ = pThing.cdeSEQ;

                if (cloneProperties)
                {
                    propertiesToInclude = pThing.GetMatchingProperties(propertiesToInclude, propertiesToExclude);

                    int propsIncludeCount = 0;
                    foreach (string key in propertiesToInclude)
                    {
                        try
                        {
                            cdeP prop;
                            if ((prop = pThing.GetProperty(key)) != null) // .MyPropertyBag.TryGetValue(key, out prop))
                            {
                                //if (prop.CheckAndResetTouchedSinceLastHistorySnapshot() || cloneProperties) // Have to always check and reset, so do this first
                                {
                                    tThing.PB[key] = prop.Value;
                                    if (bUsePropertyTimestamp)
                                    {
                                        if (prop.cdeCTIM > tThing.cdeCTIM)
                                        {
                                            if (tThing.cdeCTIM.Ticks != 0 && tThing.PB.Count > 1)
                                            {
                                                TheBaseAssets.MySYSLOG.WriteToLog(1, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ThingService, "Historian timestamp changed by property when cloning for snapshot", eMsgLevel.l6_Debug, $"{prop.Name}: {tThing.cdeCTIM:O} changed to {prop.cdeCTIM:O}. Possible victims: {tThing.PB.Aggregate("", (s, kv) => kv.Key != prop.Name ? $"{s}{kv.Key}," : s)}"));
                                            }
                                            tThing.cdeCTIM = prop.cdeCTIM;
                                        }
                                        else if (prop.cdeCTIM != tThing.cdeCTIM)
                                        {
                                            TheBaseAssets.MySYSLOG.WriteToLog(1, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ThingService, "Historian timestamp on property not applied when cloning for snapshot", eMsgLevel.l6_Debug, $"{prop.Name}: {prop.cdeCTIM:O} changed to {tThing.cdeCTIM:O}. Possible offenders: {tThing.PB.Aggregate("", (s, kv) => kv.Key != prop.Name ? $"{s}{kv.Key}," : s)}"));
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            //ignored
                        }
                        propsIncludeCount++;
                    }

                    if (tThing.cdeCTIM == DateTimeOffset.MinValue)
                    {
                        tThing.cdeCTIM = pThing.cdeCTIM;
                    }

                    // We have all properties that are available in the thing as of this time and that are requested: this snapshot can and will now be used as the basis to report unchanged properties
                    tThing.IsFullSnapshot = true;
                }
            }
            return tThing;
        }
        private const string completeSnapshotPropertyName = "_____CompleteSnapshot";
        private const string initialValueFlagPropertyName = "_____InitialValue";
        internal bool IsFullSnapshot
        {
            get { return PB.ContainsKey(completeSnapshotPropertyName); }
            private set
            {
                if (value)
                {
                    PB[completeSnapshotPropertyName] = true;
                }
                else
                {
                    try
                    {
                        if (PB.ContainsKey(completeSnapshotPropertyName))
                        {
                            PB.Remove(completeSnapshotPropertyName);
                        }
                    }
                    catch
                    {
                        //ignored
                    }
                }
            }
        }
        internal bool IsInitialValue
        {
            get { return PB.ContainsKey(initialValueFlagPropertyName); }
            set
            {
                if (value)
                {
                    PB[initialValueFlagPropertyName] = true;
                }
                else
                {
                    try
                    {
                        if (PB.ContainsKey(initialValueFlagPropertyName))
                        {
                            PB.Remove(initialValueFlagPropertyName);
                        }
                    }
                    catch
                    {
                        //ignored
                    }
                }
            }
        }
        /// <summary>
        /// Optional TheThing that provides meta data about the thing to which this update corresponds: full set of properties and their types etc.
        /// Used commonly when replicating thing updates across nodes to ensure proper type information
        /// </summary>
        public TheThing ThingMetaData { get; set; }
    }


}
