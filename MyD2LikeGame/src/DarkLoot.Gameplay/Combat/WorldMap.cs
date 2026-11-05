using System.Numerics;

namespace DarkLoot.Gameplay.Combat;

public readonly record struct WorldRect(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Right => X + Width;
    public int Top => Y;
    public int Bottom => Y + Height;

    public bool Contains(Vector2 position)
    {
        WorldMap.Validate(position, nameof(position));
        return position.X >= Left && position.X <= Right &&
            position.Y >= Top && position.Y <= Bottom;
    }
}

public sealed class WorldMap
{
    private const int CellSize = 24;
    private const double ContactMargin = 0.001;
    private readonly WorldRect[] _walls;
    private readonly WorldRect[] _obstacles;
    private readonly int _columns;
    private readonly int _rows;
    private readonly bool[] _walkable;
    private readonly double[] _cost;
    private readonly int[] _parent;
    private readonly bool[] _visited;
    private readonly PriorityQueue<int, (double Cost, int Index)> _frontier = new();
    private float _gridRadius = -1;
    private bool _gridGateOpen;

    private WorldMap(bool dungeon, int width, WorldRect[] floors, WorldRect[] obstacles, WorldRect[] walls)
    {
        IsDungeon = dungeon;
        Width = width;
        Height = 540;
        Floors = Array.AsReadOnly(floors);
        _obstacles = obstacles;
        Obstacles = Array.AsReadOnly(obstacles);
        _walls = walls;
        BossGate = dungeon ? new WorldRect(2184, 210, 16, 120) : default;
        BossGateOpen = !dungeon;
        _columns = (Width + CellSize - 1) / CellSize;
        _rows = (Height + CellSize - 1) / CellSize;
        var count = _columns * _rows;
        _walkable = new bool[count];
        _cost = new double[count];
        _parent = new int[count];
        _visited = new bool[count];
    }

    public static WorldMap Hub() => new(false, 960,
        [new(0, 0, 960, 540)], [], []);

    public static WorldMap Crypt() => new(true, 3200,
        [new(0, 0, 960, 540), new(960, 210, 160, 120), new(1120, 0, 960, 540),
         new(2080, 210, 160, 120), new(2240, 0, 960, 540)],
        [new(352, 96, 128, 72), new(1472, 144, 64, 64),
         new(1728, 336, 64, 64), new(2720, 112, 96, 64)],
        [new(960, 0, 160, 210), new(960, 330, 160, 210),
         new(2080, 0, 160, 210), new(2080, 330, 160, 210)]);

