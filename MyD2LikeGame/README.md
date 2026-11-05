# DarkLoot vertical slice

Windows, .NET 9 SDK, and a DirectX-capable desktop are required. No editor.

```powershell
dotnet run --project src\DarkLoot.Game --configuration Release
dotnet test DarkLoot.sln
dotnet build DarkLoot.sln --configuration Release
dotnet publish src\DarkLoot.Game --configuration Release --output artifacts\publish
.\artifacts\publish\DarkLoot.Game.exe
```

## 中文操作

- 游戏默认使用简体中文，无语言切换。
- 启动进入没有敌人的教堂安全区。走进东侧“进入墓穴”入口，即可进入三房间连续地牢；房间之间通过石廊连接，不切换场景，镜头随探索横向滚动。
- **WASD** 移动，鼠标瞄准，**按住左键**持续近战（每 0.35 秒最多攻击一次），松开停止。
- 底部石质控制台显示真实生命血球、战斗属性和可点击的行囊 / 铁匠 / 任务 / 重开按钮；右下 **FPS** 显示实际绘制帧率，每半秒刷新。界面不显示快捷键提示，原有按键功能保留。界面使用原创程序化石纹、黄铜边框和物品图标，不含 Diablo 素材。
- **E** 每次拾取附近最近一堆材料或配方；**I** 打开或关闭背包，打开时暂停战斗与动画。
- 右侧背包有“装备 / 材料配方”标签，采用 **8×4 格分页，每件一格、容量不限**；武器与护甲只有两个穿戴槽，已装备物品仍保留原格并标记。左键点击装备格穿戴，悬浮查看完整名称、原始词缀及换装比较；滚轮仅在格子内翻整页，也可点击上一页/下一页。切换标签回到第一页。
- 资源格显示真实材料与持有配方，超过 999 的角标简写为 `999+`，悬浮仍显示精确数量和配方学习状态；仅翻页、悬浮或点击资源不会改变存档进度。
- **F** 在铁匠（世界坐标 120,230）64 单位以内同时打开左侧铁匠与右侧背包，保留已有背包标签和页码；再次 F 只关闭铁匠。先点击四张配方之一查看详情，再点击共用“学习”或“打造”按钮。学习消耗一张配方；打造支付完整材料获得新装备，100% 成功、无金币费用、不治疗、**不自动穿戴**，在右侧装备格手动穿戴。
- 铁匠面板有“委托 / 打造”页，默认保留打造页；在委托页手动接取任务，完成后回铁匠处点击“交付领奖”。底部“任务”按钮可在任何位置打开任务日志，查看三步委托及奖励；日志占用右侧背包位置，与背包互斥，可与左侧铁匠同时打开。顶部跟踪当前目标，铁匠头顶 `!` 表示可接取，`?` 表示可交付。
- **I** 独立切换背包，两栏可同时打开。任意面板均暂停战斗和动画；所有开关面板帧（包括最后一栏关闭帧）不推进战斗，按钮和格子点击不会穿透为移动、攻击、拾取或无关的 R。
- **右键**或 **Esc** 关闭最近打开或左键操作的那一栏，悬浮不改变关闭顺序，光标无需在栏内；各栏 X 只关闭自身。右键需按下边沿，按住不重复；没有面板时右键不退出、不影响正常战斗输入。
- **R** 仅在存活且没有面板时重置整个墓穴，保留当前生命、全部装备、资源数量和铁匠知识；清除地面掉落、箭矢、受击状态和保护期，重新封闭首领门，不额外回血或授予保护。身在墓穴时回到墓穴入口，身在教堂时留在原地；死亡或面板打开期间无效。
- **Escape** 没有面板时才退出。死亡后保持原位等待 0.6 秒，再返回教堂恢复当前生命上限，获得 0.8 秒保护；重置墓穴全部怪物与首领门，清空地面掉落及箭矢，保留全部装备实例及词缀、资源数量和已学配方。
- 死亡等待期间禁止移动、攻击、拾取、换装、学习、打造和打开面板；怪物与箭矢继续运行，箭矢穿过死者。面板暂停或窗口失焦时，战斗与动画计时冻结。
- **单档自动存读**：启动读取 `%LOCALAPPDATA%\DarkLoot\save.json`；成功拾取、换装、学习、打造、接取任务、推进击杀或交付导致进度变化后立即保存，正常退出也保存。保留全部装备实例及原始品质/词缀、背包顺序、穿戴槽位、材料/配方数量、已学配方及任务进度；重复配方仍保留，再次学习不消耗。
- 每次启动应用从教堂按装备后的生命上限满血开始，墓穴重新生成；不恢复位置、当前生命、怪物、箭矢或地面掉落。首次没有存档时从空背包开始。死亡和 R 不清除存档进度。
- 存档使用带版本号的 JSON，同目录临时文件落盘后原子替换。当前版本为 2，可读取版本 1 的旧档并完整保留原装备与资源，任务从未接取开始，下次保存写入版本 2。损坏、版本不支持、定义不兼容或无法读取会明确弹出错误并停止启动，不自动重置或覆盖旧档；运行中写入失败也会报错停止，不在退出时重试覆盖。程序目录无法写诊断日志时仍显示存档错误。没有多角色、手动读写或云同步；如需重开，请先退出并自行备份、移走该文件。

