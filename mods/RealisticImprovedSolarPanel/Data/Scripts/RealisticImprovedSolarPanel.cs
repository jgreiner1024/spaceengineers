using System;
using System.Text;
using System.Collections.Generic;

using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

using SpaceEngineers.Game.ModAPI;

using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;

namespace xirathonxbox.spaceengineers.mods
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class RealisticImprovedSolarPanel : MySessionComponentBase
    {
        const float densityMultiplier = 0.7f; //400 * 0.3 = 120; 

        //track the current max output so when we change it, we don't trigger an infinite loop
        private Dictionary<long, float> currentMaxOutput = new Dictionary<long, float>();
        private Dictionary<long, float> currentAirDensity = new Dictionary<long, float>();

        public override void LoadData()
        {
            MyEntities.OnEntityCreate += MyEntities_OnEntityCreate;
            MyEntities.OnEntityRemove += MyEntities_OnEntityRemove;
        }

        protected override void UnloadData()
        {
            MyEntities.OnEntityCreate -= MyEntities_OnEntityCreate;
            MyEntities.OnEntityRemove -= MyEntities_OnEntityRemove;
        }

        private void MyEntities_OnEntityCreate(MyEntity entity)
        {
            MyEntities_OnEntity(entity, true);
        }

        private void MyEntities_OnEntityRemove(MyEntity entity)
        {
            MyEntities_OnEntity(entity, false);
        }

        private void MyEntities_OnEntity(MyEntity entity, bool create)
        {
            if (entity == null)
                return;

            if (entity is IMySolarPanel)
            {
                IMySolarPanel solarPanel = entity as IMySolarPanel;

                if (solarPanel == null || solarPanel.SourceComp == null)
                    return;

                if (create == true)
                {
                    //shouldn't happen, should always be new?
                    if (currentMaxOutput.ContainsKey(solarPanel.EntityId) == false)
                    {
                        currentMaxOutput.Add(solarPanel.EntityId, -1f);
                    }
                    solarPanel.SourceComp.MaxOutputChanged += SolarPanel_MaxOutputChanged;
                }
                else
                {
                    //shouldn't happen, should always exist
                    if (currentMaxOutput.ContainsKey(solarPanel.EntityId) == true)
                    {
                        currentMaxOutput.Remove(solarPanel.EntityId);
                    }
                    solarPanel.SourceComp.MaxOutputChanged -= SolarPanel_MaxOutputChanged;
                }

                if (entity is IMyTerminalBlock)
                {
                    IMyTerminalBlock solarPanelTerminal = entity as IMyTerminalBlock;
                    if (solarPanelTerminal == null)
                        return;

                    if (create == true)
                    {
                        if (currentAirDensity.ContainsKey(solarPanelTerminal.EntityId) == false)
                        {
                            currentAirDensity.Add(solarPanelTerminal.EntityId, 1f);
                        }
                        solarPanelTerminal.AppendingCustomInfo += SolarPanelTerminal_AppendingCustomInfo;
                    }
                    else
                    {
                        if (currentAirDensity.ContainsKey(solarPanelTerminal.EntityId) == true)
                        {
                            currentAirDensity.Remove(solarPanelTerminal.EntityId);
                        }
                        solarPanelTerminal.AppendingCustomInfo -= SolarPanelTerminal_AppendingCustomInfo;
                    }
                }
            }


        }

        private void SolarPanelTerminal_AppendingCustomInfo(IMyTerminalBlock terminalBlock, StringBuilder terminalStringBuilder)
        {
            float airDensity = 0f;
            if (currentAirDensity.ContainsKey(terminalBlock.EntityId) == true)
            {
                airDensity = currentAirDensity[terminalBlock.EntityId];
            }
            terminalStringBuilder.AppendLine($"Air Density: {Math.Floor(100f * airDensity)}%");
        }

        private void SolarPanel_MaxOutputChanged(MyDefinitionId changedResourceId, float oldOutput, MyResourceSourceComponent source)
        {
            //is this possible?
            if (source.Entity == null)
                return;

            //shouldn't happen, should always exist from the OnEntityCreate
            if (currentMaxOutput.ContainsKey(source.Entity.EntityId) == false)
                return;

            float maxOutput = source.MaxOutput;
            float currentOutput = currentMaxOutput[source.Entity.EntityId];

            //prevent infinite looping
            if (maxOutput == currentOutput)
                return;

            float airDensity = 0f;
            Vector3D entityPosition = source.Entity.GetPosition();
            MyPlanet closestPlanet = MyGamePruningStructure.GetClosestPlanet(entityPosition);
            if (closestPlanet == null || closestPlanet.PositionComp.WorldAABB.Contains(entityPosition) == ContainmentType.Disjoint)
            {
                airDensity = 0f;
            }
            else
            {
                airDensity = closestPlanet.GetAirDensity(entityPosition);
            }

            //shouldn't happen
            if (currentAirDensity.ContainsKey(source.Entity.EntityId) == false)
            {
                currentAirDensity.Add(source.Entity.EntityId, airDensity);
            }
            else
            {
                currentAirDensity[source.Entity.EntityId] = airDensity;
            }

            float modifier = 1f - (densityMultiplier * airDensity);
            if (modifier < 0f)
                modifier = 0f;

            currentOutput = maxOutput * modifier;
            currentMaxOutput[source.Entity.EntityId] = currentOutput;
            source.SetMaxOutputByType(changedResourceId, currentOutput);

            if (source.Entity is IMyTerminalBlock)
            {
                IMyTerminalBlock terminalEntity = (IMyTerminalBlock)source.Entity;
                terminalEntity.RefreshCustomInfo();
            }

        }
    }
}
