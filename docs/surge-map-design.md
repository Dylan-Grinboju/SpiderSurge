# Surge map design

The base game's black silhouettes, thin illuminated edges, subdued backgrounds and bright interactive objects are the reference. Its arenas usually have a strong overall shape with generous gaps for webs, thrown weapons and flying enemies. Symmetry is common, but it is not a requirement: the additions should offer useful movement choices rather than resemble detailed models of real structures.

The selection contains 14 arenas. Each earns its place through a recognizable silhouette or movement mechanic, rather than another arrangement of detached platforms.

| Arena | Distinct role |
|---|---|
| Aqueduct | Repeated arches support a broken upper deck, with cover and two hover pads underneath. |
| Zigzag | A continuous W of ramps creates alternating valleys and peaks. |
| Thin Ice | Breakable shelves disappear above a permanent recovery floor, whose jump pads fire up through the shelf gaps. |
| Cactus Garden | Exposed islands with lethal cacti make low footing hazardous. |
| Caldera | Outer slopes surround a central lava crater. |
| Trampoline | A deep bowl funnels players toward jump pads at its base. |
| Geysers | Air vents beneath floating shelves provide recovery from below. |
| Colonnade | A broad floor and uneven ruins offer grounded fighting and cover. |
| Jaws | Opposing toothed surfaces frame a horizontal arena with lava above and below; portals over the lip ends wrap one side to the other. |
| Crossbeams | Four separated diagonal arms leave an open central crossing; portals join each lower tip to the opposite upper tip. |
| Ladders | Broken climbing spines and alternating ledges allow vertical travel and side exits. |
| Pipeworks | Interrupted pipes form pockets with multiple exits; a vent lifts spiders through the central gap. |
| Satellite | Long wings divide upper and lower routes around a central body and dish, with drifting debris below. |
| Crescent | A large curved wall shelters one side while detached islands expose the other. |

Six overlapping layouts were removed from the previous 20-map selection:

| Removed | Reason |
|---|---|
| Fallen Blocks, Faultline, Slingshot | These and Crescent repeated the scattered-island arrangement. Crescent remains as the strongest silhouette; Trampoline already supplies jump-pad play. |
| Stairwell | Another sequence of detached footholds; Ladders gives climbing a clearer structure. |
| Truss Bridge | Its split bridge and upper crossing overlap the stronger architectural identity of Aqueduct. |
| Causeway | Its breakable crossing overlaps Thin Ice, which makes destruction the defining feature across several shelves. |

## Size

The custom map scene gives every map the same 930 × 530 camera box, about three times the area of a typical base-game arena (median 498 × 354 across the 34 arenas in the endless survival pool). The Surge maps therefore follow the base game's proportions instead:

- **Camera box:** each layout stores a box fitted to its colliders and vents, reaching 25 past the sides, 30 below and 65 above, the base-game arenas' median margins. Host and clients apply it when the map loads, so the camera no longer follows players into empty space or zooms out further than on a base-game arena.
- **Enemy spawns** lie inside that box, as they do on the base-game arenas. Before, they sat up to 110 units outside the outermost platforms.
- **Player spawns** are at most 500 apart; the widest base-game spread is 511.
- **Scale:** `surge_map(..., scale=f)` shrinks a whole arena toward (0, -150). Spawns, jump pads (whose force is fixed) and portals keep their size. Ten arenas are scaled from 0.75 to 0.9, so the selection runs from compact (Crescent 494 and Trampoline 511 wide) to large (Thin Ice 762), with a median of about 670 × 430.

| Arena | Scale | Camera box |
|---|---|---|
| Aqueduct | 1 | 749 × 527 |
| Zigzag | 0.9 | 742 × 400 |
| Thin Ice | 1 | 762 × 440 |
| Cactus Garden | 0.75 | 584 × 342 |
| Caldera | 0.85 | 670 × 409 |
| Trampoline | 0.75 | 511 × 420 |
| Geysers | 0.85 | 668 × 442 |
| Colonnade | 1 | 718 × 439 |
| Jaws | 0.9 | 630 × 460 |
| Crossbeams | 0.85 | 675 × 436 |
| Ladders | 0.8 | 530 × 407 |
| Pipeworks | 0.9 | 674 × 457 |
| Satellite | 1 | 718 × 509 |
| Crescent | 0.8 | 494 × 384 |

Jump pads keep their force on a scaled map, so they throw relatively higher: on Trampoline a pad launch peaks at y 117, level with the upper platforms and well inside the 236 camera top.

## Spawn supply

