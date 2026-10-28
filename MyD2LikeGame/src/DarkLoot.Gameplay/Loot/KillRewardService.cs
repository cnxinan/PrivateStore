using DarkLoot.Data;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Gameplay.Loot;

public enum KillRewardStatus
{
    Granted,
    AlreadyResolved,
    WrongRound
}

public sealed record KillRewardResult(
    KillRewardStatus Status,
    IReadOnlyList<ResourceStack> Resources);

public sealed class KillRewardService
{
    private readonly GameContent _content;
    private readonly Random _random;
    private readonly ResourceLootGenerator _generator = new();
    private readonly HashSet<string> _resolved = new(StringComparer.Ordinal);
    private long? _roundId;

    public KillRewardService(GameContent content, Random random)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(random);
        _content = content;
        _random = random;
    }

    public void BeginRound(long roundId)
    {
        if (roundId < 0 ||
            (_roundId is null && roundId != 0) ||
            (_roundId is { } previous && roundId <= previous))
        {
            throw new ArgumentOutOfRangeException(nameof(roundId), "Round IDs must increase.");
        }

        _resolved.Clear();
        _roundId = roundId;
    }

    public KillRewardResult Resolve(KillRecord kill)
    {
        ArgumentNullException.ThrowIfNull(kill);
        if (kill.RoundId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(kill), "Round ID must not be negative.");
        }

        if (string.IsNullOrWhiteSpace(kill.MonsterInstanceId))
        {
            throw new ArgumentException("Monster instance ID must not be empty.", nameof(kill));
        }

        if (string.IsNullOrWhiteSpace(kill.LootTableId))
        {
            throw new ArgumentException("Loot table ID must not be empty.", nameof(kill));
        }

        if (!float.IsFinite(kill.DeathPosition.X) || !float.IsFinite(kill.DeathPosition.Y))
        {
            throw new ArgumentException("Death position must contain finite coordinates.", nameof(kill));
        }

        if (_roundId is not { } currentRoundId)
        {
            throw new InvalidOperationException("BeginRound must be called before resolving kills.");
        }

        if (kill.RoundId != currentRoundId)
        {
            return new KillRewardResult(
                KillRewardStatus.WrongRound,
                Array.Empty<ResourceStack>());
        }

        if (_resolved.Contains(kill.MonsterInstanceId))
        {
            return new KillRewardResult(
                KillRewardStatus.AlreadyResolved,
                Array.Empty<ResourceStack>());
        }

        if (!_content.ResourceLootTables.TryGetValue(kill.LootTableId, out var table) || table is null)
        {
            throw new ArgumentException(
                $"Resource loot table '{kill.LootTableId}' was not found.",
                nameof(kill));
        }

        var resources = _generator.Generate(table, _content, _random);
        _resolved.Add(kill.MonsterInstanceId);
        return new KillRewardResult(KillRewardStatus.Granted, resources);
    }
}
