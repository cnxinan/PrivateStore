namespace DarkLoot.Gameplay.Quests;

public sealed record QuestProgress(int ClaimedCount, bool Accepted, int Kills);

public sealed class QuestState
{
    public QuestProgress Progress { get; private set; } = new(0, false, 0);
    public int ClaimedCount => Progress.ClaimedCount;
    public bool Accepted => Progress.Accepted;
    public int Kills => Progress.Kills;
    public QuestDefinition? Current => ClaimedCount < SmithQuests.All.Count ? SmithQuests.All[ClaimedCount] : null;

    internal void Commit(QuestProgress progress) => Progress = progress;

    internal void CreditKill(string monsterId)
    {
        if (!Accepted || Current is not { } quest || quest.Objective == QuestObjective.Materials ||
            quest.Objective == QuestObjective.Guardian && monsterId != "monster.crypt_guardian")
            return;
        Progress = Progress with { Kills = Math.Min(quest.Required, Kills + 1) };
    }

    internal static void Validate(QuestProgress progress)
    {
        if (progress.ClaimedCount < 0 || progress.ClaimedCount > SmithQuests.All.Count ||
            progress.Kills < 0 ||
            progress.ClaimedCount == SmithQuests.All.Count && (progress.Accepted || progress.Kills != 0) ||
            !progress.Accepted && progress.Kills != 0)
            throw new ArgumentException("Quest sequence or acceptance state is invalid.");
        if (progress.ClaimedCount < SmithQuests.All.Count)
        {
            var quest = SmithQuests.All[progress.ClaimedCount];
            if (progress.Kills > quest.Required ||
                quest.Objective == QuestObjective.Materials && progress.Kills != 0)
                throw new ArgumentException("Quest kill progress is invalid.");
        }
    }
}
