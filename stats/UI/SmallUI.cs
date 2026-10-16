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

        private float HeaderHeight => UIManager.ScaleValue(BASE_HEADER_HEIGHT);
        private float PlayerRowHeight => UIManager.ScaleValue(BASE_PLAYER_ROW_HEIGHT);
        private float Padding => UIManager.ScaleValue(BASE_PADDING);

        #region UI State
        private bool isDisplayVisible = false;
        private bool isDisplayVisibleAtAll => ModConfig.ShowStatsWindow;
        private Rect windowRect;
        #endregion

        #region GUI Styles
        private GUIStyle windowStyle;
        private GUIStyle headerStyle;
        private GUIStyle valueStyle;
        private int stylesVersion = -1;
        private int fontVersion = -1;
        #endregion

        #region Initialization
        public void Initialize()
        {
            windowRect = new Rect(ModConfig.DisplayPositionX, ModConfig.DisplayPositionY, 0f, 0f);
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
            Hud.EnsureFonts();
            if (stylesVersion == UIManager.StyleVersion && fontVersion == Hud.FontVersion) return;

            windowStyle = new GUIStyle();
            headerStyle = Hud.Style(UIManager.ScaleFont(HEADER_FONT_SIZE), bold: true, anchor: TextAnchor.MiddleCenter);
            valueStyle = Hud.Style(UIManager.ScaleFont(VALUE_FONT_SIZE), bold: true, anchor: TextAnchor.MiddleCenter);

            stylesVersion = UIManager.StyleVersion;
            fontVersion = Hud.FontVersion;
        }
        #endregion

        #region GUI Drawing
        private void OnGUI()
        {
            if (!isDisplayVisibleAtAll || !isDisplayVisible) return;

            InitializeStyles();
            UpdateWindowSize();

            Vector2 oldPosition = new Vector2(windowRect.x, windowRect.y);
            windowRect = GUI.Window(WindowId, windowRect, DrawStatsWindow, "", windowStyle);

            Vector2 newPosition = new Vector2(windowRect.x, windowRect.y);
            if (oldPosition != newPosition)
            {
                ModConfig.SetDisplayPosition((int)newPosition.x, (int)newPosition.y);
            }
        }

        private void DrawStatsWindow(int windowID)
        {
            try
            {
                bool background = ModConfig.SmallUIShowBackground;
                if (background)
                {
                    Hud.Fill(new Rect(0f, 0f, windowRect.width, windowRect.height), Hud.Ink);
                    Hud.Fill(new Rect(0f, 0f, windowRect.width, UIManager.ScaleValue(2f)), Hud.Pink);
                }

                bool shadow = !background;
                float killX = Padding + UIManager.ScaleValue(BASE_BAR_WIDTH + BASE_BAR_GAP);
                float killWidth = UIManager.ScaleValue(COL_WIDTH_KILL);
                float deathX = killX + killWidth;
                float deathWidth = UIManager.ScaleValue(COL_WIDTH_DEATH);
                float y = Padding;

                Hud.Label(new Rect(killX, y, killWidth, HeaderHeight), LABEL_KILL, headerStyle, Hud.Pink, shadow);
                Hud.Label(new Rect(deathX, y, deathWidth, HeaderHeight), LABEL_DEATH, headerStyle, Hud.Pink, shadow);
                y += HeaderHeight;

                var statsSnapshot = GameSessionManager.Instance.GetStatsSnapshot();
                if (statsSnapshot.ActivePlayers == null || statsSnapshot.ActivePlayers.Count == 0)
                {
                    headerStyle.alignment = TextAnchor.MiddleLeft;
                    Hud.Label(new Rect(Padding, y, windowRect.width - Padding * 2, PlayerRowHeight), LABEL_NO_PLAYERS, headerStyle, Hud.Muted, shadow);
                    headerStyle.alignment = TextAnchor.MiddleCenter;
                }
                else
                {
                    bool sessionActive = statsSnapshot.IsSurvivalActive || statsSnapshot.IsVersusActive;
                    float barInset = PlayerRowHeight * 0.18f;
                    foreach (var playerEntry in statsSnapshot.ActivePlayers)
                    {
                        var playerData = playerEntry.Value;
                        float alpha = sessionActive && !playerData.IsAlive ? DEAD_PLAYER_ALPHA : 1f;

                        Hud.Fill(new Rect(Padding, y + barInset, UIManager.ScaleValue(BASE_BAR_WIDTH), PlayerRowHeight - barInset * 2), Hud.WithAlpha(Hud.Readable(playerData.PlayerColor), alpha));
                        Hud.Label(new Rect(killX, y, killWidth, PlayerRowHeight), playerData.Kills.ToString(), valueStyle, Hud.WithAlpha(playerData.Kills > 0 ? Hud.Text : Hud.Muted, alpha), shadow);
                        Hud.Label(new Rect(deathX, y, deathWidth, PlayerRowHeight), playerData.Deaths.ToString(), valueStyle, Hud.WithAlpha(playerData.Deaths > 0 ? Hud.Pink : Hud.Muted, alpha), shadow);
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
        private void UpdateWindowSize()
        {
            int rows = GameSessionManager.Instance?.GetStatsSnapshot()?.ActivePlayers?.Count ?? 0;
            windowRect.height = Padding * 2 + HeaderHeight + Mathf.Max(rows, 1) * PlayerRowHeight;
            windowRect.width = Padding * 2 + UIManager.ScaleValue(BASE_BAR_WIDTH + BASE_BAR_GAP + COL_WIDTH_KILL + COL_WIDTH_DEATH);
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
