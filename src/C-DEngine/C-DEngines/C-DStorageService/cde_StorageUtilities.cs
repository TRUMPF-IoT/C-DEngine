// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.StorageService.Model;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.ThingService;
using System.Runtime.Serialization;
// ReSharper disable UseNullPropagation

//ERROR Range 460-469

namespace nsCDEngine.Engines.StorageService
{

    /// <summary>
    /// Commonly used utilities for Storage Related code
    /// </summary>
    public class TheStorageUtilities
    {
        internal static int GetTypeLength(string finfo)
        {
            int TypeLength;
            switch (finfo)
            {
                case "String":
                    TypeLength = 20;
                    break;
                case "Int16":
                case "Int32":
                    TypeLength = 10;
                    break;
                case "Single":
                    TypeLength = 15;
                    break;
                case "DateTimeOffset":
                    TypeLength = 22;
                    break;
                case "DateTime":
                    TypeLength = 20;
                    break;
                case "Boolean":
                    TypeLength = 5;
                    break;
                case "Guid":
                    TypeLength = 25;
                    break;
                case "Double":
                    TypeLength = 15;
                    break;
                case "Int64":
                    TypeLength = 12;
                    break;
                case "Byte":
                    TypeLength = 5;
                    break;
                case "Char":
                    TypeLength = 5;
                    break;
                case "Char[]":
                    TypeLength = 100;
                    break;
                case "Byte[]":
                    TypeLength = 0;
                    break;
                default:
                    TypeLength = 20;
                    break;
            }
            return TypeLength;
        }


