// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#pragma warning disable CS1591    //TODO: Remove and document public methods

namespace nsCDEngine.Engines.ThingService
{

    public sealed partial class TheThing : TheMetaDataBase, ICDEThing
    {

#if !CDE_NET4

        public class ThePipelineConfiguration
        {
            public string FriendlyName;
            public List<TheThingConfiguration> ThingConfigurations;

            public override string ToString()
            {
                return $"{FriendlyName}: Things: {ThingConfigurations?.Count}";
            }

        }

        /// <summary>
        /// Return the configurations for a TheThing and all the TheThing instances that depend on the thing, or that this TheThing depends on.
        /// </summary>
        /// <param name="bGeneralize">If true, any configuration properties that need to be modified when moving the pipeline to another node, or creating a new instance of the pipeline on the same node, will be captured in a separate section (TheThingConfiguration.ThingSpecializationParameters), for easy manual or automated customization (via answer files).</param>
        /// <returns>A ThePipelineConfiguration instance with the configuration information for all TheThings in the pipeline.</returns>
        public Task<ThePipelineConfiguration> GetThingPipelineConfigurationAsync(bool bGeneralize)
        {
            return GetThingPipelineConfigurationAsync(null, bGeneralize);
        }

        /// <summary>
        /// Return the configurations for a TheThing and all the TheThing instances that depend on the thing, or that this TheThing depends on.
        /// </summary>
        /// <param name="bGeneralize">If true, any configuration properties that need to be modified when moving the pipeline to another node, or creating a new instance of the pipeline on the same node, will be captured in a separate section (TheThingConfiguration.ThingSpecializationParameters), for easy manual or automated customization (via answer files).</param>
        /// <param name="friendlyName">Friendly name to use for the pipeline configuration. If null, defaults to the TheThing.FriendlyName of the root thing of the pipeline.</param>
        /// <returns>A ThePipelineConfiguration instance with the configuration information for all TheThings in the pipeline.</returns>
        public async Task<ThePipelineConfiguration> GetThingPipelineConfigurationAsync(string friendlyName, bool bGeneralize)
        {
            var pipelineThingConfigs = new List<TheThingConfiguration>();
            var thingsVisited = new List<TheThing>();
            await GetThingPipelineConfigurationRecursiveAsync(this, bGeneralize, pipelineThingConfigs, thingsVisited);
            var pipelineConfig = new ThePipelineConfiguration
            {
                FriendlyName = friendlyName ?? this.FriendlyName,
                ThingConfigurations = pipelineThingConfigs,
            };
            return pipelineConfig;
        }

