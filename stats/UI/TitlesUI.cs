using UnityEngine;
using SpiderSurge.Common;
using Logger = Silk.Logger;
using System.Collections.Generic;

namespace StatsMod
{
    public class TitlesUI : MonoBehaviour
    {
        #region Grid Layout Parameters
        public int MAX_TITLES = 8;
        public int GRID_COLUMNS = 4;
        public float CARD_WIDTH = 400f;
        public float CARD_HEIGHT = 280f;
        public float CARD_SPACING_X = 26f;
        public float CARD_SPACING_Y = 26f;
        public float CARD_PADDING = 24f;
        public float CARD_BAND_HEIGHT = 6f;
        public float HEADER_HEIGHT = 136f;
        private float HeaderHeight => HEADER_HEIGHT + (RunRecap.Last != null ? 96f : 0f);
        public float HEADER_TO_GRID_SPACING = 30f;
        public float BASE_MAX_WIDTH_PERCENT = 0.94f;
        public float BASE_MAX_HEIGHT_PERCENT = 0.92f;

        public int CAPTION_FONT_SIZE = 18;
        public int HEADER_FONT_SIZE = 64;
        public int TITLE_FONT_SIZE = 32;
        public int DESCRIPTION_FONT_SIZE = 22;
        public int PLAYER_NAME_FONT_SIZE = 24;
        public int EMPTY_FONT_SIZE = 22;

        public float PLAYER_SQUARE_SIZE = 18f;
        public float PORTRAIT_SIZE = 64f;
        public float BORDER_SIZE = 4f;
        public float SQUARE_TO_NAME_SPACING = 10f;
        public float TITLE_TO_DESCRIPTION_SPACING = 10f;
        public float BADGE_TO_TITLE_SPACING = 8f;

        public float REVEAL_SECONDS = 0.4f;
        public float REVEAL_RISE = 28f;
        #endregion

        #region Text Labels
        public string LABEL_HEADER = "TITLES";
        public string LABEL_RUN_OVER = "RUN COMPLETE";
        public string LABEL_MATCH_OVER = "MATCH COMPLETE";
        public string LABEL_LAST_SESSION = "LAST SESSION";
        public string LABEL_NO_TITLES = "No titles yet";
        public string LABEL_NO_TITLES_NOTE = "Titles are handed out at the end of a Survival run with two or more players.";
        #endregion

        #region UI State
        private List<TitleEntry> _cachedTitles;
        private bool isDisplayVisible = false;
        private bool isAnimating = false;
        private int titlesToShow = 0;
        private float animationTimer = 0f;
        private bool hasAnimationPlayed = false;
        private bool unlockSoundPlayed;
        private float[] revealedAt;
        #endregion

        private void DrawPortrait(Rect window, TitleEntry title, float scale)
        {
            Texture portrait = title.Portrait;
            if (portrait != null)
            {
                GUI.DrawTexture(window, portrait, ScaleMode.ScaleToFit, true);
                return;
            }
            Hud.Fill(window, Hud.Ink);
            float squareSize = PLAYER_SQUARE_SIZE * scale;
            Hud.Fill(new Rect(window.x + (window.width - squareSize) * 0.5f, window.y + (window.height - squareSize) * 0.5f, squareSize, squareSize), title.PrimaryColor);
        }

        #region Layout Cache
        private sealed class CardLayout
        {
            public string Name;
            public float BadgeHeight;
            public float TitleHeight;
            public float DescriptionHeight;
            public float FooterHeight;
            public float NameWidth;
        }

        private readonly List<CardLayout> cardLayouts = new List<CardLayout>();
        private int titlesVersion;
        private int layoutTitlesVersion = -1;
        private int layoutWidth = -1;
        private int layoutHeight = -1;
        private int layoutStyleVersion = -1;
        private int layoutFontVersion = -1;
        private int layoutTitleCount = -1;
        private float layoutScale;
        private float layoutCardHeight;
        private float layoutContentHeight;
        #endregion

        #region GUI Styles
        private GUIStyle captionStyle;
        private GUIStyle headerStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle cardDescriptionStyle;
        private GUIStyle cardPlayerNameStyle;
        private GUIStyle emptyStyle;
        private GUIStyle emptyNoteStyle;
        private int fontVersion = -1;
        #endregion

        #region Initialization
        public void Initialize()
        {
            revealedAt = new float[MAX_TITLES];
            ShowAllCards();
            TitlesManager.Instance.OnTitlesUpdated += UpdateTitlesCache;
            UpdateTitlesCache();
            Logger.LogInfo("TitlesUI initialized");
        }

