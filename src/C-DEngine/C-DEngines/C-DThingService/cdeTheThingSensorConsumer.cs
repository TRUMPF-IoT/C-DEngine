// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using nsCDEngine.BaseClasses;

#if CDE_INTNEWTON
using cdeNewtonsoft.Json;
#else
using Newtonsoft.Json;
#endif

#pragma warning disable CS1591    //TODO: Remove and document public methods


namespace nsCDEngine.Engines.ThingService
{
    public sealed partial class TheThing : TheMetaDataBase, ICDEThing
    {
        #region Sensor Consumer contracts

        public class MsgGetThingSubscriptions
        {
            public bool? Generalize;
        }

        public class MsgGetThingSubscriptionsResponse : IMsgResponse
        {
            public List<TheThingSubscription> ThingSubscriptions { get; set; }
            public string Error { get; set; }
        }
        public class MsgGetThingSubscriptionsResponse<TSubscription> : IMsgResponse where TSubscription : TheThingSubscription
        {
            public List<TSubscription> ThingSubscriptions { get; set; }
            public string Error { get; set; }
        }

        public class TheThingSubscription
        {
            /// <summary>
            /// The cdeMID of the subscription
            /// In some case a server can expose the same TheThing multiple times, with different configurations, which is why this cdeMID is usually different from the ThingMID.
            /// </summary>
            public Guid? SubscriptionId { get; set; }
            public TheThingReference ThingReference { get; set; }
            /// <summary>
            /// If no ThingMID is specified, all things that match on ThingAddress.EngineName and the Properties in PropertiesToMatch will be added.
            /// If ContinueMatching = false: the match is performed only once and any new things that get created later will not be included
            /// If ContinueMatching = true, any new things will get added as they appear.
            /// </summary>
            public bool? ContinueMatching { get; set; }

            /// <summary>
            /// Replace any existing entries of ThingMID in the server's list of things. Used for simple cases where exactly one thing is to be exposed in the server (avoids creating/maintaining a cdeMID for the entry).
            /// </summary>
            public bool? ReplaceExistingThing { get; set; }

            [JsonExtensionData(ReadData = true, WriteData = true)]
            public Dictionary<string, object> ExtensionData { get; set; }
            public object GetValueFromExtensionData(string name)
            {
                object value = null;
                ExtensionData?.TryGetValue(name, out value);
                return value;
            }

            // TODO Test that this works with object, or does it require JToken?
            // TODO Test that this works properly with derived classes (i.e. known properties in a derived class don't get added to the extension data)

            /// <summary>
            /// If the thing supports the sensor model, only sensors will be consumed (unless otherwise filtered out, i.e. PropertiesToExclude). This flag forces all properties to be considered, not just sensors
            /// </summary>
            public bool? ForceAllProperties { get; set; }
            /// <summary>
            /// If the thing supports the sensor model, only sensors will be consumed (unless otherwise filtered out, i.e. PropertiesToExclude). This flag forces all configuration properties to be considered, not just sensors
            /// </summary>
            public bool? ForceConfigProperties { get; set; }


            // Subscription using Thing Historian
            public uint? SamplingWindow { get; set; }
            public uint? CooldownPeriod { get; set; }
            public bool? SendUnchangedValue { get; set; }
            public bool? SendInitialValues { get; set; }
            public bool? IgnoreExistingHistory { get; set; }
            public uint? TokenExpirationInHours { get; set; }
            public List<string> PropertiesIncluded { get; set; }
            public List<string> PropertiesExcluded { get; set; }
            public Dictionary<string, object> StaticProperties { get; set; }
            public bool? KeepDurableHistory { get; set; }
            public uint? MaxHistoryTime { get; set; }
            public int? MaxHistoryCount { get; set; }

            // Subscriptions that need to be converted to another format, protocol etc.

            public string EventFormat { get; set; }
            public bool? PreserveOrder { get; set; }
            public bool? IgnorePartialFailure { get; set; }
            public bool? AddThingIdentity { get; set; }
            public string TargetType { get; set; }
            public string TargetName { get; set; }
            public string TargetUnit { get; set; }
            public string PartitionKey { get; set; }

