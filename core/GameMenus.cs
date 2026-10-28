using Doozy.Engine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpiderSurge.Common
{
    public static class GameMenus
    {
        // The pause menu (with the Mods menu and the Surge guide), the menus that pause the game and the perk choice
        public static bool Open
        {
            get
            {
                if (ChallengeController.Pending) return true;
                if (HudController.instance != null && HudController.instance.paused) return true;
                if (GameController.instance != null && GameController.instance.gamePaused) return true;
                return PerkChoiceOpen;
            }
        }

        public static bool PerkChoiceOpen
        {
            get
            {
                UIView perkChoice = SurvivalModeHud.instance != null ? SurvivalModeHud.instance.perkChoiceView : null;
                return perkChoice != null && perkChoice.gameObject.activeInHierarchy && (perkChoice.IsVisible || perkChoice.IsShowing);
            }
        }

        // The D-pad moves through a menu while one is open or a menu button has controller focus, in the main menu and lobby screens too
        public static bool Navigating
        {
            get
            {
                if (Open) return true;
                GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (selected == null || !selected.activeInHierarchy) return false;
                Selectable selectable = selected.GetComponent<Selectable>();
                return selectable != null && selectable.IsInteractable();
            }
        }

        // The game also turns player input off while a level loads or ends
        public static bool AcceptPlayerInput => !Open && (GameController.instance == null || GameController.instance.acceptingInputs);

        // A client checked its request against its own menus; on the host only the shared pause and input state can refuse it
        public static bool AcceptRemotePlayerInput => !ChallengeController.Pending && (GameController.instance == null || (!GameController.instance.gamePaused && GameController.instance.acceptingInputs));
    }
}
