// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NUnitTestForHelp
{
    public class TheBaseAppSettings
    {
        public Dictionary<string, string> ArgList = null;
        public TheBaseAppSettings()
        {
            ArgList = new Dictionary<string, string>();
            //if (ArgList != null)
            //{
            //    // Scan command line settings.
            //    for (int i = 0; i < Program.args.Length; i++)
            //    {
            //        string[] tArgs = Program.args[i].Split('=');
            //        if (tArgs.Length == 2)
            //        {
            //            string key = tArgs[0];
            //            ArgList[key] = tArgs[1];
            //        }
            //    }
            //}
        }

        public void AddKey(string strKey, string strValue)
        {
            ArgList[strKey] = strValue;
        }

        public void SetKeyUnlessAlreadySet(string strKey, string str)
        {
            if (!ArgList.ContainsKey(strKey))
            {
                AddKey(strKey, str);
            }
        }

        public void AddKey(string strKey, bool b)
        {
            ArgList[strKey] = b.ToString();
        }

        public void SetKeyUnlessAlreadySet(string strKey, bool b)
        {
            if (!ArgList.ContainsKey(strKey))
            {
                AddKey(strKey, b);
            }
        }

        public void AddKey(string strKey, int i)
        {
            ArgList[strKey] = i.ToString();
        }

        public void SetKeyUnlessAlreadySet(string strKey, int i)
        {
            if (!ArgList.ContainsKey(strKey))
            {
                AddKey(strKey, i);
            }
        }

        public void AddKey(string strKey, eDEBUG_LEVELS e)
        {
            AddKey(strKey, (int)e);
        }

        public void SetKeyUnlessAlreadySet(string strKey, eDEBUG_LEVELS e)
        {
            if (!ArgList.ContainsKey(strKey))
            {
                AddKey(strKey, (int)e);
            }
        }

        public void RemoveKey (string strKey)
        {
            if (ArgList.ContainsKey(strKey))
            {
                ArgList.Remove(strKey);
            }
        }

        public void InitWebPorts (ushort httpPort, ushort wsPort)
        {
            TheBaseAssets.MyServiceHostInfo.MyStationPort = httpPort;
            TheBaseAssets.MyServiceHostInfo.MyStationWSPort = wsPort;
            ArgList["STATIONPORT"] = httpPort.ToString() ;
            ArgList["STATIONWSPORT"] = wsPort.ToString() ;
        }
        public void InitEnvironmentVarSettings(bool bEnableUse, bool bAllowOverride)
        {
            ArgList["AllowEnvironmentVars"] = bEnableUse.ToString();
            ArgList["AllowEnvironmentVarsToOverrideConfig"] = bAllowOverride.ToString();
        }

        public void InitUserManager()
        {
            ArgList["UseUserMapper"] = "True";

            TheScopeManager.SetScopeIDFromEasyID(string.Empty);

            RemoveKey("ScopeUserLevel");
        }

        public string InitScopeManager(string strScopeID, int iScopeUserLevel)
        {
            return InitScopeManager(strScopeID, iScopeUserLevel.ToString());
        }

        public string InitScopeManager(string strScopeID, string strScopeUserLevel)
        {
            if (strScopeID.Length == 0)
                strScopeID = TheScopeManager.GenerateNewScopeID();  // TIP: instead of creating a new random ID every time your host starts, you can put a breakpoint in the next line, record the ID and feed it in the "SetScopeIDFromEasyID". Or even set a fixed ScopeID here. (FOR TESTING ONLY!!)
            TheScopeManager.SetScopeIDFromEasyID(strScopeID);

            ArgList["ScopeUserLevel"] = strScopeUserLevel;
            RemoveKey("UseUserMapper");

            return strScopeID;
        }

        public void InitClientBinPersistence(bool bPersist)
        {
            bool bUseRandomDeviceId = (!bPersist);
            AddKey("UseRandomDeviceID", bUseRandomDeviceId);
        }

        public void DisableCodeSigningValidation (bool bDisable)
        {
            AddKey("DontVerifyTrust", bDisable);
        }

    } // class
} // namespace
