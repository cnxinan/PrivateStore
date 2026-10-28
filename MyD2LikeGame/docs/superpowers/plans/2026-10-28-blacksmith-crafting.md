# Blacksmith Crafting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现“怪物掉材料和配方、铁匠学习配方、消耗材料打造装备”的可试玩闭环。

**Architecture:** 装备实例背包保持不变，增加可堆叠资源袋和铁匠已学配方状态。提取固定品质装备工厂；资源奖励、学习/打造和输入路由各有单一入口。先预计算并校验，再提交归属与扣费，不开放UI写集合。

**Tech Stack:** C#、SDK9.0.320、.NET9、MonoGame3.8.6 WindowsDX、xUnit2.9.2；Windows PowerShell，不加依赖。

## Global Constraints

- 设计依据：`docs\superpowers\specs\2026-10-28-blacksmith-crafting-design.md`，用户已批准。
- 本轮以可完整试玩的最小闭环交付，不引入装备经验/等级、金币、打造失败、打造等待时间、出售、分解、批量打造、存档或通用物品框架。
- 退出游戏清空进度；“永久学会”仅指当前运行期间。
- 死亡和R重开保留全部已拥有装备、资源数量和铁匠已学配方。
- Gameplay继续独立于MonoGame、Engine和Game。
- 先前“每怪必掉一件装备”是本次明确替换的旧行为，不再作为生产规则。
- 每只怪物在材料之外，独立有20%概率掉1张配方。四张配方等概率，允许重复。
- 同一件稀有装备的两条词缀不重复，使用现有词缀池、数值范围、命名和属性计算。
- 打造100%成功、立即完成，无金币费用。每次成功创建新的非空InstanceId，不复用或改写旧装备，不自动穿戴，不治疗。
- 沿用现有共享Random，不在本轮拆分暴击、资源和打造随机流。
- 固定品质打造直接生成词缀，不额外抽品质；正常门禁/材料拒绝不消耗随机数。
- 不以“先扣材料，异常时再加回”的回滚拼凑原子性。
- 非法内容显式报错；普通拒绝显示中文原因，不宽泛catch。
- 实施使用已有隔离worktree，不自动push；每任务测试、审查并明确暂存文件。

---

## 工作区与交付文件

基线：设计提交`09d43b5`，已推送代码基线`ba231d0`，此前220项测试。
所有命令先执行：

```powershell
Set-Location 'C:\Users\v-ningchen\Source\repos\PrivateStore\.worktrees\darkloot-vertical-slice\MyD2LikeGame'
```

不要进入原始master目录，不恢复先前装备掉落规则。实施前检查git状态，保留用户新改动。

| 路径 | 职责 |
|---|---|
| `src\DarkLoot.Data\Definitions\MaterialDefinition.cs` | 材料ID与名称 |
| `src\DarkLoot.Data\Definitions\RecipeDefinition.cs` | 配方模板、固定品质、费用；同文件MaterialCost |
| `src\DarkLoot.Data\Definitions\ResourceLootTableDefinition.cs` | 材料种类/数量区间、配方概率/候选列表 |
| `src\DarkLoot.Data\GameContent.cs`、`GameContentLoader.cs` | 新字典加载、字段及引用校验 |
| `content\definitions\materials\*.json`、`recipes\*.json`、`resource-loot-tables\*.json` | 3材料、4配方、3掉落表 |
| `src\DarkLoot.Gameplay\Items\EquipmentFactory.cs` | 固定模板/品质生成随机词缀 |
| `src\DarkLoot.Gameplay\Loot\LootGenerator.cs` | 旧生成器调用同一工厂，不复制算法 |
| `src\DarkLoot.Gameplay\Resources\ResourceStack.cs` | 资源类型与数量值对象 |
| `src\DarkLoot.Gameplay\Resources\ResourceBag.cs` | 数量、拾取收据、预计算快照和提交；同文件内部ResourceBagSnapshot |
| `src\DarkLoot.Gameplay\Resources\ResourcePickupService.cs` | 地面资源的校验、去重与入袋 |
| `src\DarkLoot.Gameplay\Crafting\BlacksmithState.cs` | 已学配方只读查询及内部提交 |
| `src\DarkLoot.Gameplay\Crafting\BlacksmithService.cs` | 存活/距离门禁、学习/打造；同文件操作结果 |
| `src\DarkLoot.Gameplay\Items\InventoryService.cs`、`Inventory.cs` | 窄内部装备入包校验/提交协作，复用PickUp逻辑 |
| `src\DarkLoot.Gameplay\Combat\PlayerState.cs` | 拥有ResourceBag，不承担打造算法 |
| `src\DarkLoot.Gameplay\Loot\ResourceLootGenerator.cs` | 单次资源生成 |
| `src\DarkLoot.Gameplay\Loot\KillRewardService.cs` | 当轮去重保留，返回资源 |
| `src\DarkLoot.Gameplay\Combat\GroundResource.cs` | 地面资源身份和位置，替代GroundItem |
| `src\DarkLoot.Gameplay\Combat\ArenaSimulation.cs` | 服务组合、资源地面/拾取/轮次生命周期 |
| `src\DarkLoot.Engine\Input\GameInput.cs` | F边沿、Esc边沿 |
| `src\DarkLoot.Game\PanelController.cs` | 可无窗口测试的面板互斥、优先级及世界推进决策 |
| `src\DarkLoot.Game\BlacksmithPanel.cs` | 配方行、学习/打造按钮及中文结果 |
| `src\DarkLoot.Game\InventoryPanel.cs` | 装备/资源页、现有比较穿戴 |
| `src\DarkLoot.Game\DarkLootGame.cs`、`ArenaRenderer.cs`、`GameText.cs`、`HudRenderer.cs` | 输入接线、铁匠/资源绘制、提示 |
| `src\DarkLoot.Game\RuntimeSmokeTest.cs` | 改造现有真实窗口fixture，保留动画/生命周期验收 |
| `src\DarkLoot.Game\Content\Fonts\UIFont.spritefont` | 新中文字符 |
| `tests\DarkLoot.Tests\Data\GameContentLoaderTests.cs`、`ContentFixture.cs` | 新内容有效性及迁移 |
| `tests\DarkLoot.Tests\Gameplay\EquipmentFactoryTests.cs`、`ResourceBagTests.cs`、`BlacksmithServiceTests.cs`、`ResourceLootGeneratorTests.cs` | 新行为测试 |
| `tests\DarkLoot.Tests\Gameplay\KillRewardServiceTests.cs`、`ArenaSimulationTests.cs`、`MixedEncounterTests.cs`、`PlayerLifecycleTests.cs`、`MixedEncounterFixture.cs`、`HitReactionTests.cs` | 旧装备奖励迁移为资源，保留战斗断言 |
| `tests\DarkLoot.Tests\Game\PanelControllerTests.cs`、`FinalAcceptanceTests.cs`、`ActorPresentationTests.cs` | 输入门禁与中文/展示回归 |
| `tests\DarkLoot.Tests\Engine\InputAndFrameRateTests.cs`、`tests\DarkLoot.Tests\DarkLoot.Tests.csproj` | 输入测试及按既有模式链接PanelController |
| `README.md` | 新玩法、按键、无存档限制 |

