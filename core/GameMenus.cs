using Doozy.Engine.UI;

namespace SpiderSurge.Common
{
    public static class GameMenus
    {
        // The pause menu (with the Mods menu and the Surge guide), the menus that pause the game and the perk choice
        public static bool Open
        {
            get
            {
                if (HudController.instance != null && HudController.instance.paused) return true;
                if (GameController.instance != null && GameController.instance.gamePaused) return true;
                UIView perkChoice = SurvivalModeHud.instance != null ? SurvivalModeHud.instance.perkChoiceView : null;
                return perkChoice != null && perkChoice.gameObject.activeInHierarchy && (perkChoice.IsVisible || perkChoice.IsShowing);
            }
        }

        // The game also turns player input off while a level loads or ends
        public static bool AcceptPlayerInput => !Open && (GameController.instance == null || GameController.instance.acceptingInputs);

        // A client checked its request against its own menus; on the host only the shared pause and input state can refuse it
        public static bool AcceptRemotePlayerInput => GameController.instance == null || (!GameController.instance.gamePaused && GameController.instance.acceptingInputs);
    }
}
