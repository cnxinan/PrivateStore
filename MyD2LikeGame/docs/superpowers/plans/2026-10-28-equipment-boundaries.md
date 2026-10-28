# Equipment Boundaries Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 解耦装备身份、属性计算、装备变更和击杀奖励，为之后的装备成长与打造提供可靠边界，不增加玩法。

**Architecture:** 保留不可变ItemInstance和现有InventoryService名称；将纯计算抽为ItemStatCalculator，将InventoryService收敛为绑定内容的操作服务。背包列表是已拥有快照的唯一来源，槽位仅引用ID。独立KillRewardService同步消费击杀记录，保持原随机调用顺序。

**Tech Stack:** C#、.NET SDK9.0.320、MonoGame3.8.6 WindowsDX、xUnit2.9.2；Windows PowerShell；不增加依赖。

## Global Constraints

- 设计依据：`docs\superpowers\specs\2026-10-28-equipment-boundaries-design.md`，已获用户书面批准。
- 只重构装备实例、属性计算、击杀奖励和装备变更四处边界，不新增玩法。
- Gameplay继续独立于MonoGame、Engine和Game。
- 不添加等级、经验、材料、配方、强化字段、存档、UI按钮或空成长接口。
- 不引入ECS、事件总线、全局装备仓库或通用事务框架。
- 保留现有全部战斗/动画规则、每怪必掉一件、武器护甲等权、品质权重1:4:2、固定词缀、背包顺序和换装比较。
- 保留现有随机源及调用顺序；本轮不拆分暴击/掉落随机流，不保证Guid随机身份的跨运行一致。
- 开始实施时保留当前未提交的必掉装备配置、测试和README改动；它们是本轮行为基线，不得恢复为50%掉落。
- 非法参数/内容抛出明确异常；死亡、未拥有、重复拾取等正常拒绝返回明确原因，不宽泛catch。
- 生命上限提高不治疗；降低只把当前生命夹紧到新上限。
- 同ID替换保留InstanceId、DefinitionId和槽位，不隐式重掷词缀。
- 实施使用现有隔离工作区，不创建第二个worktree，不自动push。

---

## 工作区、基线与文件职责

所有命令先进入：

```powershell
Set-Location 'C:\Users\v-ningchen\Source\repos\PrivateStore\.worktrees\darkloot-vertical-slice\MyD2LikeGame'
```

不要进入原始master目录。计划起点设计提交为`5620e99`；本地动画提交已经存在，不重做。

| 文件 | 职责 |
|---|---|
| `src\DarkLoot.Gameplay\Items\ItemInstance.cs` | 保留现有不可变快照，不加成长字段 |
| `src\DarkLoot.Gameplay\Items\ItemStatCalculator.cs`（新建） | 单件校验/贡献、两槽总属性、无副作用预览；同文件定义ItemStatContribution |
| `src\DarkLoot.Gameplay\Items\Inventory.cs` | 有序快照列表、ID槽位、只读查询、内部原子提交 |
| `src\DarkLoot.Gameplay\Items\EquipmentChangeResult.cs`（新建） | 正常操作的明确结果枚举 |
| `src\DarkLoot.Gameplay\Items\InventoryService.cs` | 拾取、穿戴、内部同ID替换；提交前计算完整候选状态 |
| `src\DarkLoot.Gameplay\Combat\PlayerState.cs` | 战斗状态及纯计算器支撑的只读Stats；移除TryEquip |
| `src\DarkLoot.Gameplay\Loot\KillRecord.cs`（新建） | 轮次、怪物实例ID、死亡位置、掉落表ID |
| `src\DarkLoot.Gameplay\Loot\KillRewardService.cs`（新建） | 当轮去重、同步生成奖励；同文件定义结果类型 |
| `src\DarkLoot.Gameplay\Combat\ArenaSimulation.cs` | 协调操作服务、提交击杀记录、放置奖励；不生成随机装备 |
| `src\DarkLoot.Game\InventoryPanel.cs` | 请求穿戴、共享纯预览，不写PlayerState |
| `src\DarkLoot.Game\DarkLootGame.cs` | 向背包UI传入本场景操作服务 |
| `src\DarkLoot.Game\RuntimeSmokeTest.cs` | 保留真实拾取/点击穿戴、比较、死亡和重开断言 |
| `tests\DarkLoot.Tests\Gameplay\InventoryServiceTests.cs` | 迁移已有操作测试，新增身份/原子替换测试；保留名称格式测试 |
| `tests\DarkLoot.Tests\Gameplay\ItemStatCalculatorTests.cs`（新建） | 迁移纯属性与非法实例测试，增加独立预览测试 |
| `tests\DarkLoot.Tests\Gameplay\KillRewardServiceTests.cs`（新建） | 去重、轮次、零掉落、异常与随机消费测试 |
| `tests\DarkLoot.Tests\Gameplay\ArenaSimulationTests.cs` | 地面归属、操作协调与原有战斗回归 |
| `tests\DarkLoot.Tests\Gameplay\MixedEncounterTests.cs` | 多杀即时奖励和随机顺序 |
| `tests\DarkLoot.Tests\Gameplay\PlayerLifecycleTests.cs` | 死亡写入门禁、装备保留与新轮次 |

不增加字典缓存：当前装备数量小，有序列表加按ID查找已满足唯一来源，避免双存储同步。
内部替换仅给Gameplay及测试程序集使用；不向UI增加替换按钮或公共快照写接口。

## Task 1: 抽出纯装备校验、属性贡献与预览

**Files:**
- Create: `src\DarkLoot.Gameplay\Items\ItemStatCalculator.cs`
- Create: `tests\DarkLoot.Tests\Gameplay\ItemStatCalculatorTests.cs`
- Modify: `src\DarkLoot.Gameplay\Items\InventoryService.cs`
- Modify: `src\DarkLoot.Gameplay\Combat\PlayerState.cs`
- Modify: `src\DarkLoot.Gameplay\Combat\ArenaSimulation.cs`（构造器注入）
- Modify: `src\DarkLoot.Game\InventoryPanel.cs`
- Modify: `src\DarkLoot.Game\RuntimeSmokeTest.cs`
- Modify: `tests\DarkLoot.Tests\Gameplay\InventoryServiceTests.cs`

