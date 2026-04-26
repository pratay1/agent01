using System.Diagnostics.CodeAnalysis;
using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using PhysicsSandbox.Behaviors;

namespace PhysicsSandbox.Physics;

public class PhysicsWorld
{
    private readonly List<RigidBody> _bodies = new();
    private readonly Dictionary<int, RigidBody> _bodyMap = new();
    private readonly ForceManager _forces = new();
    private readonly SpatialHash _spatialHash = new(100);

    private Vector2 _gravity = Vector2.Down * 980;
    private double _damping = 0.01;
    private bool _isPaused;

    public double GroundY { get; set; } = 600;
    public double LeftBoundary { get; set; } = 240;  // Sidebar is 240px wide
    public double RightBoundary { get; set; } = 1280;
    public double TopBoundary { get; set; } = 0;
    public double TimeScale { get; set; } = 1.0;

    public IReadOnlyList<RigidBody> Bodies => _bodies;
    public ForceManager ForceManager => _forces;
    public Vector2 Gravity { get => _gravity; set => _gravity = value; }
    public bool IsPaused { get => _isPaused; set => _isPaused = value; }

    public RigidBody CreateBody(Vector2 position, double radius, double mass, double restitution, BodyType bodyType = BodyType.Normal)
    {
        var behavior = BodyBehaviorFactory.Get(bodyType);
        var body = new RigidBody(position, radius, mass, restitution, bodyType)
        {
            Behavior = behavior
        };
        body.World = this;
        behavior.OnCreate(body);
        _bodies.Add(body);
        _bodyMap[body.Id] = body;
        return body;
    }

    public void RemoveBody(RigidBody body)
    {
        if (_bodies.Remove(body))
            _bodyMap.Remove(body.Id);
    }

    public void Clear()
    {
        _bodies.Clear();
        _bodyMap.Clear();
        _spatialHash.Clear();
        _forces.ClearExplosion();
    }

    public void ToggleGravityDirection()
    {
        _gravity = -_gravity;
        _forces.Gravity.Direction = _gravity.Normalized;
    }

    public void SetBoundaries(double left, double right, double ground)
    {
        LeftBoundary = left;
        RightBoundary = right;
        GroundY = ground;
    }

    public void SetCanvasSize(double width, double height)
    {
        RightBoundary = width;
        GroundY = height;
        LeftBoundary = 240;  // Sidebar is 240px wide
    }

     public bool TryGetBodyById(int id, out RigidBody? body) => _bodyMap.TryGetValue(id, out body);

    public void Step(double dt)
    {
        if (_isPaused || _bodies.Count == 0) return;

        dt = dt * TimeScale;
        if (dt > 0.05) dt = 0.05;
        if (dt < 0.001) return;

        ApplyForces(dt);
        UpdateBehaviors(dt);
        Integrate(dt);
        ResolveBoundaries();
        ResolveCollisions();
    }

    private void ApplyForces(double dt)
    {
        _forces.Gravity.Direction = _gravity.Normalized;
        _forces.Gravity.Strength = _gravity.Length;

        for (int i = 0; i < _bodies.Count; i++)
        {
            var body = _bodies[i];
            if (!body.IsStatic)
            {
                body.ApplyForce(_forces.GetTotalForce(body));
                body.ApplyForce(-body.Velocity * _damping * body.Mass);
            }
        }
    }

    private void UpdateBehaviors(double dt)
    {
        for (int i = 0; i < _bodies.Count; i++)
        {
            var body = _bodies[i];
            body.Behavior?.OnUpdate(body, dt, this);
        }
    }

    private void Integrate(double dt)
    {
        for (int i = 0; i < _bodies.Count; i++)
        {
            var body = _bodies[i];
            if (!body.IsStatic)
                body.Integrate(dt);
        }
    }

    private void ResolveCollisions()
    {
        Collision.ResolveAll(_bodies, _spatialHash);
    }

    private void ResolveBoundaries()
    {
        double minX = LeftBoundary;
        double maxX = RightBoundary;
        double maxY = GroundY;
        double minY = TopBoundary;

        for (int i = 0; i < _bodies.Count; i++)
        {
            var b = _bodies[i];
            if (b.IsStatic) continue;

            double x = b.Position.X;
            double y = b.Position.Y;
            double r = b.Radius;
            double vx = b.Velocity.X;
            double vy = b.Velocity.Y;

            // Left boundary - sidebar barrier
            if (x - r < minX)
            {
                b.Position = new Vector2(minX + r, y);
                if (vx < 0)
                    b.Velocity = new Vector2(-vx * b.Restitution, vy);
            }
            // Right boundary
            else if (x + r > maxX)
            {
                b.Position = new Vector2(maxX - r, y);
                if (vx > 0)
                    b.Velocity = new Vector2(-vx * b.Restitution, vy);
            }

            // Top boundary
            if (y - r < minY)
            {
                b.Position = new Vector2(x, minY + r);
                if (vy < 0)
                    b.Velocity = new Vector2(vx, -vy * b.Restitution);
            }

            // Bottom boundary (ground)
            if (y + r > maxY)
            {
                b.Position = new Vector2(x, maxY - r);
                if (vy > 0)
                    b.Velocity = new Vector2(vx, -vy * b.Restitution);
            }
        }
    }

    public void SpawnAtPosition(Vector2 position, double radius, double mass, double restitution)
    {
        radius = System.Math.Max(radius, 20);
        mass = System.Math.Max(mass, 0.1);

        foreach (var existing in _bodies)
        {
            if (Vector2.Distance(position, existing.Position) < radius + existing.Radius)
                return;
        }

        CreateBody(position, radius, mass, restitution);
    }
}
