---
name: developing-myd2likegame
description: Use when implementing, debugging, reviewing, or extending MyD2LikeGame/DarkLoot, especially MonoGame combat, UI, inventory, quests, persistence, animation, or pixelfx-inspired visual effects.
---

# MyD2LikeGame 编码指引

以现有玩法为依据，小步实现并验证。哥特式暗黑美术采用原创表达；特效参考 pixelfx 的形状、色阶、运动和层次，不让表现代码决定战斗结果。

## 开始编码

先定位包含 `DarkLoot.sln` 的 MyD2LikeGame 目录；下文游戏路径均相对它。读取 `README.md`、相关代码和测试，查看 `git status`；保留已有改动，不自动提交、推送或换工作树。框架与依赖版本以 `global.json`、项目文件为准。

| 改动 | 优先接入位置 |
|---|---|
| 战斗、碰撞、投射物、死亡 | `src\DarkLoot.Gameplay\Combat` |
| 掉落、资源、装备、打造、任务 | `src\DarkLoot.Gameplay` 下对应服务；不要在面板中扣料或发奖 |
| 存档 | `src\DarkLoot.Gameplay\Persistence`；完整候选校验与原子替换 |
| 游戏通用姿态、数学、绘制工具 | `src\DarkLoot.Engine` |
| 游戏专属外观、动画、特效 | `src\DarkLoot.Game`；先看 `ActorPresentation.cs`、`ActorRenderer.cs`、`ArenaRenderer.cs` |
| 输入、面板、暂停 | `src\DarkLoot.Game` 中的 `DarkLootGame.cs`、`PanelController.cs`、`UiLayout.cs` |
| 数据与中文 | `content\definitions`、`src\DarkLoot.Data`；界面文案放 `GameText.cs` |

Game 项目禁用隐式 using，须明确导入命名空间。Gameplay 使用 `System.Numerics`；MonoGame 绘制使用 `Microsoft.Xna.Framework`，区分同名类型。测试链接部分 Game 源文件；新增依赖时维护 `tests\DarkLoot.Tests\DarkLoot.Tests.csproj`。

## 不变量

- Gameplay 是生命、攻击、命中、掉落、任务及装备的唯一事实来源。先复用服务与验证逻辑；失败须明确返回状态或抛出错误，不部分修改资源。
- 所有面板及开关过渡帧暂停世界；生产窗口失焦也暂停。专用窗口 smoke 已有的自动化焦点豁免不得扩展到正常游戏。UI 点击不穿透攻击、拾取、移动或重开。保持界面不显示快捷键提示。
- 普通教堂/墓穴往返不治疗、不重置遭遇；死亡和重开保留拥有的进度。任务奖励只能领取一次，交付材料看实际库存。
- 恢复存档先校验整个候选，再变更状态；修改格式要处理旧档兼容。损坏或写入失败明确报错，不覆盖旧档或在退出时悄悄重试。
- 换装立即改变人物外观，但不推进暂停中的姿态。新增中文须补 `src\DarkLoot.Game\Content\Fonts\UIFont.spritefont` 的精确字符范围，不用替代字形掩盖缺字。
- 运行时资源从 `AppContext.BaseDirectory` 定位，构建与发布都要包含所需文件。不要依赖开发机工作目录。

## 特效参考 pixelfx

本机检出目录为 `C:\Users\v-ningchen\Source\repos\pixelfx`，上游为 `https://github.com/patch-gg/pixelfx`。其他机器缺少该目录时，先确认检出位置；不得声称已经参考或擅自把整个库复制进游戏。

先查 `docs\CATALOG.md` 和 `docs\AUTHORING.md`，读具体效果，再渲染预览。以下是起点，不是必须照搬的外观：

| 需求 | pixelfx 源码 |
|---|---|
| 剑弧、拖尾、刃尖火花 | `src\fx\melee\sword-slash.js` |
| 普通命中、暴击 | `src\fx\impacts\hit-spark.js`、`src\fx\impacts\crit-hit.js` |
| 魔法弹、紫色能量球 | `src\fx\projectiles\magic-missile.js`、`src\fx\arcane\arcane-orb.js` |
| 火光、环境循环 | `src\fx\fire`；搜索具体灯火效果 |
| 种子随机、粒子、色阶、分层 | `src\core\sim.js`、`src\core\color.js`、`src\core\painter.js` |

Node 18+ 可运行这些查找、PNG 渲染与检查工具，无需先执行 npm install；GIF/MP4 另需 ffmpeg。一个可复用的预览例子：

```powershell
Set-Location 'C:\Users\v-ningchen\Source\repos\pixelfx'
node tools\list.mjs slash
node tools\render.mjs melee/sword-slash --seed 2 --all --no-actor
node tools\check.mjs melee/sword-slash
```

打开 `records\melee\sword-slash\contact.png` 检查轮廓、峰值帧和尾迹。`melee/sword-slash` 是效果 ID，斜杠不是 Windows 文件路径。若选精灵表，使用 `node tools\render.mjs melee/sword-slash --sheet --no-actor`；输出同目录的 `sword-slash.png` 和 `sword-slash.json`。