上表同目录内省略重复路径前缀的文件均在该行明确目录内。
Game项目ImplicitUsings关闭，新文件显式using；测试已有全局Xunit。
测试通过链接Game中的纯类测行为，不新建测试工具或引用整个WinExe。

## Task 1: 材料、配方与资源掉落数据契约

**Files:** 新建上述三个Data定义文件及10个JSON；修改GameContent、GameContentLoader、
`tests\DarkLoot.Tests\Data\GameContentLoaderTests.cs`和`ContentFixture.cs`。

**Interfaces — produces:**

```csharp
public sealed record MaterialDefinition(string Id, string Name);
public sealed record MaterialCost(string MaterialId, int Quantity);
public sealed record RecipeDefinition(
    string Id, string Name, string ItemId, ItemRarity Rarity,
    IReadOnlyList<MaterialCost> Costs);
public sealed record ResourceLootTableDefinition(
    string Id, string MaterialId, int MinQuantity, int MaxQuantity,
    int RecipeChancePercent, IReadOnlyList<string> RecipeIds);
```

GameContent在现有五个构造参数后追加可选字典参数
`MaterialDefinitions`、`RecipeDefinitions`、`ResourceLootTableDefinitions`，默认为null；
只读便利属性名`Materials`、`Recipes`、`ResourceLootTables`，空时返回共享空字典。
保留原Items/Affixes/Encounters/LootTables参数顺序，不破坏未迁移fixture编译。

- [ ] **Step 1: 写RED内容测试**

利用ContentFixture现有临时目录创建有效基础内容，添加以下文件内容；
继续用既有WriteDefinition helper和finally删除确切临时目录，不引入另一个fixture框架。
若helper当前private，只在ContentFixture增加面向新测试的CreateWithCraftingDefinitions，
不要为测试对生产代码开放文件写入口：

```json
// materials/slime.json
{"Id":"material.slime","Name":"黏液"}
// recipes/magic-blade.json
{"Id":"recipe.magic_blade","Name":"魔法武器配方","ItemId":"item.training_sword",
 "Rarity":"Magic","Costs":[{"MaterialId":"material.slime","Quantity":1}]}
// resource-loot-tables/slime.json
{"Id":"resource.slime","MaterialId":"material.slime","MinQuantity":1,
 "MaxQuantity":2,"RecipeChancePercent":20,"RecipeIds":["recipe.magic_blade"]}
```

JSON文件不包含上面的路径注释。测试核心：

```csharp
var content = GameContentLoader.LoadDirectory(root);
Assert.Equal("黏液", content.Materials["material.slime"].Name);
Assert.Equal(ItemRarity.Magic, content.Recipes["recipe.magic_blade"].Rarity);
Assert.Equal(20, content.ResourceLootTables["resource.slime"].RecipeChancePercent);
```

给fixture加入适用武器的词缀，保证Magic所需1条合法候选。
Theory逐一替换字段验证：重复材料ID、未知成本材料、成本0/-1、
重复MaterialId费用、未知ItemId、无效Rarity、词缀池少于品质要求、
MinQuantity0、MaxQuantity小于Min、概率-1/101、配方候选重复/缺失。
每例断言ContentLoadException同时含错误文件路径和引用ID/字段。

- [ ] **Step 2: 运行RED**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter FullyQualifiedName~GameContentLoaderTests --verbosity minimal
```

预期新字典/类型不存在，或新增无效内容被错误接受。记录实际RED。

- [ ] **Step 3: 加载与校验，并落地生产JSON**

复用LoadDefinitions、AsReadOnly、Invalid和RequireText。新目录缺失可得到空字典；
有定义则必须完整校验，不静默忽略。费用非空、每项正数且材料不重复；
配方品质只允许Common/Magic/Rare，所需词缀数0/1/2且模板部位可满足。
资源数量最少1、最大不小于最小，最大允许int.MaxValue，后续用long随机上界；
概率0..100；概率大于0必须有非空、无重复且存在的配方列表。
概率0允许空列表。保留现有词缀定义校验。

生产ID、文件名和数据固定如下，名称均为设计中的中文：

```text
materials\slime.json       material.slime   黏液
materials\hide.json        material.hide    兽皮
materials\bone.json        material.bone    骨片
recipes\magic-blade.json   recipe.magic_blade  weapon.training_blade Magic  bone2,slime1
recipes\magic-vest.json    recipe.magic_vest   armor.training_vest   Magic  hide2,slime1
recipes\rare-blade.json    recipe.rare_blade   weapon.training_blade Rare   bone4,hide2,slime2
recipes\rare-vest.json     recipe.rare_vest    armor.training_vest   Rare   hide4,bone2,slime2
resource-loot-tables\slime.json     resource.slime    material.slime
resource-loot-tables\wolf.json      resource.wolf     material.hide
resource-loot-tables\skeleton.json  resource.skeleton material.bone
```

三张表MinQuantity1/MaxQuantity2/RecipeChancePercent20，
RecipeIds顺序固定magic_blade、magic_vest、rare_blade、rare_vest。
Task 1只添加资源表及其引用校验，怪物仍引用旧表保持中间提交可运行；
Task 4整体迁移怪物引用和旧fixture，最终不能保留生产双掉落路径。
旧MonsterDefinition.LootTableId字段本轮不改名，Task 4后只查ResourceLootTables。

- [ ] **Step 4: GREEN、格式与提交**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter FullyQualifiedName~GameContentLoaderTests --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
git --no-pager diff --check
```