        private static async Task GetThingPipelineConfigurationRecursiveAsync(TheThing tThing, bool bGeneralize, List<TheThingConfiguration> pipelineConfig, List<TheThing> thingsVisited)
        {
            if (tThing == null || thingsVisited.Contains(tThing))
            {
                return;
            }

            thingsVisited.Add(tThing);

            var thingConfig = await tThing.GetThingConfigurationAsync(bGeneralize, true);
            if (thingConfig == null)
            {
                return;
            }

            if (thingConfig.ConfigurationValues != null)
            {
                var thingReferencesFromProperties = thingConfig.ConfigurationValues.Where(cv => cv.IsThingReference == true);
                foreach (var thingReferenceProps in thingReferencesFromProperties)
                {
                    // This is sub optimal: inside GetThingConfiguratioAsync we already have the thing. Export is not perf sensitive so doing it this way instead of restructuring the code
                    var tReferencedThing = TheThingRegistry.GetThingByMID(TheCommonUtils.CGuid(thingReferenceProps.Value), true);
                    if (tReferencedThing == null)
                    {
                        try
                        {
                            var tThingReference = TheCommonUtils.DeserializeJSONStringToObject<TheThingReference>(TheCommonUtils.CStr(thingReferenceProps.Value));
                            tReferencedThing = tThingReference?.GetMatchingThing();
                        }
                        catch { }
                    }
                    if (tReferencedThing != null)
                    {
                        await GetThingPipelineConfigurationRecursiveAsync(tReferencedThing, bGeneralize, pipelineConfig, thingsVisited);
                    }
                }
            }
            if (thingConfig.SensorSubscriptions != null)
            {
                var sensorTargetThings = thingConfig.SensorSubscriptions.SelectMany(sub => sub.TargetThing.GetMatchingThings()).Distinct();
                // Find any things into which this provider is sending sensor data
                foreach (var sensorTargetThing in sensorTargetThings)
                {
                    if (sensorTargetThing != null)
                    {
                        await GetThingPipelineConfigurationRecursiveAsync(sensorTargetThing, bGeneralize, pipelineConfig, thingsVisited);
                    }
                }
            }

            if (thingConfig.ThingSubscriptions != null)
            {
                // Find any things that this consumer gets data from
                var consumedThings = thingConfig.ThingSubscriptions.SelectMany(sub => sub.ThingReference.GetMatchingThings()).Distinct().ToList();
                foreach (var consumedThing in consumedThings)
                {
                    if (consumedThing != null)
                    {
                        await GetThingPipelineConfigurationRecursiveAsync(consumedThing, bGeneralize, pipelineConfig, thingsVisited);
                    }
                }
            }

            if (thingConfig.ThingReferences != null)
            {
                // Find any things that this thing declares to have a reference to
                var referencedThings = thingConfig.ThingReferences.SelectMany(thingRef => thingRef.GetMatchingThings()).Distinct().ToList();
                foreach (var referencedThing in referencedThings)
                {
                    if (referencedThing != null)
                    {
                        await GetThingPipelineConfigurationRecursiveAsync(referencedThing, bGeneralize, pipelineConfig, thingsVisited);
                    }
                }

            }

            // Add this thing after any things it depends on (and that it needs to be created before it gets created)
            pipelineConfig.Add(thingConfig);
            // Subsequent things depend on this thing


            // Find things that consumer data from this thing

            // First find any history consumers (more efficient)
            var historyConsumers = tThing.Historian?.GetConsumerRegistrations();
            if (historyConsumers != null)
            {
                foreach (var historyConsumer in historyConsumers)
                {
                    if (historyConsumer.OwnerThing != null)
                    {
                        await GetThingPipelineConfigurationRecursiveAsync(historyConsumer.OwnerThing, bGeneralize, pipelineConfig, thingsVisited);
                    }
                }
            }

            // Enumerate all consumers and find the ones that subscribe to this thing
            foreach(var consumerThing in TheThingRegistry.GetBaseEnginesByCap(eThingCaps.SensorConsumer).SelectMany(eng => TheThingRegistry.GetThingsOfEngine(eng.GetEngineName(), eThingCaps.SensorConsumer).Where(thing => !thingsVisited.Contains(thing))))
            {
                var thingSubscriptionResponse = await consumerThing.GetThingSubscriptionsAsync(new MsgGetThingSubscriptions { Generalize = bGeneralize });
                if (thingSubscriptionResponse != null && string.IsNullOrEmpty(thingSubscriptionResponse.Error))
                {
                    foreach(var thingConsumed in thingSubscriptionResponse.ThingSubscriptions.SelectMany(sub => sub.ThingReference.GetMatchingThings()).Where(thingConsumed => thingConsumed == tThing))
                    {
                        await GetThingPipelineConfigurationRecursiveAsync(consumerThing, bGeneralize, pipelineConfig, thingsVisited);
                    }
                }
                else
                {
                    // TODO should we fail the export? Log it? Report the error(s) to the caller?
                }
            }

            // Find any provider things that provide data into a sensor property in this thing
            var sensorProperties = tThing.GetSensorPropertyMetaData();
            if (sensorProperties?.Any() == true)
            {
                var providerThings = sensorProperties.Select(sensorProp => sensorProp.ProviderInfo.Provider).Distinct().ToList();
                foreach (var providerThing in providerThings)
                {
                    if (providerThing != null && !thingsVisited.Contains(providerThing))
                    {
                        await GetThingPipelineConfigurationRecursiveAsync(providerThing, bGeneralize, pipelineConfig, thingsVisited);
                    }
                }
            }
        }

        public async static Task<List<TheThing>> CreateThingPipelineFromConfigurationAsync(TheMessageAddress owner, ThePipelineConfiguration pipelineConfig)
        {
            if (pipelineConfig == null)
            {
                return null;
            }
            var pipelineThings = new List<TheThing>();
            var thingReferenceMap = new Dictionary<string, string>();

            TheBaseAssets.MySYSLOG.WriteToLog(7717, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ThingService, $"Creating thing pipeline from config", eMsgLevel.l6_Debug, $"{pipelineConfig}"));

