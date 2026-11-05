using System.Numerics;
using DarkLoot.Data;
using DarkLoot.Data.Definitions;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Crafting;
using DarkLoot.Gameplay.Persistence;
using DarkLoot.Gameplay.Quests;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Tests.Gameplay;

public sealed class QuestServiceTests
{
    [Fact]
    public void Exploration_CountsOnlyAcceptedRealKillsOnceAndSurvivesEncounterReset()
    {
        var arena = CreateArena();
        Kill(arena);
        Assert.Equal(0, arena.Quests.Kills);
        Assert.Equal(QuestStatus.NotAccepted, arena.QuestOperations.TurnIn(arena.Player));
        Assert.Equal(QuestStatus.Applied, arena.QuestOperations.Accept(arena.Player));
        Assert.Equal(QuestStatus.AlreadyAccepted, arena.QuestOperations.Accept(arena.Player));
        Assert.Equal(0, arena.Quests.Kills);
        arena.ResetEncounter();
        Kill(arena);
        Assert.Equal(3, arena.Quests.Kills);
        arena.Update(new(Vector2.Zero, Vector2.UnitX, true, false), 1);
        Assert.Equal(3, arena.Quests.Kills);
        arena.ResetEncounter();
        Kill(arena);
        Assert.Equal(3, arena.Quests.Kills);

        arena.Player.Position = BlacksmithService.Position;
        Assert.Equal(QuestStatus.Applied, arena.QuestOperations.TurnIn(arena.Player));
        Assert.Equal(1, arena.Quests.ClaimedCount);
        Assert.False(arena.Quests.Accepted);
        Assert.Equal(2, arena.Player.Resources.GetCount(ResourceKind.Material, "material.bone"));
        Assert.Equal(2, arena.Player.Resources.GetCount(ResourceKind.Material, "material.slime"));
        Assert.Equal(QuestStatus.NotAccepted, arena.QuestOperations.TurnIn(arena.Player));
        Assert.Equal(2, arena.Player.Resources.GetCount(ResourceKind.Material, "material.bone"));
    }

    [Fact]
    public void Delivery_UsesCurrentInventoryAndConsumesOnlyOnSuccessfulTurnIn()
    {
        var arena = FirstClaimed();
        Assert.Equal(QuestStatus.Applied, arena.QuestOperations.Accept(arena.Player));
        Assert.Equal(QuestStatus.NotReady, arena.QuestOperations.TurnIn(arena.Player));
        PersistenceFixture.PickUp(arena, ResourceKind.Material, "material.bone", 2);
        PersistenceFixture.PickUp(arena, ResourceKind.Recipe, "recipe.magic_blade", 1);
        Assert.True(arena.QuestOperations.IsReady(arena.Player));
        Assert.Equal(BlacksmithStatus.Applied, arena.BlacksmithOperations.Learn(arena.Player, "recipe.magic_blade").Status);
        Assert.Equal(BlacksmithStatus.Applied, arena.BlacksmithOperations.Craft(arena.Player, "recipe.magic_blade").Status);
        Assert.False(arena.QuestOperations.IsReady(arena.Player));
        var bones = arena.Player.Resources.GetCount(ResourceKind.Material, "material.bone");
        Assert.Equal(QuestStatus.NotReady, arena.QuestOperations.TurnIn(arena.Player));
        Assert.Equal(bones, arena.Player.Resources.GetCount(ResourceKind.Material, "material.bone"));
        PersistenceFixture.PickUp(arena, ResourceKind.Material, "material.bone", 2);

        Assert.Equal(QuestStatus.Applied, arena.QuestOperations.TurnIn(arena.Player));
        Assert.Equal(0, arena.Player.Resources.GetCount(ResourceKind.Material, "material.bone"));
        Assert.Equal(1, arena.Player.Resources.GetCount(ResourceKind.Recipe, "recipe.magic_blade"));
        Assert.Equal(2, arena.Quests.ClaimedCount);
    }