**Interfaces:**
- Consumes: `ItemInstance`, `GameContent`, `ItemSlot`, `PlayerStatTotals`，均为现有类型。
- Produces:

```csharp
public readonly record struct ItemStatContribution(
    ItemSlot Slot, long AttackDamage, long MaximumHealth, long CriticalChance);

public sealed class ItemStatCalculator
{
    public ItemStatContribution GetContribution(ItemInstance item, GameContent content);
    public PlayerStatTotals Calculate(
        int baseAttackDamage, int baseMaximumHealth,
        ItemInstance? weapon, ItemInstance? armor, GameContent content);
    public PlayerStatTotals Preview(
        int baseAttackDamage, int baseMaximumHealth,
        Inventory inventory, GameContent content,
        ItemSlot slot, ItemInstance? replacement);
}
```

- 本任务暂留旧写操作，后续Task 2统一迁移并删除；纯计算旧方法只作短期转发，不保留双份算法。

- [ ] **Step 1: 添加独立计算与无副作用预览测试**

在新测试类中添加下列测试；使用现有`MixedEncounterFixture`，不构造假背包：
测试文件使用`DarkLoot.Data`、`DarkLoot.Data.Definitions`、
`DarkLoot.Gameplay.Items`，命名空间为`DarkLoot.Tests.Gameplay`。
操作测试额外引入`DarkLoot.Gameplay.Combat`，奖励测试额外引入
`System.Numerics`及`DarkLoot.Gameplay.Loot`。

```csharp
[Fact]
public void Calculate_CombinesBothSlotsAndDoesNotMutateSnapshots()
{
    var content = MixedEncounterFixture.CreateContent();
    var weapon = new ItemInstance(
        Guid.NewGuid(), "weapon.training_blade", ItemRarity.Magic,
        [new AffixRoll("affix.keen", ItemStat.Attack, 3)]);
    var armor = new ItemInstance(
        Guid.NewGuid(), "armor.training_vest", ItemRarity.Magic,
        [new AffixRoll("affix.vital", ItemStat.MaximumHealth, 20)]);
    var calculator = new ItemStatCalculator();

    Assert.Equal(new ItemStatContribution(ItemSlot.Weapon, 8, 0, 0),
        calculator.GetContribution(weapon, content));
    Assert.Equal(new PlayerStatTotals(18, 120, 0),
        calculator.Calculate(10, 100, weapon, armor, content));
    Assert.Equal(3, Assert.Single(weapon.Rolls).Value);
    Assert.Equal(20, Assert.Single(armor.Rolls).Value);
}

[Fact]
public void Preview_UsesCandidateSlotWithoutChangingInventory()
{
    var content = MixedEncounterFixture.CreateContent();
    var inventory = new Inventory();
    var candidate = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
    var calculator = new ItemStatCalculator();

    Assert.Equal(new PlayerStatTotals(15, 100, 0),
        calculator.Preview(10, 100, inventory, content, ItemSlot.Weapon, candidate));
    Assert.Empty(inventory.Items);
    Assert.Null(inventory.EquippedWeapon);
    Assert.Throws<ArgumentException>(() =>
        calculator.Calculate(10, 100, null, candidate, content));
}
```

- [ ] **Step 2: 运行RED**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter FullyQualifiedName~ItemStatCalculatorTests --verbosity minimal
```

预期：缺少ItemStatCalculator，编译失败。记录该失败；不能把无测试匹配当成RED。

- [ ] **Step 3: 移动现有校验并实现纯计算**

将InventoryService现有`ValidateInstance`、`IsValidAffix`、`ResolveItemDefinition`
原样移入ItemStatCalculator，维持错误信息和参数名。GetContribution先检查item/content非null、
InstanceId非空，再解析模板、校验词缀，按以下代码累计单件贡献：

```csharp
var definition = ResolveItemDefinition(content, item.DefinitionId);
ValidateInstance(item, definition, content);
long attack = definition.AttackBonus;
long maximumHealth = 0;
long criticalChance = 0;
foreach (var roll in item.Rolls)
{
    switch (roll.Stat)
    {
        case ItemStat.Attack: attack += roll.Value; break;
        case ItemStat.MaximumHealth: maximumHealth += roll.Value; break;
        case ItemStat.CriticalChance: criticalChance += roll.Value; break;
        default:
            throw new ArgumentException(
                $"Item '{item.DefinitionId}' has unsupported stat '{roll.Stat}'.", nameof(content));
    }
}
return new ItemStatContribution(definition.Slot, attack, maximumHealth, criticalChance);
```

Calculate保留原GetTotals的基础值检查、long累加、攻击/生命边界异常和最终暴击夹紧。
用本地Add函数处理两槽，不读写背包：

```csharp
void Add(ItemInstance? item, ItemSlot expectedSlot)
{
    if (item is null) return;
    var contribution = GetContribution(item, content);
    if (contribution.Slot != expectedSlot)
        throw new ArgumentException(
            $"Equipped item '{item.DefinitionId}' does not belong to {expectedSlot}.", nameof(content));
    attack += contribution.AttackDamage;
    maximumHealth += contribution.MaximumHealth;
    criticalChance += contribution.CriticalChance;
}
Add(weapon, ItemSlot.Weapon);
Add(armor, ItemSlot.Armor);
```

Preview检查inventory/content非null、slot属于Weapon/Armor，直接选择候选快照：

```csharp
return Calculate(
    baseAttackDamage, baseMaximumHealth,
    slot == ItemSlot.Weapon ? replacement : inventory.EquippedWeapon,
    slot == ItemSlot.Armor ? replacement : inventory.EquippedArmor,
    content);
