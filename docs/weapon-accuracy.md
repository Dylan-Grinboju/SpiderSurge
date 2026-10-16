# Shooting accuracy

Stats records **shots that hit / shots fired**, both overall and separately for each shooting weapon. The overall percentage uses the combined shot counts rather than averaging weapon percentages. No shots is displayed as `--`; fired shots with no hits is `0%`.

A shot succeeds when the game's existing damage path reaches a living enemy, or an opposing spider in versus. Shield hits, armor/strut hits and effective knockback count. A shot can succeed only once, even if it hits multiple targets, breaks a shield and later kills, ricochets, or damages something over several frames. Self hits, survival friendly fire, team friendly fire, friendly wasps, scenery and loose items do not improve accuracy. Immune enemies and damage rejected by the existing damage receivers do not count.

## Findings from the game

The Steam build's `MyScriptAssembly` and weapon prefabs were inspected alongside `stats/Patches/AttackPatches.cs`, `stats/Tracking/HitLogic.cs` and `stats/Tracking/PlayerTracker.cs`.

The existing `WeaponHits` totals describe credited kills and shield breaks. One shot can contribute several such events, so dividing those totals by ammo spent would give misleading accuracy, including values above 100%. Input presses are also unsuitable: some presses fail cooldown checks, Railvolver fires while held, Auto Shotgun schedules several volleys, and charge weapons fire later.

| Weapons | One counted shot | Capture point |
|---|---|---|
| Shotgun, Baby Shotgun | One emitted pellet volley | `Shotgun.ShootClientRpc` particle playback |
| Auto Shotgun | One volley; five per normal burst | Same shared shotgun emission path |
| Railvolver, Double Railvolver | One projectile; two per double-barrel firing | `Railvolver.ShootProjectile` instantiation |
| Laser Cannon | One complete beam activation | Successful cooldown change in `LaserCannon.Use` |
| Death Ray | One charged discharge | `DeathRay.Fire` |
| Staff of Khepri | One launched energy ball | `KhepriStaff.LaunchProjectile` instantiation |
| Hecksaw, Gravity Saw | One launched disc | Existing Stats disc-spawn patch |
| Rocket, Grenade, Firework and Particle Blade Launchers | One launched projectile | `ProjectileLauncher.Shoot` instantiation |
| Burst Launcher | One rocket; six per normal firing | Each instantiation in `BurstProjectileLauncher.LaunchProjectile` |

Thrown grenades, mines, cubes, melee weapons, staff melee zaps and Surge abilities are outside this shooting statistic. The Particle Blade Launcher counts its launched blade, rather than ordinary held-blade attacks.

## Attribution

`AccuracyTracker` attaches a shot record to the actual spawned object using a `ConditionalWeakTable`. This captures the shooter and launcher category at firing time and avoids instance-ID reuse, scene-wide object scans and fixed expiration windows. Parent lookup associates a launched blade's force field with its projectile.

The same shot record follows a projectile into its own explosion, including grenade-launcher grenades and rail-impact blasts. It remains valid after the shooter drops the gun or dies. Reflected rail shots and energy balls lose their original shooting attribution; reflecting does not manufacture another fired shot. Picking up a launched grenade or blade ends its original projectile attribution. Independently armed explosives and chain reactions do not inherit shooting accuracy.

Hits reuse the attack scopes and guarded receivers already used by kill credit, along with its shared teammate classification. Accuracy is recorded on contact, independently of eventual kill credit. A shared success flag prevents repeated callbacks from increasing the numerator. Resetting the player stats replaces the shot table and counters, so projectiles from an earlier session cannot change a new session's accuracy. Hit-credit cleanup at the start of a versus round clears in-flight shot attribution while preserving the match's accuracy totals. Turning Stats off also clears the shot table.

### Shotgun particles

Unity's [particle-collision callback](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/monobehaviour/onparticlecollision) groups collisions by particle system and target within a frame. It is not a callback for each pellet. Reusing one emitter for overlapping volleys therefore cannot identify the firing that produced a hit.

The Stats host creates a separate copy of the configured shot emitter for each volley, using the existing position, rotation, pellet configuration and collision script. That gives each volley its own shot record, without counting individual pellets as shots. The copied emitter is destroyed after its duration and maximum particle lifetime. Clients and gameplay outside an active Stats session retain the original playback path. This adds transient particle-system objects while Stats is active and is the main implementation tradeoff to review.

Shot capture patches the actual instantiation or playback sites. The IL rewriters report a warning if the expected sites disappear. Beam activation compares state before and after the original call, using Harmony's [prefix/postfix state mechanism](https://harmony.pardeike.net/v2/articles/patching-injections.html).

## Display and saved stats

Under **Stats settings > Player table columns > Shooting accuracy**, enable overall accuracy or the desired weapon columns. All are optional and disabled by default. YAML keys are `stats.display.bigUI.columns.accuracy.overall` and `stats.display.bigUI.columns.accuracy.<SerializationWeaponName>`, for example `Railvolver` or `LaserCannon`.

Saved run statistics include overall and per-weapon percentages plus raw successful/fired counts, regardless of column visibility. Existing kill and weapon-hit counters keep their meanings. Accuracy is tracked on the host; this does not introduce client-side stats synchronization.

## Validation

Built against the bundled game references with `dotnet build --no-restore`. Live tests used the Steam build and SpiderInspector in paused local sessions, with frozen targets and isolated projectile cleanup between cases.

- Railvolver hit followed by a miss: `1/2`, `50%`.
- One rail penetrating two wasps: `1/1`, `100%`.
- Reset while a rail is in flight: its later hit leaves the new totals at `0/0`, `--`.
- Emission counts checked for every weapon in the table, including five Auto Shotgun volleys, two Double Railvolver shots and six Burst Launcher rockets.
- Real target hits checked through shotgun pellets, sustained Laser Cannon shield break and kill, Death Ray, rocket explosion, saws, energy ball and launched blade.
- A grenade fired by Grenade Launcher retained its shot record through `ExplodeClientRpc`.
- Rail-impact blasts retained their projectile's shot record.
- Survival friendly fire stayed at zero successful shots; a versus opponent hit counted.
- A damaging teammate hit in a three-player team match stayed at `0/1`, `0%`.
- Saved run files contained the overall and per-weapon percentages and raw counts.
- F2 overall and per-weapon columns inspected visually, including percentages and `--` cells.
- Original and accuracy builds both produced 30 live pellets after two overlapping Auto Shotgun volleys; the accuracy build used two separate 15-pellet emitters.