            // TODO order by dependency graph or deal with error? For now assume the things are ordered (as GetThingPipelineConfigurationAsync already attempts to do)
            int index = 0;
            foreach (var thingConfig in pipelineConfig.ThingConfigurations)
            {
                var thing = await CreateThingFromConfigurationAsyncInternal(owner, thingConfig, thingReferenceMap);
                pipelineThings.Add(thing);
                index++;
            }
            TheBaseAssets.MySYSLOG.WriteToLog(7718, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Created thing pipeline from config", eMsgLevel.l3_ImportantMessage, $"{pipelineConfig}"));
            return pipelineThings;
        }

        internal class ApplyConfigFilesResult
        {
            public bool Success;
            public int NumberOfFiles;
            public int NumberOfFailedFiles;
        }

        internal static Task<ApplyConfigFilesResult> ApplyConfigurationFilesAsync()
        {
            bool bSuccess = true;
            int fileCount = 0;
            int failedFileCount = 0;
            var pipeLineTasks = new List<Task<List<TheThing>>>();
            try
            {
                var configDir = TheCommonUtils.cdeFixupFileName(Path.Combine("ClientBin","config"));
                if (!Directory.Exists(configDir))
                {
                    return TheCommonUtils.TaskFromResult(new ApplyConfigFilesResult
                    {
                        Success = true,
                    });
                }

                var configDirs = Directory.GetParent(configDir).GetDirectories("config");
                if (configDirs.Length != 1 || configDirs[0].FullName != configDir)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(7721, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "ClientBin/config directory has invalid casing. Must be lower case.", eMsgLevel.l1_Error));
                    return TheCommonUtils.TaskFromResult(new ApplyConfigFilesResult
                    {
                        Success = false,
                    });
                }

                var configFiles = Directory.GetFiles(configDir, "*.cdeconfig");
                fileCount = configFiles.Length;
                foreach (var configFile in configFiles)
                {
                    var pipelineJson = File.ReadAllText(configFile);
                    pipelineJson = TheCommonUtils.GenerateFinalStr(pipelineJson);
                    var pipelineConfig = TheCommonUtils.DeserializeJSONStringToObject<ThePipelineConfiguration>(pipelineJson);

                    var answerFileNames = Directory.GetFiles(configDir, $"{Path.GetFileNameWithoutExtension(configFile)}*.cdeanswer");
                    var answerConfigs = answerFileNames.Select(af =>
                    {
                        var answerJson = File.ReadAllText(af);
                        answerJson = TheCommonUtils.GenerateFinalStr(answerJson);
                        var answerConfig = TheCommonUtils.DeserializeJSONStringToObject<ThePipelineConfiguration>(answerJson);
                        return answerConfig;
                    });
                    var tasks = ApplyPipelineConfigJsonInternal(pipelineConfig, answerConfigs, configFile, answerFileNames);
                    if (tasks?.Count == 0)
                    {
                        failedFileCount++;
                    }
                    pipeLineTasks.AddRange(tasks);
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(7722, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Error processing config files", eMsgLevel.l1_Error, $"{e.Message}"));
                bSuccess = false;
            }

            return TheCommonUtils.TaskWhenAll(pipeLineTasks.Select(t => (Task)t))
                .ContinueWith(t =>
                {
                    foreach(var task in pipeLineTasks)
                    {
                        if (task.IsFaulted)
                        {
                            failedFileCount++;
                            bSuccess = false;
                        }
                    }
                    return new ApplyConfigFilesResult { Success = bSuccess, NumberOfFiles = fileCount, NumberOfFailedFiles = failedFileCount };
                });
        }
        public class MsgApplyPipelineConfig
        {
            public ThePipelineConfiguration Config { get; set; }
            public List<ThePipelineConfiguration> AnswerConfigs { get; set; }
        }

        public class MsgApplyPipelineConfigResponse : IMsgResponse
        {
            public string Error { get; set; }
            public List<TheThingIdentity> ThingsConfigured { get; set; }
        }

