using UnityEngine;
using Silk;
using Logger = Silk.Logger;
using System;

namespace StatsMod
{
    public class SmallUI : MonoBehaviour
    {
        #region Constants
        private const float BASE_WINDOW_WIDTH = 300f;
        private const float BASE_WINDOW_HEIGHT = 35f;

        private const float BASE_PLAY_TIME_HEIGHT = 45f;
        private const float BASE_ENEMY_DEATHS_HEIGHT = 45f;
        private const float BASE_PLAYER_STATS_BASE_HEIGHT = 60f;
        private const float BASE_PLAYER_STATS_PER_PLAYER_HEIGHT = 35f;

        // Scaled properties
        private float WindowWidth => UIManager.ScaleValue(BASE_WINDOW_WIDTH);
        private float WindowHeight => UIManager.ScaleValue(BASE_WINDOW_HEIGHT);
        private float PlayTimeHeight => UIManager.ScaleValue(BASE_PLAY_TIME_HEIGHT);
        private float EnemyDeathsHeight => UIManager.ScaleValue(BASE_ENEMY_DEATHS_HEIGHT);
        private float PlayerStatsBaseHeight => UIManager.ScaleValue(BASE_PLAYER_STATS_BASE_HEIGHT);
        private float PlayerStatsPerPlayerHeight => UIManager.ScaleValue(BASE_PLAYER_STATS_PER_PLAYER_HEIGHT);
        #endregion

        #region UI State
        private bool isDisplayVisible = false;
        private bool isDisplayVisibleAtAll = true;
        private Rect windowRect;
        #endregion

        #region GUI Styles
        private GUIStyle windowStyle;
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
            float xPos = ModConfig.DisplayPositionX;
            float yPos = ModConfig.DisplayPositionY;

            windowRect = new Rect(xPos, yPos, WindowWidth, 100f);
            isDisplayVisibleAtAll = ModConfig.ShowStatsWindow;
            UpdateWindowSize();
            isDisplayVisible = isDisplayVisibleAtAll;

            Logger.LogInfo("SmallUI initialized");
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

            windowStyle = UIManager.Instance.CreateWindowStyle(UIManager.Instance.GetDarkTexture());
            headerStyle = UIManager.Instance.CreateHeaderStyle();
            labelStyle = UIManager.Instance.CreateLabelStyle();
            valueStyle = UIManager.Instance.CreateValueStyle();
            cardStyle = UIManager.Instance.CreateCardStyle(UIManager.Instance.GetMediumTexture());
            errorStyle = UIManager.Instance.CreateErrorStyle();

            stylesInitialized = true;
        }
        #endregion

        #region GUI Drawing
        private void OnGUI()
        {
            if (!isDisplayVisibleAtAll || !isDisplayVisible) return;

            InitializeStyles();
            UpdateWindowSize();

            Vector2 oldPosition = new Vector2(windowRect.x, windowRect.y);
            windowRect = GUI.Window(0, windowRect, DrawStatsWindow, "", windowStyle);

            Vector2 newPosition = new Vector2(windowRect.x, windowRect.y);
            if (oldPosition != newPosition)
            {
                ModConfig.SetDisplayPosition((int)newPosition.x, (int)newPosition.y);
            }
        }