明确暂存本任务文件；标题`feat: define crafting materials recipes and resource loot`，
使用文末提交尾注。不提前切换生产怪物。

## Task 2: 提取固定品质装备工厂

**Files:** 新建`src\DarkLoot.Gameplay\Items\EquipmentFactory.cs`及
`tests\DarkLoot.Tests\Gameplay\EquipmentFactoryTests.cs`；
修改`src\DarkLoot.Gameplay\Loot\LootGenerator.cs`，保留LootGeneratorTests实质覆盖。

**Interfaces — produces:**

```csharp
public sealed class EquipmentFactory
{
    public ItemInstance Create(
        string itemId, ItemRarity rarity, GameContent content, Random random);
}
```

- [ ] **Step 1: 写固定品质、词缀范围和独立身份测试**

使用现有Gameplay命名空间的MixedEncounterFixture：

```csharp
[Theory]
[InlineData(ItemRarity.Magic, 1)]
[InlineData(ItemRarity.Rare, 2)]
public void Create_FixedQualityHasValidDistinctAffixes(ItemRarity rarity, int count)
{
    var content = MixedEncounterFixture.CreateContent();
    var factory = new EquipmentFactory();
    var first = factory.Create("weapon.training_blade", rarity, content, new Random(17));
    var second = factory.Create("weapon.training_blade", rarity, content, new Random(17));
    Assert.Equal(rarity, first.Rarity);
    Assert.Equal(count, first.Rolls.Count);
    Assert.Equal(count, first.Rolls.Select(roll => roll.AffixId).Distinct().Count());
    Assert.Equal(first.Rolls, second.Rolls);
    Assert.NotEqual(Guid.Empty, first.InstanceId);
    Assert.NotEqual(first.InstanceId, second.InstanceId);
    _ = new ItemStatCalculator().GetContribution(first, content);
}
```

加护甲Rare和Common无词缀用例；无效品质/未知模板/候选不足明确异常。
脚本Random覆盖NextDouble为抛异常，调用Create仍应成功，证明没有抽品质。

- [ ] **Step 2: RED**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter FullyQualifiedName~EquipmentFactoryTests --verbosity minimal
```

预期缺少EquipmentFactory。

- [ ] **Step 3: 移动算法而不是复制**

把LootGenerator的RollAffixes和AffixCount移到EquipmentFactory。
保持适用部位过滤、AffixId排序、无放回选择和原数值NextInt64调用不变。
Create先校验非空参数、模板存在、品质合法、适用池充足，调用原算法，
生成新Guid，最后通过ItemStatCalculator.GetContribution校验完整实例。
旧LootGenerator.GenerateItem仍先SelectRarity，再调用工厂：

```csharp
return new EquipmentFactory().Create(
    entry.ItemId, SelectRarity(table.RarityWeights, random), content, random);
```

在LootGenerator内用私有readonly工厂字段代替每次new；
其余内容表检查保留，现有静态ValidateRarityWeights中所需词缀数统一调用
`internal static int EquipmentFactory.AffixCount(ItemRarity rarity)`，
不要在两处复制RollAffixes或创建通用生成插件。

- [ ] **Step 4: GREEN和提交**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~EquipmentFactoryTests|FullyQualifiedName~LootGeneratorTests|FullyQualifiedName~ItemStatCalculatorTests' --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
git --no-pager diff --check
```

标题`refactor: share fixed quality equipment generation`。

## Task 3: 资源归属、学习和原子打造

**Files:** 新建Resources及Crafting目录中的上述文件；修改PlayerState、InventoryService、
Inventory；新建ResourceBagTests和BlacksmithServiceTests。
暂不改生产击杀奖励或Game输入。

**Interfaces — consumes:** Task 1定义/字典，Task 2工厂，现有ItemStatCalculator和装备操作。

**Interfaces — produces:**

```csharp
public enum ResourceKind { Material, Recipe }
public sealed record ResourceStack(ResourceKind Kind, string DefinitionId, int Quantity);
public sealed record GroundResource(Guid DropId, ResourceStack Stack, Vector2 Position);
public enum ResourcePickupResult { Applied, PlayerDead, AlreadyCollected }
public sealed class ResourcePickupService
{
    public ResourcePickupService(GameContent content);
    public ResourcePickupResult PickUp(PlayerState player, GroundResource drop);
}
public sealed class ResourceBag
{
    public IReadOnlyDictionary<string, int> Materials { get; }
    public IReadOnlyDictionary<string, int> Recipes { get; }
    public int GetCount(ResourceKind kind, string definitionId);
}
public sealed class BlacksmithState
{
    public IReadOnlyCollection<string> KnownRecipeIds { get; }
    public bool HasLearned(string recipeId);
}
public enum BlacksmithStatus
{
    Applied, PlayerDead, TooFar, RecipeNotOwned, AlreadyLearned,
    NotLearned, InsufficientMaterials
}
public sealed record BlacksmithResult(BlacksmithStatus Status, ItemInstance? Item = null);
public sealed class BlacksmithService
{
    public static Vector2 Position { get; } = new(120, 230);
    public BlacksmithService(
        GameContent content, Random random, InventoryService equipment, BlacksmithState state);
    public bool CanInteract(PlayerState player);
    public BlacksmithResult Learn(PlayerState player, string recipeId);
    public BlacksmithResult Craft(PlayerState player, string recipeId);
}
```

