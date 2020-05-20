// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.ViewModels;
using System;

namespace nsCDEngine.Interfaces
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Interface for the customer scope manager.
    /// </summary>
    /// <remarks>Chris, 3/25/2020.</remarks>
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    public interface ICDEScopeManager
    {
        /// <summary>
        /// Sets the mini hsi.
        /// </summary>
        /// <param name="pMini">The mini host info.</param>
        void SetMiniHSI(TheServiceHostInfoMini pMini);
        /// <summary>
        /// Gets the scope identifier.
        /// </summary>
        /// <value>The scope identifier.</value>
        string ScopeID { get; } //RealScopeID returned!
        /// <summary>
        /// Gets or sets the federation identifier.
        /// </summary>
        /// <value>The federation identifier.</value>
        string FederationID { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance is scoping enabled.
        /// </summary>
        /// <value><c>true</c> if this instance is scoping enabled; otherwise, <c>false</c>.</value>
        bool IsScopingEnabled { get; }
        /// <summary>
        /// Registers the scope changed.
        /// </summary>
        /// <param name="pCallback">The callback.</param>
        void RegisterScopeChanged(Action<bool> pCallback);
        /// <summary>
        /// Unregisters the scope changed.
        /// </summary>
        /// <param name="pCallback">The callback.</param>
        void UnregisterScopeChanged(Action<bool> pCallback);

        /// <summary>
        /// Sets the scope identifier from easy identifier.
        /// </summary>
        /// <param name="pEasyID">The easy identifier.</param>
        void SetScopeIDFromEasyID(string pEasyID);
        /// <summary>
        /// Sets the scope identifier from scrambled identifier.
        /// </summary>
        /// <param name="pScrambledId">The scrambled identifier.</param>
        void SetScopeIDFromScrambledID(string pScrambledId);

        /// <summary>
        /// Determines whether [is in scope] [the specified p MSG sid].
        /// </summary>
        /// <param name="pMsgSID">The MSG sid.</param>
        /// <param name="IsRealScopeID">if set to <c>true</c> [is real scope identifier].</param>
        /// <returns><c>true</c> if [is in scope] [the specified MSG sid]; otherwise, <c>false</c>.</returns>
        bool IsInScope(string pMsgSID, bool IsRealScopeID);

        /// <summary>
        /// Gets the real scope identifier.
        /// </summary>
        /// <param name="pScramScopeId">The scrambled scope identifier.</param>
        /// <returns>System.String.</returns>
        string GetRealScopeID(string pScramScopeId); //RealScopeID returned!
        /// <summary>
        /// Gets the real scope identifier from easy identifier.
        /// </summary>
        /// <param name="pEasyID">The p easy identifier.</param>
        /// <param name="bNoLogging">if set to <c>true</c> [b no logging].</param>
        /// <param name="bUseEasyScope16">if set to <c>true</c> [b use easy scope16].</param>
        /// <returns>System.String.</returns>
        string GetRealScopeIDFromEasyID(string pEasyID, bool bNoLogging=false, bool bUseEasyScope16=false);    //RealScopeID returned!
        /// <summary>
        /// Gets the real scope identifier from topic.
        /// </summary>
        /// <param name="pScopedTopic">The scoped topic.</param>
        /// <param name="TopicName">Name of the topic.</param>
        /// <returns>System.String.</returns>
        string GetRealScopeIDFromTopic(string pScopedTopic, out string TopicName);  //RealScopeID returned!

        /// <summary>
        /// Gets the scrambled scope identifier.
        /// </summary>
        /// <returns>System.String.</returns>
        string GetScrambledScopeID();
        /// <summary>
        /// Gets the scrambled scope identifier.
        /// </summary>
        /// <param name="pRealScopeID">The real scope identifier.</param>
        /// <param name="IsRealId">if set to <c>true</c> [is real identifier].</param>
        /// <returns>System.String.</returns>
        string GetScrambledScopeID(string pRealScopeID, bool IsRealId);

        /// <summary>
        /// Adds the scope identifier.
        /// </summary>
        /// <param name="pTopic">The topic.</param>
        /// <returns>System.String.</returns>
        string AddScopeID(string pTopic);
        /// <summary>
        /// Adds the scope identifier.
        /// </summary>
        /// <param name="pTopics">The topics.</param>
        /// <param name="bFirstTopicOnly">if set to <c>true</c> [b first topic only].</param>
        /// <returns>System.String.</returns>
        string AddScopeID(string pTopics, bool bFirstTopicOnly);
        /// <summary>
        /// Adds the scope identifier.
        /// </summary>
        /// <param name="pTopics">The topics.</param>
        /// <param name="bFirstTopicOnly">if set to <c>true</c> [b first topic only].</param>
        /// <param name="pRealScope">The real scope.</param>
        /// <returns>System.String.</returns>
        string AddScopeID(string pTopics, bool bFirstTopicOnly, string pRealScope);
        /// <summary>
        /// Adds the scope identifier.
        /// </summary>
        /// <param name="pTopics">The p topics.</param>
        /// <param name="pMessage">The p message.</param>
        /// <param name="bFirstTopicOnly">if set to <c>true</c> [b first topic only].</param>
        /// <returns>System.String.</returns>
        string AddScopeID(string pTopics, ref string pMessage, bool bFirstTopicOnly);
        /// <summary>
        /// Adds the scope identifier.
        /// </summary>
        /// <param name="pTopics">The topics.</param>
        /// <param name="pScramScopeID">The scrambled scope identifier.</param>
        /// <param name="pMessageSID">The message sid.</param>
        /// <param name="bFirstTopicOnly">if set to <c>true</c> [b first topic only].</param>
        /// <param name="IsScramRScope">if set to <c>true</c> [is scram r scope].</param>
        /// <returns>System.String.</returns>
        string AddScopeID(string pTopics, string pScramScopeID, ref string pMessageSID, bool bFirstTopicOnly, bool IsScramRScope);
        /// <summary>
        /// Removes the scope identifier.
        /// </summary>
        /// <param name="pCommand">The command.</param>
        /// <param name="IsUnscopedAllowed">if set to <c>true</c> [is unscoped allowed].</param>
        /// <param name="IsAllowedForeignProcessing">if set to <c>true</c> [is allowed foreign processing].</param>
        /// <returns>System.String.</returns>
        string RemoveScopeID(string pCommand, bool IsUnscopedAllowed, bool IsAllowedForeignProcessing);

        /// <summary>
        /// Gets the session identifier from isb.
        /// </summary>
        /// <param name="pRealPage">The p real page.</param>
        /// <returns>System.String.</returns>
        string GetSessionIDFromISB(string pRealPage);
        /// <summary>
        /// Parses the isb path.
        /// </summary>
        /// <param name="pRealPage">The real page.</param>
        /// <param name="pSessionID">The session identifier.</param>
        /// <param name="pType">Type of the sender.</param>
        /// <param name="pFID">The fid.</param>
        /// <param name="pVersion">The version.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool ParseISBPath(string pRealPage, out Guid? pSessionID, out cdeSenderType pType, out long pFID, out string pVersion);
        /// <summary>
        /// Gets the isb path.
        /// </summary>
        /// <param name="PathPrefix">The path prefix.</param>
        /// <param name="pOriginType">Type of the origin.</param>
        /// <param name="pDestinationType">Type of the destination.</param>
        /// <param name="pCounter">The counter.</param>
        /// <param name="pSessionID">The session identifier.</param>
        /// <param name="IsWS">if set to <c>true</c> [is ws].</param>
        /// <returns>System.String.</returns>
        string GetISBPath(string PathPrefix, cdeSenderType pOriginType, cdeSenderType pDestinationType, long pCounter, Guid pSessionID, bool IsWS);

        /// <summary>
        /// Sets the pro se scope.
        /// </summary>
        /// <param name="pSScope">The scrambled scope.</param>
        void SetProSeScope(string pSScope);

        // Old Statics - security uncritical
        /// <summary>
        /// Generates the new scope identifier.
        /// </summary>
        /// <returns>System.String.</returns>
        string GenerateNewScopeID();
        /// <summary>
        /// Gets the token from scope identifier.
        /// </summary>
        /// <returns>System.String.</returns>
        string GetTokenFromScopeID();
        /// <summary>
        /// Gets the token from scrambled scope identifier.
        /// </summary>
        /// <param name="SScopeID">The scrambled scope identifier.</param>
        /// <returns>System.String.</returns>
        string GetTokenFromScrambledScopeID(string SScopeID);
        /// <summary>
        /// Gets the token from easy scope identifier.
        /// </summary>
        /// <param name="pEasyID">The easy identifier.</param>
        /// <returns>System.String.</returns>
        string GetTokenFromEasyScopeID(string pEasyID);
        /// <summary>
        /// Gets the scrambled scope identifier from easy identifier.
        /// </summary>
        /// <param name="pEasyScope">The p easy scope.</param>
        /// <param name="bNoLogging">if set to <c>true</c> [b no logging].</param>
        /// <param name="bUseEasyScope16">if set to <c>true</c> [b use easy scope16].</param>
        /// <returns>System.String.</returns>
        string GetScrambledScopeIDFromEasyID(string pEasyScope, bool bNoLogging = false, bool bUseEasyScope16 = false);
        /// <summary>
        /// Gets the scope hash.
        /// </summary>
        /// <param name="pTopic">The p topic.</param>
        /// <param name="IsSScope">if set to <c>true</c> [is s scope].</param>
        /// <returns>System.String.</returns>
        string GetScopeHash(string pTopic, bool IsSScope = false);
        /// <summary>
        /// Gets the sender type from device identifier.
        /// </summary>
        /// <param name="pNodeID">The node identifier.</param>
        /// <returns>cdeSenderType.</returns>
        cdeSenderType GetSenderTypeFromDeviceID(Guid pNodeID);
        /// <summary>
        /// Generates the code.
        /// </summary>
        /// <param name="pDigits">The digits.</param>
        /// <returns>System.String.</returns>
        string GenerateCode(int pDigits = 8);
        /// <summary>
        /// Determines whether [is valid scope identifier] [the specified p scram scope i d1].
        /// </summary>
        /// <param name="pScramScopeID1">The scrambled scope id.</param>
        /// <returns><c>true</c> if [is valid scope identifier] [the specified p scram scope i d1]; otherwise, <c>false</c>.</returns>
        bool IsValidScopeID(string pScramScopeID1);
        /// <summary>
        /// Determines whether [is valid scope identifier] [the specified p scram scope i d1].
        /// </summary>
        /// <param name="pScramScopeID1">The scrambled scope id1.</param>
        /// <param name="pScramScopeID2">The scrambled scope id2.</param>
        /// <returns><c>true</c> if [is valid scope identifier] [the specified p scram scope i d1]; otherwise, <c>false</c>.</returns>
        bool IsValidScopeID(string pScramScopeID1, string pScramScopeID2);
        /// <summary>
        /// Generates the new application device identifier.
        /// </summary>
        /// <param name="tS">The t s.</param>
        /// <returns>Guid.</returns>
        Guid GenerateNewAppDeviceID(cdeSenderType tS);

        /// <summary>
        /// Calculates the register code.
        /// </summary>
        /// <param name="pGuid">The unique identifier.</param>
        /// <returns>System.String.</returns>
        string CalculateRegisterCode(Guid pGuid);
    }
}
