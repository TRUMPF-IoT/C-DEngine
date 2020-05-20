// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿#if CDE_NET35 || CDE_NET4 || CDE_NET45
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace nsCDEngine.BaseClasses
{
    internal static class NativeMethods
    {
        [DllImport("kernel32")]
        internal static extern int GetCurrentThreadId();

        // [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_BASIC_INFORMATION
        {
            public int ExitStatus;
            public int PebBaseAddress;
            public int AffinityMask;
            public int BasePriority;
            public uint UniqueProcessId;
            public uint InheritedFromUniqueProcessId;
        }
        [DllImport("kernel32.dll")]
        internal static extern bool TerminateProcess(IntPtr hProcess, int exitCode);
        [DllImport("ntdll.dll")]
        internal static extern int NtQueryInformationProcess(
           IntPtr hProcess,
           int processInformationClass /* 0 */,
           ref PROCESS_BASIC_INFORMATION processBasicInformation,
           uint processInformationLength,
           out uint returnLength
        );

        /// <summary>
        /// Terminate a process tree
        /// </summary>
        /// <param name="hProcess">The handle of the process</param>
        /// <param name="processID">The ID of the process</param>
        /// <param name="exitCode">The exit code of the process</param>
        internal static void TerminateProcessTree(IntPtr hProcess, uint processID, int exitCode)
        {
            // Retrieve all processes on the system
            Process[] processes = Process.GetProcesses();
            foreach (Process p in processes)
            {    // Get some basic information about the process
                PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
                try
                {
                    uint bytesWritten;
                    NtQueryInformationProcess(p.Handle, 0, ref pbi, (uint)Marshal.SizeOf(pbi), out bytesWritten); // == 0 is OK
                    // Is it a child process of the process we're trying to terminate?
                    if (pbi.InheritedFromUniqueProcessId == processID)
                        // The terminate the child process and its child processes
                        TerminateProcessTree(p.Handle, pbi.UniqueProcessId, exitCode);
                }
                catch (Exception /* ex */)
                {
                    // Ignore, most likely 'Access Denied'
                }
            }   // Finally, termine the process itself:
            TerminateProcess(hProcess, exitCode);
        }

        /// <summary>
        /// Delegate definition for the API callback
        /// </summary>
        internal delegate void TimerCallback(uint uTimerID, uint uMsg, UIntPtr dwUser, UIntPtr dw1, UIntPtr dw2);

        //Lib API declarations
        /// <summary>
        /// Times the set event.
        /// </summary>
        /// <param name="uDelay">The u delay.</param>
        /// <param name="uResolution">The u resolution.</param>
        /// <param name="lpTimeProc">The lp time proc.</param>
        /// <param name="dwUser">The dw user.</param>
        /// <param name="fuEvent">The fu event.</param>
        /// <returns></returns>
        [DllImport("Winmm.dll", CharSet = CharSet.Auto)]
        internal static extern uint timeSetEvent(uint uDelay, uint uResolution,
                      TimerCallback lpTimeProc, UIntPtr dwUser, uint fuEvent);

        /// <summary>
        /// Times the kill event.
        /// </summary>
        /// <param name="uTimerID">The u timer ID.</param>
        /// <returns></returns>
        [DllImport("Winmm.dll", CharSet = CharSet.Auto)]
        internal static extern uint timeKillEvent(uint uTimerID);

        /// <summary>
        /// Times the get time.
        /// </summary>
        /// <returns></returns>
        [DllImport("Winmm.dll", CharSet = CharSet.Auto)]
        internal static extern uint timeGetTime();

        /// <summary>
        /// Times the begin period.
        /// </summary>
        /// <param name="uPeriod">The u period.</param>
        /// <returns></returns>
        [DllImport("Winmm.dll", CharSet = CharSet.Auto)]
        internal static extern uint timeBeginPeriod(uint uPeriod);

        /// <summary>
        /// Times the end period.
        /// </summary>
        /// <param name="uPeriod">The u period.</param>
        /// <returns></returns>
        [DllImport("Winmm.dll", CharSet = CharSet.Auto)]
        internal static extern uint timeEndPeriod(uint uPeriod);

        [DllImport("Kernel32.dll")]
        internal static extern bool QueryPerformanceCounter(
            out long lpPerformanceCount);

        [DllImport("Kernel32.dll")]
        internal static extern bool QueryPerformanceFrequency(
            out long lpFrequency);

        #region Win32 TimerQueueTimer Functions
        internal delegate void WaitOrTimerDelegate(IntPtr lpParameter, bool timerOrWaitFired);

        [DllImport("kernel32.dll")]
        internal static extern bool CreateTimerQueueTimer(
            out IntPtr phNewTimer,          // phNewTimer - Pointer to a handle; this is an out value
            IntPtr TimerQueue,              // TimerQueue - Timer queue handle. For the default timer queue, NULL
            WaitOrTimerDelegate Callback,   // Callback - Pointer to the callback function
            IntPtr Parameter,               // Parameter - Value passed to the callback function
            uint DueTime,                   // DueTime - Time (milliseconds), before the timer is set to the signaled state for the first time
            uint Period,                    // Period - Timer period (milliseconds). If zero, timer is signaled only once
            uint Flags                      // Flags - One or more of the next values (table taken from MSDN):
            // WT_EXECUTEINTIMERTHREAD 	The callback function is invoked by the timer thread itself. This flag should be used only for short tasks or it could affect other timer operations.
            // WT_EXECUTEINIOTHREAD 	The callback function is queued to an I/O worker thread. This flag should be used if the function should be executed in a thread that waits in an alertable state.

                                            // The callback function is queued as an APC. Be sure to address reentrancy issues if the function performs an alertable wait operation.
            // WT_EXECUTEINPERSISTENTTHREAD 	The callback function is queued to a thread that never terminates. This flag should be used only for short tasks or it could affect other timer operations.

                                            // Note that currently no worker thread is persistent, although no worker thread will terminate if there are any pending I/O requests.
            // WT_EXECUTELONGFUNCTION 	Specifies that the callback function can perform a long wait. This flag helps the system to decide if it should create a new thread.
            // WT_EXECUTEONLYONCE 	The timer will be set to the signaled state only once.
            );

        [DllImport("kernel32.dll")]
        internal static extern bool DeleteTimerQueueTimer(
            IntPtr timerQueue,              // TimerQueue - A handle to the (default) timer queue
            IntPtr timer,                   // Timer - A handle to the timer
            IntPtr completionEvent          // CompletionEvent - A handle to an optional event to be signaled when the function is successful and all callback functions have completed. Can be NULL.
            );


        [DllImport("kernel32.dll")]
        internal static extern bool DeleteTimerQueue(IntPtr TimerQueue);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);

        #endregion
    }
}
#endif
