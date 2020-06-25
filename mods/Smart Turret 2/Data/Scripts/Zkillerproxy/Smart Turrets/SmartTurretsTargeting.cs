using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using ParallelTasks;
using Sandbox.Definitions;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;
using static Zkillerproxy.SmartTurretMod.SmartTurretUtilities;
using VRage.Game.Entity;

namespace Zkillerproxy.SmartTurretMod
{
    public static class SmartTurretTargetingUtilities
    {
        //MAIN TARGETING. The main targeting method, only to be run on other threads because this is laggy af.
        public static void validateTargetsThread(WorkData tempData)
        {
            Guid profilerId = SmartTurretsProfiler.Instance.Start("validateTargetsThread");
            try
            {
                TargetingData data = (TargetingData)tempData;
                Vector3D turretLocation = data.turret.Entity.GetPosition();
                Dictionary<IMyEntity, ushort> targetTypeDictionary = getTargetTypeDictionary(data.targets);

                //log("Turret: " + data.turret.Entity.EntityId.ToString() + " 0: " + targetTypeDictionary.Count.ToString() + " : " + getTypes(targetTypeDictionary));
                //Eliminate targets that are not functional.
                for (int i = targetTypeDictionary.Count - 1; i >= 0; i--)
                {
                    ushort curretType = targetTypeDictionary.ElementAt(i).Value;

                    //Ignore non-cube types.
                    if (curretType != 31 && curretType != 32 && curretType != 33)
                    {
                        if (((IMyCubeBlock)targetTypeDictionary.ElementAt(i).Key).IsFunctional == false)
                        {
                            targetTypeDictionary.Remove(targetTypeDictionary.ElementAt(i).Key);
                        }
                    }
                }

                //log("Turret: " + data.turret.Entity.EntityId.ToString() + " 1: " + targetTypeDictionary.Count.ToString() + " : " + getTypes(targetTypeDictionary));
                //Eliminate targets that are disabled in targeting settings.
                for (int i = targetTypeDictionary.Count - 1; i >= 0; i--)
                {
                    ushort CurretType = targetTypeDictionary.ElementAt(i).Value;
                    if (CurretType != 35)
                    {
                        MyTerminalControlListBoxItem typeItem = data.turret.targetTypesListItems.Find((x) => { return (x.UserData as ListBoxItemData).id == CurretType; });
                        if ((typeItem.UserData as ListBoxItemData).enabledState == false)
                        {
                            targetTypeDictionary.Remove(targetTypeDictionary.ElementAt(i).Key);
                        }
                    }
                }

                //log("Turret: " + data.turret.Entity.EntityId.ToString() + " 2: " + targetTypeDictionary.Count.ToString() + " : " + getTypes(targetTypeDictionary));
                //Eliminate targets that are friendly or are neutral should that be set appropriatly.
                for (int i = targetTypeDictionary.Count - 1; i >= 0; i--)
                {
                    ushort curretType = targetTypeDictionary.ElementAt(i).Value;

                    var cubeEntity = (data.turret.Entity as IMyCubeBlock);
                    if (cubeEntity != null)
                    {
                        if (curretType != 31 && curretType != 32 && curretType != 33)
                        {

                            if (curretType != 13)
                            {
                                //Check relation of blocks with ownership
                                IMyCubeBlock cubeBlock = (IMyCubeBlock)targetTypeDictionary.ElementAt(i).Key;
                                MyRelationsBetweenPlayerAndBlock relation = cubeBlock.GetUserRelationToOwner(cubeEntity.OwnerId);

                                if ((relation != MyRelationsBetweenPlayerAndBlock.Enemies && relation != MyRelationsBetweenPlayerAndBlock.Neutral) || ((relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.NoOwnership) && data.turret.targetNeutralsStateDictionary[targetTypeDictionary.ElementAt(i).Value] == false))
                                {
                                    targetTypeDictionary.Remove(targetTypeDictionary.ElementAt(i).Key);
                                }
                            }
                            else
                            {
                                //Check relation of blocks that cannot have ownership based on grid ownership
                                IMyCubeGrid cubeGrid = (targetTypeDictionary.ElementAt(i).Key as IMyCubeBlock).CubeGrid;
                                MyRelationsBetweenPlayerAndBlock relation = MyRelationsBetweenPlayerAndBlock.NoOwnership;

                                if (cubeGrid.BigOwners.Count > 0)
                                {
                                    relation = cubeEntity.GetUserRelationToOwner(cubeGrid.BigOwners[0]);
                                }

                                if ((relation != MyRelationsBetweenPlayerAndBlock.Enemies && relation != MyRelationsBetweenPlayerAndBlock.Neutral) || ((relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.NoOwnership) && data.turret.targetNeutralsStateDictionary[targetTypeDictionary.ElementAt(i).Value] == false))
                                {
                                    targetTypeDictionary.Remove(targetTypeDictionary.ElementAt(i).Key);
                                }
                            }
                        }
                        else if (curretType == 31) //Check relation of players
                        {

                            if (cubeEntity != null)
                            {
                                List<IMyPlayer> Players = new List<IMyPlayer>();
                                MyAPIGateway.Players.GetPlayers(Players, (x) => { return x.Character.EntityId == targetTypeDictionary.ElementAt(i).Key.EntityId; });

                                //IDK why this uses MyRelationsBetweenPlayerAndBlock when MyRelationsBetweenPlayers is a thing... KEEN!
                                MyRelationsBetweenPlayerAndBlock relation = Players.First().GetRelationTo(cubeEntity.OwnerId);
                                if ((relation != MyRelationsBetweenPlayerAndBlock.Enemies && relation != MyRelationsBetweenPlayerAndBlock.Neutral) || ((relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.NoOwnership) && data.turret.targetNeutralsStateDictionary[targetTypeDictionary.ElementAt(i).Value] == false))
                                {
                                    targetTypeDictionary.Remove(targetTypeDictionary.ElementAt(i).Key);
                                }
                            }
                        }
                    }

                    //Ignore non-cube types.
                }

                //log("Turret: " + data.turret.Entity.EntityId.ToString() + " 3: " + targetTypeDictionary.Count.ToString() + " : " + getTypes(targetTypeDictionary));
                //Eliminate targets that are out of range.
                for (int i = targetTypeDictionary.Count - 1; i >= 0; i--)
                {
                    ushort CurretType = targetTypeDictionary.ElementAt(i).Value;

                    if (CurretType != 35)
                    {
                        if (checkTargetOutOfRange(turretLocation, data.turret.rangeStateDictionary[CurretType], targetTypeDictionary.ElementAt(i).Key.GetPosition()))
                        {
                            targetTypeDictionary.Remove(targetTypeDictionary.ElementAt(i).Key);
                        }
                    }
                    else
                    {
                        var terminalEntity = data.turret.Entity as IMyTerminalBlock;
                        if(terminalEntity != null)
                        {
                            if (checkTargetOutOfRange(turretLocation, getTurretMaxRange(terminalEntity), targetTypeDictionary.ElementAt(i).Key.GetPosition()))
                            {
                                targetTypeDictionary.Remove(targetTypeDictionary.ElementAt(i).Key);
                            }
                        }
                        
                    }
                }

                //log("Turret: " + data.turret.Entity.EntityId.ToString() + " 4: " + targetTypeDictionary.Count.ToString() + " : " + getTypes(targetTypeDictionary));
                //Eliminate targets that are on the wrong type of grid.
                for (int i = targetTypeDictionary.Count - 1; i >= 0; i--)
                {
                    ushort curretType = targetTypeDictionary.ElementAt(i).Value;

                    //Ignore non-cube types and decoys.
                    if (curretType != 31 && curretType != 32 && curretType != 33 && curretType != 35)
                    {
                        IMyCubeGrid grid = ((IMyCubeBlock)targetTypeDictionary.ElementAt(i).Key).CubeGrid;
                        bool targetLarge = data.turret.targetLargeGridsStateDictionary[targetTypeDictionary.ElementAt(i).Value];
                        bool targetSmall = data.turret.targetSmallGridsStateDictionary[targetTypeDictionary.ElementAt(i).Value];
                        bool targetStation = data.turret.targetStationsStateDictionary[targetTypeDictionary.ElementAt(i).Value];

                        if ((grid.GridSizeEnum == MyCubeSize.Large && targetLarge == false) || (grid.GridSizeEnum == MyCubeSize.Small && targetSmall == false) || (grid.IsStatic == true && targetStation == false))
                        {
                            targetTypeDictionary.Remove(targetTypeDictionary.ElementAt(i).Key);
                        }
                    }
                }

                //log("Turret: " + data.turret.Entity.EntityId.ToString() + " 5: " + targetTypeDictionary.Count.ToString() + " : " + getTypes(targetTypeDictionary));
                //Eliminate targets that are on grids that are too small.
                for (int i = targetTypeDictionary.Count - 1; i >= 0; i--)
                {
                    ushort curretType = targetTypeDictionary.ElementAt(i).Value;

                    //Ignore non-cube types and decoys.
                    if (curretType != 31 && curretType != 32 && curretType != 33 && curretType != 35)
                    {
                        List<IMySlimBlock> gridBlocks = new List<IMySlimBlock>();
                        try
                        {
                            ((IMyCubeBlock)targetTypeDictionary.ElementAt(i).Key).CubeGrid.GetBlocks(gridBlocks);
                        }
                        catch
                        {
                            //Problematic Enumeration error, stop targeting.
                            data.validTargetID = null;
                            return;
                        }

                        //MinGridSize
                        if (gridBlocks.Count < data.turret.minimumGridSizeStateDictionary[targetTypeDictionary.ElementAt(i).Value])
                        {
                            targetTypeDictionary.Remove(targetTypeDictionary.ElementAt(i).Key);
                        }
                    }
                }

                //log("Turret: " + data.turret.Entity.EntityId.ToString() + " 6: " + targetTypeDictionary.Count.ToString() + " : " + getTypes(targetTypeDictionary));
                //Eliminate targets that are out of the turrets firing ark.
                
                for (int i = targetTypeDictionary.Count - 1; i >= 0; i--)
                {
                    if (isInFiringArk(data.turret, targetTypeDictionary.ElementAt(i).Key.GetPosition()) == false)
                    {
                        targetTypeDictionary.Remove(targetTypeDictionary.ElementAt(i).Key);
                    }
                }


                //log("Turret: " + data.turret.Entity.EntityId.ToString() + " 7: " + targetTypeDictionary.Count.ToString() + " : " + getTypes(targetTypeDictionary));
                //Sort targets by priority and distance.
                List<IMyEntity> tempTargets = targetTypeDictionary.Keys.ToList();
                if(data.turret.Entity != null)
                {
                    tempTargets.Sort(new SortByTargetingPriorityAndDistance(data.turret.Entity.GetPosition(), targetTypeDictionary, data.turret.targetTypesListItems));
                }
                

                //log("Turret: " + data.turret.Entity.EntityId.ToString() + " 8");
                //Update TargetTypeDictionary with new order.
                targetTypeDictionary = getTargetTypeDictionary(tempTargets);

                //log("Turret: " + data.turret.Entity.EntityId.ToString() + " 9: " + targetTypeDictionary.Count.ToString() + " : " + getTypes(targetTypeDictionary));
                //Start shooting rays.
                //try
                //{
                data.validTargetID = castTargetingRay(targetTypeDictionary, data.turret);
                    //}
                    //catch (Exception err)
                    //{
                    //    log(err.Message);
                    //    log(err.StackTrace);
                    //}
            }
            catch(Exception e)
            {
                log(e.Message);
                log(e.StackTrace);
            }

            SmartTurretsProfiler.Instance.Stop(profilerId);
        }

