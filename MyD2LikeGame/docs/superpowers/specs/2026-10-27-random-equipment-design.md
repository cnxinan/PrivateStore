# Random Equipment

Approved scope: Common, Magic and Rare items; Weapon and Armor slots; Attack,
MaximumHealth and CriticalChance stats; inventory comparisons. No saves or editor.

## Architecture

Extend the existing Data definitions with typed slots, rarity weights and affix
definitions. Keep legacy item JSON and existing constructors usable using optional
properties where possible. New affix JSON has stable IDs, name, stat, inclusive
integer range and slot restrictions. Validate references, enum values, finite
positive weights and numeric limits with source paths.

Item instances capture rarity and actual rolled values at creation, with immutable
roll collections. Common has zero affixes, Magic one, Rare two distinct affixes.
Choose weighted rarity and eligible affixes using the injected random source.
Reject pools that cannot satisfy a selectable rarity, rather than silently reducing
affix count. Default legacy tables produce Common items.

Inventory equips to the definition's slot, preserving other equipped slots and all
owned instances. A shared aggregation path computes attack, maximum health and
critical probability from both slots. Chance is measured in percentage points,
clamped to 0..100. Retain the existing attack-bonus limits and prevent overflow.
Changing maximum health clamps current health down if needed but never heals on
equip, swap or re-equip. Death restores current maximum health and retains gear.

Player hits roll critical chance only on successful melee hits. Critical damage is
twice attack damage, with arithmetic bounded explicitly. The same simulation
reports last hit damage/critical status for graphical feedback.

## Presentation and acceptance

Show rarity-colored ground loot and inventory rows, rolled affix values, both
equipped slots, current totals and comparison deltas against the currently equipped
item in the same slot. HUD shows current/maximum health, attack and critical chance.
Use existing English bitmap font and generated geometry, without external assets.
R restarts fights and retains gear.

Keep old smoke scenarios deterministic by supplying fixture content if random
sample loot changes expectations. Add a real graphics smoke scenario exercising
both slots, rolled-value stability, comparisons, health changes without healing
and critical feedback. Unit tests cover generation reproducibility, exact unique
affix counts, validation, independent slots, replacements, stat totals, health
clamping and guaranteed zero/100-percent critical outcomes. Build, tests and
format verification must pass. Physical keyboard/mouse visual review remains
distinct from injected-input graphical smoke.
