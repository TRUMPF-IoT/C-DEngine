// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace nsCDEngine.Discovery
{
    /// <summary>
    /// C-DEngine internal IP Address definition
    /// </summary>
    public class TheIPDef : EventArgs
    {
        /// <summary>
        /// IP Address
        /// </summary>
        public IPAddress IPAddr;
        /// <summary>
        /// Subnet bytes
        /// </summary>
        public byte[] SubnetBytes;
        /// <summary>
        /// IP Address bytes
        /// </summary>
        public byte[] ipAdressBytes;
        /// <summary>
        /// True if IP is private/local
        /// </summary>
        public bool IsPrivateIP;
        /// <summary>
        /// True if the network controller managing this IP has DNS enabled
        /// </summary>
        public bool IsDnsEnabled;
        /// <summary>
        /// True if this IP is a loopback (localhost/127.0.0.1)
        /// </summary>
        public bool IsLoopBack;
        /// <summary>
        /// Index into the network interfaces on this computer
        /// </summary>
        public int NetworkInterfaceIndex;
        /// <summary>
        /// String representation of the MAC address
        /// </summary>
        public string MACAddress;

        /// <summary>
        /// Friendly presentation of this class
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("{8}/{0} Sub:{1}.{2}.{3}.{4} /P:{5} D:{6} L:{7}", IPAddr.AddressFamily, SubnetBytes[0], SubnetBytes[1], SubnetBytes[2], SubnetBytes[3], IsPrivateIP, IsDnsEnabled, IsLoopBack,IPAddr);
        }
    }

    /// <summary>
    /// C-DEngine Helper class for Network related function
    /// </summary>
    public sealed class TheNetworkInfo
    {
        private sealed class ExIP
        {
            public Action<IPAddress, object> Callback;
            public object Cookie;
        }

        /// <summary>
        /// Interface Handler delegate
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void InterfaceHandler(object sender, TheIPDef e); //TheNetworkInfo

        internal static string MyHostName;
        internal static cdeConcurrentDictionary<IPAddress, TheIPDef> AddressTable = new ();
        /// <summary>
        /// Returns all iP addresses of this host hardware
        /// </summary>
        /// <returns></returns>
        public List<TheIPDef> GetLocalAddresses()
        {
            return AddressTable.Values.ToList();
        }
        private readonly Action<object,TheIPDef> OnInterfaceDisabledEvent;
        private readonly Action<object,TheIPDef> OnNewInterfaceEvent;

        /// <summary>
        /// Construtor for network related events
        /// </summary>
        /// <param name="onNewInterfaceSink"></param>
        /// <param name="onDeletedInterfaceSink"></param>
        public TheNetworkInfo(Action<object, TheIPDef> onNewInterfaceSink, Action<object, TheIPDef> onDeletedInterfaceSink)
        {
            TheBaseAssets.MySYSLOG.WriteToLog(138, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("UPnP", "Enter NetworkInfo"));

            TheQueuedSenderRegistry.RegisterHealthTimer(PollInterface);
            MyHostName = cdeGetHostName();
            TheBaseAssets.MySYSLOG.WriteToLog(139, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("UPnP", "NetworkInfo - HostName :" + MyHostName));
            GetLocalIPs(true);
            if (onNewInterfaceSink != null)
            {
                OnNewInterfaceEvent += onNewInterfaceSink;
                foreach (TheIPDef address in AddressTable.Values)
                {
                    if (address.IsDnsEnabled)
                    {
                        TheBaseAssets.MySYSLOG.WriteToLog(141, TSM.L(eDEBUG_LEVELS.FULLVERBOSE) ? null : new TSM("UPnP", string.Format("NetworkInfo - Init-Address: {0} of {1}", address, AddressTable.Count), eMsgLevel.l3_ImportantMessage));
                        OnNewInterfaceEvent?.Invoke(this, address);
                    }
                }
            }
            if (OnInterfaceDisabledEvent!=null)
                OnInterfaceDisabledEvent += onDeletedInterfaceSink;
            TheBaseAssets.MySYSLOG.WriteToLog(144, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("UPnP", "NetworkInfo - All Interfaces Initialized"));
        }

        private void PollInterface(long pTimerCnt)
        {
            if ((pTimerCnt % 30) != 0) return;
            try
            {
                ArrayList list = new (cdeGetHostEntry(MyHostName)?.AddressList);
                if (list?.Count > 0)
                {
                    foreach (IPAddress address in list)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetwork && !AddressTable.ContainsKey(address))
                        {
                            GetLocalIPs(false);
                            if (OnNewInterfaceEvent != null && AddressTable.ContainsKey(address) && AddressTable[address].IsDnsEnabled)
                                OnNewInterfaceEvent(this, new TheIPDef() { IPAddr = address });
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(147, new TSM("UPnP", "PollInterface Error", eMsgLevel.l1_Error, exception.ToString()));
            }
        }

        #region Static Network Utilities
        /// <summary>
        /// Finds a free socket port in the given range on a given IP Address
        /// </summary>
        /// <param name="LowRange">Low boundary of the area to scan</param>
        /// <param name="UpperRange">Upper boundary of the area to scan</param>
        /// <param name="OnThisIP">IP Address to scan on</param>
        /// <returns></returns>
        public static uint GetFreePort(uint LowRange, uint UpperRange, IPAddress OnThisIP)
        {
            uint num;
            Socket socket = new (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            while (true)
            {
                num = TheCommonUtils.GetRandomUInt(LowRange, UpperRange);
                IPEndPoint localEP = new (OnThisIP, (int)num);
                try
                {
                    socket.Bind(localEP);
                    break;
                }
                catch (Exception)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(146, TSM.L(eDEBUG_LEVELS.VERBOSE) ? null : new TSM("UPnP", $"Port {num} busy - trying next", eMsgLevel.l6_Debug));
                }
            }
            socket.Close();
            return num;
        }

        /// <summary>
        /// returns true if the IP address given is an intranet address
        /// </summary>
        /// <param name="pIPAddr">IP Address to query</param>
        /// <returns></returns>
        public static bool IsIPInLocalSubnet(IPAddress pIPAddr)
        {
            if (pIPAddr == null) return false;
            try
            {
                byte[] IPAddrBytes = pIPAddr.GetAddressBytes();
                if (AddressTable.Count == 0)
                    GetLocalIPs(false);
                foreach (TheIPDef tIp in AddressTable.Values)
                {
                    bool success = true;
                    for (int i = 0; i < tIp.ipAdressBytes.Length; i++)
                    {
                        if ((tIp.ipAdressBytes[i] & tIp.SubnetBytes[i]) != (IPAddrBytes[i] & tIp.SubnetBytes[i]))
                        {
                            success = false;
                            break;
                        }
                    }
                    if (success) return true;
                }
            }
            catch (Exception e)
            {
                TheBaseAssets.MySYSLOG.WriteToLog(146, new TSM("UPnP", "IsIPInLocalSubnet Error", eMsgLevel.l1_Error, e.ToString()));
            }
            return false;
        }

        static bool _platformDoesNotSupportUnicastIPAddressInformationIsDNSEnabled;
        internal static bool GetLocalIPs(bool AddLoopBack)
        {
            bool NewAddition = false;
            bool HasLoop = false;
            if ((Type.GetType("Mono.Runtime") != null))
            {
                try
                {
                    if (string.IsNullOrEmpty(MyHostName))
                        MyHostName = cdeGetHostName();

                    IPHostEntry hostByName = cdeGetHostEntry(MyHostName);
                if (hostByName == null) return false;
                    TheSystemMessageLog.ToCo(string.Format("NetworkInfo - HostName : {0}", hostByName.HostName));
                    ArrayList tAddressTable = new (hostByName.AddressList);
                    foreach (IPAddress address in tAddressTable)
                    {
                        if (address.AddressFamily != AddressFamily.InterNetworkV6)
                        {
                            NewAddition = true;
                            byte[] ipAdressBytes = address.GetAddressBytes();
                            TheIPDef tI = new () { ipAdressBytes = ipAdressBytes, SubnetBytes = new byte[] { 255, 0, 0, 0 }, IPAddr = address, IsDnsEnabled = true, IsPrivateIP = IsPrivateIPBytes(ipAdressBytes) };
                            if (address.ToString().Equals(IPAddress.Loopback.ToString()))
                            {
                                HasLoop = true;
                                tI.IsLoopBack = true;
                            }
                            AddressTable.TryAdd(address, tI);
                        }
                    }
                }
                catch (Exception e)
                {
                    TheSystemMessageLog.ToCo($"NetworkInfo - Exception: {e}");
                }
            }
            else
            {
                NetworkInterface[] networkInterfaces;
                try
                {
                    networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                }
                catch
                {
                    networkInterfaces = null;
                }
                if (networkInterfaces!= null)
                {
                    foreach (var f in networkInterfaces)
                    {
                        var ipInterface = f.GetIPProperties();

                        foreach (UnicastIPAddressInformation unicastAddress in ipInterface.UnicastAddresses)
                        {
                            try
                            {
                                if (unicastAddress.IPv4Mask != null)
                                {
                                    IPv4InterfaceProperties ipv4_properties = null;
                                    try
                                    {
                                        ipv4_properties = ipInterface.GetIPv4Properties();
                                    }
                                    catch
                                    {
                                        //ignored
                                    }
                                    if (ipv4_properties == null) continue;  
                                    byte[] tSubnetBytes = unicastAddress.IPv4Mask.GetAddressBytes();
                                    if (tSubnetBytes[0] == 0) continue;
                                    byte[] ipAdressBytes = unicastAddress.Address.GetAddressBytes();
                                    IPAddress tIP = new (ipAdressBytes);
                                    if (!AddressTable.ContainsKey(tIP))
                                    {
                                        TheIPDef tI = new ()
                                        {
                                            ipAdressBytes = ipAdressBytes,
                                            SubnetBytes = tSubnetBytes,
                                            IPAddr = tIP,
                                            //IsDnsEnabled = unicastAddress.IsDnsEligible,
                                            IsPrivateIP = IsPrivateIPBytes(ipAdressBytes),
                                            NetworkInterfaceIndex = ipv4_properties.Index,
                                            MACAddress = f.GetPhysicalAddress().ToString(),
                                        };
                                        if (!_platformDoesNotSupportUnicastIPAddressInformationIsDNSEnabled)
                                        {
                                            try
                                            {
                                                tI.IsDnsEnabled = unicastAddress.IsDnsEligible;
                                            }
                                            catch (PlatformNotSupportedException)
                                            {
                                                _platformDoesNotSupportUnicastIPAddressInformationIsDNSEnabled = true;
                                            }
                                        }
                                        if (_platformDoesNotSupportUnicastIPAddressInformationIsDNSEnabled)
                                        {
                                            if (string.IsNullOrEmpty(MyHostName))
                                                MyHostName = cdeGetHostName();
                                            var ipAddresses = cdeGetHostEntry(MyHostName)?.AddressList;
                                            if (ipAddresses?.FirstOrDefault(ip => ip.Equals(unicastAddress.Address)) != null)
                                            {
                                                tI.IsDnsEnabled = true;
                                            }
                                        }
                                        if (tIP.ToString().Equals(IPAddress.Loopback.ToString()))
                                        {
                                            HasLoop = true;
                                            tI.IsLoopBack = true;
                                        }
                                        AddressTable.TryAdd(tIP, tI);
                                        NewAddition = true;
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
            }

            if (AddLoopBack && !HasLoop)
            {
                AddressTable.TryAdd(IPAddress.Loopback, new TheIPDef() { IPAddr = IPAddress.Loopback, ipAdressBytes = IPAddress.Loopback.GetAddressBytes(), SubnetBytes = new byte[] { 255, 0, 0, 0 },IsPrivateIP=true,IsDnsEnabled=false,IsLoopBack=true });
                NewAddition = true;
            }
            return NewAddition;
        }

        /// <summary>
        /// Checks if this relay has a direct connection to the internet
        /// Do not call too frequent as this function is sending a ping to a wellknown internet Address
        /// </summary>
        /// <returns></returns>
        public static bool IsConnectedToInternet()
        {
            string host = "1.1.1.1"; //NOSONAR - fixed ping to Cloudflare
            var p = new Ping();
            try
            {
                var reply = p.Send(host, 3000);
                if (reply != null && reply.Status == IPStatus.Success)
                {
                    TheBaseAssets.MyServiceHostInfo.HasInternetAccess = true;
                    return true;
                }
            }
            catch {
                //ignored
            }
            TheBaseAssets.MyServiceHostInfo.HasInternetAccess = false;
            return false;
        }
        #region IP Utilities
        /// <summary>
        /// Returns the external IP of this hosting hardware by making a reverse lookup via checkip.dyndns.org
        /// </summary>
        /// <param name="pCallback">Will be called when the host was resolved</param>
        /// <param name="pCookie">A cookie to track this call with</param>
        public static void GetExternalIp(Action<IPAddress, object> pCallback, object pCookie)
        {
            string whatIsMyIp = "https://ifconfig.me/ip";
            ExIP tExIP = new ()
            {
                Callback = pCallback,
                Cookie = pCookie
            };
            TheREST.GetRESTAsync(new Uri(whatIsMyIp), 0, sinkExternalIP, tExIP);
        }

        private static void sinkExternalIP(TheRequestData pResult)
        {
            if (pResult.CookieObject is ExIP texip)
            {
                Action<IPAddress, object> MyCall = texip.Callback;
                IPAddress externalIp = null;
                try
                {
                    externalIp = IPAddress.Parse(TheCommonUtils.CArray2UTF8String(pResult.ResponseBuffer));
                }
                catch (Exception e)
                {
                    TheBaseAssets.MySYSLOG.WriteToLog(258, TSM.L(eDEBUG_LEVELS.OFF) ? null : new TSM("TheNetworkInfo", "GetExternalIp Exception", eMsgLevel.l1_Error, e.Message));
                }
                MyCall(externalIp, texip.Cookie);
            }
        }

        /// <summary>
        /// Refreshes the table of IP addresses
        /// </summary>
        /// <param name="bRefresh"></param>
        /// <returns></returns>
        public static IEnumerable<IPAddress> GetIPAddresses(bool bRefresh)
        {
            if (bRefresh)
            {
                AddressTable.Clear(); // CODE REVIEW: in other places we only add new IPs - will clearing the list cause problems?
                GetLocalIPs(true);
            }
            return AddressTable.Values.Select(a => new IPAddress(a.ipAdressBytes));
        }

        /// <summary>
        /// Returns an IP address of the host hardware
        /// </summary>
        /// <param name="MustBeExternal">if true, a public IP with internet access will be returned</param>
        /// <returns></returns>
        public static IPAddress GetIPAddress(bool MustBeExternal)
        {
            if (AddressTable.Count == 0)
                GetLocalIPs(true);
            foreach (TheIPDef tIp in AddressTable.Values)
            {
                if (tIp.IsDnsEnabled && (!MustBeExternal || !IsPrivateIPBytes(tIp.ipAdressBytes)))
                {
                    return new IPAddress(tIp.ipAdressBytes);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns an IP Address starting with a given scheme
        /// </summary>
        /// <param name="firsts">Starting digits of the IP (i.e. "192.168")</param>
        /// <returns></returns>
        public static IPAddress GetIPAddress(string firsts)
        {
            if (AddressTable.Count == 0)
                GetLocalIPs(true);
            foreach (TheIPDef tIp in AddressTable.Values)
            {
                if (tIp.IPAddr.ToString().StartsWith(firsts))
                {
                    return new IPAddress(tIp.ipAdressBytes);
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the mac address of the primary network adapter.
        /// </summary>
        /// <param name="MustBeExternal">if set to <c>true</c> the adapter must be external (not loopback or private ip).</param>
        /// <returns>System.String.</returns>
        public static string GetMACAddress(bool MustBeExternal)
        {
            if (AddressTable.Count == 0)
                GetLocalIPs(true);
            foreach (TheIPDef tIp in AddressTable.Values)
            {
                if (tIp.IsDnsEnabled && (!MustBeExternal || !IsPrivateIPBytes(tIp.ipAdressBytes)))
                {
                    return tIp.MACAddress;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the IPHostEntry of a given host name
        /// </summary>
        /// <param name="pName">Name of the host to get the IPHostEntry for</param>
        /// <returns></returns>
        public static IPHostEntry cdeGetHostEntry(string pName)
        {
            return Dns.GetHostEntry(pName);
        }

        /// <summary>
        /// Returns the host name of the hosting hardware
        /// </summary>
        public static string cdeGetHostName()
        {
            return Dns.GetHostName();
        }

        /// <summary>
        /// returns true if the given IP address is a local/private IP
        /// </summary>
        /// <param name="myIPAddress"></param>
        /// <returns></returns>
        public static bool IsPrivateIP(IPAddress myIPAddress)
        {
            if (myIPAddress.AddressFamily == AddressFamily.InterNetwork)
                return IsPrivateIPBytes(myIPAddress.GetAddressBytes());
            return false;
        }
        private static bool IsPrivateIPBytes(byte[] ipBytes)
        {
            if (ipBytes == null)
            {
                return false;
            }
            // 10.0.0.0/24
            if (ipBytes[0] == 10)
            {
                return true;
            }
            else if (ipBytes[0] == 127)
            {
                return true;
            }
            // 172.16.0.0/16
            else if (ipBytes[0] == 172 && (ipBytes[1] == 16 || ipBytes[1] == 24)) //24 used by Hyper-V for internal IPs
            {
                return true;
            }
            // 192.168.0.0/16
            else if (ipBytes[0] == 192 && ipBytes[1] == 168)
            {
                return true;
            }
            // 169.254.0.0/16
            else if (ipBytes[0] == 169 && ipBytes[1] == 254)
            {
                return true;
            }
            return false;
        }
#endregion

#endregion
        }
}