        /// <summary>
        /// Checks if an object is of an enum type
        /// </summary>
        /// <param name="finfo"></param>
        /// <returns></returns>
        internal static bool IsEnum(object finfo)
        {
            if (finfo is FieldInfo)
            {
                if (((FieldInfo)finfo).FieldType.IsEnum)
                {
                    return true;
                }
            }
            if (finfo is PropertyInfo)
            {
                if (((PropertyInfo)finfo).PropertyType.IsEnum)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the ID of an object's type which is used to map to the correct external storage (SQL, etc.) type
        /// </summary>
        /// <param name="pfinfo">The object whose type ID will be determined</param>
        /// <returns>The integer ID of the parameter's type</returns>
        public static int GetTypeID(object pfinfo)
        {
            //string fBaseType = "";
            bool tIsEnum = IsEnum(pfinfo);
            string fName;
            if (pfinfo is FieldInfo)
            {
                fName = ((FieldInfo)pfinfo).FieldType.FullName;
                //tIsEnum = ((FieldInfo)pfinfo).FieldType.IsEnum;
            }
            else if (pfinfo is PropertyInfo)
            {
                fName = ((PropertyInfo)pfinfo).PropertyType.FullName;
                //tIsEnum = ((PropertyInfo)pfinfo).PropertyType.IsEnum;
            }
            else if (pfinfo is string)
            {
                fName = TheCommonUtils.CStr(pfinfo);
            }
            else return 1;  //Must return a value..default is string

            int TypeID = 0;
            switch (fName)
            {
                case "System.String":
                    TypeID = 1;
                    break;
                case "System.Int32":
                    TypeID = 2;
                    break;
                case "System.Single":
                    TypeID = 3;
                    break;
                case "System.DateTime":
                case "System.DateTimeOffset":
                    TypeID = 4;
                    break;
                case "System.Boolean":
                    TypeID = 5;
                    break;
                case "System.Guid":
                    TypeID = 6;
                    break;
                case "System.Double":
                    TypeID = 7;
                    break;
                case "System.Int64":
                    TypeID = 8;
                    break;
                case "System.Byte":
                    TypeID = 9;
                    break;
                case "System.Char":
                    TypeID = 10;
                    break;
                case "System.Char[]":
                    TypeID = 11;
                    break;
                case "System.Byte[]":
                    TypeID = 12;
                    break;
                case "System.Int16":
                    TypeID = 14;
                    break;
                default:
                    if (tIsEnum)
                    {
                        TypeID = 13;
                    }
                    else if (fName.StartsWith("nsCDEngine.ViewModels.cdeConcurrentDictionary") || fName.StartsWith("System.Collections.Generic.Dictionary"))
                    {
                        TypeID = 11;
                    }
                    else if (fName.StartsWith("System.Nullable`1[[System.Int64"))
                    {
                        TypeID = 8;
                    }
                    break;
            }
            return TypeID;
        }

        /// <summary>
        /// Maps the type of a parent to the type of its property bag (if it contains one)
        /// </summary>
        /// <param name="ParentType">Type of the instance that has the property bag</param>
        public static Type GetPropertyBagRecordType(Type ParentType)
        {
            string fName = ParentType.FullName;
            Type PropBagElementType = null;
            switch (fName)
            {
                case "nsCDEngine.Engines.ThingService.TheThing":
                    Type PropBagType = ParentType.GetProperty("MyPropertyBag").PropertyType;
                    if (PropBagType.IsGenericType)
                        PropBagElementType = PropBagType.GetGenericArguments()[1];
                    break;
                case "nsCDEngine.ViewModels.TheThingStore":
                    PropBagElementType = typeof(KeyValuePair<string, object>);
                    break;
            }
            return PropBagElementType;
        }

        internal static object cdeConvertObject(object pfinfo, string FieldContent)
        {
            string fName = "";
            //string fBaseType = "";
            bool tIsEnum = IsEnum(pfinfo);
            Type[] TypeArgs = null;
            if (pfinfo is FieldInfo)
            {
                fName = ((FieldInfo)pfinfo).FieldType.FullName;
#if CDE_NET35 || CDE_NET4
                TypeArgs = ((FieldInfo)pfinfo).FieldType.GetGenericArguments().Where(t => !t.IsGenericParameter).ToArray();
#else
                TypeArgs = ((FieldInfo)pfinfo).FieldType.GenericTypeArguments;
#endif
                //tIsEnum = ((FieldInfo)pfinfo).FieldType.IsEnum;
            }
            else if (pfinfo is PropertyInfo)
            {
                fName = ((PropertyInfo)pfinfo).PropertyType.FullName;
#if CDE_NET35 || CDE_NET4
                TypeArgs = ((PropertyInfo)pfinfo).PropertyType.GetGenericArguments().Where(t => !t.IsGenericParameter).ToArray();
#else
                TypeArgs = ((PropertyInfo)pfinfo).PropertyType.GenericTypeArguments;
#endif
                //tIsEnum = ((PropertyInfo)pfinfo).PropertyType.IsEnum;
            }
            else
                return "";

            object Result = null;
            try
            {
                switch (fName)
                {
                    case "System.Char":
                        Result = TheCommonUtils.CChar(FieldContent);
                        break;
                    case "System.Byte":
                        Result = TheCommonUtils.CByte(FieldContent);
                        break;
                    case "System.Char[]":
                        Result = FieldContent.ToCharArray();
                        break;
                    case "System.Byte[]":
                        Result = Convert.FromBase64String(FieldContent);
                        break;
                    case "System.Int64":
                        if (FieldContent.Length == 0)
                            Result = (long)0;
                        else
                            Result =  TheCommonUtils.CLng(FieldContent);
                        break;
                    case "System.Int32":
                        Result = FieldContent.Length == 0 ? 0 : TheCommonUtils.CInt(FieldContent);
                        break;
                    case "System.Int16":
                        Result = FieldContent.Length == 0 ? 0 : TheCommonUtils.CShort(FieldContent);
                        break;
                    case "System.Single":
                        if (FieldContent.Length == 0)
                            Result = (float)0;
                        else
                            Result = (float)TheCommonUtils.CDbl(FieldContent);
                        break;
                    case "System.DateTime":
                    case "System.DateTimeOffset":
                        if (FieldContent.Length == 0)
                            Result = null;
                        else
                            Result = TheCommonUtils.CDate(FieldContent);
                        break;
                    case "System.Boolean":
                        Result = FieldContent.Length != 0 && TheCommonUtils.CBool(FieldContent);
                        break;
                    case "System.Double":
                        if (FieldContent.Length == 0)
                            Result = (double)0;
                        else
                            Result = TheCommonUtils.CDbl(FieldContent);
                        break;
                    case "System.Guid":
                        if (FieldContent.Length == 0)
                            Result = null;
                        else
                            Result = TheCommonUtils.CGuid(FieldContent);
                        break;
                    default:
                        if (tIsEnum)
                        {
                            Result = FieldContent.Length == 0 ? 0 : TheCommonUtils.CInt(FieldContent);
                        }
                        else if (fName.StartsWith("System.Collections.Generic.List`1[[nsCDEngine.ViewModels.eThingCaps"))
                        {
                            if (FieldContent.Length == 0)
                                Result = null;
                            else
                                Result = TheCommonUtils.DeserializeJSONStringToObject<List<eThingCaps>>(FieldContent);
                        }
                        else if (fName.StartsWith("nsCDEngine.ViewModels.cdeConcurrentDictionary"))
                        {
                            if (FieldContent.Length == 0)
                                Result = null;
                            else
                            {
                                if(TypeArgs != null)
                                {
                                    if(TypeArgs.Length == 2)
                                    {
                                        if(TypeArgs[0].Equals(typeof(string)) && TypeArgs[1].Equals(typeof(cdeP)))
                                            Result = TheCommonUtils.DeserializeJSONStringToObject<cdeConcurrentDictionary<string, cdeP>>(FieldContent);
                                    }
                                }
                            }
                            break;
                        }
                        else if (fName.StartsWith("System.Nullable`1[System.Boolean]"))
                        {
                            Result = TheCommonUtils.CBool(FieldContent);
                            break;
                        }
                        else if (fName.StartsWith("System.Nullable`1[[System.Int64"))
                        {
                            if (FieldContent.Length == 0)
                                Result = null;
                            else
                                Result = TheCommonUtils.CLng(FieldContent);
                            break;
                        }
                        else if (fName.StartsWith("System.Nullable`1[[System.Guid"))
                        {
                            if (FieldContent.Length == 0)
                                Result = null;
                            else
                                Result = TheCommonUtils.CGuid(FieldContent);
                            break;
                        }
                        else if (fName.StartsWith("System.Collections.Generic.Dictionary"))
                        {
                            if (FieldContent.Length == 0)
                                Result = null;
                            else
                            {
                                if (TypeArgs != null)
                                {
                                    if (TypeArgs.Length == 2)
                                    {
                                        if (TypeArgs[0].Equals(typeof(string)) && TypeArgs[1] is object)
                                            Result = TheCommonUtils.DeserializeJSONStringToObject<Dictionary<string, object>>(FieldContent);
                                    }
                                }
                            }
                        }
                        else
                            Result = FieldContent;
                        break;
                }
            }
            catch
            {
                Result = null;
            }
            return Result;
        }

        /// <summary>
        /// Returns true if the object is a numeric type
        /// </summary>
        /// <param name="value">Incoming object</param>
        /// <returns></returns>
        public static bool IsNumeric(object value)
        {
            return value is int
                || value is uint
                || value is double
                || value is float
                || value is short
                || value is ushort
                || value is long
                || value is ulong
                || value is byte
                || value is sbyte
                || value is decimal;
        }

        internal static string[] PropertiesToExclude = { "MyPropertyBag" };
        internal static string[] ThingStorePropertiesToInclude = { "cdeA", "cdeN", "cdeO", "cdeSEQ" };
        internal static string[] defaultRows = new string[] { "cdeIDX", "cdeMID", "cdeCTIM", "cdeSCOPEID" };

        /// <summary>
        /// Creates a unique ID from a C# Type.
        /// </summary>
        /// <param name="MyType">type to be converted to a unique ID</param>
        /// <param name="pTableName">if not null or empty, the tablename will be added to the unique id at the end</param>
        /// <returns></returns>
        public static string GenerateUniqueIDFromType(Type MyType, string pTableName)
        {
            string uid = "";
            if (MyType == null)
                uid = "<null>";
            else
            {
                int HashNumber = 0;
                string UniqueStoreFingerPrint = "";
                int tID = 0;
                List<FieldInfo> Fieldsinfoarray = MyType.GetFields().Where(field => !Attribute.IsDefined(field, typeof(IgnoreDataMemberAttribute))).OrderBy(x => x.Name).ToList();
                List<PropertyInfo> PropInfoArray;
                if (MyType.Equals(typeof(TheThingStore)))
                    PropInfoArray = MyType.GetProperties().Where(prop => !Attribute.IsDefined(prop, typeof(IgnoreDataMemberAttribute)) && ThingStorePropertiesToInclude.Contains(prop.Name)).OrderBy(x => x.Name).ToList();
                else
                    PropInfoArray = MyType.GetProperties().Where(prop => !Attribute.IsDefined(prop, typeof(IgnoreDataMemberAttribute)) && !PropertiesToExclude.Contains(prop.Name)).OrderBy(x => x.Name).ToList();
                foreach (FieldInfo finfo in Fieldsinfoarray)
                {
                    tID = GetTypeID(finfo);
                    UniqueStoreFingerPrint += string.Format("{0:X}", tID);
                    HashNumber += tID + finfo.Name.Length;
                }
                foreach (PropertyInfo finfo in PropInfoArray)
                {
                    tID = GetTypeID(finfo);
                    UniqueStoreFingerPrint += string.Format("{0:X}", tID);
                    HashNumber += tID + finfo.Name.Length;
                }
                uid = HashNumber + "&" + UniqueStoreFingerPrint + $"{(string.IsNullOrEmpty(pTableName) ? "" : $"-{pTableName}")}";
            }
            return uid;
        }

        internal static bool DoesWildContentMatch(object pClassInstance, string pContent, List<TheFieldInfo> pFldInfoList)
        {
            if (pClassInstance == null || string.IsNullOrEmpty(pContent))
                return false;
            Type MyType = pClassInstance.GetType();
            List<FieldInfo> Fieldsinfoarray = MyType.GetFields().Where(field => !Attribute.IsDefined(field, typeof(IgnoreDataMemberAttribute))).OrderBy(x => x.Name).ToList();
            List<PropertyInfo> PropInfoArray = MyType.GetProperties().Where(prop => !Attribute.IsDefined(prop, typeof(IgnoreDataMemberAttribute))).OrderBy(x => x.Name).ToList();

            foreach (FieldInfo finfo in Fieldsinfoarray)
            {
                if (pFldInfoList?.Any(fi => fi.DataItem?.Contains(finfo.Name) == true) == true && TheCommonUtils.CStr(finfo.GetValue(pClassInstance)).IndexOf(pContent, StringComparison.OrdinalIgnoreCase)>=0)
                    return true;
            }

            foreach (PropertyInfo finfo in PropInfoArray)
            {
                if (pFldInfoList?.Any(fi => fi.DataItem?.Contains(finfo.Name) == true) == true && TheCommonUtils.CStr(finfo.GetValue(pClassInstance, null)).IndexOf(pContent, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        internal static int GetFieldNameIndex(Type MyType, string pFieldName)
        {
            List<FieldInfo> Fieldsinfoarray = MyType.GetFields().Where(field => !Attribute.IsDefined(field, typeof(IgnoreDataMemberAttribute))).OrderBy(x => x.Name).ToList();
            List<PropertyInfo> PropInfoArray = MyType.GetProperties().Where(prop => !Attribute.IsDefined(prop, typeof(IgnoreDataMemberAttribute))).OrderBy(x => x.Name).ToList();
            int idx = 0;

            foreach (FieldInfo finfo in Fieldsinfoarray)
            {
                if (finfo.Name.Equals(pFieldName))
                    return idx;
                idx++;
            }

            foreach (PropertyInfo finfo in PropInfoArray)
            {
                if (finfo.Name.Equals(pFieldName))
                    return idx;
                idx++;
            }
            return -1;
        }

        /// <summary>
        /// Converts TheDataRetrievalRecord back in the the Records of the Storage mirror
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tRecords"></param>
        /// <param name="IsWriting"></param>
        /// <param name="ErrorText"></param>
        /// <returns></returns>
        public static List<T> ConvertFromStoreRecord<T>(TheDataRetrievalRecord tRecords,bool IsWriting, out string ErrorText) where T : new()
        {
            ErrorText = "";
            List<T> ResultList = new List<T>();
            List<FieldInfo> Fieldsinfoarray = typeof(T).GetFields().Where(field => !Attribute.IsDefined(field, typeof(IgnoreDataMemberAttribute))).ToList();
            List<PropertyInfo> PropInfoArray = typeof(T).GetProperties().Where(prop => !Attribute.IsDefined(prop, typeof(IgnoreDataMemberAttribute))).ToList();
            foreach (List<string> tRec in tRecords.RECs)
            {
                T tRes = new T();
                var FieldValue = "";
                foreach (var fld in tRecords.FLDs)
                {
                    try
                    {
                        FieldInfo tFFF = Fieldsinfoarray.FirstOrDefault(s => s.Name == fld.N);
                        if (tFFF != null)
                        {
                            FieldValue = GetValueByCol(tRec, fld.C);
                            if (FieldValue != null && FieldValue != "<null>")
                                tFFF.SetValue(tRes, cdeConvertObject(tFFF, FieldValue));
                        }
                        else
                        {
                            PropertyInfo tPPP = PropInfoArray.FirstOrDefault(s => s.Name == fld.N);
                            if (tPPP != null)
                            {
                                FieldValue = GetValueByCol(tRec, fld.C);
                                if (FieldValue != null && FieldValue != "<null>" && FieldValue!="")
                                    tPPP.SetValue(tRes, cdeConvertObject(tPPP, FieldValue),null);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(461, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("StorageUtils", "ConvertFromStoreRecord", eMsgLevel.l1_Error, e.ToString()));
                        ErrorText += "Error during Field Deserialization for Fld:" + fld.N;
                    }
                }
                ResultList.Add(tRes);
            }
            return ResultList;
        }

        /// <summary>
        /// Gets a value by the column number in a StorageResult
        /// </summary>
        /// <param name="tRec">List of records</param>
        /// <param name="mycol">Column number</param>
        /// <returns></returns>
        public static string GetValueByCol(List<string> tRec, int mycol)
        {
            var t = from str in tRec
                    where TheCommonUtils.CInt(str.Substring(0, 3)) == mycol
                    select str.Substring(3);
            return t.FirstOrDefault();
        }

        /// <summary>
        /// Serializes a sql command for distributed storage
        /// </summary>
        /// <param name="pMyType">Type of the class this execute call goes against in the storage service</param>
        /// <param name="pMagicID">cookie of the call</param>
        /// <param name="pSqlToExecute">sql statement to serialize</param>
        /// <param name="pSqlColFilter">additional column filter if required</param>
        /// <param name="pTableName">custom table name if required</param>
        /// <returns></returns>
        public static string SerializeDataToExecute(Type pMyType, string pMagicID, string pSqlToExecute, string pSqlColFilter, string pTableName)
        {
            StorageGetRequest tDataGramm = new StorageGetRequest
            {
                SFI = pSqlToExecute,
                CFI = pSqlColFilter,
                MID = pMagicID,
                DID= TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                UID = GenerateUniqueIDFromType(pMyType, pTableName)
            };
            return TheCommonUtils.SerializeObjectToJSONString(tDataGramm);
        }

        /// <summary>
        /// Serializes a dictionary of a given type for storage in the storage service
        /// </summary>
        /// <typeparam name="T">Type of the class to be serialized</typeparam>
        /// <param name="MyValue">Dictionary of values to be serialized</param>
        /// <param name="pMagicID">cookie of the call</param>
        /// <param name="pCMD">Storage command to be executed with this store call</param>
        /// <param name="pTableName">custom table name if required</param>
        /// <returns></returns>
        public static string SerializeDataToStore<T>(Dictionary<string, T> MyValue, string pMagicID, eSCMD pCMD, string pTableName)
        {
            TheDataRetrievalRecord tDataGramm = SerializeClassToRecord(MyValue.Values.ToList(), pTableName);
            tDataGramm.CMD = pCMD;
            tDataGramm.MID = pMagicID;
            return TheCommonUtils.SerializeObjectToJSONString(tDataGramm);
        }

        /// <summary>
        /// Serializes the CreateStore Definition
        /// </summary>
        /// <param name="MyType">Type of the class that creates a new store</param>
        /// <param name="MyValue">Default value of the class</param>
        /// <param name="pDeviceName">Name of the storage</param>
        /// <param name="StoreDescription">Description of the storage</param>
        /// <param name="pUniqueID">returns a unique id for the store</param>
        /// <param name="pTableName">allows to set a custom tablename for the store. if null or empty, the unique ID will be the table name</param>
        /// <returns></returns>
        public static string SerializeCreateStore(Type MyType, object MyValue, string pDeviceName, string StoreDescription, out string pUniqueID, string pTableName)
        {
            CreateStoreDefinition(MyType, MyValue, pDeviceName, StoreDescription, pTableName, out pUniqueID, out StorageDefinition MyStorageDefinition);
            return TheCommonUtils.SerializeObjectToJSONString(MyStorageDefinition);
        }

        internal static void CreateStoreDefinition(Type MyType, object MyValue, string pDeviceName, string StoreDescription,string pTableName, out string pUniqueID, out StorageDefinition MyStorageDefinition)
        {
            List<FieldInfo> Fieldsinfoarray = MyType.GetFields().Where(field => !Attribute.IsDefined(field, typeof(IgnoreDataMemberAttribute))).OrderBy(x => x.Name).ToList();
            List<PropertyInfo> PropInfoArray;
            if(MyType.Equals(typeof(TheThingStore)))
                PropInfoArray = MyType.GetProperties().Where(prop => !Attribute.IsDefined(prop, typeof(IgnoreDataMemberAttribute)) && ThingStorePropertiesToInclude.Contains(prop.Name)).OrderBy(x => x.Name).ToList();
            else
                PropInfoArray = MyType.GetProperties().Where(prop => !Attribute.IsDefined(prop, typeof(IgnoreDataMemberAttribute)) && !PropertiesToExclude.Contains(prop.Name)).OrderBy(x => x.Name).ToList();

            MyStorageDefinition = new StorageDefinition
            {
                NAM = pDeviceName,
                DES = StoreDescription,
                CNT = Fieldsinfoarray.Count + PropInfoArray.Count,
                DID = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                DEF = MyValue != null
            };
            MyStorageDefinition.FTY = new int[MyStorageDefinition.CNT];
            MyStorageDefinition.FNA = new string[MyStorageDefinition.CNT];
            MyStorageDefinition.FDE = new string[MyStorageDefinition.CNT];
            int FldCount = 0;
            Type fType;

            string UniqueStoreFingerPrint = "";
            int HashNumber = 0;
            object orgValue = null;
            int tID = 0;

            foreach (FieldInfo finfo in Fieldsinfoarray)
            {
                fType = finfo.FieldType;
                try
                {
                    tID = GetTypeID(finfo);
                    UniqueStoreFingerPrint += string.Format("{0:X}", tID);
                    HashNumber += tID + finfo.Name.Length;

                    MyStorageDefinition.FTY[FldCount] = tID;
                    MyStorageDefinition.FNA[FldCount] = finfo.Name;
                    if (MyValue != null)
                    {
                        orgValue = finfo.GetValue(MyValue);
                        MyStorageDefinition.FDE[FldCount] = TheCommonUtils.CTypeToString(fType, orgValue);
                    }
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(463, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("StorageUtils", "SerializeCreateStore", eMsgLevel.l1_Error, e.ToString()));
                    //string err = e.Message;
                }
                FldCount++;
            }
            foreach (PropertyInfo finfo in PropInfoArray)
            {
                fType = finfo.PropertyType;
                try
                {
                    tID = GetTypeID(finfo);
                    UniqueStoreFingerPrint += string.Format("{0:X}", tID);
                    HashNumber += tID + finfo.Name.Length;
                    MyStorageDefinition.FTY[FldCount] = tID;
                    MyStorageDefinition.FNA[FldCount] = finfo.Name;
                    if (MyValue != null)
                    {
                        orgValue = finfo.GetValue(MyValue, null);
                        MyStorageDefinition.FDE[FldCount] = TheCommonUtils.CTypeToString(fType, orgValue);
                    }
                }
                catch (Exception e)
                {
                    if (TheBaseAssets.MyServiceHostInfo.DebugLevel > eDEBUG_LEVELS.ESSENTIALS)
                        TheBaseAssets.MySYSLOG.WriteToLog(464, new TSM("StorageUtils", "SerializeCreateStore2", eMsgLevel.l1_Error, e.ToString()));
                    //string err = e.Message;
                }
                FldCount++;
            }
            MyStorageDefinition.UID = HashNumber +"&"+ UniqueStoreFingerPrint + $"{(string.IsNullOrEmpty(pTableName) ? "" : $"-{pTableName}")}";
            pUniqueID = MyStorageDefinition.UID;
        }

        internal static TheDataRetrievalRecord SerializeClassToRecord<T>(List<T> MyValue, string pTableName)
        {
            object orgValue = null;
            Type fType;
            List<FieldInfo> Fieldsinfoarray = typeof(T).GetFields().Where(field => !Attribute.IsDefined(field, typeof(IgnoreDataMemberAttribute))).OrderBy(x => x.Name).ToList();
            List<PropertyInfo> PropInfoArray;
            if (typeof(T).Equals(typeof(TheThingStore)))
                PropInfoArray = typeof(T).GetProperties().Where(prop => !Attribute.IsDefined(prop, typeof(IgnoreDataMemberAttribute)) && ThingStorePropertiesToInclude.Contains(prop.Name)).OrderBy(x => x.Name).ToList();
            else
                PropInfoArray = typeof(T).GetProperties().Where(prop => !Attribute.IsDefined(prop, typeof(IgnoreDataMemberAttribute)) && !PropertiesToExclude.Contains(prop.Name)).OrderBy(x => x.Name).ToList();
            TheDataRetrievalRecord tDataGramm = new TheDataRetrievalRecord {UID = GenerateUniqueIDFromType(typeof(T), pTableName)};
            int ColNo = 0;

            foreach (FieldInfo finfo in Fieldsinfoarray)
            {
                TFD tFieldDesc = new TFD();
                fType = finfo.FieldType;
                tFieldDesc.N = finfo.Name;
                tFieldDesc.T = finfo.FieldType.ToString();
                tFieldDesc.C = ColNo;
                tDataGramm.FLDs.Add(tFieldDesc);
                ColNo++;
            }

            foreach (PropertyInfo finfo in PropInfoArray)
            {
                TFD tFieldDesc = new TFD
                {
                    N = finfo.Name,
                    T = finfo.PropertyType.ToString(),
                    C = ColNo
                };
                tDataGramm.FLDs.Add(tFieldDesc);
                ColNo++;
            }

            foreach (T item in MyValue)
            {
                List<string> tRec = new List<string>();

                //Fieldsinfoarray = item.GetType().GetFields().OrderBy(x => x.MetadataToken).ToList();
                foreach (FieldInfo finfo in Fieldsinfoarray)
                {
                    ColNo = GetColNoByName(tDataGramm.FLDs, finfo.Name);
                    if (ColNo>=0)
                    {
                        fType = finfo.FieldType;
                        try
                        {
                            orgValue = finfo.GetValue(item);
                            if (fType == Type.GetType("System.DateTime"))
                            {
                                if (((DateTime)orgValue).Equals(DateTime.MinValue))
                                    orgValue = null;
                                else
                                {
                                    DateTime tDateTime = (DateTime)orgValue;
                                    orgValue = string.Format("{0:MM/dd/yyyy HH:mm:ss.fff}", tDateTime);
                                }
                            }
                            else if (fType == Type.GetType("System.DateTimeOffset"))
                            {
                                if (((DateTimeOffset)orgValue).Equals(DateTimeOffset.MinValue))
                                    orgValue = null;
                                else
                                {
                                    DateTimeOffset tDateTime = (DateTimeOffset)orgValue;
                                    orgValue = string.Format("{0:MM/dd/yyyy HH:mm:ss.fff zzz}", tDateTime);
                                }
                            }
                            else
                            {
                                if (fType == Type.GetType("System.Byte[]") && orgValue != null)
                                {
                                    orgValue = Convert.ToBase64String((byte[])orgValue);
                                }
                                else
                                {
                                    if (fType == Type.GetType("System.Char[]") && orgValue != null)
                                    {
                                        orgValue = new string((Char[])orgValue);
                                    }
                                    else
                                    {
                                        if (fType.IsEnum)
                                        {
                                            orgValue = TheCommonUtils.CInt(orgValue);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(465, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("StorageUtils", "SerializeClassToRecord", eMsgLevel.l1_Error, e.ToString()));
                            //string err = e.Message;
                        }
                        tRec.Add(orgValue == null ? string.Format("{0:000}", ColNo) : string.Format("{0:000}{1}", ColNo, orgValue));
                    }
                }

                //PropInfoArray = item.GetType().GetProperties().OrderBy(x => x.MetadataToken).ToList();
                foreach (PropertyInfo finfo in PropInfoArray)
                {
                    ColNo = GetColNoByName(tDataGramm.FLDs, finfo.Name);
                    if (ColNo>=0)
                    {
                        fType = finfo.PropertyType;
                        try
                        {
                            orgValue = finfo.GetValue(item,null);
                            if (fType == Type.GetType("System.DateTime"))
                            {
                                if (((DateTime)orgValue).Equals(DateTime.MinValue))
                                    orgValue = null;
                                else
                                {
                                    DateTime tDateTime = (DateTime)orgValue;
                                    orgValue = string.Format("{0:MM/dd/yyyy HH:mm:ss.fff}", tDateTime);
                                }
                            }
                            else if (fType == Type.GetType("System.DateTimeOffset"))
                            {
                                if (((DateTimeOffset)orgValue).Equals(DateTimeOffset.MinValue))
                                    orgValue = null;
                                else
                                {
                                    DateTimeOffset tDateTime = (DateTimeOffset)orgValue;
                                    orgValue = string.Format("{0:MM/dd/yyyy HH:mm:ss.fff zzz}", tDateTime);
                                }
                            }
                            else if (fType.ToString().StartsWith("System.Collections.Generic.List") || fType.ToString().StartsWith("System.Collections.Generic.Dictionary"))
                            {
                                orgValue = TheCommonUtils.SerializeObjectToJSONString(orgValue);
                            }
                            else if (fType.ToString().StartsWith("nsCDEngine.ViewModels.cdeConcurrentDictionary"))
                            {
                                orgValue = TheCommonUtils.SerializeObjectToJSONString(orgValue);
                            }
                            else
                            {
                                if (fType == Type.GetType("System.Byte[]") && orgValue != null)
                                {
                                    orgValue = Convert.ToBase64String((byte[])orgValue);
                                }
                                else
                                {
                                    if (fType == Type.GetType("System.Char[]") && orgValue != null)
                                    {
                                        orgValue = new string((Char[])orgValue);
                                    }
                                    else
                                    {
                                        if (fType.IsEnum) 
                                        {
                                            orgValue =  TheCommonUtils.CInt(orgValue);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(466, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("StorageUtils", "SerializeClassToRecord2", eMsgLevel.l1_Error, e.ToString()));
                            //string err = e.Message;
                        }
                        tRec.Add(orgValue == null ? string.Format("{0:000}", ColNo) : string.Format("{0:000}{1}", ColNo, orgValue));
                    }
                }
                if (typeof(T).Equals(typeof(TheThingStore)))
                {
                    if (item is TheThingStore tThingStore)
                    {
                        foreach (KeyValuePair<string, object> Prop in tThingStore.PB)
                        {
                            TFD pbDef = tDataGramm.FLDs.Find((tfd) => tfd.N.Equals(Prop.Key));
                            if (pbDef == null)
                            {
                                int LastColNo = tDataGramm.FLDs[tDataGramm.FLDs.Count - 1].C;
                                string fldType = "";
                                if (Prop.Key.Equals("QValue"))
                                    fldType = typeof(double).ToString();
                                else if (Prop.Value != null)
                                    fldType = Prop.Value.GetType().ToString();
                                else
                                    fldType = typeof(string).ToString();
                                tDataGramm.FLDs.Add(new TFD
                                {
                                    N = Prop.Key,
                                    T = fldType,
                                    C = LastColNo + 1
                                });
                                tRec.Add(Prop.Value == null ? string.Format("{0:000}", LastColNo + 1) : string.Format("{0:000}{1}", LastColNo + 1, Prop.Value));
                            }
                            else
                            {
                                int pbKeyColumn = pbDef.C;
                                tRec.Add(Prop.Value == null ? string.Format("{0:000}", pbKeyColumn) : string.Format("{0:000}{1}", pbKeyColumn, Prop.Value));
                            }
                        }
                    }
                }
                tDataGramm.RECs.Add(tRec);
            }

            return tDataGramm;
        }


        internal static int GetColNoByName(List<TFD> pFields, string pName)
        {
            int ColNo = -1;
            try
            {
                var tcol = from myrec in pFields
                           where myrec.N == pName
                           select myrec.C;
                var enumerable = tcol as IList<int> ?? tcol.ToList();
                if (enumerable.Any())
                    ColNo = enumerable.First();
            }
            catch //(Exception e)
            {
                // ignored
            }
            return ColNo;
        }

        /// <summary>
        /// Send the requested Charts data information back to the Originator
        /// </summary>
        /// <param name="pChartGuid">Guid of the defined Charts Data</param>
        /// <param name="pOriginator">Originator to send the data to</param>
        /// <param name="IChartFactory">AssemblyQualifiedName of the ChartFactory Type that should handle the Chart Data conversion</param>
        internal static void PushChartsData(Guid pChartGuid, Guid pOriginator, string IChartFactory=null)
        {
            TheChartDefinition tDef = TheNMIEngine.GetChartByID(pChartGuid);
            if (tDef != null)
            {
                tDef.IChartFactoryType = IChartFactory;
                object MyStorageMirror = TheCDEngines.GetStorageMirror(tDef.DataSource);
                if (MyStorageMirror != null)
                {
                    Type magicType = MyStorageMirror.GetType();
                    MethodInfo magicMethod = magicType.GetMethod("PushChartData");
                    if (magicMethod != null)
                        magicMethod.Invoke(MyStorageMirror, new[] { pOriginator, (object)TheCommonUtils.CGuid(pChartGuid) });
                }
            }
        }

        /// <summary>
        /// Creates a proper SQL filter for the given Filter String
        /// </summary>
        /// <param name="pSQLFilter">Creates a filter string from given macros</param>
        /// <returns></returns>
        public static List<SQLFilter> CreateFilter(string pSQLFilter)
        {
            string[] tFilters = TheCommonUtils.cdeSplit(pSQLFilter, ";:;", false, true);
            List<string> filterSep = new List<string>();
            foreach (string filterStr in tFilters)
            {
                string[] tFilters2 = TheCommonUtils.cdeSplit(filterStr, ";", false, true);
                filterSep.AddRange(tFilters2);
            }

            List<SQLFilter> filter = new List<SQLFilter>();
            foreach (string tf in filterSep)
            {
                try
                {
                    switch (tf)
                    {
                        case "LMI":
                            filter.Add(new SQLFilter { PropertyName = "cdeCTIM", Operation = FilterOp.GreaterThan, Value = new DateTimeOffset(DateTime.Now.Subtract(new TimeSpan(0, 1, 0))) });
                            break;
                        case "LHO":
                            filter.Add(new SQLFilter { PropertyName = "cdeCTIM", Operation = FilterOp.GreaterThan, Value = new DateTimeOffset(DateTime.Now.Subtract(new TimeSpan(1, 0, 0))) });
                            break;
                        case "LH3":
                            filter.Add(new SQLFilter { PropertyName = "cdeCTIM", Operation = FilterOp.GreaterThan, Value = new DateTimeOffset(DateTime.Now.Subtract(new TimeSpan(3, 0, 0))) });
                            break;
                        case "LH6":
                            filter.Add(new SQLFilter { PropertyName = "cdeCTIM", Operation = FilterOp.GreaterThan, Value = new DateTimeOffset(DateTime.Now.Subtract(new TimeSpan(6, 0, 0))) });
                            break;
                        case "LHC":
                            filter.Add(new SQLFilter { PropertyName = "cdeCTIM", Operation = FilterOp.GreaterThan, Value = new DateTimeOffset(DateTime.Now.Subtract(new TimeSpan(12, 0, 0))) });
                            break;
                        case "LDA":
                            filter.Add(new SQLFilter { PropertyName = "cdeCTIM", Operation = FilterOp.GreaterThan, Value = new DateTimeOffset(DateTime.Now.Subtract(new TimeSpan(1, 0, 0, 0))) });
                            break;
                        case "LWE":
                            filter.Add(new SQLFilter { PropertyName = "cdeCTIM", Operation = FilterOp.GreaterThan, Value = new DateTimeOffset(DateTime.Now.Subtract(new TimeSpan(7, 0, 0, 0))) });
                            break;
                        case "LMO":
                            filter.Add(new SQLFilter { PropertyName = "cdeCTIM", Operation = FilterOp.GreaterThan, Value = new DateTimeOffset(DateTime.Now.Subtract(new TimeSpan(30, 0, 0, 0))) });
                            break;
                        case "LYE":
                            filter.Add(new SQLFilter { PropertyName = "cdeCTIM", Operation = FilterOp.GreaterThan, Value = new DateTimeOffset(DateTime.Now.Subtract(new TimeSpan(365, 0, 0, 0))) });
                            break;
                        default:
                            if (tf.StartsWith("LSP") && tf.Length > 3)
                            {
                                int t = TheCommonUtils.CInt(tf.Substring(3));
                                if (t != 0)
                                    filter.Add(new SQLFilter { PropertyName = "cdeCTIM", Operation = FilterOp.GreaterThan, Value = new DateTimeOffset(DateTime.Now.Subtract(new TimeSpan(0, 0, t))) });
                            }
                            else
                            {
                                string pValue = ThePropertyBag.PropBagGetValue(tf, "=").Trim();
                                if (pValue.StartsWith("'"))
                                    pValue = pValue.Substring(1);
                                if (pValue.EndsWith("'"))
                                    pValue = pValue.Substring(0, pValue.Length - 1);
                                Guid pValueGuid = TheCommonUtils.CGuid(pValue);
                                if (pValueGuid != Guid.Empty)
                                    filter.Add(new SQLFilter { PropertyName = ThePropertyBag.PropBagGetName(tf, "="), Operation = FilterOp.Equals, Value = pValueGuid });
                                else
                                    filter.Add(new SQLFilter { PropertyName = ThePropertyBag.PropBagGetName(tf, "="), Operation = FilterOp.Equals, Value = pValue });
                            }
                            break;
                    }
                }
                catch
                {
                    // ignored
                }
            }
            return filter;
        }

        /// <summary>
        /// Validates a 'GROUP BY' SQL clause and returns a fixed clause that correlates
        /// with the column filter if possible.  If invalid, returns an empty string.
        /// </summary>
        /// <param name="pGRP">The 'GROUP BY' clause</param>
        /// <param name="pCFI">The column filter used in the query</param>
        /// <param name="pUID">The UID of the store</param>
        /// <param name="pAggregateFunctions">Aggregate function titles available for the DBMS (e.g. 'SUM', 'AVG', etc.)</param>
        /// <returns></returns>
        public static string ValidateGroupByClause(string pGRP, string pCFI, string pUID, string[] pAggregateFunctions)
        {
            string newClause = "";
            string[] sepGroupCols = TheCommonUtils.cdeSplit(pGRP, ",", false, true);
            string thingStoreUID = GenerateUniqueIDFromType(typeof(TheThingStore), null);
            for (int i = 0; i < sepGroupCols.Length; i++)
            {
                sepGroupCols[i] = sepGroupCols[i].Trim();
                if (pUID.StartsWith(thingStoreUID))
                {
                    if (sepGroupCols[i].StartsWith("PB."))
                        newClause += sepGroupCols[i].Substring(sepGroupCols[i].IndexOf('.') + 1);
                    else
                        newClause += sepGroupCols[i];
                }
                // Add any necessary checks for other types here
                else
                    newClause += sepGroupCols[i];
                if (i != sepGroupCols.Length - 1)
                    newClause += ", ";
            }
            string[] sepSelectCols = TheCommonUtils.cdeSplit(pCFI, ",", false, true);
            for (int i = 0; i < sepSelectCols.Length; i++)
            {
                // "Select" column filter clause must either contain columns in the "Group by" clause
                // or aggregate functions
                sepSelectCols[i] = sepSelectCols[i].Trim();
                if (!sepGroupCols.Contains(sepSelectCols[i]))
                {
                    if (!pAggregateFunctions.Any(sepSelectCols[i].ToUpper().StartsWith))
                    {
                        newClause = "";
                        break;
                    }
                }
            }
            if (!string.IsNullOrEmpty(newClause))
                newClause = "group by " + newClause;
            return newClause;
        }
    }
}
