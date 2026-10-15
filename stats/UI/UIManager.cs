using UnityEngine;
using Logger = Silk.Logger;
using UnityEngine.InputSystem;
using System.Collections;

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
            cycleInput = ModConfig.CycleInput;
        }

        #region Singleton
        public static UIManager Instance { get; private set; }
        #endregion

        #region UI Components
        private SmallUI smallUI;
        private BigUI bigUI;
        private TitlesUI titlesUI;
        #endregion

        #region Controller Cycling State
        private UIState currentUIState = UIState.Off;
        private string cycleInput;
        private Coroutine delayedPauseCoroutine;
        #endregion

        #region Shared Constants
        public const int PADDING = 8;
        public const int HEADER_SIZE = 16;
        public const int LABEL_SIZE = 14;
        public const int BIG_HEADER_SIZE = 24;
        public const int BIG_LABEL_SIZE = 18;
        #endregion

        #region Shared Colors
        public static readonly Color Blue = new Color(0.259f, 0.522f, 0.957f, 1f);
        public static readonly Color Red = new Color(1f, 0.341f, 0.133f, 1f);
        public static readonly Color Green = new Color(0.298f, 0.686f, 0.314f, 1f);
        public static readonly Color White = new Color(0.9f, 0.9f, 0.9f, 1f);
        public static readonly Color Gray = new Color(0.7f, 0.7f, 0.7f, 1f);
        public static readonly Color DarkGray = new Color(0.10f, 0.10f, 0.10f, 0.95f);
        public static readonly Color MediumGray = new Color(0.20f, 0.20f, 0.20f, 0.95f);
        public static readonly Color LightGray = new Color(0.35f, 0.35f, 0.35f, 0.95f);
        public static readonly Color Orange = new Color(1f, 0.647f, 0f, 1f);
        #endregion

        #region Shared Textures
        private Texture2D darkTexture;
        private Texture2D mediumTexture;
        private Texture2D lightTexture;
        #endregion

        #region UI Scaling
        private static float _uiScaleFactor = 1.0f;
        private static bool _scalingInitialized = false;

        public static float UIScaleFactor
        {
            get
            {
                if (!_scalingInitialized) InitializeScaling();
                return _uiScaleFactor;
            }
        }

        private static void InitializeScaling()
        {
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

                Logger.LogInfo($"UI Auto-scaling initialized: Screen {currentWidth}x{currentHeight}, Scale factor: {_uiScaleFactor:F2}");
            }
            else
            {
                _uiScaleFactor = ModConfig.UIScale;
                Logger.LogInfo($"UI Manual scaling: Scale factor: {_uiScaleFactor:F2}");
            }

            _scalingInitialized = true;
        }

        public static float ScaleValue(float value)
        {
            return value * UIScaleFactor;
        }

        public static int ScaleInt(int value)
        {
            return Mathf.RoundToInt(value * UIScaleFactor);
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
            CreateSharedTextures();

            GameObject smallUIObj = new GameObject("SmallUI");
            smallUIObj.transform.SetParent(transform);
            smallUI = smallUIObj.AddComponent<SmallUI>();
            smallUI.Initialize();

            GameObject bigUIObj = new GameObject("BigUI");
            bigUIObj.transform.SetParent(transform);
            bigUI = bigUIObj.AddComponent<BigUI>();
            bigUI.Initialize();

            GameObject titlesUIObj = new GameObject("TitlesUI");
            titlesUIObj.transform.SetParent(transform);
            titlesUI = titlesUIObj.AddComponent<TitlesUI>();
            titlesUI.Initialize();

        }
        #endregion

        #region Input Handling
        private void Update()
        {
            HandleKeyboardInput();
            HandleGamepadInput();
        }

        private void HandleKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                HandleF1Press();
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                HandleF2Press();
            }

            if (keyboard.f3Key.wasPressedThisFrame)
            {
                HandleF3Press();
            }
        }

        private void HandleGamepadInput()
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null) return;

            bool cyclePressed = false;
            bool dpadPressed = gamepad.dpad.up.wasPressedThisFrame || gamepad.dpad.down.wasPressedThisFrame ||
                               gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame;
            bool joystickPressed = gamepad.leftStickButton.wasPressedThisFrame || gamepad.rightStickButton.wasPressedThisFrame;
            switch (cycleInput)
            {
                case "dpad":
                    cyclePressed = dpadPressed;
                    break;
                case "joystick":
                    cyclePressed = joystickPressed;
                    break;
                case "both":
                    cyclePressed = dpadPressed || joystickPressed;
                    break;
                default:
                    break;
            }

            if (cyclePressed)
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
                    currentUIState = UIState.TitlesUI;
                    HideSmallUI();
                    HideBigUI();
                    ShowTitlesUIWithoutAnimation();
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

        private void HandleF1Press()
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

        private void HandleF2Press()
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

        private void HandleF3Press()
        {
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

        #region Shared Texture Management
        private void CreateSharedTextures()
        {
            darkTexture = CreateColorTexture(DarkGray);
            mediumTexture = CreateColorTexture(MediumGray);
            lightTexture = CreateColorTexture(LightGray);
        }

        private Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        public Texture2D GetDarkTexture() => darkTexture;
        public Texture2D GetMediumTexture() => mediumTexture;
        public Texture2D GetLightTexture() => lightTexture;
        #endregion

        #region Shared Style Creation
        public GUIStyle CreateWindowStyle(Texture2D backgroundTexture)
        {
            return new GUIStyle(GUI.skin.window)
            {
                normal = { background = backgroundTexture },
                padding = new RectOffset(ScaleInt(PADDING * 2), ScaleInt(PADDING * 2), ScaleInt(PADDING * 2), ScaleInt(PADDING * 2))
            };
        }

        public GUIStyle CreateHeaderStyle(int fontSize = HEADER_SIZE)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaleFont(fontSize),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Blue },
                padding = new RectOffset(ScaleInt(PADDING), 0, ScaleInt(4), ScaleInt(2))
            };
        }

        public GUIStyle CreateLabelStyle(int fontSize = LABEL_SIZE)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaleFont(fontSize),
                normal = { textColor = Gray },
                padding = new RectOffset(ScaleInt(PADDING), 0, ScaleInt(2), ScaleInt(2))
            };
        }

        public GUIStyle CreateValueStyle(int fontSize = LABEL_SIZE)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaleFont(fontSize),
                fontStyle = FontStyle.Bold,
                normal = { textColor = White },
                padding = new RectOffset(0, ScaleInt(PADDING), ScaleInt(2), ScaleInt(2))
            };
        }

        public GUIStyle CreateCardStyle(Texture2D backgroundTexture)
        {
            return new GUIStyle()
            {
                normal = { background = backgroundTexture },
                padding = new RectOffset(ScaleInt(PADDING), ScaleInt(PADDING), ScaleInt(4), ScaleInt(4)),
                margin = new RectOffset(ScaleInt(2), ScaleInt(2), ScaleInt(2), ScaleInt(2))
            };
        }

        public GUIStyle CreateErrorStyle(int fontSize = LABEL_SIZE)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaleFont(fontSize),
                normal = { textColor = Red }
            };
        }
        #endregion

        #region Event Handling
        public void OnPlayerJoined()
        {
            smallUI?.OnPlayerJoined();
            bigUI?.OnPlayerJoined();
        }

        public void OnPlayerLeft()
        {
            smallUI?.OnPlayerLeft();
            bigUI?.OnPlayerLeft();
        }
        #endregion

        #region Cleanup
        private void OnDestroy()
        {
            if (darkTexture != null) Destroy(darkTexture);
            if (mediumTexture != null) Destroy(mediumTexture);
            if (lightTexture != null) Destroy(lightTexture);

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
            if (!ModConfig.TitlesEnabled) return;
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
