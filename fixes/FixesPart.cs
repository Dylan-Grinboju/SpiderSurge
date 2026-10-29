using SpiderSurge.Common;

namespace SpiderSurge.Fixes
{
    public class FixesPart : Part
    {
        public override string Key => "fixes";
        public override string Title => "Game fixes";
        public override string Description => "Fixes bugs in the base game:\n• Every controller can pause while others are in use.\n• Reconnected controllers keep their spider.\n• Whisps wait before their first shot.\n• Off-screen arrows handle missing targets without errors.\n• Pain-level colours unlock without lobby errors.\n• Custom-map warnings no longer cause startup errors.\n• Perk timeouts can pick any offered card.\n• Shotguns use all firing and hit sounds.\n• Hidden connecting screens no longer block returning to the lobby.\n• Tiers of Heck and Parkour no longer affect pain-level scores or leaderboards.";
        public override string Namespace => "SpiderSurge.Fixes";
        public override bool OnByDefault => true;

        protected internal override void OnStart()
        {
            ControllerReconnectFix.Start();
        }

        protected internal override void OnStop()
        {
            ControllerReconnectFix.Stop();
        }
    }
}