    public bool IsDungeon { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<WorldRect> Floors { get; }
    public IReadOnlyList<WorldRect> Obstacles { get; }
    public WorldRect BossGate { get; }
    public bool BossGateOpen { get; set; }

    public bool CanOccupy(Vector2 position, float radius = 14)
    {
        Validate(position, nameof(position));
        ValidateRadius(radius);
        if (position.X < radius || position.Y < radius ||
            position.X > Width - (double)radius || position.Y > Height - (double)radius)
            return false;

        foreach (var wall in _walls)
            if (Overlaps(position, radius, wall))
                return false;
        foreach (var obstacle in _obstacles)
            if (Overlaps(position, radius, obstacle))
                return false;
        return !IsDungeon || BossGateOpen || !Overlaps(position, radius, BossGate);
    }

    public Vector2 Move(Vector2 position, Vector2 displacement, float radius = 14)
    {
        Validate(position, nameof(position));
        Validate(displacement, nameof(displacement));
        ValidateRadius(radius);
        if (!CanOccupy(position, radius))
            return position;

        double dx = displacement.X, dy = displacement.Y;
        for (var iteration = 0; iteration < 8 && (dx != 0 || dy != 0); iteration++)
        {
            var hit = FirstHit(position, dx, dy, radius);
            if (hit.Time > 1)
                return PointAlong(position, dx, dy, 1);

            var contact = PointAlong(position, dx, dy, hit.Time);
            var separated = new Vector2(
                (float)(contact.X + hit.NormalX * ContactMargin),
                (float)(contact.Y + hit.NormalY * ContactMargin));
            position = CanOccupy(separated, radius) ? separated : contact;
            dx *= 1 - hit.Time;
            dy *= 1 - hit.Time;
            var inward = dx * hit.NormalX + dy * hit.NormalY;
            if (inward < 0)
            {
                dx -= inward * hit.NormalX;
                dy -= inward * hit.NormalY;
            }
        }
        return position;
    }

    public Vector2 ClipTravel(Vector2 start, Vector2 end)
    {
        Validate(start, nameof(start));
        Validate(end, nameof(end));
        if (!CanOccupy(start, 0))
            return start;
        var hit = FirstHit(start, (double)end.X - start.X, (double)end.Y - start.Y, 0);
        return hit.Time > 1 ? end : PointAlong(start, (double)end.X - start.X, (double)end.Y - start.Y, hit.Time);
    }

    public bool HasLineOfSight(Vector2 start, Vector2 end)
    {
        Validate(start, nameof(start));
        Validate(end, nameof(end));
        return ClearSegment(start, end, 0);
    }

    public Vector2 NextWaypoint(Vector2 start, Vector2 target, float radius = 14)
    {
        Validate(start, nameof(start));
        Validate(target, nameof(target));
        ValidateRadius(radius);
        if (!CanOccupy(start, radius) || !CanOccupy(target, radius))
            return start;
        if (ClearSegment(start, target, radius))
            return target;

        PrepareGrid(radius);
        Array.Fill(_cost, double.PositiveInfinity);
        Array.Fill(_parent, -1);
        Array.Clear(_visited);
        _frontier.Clear();
        var startColumn = (int)start.X / CellSize;
        var startRow = (int)start.Y / CellSize;
        // Connect the real start, not a snapped point that could lie across a wall.
        for (var y = Math.Max(0, startRow - 2); y <= Math.Min(_rows - 1, startRow + 2); y++)
            for (var x = Math.Max(0, startColumn - 2); x <= Math.Min(_columns - 1, startColumn + 2); x++)
            {
                var index = y * _columns + x;
                var point = GridPoint(index);
                if (!_walkable[index] || !ClearSegment(start, point, radius))
                    continue;
                _cost[index] = Vector2.Distance(start, point);
                _frontier.Enqueue(index, (_cost[index] + Vector2.Distance(point, target), index));
            }

        while (_frontier.TryDequeue(out var current, out _))
        {
            if (_visited[current])
                continue;
            _visited[current] = true;
            var point = GridPoint(current);
            if (Vector2.DistanceSquared(point, target) <= CellSize * CellSize * 9 &&
                ClearSegment(point, target, radius))
            {
                var next = current;
                while (_parent[next] >= 0)
                    next = _parent[next];
                return GridPoint(next);
            }

            var column = current % _columns;
            var row = current / _columns;
            for (var oy = -1; oy <= 1; oy++)
                for (var ox = -1; ox <= 1; ox++)
                {
                    if ((ox == 0 && oy == 0) || column + ox < 0 || column + ox >= _columns ||
                        row + oy < 0 || row + oy >= _rows)
                        continue;
                    var neighbor = (row + oy) * _columns + column + ox;
                    if (_visited[neighbor] || !_walkable[neighbor])
                        continue;
                    if (ox != 0 && oy != 0 &&
                        (!_walkable[row * _columns + column + ox] ||
                         !_walkable[(row + oy) * _columns + column]))
                        continue;
                    var nextPoint = GridPoint(neighbor);
                    var cost = _cost[current] + Vector2.Distance(point, nextPoint);
                    if (cost >= _cost[neighbor] || !ClearSegment(point, nextPoint, radius))
                        continue;
                    _cost[neighbor] = cost;
                    _parent[neighbor] = current;
                    _frontier.Enqueue(neighbor, (cost + Vector2.Distance(nextPoint, target), neighbor));
                }
        }
        return start;
    }

    internal static void Validate(Vector2 point, string name)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
            throw new ArgumentOutOfRangeException(name, "Coordinates must be finite.");
    }

