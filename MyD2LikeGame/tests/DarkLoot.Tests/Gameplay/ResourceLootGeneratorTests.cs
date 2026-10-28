using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Loot;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Tests.Gameplay;

public sealed class ResourceLootGeneratorTests
{
    [Fact]
    public void Generate_UsesInclusiveQuantityAndConfiguredRecipeTicket()
    {
        var content = LoadProductionContent();
        var random = new ScriptedRandom([2, 19, 0]);

        var result = new ResourceLootGenerator().Generate(
            content.ResourceLootTables["resource.slime"], content, random);

        Assert.Equal(
            new[]
            {
                new ResourceStack(ResourceKind.Material, "material.slime", 2),
                new ResourceStack(ResourceKind.Recipe, "recipe.magic_blade", 1)
            },
            result);
        Assert.Equal(["quantity:1:3", "next:100", "next:4"], random.Calls);
    }

    [Theory]
    [InlineData(19, true)]
    [InlineData(20, false)]
    [InlineData(0, true)]
    [InlineData(99, false)]
    public void Generate_UsesTwentyPercentRecipeBoundary(int ticket, bool recipeExpected)
    {
        var content = LoadProductionContent();
        var random = new ScriptedRandom([1, ticket, 3]);

        var result = new ResourceLootGenerator().Generate(
            content.ResourceLootTables["resource.slime"], content, random);

        Assert.Equal(recipeExpected ? 2 : 1, result.Count);
        if (recipeExpected)
        {
            Assert.Equal("recipe.rare_vest", result[1].DefinitionId);
        }
        Assert.Equal(recipeExpected ? ["quantity:1:3", "next:100", "next:4"] :
            ["quantity:1:3", "next:100"], random.Calls);
    }

    [Theory]
    [InlineData("resource.slime", "material.slime")]
    [InlineData("resource.wolf", "material.hide")]
    [InlineData("resource.skeleton", "material.bone")]
    public void Generate_UsesMaterialConfiguredForEachProductionTable(
        string tableId,
        string materialId)
    {
        var content = LoadProductionContent();

        var result = new ResourceLootGenerator().Generate(
            content.ResourceLootTables[tableId], content, new ScriptedRandom([1, 20]));

        Assert.Equal(new ResourceStack(ResourceKind.Material, materialId, 1), Assert.Single(result));
    }

    [Theory]
    [InlineData(0, "recipe.magic_blade")]
    [InlineData(1, "recipe.magic_vest")]
    [InlineData(2, "recipe.rare_blade")]
    [InlineData(3, "recipe.rare_vest")]
    public void Generate_SelectsEveryConfiguredRecipeTicket(int ticket, string recipeId)
    {
        var content = LoadProductionContent();

        var result = new ResourceLootGenerator().Generate(
            content.ResourceLootTables["resource.slime"], content,
            new ScriptedRandom([1, 0, ticket]));

        Assert.Equal(recipeId, result[1].DefinitionId);
    }

    [Fact]
    public void Generate_ProbabilityZeroSkipsRecipeRandomDraw()
    {
        var content = LoadProductionContent();
        var table = content.ResourceLootTables["resource.slime"] with
        {
            RecipeChancePercent = 0,
            RecipeIds = []
        };
        var random = new ScriptedRandom([2]);

        var result = new ResourceLootGenerator().Generate(table, content, random);

        Assert.Single(result);
        Assert.Equal(["quantity:1:3"], random.Calls);
    }

    [Fact]
    public void Generate_ProbabilityOneHundredSkipsChanceDraw()
    {
        var content = LoadProductionContent();
        var table = content.ResourceLootTables["resource.slime"] with { RecipeChancePercent = 100 };
        var random = new ScriptedRandom([2, 1]);

        var result = new ResourceLootGenerator().Generate(table, content, random);

        Assert.Equal("recipe.magic_vest", result[1].DefinitionId);
        Assert.Equal(["quantity:1:3", "next:4"], random.Calls);
    }

    [Fact]
    public void Generate_AllowsIntMaxValueQuantityWithoutOverflowingRandomUpperBound()
    {
        var content = LoadProductionContent();
        var table = content.ResourceLootTables["resource.slime"] with
        {
            MinQuantity = int.MaxValue,
            MaxQuantity = int.MaxValue,
            RecipeChancePercent = 0,
            RecipeIds = []
        };
        var random = new ScriptedRandom([int.MaxValue]);

        var result = new ResourceLootGenerator().Generate(table, content, random);

        Assert.Equal(int.MaxValue, Assert.Single(result).Quantity);
        Assert.Equal($"quantity:{int.MaxValue}:{(long)int.MaxValue + 1}", Assert.Single(random.Calls));
    }

    [Fact]
    public void Generate_RejectsMissingMaterialBeforeConsumingRandomness()
    {
        var content = LoadProductionContent();
        var table = content.ResourceLootTables["resource.slime"] with { MaterialId = "material.missing" };
        var random = new ScriptedRandom([]);

        var error = Assert.Throws<ArgumentException>(() =>
            new ResourceLootGenerator().Generate(table, content, random));

        Assert.Contains("material.missing", error.Message);
        Assert.Empty(random.Calls);
    }

    private static GameContent LoadProductionContent()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "content",
                   "definitions",
                   "resource-loot-tables",
                   "slime.json")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return GameContentLoader.LoadDirectory(
            Path.Combine(directory!.FullName, "content", "definitions"));
    }

    private sealed class ScriptedRandom(params long[] values) : Random
    {
        private int _valueIndex;

        public List<string> Calls { get; } = [];

        public override long NextInt64(long minValue, long maxValue)
        {
            Calls.Add($"quantity:{minValue}:{maxValue}");
            return Math.Clamp(values[_valueIndex++], minValue, maxValue - 1);
        }

        public override int Next(int maxValue)
        {
            Calls.Add($"next:{maxValue}");
            return (int)Math.Clamp(values[_valueIndex++], 0, maxValue - 1);
        }
    }
}
