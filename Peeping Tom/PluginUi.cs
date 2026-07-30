using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility;
using PeepingTom.Ipc;
using PeepingTom.Resources;

namespace PeepingTom
{
    internal class PluginUi(PeepingTomPlugin plugin) : IDisposable
    {
        private ulong? PreviousFocus { get; set; } = new();

        private bool _wantsOpen;

        public bool WantsOpen
        {
            get => _wantsOpen;
            set => _wantsOpen = value;
        }

        public bool Visible { get; private set; }

        private bool _settingsOpen;

        public bool SettingsOpen
        {
            get => _settingsOpen;
            set => _settingsOpen = value;
        }

        public void Dispose()
        {
            WantsOpen = false;
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

            var inCombat = plugin.Condition[ConditionFlag.InCombat];
            var inInstance =
                plugin.Condition[ConditionFlag.BoundByDuty]
                || plugin.Condition[ConditionFlag.BoundByDuty56]
                || plugin.Condition[ConditionFlag.BoundByDuty95];
            var inCutscene =
                plugin.Condition[ConditionFlag.WatchingCutscene]
                || plugin.Condition[ConditionFlag.WatchingCutscene78]
                || plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent];

            // FIXME: this could just be a boolean expression
            var shouldBeShown = WantsOpen;
            if (inCombat && !plugin.Config.ShowInCombat)
            {
                shouldBeShown = false;
            }
            else if (inInstance && !plugin.Config.ShowInInstance)
            {
                shouldBeShown = false;
            }
            else if (inCutscene && !plugin.Config.ShowInCutscenes)
            {
                shouldBeShown = false;
            }

            Visible = shouldBeShown;