GroundResource路径为`src\DarkLoot.Gameplay\Combat\GroundResource.cs`，
便于下一任务接入ArenaSimulation；本任务已创建，不重复创建。
PlayerState增加只读`Resources = new ResourceBag()`。
服务的State由场景实例持有，不设static知识集合；Position是只读位置，不可变世界单例状态。

- [ ] **Step 1: 测试资源袋和学习/打造行为**

在新测试文件加`LoadProductionContent()` helper，沿用LootGeneratorTests向上定位
`content\definitions`的现有方式；构造场景使用生产内容和默认训练场。
用ResourcePickupService授予测试资源，不能直接改字典：

```csharp
private static void Give(ArenaSimulation arena, GameContent content,
    ResourceKind kind, string id, int quantity)
{
    var result = new ResourcePickupService(content).PickUp(arena.Player,
        new GroundResource(Guid.NewGuid(), new ResourceStack(kind, id, quantity),
            arena.Player.Position));
    Assert.Equal(ResourcePickupResult.Applied, result);
}

[Fact]
public void LearnThenCraft_ConsumesOnceAndProducesNewUnequippedInstance()
{
    var content = LoadProductionContent();
    var arena = new ArenaSimulation(content, new Random(17));
    var state = new BlacksmithState();
    var smith = new BlacksmithService(content, new Random(17), arena.EquipmentOperations, state);
    Give(arena, content, ResourceKind.Recipe, "recipe.magic_blade", 2);
    Give(arena, content, ResourceKind.Material, "material.bone", 4);
    Give(arena, content, ResourceKind.Material, "material.slime", 2);
    Assert.Equal(BlacksmithStatus.Applied, smith.Learn(arena.Player, "recipe.magic_blade").Status);
    Assert.Equal(BlacksmithStatus.AlreadyLearned, smith.Learn(arena.Player, "recipe.magic_blade").Status);
    Assert.Equal(1, arena.Player.Resources.GetCount(ResourceKind.Recipe, "recipe.magic_blade"));
    var first = smith.Craft(arena.Player, "recipe.magic_blade");
    var second = smith.Craft(arena.Player, "recipe.magic_blade");
    Assert.Equal(BlacksmithStatus.Applied, first.Status);
    Assert.Equal(BlacksmithStatus.Applied, second.Status);
    Assert.NotNull(first.Item);
    Assert.NotNull(second.Item);
    Assert.NotEqual(first.Item.InstanceId, second.Item.InstanceId);
    Assert.Equal(ItemRarity.Magic, first.Item.Rarity);
    Assert.Equal(2, arena.Player.Inventory.Items.Count);
    Assert.Null(arena.Player.Inventory.EquippedWeaponId);
    Assert.Equal(0, arena.Player.Resources.GetCount(ResourceKind.Material, "material.bone"));
    Assert.Equal(100, arena.Player.Health);
}
```

ResourceBagTests授予同ID两份drop，验证数量累计；重复DropId的另一对象返回AlreadyCollected。
先拾取int.MaxValue再加1，断言OverflowException且数量/收据不变；修正容量后可重试同DropId。
不能为了构造测试公开修改器，容量修正可通过Craft消耗后再试。
数量0、空DropId、非法ResourceKind、未知定义应明确异常且无写入。

BlacksmithServiceTests用Theory覆盖四张配方精确费用及产物。
增加未学/不足/未拥有/距离大于64/人物死亡正常拒绝；
ScriptedRandom记录调用数，所有拒绝前后调用数相同。
先学会后使用会抛异常的Random进行Craft，断言材料/已学/装备/生命全部不变。
死亡通过现有PlayerLifecycleTests的致死接触流程触发，远离通过PlayerIntent移动，
不公开PlayerState.Position/Health setter来便于测试。

