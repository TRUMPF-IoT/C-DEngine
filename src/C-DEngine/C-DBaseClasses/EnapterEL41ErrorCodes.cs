// SPDX-FileCopyrightText: Copyright (c) 2009-2023 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace nsCDEngine.BaseClasses
{
    /// <summary>
    /// Enapter EL4.1 Electrolyser Warning, Error and Fatal Error Codes
    /// Data source: https://handbook.enapter.com/electrolyser/el41/#warning-error-and-fatal-error-codes
    /// </summary>
    public static class EnapterEL41ErrorCodes
    {
        /// <summary>
        /// Dictionary of error codes with Code as key and Description as value.
        /// Based on the Warning, Error and Fatal Error Codes table from Enapter EL4.1 handbook.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> ErrorCodeDescriptions = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>
            {
                // TODO: Populate with actual data from https://handbook.enapter.com/electrolyser/el41/
                // 
                // The website https://handbook.enapter.com/electrolyser/el41/#warning-error-and-fatal-error-codes
                // contains a table with "Warning, Error and Fatal Error Codes".
                // 
                // Please add entries in the format:
                // { "CODE", "Description of the error/warning" },
                //
                // Example format (these are placeholders and should be replaced):
                // { "W1", "Warning description from handbook" },
                // { "W2", "Another warning description" },
                // { "E1", "Error description from handbook" },
                // { "F1", "Fatal error description from handbook" },
                
                // Add actual entries from the Enapter EL4.1 handbook table here
            });

        /// <summary>
        /// Gets the description for a given error code.
        /// </summary>
        /// <param name="code">The error code (e.g., "W1", "E5", "F3")</param>
        /// <returns>The description of the error code, or null if the code is not found</returns>
        public static string GetDescription(string code)
        {
            if (string.IsNullOrEmpty(code))
                return null;

            ErrorCodeDescriptions.TryGetValue(code, out string description);
            return description;
        }

        /// <summary>
        /// Checks if a given code exists in the error code dictionary.
        /// </summary>
        /// <param name="code">The error code to check</param>
        /// <returns>True if the code exists, false otherwise</returns>
        public static bool IsValidCode(string code)
        {
            return !string.IsNullOrEmpty(code) && ErrorCodeDescriptions.ContainsKey(code);
        }

        /// <summary>
        /// Gets all available error codes.
        /// </summary>
        /// <returns>Collection of all error code keys</returns>
        public static IEnumerable<string> GetAllCodes()
        {
            return ErrorCodeDescriptions.Keys;
        }
    }
}
