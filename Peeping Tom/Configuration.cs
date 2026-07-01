using System;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace PeepingTom
{
    [Serializable]
    internal class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        private IDalamudPluginInterface Interface { get; set; } = null!;

        public bool MarkTargeted { get; set; }

        public Vector4 TargetedColour { get; set; } = new(0f, 1f, 0f, 1f);
        public float TargetedSize { get; set; } = 2f;

        public bool MarkTargeting { get; set; }
        public Vector4 TargetingColour { get; set; } = new(1f, 0f, 0f, 1f);
        public float TargetingSize { get; set; } = 2f;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            Interface = pluginInterface;
        }

        public void Save()
        {
            Interface.SavePluginConfig(this);
        }
    }
}
