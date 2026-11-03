# Weapon artwork

## Repulsor and Poison Grenade

These sprites adapt **2D Guns** by **Kay Lousberg**, published under **CC0**:

- Creator's asset page and license declaration: <https://opengameart.org/content/2d-guns>
- Original archive: <https://opengameart.org/sites/default/files/guns_gameassets.zip>
- License: <https://creativecommons.org/publicdomain/zero/1.0/>
- Retrieved November 1, 2026. The source page explicitly permits personal and commercial use without attribution.

The Repulsor uses the source pack's pistol body and grip, with a new flared emitter and ready vents. The Poison Grenade uses its rounded grenade shell and lever, rotated and reshaped with new vents and fuse lights. Both use a simplified gray palette to match SpiderHeck, with blue accents on the Repulsor and poison-green vents on the Poison Grenade. Small screws, highlights and sights were removed where unnecessary.

Editable SVGs are in `surge/Weapons/Art/`. Their same-size transparent PNG exports are embedded from `surge/Weapons/`. `repulsor_ready` and `poison_grenade_armed` are aligned overlays driven by the game's ready and fuse toggles; the base sprites remain visible when those lights are off. The SVG canvas, pivot and pixels-per-unit must stay aligned with the body and overlay together.

The CC0 attribution above applies to these source-derived assets, not to SpiderHeck's original game assets or the other weapon sprites.
