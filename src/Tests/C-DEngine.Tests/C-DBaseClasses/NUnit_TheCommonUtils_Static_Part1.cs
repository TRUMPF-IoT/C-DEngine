// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

//extern alias MyNUnit;
//using MyNUnit.NUnit.Framework;
using NUnit.Framework;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using NUnitTestForHelp;
using static nsCDEngine.BaseClasses.TheCommonUtils;

namespace NUnit_TheCommonUtils
{
    public class UnitTestTheCommonUtils
    {
        // Not able to start C-DEngine, so leave it out for now.
        //
        // Actually only needed for one current test:
        //    public void T1052_IsHostADevice()
        //
        //[OneTimeSetUp]
        //public void OneTimeSetUp_LoadCDEngine()
        //{
        //    string strGUID = "{EB5AFC14-9ED9-4E90-82F4-FD707F04CB22}";
        //    string strScopeID = "JAKE1021";
        //    bool bInit = BaseApplication.Init(strGUID, strScopeID);
        //    bool bStart = BaseApplication.Start();

        //    Assert.IsTrue(bInit);
        //    Assert.IsTrue(bStart);
        //}

        //[OneTimeTearDown]
        //public void OneTimeTearDown_LoadCDEngine()
        //{
        //    BaseApplication.Stop();
        //}

        [Test]
        public void T1000_CArray2UTF8String()
        {
            // Auto-Generated Test Function
            // System.String CArray2UTF8String(Byte[])

            // <HelpStart fn="TheCommonUtils.CArray2UTF8String_byte[]" />
            // Convert the byte array into a .NET string.
            string strExpected = "123456789";

            byte[] pIn = new byte[] { 49, 50, 51, 52, 53, 54, 55, 56, 57 }; ;
            string strReturned = TheCommonUtils.CArray2UTF8String(pIn);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.CArray2UTF8String_byte[]" />

        } // function T1000_CArray2UTF8String

        [Test]
        public void T1001_CArray2UTF8String()
        {
            // Auto-Generated Test Function
            // System.String CArray2UTF8String(Byte[], Boolean)

            // <HelpStart fn="TheCommonUtils.CArray2UTF8String_byte[]_bool" />
            // Strip file codes (like the first three bytes, bel
            string strExpected = "2468";

            byte[] pIn = new Byte[] { 0xEF, 0xBB, 0xBF, 50, 52, 54, 56 };
            bool StripFileCodes = true;
            string strReturned = TheCommonUtils.CArray2UTF8String(pIn, StripFileCodes);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.CArray2UTF8String_byte[]_bool" />

        } // function T1001_CArray2UTF8String

        [Test]
        public void T1002_CArray2UTF8String()
        {
            // Auto-Generated Test Function
            // System.String CArray2UTF8String(Byte[], Int32, Int32)

            // <HelpStart fn="TheCommonUtils.CArray2UTF8String_byte[]_int_int" />
            string strExpected = "01357";


            byte[] pIn = new Byte[] { 48, 48, 48, 48, 48, 48, 48, 49, 51, 53, 55, 48, 48, 48, 48, 48 };
            int start = 6;
            int len = 5;
            string strReturned = TheCommonUtils.CArray2UTF8String(pIn, start, len);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.CArray2UTF8String_byte[]_int_int" />

        } // function T1002_CArray2UTF8String

        [Test]
        public void T1003_CArray2UTF8String()
        {
            // Auto-Generated Test Function
            // System.String CArray2UTF8String(Byte[], Int32, Int32, Boolean)

            // <HelpStart fn="TheCommonUtils.CArray2UTF8String_byte[]_int_int_bool" />
            string strExpected = "357000000";


            byte[] pIn = new Byte[] { 51, 53, 55, 48, 48, 48, 48, 48, 48 };
            int start = 0;
            int len = 9;
            bool StripFileCodes = true;
            string strReturned = TheCommonUtils.CArray2UTF8String(pIn, start, len, StripFileCodes);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.CArray2UTF8String_byte[]_int_int_bool" />

        } // function T1003_CArray2UTF8String

        [Test]
        public void T1004_CArray2UnicodeString()
        {
            // Auto-Generated Test Function
            // System.String CArray2UnicodeString(Byte[])

            // <HelpStart fn="TheCommonUtils.CArray2UnicodeString_byte[]" />
            string strExpected = "007";


            byte[] pIn = new Byte[] { 48, 00, 48, 00, 55, 00 };
            string strReturned = TheCommonUtils.CArray2UnicodeString(pIn);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.CArray2UnicodeString_byte[]" />

        } // function T1004_CArray2UnicodeString

        [Test]
        public void T1005_CArray2UnicodeString()
        {
            // Auto-Generated Test Function
            // System.String CArray2UnicodeString(Byte[], Int32, Int32)

            // <HelpStart fn="TheCommonUtils.CArray2UnicodeString_byte[]_int_int" />
            string strExpected = "<FPZ";


            byte[] pIn = new Byte[] { 60, 00, 70, 00, 80, 00, 90, 00 };
            int start = 0;
            int len = 8;
            string strReturned = TheCommonUtils.CArray2UnicodeString(pIn, start, len);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.CArray2UnicodeString_byte[]_int_int" />

        } // function T1005_CArray2UnicodeString

        [Test]
        public void T1006_CBool()
        {
            // Auto-Generated Test Function
            // Boolean CBool(System.Object)

            // <HelpStart fn="TheCommonUtils.CBool_object" />
            // Convert the input value to a boolean.
            bool bExpected = false;

            object inObj = (object)false;
            bool bReturned = TheCommonUtils.CBool(inObj);

            Assert.AreEqual(bExpected, bReturned);
            // <HelpEnd fn="TheCommonUtils.CBool_object" />

        } // function T1006_CBool

        [Test]
        public void T1007_CByte()
        {
            // Auto-Generated Test Function
            // Byte CByte(System.Object)

            // <HelpStart fn="TheCommonUtils.CByte_object" />
            byte Returned = 123;


            object inObj = 123;
            byte Expected = TheCommonUtils.CByte(inObj);

            Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.CByte_object" />

        } // function T1007_CByte

        [Test]
        public void T1008_CChar()
        {
            // Auto-Generated Test Function
            // Char CChar(System.Object)

            // <HelpStart fn="TheCommonUtils.CChar_object" />
            char Returned = 'Z';


            object inObj = 90;
            char Expected = TheCommonUtils.CChar(inObj);

            Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.CChar_object" />

        } // function T1008_CChar

        [Test]
        public void T1009_CDate()
        {
            // Auto-Generated Test Function
            // System.DateTimeOffset CDate(System.String)

            // <HelpStart fn="TheCommonUtils.CDate_object" />
            DateTimeOffset Returned = new DateTimeOffset(new DateTime(2012, 5, 1));


            object inObj = "5/1/2012";
            DateTimeOffset Expected = TheCommonUtils.CDate(inObj);

            Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.CDate_object" />

        } // function T1009_CDate

        [Test]
        public void T1010_CDate()
        {
            // Auto-Generated Test Function
            // System.DateTimeOffset CDate(System.Object)

            // <HelpStart fn="TheCommonUtils.CDate_object" />
            DateTimeOffset Returned = new DateTimeOffset(new DateTime(1900, 1, 1));


            object inObj = "1/1/1900";
            DateTimeOffset Expected = TheCommonUtils.CDate(inObj);

            Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.CDate_object" />

        } // function T1010_CDate


        [Test]
        public void T1012_CDbl()
        {
            // Auto-Generated Test Function
            // Double CDbl(System.Object)

            // <HelpStart fn="TheCommonUtils.CDbl_object" />
            double Expected = 9876.5432;


            object inObj = (9876.0000 + 0.5432);
            double Returned = TheCommonUtils.CDbl(inObj);

            Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.CDbl_object" />

        } // function T1012_CDbl

        [Test]
        public void T1013_CDblWithBool()
        {
            // Auto-Generated Test Function
            // Double CDblWithBool(System.Object)

            // <HelpStart fn="TheCommonUtils.CDblWithBool_object" />
            double Expected = 1;


            object inObj = (object)(bool)true;
            double Returned = TheCommonUtils.CDblWithBool(inObj);

            Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.CDblWithBool_object" />

        } // function T1013_CDblWithBool

