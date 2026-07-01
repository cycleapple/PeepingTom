using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace PeepingTom
{
    internal unsafe class ExamineHelper
    {
        public bool Open(IPlayerCharacter player)
        {
            if (player.Address == IntPtr.Zero)
            {
                return false;
            }

            var character = (Character*)player.Address;
            var agent = AgentDetail.Instance();
            if (agent == null)
            {
                return false;
            }

            var data = new InfoProxyCommonList.CharacterData
            {
                ContentId = character->ContentId,
                AccountId = character->AccountId,
                State = InfoProxyCommonList.CharacterData.OnlineStatus.Online,
                CurrentWorld = character->CurrentWorld,
                HomeWorld = character->HomeWorld,
                Sex = character->Sex,
                Job = character->ClassJob,
                NameString = player.Name.TextValue,
                FCTagString = character->FreeCompanyTagString,
            };

            agent->OpenForCharacterData(&data);
            return true;
        }
    }
}
