using SpiderSurge.Common;

namespace SpiderTeams
{
    // The attacker of the innermost Teams entry on the core attack scope
    internal static class DamageContext
    {
        private static AttackView<int> View => TeamAttacks.View;

        public static int Current => View.Active ? View.Slot(View.Top) : OwnerRegistry.Unknown;

        // The innermost explosion's instance id, 0 outside one
        public static int Blast
        {
            get
            {
                int index = View.Innermost<Explosion>();
                return index >= 0 ? View.SourceAt(index).GetInstanceID() : 0;
            }
        }

        public static bool InPulse => View.Innermost<SpiderSurge.PulseAbility>() >= 0;
    }
}