        public static void collectTargetsThread(WorkData workData)
        {
            Guid profilerId = SmartTurretsProfiler.Instance.Start("collectTargetsThread");

            CollectingData data = (CollectingData)workData;

            BoundingSphereD turretRangeSphere = new BoundingSphereD(data.Position, data.MaxRange + 1000);
            List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref turretRangeSphere);
            List<IMyEntity> targetCandidates = new List<IMyEntity>();


            foreach (IMyEntity entity in entities)
            {
                if (entity is IMyCubeGrid)
                {
                    List<IMySlimBlock> blocks = new List<IMySlimBlock>();
                    List<IMyEntity> cubeBlocks = new List<IMyEntity>();
                    (entity as IMyCubeGrid).GetBlocks(blocks, (x) => { return x.FatBlock != null; });

                    foreach (IMySlimBlock block in blocks)
                    {
                        cubeBlocks.Add(block.FatBlock);
                    }

                    targetCandidates.AddList(cubeBlocks);
                }
                else if (entity is IMyCharacter || entity is IMyMeteor)
                {
                    targetCandidates.Add(entity);
                }
            }

            targetCandidates.RemoveAll((x) => { return x.EntityId == data.EntityId; });
            data.Candidates = targetCandidates;

            SmartTurretsProfiler.Instance.Stop(profilerId);
        }


