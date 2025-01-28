using Dalamud.Configuration;
using ImGuiNET;
using System;
using System.Linq;
using Lumina.Excel.Sheets;
using Veda;
using Dalamud.Plugin;
using XIVPlugins.Shared;

namespace AutoLogin {
    public class Configuration : IPluginConfiguration {

        public int Version { get; set; }
        private bool ShowSupport;
        public bool AutoReconnectEnabled { get; set; } = true;
        public bool SimpleLoginMode { get; set; } = false;
        
        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            Plugin.PluginInterface = pluginInterface;
        }

        public void Save() {
            Plugin.PluginInterface.SavePluginConfig(this);
        }

        public uint DataCenter;
        public uint World;
        public uint CharacterSlot;

        public bool DrawConfigUI() {
            var drawConfig = true;
            const ImGuiWindowFlags windowFlags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;

            var dcSheet = Plugin.Data.Excel.GetSheet<WorldDCGroupType>();
            if (dcSheet == null) return false;
            var worldSheet = Plugin.Data.Excel.GetSheet<World>();
            if (worldSheet == null) return false;

            var currentDc = dcSheet.GetRow(DataCenter);
            if (currentDc.Region == 0) {
                DataCenter = 0;
            }

            if (ImGui.Begin("AutoLogin Config", ref drawConfig, windowFlags)) {

                var simpleMode = SimpleLoginMode;
                if (ImGui.Checkbox("Simple Login Mode", ref simpleMode))
                {
                    SimpleLoginMode = simpleMode;
                    Save();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("When enabled, plugin will only select character slot without choosing datacenter/world");
                }

                ImGui.Spacing();

                if (!SimpleLoginMode) {
                    if (ImGui.BeginCombo("Data Center", DataCenter == 0 ? "Not Selected" : currentDc.Name.ToString())) {
                        foreach (var dc  in dcSheet.Where(w => w.Region > 0 && w.Name.ToString().Trim().Length > 0)) {
                            if (ImGui.Selectable(dc.Name.ToString(), dc.RowId == DataCenter)) {
                                DataCenter = dc.RowId;
                                Save();
                            }
                        }
                        ImGui.EndCombo();
                    }

                    if (currentDc.Region != 0) {

                        var currentWorld = worldSheet.GetRow(World);
                        if (World != 0 && currentWorld.DataCenter.RowId != DataCenter) {
                            World = 0;
                            return true;
                        }

                        if (ImGui.BeginCombo("World", World == 0 ? "Not Selected" : currentWorld.Name.ToString())) {
                            foreach (var w in worldSheet.Where(w => w.DataCenter.RowId == DataCenter && w.IsPublic)) {
                                if (ImGui.Selectable(w.Name.ToString(), w.RowId == World)) {
                                    World = w.RowId;
                                    Save();
                                }
                            }
                            ImGui.EndCombo();
                        }

                        if (currentWorld.IsPublic) {
                            if (ImGui.BeginCombo("Character Slot", $"Slot #{CharacterSlot+1}")) {
                                for (uint i = 0; i < 8; i++) {
                                    if (ImGui.Selectable($"Slot #{i+1}", CharacterSlot == i)) {
                                        CharacterSlot = i;
                                        Save();
                                    }
                                }
                                ImGui.EndCombo();
                            }
                        }
                    }
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                    var autoReconnect = AutoReconnectEnabled;
                    if (ImGui.Checkbox("Enable Auto-Reconnect", ref autoReconnect))
                    {
                        AutoReconnectEnabled = autoReconnect;
                        Save();
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Automatically reconnect when connection error occurs");
                }
            }

            ImGui.End();

            return drawConfig;
        }
    }
}
