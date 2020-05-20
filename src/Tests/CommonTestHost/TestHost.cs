// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using NUnit.Framework;

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Security;
using nsCDEngine.ViewModels;
using nsCDEngine.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
#if OPCUASERVER
using CDMyOPCUAServer.ViewModel;
#endif
namespace C_DEngine.Tests.TestCommon
{
    public class TestHost
    {
        protected static TheBaseApplication MyBaseApplication;
        protected static TheThing myContentService;
        /// <summary>
        /// Starts the one and only cdeEngine host that can be used by unit tests (no support for multiple hosts at this point)
        /// </summary>
        /// <returns>true: new host was created. false: existing host was used; may not be the one expected if some (rogue) unit test created a different host.</returns>
        static int activeHosts = 0;
        static object appStartLock = new object();
        static public bool StartHost()
        {
            // TODO Figure out how to launch multiple hosts without killing the process
            lock (appStartLock)
            {
                if (Interlocked.Increment(ref activeHosts) != 1)
                {
                    TestContext.Out.WriteLine($"{DateTimeOffset.Now} Not starting host: already running");
                    return false;
                }
                else
                {
                    TestContext.Out.WriteLine($"{DateTimeOffset.Now} Starting host");
                }

                if (TheBaseAssets.MyApplication != null)
                {
                    TestContext.Out.WriteLine($"{DateTimeOffset.Now} Host already started");
                    return false;
                }

                //Assert.IsTrue(TheBaseAssets.MyApplication == null, "Not starting test host: BaseApplication already created.");

                TheScopeManager.SetApplicationID("/cVjzPfjlO;{@QMj:jWpW]HKKEmed[llSlNUAtoE`]G?"); //SDK Non-Commercial ID. FOR COMMERCIAL APP GET THIS ID FROM C-LABS!

                ushort port = 8716;
                // MSTest does not stop the Net3.5 host before starting the Net4.5 host, so need to use different ports
                var connections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
                while (connections.FirstOrDefault(c => c.Port == port) != null)
                {
                    port += 2;
                }


                TheBaseAssets.MyServiceHostInfo = new TheServiceHostInfo(cdeHostType.Application)
                {
                    ApplicationName = "My-Relay",                                   //Friendly Name of Application
                    cdeMID = Guid.NewGuid(),     //TODO: Give a Unique ID to this Host Service
                    Title = "My-Relay (C) C-Labs 2013-2017",                   //Title of this Host Service
                    ApplicationTitle = "My-Relay Portal",                           //Title visible in the NMI Portal
                    CurrentVersion = 1.0001,                                        //Version of this Service, increase this number when you publish a new version that the store displays the correct update icon
                    DebugLevel = eDEBUG_LEVELS.ESSENTIALS,                                 //Define a DebugLevel for the SystemLog output.
                    SiteName = "http://cloud.c-labs.com",                           //Link to the main Cloud Node of this host. this is not required and for documentation only

                    ISMMainExecutable = "OPCUAClientUnitTest",                        //Name of the executable (without .exe)

                    LocalServiceRoute = "LOCALHOST",                                     //Will be replaced by the full DNS name of the host during startup.

                    MyStationPort = port,                   //Port for REST access to this Host node. If your PC already uses port 80 for another webserver, change this port. We recommend using Port 8700 and higher for UPnP to work properly.
                    MyStationWSPort = (ushort)(port + 1),                 //Enables WebSockets on the station port. If UseRandomDeviceID is false, this Value cannot be changed here once the App runs for the first time. On Windows 8 and higher running under "Adminitrator" you can use the same port
                };

                #region Args Parsing
                Dictionary<string, string> ArgList = new Dictionary<string, string>();

                // TODO Get this from the text context?
                //for (int i = 0; i < args.Length; i++)
                //{
                //    string[] tArgs = args[i].Split('=');
                //    if (tArgs.Length == 2)
                //    {
                //        string key = tArgs[0].ToUpper();
                //        ArgList[key] = tArgs[1];
                //    }
                //}
                #endregion

                ArgList.Add("DontVerifyTrust", "True"); //NEW: 3.2 If this is NOT set, all plugins have to be signed with the same certificate as the host application or C-DEngine.DLL

                ArgList.Add("UseRandomDeviceID", "true");                       //ATTENTION: ONLY if you set this to false, some of these parameters will be stored on disk and loaded at a later time. "true" assigns a new node ID everytime the host starts and no configuration data will be cached on disk.
                ArgList.Add("ScopeUserLevel", "255");   //Set the Scope Access Level
                ArgList.Add("AROLE", eEngineName.NMIService + ";" + eEngineName.ContentService);    //Make NMI and Content Service known to this host
                ArgList.Add("SROLE", eEngineName.NMIService + ";" + eEngineName.ContentService);    //Add NMI and Content Service as Service to run on this host. If you omit these entries, this host will become an end-node (not able to relay) and will try to find a proper relay node to talk to.
                string tScope = TheScopeManager.GenerateNewScopeID();                 //TIP: instead of creating a new random ID every time your host starts, you can put a breakpoint in the next line, record the ID and feed it in the "SetScopeIDFromEasyID". Or even set a fixed ScopeID here. (FOR TESTING ONLY!!)
                TestContext.Out.WriteLine("Current Scope:" + tScope);
                TheScopeManager.SetScopeIDFromEasyID(tScope);                       //Set a ScopeID - the security context of this node. You can replace tScope with any random 8 characters or numbers
                MyBaseApplication = new TheBaseApplication();    //Create a new Base (C-DEngine IoT) Application

                //TheBaseAssets.MasterSwitch = false;
                var appStarted = MyBaseApplication.StartBaseApplication(null, ArgList);

                Assert.IsTrue(appStarted, "Failed to StartBaseApplication");         //Start the C-DEngine Application. If a PluginService class is added DIRECTLY to the host project you can instantiate the Service here replacing the null with "new cdePluginService1()"
                                                                                     //If the Application fails to start, quit the app. StartBaseApplication returns very fast as all the C-DEngine code is running asynchronously
                                                                                     //MyBaseApplication.MyCommonDisco.RegisterUPnPUID("*", null);     //Only necessary if UPnP is used to find devices

                bool started = false;
                var engineStartedTask = TheBaseEngine.WaitForEnginesStartedAsync();
                if (engineStartedTask.Wait(20000))
                {
                    started = engineStartedTask.Result;
                    Assert.IsTrue(started, "Failed to start engines");
                }
                else
                {
                    ///Assert.IsTrue(started, "Failed to start engines (timeout)");
                    TestContext.Out.WriteLine("Failed to start engines (timeout)");
                }
                AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
            }
            myContentService = TheThingRegistry.GetBaseEngineAsThing(eEngineName.ContentService);
            Assert.IsNotNull(myContentService);
            TestContext.Out.WriteLine("{DateTimeOffset.Now} Host started");
            return true;
        }

