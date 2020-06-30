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
using Sandbox.Game.Gui;
using VRage.Game.Gui;

namespace EnergySignatureTest
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation, int.MaxValue - 1)]
    public class RealisticImprovedSolarPanel : MySessionComponentBase
    {
        //this.RadioBroadcaster = new MyRadioBroadcaster(50f);
        private Dictionary<long, MyHudEntityParams> gridHudParams = new Dictionary<long, MyHudEntityParams>();

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
            var powerEntity = entity as IMyPowerProducer;
            if (powerEntity == null)
                return;

            if (gridHudParams.ContainsKey(powerEntity.CubeGrid.EntityId) == false)
            {
                var hudParams = new MyHudEntityParams(new StringBuilder("test"), powerEntity.OwnerId, MyHudIndicatorFlagsEnum.SHOW_ALL);
                hudParams.Share = MyOwnershipShareModeEnum.All;
                hudParams.Position = powerEntity.CubeGrid.GetPosition();

                MyHud.LocationMarkers.RegisterMarker(powerEntity.CubeGrid.EntityId, hudParams);
            }
            else
            {
                //do more stuff
            }
            //test.Add(powerEntity.CubeGrid.EntityId, 0f);
            //MyDataBroadcaster broadcast = new MyDataBroadcaster();
            //broadcast.

            
                
            
        }

        private void MyEntities_OnEntityRemove(MyEntity entity)
        {
            //todo check power and remove entity
        }

        //protected override void WorldPositionChanged(object source)
        //{
        //    base.WorldPositionChanged(source);
        //    if (this.RadioBroadcaster == null)
        //        return;
        //    this.RadioBroadcaster.MoveBroadcaster();
        //}

        //public void UpdateRadios(bool isTrue)
        //{
        //    if (this.RadioBroadcaster == null || this.RadioReceiver == null)
        //        return;
        //    this.RadioBroadcaster.WantsToBeEnabled = isTrue;
        //    this.RadioReceiver.Enabled = isTrue & this.Enabled;
        //}
    }
}
