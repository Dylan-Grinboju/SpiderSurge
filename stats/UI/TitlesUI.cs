using UnityEngine;
using Logger = Silk.Logger;
using System.Collections.Generic;

namespace StatsMod
{
    public class TitlesUI : MonoBehaviour
    {
        #region Grid Layout Parameters
        public int MAX_TITLES = 8;
        public int GRID_COLUMNS = 4;
        public int GRID_ROWS = 2;
        public float CARD_WIDTH = 350f;
        public float CARD_HEIGHT = 300f;
        public float CARD_SPACING_X = 50f;
        public float CARD_SPACING_Y = 100f;
        public float GRID_MARGIN_X = 0f;
        public float GRID_MARGIN_Y = 0f;
        public float HEADER_HEIGHT = 100f;

        // Font sizes for card elements
        public int TITLE_FONT_SIZE = 32;
        public int DESCRIPTION_FONT_SIZE = 22;
        public int PLAYER_NAME_FONT_SIZE = 24;

        // Card element sizing
        public float PLAYER_SQUARE_SIZE = 30f;
        public float BORDER_SIZE = 5f;

        // Card internal spacing
        public float TITLE_TO_DESCRIPTION_SPACING = 10f;
        public float DESCRIPTION_TO_SQUARE_SPACING = 5f;
        public float SQUARE_TO_NAME_SPACING = 8f;

        // Banner constraints
        public float BASE_MARGIN_PERCENT = 0.05f;
        public float BASE_MAX_HEIGHT_PERCENT = 0.95f;
        #endregion

        #region UI State
        private List<TitleEntry> _cachedTitles;
        private bool isDisplayVisible = false;
        private bool isAnimating = false;
        private int titlesToShow = 0;
        private float animationTimer = 0f;
        private bool hasAnimationPlayed = false;
        #endregion

        #region GUI Styles
        private GUIStyle backgroundStyle;
        private GUIStyle headerStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle cardDescriptionStyle;
        private GUIStyle cardPlayerNameStyle;
        private GUIStyle dynamicPlayerNameStyle; // Reused for dynamic player colors
        private GUIStyle noTitlesStyle;

        private bool stylesInitialized = false;
        private Texture2D whiteTexture;
        #endregion

        #region Initialization
        public void Initialize()
        {
            CreateTextures();
            TitlesManager.Instance.OnTitlesUpdated += UpdateTitlesCache;
            UpdateTitlesCache();
            Logger.LogInfo("TitlesUI initialized");
        }

        private void UpdateTitlesCache()
        {
            _cachedTitles = TitlesManager.Instance.CurrentTitles;
            hasAnimationPlayed = false;
        }

        private void CreateTextures()
        {
            whiteTexture = new Texture2D(1, 1);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
            whiteTexture.hideFlags = HideFlags.HideAndDontSave;
        }
        #endregion

        #region Display Control
        public void ShowDisplay()
        {
            if (!ModConfig.TitlesEnabled) return;
            isDisplayVisible = true;
            if (TitlesManager.Instance.HasGameEndedTitles && TitlesManager.Instance.TitleCount > 0 && !hasAnimationPlayed)
            {
                StartAnimation();
                hasAnimationPlayed = true;
            }
            else if (TitlesManager.Instance.HasGameEndedTitles && TitlesManager.Instance.TitleCount > 0)
            {
                titlesToShow = TitlesManager.Instance.TitleCount;
            }
        }

        public void ShowDisplayWithoutAnimation()
        {
            if (!ModConfig.TitlesEnabled) return;
            isDisplayVisible = true;
            titlesToShow = TitlesManager.Instance.TitleCount;
        }

        public void HideDisplay()
        {
            isDisplayVisible = false;
            StopAnimation();
        }

        public void ToggleDisplay()
        {
            if (isDisplayVisible)
                HideDisplay();
            else
                ShowDisplay();
        }

        public bool IsVisible()
        {
            return isDisplayVisible && ModConfig.TitlesEnabled;
        }

        public bool IsAnimating()
        {
            return isAnimating;
        }

        public void SkipAnimation()
        {
            if (isAnimating)
            {
                titlesToShow = TitlesManager.Instance.TitleCount;
                isAnimating = false;
            }
        }

        public void ClearTitlesForNewGame()
        {
            TitlesManager.Instance.ClearTitles();
            titlesToShow = 0;
            isAnimating = false;
            hasAnimationPlayed = false;
        }

