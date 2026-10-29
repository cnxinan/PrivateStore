using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Items;

namespace DarkLoot.Gameplay.Persistence;

public sealed record SavedItem(
    Guid InstanceId,
    string DefinitionId,
    ItemRarity Rarity,
    IReadOnlyList<AffixRoll> Rolls);

public sealed record ProgressSnapshot(
    int Version,
    IReadOnlyList<SavedItem> Items,
    Guid? EquippedWeaponId,
    Guid? EquippedArmorId,
    IReadOnlyDictionary<string, int> Materials,
    IReadOnlyDictionary<string, int> Recipes,
    IReadOnlyList<string> KnownRecipeIds);