        //Gets each entities associated type, represented by a ushort.
        private static Dictionary<IMyEntity, ushort> getTargetTypeDictionary(List<IMyEntity> targets)
            {
            Dictionary<IMyEntity, ushort> workingDictionary = new Dictionary<IMyEntity, ushort>();
            
            foreach (IMyEntity target in targets)
            {
                ushort typeID = getTypeDictionaryKey(target);
                
                if (typeID != 00)
                {
                    workingDictionary.Add(target, typeID);
                }
            }

            return workingDictionary;
        }

        private static string getTypes(Dictionary<IMyEntity, ushort> targetTypeDictionary)
        {
            string output = "";

            foreach (IMyEntity entity in targetTypeDictionary.Keys)
            {
                output += (entity.GetType().FullName.Substring(entity.GetType().FullName.LastIndexOf('.')) + ", ");
            }

            return output;
        }

        //True if the distance between two points is greater then the range value, specificaly for use with turrets and targets (althow it will work with anything).
        private static bool checkTargetOutOfRange(Vector3D turretLocation, float range, Vector3D targetLocation)
        {
            return Vector3D.DistanceSquared(turretLocation, targetLocation) > Math.Pow(range, 2);
        }

        //True if the turret can aim at its target.
        private static bool isInFiringArk(SmartTurret turret, Vector3D targetLocation)
        {
            IMyLargeTurretBase turretBase = turret.Entity as IMyLargeTurretBase;
            if (turretBase == null)
                return false;

            MyLargeTurretBaseDefinition turretDefinition = (MyDefinitionManager.Static.GetCubeBlockDefinition(turretBase.BlockDefinition) as MyLargeTurretBaseDefinition);
            int azimuthMin = turretDefinition.MinAzimuthDegrees;
            int azimuthMax = turretDefinition.MaxAzimuthDegrees;
            int elevationMin = turretDefinition.MinElevationDegrees;
            int elevationMax = turretDefinition.MaxElevationDegrees;

            //Get turrets world rotation.
            Matrix3x3 turretRotation = turretBase.WorldMatrix.Rotation;

            //Make a Quaternion from the world rotation and invert it so we can rotate to ther way.
            Quaternion offset = Quaternion.CreateFromRotationMatrix(turretBase.WorldMatrix);
            offset.Conjugate();
            offset.Normalize();

            //Find the direction of the target from the turret.
            Vector3 dirToTarget = targetLocation - turretBase.GetPosition();
            dirToTarget.Normalize();

            //Rotate the dirrection of the target by the Quaternion to cancel out the turrets world rotation.
            dirToTarget = Vector3.Transform(dirToTarget, offset);

            //The targets direction is now relative to the world so we can get Azimuth and Elevation.
            float azimuth;
            float elevation;
            Vector3.GetAzimuthAndElevation(dirToTarget, out azimuth, out elevation);

            //Convert Azimuth and Elevation to deg for comparason with the turrets elevation and azimuth limits.
            azimuth = (float)(180 / Math.PI * azimuth);
            elevation = (float)(180 / Math.PI * elevation);

            return (azimuth >= azimuthMin && azimuth <= azimuthMax && elevation >= elevationMin && elevation <= elevationMax);
        }