        public static Task<MsgApplyPipelineConfigResponse> ApplyPipelineConfigAsync(MsgApplyPipelineConfig request)
        {
            var pipeLineTasks = ApplyPipelineConfigJsonInternal(request.Config, request.AnswerConfigs, null, null);

            if (pipeLineTasks == null)
            {
                return TheCommonUtils.TaskFromResult(new MsgApplyPipelineConfigResponse
                {
                    Error = "Error applying configuration",
                });
            }

            return TheCommonUtils.TaskWhenAll(pipeLineTasks.Select(t => (Task)t))
                .ContinueWith(t =>
                {
                    bool bSuccess = true;
                    int failedFileCount = 0;
                    var thingsConfigured = new List<TheThingIdentity>();
                    foreach (var task in pipeLineTasks)
                    {
                        if (task.IsFaulted)
                        {
                            failedFileCount++;
                            bSuccess = false;
                            thingsConfigured.Add(null);
                        }
                        else
                        {
                            var thingIdentities = task.Result?.Select(thing => new TheThingIdentity(thing));
                            if (thingIdentities != null)
                            {
                                thingsConfigured.AddRange(thingIdentities);
                            }
                            else
                            {
                                thingsConfigured.Add(null);
                            }
                        }
                    }
                    return new MsgApplyPipelineConfigResponse { Error = bSuccess ? null : "Error applying configuration", ThingsConfigured = thingsConfigured, };
                });
        }

        //internal static Task<ApplyConfigFilesResult> ApplyPipelineConfigJsonAsync(string pipelineConfigJson, IEnumerable<string> answerFilesJson, string configFileNameForLog, string[] answerFileNamesForLog)
        //{
        //    var pipeLineTasks = ApplyPipelineConfigJsonInternal(pipelineConfigJson, answerFilesJson, configFileNameForLog, answerFileNamesForLog);
        //    if (pipeLineTasks == null)
        //    {
        //        return TheCommonUtils.TaskFromResult(new ApplyConfigFilesResult
        //        {
        //            Success = false,
        //        });
        //    }

        //    return TheCommonUtils.TaskWhenAll(pipeLineTasks.Select(t => (Task)t))
        //        .ContinueWith(t =>
        //        {
        //            bool bSuccess = true;
        //            int failedFileCount = 0;
        //            foreach (var task in pipeLineTasks)
        //            {
        //                if (task.IsFaulted)
        //                {
        //                    failedFileCount++;
        //                    bSuccess = false;
        //                }
        //            }
        //            return new ApplyConfigFilesResult { Success = bSuccess, NumberOfFiles = 1, NumberOfFailedFiles = failedFileCount };
        //        });
        //}