```

PlayerState构造器第二参数改成ItemStatCalculator，Stats调用Calculate。
InventoryService旧GetTotals/GetAttackDamage/GetTotalsWithReplacement暂转发给计算器；
旧TryEquip通过GetContribution/Preview校验，不再持有复制的校验逻辑。
ArenaSimulation创建PlayerState时传入计算器，保持本任务可编译。
InventoryPanel的`_service`改为ItemStatCalculator，比较调用Preview。
RuntimeSmokeTest第665行附近的比较改为相同Preview。

- [ ] **Step 4: 迁移纯测试并验证GREEN**

将InventoryServiceTests中总属性、预览、暴击夹紧、非法模板/词缀及数值边界测试迁入
ItemStatCalculatorTests，直接调用Calculate/GetContribution；保留全部实质断言。
新增非法ID、错误部位、缺失词缀、重复词缀、稀有度词缀数、数值范围用例时，
复用已有fixture或最小字典，不改生产数据迁就测试。

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~ItemStatCalculatorTests|FullyQualifiedName~InventoryServiceTests|FullyQualifiedName~ArenaSimulationTests' --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
git --no-pager diff --check
```

预期：相关测试全部通过，格式/diff无错误。格式不一致时对本任务文件执行现有dotnet format后重验。

- [ ] **Step 5: 提交纯计算边界**

仅暂存本任务明确列出的文件（包括ArenaSimulation构造器调用点），不得`git add .`。
提交标题：`refactor: extract pure equipment stat calculation`。使用本文末尾统一提交尾注。

## Task 2: 唯一快照存储与统一拾取/穿戴操作

**Files:**
- Modify: `src\DarkLoot.Gameplay\Items\Inventory.cs`
- Create: `src\DarkLoot.Gameplay\Items\EquipmentChangeResult.cs`
- Modify: `src\DarkLoot.Gameplay\Items\InventoryService.cs`
- Modify: `src\DarkLoot.Gameplay\Combat\PlayerState.cs`
- Modify: `src\DarkLoot.Gameplay\Combat\ArenaSimulation.cs`
- Modify: `src\DarkLoot.Game\InventoryPanel.cs`
- Modify: `src\DarkLoot.Game\DarkLootGame.cs`
- Modify: `tests\DarkLoot.Tests\Gameplay\InventoryServiceTests.cs`
- Modify: `tests\DarkLoot.Tests\Gameplay\ArenaSimulationTests.cs`
- Modify: `tests\DarkLoot.Tests\Gameplay\MixedEncounterTests.cs`
- Modify: `tests\DarkLoot.Tests\Gameplay\PlayerLifecycleTests.cs`

**Interfaces:**
- Consumes: Task 1的ItemStatCalculator。
- Produces:

```csharp
public enum EquipmentChangeResult { Applied, PlayerDead, NotOwned, DuplicateInstance }

public sealed class InventoryService
{
    public InventoryService(GameContent content);
    public EquipmentChangeResult PickUp(PlayerState player, ItemInstance item);
    public EquipmentChangeResult Equip(PlayerState player, Guid instanceId);
}
// ArenaSimulation只读公开本场景服务，移除旧bool TryEquip：
public InventoryService EquipmentOperations { get; }
// Inventory新增，只读对象属性保留但从ID解析：
public Guid? EquippedWeaponId { get; }
public Guid? EquippedArmorId { get; }
```

- [ ] **Step 1: 添加拒绝与原子穿戴测试**

在InventoryServiceTests中加入helper和测试；之后其余操作测试复用helper：

```csharp
private static ArenaSimulation CreateArena(GameContent? content = null) =>
    new(content ?? MixedEncounterFixture.CreateContent(), new Random(17),
        MixedEncounterFixture.CreateEncounter(
            new MonsterSpawnDefinition("target", "monster.training_slime", 500, 270)));

[Fact]
public void PickUp_DuplicateIdKeepsOriginalSnapshotAndOrder()
{
    var arena = CreateArena();
    var original = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
    var other = new ItemInstance(Guid.NewGuid(), "armor.training_vest");
    var duplicate = new ItemInstance(original.InstanceId, "armor.training_vest");
    var operations = arena.EquipmentOperations;

    Assert.Equal(EquipmentChangeResult.Applied, operations.PickUp(arena.Player, original));
    Assert.Equal(EquipmentChangeResult.Applied, operations.PickUp(arena.Player, other));
    Assert.Equal(EquipmentChangeResult.Applied, operations.Equip(arena.Player, original.InstanceId));
    Assert.Equal(EquipmentChangeResult.DuplicateInstance, operations.PickUp(arena.Player, duplicate));
    Assert.Equal(new[] { original, other }, arena.Player.Inventory.Items);
    Assert.Equal(original.InstanceId, arena.Player.Inventory.EquippedWeaponId);
    Assert.Same(original, arena.Player.Inventory.EquippedWeapon);
}

[Fact]
public void Equip_NotOwnedDoesNotChangeHealthOrSlots()
{
    var arena = CreateArena();
    var before = arena.Player.Stats;
    Assert.Equal(EquipmentChangeResult.NotOwned,
        arena.EquipmentOperations.Equip(arena.Player, Guid.NewGuid()));
    Assert.Equal(before, arena.Player.Stats);
    Assert.Equal(100, arena.Player.Health);
    Assert.Null(arena.Player.Inventory.EquippedWeaponId);
    Assert.Null(arena.Player.Inventory.EquippedArmorId);
}
```

迁移已有`TryEquip_ClampsHealthImmediatelyAndNeverHealsWhenMaximumChanges`和
`TryEquip_RejectsHealthOverflowBeforeChangingEquipment`到新操作入口，保留其90/50/150血量、
异常前槽位为空等断言，不用弱化断言消除失败。

