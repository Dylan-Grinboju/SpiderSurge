using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    internal sealed class AbilityHud
    {
        private GUIStyle titleStyle;
        private GUIStyle detailStyle;
        private GUIStyle timerStyle;
        private int fontVersion = -1;
        private string abilityName;
        private string status;
        private int lastSeconds = -1;
        private int lastState = -1;

        public void Draw(BaseAbility ability, Color readyColor, Color activeColor, Color cooldownColor)
        {
            if (Event.current.type != EventType.Repaint || !ability.IsUnlocked() || GameMenus.Open) return;
            var players = Players.All;
            if (players == null || players.Count == 0) return;
            var player = Players.ById(ability.PlayerId);
            if (player == null) return;
            bool alive = ability.IsPlayerAlive();
            bool active = alive && ability.IsActive();
            bool ready = alive && !active && !ability.IsOnCooldown();
            if (ModConfig.IndicatorShowOnlyWhenReady && !ready) return;

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
            abilityName ??= PerksManager.Instance.GetDisplayName(ability.PerkName);
            int state = !alive ? 0 : active ? 1 : ready ? 2 : 3;
            int seconds = Mathf.CeilToInt(active ? ability.ActiveRemaining : ability.CooldownRemaining);
            if (state != lastState || seconds != lastSeconds)
            {
                status = !alive ? "Down" : active ? "Active  " + Hud.Number(seconds) + "s" : ready ? "Ready" : "Cooldown  " + Hud.Number(seconds) + "s";
                lastState = state;
                lastSeconds = seconds;
            }

            int slot = 0;
            foreach (var entry in players)
                if (entry != null && entry.playerID.Value < ability.PlayerId) slot++;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.5f, 1.5f);
            float width = 268f * scale;
            float height = 76f * scale;
            float gap = 5f * scale;
            int columns = Mathf.Min(Mathf.CeilToInt(players.Count / 6f), Mathf.Max(1, Mathf.FloorToInt((Screen.width - 24f * scale) / (width + gap))));
            int rows = Mathf.CeilToInt(players.Count / (float)columns);
            int column = slot / rows;
            int row = slot % rows;
            float x = Screen.width - 16f * scale - (column + 1) * (width + gap);
            float y = Screen.height - 16f * scale - (rows - row) * (height + gap);
            var rect = new Rect(x, y, width, height);
            Color color = !alive ? Hud.Dim : active ? activeColor : ready ? readyColor : cooldownColor;
            float progress = !alive ? 0f : active ? ability.ActiveProgress : ability.CooldownProgress;
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

            if (ability is StorageAbility storage)
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
        }
    }
}