            internal static void SpecializeThingSubscription(TheThingSubscription answerSub, TheThingSubscription generalizedSub)
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
                if (answerSub.PropertiesIncluded != null)
                {
                    generalizedSub.PropertiesIncluded = answerSub.PropertiesIncluded;
                }
                if (answerSub.PropertiesExcluded != null)
                {
                    generalizedSub.PropertiesExcluded = answerSub.PropertiesExcluded;
                }
                if (answerSub.AddThingIdentity != null)
                {
                    generalizedSub.AddThingIdentity = answerSub.AddThingIdentity;
                }
                if (answerSub.ContinueMatching != null)
                {
                    generalizedSub.ContinueMatching = answerSub.ContinueMatching;
                }
                if (answerSub.CooldownPeriod != null)
                {
                    generalizedSub.CooldownPeriod = answerSub.CooldownPeriod;
                }
                if (answerSub.EventFormat != null)
                {
                    generalizedSub.EventFormat = answerSub.EventFormat;
                }
                if (answerSub.ForceAllProperties != null)
                {
                    generalizedSub.ForceAllProperties = answerSub.ForceAllProperties;
                }
                if (answerSub.IgnoreExistingHistory != null)
                {
                    generalizedSub.IgnoreExistingHistory = answerSub.IgnoreExistingHistory;
                }
                if (answerSub.IgnorePartialFailure != null)
                {
                    generalizedSub.IgnorePartialFailure = answerSub.IgnorePartialFailure;
                }
                if (answerSub.KeepDurableHistory != null)
                {
                    generalizedSub.KeepDurableHistory = answerSub.KeepDurableHistory;
                }
                if (answerSub.MaxHistoryCount != null)
                {
                    generalizedSub.MaxHistoryCount = answerSub.MaxHistoryCount;
                }
                if (answerSub.MaxHistoryTime != null)
                {
                    generalizedSub.MaxHistoryTime = answerSub.MaxHistoryTime;
                }
                if (answerSub.PartitionKey != null)
                {
                    generalizedSub.PartitionKey = answerSub.PartitionKey;
                }
                if (answerSub.PreserveOrder != null)
                {
                    generalizedSub.PreserveOrder = answerSub.PreserveOrder;
                }
                if (answerSub.ReplaceExistingThing != null)
                {
                    generalizedSub.ReplaceExistingThing = answerSub.ReplaceExistingThing;
                }
                if (answerSub.SamplingWindow != null)
                {
                    generalizedSub.SamplingWindow = answerSub.SamplingWindow;
                }
                if (answerSub.SendInitialValues != null)
                {
                    generalizedSub.SendInitialValues = answerSub.SendInitialValues;
                }
                if (answerSub.SendUnchangedValue != null)
                {
                    generalizedSub.SendUnchangedValue = answerSub.SendUnchangedValue;
                }
                if (answerSub.StaticProperties != null)
                {
                    generalizedSub.StaticProperties = answerSub.StaticProperties;
                }
                if (answerSub.TargetName != null)
                {
                    generalizedSub.TargetName = answerSub.TargetName;
                }
                if (answerSub.TargetType != null)
                {
                    generalizedSub.TargetType = answerSub.TargetType;
                }
                if (answerSub.TargetUnit != null)
                {
                    generalizedSub.TargetUnit = answerSub.TargetUnit;
                }
                if (answerSub.ThingReference != null)
                {
                    generalizedSub.ThingReference = answerSub.ThingReference;
                }
                if (answerSub.TokenExpirationInHours != null)
                {
                    generalizedSub.TokenExpirationInHours = answerSub.TokenExpirationInHours;
                }
            }
        }

        public class TheThingProcessorSubscription : TheThingSubscription
        {
            public TheSensorSubscription TargetSensor;
        }

        //public class TheThingProcessor : TheThingSubscription
        //{
        //    public string ProcessorId;
        //    public List<TheThingReference> Sources;
        //    public TheThingReference Target;
        //    public string TargetProperty;
        //    [cdeNewtonsoft.Json.JsonExtensionData(ReadData = true, WriteData = true)]
        //    public Dictionary<string, object> ExtensionData { get; set; }
        //    public object GetValueFromExtensionData(string name)
        //    {
        //        object value = null;
        //        ExtensionData?.TryGetValue(name, out value);
        //        return value;
        //    }
        //}

