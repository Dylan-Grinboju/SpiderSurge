using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Logger = Silk.Logger;
using M = SpiderSurge.Consts.Values.Meter;
using P = SpiderSurge.Consts.PerkNames;

namespace SpiderSurge
{
    // A line under the perk cards: a focused or locked ability or ultimate card says how it charges the meter
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
            string key = FocusedKey();
            if (key == _shownKey) return;
            _shownKey = key;
            _text.text = Compose(key);
        }

        // The card under the controller's focus, else the locked one
        private string FocusedKey()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var card = selected != null ? selected.GetComponent<SurvivalModifierChoiceCard>() : null;
            if (card == null || card.transform.parent != _hud.perkCardsRoot) card = PerkConfirm.Locked;
            return card != null && card.modifier != null && card.modifier.data != null ? card.modifier.data.key : null;
        }

        private static string Compose(string key)
        {
            string how = key != null ? HowItCharges(key) : null;
            if (how == null || PerksManager.Instance == null) return "";
            return $"<color={Consts.Formatting.ColorGold}>{PerksManager.Instance.GetDisplayName(key)}</color> charges the meter {how}.";
        }

        // From the same numbers the abilities charge with
        private static string HowItCharges(string key)
        {
            switch (key)
            {
                case P.PulseAbility: return $"{M.PulseChargePerEnemy} for each enemy it launches, {Cap(M.PulseChargePerEnemy, M.PulseCapPerCast)}";
                case P.AmmoAbility: return $"1 for every {M.ClampedShotsPerCharge} shots that would have cost ammo, {Cap(1, M.BottomlessClipCapPerCast)}";
                case P.ImmuneAbility: return $"{M.NullFieldChargePerHit} for each hit it blocks (at most {Mathf.RoundToInt(1f / M.NullFieldHitInterval)} a second), {Cap(M.NullFieldChargePerHit, M.NullFieldCapPerCast)}";
                case P.StorageAbility: return $"{M.PocketDimensionChargePerRecall} for recalling a weapon that still has ammo, {Cap(M.PocketDimensionChargePerRecall, M.PocketDimensionCapPerCast)}";
                case P.FreezeAbility: return $"{M.FreezeChargePerEnemy} for each enemy it freezes, {Cap(M.FreezeChargePerEnemy, M.FreezeCapPerCast)}";
                case P.PulseAbilityUltimate: return $"{M.DetonationChargePerKill} for each enemy in the death zone, {Cap(M.DetonationChargePerKill, M.DetonationCapPerCast)}, and {M.PulseChargePerEnemy} for each enemy it only launches, up to {M.PulseCapPerCast}";
                case P.AmmoAbilityUltimate: return $"{M.CarePackageChargePerPickup} for each weapon it drops that anyone picks up, {Cap(M.CarePackageChargePerPickup, M.CarePackageCapPerCast)}";
                case P.ImmuneAbilityUltimate: return $"{M.NeuralBackupChargePerEvent} for each player it revives and each kill by its friendly Wasps, {Cap(M.NeuralBackupChargePerEvent, M.NeuralBackupCapPerCast)}";
                case P.StorageAbilityUltimate: return $"{M.BanishChargePerEnemy} for banishing an enemy, {Cap(M.BanishChargePerEnemy, M.BanishCapPerCast)}";
                case P.FreezeAbilityUltimate: return $"{M.RewindChargePerEnemy} for each enemy it turns into a Wasp, {Cap(M.RewindChargePerEnemy, M.RewindCapPerCast)}";
                default: return null;
            }
        }

        private static string Cap(int charge, int cap) => cap <= charge ? "once per cast" : $"up to {cap} per cast";
    }
}
