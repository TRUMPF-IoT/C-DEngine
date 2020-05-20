// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.IO;
#pragma warning disable 1591    //TODO: Remove and document public methods

#if CDE_INTNEWTON
using cdeNewtonsoft.Json;
#else
using Newtonsoft.Json;
#endif

using System.Threading.Tasks;

namespace nsCDEngine.Engines.ThingService
{
    public partial class cdeP : TheMetaDataBase
    {
        internal const string strConfig = "cdeConfig";

        public bool IsConfig
        {
            get { return this.GetProperty(strConfig) != null; }
            set
            {
                if (value)
                {
                    this.GetProperty(strConfig, true);
                }
                else
                {
                    this.RemoveProperty(strConfig);
                }
            }
        }

        /// <summary>
        /// Helper function to retrieve the configuration meta-data for this property from the corresponding sub-properties
        /// </summary>
        /// <returns></returns>
        public TheThing.TheConfigurationProperty GetConfigMeta()
        {
            var configMetaProp = this.GetProperty(strConfig);
            if (configMetaProp == null)
            {
                return null;
            }

            var rangeMinProp = configMetaProp?.GetProperty(nameof(TheThing.TheConfigurationProperty.RangeMin));
            var rangeMaxProp = configMetaProp?.GetProperty(nameof(TheThing.TheConfigurationProperty.RangeMax));

            var generalizeProp = configMetaProp?.GetProperty(nameof(TheThing.TheConfigurationProperty.Generalize));
            var isThingReferenceProp = configMetaProp?.GetProperty(nameof(TheThing.TheConfigurationProperty.IsThingReference));
            var requiredProp = configMetaProp?.GetProperty(nameof(TheThing.TheConfigurationProperty.Required));
            var secureProp = configMetaProp?.GetProperty(nameof(TheThing.TheConfigurationProperty.Secure));

            var configMeta = new TheThing.TheConfigurationProperty
            {
                Name = this.Name,
                cdeT = (ePropertyTypes)this.cdeT,
                Value = this.Value,
                DefaultValue = configMetaProp?.GetProperty(nameof(TheThing.TheConfigurationProperty.DefaultValue))?.GetValue(),
                Generalize = generalizeProp != null && generalizeProp.Value != null ? (bool?)TheCommonUtils.CBool(generalizeProp.GetValue()) : null,
                IsThingReference = isThingReferenceProp != null && isThingReferenceProp.Value != null ? (bool?)TheCommonUtils.CBool(isThingReferenceProp.GetValue()) : null,
                Required = requiredProp != null && requiredProp.Value != null ? (bool?)TheCommonUtils.CBool(requiredProp.GetValue()) : null,
                Secure = secureProp != null && secureProp.Value != null ? (bool?)TheCommonUtils.CBool(secureProp.GetValue()) : null, // TODO Do we really want to decrypt here?
                Description = TheCommonUtils.CStrNullable(configMetaProp?.GetProperty(nameof(TheThing.TheConfigurationProperty.Description))),
                Units = TheCommonUtils.CStrNullable(configMetaProp?.GetProperty(nameof(TheThing.TheConfigurationProperty.Units))),
                RangeMin = rangeMinProp != null && rangeMinProp.Value != null ? (double?)TheCommonUtils.CDbl(rangeMinProp) : null,
                RangeMax = rangeMaxProp != null && rangeMaxProp.Value != null ? (double?)TheCommonUtils.CDbl(rangeMaxProp) : null,
                ExtensionData = ReadDictionaryFromProperties(configMetaProp, TheThing.TheConfigurationProperty.KnownProperties),
            };

            return configMeta;
        }

