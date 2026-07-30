using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using PeepingTom.Ipc;

namespace PeepingTom
{
    internal class TargetWatcher : IDisposable
    {
        private PeepingTomPlugin Plugin { get; }

        private Stopwatch UpdateWatch { get; } = new();

        private Targeter[] Current { get; set; } = Array.Empty<Targeter>();

        public IReadOnlyCollection<Targeter> CurrentTargeters => Current;

        private List<Targeter> Previous { get; } = [];

        public IReadOnlyCollection<Targeter> PreviousTargeters => Previous;

        public TargetWatcher(PeepingTomPlugin plugin)
        {
            Plugin = plugin;
            UpdateWatch.Start();

            Plugin.Framework.Update += OnFrameworkUpdate;
        }

        public void Dispose()
        {
            Plugin.Framework.Update -= OnFrameworkUpdate;
        }

        public void ClearPrevious()
        {
            Previous.Clear();
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (Plugin.InPvp)
            {
                return;
            }

            if (UpdateWatch.Elapsed > TimeSpan.FromMilliseconds(Plugin.Config.PollFrequency))
            {
                Update();
            }
        }

        private void Update()
        {
            var player = Plugin.ClientState.LocalPlayer;
            if (player == null)
            {
                return;
            }

            // get targeters and set a copy so we can release the mutex faster
            var newCurrent = GetTargeting(Plugin.ObjectTable, player);

            foreach (var newTargeter in newCurrent.Where(t => Current.All(c => c.ObjectId != t.ObjectId)))
            {
                try
                {
                    Plugin.IpcManager.SendNewTargeter(newTargeter);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Failed to send IPC message");
                }
            }

            foreach (var stopped in Current.Where(t => newCurrent.All(c => c.ObjectId != t.ObjectId)))
            {
                try
                {
                    Plugin.IpcManager.SendStoppedTargeting(stopped);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Failed to send IPC message");
                }
            }

            Current = newCurrent;

            HandleHistory(Current);
        }

        private void HandleHistory(Targeter[] targeting)
        {
            if (!Plugin.Config.KeepHistory || !Plugin.Config.HistoryWhenClosed && !Plugin.Ui.Visible)
            {
                return;
            }

            foreach (var targeter in targeting)
            {
                // add the targeter to the previous list
                if (Previous.Any(old => old.ObjectId == targeter.ObjectId))
                {
                    Previous.RemoveAll(old => old.ObjectId == targeter.ObjectId);
                }

                Previous.Insert(0, targeter);
            }

            // only keep the configured number of previous targeters (ignoring ones that are currently targeting)
            while (Previous.Count(old => targeting.All(actor => actor.ObjectId != old.ObjectId)) > Plugin.Config.NumHistory)
            {
                Previous.RemoveAt(Previous.Count - 1);
            }
        }

        private Targeter[] GetTargeting(IEnumerable<IGameObject> objects, IGameObject player)
        {
            return objects
                .Where(obj => obj.TargetObjectId == player.GameObjectId && obj is IPlayerCharacter)
                .Cast<IPlayerCharacter>()
                .Where(actor => Plugin.Config.LogParty || !InParty(actor))
                .Where(actor => Plugin.Config.LogAlliance || !InAlliance(actor))
                .Where(actor => Plugin.Config.LogInCombat || !InCombat(actor))
                .Where(actor => Plugin.Config.LogSelf || actor.GameObjectId != player.GameObjectId)
                .Select(actor => new Targeter(actor))
                .ToArray();
        }

        private static bool InCombat(IPlayerCharacter actor) => actor.StatusFlags.HasFlag(StatusFlags.InCombat);

        private static bool InParty(IPlayerCharacter actor) => actor.StatusFlags.HasFlag(StatusFlags.PartyMember);

        private static bool InAlliance(IPlayerCharacter actor) => actor.StatusFlags.HasFlag(StatusFlags.AllianceMember);
    }
}
