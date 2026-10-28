using System;
using System.Collections.Generic;
using System.Linq;
using DarkLoot.Data.Definitions;
using DarkLoot.Engine.Animation;
using DarkLoot.Gameplay.Combat;
using Vector2 = System.Numerics.Vector2;

namespace DarkLoot.Game;

public sealed record ActorHitText(string ActorId, Vector2 Position, int Damage, bool WasCritical, float Remaining);

public sealed class ActorPresentation
{
    private const float FeedbackSeconds = .18f;
    private sealed class Entry(ActorVisualKind kind, Vector2 position)
    {
        public ActorAnimator Animator { get; } = new(kind);
        public Vector2 PreviousPosition { get; set; } = position;
        public bool WasReacting { get; set; }
    }

    private readonly Dictionary<string, Entry> _monsters = new(StringComparer.Ordinal);
    private readonly HashSet<CombatEvent> _seenEvents = new(ReferenceEqualityComparer.Instance);
    private readonly List<ActorHitText> _hitTexts = [];
    private readonly IReadOnlyList<ActorHitText> _readOnlyHitTexts;
    private Entry _player = new(ActorVisualKind.Player, Vector2.Zero);
    private ArenaSimulation? _arena;
    private long _resetSequence;

    public ActorPresentation() => _readOnlyHitTexts = _hitTexts.AsReadOnly();

    public ActorPose PlayerPose => _player.Animator.Pose;
    public IReadOnlyList<ActorHitText> HitTexts => _readOnlyHitTexts;
    public float SwingRemaining { get; private set; }
    public bool SwingHasHit { get; private set; }
    public bool SwingWasCritical { get; private set; }
    public ActorPose GetMonsterPose(string instanceId) => _monsters[instanceId].Animator.Pose;

    public void Reset(ArenaSimulation arena)
    {
        ArgumentNullException.ThrowIfNull(arena);
        _arena = arena;
        _resetSequence = arena.ResetSequence;
        _hitTexts.Clear();
        _seenEvents.Clear();
        _seenEvents.UnionWith(arena.Events);
        SwingRemaining = 0;
        SwingHasHit = SwingWasCritical = false;
        _player = new(ActorVisualKind.Player, arena.Player.Position);
        Evaluate(_player, arena.Player.Position, arena.Player.Reaction.IsActive, arena.Player.IsDead,
            arena.Player.IsInvulnerable, false, 0, null, arena.Player.Facing, 0);
        _monsters.Clear();
        foreach (var monster in arena.Monsters)
        {
            var kind = monster.Behavior == MonsterBehavior.Archer ? ActorVisualKind.Archer :
                monster.Id == "monster.fast_wolf" ? ActorVisualKind.Wolf : ActorVisualKind.Slime;
            var entry = new Entry(kind, monster.Position);
            _monsters.Add(monster.InstanceId, entry);
            EvaluateMonster(entry, monster, null, 0);
        }
    }

    public void Update(ArenaSimulation arena, float elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(arena);
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!ReferenceEquals(_arena, arena) || _resetSequence != arena.ResetSequence)
        {
            Reset(arena);
            return;
        }

        SwingRemaining = Math.Max(0, SwingRemaining - elapsedSeconds);
        for (var index = _hitTexts.Count - 1; index >= 0; index--)
        {
            var text = _hitTexts[index];
            var remaining = text.Remaining - elapsedSeconds;
            if (remaining <= 0)
                _hitTexts.RemoveAt(index);
            else
                _hitTexts[index] = text with { Remaining = remaining };
        }
        // Events are per simulation Update records, not value identities: identical later hits are new hits.
        var events = arena.Events.Where(item => !_seenEvents.Contains(item)).ToArray();
        _seenEvents.Clear();
        _seenEvents.UnionWith(arena.Events);
        foreach (var hit in events.Where(item => item.Kind == CombatEventKind.Damaged))
            _hitTexts.Add(new(hit.ActorId, hit.Position, hit.Damage, hit.WasCritical, FeedbackSeconds));
        var swing = events.FirstOrDefault(item => item.Kind == CombatEventKind.Swing);
        if (swing is not null)
        {
            SwingRemaining = Math.Max(0, FeedbackSeconds - elapsedSeconds);
            SwingHasHit = arena.LastSwingHits.Count > 0;
            SwingWasCritical = arena.LastSwingHits.Any(hit => hit.WasCritical);
        }
        if (arena.Player.IsDead || arena.Player.Reaction.IsActive)
            SwingRemaining = 0;

        Evaluate(_player, arena.Player.Position, arena.Player.Reaction.IsActive, arena.Player.IsDead,
            arena.Player.IsInvulnerable, false, 0, swing, arena.Player.Facing,
            elapsedSeconds);
        foreach (var monster in arena.Monsters)
        {
            var attack = events.FirstOrDefault(item => item.ActorId == monster.InstanceId &&
                item.Kind is CombatEventKind.ContactAttack or CombatEventKind.ArrowLaunched);
            EvaluateMonster(_monsters[monster.InstanceId], monster, attack, elapsedSeconds);
        }
    }

    private static void EvaluateMonster(Entry entry, MonsterState monster, CombatEvent? attack, float elapsed) =>
        Evaluate(entry, monster.Position, monster.Reaction.IsActive, monster.IsDefeated, false,
            monster.IsWindingUp, 1 - monster.WindupRemaining / .3f, attack,
            monster.IsWindingUp ? monster.LockedAimDirection : monster.Facing, elapsed);

    private static void Evaluate(Entry entry, Vector2 position, bool hurt, bool dead, bool invulnerable,
        bool windingUp, float windupProgress, CombatEvent? attack, Vector2 facing, float elapsed)
    {
        var moving = !dead && !hurt && !entry.WasReacting &&
            Vector2.DistanceSquared(position, entry.PreviousPosition) > .00000001f;
        var input = new ActorAnimationInput(moving, hurt, dead, invulnerable, windingUp,
            windupProgress, attack is not null, attack?.Direction ?? facing);
        // Reactions/death accepted at the end of combat have not spent this Update's elapsed yet.
        var newReaction = dead && entry.Animator.Action != ActorAction.Dead ||
            hurt && !entry.WasReacting;
        entry.Animator.Update(input, newReaction ? 0 : elapsed);
        entry.PreviousPosition = position;
        entry.WasReacting = hurt;
    }
}