        /// <summary>
        /// Helper function to set the sub-properties containing configuration meta-data for this property.
        /// </summary>
        /// <param name="configMeta"></param>
        public void SetConfigMeta(TheThing.TheConfigurationProperty configMeta)
        {
            if (configMeta == null)
            {
                return;
            }
            var configMetaProp = this.GetProperty(strConfig, true);
            if (!string.IsNullOrEmpty(configMeta.Name) && configMeta.Name != this.Name)
            {
                // Log error? Throw exception?
                return;
            }

            if (configMeta.cdeT != null && configMeta.cdeT != ePropertyTypes.NOCHANGE && configMeta.cdeT != (ePropertyTypes)this.cdeT)
            {
                this.SetType((int)configMeta.cdeT);
            }
            if (configMeta.Value != null)
            {
                this.OwnerThing.SetProperty(cdeP.GetPropertyPath(this), configMeta.Value);
            }
            if (configMeta.DefaultValue != null)
            {
                configMetaProp.SetProperty(nameof(TheThing.TheConfigurationProperty.DefaultValue), configMeta.DefaultValue, ePropertyTypes.TString);
            }
            else
            {
                configMetaProp.RemoveProperty(nameof(TheThing.TheConfigurationProperty.DefaultValue));
            }
            if (configMeta.Generalize != null)
            {
                configMetaProp.SetProperty(nameof(TheThing.TheConfigurationProperty.Generalize), configMeta.Generalize.Value, ePropertyTypes.TBoolean);
            }
            else
            {
                configMetaProp.RemoveProperty(nameof(TheThing.TheConfigurationProperty.Generalize));
            }
            if (configMeta.IsThingReference != null)
            {
                configMetaProp.SetProperty(nameof(TheThing.TheConfigurationProperty.IsThingReference), configMeta.IsThingReference.Value, ePropertyTypes.TBoolean);
            }
            else
            {
                configMetaProp.RemoveProperty(nameof(TheThing.TheConfigurationProperty.IsThingReference));
            }
            if (configMeta.Required != null)
            {
                configMetaProp.SetProperty(nameof(TheThing.TheConfigurationProperty.Required), configMeta.Required.Value, ePropertyTypes.TBoolean);
            }
            else
            {
                configMetaProp.RemoveProperty(nameof(TheThing.TheConfigurationProperty.Required));
            }
            if (configMeta.Secure != null)
            {
                if (configMeta.Secure == true)
                {
                    OwnerThing?.GetBaseThing()?.DeclareSecureProperty(cdeP.GetPropertyPath(this), (ePropertyTypes)this.cdeT);
                }
                configMetaProp.SetProperty(nameof(TheThing.TheConfigurationProperty.Secure), configMeta.Secure.Value, ePropertyTypes.TBoolean);
            }
            else
            {
                configMetaProp.RemoveProperty(nameof(TheThing.TheConfigurationProperty.Secure));
            }

            if (configMeta.Description != null)
            {
                configMetaProp.SetProperty(nameof(TheThing.TheConfigurationProperty.Description), configMeta.Description, ePropertyTypes.TString);
            }
            else
            {
                configMetaProp.RemoveProperty(nameof(TheThing.TheConfigurationProperty.Description));
            }
            if (configMeta.Units != null)
            {
                configMetaProp.SetProperty(nameof(TheThing.TheConfigurationProperty.Units), configMeta.Units, ePropertyTypes.TString);
            }
            else
            {
                configMetaProp.RemoveProperty(nameof(TheThing.TheConfigurationProperty.Units));
            }
            if (configMeta.RangeMin != null)
            {
                configMetaProp.SetProperty(nameof(TheThing.TheConfigurationProperty.RangeMin), configMeta.RangeMin, ePropertyTypes.TNumber);
            }
            else
            {
                configMetaProp.RemoveProperty(nameof(TheThing.TheConfigurationProperty.RangeMin));
            }
            if (configMeta.RangeMax != null)
            {
                configMetaProp.SetProperty(nameof(TheThing.TheConfigurationProperty.RangeMax), configMeta.RangeMax, ePropertyTypes.TNumber);
            }
            else
            {
                configMetaProp.RemoveProperty(nameof(TheThing.TheConfigurationProperty.RangeMax));
            }
            if (configMeta.ExtensionData != null)
            {
                configMetaProp.SetProperties(configMeta.ExtensionData, DateTimeOffset.MinValue);
            }
        }
        private static Dictionary<string, object> ReadDictionaryFromProperties(cdeP prop, HashSet<string> propsToExclude)
        {
            if (prop == null)
            {
                return null;
            }
            Dictionary<string, object> dict = null;
            var allProps = prop.GetAllProperties()?.Where(p => propsToExclude?.Contains(p.Name) != true);
            if (allProps?.Any() == true)
            {
                dict = new Dictionary<string, object>();
                foreach (var subProp in allProps)
                {
                    dict.Add(subProp.Name, subProp.GetValue());
                }
            }
            return dict;
        }

    }

    public sealed partial class TheThing : TheMetaDataBase, ICDEThing
    {
        #region Configuration management

        [IgnoreDataMember]
        public bool cdeRequiresCustomConfig
        {
            get { return TheThing.GetSafePropertyBool(this, nameof(cdeRequiresCustomConfig)); }
            set { TheThing.SetSafePropertyBool(this, nameof(cdeRequiresCustomConfig), value); }
        }

        [IgnoreDataMember]
        public bool cdePendingConfig { get { return TheThing.GetSafePropertyBool(this, nameof(cdePendingConfig)); } }

        public cdeP DeclareConfigProperty(TheConfigurationProperty configProperty)
        {
            if (configProperty == null || string.IsNullOrEmpty(configProperty.Name))
            {
                return null;
            }
            bool bPropDidNotExist = false;
            cdeP tProp = GetProperty(configProperty.Name);
            if (tProp == null)
            {
                bPropDidNotExist = true;
                tProp = GetProperty(configProperty.Name, true);
            }
            tProp.cdeE |= 0x04; // cdeCTIM should be updated with changetime
            if (tProp.cdeO != cdeMID)
                tProp.cdeO = cdeMID;
            tProp.SetConfigMeta(configProperty);
            if (bPropDidNotExist && configProperty.Value == null && configProperty.DefaultValue != null)
            {
                SetProperty(configProperty.Name, configProperty.DefaultValue);
            }
            return tProp;
        }

        public class TheThingConfiguration
        {
            public TheThingIdentity ThingIdentity;

            // TODO Do we want to provide a single collection of all thing references? More efficient in terms of storage and potentially easier to consume. For now references are kept inline with sensor/thing subscriptions
            //public Dictionary<string, TheThingReference> ThingReferences;

            public List<TheConfigurationProperty> ConfigurationValues;
            public Dictionary<string, object> CustomConfig; // Note: this can be arbitrary JSON as defined by the thing implementor: specifically the entries in this dictionary do not have to be thing properties.

            /// <summary>
            /// The specialization parameter values to be applied when importing the configuration. Pre-populated with defaults values during export with generalization, null otherwise.
            /// </summary>
            public Dictionary<string, object> ThingSpecializationParameters;
            // If the thing is a sensor provider, the subscriptions it serves are listed here
            public List<TheSensorSubscription> SensorSubscriptions;

            // If the thing is a sensor consumer, the data it consumes is listed here
            public List<TheThingSubscription> ThingSubscriptions;

            public List<TheThingReference> ThingReferences { get; set; }

