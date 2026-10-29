using UnityEngine;
using SpiderSurge.Common;
using Logger = Silk.Logger;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StatsMod
{
    public partial class BigUI : MonoBehaviour
    {
        private enum Tone { Good, Bad, Warm, Plain }

        private sealed class Column
        {
            public string Label;
            public string Group;
            public string Header;
            public float Width;
            public float FitWidth;
            public int FitVersion = -1;
            public Func<bool> Shown;
            public Tone Tone;
            public Func<PlayerTracker.PlayerData, int> Count;
            public Func<PlayerTracker.PlayerData, TimeSpan> Time;
            public Func<PlayerTracker.PlayerData, WeaponAccuracy> Accuracy;
        }

        private static Column Count(string label, float width, string setting, Tone tone, Func<PlayerTracker.PlayerData, int> count) =>
            new Column { Label = label, Width = width, Shown = () => ModConfig.Column(setting), Tone = tone, Count = count };

        private static Column Duration(string label, float width, string setting, Func<PlayerTracker.PlayerData, TimeSpan> time) =>
            new Column { Label = label, Width = width, Shown = () => ModConfig.Column(setting), Tone = Tone.Plain, Time = time };

        private static Column Enemy(string label, string key, string setting) =>
            new Column { Label = label, Group = "enemies", Width = COL_WIDTH_ENEMY_KILLS, Shown = () => ModConfig.Column(setting), Tone = Tone.Good, Count = p => p.EnemyKills.TryGetValue(key, out int v) ? v : 0 };

        private static Column Weapon(string label, string key, string setting) =>
            new Column { Label = label, Group = "weapons", Width = COL_WIDTH_WEAPON_KILLS, Shown = () => ModConfig.Column(setting), Tone = Tone.Good, Count = p => p.WeaponHits.TryGetValue(key, out int v) ? v : 0 };

        private const float COL_WIDTH_ENEMY_KILLS = 100f;
        private const float COL_WIDTH_WEAPON_KILLS = 100f;

        private static readonly Column[] Columns = new Column[]
        {
            Count("Total Offence", 100f, "totalOffence", Tone.Good, p => p.Kills + p.EnemyShieldsTakenDown),
            Count("Friendly Hits", 100f, "totalFriendlyHits", Tone.Warm, p => p.FriendlyKills + p.FriendlyShieldsHit),
            Count("Hits Taken", 90f, "totalHitsTaken", Tone.Bad, p => p.Deaths + p.ShieldsLost),
            Count("Kills", 80f, "kills", Tone.Good, p => p.Kills),
            Count("Deaths", 90f, "deaths", Tone.Bad, p => p.Deaths),
            Count("Max Kill Streak", 100f, "maxKillStreak", Tone.Good, p => p.MaxKillStreak),
            Count("Kill Streak", 100f, "currentKillStreak", Tone.Good, p => p.KillStreak),
            Count("Max Solo Streak", 110f, "maxSoloKillStreak", Tone.Good, p => p.MaxKillStreakWhileSolo),
            Count("Solo Streak", 110f, "currentSoloKillStreak", Tone.Good, p => p.KillStreakWhileSolo),
            Count("Friendly Kills", 110f, "friendlyKills", Tone.Warm, p => p.FriendlyKills),
            Count("Enemy Shields", 110f, "enemyShields", Tone.Good, p => p.EnemyShieldsTakenDown),
            Count("Shields Lost", 100f, "shieldsLost", Tone.Bad, p => p.ShieldsLost),
            Count("Friendly Shields", 110f, "friendlyShields", Tone.Warm, p => p.FriendlyShieldsHit),
            Count("Wave Clutches", 110f, "waveClutches", Tone.Good, p => p.WaveClutches),
            Duration("Alive Time", 120f, "aliveTime", p => p.GetCurrentAliveTime()),
            new Column { Label = "Airborne Kills", Width = 100f, Shown = () => false, Tone = Tone.Good, Count = p => p.KillsWhileAirborne },
            Count("Web Swings", 100f, "webSwings", Tone.Good, p => p.WebSwings),
            Duration("Web Swing Time", 120f, "webSwingTime", p => p.GetCurrentWebSwingTime()),
            Duration("Airborne Time", 120f, "airborneTime", p => p.GetCurrentAirborneTime()),
            Count("Lava Deaths", 100f, "lavaDeaths", Tone.Bad, p => p.LavaDeaths),
            Count("Astral Returns", 110f, "astralReturns", Tone.Good, p => p.AstralReturns),
            new Column { Label = "Accuracy", Width = 110f, Shown = () => ModConfig.ShowAccuracy("overall"), Accuracy = p => p.ShootingAccuracy },

            Enemy("Wasp", "Wasp", "enemyKills.wasp"),
            Enemy("Wasp Cocoon", "Wasp Cocoon", "enemyKills.waspCocoon"),
            Enemy("Power Wasp", "Power Wasp", "enemyKills.powerWasp"),
            Enemy("Roller", "Roller", "enemyKills.roller"),
            Enemy("Whisp", "Whisp", "enemyKills.whisp"),
            Enemy("Power Whisp", "Power Whisp", "enemyKills.powerWhisp"),
            Enemy("Melee Whisp", "Melee Whisp", "enemyKills.meleeWhisp"),
            Enemy("Power Melee Whisp", "Power Melee Whisp", "enemyKills.powerMeleeWhisp"),
            Enemy("Khepri", "Khepri", "enemyKills.khepri"),
            Enemy("Power Khepri", "Power Khepri", "enemyKills.powerKhepri"),
            Enemy("Hornet Shaman", "Hornet Shaman", "enemyKills.hornetShaman"),
            Enemy("Hornet", "Hornet", "enemyKills.hornet"),

            Weapon("Shotgun", "Shotgun", "weaponKills.shotgun"),
            Weapon("RailShot", "RailShot", "weaponKills.railShot"),
            Weapon("Death Cube", "DeathCube", "weaponKills.deathCube"),
            Weapon("Death Ray", "DeathRay", "weaponKills.deathRay"),
            Weapon("Energy Ball", "EnergyBall", "weaponKills.energyBall"),
            Weapon("Particle Blade", "Particle Blade", "weaponKills.particleBlade"),
            Weapon("Khepri Staff", "KhepriStaff", "weaponKills.khepriStaff"),
            Weapon("Laser Cannon", "Laser Cannon", "weaponKills.laserCannon"),
            Weapon("Laser Cube", "Laser Cube", "weaponKills.laserCube"),
            Weapon("Saw Disc", "SawDisc", "weaponKills.sawDisc"),
            Weapon("Explosions", "Explosions", "weaponKills.explosions"),
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
        public float COL_WIDTH_PLAYER_NAME = 230f;
        public float COL_WIDTH_PLAYER_NAME_MAX = 380f;
        public float NAME_INSET = 56f;
        public float HEADER_SIDE_INSET = 4f;
        // Font sizes are rounded when the window scales, so text measured at the base size gets this much room to spare
        public float TEXT_FIT_SLACK = 1.08f;
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
        private readonly Dictionary<string, (string Text, float Width)> nameFits = new Dictionary<string, (string Text, float Width)>();
        private int nameFitVersion = -1;
        private float nameFitMax;
        private float nameColumnWidth;
        #endregion

        #region GUI Styles
        private GUIStyle captionStyle;
        private GUIStyle captionRightStyle;
        private GUIStyle titleStyle;
        private GUIStyle tileValueStyle;
        private GUIStyle columnHeaderStyle;
        private GUIStyle headerMeasureStyle;
        private GUIStyle playerHeaderStyle;
        private GUIStyle nameStyle;
        private GUIStyle nameMeasureStyle;
        private GUIStyle valueStyle;
        private GUIStyle emptyStyle;
        private int fontVersion = -1;
        #endregion

        #region Display Control
        public void ShowDisplay()
        {
            if (!isDisplayVisible) OpenPreferredPreset();
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
            headerMeasureStyle = Hud.Style(COLUMN_HEADER_FONT_SIZE, bold: true);
            playerHeaderStyle = Hud.Style(COLUMN_HEADER_FONT_SIZE, bold: true, anchor: TextAnchor.LowerLeft);
            nameStyle = Hud.Style(NAME_FONT_SIZE, bold: true);
            nameMeasureStyle = Hud.Style(NAME_FONT_SIZE, bold: true);
            valueStyle = Hud.Style(VALUE_FONT_SIZE, bold: true, anchor: TextAnchor.MiddleCenter);
            emptyStyle = Hud.Style(NAME_FONT_SIZE, anchor: TextAnchor.MiddleCenter);

            fontVersion = Hud.FontVersion;
        }

        #region GUI Drawing
        private void OnGUI()
        {
            if (!isDisplayVisibleAtAll || !isDisplayVisible || GameMenus.Open) return;
            HandlePresetMouse();
            if (Event.current.type != EventType.Repaint) return;

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
            bool showTiles = ModConfig.ShowPlayTime || ModConfig.ShowEnemyDeaths;
            bool showTable = ModConfig.ShowPlayers;
            int playerCount = statsSnapshot.ActivePlayers?.Count ?? 0;

            float baseHeight = BASE_PADDING * 2 + BASE_TITLE_HEIGHT + 36f;
            if (showTable)
            {
                baseHeight += BASE_SECTION_SPACING + BASE_COLUMN_HEADER_HEIGHT + BASE_HEADER_LINE_HEIGHT + Mathf.Max(playerCount, 1) * BASE_PLAYER_ROW_HEIGHT;
            }

            nameColumnWidth = NameColumnWidth(statsSnapshot);
            float targetScale = Mathf.Min(UIManager.UIScaleFactor, Screen.height * BASE_MAX_HEIGHT_PERCENT / baseHeight);
            pageColumnWidth = Mathf.Max(1f, Screen.width * BASE_MAX_WIDTH_PERCENT / targetScale - nameColumnWidth - BASE_PADDING * 2);
            SelectColumns();
            visibleColumns.Clear();
            float columnsWidth = nameColumnWidth;
            int pageEnd = presetPage + 1 < pageStarts.Count ? pageStarts[presetPage + 1] : presetColumns.Count;
            for (int i = pageStarts[presetPage]; i < pageEnd; i++)
            {
                Column column = presetColumns[i];
                visibleColumns.Add(column);
                columnsWidth += WidthOf(column);
            }

            float baseWidth = Mathf.Max(BASE_MIN_WIDTH, showTable ? columnsWidth : 0f) + BASE_PADDING * 2;
            float scale = Hud.FitScale(UIManager.UIScaleFactor, baseWidth, baseHeight, BASE_MAX_WIDTH_PERCENT, BASE_MAX_HEIGHT_PERCENT);
            ApplyScale(scale);

            Rect panel = new Rect((Screen.width - baseWidth * scale) * 0.5f, (Screen.height - baseHeight * scale) * 0.5f, baseWidth * scale, baseHeight * scale);
            panelRect = panel;
            Hud.Fill(panel, Hud.Ink);
            Hud.Fill(new Rect(panel.x, panel.y, panel.width, 3f * scale), Hud.Pink);

            float padding = BASE_PADDING * scale;
            Rect content = new Rect(panel.x + padding, panel.y + padding, panel.width - padding * 2, panel.height - padding * 2);

            DrawTitleRow(statsSnapshot, new Rect(content.x, content.y, content.width, BASE_TITLE_HEIGHT * scale), showTiles, scale);

            DrawPresetNavigation(content, scale);
            if (showTable)
            {
                float tableY = content.y + (BASE_TITLE_HEIGHT + BASE_SECTION_SPACING) * scale;
                DrawPlayerTable(statsSnapshot, new Rect(content.x, tableY, content.width, content.yMax - tableY), scale);
            }
        }

        // A header wraps onto two lines at most and never breaks a word, so its column grows to the narrowest width that allows that
        private float WidthOf(Column column)
        {
            if (column.FitVersion == fontVersion) return column.FitWidth;
            column.Header ??= column.Label.ToUpperInvariant();
            string[] words = column.Header.Split(' ');
            float needed = float.MaxValue;
            for (int split = 1; split <= words.Length; split++)
            {
                float first = HeaderLineWidth(string.Join(" ", words, 0, split));
                float second = split < words.Length ? HeaderLineWidth(string.Join(" ", words, split, words.Length - split)) : 0f;
                needed = Mathf.Min(needed, Mathf.Max(first, second));
            }
            column.FitWidth = Mathf.Max(column.Width, Mathf.Ceil(needed * TEXT_FIT_SLACK) + 2f * HEADER_SIDE_INSET);
            column.FitVersion = fontVersion;
            return column.FitWidth;
        }

        private float HeaderLineWidth(string line) => headerMeasureStyle.CalcSize(new GUIContent(line)).x;

        private float NameColumnWidth(GameStatsSnapshot statsSnapshot)
        {
            float width = COL_WIDTH_PLAYER_NAME;
            if (statsSnapshot.ActivePlayers == null) return width;
            foreach (var playerEntry in statsSnapshot.ActivePlayers)
            {
                width = Mathf.Max(width, FitName(playerEntry.Value.PlayerName).Width);
            }
            return width;
        }

        // The name column grows to fit a name up to its widest, and a longer name is cut short with an ellipsis
        private (string Text, float Width) FitName(string name)
        {
            name ??= "";
            if (nameFitVersion != fontVersion || nameFitMax != COL_WIDTH_PLAYER_NAME_MAX)
            {
                nameFits.Clear();
                nameFitVersion = fontVersion;
                nameFitMax = COL_WIDTH_PLAYER_NAME_MAX;
            }
            if (nameFits.TryGetValue(name, out var fit)) return fit;

            float room = COL_WIDTH_PLAYER_NAME_MAX - NAME_INSET - HEADER_SIDE_INSET;
            string text = name;
            if (NameTextWidth(text) > room)
            {
                int length = text.Length;
                while (length > 1 && NameTextWidth(text.Substring(0, length).TrimEnd() + "…") > room) length--;
                text = text.Substring(0, length).TrimEnd() + "…";
            }
            fit = (text, Mathf.Clamp(NAME_INSET + NameTextWidth(text) + HEADER_SIDE_INSET, COL_WIDTH_PLAYER_NAME, COL_WIDTH_PLAYER_NAME_MAX));
            nameFits[name] = fit;
            return fit;
        }

        private float NameTextWidth(string text) => Mathf.Ceil(nameMeasureStyle.CalcSize(new GUIContent(text)).x * TEXT_FIT_SLACK);

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
            float nameInset = NAME_INSET * scale;
            float nameWidth = nameColumnWidth * scale;
            float headerInset = 4f * scale;

            Hud.Label(new Rect(table.x + nameInset, table.y, nameWidth - nameInset, headerHeight - headerInset), LABEL_PLAYER, playerHeaderStyle, Hud.Muted);
            float x = table.x + nameWidth;
            foreach (Column column in visibleColumns)
            {
                float width = WidthOf(column) * scale;
                Hud.Label(new Rect(x + HEADER_SIDE_INSET * scale, table.y, width - 2f * HEADER_SIDE_INSET * scale, headerHeight - headerInset), column.Header, columnHeaderStyle, Hud.Muted);
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
                Rect portraitRect = new Rect(row.x + barWidth + 6f * scale, row.y + 3f * scale, 36f * scale, 36f * scale);
                Texture portrait = SpiderPortraits.ViewOf(playerEntry.Key);
                if (portrait != null) GUI.DrawTexture(portraitRect, portrait, ScaleMode.ScaleToFit);
                else Hud.Fill(portraitRect, playerColor);
                Hud.Label(new Rect(row.x + nameInset, row.y, nameWidth - nameInset, rowHeight), FitName(playerData.PlayerName).Text, nameStyle, playerColor);

                x = row.x + nameWidth;
                foreach (Column column in visibleColumns)
                {
                    float width = WidthOf(column) * scale;
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