- [ ] **Step 2: RED**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~ResourceBagTests|FullyQualifiedName~BlacksmithServiceTests' --verbosity minimal
```

预期新资源/打造类型不存在。

- [ ] **Step 3: 实现受限资源快照和装备提交协作**

ResourceBag内部用一个ResourceBagSnapshot持有只读材料/配方字典和拾取ID集合。
每次准备操作复制候选集合，所有checked加减、定义和去重检查在提交之前：

```csharp
// ResourceBag内部，不向UI暴露：
internal bool HasCollected(Guid dropId);
internal ResourceBagSnapshot PreparePickup(GroundResource drop);
internal ResourceBagSnapshot PrepareLearn(string recipeId);
internal ResourceBagSnapshot PrepareCraft(IReadOnlyList<MaterialCost> costs);
internal void Commit(ResourceBagSnapshot snapshot);
```

ResourcePickupService先校验非空参数、DropId、kind/定义、正数量和有限位置；
死亡返回PlayerDead，已收据返回AlreadyCollected，其余准备完整快照后Commit。
同DropId即使换对象也不重复加数量；PreparePickup失败不登记收据。
GetCount未知但非空ID返回0（合法的“未拥有”查询）；空ID/非法kind抛参数异常。
公开字典为只读包装，不能通过强转修改；数量为0时删除键。
Commit仅替换已准备好的状态引用，不再做可能失败的运算。
拾取去重收据属于资源袋、当前运行内保留；不是轮次奖励去重集合，不在R时清掉。

InventoryService把现有PickUp校验拆为窄内部协作：

```csharp
internal EquipmentChangeResult ValidateAddition(PlayerState player, ItemInstance item);
internal void CommitAddition(PlayerState player, ItemInstance item);
```

ValidateAddition共享当前null/ID、死亡、重复身份和GetContribution校验；
PickUp只在Applied时调用CommitAddition。CommitAddition只追加已校验物品，
Inventory增加内部AppendValidated，不能再次随机、计算或做可失败的业务校验。
原有TryAdd若无其他用途移除，保留唯一校验实现。
Crafting调用相同内部入口，不复制校验、不调用公共PickUp后忽略失败再扣费。

BlacksmithState对学习预先构建含新ID的只读集合，再内部替换引用。
Learn按定义合法→存活/距离→已学→配方数量顺序判断，准备资源和知识候选后提交。
Craft按定义合法→存活/距离→已学→全部费用顺序判断，再准备扣费快照、生成装备、
ValidateAddition确认Applied。成功的提交阶段只做无业务异常的状态替换及装备追加。
创建黑smith操作结果时Success的Item只在Craft成功时非null；Learn成功Item为null。
重复身份等不应发生的生成错误明确异常，不返回Applied空装备。

门禁使用距离平方≤64²且player存活；非有限位置作为非法参数明确异常。
所有正常拒绝无资源写入、无工厂调用/随机消费。
提交原子性限于单线程正常业务与校验异常，不尝试捕获OutOfMemory等进程级故障。

- [ ] **Step 4: GREEN与提交**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~ResourceBagTests|FullyQualifiedName~BlacksmithServiceTests|FullyQualifiedName~InventoryServiceTests|FullyQualifiedName~PlayerLifecycleTests' --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
git --no-pager diff --check
```

标题`feat: add recipe learning and atomic blacksmith crafting`。

## Task 4: 资源掉落接入战斗、拾取与生命周期

**Files:** 新建ResourceLootGenerator及其测试；修改KillRewardService、ArenaSimulation、
GameContentLoader、生产怪物JSON、ArenaRenderer资源绘制；
删除旧GroundItem与`content\definitions\loot-tables\training-slime.json`；
迁移GameContentLoaderTests/ContentFixture、KillRewardServiceTests、
ArenaSimulationTests、MixedEncounterTests/MixedEncounterFixture、PlayerLifecycleTests、
HitReactionTests、ActorPresentationTests、RuntimeSmokeTest中受类型影响的调用点。

**Interfaces — produces:**

```csharp
public sealed class ResourceLootGenerator
{
    public IReadOnlyList<ResourceStack> Generate(
        ResourceLootTableDefinition table, GameContent content, Random random);
}
// 替换KillRewardResult的Items，不保留双结果：
public sealed record KillRewardResult(
    KillRewardStatus Status, IReadOnlyList<ResourceStack> Resources);
// 场景新增/替换只读组合：
public IReadOnlyList<GroundResource> GroundResources { get; }
public ResourcePickupService ResourceOperations { get; }
public BlacksmithState Blacksmith { get; }
public BlacksmithService BlacksmithOperations { get; }
```

KillRecord.LootTableId保持字段名；查找改为content.ResourceLootTables。
GroundItems删除，不保留兼容可写/双地面列表。

- [ ] **Step 1: 锁定生产掉落边界与失败条件**

新ResourceLootGeneratorTests从生产内容取表；自定义Random覆盖NextInt64(min,max)
返回指定1/2，Next(100)返回19/20，Next(4)返回0..3并记录调用。

```csharp
var result = new ResourceLootGenerator().Generate(
    content.ResourceLootTables["resource.slime"], content, random);
Assert.Equal(new ResourceStack(ResourceKind.Material, "material.slime", 2), result[0]);
Assert.Equal(new ResourceStack(ResourceKind.Recipe, "recipe.magic_blade", 1), result[1]);
Assert.Equal(2, result.Count);
```

Theory覆盖19掉/20不掉、0与99、四个配方票据、三种怪物表和数量两端，
以及概率0/100、最大数量int.MaxValue不溢出随机上界。
生成前校验表/引用，非法内容在任何随机消费前报错。
配方概率0或100不需要概率抽样；生产20必须按规定抽样。

KillRewardServiceTests迁移Items→Resources，保留重复/旧轮无随机消费、首次必须0、
100轮重开清理、生成异常不标成功、不同实例分别奖励等断言。
旧零掉落表成功语义现在由旧LootGeneratorTests保留，资源表固定至少一堆材料，
不要为保留旧奖励零掉落测试而放宽资源MinQuantity规则。

- [ ] **Step 2: RED**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~ResourceLootGeneratorTests|FullyQualifiedName~KillRewardServiceTests' --verbosity minimal
```

预期新类型/结果字段缺失。

- [ ] **Step 3: 生成、奖励、地面和生产引用整体迁移**

ResourceLootGenerator校验参数/内容后执行：

```csharp
var quantity = (int)random.NextInt64(table.MinQuantity, (long)table.MaxQuantity + 1);
var result = new List<ResourceStack>
{
    new(ResourceKind.Material, table.MaterialId, quantity)
};
var recipeDrops = table.RecipeChancePercent == 100 ||
    (table.RecipeChancePercent > 0 && random.Next(100) < table.RecipeChancePercent);
if (recipeDrops)
    result.Add(new ResourceStack(ResourceKind.Recipe,
        table.RecipeIds[random.Next(table.RecipeIds.Count)], 1));
