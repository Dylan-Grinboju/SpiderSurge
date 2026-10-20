using System.Collections;
using Doozy.Engine.UI;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiderSurge
{
    // The Surge guide is a page of the Mods menu; its key and the first launch open the pause menu on it
    public class SurgeGuide : MonoBehaviour
    {
        public const string Page = "surge.guide";
        private const string ModsNode = "Mods";
        private const string Gold = "#FFD24A";
        private const string Cyan = "#00FFFF";
        private const float OpenTimeout = 3f;

        private static SurgeGuide _instance;
        private readonly ControlPoller _guideKey = new ControlPoller();
        private bool _opening;

        public static void Initialize()
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("SurgeGuide");
                _instance = obj.AddComponent<SurgeGuide>();
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

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (!ControlCapture.Busy && _guideKey.WasPressed(Controls.Get(Controls.SurgeGuide), Keyboard.current))
            {
                if (SettingsPage.IsShowing(Page)) HudController.instance.Resume();
                else Open();
            }

            if (ModConfig.ShowTutorial && !_opening && FirstLaunchReady())
            {
                Open();
            }
        }

        // Waits for the lobby to be up and the startup popups, Surge's telemetry question included, so the guide isn't covered or closed by them
        private static bool FirstLaunchReady()
        {
            return LobbyController.inLobby && !LevelController.IsLoading && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
                && SettingsPage.CanShow && !GameMenus.Open && !ModConfig.ShowTelemetryConsentPrompt && UIPopupManager.CurrentVisibleQueuePopup == null
                && LobbyController.instance != null && !LobbyController.instance.eulaStartPopup && !LobbyController.instance.newContentPopup;
        }

        public static void Open()
        {
            if (_instance != null && !_instance._opening) _instance.StartCoroutine(_instance.OpenGuide());
        }

        private IEnumerator OpenGuide()
        {
            _opening = true;
            HudController hud = HudController.instance;
            if (hud != null && SettingsPage.CanShow && !SettingsPage.IsShowing(Page))
            {
                if (!hud.paused && hud.canPauseMenu) hud.Pause();
                if (hud.paused)
                {
                    UIView mods = ModsMenu.instance != null ? ModsMenu.instance.GetComponent<UIView>() : null;
                    if (mods != null && !mods.IsVisible) hud.SetActiveGraphNode(ModsNode);
                    float until = Time.unscaledTime + OpenTimeout;
                    while (mods != null && !mods.IsVisible && Time.unscaledTime < until) yield return null;
                    if (mods != null && mods.IsVisible && SettingsPage.Show(Parts.Surge.Key, Page))
                    {
                        ModConfig.SetShowTutorial(false);
                    }
                }
            }
            _opening = false;
        }

        public static void Build(SettingsPage page)
        {
            page.Title("Surge guide");
            page.Line(Section("General information",
                "• <b>To play:</b> stand on the <b>Surge Survival</b> platform in the lobby, above and to the left of Wave Survival.",
                "• Surge Survival brings new wave logic and enemies. The Wave Survival platform still starts the normal game.",
                "• Surge highscores are kept separately from the vanilla game. It is still recommended to back up your save files."));
            page.Line(Section("Abilities",
                "• Special Abilities perks appear at the first perk choice.",
                "• Your Ultimate is guaranteed as a choice at wave 30 and can appear in later perk choices until unlocked.",
                "• At wave 60, you can unlock your Ultimate or switch to another ability with its Ultimate.",
                "• <b>Choose wisely:</b> Ultimates have longer cooldowns but high impact!"));
            page.Line(Section("How to activate abilities",
                $"<b>Ability:</b> {Key(Controls.AbilityKeyboard)} (keyboard) / {Key(Controls.AbilityController)} (controller)",
                $"<b>Ultimate:</b> {Key(Controls.UltimateKeyboard)} (keyboard) / {Key(Controls.UltimateController)} (controller)"));
            page.Line(Section("Customize controls",
                "<b>Change the buttons for everyone:</b> Mods > SpiderSurge > Controls.",
                "<b>Set your own ability button on a controller:</b> while holding Menu/Start, double-tap any button.",
                "<b>Back to the default:</b> hold <b>Menu/Start</b> for <b>3 seconds</b>.",
                "• Each player has their own. Settings persist until you leave or reset."));
            page.Line(Section("Ability indicator",
                $"<color={Hex(ModConfig.IndicatorAvailableColor)}><b>Ready:</b></color> the ability is ready to use.",
                $"<color={Hex(ModConfig.IndicatorActiveColor)}><b>Active:</b></color> the ability is active.",
                $"<color={Hex(ModConfig.IndicatorCooldownColor)}><b>Cooldown:</b></color> the ability is on cooldown.",
                "• Change the indicator's <b>size, offset and colors</b> in Mods > SpiderSurge > Surge > Ability indicator."));
            page.Line(Section("Synergy perks",
                $"Combine mod abilities with vanilla perks for bonus effects! Synergized perks are marked with <color={Cyan}>Synergized</color> in the perk selection."));
            page.Line(Synergy("Null Field", "Shield perks",
                "• <b>Start Shields</b>, <b>Positive Encouragement</b> or <b>Safety Net</b>: if you have a shield when activating Null Field and avoid getting hit, you keep the shield after immunity ends."));
            page.Line(Synergy("Bottomless Clip", "Efficiency",
                "• <b>No Efficiency:</b> when the effect ends or the weapon is dropped, the weapon is <b>disintegrated</b>.",
                "• <b>Efficiency Lv1:</b> keeps the weapon's ammo at its value from before the effect.",
                "• <b>Efficiency Lv2:</b> the held weapon is kept at least at <b>50%</b> capacity.",
                "• Dropped or thrown, Lv1 and Lv2 both keep the ammo from before the effect."));
            page.Line(Synergy("Kinetic Pulse", "Explosion perks",
                "• <b>Bigger Boom:</b> increases <b>knockback strength</b>.",
                "• <b>Too Cool:</b> increases the <b>death radius</b> of the ultimate."));
            page.Line(Synergy("Pocket Dimension", "Weapon perks",
                "• <b>More Guns</b>, <b>More Boom</b> or <b>More Particles</b> level 1: keep matching stored weapons between rounds.",
                "• Level 2: keep matching stored weapons even after <b>death</b>.",
                "• More Guns = guns, More Boom = explosives, throwables and mines, More Particles = particle and melee weapons."));
            if (Controls.Get(Controls.SurgeGuide) != Controls.None)
            {
                page.Note($"Press {Key(Controls.SurgeGuide)} to open this guide at any time.");
            }
        }

        private static string Key(Control control) => $"<noparse>{Controls.Label(control)}</noparse>";

        private static string Section(string title, params string[] lines)
        {
            return $"<size=120%><b><color={Hud.PinkHex}>{title.ToUpperInvariant()}</color></b></size>\n{string.Join("\n", lines)}";
        }

        private static string Synergy(string ability, string perks, params string[] lines)
        {
            return $"<b><color={Gold}>{ability}</color></b> + {perks}\n{string.Join("\n", lines)}";
        }

        private static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);
    }
}
