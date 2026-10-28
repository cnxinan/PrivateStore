# Procedural Animation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add readable procedural actor animations, bilateral hit reactions and delayed player respawn to the existing six-monster encounter.

**Architecture:** Gameplay owns authoritative hitstun, knockback, immunity and death state. A small Engine pose calculator and a Game presentation controller consume snapshots/events without mutating combat. Renderer draws evaluated poses; Update is the only time source.

**Tech Stack:** C#, .NET 9, MonoGame WindowsDX 3.8.6, System.Numerics, existing xUnit suite.

## Global Constraints

- Binding approved specification: `docs\superpowers\specs\2026-10-28-procedural-animation-design.md`, commit `398bdeb`.
- Active project: `C:\Users\v-ningchen\Source\repos\PrivateStore\.worktrees\darkloot-vertical-slice\MyD2LikeGame`.
- Player: hitstun0.15s, knockback18 units, hit immunity0.5s, death0.6s, respawn immunity0.8s.
- Monsters: hitstun0.12s, knockback10 units, death visual0.3s, no hit immunity.
- Preserve swing0.35s, archer windup0.3s/launch interval>=1.5s, arrow speed240 and current production drops.
- Gameplay must not depend on Engine, Game or MonoGame.
- No added dependencies, generic animation framework, particles, camera shake or global hit-stop.
- Backpacks and window inactivity freeze combat and presentation; Draw never advances animation.
- Preserve Chinese/font checks, actual Draw FPS, held attack, inventory and ground-label avoidance.
- R while alive clears reaction/immunity/animation state without healing; R while dead does nothing.
- No implementation is authorized until the user chooses execution. Never push without a new request.

## Baseline and working method

The linked worktree is on `feature/darkloot-vertical-slice`. Last feature
commit604b3e8 was pushed and verified with120 tests and normal/published smoke.
The design-only commit398bdeb is local. Check status and rerun baseline tests at
execution start; preserve any new user changes. Do not create a nested worktree.

For each task: write regression -> run RED -> implement -> run GREEN -> inspect
diff -> commit named task files only. Do not weaken existing assertions to match
accidental behavior. Explicitly update assertions whose immediate-respawn,
uninterrupted movement or damage cadence expectations are intentionally changed.
No probabilistic/flaky assertions. Record command/output evidence.

Each task commit includes:

```text
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 62788275-ec02-4460-a11a-bd9349f184ea
```

## File responsibilities and public contracts

New Gameplay files:

- `src\DarkLoot.Gameplay\Combat\HitReactionState.cs`: remaining stun and smooth
  displacement; no sprite offsets or color.
- `src\DarkLoot.Gameplay\Combat\CombatEvent.cs`: small typed per-Update event
  record, not a general event bus.

New Engine files:

- `src\DarkLoot.Engine\Animation\ActorAnimator.cs`: deterministic pose calculation
  for Player, Slime, Wolf and Archer; no references to gameplay entities.
- `src\DarkLoot.Engine\Animation\ActorPose.cs`: neutral numeric input/output
  records and enums used by the calculator.

New Game files:

- `src\DarkLoot.Game\ActorPresentation.cs`: one animator per InstanceId plus
  player, converts combat snapshots/events to pose input; cleans up on ResetSequence.
- `src\DarkLoot.Game\ActorRenderer.cs`: shape-local actor drawing, separated
  from ArenaRenderer's terrain/loot/HUD coordination.

New tests:

- `tests\DarkLoot.Tests\Gameplay\HitReactionTests.cs`.
- `tests\DarkLoot.Tests\Gameplay\PlayerLifecycleTests.cs`.
- `tests\DarkLoot.Tests\Engine\ActorAnimatorTests.cs`.

Modify existing ArenaSimulation, PlayerState, MonsterState,
MonsterBehaviorSystem, ProjectileSystem, DarkLootGame, ArenaRenderer,
ShapeRenderer, GameText, RuntimeSmokeTest, UIFont.spritefont and README in the
tasks assigning their behavior below. Existing tests already reference Engine;
do not reference the executable Game project solely to test pose math.

## Task 1: Authoritative bilateral reactions and combat events

**Files:** New HitReactionState/CombatEvent/HitReactionTests; modify
`src\DarkLoot.Gameplay\Combat\ArenaSimulation.cs`, `PlayerState.cs`,
`MonsterState.cs`, `MonsterBehaviorSystem.cs`, `ProjectileSystem.cs`,
`tests\DarkLoot.Tests\Gameplay\MixedEncounterTests.cs`,
`tests\DarkLoot.Tests\Gameplay\RangedCombatTests.cs`.

