// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.Interfaces;
using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using System;
using System.Security.Cryptography;
using System.Text;
using CU = nsCDEngine.BaseClasses.TheCommonUtils;

namespace nsCDEngine.Security
{
    //Scope Rules:
    //---------------
    //EasyScopeID: 8 Digits Base32
    //RealScopeID: Guid of cdeAK with intermixed EasyScopeID
    //Scrambled ScopeID: Base64 Version of RealScopeID
    //ISB Path: <5DigitsRandom><ProtocolVersion>_<FID-Counter>_<OriginSenderType>_<SessionGuid>

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>   Public interfaces to the scope Manager </summary>
    ///
    /// <remarks>   Chris, 3/31/2020. </remarks>
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    public static class TheScopeManager
    {
        #region Application ID
        /// <summary>
        /// Sets the main Application ID of the C-DEngine Node Mesh. All nodes communicating with each other have to have the Same Application ID and Scope ID (a.k.a. "Security ID").
        /// The Application ID has be to issued by C-Labs and can only be self-generated if C-Labs has authorized the generation to an OEM
        /// ATTENTION: Losing or publishing the Application ID can lead to security issues. Store this ID in a secure store, certificate, USB Key or other secure way.
        ///
        /// This has been moved here from TheScopeManager as "TheBaseAssets.MySecrets" has to be created before the AppID can be set.
        /// This function will create the default cdeSecrets if it was not set before by the Application
        /// </summary>
        /// <param name="AID">Application license key provided by C-Labs to enable access to C-Labs C-DEngine mesh services.</param>
        /// <returns></returns>
        public static bool SetApplicationID(string AID)
        {
            if (AID?.Length > 5)
            {
                TheBaseAssets.MySecrets ??= new TheDefaultSecrets();
                TheBaseAssets.MyCrypto ??= new TheDefaultCrypto(TheBaseAssets.MySecrets);
                TheBaseAssets.MySecrets.SatKays(AID);
                if (TheBaseAssets.MySecrets.IsApplicationIDValid())
                    return true;
            }
            return false;
        }
        #endregion

        #region uncritical Public ScopeID Methods

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Sets the node-scope from an easy id. </summary>
        ///
        /// <remarks>   Chris, 3/31/2020. </remarks>
        ///
        /// <param name="pEasyScope">   . </param>
        ///
        /// <returns>   True if it succeeds, false if it fails. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public static bool SetScopeIDFromEasyID(string pEasyScope)
        {
            if (TheBaseAssets.MyScopeManager != null)
            {
                TheBaseAssets.MyScopeManager.SetScopeIDFromEasyID(pEasyScope);
                return true;
            }
            return false;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Gets scrambled scope id from the current node. </summary>
        ///
        /// <remarks>   Chris, 3/31/2020. </remarks>
        ///
        /// <returns>   The scrambled scope identifier. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public static string GetScrambledScopeID()
        {
            return TheBaseAssets.MyScopeManager?.GetScrambledScopeID();
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Generates a new scope Easy Scope ID. </summary>
        ///
        /// <remarks>   Chris, 3/31/2020. </remarks>
        ///
        /// <returns>   The new easy scope id. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public static string GenerateNewScopeID()
        {
            return TheBaseAssets.MyScopeManager?.GenerateNewScopeID();
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Generates a friendly code in the format of an EasyScopeID. </summary>
        ///
        /// <remarks>   Chris, 3/31/2020. </remarks>
        ///
        /// <param name="digits">   (Optional) The number of digits requested. </param>
        ///
        /// <returns>   The code. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public static string GenerateCode(int digits=8)
        {
            return TheBaseAssets.MyScopeManager?.GenerateCode(digits);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Registers a callback that fires if the scope changed </summary>
        ///
        /// <remarks>   Chris, 3/31/2020. </remarks>
        ///
        /// <param name="pCallback">    The callback. </param>
        ///
        /// <returns>   True if it succeeds, false if it fails. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public static bool RegisterScopeChanged(Action<bool> pCallback)
        {
            if (TheBaseAssets.MyScopeManager == null) return false;
            TheBaseAssets.MyScopeManager?.RegisterScopeChanged(pCallback);
            return true;
        }

        /// <summary>
        /// Gets the 4 digits scope hash.
        /// </summary>
        /// <param name="pTopic">The topic. If IsSScope is true, the pTopic is viewed as a Scrambled ScopeID.</param>
        /// <param name="IsSScope">if set to <c>true</c> [is pTopic is a scrambled Scope ID].</param>
        /// <returns>System.String.</returns>
        public static string GetScopeHash(string pTopic, bool IsSScope=false)
        {
            if (TheBaseAssets.MyScopeManager == null) return null;
            return TheBaseAssets.MyScopeManager?.GetScopeHash(pTopic, IsSScope);
        }

        /// <summary>
        /// In order to tag data with a given ScopeID (Node Realm), this function can be used to get a Hash of the CurrentScopeID (if parameter pSScope is null)
        /// This hash is not used for any encryption and a scopeID cannot be recreated from this Hash
        /// Function returns a hash-token from an Scrambled Scope ID
        /// </summary>
        /// <param name="pSScope"></param>
        /// <returns></returns>
        public static string GetTokenFromScrambledScopeID(string pSScope = null)
        {
            if (string.IsNullOrEmpty(pSScope))
                return TheBaseAssets.MyScopeManager?.GetTokenFromScopeID();
            return TheBaseAssets.MyScopeManager?.GetTokenFromScrambledScopeID(pSScope);
        }
        /// <summary>
        /// This method creates a new scrambled ScopeID from an Easy ScopeID to allow encryption of data against a ScopeID
        /// </summary>
        /// <param name="pEasyScope"></param>
        /// <param name="bNoLogging"></param>
        /// <param name="bUseEasyScope16"></param>
        /// <returns></returns>
        public static string GetScrambledScopeIDFromEasyID(string pEasyScope, bool bNoLogging = false, bool bUseEasyScope16 = false)
        {
            return TheBaseAssets.MyScopeManager?.GetScrambledScopeIDFromEasyID(pEasyScope,bNoLogging, bUseEasyScope16);
        }

        /// <summary>
        /// Verify if the given ScrambledScopeID is valid in the current scope
        /// </summary>
        /// <param name="pSScope"></param>
        /// <returns></returns>
        public static bool IsValidScopeID(string pSScope)
        {
            return TheBaseAssets.MyScopeManager?.IsValidScopeID(pSScope) == true;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>   Verify if the given ScrambledScopeIDs are of the same scope. </summary>
        ///
        /// <remarks>   Chris, 3/31/2020. </remarks>
        ///
        /// <param name="pSScope">  First Scrambled Scope ID </param>
        /// <param name="pSScope2"> The second scrambled scope ID. </param>
        ///
        /// <returns>   True if valid scope identifier, false if not. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public static bool IsValidScopeID(string pSScope, string pSScope2)
        {
            return TheBaseAssets.MyScopeManager?.IsValidScopeID(pSScope, pSScope2) == true;
        }

        /// <summary>
        /// Creates a new DeviceID/NodeID for the current Application Scope
        /// </summary>
        /// <param name="tS">Sender Type of the Node</param>
        /// <returns></returns>
        public static Guid GenerateNewAppDeviceID(cdeSenderType tS)
        {
            if (TheBaseAssets.MyScopeManager == null) return Guid.NewGuid();
            return TheBaseAssets.MyScopeManager.GenerateNewAppDeviceID(tS);
        }
        #endregion
    }

    /// <summary>
    /// The Scope Manager Core
    /// </summary>
    internal class TheDefaultScopeManager : ICDEScopeManager
    {
        #region ICDEScopeManager Interfaces

        public void SetMiniHSI(TheServiceHostInfoMini pMini)
        {
            MyServiceHostInfo = pMini;
        }

        private string FastSScopeID = null;
        private string mScId;
        public string ScopeID
        {
            get { return mScId; }
            set { mScId = value; FastSScopeID = null; }
        }
        public bool IsScopingEnabled { get; set; }
        public string FederationID { get; set; }

        /// <summary>
        /// Sets the ScopeID of the current node using the EasyScopeID given.
        /// The system does not use the EasyScopeID for any encryption but creates a real-scopeID from the easyID
        /// </summary>
        /// <param name="pEasyID"></param>
        public void SetScopeIDFromEasyID(string pEasyID)
        {
            bool ReqConfig = false;
            if (!string.IsNullOrEmpty(pEasyID))
            {
                ScopeID = GetRealScopeIDFromEasyID(pEasyID);       //GRSI: rare
                IsScopingEnabled = true;
            }
            else //NEW: RC1.1
            {
                IsScopingEnabled = false;
                ReqConfig = true;
            }
            eventScopeIDChanged?.Invoke(ReqConfig);
        }


        public void SetScopeIDFromScrambledID(string pScrambledId)
        {
            if (string.IsNullOrEmpty(pScrambledId))
                return;
            ScopeID = GetRealScopeID(pScrambledId);     //GRSI: Low frequency
            IsScopingEnabled = true;
            eventScopeIDChanged?.Invoke(false);
        }
        public string GetRealScopeID(string pScramScopeId)
        {
            if (string.IsNullOrEmpty(pScramScopeId)) return "";

            pScramScopeId = pScramScopeId.Split(chunkSeparator, StringSplitOptions.None)[0];
            string[] tStr = Base64Decode(pScramScopeId).Split('&');
            MyServiceHostInfo?.MyKPIs?.IncrementKPI(eKPINames.KPI5);
            if (tStr.Length > 1)
                return tStr[1];
            else
                return "";
        }
        public string GetScrambledScopeID()
        {
            if (!IsScopingEnabled) return "";
            if (FastSScopeID != null)
                return FastSScopeID;
            MyServiceHostInfo?.MyKPIs?.IncrementKPI(eKPINames.KPI4);
            string t = Base64Encode(CU.GetRandomUInt(0, 1000) + "&" + ScopeID);
            if (MyServiceHostInfo?.EnableFastSecurity == true)
                FastSScopeID = t;
            return t;
        }

        public string GetScrambledScopeID(string pRealScopeID, bool IsRealId)
        {
            if (string.IsNullOrEmpty(pRealScopeID)) return "";

            MyServiceHostInfo?.MyKPIs?.IncrementKPI(eKPINames.KPI4);
            if (IsRealId)
                return Base64Encode(CU.GetRandomUInt(0, 1000) + "&" + pRealScopeID);
            else
                return Base64Encode(CU.GetRandomUInt(0, 1000) + "&" + GetRealScopeID(pRealScopeID));
        }
        public string GetRealScopeIDFromEasyID(string pEasyID, bool bNoLogging=false, bool bUseEasyScope16=false)
        {
            return InsertCodeIntoGUID(pEasyID, CU.CGuid(TheBaseAssets.MySecrets.GetAK()), bUseEasyScope16).ToString();
        }

        public bool IsInScope(string pMsgSID, bool IsRealScopeID)
        {
            string pMsgFID = "";
            bool TestForFID = false;
            if (IsScopingEnabled && !string.IsNullOrEmpty(FederationID) && !string.IsNullOrEmpty(pMsgSID))
            {
                TestForFID = true;
                string[] t = pMsgSID.Split(';');
                if (t.Length > 1)
                {
                    pMsgSID = t[0];
                    pMsgFID = t[1];
                }
            }
            if (IsRealScopeID)
                return (!IsScopingEnabled && string.IsNullOrEmpty(pMsgSID)) ||
                    (!string.IsNullOrEmpty(ScopeID) && ScopeID.Equals(pMsgSID)) ||
                    (TestForFID && !string.IsNullOrEmpty(FederationID) && FederationID.Equals(GetRealScopeID(pMsgFID)));     //GRSI: rare
            else
                return (!IsScopingEnabled && string.IsNullOrEmpty(pMsgSID)) ||
                    (!string.IsNullOrEmpty(ScopeID) && ScopeID.Equals(GetRealScopeID(pMsgSID))) ||     //GRSI: rare
                    (TestForFID && !string.IsNullOrEmpty(FederationID) && FederationID.Equals(GetRealScopeID(pMsgFID)));     //GRSI: rare
        }

        public string GetRealScopeIDFromTopic(string pScopedTopic, out string TopicName)
        {
            TopicName = pScopedTopic;
            if (string.IsNullOrEmpty(pScopedTopic)) return "";
            string[] tt = pScopedTopic.Split('@');
            TopicName = tt[0];
            if (tt.Length > 1)
            {
                var tR = GetRealScopeID(tt[1]);     //GRSI: higher frequency - at least once for each TSM to be published
                if (tR.Length > 0)
                    return tR;
            }
            return "";
        }

        /// <summary>
        /// Adds the ScopeID (Security ID) to the topic.
        /// The Result will look like TOPIC@SCRAMBLEDSCOPEID
        /// </summary>
        /// <param name="pTopic">Topic that will receive the ScopeID</param>
        /// <returns></returns>
        public string AddScopeID(string pTopic)
        {
            string tTSMSID = null;
            return AddScopeID(pTopic, ScopeID, ref tTSMSID, true, true);
        }
        /// <summary>
        /// Adds the ScopeID (Security ID) to the topic(s).
        /// The Result will look like TOPIC@SCRAMBLEDSCOPEID;NEXTTOPIC@SCRAMBLEDID...
        /// </summary>
        /// <param name="pTopics">A list of topics separated by ;</param>
        /// <param name="bFirstTopicOnly">If set to true, only the first topic will receive the ScopeID</param>
        /// <returns></returns>
        public string AddScopeID(string pTopics, bool bFirstTopicOnly)
        {
            string tTSMSID = null;
            return AddScopeID(pTopics, ScopeID, ref tTSMSID, bFirstTopicOnly, true);
        }
        /// <summary>
        /// Adds the ScopeID (Security ID) to the topic(s).
        /// The Result will look like TOPIC@SCRAMBLEDSCOPEID;NEXTTOPIC@SCRAMBLEDID...
        /// </summary>
        /// <param name="pTopics">A list of topics separated by ;</param>
        /// <param name="bFirstTopicOnly"></param>
        /// <param name="pRealScope">Allows to provide a scrambled ScopeID that will be added to the Topics</param>
        /// <returns></returns>
        public string AddScopeID(string pTopics, bool bFirstTopicOnly, string pRealScope)
        {
            string tTSMSID = null;
            return AddScopeID(pTopics, pRealScope, ref tTSMSID, bFirstTopicOnly, true);
        }
        /// <summary>
        /// Adds the ScopeID (Security ID) to the topic(s).
        /// The Result will look like TOPIC@SCRAMBLEDSCOPEID;NEXTTOPIC@SCRAMBLEDID...
        /// </summary>
        /// <param name="pTopics">A list of topics separated by ;</param>
        /// <param name="pMessage">If Message is not null and has a ScopeID, the ScopeID of the message is taken instead of the ScopeID of this node
        /// This can be useful when returning a scoped (Secure) message to an Originator that has a different scope than the current node
        /// </param>
        /// <param name="bFirstTopicOnly">If set to true, only the first topic will receive the ScopeID</param>
        /// <returns></returns>
        public string AddScopeID(string pTopics, ref string pMessage, bool bFirstTopicOnly)
        {
            return AddScopeID(pTopics, ScopeID, ref pMessage, bFirstTopicOnly, true);
        }
        /// <summary>
        /// Adds the ScopeID (Security ID) to the topic(s).
        /// The Result will look like TOPIC@SCRAMBLEDSCOPEID;NEXTTOPIC@SCRAMBLEDID...
        /// </summary>
        /// <param name="pTopics">A list of topics separated by ;</param>
        /// <param name="pScramScopeID">Allows to provide a scrambled ScopeID that will be added to the Topics</param>
        /// <param name="pMessageSID">If Message is not null and has a ScopeID, the ScopeID of the message is taken instead of the ScopeID of this node
        /// This can be useful when returning a scoped (Secure) message to an Originator that has a different scope than the current node
        /// </param>
        /// <param name="bFirstTopicOnly">If set to true, only the first topic will receive the ScopeID</param>
        /// <param name="IsScramRScope">If true the parameter is a real-scopeid</param>
        /// <returns></returns>
        public string AddScopeID(string pTopics, string pScramScopeID, ref string pMessageSID, bool bFirstTopicOnly, bool IsScramRScope)
        {
            if (string.IsNullOrEmpty(pTopics)) return "";
            if (string.IsNullOrEmpty(pScramScopeID) && string.IsNullOrEmpty(pMessageSID))
                return pTopics;
            else
            {
                string tTopic = ""; //perf tuning <- allocation of string on stack costs time
                string tRealS = !string.IsNullOrEmpty(pScramScopeID) ? (IsScramRScope ? pScramScopeID : GetRealScopeID(pScramScopeID)) : GetRealScopeID(pMessageSID);    //GRSI: possible high frequency - needs research
                string[] tTops = pTopics.Split(';');
                string tScrambledID = null;
                foreach (string t in tTops)
                {
                    if (string.IsNullOrEmpty(t)) continue;
                    if (bFirstTopicOnly && tTopic.Length > 0)
                    {
                        tTopic += ";" + t;
                        continue;
                    }

                    if (tTopic.Length > 0)
                        tTopic += ";";
                    if (!string.IsNullOrEmpty(pScramScopeID))
                    {
                        if (tRealS.Length > 0)
                        {
                            if (FindRealScopeID(t).Equals(tRealS))   //GRSI: medium
                                tTopic += t;
                            else
                            {
                                if (!t.Contains("@"))
                                {
                                    if (tScrambledID == null || (MyServiceHostInfo?.EnableFastSecurity != true))    //SECURITY: If EnableFast Security is true, all Topics wil have same SScopeID
                                    {
                                        tScrambledID = GetScrambledScopeID(pScramScopeID, IsScramRScope);   //GRSI: rare
                                    }
                                    if (pMessageSID != null)
                                        pMessageSID = (IsScramRScope ? tScrambledID : pScramScopeID);  //SID in TSM must match Topic SID otherwise QSender will reject message routing
                                    tTopic += t + "@" + (IsScramRScope ? tScrambledID : pScramScopeID);
                                    if (!string.IsNullOrEmpty(FederationID))
                                        tTopic += ":" + FederationID;
                                }
                                else
                                    tTopic += t;
                            }
                        }
                        else
                            tTopic += t;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(tRealS) && !t.Contains("@") && !FindRealScopeID(t).Equals(tRealS))  //GRSI: rare
                        {
                            tTopic += t + "@" + pMessageSID;
                            if (!string.IsNullOrEmpty(FederationID))
                                tTopic += ":" + FederationID;
                        }
                        else
                            tTopic += t;
                    }
                }
                return tTopic;
            }
        }

        public string RemoveScopeID(string pCommand, bool IsUnscopedAllowed, bool IsAllowedForeignProcessing)
        {
            if (string.IsNullOrEmpty(pCommand)) return pCommand;
            if (!IsScopingEnabled)
            {
                if (IsUnscopedAllowed && pCommand.Contains("@"))
                    return pCommand.Substring(0, pCommand.IndexOf("@", StringComparison.Ordinal));
                return pCommand;
            }
            string tRSID = FindRealScopeID(pCommand);      //GRSI: rare
            if (string.IsNullOrEmpty(tRSID))
            {
                if (IsUnscopedAllowed)
                    return pCommand;
                return "SCOPEVIOLATION";
            }
            else
            {
                if (!IsAllowedForeignProcessing && !ScopeID.Equals(tRSID) && (string.IsNullOrEmpty(RProSco) || !tRSID.Equals(RProSco)))
                    return "SCOPEVIOLATION";
                string tC = pCommand.Substring(0, pCommand.IndexOf('@'));
                return tC;
            }
        }

        private Action<bool> eventScopeIDChanged;
        public void RegisterScopeChanged(Action<bool> pCallback)
        {
            eventScopeIDChanged += pCallback;
        }
        public void UnregisterScopeChanged(Action<bool> pCallback)
        {
            eventScopeIDChanged -= pCallback;
        }

        public string GetSessionIDFromISB(string pRealPage)
        {
            string[] tQ = Base64Decode(pRealPage).Split('_');
            if (tQ.Length > 3) return tQ[3];
            return null;
        }

        public bool ParseISBPath(string pRealPage, out Guid? pSessionID, out cdeSenderType pType, out long pFID, out string pVersion)
        {
            pSessionID = null;
            pType = cdeSenderType.NOTSET;
            pFID = 0;
            pVersion = "";

            try
            {
                string[] tQ = Base64Decode(pRealPage).Split('_');

                pType = tQ.Length == 4 ? (cdeSenderType)(CU.CInt(tQ[2])) : cdeSenderType.NOTSET;
                if (tQ.Length > 1) pFID = CU.CLng(tQ[1]);
                if (tQ.Length > 3) pSessionID = CU.CGuid(tQ[3]);
                if (tQ[0].Length == 7)
                    pVersion = tQ[0].Substring(5);
            }
            catch (Exception)
            {
                // ignored
            }
            return true;
        }

        public string GetISBPath(string PathPrefix, cdeSenderType pOriginType, cdeSenderType pDestinationType, long pCounter, Guid pSessionID, bool IsWS)
        {
            if (string.IsNullOrEmpty(TheBaseAssets.MySecrets.GetApID5())) return "";
            if (string.IsNullOrEmpty(PathPrefix))
                PathPrefix = "/";
            else
            {
                if (!PathPrefix.EndsWith("/")) PathPrefix += "/";
            }
            string pSessID = pSessionID.ToString();
            if (pCounter == 1 && pSessionID == Guid.Empty && (MyServiceHostInfo?.UseFixedConnectionUrl != true))
                pSessID = "P" + GetCryptoGuid().ToString().Substring(1);
            MyServiceHostInfo?.MyKPIs?.IncrementKPI(eKPINames.KPI4);
            string tPath = PathPrefix + "ISB" + Uri.EscapeUriString(Base64Encode($"{string.Format("{0:00000}",CU.GetRandomUInt(0, 99999))}{(string.IsNullOrEmpty(MyServiceHostInfo?.ProtocolVersion)?"40":MyServiceHostInfo?.ProtocolVersion)}_{pCounter}_{((int)(pOriginType))}_{pSessID}"));
            if (IsWS && (pDestinationType == cdeSenderType.CDE_CLOUDROUTE || pDestinationType == cdeSenderType.CDE_SERVICE || pDestinationType == cdeSenderType.CDE_CUSTOMISB))
                tPath += ".ashx";
            return tPath;
        }

        public void SetProSeScope(string pSScope)
        {
            RProSco = GetRealScopeID(pSScope);   //GRSI: rare
        }

        //Old statics - security uncritical

        /// <summary>
        /// Used to create a random new EasyScopeID.
        /// </summary>
        /// <returns></returns>
        public string GenerateNewScopeID()
        {
            cdeSenderType tSender = cdeSenderType.NOTSET;
            if (MyServiceHostInfo != null)
                tSender = MyServiceHostInfo.MyDeviceSenderType;
            return CalculateRegisterCode(GenerateNewAppDeviceID(tSender));
        }

        public string GetTokenFromScopeID()
        {
            if (!IsScopingEnabled) return "";
            return ScopeID.GetHashCode().ToString();
        }
        public string GetTokenFromEasyScopeID(string pEasyID)
        {
            if (string.IsNullOrEmpty(pEasyID)) return "";
            string tID = GetRealScopeIDFromEasyID(pEasyID);       //GRSI: rare
            return tID.GetHashCode().ToString();
        }
        public string GetTokenFromScrambledScopeID(string SScopeID)
        {
            if (string.IsNullOrEmpty(SScopeID)) return "";
            string tID = GetRealScopeID(SScopeID);       //GRSI: Low frequency
            return tID.GetHashCode().ToString();
        }

        public string GetScrambledScopeIDFromEasyID(string pEasyScope)
        {
            return GetScrambledScopeIDFromEasyID(pEasyScope, false, MyServiceHostInfo?.UseEasyScope16==true);
        }

        /// <summary>
        /// Return a Scrambled ScopeID from an easy scopeID
        /// </summary>
        /// <param name="pEasyScope">Source EasyScope</param>
        /// <param name="bNoLogging">If true, errors will not be logged</param>
        /// <param name="bUseEasyScope16">if true, the EasyScopeID can have up to 16 characters. Attention: this might not be compatible to existing mesh setups as all nodes in a mesh need to understand 16chars ScopeIDs</param>
        /// <returns></returns>
        public string GetScrambledScopeIDFromEasyID(string pEasyScope, bool bNoLogging = false, bool bUseEasyScope16 = false)
        {
            if (string.IsNullOrEmpty(pEasyScope)) return null;  //NEW 10/20/2012
            Guid tG = CU.CGuid(TheBaseAssets.MySecrets.GetAK());
            MyServiceHostInfo?.MyKPIs?.IncrementKPI(eKPINames.KPI4);
            return Base64Encode($"{CU.GetRandomUInt(0, 1000)}&{InsertCodeIntoGUID(pEasyScope, tG, bUseEasyScope16)}");
        }

        /// <summary>
        /// Returns a short hash from a given Topic. If pTopic is null, the hash of the Scope of the current node is used
        /// </summary>
        /// <param name="pTopic">Subscription topic or ScrambledScopeID</param>
        /// <param name="IsSScope">Set to true if pTopic is a scrambled scopeID</param>
        /// <returns></returns>
        public string GetScopeHash(string pTopic, bool IsSScope = false)
        {
            if (string.IsNullOrEmpty(pTopic))
                return ScopeID.ToUpper().Substring(0, 4);
            string t;
            if (IsSScope)
                t = GetRealScopeID(pTopic);    //GRSI: Low frequency
            else
                t = FindRealScopeID(pTopic);
            if (!string.IsNullOrEmpty(t) && t.Length > 4)
                return t.Substring(0, 4).ToUpper();
            return "not scoped";
        }

        /// <summary>
        /// Returns the cdeSenderType of a given NodeID
        /// CM:OK
        /// </summary>
        /// <param name="pNodeID">Source NodeID</param>
        /// <returns></returns>
        public cdeSenderType GetSenderTypeFromDeviceID(Guid pNodeID)
        {
            string tN = pNodeID.ToString();
            return (cdeSenderType)CU.CInt(tN.Substring(tN.Length-1, 1));
        }

        /// <summary>
        /// Used to create a random new EasyScopeID.
        /// CM:OK
        /// </summary>
        /// <returns></returns>
        public string GenerateCode(int pDigits = 8)
        {
            cdeSenderType tSender = cdeSenderType.NOTSET;
            if (MyServiceHostInfo != null)
                tSender = MyServiceHostInfo.MyDeviceSenderType;
            return GetRegisterCode(GenerateNewAppDeviceID(tSender), pDigits);
        }

        public bool IsValidScopeID(string pScramScopeID1)
        {
            return IsValidScopeID(pScramScopeID1, GetScrambledScopeID());     //GRSI: rare
        }
        /// <summary>
        /// Compare two ScrambledScopeIDs if they are of the same Scope
        /// </summary>
        /// <param name="pScramScopeID1"></param>
        /// <param name="pScramScopeID2"></param>
        /// <returns></returns>
        public bool IsValidScopeID(string pScramScopeID1, string pScramScopeID2)
        {
            if (string.IsNullOrEmpty(pScramScopeID1) && string.IsNullOrEmpty(pScramScopeID2)) return true;
            if (string.IsNullOrEmpty(pScramScopeID1) || string.IsNullOrEmpty(pScramScopeID2)) return false;
            string tRealScopeID1 = GetRealScopeID(pScramScopeID1);   //GRSI: low frequency
            if (string.IsNullOrEmpty(tRealScopeID1)) return false;
            return tRealScopeID1.Equals(GetRealScopeID(pScramScopeID2));  //GRSI: low frequency
        }

        public Guid GenerateNewAppDeviceID(cdeSenderType tS)
        {
            string tg = Guid.NewGuid().ToString();
            string t = tg.Substring(0, 35);
            t += ((int)tS).ToString();
            return CU.CGuid(t);
        }

        public string CalculateRegisterCode(Guid pGuid)
        {
            string tstr = pGuid.ToString().Substring(0, 8) + pGuid.ToString().Substring(9, 2);
            ulong calc = ulong.Parse(tstr, System.Globalization.NumberStyles.AllowHexSpecifier);

            string code = "";
            for (int i = 0; i < 8; i++)
            {
                int tt = (int)(calc >> (i * 5));
                int tShift = tt & 0x1F;
                code = TheBaseAssets.MySecrets.GetCodeArray()[tShift] + code;
            }

            return code;
        }

        #endregion

        private static TheServiceHostInfoMini MyServiceHostInfo = null;
        private static readonly string[] chunkSeparator = new string[] { ":,:" };
        private static string RProSco { get; set; }

        /// <summary>
        /// Returns a cryptographic unique Guid
        /// </summary>
        /// <returns></returns>
        private static Guid GetCryptoGuid()
        {
            RNGCryptoServiceProvider crypto = new ();
            byte[] data = new byte[16];
            crypto.GetBytes(data);
            return CU.CGuid(data);
        }

        private static string GetRegisterCode(Guid pGuid, int pDigits = 8)
        {
            string tstr = pGuid.ToString().Replace("-", "").Substring(0, 16);
            ulong calc = ulong.Parse(tstr, System.Globalization.NumberStyles.AllowHexSpecifier);

            string code = "";
            for (int i = 0; i < pDigits; i++)
            {
                int tt = (int)(calc >> (i * 5));
                int tShift = tt & 0x1F;
                code = TheBaseAssets.MySecrets.GetCodeArray()[tShift] + code;
            }

            return code;
        }

        /// <summary>
        /// Looks in the topics for a RealScopeID and returns the first found
        /// Topics cannot have different RealSCopeIDs!!
        /// </summary>
        /// <param name="tScopedTopics"></param>
        /// <returns></returns>
        private string FindRealScopeID(string tScopedTopics)
        {
            if (string.IsNullOrEmpty(tScopedTopics)) return "";
            string[] t = tScopedTopics.Split(';');
            foreach (string str in t)
            {
                string[] tt = str.Split('@');
                if (tt.Length > 1)
                {
                    string tRSID = GetRealScopeID(tt[1]);       //GRSI: High frequency - needs tuning
                    if (!string.IsNullOrEmpty(tRSID))
                        return tRSID;
                }
            }
            return "";
        }

        private static Guid InsertCodeIntoGUID(string pCode, Guid pGuid, bool useEasyScope16)
        {
            string tg = MD5(pCode, useEasyScope16);
            string tguid = pGuid.ToString().Replace("{", "").Replace("}", "").Replace("-", "");
            if (tg.Length > tguid.Length)
                return CU.CGuid(tg.Substring(0,tguid.Length));
            return CU.CGuid(tg + tguid.Substring(0, tguid.Length-tg.Length));
        }

        private static string MD5(string text, bool useEasyScope16)
        {
            var result = default(string);
            using (var algo = new SHA512Managed()) // new MD5CryptoServiceProvider())
            {
                algo.ComputeHash(Encoding.UTF8.GetBytes(text));
                result = CU.ToHexString(algo.Hash);
            }
            return result.Substring(0,useEasyScope16?16:8);
        }

        private static string Base64Encode(string plainText)
        {
            try
            {
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                return System.Convert.ToBase64String(plainTextBytes);
            }
            catch (Exception)
            {
                //ignored
            }
            return "";
        }

        private static string Base64Decode(string base64EncodedData)
        {
            try
            {
                var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
                return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
            }
            catch (Exception)
            {
                //ignored
            }
            return "";
        }
    }
}