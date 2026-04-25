using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using PhysicsSandbox.Behaviors;
using System.Diagnostics;

namespace PhysicsSandbox.Physics;

/// <summary>
/// Manages the physics simulation including rigid bodies, forces, collisions, and boundary constraints.
/// Uses fixed timestep integration with interpolation-safe design.
/// </summary>
public class PhysicsWorld
{
    private readonly List<RigidBody> _bodies = new();
    private readonly Dictionary<int, RigidBody> _bodyMap = new();
    private readonly ForceManager _forceManager;
    private Vector2 _gravity = Vector2.Down * 980;
    private double _timeScale = 1.0;
    private double _damping = 0.01;
    private bool _isPaused;
    private double _groundY = 600;
    private double _leftBoundary = 0;
    private double _rightBoundary = 1280;
    private double _topBoundary = 0;
    private int _bodyIdCounter = 0;
    private Stopwatch _physicsStopwatch = new();

    /// <summary>Total number of bodies currently in the simulation.</summary>
    public int Count => _bodies.Count;

    public IReadOnlyList<RigidBody> Bodies => _bodies;
    public double GroundY
    {
        get => _groundY;
        set => _groundY = value;
    }
    public double LeftBoundary
    {
        get => _leftBoundary;
        set => _leftBoundary = value;
    }
    public double RightBoundary
    {
        get => _rightBoundary;
        set => _rightBoundary = value;
    }
    public double TopBoundary
    {
        get => _topBoundary;
        set => _topBoundary = value;
    }
    public Vector2 GravityCenter => new((float)_leftBoundary, (float)_groundY);
    public Vector2 GravityExtents => new((float)(_rightBoundary - _leftBoundary), (float)(_groundY - _topBoundary));

    public ForceManager ForceManager => _forceManager;
    public Vector2 Gravity
    {
        get => _gravity;
        set => _gravity = value;
    }
    public double TimeScale
    {
        get => _timeScale;
        set => _timeScale = Clamp(value, 0.01, 5.0);
    }
    public bool IsPaused
    {
        get => _isPaused;
        set => _isPaused = value;
    }

    public PhysicsWorld()
    {
        _forceManager = new ForceManager();
    }

    /// <summary>Creates a new physics body at the specified position with given properties.</summary>
    public RigidBody CreateBody(Vector2 position, double radius, double mass, double restitution, BodyType bodyType = BodyType.Normal)
    {
        try
        {
            var behavior = BodyBehaviorFactory.Get(bodyType);
            var body = new RigidBody(position, radius, mass, restitution, bodyType);

            behavior.OnCreate(body);
            _bodies.Add(body);
            _bodyMap[body.Id] = body;
            Logger.LogDebug($"Created body {body.Id} (type: {bodyType}, pos: {position}, r: {radius}, m: {mass})");
            return body;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to create body at {position} (type: {bodyType})", ex);
            throw;
        }
    }

    /// <summary>Safely removes a body from the simulation.</summary>
    public void RemoveBody(RigidBody body)
    {
        try
        {
            if (_bodies.Remove(body))
            {
                _bodyMap.Remove(body.Id);
                Logger.LogDebug($"Removed body {body.Id} (type: {body.BodyType})");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to remove body {body.Id}", ex);
        }
    }

    /// <summary>Cleans up all bodies and resets the ID counter.</summary>
    public void Clear()
    {
        _bodies.Clear();
        _bodyMap.Clear();
        RigidBody.ResetIdCounter();
    }

    /// <summary>Reverses the direction of gravity force.</summary>
    public void ToggleGravityDirection()
    {
        _gravity = -_gravity;
        _forceManager.Gravity.SetDirection(_gravity.Normalized);
    }

    /// <summary>Executes one physics simulation step with fixed timestep.</summary>
    public void Step(double dt)
    {
        if (_isPaused) return;

        _physicsStopwatch.Restart();
        double scaledDt = dt * _timeScale;
        scaledDt = Clamp(scaledDt, 0.001, 0.05); // Clamp to [1ms, 50ms]

        try
        {
            // 1. Apply forces (gravity, wind, explosions)
            ApplyForces(scaledDt);

            // 2. Behavior updates - before integration so forces apply this frame
            var bodiesSnapshot = _bodies.ToList();
            foreach (var body in bodiesSnapshot)
            {
                try
                {
                    var behavior = BodyBehaviorFactory.Get(body.BodyType);
                    behavior?.OnUpdate(body, dt, this);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Behavior update failed for body {body.Id} (type: {body.BodyType})", ex);
                }
            }

            // 3. Integrate positions from velocities
            Integrate(scaledDt);

            // 4. Solve collisions (iterative relaxation)
            SolveCollisions();
            SolveBoundaryCollisions();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Physics step failed at dt={dt}, scaledDt={scaledDt}, bodies={_bodies.Count}", ex);
        }

        // 5. Validate body state
        ValidateBodies();

        _physicsStopwatch.Stop();
        if (_physicsStopwatch.ElapsedMilliseconds > 10)
        {
            Logger.LogWarning($"Physics step took {_physicsStopwatch.ElapsedMilliseconds}ms (threshold: 10ms)");
        }
    }

    /// <summary>Sets rectangular boundary for the simulation space.</summary>
    public void SetBoundaries(double left, double right, double ground)
    {
        _leftBoundary = left;
        _rightBoundary = right;
        _groundY = ground;
        _topBoundary = 0;
    }

    /// <summary>Sets rectangular boundary for the simulation space.</summary>
    public void SetBoundaries(double left, double right, double top, double ground)
    {
        _leftBoundary = left;
        _rightBoundary = right;
        _topBoundary = top;
        _groundY = ground;
    }

    /// <summary>Resizes the canvas boundary based on actual control size.</summary>
    public void SetCanvasSize(double width, double height)
    {
        _rightBoundary = width;
        _groundY = height;
    }

    private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);

