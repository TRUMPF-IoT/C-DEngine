// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.ThingService;
using System.Threading.Tasks;
using nsCDEngine.Engines;
using System.Text;
using nsCDEngine.ISM;

#pragma warning disable CS1591    //TODO: Remove and document public methods

namespace nsCDEngine.Communication
{
    /// <summary>
    /// Indicates a Thing or engine to where a message can be sent.
    /// </summary>
    public class TheMessageAddress
    {
        /// <summary>
        /// When used as a target, Guid.Empty indicates all nodes (PublishCentral)
        /// </summary>
        public Guid Node;

        private string _engineName;
        public string EngineName
        {
            get
            {
                return _engineName;
            }
            set
            {
                if (_engineName != value && _baseEngine != null && _baseEngine.GetEngineName() != value)
                {
                    _baseEngine = null;
                }
                _engineName = value;
            }
        }
        public Guid ThingMID;
        public bool FirstNodeOnly;
        public bool ServiceOnly;
        public bool SendToProvisioningService;
        public string Route;

        private IBaseEngine _baseEngine;
        public IBaseEngine GetBaseEngine()
        {
            if (_baseEngine == null)
            {
                _baseEngine = TheThingRegistry.GetBaseEngine(EngineName);
            }
            return _baseEngine;
        }

        TheThing _baseThing;
        public TheThing GetBaseThing()
        {
            if (_baseThing == null && ThingMID != Guid.Empty)
            {
                _baseThing = TheThingRegistry.GetThingByMID(ThingMID, true);
            }
            return _baseThing;
        }

        public TheMessageAddress()
        {
            Node = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
            EngineName = eEngineName.ContentService;
            ThingMID = Guid.Empty;
        }

        public TheMessageAddress(TheThing targetThing)
        {
            if (targetThing == null || (_baseEngine = targetThing.GetBaseEngine()) == null)
            {
                Node = Guid.Empty;
                EngineName = "";
                ThingMID = Guid.Empty;
                return;
            }
            Node = targetThing.cdeN;
            EngineName = _baseEngine.GetEngineName();
            ThingMID = targetThing.cdeMID;
            FirstNodeOnly = false;
            ServiceOnly = true;
        }

        public static implicit operator TheMessageAddress(TheThing targetThing)
        {
            return new TheMessageAddress(targetThing);
        }
        public override string ToString()
        {
            if (Node == Guid.Empty || Node == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)
            {
                return TheCommonUtils.cdeGuidToString(ThingMID);
            }
            else
            {
                return String.Format("{0}-{1}", TheCommonUtils.cdeGuidToString(Node), TheCommonUtils.cdeGuidToString(ThingMID));
            }
        }
    }

    public class ParsedMessage
    {
        public TSM Response;
        public Guid CorrelationToken;
        public string[] ResponseParameters;
    }

    /// <summary>
    /// TheCommRequestResponse class has helper methods for request/response style messaging on top of the core messaging system:
    ///
    /// The "...Json" methods let you send requests and responses as TSM messages with JSON payload, using serialized .Net classes as the message contract for both request and response messages:
    /// - Serialized instances are used as the TSM.PLS payload
    /// - The class name is used for the TSM.TXT field, appending a unique correlation id (Guid, in stream form, seperated by a ":" character) to facilitate matching responses to requests.
    /// - The response gets a TSM.TXT with the original message name and a "_RESPONSE" suffix, as well as the ":" separated correlation id or the original message.
    /// PublishRequestJsonAsync&lt;inputT, outputT&gt;: Sends a TSM and waits for a response TSM.
    /// ParseRequestMessageJSON&lt;inputT&gt;: Used by the recipient of the request to obtain the message payload
    /// PublishResponseMessageJson&lt;responseT&gt;
    ///
    /// These methods let you send requests and responses using arbitrary TXT, PLS, message names. Response TSMs are assumed to have a _RESPONSE suffix.
    /// PublishRequestAsync
    /// PublishRequestAndParseResponseAsync
    /// PublishResponseMessage
    ///
    /// These methods provide the same functionality as the *Async methods, but offer a callback instead of using Tasks:
    /// PublishRequestCallback
    /// PublishRequestJsonCallback
    ///
    /// ParseRequestOrResponseMessage: can be used by the recipient of a TSM to parse the message name and correlation id.
    ///
    /// </summary>
    public static class TheCommRequestResponse
    {

