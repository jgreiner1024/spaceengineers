using System;
using System.Text;
using System.Collections.Generic;

using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

using SpaceEngineers.Game.ModAPI;

using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;


namespace xirathonxbox.spaceengineers.mods
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class RealisticImprovedSolarPanel : MySessionComponentBase
    {
        const float densityMultiplier = 0.6f; //400 * 0.4 = 160; 

        //track the solar panel data so when we change it, we don't trigger an infinite loop
        private Dictionary<long, RealisticSolarPanelData> solarPanelData = new Dictionary<long, RealisticSolarPanelData>();

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
                    if (solarPanelData.ContainsKey(solarPanel.EntityId) == false)
                    {
                        solarPanelData.Add(solarPanel.EntityId, new RealisticSolarPanelData() { 
                            EntityId = solarPanel.EntityId,
                            GridEntityId = solarPanel.CubeGrid.EntityId,
                            CurrentAirDensity = null, 
                            OriginalMaxOutput = 0f, 
                            CurrentMaxOutput = 0f, 
                            IsStatic = solarPanel.CubeGrid.IsStatic 
                        });
                    }

                    solarPanel.SourceComp.MaxOutputChanged += SolarPanel_MaxOutputChanged;
                    solarPanel.CubeGrid.OnIsStaticChanged += SolarPanelGrid_OnIsStaticChanged;
                }
                else
                {
                    //shouldn't happen, should always exist
                    if (solarPanelData.ContainsKey(solarPanel.EntityId) == true)
                    {
                        solarPanelData.Remove(solarPanel.EntityId);
                    }
                    
                    solarPanel.SourceComp.MaxOutputChanged -= SolarPanel_MaxOutputChanged;
                    solarPanel.CubeGrid.OnIsStaticChanged -= SolarPanelGrid_OnIsStaticChanged;
                }

                if (entity is IMyTerminalBlock)
                {
                    IMyTerminalBlock solarPanelTerminal = entity as IMyTerminalBlock;
                    if (solarPanelTerminal == null)
                        return;

                    if (create == true)
                    {
                        solarPanelTerminal.AppendingCustomInfo += SolarPanelTerminal_AppendingCustomInfo;
                    }
                    else
                    {
                        solarPanelTerminal.AppendingCustomInfo -= SolarPanelTerminal_AppendingCustomInfo;
                    }
                }
            }


        }

        private void SolarPanelGrid_OnIsStaticChanged(IMyCubeGrid grid, bool isStatic)
        {
            //find all solar panels that belong to this grid
            foreach(var data in solarPanelData.Values)
            {
                if(data.GridEntityId == grid.EntityId)
                {
                    data.IsStatic = isStatic;
                }
            }
        }

        private void UpdatePower(IMySolarPanel solarPanel, RealisticSolarPanelData data = null)
        {
            if (solarPanel == null)
                return;

            if(data == null)
            {
                if (solarPanelData.ContainsKey(solarPanel.EntityId) == false)
                    return;

                data = solarPanelData[solarPanel.EntityId];
            }

            float airDensity = data.CurrentAirDensity != null ? data.CurrentAirDensity.Value : 1f;
            float modifier = 1f - (densityMultiplier * airDensity);
            if (modifier < 0f)
                modifier = 0f;

            float currentOutput = data.OriginalMaxOutput * modifier;

            //only set it if our calculated max output changed
            if(currentOutput != data.CurrentMaxOutput)
            {
                data.CurrentMaxOutput = currentOutput;
                solarPanel.SourceComp.SetMaxOutput(data.CurrentMaxOutput);
            }
        }


        private void SolarPanelTerminal_AppendingCustomInfo(IMyTerminalBlock terminalBlock, StringBuilder terminalStringBuilder)
        {
            float airDensity = 0f;
            if (solarPanelData.ContainsKey(terminalBlock.EntityId) == true)
            {
                var data = solarPanelData[terminalBlock.EntityId];
                airDensity = data.CurrentAirDensity != null ? data.CurrentAirDensity.Value : 1f;
            }

            terminalStringBuilder.AppendLine($"Air Density: {Math.Floor(100f * airDensity)}%");
        }

        private void SolarPanel_MaxOutputChanged(MyDefinitionId changedResourceId, float oldOutput, MyResourceSourceComponent source)
        {
            //is this possible?
            if (source?.Entity == null)
                return;

            //shouldn't happen, should always exist from the OnEntityCreate
            if (solarPanelData.ContainsKey(source.Entity.EntityId) == false)
                return;

            var data = solarPanelData[source.Entity.EntityId];

            //if the max output matches our current max output, or our original max output don't do anything
            if (data.CurrentMaxOutput == source.MaxOutput || data.OriginalMaxOutput == source.MaxOutput)
                return;

            IMyTerminalBlock terminalEntity = source.Entity as IMyTerminalBlock;
            if (terminalEntity == null)
                return;

            //set our new original max output then recalculate            
            solarPanelData[source.Entity.EntityId].OriginalMaxOutput = source.MaxOutput;


            //did we change grids?
            if (terminalEntity.CubeGrid.EntityId != data.GridEntityId)
            {
                //check if we're static again
                data.IsStatic = terminalEntity.CubeGrid.IsStatic;

                //force a re-calculation of air density
                data.CurrentAirDensity = null;
            }

            if (data.CurrentAirDensity == null || data.IsStatic == false)
            {
                var entityPosition = terminalEntity.GetPosition();
                float airDensity = 0f;
                MyPlanet closestPlanet = MyGamePruningStructure.GetClosestPlanet(entityPosition);
                if (closestPlanet == null || closestPlanet.PositionComp.WorldAABB.Contains(entityPosition) == ContainmentType.Disjoint)
                {
                    airDensity = 0f;
                }
                else
                {
                    airDensity = closestPlanet.GetAirDensity(entityPosition);

                }

                if (data.CurrentAirDensity != airDensity)
                {
                    data.CurrentAirDensity = airDensity;
                    terminalEntity.RefreshCustomInfo();
                }
            }

            //update
            UpdatePower(source.Entity as IMySolarPanel, data);
        }
    }

    public class RealisticSolarPanelData
    {
        public long EntityId;
        public float CurrentMaxOutput;
        public float OriginalMaxOutput;
        public float? CurrentAirDensity;
        public long GridEntityId;
        public bool IsStatic;
    }
}
