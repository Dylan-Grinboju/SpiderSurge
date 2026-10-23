using HarmonyLib;
using UnityEngine;

namespace SpiderSurge
{
    public sealed class PerkChoiceHud : MonoBehaviour
    {
        private SurvivalModeHud _hud;
        private ActiveModifiersIndicator _indicator;
        private Layer _lives;
        private Layer _perks;
        private bool _refreshPerks;

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

            bool choosing = SurgeGameModeManager.IsSurgeRunActive && (_hud.perkChoiceView.IsVisible || _hud.perkChoiceView.IsShowing);
            if (!choosing)
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

    [HarmonyPatch(typeof(PerksManager), nameof(PerksManager.ApplyRemote))]
    internal static class PerksManager_ApplyRemote_ChoiceHudPatch
    {
        [HarmonyPostfix]
        private static void Postfix() => PerkChoiceHud.RefreshPerks();
    }
}
