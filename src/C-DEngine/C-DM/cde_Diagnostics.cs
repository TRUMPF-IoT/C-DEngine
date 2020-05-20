// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Diagnostics;
using System.Collections.Generic;

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;

namespace nsCDEngine.ISM
{
    /// <summary>
    ///    Summary description for NKDiagnostics.
    /// </summary>
    public static class TheDiagnostics
    {
        private static bool? EnableThreadDiag=null;
        private const int MAX_IDS = 20000;
        private static int ThreadID = 0;
        internal static void InitDiagnostics()
        {
            MyThreadNames = new string[MAX_IDS];
            MyThreadStacks = new System.Threading.Thread[MAX_IDS];
        }

        private static System.Threading.Thread[] MyThreadStacks = null;
        private static string[] MyThreadNames = null;

        /// <summary>
        /// Sets the Thread Name for debugging
        /// </summary>
        /// <param name="tName">Friendly name for the Thread</param>
        public static void SetThreadName(string tName)
        {
            SetThreadName(tName,false);
        }
        /// <summary>
        /// Sets the Thread Name for debugging
        /// </summary>
        /// <param name="tName">Friendly name for the Thread</param>
        /// <param name="IsBackGround">If true, this thread is a designated background thread</param>
        public static void SetThreadName(string tName,bool IsBackGround)
        {
            if (EnableThreadDiag == null)
                EnableThreadDiag = TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("EnableThreadDiagnostics"));
            if (EnableThreadDiag==true)
            {
                if (IsBackGround)
                    System.Threading.Thread.CurrentThread.IsBackground = IsBackGround;
                if (TheBaseAssets.MyServiceHostInfo.DebugLevel > eDEBUG_LEVELS.ESSENTIALS)
                {
#if !CDE_STANDARD
                    ThreadID = NativeMethods.GetCurrentThreadId();
#else
                    System.Threading.Interlocked.Increment(ref ThreadID);
#endif
                    string t = System.Threading.Thread.CurrentThread.Name;
                    if (string.IsNullOrEmpty(t))
                    {
                        t = "cde" + tName;
                        System.Threading.Thread.CurrentThread.Name = t;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(tName) && !string.IsNullOrEmpty(t)) t += ":cde" + tName;
                    }
                    if (MyThreadNames != null && ThreadID < MAX_IDS)
                    {
                        MyThreadNames[ThreadID] = t;
                        MyThreadStacks[ThreadID] = System.Threading.Thread.CurrentThread; // TheCommonUtils.GetStackInfo(new System.Diagnostics.StackTrace(true));
                    }
                }
            }
        }

        internal static List<TheThreadInfo> GetThreadInfo()
        {
            List<TheThreadInfo> tList = new List<TheThreadInfo>();
#if CDE_STANDARD //No Thread Name Diagnostics
#else
            try
            {
                int cid = NativeMethods.GetCurrentThreadId();
                foreach (ProcessThread pt in Process.GetCurrentProcess().Threads)
                {
                    TheThreadInfo t = new TheThreadInfo()
                    {
                        Priority = PriorityToString(pt.CurrentPriority),
                        ID = pt.Id
                    };
                    if (t.ID < MAX_IDS)
                    {
                        t.Name = MyThreadNames[t.ID];
                        //t.StackFrame = MyThreadStacks[t.ID];
                        if (t.ID == cid)
                        {
                            t.Name = "GETTHREADINFO";
                        }
                        else
                        {
                            if (MyThreadStacks[t.ID] != null)
                            {
                                t.IsBackground = MyThreadStacks[t.ID].IsBackground;
                                t.IsPooled = MyThreadStacks[t.ID].IsThreadPoolThread;
#pragma warning disable CS0618 // System.Diagnostics.StrackTrace requires the thread to be suspended
                                MyThreadStacks[t.ID].Suspend();
                                t.StackFrame = TheCommonUtils.GetStackInfo(new System.Diagnostics.StackTrace(MyThreadStacks[t.ID], true));
                                MyThreadStacks[t.ID].Resume();
#pragma warning restore CS0618
                            }
                        }
                    }
                    t.CoreTimeInMs = pt.PrivilegedProcessorTime.TotalMilliseconds;
                    t.State = ThreadStateToString(pt.ThreadState);
                    t.UserTimeInMs = pt.UserProcessorTime.TotalMilliseconds;
                    if (ThreadState.Wait != pt.ThreadState)
                        t.WaitReason = "Not Waiting";
                    else
                        t.WaitReason = ThreadWaitReasonToString(pt.WaitReason);
                    t.StartTime = pt.StartTime;
                    tList.Add(t);
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(999, new TSM("Diagnostics", "Thread Diagnostics Failed", eMsgLevel.l1_Error, e.ToString()));
            }
#endif
            return tList;
        }

        /// <summary>
        /// Method to return array of names of the procedures that are loaded by the
        /// process corresponding to given Process ID.
        /// </summary>
        /// <param name="nProcID"> </param>
        internal static List<TheModuleInfo> GetLoadedModules (int nProcID)
        {
            List<TheModuleInfo> tList = new List<TheModuleInfo>();
            try
            {
                Process proc = null;
                if (nProcID == 0)
                    proc = Process.GetCurrentProcess();
                else
                    proc=Process.GetProcessById(nProcID);
                ProcessModuleCollection modules = proc.Modules;
                int nCount = modules.Count;
                if (nCount > 0)
                {
                    for (int i = 0; i < nCount; i++)
                    {
                        TheModuleInfo t = new TheModuleInfo()
                        {
                            Name = modules[i].ModuleName,
                            MemorySize = modules[i].ModuleMemorySize
                        };
                        tList.Add(t);
                    }
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(999, new TSM("Diagnostics", "Diag Modules Failed", eMsgLevel.l1_Error, e.ToString()));
            }
            return tList;
        }

        /// <summary>
        /// Method returns an array containg the threads that a process is running.
        /// </summary>
        /// <param name="nProcID"> </param>
        public static ProcessThreadCollection GetProcessThreads(int nProcID)
        {
            try
            {
                Process proc = Process.GetProcessById (nProcID);
                ProcessThreadCollection threads = proc.Threads;
                return threads;
            }
            catch ( Exception)
            {
                //TheSystemMessageLog.ToCo(e.Message);
                return null;
            }
        }
        /// <summary>
        /// Method translates ThreadState enumerator value into a String value.
        /// </summary>
        /// <param name="state">ThreadState value that needs to be translated. </param>
        public static String ThreadStateToString (ThreadState state)
        {
            switch (state)
            {
                case ThreadState.Initialized:
                    return "Initialized";
                case ThreadState.Ready:
                    return "Ready";
                case ThreadState.Running:
                    return "Running";
                case ThreadState.Standby:
                    return "StandBy";
                case ThreadState.Terminated:
                    return "Terminated";
                case ThreadState.Transition:
                    return "Transition";
                case ThreadState.Wait:
                    return "Waiting";
                case ThreadState.Unknown:
                default:
                    return "Uknown";
            }
        }

        /// <summary>
        /// Method translates ThreadWaitReason enumerator value into a String value.
        /// </summary>
        /// <param name="reason">ThreadWaitReason value that needs to be translated. </param>
        public static String ThreadWaitReasonToString (ThreadWaitReason reason)
        {
            switch (reason)
            {
                case ThreadWaitReason.EventPairHigh:
                    return "EventPairHigh";
                case ThreadWaitReason.EventPairLow:
                    return "EventPairLow";
                case ThreadWaitReason.ExecutionDelay:
                    return "Execution Delay";
                case ThreadWaitReason.Executive:
                    return "Executive";
                case ThreadWaitReason.FreePage:
                    return "FreePage";
                case ThreadWaitReason.LpcReceive:
                    return "LPC Recieve";
                case ThreadWaitReason.LpcReply:
                    return "LPC Reply";
                case ThreadWaitReason.PageIn:
                    return "Page In";
                case ThreadWaitReason.PageOut:
                    return "Page Out";
                case ThreadWaitReason.Suspended:
                    return "Suspended";
                case ThreadWaitReason.SystemAllocation:
                    return "System Allocation";
                case ThreadWaitReason.UserRequest:
                    return "User Request";
                case ThreadWaitReason.VirtualMemory:
                    return "Virtual Memory";
                case ThreadWaitReason.Unknown:
                default:
                    return "Unknown";

            }
        }
        /// <summary>
        /// Method translates thread priority level enumerator value into a String value.
        /// </summary>
        /// <param name="level">Thread priority value that needs to be translated. </param>
        public static String PriorityToString(int level)
        {
            switch (level)
            {
                case 8:
                    return "Normal";
                case 13:
                    return "High";
                case 24:
                    return "Real Time";
                case 4:
                default:
                    return "Idle";
            }
        }
    }
}
