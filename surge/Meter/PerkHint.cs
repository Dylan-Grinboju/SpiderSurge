using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Logger = Silk.Logger;
using M = SpiderSurge.Consts.Values.Chaos;

namespace SpiderSurge
{
    // A line under the focused or locked card explains its perk synergy, or warns about the ability's downside
    public class PerkHint : MonoBehaviour
    {
        public const string ObjectName = "SurgePerkHint";
        private const float Width = 640f;
        private const float Height = 68f;
        private const float Gap = 10f;

        // Room the cards leave under them for the line, two lines of text at most
        public const float Reserve = Gap + Height;

        private SurvivalModeHud _hud;
        private TMP_Text _text;
        private string _shownKey;
        private int _shownLevel = -1;
        private int _shownCurrentLevel = -1;
        private int _selectionStamp = -1;

        public static PerkHint Current { get; private set; }

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
            if (!ChaosWallet.Enabled || prefabCard == null || prefabCard.perkDescriptionText == null) return;

            try
            {
                GameObject textObject = Instantiate(prefabCard.perkDescriptionText.gameObject, hud.perkChoiceView.transform);
                textObject.name = ObjectName;
                var hint = textObject.AddComponent<PerkHint>();
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
                Logger.LogError($"[Chaos] could not build the perk screen hint: {ex}");
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
            int currentLevel = card?.modifier?.levelInSurvival ?? 0;
            int level = currentLevel + 1;
            if (key != null && ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Contains(key)) level = 2;
            int selection = PerkSynergy.SelectionStamp;
            if (selection != _selectionStamp && !AbilitySelection.IsChoosing)
                foreach (var choice in PerkChoiceWidget.Cards(_hud))
                    if (choice.modifier?.data != null)
                        SurvivalModifierChoiceCard_SetupCard_Patch.Postfix(choice, choice.modifier, choice.level, GameFields.CardIndex(choice), false);
            if (key == _shownKey && level == _shownLevel && currentLevel == _shownCurrentLevel && selection == _selectionStamp) return;
            _shownKey = key;
            _shownLevel = level;
            _shownCurrentLevel = currentLevel;
            _selectionStamp = selection;
            _text.text = Compose(key, level, currentLevel);
        }

        // The card under the controller's focus, else the locked one
        private SurvivalModifierChoiceCard FocusedCard()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var card = selected != null ? selected.GetComponent<SurvivalModifierChoiceCard>() : null;
            if (card == null || card.transform.parent != _hud.perkCardsRoot) card = PerkConfirm.Locked;
            return card;
        }

        private static string Compose(string key, int level, int currentLevel)
        {
            string detail = key == Consts.PerkNames.AmmoAbility
                ? "Overcharge uses the held weapon without spending ammo for a short burst, then burns it out."
                : PerkSynergy.Describe(key, level, currentLevel);
            if (AbilitySelection.StartingChoiceOpen)
                return "Each player chooses independently; duplicate picks are allowed.\n" +
                    (AbilitySelection.Round.Ultimate ? "Your ultimate shares a cooldown with your regular ability."
                        : SurgePainLevels.SelectedEight ? "A separate ultimate choice follows."
                        : $"Choose an independent ultimate after wave {Consts.Values.Waves.UltUpgradeWave}.") +
                    (string.IsNullOrEmpty(detail) ? "" : "\n" + detail);
            return detail ?? "Perks apply to the whole team. Chaos buys extra upgrades in the shop.";
        }
    }
}