    private static void ValidateRadius(float radius)
    {
        if (!float.IsFinite(radius) || radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be finite and nonnegative.");
    }

    private static bool Overlaps(Vector2 point, float radius, WorldRect rect)
    {
        if (point.X > rect.Left && point.X < rect.Right &&
            point.Y > rect.Top && point.Y < rect.Bottom)
            return true;
        double dx = point.X - Math.Clamp((double)point.X, rect.Left, rect.Right);
        double dy = point.Y - Math.Clamp((double)point.Y, rect.Top, rect.Bottom);
        return dx * dx + dy * dy < (double)radius * radius - 0.00001;
    }

    private bool ClearSegment(Vector2 start, Vector2 end, float radius) =>
        CanOccupy(start, radius) && CanOccupy(end, radius) &&
        FirstHit(start, (double)end.X - start.X, (double)end.Y - start.Y, radius).Time >= 1;

    private void PrepareGrid(float radius)
    {
        if (_gridRadius == radius && _gridGateOpen == BossGateOpen)
            return;
        for (var i = 0; i < _walkable.Length; i++)
            _walkable[i] = CanOccupy(GridPoint(i), radius);
        _gridRadius = radius;
        _gridGateOpen = BossGateOpen;
    }

    private Vector2 GridPoint(int index) =>
        new(index % _columns * CellSize + CellSize / 2, index / _columns * CellSize + CellSize / 2);

    private static Vector2 PointAlong(Vector2 start, double dx, double dy, double time) =>
        new((float)(start.X + dx * time), (float)(start.Y + dy * time));

    private readonly record struct Hit(double Time, double NormalX, double NormalY);

    private Hit FirstHit(Vector2 start, double dx, double dy, float radius)
    {
        var hit = new Hit(double.PositiveInfinity, 0, 0);
        if (dx < 0) Consider(ref hit, (radius - start.X) / dx, 1, 0);
        if (dx > 0) Consider(ref hit, (Width - (double)radius - start.X) / dx, -1, 0);
        if (dy < 0) Consider(ref hit, (radius - start.Y) / dy, 0, 1);
        if (dy > 0) Consider(ref hit, (Height - (double)radius - start.Y) / dy, 0, -1);
        foreach (var wall in _walls)
            SweepRect(ref hit, start, dx, dy, radius, wall);
        foreach (var obstacle in _obstacles)
            SweepRect(ref hit, start, dx, dy, radius, obstacle);
        if (IsDungeon && !BossGateOpen)
            SweepRect(ref hit, start, dx, dy, radius, BossGate);
        return hit;
    }

    private static void Consider(ref Hit hit, double time, double nx, double ny)
    {
        if (time >= -1e-10 && time <= 1 && time < hit.Time)
            hit = new Hit(Math.Max(0, time), nx, ny);
    }

    private static void SweepRect(ref Hit hit, Vector2 start, double dx, double dy, float radius, WorldRect rect)
    {
        // A swept circle sees a rectangle with straight expanded sides and rounded corners.
        SweepBox(ref hit, start, dx, dy, rect.Left - (double)radius, rect.Top,
            rect.Right + (double)radius, rect.Bottom);
        SweepBox(ref hit, start, dx, dy, rect.Left, rect.Top - (double)radius,
            rect.Right, rect.Bottom + (double)radius);
        if (radius == 0)
            return;
        SweepCorner(ref hit, start, dx, dy, radius, rect.Left, rect.Top);
        SweepCorner(ref hit, start, dx, dy, radius, rect.Right, rect.Top);
        SweepCorner(ref hit, start, dx, dy, radius, rect.Left, rect.Bottom);
        SweepCorner(ref hit, start, dx, dy, radius, rect.Right, rect.Bottom);
    }

    private static void SweepBox(ref Hit hit, Vector2 start, double dx, double dy,
        double left, double top, double right, double bottom)
    {
        double entry = double.NegativeInfinity, exit = double.PositiveInfinity, nx = 0, ny = 0;
        if (!Slab(start.X, dx, left, right, ref entry, ref exit, ref nx, ref ny, true) ||
            !Slab(start.Y, dy, top, bottom, ref entry, ref exit, ref nx, ref ny, false) ||
            entry > exit || exit < 0 || dx * nx + dy * ny >= 0)
            return;
        Consider(ref hit, entry, nx, ny);
    }

    private static bool Slab(double position, double delta, double minimum, double maximum,
        ref double entry, ref double exit, ref double nx, ref double ny, bool horizontal)
    {
        if (delta == 0)
            return position > minimum && position < maximum;
        var near = (minimum - position) / delta;
        var far = (maximum - position) / delta;
        var normal = -1.0;
        if (near > far)
        {
            (near, far) = (far, near);
            normal = 1;
        }
        if (near > entry)
        {
            entry = near;
            nx = horizontal ? normal : 0;
            ny = horizontal ? 0 : normal;
        }
        exit = Math.Min(exit, far);
        return entry <= exit;
    }

    private static void SweepCorner(ref Hit hit, Vector2 start, double dx, double dy,
        float radius, double x, double y)
    {
        var a = dx * dx + dy * dy;
        if (a == 0)
            return;
        var sx = start.X - x;
        var sy = start.Y - y;
        var b = sx * dx + sy * dy;
        var c = sx * sx + sy * sy - (double)radius * radius;
        var discriminant = b * b - a * c;
        if (discriminant <= 0)
            return;
        var time = (-b - Math.Sqrt(discriminant)) / a;
        var nx = (sx + dx * time) / radius;
        var ny = (sy + dy * time) / radius;
        if (dx * nx + dy * ny < 0)
            Consider(ref hit, time, nx, ny);
    }
}