**Contracts produced:**

```csharp
public sealed class HitReactionState
{
    public float RemainingSeconds { get; private set; }
    public bool IsActive => RemainingSeconds > 0;
    public void Begin(Vector2 direction, float distance, float duration);
    public Vector2 Advance(float elapsedSeconds);
    public void Clear();
}
public enum CombatEventKind { Swing, Damaged, ContactAttack, ArrowLaunched, Died }
public sealed record CombatEvent(
    CombatEventKind Kind, string ActorId, Vector2 Position,
    Vector2 Direction, int Damage, bool WasCritical);
```

Player and Monster expose `HitReactionState Reaction { get; }`.
Player exposes `float InvulnerabilityRemaining { get; internal set; }` and
`bool IsInvulnerable`; monster exposes last accepted facing/direction where
required for visuals. Arena exposes read-only `Events` cleared at Update start,
and locked swing direction from its Swing event. Actor ID `"player"` is reserved;
monster event actor IDs are existing InstanceIds.

ProjectileSystem damage callback changes from `Func<int,bool>` to
`Func<ProjectileState,bool>` so source direction and damage cannot be guessed
from current source position. Keep the bool as “stop current damage processing”;
in Task2 death no longer resets/clears arrows immediately. Migrate every callback
and test directly; no redundant compatibility overloads.

- [ ] Add isolated reaction curve test:

```csharp
var reaction = new HitReactionState();
reaction.Begin(Vector2.UnitX, 18f, .15f);
var displacement = reaction.Advance(.05f)
    + reaction.Advance(.05f) + reaction.Advance(.05f);
Assert.Equal(18f, displacement.X, 3);
Assert.Equal(0f, displacement.Y);
Assert.False(reaction.IsActive);
Assert.Equal(Vector2.Zero, reaction.Advance(1f));
```

  Add equivalent monster10/.12 test, restarting reaction discards unfinished
  displacement, split vs whole elapsed gives equal result, zero/invalid times,
  finite direction normalization and stable coincident-source fallback.
- [ ] Add arena tests using existing MixedEncounterFixture: two slimes at
  (190,265)/(190,275), player(160,270),100HP fixture targets. One swing hits both,
  and neither moves until reaction advance; total accepted knockback is10 each.
  Stunned monsters cannot chase/contact/wind up. Archer interrupt cancels
  pending shot; after recovery it needs full.3 windup and preserves>=1.5 cadence.
- [ ] Add real arrow/contact cases: one accepted player hit makes health drop
  once, starts.15 stun/.5 immunity, and source direction produces18 displacement.
  A second arrow in that update is removed without damage or control extension.
  Immunity expiry permits a later hit; zero damage does not animate/control.
  Existing shared.8 contact cooldown still limits actual contact attacks.
- [ ] Run RED:
  `dotnet test tests\DarkLoot.Tests -c Release --filter FullyQualifiedName~HitReactionTests --verbosity minimal`.
  Expect missing state/behavior failures, not malformed fixtures.
- [ ] Implement displacement as successive eased progress differences rather
  than repeated full offsets. Use `1 - (1-t)*(1-t)` with t clamped0..1. Clamp
  actual entity position to the arena after applying each delta.
  Advance timers only for actors already reacting at frame start; a newly hit
  actor must not consume the entire frame as if hit at its beginning.
  Block voluntary movement/attack/pickup for the portion covered by stun.
  Cooldowns continue; do not generate catch-up swings.
- [ ] Centralize accepted player damage so immunity checks, health changes,
  event emission and reaction initialization cannot disagree between arrows and
  contact. Contact timer starts only after actual accepted positive damage.
  Build the entire melee target snapshot before applying damage/knockback.
  Emit one Swing per accepted swing, one Damaged per actual hit and ContactAttack
  only for a successful contact; ArrowLaunched emitted from the real launch path.
  Ignore zero damage as a new reaction. Died is emitted exactly once.
- [ ] Replace interrupted archer windup without resetting last-launch history.
  Stun freezes action, not launch cooldown; recovery requires complete new
  windup. Existing arrows remain alive.
- [ ] Run GREEN focused + mixed/ranged tests; build solution. Update legacy
  tests that assume chasing during stun with explicit before/after timing.
  Commit `feat: add bilateral hit reactions and damage immunity`.

## Task 2: Delayed death, protected respawn and control gates

