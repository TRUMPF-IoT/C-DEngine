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
using System.Threading.Tasks;

#if CDE_INTNEWTON
using cdeNewtonsoft.Json;
#else
using Newtonsoft.Json;
#endif

// ReSharper disable All
#pragma warning disable CS1591    //TODO: Remove and document public methods

namespace nsCDEngine.Engines.ThingService
{
    public partial class cdeP : TheMetaDataBase
    {

        public class TheProviderInfo
        {
            [IgnoreDataMember]
            public TheThing Provider
            {
                get
                {
                    return TheThingRegistry.GetThingByMID(ProviderMid ?? Guid.Empty, true);
                }
                set
                {
                    ProviderMid = value?.cdeMID ?? Guid.Empty;
                }
            }

            public Guid? ProviderMid;

            public TheThing.TheSensorSubscription Subscription;

            public TheProviderInfo() { }
            public TheProviderInfo(TheThing providerThing, TheThing.TheSensorSubscription subscription)
            {
                Provider = providerThing;
                Subscription = subscription;
            }

            static HashSet<string> _knownProperties;
            [IgnoreDataMember]
            static internal HashSet<string> KnownProperties
            {
                get
                {
                    _knownProperties ??= new HashSet<string>
                    {
                        nameof(TheThing.TheSensorSubscription.SensorId),
                        nameof(TheThing.TheSensorSubscription.SampleRate),
                        nameof(TheThing.TheSensorSubscription.TargetThing),
                        nameof(TheThing.TheSensorSubscription.TargetProperty),
                        nameof(TheThing.TheSensorSubscription.SubscriptionId),
                        nameof(ProviderMid),
                    };
                    return _knownProperties;
                }
            }


        }

        internal const string strSource = "cdeSource";

        public TheProviderInfo GetSensorProviderInfo()
        {
            var providerInfoProp = this.GetProperty(strSource);
            var subscriptionId = TheCommonUtils.CGuid(providerInfoProp?.GetProperty(nameof(TheThing.TheSensorSubscription.SubscriptionId), false));

            var providerInfo = new TheProviderInfo
            {
                ProviderMid = TheCommonUtils.CGuidNullable(providerInfoProp?.GetProperty(nameof(TheProviderInfo.ProviderMid), false)),

                Subscription = new TheThing.TheSensorSubscription
                {
                    SensorId = TheCommonUtils.CStrNullable(providerInfoProp?.GetProperty(nameof(TheThing.TheSensorSubscription.SensorId), false)),
                    SampleRate = TheCommonUtils.CIntNullable(providerInfoProp?.GetProperty(nameof(TheThing.TheSensorSubscription.SampleRate), false)),
                    SubscriptionId = subscriptionId != Guid.Empty ? subscriptionId : this.cdeMID,
                    TargetThing = new TheThingReference(this.OwnerThing),
                    TargetProperty = cdeP.GetPropertyPath(this),
                    ExtensionData = ReadDictionaryFromProperties(providerInfoProp, TheProviderInfo.KnownProperties),
                }
            };
            return providerInfo;
        }

