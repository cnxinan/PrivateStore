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
- Click a weapon row to equip; mouse wheel scrolls long inventories.
- **R** restores the slime for another fight, retaining health, inventory, equipment and existing loot.
- **Escape** exits. At zero health the arena respawns, clears ground loot and keeps equipment.

Defeat the 20-HP slime, collect its guaranteed Training Blade and equip it to raise attack from 10 to 15.
Blue is the player; green is the slime; cyan swings and gold hits provide attack feedback.

## Architecture

- **Engine**: game-independent input snapshots, follow camera and generated rectangle/circle textures.
- **Data**: shared JSON definitions loader and validation.
- **Gameplay**: headless `ArenaSimulation`, combat, inventory/equipment and loot rules.
- **Game**: MonoGame host maps Engine input to `PlayerIntent`; renderers and inventory hit-testing contain no combat/drop rules.

Definitions and the compiled Arial 14-point bitmap SpriteFont are copied on build/publish.
Runtime paths resolve from `AppContext.BaseDirectory`, independent of the working directory.
MonoGame framework, pipeline and tools are pinned to 3.8.6. Building the font requires installed Arial.

Item `attackBonus` must be between 0 and 1,000,000 inclusive (`ItemDefinition.MaxAttackBonus`).
The loader reports invalid values with their source path and field; equipment also rejects invalid
in-memory definitions. Attack damage remains an `int`: a base damage whose sum with the equipped bonus
exceeds `Int32.MaxValue` is rejected with `ArgumentOutOfRangeException` for `baseDamage`.

## Automated window smoke

```powershell
dotnet run --project src\DarkLoot.Game --configuration Release -- --smoke-test
Get-Content src\DarkLoot.Game\bin\Release\net9.0-windows\smoke-test.log
```

This opens a real graphics window, injects deterministic input snapshots into the same host update/UI/draw
path, exercises movement, kill/drop/pickup, click-to-equip, restart, increased damage, contact and death,
then exits. It uses accelerated simulation time, is bounded to 1,200 updates and returns nonzero on failure.
It does not replace human visual review or test physical OS input polling. Startup errors are written beside
the executable to `startup-error.log` (smoke failures to `smoke-test.log`).