        [Test]
        public void T1014_CFloat()
        {
            // Auto-Generated Test Function
            // Single CFloat(System.Object)

            // <HelpStart fn="TheCommonUtils.CFloat_object" />
            float Expected = 3.141592654f;

            object inObj = "3.141592654";
            float Returned = TheCommonUtils.CFloat(inObj);

            Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.CFloat_object" />

        } // function T1014_CFloat

        [Test]
        public void T1015_CGuid()
        {
            // Auto-Generated Test Function
            // System.Guid CGuid(System.Object)

            // <HelpStart fn="TheCommonUtils.CGuid_object" />
            Guid Expected = new Guid(0x1BDDE03F, 0x7BAE, 0x4CA4, 0x9A, 0x00, 0xC8, 0x09, 0xB0, 0x2E, 0x41, 0xC9);

            object inObj = "{1BDDE03F-7BAE-4CA4-9A00-C809B02E41C9}";
            Guid Returned = TheCommonUtils.CGuid(inObj);

            Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.CGuid_object" />

        } // function T1015_CGuid

        [Test]
        public void T1016_CInt()
        {
            // Auto-Generated Test Function
            // Int32 CInt(System.Object)

            // <HelpStart fn="TheCommonUtils.CInt_object" />
            int iExpected = 123456789;


            object inObj = "123456789";
            int iReturned = TheCommonUtils.CInt(inObj);

            Assert.AreEqual(iExpected, iReturned);
            // <HelpEnd fn="TheCommonUtils.CInt_object" />

        } // function T1016_CInt




        [Test]
        public void T1017_CJSONDateToDateTime()
        {
            // Auto-Generated Test Function
            // System.DateTimeOffset CJSONDateToDateTime(System.String)

            // <HelpStart fn="TheCommonUtils.CJSONDateToDateTime_string" />
            DateTime dateInput1 = new DateTime(2019, 5, 15, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTimeOffset dateInput2 = new DateTimeOffset(dateInput1);

            string strJSONDate = CDateTimeToJSONDate2(dateInput2);

            DateTimeOffset Returned = CJSONDateToDateTime(strJSONDate);

            Assert.AreEqual(dateInput1.Hour, Returned.Hour);
            Assert.AreEqual(dateInput1.Minute, Returned.Minute);
            Assert.AreEqual(dateInput1.Second, Returned.Second);
            Assert.AreEqual(dateInput1.Millisecond, Returned.Millisecond);
            Assert.AreEqual(dateInput1.Ticks, Returned.Ticks);
            Assert.AreEqual(dateInput1.Year, Returned.Year);
            Assert.AreEqual(dateInput1.Month, Returned.Month);
            Assert.AreEqual(dateInput1.Day, Returned.Day);

            Assert.AreEqual(dateInput2.Hour, Returned.Hour);
            Assert.AreEqual(dateInput2.Minute, Returned.Minute);
            Assert.AreEqual(dateInput2.Second, Returned.Second);
            Assert.AreEqual(dateInput2.Millisecond, Returned.Millisecond);
            Assert.AreEqual(dateInput2.Ticks, Returned.Ticks);
            Assert.AreEqual(dateInput2.Year, Returned.Year);
            Assert.AreEqual(dateInput2.Month, Returned.Month);
            Assert.AreEqual(dateInput2.Day, Returned.Day);
            // <HelpEnd fn="TheCommonUtils.CJSONDateToDateTime_string" />

        } // function T1017_CJSONDateToDateTime

        [Test]
        public void T1017_T1011_CDateTimeToJSONDate_CJSONDateToDateTime()
        {
            // Step 1: Convert .NET date to JSON date.
            DateTime date = new DateTime(2019, 5, 15, 0, 0, 0, 0, DateTimeKind.Utc);
            // DateTimeOffset dateInput = new DateTimeOffset(date);

            string strJSONDate = TheCommonUtils.CDateTimeToJSONDate(date);
            string strJSONDate2 = CDateTimeToJSONDate2(date);

            // Step 2: Convert JSON date to .NET date.
            // DateTimeOffset dateOutput = TheCommonUtils.CJSONDateToDateTime(strJSONDate);
            DateTimeOffset dateOutput2 = CJSONDateToDateTime(strJSONDate2);

            //Assert.AreEqual(date.Hour, dateOutput1.Hour);
            //Assert.AreEqual(date.Minute, dateOutput1.Minute);
            //Assert.AreEqual(date.Second, dateOutput1.Second);
            //Assert.AreEqual(date.Millisecond, dateOutput1.Millisecond);
            //Assert.AreEqual(date.Ticks, dateOutput1.Ticks);
            //Assert.AreEqual(date.Year, dateOutput1.Year);
            //Assert.AreEqual(date.Month, dateOutput1.Month);
            //Assert.AreEqual(date.Day, dateOutput1.Day);

            Assert.AreEqual(date.Hour, dateOutput2.Hour);
            Assert.AreEqual(date.Minute, dateOutput2.Minute);
            Assert.AreEqual(date.Second, dateOutput2.Second);
            Assert.AreEqual(date.Millisecond, dateOutput2.Millisecond);
            Assert.AreEqual(date.Ticks, dateOutput2.Ticks);
            Assert.AreEqual(date.Year, dateOutput2.Year);
            Assert.AreEqual(date.Month, dateOutput2.Month);
            Assert.AreEqual(date.Day, dateOutput2.Day);

            //Assert.AreEqual(dateInput.Hour, dateOutput1.Hour);
            //Assert.AreEqual(dateInput.Minute, dateOutput1.Minute);
            //Assert.AreEqual(dateInput.Second, dateOutput1.Second);
            //Assert.AreEqual(dateInput.Millisecond, dateOutput1.Millisecond);
            //Assert.AreEqual(dateInput.Ticks, dateOutput1.Ticks);
            //Assert.AreEqual(dateInput.Year, dateOutput1.Year);
            //Assert.AreEqual(dateInput.Month, dateOutput1.Month);
            //Assert.AreEqual(dateInput.Day, dateOutput1.Day);

        } // function T1011_CDateTimeToJSONDate


        public static DateTimeOffset CJSONDateToDateTime(string jsonTime)
        {
            if (!string.IsNullOrEmpty(jsonTime) && jsonTime.IndexOf("Date") > -1)
            {
                string milis = jsonTime.Substring(jsonTime.IndexOf("(") + 1);
                TimeSpan offset = DateTimeOffset.Now.Offset; // Default to current time zone
                var offsetIndex = milis.IndexOfAny("+-".ToCharArray());
                if (offsetIndex > 0)
                {
                    string strHours = milis.Substring(offsetIndex + 1, 2);
                    int iHours = TheCommonUtils.CInt(strHours);
                    offset = new TimeSpan(iHours, 0, 0);
                    milis = milis.Substring(0, offsetIndex);
                }
                else
                {
                    milis = milis.Substring(0, milis.Length - 1);
                }
                var origin = new DateTimeOffset(1970, 1, 1, 0, 0, 0, offset);
                return origin.AddMilliseconds(TheCommonUtils.CLng(milis));
            }
            return DateTimeOffset.MinValue;
        }

        public static string CDateTimeToJSONDate2(DateTimeOffset tDate)
        {
            long ticks = tDate.Subtract(iEpochTime).Ticks / 10000; //Epoch no need for Offset
            return string.Format("Date({0})", ticks);
        }
        private static DateTime iEpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);


        [Test]
        public void T1018_CLeft()
        {
            // Auto-Generated Test Function
            // System.String CLeft(System.String, Int32)

            // <HelpStart fn="TheCommonUtils.CLeft_string_int" />
            string strExpected = "Hello C-DEngine";


            string inStr = "Hello C-DEngine";
            int len = 789;
            string strReturned = TheCommonUtils.CLeft(inStr, len);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.CLeft_string_int" />

        } // function T1018_CLeft