        public void SetSensorProviderInfo(TheProviderInfo value)
        {
            if (value == null)
            {
                this.RemoveProperty(strSource);
                return;
            }
            var providerInfoProp = this.GetProperty(strSource, true);
            if (value.ProviderMid.HasValue)
            {
                providerInfoProp.SetProperty(nameof(TheProviderInfo.ProviderMid), value.ProviderMid.Value, ePropertyTypes.TGuid);
            }
            else
            {
                providerInfoProp.RemoveProperty(nameof(TheProviderInfo.ProviderMid));
            }
            if (value.Subscription != null)
            {
                var subscription = value.Subscription;
                if (!string.IsNullOrEmpty(subscription.SensorId))
                {
                    providerInfoProp.SetProperty(nameof(TheThing.TheSensorSubscription.SensorId), subscription.SensorId, ePropertyTypes.TString);
                }
                else
                {
                    providerInfoProp.RemoveProperty(nameof(TheThing.TheSensorSubscription.SensorId));
                }
                if (subscription.SampleRate.HasValue)
                {
                    providerInfoProp.SetProperty(nameof(TheThing.TheSensorSubscription.SampleRate), subscription.SampleRate.Value, ePropertyTypes.TNumber);
                }
                else
                {
                    providerInfoProp.RemoveProperty(nameof(TheThing.TheSensorSubscription.SampleRate));
                }
                if (subscription.SubscriptionId.HasValue && subscription.SubscriptionId.Value != Guid.Empty && subscription.SubscriptionId.Value != this.cdeMID)
                {
                    providerInfoProp.SetProperty(nameof(TheThing.TheSensorSubscription.SubscriptionId), subscription.SubscriptionId.Value, ePropertyTypes.TGuid);
                }

                var extensionData = subscription?.GetBaseSubscription()?.ExtensionData; // Make sure any state in derived classes is normalized (into ExtensionData)
                if (extensionData != null)
                {
                    // TODO: cleanup deleted properties?
                    providerInfoProp.SetProperties(extensionData, DateTimeOffset.MinValue);
                }
            }
        }

    }

    public sealed partial class TheThing : TheMetaDataBase, ICDEThing
    {
        #region Sensor Meta-data, Source and Target management

        /// <summary>
        /// Returns all properties that are marked as coming from a sensor provider.
        /// </summary>
        /// <returns></returns>
        public List<cdeP> GetSensorProviderProperties()
        {
            return GetAllProperties(10).Where(p => p.GetProperty(cdeP.strSource) != null).ToList();
        }

        #endregion

        #region Sensor Provider


        /// <summary>
        /// Captures the information about a sensor as needed by the provider, independent of a particular subscription
        /// </summary>
        public class TheSensorSourceInfo
        {
            /// <summary>
            /// Uniquely identifies the source of the sensor within the provider's scope (i.e. an OPC UA node id).
            /// </summary>
            public string SensorId { get; set; }
            /// <summary>
            /// A path of display names for user display: in the provider, there may be more than one such path for a given sensor id.
            /// </summary>
            public string[] DisplayNamePath { get; set; }
            /// <summary>
            /// The type of the sensor in the provider's scope. Type system here is entirely defined by the provider.
            /// </summary>
            public string SourceType { get; set; }
            /// <summary>
            /// The default cdeT type that this sensor should be mapped to
            /// </summary>
            public ePropertyTypes? cdeType { get; set; }

            /// <summary>
            /// Contains additional, provider specific meta data about the sensor.
            /// </summary>
            [JsonExtensionData(ReadData = true, WriteData = true)]
            public Dictionary<string, object> ExtensionData { get; set; }

            public TheSensorSourceInfo() { }

            public TheSensorSourceInfo(TheSensorSourceInfo sensorInfo)
            {
                SensorId = sensorInfo.SensorId;
                DisplayNamePath = sensorInfo.DisplayNamePath;
                SourceType = sensorInfo.SourceType;
                cdeType = sensorInfo.cdeType;
                if (sensorInfo.ExtensionData != null)
                {
                    ExtensionData = new Dictionary<string, object>(sensorInfo.ExtensionData);
                }
            }

        }

        public Task<MsgBrowseSensorsResponse> ProviderBrowseSensorsAsync()
        {
            return ProviderBrowseSensorsAsync(new TheThing.MsgBrowseSensors(), false);
        }
        public Task<MsgBrowseSensorsResponse> ProviderBrowseSensorsAsync(TheThing.MsgBrowseSensors browseRequest)
        {
            return ProviderBrowseSensorsAsync(browseRequest, false);
        }
        public Task<MsgBrowseSensorsResponse> ProviderBrowseSensorsAsync(TheThing.MsgBrowseSensors browseRequest, bool bypassCapabilityCheck)
        {
            if (!bypassCapabilityCheck && !this.Capabilities.Contains(eThingCaps.SensorProvider))
            {
                return TheCommonUtils.TaskFromResult(new MsgBrowseSensorsResponse { Error = "Thing is not a sensor provider" });
            }
            var browseResponseTask = TheCommRequestResponse.PublishRequestJSonAsync<TheThing.MsgBrowseSensors, TheThing.MsgBrowseSensorsResponse>(this, browseRequest);
            return browseResponseTask;
        }