return result.AsReadOnly();
```

KillRewardService沿用当前轮次去重算法，完整生成后标记，重复返回空Resources。
ArenaSimulation用同一个random组合所有服务；每份ResourceStack生成非空Guid的
GroundResource放在kill.DeathPosition；死亡动画仍立刻开始而非等待拾取。
拾取遍历GroundResources沿用原36范围/最近和等距选择，ResourceOperations返回Applied
才删地面；AlreadyCollected/PlayerDead明确分支保留，不把失败当成功。
ResetEncounter仅清地面/轮次，不重建Player.Resources/Blacksmith或操作服务。

生产怪物JSON的LootTableId分别指resource.slime/resource.wolf/resource.skeleton，
删除旧production装备掉落JSON。GameContentLoader最终只校验怪物引用资源表；
旧LootTables仍用于独立生成器测试，不能给Monster回退查旧装备表。
内容fixture和combat fixture全部补上合法材料/配方/资源表。
不要仅复制GameContent前5个字段丢掉新字典：所有with/构造拷贝点一起迁移。

ArenaRenderer按资源Kind解析Materials/Recipes名称，显示`E 拾取：名称 ×数量`；
材料用统一中性色，配方用明确的另一颜色，复用LootLabelLayout及连线。
本任务同步迁移RuntimeSmokeTest的地面类型、签名/数量采集，
旧装备来源阶段可暂无法走完，但必须保持编译；Task 6负责完整流程，不削掉后续断言。

- [ ] **Step 4: 资源来源替换，保留战斗特征测试**

MixedEncounterTests把原crit→count→entry断言换为当前资源序列：

```text
每个目标：Next(100)暴击 → NextInt64(1,3)数量 →
Next(100)配方概率 → 成功时Next(4)配方选择
```

仍逐目标交错，不能先滚全部暴击再全部奖励。断言物品位置、数量和怪物命中顺序。
原“地面装备拾取后穿戴”测试替换为“资源拾取→学习→打造→穿戴”；
纯战斗数值测试可用EquipmentOperations.PickUp授予装备，不能依赖怪物直接掉装备。
新增死亡/R保留材料、配方、知识与已穿装备；新轮次仍掉新DropId；
重复地面拾取、数量溢出失败保留地面并无部分入袋。
ActorPresentationTests只改奖励断言，保留死亡/反应时间断言。
LootGeneratorTests将旧ProductionTable装备概率测试改为资源生产规则测试，
通用装备随机词缀测试保留在工厂/旧生成器，不再断言生产怪物出装备。

- [ ] **Step 5: GREEN、构建与提交**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --verbosity minimal
dotnet build src\DarkLoot.Game -c Release --no-restore --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
git --no-pager diff --check
rg -n 'GroundItems|GroundItem\b|loot\.training_slime' src content tests -g '*.cs' -g '*.json'
```

预期测试全部通过、构建零警告。rg中的旧production ID仅能存在于明确的独立测试表，
不能存在生产怪物/加载引用或旧可写入口；若fixture使用同名历史ID，改成loot.test_equipment。
标题`feat: replace monster equipment drops with crafting resources`。

## Task 5: 铁匠面板、资源背包和输入路由

**Files:** 新建PanelController、BlacksmithPanel及PanelControllerTests；
修改GameInput、InputAndFrameRateTests、InventoryPanel、DarkLootGame、ArenaRenderer、
GameText、HudRenderer、UIFont.spritefont、DarkLoot.Tests.csproj、FinalAcceptanceTests。

**Interfaces — consumes:** ArenaSimulation.Player.Resources、Blacksmith/BlacksmithOperations服务；输入快照。
**Interfaces — produces:**

```csharp
// GameInputSnapshot末尾追加，不改变既有参数位置：
bool InteractPressed = false
public enum PanelKind { None, Inventory, Blacksmith }
public readonly record struct PanelFrame(
    PanelKind ActivePanel, bool AdvanceWorld, bool HandlePanelActions,
    bool ExitRequested, bool RestartRequested, BlacksmithStatus? Feedback);
public sealed class PanelController
{
    public PanelKind ActivePanel { get; }
    public PanelFrame Route(
        GameInputSnapshot input, PlayerState player, BlacksmithService smith);
}
```

InventoryPanel.Update不再自己切换IsOpen；新增`SetOpen(bool)`供宿主同步状态，
原滚轮/装备行点击保留，增加资源页按钮及资源行滚动。
BlacksmithPanel采用`SetOpen(bool)`、`Update(input, arena, content, width)`、
`Draw(batch, shapes, font, arena, content, width)`，按钮命中矩形供真实smoke使用，
但不能在面板里写资源或知识集合。

- [ ] **Step 1: 路由与按键RED测试**

在Tests.csproj沿用Compile Link方式链接PanelController；输入捕获测试加F边沿，
Esc由持续按住改为边沿。PanelControllerTests创建真实scene/service和以下输入：

```csharp
var idle = new GameInputSnapshot(Vector2.Zero, Vector2.UnitX, Vector2.Zero,
    false, false, false, false, false);
var controller = new PanelController();
var opened = controller.Route(idle with { InteractPressed = true },
    arena.Player, arena.BlacksmithOperations);
Assert.Equal(PanelKind.Blacksmith, opened.ActivePanel);
Assert.False(opened.AdvanceWorld);
Assert.False(opened.HandlePanelActions);
var closing = controller.Route(
    idle with { ExitPressed = true, AttackHeld = true, RestartPressed = true },
    arena.Player, arena.BlacksmithOperations);
Assert.Equal(PanelKind.None, closing.ActivePanel);
Assert.False(closing.AdvanceWorld);
Assert.False(closing.RestartRequested);
Assert.False(closing.ExitRequested);
Assert.True(controller.Route(idle with { ExitPressed = true },
    arena.Player, arena.BlacksmithOperations).ExitRequested);
```

加I/F互斥、死亡禁止面板、远离F返回TooFar、面板内R无效、
点击关闭帧不推进、无面板持有攻击可正常推进测试。
同帧优先Esc，再I，再F；转换面板时HandlePanelActions=false。

