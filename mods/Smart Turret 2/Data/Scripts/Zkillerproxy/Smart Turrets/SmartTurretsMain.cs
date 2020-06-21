using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using Sandbox.ModAPI.Interfaces.Terminal;
using ParallelTasks;
using Sandbox.Common.ObjectBuilders;
using VRage.ObjectBuilders;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;
using static Zkillerproxy.SmartTurretMod.SmartTurretUtilities;
using static Zkillerproxy.SmartTurretMod.SmartTurretTargetingUtilities;

namespace Zkillerproxy.SmartTurretMod
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeGatlingTurret), true)]
    public class ExtendedLargeGatlingTurret : SmartTurret { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeMissileTurret), true)]
    public class ExtendedLargeMissileTurret : SmartTurret { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_InteriorTurret), true)]
    public class ExtendedInteriorTurret : SmartTurret { }

    public class SmartTurret : MyGameLogicComponent
    {
        //Waiting List
        public static List<IMyEntity> targetingWaitingList = new List<IMyEntity>();
        public bool needsUpdate = false;

        //Terminal Control Data
        public bool smartTargetingSwitchState = false;
        public List<MyTerminalControlListBoxItem> selectedListItems = new List<MyTerminalControlListBoxItem>();
        public List<MyTerminalControlListBoxItem> targetTypesListItems = new List<MyTerminalControlListBoxItem>();
        public Dictionary<ushort, float> rangeStateDictionary = new Dictionary<ushort, float>();
        public Dictionary<ushort, bool> targetSmallGridsStateDictionary = new Dictionary<ushort, bool>();
        public Dictionary<ushort, bool> targetLargeGridsStateDictionary = new Dictionary<ushort, bool>();
        public Dictionary<ushort, bool> targetStationsStateDictionary = new Dictionary<ushort, bool>();
        public Dictionary<ushort, bool> targetNeutralsStateDictionary = new Dictionary<ushort, bool>();
        public Dictionary<ushort, float> minimumGridSizeStateDictionary = new Dictionary<ushort, float>();
        public Dictionary<ushort, float> obstacleToleranceStateDictionary = new Dictionary<ushort, float>();
        public Dictionary<ushort, bool> throughFriendliesStateDictionary = new Dictionary<ushort, bool>();
        public Dictionary<ushort, bool> throughNeutralsStateDictionary = new Dictionary<ushort, bool>();
        public Dictionary<ushort, bool> throughHostilesStateDictionary = new Dictionary<ushort, bool>();
        public List<MyTerminalControlListBoxItem> presentListContent = new List<MyTerminalControlListBoxItem>();
        public List<MyTerminalControlListBoxItem> presentSelectedList = new List<MyTerminalControlListBoxItem>();
        public StringBuilder presentNameStringBuilder = new StringBuilder();

        //Dummy control data, used untill the save button is pressed.
        public List<MyTerminalControlListBoxItem> selectedListItemsDummy = new List<MyTerminalControlListBoxItem>();
        public List<MyTerminalControlListBoxItem> targetTypesListItemsDummy = new List<MyTerminalControlListBoxItem>();
        public Dictionary<ushort, float> rangeStateDictionaryDummy = new Dictionary<ushort, float>();
        public Dictionary<ushort, bool> targetSmallGridsStateDictionaryDummy = new Dictionary<ushort, bool>();
        public Dictionary<ushort, bool> targetLargeGridsStateDictionaryDummy = new Dictionary<ushort, bool>();
        public Dictionary<ushort, bool> targetStationsStateDictionaryDummy = new Dictionary<ushort, bool>();
        public Dictionary<ushort, bool> targetNeutralsStateDictionaryDummy = new Dictionary<ushort, bool>();
        public Dictionary<ushort, float> minimumGridSizeStateDictionaryDummy = new Dictionary<ushort, float>();
        public Dictionary<ushort, float> obstacleToleranceStateDictionaryDummy = new Dictionary<ushort, float>();
        public Dictionary<ushort, bool> throughFriendliesStateDictionaryDummy = new Dictionary<ushort, bool>();
        public Dictionary<ushort, bool> throughNeutralsStateDictionaryDummy = new Dictionary<ushort, bool>();
        public Dictionary<ushort, bool> throughHostilesStateDictionaryDummy = new Dictionary<ushort, bool>();

        //Other Variables
        static bool controlsAdded = false;
        long? targetID = null;
        bool isOtherThreadRunning = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            //Load and Setup
            loadTurretData(Entity);
            setupTerminalVariables();

            //Add controls on init, and only once.
            if (controlsAdded == false)
            {
                controlsAdded = true;
                addTerminalControls();
            }

            //Bind ControlModifier for this turret.
            MyAPIGateway.TerminalControls.CustomControlGetter += controlModifier;

            //Must be triggered after setup, otherwise would have this in load and setup.
            applyVanillaControlDisabler(this);
        }

        //Shoot if there is a target.
        public override void UpdateBeforeSimulation()
        {
            if (MyAPIGateway.Session.IsServer && smartTargetingSwitchState && (Entity as IMyTerminalBlock).IsWorking)
            {
                //Fire control
                IMyLargeTurretBase turretBase = Entity as IMyLargeTurretBase;

                if (targetID.HasValue && MyAPIGateway.Entities.EntityExists(targetID))
                {
                    IMyEntity targetEntity = MyAPIGateway.Entities.GetEntityById(targetID);
                    if (turretBase.HasTarget == false && targetEntity != null)
                    {
                        turretBase.TrackTarget(targetEntity);
                    }
                }
                else
                {
                    if (turretBase.HasTarget == true)
                    {
                        bool isIdleRotationEnabled = turretBase.EnableIdleRotation;
                        turretBase.ResetTargetingToDefault(); //for some reason this also resets idle rotation, hence all the code either side for correcting that.
                        turretBase.EnableIdleRotation = isIdleRotationEnabled;
                        turretBase.SyncEnableIdleRotation();
                    }
                }

                if (needsUpdate)
                {
                    startTargeting();
                    needsUpdate = false;
                }
                else if (!targetingWaitingList.Contains(Entity))
                {
                    targetingWaitingList.Add(Entity);
                }
            }
        }

        //Clean up when the turret is removed.
        public override void Close()
        {
            //Unbind ControlModifier for this turret.
            MyAPIGateway.TerminalControls.CustomControlGetter -= controlModifier;
        }

        //START OF TARGETING. Targeting thread is initiated here.
        private void startTargeting()
        {
            if (smartTargetingSwitchState == true && (Entity as IMyTerminalBlock).IsWorking)
            {
                if (isOtherThreadRunning == false)
                {
                    //Start check for target valididity on curret target (so we know if a new target is needed), if there isn't a target find a new one.
                    if (MyAPIGateway.Entities.EntityExists(targetID))
                    {
                        TargetingData data = new TargetingData(
                            this,
                            new List<IMyEntity>() { MyAPIGateway.Entities.GetEntityById(targetID) }
                            );

                        MyAPIGateway.Parallel.StartBackground(validateTargets, validateTargetsCallback, data);
                        isOtherThreadRunning = true;
                    }
                    else
                    {
                        BoundingSphereD turretRangeSphere = new BoundingSphereD(Entity.GetPosition(), getTurretMaxRange(Entity as IMyTerminalBlock) + 1000);
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

                        targetCandidates.RemoveAll((x) => { return x.EntityId == Entity.EntityId; });

                        TargetingData data = new TargetingData(
                            this,
                            targetCandidates
                            );

                        MyAPIGateway.Parallel.StartBackground(validateTargets, validateTargetsCallback, data);
                        isOtherThreadRunning = true;
                    }
                }
            }
        }

        //END OF TARGETING. Callback for target validation.
        private void validateTargetsCallback(WorkData data)
        {
            if (Entity != null)
            {
                //log("Turret: " + Entity.EntityId.ToString() + " Target: " + (data as TargetingData).validTargetID.ToString());
                targetID = (data as TargetingData).validTargetID;
                isOtherThreadRunning = false;
            }
        }

        private void setupTerminalVariables()
        {
            if (targetTypesListItems.Count == 0)
            {
                targetTypesListItems.AddList(new ListBoxItemDefaultGenerator().listBoxItemDefaultList);
            }

            if (rangeStateDictionary.Count == 0)
            {
                float maxRange = getTurretMaxRange(Entity as IMyTerminalBlock);

                foreach (MyTerminalControlListBoxItem item in targetTypesListItems)
                {
                    rangeStateDictionary.Add((item.UserData as ListBoxItemData).id, maxRange);
                }
            }

            if (targetSmallGridsStateDictionary.Count == 0)
            {
                foreach (MyTerminalControlListBoxItem item in targetTypesListItems)
                {
                    targetSmallGridsStateDictionary.Add((item.UserData as ListBoxItemData).id, true);
                }
            }

            if (targetLargeGridsStateDictionary.Count == 0)
            {
                foreach (MyTerminalControlListBoxItem item in targetTypesListItems)
                {
                    targetLargeGridsStateDictionary.Add((item.UserData as ListBoxItemData).id, true);
                }
            }

            if (targetStationsStateDictionary.Count == 0)
            {
                foreach (MyTerminalControlListBoxItem item in targetTypesListItems)
                {
                    targetStationsStateDictionary.Add((item.UserData as ListBoxItemData).id, true);
                }
            }

            if (targetNeutralsStateDictionary.Count == 0)
            {
                foreach (MyTerminalControlListBoxItem item in targetTypesListItems)
                {
                    targetNeutralsStateDictionary.Add((item.UserData as ListBoxItemData).id, false);
                }
            }

            if (minimumGridSizeStateDictionary.Count == 0)
            {
                foreach (MyTerminalControlListBoxItem item in targetTypesListItems)
                {
                    minimumGridSizeStateDictionary.Add((item.UserData as ListBoxItemData).id, 10);
                }
            }

            if (obstacleToleranceStateDictionary.Count == 0)
            {
                foreach (MyTerminalControlListBoxItem item in targetTypesListItems)
                {
                    obstacleToleranceStateDictionary.Add((item.UserData as ListBoxItemData).id, 3);
                }
            }

            if (throughFriendliesStateDictionary.Count == 0)
            {
                foreach (MyTerminalControlListBoxItem item in targetTypesListItems)
                {
                    throughFriendliesStateDictionary.Add((item.UserData as ListBoxItemData).id, false);
                }
            }

            if (throughNeutralsStateDictionary.Count == 0)
            {
                foreach (MyTerminalControlListBoxItem item in targetTypesListItems)
                {
                    throughNeutralsStateDictionary.Add((item.UserData as ListBoxItemData).id, false);
                }
            }

            if (throughHostilesStateDictionary.Count == 0)
            {
                foreach (MyTerminalControlListBoxItem item in targetTypesListItems)
                {
                    throughHostilesStateDictionary.Add((item.UserData as ListBoxItemData).id, false);
                }
            }

            cloneToDummies(this);
        }

        private void addTerminalControls()
        {
            //Terminal Control Variables
            IMyTerminalControlButton targetTypesEnableDisable = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyLargeTurretBase>("ST_TargetTypesEnableDisable");
            IMyTerminalControlButton targetTypesPriorityUp = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyLargeTurretBase>("ST_TargetTypesPriorityUp");
            IMyTerminalControlButton targetTypesPriorityDown = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyLargeTurretBase>("ST_TargetTypesPriorityDown");
            IMyTerminalControlListbox targetTypesList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyLargeTurretBase>("ST_TargetTypesList");
            IMyTerminalControlSlider smartTargetRange = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyLargeTurretBase>("ST_RangeSlider");
            IMyTerminalControlOnOffSwitch targetSmallGrids = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyLargeTurretBase>("ST_TargetSmallGrids");
            IMyTerminalControlOnOffSwitch targetLargeGrids = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyLargeTurretBase>("ST_TargetLargeGrids");
            IMyTerminalControlOnOffSwitch targetStations = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyLargeTurretBase>("ST_TargetStations");
            IMyTerminalControlOnOffSwitch targetNeutrals = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyLargeTurretBase>("ST_TargetNeutrals");
            IMyTerminalControlSlider minimumGridSize = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyLargeTurretBase>("ST_MinimumGridSize");
            IMyTerminalControlSlider obstacleTolerance = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyLargeTurretBase>("ST_ObstacleToleranceSlider");
            IMyTerminalControlOnOffSwitch throughFriendlies = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyLargeTurretBase>("ST_ThroughFriendlies");
            IMyTerminalControlOnOffSwitch throughNeutrals = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyLargeTurretBase>("ST_ThroughNeutrals");
            IMyTerminalControlOnOffSwitch throughHostiles = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyLargeTurretBase>("ST_ThroughHostiles");
            IMyTerminalControlOnOffSwitch smartTargetingSwitch = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyLargeTurretBase>("ST_SmartTargetingSwitch");
            IMyTerminalControlButton loadPresent = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyLargeTurretBase>("ST_LoadPresentButton");
            IMyTerminalControlButton deletePresent = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyLargeTurretBase>("ST_DeletePresentButton");
            IMyTerminalControlListbox presentList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyLargeTurretBase>("ST_PresentList");
            IMyTerminalControlButton savePresent = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyLargeTurretBase>("ST_SavePresentButton");
            IMyTerminalControlTextbox presentName = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyLargeTurretBase>("ST_PresentName");
            IMyTerminalControlButton saveChanges = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyLargeTurretBase>("ST_SaveChangesButton");
            IMyTerminalControlButton undoChanges = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyLargeTurretBase>("ST_UndoChangesButton");

            //Divider
            IMyTerminalControlSeparator divider1 = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyLargeTurretBase>("D_ST_Divider1");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(divider1);

            #region SmartTargetingSwitch
            smartTargetingSwitch.Getter = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret != null)
                {
                    return ActionTurret.smartTargetingSwitchState;
                }

                return false;
            };
            smartTargetingSwitch.Setter = (TerminalTurret, NewState) => {
                getSmartTurret(TerminalTurret).smartTargetingSwitchState = NewState;
                if (NewState == true)
                {
                    disableVanillaControls(TerminalTurret);
                    updateVanillaControls(TerminalTurret);
                }
                else
                {
                    updateVanillaControls(TerminalTurret);
                }
                saveTurretData(TerminalTurret, true);
            };
            smartTargetingSwitch.Title = MyStringId.GetOrCompute("Smart Targeting");
            smartTargetingSwitch.Tooltip = MyStringId.GetOrCompute("Use Smart Targeting AI.\nThis will disable the vanilla targeting settings,\nthe Smart Targeting settings will be used instead.");
            smartTargetingSwitch.SupportsMultipleBlocks = true;
            smartTargetingSwitch.OffText = MyStringId.GetOrCompute("Off");
            smartTargetingSwitch.OnText = MyStringId.GetOrCompute("On");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(smartTargetingSwitch);

            //SmartTargetingSwitch Action
            IMyTerminalAction actionSmartTargetingSwitch = MyAPIGateway.TerminalControls.CreateAction<IMyLargeTurretBase>("ST_SmartTargetingSwitchAction");
            actionSmartTargetingSwitch.Action = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);
                if (ActionTurret.smartTargetingSwitchState == true)
                {
                    ActionTurret.smartTargetingSwitchState = false;
                    updateVanillaControls(TerminalTurret);
                }
                else
                {
                    ActionTurret.smartTargetingSwitchState = true;
                    disableVanillaControls(TerminalTurret);
                    updateVanillaControls(TerminalTurret);
                }
                saveTurretData(TerminalTurret, true);
            };
            actionSmartTargetingSwitch.Name = new StringBuilder("Smart Targeting On/Off");
            actionSmartTargetingSwitch.Writer = (TerminalTurret, ActionText) => {
                ActionText.Clear();
                if (getSmartTurret(TerminalTurret).smartTargetingSwitchState == true)
                {
                    ActionText.Append("On");
                }
                else
                {
                    ActionText.Append("Off");
                }
            };
            actionSmartTargetingSwitch.Icon = "Textures\\GUI\\Icons\\Actions\\Toggle.dds";
            MyAPIGateway.TerminalControls.AddAction<IMyLargeTurretBase>(actionSmartTargetingSwitch);
            #endregion 

            //Divider
            IMyTerminalControlSeparator divider4 = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyLargeTurretBase>("D_ST_Divider4");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(divider4);

            //Title
            IMyTerminalControlLabel titlePresents = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyLargeTurretBase>("D_ST_TitlePresents");
            titlePresents.Label = MyStringId.GetOrCompute("Presets");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(titlePresents);

            #region LoadPresentButton
            loadPresent.Action = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);
                
                if (ActionTurret == null)
                {
                    return;
                }

                if (ActionTurret.presentSelectedList.Count == 1)
                {
                    loadTurretPresent(TerminalTurret, presentName, ActionTurret.presentSelectedList.First().Text.String);
                    saveTurretData(TerminalTurret, true);
                    ActionTurret.selectedListItemsDummy.Clear();
                }

                targetTypesList.UpdateVisual();
                smartTargetRange.UpdateVisual();
                targetSmallGrids.UpdateVisual();
                targetLargeGrids.UpdateVisual();
                targetStations.UpdateVisual();
                targetNeutrals.UpdateVisual();
                minimumGridSize.UpdateVisual();
                obstacleTolerance.UpdateVisual();
                throughFriendlies.UpdateVisual();
                throughNeutrals.UpdateVisual();
                throughHostiles.UpdateVisual();
            };
            loadPresent.Enabled = (TerminalTurret) => {
                return getSmartTurret(TerminalTurret).presentSelectedList.Count > 0;
            };
            loadPresent.SupportsMultipleBlocks = true;
            loadPresent.Title = MyStringId.GetOrCompute("Load Preset");
            loadPresent.Tooltip = MyStringId.GetOrCompute("Load the selected settings preset.");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(loadPresent);
            #endregion

            #region DeletePresentButton
            deletePresent.Action = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);
                
                if (ActionTurret == null)
                {
                    return;
                }

                if (ActionTurret.presentSelectedList.Count == 1)
                {
                    deleteTurretPresent(TerminalTurret, presentName, ActionTurret.presentSelectedList.First().Text.String);
                }
                presentList.UpdateVisual();
            };
            deletePresent.Enabled = (TerminalTurret) => {
                return getSmartTurret(TerminalTurret).presentSelectedList.Count > 0;
            };
            deletePresent.SupportsMultipleBlocks = false;
            deletePresent.Title = MyStringId.GetOrCompute("Delete Preset");
            deletePresent.Tooltip = MyStringId.GetOrCompute("Delete the selected settings preset.");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(deletePresent);
            #endregion

            #region PresentList
            presentList.ListContent = (TerminalTurret, ItemList, SelectedItemList) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);
                
                if (ActionTurret == null)
                {
                    return;
                }

                refreshTurretPresentList(TerminalTurret);
                ItemList.Clear();
                ItemList.AddList(ActionTurret.presentListContent);
                SelectedItemList.Clear();

                foreach (MyTerminalControlListBoxItem Item in ActionTurret.presentSelectedList)
                {
                    SelectedItemList.Add(ActionTurret.presentListContent.Find((x) => { return x.Text == Item.Text; }));
                }
            };
            presentList.ItemSelected = (TerminalTurret, SelectedItemList) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);
                ActionTurret.presentSelectedList = SelectedItemList;
                loadPresent.UpdateVisual();
                deletePresent.UpdateVisual();
            };
            presentList.SupportsMultipleBlocks = true;
            presentList.Multiselect = false;
            presentList.VisibleRowsCount = 5;
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(presentList);
            #endregion

            #region SavePresentButton
            savePresent.Action = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return;
                }

                saveTurretPresent(TerminalTurret, presentName);
                presentList.UpdateVisual();
            };
            savePresent.SupportsMultipleBlocks = false;
            savePresent.Title = MyStringId.GetOrCompute("Save Preset");
            savePresent.Tooltip = MyStringId.GetOrCompute("Save this turrets current settings as a preset,\nmake sure to type a unique name for the preset below.");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(savePresent);
            #endregion

            #region PresentName
            presentName.Getter = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret != null)
                {
                    return ActionTurret.presentNameStringBuilder;
                }
                
                return new StringBuilder();
            };
            presentName.Setter = (TerminalTurret, stringbuilder) => {
                getSmartTurret(TerminalTurret).presentNameStringBuilder = stringbuilder;
            };
            presentName.Title = MyStringId.GetOrCompute("Preset Title");
            presentName.Tooltip = MyStringId.GetOrCompute("Type a unique name for your preset here.");
            presentName.SupportsMultipleBlocks = true;
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(presentName);
            #endregion

            //Divider
            IMyTerminalControlSeparator divider2 = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyLargeTurretBase>("D_ST_Divider2");
            divider2.SupportsMultipleBlocks = false;
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(divider2);

            //Title
            IMyTerminalControlLabel titleSave = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyLargeTurretBase>("D_ST_TitleSave");
            titleSave.Label = MyStringId.GetOrCompute("Targeting Settings");
            titleSave.SupportsMultipleBlocks = false;
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(titleSave);

            #region SaveChangesButton
            saveChanges.Action = (TerminalTurret) => {
                saveTurretData(TerminalTurret, true);
            };
            saveChanges.SupportsMultipleBlocks = false;
            saveChanges.Title = MyStringId.GetOrCompute("Save Changes");
            saveChanges.Tooltip = MyStringId.GetOrCompute("You must click this button to save your changes.\nYou DO NOT need to save changes it you:\n1: Only changed Smart Targeting On/Off.\n2: Loaded a preset.");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(saveChanges);
            #endregion

            #region UndoChangesButton
            undoChanges.Action = (TerminalTurret) => {
                cloneToDummies(getSmartTurret(TerminalTurret));
            };
            undoChanges.SupportsMultipleBlocks = false;
            undoChanges.Title = MyStringId.GetOrCompute("Undo Changes");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(undoChanges);
            #endregion

            //Divider
            IMyTerminalControlSeparator divider5 = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyLargeTurretBase>("D_ST_Divider5");
            divider5.SupportsMultipleBlocks = false;
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(divider5);

            //Title
            IMyTerminalControlLabel titleTypes = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyLargeTurretBase>("D_ST_TitleTypes");
            titleTypes.Label = MyStringId.GetOrCompute("Targeting Priorities");
            titleTypes.SupportsMultipleBlocks = false;
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(titleTypes);

            #region TargetTypesEnableDisable
            targetTypesEnableDisable.Action = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return;
                }

                if (ActionTurret.selectedListItemsDummy == null)
                {
                    return;
                }

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    string selectedListItemText = ActionTurret.selectedListItemsDummy[i].Text.String;
                    MyTerminalControlListBoxItem ListItem = ActionTurret.targetTypesListItemsDummy.Find(x => (x.UserData as ListBoxItemData).id == (ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id);
                    
                    if (selectedListItemText[1] == '+')
                    {
                        ListItem.Text = MyStringId.GetOrCompute(selectedListItemText.Replace('+', '-'));
                        (ListItem.UserData as ListBoxItemData).enabledState = false;
                    }
                    else
                    {
                        ListItem.Text = MyStringId.GetOrCompute(selectedListItemText.Replace('-', '+'));
                        (ListItem.UserData as ListBoxItemData).enabledState = true;
                    }
                    ActionTurret.selectedListItemsDummy[i] = ListItem;
                }
                targetTypesList.UpdateVisual();
            };
            targetTypesEnableDisable.SupportsMultipleBlocks = false;
            targetTypesEnableDisable.Title = MyStringId.GetOrCompute("Enable/Disable");
            targetTypesEnableDisable.Tooltip = MyStringId.GetOrCompute("(+) == Enabled, (-) == Disabled");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(targetTypesEnableDisable);
            #endregion

            #region TargetTypesPriorityUp
            targetTypesPriorityUp.Action = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return;
                }

                if (ActionTurret.selectedListItemsDummy == null)
                {
                    return;
                }

                SortSelectedItemsByIndex Comparer = new SortSelectedItemsByIndex(ActionTurret.targetTypesListItemsDummy);
                ActionTurret.selectedListItemsDummy.Sort(Comparer);

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    int index = ActionTurret.targetTypesListItemsDummy.FindIndex(x => (x.UserData as ListBoxItemData).id == (ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id);

                    if (index > 0)
                    {
                        ActionTurret.targetTypesListItemsDummy[index] = ActionTurret.targetTypesListItemsDummy[index - 1];
                        ActionTurret.targetTypesListItemsDummy[index - 1] = ActionTurret.selectedListItemsDummy[i];
                    }
                    else
                    {
                        return;
                    }
                }
                targetTypesList.UpdateVisual();
            };
            targetTypesPriorityUp.SupportsMultipleBlocks = false;
            targetTypesPriorityUp.Title = MyStringId.GetOrCompute("Priority Up");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(targetTypesPriorityUp);
            #endregion

            #region TargetTypesPriorityDown
            targetTypesPriorityDown.Action = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return;
                }

                if (ActionTurret.selectedListItemsDummy == null)
                {
                    return;
                }

                SortSelectedItemsByIndex Comparer = new SortSelectedItemsByIndex(ActionTurret.targetTypesListItemsDummy);
                ActionTurret.selectedListItemsDummy.Sort(Comparer);
                ActionTurret.selectedListItemsDummy.Reverse();

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    int index = ActionTurret.targetTypesListItemsDummy.FindIndex(x => (x.UserData as ListBoxItemData).id == (ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id);

                    if (index < ActionTurret.targetTypesListItemsDummy.Count() - 1)
                    {
                        ActionTurret.targetTypesListItemsDummy[index] = ActionTurret.targetTypesListItemsDummy[index + 1];
                        ActionTurret.targetTypesListItemsDummy[index + 1] = ActionTurret.selectedListItemsDummy[i];
                    }
                    else
                    {
                        return;
                    }
                }
                targetTypesList.UpdateVisual();
            };
            targetTypesPriorityDown.SupportsMultipleBlocks = false;
            targetTypesPriorityDown.Title = MyStringId.GetOrCompute("Priority Down");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(targetTypesPriorityDown);
            #endregion

            #region TargetTypesList
            targetTypesList.ListContent = (TerminalTurret, ItemList, SelectedItemList) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return;
                }

                ItemList.Clear();
                ItemList.AddList(ActionTurret.targetTypesListItemsDummy);
                SelectedItemList.Clear();

                foreach (MyTerminalControlListBoxItem Item in ActionTurret.selectedListItemsDummy)
                {
                    SelectedItemList.Add(ActionTurret.targetTypesListItemsDummy.Find((x) => { return (x.UserData as ListBoxItemData).id == (Item.UserData as ListBoxItemData).id; }));
                }
            };
            targetTypesList.ItemSelected = (TerminalTurret, SelectedItemList) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);
                ActionTurret.selectedListItemsDummy = SelectedItemList;

                smartTargetRange.UpdateVisual();
                targetSmallGrids.UpdateVisual();
                targetLargeGrids.UpdateVisual();
                targetStations.UpdateVisual();
                targetNeutrals.UpdateVisual();
                minimumGridSize.UpdateVisual();
                obstacleTolerance.UpdateVisual();
                throughFriendlies.UpdateVisual();
                throughNeutrals.UpdateVisual();
                throughHostiles.UpdateVisual();
            };
            targetTypesList.SupportsMultipleBlocks = false;
            targetTypesList.Multiselect = true;
            targetTypesList.VisibleRowsCount = 30;
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(targetTypesList);
            #endregion

            //Divider
            IMyTerminalControlSeparator divider3 = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyLargeTurretBase>("D_ST_Divider3");
            divider3.SupportsMultipleBlocks = false;
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(divider3);

            //Title
            IMyTerminalControlLabel titleSettings = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyLargeTurretBase>("D_ST_TitleSettings");
            titleSettings.Label = MyStringId.GetOrCompute("Targeting Settings For Selected Types");
            titleSettings.SupportsMultipleBlocks = false;
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(titleSettings);

            #region SmartTargetRangeSlider
            smartTargetRange.Getter = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    return ActionTurret.rangeStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id];
                }
                return 0;
            };
            smartTargetRange.Setter = (TerminalTurret, NewState) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    ActionTurret.rangeStateDictionaryDummy[(ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id] = NewState;
                }
            };
            smartTargetRange.Enabled = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return false;
                }
                return ActionTurret.selectedListItemsDummy.Count > 0;
            };
            smartTargetRange.Writer = (TerminalTurret, stringBuilder) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);
                stringBuilder.Clear();

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    stringBuilder.Append(Math.Round(ActionTurret.rangeStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id]));
                }
            };
            smartTargetRange.Title = MyStringId.GetOrCompute("Range");
            smartTargetRange.Tooltip = MyStringId.GetOrCompute("The maximum range at which the selected type is targeted, \nbasically the same as the vanilla setting.");
            smartTargetRange.SupportsMultipleBlocks = false;
            //The max range is set for each turret when the control is generated, so that it can be diffrent for diffrent turrets.
            smartTargetRange.SetLimits(0, 0);
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(smartTargetRange);
            #endregion

            #region TargetSmallGridsSwitch
            targetSmallGrids.Getter = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    return ActionTurret.targetSmallGridsStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id];
                }
                return false;
            };
            targetSmallGrids.Setter = (TerminalTurret, NewState) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    ActionTurret.targetSmallGridsStateDictionaryDummy[(ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id] = NewState;
                }
            };
            targetSmallGrids.Enabled = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return false;
                }
                return ActionTurret.selectedListItemsDummy.Count > 0;
            };
            targetSmallGrids.Title = MyStringId.GetOrCompute("Target Small Grids");
            targetSmallGrids.Tooltip = MyStringId.GetOrCompute("Whether or not to target small grids for the selected type, \nbasically the same as the vanilla setting. \nHas no effect on non-block target types.");
            targetSmallGrids.SupportsMultipleBlocks = false;
            targetSmallGrids.OffText = MyStringId.GetOrCompute("Off");
            targetSmallGrids.OnText = MyStringId.GetOrCompute("On");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(targetSmallGrids);
            #endregion

            #region TargetLargeGridsSwitch
            targetLargeGrids.Getter = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    return ActionTurret.targetLargeGridsStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id];
                }
                return false;
            };
            targetLargeGrids.Setter = (TerminalTurret, NewState) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    ActionTurret.targetLargeGridsStateDictionaryDummy[(ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id] = NewState;
                }
            };
            targetLargeGrids.Enabled = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return false;
                }
                return ActionTurret.selectedListItemsDummy.Count > 0;
            };
            targetLargeGrids.Title = MyStringId.GetOrCompute("Target Large Grids");
            targetLargeGrids.Tooltip = MyStringId.GetOrCompute("Whether or not to target large grids for the selected type, \nbasically the same as the vanilla setting. \nHas no effect on non-block target types.");
            targetLargeGrids.SupportsMultipleBlocks = false;
            targetLargeGrids.OffText = MyStringId.GetOrCompute("Off");
            targetLargeGrids.OnText = MyStringId.GetOrCompute("On");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(targetLargeGrids);
            #endregion

            #region TargetStationsSwitch
            targetStations.Getter = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    return ActionTurret.targetStationsStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id];
                }
                return false;
            };
            targetStations.Setter = (TerminalTurret, NewState) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    ActionTurret.targetStationsStateDictionaryDummy[(ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id] = NewState;
                }
            };
            targetStations.Enabled = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return false;
                }
                return ActionTurret.selectedListItemsDummy.Count > 0;
            };
            targetStations.Title = MyStringId.GetOrCompute("Target Stations");
            targetStations.Tooltip = MyStringId.GetOrCompute("Whether or not to target stations for the selected type, \nbasically the same as the vanilla setting. \nHas no effect on non-block target types.");
            targetStations.SupportsMultipleBlocks = false;
            targetStations.OffText = MyStringId.GetOrCompute("Off");
            targetStations.OnText = MyStringId.GetOrCompute("On");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(targetStations);
            #endregion

            #region TargetNeutralsSwitch
            targetNeutrals.Getter = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    return ActionTurret.targetNeutralsStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id];
                }
                return false;
            };
            targetNeutrals.Setter = (TerminalTurret, NewState) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    ActionTurret.targetNeutralsStateDictionaryDummy[(ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id] = NewState;
                }
            };
            targetNeutrals.Enabled = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return false;
                }
                return ActionTurret.selectedListItemsDummy.Count > 0;
            };
            targetNeutrals.Title = MyStringId.GetOrCompute("Target Neutrals");
            targetNeutrals.Tooltip = MyStringId.GetOrCompute("Whether or not to target neutrals for the selected type, \nbasically the same as the vanilla setting.");
            targetNeutrals.SupportsMultipleBlocks = false;
            targetNeutrals.OffText = MyStringId.GetOrCompute("Off");
            targetNeutrals.OnText = MyStringId.GetOrCompute("On");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(targetNeutrals);
            #endregion

            #region MinimumGridSizeSlider
            minimumGridSize.Getter = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    return (float)Math.Floor(ActionTurret.minimumGridSizeStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id]);
                }
                return 0;
            };
            minimumGridSize.Setter = (TerminalTurret, NewState) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    ActionTurret.minimumGridSizeStateDictionaryDummy[(ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id] = (float)Math.Floor(NewState);
                }
            };
            minimumGridSize.Enabled = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return false;
                }
                return ActionTurret.selectedListItemsDummy.Count > 0;
            };
            minimumGridSize.Writer = (TerminalTurret, stringBuilder) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);
                stringBuilder.Clear();

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    stringBuilder.Append(Math.Floor(ActionTurret.minimumGridSizeStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id]));
                }
            };
            minimumGridSize.Title = MyStringId.GetOrCompute("Minimum Target Grid Size");
            minimumGridSize.Tooltip = MyStringId.GetOrCompute("The minimum size of the target grid when targeting for the selected type. \nHas no effect on non-block target types.");
            minimumGridSize.SupportsMultipleBlocks = false;
            minimumGridSize.SetLimits(0, 100);
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(minimumGridSize);
            #endregion

            #region ObstacleToleranceSlider
            obstacleTolerance.Getter = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    return (float)Math.Floor(ActionTurret.obstacleToleranceStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id]);
                }
                return 0;
            };
            obstacleTolerance.Setter = (TerminalTurret, NewState) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    ActionTurret.obstacleToleranceStateDictionaryDummy[(ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id] = (float)Math.Floor(NewState);
                }
            };
            obstacleTolerance.Enabled = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return false;
                }
                return ActionTurret.selectedListItemsDummy.Count > 0;
            };
            obstacleTolerance.Writer = (TerminalTurret, stringBuilder) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);
                stringBuilder.Clear();

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    int value = (int)Math.Floor(ActionTurret.obstacleToleranceStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id]);

                    if (value == 31)
                    {
                        stringBuilder.Append("Infinite");
                    }
                    else
                    {
                        stringBuilder.Append(value);
                    }
                }
            };
            obstacleTolerance.Title = MyStringId.GetOrCompute("Obstacle Block Tolerance");
            obstacleTolerance.Tooltip = MyStringId.GetOrCompute("The maximum number of blocks on the target grid the turret can \nshoot through to reach the target block when targeting for the selected type. \nSet to max for infinite. \nHas no effect on non-block target types.");
            obstacleTolerance.SupportsMultipleBlocks = false;
            obstacleTolerance.SetLimits(0, 31);
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(obstacleTolerance);
            #endregion

            #region ThroughFriendliesSwitch
            throughFriendlies.Getter = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    return ActionTurret.throughFriendliesStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id];
                }
                return false;
            };
            throughFriendlies.Setter = (TerminalTurret, NewState) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    ActionTurret.throughFriendliesStateDictionaryDummy[(ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id] = NewState;
                }
            };
            throughFriendlies.Enabled = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return false;
                }
                return ActionTurret.selectedListItemsDummy.Count > 0;
            };
            throughFriendlies.Title = MyStringId.GetOrCompute("Fire Through Friendlies");
            throughFriendlies.Tooltip = MyStringId.GetOrCompute("Whether or not to fire through friendly grids to reach a target of the selected type. \nDoes not include own grid.");
            throughFriendlies.SupportsMultipleBlocks = false;
            throughFriendlies.OffText = MyStringId.GetOrCompute("Off");
            throughFriendlies.OnText = MyStringId.GetOrCompute("On");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(throughFriendlies);
            #endregion

            #region ThroughNeutralsSwitch
            throughNeutrals.Getter = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    return ActionTurret.throughNeutralsStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id];
                }
                return false;
            };
            throughNeutrals.Setter = (TerminalTurret, NewState) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    ActionTurret.throughNeutralsStateDictionaryDummy[(ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id] = NewState;
                }
            };
            throughNeutrals.Enabled = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return false;
                }
                return ActionTurret.selectedListItemsDummy.Count > 0;
            };
            throughNeutrals.Title = MyStringId.GetOrCompute("Fire Through Neutrals");
            throughNeutrals.Tooltip = MyStringId.GetOrCompute("Whether or not to fire through neutral grids to reach a target of the selected type.");
            throughNeutrals.SupportsMultipleBlocks = false;
            throughNeutrals.OffText = MyStringId.GetOrCompute("Off");
            throughNeutrals.OnText = MyStringId.GetOrCompute("On");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(throughNeutrals);
            #endregion

            #region ThroughHostilesSwitch
            throughHostiles.Getter = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret != null && ActionTurret.selectedListItemsDummy.Count > 0)
                {
                    return ActionTurret.throughHostilesStateDictionaryDummy[(ActionTurret.selectedListItemsDummy.First().UserData as ListBoxItemData).id];
                }
                return false;
            };
            throughHostiles.Setter = (TerminalTurret, NewState) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                for (int i = 0; i < ActionTurret.selectedListItemsDummy.Count; i++)
                {
                    ActionTurret.throughHostilesStateDictionaryDummy[(ActionTurret.selectedListItemsDummy[i].UserData as ListBoxItemData).id] = NewState;
                }
            };
            throughHostiles.Enabled = (TerminalTurret) => {
                SmartTurret ActionTurret = getSmartTurret(TerminalTurret);

                if (ActionTurret == null)
                {
                    return false;
                }
                return ActionTurret.selectedListItemsDummy.Count > 0;
            };
            throughHostiles.Title = MyStringId.GetOrCompute("Fire Through Hostiles");
            throughHostiles.Tooltip = MyStringId.GetOrCompute("Whether or not to fire through hostile grids to reach a target of the selected type.\nDoes not include target grid (use 'Obstacle Block Tolerance' for that).");
            throughHostiles.SupportsMultipleBlocks = false;
            throughHostiles.OffText = MyStringId.GetOrCompute("Off");
            throughHostiles.OnText = MyStringId.GetOrCompute("On");
            MyAPIGateway.TerminalControls.AddControl<IMyLargeTurretBase>(throughHostiles);
            #endregion
        }
    }
}