        public class MsgBrowseSensors
        {
            public object Filter { get; set; } // TODO are there generic filters we could offer/require?
        }

        public class MsgBrowseSensorsResponse
        {
            public List<TheSensorSourceInfo> Sensors { get; set; }
            public string Error { get; set; }
        }

        public class TheSensorSubscription
        {
            public Guid? SubscriptionId { get; set; } // allocated by the provider/plug-in. Must be durable.
            public string SensorId { get; set; }
            public int? SampleRate { get; set; }
            public TheThingReference TargetThing { get; set; } // can be null on subscribe request (defaults to reference in the subscribe message)
            public string TargetProperty { get; set; }

            [JsonExtensionData(ReadData = true, WriteData = true)]
            public Dictionary<string, object> ExtensionData { get; set; }

            public TheSensorSubscription() { }

            public TheSensorSubscription(TheSensorSubscription sensorInfo) : this(sensorInfo, true)
            {
            }
            public TheSensorSubscription(TheSensorSubscription sensorInfo, bool normalizeExtensionData)
            {
                this.SubscriptionId = sensorInfo.SubscriptionId;
                this.SensorId = sensorInfo.SensorId;
                this.SampleRate = sensorInfo.SampleRate;
                this.TargetThing = sensorInfo.TargetThing;
                this.TargetProperty = sensorInfo.TargetProperty;
                var extensionData = normalizeExtensionData ? sensorInfo.GetBaseSubscription()?.ExtensionData : sensorInfo.ExtensionData;
                if (extensionData != null)
                {
                    this.ExtensionData = new Dictionary<string, object>(extensionData);
                }
            }

            /// <summary>
            /// Places any additional properties of a derived class into TheThing.TheSensorSubscription.ExtensionData
            /// </summary>
            /// <returns></returns>
            public virtual TheThing.TheSensorSubscription GetBaseSubscription()
            {
                if (this.GetType().IsSubclassOf(typeof(TheThing.TheSensorSubscription)))
                {
                    var baseJson = TheCommonUtils.SerializeObjectToJSONString(this);
                    var baseSubscription = TheCommonUtils.DeserializeJSONStringToObject<TheThing.TheSensorSubscription>(baseJson);
                    return baseSubscription;
                }
                return this;
            }

            internal static void SpecializeSensorSubscription(TheSensorSubscription answerSub, TheSensorSubscription generalizedSub)
            {
                if (answerSub.SubscriptionId != null)
                {
                    generalizedSub.SubscriptionId = answerSub.SubscriptionId;
                }
                if (answerSub.ExtensionData != null)
                {
                    foreach (var prop in answerSub.ExtensionData)
                    {
                        generalizedSub.ExtensionData[prop.Key] = prop.Value;
                    }
                }
                if (answerSub.SampleRate != null)
                {
                    generalizedSub.SampleRate = answerSub.SampleRate;
                }
                if (answerSub.SensorId != null)
                {
                    generalizedSub.SensorId = answerSub.SensorId;
                }
                if (answerSub.TargetProperty != null)
                {
                    generalizedSub.TargetProperty = answerSub.TargetProperty;
                }
                if (answerSub.TargetThing != null)
                {
                    generalizedSub.TargetThing = answerSub.TargetThing;
                }
            }



        }

        public class TheSensorSubscriptionStatus : TheSensorSubscriptionStatus<TheSensorSubscription>
        {
        }

        public class TheSensorSubscriptionStatus<subscriptionT> where subscriptionT : TheSensorSubscription
        {
            public string Error { get; set; } // null/empty = success

            public subscriptionT Subscription { get; set; }

            public TheSensorSubscriptionStatus()
            {
            }
            public TheSensorSubscriptionStatus(TheSensorSubscriptionStatus<subscriptionT> subscriptionStatus)
            {
                this.Subscription = subscriptionStatus.Subscription;
                this.Error = subscriptionStatus.Error;
            }
        }

