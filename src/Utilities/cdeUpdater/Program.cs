// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using CDEngine.CDUtils.Zlib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace cdeUpdater
{
    //test with: "C:\CMyHomeRelay\CMyHome-Relay V1.5002.CDEX" "C:\CMyHomeRelay" "CMyHomeRelay" 2
    //New/Updated Plugins with: "C:\CMyHomeRelay\C-DMyEnergy V1.5002.CDEP" "C:\CMyHomeRelay" "CMyHomeRelay" 1
    //Just content with "C:\CMyHomeRelay\ClientBin.CDEC" "C:\CMyHomeRelay" "CMyHomeRelay" 0
    class Program
    {
        static void Main(string[] args)
        {

            try
            {
                if (File.Exists("cdeenablestartuplog"))
                {
                    bStartupLog = true;
                    string[] lines = File.ReadAllLines("cdeenablestartuplog");
                    if (lines.Length > 0)
                    {
                        var path = lines[0];
                        if (!string.IsNullOrEmpty(path))
                        {
                            startupLogPath = path;
                        }
                    }
                }
            }
            catch { 
                //ignored
            }

            foreach (string t in args)
                StartupLog("Arg " + t);
            if (args.Length < 3)
                return;

            var target = args[0];
            var processDirectory = args[1];
            string processName = args[2];
            int processPid;
            if (args[2].Contains(":"))
            {
                var t = args[2].Split(':');
                processPid = CInt(t[0]);
                if (t.Length > 1)
                {
                    processName = t[1];
                }
            }
            else
            {
                processPid = 0;
            }
            cdeHostType HostType = 0;
            if (args.Length > 3)
                HostType = CHostType(args[3]);

            StartupLog($"Upgrading to \"{target}\" of {processName} / pid {processPid} in directory {processDirectory}");
            try
            {
                switch (target.ToUpper())
                {
                    case "RESTART":
                        switch (HostType)
                        {
                            case cdeHostType.Service:
                                StartStopService(false, processName, processDirectory, processPid);
                                StartStopService(true, processName, processDirectory, processPid);
                                break;
                            case cdeHostType.IIS:
                                StopAppPool("C-DEngine", true);
                                break;
                            default:
                                if (IsMainProcessRunning(processName, processDirectory, processPid))
                                    KillProcess(processName, processDirectory, processPid);
                                StartProcess(processName, processDirectory);
                                break;
                        }
                        return;
                    case "WIPENODE":
                        switch (HostType)
                        {
                            case cdeHostType.Service:
                                StartStopService(false, processName, processDirectory, processPid);
                                break;
                            case cdeHostType.IIS:
                                StopAppPool("C-DEngine", false);
                                break;
                            default:
                                if (IsMainProcessRunning(processName, processDirectory, processPid))
                                    KillProcess(processName, processDirectory, processPid);
                                break;
                        }
                        //HEFRE WIPE
                        try
                        {
                            StartupLog($"Wiping Node: {processDirectory}/ClientBin");
                            Directory.Delete($"{processDirectory}/ClientBin", true);
#if !CDE_NET35
                            StartupLog($"Wiping Plugins from {processDirectory}");
                            var dir = new DirectoryInfo(processDirectory);
                            foreach (var file in dir.EnumerateFiles("CDMy*.dll"))
                            {
                                file.Delete();
                            }
                            foreach (var file in dir.EnumerateFiles("C-DMy*.dll"))
                            {
                                file.Delete();
                            }
#endif
                        }
                        catch (Exception)
                        {
                            StartupLog($"WipeNode Failed for {processDirectory}/ClientBin");
                        }
                        switch (HostType)
                        {
                            case cdeHostType.Service:
                                StartStopService(true, processName, processDirectory, processPid);
                                break;
                            case cdeHostType.IIS:
                                StopAppPool("C-DEngine", true);
                                break;
                            default:
                                StartProcess(processName, processDirectory);
                                break;
                        }
                        return;
                    case "STOPSVC":
                        StartStopService(false, processName, processDirectory, processPid);
                        return;
                    case "STARTSVC":
                        StartStopService(true, processName, processDirectory, processPid);
                        return;

                    case "START":
                        StartupLog($"Waiting to close {processName} / {processPid} in directory {processDirectory}");
                        while (IsMainProcessRunning(processName, processDirectory, processPid))
                        {
                            Thread.Sleep(5);
                        }
                        StartProcess(processName, processDirectory);
                        StartupLog($"Starting {processName}.exe in directory ${processDirectory}");
                        return;
                    case "STOP":
                        if (!IsMainProcessRunning(processName, processDirectory, processPid)) return;
                        StartupLog($"Trying to kill {processName} / {processPid} in directory {processDirectory}");
                        KillProcess(processName, processDirectory, processPid);
                        return;
                }
                string[] NewFiles = cdeSplit(target, ";:;", true, true);
                List<string> MyNewFiles = new ();
                foreach (string tFile in NewFiles)
                {
                    if (!File.Exists(tFile))
                        StartupLog("Update File " + tFile + " was not found....");
                    else
                        MyNewFiles.Add(tFile);
                }
                if (MyNewFiles.Count==0)
                {
                    StartupLog("No fils found to update - exiting....");
                    return;
                }
                StartupLog("Waiting for " + processName + " to close....");
                if (args.Length > 3)
                {
                    HostType = CHostType(args[3]);
                    switch (HostType)
                    {
                        case cdeHostType.NOTSET:
                            break;
                        case cdeHostType.Service:
                            StartStopService(false, processName, processDirectory, processPid);
                            break;
                        case cdeHostType.IIS:
                            break;
                        default:
                            int tTimeOut = 15;
                            while (IsMainProcessRunning(processName, processDirectory, processPid))
                            {
                                StartupLog("waiting ... " + tTimeOut);
                                Thread.Sleep(1000);
                                tTimeOut--; if (tTimeOut == 0) break;
                            }
                            if (IsMainProcessRunning(processName, processDirectory, processPid))
                                KillProcess(processName, processDirectory, processPid);
                            break;
                    }
                }
                StartupLog($"App {processName} was closed....");

                foreach (string tFile in MyNewFiles)
                {
                    try
                    {
                        using (ZipFile zip = ZipFile.Read(tFile, new ReadOptions()))  //args[1] + "\\" +
                        {
                            if (tFile.ToUpper().EndsWith(".CDEX"))
                                zip.ExtractAll(args[1], ExtractExistingFileAction.OverwriteSilently);
                            else
                                zip.ExtractAll(args[1] + $"{Path.DirectorySeparatorChar}ClientBin", ExtractExistingFileAction.OverwriteSilently);
                        }
                        File.Delete(tFile + ".old");
                        File.Move(tFile, tFile + ".old");
                    }
                    catch (Exception ee)
                    {
                        StartupLog($"File: {tFile} failed to upgrade: {ee}");
                    }
                }
            }
            catch (Exception e)
            {
                StartupLog(e.ToString());
            }

            try
            {
                switch (HostType)
                {
                    case cdeHostType.NOTSET:
                        break;
                    case cdeHostType.Service:
                        StartStopService(true, processName, processDirectory, processPid);
                        break;
                    case cdeHostType.IIS:
                        break;
                    default:
                        StartProcess(processName, processDirectory);
                        break;
                }
            }
            catch { 
                //left blank
            }
            StartupLog("Updater done.");
        }

        public static string[] cdeSplit(string pToSplit, string pSeparator, bool RemoveDuplicates, bool RemoveEmtpyEntries)
        {
            if (string.IsNullOrEmpty(pToSplit)) return new string[] { pToSplit };
            if (RemoveDuplicates)
            {
                List<string> tList = new ();
                int tPos;
                int oldPos = 0;
                do
                {
                    tPos = pToSplit.IndexOf(pSeparator, oldPos);
                    if (tPos < 0) tPos = pToSplit.Length;
                    string tStr = pToSplit.Substring(oldPos, tPos - oldPos);
                    if ((!RemoveDuplicates || !tList.Contains(tStr)) && (!RemoveEmtpyEntries || !string.IsNullOrEmpty(tStr)))
                        tList.Add(tStr);
                    oldPos = tPos + pSeparator.Length;
                } while (tPos >= 0 && oldPos < pToSplit.Length);
                return tList.ToArray();
            }
            else
            {
                StringSplitOptions tOpt = StringSplitOptions.None;
                if (RemoveEmtpyEntries)
                    tOpt = StringSplitOptions.RemoveEmptyEntries;
                return pToSplit.Split(new string[] { pSeparator }, tOpt);
            }
        }

        private static void StartProcess(string pName, string pDire)
        {
            using (Process mainProcess = new ())
            {
                mainProcess.StartInfo.FileName = pName + ".exe";
                mainProcess.StartInfo.WorkingDirectory = pDire;
                mainProcess.StartInfo.Arguments = "";
                mainProcess.Start();
            }
        }

        private static void KillProcess(string processName, string processDirectory, int processPid)
        {

            var filePath = processDirectory != null ? Path.Combine(processDirectory, processName).ToUpperInvariant() : processName.ToUpperInvariant();


            foreach (Process clsProcess in Process.GetProcesses())
            {
                if (processPid > 0)
                {
                    if (processPid == clsProcess.Id)
                    {
                        clsProcess.Kill();
                        StartupLog($"Kill sent for {processName} / {processPid}");
                    }
                }
                else
                {
                    try
                    {
                        if ((clsProcess.MainModule.FileName.ToUpperInvariant().Contains(filePath)))
                        {
                            clsProcess.Kill();
                            StartupLog($"Kill send for {processName} in {processDirectory}");
                        }
                    }
                    catch { 
                        //ignored
                    }
                }
            }
        }

        private static void StopAppPool(string pPoolName, bool DoRestart)
        {
            try
            {
                System.Management.ManagementScope scope = new ("root\\MicrosoftIISv2");
                scope.Connect();
                System.Management.ManagementObject appPool = new (scope, new System.Management.ManagementPath("IIsApplicationPool.Name='W3SVC/AppPools/" + pPoolName + "'"), null);
                if (DoRestart)
                    appPool?.InvokeMethod("Recycle", null, null);
                else
                    appPool?.InvokeMethod("Stop", null, null);
            }
            catch (Exception ee)
            {
                StartupLog($"IIS Shutdown problem : {ee}");
            }
        }

        private static void StartStopService(bool StartService, string processName, string processDirectory, int processPid)
        {
            ServiceController service = new (processName);
            try
            {
                if (StartService)
                {
                    TimeSpan timeout = TimeSpan.FromSeconds(60);
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.StartPending, timeout);
                }
                else
                {
                    TimeSpan timeout = TimeSpan.FromSeconds(100);
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                    StartupLog($"App {processName} running: {IsMainProcessRunning(processName, processDirectory, processPid)}");
                    Thread.Sleep(3000);
                    if (IsMainProcessRunning(processName, processDirectory, processPid))
                        KillProcess(processName, processDirectory, processPid);
                }
            }
            catch
            {
                //ignored
            }
        }

        static private bool IsMainProcessRunning(string pMainExecutable, string processDirectory, int processPid)
        {
            if (string.IsNullOrEmpty(pMainExecutable)) return false;
            var filePath = processDirectory != null ? Path.Combine(processDirectory, pMainExecutable).ToUpperInvariant() : pMainExecutable.ToUpperInvariant();

            foreach (Process clsProcess in Process.GetProcesses())
            {
                if (processPid > 0)
                {
                    if (processPid == clsProcess.Id)
                        return true;
                }
                else
                {
                    try
                    {
                        if ((clsProcess.MainModule.FileName.ToUpperInvariant().Contains(filePath)))
                        {
                            return true;
                        }
                    }
                    catch { 
                        //ignored
                    }
                }
            }
            return false;
        }

        #region DeviceTimeSetter
        public static DateTime CDate(string instr)
        {
            DateTime ret = System.DateTime.MinValue;
            if (instr.Length == 0) return ret;
            try
            {
                ret = System.DateTime.Parse(instr, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                //ignored
            }
            return ret;
        }

        public struct SystemTime
        {
            public ushort Year;
            public ushort Month;
            public ushort DayOfWeek;
            public ushort Day;
            public ushort Hour;
            public ushort Minute;
            public ushort Second;
            public ushort Millisecond;
        };
        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "SetSystemTime", SetLastError = true)]
        public extern static bool Win32SetSystemTime(ref SystemTime sysTime);

        public static void SetSystemTime(DateTime pDate)
        {
            SystemTime updatedTime = new ()
            {
                Year = (ushort)pDate.Year,
                Month = (ushort)pDate.Month,
                Day = (ushort)pDate.Day,
                // UTC time; it will be modified according to the regional settings of the target computer so the actual hour might differ
                Hour = (ushort)pDate.Hour,
                Minute = (ushort)pDate.Minute,
                Second = (ushort)0
            };
            // Call the unmanaged function that sets the new date and time instantly
            Win32SetSystemTime(ref updatedTime);
        }
        #endregion

        private static cdeHostType CHostType(object inObj)
        {
            return (cdeHostType)CInt(inObj);
        }
        /// <summary>
        /// Tuned
        /// </summary>
        /// <param name="inObj"></param>
        /// <returns></returns>
        private static int CInt(object inObj)
        {
            if (inObj == null || inObj.ToString().Length == 0) return 0;
            int retVal;
            try
            {
                if (inObj is int i1) return i1;
                retVal = Convert.ToInt32(inObj);
            }
            catch (Exception)
            {
                retVal = 0;
            }
            return retVal;
        }

        static string startupLogPath = "startup.log";
        static bool bStartupLog = false;
        static void StartupLog(string text)
        {
            Console.WriteLine(text);
            if (bStartupLog)
            {
                try
                {
                    File.AppendAllText(startupLogPath, String.Format("{0}: {1}\r\n", DateTime.Now, text));
                }
                catch { 
                    //ignored
                }
            }
        }
        /// <summary>
        /// The cdeHostType is defining what kind of hosting application is used for the C-DEngine
        /// </summary>
        public enum cdeHostType
        {
            /// <summary>
            /// Not set, yet
            /// </summary>
            NOTSET = 0,
            /// <summary>
            /// C-DEngine runs inside an Application (.EXE)
            /// Node can relay information and has WebServer + Outbound (cdeHostType.Relay)
            /// </summary>
            Application = 1,
            /// <summary>
            /// C-DEngine runs inside a Windows Service
            /// Node can relay information and has WebServer + Outbound (cdeHostType.Relay)
            /// </summary>
            Service = 2,
            /// <summary>
            /// C-DEngine runs inside IIS either on a server or in the cloud
            /// Node can relay information and has WebServer + Outbound (cdeHostType.Relay)
            /// </summary>
            IIS = 3,
            /// <summary>
            /// C-DEngine runs inside a Application that runs on a device
            /// The device can only to outbound (has no WebServer) = (cdeHostType.Active)
            /// </summary>
            Device = 4,
            /// <summary>
            /// C-DEngine runs inside a browser (uses the Javascript version of the C-DEngine)
            /// The device can only to outbound (has no WebServer) = (cdeHostType.Active)
            /// </summary>
            Browser = 5,
            /// <summary>
            /// C-DEngine runs inside Silverlight   RETIRED - no more Silverlight support
            /// The device can only to outbound (has no WebServer) = (cdeHostType.Active)
            /// </summary>
            Silverlight = 6,
            /// <summary>
            /// C-DEngine runs inside a Phone (Windows Phone, Android or iOS)
            /// The device can only to outbound (has no WebServer) = (cdeHostType.Active)
            /// </summary>
            Phone = 7,
            /// <summary>
            /// C-DEngine runs inside Windows 8 RT (RETIRED: Use Device instead)
            /// The device can only to outbound (has no WebServer) = (cdeHostType.Active)
            /// </summary>
            Metro = 8,

            /// <summary>
            /// C-DEngine runs inside a small Device (i.e. the Phoenix-Contact eCLR Runtime)
            /// The device has only a mini WebServer - no outbound (cdeHostType.Passive)
            /// </summary>
            Mini = 9
        }


    }
}
