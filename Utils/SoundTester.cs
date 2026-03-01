using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Logger = Silk.Logger;

namespace SpiderSurge;

// Press F10 to toggle the sound tester menu.
public class SoundTester : MonoBehaviour
{
    public static SoundTester Instance { get; private set; }

    private bool _showMenu = false;
    private Vector2 _scrollPos = Vector2.zero;
    private Rect _windowRect = new(20, 20, 400, 500);

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
        if (Instance is not null && Instance != this)
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
        _sounds =
        [
            new() {
                Name = Consts.SoundNames.LuckyUpgrade,
                DisplayName = "Lucky Upgrade",
                GetVolume = () => Consts.SoundVolumes.LuckyUpgrade,
                SetVolume = (v) => Consts.SoundVolumes.LuckyUpgrade = v
            },
            new() {
                Name = Consts.SoundNames.AbilityNotReady,
                DisplayName = "Ability Not Ready",
                GetVolume = () => Consts.SoundVolumes.AbilityNotReady,
                SetVolume = (v) => Consts.SoundVolumes.AbilityNotReady = v
            },
            new() {
                Name = Consts.SoundNames.AbilityReady,
                DisplayName = "Ability Ready",
                GetVolume = () => Consts.SoundVolumes.AbilityReady,
                SetVolume = (v) => Consts.SoundVolumes.AbilityReady = v
            },
            new() {
                Name = Consts.SoundNames.AmmoAbility,
                DisplayName = "Ammo Ability",
                GetVolume = () => Consts.SoundVolumes.AmmoAbility,
                SetVolume = (v) => Consts.SoundVolumes.AmmoAbility = v
            },
            new() {
                Name = Consts.SoundNames.PulseAbility,
                DisplayName = "Pulse Ability",
                GetVolume = () => Consts.SoundVolumes.PulseAbility,
                SetVolume = (v) => Consts.SoundVolumes.PulseAbility = v
            },
            new() {
                Name = Consts.SoundNames.PowerUp,
                DisplayName = "Power Up",
                GetVolume = () => Consts.SoundVolumes.PowerUp,
                SetVolume = (v) => Consts.SoundVolumes.PowerUp = v
            },
            new() {
                Name = Consts.SoundNames.ImmuneAbility,
                DisplayName = "Immune Ability",
                GetVolume = () => Consts.SoundVolumes.ImmuneAbility,
                SetVolume = (v) => Consts.SoundVolumes.ImmuneAbility = v
            },
            new() {
                Name = Consts.SoundNames.ImmuneUlt,
                DisplayName = "Immune Ultimate",
                GetVolume = () => Consts.SoundVolumes.ImmuneUlt,
                SetVolume = (v) => Consts.SoundVolumes.ImmuneUlt = v
            },
            new() {
                Name = Consts.SoundNames.StorageSend,
                DisplayName = "Storage Send",
                GetVolume = () => Consts.SoundVolumes.StorageSend,
                SetVolume = (v) => Consts.SoundVolumes.StorageSend = v
            },
            new() {
                Name = Consts.SoundNames.StorageRetrieve,
                DisplayName = "Storage Retrieve",
                GetVolume = () => Consts.SoundVolumes.StorageRetrieve,
                SetVolume = (v) => Consts.SoundVolumes.StorageRetrieve = v
            },
            new() {
                Name = Consts.SoundNames.AbilityEnded,
                DisplayName = "Ability Ended",
                GetVolume = () => Consts.SoundVolumes.AbilityEnded,
                SetVolume = (v) => Consts.SoundVolumes.AbilityEnded = v
            }
        ];
    }

    private void Update()
    {
        if (Keyboard.current is null)
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

        if (SoundManager.Instance is null)
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
        if (Instance is not null)
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
