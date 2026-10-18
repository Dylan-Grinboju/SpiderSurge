using System.Collections.Generic;
using System.Reflection;
using Doozy.Engine.UI.Input;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiderSurge
{
    public class TutorialUI : MonoBehaviour
    {
        private const string Gold = "#FFD24A";
        private const string Cyan = "#00FFFF";
        private const float StickDeadZone = 0.25f;
        private const float ScrollSpeed = 900f;

        private static TutorialUI _instance;
        public static TutorialUI Instance => _instance;

        public static bool IsShowing => _instance != null && _instance.isVisible;

        // Also true until the button that closed the guide is let go, so that press doesn't reach the game
        public static bool BlocksInput => _instance != null && (_instance.isVisible || _instance.releasePending);

        private bool isVisible = false;
        private bool showPending;
        private bool releasePending;
        private int fontVersion = -1;
        private float scale = 1f;
        private Vector2 scrollPosition = Vector2.zero;

        #region GUI Styles
        private GUIStyle captionStyle;
        private GUIStyle titleStyle;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle keyStyle;
        private GUIStyle valueStyle;
        private GUIStyle footerStyle;
        private GUIStyle cardStyle;
        #endregion

        public static void Initialize()
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("TutorialUI");
                _instance = obj.AddComponent<TutorialUI>();
                DontDestroyOnLoad(obj);
            }
        }

        public static void ResetInstance()
        {
            _instance = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            showPending = ModConfig.ShowTutorial;
            Settings.Changed += OnSettingChanged;
        }

        private void OnSettingChanged(string key)
        {
            if (key == null || key == ModConfig.ShowTutorialKey)
            {
                showPending = ModConfig.ShowTutorial;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame)
            {
                Toggle();
            }

            // Turned on in the settings, or set on first launch: shown once the menus and startup popups are closed
            if (showPending && !isVisible && MenusClosed())
            {
                showPending = false;
                Show();
                ModConfig.SetShowTutorial(false);
            }

            if (releasePending && !CloseHeld())
            {
                releasePending = false;
            }
            if (!isVisible) return;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            float scroll = 0f;
            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
            {
                Gamepad pad = pads[i];
                if (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)
                {
                    Close();
                    return;
                }
                float y = Mathf.Clamp(pad.leftStick.y.ReadValue() + pad.rightStick.y.ReadValue() + pad.dpad.y.ReadValue(), -1f, 1f);
                if (Mathf.Abs(y) > Mathf.Abs(scroll)) scroll = y;
            }
            if (Mathf.Abs(scroll) > StickDeadZone)
            {
                scrollPosition.y = Mathf.Max(0f, scrollPosition.y - scroll * ScrollSpeed * scale * Time.unscaledDeltaTime);
            }
        }

        private void Close()
        {
            Hide();
            releasePending = true;
        }

        private static bool CloseHeld()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.isPressed) return true;
            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
            {
                if (pads[i].buttonEast.isPressed || pads[i].startButton.isPressed) return true;
            }
            return false;
        }

        private static bool MenusClosed()
        {
            return HudController.instance != null && !HudController.instance.paused
                && LobbyController.instance != null && !LobbyController.instance.eulaStartPopup && !LobbyController.instance.newContentPopup;
        }

        public void Toggle()
        {
            isVisible = !isVisible;
        }

        public void Show()
        {
            isVisible = true;
        }

        public void Hide()
        {
            isVisible = false;
        }

        private int S(float value) => Mathf.RoundToInt(value * scale);

        private void InitializeStyles()
        {
            Hud.EnsureFonts();
            float screenScale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 3f);
            if (fontVersion == Hud.FontVersion && cardStyle != null && Mathf.Approximately(scale, screenScale)) return;
            scale = screenScale;

            captionStyle = Hud.Style(S(16), bold: true, anchor: TextAnchor.MiddleCenter);
            captionStyle.normal.textColor = Hud.Muted;
            titleStyle = Hud.Style(S(52), bold: true, anchor: TextAnchor.MiddleCenter);
            headerStyle = Hud.Style(S(24), bold: true);
            headerStyle.normal.textColor = Hud.Pink;
            headerStyle.padding = new RectOffset(0, 0, 0, S(8));
            labelStyle = Hud.Style(S(20), wrap: true);
            labelStyle.padding = new RectOffset(0, 0, S(4), S(4));
            keyStyle = Hud.Style(S(20), bold: true, wrap: true);
            keyStyle.normal.textColor = Hud.Muted;
            keyStyle.padding = new RectOffset(0, 0, S(4), S(4));
            valueStyle = Hud.Style(S(20), bold: true);
            valueStyle.normal.textColor = Hud.Text;
            valueStyle.padding = new RectOffset(0, 0, S(4), S(4));
            footerStyle = Hud.Style(S(16), bold: true, anchor: TextAnchor.MiddleCenter);
            footerStyle.normal.textColor = Hud.Muted;

            cardStyle = new GUIStyle
            {
                normal = { background = Hud.Solid(Hud.Card) },
                padding = new RectOffset(S(28), S(28), S(22), S(22)),
                margin = new RectOffset(0, 0, 0, S(16))
            };

            fontVersion = Hud.FontVersion;
        }

        private void OnGUI()
        {
            if (!isVisible) return;

            // Reset GUI state to prevent interference from other mods
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;

            InitializeStyles();

            float marginPercent = 0.05f;
            float width = Screen.width * (1f - marginPercent * 2f);
            float height = Screen.height * (1f - marginPercent * 2f);
            float x = Screen.width * marginPercent;
            float y = Screen.height * marginPercent;

            Hud.Fill(new Rect(0f, 0f, Screen.width, Screen.height), Hud.WithAlpha(Hud.Dimmer, 0.6f));
            Rect rect = new Rect(x, y, width, height);
            Hud.Fill(rect, Hud.Ink);
            Hud.Fill(new Rect(x, y, width, S(3)), Hud.Pink);

            GUILayout.BeginArea(new Rect(x + S(40), y + S(24), width - S(80), height - S(48)));

            // Fixed top part
            GUILayout.Label("SURVIVAL GUIDE", captionStyle);
            GUILayout.Label(Hud.Logo, titleStyle);
            GUILayout.Space(S(18));

            // Scrollable content
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical();

            DrawSection("General information", new string[] {
                "• <b>To play:</b> stand on the <b>Surge Survival</b> platform in the lobby, above and to the left of Wave Survival.",
                "• Surge Survival brings new wave logic and enemies. The Wave Survival platform still starts the normal game.",
                "• Surge highscores are kept separately from the vanilla game. It is still recommended to backup your save files."
            });

            DrawSection("Abilities", new string[] {
                "• Special Abilities perks appear at the first perk choice.",
                "• Your Ultimate is guaranteed as a choice at wave 30 and can appear in later perk choices until unlocked.",
                "• At wave 60, you can unlock your Ultimate or switch to another ability with its Ultimate.",
                "• <b>Choose wisely:</b> Ultimates have longer cooldowns but high impact!",
            });

            DrawSection("How to activate abilities", new string[] {
                "Ability", "Q (Keyboard) / L1 (Gamepad)",
                "Ultimate Ability", "C (Keyboard) / L3 + R3 (Gamepad)",
                "D-pad", "Turn on <b>Ultimate on the D-pad</b> in Mods > SpiderSurge > Surge to cast it with any D-pad direction."
            }, true);

            DrawSection("Customize controls", new string[] {
                "<b>Set a new ability button on a gamepad:</b> While holding Menu/Start, double-tap any button.",
                "<b>Reset to Default:</b> Hold <b>Menu/Start</b> for <b>3 seconds</b>.",
                "• Customizable Per-Player. Settings persist until you leave or reset."
            });

            DrawIndicatorSection();

            DrawSynergySection();

            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            // Fixed bottom part
            GUILayout.FlexibleSpace();
            GUILayout.Space(S(10));
            GUILayout.Label($"PRESS <color={Hud.PinkHex}>[ ` ]</color> (BACKTICK) TO TOGGLE THIS GUIDE AT ANY TIME, OR <color={Hud.PinkHex}>B</color> / <color={Hud.PinkHex}>START</color> ON A GAMEPAD TO CLOSE IT", footerStyle);

            GUILayout.EndArea();
        }

        private void DrawIndicatorSection()
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label("ABILITY INDICATOR", headerStyle);

            DrawIndicatorRow(ModConfig.IndicatorAvailableColor, "Ready", "Ability is ready to use.");
            DrawIndicatorRow(ModConfig.IndicatorActiveColor, "Active", "Ability is currently active.");
            DrawIndicatorRow(ModConfig.IndicatorCooldownColor, "Cooldown", "Ability is on cooldown.");

            GUILayout.Space(S(10));
            GUILayout.Label("• Change the indicator's <b>size, offset and colors</b> in <b>Mods > SpiderSurge > Surge</b>.", labelStyle);

            GUILayout.EndVertical();
        }

        private void DrawIndicatorRow(Color color, string status, string meaning)
        {
            GUILayout.BeginHorizontal();
            Rect dot = GUILayoutUtility.GetRect(S(26), S(32), GUILayout.Width(S(26)));
            Hud.Fill(new Rect(dot.x, dot.center.y - S(7), S(14), S(14)), color);
            GUILayout.Label(status, keyStyle, GUILayout.Width(S(160)));
            GUILayout.Label(meaning, labelStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawSynergySection()
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label("SYNERGY PERKS", headerStyle);
            GUILayout.Label($"Combine mod abilities with vanilla perks for bonus effects! Synergized perks are marked with <color={Cyan}>Synergized</color> in the perk selection.", labelStyle);
            GUILayout.Space(S(10));

            GUILayout.Label($"<b><color={Gold}>Null Field</color></b> + Shield Perks:", labelStyle);
            GUILayout.Label("• <b>Start Shields</b>, <b>Positive Encouragement</b>, or <b>Safety Net</b>:", labelStyle);
            GUILayout.Label("  → If you have a shield when activating Null Field and avoid getting hit, you keep the shield after immunity ends.", labelStyle);
            GUILayout.Space(S(8));

            GUILayout.Label($"<b><color={Gold}>Bottomless Clip</color></b> + Efficiency:", labelStyle);
            GUILayout.Label("• No Efficiency: When the effect ends or weapon is dropped, the weapon is <b>disintegrated</b>.", labelStyle);
            GUILayout.Label("• <b>Efficiency Lv1</b>: Keep weapon ammo at its original pre-effect value.", labelStyle);
            GUILayout.Label("• <b>Efficiency Lv2</b>: Held weapon is kept at least at <b>50%</b> capacity.", labelStyle);
            GUILayout.Label("  → If dropped/thrown, both Lv1/Lv2 keep original pre-effect ammo.", labelStyle);
            GUILayout.Space(S(8));

            GUILayout.Label($"<b><color={Gold}>Kinetic Pulse</color></b> + Explosion Perks:", labelStyle);
            GUILayout.Label("• <b>Bigger Boom</b>: Increases <b>knockback strength</b>.", labelStyle);
            GUILayout.Label("• <b>Too Cool</b>: Increases <b>death radius</b> of the ultimate.", labelStyle);
            GUILayout.Space(S(8));

            GUILayout.Label($"<b><color={Gold}>Pocket Dimension</color></b> + Weapon Perks:", labelStyle);
            GUILayout.Label("• <b>More Guns</b>, <b>More Boom</b>, or <b>More Particles</b>:", labelStyle);
            GUILayout.Label("  → <b>Level 1</b>: Keep matching stored weapons between rounds.", labelStyle);
            GUILayout.Label("  → <b>Level 2</b>: Keep matching stored weapons even after <b>death</b>.", labelStyle);
            GUILayout.Label("  (More Guns = Guns, More Boom = Explosives/Throwables/Mines, More Particles = Particle/Melee)", labelStyle);

            GUILayout.EndVertical();
        }

        private void DrawSection(string title, string[] content, bool isKeyPairs = false)
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label(title.ToUpperInvariant(), headerStyle);

            if (isKeyPairs)
            {
                float keyWidth = S(260);
                for (int i = 0; i < content.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(content[i], keyStyle, GUILayout.Width(keyWidth));
                    if (i + 1 < content.Length)
                    {
                        GUILayout.Label(content[i + 1], content[i + 1].Contains("<") ? labelStyle : valueStyle);
                    }
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                foreach (string line in content)
                {
                    // Only split if it's a simple key:value without rich text tags in the key part
                    if (line.Contains(":") && !line.Contains("<"))
                    {
                        var parts = line.Split(new[] { ':' }, 2);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(parts[0] + ":", keyStyle, GUILayout.Width(S(350)));
                        GUILayout.Label(parts.Length > 1 ? parts[1].Trim() : string.Empty, labelStyle);
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUILayout.Label(line, labelStyle);
                    }
                }
            }

            GUILayout.EndVertical();
        }

        private void OnDestroy()
        {
            Settings.Changed -= OnSettingChanged;
            if (_instance == this) _instance = null;
        }
    }

    // The press that closes the guide would also pause the game, throw a weapon or go back in a menu
    [HarmonyPatch(typeof(HudController), nameof(HudController.Pause))]
    public static class TutorialUI_Pause_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix() => !TutorialUI.BlocksInput;
    }

    [HarmonyPatch(typeof(BackButton), nameof(BackButton.Execute))]
    public static class TutorialUI_Back_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix() => !TutorialUI.BlocksInput;
    }

    [HarmonyPatch]
    public static class TutorialUI_Equip_Patch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SpiderWeaponManager), nameof(SpiderWeaponManager.OnEquip));
            yield return AccessTools.Method(typeof(PickUpVacuum), nameof(PickUpVacuum.OnEquip));
        }

        [HarmonyPrefix]
        public static bool Prefix(InputValue inputValue) => !(TutorialUI.BlocksInput && inputValue.isPressed);
    }
}