        //public class TheThingProcessorStatus
        //{
        //    public TheThingProcessor ThingProcessor;
        //    public string Error;
        //}

        //public class MsgAddThingProcessors
        //{
        //    public List<TheThingProcessor> ThingProcessors;
        //}

        //public class MsgAddThingProcessors<T> where T : TheThingProcessor
        //{
        //    public List<T> ThingProcessors;
        //}

        //public class MsgAddThingProcessorsResponse
        //{
        //    public List<TheThingProcessorStatus> ThingProcessorStatus { get; set; }
        //    public string Error { get; set; }
        //    public MsgAddThingProcessorsResponse()
        //    {
        //        ThingProcessorStatus = new List<TheThingProcessorStatus>();
        //    }

        //    public string GetSubscriptionErrors()
        //    {
        //        return ThingProcessorStatus?.Where(status => !string.IsNullOrEmpty(status.Error)).Aggregate("", (s, status) => $"{s}{status.ThingProcessor.ProcessorId}:{status.Error},").TrimEnd(',');
        //    }
        //}

        //public class MsgAddThingProcessorsResponse<TProcessorStatus> where TProcessorStatus : TheThingProcessorStatus
        //{
        //    public List<TProcessorStatus> ThingProcessorStatus { get; set; }
        //    public string Error { get; set; }
        //    public MsgAddThingProcessorsResponse()
        //    {
        //        ThingProcessorStatus = new List<TProcessorStatus>();
        //    }

        //    public string GetSubscriptionErrors()
        //    {
        //        return ThingProcessorStatus?.Where(status => !string.IsNullOrEmpty(status.Error)).Aggregate("", (s, status) => $"{s}{status.ThingProcessor.ProcessorId}:{status.Error},").TrimEnd(',');
        //    }
        //}

        public class MsgSubscribeToThings
        {
            public List<TheThingSubscription> SubscriptionRequests { get; set; }

            public MsgSubscribeToThings() : base() { }
            public MsgSubscribeToThings(TheThingSubscription thingSubscription)
            {
                SubscriptionRequests = new List<TheThingSubscription> { thingSubscription };
            }
        }

        public class MsgSubscribeToThings<TSubscription> where TSubscription : TheThingSubscription
        {
            public List<TSubscription> SubscriptionRequests { get; set; }

            public MsgSubscribeToThings() : base() { }
            public MsgSubscribeToThings(TSubscription thingSubscription)
            {
                SubscriptionRequests = new List<TSubscription> { thingSubscription };
            }
        }

        public class MsgSubscribeToThingsResponse : IMsgResponse
        {
            public List<TheThingSubscriptionStatus> SubscriptionStatus { get; set; }
            public const string strErrorMoreThanOneMatchingThingFound = "More than one matching Thing found";
            public string Error { get; set; }
            public MsgSubscribeToThingsResponse()
            {
                SubscriptionStatus = new List<TheThingSubscriptionStatus>();
            }

            public string GetSubscriptionErrors()
            {
                return GetSubscriptionErrors(this);
            }
            public static string GetSubscriptionErrors(MsgSubscribeToThingsResponse response)
            {
                if (response == null)
                {
                    return "Timeout";
                }
                string error = "";
                if (!string.IsNullOrEmpty(response.Error))
                {
                    error = $"{response.Error}";
                }
                var subscriptionErrors = response.SubscriptionStatus?.Where(status => !string.IsNullOrEmpty(status.Error)).Aggregate("", (s, status) => $"{s}{status.Subscription.SubscriptionId}:{status.Error},").TrimEnd(',');
                if (!string.IsNullOrEmpty(subscriptionErrors))
                {
                    error = $"{error}. Subscriptions: {subscriptionErrors}";
                }
                if (string.IsNullOrEmpty(error))
                {
                    return null;
                }
                return error;
            }
        }
        public class MsgSubscribeToThingsResponse<TSubscriptionStatus> where TSubscriptionStatus : TheThingSubscriptionStatus
        {
            public List<TSubscriptionStatus> SubscriptionStatus { get; set; }
            public string Error { get; set; }
            public MsgSubscribeToThingsResponse()
            {
                SubscriptionStatus = new List<TSubscriptionStatus>();
            }