**Files:** Modify ArenaSimulation, PlayerState, ProjectileSystem,
`src\DarkLoot.Game\DarkLootGame.cs`, `InventoryPanel.cs`,
`RuntimeSmokeTest.cs`; create PlayerLifecycleTests and update existing
ArenaSimulationTests/MixedEncounterTests/RangedCombatTests.

**Consumes:** reaction/immunity state and Events. **Produces:**

```csharp
// PlayerState
public bool IsDead => Health == 0;
public float DeathRemaining { get; internal set; }
// ArenaSimulation
// Existing ResetSequence changes only when a reset/respawn really occurs.
// ResetEncounter() is a no-op while dead.
```

- [ ] Add lifecycle regression using actual lethal contact from a fixture
  monster (not direct field mutation):

```csharp
arena.Update(idle, 0);
Assert.True(arena.Player.IsDead);
Assert.Equal(.6f, arena.Player.DeathRemaining, 3);
var deathPosition = arena.Player.Position;
arena.Update(new PlayerIntent(Vector2.UnitX, new Vector2(900, 270), true, true), .3f);
Assert.True(arena.Player.IsDead);
Assert.Equal(deathPosition, arena.Player.Position);
arena.ResetEncounter();
Assert.True(arena.Player.IsDead);
arena.Update(idle, .3f);
Assert.False(arena.Player.IsDead);
Assert.Equal(arena.Player.MaximumHealth, arena.Player.Health);
Assert.Equal(new Vector2(160, 270), arena.Player.Position);
Assert.Equal(.8f, arena.Player.InvulnerabilityRemaining, 3);
```

  Define `idle` locally as a zero-movement/no-attack PlayerIntent aimed at
  player position; fixture spawn includes a contactDamage>=100 monster at
  player position and a second distant chase monster to observe world movement.
- [ ] Add death-world test: a distant monster advances and an existing arrow
  moves during death; arrow crossing dead player continues instead of being
  consumed. No duplicate damage/death event. Verify owned equipped instances and
  rolls survive respawn; ground items/arrows and all six AI states reset.
- [ ] Run RED:
  `dotnet test tests\DarkLoot.Tests -c Release --filter FullyQualifiedName~PlayerLifecycleTests --verbosity minimal`.
- [ ] Replace immediate RespawnAfterDeath with EnterDeath and timer-driven
  CompleteRespawn. Newly entered death does not consume that same Update's
  elapsed duration; return from remaining player damage processing immediately.
  Subsequent dead Updates advance world without live-player collision. Extend
  ProjectileSystem.Update with explicit `bool playerAlive` argument rather than
  passing NaN or a fake position to suppress collision.
  On timer expiry reset and return without consuming old input, then set.8
  immunity after reset so reset cannot erase protection.
- [ ] Gate Player.TryEquip/Arena.TryEquip while dead, plus normal control paths.
  In DarkLootGame suppress new inventory toggles while dead and ignore R there
  and in the simulation. Keep Escape polling available. Reset clears reaction,
  immunity and feedback; no-op dead R emits no ResetSequence.
  Preserve live inventory R behavior. A live R should not grant immunity.
- [ ] Update existing deterministic smoke's immediate-death stage to wait for
  explicit death and later respawn, with bounded timeout and positive evidence.
  Do not remove prior assertions. Later Task5 adds detailed animation scenarios.
- [ ] Run lifecycle/mixed/ranged tests, full suite and build. Commit
  `feat: add delayed player death and protected respawn`.

## Task 3: Deterministic procedural pose calculator

**Files:** Create Engine ActorAnimator/ActorPose and ActorAnimatorTests.
No Gameplay or renderer edits in this task.

**Contracts produced** (System.Numerics.Vector2):

```csharp
public enum ActorVisualKind { Player, Slime, Wolf, Archer }
public enum ActorAction { Idle, Move, Attack, Hurt, Dead }
public readonly record struct ActorAnimationInput(
    bool Moving, bool Hurt, bool Dead, bool Invulnerable,
    bool WindingUp, float WindupProgress,
    bool AttackTriggered, Vector2 Facing);
public readonly record struct ActorPose(
    Vector2 Offset, Vector2 Scale, float Rotation, float Opacity,
    float WhiteMix, float LimbSwing, float AttackProgress, Vector2 Facing);
public sealed class ActorAnimator
{
    public ActorAnimator(ActorVisualKind kind);
    public ActorPose Pose { get; }
    public ActorAction Action { get; }
    public void Update(ActorAnimationInput input, float elapsedSeconds);
    public void Reset();
}
```

