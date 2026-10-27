# Final review fix

## Rationale and scope

- Root cause: JSON validation accepted every nonnegative `int` attack bonus, while
  equipment damage used checked `int` addition. `int.MaxValue` therefore passed
  loading but crashed the normal base-damage-10 equipment/HUD path.
- `ItemDefinition.MaxAttackBonus` explicitly defines the supported inclusive
  maximum as 1,000,000. The loader rejects oversized bonuses with the source
  file path, `AttackBonus` field, and maximum in its diagnostic.
- Equipment uses the same model constant to reject invalid in-memory definitions
  before changing equipment. Negative bonuses remain unsupported.
- Damage remains an `int`; supported sums, including exactly `Int32.MaxValue`,
  are returned unchanged. A base damage too large for the equipped bonus is
  rejected explicitly with `ArgumentOutOfRangeException` naming `baseDamage`,
  rather than leaking an arithmetic overflow. No saturation or stat redesign.
- README documents the supported range and API rejection contract.

## DPI warning

The original Release game build reproduced WFO0003 (one warning). Applied the
standard WinForms fix: remove both manifest DPI declarations, set
`ApplicationHighDpiMode` to `PerMonitorV2`, and call generated
`ApplicationConfiguration.Initialize()` before creating the game window.
Removing only obsolete `dpiAware` would leave another competing manifest DPI
setting. This retains the modern per-monitor-v2 intent in one supported place,
without suppressing the warning or changing unrelated manifest settings.

Reference: <https://learn.microsoft.com/en-us/dotnet/desktop/winforms/compiler-messages/wfo0003>

## Verification evidence

Commands ran from `MyD2LikeGame` using SDK 9.0.320.

1. **TDD red**, before production changes:
   `dotnet test tests\DarkLoot.Tests\DarkLoot.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~GameContentLoaderTests|FullyQualifiedName~InventoryServiceTests' --verbosity minimal`
   — 6 failed, 20 passed. Oversized loader values (1,000,001 and
   `int.MaxValue`) and invalid equipment definitions were accepted; the
   too-large base-damage test received `OverflowException` instead of the
   explicit argument rejection.
2. **Focused green**, same command after the fix:
   26 passed, 0 failed, 0 skipped. Includes loader acceptance at zero and the
   maximum, exact maximum-bonus damage at base 10 and the largest fitting base,
   oversized loader path/field diagnostics, invalid equipment rejection, and
   clear rejection one beyond the largest fitting base.
3. **Formatting**:
   `dotnet format DarkLoot.sln --no-restore --include src\DarkLoot.Data\Definitions\ItemDefinition.cs src\DarkLoot.Data\GameContentLoader.cs src\DarkLoot.Gameplay\Items\InventoryService.cs src\DarkLoot.Game\Program.cs tests\DarkLoot.Tests\Data\ContentFixture.cs tests\DarkLoot.Tests\Data\GameContentLoaderTests.cs tests\DarkLoot.Tests\Gameplay\InventoryServiceTests.cs --verbosity minimal`
   — exit 0.
4. **Full Release suite**:
   `dotnet test DarkLoot.sln --configuration Release --no-restore --verbosity minimal`
   — 44 passed, 0 failed, 0 skipped.
5. **Full non-incremental Release build**:
   `dotnet build DarkLoot.sln --configuration Release --no-restore --no-incremental --verbosity minimal`
   — succeeded, 0 warnings, 0 errors; WFO0003 resolved.
6. **Real-window runtime smoke**:
   `dotnet run --project src\DarkLoot.Game --configuration Release --no-build -- --smoke-test`
   — exit 0; all movement, combat, pickup, inventory, equipment/HUD, restart,
   pause/resume, contact, death/respawn, and graphics-loop checks passed.
   Completed at 304 updates and 163 draws.
7. `git diff --check` — no whitespace errors.

Changes are committed on the existing worktree branch; no merge performed.
