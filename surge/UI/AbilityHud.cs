using System.Collections.Generic;
using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    // The compact ability HUD: one row per player in the bottom right corner instead of the ring over each spider
    internal sealed class AbilityHud
    {
        private static readonly Dictionary<int, AbilityHud> Rows = new Dictionary<int, AbilityHud>();
        private static readonly List<int> Gone = new List<int>();
        private static readonly Vector3[] Corners = new Vector3[4];

        private BaseAbility ability;
        private string perkName;
        private GUIStyle titleStyle;
        private GUIStyle detailStyle;
        private GUIStyle timerStyle;
        private int fontVersion = -1;
        private string abilityName;
        private string status;
        private int lastSeconds = -1;
        private int lastState = -1;
        private string lastRestriction;
        private float readyAt = -10f;

        public static void Ready(BaseAbility tracked)
        {
            if (Rows.TryGetValue(tracked.PlayerId, out var row) && row.ability == tracked) row.readyAt = Time.time;
        }

        // A dead player's spider is destroyed with its ability, so the row stays and shows Down until the next spider's ability takes it over
        public static void Track(BaseAbility tracked)
        {
            if (tracked == null || tracked.PlayerId < 0) return;
            if (!Rows.TryGetValue(tracked.PlayerId, out AbilityHud row))
            {
                row = new AbilityHud();
                Rows[tracked.PlayerId] = row;
            }
            if (row.perkName != tracked.PerkName)
            {
                row.perkName = tracked.PerkName;
                row.abilityName = null;
                row.lastState = -1;
            }
            row.ability = tracked;
            AbilityHudDrawer.Ensure();
        }

        public static void DrawAll(Color readyColor, Color activeColor, Color cooldownColor)
        {
            if (Event.current.type != EventType.Repaint || GameMenus.Open || Rows.Count == 0) return;
            var players = Players.All;
            if (players == null || players.Count == 0) return;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.5f, 1.5f);
            float bottom = BottomInset(scale);
            foreach (var pair in Rows)
            {
                if (!pair.Value.Draw(pair.Key, players, scale, bottom, readyColor, activeColor, cooldownColor)) Gone.Add(pair.Key);
            }
            foreach (int playerId in Gone)
            {
                Rows.Remove(playerId);
            }
            Gone.Clear();
        }

        // The game's pain-level number sits in the bottom right corner, so the rows start above it while it shows
        private static float BottomInset(float scale)
        {
            float inset = 16f * scale;
            var text = SurvivalModeHud.instance != null ? SurvivalModeHud.instance.painLevelText : null;
            if (text == null || !text.isActiveAndEnabled) return inset;
            // The topmost RectTransform is the HUD canvas, which covers the whole screen
            RectTransform canvas = null;
            for (Transform parent = text.transform; parent != null; parent = parent.parent)
                if (parent is RectTransform rect) canvas = rect;
            Rect area = canvas.rect;
            if (area.height <= 0f) return inset;
            text.rectTransform.GetWorldCorners(Corners);
            float top = (canvas.InverseTransformPoint(Corners[1]).y - area.yMin) / area.height * Screen.height;
            return Mathf.Max(inset, top + 6f * scale);
        }

        // False once the row is no longer needed: its ability is gone and its player left or the ability was lost at the end of the run
        private bool Draw(int playerId, List<PlayerController> players, float scale, float bottom, Color readyColor, Color activeColor, Color cooldownColor)
        {
            bool exists = ability != null;
            var player = Players.ById(playerId);
            bool unlocked = PerksManager.Instance != null && PerksManager.Instance.GetPerkLevel(perkName) > 0;
            if (player == null || !unlocked) return exists;
            bool alive = exists && ability.IsPlayerAlive();
            bool active = alive && ability.IsActive();
            bool available = alive && ability.HasAvailableAction;
            bool ready = available && !active && !ability.IsOnCooldown();
            if (ModConfig.IndicatorShowOnlyWhenReady && !ready) return true;

            Hud.EnsureFonts();
            if (titleStyle == null || fontVersion != Hud.FontVersion)
            {
                titleStyle = Hud.Style(14, true);
                detailStyle = Hud.Style(12);
                timerStyle = Hud.Style(12, false, TextAnchor.MiddleRight);
                titleStyle.richText = detailStyle.richText = timerStyle.richText = false;
                fontVersion = Hud.FontVersion;
            }
            string name = Players.NameOf(player);
            abilityName ??= PerksManager.Instance.GetDisplayName(perkName);
            string restriction = alive ? ability.ActionRestriction : "";
            int state = !alive ? 0 : active ? 1 : !available ? 4 : ready ? 2 : 3;
            int seconds = !alive ? 0 : Mathf.CeilToInt(active ? ability.ActiveRemaining : ability.CooldownRemaining);
            if (state != lastState || seconds != lastSeconds || restriction != lastRestriction)
            {
                status = !alive ? "Down" : active ? "Active  " + Hud.Number(seconds) + "s" : !available ? restriction : ready ? "Ready" : "Cooldown  " + Hud.Number(seconds) + "s";
                if (available && restriction.Length > 0) status = restriction + " · " + status;
                lastState = state;
                lastSeconds = seconds;
                lastRestriction = restriction;
            }

            int slot = 0;
            foreach (var entry in players)
                if (entry != null && entry.playerID.Value < playerId) slot++;
            float width = 268f * scale;
            float height = 76f * scale;
            float gap = 5f * scale;
            int columns = Mathf.Min(Mathf.CeilToInt(players.Count / 6f), Mathf.Max(1, Mathf.FloorToInt((Screen.width - 24f * scale) / (width + gap))));
            int rows = Mathf.CeilToInt(players.Count / (float)columns);
            int column = slot / rows;
            int row = slot % rows;
            float x = Screen.width - 16f * scale - (column + 1) * (width + gap);
            float y = Screen.height - bottom - (rows - row) * (height + gap);
            var rect = new Rect(x, y, width, height);
            Color color = !alive || !available && !active ? Hud.Dim : active ? activeColor : ready ? readyColor : cooldownColor;
            float progress = !alive || !available && !active ? 0f : active ? ability.ActiveProgress : ability.CooldownProgress;
            Hud.Fill(rect, Hud.Ink);
            Hud.Fill(new Rect(x, y, 3f * scale, height), Hud.Readable(player.playerColor.Value));
            Hud.Resize(titleStyle, 14, scale);
            Hud.Resize(detailStyle, 12, scale);
            Hud.Resize(timerStyle, 12, scale);
            Hud.Label(new Rect(x + 12f * scale, y + 5f * scale, width - 58f * scale, 22f * scale), name, titleStyle, Hud.Readable(player.playerColor.Value));
            Hud.Label(new Rect(x + 12f * scale, y + 27f * scale, width - 24f * scale, 19f * scale), abilityName, detailStyle, Hud.Text);
            Hud.Label(new Rect(x + 12f * scale, y + 45f * scale, width - 24f * scale, 18f * scale), status, timerStyle, color);
            var bar = new Rect(x + 12f * scale, y + height - 9f * scale, width - 24f * scale, 3f * scale);
            Hud.Fill(bar, Hud.Line);
            bar.width *= progress;
            Hud.Fill(bar, color);
            float glint = Mathf.Clamp01(1f - (Time.time - readyAt) / 0.22f);
            if (ready && glint > 0f)
                Hud.Fill(new Rect(bar.xMax - 5f * scale, bar.y - 1f * scale, 5f * scale, bar.height + 2f * scale), new Color(1f, 1f, 1f, glint * (ModConfig.ReducedEffects ? 0.3f : 0.8f)));

            if (exists && ability is StorageAbility storage)
            {
                Sprite sprite = storage.StoredWeaponSprite;
                var preview = new Rect(x + width - 38f * scale, y + 5f * scale, 26f * scale, 22f * scale);
                if (sprite != null)
                {
                    Rect uv = sprite.textureRect;
                    float fit = Mathf.Min(preview.width / uv.width, preview.height / uv.height);
                    preview = new Rect(preview.center.x - uv.width * fit / 2f, preview.center.y - uv.height * fit / 2f, uv.width * fit, uv.height * fit);
                    GUI.DrawTextureWithTexCoords(preview, sprite.texture, new Rect(uv.x / sprite.texture.width, uv.y / sprite.texture.height, uv.width / sprite.texture.width, uv.height / sprite.texture.height));
                }
                else Hud.Label(new Rect(x + width - 60f * scale, y + 5f * scale, 48f * scale, 22f * scale), "Empty", timerStyle, Hud.Muted);
            }
            return true;
        }
    }

    // One object draws every row, and it is disabled while the HUD is off so Unity makes no OnGUI call for it
    internal sealed class AbilityHudDrawer : MonoBehaviour
    {
        private static AbilityHudDrawer instance;
        private Color readyColor;
        private Color activeColor;
        private Color cooldownColor;

        public static void Ensure()
        {
            if (instance != null) return;
            var holder = new GameObject("SurgeAbilityHud");
            DontDestroyOnLoad(holder);
            instance = holder.AddComponent<AbilityHudDrawer>();
        }

        private void Awake()
        {
            Settings.Changed += Changed;
            ApplySettings();
        }

        private void OnDestroy()
        {
            Settings.Changed -= Changed;
        }

        private void Changed(string key)
        {
            if (Settings.Affects(key, "surge.indicator") || key.StartsWith("surge.indicator.")) ApplySettings();
        }

        private void ApplySettings()
        {
            readyColor = ModConfig.IndicatorAvailableColor;
            activeColor = ModConfig.IndicatorActiveColor;
            cooldownColor = ModConfig.IndicatorCooldownColor;
            enabled = ModConfig.IndicatorCompactHud;
        }

        private void OnGUI()
        {
            AbilityHud.DrawAll(readyColor, activeColor, cooldownColor);
        }
    }
}
