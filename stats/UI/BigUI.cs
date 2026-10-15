using UnityEngine;
using Silk;
using Logger = Silk.Logger;
using System;

namespace StatsMod
{
    public class BigUI : MonoBehaviour
    {
        #region Base Dimensions
        public float BASE_SECTION_SPACING = 25f;
        public float BASE_CARD_PADDING = 20f;
        public float BASE_PLAYER_ROW_HEIGHT = 40f;
        public float BASE_SURVIVAL_SECTION_HEIGHT = 100f;
        public float BASE_CONTENT_PADDING = 40f;
        public float BASE_MARGIN_PERCENT = 0.15f;
        public float BASE_MAX_HEIGHT_PERCENT = 0.9f;
        public float BASE_BACKGROUND_PADDING = 80f;
        #endregion

        #region Column Widths
        public float COL_WIDTH_PLAYER_NAME = 120f;
        public float COL_WIDTH_DEATHS = 90f;
        public float COL_WIDTH_KILLS = 70f;
        public float COL_WIDTH_PVP = 110f;
        public float COL_WIDTH_ENEMY_SHIELDS = 120f;
        public float COL_WIDTH_PLAYER_SHIELDS = 110f;
        public float COL_WIDTH_SHIELDS_LOST = 100f;
        public float COL_WIDTH_ALIVE_TIME = 120f;
        public float COL_WIDTH_KILL_STREAK = 100f;
        public float COL_WIDTH_WAVE_CLUTCHES = 120f;
        public float COL_WIDTH_WEB_SWINGS = 100f;
        public float COL_WIDTH_WEB_SWING_TIME = 120f;
        public float COL_WIDTH_AIRBORNE_TIME = 120f;
        public float COL_WIDTH_CURRENT_STREAK = 100f;
        public float COL_WIDTH_SOLO_STREAK = 120f;
        public float COL_WIDTH_ENEMY_KILLS = 100f;
        public float COL_WIDTH_WEAPON_KILLS = 100f;
        public float COL_WIDTH_TOTAL_OFFENCE = 100f;
        public float COL_WIDTH_TOTAL_FRIENDLY = 110f;
        public float COL_WIDTH_TOTAL_HITS = 90f;
        public float COL_WIDTH_LAVA_DEATHS = 100f;
        public float COL_WIDTH_ASTRAL_RETURNS = 120f;
        public float COL_WIDTH_INDENT = 30f;
        #endregion

        #region Text Labels
        public string LABEL_PLAYER = "Player";
        public string LABEL_DEATHS = "Deaths";
        public string LABEL_KILLS = "Kills";
        public string LABEL_PVP = "Friendly Kills";
        public string LABEL_ENEMY_SHIELDS = "Enemy Shields";
        public string LABEL_PLAYER_SHIELDS = "Friendly Shields";
        public string LABEL_SHIELDS_LOST = "Shields Lost";
        public string LABEL_ALIVE_TIME = "Alive Time";
        public string LABEL_KILL_STREAK = "Max Kill Streak";
        public string LABEL_CURRENT_KILL_STREAK = "Kill Streak";
        public string LABEL_MAX_SOLO_KILL_STREAK = "Max Solo Streak";
        public string LABEL_CURRENT_SOLO_KILL_STREAK = "Solo Streak";
        public string LABEL_WAVE_CLUTCHES = "Wave Clutches";
        public string LABEL_WEB_SWINGS = "Web Swings";
        public string LABEL_WEB_SWING_TIME = "Web Swing Time";
        public string LABEL_AIRBORNE_TIME = "Airborne Time";
        public string LABEL_LAVA_DEATHS = "Lava Deaths";
        public string LABEL_ASTRAL_RETURNS = "Astral Returns";

        // Computed stat labels
        public string LABEL_TOTAL_OFFENCE = "Total Offence";
        public string LABEL_TOTAL_FRIENDLY_HITS = "Friendly Hits";
        public string LABEL_TOTAL_HITS_TAKEN = "Hits Taken";

        // Enemy kill labels
        public string LABEL_WASP_KILLS = "Wasp";
        public string LABEL_POWER_WASP_KILLS = "Power Wasp";
        public string LABEL_ROLLER_KILLS = "Roller";
        public string LABEL_WHISP_KILLS = "Whisp";
        public string LABEL_POWER_WHISP_KILLS = "Power Whisp";
        public string LABEL_MELEE_WHISP_KILLS = "Melee Whisp";
        public string LABEL_POWER_MELEE_WHISP_KILLS = "Pwr Melee Whisp";
        public string LABEL_KHEPRI_KILLS = "Khepri";
        public string LABEL_POWER_KHEPRI_KILLS = "Power Khepri";
        public string LABEL_HORNET_SHAMAN_KILLS = "Hornet Shaman";
        public string LABEL_HORNET_KILLS = "Hornet";
        public string LABEL_PLAYER_KILLS = "Player Kills";

