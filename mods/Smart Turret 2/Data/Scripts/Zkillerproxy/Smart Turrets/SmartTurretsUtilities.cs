using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.Definitions;
using System.IO;

namespace Zkillerproxy.SmartTurretMod
{
    public static class SmartTurretUtilities
    {
        private static bool debugMode = false;
        public static readonly Guid storageGUID = new Guid("608b9eeb-e108-4b32-a723-ea632296e4bf");
        public static ushort settingsReportSend = 4560;
        public static ushort settingsReportRecieve = 4561;

        public static ushort getTypeDictionaryKey(IMyEntity target)
        {
            if (target is IMyLargeTurretBase)
                return 01;

            if (target is IMySmallGatlingGun || target is IMySmallMissileLauncher || target is IMySmallMissileLauncherReload)
                return 02;

            if (target is IMyWarhead)
                return 03;

            if (target is IMyRemoteControl || target is IMyCockpit)
                return 04;

            if (target is IMySensorBlock)
                return 05;

            if (target is IMyCameraBlock)
                return 06;

            if (target is IMyPowerProducer)
                return 07;

            if (target is IMyJumpDrive)
                return 08;

            if (target is IMyProjector)
                return 09;

            if (target is IMyShipToolBase || target is IMyShipDrill)
                return 10;

            if (target is IMyProgrammableBlock)
                return 11;

            if (target is IMyTimerBlock)
                return 12;

            /*if (target is IMyThrust)
                return 13;*/

            if (target is IMyGyro)
                return 14;

            if (target is IMyRadioAntenna || target is IMyLaserAntenna || target is IMyBeacon)
                return 15;

            if (target is IMyCryoChamber || target is IMyMedicalRoom)
                return 16;

            if (target is IMyGasGenerator)
                return 17;

            if (target is IMyGasTank)
                return 18;

            if (target is IMyGravityGeneratorBase)
                return 19;

            if (target is IMyArtificialMassBlock)
                return 20;

            if (target is IMyParachute)
                return 21;

            if (target is IMyLandingGear)
                return 22;

            if (target is IMyOreDetector)
                return 23;
            
            if (target is IMyMotorBase)
                return 24;
            
            if (target is IMyPistonBase)
                return 25;

            if (target is IMyShipMergeBlock)
                return 26;

            if (target is IMyShipConnector)
                return 27;

            if (target is IMyProductionBlock)
                return 28;

            if (target is IMyDoor)
                return 29;

            if (target is IMyCargoContainer)
                return 30;

            /*if (target is IMyCharacter && ((IMyCharacter)target).IsPlayer && !((IMyCharacter)target).IsBot)
                return 31;*/

            /*if (target is IMyCharacter)
                return 32;*/

            /*if (target is IMyMeteor)
                return 33;*/

            if (target is IMyDecoy)
                return 35;

            if (target is IMyFunctionalBlock)
                return 34;

            //Default
            //log("An entity has failed to have a type and has been ignored: " + Target.GetType());
            return 00;
        }

        public static void saveTurretData(IMyEntity turretEnitiy, bool needsSync)
        {
            SmartTurret turret = turretEnitiy.GameLogic.GetAs<SmartTurret>();

            if (turretEnitiy == null)
            {
                //Log("Turret could not be saved, does not exist!");
                return;
            }

            cloneFromDummies(turret);
            SmartTurretSaveData turretSaveData = new SmartTurretSaveData(turret);
            string saveData = MyAPIGateway.Utilities.SerializeToXML(turretSaveData);

            //Log("Saving Turret Config...");

            if (turretEnitiy.Storage == null)
            {
                turretEnitiy.Storage = new MyModStorageComponent();
                turretEnitiy.Storage[storageGUID] = saveData;

                if (needsSync)
                {
                    syncSettingsToServer(turretEnitiy, turretSaveData);
                }

                //Log("Turret Config Saved.");
            }
            else
            {
                turretEnitiy.Storage[storageGUID] = saveData;

                if (needsSync)
                {
                    syncSettingsToServer(turretEnitiy, turretSaveData);
                }

                //Log("Turret Config Saved.");
            }
        }