        /// <summary>
        /// Sends a TSM message to the targetThing and return the matching response message. Uses a timeout of 60 seconds.
        /// </summary>
        /// <param name="senderThing">Thing from which the message is sent.</param>
        /// <param name="targetThing">Thing to which the message is to be sent.</param>
        /// <param name="messageName">Name of the message, as defined by the target thing</param>
        /// <param name="txtParameters">Array of simple string parameters, to be attached to the message's TXT field, as defined by the target thing. txtParameters must not contain ":" characters.</param>
        /// <param name="PLS">String payload to be set as the message's PLS field, as defined by the target thing.</param>
        /// <param name="PLB">Binary pauload to be set as the message's PLB field, as defined by the target thing.</param>
        /// <returns>The parsed response message as defined by the target thing. The target thing must use the PublishResponseMessage or an equivalent message format.
        /// If the message times out or an error occured while sending the message, the return value will be null.
        /// </returns>
        public static Task<ParsedMessage> PublishRequestAndParseResponseAsync(TheThing senderThing, TheThing targetThing, string messageName, string[] txtParameters = null, string PLS = null, byte[] PLB = null)
        {
            return PublishRequestAndParseResponseAsync(senderThing, targetThing, messageName, defaultRequestTimeout, txtParameters, PLS, PLB);
        }
        /// <summary>
        /// Sends a TSM message to the targetThing and return the matching response message
        /// </summary>
        /// <param name="originator">Thing or engine to use for response messages. If NULL defaults to ContentService.</param>
        /// <param name="target">Thing or engine to which the message is to be sent.</param>
        /// <param name="messageName">Name of the message, as defined by the target thing</param>
        /// <param name="txtParameters">Array of simple string parameters, to be attached to the message's TXT field, as defined by the target thing. txtParameters must not contain ":" characters.</param>
        /// <param name="PLS">String payload to be set as the message's PLS field, as defined by the target thing.</param>
        /// <param name="PLB">Binary pauload to be set as the message's PLB field, as defined by the target thing.</param>
        /// <param name="timeout">Time to wait for the response message. Can not exceed TheBaseAsset.MaxMessageResponseTimeout (default: 1 hour).</param>
        /// <returns>The response message as defined by the target thing. The response parameters and correlation token can be parsed using ParseRequestOrResponseMessage, if the target thing used the PublishResponseMessage or an equivalent message format.
        /// If the message times out or an error occured while sending the message, the return value will be null.
        /// </returns>
        public static Task<ParsedMessage> PublishRequestAndParseResponseAsync(TheMessageAddress originator, TheMessageAddress target, string messageName, TimeSpan timeout, string[] txtParameters = null, string PLS = null, byte[] PLB = null)
        {
            return PublishRequestAsync(originator, target, messageName, timeout, txtParameters, PLS, PLB).ContinueWith((t) =>
            {
                var msg = t.Result;
                return ParseRequestOrResponseMessage(msg);
            });
        }

        /// <summary>
        /// Sends a TSM message to the targetThing and return the matching response message. Uses a timeout of 60 seconds.
        /// </summary>
        /// <param name="senderThing">Thing from which the message is sent.</param>
        /// <param name="targetThing">Thing to which the message is to be sent.</param>
        /// <param name="messageName">Name of the message, as defined by the target thing</param>
        /// <param name="txtParameters">Array of simple string parameters, to be attached to the message's TXT field, as defined by the target thing. txtParameters must not contain ":" characters.</param>
        /// <param name="PLS">String payload to be set as the message's PLS field, as defined by the target thing.</param>
        /// <param name="PLB">Binary pauload to be set as the message's PLB field, as defined by the target thing.</param>
        /// <returns>The response message as defined by the target thing. The response parameters and correlation token can be parsed using ParseRequestOrResponseMessage, if the target thing used the PublishResponseMessage or an equivalent message format.
        /// If the message times out or an error occured while sending the message, the return value will be null.
        /// </returns>
        public static Task<TSM> PublishRequestAsync(TheThing senderThing, TheThing targetThing, string messageName, string[] txtParameters = null, string PLS = null, byte[] PLB = null)
        {
            return PublishRequestAsync(senderThing, targetThing, messageName, defaultRequestTimeout, txtParameters, PLS, PLB);
        }
        static readonly TimeSpan defaultRequestTimeout = new TimeSpan(0, 1, 0); // TODO make this configurable?