        private static long? castTargetingRay(Dictionary<IMyEntity, ushort> targetTypeDictionary, SmartTurret turret)
        {
            IMyEntity turretEntity = turret.Entity;
            if (turretEntity == null)
                return null;

            IMySlimBlock turretSlimBlock = (turretEntity as IMyCubeBlock).SlimBlock;
            IMyCubeGrid turretCubeGrid = turretSlimBlock.CubeGrid;
            Vector3D turretLocation = getTurretLocation(turretEntity);
            
            List<IMyCubeGrid> grids = new List<IMyCubeGrid>();
            List<IMySlimBlock> turretGridBlockList = new List<IMySlimBlock>();
            List<IMySlimBlock> targetGridBlockList = new List<IMySlimBlock>();
            try
            {
                turretCubeGrid.GetBlocks(turretGridBlockList, (x) => { return x != turretSlimBlock; });
            }
            catch
            {
                //Harmless Enumeration error, stop targeting.
                return null;
            }
            
            foreach (KeyValuePair<IMyEntity, ushort> pair in targetTypeDictionary)
            {
                grids.Clear();
                targetGridBlockList.Clear();

                IMySlimBlock targetSlimBlock = null;
                IMyEntity target = pair.Key;
                bool isTargetValid = true;
                IMyCubeGrid targetCubeGrid = null;
                Vector3D targetLocation = target.GetPosition();
                LineD line = new LineD(turretLocation, targetLocation);
                BoundingSphereD entityCollectionZone = new BoundingSphereD(turretLocation + (turretLocation - targetLocation) / 2, Vector3D.Distance(turretLocation, targetLocation) + 1000);
                List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref entityCollectionZone);
                
                //Block specific setup
                if (target is IMyCubeBlock)
                {
                    targetSlimBlock = (target as IMyCubeBlock).SlimBlock;
                    targetCubeGrid = targetSlimBlock.CubeGrid;
                    try
                    {
                        targetCubeGrid.GetBlocks(targetGridBlockList, (x) => { return x != targetSlimBlock; });
                    }
                    catch
                    {
                        //Harmless Enumeration error, stop targeting.
                        return null;
                    }
                }

                //More setup and check voxels
                foreach (IMyEntity entity in entities)
                {
                    if (entity is IMyCubeGrid)
                    {
                        IMyCubeGrid grid = entity as IMyCubeGrid;

                        if (grid != turretCubeGrid && grid != targetCubeGrid)
                        {
                            grids.Add(grid);
                        }
                    }
                    else if (entity is MyVoxelBase)
                    {
                        Vector3D? output;
                        if ((entity as MyVoxelBase).GetIntersectionWithLine(ref line, out output))
                        {
                            //log("Target: " + target.GetType().ToString() + " Failed because of intersecting voxel");
                            isTargetValid = false;
                            break;
                        }
                    }
                }
                
                if (!isTargetValid)
                {
                    continue;
                }

                //Relation of intersecting grids
                foreach (IMyCubeGrid grid in grids)
                {
                    if (grid.WorldAABB.Intersects(ref line) == true)
                    {
                        List<IMySlimBlock> currentGridBlocks = new List<IMySlimBlock>();
                        try
                        {
                            grid.GetBlocks(currentGridBlocks);
                        }
                        catch
                        {
                            //Harmless Enumeration error, stop targeting.
                            return null;
                        }


                        MyRelationsBetweenPlayerAndBlock relation = MyRelationsBetweenPlayerAndBlock.NoOwnership;

                        if (grid.BigOwners.Count > 0)
                        {
                            relation = turretSlimBlock.FatBlock.GetUserRelationToOwner(grid.BigOwners[0]);
                        }

                        bool throughFriendliesState = pair.Value == 35 ? false : turret.throughFriendliesStateDictionary[pair.Value];
                        bool throughNeutralsState = pair.Value == 35 ? false : turret.throughNeutralsStateDictionary[pair.Value];
                        bool throughHostilesState = pair.Value == 35 ? false : turret.throughHostilesStateDictionary[pair.Value];

                        if ((!throughHostilesState && relation == MyRelationsBetweenPlayerAndBlock.Enemies) || (!throughNeutralsState && (relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)) || (!throughFriendliesState && (relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare)))
                        {
                            foreach (IMySlimBlock slimBlock in currentGridBlocks)
                            {
                                BoundingBoxD collisionBox;
                                slimBlock.GetWorldBoundingBox(out collisionBox, false);
                                //Vector3D boxCenter = collisionBox.Center;

                                /*float azimuth;
                                float elevation;
                                getAzimuthAndElevationRelative(collisionBox.Center - turretLocation, out azimuth, out elevation);

                                float tollerence = (float)(Math.Tanh(collisionBox.Size.LengthSquared() / Vector3D.DistanceSquared(boxCenter, turretLocation)) / 2 * 0.0174533);

                                if (azimuth < tollerence && azimuth > -tollerence && elevation < tollerence && elevation > -tollerence)
                                {*/
                                    if (collisionBox.Intersects(ref line) == true)
                                    {
                                        slimBlock.GetWorldBoundingBox(out collisionBox, true);

                                        if (collisionBox.Intersects(ref line) == true)
                                        {
                                            //log("Target: " + target.GetType().ToString() + " Failed because of intersecting other grid");
                                            isTargetValid = false;
                                            break;
                                        }
                                    }
                                //}
                            }
                        }
                    }
                }
                
                if (!isTargetValid)
                {
                    continue;
                }

                //If intersecting turrets grid
                foreach (IMySlimBlock slimBlock in turretGridBlockList)
                {
                    BoundingBoxD collisionBox;
                    slimBlock.GetWorldBoundingBox(out collisionBox, false);
                    //Vector3D boxCenter = collisionBox.Center;

                    /*float azimuth;
                    float elevation;
                    getAzimuthAndElevationRelative(collisionBox.Center - turretLocation, out azimuth, out elevation);

                    float tollerence = (float)(Math.Tanh(collisionBox.Size.LengthSquared() / Vector3D.DistanceSquared(boxCenter, turretLocation)) / 2 * 0.0174533);

                    if (azimuth < tollerence && azimuth > -tollerence && elevation < tollerence && elevation > -tollerence)
                    {*/
                        if (collisionBox.Intersects(ref line) == true)
                        {
                            slimBlock.GetWorldBoundingBox(out collisionBox, true);

                            if (collisionBox.Intersects(ref line) == true)
                            {
                                //log("Target: " + target.GetType().ToString() + " Failed because of intersecting own grid");
                                isTargetValid = false;
                                break;
                            }
                        }
                    //}
                }
                
                if (!isTargetValid)
                {
                    continue;
                }

                //If intersecting targets grid and outside of manhatten distance
                if (targetSlimBlock != null)
                {
                    float obstacleToleranceState = pair.Value == 35 ? 3 : turret.obstacleToleranceStateDictionary[pair.Value];

                    if (obstacleToleranceState < 31)
                    {
                        foreach (IMySlimBlock slimBlock in targetGridBlockList)
                        {
                            BoundingBoxD collisionBox;
                            slimBlock.GetWorldBoundingBox(out collisionBox, false);
                            //Vector3D boxCenter = collisionBox.Center;

                            /*float azimuth;
                            float elevation;
                            getAzimuthAndElevationRelative(collisionBox.Center - turretLocation, out azimuth, out elevation);

                            float tollerence = (float)(Math.Tanh(collisionBox.Size.LengthSquared() / Vector3D.DistanceSquared(boxCenter, turretLocation)) / 2 * 0.0174533);

                            if (azimuth < tollerence && azimuth > -tollerence && elevation < tollerence && elevation > -tollerence)
                            {*/
                                if (collisionBox.Intersects(ref line) == true)
                                {
                                    slimBlock.GetWorldBoundingBox(out collisionBox, true);

                                    if (collisionBox.Intersects(ref line) == true)
                                    {
                                        if (Vector3I.DistanceManhattan(slimBlock.Position, targetSlimBlock.Position) > obstacleToleranceState)
                                        {
                                            //log("Target: " + target.GetType().ToString() + " Failed because of intersecting target grid outside of manhattan");
                                            isTargetValid = false;
                                            break;
                                        }
                                    }
                                }
                            //}
                        }
                    }
                }
                
                if (!isTargetValid)
                {
                    continue;
                }

                //Target Passed!
                return target.EntityId;
            }