Single animator per actor; not a graph, clip registry or data-driven DSL.
AttackTriggered is a one-Update impulse; WindingUp is continuous authoritative
state. Store the accepted attack's Facing while its visual lasts. Archer launch
uses AttackTriggered for short recoil; windup priority below Hurt/Dead.

- [ ] Add pose tests with real inputs: idle and movement at two different
  phases produce distinct but bounded poses; static repeated reads return
  identical pose; Update time partitions give matching phase. Example:

```csharp
var animator = new ActorAnimator(ActorVisualKind.Slime);
var input = new ActorAnimationInput(true, false, false, false, false, 0, false, Vector2.UnitX);
animator.Update(input, .05f);
var first = animator.Pose;
animator.Update(input, .05f);
Assert.NotEqual(first, animator.Pose);
var second = animator.Pose;
Assert.Equal(second, animator.Pose);
```

  Add death middle/end tests (monster.3/player.6), priority overrides,
  hit/attack only start once per impulse, fixed swing direction during mouse
  movement, recovery from hurt, bounded immunity brightness and Reset neutral.
- [ ] Run RED:
  `dotnet test tests\DarkLoot.Tests -c Release --filter FullyQualifiedName~ActorAnimatorTests --verbosity minimal`.
- [ ] Implement small named curve constants: idle breathing, movement oscillation,
  slime squash, wolf/skeleton limbs, player.18 swing, archer.12 recoil, hurt
  flash/compression and death fade. Use smooth falloff and low amplitude;
  pose scales stay positive until death finishes and opacity remains0..1.
  Idle/movement pose blending returns smoothly toward neutral when stopping.
  Finite elapsed/input validation throws explicitly; clamp valid progress0..1.
  Dead is terminal until Reset; dead opacity remains0 after completion, without
  restarting on repeated Dead input. Hurt overrides attacks and disallows
  stale attack replay on recovery.
- [ ] Run focused tests + all Engine tests. Commit
  `feat: add deterministic actor pose animations`.

## Task 4: Actor presentation and animated geometry integration

**Files:** Create Game ActorPresentation/ActorRenderer; modify DarkLootGame,
ArenaRenderer, Engine ShapeRenderer and existing RuntimeSmokeTest.

**Consumes:** Task1 events, Task2 lifecycle, Task3 input/pose. **Produces:**

```csharp
// Game presentation controller, no direct mutation of arena:
public void Update(ArenaSimulation arena, float elapsedSeconds);
public ActorPose PlayerPose { get; }
public ActorPose GetMonsterPose(string instanceId);
public void Reset(ArenaSimulation arena);
```

ActorPresentation holds per-instance previous position and animators; player
has its own entry. Moving derives from real voluntary movement, not screen/camera
motion or knockback; use reaction/death state to exclude those changes.
Consume Events once after each simulation Update. ResetSequence change clears
all entries and prior hit text; initialize to current state without replaying
events from the pre-reset world. Presentation snapshots stay readable while paused.

- [ ] Extend real smoke with failed precondition assertions for pose change
  during walking, attack direction retained, two monsters' phases/events isolated,
  partial death geometry still visible, and frozen nonneutral pose during pause.
  Keep scenario timeout errors naming the missing condition.
- [ ] Run smoke RED after build; verify it fails due to absent pose behavior.
  No golden screenshots or test-only fake animation timers.
- [ ] Integrate presentation immediately after each successful Arena.Update,
  using the same elapsed. When backpack open skip both; if R changed ResetSequence
  while paused call presentation.Reset without advancing time. Ignore both on
  inactive window. Draw only reads poses, including while paused.
- [ ] Move actor shape rendering into ActorRenderer and transform local geometry:

```csharp
var localScaled = localPoint * pose.Scale;
var sin = MathF.Sin(pose.Rotation);
var cos = MathF.Cos(pose.Rotation);
var rotated = new Vector2(
    localScaled.X * cos - localScaled.Y * sin,
    localScaled.X * sin + localScaled.Y * cos);
var screenPoint = worldScreenAnchor + pose.Offset + rotated;
```

  Add only required ShapeRenderer primitive (ellipse/rotated rectangle) if local
  line/point geometry cannot express the animation cleanly; do not add a scene
  graph. Opacity affects every body part, eyes, limbs and weapon consistently.
  Hurt WhiteMix is applied before opacity. Death bodies render until fade ends,
  without names/bars; living names/bars remain on the actual world anchor.