### 铁匠委托

| 委托 | 接取后的目标 | 一次性奖励 |
|---|---|---|
| 墓穴初探 | 击败任意 3 只怪物 | 骨片×2、黏液×2 |
| 锻造备料 | 交付骨片×4，交付时扣除 | 魔法武器配方×1 |
| 讨伐守卫 | 击败墓穴守卫 | 稀有护甲配方×1、兽皮×2 |

必须交付前一任务才解锁下一任务，不自动接取或发奖。接取前的击杀不计数；
提前杀过守卫，需要接取讨伐任务后重开墓穴再次击败它。
材料任务允许使用已有骨片，但以交付时的实际库存为准，打造花掉后需要补齐。
配方奖励放入资源，不自动学习或打造。交付统一校验距离、存活状态和完整资源变更，
数量溢出时明确拒绝，不部分扣料或发奖。死亡、R、普通往返和退出不重置任务或已领取奖励。
没有每日委托、重复悬赏、放弃、分支、经验或任务货币。

墓穴入口房间有两只史莱姆和一只骷髅剑士，战场房间有两只迅捷狼、两只骷髅弓手和一名邪教法师，
祭坛房间有一只强化骷髅弓手“墓穴守卫”（180 生命，锁定射击机制沿用弓手），全图共九只敌人。
骷髅剑士为 50 生命、55 移速、8 接触伤害的慢速近战，使用原创骨架、钢剑和黄铜包边盾牌；
盾牌只负责外观，不额外引入格挡或减伤规则。邪教法师为 30 生命、60 移速，穿紫色兜帽法袍、持发光法杖；
进入 280 射程后蓄力 0.6 秒，至少间隔 2 秒发射一枚速度 170、伤害 9 的紫色直线魔法弹。
蓄力方向锁定、魔法弹不追踪，侧移可以躲避；受击打断蓄力，墙柱和关闭的门会拦截魔法弹。
魔法弹沿用既有投射物伤害规则，不新增元素伤害或状态异常。两种新怪物仍掉骨片和原有配方，不引入新材料或存档字段。
后续房间的敌人在玩家进入该房间前保持休眠，唤醒后能够绕开石柱并追入走廊。
墙壁、石棺、石柱与关闭的首领门真实阻挡玩家、怪物、击退和箭矢；近战与拾取不能穿墙。
击败战场五只敌人（包括邪教法师）后首领门开启，击败守卫后祭坛东侧出现返回教堂的出口；墓穴入口处始终可以返回。
普通往返保留本次墓穴的敌人生命、掉落和箭矢，不治疗；铁匠只在教堂提供服务。
地图固定，没有随机生成、小地图、新技能或新增装备槽。
原 `encounter.training_arena` 六怪训练遭遇仅用于旧版完整窗口回归。

