// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using nsCDEngine.ViewModels;
using System.Collections.Generic;

namespace nsCDEngine.Communication
{
    /// <summary>
    /// the IHttpInterceptor Interface allows plugins to inject code into the HTTP Processing Path in order to parse special resources.
    /// </summary>
    public interface IHttpInterceptor
    {
        /// <summary>
        /// This function registers a callback function to provide one or more global Javascript files. If this
        /// function is called from inside an isolated plugin, the request is forwarded to the master node.
        /// </summary>
        /// <param name="pUrl">A string identifying the beginning of the URL for the target Javascript files.</param>
        /// <param name="sinkInterceptHttp">A callback function to provide the requested resource.</param>
        void RegisterGlobalScriptInterceptor(string pUrl, Action<TheRequestData> sinkInterceptHttp);
        /// <summary>
        /// This function registers an interceptor callback function, and a target URL path, to be called BEFORE the
        /// C DEngine processes the request further. If this function is called from inside an isolated plugin, the
        /// request is forwarded to the master node.
        /// </summary>
        /// <param name="pUrl">Identifies target URLs are starting with a provided string.</param>
        /// <param name="sinkInterceptHttp">Callback to handle the http request.</param>
        void RegisterHttpInterceptorB4(string pUrl, Action<TheRequestData> sinkInterceptHttp);
        /// <summary>
        /// Unregisters a path from the Interceptor
        /// If this function is called from within an isolated plugin, the request is forwarded to the master node.
        /// </summary>
        /// <param name="pUrl">Unregisters the path from the Interceptor</param>
        void UnregisterHttpInterceptorB4(string pUrl);
        /// <summary>
        /// This Interceptor registers a path that will be parsed AFTER the C-DEngine processes the request further
        /// If this function is called from within an isolated plugin, the request is forwarded to the master node.
        /// </summary>
        /// <param name="pUrl">Part of the Path to be intecepted</param>
        /// <param name="sinkInterceptHttp">Callback that will handle the parsing</param>
        void RegisterHttpInterceptorAfter(string pUrl,Action<TheRequestData> sinkInterceptHttp);
        /// <summary>
        /// Unregisters a path from the Interceptor
        /// If this function is called from within an isolated plugin, the request is forwarded to the master node.
        /// </summary>
        /// <param name="pUrl">Unregisters the path from the Interceptor</param>
        void UnregisterHttpInterceptorAfter(string pUrl);

        /// <summary>
        /// This function allows a plugin to register a callback function that adds HTML to the
        /// web page that is displayed when the user summons the cdeStatus.aspx status page with
        /// either ALL or DIAG specified as a parameter.
        /// </summary>
        /// <param name="sinkGetStat">A callback function to provide the requested status information.</param>
        void RegisterStatusRequest(Action<TheRequestData> sinkGetStat);

        /// <summary>
        /// A helper function to create a header from the provided TheRequestData.
        /// </summary>
        /// <param name="pRequestData">A reference to request data, a parameter to all IHttpInterceptor callback functions.</param>
        /// <returns></returns>
        string CreateHttpHeader(TheRequestData pRequestData);
        /// <summary>
        /// This is the main http processing function of the C-DEngine. You can call this function to inject an Http
        /// request into the C DEngine. Among other uses, it could be used for unit-testing of an http interceptor.
        /// </summary>
        /// <param name="tReq">A valid request data structure. If there are invalid parameter in this parameter, the call fails.</param>
        /// <returns></returns>
        bool cdeProcessPost(TheRequestData tReq);

        /// <summary>
        /// New V4: returns a list of all globally registered Javascript scripts.
        /// </summary>
        /// <returns></returns>
        List<string> GetGlobalScripts();

        /// <summary>
        /// New in V4.301: Validates a client certificate and returns a list of permitted scopeIDs for the connection request
        /// This function is called during the initial ISB Connection
        /// </summary>
        /// <param name="pReq">TheRequestData containing the Client Certificate</param>
        /// <returns>A list of permitted Easy ScopeIDs. Returns an empty list if access is denied or no scopes are configured</returns>
        List<string> GetScopesFromClientCertificate(TheRequestData pReq);

        /// <summary>
        /// New in V4.301: Validates the root of a ClientCertificate if RequiredClientCertRootThumbprints is set in the configuration
        /// </summary>
        /// <param name="pReq">TheRequestData containing a Client Certificate</param>
        /// <returns>empty string if sucess, otherwise contains error during the processing</returns>
        string ValidateCertificateRoot(TheRequestData pReq);
    }
}
