using SpiderSurge.Common;

namespace SpiderSurge.Fixes
{
    public class FixesPart : Part
    {
        public override string Key => "fixes";
        public override string Title => "Game fixes";
        public override string Description => "Fixes bugs in the game itself: whisps no longer fire the moment they spawn, and off-screen arrows no longer throw errors once their target is gone.";
        public override string Namespace => "SpiderSurge.Fixes";
        public override bool OnByDefault => true;
    }
}
