using UnityEngine;
using SpiderSurge.Common;
using Logger = Silk.Logger;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StatsMod
{
    public class BigUI : MonoBehaviour
    {
        private enum Tone { Good, Bad, Warm, Plain }

        private sealed class Column
        {
            public string Label;
            public string Header;
            public float Width;
            public Func<bool> Shown;
            public Tone Tone;
            public Func<PlayerTracker.PlayerData, int> Count;
            public Func<PlayerTracker.PlayerData, TimeSpan> Time;
            public Func<PlayerTracker.PlayerData, WeaponAccuracy> Accuracy;
        }

        private static Column Count(string label, float width, Func<bool> shown, Tone tone, Func<PlayerTracker.PlayerData, int> count) =>
            new Column { Label = label, Width = width, Shown = shown, Tone = tone, Count = count };

        private static Column Duration(string label, float width, Func<bool> shown, Func<PlayerTracker.PlayerData, TimeSpan> time) =>
            new Column { Label = label, Width = width, Shown = shown, Tone = Tone.Plain, Time = time };

        private static Column Enemy(string label, string key, Func<bool> shown) =>
            Count(label, COL_WIDTH_ENEMY_KILLS, shown, Tone.Good, p => p.EnemyKills.TryGetValue(key, out int v) ? v : 0);

        private static Column Weapon(string label, string key, Func<bool> shown) =>
            Count(label, COL_WIDTH_WEAPON_KILLS, shown, Tone.Good, p => p.WeaponHits.TryGetValue(key, out int v) ? v : 0);

        private const float COL_WIDTH_ENEMY_KILLS = 100f;
        private const float COL_WIDTH_WEAPON_KILLS = 100f;

        private static readonly Column[] Columns = new Column[]
        {
            Count("Total Offence", 100f, () => ModConfig.BigUIShowTotalOffence, Tone.Good, p => p.Kills + p.EnemyShieldsTakenDown),
            Count("Friendly Hits", 100f, () => ModConfig.BigUIShowTotalFriendlyHits, Tone.Warm, p => p.FriendlyKills + p.FriendlyShieldsHit),
            Count("Hits Taken", 90f, () => ModConfig.BigUIShowTotalHitsTaken, Tone.Bad, p => p.Deaths + p.ShieldsLost),
            Count("Kills", 80f, () => ModConfig.BigUIShowKills, Tone.Good, p => p.Kills),
            Count("Deaths", 90f, () => ModConfig.BigUIShowDeaths, Tone.Bad, p => p.Deaths),
            Count("Max Kill Streak", 100f, () => ModConfig.BigUIShowMaxKillStreak, Tone.Good, p => p.MaxKillStreak),
            Count("Kill Streak", 100f, () => ModConfig.BigUIShowCurrentKillStreak, Tone.Good, p => p.KillStreak),
            Count("Max Solo Streak", 110f, () => ModConfig.BigUIShowMaxSoloKillStreak, Tone.Good, p => p.MaxKillStreakWhileSolo),
            Count("Solo Streak", 110f, () => ModConfig.BigUIShowCurrentSoloKillStreak, Tone.Good, p => p.KillStreakWhileSolo),
            Count("Friendly Kills", 110f, () => ModConfig.BigUIShowFriendlyKills, Tone.Warm, p => p.FriendlyKills),
            Count("Enemy Shields", 110f, () => ModConfig.BigUIShowEnemyShields, Tone.Good, p => p.EnemyShieldsTakenDown),
            Count("Shields Lost", 100f, () => ModConfig.BigUIShowShieldsLost, Tone.Bad, p => p.ShieldsLost),
            Count("Friendly Shields", 110f, () => ModConfig.BigUIShowFriendlyShields, Tone.Warm, p => p.FriendlyShieldsHit),
            Count("Wave Clutches", 110f, () => ModConfig.BigUIShowWaveClutches, Tone.Good, p => p.WaveClutches),
            Duration("Alive Time", 120f, () => ModConfig.BigUIShowAliveTime, p => p.GetCurrentAliveTime()),
            Count("Web Swings", 100f, () => ModConfig.BigUIShowWebSwings, Tone.Good, p => p.WebSwings),
            Duration("Web Swing Time", 120f, () => ModConfig.BigUIShowWebSwingTime, p => p.GetCurrentWebSwingTime()),
            Duration("Airborne Time", 120f, () => ModConfig.BigUIShowAirborneTime, p => p.GetCurrentAirborneTime()),
            Count("Lava Deaths", 100f, () => ModConfig.BigUIShowLavaDeaths, Tone.Bad, p => p.LavaDeaths),
            Count("Astral Returns", 110f, () => ModConfig.BigUIShowAstralReturns, Tone.Good, p => p.AstralReturns),
            new Column { Label = "Accuracy", Width = 110f, Shown = () => ModConfig.ShowAccuracy("overall"), Accuracy = p => p.ShootingAccuracy },

            Enemy("Wasp", "Wasp", () => ModConfig.BigUIShowWaspKills),
            Enemy("Wasp Cocoon", "Wasp Cocoon", () => ModConfig.BigUIShowWaspCocoonKills),
            Enemy("Power Wasp", "Power Wasp", () => ModConfig.BigUIShowPowerWaspKills),
            Enemy("Roller", "Roller", () => ModConfig.BigUIShowRollerKills),
            Enemy("Whisp", "Whisp", () => ModConfig.BigUIShowWhispKills),
            Enemy("Power Whisp", "Power Whisp", () => ModConfig.BigUIShowPowerWhispKills),
            Enemy("Melee Whisp", "Melee Whisp", () => ModConfig.BigUIShowMeleeWhispKills),
            Enemy("Power Melee Whisp", "Power Melee Whisp", () => ModConfig.BigUIShowPowerMeleeWhispKills),
            Enemy("Khepri", "Khepri", () => ModConfig.BigUIShowKhepriKills),
            Enemy("Power Khepri", "Power Khepri", () => ModConfig.BigUIShowPowerKhepriKills),
            Enemy("Hornet Shaman", "Hornet Shaman", () => ModConfig.BigUIShowHornetShamanKills),
            Enemy("Hornet", "Hornet", () => ModConfig.BigUIShowHornetKills),

            Weapon("Shotgun", "Shotgun", () => ModConfig.BigUIShowShotgunKills),
            Weapon("RailShot", "RailShot", () => ModConfig.BigUIShowRailShotKills),
            Weapon("Death Cube", "DeathCube", () => ModConfig.BigUIShowDeathCubeKills),
            Weapon("Death Ray", "DeathRay", () => ModConfig.BigUIShowDeathRayKills),
            Weapon("Energy Ball", "EnergyBall", () => ModConfig.BigUIShowEnergyBallKills),
            Weapon("Particle Blade", "Particle Blade", () => ModConfig.BigUIShowParticleBladeKills),
            Weapon("Khepri Staff", "KhepriStaff", () => ModConfig.BigUIShowKhepriStaffKills),
            Weapon("Laser Cannon", "Laser Cannon", () => ModConfig.BigUIShowLaserCannonKills),
            Weapon("Laser Cube", "Laser Cube", () => ModConfig.BigUIShowLaserCubeKills),
            Weapon("Saw Disc", "SawDisc", () => ModConfig.BigUIShowSawDiscKills),
            Weapon("Explosions", "Explosions", () => ModConfig.BigUIShowExplosionKills),
        }.Concat(WeaponAccuracy.Weapons.Select(weapon => new Column
        {
            Label = weapon.Label + " Accuracy",
            Width = 130f,
            Shown = () => ModConfig.ShowAccuracy(weapon.Key),
            Accuracy = p => p.AccuracyByWeapon.TryGetValue(weapon.Key, out var accuracy) ? accuracy : null
        })).ToArray();

        #region Base Dimensions
        public float BASE_PADDING = 28f;
        public float BASE_TITLE_HEIGHT = 64f;
        public float BASE_SECTION_SPACING = 22f;
        public float BASE_COLUMN_HEADER_HEIGHT = 44f;
        public float BASE_PLAYER_ROW_HEIGHT = 42f;
        public float BASE_HEADER_LINE_HEIGHT = 2f;
        public float BASE_TILE_WIDTH = 190f;
        public float BASE_MIN_WIDTH = 620f;
        public float BASE_MAX_WIDTH_PERCENT = 0.94f;
        public float BASE_MAX_HEIGHT_PERCENT = 0.92f;
        public float COL_WIDTH_PLAYER_NAME = 190f;
        #endregion

        #region Font Sizes
        public int CAPTION_FONT_SIZE = 14;
        public int TITLE_FONT_SIZE = 34;
        public int TILE_VALUE_FONT_SIZE = 28;
        public int COLUMN_HEADER_FONT_SIZE = 14;
        public int NAME_FONT_SIZE = 19;
        public int VALUE_FONT_SIZE = 22;
        #endregion

        #region Text Labels
        public string LABEL_PLAYER = "PLAYER";
        public string LABEL_RUN_TIME = "RUN TIME";
        public string LABEL_MATCH_TIME = "MATCH TIME";
        public string LABEL_LAST_GAME = "LAST GAME";
        public string LABEL_ENEMIES_KILLED = "ENEMIES KILLED";
        public string LABEL_NO_PLAYERS = "No players connected";
        public string LABEL_NO_GAMES = "--:--";
        #endregion

        #region UI State
        private bool isDisplayVisible = false;
        private bool isDisplayVisibleAtAll => ModConfig.ShowStatsWindow;
        private readonly List<Column> visibleColumns = new List<Column>();
        private readonly Dictionary<(Column, int), (long Seconds, string Text)> timeTexts = new Dictionary<(Column, int), (long Seconds, string Text)>();
        #endregion

        #region GUI Styles
        private GUIStyle captionStyle;
        private GUIStyle captionRightStyle;
        private GUIStyle titleStyle;
        private GUIStyle tileValueStyle;
        private GUIStyle columnHeaderStyle;
        private GUIStyle playerHeaderStyle;
        private GUIStyle nameStyle;
        private GUIStyle valueStyle;
        private GUIStyle emptyStyle;
        private int fontVersion = -1;
        #endregion

        #region Display Control
        public void ToggleDisplay()
        {
            isDisplayVisible = !isDisplayVisible;
        }

        public void ShowDisplay()
        {
            isDisplayVisible = true;
        }

        public void HideDisplay()
        {
            isDisplayVisible = false;
        }

        public bool IsVisible()
        {
            return isDisplayVisibleAtAll && isDisplayVisible;
        }
        #endregion

        private void InitializeStyles()
        {
            Hud.EnsureFonts();
            if (fontVersion == Hud.FontVersion && captionStyle != null) return;

            captionStyle = Hud.Style(CAPTION_FONT_SIZE, bold: true);
            captionRightStyle = Hud.Style(CAPTION_FONT_SIZE, bold: true, anchor: TextAnchor.MiddleRight);
            titleStyle = Hud.Style(TITLE_FONT_SIZE, bold: true, anchor: TextAnchor.UpperLeft);
            tileValueStyle = Hud.Style(TILE_VALUE_FONT_SIZE, bold: true, anchor: TextAnchor.UpperRight);
            columnHeaderStyle = Hud.Style(COLUMN_HEADER_FONT_SIZE, bold: true, anchor: TextAnchor.LowerCenter, wrap: true);
            playerHeaderStyle = Hud.Style(COLUMN_HEADER_FONT_SIZE, bold: true, anchor: TextAnchor.LowerLeft);
            nameStyle = Hud.Style(NAME_FONT_SIZE, bold: true);
            valueStyle = Hud.Style(VALUE_FONT_SIZE, bold: true, anchor: TextAnchor.MiddleCenter);
            emptyStyle = Hud.Style(NAME_FONT_SIZE, anchor: TextAnchor.MiddleCenter);

            fontVersion = Hud.FontVersion;
        }

        #region GUI Drawing
        private void OnGUI()
        {
            if (!isDisplayVisibleAtAll || !isDisplayVisible || Event.current.type != EventType.Repaint || GameMenus.Open) return;

            InitializeStyles();

            Color originalColor = GUI.color;
            GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, ModConfig.BigUIOpacity / 100f);

            try
            {
                var statsSnapshot = GameSessionManager.Instance.GetDisplaySnapshot();
                Draw(statsSnapshot);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error displaying player stats: {ex.Message}");
            }

            GUI.color = originalColor;
        }

        private void Draw(GameStatsSnapshot statsSnapshot)
        {
            visibleColumns.Clear();
            float columnsWidth = COL_WIDTH_PLAYER_NAME;
            foreach (Column column in Columns)
            {
                if (column.Shown())
                {
                    visibleColumns.Add(column);
                    columnsWidth += column.Width;
                }
            }

            bool showTiles = ModConfig.ShowPlayTime || ModConfig.ShowEnemyDeaths;
            bool showTable = ModConfig.ShowPlayers;
            int playerCount = statsSnapshot.ActivePlayers?.Count ?? 0;

            float baseWidth = Mathf.Max(BASE_MIN_WIDTH, showTable ? columnsWidth : 0f) + BASE_PADDING * 2;
            float baseHeight = BASE_PADDING * 2 + BASE_TITLE_HEIGHT;
            if (showTable)
            {
                baseHeight += BASE_SECTION_SPACING + BASE_COLUMN_HEADER_HEIGHT + BASE_HEADER_LINE_HEIGHT + Mathf.Max(playerCount, 1) * BASE_PLAYER_ROW_HEIGHT;
            }

            float scale = Hud.FitScale(UIManager.UIScaleFactor, baseWidth, baseHeight, BASE_MAX_WIDTH_PERCENT, BASE_MAX_HEIGHT_PERCENT);
            ApplyScale(scale);

            Rect panel = new Rect((Screen.width - baseWidth * scale) * 0.5f, (Screen.height - baseHeight * scale) * 0.5f, baseWidth * scale, baseHeight * scale);
            Hud.Fill(panel, Hud.Ink);
            Hud.Fill(new Rect(panel.x, panel.y, panel.width, 3f * scale), Hud.Pink);

            float padding = BASE_PADDING * scale;
            Rect content = new Rect(panel.x + padding, panel.y + padding, panel.width - padding * 2, panel.height - padding * 2);

            DrawTitleRow(statsSnapshot, new Rect(content.x, content.y, content.width, BASE_TITLE_HEIGHT * scale), showTiles, scale);

            if (showTable)
            {
                float tableY = content.y + (BASE_TITLE_HEIGHT + BASE_SECTION_SPACING) * scale;
                DrawPlayerTable(statsSnapshot, new Rect(content.x, tableY, content.width, content.yMax - tableY), scale);
            }
        }

        private void ApplyScale(float scale)
        {
            Hud.Resize(captionStyle, CAPTION_FONT_SIZE, scale);
            Hud.Resize(captionRightStyle, CAPTION_FONT_SIZE, scale);
            Hud.Resize(titleStyle, TITLE_FONT_SIZE, scale);
            Hud.Resize(tileValueStyle, TILE_VALUE_FONT_SIZE, scale);
            Hud.Resize(columnHeaderStyle, COLUMN_HEADER_FONT_SIZE, scale);
            Hud.Resize(playerHeaderStyle, COLUMN_HEADER_FONT_SIZE, scale);
            Hud.Resize(nameStyle, NAME_FONT_SIZE, scale);
            Hud.Resize(valueStyle, VALUE_FONT_SIZE, scale);
            Hud.Resize(emptyStyle, NAME_FONT_SIZE, scale);
        }

        private void DrawTitleRow(GameStatsSnapshot statsSnapshot, Rect row, bool showTiles, float scale)
        {
            bool live = statsSnapshot.IsSurvivalActive || statsSnapshot.IsVersusActive;
            GameMode mode = live ? statsSnapshot.GameMode : GameSessionManager.Instance.LastGameMode;
            string caption = live ? "LIVE STATS" : mode == GameMode.None ? "NO GAMES YET" : mode == GameMode.Versus ? "LAST MATCH" : "LAST RUN";
            string title = mode == GameMode.Versus ? "VERSUS" : mode == GameMode.Survival ? (IsSurgeRun() ? "SURGE" : "SURVIVAL") : "STATS";

            float captionHeight = CAPTION_FONT_SIZE * 1.6f * scale;
            Hud.Label(new Rect(row.x, row.y, row.width, captionHeight), caption, captionStyle, live ? Hud.Pink : Hud.Muted);
            Hud.Label(new Rect(row.x, row.y + captionHeight, row.width, row.height - captionHeight), title, titleStyle, Hud.Text);

            if (!showTiles) return;

            float tileWidth = BASE_TILE_WIDTH * scale;
            float x = row.xMax;
            if (ModConfig.ShowEnemyDeaths && mode != GameMode.Versus)
            {
                x -= tileWidth;
                int enemiesKilled = statsSnapshot.EnemiesKilled;
                DrawTile(new Rect(x, row.y, tileWidth, row.height), LABEL_ENEMIES_KILLED, Hud.Number(enemiesKilled), enemiesKilled > 0 ? Hud.Good : Hud.Muted, captionHeight);
            }
            if (ModConfig.ShowPlayTime)
            {
                x -= tileWidth;
                string value = live
                    ? TimeText(null, -1, statsSnapshot.CurrentSessionTime)
                    : statsSnapshot.LastGameDuration.TotalSeconds > 0 ? TimeText(null, -1, statsSnapshot.LastGameDuration) : LABEL_NO_GAMES;
                string timeCaption = !live ? LABEL_LAST_GAME : mode == GameMode.Versus ? LABEL_MATCH_TIME : LABEL_RUN_TIME;
                DrawTile(new Rect(x, row.y, tileWidth, row.height), timeCaption, value, live ? Hud.Text : Hud.Muted, captionHeight);
            }
        }

        // A time's text changes once a second, so it is formatted only then
        private string TimeText(Column column, int player, TimeSpan time)
        {
            long seconds = time.Ticks / TimeSpan.TicksPerSecond;
            var key = (column, player);
            if (!timeTexts.TryGetValue(key, out var cached) || cached.Seconds != seconds || cached.Text == null)
            {
                cached = (seconds, TimeFormatUtils.FormatTimeSpan(time));
                timeTexts[key] = cached;
            }
            return cached.Text;
        }

        private static bool IsSurgeRun()
        {
            return Parts.Surge.Running && (SpiderSurge.SurgeGameModeManager.IsSurgeRunActive || SpiderSurge.SurgeGameModeManager.WasSurgeRunUsed);
        }

        private void DrawTile(Rect tile, string caption, string value, Color valueColor, float captionHeight)
        {
            Hud.Label(new Rect(tile.x, tile.y, tile.width, captionHeight), caption, captionRightStyle, Hud.Muted);
            Hud.Label(new Rect(tile.x, tile.y + captionHeight, tile.width, tile.height - captionHeight), value, tileValueStyle, valueColor);
        }

        private void DrawPlayerTable(GameStatsSnapshot statsSnapshot, Rect table, float scale)
        {
            float headerHeight = BASE_COLUMN_HEADER_HEIGHT * scale;
            float rowHeight = BASE_PLAYER_ROW_HEIGHT * scale;
            float barWidth = 4f * scale;
            float nameInset = barWidth + 12f * scale;
            float nameWidth = COL_WIDTH_PLAYER_NAME * scale;
            float headerInset = 4f * scale;

            Hud.Label(new Rect(table.x + nameInset, table.y, nameWidth - nameInset, headerHeight - headerInset), LABEL_PLAYER, playerHeaderStyle, Hud.Muted);
            float x = table.x + nameWidth;
            foreach (Column column in visibleColumns)
            {
                float width = column.Width * scale;
                Hud.Label(new Rect(x + 4f * scale, table.y, width - 8f * scale, headerHeight - headerInset), column.Header ??= column.Label.ToUpperInvariant(), columnHeaderStyle, Hud.Muted);
                x += width;
            }

            float y = table.y + headerHeight;
            float lineHeight = BASE_HEADER_LINE_HEIGHT * scale;
            Hud.Fill(new Rect(table.x, y, table.width, lineHeight), Hud.Pink);
            y += lineHeight;

            if (statsSnapshot.ActivePlayers == null || statsSnapshot.ActivePlayers.Count == 0)
            {
                Hud.Label(new Rect(table.x, y, table.width, rowHeight), LABEL_NO_PLAYERS, emptyStyle, Hud.Muted);
                return;
            }

            int index = 0;
            foreach (var playerEntry in statsSnapshot.ActivePlayers)
            {
                var playerData = playerEntry.Value;
                Rect row = new Rect(table.x, y, table.width, rowHeight);
                if (index % 2 == 1)
                {
                    Hud.Fill(row, Hud.Stripe);
                }

                Color playerColor = Hud.Readable(playerData.PlayerColor);
                float barInset = rowHeight * 0.2f;
                Hud.Fill(new Rect(row.x, row.y + barInset, barWidth, rowHeight - barInset * 2), playerColor);
                Hud.Label(new Rect(row.x + nameInset, row.y, nameWidth - nameInset, rowHeight), playerData.PlayerName, nameStyle, playerColor);

                x = row.x + nameWidth;
                foreach (Column column in visibleColumns)
                {
                    float width = column.Width * scale;
                    Rect cell = new Rect(x, row.y, width, rowHeight);
                    if (column.Accuracy != null)
                    {
                        WeaponAccuracy accuracy = column.Accuracy(playerData);
                        Hud.Label(cell, accuracy?.Display ?? "--", valueStyle, accuracy?.ShotsFired > 0 ? Hud.Good : Hud.Dim);
                    }
                    else if (column.Time != null)
                    {
                        TimeSpan time = column.Time(playerData);
                        Hud.Label(cell, TimeText(column, playerData.PlayerId, time), valueStyle, time.TotalSeconds >= 1 ? Hud.Text : Hud.Dim);
                    }
                    else
                    {
                        int value = column.Count(playerData);
                        Hud.Label(cell, Hud.Number(value), valueStyle, value > 0 ? ToneColor(column.Tone) : Hud.Dim);
                    }
                    x += width;
                }

                y += rowHeight;
                index++;
            }
        }

        private static Color ToneColor(Tone tone)
        {
            switch (tone)
            {
                case Tone.Good: return Hud.Good;
                case Tone.Bad: return Hud.Pink;
                case Tone.Warm: return Hud.Warm;
                default: return Hud.Text;
            }
        }
        #endregion
    }
}