            if (shouldBeShown)
            {
                ShowMainWindow();
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

            var player = plugin.ClientState.LocalPlayer;
            if (player == null)
            {
                goto EndDummy;
            }

            var targeting = plugin
                .Watcher.CurrentTargeters.Select(targeter =>
                    plugin.ObjectTable.FirstOrDefault(obj => obj.GameObjectId == targeter.ObjectId)
                )
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
            ImGui.SetNextWindowSize(new Vector2(700, 250));
            var windowTitle = string.Format(Language.SettingsTitle, PeepingTomPlugin.Name);
            if (!ImGui.Begin($"{windowTitle}###ptom-settings", ref _settingsOpen, ImGuiWindowFlags.NoResize))
            {
                ImGui.End();
                return;
            }

            if (ImGui.BeginTabBar("##settings-tabs"))
            {
                if (ImGui.BeginTabItem($"{Language.SettingsMarkersTab}###markers-tab"))
                {
                    var markTargeted = plugin.Config.MarkTargeted;
                    if (ImGui.Checkbox(Language.SettingsMarkersMarkTarget, ref markTargeted))
                    {
                        plugin.Config.MarkTargeted = markTargeted;
                        plugin.Config.Save();
                    }

                    var targetedColour = plugin.Config.TargetedColour;
                    if (ImGui.ColorEdit4(Language.SettingsMarkersMarkTargetColour, ref targetedColour))
                    {
                        plugin.Config.TargetedColour = targetedColour;
                        plugin.Config.Save();
                    }

                    var targetedSize = plugin.Config.TargetedSize;
                    if (ImGui.DragFloat(Language.SettingsMarkersMarkTargetSize, ref targetedSize, 0.01f, 0f, 15f))
                    {
                        targetedSize = Math.Max(0f, targetedSize);
                        plugin.Config.TargetedSize = targetedSize;
                        plugin.Config.Save();
                    }

                    ImGui.Spacing();

                    var markTargeting = plugin.Config.MarkTargeting;
                    if (ImGui.Checkbox(Language.SettingsMarkersMarkTargeting, ref markTargeting))
                    {
                        plugin.Config.MarkTargeting = markTargeting;
                        plugin.Config.Save();
                    }

                    var targetingColour = plugin.Config.TargetingColour;
                    if (ImGui.ColorEdit4(Language.SettingsMarkersMarkTargetingColour, ref targetingColour))
                    {
                        plugin.Config.TargetingColour = targetingColour;
                        plugin.Config.Save();
                    }

                    var targetingSize = plugin.Config.TargetingSize;
                    if (ImGui.DragFloat(Language.SettingsMarkersMarkTargetingSize, ref targetingSize, 0.01f, 0f, 15f))
                    {
                        targetingSize = Math.Max(0f, targetingSize);
                        plugin.Config.TargetingSize = targetingSize;
                        plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsFilterTab}###filters-tab"))
                {
                    var showParty = plugin.Config.LogParty;
                    if (ImGui.Checkbox(Language.SettingsFilterLogParty, ref showParty))
                    {
                        plugin.Config.LogParty = showParty;
                        plugin.Config.Save();
                    }

                    var logAlliance = plugin.Config.LogAlliance;
                    if (ImGui.Checkbox(Language.SettingsFilterLogAlliance, ref logAlliance))
                    {
                        plugin.Config.LogAlliance = logAlliance;
                        plugin.Config.Save();
                    }

                    var logInCombat = plugin.Config.LogInCombat;
                    if (ImGui.Checkbox(Language.SettingsFilterLogCombat, ref logInCombat))
                    {
                        plugin.Config.LogInCombat = logInCombat;
                        plugin.Config.Save();
                    }

                    var logSelf = plugin.Config.LogSelf;
                    if (ImGui.Checkbox(Language.SettingsFilterLogSelf, ref logSelf))
                    {
                        plugin.Config.LogSelf = logSelf;
                        plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsBehaviourTab}###behaviour-tab"))
                {
                    var focusTarget = plugin.Config.FocusTargetOnHover;
                    if (ImGui.Checkbox(Language.SettingsBehaviourFocusHover, ref focusTarget))
                    {
                        plugin.Config.FocusTargetOnHover = focusTarget;
                        plugin.Config.Save();
                    }

                    var openExamine = plugin.Config.OpenExamine;
                    if (ImGui.Checkbox(Language.SettingsBehaviourExamineEnabled, ref openExamine))
                    {
                        plugin.Config.OpenExamine = openExamine;
                        plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsWindowTab}###window-tab"))
                {
                    var openOnLogin = plugin.Config.OpenOnLogin;
                    if (ImGui.Checkbox(Language.SettingsWindowOpenLogin, ref openOnLogin))
                    {
                        plugin.Config.OpenOnLogin = openOnLogin;
                        plugin.Config.Save();
                    }

                    var allowMovement = plugin.Config.AllowMovement;
                    if (ImGui.Checkbox(Language.SettingsWindowAllowMovement, ref allowMovement))
                    {
                        plugin.Config.AllowMovement = allowMovement;
                        plugin.Config.Save();
                    }

                    var allowResizing = plugin.Config.AllowResize;
                    if (ImGui.Checkbox(Language.SettingsWindowAllowResize, ref allowResizing))
                    {
                        plugin.Config.AllowResize = allowResizing;
                        plugin.Config.Save();
                    }

                    ImGui.Spacing();

                    var showInCombat = plugin.Config.ShowInCombat;
                    if (ImGui.Checkbox(Language.SettingsWindowShowCombat, ref showInCombat))
                    {
                        plugin.Config.ShowInCombat = showInCombat;
                        plugin.Config.Save();
                    }

                    var showInInstance = plugin.Config.ShowInInstance;
                    if (ImGui.Checkbox(Language.SettingsWindowShowInstance, ref showInInstance))
                    {
                        plugin.Config.ShowInInstance = showInInstance;
                        plugin.Config.Save();
                    }

                    var showInCutscenes = plugin.Config.ShowInCutscenes;
                    if (ImGui.Checkbox(Language.SettingsWindowShowCutscene, ref showInCutscenes))
                    {
                        plugin.Config.ShowInCutscenes = showInCutscenes;
                        plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsHistoryTab}###history-tab"))
                {
                    var keepHistory = plugin.Config.KeepHistory;
                    if (ImGui.Checkbox(Language.SettingsHistoryEnabled, ref keepHistory))
                    {
                        plugin.Config.KeepHistory = keepHistory;
                        plugin.Config.Save();
                    }

                    var historyWhenClosed = plugin.Config.HistoryWhenClosed;
                    if (ImGui.Checkbox(Language.SettingsHistoryRecordClosed, ref historyWhenClosed))
                    {
                        plugin.Config.HistoryWhenClosed = historyWhenClosed;
                        plugin.Config.Save();
                    }

                    var numHistory = plugin.Config.NumHistory;
                    if (ImGui.InputInt(Language.SettingsHistoryAmount, ref numHistory))
                    {
                        numHistory = Math.Max(0, Math.Min(50, numHistory));
                        plugin.Config.NumHistory = numHistory;
                        plugin.Config.Save();
                    }

                    var showTimestamps = plugin.Config.ShowTimestamps;
                    if (ImGui.Checkbox(Language.SettingsHistoryTimestamps, ref showTimestamps))
                    {
                        plugin.Config.ShowTimestamps = showTimestamps;
                        plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsAdvancedTab}###advanced-tab"))
                {
                    var pollFrequency = plugin.Config.PollFrequency;
                    if (ImGui.DragInt(Language.SettingsAdvancedPollFrequency, ref pollFrequency, .1f, 1, 1600))
                    {
                        plugin.Config.PollFrequency = pollFrequency;
                        plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        private void ShowMainWindow()
        {
            var targeting = plugin.Watcher.CurrentTargeters;
            var previousTargeters = plugin.Config.KeepHistory ? plugin.Watcher.PreviousTargeters : null;

            // to prevent looping over a subset of the actors repeatedly when multiple people are targeting,
            // create a dictionary for O(1) lookups by actor id
            Dictionary<ulong, IGameObject>? objects = null;
            if (targeting.Count + (previousTargeters?.Count ?? 0) > 1)
            {
                var dict = new Dictionary<ulong, IGameObject>();
                foreach (var obj in plugin.ObjectTable)
                {
                    if (dict.ContainsKey(obj.GameObjectId) || obj.ObjectKind != ObjectKind.Player)
                    {
                        continue;
                    }

                    dict.Add(obj.GameObjectId, obj);
                }

                objects = dict;
            }

            var flags = ImGuiWindowFlags.None;
            if (!plugin.Config.AllowMovement)
            {
                flags |= ImGuiWindowFlags.NoMove;
            }

            if (!plugin.Config.AllowResize)
            {
                flags |= ImGuiWindowFlags.NoResize;
            }

            ImGui.SetNextWindowSize(new Vector2(290, 195), ImGuiCond.FirstUseEver);
            if (!ImGui.Begin(PeepingTomPlugin.Name, ref _wantsOpen, flags))
            {
                ImGui.End();
                return;
            }

            {
                ImGui.Text(Language.MainTargetingYou);
                ImGui.SameLine();
                HelpMarker(plugin.Config.OpenExamine ? Language.MainHelpExamine : Language.MainHelpNoExamine);

                var height = ImGui.GetContentRegionAvail().Y;
                height -= ImGui.GetStyle().ItemSpacing.Y;

                var anyHovered = false;
                if (ImGui.BeginListBox("##targeting", new Vector2(-1, height)))
                {
                    // add the two first players for testing
                    // foreach (var p in this.Plugin.Interface.ClientState.Actors
                    //     .Where(actor => actor is PlayerCharacter)
                    //     .Skip(1)
                    //     .Select(actor => actor as PlayerCharacter)
                    //     .Take(2)) {
                    //     this.AddEntry(new Targeter(p), p, ref anyHovered);
                    // }

                    foreach (var targeter in targeting)
                    {
                        IGameObject? obj = null;
                        objects?.TryGetValue(targeter.ObjectId, out obj);
                        AddEntry(targeter, obj, ref anyHovered);
                    }

                    if (plugin.Config.KeepHistory)
                    {
                        // get a list of the previous targeters that aren't currently targeting
                        var previous = (previousTargeters ?? new List<Targeter>())
                            .Where(old => targeting.All(actor => actor.ObjectId != old.ObjectId))
                            .Take(plugin.Config.NumHistory);
                        // add previous targeters to the list
                        foreach (var oldTargeter in previous)
                        {
                            IGameObject? obj = null;
                            objects?.TryGetValue(oldTargeter.ObjectId, out obj);
                            AddEntry(oldTargeter, obj, ref anyHovered, ImGuiSelectableFlags.Disabled);
                        }
                    }

                    ImGui.EndListBox();
                }

                var previousFocus = PreviousFocus;
                if (plugin.Config.FocusTargetOnHover && !anyHovered && previousFocus != null)
                {
                    if (previousFocus == ulong.MaxValue)
                    {
                        plugin.TargetManager.FocusTarget = null;
                    }
                    else
                    {
                        var actor = plugin.ObjectTable.FirstOrDefault(a => a.GameObjectId == previousFocus);
                        // either target the actor if still present or target nothing
                        plugin.TargetManager.FocusTarget = actor;
                    }

                    PreviousFocus = null;
                }

                ImGui.End();
            }
        }

        private static void HelpMarker(string text)
        {
            ImGui.TextDisabled("(?)");
            if (!ImGui.IsItemHovered())
            {
                return;
            }

            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        private void AddEntry(
            Targeter targeter,
            IGameObject? obj,
            ref bool anyHovered,
            ImGuiSelectableFlags flags = ImGuiSelectableFlags.None
        )
        {
            ImGui.BeginGroup();

            ImGui.Selectable(targeter.Name.TextValue, false, flags);

            if (plugin.Config.ShowTimestamps)
            {
                var time =
                    DateTime.UtcNow - targeter.When >= TimeSpan.FromDays(1)
                        ? targeter.When.ToLocalTime().ToString("dd/MM")
                        : targeter.When.ToLocalTime().ToString("t");
                var windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
                ImGui.SameLine(windowWidth - ImGui.CalcTextSize(time).X);

                if (flags.HasFlag(ImGuiSelectableFlags.Disabled))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
                }

                ImGui.TextUnformatted(time);

                if (flags.HasFlag(ImGuiSelectableFlags.Disabled))
                {
                    ImGui.PopStyleColor();
                }
            }

            ImGui.EndGroup();

            var hover = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
            var left = hover && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var right = hover && ImGui.IsMouseClicked(ImGuiMouseButton.Right);

            obj ??= plugin.ObjectTable.FirstOrDefault(a => a.GameObjectId == targeter.ObjectId);

            // don't count as hovered if the actor isn't here (clears focus target when hovering missing actors)
            if (obj != null)
            {
                anyHovered |= hover;
            }

            if (plugin.Config.FocusTargetOnHover && hover && obj != null)
            {
                PreviousFocus ??= plugin.TargetManager.FocusTarget?.GameObjectId ?? ulong.MaxValue;
                plugin.TargetManager.FocusTarget = obj;
            }

            if (left)
            {
                if (plugin.Config.OpenExamine && ImGui.GetIO().KeyAlt)
                {
                    if (obj is IPlayerCharacter player)
                    {
                        plugin.ExamineHelper.Open(player);
                    }
                    else
                    {
                        var error = string.Format(Language.ExamineErrorToast, targeter.Name);
                        plugin.ToastGui.ShowError(error);
                    }
                }
                else
                {
                    var payload = new PlayerPayload(targeter.Name.TextValue, targeter.HomeWorldId);
                    Payload[] payloads = { payload };
                    plugin.ChatGui.Print(new XivChatEntry { Message = new SeString(payloads) });
                }
            }
            else if (right && obj != null)
            {
                plugin.TargetManager.Target = obj;
            }
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
            var player = plugin.ClientState.LocalPlayer;
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
