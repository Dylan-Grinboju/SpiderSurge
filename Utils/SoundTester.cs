using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // Press F10 to toggle the sound tester menu.
    public class SoundTester : MonoBehaviour
    {
        public static SoundTester Instance { get; private set; }

        private bool _showMenu = false;
        private Vector2 _scrollPos = Vector2.zero;
        private Rect _windowRect = new Rect(20, 20, 400, 500);

        // Sound info for display
        private class SoundInfo
        {
            public string Name;
            public string DisplayName;
            public System.Func<float> GetVolume;
            public System.Action<float> SetVolume;
        }

        private List<SoundInfo> _sounds;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeSoundList();
            Logger.LogInfo("[SoundTester] Initialized - Press F10 to toggle menu");
        }

        private void InitializeSoundList()
        {
            _sounds = new List<SoundInfo>
            {
                new SoundInfo
                {
                    Name = Consts.SoundNames.LuckyUpgrade,
                    DisplayName = "Lucky Upgrade",
                    GetVolume = () => Consts.SoundVolumes.LuckyUpgrade,
                    SetVolume = (v) => Consts.SoundVolumes.LuckyUpgrade = v
                },
                new SoundInfo
                {
                    Name = Consts.SoundNames.AbilityNotReady,
                    DisplayName = "Ability Not Ready",
                    GetVolume = () => Consts.SoundVolumes.AbilityNotReady,
                    SetVolume = (v) => Consts.SoundVolumes.AbilityNotReady = v
                },
                new SoundInfo
                {
                    Name = Consts.SoundNames.AmmoAbility,
                    DisplayName = "Ammo Ability",
                    GetVolume = () => Consts.SoundVolumes.AmmoAbility,
                    SetVolume = (v) => Consts.SoundVolumes.AmmoAbility = v
                },
                new SoundInfo
                {
                    Name = Consts.SoundNames.ExplosionAbility,
                    DisplayName = "Explosion Ability",
                    GetVolume = () => Consts.SoundVolumes.ExplosionAbility,
                    SetVolume = (v) => Consts.SoundVolumes.ExplosionAbility = v
                },
                new SoundInfo
                {
                    Name = Consts.SoundNames.PowerUp,
                    DisplayName = "Power Up",
                    GetVolume = () => Consts.SoundVolumes.PowerUp,
                    SetVolume = (v) => Consts.SoundVolumes.PowerUp = v
                }
            };
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            // F10 - Toggle sound tester menu
            if (Keyboard.current.f10Key.wasPressedThisFrame)
            {
                _showMenu = !_showMenu;
                Logger.LogInfo($"[SoundTester] Menu {(_showMenu ? "opened" : "closed")}");
            }
        }

        private void OnGUI()
        {
            if (!_showMenu) return;

            // Make the window draggable
            _windowRect = GUI.Window(12345, _windowRect, DrawSoundTesterWindow, "Sound Tester (F10 to close)");
        }

        private void DrawSoundTesterWindow(int windowID)
        {
            GUILayout.BeginVertical();

            // Master Volume control
            GUILayout.BeginHorizontal();
            GUILayout.Label("Master Volume:", GUILayout.Width(120));
            Consts.SoundVolumes.MasterVolume = GUILayout.HorizontalSlider(
                Consts.SoundVolumes.MasterVolume, 0f, 1f, GUILayout.Width(150));
            GUILayout.Label($"{Consts.SoundVolumes.MasterVolume:F2}", GUILayout.Width(50));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("Individual Sounds:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUILayout.Space(5);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            if (SoundManager.Instance == null)
            {
                GUILayout.Label("SoundManager not initialized!");
            }
            else
            {
                foreach (var sound in _sounds)
                {
                    DrawSoundRow(sound);
                    GUILayout.Space(5);
                }
            }

            GUILayout.EndScrollView();

            // Stop all sounds button (if audio source is available)
            if (GUILayout.Button("Stop All"))
            {
                SoundManager.Instance?.StopAll();
            }

            GUILayout.Space(5);
            GUILayout.Label("Tip: Edit volumes live via Unity Explorer", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic, fontSize = 11 });
            GUILayout.Label("Path: SpiderSurge.Consts.SoundVolumes", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic, fontSize = 10 });

            GUILayout.EndVertical();

            // Make window draggable
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void DrawSoundRow(SoundInfo sound)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            // Sound name and play button
            GUILayout.BeginHorizontal();
            GUILayout.Label(sound.DisplayName, GUILayout.Width(150));
            if (GUILayout.Button("▶ Play", GUILayout.Width(70)))
            {
                float effectiveVolume = sound.GetVolume() * Consts.SoundVolumes.MasterVolume;
                SoundManager.Instance?.PlaySound(sound.Name, effectiveVolume);
                Logger.LogInfo($"[SoundTester] Playing {sound.DisplayName} at volume {effectiveVolume:F2}");
            }
            GUILayout.EndHorizontal();

            // Volume slider
            GUILayout.BeginHorizontal();
            GUILayout.Label("Volume:", GUILayout.Width(60));
            float currentVol = sound.GetVolume();
            float newVol = GUILayout.HorizontalSlider(currentVol, 0f, 1f, GUILayout.Width(150));
            if (newVol != currentVol)
            {
                sound.SetVolume(newVol);
            }
            GUILayout.Label($"{newVol:F2}", GUILayout.Width(40));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        public static void Initialize()
        {
            if (Instance != null)
            {
                return;
            }

            var go = new GameObject("SoundTester");
            go.AddComponent<SoundTester>();
            Logger.LogInfo("[SoundTester] Created SoundTester GameObject");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
