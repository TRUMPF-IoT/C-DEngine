// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

namespace nsCDEngine.Communication
{
    /// <summary>
    /// HTTP Status Code
    /// </summary>
    public enum eHttpStatusCode 
    {
        /// <summary>
        /// 100 Continue for HTTP1.1 telegrams
        /// </summary>
        Continue = 100,
        /// <summary>
        /// 200 OK
        /// </summary>
        OK = 200,

        /// <summary>
        /// 302 for Redirects
        /// </summary>
        PermanentMoved =302,

        /// <summary>
        /// 403 Access Denied
        /// </summary>
        AccessDenied=403,
        /// <summary>
        /// 404 Not Found
        /// </summary>
        NotFound = 404,

        /// <summary>
        /// Telegram cannot be parsed by the serive
        /// </summary>
        NotAcceptable = 406,

        /// <summary>
        /// Something happend on the Server
        /// </summary>
        ServerError = 500,

        /// <summary>
        /// Server is Busy
        /// </summary>
        Busy=503
    }
}
