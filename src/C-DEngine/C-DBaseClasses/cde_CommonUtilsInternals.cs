// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace nsCDEngine.BaseClasses
{
    public static partial class TheCommonUtils
    {
        private static readonly string[] holoDevices = new string[] { "hololens", "holographic", "edge/14.14342", "edge/14.14393" };

        private static readonly string[] mobileDevices = new string[] {"iphone","ppc", "iPad",
                                                      "windows ce","blackberry",
                                                      "opera mini","mobile","palm",
                                                      "portable","opera mobi" };

        internal static bool cdeDeleteIfContainsCookie(List<string> pCookies, string pCook)
        {
            for (int i = 0; i < pCookies.Count; i++)
            {
                string[] tco = pCookies[i].Split(';');
                if (tco[0] == pCook)
                {
                    pCookies.Remove(pCookies[i]);
                    return true;
                }
            }
            return false;
        }

        internal static string cdeSQLEncode(object oStr)
        {
            if (oStr == null) return "";
            string iStr = CStr(oStr);
            if (iStr.Length == 0) return "";
            //<New>CM,3.5.200,3/12/2004,SQLEncode will not allow the SQL Remarks syntax anymore
            int pos = iStr.IndexOf("'", 0);
            if (pos >= 0)
            {
                if (iStr.IndexOf("--", pos) >= 0)
                    iStr = iStr.Substring(0, pos);
            }
            //</New>
            return iStr.Replace("'", "''"); //+ CHAR(39) +'");
        }

        #region internal Get... functions
        internal static string GetMyDevicesSQLFilter()
        {
            string res = "";
            if (TheBaseAssets.MyServiceHostInfo.MyDeviceInfo != null)
                res += "DeviceID='" + TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID + "'";
            foreach (TheDeviceRegistryData tstre in TheBaseAssets.MyApplication.MyUserManager.GetMyDevices())
            {
                if (tstre.DeviceID.Equals(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)) continue;
                if (res.Length > 0) res += " or ";
                res += "DeviceID='" + tstre.DeviceID + "'";
            }
            if (res.Length > 0) res = "(" + res + ")";
            return res;
        }

        internal static TheClientInfo GetClientInfo(TheProcessMessage pMsg)
        {
            TheClientInfo tRes = new TheClientInfo
            {
                UserID = pMsg.CurrentUserID,
                NodeID = pMsg.Message.GetOriginator(),
                IsFirstNode = pMsg.Message.IsFirstNode()
            };
            tRes.IsOnCloud = (tRes.IsFirstNode && TheBaseAssets.MyServiceHostInfo.IsCloudService) || !tRes.IsFirstNode;
            tRes.IsTrusted = TheBaseAssets.MySettings.IsNodeTrusted(pMsg.Message.GetOriginatorSecurityProxy());
            if (!tRes.IsTrusted && pMsg.Message.HobCount() == 3 && TheBaseAssets.MyServiceHostInfo.IsIsolated)
                tRes.IsTrusted = TheBaseAssets.MySettings.IsNodeTrusted(pMsg.Message.GetLastRelay());
            tRes.IsUserTrusted = TheUserManager.IsUserTrusted(pMsg.CurrentUserID);
            TheSessionState tSess = TheBaseAssets.MySession.ValidateSEID(TheCommonUtils.CGuid(pMsg.Message.SEID));  //HIGH NMI frequency
            if (tSess != null)
            {
                tRes.LCID = TheCommonUtils.GetLCID(pMsg); // tSess.LCID;
                tRes.WebPlatform = tSess.WebPlatform;
                tRes.IsMobile = (tSess.WebPlatform == eWebPlatform.Mobile);
            }
            return tRes;
        }

        internal static cdeSenderType GetOriginST(TheChannelInfo pChan)
        {
            if (pChan == null)
                return cdeSenderType.NOTSET;
            return pChan.SenderType == cdeSenderType.CDE_CUSTOMISB ? pChan.SenderType : TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType;
        }

        internal static Guid GetDeviceIDFromTopic(string pTopic)
        {
            Guid DeviceID = Guid.Empty;
            string[] tt = pTopic.Split(';');
            if (tt.Length > 1) DeviceID = CGuid(tt[1]);
            return DeviceID;
        }

        internal static int GetLCID(TheProcessMessage pMSG, TheSessionState session)
        {
            int lcid = 0;
            if (session != null)
            {
                lcid = session.LCID;
            }
            if (lcid == 0)
            {
                var user = pMSG != null ? TheUserManager.GetUserByID(pMSG.CurrentUserID) : null;
                if (user != null)
                {
                    lcid = user.LCID;
                }
                if (lcid == 0)
                {
                    lcid = TheBaseAssets.MyServiceHostInfo.DefaultLCID;
                }
            }
            return lcid;
        }

        internal static string GetCustomClassType(out Type pType, string pClassName)
        {
            string tError = "";
            pType = null;
            try
            {
                List<Type> Typelist = TheCommonUtils.cdeGetExecutingAssembly().GetTypes().Where(tt => tt.FullName.EndsWith(pClassName)).ToList();
                if (Typelist?.Count == 0)
                {
                    Typelist = Assembly.GetCallingAssembly().GetTypes().Where(tt => tt.FullName.EndsWith(pClassName)).ToList();
                    if (Typelist?.Count == 0)
                        Typelist = TheBaseAssets.MyServiceTypes.Where(tt => tt.FullName.EndsWith(pClassName)).ToList();
                }
                if (Typelist != null && Typelist.Count > 0)
                    pType = Typelist[0];
                if (pType == null)
                    tError = "Could not find: " + pClassName;
            }
            catch (Exception e)
            {
                tError = "GetCustomClassType Fatal Error:" + e.ToString();
            }
            return tError;
        }
        #endregion

        internal static bool SetPropValue(object target, string propName, object value)
        {
            bool success = true;
            try
            {
                PropertyInfo tInfo = target.GetType().GetProperty(propName);
                if (tInfo != null)
                {
                    var targetValue = Convert.ChangeType(value, tInfo.PropertyType);
                    tInfo.SetValue(target, targetValue, BindingFlags.SetProperty | BindingFlags.Public, null, null, null);
                }
                else
                {
                    FieldInfo tF = target.GetType().GetField(propName);
                    if (tF != null)
                    {
                        tF.SetValue(target, value);
                    }
                    else
                    {
                        success = false;
                    }
                }
            }
            catch
            {
                success = false;
            }
            return success;
        }

        #region internal probes
        internal static bool IsPropertyOfClass(string propName, Type nmiPlatBag)
        {
            PropertyInfo[] tInfo = nmiPlatBag.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
            return tInfo.Any(s => s.Name == propName);
        }

        internal static bool Check4ValidEmail(string userName)
        {
            if (String.IsNullOrEmpty(userName))
            {
                return false;
            }
            if (!userName.Contains("@") || !userName.Contains(".") || userName.Length < 5)
            {
                return false;
            }
            // TODO Do RegEx validation (like in NMI/.TS)
            //var RegExp = new RegexStringValidator("[a-z0-9!#$%&'*+\\=?^_`{|}~-]+(?:\\.[a-z0-9!#$%&'*+\\=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?");
            ////var filter:RegExp = /^([a-zA-Z0-9_\.\-])+\@(([a-zA-Z0-9\-])+\.)+([a-zA-Z0-9]{2,4})+$/;
            ////var filter:RegExp = /[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+(?:[A-Z]{2}|com|org|net|gov|biz|info|name|aero|biz|info|jobs|museum)/;
            //if (!email || !filter.test(email.toLowerCase()) || email.substring(0, 1) === '.' || email.substring(email.length - 1, 1) === '.')
            //    return false;
            return true;
        }
        #endregion

        #region internal converter and creater
        internal static string CTypeToString(Type fType, object orgValue)
        {
            if (fType.Equals(typeof(DateTime)))
            {
                if (((DateTime)orgValue).Equals(DateTime.MinValue))
                    orgValue = null;
                else
                {
                    DateTimeOffset tDateTime = CDate(orgValue);
                    orgValue = string.Format("{0:MM/dd/yyyy hh:mm:ss.fff tt zzz}", tDateTime); // CODE REVIEW: Why this format, specifically AM/PM? Display in JS/NMI? CM: This is for SQL Server. If it can handle ISO format we should switch to ISO
                }
            }
            else if (fType.Equals(typeof(DateTimeOffset)))
            {
                if (((DateTimeOffset)orgValue).Equals(DateTimeOffset.MinValue))
                    orgValue = null;
                else
                {
                    DateTimeOffset tDateTime = (DateTimeOffset)orgValue;
                    orgValue = string.Format("{0:MM/dd/yyyy hh:mm:ss.fff tt zzz}", tDateTime); // CODE REVIEW: Why this format, specifically AM/PM? Display in JS/NMI? CM: This is for SQL Server. If it can handle ISO format we should switch to ISO
                }
            }
            else
            {
                if (fType.IsEnum)
                    orgValue = (int)orgValue;
            }
            if (orgValue == null)
                return "";
            else
                return orgValue.ToString();
        }

        internal static string CreateListFromDataSource(string pDataSource)
        {
            if (string.IsNullOrEmpty(pDataSource)) return null;

            string[] t = pDataSource.Split(new string[] { ";:;" }, StringSplitOptions.None);
            GetCustomClassType(out Type TemplateType, t[0]);

            if (TemplateType == null) return null;
            object tres = Activator.CreateInstance(TemplateType);
            return SerializeObjectToJSONString(tres);
        }

        internal static void GuidSwitchNetworkOrder(byte[] guidBytes)
        {
            SwitchBytes(guidBytes, 0, 3);
            SwitchBytes(guidBytes, 1, 2);
            SwitchBytes(guidBytes, 4, 5);
            SwitchBytes(guidBytes, 6, 7);
        }

        internal static void SwitchBytes(byte[] bytes, int i, int j)
        {
            var temp = bytes[i];
            bytes[i] = bytes[j];
            bytes[j] = temp;
        }

        internal static string MOTLockGenerator()
        {
            string ret = "";
            string last = "";
            uint plen = cdeRND.NextUInt(4, 8);
            int eTries = 0;
            for (int i = 0; i < plen; i++)
            {
                string pnew = cdeRND.NextUInt(0, 999).ToString();
                if (!last.Equals(pnew))
                {
                    if (ret.Length > 0) ret += ";";
                    ret += pnew;
                    last = pnew;
                }
                else
                {
                    if (eTries < plen)
                    {
                        i--;
                        eTries++;
                    }
                }
            }
            return ret;
        }
        #endregion

        internal static bool ProcessChunkedMessage(string[] CommandParts, TSM recvMessage)
        {
            if (recvMessage.PLB == null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(286, new TSM("CoreComm", string.Format("Chunk Error: PLB must not be null {0}-{1} - {2} - TXT:{3}", CommandParts[2], CommandParts[3], CommandParts[1], recvMessage.TXT), eMsgLevel.l1_Error));
                return false;
            }

            Guid chunkGuid = TheCommonUtils.CGuid(CommandParts[3]);
            if (chunkGuid != Guid.Empty)
            {
                var chunkIndex = int.Parse(CommandParts[1]);
                var expectedChunkCount = TheCommonUtils.CInt(CommandParts[2]);
                try
                {
                    TheReceivedParts tDic = null;
                    bool bNewlyAdded = false;
                    bool bComplete = false;
                    int pendingTSMCount = 0;
                    TheBaseAssets.MyReceivedParts.MyRecordsRWLock.RunUnderUpgradeableReadLock(() =>
                    {
                        tDic = TheBaseAssets.MyReceivedParts.GetEntryByID(chunkGuid);
                        if (tDic == null)
                        {
                            bNewlyAdded = true;
                            tDic = new TheReceivedParts()
                            {
                                MyParts = new cdeConcurrentDictionary<int, byte[]>()         //DIC-Allowed
                            };
                            tDic.MyParts.TryAdd(chunkIndex, recvMessage.PLB);
                            tDic.cdeMID = chunkGuid;
                            TheBaseAssets.MyReceivedParts.AddAnItem(tDic, null);
                        }
                        else
                        {
                            // TODO: need to expire incomplete TSMs.
                            tDic.MyParts.TryAdd(chunkIndex, recvMessage.PLB);
                        }
                        if (tDic.MyParts.Count == expectedChunkCount) // CODE REVIEW: This allows single-chunk TSMs. Previously those would be ignored (leaked in MyReceivedParts). Should we drop or allow?
                        {
                            TheBaseAssets.MyReceivedParts.RemoveAnItem(tDic, null);
                            bComplete = true;
                        }
                        else if (!bNewlyAdded)
                        {
                            TheBaseAssets.MyReceivedParts.AddOrUpdateItem(tDic);
                        }
                        pendingTSMCount = TheBaseAssets.MyReceivedParts.Count;
                    });
                    if (tDic != null)
                    {
                        if (bComplete)
                        {
                            int cntFinal = 0;
                            for (int i = 0; i < tDic.MyParts.Count; i++) cntFinal += tDic.MyParts[i].Length;
                            recvMessage.PLB = new byte[cntFinal];
                            int bufPos = 0;
                            for (int i = 0; i < tDic.MyParts.Count; i++)
                            {
                                TheCommonUtils.cdeBlockCopy(tDic.MyParts[i], 0, recvMessage.PLB, bufPos, tDic.MyParts[i].Length);
                                bufPos += tDic.MyParts[i].Length;
                            }
                            TheBaseAssets.MySYSLOG.WriteToLog(286, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"MultiNode Package Completed ! {tDic.MyParts.Count}/{chunkIndex}-{expectedChunkCount} - {recvMessage.PLB.Length} - ID:{chunkGuid} Pending:{pendingTSMCount} TXT:{recvMessage.TXT}", eMsgLevel.l7_HostDebugMessage));
                            tDic.MyParts = null;
                            tDic = null;
                        }
                        else
                        {
                            IBaseEngine tBase = TheThingRegistry.GetBaseEngine(recvMessage.ENG);
                            tBase?.FireEvent(eEngineEvents.ChunkReceived, $"{tDic.MyParts.Count},{chunkIndex},{recvMessage.TXT}", true); // CODE REVIEW: Should we fire this event also for the last chunk received? Technically a breaking change
                            if (bNewlyAdded)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(286, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"New MultiNode Started {tDic.MyParts.Count}/{chunkIndex}-{expectedChunkCount} - {recvMessage.PLB.Length} - ID:{chunkGuid} Pending:{pendingTSMCount}TXT:{recvMessage.TXT}", eMsgLevel.l7_HostDebugMessage));
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(286, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"MultiNode received {tDic.MyParts.Count}/{chunkIndex}-{expectedChunkCount} - {recvMessage.PLB.Length} - ID:{chunkGuid} Pending:{pendingTSMCount} TXT:{recvMessage.TXT}", eMsgLevel.l7_HostDebugMessage));
                            }
                            return false;
                        }
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(286, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("CoreComm", $"Internal error processing MultiNode unknown/{chunkIndex}-{expectedChunkCount} - {recvMessage.PLB.Length} - ID:{chunkGuid} Pending:{pendingTSMCount} TXT:{recvMessage.TXT}", eMsgLevel.l1_Error));
                        return false;
                    }
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(286, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("CoreComm", string.Format("Chunking error - ID:{0}", e.ToString()), eMsgLevel.l7_HostDebugMessage));
                }
            }
            return true;
        }


        #region Internal Assembly functions
        internal static string GetAssemblyResourceStr(Assembly a, string pName)
        {
            if (a == null)
                a = cdeGetExecutingAssembly();
            Stream astreamjs = a.GetManifestResourceStream(pName);
            if (astreamjs == null)
            {
                string[] t = a.GetManifestResourceNames();
                for (int i = 0; i < t.Length; i++)
                {
                    if (t[i].EndsWith("." + pName, StringComparison.OrdinalIgnoreCase))
                    {
                        astreamjs = a.GetManifestResourceStream(t[i]);
                        break;
                    }
                }
            }
            if (astreamjs != null)
            {
                using (StreamReader tStrjs = new StreamReader(astreamjs))
                {
                    return tStrjs.ReadToEnd();
                }
            }
            return null;
        }

        internal static Assembly cdeGetExecutingAssembly()
        {
            return Assembly.GetExecutingAssembly();
        }

        internal static byte[] GetAssemblyResource(Assembly a, string pName)
        {
            if (a == null)
            {
                a = cdeGetExecutingAssembly();
            }
            string[] t = a.GetManifestResourceNames();
            pName = pName.Replace('/', '.').Replace('\\', '.');
            pName = (pName.StartsWith(".") ? "" : ".")+pName;
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i].EndsWith(pName, StringComparison.OrdinalIgnoreCase))
                {
                    Stream imgStream = a.GetManifestResourceStream(t[i]);
                    if (imgStream != null)
                    {
#if !CDE_NET35
                        using (MemoryStream ms = new MemoryStream())
                        {
                            imgStream.CopyTo(ms);
                            return ms.ToArray();
                        }
#else
                    byte[] buffer = new byte[TheBaseAssets.MAX_MessageSize[0]];
                    using (MemoryStream ms = new MemoryStream())
                    {
                        int read;
                        while ((read = imgStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }
                        return ms.ToArray();
                    }
#endif
                    }
                }
            }
            return null;
        }

        internal static string GetAssemblyProduct(object plugin)
        {
            return GetAssemblyAttribute<System.Reflection.AssemblyProductAttribute>(plugin)?.Product;
        }

        private static T GetAssemblyAttribute<T>(object plugin) where T : class
        {
            T attribute = null;
            try
            {
                Type pluginType;
                if (plugin is Type)
                {
                    pluginType = plugin as Type;
                }
                else
                {
                    pluginType = plugin.GetType();
                }
                attribute = (pluginType.Assembly.GetCustomAttributes(typeof(T), false)?[0] as T);
            }
            catch (Exception) { }
            return attribute;
        }

        internal static string GetRootAssembly()
        {
            Assembly tAss = cdeGetExecutingAssembly();
            string[] parts = tAss.FullName.Split(',');
            return parts[0];
        }
        #endregion

        #region Internal Process Helpers
        internal static bool cdeRunAsync(string pThreadName, bool TrapExceptions, cdeWaitCallback callBack, int timeout, object pState = null)
        {
            var task = cdeRunTaskAsync(pThreadName, callBack, pState);
            if (timeout > 0)
            {
                if (TrapExceptions)
                {
                    try
                    {
                        task.Wait(timeout);
                    }
                    catch { }
                }
                else
                {
                    task.Wait(timeout);
                }
            }
            else
            {
                if (!TSM.L(eDEBUG_LEVELS.ESSENTIALS))
                {
                    task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            TheBaseAssets.MySYSLOG?.WriteToLog(TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("cdeRunAsync", $"Exception during task execution", eMsgLevel.l1_Error) { PLS = t.Exception?.ToString() }, 5001);
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            return !task.IsCanceled;
        }

        internal static void CloseOrDispose(WaitHandle asyncWaitHandle)
        {
#if !NET35
            asyncWaitHandle?.Dispose();
#else
            try
            {
                asyncWaitHandle?.Close();
            }
            catch { }
#endif
        }

        internal class TaskKPIs
        {
            public int Created;
            public int WaitingForActivation;
            public int WaitingToRun;
            public int Running;
            public int WaitingForChildrenToComplete;
            public int RanToCompletion;
            public int Faulted;
            public int Canceled;
            public TimeSpan fastest;
            public TimeSpan slowest;
            public int maxStartDelay;
            public int LongRunning;
        }

        internal static TaskKPIs GetTaskKpis(TheThing pThing)
        {
            var taskKPIs = new TaskKPIs
            {
                fastest = TimeSpan.MaxValue,
                slowest = TimeSpan.MaxValue,
            };
            DateTime latestCreate = DateTime.MinValue;
            DateTime earliestCreate = DateTime.MaxValue;
            double maxStartDelay = 0;
            var globalTasks = _globalTasks.ToArray();
            foreach (var pendingTask in globalTasks)
            {
                if (pendingTask == null)
                {
                    // Unclear why this happens: race between remove and ToArray()? Null gets inserted somewhere?
                    continue;
                }
                var task = pendingTask?.task;
                bool longRunning = false;
                if (task != null)
                {
                    switch (task?.Status)
                    {
                        case TaskStatus.Created:
                            taskKPIs.Created++;
                            break;
                        case TaskStatus.WaitingForActivation:
                            taskKPIs.WaitingForActivation++;
                            break;
                        case TaskStatus.WaitingToRun:
                            taskKPIs.WaitingToRun++;
                            break;
                        case TaskStatus.Running:
                            taskKPIs.Running++;
                            break;
                        case TaskStatus.WaitingForChildrenToComplete:
                            taskKPIs.WaitingForChildrenToComplete++;
                            break;
                        case TaskStatus.RanToCompletion:
                            taskKPIs.RanToCompletion++;
                            if (pendingTask.createTime > latestCreate)
                            {
                                latestCreate = pendingTask.createTime;
                            }
                            if (pendingTask.createTime < earliestCreate)
                            {
                                earliestCreate = pendingTask.createTime;
                            }
                            _globalTasks.Remove(pendingTask);
                            break;
                        case TaskStatus.Faulted:
                            taskKPIs.Faulted++;
                            if (pendingTask.createTime > latestCreate)
                            {
                                latestCreate = pendingTask.createTime;
                            }
                            if (pendingTask.createTime < earliestCreate)
                            {
                                earliestCreate = pendingTask.createTime;
                            }
                            _globalTasks.Remove(pendingTask);
                            break;
                        case TaskStatus.Canceled:
                            taskKPIs.Canceled++;
                            if (pendingTask.createTime > latestCreate)
                            {
                                latestCreate = pendingTask.createTime;
                            }
                            if (pendingTask.createTime < earliestCreate)
                            {
                                earliestCreate = pendingTask.createTime;
                            }
                            _globalTasks.Remove(pendingTask);
                            break;
                    }
                    if ((task.CreationOptions & TaskCreationOptions.LongRunning) != 0)
                    {
                        longRunning = true;
                        taskKPIs.LongRunning++;
                    }
                }
                else
                {
                    // It's a task that was created externally (i.e. Parallel.ForEach) where the Task instance was not accessible
                    if (pendingTask.endTime != DateTime.MinValue)
                    {
                        taskKPIs.RanToCompletion++;
                        _globalTasks.Remove(pendingTask);
                    }
                    else if (pendingTask.startTime == DateTime.MinValue)
                    {
                        taskKPIs.WaitingForActivation++;
                    }
                    else
                    {
                        taskKPIs.Running++;
                    }
                    if (pendingTask.createTime > latestCreate)
                    {
                        latestCreate = pendingTask.createTime;
                    }
                    if (pendingTask.createTime < earliestCreate)
                    {
                        earliestCreate = pendingTask.createTime;
                    }
                }
                if (pendingTask.startTime != DateTime.MinValue && !longRunning)
                {
                    var startDelay = (pendingTask.startTime - pendingTask.createTime).TotalMilliseconds;
                    if (startDelay > maxStartDelay)
                    {
                        maxStartDelay = startDelay;
                    }
                }
            }
            var now = DateTime.UtcNow;
            taskKPIs.fastest = now - latestCreate;
            taskKPIs.slowest = now - earliestCreate;
            taskKPIs.maxStartDelay = (int) maxStartDelay;

            if (pThing == null) return taskKPIs;
            pThing.SetProperty("TSK_Canceled", taskKPIs.Canceled);
            pThing.SetProperty("TSK_Created", taskKPIs.Created);
            pThing.SetProperty("TSK_Fastest", taskKPIs.fastest);
            pThing.SetProperty("TSK_Faulted", taskKPIs.Faulted);
            pThing.SetProperty("TSK_RanToCompletion", taskKPIs.RanToCompletion);
            pThing.SetProperty("TSK_Running", taskKPIs.Running);
            pThing.SetProperty("TSK_Slowest", taskKPIs.slowest);
            pThing.SetProperty("TSK_WaitingForActivation", taskKPIs.WaitingForActivation);
            pThing.SetProperty("TSK_WaitingForChildrenToComplete", taskKPIs.WaitingForChildrenToComplete);
            pThing.SetProperty("TSK_WaitingToRun", taskKPIs.WaitingToRun);
            pThing.SetProperty("TSK_MaxStartDelay", taskKPIs.maxStartDelay);
            pThing.SetProperty("TSK_LongRunning", taskKPIs.LongRunning);
            return taskKPIs;
        }

        #region internal Event helpers
        internal abstract class RegisteredEventHelperBase<T> where T : System.MulticastDelegate
        {
            private readonly ViewModels.cdeConcurrentDictionary<string, T> MyRegisteredEvents = new ViewModels.cdeConcurrentDictionary<string, T>();

            internal List<string> GetKnownEvents()
            {
                return MyRegisteredEvents.Keys.ToList();
            }

            /// <summary>
            /// Removes all Events
            /// </summary>
            public void ClearAllEvents()
            {
                foreach (var tEventName in MyRegisteredEvents.Keys)
                {
                    MyRegisteredEvents[tEventName] = null;
                }
                MyRegisteredEvents.Clear();
            }

            /// <summary>
            /// Registers a new Event with TheThing
            /// New Events can be registerd and Fired at Runtime
            /// </summary>
            /// <param name="pEventName">Name of the Event to Register</param>
            /// <param name="pCallback">Callback called when the event fires</param>
            public T RegisterEvent(string pEventName, T pCallback)
            {
                if (pCallback == null || string.IsNullOrEmpty(pEventName)) return null;
                return MyRegisteredEvents.AddOrUpdate(pEventName, (key) => pCallback, (key, existingAction) =>
                {
                    existingAction = Delegate.Remove(existingAction, pCallback) as T;
                    existingAction = Delegate.Combine(existingAction, pCallback) as T;
                    return existingAction;
                });
            }

            /// <summary>
            /// Unregisters a callback for a given Event Name
            /// </summary>
            /// <param name="pEventName">Name of the Event to unregister</param>
            /// <param name="pCallback">Callback to unregister</param>
            public bool UnregisterEvent(string pEventName, T pCallback)
            {
                if (!string.IsNullOrEmpty(pEventName) && MyRegisteredEvents.ContainsKey(pEventName))
                {
                    if (pCallback == null)
                        MyRegisteredEvents[pEventName] = null;
                    else
                    {
                        MyRegisteredEvents.AddOrUpdate(pEventName, (key) => null, (key, action) =>
                        {
                            action = Delegate.Remove(action, pCallback) as T;
                            return action;
                        });
                    }
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Returns true if the event specified exists in the Eventing System of TheThing
            /// </summary>
            /// <param name="pEventName"></param>
            /// <returns></returns>
            public bool HasRegisteredEvents(string pEventName)
            {
                return (!string.IsNullOrEmpty(pEventName) && MyRegisteredEvents.ContainsKey(pEventName) && MyRegisteredEvents[pEventName] != null);
            }

            internal bool HasRegisteredEvents()
            {
                return MyRegisteredEvents.Any();
            }

            /// <summary>
            /// Fires the given Event.
            /// Every TheThing can register and Fire any Event on any event.
            /// New Events can be defined at Runtime, registered and fired
            /// </summary>
            /// <param name="pEventName">Name of the Event to Fire</param>
            /// <param name="doFire">Action that will invoke the actual action for the event. This allows for the caller to provide arguments to the invokation.</param>
            protected void FireEvent(string pEventName, Action<T> doFire)
            {
                if (!string.IsNullOrEmpty(pEventName))
                {
                    if (MyRegisteredEvents.TryGetValue(pEventName, out var action))
                    {
                        doFire(action);
                    }
                }
            }
        }
        internal class RegisteredEventHelper<T1> : RegisteredEventHelperBase<Action<T1>>
        {
            /// <summary>
            /// Fires the given Event.
            /// Every TheThing can register and Fire any Event on any event.
            /// New Events can be defined at Runtime, registered and fired
            /// </summary>
            /// <param name="pEventName">Name of the Event to Fire</param>
            /// <param name="sender">this pointer or any other ICDETHing that will be handed to the callback. If set to null, "this" will be used </param>
            /// <param name="pPara">Parameter to be handed with the Event</param>
            /// <param name="FireAsync">If set to true, the callback is running on a new Thread</param>
            public void FireEvent(string pEventName, T1 sender, bool FireAsync, int pFireEventTimeout = -1)
            {
                base.FireEvent(pEventName, (action) => TheCommonUtils.DoFireEvent(action, sender, FireAsync, pFireEventTimeout));
            }

        }
        internal class RegisteredEventHelper<T1, T2> : RegisteredEventHelperBase<Action<T1, T2>>
        {
            /// <summary>
            /// Fires the given Event.
            /// Every TheThing can register and Fire any Event on any event.
            /// New Events can be defined at Runtime, registered and fired
            /// </summary>
            /// <param name="pEventName">Name of the Event to Fire</param>
            /// <param name="sender">this pointer or any other ICDETHing that will be handed to the callback. If set to null, "this" will be used </param>
            /// <param name="pPara">Parameter to be handed with the Event</param>
            /// <param name="FireAsync">If set to true, the callback is running on a new Thread</param>
            public void FireEvent(string pEventName, T1 sender, T2 pPara, bool FireAsync, int pFireEventTimeout = -1)
            {
                base.FireEvent(pEventName, (action) => TheCommonUtils.DoFireEvent(action, sender, pPara, FireAsync, pFireEventTimeout));
            }
        }

        #endregion

        internal static void DoFireEvent<T>(Action<T, TheProcessMessage> action, T Para1, TheProcessMessage pMsg, bool FireAsync, int pFireEventTimeout = 0)
        {
            if (FireAsync)
            {
                if (pFireEventTimeout < 0)
                    pFireEventTimeout = TheBaseAssets.MyServiceHostInfo.EventTimeout;
                if (pFireEventTimeout == 0)
                {
                    TheCommonUtils.cdeRunAsync("cdePEvent", true, (o) => action(Para1, pMsg), null);
                }
                else
                {
                    DoFireEventParallelInternal(action, a =>
                    {
                        var innerAction = (a as Action<T, TheProcessMessage>);
                        if (innerAction == null)
                        {
                            // This should never happen
                        }
                        innerAction?.Invoke(Para1, pMsg);
                    }, pFireEventTimeout);
                }
            }
            else
            {

                try
                {
                    action(Para1, pMsg);
                }
                catch
                {
                    // Repurposing timeout KPIs as FireAsync is (currently) a global setting
                    TheCDEKPIs.IncrementKPI(eKPINames.EventTimeouts);
                    TheCDEKPIs.IncrementKPI(eKPINames.TotalEventTimeouts);
                }
            }
        }

        private static void DoFireEventParallelInternal(Delegate action, Action<Delegate> invoker, int pFireEventTimeout)
        {
            var handlers = action?.GetInvocationList();
            var tCreateTime = DateTime.UtcNow; // UtcNow is significantly faster and we only need elapsed time here
            var result = Parallel.ForEach(handlers, new ParallelOptions { MaxDegreeOfParallelism = 10 }, innerAction =>
            {
                PendingTask pendingTask = null;
                var tStartTime = DateTime.UtcNow; // UtcNow is significantly faster and we only need elapsed time here
                if (TheBaseAssets.MyServiceHostInfo.EnableTaskKPIs)
                {
                    pendingTask = new PendingTask { startTime = tStartTime, createTime = tCreateTime, };
                    _globalTasks.Add(pendingTask);
                }
                invoker(innerAction);

                var now = DateTime.UtcNow;
                if (pendingTask != null)
                {
                    pendingTask.endTime = now;
                }
                var exeSinceCreate = now.Subtract(tCreateTime).TotalMilliseconds;
                if (exeSinceCreate > pFireEventTimeout)
                {
                    var exeSinceStart = (now - tStartTime).TotalMilliseconds;
                    //CODE-REVIEW: this is very dangerous to WriteToLog here especially if this is happening during the systemlog already writing. We either need to exclude "RunAsync" by default from SystemLoggin and have it turned on or keep the log level hight to avoid collisions
#if CDE_NET35 || CDE_NET4
                     TheBaseAssets.MySYSLOG?.WriteToLog(TSM.L(eDEBUG_LEVELS.VERBOSE)?null: new TSM("RunAsync", $"DoFireEventTPM Async timed out {pFireEventTimeout} ms - elapsed: {exeSinceCreate}, since start: {exeSinceStart}", eMsgLevel.l2_Warning), 5002);
#else
                    var outputMsg = innerAction.Method.Name;
                    TheBaseAssets.MySYSLOG?.WriteToLog(TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("RunAsync", $"DoFireEventTPM Async timed out {pFireEventTimeout} ms in Target:{outputMsg} - elapsed: {exeSinceCreate}, since start: {exeSinceStart}", eMsgLevel.l2_Warning), 5002);
#endif
                    TheCDEKPIs.IncrementKPI(eKPINames.EventTimeouts);
                    TheCDEKPIs.IncrementKPI(eKPINames.TotalEventTimeouts);
                }
            });
        }
#if CDE_NET4 || CDE_NET35
        private static readonly TaskCreationOptions defaultTaskCreationOptions = TaskCreationOptions.PreferFairness;
#else
        private static readonly TaskCreationOptions defaultTaskCreationOptions = TaskCreationOptions.DenyChildAttach | TaskCreationOptions.PreferFairness;
#endif
        private class PendingTask
        {
            public Task task;
            public DateTime startTime;
            public DateTime createTime;
            public DateTime endTime;
        }
        private static readonly List<PendingTask> _globalTasks = new List<PendingTask>();

        /// <summary>
        /// Helper to shield from platform differences when returning a value from a function that is conditionally compiled with the async keyword,
        /// </summary>
        /// <typeparam name="T">Type of the task being returned.</typeparam>
        /// <param name="task">Task to be returned from the conditionally compiled async function.</param>
        /// <returns></returns>
        internal static
#if !CDE_NET4
        System.Threading.Tasks.Task<T>
#else
        T
#endif
            TaskOrResult<T>(System.Threading.Tasks.Task<T> task)
        {
#if !CDE_NET4
            return task;
#else
            return task.Result;
#endif
        }

        /// <summary>
        /// Helper to shield from platform differences when returning a value from a function that is conditionally compiled with the async keyword
        /// </summary>
        /// <typeparam name="T">Type of the Task being returned.</typeparam>
        /// <param name="result">Value to be returned from the conditionally compiled async function.</param>
        /// <returns></returns>
        internal static
#if !CDE_NET4
        T
#else
        System.Threading.Tasks.Task<T>
#endif
            TaskOrResult<T>(T result)
        {
#if !CDE_NET4
            return result;
#else
            return TaskFromResult<T>(result);
#endif
        }

        internal static cdePlatform GetAssemblyPlatform(Assembly tAss, bool bAdjustForHostProcess, bool bDiagnostics, out string diagnosticsInfo)
        {
            if (tAss == null)
                tAss = Assembly.GetExecutingAssembly();
            if (tAss == null)
                tAss = typeof(TheCommonUtils).GetType().Assembly;
            diagnosticsInfo = string.Empty;
            tAss.ManifestModule.GetPEKind(out var assemblyPEKind, out var assemblyPlatform);
            var runtimeVersion = tAss.ImageRuntimeVersion;
            string targetFrameworkSku = "";
            if (runtimeVersion.StartsWith("v2.0"))
            {
                // Net 3.5 (can also be 3.0 or 2.0, but there's no easy way to distinguish them and we only care about Net 3.5 anymore)
                targetFrameworkSku = ".NETFramework,Version=v3.5";
            }
            else
            {
#if !CDE_NET35
                // Net 4.x, including .Net Standard
#if !CDE_NET4
                var targetFrameworkData = tAss.GetCustomAttributesData()?.FirstOrDefault(at => at.AttributeType.FullName == typeof(System.Runtime.Versioning.TargetFrameworkAttribute).FullName);
                var nameArg = targetFrameworkData?.ConstructorArguments[0];
                targetFrameworkSku = nameArg?.Value?.ToString();
                //targetFrameworkName = targetFramework.FrameworkName;
#else
                // No real need to distinguish on .Net4 hosts: all plug-ins on a .Net4 C-DEngine will be considered as .Net4 plug-ins
                targetFrameworkSku = ".NETFramework,Version=v4.0";
#endif
#else
                targetFrameworkSku = ".NETFramework,Version=v3.5";
#endif
            }
            var targetFramework = new FrameworkName(targetFrameworkSku);

            if (bDiagnostics)
            {
                diagnosticsInfo += $"AssemblyInfo: {new Uri(tAss.CodeBase).LocalPath},{runtimeVersion},{targetFrameworkSku},{assemblyPEKind},{assemblyPlatform}\r\n";
#if !CDE_NET35
                diagnosticsInfo += (Directory.EnumerateFiles(Path.GetDirectoryName(new Uri(tAss.CodeBase).LocalPath)).Aggregate("", (s, f) => $"{s} {f}"));
#endif
            }
            return GetCDEPlatform(assemblyPlatform, targetFramework, assemblyPEKind, bAdjustForHostProcess);
        }

#if CDE_NET35
        // Just a stub for .Net35: we'll consider all plug-ins loaded into a .Net35 C-DEngine to be .Net35 plug-ins
        class FrameworkName
        {
            public FrameworkName(string sku)
            {
                Identifier = ".NETFramework";
                Version = new Version(3, 5);
                SKU = sku;
            }

            public string Identifier { get; internal set; }
            public Version Version { get; internal set; }
            public string SKU { get; internal set; }
        }
#endif

        private static cdePlatform GetCDEPlatform(ImageFileMachine assemblyPlatform, FrameworkName targetFramework, PortableExecutableKinds pPEKind, bool bAdjustForHostProcess)
        {
            cdePlatform tPluginPlatform = cdePlatform.NOTSET;

            switch (targetFramework.Identifier)
            {
                case ".NETStandard":
                    switch (targetFramework.Version.Major)
                    {
                        case 2:
                            tPluginPlatform = cdePlatform.NETSTD_V20;
                            break;
                        default:
                            // TODO What should we do for 3.x and 1.x?
                            break;
                    }
                    break;
                case ".NETFramework":
                    switch (targetFramework.Version.Major)
                    {
                        case 4:
                            switch (targetFramework.Version.Minor)
                            {
                                case 0:
                                    switch (assemblyPlatform)
                                    {
                                        case ImageFileMachine.AMD64:
                                        case ImageFileMachine.IA64:
                                            tPluginPlatform = cdePlatform.NETV4_64;
                                            break;

                                        case ImageFileMachine.I386:
                                            tPluginPlatform = IsI386Image64Bit(pPEKind, bAdjustForHostProcess) ? cdePlatform.NETV4_64 : cdePlatform.NETV4_32;
                                            break;
                                        default:
                                            break;
                                    }
                                    break;
                                case 5:
                                case 6:
                                case 7:
                                case 8: //We dont plan any special additions for 4.8 - maybe with 5.0 and need to add
                                    switch (assemblyPlatform)
                                    {
                                        case ImageFileMachine.AMD64:
                                        case ImageFileMachine.IA64:
                                            tPluginPlatform = cdePlatform.X64_V3;
                                            break;

                                        case ImageFileMachine.I386:
                                            tPluginPlatform = IsI386Image64Bit(pPEKind, bAdjustForHostProcess) ? cdePlatform.X64_V3 : cdePlatform.X32_V4;
                                            break;
                                        default:
                                            break;
                                    }
                                    break;
                                default:
                                    // TODO What should we do for 4.8 etc.?
                                    break;
                            }
                            break;
                        case 3:
                            if (targetFramework.Version.Minor == 5)
                            {
                                switch (assemblyPlatform)
                                {
                                    case ImageFileMachine.AMD64:
                                    case ImageFileMachine.IA64:
                                        // not supported/does not exist (note X64_V3 denotes .Net 4.5 64 bit)
                                        break;

                                    case ImageFileMachine.I386:
                                        tPluginPlatform = cdePlatform.X32_V3;
                                        break;
                                    default:
                                        break;
                                }
                            }
                            break;
                        default:
                            // not supported
                            break;
                    }
                    break;
            }
            return tPluginPlatform;
        }

        private static bool IsI386Image64Bit(PortableExecutableKinds pPEKind, bool bAdjustForHostProcess)
        {
            bool bIs64Bit;
#if !CDE_NET35
            if (bAdjustForHostProcess)
            {
                bIs64Bit = Environment.Is64BitProcess; // regardless of PEKind, if it's loaded in a 64bit process it's 64bit capable
            }
            else
#endif
            {
                if (
                    ((pPEKind & PortableExecutableKinds.ILOnly) != 0 || (pPEKind & PortableExecutableKinds.PE32Plus) != 0)
                    && (pPEKind & PortableExecutableKinds.Required32Bit) == 0
#if !CDE_NET35 && !CDE_NET4
                                                    && (pPEKind & PortableExecutableKinds.Preferred32Bit) == 0
#endif
                                                )
                    bIs64Bit = true;
                else
                    bIs64Bit = false;
            }

            return bIs64Bit;
        }

        static string GetExceptionMessageWithInnerHelper(Exception e, bool includeStacktrace)
        {
            if (e.InnerException != null)
            {
                return e.Message + (includeStacktrace ? e.StackTrace : "") + " Inner: " + GetAggregateExceptionMessage(e.InnerException, includeStacktrace);
            }
            return e.Message + (includeStacktrace ? e.StackTrace : "");
        }
        #endregion

        #region internal File Helpers
        internal static string SaveBlobToDisk(TSM pMessage, string[] command, string PathAdd)
        {
            byte[] fileBytes = null;
            bool IsImage = false;   //TODO: find a compatible way to create a ThumbNail in UWA, Android and IOS
            string Extension = "";
            string tTargetFileName = command[1];
            string FinalFileName = null;
            if (pMessage.PLB != null && pMessage.PLB.Length > 0)
            {
                fileBytes = pMessage.PLB;
#if PORTABLE
                        if (!tTargetFileName.Contains("\\") && !tTargetFileName.Contains("/"))
#else
                if (!tTargetFileName.Contains(Path.DirectorySeparatorChar) && !tTargetFileName.Contains(Path.AltDirectorySeparatorChar))
#endif
                {
                    // CODE REVIEW: While this is more correct than tTargetFileName.ToUpper().EndsWith("PNG") it is also more stricts (i.e. foo.myPNG will not longer match). ARe there use cases for the less stringent match?
                    var extension = Path.GetExtension(tTargetFileName).ToUpper();
                    if (String.Equals(extension, ".PNG", StringComparison.OrdinalIgnoreCase) || String.Equals(extension, ".JPG", StringComparison.OrdinalIgnoreCase) || String.Equals(extension, ".JPEG", StringComparison.OrdinalIgnoreCase))
                    {
                        Extension = extension;
                        tTargetFileName = Path.Combine("IMAGES", tTargetFileName);
                        IsImage = true;
                    }
                    else if (String.Equals(extension, ".CSS", StringComparison.OrdinalIgnoreCase))
                    {
                        tTargetFileName = Path.Combine("CSS", tTargetFileName);
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(pMessage.PLS))
                {
                    if (pMessage.PLS.StartsWith("data:"))
                    {
                        string[] fParts = cdeSplit(pMessage.PLS, "base64,", false, false);
#if PORTABLE
                        if (!tTargetFileName.Contains("\\") && !tTargetFileName.Contains("/"))
#else
                        if (!!tTargetFileName.Contains(Path.DirectorySeparatorChar) && !tTargetFileName.Contains(Path.AltDirectorySeparatorChar))
#endif
                        {
                            if (fParts[0].Contains("image"))
                            {
                                tTargetFileName = Path.Combine("IMAGES", tTargetFileName);
                                IsImage = true;
                                Extension = Path.GetExtension(tTargetFileName).ToUpper();
                            }
                            else if (fParts[0].Contains("css"))
                                tTargetFileName = Path.Combine("CSS", tTargetFileName);
                        }
                        if (fParts.Length > 1)
                            fileBytes = Convert.FromBase64String(fParts[1]);
                    }
                    else
                    {
                        fileBytes = TheCommonUtils.CUTF8String2Array(pMessage.PLS);
                    }
                }
            }
            if (fileBytes != null && fileBytes.Length > 0 && command.Length > 1)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, string.Format("FilePush requested {0} {1}", command.Length, fileBytes.Length), eMsgLevel.l6_Debug), true);
                string FileToReturn = "";
                try
                {
                    bool IsAnUpdate = false;
                    if (!IsDeviceSenderType(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType) && ISM.TheISMManager.HasISMExtension(tTargetFileName)) //IDST-OK: No Update storage for "Devices"
                    {
                        if (command[0].Equals("CDE_UPDPUSH"))
                            IsAnUpdate = true;
                        if (!tTargetFileName.ToLower().Contains("updates"))
                            tTargetFileName = Path.Combine("Updates", tTargetFileName);
                    }
                    if (command[0].Equals("SCENE"))
                    {
                        tTargetFileName = Path.Combine("Scenes", tTargetFileName);
                    }
                    FinalFileName = tTargetFileName;
                    if (tTargetFileName.Contains("?"))
                        FinalFileName = tTargetFileName.Substring(0, tTargetFileName.IndexOf("?"));
                    if (!string.IsNullOrEmpty(PathAdd))
                        FinalFileName = Path.Combine(PathAdd.ToLower(), FinalFileName);
                    FileToReturn = cdeFixupFileName(FinalFileName);
                    TheCommonUtils.CreateDirectories(FileToReturn);

                    TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM(eEngineName.ContentService, string.Format("FilePush writes to: {0}", FileToReturn), eMsgLevel.l6_Debug), true);

                    System.IO.FileStream _FileStream = new System.IO.FileStream(FileToReturn, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                    _FileStream.Write(fileBytes, 0, fileBytes.Length);
#if CDE_STANDARD   //Core uses Dispose not CLose
                    _FileStream.Dispose();
#else
                    _FileStream.Close();
#endif
                    if (IsImage)
                    {
#if !CDE_STANDARD   //No System.Drawing
                        System.Drawing.Imaging.ImageFormat tFormat = System.Drawing.Imaging.ImageFormat.Jpeg;
                        if (Extension.Equals(".PNG"))
                            tFormat = System.Drawing.Imaging.ImageFormat.Png;
                        byte[] tThumb = CreateImageThumbnail(fileBytes, 75, 75, tFormat);
                        string ThumbLink = FileToReturn.Substring(0, FileToReturn.LastIndexOf('.'));
                        ThumbLink += "_s" + Extension;
                        _FileStream = new System.IO.FileStream(ThumbLink, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                        _FileStream.Write(tThumb, 0, tThumb.Length);
                        _FileStream.Close();
#endif
                    }
                    if (IsAnUpdate && TheBaseAssets.MyApplication.MyISMRoot != null)
                    {
                        string[] tcmds = pMessage.PLS.Split(':');
                        if (tcmds.Length > 1 && TheCommonUtils.CBool(tcmds[0]) && TheBaseAssets.MyServiceHostInfo.cdePlatform.ToString() == tcmds[1])
                        {
                            TheBaseAssets.MyApplication.MyISMRoot.AddPluginPackageForInstall(FileToReturn, true);
                            if (TheBaseAssets.MyServiceHostInfo.AllowRemoteAdministration && TheBaseAssets.MyServiceHostInfo.AllowAutoUpdate && TheBaseAssets.MyApplication?.MyISMRoot?.IsUpdateAvailable() == true)
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(401, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM(eEngineName.ContentService, $"Launching Updater", eMsgLevel.l3_ImportantMessage));
                                TheBaseAssets.MyApplication.MyISMRoot.LaunchUpdater(null, null,null, true);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(454, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM(eEngineName.ContentService, string.Format("Error Writing FILEPUSH {0}", FileToReturn), eMsgLevel.l1_Error, e.ToString()), true);
                }
            }
            return FinalFileName;
        }

        static cdeConcurrentDictionary<string, object> _logFileLocks = null;
        internal static void LogDataToFile(string fileName, Dictionary<string, object> logInfo, string logSource, string sourceItem)
        {
            if (_logFileLocks == null)
            {
                _logFileLocks = new cdeConcurrentDictionary<string, object>();
            }
            var _logFileLock = _logFileLocks.GetOrAdd(fileName, f => new object());
            int retriesLeft = 0;
            do
            {
                try
                {
                    if (retriesLeft > 0)
                    {
                        logInfo["LogRetryCount"] = retriesLeft == 0 ? retriesLeft : 100 - retriesLeft;
                    }
                    var loginfoJson = TheCommonUtils.SerializeObjectToJSONString(logInfo);
                    lock (_logFileLock)
                    {
                        File.AppendAllText(fileName, $"{loginfoJson},\r\n");
                    }
                    //File.AppendAllText("opcclientdata.log", $"{DateTimeOffset.Now:O},{property.Name},0x{((int?)(pValue.WrappedValue.TypeInfo?.BuiltInType)) ?? 0:x4},{pValue.Value},0x{pValue.StatusCode.Code:x8},{timeStampForProperty.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fffZ},{pValue.ServerTimestamp.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.fffZ}\r\n");
                    retriesLeft = 0;
                }
                catch (Exception e)
                {
                    if (retriesLeft <= 0)
                    {
                        retriesLeft = 100;
                    }
                    else
                    {
                        retriesLeft--;
                        if (retriesLeft < 50)
                        {
                            System.Threading.Thread.Sleep(100);
                        }
                        if (retriesLeft <= 0)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(78301, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheOPCTag", $"[{logSource}] Unable to log data to file after retries: {sourceItem}", eMsgLevel.l2_Warning, e.ToString()));
                        }
                    }
                }
            } while (retriesLeft > 0);

        }

        internal static string GetCurrentAppDomainBaseDirWithTrailingSlash()
        {
            var baseDirectory = TheBaseAssets.MyServiceHostInfo.BaseDirectory;
            if (string.IsNullOrEmpty(baseDirectory))
                baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            if (!string.IsNullOrEmpty(baseDirectory))
            {
                if (!baseDirectory.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                    baseDirectory += System.IO.Path.DirectorySeparatorChar.ToString();
            }
            return baseDirectory;
        }

        internal static string GetTargetFolder(bool IsUSB, bool FallbackToHD)
        {
            if (!IsUSB) return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\";
            DriveInfo[] drives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives)
            {
                if (drive.DriveType == DriveType.Removable && drive.IsReady)
                    return drive.RootDirectory.Root.FullName;
            }
            if (FallbackToHD)
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\";
            else
                return null;
        }

        internal static byte[] LoadBlobFromDisk(string tTargetFileName, string pathAdd)
        {
            if (tTargetFileName == null) return null;

            string finalFileName = tTargetFileName;
            if (tTargetFileName.Contains("?"))
                finalFileName = tTargetFileName.Substring(0, tTargetFileName.IndexOf("?"));
            if (!string.IsNullOrEmpty(pathAdd))
                finalFileName = pathAdd.ToLower() + "\\" + finalFileName;
            string fileToReturn = cdeFixupFileName(finalFileName);
            if (!cdeFileExists(fileToReturn)) return null;
            byte[] returnValue = null;
            try
            {
                using (FileStream fr = new FileStream(fileToReturn, FileMode.Open))
                {
                    using (BinaryReader br = new BinaryReader(fr))
                    {
                        returnValue = br.ReadBytes((int)fr.Length);
                    }
                }
            }
            catch (Exception)
            { }
            return returnValue;
        }

        internal static bool DeleteFromDisk(string tTargetFileName, string pathAdd)
        {
            string finalFileName = tTargetFileName;
            if (tTargetFileName.Contains("?"))
                finalFileName = tTargetFileName.Substring(0, tTargetFileName.IndexOf("?"));
            if (!string.IsNullOrEmpty(pathAdd))
                finalFileName = pathAdd.ToLower() + "\\" + finalFileName;
            string fileToReturn = cdeFixupFileName(finalFileName);
            if (!cdeFileExists(fileToReturn)) return false;
            try
            {
                File.Delete(fileToReturn);
            }
            catch
            {
                return false;
            }
            return true;
        }

        internal static string cdeFixupFileName(string pFilePath, bool AllowCacheAccess)
        {
            if (pFilePath == null || string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.BaseDirectory))
            {
                return null;
            }
            string fileToReturn = TheBaseAssets.MyServiceHostInfo.BaseDirectory;
            if (!pFilePath.StartsWith("clientbin", StringComparison.CurrentCultureIgnoreCase))
            {
                fileToReturn = Path.Combine(fileToReturn, "ClientBin");
            }
            if (pFilePath.StartsWith("\\") || pFilePath.StartsWith("/"))
            {
                pFilePath = pFilePath.Substring(1);
            }
            if (cdeIsFileSystemCaseSensitive()) // IsMono())
            {
                fileToReturn = Path.Combine(fileToReturn, pFilePath.ToLower());
                fileToReturn = fileToReturn.Replace("clientbin", "ClientBin"); //Fixup for Mono being case sensitive and our ClientBin is now spelled with C and B
            }
            else
            {
                fileToReturn = Path.Combine(fileToReturn, pFilePath);
            }
            if (fileToReturn.StartsWith("/") || Path.DirectorySeparatorChar == '/')//IsMono())
            {
                fileToReturn = fileToReturn.Replace('\\', '/');
            }
            else
            {
                fileToReturn = fileToReturn.Replace('/', '\\');
            }
            if (!AllowCacheAccess)
            {
                if (fileToReturn.ToLower().Contains("\\cache") || fileToReturn.ToLower().Contains("/cache"))   //do not allow access to cache folder!
                    return "";
            }
            return fileToReturn;
        }

        static bool? _fileSystemCaseSensitive;
        internal static bool cdeIsFileSystemCaseSensitive()
        {
            if (_fileSystemCaseSensitive == null)
            {
                try
                {
#if !CDE_NET35
                    var firstFile = Directory.EnumerateFiles(TheBaseAssets.MyServiceHostInfo.BaseDirectory).FirstOrDefault();
                    if (firstFile != null)
                    {
                        _fileSystemCaseSensitive = !(File.Exists(firstFile.ToLowerInvariant()) && File.Exists(firstFile.ToUpperInvariant()));
                        TheBaseAssets.MySYSLOG?.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheCommonUtils", $"Filesystem case-sensitivity: {_fileSystemCaseSensitive}", eMsgLevel.l4_Message));
                    }
                    else
                    {
                        TheBaseAssets.MySYSLOG?.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommonUtils", "Filesystem case-sensitivity: Unable to determine - assuming case-insensitive.", eMsgLevel.l2_Warning));
                        _fileSystemCaseSensitive = false;
                    }
#else
                    _fileSystemCaseSensitive = false;
#endif
                }
                catch (Exception e)
                {
                    _fileSystemCaseSensitive = false;
                    TheBaseAssets.MySYSLOG?.WriteToLog(2821, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheCommonUtils", "Filesystem case-sensitivity: Unable to determine due to error - assuming case-insensitive.", eMsgLevel.l2_Warning, e.ToString()));
                }
            }
            return _fileSystemCaseSensitive.Value;
        }

#if !CDE_STANDARD   //No System.Drawing
        private static byte[] CreateImageThumbnail(byte[] byteArrayIn, float pWidth, float pHeight, System.Drawing.Imaging.ImageFormat pFormat)
        {
            MemoryStream ms = new MemoryStream(byteArrayIn);
            System.Drawing.Image imgToResize = System.Drawing.Image.FromStream(ms);

            int sourceWidth = imgToResize.Width;
            int sourceHeight = imgToResize.Height;
            float nPercentW = pWidth / sourceWidth;
            float nPercentH = pHeight / sourceHeight;
            float nPercent;
            if (nPercentH < nPercentW)
                nPercent = nPercentH;
            else
                nPercent = nPercentW;

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            System.Drawing.Bitmap b = new System.Drawing.Bitmap(destWidth, destHeight);
            System.Drawing.Graphics g = System.Drawing.Graphics.FromImage((System.Drawing.Image)b);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            g.DrawImage(imgToResize, 0, 0, destWidth, destHeight);
            g.Dispose();

            ms = new MemoryStream();
            ((System.Drawing.Image)b).Save(ms, pFormat);
            return ms.ToArray();
        }

        [System.Runtime.InteropServices.DllImport("Shlwapi.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private extern static bool PathFileExists(StringBuilder path);

        internal static bool cdeFileExists(string pName)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(pName);
            return PathFileExists(builder);
        }
#else
        internal static bool cdeFileExists(string pName)
        {
            return File.Exists(pName);
        }
#endif
        #endregion
    }
}