- [ ] Draw distinct existing actor shapes with new limbs, player aimed weapon
  swing, slime squash, wolf contact impulse and archer bow draw/recoil. Arrow
  telegraph uses authoritative locked direction; do not animate its collision
  coordinates. Monster death does not move loot, delay drop or change count.
  Preserve terrain, actual projectiles, FPS and loot rows.
- [ ] Keep damage text associated with the actual per-target accepted hit and
  reset it on R/respawn; no repeated event on frames without new simulation.
  Run full smoke with existing mixed combat assertions intact, then build and
  tests. Commit `feat: render animated actors and combat reactions`.

## Task 5: Lifecycle/pause acceptance, Chinese feedback and final review

**Files:** Modify RuntimeSmokeTest, GameText, HudRenderer,
`src\DarkLoot.Game\Content\Fonts\UIFont.spritefont`, README;
extend HitReactionTests/PlayerLifecycleTests/ActorAnimatorTests only for gaps
found in scenario coverage.

**Consumes:** all prior contracts. **Produces:** verified normal/published
builds, explicit death/respawn UI, updated controls and limitations.

- [ ] Add Chinese death/revival feedback with countdown and disabled-controls
  explanation; no healthbar can suggest dead player is alive. New text must be
  included in GameText reflection and extreme measured-layout cases.
- [ ] Run smoke RED for missing glyphs/new expectations, then add only required
  font characters and fitting layout. No fallback glyph.
- [ ] Add bounded deterministic real-window scenarios:
  player hurt with active knockback; second arrow ignored/removed; interrupted
  archer recovers; monster death fades while loot/count already updated; actual
  lethal death lasts.6; dead R/toggle do nothing; monsters/arrows continue;
  respawn preserves gear and protects for.8.
  Each setup must produce actual nonempty events, arrows or loot as appropriate.
- [ ] Pause with nonzero hitstun, protection and animation pose for20 updates,
  assert identical actor positions, timers and poses; then close inventory and
  observe resumed progression. Test window inactivity via factored host gating
  using the same production gate, not a smoke-only alternate code path.
  For death timer pause, simulation tests verify no Update means no progression;
  do not create an illegal backpack-during-death UI interaction merely to test it.
- [ ] Test live R while stunned and while inventory open cleans stale reaction,
  immunity, corpses and swing feedback without healing or unpausing.
  Run deterministic zero/large elapsed tests that ensure no overshoot, duplicate
  death event or same-frame respawn damage.
- [ ] Update Chinese README describing controls, bilateral stun/knockback,
  immunity, delayed respawn and animation scope. Document that manual feel review
  is separate from automated smoke. No new runtime settings.
- [ ] Run final verification, stopping at failures:

```powershell
dotnet build DarkLoot.sln -c Release --no-restore --verbosity minimal
dotnet test tests\DarkLoot.Tests -c Release --no-build --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
dotnet run --project src\DarkLoot.Game -c Release --no-build -- --smoke-test
Get-Content src\DarkLoot.Game\bin\Release\net9.0-windows\smoke-test.log -Tail 35
dotnet publish src\DarkLoot.Game -c Release --no-restore -o artifacts\publish
$p = Start-Process -FilePath .\artifacts\publish\DarkLoot.Game.exe -ArgumentList '--smoke-test' -Wait -PassThru
if ($p.ExitCode -ne 0) { throw "Published smoke failed: $($p.ExitCode)" }
Get-Content artifacts\publish\smoke-test.log -Tail 35
git --no-pager diff --check
git status --short
```

  Require zero-warning build, all tests, clean formatter, glyph/layout checks
  and both runtime logs' named scenarios PASS. Do not report manual playtesting
  unless it actually occurred. Review timing/state edges and API call sites,
  not just test counts. Commit `feat: verify animated combat lifecycle and presentation`.

## Dependency and review gates

Tasks1->2 establish combat/lifecycle; Task3 is independently testable pose math;
Task4 requires1/2/3; Task5 requires4. Prefer sequential execution in this shared
worktree, not concurrent editing of ArenaSimulation/RuntimeSmokeTest.

Self-review coverage: bilateral reaction/immunity/events ->1; death/reset/gates
->2; deterministic animation curves/priority ->3; concrete visuals/pause/reset
integration ->4; Chinese/normal+publish acceptance/manual-review disclosure ->5.
No design requirement is delegated to an unspecified later feature.

Before coding, ask the user to approve this plan and choose inline execution
with review checkpoints or fresh task-specific subagents. A previous feature's
subagent choice does not automatically authorize this feature's execution.
