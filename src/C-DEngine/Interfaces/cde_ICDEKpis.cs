// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0
#pragma warning disable 1591

namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// All existing default KPI names in the C-DEngine
    /// </summary>
    public enum eKPINames
    {
        /// <summary>
        /// The q senders
        /// </summary>
        QSenders, QSenderInRegistry, QSReceivedTSM, QSSendErrors,
        /// <summary>
        /// The qs inserted
        /// </summary>
        QSInserted, QSQueued, QSRejected, QSSETPRejected,
        /// <summary>
        /// The qs sent
        /// </summary>
        QSSent, QSLocalProcessed, QSConnects, QSDisconnects,
        /// <summary>
        /// The qs not relayed
        /// </summary>
        QSNotRelayed, QKBSent, QKBReceived, QSCompressedPLS,
        /// <summary>
        /// The total engine errors
        /// </summary>
        TotalEngineErrors, EngineErrors, EventTimeouts, TotalEventTimeouts,
        /// <summary>
        /// The seen before count
        /// </summary>
        SeenBeforeCount, HTCallbacks, CCTSMsRelayed, CCTSMsReceived,
        /// <summary>
        /// The CCTS ms evaluated
        /// </summary>
        CCTSMsEvaluated, UniqueMeshes, BruteDelay, SessionCount,
        /// <summary>
        /// The unsigned plugins
        /// </summary>
        UnsignedPlugins, UnsignedNodes, KPI1, KPI2,
        /// <summary>
        /// The kp i3
        /// </summary>
        KPI3, KPI4, KPI5, KPI6,
        /// <summary>
        /// The kp i7
        /// </summary>
        KPI7, KPI8, KPI9, KPI10,
        /// <summary>
        /// The set ps fired
        /// </summary>
        SetPsFired, WSTestClients, KnownNMINodes, StreamsNotFound,
        /// <summary>
        /// The blobs not found
        /// </summary>
        BlobsNotFound, RejectedClientConnections, IISRejectedClientConnections,
    }

    /// <summary>
    /// Interface ICDEKpis
    /// </summary>
    public interface ICDEKpis
    {
        /// <summary>
        /// Increments the kpi.
        /// </summary>
        /// <param name="pName">Name of the p.</param>
        void IncrementKPI(eKPINames pName);
        /// <summary>
        /// Increments the kpi.
        /// </summary>
        /// <param name="pName">Name of the p.</param>
        /// <param name="dontReset">if set to <c>true</c> [dont reset].</param>
        void IncrementKPI(string pName, bool dontReset = false);
    }
}
