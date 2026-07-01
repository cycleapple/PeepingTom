using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace PeepingTom
{
    internal class TargetWatcher : IDisposable
    {
        private const int PollFrequencyMs = 100;

        private PeepingTomPlugin Plugin { get; }

        private Stopwatch UpdateWatch { get; } = new();

        private ulong[] Current { get; set; } = [];

        public IReadOnlyCollection<ulong> CurrentTargeters => Current;

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

        private void OnFrameworkUpdate(IFramework framework)
        {
            if (Plugin.InPvp)
            {
                return;
            }

            if (UpdateWatch.Elapsed > TimeSpan.FromMilliseconds(PollFrequencyMs))
            {
                UpdateWatch.Restart();
                Update();
            }
        }

        private void Update()
        {
            var player = Plugin.ObjectTable.LocalPlayer;
            if (player == null)
            {
                return;
            }

            // get targeters and set a copy so we can release the mutex faster
            var newCurrent = GetTargeting(Plugin.ObjectTable, player);

            Current = newCurrent;
        }

        private static ulong[] GetTargeting(IEnumerable<IGameObject> objects, IGameObject player)
        {
            return
            [
                .. objects
                    .Where(obj => obj.TargetObjectId == player.GameObjectId && obj is IPlayerCharacter)
                    .Cast<IPlayerCharacter>()
                    .Where(actor => actor.GameObjectId != player.GameObjectId)
                    .Select(actor => actor.GameObjectId),
            ];
        }
    }
}
