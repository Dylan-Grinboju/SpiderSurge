using System.Linq;
using SpiderSurge.Enemies;
using SpiderSurge.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiderSurge
{
    public static class SurgeGuide
    {
        public const string Page = "surge.guide";
        private const string Gold = "#FFD24A";

        public static void Build(SettingsPage page)
        {
            page.Title("How to play");
            string section = page.Current;
            if (section == Page)
            {
                page.Line("Learn the essentials, then explore each system. Use the stick or D-pad to move through the guide, or scroll with the mouse. Back returns to the topic list.");
                page.Button("Start here: your first Surge run", () => page.Open(Page + ".start"));
                page.Button("Abilities and ultimates", () => page.Open(Page + ".abilities"));
                page.Button("Perks and synergies", () => page.Open(Page + ".perks"));
                page.Button("Chaos points and challenges", () => page.Open(Page + ".chaos"));
                page.Button("Enemies", () => page.Open(Page + ".enemies"));
                page.Button("Controls and indicators", () => page.Open(Page + ".controls"));
                page.Button("Progression and records", () => page.Open(Page + ".progression"));
                page.Button("Stats, titles and history", () => page.Open(Page + ".stats"));
                page.Button("Teams and versus", () => page.Open(Page + ".teams"));
                page.Button("Settings and support", () => page.Open(Page + ".support"));
                return;
            }
            if (section == Page + ".start")
            {
                page.Title("Your first Surge run");
                page.Line(Section("1. Pick the mode", "In the lobby, stand on the Surge Survival platform above and left of Wave Survival. Bring the whole group onto it for the countdown, then choose a pain level. Wave Survival starts the original mode."));
                page.Line(Section("2. Choose an ability", "Before wave 1, choose the group's shared ability. Selecting a card locks it; move down to Confirm to start. Read the ability's effect before confirming."));
                page.Line(Section("3. Cast, then recharge", $"Ability: {Controls.Display(Controls.AbilityKeyboard)} / {Controls.Display(Controls.AbilityController)}. Use it to survive. Watch the ring beside your spider or the compact HUD: a full ready indicator means you can cast again. The ultimate shares that cooldown."));
                page.Line(Section("4. Spend chaos points", "Choose marked maps and complete optional challenges to earn team chaos points. At later perk choices, move right from the last card to visit the shop. Choose an item and then Buy; unspent points carry forward."));
                page.Line(Section("5. Build around your ability", "Choose cooldown, duration or synergy perks to strengthen your build. The ultimate is guaranteed as an option at wave 30 and can appear afterwards until unlocked; Pain 8 starts with it. Wave 60 offers an ultimate or a switch to another ability with its ultimate."));
                page.Line(Section("6. Find help and tune the game", "Pause and open Mods > SpiderSurge. Hover or select any setting for its explanation. How to play is always available here. Surge changes effects, sound and the ability indicator. Settings save automatically."));
                return;
            }
            if (section == Page + ".abilities")
            {
                page.Title("Abilities and ultimates");
                page.Line("An ability and its ultimate share a cooldown. Using the ultimate usually means waiting longer before either can be used again. Unlock the ultimate through a perk before using it.");
                Ability(page, "Null Field", "immune_ability", "Become immune briefly. A shield present when you cast is kept only if you avoid hits during the effect.", "Neural Backup", "After a cast, revive a random dead player. In Survival, Astral ghosts count as dead, including your own ghost. A fully dead caster cannot revive themselves. If nobody is dead, summon friendly Wasps instead. In team versus, revives target another teammate.");
                Ability(page, "Bottomless Clip", "ammo_ability", "Shoot without spending ammo for a short time. Without Efficiency, most weapons disintegrate when the effect ends or you drop it.", "Care Package", "Spawn weapons around the map. Its upgrade places weapons at more existing spawn points.");
                Ability(page, "Kinetic Pulse", "pulse_ability", "Push enemies, spiders and large projectiles such as rockets away. Small shots fly on. Use it to clear space or push enemies into lava.", "Thermal Detonation", "Kills enemies close to the caster and knocks back the rest of the pulse. Take care around teammates; team versus respects the friendly-fire setting.");
                Ability(page, "Pocket Dimension", "storage_ability", "Store the held weapon, retrieve the stored one, or swap them. The weapon beside your ring is the one in storage. Matching weapon perks determine whether storage survives a map change or death.", "Black Hole", "For 3 seconds, a black hole around you holds the enemies that were inside it when it opened, so they can't move, shoot or explode, and pulls them toward you along with every spider inside it. Whatever reaches its small inner circle is swallowed into the pocket dimension, teammates included, so they have to get away, or you away from them. An enemy is swallowed before it can touch you. Enemies far out or behind a wall may not make it in time and stay where it leaves them.");
                Ability(page, "Freeze Time", "freeze_ability", "Freeze nearby enemies, other players and shots. Walls block the freeze. The caster remains free; frozen players cannot move, jump, shoot, swap weapons or activate abilities until they thaw. Frozen blades, saws, poison and shots still hurt on contact. Shots continue moving when thawed; overlapping freezes keep the longer remaining freeze.", "Rewind", "Turn nearby enemies into plain Wasps. Shielded enemies keep their shields.");
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
                page.Line(Synergy("Kinetic Pulse", "Explosion perks", "Bigger Boom first strengthens Kinetic Pulse knockback; its second level also strengthens Thermal Detonation knockback. Too Cool widens the ultimate's lethal area. Bigger Pulse's second level widens only its knockback."));
                page.Line(Synergy("Pocket Dimension", "Weapon perks", "More Guns preserves guns, More Boom preserves explosives, throwables and mines, and More Particles preserves particle and melee weapons. The first level keeps them between rounds; the second also keeps them after death."));
                page.Line(Synergy("Freeze Time", "Slow-motion perks", "Adrenaline, Blademaster and Cool Explosions make Freeze Time and Rewind recharge faster during their slow motion. Overlapping effects do not stack. Pauses and menus do not count."));
                return;
            }
            if (section == Page + ".chaos")
            {
                page.Title("Chaos points and challenges");
                page.Line("Chaos points belong to the team. The counter in the top HUD briefly grows and glows gold when points are earned. Spend them in the shop at map choices; unspent points carry forward. Ability use does not earn points.");
                page.Line("Each map choice has a <b>75% chance</b> to mark one card with <b>+1 chaos point</b>. Choose that map to earn it. Rerolls keep the bonus on the same card slot.");
                page.Line($"Each shop offers two items: <color={Gold}>1</color> Reroll, <color={Gold}>2</color> Extra Life, <color={Gold}>3</color> Mystery Perk, <color={Gold}>5</color> Bigger Heart (+1 maximum life and +1 life), or Blood Pact (one life for one point). Each item can be bought <b>once per visit by the team</b>. Rerolling does not restock it.");
                page.Line("After choosing a map, there is a 50% chance of an optional challenge. It lasts until the next map choice, including retries. Rewards arrive before the next shop. The host holds Accept briefly or presses Skip for the team. Release the previous press first. Skipping has no penalty; recent offers are avoided. Clean Hands is offered only with teammates.");
                foreach (ChallengeKind kind in new[] { ChallengeKind.PoisonedHeart, ChallengeKind.DoubleCooldown, ChallengeKind.NoRegularAbilities, ChallengeKind.LifeWager, ChallengeKind.NoUltimate, ChallengeKind.Overrun, ChallengeKind.CleanHands })
                    page.Line(Section(ChaosRules.Name(kind) + $" · +{ChaosRules.Reward(kind)}", ChaosRules.Description(kind)));
                page.Line("Press right from the last perk card to enter the shop, select an item, then select Buy. A perk's synergy is explained below the cards.");
                page.Line("After buying Mystery Perk, its shop slot reveals the perk's icon, name and description for the rest of that visit.");
                page.Line("With the perk timer off (the default), clicking a card only <b>locks</b> it. Press down to the <b>Confirm</b> button to make the pick. The starting ability choice always works this way.");
                page.Line("Use Menu, Escape, or the controller's Menu button during a perk choice to open settings or leave. Resume returns to the current choice. Press up from a card to reach Menu with a controller.");
                return;
            }
            if (section == Page + ".enemies")
            {
                page.Title("Surge enemies");
                Enemy(page, "Twin Whisp", CustomEnemies.TwinWhispPrefab, "Two cores fire paired shots. Leave room to dodge both. The shielded variant needs its shield broken first.");
                Enemy(page, "Shielded Twin Whisp", CustomEnemies.ShieldedTwinWhispPrefab, "The paired shots remain dangerous while you work through its shield.");
                Enemy(page, "Missile Whisp", CustomEnemies.MissileWhispPrefab, "A flying turret-shaped enemy that fires rockets. Watch the blast area as well as the rocket. Its charcoal body has bright yellow lights.");
                Enemy(page, "Shielded Missile Whisp", CustomEnemies.ShieldedMissileWhispPrefab, "Wears a pink shield bubble. Breaking it leaves a plain Missile Whisp, which still fires rockets.");
                Enemy(page, "Twin Blade Whisp", CustomEnemies.TwinBladeMeleeWhispPrefab, "Carries a double-ended particle blade. Both ends are dangerous, even if the enemy is frozen.");
                Enemy(page, "Power Twin Blade Whisp", CustomEnemies.TwinBladePowerMeleeWhispPrefab, "The stronger twin-blade variant. Keep clear of the weapon's reach.");
                Enemy(page, "Wasp Cocoon", CustomEnemies.WaspCocoonPrefab, "A stationary enemy that keeps hatching Wasps until destroyed, with a limit on its living offspring. Its Wasps must also die before the wave ends. Freeze Time pauses hatching.");
                Enemy(page, "Shielded Wasp Cocoon", CustomEnemies.ShieldedWaspCocoonPrefab, "Hatches Shielded Wasps and sends out small purple waves that gently repel nearby spiders and loose objects periodically while its shield holds. Break the shield to stop the pulses and future offspring receiving that protection.");
                Enemy(page, "Laser enemies", CustomEnemies.LaserPrefabs.TryGetValue("LaserWhisp", out var laserWhisp) ? laserWhisp : CustomEnemies.LaserPrefabs.Values.FirstOrDefault(),"Red-shielded variants periodically show a faint warning ring and play a charge sound, followed by a lethal laser ring. Its hits can also trigger grenades, just like the Laser Cannon. Stay clear of the ring. Breaking the red shield removes the laser attack permanently. Khepris and Exploding Rollers do not get this variant.");
                return;
            }
            if (section == Page + ".controls")
            {
                page.Title("Controls and indicators");
                page.Line($"Default ability: {Controls.Display(Controls.AbilityKeyboard)} / {Controls.Display(Controls.AbilityController)}\nUltimate: {Controls.Display(Controls.UltimateKeyboard)} / {Controls.Display(Controls.UltimateController)}");
                page.Header("Current controller bindings");
                bool anyController = false;
                foreach (var input in PlayerInput.all)
                {
                    if (!input.devices.Any(device => device is Gamepad)) continue;
                    anyController = true;
                    var player = Players.All?.FirstOrDefault(p => Players.IsLocal(p) && p.playerInputIndex.Value == input.playerIndex);
                    string name = player != null ? MatchHistoryPage.Text(Players.NameOf(player)) : "Controller " + (input.playerIndex + 1);
                    page.Line($"<b>{name}</b> · Ability: {Controls.PathLabel(PlayerControlSettings.GetPlayerAbilityButton(input.playerIndex))}");
                }
                if (!anyController) page.Note("Join with a controller to see its current ability button here.");
                page.Line($"To move your controller's ability to another button, hold Select and double-tap that button. Release Select after remapping. To reset, start a fresh Select hold for 3 seconds to go back to {Controls.Display(Controls.AbilityController)}. Your choice lasts until you leave.");
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
                page.Line($"Choose the starting scoreboard in Settings > Stats. Customize My scoreboard columns in Customize scoreboard. Combat, Enemy kills, Weapon kills, Accuracy and Movement are fixed presets; hide any you do not want in settings. Move through their pages with {Controls.Display(Controls.PreviousPresetKeyboard)} / {Controls.Display(Controls.NextPresetKeyboard)}, {Controls.Display(Controls.PreviousPresetController)} / {Controls.Display(Controls.NextPresetController)}, the arrows or the mouse wheel.");
                page.Line("The controller window button cycles the small window and Off during a match. In the lobby it also offers the full scoreboard and titles. Tiny spiders identify players in the full scoreboard.");
                page.Line("Titles reward outstanding and unusual performances after Survival with two or more players. Only titles shown on the results screen enter the collection. Collection > Titles explains how to earn them; All, Locked, Unlocked, Recently earned and New help you browse. New shows only unseen unlocks, also counted in the Collection menu. Select an unlocked entry or use Mark all seen to clear its NEW marker. The results screen still remembers which titles were new that run.");
                page.Line("Surge results include your final build, wave reached, completed challenges, Chaos earned and spent, and new team achievements above the title cards. The same recap is saved in Match history when recording is on.");
                page.Line("Match history keeps separate Survival and Versus records with player statistics, weapon accuracy, maps, perks and titles. It keeps every recorded match, with pages for browsing. New matches are recorded while Keep match history is on. Saving history is independent of text reports and telemetry. Clear match history removes these records after confirmation.");
                if (Parts.Stats.Running) page.Button("Stats settings", () => page.Open("stats"));
                page.Button("Match history", () => page.Open("history"));
                return;
            }
            if (section == Page + ".teams")
            {
                page.Title("Teams and versus");
                page.Line("Teams is always loaded. Team options appear in Versus Battle setup. Turn on Team versus, choose the team count and move players with the roster arrows; Shuffle and Balance help arrange the teams.");
                page.Line($"While team selection is open, switch your team with {Controls.Display(Controls.TeamKeyboard)} / {Controls.Display(Controls.TeamController)}. Team colors and markers identify teammates; Spawn teammates together groups their starting positions.");
                page.Line("Friendly fire has three choices: Push lets teammates knock each other back without killing or breaking shields; Ignore makes their hits do nothing; Damage lets their hits hurt normally. Teams share a score and win together.");
                page.Line("Give everyone an ability adds the same Surge ability to every player in versus. Choose an ability or choose Random to change it each round; ultimates and upgrades can be configured there too. Surge must be running. Matches that used this option carry a Surge abilities badge in history.");
                return;
            }
            if (section == Page + ".support")
            {
                page.Title("Settings and support");
                page.Line("Mods > SpiderSurge > Settings groups Stats, Surge, and Records and privacy. Advanced contains game fixes, update checks and manual config reload. Only Game fixes can be turned off, after a restart. Settings save automatically.");
                page.Line("The Fixes part handles base-game problems such as Whisp cooldowns, controller reconnection, off-screen enemy arrows, pain-level color unlocks and the custom-maps warning. It works independently of Surge runs.");
                page.Line("About SpiderSurge > Support opens the logs, config and save folders. Copy diagnostic information copies versions and enabled-part status. Report a bug opens the issue form for you to fill in. These actions do not upload your logs or save data.");
                page.Line("Surge achievements, collected titles and highscores are kept in the mod's progress file. Match history is stored separately beside it, so retaining more matches does not expand that progress file. Resetting a collection and clearing history are separate actions.");
                page.Button("Support", () => page.Open("support"));
                return;
            }
            page.Title("Progression and records");
            page.Line("Choose the group's shared ability before wave 1, from as many random abilities as a normal perk choice offers. The arena waits until you choose, and the first earned perk choice offers normal upgrades. Its ultimate is guaranteed as an option at wave 30 and may appear in later choices until unlocked; Pain 8 starts with it. At wave 60 you can unlock the ultimate or switch to another ability with its ultimate.");
            page.Line("Play without abilities removes ability, ultimate and ability-upgrade choices for the next Surge run. Luck remains available. Unlimited time to choose perks lets everyone take their time.");
            page.Line("Surge highscores are separate from normal Survival. Achievements and the title collection live under Mods > SpiderSurge > Collection. Titles unlock when displayed after a Survival run with at least two players. NEW marks a first unlock.");
            page.Line("Match history has separate Survival and Versus pages under Mods > SpiderSurge. A Surge badge identifies Surge Survival; Surge abilities marks versus matches that used abilities. New matches are recorded while Keep match history is on.");
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