        private void UpdateTitlesCache()
        {
            _cachedTitles = TitlesManager.Instance.CurrentTitles;
            titlesVersion++;
            hasAnimationPlayed = false;
            unlockSoundPlayed = false;
        }
        #endregion

        #region Display Control
        public void ShowDisplay()
        {
            if (!UIManager.CanShowTitles) return;
            isDisplayVisible = true;
            if (!ModConfig.TitlesEnabled)
            {
                StopAnimation();
                return;
            }
            if (TitlesManager.Instance.HasGameEndedTitles && !hasAnimationPlayed)
            {
                StartAnimation();
                hasAnimationPlayed = true;
            }
            else if (TitlesManager.Instance.HasGameEndedTitles)
            {
                titlesToShow = TitlesManager.Instance.TitleCount;
            }
        }

        public void ShowDisplayWithoutAnimation()
        {
            if (!UIManager.CanShowTitles) return;
            isDisplayVisible = true;
            titlesToShow = TitlesManager.Instance.TitleCount;
            if (!isAnimating)
            {
                ShowAllCards();
            }
        }

        public void HideDisplay()
        {
            isDisplayVisible = false;
            StopAnimation();
        }

        public bool IsVisible()
        {
            return isDisplayVisible && UIManager.CanShowTitles;
        }

        public bool IsAnimating()
        {
            return isAnimating;
        }

        public void SkipAnimation()
        {
            if (isAnimating)
            {
                FeedbackSounds.Cancel(FeedbackCue.Title);
                titlesToShow = TitlesManager.Instance.TitleCount;
                isAnimating = false;
                ShowAllCards();
            }
        }
        #endregion

        #region Animation
        private void StartAnimation()
        {
            if (TitlesManager.Instance.TitleCount == 0) return;
            isAnimating = true;
            titlesToShow = 0;
            animationTimer = 0f;
            for (int i = 0; i < revealedAt.Length; i++)
            {
                revealedAt[i] = float.NaN;
            }
        }

        private void StopAnimation()
        {
            isAnimating = false;
            FeedbackSounds.Cancel(FeedbackCue.Title);
        }

        private void ShowAllCards()
        {
            for (int i = 0; i < revealedAt.Length; i++)
            {
                revealedAt[i] = float.NegativeInfinity;
            }
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
                ShowAllCards();
                return;
            }
            int shouldShow = Mathf.FloorToInt(animationTimer / revealDelay);
            if (shouldShow > titlesToShow)
            {
                titlesToShow = Mathf.Min(shouldShow, TitlesManager.Instance.TitleCount);
                if (!UIManager.TitlesBlockedByMenu) FeedbackSounds.Play(FeedbackCue.Title);
            }

            if (titlesToShow >= TitlesManager.Instance.TitleCount)
            {
                isAnimating = false;
            }
        }

        private void PlayNewTitleSound()
        {
            if (unlockSoundPlayed || !TitlesManager.Instance.HasGameEndedTitles || _cachedTitles == null || !_cachedTitles.Exists(title => title.IsNew)) return;
            unlockSoundPlayed = true;
            FeedbackSounds.Play(FeedbackCue.Unlock);
        }

        // 0 when the card is first drawn, 1 once its fade-in is done.
        private float RevealProgress(int index)
        {
            if (float.IsNaN(revealedAt[index]))
            {
                revealedAt[index] = Time.unscaledTime;
            }
            return REVEAL_SECONDS <= 0f ? 1f : Hud.Ease((Time.unscaledTime - revealedAt[index]) / REVEAL_SECONDS);
        }
        #endregion

        #region Style Management
        private void InitializeStyles()
        {
            Hud.EnsureFonts();
            if (fontVersion == Hud.FontVersion && captionStyle != null) return;

            captionStyle = Hud.Style(CAPTION_FONT_SIZE, bold: true, anchor: TextAnchor.UpperCenter);
            headerStyle = Hud.Style(HEADER_FONT_SIZE, bold: true, anchor: TextAnchor.UpperCenter);
            cardTitleStyle = Hud.Style(TITLE_FONT_SIZE, bold: true, anchor: TextAnchor.UpperCenter, wrap: true);
            cardDescriptionStyle = Hud.Style(DESCRIPTION_FONT_SIZE, anchor: TextAnchor.UpperCenter, wrap: true);
            cardPlayerNameStyle = Hud.Style(PLAYER_NAME_FONT_SIZE, bold: true, wrap: true);
            emptyStyle = Hud.Style(EMPTY_FONT_SIZE, bold: true, anchor: TextAnchor.MiddleCenter);
            emptyNoteStyle = Hud.Style(DESCRIPTION_FONT_SIZE, anchor: TextAnchor.MiddleCenter, wrap: true);

            fontVersion = Hud.FontVersion;
        }