- [ ] **Step 2: 运行RED**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~InventoryServiceTests|FullyQualifiedName~ArenaSimulationTests' --verbosity minimal
```

预期：缺少新操作API，编译失败。

- [ ] **Step 3: 改为ID槽位并实现提交前校验**

保留Inventory的List和ReadOnlyCollection。`Find(Guid)`成为public只读查询；
内部Equip改为`Equip(ItemSlot slot, Guid instanceId)`并只写对应ID。
所有入背包快照依然先检查重复ID，集合本身不公开写权限：

```csharp
public Guid? EquippedWeaponId { get; private set; }
public Guid? EquippedArmorId { get; private set; }
public ItemInstance? EquippedWeapon =>
    EquippedWeaponId is { } id ? Find(id) : null;
public ItemInstance? EquippedArmor =>
    EquippedArmorId is { } id ? Find(id) : null;
```

InventoryService构造器校验并保存content，私有持有计算器。实现：

```csharp
public EquipmentChangeResult PickUp(PlayerState player, ItemInstance item)
{
    ArgumentNullException.ThrowIfNull(player);
    ArgumentNullException.ThrowIfNull(item);
    if (item.InstanceId == Guid.Empty)
        throw new ArgumentException("Item instance ID must not be empty.", nameof(item));
    if (player.IsDead) return EquipmentChangeResult.PlayerDead;
    if (player.Inventory.Find(item.InstanceId) is not null)
        return EquipmentChangeResult.DuplicateInstance;
    _ = _calculator.GetContribution(item, _content);
    if (!player.Inventory.TryAdd(item))
        throw new InvalidOperationException("Validated item could not be added.");
    return EquipmentChangeResult.Applied;
}

public EquipmentChangeResult Equip(PlayerState player, Guid instanceId)
{
    ArgumentNullException.ThrowIfNull(player);
    if (instanceId == Guid.Empty)
        throw new ArgumentException("Item instance ID must not be empty.", nameof(instanceId));
    if (player.IsDead) return EquipmentChangeResult.PlayerDead;
    var item = player.Inventory.Find(instanceId);
    if (item is null) return EquipmentChangeResult.NotOwned;
    var slot = _calculator.GetContribution(item, _content).Slot;
    var candidate = _calculator.Preview(
        player.BaseAttackDamage, player.BaseMaximumHealth,
        player.Inventory, _content, slot, item);
    player.Inventory.Equip(slot, instanceId);
    player.Health = Math.Min(player.Health, candidate.MaximumHealth);
    return EquipmentChangeResult.Applied;
}
```

所有可能抛出的内容/属性计算在写槽位之前；提交后不重新调用Stats做可能失败的计算。
删除InventoryService全部旧TryEquip/TryPickUp和计算转发API；删除PlayerState.TryEquip。
ArenaSimulation构造器初始化`EquipmentOperations = new InventoryService(content)`，
移除旧_inventoryService字段及TryEquip转发。

原拾取仅在Applied时删除地面项；正常拒绝明确分支保留地面物品：

```csharp
switch (EquipmentOperations.PickUp(Player, item))
{
    case EquipmentChangeResult.Applied:
        _groundItems.RemoveAt(nearestIndex);
        break;
    case EquipmentChangeResult.PlayerDead:
    case EquipmentChangeResult.DuplicateInstance:
        break;
    default:
        throw new InvalidOperationException("Unexpected pickup result.");
}
```

- [ ] **Step 4: 迁移全部直接调用方**

InventoryPanel.Update增加末尾参数`InventoryService operations`，行点击用以下代码；
Dead门禁仍保留，不增加UI消息或对话框：

```csharp
var result = operations.Equip(player, inventory.Items[row + _firstRow].InstanceId);
switch (result)
{
    case EquipmentChangeResult.Applied:
    case EquipmentChangeResult.PlayerDead:
        break;
    default:
        throw new InvalidOperationException($"Inventory row equip was rejected: {result}.");
}
```

DarkLootGame原调用增加`_arena.EquipmentOperations`。
现有测试将拾取改为
`Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.PickUp(arena.Player, item));`
穿戴改为
`Assert.Equal(EquipmentChangeResult.Applied, arena.EquipmentOperations.Equip(arena.Player, item.InstanceId));`。
死亡测试断言PlayerDead，未拥有断言NotOwned，不再保留对PlayerState.TryEquip的调用。
非法实例现在在PickUp就抛出；把原“先入包后穿戴抛出”的测试前移到入包断言，
同时保留纯计算器对非法实例的覆盖。健康总属性溢出仍发生在穿戴候选计算时。

- [ ] **Step 5: GREEN、旧API扫描与提交**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~InventoryServiceTests|FullyQualifiedName~ItemStatCalculatorTests|FullyQualifiedName~ArenaSimulationTests|FullyQualifiedName~MixedEncounterTests|FullyQualifiedName~PlayerLifecycleTests' --verbosity minimal
rg -n 'TryEquip\(|TryPickUp\(|GetTotalsWithReplacement|new InventoryService\(\)' src tests -g '*.cs'
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
git --no-pager diff --check
```

预期测试通过，rg没有旧API调用（现有测试方法名字可以随后按新操作名称重命名，
不能保留旧生产入口）。明确暂存本任务文件。
提交标题：`refactor: centralize owned equipment mutations`。

## Task 3: 内部同ID快照替换与原子生命协调

**Files:**
- Modify: `src\DarkLoot.Gameplay\Items\Inventory.cs`
- Modify: `src\DarkLoot.Gameplay\Items\InventoryService.cs`
- Modify: `src\DarkLoot.Gameplay\DarkLoot.Gameplay.csproj`
- Modify: `tests\DarkLoot.Tests\Gameplay\InventoryServiceTests.cs`
- Modify: `tests\DarkLoot.Tests\Gameplay\PlayerLifecycleTests.cs`

