using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using NAudio.Wave;
using PeepingTom.Ipc;
using PeepingTom.Resources;

namespace PeepingTom
{
    internal class TargetWatcher : IDisposable
    {
        private PeepingTomPlugin Plugin { get; }

        private Stopwatch UpdateWatch { get; } = new();
        private Stopwatch? SoundWatch { get; set; }
        private int LastTargetAmount { get; set; }

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
            var player = Plugin.ObjectTable.LocalPlayer;
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

            // play sound if necessary
            if (CanPlaySound())
            {
                SoundWatch?.Restart();
                PlaySound();
            }

            LastTargetAmount = Current.Length;
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

        private bool CanPlaySound()
        {
            if (!Plugin.Config.PlaySoundOnTarget)
            {
                return false;
            }

            if (Current.Length <= LastTargetAmount)
            {
                return false;
            }

            if (!Plugin.Config.PlaySoundWhenClosed && !Plugin.Ui.Visible)
            {
                return false;
            }

            if (SoundWatch == null)
            {
                SoundWatch = new Stopwatch();
                return true;
            }

            var secs = SoundWatch.Elapsed.TotalSeconds;
            return secs >= Plugin.Config.SoundCooldown;
        }

        private void PlaySound()
        {
            var soundDevice = DirectSoundOut.Devices.FirstOrDefault(d => d.Guid == Plugin.Config.SoundDeviceNew);
            if (soundDevice == null)
            {
                return;
            }

            new Thread(() =>
            {
                WaveStream reader;
                try
                {
                    if (Plugin.Config.SoundPath == null)
                    {
                        reader = new WaveFileReader(GetEmbeddedSound());
                    }
                    else
                    {
                        reader = new MediaFoundationReader(Plugin.Config.SoundPath);
                    }
                }
                catch (Exception e)
                {
                    var error = string.Format(Language.SoundChatError, e.Message);
                    SendError(error);
                    return;
                }

                using var channel = new WaveChannel32(reader) { Volume = Plugin.Config.SoundVolume, PadWithZeroes = false };

                using (reader)
                {
                    using var output = new DirectSoundOut(soundDevice.Guid);

                    try
                    {
                        output.Init(channel);
                        output.Play();

                        while (output.PlaybackState == PlaybackState.Playing)
                        {
                            Thread.Sleep(500);
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error(ex, "Exception playing sound");
                    }
                }
            }).Start();
        }

        private void SendError(string message)
        {
            Plugin.ChatGui.Print(new XivChatEntry { Message = $"[{PeepingTomPlugin.Name}] {message}", Type = XivChatType.ErrorMessage });
        }

        private static Stream GetEmbeddedSound()
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream("PeepingTom.Resources.target.wav")
                ?? throw new InvalidOperationException("Missing embedded target sound");
        }
    }
}
