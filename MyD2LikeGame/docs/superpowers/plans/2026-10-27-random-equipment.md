# Random Equipment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add meaningful randomized loot, two equipment slots and comparisons.

**Architecture:** Extend the existing shared definitions, generated instances and
stat aggregation, then expose their results in the existing game UI. Preserve the
headless rules/MonoGame boundary.

**Tech Stack:** Existing .NET 9, MonoGame 3.8.6, System.Text.Json and xUnit.

## Global Constraints

- Common has zero affixes, Magic one, Rare two distinct affixes.
- Weapon and Armor equip independently.
- Rolls are fixed at generation and cannot change during equipment operations.
- Maximum-health changes never grant free healing.
- Only successful player melee hits roll critical chance; critical damage is doubled.
- No save system, editor or external assets in this increment.

## Task 1: Deliver the randomized-equipment loop

This is one cohesive cross-layer change: UI comparisons and combat must use the
same aggregation and rolled-instance contract.

**Files:** Extend `src\DarkLoot.Data\Definitions`, `GameContent.cs`,
`GameContentLoader.cs`; `src\DarkLoot.Gameplay\Items`, `Loot\LootGenerator.cs`,
`Combat\PlayerState.cs` and `ArenaSimulation.cs`; Game inventory, HUD, arena and
smoke components; `content\definitions`; existing Data/Gameplay test files;
`README.md`.

**Interfaces:** Preserve existing overloads where useful. Add typed slot/rarity/stat
contracts and immutable generated rolls. Introduce a shared stat-total value
object consumed by player combat and comparison UI; no duplicate formulas.

- [ ] Write failing tests for seeded rarity/affix generation, exact counts without
  duplicates, stable values on repeated equip and independent armor/weapon slots.
- [ ] Run combined targeted filters and confirm meaningful failures before changes.
- [ ] Extend validated data/instance contracts and implement weighted generation.
- [ ] Implement shared stat totals and hypothetical same-slot replacement totals;
  test actual totals equal predicted totals.
- [ ] Integrate max-health clamping without healing and hit-only critical rolls.
  Verify zero/100-percent cases, cooldown, death restore and doubled actual damage.
- [ ] Present rarity, rolls, both slots, comparisons and actual critical feedback
  through the existing generated graphics/font path.
- [ ] Add JSON samples with enough eligible affixes for every selectable rarity;
  retain legacy definitions/tables as supported Common defaults.
- [ ] Run tests, Release build and `dotnet format DarkLoot.sln
  --verify-no-changes --no-restore`; fix issues introduced by the change.
- [ ] Run bounded actual graphics smoke for original gameplay and new gear behavior.
  Report exact coverage and physical-input limitations.
- [ ] Update README, review diff and commit with required session/coauthor trailers.
