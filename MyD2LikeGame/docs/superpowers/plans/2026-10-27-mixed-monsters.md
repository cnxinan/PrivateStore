# Mixed Monsters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a six-enemy arena with slime/wolf melee, dodgeable skeleton arrows, area melee and independently resolved random drops.

**Architecture:** Keep simulation in Gameplay, configuration in Data and graphics/input composition in Game. ArenaSimulation coordinates instances and resets; MonsterBehaviorSystem owns AI decisions, ProjectileSystem owns arrows, and LootGenerator remains the only item-generation implementation.

**Tech Stack:** C#, .NET 9, MonoGame WindowsDX 3.8.6, System.Text.Json, xUnit.

## Global Constraints

- Approved specification: `docs\superpowers\specs\2026-10-27-mixed-monsters-design.md`.
- Work only in `C:\Users\v-ningchen\Source\repos\PrivateStore\.worktrees\darkloot-vertical-slice\MyD2LikeGame`.
- Gameplay must not reference MonoGame or Engine. Use System.Numerics.Vector2 there.
- No new dependencies, ECS, behavior trees, pathfinding, maps, bosses or equipment.
- Six configured instances; all production values and reset semantics match the specification.
- Preserve held input, 0.35-second swing cooldown, FPS, Chinese text and loot-label avoidance.
- R intentionally starts clearing ground loot. Player death retains owned equipment.
- Validate invalid configuration explicitly with source paths; never silently default unknown behavior.
- Do not push. Stage only named task files; do not use `git add .`.
- Existing uncommitted changes include HP10, held attack, FPS and loot layout. Preserve them.

## Starting state and execution discipline

The approved specification is committed as `29ef9c2`. Earlier gameplay changes
are dirty and were verified with 69 tests. Before implementation, inspect and
record `git status --short` and `git diff --stat`; these are the user's active
baseline, not changes to discard. Do not mix their unrelated files into task
commits. A changed file may contain both prior and new work: stage new hunks
deliberately or leave that task uncommitted and report the boundary.

Every task follows write test -> observe failure -> minimal implementation ->
observe pass -> review diff. Commands below run from the project directory.
Use `dotnet test ... --no-restore` only after a normal restore/build has succeeded;
restore/install only on a missing dependency failure or manifest change.

Each task commit, if cleanly separable, uses its listed message and trailers:

```text
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 62788275-ec02-4460-a11a-bd9349f184ea
```

## File and interface map

New Data files:

- `src\DarkLoot.Data\Definitions\MonsterBehavior.cs`: Chase / Archer enum.
- `src\DarkLoot.Data\Definitions\RangedAttackDefinition.cs`: range, speed,
  launch interval, windup and damage.
- `src\DarkLoot.Data\Definitions\EncounterDefinition.cs`: encounter ID and spawn
  list; each spawn has InstanceId, MonsterId, X, Y.
- `content\definitions\encounters\training-arena.json`: approved six spawns.
- `content\definitions\monsters\fast-wolf.json`, `skeleton-archer.json`.

New Gameplay files:

- `src\DarkLoot.Gameplay\Combat\MonsterBehaviorSystem.cs`: chase/ranged decisions.
- `src\DarkLoot.Gameplay\Combat\ProjectileState.cs`: read-only public arrow state.
- `src\DarkLoot.Gameplay\Combat\ProjectileSystem.cs`: motion, swept hits, cleanup.
- `src\DarkLoot.Gameplay\Combat\MeleeHit.cs`: immutable per-target swing result.

New tests:

- `tests\DarkLoot.Tests\Gameplay\MixedEncounterFixture.cs`: deterministic content.
- `tests\DarkLoot.Tests\Gameplay\MixedEncounterTests.cs`: melee, contact, loot/reset.
- `tests\DarkLoot.Tests\Gameplay\RangedCombatTests.cs`: AI, arrows and death.

Existing loader, states, arena, renderers, GameText, SpriteFont, smoke and README
are modified in the tasks below. No separate loot service or input framework.

## Task 1: Validated encounter and monster definitions

