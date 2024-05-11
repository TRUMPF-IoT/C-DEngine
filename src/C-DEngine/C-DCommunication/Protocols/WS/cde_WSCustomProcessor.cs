// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace nsCDEngine.Communication
{
    public class TheWebSocketHooks: TheDataBase
    {
        public void PostToSocket(object sender, byte[] data)
        {
            (sender as TheWSCustomProcessor)?.PostToSocket(null,data,true,false);
        }
    }

    internal class TheWSCustomProcessor : TheWSProcessorBase
    {
        private static bool IsSocketReady(WebSocket ws)
        {
            return ws != null && (ws.State == WebSocketState.Open || ws.State == WebSocketState.Connecting);
        }

        public TheWSCustomProcessor(WebSocket pWS) :base() 
        {
            websocket = pWS;
            mCancelToken = new();
        }

        readonly WebSocket websocket;
        /// <summary>
        /// new in CDE 6.0: Allows to intercept WebSocket Calls other than ISB requests
        /// </summary>
        /// <param name="pRequestData"></param>
        /// <returns></returns>
        public async Task<string> ProcessWS(TheRequestData pRequestData)
        {
            int tMaxMsgSize = 0;
            ArraySegment<byte> buffer;
            byte[] receiveBuffer;
            try
            {
                tMaxMsgSize = TheCommonUtils.GetMaxMessageSize(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.SenderType);
                receiveBuffer = new byte[tMaxMsgSize];
                buffer = new ArraySegment<byte>(receiveBuffer);
                TheBaseAssets.MySYSLOG.WriteToLog(4367, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSProcessor", "New CustomProcessWS thread started ", eMsgLevel.l4_Message));
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4367, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSProcessor", "Failure while starting new CustomProcessWS thread", eMsgLevel.l4_Message, e.ToString()));
                return "Failure";
            }

            bool hasFaulted = false;
            string tCause = "1604:Thread ended";
            try
            {
                while (TheBaseAssets.MasterSwitch && IsSocketReady(websocket))
                {
                    WebSocketReceiveResult receiveResult = await websocket.ReceiveAsync(buffer, mCancelToken.Token);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        tCause = "1605:WebSocket Closed";
                        break;
                    }

                    int offset = receiveResult.Count;
                    bool IsUsingTArray = false;
                    byte[] tPostData = null;
                    if (!receiveResult.EndOfMessage)
                    {
                        List<byte[]> tTelList = new();
                        byte[] tArray = null;
                        while (!receiveResult.EndOfMessage)
                        {
                            if (IsUsingTArray)
                            {
                                var arraySeg = new ArraySegment<byte>(tArray, offset, tMaxMsgSize - offset);
                                receiveResult = await websocket.ReceiveAsync(arraySeg, CancellationToken.None);
                            }
                            else
                            {
                                var arraySeg = new ArraySegment<byte>(receiveBuffer, offset, tMaxMsgSize - offset);
                                receiveResult = await websocket.ReceiveAsync(arraySeg, CancellationToken.None);
                            }

                            if (receiveResult.Count == 0)
                            {
                                if (receiveResult.Count + offset != tMaxMsgSize)
                                    TheBaseAssets.MySYSLOG.WriteToLog(4369,
                                        TSM.L(eDEBUG_LEVELS.OFF)
                                            ? null
                                            : new TSM("ProcessWS",
                                                string.Format("WS Buffer count wrong: Max={0} Offset={1} Count={2}",
                                                    tMaxMsgSize, offset, receiveResult.Count), eMsgLevel.l1_Error));
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

                        if (tTelList.Count > 0)
                            tPostData = new byte[((tTelList.Count - 1) * tMaxMsgSize) + offset];
                        for (int i = 0; i < tTelList.Count - 1; i++)
                        {
                            byte[] tb = tTelList[i];
                            TheCommonUtils.cdeBlockCopy(tb, 0, tPostData, i * tMaxMsgSize, tMaxMsgSize);
                        }

                        if (IsUsingTArray && offset > 0 && tTelList.Count > 0 && tPostData != null)
                        {
                            TheCommonUtils.cdeBlockCopy(tTelList[tTelList.Count - 1], 0, tPostData,
                                (tTelList.Count - 1) * tMaxMsgSize, offset);
                        }
                    }

                    if (!IsUsingTArray)
                    {
                        tPostData = new byte[offset];
                        TheCommonUtils.cdeBlockCopy(receiveBuffer, 0, tPostData, 0, offset);
                    }
                    TheCommCore.CustomWSHooks.FireEvent(pRequestData.RequestUri.PathAndQuery, 
                        new TheProcessMessage { ClientInfo = null, Message=new TSM(eEngineName.ContentService,"INCOMING_WS") { PLB=tPostData }, Topic = pRequestData.RequestUri.PathAndQuery, Cookie = this }, true);
                }
            }
            catch (OperationCanceledException)
            {
                hasFaulted = true;
            }
            catch (Exception eee)
            {
                hasFaulted = true;
                if (TheBaseAssets.MasterSwitch)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4369, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("ProcessWS", $"CustomProcessWS Loop has failed because WebSocket was closed during ReceiveAsync.", eMsgLevel.l1_Error));
                }
            }
            if (hasFaulted || (websocket != null && websocket.State != WebSocketState.Closed))
            {
                tCause = $"1607:WebSocket Faulted:{hasFaulted} WS State:{(websocket != null ? websocket.State.ToString() : "is null")}";
            }
            return tCause;
        }

        private readonly object _postToSocketLock = new();

        internal override void PostToSocket(TheDeviceMessage pMsg, byte[] pPostBuffer, bool pSendAsBinary, bool IsInitialConnect)
        {
            if (!TheBaseAssets.MasterSwitch || pPostBuffer == null || pPostBuffer.Length == 0)
                return;
            bool HasFaulted = false;
            lock (_postToSocketLock)
            {
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
                        ArraySegment<byte> outputBuffer = new ArraySegment<byte>(pPostBuffer);
                        Task tTask = websocket.SendAsync(outputBuffer, pSendAsBinary ? WebSocketMessageType.Binary : WebSocketMessageType.Text, true, TheBaseAssets.MasterSwitchCancelationToken);
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
                    TheBaseAssets.MySYSLOG.WriteToLog(43610, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSProcessor", "WebSocketServer-PostToSocket Error:", eMsgLevel.l1_Error, ((int)TheBaseAssets.MyServiceHostInfo.DebugLevel > 1 ? e.ToString() : e.Message)));
                    errorMsg = $"PostToSocket failed: {e.Message}";
                    HasFaulted = true;
                }
                if (HasFaulted)
                {
                    Shutdown(true, $"1610:{errorMsg}");
                }
            }
        }
    }
}