        public void ResetRevealAnimationForCurrentTitles()
        {
            if (!TitlesManager.Instance.HasGameEndedTitles || TitlesManager.Instance.TitleCount == 0)
            {
                return;
            }

            isAnimating = false;
            hasAnimationPlayed = false;
            titlesToShow = 0;
            animationTimer = 0f;
        }
        #endregion

        #region Animation
        private void StartAnimation()
        {
            if (TitlesManager.Instance.TitleCount == 0) return;
            isAnimating = true;
            titlesToShow = 0;
            animationTimer = 0f;
        }

        private void StopAnimation()
        {
            isAnimating = false;
        }

        private void Update()
        {
            if (!isAnimating || !isDisplayVisible) return;

            animationTimer += Time.deltaTime;
            float revealDelay = ModConfig.TitlesRevealDelaySeconds;

            if (revealDelay <= 0)
            {
                titlesToShow = TitlesManager.Instance.TitleCount;
                isAnimating = false;
                return;
            }
            int shouldShow = Mathf.FloorToInt(animationTimer / revealDelay);
            if (shouldShow > titlesToShow)
            {
                titlesToShow = Mathf.Min(shouldShow, TitlesManager.Instance.TitleCount);
            }

            if (titlesToShow >= TitlesManager.Instance.TitleCount)
            {
                isAnimating = false;
            }
        }
        #endregion

        #region Public Methods

        public List<TitleEntry> GetCurrentTitles()
        {
            return TitlesManager.Instance.CurrentTitles;
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

            headerStyle = UIManager.Instance.CreateHeaderStyle(64);
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.normal.textColor = UIManager.Blue;

            cardTitleStyle = UIManager.Instance.CreateHeaderStyle(TITLE_FONT_SIZE);
            cardTitleStyle.alignment = TextAnchor.UpperCenter;
            cardTitleStyle.normal.textColor = UIManager.Orange;
            cardTitleStyle.wordWrap = true;

            cardDescriptionStyle = UIManager.Instance.CreateValueStyle(DESCRIPTION_FONT_SIZE);
            cardDescriptionStyle.alignment = TextAnchor.UpperCenter;
            cardDescriptionStyle.normal.textColor = UIManager.Gray;
            cardDescriptionStyle.wordWrap = true;

            cardPlayerNameStyle = UIManager.Instance.CreateValueStyle(PLAYER_NAME_FONT_SIZE);
            cardPlayerNameStyle.alignment = TextAnchor.MiddleCenter;

            // Create reusable style for dynamic player colors
            dynamicPlayerNameStyle = new GUIStyle(cardPlayerNameStyle);
            noTitlesStyle = new GUIStyle(cardPlayerNameStyle) { normal = { textColor = UIManager.Gray } };


            stylesInitialized = true;
        }
        #endregion

        #region GUI Drawing
        private void OnGUI()
        {
            if (!isDisplayVisible || !ModConfig.TitlesEnabled) return;

            InitializeStyles();

            Color originalColor = GUI.color;
            float opacity = 1.0f;
            GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, opacity);

            float marginX = Screen.width * BASE_MARGIN_PERCENT;
            float contentHeight = CalculateContentHeight();
            float backgroundHeight = Mathf.Min(contentHeight, Screen.height * BASE_MAX_HEIGHT_PERCENT);
            float backgroundY = (Screen.height - backgroundHeight) * 0.5f;

            Rect backgroundRect = new Rect(marginX, backgroundY, Screen.width - (marginX * 2), backgroundHeight);
            GUI.Box(backgroundRect, "", backgroundStyle);

            GUILayout.BeginArea(backgroundRect);

            bool showingPreviousSessionTitles = GameSessionManager.Instance.IsActive &&
                                               TitlesManager.Instance.HasGameEndedTitles &&
                                               TitlesManager.Instance.TitleCount > 0;
            string headerText = showingPreviousSessionTitles ? "Titles - Last Session" : "Titles";
            GUILayout.Label(headerText, headerStyle);
            GUILayout.Space(UIManager.ScaleValue(40f));

            if (!TitlesManager.Instance.HasGameEndedTitles || TitlesManager.Instance.TitleCount == 0)
            {
                GUILayout.Label("No titles to display", noTitlesStyle);
            }
            else
            {
                DrawTitles(backgroundRect.width);
            }

            GUILayout.EndArea();

            GUI.color = originalColor;
        }