**Interfaces:**
- Consumes: Task 1计算器、Task 2操作/ID槽位。
- Produces: `internal EquipmentChangeResult InventoryService.ReplaceOwned(PlayerState player, ItemInstance replacement)`。
- 仅向DarkLoot.Tests授予内部可见性，不向Game提供替换API：

```xml
<ItemGroup>
  <InternalsVisibleTo Include="DarkLoot.Tests" />
</ItemGroup>
```

- [ ] **Step 1: 写已穿与未穿快照替换测试**

```csharp
[Fact]
public void ReplaceOwned_EquippedSnapshotUpdatesStatsWithoutChangingIdentityOrOrder()
{
    var arena = CreateArena();
    var operations = arena.EquipmentOperations;
    var original = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
    var sibling = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
    Assert.Equal(EquipmentChangeResult.Applied, operations.PickUp(arena.Player, original));
    Assert.Equal(EquipmentChangeResult.Applied, operations.PickUp(arena.Player, sibling));
    Assert.Equal(EquipmentChangeResult.Applied, operations.Equip(arena.Player, original.InstanceId));
    var replacement = new ItemInstance(original.InstanceId, original.DefinitionId,
        ItemRarity.Magic, [new AffixRoll("affix.keen", ItemStat.Attack, 5)]);

    Assert.Equal(EquipmentChangeResult.Applied, operations.ReplaceOwned(arena.Player, replacement));
    Assert.Equal(original.InstanceId, arena.Player.Inventory.EquippedWeaponId);
    Assert.Same(replacement, arena.Player.Inventory.EquippedWeapon);
    Assert.Equal(new[] { replacement, sibling }, arena.Player.Inventory.Items);
    Assert.Same(sibling, arena.Player.Inventory.Find(sibling.InstanceId));
    Assert.Empty(original.Rolls);
    Assert.Equal(20, arena.Player.AttackDamage);
    Assert.Equal(100, arena.Player.Health);
}

[Fact]
public void ReplaceOwned_UnwornItemDoesNotAffectStatsAndTemplateChangeIsAtomic()
{
    var arena = CreateArena();
    var original = new ItemInstance(Guid.NewGuid(), "weapon.training_blade");
    var operations = arena.EquipmentOperations;
    Assert.Equal(EquipmentChangeResult.Applied, operations.PickUp(arena.Player, original));
    var replacement = new ItemInstance(original.InstanceId, original.DefinitionId,
        ItemRarity.Magic, [new AffixRoll("affix.keen", ItemStat.Attack, 5)]);
    Assert.Equal(EquipmentChangeResult.Applied, operations.ReplaceOwned(arena.Player, replacement));
    Assert.Equal(10, arena.Player.AttackDamage);
    Assert.Throws<ArgumentException>(() => operations.ReplaceOwned(arena.Player,
        new ItemInstance(original.InstanceId, "armor.training_vest")));
    Assert.Same(replacement, arena.Player.Inventory.Find(original.InstanceId));
    Assert.Null(arena.Player.Inventory.EquippedArmorId);
    Assert.Equal(100, arena.Player.Health);
}
```

在InventoryServiceTests复制现有健康词缀fixture的最小内容构造，
将`affix.vital`范围扩为-200..50；同一护甲ID先后使用
`new AffixRoll("affix.vital", ItemStat.MaximumHealth, 50)`、
`new AffixRoll("affix.vital", ItemStat.MaximumHealth, -50)`、
`new AffixRoll("affix.vital", ItemStat.MaximumHealth, -200)`。
断言+50不治疗、-50夹紧、-200候选异常保持旧快照/槽位/血量。
沿用PlayerLifecycleTests真实死亡流程，
在死亡时请求ReplaceOwned应返回PlayerDead且不改快照；不公开Health setter。
再断言未拥有返回NotOwned、空ID及非法词缀异常不改任何状态。

- [ ] **Step 2: RED**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter FullyQualifiedName~InventoryServiceTests --verbosity minimal
```

预期ReplaceOwned不存在；添加内部可见性后再实现。

- [ ] **Step 3: 实现受限替换**

Inventory增加内部`ReplaceSnapshot(ItemInstance replacement)`，在有序列表原索引替换；
找不到时抛InvalidOperationException，不能新增或追加：

```csharp
var index = _items.FindIndex(item => item.InstanceId == replacement.InstanceId);
if (index < 0) throw new InvalidOperationException("Cannot replace an item that is not owned.");
_items[index] = replacement;
```

InventoryService.ReplaceOwned完整流程：

```csharp
ArgumentNullException.ThrowIfNull(player);
ArgumentNullException.ThrowIfNull(replacement);
if (replacement.InstanceId == Guid.Empty)
    throw new ArgumentException("Item instance ID must not be empty.", nameof(replacement));
if (player.IsDead) return EquipmentChangeResult.PlayerDead;
var original = player.Inventory.Find(replacement.InstanceId);
if (original is null) return EquipmentChangeResult.NotOwned;
if (!string.Equals(original.DefinitionId, replacement.DefinitionId, StringComparison.Ordinal))
    throw new ArgumentException("Replacement must preserve the item definition.", nameof(replacement));
var oldSlot = _calculator.GetContribution(original, _content).Slot;
var newSlot = _calculator.GetContribution(replacement, _content).Slot;
if (oldSlot != newSlot)
    throw new ArgumentException("Replacement must preserve the item slot.", nameof(replacement));
var inventory = player.Inventory;
var equipped = inventory.EquippedWeaponId == replacement.InstanceId ||
    inventory.EquippedArmorId == replacement.InstanceId;
var candidate = equipped
    ? _calculator.Preview(player.BaseAttackDamage, player.BaseMaximumHealth,
        inventory, _content, newSlot, replacement)
    : _calculator.Calculate(player.BaseAttackDamage, player.BaseMaximumHealth,
        inventory.EquippedWeapon, inventory.EquippedArmor, _content);