            public TheThingIdentity GetSpecializedThingIdentity(Dictionary<string, string> thingReferenceMap)
            {
                if (ThingSpecializationParameters != null && ThingSpecializationParameters.TryGetValue(nameof(ID), out var newIdObj))
                {
                    var newId = new TheThingIdentity(ThingIdentity);
                    newId.ID = TheCommonUtils.CStr(newIdObj);
                    newId.ThingMID = null;
                    if (newId.ID != this.ThingIdentity.ID)
                    {
                        thingReferenceMap.Add(ThingIdentity.ID, newId.ID);
                    }
                    return newId;
                }
                return ThingIdentity;
            }


            public override string ToString()
            {
                var result = $"{ThingIdentity}: ConfigCount: {ConfigurationValues?.Count}";
                if (CustomConfig != null)
                {
                    result += $" Custom: {CustomConfig != null}";
                }
                if (SensorSubscriptions != null)
                {
                    result += $" Sensors: {SensorSubscriptions?.Count}";
                }
                if (ThingSubscriptions != null)
                {
                    result += $" ThingSubs: {ThingSubscriptions?.Count}";
                }
                if (ThingReferences != null)
                {
                    result += $" ThingRefs: {ThingReferences?.Count}";
                }
                if (ThingSpecializationParameters != null)
                {
                    result += $" ParamCount: { ThingSpecializationParameters?.Count} ";
                }
                return result;
            }

        }

        public class TheConfigurationProperty
        {
            // TODO How do we model properties that are thing references (i.e. OPC Client TagHostThingForSubscribeAll)? Are these just thing subscriptions?
            public string Name { get; set; }
            public ePropertyTypes? cdeT { get; set; }
            public object Value { get; set; }
            public object DefaultValue { get; set; }
            public bool? Generalize { get; set; }
            public bool? IsThingReference { get; set; }
            public bool? Required { get; set; }
            public bool? Secure { get; set; }
            public string Description { get; set; }
            public string Units { get; set; }
            public double? RangeMin { get; set; }
            public double? RangeMax { get; set; }
            [JsonExtensionData(ReadData = true, WriteData = true)]
            public Dictionary<string, object> ExtensionData { get; set; }

            public TheConfigurationProperty()
            {

            }

            public TheConfigurationProperty(TheConfigurationProperty cp)
            {
                Name = cp.Name;
                cdeT = cp.cdeT;
                Value = cp.Value;
                DefaultValue = cp.DefaultValue;
                Generalize = cp.Generalize;
                IsThingReference = cp.IsThingReference;
                Required = cp.Required;
                Secure = cp.Secure;
                Description = cp.Description;
                Units = cp.Units;
                RangeMin = cp.RangeMin;
                RangeMax = cp.RangeMax;
                if (cp.ExtensionData != null)
                {
                    ExtensionData = new Dictionary<string, object>(cp.ExtensionData);
                }
            }

            public TheConfigurationProperty(string name, Type type, ConfigPropertyAttribute configAttribute)
            {
                if (string.IsNullOrEmpty(configAttribute.NameOverride))
                {
                    Name = name;
                }
                else
                {
                    Name = configAttribute.NameOverride;
                }
                if (configAttribute.cdeT != ePropertyTypes.NOCHANGE)
                {
                    cdeT = configAttribute.cdeT;
                }
                else if (type != null)
                {
                    var mappedType = cdeP.GetCDEType(type);
                    if (mappedType != ePropertyTypes.NOCHANGE)
                    {
                        cdeT = mappedType;
                    }
                }
                IsThingReference = configAttribute.IsThingReference;
                DefaultValue = configAttribute.DefaultValue;
                Generalize = configAttribute.Generalize;
                Required = configAttribute.Required;
                Secure = configAttribute.Secure;
                Description = configAttribute.Description;
                Units = configAttribute.Units;
                if (configAttribute.RangeMin != 0)
                {
                    RangeMin = configAttribute.RangeMin;
                }
                if (configAttribute.RangeMax != 0)
                {
                    RangeMax = configAttribute.RangeMax;
                }
            }

            static HashSet<string> _knownProperties;
            [IgnoreDataMember]
            static internal HashSet<string> KnownProperties
            {
                get
                {
                    if (_knownProperties == null)
                    {
                        _knownProperties = new HashSet<string>
                    {
                        nameof(TheThing.TheConfigurationProperty.DefaultValue),
                        nameof(TheThing.TheConfigurationProperty.Generalize),
                        nameof(TheThing.TheConfigurationProperty.IsThingReference),
                        nameof(TheThing.TheConfigurationProperty.Required),
                        nameof(TheThing.TheConfigurationProperty.Secure),
                        nameof(TheThing.TheConfigurationProperty.Description),
                        nameof(TheThing.TheConfigurationProperty.Units),
                        nameof(TheThing.TheConfigurationProperty.RangeMin),
                        nameof(TheThing.TheConfigurationProperty.RangeMax),
                    };
                    }
                    return _knownProperties;
                }
            }

