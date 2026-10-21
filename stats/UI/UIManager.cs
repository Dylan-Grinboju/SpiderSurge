using UnityEngine;
using Logger = Silk.Logger;
using UnityEngine.InputSystem;
using System.Collections;
using SpiderSurge.Common;
using Doozy.Engine.UI;

namespace StatsMod
{
    public enum UIState
    {
        Off,
        SmallUI,
        BigUI,
        TitlesUI
    }

    public class UIManager : MonoBehaviour
    {
        private void Awake()
        {
            Settings.Changed += OnSettingsChanged;
        }

        private static void OnSettingsChanged(string key)
        {
            if (Settings.Affects(key, ModConfig.AutoScaleKey) || Settings.Affects(key, ModConfig.UIScaleKey))
            {
                _scalingInitialized = false;
                StyleVersion++;
            }
        }

        #region Singleton
        public static UIManager Instance { get; private set; }
        public static bool CanShowTitles
        {
            get
            {
                var session = GameSessionManager.Instance;
                GameMode mode = session.CurrentGameMode != GameMode.None ? session.CurrentGameMode : session.LastGameMode;
                return ModConfig.TitlesEnabled && mode != GameMode.Versus;
            }
        }
        internal static bool TitlesBlockedByMenu
        {
            get
            {
                if (SettingsPage.IsOpen) return true;
                var perkChoice = SurvivalModeHud.instance != null ? SurvivalModeHud.instance.perkChoiceView : null;
                if (perkChoice != null && perkChoice.gameObject.activeInHierarchy && (perkChoice.IsVisible || perkChoice.IsShowing)) return true;
                if (!GameMenus.Open) return false;
                HudController hud = HudController.instance;
                if (Instance == null || !Instance.restartTitlesPause || hud == null || !hud.paused ||
                    hud.pauseView == null || !hud.pauseView.gameObject.activeInHierarchy ||
                    !hud.pauseView.IsVisible || hud.pauseView.IsHiding) return true;
                if (hud.optionsView != null && (hud.optionsView.IsVisible || hud.optionsView.IsShowing)) return true;
                UIView mods = ModsMenu.instance != null ? ModsMenu.instance.GetComponent<UIView>() : null;
                return (mods != null && (mods.IsVisible || mods.IsShowing)) || UIPopupManager.CurrentVisibleQueuePopup != null;
            }
        }
        #endregion

        #region UI Components
        private SmallUI smallUI;
        private BigUI bigUI;
        private TitlesUI titlesUI;
        #endregion

        #region Controller Cycling State
        private UIState currentUIState = UIState.Off;
        private Coroutine delayedPauseCoroutine;
        private bool restartTitlesPause;
        private readonly ControlPoller smallWindowKey = new ControlPoller();
        private readonly ControlPoller bigWindowKey = new ControlPoller();
        private readonly ControlPoller titlesKey = new ControlPoller();
        private readonly AnyPadPoller switchButton = new AnyPadPoller();
        #endregion

        #region UI Scaling
        private static float _uiScaleFactor = 1.0f;
        private static bool _scalingInitialized = false;
        private static bool _scalingLogged;
        private static int _scaledWidth;
        private static int _scaledHeight;
        private static Vector2Int _resizedTo;
        private static float _resizedAt;
        private const float ResizeSettleSeconds = 0.25f;
        public static int StyleVersion { get; private set; }

        // A window being dragged to a new size changes it every frame, so the windows rescale once it has held still for a moment
        public static float UIScaleFactor
        {
            get
            {
                if (!_scalingInitialized)
                {
                    InitializeScaling();
                }
                else if (Screen.width != _scaledWidth || Screen.height != _scaledHeight)
                {
                    var size = new Vector2Int(Screen.width, Screen.height);
                    if (size != _resizedTo)
                    {
                        _resizedTo = size;
                        _resizedAt = Time.unscaledTime;
                    }
                    else if (Time.unscaledTime - _resizedAt >= ResizeSettleSeconds)
                    {
                        _scalingLogged = false;
                        InitializeScaling();
                        StyleVersion++;
                    }
                }
                return _uiScaleFactor;
            }
        }

        private static void InitializeScaling()
        {
            _scaledWidth = Screen.width;
            _scaledHeight = Screen.height;

            if (ModConfig.AutoScale)
            {
                // Base resolution: 1920x1080
                float baseWidth = 1920f;
                float baseHeight = 1080f;

                // Get current screen resolution
                float currentWidth = Screen.width;
                float currentHeight = Screen.height;

                // Calculate scale based on the smaller dimension to maintain readability
                float widthScale = currentWidth / baseWidth;
                float heightScale = currentHeight / baseHeight;
                float autoScale = Mathf.Min(widthScale, heightScale);

                // Apply manual scale multiplier
                _uiScaleFactor = autoScale * ModConfig.UIScale;

                // Clamp to reasonable values
                _uiScaleFactor = Mathf.Clamp(_uiScaleFactor, 0.5f, 3.0f);

                if (!_scalingLogged) Logger.LogInfo($"UI Auto-scaling initialized: Screen {currentWidth}x{currentHeight}, Scale factor: {_uiScaleFactor:F2}");
            }
            else
            {
                _uiScaleFactor = ModConfig.UIScale;
                if (!_scalingLogged) Logger.LogInfo($"UI Manual scaling: Scale factor: {_uiScaleFactor:F2}");
            }

            _scalingInitialized = true;
            _scalingLogged = true;
        }

