using SpiderSurge.Common;

namespace SpiderSurge.Fixes
{
    public class FixesPart : Part
    {
        public override string Key => "fixes";
        public override string Title => "Game fixes";
        public override string Description => "Fixes bugs in the game itself: reconnecting controllers keep their original spider, whisps no longer fire the moment they spawn, off-screen arrows no longer throw errors once their target is gone, pain-level colours unlock without an error on every lobby load and the game starts without the custom-maps warning error.";
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
