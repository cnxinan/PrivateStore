using System.Numerics;
using DarkLoot.Gameplay.Combat;

namespace DarkLoot.Tests.Gameplay;

public sealed class WorldMapTests
{
    [Fact]
    public void MovementSlidesAlongWallsWithoutTunnelingThroughTombs()
    {
        var map = WorldMap.Crypt();
        var slid = map.Move(new Vector2(900, 100), new Vector2(300, 100));
        Assert.True(map.CanOccupy(slid));
        Assert.InRange(slid.X, 900, 946.01f);
        Assert.InRange(slid.Y, 199, 201);

        var stopped = map.Move(new Vector2(300, 132), new Vector2(2400, 0));
        Assert.InRange(stopped.X, 337.9f, 338.01f);
        Assert.True(map.CanOccupy(stopped));
    }

    [Fact]
    public void GateBlocksMovementUntilOpened()
    {
        var map = WorldMap.Crypt();
        Assert.False(map.CanOccupy(new Vector2(2192, 270)));
        Assert.InRange(map.Move(new Vector2(2120, 270), new Vector2(200, 0)).X, 2169.9f, 2170.01f);
        map.BossGateOpen = true;
        Assert.Equal(new Vector2(2320, 270), map.Move(new Vector2(2120, 270), new Vector2(200, 0)));
        Assert.True(map.CanOccupy(new Vector2(2192, 270)));
        Assert.DoesNotContain(map.BossGate, map.Obstacles);
    }

    [Fact]
    public void ProjectilesClipAtEarliestObstacleWallOrGate()
    {
        var map = WorldMap.Crypt();
        Assert.Equal(new Vector2(352, 132), map.ClipTravel(new Vector2(300, 132), new Vector2(800, 132)));
        Assert.Equal(new Vector2(960, 100), map.ClipTravel(new Vector2(900, 100), new Vector2(1200, 100)));
        Assert.Equal(new Vector2(2184, 270), map.ClipTravel(new Vector2(2100, 270), new Vector2(2300, 270)));
        Assert.False(map.HasLineOfSight(new Vector2(300, 132), new Vector2(800, 132)));
        Assert.False(map.HasLineOfSight(new Vector2(2100, 270), new Vector2(2300, 270)));
        Assert.True(map.HasLineOfSight(new Vector2(900, 270), new Vector2(1200, 270)));
        map.BossGateOpen = true;
        Assert.True(map.HasLineOfSight(new Vector2(2100, 270), new Vector2(2300, 270)));
        Assert.Equal(new Vector2(2300, 270), map.ClipTravel(new Vector2(2100, 270), new Vector2(2300, 270)));
    }

    [Fact]
    public void NavigationRoutesAroundTombFromAnArbitraryStart()
    {
        var map = WorldMap.Crypt();
        FollowPath(map, new Vector2(337.5f, 132.5f), new Vector2(550, 132));
    }

    [Theory]
    [InlineData(324, 132)]
    [InlineData(348, 204)]
    [InlineData(936, 204)]
    public void NavigationMakesProgressFromGridCentersAndNearCorridorCorners(float x, float y)
    {
        FollowPath(WorldMap.Crypt(), new Vector2(x, y), new Vector2(1600, 270));
    }

    [Fact]
    public void ExtremeFiniteMotionStillStopsAtWalls()
    {
        var map = WorldMap.Hub();
        var result = map.Move(new Vector2(100, 100), new Vector2(float.MaxValue, float.MaxValue));
        Assert.True(map.CanOccupy(result));
        Assert.InRange(result.X, 945.9f, 946);
        Assert.InRange(result.Y, 525.9f, 526);
    }

    [Fact]
    public void NavigationRoutesThroughCorridorAndAroundPillars()
    {
        var map = WorldMap.Crypt();
        FollowPath(map, new Vector2(850, 90), new Vector2(1900, 400));
        FollowPath(map, new Vector2(1400, 174), new Vector2(1600, 174));
    }

    [Fact]
    public void ClosedGateMakesBossRoomUnreachable()
    {
        var map = WorldMap.Crypt();
        var start = new Vector2(1900, 270);
        var target = new Vector2(2500, 270);
        Assert.Equal(start, map.NextWaypoint(start, target));
        map.BossGateOpen = true;
        FollowPath(map, start, target);
    }

    [Fact]
    public void CircleClearanceIncludesCornersAndCorridorEdges()
    {
        var map = WorldMap.Crypt();
        Assert.False(map.CanOccupy(new Vector2(965, 218)));
        Assert.True(map.CanOccupy(new Vector2(970, 225)));
        Assert.False(map.CanOccupy(new Vector2(345, 89)));
        Assert.True(map.CanOccupy(new Vector2(340, 84)));
        Assert.True(WorldMap.Hub().CanOccupy(new Vector2(14, 14)));
        Assert.False(WorldMap.Hub().CanOccupy(new Vector2(13, 14)));
    }

    [Fact]
    public void InvalidNumericArgumentsAreRejectedExplicitly()
    {
        var map = WorldMap.Crypt();
        var point = new Vector2(100, 100);
        var invalid = new Vector2(float.NaN, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => map.CanOccupy(invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.CanOccupy(point, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Move(point, new Vector2(float.PositiveInfinity, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Move(point, Vector2.Zero, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.ClipTravel(point, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.HasLineOfSight(invalid, point));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.NextWaypoint(point, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.NextWaypoint(point, point, float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldRect(0, 0, 1, 1).Contains(invalid));
    }

    private static void FollowPath(WorldMap map, Vector2 start, Vector2 target)
    {
        var position = start;
        for (var step = 0; step < 900 && Vector2.Distance(position, target) > 1; step++)
        {
            var waypoint = map.NextWaypoint(position, target);
            Assert.True(map.CanOccupy(waypoint));
            var offset = waypoint - position;
            Assert.True(offset.Length() > 0.001f, $"Path stalled at {position} towards {target}.");
            var movement = Vector2.Normalize(offset) * Math.Min(8, offset.Length());
            var next = map.Move(position, movement);
            Assert.True(map.CanOccupy(next));
            Assert.True(Vector2.Distance(position, next) > 0.001f);
            position = next;
        }

        Assert.InRange(Vector2.Distance(position, target), 0, 1);
    }
}