        private static void OnDomainUnload(object sender, EventArgs e)
        {
            StopHostInternal(true);
        }

        protected static void StopHost()
        {
            TestContext.Out.WriteLine($"{DateTimeOffset.Now} About to stop host");

            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(10000);
                StopHostInternal(false);
            });
        }
        private static void StopHostInternal(bool force)
        {
            lock (appStartLock)
            {
                if (force || Interlocked.Decrement(ref activeHosts) <= 0)
                {
                    TestContext.Out.WriteLine($"{DateTimeOffset.Now} Stopping host");
                    // TODO Re-creating a host does not work reliably: all unit tests must use the same host (at least for now)
                    MyBaseApplication?.Shutdown(true, true);
                    TheBaseAssets.MyApplication = null;
                }
                else
                {
                    TestContext.Out.WriteLine($"{DateTimeOffset.Now} Not stopping host: other tests still running");
                }
            }

        }

        static object opcServerStartupLock = new object();

        static protected TheThing StartOPCServer(bool disableSecurity)
        {
#if OPCUASERVER
            // TODO Actually use our own OPC Server for the unit test
            var opcServerThing = TheThingRegistry.GetThingsOfEngine("CDMyOPCUAServer.cdeMyOPCServerService").FirstOrDefault();
            Assert.IsNotNull(opcServerThing, $"Unable to obtain OPC Server thing: error loading plug-in or server not yet initialized?");

            lock (opcServerStartupLock)
            {
                if (!TheThing.GetSafePropertyBool(opcServerThing, "IsRunning"))
                {

                    TheThing.SetSafePropertyBool(opcServerThing, "DisableSecurity", disableSecurity);
                    TheThing.SetSafePropertyBool(opcServerThing, "NoServerCertificate", disableSecurity);

                    var theOpcThing = new TheThing();
                    theOpcThing.EngineName = "OPCTestEng";
                    theOpcThing.DeviceType = "OPCTestDT";
                    theOpcThing.Address = "OPCTestAddress";
                    theOpcThing.SetProperty("OpcProp01", "0001");
                    theOpcThing.SetProperty("OpcProp02", "0002");
                    theOpcThing.SetProperty("OpcProp03", "0003");
                    theOpcThing.SetProperty("OpcProp04", "0004");
                    theOpcThing.SetProperty("OpcProp05", "0005");
                    theOpcThing.SetProperty("OpcProp06", "0006");
                    theOpcThing.SetProperty("OpcProp07", "0007");
                    theOpcThing.SetProperty("OpcProp08", "0008");
                    theOpcThing.SetProperty("OpcProp09", "0009");
                    theOpcThing.SetProperty("OpcProp10", "0010");
                    var tThing = TheThingRegistry.RegisterThing(theOpcThing);
                    Assert.IsNotNull(tThing);

                    var addThingResponse = TheCommRequestResponse.PublishRequestJSonAsync<MsgAddThingsToServer, MsgAddThingsToServerResponse>(myContentService, opcServerThing, new MsgAddThingsToServer
                    (
                        new TheThingToAddToServer
                        {
                            cdeMID = Guid.NewGuid(),
                            ReplaceExistingThing = false,
                            ThingMID = TheCommonUtils.cdeGuidToString(theOpcThing.cdeMID),
                        }
                    ), new TimeSpan(0, 0, 30)).Result;

                    Assert.IsNotNull(addThingResponse, "No reply to OPC Server MsgAddThingToServer");
                    Assert.IsTrue(string.IsNullOrEmpty(addThingResponse.Error), $"Error adding thing to OPC Server: '{addThingResponse.Error}'.");
                    Assert.AreEqual(1, addThingResponse.ThingStatus.Count, $"Error adding thing to OPC Server.");
                    Assert.IsTrue(string.IsNullOrEmpty(addThingResponse.ThingStatus[0].Error), $"Error adding thing to OPC Server: '{addThingResponse.ThingStatus[0].Error}'.");

                    MsgStartStopServerResponse responseStart;
                    int retryCount = 1;
                    do
                    {
                        responseStart = TheCommRequestResponse.PublishRequestJSonAsync<MsgStartStopServer, MsgStartStopServerResponse>(myContentService, opcServerThing,
                            new MsgStartStopServer
                            {
                                Restart = true,
                            },
                            new TimeSpan(0, 0, 30)).Result;
                        retryCount--;
                    } while (retryCount >= 0 && responseStart == null);

                    Assert.IsNotNull(responseStart, "Failed to send MsgStartStopServer message to restart OPC UA Server");
                    Assert.IsTrue(string.IsNullOrEmpty(responseStart.Error), $"Error restarting OPC Server: '{addThingResponse.Error}'.");
                    Assert.IsTrue(responseStart.Running, $"OPC Server not running after MsgStartStopServer Restart message");
                }
            }
            return opcServerThing;
#else
            return null;
#endif
        }


    }
}
