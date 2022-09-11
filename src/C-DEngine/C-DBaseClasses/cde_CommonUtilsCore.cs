// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Engines.ThingService;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// Small verison of TheCommonUtils used in the crypto Lib
    /// </summary>
    public partial class TheCommonUtils
    {
        #region Detectors
        /// <summary>
        /// Query whether underlying platform is Linux.
        /// </summary>
        /// <returns>True if Linux.</returns>
        public static bool IsOnLinux()
        {
            return Environment.OSVersion.Platform == PlatformID.Unix;
        }
        /// <summary>
        /// Query whether underlying platform is Apple Mac OS.
        /// </summary>
        /// <returns>True if Apple Mac OS.</returns>
        public static bool IsOnMAC()
        {
            return Environment.OSVersion.Platform == PlatformID.MacOSX;
        }

        /// <summary>
        /// Detect the presence of the portable .NET Core platform.
        /// </summary>
        /// <returns>True if running on .NET Core.</returns>
        public static bool IsNetCore()
        {
#if !CDE_NET35 && !CDE_NET4
            try
            {
                var assembly = typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly;
                return assembly.FullName.Contains("CoreLib");
            }
            catch
            {
                //intent
            }
#endif
            return false;
        }

        /// <summary>
        /// Only use this method if you need to know if you are running in the MONO Runtime. If you want to find out if you are running on Linux use TheCommonUtils.cdeIsFileSystemCaseSensitive or IsOnLinux insted
        /// </summary>
        /// <returns>true if CDE runs inside Mono Runtime</returns>
        public static bool IsMono()
        {
            return IsMonoRT();
        }
        /// <summary>
        /// Only use this method if you need to know if you are running in the MONO Runtime. If you want to find out if you are running on Linux use TheCommonUtils.cdeIsFileSystemCaseSensitive or IsOnLinux insted
        /// </summary>
        /// <returns>true if CDE runs inside Mono Runtime</returns>
        public static bool IsMonoRT()
        {
            if (TheBaseAssets.MyServiceHostInfo.MonoRTDetected)
                return TheBaseAssets.MyServiceHostInfo.MonoRTActive;
            TheBaseAssets.MyServiceHostInfo.MonoRTActive = (Type.GetType("Mono.Runtime") != null);
            TheBaseAssets.MyServiceHostInfo.MonoRTDetected = true;
            TheBaseAssets.MySYSLOG?.WriteToLog(new TSM("CommonUtilsCore", $"Is MonoRT Active:{TheBaseAssets.MyServiceHostInfo.MonoRTActive}", eMsgLevel.l6_Debug), 5019);
            return TheBaseAssets.MyServiceHostInfo.MonoRTActive;
        }
        #endregion

        #region Conversion helpers

        /// <summary>
        /// Converts an object to boolean.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to a boolean value. Safe means that
        /// no unhandled exceptions are generated. A null value is interpreted
        /// as 'false', as is any invalid value, or a string containing "0".
        /// A string containing "1" will be interpreted as 'true'.
        /// </remarks>
        public static bool CBool(object inObj)
        {
            if (inObj == null) return false;
            if (inObj is bool b1) return b1;
            if (inObj is string strInObj)
            {
                if (string.IsNullOrEmpty(strInObj)) return false;
                strInObj = strInObj.Trim();
                if (String.Equals(strInObj, "0")) return false;
                if (String.Equals(strInObj, "1")) return true;
            }
            bool retVal;
            try
            {
                if (inObj is IConvertible)
                {
                    retVal = Convert.ToBoolean(inObj);
                }
                else
                {
                    retVal = false;
                }
            }
            catch (Exception)
            {
                retVal = false;
            }
            return retVal;
        }

        /// <summary>
        /// Converts an object to an unsigned short integer.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to an unsigned integer. Safe means that
        /// no unhandled exceptions are generated. A null input value returns 0.
        /// Any invalid input value returns 0.
        /// If the input object is of type bool, 'true' returns 1 and 'false' returns 0.
        /// Input strings that start with "0x" are interpreted as hexidecimal values.
        /// </remarks>
        public static ushort CUShort(object inObj)
        {
            if (inObj == null) return 0;
            if (inObj is ushort s1) return s1;
            var inObjStr = CStr(inObj);
            if (string.IsNullOrEmpty(inObjStr)) return 0;
            ushort retVal;
            try
            {
                if (inObj is string && inObjStr.StartsWith("0x"))
                    retVal = ushort.Parse(inObjStr.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                else
                {
                    if (inObj is cdeP)
                        retVal = Convert.ToUInt16(CFloor(Convert.ToDouble(inObjStr, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                    else
                        retVal = Convert.ToUInt16(CFloor(Convert.ToDouble(inObj, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                retVal = 0;
            }
            return retVal;
        }
        /// <summary>
        /// Converts an object to a signed short integer.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to a signed integer. Safe means that
        /// no unhandled exceptions are generated. A null input value returns 0.
        /// Any invalid input value returns 0.
        /// If the input object is of type bool, 'true' returns 1 and 'false' returns 0.
        /// Input strings that start with "0x" are interpreted as hexidecimal values.
        /// </remarks>
        public static int CInt(object inObj)
        {
            if (inObj == null) return 0;
            if (inObj is int i1) return i1;
            var inObjStr = CStr(inObj);
            if (string.IsNullOrEmpty(inObjStr)) return 0;
            int retVal;
            try
            {
                if (inObj is string && inObjStr.StartsWith("0x"))
                    retVal = int.Parse(inObjStr.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                else
                {
                    if (inObj is cdeP)
                        retVal = Convert.ToInt32(CFloor(Convert.ToDouble(inObjStr, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                    else
                        retVal = Convert.ToInt32(CFloor(Convert.ToDouble(inObj, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                retVal = 0;
            }
            return retVal;
        }
        /// <summary>
        /// Converts an object to a Guid.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to a Guid value. Safe means that
        /// no unhandled exceptions are generated. A null input value returns Guid.Empty.
        /// Any invalid input value returns Guid.Empty.
        /// </remarks>
        public static Guid CGuid(object inObj)
        {
            if (inObj == null) return Guid.Empty;
            if (inObj is Guid g1) return g1;
            if (inObj is cdeP p1)
            {
                inObj = p1.GetValue();
            }
            Guid retGuid = Guid.Empty;
            try
            {
                if (inObj is byte[]) return (new Guid(inObj as byte[]));
                if (inObj is string s2 && s2 == "") return Guid.Empty;
                if (inObj is string s1 && s1.Length == 32)
                {
                    return cdeUUIDtoGuid(s1);
                }
#if CDE_NET35
                retGuid = new Guid(CStr(inObj));
#else
                Guid.TryParse(CStr(inObj), out retGuid);
#endif
            }
            catch
            {
                // ignored
            }
            return retGuid;
        }

        /// <summary>
        /// Converts an object to Double.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>If the object is boolean, 'true' converts to 1 and 'false' converts to 0.</remarks>
        public static double CDblWithBool(object inObj)
        {
            if (inObj is bool b1)
            {
                return b1 ? 1 : 0;
            }
            else if (CBool(inObj))
                return 1;
            else
                return CDbl(inObj);
        }

        /// <summary>
        /// Converts an object to a float.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to a float. Safe means that
        /// no unhandled exceptions are generated. A null input value returns 0.
        /// Any invalid input value returns 0.
        /// If the input object is of type bool, 'true' returns 1 and 'false' returns 0.
        /// Input strings that start with "0x" are interpreted as hexidecimal values.
        /// </remarks>
        public static float CFloat(object inObj)
        {
            if (inObj == null) return 0;
            if (inObj is float f1) return f1;
            var inObjStr = CStr(inObj);
            if (string.IsNullOrEmpty(inObjStr)) return 0;
            float retVal;
            try
            {
                if (inObj is string && inObjStr.StartsWith("0x"))
                    retVal = (float)double.Parse(inObjStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                else
                {
                    if (inObj is cdeP)
                        retVal = (float)Convert.ToDouble(inObjStr, CultureInfo.InvariantCulture);
                    else
                        retVal = (float)Convert.ToDouble(inObj, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                retVal = 0;
            }
            return retVal;
        }


        /// <summary>
        /// Converts a JSON notation date 'Date(ticks)' to a DateTimeOffset value.
        /// Supports input like this: "Date(1335205592410)"
        /// Or this: "\"\\/Date(1335205592410-0500)\\/", as produced by the
        /// .NET DataContractJsonSerializer.
        /// </summary>
        /// <param name="jsonTime">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        public static DateTimeOffset CJSONDateToDateTime(string jsonTime)
        {
            if (!string.IsNullOrEmpty(jsonTime) && jsonTime.IndexOf("Date") > -1)
            {
                // Remove all extranous delimiters
                int iOpenParen = jsonTime.IndexOf("(");
                int iCloseParen = jsonTime.IndexOf(")");
                int ccCopy = iCloseParen - iOpenParen - 1;
                if (iOpenParen > 0 && ccCopy > 0)
                {
                    string milis = jsonTime.Substring(iOpenParen + 1, ccCopy);

                    TimeSpan offset = DateTimeOffset.Now.Offset; // Default to current time zone

                    // If a time-zone value is provided, use it.
                    int offsetIndex = milis.IndexOfAny("+-".ToCharArray());
                    if (offsetIndex > 0)
                    {
                        offset = new TimeSpan(CInt(milis.Substring(offsetIndex, milis.Length - 2 - offsetIndex)), 0, 0);
                        milis = milis.Substring(0, offsetIndex);
                    }

                    DateTimeOffset origin = new (1970, 1, 1, 0, 0, 0, offset);
                    return origin.AddMilliseconds(CLng(milis));
                }
            }
            return DateTimeOffset.MinValue;
        }

        /// <summary>
        /// Converts an input date into the JSON Date(ticks) notation.
        /// </summary>
        /// <param name="tDate">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        public static string CDateTimeToJSONDate(DateTimeOffset tDate)
        {
            long ticks = tDate.Subtract(iEpochTime).Ticks / 10000; //Epoch no need for Offset
            return string.Format("Date({0})", ticks);
        }
        private static readonly DateTime iEpochTime = new (1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private static DateTimeOffset iEpochTimeOffsetUtc = new (iEpochTime, new TimeSpan(0));

        /// <summary>
        /// Converts an incoming object to a TimeSpan
        /// </summary>
        /// <param name="inObj">Incoming object</param>
        /// <returns></returns>
        public static TimeSpan CTimeSpan(object inObj)
        {
            TimeSpan ret = TimeSpan.Zero;
            if (inObj is cdeP p1)
            {
                inObj = p1?.GetValue();
            }
            if (inObj is TimeSpan s1)
            {
                ret = s1;
            }
            else if (inObj is DateTimeOffset d1)
            {
                try
                {
                    ret = (d1 - iEpochTimeOffsetUtc);
                }
                catch { 
                    //intent
                }
            }
            else if (inObj is DateTime t1)
            {
                try
                {
                    ret = (t1.ToUniversalTime() - iEpochTimeOffsetUtc);
                }
                catch { 
                    //intent
                }
            }
            else if (inObj is string s2)
            {
#if !CDE_NET35
                if (!TimeSpan.TryParse(s2, CultureInfo.InvariantCulture, out ret))
                { 
                    //intent
                }
#else
                if (!TimeSpan.TryParse((string)inObj, out ret))
                { }
#endif
            }
            return ret;
        }

        /// <summary>
        /// Converts an object to a character.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to a Char, a UTF-16 code unit.
        /// Safe means that no unhandled exceptions are generated.
        /// If the object cannot be converted, the function returns (Char)0.
        /// </remarks>
        public static Char CChar(object inObj)
        {
            if (inObj == null) return (char)0;
            if (inObj is Char c1) return c1;
            var inObjStr = CStr(inObj);
            if (string.IsNullOrEmpty(inObjStr)) return (char)0;

            Char retVal;
            try
            {
                retVal = Convert.ToChar(inObj, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                retVal = (Char)0;
            }
            return retVal;
        }

        /// <summary>
        /// Converts an object to a byte.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to an unsigned byte value. Safe means that
        /// no unhandled exceptions are generated. A null input returns a value of zero.
        /// An empty string also returns zero. To handle values over 256, modulo of 256 is returned.
        /// </remarks>
        public static Byte CByte(object inObj)
        {
            if (inObj == null) return 0;
            if (inObj is Byte b1) return b1;
            var inObjStr = CStr(inObj);
            if (String.IsNullOrEmpty(inObjStr)) return 0;
            Byte retVal;
            try
            {
                int tIn = CInt(inObj);
                retVal = Convert.ToByte(tIn % 256, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                retVal = 0;
            }
            return retVal;
        }

        /// <summary>
        /// Converts an object to an 8-bit signed integer.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to an 8-bit signed integer.
        /// Safe means that no unhandled exceptions are generated. A null input value returns 0.
        /// Any invalid input value returns 0.
        /// If the input object is of type bool, 'true' returns 1 and 'false' returns 0.
        /// Input strings that start with "0x" are interpreted as hexidecimal values.
        /// </remarks>
        public static SByte CSByte(object inObj)
        {
            if (inObj == null) return 0;
            if (inObj is SByte b1) return b1;
            var inObjStr = CStr(inObj);
            if (String.IsNullOrEmpty(inObjStr)) return 0;
            SByte retVal;
            try
            {
                int tIn = CInt(inObj);
                retVal = Convert.ToSByte(tIn % 128, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                retVal = 0;
            }
            return retVal;
        }
        /// <summary>
        /// Converts an object to a short signed integer.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to a short. Safe means that
        /// no unhandled exceptions are generated. A null input value returns 0.
        /// Any invalid input value returns 0.
        /// If the input object is of type bool, 'true' returns 1 and 'false' returns 0.
        /// Input strings that start with "0x" are interpreted as hexidecimal values.
        /// </remarks>
        public static short CShort(object inObj)
        {
            if (inObj == null) return 0;
            if (inObj is short c1) return c1;
            var inObjStr = CStr(inObj);
            if (String.IsNullOrEmpty(inObjStr)) return 0;
            short retVal;
            try
            {
                if (inObj is string && inObjStr.StartsWith("0x"))
                    retVal = short.Parse(inObjStr.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                else
                {
                    if (inObj is cdeP)
                        retVal = Convert.ToInt16(CFloor(Convert.ToDouble(inObjStr, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                    else
                        retVal = Convert.ToInt16(CFloor(Convert.ToDouble(inObj, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                retVal = 0;
            }
            return retVal;
        }

        /// <summary>
        /// Converts a string to a date.
        /// </summary>
        /// <param name="strIn"></param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to DateTimeOffset. Safe means that
        /// no unhandled exceptions are generated.
        /// If the conversion is not possible, the function returns DateTimeOffset.MinValue.
        /// </remarks>
        public static DateTimeOffset CDate(string strIn)
        {
            DateTimeOffset ret = DateTimeOffset.MinValue;
            if (String.IsNullOrEmpty(strIn)) return ret;
            ret = CJSONDateToDateTime(strIn);
            if (ret == DateTimeOffset.MinValue)
            {
                try
                {
                    ret = DateTimeOffset.Parse(strIn, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // ignored
                }
            }
            return ret;
        }

        /// <summary>
        /// Convert an object to a date.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to DateTimeOffset. Safe means that
        /// no unhandled exceptions are generated.
        /// If the conversion is not possible, the function returns DateTimeOffset.MinValue.
        /// </remarks>
        public static DateTimeOffset CDate(object inObj)
        {
            DateTimeOffset ret;
            if (inObj is DateTimeOffset d1)
            {
                ret = d1;
            }
            else if (inObj is DateTime t1)
            {
                return CDateTimeToDateTimeOffsetInternal(t1);
            }
            else if (inObj is string s1)
            {
                ret = CDate(s1);
            }
            else
            {
                try
                {
                    DateTime localTime1;
                    if (inObj is cdeP)
                    {
                        localTime1 = Convert.ToDateTime(CStr(inObj), CultureInfo.InvariantCulture);   // CODE REVIEW: Why force CStr first here? This can lose time zone information etc.
                    }
                    else
                    {
                        localTime1 = Convert.ToDateTime(inObj, CultureInfo.InvariantCulture);    //Can crash if object is not convertible
                    }
                    ret = CDateTimeToDateTimeOffsetInternal(localTime1);
                }
                catch (Exception)
                {
                    ret = DateTimeOffset.MinValue;
                }
            }
            return ret;
        }

        internal static DateTimeOffset CDateTimeToDateTimeOffsetInternal(DateTime dateTime)
        {
            DateTimeOffset ret;
            if (dateTime == DateTime.MinValue)
            {
                ret = DateTimeOffset.MinValue; // implicit conversion will fail for timezones with offset > 0, so do it explicitly to avoid exception
            }
            else if (dateTime == DateTime.MaxValue)
            {
                ret = DateTimeOffset.MaxValue; // implicit conversion will fail for timezones with offset < 0, so do it explicitly to avoid exception
            }
            else
            {
                if (dateTime.Kind == DateTimeKind.Unspecified)
                {
                    dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Local);
                }
                try
                {
                    ret = dateTime.ToLocalTime(); // this does implicit conversion from DateTime to DateTimeOffset, which can fail (hence all this logic around it
                }
                catch (ArgumentOutOfRangeException)
                {
                    // The localTime1 was close enough to MinValue or MaxValue (within the offset of the current timezone) that it could not be represented as a DateTimeOffset
                    // Round to the MaxValue or MinValue
                    ret = dateTime.Year > 9000 ? DateTimeOffset.MaxValue : DateTimeOffset.MinValue;
                }
            }
            return ret;
        }

        /// <summary>
        /// Convert an object to double.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to a double. Safe means that
        /// no unhandled exceptions are generated. A null input value returns 0.
        /// Any invalid input value returns 0.
        /// If the input object is of type bool, 'true' returns 1 and 'false' returns 0.
        /// Input strings that start with "0x" are interpreted as hexidecimal values.
        /// </remarks>
        public static double CDbl(object inObj)
        {
            if (inObj == null) return 0;
            if (inObj is double d1) return d1;
            var inObjStr = CStr(inObj);
            if (string.IsNullOrEmpty(inObjStr)) return 0;
            double retVal;
            try
            {
                if (inObj is bool && CBool(inObj)) return 1;
                if (inObj is string s2)
                {
                    if (String.Equals(s2, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        return 1;
                    }
                    if (s2.StartsWith("0x"))
                    {
                        return double.Parse(s2, NumberStyles.HexNumber);
                    }
                }
                if (inObj is cdeP)
                    retVal = Convert.ToDouble(inObjStr, CultureInfo.InvariantCulture);
                else
                {
                    if (inObj is string s3)
                        double.TryParse(s3, NumberStyles.Any, CultureInfo.InvariantCulture, out retVal);
                    else
                        retVal = Convert.ToDouble(inObj, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                retVal = 0;
            }
            return retVal;
        }

        /// <summary>
        /// Converts an object to an unsigned integer.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to an unsigned integer. Safe means that
        /// no unhandled exceptions are generated. A null input value returns 0.
        /// Any invalid input value returns 0.
        /// If the input object is of type bool, 'true' returns 1 and 'false' returns 0.
        /// Input strings that start with "0x" are interpreted as hexidecimal values.
        /// </remarks>
        public static uint CUInt(object inObj)
        {
            if (inObj == null) return 0;
            if (inObj is uint u1) return u1;
            var inObjStr = CStr(inObj);
            if (String.IsNullOrEmpty(inObjStr)) return 0;
            uint retVal;
            try
            {
                if (inObj is string && inObjStr.StartsWith("0x"))
                    retVal = uint.Parse(inObjStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                else
                {
                    if (inObj is cdeP)
                        retVal = Convert.ToUInt32(CFloor(Convert.ToDouble(inObjStr, CultureInfo.InvariantCulture)));
                    else
                        retVal = Convert.ToUInt32(CFloor(Convert.ToDouble(inObj, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                retVal = 0;
            }
            return retVal;
        }


        /// <summary>
        /// Converts an object to an unsigned long integer.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to an unsigned long. Safe means that
        /// no unhandled exceptions are generated. A null input value returns 0.
        /// Any invalid input value returns 0.
        /// If the input object is of type bool, 'true' returns 1 and 'false' returns 0.
        /// Input strings that start with "0x" are interpreted as hexidecimal values.
        /// </remarks>
        public static ulong CULng(object inObj)
        {
            if (inObj == null) return 0;
            if (inObj is ulong u1) return u1;
            var inObjStr = CStr(inObj);
            if (String.IsNullOrEmpty(inObjStr)) return 0;
            ulong retVal;
            try
            {
                if (inObj is string && inObjStr.StartsWith("0x"))
                    retVal = ulong.Parse(inObjStr, NumberStyles.HexNumber);
                else
                {
                    if (inObj is cdeP)
                        retVal = Convert.ToUInt64(CFloor(Convert.ToDouble(inObjStr, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                    else
                        retVal = Convert.ToUInt64(CFloor(Convert.ToDouble(inObj, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                retVal = 0;
            }
            return retVal;
        }

        /// <summary>
        /// Returns Math.Floor. On platforms that do not support math library, the
        /// value is calculated by casting to (double)(int) to get the floor.
        /// </summary>
        /// <param name="InVal"></param>
        /// <returns>A value rounded down.</returns>
        private static double CFloor(double InVal)
        {
            return Math.Floor(InVal);
        }
        /// <summary>
        /// Converts an object to signed long integer.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert any input to a long value. Safe means that
        /// no unhandled exceptions are generated. A null input value returns 0.
        /// Any invalid input value returns 0.
        /// If the input object is of type bool, 'true' returns 1 and 'false' returns 0.
        /// Input strings that start with "0x" are interpreted as hexidecimal values.
        /// </remarks>
        public static long CLng(object inObj)
        {
            if (inObj == null) return 0;
            if (inObj is long l1) return l1;
            var inObjStr = CStr(inObj);
            if (String.IsNullOrEmpty(inObjStr)) return 0;
            long retVal;
            try
            {
                if (inObj is string && inObjStr.StartsWith("0x"))
                    retVal = long.Parse(inObjStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                else
                {
                    if (inObj is cdeP)
                        retVal = Convert.ToInt64(CFloor(Convert.ToDouble(inObjStr, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                    else
                        retVal = Convert.ToInt64(CFloor(Convert.ToDouble(inObj, CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                retVal = 0;
            }
            return retVal;
        }

        /// <summary>
        /// Normalize a UUID to conform to the Microsoft GUID format.
        /// </summary>
        /// <param name="pUid">A string containing a UUID.</param>
        /// <returns>A Guid.</returns>
        public static Guid cdeUUIDtoGuid(string pUid)
        {
            Guid res = Guid.Empty;

            pUid = pUid.Replace("-", "");
            if (pUid.Length > 21)
            {
                pUid = pUid.Substring(0, 8) + "-" + pUid.Substring(8, 4) + "-" + pUid.Substring(12, 4) + "-" + pUid.Substring(16, 4) + "-" + pUid.Substring(20, pUid.Length - 20);
                if (pUid.Length > 36)
                    pUid = pUid.Substring(0, 36);
                else
                {
                    if (pUid.Length < 36)
                        pUid += "000000000000".Substring(0, 36 - pUid.Length);
                }
                res = CGuid(pUid);
            }
            return res;
        }

        /// <summary>
        /// Converts an object to a string.
        /// </summary>
        /// <param name="inObj">Input to be converted.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// A safe, efficient function to convert an object to a string. Safe means that
        /// no unhandled exceptions are generated. The conversion is done in a
        /// culturally neutral manner (CultureInfo.InvariantCulture).
        /// </remarks>
        public static string CStr(object inObj)
        {
            if (inObj == null) return "";
            if (inObj is string s1) return s1;
            try
            {
                if (inObj is DateTime t1) // DateTime is IConvertible so must check this first
                {
                    return t1.ToString("O", CultureInfo.InvariantCulture);
                }
                else if (inObj is IConvertible ic)
                {
                    return ic.ToString(CultureInfo.InvariantCulture);
                }
                else if (inObj is DateTimeOffset d1)
                {
                    return d1.ToString("O", CultureInfo.InvariantCulture);
                }
                else if (inObj is IFormattable formattable)
                {
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                }
                else if (inObj is byte[])
                {
                    return Convert.ToBase64String(inObj as byte[]);
                }
            }
            catch { 
                //intent
            }
            return inObj.ToString(); // ToString() OK - last resort, all cultureinvariant ways to convert to string exhausted
        }

        /// <summary>
        /// Convert a byte array into a hex string.
        /// </summary>
        /// <param name="byteValue">Input byte array.</param>
        /// <returns>A string.</returns>
        public static string ToHexString(byte[] byteValue)
        {
            if (byteValue == null || byteValue.Length == 0)
                return "";
            char[] lookup = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
            int i = 0, p = 0, l = byteValue.Length;
            char[] c = new char[l * 2];
            while (i < l)
            {
                byte d = byteValue[i++];
                c[p++] = lookup[d / 0x10];
                c[p++] = lookup[d % 0x10];
            }
            return new string(c, 0, c.Length);
        }
        /// <summary>
        /// Convert a hex string into a byte array.
        /// </summary>
        /// <param name="str">An input string.</param>
        /// <returns>A byte array.</returns>
        public static byte[] ToHexByte(string str)
        {
            if (string.IsNullOrEmpty(str))
                return null;
            if (str.Length % 2 == 1) //Fix here
                str += "0";
            byte[] b = new byte[str.Length / 2];
            for (int y = 0, x = 0; x < str.Length; ++y, x++)
            {
                byte c1 = (byte)str[x];
                if (c1 > 0x60) c1 -= 0x57;
                else if (c1 > 0x40) c1 -= 0x37;
                else c1 -= 0x30;
                byte c2 = (byte)str[++x];
                if (c2 > 0x60) c2 -= 0x57;
                else if (c2 > 0x40) c2 -= 0x37;
                else c2 -= 0x30;
                b[y] = (byte)((c1 << 4) + c2);
            }
            return b;
        }

        /// <summary>
        /// Add escapes for XML special characters.
        /// </summary>
        /// <param name="strInput">The input string.</param>
        /// <returns>The converted string.</returns>
        /// <remarks>
        /// This function modifies a string to convert XML delimiter characters into their
        /// safe equivalent. Supported characters include ampersand, less than,
        /// greater than, quote, and apostrophe (&quot;&amp;&quot;, &quot;&gt;&quot;, &quot;&lt;&quot;, &amp;&quot;&amp;, and &quot;&amp;&quot;).
        /// </remarks>
        public static string cdeESCXML(string strInput)
        {
            return strInput.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }
        /// <summary>
        /// Add escapes for XML special characters plus newline break.
        /// </summary>
        /// <param name="strInput">The input string.</param>
        /// <returns>The converted string.</returns>
        /// <remarks>
        /// Modify a string to convert XML delimiter characters into their
        /// safe equivalent. Supported characters include ampersand, less than,
        /// greater than, quote, and apostrophe (&quot;&amp;&quot;, &quot;&gt;&quot;, &quot;&lt;&quot;, &amp;&quot;&amp;, and &quot;&amp;&quot;).
        /// Also supported is the %BR% character.
        /// </remarks>
        public static string cdeESCXMLwBR(string strInput)
        {
            string t = strInput.Replace("<br/>", "%BR%");
            t = t.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
            return t.Replace("%BR%", "<br/>");
        }
        /// <summary>
        /// Remove XML special character escapes.
        /// </summary>
        /// <param name="strInput"></param>
        /// <returns>The converted string.</returns>
        /// <remarks>
        /// Modify a string to convert any escaped XML characters back to their
        /// regular, unescaped value. Supported characters include ampersand, less than,
        /// greater than, quote, and apostrophe (&quot;&amp;&quot;, &quot;&gt;&quot;, &quot;&lt;&quot;, &amp;&quot;&amp;, and &quot;&amp;&quot;).
        /// </remarks>
        public static string cdeUnESCXML(string strInput)
        {
            return strInput.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'");
        }

        /// <summary>
        /// Create an XML element.
        /// </summary>
        /// <param name="strInput">The stand-alone XML element (when pTag is null) or the XML content (when pTag is non-null).</param>
        /// <param name="pTag">Either null, or the outer XML tag name.</param>
        /// <returns>A string containing XML.</returns>
        /// <remarks>
        /// Create a stand-alone XML element, or an XML element between a provided tag.
        /// When null is provided for the tag, the result is &gt;strInput /> followed by newline character.
        /// When a non-null value is provided for the tag, the result is &gt;tag> strInput &gt;/tag>
        /// </remarks>
        public static string cdeCreateXMLElement(string strInput, string pTag)
        {
            if (string.IsNullOrEmpty(strInput)) return null;
            return string.IsNullOrEmpty(pTag) ? string.Format("<{0} />\n", strInput) : string.Format("<{0}>{1}</{0}>\n", strInput, cdeESCXML(pTag));
        }
        /// <summary>
        /// Create an XML element.
        /// </summary>
        /// <param name="strInput">The stand-alone XML element (when pTag is null) or the XML content (when pTag is non-null).</param>
        /// <param name="pTag">Either null, or the outer XML tag name as a byte array.</param>
        /// <returns>A string containing XML.</returns>
        /// <remarks>
        /// Create a stand-alone XML element, or an XML element between a provided tag.
        /// When null is provided for the tag, the result is &gt;strInput /> followed by newline character.
        /// When a non-null value is provided for the tag, the result is &gt;tag> strInput &gt;/tag>
        /// </remarks>
        public static string cdeCreateXMLElement(string strInput, byte[] pTag)
        {
            if (string.IsNullOrEmpty(strInput)) return null;
            return pTag == null ? string.Format("<{0} />\n", strInput) : string.Format("<{0}>{1}</{0}>", strInput, Convert.ToBase64String(pTag));
        }

        /// <summary>
        /// Converts a byte array using UTF8 encoding to a character string.
        /// </summary>
        /// <param name="pIn">Input array of UTF8 characters, stored as byte value.</param>
        /// <returns>The converted string.</returns>
        public static string CArray2UTF8String(byte[] pIn)
        {
            if (pIn == null) return "";
            return CArray2UTF8String(pIn, 0, pIn.Length);
        }

        /// <summary>
        /// Converts a byte array using UTF8 encoding to a character string.
        /// </summary>
        /// <param name="pIn">Input array of UTF8 characters, stored as byte value.</param>
        /// <param name="index">Index of input array beginning of range of items to include in the conversion.</param>
        /// <param name="count">Count of array items to include in conversion.</param>
        /// <returns>The converted string.</returns>
        public static string CArray2UTF8String(byte[] pIn, int index, int count)
        {
            UTF8Encoding enc = new ();
            return enc.GetString(pIn, index, count);
        }

        /// <summary>
        /// Converts a string to a byte array using UTF8 encoding.
        /// </summary>
        /// <param name="strIn">Input string with values to include in output array.</param>
        /// <returns>A byte array.</returns>
        public static byte[] CUTF8String2Array(string strIn)
        {
            UTF8Encoding enc = new ();
            return enc.GetBytes(strIn);
        }

        /// <summary>
        /// Converts a portion of a Unicode-encoded byte array to a string.
        /// </summary>
        /// <param name="pIn">Input byte array containing Unicode characters.</param>
        /// <param name="index">Index of first byte to convert.</param>
        /// <param name="count">Number of bytes to include in conversion.</param>
        /// <returns>A string.</returns>
        public static string CArray2UnicodeString(byte[] pIn, int index, int count)
        {
            UnicodeEncoding enc = new ();
            return enc.GetString(pIn, index, count);
        }


        /// <summary>
        /// Converts a string into a byte array using Unicode encoding.
        /// </summary>
        /// <param name="strIn">The string to convert.</param>
        /// <returns>A byte array.</returns>
        public static byte[] CUnicodeString2Array(string strIn)
        {
            UnicodeEncoding enc = new ();
            return enc.GetBytes(strIn);
        }

        /// <summary>
        /// Convert a string to a byte array.
        /// </summary>
        /// <param name="strIn">String to convert.</param>
        /// <returns>A byte array with the results of the conversion, or null on error.</returns>
        /// <remarks>
        /// A safe function to convert a string into a byte array. Safe means that
        /// no unhandled exceptions are generated.
        /// </remarks>
        public static byte[] CStringToByteArray(string strIn)
        {
            if (string.IsNullOrEmpty(strIn)) return null;
            try
            {
                return Enumerable.Range(0, strIn.Length)
                                 .Where(x => x % 2 == 0)
                                 .Select(x => Convert.ToByte(strIn.Substring(x, 2), 16))
                                 .ToArray();
            }
            catch
            {
                //ignored
            }
            return null;
        }

        /// <summary>
        /// Split a string into a list.
        /// </summary>
        /// <param name="strIn">The input string to parse.</param>
        /// <param name="sep">The separator character.</param>
        /// <returns>A List holding the results of the parsing.</returns>
        /// <remarks>
        /// Divide an input string into a set of strings, using a
        /// single separator character as the separator between each string.
        /// Put the results into a List.
        /// </remarks>
        public static List<string> CStringToList(string strIn, char sep)
        {
            if (strIn == null) return null;
            return strIn.Split(sep).ToList();
        }

        /// <summary>
        /// Search for a token in a compound string,
        /// </summary>
        /// <param name="strIn">A compound string made up of multiple tokens.</param>
        /// <param name="pSearchStr">The token to search for.</param>
        /// <param name="sep">The delimiter character in the compound string.</param>
        /// <returns>A zero-based index for the string when found.
        /// Returns -1 means token not found.</returns>
        /// <remarks>
        /// A compound string is a string made up of multiple tokens, with a delimiter character
        /// separating tokens from one another.
        /// The return value is not the character position, but instead
        /// is the count of delimiter characters from the start of the string
        /// to the search string.
        /// For example, in the following call CStringPosInStringList("first;second;third", "second", ';')
        /// the function returns 1.
        /// </remarks>
        public static int CStringPosInStringList(string strIn, string pSearchStr, char sep)
        {
            List<string> tList = CStringToList(strIn, sep);
            int pos = 0;
            bool found = false;
            foreach (string t in tList)
            {
                if (t.Equals(pSearchStr, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
                pos++;
            }
            if (found)
                return pos;
            else
                return -1;
        }

        /// <summary>
        /// Converts a collection into a string.
        /// </summary>
        /// <param name="pList">Reference to a string collection.</param>
        /// <param name="Sep">A string to insert between each item.</param>
        /// <returns>A concatenated string with delimiter 'sep' between each part.</returns>
        /// <remarks>
        /// This function converts a collection of strings to a
        /// single string consisting. Individual items in the resulting
        /// string are separated from each other by the 'sep' delimiter.
        /// Empty input objects are ignored.
        /// </remarks>
        public static string CListToString(ICollection<string> pList, string Sep)
        {
            if (pList == null) return null;
            string AllTopics = "";
            foreach (string t in pList)
            {
                if (string.IsNullOrEmpty(t)) continue;
                if (AllTopics.Length > 0) AllTopics += Sep;
                AllTopics += t;
            }
            return AllTopics;
        }

        /// <summary>
        /// Converts a collection into a string.
        /// </summary>
        /// <typeparam name="T">Generic Type parameter of input collection.</typeparam>
        /// <param name="pList">Reference to a string collection.</param>
        /// <param name="Sep">A string to insert between each item.</param>
        /// <returns>A concatenated string with delimiter 'sep' between each part.</returns>
        /// <remarks>
        /// Converts a collection of objects of type T to a single string
        /// containing each of the input objects separated by the 'sep' delimiter.
        /// Empty input objects are ignored.
        /// </remarks>
        public static string CListToString<T>(ICollection<T> pList, string Sep)
        {
            if (pList == null) return null;
            string AllTopics = "";
            foreach (T tObj in pList)
            {
                if (tObj == null) continue;
                string t = CStr(tObj);
                if (string.IsNullOrEmpty(t)) continue;
                if (AllTopics.Length > 0) AllTopics += Sep;
                AllTopics += t;
            }
            return AllTopics;
        }

        /// <summary>
        /// Converts a collection into a string.
        /// </summary>
        /// <param name="pList">Reference to a string collection.</param>
        /// <param name="Sep">A string to insert between each item.</param>
        /// <returns>A concatenated string with delimiter 'sep' between each part.</returns>
        /// <remarks>
        /// Converts a list of strings to a single string consisting of
        /// each of the input objects separated by the 'sep' delimiter.
        /// Empty list objects are ignored.
        /// </remarks>
        public static string CListToString(List<string> pList, string Sep)
        {
            if (pList == null) return null;
            StringBuilder AllTopics = new (512);
            foreach (string t in pList)
            {
                if (string.IsNullOrEmpty(t)) continue;
                if (AllTopics.Length > 0) AllTopics.Append(Sep);
                AllTopics.Append(t);
            }
            return AllTopics.ToString(); // ToString() OK
        }

        /// <summary>
        /// Converts an array of strings a single string.
        /// </summary>
        /// <param name="pList">The input array of strings.</param>
        /// <param name="Sep">The delimiter inserted between strings.</param>
        /// <returns>A single string.</returns>
        public static string CListToString(string[] pList, string Sep)
        {
            if (pList == null) return null;
            string AllTopics = "";
            foreach (string t in pList)
            {
                if (string.IsNullOrEmpty(t)) continue;
                if (AllTopics.Length > 0) AllTopics += Sep;
                AllTopics += t;
            }
            return AllTopics;
        }
        #endregion

        #region Randomizer
        private static readonly IRandomizer cdeRND = new cdeRandomizer(DateTime.Now.Millisecond);   //No Offset needed

        /// <summary>
        /// Generate a pseudo-random number.
        /// </summary>
        /// <returns>A random double value.</returns>
        public static double GetRandomDouble()
        {
            lock (cdeRND)
                return cdeRND.NextDouble();
        }
        /// <summary>
        /// Generate a pseudo-random number.
        /// </summary>
        /// <param name="pMin"></param>
        /// <param name="pMax"></param>
        /// <returns>A random unsigned integer.</returns>
        public static uint GetRandomUInt(uint pMin, uint pMax)
        {
            return cdeRND.NextUInt(pMin, pMax);
        }

        internal interface IRandomizer
        {
            double NextDouble();
            uint NextUInt(uint pMin, uint pMax);
        }
        internal class cdeRandomizer : IRandomizer
        {
            /// <summary>
            /// Creates a new pseudo-random number generator with a given seed.
            /// </summary>
            /// <param name="seed">A value to use as a seed.</param>
            public cdeRandomizer(int seed)
            {
                init((uint)seed);
            }

            private RandomNumberGenerator mRandom;

            private void init(uint seed)
            {
                mRandom = RandomNumberGenerator.Create(); // Compliant for security-sensitive use cases
            }

            public uint NextUInt(uint pMin, uint pMax)
            {
                if (pMin == pMax) return pMin;
                if (pMin > pMax)
                {
                    (pMin, pMax) = (pMax, pMin);
                }
                lock (mRandom)
                {
                    byte[] data = new byte[16];
                    mRandom.GetBytes(data);
                    var ul = BitConverter.ToUInt64(data, 0);
                    return (uint)((ul % (pMax - pMin)) + pMin);
                }
            }

            public double NextDouble()
            {
                lock (mRandom)
                {
                    byte[] data = new byte[16];
                    mRandom.GetBytes(data);
                    var ul = BitConverter.ToUInt64(data, 0) >>11;
                    return ul / (double)(1UL << 53);
                }
            }
        }

        #endregion

        #region Process Helpers
        /// <summary>
        /// Callback delegate with state
        /// </summary>
        /// <param name="state">State</param>
        public delegate void cdeWaitCallback(object state);
        /// <summary>
        /// Runs code asynchronously without waiting for the result.
        /// </summary>
        /// <param name="pThreadName">Thread name (to assist in debugging).</param>
        /// <param name="TrapExceptions">The Callback Code will be encapsulated in a Try/Catch block.</param>
        /// <param name="callBack">Code to execute.</param>
        /// <param name="pState">Initial state to pass to callback function (optional).</param>
        /// <returns>True when task successfully created.</returns>
        /// <seealso cref="cdeRunTaskAsync"/>
        public static bool cdeRunAsync(string pThreadName, bool TrapExceptions, cdeWaitCallback callBack, object pState = null)
        {
            return cdeRunAsync(pThreadName, TrapExceptions, callBack, 0, pState);
        }

        /// <summary>
        /// New in V4.105: Allows to fire a generic event on all supported platforms
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action">Callback to be fired by the Event</param>
        /// <param name="para">Parameter of same type as action() parameter</param>
        /// <param name="FireAsync">if true, the event is fired asynch</param>
        /// <param name="pFireEventTimeout">if larger </param>
        public static void DoFireEvent<T>(Action<T> action, T para, bool FireAsync, int pFireEventTimeout = 0)
        {
            if (action == null) return;
            if (action.GetInvocationList().Length == 0) return;
            if (FireAsync)
            {
                if (pFireEventTimeout == 0)
                    pFireEventTimeout = TheBaseAssets.MyServiceHostInfo.EventTimeout; //3.2 was hardcoded 60000
                if (pFireEventTimeout < 0)
                {
                    TheCommonUtils.cdeRunAsync("DFE-T No Timeout", true, (o) => action(para), null);
                }
                else
                {
                    TheCommonUtils.cdeRunAsync("DFE-T with Timeout", true, (o) =>
                    {
                        DoFireEventParallelInternal(action, a =>
                        {
                            var innerAction = (a as Action<T>);
                            if (innerAction == null)
                            {
                                // This should never happen
                            }
                            innerAction?.Invoke(para);
                        }, pFireEventTimeout);

                    }, null);
                }
            }
            else
            {
                try
                {
                    action(para);
                }
                catch
                {
                    // Repurposing timeout KPIs as FireAsync is (currently) a global setting
                    TheCDEKPIs.IncrementKPI(eKPINames.EventTimeouts);
                    TheCDEKPIs.IncrementKPI(eKPINames.TotalEventTimeouts);
                }
            }
        }
        /// <summary>
        /// New in V4.105: Allows to fire a generic event on all supported platforms
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action">Callback to be fired by the Event. First parameter is generic, second is a cookie object</param>
        /// <param name="para">Parameter of same type as action() first generic parameter</param>
        /// <param name="Para2">cookie object to be fired with the callback</param>
        /// <param name="FireAsync">if true, the event is fired asynch</param>
        /// <param name="pFireEventTimeout">if larger </param>
        public static void DoFireEvent<T>(Action<T, object> action, T para, object Para2, bool FireAsync, int pFireEventTimeout = 0)
        {
            DoFireEvent<T, object>(action, para, Para2, FireAsync, pFireEventTimeout);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T1">Type of the first callback parameter.</typeparam>
        /// <typeparam name="T2">Type of the second callback parameter.</typeparam>
        /// <param name="action">Callback to be fired by the Event. Both parameters are generic</param>
        /// <param name="para">First parameter given to the callback.</param>
        /// <param name="Para2">Second parameter given to the callback.</param>
        /// <param name="FireAsync">if true, the event is fired asynch</param>
        /// <param name="pFireEventTimeout">if larger </param>
        public static void DoFireEvent<T1, T2>(Action<T1, T2> action, T1 para, T2 Para2, bool FireAsync, int pFireEventTimeout = 0)
        {
            if (action == null) return;
            if (FireAsync)
            {
                if (pFireEventTimeout == 0)
                    pFireEventTimeout = TheBaseAssets.MyServiceHostInfo.EventTimeout; //3.2 was hardcoded 60000
                if (pFireEventTimeout < 0)
                {
                    TheCommonUtils.cdeRunAsync("DFE-T,O No Timeout", true, (o) => action(para, Para2), null);
                }
                else
                {
                    TheCommonUtils.cdeRunAsync("DFE-T,O with Timeout", true, (o) =>
                    {
                        DoFireEventParallelInternal(action, a =>
                        {
                            var innerAction = (a as Action<T1, T2>);
                            if (innerAction == null)
                            {
                                // This should never happen
                            }
                            innerAction?.Invoke(para, Para2);
                        }, pFireEventTimeout);

                    }, null);
                }
            }
            else
            {

                try
                {
                    action(para, Para2);
                }
                catch
                {
                    // Repurposing timeout KPIs as FireAsync is (currently) a global setting
                    TheCDEKPIs.IncrementKPI(eKPINames.EventTimeouts);
                    TheCDEKPIs.IncrementKPI(eKPINames.TotalEventTimeouts);
                }
            }
        }

        /// <summary>
        /// Runs code asynchronously and lets the caller wait for the code to finish
        /// </summary>
        /// <param name="pThreadName">Thread name (to assist in debugging).</param>
        /// <param name="callBack">Code to execute.</param>
        /// <param name="pState">Initializer to pass to callback function.</param>
        /// <param name="longRunning">Hint for scheduler about nature of task.</param>
        /// <returns>A reference to the task that was created.</returns>
        /// <seealso cref="cdeRunAsync(string,bool,cdeWaitCallback,object)"/>
        public static Task cdeRunTaskAsync(string pThreadName, cdeWaitCallback callBack, object pState = null, bool longRunning = false)
        {
            PendingTask pendingTask = null;
            if (TheBaseAssets.MyServiceHostInfo?.EnableTaskKPIs == true)
            {
                pendingTask = new PendingTask { createTime = DateTime.UtcNow };
            }
            var task = Task.Factory.StartNew((o) =>
            {
                if (pendingTask != null)
                {
                    pendingTask.startTime = DateTime.UtcNow;
                }

                callBack?.Invoke(pState);

                if (pendingTask != null)
                {
                    pendingTask.endTime = DateTime.UtcNow;
                }
            }, pThreadName, defaultTaskCreationOptions | (longRunning ? TaskCreationOptions.LongRunning : 0));

#if CDE_NET4 || CDE_NET35
            // On Net4 and earlier a task with an unobserved exception takes down the process during garbage collection/finalization of the task object
            // This continuation observes the exception and prevents that.
            // On Net45 and newer, such unobserved task exception are ignored.
            task.ContinueWith(c => { var ignored = c.Exception; }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
#endif

            if (pendingTask != null)
            {
                pendingTask.task = task;
                _globalTasks.Add(pendingTask);
            }
            return task;
        }
        #endregion
    }

    /// <summary>
    /// New in V4: Allows to set WebSocket Ports and moniker for a given URI
    /// </summary>
    public static class UriExtensions
    {
        /// <summary>
        /// Changes the WebSockets information to an URI
        /// </summary>
        /// <param name="uri">this Uri</param>
        /// <param name="newPort">new WebSocket Port</param>
        /// <param name="pPath">Path for the uri</param>
        /// <returns></returns>
        public static Uri SetWSInfo(this Uri uri, int newPort, string pPath)
        {
#if CDE_NET35
            if (newPort == -1)
            {
                if (uri.Scheme == "wss")
                {
                    newPort = 443;
                }
            }
#endif
            var builder = new UriBuilder(uri + pPath)
            {
                Port = newPort
            };
            if (newPort == 443 || builder.Scheme == "https" || builder.Scheme == "wss")
                builder.Scheme = "wss";
            else
                builder.Scheme = "ws";
            return builder.Uri;
        }

        /// <summary>
        /// return true if this uri has TLS enabled on the given port
        /// </summary>
        /// <param name="uri">this URI</param>
        /// <param name="newPort">Port to probe for</param>
        /// <returns></returns>
        public static bool IsUsingTLS(this Uri uri, int newPort)
        {
            var builder = new UriBuilder(uri)
            {
                Port = newPort
            };
            return (newPort == 443 || builder.Scheme == "https" || builder.Scheme == "wss");
        }

        /// <summary>
        /// Sets a new http port on an uri
        /// </summary>
        /// <param name="uri">this uri</param>
        /// <param name="newPort">new http port</param>
        /// <param name="pPath">path for the uri</param>
        /// <returns></returns>
        public static Uri SetHTTPInfo(this Uri uri, int newPort, string pPath)
        {
            var builder = new UriBuilder(uri + pPath)
            {
                Port = newPort
            };
            if (newPort == 443)
                builder.Scheme = "https";
            else 
                builder.Scheme = "http";
            return builder.Uri;
        }
    }
}