        [Test]
        public void T1019_CLng()
        {
            // Auto-Generated Test Function
            // Int64 CLng(System.Object)

            // <HelpStart fn="TheCommonUtils.CLng_object" />
            long lExpected = 123456789000;


            object inObj = "123456789000";
            long lReturned = TheCommonUtils.CLng(inObj);

            Assert.AreEqual(lExpected, lReturned);
            // <HelpEnd fn="TheCommonUtils.CLng_object" />

        } // function T1019_CLng

        [Test]
        public void T1020_CShort()
        {
            // Auto-Generated Test Function
            // Int16 CShort(System.Object)

            // <HelpStart fn="TheCommonUtils.CShort_object" />
            short iExpected = 4096;


            object inObj = "4096";
            short iReturned = TheCommonUtils.CShort(inObj);

            Assert.AreEqual(iExpected, iReturned);
            // <HelpEnd fn="TheCommonUtils.CShort_object" />

        } // function T1020_CShort

        [Test]
        public void T1021_CStr()
        {
            // Auto-Generated Test Function
            // System.String CStr(System.Object)

            // <HelpStart fn="TheCommonUtils.CStr_object" />
            string strExpected = "Hello C-DEngine";


            object inObj = "Hello" + " " + "C-DEngine";
            string strReturned = TheCommonUtils.CStr(inObj);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.CStr_object" />

        } // function T1021_CStr

        [Test]
        public void T1022_CStringPosInStringList()
        {
            // Auto-Generated Test Function
            // Int32 CStringPosInStringList(System.String, System.String, Char)

            // <HelpStart fn="TheCommonUtils.CStringPosInStringList_string_string_char" />
            int iExpected = 3;

            //  0  1  2  3  4  5  6  7  8  9
            string pInStr = "aa,bb,cc,dd,ee,ff,gg,hh,ii,jj";
            string pSearchStr = "dd";
            char sep = ',';
            int iReturned = TheCommonUtils.CStringPosInStringList(pInStr, pSearchStr, sep);

            Assert.AreEqual(iExpected, iReturned);
            // <HelpEnd fn="TheCommonUtils.CStringPosInStringList_string_string_char" />

        } // function T1022_CStringPosInStringList

        [Test]
        public void T1023_CStringToByteArray()
        {
            // Auto-Generated Test Function
            // Byte[] CStringToByteArray(System.String)

            // <HelpStart fn="TheCommonUtils.CStringToByteArray_string" />
            byte[] abExpected = new Byte[] { 1, 2, 3, 4, 5, 6 };


            string hex = "010203040506";
            byte[] abReturned = TheCommonUtils.CStringToByteArray(hex);

            CollectionAssert.AreEqual(abExpected, abReturned);
            // <HelpEnd fn="TheCommonUtils.CStringToByteArray_string" />

        } // function T1023_CStringToByteArray

        [Test]
        public void T1024_CStringToList()
        {
            // Auto-Generated Test Function
            // System.Collections.Generic.List`1[System.String] CStringToList(System.String, Char)

            // <HelpStart fn="TheCommonUtils.CStringToList_string_char" />
            List<string> strlistExpected = new List<string>() { "one", "two", "three" };


            string pInStr = "one,two,three";
            char sep = ',';
            List<string> strlistReturned = TheCommonUtils.CStringToList(pInStr, sep);

            CollectionAssert.AreEqual(strlistExpected, strlistReturned);
            // <HelpEnd fn="TheCommonUtils.CStringToList_string_char" />

        } // function T1024_CStringToList

        [Test]
        public void T1025_CStringToMemoryStream()
        {
            // Auto-Generated Test Function
            // System.IO.MemoryStream CStringToMemoryStream(System.String)

            // <HelpStart fn="TheCommonUtils.CStringToMemoryStream_string" />
            MemoryStream memstrExpected = new MemoryStream();
            memstrExpected.Write(new byte[] { 48, 0, 49, 00, 50, 00 }, 0, 6);

            string strInput = "123";
            MemoryStream memstrReturned = TheCommonUtils.CStringToMemoryStream(strInput);

            byte[] abExpected = new byte[6];
            int c1Read = memstrExpected.Read(abExpected, 0, 6);

            byte[] abReturned = new byte[6];
            int c2Read = memstrExpected.Read(abReturned, 0, 6);

            Assert.AreEqual(c1Read, c2Read);
            CollectionAssert.AreEqual(abExpected, abReturned);

            // <HelpEnd fn="TheCommonUtils.CStringToMemoryStream_string" />

        } // function T1025_CStringToMemoryStream

        [Test]
        public void T1026_CUInt()
        {
            // Auto-Generated Test Function
            // UInt32 CUInt(System.Object)

            // <HelpStart fn="TheCommonUtils.CUInt_object" />
            UInt32 uiExpected = 1024;

            object inObj = (object)1024;
            UInt32 uiReturned = TheCommonUtils.CUInt(inObj);

            Assert.AreEqual(uiExpected, uiReturned);
            // <HelpEnd fn="TheCommonUtils.CUInt_object" />

        } // function T1026_CUInt

        [Test]
        public void T1027_CULng()
        {
            // Auto-Generated Test Function
            // UInt64 CULng(System.Object)

            // <HelpStart fn="TheCommonUtils.CULng_object" />
            UInt64 uiExpected = 0xffffff;

            object inObj = (object)16777215;
            UInt64 uiReturned = TheCommonUtils.CULng(inObj);

            Assert.AreEqual(uiExpected, uiReturned);
            // <HelpEnd fn="TheCommonUtils.CULng_object" />

        } // function T1027_CULng

        [Test]
        public void T1028_CUShort()
        {
            // Auto-Generated Test Function
            // UInt16 CUShort(System.Object)

            // <HelpStart fn="TheCommonUtils.CUShort_object" />
            UInt16 uiExpected = 31415;


            object inObj = "31415";
            UInt16 uiReturned = TheCommonUtils.CUShort(inObj);

            Assert.AreEqual(uiExpected, uiReturned);
            // <HelpEnd fn="TheCommonUtils.CUShort_object" />

        } // function T1028_CUShort

        [Test]
        public void T1029_CUTF8String2Array()
        {
            // Auto-Generated Test Function
            // Byte[] CUTF8String2Array(System.String)

            // Test #1 - Help snippet
            // <HelpStart fn="TheCommonUtils.CUTF8String2Array_string" />
            byte[] abExpected = new Byte[] { 77, 195, 188, 110, 99, 104, 101, 110 };

            string OrginalString = "München";
            byte[] abReturned = TheCommonUtils.CUTF8String2Array(OrginalString);

            CollectionAssert.AreEqual(abExpected, abReturned);
            // <HelpEnd fn="TheCommonUtils.CUTF8String2Array_string" />

            // Test #2 - Another test
            abExpected = new Byte[] { 83, 101, 97, 116, 116, 108, 101 };

            OrginalString = "Seattle";
            abReturned = TheCommonUtils.CUTF8String2Array(OrginalString);

            CollectionAssert.AreEqual(abExpected, abReturned);

        } // function T1029_CUTF8String2Array

        [Test]
        public void T1030_CUnicodeString2Array()
        {
            // Auto-Generated Test Function
            // Byte[] CUnicodeString2Array(System.String)

            // <HelpStart fn="TheCommonUtils.CUnicodeString2Array_string" />
            byte[] abExpected = new Byte[] { 83, 0, 101, 0, 97, 0, 116, 0, 116, 0, 108, 0, 101, 0 };

            string OrginalString = "Seattle";
            byte[] abReturned = TheCommonUtils.CUnicodeString2Array(OrginalString);

            CollectionAssert.AreEqual(abExpected, abReturned);
            // <HelpEnd fn="TheCommonUtils.CUnicodeString2Array_string" />

        } // function T1030_CUnicodeString2Array

        [Test]
        public void T1031_CUri()
        {
            // Auto-Generated Test Function
            // System.Uri CUri(System.Object, Boolean)

            // <HelpStart fn="TheCommonUtils.CUri_object_bool" />
            Uri uriExpected = null;

            object inObj = 123;
            bool IgnoreGuids = true;
            Uri uriReturned = TheCommonUtils.CUri(inObj, IgnoreGuids);

            Assert.AreEqual(uriExpected, uriReturned);
            // <HelpEnd fn="TheCommonUtils.CUri_object_bool" />

        } // function T1031_CUri

