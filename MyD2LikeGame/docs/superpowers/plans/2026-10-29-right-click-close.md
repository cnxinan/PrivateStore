# Right Click Close Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 右键按下关闭背包和铁匠，且关闭帧不穿透。

**Architecture:** GameInput捕获右键边沿，PanelController统一关闭当前面板，复用既有PanelFrame转换门禁。

**Tech Stack:** C#/.NET9、MonoGame、xUnit，Windows PowerShell，无新依赖。

## Global Constraints

- 设计：`docs\superpowers\specs\2026-10-29-right-click-close-design.md`。
- 按住不重复触发。没有面板时右键不执行额外操作，不退出游戏，不阻断正常战斗输入。
- 右键关闭优先于同帧I/F及左键面板操作；Esc保持原优先级及退出语义。
- 关闭帧不推进世界、不执行面板动作、不攻击/移动/拾取/重开。
- 不推送、不创建新worktree、不改无关功能。

---

### Task 1: 输入、路由和提示

**Files:** 修改
`src\DarkLoot.Engine\Input\GameInput.cs`（追加边沿字段），
`src\DarkLoot.Game\PanelController.cs`（统一路由），
`src\DarkLoot.Game\GameText.cs`、`README.md`（提示），
`tests\DarkLoot.Tests\Engine\InputAndFrameRateTests.cs`、
`tests\DarkLoot.Tests\Game\PanelControllerTests.cs`（行为测试）。

**Interfaces:** GameInputSnapshot末尾追加`bool ClosePanelPressed = false`，其他签名不变。

- [ ] 写RED测试

在PanelControllerTests使用既有Idle和CreateArena：

```csharp
[Theory]
[InlineData(false)]
[InlineData(true)]
public void RightClick_ClosesEitherPanelWithoutInputBleed(bool smith)
{
    var arena = CreateArena();
    var controller = new PanelController();
    controller.Route(Idle with {
        InteractPressed = smith, InventoryTogglePressed = !smith
    }, arena.Player, arena.BlacksmithOperations);
    var frame = controller.Route(Idle with {
        ClosePanelPressed = true, AttackPressed = true, AttackHeld = true,
        InventoryTogglePressed = true, InteractPressed = true,
        PickupPressed = true, RestartPressed = true
    }, arena.Player, arena.BlacksmithOperations);
    Assert.Equal(PanelKind.None, frame.ActivePanel);
    Assert.False(frame.AdvanceWorld);
    Assert.False(frame.HandlePanelActions);
    Assert.False(frame.RestartRequested);
    Assert.False(frame.ExitRequested);
}
```

另测无面板右键+RestartPressed不退出、AdvanceWorld/RestartRequested为true；
无面板右键+Esc仍退出。输入测试构造MouseState
`new(100,100,0,Released,Released,Pressed,Released,Released)`，
其按钮顺序是left/middle/right/x1/x2；用ButtonState完整枚举值。
Capture首次true、按住false、松开false、再次按下true。
验证失焦期间Capture更新右键边沿，恢复不重放，沿用WindowActivityGate测试模式。

- [ ] 运行RED

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~PanelControllerTests|FullyQualifiedName~InputAndFrameRateTests' --verbosity minimal
```

预期缺少ClosePanelPressed字段，编译失败。

- [ ] 最小实现

GameInputSnapshot尾部增加字段，Capture构造尾部增加：

```csharp
mouse.RightButton == ButtonState.Pressed &&
    _previousMouse.RightButton == ButtonState.Released
```

PanelController在Esc分支之后、I分支之前插入：

```csharp
if (input.ClosePanelPressed && ActivePanel != PanelKind.None)
{
    ActivePanel = PanelKind.None;
    return new(ActivePanel, false, false, false, false, null);
}
```

死亡分支保持不变。宿主已通过PanelFrame阻断转换帧世界与面板动作，无需重复处理。
GameText.Paused的关闭键改`I / Esc / 右键`，
SmithPaused改`F / Esc / 右键`；README加右键关闭说明。
这些汉字已有字体，沿用现有字符覆盖测试；不扩写HUD长行。

- [ ] GREEN与验证

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~PanelControllerTests|FullyQualifiedName~InputAndFrameRateTests|FullyQualifiedName~FinalAcceptanceTests' --verbosity minimal
dotnet build src\DarkLoot.Game -c Release --no-restore --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
git --no-pager diff --check
```

在现有隔离项目目录运行；预期全部通过、构建零警告。无需重新尝试已知无显卡窗口，
不能声称人工交互验收通过。代码审查直接检查小diff，不另派代理。

- [ ] 明确暂存并提交

仅本任务六个文件，提交附加尾注：

```text
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 62788275-ec02-4460-a11a-bd9349f184ea
```

不合并、不推送，保持当前分支。
