// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using nsCDEngine.Engines;
using nsCDEngine.ViewModels;
using nsCDEngine.Engines.StorageService;
using System.IO;
using System.Collections.Concurrent;
using nsCDEngine.Interfaces;

namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// Structure for GELF logging
    /// </summary>
    public class TheGELFLogEntry
    {
        /// <summary>
        /// version
        /// </summary>
        public string version { get; set; }
        /// <summary>
        /// Host
        /// </summary>
        public string host { get; set; }
        /// <summary>
        /// SHort message of the log entry
        /// </summary>
        public string short_message { get; set; }
        /// <summary>
        /// Full message of the log entry
        /// </summary>
        public string full_message { get; set; }
        /// <summary>
        /// Timestamp when the event occured
        /// </summary>
        public double timestamp { get; set; }
        /// <summary>
        /// severity level of the event
        /// </summary>
        public int level
        {
            get { return mlevel; }
            set
            {
                _status_level = value;
                mlevel = value switch
                {
                    1 => 3,
                    2 => 4,
                    3 => 5,
                    4 => 6,
                    5 => 6,
                    6 => 7,
                    _ => value,
                };
            }
        }
        /// <summary>
        /// Log ID of the event
        /// </summary>
        public int _log_id { get; set; }

        int mlevel;

        /// <summary>
        /// CDE Status Level of the event
        /// </summary>
        public int _status_level { get; set; }

        /// <summary>
        /// Unique serial number of the event
        /// </summary>
        public long _serial_no { get; set; }
        /// <summary>
        /// TSM Topic (if applicable)
        /// </summary>
        public string _topic { get; set; }
        /// <summary>
        /// TSM Engine (if applicable)
        /// </summary>
        public string _engine { get; set; }
        /// <summary>
        /// Device ID (if applicable)
        /// </summary>
        public string _device_id { get; set; }
    }
    /// <summary>
    /// the class defining the entry to the SystemLog
    /// </summary>
    public class TheEventLogEntry : TheDataBase
    {
        /// <summary>
        /// The TSM containing the content of the Log Entry
        /// </summary>
        public TSM Message { get; set; }
        /// <summary>
        /// A string pointing at the source of the Log Entry
        /// </summary>
        public string Source { get; set; }
        /// <summary>
        /// If the event has an ID it will be in this integer
        /// </summary>
        public int EventID { get; set; }
        /// <summary>
        /// If the event has an ID it will be in this integer
        /// </summary>
        public long Serial { get; set; }
        /// <summary>
        /// Internal use only: This flag is set once the event entry was written to file
        /// </summary>
        public bool WasWritten { get; set; }
    }

    /// <summary>
    /// The main System Message Log of the C-DEngine for the current node
    /// All Log entries are written to this log
    /// </summary>
    public class TheSystemMessageLog : TheMetaDataBase,ICDESystemLog, IDisposable
    {
        /// <summary>
        /// Any message with a eMsgLevel lower than this filter will be displayed in the UX
        /// </summary>
        public eMsgLevel MessageFilterLevel = eMsgLevel.l4_Message;
        /// <summary>
        /// Any Message with a eMsgLevel lower than this will be written to the log
        /// </summary>
        public eMsgLevel LogFilterLevel = eMsgLevel.l3_ImportantMessage;
        /// <summary>
        /// Any Message with a eMsgLevel lower than this will be written to file (if the LogWriteBuffer is >0)
        /// </summary>
        public eMsgLevel LogWriteFilterLevel = eMsgLevel.l4_Message;
        /// <summary>
        /// Amount of messages to be buffered until written to disk. If set to zero - no messages are written to disk (which is the default)
        /// </summary>
        public int LogWriteBuffer;

        /// <summary>
        /// Amount of Files allowed in the directory
        /// </summary>
        public int MaxLogFiles = 0;

        private TSM LastError;
        /// <summary>
        /// Retrieves the last error written to the Log
        /// </summary>
        /// <returns>A TS</returns>
        public TSM GetLastError()
        {
            if (LastError == null)
                return new TSM("SystemLog", "No Error", "No Error");
            return LastError;
        }

        internal List<string> LogFilter = new ();
        internal List<string> LogIgnore = new ();
        internal List<string> LogOnly = new ();
        internal TheStorageMirror<TheEventLogEntry> MyMessageLog;
        private void Add(int pEventID, long pSerial, string pSource, TSM AMessage)
        {
            TheEventLogEntry tLog = new ()
            {
                Message = AMessage,
                Serial = pSerial,
                Source = string.IsNullOrEmpty(pSource) ? AMessage.ENG : pSource,
                EventID = pEventID
            };
            MyMessageLog.AddAnItem(tLog);
            if (IsEventRegistered("NewLogEntry"))
                FireEvent("NewLogEntry", new TheProcessMessage() { Message=AMessage, Cookie = tLog }, true);    //Prevents a call to AddScopeID for the TOPIC
        }
        internal int MaxMessageBirthBuffer = 100;
        bool UseSysLogQueue;

        internal TheSystemMessageLog()
        {
            if (UseSysLogQueue)
            {
                InitDequeue();
            }
        }

        internal TheSystemMessageLog(int MaxEntries, bool UseQueue = false)
        {
            UseSysLogQueue = UseQueue;
            if (UseSysLogQueue)
            {
                InitDequeue();
            }
            if (MaxEntries == 0) MaxEntries = 500;
            MyMessageLog = new TheStorageMirror<TheEventLogEntry>(TheCDEngines.MyIStorageService) { IsRAMStore = true, BlockWriteIfIsolated = true };
            MyMessageLog.InitializeStore(true);
            SetMaxLogEntries(MaxEntries);
            if (LogWriteBuffer > 0 && LogWriteBuffer < 10) LogWriteBuffer = 10;
            LogFilePath = null;
        }
        internal void SetMaxLogEntries(int maxEntries)
        {
            if (maxEntries == 0) maxEntries = 500;
            maxEntries = TheBaseAssets.MyServiceHostInfo?.IsMemoryOptimized == true && maxEntries > 10 ? 10: maxEntries;
            MyMessageLog.SetMaxStoreSize(maxEntries);
        }

        internal void Shutdown()
        {
            if (LogWriteBuffer > 0)
                WriteLogToFile(true);
        }

        /// <summary>
        /// Allows to change the amount of messages kept in the RAM buffer of the SystemLog
        /// </summary>
        /// <param name="pEntries">Amount of entries to be stored in RAM</param>
        public void ChangeMaxLogEntries(int pEntries)
        {
            SetMaxLogEntries(pEntries);
        }

        /// <summary>
        /// Allows to inject dispose code if required
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    syslogCancel?.Cancel();
                }
                catch { 
                    //intent
                }
            }
        }
        /// <summary>
        /// Disposes the log
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private readonly object writeLock = new ();
        private int LogCounter;
        private string MySystemFileLogName = "";
        private string MyCurLog = "";

        private void WriteLogToFile(bool WaitForLock)
        {
            if (string.IsNullOrEmpty(MyCurLog))
            {
                return;
            }
            // ReSharper disable once EmptyEmbeddedStatement
            if (WaitForLock)
            {
                while (TheCommonUtils.cdeIsLocked(writeLock)) // FIXED: This can go into a tight loop - should yield the thread or sleep for 1 few milliseconds before checking again
                {
                    TheCommonUtils.SleepOneEye(5, 5);
                    if (!TheBaseAssets.MasterSwitch)
                        return;
                }
            }
            if (!TheCommonUtils.cdeIsLocked(writeLock))
            {
                lock (writeLock)
                {
                    var bLogFileExists = File.Exists(MyCurLog);
                    if (MaxLogFileSize > 0 && bLogFileExists)
                    {
                        try
                        {
                            FileInfo f2 = new (MyCurLog);
                            if (f2.Length > MaxLogFileSize * (1024 * 1024))
                            {
                                LogFilePath = mLogFilePath;
                                File.Move(MyCurLog, MySystemFileLogName);
                                CheckFileRollover();
                            }
                        }
                        catch {
                            //ignored
                        }
                        bLogFileExists = File.Exists(MyCurLog);
                    }
                    try
                    {
                        using (StreamWriter fs = new (MyCurLog, bLogFileExists))
                        {
                            foreach (TheEventLogEntry tLogs in MyMessageLog.MyMirrorCache.MyRecords.Values.Where(s => !s.WasWritten).OrderBy(s => s.Serial).ToList())   //serial is more precise than CTIM
                            {
                                fs.WriteLine($"{tLogs.Source} : {tLogs.EventID} : {tLogs.Serial} : {tLogs.Message}");   //Serial is required to see the exact order of messages put in the log if the ms in the timestamp is the same
                                tLogs.WasWritten = true;
                            }
                        }
                    }
                    catch
                    {
                        //ignored
                    }
                }
            }
        }

        private void CheckFileRollover()
        {
            if (string.IsNullOrEmpty(LogFilePath) || MaxLogFiles==0) return;
            DirectoryInfo di = new (LogFilePath);
            FileInfo[] fileInfo = di.GetFiles();
            if (!fileInfo.Any()) return;

            List<FileInfo> InfoList = new ();

            foreach (FileInfo fiNext in fileInfo)
            {
                if (fiNext.Extension.ToUpper().Equals(".TXT") && fiNext.Name.StartsWith(TheBaseAssets.MyServiceHostInfo.ApplicationName + "_SYSTEMLOG_"))
                {
                    InfoList.Add(fiNext);
                }
            }
            if (InfoList.Count > 0 && InfoList.Count > MaxLogFiles)
            {
                FileInfo tI = InfoList.OrderBy(s => s.CreationTime).First();
                if (tI != null)
                    File.Delete(tI.FullName);
            }
        }

        /// <summary>
        /// Path to the Log File
        /// </summary>
        public string LogFilePath
        {
            get { return mLogFilePath; }
            set
            {
                if (value == null)
                    return; //4.0114: Do not create log path if set to null...otherwise it creates log folder on cloud
                mLogFilePath = value;
                if (TheCommonUtils.cdeIsFileSystemCaseSensitive())
                {
                    if (!string.IsNullOrEmpty(value) && !value.EndsWith("\\"))
                        mLogFilePath += "\\";
                    mLogFilePath = TheCommonUtils.cdeFixupFileName(mLogFilePath);
                    TheCommonUtils.CreateDirectories(mLogFilePath);
                }
                else
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        mLogFilePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        if (String.IsNullOrEmpty(mLogFilePath))
                        {
                            mLogFilePath = Path.Combine(Path.Combine(TheBaseAssets.MyServiceHostInfo.BaseDirectory, "ClientBin"), "Logs") + Path.DirectorySeparatorChar;  //CODE-REVIEW: path must be validated with cdeFixupFileName
                            TheCommonUtils.CreateDirectories(mLogFilePath);
                        }
                        else
                        {
                            mLogFilePath += "\\";
                        }
                    }
                    else
                    {
                        mLogFilePath = value;
                        if (!Path.IsPathRooted(mLogFilePath))
                        {
                            var root = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                            if (String.IsNullOrEmpty(root))
                            {
                                root = Path.Combine(Path.Combine(TheBaseAssets.MyServiceHostInfo.BaseDirectory, "ClientBin"), "Logs"); //CODE-REVIEW: path must be validated with cdeFixupFileName
                            }
                            mLogFilePath = Path.Combine(root, mLogFilePath);
                        }
                        if (!value.EndsWith("\\"))
                            mLogFilePath += "\\";
                        TheCommonUtils.CreateDirectories(mLogFilePath);
                    }
                }
                MySystemFileLogName = mLogFilePath + string.Format(TheBaseAssets.MyServiceHostInfo.ApplicationName + "_SYSTEMLOG_{0:yyyMMdd_HHmmss}.txt", DateTime.Now);
                MyCurLog = string.Format("{0}{1}_SYSTEMLOG.txt", mLogFilePath, TheBaseAssets.MyServiceHostInfo.ApplicationName);
            }
        }
        private string mLogFilePath;

        /// <summary>
        /// Specify the maximum Log File Size
        /// </summary>
        public int MaxLogFileSize = 0;

        /// <summary>
        /// Main Log entry for the CDE System Log
        /// </summary>
        /// <param name="tLogID">Unique ID of the Event</param>
        /// <param name="MyText">TSM with the log entry</param>
        /// <param name="NoLog">if true, this entry will not be written to the log-history</param>
        [Conditional("CDE_SYSLOG")]
        public static void WriteLog(int tLogID, TSM MyText, bool NoLog)
        {
            if (TheBaseAssets.MySYSLOG == null) return;
            TheBaseAssets.MySYSLOG.WriteToLog(tLogID, MyText, NoLog);
        }

        [Conditional("CDE_SYSLOG")]
        internal static void ToCo(string text)
        {
            ToCo(text, false);
        }
        [Conditional("CDE_SYSLOG")]
        internal static void ToCo(string text, bool Force)
        {
            if (TheBaseAssets.MyServiceHostInfo?.DisableConsole != true && (TheBaseAssets.MyServiceHostInfo == null || TheBaseAssets.MyServiceHostInfo.DebugLevel > eDEBUG_LEVELS.OFF || Force))
                Console.WriteLine("{0}:{1}", Interlocked.Increment(ref LogSerial), text);
        }

        private static long LogSerial;

        /// <summary>
        /// Writes a TSM to the SystemLog with a given ID, if the logLevel is greater than the configured debug level (TheBaseAssets.MyServiceHostInfo.DebugLevel).
        /// The TSM is only created if logging will actually occur. If the TSM creation fails, an alternate text is logged.
        /// </summary>
        /// <param name="LogID">Event ID of the entry</param>
        /// <param name="logLevel">logLevel to be checked against the configured debug level.</param>
        /// <param name="createMyText">Function that creates a TSM with the Main Content of the Entry, i.e. () => new TSM(...).</param>
        [Conditional("CDE_SYSLOG")]
        public void WriteToLogSafe(int LogID, eDEBUG_LEVELS logLevel, Func<TSM> createMyText)
        {
            if (!TSM.L(logLevel))
            {
                TSM text;
                try
                {
                    text = createMyText();
                }
                catch (Exception e) { text = new TSM(eEngineName.ContentService, "Error creating log message", eMsgLevel.l2_Warning, e.ToString()); }
                WriteToLog(LogID, text, false);
            }
        }

        /// <summary>
        /// Writes a TSM to the SystemLog with a given ID, if the logLevel is greater than the configured debug level (TheBaseAssets.MyServiceHostInfo.DebugLevel).
        /// The TSM is only created if logging will actually occur. If the TSM creation fails, an alternate text is logged.
        /// </summary>
        /// <param name="LogID">Event ID of the entry</param>
        /// <param name="logLevel">logLevel to be checked against the configured debug level.</param>
        /// <param name="createMyText">Function that creates a TSM with the Main Content of the Entry, i.e. () => new TSM(...).</param>
        /// <param name="NoLog">If set to true, the entry will not be stored to the StorageService or Disk</param>
        [Conditional("CDE_SYSLOG")]
        public void WriteToLogSafe(int LogID, eDEBUG_LEVELS logLevel, Func<TSM> createMyText, bool NoLog)
        {
            if (!TSM.L(logLevel))
            {
                TSM text;
                try
                {
                    text = createMyText();
                }
                catch (Exception e) { text = new TSM(eEngineName.ContentService, "Error creating log message", eMsgLevel.l2_Warning, e.ToString()); }
                WriteToLog(LogID, text, NoLog);
            }
        }

        /// <summary>
        /// Writes a TSM to the SystemLog with a given ID
        /// </summary>
        /// <param name="LogID">Event ID of the entry</param>
        /// <param name="MyText">Main Content of the Entry</param>
        [Conditional("CDE_SYSLOG")]
        public void WriteToLog(int LogID, TSM MyText)
        {
            if (MyText == null) return;
            WriteToLog(LogID, MyText, false);
        }

        /// <summary>
        /// Writes a TSM to The SystemLog
        /// </summary>
        /// <param name="MyText"></param>
        /// <param name="LogID"></param>
        /// <param name="NoLog"></param>
        public void WriteToLog(TSM MyText, int LogID = 0, bool NoLog = false)
        {
            if (MyText == null) return;
            WriteToLog(LogID, MyText, NoLog);
        }

        /// <summary>
        /// Writes to the SystemLog
        /// </summary>
        /// <param name="LogID"></param>
        /// <param name="LogLevel"></param>
        /// <param name="pTopic"></param>
        /// <param name="pSeverity"></param>
        /// <param name="pLogText"></param>
        /// <param name="NoLog"></param>
        public void WriteToLog(eDEBUG_LEVELS LogLevel, int LogID, string pTopic, string pLogText, eMsgLevel pSeverity=eMsgLevel.l4_Message, bool NoLog= false)
        {
            if (TSM.L(LogLevel)) return;
            WriteToLog(LogID, new TSM(pTopic, pLogText, pSeverity), NoLog);
        }

        /// <summary>
        /// Writes a TSM to the SystemLog with a given ID
        /// </summary>
        /// <param name="pLogID">Event ID of the entry</param>
        /// <param name="MyText">Main Content of the Entry</param>
        /// <param name="NoLog">If set to true, the entry will not be stored to the StorageService or Disk</param>
        [Conditional("CDE_SYSLOG")]
        public void WriteToLog(int pLogID, TSM MyText, bool NoLog)
        {
            if (MyText == null) return;
            if (!UseSysLogQueue)
            {
                TheCommonUtils.cdeRunAsync(string.Format("WriteLog:{0}", pLogID), false, o =>
                {
                    WriteToLogInternal(pLogID, MyText, NoLog, TheCommonUtils.CLng(o));
                }, Interlocked.Increment(ref LogSerial));
            }
            else
            {
                if (!sysLogQueue.TryAdd(new LogArgs { pLogID = pLogID, MyText = MyText, NoLog = NoLog, tLogSerial = Interlocked.Increment(ref LogSerial) }))
                {
                    // queue is full, log entry dropped
                }
            }
        }


        void WriteToLogInternal(int pLogID, TSM MyText, bool NoLog, long tLogSerial)
        {
            bool AddEntry = true;
            if (LogFilter.Count > 0)
            {
                AddEntry = false;
                foreach (string t in LogFilter)
                {
                    if (MyText.ENG.Equals(t))
                    {
                        AddEntry = true;
                        break;
                    }
                }
            }
            if (LogOnly.Count > 0)
            {
                bool Found = false;
                foreach (string t in LogOnly)
                {
                    if (MyText.ENG == null || MyText.ENG.Equals(t))
                    {
                        Found = true;
                        break;
                    }
                }
                if (!Found)
                    return;
            }
            if (LogIgnore.Count > 0)
            {
                foreach (string t in LogIgnore)
                {
                    if (MyText.ENG == null || MyText.ENG.Equals(t)) return;
                }
            }
            if (!TheBaseAssets.MyServiceHostInfo.DisableConsole)
            {
                Console.ForegroundColor = MyText.LVL switch
                {
                    eMsgLevel.l1_Error => ConsoleColor.Red,
                    eMsgLevel.l2_Warning => ConsoleColor.Yellow,
                    eMsgLevel.l3_ImportantMessage => ConsoleColor.Green,
                    eMsgLevel.l6_Debug or eMsgLevel.l7_HostDebugMessage => ConsoleColor.Gray,
                    _ => ConsoleColor.White,
                };
                string tout = null;
                if (TheBaseAssets.MyServiceHostInfo.UseGELFLoggingFormat)
                {
                    var t = new TheGELFLogEntry()
                    {
                        version = "1.1",
                        host = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false),
                        level = (int)MyText?.LVL,
                        timestamp = TheCommonUtils.CDbl($"{MyText.TIM.ToUnixTimeSeconds()}.{MyText.TIM.Millisecond}"),
                        full_message = TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog ? TheCommonUtils.cdeStripHTML(MyText?.PLS) : MyText?.PLS,
                        short_message = TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog? TheCommonUtils.cdeStripHTML(MyText?.TXT):MyText?.TXT,
                        _serial_no = tLogSerial,
                        _log_id = pLogID,
                        _engine = MyText?.ENG,
                        _device_id = MyText.ORG,
                        // _status_level = ?
                        // _topic = ?
                    };
                    tout = TheCommonUtils.SerializeObjectToJSONString<TheGELFLogEntry>(t);
                }
                else
                    tout = $"ID:{pLogID} SN:{tLogSerial} {MyText.ToAllString()}";
                Console.WriteLine(tout);
                Console.ForegroundColor = ConsoleColor.White;
            }
            if (AddEntry)
            {
                if (MyMessageLog != null)
                    Add(pLogID, tLogSerial, "", MyText);
                if (MyText.LVL == eMsgLevel.l1_Error)
                    LastError = MyText;
#if CDE_DEBUGVIEW
                        System.Diagnostics.Debug.WriteLine(string.Format("ID:{0} SN:{1} {2}", pLogID, tLogSerial, MyText.ToAllString()));
#endif
                if (LogWriteBuffer > 0 && MyText.LVL < LogWriteFilterLevel)
                {
                    LogCounter++;
                    if (LogCounter > LogWriteBuffer)
                    {
                        LogCounter = 0;
                        WriteLogToFile(false);
                    }
                }
                if (TheBaseAssets.MyApplication != null && MyText.LVL < MessageFilterLevel)
                    TheBaseAssets.MyApplication.ShowMessageToast(MyText, "");
                if (TheCDEngines.MyIStorageService != null && TheBaseAssets.MyServiceHostInfo.StoreLoggedMessages && !NoLog &&
                    (MyText.LVL < LogFilterLevel) && TheCDEngines.MyIStorageService.GetBaseEngine().GetEngineState().IsEngineReady)
                    TheCDEngines.MyIStorageService.EdgeDataStoreOnly(MyText, null);
            }
        }

        class LogArgs
        {
            public int pLogID;
            public TSM MyText;
            public bool NoLog;
            public long tLogSerial;
        }

        internal void SwitchToQueue()
        {
            InitDequeue();
            UseSysLogQueue = true;
        }
        void InitDequeue()
        {
            if (syslogCancel == null)
            {
                lock (sysLogQueue)
                {
                    if (syslogCancel == null)
                    {
                        syslogCancel = new CancellationTokenSource();
                        TheCommonUtils.cdeRunAsync("SysLogDequeue", true, DequeueSysLog, syslogCancel.Token);
                    }
                }
            }
        }

        readonly BlockingCollection<LogArgs> sysLogQueue = new (); // CODEREVIEW: we currently don't put a limit on the pending tasks. Should we put a limit on the queue size?
        CancellationTokenSource syslogCancel;

        private void DequeueSysLog(object cancelTokenObj)
        {
            try
            {
                var cancelToken = (CancellationToken)cancelTokenObj;
                while (!cancelToken.IsCancellationRequested)
                {
                    try
                    {
                        foreach (var logArg in sysLogQueue.GetConsumingEnumerable(cancelToken))
                        {
                            long tLogSerial = logArg.tLogSerial;
                            var pLogID = logArg.pLogID;
                            var MyText = logArg.MyText;
                            var NoLog = logArg.NoLog;
                            WriteToLogInternal(pLogID, MyText, NoLog, tLogSerial);
                        }
                    }
                    catch { 
                        //intent
                    }
                }
            }
            catch { 
                //intent
            }
        }
        internal string GetNodeLog(TheSessionState pSession, string InTopic, bool ShowLinks)
        {
            string outText = "";
            if (!string.IsNullOrEmpty(InTopic))
                InTopic = TheCommonUtils.cdeUnescapeString(InTopic);
            outText += $"<div class=\"cdeInfoBlock\" style=\"clear:both; width:initial; \"><div class=\"cdeInfoBlockHeader cdeInfoBlockHeaderText\" id=\"systemLog\">Current SystemLog <a download=\"cdeSysLog_{TheCommonUtils.GetMyNodeName()}.csv\" href=\"#\" class=\'cdeExportLink\' onclick=\"return ExcellentExport.csv(this, 'cdeSysLog');\">(Export as CSV)</a></div>";
            try
            {
                outText += "<table class=\"cdeHilite\" id=\"cdeSysLog\" style=\"width:95%\">";
                outText += "<tr><th style=\"background-color:rgba(90,90,90, 0.25);font-size:small;\">Serial</th>";
                outText += "<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; \">LogID</th>";
                outText += "<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; \">Entry Date</th>";
                outText += "<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; \">Level</th>";
                outText += "<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; \">Engine</th>";
                outText += "<th style=\"background-color:rgba(90,90,90, 0.25);font-size:small; \">Text</th><tr>";
                int MaxCnt = MyMessageLog.MyMirrorCache.Count;
                foreach (TheEventLogEntry tLogEntry in MyMessageLog.MyMirrorCache.MyRecords.Values.OrderByDescending(s=>s.Serial).ToList()) //.cdeCTIM).ThenByDescending(s=>s.cdeCTIM.Millisecond).ToList())
                {
                    TSM tMsg = tLogEntry.Message;
                    if (!string.IsNullOrEmpty(InTopic) && !tMsg.ENG.Equals(InTopic)) continue;
                    tMsg.TXT ??= "";
                    var tColor = "black";
                    if (tMsg.TXT.Contains("ORG:2;"))
                        tColor = "blue";
                    else
                    {
                        if (tMsg.TXT.Contains("ORG:4;"))
                            tColor = "purple";
                        else
                        {
                            if (tMsg.TXT.Contains("ORG:3;"))
                                tColor = "navy";
                            else
                            {
                                if (tMsg.TXT.Contains("ORG:7;"))
                                    tColor = "brown";
                                else
                                {
                                    if (tMsg.TXT.Contains("ORG:8;") || TheCommonUtils.DoesContainLocalhost(tMsg.TXT))
                                        tColor = "gray";
                                }
                            }
                        }
                    }
                    switch (tMsg.LVL)
                    {
                        case eMsgLevel.l1_Error:
                            tColor = "red";
                            break;
                        case eMsgLevel.l2_Warning:
                            tColor = "orange";
                            break;
                        case eMsgLevel.l3_ImportantMessage:
                            tColor = "green";
                            break;
                        case eMsgLevel.l7_HostDebugMessage:
                            tColor = "gray";
                            break;
                        case eMsgLevel.l6_Debug:
                            tColor = "gray";
                            break;
                    }
                    outText += $"<tr>";
                    outText += $"<td class=\"cdeLogEntry\" style=\"color:{tColor}\">{tLogEntry.Serial}</td>";
                    outText += $"<td class=\"cdeLogEntry\" style=\"color:{tColor}\">{tLogEntry.EventID}</td>";
                    outText += $"<td class=\"cdeLogEntry\" style=\"color:{tColor}\">{TheCommonUtils.GetDateTimeString(tMsg.TIM, 0, "yyyy-MM-dd HH:mm:ss.fff")}</td>";
                    outText += $"<td class=\"cdeLogEntry\" style=\"color:{tColor}\">{tMsg.LVL}{(TSM.L(eDEBUG_LEVELS.ESSENTIALS)?tMsg.GetHash().ToString():"")}</td>";
                    //outText += $"<td class=\"cdeLogEntry\" style=\"color:{tColor}\">{tMsg.ORG}</td>";    //ORG-OK
                    if (ShowLinks && pSession != null)
                        outText += $"<td class=\"cdeLogEntry\"><SMALL><a href=\"/cdeStatus.aspx?Filter={TheCommonUtils.cdeEscapeString(tMsg.ENG)}\">{tMsg.ENG}</a></SMALL></td>";
                    else
                        outText += $"<td class=\"cdeLogEntry\"><SMALL>{tMsg.ENG.Replace(".", " ")}</SMALL></td>";
                    outText += $"<td class=\"cdeLogEntry\" style=\"color:{tColor}\">{tMsg.TXT}</td>";
                    outText += "</tr>";
                    if (!string.IsNullOrEmpty(tMsg.PLS))
                        outText += $"<tr><td class=\"cdeLogEntry\" style=\"color:{tColor}\">{tLogEntry.Serial}</td><td class=\"cdeLogEntry\" colspan=\"7\" style=\"color:{tColor}\"><SMALL>{TheCommonUtils.cdeESCXMLwBR(TheCommonUtils.cdeSubstringMax(tMsg.PLS, 2000))}</SMALL></td></tr>"; //Find a better way. This does not work with the sorttable
                    MaxCnt--;
                }
                outText += "</tbody></table></div>";
            }
            catch (Exception e)
            {
                outText += "Exception in Log: " + e;
            }
            return outText;
        }
    }
}
