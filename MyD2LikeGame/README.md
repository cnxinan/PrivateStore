# DarkLoot vertical slice

Windows, .NET 9 SDK, and a DirectX-capable desktop are required. No editor or save system.

```powershell
dotnet run --project src\DarkLoot.Game --configuration Release
dotnet test DarkLoot.sln
dotnet build DarkLoot.sln --configuration Release
dotnet publish src\DarkLoot.Game --configuration Release --output artifacts\publish
.\artifacts\publish\DarkLoot.Game.exe
```

## Controls

- **WASD** move; mouse aims; **left click** swings a short melee arc (click again for each attack).
- **E** picks up nearby gold loot. **I** opens/closes inventory and pauses combat.
- Click an item row to compare its stat deltas and equip it to its weapon or armor slot; mouse wheel scrolls.
- **R** restores the slime for another fight, retaining health, inventory, equipment and existing loot.
- **Escape** exits. At zero health the arena respawns, clears ground loot and keeps equipment.

Defeat the Training Slime for a guaranteed Training Blade and Training Vest. Loot rolls Common, Magic,
or Rare with zero, one, or two distinct affixes; generated values stay fixed on each item, and prefix/suffix affixes compose the display name. Equip both
slots to combine attack, maximum-health and critical-chance totals. The inventory shows each item's
rolled values and hypothetical changes to those totals. Equipping maximum health never heals, while
death restores current maximum health and retains all gear. Blue is the player; rarity colors mark loot,
and critical hits use a distinct orange-red combat flash.

## Architecture

- **Engine**: game-independent input snapshots, follow camera and generated rectangle/circle textures.
- **Data**: shared JSON definitions loader and validation.
- **Gameplay**: headless `ArenaSimulation`, weighted rarity/affix generation, two-slot equipment,
  shared stat totals, hit-only critical rolls and combat rules.
- **Game**: MonoGame host maps Engine input to `PlayerIntent`; renderers and inventory hit-testing contain no combat/drop rules.

Definitions and the compiled Arial 14-point bitmap SpriteFont are copied on build/publish.
Runtime paths resolve from `AppContext.BaseDirectory`, independent of the working directory.
MonoGame framework, pipeline and tools are pinned to 3.8.6. Building the font requires installed Arial.

Legacy item definitions default to the Weapon slot, and loot tables without `rarityWeights` produce
Common items. Item `attackBonus` remains between 0 and 1,000,000 inclusive. Affix ranges are inclusive,
bounded to +/-1,000,000 (critical chance to +/-100 percentage points), and every selectable rarity must
have enough eligible distinct affixes for every item slot in its table. Invalid references, slots, stats,
weights and ranges fail with the source file or loot-table ID. Combined attack and health totals are
bounded to `Int32`; critical chance is clamped to 0..100.

## Automated window smoke

```powershell
dotnet run --project src\DarkLoot.Game --configuration Release -- --smoke-test
Get-Content src\DarkLoot.Game\bin\Release\net9.0-windows\smoke-test.log
```

This opens a real graphics window and injects deterministic input snapshots through the host update/UI/draw
path. It exercises guaranteed weapon/armor drops, pickup, both equipment slots, comparison totals, stable
rolls on re-equip, restart, critical damage feedback, pause, contact damage and death restoration, then exits.
It uses accelerated simulation time, is bounded to 1,200 updates and returns nonzero on failure. Physical
keyboard/mouse input and human visual review remain unverified. Startup errors are written beside the
executable to `startup-error.log` (smoke failures to `smoke-test.log`).
