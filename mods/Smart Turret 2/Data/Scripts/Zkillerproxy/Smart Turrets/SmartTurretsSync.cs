﻿using Sandbox.ModAPI;
using VRage.Game.Components;
using static Zkillerproxy.SmartTurretMod.SmartTurretUtilities;
using VRage.Game;
using VRage.ModAPI;
using System;

namespace Zkillerproxy.SmartTurretMod
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class SmartTurretsSyncServer : MySessionComponentBase
    {
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                MyAPIGateway.Multiplayer.RegisterMessageHandler(settingsReportSend, handleSettingsReportSend);
            }
        }

        protected override void UnloadData()
        {
            SmartTurretsProfiler.Instance.LogStats();
            if (MyAPIGateway.Session.IsServer)
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(settingsReportSend, handleSettingsReportSend);
            }
        }

        //Handle staggered turret updates
        public override void UpdateBeforeSimulation()
        {
            Guid profilerId = SmartTurretsProfiler.Instance.Start("SyncUpdateBeforeSimulation");

            int numToUpdate = (int)Math.Ceiling((SmartTurret.targetingWaitingList.Count / 60f));
            
            for (int i = 0; i < numToUpdate; i++)
            {
                IMyEntity entity = SmartTurret.targetingWaitingList[i];

                if (entity != null)
                {
                    SmartTurret actionTurret = getSmartTurret(entity as IMyTerminalBlock);

                    if (actionTurret != null)
                    {
                        actionTurret.needsUpdate = true;
                        SmartTurret.targetingWaitingList.Remove(SmartTurret.targetingWaitingList[i]);
                    }
                    else
                    {
                        SmartTurret.targetingWaitingList.Remove(SmartTurret.targetingWaitingList[i]);
                    }
                }
                else
                {
                    SmartTurret.targetingWaitingList.Remove(SmartTurret.targetingWaitingList[i]);
                }
            }
            SmartTurretsProfiler.Instance.Stop(profilerId);
        }

        private void handleSettingsReportSend(byte[] data)
        {
            Guid profilerId = SmartTurretsProfiler.Instance.Start("handleSettingsReportSend");
            handleSettingsRecieve(data);

            MyAPIGateway.Multiplayer.SendMessageToOthers(settingsReportRecieve, data);
            SmartTurretsProfiler.Instance.Stop(profilerId);
        }
    }

    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class SmartTurretsSyncClient : MySessionComponentBase
    {
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            if (!MyAPIGateway.Session.IsServer)
            {
                MyAPIGateway.Multiplayer.RegisterMessageHandler(settingsReportRecieve, handleSettingsReportRecieve);
            }
        }

        protected override void UnloadData()
        {
            if (!MyAPIGateway.Session.IsServer)
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(settingsReportRecieve, handleSettingsReportRecieve);
            }
        }

        private void handleSettingsReportRecieve(byte[] data)
        {
            Guid profilerId = SmartTurretsProfiler.Instance.Start("handleSettingsReportRecieve");
            handleSettingsRecieve(data);
            SmartTurretsProfiler.Instance.Stop(profilerId);

        }
    }
}
