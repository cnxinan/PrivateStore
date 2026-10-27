using DarkLoot.Data;
using DarkLoot.Data.Definitions;

namespace DarkLoot.Gameplay.Items;

public static class ItemNameFormatter
{
    public static string GetDisplayName(
        ItemInstance item,
        ItemDefinition definition,
        GameContent content)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(content);

        var prefixes = new List<string>();
        var suffixes = new List<string>();
        foreach (var roll in item.Rolls)
        {
            if (roll is null ||
                !content.Affixes.TryGetValue(roll.AffixId, out var affix) ||
                affix is null ||
                string.IsNullOrWhiteSpace(affix.Name) ||
                affix.NameKind is not (AffixNameKind.Prefix or AffixNameKind.Suffix))
            {
                throw new ArgumentException(
                    $"Item instance '{item.InstanceId}' has an invalid naming affix '{roll?.AffixId}'.",
                    nameof(item));
            }

            if (affix.NameKind == AffixNameKind.Prefix)
            {
                prefixes.Add(affix.Name);
            }
            else
            {
                suffixes.Add(affix.Name);
            }
        }

        var prefix = prefixes.Count == 0 ? "" : string.Join(" ", prefixes) + " ";
        var suffix = suffixes.Count == 0 ? "" : " " + string.Join(" ", suffixes);
        return $"{prefix}{definition.Name}{suffix}";
    }
}