- [ ] **Step 2: RED**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~PanelControllerTests|FullyQualifiedName~InputAndFrameRateTests' --verbosity minimal
```

预期缺少新路由/F字段。

- [ ] **Step 3: 接入宿主，不让输入穿透**

GameInput.Capture将最后新增值设为Pressed(Keys.F)，Esc用Pressed(Keys.Escape)。
DarkLootGame删除当前Update里提前检查ExitPressed直接Exit的逻辑，
统一由PanelController.Route处理退出/面板/世界推进，避免Esc关闭后仍退出。
ApplyInput执行顺序：

```csharp
var frame = _panels.Route(input, _arena.Player, _arena.BlacksmithOperations);
_inventory.SetOpen(frame.ActivePanel == PanelKind.Inventory);
_blacksmith.SetOpen(frame.ActivePanel == PanelKind.Blacksmith);
if (frame.ExitRequested) { Exit(); return; }
if (frame.HandlePanelActions)
{
    // 仅当前打开面板Update；I背包传EquipmentOperations，铁匠传场景服务。
}
if (frame.AdvanceWorld)
{
    if (frame.RestartRequested) { _arena.ResetEncounter(); _presentation.Reset(_arena); }
    _arena.Update(new PlayerIntent(input.MoveDirection, input.AimWorldPosition,
        input.AttackHeld || input.AttackPressed, input.PickupPressed), elapsed);
    _presentation.Update(_arena, elapsed);
}
FollowPlayer();
```

实现实际面板调用，不保留上述注释作为空分支。
PanelController里死亡时只允许无面板世界推进/无面板退出，忽略I/F/R；
活着时Esc有面板只关闭，无面板退出；I切换背包、F已开则关或CanInteract时开。
任一面板转换帧AdvanceWorld=false；面板稳定打开时AdvanceWorld=false、
HandlePanelActions=true；普通无面板帧AdvanceWorld=true。
暂停时仍Draw/FPS，失焦仍先Capture更新边沿、ActivityGate决定不推进。

- [ ] **Step 4: 实现面板与中文呈现**

铁匠面板使用现有宽度布局风格，4行固定展示，行内显示状态/配方数量/材料持有与费用。
以BlacksmithState、ResourceBag和RecipeDefinition实时算按钮可用条件；
点击再调用服务，不能以按钮判断替代服务门禁。
状态文本按BlacksmithStatus完整switch，未知枚举抛异常，不用空字符串成功兜底。
成功打造展示Item实际Rolls，用现有ItemNameFormatter和InventoryPanel.FormatRolls。
缺少材料用每项需求与GetCount比较生成中文明细；不能只显示笼统的“失败”。
面板暂停提示、关闭键说明及远离F反馈均有对应GameText文本。

资源袋页只显示当前拥有的材料/配方，不显示0键；
切换页重置/夹紧滚动位置，资源点击不触发装备操作。
场景铁匠用ShapeRenderer绘制简洁铁砧/人形，并在CanInteract时显示F提示。
HUD改为资源/配方打造目标，移除“击败敌人可获得武器和护甲”及旧Esc/R说明。
新增所有用户文案集中GameText；UIFont加入实际新增中文字符，
扩展现有字体字符及Measured layout验收数据，不改为缩小所有字掩盖溢出。

- [ ] **Step 5: GREEN和提交**

```powershell
dotnet test tests\DarkLoot.Tests -c Release --filter 'FullyQualifiedName~PanelControllerTests|FullyQualifiedName~InputAndFrameRateTests|FullyQualifiedName~FinalAcceptanceTests' --verbosity minimal
dotnet build src\DarkLoot.Game -c Release --no-restore --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
git --no-pager diff --check
```

标题`feat: add blacksmith interaction and resource inventory panels`。

## Task 6: 真实闭环smoke、文档和完整交付

**Files:** 修改RuntimeSmokeTest、README、FinalAcceptanceTests；
必要的行为缺口仅修本任务验收直接依赖的文件，不削减既有战斗/动画断言。

**Interfaces — consumes:** 完成的生产Data/Gameplay/面板API。
**Produces:** 正常和发布版同一套真实输入/渲染闭环验证及清晰的无存档说明。

- [ ] **Step 1: 改造实际窗口fixture**

RuntimeSmokeTest.CreateFixture拷贝生产内容时保留新字典。
测试表可以把数量固定到足够打造、配方概率100、候选为指定配方来保证确定性，
但只能从击杀→真实E拾取得到资源，不能直接给人物加装备或已学知识代替闭环。
需要两槽时安排不同怪物掉magic_blade和magic_vest配方；
费用仍引用生产配方，不能降费用迁就smoke。
测试内容可按既有需求约束词缀池，让打造仍得到原50%暴击和+20生命fixture，
但生成必须走EquipmentFactory和正式BlacksmithService。

流程顺序：近战击杀多目标 → 收集全部地面堆 → 走到铁匠64以内 →
F打开 → 点击学习武器/护甲 → 点击各打造一次 → 检查真实扣费/新实例 →
I切到装备页 → 点击装备行 → 检查比较与实际属性一致。
之后继续原箭矢、受击、死亡、自动复活、R、失焦门禁和动画断言。
原_pickupItems改为资源数量/收据核对，打造后单独记住装备ID和Rolls用于后续保留断言。
Smoke的Esc关闭/退出逻辑与新输入路由一致，不能直接调用服务代替UI点击。

- [ ] **Step 2: 增加无窗口集成断言**

在现有PlayerLifecycleTests扩展“资源+知识+穿戴装备”跨死亡/R保留快照：

```csharp
var beforeMaterials = arena.Player.Resources.Materials.ToArray();
var beforeRecipes = arena.Player.Resources.Recipes.ToArray();
var beforeKnown = arena.Blacksmith.KnownRecipeIds.Order(StringComparer.Ordinal).ToArray();
var beforeEquipment = arena.Player.Inventory.Items.ToArray();
arena.ResetEncounter();
Assert.Equal(beforeMaterials, arena.Player.Resources.Materials.ToArray());
Assert.Equal(beforeRecipes, arena.Player.Resources.Recipes.ToArray());
Assert.Equal(beforeKnown, arena.Blacksmith.KnownRecipeIds.Order(StringComparer.Ordinal));
Assert.Equal(beforeEquipment, arena.Player.Inventory.Items);
Assert.Empty(arena.GroundResources);
```

真实死亡流程复用同一组断言；新建ArenaSimulation断言资源/知识/装备都为空，
验证无存档含义，不靠文件不存在这种代理断言。
FinalAcceptanceTests更新所有玩家文本和字体字符收集；
RuntimeSmokeTest.ValidatePresentation纳入材料/配方名称、费用行、按钮、成功与拒绝文本。

- [ ] **Step 3: 更新README与清理旧语义**

说明实际按键E/F/I/Esc、背包资源页、学习消耗1张、重复配方保留、
四配方费用、每怪材料和额外20%配方、无装备直掉、
死亡/R保留但退出清空，以及不自动穿戴。
保留原战斗/动画操作说明，修正受本轮直接影响的smoke步骤。
搜索生产源码/内容和README，不再声称生产每怪必掉装备：

```powershell
rg -n '必掉.*装备|直接.*掉.*装备|GroundItem\b|GroundItems' README.md src content -g '*.md' -g '*.cs' -g '*.json'
```

允许README明确说“不直接掉装备”，不允许旧行为文案误导。
历史spec/plan不改写。

- [ ] **Step 4: 完整验证**

```powershell
dotnet build DarkLoot.sln -c Release --no-restore --verbosity minimal
dotnet test tests\DarkLoot.Tests -c Release --no-build --verbosity minimal
dotnet format DarkLoot.sln --verify-no-changes --no-restore --verbosity minimal
git --no-pager diff --check
dotnet run --project src\DarkLoot.Game -c Release --no-build -- --smoke-test
dotnet publish src\DarkLoot.Game -c Release --no-restore -o artifacts\publish
```

预期0警告0错误、完整测试通过、格式通过，记录实际测试数。
不要求固定220，因为新增测试及旧行为替换会改变数量。
普通smoke失败后仍执行publish和发布版验证，分别记录结果。

发布版必须等待真实进程并捕获stdout/stderr，不能把PowerShell直接启动WinExe返回0
当作窗口通过证据。日志放本会话/任务报告目录，使用已解析的确切路径：

```powershell
$logs = 'C:\Users\v-ningchen\.copilot\session-state\62788275-ec02-4460-a11a-bd9349f184ea\files'
$p = Start-Process -FilePath '.\artifacts\publish\DarkLoot.Game.exe' `
    -ArgumentList '--smoke-test' -Wait -PassThru `
    -RedirectStandardOutput (Join-Path $logs 'crafting-published.stdout.log') `
    -RedirectStandardError (Join-Path $logs 'crafting-published.stderr.log')
Get-Content (Join-Path $logs 'crafting-published.stdout.log')
Get-Content (Join-Path $logs 'crafting-published.stderr.log')
if ($p.ExitCode -ne 0) { throw "Published smoke exited $($p.ExitCode)." }
```

