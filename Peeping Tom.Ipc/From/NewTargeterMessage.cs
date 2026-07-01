using System;

namespace PeepingTom.Ipc.From
{
    [Serializable]
    public class NewTargeterMessage(Targeter targeter) : IFromMessage
    {
        public Targeter Targeter { get; } = targeter;
    }
}
