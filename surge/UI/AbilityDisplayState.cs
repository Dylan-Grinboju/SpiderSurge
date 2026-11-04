using System;
using System.IO;

namespace SpiderSurge
{
    internal readonly struct AbilityDisplayState
    {
        public readonly bool Known, Active, CoolingDown;
        public readonly float ActiveRemaining, ActiveDuration, CooldownRemaining, CooldownDuration;

        public AbilityDisplayState(bool active, bool coolingDown, float activeRemaining, float activeDuration, float cooldownRemaining, float cooldownDuration)
        {
            Known = true;
            Active = active;
            CoolingDown = coolingDown;
            ActiveRemaining = Math.Max(0f, activeRemaining);
            ActiveDuration = activeDuration;
            CooldownRemaining = Math.Max(0f, cooldownRemaining);
            CooldownDuration = cooldownDuration;
        }

        public bool Ready => Known && !Active && !CoolingDown;
        public float ActiveProgress => ActiveDuration > 0f ? Math.Min(1f, ActiveRemaining / ActiveDuration) : 1f;
        public float CooldownProgress => !Known ? 0f : !CoolingDown ? 1f : CooldownDuration <= 0f ? 0f : Math.Max(0f, 1f - CooldownRemaining / CooldownDuration);

        public AbilityDisplayState Advance(float seconds) => new AbilityDisplayState(Active, CoolingDown,
            ActiveRemaining - Math.Max(0f, seconds), ActiveDuration, CooldownRemaining - Math.Max(0f, seconds), CooldownDuration);

        public void Write(BinaryWriter writer)
        {
            writer.Write(Active);
            writer.Write(CoolingDown);
            writer.Write(ActiveRemaining);
            writer.Write(ActiveDuration);
            writer.Write(CooldownRemaining);
            writer.Write(CooldownDuration);
        }

        public static AbilityDisplayState Read(BinaryReader reader) => new AbilityDisplayState(reader.ReadBoolean(), reader.ReadBoolean(),
            reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
}