        // Weapon kill labels
        public string LABEL_SHOTGUN_KILLS = "Shotgun";
        public string LABEL_RAILSHOT_KILLS = "RailShot";
        public string LABEL_DEATHCUBE_KILLS = "Death Cube";
        public string LABEL_DEATHRAY_KILLS = "Death Ray";
        public string LABEL_ENERGYBALL_KILLS = "Energy Ball";
        public string LABEL_PARTICLEBLADE_KILLS = "Particle Blade";
        public string LABEL_KHEPRISTAFF_KILLS = "Khepri Staff";
        public string LABEL_LASERCANNON_KILLS = "Laser Cannon";
        public string LABEL_LASERCUBE_KILLS = "Laser Cube";
        public string LABEL_SAWDISC_KILLS = "Saw Disc";
        public string LABEL_EXPLOSION_KILLS = "Explosions";

        public string LABEL_SURVIVAL_MODE = "Survival Mode";
        public string LABEL_ENEMY_STATISTICS = "Enemy Statistics";
        public string LABEL_TIME = "Time:";
        public string LABEL_LAST_GAME = "Last Game:";
        public string LABEL_ENEMIES_KILLED = "Enemies Killed:";
        public string LABEL_NO_PLAYERS = "No players connected";
        public string LABEL_NO_GAMES = "No games yet";
        #endregion

        #region Spacing Values
        public float SPACING_BETWEEN_HEADERS = 10f;
        public float SPACING_BETWEEN_PLAYERS = 15f;
        public float SPACING_CARD_PERCENT = 0.04f;
        public float SPACING_ENEMIES_VALUE = 25f;
        #endregion

        #region Other Widths
        public float WIDTH_TIME_LABEL = 150f;
        public float WIDTH_LAST_GAME_LABEL = 200f;

        public float WIDTH_CARD_HALF = 0.48f;
        #endregion

        // Scaled properties
        private float SectionSpacing => UIManager.ScaleValue(BASE_SECTION_SPACING);
        private float CardPadding => UIManager.ScaleValue(BASE_CARD_PADDING);
        private float PlayerRowHeight => UIManager.ScaleValue(BASE_PLAYER_ROW_HEIGHT);
        private float SurvivalSectionHeight => UIManager.ScaleValue(BASE_SURVIVAL_SECTION_HEIGHT);

        private float Total_Height = 0f;
        private float Total_Width = 0f;

        // Minimum width for top sections
        public float BASE_MIN_WIDTH_ONE_SECTION = 400f;
        public float BASE_MAX_WIDTH_PERCENT = 0.9f;

        #region UI State
        private bool isDisplayVisible = false;
        private bool isDisplayVisibleAtAll = true;
        #endregion

        #region GUI Styles
        private GUIStyle backgroundStyle;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle cardStyle;
        private GUIStyle errorStyle;
        private GUIStyle timerStyle;
        private GUIStyle statusStyle;
        private GUIStyle killsGreenStyle;
        private GUIStyle killsWhiteStyle;
        private GUIStyle deathsRedStyle;
        private GUIStyle deathsWhiteStyle;
        private GUIStyle friendlyKillsOrangeStyle;
        private GUIStyle friendlyKillsWhiteStyle;
        private GUIStyle centeredWhiteStyle;
        private GUIStyle dynamicPlayerNameStyle;

        private bool stylesInitialized = false;
        #endregion

        #region Initialization
        public void Initialize()
        {
            isDisplayVisibleAtAll = ModConfig.ShowStatsWindow;
            Logger.LogInfo("BigUI initialized");
        }
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
            if (stylesInitialized) return;

            backgroundStyle = new GUIStyle()
            {
                normal = { background = UIManager.Instance.GetDarkTexture() }
            };

            headerStyle = UIManager.Instance.CreateHeaderStyle(UIManager.BIG_HEADER_SIZE);
            headerStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle = UIManager.Instance.CreateLabelStyle(UIManager.BIG_LABEL_SIZE);
            valueStyle = UIManager.Instance.CreateValueStyle(UIManager.BIG_LABEL_SIZE);
            cardStyle = UIManager.Instance.CreateCardStyle(UIManager.Instance.GetMediumTexture());
            errorStyle = UIManager.Instance.CreateErrorStyle(UIManager.BIG_LABEL_SIZE);

            cardStyle.padding = new RectOffset((int)CardPadding, (int)CardPadding, (int)CardPadding, (int)CardPadding);

            timerStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.Green } };
            statusStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.Gray } };
            killsGreenStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.Green }, alignment = TextAnchor.MiddleCenter };
            killsWhiteStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.White }, alignment = TextAnchor.MiddleCenter };
            deathsRedStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.Red }, alignment = TextAnchor.MiddleCenter };
            deathsWhiteStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.White }, alignment = TextAnchor.MiddleCenter };
            friendlyKillsOrangeStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.Orange }, alignment = TextAnchor.MiddleCenter };
            friendlyKillsWhiteStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.White }, alignment = TextAnchor.MiddleCenter };
            centeredWhiteStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.White }, alignment = TextAnchor.MiddleCenter };
            dynamicPlayerNameStyle = new GUIStyle(valueStyle) { alignment = TextAnchor.MiddleCenter };


            stylesInitialized = true;
        }


        #region GUI Drawing
        private void OnGUI()
        {
            if (!isDisplayVisibleAtAll || !isDisplayVisible) return;

            InitializeStyles();

            Color originalColor = GUI.color;
            float opacity = ModConfig.BigUIOpacity / 100f;
            GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, opacity);

            // Calculate width based on content
            CalculateContentWidth();

            float scaledContentPadding = UIManager.ScaleValue(BASE_CONTENT_PADDING);
            float scaledBackgroundPadding = UIManager.ScaleValue(BASE_BACKGROUND_PADDING);

            float backgroundWidth = Total_Width + (scaledContentPadding * 2) + scaledBackgroundPadding;
            backgroundWidth = Mathf.Min(backgroundWidth, Screen.width * BASE_MAX_WIDTH_PERCENT);
            float marginX = (Screen.width - backgroundWidth) * 0.5f;

            CalculateContentHeight();
            float backgroundHeight = Mathf.Min(Total_Height + scaledBackgroundPadding, Screen.height * BASE_MAX_HEIGHT_PERCENT);
            float backgroundY = (Screen.height - backgroundHeight) * 0.5f;

            Rect backgroundRect = new Rect(marginX, backgroundY, backgroundWidth, backgroundHeight);

            GUI.Box(backgroundRect, "", backgroundStyle);

            // Calculate content area within the background
            float contentPadding = scaledContentPadding;
            float contentWidth = backgroundRect.width - (contentPadding * 2);
            float availableContentHeight = backgroundRect.height - (contentPadding * 2);

            GUILayout.BeginArea(new Rect(backgroundRect.x + contentPadding, backgroundRect.y + contentPadding, contentWidth, availableContentHeight));

            // Begin a horizontal group to capture actual content width
            GUILayout.BeginVertical();

            var statsSnapshot = GameSessionManager.Instance.GetStatsSnapshot();

            // Draw survival and enemy stats horizontally if both are enabled
            if (ModConfig.ShowPlayTime && ModConfig.ShowEnemyDeaths)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(contentWidth * WIDTH_CARD_HALF));
                DrawSurvivalModeStats(statsSnapshot);
                GUILayout.EndVertical();

                GUILayout.Space(contentWidth * SPACING_CARD_PERCENT);

                GUILayout.BeginVertical(GUILayout.Width(contentWidth * WIDTH_CARD_HALF));
                DrawEnemyStats(statsSnapshot);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                GUILayout.Space(SectionSpacing);
            }
            else if (ModConfig.ShowPlayTime)
            {
                DrawSurvivalModeStats(statsSnapshot);
                GUILayout.Space(SectionSpacing);
            }
            else if (ModConfig.ShowEnemyDeaths)
            {
                DrawEnemyStats(statsSnapshot);
                GUILayout.Space(SectionSpacing);
            }

            if (ModConfig.ShowPlayers)
            {
                DrawPlayerStats(statsSnapshot);
            }

            GUILayout.EndVertical();

            GUILayout.EndArea();

            GUI.color = originalColor;
        }

        private void CalculateContentHeight()
        {
            float totalHeight = 0f;

            if (ModConfig.ShowPlayTime || ModConfig.ShowEnemyDeaths)
            {
                totalHeight += SurvivalSectionHeight + SectionSpacing;
            }

            if (ModConfig.ShowPlayers)
            {
                var statsSnapshot = GameSessionManager.Instance?.GetStatsSnapshot();
                float playerSectionHeight = UIManager.ScaleValue(120f); // Base height for player section
                if (statsSnapshot?.ActivePlayers != null)
                {
                    playerSectionHeight += statsSnapshot.ActivePlayers.Count * PlayerRowHeight;
                }
                totalHeight += playerSectionHeight;
            }

            Total_Height = totalHeight;
        }

        private void CalculateContentWidth()
        {
            float columnWidth = 0f;

            // Player name column is always shown
            columnWidth += COL_WIDTH_PLAYER_NAME;

            // Computed stat columns
            if (ModConfig.BigUIShowTotalOffence) columnWidth += COL_WIDTH_TOTAL_OFFENCE;
            if (ModConfig.BigUIShowTotalFriendlyHits) columnWidth += COL_WIDTH_TOTAL_FRIENDLY;
            if (ModConfig.BigUIShowTotalHitsTaken) columnWidth += COL_WIDTH_TOTAL_HITS;

            // Basic stats columns
            if (ModConfig.BigUIShowKills) columnWidth += COL_WIDTH_KILLS;
            if (ModConfig.BigUIShowDeaths) columnWidth += COL_WIDTH_DEATHS;
            if (ModConfig.BigUIShowMaxKillStreak) columnWidth += COL_WIDTH_KILL_STREAK;
            if (ModConfig.BigUIShowCurrentKillStreak) columnWidth += COL_WIDTH_CURRENT_STREAK;
            if (ModConfig.BigUIShowMaxSoloKillStreak) columnWidth += COL_WIDTH_SOLO_STREAK;
            if (ModConfig.BigUIShowCurrentSoloKillStreak) columnWidth += COL_WIDTH_SOLO_STREAK;
            if (ModConfig.BigUIShowFriendlyKills) columnWidth += COL_WIDTH_PVP;
            if (ModConfig.BigUIShowEnemyShields) columnWidth += COL_WIDTH_ENEMY_SHIELDS;
            if (ModConfig.BigUIShowShieldsLost) columnWidth += COL_WIDTH_SHIELDS_LOST;
            if (ModConfig.BigUIShowFriendlyShields) columnWidth += COL_WIDTH_PLAYER_SHIELDS;
            if (ModConfig.BigUIShowWaveClutches) columnWidth += COL_WIDTH_WAVE_CLUTCHES;
            if (ModConfig.BigUIShowAliveTime) columnWidth += COL_WIDTH_ALIVE_TIME;
            if (ModConfig.BigUIShowWebSwings) columnWidth += COL_WIDTH_WEB_SWINGS;
            if (ModConfig.BigUIShowWebSwingTime) columnWidth += COL_WIDTH_WEB_SWING_TIME;
            if (ModConfig.BigUIShowAirborneTime) columnWidth += COL_WIDTH_AIRBORNE_TIME;
            if (ModConfig.BigUIShowLavaDeaths) columnWidth += COL_WIDTH_LAVA_DEATHS;
            if (ModConfig.BigUIShowAstralReturns) columnWidth += COL_WIDTH_ASTRAL_RETURNS;

            // Enemy kill columns
            if (ModConfig.BigUIShowWaspKills) columnWidth += COL_WIDTH_ENEMY_KILLS;
            if (ModConfig.BigUIShowPowerWaspKills) columnWidth += COL_WIDTH_ENEMY_KILLS;
            if (ModConfig.BigUIShowRollerKills) columnWidth += COL_WIDTH_ENEMY_KILLS;
            if (ModConfig.BigUIShowWhispKills) columnWidth += COL_WIDTH_ENEMY_KILLS;
            if (ModConfig.BigUIShowPowerWhispKills) columnWidth += COL_WIDTH_ENEMY_KILLS;
            if (ModConfig.BigUIShowMeleeWhispKills) columnWidth += COL_WIDTH_ENEMY_KILLS;
            if (ModConfig.BigUIShowPowerMeleeWhispKills) columnWidth += COL_WIDTH_ENEMY_KILLS;
            if (ModConfig.BigUIShowKhepriKills) columnWidth += COL_WIDTH_ENEMY_KILLS;
            if (ModConfig.BigUIShowPowerKhepriKills) columnWidth += COL_WIDTH_ENEMY_KILLS;
            if (ModConfig.BigUIShowHornetShamanKills) columnWidth += COL_WIDTH_ENEMY_KILLS;
            if (ModConfig.BigUIShowHornetKills) columnWidth += COL_WIDTH_ENEMY_KILLS;

            // Weapon kill columns
            if (ModConfig.BigUIShowShotgunKills) columnWidth += COL_WIDTH_WEAPON_KILLS;
            if (ModConfig.BigUIShowRailShotKills) columnWidth += COL_WIDTH_WEAPON_KILLS;
            if (ModConfig.BigUIShowDeathCubeKills) columnWidth += COL_WIDTH_WEAPON_KILLS;
            if (ModConfig.BigUIShowDeathRayKills) columnWidth += COL_WIDTH_WEAPON_KILLS;
            if (ModConfig.BigUIShowEnergyBallKills) columnWidth += COL_WIDTH_WEAPON_KILLS;
            if (ModConfig.BigUIShowParticleBladeKills) columnWidth += COL_WIDTH_WEAPON_KILLS;
            if (ModConfig.BigUIShowKhepriStaffKills) columnWidth += COL_WIDTH_WEAPON_KILLS;
            if (ModConfig.BigUIShowLaserCannonKills) columnWidth += COL_WIDTH_WEAPON_KILLS;
            if (ModConfig.BigUIShowLaserCubeKills) columnWidth += COL_WIDTH_WEAPON_KILLS;
            if (ModConfig.BigUIShowSawDiscKills) columnWidth += COL_WIDTH_WEAPON_KILLS;
            if (ModConfig.BigUIShowExplosionKills) columnWidth += COL_WIDTH_WEAPON_KILLS;

            columnWidth = UIManager.ScaleValue(columnWidth);

            float minWidth = 0f;
            bool showSurvival = ModConfig.ShowPlayTime;
            bool showEnemy = ModConfig.ShowEnemyDeaths;

            if (showSurvival && showEnemy)
            {
                minWidth = BASE_MIN_WIDTH_ONE_SECTION * 2;
            }
            else if (showSurvival || showEnemy)
            {
                minWidth = BASE_MIN_WIDTH_ONE_SECTION;
            }

            Total_Width = Mathf.Max(columnWidth, minWidth);
        }

        private void DrawSurvivalModeStats(GameStatsSnapshot statsSnapshot)
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label(LABEL_SURVIVAL_MODE, headerStyle);

            GUILayout.BeginHorizontal();
            if (statsSnapshot.IsSurvivalActive)
            {
                GUILayout.Label(LABEL_TIME, labelStyle, GUILayout.Width(WIDTH_TIME_LABEL));
                GUILayout.Label(TimeFormatUtils.FormatTimeSpan(statsSnapshot.CurrentSessionTime), timerStyle);
            }
            else
            {
                GUILayout.Label(LABEL_LAST_GAME, labelStyle, GUILayout.Width(WIDTH_LAST_GAME_LABEL));
                GUILayout.Label(statsSnapshot.LastGameDuration.TotalSeconds > 0 ? TimeFormatUtils.FormatTimeSpan(statsSnapshot.LastGameDuration) : LABEL_NO_GAMES, statusStyle);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawEnemyStats(GameStatsSnapshot statsSnapshot)
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label(LABEL_ENEMY_STATISTICS, headerStyle);

            try
            {
                int enemiesKilled = statsSnapshot.EnemiesKilled;
                GUILayout.BeginHorizontal();
                GUILayout.Label(LABEL_ENEMIES_KILLED, labelStyle, GUILayout.ExpandWidth(false));
                GUILayout.Space(SPACING_ENEMIES_VALUE);
                GUILayout.Label(enemiesKilled.ToString(), enemiesKilled > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
            }
            catch (System.Exception ex)
            {
                GUILayout.Label($"Error: {ex.Message}", errorStyle);
                Logger.LogError($"Error displaying enemy stats: {ex.Message}");
            }
            GUILayout.EndVertical();
        }

        private void DrawPlayerStats(GameStatsSnapshot statsSnapshot)
        {
            GUILayout.BeginVertical(cardStyle);
            try
            {
                if (statsSnapshot.ActivePlayers != null && statsSnapshot.ActivePlayers.Count > 0)
                {
                    // Draw column headers (Player is always shown)
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(LABEL_PLAYER, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_PLAYER_NAME)));

                    // Computed stat columns
                    if (ModConfig.BigUIShowTotalOffence)
                        GUILayout.Label(LABEL_TOTAL_OFFENCE, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_TOTAL_OFFENCE)));
                    if (ModConfig.BigUIShowTotalFriendlyHits)
                        GUILayout.Label(LABEL_TOTAL_FRIENDLY_HITS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_TOTAL_FRIENDLY)));
                    if (ModConfig.BigUIShowTotalHitsTaken)
                        GUILayout.Label(LABEL_TOTAL_HITS_TAKEN, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_TOTAL_HITS)));

                    // Basic stats columns
                    if (ModConfig.BigUIShowKills)
                        GUILayout.Label(LABEL_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_KILLS)));
                    if (ModConfig.BigUIShowDeaths)
                        GUILayout.Label(LABEL_DEATHS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_DEATHS)));
                    if (ModConfig.BigUIShowMaxKillStreak)
                        GUILayout.Label(LABEL_KILL_STREAK, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_KILL_STREAK)));
                    if (ModConfig.BigUIShowCurrentKillStreak)
                        GUILayout.Label(LABEL_CURRENT_KILL_STREAK, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_CURRENT_STREAK)));
                    if (ModConfig.BigUIShowMaxSoloKillStreak)
                        GUILayout.Label(LABEL_MAX_SOLO_KILL_STREAK, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_SOLO_STREAK)));
                    if (ModConfig.BigUIShowCurrentSoloKillStreak)
                        GUILayout.Label(LABEL_CURRENT_SOLO_KILL_STREAK, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_SOLO_STREAK)));
                    if (ModConfig.BigUIShowFriendlyKills)
                        GUILayout.Label(LABEL_PVP, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_PVP)));
                    if (ModConfig.BigUIShowEnemyShields)
                        GUILayout.Label(LABEL_ENEMY_SHIELDS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_SHIELDS)));
                    if (ModConfig.BigUIShowShieldsLost)
                        GUILayout.Label(LABEL_SHIELDS_LOST, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_SHIELDS_LOST)));
                    if (ModConfig.BigUIShowFriendlyShields)
                        GUILayout.Label(LABEL_PLAYER_SHIELDS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_PLAYER_SHIELDS)));
                    if (ModConfig.BigUIShowWaveClutches)
                        GUILayout.Label(LABEL_WAVE_CLUTCHES, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WAVE_CLUTCHES)));
                    if (ModConfig.BigUIShowAliveTime)
                        GUILayout.Label(LABEL_ALIVE_TIME, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ALIVE_TIME)));
                    if (ModConfig.BigUIShowWebSwings)
                        GUILayout.Label(LABEL_WEB_SWINGS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEB_SWINGS)));
                    if (ModConfig.BigUIShowWebSwingTime)
                        GUILayout.Label(LABEL_WEB_SWING_TIME, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEB_SWING_TIME)));
                    if (ModConfig.BigUIShowAirborneTime)
                        GUILayout.Label(LABEL_AIRBORNE_TIME, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_AIRBORNE_TIME)));
                    if (ModConfig.BigUIShowLavaDeaths)
                        GUILayout.Label(LABEL_LAVA_DEATHS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_LAVA_DEATHS)));
                    if (ModConfig.BigUIShowAstralReturns)
                        GUILayout.Label(LABEL_ASTRAL_RETURNS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ASTRAL_RETURNS)));

                    // Enemy kill columns
                    if (ModConfig.BigUIShowWaspKills)
                        GUILayout.Label(LABEL_WASP_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                    if (ModConfig.BigUIShowPowerWaspKills)
                        GUILayout.Label(LABEL_POWER_WASP_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                    if (ModConfig.BigUIShowRollerKills)
                        GUILayout.Label(LABEL_ROLLER_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                    if (ModConfig.BigUIShowWhispKills)
                        GUILayout.Label(LABEL_WHISP_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                    if (ModConfig.BigUIShowPowerWhispKills)
                        GUILayout.Label(LABEL_POWER_WHISP_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                    if (ModConfig.BigUIShowMeleeWhispKills)
                        GUILayout.Label(LABEL_MELEE_WHISP_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                    if (ModConfig.BigUIShowPowerMeleeWhispKills)
                        GUILayout.Label(LABEL_POWER_MELEE_WHISP_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                    if (ModConfig.BigUIShowKhepriKills)
                        GUILayout.Label(LABEL_KHEPRI_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                    if (ModConfig.BigUIShowPowerKhepriKills)
                        GUILayout.Label(LABEL_POWER_KHEPRI_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                    if (ModConfig.BigUIShowHornetShamanKills)
                        GUILayout.Label(LABEL_HORNET_SHAMAN_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                    if (ModConfig.BigUIShowHornetKills)
                        GUILayout.Label(LABEL_HORNET_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));

                    // Weapon kill columns
                    if (ModConfig.BigUIShowShotgunKills)
                        GUILayout.Label(LABEL_SHOTGUN_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                    if (ModConfig.BigUIShowRailShotKills)
                        GUILayout.Label(LABEL_RAILSHOT_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                    if (ModConfig.BigUIShowDeathCubeKills)
                        GUILayout.Label(LABEL_DEATHCUBE_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                    if (ModConfig.BigUIShowDeathRayKills)
                        GUILayout.Label(LABEL_DEATHRAY_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                    if (ModConfig.BigUIShowEnergyBallKills)
                        GUILayout.Label(LABEL_ENERGYBALL_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                    if (ModConfig.BigUIShowParticleBladeKills)
                        GUILayout.Label(LABEL_PARTICLEBLADE_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                    if (ModConfig.BigUIShowKhepriStaffKills)
                        GUILayout.Label(LABEL_KHEPRISTAFF_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                    if (ModConfig.BigUIShowLaserCannonKills)
                        GUILayout.Label(LABEL_LASERCANNON_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                    if (ModConfig.BigUIShowLaserCubeKills)
                        GUILayout.Label(LABEL_LASERCUBE_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                    if (ModConfig.BigUIShowSawDiscKills)
                        GUILayout.Label(LABEL_SAWDISC_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                    if (ModConfig.BigUIShowExplosionKills)
                        GUILayout.Label(LABEL_EXPLOSION_KILLS, headerStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));

                    GUILayout.EndHorizontal();

                    GUILayout.Space(SPACING_BETWEEN_HEADERS);

                    foreach (var playerEntry in statsSnapshot.ActivePlayers)
                    {
                        var playerData = playerEntry.Value;

                        GUILayout.BeginHorizontal();
                        // Player name is always shown
                        dynamicPlayerNameStyle.normal.textColor = playerData.PlayerColor;
                        GUILayout.Label(playerData.PlayerName, dynamicPlayerNameStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_PLAYER_NAME)));

                        // Computed stat data
                        if (ModConfig.BigUIShowTotalOffence)
                        {
                            int totalOffence = playerData.Kills + playerData.EnemyShieldsTakenDown;
                            GUILayout.Label(totalOffence.ToString(), totalOffence > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_TOTAL_OFFENCE)));
                        }
                        if (ModConfig.BigUIShowTotalFriendlyHits)
                        {
                            int totalFriendly = playerData.FriendlyKills + playerData.FriendlyShieldsHit;
                            GUILayout.Label(totalFriendly.ToString(), totalFriendly > 0 ? friendlyKillsOrangeStyle : friendlyKillsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_TOTAL_FRIENDLY)));
                        }
                        if (ModConfig.BigUIShowTotalHitsTaken)
                        {
                            int totalHits = playerData.Deaths + playerData.ShieldsLost;
                            GUILayout.Label(totalHits.ToString(), totalHits > 0 ? deathsRedStyle : deathsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_TOTAL_HITS)));
                        }

                        // Basic stats data
                        if (ModConfig.BigUIShowKills)
                            GUILayout.Label(playerData.Kills.ToString(), playerData.Kills > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_KILLS)));
                        if (ModConfig.BigUIShowDeaths)
                            GUILayout.Label(playerData.Deaths.ToString(), playerData.Deaths > 0 ? deathsRedStyle : deathsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_DEATHS)));
                        if (ModConfig.BigUIShowMaxKillStreak)
                            GUILayout.Label(playerData.MaxKillStreak.ToString(), playerData.MaxKillStreak > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_KILL_STREAK)));
                        if (ModConfig.BigUIShowCurrentKillStreak)
                            GUILayout.Label(playerData.KillStreak.ToString(), playerData.KillStreak > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_CURRENT_STREAK)));
                        if (ModConfig.BigUIShowMaxSoloKillStreak)
                            GUILayout.Label(playerData.MaxKillStreakWhileSolo.ToString(), playerData.MaxKillStreakWhileSolo > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_SOLO_STREAK)));
                        if (ModConfig.BigUIShowCurrentSoloKillStreak)
                            GUILayout.Label(playerData.KillStreakWhileSolo.ToString(), playerData.KillStreakWhileSolo > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_SOLO_STREAK)));
                        if (ModConfig.BigUIShowFriendlyKills)
                            GUILayout.Label(playerData.FriendlyKills.ToString(), playerData.FriendlyKills > 0 ? friendlyKillsOrangeStyle : friendlyKillsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_PVP)));
                        if (ModConfig.BigUIShowEnemyShields)
                            GUILayout.Label(playerData.EnemyShieldsTakenDown.ToString(), playerData.EnemyShieldsTakenDown > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_SHIELDS)));
                        if (ModConfig.BigUIShowShieldsLost)
                            GUILayout.Label(playerData.ShieldsLost.ToString(), playerData.ShieldsLost > 0 ? deathsRedStyle : deathsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_SHIELDS_LOST)));
                        if (ModConfig.BigUIShowFriendlyShields)
                            GUILayout.Label(playerData.FriendlyShieldsHit.ToString(), playerData.FriendlyShieldsHit > 0 ? friendlyKillsOrangeStyle : friendlyKillsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_PLAYER_SHIELDS)));
                        if (ModConfig.BigUIShowWaveClutches)
                            GUILayout.Label(playerData.WaveClutches.ToString(), playerData.WaveClutches > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WAVE_CLUTCHES)));
                        if (ModConfig.BigUIShowAliveTime)
                            GUILayout.Label(TimeFormatUtils.FormatTimeSpan(playerData.GetCurrentAliveTime()), centeredWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ALIVE_TIME)));
                        if (ModConfig.BigUIShowWebSwings)
                            GUILayout.Label(playerData.WebSwings.ToString(), playerData.WebSwings > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEB_SWINGS)));
                        if (ModConfig.BigUIShowWebSwingTime)
                            GUILayout.Label(TimeFormatUtils.FormatTimeSpan(playerData.GetCurrentWebSwingTime()), centeredWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEB_SWING_TIME)));
                        if (ModConfig.BigUIShowAirborneTime)
                            GUILayout.Label(TimeFormatUtils.FormatTimeSpan(playerData.GetCurrentAirborneTime()), centeredWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_AIRBORNE_TIME)));
                        if (ModConfig.BigUIShowLavaDeaths)
                            GUILayout.Label(playerData.LavaDeaths.ToString(), playerData.LavaDeaths > 0 ? deathsRedStyle : deathsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_LAVA_DEATHS)));
                        if (ModConfig.BigUIShowAstralReturns)
                            GUILayout.Label(playerData.AstralReturns.ToString(), playerData.AstralReturns > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ASTRAL_RETURNS)));

                        // Enemy kill data
                        if (ModConfig.BigUIShowWaspKills)
                        {
                            int val = playerData.EnemyKills.TryGetValue("Wasp", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                        }
                        if (ModConfig.BigUIShowPowerWaspKills)
                        {
                            int val = playerData.EnemyKills.TryGetValue("Power Wasp", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                        }
                        if (ModConfig.BigUIShowRollerKills)
                        {
                            int val = playerData.EnemyKills.TryGetValue("Roller", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                        }
                        if (ModConfig.BigUIShowWhispKills)
                        {
                            int val = playerData.EnemyKills.TryGetValue("Whisp", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                        }
                        if (ModConfig.BigUIShowPowerWhispKills)
                        {
                            int val = playerData.EnemyKills.TryGetValue("Power Whisp", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                        }
                        if (ModConfig.BigUIShowMeleeWhispKills)
                        {
                            int val = playerData.EnemyKills.TryGetValue("Melee Whisp", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                        }
                        if (ModConfig.BigUIShowPowerMeleeWhispKills)
                        {
                            int val = playerData.EnemyKills.TryGetValue("Power Melee Whisp", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                        }
                        if (ModConfig.BigUIShowKhepriKills)
                        {
                            int val = playerData.EnemyKills.TryGetValue("Khepri", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                        }
                        if (ModConfig.BigUIShowPowerKhepriKills)
                        {
                            int val = playerData.EnemyKills.TryGetValue("Power Khepri", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                        }
                        if (ModConfig.BigUIShowHornetShamanKills)
                        {
                            int val = playerData.EnemyKills.TryGetValue("Hornet Shaman", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                        }
                        if (ModConfig.BigUIShowHornetKills)
                        {
                            int val = playerData.EnemyKills.TryGetValue("Hornet", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_ENEMY_KILLS)));
                        }

                        // Weapon kill data
                        if (ModConfig.BigUIShowShotgunKills)
                        {
                            int val = playerData.WeaponHits.TryGetValue("Shotgun", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                        }
                        if (ModConfig.BigUIShowRailShotKills)
                        {
                            int val = playerData.WeaponHits.TryGetValue("RailShot", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                        }
                        if (ModConfig.BigUIShowDeathCubeKills)
                        {
                            int val = playerData.WeaponHits.TryGetValue("DeathCube", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                        }
                        if (ModConfig.BigUIShowDeathRayKills)
                        {
                            int val = playerData.WeaponHits.TryGetValue("DeathRay", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                        }
                        if (ModConfig.BigUIShowEnergyBallKills)
                        {
                            int val = playerData.WeaponHits.TryGetValue("EnergyBall", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                        }
                        if (ModConfig.BigUIShowParticleBladeKills)
                        {
                            int val = playerData.WeaponHits.TryGetValue("Particle Blade", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                        }
                        if (ModConfig.BigUIShowKhepriStaffKills)
                        {
                            int val = playerData.WeaponHits.TryGetValue("KhepriStaff", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                        }
                        if (ModConfig.BigUIShowLaserCannonKills)
                        {
                            int val = playerData.WeaponHits.TryGetValue("Laser Cannon", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                        }
                        if (ModConfig.BigUIShowLaserCubeKills)
                        {
                            int val = playerData.WeaponHits.TryGetValue("Laser Cube", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                        }
                        if (ModConfig.BigUIShowSawDiscKills)
                        {
                            int val = playerData.WeaponHits.TryGetValue("SawDisc", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                        }
                        if (ModConfig.BigUIShowExplosionKills)
                        {
                            int val = playerData.WeaponHits.TryGetValue("Explosions", out int v) ? v : 0;
                            GUILayout.Label(val.ToString(), val > 0 ? killsGreenStyle : killsWhiteStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_WEAPON_KILLS)));
                        }

                        GUILayout.EndHorizontal();

                        GUILayout.Space(SPACING_BETWEEN_PLAYERS);
                    }
                }
                else
                {
                    GUILayout.Label(LABEL_NO_PLAYERS, labelStyle);
                }
            }
            catch (System.Exception ex)
            {
                GUILayout.Label($"Error: {ex.Message}", errorStyle);
                Logger.LogError($"Error displaying player stats: {ex.Message}");
            }
            GUILayout.EndVertical();
        }

        #endregion

        #region Event Handling
        public void OnPlayerJoined()
        {
            CalculateContentHeight();
        }

        public void OnPlayerLeft()
        {
            CalculateContentHeight();
        }
        #endregion
    }
}
