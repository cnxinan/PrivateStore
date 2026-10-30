# Gothic dual-panel UI redesign

## Goal and scope

Replace the current flat text HUD and list panels with an original dark-gothic
interface inspired by classic loot ARPG layouts: carved stone, aged brass,
recessed controls, a red health globe, and simultaneous left blacksmith/right
inventory panels. Do not copy Diablo artwork, logos, sound, fonts or interface
assets. Use original procedurally generated textures, ornament and item icons.

The user selected layout B and explicitly chose one inventory cell per item,
8 columns by 4 rows per page, with unlimited pages/capacity.

Redesign only HUD, inventory, blacksmith and item/resource tooltips. Retain
simplified-Chinese presentation, two equipment slots, combat rules, recipe costs,
drop rules and automatic persistence. Do not add mana, potion/skill systems,
drag/drop, multi-cell packing, capacity limits, unequip actions, character/map art
or an additional character sheet.

## Visual composition

Target the existing fixed 1120 by 720 window. The bottom approximately 120 pixels
form a carved-stone control console, with a red health globe on the left,
objective/feedback and compact combat stats in the center, and real inventory,
blacksmith and encounter-restart buttons. FPS is secondary, not the headline.
The globe fill represents actual current/maximum health, including empty/dead
and equipment-adjusted maximum-health states. No decorative fake mana globe.

A compact top label shows the training area, remaining enemies, and paused state
when applicable. Remove the old full-width three-line top HUD. Keep useful
control hints concise, and show actual death countdown/disabled-action state.

The left blacksmith panel is approximately 360 pixels wide; the right inventory
panel is approximately 432 pixels wide. Both sit above the console with outer
margins and independently clickable close controls. They overlay the scene.
Opening a panel does not jump the camera. Center the world camera in the
playfield above the console; maintain consistent screen-to-world aiming and
keep ground-label placement out of the console/top status area.

Use layered stone texture, restrained aged-metal highlights, inset cell wells,
readable warm text and clear hover/selection/pressed states. Common equipment is
white/gray, magic blue, rare gold. Original sword, vest, material and scroll icons
must communicate the current content rather than leaving text-only colored boxes.

## Inventory and tooltips

Show only weapon and armor equipment slots at the top, then real total attack,
maximum health and critical chance, equipment/resources tabs, an 8 by 4 grid,
and page navigation. Equipped items remain in their normal inventory positions
and carry an equipped marker. Items retain existing order and instance identity.

Each equipment instance occupies one cell. Left-clicking an occupied equipment
cell uses the existing InventoryService to equip; empty cells do nothing.
Equipment slots display their worn items but do not introduce removal or swaps
outside the existing equipment service.

Materials and owned recipe stacks use the resource tab, one cell per definition,
with actual counts and distinct icons. Large quantities may use a `999+` compact
badge, but the tooltip must always display the exact count. Learned recipe
knowledge remains distinct from owned recipe count. Do not show invented stacks.

Mouse wheel over the inventory grid and explicit previous/next buttons change
whole pages, clamped to existing pages. Switching tabs starts on page one.
Keep page state valid after counts change; zero items still shows an empty first
page and an appropriate empty-state hint. No item or resource capacity limit.

Hovering an equipment cell or occupied equipment slot shows its complete generated
name, slot, rarity, exact stored rolls, equipped marker and stat differences from
the current loadout. Use ItemStatCalculator for comparison; hovering never rolls,
equips or mutates an item. Hovering a resource shows its name, exact count, kind
and recipe-learning state where applicable.

Tooltips are measured/wrapped and constrained to the window. Their text and
geometry must not silently clip or depend on fallback glyphs. Resource and item
page navigation, hover and selection must not modify progression/save contents.

## Blacksmith

The left panel lists the four actual recipes, with selected recipe highlighting,
rarity colors, learned state, and held recipe count. Selecting a recipe changes
only the displayed detail; it does not learn or craft.

Details show the selected product, fixed rarity, all full material costs with
owned/required quantities, and the existing learning/crafting controls. Preserve
existing service authority and refusal feedback: unavailable-looking controls
must not silently pretend success or bypass validation. Route attempted actions
through BlacksmithService and present its specific result; show success in gold
and refusal/material shortages in red. Successful crafting appends a new item
without equipping or healing. Display its actual name and stored rolls.

