using UnityEngine;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace StatsMod
{
    public class SmallUI : MonoBehaviour
    {
        private const int WindowId = 0x5353;

        #region Base Dimensions
        public float BASE_HEADER_HEIGHT = 20f;
        public float BASE_PLAYER_ROW_HEIGHT = 30f;
        public float BASE_PADDING = 10f;
        public float BASE_BAR_WIDTH = 4f;
        public float BASE_BAR_GAP = 8f;
        #endregion

        #region Column Widths
        public float COL_WIDTH_KILL = 62f;
        public float COL_WIDTH_DEATH = 72f;
        #endregion

        #region Text Labels
        public string LABEL_KILL = "KILLS";
        public string LABEL_DEATH = "DEATHS";
        public string LABEL_NO_PLAYERS = "NO PLAYERS";
        #endregion

        #region Font Sizes
        public int HEADER_FONT_SIZE = 12;
        public int VALUE_FONT_SIZE = 21;
        #endregion

        public float DEAD_PLAYER_ALPHA = 0.5f;
        public float MAX_HEIGHT_PERCENT = 0.9f;

        private float scale = 1f;
        private float HeaderHeight => BASE_HEADER_HEIGHT * scale;
        private float PlayerRowHeight => BASE_PLAYER_ROW_HEIGHT * scale;
        private float Padding => BASE_PADDING * scale;
        private float Scaled(float value) => value * scale;

        #region UI State
        private bool isDisplayVisible = false;
        private bool isDisplayVisibleAtAll => ModConfig.ShowStatsWindow;
        private Rect windowRect;
        private GameStatsSnapshot statsSnapshot;
        private GUI.WindowFunction drawWindow;
        #endregion

        #region GUI Styles
        private GUIStyle windowStyle;
        private GUIStyle headerStyle;
        private GUIStyle noPlayersStyle;
        private GUIStyle valueStyle;
        private int stylesVersion = -1;
        private int fontVersion = -1;
        private float stylesScale = -1f;
        #endregion

        #region Initialization
        public void Initialize()
        {
            isDisplayVisible = isDisplayVisibleAtAll;

            Logger.LogInfo("SmallUI initialized");
        }
        #endregion

        #region Display Control
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
            Hud.EnsureFonts();
            if (stylesVersion != UIManager.StyleVersion || fontVersion != Hud.FontVersion || windowStyle == null)
            {
                windowStyle = new GUIStyle();
                headerStyle = Hud.Style(HEADER_FONT_SIZE, bold: true, anchor: TextAnchor.MiddleCenter);
                noPlayersStyle = Hud.Style(HEADER_FONT_SIZE, bold: true, anchor: TextAnchor.MiddleCenter);
                valueStyle = Hud.Style(VALUE_FONT_SIZE, bold: true, anchor: TextAnchor.MiddleCenter);

                stylesVersion = UIManager.StyleVersion;
                fontVersion = Hud.FontVersion;
                stylesScale = -1f;
            }

            if (stylesScale != scale)
            {
                Hud.Resize(headerStyle, HEADER_FONT_SIZE, scale);
                Hud.Resize(noPlayersStyle, HEADER_FONT_SIZE, scale);
                Hud.Resize(valueStyle, VALUE_FONT_SIZE, scale);
                stylesScale = scale;
            }
        }
        #endregion

        #region GUI Drawing
        private void OnGUI()
        {
            if (!isDisplayVisibleAtAll || !isDisplayVisible || GameMenus.Open) return;

            statsSnapshot = GameSessionManager.Instance?.GetDisplaySnapshot();
            UpdateWindowSize(statsSnapshot?.ActivePlayers?.Count ?? 0);
            InitializeStyles();
            Vector2 saved = ModConfig.DisplayPosition;
            windowRect.x = saved.x / 100f * Screen.width;
            windowRect.y = saved.y / 100f * Screen.height;
            ClampToScreen();

            Vector2 placed = windowRect.position;
            windowRect = GUI.Window(WindowId, windowRect, drawWindow ??= DrawStatsWindow, "", windowStyle);

            if (windowRect.position != placed)
            {
                ClampToScreen();
                ModConfig.SetDisplayPosition(windowRect.position);
            }
        }

        private void ClampToScreen()
        {
            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height - windowRect.height));
        }

        private void DrawStatsWindow(int windowID)
        {
            try
            {
                bool background = ModConfig.SmallUIShowBackground;
                if (background)
                {
                    Hud.Fill(new Rect(0f, 0f, windowRect.width, windowRect.height), Hud.Ink);
                    Hud.Fill(new Rect(0f, 0f, windowRect.width, Scaled(2f)), Hud.Pink);
                }

                bool shadow = !background;
                float killX = Padding + Scaled(BASE_BAR_WIDTH + BASE_BAR_GAP);
                float killWidth = Scaled(COL_WIDTH_KILL);
                float deathX = killX + killWidth;
                float deathWidth = Scaled(COL_WIDTH_DEATH);
                float y = Padding;

                Hud.Label(new Rect(killX, y, killWidth, HeaderHeight), LABEL_KILL, headerStyle, Hud.Pink, shadow);
                Hud.Label(new Rect(deathX, y, deathWidth, HeaderHeight), LABEL_DEATH, headerStyle, Hud.Pink, shadow);
                y += HeaderHeight;

                if (statsSnapshot?.ActivePlayers == null || statsSnapshot.ActivePlayers.Count == 0)
                {
                    Hud.Label(new Rect(killX, y, killWidth + deathWidth, PlayerRowHeight), LABEL_NO_PLAYERS, noPlayersStyle, Hud.Muted, shadow);
                }
                else
                {
                    bool sessionActive = statsSnapshot.IsSurvivalActive || statsSnapshot.IsVersusActive;
                    float barInset = PlayerRowHeight * 0.18f;
                    foreach (var playerEntry in statsSnapshot.ActivePlayers)
                    {
                        var playerData = playerEntry.Value;
                        float alpha = sessionActive && !playerData.IsAlive ? DEAD_PLAYER_ALPHA : 1f;

                        Hud.Fill(new Rect(Padding, y + barInset, Scaled(BASE_BAR_WIDTH), PlayerRowHeight - barInset * 2), Hud.WithAlpha(Hud.Readable(playerData.PlayerColor), alpha));
                        Hud.Label(new Rect(killX, y, killWidth, PlayerRowHeight), Hud.Number(playerData.Kills), valueStyle, Hud.WithAlpha(playerData.Kills > 0 ? Hud.Text : Hud.Muted, alpha), shadow);
                        Hud.Label(new Rect(deathX, y, deathWidth, PlayerRowHeight), Hud.Number(playerData.Deaths), valueStyle, Hud.WithAlpha(playerData.Deaths > 0 ? Hud.Pink : Hud.Muted, alpha), shadow);
                        y += PlayerRowHeight;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error displaying player stats: {ex.Message}");
            }

            GUI.DragWindow();
        }
        #endregion

        #region Window Size Management
        // Many players shrink the whole window so every row fits on screen
        private void UpdateWindowSize(int rows)
        {
            float baseWidth = BASE_PADDING * 2 + BASE_BAR_WIDTH + BASE_BAR_GAP + COL_WIDTH_KILL + COL_WIDTH_DEATH;
            float baseHeight = BASE_PADDING * 2 + BASE_HEADER_HEIGHT + Mathf.Max(rows, 1) * BASE_PLAYER_ROW_HEIGHT;
            scale = Hud.FitScale(UIManager.UIScaleFactor, baseWidth, baseHeight, 1f, MAX_HEIGHT_PERCENT);
            windowRect.width = baseWidth * scale;
            windowRect.height = baseHeight * scale;
        }
        #endregion
    }
}