    private void SolveBoundaryCollisions()
    {
        foreach (var body in _bodies)
        {
            if (body.IsStatic) continue;

            // Bottom (ground)
            double bottom = _groundY - body.Radius;
            if (body.Position.Y > bottom)
            {
                body.Position = new Vector2(body.Position.X, bottom);
                if (body.Velocity.Y > 0)
                {
                    body.Velocity = new Vector2(body.Velocity.X * 0.8, -body.Velocity.Y * body.Restitution);
                }
            }

            // Left
            double left = _leftBoundary + body.Radius;
            if (body.Position.X < left)
            {
                body.Position = new Vector2(left, body.Position.Y);
                if (body.Velocity.X < 0)
                {
                    body.Velocity = new Vector2(-body.Velocity.X * body.Restitution, body.Velocity.Y);
                }
            }

            // Right
            double right = _rightBoundary - body.Radius;
            if (body.Position.X > right)
            {
                body.Position = new Vector2(right, body.Position.Y);
                if (body.Velocity.X > 0)
                {
                    body.Velocity = new Vector2(-body.Velocity.X * body.Restitution, body.Velocity.Y);
                }
            }

            // Top
            double top = body.Radius;
            if (body.Position.Y < top)
            {
                body.Position = new Vector2(body.Position.X, top);
                if (body.Velocity.Y < 0)
                {
                    body.Velocity = new Vector2(body.Velocity.X, -body.Velocity.Y * body.Restitution);
                }
            }
        }
    }

    private void ApplyForces(double dt)
    {
        _forceManager.Gravity.SetDirection(_gravity.Normalized);
        _forceManager.Gravity.Strength = _gravity.Length;

        foreach (var body in _bodies)
        {
            if (body.IsStatic) continue;

            Vector2 force = _forceManager.GetTotalForce(body);
            body.ApplyForce(force);

            if (_damping > 0)
            {
                Vector2 dampingForce = -body.Velocity * _damping * body.Mass;
                body.ApplyForce(dampingForce);
            }
        }
    }

    private void Integrate(double dt)
    {
        foreach (var body in _bodies)
        {
            if (body.IsStatic) continue;
            body.Integrate(dt);
            body.ClearForces();
        }
    }

    private void SolveCollisions()
    {
        const int iterations = 8;

        for (int i = 0; i < iterations; i++)
        {
            var manifolds = Collision.DetectAll(_bodies);
            if (manifolds.Count == 0) break;

            Collision.ResolveAll(manifolds);
        }
    }

    private void ValidateBodies()
    {
        for (int i = 0; i < _bodies.Count; i++)
        {
            var body = _bodies[i];
            if (!body.IsValid())
            {
                Logger.LogWarning($"Removing invalid body {body.Id} (type: {body.BodyType}, pos: {body.Position}, vel: {body.Velocity})");
                _bodies.RemoveAt(i);
                _bodyMap.Remove(body.Id);
                i--;
            }
        }
    }

    /// <summary>Spawns a body at the specified position if the location is not occupied.</summary>
    public void SpawnAtPosition(Vector2 position, double radius, double mass, double restitution)
    {
        radius = Math.Max(radius, 20);
        mass = Math.Max(mass, 0.1);

        foreach (var existing in _bodies)
        {
            if (Vector2.Distance(position, existing.Position) < radius + existing.Radius)
            {
                return;
            }
        }

        CreateBody(position, radius, mass, restitution);
    }

    /// <summary>Attempts to retrieve a body by its unique identifier.</summary>
    public bool TryGetBodyById(int id, out RigidBody? body)
    {
        return _bodyMap.TryGetValue(id, out body);
    }
}