            public string GetSubscriptionErrors()
            {
                return SubscriptionStatus?.Where(status => !string.IsNullOrEmpty(status.Error)).Aggregate("", (s, status) => $"{s}{status.Subscription.SubscriptionId}:{status.Error},").TrimEnd(',');
            }
        }

        // Experimental for now: this would let us avoid specifying the return message type on many functions (like PublishRequestJSonAsync)
        public abstract class IMsgRequest<T> where T : new()
        {
            public T GetResponseMessage()
            {
                return new T();
            }
            public string GetResponseMessageName()
            {
                return nameof(T);
            }
        }

        public class MsgUnsubscribeFromThings : IMsgRequest<MsgUnsubscribeFromThingsResponse>
        {
            public List<Guid> SubscriptionIds { get; set; }
            public bool UnsubscribeAll { get; set; }

            public MsgUnsubscribeFromThings()
            {
                SubscriptionIds = new List<Guid>();
            }
            public MsgUnsubscribeFromThings(Guid subscriptionId)
            {
                SubscriptionIds = new List<Guid>
                {
                    subscriptionId
                };
            }
        }

        public class TheThingSubscriptionStatus
        {
            public string Error { get; set; }
            public TheThingSubscription Subscription { get; set; }
        }
        public interface IMsgResponse : TheCommRequestResponse.IMsgResponse
        {
        }

        public class MsgUnsubscribeFromThingsResponse : IMsgResponse
        {
            public string Error { get; set; }
            public const string strErrorMoreThanOneMatchingThingFound = "More than one matching Thing found";
            public List<TheThingSubscriptionStatus> Failed { get; set; }
            public MsgUnsubscribeFromThingsResponse()
            {
                Failed = new List<TheThingSubscriptionStatus>();
            }

            public MsgUnsubscribeFromThingsResponse(TheThingSubscriptionStatus thingStatus)
            {
                Failed.Add(thingStatus);
            }
        }

        public Task<MsgSubscribeToThingsResponse> SubscribeToThingAsync(TheThingSubscription thingSubscriptionRequest)
        {
            return SubscribeToThingsAsync(new List<TheThingSubscription> { thingSubscriptionRequest });
        }
        public Task<MsgSubscribeToThingsResponse> SubscribeToThingsAsync(IEnumerable<TheThingSubscription> thingSubscriptionRequests)
        {
            var subscribeThingsRequest = new MsgSubscribeToThings
            {
                SubscriptionRequests = thingSubscriptionRequests.ToList(),
            };

            return SubscribeToThingsAsync(subscribeThingsRequest);
        }

        public Task<MsgSubscribeToThingsResponse> SubscribeToThingsAsync(MsgSubscribeToThings subscribeThingsRequest)
        {
            return TheCommRequestResponse.PublishRequestJSonAsync<MsgSubscribeToThings, MsgSubscribeToThingsResponse>(this, subscribeThingsRequest);
        }

        public Task<MsgGetThingSubscriptionsResponse> GetThingSubscriptionsAsync()
        {
            var getSubscriptionsRequest = new MsgGetThingSubscriptions
            {
            };

            return GetThingSubscriptionsAsync(getSubscriptionsRequest);
        }

        public Task<MsgGetThingSubscriptionsResponse> GetThingSubscriptionsAsync(MsgGetThingSubscriptions getSubscriptionsRequest)
        {
            return TheCommRequestResponse.PublishRequestJSonAsync<MsgGetThingSubscriptions, MsgGetThingSubscriptionsResponse>(this, getSubscriptionsRequest);
        }


        public Task<MsgUnsubscribeFromThingsResponse> UnsubscribeFromAllThingsAsync()
        {
            var unsubscribeThingsRequest = new MsgUnsubscribeFromThings
            {
                UnsubscribeAll = true,
            };
            return UnsubscribeFromThingsAsync(unsubscribeThingsRequest);
        }