        [Test]
        public void T1032_DoUrlsContainAnyUrl()
        {
            // Auto-Generated Test Function
            // Boolean DoUrlsContainAnyUrl(System.String, System.String)

            // <HelpStart fn="TheCommonUtils.DoUrlsContainAnyUrl_string_string" />
            bool bExpected = true;

            string sGuidList = "Hello C-DEngine";
            string pGuid = "Hello C-DEngine";
            bool bReturned = TheCommonUtils.DoUrlsContainAnyUrl(sGuidList, pGuid);

            Assert.AreEqual(bExpected, bReturned);
            // <HelpEnd fn="TheCommonUtils.DoUrlsContainAnyUrl_string_string" />

        } // function T1032_DoUrlsContainAnyUrl



        [Test]
        public void T1035_GetCalendarweek()
        {
            // Auto-Generated Test Function
            // Int32 GetCalendarweek(System.DateTime)

            // <HelpStart fn="TheCommonUtils.GetCalendarweek_DateTime" />
            // Report on the week number for a given date, given the rules
            // that a week starts on Monday and we are counting full weeks.

            // December 25, 2016 occurs in the 52nd week
            int iExpected = 52;
            DateTime datetime = new DateTime(2016, 12, 31);
            int dowExpected = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(datetime, System.Globalization.CalendarWeekRule.FirstFullWeek, DayOfWeek.Monday);

            int dowReturned = TheCommonUtils.GetCalendarweek(datetime);

            Assert.AreEqual(dowExpected, dowReturned);
            Assert.AreEqual(iExpected, dowReturned);

            // January 1, 2016 also occurs in the 52nd week!
            iExpected = 52;
            datetime = new DateTime(2016, 1, 1);
            dowExpected = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(datetime, System.Globalization.CalendarWeekRule.FirstFullWeek, DayOfWeek.Monday);

            dowReturned = TheCommonUtils.GetCalendarweek(datetime);

            Assert.AreEqual(dowExpected, dowReturned);
            Assert.AreEqual(iExpected, dowReturned);
            // <HelpEnd fn="TheCommonUtils.GetCalendarweek_DateTime" />

        } // function T1035_GetCalendarweek

        [Test]
        public void T1036_GetCmdArgValue()
        {
            // Auto-Generated Test Function
            // System.String GetCmdArgValue(System.String)

            // <HelpStart fn="TheCommonUtils.GetCmdArgValue_string" />
            string strExpected = null;

            string pKey = "P";
#pragma warning disable CS0618 // Type or member is obsolete
            string strReturned = TheCommonUtils.GetCmdArgValue(pKey);
#pragma warning restore CS0618 // Type or member is obsolete

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.GetCmdArgValue_string" />

        } // function T1036_GetCmdArgValue




        [Test]
        public void T1039_GetFirstURL()
        {
            // Auto-Generated Test Function
            // System.Guid GetFirstURL(System.String)

            // <HelpStart fn="TheCommonUtils.GetFirstURL_string" />
            // Parse the message route and provide GUID for first node.
            Guid Expected = Guid.Empty;

            string pURLs = "Guid.NewGuid().ToString()";
            Guid Returned = TheCommonUtils.GetFirstURL(pURLs);

            Assert.AreEqual(Returned, Expected);
            // <HelpEnd fn="TheCommonUtils.GetFirstURL_string" />

        } // function T1039_GetFirstURL

        [Test]
        public void T1040_GetLastURL()
        {
            // Auto-Generated Test Function
            // System.Guid GetLastURL(System.String)

            // <HelpStart fn="TheCommonUtils.GetLastURL_string" />
            Guid Expected = Guid.Empty;

            string pURLs = "Hello C-DEngine";
            Guid Returned = TheCommonUtils.GetLastURL(pURLs);

            Assert.AreEqual(Returned, Expected);
            // <HelpEnd fn="TheCommonUtils.GetLastURL_string" />

        } // function T1040_GetLastURL

        [Test]
        public void T1041_GetMaxMessageSize()
        {
            // Auto-Generated Test Function
            // Int32 GetMaxMessageSize(nsCDEngine.ViewModels.cdeSenderType)

            // <HelpStart fn="TheCommonUtils.GetMaxMessageSize_cdeSenderType" />
            // Maximum message size for different sender node types.
            int iExpected = 400000;

            cdeSenderType pType = cdeSenderType.CDE_SERVICE;
            int iReturned = TheCommonUtils.GetMaxMessageSize(pType);

            Assert.AreEqual(iExpected, iReturned);
            // <HelpEnd fn="TheCommonUtils.GetMaxMessageSize_cdeSenderType" />

        } // function T1041_GetMaxMessageSize

        [Test]
        public void T1042_GetMimeTypeFromExtension()
        {
            // Auto-Generated Test Function
            // System.String GetMimeTypeFromExtension(System.String)

            // <HelpStart fn="TheCommonUtils.GetMimeTypeFromExtension_string" />
            string strExpected = "application/octet-stream";

            string extension = "Hello C-DEngine";
            string strReturned = TheCommonUtils.GetMimeTypeFromExtension(extension);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.GetMimeTypeFromExtension_string" />

        } // function T1042_GetMimeTypeFromExtension

        [Test]
        public void T1043_GetNodeById()
        {
            // Auto-Generated Test Function
            // System.Guid GetNodeById(System.String, Int32)

            // <HelpStart fn="TheCommonUtils.GetNodeById_string_int" />
            Guid Expected = Guid.Empty;

            string pURLs = "Hello C-DEngine";
            int pIdx = 789;
            Guid Returned = TheCommonUtils.GetNodeById(pURLs, pIdx);

            Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.GetNodeById_string_int" />

        } // function T1043_GetNodeById

        [Test]
        public void T1044_GetPropValue()
        {
            // Auto-Generated Test Function
            // System.Object GetPropValue(System.Object, System.String)

            // <HelpStart fn="TheCommonUtils.GetPropValue_object_string" />
            object Returned = 123;

            object src = 123;
            string propName = "Hello C-DEngine";
            object Expected = TheCommonUtils.GetPropValue(src, propName);

            Assert.AreNotEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.GetPropValue_object_string" />

        } // function T1044_GetPropValue

        [Test]
        public void T1045_GetRandomDouble()
        {
            // Auto-Generated Test Function
            // Double GetRandomDouble()

            // <HelpStart fn="TheCommonUtils.GetRandomDouble" />
            double Returned = 9876.5432;

            double Expected = TheCommonUtils.GetRandomDouble();

            Assert.AreNotEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.GetRandomDouble" />

        } // function T1045_GetRandomDouble

        [Test]
        public void T1046_GetRandomUInt()
        {
            // Auto-Generated Test Function
            // UInt32 GetRandomUInt(UInt32, UInt32)

            // <HelpStart fn="TheCommonUtils.GetRandomUInt_UInt32_UInt32" />
            UInt32 uiExpected = 32;

            UInt32 pMin = 32;
            UInt32 pMax = 33;
            UInt32 uiReturned = TheCommonUtils.GetRandomUInt(pMin, pMax);

            Assert.AreEqual(uiExpected, uiReturned);
            // <HelpEnd fn="TheCommonUtils.GetRandomUInt_UInt32_UInt32" />

        } // function T1046_GetRandomUInt

        //[Test]
        //public void T1047_GetSenderTypeFromGuid()
        //{
        //    // Auto-Generated Test Function
        //   // nsCDEngine.ViewModels.cdeSenderType GetSenderTypeFromGuid(System.Guid)

        //    // <HelpStart fn="TheCommonUtils.GetSenderTypeFromGuid_Guid" />
        //    cdeSenderType Returned = cdeSenderType.CDE_SERVICE;

        //    Guid pSenderGuid = Guid.NewGuid();
        //    cdeSenderType Expected = TheCommonUtils.T1047_GetSenderTypeFromGuid(pSenderGuid);

        //    Assert.AreEqual(Expected, Returned);
        //   //HelpEnd fn="TheCommonUtils.GetSenderTypeFromGuid_Guid" ;