        public static float ScaleValue(float value)
        {
            return value * UIScaleFactor;
        }

        public static int ScaleFont(int baseFontSize)
        {
            return Mathf.RoundToInt(baseFontSize * UIScaleFactor);
        }
        #endregion

        #region Initialization
        public static void Initialize()
        {
            if (Instance == null)
            {
                GameObject uiManagerObj = new GameObject("UIManager");
                Instance = uiManagerObj.AddComponent<UIManager>();
                DontDestroyOnLoad(uiManagerObj);

                Instance.InitializeComponents();
                Logger.LogInfo("UIManager initialized");
            }
        }

        private void InitializeComponents()
        {
            GameObject smallUIObj = new GameObject("SmallUI");
            smallUIObj.transform.SetParent(transform);
            smallUI = smallUIObj.AddComponent<SmallUI>();
            smallUI.Initialize();

            GameObject bigUIObj = new GameObject("BigUI");
            bigUIObj.transform.SetParent(transform);
            bigUI = bigUIObj.AddComponent<BigUI>();

            GameObject titlesUIObj = new GameObject("TitlesUI");
            titlesUIObj.transform.SetParent(transform);
            titlesUI = titlesUIObj.AddComponent<TitlesUI>();
            titlesUI.Initialize();
            titlesUIObj.AddComponent<SpiderPortraits>();

        }
        #endregion

        #region Input Handling
        private void Update()
        {
            if (HudController.instance == null || !HudController.instance.paused) restartTitlesPause = false;
            HandleKeyboardInput();
            HandleGamepadInput();
        }

        private void HandleKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (bigUI != null && bigUI.HandlePresetInput(keyboard)) return;
            if (smallWindowKey.WasPressed(Controls.Get(Controls.SmallWindow), keyboard))
            {
                HandleSmallWindowKey();
            }

            if (bigWindowKey.WasPressed(Controls.Get(Controls.BigWindow), keyboard))
            {
                HandleBigWindowKey();
            }

            if (titlesKey.WasPressed(Controls.Get(Controls.TitlesWindow), keyboard))
            {
                HandleTitlesKey();
            }
        }

        private void HandleGamepadInput()
        {
            if (Gamepad.all.Count == 0 || GameMenus.Open) return;

            if (bigUI != null && bigUI.HandlePresetPads()) return;
            if (switchButton.WasPressed(ModConfig.SwitchWindowsBinding))
            {
                CycleUIState();
            }
        }

        private void CycleUIState()
        {
            if (GameSessionManager.Instance.IsActive)
            {
                CycleUIStateInGame();
                return;
            }

            switch (currentUIState)
            {
                case UIState.Off:
                    currentUIState = UIState.SmallUI;
                    HideBigUI();
                    HideTitlesUI();
                    ShowSmallUI();
                    break;
                case UIState.SmallUI:
                    currentUIState = UIState.BigUI;
                    HideSmallUI();
                    HideTitlesUI();
                    ShowBigUI();
                    break;
                case UIState.BigUI:
                    HideSmallUI();
                    HideBigUI();
                    if (CanShowTitles)
                    {
                        currentUIState = UIState.TitlesUI;
                        ShowTitlesUIWithoutAnimation();
                    }
                    else
                    {
                        currentUIState = UIState.Off;
                        HideTitlesUI();
                    }
                    break;
                case UIState.TitlesUI:
                    currentUIState = UIState.Off;
                    HideSmallUI();
                    HideBigUI();
                    HideTitlesUI();
                    break;
            }
        }

        private void CycleUIStateInGame()
        {
            switch (currentUIState)
            {
                case UIState.Off:
                    currentUIState = UIState.SmallUI;
                    HideBigUI();
                    HideTitlesUI();
                    ShowSmallUI();
                    break;
                case UIState.SmallUI:
                    currentUIState = UIState.Off;
                    HideSmallUI();
                    HideBigUI();
                    HideTitlesUI();
                    break;
                default:
                    currentUIState = UIState.SmallUI;
                    HideBigUI();
                    HideTitlesUI();
                    ShowSmallUI();
                    break;
            }
        }

        private void HandleSmallWindowKey()
        {
            bool isSmallVisible = smallUI != null && smallUI.IsVisible();
            HideBigUI();
            HideTitlesUI();
            if (isSmallVisible)
            {
                HideSmallUI();
                currentUIState = UIState.Off;
            }
            else
            {
                ShowSmallUI();
                currentUIState = UIState.SmallUI;
            }
        }