近战挥击命中前方范围内的所有存活敌人，接触伤害共享冷却。每只史莱姆必掉黏液、狼必掉兽皮、骷髅弓手必掉骨片，每次数量为 1～2，等概率；
每只怪物在材料之外独立有 20% 概率掉一张配方，四张等概率，允许重复、允许已学配方，**不直接掉装备**。初始空手也可用基础攻击击杀怪物。

教堂使用原创哥特式房间：尖拱彩窗、雕刻石柱、旗帜、磨损石板与中央黄铜纹章，
左侧保留铁匠工作角、炉火和工具架；冷色窗光与暖色烛火交织，火焰、火星与浮尘随世界计时，
面板暂停和窗口失焦时冻结。镜头限制在房间范围并偏向后墙，不再露出场外大片空白；
教堂装饰不增加碰撞，教堂范围为 960×540。墓穴为 3200×540 连续地图，
入口、战场与祭坛分别使用冷蓝石材、灰石与暗红祭坛装饰；走廊缺口与实体障碍位置对应，门与出口随战斗状态实时更新。

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
`KillRewardService`按战斗轮次和怪物实例去重结算资源奖励；`BlacksmithService`统一校验学习、打造与完整扣费。本轮未引入装备经验或等级。
玩家使用原创分层人物：头部、身体、手臂、腿与皮靴共同组成冒险者，基础衣物为暗蓝色。
未装备武器时空手，穿戴后显示剑身、护手与剑柄；护甲增加胸甲和肩甲。
普通、魔法、稀有装备分别以钢铁、蓝钢、黄铜镶边及局部装饰区分，同款同品质外形一致，不按随机词缀改变模型。
场景人物直接读取当前穿戴槽，背包换装立即更新外观，但暂停中的动作不推进；重新启动后从已有装备重建外观，
无需额外存档字段。本轮不增加装备槽、背包人物预览或物理布娃娃。
材料与配方地面堆显示实际名称和数量，铁匠使用像素动画，暴击使用橙红色闪光反馈。
绿色胶状精灵是史莱姆，长身、尖耳和尾巴的是迅捷狼，灰色骷髅与弓是骷髅弓手；
每只存活怪物独立显示名称和生命条，群体挥击在各目标旁独立显示伤害和暴击。
玩家攻击加入 pixelfx 风格的原生像素特效：空手为短拳风，装备武器为分层冷钢色剑弧与拖尾，
挥空也会播放；真实命中在各目标受伤位置产生定向暖色火花，暴击增加星芒与破碎光环。
特效寿命为拳风 0.12 秒、剑弧 0.18 秒、普通命中 0.24 秒、暴击 0.36 秒，不改变伤害或冷却。
帧贴图一次生成并缓存，最多保留 32 个临时实例；不消耗战斗随机数，也不写入存档。
面板与失焦冻结特效，重开、复活和切图清理旧实例；血条、名字及伤害文字绘制在特效之上。
授权与来源说明随构建和发布包含在 `ThirdPartyNotices.txt`，不导入上游演示角色或字体。
角色有呼吸、行走、挥击或拉弓动作；玩家和怪物受击都会进入短暂硬直并向受击方向击退，
受击闪白并轻微压缩。玩家硬直 0.15 秒、击退 18 单位，命中保护 0.5 秒；
怪物硬直 0.12 秒、击退 10 单位。死亡等待 0.6 秒后复活，并获得 0.8 秒保护。
这些精灵动画和姿态偏移只负责表现，不改变碰撞、攻击、箭矢或掉落规则。
怪物死亡原地缩小淡出 0.3 秒，人物原地倾倒淡出 0.6 秒；掉落与击杀立即结算。
名字、血条和拾取位置保持真实坐标，不随动画偏移。背包暂停及窗口失焦同时冻结表现，
面板内按 R 不重开；关闭面板后再按 R 清理旧动作和伤害提示。
弓手进入射程后停步，每箭先以橙色方向线锁定瞄准 0.3 秒，发射间隔至少 1.5 秒；
箭不会追踪，横向移动可以躲避，杀死弓手不会消除已发射的箭。
顶部中央显示当前位置、墓穴剩余怪物数量与面板暂停状态；教堂显示安全区域，控制台显示探索、开门或首领完成目标。按 R 重开会清空未拾取的地面资源。

