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
- **E** 每次拾取附近最近一堆材料或配方；**I** 打开或关闭背包，打开时暂停战斗与动画。
- 背包有“装备/资源”页；资源页显示材料与配方数量。装备页显示属性变化，点击物品行即可装备到武器或护甲栏；滚轮翻页。
- **F** 在铁匠（世界坐标 120,230）64 单位以内打开铁匠，再按 F 关闭。左键单击“学习”消耗一张配方，单击“打造”支付材料立即获得新装备，100% 成功、无金币费用、不治疗、**不自动穿戴**；按 I 切到背包装备页手动穿戴。
- 铁匠与背包互斥，均暂停战斗和动画；关闭或切换面板的那一帧不推进战斗，也不穿透移动、攻击、拾取或 R。
- **右键**按下时关闭当前背包或铁匠面板，光标无需位于面板内；按住不重复触发，关闭帧不会执行面板操作或重新打开面板。没有面板时右键不退出、不影响正常战斗输入。
- **R** 仅在存活且没有面板时重置整个遭遇，保留玩家位置、当前生命、全部装备、资源数量和铁匠知识；清除地面掉落、箭矢、受击状态和保护期，不额外回血或授予保护。死亡或面板打开期间无效。
- **Escape** 有面板时只关闭当前面板，没有面板时才退出。死亡后保持原位等待 0.6 秒，再回到出生点恢复当前生命上限，获得 0.8 秒保护；重置全部怪物，清空地面掉落及箭矢，保留全部装备实例及词缀、资源数量和已学配方。
- 死亡等待期间禁止移动、攻击、拾取、换装、学习、打造和打开面板；怪物与箭矢继续运行，箭矢穿过死者。面板暂停或窗口失焦时，战斗与动画计时冻结。
- **没有存档**：已学配方仅在当前运行期间持续有效；重复掉落的配方保留在资源袋，再次点击学习不会消耗。死亡和 R 保留进度，但退出游戏后装备、资源与已学知识全部清空，新场景从空背包开始。

训练场按 `encounter.training_arena` 配置两只史莱姆、两只迅捷狼和两只骷髅弓手；
近战挥击命中前方范围内的所有存活敌人，接触伤害共享冷却。每只史莱姆必掉黏液、狼必掉兽皮、骷髅弓手必掉骨片，每次数量为 1～2，等概率；
每只怪物在材料之外独立有 20% 概率掉一张配方，四张等概率，允许重复、允许已学配方，**不直接掉装备**。初始空手也可用基础攻击击杀怪物。

| 配方 | 固定产物品质 | 每次打造费用 |
|---|---|---|
| 魔法武器配方 | 魔法训练之刃 | 骨片×2、黏液×1 |
| 魔法护甲配方 | 魔法训练护甲 | 兽皮×2、黏液×1 |
| 稀有武器配方 | 稀有训练之刃 | 骨片×4、兽皮×2、黏液×2 |
| 稀有护甲配方 | 稀有训练护甲 | 兽皮×4、骨片×2、黏液×2 |

学会后可反复按完整费用打造，每次生成独立的新装备实例。魔法装备有一条随机词缀，稀有装备有两条不同的随机词缀；
生成后的词缀数值固定，前后缀会组成装备名称。
武器和护甲共同提供攻击、生命上限和暴击率，背包显示随机属性及换装后的属性变化。
提高生命上限不会立即回血；死亡后恢复当前生命上限，并保留所有装备。
`ItemStatCalculator`统一计算实际属性与换装预览；`InventoryService`统一处理拾取、穿戴及内部同 ID 快照替换；
`KillRewardService`按战斗轮次和怪物实例去重结算资源奖励；`BlacksmithService`统一校验学习、打造与完整扣费。本轮未引入装备经验、等级或存档。
蓝色代表玩家，材料与配方地面堆显示实际名称和数量，铁匠使用几何形状绘制，暴击使用橙红色闪光反馈。
绿色圆形是史莱姆，长身、尖耳和尾巴的是迅捷狼，灰色骷髅与弓是骷髅弓手；
每只存活怪物独立显示名称和生命条，群体挥击在各目标旁独立显示伤害和暴击。
角色有呼吸、行走、挥击或拉弓动作；玩家和怪物受击都会进入短暂硬直并向受击方向击退，
受击闪白并轻微压缩。玩家硬直 0.15 秒、击退 18 单位，命中保护 0.5 秒；
怪物硬直 0.12 秒、击退 10 单位。死亡等待 0.6 秒后复活，并获得 0.8 秒保护。
这些程序化姿态只负责表现，不改变碰撞、攻击、箭矢或掉落规则。
怪物死亡原地缩小淡出 0.3 秒，人物原地倾倒淡出 0.6 秒；掉落与击杀立即结算。
名字、血条和拾取位置保持真实坐标，不随动画偏移。背包暂停及窗口失焦同时冻结表现，
面板内按 R 不重开；关闭面板后再按 R 清理旧动作和伤害提示。
弓手进入射程后停步，每箭先以橙色方向线锁定瞄准 0.3 秒，发射间隔至少 1.5 秒；
箭不会追踪，横向移动可以躲避，杀死弓手不会消除已发射的箭。
左上显示剩余怪物数量，击败六只后显示清场提示；按 R 重开会清空未拾取的地面资源。