        //} // function T1047_GetSenderTypeFromGuid

        [Test]
        public void T1048_GetStackInfo()
        {
            // Auto-Generated Test Function
            // System.String GetStackInfo(System.Object)

            // <HelpStart fn="TheCommonUtils.GetStackInfo_object" />
            string strExpected = "Stack Trace as HTML";

            object pStack = new System.Diagnostics.StackTrace(true);
            string strReturned = TheCommonUtils.GetStackInfo(pStack);

            Assert.AreNotEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.GetStackInfo_object" />

        } // function T1048_GetStackInfo

        [Test]
        public void T1049_GetTimeStamp()
        {
            // Auto-Generated Test Function
            // System.String GetTimeStamp()

            // <HelpStart fn="TheCommonUtils.GetTimeStamp" />
            DateTime dt = DateTime.Now;
            string strExpected = String.Format("{0:0000}{1:00}{2:00}{3:00}{4:00}{5:00}{6:000}", dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond);
            // string strExpected = String.Format("{0:0000}{1:00}{2:00}{3:00}{4:00}{5:000}", dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Millisecond);

            string strReturned = TheCommonUtils.GetTimeStamp();

            // Return value is 17 characters long
            // Trim to 15 characters to avoid timing discrepencies.
            strExpected = strExpected.Substring(0, 15);
            strReturned = strReturned.Substring(0, 15);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.GetTimeStamp" />

        } // function T1049_GetTimeStamp


        [Test]
        public void T1051_IsGuid()
        {
            // Auto-Generated Test Function
            // Boolean IsGuid(System.Object)

            // <HelpStart fn="TheCommonUtils.IsGuid_object" />
            // Two calls - first with a GUID, second with no GUID
            bool bExpected = true;

            object inObj = Guid.NewGuid().ToString();
            bool bReturned = TheCommonUtils.IsGuid(inObj);

            Assert.AreEqual(bExpected, bReturned);

            // Call without a valid GUID
            bExpected = false;

            inObj = "123456-1234-1234-1234-1234";
            bReturned = TheCommonUtils.IsGuid(inObj);

            Assert.AreEqual(bExpected, bReturned);
            // <HelpEnd fn="TheCommonUtils.IsGuid_object" />

        } // function T1051_IsGuid

        // This requires startup up the C-DEngine
        //[Test]
        //public void T1052_IsHostADevice()
        //{
        //    // Auto-Generated Test Function
        //    // Boolean IsHostADevice()

        //    // <HelpStart fn="TheCommonUtils.IsHostADevice" />
        //    bool bExpected = false;

        //    bool bReturned = TheCommonUtils.IsHostADevice();

        //    Assert.AreEqual(bExpected, bReturned);
        //    // <HelpEnd fn="TheCommonUtils.IsHostADevice" />

        //} // function T1052_IsHostADevice



        [Test]
        public void T1054_IsMobileDevice()
        {
            // Auto-Generated Test Function
            // Boolean IsMobileDevice(System.String)

            // <HelpStart fn="TheCommonUtils.IsMobileDevice_string" />
            bool bExpected = false;

            string userAgent = "Hello C-DEngine";
            bool bReturned = TheCommonUtils.IsMobileDevice(userAgent);

            Assert.AreEqual(bExpected, bReturned);
            // <HelpEnd fn="TheCommonUtils.IsMobileDevice_string" />

        } // function T1054_IsMobileDevice

#if false  // NullReferenceException -- needs system to initialize?
        [TestMethod]
        public void T1055_IsMono()
        {
            // Auto-Generated Test Function
            // Boolean IsMono()

            // <HelpStart fn="TheCommonUtils.IsMono" />
            bool bExpected = false;

            bool bReturned = TheCommonUtils.IsMono();

            Assert.AreEqual(bExpected, bReturned);
            // <HelpEnd fn="TheCommonUtils.IsMono" />

        } // function T1055_IsMono

        [TestMethod]
        public void T1056_IsMonoRT()
        {
            // Auto-Generated Test Function
            // Boolean IsMonoRT()

            // <HelpStart fn="TheCommonUtils.IsMonoRT" />
            bool bExpected = false;

            bool bReturned = TheCommonUtils.IsMonoRT();

            Assert.AreEqual(bExpected, bReturned);
            // <HelpEnd fn="TheCommonUtils.IsMonoRT" />

        } // function T1056_IsMonoRT
#endif

        [Test]
        public void T1057_IsUrlLocalhost()
        {
            // Auto-Generated Test Function
            // Boolean IsUrlLocalhost(System.String)

            // <HelpStart fn="TheCommonUtils.IsUrlLocalhost_string" />
            bool bExpected = true;

            string url = "LOCALHOST";
            bool bReturned = TheCommonUtils.IsUrlLocalhost(url);

            Assert.AreEqual(bExpected, bReturned);
            // <HelpEnd fn="TheCommonUtils.IsUrlLocalhost_string" />

        } // function T1057_IsUrlLocalhost

        [Test]
        public void T1058_LogSession()
        {
            // Auto-Generated Test Function
            // Void LogSession(System.Guid, System.String, System.String, System.String, System.String, System.String)

            // <HelpStart fn="TheCommonUtils.LogSession_Guid_string_string_string_string_string" />

            Guid pSessionID = Guid.NewGuid();
            string pUrl = "Hello C-DEngine";
            string pBrowser = "Hello C-DEngine";
            string pBrowserDesc = "Hello C-DEngine";
            string pRef = "Hello C-DEngine";
            string pCustomData = "Hello C-DEngine";
            TheCommonUtils.LogSession(pSessionID, pUrl, pBrowser, pBrowserDesc, pRef, pCustomData);

            // No return value, so omit Assert statement.
            // Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.LogSession_Guid_string_string_string_string_string" />

        } // function T1058_LogSession

        // Function made internal.
        //[Test]
        //public void T1059_MOTLockGenerator()
        //{
        //    // Auto-Generated Test Function
        //    // System.String MOTLockGenerator()

        //    // <HelpStart fn="TheCommonUtils.MOTLockGenerator" />
        //    string strExpected = "Hello C-DEngine";

        //    string strReturned = TheCommonUtils.MOTLockGenerator();

        //    Assert.AreEqual(strExpected, strReturned);
        //    // <HelpEnd fn="TheCommonUtils.MOTLockGenerator" />

        //} // function T1059_MOTLockGenerator

        [Test]
        public void T1060_ParseQueryString()
        {
            // Auto-Generated Test Function
            // System.Collections.Generic.Dictionary`2[System.String,System.String] ParseQueryString(System.String)

            // <HelpStart fn="TheCommonUtils.ParseQueryString_string" />
            Dictionary<string, string> Expected = new Dictionary<string, string>()
            {
                {"FIRST", "1" },
                {"SECOND", "2" },
                {"THIRD", "3" }
            };

            string query = "?First=1&Second=2&Third=3";
            Dictionary<string, string> Returned = TheCommonUtils.ParseQueryString(query);

            CollectionAssert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.ParseQueryString_string" />

        } // function T1060_ParseQueryString

        [Test]
        public void T1061_RoundDateToMinuteInterval_Failure_Case()
        {
            DateTime dtNow = new DateTime(2016, 9, 28, 1, 0, 24);
            DateTimeOffset time = new DateTimeOffset(dtNow);
            int minuteInterval = 5;
            RoundingDirection direction = RoundingDirection.RoundDown;
            DateTimeOffset Returned = TheCommonUtils.RoundDateToMinuteInterval(time, minuteInterval, direction);
            Assert.AreEqual(Returned.Second, 0);
        }


