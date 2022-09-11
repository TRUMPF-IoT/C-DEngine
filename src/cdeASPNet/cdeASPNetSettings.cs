// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Configuration;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.Interfaces;
using nsCDEngine.ISM;
using nsCDEngine.ViewModels;
using System;
using System.Collections.Generic;

namespace cdeASPNetMiddleware
{
    public class TheASPCoreSettings : TheCDESettings
    {
        IConfiguration mConfig;

        public TheASPCoreSettings(IConfiguration pConfig):base()
        {
            mConfig = pConfig;
        }

        public override bool HasSetting(string pSetting, Guid? pOwner = null)
        {
            if (base.HasSetting(pSetting, pOwner)) return true;
            return !string.IsNullOrEmpty(mConfig.GetValue<string>(pSetting));
        }

        public override string GetAppSetting(string pSetting, string alt, bool IsEncrypted, bool IsAltDefault = false, Guid? pOwner = null)
        {
            string tres=mConfig.GetValue<string>(pSetting);
            if (!string.IsNullOrEmpty(tres)) return tres;
            return base.GetAppSetting(pSetting, alt, IsEncrypted, IsAltDefault, pOwner);
        }

        public override string GetSetting(string pSetting, Guid? pOwner = null)
        {
            string tres= mConfig.GetValue<string>(pSetting);
            if (!string.IsNullOrEmpty(tres)) return tres;
            return base.GetSetting(pSetting, pOwner);
        }
    }
}
