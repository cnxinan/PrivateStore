using System.Numerics;

namespace DarkLoot.Engine.Animation;

public sealed class ActorAnimator
{
    private const float PlayerSwingSeconds = .18f;
    private const float ArcherRecoilSeconds = .12f;
    private const float ContactAttackSeconds = .16f;
    private const float PlayerHurtSeconds = .15f;
    private const float MonsterHurtSeconds = .12f;
    private const float PlayerDeathSeconds = .6f;
    private const float MonsterDeathSeconds = .3f;
    private const float BreathingAmplitude = .015f;
    private const float SlimeSquashAmplitude = .08f;
    private const float MovementBob = 1.2f;
    private const float LimbAmplitude = .35f;
    private const float MovementBlendRate = 12;
    private const float HurtCompression = .12f;
    private const float ImmunityBrightness = .25f;
    private readonly ActorVisualKind _kind;
    private double _phaseSeconds;
    private double _actionSeconds;
    private double _movementWeight;
    private Vector2 _facing;
    private Vector2 _actionFacing;
    private bool _wasHurt;
    private bool _windingUp;

    public ActorAnimator(ActorVisualKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        _kind = kind;
        Reset();
    }

    public ActorPose Pose { get; private set; }
    public ActorAction Action { get; private set; }

    public void Update(ActorAnimationInput input, float elapsedSeconds)
    {
        Validate(input, elapsedSeconds);
        if (Action == ActorAction.Dead)
        {
            _actionSeconds = Math.Min(DeathSeconds, _actionSeconds + elapsedSeconds);
            Pose = DeathPose();
            return;
        }

        _facing = NormalizeFacing(input.Facing, _facing);
        if (input.Dead)
        {
            Action = ActorAction.Dead;
            _actionFacing = _facing;
            _actionSeconds = Math.Min(DeathSeconds, elapsedSeconds);
            _windingUp = false;
            Pose = DeathPose();
            return;
        }

        _phaseSeconds = (_phaseSeconds + elapsedSeconds) % 4;
        var target = input.Moving ? 1d : 0d;
        _movementWeight = target + (_movementWeight - target) * Math.Exp(-MovementBlendRate * (double)elapsedSeconds);

        if (input.Hurt && !_wasHurt)
        {
            Action = ActorAction.Hurt;
            _actionSeconds = 0;
        }
        _wasHurt = input.Hurt;

        // Impulses while still hurt are discarded, never queued for recovery.
        if (Action == ActorAction.Hurt)
        {
            _windingUp = false;
            _actionSeconds = Math.Min(HurtSeconds, _actionSeconds + elapsedSeconds);
            if (input.Hurt || _actionSeconds < HurtSeconds)
            {
                Pose = LivingPose(input);
                return;
            }
            Action = input.Moving ? ActorAction.Move : ActorAction.Idle;
        }
        if (input.AttackTriggered)
        {
            Action = ActorAction.Attack;
            _actionSeconds = 0;
            _actionFacing = _facing;
            _windingUp = false;
        }

        if (Action == ActorAction.Attack && !_windingUp)
        {
            _actionSeconds = Math.Min(AttackSeconds, _actionSeconds + elapsedSeconds);
            if (_actionSeconds >= AttackSeconds)
                Action = input.Moving ? ActorAction.Move : ActorAction.Idle;
        }

        if (Action != ActorAction.Attack || _windingUp)
        {
            if (_kind == ActorVisualKind.Archer && input.WindingUp)
            {
                if (!_windingUp)
                    _actionFacing = _facing;
                _windingUp = true;
                Action = ActorAction.Attack;
            }
            else
            {
                _windingUp = false;
                Action = input.Moving ? ActorAction.Move : ActorAction.Idle;
            }
        }
        Pose = LivingPose(input);
    }

    public void Reset()
    {
        _phaseSeconds = 0;
        _actionSeconds = 0;
        _movementWeight = 0;
        _facing = _actionFacing = Vector2.UnitX;
        _wasHurt = _windingUp = false;
        Action = ActorAction.Idle;
        Pose = new(Vector2.Zero, Vector2.One, 0, 1, 0, 0, 0, _facing);
    }