            public override string ToString()
            {
                return $"{Name}: {Value} [ cdeT:{cdeT} Def: {DefaultValue} ]";
            }

        }

#if !CDE_NET4
        public Task<TheThingConfiguration> GetThingConfigurationAsync(bool bGeneralize)
        {
            return GetThingConfigurationAsync(bGeneralize, false);
        }
        public async Task<TheThingConfiguration> GetThingConfigurationAsync(bool bGeneralize, bool ignoreCapabilityCheck)
        {
            if (!ignoreCapabilityCheck && !Capabilities.Contains(eThingCaps.ConfigManagement))
            {
                return null;
            }
            var thingConfig = new TheThingConfiguration
            {
                ConfigurationValues = new List<TheConfigurationProperty>(),
                ThingSpecializationParameters = bGeneralize ? new Dictionary<string, object>() : null,
            };
            var configProps = this.GetAllProperties(10).Where(p => p.IsConfig).ToList();
            if (!string.IsNullOrEmpty(this.FriendlyName) && configProps.FirstOrDefault(p => p.Name == nameof(FriendlyName)) == null)
            {
                configProps.Add(this.GetProperty(nameof(FriendlyName)));
            }
            if (!string.IsNullOrEmpty(this.ID) && configProps.FirstOrDefault(p => p.Name == nameof(ID)) == null)
            {
                configProps.Add(this.GetProperty(nameof(ID)));
            }
            var thingsReferencedFromProperties = new List<TheThing>();
            foreach (var configProp in configProps)
            {
                var configPropToReturn = configProp.GetConfigMeta();
                if (configPropToReturn == null)
                {
                    if (configProp.Name == nameof(TheThing.FriendlyName))
                    {
                        configPropToReturn = new TheConfigurationProperty { Name = nameof(TheThing.FriendlyName), cdeT = ePropertyTypes.TString, Generalize = true, Value = this.FriendlyName };
                    }
                    else if (configProp.Name == nameof(TheThing.ID))
                    {
                        configPropToReturn = new TheConfigurationProperty { Name = nameof(TheThing.ID), cdeT = ePropertyTypes.TString, Generalize = true, Value = this.ID };
                    }
                }
                if (configPropToReturn.IsThingReference == true)
                {
                    // Get additional information so we can reconnect the thing reference on import using enginename/devicetype/ID, not just cdeMID
                    var tThing = TheThingRegistry.GetThingByMID(TheCommonUtils.CGuid(configPropToReturn.Value));
                    if (tThing != null)
                    {
                        var thingReference = new TheThingReference(tThing);
                        thingReference = GeneralizeThingReference(thingReference);
                        configPropToReturn.Value = TheCommonUtils.SerializeObjectToJSONString(thingReference);
                        thingsReferencedFromProperties.Add(tThing);
                    }
                }
                if (bGeneralize && configPropToReturn?.Generalize == true)
                {
                    thingConfig.ThingSpecializationParameters.Add(configPropToReturn.Name, configPropToReturn.Value);
                    configPropToReturn.Value = configPropToReturn.DefaultValue;
                }
                thingConfig.ConfigurationValues.Add(configPropToReturn);
            }
            // TODO Do we also need to preserve sensor meta data as part of the thing's configuration (for pure container things?)? For now leave it up to the thing/plug-in and the providers to preserve/reestablish the sensor meta data on import

            if (Capabilities.Contains(eThingCaps.SensorProvider))
            {
                var sensorSubscriptionResponse = await this.GetSensorProviderSubscriptionsAsync();
                if (sensorSubscriptionResponse != null && string.IsNullOrEmpty(sensorSubscriptionResponse.Error))
                {
                    thingConfig.SensorSubscriptions = sensorSubscriptionResponse.Subscriptions.Select(s =>
                    {
                        var sub = new TheSensorSubscription(s);
                        if (bGeneralize)
                        {
                            sub.TargetThing = GeneralizeThingReference(sub.TargetThing);
                        }
                        return sub;
                    }
                    ).ToList();
                }
                else
                {
                    // No sensor subscriptions returned!
                    //return null; //TODO: we should state that something went wrong with the SensorProvider but not return here as the Config was already collected
                }
            }

            if (Capabilities.Contains(eThingCaps.SensorConsumer))
            {
                var msgGetThingSubscriptions = new MsgGetThingSubscriptions { Generalize = bGeneralize };
                var thingSubscriptionResponse = await TheCommRequestResponse.PublishRequestJSonAsync<MsgGetThingSubscriptions, MsgGetThingSubscriptionsResponse>(this, this, msgGetThingSubscriptions);
                if (thingSubscriptionResponse != null && string.IsNullOrEmpty(thingSubscriptionResponse.Error))
                {
                    if (bGeneralize)
                    {
                        foreach (var subscription in thingSubscriptionResponse.ThingSubscriptions)
                        {
                            subscription.ThingReference = GeneralizeThingReference(subscription.ThingReference);
                            // TODO eliminate duplicates and replace with wildcards etc.
                        }
                    }
                    thingConfig.ThingSubscriptions = thingSubscriptionResponse.ThingSubscriptions;
                }
                else
                {
                    // No thing subscriptions returned!
                    //return null; //TODO: we should state that something went wrong with the SensorProvider but not return here as the Config was already collected;
                }
            }

            if (this.cdeRequiresCustomConfig)
            {
                var msgExportConfig = new MsgExportConfig { Generalize = bGeneralize };
                var thingExport = await TheCommRequestResponse.PublishRequestJSonAsync<MsgExportConfig, MsgExportConfigResponse>(this, this, msgExportConfig);
                if (thingExport != null)
                {
                    thingConfig.CustomConfig = thingExport.Configuration;
                    if (thingExport.ThingIdentity != null)
                    {
                        thingConfig.ThingIdentity = thingExport.ThingIdentity;
                    }
                    if (thingExport.ThingReferencesToExport != null)
                    {
                        if (thingConfig.ThingReferences == null)
                        {
                            thingConfig.ThingReferences = new List<TheThingReference>();
                        }
                        thingConfig.ThingReferences.AddRange(thingExport.ThingReferencesToExport);
                    }
                }
                else
                {
                    if (!thingConfig.ConfigurationValues.Any())
                    {
                        // It's a legacy thing: no config properties and no custom config returned
                        // TODO Should we return all thing properties? For now just fail and leave it up to the caller
                        //return null; //TODO: we should state that something went wrong with the SensorProvider but not return here as the Config was already collected;
                    }
                }
            }
            if (thingConfig.ThingIdentity == null)
            {
                thingConfig.ThingIdentity = new TheThingReference(this);
                if (bGeneralize)
                {
                    thingConfig.ThingIdentity = GeneralizeThingIdentity(thingConfig.ThingIdentity);
                }
            }
            return thingConfig;
        }
        #endif
        private TheThingReference GeneralizeThingReference(TheThingReference thingReference)
        {
            // TODO also generalize other parts of the reference (Address?)
            return GeneralizeThingIdentity(thingReference) as TheThingReference;
        }

