# Tasks 5 + 6 — integrated playable runtime

## Status and scope

Complete on the existing `darkloot-vertical-slice` worktree, based on `7802d0e`.
The runtime and its required sample content ship together in the commit titled
`feat: complete DarkLoot vertical slice`. No master merge or changes to other parent projects.

The Windows MonoGame app opens a real DirectX window and provides generated arena graphics,
WASD movement, mouse-aimed just-pressed melee, E pickup, I inventory, click-to-equip,
R encounter restart preserving equipment, Escape exit, player-follow camera, health/attack HUD,
on-screen controls, pickup labels, health bars, swing arcs and hit flashes.
Inventory pauses the live encounter; closing it resumes play. Unfocused normal windows pause
and consume polled input to avoid replaying stale clicks. Long inventories support wheel scrolling.

## Files and interfaces

### Engine (game-agnostic)

- Removed unused `src\DarkLoot.Engine\Class1.cs`.
- `Camera\Camera2D.cs`: follows a numerics world position and viewport dimensions;
  `ScreenToWorld(XNA.Vector2)` returns `System.Numerics.Vector2`,
  and `WorldToScreen(System.Numerics.Vector2)` returns `XNA.Vector2`.
  Conversions construct each vector explicitly.
- `Input\GameInput.cs`: `Capture(Camera2D)` polls WASD/mouse/E/I/R/Escape and returns an
  independent `GameInputSnapshot`. Its fields are numerics vectors, booleans and scroll delta;
  it does not reference Gameplay or `PlayerIntent`. WASD is normalized; click/action edges
  are detected using previous keyboard/mouse states.
- `Rendering\ShapeRenderer.cs`: owns/disposes generated 1x1 rectangle and alpha-masked
  32x32 circle textures, with rectangle/circle/line drawing helpers.

### Game composition and UI

- Replaced scaffold `Game1.cs` with `DarkLootGame.cs`; updated `Program.cs`.
- Host loads shared `GameContentLoader`, creates the existing `ArenaSimulation`, maps the
  Engine snapshot to the existing `PlayerIntent`, updates the simulation once per active
  unpaused frame, follows the player and draws with SpriteBatch.
- `ArenaRenderer.cs`: bounded ground/grid, player, slime, dropped weapons, bars and melee feedback.
- `HudRenderer.cs`: health `current/100`, numeric attack, control hints and encounter status.
- `InventoryPanel.cs`: game-layer row hit testing, scrolling, equipment display and calls to
  the existing `InventoryService.TryEquip`.
- `RuntimeSmokeTest.cs`: bounded deterministic input snapshots pass through the same
  host mapping/UI/update/draw path. No direct mutation of gameplay state and no alternate
  combat/drop implementation. Failures produce nonzero exit; diagnostics persist beside the app.
- All combat, pursuit, contact cooldowns, damage, death respawn, drops, pickup and equipment
  calculations remain in the existing shared Gameplay services. No Gameplay sources were changed.

### Content, deployment and documentation

- `content\definitions\items\training-blade.json`: `weapon.training_blade`, Training Blade, +5 attack.
- `content\definitions\monsters\training-slime.json`: `monster.training_slime`, Training Slime,
  HP 20, contact damage 3, speed 55, `loot.training_slime`.
- `content\definitions\loot-tables\training-slime.json`: `loot.training_slime`, min/max drops 1,
  exactly one weight-1 `weapon.training_blade` entry.
- `DarkLoot.Game.csproj`: definition links under `Definitions`, both output and publish copying
  with `PreserveNewest`; explicit MonoGame content reference.
- `Content\Fonts\UIFont.spritefont` and `Content\Content.mgcb`: Arial, 14 points, real MonoGame
  bitmap SpriteFont pipeline. Initial successful build reported:
  `Building Font C:\Windows\Fonts\arial.ttf`.
- Framework, builder and tool versions remain pinned at 3.8.6.
- Both definitions and compiled content resolve from `AppContext.BaseDirectory`, not cwd.
- Removed unused `src\DarkLoot.Data\Class1.cs`.
- `README.md`: prerequisites, run/build/test/publish/smoke commands, controls and layer boundaries.

## Automated verification

Commands ran from `MyD2LikeGame`:

```powershell
dotnet test tests\DarkLoot.Tests --nologo -v minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --include src\DarkLoot.Engine src\DarkLoot.Game --verbosity minimal
dotnet build DarkLoot.sln --configuration Release --nologo -v minimal
dotnet test DarkLoot.sln --configuration Release --no-restore --nologo -v minimal
dotnet publish src\DarkLoot.Game --configuration Release --no-restore --output artifacts\publish --nologo -v minimal
git diff --check
```

