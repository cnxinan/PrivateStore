using System.Collections.ObjectModel;

namespace DarkLoot.Gameplay.Crafting;

public sealed class BlacksmithState
{
    private HashSet<string> _knownRecipeIds = new(StringComparer.Ordinal);
    private IReadOnlyCollection<string> _readOnlyKnownRecipeIds = Array.Empty<string>();

    public IReadOnlyCollection<string> KnownRecipeIds => _readOnlyKnownRecipeIds;

    public bool HasLearned(string recipeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        return _knownRecipeIds.Contains(recipeId);
    }

    internal BlacksmithStateSnapshot PrepareLearn(string recipeId)
    {
        var candidate = new HashSet<string>(_knownRecipeIds, StringComparer.Ordinal);
        candidate.Add(recipeId);
        var knownRecipeIds = new ReadOnlyCollection<string>(candidate.ToList());
        return new BlacksmithStateSnapshot(candidate, knownRecipeIds);
    }

    internal void Commit(BlacksmithStateSnapshot snapshot)
    {
        _knownRecipeIds = snapshot.KnownRecipeIds;
        _readOnlyKnownRecipeIds = snapshot.ReadOnlyKnownRecipeIds;
    }
}

internal sealed record BlacksmithStateSnapshot(
    HashSet<string> KnownRecipeIds,
    IReadOnlyCollection<string> ReadOnlyKnownRecipeIds);
