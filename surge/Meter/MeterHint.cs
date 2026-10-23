using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Logger = Silk.Logger;
using M = SpiderSurge.Consts.Values.Meter;
using P = SpiderSurge.Consts.PerkNames;

namespace SpiderSurge
{
    // A line under the focused or locked card explains its meter charge or perk synergy.
    public class MeterHint : MonoBehaviour
    {
        public const string ObjectName = "SurgeMeterHint";
        private const float Width = 640f;
        private const float Height = 40f;
        private const float Gap = 10f;

        // Room the cards leave under them for the line, two lines of text at most
        public const float Reserve = Gap + 34f;

        private SurvivalModeHud _hud;
        private TMP_Text _text;
        private string _shownKey;
        private int _shownLevel = -1;

        public static MeterHint Current { get; private set; }

        public static void Clear()
        {
            PerkChoiceWidget.Remove(SurvivalModeHud.instance, ObjectName);
            Current = null;
        }

        public static void Build(SurvivalModeHud hud)
        {
            if (hud == null || hud.perkChoiceView == null || hud.perkCardsRoot == null) return;
            PerkChoiceWidget.Remove(hud, ObjectName);
            Current = null;
            var prefabCard = hud.perkCardPrefab != null ? hud.perkCardPrefab.GetComponent<SurvivalModifierChoiceCard>() : null;
            if (!SurgeMeter.Enabled || prefabCard == null || prefabCard.perkDescriptionText == null) return;

            try
            {
                GameObject textObject = Instantiate(prefabCard.perkDescriptionText.gameObject, hud.perkChoiceView.transform);
                textObject.name = ObjectName;
                var hint = textObject.AddComponent<MeterHint>();
                hint._hud = hud;
                hint._text = textObject.GetComponent<TMP_Text>();
                SettingsPage.SetText(hint._text, "");
                hint._text.raycastTarget = false;
                hint._text.fontSize = 13f;
                hint._text.enableAutoSizing = false;
                hint._text.alignment = TextAlignmentOptions.Top;
                hint._text.enableWordWrapping = true;
                hint._text.overflowMode = TextOverflowModes.Overflow;
                hint._text.color = Color.white;

                var rect = (RectTransform)textObject.transform;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(Width, Height);
                hint.Reposition();
                hint.Refresh();
                Current = hint;
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[Surge meter] could not build the perk screen hint: {ex}");
            }
        }

        // Under the confirm button when there is one, else under the cards
        public void Reposition()
        {
            if (this == null || _hud == null) return;
            var confirm = PerkConfirmButton.Current;
            Vector2 below = PerkChoiceWidget.BelowCards(_hud);
            float top = confirm != null
                ? confirm.transform.localPosition.y - M.ConfirmHeight - Gap
                : below.y - Gap;
            var parent = _hud.perkChoiceView.transform as RectTransform;
            if (parent != null && _text != null)
            {
                float space = 2f * Mathf.Min(below.x - parent.rect.xMin - M.SideMargin, parent.rect.xMax - M.SideMargin - below.x);
                _text.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(space, 1f, Width), Height);
            }
            transform.localPosition = new Vector3(below.x, top, 0f);
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_text == null) return;
            var card = FocusedCard();
            string key = card?.modifier?.data?.key;
            int level = (card?.modifier?.levelInSurvival ?? 0) + 1;
            if (key != null && ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Contains(key)) level = 2;
            if (key == _shownKey && level == _shownLevel) return;
            _shownKey = key;
            _shownLevel = level;
            _text.text = Compose(key, level);
        }

        // The card under the controller's focus, else the locked one
        private SurvivalModifierChoiceCard FocusedCard()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var card = selected != null ? selected.GetComponent<SurvivalModifierChoiceCard>() : null;
            if (card == null || card.transform.parent != _hud.perkCardsRoot) card = PerkConfirm.Locked;
            return card;
        }

        private static string Compose(string key, int level)
        {
            string synergy = PerkSynergy.Describe(key, level);
            if (synergy != null) return $"<color={Consts.Formatting.ColorGold}>Synergy:</color> {synergy}";
            string how = key != null ? HowItCharges(key) : null;
            if (how == null || PerksManager.Instance == null) return "";
            return $"<color={Consts.Formatting.ColorGold}>{PerksManager.Instance.GetDisplayName(key)}</color> charges the meter {how}.";
        }

        private static string HowItCharges(string key)
        {
            switch (key)
            {
                case P.PulseAbility: return "by launching enemies";
                case P.AmmoAbility: return "by defeating enemies while it is active";
                case P.ImmuneAbility: return "by blocking hits and defeating enemies while it is active";
                case P.StorageAbility: return "by retrieving stored weapons";
                case P.FreezeAbility: return "by freezing enemies and defeating them while frozen";
                case P.PulseAbilityUltimate: return "by hitting enemies with the detonation";
                case P.AmmoAbilityUltimate: return "by defeating enemies with the weapons it supplies";
                case P.ImmuneAbilityUltimate: return "by reviving teammates and letting its Wasps defeat enemies";
                case P.StorageAbilityUltimate: return "by swallowing enemies";
                case P.FreezeAbilityUltimate: return "by turning enemies into Wasps";
                default: return null;
            }
        }
    }
}
