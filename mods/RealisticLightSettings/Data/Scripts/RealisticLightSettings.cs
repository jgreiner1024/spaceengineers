using System;
using System.Text;
using System.Collections.Generic;

using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;

using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;

using SpaceEngineers.Game.ModAPI;

using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRage.ModAPI;
using System.Linq;

namespace RealisticLightSettings.Data.Scripts
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_InteriorLight), false)]
    public class RealisticLightSettings : MyGameLogicComponent
    {
        static bool controlsAdded = false;
        private LightPresetData selectedLightPresetData = null;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            addTerminalControls();
        }

        public override void Close()
        {
            base.Close();
        }

        private void addTerminalControls()
        {
            if (controlsAdded == true)
                return;

            controlsAdded = true;

            IMyTerminalControlListbox lightSettingsList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyInteriorLight>("XOX_LightList");
            IMyTerminalControlButton applyButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyInteriorLight>("XOX_ApplyButton");

            lightSettingsList.ListContent = (terminalBlock, itemList, selectedItemList) =>
            {
                IMyInteriorLight terminalLight = terminalBlock as IMyInteriorLight;
                if (terminalLight == null)
                    return;

                itemList.Clear();
                itemList.Add(GetListBoxItem("Candle", 255, 147, 41));
                itemList.Add(GetListBoxItem("40W Tungsten", 255, 197, 143));
                itemList.Add(GetListBoxItem("100W Tungsten", 255, 214, 170));
                itemList.Add(GetListBoxItem("Halogen", 255, 241, 224));
                itemList.Add(GetListBoxItem("Carbon Arc", 255, 250, 244));
                itemList.Add(GetListBoxItem("High Noon Sun", 255, 255, 251));
                itemList.Add(GetListBoxItem("Direct Sunlight", 255, 255, 255));
                itemList.Add(GetListBoxItem("Overcast Sky", 201, 226, 255));
                itemList.Add(GetListBoxItem("Clear Blue Sky", 64, 156, 255));
                itemList.Add(GetListBoxItem("Warm Fluorescent", 255, 244, 229));
                itemList.Add(GetListBoxItem("Standard Fluorescent", 244, 255, 250));
                itemList.Add(GetListBoxItem("Cool White Fluorescent", 212, 235, 255));
                itemList.Add(GetListBoxItem("Full Spectrum Fluorescent", 255, 244, 242));
                itemList.Add(GetListBoxItem("Grow Light Fluorescent", 255, 239, 247));
                itemList.Add(GetListBoxItem("Black Light Fluorescent", 167, 0, 255));
                itemList.Add(GetListBoxItem("Mercury Vapor", 216, 247, 255));
                itemList.Add(GetListBoxItem("Sodium Vapor", 255, 209, 178));
                itemList.Add(GetListBoxItem("Metal Halide", 242, 252, 255));
                itemList.Add(GetListBoxItem("High Pressure Sodium", 255, 183, 76));
            };

            lightSettingsList.ItemSelected = (terminalBlock, selectedItemList) => {
                IMyInteriorLight terminalLight = terminalBlock as IMyInteriorLight;
                if (terminalLight == null)
                    return;

                 

                RealisticLightSettings lightSettings = terminalLight?.GameLogic.GetAs<RealisticLightSettings>();
                if (lightSettings == null)
                    return;

                var selectedItem = selectedItemList.FirstOrDefault();
                lightSettings.selectedLightPresetData = selectedItem?.UserData as LightPresetData;
                MyLog.Default.WriteLine($"Selected data is null? {lightSettings.selectedLightPresetData == null}");
            };

            lightSettingsList.Title = MyStringId.GetOrCompute("Presets");
            lightSettingsList.SupportsMultipleBlocks = true;
            lightSettingsList.Multiselect = false;
            lightSettingsList.VisibleRowsCount = 5;
            MyAPIGateway.TerminalControls.AddControl<IMyInteriorLight>(lightSettingsList);

            applyButton.Action = (terminalBlock) => {

                 IMyInteriorLight terminalLight = terminalBlock as IMyInteriorLight;
                if (terminalLight == null)
                    return;

                RealisticLightSettings lightSettings = terminalLight?.GameLogic as RealisticLightSettings;
                if (lightSettings == null)
                    return;

                if (lightSettings.selectedLightPresetData == null)
                    return;

                terminalLight.Falloff = lightSettings.selectedLightPresetData.Falloff;
                terminalLight.Intensity = lightSettings.selectedLightPresetData.Intensity;
                terminalLight.Color = lightSettings.selectedLightPresetData.LightColor;
            };
            applyButton.SupportsMultipleBlocks = true;
            applyButton.Title = MyStringId.GetOrCompute("Apply");
            MyAPIGateway.TerminalControls.AddControl<IMyInteriorLight>(applyButton);
        }

        private MyTerminalControlListBoxItem GetListBoxItem(string name, int r, int g, int b)
        {
            return new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(name), MyStringId.GetOrCompute(name),
                new LightPresetData() {
                    Falloff = 2f,
                    Intensity = 1f,
                    LightColor = new Color(r, g, b)
                });
        }

    }

    public class LightPresetData
    {

        public float Falloff;
        public float Intensity;
        public Color LightColor;
    }
}
