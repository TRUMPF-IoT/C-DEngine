// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using cdePackager;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;

namespace cdePackagerTask
{
    public class PackagePlugIn : AppDomainIsolatedTask, cdePackager.ILogger
    {
        [Required]
        public string PluginFilePath { get; set; }
        [Required]
        public string OutputPath { get; set; }
        public string StorePath { get; set; }

        public string pPlatform { get; set; }
        public bool Diagnostics { get; set; }
        public override bool Execute()
        {
            var storePath = StorePath;
            if (String.IsNullOrEmpty(storePath))
            {
                storePath = OutputPath;
            }
            bool bCheckForFileOverwrite = Diagnostics || Environment.GetEnvironmentVariable("cdePackagerCheckOverwrite") != null;
            ThePackager.PackagePlugIn(PluginFilePath, OutputPath, storePath,pPlatform, this, Diagnostics, bCheckForFileOverwrite, out var error);
            return string.IsNullOrEmpty(error);
        }

        public void WriteLine(string text)
        {
            //intended
        }
    }
}
