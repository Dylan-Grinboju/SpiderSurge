using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpiderSurge
{
    public sealed class PerkChoiceHud : MonoBehaviour
    {
        private SurvivalModeHud _hud;
        private ActiveModifiersIndicator _indicator;
        private Layer _lives;
        private Layer _perks;
        private bool _refreshPerks;
        private GameObject _focus;

        private sealed class Layer
        {
            public readonly Canvas Canvas;
            private readonly bool _enabled;
            private readonly bool _overrideSorting;
            private readonly int _order;
            private readonly int _layer;

            public Layer(GameObject target)
            {
                Canvas = target.GetComponent<Canvas>();
                if (Canvas == null) Canvas = target.AddComponent<Canvas>();
                _enabled = Canvas.enabled;
                _overrideSorting = Canvas.overrideSorting;
                _order = Canvas.sortingOrder;
                _layer = Canvas.sortingLayerID;
            }

            public void Raise(Canvas above)
            {
                if (Canvas == null || above == null) return;
                Canvas.enabled = true;
                Canvas.overrideSorting = true;
                Canvas.sortingLayerID = above.sortingLayerID;
                Canvas.sortingOrder = above.sortingOrder + 1;
            }

            public void Restore()
            {
                if (Canvas == null) return;
                Canvas.enabled = _enabled;
                Canvas.overrideSorting = _overrideSorting;
                Canvas.sortingOrder = _order;
                Canvas.sortingLayerID = _layer;
            }
        }

        public static void Attach(SurvivalModeHud hud)
        {
            if (hud == null) return;
            var status = hud.GetComponent<PerkChoiceHud>() ?? hud.gameObject.AddComponent<PerkChoiceHud>();
            status._hud = hud;
        }

        public static void RefreshPerks()
        {
            if (!SurgeGameModeManager.IsSurgeRunActive || SurvivalModeHud.instance == null) return;
            Attach(SurvivalModeHud.instance);
            SurvivalModeHud.instance.GetComponent<PerkChoiceHud>()._refreshPerks = true;
        }

        private void LateUpdate()
        {
            if (_hud == null || _hud.perkChoiceView == null) return;
            if (_indicator == null)
            {
                _indicator = _hud.perkChoiceView.transform.parent.GetComponentInChildren<ActiveModifiersIndicator>(true);
            }
            if (_refreshPerks && _indicator != null && ModifierManager.instance != null && SurvivalMode.instance != null)
            {
                _refreshPerks = false;
                _indicator.UpdateActiveMods();
            }

            bool choosing = (HudController.instance == null || !HudController.instance.paused)
                && (_hud.perkChoiceView.IsVisible || _hud.perkChoiceView.IsShowing);
            if (!choosing)
            {
                Restore();
                return;
            }
            if (!AbilitySelection.IsChoosing) KeepFocus();
            if (!SurgeGameModeManager.IsSurgeRunActive)
            {
                Restore();
                return;
            }

            Canvas choice = _hud.perkChoiceView.GetComponent<Canvas>();
            if (choice == null) return;
            Canvas above = choice.overrideSorting ? choice : choice.rootCanvas;
            if ((_lives == null || _lives.Canvas == null) && _hud.livesCounter != null)
                _lives = new Layer(_hud.livesCounter.transform.parent.gameObject);
            if ((_perks == null || _perks.Canvas == null) && _indicator != null)
                _perks = new Layer(_indicator.gameObject);
            _lives?.Raise(above);
            _perks?.Raise(above);
        }

        // Doozy selects any button the mouse passes over, the disabled Confirm and Buy included, and the pause menu or a click on the background leaves nothing selected; either way a pad has no card to move from, so the last usable control keeps the focus
        private void KeepFocus()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || PerkChoiceWidget.ChoiceMade || _hud.perkChoiceView.IsHiding) return;
            Transform view = _hud.perkChoiceView.transform;
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy)
            {
                if (!selected.transform.IsChildOf(view)) return;
                Selectable selectable = selected.GetComponent<Selectable>();
                if (selectable == null || selectable.IsInteractable())
                {
                    _focus = selected;
                    return;
                }
                if (selected.GetComponent<PerkActionButton>() == null) return;
            }
            GameObject locked = PerkConfirm.Locked != null && PerkConfirm.Locked.cardButton != null ? PerkConfirm.Locked.cardButton.gameObject : null;
            Selectable first = PerkChoiceWidget.FirstCard(_hud);
            GameObject target = Usable(_focus, view) ? _focus
                : Usable(locked, view) ? locked
                : Usable(_hud.perkChoiceView.SelectedButton, view) ? _hud.perkChoiceView.SelectedButton
                : first != null ? first.gameObject : null;
            if (target != null && target != selected) eventSystem.SetSelectedGameObject(target);
        }

        private static bool Usable(GameObject target, Transform view)
        {
            if (target == null || !target.activeInHierarchy || !target.transform.IsChildOf(view)) return false;
            Selectable selectable = target.GetComponent<Selectable>();
            return selectable != null && selectable.IsInteractable();
        }

        private void OnDisable() => Restore();

        private void Restore()
        {
            _lives?.Restore();
            _perks?.Restore();
        }
    }

    [HarmonyPatch(typeof(ModifierManager), "SetModLevelClientRpc")]
    internal static class ModifierManager_SetModLevelClientRpc_ChoiceHudPatch
    {
        [HarmonyPostfix]
        private static void Postfix() => PerkChoiceHud.RefreshPerks();
    }
}
