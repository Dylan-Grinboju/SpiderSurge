using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiderSurge
{
    public class TutorialUI : MonoBehaviour
    {
        private static TutorialUI _instance;
        public static TutorialUI Instance => _instance;

        private bool isVisible = false;
        private bool stylesInitialized = false;

        #region GUI Styles
        private GUIStyle backgroundStyle;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle cardStyle;
        private GUIStyle titleStyle;
        private GUIStyle keyStyle;
        private GUIStyle greenStyle;
        private GUIStyle blueStyle;
        private GUIStyle redStyle;
        #endregion

        #region Colors
        private static readonly Color Blue = new Color(0.259f, 0.522f, 0.957f, 1f);
        private static readonly Color Red = new Color(1f, 0.341f, 0.133f, 1f);
        private static readonly Color Green = new Color(0.298f, 0.686f, 0.314f, 1f);
        private static readonly Color White = new Color(0.9f, 0.9f, 0.9f, 1f);
        private static readonly Color DarkGray = new Color(0.10f, 0.10f, 0.10f, 0.98f);
        private static readonly Color MediumGray = new Color(0.20f, 0.20f, 0.20f, 0.95f);
        private static readonly Color Orange = new Color(1f, 0.647f, 0f, 1f);
        #endregion

        #region Textures
        private Texture2D darkTexture;
        private Texture2D mediumTexture;
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

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            CreateTextures();
        }

        private void Start()
        {
            if (ModConfig.ShowTutorial)
            {
                Show();
                ModConfig.SetShowTutorial(false);
            }
        }

        private void Update()
        {
            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                Toggle();
            }
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

        private void CreateTextures()
        {
            darkTexture = new Texture2D(1, 1);
            darkTexture.SetPixel(0, 0, DarkGray);
            darkTexture.Apply();

            mediumTexture = new Texture2D(1, 1);
            mediumTexture.SetPixel(0, 0, MediumGray);
            mediumTexture.Apply();
        }

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            backgroundStyle = new GUIStyle { normal = { background = darkTexture } };

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Orange },
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Blue },
                padding = new RectOffset(10, 10, 10, 5),
                richText = true
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                normal = { textColor = White },
                padding = new RectOffset(20, 10, 5, 5),
                wordWrap = true,
                richText = true
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Green },
                padding = new RectOffset(5, 5, 5, 5),
                richText = true
            };

            keyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Orange },
                padding = new RectOffset(5, 5, 5, 5),
                richText = true
            };

            cardStyle = new GUIStyle
            {
                normal = { background = mediumTexture },
                padding = new RectOffset(20, 20, 15, 15),
                margin = new RectOffset(10, 10, 10, 10)
            };

            greenStyle = new GUIStyle(labelStyle) { normal = { textColor = Green }, fontStyle = FontStyle.Bold, richText = true };
            blueStyle = new GUIStyle(labelStyle) { normal = { textColor = Blue }, fontStyle = FontStyle.Bold, richText = true };
            redStyle = new GUIStyle(labelStyle) { normal = { textColor = Red }, fontStyle = FontStyle.Bold, richText = true };

            stylesInitialized = true;
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

            Rect rect = new Rect(x, y, width, height);
            GUI.Box(rect, "", backgroundStyle);

            GUILayout.BeginArea(new Rect(x + 30, y + 20, width - 60, height - 40));

            // Fixed top part
            GUILayout.Label("SPIDER SURGE MOD", titleStyle);
            GUILayout.Space(10);

            // Scrollable content
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false);
            GUILayout.BeginVertical();

            DrawSection("General Information", new string[] {
                "• This mod overrides the standard Survival Mode with new wave logic and enemies.",
                "• Highscores are kept separately from the vanilla game. It is still recommended to backup your save files.",
                "• <b>To Disable:</b> Delete the SpiderSurge.dll or set 'EnableSurgeMode' to false in the config."
            });

            DrawSection("ABILITIES", new string[] {
                "• Special Abilities perks appear at the first perk choice.",
                "• Special Ultimate upgrades appear at wave 30.",
                "• Choose wisely, Ultimates have longer cooldowns but high impact!",
            });

            DrawSection("HOW TO ACTIVATE ABILITIES", new string[] {
                "Ability:", "Q (Keyboard) / L1 (Gamepad)",
                "Ultimate Ability:", "C (Keyboard) / L3 + R3 (Gamepad)",
                "Config:", "Set 'UseDpadForUltimate' to <b>true</b> to use D-pad."
            }, true);

            DrawSection("CUSTOMIZE CONTROLS", new string[] {
                "<b>Set New Button:</b> Hold <b>Menu/Start</b> + <b>Double-Tap</b> any button.",
                "<b>Reset to Default:</b> Hold <b>Menu/Start</b> for <b>3 seconds</b>.",
                "• Customizable Per-Player. Settings persist until you leave or reset."
            });

            DrawIndicatorSection();

            DrawSynergySection();

            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            // Fixed bottom part
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("<b>Press [ ` ] (Backtick) to toggle this UI at any time</b>", keyStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.EndArea();
        }

        private Vector2 scrollPosition = Vector2.zero;

        private void DrawIndicatorSection()
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label("ABILITY INDICATOR", headerStyle);

            float dotWidth = 25f;
            float statusWidth = 140f;

            GUILayout.BeginHorizontal();
            GUILayout.Label("•", labelStyle, GUILayout.Width(dotWidth));
            GUILayout.Label("Green:", greenStyle, GUILayout.Width(statusWidth));
            GUILayout.Label("Ability is ready to use.", labelStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("•", labelStyle, GUILayout.Width(dotWidth));
            GUILayout.Label("Blue:", blueStyle, GUILayout.Width(statusWidth));
            GUILayout.Label("Ability is currently active.", labelStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("•", labelStyle, GUILayout.Width(dotWidth));
            GUILayout.Label("Red:", redStyle, GUILayout.Width(statusWidth));
            GUILayout.Label("Ability is on cooldown.", labelStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("• You can customize indicator <b>radius, offset, and colors</b> in the config YAML.", labelStyle);

            GUILayout.EndVertical();
        }

        private void DrawSynergySection()
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label("SYNERGY PERKS", headerStyle);
            GUILayout.Label("Combine mod abilities with vanilla perks for bonus effects! Synergized perks are marked with <color=#00FFFF>Synergized</color> in the perk selection.", labelStyle);
            GUILayout.Space(10);

            // Parry (Shield) Synergies
            GUILayout.Label("<b><color=#FFD700>Parry</color></b> + Shield Perks:", labelStyle);
            GUILayout.Label("• <b>Start Shields</b>, <b>Positive Encouragement</b>, or <b>Safety Net</b>:", labelStyle);
            GUILayout.Label("  → If you have a shield when activating Parry, gain <b>full immunity</b> instead of a breakable shield.", labelStyle);
            GUILayout.Space(8);

            // Keep Shooting (Infinite Ammo) Synergies
            GUILayout.Label("<b><color=#FFD700>Keep Shooting</color></b> + Efficiency:", labelStyle);
            GUILayout.Label("• <b>Efficiency Lv1</b>: Ability refills ammo to at least <b>50%</b> of max.", labelStyle);
            GUILayout.Label("• <b>Efficiency Lv2</b>: Ability refills ammo to <b>100%</b> of max.", labelStyle);
            GUILayout.Space(8);

            // The Force (Explosion) Synergies
            GUILayout.Label("<b><color=#FFD700>The Force</color></b> + Explosion Perks:", labelStyle);
            GUILayout.Label("• <b>Bigger Boom</b>: Increases <b>knockback strength</b>.", labelStyle);
            GUILayout.Label("• <b>Too Cool</b>: Increases <b>death radius</b> of the ultimate.", labelStyle);
            GUILayout.Space(8);

            // Interdimensional Storage Synergies
            GUILayout.Label("<b><color=#FFD700>Interdimensional Storage</color></b> + Weapon Perks:", labelStyle);
            GUILayout.Label("• <b>More Guns</b>, <b>More Boom</b>, or <b>More Particles</b>:", labelStyle);
            GUILayout.Label("  → <b>Level 1</b>: Keep matching stored weapons between rounds.", labelStyle);
            GUILayout.Label("  → <b>Level 2</b>: Keep matching stored weapons even after <b>death</b>.", labelStyle);
            GUILayout.Label("  (More Guns = Guns, More Boom = Explosives/Throwables/Mines, More Particles = Particle/Melee)", labelStyle);

            GUILayout.EndVertical();
        }

        private void DrawSection(string title, string[] content, bool isKeyPairs = false)
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label(title, headerStyle);

            if (isKeyPairs)
            {
                float keyWidth = 400f;
                for (int i = 0; i < content.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(content[i], labelStyle, GUILayout.Width(keyWidth));
                    if (i + 1 < content.Length)
                    {
                        GUILayout.Label(content[i + 1], valueStyle);
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
                        var parts = line.Split(':');
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(parts[0] + ":", keyStyle, GUILayout.Width(350));
                        GUILayout.Label(parts[1].Trim(), labelStyle);
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
            if (darkTexture != null) Destroy(darkTexture);
            if (mediumTexture != null) Destroy(mediumTexture);
            _instance = null;
        }
    }
}
