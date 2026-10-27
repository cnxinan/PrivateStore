# Mixed Monster Encounter

## Approved scope

Extend the existing arena into one fixed encounter with two slimes, two fast
wolves and two skeleton archers. No automatic waves or replenishment. Clearing
all six shows a Chinese completion message prompting R to restart.

Use lightweight components within the current Data / Gameplay / Game boundaries:
the arena coordinates combat, movement and ranged behavior are separated from
encounter orchestration, projectiles have their own simulation, and existing
loot generation remains independent. Do not introduce ECS, behavior trees or
another framework.

Out of scope: maps, obstacles, pathfinding, monster-to-monster collision,
charging, fleeing, elites, bosses, new equipment, new affixes and saves.
Monsters may overlap and pass through each other.

## Encounter and configuration

Monster definitions configure behavior and combat values. An encounter JSON
defines the six spawn entries with stable instance IDs and world positions;
definition IDs are shared by instances of the same type. Each state instance
tracks its own health, behavior timers and one-time death/drop resolution.
Projectile state is not shared between archers.

Retain the 960 x 540 arena and player spawn at (160, 270). Initial monster
positions are slime (480, 150), (480, 390); wolf (650, 180), (650, 360);
archer (820, 150), (820, 390). All are inside the arena and initially beyond
archer range of the player. These positions are configuration, not hardcoded AI.

| Monster | Health | Move speed | Behavior | Damage |
|---|---:|---:|---|---:|
| Slime | 10 | 55 | Chase player | 3 on contact |
| Fast wolf | 20 | 140 | Chase player, no charge | 5 on contact |
| Skeleton archer | 20 | 70 | Approach outside range 260, otherwise stand and shoot | 5 per arrow |

Archers do not flee and do not deal contact damage. Player health 100, base
attack 10 and movement speed 180 remain unchanged; equipment still modifies
existing stats.

Validate behavior enums, required ranged parameters, finite numeric values,
positive health/speed, damage bounds, referenced monster/loot IDs, unique
instance IDs and in-arena spawn positions. Invalid configuration must report
the responsible definition/path and fail explicitly, not substitute defaults.
Keep existing definition constructors usable where reasonable; update old
single-monster consumers and fixtures explicitly rather than retaining
misleading single-monster state as the active encounter.

## Combat

Held melee keeps the existing range 48, half-angle 45 degrees and 0.35-second
cooldown, including missed swings. One accepted swing hits every living monster
inside that arc, at most once per instance. Roll criticals independently only
for actual hits. Keep bounded double damage and clamp applied damage to health.
Report per-target hit results so visual feedback cannot incorrectly reuse one
target's critical status for another. Preserve one swing animation per accepted
attack and rarity-colored ground loot display.

Slime/wolf contact uses the existing distance 24 and one player-wide 0.8-second
contact cooldown, not one cooldown per monster. If multiple monsters qualify
on the same update, stable encounter order chooses the first contact; subsequent
contacts are blocked until the cooldown expires. Arrows deal damage separately
and do not consume or bypass contact cooldown accounting.

## Ranged attacks

An archer outside range 260 approaches the player. Within range it stands still.
Its first shot, and every later shot, has a visible 0.3-second windup. Lock aim
at windup start; windup then completes even if the player leaves firing range.
Require at least 1.5 seconds between consecutive launches, including the next
windup. Do not burst-fire missed shots after a long update.

Arrows travel straight at speed 240 and never home. Spawn at the shooter's
position, move from there, and use swept movement collision against the player's
existing body radius 14 so a fast arrow cannot tunnel through the player.
Treat arrows as point projectiles for collision; the drawn shaft is cosmetic.
Deal 5 damage on the first player hit and remove the arrow. Remove arrows when
they leave arena bounds. Already launched arrows survive their shooter's death;
an unfinished windup is cancelled when that archer dies.

Resolve player death once, abandon the rest of that update's encounter damage,
and reset the encounter so stale contact/projectiles cannot damage the newly
respawned player.

## Drops

Replace the sample's guaranteed blade-plus-vest drops with the same shared
random table for all three types:

- Each death independently produces zero or one item with equal probability.
- Given a drop, blade and vest have equal weight.
- Common / Magic / Rare retain weights 1 / 4 / 2 and existing affix rules.
- Average yield per completed encounter is three items, not a guarantee.

Use existing inclusive MinDrops=0, MaxDrops=1 and two nonguaranteed equal-weight
entries; do not add another probability mechanism. Each monster resolves loot
once, including when several monsters die in the same swing. Ground items retain
their actual world pickup positions and the existing display-row collision
avoidance.

## Reset and pause

R resets all six monsters and behavior timers, clears arrows and ground items,
and resets player attack/contact cooldowns and hit feedback. Keep player position,
current health, equipment and inventory. This intentionally changes R from
retaining ground loot to clearing it.

Death restores the player to the original spawn with current maximum health,
keeps equipment and inventory, resets all six monsters/timers, and clears arrows
and ground loot.

Opening inventory pauses combat, enemy movement, projectiles, attack windups
and cooldowns. Closing resumes from that state. Inventory clicks remain
edge-triggered; held melee and the existing real Draw-based FPS remain intact.

## Presentation

Continue using generated shapes: green round slimes, distinct wolf silhouettes
and gray skeletal archers with visible bows/arrows. Differentiate by shape as
well as color. Each living monster has its own Chinese name and health bar.
Archers show a visible windup indicator toward the locked direction.

HUD displays remaining enemies out of six and a clear completion/restart prompt,
while retaining player stats, controls and FPS. Update the Chinese R description
to disclose that restarting clears ground loot. Extend the font repertoire for
all new rendered Chinese text and verify actual loaded glyphs and measured text
bounds. Preserve nonoverlapping ground loot labels.

## Acceptance and verification

Focused deterministic tests must cover:

- Six distinct instances in configured positions, duplicate-definition support,
  configuration validation and independent health/AI timers.
- Multi-target melee arc/range, one hit per target/swing, cooldown, independent
  critical rolls and no duplicate loot after death.
- Chase versus archer movement, windup direction lock, launch cadence, projectile
  hits/misses, swept collision, exit cleanup and shooter-death behavior.
- Shared contact cooldown, separate arrow damage and atomic death reset.
- Shared zero/one drop table, equal selectable entries, existing rarity/affix
  rules and item identity retained through pickup/equip.
- R cleanup and preserved player state; death cleanup and retained equipment;
  inventory pause of the entire simulation.

Extend real-window smoke with deterministic injected content/randomness to
exercise mixed enemies, ranged combat, a multi-kill, pickup/equipment,
completion, R, death, pause and actual Draw-based FPS. Do not assert guaranteed
sample drops: fixtures may force rolls for specific scenarios, while unit tests
verify the production probability table separately.

Release build must have no warnings; the full test suite, existing formatter,
loaded-font coverage and measured UI checks must pass. Injected-input graphics
smoke is not a claim of manual keyboard/mouse playtesting.
