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
using System.Text;
// ReSharper disable AssignNullToNotNullAttribute

namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// TSMCosting describes the costs of a TSM in form of a receipt
    /// A TSMCosting receipt can be added to a TSM with the AddCosting method
    /// </summary>
    public class TSMCosting : TheDataBase
    {
        /// <summary>
        /// A number of credits. Inside a solution (Same ApplicationID) the value of a Credit should be unified for better billing
        /// </summary>
        public int Credits { get; set; }
        /// <summary>
        /// Description what the credit was for (i.e. TSM Relay or Image Creation etc)
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Name of the Vendor issuing the credit
        /// </summary>
        public string Vendor { get; set; }
        /// <summary>
        /// A arbitrary code for the Vendor
        /// </summary>
        public string VendorCode { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pCredits">Amount of credits</param>
        /// <param name="pDescription">Description of the credits purpose</param>
        public TSMCosting(int pCredits, string pDescription)
        {
            Credits = pCredits;
            Description = pDescription;
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pCredits">Amount of credits</param>
        /// <param name="pDescription">Description of the credits purpose</param>
        /// <param name="pVendor">Vendor Name</param>
        public TSMCosting(int pCredits, string pDescription,string pVendor)
        {
            Credits = pCredits;
            Description = pDescription;
            Vendor = pVendor;
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pCredits">Amount of credits</param>
        /// <param name="pDescription">Description of the credits purpose</param>
        /// <param name="pVendor">Vendor Name</param>
        /// <param name="pVC">Vendor Code</param>
        public TSMCosting(int pCredits, string pDescription, string pVendor, string pVC)
        {
            Credits = pCredits;
            Description = pDescription;
            Vendor = pVendor;
            VendorCode = pVC;
        }

        /// <summary>
        /// A friendly representation of the costing receipt
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("{0} {1} ({2}/{3})\n", Credits, Description, Vendor, VendorCode);
        }
    }

    /// <summary>
    /// Creates a receipt of all Costs associated with a TSM at the time of Costing
    /// </summary>
    public class TSMTotalCost : TheDataBase
    {
        internal List<TSMCosting> Items;
        internal int TotalItems { get; set; }
        internal long TotalCredits { get; set; }

        /// <summary>
        /// Friendly readable text of the Total Costs with all intermitten receipts
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (TotalItems == 0) return "No Cost";
            string t = "Cost Receipt:\n========================\n";
            foreach (TSMCosting tCost in Items)
                t += tCost.ToString();
            t += "===========================\n";
            t += string.Format("Total Credits: {0} for {1} Items", TotalCredits, TotalItems);
            return t;
        }
    }

    /// <summary>
    /// TSM = The System Message
    /// This is the frame telegram that is used to send all communication between nodes.
    /// </summary>
    public partial class TSM
    {
        /// <summary>
        /// Add the current station DeviceID to the list of Originators
        /// </summary>
        public bool AddHopToOrigin()
        {
            if (!DoesORGContain(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID))
            {
                if (string.IsNullOrEmpty(ORG))
                    ORG = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString();
                else
                    ORG += ";" + TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString();
                if (TheBaseAssets.MyServiceHostInfo.EnableCosting)
                    TheCDEngines.updateCosting(this);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Checks if the given Originator DeviceID is in the list of Origins
        /// </summary>
        /// <param name="tGuid"></param>
        /// <returns></returns>
        public bool DoesORGContain(Guid tGuid)
        {
            if (tGuid==Guid.Empty) return false;
            return TheCommonUtils.DoUrlsContainAnyUrl(ORG, tGuid.ToString());
        }
        /// <summary>
        /// Checks if a ; separated list of IDs is found in the Originators of this message.
        /// Only if ALL pIDs are found in the ORG, the method will return true
        /// </summary>
        /// <param name="pIDs"></param>
        /// <returns></returns>
        public bool DoesORGContainAny(string pIDs)
        {
            if (string.IsNullOrEmpty(pIDs)) return false;
            return TheCommonUtils.DoUrlsContainAnyUrl(ORG, pIDs);
        }
        /// <summary>
        /// True if the Originator was the local machine
        /// </summary>
        /// <returns></returns>
        public bool DoesORGContainLocalHost()
        {
            return ORG.Contains(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString());
        }

        /// <summary>
        /// NEW in V4: Returns the next node in GRO to optimize Route traffic
        /// </summary>
        /// <returns></returns>
        public Guid GetNextNode()
        {
            if (string.IsNullOrEmpty(GRO) || !GRO.Contains(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString())) return Guid.Empty;

            var tNodes = TheCommonUtils.cdeSplit(GRO, ';', false, false);
            if (tNodes.Length < 2) return Guid.Empty;
            var tLastNode = tNodes[0].Split(':')[0];
            for (int i=1;i<tNodes.Length;i++)
            {
                var tN = tNodes[i].Split(':')[0];
                if (tN == TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString())
                    return TheCommonUtils.CGuid(tLastNode);
                tLastNode = tN;
            }
            return Guid.Empty;
        }

        /// <summary>
        /// Returns true if this message came from the FirstNode
        /// RETIRED: Use with bool
        /// </summary>
        /// <returns></returns>
        public bool IsFirstNode()
        {
            int t = HobCount();
            if (t == 1 || ((t == 2 ) || (TheBaseAssets.MyServiceHostInfo.IsIsolated && t==3)) //New in V4: If Last Node IsIsolated - FirstNode Rule still applies
                && TheCommonUtils.GetLastURL(ORG).Equals(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID))
                return true;
            else
            {
                //if (t==3) TheSystemMessageLog.ToCo($"DID:{TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID} ORG:{ORG} LastURL:{TheCommonUtils.GetLastURL(ORG)} ");
                return false;
            }
        }
        /// <summary>
        /// Returns true if this message came from the FirstNode
        /// </summary>
        /// <returns></returns>
        public bool IsFirstNode(bool CheckISO)
        {
            int t = HobCount();
            if (t == 1 || ((t == 2 || (CheckISO && TheBaseAssets.MyServiceHostInfo.IsIsolated && t==3)) //New in V4: If Last Node IsIsolated - FirstNode Rule still applies
                && TheCommonUtils.GetLastURL(ORG).Equals(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID)))
                return true;
            else
            {
                //if (t==3) TheSystemMessageLog.ToCo($"DID:{TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID} ORG:{ORG} LastURL:{TheCommonUtils.GetLastURL(ORG)} ");
                return false;
            }
        }

        /// <summary>
        /// Gets the first initial Originator of the message
        /// </summary>
        /// <returns></returns>
        public Guid GetOriginator()   //SL SAFE
        {
            return TheCommonUtils.GetFirstURL(ORG);
        }

        /// <summary>
        /// Gets the first initial Originator from an ORG string
        /// </summary>
        /// <returns></returns>
        public static Guid GetOriginator(string ORG)
        {
            return TheCommonUtils.GetFirstURL(ORG);
        }

        /// <summary>
        /// Gets the initial Originating thing of the message (commonly used for replying to the originating thing)
        /// </summary>
        /// <returns></returns>
        public Guid GetOriginatorThing()   //SL SAFE
        {
            if (string.IsNullOrEmpty(ORG)) return Guid.Empty;
            string[] t = ORG.Split(';');
            if (t.Length > 0)
            {
                var urlParts = t[0].Split(':');
                if (urlParts.Length > 1)
                {
                    return TheCommonUtils.CGuid(urlParts[1]);
                }
            }
            return Guid.Empty;
        }

        /// <summary>
        /// This function will return the first NodeID in the TSM that matches a Relay. Browsers do not have trust but the first-node a browser is conencted to is the browsers security proxy
        /// The function will return the Originator if no proxy was found
        /// </summary>
        /// <returns></returns>
        public Guid GetOriginatorSecurityProxy()
        {
            if (string.IsNullOrEmpty(ORG))
                return Guid.Empty;
            string[] t = ORG.Split(';');

            Guid relay = TheCommonUtils.CGuid(t[0].Split(':')[0]);
            if (t.Length > 2 && TheBaseAssets.MyScopeManager.GetSenderTypeFromDeviceID(relay) == cdeSenderType.CDE_JAVAJASON)
            {
                relay = TheCommonUtils.CGuid(t[1].Split(':')[0]);
            }
            return relay;
        }
        /// <summary>
        /// Gets the DeviceID of the last known Relay forwarding this message
        /// </summary>
        /// <returns></returns>
        public Guid GetLastRelay()
        {
            string[] t = ORG.Split(';');
            Guid relay = Guid.Empty;
            if (t.Length > 0)
            {
                var tLast = t[t.Length - 1].Split(':')[0];
                if (tLast.Equals(TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString()) && t.Length > 1)
                    relay = TheCommonUtils.CGuid(t[t.Length - 2].Split(':')[0]);
                else
                    relay = TheCommonUtils.CGuid(tLast);
            }
            return relay;
        }
        /// <summary>
        /// Sets a new Originator to the message
        /// </summary>
        /// <param name="pURL"></param>
        public void SetOriginator(Guid pURL)
        {
            if (DoesORGContain(pURL)) return;
            string[] t = ORG.Split(';');
            if (t.Length > 0)
            {
                t[0] = pURL.ToString();
                ORG = "";
                foreach (string tt in t)
                {
                    if (ORG.Length > 0) ORG += ";";
                    ORG += tt;
                }
            }
            else
                ORG = pURL.ToString();
        }

        /// <summary>
        /// Sets the Originator Thing on a TSM
        /// </summary>
        /// <param name="MyBaseThing"></param>
        /// <returns></returns>
        public TSM SetOriginatorThing(TheThing MyBaseThing)
        {
            if (MyBaseThing!=null)
                SetOriginatorThing(MyBaseThing.cdeMID);
            return this;
        }

        /// <summary>
        /// Sets a new Originator/Thing to the message
        /// </summary>
        /// <param name="pThingMID">Originating thing's cdeMID value</param>
        public void SetOriginatorThing(Guid pThingMID)
        {
            var tORG = GetOriginator();
            if (tORG == Guid.Empty)
                tORG = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
            var combinedUrl = $"{tORG}:{pThingMID}";

            string[] t = ORG.Split(';');

            if (t.Length > 0)
            {
                if (t[0] == combinedUrl)
                {
                    return;
                }

                t[0] = combinedUrl;
                ORG = "";
                foreach (string tt in t)
                {
                    if (ORG.Length > 0) ORG += ";";
                    ORG += tt;
                }
            }
            else
                ORG = combinedUrl;
        }



        /// <summary>
        /// Adds an encrypted cost-ticket to the current message to the CST parameter
        /// </summary>
        /// <param name="pCosts"></param>
        public void AddCosting(TSMCosting pCosts)
        {
            if (pCosts != null)
            {
                if (string.IsNullOrEmpty(CST)) CST = "";
                string tCosting = TheCommonUtils.cdeEncrypt(string.Format("{0};{1};{2};{3}", pCosts.Credits, pCosts.Description, pCosts.Vendor, pCosts.VendorCode), TheBaseAssets.MySecrets.GetAI()); //3.083: Must be cdeAI
                if (!string.IsNullOrEmpty(CST)) CST += ";:;";
                CST += tCosting;
            }
        }

        /// <summary>
        /// Returns the total costs of the Message in form of a TSMTotalCost receipt
        /// </summary>
        /// <returns></returns>
        public TSMTotalCost GetTotalCosts()
        {
            if (string.IsNullOrEmpty(CST)) return null;
            string[] tItems = TheCommonUtils.cdeSplit(CST, ";:;", true,true);
            TSMTotalCost tTotal = new TSMTotalCost()
            {
                Items = new List<TSMCosting>()
            };
            foreach (string t in tItems)
            {
                string[] tt = TheCommonUtils.cdeDecrypt(t, TheBaseAssets.MySecrets.GetAI()).Split(';');     //3.083: Must be cdeAI
                if (tt.Length < 4) continue;
                TSMCosting tCost = new TSMCosting(TheCommonUtils.CInt(tt[0]), TheCommonUtils.CStr(tt[1]), TheCommonUtils.CStr(tt[2]), TheCommonUtils.CStr(tt[3]));
                tTotal.Items.Add(tCost);
                tTotal.TotalCredits += tCost.Credits;
                tTotal.TotalItems++;
            }
            return tTotal;
        }

        /// <summary>
        /// Calculates a hash integer that defines a unique hash for the message
        /// Only LVL, ENG, the Originator and the First parameter of the TXT will be taken in account.
        /// BEWARE: there are rare occasions where two messages are considered equal although they are not. Your code should be fault tolerant to these conditions
        /// </summary>
        /// <returns></returns>
        public int GetHash()
        {
            return GetHash(null);
        }

        /// <summary>
        /// Calculates a hash integer that defines a unique hash for the message
        /// Only LVL, ENG, the Originator, OWN and the First parameter of the TXT will be taken in account.
        /// BEWARE: there are rare occasions where two messages are considered equal although they are not. Your code should be fault tolerant to these conditions
        /// The Salt can be use to add a random or other string-source to the Hash
        /// </summary>
        /// <param name="Salt"></param>
        /// <returns></returns>
        public int GetHash(string Salt)
        {
            int hash = 0;
            if (!string.IsNullOrEmpty(Salt))
                hash += Salt.GetHashCode();
            hash += LVL.GetHashCode();
            if (!string.IsNullOrEmpty(ENG))
                hash += ENG.GetHashCode();
            hash += GetOriginator().GetHashCode();
            if (!string.IsNullOrEmpty(OWN))
                hash += OWN.GetHashCode();
            string[] t = TheCommonUtils.cdeSplit(TXT, ";:;", false, false);
            if (t != null && t.Length > 0 && !string.IsNullOrEmpty(t[0]))
                hash += t[0].GetHashCode();
            return hash;
        }

        /// <summary>
        /// Creates a new empty TSM - ORG and SID will NOT be set and therefore this is the fastest way to create a TSM (used by the Serializer)
        /// </summary>
        public TSM()
        {
            //NEW4.207: No Org, SID or TXT set for "deserializing" TSMs - had to be reversed as several scenarios did not work anymore
            //TXT = "empty";
            SetOrigin();
        }

        /// <summary>
        /// Creates a new TSM with a given Engine and Command Text
        /// </summary>
        /// <param name="tEngine">Engine Name</param>
        /// <param name="pText">Command Text</param>
        public TSM(string tEngine, string pText)
        {
            ENG = tEngine;
            TXT = pText;
            SetOrigin();

        }



        /// <summary>
        /// Creates a new TSM with a given Engine and Command Text and Payload
        /// </summary>
        /// <param name="tEngine">Engine Name</param>
        /// <param name="pText">Command Text</param>
        /// <param name="pPayload">Payload string for the message</param>
        public TSM(string tEngine, string pText, string pPayload)
        {
            ENG = tEngine;
            TXT = pText;
            PLS = pPayload;
            SetOrigin();
        }

        /// <summary>
        /// Creates a new TSM with a given Engine and Command Localizable Text and Payload
        /// </summary>
        /// <param name="tEngine">Engine Name - will also be used for the Resource lookup </param>
        /// <param name="pText">Command Text</param>
        /// <param name="pPayload">Payload string for the message</param>
        /// <param name="LCID">Language ID to translate the string in PLS to</param>
        public TSM(string tEngine, string pText, string pPayload, int LCID)
        {
            ENG = tEngine;
            TXT = pText;
            if (!string.IsNullOrEmpty(pPayload) && pPayload.StartsWith("###"))
                pPayload = TheBaseAssets.MyLoc.GetLocalizedStringByKey(LCID, tEngine, pPayload);
            PLS = pPayload;
            SetOrigin();
        }

        /// <summary>
        /// Creates a new TSM with a given Engine and Message Text, Message Level and Payload
        /// ATTENTION: Only use this Constructor for WriteToLog() entries.
        /// ATTENTION: This constructor does NOT set the SID by default to avoid performance hit for SID creation
        /// To change this behavior you have to set "DisableFastTSMs=true" in the App.config
        /// </summary>
        /// <param name="tEngine">Engine Name</param>
        /// <param name="pText">Command Text</param>
        /// <param name="pLevel">Priority Level of the Message</param>
        /// <param name="pPayload">Payload string for the message</param>
        public TSM(string tEngine, string pText, eMsgLevel pLevel, string pPayload)
        {
            ENG = tEngine;
            PLS = pPayload;
            TXT = pText;
            LVL = pLevel;
            SetOrigin(TheBaseAssets.MyServiceHostInfo?.DisableFastTSMs==true?false:true);
        }

        /// <summary>
        /// Creates a new TSM with a given Engine and Command Text and Payload
        /// </summary>
        /// <param name="tEngine">Engine Name</param>
        /// <param name="pText">Command Text</param>
        /// <param name="pLevel">Priority Level of the Message</param>
        /// <param name="pPayload">Payload string for the message</param>
        /// <param name="pQueueIdx">Queue Index for the Message - the lower the faster the message is transported</param>
        public TSM(string tEngine, string pText, eMsgLevel pLevel, string pPayload, int pQueueIdx)
        {
            ENG = tEngine;
            PLS = pPayload;
            TXT = pText;
            LVL = pLevel;
            QDX = pQueueIdx;
            SetOrigin();
        }

        /// <summary>
        /// Creates a new TSM with a given Engine and Command Text and Payload
        /// </summary>
        /// <param name="tEngine">Engine Name</param>
        /// <param name="pText">Command Text</param>
        /// <param name="pLevel">Priority Level of the Message</param>
        /// <param name="pTimeStamp">Custom Timestamp for the message</param>
        /// <param name="pPayload">Payload string for the message</param>
        public TSM(string tEngine, string pText, eMsgLevel pLevel, DateTimeOffset pTimeStamp, string pPayload)
        {
            ENG = tEngine;
            PLS = pPayload;
            TXT = pText;
            LVL = pLevel;
            TIM = pTimeStamp;
            SetOrigin();
        }

        /// <summary>
        /// Returns the TSM Header information
        /// </summary>
        /// <returns></returns>
        public string MsgHeader()
        {
            return $"{TheCommonUtils.GetDateTimeString(TIM, 0, "yyyy-MM-dd HH:mm:ss.fff")} : {FID}/{QDX}/{LVL} : {ENG} : {ORG}";
        }
        /// <summary>
        /// Returns the TSM Body (TXT and PLS)
        /// </summary>
        /// <returns></returns>
        public string MsgBody()
        {
            return $"MSG=({(TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog ? TheCommonUtils.cdeStripHTML(TXT) : TXT)}){(string.IsNullOrEmpty(PLS)?"":$" Payload=({(TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog ? TheCommonUtils.cdeStripHTML(PLS) : PLS)}")}";
        }

        /// <summary>
        /// Returns a friendly representation of the TSM with 3000 characters max for PLS
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{TheCommonUtils.GetDateTimeString(TIM, 0, "yyyy-MM-dd HH:mm:ss.fff")} : {FID}/{QDX}/{LVL} : {ENG} : {ORG} : MSG=({(TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog ? TheCommonUtils.cdeStripHTML(TXT) : TXT)}){(string.IsNullOrEmpty(PLS) ? "" : $" Payload=({(TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog ? TheCommonUtils.cdeStripHTML(TheCommonUtils.cdeSubstringMax(PLS, 3000)) : TheCommonUtils.cdeSubstringMax(PLS, 3000))})")}";
        }
        /// <summary>
        /// Same as ToString just without the 3000 character limitation USE CAREFULLY! This can create huge memory allocation
        /// </summary>
        /// <returns></returns>
        public string ToAllString()
        {
            return $"{TheCommonUtils.GetDateTimeString(TIM, 0, "yyyy-MM-dd HH:mm:ss.fff")} : {FID}/{QDX}/{LVL} : {ENG} : {ORG} : MSG=({(TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog ? TheCommonUtils.cdeStripHTML(TXT) : TXT)}){(string.IsNullOrEmpty(PLS) ? "" : $" Payload=({(TheBaseAssets.MyServiceHostInfo.ShowMarkupInLog ? TheCommonUtils.cdeStripHTML(PLS) : PLS)})")}";
        }

        /// <summary>
        /// Extracts the Scrambed ScopeID from a message and creates Topic extension
        /// </summary>
        /// <returns></returns>
        public string AddScopeIDFromTSM()
        {
            string toPub = "";
            if (!string.IsNullOrEmpty(SID))  //TheBaseAssets.IsCldService && Relaying now allowd of ScopeID Messages even if not just in the cloud
                toPub = "@" + SID;
            return toPub;
        }

        /// <summary>
        /// Make a deep clone of the Message
        /// </summary>
        /// <param name="tMsg">Mesage to be cloned</param>
        /// <param name="DoClonePLB">If set to True, the PLB parameter will be block-copied, too</param>
        /// <returns></returns>
        public static TSM Clone(TSM tMsg, bool DoClonePLB)
        {
            TSM tRes = new TSM(tMsg.SID)    //Save not to set ORG or SID for fast cloning
            {
                TXT = tMsg.TXT,
                TIM = tMsg.TIM,
                FLG = tMsg.FLG,
                ORG = tMsg.ORG,//ORG-OK
                QDX = tMsg.QDX,
                LVL = tMsg.LVL,
                ENG = tMsg.ENG,
                FID = tMsg.FID,
                SEID = tMsg.SEID,
                CST = tMsg.CST,
                UID = tMsg.UID,
                PLS = tMsg.PLS,
                OWN = tMsg.OWN
            };
            if (DoClonePLB && tMsg.PLB != null && tMsg.PLB.Length > 0)
            {
                tRes.PLB = new byte[tMsg.PLB.Length];
                TheCommonUtils.cdeBlockCopy(tMsg.PLB, 0, tRes.PLB, 0, tMsg.PLB.Length);
            }
            return tRes;
        }


#if CDE_FASTTSMSERIALIZER
        /// <summary>
        /// Deserializes a JSON string to a TSM
        /// PLB will be ignored
        /// WARNING: this is not used in the C-DEngine and migh
        /// </summary>
        /// <param name="pPayload"></param>
        /// <returns></returns>
        internal static TSM DeserializeJStringToObjectTSM(object pPayload)
        {
            if (pPayload == null) return null;

            TSM tTSM = new TSM();   //Save not to set ORG or SID
            tTSM.SID = "";

            Dictionary<string, object> tdic = pPayload as Dictionary<string, object>;
            if (tdic == null)
            {
                fastJSON.JsonParser tParser = new fastJSON.JsonParser(pPayload.ToString(), false);
                tdic = tParser.Decode() as Dictionary<string, object>;
            }
            if (tdic != null)
            {
                foreach (string key in tdic.Keys)
                {
                    if (tdic[key] != null)
                        ValSetter2(ref tTSM, key, tdic[key],true);
                }
            }
            return tTSM;
        }
        private static void ValSetter2(ref TSM tTSM, string ptr, object tval, bool IsJSON)
        {
            switch (ptr) //int
            {
                case "TXT":
                    tTSM.TXT = TheCommonUtils.CStr(tval);
                    break;
                case "TIM":
                    if (IsJSON)
                    {
                        if (tval != null)
                        {
                            long tim = TheCommonUtils.CLng(tval.ToString().Substring(6, tval.ToString().Length - 8));
                            tTSM.TIM = new DateTimeOffset(1970, 1, 1,0,0,0,DateTimeOffset.Now.Offset).Add(new TimeSpan(tim * 10000));
                        }
                    }
                    else
                    {
                        if (tval != null)
                            tTSM.TIM = TheCommonUtils.CDate(tval.ToString());
                    }
                    break;
                case "FLG":
                    tTSM.FLG = (ushort)TheCommonUtils.CInt(tval);
                    break;
                case "PLS":
                    tTSM.PLS = TheCommonUtils.CStr(tval);
                    break;
                case "ORG":
                    tTSM.ORG = TheCommonUtils.CStr(tval);//ORG-OK
                    break;
                case "QDX":
                    tTSM.QDX = TheCommonUtils.CInt(tval);
                    break;
                case "LVL":
                    if (tval.Equals("l1_Error")) tTSM.LVL = eMsgLevel.l1_Error;
                    else if (tval.Equals("l2_Warning")) tTSM.LVL = eMsgLevel.l2_Warning;
                    else if (tval.Equals("l3_ImportantMessage")) tTSM.LVL = eMsgLevel.l3_ImportantMessage;
                    else if (tval.Equals("l4_Message")) tTSM.LVL = eMsgLevel.l4_Message;
                    else if (tval.Equals("l5_HostMessage")) tTSM.LVL = eMsgLevel.l5_HostMessage;
                    else if (tval.Equals("l6_Debug")) tTSM.LVL = eMsgLevel.l6_Debug;
                    else if (tval.Equals("l7_HostDebugMessage")) tTSM.LVL = eMsgLevel.l7_HostDebugMessage;
                    else if (tval.Equals("ALL")) tTSM.LVL = eMsgLevel.ALL;
                    else tTSM.LVL = 0;
                    break;
                case "ENG":
                    tTSM.ENG = TheCommonUtils.CStr(tval);
                    break;
                case "FID":
                    tTSM.FID = TheCommonUtils.CStr(tval);
                    break;
                case "SID":
                    tTSM.SID = TheCommonUtils.CStr(tval);
                    break;
                case "SEID":
                    tTSM.SEID = TheCommonUtils.CStr(tval);
                    break;
                case "UID":
                    tTSM.UID = TheCommonUtils.CStr(tval);
                    break;
                case "CST":
                    tTSM.CST = TheCommonUtils.CStr(tval);
                    break;
                case "OWN":
                    tTSM.OWN = TheCommonUtils.CStr(tval);
                    break;
                case "PLB":
                    if (tval!=null)
                        tTSM.PLB = Convert.FromBase64String(tval.ToString());
                    break;
            }
        }
#endif

        private static readonly byte[] zeroInt = new byte[] { 0, 0, 0, 0 };

        /// <summary>
        /// Serializes the TSM to a binary object format only known to the C-Engine
        /// This is not used for the HTTP channel but reserverd for future use on other TCP/UPD channels
        /// </summary>
        /// <param name="pOffset"></param>
        /// <returns></returns>
        internal byte[] ToPacketBytes(int pOffset)
        {
            long timeBytes = TIM.ToFileTime();
            short timeOffsetBytes = (short) TIM.Offset.TotalMinutes;

            string[] Data = new string[9];
            Data[0] = TXT;              // Text
            Data[1] = PLS;         // PayLoad String
            Data[2] = ORG;         //  Origin
            Data[3] = ENG;         //eEngineName
            Data[4] = FID;         // Federation ID
            Data[5] = SID;         // Scope ID
            Data[6] = SEID;        // Session ID
            Data[7] = UID;          //UserID
            Data[8] = CST;
            Data[9] = OWN;
            //public byte[] PLB;              // PayLoad Binary

            int dataByteCounter = 0;

            byte[] dataBytes = null;
            using (MemoryStream dataByteWriter = new MemoryStream())
            {
                byte[] dataStringBytes = null;
                foreach (string dataString in Data)
                {
                    if (string.IsNullOrEmpty(dataString))
                    {
                        dataByteWriter.Write(zeroInt, 0, 4);
                        dataByteCounter += 4;
                    }
                    else
                    {
                        dataStringBytes = Encoding.UTF8.GetBytes(dataString);
                        dataByteWriter.WriteByte((byte)((dataStringBytes.Length >> 0) & 0xff));
                        dataByteWriter.WriteByte((byte)((dataStringBytes.Length >> 8) & 0xff));
                        dataByteWriter.WriteByte((byte)((dataStringBytes.Length >> 16) & 0xff));
                        dataByteWriter.WriteByte((byte)((dataStringBytes.Length >> 24) & 0xff));
                        dataByteWriter.Write(dataStringBytes, 0, dataStringBytes.Length);
                        dataByteCounter += 4;
                        dataByteCounter += dataStringBytes.Length;
                    }
                }
                dataBytes = dataByteWriter.GetBuffer();
            }

            int PLBLength = 0;
            if (PLB != null && PLB.Length > 0)
                PLBLength = PLB.Length;
            int totalBytes = 21 + PLBLength + dataByteCounter + pOffset;

            byte[] packetBuffer = null;
            using (MemoryStream stream = new MemoryStream())
            {
                stream.WriteByte((byte)((totalBytes >> 0) & 0xff));
                stream.WriteByte((byte)((totalBytes >> 8) & 0xff));
                stream.WriteByte((byte)((totalBytes >> 16) & 0xff));
                stream.WriteByte((byte)((totalBytes >> 24) & 0xff));

                stream.WriteByte((byte)((timeBytes >> 0) & 0xff));
                stream.WriteByte((byte)((timeBytes >> 8) & 0xff));
                stream.WriteByte((byte)((timeBytes >> 16) & 0xff));
                stream.WriteByte((byte)((timeBytes >> 24) & 0xff));
                stream.WriteByte((byte)((timeBytes >> 32) & 0xff));
                stream.WriteByte((byte)((timeBytes >> 40) & 0xff));
                stream.WriteByte((byte)((timeBytes >> 48) & 0xff));
                stream.WriteByte((byte)((timeBytes >> 56) & 0xff));

                stream.WriteByte((byte)((timeOffsetBytes >> 0) & 0xff));
                stream.WriteByte((byte)((timeOffsetBytes >> 8) & 0xff));


                stream.WriteByte((byte)LVL);
                stream.WriteByte((byte)(FLG >> 8));
                stream.WriteByte((byte)(FLG & 255));
                stream.WriteByte((byte)QDX);
                stream.WriteByte((byte)Data.Length);
                stream.WriteByte((byte)((PLBLength >> 0) & 0xff));
                stream.WriteByte((byte)((PLBLength >> 8) & 0xff));
                stream.WriteByte((byte)((PLBLength >> 16) & 0xff));
                stream.WriteByte((byte)((PLBLength >> 24) & 0xff));
                stream.Write(dataBytes, 0, dataByteCounter);
                if (PLBLength > 0)
                    stream.Write(PLB, 0, PLBLength);

                packetBuffer = new byte[totalBytes];

                TheCommonUtils.cdeBlockCopy(stream.GetBuffer(), 0, packetBuffer, pOffset, packetBuffer.Length - pOffset);

                if (totalBytes != packetBuffer.Length)
                    throw new InvalidOperationException("Sending Invalid Packet Length");
            }

            return packetBuffer;
        }

        /// <summary>
        /// Deserializes the TSM from the C-DEngine binary format
        /// This is not used for the HTTP channel but reserverd for future use on other TCP/UPD channels
        /// </summary>
        /// <param name="pInBuffer"></param>
        /// <param name="pOffset"></param>
        /// <returns></returns>
        internal static TSM FromPacketBytes(byte[] pInBuffer, int pOffset)
        {
            int numberOfDataEntries = 0;
            byte[] dataBytes = null;
            TSM tNewTSM = new TSM();    //Save not to write ORG or SID

            using (MemoryStream byteStream = new MemoryStream(pInBuffer, pOffset, pInBuffer.Length - pOffset))
            {
                int packetSize = (byteStream.ReadByte() << 0);
                packetSize |= (byteStream.ReadByte() << 8);
                packetSize |= (byteStream.ReadByte() << 16);
                packetSize |= (byteStream.ReadByte() << 24);
                if (packetSize != pInBuffer.Length)
                    throw new InvalidOperationException("Invalid packet size");

                long timeBinary = byteStream.ReadByte();
                timeBinary |= ((long)byteStream.ReadByte()) << 8;
                timeBinary |= ((long)byteStream.ReadByte()) << 16;
                timeBinary |= ((long)byteStream.ReadByte()) << 24;
                timeBinary |= ((long)byteStream.ReadByte()) << 32;
                timeBinary |= ((long)byteStream.ReadByte()) << 40;
                timeBinary |= ((long)byteStream.ReadByte()) << 48;
                timeBinary |= ((long)byteStream.ReadByte()) << 56;

                int timeOffsetInMinutes = (byteStream.ReadByte() << 0);
                timeOffsetInMinutes |= (byteStream.ReadByte() << 8);

                tNewTSM.TIM = new DateTimeOffset(DateTime.FromFileTime(timeBinary), new TimeSpan(0, timeOffsetInMinutes, 0));

                tNewTSM.LVL = (eMsgLevel)byteStream.ReadByte();
                tNewTSM.FLG = (ushort)(((ushort)byteStream.ReadByte()) << 8);
                tNewTSM.FLG += (ushort)byteStream.ReadByte();
                tNewTSM.QDX += byteStream.ReadByte();
                numberOfDataEntries = byteStream.ReadByte();
                int PLBlength = byteStream.ReadByte();
                PLBlength += byteStream.ReadByte() << 8;
                PLBlength += byteStream.ReadByte() << 16;
                PLBlength += byteStream.ReadByte() << 24;
                dataBytes = new byte[packetSize - (21 + PLBlength + pOffset)];
                byteStream.Read(dataBytes, 0, dataBytes.Length);

                if (PLBlength > 0)
                {
                    tNewTSM.PLB = new byte[PLBlength];
                    byteStream.Read(tNewTSM.PLB, 0, PLBlength);
                }
            }

            int byteIndex = 0;
            for (int n = 0; n < numberOfDataEntries; n++)
            {
                //int dataStringLength = BitConverter.ToInt32(dataBytes, byteIndex);
                int dataStringLength = (dataBytes[byteIndex] << 0);
                dataStringLength |= (dataBytes[byteIndex+1] << 8);
                dataStringLength |= (dataBytes[byteIndex+2] << 16);
                dataStringLength |= (dataBytes[byteIndex+3] << 24);
                byteIndex += 4;

                string dataString = TheCommonUtils.CArray2UTF8String(dataBytes, byteIndex, dataStringLength);
                byteIndex += dataStringLength;

                switch (n)
                {
                    case 0: tNewTSM.TXT = dataString; break;
                    case 1: tNewTSM.PLS = dataString; break;
                    case 2: tNewTSM.ORG = dataString; break;//ORG-OK
                    case 3: tNewTSM.ENG = dataString; break;
                    case 4: tNewTSM.FID = dataString; break;
                    case 5: tNewTSM.SID = dataString; break;
                    case 6: tNewTSM.SEID = dataString; break;
                    case 7: tNewTSM.UID = dataString; break;
                    case 8: tNewTSM.CST = dataString; break;
                    case 9: tNewTSM.OWN = dataString; break;
                }
            }
            return tNewTSM;
        }
    }
}
