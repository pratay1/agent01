using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System.Collections.Generic;
using System;

namespace PhysicsSandbox.Physics;

public class SpatialHash
{
    private readonly Dictionary<(int, int), List<RigidBody>> _buckets;
    private readonly double _cellSize;

    public SpatialHash(double cellSize = 100.0)
    {
        _cellSize = cellSize;
        _buckets = new Dictionary<(int, int), List<RigidBody>>();
    }

    public SpatialHash(List<RigidBody> bodies, double cellSize = 100.0)
    {
        _cellSize = cellSize > 0 ? cellSize : 100.0;
        _buckets = new Dictionary<(int, int), List<RigidBody>>();
        foreach (var body in bodies)
        {
            Insert(body);
        }
    }

    public void Insert(RigidBody body)
    {
        var (cellX, cellY) = GetCellCoords(body.Position);
        var key = (cellX, cellY);

        if (!_buckets.TryGetValue(key, out var bucket))
        {
            bucket = new List<RigidBody>();
            _buckets[key] = bucket;
        }

        bucket.Add(body);
    }

    public List<RigidBody> Query(Vector2 position, double radius)
    {
        var result = new List<RigidBody>();
        var (centerCellX, centerCellY) = GetCellCoords(position);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                var key = (centerCellX + dx, centerCellY + dy);
                if (_buckets.TryGetValue(key, out var bucket))
                {
                    result.AddRange(bucket);
                }
            }
        }

        return result;
    }

    private (int, int) GetCellCoords(Vector2 position)
    {
        int cellX = (int)Math.Floor(position.X / _cellSize);
        int cellY = (int)Math.Floor(position.Y / _cellSize);
        return (cellX, cellY);
    }

    public void Clear()
    {
        foreach (var bucket in _buckets.Values)
        {
            bucket.Clear();
        }
        _buckets.Clear();
    }

    public int Count
    {
        get
        {
            int count = 0;
            foreach (var bucket in _buckets.Values)
            {
                count += bucket.Count;
            }
            return count;
        }
    }

    public int BucketCount => _buckets.Count;
}