        static List<Task<List<TheThing>>> ApplyPipelineConfigJsonInternal(ThePipelineConfiguration pipelineConfig, IEnumerable<ThePipelineConfiguration> answerConfigs, string configFileNameForLog, string[] answerFileNamesForLog)
        {
            List<Task<List<TheThing>>> pipeLineTasks = new List<Task<List<TheThing>>>();
            try
            {
                if (configFileNameForLog == null)
                {
                    configFileNameForLog = pipelineConfig?.FriendlyName ?? "Not specified";
                }
                if (pipelineConfig != null && pipelineConfig.ThingConfigurations != null)
                {
                    var pipelines = new List<ThePipelineConfiguration>();

                    if (!pipelineConfig.ThingConfigurations.Any(tc => tc.ThingSpecializationParameters == null)) // If all thing instances have specializations, add the pipeline as a config
                    {
                        pipelines.Add(pipelineConfig);
                    }

                    try
                    {
                        if (!answerConfigs?.Any() == true)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(7721, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Found no answer file for generalized config. Attempting to install the generalized config.", eMsgLevel.l2_Warning, $"File: {configFileNameForLog}. Config {pipelineConfig}"));
                            pipelines.Add(pipelineConfig);
                        }
                        int answerFileIndex = 0;
                        foreach (var answerConfig in answerConfigs)
                        {
                            var answerFileNameForLog = answerFileIndex < answerFileNamesForLog?.Length ? answerFileNamesForLog[answerFileIndex] : answerConfig.FriendlyName ?? $"Answer Config {answerFileIndex}";
                            answerFileIndex++;
                            try
                            {
                                var pipelineInstance = TheCommonUtils.DeserializeJSONStringToObject<ThePipelineConfiguration>(TheCommonUtils.SerializeObjectToJSONString(pipelineConfig)); // TODO implement Clone to avoid reparsing

                                int thingConfigIndex = 0;
                                foreach (var answerThingConfig in answerConfig.ThingConfigurations)
                                {
                                    // verify that the rest matches (or doesn't exist)
                                    if (thingConfigIndex < pipelineInstance.ThingConfigurations.Count)
                                    {
                                        if (answerThingConfig.ThingSpecializationParameters != null)
                                        {
                                            pipelineInstance.ThingConfigurations[thingConfigIndex].ThingSpecializationParameters = answerThingConfig.ThingSpecializationParameters;
                                        }
                                        else
                                        {
                                            TheBaseAssets.MySYSLOG.WriteToLog(7721, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Error processing answer file: no specialization parameters", eMsgLevel.l1_Error, $"File: {configFileNameForLog}. Answer File: {answerFileNameForLog}. Config {pipelineInstance.ThingConfigurations[thingConfigIndex]}"));
                                        }

                                        if (answerThingConfig.ThingSubscriptions != null)
                                        {
                                            var generalizedSubscriptions = pipelineInstance.ThingConfigurations[thingConfigIndex].ThingSubscriptions;
                                            int subIndex = 0;
                                            var newSubscriptions = new List<TheThingSubscription>();

                                            foreach (var answerSub in answerThingConfig.ThingSubscriptions)
                                            {
                                                if (answerSub != null)
                                                {
                                                    if (answerSub.ExtensionData.ContainsKey("Add"))
                                                    {
                                                        // Add a new subscription: remember for now, add later to not confused the subIndex logic
                                                        newSubscriptions.Add(answerSub);
                                                    }
                                                    else if (answerSub.ExtensionData.ContainsKey("Remove"))
                                                    {
                                                        // remove this subscription
                                                        generalizedSubscriptions[subIndex] = null; // Just set to null here, so the subIndex logic doesn't get confused. Will remove the null's later.
                                                        subIndex++;
                                                    }
                                                    else
                                                    {
                                                        // Update this subscription
                                                        var generalizedSub = generalizedSubscriptions[subIndex];
                                                        TheThingSubscription.SpecializeThingSubscription(answerSub, generalizedSub);
                                                        subIndex++;
                                                    }
                                                }
                                            }
                                            generalizedSubscriptions.AddRange(newSubscriptions);
                                            generalizedSubscriptions.RemoveAll(sub => sub == null);
                                        }
                                        if (answerThingConfig.SensorSubscriptions != null)
                                        {
                                            var generalizedSubscriptions = pipelineInstance.ThingConfigurations[thingConfigIndex].SensorSubscriptions;
                                            int subIndex = 0;
                                            var newSubscriptions = new List<TheSensorSubscription>();

                                            foreach (var answerSub in answerThingConfig.SensorSubscriptions)
                                            {
                                                if (answerSub != null)
                                                {
                                                    if (answerSub.ExtensionData.ContainsKey("Add"))
                                                    {
                                                        // Add a new subscription: remember for now, add later to not confused the index
                                                        newSubscriptions.Add(answerSub);
                                                    }
                                                    else if (answerSub.ExtensionData.ContainsKey("Remove"))
                                                    {
                                                        // remove this subscription
                                                        generalizedSubscriptions[subIndex] = null; // Just set to null here, so the index doesn't get confused. Will remove the null's later.
                                                        subIndex++;
                                                    }
                                                    else
                                                    {
                                                        // Update this subscription
                                                        var generalizedSub = generalizedSubscriptions[subIndex];
                                                        TheSensorSubscription.SpecializeSensorSubscription(answerSub, generalizedSub);

                                                        subIndex++;
                                                    }
                                                }
                                            }
                                            generalizedSubscriptions.AddRange(newSubscriptions);
                                            generalizedSubscriptions.RemoveAll(sub => sub == null);
                                        }

                                    }
                                    else
                                    {
                                        TheBaseAssets.MySYSLOG.WriteToLog(7721, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Error processing answer file: too many specialization parameters. Ignoring.", eMsgLevel.l1_Error, $"File: {configFileNameForLog}. Answer File: {answerFileNameForLog}. Config# {thingConfigIndex}"));
                                    }
                                    thingConfigIndex++;
                                }
                                if (answerConfig.ThingConfigurations.Count < pipelineInstance.ThingConfigurations.Count)
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(7721, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Error processing answer file: not enough specialization parameters.", eMsgLevel.l1_Error, $"File: {configFileNameForLog}. Answer File: {answerFileNameForLog}. Params {answerConfig.ThingConfigurations.Count} Expected: {pipelineInstance.ThingConfigurations.Count}"));
                                }
                                pipelines.Add(pipelineInstance);
                            }
                            catch (Exception e)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(7721, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Error processing answer file", eMsgLevel.l1_Error, $"File: {configFileNameForLog}. Answer File: {answerFileNameForLog}. {e.Message}"));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7721, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Error processing answer files", eMsgLevel.l1_Error, $"File: {configFileNameForLog}. {e.Message}"));
                    }

                    // TODO use the application host thing or specify the host thing in the pipeline config/answer file?
                    var owner = TheThingRegistry.GetBaseEngineAsThing(eEngineName.ThingService);
                    if (!owner.IsInit())
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(7720, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"ApplyConfig Files: error getting Thing engine", eMsgLevel.l1_Error, $"{pipelineConfig}"));
                    }

                    foreach (var pipeline in pipelines)
                    {
                        var thingReferenceMap = new Dictionary<string, string>();
                        foreach (var thingConfig in pipeline.ThingConfigurations)
                        {
                            var specializedThingIdentity = thingConfig.GetSpecializedThingIdentity(thingReferenceMap);

                            // First synchronously apply the configuration properties, so the plug-in does not start up with the old values
                            var tThing = new TheThingReference(specializedThingIdentity).GetMatchingThings().FirstOrDefault();
                            if (tThing != null)
                            {
                                if (!tThing.ApplyConfigurationPropertiesInternal(thingConfig, thingReferenceMap))
                                {
                                    TheBaseAssets.MySYSLOG.WriteToLog(7719, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, $"Error applying config properties", eMsgLevel.l3_ImportantMessage, $"{thingConfig}"));
                                }
                            }
                        }

                        var thingsTask = CreateThingPipelineFromConfigurationAsync(owner, pipeline);
                        pipeLineTasks.Add(thingsTask);
                    }
                }
                else
                {
                    throw new Exception($"Error parsing config file {configFileNameForLog}");
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(7721, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ThingService, "Error processing config file", eMsgLevel.l1_Error, $"File: {configFileNameForLog}. {e.Message}"));
                pipeLineTasks = null;
            }

