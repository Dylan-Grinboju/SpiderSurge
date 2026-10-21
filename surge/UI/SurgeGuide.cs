using System.Collections;
using System.Linq;
using SpiderSurge.Enemies;
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
            if (_guideKey.WasPressed(Controls.Get(Controls.SurgeGuide), Keyboard.current))
            {
                if (SettingsPage.IsShowingTree(Page)) HudController.instance.Resume();
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
            if (hud != null && SettingsPage.CanShow && !SettingsPage.IsShowingTree(Page))
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
            string section = page.Current;
            if (section == Page)
            {
                page.Line("Stand on the Surge Survival platform above and left of Wave Survival. Surge brings new abilities, perks and enemies. Wave Survival still starts the normal game.");
                page.Button("Abilities and ultimates", () => page.Open(Page + ".abilities"));
                page.Button("Perks and synergies", () => page.Open(Page + ".perks"));
                page.Button("Surge meter and shop", () => page.Open(Page + ".meter"));
                page.Button("Enemies", () => page.Open(Page + ".enemies"));
                page.Button("Controls and indicators", () => page.Open(Page + ".controls"));
                page.Button("Progression and records", () => page.Open(Page + ".progression"));
                page.Button("Stats, titles and history", () => page.Open(Page + ".stats"));
                page.Button("Teams and versus", () => page.Open(Page + ".teams"));
                page.Button("Settings and support", () => page.Open(Page + ".support"));
                return;
            }
            if (section == Page + ".abilities")
            {
                page.Title("Abilities and ultimates");
                page.Line("An ability and its ultimate share a cooldown. Using the ultimate usually means waiting longer before either can be used again. Unlock the ultimate through a perk before using it.");
                Ability(page, "Null Field", "immune_ability", "Become immune briefly. A shield present when you cast is kept only if you avoid hits during the effect.", "Neural Backup", "After a cast, revive a random dead player. In Survival, Astral ghosts count as dead, including your own ghost. A fully dead caster cannot revive themselves. If nobody is dead, summon friendly Wasps instead. In team versus, revives target another teammate.");
                Ability(page, "Bottomless Clip", "ammo_ability", "Shoot without spending ammo for a short time. Without Efficiency, most weapons disintegrate when the effect ends or you drop it.", "Care Package", "Spawn weapons around the map. Its upgrade places weapons at more existing spawn points.");
                Ability(page, "Kinetic Pulse", "pulse_ability", "Destroy nearby projectiles and push enemies and spiders away. Use it to clear space or push enemies into lava.", "Thermal Detonation", "A lethal pulse near the caster. Take care around teammates; team versus respects the friendly-fire setting.");
                Ability(page, "Pocket Dimension", "storage_ability", "Store the held weapon, retrieve the stored one, or swap them. The weapon beside your ring is the one in storage. Matching weapon perks determine whether storage survives a map change or death.", "Black Hole", "For 3 seconds, a black hole around you holds the enemies that were inside it when it opened, so they can't move, shoot or explode, and pulls them toward you along with every spider inside it. Whatever reaches its small inner circle is swallowed into the pocket dimension, teammates included, so they have to get away, or you away from them. An enemy is swallowed before it can touch you. Enemies far out or behind a wall may not make it in time and stay where it leaves them.");
                Ability(page, "Freeze Time", "freeze_ability", "Freeze nearby enemies and shots. Spiders and held weapons remain free. Frozen blades, saws, poison and shots still hurt on contact. Shots continue moving when thawed; overlapping freezes keep the longer remaining freeze.", "Rewind", "Turn nearby enemies into plain Wasps. Shielded enemies keep their shields.");
                return;
            }
            if (section == Page + ".perks")
            {
                page.Title("Perks and synergies");
                page.Line("Ability Cooldown strengthens the ability first, then the ultimate. Ability Duration strengthens the ability first, then the ultimate; its effect depends on the ability. Look for Bigger Pulse, Faster Retrieval or Deep Freeze.");
                page.Line("Short Term Investment strengthens the ability at the ultimate's expense. Long Term Investment strengthens the ultimate at the ability's expense.");
                page.Line("Luck can offer a higher-level perk before its lower level. Lucky upgrades are marked on the choice screen.");
                page.Line(Synergy("Null Field", "Shield perks", "Start Shields, Positive Encouragement or Safety Net can provide the shield that Null Field preserves if you avoid hits. Starting Neural Backup with a shield can give the revived player a shield, or summon an extra friendly Wasp."));
                page.Line(Synergy("Bottomless Clip", "Efficiency", "Without Efficiency, most weapons are consumed. Grenades, mines, Boomsticks, Laser Cubes and Death Cubes are exempt. The first level preserves its original ammo; the second also refills a held weapon to at least half capacity. Dropping it preserves its original ammo at either level."));
                page.Line(Synergy("Kinetic Pulse", "Explosion perks", "Bigger Boom first strengthens Kinetic Pulse knockback; its second level also strengthens Thermal Detonation knockback. Too Cool widens the ultimate's lethal area."));
                page.Line(Synergy("Pocket Dimension", "Weapon perks", "More Guns preserves guns, More Boom preserves explosives, throwables and mines, and More Particles preserves particle and melee weapons. The first level keeps them between rounds; the second also keeps them after death."));
                return;
            }
            if (section == Page + ".meter")
            {
                page.Title("Surge meter and shop");
                page.Line("The bar under the lives counter is the team's <b>Surge meter</b>. It fills from what your abilities do during a wave, and a full bar is one <b>chaos point</b>. It never resets between waves.");
                page.Line("Kinetic Pulse launching enemies, kills while Bottomless Clip or Null Field runs, Null Field blocking hits, the Pocket Dimension giving back a stored weapon, and Freeze Time freezing enemies and frozen enemies dying all charge it. Ultimates charge it too: enemies hit by Thermal Detonation, kills with Care Package weapons, Neural Backup revives and its Wasps' kills, Black Hole and Rewind.");
                page.Line("Every enemy a cast reaches counts. A chaos point takes more charge in a bigger team, as much more as its waves have more enemies, so everyone has to cast.");
                page.Line("Every perk screen shows the chaos points and the meter beside the cards, and under the cards the focused ability or ultimate says how it charges the meter.");
                page.Line($"Spend chaos points in the <b>shop</b> beside the cards at every perk choice after the starting ability pick: press right from the last card. Each screen sells two of these five, drawn at random: <color={Gold}>1</color> Reroll the cards, <color={Gold}>2</color> an extra life, <color={Gold}>3</color> a random perk you don't have, <color={Gold}>5</color> +1 max lives, and the <b>Blood Pact</b>, a life for a chaos point. Pick an item, then press <b>Buy</b> under it. Unspent chaos points carry on.");
                page.Line("With the perk timer off (the default), clicking a card only <b>locks</b> it. Press down to the <b>Confirm</b> button to make the pick. The starting ability choice works the same way.");
                return;
            }
            if (section == Page + ".enemies")
            {
                page.Title("Surge enemies");
                Enemy(page, "Twin Whisp", CustomEnemies.TwinWhispPrefab, "Two cores fire paired shots. Leave room to dodge both. The shielded variant needs its shield broken first.");
                Enemy(page, "Shielded Twin Whisp", CustomEnemies.ShieldedTwinWhispPrefab, "The paired shots remain dangerous while you work through its shield.");
                Enemy(page, "Missile Whisp", CustomEnemies.MissileWhispPrefab, "A flying turret-shaped enemy that fires rockets. Watch the blast area as well as the rocket. Its body is blue with yellow lights.");
                Enemy(page, "Shielded Missile Whisp", CustomEnemies.ShieldedMissileWhispPrefab, "Wears a pink shield bubble. Breaking it leaves a plain Missile Whisp, which still fires rockets.");
                Enemy(page, "Twin Blade Whisp", CustomEnemies.TwinBladeMeleeWhispPrefab, "Carries a double-ended particle blade. Both ends are dangerous, even if the enemy is frozen.");
                Enemy(page, "Power Twin Blade Whisp", CustomEnemies.TwinBladePowerMeleeWhispPrefab, "The stronger twin-blade variant. Keep clear of the weapon's reach.");
                Enemy(page, "Wasp Cocoon", CustomEnemies.WaspCocoonPrefab, "A stationary enemy that keeps hatching Wasps until destroyed, with a limit on its living offspring. Its Wasps must also die before the wave ends. Freeze Time pauses hatching.");
                Enemy(page, "Shielded Wasp Cocoon", CustomEnemies.ShieldedWaspCocoonPrefab, "Hatches Shielded Wasps while its own shield holds. Break the cocoon's shield to stop future offspring receiving that protection.");
                Enemy(page, "Laser enemies", CustomEnemies.LaserPrefabs.TryGetValue("LaserWhisp", out var laserWhisp) ? laserWhisp : CustomEnemies.LaserPrefabs.Values.FirstOrDefault(),"Red-shielded variants periodically show a faint warning ring and play a charge sound, followed by a lethal laser ring. Stay clear of the ring. Breaking the red shield removes the laser attack permanently. Exploding Rollers do not get this variant.");
                return;
            }
            if (section == Page + ".controls")
            {
                page.Title("Controls and indicators");
                page.Line($"Ability: {Controls.Display(Controls.AbilityKeyboard)} / {Controls.Display(Controls.AbilityController)}\nUltimate: {Controls.Display(Controls.UltimateKeyboard)} / {Controls.Display(Controls.UltimateController)}");
                page.Button("Change controls", () => page.Open(ControlsPage.Page));
                page.Line("To remap only your controller's ability, hold Select and double-tap the desired button. Hold Select for 3 seconds to reset it. Personal overrides last until you leave.");
                page.Line($"The ring drains as a timed ability or ultimate runs out, then fills clockwise as cooldown expires. <color={Hex(ModConfig.IndicatorAvailableColor)}>Ready</color>, <color={Hex(ModConfig.IndicatorActiveColor)}>active</color>, and <color={Hex(ModConfig.IndicatorCooldownColor)}>cooling down</color> use your chosen colors. The ready ring is complete. Active effects without a duration also keep a complete ring.");
                page.Line("Pocket Dimension shows the stored weapon beside the ring. No weapon means an empty slot. With Only show when ready enabled, the preview stays visible during cooldown only with the rings; the compact HUD hides the whole row until the ability is ready.");
                page.Line("Use compact ability HUD replaces the rings with a fixed panel at the bottom right. Each player has a named row: active time drains with a seconds countdown, cooldown progress fills with a seconds countdown, and Ready means the ability can be used. The same cooldown is shared with the ultimate. Both timers use game time and stop while paused.");
                page.Button("Ability indicator settings", () => page.Open("surge.indicator"));
                page.Line("Reduced visual effects softens decorative particles and flashes while keeping danger warnings visible. Find it on the Surge settings page.");
                return;
            }
            if (section == Page + ".stats")
            {
                page.Title("Stats, titles and history");
                page.Line($"Small stats: {Controls.Display(Controls.SmallWindow)}. Full scoreboard: {Controls.Display(Controls.BigWindow)}. Results titles: {Controls.Display(Controls.TitlesWindow)}. The small window can be dragged with the mouse; size, opacity and visible statistics live in Stats settings.");
                page.Line($"The full scoreboard opens on Default, the recommended preset. Customize its columns in Stats > Scoreboard presets. Combat, Enemy kills, Weapon kills, Accuracy and Movement are fixed presets; hide any you do not want in settings. Move through their pages with {Controls.Display(Controls.PreviousPresetKeyboard)} / {Controls.Display(Controls.NextPresetKeyboard)}, {Controls.Display(Controls.PreviousPresetController)} / {Controls.Display(Controls.NextPresetController)}, the arrows or the mouse wheel.");
                page.Line("The controller window button cycles the small window and Off during a match. In the lobby it also offers the full scoreboard and titles. A button assigned to an ultimate takes priority over scoreboard navigation. Tiny spiders identify players in the full scoreboard.");
                page.Line("Titles reward outstanding and unusual performances after Survival with two or more players. Only titles shown on the results screen enter the collection. Achievements > Titles explains how to earn them; All, Locked, Unlocked and Recently earned help you browse. Select an unlocked entry or use Mark all seen to clear its NEW marker. The results screen still remembers which titles were new that run.");
                page.Line("Match history keeps separate Survival and Versus records with player statistics, weapon accuracy, maps, perks and titles. It keeps every recorded match, with pages for browsing. Save match history needs Stats enabled; existing history remains available with Stats off. Saving history is independent of text reports and telemetry. Clear match history removes these records after confirmation.");
                if (Parts.Stats.Running) page.Button("Stats settings", () => page.Open("stats"));
                page.Button("Match history", () => page.Open("history"));
                return;
            }
            if (section == Page + ".teams")
            {
                page.Title("Teams and versus");
                page.Line("Enable Teams in Enable or disable parts, then restart the game. Team options appear in Versus Battle setup. Turn on Team versus, choose the team count and move players with the roster arrows; Shuffle and Balance help arrange the teams.");
                page.Line($"While team selection is open, switch your team with {Controls.Display(Controls.TeamKeyboard)} / {Controls.Display(Controls.TeamController)}. Team colors and markers identify teammates; Spawn teammates together groups their starting positions.");
                page.Line("Friendly fire has three choices: Push lets teammates knock each other back without killing or breaking shields; Ignore makes their hits do nothing; Damage lets their hits hurt normally. Teams share a score and win together.");
                page.Line("Give everyone an ability adds the same Surge ability to every player in versus. Choose an ability or choose Random to change it each round; ultimates and upgrades can be configured there too. Surge must be running. Matches that used this option carry a Surge abilities badge in history.");
                return;
            }
            if (section == Page + ".support")
            {
                page.Title("Settings and support");
                page.Line("Mods > SpiderSurge groups the settings for Stats, Surge, Controls and the enabled parts. Settings save automatically. Changes to enabled parts take effect after restarting; General can reload manual changes to the settings file.");
                page.Line("The Fixes part handles base-game problems such as Whisp cooldowns, controller reconnection, off-screen enemy arrows, pain-level color unlocks and the custom-maps warning. It works independently of Surge runs.");
                page.Line("About SpiderSurge > Support opens the logs, config and save folders. Copy diagnostic information copies versions and enabled-part status. Report a bug opens the issue form for you to fill in. These actions do not upload your logs or save data.");
                page.Line("Surge achievements, collected titles and highscores are kept in the mod's progress file. Match history is stored separately beside it, so retaining more matches does not expand that progress file. Resetting a collection and clearing history are separate actions.");
                page.Button("Support", () => page.Open("support"));
                return;
            }
            page.Title("Progression and records");
            page.Line("Choose the group's shared ability before wave 1, from as many random abilities as a normal perk choice offers. The arena waits until you choose, and the first earned perk choice offers normal upgrades. Its ultimate is guaranteed as an option at wave 30 and may appear in later choices until unlocked. At wave 60 you can unlock the ultimate or switch to another ability with its ultimate.");
            page.Line("Play without abilities removes ability, ultimate and ability-upgrade choices for the next Surge run. Luck remains available. Unlimited time to choose perks lets everyone take their time.");
            page.Line("Surge highscores are separate from normal Survival. Achievements and the title collection live under Mods > SpiderSurge > Achievements. Titles unlock when displayed after a Survival run with at least two players. NEW marks a first unlock.");
            page.Line("Match history has separate Survival and Versus pages under Mods > SpiderSurge. A Surge badge identifies Surge Survival; Surge abilities marks versus matches that used abilities. New matches are recorded while Stats and Save match history are enabled.");
        }

        private static void Ability(SettingsPage page, string name, string icon, string description, string ultimate, string effect)
        {
            page.Line($"<b>{name}</b>\n{description}\n<color={Gold}><b>{ultimate}</b></color>\n{effect}", illustration: IconLoader.GetIcon(icon));
        }

        private static void Enemy(SettingsPage page, string name, GameObject prefab, string description)
        {
            page.Line($"<b>{name}</b>\n{description}", enemy: prefab);
        }

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
