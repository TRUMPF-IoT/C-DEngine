// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataLogUtilities
{
    public class DataLogComparer
    {
        public static void SanitizeP08Data(List<OpcClientDataLogEntry> opcTags, List<OpcClientDataLogEntry> meshTags)
        {
            foreach (var t in opcTags)
            {
                t.Type = null;
                t.ReceiveTime = default(DateTimeOffset);
                if (t.TagId == "integer Array" && t.value != null)
                {
                    t.value = TheCommonUtils.SerializeObjectToJSONString(t.value).Replace("[", "{").Replace(",", " |").Replace("]", "}");
                }

                //            // -		value	{{
                //            "Body": {
                //                "ByteString": {
                //                    "@xmlns": "http://opcfoundation.org/UA/2008/02/Types.xsd",
                //  "#text": "AQw="
                //                }
                //            }
                //        }
                //    }
                //    object {cdeNewtonsoft.Json.Linq.JObject
                //}

                if (t.value?.GetType().Name == "JObject")
                {
                    t.value = new Dictionary<string, object> { { "IsAvailable", true }, { "QualityValue", 12 } };
                }
            }
            foreach (var t in meshTags)
            {
                //if ((t.value as String)?.Contains("QualityValue") == true)
                //{
                //    var valueDict = TheCommonUtils.DeserializeJSONStringToObject<Dictionary<string,object>>(t.value as string);
                //    valueDict["QualityValue"] = 12;
                //    t.value = TheCommonUtils.SerializeObjectToJSONString(valueDict);
                //}
                if (t.value?.GetType().Name == "JObject")
                {
                    t.value = new Dictionary<string, object> { { "IsAvailable", true }, { "QualityValue", 12 } };
                }

            }
        }

        public class VerifierStats
        {
            public int opc35RunningOutOfSequence { get; set; }
            public int duplicateOPCTagCount { get; set; }
            public int duplicateMeshTagCount { get; set; }
            public int missingOpcTags { get; set; }
            public int extraMeshTags { get; set; }
            public string outputPathPrefix { get; set; }
        }
        public static VerifierStats CompareDataLogs<T>(string opcClientDataPath, string meshDataPath, string outputPath, Func<List<MeshSenderDataLogEntry<T>>, TimeSpan, List<OpcClientDataLogEntry>>converter, TimeSpan timeZoneOffset, int toleranceInTicks = 0, Action<List<OpcClientDataLogEntry>, List<OpcClientDataLogEntry>> sanitizeDataCallback = null)
        {
            var stats = new VerifierStats();

            var opcTags = DataLogParser.ReadOpcClientDataLog(opcClientDataPath);

            var opc35Running = opcTags.Where(t => t.TagId == "35.Running").ToList();
            var opc35RunningOutOfSequence = new List<OpcClientDataLogEntry>();
            int lastAdded = -1;
            for (int i=1; i< opc35Running.Count; i++)
            {
                if (TheCommonUtils.CStr(opc35Running[i].value) == TheCommonUtils.CStr(opc35Running[i - 1].value))
                {
                    if (lastAdded != i - 1)
                    {
                        opc35RunningOutOfSequence.Add(opc35Running[i - 1]);
                    }
                    opc35RunningOutOfSequence.Add(opc35Running[i]);
                    lastAdded = i;
                }
            }
            stats.opc35RunningOutOfSequence = opc35RunningOutOfSequence.Count;

            var meshEntries = DataLogParser.ReadJsonOPCDataLog<T>(meshDataPath);
            var meshTags = converter(meshEntries, timeZoneOffset);
            //var meshTags = DataLogParser.ConvertMeshSenderDataToOpcTags(meshEntries, timeZoneOffset).Where(mTag => mTag.TagId != "DeviceGateLog").ToList();

            sanitizeDataCallback?.Invoke(opcTags, meshTags);

            string filePrefix = outputPath != null ? Path.Combine(outputPath, DateTime.Now.ToString("yyyyMMdd_hhmmss")) : null;
            stats.outputPathPrefix = filePrefix;
            //File.WriteAllText($"{filePrefix}allmesh", TheCommonUtils.SerializeObjectToJSONString(meshTags.OrderBy(e => e.Source.ToString("yyyyMMddhhmmss")+"/"+e.TagId), true));
            //File.WriteAllText($"{filePrefix}allopc", TheCommonUtils.SerializeObjectToJSONString(opcTags.OrderBy(e => e.Source.ToString("yyyyMMddhhmmss") + "/" + e.TagId), true));
            if (filePrefix != null)
            {
                File.WriteAllText($"{filePrefix}opc35runningoutofsequence.log", TheCommonUtils.SerializeObjectToJSONString(opc35RunningOutOfSequence.OrderBy(e => e.Source.ToString("yyyyMMddhhmmss") + "/" + e.TagId)).Replace("},{", "},\r\n{"));
            }

            var uniqueOPCTags = opcTags.Distinct(new CompareOpcTagsP08()).ToList();
            stats.duplicateOPCTagCount = opcTags.Count - uniqueOPCTags.Count;

            var duplicateOPCTags = opcTags.Except(uniqueOPCTags).ToList(); // uses object identity comparison
            if (filePrefix != null)
            {
                File.WriteAllText($"{filePrefix}opcduplicate.log", TheCommonUtils.SerializeObjectToJSONString(duplicateOPCTags.OrderBy(e => e.Source.ToString("yyyyMMddhhmmss") + "/" + e.TagId)).Replace("},{", "},\r\n{"));
            }

            var uniqueMeshTags = meshTags.Distinct(new CompareOpcTagsP08()).ToList();
            //File.WriteAllText($"{filePrefix}uniquemesh", TheCommonUtils.SerializeObjectToJSONString(uniqueMeshTags.OrderBy(e => e.Source.ToString("yyyyMMddhhmmss") + "/" + e.TagId), true));

            stats.duplicateMeshTagCount = meshTags.Count - uniqueMeshTags.Count;
            var duplicateMeshTags = meshTags.Except(uniqueMeshTags).ToList(); // uses object identity comparison
            if (filePrefix != null)
            {
                File.WriteAllText($"{filePrefix}meshduplicate.log", TheCommonUtils.SerializeObjectToJSONString(duplicateMeshTags.OrderBy(e => e.Source.ToString("yyyyMMddhhmmss") + "/" + e.TagId)).Replace("},{", "},\r\n{"));
            }

            var comparer = new CompareOpcTagsP08(toleranceInTicks);
            var missingOpcTags = opcTags.Where(oTag => !uniqueMeshTags.Contains(oTag, comparer)).ToList();
            if (filePrefix != null)
            {
                File.WriteAllText($"{filePrefix}missingopc.log", TheCommonUtils.SerializeObjectToJSONString(missingOpcTags.OrderBy(e => e.Source.ToString("yyyyMMddhhmmss") + "/" + e.TagId)).Replace("},{", "},\r\n{"));
            }
            var opcTagsClone = opcTags.ToList();
            var extraMeshTags = meshTags.Where(mTag =>
            {
                return !opcTags.Contains(mTag, comparer);
                // possible optimization: remove tags that have already been found, but have to consider entries that apply multiple times (for props-of-props like [tag].[statusCode])
                //var oTag = opcTags.FirstOrDefault(t => comparer.Equals(t, mTag));
                //if (oTag != null)
                //{
                //    //opcTagsClone.Remove(oTag);
                //}
                //return oTag == null;
                }).ToList();
            if (filePrefix != null)
            {
                File.WriteAllText($"{filePrefix}extramesh.log", TheCommonUtils.SerializeObjectToJSONString(extraMeshTags.OrderBy(e => e.Source.ToString("yyyyMMddhhmmss") + "/" + e.TagId)).Replace("},{", "},\r\n{"));
            }
            stats.missingOpcTags = missingOpcTags.Count;
            stats.extraMeshTags = extraMeshTags.Count;
            return stats;
        }

    }
}
