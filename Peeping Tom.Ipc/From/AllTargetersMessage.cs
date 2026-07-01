using System;
using System.Collections.Generic;

namespace PeepingTom.Ipc.From
{
    [Serializable]
    public class AllTargetersMessage(List<(Targeter, bool)> targeters) : IFromMessage
    {
        public List<(Targeter targeter, bool currentlyTargeting)> Targeters { get; } = targeters;
    }
}