**Files:** New Data/configuration files above; modify
`src\DarkLoot.Data\Definitions\MonsterDefinition.cs`,
`src\DarkLoot.Data\GameContent.cs`, `src\DarkLoot.Data\GameContentLoader.cs`,
`content\definitions\monsters\training-slime.json`;
test `tests\DarkLoot.Tests\Data\GameContentLoaderTests.cs` and
`tests\DarkLoot.Tests\Data\ContentFixture.cs`.

**Produces these contracts** (record parameter defaults preserve old constructors):

```csharp
public enum MonsterBehavior { Chase, Archer }
public sealed record RangedAttackDefinition(
    float Range, float ProjectileSpeed, float FireInterval, float WindupSeconds, int Damage);
public sealed record MonsterSpawnDefinition(string InstanceId, string MonsterId, float X, float Y);
public sealed record EncounterDefinition(string Id, IReadOnlyList<MonsterSpawnDefinition> Spawns);
```

Append `MonsterBehavior Behavior = MonsterBehavior.Chase,
RangedAttackDefinition? RangedAttack = null` to MonsterDefinition. Append optional
`IReadOnlyDictionary<string, EncounterDefinition>? EncounterDefinitions = null`
to GameContent and expose a non-null `Encounters` view using the existing
AffixDefinitions/Affixes pattern. No implicit default encounter in the loader.

- [ ] Add loader regressions by extending ContentFixture's existing temporary
  JSON-directory creation. Use real JSON for an archer and encounter:

```json
{
  "id": "monster.skeleton_archer",
  "name": "骷髅弓手",
  "maxHealth": 20,
  "contactDamage": 0,
  "moveSpeed": 70,
  "lootTableId": "loot.training_slime",
  "behavior": "Archer",
  "rangedAttack": {
    "range": 260, "projectileSpeed": 240,
    "fireInterval": 1.5, "windupSeconds": 0.3, "damage": 5
  }
}
```

  Assert parsed enum, ranged values, six distinct instance IDs and repeated
  monster definition IDs. Parameterize malformed cases: unknown enum,
  missing ranged parameters for Archer, ranged parameters on Chase, nonfinite
  or nonpositive ranged values, windup greater than fire interval, negative
  damage, duplicate spawn ID, empty/null spawn list, missing monster reference
  and coordinates outside x=0..960/y=0..540. Errors contain the source path.
- [ ] Run `dotnet test tests\DarkLoot.Tests -c Release --filter FullyQualifiedName~GameContentLoaderTests --verbosity minimal`.
  Confirm new tests fail for missing encounters/validation, not malformed test setup.
- [ ] Load `encounters` through existing LoadDefinitions/LoadedDefinition path
  tracking. Validate enum with Enum.IsDefined; validate finite values explicitly.
  Add encounter references to the loader's cross-reference pass. Direct
  simulation construction will revalidate required encounter invariants in Task 2.
- [ ] Add production encounter `encounter.training_arena` with entries
  slime.1/slime.2 at (480,150)/(480,390), wolf.1/wolf.2 at
  (650,180)/(650,360), archer.1/archer.2 at (820,150)/(820,390).
  Monster IDs: `monster.training_slime`, `monster.fast_wolf`,
  `monster.skeleton_archer`. Wolf: HP20, speed140, contact5, Chase.
  Slime retains HP10/speed55/contact3. Use current loot ID until Task 4 replaces
  the table values; this task must still load and run the old encounter.
- [ ] Repeat loader command; run all Data tests. Review serializer compatibility
  and JSON output copy via the existing recursive Game.csproj content include.
- [ ] Commit separable files: `feat: add validated mixed encounter definitions`.

## Task 2: Multi-instance arena, chase and area melee

**Files:** Create MeleeHit, MonsterBehaviorSystem, MixedEncounterFixture and
MixedEncounterTests; modify `src\DarkLoot.Gameplay\Combat\MonsterState.cs`,
`ArenaSimulation.cs`, `PlayerState.cs`, existing `ArenaSimulationTests.cs`,
`src\DarkLoot.Game\DarkLootGame.cs`, `ArenaRenderer.cs`,
`RuntimeSmokeTest.cs`.

**Consumes:** Task 1 definitions and content. **Produces:**

