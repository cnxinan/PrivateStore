using DarkLoot.Data;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Gameplay.Quests;

public enum QuestStatus
{
    Applied, PlayerDead, TooFar, AlreadyAccepted, NotAccepted, NotReady, AllCompleted, ResourceOverflow
}

public sealed class QuestService(GameContent content, QuestState state, BlacksmithService smith)
{
    public bool IsReady(PlayerState player) =>
        state.Accepted && state.Current is { } quest &&
        (quest.Objective == QuestObjective.Materials
            ? quest.Costs.All(cost => player.Resources.GetCount(ResourceKind.Material, cost.MaterialId) >= cost.Quantity)
            : state.Kills >= quest.Required);

    public QuestStatus Accept(PlayerState player)
    {
        var access = CheckAccess(player);
        if (access is not null)
            return access.Value;
        if (state.Current is null)
            return QuestStatus.AllCompleted;
        if (state.Accepted)
            return QuestStatus.AlreadyAccepted;
        state.Commit(new(state.ClaimedCount, true, 0));
        return QuestStatus.Applied;
    }

    public QuestStatus TurnIn(PlayerState player)
    {
        var access = CheckAccess(player);
        if (access is not null)
            return access.Value;
        if (state.Current is not { } quest)
            return QuestStatus.AllCompleted;
        if (!state.Accepted)
            return QuestStatus.NotAccepted;
        if (!IsReady(player))
            return QuestStatus.NotReady;
        foreach (var reward in quest.Rewards)
        {
            if (reward.Kind == ResourceKind.Material ? !content.Materials.ContainsKey(reward.DefinitionId) :
                !content.Recipes.ContainsKey(reward.DefinitionId))
                throw new InvalidOperationException($"Quest reward '{reward.DefinitionId}' is missing from game content.");
        }
        try
        {
            var resources = player.Resources.PrepareQuest(quest.Costs, quest.Rewards);
            var progress = new QuestProgress(state.ClaimedCount + 1, false, 0);
            player.Resources.Commit(resources);
            state.Commit(progress);
        }
        catch (OverflowException)
        {
            return QuestStatus.ResourceOverflow;
        }
        return QuestStatus.Applied;
    }

    private QuestStatus? CheckAccess(PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.IsDead ? QuestStatus.PlayerDead : !smith.CanInteract(player) ? QuestStatus.TooFar : null;
    }
}
