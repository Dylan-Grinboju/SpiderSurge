using SpiderSurge.Common;

namespace SpiderSurge.Fixes
{
    public class FixesPart : Part
    {
        public override string Key => "fixes";
        public override string Title => "Game fixes";
        public override string Description => "Fixes bugs in the game itself:\n• Every controller can pause, even while another is sending input.\n• Reconnecting controllers keep their original spider.\n• Whisps no longer fire the moment they spawn.\n• Off-screen arrows no longer throw errors once their target is gone.\n• Pain-level colours unlock without an error on every lobby load.\n• The game starts without the custom-maps warning error.\n• Perk timeouts can choose every offered card.\n• Shotguns use every firing and hit sound.\n• A hidden connecting screen no longer blocks returning to the lobby.\n• Tiers of Heck and Parkour no longer count toward the last pain level's high score and leaderboard.";
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
