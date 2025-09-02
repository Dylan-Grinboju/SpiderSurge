using UnityEngine;
using Silk;
using Logger = Silk.Logger;
using System;

namespace StatsMod
{
    public class BigUI : MonoBehaviour
    {
        #region Constants
        private const float BASE_SECTION_SPACING = 25f;
        private const float BASE_CARD_PADDING = 20f;
        private const float BASE_PLAYER_ROW_HEIGHT = 40f;
        private const float BASE_SURVIVAL_SECTION_HEIGHT = 100f;

        // Scaled properties
        private float SectionSpacing => UIManager.ScaleValue(BASE_SECTION_SPACING);
        private float CardPadding => UIManager.ScaleValue(BASE_CARD_PADDING);
        private float PlayerRowHeight => UIManager.ScaleValue(BASE_PLAYER_ROW_HEIGHT);
        private float SurvivalSectionHeight => UIManager.ScaleValue(BASE_SURVIVAL_SECTION_HEIGHT);

        private float Total_Height = 0f;
        #endregion

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

        #region Style Management
        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            backgroundStyle = new GUIStyle()
            {
                normal = { background = UIManager.Instance.GetDarkTexture() }
            };

            headerStyle = UIManager.Instance.CreateHeaderStyle(UIManager.BIG_HEADER_SIZE);
            labelStyle = UIManager.Instance.CreateLabelStyle(UIManager.BIG_LABEL_SIZE);
            valueStyle = UIManager.Instance.CreateValueStyle(UIManager.BIG_LABEL_SIZE);
            cardStyle = UIManager.Instance.CreateCardStyle(UIManager.Instance.GetMediumTexture());
            errorStyle = UIManager.Instance.CreateErrorStyle(UIManager.BIG_LABEL_SIZE);

            cardStyle.padding = new RectOffset((int)CardPadding, (int)CardPadding, (int)CardPadding, (int)CardPadding);

            stylesInitialized = true;
        }
        #endregion

        #region GUI Drawing
        private void OnGUI()
        {
            if (!isDisplayVisibleAtAll || !isDisplayVisible) return;

            InitializeStyles();

            // Calculate background rect with dynamic height based on content
            float marginX = Screen.width * 0.2f;
            CalculateContentHeight();
            float backgroundHeight = Mathf.Min(Total_Height + 80f, Screen.height * 0.8f); // Add padding and cap at 80% of screen
            float backgroundY = (Screen.height - backgroundHeight) * 0.5f; // Center vertically

            Rect backgroundRect = new Rect(marginX, backgroundY, Screen.width - (marginX * 2), backgroundHeight);

            GUI.Box(backgroundRect, "", backgroundStyle);

            // Calculate content area within the background
            float contentPadding = 40f;
            float contentWidth = backgroundRect.width - (contentPadding * 2);
            float availableContentHeight = backgroundRect.height - (contentPadding * 2);

            GUILayout.BeginArea(new Rect(backgroundRect.x + contentPadding, backgroundRect.y + contentPadding, contentWidth, availableContentHeight));

            var statsSnapshot = StatsManager.Instance.GetStatsSnapshot();

            // Draw survival and enemy stats horizontally if both are enabled
            if (ModConfig.ShowPlayTime && ModConfig.ShowEnemyDeaths)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(contentWidth * 0.48f));
                DrawSurvivalModeStats(statsSnapshot);
                GUILayout.EndVertical();

                GUILayout.Space(contentWidth * 0.04f); // 4% spacing between cards

                GUILayout.BeginVertical(GUILayout.Width(contentWidth * 0.48f));
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

            GUILayout.EndArea();
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
                var statsSnapshot = StatsManager.Instance?.GetStatsSnapshot();
                float playerSectionHeight = UIManager.ScaleValue(120f); // Base height for player section
                if (statsSnapshot?.ActivePlayers != null)
                {
                    playerSectionHeight += statsSnapshot.ActivePlayers.Count * PlayerRowHeight;
                }
                totalHeight += playerSectionHeight;
            }

            Total_Height = totalHeight;
        }

        private void DrawSurvivalModeStats(GameStatsSnapshot statsSnapshot)
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label("Survival Mode", headerStyle);

            GUILayout.BeginHorizontal();
            if (statsSnapshot.IsSurvivalActive)
            {
                GUILayout.Label("Time:", labelStyle, GUILayout.Width(100));
                var timerStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.Green } };
                GUILayout.Label(FormatTimeSpan(statsSnapshot.CurrentSessionTime), timerStyle);
            }
            else
            {
                GUILayout.Label("Last Game:", labelStyle, GUILayout.Width(300));
                var statusStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.Gray } };
                GUILayout.Label(statsSnapshot.LastGameDuration.TotalSeconds > 0 ? FormatTimeSpan(statsSnapshot.LastGameDuration) : "No games yet", statusStyle);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawEnemyStats(GameStatsSnapshot statsSnapshot)
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label("Enemy Statistics", headerStyle);

            try
            {
                int enemiesKilled = statsSnapshot.EnemiesKilled;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Enemies Killed:", labelStyle, GUILayout.Width(300));
                var killsStyle = new GUIStyle(valueStyle) { normal = { textColor = enemiesKilled > 0 ? UIManager.Green : UIManager.White } };
                GUILayout.Label(enemiesKilled.ToString(), killsStyle);
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
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Player", headerStyle, GUILayout.Width(UIManager.ScaleValue(180)));
                    GUILayout.Label("Deaths", headerStyle, GUILayout.Width(UIManager.ScaleValue(150)));
                    GUILayout.Label("Kills", headerStyle, GUILayout.Width(UIManager.ScaleValue(120)));
                    GUILayout.EndHorizontal();

                    GUILayout.Space(10);

                    foreach (var playerEntry in statsSnapshot.ActivePlayers)
                    {
                        var playerData = playerEntry.Value;

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("", valueStyle, GUILayout.Width(UIManager.ScaleValue(5)));

                        var playerNameStyle = new GUIStyle(valueStyle) { normal = { textColor = playerData.PlayerColor } };
                        GUILayout.Label(playerData.PlayerName, playerNameStyle, GUILayout.Width(UIManager.ScaleValue(205)));

                        var deathsStyle = new GUIStyle(valueStyle) { normal = { textColor = playerData.Deaths > 0 ? UIManager.Red : UIManager.White } };
                        GUILayout.Label(playerData.Deaths.ToString(), deathsStyle, GUILayout.Width(UIManager.ScaleValue(140)));

                        var killsStyle = new GUIStyle(valueStyle) { normal = { textColor = playerData.Kills > 0 ? UIManager.Green : UIManager.White } };
                        GUILayout.Label(playerData.Kills.ToString(), killsStyle, GUILayout.Width(UIManager.ScaleValue(120)));
                        GUILayout.EndHorizontal();

                        GUILayout.Space(15);
                    }
                }
                else
                {
                    GUILayout.Label("No players connected", labelStyle);
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

        #region Utility Methods
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            return $"{timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
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