**默认原生 C# / MonoGame 实现**：参考少量效果的算法，用现有绘制工具重现；不引入 JS 运行时或照抄浏览器画布接口。固定帧的效果可选择效果专属精灵表，遵循 JSON 中的帧时长、循环和 pivot；保持角色为本游戏人物，不导入包含演示角色的整段 act。

保持清晰像素轮廓、离散色阶、快速爆发与逐渐消散、少量次级粒子。使用 PointClamp；像素素材优先整数倍缩放。冷色魔法、暖色火光融入现有哥特房间，避免大面积霓虹和遮挡目标。区分色彩透明与加法光层，精灵表已烘焙的光晕不要重复叠加。

**接入契约**：

1. `ArenaSimulation.Events` 的 `Swing`、`Damaged`、`ArrowLaunched`、`Died` 是事件起点。参考 `ActorPresentation.Update` 的引用身份去重：只保留最近读取事件批次的引用，重复读取同一事件不重播，后续值相同的新事件仍有效；Reset 时将当前批次标记已读，避免重播旧事件。挥空可有剑弧，但命中火花只随真实伤害出现。
2. 时间仅在 `PanelFrame.AdvanceWorld` 允许的 Update 推进，Draw 只绘制。随机使用独立的视觉种子，不消耗战斗、掉落或打造 RNG；模拟型特效必要时用确定性的子步进。
3. 朝向、出生点与目标来自真实事件/投射物；缓存贴图，限制实例和粒子数量，结束后回收。原生版本与 pixelfx 预览在相同尺度对比。
4. 已接受的重开、复活及切图按现有表现 Reset 边界清理临时效果，屏蔽旧事件。切图后从目标场景的存活投射物重新关联持续表现，不重播旧发射火花；普通往返或射手死亡不能删除真实箭矢。`Died` 可以触发死亡效果，但不是清空所有表现的信号，死亡淡出仍遵循实际死亡阶段。特效实例、粒子和随机种子不进入进度存档。
5. pixelfx 的 act、演示前摇和全局 hit-stop **不直接照搬**：它们会改变攻击、蓄力、冷却及死亡时序。仅借鉴视觉节奏；显式要求玩法时序变化时，另行确认设计并添加相应测试。

**授权**：先读 pixelfx `LICENSE`、`NOTICE.md` 和具体文件的署名。移植代码或分发导出的效果时保留适用版权、MIT 许可和来源说明。`gallery\fonts` 的 Patch Pixel 字体不受仓库 MIT 许可覆盖，须另查 `gallery\fonts\NOTICE.md`；不要把字体或其他未确认资产当作免费素材。不要复制 Diablo 的角色、素材或标志。

## 验证与交付

行为改动先写会失败的针对性测试，再最小实现；按变更范围选择测试，不为纯文档改动跑游戏全套。特效重点测事件只生成一次、重复值的新事件有效、暂停不计时、R/切图清理、视觉随机不影响玩法、循环连续和一次性效果结束为空。

在 MyD2LikeGame 目录运行必要检查：

```powershell
dotnet test DarkLoot.sln -c Release --no-restore
dotnet format DarkLoot.sln --no-restore --verify-no-changes
dotnet build DarkLoot.sln -c Release --no-restore
dotnet publish src\DarkLoot.Game -c Release --no-restore --output artifacts\publish
```

先运行覆盖改动的最小测试；缺依赖时再恢复。格式需要修正时运行 `dotnet format DarkLoot.sln --no-restore`，不要假设 `--include` 一个目录会递归格式化。

视觉改动还需真实窗口：`--smoke-test` 覆盖战斗/人物/暂停，`--dungeon-smoke-test` 覆盖地牢，`--quest-smoke-test` 覆盖委托，`--persistence-smoke-test` 覆盖存读。依据改动选择相关场景，图形 smoke 串行运行，使用独立临时存档，不碰玩家真实存档。

发布版用 `Start-Process -Wait -PassThru` 获取 WinExe 的实际 ExitCode。看实际截图或连续帧，验证方向、墙柱遮挡、命中位置、峰值帧、暂停和结束；编译成功不能代替视觉验收。若图形环境不可用，明确说明未验证，不弱化已有 smoke 断言。只报告已经完成的测试和证据。

## 常见误区

| 误区 | 正确处理 |
|---|---|
| 看过图库就算参考，单张截图好看就算完成 | 读具体源文件，检查完整运动，再看实际游戏窗口 |
| 在 Draw 计时、生成粒子或结算伤害 | 在受世界暂停门控制的 Update 管理表现；Gameplay 结算伤害 |
| 值相等事件永久去重，或每帧扫描尸体发奖励 | 引用身份去重每批真实事件；奖励仍走已有 Gameplay 服务 |
| 直接导入 pixelfx act、字体或整个 npm 库 | 保留原人物与战斗时序，选原生实现或效果专属精灵表，逐项确认授权 |
