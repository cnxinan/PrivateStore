using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Gameplay.Quests;

public enum QuestObjective { Kills, Materials, Guardian }

public sealed record QuestDefinition(
    string Id, QuestObjective Objective, int Required,
    IReadOnlyList<MaterialCost> Costs, IReadOnlyList<ResourceStack> Rewards);

public static class SmithQuests
{
    public static IReadOnlyList<QuestDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new QuestDefinition("quest.exploration", QuestObjective.Kills, 3,
            Array.Empty<MaterialCost>(), Array.AsReadOnly(new[]
            {
                new ResourceStack(ResourceKind.Material, "material.bone", 2),
                new ResourceStack(ResourceKind.Material, "material.slime", 2)
            })),
        new QuestDefinition("quest.supplies", QuestObjective.Materials, 4,
            Array.AsReadOnly(new[] { new MaterialCost("material.bone", 4) }),
            Array.AsReadOnly(new[] { new ResourceStack(ResourceKind.Recipe, "recipe.magic_blade", 1) })),
        new QuestDefinition("quest.guardian", QuestObjective.Guardian, 1,
            Array.Empty<MaterialCost>(), Array.AsReadOnly(new[]
            {
                new ResourceStack(ResourceKind.Recipe, "recipe.rare_vest", 1),
                new ResourceStack(ResourceKind.Material, "material.hide", 2)
            }))
    });
}
