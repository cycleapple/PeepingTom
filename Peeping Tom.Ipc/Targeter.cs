using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;

namespace PeepingTom.Ipc
{
    [Serializable]
    public class Targeter
    {
        [JsonConverter(typeof(SeStringConverter))]
        public SeString Name { get; }
        public uint HomeWorldId { get; }
        public ulong ObjectId { get; }
        public DateTime When { get; }

        public Targeter(IPlayerCharacter character)
        {
            Name = character.Name;
            HomeWorldId = character.HomeWorld.RowId;
            ObjectId = character.GameObjectId;
            When = DateTime.UtcNow;
        }

        [JsonConstructor]
        public Targeter(SeString name, uint homeWorldId, ulong objectId, DateTime when)
        {
            Name = name;
            HomeWorldId = homeWorldId;
            ObjectId = objectId;
            When = when;
        }

        public IPlayerCharacter? GetPlayerCharacter(IObjectTable objectTable)
        {
            return objectTable.FirstOrDefault(actor => actor.GameObjectId == ObjectId && actor is IPlayerCharacter) as IPlayerCharacter;
        }
    }
}