            //All targets failed
            return null;
        }

        private static void getAzimuthAndElevationRelative(Vector3 dirToTarget, out float azimuth, out float elevation)
        {
            if (dirToTarget != Vector3.Backward)
            {
                Quaternion offset;
                Quaternion.CreateFromTwoVectors(ref dirToTarget, ref Vector3.Forward, out offset);

                dirToTarget = Vector3.Transform(dirToTarget, offset);
            }

            //Return azimuth and elevation
            Vector3.GetAzimuthAndElevation(dirToTarget, out azimuth, out elevation);
        }

        //Some (lasy :P ) code of Lucas's for getting subparts
        private static Vector3D getTurretLocation(IMyEntity turretEntity)
        {
            MyEntity entity = turretEntity as MyEntity;

            Vector3D output = new Vector3D();

            foreach (var key in entity.Subparts.Keys)
            {
                foreach (var keyB in entity.Subparts[key].Subparts.Keys)
                {
                    output = entity.Subparts[key].Subparts[keyB].PositionComp.GetPosition();
                }
            }

            return output;
        }

        //Old raycast methods
        /*
        //True if the shot is good. VERY RESOURCE DEMANDING PARRALLEL USE ONLY!!!
        public static long? castTargetingRay(Dictionary<IMyEntity, ushort> targetTypeDictionary, TargetingData data)
        {
            //Get locations
            Vector3D turretLocation = data.turret.Entity.GetPosition();
            Vector3D targetLocation = targetTypeDictionary.First().Key.GetPosition();
            
            //Setup ray
            LineD line = new LineD(turretLocation, targetLocation);
            BoundingSphereD entityCollectionZone = new BoundingSphereD(turretLocation + (turretLocation - targetLocation) / 2, Vector3D.Distance(turretLocation, targetLocation) + 1000);
            
            //Setup lists
            List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref entityCollectionZone);
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            List<IMySlimBlock> blockList = new List<IMySlimBlock>();
            List<IMySlimBlock> restoreBlockList;
            
            //Get cubes from grids and handle voxels
            foreach (IMyEntity entity in entities)
            {
                if (entity is IMyCubeGrid)
                {
                    (entity as IMyCubeGrid).GetBlocks(blockList);
                }
                else if (entity is MyVoxelBase)
                {
                    Vector3D? output;,
                    if ((entity as MyVoxelBase).GetIntersectionWithLine(ref line, out output))
                    {
                        return null;
                    }
                }
            }
            
            //Remove the turret from the blocklist and make a restore point.
            blockList.Remove((data.turret.Entity as IMyCubeBlock).SlimBlock);
            restoreBlockList = new List<IMySlimBlock>(blockList);

            //Evaluate the blocks rays
            for (int i = 0; i < targetTypeDictionary.Count; i++)
            {
                if (evaluateTargetingRay(i, targetTypeDictionary, data, line, blockList) == true)
                {
                    return targetTypeDictionary.ElementAt(i).Key.EntityId;
                }

                blockList = new List<IMySlimBlock>(restoreBlockList);
            }
            log("Target Fail");
            return null;
        }

        //Internal component of castTargetingRay.
        private static bool evaluateTargetingRay(int index, Dictionary<IMyEntity, ushort> targetTypeDictionary, TargetingData data, LineD line, List<IMySlimBlock> blockList)
        {
            //Get grid of target (if block).
            long? targetGridID = null;
            if (targetTypeDictionary.ElementAt(index).Key is IMyCubeBlock)
            {
                targetGridID = (targetTypeDictionary.ElementAt(index).Key as IMyCubeBlock).CubeGrid.EntityId;
            }
            
            //Main "raycast" section with processing
            for (int k = blockList.Count - 1; k >= 0; k--)
            {
                BoundingBoxD collisionBox;

                if (blockList[k].FatBlock != null)
                {
                    collisionBox = blockList[k].FatBlock.WorldAABB;
                }
                else
                {
                    blockList[k].GetWorldBoundingBox(out collisionBox, true);
                }
                
                if (collisionBox.Intersects(ref line) == true)
                {
                    IMyCubeGrid grid = blockList[k].CubeGrid;
                    List<long> ownerIDs = grid.BigOwners;

                    if (targetGridID == grid.EntityId)
                    {
                        if (Vector3I.DistanceManhattan(blockList[k].Position, (targetTypeDictionary.ElementAt(index).Key as IMyCubeBlock).Position) > data.turret.obstacleToleranceStateDictionary[targetTypeDictionary.ElementAt(index).Value])
                        {
                            return false;
                        }
                    }
                    else
                    {
                        foreach (long ownerID in ownerIDs)
                        {
                            MyRelationsBetweenPlayerAndBlock relation = (data.turret.Entity as IMyTerminalBlock).GetUserRelationToOwner(ownerID);

                            if ((relation == MyRelationsBetweenPlayerAndBlock.Enemies && data.turret.throughHostilesStateDictionary[targetTypeDictionary.ElementAt(index).Value] == false) || (relation == MyRelationsBetweenPlayerAndBlock.Neutral && data.turret.throughNeutralsStateDictionary[targetTypeDictionary.ElementAt(index).Value] == false) || (relation != MyRelationsBetweenPlayerAndBlock.Enemies && relation != MyRelationsBetweenPlayerAndBlock.Neutral && data.turret.throughFriendliesStateDictionary[targetTypeDictionary.ElementAt(index).Value] == false))
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }
        */
    }

    public class TargetingData : WorkData
    {
        public SmartTurret turret;
        public List<IMyEntity> targets;
        public long? validTargetID;

        public TargetingData(SmartTurret turret, List<IMyEntity> targets)
        {
            this.turret = turret;
            this.targets = targets;
        }

        public TargetingData(long? validTargetID)
        {
            this.validTargetID = validTargetID;
        }
    }

    public class CollectingData : WorkData
    {
        public long EntityId;
        public Vector3D Position;
        public float MaxRange;
        public List<IMyEntity> Candidates;

    }

    //Comparator for target priority
    public class SortByTargetingPriorityAndDistance : IComparer<IMyEntity>
    {
        Vector3D turretLocation;
        Dictionary<IMyEntity, ushort> targetTypeDictionary;
        List<MyTerminalControlListBoxItem> targetTypesListItems;

        public int Compare(IMyEntity x, IMyEntity y)
        {
            MyTerminalControlListBoxItem typeItemx = null;
            MyTerminalControlListBoxItem typeItemy = null;
            int priorityx = -1;
            int priorityy = -1;

            if (!(x is IMyDecoy))
            {
                typeItemx = targetTypesListItems.Find((z) => { return (z.UserData as ListBoxItemData).id == targetTypeDictionary[x]; });
                priorityx = targetTypesListItems.IndexOf(typeItemx);
            }
            if (!(y is IMyDecoy))
            {
                typeItemy = targetTypesListItems.Find((z) => { return (z.UserData as ListBoxItemData).id == targetTypeDictionary[y]; });
                priorityy = targetTypesListItems.IndexOf(typeItemy);
            }

            if (priorityx > priorityy)
            {
                return 1;
            }
            else if (priorityx < priorityy)
            {
                return -1;
            }
            else
            {
                double distx = Vector3D.DistanceSquared(x.GetPosition(), turretLocation);
                double disty = Vector3D.DistanceSquared(y.GetPosition(), turretLocation);

                if (distx > disty)
                {
                    return 1;
                }
                else if (disty > distx)
                {
                    return -1;
                }
                else
                {
                    return 0;
                }
            }
        }

        public SortByTargetingPriorityAndDistance(Vector3D turretLocation, Dictionary<IMyEntity, ushort> targetTypeDictionary, List<MyTerminalControlListBoxItem> targetTypesListItems)
        {
            this.turretLocation = turretLocation;
            this.targetTypeDictionary = targetTypeDictionary;
            this.targetTypesListItems = targetTypesListItems;
        }
    }
}