## Dual-panel state and routing

Inventory and blacksmith visibility are independent. Track which visible panel
was most recently opened or left-clicked for keyboard/right-click close priority.
Hovering alone does not change this priority.

| Input | Behavior |
|---|---|
| I / inventory console button | Toggle only the right inventory panel. |
| F / blacksmith console button, blacksmith closed | Require existing proximity/alive gate; open blacksmith and inventory together, with blacksmith most recent. Preserve an already-open inventory's tab/page. |
| F / blacksmith console button, blacksmith open | Close only blacksmith, retaining inventory if open. |
| Panel close control | Close that panel only. |
| Escape or right-button press with a visible panel | Close the most recently opened/clicked visible panel, regardless of pointer position. |
| Escape with no panels | Existing game exit behavior. |
| Right-button press with no panels | No exit or panel action; preserve ordinary world behavior. |
| R / restart console button | Existing restart action only while alive with no panels; otherwise disabled/refused without resetting. |

If one panel closes, make the remaining panel the close target. A right-button
edge is still required: holding it does not repeat. Close/exit handling takes
precedence over other same-frame actions. A visible panel pauses combat and
animation; all panel-open, switch and close frames remain frozen, including the
frame closing the final panel. The next ordinary world frame resumes.

Use one routing boundary to consume UI input before world intent. Pointer actions
on the console, panels, close controls or pagination never penetrate as attacks,
movement, pickup or an unrelated restart; an explicitly enabled console command
still performs its own routed action. Scroll affects only the inventory grid it is over.
Clicks outside open panels cannot resume battle while a panel remains visible.

Death retains existing countdown/respawn rules and prevents opening or using
inventory/blacksmith controls. Do not change inactive-window timing or input
edge handling.

## Architecture and data boundaries

Introduce shared viewport layout geometry used by drawing, hit-testing, smoke
input injection and layout tests; do not duplicate pixel coordinates separately.
Centralize the stone/metal palette, reusable panel/button/grid/globe rendering
and original icons. Cache generated textures for the graphics-device lifecycle
and dispose them with the host.

Keep UI layout, selection, page and hover state in the presentation layer.
Extend PanelController's single-panel representation into explicit independent
visibility/focus state with an unambiguous per-frame routing result. Do not put
drawing or pointer rectangles in Gameplay.

Use existing inventory/crafting/stat services. Preserve the current save schema,
inventory order and exact rolled values; presentation-only interactions cannot
trigger progression changes. Extend GameText and the explicit SpriteFont
repertoire for new Chinese strings, and validate the loaded font.

## Verification and acceptance

Focused headless tests must cover both panels coexisting, proximity gates,
independent toggles, most-recent close order, panel-specific closes, same-frame
close priority, no world input penetration, death gating and final-close freeze.

Inventory tests must cover 0, 1, 32, 33 and multi-page items; tab reset, clamping,
pointer-local scroll, empty-cell clicks, correct instance selection on later
pages and no hover/navigation mutations. Verify real stat comparisons, original
roll text, exact large resource quantities and tooltip bounds.

Blacksmith tests retain real learning/crafting refusals, complete costs,
resource changes, unique crafted identities and no automatic equip/healing;
recipe selection alone must be harmless. Rendering tests check layout containment,
loaded glyphs and readable measured text, not just color constants.

Update the real-window smoke input script to the intentionally changed dual-panel
rules and shared geometry. Preserve existing gameplay/crafting/death/R assertions.
Verify both real panel layouts, grid hit-testing, tooltips, globe health states
and clickable console controls, plus source and published persistence restoration.
Run solution tests, Release build/publish, changed-file formatter checks and
git diff checks.

Baseline limitation: before this UI change, the full window smoke and the
pre-autosave published binary both failed the green partial-death slime
backbuffer assertion at ClearEncounter phase 0. Keep that assertion; do not
weaken it merely to obtain a pass. Report remaining acceptance gaps explicitly.
The persistence-specific two-window smoke currently passes and must remain valid.

Automated checks do not replace human visual/style and physical-input review.
