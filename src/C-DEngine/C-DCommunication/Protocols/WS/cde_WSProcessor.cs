// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

#if CDE_USEWSS8
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using nsCDEngine.ISM;
using System.Security.Cryptography.X509Certificates;
using nsCDEngine.Security;
using System.Net.WebSockets;

namespace nsCDEngine.Communication
{
    internal class TheWSProcessor8: TheWSProcessorBase
    {

        static readonly cdeConcurrentDictionary<WebSocket, TheWSProcessor8> allSockets = new cdeConcurrentDictionary<WebSocket, TheWSProcessor8>();

        public TheWSProcessor8(object wsc)
        {
            websocket = wsc as WebSocket;
            if (websocket != null)
            {
                while (!allSockets.TryAdd(websocket, this))
                {
                    if (allSockets.TryRemove(websocket, out TheWSProcessor8 wsProcessor))
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4363, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSProcessor8", "Attempt to create second TheWSProcessor8 instance for the same websocket!", eMsgLevel.l2_Warning, wsProcessor.IsActive.ToString()));
                    }
                }
            }
        }

        protected override void iDispose()
        {
            if (!IsClient && websocket!=null && !allSockets.RemoveNoCare(websocket))
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4364, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSProcessor8", "Disposing TheWSProcessor8 instance twice?", eMsgLevel.l2_Warning));
            }
            var myWebSocket = Interlocked.CompareExchange(ref websocket, null, websocket);
            if (myWebSocket != null)
            {
                try
                {
                    myWebSocket.Dispose();
                }
                catch { }
            }
        }

        private WebSocket websocket;
        bool ConnectSuccess;

        internal override bool Connect(TheQueuedSender pSender)
        {
            if (pSender == null /*|| pSender.MyTargetNodeChannel==null*/)
                return false;
            if (pSender.IsConnected)
                return true;

            IsClient = true;
            if (MyQSender == null)
                MyQSender = pSender;

            var _MyTargetNodeChannel = pSender?.MyTargetNodeChannel;
            Uri TargetUri = TheCommonUtils.CUri(_MyTargetNodeChannel?.TargetUrl, true);
            if (TargetUri == null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("WSProcessor", "Invalid Target URL", eMsgLevel.l1_Error, $"{_MyTargetNodeChannel?.ToString()}"));
                return false;
            }
            OwnerNodeID = _MyTargetNodeChannel.cdeMID;
            string TargetUriPath = TargetUri.AbsolutePath;
            Uri tTarget = new Uri(TargetUri, TheBaseAssets.MyScopeManager.GetISBPath(TargetUriPath, TheCommonUtils.GetOriginST(_MyTargetNodeChannel), _MyTargetNodeChannel.SenderType, 1, Guid.Empty, true));
            tTarget = tTarget.SetWSInfo(tTarget.Port, "");

            string connectFailureReason = "";
            try
            {
                IsActive = true;
                CloseFired = false;
                ConnectSuccess = false;
                ClientWebSocket wsClientSocket = new ClientWebSocket();
                websocket = wsClientSocket;
                if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUrl))
                {
                    try
                    {
                        System.Net.NetworkCredential tNet = null;
                        if (!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUID))
                        {
                            tNet = new System.Net.NetworkCredential(TheBaseAssets.MyServiceHostInfo.ProxyUID, TheBaseAssets.MyServiceHostInfo.ProxyPWD);
                        }
                        if (wsClientSocket.Options != null)
                        {
                            wsClientSocket.Options.Proxy = new System.Net.WebProxy(TheBaseAssets.MyServiceHostInfo.ProxyUrl, true, null, tNet);
                            TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("WSProcessor", $"Web Proxy set: {TheBaseAssets.MyServiceHostInfo.ProxyUrl} UID: {!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyUID)} PWD: {!string.IsNullOrEmpty(TheBaseAssets.MyServiceHostInfo.ProxyPWD)}", eMsgLevel.l4_Message));
                        }
                    }
                    catch (Exception e)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("WSProcessor", "Error setting Proxy credentials:", eMsgLevel.l1_Error, e.ToString()));
                    }
                }
                if (!string.IsNullOrEmpty(_MyTargetNodeChannel?.ClientCertificateThumb))
                {
                    try
                    {
                        X509Certificate2Collection cert = TheCertificates.GetClientCertificatesByThumbprint(_MyTargetNodeChannel?.ClientCertificateThumb);
                        if (cert?.Count < 1)
                        {
                            TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("WSProcessor", $"Error setting Client Certificate - Requested Cert with Thumbprint {_MyTargetNodeChannel?.ClientCertificateThumb} not found", eMsgLevel.l1_Error));
                            return false;
                        }
                        wsClientSocket.Options.ClientCertificates = cert;
                    }
                    catch (Exception certex)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("WSProcessor", "Error setting Client Certificate", eMsgLevel.l1_Error, certex.ToString()));
                        return false;
                    }
                }
                Task myTask = wsClientSocket.ConnectAsync(tTarget, CancellationToken.None);
                myTask.Wait(TheBaseAssets.MyServiceHostInfo.TO.WsTimeOut*12);
                if (myTask.IsCompleted)
                {
                    // CODE REVIEW MH: ProcessWS synchronously can run for quite a bit until the first await. Should we start this as a new task?
                    ConnectSuccess = true;
                    var taskNoWait = ProcessWS(); //Sets Connecting and Connected when ready
                }
                else
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4365, new TSM("WSProcessor", $"WSClient Connect Request timed out Task:{myTask.Status}", eMsgLevel.l2_Warning));
                    connectFailureReason += "Connect Request Timeout";
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4365, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("WSProcessor", "ClientConnect Request Failed:", eMsgLevel.l1_Error, e.ToString()));
                connectFailureReason += TheCommonUtils.GetAggregateExceptionMessage(e, false);
            }
            if (ConnectSuccess) //This prevents double shutdown as the eventconnecte with ConnectSuccess=false will cause a shutdown again
                eventConnected?.Invoke(this, ConnectSuccess, $"1602:{connectFailureReason}");
            else
                Shutdown(true, $"1603:WSConnect was not successful ({connectFailureReason})");
            return ConnectSuccess;
        }

        private static bool IsSocketReady(WebSocket ws)
        {
            return ws!=null && (ws.State == WebSocketState.Open || ws.State == WebSocketState.Connecting);
        }

        public async Task ProcessWS()
        {
            TheDiagnostics.SetThreadName("WSProcessWS");
            if (MyQSender == null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4366, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSProcessor", "ProcessWS - no QSender Specified! ", eMsgLevel.l1_Error));
                return;
            }
            int tMaxMsgSize = 0;
            ArraySegment<byte> buffer;
            byte[] receiveBuffer;
            try
            {
                if (IsClient)
                    tMaxMsgSize = TheCommonUtils.GetMaxMessageSize(MyQSender.MyTargetNodeChannel.SenderType);
                else
                    tMaxMsgSize = TheCommonUtils.GetMaxMessageSize(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType);
                receiveBuffer = new byte[tMaxMsgSize];
                buffer = new ArraySegment<byte>(receiveBuffer);
                TheBaseAssets.MySYSLOG.WriteToLog(4367, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSProcessor", "New ProcessWS thread started ", eMsgLevel.l4_Message));
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4367, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSProcessor", "Failure while starting new ProcessWS thread", eMsgLevel.l4_Message, e.ToString()));
                return;
            }

            CloseFired = false;
            bool HasFaulted = false;
            string tCause = "1604:Thread ended";
            try
            {
                if (IsSocketReady(websocket))
                {
                    if (IsClient)
                    {
                        //MyQSender.IsConnected = true;
                        byte[] tSendBuffer = TheCommCore.SetConnectingBuffer(MyQSender);
                        PostToSocket(null, tSendBuffer, false,true);
                    }
                }

                while (TheBaseAssets.MasterSwitch && IsActive && IsSocketReady(websocket))
                {
                    WebSocketReceiveResult receiveResult = await websocket.ReceiveAsync(buffer, CancellationToken.None);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        tCause = "1605:WebSocket Closed";
                        break;
                    }
                    //LogBufferToFile("wsinputlog.dat", buffer.Array, buffer.Offset, receiveResult.Count);
                    if (MyQSender == null || !MyQSender.IsAlive)
                    {
                        tCause = "1606:QSender No longer alive";
                        break;
                    }

                    int offset = receiveResult.Count;
                    bool IsUsingTArray = false;
                    byte[] tPostData = null;
                    int tPostDataLength = 0;
                    if (receiveResult.EndOfMessage == false)
                    {
                        List<byte[]> tTelList = new List<byte[]>();
                        byte[] tArray = null;
                        while (receiveResult.EndOfMessage == false)
                        {
                            if (IsUsingTArray)
                            {
                                var arraySeg = new ArraySegment<byte>(tArray, offset, tMaxMsgSize - offset);
                                receiveResult = await websocket.ReceiveAsync(arraySeg, CancellationToken.None);
                                //LogBufferToFile("wsinputlog.dat", arraySeg.Array, arraySeg.Offset, receiveResult.Count);
                            }
                            else
                            {
                                var arraySeg = new ArraySegment<byte>(receiveBuffer, offset, tMaxMsgSize - offset);
                                receiveResult = await websocket.ReceiveAsync(arraySeg, CancellationToken.None);
                                //LogBufferToFile("wsinputlog.dat", arraySeg.Array, arraySeg.Offset, receiveResult.Count);
                            }
                            if (receiveResult.Count == 0)
                            {
                                if (receiveResult.Count+offset!=tMaxMsgSize)
                                    TheBaseAssets.MySYSLOG.WriteToLog(4369, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("ProcessWS", string.Format("WS Buffer count wrong: Max={0} Offset={1} Count={2}", tMaxMsgSize,offset,receiveResult.Count), eMsgLevel.l1_Error));
                                tArray = new byte[tMaxMsgSize];
                                if (!IsUsingTArray)
                                    tTelList.Add(receiveBuffer);
                                tTelList.Add(tArray);
                                IsUsingTArray = true;
                                offset = 0;
                            }
                            else
                                offset += receiveResult.Count;
                        }
                        if (tTelList.Count>0)
                            tPostData = new byte[((tTelList.Count-1) * tMaxMsgSize) + offset];
                        tPostDataLength = 0;
                        for (int i = 0; i < tTelList.Count - 1; i++)
                        {
                            byte[] tb = tTelList[i];
                            TheCommonUtils.cdeBlockCopy(tb, 0, tPostData, i * tMaxMsgSize, tMaxMsgSize);
                            tb = null;
                        }
                        if (IsUsingTArray && offset > 0 && tTelList.Count > 0 && tPostData!=null)
                        {
                            TheCommonUtils.cdeBlockCopy(tTelList[tTelList.Count - 1], 0, tPostData, (tTelList.Count - 1) * tMaxMsgSize, offset);
                            tPostDataLength = tPostData.Length;
                        }
                        //tTelList.Clear();
                    }
                    if (!IsUsingTArray)
                    {
                        tPostData = new byte[offset];
                        TheCommonUtils.cdeBlockCopy(receiveBuffer, 0, tPostData, 0, offset);
                        tPostDataLength = offset;
                    }
                    //LogBufferToFile("wsinputbactchedlog.dat", tPostData, 0, tPostData.Length);
                    TheCommonUtils.cdeRunAsync("ProcessFromWS", true,(o)=> ProcessIncomingData(null,(byte [])o, ((byte[])o).Length),tPostData);
                }
            }
            catch (Exception eee)
            {
                HasFaulted = true;
                if (eee.Source=="System")
                    TheBaseAssets.MySYSLOG.WriteToLog(4369, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("ProcessWS", "ProcessWS Loop has failed because WebSocket was closed during ReceiveAsync.", eMsgLevel.l1_Error));
                else
                    TheBaseAssets.MySYSLOG.WriteToLog(4369, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("ProcessWS", "ProcessWS Loop has failed.", eMsgLevel.l1_Error, eee.ToString()));
            }
            TheBaseAssets.MySYSLOG.WriteToLog(4369, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("ProcessWS", $"ProcessWS Loop for {OwnerNodeID} has ended. Faulted:{HasFaulted} HasWS:{websocket != null}", eMsgLevel.l3_ImportantMessage));
            ConnectSuccess = false;
            if (HasFaulted || (websocket != null && websocket.State != WebSocketState.Closed))
            {
                tCause = string.Format("1607:WebSocket Faulted:{0} WS State:{1}",HasFaulted,websocket!=null?websocket.State.ToString():"is null");
            }
            Shutdown(true, tCause);
        }

        //private void LogBufferToFile(string filePath, byte[] buffer, int offset, int count)
        //{
        //    using (var file = new System.IO.FileStream(filePath, System.IO.FileMode.Append, System.IO.FileAccess.Write))
        //    {
        //        string decomp = null;
        //        if (buffer[offset] != '[')
        //        {
        //            try
        //            {
        //                decomp = TheCommonUtils.cdeDecompressToString(buffer, offset, count);
        //                decomp = $"{DateTimeOffset.Now}:{decomp}\r\n";
        //            }
        //            catch
        //            {
        //                decomp = null;
        //            }

        //        }
        //        if (decomp == null)
        //        {
        //            var bytes = Encoding.UTF8.GetBytes($"{DateTimeOffset.Now}:");
        //            file.Write(bytes, 0, bytes.Length);

        //            file.Write(buffer, offset, count);

        //            bytes = Encoding.UTF8.GetBytes($"\r\n");
        //            file.Write(bytes, 0, bytes.Length);
        //        }
        //        else
        //        {
        //            var bytes = Encoding.UTF8.GetBytes(decomp);
        //            file.Write(bytes, 0, bytes.Length);
        //        }
        //        file.Flush();
        //    }
        //}

        private readonly object _postToSocketLock = new object();

        internal override void PostToSocket(TheDeviceMessage pMsg, byte[] pPostBuffer, bool SendAsBinary, bool IsInitialConnect)
        {
            TheDiagnostics.SetThreadName("WSPostToSocket:" + ((MyQSender?.MyTargetNodeChannel!=null) ? MyQSender.MyTargetNodeChannel.ToString():"DEAD"));
            if (MyQSender?.MyTargetNodeChannel==null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4369, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("ProcessWS", $"QSender8 not Has not QSender or TargetChannel {MyQSender}", eMsgLevel.l1_Error));
                Shutdown(true, "1608:WS8 has not QSender but is Posting - illegal state");
                return;
            }

            if (MyQSender != null && !MyQSender.IsConnected && !IsInitialConnect)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4369, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("ProcessWS", "QSender8 not connected but Posting - illegal state", eMsgLevel.l1_Error));
                Shutdown(true, "1609:QSender8 not connected but Posting - illegal state");
                return;
            }

            bool HasFaulted = false;
            lock (_postToSocketLock)
            // The lock needs to be at the websocket level: assumption is that there is at most one WSProcessor per socket!
            {
                if (!IsActive || !TheBaseAssets.MasterSwitch)
                    return;

                ProcessingAllowed = false;
                string errorMsg = "";
                try
                {
                    if (!IsSocketReady(websocket))
                    {
                        HasFaulted = true;
                        errorMsg = "Socket Not Ready";
                    }
                    else
                    {
                        // TODO MH - Move this outside of the lock so we can minimize the lock time? Or are there ordering constraints on the serialization for some reason?
                        ArraySegment<byte> outputBuffer;
                        if (pPostBuffer != null)
                        {
                            TheCDEKPIs.IncrementKPI(eKPINames.QKBSent, pPostBuffer.Length);
                            outputBuffer = new ArraySegment<byte>(pPostBuffer);
                        }
                        else
                        {
                            string tStr = TheCommonUtils.SerializeObjectToJSONString(pMsg);
                            TheCDEKPIs.IncrementKPI(eKPINames.QKBSent, tStr.Length);
                            outputBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(tStr));
                        }
                        //LogBufferToFile("wsoutputputlog", outputBuffer.Array, outputBuffer.Offset, outputBuffer.Count);
                        Task tTask = websocket.SendAsync(outputBuffer, SendAsBinary ? WebSocketMessageType.Binary : WebSocketMessageType.Text, true, TheBaseAssets.MasterSwitchCancelationToken);
                        tTask.Wait(TheBaseAssets.MyServiceHostInfo.TO.WsTimeOut * 10);
                        if (!tTask.IsCompleted)
                        {
                            var timeoutValue = TheBaseAssets.MyServiceHostInfo.TO.WsTimeOut * 10;
                            TheBaseAssets.MySYSLOG.WriteToLog(43610, new TSM("TheWSProcessor", $"WebSocketServer-PostToSocket error: PostAsync Timeout {tTask.Status} after {timeoutValue} ms for {MyQSender?.MyTargetNodeChannel}", eMsgLevel.l1_Error));
                            errorMsg = $"SendAsync timed out after {timeoutValue} ms";
                            HasFaulted = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(43610, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSProcessor", "WebSocketServer-PostToSocket Error:", eMsgLevel.l1_Error,((int)TheBaseAssets.MyServiceHostInfo.DebugLevel>1? e.ToString():e.Message)));
                    errorMsg = $"PostToSocket failed: {e.Message}";
                    HasFaulted = true;
                }
                if (HasFaulted)
                {
                    Shutdown(true,$"1610:{errorMsg}");
                }
                //IsPosting = false;
                if (mre != null)
                    ProcessingAllowed = true;
            }
        }



        public override void Shutdown(bool FireEvent, string pReason)
        {
            base.Shutdown(FireEvent, pReason);
            try
            {
                TheBaseAssets.MySYSLOG.WriteToLog(43611, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheWSServer", $"WS Server for {OwnerNodeID} CloseFired:{CloseFired} Reason: {pReason}", eMsgLevel.l3_ImportantMessage));
                try
                {
                    if (!IsClient && websocket != null) // && IsSocketReady(websocket))
                    {
                        Task tTask = websocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
                        tTask.Wait(TheBaseAssets.MyServiceHostInfo.TO.WsTimeOut);
                        if (!tTask.IsCompleted)
                            TheBaseAssets.MySYSLOG.WriteToLog(43612, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSServer", $"WebSocketServer-Shutdown Warning: CloseAsync did not finish in time {tTask.Status}", eMsgLevel.l2_Warning));
                    }
                }
                catch {
                    //ignored
                }
                if (FireEvent && !CloseFired && eventClosed != null) //FireEvent &&
                {
                    CloseFired = true;
                    eventClosed(pReason);
                }
                try
                {
                    Dispose();
                }
                catch
                {
                    //ignored
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(43612, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSServer", "WebSocketServer-Shutdown Error", eMsgLevel.l1_Error, e.ToString()));
            }
        }
    }
}
#endif