        private TheThingIdentity GeneralizeThingIdentity(TheThingIdentity thingReference)
        {
            // TODO move this into the TheThingReference class?

            var thingReferenceToReturn = thingReference;
            if (string.IsNullOrEmpty(thingReferenceToReturn.EngineName) || string.IsNullOrEmpty(thingReferenceToReturn.DeviceType))
            {
                var tThing = TheThingRegistry.GetThingByMID(thingReferenceToReturn.ThingMID ?? Guid.Empty); // TODO Use thing finder here!
                if (tThing != null)
                {
                    thingReferenceToReturn = new TheThingReference(tThing);
                }
            }
            if (!string.IsNullOrEmpty(thingReferenceToReturn.EngineName))
            {
                thingReferenceToReturn.ThingMID = null;
            }
            return thingReferenceToReturn;
        }

        public class MsgExportConfig
        {
            public bool Generalize { get; set; }
        }

        public class MsgExportConfigResponse
        {
            public Dictionary<string, object> Configuration;
            /// <summary>
            /// Optional: lets the thing customize the thing identity to be used when re-creating the thing on import
            /// </summary>
            public TheThingReference ThingIdentity;
            public string Error;

            public List<TheThingReference> ThingReferencesToExport { get; set; }
        }

        public class MsgApplyConfig
        {
            public Dictionary<string, object> Configuration;
            /// <summary>
            /// Indicates if there are any further pending configuration changes (i.e. subscriptions) that will be concluded with an additional MsgApplyConfig messages.
            /// </summary>
            public bool ConfigurationPending;

            public Dictionary<string, string> ThingReferenceMap;
        }

        public class MsgApplyConfigResponse
        {
            public string Error;
        }

#if !CDE_NET4

        public async Task<bool> ApplyThingConfigurationAsync(TheThingConfiguration thingConfig)
        {
            try
            {
                var thingReferenceMap = new Dictionary<string, string>();
                var specializedThingIdentity = thingConfig.GetSpecializedThingIdentity(thingReferenceMap);
                if (!ApplyConfigurationPropertiesInternal(thingConfig, thingReferenceMap))
                {
                    return false;
                }

                // The subsequent operations require the actual thing object: make sure it's initialized
                if (await TheThingRegistry.WaitForInitializeAsync(this, new TimeSpan(0, 5, 0)) == null)
                {
                    // Log error
                    return false;
                }
                return await ApplySubscriptionsInternalAsync(thingConfig, thingReferenceMap);
            }
            finally
            {
                this.RemoveProperty(nameof(cdePendingConfig));
            }
        }

        private bool ApplyConfigurationPropertiesInternal(TheThingConfiguration thingConfig, Dictionary<string, string> thingReferenceMap)
        {
            var properties = GetConfigurationPropertiesInternal(thingConfig, thingReferenceMap);
            if (properties == null)
            {
                return false;
            }
            TheThing.SetSafePropertyBool(this, nameof(cdePendingConfig), true);
            foreach (var prop in properties)
            {
                this.SetProperty(prop.Name, prop.Value, prop.cdeT);
            }
            return true;
        }

        private static List<cdeP> GetConfigurationPropertiesInternal(TheThingConfiguration thingConfig, Dictionary<string, string> thingReferenceMap)
        {
            var properties = new List<cdeP>();

            // TODO validate that thingConfig.ThingReference matches?
            if (thingConfig.ConfigurationValues != null)
            {
                foreach (var configValue in thingConfig.ConfigurationValues)
                {

                    if (configValue.IsThingReference == true)
                    {
                        try
                        {
                            // Adjust thing references on a property marked as containing a thing cdeMID (IsThingReference == true)
                            var thingReference = TheCommonUtils.DeserializeJSONStringToObject<TheThingReference>(TheCommonUtils.CStr(configValue.Value));
                            if (thingReference != null)
                            {
                                if (thingReferenceMap != null)
                                {
                                    if (thingReferenceMap.TryGetValue(thingReference.ID, out var newID))
                                    {
                                        thingReference.ID = newID;
                                    }
                                }
                                var thingMID = thingReference.GetMatchingThingMID();
                                if (thingMID != null)
                                {
                                    configValue.Value = thingMID;
                                }
                            }
                        }
                        catch { }
                    }

                    properties.Add(new cdeP { Name = configValue.Name, Value = configValue.Value, cdeT = (int) (configValue.cdeT ?? ePropertyTypes.NOCHANGE) });
                    // TODO Set config meta? Or should we assume this is done by the Thing?
                }
            }

            if (thingConfig.ThingSpecializationParameters != null)
            {
                var propertiesToSpecialize = thingConfig.ConfigurationValues.Where(cv => cv.Generalize == true).ToDictionary(tp => tp.Name);
                if (thingConfig.ThingSpecializationParameters != null)
                {
                    foreach (var specializationParameter in thingConfig.ThingSpecializationParameters)
                    {
                        cdeP specializedProp;
                        if (propertiesToSpecialize.TryGetValue(specializationParameter.Key, out var configProp))
                        {
                            specializedProp = new cdeP { Name = configProp.Name, Value = specializationParameter.Value, cdeT = (int)(configProp.cdeT ?? ePropertyTypes.NOCHANGE) };
                        }
                        else
                        {
                            specializedProp = new cdeP { Name = specializationParameter.Key, Value = specializationParameter.Value, cdeT = (int) ePropertyTypes.NOCHANGE };
                        }
                        properties.RemoveAll(p => p.Name == specializationParameter.Key);
                        properties.Add(specializedProp);
                        propertiesToSpecialize.Remove(specializationParameter.Key);
                    }
                }
                foreach (var unspecifiedProp in propertiesToSpecialize)
                {
                    if (unspecifiedProp.Value.DefaultValue != null)
                    {
                        properties.RemoveAll(p => p.Name == unspecifiedProp.Value.Name);
                        properties.Add(new cdeP { Name = unspecifiedProp.Value.Name, Value = unspecifiedProp.Value.DefaultValue, cdeT = (int)(unspecifiedProp.Value.cdeT ?? ePropertyTypes.NOCHANGE) });
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7723, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Error applying config properties: required specialization parameter not specified", eMsgLevel.l1_Error, $"{thingConfig}: {unspecifiedProp.Key}"));
                        return null;
                    }
                }
            }
            return properties;
        }