## Pixel Content：第一阶段

教堂、三房墓穴、铁匠、玩家和五种怪物现在使用
`Content\Pixel` 下的 32 张 PNG，经 MGCB 编译为 XNB，再由
`Content.Load<Texture2D>` 加载。角色包含八方向、待机 / 移动 / 攻击 / 受击 / 死亡；
武器和护甲使用独立品质图层。地图碰撞、战斗时序、暂停、任务和存档不变。
UI、攻击特效及箭矢 / 魔法弹仍使用既有实现，并非全游戏美术替换。

角色图集为 288×1920，每格 48×48、每段六帧，以 2 倍 PointClamp 绘制；
`atlas.json` 固定版本 1 的尺寸、方向、动作和锚点。地形使用 32px 图块，
道具使用 64px 单元，边界裁切而非拉伸。缺资源或不兼容元数据会明确报错，
不会回退旧几何人物。所有已加载像素贴图由 ContentManager 管理生命周期，
场景和人物 renderer 不自行销毁共享贴图。

这是可替换的首批原创像素素材，不是最终精修美术。可编辑源代码、接触表及材质来源
位于 `tools\pixel_art`；角色和道具由离线绘图脚本创作，部分墓穴地板使用已保存的
本地 AI 材质再切片、重绘，模型来源与许可标签见 `source\provenance.json`。
不包含 Diablo 素材、模型权重或在线生成服务；游戏构建和运行不需要 Python / AI。
重建素材只需 Python 3.12 和 `tools\pixel_art\requirements.txt` 指定的 Pillow：

```powershell
python tools\pixel_art\build_assets.py
python -m unittest discover -s tools\pixel_art -v
```

提交的 PNG 可直接构建；重新生成后运行 `dotnet build` 会重新编译 Content。
发布时同时复制编译贴图、`atlas.json` 和来源说明，运行路径不依赖当前工作目录。

## Architecture

- **Engine**: game-independent input snapshots, follow camera, deterministic actor poses and shape primitives.
- **Data**: shared JSON definitions loader and validation.
- **Gameplay**: headless multi-instance `ArenaSimulation`, per-target area melee, resource rewards,
  stackable materials/recipes, recipe knowledge, atomic crafting through the shared equipment factory,
  two-slot equipment, shared stat totals, hit-only critical rolls and combat rules.
  `ProgressPersistence` captures detached progression and validates the whole candidate before startup
  restoration; `JsonSaveStore` provides strict versioned JSON and atomic writes; `SaveSession` saves
  changed progression and normal exit, disabling writes after a persistence failure.
  `DungeonSession` switches between hub/crypt simulations sharing player and smith progression;
  `WorldMap` provides swept-circle sliding, wall-clipped line travel and bounded deterministic A*
  navigation. Legacy null-map training simulation retains its original behavior.
  `QuestState` shares persistent quest progression between both simulations; the immutable `SmithQuests`
  catalog is separate from player state. `QuestService` validates manual acceptance/turn-in and prepares
  the entire cost/reward candidate before committing ownership and completion together.
  Kill credit is emitted only from the once-only granted monster-death settlement, not from Draw or corpse polling.
- **Game**: MonoGame host maps Engine input to `PlayerIntent`; `ActorPresentation` consumes per-Update combat
  events once and maintains one animator per instance, exposing read-only sprite-frame snapshots.
  Renderers and inventory hit-testing contain no combat/drop rules; Draw never advances animation time.
  `UiLayout` shares panel/grid/button rectangles across rendering, routing and smoke injection.
  `GothicUiRenderer` caches and disposes original stone/globe textures; `ItemTooltip` measures/wraps
  instance-derived details. Camera and loot labels use the playfield above the console, independent
  of panel visibility. `PixelContent` loads and validates the shared Content-owned atlases;
  `PixelSceneRenderer` draws cropped tiles, props, torch and portal frames.
  `GothicRoomRenderer` advances ambience only on world frames.
  `GothicRoomLayout` bounds the camera without changing gameplay collision. UI state and the save schema remain separate.
  `PlayerAppearance` reads equipped slots; `ActorRenderer` layers body, armor and weapons with
  facing-dependent occlusion, prebuilt white masks and existing opacity.
  Legacy `PlayerPaperDoll`, `GothicRoomArt` and `DungeonRoomArt` remain reference/test code,
  not production character/scene rendering.
  `DungeonRoomRenderer` draws live gate/portal
  state and freezes ambience with the same world-frame gate as combat.
  `ActorPresentation` consumes attack events once and owns transient `CombatEffect` lifetimes.
  `CombatEffectArt` generates deterministic two-pixel frames; `CombatEffectRenderer` caches/disposes
  those textures and draws the locked event direction without advancing time.

