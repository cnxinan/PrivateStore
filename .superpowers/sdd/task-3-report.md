# Task 3 Report: Loot, Inventory, and Equipment

## Result

Implemented framework-independent loot generation and inventory/equipment rules using `DarkLoot.Data.GameContent` and the existing definition records. Inventory state is publicly read-only; internal mutation is limited to the gameplay assembly. Loot uses an inclusive drop range and positive-weight selection, stores stable definition IDs, and validates public references before generating. Pickup/equip failures do not mutate inventory. Weapon bonuses resolve from `GameContent` when calculating attack damage.

Removed the empty `Class1` and `UnitTest1` templates. No combat or rendering work was started.

## TDD evidence

- **RED:** Before adding gameplay types, the combined focused test run failed to compile because `DarkLoot.Gameplay.Items` and `DarkLoot.Gameplay.Loot` did not exist (exit code 1).
- **GREEN, focused:** `dotnet test tests\DarkLoot.Tests --filter 'FullyQualifiedName~LootGeneratorTests|FullyQualifiedName~InventoryServiceTests'` — 13 passed, 0 failed.
- **GREEN, full project:** `dotnet test tests\DarkLoot.Tests` — 23 passed, 0 failed.

Focused coverage includes seeded definition-sequence reproducibility, content references, inclusive count endpoints, weight-ticket selection, missing references, duplicate/invalid pickup, failed equip immutability, read-only inventory exposure, and attack bonus application after equipping.
