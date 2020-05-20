// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DataLogUtilities
{
    public class OpcClientDataLogEntry
    {
        public DateTimeOffset ReceiveTime { get; set; }
        public string TagId { get; set; }
        public long? Type { get; set; }
        public object value { get; set; }
        public long Status { get; set; }
        public long? SequenceNumber { get; set; }
        public DateTime Source { get; set; }
        public DateTime Server { get; set; }
        public DateTimeOffset PropertyTime { get; set; }
        public long? MonitoredItem { get; set; }
        public override string ToString()
        {
            return $"{TagId} {Source:yyyy/MM/dd HH:mm:ss.fffffff} '{value}'".ToString(CultureInfo.InvariantCulture);
        }
    }

    class CompareOpcTagsP08 : IEqualityComparer<OpcClientDataLogEntry>
    {
        long _toleranceInTicks;
        public CompareOpcTagsP08(long toleranceInTicks = 0)
        {
            _toleranceInTicks = toleranceInTicks;
        }
        public bool Equals(OpcClientDataLogEntry x, OpcClientDataLogEntry y)
        {
            if (
                   x.TagId == y.TagId
                && (x.Source == y.Source
                    // || (Math.Abs((x.Source - y.Source).Ticks) <= _toleranceInTicks)
                    ))
            {
                var xValue = TheCommonUtils.CStr(x.value);
                var yValue = TheCommonUtils.CStr(y.value);
                if (xValue == yValue)
                {
                    return true;
                }
                if (yValue.Contains("http://opcfoundation.org/UA/2008/02/Types.xsd") || xValue.Contains("http://opcfoundation.org/UA/2008/02/Types.xsd"))
                {
                    return true;
                }
                if ((string.IsNullOrEmpty(xValue) && yValue.StartsWith("[")) || (string.IsNullOrEmpty(yValue) && xValue.StartsWith("[")))
                {
                    return true;
                }
                if ((xValue == "(null)" && string.IsNullOrEmpty(yValue)) || (yValue == "(null)" && string.IsNullOrEmpty(xValue)))
                {
                    return true;
                }
            }
            if (x.TagId == $"[{y.TagId}].[statusCode]" && x.Source == y.Source)
            {
                if (TheCommonUtils.CStr(x.value) == TheCommonUtils.CStr(y.Status))
                {
                    return true;
                }
            }
            if (y.TagId == $"[{x.TagId}].[statusCode]" && x.Source == y.Source)
            {
                if (TheCommonUtils.CStr(y.value) == TheCommonUtils.CStr(x.Status))
                {
                    return true;
                }
            }
            if (x.TagId == $"[{y.TagId}].[sequenceNumber]" && x.Source == y.Source)
            {
                if (y.SequenceNumber == null || (TheCommonUtils.CStr(x.value) == TheCommonUtils.CStr(y.SequenceNumber.Value)))
                {
                    return true;
                }
            }
            if (y.TagId == $"[{x.TagId}].[sequenceNumber]" && x.Source == y.Source)
            {
                if (x.SequenceNumber == null || (TheCommonUtils.CStr(y.value) == TheCommonUtils.CStr(x.SequenceNumber.Value)))
                {
                    return true;
                }
            }

            return false;
        }

        public int GetHashCode(OpcClientDataLogEntry obj)
        {
            return obj.TagId.GetHashCode() + obj.Source.GetHashCode();
        }
    }

    // TODO Expose from OpcUAJsonEventConverter?
    public class JSonOpcArrayElement
    {
        public string machineid { get; set; }
        public string linkid { get; set; }
        public string @namespace { get; set; }
        public string identifier { get; set; }
        public long servertimestamp { get; set; }
        public long sourcetimestamp { get; set; }
        public object value { get; set; }
    }

    public class MeshSenderDataLogEntry<T>
    {
        public DateTimeOffset TimePublished { get; set; }
        public T PLS { get; set; }
    }

    public class DataLogParser
    {
        public static List<T> ReadDataLog<T>(string filePath)
        {
            var json = File.ReadAllText(filePath);
            json = "[" + json + "]";
            var entries = TheCommonUtils.DeserializeJSONStringToObject<List<T>>(json);
            return entries;
        }

        public static List<OpcClientDataLogEntry> ReadOpcClientDataLog(string filePath)
        {
            return ReadDataLog<OpcClientDataLogEntry>(filePath);
        }

        public static List<MeshSenderDataLogEntry<T>> ReadJsonOPCDataLog<T>(string filePath)
        {
            return ReadDataLog<MeshSenderDataLogEntry<T>>(filePath);
        }

        public static List<OpcClientDataLogEntry> ConvertMeshSenderDataToOpcTags(List<MeshSenderDataLogEntry<JSonOpcArrayElement[]>> meshEntries, TimeSpan offset)
        {
            var opcTags = new List<OpcClientDataLogEntry>();
            foreach (var entry in meshEntries)
            {
                foreach (var meshTag in entry.PLS.Where(e => e.identifier != "DeviceGateLog"))
                {
                    opcTags.Add(new OpcClientDataLogEntry
                    {
                        Source = new DateTime(meshTag.sourcetimestamp, DateTimeKind.Utc) - offset,
                        PropertyTime = new DateTimeOffset(meshTag.sourcetimestamp, offset),
                        Server = new DateTime(meshTag.servertimestamp, DateTimeKind.Utc) - offset,
                        TagId = meshTag.identifier,
                        value = meshTag.value,
                    });
                }
            }
            return opcTags;
        }
        public static List<OpcClientDataLogEntry> ConvertTDS01MeshSenderDataToOpcTags(List<MeshSenderDataLogEntry<TheThingStore>> meshEntries, TimeSpan offset)
        {
            var opcTags = new List<OpcClientDataLogEntry>();
            foreach (var entry in meshEntries)
            {
                foreach (var meshTag in entry.PLS.PB.Where(e =>
                    !e.Key.StartsWith("DeviceGate")
                    && !e.Key.StartsWith("MeshSender")
                    && !(e.Key == ("cdeDataNotReady"))
                    && !(e.Key == ("EngineName"))
                    && !(e.Key == ("ID"))
                    && !(e.Key == ("DeviceType"))
                    && !(e.Key == ("FriendlyName"))
                    ))
                {
                    opcTags.Add(new OpcClientDataLogEntry
                    {
                        Source = (entry.PLS.cdeCTIM - offset).UtcDateTime,
                        PropertyTime = entry.PLS.cdeCTIM,
                        Server = (entry.PLS.cdeCTIM - offset).UtcDateTime,
                        TagId = meshTag.Key,
                        value = meshTag.Value,
                    });
                }
            }
            return opcTags;
        }
    }
}
