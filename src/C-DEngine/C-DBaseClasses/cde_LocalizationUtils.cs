// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Text;

namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// A Collection of useful helper functions used frequently in C-DEngine Solutions
    /// </summary>
    public interface ILocalizationHooks
    {
        /// <summary>
        /// Returns a localized String from an engine resource by its key
        /// </summary>
        /// <param name="lcid">Desired LCID of the string to be returned</param>
        /// <param name="engine">Plugin/Engine name where to locate the key in</param>
        /// <param name="keyOrString">Key of the resource to retreive</param>
        /// <param name="IsKey">If true, the key is an index</param>
        /// <returns></returns>
        string GetLocalizedStringByKey(int lcid, string engine, string keyOrString, bool IsKey = false);
    }

    /// <summary>
    /// The default implementation of the ILocalizationHooks
    /// </summary>
    public class TheDefaultLocalizationUtils : ILocalizationHooks
    {
        internal bool DoPseudoLoc { get; set; }
        private bool _createTextLog;
        private bool _firstRun = true;

        /// <summary>
        /// </summary>
        /// <param name="lcid"></param>
        /// <param name="engine"></param>
        /// <param name="keyOrString"></param>
        /// <param name="IsKey"></param>
        /// <returns></returns>
        public string GetLocalizedStringByKey(int lcid, string engine, string keyOrString, bool IsKey = false)
        {
            if (string.IsNullOrEmpty(keyOrString)) return keyOrString;

            string[] originalLineParts;
            if (IsKey)
            {
                originalLineParts = new string[1];
                originalLineParts[0] = keyOrString;
            }
            else
            {
                if (!keyOrString.Contains("###"))
                {
                    if (_firstRun)
                    {
                        _createTextLog = TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("CreateTextLog"));
                        _firstRun = false;
                    }
                    if (_createTextLog && !string.IsNullOrEmpty(keyOrString))
                    {
                        CreateFileWithAllStringsNotFormattedForLocalization(engine, keyOrString);
                    }
                    return keyOrString;
                }
                originalLineParts = TheCommonUtils.cdeSplit(keyOrString, "###", false, false);
                if (originalLineParts == null) return keyOrString;
            }

            var localizedLine = "";
            if (originalLineParts.Length > 2) // contains localizable elements
            {
                localizedLine = BuildLocalizedLine(engine, lcid, originalLineParts);
            }
            else // contains only the localization key (IsKey param is not set to true)
            {
                var rm = GetStringResourceManager(engine);

                var localizationKey = originalLineParts.Length == 2
                    ? originalLineParts[1]
                    : TheCommonUtils.cdeSplit(originalLineParts[1], "#", false, false)[1];

                if (localizationKey == null || rm == null) return null;

                var localizedText = GetText(localizationKey, lcid, rm);
                if (localizedText != null)
                {
                    localizedLine = localizedText;
                }
            }
            return localizedLine;
        }

        private static string BuildLocalizedLine(string engine, int lcid, IList<string> originalLineParts)
        {
            var localizableParts = GetLocalizableParts(originalLineParts);

            var sb = new StringBuilder();
            var count = 0;
            foreach (var part in originalLineParts)
            {
                if (count>=localizableParts.Count || part != localizableParts[count])
                {
                    sb.Append(part);
                    continue;
                }
                var rm = GetStringResourceManager(GetEngine(engine, part));
                var key = GetKey(part);
                var tempLocStr = GetText(key, lcid, rm);
                if (string.IsNullOrEmpty(tempLocStr) || tempLocStr==key)
                    tempLocStr = GetFallbackText(part);
                sb.Append(tempLocStr);
                count++;
            }
            return sb.ToString();
        }

        private static List<string> GetLocalizableParts(IList<string> originalLineParts)
        {
            var index = 0;
            var localizableParts = new List<string>();
            foreach (var part in originalLineParts)
            {
                if (index != originalLineParts.Count && IsOdd(index))
                {
                    localizableParts.Add(part);
                }
                index++;
            }
            return localizableParts;
        }

        /// <summary>
        /// Determines whether or no an integer value is odd.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool IsOdd(int value)
        {
            return value % 2 != 0;
        }

        private static bool NoManifestResources = false;    //Optimization to avoid Exception (very expensive on Core!)
        /// <summary>
        ///
        /// </summary>
        /// <param name="key"></param>
        /// <param name="lcid"></param>
        /// <param name="rm"></param>
        /// <returns></returns>
        private static string GetText(string key, int lcid, ResourceManager rm)
        {
            if (NoManifestResources)
                return key;
            string text;
            try
            {
                if (lcid == 0)
                    lcid = 1033;
                var tCult = new CultureInfo(lcid);
                text = rm?.GetString(key, tCult);
            }
            catch (System.Resources.MissingManifestResourceException)
            {
                NoManifestResources = true;
                text = key;
            }
            catch (FileNotFoundException)
            {
                NoManifestResources = true;
                text = key;
            }
            catch
            {
                text = key;
            }
            return text;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="localizationElement"></param>
        /// <returns></returns>
        private static string GetKey(string localizationElement)
        {
            if (!localizationElement.Contains("#")) return localizationElement;

            var locElementParams = TheCommonUtils.cdeSplit(localizationElement, "#", false, false);
            var key = locElementParams.Length == 3
                ? locElementParams[1]
                : locElementParams[0];

            return key;
        }

        /// <summary>
        /// Finds the engine that contains the resource file in use.
        /// Attempts to use the engine defined as a parameter in the first localization string.
        /// It's assumed that all other localization strings in the list are using the same engine.
        /// If no engine parameter is defined, the engine provided by the CDEngine is used.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="localizableElement"></param>
        /// <returns></returns>
        private static string GetEngine(string engine, string localizableElement)
        {
            if (!localizableElement.Contains("#")) return engine;

            var locElementParams = TheCommonUtils.cdeSplit(localizableElement, "#", false, false);
            var engineParamDefined = locElementParams.Length == 3 && !locElementParams[0].Equals("");

            return engineParamDefined ? locElementParams[0] : engine;
        }

        /// <summary>
        /// Finds the fall back UI text, as defined in the localization element string.
        /// ex./ ###CDMyArithmetics.ArithmeticsService#Addaarithmeticoperation385660#Add a arithmetic operation###
        /// "Add a arithmetic operation" is the fall back UI text parameter in the localization string.
        /// </summary>
        /// <param name="localizationElement"></param>
        /// <returns></returns>
        private static string GetFallbackText(string localizationElement)
        {
            if (!localizationElement.Contains("#")) return localizationElement;

            var locElementParams = TheCommonUtils.cdeSplit(localizationElement, "#", false, false);
            var fallbackText = locElementParams.Length == 3
                ? locElementParams[2]
                : locElementParams[1];

            return fallbackText;
        }

        private static void CreateFileWithAllStringsNotFormattedForLocalization(string engine, string originalString)
        {
            string fileName = TheCommonUtils.cdeFixupFileName(@"Localization\Workflow\need-to-be-formatted.txt");
            TheCommonUtils.CreateDirectories(fileName);
            CreateFileForLocalizationKeyWords(fileName);
            WriteKeywordToFile(fileName, engine + " - " + originalString);
        }

        private static void CreateFileForLocalizationKeyWords(string fileName)
        {
            if (File.Exists(fileName)) return;
            using (var fs = File.Create(fileName))
            {
                var title = new UTF8Encoding(true).GetBytes("Localization Key Words:");
                fs.Write(title, 0, title.Length);
            }
        }

        private static readonly object FileLock = new object();
        private static void WriteKeywordToFile(string fileName, string keyword)
        {
            lock (FileLock)
            {
                using (var file = new StreamWriter(fileName, true))
                {
                    file.WriteLine(keyword);
                }
            }
        }


        private static readonly cdeConcurrentDictionary<string, ResourceManager> ResourceManagersPerEngine = new cdeConcurrentDictionary<string, ResourceManager>();

        /// <summary>
        ///
        /// </summary>
        /// <param name="engine"></param>
        /// <returns></returns>
        private static ResourceManager GetStringResourceManager(string engine)
        {
            ResourceManager rm=null;
            if (!string.IsNullOrEmpty(engine) && ResourceManagersPerEngine.TryGetValue(engine, out rm)) return rm;
            if (string.IsNullOrEmpty(engine) || !TheBaseAssets.MyCDEPluginTypes.TryGetValue(engine, out Type plugInType))
            {
                engine = "cdeEngine";
                if (ResourceManagersPerEngine.TryGetValue(engine, out rm)) return rm;
                plugInType = typeof(TheCommonUtils);
            }
            try
            {
                rm = new ResourceManager("cdeStrings", plugInType.Assembly);
            }
            catch (Exception ex)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(1, TSM.L(eDEBUG_LEVELS.OFF)
                    ? null
                    : new TSM(eEngineName.NMIService, "Error creating resource manager", eMsgLevel.l2_Warning, $"cdeStrings, {plugInType.Name}: {ex}"));
            }
            if (rm != null)
            {
                ResourceManagersPerEngine.TryAdd(engine, rm);
            }
            return rm;
        }
    }
}