        public Task<MsgUnsubscribeFromThingsResponse> UnsubscribeFromThingsAsync(IEnumerable<Guid> subscriptionIds)
        {
            var unsubscribeThingsRequest = new MsgUnsubscribeFromThings
            {
                SubscriptionIds = subscriptionIds.ToList(),
            };
            return UnsubscribeFromThingsAsync(unsubscribeThingsRequest);
        }

        public Task<MsgUnsubscribeFromThingsResponse> UnsubscribeFromThingsAsync(MsgUnsubscribeFromThings unsubscribeThingsRequest)
        {
            return TheCommRequestResponse.PublishRequestJSonAsync<MsgUnsubscribeFromThings, MsgUnsubscribeFromThingsResponse>(this, unsubscribeThingsRequest);
        }

        #endregion

        #region Sensor Consumer Helpers
        public class TheSensorConsumerHandler<TSubscription> where TSubscription : TheThingSubscription 
        {
            public delegate Task<TheThingSubscriptionStatus> SubscribeHandlerAsync(TSubscription subscription);
            public delegate void RefreshSubscriptionStateHandler();

            public delegate List<TSubscription> GetSubscriptionsHandler(bool generalize);
            public delegate List<TheThingSubscriptionStatus> UnsubscribeHandler(List<Guid> subscriptionIds, bool unsubscribeAll);


            SubscribeHandlerAsync _subscribeHandler;
            RefreshSubscriptionStateHandler _refreshSubscriptionStateHandler;
            GetSubscriptionsHandler _getSubscriptionsHandler;
            UnsubscribeHandler _unsubscribeHandler;

            public TheSensorConsumerHandler(SubscribeHandlerAsync subscribeHandler, RefreshSubscriptionStateHandler refreshSubscriptionStateHandler, GetSubscriptionsHandler getSubscriptionsHandler, UnsubscribeHandler unsubscribeHandler)
            {
                _subscribeHandler = subscribeHandler;
                _refreshSubscriptionStateHandler = refreshSubscriptionStateHandler;
                _getSubscriptionsHandler = getSubscriptionsHandler;
                _unsubscribeHandler = unsubscribeHandler;
            }

            public void HandleMessage(TSM message)
            {
                var cmd = TheCommonUtils.cdeSplit(message.TXT, ":", false, false);

                switch (cmd[0])
                {
                    case nameof(TheThing.MsgSubscribeToThings):
                        {
                            TheCommRequestResponse.DoHandleMessage<MsgSubscribeToThings<TSubscription>, MsgSubscribeToThingsResponse>(message,
#if !CDE_NET4
                                async
#endif
                                (request, response) => 
                                {
                                    response.SubscriptionStatus = new List<TheThing.TheThingSubscriptionStatus>();
                                    foreach (var subscription in request.SubscriptionRequests)
                                    {
#if !CDE_NET4
                                        response.SubscriptionStatus.Add(await _subscribeHandler(subscription));
#else
                                        response.SubscriptionStatus.Add(_subscribeHandler(subscription).Result);
#endif
                                    }
                                    _refreshSubscriptionStateHandler();
                                }
                            );
                            break;
                        }

                    case nameof(TheThing.MsgGetThingSubscriptions):
                        {
                            TheCommRequestResponse.DoHandleMessage<MsgGetThingSubscriptions, MsgGetThingSubscriptionsResponse<TSubscription>>(message, (request, response) => 
                            {
                                response.ThingSubscriptions = _getSubscriptionsHandler(request.Generalize ?? false);
                            });
                            break;
                        }
                    case nameof(TheThing.MsgUnsubscribeFromThings):
                        {
                            TheCommRequestResponse.DoHandleMessage<TheThing.MsgUnsubscribeFromThings, TheThing.MsgUnsubscribeFromThingsResponse>(message, (request, response) =>
                            {
                                response.Failed = _unsubscribeHandler(request.SubscriptionIds, request.UnsubscribeAll);
                            }
                            );
                            break;
                        }
                    default:
                        break;
                }
            }

        }
#endregion
    }
}