            return pipeLineTasks;
        }

        /// <summary>
        /// Creates a ThePipelineConfiguration that only contains the configuration values needed to specialize a pipeline instance. This is typically used an answer file that instantiates a thing pipeline.
        /// </summary>
        /// <param name="pipelineConfig">The pipeline configuration to be used as the basis for the answer configuration.</param>
        /// <param name="bRemoveDefaultedValuesFromPipelineConfig">If true, any configuration values in the pipelineConfig that match the declared default value are removed from both the pipelineConfig (input) and the answer config (return value).</param>
        /// <returns>The pipeline answer configuration that only contains specialization values.</returns>
        public static ThePipelineConfiguration GeneratePipelineAnswerConfiguration(ThePipelineConfiguration pipelineConfig, bool bRemoveDefaultedValuesFromPipelineConfig)
        {
            var pipelineAnswerConfig = new ThePipelineConfiguration();
            pipelineAnswerConfig.ThingConfigurations = new List<TheThingConfiguration>();
            foreach (var thingConfig in pipelineConfig.ThingConfigurations)
            {
                var thingAnswerConfig = new TheThingConfiguration
                {
                    ThingSpecializationParameters = thingConfig.ThingSpecializationParameters,
                };
                thingConfig.ThingSpecializationParameters = new Dictionary<string, object>();
                if (bRemoveDefaultedValuesFromPipelineConfig)
                {
                    foreach (var config in thingConfig.ConfigurationValues.ToList())
                    {
                        if ((config.Value == null && config.DefaultValue == null) || (config.Value != null && TheCommonUtils.CStr(config.Value) == TheCommonUtils.CStr(config.DefaultValue)))
                        {
                            thingConfig.ConfigurationValues.Remove(config);
                        }
                        else
                        {

                        }
                        if (thingAnswerConfig.ThingSpecializationParameters.TryGetValue(config.Name, out var value))
                        {
                            if ((config.Value == null && value == null) || (config.Value != null && TheCommonUtils.CStr(config.Value) == TheCommonUtils.CStr(value)))
                            {
                                thingAnswerConfig.ThingSpecializationParameters.Remove(config.Name);
                            }
                        }

                    }
                }
                pipelineAnswerConfig.ThingConfigurations.Add(thingAnswerConfig);
            }
            return pipelineAnswerConfig;
        }

#endif

    }

}
