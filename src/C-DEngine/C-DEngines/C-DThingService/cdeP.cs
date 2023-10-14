// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace nsCDEngine.Engines.ThingService
{
    /// <summary>
    /// Avalable Property Types for C-DEngine Things
    /// </summary>
    public enum ePropertyTypes 
    {
        /// <summary>
        /// The default property Type is string
        /// </summary>
        TString = 0,
        /// <summary>
        /// This property manages a number. Can be a float, number, double or int/long
        /// </summary>
        TNumber = 1,
        /// <summary>
        /// This property manages a boolan. I can be TRUE,FALSE and NULL!
        /// </summary>
        TBoolean = 2,
        // CODE REVIEW: Do we want to introduce a DateTimeOffset type here as well? Right now we are using DateTimeOffset for everything, which is a safe route to go (it's a superset of DateTime) CM: Lets ONLY manage DateTimeOffset - no more just DateTime
        /// <summary>
        /// This property manages a DateTimeOffset
        /// </summary>
        TDate = 3,
        /// <summary>
        /// This property is a byte []
        /// </summary>
        TBinary = 4,
        /// <summary>
        /// TBD: This property type will hold a javascript function that can be interpreted with eval ()
        /// </summary>
        TFunction = 5,
        /// <summary>
        /// GUID formatted string
        /// </summary>
        TGuid = 6,
        /// <summary>
        /// All other types not specifically set
        /// </summary>
        NOCHANGE = 99
    }

    /// <summary>
    /// this is the Smart Property Class of C-DEngine managed Things.
    /// Each Thing can have zero, one ore more cdeP Properties.
    /// They are normally stored in a "PropertyBag" that can be accessed via the plugins or in JavaScript on the Browser side
    /// </summary>
    public partial class cdeP : TheMetaDataBase, ICDEProperty 
    {
        /// <summary>
        /// Fire On Change Bits
        /// Bit 1(1)=PublishCentral "eThingEvents.PropertyChanged" Event to all Clients or Originator if provided
        /// Bit 2(2)=Publish To Service Only (If BaseThing is managed on a different Node)
        /// Bit 3(4)=Fire "eThingEvents.PropertyChanged" Event to local plugins
        /// Bit 4(8)=iValue was set - dont send update to other nodes just fire OnChange locally. This flag is only valid for one SetProperty Operation
        /// Bit 5(16)=Dont Fire now and dont set value. Use this to set Type and events. This flag is only valid for one SetProperty Operation
        /// Bit 6(32)=Always fire "eThingEvents.PropertySet" and Publishing event. This flag is only valid for one SetProperty Operation
        /// Bit 7(64)=Property is NMI related
        /// Bit 8(128)=Property is READ ONLY (not active in V3)
        /// </summary>
        internal int cdeFOC { get; private set; }

        /// <summary>
        /// Extended Property Flags
        /// Bit 1(1) If set to TRUE, all storage and communication for this property has to be encrypted
        /// Bit 2(2) If Set to TRUE, the Property Update will be sent with Queue Prio 1 (Fastest Sending using FILO)
        /// Bit 3(4) If set to TRUE, the CTIM of the Property will be automatically set to DateTimeOffset.Now if the property was changed.
        /// Bit 4(8) If set to TRUE, the property will ALWAYS update - no mater if the old value= new value ("forceWrite")
        /// Bit 5(16) if Set to TRUE, the property will NOT be sent via initial NMI_SET_DATA to browsers, only updates are sent
        /// Bit 7(64) Property is NMI related
        /// Bit 8(128) Property change does not cause ThingRegistry to be written to disk (for High speed/real-time properties)
        /// Bit 9(256) If set, property events are fired synchronously (default is async)
        /// </summary>
        public int cdeE { get; set; }

        #region Updater
        /// <summary>
        /// Sets an Updater Guid for the Property. The updater can check if an OnPropertyChange was fired by itself
        /// </summary>
        /// <param name="tUpd">Guid of the updater</param>
        public void SetUpdater(Guid tUpd)
        {
            cdeUpdater = tUpd;
        }
        private Guid cdeUpdater;

        /// <summary>
        /// Returns the Updater Guid
        /// </summary>
        /// <returns></returns>
        public Guid GetUpdater()
        {
            return cdeUpdater;
        }
        /// <summary>
        /// Resets the Updater
        /// </summary>
        public void ResetUpdater()
        {
            cdeUpdater = Guid.Empty;
        }
        #endregion

        /// <summary>
        /// Name of the property
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Returns true if theproperty has changed during the last SetProperty. If false, the last SetProperty tried to write the same value
        /// </summary>
        [IgnoreDataMember]
        public bool HasChanged
        {
            get { return changedMask != 0; }
        }
        private object mOldValue;
        /// <summary>
        /// Contains the value that was set before an update
        /// </summary>
        [IgnoreDataMember]
        public object OldValue { get { return mOldValue; } }
        private object mValue;

        /// <summary>
        /// Set the PublishCentral bit on the cdeFOC. The requestor is the node requesting the PC
        /// </summary>
        /// <param name="bOnOff">if true the property will fire OnChange publications</param>
        /// <param name="pRequestor">Guid of the requestor (Future use)</param>
        /// <returns>Returns true if the requestor could be registered</returns>
        public bool SetPublication(bool bOnOff, Guid pRequestor)
        {
            if (bOnOff)
            {
                cdeFOC |= 1;
            }
            else
            {
                cdeFOC &= 0xfffe;
            }
            return false;
        }

        internal void RemoveTempFlags()
        {
            cdeFOC &= 0xFFC7; //Removes the iOnlyFlag and ForceFlag
        }

        internal void SetEventAction(int pAction)
        {
            cdeFOC |= pAction;
        }

        /// <summary>
        /// Type of Property
        /// 0=String
        /// 1=Number
        /// 2=boolean
        /// 3=Date
        /// 4=Binary
        /// 5=Function
        /// 6=Guid
        /// </summary>
        public int cdeT { get { return _cdeT; } set { SetType(value); } }
        private int _cdeT;
        void SetType(int newType)
        {
            var previousType = _cdeT; // Could use Interlocked.Exchange here, but it's unclear if the benefits (event gets fired exactly once per type change, as opposed to up to two times, or zero times if the type is changed back) warrants the cost of the lock
            if (previousType != newType)
            {
                _cdeT = newType;
                FireEvent(eThingEvents.PropertyTypeChanged, true);
                var pThing = GetThing();
                pThing?.FireEvent(eThingEvents.PropertyTypeChanged, pThing, this, true);
            }
        }

        /// <summary>
        /// The current value of the property.
        /// ATTENTION: If the value is stored encrypted, this property return the encrypted value NOT the unencrypted value!
        /// Is is required for proper serialization/deserialization.
        /// Use .GetValue() instead for proper decrypted value
        /// </summary>
        public object Value
        {
            get
            {
                return mValue;
            }
            set
            {
                SetValue(value, null, DateTimeOffset.MinValue);
            }
        }

        /// <summary>
        /// Sets a new Value to the Property
        /// if cdeE|1 is set, the value will be automatically encrypted
        /// if the property is on a remote node, the C-DEngine will automatically forward the Set Property to the remote node
        /// </summary>
        /// <param name="pValue">The new value</param>
        /// <param name="pOriginator">The originator who wrote the value</param>
        /// <param name="sourceTimeStamp">Time Stamp of the Source. If set to DateTimeOffset.MinValue the CTIM will be set to the current system time.</param>
        /// <returns></returns>
        internal bool SetValue(object pValue, string pOriginator, DateTimeOffset sourceTimeStamp)
        {
            if (OwnerThing == null && cdeO != Guid.Empty)
            {
                OwnerThing = TheThingRegistry.GetThingByMID("*", cdeO);
            }
            TheThing ownerBase = null;
            if (OwnerThing != null)
            {
                ownerBase = OwnerThing.GetBaseThing();
                if (ownerBase.IsOnLocalNode() && cdeN != ownerBase.cdeN)
                    cdeN = ownerBase.cdeN;  //REVIEW: Somewhere cdeN is changed in Isolated Scenarios
            }
            mOldValue = mValue;

            object pVal = pValue;
#if !CDE_STANDARD   //No System.Drawing
            //else
            if (pValue is System.Drawing.Bitmap)
            {
                lock (pValue)
                {
                    System.Drawing.Bitmap tBitmap = pValue as System.Drawing.Bitmap;
                    cdeT = (int)ePropertyTypes.TBinary;
                    byte[] TargetBytes;
                    ThePlanarImage Img = new ThePlanarImage();
                    using (System.IO.MemoryStream memstream = new System.IO.MemoryStream())
                    {
                        tBitmap.Save(memstream, System.Drawing.Imaging.ImageFormat.Jpeg);
                        TargetBytes = memstream.ToArray();
                    }
                    pVal = TargetBytes;
                }
            }
#endif

            if ((cdeE & 1) != 0)
            {
                string tVal = TheCommonUtils.CStr(pVal);
                if (!string.IsNullOrEmpty(tVal) && OwnerThing != null)
                    pVal=TheBaseAssets.MyCrypto.EncryptToString(TheCommonUtils.CStr(tVal),"CDEP");
            }

            bool bIsRemoteThing = ownerBase != null && TheThing.GetSafePropertyBool(OwnerThing, "IsRegisteredGlobally");

            if (bIsRemoteThing && ownerBase?.IsOnLocalNode() == false && string.IsNullOrEmpty(pOriginator)) // NEW: !ownerBase.IsFromOwningRemoteNode(pOriginator) must not be there as changes from other nodes only need to send back to the original owner by that changing node - not all in the mesh
            {
                //Isolated plugins will get this publish
                // publish value to the owning node only, without updating local value
                PublishChange(null, ownerBase.cdeN, pVal);
                return AreValuesDifferent(this.mValue, pVal, (ePropertyTypes)this.cdeT); // We can't really check if this will result in a value change, as that can only be determined by the owning node of the Remote Thing. But this should work in the happy path (slow change rate, good network connectivity).
            }

            if (sourceTimeStamp == DateTimeOffset.MinValue)
            {
                sourceTimeStamp = DateTimeOffset.Now;
            }
            this.cdeCTIM = sourceTimeStamp;

            bool hasChanged = SetProperty(ref mValue, pVal, cdeT, cdeE);
            if (hasChanged)
            {
                this.changedMask = 1;
            }
            else
            {
                this.changedMask = 0;
            }

            // CODE REVIEW: Changing a race condition behavior for property notifications: before we would not fire if a concurrent/subsequent set had changed the value to the same value.
            // However, the receiver will still see the HasChanged property as not having changed (inherent race condition of the design). But callers will get the right indication for the time of the call/change
            // Only consumer of the return value seems to be the JSON Deserializer of property change messages: unclear if it behaves better with either race condition behavior?

            if (hasChanged || (cdeE & 8) != 0) //5.171.0: Always update was missing! Required for HMI-NMI screens
            {
                if ((cdeE & 4) != 0)
                    cdeCTIM = DateTimeOffset.Now;

                if ((cdeFOC > 0 && ((cdeFOC & 8) == 0 || (cdeFOC & 0x20) != 0)) || (bIsRemoteThing && ownerBase?.IsOnLocalNode() == true)) //Only fire this if the ownerThing is marked IsRegisteredGlobally and its hosted on this node
                {
                    ownerBase?.ThrottledSETP(this, cdeN, bIsRemoteThing);
                }
            }

#if CDE_SYNC
                bool FireAsync=false;
#else
            bool FireAsync = (cdeFOC&256)==0; 
#endif
            if (hasChanged)
            {
                if ((cdeFOC & 0x24) != 0)
                {
                    FireEvent(eThingEvents.PropertyChanged, FireAsync, FireEventTimeout);
                    OwnerThing?.FireEvent(eThingEvents.PropertyChanged, OwnerThing, this, FireAsync);
                    if (Name == "Value")
                    {
                        FireEvent(eThingEvents.ValueChanged, FireAsync, FireEventTimeout);
                        if (OwnerThing != null)
                            OwnerThing.FireEvent(eThingEvents.ValueChanged, OwnerThing, this, FireAsync);
                    }
                }
                if (TheBaseAssets.MyServiceHostInfo.IsIsolated && OwnerThing != null && OwnerThing.GetBaseThing() != null && TheBaseAssets.MyCDEPluginTypes.ContainsKey(OwnerThing.GetBaseThing().EngineName))
                {
                    TSM tFireTSM = new (eEngineName.ContentService, $"CDE_SETP:{OwnerThing.GetBaseThing().cdeMID}", TheCommonUtils.SerializeObjectToJSONString(this));    //ThingProperties
                    TheQueuedSenderRegistry.PublishToMasterNode(tFireTSM, false);
                }
            }
            if (ownerBase != null)
            {
                var historian = ownerBase.Historian;
                if (historian != null)
                {
                    historian.AddPropertySnapshot(ownerBase, cdeP.GetPropertyPath(this), pVal, sourceTimeStamp, this.cdeSEQ);
                }
                OwnerThing.FireEvent(eThingEvents.PropertySet, OwnerThing, this, FireAsync);
            }
            FireEvent(eThingEvents.PropertySet, FireAsync, FireEventTimeout);

            if (hasChanged && ownerBase != null && ownerBase.DeviceType != eKnownDeviceTypes.IBaseEngine)
            {
                switch (Name)
                {
                    case "StatusLevel":
                        IBaseEngine tBase = ownerBase.GetBaseEngine();
                        if (tBase != null)
                            tBase.SetStatusLevel(-1);
                        break;
                    case "LastMessage":
                        IBaseEngine tBase2 = ownerBase.GetBaseEngine();
                        if (tBase2 != null)
                        {
                            tBase2.GetEngineState().LastMessage = TheCommonUtils.CStr(pVal);
                        }
                        break;
                }
            }
            return hasChanged;
        }

        /// <summary>
        /// Use this method to ensure the correct value of a Property.
        /// If cdeE|1 is set, the property value is stored encrypted and only this method returns the correct Value
        /// </summary>
        /// <returns></returns>
        public object GetValue()
        {
            if ((cdeE & 1) != 0)
            {
                string tVal = TheCommonUtils.CStr(mValue);
                if (!string.IsNullOrEmpty(tVal))
                    return TheBaseAssets.MyCrypto?.DecryptToString(tVal, "CDEP");
            }
            return mValue;
        }

        // Bit 0 (0x1): Property value was changed during the last SetProperty
        private byte changedMask = 0;

        internal static string GetPropName(string tProName)
        {
            if (tProName.StartsWith("MyPropertyBag"))
            {
                string[] tSplit = tProName.Split('.');
                tProName = tSplit[1];
                if (tSplit.Length > 3)
                {
                    for (int i = 2; i < tSplit.Length - 1; i++)
                        tProName += $".{tSplit[i]}";
                }
            }
            return tProName;
        }

        internal cdeP cdeOP = null;

        #region Event Handling
        internal List<string> GetKnownEvents()
        {
            return MyRegisteredEvents.GetKnownEvents();
        }

        private TheCommonUtils.RegisteredEventHelper<cdeP> MyRegisteredEvents = new ();

        /// <summary>
        /// Removes all Events from cdeP
        /// </summary>
        public void ClearAllEvents()
        {
            MyRegisteredEvents.ClearAllEvents();
        }
        /// <summary>
        /// Register a callback that will be fired on a Property Event
        /// </summary>
        /// <param name="pEventName">Using eThingEvents.XXX to register a callback</param>
        /// <param name="pCallback">A Callback that will be called when the eThingEvents.XXX fires</param>
        public void RegisterEvent(string pEventName, Action<cdeP> pCallback)
        {
            MyRegisteredEvents.RegisterEvent(pEventName, pCallback);
            cdeFOC |= 4;
        }

        /// <summary>
        /// Unregister a previously registered callback
        /// </summary>
        /// <param name="pEventName">eThingEvents that holds the callback </param>
        /// <param name="pCallback">The callback to unregister</param>
        public void UnregisterEvent(string pEventName, Action<cdeP> pCallback)
        {
            MyRegisteredEvents.UnregisterEvent(pEventName, pCallback);
            if (MyRegisteredEvents.HasRegisteredEvents(pEventName))
                cdeFOC &= 0xFFFB;
        }
        /// <summary>
        /// Fire an Event on a property
        /// </summary>
        /// <param name="pEventName">Required - Name of the event</param>
        /// <param name="FireAsync">Required. If set to <see langword="true" />, then the event is fired async ; otherwise, synchronous .</param>
        /// <param name="pFireEventTimeout">Optional. The default value is 0 and events are fired without waiting for execution. -1 sets the timeout to MyServiceHostInfo.EventTimeout</param>
        /// <remarks></remarks>
        public void FireEvent(string pEventName, bool FireAsync, int pFireEventTimeout = 0)
        {
            MyRegisteredEvents.FireEvent(pEventName, this, FireAsync, pFireEventTimeout);
        }

        internal void MoveEventsTo(cdeP pProp)
        {
            if (pProp == null) return;
            pProp.MyRegisteredEvents = MyRegisteredEvents;
            MyRegisteredEvents = null;
        }

        /// <summary>
        /// Sets a timeout in ms for Property Events
        /// </summary>
        internal int FireEventTimeout { get; set; }

        /// <summary>
        /// Returns true if the requested eThingEvents has registered callbacks
        /// </summary>
        /// <param name="pEventName">ethingEvents to be checked</param>
        /// <returns>True if the event has callbacks</returns>
        public bool HasRegisteredEvents(string pEventName)
        {
            return MyRegisteredEvents.HasRegisteredEvents(pEventName);
        }

        /// <summary>
        /// Returns true if this property has registered events
        /// </summary>
        /// <returns></returns>
        public bool HasRegisteredEvents()
        {
            return MyRegisteredEvents.HasRegisteredEvents();
        }

        #endregion

        /// <summary>
        /// A static function that allows to compare two property-values of a given Property Type
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="value"></param>
        /// <param name="cdeT"></param>
        /// <returns></returns>
        public static bool AreValuesDifferent(object storage, object value, ePropertyTypes cdeT)
        {
            switch (cdeT)
            {
                case ePropertyTypes.TNumber:
                    if (TheCommonUtils.CDbl(storage) == TheCommonUtils.CDbl(value)) return false;
                    break;
                case ePropertyTypes.TBoolean:
                    if (TheCommonUtils.CBool(storage) == TheCommonUtils.CBool(value)) return false;
                    break;
                case ePropertyTypes.TDate:
                    if (storage != null && value != null && TheCommonUtils.CDate(storage) == TheCommonUtils.CDate(value)) return false;
                    break;
                case ePropertyTypes.TBinary:
                    if ((storage == null && value == null)) return false;
                    break;
                case ePropertyTypes.TFunction:
                    return false;
                case ePropertyTypes.TGuid:
                    if (TheCommonUtils.CGuid(storage) == TheCommonUtils.CGuid(value)) return false;
                    break;
                default:
                    if (object.Equals(storage, value)) return false;
                    break;
            }
            return true;
        }

        /// <summary>
        /// Returns the Hirarchically property path of a given property
        /// </summary>
        /// <param name="pProp">Property in question</param>
        /// <returns></returns>
        static public string GetPropertyPath(cdeP pProp)
        {
            if (pProp == null) return null;
            if (pProp.cdeOP == null) return pProp.Name;
            return GetParentPath(pProp, "");
        }

        internal static bool IsMetaProperty(cdeP pProp)
        {
            return IsMetaProperty(cdeP.GetPropertyPath(pProp));
        }


        internal static bool IsMetaProperty(string propertyNamePath)
        {
            return propertyNamePath.Contains(".[cdeSensor]") || propertyNamePath.Contains(".[cdeSource]") || propertyNamePath.Contains(".[cdeConfig]");
        }

        static private string GetParentPath(cdeP pProp, string pRes)
        {
            var tRes = $"[{pProp.Name}]";  //TODOV4: Validate with OPC Client/Server and Markus
            if (!string.IsNullOrEmpty(pRes))
                tRes += $".{pRes}";
            if (pProp.cdeOP != null)
                tRes = GetParentPath(pProp.cdeOP, tRes);
            return tRes;
        }
        /// <summary>
        /// Returns the parent property of a given property if it has a parent
        /// </summary>
        /// <param name="pProp">Property to query</param>
        /// <returns></returns>
        static public cdeP GetParentProperty(cdeP pProp)
        {
            return pProp?.cdeOP;
        }

        /// <summary>
        /// Turns event on/off on a Property.
        /// </summary>
        /// <param name="TurnEventsOn">if true, events on the property will be turned on</param>
        /// <returns></returns>
        [Obsolete("Don't turn this on manually. The NMI Subscription system will know when a registration is needed")]
        public cdeP SetPropertyEvents(bool TurnEventsOn)
        {
            if (TurnEventsOn)
                cdeFOC |= 0x1;
            else
                cdeFOC &= 0xFFFE;
            return this;
        }

        /// <summary>
        /// Allows to request synchronous firing of OnChange events. BEWARE: If you stay too long in the OnChange callback you can stall the system!
        /// </summary>
        /// <param name="SyncOn"></param>
        /// <returns></returns>
        public cdeP SetFireSync(bool SyncOn)
        {
            if (SyncOn)
                cdeFOC |= 0x100;
            else
                cdeFOC &= 0xFEFF;
            return this;
        }

        /// <summary>
        /// if set, this Property changes frequently and will not cause the ThingRegistry to be written to Disk on Change
        /// </summary>
        /// <param name="IsHighSpeed"></param>
        /// <returns></returns>
        public cdeP SetHighSpeed(bool IsHighSpeed)
        {
            if (IsHighSpeed)
                cdeFOC |= 0x80;
            else
                cdeFOC &= 0xFF7F;
            return this;
        }

        /// <summary>
        /// Is true if cdeFOC Flag Bit 8 (128 = HighSpeed) is set
        /// </summary>
        public bool IsHighSpeed
        {
            get { return (cdeFOC & 128) != 0; }
        }

        #region Property Management

        /// <summary>
        /// The Proptery Bag of a Property.
        /// ATTENTION: Direct Manipulation of cdePB will not fire any events and circumvents encryption and other API based management.
        /// Only access this Bag if you know exactly what you are doing.
        /// NOTE: It is possible that we remove access to the Bag in a later version of the C-DEngine - do not rely on its access availability
        /// </summary>
        public cdeConcurrentDictionary<string, cdeP> cdePB { get; set; }

        /// <summary>
        /// Returns a count of all current Properties
        /// Only properties directly attached to a this Property will be counted.
        /// </summary>
        [IgnoreDataMember]
        public int PropertyCount
        {
            get { return cdePB == null ? 0 : cdePB.Count; }
        }

        #endregion

        static readonly Dictionary<Type, ePropertyTypes> typeMapping = new ()
        {
            { typeof(double), ePropertyTypes.TNumber },
            { typeof(float), ePropertyTypes.TNumber },
            { typeof(sbyte), ePropertyTypes.TNumber },
            { typeof(Int16), ePropertyTypes.TNumber },
            { typeof(Int32), ePropertyTypes.TNumber },
            { typeof(Int64), ePropertyTypes.TNumber },
            { typeof(byte), ePropertyTypes.TNumber },
            { typeof(UInt16), ePropertyTypes.TNumber },
            { typeof(UInt32), ePropertyTypes.TNumber },
            { typeof(UInt64), ePropertyTypes.TNumber },
            { typeof(DateTimeOffset), ePropertyTypes.TDate },
            { typeof(DateTime), ePropertyTypes.TDate },
            { typeof(bool), ePropertyTypes.TBoolean },
            { typeof(Guid), ePropertyTypes.TGuid },
            { typeof(byte[]), ePropertyTypes.TBinary },
            { typeof(string), ePropertyTypes.TString },
        };
        internal static ePropertyTypes GetCDEType(Type type)
        {
            if (typeMapping.TryGetValue(type, out var cdet))
            {
                return cdet;
            }
            return ePropertyTypes.NOCHANGE;
        }

        internal void PublishChange(string pOriginator, Guid pTargetNode, object valueToPublish, string pTargetEngine = eEngineName.NMIService, bool ForcePublish = false)
        {
            if ((TheBaseAssets.MyServiceHostInfo.DisableNMI || TheBaseAssets.MyServiceHostInfo.DisableNMIMessages) && pTargetEngine == eEngineName.NMIService)
                return;

            if ((Guid.Empty == pTargetNode && (cdeFOC & 1) == 0 && !ForcePublish) || (!string.IsNullOrEmpty(pOriginator) && pTargetNode != Guid.Empty && pTargetNode == TSM.GetOriginator(pOriginator))) return;

            TSM tFireTSM;
            if (OwnerThing != null && OwnerThing.GetBaseThing() != null && cdeO != OwnerThing?.GetBaseThing()?.cdeMID)
            {
                var tName = cdeP.GetPropertyPath(this);
                tFireTSM = new TSM(pTargetEngine, $"SETP:{cdeO}", $"{tName}={(valueToPublish ?? (this))}") { OWN = OwnerThing.GetBaseThing().cdeMID.ToString() };    //ThingProperties
            }
            else
                tFireTSM = new TSM(pTargetEngine, $"SETP:{cdeO}", $"{Name}={(valueToPublish ?? (this))}") { OWN = cdeO.ToString() };    //ThingProperties
            if ((OwnerThing?.GetBaseThing()?.cdeF & 1) != 0)
                tFireTSM.ENG = $"NMI{tFireTSM.OWN}";

#pragma warning disable S3358 // Ternary operators should not be nested - Performance over beauty!
            TheBaseAssets.MySYSLOG.WriteToLog(7678, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM(pTargetEngine, $"Publish({cdeFOC}) to {pTargetNode} Change of {Name}", eMsgLevel.l3_ImportantMessage, tFireTSM.PLS.Length > 50 ? tFireTSM.PLS.Substring(0, 50) : tFireTSM.PLS));
#pragma warning restore S3358 // Ternary operators should not be nested

            tFireTSM.LVL = eMsgLevel.l3_ImportantMessage;
            if ((cdeE & 2) != 0)
            {
                tFireTSM.SetNoDuplicates(true);
                tFireTSM.QDX = 1;
            }
            else //V4: All Property Changes have to be sent...filter might filter out two posts to a thing if sent to fast. Batching should take care of this
            {
                tFireTSM.TXT += $":{Name}";
                tFireTSM.SetNotToSendAgain(true);
            }
            var tOrgGuid = TSM.GetOriginator(pOriginator);
            if (tOrgGuid != Guid.Empty)
            {
                tFireTSM.SetOriginator(tOrgGuid);
                tFireTSM.AddHopToOrigin();
            }
            else if ((cdeFOC & 2) != 0)
                tFireTSM.SetToServiceOnly(true);
            if (Guid.Empty != pTargetNode)
            {
                TheCommCore.PublishToNode(pTargetNode, tFireTSM);
            }
            else
            {
                TheCommCore.PublishCentral(tFireTSM);
            }
            TheCDEKPIs.IncrementKPI(eKPINames.SetPsFired);
        }

        /// <summary>
        /// Returns a string representation of the Property Value
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (mValue != null)
            {
                if (mValue is byte[] && cdeT != (int)ePropertyTypes.TBinary)
                    cdeT = (int)ePropertyTypes.TBinary;
                string tVal;
                if (cdeT == (int)ePropertyTypes.TBinary)
                    tVal = (mValue is string ? TheCommonUtils.CStr(mValue) : Convert.ToBase64String((byte[])mValue));
                else
                    tVal = TheCommonUtils.CStr(mValue);
                return tVal;
            }
            else
                return "";
        }

        /// <summary>
        /// Constructor of a new Property
        /// </summary>
        public cdeP()
        { }

        /// <summary>
        /// Creates a new property from an exiting property
        /// </summary>
        /// <param name="ToClone">Property to be cloned to this</param>
        public cdeP(cdeP ToClone)
        {
            this.cdeA = ToClone.cdeA;
            this.cdeAVA = ToClone.cdeAVA;
            this.cdeCTIM = ToClone.cdeCTIM;
            this.cdeE = ToClone.cdeE;
            this.cdeEXP = ToClone.cdeEXP;
            this.cdeF = ToClone.cdeF;
            this.cdeFOC = ToClone.cdeFOC;
            this.cdeM = ToClone.cdeM;
            this.cdeMID = ToClone.cdeMID;
            this.cdeN = ToClone.cdeN;
            this.cdeO = ToClone.cdeO;
            this.cdeOP = ToClone.cdeOP;
            this.cdePRI = ToClone.cdePRI;
            this.cdeT = ToClone.cdeT;
            this.Name = ToClone.Name;
            this.mValue = ToClone.mValue;
            this.mOldValue = ToClone.mOldValue;
            this.OwnerThing = ToClone.OwnerThing;
            ToClone.Clone(this);
        }

        /// <summary>
        /// Creates a temporary new Property that is not bound to a TheThing and will not be made persistant
        /// </summary>
        /// <param name="pName"></param>
        /// <param name="pValue"></param>
        public cdeP(string pName, object pValue)
        {
            Name = pName;
            SetValue(pValue, null, DateTimeOffset.Now);
        }

        /// <summary>
        /// Constructor of a new property
        /// </summary>
        /// <param name="pThing">The Owner-Thing of the Property</param>
        public cdeP(ICDEThing pThing)
        {
            OwnerThing = pThing;
        }

        /// <summary>
        /// Constructor of a new Property setting the Owner-Thing and an OnProperty Change event
        /// </summary>
        /// <param name="pThing"></param>
        /// <param name="pOnChangeEvent"></param>
        public cdeP(ICDEThing pThing, Action<cdeP> pOnChangeEvent)
        {
            OwnerThing = pThing;
            if (pOnChangeEvent != null)
            {
                RegisterEvent(eThingEvents.PropertyChanged, pOnChangeEvent);
                cdeFOC |= 4;
            }
        }

        private ICDEThing OwnerThing = null;

        /// <summary>
        /// Returns true if the property has an Owner-Thing
        /// </summary>
        /// <returns></returns>
        public bool HasThing() { return OwnerThing != null; }

        /// <summary>
        /// Sets the Owner-Thing of a Property
        /// </summary>
        /// <param name="pThing">Owner-Thing that owns this property</param>
        public void SetThing(ICDEThing pThing)
        {
            OwnerThing = pThing;
        }

        /// <summary>
        /// Return the OwnerThing of this property
        /// </summary>
        /// <returns></returns>
        public ICDEThing GetThing()
        {
            return OwnerThing;
        }

        /// <summary>
        /// If this function is called, the Property will be known globally on all nodes
        /// </summary>
        /// <param name="pOwner"></param>
        public void RegisterGlobally(ICDEThing pOwner)
        {
            OwnerThing ??= pOwner;
            if (OwnerThing == null) return;
            TSM tTSM = new (eEngineName.ContentService, "CDE_REGISTERPROPERTY:" + OwnerThing.ToString(), this.Name);
            tTSM.SetToServiceOnly(true);
            TheCommCore.PublishCentral(tTSM);
        }

        internal void Clone(cdeP Target)
        {
            if (cdePB != null && cdePB.Count > 0)
            {
                Target.cdePB = new cdeConcurrentDictionary<string, cdeP>();
                foreach (string key in cdePB.Keys)
                {
                    cdeP tP = null;
                    if (cdePB[key].Value != null)
                        tP = Target.SetProperty(key, TheCommonUtils.CStr(cdePB[key].GetValue()), (ePropertyTypes)cdePB[key].cdeT, cdePB[key].cdeFOC, null);
                    else
                        tP = Target.SetProperty(key, null, (ePropertyTypes)cdePB[key].cdeT, cdePB[key].cdeFOC, null); // CODE REVIEW: Why use "" here?
                    tP.cdeE = cdePB[key].cdeE;
                    cdePB[key].Clone(tP);
                }
            }
        }
        internal void ClonePropertyMetaData(cdeP Target)
        {
            if (Target == null)
            {
                return;
            }
            CloneMetaDataNoRecursion(Target);
            if (cdePB != null && cdePB.Count > 0)
            {
                foreach (string key in cdePB.Keys)
                {
                    var childProp = cdePB[key];
                    if (Target.cdePB == null || !Target.cdePB.TryGetValue(key, out var targetChild))
                    {
                        targetChild = Target.SetPropertyTypeOnly(key, (ePropertyTypes)childProp.cdeT);
                    }
                    childProp.ClonePropertyMetaData(targetChild);
                }
            }
        }

        void CloneMetaDataNoRecursion(cdeP tP)
        {
            if (tP.Name != this.Name)
            {
                return;
            }
            tP.cdeT = cdeT; // CODE REVIEW: Should we use this or directly update the cdeP (more efficient). What do we lose? CM: we dont lose anything - this function is "NoRecursion" so it should be fine


            tP.cdeA = this.cdeA;
            tP.cdeAVA = this.cdeAVA;
            tP.cdeE = this.cdeE;
            tP.cdeEXP = this.cdeEXP;
            tP.cdeF = this.cdeF;
            tP.cdeM = this.cdeM;
            tP.cdeN = this.cdeN;
            tP.cdePRI = this.cdePRI;
        }

        #region SetSafeProperty...Functions

        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TNumber"
        /// </summary>
        /// <param name="pProp">Target Property</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Numeric (double) Property Value</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafePropertyNumber(cdeP pProp, string pName, double pValue, bool UpdateThing = false)
        {
            return SetSafeProperty(pProp, pName, pValue, ePropertyTypes.TNumber, UpdateThing);
        }

        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TBoolean"
        /// </summary>
        /// <param name="pProp">Target Property</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Boolean Property Value</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafePropertyBool(cdeP pProp, string pName, bool pValue, bool UpdateThing = false)
        {
            return SetSafeProperty(pProp, pName, pValue, ePropertyTypes.TBoolean, UpdateThing);
        }

        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TDate"
        /// </summary>
        /// <param name="pProp">Target Property</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">DateTimeOffset Property Value</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafePropertyDate(cdeP pProp, string pName, DateTimeOffset pValue, bool UpdateThing = false)
        {
            return SetSafeProperty(pProp, pName, pValue, ePropertyTypes.TDate, UpdateThing);
        }

        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TGuid"
        /// </summary>
        /// <param name="pProp">Target Property</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Guid Property Value</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafePropertyGuid(cdeP pProp, string pName, Guid pValue, bool UpdateThing = false)
        {
            return SetSafeProperty(pProp, pName, pValue, ePropertyTypes.TGuid, UpdateThing);
        }

        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TString"
        /// </summary>
        /// <param name="pProp">Target Property</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">String Property Value</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafePropertyString(cdeP pProp, string pName, string pValue, bool UpdateThing = false)
        {
            return SetSafeProperty(pProp, pName, pValue, ePropertyTypes.TString, UpdateThing);
        }

        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TString"
        /// </summary>
        /// <param name="pProp">Target Property</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">A value to be put in the Property. No Conversion will take place - it will be stored in the Property with its original Type but Serialized with ToString()</param>
        /// <param name="pType">Sets the Type of the Property</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <returns></returns>
        public static cdeP SetSafeProperty(cdeP pProp, string pName, object pValue, ePropertyTypes pType, bool UpdateThing = false)
        {
            if (pProp == null) return null;
            cdeP ret = pProp.GetProperty(pName, true);
            ret.cdeT = (int)pType;
            ret = pProp.SetProperty(pName, pValue);
            if (UpdateThing && ret.OwnerThing != null)
                TheThingRegistry.UpdateThing(ret.OwnerThing, true, ret.IsHighSpeed);
            return ret;
        }

        /// <summary>
        /// A type/null save helper to Store a Numeric value in a given property. The target property type  will be set to "TString"
        /// </summary>
        /// <param name="pProp">Target Property</param>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">A value to be put in the Property. No Conversion will take place - it will be stored in the Property with its original Type but Serialized with ToString()</param>
        /// <param name="pType">Sets the Type of the Property</param>
        /// <param name="UpdateThing">If set to true, TheThing's UpdateThing function will be called and TheThing is updated in TheThingRegistry and TheThingRegistry is flushed to Disk</param>
        /// <param name="ForceUpdate">Forces an update to the Thing - even if the value of the property is the same as already store in the property</param>
        /// <returns></returns>
        public static cdeP SetSafeProperty(cdeP pProp, string pName, object pValue, ePropertyTypes pType, bool UpdateThing, bool ForceUpdate)
        {
            if (pProp == null) return null;
            cdeP ret = pProp.SetProperty(pName, pValue, pType, ForceUpdate ? 0x20 : -1, null); //NEW3.1: 0x20 was 8
            if (UpdateThing && ret.OwnerThing != null)
                TheThingRegistry.UpdateThing(ret.OwnerThing, true, ret.IsHighSpeed);
            return ret;
        }

        #endregion

        #region Property Management

        /// <summary>
        /// Removes a property from TheThing at Runtime
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public cdeP RemoveProperty(string pName)
        {
            cdeP t = GetProperty(pName);
            if (t != null)
            {
                if (t.cdeOP == null)
                    cdePB?.TryRemove(t.Name, out _);
                else
                    t.cdeOP.cdePB?.TryRemove(t.Name, out _);
                t.ClearAllEvents();
                FireEvent(eThingEvents.PropertyDeleted, true);
            }
            return t;
        }

        /// <summary>
        /// Removes all properties with a given prefix from TheThing at Runtime
        /// Only properties directly attached to a this Property will be deleted.
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public bool RemovePropertiesStartingWith(string pName)
        {
            bool retVal = false;
            if (cdePB != null)
            {
                List<string> Keys = new ();
                lock (cdePB.MyLock)
                {
                    Keys.AddRange(from string key in cdePB.Keys
                                  where key.StartsWith(pName)
                                  select key);
                    foreach (string key in Keys)
                    {
                        RemoveProperty(key);
                        retVal = true;
                    }
                }
            }
            return retVal;
        }

        /// <summary>
        /// returns a list of all sub-properties of the cdeP
        /// Only properties directly attached to a this property will be returned.
        /// </summary>
        /// <returns></returns>
        public List<cdeP> GetAllProperties()
        {
            if (cdePB == null)
            {
                return new();
            }
            var propNames = cdePB.Keys.ToList();
            var props = new List<cdeP>();
            foreach (var propName in propNames)
            {
                bool bNoFire = false;
                props.Add(GetPropertyInternal(propName, true, ref bNoFire));
            }
            return props;
        }

        /// <summary>
        /// returns a list of all sub-properties of the cdeP as well as their sub-properties up to the specified nesting level
        /// </summary>
        /// <param name="maxSubPropertyLevel">Indicates the maximum nesting level of sub-properties. 0 = only top-level properties, 1 = only top-level properties and their immediate sub-properties etc.</param>
        /// <returns></returns>
        public IEnumerable<cdeP> GetAllProperties(int maxSubPropertyLevel)
        {
            if (cdePB == null)
            {
                return new List<cdeP>();
            }
            var tPropNames = cdePB.Keys.ToList();
            var props = new List<cdeP>();
            foreach (var propName in tPropNames)
            {
                bool bNoFire = false;
                var prop = GetPropertyInternal(propName, true, ref bNoFire);
                props.Add(prop);
                if (maxSubPropertyLevel > 1)
                {
                    props.AddRange(prop.GetAllProperties(maxSubPropertyLevel - 1));
                }
            }
            return props;
        }

        /// <summary>
        /// Returns a list of Properties starting with pName in the Name
        /// Only properties directly attached to a this Property will be returned.
        /// </summary>
        /// <param name="pName">Start String of pName. case is ignored</param>
        /// <returns>List of properties matching the condition</returns>
        public List<cdeP> GetPropertiesStartingWith(string pName)
        {
            List<cdeP> Keys = new ();
            if (cdePB != null)
            {
                lock (cdePB.MyLock)
                {
                    foreach (var key in cdePB.Keys.Where(key => key.StartsWith(pName, StringComparison.OrdinalIgnoreCase)))
                    {
                        bool bNoFire = false;
                        var prop = GetPropertyInternal(key, true, ref bNoFire);
                        Keys.Add(prop);
                    }
                }
            }
            return Keys;
        }

        /// <summary>
        /// Returns all properties where the cdeM Meta Field starts with pName
        /// Only properties directly attached to a this Property will be returned.
        /// </summary>
        /// <param name="pName">Start Sequence of the MetaData. case is ignored</param>
        /// <returns>List of properties matching the condition</returns>
        public List<cdeP> GetPropertiesMetaStartingWith(string pName)
        {
            List<cdeP> Keys = new ();
            if (cdePB != null)
            {
                lock (cdePB.MyLock)
                {
                    foreach (cdeP key in cdePB.Values)
                    {
                        if (!string.IsNullOrEmpty(key.cdeM) && key.cdeM.StartsWith(pName, StringComparison.OrdinalIgnoreCase))
                        {
                            bool bNoFire = false;
                            var prop = GetPropertyInternal(key.Name, true, ref bNoFire);
                            Keys.Add(prop);
                        }
                    }
                }
            }
            return Keys;
        }


        /// <summary>
        /// Registers a new property with TheThing at runtime
        /// </summary>
        /// <param name="pName">Name of the Property to be registered.</param>
        /// <returns></returns>
        public cdeP RegisterProperty(string pName)
        {
            return SetProperty(pName, null);
        }
        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// The Property Change Events and Set Property events are NOT Fired
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <returns></returns>
        public cdeP SetPropertyNoEvents(string pName, object pValue)
        {
            return SetProperty(pName, pValue, ePropertyTypes.NOCHANGE, DateTimeOffset.MinValue, 0x8, null, null);
        }
        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <returns></returns>
        public cdeP SetPropertyForceEvents(string pName, object pValue)
        {
            return SetProperty(pName, pValue, ePropertyTypes.NOCHANGE, DateTimeOffset.MinValue, 0x20, null, null);
        }

        /// <summary>
        /// Sets the type of a property
        /// If the property does not exist, it will be created
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pType">Type of the property</param>
        /// <returns></returns>
        public cdeP SetPropertyTypeOnly(string pName, ePropertyTypes pType)
        {
            return SetProperty(pName, null, pType, DateTimeOffset.MinValue, 0x10, null, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <returns></returns>
        public cdeP SetProperty(string pName, object pValue)
        {
            return SetProperty(pName, pValue, ePropertyTypes.NOCHANGE, DateTimeOffset.MinValue, -1, null, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="pType">The Type of the Property</param>
        /// <returns></returns>
        public cdeP SetProperty(string pName, object pValue, ePropertyTypes pType)
        {
            return SetProperty(pName, pValue, pType, DateTimeOffset.MinValue, -1, null, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="EventAction">Defines what events should be fired</param>
        /// <param name="pOnChangeEvent">a callback for a local (current node) onChangeEvent</param>
        /// <returns></returns>
        public cdeP SetProperty(string pName, object pValue, int EventAction, Action<cdeP> pOnChangeEvent = null)
        {
            return SetProperty(pName, pValue, ePropertyTypes.NOCHANGE, DateTimeOffset.MinValue, EventAction, pOnChangeEvent, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="pType">The Type of the Property</param>
        /// <param name="EventAction">Defines what events should be fired</param>
        /// <param name="pOnChangeEvent">a callback for a local (current node) onChangeEvent</param>
        /// <returns></returns>
        public cdeP SetProperty(string pName, object pValue, ePropertyTypes pType, int EventAction, Action<cdeP> pOnChangeEvent = null)
        {
            return SetProperty(pName, pValue, pType, DateTimeOffset.MinValue, EventAction, pOnChangeEvent, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="sourceTimeStamp">Overwrite the CTIM (Creation Time Stamp). If set to DateTimeOffset.MinValue the CTIM will be set to the current system time.</param>
        public cdeP SetProperty(string pName, object pValue, DateTimeOffset sourceTimeStamp)
        {
            return SetProperty(pName, pValue, ePropertyTypes.NOCHANGE, sourceTimeStamp, -1, null, null);
        }


        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="pType">The Type of the Property</param>
        /// <param name="sourceTimeStamp">Overwrite the CTIM (Creation Time Stamp). If set to DateTimeOffset.MinValue the CTIM will be set to the current system time.</param>
        /// <param name="EventAction">Defines what events should be fired</param>
        /// <param name="pOnChangeEvent">a callback for a local (current node) onChangeEvent</param>
        /// <returns></returns>
        public cdeP SetProperty(string pName, object pValue, ePropertyTypes pType, DateTimeOffset sourceTimeStamp, int EventAction, Action<cdeP> pOnChangeEvent)
        {
            return SetProperty(pName, pValue, pType, sourceTimeStamp, EventAction, pOnChangeEvent, null);
        }

        /// <summary>
        /// Sets a property
        /// If the property does not exist, it will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="pName">Property Name</param>
        /// <param name="pValue">Value to be set</param>
        /// <param name="pType">The Type of the Property</param>
        /// <param name="sourceTimeStamp">Overwrite the CTIM (Creation Time Stamp). If set to DateTimeOffset.MinValue the CTIM will be set to the current system time.</param>
        /// <param name="EventAction">Defines what events should be fired</param>
        /// <param name="pOnChangeEvent">a callback for a local (current node) onChangeEvent</param>
        /// <param name="originator">Originating node of the change when processing changes from a Remote Thing: used to prevent re-publishing of changes and ensure local processing</param>
        /// <returns></returns>
        internal cdeP SetProperty(string pName, object pValue, ePropertyTypes pType, DateTimeOffset sourceTimeStamp, int EventAction, Action<cdeP> pOnChangeEvent, string originator)
        {
            return SetPropertyInternal(pName, pValue, pType, sourceTimeStamp, EventAction, pOnChangeEvent, originator);
        }
        internal cdeP SetPropertyInternal(string pName, object pValue, ePropertyTypes pType, DateTimeOffset sourceTimeStamp, int EventAction, Action<cdeP> pOnChangeEvent, string originator)
        {
            if (string.IsNullOrEmpty(pName)) return null;

            object tInternalValue = pValue;

            bool WasOk = true;
            if ("iValue".Equals(pName))
            {
                pName = "Value";
                if (EventAction < 0)
                    EventAction = 8;
                else
                    EventAction |= 8;
            }
            cdeP tProp = GetPropertyInternal(pName, true, ref WasOk);

            // Code Review: Why is at least some of this logic not in cdeP.SetValue? This creates more entry points with subtly different semantics
            tProp.cdeFOC &= 0xFFC7; //Removes the iOnlyFlag and ForceFlag

            if (pType != ePropertyTypes.NOCHANGE)
                tProp.cdeT = (int)pType;
            if (EventAction >= 0)
                tProp.cdeFOC |= EventAction;
            if (pOnChangeEvent != null)
            {
                tProp.UnregisterEvent(eThingEvents.PropertyChanged, pOnChangeEvent);
                tProp.RegisterEvent(eThingEvents.PropertyChanged, pOnChangeEvent);
            }
            if (EventAction < 0 || (EventAction & 0x10) == 0)
            {
                tProp.SetValue(tInternalValue, originator, sourceTimeStamp);
            }
            UpdatePropertyInBag(tProp, WasOk);

            return tProp;
        }

        internal void UpdatePropertyInBag(cdeP pProp, bool NoFireAdd)
        {
            if (pProp == null) return;
            cdeConcurrentDictionary<string, cdeP> tPB;
            if (pProp.cdeOP == null)
                tPB = cdePB;
            else
            {
                pProp.cdeOP.cdePB ??= new cdeConcurrentDictionary<string, cdeP>();
                tPB = pProp.cdeOP.cdePB;
            }
            if (NoFireAdd)
                tPB[pProp.Name] = pProp;
            else
            {
                NoFireAdd = tPB.TryAdd(pProp.Name, pProp);
                if (NoFireAdd)
                    FireEvent(eThingEvents.PropertyAdded, true);
            }
        }

        /// <summary>
        /// Sets multiple properties at once. Use with the Historian feature for a consistent snapshot that has all these property changes.
        /// If any of the properties do not exist, they will be created
        /// All Events (Change and Set) will be fired - even if the property has not changed
        /// The Property Type will be set as well
        /// </summary>
        /// <param name="nameValueDictionary"></param>
        /// <param name="sourceTimestamp"></param>
        /// <returns></returns>
        public bool SetProperties(IDictionary<string, object> nameValueDictionary, DateTimeOffset sourceTimestamp)
        {
            if (nameValueDictionary == null) return false;
            foreach (var nv in nameValueDictionary)
            {
                SetPropertyInternal(nv.Key, nv.Value, ePropertyTypes.NOCHANGE, sourceTimestamp, 0, null, null);
            }
            return true;
        }

        /// <summary>
        /// Registers a new OnPropertyChange Event Callback
        /// </summary>
        /// <param name="OnUpdateName">Name of the property to monitor</param>
        /// <param name="oOnChangeCallback">Callback to be called when the property changes</param>
        /// <returns></returns>
        public bool RegisterOnChange(string OnUpdateName, Action<cdeP> oOnChangeCallback)
        {
            if (oOnChangeCallback == null || string.IsNullOrEmpty(OnUpdateName)) return false;

            cdeP tProp = GetProperty(OnUpdateName, true);
            if (tProp != null)
            {
                tProp.cdeFOC |= 4;
                tProp.UnregisterEvent(eThingEvents.PropertyChanged, oOnChangeCallback);
                tProp.RegisterEvent(eThingEvents.PropertyChanged, oOnChangeCallback);
            }
            else
                SetProperty(OnUpdateName, null, 1, oOnChangeCallback);
            return true;
        }

        /// <summary>
        /// Returns a property of a given Name. If the property does not exist the method returns NULL
        /// </summary>
        /// <param name="pName">Name of the property</param>
        /// <returns></returns>
        public cdeP GetProperty(string pName)
        {
            return GetProperty(pName, false);
        }
        /// <summary>
        /// Returns a property of a given Name. If the property does not exist the method returns NULL
        /// </summary>
        /// <param name="pName">Name of the property</param>
        /// <param name="DoCreate">if true, the property will be created if it did not exist</param>
        /// <returns></returns>
        public cdeP GetProperty(string pName, bool DoCreate)
        {
            bool tFire = false;
            return GetPropertyInternal(pName, DoCreate, ref tFire);
        }
        private cdeP GetPropertyRoot(string pName, bool DoCreate, ref bool NoFireAdded)
        {
            if (string.IsNullOrEmpty(pName) || (cdePB == null && !DoCreate)) return null;
            if (!cdePB.TryGetValue(pName, out cdeP tProp))
            {
                if (DoCreate)
                {
                    tProp = new cdeP(OwnerThing) { Name = pName, cdeO = cdeMID, cdeOP = this };
                    if (cdePB.TryAdd(pName, tProp) && !NoFireAdded)
                        FireEvent(eThingEvents.PropertyAdded, true);
                    NoFireAdded = false;
                }
            }
            else
            {
                if (!tProp.HasThing())
                    tProp.SetThing(OwnerThing);
                tProp.cdeO = cdeMID;
                tProp.cdeOP ??= this;
            }
            return tProp;
        }

        private cdeP GetPropertyInternal(string pName, bool DoCreate, ref bool NoFireAdded)
        {
            if (string.IsNullOrEmpty(pName) || (cdePB == null && !DoCreate)) return null;

            cdePB ??= new cdeConcurrentDictionary<string, cdeP>();

            if (!pName.StartsWith("["))
                return GetPropertyRoot(pName, DoCreate, ref NoFireAdded);


            cdeP parent = this;
            cdeP tP = null;
            var tPath = TheCommonUtils.cdeSplit(pName, "].[", false, false);
            var tResBag = cdePB;
            for (int i = 0; i < tPath.Length; i++)
            {
                var tName = (i == 0 ? tPath[i].Substring(1) : tPath[i]);
                if (i == tPath.Length - 1)
                    tName = tName.Substring(0, tName.Length - 1);
                if (!tResBag.TryGetValue(tName, out tP))
                {
                    if (DoCreate)
                    {
                        tP = new cdeP(OwnerThing) { Name = tName, cdeO = parent.cdeMID, cdeOP = parent };
                        if (i < tPath.Length - 1)
                            tP.cdePB = new cdeConcurrentDictionary<string, cdeP>();
                        if (tResBag.TryAdd(tName, tP) && !NoFireAdded)
                            FireEvent(eThingEvents.PropertyAdded, true);
                        tResBag = tP.cdePB;
                    }
                    else
                        return null;
                }
                else
                {
                    if (!tP.HasThing())
                        tP.SetThing(OwnerThing);
                    tP.cdeOP ??= parent;
                    if (i < tPath.Length - 1)
                    {
                        if (tP.cdePB == null && DoCreate)
                        {
                            tP.cdePB = new cdeConcurrentDictionary<string, cdeP>();
                            tP.SetProperty(tName, true);
                        }
                        tResBag = tP.cdePB;
                    }
                }
                if (tResBag == null)
                    return null;
                parent = tP;
            }
            return tP;
        }

        /// <summary>
        /// This function allows to declare a secure Property
        /// Secure Properties are stored encrypted and can only be decrypted on nodes with the same ApplicationID and SecurityID.
        /// These properties are sent encrypted via the mesh. JavaScript clients CANNOT decrypt the value of the property!
        /// </summary>
        /// <param name="pName"></param>
        /// <param name="pType">In V3.2 only STRING can be declared secure.</param>
        /// <returns></returns>
        public cdeP DeclareSecureProperty(string pName, ePropertyTypes pType)
        {
            cdeP tProp = GetProperty(pName, true);
            if (tProp == null || (tProp.cdeE & 0x01) != 0) return tProp;

            string tOldValue = tProp.ToString();
            if (tProp.cdeO != cdeMID)
                tProp.cdeO = cdeMID;
            tProp.cdeE |= 0x1;
            tProp.cdeT = (int)pType;
            if (!TheCommonUtils.IsNullOrWhiteSpace(tOldValue))
                tProp.Value = tOldValue;
            return tProp;
        }


        /// <summary>
        /// Return the property type of a given property name. Returns ZERO if the property does not exist or the name is null/""
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public ePropertyTypes GetPropertyType(string pName)
        {
            if (string.IsNullOrEmpty(pName) || cdePB == null) return 0;
            var tP = GetProperty(pName, false);
            if (tP != null)
                return (ePropertyTypes)tP.cdeT;
            return 0;
        }

        /// <summary>
        /// registers a function that is called when the StatusLevel of this Thing has changed
        /// </summary>
        /// <param name="sinkStatusHasChanged"></param>
        public void RegisterStatusChanged(Action<cdeP> sinkStatusHasChanged)
        {
            if (sinkStatusHasChanged != null)
                GetProperty("StatusLevel", true).RegisterEvent(eThingEvents.PropertyChanged, sinkStatusHasChanged);
        }

        /// <summary>
        /// unregisters a function that is called when the StatusLevel of this Thing has changed
        /// </summary>
        /// <param name="sinkStatusHasChanged"></param>
        public void UnregisterStatusChanged(Action<cdeP> sinkStatusHasChanged)
        {
            if (sinkStatusHasChanged != null)
                GetProperty("StatusLevel", true).UnregisterEvent(eThingEvents.PropertyChanged, sinkStatusHasChanged);
        }
        #endregion
    }
}
