// SPDX-FileCopyrightText: Copyright (c) 2009-2026 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;

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
        internal static JsonSerializerOptions cdeJsonEtConfig = new JsonSerializerOptions 
        { 
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault, 
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            PropertyNameCaseInsensitive = true,
            IncludeFields =true,
            MaxDepth=64,
            AllowTrailingCommas=true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString ,
            Converters = { new ObjectToInferredTypesConverter() }
        };
        internal static JsonSerializerOptions cdeJsonEtConfigNoCon = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            RespectNullableAnnotations=false,
            MaxDepth = 64,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };

        internal class ObjectToInferredTypesConverter : JsonConverter<object>
        {
            public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                // Check if the current JSON token is explicitly null
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return null; // Directly returns C# null, avoiding JsonElement creation
                }

                if (typeToConvert == typeof(string))
                {
                    return reader.TokenType switch
                    {
                        JsonTokenType.String => reader.GetString(),
                        _ => JsonDocument.ParseValue(ref reader).RootElement.GetRawText()
                    };
                }
                else
                {
                    return reader.TokenType switch
                    {
                        JsonTokenType.String => reader.GetString(),
                        JsonTokenType.True => true,
                        JsonTokenType.False => false,
                        JsonTokenType.Number => reader.TryGetInt64(out long l) ? l : reader.GetDouble(),
                        _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
                    };
                }
            }

            public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
                JsonSerializer.Serialize(writer, value, value.GetType(), cdeJsonEtConfigNoCon);
        }
        internal class cdeStringConverter : JsonConverter<string>
        {
            public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                // If it's a number, convert it to a string directly
                if (reader.TokenType == JsonTokenType.Number)
                {
                    using (var doc = JsonDocument.ParseValue(ref reader))
                    {
                        return doc.RootElement.ToString();
                    }
                }

                // If it's a boolean, null, or other primitive, you can expand this block if needed
                if (reader.TokenType == JsonTokenType.String)
                {
                    return reader.GetString();
                }

                // Fallback default behavior
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    return doc.RootElement.ToString();
                }
            }

            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value);
            }
        }
        extension(JsonNode node)
        {
            private JsonNode? SelectToken(string path)
            {
                if (node == null || string.IsNullOrWhiteSpace(path)) return null;

                string[] parts = path.Split('.');
                JsonNode? current = node;

                foreach (var part in parts)
                {
                    // Safely navigate into the JsonObject using the indexer
                    current = current?[part];

                    if (current == null) return null; // Path segment does not exist
                }

                return current;
            }
        }

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

            System.Text.StringBuilder sb = new (256);
            StringWriter sw = new (sb, System.Globalization.CultureInfo.InvariantCulture);
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
            T tDataf = System.Text.Json.JsonSerializer.Deserialize<T>(json, cdeJsonEtConfig);
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