```csharp
// ArenaSimulation
public ArenaSimulation(GameContent content, Random random, EncounterDefinition encounter);
public IReadOnlyList<MonsterState> Monsters { get; }
public IReadOnlyList<MeleeHit> LastSwingHits { get; }
public int RemainingMonsters { get; }
public bool IsCleared { get; }
// MeleeHit
public sealed record MeleeHit(string MonsterInstanceId, int Damage, bool WasCritical);
// MonsterState public readable properties
// string InstanceId, string Id, Vector2 Position, int Health, int MaxHealth,
// int ContactDamage, float MoveSpeed, MonsterBehavior Behavior, bool IsDefeated
```

Retain `ArenaSimulation(GameContent, Random)` as a production convenience
constructor that explicitly requires `encounter.training_arena` from Encounters;
no fallback to a hidden slime. Unit fixtures use the three-argument constructor.
Remove `ArenaSimulation.Monster`; migrate every consumer in this task.

MixedEncounterFixture exposes `CreateContent()` with 100-HP zero-contact chase
definitions, weapon/armor items, existing required affix definitions and a
zero-drop table; `CreateEncounter(params MonsterSpawnDefinition[] spawns)`
creates an encounter from explicit test spawns. Tests needing production values
load production JSON rather than treating fixture values as sample values.

- [ ] Add area-melee regression using two targets in front and one behind:

```csharp
var content = MixedEncounterFixture.CreateContent();
var encounter = MixedEncounterFixture.CreateEncounter(
    new("front.1", "monster.training_slime", 190, 265),
    new("front.2", "monster.training_slime", 190, 275),
    new("back", "monster.training_slime", 130, 270));
var arena = new ArenaSimulation(content, new Random(17), encounter);
arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), 0);
Assert.Equal(new[] { 90, 90, 100 }, arena.Monsters.Select(monster => monster.Health));
Assert.Equal(2, arena.LastSwingHits.Count);
arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), .1f);
Assert.Equal(new[] { 90, 90, 100 }, arena.Monsters.Select(monster => monster.Health));
```

- [ ] Run `dotnet test tests\DarkLoot.Tests -c Release --filter FullyQualifiedName~MixedEncounterTests --verbosity minimal`;
  confirm the new multi-instance contract fails before implementation.
- [ ] Replace the single monster with a read-only list created in spawn order,
  copying each spawn into a separate state. Retain the immutable definition on
  each state so loot and ranged parameters cannot be looked up against the
  wrong instance. Use an instance-local death-resolved flag.
- [ ] Move chase calculations into
  `MonsterBehaviorSystem.Update(MonsterState monster, Vector2 playerPosition, float elapsedSeconds)`.
  This task implements Chase and Archer approach/stand behavior; shooting is
  added to the same system in Task 3. Normalize safely and clamp to arena bounds.
- [ ] Iterate all living targets on an accepted swing. Clear LastSwingHits only
  on the next accepted swing/reset, not on held cooldown frames. Roll crits
  independently and expose actual bounded damage per target. Keep AttackSequence
  and existing LastHit fields temporarily as single-hit compatibility feedback
  for migrated tests, but active rendering uses per-target hits.
- [ ] Add tests for independent state despite shared definition IDs, stable
  melee ordering, 50% crit rolls driven by a scripted Random, range/arc misses
  consuming cooldown, and exactly one hit per target. Use target health100 so
  critical damage is observable without health clipping.
- [ ] Keep one player-wide contact timer. Iterate living Chase monsters in
  configured order; only the first eligible contact deals damage per interval.
  On death return immediately after respawn rather than continuing the loop.
  Add a test with two contact monsters at the player: damage is not summed.
- [ ] Migrate every `.Monster` consumer explicitly: initialize production arena
  from configured encounter; render health bars by iterating Monsters; compare
  LastSwingHits rather than old HP delta for hit feedback. Existing one-monster
  tests pass explicit one-spawn encounters and index their known fixture target.
  Smoke uses an explicit single-target fixture temporarily, never production
  fallback state. Task 5 replaces it with the mixed smoke.
- [ ] Run all Gameplay tests and `dotnet build DarkLoot.sln -c Release --no-restore --verbosity minimal`.
  Review all `.Monster` references using scoped rg to ensure no active single
  monster path remains. Commit: `feat: support multi-monster melee encounters`.

