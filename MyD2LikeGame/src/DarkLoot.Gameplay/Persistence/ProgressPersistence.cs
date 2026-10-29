using System.Collections.ObjectModel;
using DarkLoot.Data;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Items;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Gameplay.Persistence;

public sealed class ProgressPersistence
{
    private const int CurrentVersion = 1;
    private readonly GameContent _content;
    private readonly ItemStatCalculator _calculator = new();

    public ProgressPersistence(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _content = content;
    }

    public ProgressSnapshot Capture(ArenaSimulation arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        var inventory = arena.Player.Inventory;
        return new ProgressSnapshot(
            CurrentVersion,
            Array.AsReadOnly(inventory.Items.Select(item => new SavedItem(
                item.InstanceId, item.DefinitionId, item.Rarity,
                Array.AsReadOnly(item.Rolls.ToArray()))).ToArray()),
            inventory.EquippedWeaponId,
            inventory.EquippedArmorId,
            CopyCounts(arena.Player.Resources.Materials),
            CopyCounts(arena.Player.Resources.Recipes),
            Array.AsReadOnly(arena.Blacksmith.KnownRecipeIds.Order(StringComparer.Ordinal).ToArray()));
    }

    public void Restore(ArenaSimulation arena, ProgressSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (arena.HasStarted ||
            arena.Player.Inventory.Items.Count != 0 ||
            arena.Player.Resources.Materials.Count != 0 ||
            arena.Player.Resources.Recipes.Count != 0 ||
            arena.Blacksmith.KnownRecipeIds.Count != 0)
        {
            throw new InvalidOperationException("Progress can only be restored into a fresh arena.");
        }

        if (snapshot.Version != CurrentVersion)
            throw Invalid("version", $"unsupported save version {snapshot.Version}; expected {CurrentVersion}");
        if (snapshot.Items is null)
            throw Invalid("items", "collection is required");

        var items = new List<ItemInstance>();
        var byId = new Dictionary<Guid, ItemInstance>();
        for (var index = 0; index < snapshot.Items.Count; index++)
        {
            var saved = snapshot.Items[index];
            if (saved is null || saved.Rolls is null)
                throw Invalid($"items[{index}]", "item and rolls are required");
            ItemInstance item;
            try
            {
                item = new ItemInstance(saved.InstanceId, saved.DefinitionId, saved.Rarity, saved.Rolls);
                _ = _calculator.GetContribution(item, _content);
            }
            catch (ArgumentException exception)
            {
                throw Invalid($"items[{index}]", exception.Message, exception);
            }
            if (!byId.TryAdd(item.InstanceId, item))
                throw Invalid($"items[{index}].instanceId", $"duplicate ID '{item.InstanceId}'");
            items.Add(item);
        }

        ItemInstance? ResolveEquipped(Guid? id, string field)
        {
            if (id is null)
                return null;
            if (!byId.TryGetValue(id.Value, out var item))
                throw Invalid(field, $"item '{id}' is not owned");
            return item;
        }

        PlayerStatTotals totals;
        try
        {
            totals = _calculator.Calculate(arena.Player.BaseAttackDamage, arena.Player.BaseMaximumHealth,
                ResolveEquipped(snapshot.EquippedWeaponId, "equippedWeaponId"),
                ResolveEquipped(snapshot.EquippedArmorId, "equippedArmorId"), _content);
        }
        catch (ArgumentException exception)
        {
            throw Invalid("equipment", exception.Message, exception);
        }

        var materials = ValidateCounts(snapshot.Materials, _content.Materials, "materials");
        var recipes = ValidateCounts(snapshot.Recipes, _content.Recipes, "recipes");
        if (snapshot.KnownRecipeIds is null)
            throw Invalid("knownRecipeIds", "collection is required");
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in snapshot.KnownRecipeIds)
        {
            if (string.IsNullOrWhiteSpace(id) || !_content.Recipes.ContainsKey(id) || !known.Add(id))
                throw Invalid("knownRecipeIds", $"unknown or duplicate recipe '{id}'");
        }

        var resources = new ResourceBagSnapshot(materials, recipes, []);
        var knowledge = new BlacksmithStateSnapshot(known,
            new ReadOnlyCollection<string>(known.Order(StringComparer.Ordinal).ToList()));
        arena.Player.Inventory.RestoreValidated(items, snapshot.EquippedWeaponId, snapshot.EquippedArmorId);
        arena.Player.Resources.Commit(resources);
        arena.Blacksmith.Commit(knowledge);
        arena.Player.Health = totals.MaximumHealth;
    }

    private static IReadOnlyDictionary<string, int> CopyCounts(IReadOnlyDictionary<string, int> counts) =>
        new ReadOnlyDictionary<string, int>(
            counts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

    private static Dictionary<string, int> ValidateCounts<T>(
        IReadOnlyDictionary<string, int>? counts, IReadOnlyDictionary<string, T> definitions, string field)
    {
        if (counts is null)
            throw Invalid(field, "collection is required");
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, count) in counts)
        {
            if (string.IsNullOrWhiteSpace(id) || !definitions.ContainsKey(id) || count <= 0)
                throw Invalid($"{field}.{id}", "definition must exist and count must be positive");
            result.Add(id, count);
        }
        return result;
    }

    private static SaveGameException Invalid(string field, string reason, Exception? inner = null) =>
        new($"Invalid save field '{field}': {reason}.", inner);
}
