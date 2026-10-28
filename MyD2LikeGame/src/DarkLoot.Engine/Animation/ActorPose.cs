using System.Numerics;

namespace DarkLoot.Engine.Animation;

public enum ActorVisualKind { Player, Slime, Wolf, Archer }
public enum ActorAction { Idle, Move, Attack, Hurt, Dead }

/// <summary>Update snapshot. AttackTriggered is a one-Update impulse; Hurt and WindingUp are continuous states.</summary>
public readonly record struct ActorAnimationInput(
    bool Moving, bool Hurt, bool Dead, bool Invulnerable,
    bool WindingUp, float WindupProgress,
    bool AttackTriggered, Vector2 Facing);

/// <summary>Shape-local numeric presentation, independent of world position and combat. Rotation is in radians.</summary>
public readonly record struct ActorPose(
    Vector2 Offset, Vector2 Scale, float Rotation, float Opacity,
    float WhiteMix, float LimbSwing, float AttackProgress, Vector2 Facing);
