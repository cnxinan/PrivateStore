namespace DarkLoot.Data.Definitions;

public sealed record EncounterDefinition(
    string Id,
    IReadOnlyList<MonsterSpawnDefinition> Spawns);
