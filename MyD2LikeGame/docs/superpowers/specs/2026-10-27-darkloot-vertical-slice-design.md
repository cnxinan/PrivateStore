# DarkLoot Vertical Slice Design

## Goal

Create the first runnable Windows desktop slice of a single-player, top-down loot-focused ARPG in `MyD2LikeGame`. The slice validates the shared-data and gameplay foundations before adding the independent content editor described in the broader project proposal.

## Scope

The player can move around a small arena, aim and attack a pursuing monster, defeat it, collect its equipment drop, and equip that item. The equipped item changes the displayed attack stat and subsequent damage. The first slice uses generated placeholder graphics and a small HUD; it does not depend on external art.

Included: one player, one monster type, directional melee, health and contact damage, one equipment definition and loot table, pickup, inventory/equipment UI, JSON content loading, and focused automated tests.

Excluded: Avalonia editor, content builder, Tiled integration, skills, affix combinations, save/load, quests, multiple maps or monster types, advanced lighting, and production art. These remain possible later milestones rather than prerequisites for the playable slice.

## Architecture

Use a solution with separated assemblies so gameplay rules remain testable without a graphics device:

```text
MyD2LikeGame/
├── DarkLoot.sln
├── Directory.Build.props
├── global.json
├── src/
│   ├── DarkLoot.Game/       # Windows app entry point and MonoGame host
│   ├── DarkLoot.Engine/     # Input, camera, collision/rendering primitives
│   ├── DarkLoot.Gameplay/   # Combat, inventory, equipment, loot rules
│   └── DarkLoot.Data/       # Definitions, JSON serialization and validation
├── tests/
│   └── DarkLoot.Tests/      # Headless gameplay and data tests
└── content/
    └── definitions/        # Sample JSON definitions consumed by the game
```

`Game` composes the other runtime modules. `Gameplay` depends on `Data` but not MonoGame. `Engine` owns MonoGame-facing input and rendering support. `Data` has no UI or rendering dependency. The initial editor and content-builder projects are deliberately postponed.

The available SDK is .NET 9.0.320. Pin the repository SDK and target a Windows desktop framework compatible with the selected MonoGame package; validate package/framework compatibility during implementation before fixing package versions.

## Gameplay and data flow

At startup, the game loads JSON definitions and reports invalid or missing content with the file and definition context. WASD moves the player; the mouse aims; left-click performs a directional melee attack; `E` picks up nearby loot; `I` opens the inventory, where clicking an item equips it.

The monster pursues the player and deals contact damage. Combat resolves through framework-independent gameplay services. On defeat, the monster requests a drop from the loot generator; the resulting item instance is placed in the arena. Pickup transfers the instance into inventory. Equipping it updates the player's aggregated attack stat, which is used by later attacks. Definitions use stable IDs and are distinct from runtime item instances. Randomness is injectable so tests can reproduce loot outcomes.

The first arena, characters, equipment, and drop are rendered from simple generated shapes using MonoGame's 2D drawing path. No external art or visual editor is required.

## Errors and validation

Malformed JSON, missing referenced IDs, and invalid gameplay values must produce actionable errors rather than silently substituting success-shaped defaults. Gameplay services remain independently callable so combat, equipment changes, and loot generation can be tested without launching the window.

Focused tests cover attack damage and hit handling, equipment stat changes, inventory transfer, and deterministic loot generation. The solution must build and its tests must pass. Manual acceptance is a complete loop in the launched game: move, kill the monster, see and collect the drop, equip it, and observe the attack stat/damage change.

## Repository integration

The target directory is ignored by the parent `PrivateStore` repository. Add only a narrow `.gitignore` exception for `MyD2LikeGame/` so the new project and this specification can be versioned without changing unrelated ignore rules or parent-project contents.
