// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Globalization;
using System.IO;
using System.Text;

#if CDE_INTNEWTON
using cdeNewtonsoft.Json;
using jsonNet = cdeNewtonsoft.Json;
#else
using Newtonsoft.Json;
using jsonNet = Newtonsoft.Json;
#endif

namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// A Collection of useful helper functions used frequently in C-DEngine Solutions
    /// </summary>
    public static partial class TheCommonUtils
    {
#region Serialization Helpers
        internal static JsonSerializerSettings cdeNewtonJSONConfig = new ()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateParseHandling = DateParseHandling.DateTimeOffset,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        /// <summary>
        /// Serializes an object/class to JSON.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tData"></param>
        /// <returns></returns>
        public static string SerializeObjectToJSONString<T>(T tData)
        {
#if CDE_MEADOW
            if (IsMeadowFeather())
            {
                Console.WriteLine($"B4 SerJSON: {tData}");
                string t = System.Text.Json.JsonSerializer.Serialize(tData);
                Console.WriteLine($"After SerJSON: {tData}");
                return t;
            }
#endif
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
        }
        static JsonSerializer _objectSerializer;
        /// <summary>
        /// Deserializes an object from a JSON string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <returns></returns>
        public static T DeserializeJSONStringToObject<T>(string json)
        {
            if (json == null) return default;
#if CDE_MEADOW
            if (IsMeadowFeather())
            {
                Console.WriteLine($"B4 DeSerJSON");
                T tDataf = System.Text.Json.JsonSerializer.Deserialize<T>(json);
                Console.WriteLine($"After DeSerJSON");
                return tDataf;
            }
#endif
            T tData = JsonConvert.DeserializeObject<T>(json, cdeNewtonJSONConfig);
            return tData;
        }

        /// <summary>
        /// Uses JsonConvert.SerializeObject to serialize an object to JSON. (New in V4).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tData"></param>
        /// <returns></returns>
        public static string JsonConvertSerializeObject<T>(T tData)
        {
            return JsonConvert.SerializeObject(tData);
        }

        internal static string SerializeObjectToJSONStringM<T>(T tData)
        {
            return JsonConvert.SerializeObject(tData, Formatting.None, cdeNewtonJSONConfig);
        }

        /// <summary>
        /// Retrieves a value from a parsed JSON data structure based on a JSON path lookup/query. Obtain the parsed JSON using TheCommonUtils.DeserializeJSONStringToObject(). JSON Path details follow those in NewtonSoft's JObject.SelectToken().
        /// </summary>
        /// <param name="parsedJson"></param>
        /// <param name="jsonPath"></param>
        /// <returns></returns>
        public static object GetJSONValueByPath(object parsedJson, string jsonPath)
        {
            var jsonJObject = parsedJson as jsonNet.Linq.JObject;
            return jsonJObject?.SelectToken(jsonPath);
        }

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
#if !CDE_STANDARD
                writeFile.Close();
#endif
            }
        }
#endregion

    }
}