    private float AttackSeconds => _kind switch
    {
        ActorVisualKind.Player => PlayerSwingSeconds,
        ActorVisualKind.Archer => ArcherRecoilSeconds,
        _ => ContactAttackSeconds
    };

    private float HurtSeconds => _kind == ActorVisualKind.Player ? PlayerHurtSeconds : MonsterHurtSeconds;
    private float DeathSeconds => _kind == ActorVisualKind.Player ? PlayerDeathSeconds : MonsterDeathSeconds;

    private ActorPose LivingPose(ActorAnimationInput input)
    {
        var breath = (float)Math.Sin(_phaseSeconds * Math.Tau * .75) * BreathingAmplitude;
        var stride = (float)Math.Sin(_phaseSeconds * Math.Tau * 3);
        var movement = (float)_movementWeight;
        var scale = new Vector2(1 - breath, 1 + breath);
        var offset = new Vector2(0, -stride * MovementBob * movement);
        var limbs = _kind is ActorVisualKind.Player or ActorVisualKind.Wolf or ActorVisualKind.Archer
            ? stride * LimbAmplitude * movement : 0;
        if (_kind == ActorVisualKind.Slime)
        {
            var squash = stride * SlimeSquashAmplitude * movement;
            scale += new Vector2(squash, -squash);
        }
        var rotation = 0f;
        var white = input.Invulnerable
            ? ImmunityBrightness * (1 + (float)Math.Sin(_phaseSeconds * Math.Tau)) / 2 : 0;
        var progress = 0f;
        var facing = _facing;
        if (Action == ActorAction.Hurt)
        {
            var strength = 1 - Smooth((float)(_actionSeconds / HurtSeconds));
            scale += new Vector2(HurtCompression, -HurtCompression) * strength;
            white = Math.Max(white, strength);
            limbs *= 1 - strength;
        }
        else if (Action == ActorAction.Attack)
        {
            facing = _actionFacing;
            progress = _windingUp ? Math.Clamp(input.WindupProgress, 0, 1)
                : (float)(_actionSeconds / AttackSeconds);
            var arc = MathF.Sin(progress * MathF.PI);
            switch (_kind)
            {
                case ActorVisualKind.Player:
                    offset += facing * (1.5f * arc);
                    rotation = (Smooth(progress) * 2 - 1) * .55f;
                    limbs = arc * .6f;
                    break;
                case ActorVisualKind.Slime:
                    scale += new Vector2(.12f, -.1f) * arc;
                    offset += facing * arc;
                    break;
                case ActorVisualKind.Wolf:
                    offset += facing * (2 * arc);
                    scale += new Vector2(.06f, -.04f) * arc;
                    break;
                case ActorVisualKind.Archer:
                    offset -= facing * (_windingUp ? Smooth(progress) : arc) * 1.5f;
                    limbs = _windingUp ? .5f * Smooth(progress) : -.3f * arc;
                    break;
            }
        }
        return new(offset, scale, rotation, 1, white, limbs, progress, facing);
    }

    private ActorPose DeathPose()
    {
        // Evaluate the remaining curve directly to avoid cancellation to zero before the endpoint.
        var remaining = (DeathSeconds - _actionSeconds) / DeathSeconds;
        var opacity = (float)(remaining * remaining * (3 - 2 * remaining));
        var scale = Vector2.One * opacity;
        var rotation = _kind == ActorVisualKind.Player ? (1 - opacity) * MathF.PI / 2 : 0;
        return new(Vector2.Zero, scale, rotation, opacity, 0, 0, 0, _actionFacing);
    }

    private static float Smooth(float progress) => progress * progress * (3 - 2 * progress);

    private static Vector2 NormalizeFacing(Vector2 facing, Vector2 fallback)
    {
        var length = Math.Sqrt((double)facing.X * facing.X + (double)facing.Y * facing.Y);
        return length == 0 ? fallback : new((float)(facing.X / length), (float)(facing.Y / length));
    }

    private static void Validate(ActorAnimationInput input, float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!float.IsFinite(input.WindupProgress) || !float.IsFinite(input.Facing.X) || !float.IsFinite(input.Facing.Y))
            throw new ArgumentOutOfRangeException(nameof(input), "Animation input must be finite.");
    }
}