## Task 3: Telegraphing archers and swept arrows

**Files:** Create `ProjectileState.cs`, `ProjectileSystem.cs`,
`tests\DarkLoot.Tests\Gameplay\RangedCombatTests.cs`; modify
`MonsterState.cs`, `MonsterBehaviorSystem.cs`, `ArenaSimulation.cs`.

**Produces:**

```csharp
public sealed record ProjectileState(
    string SourceInstanceId, Vector2 Position, Vector2 Direction, float Speed, int Damage);
// ArenaSimulation
public IReadOnlyList<ProjectileState> Projectiles { get; }
// MonsterState (public read/internal write)
public bool IsWindingUp { get; }
public float WindupRemaining { get; }
public Vector2 LockedAimDirection { get; }
// ProjectileSystem, owned by ArenaSimulation
public IReadOnlyList<ProjectileState> Projectiles { get; }
public void Spawn(ProjectileState projectile);
public void Clear();
public bool Update(float elapsedSeconds, Vector2 playerPosition, Func<int, bool> applyDamage);
```

`applyDamage` returns true when damage caused respawn. Projectile Update then
clears and returns true immediately; otherwise it returns false. Remove a hit
projectile before calling the delegate. Do not mutate a collection being
enumerated by a reset callback.

Extend MonsterBehaviorSystem.Update with `Action<ProjectileState> launch` and
pass ProjectileSystem.Spawn from the arena. Use remaining-until-next-launch and
windup timers on each monster, never global archer state.

- [ ] Add behavior tests: player in range starts windup with no immediate arrow;
  moving sideways during the next .3 seconds does not change locked direction;
  launch occurs after .3, subsequent launches are at least1.5 apart.
  Outside range an idle archer approaches; inside range it stops. An active
  windup finishes outside range. Dead archer cannot finish windup.
- [ ] Add projectile regression at (0,270) traveling right at240 for1second
  against player (160,270): it must hit despite ending beyond the player.
  Separate miss at y=300, zero elapsed, starting inside radius14, exit cleanup,
  shooter death and two arrows causing one atomic death/reset.
- [ ] Run `dotnet test tests\DarkLoot.Tests -c Release --filter FullyQualifiedName~RangedCombatTests --verbosity minimal`.
  Observe absent launch/collision behavior fail.
- [ ] Implement windup start/lock and completion in MonsterBehaviorSystem.
  Initialize the first-shot timer to allow immediate windup, not immediate
  launch. Launch sets remaining interval to1.5. Begin next windup only when
  remaining interval is at most .3 and the target is within260.
  Consume elapsed across windup boundaries once; never emit multiple shots
  from one long update. Normalize coincident aim to a finite UnitX direction.
- [ ] Implement segment-circle hit by nearest point on arrow movement:

```csharp
var travel = end - start;
var lengthSquared = travel.LengthSquared();
var t = lengthSquared == 0 ? 0
    : Math.Clamp(Vector2.Dot(playerPosition - start, travel) / lengthSquared, 0f, 1f);
var nearest = start + travel * t;
var hit = Vector2.DistanceSquared(nearest, playerPosition) <= 14f * 14f;
```

  Check hits along the portion inside arena bounds, then remove out-of-bounds
  arrows. Keep directions immutable and velocity finite; no homing.
  Use the projectile's own damage, not the current source monster's damage.
- [ ] Define update ordering: reduce player cooldowns; move player/aim; accept
  melee and deaths; advance preexisting arrows; return immediately if player
  dies; update living monster movement/windups and enqueue new arrows; resolve
  contact with immediate death return; then pickup. Newly launched arrows start
  moving next update, avoiding a full-frame move before their launch time.
- [ ] Reset clears projectile storage and every monster's timers. Test already
  launched arrows survive shooter death until a hit, bounds exit or reset.
  Arrow damage is independent of the contact timer. Run ranged and mixed tests
  together, then build. Commit: `feat: add dodgeable telegraphed archer attacks`.

## Task 4: Production drops and encounter lifecycle

