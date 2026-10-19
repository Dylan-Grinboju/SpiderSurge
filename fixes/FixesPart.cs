using SpiderSurge.Common;

namespace SpiderSurge.Fixes
{
    public class FixesPart : Part
    {
        public override string Key => "fixes";
        public override string Title => "Game fixes";
        public override string Description => "Fixes bugs in the game itself:\n• Reconnecting controllers keep their original spider.\n• Whisps no longer fire the moment they spawn.\n• Off-screen arrows no longer throw errors once their target is gone.\n• Pain-level colours unlock without an error on every lobby load.\n• The game starts without the custom-maps warning error.";
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
