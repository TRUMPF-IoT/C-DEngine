// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using DataLogUtilities;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;

namespace DataLogVerifier
{
    class Program
    {
        static void Main(string[] args)
        {
            var opcLogFile = "opcclientdata.log";
            var meshSenderLogFile = "meshsenderdata.log";
            var outputPath = ".";

            if (args.Length > 0)
            {
                if (args[0] == "/?" || args[0] == "-?" || args[0] == "-help")
                {
                    Console.WriteLine("Usage: DataLogVerifier [<opcdatalog file> [<meshsenderlog file> [<output path> ]]]");
                    Console.WriteLine($"Default: {opcLogFile} {meshSenderLogFile} {outputPath}");
                    return;
                }
            }

            if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                opcLogFile = args[0];
            }
            if (args.Length > 1 && !string.IsNullOrEmpty(args[1]))
            {
                meshSenderLogFile = args[1];
            }
            if (args.Length > 2 && !string.IsNullOrEmpty(args[2]))
            {
                outputPath = args[2];
            }

            DataLogComparer.VerifierStats stats = null;
            try
            {
                var tzOffset = new TimeSpan(0, 0, 0);
                TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo();
                stats = DataLogComparer.CompareDataLogs<TheThingStore>(opcLogFile, meshSenderLogFile, outputPath, DataLogParser.ConvertTDS01MeshSenderDataToOpcTags, tzOffset, 0, null);
            }
            catch (Exception e)
            {
                Console.WriteLine($"ERROR: {e.ToString()}");
            }
            if (stats != null)
            {
                Console.WriteLine($"Found {stats.duplicateOPCTagCount} duplicate opc tags" + (stats.duplicateOPCTagCount > 0 ? $" => {stats.outputPathPrefix}opcduplicate.log." : "."));
                Console.WriteLine($"Found {stats.duplicateMeshTagCount} duplicate mesh tags" + (stats.duplicateMeshTagCount > 0 ? $" => {stats.outputPathPrefix}duplicatemesh.log." : "."));
                Console.WriteLine($"Found {stats.missingOpcTags} missing OPC tags" + (stats.missingOpcTags > 0 ? $" => {stats.outputPathPrefix}missingopc.log." : "."));
                Console.WriteLine($"Found {stats.extraMeshTags} unexpected mesh tags" + (stats.extraMeshTags > 0 ? $" => {stats.outputPathPrefix}extramesh.log." : "."));
                Console.WriteLine($"Found {stats.opc35RunningOutOfSequence} '35.Running' OPC tags out of sequence" + (stats.opc35RunningOutOfSequence > 0 ? $" => {stats.outputPathPrefix}opc35runningoutofsequence.log." : "."));
            }
        }
    }
}