        async Task<bool> ApplySubscriptionsInternalAsync(TheThingConfiguration thingConfig, Dictionary<string,string> thingReferenceMap)
        {
            bool bSuccess = true;
            if (thingConfig.CustomConfig?.Count > 0)
            {
                bool moreConfigPending = thingConfig.SensorSubscriptions != null || thingConfig.ThingSubscriptions != null;
                var msgApplyConfig = new MsgApplyConfig { Configuration = thingConfig.CustomConfig, ConfigurationPending = moreConfigPending, ThingReferenceMap = thingReferenceMap };
                var thingApplyResult = await TheCommRequestResponse.PublishRequestJSonAsync<MsgApplyConfig, MsgApplyConfigResponse>(this, this, msgApplyConfig);
                if (thingApplyResult != null)
                {
                    if (string.IsNullOrEmpty(thingApplyResult.Error))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7700, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Applied custom config", eMsgLevel.l3_ImportantMessage, $"{thingConfig.ThingIdentity}"));
                    }
                    else
                    {
                        bSuccess = false;
                        TheBaseAssets.MySYSLOG.WriteToLog(7701, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Error applying custom config", eMsgLevel.l1_Error, $"{thingConfig.ThingIdentity}: {thingApplyResult.Error}"));
                    }
                }
                else
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(7702, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Timeout applying custom config", eMsgLevel.l1_Error, $"{thingConfig.ThingIdentity}"));
                    bSuccess = false;
                }
                if (!moreConfigPending)
                {
                    return bSuccess;
                }
            }

            if (thingConfig.ThingSubscriptions?.Count > 0)
            {
                foreach(var sub in thingConfig.ThingSubscriptions)
                {
                    if (!string.IsNullOrEmpty(sub.ThingReference.ID) && thingReferenceMap.TryGetValue(sub.ThingReference.ID, out var newId))
                    {
                        sub.ThingReference.ID = newId;
                    }
                    // TODO Figure out how to specialize sub.SubscriptionId in a stable (idempotent!) way
                }

                var thingSubscribeResult = await this.SubscribeToThingsAsync(thingConfig.ThingSubscriptions);
                //var msgSubscribeToThings = new MsgSubscribeToThings { SubscriptionRequests = new List<TheThingSubscription>() };
                //msgSubscribeToThings.SubscriptionRequests.AddRange(thingConfig.ThingSubscriptions);
                //var thingSubscribeResult = await TheCommRequestResponse.PublishRequestJSonAsync<MsgSubscribeToThings, MsgSubscribeToThingsResponse>(this, this, msgSubscribeToThings);
                if (thingSubscribeResult != null)
                {
                    if (string.IsNullOrEmpty(thingSubscribeResult.Error))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7703, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Created Thing Subscriptions", eMsgLevel.l3_ImportantMessage, $"{thingConfig.ThingIdentity}: {thingSubscribeResult.GetSubscriptionErrors()}"));
                    }
                    else
                    {
                        bSuccess = false;
                        TheBaseAssets.MySYSLOG.WriteToLog(7704, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Error creating Thing Subscriptions", eMsgLevel.l1_Error, $"{thingConfig.ThingIdentity}: {thingSubscribeResult.Error}. Subscriptions: {thingSubscribeResult.GetSubscriptionErrors()}"));
                    }
                }
                else
                {
                    bSuccess = false;
                    TheBaseAssets.MySYSLOG.WriteToLog(7705, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Error creating Thing Subscriptions: timeout", eMsgLevel.l1_Error, $"{thingConfig.ThingIdentity}"));
                }
            }

            if (thingConfig.SensorSubscriptions?.Count > 0)
            {
                foreach (var sub in thingConfig.SensorSubscriptions)
                {
                    if (!string.IsNullOrEmpty(sub.TargetThing.ID) && thingReferenceMap.TryGetValue(sub.TargetThing.ID, out var newId))
                    {
                        sub.TargetThing.ID = newId;
                    }
                }
                var msgSubscribeSensors = new MsgSubscribeSensors
                {
                    SubscriptionRequests = thingConfig.SensorSubscriptions,
                };
                var sensorSubscribeResult = await this.SubscribeSensorsAsync(msgSubscribeSensors);
                if (sensorSubscribeResult != null)
                {
                    if (string.IsNullOrEmpty(sensorSubscribeResult.Error))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7706, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Created Sensor Subscriptions", eMsgLevel.l3_ImportantMessage, $"{thingConfig.ThingIdentity}: {sensorSubscribeResult.GetSubscriptionErrors()}"));
                    }
                    else
                    {
                        bSuccess = false;
                        TheBaseAssets.MySYSLOG.WriteToLog(7707, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Error creating Sensor Subscriptions", eMsgLevel.l1_Error, $"{thingConfig.ThingIdentity}: {sensorSubscribeResult.Error}. Subscriptions: {sensorSubscribeResult.GetSubscriptionErrors()}"));
                    }
                }
                else
                {
                    bSuccess = false;
                    TheBaseAssets.MySYSLOG.WriteToLog(7708, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Error creating Sensor Subscriptions: timeout", eMsgLevel.l1_Error, $"{thingConfig.ThingIdentity}"));
                }
            }
            if ((thingConfig.CustomConfig?.Count > 0) || this.cdeRequiresCustomConfig)
            {
                var msgApplyConfig = new MsgApplyConfig { ConfigurationPending = false, ThingReferenceMap = thingReferenceMap };
                var thingApplyResult = await TheCommRequestResponse.PublishRequestJSonAsync<MsgApplyConfig, MsgApplyConfigResponse>(this, this, msgApplyConfig);
                if (thingApplyResult != null)
                {
                    if (string.IsNullOrEmpty(thingApplyResult.Error))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7709, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Finalized config", eMsgLevel.l3_ImportantMessage, $"{thingConfig.ThingIdentity}"));
                    }
                    else
                    {
                        bSuccess = false;
                        TheBaseAssets.MySYSLOG.WriteToLog(7710, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Error finalizing config", eMsgLevel.l1_Error, $"{thingConfig.ThingIdentity}: {thingApplyResult.Error}"));
                    }
                }
                else
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(7711, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Timeout finalizing config", eMsgLevel.l1_Error, $"{thingConfig.ThingIdentity}"));
                    bSuccess = false;
                }
            }

            return bSuccess;
        }

        public static Task<TheThing> CreateThingFromConfigurationAsync(TheMessageAddress owner, TheThingConfiguration thingConfig)
        {
            return CreateThingFromConfigurationAsyncInternal(owner, thingConfig, new Dictionary<string, string>());
        }

        internal static async Task<TheThing> CreateThingFromConfigurationAsyncInternal(TheMessageAddress owner, TheThingConfiguration thingConfig, Dictionary<string, string> thingReferenceMap)
        {
            TheThing tThing = null;
            try
            {
                TheBaseAssets.MySYSLOG.WriteToLog(7712, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ThingService, $"Creating thing from config", eMsgLevel.l6_Debug, $"{thingConfig}"));
                await TheBaseEngine.WaitForEnginesStartedAsync();

                string thingID = MapThingIdentityWithSpecialization(thingConfig, thingReferenceMap);

                var properties = GetConfigurationPropertiesInternal(thingConfig, thingReferenceMap);

                properties.Add(new cdeP { Name = nameof(TheThing.cdePendingConfig), Value = true, cdeT = (int)ePropertyTypes.TBoolean });

                // Create the thing if needed and/or wait for it to be initialized
                tThing = await TheThingRegistry.CreateOwnedThingAsync(new TheThingRegistry.MsgCreateThingRequestV1
                {
                    OwnerAddress = owner,
                    CreateIfNotExist = true,
                    DoNotModifyIfExists = false,
                    ThingMID = thingConfig.ThingIdentity.ThingMID,
                    EngineName = thingConfig.ThingIdentity.EngineName,
                    DeviceType = thingConfig.ThingIdentity.DeviceType,
                    ID = thingID,
                    cdeProperties = properties,
                    // TODO Move to a MsgCreateThingRequestV2 that takes a TheThingReference instead of EngineName etc.?
                });

                if (tThing == null)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(7713, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Failed to create thing from config", eMsgLevel.l1_Error, $"{thingConfig}"));
                    return null;
                }

                // Ensure Config Meta is set, even if the thing itself didn't set it (yet)
                foreach (var configProp in thingConfig.ConfigurationValues)
                {
                    var prop = tThing.GetProperty(configProp.Name);
                    prop.IsConfig = true;
                    var configMeta = prop.GetConfigMeta();
                    bool bUpdated = false;
                    if (configMeta.Description == null && configProp.Description != null)
                    {
                        configMeta.Description = configProp.Description;
                        bUpdated = true;
                    }
                    if (configMeta.RangeMax == null && configProp.RangeMax != null)
                    {
                        configMeta.RangeMax = configProp.RangeMax;
                        bUpdated = true;
                    }
                    if (configMeta.RangeMin == null && configProp.RangeMin != null)
                    {
                        configMeta.RangeMin = configProp.RangeMin;
                        bUpdated = true;
                    }
                    if (configMeta.Units == null && configProp.Units != null)
                    {
                        configMeta.Units = configProp.Units;
                        bUpdated = true;
                    }
                    if (configMeta.Secure == null && configProp.Secure == true)
                    {
                        configMeta.Secure = configProp.Secure;
                        bUpdated = true;
                    }
                    if (configMeta.DefaultValue == null && configProp.DefaultValue != null)
                    {
                        configMeta.DefaultValue = configProp.DefaultValue;
                        bUpdated = true;
                    }
                    if (bUpdated)
                    {
                        prop.SetConfigMeta(configMeta);
                    }

                }

                TheBaseAssets.MySYSLOG.WriteToLog(7714, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ThingService, $"Creating thing from config: applying subscriptions", eMsgLevel.l6_Debug, $"{tThing} {thingConfig}"));
                if (!await tThing.ApplySubscriptionsInternalAsync(thingConfig, thingReferenceMap))
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(7715, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Created thing, but failed to apply subscriptions", eMsgLevel.l1_Error, $"{tThing} {thingConfig}"));
                    return null;
                }
                TheBaseAssets.MySYSLOG.WriteToLog(7716, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Created thing from config", eMsgLevel.l3_ImportantMessage, $"{tThing} {thingConfig}"));
                return tThing;
            }
            finally
            {
                tThing?.RemoveProperty(nameof(TheThing.cdePendingConfig));
            }
        }

        private static string MapThingIdentityWithSpecialization(TheThingConfiguration thingConfig, Dictionary<string, string> thingReferenceMap)
        {
            string thingID = thingConfig.ThingIdentity.ID;
            if (thingConfig.ThingSpecializationParameters != null && thingConfig.ThingSpecializationParameters.TryGetValue(nameof(ID), out var newIDObj))
            {
                var newID = TheCommonUtils.CStr(newIDObj);
                if (thingReferenceMap.TryGetValue(thingID, out var previousNewId) && previousNewId != newID)
                {
                    // internal error? Remapped the ID?
                }
                if (thingID != newID)
                {
                    thingReferenceMap[thingID] = newID;
                    thingID = newID;
                }
                else
                {
                    // Installing with the original specialized info
                }
            }

            return thingID;
        }
#endif

        #endregion

    }

