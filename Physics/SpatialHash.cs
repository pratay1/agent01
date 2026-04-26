using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Physics;

public class SpatialHash
{
    private readonly Dictionary<(int, int), List<RigidBody>> _cells = new();
    private readonly double _cellSize;

    public SpatialHash(double cellSize = 100.0)
    {
        _cellSize = cellSize;
    }

    public void Clear() => _cells.Clear();

    public void Insert(RigidBody body)
    {
        int cx = (int)(body.Position.X / _cellSize);
        int cy = (int)(body.Position.Y / _cellSize);
        var key = (cx, cy);
        if (!_cells.TryGetValue(key, out var list))
        {
            list = new List<RigidBody>();
            _cells[key] = list;
        }
        list.Add(body);
    }

    public List<RigidBody> Query(RigidBody body)
    {
        var results = new List<RigidBody>();
        int cx = (int)(body.Position.X / _cellSize);
        int cy = (int)(body.Position.Y / _cellSize);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (_cells.TryGetValue((cx + dx, cy + dy), out var list))
                    results.AddRange(list);
            }
        }
        return results;
    }
}
