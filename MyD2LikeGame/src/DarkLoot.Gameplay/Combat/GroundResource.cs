using System.Numerics;
using DarkLoot.Gameplay.Resources;

namespace DarkLoot.Gameplay.Combat;

public sealed record GroundResource(Guid DropId, ResourceStack Stack, Vector2 Position);
