// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using System.IO;
using System.Text;

#if CDE_INTNEWTON
using cdeNewtonsoft.Json;
using jsonNet = cdeNewtonsoft.Json;
#else
#if CDE_JSONET
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
using jsonNet = Newtonsoft.Json;
#endif
#endif

namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// A Collection of useful helper functions used frequently in C-DEngine Solutions
    /// </summary>
    public static partial class TheCommonUtils
    {
        #region Serialization Helpers
#if CDE_JSONET
        internal static JsonSerializerOptions cdeJsonEtConfig = new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull, PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace };
#else
        internal static JsonSerializerSettings cdeNewtonJSONConfig = new ()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateParseHandling = DateParseHandling.DateTimeOffset,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };
        static JsonSerializer _objectSerializer;
#endif

        /// <summary>
        /// Serializes an object/class to JSON.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tData"></param>
        /// <returns></returns>
        public static string SerializeObjectToJSONString<T>(T tData)
        {
#if CDE_JSONET
            string t = System.Text.Json.JsonSerializer.Serialize(tData, cdeJsonEtConfig);
            return t;
#else
            if (_objectSerializer == null)
            {
                var jsonSerializer = JsonSerializer.CreateDefault(cdeNewtonJSONConfig);
                jsonSerializer.Formatting = Formatting.None;
                _objectSerializer = jsonSerializer;
            }

            StringBuilder sb = new (256);
            StringWriter sw = new (sb, CultureInfo.InvariantCulture);
            using (var jsonWriter = new JsonTextWriter(sw))
            {
                jsonWriter.Formatting = _objectSerializer.Formatting;

                _objectSerializer.Serialize(jsonWriter, tData, null);
            }

            return sw.ToString();
#endif
        }
        /// <summary>
        /// Deserializes an object from a JSON string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <returns></returns>
        public static T DeserializeJSONStringToObject<T>(string json)
        {
            if (json == null) return default;
#if CDE_JSONET
            T tDataf = System.Text.Json.JsonSerializer.Deserialize<T>(json);
            return tDataf;
#else
            T tData = JsonConvert.DeserializeObject<T>(json, cdeNewtonJSONConfig);
            return tData;
#endif
        }

        /// <summary>
        /// Uses JsonConvert.SerializeObject to serialize an object to JSON. (New in V4).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tData"></param>
        /// <returns></returns>
        public static string JsonConvertSerializeObject<T>(T tData)
        {
#if CDE_JSONET
            return System.Text.Json.JsonSerializer.Serialize(tData, cdeJsonEtConfig);
#else
            return JsonConvert.SerializeObject(tData);
#endif
        }

        internal static string SerializeObjectToJSONStringM<T>(T tData)
        {
#if CDE_JSONET
            return System.Text.Json.JsonSerializer.Serialize(tData, cdeJsonEtConfig);
#else
            return JsonConvert.SerializeObject(tData, Formatting.None, cdeNewtonJSONConfig);
#endif
        }

        /// <summary>
        /// Retrieves a value from a parsed JSON data structure based on a JSON path lookup/query. Obtain the parsed JSON using TheCommonUtils.DeserializeJSONStringToObject(). JSON Path details follow those in NewtonSoft's JObject.SelectToken().
        /// </summary>
        /// <param name="parsedJson"></param>
        /// <param name="jsonPath"></param>
        /// <returns></returns>
        public static object GetJSONValueByPath(object parsedJson, string jsonPath)
        {
#if CDE_JSONET
            JsonObject jsonJObject = parsedJson as JsonObject;
            return jsonJObject?.SelectToken(jsonPath);
#else
            var jsonJObject = parsedJson as jsonNet.Linq.JObject;
            return jsonJObject?.SelectToken(jsonPath);
#endif
        }

#if CDE_JSONET
        internal static void SerializeObjectToJSONFileInternal<T>(string fileName, T tData)
        {
            using (FileStream fs = File.Create(fileName))
            {
                JsonSerializer.Serialize(fs, tData, cdeJsonEtConfig);
            }
        }
#else
        static JsonSerializer _fileSerializer;
        // This function does not verify that the fileName is under the ClientBin directory. Internal use only and only when fileName is known to be under ClientBin!
        internal static void SerializeObjectToJSONFileInternal<T>(string fileName, T tData)
        {
            using (var writeFile = new System.IO.StreamWriter(fileName, false))
            {
                _fileSerializer ??= new JsonSerializer
                    {
                        Formatting = cdeNewtonJSONConfig.Formatting,
                        ReferenceLoopHandling = cdeNewtonJSONConfig.ReferenceLoopHandling,
                        //StringEscapeHandling = cdeNewtonJSONConfig.StringEscapeHandling,
                        DateFormatHandling = cdeNewtonJSONConfig.DateFormatHandling,
                        NullValueHandling = cdeNewtonJSONConfig.NullValueHandling,
                        ObjectCreationHandling = cdeNewtonJSONConfig.ObjectCreationHandling,
                        MissingMemberHandling = cdeNewtonJSONConfig.MissingMemberHandling,
                        //Context = new System.Runtime.Serialization.StreamingContext(System.Runtime.Serialization.StreamingContextStates.All, true),
                    };
                _fileSerializer.Serialize(writeFile, tData);
                writeFile.Flush();
            }
        }
#endif
#endregion

    }
}
