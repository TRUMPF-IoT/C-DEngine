// SPDX-FileCopyrightText: Copyright (c) 2009-2025 TRUMPF Laser GmbH, authors: C-Labs, Hyviva
//
// SPDX-License-Identifier: MPL-2.0
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;
using NMI = nsCDEngine.Engines.NMIService.TheNMIEngine;
using TT = nsCDEngine.Engines.ThingService.TheThing;

namespace nsCDEngine.Engines.ThingService
{
    public class TheDeviceDescription
    {
        public List<string> Variables { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public Dictionary<string, TheDeviceTagMapping> TagMappings { get; set; }

        public TheDeviceDescription()
        {
        }

        /// <summary>
        /// Sends a Template to an Engine/Plugin
        /// </summary>
        /// <param name="pSenderThing">Who is sending it</param>
        /// <param name="pEngineName">The name of the engine</param>
        /// <param name="pDeviceTemplate">The device template</param>
        public static void SendTemplateToEngine(ICDEThing pSenderThing, string pEngineName, TheDeviceDescription pDeviceTemplate)
        {
            var tsm = new TSM(pEngineName, "CDE_CBYT", CU.SerializeObjectToJSONString(pDeviceTemplate));
            var enbase = TheThingRegistry.GetBaseEngine(pEngineName);
            enbase?.GetBaseThing()?.HandleMessage(pSenderThing, new TheProcessMessage { Message = tsm });
        }

        /// <summary>
        /// Gets a device template from disk
        /// </summary>
        /// <param name="MyBaseThing">Owner Thing</param>
        /// <param name="TemplateName">Name of the Template</param>
        /// <returns></returns>
        public static TheDeviceDescription GetDeviceTemplate(TT MyBaseThing, string TemplateName)
        {
            var newFileName = CU.cdeFixupFileName($"/Templates/{TemplateName}.cdeTemplate");
            if (File.Exists(newFileName))
            {
                try
                {
                    var testJSON = File.ReadAllText(newFileName);
                    return CU.DeserializeJSONStringToObject<TheDeviceDescription>(testJSON);
                }
                catch (Exception ee)
                {
                    MyBaseThing?.MyThingBase?.SetMessage($"Error during Modbus Template:{ee}", DateTimeOffset.Now);
                }
            }
            return null;
        }

        /// <summary>
        /// Check if a template was in the message
        /// </summary>
        /// <param name="MyBaseThing">Owner Thing</param>
        /// <param name="pMsg">TSM with the message.</param>
        /// <returns>If a TXT with "CDE_CBYT" is found and the DeviceType+Parent does not exist a DevicDescription is returned</returns>
        public static TheDeviceDescription CheckForTemplate(TT MyBaseThing, TSM pMsg)
        {
            if (MyBaseThing == null || pMsg == null || string.IsNullOrEmpty(pMsg.PLS))
                return null;
            string[] cmd = pMsg.TXT.Split(':');
            if (cmd[0] != "CDE_CBYT")
                return null;
            try
            {
                var temp2 = CU.DeserializeJSONStringToObject<TheDeviceDescription>(pMsg.PLS);
                var tt = TheThingRegistry.GetThingByFunc(MyBaseThing.EngineName, s => s.DeviceType == CU.CStr(temp2.Properties["DeviceType"]) && TT.GetSafePropertyString(s,"ParentID") == CU.CStr(temp2.Properties["ParentID"]));
                if (tt == null)
                    return temp2;
            }
            catch (Exception ee)
            {
                MyBaseThing?.MyThingBase?.SetMessage($"Error during Check for Template:{ee}", DateTimeOffset.Now);
            }
            return null;
        }

        /// <summary>
        /// Creates a new device template and stores it on disk
        /// </summary>
        /// <typeparam name="T">Type of the Storage Mirror that holds tags</typeparam>
        /// <param name="MyBaseThing">Owner Thing</param>
        /// <param name="TemplateName">Template Name</param>
        /// <param name="pSubStores"></param>
        /// <returns></returns>
        public static string CreateDeviceTemplate<T>(TT MyBaseThing, string TemplateName, Dictionary<string, TheStorageMirror<T>> pSubStores)
                                                    where T : TheDataBase, System.ComponentModel.INotifyPropertyChanged, new()
        {
            var templ = new TheDeviceDescription { };
            var l = MyBaseThing.GetAllProperties();
            templ.Properties = l.ToDictionary(prop => prop.Name, prop => prop.Value);
            if (pSubStores?.Count > 0)
            {
                templ.TagMappings = new Dictionary<string, TheDeviceTagMapping>();
                foreach (var subStore in pSubStores)
                {
                    var tagMap = new TheDeviceTagMapping
                    {
                        Name = subStore.Key,
                        FieldList = new List<Dictionary<string, object>>()
                    };

                    foreach (var field in subStore.Value.TheValues.ToList())
                    {
                        var fieldDict = TheDeviceTagMapping.ClassToBag(field);
                        tagMap.FieldList.Add(fieldDict);
                    }

                    templ.TagMappings[subStore.Key] = tagMap;
                }
            }

            string testJSON = CU.SerializeObjectToJSONString(templ);
            var newFileName = CU.cdeFixupFileName($"/Templates/{TemplateName}.cdeTemplate");
            CU.CreateDirectories(newFileName);
            File.WriteAllText(newFileName, testJSON);
            return testJSON;
        }
    }

    public class TheDeviceTagMapping
    {
        public string Name { get; set; }
        public List<Dictionary<string, object>> FieldList { get; set; }

        public TheDeviceTagMapping()
        {
            FieldList = new List<Dictionary<string, object>>();
        }

        public static Dictionary<string, object> ClassToBag<T>(T obj) where T : new()
        {
            var dict = new Dictionary<string, object>();
            var props = typeof(T).GetProperties();
            foreach (var prop in props)
            {
                if (prop.Name.StartsWith("cde"))
                    continue;
                var value = prop.GetValue(obj);
                dict[prop.Name] = value;
            }
            return dict;
        }

        public static T BagToClass<T>(Dictionary<string, object> properties) where T : new()
        {
            var obj = new T();
            var props = typeof(T).GetProperties();
            foreach (var prop in props)
            {
                if (properties.ContainsKey(prop.Name))
                {
                    var value = properties[prop.Name];
                    if (value != null && prop.PropertyType.IsAssignableFrom(value.GetType()))
                    {
                        prop.SetValue(obj, value);
                    }
                    else if (value != null)
                    {
                        try
                        {
                            var convertedValue = System.Convert.ChangeType(value, prop.PropertyType);
                            prop.SetValue(obj, convertedValue);
                        }
                        catch
                        {
                            // Handle conversion error if necessary
                        }
                    }
                }
            }
            return obj;
        }
    }
}