        private void HandleBigWindowKey()
        {
            bool isBigVisible = bigUI != null && bigUI.IsVisible();
            HideSmallUI();
            HideTitlesUI();
            if (isBigVisible)
            {
                HideBigUI();
                currentUIState = UIState.Off;
            }
            else
            {
                ShowBigUI();
                currentUIState = UIState.BigUI;
            }
        }

        private void HandleTitlesKey()
        {
            if (!CanShowTitles || TitlesBlockedByMenu) return;
            // If currently animating, skip the animation
            if (titlesUI != null && titlesUI.IsAnimating())
            {
                titlesUI.SkipAnimation();
                return;
            }

            bool isTitlesVisible = titlesUI != null && titlesUI.IsVisible();
            HideSmallUI();
            HideBigUI();
            if (isTitlesVisible)
            {
                HideTitlesUI();
                currentUIState = UIState.Off;
            }
            else
            {
                ShowTitlesUIWithoutAnimation();
                currentUIState = UIState.TitlesUI;
            }
        }

        public void ToggleSmallUI()
        {
            if (smallUI != null)
            {
                smallUI.ToggleDisplay();
            }
        }

        public void ToggleBigUI()
        {
            if (bigUI != null)
            {
                bigUI.ToggleDisplay();
            }
        }

        public void ShowSmallUI()
        {
            if (smallUI != null)
            {
                smallUI.ShowDisplay();
            }
        }

        public void HideSmallUI()
        {
            if (smallUI != null)
            {
                smallUI.HideDisplay();
            }
        }

        public void ShowBigUI()
        {
            if (bigUI != null)
            {
                bigUI.ShowDisplay();
            }
        }

        public void HideBigUI()
        {
            if (bigUI != null)
            {
                bigUI.HideDisplay();
            }
        }

        public void ShowTitlesUI()
        {
            if (titlesUI != null)
            {
                titlesUI.ShowDisplay();
            }
        }

        public void ShowTitlesUIWithoutAnimation()
        {
            if (titlesUI != null)
            {
                titlesUI.ShowDisplayWithoutAnimation();
            }
        }

        public void HideTitlesUI()
        {
            restartTitlesPause = false;
            if (titlesUI != null)
            {
                titlesUI.HideDisplay();
            }
        }

        public TitlesUI GetTitlesUI()
        {
            return titlesUI;
        }
        #endregion

        #region Cleanup
        private void OnDestroy()
        {
            Settings.Changed -= OnSettingsChanged;

            if (Instance == this)
            {
                Instance = null;
            }
        }
        #endregion

        #region Public Interface for External Classes
        public static void AutoPullHUD()
        {
            if (Instance == null) return;


            Instance.ShowBigUI();
            Instance.HideSmallUI();
            Instance.HideTitlesUI();
            Instance.currentUIState = UIState.BigUI;
        }

        public static void AutoShowTitles(bool animate = true)
        {
            if (!CanShowTitles) return;
            if (Instance == null) return;

            Instance.HideSmallUI();
            Instance.HideBigUI();

            if (animate)
            {
                Instance.ShowTitlesUI();
            }
            else
            {
                Instance.ShowTitlesUIWithoutAnimation();
            }

            Instance.currentUIState = UIState.TitlesUI;
        }

        public static void ClearTitlesForNewGame()
        {
            Instance?.titlesUI?.ClearTitlesForNewGame();
        }

        public static void OnGameSessionStarted(bool preserveEndOfGameTitles = false)
        {
            if (Instance == null) return;

            Instance.restartTitlesPause = false;

            if (preserveEndOfGameTitles &&
                Instance.currentUIState == UIState.TitlesUI &&
                TitlesManager.Instance.HasGameEndedTitles &&
                TitlesManager.Instance.TitleCount > 0)
            {
                return;
            }

            if (Instance.currentUIState == UIState.BigUI || Instance.currentUIState == UIState.TitlesUI)
            {
                Instance.HideBigUI();
                Instance.HideTitlesUI();
                Instance.ShowSmallUI();
                Instance.currentUIState = UIState.SmallUI;
            }
        }

        public static void PauseGameLikeEsc()
        {
            try
            {
                if (HudController.instance != null && !HudController.instance.paused)
                {
                    HudController.instance.Pause();
                    if (Instance != null)
                    {
                        Instance.restartTitlesPause = HudController.instance.paused &&
                            Instance.titlesUI != null && Instance.titlesUI.IsVisible();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"Failed to pause game through HudController: {ex.Message}");
            }
        }

        public static void PauseGameLikeEscDelayed(float delaySeconds)
        {
            if (Instance == null)
            {
                PauseGameLikeEsc();
                return;
            }

            if (Instance.delayedPauseCoroutine != null)
            {
                Instance.StopCoroutine(Instance.delayedPauseCoroutine);
            }

            Instance.delayedPauseCoroutine = Instance.StartCoroutine(Instance.PauseGameLikeEscAfterDelay(delaySeconds));
        }

        private IEnumerator PauseGameLikeEscAfterDelay(float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(delaySeconds);
            }

            PauseGameLikeEsc();
            delayedPauseCoroutine = null;
        }
        #endregion
    }
}