        private void DrawStatsWindow(int windowID)
        {
            GUILayout.BeginVertical();

            var statsSnapshot = StatsManager.Instance.GetStatsSnapshot();

            if (ModConfig.ShowPlayTime)
            {
                DrawSurvivalModeStats(statsSnapshot);
            }

            if (ModConfig.ShowEnemyDeaths)
            {
                DrawEnemyStats(statsSnapshot);
            }

            if (ModConfig.ShowPlayers)
            {
                DrawPlayerStats(statsSnapshot);
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }
        #endregion

        #region Drawing Methods
        private void DrawSurvivalModeStats(GameStatsSnapshot statsSnapshot)
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label("Survival Mode", headerStyle);

            GUILayout.BeginHorizontal();
            if (statsSnapshot.IsSurvivalActive)
            {
                GUILayout.Label("Time:", labelStyle, GUILayout.Width(UIManager.ScaleValue(50)));
                var timerStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.Green } };
                GUILayout.Label(FormatTimeSpan(statsSnapshot.CurrentSessionTime), timerStyle, GUILayout.MinWidth(UIManager.ScaleValue(80)));
            }
            else
            {
                GUILayout.Label("Last Game:", labelStyle, GUILayout.Width(UIManager.ScaleValue(120)));
                var statusStyle = new GUIStyle(valueStyle) { normal = { textColor = UIManager.Gray } };
                GUILayout.Label(statsSnapshot.LastGameDuration.TotalSeconds > 0 ? FormatTimeSpan(statsSnapshot.LastGameDuration) : "No games yet", statusStyle);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(UIManager.ScaleValue(4));
        }

        private void DrawEnemyStats(GameStatsSnapshot statsSnapshot)
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label("Enemy Statistics", headerStyle);

            try
            {
                int enemiesKilled = statsSnapshot.EnemiesKilled;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Enemies Killed:", labelStyle, GUILayout.Width(UIManager.ScaleValue(120)));
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
            GUILayout.Space(UIManager.ScaleValue(4));
        }

        private void DrawPlayerStats(GameStatsSnapshot statsSnapshot)
        {
            GUILayout.BeginVertical(cardStyle);
            try
            {
                if (statsSnapshot.ActivePlayers != null && statsSnapshot.ActivePlayers.Count > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Player", headerStyle, GUILayout.Width(UIManager.ScaleValue(95)));
                    GUILayout.Label("Deaths", headerStyle, GUILayout.Width(UIManager.ScaleValue(100)));
                    GUILayout.Label("Kills", headerStyle, GUILayout.Width(UIManager.ScaleValue(60)));
                    GUILayout.EndHorizontal();

                    GUILayout.Space(UIManager.ScaleValue(4));

                    foreach (var playerEntry in statsSnapshot.ActivePlayers)
                    {
                        var playerData = playerEntry.Value;

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("", valueStyle, GUILayout.Width(UIManager.ScaleValue(5)));

                        var playerNameStyle = new GUIStyle(valueStyle) { normal = { textColor = playerData.PlayerColor } };
                        GUILayout.Label(playerData.PlayerName, playerNameStyle, GUILayout.Width(UIManager.ScaleValue(115)));

                        var deathsStyle = new GUIStyle(valueStyle) { normal = { textColor = playerData.Deaths > 0 ? UIManager.Red : UIManager.White } };
                        GUILayout.Label(playerData.Deaths.ToString(), deathsStyle, GUILayout.Width(UIManager.ScaleValue(90)));

                        var killsStyle = new GUIStyle(valueStyle) { normal = { textColor = playerData.Kills > 0 ? UIManager.Green : UIManager.White } };
                        GUILayout.Label(playerData.Kills.ToString(), killsStyle, GUILayout.Width(UIManager.ScaleValue(60)));
                        GUILayout.EndHorizontal();

                        GUILayout.Space(UIManager.ScaleValue(8));
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

        #region Window Size Management
        private void UpdateWindowSize()
        {
            float totalHeight = WindowHeight;

            if (ModConfig.ShowPlayTime)
            {
                totalHeight += PlayTimeHeight;
            }

            if (ModConfig.ShowEnemyDeaths)
            {
                totalHeight += EnemyDeathsHeight;
            }

            if (ModConfig.ShowPlayers)
            {
                totalHeight += PlayerStatsBaseHeight;

                var statsSnapshot = StatsManager.Instance?.GetStatsSnapshot();
                if (statsSnapshot?.ActivePlayers != null)
                {
                    totalHeight += statsSnapshot.ActivePlayers.Count * PlayerStatsPerPlayerHeight;
                }
            }

            windowRect.height = totalHeight;
        }

        public void OnPlayerJoined()
        {
            UpdateWindowSize();
        }

        public void OnPlayerLeft()
        {
            UpdateWindowSize();
        }
        #endregion
    }
}
