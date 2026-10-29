# Single-slot automatic persistence

## Scope and accepted behavior

Persist player progression across runs in one versioned JSON file at
`%LOCALAPPDATA%\DarkLoot\save.json`. No file means a fresh game. Save all owned
equipment in inventory order, preserving instance IDs, definition IDs, rarities
and exact affix rolls; equipped weapon and armor IDs; material and recipe counts;
and learned recipe IDs.

Do not persist position, current health, combat timers, monsters, arrows, ground
drops, pickup receipts, or random-generator state. Each launch creates a fresh
training encounter at the player spawn with full equipment-adjusted health.
Death and encounter restart retain existing progression rules.

Save after successful pickup, equip, recipe learning and crafting when persistent
progress actually changes, and on normal exit. Do not write every combat frame.
There is no manual save/load control, multi-slot UI, cloud sync, migration or
automatic reset of incompatible saves.

## Boundaries and restoration

Keep the save DTO, capture/validation/restoration service and JSON file store
headless in Gameplay; Data must not depend on Gameplay. The Game host owns
startup loading, change-triggered saving and normal-exit saving.

Validation checks the format version, required fields, unique nonempty equipment
IDs, legal definitions/rarities/affix rolls, valid owned equipment slot references,
positive resource counts and known resource/recipe IDs, unique learned recipes,
and valid combined equipment stats. Reuse ItemStatCalculator for equipment rules.
Validate the complete candidate before making it live; restoring inventory,
resources and knowledge is an explicit persistence operation, not simulated
pickups or crafting with new randomness.

## Storage and failure behavior

Serialize to a unique temporary file in the save directory, flush it, and
atomically replace the destination (or move into place on first save). Clean up
only that operation's temporary file. A failed write must not damage the prior
save. Do not use a copy-overwrite fallback.

Missing saves alone start fresh. Malformed JSON, unsupported versions, invalid
references or values, and inaccessible files are explicit errors with file and
field context. Preserve the existing save and stop startup rather than silently
discarding progression. Runtime save failures likewise surface a visible error
and stop the game. A failed load/save must not trigger an exit save that overwrites
the original. Existing diagnostics remain available, with a visible error for
normal interactive runs and nonzero exit codes on failures.

## Verification

Add focused headless tests for exact round-trip restoration, equipped maximum
health at spawn in a fresh encounter, validation rejection without partial
mutation, malformed/unsupported saves, and old-file preservation on write failure.
Exercise the host persistence coordinator for all four progression mutations,
unchanged frames, normal exit and failed-load/save guards.

Run the existing solution tests and Release build, then real-window smoke and
published verification. Smoke must use a unique isolated temporary save location,
never a real player's file, and verify persistence across independently created
game sessions without changing production content. Keep existing combat,
crafting, panel and rendering smoke assertions intact. Automated window checks
do not establish human visual or physical-input acceptance.