    public class TheThingIdentity
    {
        public string EngineName { get; set; }
        public string DeviceType { get; set; }
        public string ID { get; set; }
        // Hint for quicker resolution
        public Guid? ThingMID { get; set; }

        public TheThingIdentity()
        {
        }
        public TheThingIdentity(TheThingIdentity thingIdentity)
        {
            if (thingIdentity != null)
            {
                EngineName = thingIdentity.EngineName;
                DeviceType = thingIdentity.DeviceType;
                ID = thingIdentity.ID;
                ThingMID = thingIdentity.ThingMID;
            }
        }

        public TheThingIdentity(TheThing tThing)
        {
            if (tThing != null)
            {
                ThingMID = tThing.cdeMID;
                EngineName = tThing.EngineName;
                DeviceType = tThing.DeviceType;
                ID = tThing.ID;
            }
        }

        public override int GetHashCode()
        {
            var hash = 13;
            AddHash(ref hash, EngineName);
            AddHash(ref hash, DeviceType);
            AddHash(ref hash, ID);
            AddHash(ref hash, ThingMID);
            return hash;
        }
        protected static void AddHash(ref int hash, object obj)
        {
            if (obj != null)
            {
                hash = hash * 7 + obj.GetHashCode();
            }
        }
        public override string ToString()
        {
            return $"{EngineName}/{DeviceType}/{ID} ({ThingMID})";
        }
    }
    public class TheThingReference : TheThingIdentity
    {
        public string FriendlyName { get; set; }
        public string Address { get; set; }
        public Dictionary<string, object> PropertiesToMatch { get; set; }