NoSuitableGraphicsDeviceException发生在初始化时，记录为窗口验收受阻，
不伪造/跳过断言、不为环境问题改游戏，不反复无意义重试。
即使图形受阻，构建/所有逻辑测试/格式仍必须通过，不能省略新UI路由测试。
依赖仅在缺少工具/包的真实失败后restore，不预先安装。

- [ ] **Step 5: 自检、提交及最终审查**

核对设计所有验收项，检查git差异只包含本轮内容。提交标题
`test: verify blacksmith crafting gameplay loop`，带统一尾注。
逐任务审查后再做一次完整功能/分支集成审查；若有重要缺陷修复后运行覆盖测试并复审。
最终报告提交、测试数、构建结果及窗口是否真正通过。
保留分支/worktree，不自动push；不声称有存档或装备成长。

## 每次提交与任务审查约定

每任务按RED→最小实现→GREEN→格式/diff检查→明确暂存→提交→审查执行。
大迁移的特征测试可重构前先通过，不伪称已有行为的测试RED。
提交命令示例（标题按各任务使用）：

```powershell
git commit -m 'feat: add recipe learning and atomic blacksmith crafting' -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`nCopilot-Session: 62788275-ec02-4460-a11a-bd9349f184ea"
```

不`git add .`，不提交日志/进度笔记，不覆盖用户更改，不修改历史提交。
任务顺序1→2→3→4→5→6，单共享worktree不并行实施。
每任务是可编译、可测试提交；Task 4到Task 6之间旧窗口脚本可暂未完成新的流程，
此限制写报告并由Task 6关闭，不把未运行/未通过的窗口标为绿。

## 设计覆盖自检

| 设计约束 | 任务 |
|---|---|
| 3材料、4配方、精确费用、数据引用与词缀池合法 | 1 |
| 固定品质与随机适用词缀、不抽品质、不重复稀有词缀 | 2、3 |
| 堆叠、稳定地面身份、重复拾取与溢出无部分写入 | 3、4 |
| 学习扣1、已学不再消耗、重复配方保留 | 3、5、6 |
| 原子扣费入包、拒绝无随机、失败无半成品/生命变化 | 3 |
| 每怪材料＋独立20%配方、逐怪即时奖励、轮次去重 | 4 |
| 地面分开显示、E最近一堆、生产不掉装备 | 4 |
| F距离门禁、I互斥、Esc关闭优先、R/关闭帧无穿透 | 5 |
| 死亡/R保留资源/知识/装备，退出清空 | 3、4、6 |
| 中文字符/布局、FPS、暂停/失焦、真实学习打造穿戴 | 5、6 |
| 无存档/成长/打造失败/额外框架、图形受阻诚实报告 | 全部、6 |
