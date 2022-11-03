// SPDX-FileCopyrightText: Copyright (c) 2009-2020 TRUMPF Laser GmbH, authors: C-Labs
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.ThingService;
using System;
using System.Globalization;
using System.Threading;

namespace nsCDEngine.Engines
{

    /// <summary>
    /// This class will be used to manage the Host on this node - in V3.2 only the Thing is available but does not do anything
    /// In V4 we will add Plugin Management, TheServiceHostInfo and other NodeHost related functions, properties and communication here
    /// </summary>
    internal class TheNodeHost : TheThingBase
    {
        /// <summary>
        /// Interval at which TheNodeHost will gather KPI totals from TheCDEKPIs
        /// </summary>
        public int KPIHarvestInterval
        {
            get { return (int)TheThing.MemberGetSafePropertyNumber(MyBaseThing); }
            set { TheThing.MemberSetSafePropertyNumber(MyBaseThing, value); }
        }
        public TheNodeHost()
        {
        }

        public TheNodeHost(TheThing tBaseThing)
        {
            if (tBaseThing != null)
                MyBaseThing = tBaseThing;
            else
                MyBaseThing = new TheThing();

            // CODE REVIEW: This normally happens in InitEngineAssets: should we adhere to this pattern even for this "special" host?
            MyBaseThing.ID = TheBaseAssets.MyAppInfo.cdeMID.ToString();
            MyBaseThing.DeviceType = eKnownDeviceTypes.ApplicationHost;
            MyBaseThing.FriendlyName = TheBaseAssets.MyAppInfo.ServiceName;
            MyBaseThing.EngineName = string.Format("{0} on {1} V{2}", TheBaseAssets.MyAppInfo.ServiceName, TheBaseAssets.MyAppInfo.Platform, TheBaseAssets.MyAppInfo.CurrentVersion);
            MyBaseThing.MyBaseEngine = new TheBaseEngine();
            MyBaseThing.MyBaseEngine.SetEngineID(new Guid("af7b3917-3030-46ff-8c88-260554e1aa80"));
            MyBaseThing.MyBaseEngine.SetEngineName(MyBaseThing.EngineName);
            MyBaseThing.MyBaseEngine.SetVersion(TheBaseAssets.MyAppInfo.CurrentVersion);

            MyBaseThing.SetIThingObject(this);
        }

        public override bool Init()
        {
            if (mIsInitCalled) return false;

            mIsInitCalled = true;
            MyBaseThing.StatusLevel = 1;

            MyBaseThing.Version = TheBaseAssets.MyAppInfo.CurrentVersion.ToString(CultureInfo.InvariantCulture);
            MyBaseThing.Capabilities = TheBaseAssets.MyAppInfo.Capabilities;
            MyBaseThing.Address = TheBaseAssets.MyServiceHostInfo.GetPrimaryStationURL(false);
            TheThing.SetSafePropertyString(MyBaseThing, "Description", TheBaseAssets.MyAppInfo.LongDescription);
            mIsInitialized = true;

            TheThing.SetSafePropertyBool(MyBaseThing, "EnableKPIs", TheCommonUtils.CBool(TheBaseAssets.MySettings.GetSetting("EnableKPIs")));

            if (TheBaseAssets.MyServiceHostInfo.EnableTaskKPIs)
            {
                var taskKpiThread = new Thread(() =>
                {
                    TheSystemMessageLog.ToCo($"Tasks {DateTime.Now}: NodeHost starting Task KPI thread");
                    do
                    {
                        Thread.Sleep(1000); // Keeping it simple here, to minimize interference on task scheduler/thread scheduler etc. (Assumption: not used on production systems) // TheCommonUtils.SleepOneEye(1000, 1000)
                    var kpis = TheCommonUtils.GetTaskKpis(null);
                        TheSystemMessageLog.ToCo($"Tasks {DateTime.Now}: {TheCommonUtils.SerializeObjectToJSONString(kpis)}");
                    } while (TheBaseAssets.MasterSwitch && TheBaseAssets.MyServiceHostInfo.EnableTaskKPIs);
                    TheSystemMessageLog.ToCo($"Tasks {DateTime.Now}: NodeHost ending Task KPI thread");
                });
                taskKpiThread.Start();
            }
            KPIHarvestInterval = TheCommonUtils.CInt(TheBaseAssets.MySettings.GetAppSetting("KPIHarvestIntervalInSeconds","5",false,true));
            if (KPIHarvestInterval>0)
                TheQueuedSenderRegistry.RegisterHealthTimer(sinkCyclic);
            return true;
        }

        private void sinkCyclic(long timer)
        {
            if (KPIHarvestInterval == 0) return;
            if (timer % KPIHarvestInterval != 0) return;
            if (TheThing.GetSafePropertyBool(MyBaseThing, "EnableKPIs"))
            {
                TheCDEKPIs.ToThingProperties(MyBaseThing, true);
            }
        }
    }
}