Definitions and the compiled Microsoft YaHei 12-point bitmap SpriteFont are copied on build/publish.
Runtime paths resolve from `AppContext.BaseDirectory`, independent of the working directory.
MonoGame framework, pipeline and tools are pinned to 3.8.6. Building the font requires installed
Microsoft YaHei (微软雅黑, Windows Chinese supplemental fonts). The published game uses only the
compiled atlas; it does not need the source font installed or distributed.
`GameText.cs` holds Chinese presentation text and enum labels; JSON `name` fields hold content names.
When adding text/names, extend the explicit Unicode regions in `Content\Fonts\UIFont.spritefont`
and run the window smoke. There is deliberately no fallback glyph: missing characters fail rather
than silently rendering as `?`. Labels are measured inside shared bounds; full tooltips wrap without
truncation and remain inside the playfield.

Legacy item definitions default to the Weapon slot, and loot tables without `rarityWeights` produce
Common items. Item `attackBonus` remains between 0 and 1,000,000 inclusive. Affix ranges are inclusive,
bounded to +/-1,000,000 (critical chance to +/-100 percentage points), and every selectable rarity must
have enough eligible distinct affixes for every item slot in its table. Invalid references, slots, stats,
weights and ranges fail with the source file or loot-table ID. Combined attack and health totals are
bounded to `Int32`; critical chance is clamped to 0..100.

## Automated window smoke

```powershell
dotnet run --project src\DarkLoot.Game --configuration Release -- --smoke-test
dotnet run --project src\DarkLoot.Game --configuration Release -- --persistence-smoke-test
dotnet run --project src\DarkLoot.Game --configuration Release -- --dungeon-smoke-test
dotnet run --project src\DarkLoot.Game --configuration Release -- --quest-smoke-test
Get-Content src\DarkLoot.Game\bin\Release\net9.0-windows\smoke-test.log
```

All commands use a unique temporary save directory, never the player's real save, and clean it up
afterward. The full smoke retains all existing combat/rendering assertions, then checks the on-disk
snapshot against actual live progression, closes the game, and creates a second independent game
session/window that loads that same file. Two real Draws validate exact IDs, rolled values, inventory
order, both slots, resources and learned recipes, along with full equipped health and a fresh encounter.
`--persistence-smoke-test` specifically runs the real kill/pickup/learn/craft/equip/R loop, then performs
that same two-window persistence check before the armed combat acceptance stages. It does not replace
the full smoke's death-pixel assertions.

Attack effects pass **484 tests**, formatting verification, Release build and publish. The full
`--smoke-test` now additionally validates GPU frames for four aim directions, decay and expiry,
then checks actual world pixels for punch, equipped slash, ordinary hit and critical hit.
Open-panel Draws verify that effect instances and ages stay frozen. Source and published combat,
dungeon and quest windows retain all previous acceptance assertions; evidence is preserved in
`artifacts\attack-effects-source-*.log` and `artifacts\attack-effects-published-*.log`.
`artifacts\attack-effects-frames.png` compares directional and lifetime frames; the punch, slash,
hit and critical screenshots show effects from real combat events.

