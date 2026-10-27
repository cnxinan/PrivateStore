# DarkLoot vertical slice

Windows, .NET 9 SDK, and a DirectX-capable desktop are required. No editor or save system.

```powershell
dotnet run --project src\DarkLoot.Game --configuration Release
dotnet test DarkLoot.sln
dotnet build DarkLoot.sln --configuration Release
dotnet publish src\DarkLoot.Game --configuration Release --output artifacts\publish
.\artifacts\publish\DarkLoot.Game.exe
```

## 中文操作

- 游戏默认使用简体中文，无语言切换。
- **WASD** 移动，鼠标瞄准，**按住左键**持续近战（每 0.35 秒最多攻击一次），松开停止。
- 顶部 **FPS** 显示实际绘制帧率，每半秒刷新；背包内仍使用单次点击装备。
- **E** 拾取附近战利品；**I** 打开或关闭背包，打开时暂停战斗。
- 背包显示属性变化，点击物品行即可装备到武器或护甲栏；滚轮翻页。
- **R** 重置整个遭遇，保留生命、背包、装备和已有掉落。
- **Escape** 退出。死亡后恢复当前生命上限，清空地面掉落，保留装备。

训练场按 `encounter.training_arena` 配置两只史莱姆、两只迅捷狼和两只骷髅弓手；
近战挥击命中前方范围内的所有存活敌人，接触伤害共享冷却。击败训练史莱姆必定掉落训练之刃和训练护甲。
装备随机生成普通、魔法或稀有品质，
分别拥有零条、一条或两条不同词缀；生成后的词缀数值固定，前后缀会组成装备名称。
武器和护甲共同提供攻击、生命上限和暴击率，背包显示随机属性及换装后的属性变化。
提高生命上限不会立即回血；死亡后恢复当前生命上限，并保留所有装备。
蓝色代表玩家，战利品按品质着色，暴击使用橙红色闪光反馈。

## Architecture

- **Engine**: game-independent input snapshots, follow camera and generated rectangle/circle textures.
- **Data**: shared JSON definitions loader and validation.
- **Gameplay**: headless multi-instance `ArenaSimulation`, per-target area melee, weighted rarity/affix
  generation, two-slot equipment, shared stat totals, hit-only critical rolls and combat rules.
- **Game**: MonoGame host maps Engine input to `PlayerIntent`; renderers and inventory hit-testing contain no combat/drop rules.

Definitions and the compiled Microsoft YaHei 12-point bitmap SpriteFont are copied on build/publish.
Runtime paths resolve from `AppContext.BaseDirectory`, independent of the working directory.
MonoGame framework, pipeline and tools are pinned to 3.8.6. Building the font requires installed
Microsoft YaHei (微软雅黑, Windows Chinese supplemental fonts). The published game uses only the
compiled atlas; it does not need the source font installed or distributed.
`GameText.cs` holds Chinese presentation text and enum labels; JSON `name` fields hold content names.
When adding text/names, extend the explicit Unicode regions in `Content\Fonts\UIFont.spritefont`
and run the window smoke. There is deliberately no fallback glyph: missing characters fail rather
than silently rendering as `?`. Text is measured and fitted inside existing inventory row/column
bounds, leaving click regions unchanged.

Legacy item definitions default to the Weapon slot, and loot tables without `rarityWeights` produce
Common items. Item `attackBonus` remains between 0 and 1,000,000 inclusive. Affix ranges are inclusive,
bounded to +/-1,000,000 (critical chance to +/-100 percentage points), and every selectable rarity must
have enough eligible distinct affixes for every item slot in its table. Invalid references, slots, stats,
weights and ranges fail with the source file or loot-table ID. Combined attack and health totals are
bounded to `Int32`; critical chance is clamped to 0..100.

## Automated window smoke

```powershell
dotnet run --project src\DarkLoot.Game --configuration Release -- --smoke-test
Get-Content src\DarkLoot.Game\bin\Release\net9.0-windows\smoke-test.log
```

This opens a real graphics window and injects deterministic input snapshots through the host update/UI/draw
path. It first verifies actual loaded `SpriteFont.Characters` against all presentation strings, enum
labels, content names, and all legal zero/one/two-affix display combinations at roll endpoints.
It checks `MeasureString` layout bounds (including extreme stat comparisons), with no silent font
fallback. It exercises guaranteed weapon/armor drops, pickup, both equipment slots, comparison totals, stable
rolls on re-equip, restart, critical damage feedback, pause, contact damage and death restoration, then exits.
It uses accelerated simulation time, is bounded to 1,200 updates and returns nonzero on failure. Physical
keyboard/mouse input and human visual review remain unverified. Startup errors are written beside the
executable to `startup-error.log` (smoke failures to `smoke-test.log`).
Run `.\artifacts\publish\DarkLoot.Game.exe --smoke-test` after publishing to validate the copied atlas
and definitions too. Diagnostic logs remain in English.