**Files:** Modify `content\definitions\loot-tables\training-slime.json`,
`ArenaSimulation.cs`, `MixedEncounterTests.cs`,
`tests\DarkLoot.Tests\Gameplay\LootGeneratorTests.cs`,
`tests\DarkLoot.Tests\Gameplay\ArenaSimulationTests.cs`.

**Consumes:** Per-instance definitions/death flags, ProjectileSystem.Clear,
existing LootGenerator.Generate. **Produces:** production lifecycle and shared
50% zero/one-item table, no new public loot API.

- [ ] Add deterministic table tests forcing inclusive NextInt64 draw count to
  zero then one; force each equal-weight entry selection in separate cases.
  Check zero or one output and correct item ID. Assert production JSON uses
  MinDrops0/MaxDrops1, two nonguaranteed entries of equal positive weight and
  rarity weights1/4/2. Keep existing rarity/affix generator tests.
- [ ] Add mixed multi-kill test with a fixture guaranteed one-item table:
  three defeated instances yield exactly3 items with distinct IDs; repeated
  held attacks yield no extras. Production probability is not weakened to make
  this test deterministic.
- [ ] Add R regression:

```csharp
var playerPosition = arena.Player.Position;
var playerHealth = arena.Player.Health;
var equippedWeapon = arena.Player.Inventory.EquippedWeapon;
arena.ResetEncounter();
Assert.Empty(arena.GroundItems);
Assert.Empty(arena.Projectiles);
Assert.Equal(arena.Monsters.Count, arena.RemainingMonsters);
Assert.Equal(playerPosition, arena.Player.Position);
Assert.Equal(playerHealth, arena.Player.Health);
Assert.Equal(equippedWeapon, arena.Player.Inventory.EquippedWeapon);
Assert.Empty(arena.LastSwingHits);
Assert.All(arena.Monsters, monster => Assert.False(monster.IsWindingUp));
```

  Arrange this with actual ground loot, active arrows and equipped inventory,
  not empty collections that make reset assertions vacuous. Add equivalent
  death test asserting player spawn/full current maximum health and retained
  owned item instances.
- [ ] Run `dotnet test tests\DarkLoot.Tests -c Release --filter "FullyQualifiedName~MixedEncounterTests|FullyQualifiedName~LootGeneratorTests|FullyQualifiedName~ArenaSimulationTests" --verbosity minimal`.
  Observe new R semantics and production drop assertions fail.
- [ ] Set the shared table to0..1, existing blade/vest equal weights and no
  Guaranteed flags, retaining rarity weights1/4/2. Resolve loot using each
  defeated instance's own LootTableId and position with its death-resolved flag.
  Preserve ground pickup coordinates and item instance rolls.
- [ ] Implement shared encounter reset for states/timers/arrows/ground loot,
  resetting hit feedback and contact/attack cooldowns. Death additionally
  restores player spawn, facing and current maximum health. Update old reset
  assertions and README claims rather than silently retaining old semantics.
- [ ] Run selected tests and build. Commit: `feat: finalize mixed encounter loot and reset rules`.

## Task 5: Chinese presentation, mixed runtime smoke and final acceptance

**Files:** Modify `src\DarkLoot.Game\ArenaRenderer.cs`, `DarkLootGame.cs`,
`HudRenderer.cs`, `GameText.cs`, `RuntimeSmokeTest.cs`,
`Content\Fonts\UIFont.spritefont`, `README.md`.
Existing loot row placement and FPS code remain intact.

**Consumes:** Monsters, LastSwingHits, Projectiles, windup state,
RemainingMonsters and IsCleared. **Produces:** playable mixed encounter with
automated graphics acceptance; no single-target production assumptions.

- [ ] Extend loaded-font/layout checks first with new GameText constants:

```csharp
public const string Enemies = "剩余怪物 {0}/{1}";
public const string Cleared = "已清场，按 R 重开（清空地面装备）";
```

  Update R controls text and add monster names from production definitions.
  Add measured cases for counter and completion prompt. Run smoke to observe
  missing glyph/layout checks fail before updating the atlas.
- [ ] Render all living monsters with their own name and health bar. Draw
  green round slimes, wolf body/ears, skeleton head/bow; switch on typed behavior
  plus monster definition ID where wolf/slime share Chase. Never infer AI from
  a presentation name. Render archers' locked-aim windup line and moving arrows.
