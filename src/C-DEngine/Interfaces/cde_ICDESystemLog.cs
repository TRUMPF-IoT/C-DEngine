// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;

namespace nsCDEngine.Interfaces
{
    /// <summary>
    /// Defines the interface to the System Log
    /// </summary>
    public interface ICDESystemLog
    {
        /// <summary>
        /// Writes to the log with a given LogID
        /// </summary>
        /// <param name="LogLevel">Only if current DebugLevel is lower the messge will be logged</param>
        /// <param name="LogID">LogID</param>
        /// <param name="pTopic">Topic or Engine to be logged</param>
        /// <param name="pLogText">Message to be logged</param>
        /// <param name="pSeverity">Message severity</param>
        /// <param name="NoLog">If true, the message will not be logged in files to prevent log overrun</param>
        void WriteToLog(eDEBUG_LEVELS LogLevel, int LogID, string pTopic, string pLogText,eMsgLevel pSeverity = eMsgLevel.l4_Message, bool NoLog = false);
    }
}