        public static void loadTurretData(IMyEntity turretEnitiy)
        {
            SmartTurret turret = turretEnitiy.GameLogic.GetAs<SmartTurret>();
            string output;

            if (turretEnitiy != null)
            {
                //Log("Loading Turret Config...");
                if (turretEnitiy.Storage == null)
                {
                    //Log("Turret config not found, using default.");
                }
                else if (turretEnitiy.Storage.TryGetValue(storageGUID, out output) == false)
                {
                    //Log("Turret config not asigned a value, using default.");
                }
                else
                {
                    bool errorOccurred = false;
                    SmartTurretSaveData saveData = null;

                    try
                    {
                        saveData = MyAPIGateway.Utilities.SerializeFromXML<SmartTurretSaveData>(output);
                    }
                    catch (Exception)
                    {
                        errorOccurred = true;
                    }

                    if (errorOccurred == false)
                    {
                        turret.smartTargetingSwitchState = saveData.smartTargetingSwitchState;
                        foreach(ListBoxItemData data in saveData.targetTypesListItems)
                        {
                            MyTerminalControlListBoxItem item = new ListBoxItemDefaultGenerator().listBoxItemDefaultList.Find((x) => { return (x.UserData as ListBoxItemData).id == data.id; });

                            if (item != null)
                            {
                                if ((item.UserData as ListBoxItemData).enabledState != data.enabledState)
                                {
                                    (item.UserData as ListBoxItemData).enabledState = data.enabledState;

                                    if (item.Text.String[1] == '+')
                                    {
                                        item.Text = MyStringId.GetOrCompute(item.Text.String.Replace('+', '-'));
                                    }
                                    else
                                    {
                                        item.Text = MyStringId.GetOrCompute(item.Text.String.Replace('-', '+'));
                                    }
                                }

                                turret.targetTypesListItems.Add(item);
                            }
                        }
                        turret.rangeStateDictionary = findListBoxItemsFloat(saveData.rangeStateDictionaryKey, saveData.rangeStateDictionaryValue);
                        turret.targetSmallGridsStateDictionary = findListBoxItemsBool(saveData.targetSmallGridsStateDictionaryKey, saveData.targetSmallGridsStateDictionaryValue);
                        turret.targetLargeGridsStateDictionary = findListBoxItemsBool(saveData.targetLargeGridsStateDictionaryKey, saveData.targetLargeGridsStateDictionaryValue);
                        turret.targetStationsStateDictionary = findListBoxItemsBool(saveData.targetStationsStateDictionaryKey, saveData.targetStationsStateDictionaryValue);
                        turret.targetNeutralsStateDictionary = findListBoxItemsBool(saveData.targetNeutralsStateDictionaryKey, saveData.targetNeutralsStateDictionaryValue);
                        turret.minimumGridSizeStateDictionary = findListBoxItemsFloat(saveData.minimumGridSizeStateDictionaryKey, saveData.minimumGridSizeStateDictionaryValue);
                        turret.obstacleToleranceStateDictionary = findListBoxItemsFloat(saveData.obstacleToleranceStateDictionaryKey, saveData.obstacleToleranceStateDictionaryValue);
                        turret.throughFriendliesStateDictionary = findListBoxItemsBool(saveData.throughFriendliesStateDictionaryKey, saveData.throughFriendliesStateDictionaryValue);
                        turret.throughNeutralsStateDictionary = findListBoxItemsBool(saveData.throughNeutralsStateDictionaryKey, saveData.throughNeutralsStateDictionaryValue);
                        turret.throughHostilesStateDictionary = findListBoxItemsBool(saveData.throughHostilesStateDictionaryKey, saveData.throughHostilesStateDictionaryValue);

                        cloneToDummies(turret);
                        //Log("Turret config loaded.");
                    }
                    else
                    {
                        //Log("Turret config failed to read as XML, using default.");
                    }
                }
            }
        }

        public static void saveTurretPresent(IMyTerminalBlock terminalTurret, IMyTerminalControlTextbox presentName)
        {
            SmartTurret actionTurret = getSmartTurret(terminalTurret);

            //Check for Present File Name
            if (actionTurret.presentNameStringBuilder.Length == 0)
            {
                actionTurret.presentNameStringBuilder = new StringBuilder("Not Saved, Type A Name Here!");
                presentName.UpdateVisual();
                return;
            }

            //Check for a valid File Name
            if (actionTurret.presentNameStringBuilder.ToString().IndexOfAny(Path.GetInvalidFileNameChars()) > 0)
            {
                actionTurret.presentNameStringBuilder = new StringBuilder("Not Saved, Invalid File Name!");
                presentName.UpdateVisual();
                return;
            }

            //Check for Present File
            if (MyAPIGateway.Utilities.FileExistsInLocalStorage("UserPresent_" + actionTurret.presentNameStringBuilder.ToString() + ".xml", typeof(SmartTurretSaveData)))
            {
                actionTurret.presentNameStringBuilder = new StringBuilder("Not Saved, Name Already Taken!");
                presentName.UpdateVisual();
                return;
            }

            //Save present File
            using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("UserPresent_" + actionTurret.presentNameStringBuilder.ToString() + ".xml", typeof(SmartTurretSaveData)))
            {
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(new SmartTurretSaveData(actionTurret)));
                writer.Dispose();
            }

            editPresentNameList(true, actionTurret.presentNameStringBuilder.ToString());