        [Test]
        public void T1061_RoundDateToMinuteInterval()
        {
            // Auto-Generated Test Function
            // System.DateTimeOffset RoundDateToMinuteInterval(System.DateTimeOffset, Int32, RoundingDirection)

            // <HelpStart fn="TheCommonUtils.RoundDateToMinuteInterval_DateTimeOffset_int_RoundingDirection" />
            // Round time to the nearest five minutes

            DateTime dtNow = DateTime.Now;
            int year = dtNow.Year;
            int month = dtNow.Month;
            int day = dtNow.Day;
            int hour = dtNow.Hour;
            int minute = dtNow.Minute;
            int second = 0;
            minute = minute / 5;
            minute = minute * 5;
            DateTimeOffset time = new DateTimeOffset(dtNow);
            DateTimeOffset Expected = new DateTimeOffset(year, month, day, hour, minute, second, time.Offset);

            int minuteInterval = 5;
            RoundingDirection direction = RoundingDirection.RoundDown;
            DateTimeOffset Returned = TheCommonUtils.RoundDateToMinuteInterval(time, minuteInterval, direction);

            Assert.AreEqual(Expected.Year, Returned.Year);
            Assert.AreEqual(Expected.Month, Returned.Month);
            Assert.AreEqual(Expected.Day, Returned.Day);
            Assert.AreEqual(Expected.Hour, Returned.Hour);
            Assert.AreEqual(Expected.Minute, Returned.Minute);
            Assert.AreEqual(Expected.Second, Returned.Second);
            Assert.AreEqual(Expected.Millisecond, Returned.Millisecond);
            // <HelpEnd fn="TheCommonUtils.RoundDateToMinuteInterval_DateTimeOffset_int_RoundingDirection" />


        } // function T1061_RoundDateToMinuteInterval


        [Test]
        public void T1063_SleepOneEye()
        {
            // Auto-Generated Test Function
            // Void SleepOneEye(UInt32, UInt32)

            // <HelpStart fn="TheCommonUtils.SleepOneEye_UInt32_UInt32" />

            UInt32 ms = 32;
            UInt32 minPeriod = 32;
            TheCommonUtils.SleepOneEye(ms, minPeriod);

            // No return value, so omit Assert statement.
            // Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.SleepOneEye_UInt32_UInt32" />

        } // function T1063_SleepOneEye



        [Test]
        public void T1065_ToHexByte()
        {
            // Auto-Generated Test Function
            // Byte[] ToHexByte(System.String)

            // <HelpStart fn="TheCommonUtils.ToHexByte_string" />
            // Convert string containing hex numbers
            // into an array of hex bytes.
            byte[] abExpected = new Byte[] { 0xab, 0xcd, 0x12, 0x34 };

            string str = "abcd1234";
            byte[] abReturned = TheCommonUtils.ToHexByte(str);

            CollectionAssert.AreEqual(abExpected, abReturned);
            // <HelpEnd fn="TheCommonUtils.ToHexByte_string" />

        } // function T1065_ToHexByte

        [Test]
        public void T1066_ToHexString()
        {
            // Auto-Generated Test Function
            // System.String ToHexString(Byte[])

            // <HelpStart fn="TheCommonUtils.ToHexString_byte[]" />
            // Convert a byte array into a string with the hex bytes.
            string strExpected = "000102030405";

            byte[] byteValue = new Byte[] { 0, 1, 2, 3, 4, 5 };
            string strReturned = TheCommonUtils.ToHexString(byteValue);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.ToHexString_byte[]" />

        } // function T1066_ToHexString

        [Test]
        public void T1067_TruncTrailingSlash()
        {
            // Auto-Generated Test Function
            // System.String TruncTrailingSlash(System.String)

            // <HelpStart fn="TheCommonUtils.TruncTrailingSlash_string" />
            string strExpected = "First/Second/Third";

            string pUrl = "First/Second/Third/";
            string strReturned = TheCommonUtils.TruncTrailingSlash(pUrl);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.TruncTrailingSlash_string" />

        } // function T1067_TruncTrailingSlash

        [Test]
        public void T1068_cdeBlockCopy()
        {
            // Auto-Generated Test Function
            // Void cdeBlockCopy(Byte[], Int32, Byte[], Int32, Int32)

            // <HelpStart fn="TheCommonUtils.cdeBlockCopy_byte[]_int_byte[]_int_int" />
            // Copy from one byte array to another.
            byte[] Expected = new byte[100];
            for (int i = 0; i < 11; i++) Expected[i] = (byte)i;

            byte[] src = new Byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int srcOffset = 0;
            byte[] dest = new Byte[100];
            int destOffset = 0;
            int len = 11;
            TheCommonUtils.cdeBlockCopy(src, srcOffset, dest, destOffset, len);

            CollectionAssert.AreEqual(Expected, dest);
            // <HelpEnd fn="TheCommonUtils.cdeBlockCopy_byte[]_int_byte[]_int_int" />

        } // function T1068_cdeBlockCopy




        [Test]
        public void T1070_cdeCompressString()
        {
            // Byte[] cdeCompressString(System.String)

            // <HelpStart fn="TheCommonUtils.cdeCompressString_string" />
            string sourceString = "This is a typical string in a program.";
            byte[] abReturned = TheCommonUtils.cdeCompressString(sourceString);

            string strOutput = TheCommonUtils.cdeDecompressToString(abReturned);
            Assert.AreEqual(sourceString, strOutput);
            // <HelpEnd fn="TheCommonUtils.cdeCompressString_string" />

            sourceString = "";
            for (int i = 0; i < 1000; i++)
            {
                sourceString = sourceString + "a";
                abReturned = TheCommonUtils.cdeCompressString(sourceString);

                strOutput = TheCommonUtils.cdeDecompressToString(abReturned);
                Assert.AreEqual(sourceString, strOutput);
            }

        } // function T1070_cdeCompressString




        [Test]
        public void T1073_cdeDecompressToString()
        {
            // System.String cdeDecompressToString(Byte[])

            // <HelpStart fn="TheCommonUtils.cdeDecompressToString_byte[]" />
            string sourceString = "This is a typical string in a program.";
            byte[] sourceArray = TheCommonUtils.cdeCompressString(sourceString);

            string strOutput = TheCommonUtils.cdeDecompressToString(sourceArray);
            Assert.AreEqual(sourceString, strOutput);
            // <HelpEnd fn="TheCommonUtils.cdeDecompressToString_byte[]" />

        } // function T1073_cdeDecompressToString


        [Test]
        public void T1075_cdeDeleteAppSetting()
        {
            // Auto-Generated Test Function
            // Void cdeDeleteAppSetting(System.String)

            // <HelpStart fn="TheCommonUtils.cdeDeleteAppSetting_string" />

            string pKeyname = "Hello C-DEngine";
#pragma warning disable CS0618 // Type or member is obsolete
            TheCommonUtils.cdeDeleteAppSetting(pKeyname);
#pragma warning restore CS0618 // Type or member is obsolete

            // No return value, so omit Assert statement.
            // Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.cdeDeleteAppSetting_string" />

        } // function T1075_cdeDeleteAppSetting

        [Test]
        public void T1076_cdeESCXML()
        {
            // Auto-Generated Test Function
            // System.String cdeESCXML(System.String)

            // <HelpStart fn="TheCommonUtils.cdeESCXML_string" />
            string strExpected = "Hello C-DEngine";

            string pToEscape = "Hello C-DEngine";
            string strReturned = TheCommonUtils.cdeESCXML(pToEscape);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdeESCXML_string" />

        } // function T1076_cdeESCXML

        [Test]
        public void T1077_cdeESCXMLwBR()
        {
            // Auto-Generated Test Function
            // System.String cdeESCXMLwBR(System.String)

            // <HelpStart fn="TheCommonUtils.cdeESCXMLwBR_string" />
            string strExpected = "Hello C-DEngine";

            string pToEscape = "Hello C-DEngine";
            string strReturned = TheCommonUtils.cdeESCXMLwBR(pToEscape);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdeESCXMLwBR_string" />

        } // function T1077_cdeESCXMLwBR

        [Test]
        public void T1078_cdeEscapeString()
        {
            // Auto-Generated Test Function
            // System.String cdeEscapeString(System.String)

            // <HelpStart fn="TheCommonUtils.cdeEscapeString_string" />

            string pToEscape = "One + Two = Three (Right!)";
            string strReturned = TheCommonUtils.cdeEscapeString(pToEscape);
            string strExpected = "One%20%2B%20Two%20%3D%20Three%20%28Right%21%29";

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdeEscapeString_string" />

        } // function T1078_cdeEscapeString



