using System.Collections.Generic;
using VRage.ModAPI;

namespace Zkillerproxy.SmartTurretMod
{
    public class SmartTurretSaveData
    {
        public bool smartTargetingSwitchState;
        public List<ListBoxItemData> targetTypesListItems;
        public List<ushort> rangeStateDictionaryKey;
        public List<float> rangeStateDictionaryValue;
        public List<ushort> targetSmallGridsStateDictionaryKey;
        public List<bool> targetSmallGridsStateDictionaryValue;
        public List<ushort> targetLargeGridsStateDictionaryKey;
        public List<bool> targetLargeGridsStateDictionaryValue;
        public List<ushort> targetStationsStateDictionaryKey;
        public List<bool> targetStationsStateDictionaryValue;
        public List<ushort> targetNeutralsStateDictionaryKey;
        public List<bool> targetNeutralsStateDictionaryValue;
        public List<ushort> minimumGridSizeStateDictionaryKey;
        public List<float> minimumGridSizeStateDictionaryValue;
        public List<ushort> obstacleToleranceStateDictionaryKey;
        public List<float> obstacleToleranceStateDictionaryValue;
        public List<ushort> throughFriendliesStateDictionaryKey;
        public List<bool> throughFriendliesStateDictionaryValue;
        public List<ushort> throughNeutralsStateDictionaryKey;
        public List<bool> throughNeutralsStateDictionaryValue;
        public List<ushort> throughHostilesStateDictionaryKey;
        public List<bool> throughHostilesStateDictionaryValue;

        public SmartTurretSaveData(SmartTurret turret)
        {
            smartTargetingSwitchState = turret.smartTargetingSwitchState;

            targetTypesListItems = new List<ListBoxItemData>();
            foreach (MyTerminalControlListBoxItem item in turret.targetTypesListItems)
            {
                targetTypesListItems.Add(item.UserData as ListBoxItemData);
            }

            rangeStateDictionaryKey = new List<ushort>();
            rangeStateDictionaryValue = new List<float>();
            foreach (KeyValuePair<ushort, float> item in turret.rangeStateDictionary)
            {
                rangeStateDictionaryKey.Add(item.Key);
                rangeStateDictionaryValue.Add(item.Value);
            }

            targetSmallGridsStateDictionaryKey = new List<ushort>();
            targetSmallGridsStateDictionaryValue = new List<bool>();
            foreach (KeyValuePair<ushort, bool> item in turret.targetSmallGridsStateDictionary)
            {
                targetSmallGridsStateDictionaryKey.Add(item.Key);
                targetSmallGridsStateDictionaryValue.Add(item.Value);
            }

            targetLargeGridsStateDictionaryKey = new List<ushort>();
            targetLargeGridsStateDictionaryValue = new List<bool>();
            foreach (KeyValuePair<ushort, bool> item in turret.targetLargeGridsStateDictionary)
            {
                targetLargeGridsStateDictionaryKey.Add(item.Key);
                targetLargeGridsStateDictionaryValue.Add(item.Value);
            }

            targetStationsStateDictionaryKey = new List<ushort>();
            targetStationsStateDictionaryValue = new List<bool>();
            foreach (KeyValuePair<ushort, bool> item in turret.targetStationsStateDictionary)
            {
                targetStationsStateDictionaryKey.Add(item.Key);
                targetStationsStateDictionaryValue.Add(item.Value);
            }

            targetNeutralsStateDictionaryKey = new List<ushort>();
            targetNeutralsStateDictionaryValue = new List<bool>();
            foreach (KeyValuePair<ushort, bool> item in turret.targetNeutralsStateDictionary)
            {
                targetNeutralsStateDictionaryKey.Add(item.Key);
                targetNeutralsStateDictionaryValue.Add(item.Value);
            }

            minimumGridSizeStateDictionaryKey = new List<ushort>();
            minimumGridSizeStateDictionaryValue = new List<float>();
            foreach (KeyValuePair<ushort, float> item in turret.minimumGridSizeStateDictionary)
            {
                minimumGridSizeStateDictionaryKey.Add(item.Key);
                minimumGridSizeStateDictionaryValue.Add(item.Value);
            }

            obstacleToleranceStateDictionaryKey = new List<ushort>();
            obstacleToleranceStateDictionaryValue = new List<float>();
            foreach (KeyValuePair<ushort, float> item in turret.obstacleToleranceStateDictionary)
            {
                obstacleToleranceStateDictionaryKey.Add(item.Key);
                obstacleToleranceStateDictionaryValue.Add(item.Value);
            }

            throughFriendliesStateDictionaryKey = new List<ushort>();
            throughFriendliesStateDictionaryValue = new List<bool>();
            foreach (KeyValuePair<ushort, bool> item in turret.throughFriendliesStateDictionary)
            {
                throughFriendliesStateDictionaryKey.Add(item.Key);
                throughFriendliesStateDictionaryValue.Add(item.Value);
            }

            throughNeutralsStateDictionaryKey = new List<ushort>();
            throughNeutralsStateDictionaryValue = new List<bool>();
            foreach (KeyValuePair<ushort, bool> item in turret.throughNeutralsStateDictionary)
            {
                throughNeutralsStateDictionaryKey.Add(item.Key);
                throughNeutralsStateDictionaryValue.Add(item.Value);
            }

            throughHostilesStateDictionaryKey = new List<ushort>();
            throughHostilesStateDictionaryValue = new List<bool>();
            foreach (KeyValuePair<ushort, bool> item in turret.throughHostilesStateDictionary)
            {
                throughHostilesStateDictionaryKey.Add(item.Key);
                throughHostilesStateDictionaryValue.Add(item.Value);
            }
        }

        public SmartTurretSaveData()
        {
            //Parameterless constructor for serialization.
        }
    }

    public class SmartTurretSyncData
    {
        public SmartTurretSaveData saveData;
        public long turretEntityId;

        public SmartTurretSyncData(SmartTurretSaveData saveData, long turretEntityId)
        {
            this.saveData = saveData;
            this.turretEntityId = turretEntityId;
        }

        public SmartTurretSyncData()
        {
            //Parameterless constructor for serialization.
        }
    }
}