- [ ] Replace global hit/critical parameters in ArenaRenderer.Draw with
  per-target results from LastSwingHits, gated by existing .18-second swing
  flash. Keep one player swing arc; draw target damage/critical feedback near
  each affected monster, including targets just killed. Preserve dead instance
  positions until encounter reset. A later miss must not show old hit results.
- [ ] Render remaining/total count without covering FPS/player stats, and
  completion prompt when IsCleared even if surviving arrows are still in flight.
  Clear visual hit feedback when R or death resets. Inventory pauses the
  simulation by skipping Arena.Update, retaining windup/arrow/cooldown state.
- [ ] Replace old single-target smoke stages with deterministic mixed scenarios.
  Add a smoke content factory within RuntimeSmokeTest that returns GameContent
  and EncounterDefinition copies; normal startup always uses unmodified loaded
  production content. Explicit fixture controls may force high HP/crit/drop
  outcomes; do not edit production JSON from the smoke test.
  Use an explicit scenario enum and bounded update timeout with stage names:

```csharp
private enum SmokeStage
{
    Approach, MultiHit, DodgeArrow, TakeArrow, ClearEncounter, Pickup,
    Equip, PauseWithArrow, RestartCleanup, DeathRespawn, Complete
}
```

  Assert actual traces, not just stage progress: multiple distinct target hits
  in one swing; real arrow existed and missed/hit appropriately; six defeated;
  two different equipment slots picked/equipped with fixed rolls; opening the
  real inventory freezes every monster, arrow and player health for20 updates;
  R clears nonempty arrows/ground loot while retaining gear; death restores
  six monsters and current player maximum health. If one long scenario makes
  setup brittle, use explicit named fixture scenarios in the same smoke run.
- [ ] Integrate revised smoke with DarkLootGame.Initialize and input override,
  leaving physical input and production seed behavior unchanged. Extend atlas
  only for required new characters; retain null DefaultCharacter and existing
  measured layout checks. Update Chinese README with types, controls, 50% drop
  rule and R cleanup warning.
- [ ] Run final commands in order, stopping on each failure:

```powershell
dotnet build DarkLoot.sln -c Release --no-restore --verbosity minimal
dotnet test tests\DarkLoot.Tests -c Release --no-build --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
dotnet run --project src\DarkLoot.Game -c Release --no-build -- --smoke-test
Get-Content src\DarkLoot.Game\bin\Release\net9.0-windows\smoke-test.log -Tail 30
dotnet publish src\DarkLoot.Game -c Release --no-restore -o artifacts\publish
$smoke = Start-Process -FilePath .\artifacts\publish\DarkLoot.Game.exe -ArgumentList '--smoke-test' -PassThru -Wait
if ($smoke.ExitCode -ne 0) { throw "Published smoke failed: $($smoke.ExitCode)" }
Get-Content artifacts\publish\smoke-test.log -Tail 30
git --no-pager diff --check
```

  Require zero build warnings, all tests passing, format exit0, no missing
  glyphs, readable measured layout, positive actual Draw FPS, and every named
  mixed-smoke assertion PASS in both normal output and publish output.
  Published smoke uses a specifically tracked process and waits for exit before
  inspecting its exit code and fresh log.
- [ ] Directly review the changed interfaces, timing, death short-circuit,
  probability table and dirty-baseline preservation. Update plan checkboxes
  only after evidence exists. Commit separable final changes:
  `feat: render and verify mixed monster combat`.

## Plan self-review and handoff

Coverage mapping: definitions/spawns/errors -> Task1; independent instances,
area crit/chase/contact -> Task2; windup/projectiles/death atomicity -> Task3;
probability/identity/reset -> Task4; HUD/Chinese/shape differentiation/pause/
graphics/publish -> Task5. No new dependencies or planned automatic push.

Dependencies are sequential: 1 -> 2 -> 3 -> 4 -> 5. This is one continuous
combat integration, not independent subsystems to implement concurrently.
Use inline execution with checkpoints as the economical default if the user
chooses it; delegate only if explicitly selected.

Planning does not authorize implementation. Present this plan for approval and
offer inline execution or task-specific subagents; stop until the user chooses.