        private void ApplyScale(float scale)
        {
            Hud.Resize(captionStyle, CAPTION_FONT_SIZE, scale);
            Hud.Resize(headerStyle, HEADER_FONT_SIZE, scale);
            Hud.Resize(cardTitleStyle, TITLE_FONT_SIZE, scale);
            Hud.Resize(cardDescriptionStyle, DESCRIPTION_FONT_SIZE, scale);
            Hud.Resize(cardPlayerNameStyle, PLAYER_NAME_FONT_SIZE, scale);
            Hud.Resize(emptyStyle, EMPTY_FONT_SIZE, scale);
            Hud.Resize(emptyNoteStyle, DESCRIPTION_FONT_SIZE, scale);
        }
        #endregion

        #region GUI Drawing
        private void OnGUI()
        {
            if (!isDisplayVisible || !UIManager.CanShowTitles || Event.current.type != EventType.Repaint || UIManager.TitlesBlockedByMenu) return;
            PlayNewTitleSound();

            InitializeStyles();

            Color originalColor = GUI.color;
            GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);

            Hud.Fill(new Rect(0f, 0f, Screen.width, Screen.height), Hud.Dimmer);

            bool hasTitles = ModConfig.TitlesEnabled && TitlesManager.Instance.HasGameEndedTitles;
            if (_cachedTitles == null) _cachedTitles = TitlesManager.Instance.CurrentTitles;
            int titleCount = hasTitles ? Mathf.Min(_cachedTitles.Count, MAX_TITLES) : 0;
            int rows = titleCount == 0 ? 0 : (titleCount + GRID_COLUMNS - 1) / GRID_COLUMNS;

            UpdateLayout(titleCount, rows);
            float scale = layoutScale;
            float cardHeight = layoutCardHeight;
            float contentHeight = layoutContentHeight;

            float top = (Screen.height - contentHeight) * 0.5f;
            string caption = !hasTitles && RunRecap.Last == null ? ""
                : GameSessionManager.Instance.IsActive ? LABEL_LAST_SESSION
                : GameSessionManager.Instance.LastGameMode == GameMode.Versus ? LABEL_MATCH_OVER : LABEL_RUN_OVER;
            DrawHeader(new Rect(0f, top, Screen.width, HeaderHeight * scale), caption, scale);

            float gridTop = top + (HeaderHeight + HEADER_TO_GRID_SPACING) * scale;
            if (titleCount == 0)
            {
                float lineHeight = EMPTY_FONT_SIZE * 2f * scale;
                Hud.Label(new Rect(0f, gridTop, Screen.width, lineHeight), RunRecap.Last != null ? "Another run. Another build." : LABEL_NO_TITLES, emptyStyle, Hud.Text);
                Hud.Label(new Rect(Screen.width * 0.15f, gridTop + lineHeight, Screen.width * 0.7f, lineHeight * 2f), RunRecap.Last != null ? "Switch to the scoreboard for full stats. Revisit this run in Match history." : LABEL_NO_TITLES_NOTE, emptyNoteStyle, Hud.Muted);
            }
            else
            {
                DrawTitles(gridTop, Mathf.Min(titlesToShow, titleCount), cardHeight, scale);
            }