`--quest-smoke-test` drives the real smith tabs, acceptance and reward buttons, walks the dungeon,
kills and collects actual drops, delivers four bones, defeats the guardian after acceptance,
checks repeated reward refusal, restarts the dungeon, and opens a second independent window
to verify the exact persisted completed chain and resources. The isolated fixture uses one-health,
very slow, harmless enemies and fixed two-material drops without recipe drops; production values
are unchanged. Actual backbuffer pixels validate both smith markers, and screenshots cover
the commission page, paused journal, completed chain and restored window.
Quest integration passes **469 tests**, formatting verification and Release publish.
Source and published real-window quest, full-combat and dungeon regressions all pass:
`artifacts\quests-source-quest.log`, `artifacts\quests-source-combat.log`,
`artifacts\quests-source-dungeon.log`, `artifacts\quests-published-quest.log`,
`artifacts\quests-published-combat.log` and `artifacts\quests-published-dungeon.log`.
Quest screenshots are preserved as `artifacts\quests-*.png`.

Full combat/death acceptance now passes in both source and published real-window runs; see
`artifacts\combat-source-smoke.log` and `artifacts\combat-published-smoke.log`.
The pre-dungeon baseline passed **408 tests**, including blocked/clear west-facing miss regressions,
along with formatting and Release publish checks.
Both full runs cover death/respawn, corpse pixels, arrow/windup/reaction pauses, reset cleanup and the
second-window exact progression/appearance restore.

The earlier `PauseWithArrow`, phase 3 failure was a smoke-input error: the script aimed west while a
live `smoke.slime.2` was about ten units west of the player, so the production hit was correct.
The script now waits for the existing west-facing 48-unit/45-degree melee cone to be empty before
attempting the miss. Aim locking and all original acceptance assertions remain unchanged; no combat
rules were modified. Earlier failure logs remain in `artifacts\gothic-full-smoke.log`,
`artifacts\cathedral-full-smoke.log` and `artifacts\paperdoll-full-smoke.log`;
the reproduced baseline and position evidence are `artifacts\combat-baseline-smoke.log` and
`artifacts\combat-diagnostic-smoke.log`.
The full smoke now also waits for an actual Draw after observing the narrow partial-slime-death
pose, before allowing another smoke Update. This fixes multiple Updates skipping the sample frame;
the original green corpse-pixel assertion and production death timing remain unchanged.

Continuous dungeon acceptance: **433 tests pass**. Source and published full combat and dungeon
two-window runs are recorded in `artifacts\dungeon-source-smoke-test.log`,
`artifacts\dungeon-source-dungeon-smoke.log`, `artifacts\dungeon-published-smoke-test.log`
and `artifacts\dungeon-published-dungeon-smoke.log`.
The dedicated dungeon smoke walks all three rooms using real input routing, attacks/picks up real
drops before leaving, pauses actors/poses/ambience across actual Draws, captures closed/open gate
states, learns/crafts/equips at the hub, then dies through real wolf contact and checks exact
progression retention and a fresh dungeon after returning. A second independent window restores
the exact persisted items, rolls, slots, resources and learned recipes into a fresh safe hub.
Its copied fixture uses very slow enemies, one-health wolves with ten-damage contact, harmless
arrows and guaranteed magic-blade recipes; production health, combat and 1–2-material/20%-recipe
drop rules remain unchanged. Only explicit dungeon automation advances when its OS window loses
focus; normal gameplay still freezes while unfocused. Screenshots beside the executable (also
copied to `artifacts`) include `dungeon-hub.png`, `dungeon-room-0.png` through `dungeon-room-2.png`,
`dungeon-gate-closed.png`, `dungeon-gate-open.png`, `dungeon-death-return.png` and `dungeon-restored.png`.