inventory.ReplaceSnapshot(replacement);
player.Health = Math.Min(player.Health, candidate.MaximumHealth);
return EquipmentChangeResult.Applied;
```

不复制rolls、不调用Random、不对旧record修改属性。提交后的ID槽位解析必须立即返回新快照。

- [ ] **Step 4: GREEN与提交**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~InventoryServiceTests|FullyQualifiedName~ItemStatCalculatorTests|FullyQualifiedName~PlayerLifecycleTests' --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
git --no-pager diff --check
```

提交标题：`refactor: add atomic owned item snapshot replacement`。

## Task 4: 独立击杀奖励结算与当轮去重

**Files:**
- Create: `src\DarkLoot.Gameplay\Loot\KillRecord.cs`
- Create: `src\DarkLoot.Gameplay\Loot\KillRewardService.cs`
- Create: `tests\DarkLoot.Tests\Gameplay\KillRewardServiceTests.cs`

**Interfaces:**
- Consumes: 原有`LootGenerator.Generate(LootTableDefinition, GameContent, Random)`，不修改生成器。
- Produces:

```csharp
public sealed record KillRecord(
    long RoundId, string MonsterInstanceId, Vector2 DeathPosition, string LootTableId);
public enum KillRewardStatus { Granted, AlreadyResolved, WrongRound }
public sealed record KillRewardResult(
    KillRewardStatus Status, IReadOnlyList<ItemInstance> Items);
public sealed class KillRewardService
{
    public KillRewardService(GameContent content, Random random);
    public void BeginRound(long roundId);
    public KillRewardResult Resolve(KillRecord kill);
}
```

RoundId从0开始，只允许BeginRound严格增加；Resolve在首次BeginRound之前抛InvalidOperationException。
任意非当前轮次返回WrongRound且不消费随机；只保存当前轮次HashSet<string>。
不公开去重集合或为测试新增计数属性。

- [ ] **Step 1: 写去重与随机消费测试**

```csharp
private sealed class CountingRandom : Random
{
    public int Calls { get; private set; }
    public override long NextInt64(long minValue, long maxValue)
    {
        Calls++;
        return minValue;
    }
    public override long NextInt64(long maxValue)
    {
        Calls++;
        return 0;
    }
}

[Fact]
public void Resolve_DuplicateAndStaleKillsDoNotConsumeRandomness()
{
    var content = MixedEncounterFixture.CreateContent();
    var random = new CountingRandom();
    var service = new KillRewardService(content, random);
    var kill = new KillRecord(0, "slime.1", new Vector2(190, 270), "loot.training_empty");
    service.BeginRound(0);
    Assert.Equal(KillRewardStatus.Granted, service.Resolve(kill).Status);
    var afterFirst = random.Calls;
    Assert.Equal(KillRewardStatus.AlreadyResolved, service.Resolve(kill).Status);
    Assert.Equal(afterFirst, random.Calls);
    service.BeginRound(1);
    Assert.Equal(KillRewardStatus.WrongRound, service.Resolve(kill).Status);
    Assert.Equal(afterFirst, random.Calls);
    Assert.Equal(KillRewardStatus.Granted, service.Resolve(kill with { RoundId = 1 }).Status);
    Assert.True(random.Calls > afterFirst);
}

[Fact]
public void Resolve_GenerationExceptionDoesNotRecordSuccess()
{
    var fixture = MixedEncounterFixture.CreateContent();
    var tables = fixture.LootTables.ToDictionary(pair => pair.Key, pair => pair.Value);
    tables["broken"] = new LootTableDefinition(
        "broken", 1, 1, [new LootEntryDefinition("missing", 1)]);
    var content = new GameContent(fixture.Items, fixture.Monsters, tables, fixture.Affixes);
    var service = new KillRewardService(content, new Random(17));
    service.BeginRound(0);
    var kill = new KillRecord(0, "slime.1", Vector2.Zero, "broken");
    Assert.Throws<ArgumentException>(() => service.Resolve(kill));
    var retried = service.Resolve(kill with { LootTableId = "loot.training_empty" });
    Assert.Equal(KillRewardStatus.Granted, retried.Status);
    Assert.Empty(retried.Items);
}
```

增加非零表的两个怪物实例各得一件、重复返回空结果、缺失表异常、
NaN位置/空实例/空表ID/负轮次异常测试。对BeginRound增加同值/倒退拒绝且不清除当轮去重的测试。
循环100轮每轮同实例首次Granted、再次AlreadyResolved、旧轮WrongRound，验证清理语义；
代码审阅确认HashSet只存当轮，而不是用内存估算测试。

- [ ] **Step 2: RED**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter FullyQualifiedName~KillRewardServiceTests --verbosity minimal
```

预期新类型缺失。

- [ ] **Step 3: 实现服务与显式失败语义**

KillRecord文件使用System.Numerics.Vector2。KillRewardService保存content、同一个Random、
LootGenerator、`long? _roundId`和`HashSet<string> _resolved = new(StringComparer.Ordinal)`。
构造器对content/random检查非null。

```csharp
public void BeginRound(long roundId)
{
    if (roundId < 0 || (_roundId is { } previous && roundId <= previous))
        throw new ArgumentOutOfRangeException(nameof(roundId), "Round IDs must increase.");
    _resolved.Clear();
    _roundId = roundId;
}
```

Resolve先校验非null、非负RoundId、非空两个ID、有限DeathPosition；
若尚未BeginRound抛InvalidOperationException。然后：

```csharp
if (kill.RoundId != _roundId)
    return new KillRewardResult(KillRewardStatus.WrongRound, Array.Empty<ItemInstance>());
if (_resolved.Contains(kill.MonsterInstanceId))
    return new KillRewardResult(KillRewardStatus.AlreadyResolved, Array.Empty<ItemInstance>());
