// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Text;
using System.Globalization;

using nsCDEngine.ViewModels;
using nsCDEngine.Engines;
using nsCDEngine.Security;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Communication.HttpService;

using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Runtime.Versioning;

using System.Linq;
// ReSharper disable StringIndexOfIsCultureSpecific.1
// ReSharper disable StringIndexOfIsCultureSpecific.2
#pragma warning disable CS1591    //TODO: Remove and document public methods

#if CDE_USEZLIB
using CDEngine.CDUtils.Zlib;
#endif

namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// A Collection of useful helper functions used frequently in C-DEngine Solutions
    /// </summary>
    public static partial class TheCommonUtils
    {
        #region cde Functions
        /// <summary>
        /// Converst a NameValueCollection to a Dictionary
        /// </summary>
        /// <param name="pQ">imcoming collection to be converted</param>
        /// <returns></returns>
        public static cdeConcurrentDictionary<string, string> cdeNameValueToDirectory(System.Collections.Specialized.NameValueCollection pQ)
        {
            if (pQ == null) return null;

            cdeConcurrentDictionary<string, string> tList = new cdeConcurrentDictionary<string, string>();
            foreach (string s in pQ)
                tList.TryAdd(s, pQ[s]);
            return tList;
        }

        /// <summary>
        /// Remove duplicates from a list of tokens.
        /// </summary>
        /// <param name="pCookies">A List containing two or more </param>
        /// <returns>A List with duplicate tokens removed.</returns>
        /// <remarks>
        /// This function removes duplicate tokens from a list of tokens.
        /// Each list item can continue multiple parts, with a semicolon delimiter
        /// between each part. Only the first part of each list item is used as
        /// the key value for checking for uniqueness.
        /// </remarks>
        public static List<string> cdeDistinctCookies(List<string> pCookies)
        {
            List<string> resList = new List<string>();
            foreach (string t in pCookies)
            {
                string FoundOne = "";
                string[] tco = t.Split(';');
                foreach (string tt in resList)
                {
                    string[] co = tt.Split(';');
                    if (co[0].Equals(tco[0]))
                    {
                        FoundOne = tt;
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(FoundOne))
                    resList.Remove(FoundOne);
                resList.Add(t);
            }
            return resList;
        }

        /// <summary>
        /// Query a list of tokens for the presence of a specific token.
        /// </summary>
        /// <param name="pCookies"></param>
        /// <param name="pCook"></param>
        /// <returns>True when the token is found, otherwise false.</returns>
        /// <remarks>
        /// Each list item may contain subparts, with a semicolon used to separate the parts from each other.
        /// The key value, for the purposes of searching the list, is the substring before the first semicolon delimiter.
        /// </remarks>
        public static bool cdeContainsCookie(List<string> pCookies, string pCook)
        {
            foreach (string t in pCookies)
            {
                string[] tco = t.Split(';');
                if (tco[0] == pCook) return true;
            }
            return false;
        }

        /// <summary>
        /// Truncates strings longer than a specified maximum length.
        /// </summary>
        /// <param name="pIn">String to truncate.</param>
        /// <param name="pMax">Maximum allowed characters in the string.</param>
        /// <returns>A string that is guaranteed to be no longer than pMax+3 in length.
        /// </returns>
        /// <remarks>
        /// This function truncates a string and adds an elipses ("...") to the end of the string
        /// when the string length exceeds the value of pMax.
        /// </remarks>
        public static string cdeSubstringMax(string pIn, int pMax)
        {
            if (!string.IsNullOrEmpty(pIn) && pIn.Length > pMax)
            {
                // ToDo: Length will be pMax+3. Is that what we want?
                return pIn.Substring(0, pMax) + "...";
            }
            return pIn;
        }

        /// <summary>
        /// Quickly verifies whether an object holds a lock or not.
        /// </summary>
        /// <param name="o">Object to test.</param>
        /// <returns>True if the object has a lock</returns>
        public static bool cdeIsLocked(object o)
        {
            if (!Monitor.TryEnter(o))
                return true;
            Monitor.Exit(o);
            return false;
        }

        /// <summary>
        /// Converts a Guid to a string.
        /// </summary>
        /// <param name="InGuid">The input GUID.</param>
        /// <returns>A string.</returns>
        /// <remarks>
        /// Converts a Guid to an upper-case string, removing all
        /// non alpha-numeric values (including '{', '}' and '-'.
        /// </remarks>
        public static string cdeGuidToString(Guid InGuid)
        {
            string OutGuid = InGuid.ToString().Replace("{", "").Replace("}", "").Replace("-", "");
            return OutGuid.ToUpper();
        }

        /// <summary>
        /// Truncates a Float to the amount of digits specified.
        /// </summary>
        /// <param name="value">Input float variable.</param>
        /// <param name="digits">Digits after the comma.</param>
        /// <returns></returns>
        public static float cdeTruncate(this float value, int digits)
        {
            double mult = Math.Pow(10.0, digits);
            double result = Math.Truncate(mult * value) / mult;
            return (float)result;
        }

        /// <summary>
        /// Truncates a double to the amount of digits specified.
        /// </summary>
        /// <param name="value">Input float variable.</param>
        /// <param name="digits">Digits after the comma.</param>
        /// <returns>
        /// The truncated value.
        /// </returns>
        public static double cdeTruncate(this double value, int digits)
        {
            double mult = Math.Pow(10.0, digits);
            double result = Math.Truncate(mult * value) / mult;
            return result;
        }

        /// <summary>
        /// Converts a string into a string array. if you don't need the Remove.. parameter, please use .NET .split() function. if you need the remove... parameter use the override with the string sep(erator)
        /// </summary>
        /// <param name="pToSplit">The input string.</param>
        /// <param name="sep">The delimiter in the input string.</param>
        /// <param name="RemoveDuplicates">if true, the returning list does not contain duplicate entries</param>
        /// <param name="RemoveEmptyEntries">if true, the returning list does not contain empty entries</param>
        /// <returns>An array of strings with the parsed results.</returns>
        public static string[] cdeSplit(string pToSplit, char sep, bool RemoveDuplicates, bool RemoveEmptyEntries)
        {
            if (RemoveDuplicates || RemoveEmptyEntries)
            {
                return cdeSplit(pToSplit, sep.ToString(), RemoveDuplicates, RemoveEmptyEntries);
            }
            if (string.IsNullOrEmpty(pToSplit)) return new[] { pToSplit };
            return pToSplit.Split(sep);
        }

        /// <summary>
        /// Converts a string into a string array.
        /// </summary>
        /// <param name="pToSplit">The input string.</param>
        /// <param name="sep">The delimiter character in the input string.</param>
        /// <param name="RemoveDuplicates">if true, the returning list does not contain duplicate entries</param>
        /// <param name="RemoveEmptyEntries">if true, the returning list does not contain empty entries</param>
        /// <returns>An array of strings with the parsed results.</returns>
        public static string[] cdeSplit(string pToSplit, string sep, bool RemoveDuplicates, bool RemoveEmptyEntries)
        {
            if (string.IsNullOrEmpty(pToSplit)) return new[] { pToSplit };
            if (RemoveDuplicates)
            {
                List<string> tList = new List<string>();
                int oldPos = 0;
                int tPos;
                do
                {
                    tPos = pToSplit.IndexOf(sep, oldPos);
                    if (tPos < 0) tPos = pToSplit.Length;
                    string tStr = pToSplit.Substring(oldPos, tPos - oldPos);
                    if ((!tList.Contains(tStr)) && (!RemoveEmptyEntries || !string.IsNullOrEmpty(tStr)))
                        tList.Add(tStr);
                    oldPos = tPos + sep.Length;
                } while (tPos >= 0 && oldPos < pToSplit.Length);
                return tList.ToArray();
            }
            else
            {
                StringSplitOptions tOpt = StringSplitOptions.None;
                if (RemoveEmptyEntries)
                    tOpt = StringSplitOptions.RemoveEmptyEntries;
                return pToSplit.Split(new[] { sep }, tOpt);
            }
        }

        /// <summary>
        /// Abstracts Buffer.BlockCopy for older OS/.NET Versions
        /// </summary>
        /// <param name="src"></param>
        /// <param name="srcOffset"></param>
        /// <param name="dest"></param>
        /// <param name="destOffset"></param>
        /// <param name="len"></param>
        public static void cdeBlockCopy(byte[] src, int srcOffset, byte[] dest, int destOffset, int len)
        {
            if (dest == null)
                dest = new byte[len];
            Buffer.BlockCopy(src, srcOffset, dest, destOffset, len);
        }

        /// <summary>
        /// Removes Javascript special characters.
        /// </summary>
        /// <param name="ostr">The string to convert.</param>
        /// <returns>A normalized .NET string.</returns>
        /// <remarks>
        /// This function converts Javascript special characters to their .NET equivalent.
        /// </remarks>
        public static string cdeJavaEncode(string ostr)
        {
            if (ostr == null) return "";
            string iStr = CStr(ostr);
            if (iStr.Length == 0) return "";
            string outStr = iStr.Replace("\"", "\\\"");
            outStr = outStr.Replace("'", @"\'");
            return outStr;
        }

        /// <summary>
        /// Removes Javascript special characters.
        /// </summary>
        /// <param name="ostr">The input string.</param>
        /// <returns>A string that can be safely displayed in a browser.</returns>
        /// <remarks>
        /// This function converts Javascript special characters into their HTML equivalent.
        /// For example, end of line characters are converted to &gt;BR>.
        /// </remarks>
        public static string cdeJavaEncode4Line(string ostr)
        {
            if (ostr == null) return "";
            string iStr = CStr(ostr);
            if (iStr.Length == 0) return "";
            string outStr = iStr.Replace("\"", "\\\"");
            outStr = outStr.Replace("'", @"\'");
            outStr = outStr.Replace("\n\r", "<BR>");
            outStr = outStr.Replace("\r\n", "<BR>");
            outStr = outStr.Replace("\n", "<BR>");
            outStr = outStr.Replace("\r", "<BR>");
            outStr = outStr.Replace("\t", "&nbsp;");
            return outStr;
        }

        /// <summary>
        /// Removes Javascript special characters.
        /// </summary>
        /// <param name="ostr">The string to convert.</param>
        /// <returns>A normalized .NET string.</returns>
        /// <remarks>
        /// This function converts Javascript special characters to their .NET equivalent.
        /// </remarks>
        public static string cdeJavaEncode4Code(string ostr)
        {
            if (ostr == null) return "";
            string iStr = CStr(ostr);
            if (iStr.Length == 0) return "";
            string outStr = iStr.Replace("\"", "\\\"");
            outStr = outStr.Replace("'", "\\\\'");
            return outStr;
        }

        /// <summary>
        /// Removes the return, linefeed, and tab characters from a string.
        /// </summary>
        /// <param name="iStr">The input string.</param>
        /// <returns>The input string without any return, linefeed, or tab characters.</returns>
        public static string cdeStripIllegalChars(string iStr)
        {
            if (iStr.Length == 0) return "";

            string outStr = iStr.Replace("\n", "");
            outStr = outStr.Replace("\r", "");
            outStr = outStr.Replace("\t", "");
            return outStr;
        }

        /// <summary>
        /// Remove web address URL encoding.
        /// </summary>
        /// <param name="strInput">The input string.</param>
        /// <returns>A string with any percent encoded characters.</returns>
        /// <remarks>
        /// This function modifies a string to remove any percent encoding and return the
        /// string to its original, unencoded form. For example, %20 becomes
        /// the space character.
        /// </remarks>
        public static string cdeUnescapeString(string strInput)
        {
            if (string.IsNullOrEmpty(strInput)) return "";
            return Uri.UnescapeDataString(strInput); // pToEscape.Replace("%3D", "=");
        }

        /// <summary>
        /// Add web address URL encoding.
        /// </summary>
        /// <param name="strInput">The input string.</param>
        /// <returns>A string without invalid URL characters.</returns>
        /// <remarks>
        /// Modify a string to make it suitable for use in a URL. For example, the space
        /// character becomes %20.
        /// </remarks>
        public static string cdeEscapeString(string strInput)
        {
            if (string.IsNullOrEmpty(strInput)) return "";
            return Uri.EscapeDataString(strInput); // pToEscape.Replace("=", "%3D");
        }

        /// <summary>
        /// Add escapes for back-slash and apostrophe.
        /// </summary>
        /// <param name="strInput">The input string.</param>
        /// <returns>The converted string.</returns>
        /// <remarks>
        /// This function modifies a string to convert two characters (back-slash and apostrophe)
        /// into their HTML equivalent (&quot;&amp;quot;&quot; and &quot;&amp;apos&quot;).
        /// </remarks>
        public static string cdePartialEscapeString(string strInput)
        {
            strInput = strInput.Replace("\"", "&quot;");
            strInput = strInput.Replace("'", "&apos;");
            return strInput;
        }

        /// <summary>
        /// Process a string to remove all HTML tags.
        /// </summary>
        /// <param name="strInput">The input string.</param>
        /// <returns>The processed string.</returns>
        public static string cdeStripHTML(string strInput)
        {
            if (string.IsNullOrEmpty(strInput)) return null;
            var array = new char[strInput.Length];
            int arrayIndex = 0;
            bool inside = false;

            foreach (var @let in strInput)
            {
                switch (@let)
                {
                    case '<':
                        inside = true;
                        continue;
                    case '>':
                        inside = false;
                        continue;
                }
                if (inside) continue;
                array[arrayIndex] = @let;
                arrayIndex++;
            }
            return new string(array, 0, arrayIndex);
        }
        #endregion

        #region Get... functions
        /// <summary>
        /// Extract a section from a string containing XML. The section consists of
        /// everything between an opening and a closing tag (but not including the tag itself).
        /// </summary>
        /// <param name="strTag">The name of the delimiting tag.</param>
        /// <param name="strIn">The input XML string.</param>
        /// <returns>A string containing the requested substring, or null on error.</returns>
        public static string GetXMLSection(string strTag, string strIn)
        {
            // To Do: Maybe add check for closing tag to avoid Exceptions??   && strIn.Contains(string.Format("</{0}>", strTag))
            if (!string.IsNullOrEmpty(strTag) && !string.IsNullOrEmpty(strIn) && strIn.Contains(string.Format("<{0}>", strTag)))
            {
                int pos = strIn.IndexOf(string.Format("<{0}>", strTag));
                int pos2 = strIn.IndexOf(string.Format("</{0}>", strTag), pos) - pos;
                return strIn.Substring(pos + strTag.Length + 2, pos2 - (strTag.Length + 2));
            }
            return null;
        }

        /// <summary>
        /// A dictionary lookup.
        /// </summary>
        /// <param name="pQ">The dictionary.</param>
        /// <param name="strKeyName">The keyname for the lookup.</param>
        /// <returns>The associated value for the keyname, or null on error.</returns>
        /// <remarks>
        /// Performs a lookup in a dictionary for the provided keyword,
        /// returning the associated value.
        /// </remarks>
        public static string GetQueryPart(Dictionary<string, string> pQ, string strKeyName)
        {
            if (pQ == null || string.IsNullOrEmpty(strKeyName)) return null;
            if (pQ.TryGetValue(strKeyName, out string retStr))
                return retStr;
            return null;
        }

        /// <summary>
        /// Retrieves a section of a string given the parameter of this function. Can be used to parse HTML or other string snippets.
        /// </summary>
        /// <param name="pSource">Source string to be parsed</param>
        /// <param name="pStartPos">Start Position in the string to start searching. After the search the END position will be returned in this parameter to allow continuous search in large texts</param>
        /// <param name="pFrom">Matching patter to seach for</param>
        /// <param name="pTo">Matching end of the pattern to search for. All between the start and end pattern will be returned. </param>
        /// <param name="IgnoreStartEnd">Default: true. If false, the function returns if either pFrom or pTo was not found in the pSource string</param>
        /// <returns>The requested substring, or null on error.</returns>
        public static string GetStringSection(string pSource, ref int pStartPos, string pFrom = null, string pTo = null, bool IgnoreStartEnd = true)
        {
            if (string.IsNullOrEmpty(pSource)) return null;
            try
            {
                int endPos = pTo.Length;
                int startPos = pStartPos;
                if (!string.IsNullOrEmpty(pFrom))
                {
                    startPos = pSource.IndexOf(pFrom, pStartPos);
                    if (startPos < 0)
                    {
                        if (IgnoreStartEnd)
                            startPos = pStartPos;
                        else
                        {
                            pStartPos = -1;
                            return null;
                        }
                    }
                    else
                        startPos += pFrom.Length;
                }
                if (!string.IsNullOrEmpty(pTo))
                {
                    endPos = pSource.IndexOf(pTo, startPos);
                    if (endPos < 0)
                    {
                        if (IgnoreStartEnd)
                            endPos = pTo.Length;
                        else
                        {
                            pStartPos = -1;
                            return null;
                        }
                    }
                }
                pStartPos = endPos;
                return pSource.Substring(startPos, (endPos - startPos));
            }
            catch (Exception)
            {
                // ignored
            }
            pStartPos = -1;
            return null;
        }

        /// <summary>
        /// Formats a date as a string.
        /// </summary>
        /// <param name="pDate">Date to be converted</param>
        /// <param name="pSEID">Session ID of the request for the string</param>
        /// <param name="pFormat">Format string for the return. If not set, "" will be used</param>
        /// <returns>The formatted string.</returns>
        /// <remarks>
        /// Returns the DateTime String matching the LCID stored in the session given by the pSEID
        /// Only use for Displaying DateTimeOffsets in UX! Do not use for Messaging or Storage!
        /// </remarks>
        public static string GetDateTimeString(DateTimeOffset pDate, Guid pSEID, string pFormat = "")
        {
            if (pDate == DateTimeOffset.MinValue)
                return "";
            if (pSEID == Guid.Empty)
                return GetDateTimeString(pDate, -1, pFormat);

            TheSessionState tSess = TheBaseAssets.MySession.ValidateSEID(pSEID);    //Low Frequency
            if (tSess == null)
                return GetDateTimeString(pDate, -1, pFormat);
            else
                return GetDateTimeString(pDate, tSess.LCID, pFormat);

        }

        /// <summary>
        /// Returns a cultural correct string representation of a DateTimeOffset.
        /// Only use for Displaying DateTimeOffsets in UX! Do not use for Messaging or Storage!
        /// </summary>
        /// <param name="pDate">If null or MinValue the function return an empty string.</param>
        /// <param name="pLCID">LCID of the language required. If set to 0 the current culture is used. Negative numbers return the Date in ISO8601 format.</param>
        /// <param name="pFormat">Format string for the return. If not set, "" will be used.</param>
        /// <returns>The formatted string.</returns>
        public static string GetDateTimeString(DateTimeOffset pDate, int pLCID = 0, string pFormat = "")
        {
            if (pDate == DateTimeOffset.MinValue)
                return "";

            if (pLCID < 0)
                return pDate.ToString("yyyy-MM-dd HH:mm:ss");
            if (pLCID == 0)
            {
                if (!string.IsNullOrEmpty(pFormat))
                    return pDate.ToString(pFormat, CultureInfo.CurrentCulture);
                return pDate.ToString(CultureInfo.CurrentCulture);
            }
            else
            {
                try
                {
                    CultureInfo tCult = new CultureInfo(pLCID);
                    if (!string.IsNullOrEmpty(pFormat))
                        return pDate.ToString(pFormat, tCult);
                    return pDate.ToString(tCult);
                }
                catch (Exception)
                {
                    return pDate.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        /// <summary>
        /// Returns the Maximum Message Size allowed for a given SenderType
        /// </summary>
        /// <param name="pType"></param>
        /// <returns></returns>
        public static int GetMaxMessageSize(cdeSenderType pType) { return TheBaseAssets.MAX_MessageSize[(int)pType]; }

        /// <summary>
        /// Returns the Calendar Week for a given DateTime.
        /// The calculation is done using Sunday as the first day of the week,
        /// and first week as the one with at least four days.
        /// </summary>
        /// <param name="datetime">The date</param>
        /// <returns>A value from 1 to 52 for the week of the year.</returns>
        public static int GetCalendarweek(DateTime datetime)
        {
            //Calendar does not support DateTimeOffset
            CultureInfo culture = CultureInfo.CurrentCulture;
            int calendarweek = culture.Calendar.GetWeekOfYear(datetime, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Sunday);
            return calendarweek;
        }

        /// <summary>
        /// Returns a TimeStamp that can be used for Files up to the millisecond.
        /// </summary>
        /// <returns>
        /// A string formatted as YYYYMMDDhhmmssxxx, where YYYY is a 4-digit year,
        /// MM is a 2-digit month, DD is a 2-digit day, hh is a 2-digit hour, mm is a 2-digit minute,
        /// ss is a 2-digit second, and xxx is 3-digit millisecond value.
        /// </returns>
        public static string GetTimeStamp()
        {
            var now = DateTime.Now; //No Offset Needed
            return string.Format("{0:0000}{1:00}{2:00}{3:00}{4:00}{5:00}{6:000}", now.Year, now.Month, now.Day, now.Hour, now.Minute,now.Second, now.Millisecond);
        }

        /// <summary>
        /// Returns a file name where all reserved characters are replaced with underscore ("_"). Optionally add a timestamp to the file name.
        /// Example: GetSafeFileName("a+b:c/d", "json", true) returns a+b_c_d_201801241106123.json.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="extension"></param>
        /// <param name="addTimeStamp"></param>
        /// <returns>A string with the safe file name.</returns>
        public static string GetSafeFileName(string name, string extension, bool addTimeStamp)
        {
            var fileName = addTimeStamp ? $"{name}_{TheCommonUtils.GetTimeStamp()}.{extension}" : $"{name}.{extension}";
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            return fileName;
        }

        /// <summary>
        /// Query for a mime type from a file extension.
        /// </summary>
        /// <param name="extension">A file extension (include period '.').</param>
        /// <returns>A valid MIME type.
        /// If extension is not found, "application/octet-stream" is returned.</returns>
        /// <remarks>
        /// Return the standard Multipurpose Internet Mail Extensions (MIME) type
        /// from common file name extensions.
        /// Example: ".css" returns "text/css".
        /// </remarks>
        public static string GetMimeTypeFromExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return "";
            switch (extension.ToLower())
            {
                case ".avi": return "video/x-msvideo";
                case ".css": return "text/css";
                case ".doc": return "application/msword";
                case ".gif": return "image/gif";
                case ".htm":
                case ".html": return "text/html";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".js": return "application/javascript";
                case ".mp3": return "audio/mpeg";
                case ".wav": return "audio/wav";
                case ".png": return "image/png";
                case ".pdf": return "application/pdf";
                case ".ppt": return "application/vnd.ms-powerpoint";
                case ".zip": return "application/x-gzip";
                case ".gz": return "application/gzip";
                case ".txt": return "text/plain";
                case ".svg": return "image/svg+xml";
                case ".xml": return "text/xml";
                case "":
                case ".json": return "application/json";
                case ".eot": //Web Fonts
                    return "application/vnd.ms-fontobject";
                case ".ttf":
                case ".otf":
                case ".woff":
                case ".woff2":
                    return "application/octet-stream";
                case ".stp": //3d modeling binary
                case ".step": //3d modeling binary
                    return "application/step";
                case ".obj":
                case ".mtl":
                    return "text/plain";
                case ".stl": //3D Modeling Binary
                    return "application/sla";
                case ".ico":
                    return "image/vnd.Microsoft.icon";
                default:
                    return "application/octet-stream";
            }
        }

        /// <summary>
        /// Query for an LCID.
        /// </summary>
        /// <param name="pMSG"></param>
        /// <returns>A valid Locale ID.</returns>
        /// <remarks>
        /// Query the Locale ID (LCID) for the current user. If null is provided
        /// for the pMSG parameter, the default Locale ID for the system is returned.
        /// </remarks>
        public static int GetLCID(TheProcessMessage pMSG)
        {
            return GetLCID(pMSG, null);
        }

        /// <summary>
        /// Parse a string containing a list of Guid values for the first one in the list.
        /// </summary>
        /// <param name="strGuidList">A string with semicolon delimited Guid values.</param>
        /// <returns>A valid Guid value; Guid.Empty when no Guid is found.</returns>
        public static Guid GetFirstURL(string strGuidList)
        {
            if (string.IsNullOrEmpty(strGuidList)) return Guid.Empty;
            string[] t = strGuidList.Split(';');
            if (t.Length > 0) return CGuid(t[0].Split(':')[0]);
            return Guid.Empty;
        }

        /// <summary>
        /// Parse a string containing a list of Guid values for the Nth one in the list.
        /// </summary>
        /// <param name="strGuidList"></param>
        /// <param name="index"></param>
        /// <returns>A valid Guid value; Guid.Empty when no Guid is found.</returns>
        public static Guid GetNodeById(string strGuidList, int index)
        {
            if (string.IsNullOrEmpty(strGuidList)) return Guid.Empty;
            string[] t = strGuidList.Split(';');
            if (t.Length > index) return CGuid(t[index].Split(':')[0]);
            return Guid.Empty;
        }

        /// <summary>
        /// Gets a Markup encoded version of a DeviceID for the System Log. If "ShowMInLog" is true. Otherwise just returns the pDeviceID.ToString()
        /// </summary>
        /// <param name="pDeviceID">Device ID to be wrapped in Markup</param>
        /// <param name="AddToSpanStyle">Any style to add to the span surrounding the DeviceID. Can be used i.e. to change color or font width (default is C-Labs Blue and bold)</param>
        /// <returns></returns>
        public static string GetDeviceIDML(Guid pDeviceID, string AddToSpanStyle = null)
        {
            if (!TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog)
                return pDeviceID.ToString().Substring(0, 8);
            return $"<span onclick=\"if (cdeCSOn) cdeCSOn=null; else cdeCSOn=true;\" onmouseenter=\"cdeColorSpan(this, 'black')\" onmouseleave=\"cdeColorSpan(null)\"  class=\"cdeRSHash\" style='color:rgb(29, 163, 209); font-weight:bold; {(string.IsNullOrEmpty(AddToSpanStyle) ? "" : AddToSpanStyle)}'>{pDeviceID.ToString().Substring(0, 8)}</span>";
        }

        /// <summary>
        /// Gets a Markup encoded version of all DeviceIDs in an ORG for the System Log. If "ShowMInLog" is true. Otherwise just returns the pORG
        /// </summary>
        /// <param name="pORG">Semicolon separated DeviceIDs - mostly coming from the ORG parameter of a TSM</param>
        /// <param name="AddToSpanStyle">Any style to add to the span surrounding the DeviceID. Can be used i.e. to change color or font width (default is C-Labs Blue and bold)</param>
        /// <returns></returns>
        public static string GetDeviceIDML(string pORG, string AddToSpanStyle = null)
        {
            if (string.IsNullOrEmpty(pORG))
                return "";
            if (!TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog)
                return pORG;

            var tOrgs = pORG.Split(';');
            string res = "";
            foreach (var t in tOrgs)
            {
                if (res.Length > 0)
                    res += " via ";
                res += GetDeviceIDML(CGuid(t), AddToSpanStyle);
            }
            return res;
        }

        /// <summary>
        /// Returns the Node Name of this node
        /// </summary>
        /// <returns></returns>
        public static string GetMyNodeName()
        {
            string tNodeName;
            if (TheBaseAssets.MyServiceHostInfo != null)
            {
                tNodeName = TheBaseAssets.MyServiceHostInfo.NodeName;
                if (string.IsNullOrEmpty(tNodeName))
                    tNodeName = TheBaseAssets.MyServiceHostInfo.MyStationName;
            }
            else
                tNodeName = $"{TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID}";
            return tNodeName;
        }

        /// <summary>
        /// Returns the value of a property.
        /// </summary>
        /// <param name="src">The object containing the property.</param>
        /// <param name="propName">The propety name.</param>
        /// <returns>The value for the property, or null.</returns>
        /// <remarks>
        /// There are two ways to call this function:<br/>(1) On a static
        /// snapshot of a thing property bag, or <br/>(2) On the C# properties
        /// for a TheThing / ICDEThing object.<br/><br/>
        /// This code fragment demonstrates the first approach:
        /// <code>
        /// TheThingStore tstore = TheThingStore.CloneFromTheThing(MyBaseThing, false);
        /// Object pPropValue1 = TheCommonUtils.GetPropValue(tstore, &quot;SampleProperty&quot;);</code>
        /// This code fragment demonstrates the second approach, which uses Reflection
        /// in the .NET Framework to locate the specfied C# property:
        /// <code>
        /// Object pPropValue2 = TheCommonUtils.GetPropValue(this, &quot;SampleProperty&quot;);</code>
        /// </remarks>
        public static object GetPropValue(object src, string propName)
        {
            if (src == null || string.IsNullOrEmpty(propName)) return null;
            try
            {
                if (src is TheThingStore)
                {
                    TheThingStore tSt = src as TheThingStore;
                    if (tSt.PB.ContainsKey(propName))
                        return tSt.PB[propName];
                    string OnUpdateName;
                    if (propName.Contains("."))
                    {
                        string[] pParts = cdeSplit(propName, '.', false, false);

                        OnUpdateName = pParts[1];
                        if (pParts.Length > 3)
                        {
                            for (int i = 2; i < pParts.Length - 1; i++)
                                OnUpdateName += "." + pParts[i];
                        }
                        if (tSt.PB.ContainsKey(OnUpdateName))
                            return tSt.PB[OnUpdateName];
                    }
                    // This used to return null if no property was found: now attempting reflection
                }
                if ((src is TheThing || src is ICDEThing)) // CODE REVIEW: This used to always use reflection if propName contains no dot.
                {
                    TheThing tSt = (src as ICDEThing).GetBaseThing();
                    if (tSt != null)
                    {
                        string OnUpdateName;
                        if (propName.Contains("."))
                        {
                            string[] pParts = cdeSplit(propName, '.', false, false);
                            OnUpdateName = pParts[1];
                            if (pParts.Length > 3)
                            {
                                for (int i = 2; i < pParts.Length - 1; i++)
                                    OnUpdateName += "." + pParts[i];
                            }

                            else
                            {
                                OnUpdateName = propName;
                            }
                            cdeP tp = tSt.GetProperty(OnUpdateName);
                            if (tp != null)
                                return tp.Value;    //Not allowed to get Secure Property
                            // This used to return null if no property was found: now attempting reflection
                        }
                    }
                }

                PropertyInfo tInfo = src.GetType().GetProperty(propName);
                if (tInfo != null)
                    return tInfo.GetValue(src, null);
                else
                {
                    FieldInfo tF = src.GetType().GetField(propName);
                    if (tF != null)
                        return tF.GetValue(src);
                }

            }
            catch
            {
                // ignored
            }
            return null;
        }

        /// <summary>
        /// Returns the eWebPlatform of a given UserAgent
        /// </summary>
        /// <param name="userAgent">User Agent String</param>
        /// <returns></returns>
        public static eWebPlatform GetWebPlatform(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return eWebPlatform.Desktop;
            userAgent = userAgent.ToLower();
            if (botDevices.Any(x => userAgent.Contains(x)))
                return eWebPlatform.Bot;
            if (userAgent.Contains("qtcarbrowser") || userAgent.Contains("tesla") || userAgent.Contains("linux; x86_64 gnu/linux"))
                return eWebPlatform.TeslaXS;
            if (userAgent.Contains("xbox"))
                return eWebPlatform.XBox;
            if (userAgent.Contains("samsung family hub"))
                return eWebPlatform.TizenFamilyHub;
            if (userAgent.Contains("tizen"))
                return eWebPlatform.TizenTV;
            if (mobileDevices.Any(x => userAgent.Contains(x)))
                return eWebPlatform.Mobile;
            if (holoDevices.Any(x => userAgent.Contains(x)))
                return eWebPlatform.HoloLens;
            return eWebPlatform.Desktop;
        }

        /// <summary>
        /// Parse a string containing a list of Guid values for the last one in the list.
        /// </summary>
        /// <param name="strGuidList"></param>
        /// <returns>A valid Guid value; Guid.Empty when no Guid is found.</returns>
        public static Guid GetLastURL(string strGuidList)
        {
            if (string.IsNullOrEmpty(strGuidList)) return Guid.Empty;
            string[] t = strGuidList.Split(';');
            if (t.Length > 0) return CGuid(t[t.Length - 1].Split(':')[0]);
            return Guid.Empty;
        }

        /// <summary>
        /// NEWV4: Returns a known http resource on this node (TODOV4: Security Review)
        /// </summary>
        /// <param name="pData"></param>
        public static void GetAnyFile(TheRequestData pData)
        {
            TheHttpService.GetAnyFile(pData, false);
        }

        /// <summary>
        /// Request the version number of a specific plugin.
        /// </summary>
        /// <param name="plugin">Any object or type created by the plugin in question.</param>
        /// <returns>Major and minor version as a double.</returns>
        public static double GetAssemblyVersion(object plugin)
        {
            double dVersion = -1;
            try
            {
                var versionString = GetAssemblyAttribute<AssemblyFileVersionAttribute>(plugin)?.Version;
                var verParts = versionString?.Split(new char[] { '.' }, 2);
                if (verParts?.Length > 1)
                    dVersion = CDbl($"{verParts[0]}.{verParts[1].Replace(".", "")}");
                else
                    dVersion = 1.0;
            }
            catch (Exception) { }
            return dVersion;
        }

        public static cdePlatform GetAssemblyPlatformHostAgnostic(Assembly tAss, bool bDiagnostics, out string diagnosticsInfo)
        {
            return GetAssemblyPlatform(tAss, false, bDiagnostics, out diagnosticsInfo);
        }
        public static cdePlatform GetAssemblyPlatform(Assembly tAss, bool bDiagnostics, out string diagnosticsInfo)
        {
            return GetAssemblyPlatform(tAss, true, bDiagnostics, out diagnosticsInfo);
        }

        /// <summary>
        /// Fetch a resource from an assembly file.
        /// </summary>
        /// <param name="assem">A reference to an assembly.</param>
        /// <param name="strResName">The name of the resource to retrieve.</param>
        /// <returns>
        /// A byte array containing the resource. On error, a null.
        /// </returns>
        /// <remarks>
        /// When null is provided for the assem parameter, the assembly of the
        /// currently executing plugin is the search target.
        /// </remarks>
        public static byte[] GetSystemResource(Assembly assem, string strResName)
        {
            if (string.IsNullOrEmpty(strResName)) return null;

            byte[] tBlobBuffer = null;
            Stream imgStream = null;
            imgStream = cdeOpenFile(strResName, FileMode.Open, FileAccess.Read);
            if (imgStream == null)
            {
                List<string> tEnum = null;
                if (strResName.StartsWith("clientbin", StringComparison.OrdinalIgnoreCase))
                    strResName = strResName.Substring(10);
                if (assem == null)
                {
                    if (TheCDEngines.MyNMIService != null)
                    {
                        tBlobBuffer = TheCDEngines.MyNMIService.GetPluginResourceBlob(strResName);
                        if (tBlobBuffer != null)
                            return tBlobBuffer;
                    }
                    if (imgStream == null)
                        tBlobBuffer = GetAssemblyResource(null, strResName);
                }
                else
                {
                    string[] allres = assem.GetManifestResourceNames();
                    strResName = strResName.Replace('/', '.').Replace('\\', '.');
                    strResName = (strResName.StartsWith(".") ? "" : ".") + strResName;
                    tEnum = allres.Where(x => x.EndsWith(strResName, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (tEnum.Any())
                        imgStream = assem.GetManifestResourceStream(tEnum.First());
                }
            }
            if (imgStream != null)
            {
#if !CDE_NET35
                using (MemoryStream ms = new MemoryStream())
                {
                    imgStream.CopyTo(ms);
                    tBlobBuffer = ms.ToArray();
                }
#else
                    byte[] buffer = new byte[TheBaseAssets.MAX_MessageSize[0]];
                    using (MemoryStream ms = new MemoryStream())
                    {
                        int read;
                        while ((read = imgStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }
                        tBlobBuffer=ms.ToArray();
                    }
#endif
            }
            return tBlobBuffer;
        }
        #endregion

        #region Probe Functions
        /// <summary>
        /// Safe check for strings with no content.
        /// </summary>
        /// <param name="str"></param>
        /// <returns>True if null or white spaces, otherwise False.</returns>
        /// <summary>
        /// This function provides a platform-independent way to validate string references. It
        /// enables support for String.IsNullOrWhiteSpace() under the .NET Framework version 3.5.
        /// </summary>
        public static bool IsNullOrWhiteSpace(string str)
        {
#if !CDE_NET35
            return string.IsNullOrWhiteSpace(str);
#else
            return string.IsNullOrEmpty(str) || str.Trim().Length == 0;
#endif
        }

        /// <summary>
        /// Check for a Guid.
        /// </summary>
        /// <param name="inObj">The input object.</param>
        /// <returns>True if a Guid is found, otherwise False.</returns>
        /// <remarks>
        /// A safe way to determine whether an object contains a Guid or not. Safe means that
        /// no unhandled exceptions are generated.
        /// </remarks>
        public static bool IsGuid(object inObj)
        {
            if (inObj == null)
            {
                return false;
            }
            if (inObj is Guid)
            {
                return true;
            }
            bool isGuid = true;
#if !CDE_NET35 // No Guid.TryParse
            isGuid = Guid.TryParse(CStr(inObj), out _);
            if (!isGuid)
                isGuid = false;
#else
            try
            {
                Guid tGuid = new Guid(CStr(inObj));
            }
            catch { isGuid = false; }
#endif
            return isGuid;
        }

        /// <summary>
        /// Query whether the current host is an end-user device or not, such
        /// as a browser or a phone.
        /// </summary>
        /// <returns>'True' if a user device, 'False' if not.</returns>
        public static bool IsHostADevice()
        {
            if (TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.Device ||
                //TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.Mini || //Mini has a webServer
                TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.Browser ||
                TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.Phone ||
                TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.Metro ||
                TheBaseAssets.MyServiceHostInfo.cdeHostingType == cdeHostType.Silverlight)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Returns true if a sendery type is a device
        /// </summary>
        /// <param name="pType"></param>
        /// <returns></returns>
        public static bool IsDeviceSenderType(cdeSenderType pType)
        {
            if (pType == cdeSenderType.CDE_MINI || pType == cdeSenderType.CDE_DEVICE || pType == cdeSenderType.CDE_JAVAJASON || pType == cdeSenderType.CDE_PHONE || pType == cdeSenderType.CDE_CUSTOMISB)  //4.207: CustomISB is a device
                return true;
            else
                return false;
        }

        /// <summary>
        /// Cross-check two strings containing one or more Guids
        /// separated by semi-colons, searching for any match between
        /// the two lists.
        /// </summary>
        /// <param name="strGuidList1">A list of one or more semi-colon separated Guids.</param>
        /// <param name="strGuidList2">Another list of one or more semi-colon separated Guids.</param>
        /// <returns>True if there is any overlap between the Guid lists, otherwise False.</returns>
        public static bool DoUrlsContainAnyUrl(string strGuidList1, string strGuidList2)
        {
            if (string.IsNullOrEmpty(strGuidList2) || string.IsNullOrEmpty(strGuidList1))
                return false;
            string[] tUrls = strGuidList2.Split(';');
            List<string> orgs = strGuidList1.Split(';').Select((s) => s.Split(':')[0]).ToList(); // Remove any trailing ThingIDs
            for (int i = 0; i < tUrls.Length; i++)
            {
                string tUrl = tUrls[i].Split(':')[0]; // Remove any trailing ThingIDs
                if (orgs.Contains(tUrl, StringComparer.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Determines whether provided URL specifies the current host on which process is running.
        /// </summary>
        /// <param name="url">The input URL.</param>
        /// <returns>
        /// 'True' when running on local host, otherwise 'False'.
        /// </returns>
        public static bool IsUrlLocalhost(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            if (url.Equals("LOCALHOST") || url.Equals(TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false), StringComparison.OrdinalIgnoreCase))
                return true;
            if (TheBaseAssets.MyServiceHostInfo.MyAltStationURLs != null && TheBaseAssets.MyServiceHostInfo.MyAltStationURLs.Count > 0)
            {
                foreach (string tUrl in TheBaseAssets.MyServiceHostInfo.MyAltStationURLs)
                {
                    if (url.Equals(tUrl, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if the UserAgent is a mobile device.
        /// </summary>
        /// <param name="userAgent"></param>
        /// <returns></returns>
        public static bool IsMobileDevice(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return false;
            userAgent = userAgent.ToLower();
            return mobileDevices.Any(x => userAgent.Contains(x));
        }

        /// <summary>
        /// Determines whether a node ID references the current host.
        /// </summary>
        /// <param name="urlGuid">Each thing, and each property of a thing, is tagged with the creating node in a field named cdeN.
        /// The node ID for a given host is found in this location: TheBaseAssets.MyServiceInfo.MyDeviceInfo.DeviceID.</param>
        /// <returns>
        /// 'True' when node ID refers to current host, otherwise 'False'.
        /// </returns>
        public static bool IsLocalhost(Guid urlGuid)
        {
            if (urlGuid == Guid.Empty) return false;

            if (urlGuid.Equals(TheBaseAssets.MyServiceHostInfo?.MyDeviceInfo?.DeviceID))
                return true;
            return false;
        }

        /// <summary>
        /// Determines whether any of the URLs in the provided input string are a reference to the current host.
        /// Note that this function always ignores the hard-coded value of "LOCALHOST".
        /// </summary>
        /// <param name="url">A string containing one or more URLs separated by semi-colons.</param>
        /// <returns>
        /// 'True' when a URL for the current host is present, otherwise 'False'.
        /// </returns>
        public static bool DoesContainLocalhost(string url)
        {
            return DoesContainLocalhost(url, false);
        }

        /// <summary>
        /// Determines whether any of the URLs in the provided input string are a reference to the current host.
        /// </summary>
        /// <param name="url">A string containing one or more URLs separated by semi-colons.</param>
        /// <param name="IgnoreLOCALHOST">Whether to ignore the hard-coded value of "LOCALHOST".</param>
        /// <returns></returns>
        public static bool DoesContainLocalhost(string url, bool IgnoreLOCALHOST)
        {
            if (string.IsNullOrEmpty(url)) return false;

            if ((!IgnoreLOCALHOST && url.Equals("LOCALHOST")) || url.Contains(TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false)))  //MSU-OK
                return true;
            if (TheBaseAssets.MyServiceHostInfo.MyAltStationURLs != null && TheBaseAssets.MyServiceHostInfo.MyAltStationURLs.Count > 0)
            {
                foreach (string tUrl in TheBaseAssets.MyServiceHostInfo.MyAltStationURLs)
                {
                    if (url.Contains(tUrl))
                        return true;
                }
            }
            return false;
        }
        #endregion

        #region compression helpers
        /// <summary>
        /// Convert a Unicode-encoded string to a memory stream.
        /// </summary>
        /// <param name="pIn">The input string.</param>
        /// <returns>A System.IO.MemoryStream.</returns>
        public static MemoryStream CStringToMemoryStream(string pIn)
        {
            byte[] a = Encoding.Unicode.GetBytes(pIn);
            return new MemoryStream(a);
        }
        /// <summary>
        /// Compress a string to a byte array. Reverse the process by calling cdeDecompressToString.
        /// </summary>
        /// <param name="sourceString">Input parameter - the string to compress.</param>
        /// <returns>A byte array representing the compressed data.</returns>
        public static byte[] cdeCompressString(string sourceString)
        {
            byte[] compressed = null;
#if !CDE_USEZLIB
            try
            {
                using (var outStream = new MemoryStream())
                {
                    using (var tinyStream = new GZipStream(outStream, CompressionMode.Compress))
                    {
                        using (var mStream = new MemoryStream(CUTF8String2Array(sourceString)))
                        {
#if !CDE_NET35
                            mStream.CopyTo(tinyStream);
#else
                            byte[] buffer = new byte[TheBaseAssets.MAX_MessageSize[0]];
                            int read;
                            while ((read = mStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                tinyStream.Write(buffer, 0, read);
                            }
#endif
                        }
                    }
                    compressed = outStream.ToArray();
                }
            }
            catch (EntryPointNotFoundException)
#endif
            {
#if CDE_USEZLIB
                using (var outStream = new MemoryStream())
                {
                    using (var tinyStream = new CDEngine.CDUtils.Zlib.GZipStream(outStream, CDEngine.CDUtils.Zlib.CompressionMode.Compress))
                    using (var mStream = new MemoryStream(CUTF8String2Array(sourceString)))
                        mStream.CopyTo(tinyStream);

                    compressed = outStream.ToArray();
                }
#endif
            }
            return compressed;
        }

        /// <summary>
        /// Compress a portion of a byte array.
        /// </summary>
        /// <param name="pBuffer">Pointer to the buffer to compress.</param>
        /// <param name="index">The starting location to use.</param>
        /// <param name="count">The number of bytes to compress.</param>
        /// <returns>A byte array holding the compressed data.</returns>
        public static byte[] cdeCompressBuffer(byte[] pBuffer, int index, int count)
        {
            byte[] bRes = null;
#if !CDE_USEZLIB
            try
            {
                using (var ms = new MemoryStream())
                {
                    using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
                        zip.Write(pBuffer, index, count);
                    bRes = ms.ToArray();
                }
            }
            catch (EntryPointNotFoundException)
#endif
            {
#if CDE_USEZLIB
                using (var ms = new MemoryStream())
                {
                    using (var zip = new CDEngine.CDUtils.Zlib.GZipStream(ms, CDEngine.CDUtils.Zlib.CompressionMode.Compress, true))
                        zip.Write(pBuffer, pBufPos, pBufLen);
                    bRes = ms.ToArray();
                }
#endif
            }
            return bRes;
        }

        /// <summary>
        /// Decompresses data that was originally compressed with a call to cdeCompressString.
        /// </summary>
        /// <param name="sourceArray">The input byte array.</param>
        /// <returns>A string representing the results of decompressing the input buffer.</returns>
        public static string cdeDecompressToString(byte[] sourceArray)
        {
            return cdeDecompressToString(sourceArray, 0, sourceArray.Length); // new MemoryStream(sourceArray));
        }

        /// <summary>
        /// Decompress data from a portion of a byte array to a string.
        /// </summary>
        /// <param name="sourceArray">The input byte array. </param>
        /// <param name="index">Index of the first elements of the array to decompress.</param>
        /// <param name="count">Number of bytes to decompress.</param>
        /// <returns></returns>
        public static string cdeDecompressToString(byte[] sourceArray, int index, int count)
        {
#if !CDE_USEZLIB
            try
            {
                using (var inStream = new MemoryStream(sourceArray, index, count))
                using (var bigStream = new GZipStream(inStream, CompressionMode.Decompress))
                using (var bigStreamOut = new MemoryStream())
                {
#if !CDE_NET35
                    bigStream.CopyTo(bigStreamOut);
#else
                    byte[] buffer = new byte[TheBaseAssets.MAX_MessageSize[0]];
                    int read;
                    while ((read = bigStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        bigStreamOut.Write(buffer, 0, read);
                    }
#endif

                    return CArray2UTF8String(bigStreamOut.ToArray());
                }
            }
            catch (EntryPointNotFoundException)
#endif
            {
#if CDE_USEZLIB
                using (var inStream = new MemoryStream(sourceArray, index, count))
                using (var bigStream = new CDEngine.CDUtils.Zlib.GZipStream(inStream, CDEngine.CDUtils.Zlib.CompressionMode.Decompress))
                using (var bigStreamOut = new MemoryStream())
                {
                    bigStream.CopyTo(bigStreamOut);
                    return CArray2UTF8String(bigStreamOut.ToArray());
                }
#else
                return null;
#endif
            }
        }
        #endregion

        #region File Utilities
        /// <summary>
        /// Creates nested folder hierarchy for a file being created.
        /// </summary>
        /// <param name="strFilePath">Path to a folder hierarchy and file to be created.</param>
        /// <remarks>
        /// Call this function when you are about to create a file that
        /// exists within a folder hierarchy. Include the file name
        /// at the end of the path. Example:
        /// <code>
        /// // Create "Data" folder
        /// // Create "Data/Current" (or Data\Current) folder
        /// // In preparation for creating file "today.dat"
        /// char sep = Path.DirectorySeparatorChar;
        /// string strDataFile = strDataRoot + sep + "Data" + sep + "Current" + sep + "today.dat";
        /// CreateDirectories(strDataFile);
        /// </code>
        /// </remarks>
        /// <seealso>CreateDirectories(string,bool)</seealso>
        public static void CreateDirectories(string strFilePath)
        {
            CreateDirectories(strFilePath, false);
        }

        /// <summary>
        /// Creates folder hierarchy, optionally omitting file name.
        /// </summary>
        /// <param name="strFilePath">Path to a folder hierarchy and file to be created.</param>
        /// <param name="bIsDirectoryPath">True when all parts are folder names, False when last part is file name.</param>
        /// <seealso>CreateDirectories(string)</seealso>
        public static void CreateDirectories(string strFilePath, bool bIsDirectoryPath)
        {
            // ToDo: This ignores ClientBin. Should we care?
            if (String.IsNullOrEmpty(strFilePath)) return;
            if (File.Exists(strFilePath)) return;
            char sep = Path.DirectorySeparatorChar;
            //if (IsMono())
            //    sep = '/';
            string[] subdirs = strFilePath.Split(sep);
#if false // Old Code
            // Assumes more than one folder. Misses edge case of one folder with
            // no file specified.
            if (subdirs.Length > 1)
            {
                int pathSegmentsToCreate = bIsDirectoryPath ? subdirs.Length : subdirs.Length - 1;
#else
            // Allows for creation of single folder
            int pathSegmentsToCreate = bIsDirectoryPath ? subdirs.Length : subdirs.Length - 1;
            if (pathSegmentsToCreate > 0)
            {
#endif
                for (int i = 0; i < pathSegmentsToCreate; i++)
                {
                    string tDir = "";
                    for (int j = 0; j <= i; j++)
                    {
                        if (j > 0) tDir += sep;
                        tDir += subdirs[j];
                    }
                    if (!string.IsNullOrEmpty(tDir))
                    {
                        try
                        {
                            Directory.CreateDirectory(tDir);
                        }
                        catch { }
                    }
                }
            }
        }

        /// <summary>
        /// Opens a file in the local filesystem securely.
        /// The file will ONLY be opened under clientbin
        /// The path is automatically set and fixed up for MONO and Linux environments
        /// </summary>
        /// <param name="pFileName">Filename to be opened - if it does not start with ClientBin, ClientBin is added</param>
        /// <param name="pMode">File mode</param>
        /// <param name="pAccess">File Access Mode</param>
        /// <returns></returns>
        public static Stream cdeOpenFile(string pFileName, FileMode pMode, FileAccess pAccess)
        {
            try
            {
                string fileToReturn = cdeFixupFileName(pFileName);
                TheBaseAssets.MySYSLOG.WriteToLog(5014, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("CommonUtils", string.Format("opening File :{0}", fileToReturn), eMsgLevel.l6_Debug), true);
                return File.Exists(fileToReturn) ? new FileStream(fileToReturn, pMode, pAccess) : null;
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(5015, new TSM("CommonUtils", string.Format("cdeOpenFile opening File :{0} FAILED", pFileName), eMsgLevel.l1_Error, e.ToString()), true);
                return null;
            }
        }

        /// <summary>
        /// Modify a file path as required to enable compatibilty across different
        /// target platforms.
        /// </summary>
        /// <param name="pFilePath">Input file path.</param>
        /// <returns>A valid file path for the current platform.</returns>
        public static string cdeFixupFileName(string pFilePath)
        {
            return cdeFixupFileName(pFilePath, false);
        }

        /// <summary>
        /// Loads the content of a file into a string owned by a plugin
        /// </summary>
        /// <param name="pBase">Plugin that owns the file</param>
        /// <param name="tTargetFileName">Filename to be loaded</param>
        /// <returns></returns>
        public static string LoadStringFromDisk(IBaseEngine pBase, string tTargetFileName)
        {
            if (pBase == null) return null;
            string pA = null;
            if (pBase.GetEngineID() != TheCDEngines.MyContentEngine.GetBaseEngine().GetEngineID())
                pA = pBase.GetEngineID().ToString();
            return LoadStringFromDisk(tTargetFileName, pA);
        }

        /// <summary>
        /// Loads the content of a file into a byte array owned by a plugin
        /// </summary>
        /// <param name="pBase">Plugins that owns the file</param>
        /// <param name="tTargetFileName">Filename to be loaded</param>
        /// <returns></returns>
        public static byte[] LoadBlobFromDisk(IBaseEngine pBase, string tTargetFileName)
        {
            if (pBase == null) return null;
            string pA = null;
            if (pBase.GetEngineID() != TheCDEngines.MyContentEngine.GetBaseEngine().GetEngineID())
                pA = pBase.GetEngineID().ToString();
            return LoadBlobFromDisk(tTargetFileName, pA);
        }

        /// <summary>
        /// Loads the content of a file using a given path (under ClientBin)
        /// </summary>
        /// <param name="tTargetFileName">Filename to be loaded</param>
        /// <param name="pathAdd">Path under ClientBin to look for the file</param>
        /// <returns></returns>
        public static string LoadStringFromDisk(string tTargetFileName, string pathAdd)
        {
            if (tTargetFileName == null) return null;

            string finalFileName = tTargetFileName;
            if (tTargetFileName.Contains("?"))
                finalFileName = tTargetFileName.Substring(0, tTargetFileName.IndexOf("?"));
            if (!string.IsNullOrEmpty(pathAdd))
                finalFileName = pathAdd.ToLower() + "\\" + finalFileName;
            string fileToReturn = cdeFixupFileName(finalFileName);
            if (!cdeFileExists(fileToReturn)) return null;
            string resStr = null;
            try
            {
                System.IO.StreamReader myFile = new System.IO.StreamReader(fileToReturn);
                resStr = myFile.ReadToEnd();
                myFile.Close();
            }
            catch (Exception)
            { }
            return resStr;
        }

        /// <summary>
        /// Saves a message to disk in a directory for a given plugin
        /// </summary>
        /// <param name="pBase">Plugin owning the list</param>
        /// <param name="pMessage">Message containing the data to be stored</param>
        /// <param name="command">Extra commands to be executed during the storing</param>
        /// <returns></returns>
        public static string SaveBlobToDisk(IBaseEngine pBase, TSM pMessage, string[] command)
        {
            if (pBase == null) return null;
            string pA = null;
            if (pBase.GetEngineID() != TheCDEngines.MyContentEngine.GetBaseEngine().GetEngineID())
                pA = pBase.GetEngineID().ToString();
            return SaveBlobToDisk(pMessage, command, pA);
        }
        #endregion

        #region Process Helpers
        /// <summary>
        /// A debug helper to format the call stack in HTML.
        /// </summary>
        /// <param name="pStack">A StackTrace object created by the caller.</param>
        /// <returns>A string formatted in HTML with stack trace details.</returns>
        /// <remarks>
        /// Call this function with a System.Diagnostics.StackTrace object, as shown here:
        /// <code>
        /// string strHtmlCallStack = GetStackInfo(new StackTrace(true));</code>
        /// </remarks>
        public static string GetStackInfo(object pStack)
        {
            string stackResult = "Stack:<br/>";
            try
            {
                System.Diagnostics.StackTrace st = pStack as System.Diagnostics.StackTrace;
                string stackIndent = "";
                for (int i = 0; i < st.FrameCount; i++)
                {
                    // Note that at this level, there are four
                    // stack frames, one for each method invocation.
                    System.Diagnostics.StackFrame sf = st.GetFrame(i);
                    stackResult += string.Format(stackIndent + "File: {0} Method:{1} Line:{2}<br/>", sf.GetFileName(), sf.GetMethod(), sf.GetFileLineNumber());
                    stackIndent += "  ";
                }
            }
            catch (Exception e)
            {
                stackResult = "not available:" + e.ToString();
            }
            return stackResult;
        }

        /// <summary>
        /// Runs a sequence of asynchronous tasks (task continuations or a method that uses the C# async keyword) and lets the caller wait for the entire chain to finish. The call returns immediately and not when the first continuation is hit.
        /// </summary>
        /// <param name="pThreadName"></param>
        /// <param name="callBack"></param>
        /// <param name="pState"></param>
        /// <param name="longRunning"></param>
        /// <returns></returns>
        public static Task cdeRunTaskChainAsync(string pThreadName, Func<object, Task> callBack, object pState = null, bool longRunning = false)
        {
            PendingTask pendingTask = null;
            TaskCompletionSource<bool> tcs;
            Dictionary<string, object> taskInfo = null;
            if (TheBaseAssets.MyServiceHostInfo.EnableTaskKPIs)
            {
                pendingTask = new PendingTask { createTime = DateTime.UtcNow };
                taskInfo = new Dictionary<string, object>
                {
                    { "name", pThreadName }
                };
                tcs = new TaskCompletionSource<bool>(taskInfo);
            }
            else
            {
                tcs = new TaskCompletionSource<bool>(pThreadName);
            }

            var task = tcs.Task;

            var task2 = Task.Factory.StartNew((o) =>
            {
                if (pendingTask != null)
                {
                    pendingTask.startTime = DateTime.UtcNow;
                }

                //TheDiagnostics.SetThreadName(pThreadName, true);
                var t2 = callBack?.Invoke(pState);
                taskInfo?.Add("firstTask", t2);
                var t3 = t2?.ContinueWith((t) =>
                {
                    tcs.TrySetResult(true);
                    if (pendingTask != null)
                    {
                        pendingTask.endTime = DateTime.UtcNow;
                    }
                });
                taskInfo?.Add("continuationTask", t3);
                if (t2 == null || t3 == null)
                {
                    tcs.TrySetResult(false);
                }
            }, pThreadName, defaultTaskCreationOptions | (longRunning ? TaskCreationOptions.LongRunning : 0));

#if CDE_NET4 || CDE_NET35
            // On Net4 and earlier a task with an unobserved exception takes down the process during garbage collection/finalization of the task object
            // This continuation observes the exception and prevents that.
            // On Net45 and newer, such unobserved task exception are ignored.
            task.ContinueWith(c => { var ignored = c.Exception; }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
#endif
            taskInfo?.Add("wrapperTask", task2);

            if (pendingTask != null)
            {
                pendingTask.task = task;
                _globalTasks.Add(pendingTask);
            }
            return task;
        }
        #endregion

        #region Conversion Helpers
        /// <summary>
        /// Rotates a point in 2-dimensional space around an arbitrary
        /// rotation point by the number the specified angle of rotation.
        /// </summary>
        /// <param name="oldX">Original X value (and X return value).</param>
        /// <param name="oldY">Original Y value (and Y return value).</param>
        /// <param name="pAxisX">Point of rotation X value.</param>
        /// <param name="pAxisY">Point of rotation Y value.</param>
        /// <param name="pAngle">Angle of rotation in degrees.</param>
        public static void Rotate2D(ref double oldX, ref double oldY, double pAxisX, double pAxisY, double pAngle)
        {
            double angle = Math.PI * pAngle / 180.0;

            oldX -= pAxisX;
            oldY -= pAxisY;

            double xnew = oldX * Math.Cos(angle) + oldY * Math.Sin(angle);
            double ynew = oldY * Math.Cos(angle) - oldX * Math.Sin(angle);
            double outPointX = xnew;
            double outPointY = ynew;

            outPointX += pAxisX;
            outPointY += pAxisY;

            oldX = outPointX;
            oldY = outPointY;
        }

        /// <summary>
        /// Rounds a date value to a given minute interval.
        /// </summary>
        /// <param name="time">Original time value.</param>
        /// <param name="minuteInterval">Minute to round up or down to.</param>
        /// <param name="direction">Round up, down or nearest.</param>
        /// <returns>
        /// The rounded DateTimeOffset value.
        /// </returns>
        public static DateTimeOffset RoundDateToMinuteInterval(DateTimeOffset time, int minuteInterval, RoundingDirection direction)
        {
            if (minuteInterval == 0)
                return time;
            TimeSpan d = TimeSpan.FromMinutes(minuteInterval);
            switch (direction)
            {
                default: //RoundingDirection.Round:
                    {
                        var delta = time.Ticks % d.Ticks;
                        bool roundUp = delta > d.Ticks / 2;
                        var offset = roundUp ? d.Ticks : 0;

                        return new DateTimeOffset(time.Ticks + offset - delta, time.Offset);
                    }
                case RoundingDirection.RoundDown:
                    {
                        var delta = time.Ticks % d.Ticks;
                        return new DateTimeOffset(time.Ticks - delta, time.Offset);
                    }
                case RoundingDirection.RoundUp:
                    {
                        var modTicks = time.Ticks % d.Ticks;
                        var delta = modTicks != 0 ? d.Ticks - modTicks : 0;
                        return new DateTimeOffset(time.Ticks + delta, time.Offset);
                    }
            }
        }

        /// <summary>
        /// Rounds a date value to a given second interval.
        /// </summary>
        /// <param name="time">Original time value.</param>
        /// <param name="secondInterval">Second to round up or down to.</param>
        /// <param name="direction">Round up, down or nearest.</param>
        /// <returns>
        /// The rounded DateTimeOffset value.
        /// </returns>
        public static DateTimeOffset RoundDateToSecondInterval(DateTimeOffset time, int secondInterval, RoundingDirection direction)
        {
            if (secondInterval == 0)
                return time;
            TimeSpan d = TimeSpan.FromSeconds(secondInterval);
            switch (direction)
            {
                default: //RoundingDirection.Round:
                    {
                        var delta = time.Ticks % d.Ticks;
                        bool roundUp = delta > d.Ticks / 2;
                        var offset = roundUp ? d.Ticks : 0;

                        return new DateTimeOffset(time.Ticks + offset - delta, time.Offset);
                    }
                case RoundingDirection.RoundDown:
                    {
                        var delta = time.Ticks % d.Ticks;
                        return new DateTimeOffset(time.Ticks - delta, time.Offset);
                    }
                case RoundingDirection.RoundUp:
                    {
                        var modTicks = time.Ticks % d.Ticks;
                        var delta = modTicks != 0 ? d.Ticks - modTicks : 0;
                        return new DateTimeOffset(time.Ticks + delta, time.Offset);
                    }
            }
        }

        /// <summary>
        /// Specifies how to handle rounding operation for rounding functions.
        /// </summary>
        public enum RoundingDirection
        {
            /// <summary>
            /// Always round to the higher value.
            /// </summary>
            RoundUp,
            /// <summary>
            /// Always round to the lower value.
            /// </summary>
            RoundDown,
            /// <summary>
            /// Round to nearest whole value, either up or down, based on distance from whole value.
            /// </summary>
            Round
        }

        /// <summary>
        /// Converts a Unicode-encoded byte array to a string.
        /// </summary>
        /// <param name="pIn">Input byte array containing Unicode characters.</param>
        /// <returns>A string.</returns>
        public static string CArray2UnicodeString(byte[] pIn)
        {
            return CArray2UnicodeString(pIn, 0, pIn.Length);
        }

        /// <summary>
        /// Converts a byte array using UTF8 encoding to a character string.
        /// </summary>
        /// <param name="pIn">Input array of UTF8 characters, stored as byte value.</param>
        /// <param name="StripFileCodes">If true, the function checks for the Unicode File Tag EF BB BF and strips it from the byte stream</param>
        /// <returns>The converted string.</returns>
        public static string CArray2UTF8String(byte[] pIn, bool StripFileCodes)
        {
            if (pIn == null) return "";
            return CArray2UTF8String(pIn, 0, pIn.Length, StripFileCodes);
        }

        /// <summary>
        /// Converts a byte array using UTF8 encoding to a character string.
        /// </summary>
        /// <param name="pIn">Input array of UTF8 characters, stored as byte value.</param>
        /// <param name="start">Index of input array beginning of range of items to include in the conversion.</param>
        /// <param name="len">Count of array items to include in conversion.</param>
        /// <param name="StripFileCodes">If true, the function checks for the Unicode File Tag EF BB BF and strips it from the byte stream</param>
        /// <returns>The converted string.</returns>
        public static string CArray2UTF8String(byte[] pIn, int start, int len, bool StripFileCodes)
        {
            UTF8Encoding enc = new UTF8Encoding();
            if (StripFileCodes && start==0 && pIn.Length>3 && pIn[0]==0xEF && pIn[1]==0xBB && pIn[2]==0xBF)
                return enc.GetString(pIn, start+3, len-3);
            return enc.GetString(pIn, start, len);
        }

        /// <summary>
        /// Removes a trailing slash from a URL.
        /// </summary>
        /// <param name="pUrl"></param>
        /// <returns>A URL without a trailing slash.</returns>
        public static string TruncTrailingSlash(string pUrl)
        {
            if (pUrl.EndsWith("/"))
                return pUrl.Substring(0, pUrl.Length - 1);
            return pUrl;
        }


        public static string CStrNullable(object inObj)
        {
            if (inObj == null) return null;
            return CStr(inObj);
        }
        public static Guid? CGuidNullable(object inObj)
        {
            if (inObj == null) return null;
            return CGuid(inObj);
        }
        public static int? CIntNullable(object inObj)
        {
            if (inObj == null) return null;
            return CInt(inObj);
        }
        public static DateTimeOffset? CDateNullable(object inObj)
        {
            if (inObj == null) return null;
            var date = CDate(inObj);
            return date;
        }

        /// <summary>
        /// Safely converts an object (likely a string) to a System.Uri object.
        /// </summary>
        /// <param name="inObj">Object to be converted</param>
        /// <param name="IgnoreGuids">If true, the converter removes all '{' and '}' from the string first.
        /// Since DeviceUris of the C-DEngine have a guid as the Host, the '{' and '}' have to be removed first</param>
        /// <returns>A System.Uri object. On error, null is returned.</returns>
        public static Uri CUri(object inObj, bool IgnoreGuids)
        {
            if (inObj == null) return null;
            else
            {
                try
                {
                    if (inObj is string && string.IsNullOrEmpty((string)inObj)) return null;
                    if (IgnoreGuids && inObj is string)
                        inObj = ((string)inObj).Replace("{", "").Replace("}", "");
                    Uri uri;
                    if (inObj is Uri)
                    {
                        uri = (Uri)inObj;
                    }
                    else
                    {
                        uri = new Uri(CStr(inObj));
                    }
#if CDE_NET35
                    // NET35 does not know about wss and hence does not default the port correctly
                    if (uri != null && uri.Port == -1 && uri.Scheme == "wss")
                    {
                        var builder = new UriBuilder(uri)
                        {
                            Port = 443
                        };
                        uri = builder.Uri;
                    }
#endif
                    return uri;
                }
                catch
                {
                    // ignored
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a specified number of characters from the left side of a string.
        /// </summary>
        /// <param name="strIn">Input string.</param>
        /// <param name="count">Number of characters to include in returned string.</param>
        /// <returns>The substring, or input string if len is greater than the length of the input string.</returns>
        /// <remarks>
        /// This function returns a substring of the input string that is at most count
        /// characters in length.
        /// </remarks>
        public static string CLeft(string strIn, int count)
        {
            if (string.IsNullOrEmpty(strIn)) return "";
            if (strIn.Length > count)
            {
                return strIn.Substring(0, count);
            }

            return strIn;
        }

        /// <summary>
        /// Parse a web address for parameters.
        /// </summary>
        /// <param name="query">The query string to parse.</param>
        /// <returns>A keys and values collection.</returns>
        /// <remarks>
        /// Parse the query string in the URI into a KeyValuePair&lt;string, string&gt; collection.
        /// The keys and values will be URL decoded.
        /// </remarks>
        public static Dictionary<string, string> ParseQueryString(string query)
        {
            if (string.IsNullOrEmpty(query))
                return null;

            if (query.StartsWith("?"))
                query = query.Substring(1);

            var values = new Dictionary<string, string>();  //DIC-Allowed

            int length = query.Length;
            int startIndex, equalIndex;

            for (int i = 0; i < length; i++)
            {
                startIndex = i;
                equalIndex = -1;

                while (i < length)
                {
                    char c = query[i];

                    if (c == '=')
                    {
                        if (equalIndex < 0)
                        {
                            equalIndex = i;
                        }
                    }
                    else if (c == '&')
                    {
                        break;
                    }

                    i++;
                }

                string value = null;


                string key;
                if (equalIndex >= 0)
                {
                    key = query.Substring(startIndex, equalIndex - startIndex);
                    value = query.Substring(equalIndex + 1, (i - equalIndex) - 1);
                }
                else
                {
                    //standalone value is key or value?
                    key = query.Substring(startIndex, i - startIndex);
                }

                if (value == null)
                {
                    value = string.Empty;
                }

                if (!values.ContainsKey(key.ToUpper()))
                    values.Add(key.ToUpper(), value);
            }

            return values;
        }
        #endregion

        #region Process Helpers

        /// <summary>
        /// A safe sleep function.
        /// </summary>
        /// <param name="ms">Time in ms to sleep.</param>
        /// <param name="minPeriod">Probe for MASTERSWITCH every minPeriod ms.</param>
        /// <remarks>
        /// Sleeps for a certain period of time but monitors the TheBaseAssets.MASTERSWICH to ensure the app was not closed during this sleep process.
        /// </remarks>
        public static void SleepOneEye(uint ms, uint minPeriod)
        {
            ManualResetEvent mre = new ManualResetEvent(false);
            if (ms < minPeriod)
            {
                mre.WaitOne((int)ms);
            }
            else
            {
                ms /= minPeriod;
                while (TheBaseAssets.MasterSwitch && ms-- > 0)
                {
                    mre.WaitOne((int)minPeriod);
                }
            }
#if !CDE_NET35
            mre.Dispose();
#endif
        }

        /// <summary>
        /// Creates a cancel token for C-DEngine safe sleep functions.
        /// </summary>
        /// <returns>An object for use in various SleepOneEyexxx functions. </returns>
        /// <remarks>The cancel token is for use in the following functions: a) SleepOneEyeWithCancel b) TaskDelayOneEye
        /// c) TaskWaitTimeout. To set the returned object to a 'signaled' state, call the SleepOneEyeCancel function.</remarks>
        public static object SleepOneEyeGetCancelToken()
        {
            return new ManualResetEvent(false);
        }

        /// <summary>
        /// A safe sleep function.
        /// </summary>
        /// <param name="ms">Time in ms to sleep.</param>
        /// <param name="minPeriod">Probe for MASTERSWITCH every minPeriod ms.</param>
        /// <param name="cancelToken">A cancel token created by calling SleepOneEyeGetCancelToken().</param>
        /// <remarks>
        /// This function sleeps for the specified number of milliseconds.
        /// Returns a task that completes after a certain period of time but
        /// cancels (faults with TaskCanceledException) if TheBaseAssets.MASTERSWICH
        /// indicates that a shutdown was started during this period.
        /// </remarks>
        public static void SleepOneEyeWithCancel(uint ms, uint minPeriod, object cancelToken)
        {
            if (!(cancelToken is ManualResetEvent mre))
            {
                return;
            }
            if (ms < minPeriod)
            {
                mre.WaitOne((int)ms);
            }
            else
            {
                ms /= minPeriod;
                while (TheBaseAssets.MasterSwitch && ms-- > 0)
                {
                    if (mre.WaitOne((int)minPeriod))
                    {
                        break;
                    }
                }
            }
#if !CDE_NET35
            mre.Dispose();
#endif
        }

        /// <summary>
        /// Set the cancel token to a 'signaled' state.
        /// </summary>
        /// <param name="cancelToken">The cancellation token.</param>
        /// <remarks>
        /// Any tasks waiting on the cancellation token are released to run when it is in a 'signaled' state.
        /// </remarks>
        public static void SleepOneEyeCancel(object cancelToken)
        {
            if (!(cancelToken is ManualResetEvent mre))
            {
                return;
            }
            mre.Set();
        }

        /// <summary>
        /// A safe sleep function.
        /// </summary>
        /// <param name="ms">Time in ms to sleep.</param>
        /// <param name="minPeriod">Probe for MASTERSWITCH every minPeriod ms.</param>
        /// <remarks>
        /// This function sleeps for the specified number of milliseconds.
        /// Returns a task that completes after a certain period of time but
        /// cancels (faults with TaskCanceledException) if TheBaseAssets.MASTERSWICH
        /// indicates that the app was closed during this period.
        /// </remarks>
#if !(CDE_NET35 || CDE_NET4)
        public static Task TaskDelayOneEye(int ms, uint minPeriod)
        {
            return TaskDelayOneEye(ms, minPeriod, null);
        }

        /// <summary>
        /// A safe sleep function.
        /// </summary>
        /// <param name="ms">Time in ms to sleep.</param>
        /// <param name="minPeriod">Probe for MASTERSWITCH every minPeriod ms. Not used with Async Task. Use the non-async method if you need to use this</param>
        /// <param name="cancelToken">A cancel token created by calling SleepOneEyeGetCancelToken().</param>
        /// <remarks>
        /// This function sleeps for the specified number of milliseconds.
        /// Returns after a certain period of time but cancels (faults with TaskCanceledException)
        /// if TheBaseAssets.MASTERSWICH indicates that the app was closed during this period.
        /// </remarks>
        public static async Task TaskDelayOneEye(int ms, uint minPeriod, CancellationToken? cancelToken)
        {
            CancellationTokenSource cts = null;
            try
            {
                if (cancelToken.HasValue && cancelToken.Value != TheBaseAssets.MasterSwitchCancelationToken)
                {
                    cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken.Value, TheBaseAssets.MasterSwitchCancelationToken);
                    await System.Threading.Tasks.Task.Delay(ms, cts.Token).ConfigureAwait(false);
                }
                else
                {
                    await System.Threading.Tasks.Task.Delay(ms, TheBaseAssets.MasterSwitchCancelationToken).ConfigureAwait(false);
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                // Ensure we don't block in case the task was canceled due to a token cancelation notification:
                // In this case we are executing on the thread that called the cancel and will continue blocking it if the caller of TaskDelayOneEye performs a loop that only checks a linked cancel token
                await Task.Yield();
            }
            finally
            {
                cts?.Dispose();
            }
        }

        #elif CDE_NET35
        public static System.Threading.Tasks.Task TaskDelayOneEye(int ms, uint minPeriod)
        {
            return TaskDelayOneEye(ms, minPeriod, null);
        }
        public static System.Threading.Tasks.Task TaskDelayOneEye(int ms, uint minPeriod, CancellationToken? cancelToken)
        {
            // Use Async Target package (Microsoft.Bcl.Async) - this bubbles up to all plug-ins and requires a QFE to Net40, which is too impactful for now
            return System.Threading.Tasks.TaskEx.Delay(ms, !cancelToken.HasValue ? TheBaseAssets.MasterSwitchCancelationToken : CancellationTokenSource.CreateLinkedTokenSource(cancelToken.Value, TheBaseAssets.MasterSwitchCancelationToken).Token).ContinueWith(t => { });
        }

#else
        public static System.Threading.Tasks.Task TaskDelayOneEye(int ms, uint minPeriod)
        {
            return System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                SleepOneEye((uint)ms, minPeriod);
            });
        }
        public static System.Threading.Tasks.Task TaskDelayOneEye(int ms, uint minPeriod, CancellationToken? cancelToken)
        {
            if (!cancelToken.HasValue)
            {
                return TaskDelayOneEye(ms, minPeriod);
            }
            return System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    SleepOneEye((uint)ms, minPeriod);
                }, cancelToken.Value).ContinueWith(t => { });
        }
#endif

        /// <summary>
        /// Creates a task that completes when any of the provided tasks completes.
        /// </summary>
        /// <param name="tasks">A collection with one or more tasks.</param>
        /// <returns>The task that completed.</returns>
        public static System.Threading.Tasks.Task<System.Threading.Tasks.Task> TaskWhenAny(IEnumerable<System.Threading.Tasks.Task> tasks)
        {
#if !(CDE_NET35 || CDE_NET4)
            return System.Threading.Tasks.Task.WhenAny(tasks);
#elif CDE_NET35
            // No Task.Delay in Net4: Use Async Target package (Microsoft.Bcl.Async)
            return System.Threading.Tasks.TaskEx.WhenAny(tasks);
#else
            // No dependency on Bcl.Async: Fall back to WaitAny (block a thread  - inefficient but current usage in plug-ins does not create many of theses)
            var taskArray = tasks.ToArray();
            return System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                var completedTask = taskArray[System.Threading.Tasks.Task.WaitAny(taskArray)];
                return completedTask;
            });
#endif
        }

        /// <summary>
        /// Creates a task that completes when all of the provided tasks are complete.
        /// </summary>
        /// <param name="tasks"></param>
        /// <returns>A task that represents the completion of all the supplied tasks.</returns>
        public static System.Threading.Tasks.Task TaskWhenAll(IEnumerable<System.Threading.Tasks.Task> tasks)
        {
#if !(CDE_NET35 || CDE_NET4)
            return System.Threading.Tasks.Task.WhenAll(tasks);
#elif CDE_NET35
            // No Task.Delay in Net4: Use Async Target package (Microsoft.Bcl.Async)
            return System.Threading.Tasks.TaskEx.WhenAll(tasks);
#else
            // No dependency on Bcl.Async: Fall back to WaitAny (block a thread - inefficient but current usage in plug-ins does not create many of theses)
            var taskArray = tasks.ToArray();
            var aggregateCS = new System.Threading.Tasks.TaskCompletionSource<bool>();
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    System.Threading.Tasks.Task.WaitAll(taskArray);
                }
                catch (Exception e)
                {
                    aggregateCS.TrySetException(e);
                }
                aggregateCS.TrySetResult(true);
            });

            return aggregateCS.Task;
#endif
        }

        /// <summary>
        /// Returns a task that completes when the task completes or a timeout occurrs.
        /// </summary>
        /// <param name="task">Task to be timed out.</param>
        /// <param name="timeout">Timeout interval.</param>
        /// <returns>The completed task.</returns>
        public static Task TaskWaitTimeout(System.Threading.Tasks.Task task, TimeSpan timeout)
        {
            return TaskWaitTimeout(task, timeout, null);
        }

        /// <summary>
        /// Returns a task that completes when the task completes or a timeout occurrs.
        /// </summary>
        /// <param name="task">Task to be timed out.</param>
        /// <param name="timeout">Timeout interval.</param>
        /// <param name="cancelToken">Cancel token for the task</param>
        /// <returns>The completed task.</returns>
        public static Task TaskWaitTimeout(System.Threading.Tasks.Task task, TimeSpan timeout, CancellationToken? cancelToken)
        {
            if (timeout < TimeSpan.Zero)
            {
                timeout = TimeSpan.Zero;
            }
#if !CDE_NET35 && !CDE_NET4
            var timeoutCancelTokenSource = new CancellationTokenSource(timeout);
            var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancelToken ?? TheBaseAssets.MasterSwitchCancelationToken, timeoutCancelTokenSource.Token);
            var combinedTask = task.ContinueWith(t =>
                {
                    try { combinedTokenSource.Dispose(); } catch { }
                    try { timeoutCancelTokenSource.Dispose(); } catch { }
                }, combinedTokenSource.Token)
                .ContinueWith(t =>
                {
                    try { combinedTokenSource.Dispose(); } catch { }
                    try { timeoutCancelTokenSource.Dispose(); } catch { }
                });
#else
            var timeoutInMs = timeout.TotalMilliseconds;
            if (timeout.TotalMilliseconds > int.MaxValue)
            {
                timeoutInMs = int.MaxValue;
            }

            // TODO Improve disposal of timeoutTasks (right now the task remains until it times out)
            var timeoutTask = TaskDelayOneEye((int)timeoutInMs, 100, cancelToken);
            var combinedTask = TaskWhenAny(new System.Threading.Tasks.Task[] { task, timeoutTask }).ContinueWith(t => { });
#endif
            return combinedTask;
        }

        /// <summary>
        /// Returns a task that completes when the task completes or the cancelToken was canceled.
        /// Note that the underlying task will NOT be canceled, only the await/Wait for the returned task will be canceled.
        /// </summary>
        /// <param name="task">Task to waited for.</param>
        /// <param name="cancelToken">CancellationToken to be observed while waiting for the task.</param>
        /// <returns>The completed task.</returns>
        public static System.Threading.Tasks.Task TaskWaitCancel(System.Threading.Tasks.Task task, CancellationToken cancelToken)
        {
            var combinedTask = task.ContinueWith(t => { }, cancelToken).ContinueWith(t => { });
            return combinedTask;
        }


        /// <summary>
        /// A portable helper function that resolves platform-specific differences in the handling of System.Threading.Tasks.Task.Result.
        /// </summary>
        /// <typeparam name="T">Type of the completed task to return.</typeparam>
        /// <param name="result">Result for the completed task.</param>
        /// <returns>Completed task with Task.Result == result.</returns>
        public static System.Threading.Tasks.Task<T> TaskFromResult<T>(T result)
        {
#if !(CDE_NET35 || CDE_NET4)
            return System.Threading.Tasks.Task.FromResult<T>(result);
#elif CDE_NET35
            // No Task.Delay in Net4: Use Async Target package (Microsoft.Bcl.Async)
            return System.Threading.Tasks.TaskEx.FromResult<T>(result);;
#else
            return System.Threading.Tasks.Task.Factory.StartNew( () => result );
#endif
        }

        /// <summary>
        /// Retrieves the task that was running when an exception occured.
        /// </summary>
        /// <param name="e">The exception parameter.</param>
        /// <returns>A task.</returns>
        public static Task TaskFromException(Exception e)
        {
#if !(CDE_NET35 || CDE_NET4 || CDE_NET45)
            return Task.FromException(e);
#else
            var taskCS = new TaskCompletionSource<bool>();
            taskCS.SetException(e);
            return taskCS.Task;
#endif
        }


        /// <summary>
        /// Retrieve exception details in a string, including the stack trace.
        /// </summary>
        /// <param name="e">The exception details passed to an exception handler.</param>
        /// <returns>A string containing exception details.</returns>
        /// <remarks>Note: Call this function from within an exception handler.</remarks>
        public static string GetAggregateExceptionMessage(Exception e)
        {
            return GetAggregateExceptionMessage(e, true);
        }
        /// <summary>
        /// Retrieve exception details in a string, optionally including the stack trace.
        /// </summary>
        /// <param name="e">The exception details passed to an exception handler.</param>
        /// <param name="includeStackTrace">Whether to include the stack trace or not.</param>
        /// <returns>A string containing exception details.</returns>
        /// <remarks>Note: Call this function from within an exception handler.</remarks>
        public static string GetAggregateExceptionMessage(Exception e, bool includeStackTrace)
        {
            if (e == null)
            {
                return "";
            }
            if (e is AggregateException)
            {
                var agg = e as AggregateException;
                string messages;
                if (agg.InnerExceptions.Count > 1)
                {
                    messages = agg.Message.TrimEnd('.') + ": ";
                }
                else
                {
                    messages = "";
                }
                foreach (var inner in agg.InnerExceptions)
                {
                    messages += GetExceptionMessageWithInnerHelper(inner, includeStackTrace) + ". ";
                }
                return messages;
            }
            else
            {
                return GetExceptionMessageWithInnerHelper(e, includeStackTrace);
            }
        }
        #endregion

        #region Generate Strings
        /// <summary>
        /// Performs macro substitutions to generate a final output string.
        /// </summary>
        /// <param name="strIn">String containing macros, such as %D% for date.</param>
        /// <returns>A final string for display to users.</returns>
        /// <remarks>
        /// Here are the set of available macros:<br/>
        ///
        /// <list type="table">
        ///    <listheader>
        ///       <term>Macro</term>
        ///       <description>Description</description>
        ///    </listheader>
        /// <item><term>ACL</term><description>Current user name</description></item>
        /// <item><term>ADMINPORTAL1</term><description>Admin portal1.</description></item>
        /// <item><term>ADMINPORTAL2</term><description>Admin portal 2.</description></item>
        /// <item><term>APPNAME</term><description>Application name.</description></item>
        /// <item><term>D</term><description>Today's date</description></item>
        /// <item><term>DEVICEFILTER</term><description>Device Filter.</description></item>
        /// <item><term>FN</term><description>Current user role.</description></item>
        /// <item><term>GetLiveScreens</term><description>Live screens.</description></item>
        /// <item><term>GetNMIViews</term><description>NMI Views.</description></item>
        /// <item><term>MAINDASHBOARD</term><description>Main dashboard.</description></item>
        /// <item><term>N</term><description>Host name / Url.</description></item>
        /// <item><term>RegisteredControlTypes</term><description>Registered control types.</description></item>
        /// <item><term>SITENAME</term><description>Site Name.</description></item>
        /// <item><term>STARTSCREEN</term><description>Start screen.</description></item>
        /// <item><term>T</term><description>Time</description></item>
        /// <item><term>U</term><description>Current logged on user unique ID.</description></item>
        /// <item><term>V</term><description>Version</description></item>
        /// </list>
        /// </remarks>
        public static string GenerateFinalStr(string strIn)
        {
            return GenerateFinalStr(strIn, null);
        }

        /// <summary>
        /// Performs macro substitutions to generate a final output string.
        /// </summary>
        /// <param name="strIn">String containing macros, such as %D% for date.</param>
        /// <param name="pThing"></param>
        /// <returns>A final string for display to users.</returns>
        public static string GenerateFinalStr(string strIn, ICDEThing pThing)
        {
            return GenerateFinalStr(strIn, pThing, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Performs macro substitutions to generate a final output string.
        /// </summary>
        /// <param name="strIn">String containing macros, such as %D% for date.</param>
        /// <param name="pThing"></param>
        /// <param name="cultureInfo"></param>
        /// <returns>A final string for display to users.</returns>
        public static string GenerateFinalStr(string strIn, ICDEThing pThing, CultureInfo cultureInfo)
        {
            if (string.IsNullOrEmpty(strIn)) return "";
            if (strIn.IndexOf("%") < 0) return strIn;
            //cPage.Trace.Write("GenerateFinalStrIn",gfsinStr);
#if JCDEBUG
			ProfilerString=UpdateProfile(ProfilerString,"GenerateFinalStr (MAINMACROS) enter",ProfileTimer);
#endif
            int tT = 1;
            string gfsoutStr = "", tAppString = "", gfsoutStr1 = "";
            int tAppPos, tAppPosEnd;
            StringBuilder RecursLog = new StringBuilder("", 255);

            gfsoutStr1 = strIn;
            while (tT == 1)
            {
                gfsoutStr = gfsoutStr1;
                if (gfsoutStr.IndexOf("%") < 0) break;		// 3 Recursions allowed then out...

                if (gfsoutStr.Contains("%V%"))
                    gfsoutStr = gfsoutStr.Replace("%V%", TheBaseAssets.CurrentVersionInfo);
                if (TheBaseAssets.MyServiceHostInfo.IsViewer && (TheBaseAssets.MyApplication.MyUserManager != null && TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser != null))
                {
                    gfsoutStr = gfsoutStr.Replace("%U%", TheCommonUtils.CStr(TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser.cdeMID));
                    gfsoutStr = gfsoutStr.Replace("%PWD%", TheCommonUtils.CStr(TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser.LoginGesture));
                    gfsoutStr = gfsoutStr.Replace("%E%", TheCommonUtils.CStr(TheBaseAssets.MyApplication.MyUserManager.LoggedOnUser.EMail));
                    gfsoutStr = gfsoutStr.Replace("%FN%", TheUserManager.GetCurrentUserRole());
                    gfsoutStr = gfsoutStr.Replace("%ACL%", TheUserManager.GetCurrentUserName());
                }
                else
                {
                    gfsoutStr = gfsoutStr.Replace("%U%", "0");
                    gfsoutStr = gfsoutStr.Replace("%PWD%", "NOT SET");
                    gfsoutStr = gfsoutStr.Replace("%E%", "NOT SET");
                    gfsoutStr = gfsoutStr.Replace("%FN%", "NOT SET");
                    gfsoutStr = gfsoutStr.Replace("%ACL%", "NOT SET");
                }

                if (gfsoutStr.Contains("%N%"))
                    gfsoutStr = gfsoutStr.Replace("%N%", (string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.MyStationURL) ? (TheBaseAssets.MyServiceHostInfo.MyDeviceInfo != null ? TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString() : "NOTSETYET") : (TheCommonUtils.CUri(TheBaseAssets.MyServiceHostInfo.MyStationURL, true)).Host));
                gfsoutStr = gfsoutStr.Replace("%D%", System.DateTime.Today.ToString("d", cultureInfo));  //no Today on DateTimeOffset
                gfsoutStr = gfsoutStr.Replace("%T%", System.DateTimeOffset.Now.ToString("T", cultureInfo));
                gfsoutStr = gfsoutStr.Replace("%DT%", System.DateTimeOffset.Now.ToString("G", cultureInfo));
                gfsoutStr = gfsoutStr.Replace("%DTO%", System.DateTimeOffset.Now.ToString("G", cultureInfo));
                if (gfsoutStr.Contains("%MAC%"))
                    gfsoutStr = gfsoutStr.Replace("%MAC%", Discovery.TheNetworkInfo.GetMACAddress(false));
                gfsoutStr = gfsoutStr.Replace("%APPNAME%", TheBaseAssets.MyServiceHostInfo.ApplicationName);
                gfsoutStr = gfsoutStr.Replace("%APPTITLE%", TheBaseAssets.MyServiceHostInfo.ApplicationTitle);
                if (gfsoutStr.Contains("%NODENAME%"))
                    gfsoutStr = gfsoutStr.Replace("%NODENAME%", !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.NodeName) ? TheBaseAssets.MyServiceHostInfo.NodeName : TheBaseAssets.MyServiceHostInfo.MyDeviceInfo != null ? TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString() : "NOTSETYET");
                if (gfsoutStr.Contains("%STATIONNAME%"))
                    gfsoutStr = gfsoutStr.Replace("%STATIONNAME%", !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.MyStationName) ? TheBaseAssets.MyServiceHostInfo.MyStationName : TheBaseAssets.MyServiceHostInfo.MyStationURL);
                if (gfsoutStr.Contains("%STATIONURL%"))
                    gfsoutStr = gfsoutStr.Replace("%STATIONURL%", !string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.MyStationURL) ? TheBaseAssets.MyServiceHostInfo.MyStationURL : "");
                if (gfsoutStr.Contains("%INSERTMETA%"))
                    gfsoutStr = gfsoutStr.Replace("%INSERTMETA%", TheBaseAssets.MyServiceHostInfo.GetMeta(""));
                gfsoutStr = gfsoutStr.Replace("%SITENAME%", TheBaseAssets.MyServiceHostInfo.SiteName);
                if (gfsoutStr.Contains("%DEVICEFILTER%"))
                    gfsoutStr = gfsoutStr.Replace("%DEVICEFILTER%", TheCommonUtils.GetMyDevicesSQLFilter());

                if (TheCDEngines.MyNMIService != null)
                {
                    gfsoutStr = gfsoutStr.Replace("%STARTSCREEN%", TheCDEngines.MyNMIService.MyNMIModel.StartScreen.ToString());
                    gfsoutStr = gfsoutStr.Replace("%ADMINPORTAL1%", TheCDEngines.MyNMIService.MyNMIModel.AdminPortal1.ToString());
                    gfsoutStr = gfsoutStr.Replace("%ADMINPORTAL2%", TheCDEngines.MyNMIService.MyNMIModel.AdminPortal2.ToString());
                    gfsoutStr = gfsoutStr.Replace("%MAINDASHBOARD%", TheCDEngines.MyNMIService.MyNMIModel.MainDashboardScreen.ToString());
                    if (gfsoutStr.Contains("%RegisteredControlTypes%"))
                        gfsoutStr = gfsoutStr.Replace("%RegisteredControlTypes%", TheCDEngines.MyNMIService.MyNMIModel.GetControlTypeList());
                    if (gfsoutStr.Contains("%GetNMIViews%"))
                        gfsoutStr = gfsoutStr.Replace("%GetNMIViews%", TheCDEngines.MyNMIService.MyNMIModel.GetNMIViews());
                    if (gfsoutStr.Contains("%GetLiveScreens%"))
                        gfsoutStr = gfsoutStr.Replace("%GetLiveScreens%", TheCDEngines.MyNMIService.MyNMIModel.GetLiveScreens());
                }

                if (gfsoutStr != gfsoutStr1) { gfsoutStr1 = gfsoutStr; continue; };
                tAppPos = gfsoutStr.IndexOf("%APP:");
                if (tAppPos >= 0)
                {
                    tAppPosEnd = gfsoutStr.IndexOf("%", tAppPos + 5);
                    if (tAppPosEnd >= 0)
                    {
                        tAppString = gfsoutStr.Substring(tAppPos + 5, tAppPosEnd - (tAppPos + 5));
                        var tAppStringParts = tAppString.Split(new char[] { ':' }, 2);
                        string tPAR = CStr(TheBaseAssets.MySettings.GetAppSetting(tAppStringParts[0], tAppStringParts.Length > 1 ? tAppStringParts[1] : null, false, true));
                        gfsoutStr = gfsoutStr.Replace($"%APP:{tAppString}%", tPAR);
                    }
                }
                if (gfsoutStr != gfsoutStr1) { gfsoutStr1 = gfsoutStr; continue; };
                if (pThing != null)
                {
                    List<cdeP> tProps = pThing.GetBaseThing().GetAllProperties();
                    foreach (cdeP p in tProps)
                    {
                        gfsoutStr = gfsoutStr.Replace(string.Format("%{0}%", p.Name), CStr(p.GetValue()));
                    }
                }

                tAppPos = gfsoutStr.IndexOf("%INT:");
                if (tAppPos >= 0)
                {
                    tAppPosEnd = gfsoutStr.IndexOf("%", tAppPos + 5);
                    if (tAppPosEnd >= 0)
                    {
                        tAppString = gfsoutStr.Substring(tAppPos + 5, tAppPosEnd - (tAppPos + 5));	//<Fix>V31,CM,3.0.63,8/24/2002,+5 to +6</Fix>
                        gfsoutStr = gfsoutStr.Replace($"%INT:{tAppString}%", CLng(tAppString).ToString(cultureInfo));
                    }
                }
                if (gfsoutStr != gfsoutStr1) { gfsoutStr1 = gfsoutStr; continue; };

                tAppPos = gfsoutStr.IndexOf("%$:");
                if (tAppPos >= 0)
                {
                    tAppPosEnd = gfsoutStr.IndexOf("%", tAppPos + 3);
                    if (tAppPosEnd >= 0)
                    {
                        tAppString = gfsoutStr.Substring(tAppPos + 3, tAppPosEnd - (tAppPos + 3));
                        double tDbl = CDbl(tAppString);
                        gfsoutStr = gfsoutStr.Replace($"%$:{tAppString}%", tDbl.ToString("$#,##0.00;($#,##0.00);Zero"));
                    }
                }
                //new in V4.0.0: format Strings
                tAppPos = gfsoutStr.IndexOf("%FD:");
                if (tAppPos >= 0)
                {
                    tAppPosEnd = gfsoutStr.IndexOf("%", tAppPos + 4);
                    if (tAppPosEnd >= 0)
                    {
                        tAppString = gfsoutStr.Substring(tAppPos + 4, tAppPosEnd - (tAppPos + 4));
                        string[] Parts = tAppString.Split(',');
                        if (Parts.Length == 2)
                        {
                            DateTimeOffset Dt = CDate(Parts[0]);
                            gfsoutStr = gfsoutStr.Replace($"%FD:{tAppString}%", Dt.ToString(Parts[1].ToString(), cultureInfo));
                        }
                        else
                        {
                            gfsoutStr = "wrong format";
                        }
                    }
                }

                tAppPos = gfsoutStr.IndexOf("%RND:");
                if (tAppPos >= 0)
                {
                    tAppPosEnd = gfsoutStr.IndexOf("%", tAppPos + 5);
                    if (tAppPosEnd >= 0)
                    {
                        tAppString = gfsoutStr.Substring(tAppPos + 5, tAppPosEnd - (tAppPos + 5));
                        uint rander = GetRandomUInt(0, (uint)CInt(tAppString));
                        gfsoutStr = gfsoutStr.Replace($"%RND:{tAppString}%", rander.ToString(cultureInfo));
                    }
                }

                tAppPos = gfsoutStr.IndexOf("%LDATE:");
                if (tAppPos >= 0)
                {
                    tAppPosEnd = gfsoutStr.IndexOf("%", tAppPos + 7);
                    if (tAppPosEnd >= 0)
                    {
                        tAppString = gfsoutStr.Substring(tAppPos + 7, tAppPosEnd - (tAppPos + 7));
                        DateTimeOffset dt = TheCommonUtils.CDate(tAppString);
                        gfsoutStr = gfsoutStr.Replace($"%LDATE:{tAppString}%", dt.ToString("D", cultureInfo));
                    }
                }

                if (gfsoutStr != gfsoutStr1) gfsoutStr1 = gfsoutStr; else tT = 0;
            }
            return gfsoutStr1;
        }
        #endregion

        /// <summary>
        /// Create a shallow copy of an object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="MyValue">The object to clone.</param>
        /// <param name="ResetBase">Whether to generate a unique object key (cdeMID) and creation time (cdeCTIM). </param>
        /// <returns>A shallow clone of the object. In case of error, the return value is null.</returns>
        /// <remarks>
        /// This function uses .NET Reflection to create a shallow copy of a reference type.
        /// For C-DEngine storage classes (which means classes derived from TheDataBase)
        /// this function can optionally generate a unique object key and creation time
        /// by setting the ResetBase field to true.
        /// </remarks>
        static public T ClonePublic<T>(T MyValue, bool ResetBase) where T : TheDataBase, new()
        {
            if (MyValue == null) return null;
            List<PropertyInfo> PropInfoArray = typeof(T).GetProperties().OrderBy(x => x.Name).ToList();
            T tNewVal = new T();
            foreach (PropertyInfo finfo in PropInfoArray)
            {
                try
                {
                    finfo.SetValue(tNewVal, finfo.GetValue(MyValue, null), null);
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(466, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCommonUtils", "ClonePublic", eMsgLevel.l1_Error, e.ToString()));
                }
            }
            if (ResetBase)
            {
                tNewVal.cdeMID = Guid.NewGuid();
                tNewVal.cdeCTIM = DateTimeOffset.Now;
            }
            return tNewVal;
        }

        /// <summary>
        /// Logs a session in the Session log for this Application in the StorageService
        /// If there is no StorageService Role defined for this application this call does not do anything
        /// </summary>
        /// <param name="pSessionID">Guid of the Session</param>
        /// <param name="pUrl">Current URL to be logged</param>
        /// <param name="pBrowser">Browser Type</param>
        /// <param name="pBrowserDesc">Browser Description</param>
        /// <param name="pRef">Referer</param>
        /// <param name="pCustomData">Any other custom data to be logged with the entries</param>
        static public void LogSession(Guid pSessionID, string pUrl, string pBrowser, string pBrowserDesc, string pRef, string pCustomData)
        {
            if (TheBaseAssets.MySession != null)
                TheBaseAssets.MySession.LogSession(pSessionID, pUrl, pBrowser, pBrowserDesc, pRef, pCustomData);
        }


        #region obsolete function - remove b4 OOS

        /// <summary>
        /// Returns a setting either known in the current Settings array or set as an environment variable or in App.Config
        /// First it looks into App.config and if this does not contain the setting it falls back to the existing MySettings and if that does not have the entry it looks in the environment variables.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [Obsolete("Please Use TheBaseAssets.MySettings.GetAppSetting()")]
        public static string GetSetting(string key)
        {
            var tres = TheBaseAssets.MySettings.GetAppSetting(key, "", false, false);
            return tres;
        }

        /// <summary>
        /// Retrieves a setting from the App.config/web.config "Configuration" Settings
        /// </summary>
        /// <param name="pSetting">Key of the Configuration setting</param>
        /// <param name="alt">Use this alternative if platform does not support the app.config</param>
        /// <param name="IsEncrypted">If set to true, the value is encrypted</param>
        /// <returns>The Value of the configuration setting</returns>
        [Obsolete("Please Use TheBaseAssets.MySettings.GetAppSetting()")]
        public static string cdeGetAppSetting(string pSetting, string alt, bool IsEncrypted)
        {
            return TheBaseAssets.MySettings.GetAppSetting(pSetting, alt, IsEncrypted, false);
        }
        /// <summary>
        /// Retrieves a setting from the App.config/web.config "Configuration" Settings
        /// </summary>
        /// <param name="pSetting">Key of the Configuration setting</param>
        /// <param name="alt">Use this alternative if platform does not support the app.config</param>
        /// <param name="IsEncrypted">If true, the value is encrypted</param>
        /// <param name="IsAltDefault">If true, set the value to the Alternative</param>
        /// <returns>The Value of the configuration setting</returns>
        [Obsolete("Please Use TheBaseAssets.MySettings.GetAppSetting()")]
        public static string cdeGetAppSetting(string pSetting, string alt, bool IsEncrypted, bool IsAltDefault)
        {
            return TheBaseAssets.MySettings?.GetAppSetting(pSetting, alt, IsEncrypted, IsAltDefault);
        }

        /// <summary>
        /// Deletes a setting from the app.config
        /// </summary>
        /// <param name="pKeyname"></param>
        [Obsolete("Please Use TheBaseAssets.MySettings.DeleteAppSetting()")]
        public static void cdeDeleteAppSetting(string pKeyname)
        {
            TheBaseAssets.MySettings.DeleteAppSetting(pKeyname);
        }

        [Obsolete("Please use TheISMManager.BackupCache")]
        public static string BackupCache(string pTitle)
        {
            return ISM.TheISMManager.BackupCache(pTitle);
        }

        [Obsolete("Please use TheISMManager.RestoreCache")]
        public static void RestoreCache(string pTitle)
        {
            ISM.TheISMManager.RestoreCache(pTitle);
        }

        /// <summary>
        /// Retrieves a Value for a Key provided at application host startup for values
        /// provided as parameters in the call to TheBaseApplication.StartBaseApplication.
        /// </summary>
        /// <param name="pKey">Case Sensitive Key to be retrieved.</param>
        /// <returns>Value provided as key, or null if not found or not available.</returns>
        [Obsolete("Use TheBaseAssets.MySettings.GetSetting() instead")]
        public static string GetCmdArgValue(string pKey)
        {
            return TheBaseAssets.MySettings.GetSetting(pKey);
        }
        #endregion
    }
}