            actionTurret.presentNameStringBuilder = new StringBuilder("Present Saved Successfully!");
            presentName.UpdateVisual();
        }

        public static void loadTurretPresent(IMyTerminalBlock terminalTurret, IMyTerminalControlTextbox presentNameControl, string presentNameString)
        {
            SmartTurret actionTurret = getSmartTurret(terminalTurret);

            if (MyAPIGateway.Utilities.FileExistsInLocalStorage("UserPresent_" + presentNameString + ".xml", typeof(SmartTurretSaveData)))
            {
                try
                {
                    SmartTurretSaveData saveData;

                    using (TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("UserPresent_" + presentNameString + ".xml", typeof(SmartTurretSaveData)))
                    {
                        saveData = MyAPIGateway.Utilities.SerializeFromXML<SmartTurretSaveData>(reader.ReadToEnd());
                        reader.Dispose();
                    }

                    //Load smart targeting switch and target types list.
                    actionTurret.smartTargetingSwitchState = saveData.smartTargetingSwitchState;
                    actionTurret.targetTypesListItems.Clear();
                    List<MyTerminalControlListBoxItem> WorkingList = new ListBoxItemDefaultGenerator().listBoxItemDefaultList;

                    foreach (ListBoxItemData data in saveData.targetTypesListItems)
                    {
                        MyTerminalControlListBoxItem item = WorkingList.Find((x) => { return (x.UserData as ListBoxItemData).id == data.id; });

                        if ((item.UserData as ListBoxItemData).enabledState != data.enabledState)
                        {
                            (item.UserData as ListBoxItemData).enabledState = data.enabledState;

                            if (data.enabledState == true)
                            {
                                item.Text = MyStringId.GetOrCompute(item.Text.String.Replace('-', '+'));
                            }
                            else
                            {
                                item.Text = MyStringId.GetOrCompute(item.Text.String.Replace('+', '-'));
                            }
                        }

                        actionTurret.targetTypesListItems.Add(item);
                    }

                    //Make sure the present doesent load turret ranges that are above the turrets max ranges.
                    for (int i = 0; i < saveData.rangeStateDictionaryValue.Count; i++)
                    {
                        float maxRange = getTurretMaxRange(terminalTurret);

                        if (saveData.rangeStateDictionaryValue[i] > maxRange)
                        {
                            saveData.rangeStateDictionaryValue[i] = maxRange;
                        }
                    }

                    //Load other things.
                    actionTurret.rangeStateDictionary = findListBoxItemsFloat(saveData.rangeStateDictionaryKey, saveData.rangeStateDictionaryValue);
                    actionTurret.targetSmallGridsStateDictionary = findListBoxItemsBool(saveData.targetSmallGridsStateDictionaryKey, saveData.targetSmallGridsStateDictionaryValue);
                    actionTurret.targetLargeGridsStateDictionary = findListBoxItemsBool(saveData.targetLargeGridsStateDictionaryKey, saveData.targetLargeGridsStateDictionaryValue);
                    actionTurret.targetStationsStateDictionary = findListBoxItemsBool(saveData.targetStationsStateDictionaryKey, saveData.targetStationsStateDictionaryValue);
                    actionTurret.targetNeutralsStateDictionary = findListBoxItemsBool(saveData.targetNeutralsStateDictionaryKey, saveData.targetNeutralsStateDictionaryValue);
                    actionTurret.minimumGridSizeStateDictionary = findListBoxItemsFloat(saveData.minimumGridSizeStateDictionaryKey, saveData.minimumGridSizeStateDictionaryValue);
                    actionTurret.obstacleToleranceStateDictionary = findListBoxItemsFloat(saveData.obstacleToleranceStateDictionaryKey, saveData.obstacleToleranceStateDictionaryValue);
                    actionTurret.throughFriendliesStateDictionary = findListBoxItemsBool(saveData.throughFriendliesStateDictionaryKey, saveData.throughFriendliesStateDictionaryValue);
                    actionTurret.throughNeutralsStateDictionary = findListBoxItemsBool(saveData.throughNeutralsStateDictionaryKey, saveData.throughNeutralsStateDictionaryValue);
                    actionTurret.throughHostilesStateDictionary = findListBoxItemsBool(saveData.throughHostilesStateDictionaryKey, saveData.throughHostilesStateDictionaryValue);

                    actionTurret.presentNameStringBuilder = new StringBuilder("Present Loaded Successfully!");

                    cloneToDummies(actionTurret);
                    saveTurretData(terminalTurret, true);
                }
                catch (Exception)
                {
                    actionTurret.presentNameStringBuilder = new StringBuilder("Not Loaded, Error Occured!");
                    presentNameControl.UpdateVisual();
                    return;
                }
            }
            else
            {
                actionTurret.presentNameStringBuilder = new StringBuilder("Present Not Found!");
                presentNameControl.UpdateVisual();
                return;
            }
        }

        public static void deleteTurretPresent(IMyTerminalBlock terminalTurret, IMyTerminalControlTextbox presentNameControl, string presentNameString)
        {
            SmartTurret actionTurret = getSmartTurret(terminalTurret);

            editPresentNameList(false, presentNameString);

            if (MyAPIGateway.Utilities.FileExistsInLocalStorage("UserPresent_" + presentNameString + ".xml", typeof(SmartTurretSaveData)))
            {
                //Remove old present File.
                MyAPIGateway.Utilities.DeleteFileInLocalStorage("UserPresent_" + presentNameString + ".xml", typeof(SmartTurretSaveData));
                actionTurret.presentNameStringBuilder = new StringBuilder("Present Deleted.");
                presentNameControl.UpdateVisual();
            }
            else
            {
                actionTurret.presentNameStringBuilder = new StringBuilder("Not Found! Removed from List.");
                presentNameControl.UpdateVisual();
            }
        }

        public static void editPresentNameList(bool addOrRemove, string name)
        {
            //Get PresentNames File, create a new one if none.
            PresentNames presentNames;

            if (MyAPIGateway.Utilities.FileExistsInLocalStorage("UserPresentNameList.xml", typeof(PresentNames)))
            {
                using (TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("UserPresentNameList.xml", typeof(PresentNames)))
                {
                    presentNames = MyAPIGateway.Utilities.SerializeFromXML<PresentNames>(reader.ReadToEnd());
                    reader.Dispose();
                }
            }
            else
            {
                presentNames = new PresentNames(new List<string>());
            }

            if (addOrRemove == true)
            {
                //Add New Present to PresentNames
                if (presentNames.presentNames.Contains(name) == false)
                {
                    presentNames.presentNames.Add(name);
                }
            }
            else
            {
                //Remove Present from PresentNames
                if (presentNames.presentNames.Contains(name) == true)
                {
                    presentNames.presentNames.Remove(name);
                }
            }

            //Rewrite PresentNames
            using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("UserPresentNameList.xml", typeof(PresentNames)))
            {
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(presentNames));
                writer.Dispose();
            }
        }

        public static void refreshTurretPresentList(IMyTerminalBlock terminalTurret)
        {
            SmartTurret actionTurret = getSmartTurret(terminalTurret);
            
            //Get PresentNames File, create a new one if none.
            PresentNames presentNames;
            if (MyAPIGateway.Utilities.FileExistsInLocalStorage("UserPresentNameList.xml", typeof(PresentNames)))
            {
                using (TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("UserPresentNameList.xml", typeof(PresentNames)))
                {
                    presentNames = MyAPIGateway.Utilities.SerializeFromXML<PresentNames>(reader.ReadToEnd());
                    reader.Dispose();
                }
            }
            else
            {
                presentNames = new PresentNames(new List<string>());
            }
            
            List<MyTerminalControlListBoxItem> Items = new List<MyTerminalControlListBoxItem>();
            
            foreach (string name in presentNames.presentNames)
            {
                Items.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(name), MyStringId.NullOrEmpty, null));
            }
            
            actionTurret.presentListContent = Items;
        }

        public static SmartTurret getSmartTurret(IMyTerminalBlock terminalTurret)
        {
            if (terminalTurret != null)
            {
                if (terminalTurret.GameLogic != null)
                {
                    return terminalTurret.GameLogic.GetAs<SmartTurret>() ?? null;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public static void disableVanillaControls(IMyTerminalBlock terminalTurret)
        {
            List<ITerminalAction> terminalActions = new List<ITerminalAction>();
            terminalTurret.GetActions(terminalActions);

            foreach (ITerminalAction action in terminalActions)
            {
                switch (action.Name.ToString())
                {
                    /*
                    case "Enable idle movement Off":
                        action.Apply(terminalTurret);
                        break;
                    */

                    case "Target meteors Off":
                        action.Apply(terminalTurret);
                        break;

                    case "Target missiles Off":
                        action.Apply(terminalTurret);
                        break;

                    case "Target small ships Off":
                        action.Apply(terminalTurret);
                        break;

                    case "Target large ships Off":
                        action.Apply(terminalTurret);
                        break;

                    case "Target characters Off":
                        action.Apply(terminalTurret);
                        break;

                    case "Target stations Off":
                        action.Apply(terminalTurret);
                        break;

                    case "Target neutrals Off":
                        action.Apply(terminalTurret);
                        break;
                }
            }
        }

        public static void updateVanillaControls(IMyTerminalBlock terminalTurret)
        {
            List<ITerminalProperty> terminalControls = new List<ITerminalProperty>();
            terminalTurret.GetProperties(terminalControls);
            
            foreach (ITerminalProperty property in terminalControls)
            {
                if (property as IMyTerminalControlOnOffSwitch != null)
                {
                    IMyTerminalControlOnOffSwitch switchControl = property as IMyTerminalControlOnOffSwitch;

                    if (switchControl.Title.String.StartsWith("BlockPropertyTitle_"))
                    {
                        switchControl.UpdateVisual();
                    }
                }
                else if (property as IMyTerminalControlSlider != null)
                {
                    IMyTerminalControlSlider sliderControl = property as IMyTerminalControlSlider;

                    if (sliderControl.Title.String == "BlockPropertyTitle_LargeTurretRadius")
                    {
                        sliderControl.UpdateVisual();
                    }
                }
            }
        }

        public static void applyVanillaControlDisabler(SmartTurret turret)
        {
            List<ITerminalProperty> terminalControls = new List<ITerminalProperty>();
            try
            {
                (turret.Entity as IMyTerminalBlock).GetProperties(terminalControls);
            }
            catch (InvalidOperationException)
            {
                //Collection was modified, try again.
                applyVanillaControlDisabler(turret);
            }
            

            for (int i = 0; i < terminalControls.Count; i++)
            {
                if (!(i < terminalControls.Count))
                {
                    //If the collection has been modified, stop.
                    return;
                }

                if (terminalControls[i] is IMyTerminalControlOnOffSwitch)
                {
                    IMyTerminalControlOnOffSwitch switchControl = terminalControls[i] as IMyTerminalControlOnOffSwitch;

                    if (switchControl.Title.String.StartsWith("BlockPropertyTitle_") && switchControl.Title.String != "BlockPropertyTitle_LargeTurretEnableTurretIdleMovement")
                    {
                        switchControl.Enabled = (TerminalTurret) => { SmartTurret Turret = getSmartTurret(TerminalTurret); if (Turret != null && Turret.smartTargetingSwitchState) { return false; } return true; };
                        switchControl.UpdateVisual();
                    }
                }
                else if (terminalControls[i] is IMyTerminalControlSlider)
                {
                    IMyTerminalControlSlider sliderControl = terminalControls[i] as IMyTerminalControlSlider;

                    if (sliderControl.Title.String == "BlockPropertyTitle_LargeTurretRadius")
                    {
                        sliderControl.Enabled = (TerminalTurret) => { SmartTurret Turret = getSmartTurret(TerminalTurret); if (Turret != null && Turret.smartTargetingSwitchState) { return false; } return true; };
                        sliderControl.UpdateVisual();
                    }
                }
            }
        }

        public static void disableSpecificControls(IMyTerminalBlock terminalTurret)
        {
            List<ITerminalProperty> terminalControls = new List<ITerminalProperty>();
            terminalTurret.GetProperties(terminalControls);

            foreach (ITerminalProperty property in terminalControls)
            {
                if (property as IMyTerminalControlOnOffSwitch != null)
                {
                    IMyTerminalControlOnOffSwitch switchControl = property as IMyTerminalControlOnOffSwitch;

                    if (switchControl.Title.String.StartsWith("STS_"))
                    {
                        switchControl.Enabled = (value) => { return false; };
                        switchControl.UpdateVisual();
                    }
                }
                else if (property as IMyTerminalControlSlider != null)
                {
                    IMyTerminalControlSlider sliderControl = property as IMyTerminalControlSlider;

                    if (sliderControl.Title.String.StartsWith("STS_"))
                    {
                        sliderControl.Enabled = (value) => { return false; };
                        sliderControl.UpdateVisual();
                    }
                }
            }
        }

        public static void enableSpecificControls(IMyTerminalBlock terminalTurret)
        {
            List<ITerminalProperty> terminalControls = new List<ITerminalProperty>();
            terminalTurret.GetProperties(terminalControls);

            foreach (ITerminalProperty property in terminalControls)
            {
                if (property as IMyTerminalControlOnOffSwitch != null)
                {
                    IMyTerminalControlOnOffSwitch switchControl = property as IMyTerminalControlOnOffSwitch;

                    if (switchControl.Title.String.StartsWith("STS_"))
                    {
                        switchControl.Enabled = (value) => { return true; };
                        switchControl.UpdateVisual();
                    }
                }
                else if (property as IMyTerminalControlSlider != null)
                {
                    IMyTerminalControlSlider sliderControl = property as IMyTerminalControlSlider;

                    if (sliderControl.Title.String.StartsWith("STS_"))
                    {
                        sliderControl.Enabled = (value) => { return true; };
                        sliderControl.UpdateVisual();
                    }
                }
            }
        }

        public static float getTurretMaxRange(IMyTerminalBlock terminalTurret)
        {
            return (MyDefinitionManager.Static.GetCubeBlockDefinition((terminalTurret as IMyLargeTurretBase).BlockDefinition) as MyLargeTurretBaseDefinition).MaxRangeMeters;
        }

        public static void handleSettingsRecieve(byte[] data)
        {
            bool errorOccurred = false;
            SmartTurretSyncData syncData = null;

            try
            {
                syncData = MyAPIGateway.Utilities.SerializeFromXML<SmartTurretSyncData>(MyAPIGateway.Utilities.SerializeFromBinary<string>(data));
            }
            catch (Exception)
            {
                errorOccurred = true;
            }

            if (errorOccurred == false)
            {
                syncSettingsFromServer(syncData.turretEntityId, syncData.saveData);
            }
        }

        public static void syncSettingsToServer(IMyEntity turretEnitiy, SmartTurretSaveData saveData)
        {
            SmartTurretSyncData syncData = new SmartTurretSyncData(saveData, turretEnitiy.EntityId);

            byte[] byteArraySyncData = MyAPIGateway.Utilities.SerializeToBinary(MyAPIGateway.Utilities.SerializeToXML(syncData));
            
            MyAPIGateway.Multiplayer.SendMessageToServer(settingsReportSend, byteArraySyncData);
        }

        public static void syncSettingsFromServer(long turretId, SmartTurretSaveData saveData)
        {
            if (MyAPIGateway.Entities.EntityExists(turretId))
            {
                IMyEntity entity = MyAPIGateway.Entities.GetEntityById(turretId);

                if (entity is IMyTerminalBlock)
                {
                    IMyTerminalBlock terminalTurret = entity as IMyTerminalBlock;
                    SmartTurret actionTurret = getSmartTurret(entity as IMyTerminalBlock);

                    if (actionTurret != null)
                    {
                        //Load smart targeting switch and target types list.
                        actionTurret.smartTargetingSwitchState = saveData.smartTargetingSwitchState;
                        actionTurret.targetTypesListItems.Clear();
                        List<MyTerminalControlListBoxItem> WorkingList = new ListBoxItemDefaultGenerator().listBoxItemDefaultList;

                        foreach (ListBoxItemData data in saveData.targetTypesListItems)
                        {
                            MyTerminalControlListBoxItem item = WorkingList.Find((x) => { return (x.UserData as ListBoxItemData).id == data.id; });

                            if ((item.UserData as ListBoxItemData).enabledState != data.enabledState)
                            {
                                (item.UserData as ListBoxItemData).enabledState = data.enabledState;

                                if (data.enabledState == true)
                                {
                                    item.Text = MyStringId.GetOrCompute(item.Text.String.Replace('-', '+'));
                                }
                                else
                                {
                                    item.Text = MyStringId.GetOrCompute(item.Text.String.Replace('+', '-'));
                                }
                            }

                            actionTurret.targetTypesListItems.Add(item);
                        }

                        //Make sure the present doesent load turret ranges that are above the turrets max ranges.
                        for (int i = 0; i < saveData.rangeStateDictionaryValue.Count; i++)
                        {
                            float maxRange = getTurretMaxRange(terminalTurret);

                            if (saveData.rangeStateDictionaryValue[i] > maxRange)
                            {
                                saveData.rangeStateDictionaryValue[i] = maxRange;
                            }
                        }

                        //Load other things.
                        actionTurret.rangeStateDictionary = findListBoxItemsFloat(saveData.rangeStateDictionaryKey, saveData.rangeStateDictionaryValue);
                        actionTurret.targetSmallGridsStateDictionary = findListBoxItemsBool(saveData.targetSmallGridsStateDictionaryKey, saveData.targetSmallGridsStateDictionaryValue);
                        actionTurret.targetLargeGridsStateDictionary = findListBoxItemsBool(saveData.targetLargeGridsStateDictionaryKey, saveData.targetLargeGridsStateDictionaryValue);
                        actionTurret.targetStationsStateDictionary = findListBoxItemsBool(saveData.targetStationsStateDictionaryKey, saveData.targetStationsStateDictionaryValue);
                        actionTurret.targetNeutralsStateDictionary = findListBoxItemsBool(saveData.targetNeutralsStateDictionaryKey, saveData.targetNeutralsStateDictionaryValue);
                        actionTurret.minimumGridSizeStateDictionary = findListBoxItemsFloat(saveData.minimumGridSizeStateDictionaryKey, saveData.minimumGridSizeStateDictionaryValue);
                        actionTurret.obstacleToleranceStateDictionary = findListBoxItemsFloat(saveData.obstacleToleranceStateDictionaryKey, saveData.obstacleToleranceStateDictionaryValue);
                        actionTurret.throughFriendliesStateDictionary = findListBoxItemsBool(saveData.throughFriendliesStateDictionaryKey, saveData.throughFriendliesStateDictionaryValue);
                        actionTurret.throughNeutralsStateDictionary = findListBoxItemsBool(saveData.throughNeutralsStateDictionaryKey, saveData.throughNeutralsStateDictionaryValue);
                        actionTurret.throughHostilesStateDictionary = findListBoxItemsBool(saveData.throughHostilesStateDictionaryKey, saveData.throughHostilesStateDictionaryValue);

                        cloneToDummies(actionTurret);
                        saveTurretData(terminalTurret, false);
                    }
                }
            }
        }

        public static void updateSTControlsVisual(IMyTerminalBlock terminalTurret)
        {
            List<IMyTerminalControl> controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<IMyLargeTurretBase>(out controls);

            foreach (IMyTerminalControl control in controls)
            {
                if (control.Id.StartsWith("ST_") && (control is IMyTerminalControlListbox || control is IMyTerminalControlOnOffSwitch || control is IMyTerminalControlSlider || control is IMyTerminalControlTextbox))
                {
                    control.UpdateVisual();
                }
            }
        }

        public static void controlModifier(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (block != null && controls != null && getSmartTurret(block) != null)
            {
                float maxRange = getTurretMaxRange(block);

                if (maxRange != null) //This should never be null, but just to be sure...
                {
                    IMyTerminalControlSlider rangeSlider = controls.Find((x) => { return x.Id == "ST_RangeSlider"; }) as IMyTerminalControlSlider;

                    if (rangeSlider != null)
                    {
                        rangeSlider.SetLimits(0, maxRange);
                    }
                }
            }
        }

        public static Dictionary<ushort, float> findListBoxItemsFloat(List<ushort> inputKey, List<float> inputValue)
        {
            Dictionary<ushort, float> output = new Dictionary<ushort, float>();

            for (int i = 0; i < inputKey.Count; i ++)
            {
                output.Add(inputKey[i], inputValue[i]);
            }

            return output;
        }

        public static Dictionary<ushort, bool> findListBoxItemsBool(List<ushort> inputKey, List<bool> inputValue)
        {
            Dictionary<ushort, bool> output = new Dictionary<ushort, bool>();

            for (int i = 0; i < inputKey.Count; i++)
            {
                output.Add(inputKey[i], inputValue[i]);
            }

            return output;
        }

        public static void cloneToDummies(SmartTurret actionTurret)
        {
            actionTurret.targetTypesListItemsDummy.Clear();

            List<MyTerminalControlListBoxItem> WorkingList = new ListBoxItemDefaultGenerator().listBoxItemDefaultList;
            List<ListBoxItemData> targetTypesListItems = new List<ListBoxItemData>();
            foreach (MyTerminalControlListBoxItem item in actionTurret.targetTypesListItems)
            {
                targetTypesListItems.Add(item.UserData as ListBoxItemData);
            }

            foreach (ListBoxItemData data in targetTypesListItems)
            {
                MyTerminalControlListBoxItem item = WorkingList.Find((x) => { return (x.UserData as ListBoxItemData).id == data.id; });

                if ((item.UserData as ListBoxItemData).enabledState != data.enabledState)
                {
                    (item.UserData as ListBoxItemData).enabledState = data.enabledState;

                    if (data.enabledState == true)
                    {
                        item.Text = MyStringId.GetOrCompute(item.Text.String.Replace('-', '+'));
                    }
                    else
                    {
                        item.Text = MyStringId.GetOrCompute(item.Text.String.Replace('+', '-'));
                    }
                }

                actionTurret.targetTypesListItemsDummy.Add(item);
            }

            actionTurret.selectedListItemsDummy = new List<MyTerminalControlListBoxItem>();
            actionTurret.rangeStateDictionaryDummy = new Dictionary<ushort, float>(actionTurret.rangeStateDictionary);
            actionTurret.targetSmallGridsStateDictionaryDummy = new Dictionary<ushort, bool>(actionTurret.targetSmallGridsStateDictionary);
            actionTurret.targetLargeGridsStateDictionaryDummy = new Dictionary<ushort, bool>(actionTurret.targetLargeGridsStateDictionary);
            actionTurret.targetStationsStateDictionaryDummy = new Dictionary<ushort, bool>(actionTurret.targetStationsStateDictionary);
            actionTurret.targetNeutralsStateDictionaryDummy = new Dictionary<ushort, bool>(actionTurret.targetNeutralsStateDictionary);
            actionTurret.minimumGridSizeStateDictionaryDummy = new Dictionary<ushort, float>(actionTurret.minimumGridSizeStateDictionary);
            actionTurret.obstacleToleranceStateDictionaryDummy = new Dictionary<ushort, float>(actionTurret.obstacleToleranceStateDictionary);
            actionTurret.throughFriendliesStateDictionaryDummy = new Dictionary<ushort, bool>(actionTurret.throughFriendliesStateDictionary);
            actionTurret.throughNeutralsStateDictionaryDummy = new Dictionary<ushort, bool>(actionTurret.throughNeutralsStateDictionary);
            actionTurret.throughHostilesStateDictionaryDummy = new Dictionary<ushort, bool>(actionTurret.throughHostilesStateDictionary);

            updateSTControlsVisual(actionTurret.Entity as IMyTerminalBlock);
        }

        public static void cloneFromDummies(SmartTurret actionTurret)
        {
            actionTurret.targetTypesListItems.Clear();

            List<MyTerminalControlListBoxItem> WorkingList = new ListBoxItemDefaultGenerator().listBoxItemDefaultList;
            List<ListBoxItemData> targetTypesListItems = new List<ListBoxItemData>();
            foreach (MyTerminalControlListBoxItem item in actionTurret.targetTypesListItemsDummy)
            {
                targetTypesListItems.Add(item.UserData as ListBoxItemData);
            }

            foreach (ListBoxItemData data in targetTypesListItems)
            {
                MyTerminalControlListBoxItem item = WorkingList.Find((x) => { return (x.UserData as ListBoxItemData).id == data.id; });

                if ((item.UserData as ListBoxItemData).enabledState != data.enabledState)
                {
                    (item.UserData as ListBoxItemData).enabledState = data.enabledState;

                    if (data.enabledState == true)
                    {
                        item.Text = MyStringId.GetOrCompute(item.Text.String.Replace('-', '+'));
                    }
                    else
                    {
                        item.Text = MyStringId.GetOrCompute(item.Text.String.Replace('+', '-'));
                    }
                }

                actionTurret.targetTypesListItems.Add(item);
            }

            actionTurret.selectedListItems = new List<MyTerminalControlListBoxItem>();
            actionTurret.rangeStateDictionary = new Dictionary<ushort, float>(actionTurret.rangeStateDictionaryDummy);
            actionTurret.targetSmallGridsStateDictionary = new Dictionary<ushort, bool>(actionTurret.targetSmallGridsStateDictionaryDummy);
            actionTurret.targetLargeGridsStateDictionary = new Dictionary<ushort, bool>(actionTurret.targetLargeGridsStateDictionaryDummy);
            actionTurret.targetStationsStateDictionary = new Dictionary<ushort, bool>(actionTurret.targetStationsStateDictionaryDummy);
            actionTurret.targetNeutralsStateDictionary = new Dictionary<ushort, bool>(actionTurret.targetNeutralsStateDictionaryDummy);
            actionTurret.minimumGridSizeStateDictionary = new Dictionary<ushort, float>(actionTurret.minimumGridSizeStateDictionaryDummy);
            actionTurret.obstacleToleranceStateDictionary = new Dictionary<ushort, float>(actionTurret.obstacleToleranceStateDictionaryDummy);
            actionTurret.throughFriendliesStateDictionary = new Dictionary<ushort, bool>(actionTurret.throughFriendliesStateDictionaryDummy);
            actionTurret.throughNeutralsStateDictionary = new Dictionary<ushort, bool>(actionTurret.throughNeutralsStateDictionaryDummy);
            actionTurret.throughHostilesStateDictionary = new Dictionary<ushort, bool>(actionTurret.throughHostilesStateDictionaryDummy);
        }

        public static void log(string input)
        {
            if (debugMode)
            {
                MyLog.Default.WriteLineAndConsole("Smart Turrets: " + input);

                MyAPIGateway.Utilities.ShowMessage("Smart Turrets", input);
            }
        }
    }

    public class ListBoxItemDefaultGenerator
    {
        public List<MyTerminalControlListBoxItem> listBoxItemDefaultList;

        public ListBoxItemDefaultGenerator()
        {
            listBoxItemDefaultList = new List<MyTerminalControlListBoxItem>()
            {
                //Some of these are commented out as they are not currently in use because of issues with Keens TrackTarget method, they can be safely enabled or dissabled from here but the length of the GUI list must be ajusted.
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(+) Turrets"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyLargeTurretBase'"), new ListBoxItemData(01, true)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(+) Fixed Weapons"), MyStringId.GetOrCompute("Target blocks that fall under 'IMySmallGatlingGun', 'IMySmallMissileLauncher' and 'IMySmallMissileLauncherReload'"), new ListBoxItemData(02, true)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(+) Warheads"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyWarhead'"), new ListBoxItemData(03, true)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(+) Control Blocks"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyRemoteControl' and 'IMyCockpit'"), new ListBoxItemData(04, true)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Sensors"), MyStringId.GetOrCompute("Target blocks that fall under 'IMySensorBlock'"), new ListBoxItemData(05, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Cameras"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyCameraBlock'"), new ListBoxItemData(06, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Power Blocks"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyPowerProducer'"), new ListBoxItemData(07, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Jump Drives"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyJumpDrive'"), new ListBoxItemData(08, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Projectors"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyProjector'"), new ListBoxItemData(09, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Ship Tools"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyShipToolBase' and 'IMyShipDrill'"), new ListBoxItemData(10, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Programmable Blocks"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyProgrammableBlock'"), new ListBoxItemData(11, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Timer Blocks"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyTimerBlock'"), new ListBoxItemData(12, false)),
                //new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Thrusters"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyThrust'"), new ListBoxItemData(13, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Gyroscopes"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyGyro'"), new ListBoxItemData(14, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Communication Blocks"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyRadioAntenna', 'IMyLaserAntenna' and 'IMyBeacon'"), new ListBoxItemData(15, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Medical Blocks"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyCryoChamber' and 'IMyMedicalRoom'"), new ListBoxItemData(16, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Oxygen Generators"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyGasGenerator'"), new ListBoxItemData(17, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Gas Tanks"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyGasTank'"), new ListBoxItemData(18, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Gravity Generators"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyGravityGeneratorBase'"), new ListBoxItemData(19, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Artificial Masses"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyArtificialMassBlock'"), new ListBoxItemData(20, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Parachute Hatches"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyParachute'"), new ListBoxItemData(21, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Landing Gears"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyLandingGear'"), new ListBoxItemData(22, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Ore Detectors"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyOreDetector'"), new ListBoxItemData(23, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Rotors"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyMotorBase'"), new ListBoxItemData(24, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Pistons"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyPistonBase'"), new ListBoxItemData(25, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Merge Blocks"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyShipMergeBlock'"), new ListBoxItemData(26, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Connectors"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyShipConnector'"), new ListBoxItemData(27, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Production Blocks"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyProductionBlock'"), new ListBoxItemData(28, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Doors"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyDoor'"), new ListBoxItemData(29, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Cargo Containers"), MyStringId.GetOrCompute("Target blocks that fall under 'IMyCargoContainer'"), new ListBoxItemData(30, false)),
                //new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Players"), MyStringId.GetOrCompute("Target players"), new ListBoxItemData(31, false)),
                //new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) NPCs"), MyStringId.GetOrCompute("Target non-player characters, like saberoids."), new ListBoxItemData(32, false)),
                //new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Meteors"), MyStringId.GetOrCompute("Target Meteors"), new ListBoxItemData(33, false)),
                new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("(-) Functional Blocks"), MyStringId.GetOrCompute("Target functional blocks that did not match any other type"), new ListBoxItemData(34, false))
            };
        }
    }

    public class SortSelectedItemsByIndex : IComparer<MyTerminalControlListBoxItem>
    {
        List<MyTerminalControlListBoxItem> targetTypesListItems;
        public int Compare(MyTerminalControlListBoxItem X, MyTerminalControlListBoxItem Y)
        {
            int indexX = targetTypesListItems.FindIndex(x => x.Text == X.Text);
            int indexY = targetTypesListItems.FindIndex(x => x.Text == Y.Text);

            if (indexX > indexY)
            {
                return 1;
            }
            else if (indexY > indexX)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }

        public SortSelectedItemsByIndex(List<MyTerminalControlListBoxItem> targetTypesListItems)
        {
            this.targetTypesListItems = targetTypesListItems;
        }
    }

    public class ListBoxItemData
    {
        public ushort id;
        public bool enabledState;

        public ListBoxItemData(ushort id, bool enabledState)
        {
            this.id = id;
            this.enabledState = enabledState;
        }

        public ListBoxItemData()
        {
            //Parameterless constructor for serialization.
        }
    }

    public class PresentNames
    {
        public List<string> presentNames;

        public PresentNames(List<string> presentNames)
        {
            this.presentNames = presentNames;
        }

        public PresentNames()
        {
            //Parameterless constructor for serialization.
        }
    }
}