        /// <summary>
        /// Sends a TSM message to the targetThing and return the matching response message
        /// </summary>
        /// <param name="originator">Thing or engine to use for response messages. If NULL defaults to ContentService.</param>
        /// <param name="target">Thing or engine to which the message is to be sent.</param>
        /// <param name="messageName">Name of the message, as defined by the target thing. The response message will be messageName_RESPONSE.</param>
        /// <param name="txtParameters">Array of simple string parameters, to be attached to the message's TXT field, as defined by the target thing. txtParameters must not contain ":" characters.</param>
        /// <param name="PLS">String payload to be set as the message's PLS field, as defined by the target thing.</param>
        /// <param name="PLB">Binary pauload to be set as the message's PLB field, as defined by the target thing.</param>
        /// <param name="timeout">Time to wait for the response message. Can not exceed TheBaseAsset.MaxMessageResponseTimeout (default: 1 hour).</param>
        /// <returns>The response message as defined by the target thing. The response parameters and correlation token can be parsed using ParseRequestOrResponseMessage, if the target thing used the PublishResponseMessage or an equivalent message format.
        /// If the message times out or an error occured while sending the message, the return value will be null.
        /// </returns>
        public static Task<TSM> PublishRequestAsync(TheMessageAddress originator, TheMessageAddress target, string messageName, TimeSpan timeout, string[] txtParameters = null, string PLS = null, byte[] PLB = null)
        {
            Guid correlationToken = Guid.NewGuid();
            if (originator == null)
            {
                originator = TheThingRegistry.GetBaseEngineAsThing(eEngineName.ContentService);
            }
            var msg = PrepareRequestMessage(originator, target, messageName, correlationToken, txtParameters, PLS, PLB);
            if (msg == null)
            {
                return null;
            }

            if (timeout > MaxTimeOut)
            {
                timeout = MaxTimeOut;
            }
            if (timeout == TimeSpan.Zero)
            {
                timeout = defaultRequestTimeout;
            }

            var tcsResponse = new TaskCompletionSource<TSM>();

            var callback = new Action<ICDEThing, object>((tSenderThing, responseMsgObj) =>
            {
                var responseMsg = CheckResponseMessage(messageName, correlationToken, responseMsgObj);
                if (responseMsg != null)
                {
                    var bReceived = tcsResponse.TrySetResult(responseMsg);
                    if (bReceived)
                    {
                        TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("Thing Registry", $"Processed response message for {messageName}", eMsgLevel.l2_Warning, $"{correlationToken}: {responseMsg?.TXT}"), false);
                    }
                    else
                    {
                        TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("Thing Registry", $"Failed to process response message for {messageName}: timed out or double response", eMsgLevel.l2_Warning, $"{correlationToken}: {responseMsg?.TXT}"), false);
                    }
                }
                else
                {
                    responseMsg = (responseMsgObj as TheProcessMessage)?.Message;
                    TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("Thing Registry", $"Ignoring response message for {messageName}", eMsgLevel.l2_Warning, $"{correlationToken}: {responseMsg?.TXT}"), false);
                }
            });

            var originatorThingOrEngine = RegisterRequestCallback(originator, callback);
            if (originatorThingOrEngine == null)
            {
                return null;
            }

            if (target.SendToProvisioningService)
            {
                TheISMManager.SendToProvisioningService(msg);
            }
            else if(target.Node != Guid.Empty)
            {
                TheCommCore.PublishToNode(target.Node, msg);
            }
            else
            {
                TheCommCore.PublishCentral(msg, true);
            }

            TheCommonUtils.TaskWaitTimeout(tcsResponse.Task, timeout).ContinueWith((t) =>
            {
                UnregisterRequestCallback(originatorThingOrEngine, callback);
                var timedOut = tcsResponse.TrySetResult(null); // Will preserve value if actual result has already been set
                if (timedOut)
                {
                    TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("Thing Registry", $"Timeout waiting for response message for {messageName}", eMsgLevel.l2_Warning, $"{correlationToken}"), false);
                }
            });
            return tcsResponse.Task;
        }

        private static object RegisterRequestCallback(TheMessageAddress originator, Action<ICDEThing, object> callback)
        {
            // TODO optimize engine handler registration/unregistration / caching? Better way to receive messages targetted to a thing?
            var originatorThing = originator.GetBaseThing();
            if (originatorThing != null)
            {
                originatorThing.RegisterEvent(eThingEvents.IncomingMessage, callback);
                return originatorThing;
            }
            IBaseEngine originatorEngine = originator.GetBaseEngine();
            if (originatorEngine != null)
            {
                originatorEngine.RegisterEvent(eEngineEvents.IncomingMessage, callback);
            }

            return originatorEngine;
        }

        private static void UnregisterRequestCallback(object originatorThingOrEngine, Action<ICDEThing, object> callback)
        {
            if (originatorThingOrEngine == null) return;

            if (originatorThingOrEngine is TheThing)
            {
                (originatorThingOrEngine as TheThing).UnregisterEvent(eThingEvents.IncomingMessage, callback);
            }
            else if (originatorThingOrEngine is IBaseEngine)
            {
                (originatorThingOrEngine as IBaseEngine).UnregisterEvent(eEngineEvents.IncomingMessage, callback);
            }

        }


        static TimeSpan MaxTimeOut = new TimeSpan(1, 0, 0); // TODO Add to TheBaseAssets and make configurable?

        private static TSM PrepareRequestMessage(TheMessageAddress originator, TheMessageAddress target, string messageName, Guid correlationToken, string[] txtParameters, string PLS, byte[] PLB)
        {

            var parameterText = CreateParameterText(txtParameters);
            if (parameterText == null)
            {
                return null;
            }

            TSM msg = new TSM(target.EngineName, String.Format("{0}:{1}:{2}", messageName, TheCommonUtils.cdeGuidToString(correlationToken), parameterText), PLS);
            if (PLB != null)
            {
                msg.PLB = PLB;
            }
            if (originator.ThingMID != Guid.Empty)
            {
                msg.SetOriginatorThing(originator.ThingMID);
            }
            if (target.ThingMID != Guid.Empty)
            {
                msg.OWN = TheCommonUtils.cdeGuidToString(target.ThingMID);
            }
            if (!string.IsNullOrEmpty(target.Route))
                msg.GRO = target.Route;
            return msg;
        }

        private static string CreateParameterText(string[] parameters)
        {
            var parameterText = new StringBuilder();
            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    if (parameter.Contains(":"))
                    {
                        // TODO come up with an escaping mechanism or leave it up to the caller?
                        // TODO Any other reserved characters (;:; is used in the cdeEngine)?
                        return null;
                    }
                    if (parameterText.Length != 0)
                    {
                        parameterText.Append(":");
                    }
                    parameterText.Append(parameter);
                }
            }
            return parameterText.ToString();
        }

        private static TSM CheckResponseMessage(string messageName, Guid correlationToken, object responseMsgObj)
        {
            if (!(responseMsgObj is TheProcessMessage processMsg))
            {
                return null;
            }
            var responseMsg = processMsg.Message;
            if (!responseMsg.TXT.StartsWith(messageName + "_RESPONSE:"))
            {
                return null;
            }

            var parts = responseMsg.TXT.Split(new[] { ':' }, 3);
            if (parts.Length < 2 || TheCommonUtils.CGuid(parts[1]) != correlationToken)
            {
                return null;
            }
            return responseMsg;
        }

        /// <summary>
        /// Sends a message with the given TXT and PLS/PLB to the target and returns the TXT of the response message separated by ':' into a string array.
        /// </summary>
        /// <param name="originator">Thing or engine to use for response messages. If NULL defaults to ContentService.</param>
        /// <param name="target">Thing or engine to which the message is to be sent.</param>
        /// <param name="messageName">The "command" of the message. This will be the first entry in the TXT separated by ':', i.e. messageName:correlationToken:txtParameter1:txtParameter2...</param>
        /// <param name="timeout">If the response message is not returned to the callback within this time, the callback will be invoked with a null parameter.</param>
        /// <param name="responseCallback">The method that will be called once the response message has been received or a timeout has occured.  The parameter will contain the array of parts of the response message's TXT.</param>
        /// <param name="txtParameters">The strings that will follow messageName and the correlation token in the TXT separated by ':', i.e. messageName:correlationToken:txtParameter1:txtParameter2...</param>
        /// <param name="PLS">The payload of the message as a string.</param>
        /// <param name="PLB">The payload of the message as a byte[].</param>
        public static void PublishRequestCallback(TheMessageAddress originator, TheMessageAddress target, string messageName, TimeSpan timeout, Action<string[]> responseCallback, string[] txtParameters = null, string PLS = null, byte[] PLB = null)
        {
            PublishRequestCallback(originator, target, messageName, timeout, (TSM msg) =>
            {
                ParseRequestOrResponseMessage(msg, out string[] responseParams, out Guid token);
                responseCallback(responseParams);
            }, txtParameters, PLS, PLB);
        }

        /// <summary>
        /// Sends a message with the given TXT and PLS/PLB to the target and returns the response message to the callback as a TSM parameter.
        /// </summary>
        /// <param name="originator">Thing or engine to use for response messages. If NULL defaults to ContentService.</param>
        /// <param name="target">Thing or engine to which the message is to be sent.</param>
        /// <param name="messageName">The "command" of the message. This will be the first entry in the TXT separated by ':', i.e. messageName:correlationToken:txtParameter1:txtParameter2...</param>
        /// <param name="timeout">If the response message is not returned to the callback within this time, the callback will be invoked with a null parameter.</param>
        /// <param name="responseCallback">The method that will be called once the response message has been received or a timeout has occured.  The parameter will contain the message as a TSM, or null if a timeout occurs.</param>
        /// <param name="txtParameters">The strings that will follow messageName and the correlation token in the TXT separated by ':', i.e. messageName:correlationToken:txtParameter1:txtParameter2...</param>
        /// <param name="PLS">The payload of the message as a string.</param>
        /// <param name="PLB">The payload of the message as a byte[].</param>
        public static void PublishRequestCallback(TheMessageAddress originator, TheMessageAddress target, string messageName, TimeSpan timeout, Action<TSM> responseCallback, string[] txtParameters = null, string PLS = null, byte[] PLB = null)
        {
            Guid correlationToken = Guid.NewGuid();
            if (originator == null)
            {
                originator = TheThingRegistry.GetBaseEngineAsThing(eEngineName.ContentService);
            }
            var msg = PrepareRequestMessage(originator, target, messageName, correlationToken, txtParameters, PLS, PLB);
            if (msg == null)
            {
                responseCallback(null);
                return;
            }

            if (timeout > MaxTimeOut)
            {
                timeout = MaxTimeOut;
            }

            object cancelToken = TheCommonUtils.SleepOneEyeGetCancelToken();

            bool bCompleted = false;

            var innerCallback = new Action<ICDEThing, object>((tSenderThing, responseMsgObj) =>
            {
                var responseMsg = CheckResponseMessage(messageName, correlationToken, responseMsgObj);
                if (responseMsg != null)
                {
                    bCompleted = true;
                    TheCommonUtils.SleepOneEyeCancel(cancelToken);
                    responseCallback(responseMsg);
                }
            });
            var originatorThingOrEngine = RegisterRequestCallback(originator, innerCallback);
            if (originatorThingOrEngine == null)
            {
                responseCallback(null);
                return;
            }

            TheCommCore.PublishCentral(msg, true);

            TheCommonUtils.cdeRunAsync("ResponseTimeout", true, (o) =>
            {
                TheCommonUtils.SleepOneEyeWithCancel((uint)timeout.TotalMilliseconds, 100, cancelToken);
                UnregisterRequestCallback(originatorThingOrEngine, innerCallback);
                if (!bCompleted)
                {
                    responseCallback(null);
                }
            });
        }

        /// <summary>
        /// Sends a message with JSON-serialized parameters to the targetThing and returns the response message as a deserialized object
        /// </summary>
        /// <typeparam name="inputT"></typeparam>
        /// <typeparam name="outputT"></typeparam>
        /// <param name="target">Thing or engine to which the message is to be sent.</param>
        /// <param name="messageParameters">The object to send as the payload of the message.</param>
        /// <returns></returns>
        public static Task<outputT> PublishRequestJSonAsync<inputT, outputT>(TheMessageAddress target, inputT messageParameters)
        {
            return PublishRequestJSonAsync<inputT, outputT>(null, target, messageParameters, defaultRequestTimeout);
        }

        /// <summary>
        /// Sends a message with JSON-serialized parameters to the targetThing and returns the response message as a deserialized object
        /// </summary>
        /// <typeparam name="inputT"></typeparam>
        /// <typeparam name="outputT"></typeparam>
        /// <param name="originator">Thing or engine to use for response messages. If NULL defaults to ContentService.</param>
        /// <param name="target">Thing or engine to which the message is to be sent.</param>
        /// <param name="messageParameters">The object to send as the payload of the message.</param>
        /// <returns></returns>
        public static Task<outputT> PublishRequestJSonAsync<inputT, outputT>(TheMessageAddress originator, TheMessageAddress target, inputT messageParameters)
        {
            return PublishRequestJSonAsync<inputT, outputT>(originator, target, messageParameters, defaultRequestTimeout);
        }

        /// <summary>
        /// Sends a message with JSON-serialized parameters to the targetThing and returns the response message as a deserialized object
        /// </summary>
        /// <typeparam name="inputT"></typeparam>
        /// <typeparam name="outputT"></typeparam>
        /// <param name="target">Thing or engine to which the message is to be sent.</param>
        /// <param name="messageParameters">The object to send as the payload of the message.</param>
        /// <param name="timeout">If the response message is not returned within this time, the result of the Task will be set to null.</param>
        /// <returns></returns>
        public static Task<outputT> PublishRequestJSonAsync<inputT, outputT>(TheMessageAddress target, inputT messageParameters, TimeSpan timeout)
        {
            return PublishRequestJSonAsync<inputT, outputT>(null, target, messageParameters, timeout);
        }

        /// <summary>
        /// Sends a message with JSON-serialized parameters to the targetThing and returns the payload of the response message as a deserialized object.
        /// </summary>
        /// <typeparam name="inputT"></typeparam>
        /// <typeparam name="outputT"></typeparam>
        /// <param name="originator">Thing or engine to use for response messages. If NULL defaults to ContentService.</param>
        /// <param name="target">Thing or engine to which the message is to be sent.</param>
        /// <param name="messageParameters">The object to send as the payload of the message.</param>
        /// <param name="timeout">If the response message is not returned twithin this time, the result of the Task will be set to null.</param>
        /// <returns></returns>
        public static Task<outputT> PublishRequestJSonAsync<inputT, outputT>(TheMessageAddress originator, TheMessageAddress target, inputT messageParameters, TimeSpan timeout)
        {
            string messageName = messageParameters.GetType().Name;
            return PublishRequestAsync(originator, target, messageName, timeout, null, TheCommonUtils.SerializeObjectToJSONString(messageParameters), null).ContinueWith(
                (msgTask) =>
                {
                    var msg = msgTask.Result;
                    if (msg != null)
                    {
                        return TheCommonUtils.DeserializeJSONStringToObject<outputT>(msg.PLS);
                    }
                    else
                    {
                        return default;
                    }
                });
        }

        /// <summary>
        /// Sends a message with JSON-serialized parameters to the targetThing and returns the payload of the response message to the callback as a deserialized object.
        /// </summary>
        /// <typeparam name="inputT"></typeparam>
        /// <typeparam name="outputT"></typeparam>
        /// <param name="originator">Thing or engine to use for response messages. If NULL defaults to ContentService.</param>
        /// <param name="target">Thing or engine to which the message is to be sent.</param>
        /// <param name="messageParameters">The object to send as the payload of the message.</param>
        /// <param name="timeout">If the response message is not returned to the callback within this time, the callback will be invoked with the default of <typeparamref name="outputT"/> as a parameter.</param>
        /// <param name="responseCallback">The method that will be called once the response message has been received or a timeout has occured.  The parameter will contain the deserialized payload of the response message, or the default of <typeparamref name="outputT"/> if a timeout occurs.</param>
        public static void PublishRequestJsonCallback<inputT, outputT>(TheMessageAddress originator, TheMessageAddress target, inputT messageParameters, TimeSpan timeout, Action<outputT> responseCallback)
        {
            string messageName = messageParameters.GetType().Name;
            PublishRequestCallback(originator, target, messageName, timeout, (msg) =>
            {
                if (msg != null)
                {
                    responseCallback(TheCommonUtils.DeserializeJSONStringToObject<outputT>(msg.PLS));
                }
                else
                {
                    responseCallback(default);
                }
            }, null, TheCommonUtils.SerializeObjectToJSONString(messageParameters));
        }

        /// <summary>
        /// Parses the TXT of the message in the format name:correlationToken:param1:param2...
        /// </summary>
        /// <param name="message">The message that contains the TXT to parse.</param>
        /// <param name="messageParameters">The returned array of parts of the TXT split by ':'</param>
        /// <param name="correlationToken">The returned correlation token of the message found in the TXT.</param>
        /// <returns>The name part of the TXT</returns>
        public static string ParseRequestOrResponseMessage(TSM message, out string[] messageParameters, out Guid correlationToken)
        {
            messageParameters = null;
            correlationToken = Guid.Empty;
            var parts = message.TXT.Split(new char[] { ':' });
            if (parts.Length < 2)
            {
                return null;
            }
            string messageName = parts[0];
            correlationToken = TheCommonUtils.CGuid(parts[1]);
            messageParameters = new string[parts.Length - 2];
            int index = 0;
            for (int partIndex = 2; partIndex < parts.Length; partIndex++)
            {
                messageParameters[index] = parts[partIndex];
                index++;
            }
            return messageName;
        }

        private static ParsedMessage ParseRequestOrResponseMessage(TSM message)
        {
            if (String.IsNullOrEmpty(ParseRequestOrResponseMessage(message, out string[] responseParameters, out Guid correlationToken)))
            {
                return null;
            }
            return new ParsedMessage
            {
                ResponseParameters = responseParameters,
                Response = message,
                CorrelationToken = correlationToken,
            };
        }


        /// <summary>
        /// Deserializes the JSON string in the PLS of message into an object of type <typeparamref name="T"/>.  If it cannot be deserialized, returns the default of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message">The TSM that contains the PLS to be parsed.</param>
        /// <returns></returns>
        static public T ParseRequestMessageJSON<T>(TSM message)
        {
            try
            {
                var request = TheCommonUtils.DeserializeJSONStringToObject<T>(message.PLS);
                return request;
            }
            catch (Exception e)
            {
                TheSystemMessageLog.WriteLog(1000, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(message.ENG, "Exception while parsing JSON Message Request", eMsgLevel.l2_Warning, String.Format("{0}:{1}", nameof(T), e)), false);
            }
            return default;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Simple Publish response message. </summary>
        ///
        /// <remarks>   Chris, 3/17/2020. </remarks>
        ///
        /// <param name="originalMessage">  The incoming receiving message. </param>
        /// <param name="PLS">              The returning PLS (payload string). </param>
        /// <param name="PLB">              (Optional) returning PLB (payload binary). </param>
        ///
        /// <returns>   True if it succeeds, false if it fails. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public static bool PublishResponseMessage(TSM originalMessage, string PLS, byte[] PLB=null)
        {
            return PublishResponseMessage(originalMessage, null, PLS, PLB);
        }

        /// <summary>
        /// Publishes the response message back to the originator of the <paramref name="originalMessage"/>.
        /// </summary>
        /// <param name="originalMessage">The incoming receiving message.</param>
        /// <param name="responseParameters">The array of strings that will be added to the end of the TXT, each separated by ':'</param>
        /// <param name="PLS">The returning PLS (payload string). Either the PLS or PLB should be specified.</param>
        /// <param name="PLB">The returning PLB (payload binary). Either the PLS or PLB should be specified.</param>
        /// <returns></returns>
        public static bool PublishResponseMessage(TSM originalMessage, string[] responseParameters, string PLS, byte[] PLB)
        {
            if (originalMessage == null)
            {
                return false;
            }
            var indexOfSeparator = originalMessage.TXT.IndexOf(':');
            if (indexOfSeparator < 0)
            {
                return false;
            }
            var indexOfSecondSeparator = originalMessage.TXT.IndexOf(":", indexOfSeparator + 1);
            if (indexOfSecondSeparator < 0)
            {
                return false;
            }

            var responseParameterText = CreateParameterText(responseParameters);
            if (responseParameterText == null)
            {
                return false;
            }

            var responseMsg = new TSM(eEngineName.ContentService, String.Format("{0}_RESPONSE:{1}:{2}", originalMessage.TXT.Substring(0, indexOfSeparator), originalMessage.TXT.Substring(indexOfSeparator + 1, indexOfSecondSeparator - indexOfSeparator), responseParameterText), PLS);
            if (PLB != null)
            {
                responseMsg.PLB = PLB;
            }

            TheCommCore.PublishToOriginator(originalMessage, responseMsg, true);

            return true;
        }

        /// <summary>
        /// Publishes the response message back to the originator of the <paramref name="originalMessage"/> with the <paramref name="responseParameters"/> serialized to a JSON string in the payload.
        /// </summary>
        /// <typeparam name="outputT"></typeparam>
        /// <param name="originalMessage">The incoming receiving message.</param>
        /// <param name="responseParameters">The object to send back, which will be serialized to JSON in the payload of the message.</param>
        /// <param name="PLB">(Optional) The returning PLB (payload binary).</param>
        /// <returns></returns>
        public static bool PublishResponseMessageJson<outputT>(TSM originalMessage, outputT responseParameters, byte[] PLB = null)
        {
            if (originalMessage == null)
            {
                return false;
            }
            var indexOfSeparator = originalMessage.TXT.IndexOf(':');
            if (indexOfSeparator < 0)
            {
                return false;
            }
            var indexOfSecondSeparator = originalMessage.TXT.IndexOf(":", indexOfSeparator + 1);
            if (indexOfSecondSeparator < 0)
            {
                return false;
            }

            var responseParameterText = TheCommonUtils.SerializeObjectToJSONString(responseParameters);
            if (responseParameterText == null)
            {
                return false;
            }

            var responseMsg = new TSM(eEngineName.ContentService, String.Format("{0}_RESPONSE:{1}", originalMessage.TXT.Substring(0, indexOfSeparator), originalMessage.TXT.Substring(indexOfSeparator + 1, indexOfSecondSeparator - indexOfSeparator)), responseParameterText);
            if (PLB != null)
            {
                responseMsg.PLB = PLB;
            }

            // CODE REVIEW: Should we support TheProcessMessage's LocalCallback mechanism here as well? Much better performance
            TheCommCore.PublishToOriginator(originalMessage, responseMsg, true);

            return true;

        }

    }
}