## Architecture

- **Engine**: game-independent input snapshots, follow camera, deterministic actor poses and shape primitives.
- **Data**: shared JSON definitions loader and validation.
- **Gameplay**: headless multi-instance `ArenaSimulation`, per-target area melee, resource rewards,
  stackable materials/recipes, recipe knowledge, atomic crafting through the shared equipment factory,
  two-slot equipment, shared stat totals, hit-only critical rolls and combat rules.
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
50% critical weapon / +20 health armor affix pools, fixed eight-material drops and 100% designated recipes.
It preserves the production recipe costs and all material/recipe definitions; production uses 1–2 materials
and an independent 20% recipe chance, never direct equipment drops.
Named stages are Approach, MultiHit, DodgeArrow, TakeArrow, GatherMaterials, Pickup, ApproachSmith,
LearnCraft, InspectResources, Equip, ReopenEncounter, ClearEncounter, PauseWithArrow, RestartCleanup,
DeathRespawn, PauseWithReaction and Complete. The first unarmed round gathers materials and both recipe items
only through actual kills and E pickups (including identity receipts). Movement reaches the real smith gate;
F opens the panel, real button clicks test refusals, learn both recipes and craft once each through the
official services. Assertions check full production costs, unique instances, real displayed names/rolls,
no automatic equip/healing, retained duplicate recipes, I panel switching, resource tabs, and row comparisons
computed before equipping. Esc closes without exiting or advancing combat. R begins the armed combat round.
Assertions retain real multi-kills, independent critical/normal target rolls, locked non-homing arrows that
miss and hit, both crafted/equipped slots with original identities/rolls, and six kills with a completion
Draw while arrows remain in flight.
The real inventory freezes all monsters, an active windup, in-flight arrows and player health for 20 updates;
the closing frame remains frozen, then the next world frame resumes motion while retaining the pre-pause
swing cooldown. R is refused in a panel; live R must clear nonempty arrows/loot and reset animation at zero
time before the next simulation advance, without losing gear, resources or knowledge.
Actual lethal contact must enter a full 0.6-second death wait, reject dead
controls/inventory/R, then respawn at the equipped maximum health with six monsters and 0.8-second protection.
Pose checks require walking, locked swing direction, independent instance reactions, nonneutral paused poses,
and zero-time reset cleanup after the paused-R refusal and gated Esc close.
Actual smith feedback, names, costs, buttons and resource rows are measured and glyph-checked when drawn.
Real backbuffer samples require visible partial monster/player death bodies;
these are color-presence checks at authoritative anchors, not golden screenshots. Headless controller tests
compile the presentation source directly, without referencing the executable Game project.
It uses accelerated simulation time, bounds each named stage to 450 updates and the run to 2,400 updates,
and returns nonzero with the stage/phase on failure (no success fallback). Physical
keyboard/mouse input and human visual review remain unverified. Startup errors are written beside the
executable to `startup-error.log` (smoke failures to `smoke-test.log`).
After publishing, wait for the real WinExe process and capture its true exit code (direct shell invocation
can misleadingly report success):

```powershell
$p = Start-Process -FilePath (Resolve-Path '.\artifacts\publish\DarkLoot.Game.exe') `
    -ArgumentList '--smoke-test' -Wait -PassThru `
    -RedirectStandardOutput (Join-Path $PWD 'artifacts\crafting-published.stdout.log') `
    -RedirectStandardError (Join-Path $PWD 'artifacts\crafting-published.stderr.log')
if ($p.ExitCode -ne 0) { throw "Published smoke exited $($p.ExitCode)." }
```

This validates the copied atlas and definitions too. A startup `NoSuitableGraphicsDeviceException`
(no graphics adapters) blocks window acceptance; it is not a passed or skipped smoke.
The headless suite separately drives the smoke input script through kills, pickups, learning, crafting,
equipping and the armed six-kill round, and verifies actual service ownership across death/R and an empty
new scene. These do not replace window rendering/loaded-font checks. Diagnostic logs remain in English.
自动烟测不能代替人工视觉与手感验收；本次只执行自动化验证，没有进行人工游戏手感或窗口焦点实测。
