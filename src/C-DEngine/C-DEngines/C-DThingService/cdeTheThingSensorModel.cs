// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System;
#if CDE_INTNEWTON
using cdeNewtonsoft.Json;
#else
using Newtonsoft.Json;
#endif

#pragma warning disable CS1591    //TODO: Remove and document public methods

namespace nsCDEngine.Engines.ThingService
{
    public partial class cdeP : TheMetaDataBase
    {
        internal const string strSensor = "cdeSensor";

        [IgnoreDataMember]
        public bool IsSensor { get { return this.GetProperty(strSensor) != null; } }

        public class TheSensorMeta
        {
            public string SourceType { get; set; }
            public string Units { get; set; }
            public string SourceUnits { get; set; }
            public double? RangeMin { get; set; }
            public double? RangeMax { get; set; }
            public double? RangeAverage { get; set; }
            public string Kind { get; set; } // Analog, binary, state ??
            public string Description { get; set; }
            public string FriendlyName { get; set; }
            public string SemanticTypes { get; set; }

            [JsonExtensionData(ReadData = true, WriteData = true)]
            public Dictionary<string, object> ExtensionData { get; set; }

            public TheSensorMeta() { }
            public TheSensorMeta(TheSensorMeta sensorMeta)
            {
                SourceType = sensorMeta.SourceType;
                Units = sensorMeta.Units;
                SourceUnits = sensorMeta.SourceUnits;
                RangeMin = sensorMeta.RangeMin;
                RangeMax = sensorMeta.RangeMax;
                RangeAverage = sensorMeta.RangeAverage;
                Kind = sensorMeta.Kind;
                Description = sensorMeta.Description;
                FriendlyName = sensorMeta.FriendlyName;
                SemanticTypes = sensorMeta.SemanticTypes;
                if (sensorMeta.ExtensionData != null)
                {
                    ExtensionData = new Dictionary<string, object>(sensorMeta.ExtensionData);
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
                            nameof(TheSensorMeta.RangeMin),
                            nameof(TheSensorMeta.RangeMax),
                            nameof(TheSensorMeta.RangeAverage),
                            nameof(TheSensorMeta.Kind),
                            nameof(TheSensorMeta.SourceType),
                            nameof(TheSensorMeta.SourceUnits),
                            nameof(TheSensorMeta.Units),
                            nameof(TheSensorMeta.Description),
                            nameof(TheSensorMeta.SemanticTypes),
                            nameof(TheSensorMeta.FriendlyName),
                        };
                    }
                    return _knownProperties;
                }
            }

        }

        public TheSensorMeta GetSensorMeta()
        {
            var sensorMetaProp = this.GetProperty(strSensor);
            var rangeMinProp = sensorMetaProp?.GetProperty(nameof(TheSensorMeta.RangeMin));
            var rangeMaxProp = sensorMetaProp?.GetProperty(nameof(TheSensorMeta.RangeMax));
            var rangeAvgProp = sensorMetaProp?.GetProperty(nameof(TheSensorMeta.RangeAverage));
            var sensorMeta = new TheSensorMeta
            {
                SourceType = TheCommonUtils.CStrNullable(sensorMetaProp?.GetProperty(nameof(TheSensorMeta.SourceType))),
                Units = TheCommonUtils.CStrNullable(sensorMetaProp?.GetProperty(nameof(TheSensorMeta.Units))),
                SourceUnits = TheCommonUtils.CStrNullable(sensorMetaProp?.GetProperty(nameof(TheSensorMeta.SourceUnits))),
                RangeMin = rangeMinProp != null ? (double?)TheCommonUtils.CDbl(rangeMinProp) : null,
                RangeMax = rangeMaxProp != null ? (double?)TheCommonUtils.CDbl(rangeMaxProp) : null,
                RangeAverage = rangeAvgProp != null ? (double?)TheCommonUtils.CDbl(rangeAvgProp) : null,
                Kind = TheCommonUtils.CStrNullable(sensorMetaProp?.GetProperty(nameof(TheSensorMeta.Kind))),
                Description = TheCommonUtils.CStrNullable(sensorMetaProp?.GetProperty(nameof(TheSensorMeta.Description))),
                FriendlyName = TheCommonUtils.CStrNullable(sensorMetaProp?.GetProperty(nameof(TheSensorMeta.FriendlyName))),
                SemanticTypes = TheCommonUtils.CStrNullable(sensorMetaProp?.GetProperty(nameof(TheSensorMeta.SemanticTypes))),
                ExtensionData = ReadDictionaryFromProperties(sensorMetaProp, TheSensorMeta.KnownProperties),
            };
            return sensorMeta;
        }

        public void SetSensorMeta(TheSensorMeta value)
        {
            var sensorMetaProp = this.GetProperty(strSensor, true);
            if (!string.IsNullOrEmpty(value.SourceType))
            {
                sensorMetaProp.SetProperty(nameof(TheSensorMeta.SourceType), value.SourceType, ePropertyTypes.TString);
            }
            else
            {
                sensorMetaProp.RemoveProperty(nameof(TheSensorMeta.SourceType));
            }
            if (!string.IsNullOrEmpty(value.Units))
            {
                sensorMetaProp.SetProperty(nameof(TheSensorMeta.Units), value.Units, ePropertyTypes.TString);
            }
            else
            {
                sensorMetaProp.RemovePropertiesStartingWith(nameof(TheSensorMeta.Units));
            }
            if (!string.IsNullOrEmpty(value.SourceUnits))
            {
                sensorMetaProp.SetProperty(nameof(TheSensorMeta.SourceUnits), value.SourceUnits, ePropertyTypes.TString);
            }
            else
            {
                sensorMetaProp.RemoveProperty(nameof(TheSensorMeta.SourceUnits));
            }
            if (value.RangeMin.HasValue)
            {
                sensorMetaProp.SetProperty(nameof(TheSensorMeta.RangeMin), value.RangeMin, ePropertyTypes.TNumber);
            }
            else
            {
                sensorMetaProp.RemoveProperty(nameof(TheSensorMeta.RangeMin));
            }
            if (value.RangeMin.HasValue)
            {
                sensorMetaProp.SetProperty(nameof(TheSensorMeta.RangeMax), value.RangeMax, ePropertyTypes.TNumber);
            }
            else
            {
                sensorMetaProp.RemoveProperty(nameof(TheSensorMeta.RangeMax));
            }
            if (value.RangeAverage.HasValue)
            {
                sensorMetaProp.SetProperty(nameof(TheSensorMeta.RangeAverage), value.RangeAverage, ePropertyTypes.TNumber);
            }
            else
            {
                sensorMetaProp.RemoveProperty(nameof(TheSensorMeta.RangeAverage));
            }
            if (!string.IsNullOrEmpty(value.Kind))
            {
                sensorMetaProp.SetProperty(nameof(TheSensorMeta.Kind), value.Kind, ePropertyTypes.TString);
            }
            else
            {
                sensorMetaProp.RemoveProperty(nameof(TheSensorMeta.Kind));
            }
            if (!string.IsNullOrEmpty(value.Description))
            {
                sensorMetaProp.SetProperty(nameof(TheSensorMeta.Description), value.Description, ePropertyTypes.TString);
            }
            else
            {
                sensorMetaProp.RemoveProperty(nameof(TheSensorMeta.Description));
            }
            if (!string.IsNullOrEmpty(value.FriendlyName))
            {
                sensorMetaProp.SetProperty(nameof(TheSensorMeta.FriendlyName), value.FriendlyName, ePropertyTypes.TString);
            }
            else
            {
                sensorMetaProp.RemoveProperty(nameof(TheSensorMeta.FriendlyName));
            }
            if (!string.IsNullOrEmpty(value.SemanticTypes))
            {
                sensorMetaProp.SetProperty(nameof(TheSensorMeta.SemanticTypes), value.SemanticTypes, ePropertyTypes.TString);
            }
            else
            {
                sensorMetaProp.RemoveProperty(nameof(TheSensorMeta.SemanticTypes));
            }
            if (value.ExtensionData != null)
            {
                sensorMetaProp.SetProperties(value.ExtensionData, DateTimeOffset.MinValue);
            }

            var ownerThing = this.OwnerThing.GetBaseThing();
            if (ownerThing != null)
            {
                if (!ownerThing.Capabilities.Contains(eThingCaps.SensorContainer))
                {
                    ownerThing.AddCapability(eThingCaps.SensorContainer);
                }
            }
        }


    }

    public sealed partial class TheThing : TheMetaDataBase, ICDEThing
    {
        #region Sensor Meta-data, Source and Target management

        /// <summary>
        /// Returns all properties that are marked as Sensors.
        /// </summary>
        /// <returns></returns>
        public List<cdeP> GetSensorProperties()
        {
            return GetAllProperties(10)?.Where(p => p.IsSensor== true)?.ToList();
        }
        /// <summary>
        /// Returns all properties that are marked as Sensors.
        /// </summary>
        /// <returns></returns>
        public List<cdeP> GetConfigProperties()
        {
            return GetAllProperties(10)?.Where(p => p.IsConfig == true)?.ToList();
        }

        /// <summary>
        /// Describes instance independent meta-data about a property, including sensor meta data.
        /// </summary>
        public class TheSensorPropertyMeta : cdeP.TheSensorMeta
        {
            public TheSensorPropertyMeta() : base() { }
            public TheSensorPropertyMeta(cdeP.TheSensorMeta sensorMeta) : base(sensorMeta) { }

            public TheSensorPropertyMeta(string name, Type type, SensorPropertyAttribute sensorAttribute)
            {
                if (string.IsNullOrEmpty(sensorAttribute.NameOverride))
                {
                    Name = name;
                }
                else
                {
                    Name = sensorAttribute.NameOverride;
                }
                if (sensorAttribute.cdeT != ePropertyTypes.NOCHANGE)
                {
                    cdeT = sensorAttribute.cdeT;
                }
                else if (type != null)
                {
                    var mappedType = cdeP.GetCDEType(type);
                    if (mappedType != ePropertyTypes.NOCHANGE)
                    {
                        cdeT = mappedType;
                    }
                }

                SourceType = sensorAttribute.SourceType;
                Units = sensorAttribute.Units;
                SourceUnits = sensorAttribute.SourceUnits;
                if (sensorAttribute.RangeMin != 0)
                {
                    RangeMin = sensorAttribute.RangeMin;
                }
                if (sensorAttribute.RangeMax != 0)
                {
                    RangeMax = sensorAttribute.RangeMax;
                }
                if (sensorAttribute.RangeAverage != 0)
                {
                    RangeMax = sensorAttribute.RangeAverage;
                }
                Kind = sensorAttribute.Kind;
                Description = sensorAttribute.Description;
                FriendlyName = sensorAttribute.FriendlyName;
                SemanticTypes = sensorAttribute.SemanticTypes;
            }

            public string Name;
            public ePropertyTypes cdeT;
            public int cdeA;
        }
        /// <summary>
        /// Describes instance-dependent meta-data about a property, including potential provider info
        /// </summary>
        public class TheSensorInstancePropertyMeta : TheSensorPropertyMeta
        {
            public TheSensorInstancePropertyMeta() : base() { }
            public TheSensorInstancePropertyMeta(cdeP.TheSensorMeta sensorMeta) : base(sensorMeta) { }

            public cdeP.TheProviderInfo ProviderInfo;
            // TODO Do we need/want other info like cdeAVA, cdeM here as well?
        }

        public List<TheSensorInstancePropertyMeta> GetSensorPropertyMetaData()
        {
            var sensorProps = GetSensorProperties();
            return sensorProps.Select(sp =>
            {
                var sensorPropertyMeta = new TheSensorInstancePropertyMeta(sp.GetSensorMeta())
                {
                    Name = sp.Name,
                    cdeT = (ePropertyTypes)sp.cdeT,
                    cdeA = sp.cdeA,
                    // TODO Are there any other cdeP fields that are instance-independent meta data?
                    ProviderInfo = sp.GetSensorProviderInfo(),
                };
                return sensorPropertyMeta;
            }).ToList();
        }

        public cdeP DeclareSensorProperty(string propertyName, ePropertyTypes propertyType, cdeP.TheSensorMeta sensorMeta)
        {
            cdeP tProp = GetProperty(propertyName, true);
            if (tProp.cdeO != cdeMID)
                tProp.cdeO = cdeMID;
            tProp.cdeE |= 0x04; // cdeCTIM should be updated with changetime
            tProp.SetSensorMeta(sensorMeta);
            return tProp;
        }
        internal void DeclareSensorProperty(TheSensorPropertyMeta sensorProperty)
        {
            DeclareSensorProperty(sensorProperty.Name, sensorProperty.cdeT, sensorProperty);
        }

        #endregion

    }

}