        public Task<MsgSubscribeSensorsResponse> SubscribeSensorsAsync(MsgSubscribeSensors subscribeRequest)
        {
            return SubscribeSensorsAsync(subscribeRequest, false);
        }
        public Task<MsgSubscribeSensorsResponse> SubscribeSensorsAsync(MsgSubscribeSensors subscribeRequest, bool bypassCapabilityCheck)
        {
            if (!bypassCapabilityCheck && !this.Capabilities.Contains(eThingCaps.SensorProvider))
            {
                return TheCommonUtils.TaskFromResult(new MsgSubscribeSensorsResponse { Error = "Thing is not a sensor provider" });
            }
            var subscribeResponseTask = TheCommRequestResponse.PublishRequestJSonAsync<TheThing.MsgSubscribeSensors, TheThing.MsgSubscribeSensorsResponse>(this, subscribeRequest);
            return subscribeResponseTask;
        }

        public Task<MsgSubscribeSensorsResponse<subscriptionT>> SubscribeSensorsAsync<subscriptionT>(MsgSubscribeSensors<subscriptionT> subscribeRequest) where subscriptionT : TheSensorSubscription
        {
            return SubscribeSensorsAsync(subscribeRequest, false);
        }

        public Task<MsgSubscribeSensorsResponse<subscriptionT>> SubscribeSensorsAsync<subscriptionT>(MsgSubscribeSensors<subscriptionT> subscribeRequest, bool bypassCapabilityCheck) where subscriptionT : TheSensorSubscription
        {
            if (!bypassCapabilityCheck && !this.Capabilities.Contains(eThingCaps.SensorProvider))
            {
                return TheCommonUtils.TaskFromResult(new MsgSubscribeSensorsResponse<subscriptionT> { Error = "Thing is not a sensor provider" });
            }
            var subscribeResponseTask = TheCommRequestResponse.PublishRequestJSonAsync<TheThing.MsgSubscribeSensors<subscriptionT>, TheThing.MsgSubscribeSensorsResponse<subscriptionT>>(this, subscribeRequest);
            return subscribeResponseTask;
        }

        public class MsgSubscribeSensors
        {
            public TheThingReference DefaultTargetThing { get; set; }
            public List<TheSensorSubscription> SubscriptionRequests { get; set; }
            public bool ReplaceAll { get; set; }

            public static implicit operator MsgSubscribeSensors<TheSensorSubscription>(MsgSubscribeSensors msg)
            {
                return new MsgSubscribeSensors<TheSensorSubscription>
                {
                    DefaultTargetThing = msg.DefaultTargetThing,
                    ReplaceAll = msg.ReplaceAll,
                    SubscriptionRequests = msg.SubscriptionRequests,
                };
            }

        }

        public class MsgSubscribeSensors<T> where T : TheSensorSubscription
        {
            public TheThingReference DefaultTargetThing { get; set; }
            public List<T> SubscriptionRequests { get; set; }
            public bool ReplaceAll { get; set; }
        }

        public class MsgSubscribeSensorsResponse
        {
            public List<TheSensorSubscriptionStatus> SubscriptionStatus { get; set; }
            public string Error { get; set; }

            public string GetSubscriptionErrors()
            {
                return SubscriptionStatus?.Where(status => !string.IsNullOrEmpty(status.Error)).Aggregate("", (s, status) => $"{s}{status.Subscription.SubscriptionId}/{status.Subscription.SensorId}:{status.Error},").TrimEnd(',');
            }
            public static implicit operator MsgSubscribeSensorsResponse(MsgSubscribeSensorsResponse<TheSensorSubscription> msg)
            {
                return new MsgSubscribeSensorsResponse
                {
                    Error = msg.Error,
                    SubscriptionStatus = msg.SubscriptionStatus.Select(st => new TheSensorSubscriptionStatus { Error = st.Error, Subscription = st.Subscription }).ToList(),
                };
            }
        }