Two-enemy expansion: **445 tests pass**, including the warrior's multi-hit bone-drop/reset behavior,
the cultist's required gate kill, locked casting and wall-blocked projectile, full-duration cast
presentation and both new actors' move/attack/hurt/death curves. Formatting, Release build and publish
pass. Both source and published full combat and dungeon two-window runs pass; logs are
`artifacts\new-enemies-source-combat.log`, `artifacts\new-enemies-source-dungeon.log`,
`artifacts\new-enemies-published-combat.log` and `artifacts\new-enemies-published-dungeon.log`.
The dungeon smoke also validates actual sword/shield, robe/staff and emitted violet-bolt pixels,
then kills all nine enemies through real input and repeats crafting, death-return and exact saved
progression restore. The original six-enemy combat smoke and its pixel assertions remain unchanged.
Inspected actual renders are `artifacts\new-enemy-poses.png`, `artifacts\dungeon-skeleton-warrior.png`,
`artifacts\dungeon-cultist-mage.png` and `artifacts\dungeon-magic-bolt.png`.
Earlier pre-redesign source and published runs failed the green partial-death slime assertion at
`ClearEncounter`, phase 0. Neither assertion was weakened or removed. The redesigned source and published
UI/persistence-only two-window checks pass independently.
The preserved baseline failure log is `artifacts\autosave-baseline-full-smoke.log`; redesigned UI and
persistence results are `artifacts\gothic-source-smoke.log` and `artifacts\gothic-published-smoke.log`.
Real Draw screenshots are `gothic-ui-both.png`, `gothic-ui-resource-tooltip.png`, and
`gothic-ui-item-tooltip.png` beside the executable (also copied to `artifacts` during verification).

Cathedral validation: **397 headless tests pass**, Release build/publish and formatting checks pass,
and source/published two-window UI/persistence checks pass (`artifacts\cathedral-source-smoke.log`,
`artifacts\cathedral-published-smoke.log`). The actual backbuffer contains pointed blue glass and matches
the generated stone material (3,072/3,072 sampled pixels); panel Draws verify that room animation time stays frozen.
The inspected room screenshot is `artifacts\gothic-cathedral-room.png`, also generated beside the executable.

Paper-doll validation: **406 tests pass**, formatting and Release build/publish pass.
Source/published two-window UI/persistence checks pass (`artifacts\paperdoll-source-smoke.log`,
`artifacts\paperdoll-published-smoke.log`). The actual renderer produces base/weapon-only/armor-only/combined
variants for all three rarities plus four facing directions; smoke-only equipment is created and equipped
through the real factory/inventory services without touching the player's save. World backbuffer checks
wait for real Draws before advancing equip phases, verify the unarmed, sword-only and armored sword appearances
while the world/pose remains paused, and check the equipped appearance again after a second-session load.
The inspected comparison sheet is `artifacts\paperdoll-equipment.png`; actual world frames are
`artifacts\paperdoll-world-1.png`, `artifacts\paperdoll-world-2.png`, `artifacts\paperdoll-world-8.png`
and `artifacts\paperdoll-restored.png`. These are also generated beside the executable.

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
the console smith button opens both panels, real recipe-selection/detail-button clicks test refusals,
learn both recipes and craft once each through the
official services. Assertions check full production costs, unique instances, real displayed names/rolls,
no automatic equip/healing, retained duplicate recipes, independent smith closing, resource tabs and
harmless resource/equipment hover, with comparisons computed before equipping. Esc closes without exiting
or advancing combat. The console restart button begins the armed round without attack/movement/pickup bleed.
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
Actual smith feedback, names, costs, buttons and resource details are measured and glyph-checked when drawn.
Gothic acceptance samples both brass panel frames, all 32 inset grid cells, full/partial real-health
globe pixels, and measured/wrapped tooltip borders in the actual backbuffer. Hover stages wait for a real
Draw even if MonoGame catches up with multiple Updates. Circular empty/full clipping is also tested
headlessly against generated texture pixels. This does not bypass the full smoke's death-pixel checks.
Real backbuffer samples require visible partial monster/player death bodies;
these are color-presence checks at authoritative anchors, not golden screenshots. Headless controller tests
compile the presentation source directly, without referencing the executable Game project.
The first session uses accelerated simulation time, bounds each named stage to 450 updates and the run to 2,400 updates,
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
new scene. Persistence tests additionally verify exact cross-session restoration, complete rejection of
invalid snapshots, strict/malformed JSON, locked destinations preserving old bytes, and all four autosave
triggers and exit/failure guards. These do not replace window rendering/loaded-font checks. Diagnostic logs remain in English.
自动烟测不能代替人工视觉与手感验收；本次只执行自动化验证，没有进行人工游戏手感或窗口焦点实测。