        private float CalculateContentHeight()
        {
            float headerHeight = UIManager.ScaleValue(HEADER_HEIGHT);

            if (!TitlesManager.Instance.HasGameEndedTitles || TitlesManager.Instance.TitleCount == 0)
            {
                return headerHeight + UIManager.ScaleValue(100f);
            }

            float gridHeight = UIManager.ScaleValue(GRID_ROWS * CARD_HEIGHT + (GRID_ROWS - 1) * CARD_SPACING_Y);
            return headerHeight + gridHeight + UIManager.ScaleValue(40f);
        }

        private void DrawTitles(float contentWidth)
        {
            if (_cachedTitles == null) _cachedTitles = TitlesManager.Instance.CurrentTitles;
            var titles = _cachedTitles;
            int count = Mathf.Min(titlesToShow, titles.Count, MAX_TITLES);
            if (count == 0)
            {
                return;
            }


            float cardWidth = UIManager.ScaleValue(CARD_WIDTH);
            float cardHeight = UIManager.ScaleValue(CARD_HEIGHT);
            float spacingX = UIManager.ScaleValue(CARD_SPACING_X);
            float spacingY = UIManager.ScaleValue(CARD_SPACING_Y);
            float gridMarginX = UIManager.ScaleValue(GRID_MARGIN_X);
            float gridMarginY = UIManager.ScaleValue(GRID_MARGIN_Y);

            float totalGridWidth = (GRID_COLUMNS * cardWidth) + ((GRID_COLUMNS - 1) * spacingX);
            float availableWidth = contentWidth - (gridMarginX * 2);
            float startX = gridMarginX + ((availableWidth - totalGridWidth) / 2f);

            GUILayout.Space(gridMarginY);

            int titleIndex = 0;

            for (int row = 0; row < GRID_ROWS; row++)
            {
                if (titleIndex >= count) break;

                GUILayout.BeginHorizontal();
                GUILayout.Space(startX);

                for (int col = 0; col < GRID_COLUMNS; col++)
                {
                    if (titleIndex >= count) break;

                    var title = titles[titleIndex];
                    DrawTitleCard(title, cardWidth, cardHeight);
                    titleIndex++;

                    if (col < GRID_COLUMNS - 1 && titleIndex < count)
                        GUILayout.Space(spacingX);
                }

                GUILayout.EndHorizontal();

                if (row < GRID_ROWS - 1 && titleIndex < count)
                    GUILayout.Space(spacingY);
            }
        }

        private void DrawTitleCard(TitleEntry title, float cardWidth, float cardHeight)
        {
            float squareSize = UIManager.ScaleValue(PLAYER_SQUARE_SIZE);
            float borderSize = UIManager.ScaleValue(BORDER_SIZE);
            float totalSquareSize = squareSize + (borderSize * 2);

            GUILayout.BeginVertical(GUILayout.Width(cardWidth), GUILayout.Height(cardHeight));

            GUILayout.Label(title.TitleName, cardTitleStyle, GUILayout.Width(cardWidth));

            GUILayout.Space(UIManager.ScaleValue(TITLE_TO_DESCRIPTION_SPACING));

            GUILayout.Label(title.Description ?? "", cardDescriptionStyle, GUILayout.Width(cardWidth));

            GUILayout.Space(UIManager.ScaleValue(DESCRIPTION_TO_SQUARE_SPACING));

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect outerRect = GUILayoutUtility.GetRect(totalSquareSize, totalSquareSize);
            DrawPlayerSquare(outerRect, title.PrimaryColor, title.SecondaryColor, squareSize, borderSize);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(UIManager.ScaleValue(SQUARE_TO_NAME_SPACING));

            // Reuse cached style, just update the color
            dynamicPlayerNameStyle.normal.textColor = title.PrimaryColor;
            GUILayout.Label(title.PlayerName, dynamicPlayerNameStyle, GUILayout.Width(cardWidth));

            GUILayout.EndVertical();
        }

        private void DrawPlayerSquare(Rect outerRect, Color primaryColor, Color secondaryColor, float squareSize, float borderSize)
        {
            Color originalColor = GUI.color;
            GUI.color = secondaryColor;
            GUI.DrawTexture(outerRect, whiteTexture);

            Rect innerRect = new Rect(
                outerRect.x + borderSize,
                outerRect.y + borderSize,
                squareSize,
                squareSize
            );
            GUI.color = primaryColor;
            GUI.DrawTexture(innerRect, whiteTexture);

            GUI.color = originalColor;
        }
        #endregion

        #region Cleanup
        private void OnDestroy()
        {
            TitlesManager.Instance.OnTitlesUpdated -= UpdateTitlesCache;
            if (whiteTexture != null) Destroy(whiteTexture);
        }
        #endregion
    }
}