        [Test]
        public void T1081_cdeGetAppSetting()
        {
            // Auto-Generated Test Function
            // System.String cdeGetAppSetting(System.String, System.String, Boolean, Boolean)

            // <HelpStart fn="TheCommonUtils.cdeGetAppSetting_string_string_bool_bool" />
            string strExpected = "Hello C-DEngine";

            string pSetting = "Hello C-DEngine";
            string alt = "Hello C-DEngine";
            bool IsEncrypted = true;
            bool IsAltDefault = true;
#pragma warning disable CS0618 // Type or member is obsolete
            string strReturned = TheCommonUtils.cdeGetAppSetting(pSetting, alt, IsEncrypted, IsAltDefault);
#pragma warning restore CS0618 // Type or member is obsolete

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdeGetAppSetting_string_string_bool_bool" />

        } // function T1081_cdeGetAppSetting

        [Test]
        public void T1082_cdeGuidToString()
        {
            // Auto-Generated Test Function
            // System.String cdeGuidToString(System.Guid)

            // <HelpStart fn="TheCommonUtils.cdeGuidToString_Guid" />
            Guid InGuid = Guid.NewGuid();
            string strExpected = InGuid.ToString();
            strExpected = strExpected.Replace("-", "").ToUpper();

            string strReturned = TheCommonUtils.cdeGuidToString(InGuid);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdeGuidToString_Guid" />

        } // function T1082_cdeGuidToString



        [Test]
        public void T1084_cdeJavaEncode()
        {
            // Auto-Generated Test Function
            // System.String cdeJavaEncode(System.String)

            // <HelpStart fn="TheCommonUtils.cdeJavaEncode_string" />
            string strExpected = "Hello C-DEngine";

            string ostr = "Hello C-DEngine";
            string strReturned = TheCommonUtils.cdeJavaEncode(ostr);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdeJavaEncode_string" />

        } // function T1084_cdeJavaEncode

        [Test]
        public void T1085_cdeJavaEncode4Code()
        {
            // Auto-Generated Test Function
            // System.String cdeJavaEncode4Code(System.String)

            // <HelpStart fn="TheCommonUtils.cdeJavaEncode4Code_string" />
            string strExpected = "Hello C-DEngine";

            string ostr = "Hello C-DEngine";
            string strReturned = TheCommonUtils.cdeJavaEncode4Code(ostr);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdeJavaEncode4Code_string" />

        } // function T1085_cdeJavaEncode4Code

        [Test]
        public void T1086_cdeJavaEncode4Line()
        {
            // Auto-Generated Test Function
            // System.String cdeJavaEncode4Line(System.String)

            // <HelpStart fn="TheCommonUtils.cdeJavaEncode4Line_string" />
            string strExpected = "Hello C-DEngine";

            string ostr = "Hello C-DEngine";
            string strReturned = TheCommonUtils.cdeJavaEncode4Line(ostr);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdeJavaEncode4Line_string" />

        } // function T1086_cdeJavaEncode4Line

        [Test]
        public void T1087_cdePartialEscapeString()
        {
            // Auto-Generated Test Function
            // System.String cdePartialEscapeString(System.String)

            // <HelpStart fn="TheCommonUtils.cdePartialEscapeString_string" />
            string strExpected = "Hello C-DEngine";

            string InString = "Hello C-DEngine";
            string strReturned = TheCommonUtils.cdePartialEscapeString(InString);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdePartialEscapeString_string" />

        } // function T1087_cdePartialEscapeString



        [Test]
        public void T1092_cdeSplit()
        {
            // Auto-Generated Test Function
            // System.String[] cdeSplit(System.String, Char, Boolean, Boolean)

            // <HelpStart fn="TheCommonUtils.cdeSplit_string_char_bool_bool" />
            string[] astrExpected = new string[] { "2018", "06", "01" };

            string pToSplit = "2018-06-01";
            char pSeparator = '-';
            bool RemoveDuplicates = false;
            bool RemoveEmtpyEntries = true;
            string[] astrReturned = TheCommonUtils.cdeSplit(pToSplit, pSeparator, RemoveDuplicates, RemoveEmtpyEntries);

            CollectionAssert.AreEqual(astrExpected, astrReturned);
            // <HelpEnd fn="TheCommonUtils.cdeSplit_string_char_bool_bool" />

        } // function T1092_cdeSplit

        [Test]
        public void T1093_cdeSplit()
        {
            // Auto-Generated Test Function
            // System.String[] cdeSplit(System.String, System.String, Boolean, Boolean)

            // <HelpStart fn="TheCommonUtils.cdeSplit_string_char_bool_bool" />
            string[] astrExpected = new string[] { "192", "168", "45", "1", "3600" };

            string pToSplit = "192..168..45..1..3600";
            string pSeparator = "..";
            bool RemoveDuplicates = true;
            bool RemoveEmtpyEntries = true;
            string[] astrReturned = TheCommonUtils.cdeSplit(pToSplit, pSeparator, RemoveDuplicates, RemoveEmtpyEntries);

            CollectionAssert.AreEqual(astrExpected, astrReturned);
            // <HelpEnd fn="TheCommonUtils.cdeSplit_string_char_bool_bool" />

        } // function T1093_cdeSplit

        [Test]
        public void T1094_cdeStripIllegalChars()
        {
            // Auto-Generated Test Function
            // System.String cdeStripIllegalChars(System.String)

            // <HelpStart fn="TheCommonUtils.cdeStripIllegalChars_string" />
            string strExpected = "Hello C-DEngine";

            string strInput = "\t\r\nHello C-DEngine\t\r\n";
            string strReturned = TheCommonUtils.cdeStripIllegalChars(strInput);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdeStripIllegalChars_string" />

        } // function T1094_cdeStripIllegalChars

        [Test]
        public void T1095_cdeSubstringMax()
        {
            // Auto-Generated Test Function
            // System.String cdeSubstringMax(System.String, Int32)

            // <HelpStart fn="TheCommonUtils.cdeSubstringMax_string_int" />
            string strExpected = "Hello C-DEngine";

            string pIn = "Hello C-DEngine";
            int pMax = 789;
            string strReturned = TheCommonUtils.cdeSubstringMax(pIn, pMax);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdeSubstringMax_string_int" />

        } // function T1095_cdeSubstringMax

        [Test]
        public void T1096_cdeTruncate()
        {
            // Auto-Generated Test Function
            // Single cdeTruncate(Single, Int32)

            // <HelpStart fn="TheCommonUtils.cdeTruncate_double_int" />
            Single Expected = 9876.59f;

            Single value = 9876.5987f;
            int digits = 2;
            double Returned = TheCommonUtils.cdeTruncate(value, digits);

            Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.cdeTruncate_double_int" />

        } // function T1096_cdeTruncate

        [Test]
        public void T1097_cdeTruncate()
        {
            // Auto-Generated Test Function
            // Double cdeTruncate(Double, Int32)

            // <HelpStart fn="TheCommonUtils.cdeTruncate_double_int" />
            double Expected = 987654321.9876;

            double value = 987654321.9876543;
            int digits = 4;
            double Returned = TheCommonUtils.cdeTruncate(value, digits);

            Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.cdeTruncate_double_int" />

        } // function T1097_cdeTruncate

        [Test]
        public void T1098_cdeUUIDtoGuid()
        {
            // Auto-Generated Test Function
            // System.Guid cdeUUIDtoGuid(System.String)

            // <HelpStart fn="TheCommonUtils.cdeUUIDtoGuid_string" />
            // Convert a GUID in a string to a C# Guid
            Guid Expected = Guid.NewGuid();

            string pUid = Expected.ToString();
            Guid Returned = TheCommonUtils.cdeUUIDtoGuid(pUid);

            Assert.AreEqual(Expected, Returned);
            // <HelpEnd fn="TheCommonUtils.cdeUUIDtoGuid_string" />

        } // function T1098_cdeUUIDtoGuid

        [Test]
        public void T1099_cdeUnESCXML()
        {
            // Auto-Generated Test Function
            // System.String cdeUnESCXML(System.String)

            // <HelpStart fn="TheCommonUtils.cdeUnESCXML_string" />
            string strExpected = "Hello C-DEngine";

            string pToEscape = "Hello C-DEngine";
            string strReturned = TheCommonUtils.cdeUnESCXML(pToEscape);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdeUnESCXML_string" />

        } // function T1099_cdeUnESCXML

