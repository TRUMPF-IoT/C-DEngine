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
using System.Text.Json.Serialization.Metadata;
using System.Runtime.Serialization;
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
        /// <summary>
        /// System.Text.Json does not honor [IgnoreDataMember]. This resolver removes any member carrying that attribute
        /// from serialization and deserialization so that shortcut properties (e.g. TheThing.Address) do not overwrite MyPropertyBag.
        /// </summary>
        internal static readonly IJsonTypeInfoResolver cdeJsonEtTypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { IgnoreDataMemberModifier, NullToDefaultModifier }
        };

        private static void IgnoreDataMemberModifier(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
                return;
            for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
            {
                var prop = typeInfo.Properties[i];
                if (prop.AttributeProvider?.IsDefined(typeof(IgnoreDataMemberAttribute), true) == true)
                    typeInfo.Properties.RemoveAt(i);
            }
        }

        /// <summary>
        /// A JSON null must not overwrite a member: the member keeps its constructor/initializer default.
        /// Non-nullable value types never reach the setter with null; they are handled by cdeNullToDefaultConverterFactory.
        /// </summary>
        private static void NullToDefaultModifier(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
                return;
            foreach (var prop in typeInfo.Properties)
            {
                var set = prop.Set;
                if (set == null)
                    continue;
                prop.Set = (obj, value) =>
                {
                    if (value != null)
                        set(obj, value);
                };
            }
        }

        internal static JsonSerializerOptions cdeJsonEtConfig = new JsonSerializerOptions
        {
            TypeInfoResolver = cdeJsonEtTypeInfoResolver,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            MaxDepth = 64,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString|JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters = { new ObjectToInferredTypesConverter(), new cdeGuidConverter(), new cdeStringConverter(), new cdeBooleanConverter(), new cdeNullToDefaultConverterFactory() }
        };
        internal static JsonSerializerOptions cdeJsonEtConfigStrict = new JsonSerializerOptions 
        { 
            TypeInfoResolver = cdeJsonEtTypeInfoResolver,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault, 
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            PropertyNameCaseInsensitive = true,
            IncludeFields =true,
            MaxDepth=64,
            AllowTrailingCommas=true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters = { new ObjectToInferredTypesConverter() } 
        };
        internal static JsonSerializerOptions cdeJsonEtConfigNoCon = new JsonSerializerOptions
        {
            TypeInfoResolver = cdeJsonEtTypeInfoResolver,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            RespectNullableAnnotations=false,
            MaxDepth = 64,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters = { new cdeNullToDefaultConverterFactory() }
        };
        internal static JsonSerializerOptions cdeJsonEtConfigNMI = new JsonSerializerOptions
        {
            TypeInfoResolver = cdeJsonEtTypeInfoResolver,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            RespectNullableAnnotations = false,
            MaxDepth = 64,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters = { new cdeNullToDefaultConverterFactory() }
        };

        /// <summary>
        /// Makes non-nullable value types (int, double, DateTimeOffset, enums, ...) tolerate a JSON null by returning default(T)
        /// instead of throwing. All other tokens are delegated to the built-in converter.
        /// </summary>
        internal class cdeNullToDefaultConverterFactory : JsonConverterFactory
        {
            private static readonly JsonSerializerOptions BuiltInOptions = new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals
            };

            private static readonly System.Collections.Generic.HashSet<Type> SupportedTypes = new()
            {
                typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid),
                typeof(DateOnly), typeof(TimeOnly), typeof(Half), typeof(Int128), typeof(UInt128)
            };

            public override bool CanConvert(Type typeToConvert)
            {
                // Only built-in scalar value types; user-defined structs (e.g. StorageGetRequest) must go through the regular object converter
                return typeToConvert.IsPrimitive || typeToConvert.IsEnum || SupportedTypes.Contains(typeToConvert);
            }

            public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            {
                var inner = BuiltInOptions.GetConverter(typeToConvert);
                return (JsonConverter)Activator.CreateInstance(typeof(NullToDefaultConverter<>).MakeGenericType(typeToConvert), inner);
            }

            private class NullToDefaultConverter<T> : JsonConverter<T> where T : struct
            {
                private readonly JsonConverter<T> _inner;

                public NullToDefaultConverter(JsonConverter inner)
                {
                    _inner = (JsonConverter<T>)inner;
                }

                public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                {
                    if (reader.TokenType == JsonTokenType.Null)
                        return default;
                    return _inner.Read(ref reader, typeToConvert, options);
                }

                public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
                {
                    _inner.Write(writer, value, options);
                }

                public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                {
                    return _inner.ReadAsPropertyName(ref reader, typeToConvert, options);
                }

                public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
                {
                    _inner.WriteAsPropertyName(writer, value, options);
                }
            }
        }

        internal class cdeBooleanConverter : JsonConverter<bool>
        {
            public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                // Handle native JSON boolean literals: true or false
                if (reader.TokenType == JsonTokenType.True) return true;
                if (reader.TokenType == JsonTokenType.False) return false;
                if (reader.TokenType == JsonTokenType.Null) return default;

                // Handle string values: "true" or "false"
                if (reader.TokenType == JsonTokenType.String)
                {
                    string? value = reader.GetString();

                    if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    throw new JsonException($"Cannot convert string value '{value}' to a boolean.");
                }

                // Fallback for unexpected token types
                throw new JsonException($"Unexpected token type '{reader.TokenType}' when parsing a boolean.");
            }

            public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
            {
                // Always write out standard JSON boolean literals
                writer.WriteBooleanValue(value);
            }
        }

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
        public class cdeStringConverter : JsonConverter<string>
        {
            public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return null;
                }

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
        public class cdeGuidConverter : JsonConverter<Guid>
        {
            public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return default;
                }

                // Try reading it via default behavior first for optimal performance
                if (reader.TryGetGuid(out Guid guid))
                {
                    return guid;
                }

                // Fallback: Parse the raw string value (handles braces, spaces, etc.)
                string? rawValue = reader.GetString();
                if (Guid.TryParse(rawValue, out Guid parsedGuid))
                {
                    return parsedGuid;
                }

                throw new JsonException($"The JSON value '{rawValue}' cannot be converted to a System.Guid.");
            }

            public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
            {
                // Writes it back as a standard hyphenated string (without braces)
                writer.WriteStringValue(value.ToString());
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
            return System.Text.Json.JsonSerializer.Serialize(tData, cdeJsonEtConfigNMI);
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

        internal static string SerializeObjectToJSONStringM<T>(T tData)
        {
#if CDE_JSONET
            return System.Text.Json.JsonSerializer.Serialize(tData, cdeJsonEtConfigNoCon);
#else
            return JsonConvert.SerializeObject(tData, Formatting.None, cdeNewtonJSONConfig);
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
            if (string.IsNullOrEmpty(json)) return default;
#if CDE_JSONET
            T tDataf = System.Text.Json.JsonSerializer.Deserialize<T>(json, TheBaseAssets.MyServiceHostInfo.UseStrictJSON ? cdeJsonEtConfigStrict : cdeJsonEtConfig);
            return tDataf;
#else
            T tData = JsonConvert.DeserializeObject<T>(json, cdeNewtonJSONConfig);
            return tData;
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