            GUI.color = originalColor;
        }

        // Text is measured only when the titles, the screen or the scale change, not on every repaint
        private void UpdateLayout(int titleCount, int rows)
        {
            float uiScale = UIManager.UIScaleFactor;
            if (layoutTitlesVersion == titlesVersion && layoutTitleCount == titleCount && layoutWidth == Screen.width && layoutHeight == Screen.height &&
                layoutStyleVersion == UIManager.StyleVersion && layoutFontVersion == Hud.FontVersion)
            {
                return;
            }

            float baseGridWidth = GRID_COLUMNS * CARD_WIDTH + (GRID_COLUMNS - 1) * CARD_SPACING_X;
            float scale = Hud.FitScale(uiScale, baseGridWidth, HeaderHeight, BASE_MAX_WIDTH_PERCENT, BASE_MAX_HEIGHT_PERCENT);
            float cardHeight = 0f;
            float contentHeight = 0f;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                ApplyScale(scale);
                cardHeight = MeasureCards(titleCount, scale);
                float gridHeight = rows == 0 ? 120f * scale : rows * cardHeight + (rows - 1) * CARD_SPACING_Y * scale;
                contentHeight = (HeaderHeight + HEADER_TO_GRID_SPACING) * scale + gridHeight;
                float fit = Screen.height * BASE_MAX_HEIGHT_PERCENT / contentHeight;
                if (fit >= 1f || attempt == 4) break;
                scale *= fit * 0.99f;
            }

            layoutScale = scale;
            layoutCardHeight = cardHeight;
            layoutContentHeight = contentHeight;
            layoutTitlesVersion = titlesVersion;
            layoutTitleCount = titleCount;
            layoutWidth = Screen.width;
            layoutHeight = Screen.height;
            layoutStyleVersion = UIManager.StyleVersion;
            layoutFontVersion = Hud.FontVersion;
        }

        private void DrawHeader(Rect header, string caption, float scale)
        {
            float captionHeight = CAPTION_FONT_SIZE * 2.2f * scale;
            Hud.Label(new Rect(header.x, header.y, header.width, captionHeight), caption, captionStyle, Hud.Pink);
            var recap = RunRecap.Last;
            Hud.Label(new Rect(header.x, header.y + captionHeight, header.width, recap != null ? 80f * scale : header.height - captionHeight), recap != null ? "RUN RESULTS" : LABEL_HEADER, headerStyle, Hud.Text);
            if (recap != null)
            {
                float left = Screen.width * .08f;
                float width = Screen.width * .84f;
                Hud.Label(new Rect(left, header.y + 122f * scale, width, 30f * scale), recap.Build, emptyNoteStyle, Hud.Text);
                Hud.Label(new Rect(left, header.y + 154f * scale, width, 30f * scale), recap.Summary, captionStyle, Hud.Warm);
                string unlocks = recap.Achievements;
                if (unlocks.Length > 100) unlocks = unlocks.Substring(0, 97) + "…";
                string titles = TitlesManager.Instance.LastNewlyUnlocked > 0 ? $"{TitlesManager.Instance.LastNewlyUnlocked} new titles" : "";
                string collection = (unlocks.Length > 0 ? "New team achievements: " + unlocks : "") + (titles.Length > 0 ? (unlocks.Length > 0 ? "  ·  " : "") + titles : "");
                Hud.Label(new Rect(left, header.y + 190f * scale, width, 30f * scale), collection, captionStyle, Hud.Pink);
            }

            float lineWidth = 64f * scale;
            Hud.Fill(new Rect((Screen.width - lineWidth) * 0.5f, header.yMax - 4f * scale, lineWidth, 4f * scale), Hud.Pink);
        }

        // Measure every card before revealing any so wrapped text cannot move the grid mid-animation.
        private float MeasureCards(int count, float scale)
        {
            float height = CARD_HEIGHT * scale;
            float innerWidth = (CARD_WIDTH - CARD_PADDING * 2f) * scale;
            float chipSize = (PORTRAIT_SIZE + BORDER_SIZE * 2f) * scale;
            float maxNameWidth = innerWidth - chipSize - SQUARE_TO_NAME_SPACING * scale;
            float badgeHeight = 0f;
            for (int index = 0; index < count; index++)
            {
                if (!_cachedTitles[index].IsNew) continue;
                badgeHeight = captionStyle.CalcHeight(new GUIContent("NEW"), innerWidth);
                break;
            }
            while (cardLayouts.Count < count)
            {
                cardLayouts.Add(new CardLayout());
            }

            for (int index = 0; index < count; index++)
            {
                TitleEntry title = _cachedTitles[index];
                CardLayout layout = cardLayouts[index];
                string playerName = title.PlayerName ?? "";
                layout.Name = (title.TitleName ?? "").ToUpperInvariant();
                layout.BadgeHeight = badgeHeight;
                layout.TitleHeight = cardTitleStyle.CalcHeight(new GUIContent(layout.Name), innerWidth);
                layout.DescriptionHeight = cardDescriptionStyle.CalcHeight(new GUIContent(title.Description ?? ""), innerWidth);
                layout.FooterHeight = Mathf.Max(chipSize, PLAYER_NAME_FONT_SIZE * 1.5f * scale, cardPlayerNameStyle.CalcHeight(new GUIContent(playerName), maxNameWidth));
                layout.NameWidth = Mathf.Min(Hud.Width(cardPlayerNameStyle, playerName), maxNameWidth);
                height = Mathf.Max(height, layout.TitleHeight + layout.DescriptionHeight + layout.FooterHeight
                    + (badgeHeight > 0f ? badgeHeight + BADGE_TO_TITLE_SPACING * scale : 0f)
                    + (CARD_BAND_HEIGHT + CARD_PADDING * 3f + TITLE_TO_DESCRIPTION_SPACING) * scale);
            }
            return height;
        }

        private void DrawTitles(float gridTop, int count, float cardHeight, float scale)
        {
            var titles = _cachedTitles;
            float cardWidth = CARD_WIDTH * scale;
            float spacingX = CARD_SPACING_X * scale;
            float spacingY = CARD_SPACING_Y * scale;
            int total = Mathf.Min(titles.Count, MAX_TITLES);

            for (int index = 0; index < count; index++)
            {
                int row = index / GRID_COLUMNS;
                int col = index % GRID_COLUMNS;
                int inRow = Mathf.Min(GRID_COLUMNS, total - row * GRID_COLUMNS);
                float rowWidth = inRow * cardWidth + (inRow - 1) * spacingX;
                float x = (Screen.width - rowWidth) * 0.5f + col * (cardWidth + spacingX);
                float y = gridTop + row * (cardHeight + spacingY);

                float reveal = RevealProgress(index);
                Rect card = new Rect(x, y + (1f - reveal) * REVEAL_RISE * scale, cardWidth, cardHeight);

                Color old = GUI.color;
                GUI.color = new Color(old.r, old.g, old.b, old.a * reveal);
                DrawTitleCard(titles[index], cardLayouts[index], card, scale);
                GUI.color = old;
            }
        }

        private void DrawTitleCard(TitleEntry title, CardLayout layout, Rect card, float scale)
        {
            Color playerColor = Hud.Readable(title.PrimaryColor);
            Hud.Fill(card, Hud.Card);
            Hud.Fill(new Rect(card.x, card.y, card.width, CARD_BAND_HEIGHT * scale), playerColor);

            float padding = CARD_PADDING * scale;
            float innerWidth = card.width - padding * 2;
            float y = card.y + CARD_BAND_HEIGHT * scale + padding;

            if (layout.BadgeHeight > 0f)
            {
                if (title.IsNew)
                    Hud.Label(new Rect(card.x + padding, y, innerWidth, layout.BadgeHeight), "NEW", captionStyle, Hud.Good);
                y += layout.BadgeHeight + BADGE_TO_TITLE_SPACING * scale;
            }

            Hud.Label(new Rect(card.x + padding, y, innerWidth, layout.TitleHeight), layout.Name, cardTitleStyle, Hud.Text);
            y += layout.TitleHeight + TITLE_TO_DESCRIPTION_SPACING * scale;

            Hud.Label(new Rect(card.x + padding, y, innerWidth, layout.DescriptionHeight), title.Description ?? "", cardDescriptionStyle, Hud.Muted);

            float portraitSize = PORTRAIT_SIZE * scale;
            float borderSize = BORDER_SIZE * scale;
            float chipSize = portraitSize + borderSize * 2;
            string playerName = title.PlayerName ?? "";
            float nameWidth = layout.NameWidth;
            float groupWidth = chipSize + SQUARE_TO_NAME_SPACING * scale + nameWidth;
            float footerHeight = layout.FooterHeight;
            float footerY = card.yMax - padding - footerHeight;
            float groupX = card.x + (card.width - groupWidth) * 0.5f;

            Hud.Fill(new Rect(card.x + padding, footerY - padding * 0.6f, innerWidth, Mathf.Max(1f, scale)), Hud.Line);
            Rect chip = new Rect(groupX, footerY + (footerHeight - chipSize) * 0.5f, chipSize, chipSize);
            Hud.Fill(chip, title.SecondaryColor);
            DrawPortrait(new Rect(chip.x + borderSize, chip.y + borderSize, portraitSize, portraitSize), title, scale);
            Hud.Label(new Rect(chip.xMax + SQUARE_TO_NAME_SPACING * scale, footerY, nameWidth, footerHeight), playerName, cardPlayerNameStyle, playerColor);
        }
        #endregion

        #region Cleanup
        private void OnDestroy()
        {
            TitlesManager.Instance.OnTitlesUpdated -= UpdateTitlesCache;
        }
        #endregion
    }
}