if (!_content.LootTables.TryGetValue(kill.LootTableId, out var table) || table is null)
    throw new ArgumentException($"Loot table '{kill.LootTableId}' was not found.", nameof(kill));
var items = _generator.Generate(table, _content, _random);
_resolved.Add(kill.MonsterInstanceId);
return new KillRewardResult(KillRewardStatus.Granted, items);
```

不catch生成异常，不承诺回滚已消费的随机数，但异常后不能标为已结算。
合法零掉落仍Granted，再次消费才AlreadyResolved。

- [ ] **Step 4: GREEN与提交**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~KillRewardServiceTests|FullyQualifiedName~LootGeneratorTests' --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
git --no-pager diff --check
```

提交标题：`refactor: isolate round scoped kill reward resolution`。

## Task 5: 接入战斗、锁定随机顺序与完整验收

**Files:**
- Modify: `src\DarkLoot.Gameplay\Combat\ArenaSimulation.cs`
- Modify: `tests\DarkLoot.Tests\Gameplay\MixedEncounterTests.cs`
- Modify: `tests\DarkLoot.Tests\Gameplay\ArenaSimulationTests.cs`
- Modify: `tests\DarkLoot.Tests\Gameplay\PlayerLifecycleTests.cs`
- Modify: `src\DarkLoot.Game\RuntimeSmokeTest.cs`（仅API迁移遗漏，不削减断言）
- Modify: `README.md`（在现有必掉说明上补充模块职责）

**Interfaces:**
- Consumes: KillRewardService及KillRecord；EquipmentOperations。
- Produces: ArenaSimulation使用现有ResetSequence作为奖励RoundId，无第二个重复计数器。
- 构造时BeginRound(0)，每次成功R/复活ResetSequence增加后BeginRound(ResetSequence)；
  死亡期间R不增加、不清理奖励记录。

- [ ] **Step 1: 先锁定现有随机调用顺序与拾取归属**

在MixedEncounterTests添加记录随机调用的fixture，两只1HP同模板怪物都在正面，
装备现有50%暴击武器，使用无品质配置的必掉一件测试表。
脚本随机数返回每个区间最小值并记录调用类型：

```csharp
private sealed class OrderedRandom : Random
{
    public List<string> Calls { get; } = [];
    public override int Next(int maxValue)
    {
        Calls.Add($"crit:{maxValue}");
        return 0;
    }
    public override long NextInt64(long minValue, long maxValue)
    {
        Calls.Add($"count:{minValue}:{maxValue}");
        return minValue;
    }
    public override long NextInt64(long maxValue)
    {
        Calls.Add($"entry:{maxValue}");
        return 0;
    }
}
```

新增测试的完整内容构造和断言如下：

```csharp
var fixture = MixedEncounterFixture.CreateContent();
var monsters = fixture.Monsters.ToDictionary(pair => pair.Key, pair => pair.Value);
monsters["monster.training_slime"] = monsters["monster.training_slime"] with
{
    MaxHealth = 1,
    LootTableId = "loot.order"
};
var tables = fixture.LootTables.ToDictionary(pair => pair.Key, pair => pair.Value);
tables["loot.order"] = new LootTableDefinition(
    "loot.order", 1, 1, [new LootEntryDefinition("weapon.training_blade", 1)]);
var content = new DarkLoot.Data.GameContent(fixture.Items, monsters, tables, fixture.Affixes);
var random = new OrderedRandom();
var arena = new ArenaSimulation(content, random,
    MixedEncounterFixture.CreateEncounter(
        new MonsterSpawnDefinition("first", "monster.training_slime", 190, 265),
        new MonsterSpawnDefinition("second", "monster.training_slime", 190, 275)));
var weapon = new ItemInstance(
    Guid.NewGuid(), "weapon.training_blade", ItemRarity.Magic,
    [new AffixRoll("affix.test_crit", ItemStat.CriticalChance, 50)]);
Assert.Equal(EquipmentChangeResult.Applied,
    arena.EquipmentOperations.PickUp(arena.Player, weapon));
Assert.Equal(EquipmentChangeResult.Applied,
    arena.EquipmentOperations.Equip(arena.Player, weapon.InstanceId));
arena.Update(new PlayerIntent(Vector2.Zero, new Vector2(300, 270), true, false), 0);
Assert.Equal(new[] {
    "crit:100", "count:1:2", "entry:1",
    "crit:100", "count:1:2", "entry:1"
}, random.Calls);
Assert.Equal(new[] { "first", "second" },
    arena.LastSwingHits.Select(hit => hit.MonsterInstanceId));
Assert.Equal(new[] { new Vector2(190, 265), new Vector2(190, 275) },
    arena.GroundItems.Select(drop => drop.Position));
Assert.Equal(2, arena.GroundItems.Count);
```

此测试是重构前通过的特征测试，不伪称RED。再在ArenaSimulationTests
增加独立拒绝拾取测试，复用该类已有fixture及AdvanceUntilDefeated：

```csharp
var simulation = CreateSingleTargetArena(CreateContent(), new Random(17));
AdvanceUntilDefeated(simulation);
var drop = Assert.Single(simulation.GroundItems);
Assert.Equal(EquipmentChangeResult.Applied,
    simulation.EquipmentOperations.PickUp(simulation.Player, drop.Item));
var offset = drop.Position - simulation.Player.Position;
simulation.Update(new PlayerIntent(offset, drop.Position, false, false), offset.Length() / 180f);
simulation.Update(new PlayerIntent(Vector2.Zero, drop.Position, false, true), 0);
Assert.Same(drop, Assert.Single(simulation.GroundItems));
Assert.Same(drop.Item, Assert.Single(simulation.Player.Inventory.Items));
```

以独立测试保存重复拾取场景，不能改变原成功拾取测试。
复用原有R/复活装备保留测试，增加新轮次同配置怪物再次掉落的断言。

