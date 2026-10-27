namespace DarkLoot.Data.Definitions;

public sealed record MonsterSpawnDefinition(
    string InstanceId,
    string MonsterId,
    float X,
    float Y);
