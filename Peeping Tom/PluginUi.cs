using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility;
using PeepingTom.Localization;

namespace PeepingTom
{
    internal class PluginUi(PeepingTomPlugin plugin) : IDisposable
    {
        private bool _settingsOpen;

        public bool SettingsOpen
        {
            get => _settingsOpen;
            set => _settingsOpen = value;
        }

        public void Dispose()
        {
            SettingsOpen = false;
        }

        public void Draw()
        {
            if (plugin.InPvp)
            {
                return;
            }

            if (SettingsOpen)
            {
                ShowSettings();
            }

            const ImGuiWindowFlags flags =
                ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.NoNavInputs
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.NoInputs
                | ImGuiWindowFlags.NoMouseInputs
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoDecoration
                | ImGuiWindowFlags.NoScrollWithMouse;
            ImGuiHelpers.ForceNextWindowMainViewport();
            if (!ImGui.Begin("Peeping Tom targeting indicator dummy window", flags))
            {
                ImGui.End();
                return;
            }

            if (plugin.Config.MarkTargeted)
            {
                MarkPlayer(GetCurrentTarget(), plugin.Config.TargetedColour, plugin.Config.TargetedSize);
            }

            if (!plugin.Config.MarkTargeting)
            {
                goto EndDummy;
            }

            var player = plugin.ObjectTable.LocalPlayer;
            if (player == null)
            {
                goto EndDummy;
            }

            var targeting = plugin
                .Watcher.CurrentTargeters.Select(targeterId => plugin.ObjectTable.FirstOrDefault(obj => obj.GameObjectId == targeterId))
                .Where(targeter => targeter is IPlayerCharacter)
                .Cast<IPlayerCharacter>()
                .ToArray();
            foreach (var targeter in targeting)
            {
                MarkPlayer(targeter, plugin.Config.TargetingColour, plugin.Config.TargetingSize);
            }

            EndDummy:
            ImGui.End();
        }

        private void ShowSettings()
        {
            ImGui.SetNextWindowSize(new Vector2(520, 210), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(360, 180), new Vector2(float.MaxValue, float.MaxValue));
            var windowTitle = string.Format(Loc.Text("SettingsTitle"), PeepingTomPlugin.Name);
            if (!ImGui.Begin($"{windowTitle}###ptom-settings", ref _settingsOpen))
            {
                ImGui.End();
                return;
            }

            if (ImGui.BeginTabBar("##settings-tabs"))
            {
                if (ImGui.BeginTabItem($"{Loc.Text("SettingsMarkersTab")}###markers-tab"))
                {
                    var markTargeted = plugin.Config.MarkTargeted;
                    if (ImGui.Checkbox(Loc.Text("SettingsMarkersMarkTarget"), ref markTargeted))
                    {
                        plugin.Config.MarkTargeted = markTargeted;
                        plugin.Config.Save();
                    }

                    var targetedColour = plugin.Config.TargetedColour;
                    if (ImGui.ColorEdit4(Loc.Text("SettingsMarkersMarkTargetColour"), ref targetedColour))
                    {
                        plugin.Config.TargetedColour = targetedColour;
                        plugin.Config.Save();
                    }

                    var targetedSize = plugin.Config.TargetedSize;
                    if (ImGui.DragFloat(Loc.Text("SettingsMarkersMarkTargetSize"), ref targetedSize, 0.01f, 0f, 15f))
                    {
                        targetedSize = Math.Max(0f, targetedSize);
                        plugin.Config.TargetedSize = targetedSize;
                        plugin.Config.Save();
                    }

                    ImGui.Spacing();

                    var markTargeting = plugin.Config.MarkTargeting;
                    if (ImGui.Checkbox(Loc.Text("SettingsMarkersMarkTargeting"), ref markTargeting))
                    {
                        plugin.Config.MarkTargeting = markTargeting;
                        plugin.Config.Save();
                    }

                    var targetingColour = plugin.Config.TargetingColour;
                    if (ImGui.ColorEdit4(Loc.Text("SettingsMarkersMarkTargetingColour"), ref targetingColour))
                    {
                        plugin.Config.TargetingColour = targetingColour;
                        plugin.Config.Save();
                    }

                    var targetingSize = plugin.Config.TargetingSize;
                    if (ImGui.DragFloat(Loc.Text("SettingsMarkersMarkTargetingSize"), ref targetingSize, 0.01f, 0f, 15f))
                    {
                        targetingSize = Math.Max(0f, targetingSize);
                        plugin.Config.TargetingSize = targetingSize;
                        plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        private void MarkPlayer(IGameObject? player, Vector4 colour, float size)
        {
            if (player == null)
            {
                return;
            }

            if (!plugin.GameGui.WorldToScreen(player.Position, out var screenPos))
            {
                return;
            }

            ImGui.PushClipRect(ImGuiHelpers.MainViewport.Pos, ImGuiHelpers.MainViewport.Pos + ImGuiHelpers.MainViewport.Size, false);

            ImGui
                .GetWindowDrawList()
                .AddCircleFilled(
                    ImGuiHelpers.MainViewport.Pos + new Vector2(screenPos.X, screenPos.Y),
                    size,
                    ImGui.GetColorU32(colour),
                    100
                );

            ImGui.PopClipRect();
        }

        private IPlayerCharacter? GetCurrentTarget()
        {
            var player = plugin.ObjectTable.LocalPlayer;
            if (player == null)
            {
                return null;
            }

            var targetId = player.TargetObjectId;
            if (targetId <= 0)
            {
                return null;
            }

            return plugin
                .ObjectTable.Where(actor => actor.GameObjectId == targetId && actor is IPlayerCharacter)
                .Select(actor => actor as IPlayerCharacter)
                .FirstOrDefault();
        }
    }
}
