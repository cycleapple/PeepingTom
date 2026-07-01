using System;

namespace PeepingTom.Ipc.From
{
    [Serializable]
    public class StoppedTargetingMessage(Targeter targeter) : IFromMessage
    {
        public Targeter Targeter { get; } = targeter;
    }
}
