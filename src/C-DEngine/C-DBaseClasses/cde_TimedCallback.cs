// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.ViewModels;
using System;
using nsCDEngine.ISM;
using System.Threading;

namespace nsCDEngine.BaseClasses
{
    internal class TheTimedRequest<T>
    {
        public Action<string, T> MyRequest;
        public Timer MyTimer;
        public Guid MagicID;
        public object Cookie;
    }

    /// <summary>
    /// Creates a Callback Class that can be used to track a callback and its cookie
    ///
    /// </summary>
    /// <typeparam name="T">The Class of the object that will be called</typeparam>
    public class TheTimedCallbacks<T>
    {

        private static readonly cdeConcurrentDictionary<string, TheTimedRequest<T>> MyStoreRequests = new cdeConcurrentDictionary<string, TheTimedRequest<T>>();
        // ReSharper disable once StaticMemberInGenericType
        private static readonly object MyStoreRequestsLock = new object();

        /// <summary>
        /// Adds a new callback to the waiting list
        /// </summary>
        /// <param name="pCallBack">The callback to be called</param>
        /// <param name="pTimeOut">A timeout after which the callback will be called if it has not fired before.</param>
        /// <param name="pCookie">A cookie that will be fired with the callback to be sent back to the caller</param>
        /// <returns></returns>
        public static string AddTimedRequest(Action<string, T> pCallBack,int pTimeOut, object pCookie)
        {
            TheTimedRequest<T> tReq = new TheTimedRequest<T>
            {
                MyRequest = pCallBack,
                MagicID = Guid.NewGuid(),
                Cookie = pCookie
            };
            string Magix = tReq.MagicID.ToString();
            SetFailureTimer(tReq,pTimeOut);
            lock (MyStoreRequestsLock)
            {
                if (MyStoreRequests.ContainsKey(tReq.MagicID.ToString()))
                    MyStoreRequests[tReq.MagicID.ToString()] = tReq;
                else
                    MyStoreRequests.TryAdd(tReq.MagicID.ToString(), tReq);
            }
            return Magix;
        }

        internal static void FlushCallback()
        {
            lock (MyStoreRequestsLock)
            {
                MyStoreRequests.Clear();
            }
        }

        /// <summary>
        /// Retrieves a Callback by its MagicID
        /// </summary>
        /// <param name="LastID">MagicID of the callback</param>
        /// <returns></returns>
        public static Action<string, T> GetTimedRequest(string LastID)
        {
            lock (MyStoreRequestsLock)
            {
                TheTimedRequest<T> tReq;
                MyStoreRequests.TryGetValue(LastID, out tReq);
                if (tReq != null)
                {
                    if (tReq.MyTimer != null)
                    {
                        tReq.MyTimer.Dispose();
                        tReq.MyTimer = null;
                    }
                    MyStoreRequests.RemoveNoCare(LastID);
                    return tReq.MyRequest;
                }
            }
            return null;
        }

        /// <summary>
        /// Fires a callback by its MagicID and add as status to the call
        /// </summary>
        /// <param name="LastID">MagicID to retrieve the Callback</param>
        /// <param name="pStatus">Status of the callback - great for sending error codes</param>
        /// <returns></returns>
        public static bool FireTimedRequest(string LastID,string pStatus)
        {
            lock (MyStoreRequestsLock)
            {
                TheTimedRequest<T> tReq;
                MyStoreRequests.TryGetValue(LastID, out tReq);
                if (tReq != null)
                {
                    if (tReq.MyTimer != null)
                    {
                        tReq.MyTimer.Dispose();
                        tReq.MyTimer = null;
                    }
                    MyStoreRequests.RemoveNoCare(LastID);
                    tReq.MyRequest?.Invoke(pStatus, (T)tReq.Cookie);
                    return true;
                }
            }
            return false;
        }
        private static void SetFailureTimer(TheTimedRequest<T> tReq, int pTimeOut)
        {
            Timer tTimer = null;
            if (pTimeOut > 0)
                tTimer = new Timer(sinkRequestFailed, tReq, TheBaseAssets.MyServiceHostInfo.TO.StoreRequestTimeout * 1000, Timeout.Infinite);
            tReq.MyTimer = tTimer;
        }

        private static void sinkRequestFailed(object state)
        {
            TheDiagnostics.SetThreadName("TimedRequestFailed", true);
            TheTimedRequest<T> req = state as TheTimedRequest<T>;
            if (state!=null)
            {
                TheTimedRequest<T> tReq = req;
                lock (MyStoreRequestsLock)
                {
                    if (MyStoreRequests.ContainsKey(tReq.MagicID.ToString()))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(472, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheTimedCallbacks", "Timed-Request Timed Out", eMsgLevel.l2_Warning));
                        MyStoreRequests.RemoveNoCare(tReq.MagicID.ToString());
                        tReq.MyRequest("Timed-Request Timed Out", (T)tReq.Cookie);
                    }
                }
            }
        }

    }
}