        [Test]
        public void T1100_cdeUnescapeString()
        {
            // Auto-Generated Test Function
            // System.String cdeUnescapeString(System.String)

            // <HelpStart fn="TheCommonUtils.cdeUnescapeString_string" />
            string strExpected = "Hello C-DEngine";


            string pToEscape = "Hello C-DEngine";
            string strReturned = TheCommonUtils.cdeUnescapeString(pToEscape);

            Assert.AreEqual(strExpected, strReturned);
            // <HelpEnd fn="TheCommonUtils.cdeUnescapeString_string" />

        } // function T1100_cdeUnescapeString


    }
    }























//[Test]
//public void T1072_cdeCreateXMLElemet()
//{
//    // Auto-Generated Test Function
//    // System.String cdeCreateXMLElemet(System.String, Byte[])

//    // <HelpStart fn="TheCommonUtils.cdeCreateXMLElemet_string_byte[]" />
//    string strExpected = "Hello C-DEngine";

//    string pEleName = "Hello C-DEngine";
//    byte[] pTag = new Byte[] { 0, 1, 2, 3, 4, 5 };
//    string strReturned = TheCommonUtils.cdeCreateXMLElement(pEleName, pTag);

//    Assert.AreEqual(strExpected, strReturned);
//    // <HelpEnd fn="TheCommonUtils.cdeCreateXMLElemet_string_byte[]" />

//} // function T1072_cdeCreateXMLElemet



//[Test]
//public void T1074_cdeDecompressToString()
//{
//    // Auto-Generated Test Function
//    // System.String cdeDecompressToString(Byte[], Int32, Int32)

//    // <HelpStart fn="TheCommonUtils.cdeDecompressToString_byte[]_int_int" />
//    string strExpected = "Hello C-DEngine";

//    byte[] sourceArray = new Byte[] { 0, 1, 2, 3, 4, 5 };
//    int pStartPointer = 789;
//    int len = 789;
//    string strReturned = TheCommonUtils.cdeDecompressToString(sourceArray, pStartPointer, len);

//    Assert.AreEqual(strExpected, strReturned);
//    // <HelpEnd fn="TheCommonUtils.cdeDecompressToString_byte[]_int_int" />

//} // function T1074_cdeDecompressToString


//[Test]
//public void T1079_cdeFixupFileName()
//{
//    // Auto-Generated Test Function
//    // System.String cdeFixupFileName(System.String)

//    // <HelpStart fn="TheCommonUtils.cdeFixupFileName_string" />
//    string strExpected = "Hello C-DEngine";

//    string pFileName = "Hello C-DEngine";
//    string strReturned = TheCommonUtils.cdeFixupFileName(pFileName);

//    Assert.AreEqual(strExpected, strReturned);
//    // <HelpEnd fn="TheCommonUtils.cdeFixupFileName_string" />

//} // function T1079_cdeFixupFileName

//[Test]
//public void T1080_cdeGetAppSetting()
//{
//    // Auto-Generated Test Function
//    // System.String cdeGetAppSetting(System.String, System.String, Boolean)

//    // <HelpStart fn="TheCommonUtils.cdeGetAppSetting_string_string_bool" />
//    string strExpected = "Hello C-DEngine";

//    string pSetting = "Hello C-DEngine";
//    string alt = "Hello C-DEngine";
//    bool IsEncrypted = true;
//    string strReturned = TheCommonUtils.cdeGetAppSetting(pSetting, alt, IsEncrypted);

//    Assert.AreEqual(strExpected, strReturned);
//    // <HelpEnd fn="TheCommonUtils.cdeGetAppSetting_string_string_bool" />

//} // function T1080_cdeGetAppSetting



//[Test]
//public void T1083_cdeIsLocked()
//{
//    // Auto-Generated Test Function
//    // Boolean cdeIsLocked(System.Object)

//    // <HelpStart fn="TheCommonUtils.cdeIsLocked_object" />
//    // Test 1 - not locked.
//    bool bExpected = true;
//    bool bReturned = false;

//    object o = new object();
//    //TheCommonUtils.cdeRunAsync("cdeIsLocked", false, (obj)=>
//    //{
//    //    Monitor.Enter(obj);
//    //    TheCommonUtils.SleepOneEye(5000, 100);
//    //    // Monitor.Exit(o);
//    //}, o);
//    //System.Threading.Thread.Sleep(1000);
//    //// TheCommonUtils.SleepOneEye(20000, 10);
//    //bReturned = TheCommonUtils.cdeIsLocked(o);

//    Assert.AreEqual(bExpected, bReturned);

//    // Test 1 - locked object.
//    bExpected = true;

//    lock (o)
//    {
//        bReturned = TheCommonUtils.cdeIsLocked(o);
//    }

//    Assert.AreEqual(bExpected, bReturned);

//    // <HelpEnd fn="TheCommonUtils.cdeIsLocked_object" />

//} // function T1083_cdeIsLocked




//[Test]
//public void T1088_cdeRSADecrypt()
//{
//    // Auto-Generated Test Function
//    // System.String cdeRSADecrypt(System.Guid, System.String)

//    // <HelpStart fn="TheCommonUtils.cdeRSADecrypt_Guid_byte[]" />
//    string strExpected = "Hello C-DEngine";

//    Guid pSessionID = Guid.NewGuid();
//    byte[] val = new Byte[] { 0, 1, 2, 3, 4, 5 };
//    string strReturned = TheCommonUtils.cdeRSADecrypt(pSessionID, val);

//    Assert.AreEqual(strExpected, strReturned);
//    // <HelpEnd fn="TheCommonUtils.cdeRSADecrypt_Guid_byte[]" />

//} // function T1088_cdeRSADecrypt

//[Test]
//public void T1089_cdeRSADecrypt()
//{
//    // Auto-Generated Test Function
//    // System.String cdeRSADecrypt(System.Guid, Byte[])

//    // <HelpStart fn="TheCommonUtils.cdeRSADecrypt_Guid_byte[]" />
//    string strExpected = "Hello C-DEngine";

//    Guid pSessionID = Guid.NewGuid();
//    byte[] val = new Byte[] { 0, 1, 2, 3, 4, 5 };
//    string strReturned = TheCommonUtils.cdeRSADecrypt(pSessionID, val);

//    Assert.AreEqual(strExpected, strReturned);
//    // <HelpEnd fn="TheCommonUtils.cdeRSADecrypt_Guid_byte[]" />

//} // function T1089_cdeRSADecrypt

//[Test]
//public void T1090_cdeRSAEncrypt()
//{
//    // Auto-Generated Test Function
//    // Byte[] cdeRSAEncrypt(System.Guid, System.String)

//    // <HelpStart fn="TheCommonUtils.cdeRSAEncrypt_Guid_string" />
//    byte[] abExpected = new Byte[] { 0, 1, 2, 3, 4, 5 };

//    Guid pSessionID = Guid.NewGuid();
//    string val = "Hello C-DEngine";
//    byte[] abReturned = TheCommonUtils.cdeRSAEncrypt(pSessionID, val);

//    Assert.AreEqual(abExpected, abReturned);
//    // <HelpEnd fn="TheCommonUtils.cdeRSAEncrypt_Guid_string" />

//} // function T1090_cdeRSAEncrypt

//[Test]
//public void T1091_cdeRSAEncryptWithKeys()
//{
//    // Auto-Generated Test Function
//    // System.String cdeRSAEncryptWithKeys(System.String, System.String)

//    // <HelpStart fn="TheCommonUtils.cdeRSAEncryptWithKeys_string_string" />
//    byte[] abExpected = new byte[5];

//    string rsaPublic = "";
//    string val = "";
//    byte[] abReturned = TheCommonUtils.cdeRSAEncryptWithKeys(rsaPublic, val);

//    Assert.AreEqual(abExpected, abReturned);
//    // <HelpEnd fn="TheCommonUtils.cdeRSAEncryptWithKeys_string_string" />

//} // function T1091_cdeRSAen