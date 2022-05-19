// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using System;
using System.Net;
using System.Net.Sockets;

namespace nsCDEngine.Communication.HttpService
{
    internal class cdeMidiHttpService : IDisposable
    {
        protected int mPort;
        internal bool IsActive;
        internal bool IsHttpListener;
        private TcpListener mListener;
        private string MyHttpUrl;
        private HttpListener mHttpListener;

        public void Dispose()
        {
            mHttpListener?.Close();
            mHttpListener = null;
        }

        public bool Startup(ushort pPort, bool IsOnThread)
        {
            if (pPort == 0) //new in 4.211: If no MyStationPort is specified, Webserver is disabled and cdeNodeType falls back to "Active" (outbound only)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4340, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpMidiServer", "WebServer NOT started as no station Port was specified", eMsgLevel.l3_ImportantMessage));
                return false;
            }
            if (!TheBaseAssets.MyServiceHostInfo.UseTcpListenerInsteadOfHttpListener)
            {
                try
                {
                    mHttpListener = new HttpListener();
#if !CDE_NET35
                    Uri tUri = new Uri(TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false));
#else
                Uri tUri = TheCommonUtils.CUri(TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false), false);
#endif
                    MyHttpUrl = tUri.Scheme + "://*"; // +tUri.Host;
                    if ((tUri.Scheme.Equals("https") && tUri.Port != 443) || (tUri.Scheme.Equals("http") && tUri.Port != 80))
                        MyHttpUrl += ":" + pPort; // tUri.Port;
                    MyHttpUrl += "/";
                    mHttpListener.Prefixes.Add(MyHttpUrl);
                    mHttpListener.Start();
                    TheBaseAssets.MySYSLOG.WriteToLog(4340, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpMidiServer", $"HttpListener started listening on port: {pPort}", eMsgLevel.l3_ImportantMessage));
                    IsHttpListener = true;
                    IsActive = true;
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4341, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpMidiServer", $"HttpListener on port:{pPort} failed to Start", eMsgLevel.l2_Warning, e.ToString()));
                    mHttpListener = null;
                }
            }
            else
            {
                mHttpListener = null;
            }
            if (mHttpListener == null)
            {
                try
                {
                    mPort = pPort;
                    mListener = new TcpListener(IPAddress.Any, mPort);
                    mListener.Start();
                    if (TheBaseAssets.MyServiceHostInfo.FailOnAdminCheck)  //New in 4.207: This was the original intension of the IgnoreAdminCheck (Bug#1544). In order to keep the current behavior the Flag was inverted
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(4342, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpMidiServer", "TcpListener started but HttpListener Failed. This is an indicator that this node is not running As Administrator. Please restart with Admin rights or set IgnoreAdminCheck=true - Client Certificates WILL NOT WORK!", eMsgLevel.l3_ImportantMessage, $"Port: {pPort}"));
                        return false;
                    }
                    TheBaseAssets.MySYSLOG.WriteToLog(4342, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpMidiServer", $"TcpListener started on port:{pPort} (Failed to start HttpListener - Not run as admin?) - IgnoreAdminCheck is true - Node will continue in User-Mode but Client Certificates WILL NOT WORK!", eMsgLevel.l3_ImportantMessage));
                    IsActive = true;
                }
                catch (Exception ee)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4343, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpMidiServer", $"HttpMidiServer:TcpListener start Failed - Port Conflict? There might be another application/server using Port {mPort}", eMsgLevel.l2_Warning, $"{ee.ToString()}"));
                    return IsActive;
                }
            }
            TheCommonUtils.cdeRunTaskAsync("MidiWebServer - Processing Thread", o => // new Thread(() =>
            {
                while (IsActive && TheBaseAssets.MasterSwitch)
                {
                    try
                    {
                        if (mHttpListener != null)
                        {
                            HttpListenerContext context = mHttpListener.GetContext();
#if CDE_USEWSS8
                            TheBaseAssets.MySYSLOG.WriteToLog(4343, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("HttpMidiServer", $"Incoming Request. IsWebSocketRequest:{context.Request.IsWebSocketRequest}", eMsgLevel.l3_ImportantMessage));
                            if (!TheBaseAssets.MyServiceHostInfo.DisableWebSockets && TheBaseAssets.MyServiceHostInfo.MyStationWSPort > 0 && TheBaseAssets.MyServiceHostInfo.MyStationWSPort == TheBaseAssets.MyServiceHostInfo.MyStationPort
                               && context.Request.IsWebSocketRequest)
                            {
                                if (!TheBaseAssets.MyServiceHostInfo.DisableWebSockets && TheBaseAssets.MyServiceHostInfo.MyStationWSPort > 0) //TheBaseAssets.MyServiceHostInfo.MyStationWSPort==TheBaseAssets.MyServiceHostInfo.MyStationPort
                                {
                                    TheCommonUtils.cdeRunAsync("WSWait4AcceptThread", false, async oo =>
                                    {
                                        try
                                        {
                                            await TheWSServer8.WaitForWSAccept(context);
                                        }
                                        catch (Exception e)
                                        {
                                            TheBaseAssets.MySYSLOG.WriteToLog(4344, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("TheWSServer", "Error During WSAccept", eMsgLevel.l1_Error, e.ToString()));
                                            IsActive = false;
                                        }
                                    });
                                }
                            }
                            else
#endif
                            {
                                TheCommonUtils.cdeRunAsync("MidiWebServer-HttpProcessing", false, (p) =>
                                {
                                    //TheSystemMessageLog.ToCo("HTTP Connected and processing", true);
                                    var tmyProcessor = new cdeHttpProcessor();
                                    tmyProcessor.ProcessHttpRequest(p as HttpListenerContext);
                                }, context);
                            }
                        }
                        else
                        {
                            TcpClient s = mListener.AcceptTcpClient();
                            TheCommonUtils.cdeRunAsync("MidiWebServer-Processing", false, p =>
                            {
                                var tmyProcessor = new cdeHttpProcessor();
                                //TheSystemMessageLog.ToCo("TCP Connected and processing", true);
                                tmyProcessor.ProcessRequest(p as TcpClient, this);
                            }, s);
                        }
                    }
                    catch (Exception e)
                    {
                        if (TheBaseAssets.MasterSwitch)
                        {
                            if (!(e is HttpListenerException))
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(4345, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("HttpMidiServer", "Failed - Will Stop!", eMsgLevel.l1_Error, e.ToString()));
                            }
                            else
                            {
                                TheBaseAssets.MySYSLOG.WriteToLog(4345, TSM.L(eDEBUG_LEVELS.ESSENTIALS) ? null : new TSM("HttpMidiServer", "Failed - Will Stop!", eMsgLevel.l4_Message, e.ToString()));
                            }
                        }
                        IsActive = false;
                    }
                }
                ShutDown();
            }, null, true); //.Start();
            return IsActive;
        }

        public void ShutDown()
        {
            IsActive = false;
            if (mHttpListener != null)
            {
                try
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(4345, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("HttpMidiServer", "Stopping Http Listener", eMsgLevel.l6_Debug));
                    mHttpListener.Stop();
                    mHttpListener.Prefixes.Remove(MyHttpUrl);
                }
                catch
                {
                    //Ignored
                }
                mHttpListener = null;
            }
            if (mListener != null)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(4345, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("HttpMidiServer", "Stopping Listener", eMsgLevel.l6_Debug));
                mListener.Stop();
                mListener = null;
            }
        }
    }
}