- [ ] **Step 2: 验证特征测试，并接入奖励服务**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~MixedEncounterTests|FullyQualifiedName~ArenaSimulationTests|FullyQualifiedName~PlayerLifecycleTests' --verbosity minimal
```

新增场景在旧直接生成路径就应通过。随后移除ArenaSimulation的LootGenerator字段，
新增KillRewardService，在构造器用传入的同一random创建并BeginRound(0)。
替换GenerateMonsterLoot为只协调记录/位置的方法：

```csharp
private void ResolveMonsterRewards(MonsterState monster)
{
    var kill = new KillRecord(
        ResetSequence, monster.InstanceId, monster.Position, monster.Definition.LootTableId);
    var result = _killRewards.Resolve(kill);
    if (result.Status != KillRewardStatus.Granted)
        throw new InvalidOperationException($"Fresh monster kill was rejected: {result.Status}.");
    foreach (var item in result.Items)
        _groundItems.Add(new GroundItem(item, kill.DeathPosition));
}
```

在原死亡分支同步调用，不移动到下一帧或先收集全部击杀后批量处理。
先成功取得完整奖励再标记`monster.DeathResolved = true`；保留Damaged/Died事件顺序，
死亡动画仍与奖励无关。内容异常直接终止当前更新，不吞异常伪装成完整成功。
ResetEncounter的存活门禁之后将ResetSequence增加改为checked运算，
紧接`_killRewards.BeginRound(ResetSequence)`；复活沿用既有ResetEncounter调用。
不改变ResetSequence的展示用途。

- [ ] **Step 3: 验证接入后行为及文档**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~KillRewardServiceTests|FullyQualifiedName~InventoryServiceTests|FullyQualifiedName~ItemStatCalculatorTests|FullyQualifiedName~MixedEncounterTests|FullyQualifiedName~ArenaSimulationTests|FullyQualifiedName~PlayerLifecycleTests|FullyQualifiedName~LootGeneratorTests' --verbosity minimal
```

预期全部通过，特别是crit/count/entry交错顺序完全相同。
README保留“每怪必掉一件”，仅在既有结构说明添加：

```text
ItemStatCalculator统一计算实际属性与换装预览；
InventoryService统一处理拾取、穿戴及内部同ID快照替换；
KillRewardService按战斗轮次和怪物实例去重结算装备奖励。
本轮未引入装备经验、打造或存档。
```

- [ ] **Step 4: 完整构建、测试与格式验收**

```powershell
dotnet build DarkLoot.sln -c Release --no-restore --verbosity minimal
dotnet test tests\DarkLoot.Tests -c Release --no-build --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
git --no-pager diff --check
```

预期Release构建0警告0错误、完整测试全部通过、格式与空白检查通过。
测试数不是固定目标；记录实际数量。仅在缺少依赖错误出现时restore，不预先安装工具。

- [ ] **Step 5: 普通与发布版真实窗口smoke**

```powershell
dotnet run --project src\DarkLoot.Game -c Release --no-build -- --smoke-test
dotnet publish src\DarkLoot.Game -c Release --no-restore -o artifacts\publish
$p = Start-Process -FilePath .\artifacts\publish\DarkLoot.Game.exe -ArgumentList '--smoke-test' -Wait -PassThru
if ($p.ExitCode -ne 0) { throw "Published smoke exited $($p.ExitCode)." }
```

运行时仍走真实地面拾取、背包行点击穿戴、属性比较、死亡/复活、R重开和中文布局断言。
如无图形适配器，再尝试发布版以区分环境与打包，但不反复重跑；
记录`NoSuitableGraphicsDeviceException: No graphics adapters were found`及失败阶段，
不能称窗口验收通过，不改变smoke为静默跳过。手动视觉体验未执行时明确说明。

- [ ] **Step 6: 提交接入变更并保留可交付基线**

提交前明确查看`git status --short`与全部diff。本任务暂存ArenaSimulation、相关测试、
必要smoke迁移及README本轮新增段落；README原有必掉段落若一起提交，
必须同时将原有JSON和LootGeneratorTests必掉改动纳入单独的基线提交，不能只提交说明。
推荐先创建独立基线提交：

```powershell
git add -- content\definitions\loot-tables\training-slime.json tests\DarkLoot.Tests\Gameplay\LootGeneratorTests.cs
```

README用`git diff`确认后将完整必掉与模块说明留在最终接入提交，
基线提交标题`fix: guarantee one equipment drop per monster`；
最终提交标题`refactor: route combat kills through reward service`。
两者都带统一尾注。不得暂存无关文件。
若用户新增了与这些文件冲突的修改，停止并请用户选择，不覆盖。

## 提交约定与交付

每次提交明确列出文件；命令附加同一尾注，例如：

```powershell
git commit -m 'refactor: extract pure equipment stat calculation' -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: 62788275-ec02-4460-a11a-bd9349f184ea"
```

实施完成后检查git状态、提交历史和差异；不push。
最终说明：四个边界如何变化、测试实际结果、窗口验证是否受环境限制。
不宣称装备成长或打造已完成。

## 计划自检映射

| 设计验收 | 任务 |
|---|---|
| 不可变快照、同模板多实例、顺序、ID槽位最新解析 | 2、3 |
| 单件校验、纯总属性/比较、词缀与数值保护 | 1 |
| 拾取/穿戴/替换统一入口、无旧写API、生命原子协调 | 2、3 |
| 拾取失败保留地面物品、成功保留身份/词缀 | 2、5及原smoke |
| 两槽替换、死亡门禁、失败不改归属/血量 | 2、3及生命周期回归 |
| 当轮去重、新轮可再掉、拒绝旧轮、集合有界 | 4、5 |
| 零掉落成功、生成异常不去重、重复不消费随机 | 4 |
| 死亡即时奖励、不依赖动画事件、随机交错顺序 | 5 |
| 必掉基线、旧战斗/动画行为、真实窗口诚实验证 | 5 |