Results:

- All commands exited 0.
- Debug headless tests: **34 passed, 0 failed, 0 skipped**.
- Final Release tests: **34 passed, 0 failed, 0 skipped** (312 ms reported test duration).
- Release build: **0 errors**, one scaffold manifest warning `WFO0003` about WinForms high-DPI
  configuration. Existing `app.manifest` was intentionally not changed.
- Scoped full formatter verification: no changes or diagnostics required.
- Publish succeeded. Verified all three definition JSON files and
  `Content\Fonts\UIFont.xnb` (72,730 bytes) exist in published output.
- Existing kill/drop/pickup/equip/reset/death tests already cover the headless acceptance loop;
  no duplicate full-loop xUnit tests were added. New host/UI behavior is asserted by the real
  runtime smoke, including a focused 20-update live-inventory pause/input-consumption regression.

## Real-window verification and captured evidence

Launched the actual Release executable using PowerShell `Start-Process -PassThru` from the
project root, **not** its output directory. Checked `MainWindowHandle`, `Responding`, process
exit status, and bounded every smoke with `WaitForExit(45000)`. Cleanup used only specific
process IDs. The final normal test window was stopped by PID and waited for exit.

Representative launch/cleanup commands:

```powershell
$root = (Get-Location).Path
$release = Join-Path $root 'src\DarkLoot.Game\bin\Release\net9.0-windows'
$p = Start-Process (Join-Path $release 'DarkLoot.Game.exe') -WorkingDirectory $root -PassThru
Start-Sleep -Seconds 3
$p.Refresh()
$p | Select-Object Id, MainWindowHandle, Responding, MainWindowTitle
Stop-Process -Id $p.Id
$p.WaitForExit()

$p = Start-Process (Join-Path $release 'DarkLoot.Game.exe') -ArgumentList '--smoke-test' -WorkingDirectory $root -PassThru
Start-Sleep -Seconds 2
$p.Refresh()
$p | Select-Object Id, MainWindowHandle, Responding
$p.WaitForExit(45000)
$p.ExitCode
Get-Content (Join-Path $release 'smoke-test.log')
```

Repeated the smoke using `artifacts\publish\DarkLoot.Game.exe` with the same project-root cwd,
demonstrating that published definitions/font loading does not depend on cwd.

Final process evidence:

```text
NORMAL PID=34068 MainWindowHandle=8259576 Responding=True Title=DarkLoot - Training Arena
STOPPED normal window PID=34068
SMOKE path=src\DarkLoot.Game\bin\Release\net9.0-windows PID=27584 MainWindowHandle=8325112 Responding=True
EXIT PID=27584 code=0
SMOKE path=artifacts\publish PID=42496 MainWindowHandle=330376 Responding=True
EXIT PID=42496 code=0
All launched test windows exited or were stopped by specific PID.
```

Final Release smoke output (`src\DarkLoot.Game\bin\Release\net9.0-windows\smoke-test.log`):

```text
DarkLoot runtime smoke; PID=27584
PASS movement changed player position
PASS melee killed slime and generated exactly one drop
PASS pickup input transferred the ground item
PASS inventory toggle opened panel
PASS inventory click did not attack or restart encounter
PASS row click equipped training blade; attack=15
PASS R reset restored slime and preserved equipped instance
PASS movement changed player position
PASS repeat fight hit used equipped damage=15 (20 -> 5)
PASS inventory paused a live encounter and consumed movement/attack inputs for 20 updates
PASS inventory closed and resumed live encounter
PASS contact reduced health 94 -> 91
PASS death respawn restored arena and preserved equipment
PASS real graphics update/draw loop rendered multiple frames
PASS runtime smoke complete; updates=304; draws=298
```

Published smoke output (`artifacts\publish\smoke-test.log`) has the same assertions,
304 updates and 298 draws, with PID 42496. Both smoke runs exited normally with code 0.
The smoke is bounded to 1,200 updates and exercises accelerated simulation time.
Generated output/publish directories remain ignored; this report preserves the evidence.

## Limitations

- No claim of human visual inspection, physical WASD/mouse operation, screenshot review,
  or Win32 SendInput coverage. The verified GUI evidence is process/window responsiveness plus
  deterministic integration through the real host, shared gameplay services and graphics draws.
- Smoke input bypasses OS polling but does not bypass host mapping, inventory hit testing,
  simulation, generated graphics or actual SpriteFont loading.
- Windows/DirectX and installed Arial are needed to build/run as documented.
  Publish was tested locally as a framework-dependent deployment, not on a clean machine.
- No editor/save system, intentionally within requested scope.