The vanilla endless survival pool contains 34 arenas, averaging 7 weapon points and 6.12 enemy points; 31 of those arenas have exactly 6 enemy points. Across all 55 vanilla arenas the averages are 7.04 weapons and 6.02 enemies. Counts include active, enabled weapon spawners and active objects tagged `EnemySpawn`, excluding lobby and parkour scenes.

Every Surge arena now has **6 regular weapon points, 1 rare weapon point and 6 enemy points**. Previously, twelve arenas had only 2 regular weapons and the other two had 3; enemy points numbered 7–8. The new weapon points cover lower recovery routes, intermediate footing and upper platforms. Trampoline gets two points at the foot of the bowl and two along its slopes, in addition to the upper pair and rare point. Thin Ice keeps two regular points over its permanent floor after the shelves break.

All fourteen existing geometries can support this supply without additional platforms. Weapon points stay at least 60 units apart; enemy points stay at least 90 apart and 60 from initial player spawns. The checker rejects insufficient counts, stacked points and missing weapon supply near the lowest player route, in addition to its existing footing, hazard, gadget and camera checks. An arena that cannot meet these requirements within its intended geometry should be removed from the roster.

## Gadgets

The map editor's gadgets give several arenas a movement idea beyond their silhouette. They are the game's own networked elements, so the host runs them and clients see the result.

| Gadget | Editor name | Behavior | Used on |
|---|---|---|---|
| Jump pad | Booster (`Jump Pad`) | Launches spiders and enemies along its up direction | Trampoline, Thin Ice |
| Vent | Aero Tube (`AeroTube`) | Slows anything entering its column and lifts it about 120 units per scale | Geysers, Pipeworks |
| Hover pad | Floating Platform (`FloatingPlatformWide Variant`) | Solid, without gravity; drifts when pushed or stood on and does not return to its place | Aqueduct, Satellite |
| Portal | Portal Point (`PortalPoint` with a linked `PortalPoint Variant`) | Moves spiders, enemies and loose weapons to the other end, keeping their velocity | Jaws, Crossbeams |

Portals make an arena harder: enemies can arrive through them, and a spider keeps its speed on the far side. They are therefore limited to non-beginner maps and placed so that each exit leads back into the arena:

- Every exit has solid, non-hazard footing within 80 units below, so falling into one end lands on ground at the other.
- Portals keep 70 units from player and weapon spawns, 60 from enemy spawns and 20 from solids, and stay away from the lava.
- On Jaws, a spider leaving one side moves inward from the other. On Crossbeams, sliding down a lower arm into its portal drops it onto the opposite upper arm.

Hover pads are never spawn footing, because they drift. Jump pads sit on solid ground away from spawns.

Base-game arenas remain in Surge's pool. These maps do not replace their geometry or change their behavior; normal Wave Survival and Versus keep their original pools.

![Geometry previews of the 14 retained Surge maps](images/surge-map-roster.png)

All 14 cards use consistent geometry previews, each framed to its map's camera box: native collider silhouettes, subdued biome colors, orange hazards, cyan ice, pad/vent markers, blue hover pads and yellow portal rings. These are diagrams, not gameplay captures; they omit scenery, weapons and players. Regenerating them from the layouts keeps the card's shape accurate after an edit.

## Authoring and validation

`surge/Maps/Tools/layouts.py` is the source for the embedded JSON. Extract current collider outlines with `shapes.py`, then run:

```powershell
python surge/Maps/Tools/shapes.py <scratch> --scenedump <workspace>/SceneDump
python surge/Maps/Tools/layouts.py --shapes <scratch>
python surge/Maps/Tools/thumbnails.py <scratch>
python tests/MapSpawns.Tests.py
```

`--shapes` is required, because the camera boxes are fitted to the collider outlines. The checker rejects unknown collider data, unsupported spawn positions, solid overlaps, hazard or hover-pad footing, insufficient enemy clearance, enemy spawns outside the camera box, player spawns more than 500 apart, unpaired portals, portals on beginner maps or without footing below their exits, and jump pads without footing. A failure returns a nonzero exit code before writing layouts. `--only id,id` validates and writes only those maps. `--preview` also draws collider diagrams and requires matplotlib.

The thumbnail renderer requires Pillow (available in the workspace's SceneDump environment), validates all maps before writing, and produces 320 × 180 PNGs. It changes only card artwork; the playable maps still use the game's actual prefab materials and backgrounds.

These are static placement checks. They do not establish reachability, pad trajectories, destruction behavior, how the camera frames play or online synchronization; those require gameplay observation. Changes to an arena also require a matching thumbnail, since the card should show its actual shape.