        public TheThingReference() { }
        public TheThingReference(TheThing tThing) : base(tThing)
        {
        }

        public TheThingReference(TheThingIdentity thingIdentity) : base(thingIdentity)
        {
        }

        public TheThingReference(ICDEThing iThing) : this(iThing?.GetBaseThing())
        {
        }

        public static implicit operator TheThingReference(TheThing targetThing)
        {
            if (targetThing == null)
            {
                return null;
            }
            return new TheThingReference(targetThing);
        }

        public List<TheThing> GetMatchingThings()
        {
            var things = new List<TheThing>();
            things.Add(GetThing()); // TODO do ThingFinder lookup etc.
            return things;
        }

        public TheThing GetMatchingThing()
        {
            var targetThings = GetMatchingThings();
            if (targetThings.Count() != 1)
            {
                return null;
            }
            var targetThing = targetThings.FirstOrDefault();
            return targetThing;
        }


        public string GetMatchingThingMID()
        {
            var targetThing = GetMatchingThing();
            if (targetThing == null)
            {
                return this.ThingMID?.ToString();
            }
            return targetThing?.cdeMID.ToString();
        }

        private TheThing GetThing()
        {
            TheThing tThing = null;
            if (ThingMID.HasValue)
            {
                tThing = TheThingRegistry.GetThingByMID(ThingMID.Value);
            }
            if (tThing == null)
            {
                tThing = TheThingRegistry.GetThingByID(EngineName, ID, true);
            }
            return tThing;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is TheThingReference))
            {
                return base.Equals(obj);
            }
            var r = obj as TheThingReference;
            return base.Equals(obj) && this.Address == r.Address && this.FriendlyName == r.FriendlyName
                && AreDictionariesEqual(this.PropertiesToMatch, r.PropertiesToMatch);
        }

        static bool AreDictionariesEqual(Dictionary<string, object> d1, Dictionary<string, object> d2)
        {
            // Except() enumerates and compares the KeyValuePairs in each dictionary, which includes the values
            return d1 == d2 || (d1 != null && d2?.Except(d1).Any() != true);
        }

        public override int GetHashCode()
        {
            var hash = base.GetHashCode();
            AddHash(ref hash, Address);
            AddHash(ref hash, FriendlyName);
            if (PropertiesToMatch != null && PropertiesToMatch.Count > 0)
            {
                hash = hash * 7 + PropertiesToMatch?.OrderBy(kv => kv.Key).Aggregate(0, (hc, kv) => hc * 7 + kv.GetHashCode()) ?? 0;
            }
            return hash;
        }

        public override string ToString()
        {
            return $"{base.ToString()} '{FriendlyName}' Props: {this.PropertiesToMatch?.Count}";
        }
    }

}