        public class MsgSubscribeSensorsResponse<T> : IMsgResponse where T : TheSensorSubscription
        {
            public List<TheSensorSubscriptionStatus<T>> SubscriptionStatus { get; set; }
            public string Error { get; set; }

            public string GetSubscriptionErrors()
            {
                return SubscriptionStatus?.Where(status => !string.IsNullOrEmpty(status.Error)).Aggregate("", (s, status) => $"{s}{status.Subscription.SubscriptionId}/{status.Subscription.SensorId}:{status.Error},").TrimEnd(',');
            }

        }

        public Task<MsgUnsubscribeSensorsResponse> UnsubscribeSensorsAsync(MsgUnsubscribeSensors unsubscribeRequest)
        {
            return UnsubscribeSensorsAsync(unsubscribeRequest, false);
        }

        public Task<MsgUnsubscribeSensorsResponse> UnsubscribeSensorsAsync(MsgUnsubscribeSensors unsubscribeRequest, bool bypassCapabilityCheck)
        {
            if (!bypassCapabilityCheck && !this.Capabilities.Contains(eThingCaps.SensorProvider))
            {
                return TheCommonUtils.TaskFromResult(new MsgUnsubscribeSensorsResponse { Error = "Thing is not a sensor provider" });
            }
            var unsubscribeResponseTask = TheCommRequestResponse.PublishRequestJSonAsync<MsgUnsubscribeSensors, MsgUnsubscribeSensorsResponse>(this, unsubscribeRequest);
            return unsubscribeResponseTask;
        }


        public class MsgUnsubscribeSensors
        {
            public List<Guid> SubscriptionIds { get; set; } // TODO Is this sufficient or do we want to allow unsubscribe by other sensor parameters (i.e. displayname)?
            public bool UnsubscribeAll { get; set; }
        }

        public class MsgUnsubscribeSensorsResponse : IMsgResponse
        {
            /// <summary>
            /// Subscriptions that failed to unsubscribe, only SubscriptionId and Error must be populated. Other fields are optional, but if any are present they must be complete.
            /// </summary>
            public List<TheSensorSubscriptionStatus> Failed { get; set; }
            public string Error { get; set; }
        }

        public Task<MsgGetSensorSubscriptionsResponse> GetSensorProviderSubscriptionsAsync()
        {
            return GetSensorProviderSubscriptionsAsync(new MsgGetSensorSubscriptions(), false);
        }
        public Task<MsgGetSensorSubscriptionsResponse> GetSensorProviderSubscriptionsAsync(MsgGetSensorSubscriptions getSubscriptionsRequest)
        {
            return GetSensorProviderSubscriptionsAsync(getSubscriptionsRequest, false);
        }
        public Task<MsgGetSensorSubscriptionsResponse> GetSensorProviderSubscriptionsAsync(MsgGetSensorSubscriptions getSubscriptionsRequest, bool bypassCapabilityCheck)
        {

            if (!bypassCapabilityCheck && !this.Capabilities.Contains(eThingCaps.SensorProvider))
            {
                return TheCommonUtils.TaskFromResult(new MsgGetSensorSubscriptionsResponse { Error = "Thing is not a sensor provider" });
            }
            var getSubscriptionResponseTask = TheCommRequestResponse.PublishRequestJSonAsync<MsgGetSensorSubscriptions, MsgGetSensorSubscriptionsResponse>(this, getSubscriptionsRequest);
            return getSubscriptionResponseTask;
        }

        public class MsgGetSensorSubscriptions : MsgGetSensorSubscriptions<TheSensorSubscription> { }
        public class MsgGetSensorSubscriptions<subscriptionT> where subscriptionT : TheSensorSubscription
        {
            // TODO: Filtering?
        }
        public class MsgGetSensorSubscriptionsResponse : MsgGetSensorSubscriptionsResponse<TheSensorSubscription> { }
        public class MsgGetSensorSubscriptionsResponse<subscriptionT> where subscriptionT : TheSensorSubscription
        {
            public List<subscriptionT> Subscriptions { get; set; }
            public string Error { get; set; }
        }

        #endregion

    }

}
