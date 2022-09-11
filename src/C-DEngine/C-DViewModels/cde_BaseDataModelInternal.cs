// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.NMIService;
using nsCDEngine.Engines.StorageService;
using nsCDEngine.Security;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace nsCDEngine.ViewModels
{
    internal class TheReceivedParts : TheDataBase
    {
        public cdeConcurrentDictionary<int, byte[]> MyParts { get; set; }
    }

    internal class TheIPXIBootMapper : TheDataBase
    {
        public string RemoteAddress { get; set; }
        public string StationName { get; set; }
        public string StationDescription { get; set; }
        public string iSCSITarget { get; set; }
        public string iSCSIIQN { get; set; }
        public string CustomIPXIScript { get; set; }
    }

    internal class TheCoreQueueContent : TheDataBase
    {
        public int QueueIdx { get; set; }
        public string Topic { get; set; }
        public TSM OrgMessage { get; set; }
        public int HashID { get; set; }
        public int SubMsgCnt { get; set; }

        public bool IsChunked { get; set; }
        public DateTimeOffset EntryTime { get; set; }

        public long sn { get; set; }

        public bool IsLocked { get; set; }
        public bool IsSent { get; set; }
    }

    internal class TheThreadInfo : TheDataBase
    {
        public string Name { get; set; }
        public int ID { get; set; }
        public string Priority { get; set; }
        public string State { get; set; }
        public string WaitReason { get; set; }
        public double UserTimeInMs { get; set; }
        public double CoreTimeInMs { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public string StackFrame { get; set; }
        public bool IsBackground { get; set; }
        public bool IsPooled { get; set; }
    }

    internal class TheModuleInfo : TheDataBase
    {
        public string Name { get; set; }
        public int MemorySize { get; set; }
    }

    internal class TheChartNameValue
    {
        public string Name { get; set; }
        public double Value { get; set; }

        public TheChartNameValue() { }
        public TheChartNameValue(string pName, double pVal)
        {
            Name = pName;
            Value = pVal;
        }
    }

    internal class TheMeshPicker : TheDataBase
    {
        public string MeshHash { get; set; }
        public List<string> NodeNames { get; set; }
        internal Guid UserID { get; set; }
        public string HomeNode { get; set; }
    }

	internal class TheUserDetailsI : TheMetaDataBase
	{
		public string Name { get; set; }
		public string LoginGesture { get; set; }
		public string UserName { get; set; }
		public string ThemeName { get; set; }
		public string PrimaryRole { get; set; }
		// public List<Guid> Roles { get; set; } // CODE REVIEW: Does thist class also need the new fields?
		public string EMail { get; set; }
		public string HomeScreen { get; set; }
		public Guid PrimaryDeviceID { get; set; }
		public string ApplicationName { get; set; }
		public int AccessMask { get; set; }
		// public List<string> Permissions { get; set; } // CODE REVIEW: Does thist class also need the new fields?
		public string NodeScope { get; set; }
        public string PinHash { get; set; }
        public string TeTo { get; set; }
        public string TempPin { get; set; }

        //We should move that in a sub class...
        public bool ShowClassic { get; set; }
        public bool ShowToolTipsInTable { get; set; }
        public bool SpeakToasts { get; set; }
        /// <summary>
        /// New in 4.1: Required for Mesh Selection in Cloud
        /// </summary>
        public string HomeNodeName { get; set; }
        public Guid HomeNode { get; set; }
		/// <summary>
		/// Language ID of User
		/// </summary>
		public int LCID { get; set; }
		public bool IsReadOnly { get; set; }

		public string SecToken { get; set; }

		public string Password { get; set; }
		public string AssignedEasyScope { get; set; }
		/// <summary>
		/// Creates a new TheUserDetails
		/// </summary>
		public TheUserDetailsI()
		{
			HomeNode = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
			NodeScope = HomeNode.ToString(); 
		}

		internal TheUserDetails CloneToUser(Guid pUserID)
		{
			TheUserDetails tMinUser = TheUserManager.GetUserByID(pUserID);
			if (tMinUser == null) return null;

			Guid pSessionID = tMinUser.CurrentSessionID;

            TheUserDetails tU = new ()
            {
                ApplicationName = ApplicationName,
                AssignedEasyScope = AssignedEasyScope,  //OK
                cdeAVA = cdeAVA,
                cdeCTIM = cdeCTIM,
                cdeMID = cdeMID,
                cdePRI = cdePRI,
                cdeO = cdeO,
                EMail = EMail,
                Name = Name,
                AccessMask = -1,
                HomeScreen = HomeScreen,
                IsReadOnly = IsReadOnly,
                LoginGesture = LoginGesture,
                HomeNode = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID,
                PrimaryDeviceID = PrimaryDeviceID,
                PrimaryRole = PrimaryRole,
                ThemeName = ThemeName,
                UserName = UserName,
                TeTo = TeTo,
				LCID = LCID,
                ShowClassic=ShowClassic,
                ShowToolTipsInTable=ShowToolTipsInTable,
                SpeakToasts=SpeakToasts,
                HomeNodeName=HomeNodeName
			};
			if (string.IsNullOrEmpty(NodeScope) || NodeScope.ToUpper() == "THIS")
				NodeScope = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID.ToString();
			tU.NodeScope = NodeScope;
            if (string.IsNullOrEmpty(HomeNodeName))
                HomeNodeName = TheBaseAssets.MyServiceHostInfo.MyStationName;

			if (!string.IsNullOrEmpty(SecToken))
			{
				string[] tSec = TheCommonUtils.cdeSplit(TheCommonUtils.cdeRSADecrypt(pSessionID, SecToken), ";:;", false, false);
				if ("CDE!" == tSec[0])
				{
					for (int i = 1; i < tSec.Length; i++)
					{
						int tPos = tSec[i].IndexOf('=');
						if (tPos < 0) continue;
						int tFldNo = TheCommonUtils.CInt(tSec[i].Substring(0, tPos));
						string tPara = tSec[i].Substring(tPos + 1);
						if (TheCommonUtils.IsNullOrWhiteSpace(tPara) || tPara == "null") continue;
						switch (tFldNo)
						{
                            case 40:
								tU.EMail = tPara;
								break;
							case 20: //Name of User
								tU.Name = tPara;
								break;
							case 50: //Password
								tU.Password = TheBaseAssets.MySecrets.CreatePasswordHash(tPara);    //PW-SECURITY-REVIEW: Create Hash Here
								break;
                            case 56: //Pin
                                if (string.IsNullOrEmpty(tU.TeTo))
                                    tU.TeTo= TheBaseAssets.MyScopeManager.GenerateCode(8);
                                tU.PinHash = TheBaseAssets.MySecrets.CreatePasswordHash($"{tU.TeTo.ToUpper()}@@{tPara}");
                                break;
                            case 120: //HomeScreen
								if (tU.PrimaryRole != "NMIADMIN") //not allowed to set a HomeScreen for NMIAdmin
									tU.HomeScreen = tPara;
								break;
							case 110: //Language ID
								tU.LCID = TheCommonUtils.CInt(tPara);
								break;
							case 6: //Node Scope
								tU.NodeScope = tPara;
								break;
							case 70: //AccessMask
								tU.AccessMask = TheCommonUtils.CInt(tPara);
								break;
						}
					}
				}
				else
				{
                    //Should not come here
				}
			}
			else
			{
				tU.Password = TheBaseAssets.MySecrets.CreatePasswordHash(Password); //PW-SECURITY-REVIEW: should contain hash
                tU.AccessMask = AccessMask;
			}
			tU.SecToken = TheCommonUtils.cdeEncrypt(tU.Password + ";:;" + tU.AssignedEasyScope + ";:;" + tU.AccessMask, TheBaseAssets.MySecrets.GetAI());  //AES OK ; PW Should be Hash
			return tU;
		}
	}

    internal class TheUserPreferences
    {
        public bool ShowClassic { get; set; }
        public string ScreenParts { get; set; }
        public string ThemeName { get; set; }
        public int LCID { get; set; }

        public bool ShowToolTipsInTable { get; set; }

        public bool SpeakToasts { get; set; }

        public string Transforms { get; set; }

        public string CurrentUserName { get; set; }

        public bool HideHeader { get; set; }

        public string PortalScreen { get; set; }
        public string StartScreen { get; set; }
    }

    internal class TheUserCreator
	{
		public string UID { get; set; }

		public string PWD { get; set; } 
		public string Name { get; set; }
		public string ThemeName { get; set; }
		public string Role { get; set; }
		public string EMail { get; set; }
		public string HS { get; set; }
		public string ApplicationName { get; set; }
		/// <summary>
		/// Language ID of User
		/// </summary>
		public int LCID { get; set; }
		public int ACL { get; set; }

		public bool RO { get; set; }

		public bool LNO { get; set; }
	}

    internal class TheRefreshToken: TheDataBase
    {
        //possibly additional fields later
    }

	internal class TheUserDetails : TheMetaDataBase
	{
		public string Name { get; set; }
		public string LoginGesture { get; set; }
		public string UserName { get; set; }
		public string ThemeName { get; set; }
		public string PrimaryRole { get; set; }
		public List<Guid> Roles { get; set; }
		public string EMail { get; set; }
		public string HomeScreen { get; set; }
		public Guid PrimaryDeviceID { get; set; }
		public string ApplicationName { get; set; }

        //We should move that in a sub class...
        public bool ShowClassic { get; set; }
        public bool ShowToolTipsInTable { get; set; }
        public bool SpeakToasts { get; set; }
        /// <summary>
        /// New in 4.1: Required for Mesh Selection in Cloud
        /// </summary>
        public string HomeNodeName { get; set; }
		public string NodeScope { get; set; }
        public bool IsReadOnly { get; set; }
        public string PinHash { get; set; }
        public string TeTo { get; set; }

		/// <summary>
		/// Language ID of User
		/// </summary>
		public int LCID { get; set; }
		public bool IsTempUser { get; set; }
		public string SecToken { get; set; }
        public string TempPin { get; set; }

		internal int AccessMask { get; set; }
		public List<string> Permissions { get; set; } // CODE REVIEW: Do these need to be hidden/encrypted like the AccessMask? What UX do we provide?
		internal string Password { get; set; }
        private TheMirrorCache<TheRefreshToken> _refreshTokens;
        internal TheMirrorCache<TheRefreshToken> RefreshTokens
        {
            set { _refreshTokens = value; }
            get
            {
                _refreshTokens ??= new TheMirrorCache<TheRefreshToken>(5);
                return _refreshTokens;
            }
        }
        /// <summary>
        /// SECURITY-REVIEW: Starting 4.106 - the assigned Easy Scope should contain SScopeID - not EasyScopeID; this is NOT compatbile with existing clouds!
        /// </summary>
		internal string AssignedEasyScope { get; set; }

		public Guid HomeNode { get; set; }

		internal Guid CurrentSessionID;
		/// <summary>
		/// Creates a new TheUserDetails
		/// </summary>
		public TheUserDetails()
		{
			HomeNode = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
			NodeScope = HomeNode.ToString(); 
			IsTempUser = false;
		}

		/// <summary>
		/// Creates a new User with some parameter
		/// </summary>
		/// <param name="pUID">UID Of the User</param>
		/// <param name="pPassword">Password for the User</param>
		/// <param name="pRole">Primary Role of User</param>
		/// <param name="pEmail">Email of User</param>
		/// <param name="pPrimDeviceGuid">Primary DeviceID of User. If Empty, the LocalHost DeviceID will be used</param>
		public TheUserDetails(string pUID, string pPassword, string pRole, string pEmail, Guid pPrimDeviceGuid)
		{
			UserName = pUID;
			Password = TheBaseAssets.MySecrets.CreatePasswordHash(pPassword);   //PW-SECURITY-REVIEW: Hash Here
            PrimaryRole = pRole;
			EMail = pEmail;
			ApplicationName = TheBaseAssets.MyServiceHostInfo.ApplicationName;
			PrimaryDeviceID = pPrimDeviceGuid == Guid.Empty ? TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID : pPrimDeviceGuid;
			HomeNode = TheBaseAssets.MyServiceHostInfo.MyDeviceInfo.DeviceID;
            HomeNodeName = TheBaseAssets.MyServiceHostInfo.MyStationName;
			NodeScope = HomeNode.ToString(); 
            IsTempUser = false;
			LCID = 0;
		}

        internal string GetUserPrefString()
        {
            TheUserPreferences tPr = new ()
            {
                ShowClassic = ShowClassic,
                LCID = LCID,
                ThemeName = ThemeName,
                SpeakToasts = SpeakToasts,
                ShowToolTipsInTable = ShowToolTipsInTable,
                Transforms = TheNMIEngine.GetTransforms(this),
                CurrentUserName = Name,
                StartScreen = !string.IsNullOrEmpty(HomeScreen) ? HomeScreen.Split(';')[0] : null,
                PortalScreen = !string.IsNullOrEmpty(HomeScreen) && HomeScreen.Split(';').Length > 1 ? HomeScreen.Split(';')[1] : null
            };
            return TheCommonUtils.SerializeObjectToJSONString(tPr);
        }



        /// <summary>
        /// Returns True if this user is logged on to the current node.
        /// </summary>
        /// <returns></returns>
        public bool IsOnCurrentNode()
		{
			return TheCommonUtils.IsLocalhost(HomeNode);
		}

		/// <summary>
		/// Returns a friendly description of the User
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
#if JC_COMMDEBUG
			return string.Format("ID:{0} UN:{1} EM:{2} ACL:{3} PW:{4} SID:{5}",cdeMID,UserName, EMail, AccessMask, Password, AssignedEasyScope);
#else
			return $"ID:{cdeMID} N:{HomeNode} NS:{NodeScope} UN:{UserName} EM:{EMail} ACL:{AccessMask} LCID:{LCID}".ToString(CultureInfo.InvariantCulture);
#endif

		}
	}

	internal class TheUserLog : TheDataBase
	{
		public string UserInfo { get; set; }
		public string UserLevel { get; set; }
		public string UserAction { get; set; }
		public string CurrentScreen { get; set; }
		public Guid ImageID { get; set; }

		public static TheUserLog GetDefaults()
		{
			TheUserLog tULG = new ()
			{
				UserAction = "Unknown",
				UserInfo = "Unknown",
				UserLevel = "Unknown",
				CurrentScreen = "Unknown"
			};
			return tULG;
		}
	}

	internal class TheSubscriptionInfo : TheDataBase
	{
		public bool UnsubAfterPublish { get; set; }
		public bool ToServiceOnly { get; set; }
		public int ExpiresIn { get; set; }

        internal string RScopeID;
        internal long Hits;
        internal string Topic;
	}

    internal class TheSentRegistryItem : TheDataBase
	{
		public double FID;
        public DateTimeOffset SentTime;
        public bool IsOutgoing;
		public Guid SessionID;
		public Guid ORG;
		public int TopicHash;
		public int PLSHash;
		public string Engine;

		public override string ToString()
		{
			return $"ORG:{TheCommonUtils.GetDeviceIDML(ORG)},TH:{TopicHash}, FID:{FID}, OUT:{IsOutgoing}, TIM:{TheCommonUtils.GetDateTimeString(SentTime,-1)}, ENG:{Engine}";
		}
    }

    internal class TheSentRegistryItemHS
    {
        public int cdeEXP;
        public double FID;
        public DateTimeOffset SentTime;
        public bool IsOutgoing;
        public Guid SessionID;
        public Guid ORG;
        public int TopicHash;
        public int PLSHash;
        public string Engine;

        public override string ToString()
        {
            return $"ORG:{TheCommonUtils.GetDeviceIDML(ORG)},TH:{TopicHash}, FID:{FID}, OUT:{IsOutgoing}, TIM:{TheCommonUtils.GetDateTimeString(SentTime, -1)}, ENG:{Engine}";
        }
    }

    #region Retired - remove before OSS Release
    internal class TheTestBase : TheDataBase
    {
        public byte[] TestByteArrary { get; set; }
        public Char[] TestCharArrary { get; set; }

        public string TestString { get; set; }
        public bool TestBool { get; set; }
        public float TestFloat { get; set; }
        public double TestDouble { get; set; }
        public int TestInt { get; set; }
        public long TestLong { get; set; }
        public byte TestByte { get; set; }
        public Guid TestGuid { get; set; }
        public DateTimeOffset TestDateTime { get; set; }
    }

    internal class TheAppParameter : TheDataBase
    {
        public bool RequiresConfiguration { get; set; }
        /// <summary>
        /// Starting 4.106 this is no longer the EasyScope but a ScrambledScope. There is no storage of the easy scopeID in the CDE anymore
        /// </summary>
        public string EasyScope { get; set; }
        public string FederationID { get; set; }
        public string CloudServiceRoute { get; set; }

        public string MyStationName { get; set; }
        public string MyStationURL { get; set; }
        public ushort MyStationPort { get; set; }
        public ushort MyStationWSPort { get; set; }
        public bool UseUserMapper { get; set; }
        /// <summary>
        /// If set to true, many administrative operations can be performed from any FR in the mesh, not just from the FR that is to be managed ("FirstNode").
        /// </summary>
        public bool AllowRemoteAdministration { get; set; }
        public bool AllowRemoteThingCreation { get; set; }

        /// <summary>
        /// if true, users cannot login via the NMI in the Cloud
        /// </summary>
        public bool BlockCloudNMI { get; set; }
        public bool UseUPnP { get; set; }
        public bool IsCloudDisabled { get; set; }
        public string SQLCredentialToken { get; set; }

        public bool EnforceTLS { get; set; }
        public string TrustedNodesToken { get; set; }

        public string ProxyToken { get; set; }
        public TheDeviceRegistryData MyDeviceInfo { get; set; }

        public TheUserDetails MyUserInfo { get; set; }

        public cdeConcurrentDictionary<string, string> CustomParameter { get; set; }
    }
    #endregion
}