    [Fact]
    public void Guardian_RequiresNewKillAfterAcceptanceAndAllRewardsAreOnceOnly()
    {
        var arena = FirstClaimed(guardian: true);
        arena.QuestOperations.Accept(arena.Player);
        PersistenceFixture.PickUp(arena, ResourceKind.Material, "material.bone", 2);
        Assert.Equal(QuestStatus.Applied, arena.QuestOperations.TurnIn(arena.Player));
        Assert.Equal(QuestStatus.Applied, arena.QuestOperations.Accept(arena.Player));
        Assert.False(arena.QuestOperations.IsReady(arena.Player));
        arena.ResetEncounter();
        Kill(arena);
        Assert.True(arena.QuestOperations.IsReady(arena.Player));
        arena.Player.Position = BlacksmithService.Position;

        Assert.Equal(QuestStatus.Applied, arena.QuestOperations.TurnIn(arena.Player));
        Assert.Equal(QuestStatus.AllCompleted, arena.QuestOperations.TurnIn(arena.Player));
        Assert.Equal(QuestStatus.AllCompleted, arena.QuestOperations.Accept(arena.Player));
        Assert.Equal(1, arena.Player.Resources.GetCount(ResourceKind.Recipe, "recipe.rare_vest"));
        Assert.Equal(2, arena.Player.Resources.GetCount(ResourceKind.Material, "material.hide"));
        Assert.False(arena.Blacksmith.HasLearned("recipe.rare_vest"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RewardOverflow_LeavesWholeCandidateUnchanged(bool delivery)
    {
        var arena = delivery ? FirstClaimed() : CreateArena();
        arena.QuestOperations.Accept(arena.Player);
        if (delivery)
        {
            PersistenceFixture.PickUp(arena, ResourceKind.Material, "material.bone", 2);
            PersistenceFixture.PickUp(arena, ResourceKind.Recipe, "recipe.magic_blade", int.MaxValue);
        }
        else
        {
            Kill(arena);
            arena.Player.Position = BlacksmithService.Position;
            PersistenceFixture.PickUp(arena, ResourceKind.Material, "material.slime", int.MaxValue);
        }
        var persistence = new ProgressPersistence(PersistenceFixture.LoadContent());
        var before = System.Text.Json.JsonSerializer.Serialize(persistence.Capture(arena));

        Assert.Equal(QuestStatus.ResourceOverflow, arena.QuestOperations.TurnIn(arena.Player));
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(persistence.Capture(arena)));
    }

    [Fact]
    public void AccessChecks_RejectDistantDeadAndDungeonInteractions()
    {
        var arena = CreateArena();
        arena.Player.Position = new(700, 270);
        Assert.Equal(QuestStatus.TooFar, arena.QuestOperations.Accept(arena.Player));
        arena.Player.Position = BlacksmithService.Position;
        arena.Player.Health = 0;
        Assert.Equal(QuestStatus.PlayerDead, arena.QuestOperations.Accept(arena.Player));
        Assert.Equal(QuestStatus.PlayerDead, arena.QuestOperations.TurnIn(arena.Player));
        Assert.False(arena.Quests.Accepted);
        var session = new DungeonSession(PersistenceFixture.LoadContent(), new Random(7));
        session.Arena.Player.Position = DungeonSession.HubEntrance;
        Assert.True(session.TryTravel());
        session.Arena.Player.Position = BlacksmithService.Position;
        Assert.Equal(QuestStatus.TooFar, session.Arena.QuestOperations.Accept(session.Arena.Player));
        Assert.Same(session.Hub.Quests, session.Dungeon.Quests);
    }

    internal static ArenaSimulation CreateArena(bool guardian = false)
    {
        var content = PersistenceFixture.LoadContent();
        content = content with
        {
            Monsters = content.Monsters.ToDictionary(pair => pair.Key,
                pair => pair.Value with { MaxHealth = 1, MoveSpeed = .01f, ContactDamage = 0 })
        };
        var spawns = Enumerable.Range(0, 3).Select(i => new MonsterSpawnDefinition(
            guardian && i == 0 ? "boss.guardian" : $"slime{i}",
            guardian && i == 0 ? "monster.crypt_guardian" : "monster.training_slime", 200, 270)).ToArray();
        return new(content, new Random(5), new("quest-test", spawns));
    }

    internal static void Kill(ArenaSimulation arena)
    {
        arena.Player.Position = new(160, 270);
        arena.Update(new(Vector2.Zero, new(200, 270), true, false), .4f);
    }

    private static ArenaSimulation FirstClaimed(bool guardian = false)
    {
        var arena = CreateArena(guardian);
        Assert.Equal(QuestStatus.Applied, arena.QuestOperations.Accept(arena.Player));
        Kill(arena);
        arena.Player.Position = BlacksmithService.Position;
        Assert.Equal(QuestStatus.Applied, arena.QuestOperations.TurnIn(arena.Player));
        return arena;
    }
}
