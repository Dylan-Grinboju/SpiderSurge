using UnityEngine;
using Silk;
using Logger = Silk.Logger;

namespace StatsMod
{
    public class SmallUI : MonoBehaviour
    {
        #region Base Dimensions
        public float BASE_WINDOW_WIDTH = 200f;
        public float BASE_WINDOW_HEIGHT = 15f;
        public float BASE_HEADER_HEIGHT = 35f;
        public float BASE_PLAYER_ROW_HEIGHT = 30f;
        public float BASE_PADDING = 10f;
        #endregion

        #region Column Widths
        public float COL_WIDTH_KILL = 50f;
        public float COL_WIDTH_DEATH = 80f;
        #endregion

        #region Text Labels
        public string LABEL_KILL = "Kills";
        public string LABEL_DEATH = "Deaths";
        public string LABEL_NO_PLAYERS = "No players";
        #endregion

        #region Spacing
        public float SPACING_BETWEEN_ROWS = 2f;
        #endregion

        // Scaled properties
        private float WindowWidth => UIManager.ScaleValue(BASE_WINDOW_WIDTH);
        private float WindowHeight => UIManager.ScaleValue(BASE_WINDOW_HEIGHT);
        private float HeaderHeight => UIManager.ScaleValue(BASE_HEADER_HEIGHT);
        private float PlayerRowHeight => UIManager.ScaleValue(BASE_PLAYER_ROW_HEIGHT);
        private float Padding => UIManager.ScaleValue(BASE_PADDING);

        #region UI State
        private bool isDisplayVisible = false;
        private bool isDisplayVisibleAtAll = true;
        private Rect windowRect;
        #endregion

        #region GUI Styles
        private GUIStyle windowStyle;
        private GUIStyle transparentWindowStyle;
        private GUIStyle headerStyle;
        private GUIStyle headerCenteredStyle;
        private GUIStyle labelStyle;
        private GUIStyle playerColorCenteredStyle;
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
            
            // Create transparent window style for no-background mode
            transparentWindowStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = null },
                padding = new RectOffset(UIManager.ScaleInt(UIManager.PADDING * 2), UIManager.ScaleInt(UIManager.PADDING * 2), UIManager.ScaleInt(UIManager.PADDING * 2), UIManager.ScaleInt(UIManager.PADDING * 2))
            };
            
            headerStyle = UIManager.Instance.CreateHeaderStyle();
            headerCenteredStyle = new GUIStyle(headerStyle) { alignment = TextAnchor.MiddleCenter };
            labelStyle = UIManager.Instance.CreateLabelStyle();
            playerColorCenteredStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter };

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
            GUIStyle activeWindowStyle = ModConfig.SmallUIShowBackground ? windowStyle : transparentWindowStyle;
            windowRect = GUI.Window(0, windowRect, DrawStatsWindow, "", activeWindowStyle);

            Vector2 newPosition = new Vector2(windowRect.x, windowRect.y);
            if (oldPosition != newPosition)
            {
                ModConfig.SetDisplayPosition((int)newPosition.x, (int)newPosition.y);
            }
        }

        private void DrawStatsWindow(int windowID)
        {
            GUILayout.BeginVertical();

            var statsSnapshot = GameSessionManager.Instance.GetStatsSnapshot();
            DrawPlayerStats(statsSnapshot);

            GUILayout.EndVertical();
            GUI.DragWindow();
        }
        #endregion

        #region Drawing Methods
        private void DrawPlayerStats(GameStatsSnapshot statsSnapshot)
        {
            try
            {
                // Header row
                GUILayout.BeginHorizontal();
                GUILayout.Label(LABEL_KILL, headerCenteredStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_KILL)));
                GUILayout.Label(LABEL_DEATH, headerCenteredStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_DEATH)));
                GUILayout.EndHorizontal();

                if (statsSnapshot.ActivePlayers != null && statsSnapshot.ActivePlayers.Count > 0)
                {
                    foreach (var playerEntry in statsSnapshot.ActivePlayers)
                    {
                        var playerData = playerEntry.Value;

                        // Set the player's color for the style
                        playerColorCenteredStyle.normal.textColor = playerData.PlayerColor;

                        GUILayout.Space(UIManager.ScaleValue(SPACING_BETWEEN_ROWS));

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(playerData.Kills.ToString(), playerColorCenteredStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_KILL)));
                        GUILayout.Label(playerData.Deaths.ToString(), playerColorCenteredStyle, GUILayout.Width(UIManager.ScaleValue(COL_WIDTH_DEATH)));
                        GUILayout.EndHorizontal();
                    }
                }
                else
                {
                    GUILayout.Label(LABEL_NO_PLAYERS, labelStyle);
                }
            }
            catch (System.Exception ex)
            {
                GUILayout.Label($"Error: {ex.Message}", labelStyle);
                Logger.LogError($"Error displaying player stats: {ex.Message}");
            }
        }
        #endregion

        #region Window Size Management
        private void UpdateWindowSize()
        {
            float totalHeight = WindowHeight + HeaderHeight;

            var statsSnapshot = GameSessionManager.Instance?.GetStatsSnapshot();
            if (statsSnapshot?.ActivePlayers != null && statsSnapshot.ActivePlayers.Count > 0)
            {
                totalHeight += statsSnapshot.ActivePlayers.Count * PlayerRowHeight;
            }
            else
            {
                totalHeight += PlayerRowHeight; // Space for "No players" message
            }

            windowRect.height = totalHeight;

            // Calculate width based on kill + death columns + padding
            float dynamicWidth = UIManager.ScaleValue(COL_WIDTH_KILL) + UIManager.ScaleValue(COL_WIDTH_DEATH) + Padding * 2;
            windowRect.width = dynamicWidth;
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
