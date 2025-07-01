using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace nsCDEngine.Engines.ThingService
{
    public interface ICDEIssueLog
    {
        public void ClearIssue(string pDevId, string pSensorId);
        public void ClearIssues(string pDevId, List<string> pSensorIds);
        public bool HasIssue(string pDevId, string pSensorId);
        public void FileIssue(TheThing pBaseThing, string pSensorId, string pMessage);
        public void FileIssue(string pDevId, string pSensorId, string pDevName, string pCategory, string pMessage);
    }

    /// <summary>
    /// Interface that must be present on a Plugin to enable the Factory
    /// </summary>
    public interface ICDEIssueLogFactory
    {
        public ICDEIssueLog InitIssueLog();
    }
    public class TheIssue : TheMetaDataBase
    {
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

        public DateTimeOffset clear_time { get; set; }
        public DateTimeOffset ack_time { get; set; }
        public DateTimeOffset last_update { get; set; }
        public string device_id { get; set; }
        public string sensor_id { get; set; }
        public string device_name { get; set; }
        public string category { get; set; }
        public string current_status { get; set; }
        public string suspended_by { get; set; }

        public string comment { get; set; }
        public string engine { get; set; }

        //possibly to TheMetaDataBase?
        public string cdeRC { get; set; }

        public override string ToString()
        {
            return $"{device_name}:{short_message} {current_status}";
        }
    }
    /// <summary>
    /// TheIssueLog logs alarms and messages that can be used to track issues with devices and sensors.
    /// Issues can be cleared, acknowledged, and categorized.
    /// </summary>
    public static class TheIssueFactory
    {
        private static ICDEIssueLog _issueLog = null;
        public static ICDEIssueLog MyIssueLog
        {
            get { 
                if (_issueLog == null)
                {
                    InitIssueLog();
                }
                return _issueLog;
            }
        }
        public static void InitIssueLog()
        {
            if (_issueLog == null)
            {
                var FoundEngines = TheThingRegistry.GetBaseEnginesByCap(eThingCaps.IssueLog);
                if (FoundEngines?.Count > 0)
                {
                    var EngineThing = FoundEngines?.First()?.GetBaseThing();
                    var issueLogFac = EngineThing?.GetObject() as ICDEIssueLogFactory;
                    if (EngineThing != null && issueLogFac != null)
                    {
                        _issueLog=issueLogFac.InitIssueLog();
                    }
                }
            }
        }
    }
}
