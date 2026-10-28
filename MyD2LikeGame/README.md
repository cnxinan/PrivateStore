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
- **R** 存活时重置整个遭遇，保留玩家位置、当前生命、背包和装备；清除地面掉落、箭矢、受击状态和保护期，不额外回血或授予保护。死亡期间无效。
- **Escape** 随时退出。死亡后保持原位等待 0.6 秒，再回到出生点恢复当前生命上限，获得 0.8 秒保护；重置全部怪物，清空地面掉落及箭矢，保留背包、装备和词缀。
- 死亡等待期间禁止移动、攻击、拾取、换装和打开背包；怪物与箭矢继续运行，箭矢穿过死者。背包暂停或窗口失焦时，战斗计时冻结。

训练场按 `encounter.training_arena` 配置两只史莱姆、两只迅捷狼和两只骷髅弓手；
近战挥击命中前方范围内的所有存活敌人，接触伤害共享冷却。每只敌人被击败时有 50% 概率掉落一件训练之刃或训练护甲，两者权重相同；普通、魔法、稀有品质的权重为 1:4:2。
装备随机生成普通、魔法或稀有品质，
分别拥有零条、一条或两条不同词缀；生成后的词缀数值固定，前后缀会组成装备名称。
武器和护甲共同提供攻击、生命上限和暴击率，背包显示随机属性及换装后的属性变化。
提高生命上限不会立即回血；死亡后恢复当前生命上限，并保留所有装备。
蓝色代表玩家，战利品按品质着色，暴击使用橙红色闪光反馈。
绿色圆形是史莱姆，长身、尖耳和尾巴的是迅捷狼，灰色骷髅与弓是骷髅弓手；
每只存活怪物独立显示名称和生命条，群体挥击在各目标旁独立显示伤害和暴击。
角色有呼吸、行走、挥击或拉弓动作；受击闪白并轻微压缩，保护期低频明暗变化。
怪物死亡原地缩小淡出 0.3 秒，人物原地倾倒淡出 0.6 秒；掉落与击杀立即结算。
名字、血条和拾取位置保持真实坐标，不随动画偏移。背包暂停及窗口失焦同时冻结表现，
背包内按 R 立即清理旧动作和伤害提示，但不推进动画时间。
弓手进入射程后停步，每箭先以橙色方向线锁定瞄准 0.3 秒，发射间隔至少 1.5 秒；
箭不会追踪，横向移动可以躲避，杀死弓手不会消除已发射的箭。
左上显示剩余怪物数量，击败六只后显示清场提示；按 R 重开会清空地面装备。

## Architecture

- **Engine**: game-independent input snapshots, follow camera, deterministic actor poses and shape primitives.
- **Data**: shared JSON definitions loader and validation.
- **Gameplay**: headless multi-instance `ArenaSimulation`, per-target area melee, weighted rarity/affix
  generation, two-slot equipment, shared stat totals, hit-only critical rolls and combat rules.
- **Game**: MonoGame host maps Engine input to `PlayerIntent`; `ActorPresentation` consumes per-Update combat
  events once and maintains one animator per instance, while `ActorRenderer` transforms actor-local geometry.
  Renderers and inventory hit-testing contain no combat/drop rules; Draw never advances animation time.

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
fallback. Production content is checked unmodified, including its six configured spawns and full legal
affix repertoire. Only smoke uses copied definitions/spawns: slow enemies, lethal wolf contact, fixed
50% critical gear and guaranteed two-slot fixture loot; production keeps the shared 50% zero/one drop table.
Named stages are Approach, MultiHit, DodgeArrow, TakeArrow, Pickup, Equip, ClearEncounter,
PauseWithArrow, RestartCleanup, DeathRespawn and Complete. Assertions require real multi-kills,
independent critical/normal target rolls, locked non-homing arrows that miss and hit, both picked/equipped
slots with original identities/rolls, and six kills with a completion Draw while arrows remain in flight.
The real inventory freezes all monsters, an active windup, in-flight arrows and player health for 20 updates;
closing it resumes motion while retaining the pre-pause swing cooldown. R must clear nonempty arrows/loot
without losing gear. Actual lethal contact must enter a full 0.6-second death wait, reject dead
controls/inventory/R, then respawn at the equipped maximum health with six monsters and 0.8-second protection.
Pose checks require walking, locked swing direction, independent instance reactions, nonneutral paused poses,
and zero-time paused reset. Real backbuffer samples require visible partial monster/player death bodies;
these are color-presence checks at authoritative anchors, not golden screenshots. Headless controller tests
compile the presentation source directly, without referencing the executable Game project.
It uses accelerated simulation time, bounds each named stage to 450 updates and the run to 2,400 updates,
and returns nonzero with the stage/phase on failure (no success fallback). Physical
keyboard/mouse input and human visual review remain unverified. Startup errors are written beside the
executable to `startup-error.log` (smoke failures to `smoke-test.log`).
Run `.\artifacts\publish\DarkLoot.Game.exe --smoke-test` after publishing to validate the copied atlas
and definitions too. Diagnostic logs remain in English.
