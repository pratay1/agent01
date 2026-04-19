using PhysicsSandbox.Math;

namespace PhysicsSandbox.Physics;

public class PhysicsWorld
{
    private readonly List<RigidBody> _bodies = new();
    private readonly ForceManager _forceManager;
    private Vector2 _gravity = Vector2.Down * 980;
    private double _timeScale = 1.0;
    private double _damping = 0.01;
    private bool _isPaused;
    private double _groundY = 600;
    private double _leftBoundary = 0;
    private double _rightBoundary = 1280;

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
    public ForceManager ForceManager => _forceManager;
    public Vector2 Gravity
    {
        get => _gravity;
        set => _gravity = value;
    }
    public double TimeScale
    {
        get => _timeScale;
        set => _timeScale = System.Math.Clamp(value, 0.1, 2.0);
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

    public RigidBody CreateBody(Vector2 position, double radius, double mass = 1.0, double restitution = 0.6)
    {
        var body = new RigidBody(position, radius, mass, restitution)
        {
            Restitution = System.Math.Clamp(restitution, 0, 1)
        };
        _bodies.Add(body);
        return body;
    }

    public void RemoveBody(RigidBody body)
    {
        _bodies.Remove(body);
    }

    public void Clear()
    {
        _bodies.Clear();
        RigidBody.ResetIdCounter();
    }

    public void ToggleGravityDirection()
    {
        _gravity = -_gravity;
        _forceManager.Gravity.SetDirection(_gravity.Normalized);
    }

    public void Step(double dt)
    {
        if (_isPaused) return;

        double scaledDt = dt * _timeScale;

        ApplyForces(scaledDt);
        Integrate(scaledDt);
        HandleSpecialBodyBehaviors();
        SolveCollisions();
        SolveBoundaryCollisions();
        ValidateBodies();
    }

    private void HandleSpecialBodyBehaviors()
    {
        // Handle explosive bodies - explode on contact with other bodies
        foreach (var body in _bodies.ToList())
        {
            if (body.BodyType == BodyType.Explosive && !body.HasExploded)
            {
                // Check if explosive body is touching any other body
                foreach (var other in _bodies)
                {
                    if (body != other && 
                        Vector2.Distance(body.Position, other.Position) < body.Radius + other.Radius)
                    {
                        // Trigger explosion
                        body.HasExploded = true;
                        ForceManager.Explosion.Trigger(body.Position);
                        
                        // Create visual effect by spawning smaller bodies
                        Random rand = new Random();
                        for (int i = 0; i < 8; i++)
                        {
                        float angle = i * (float)System.Math.PI * 2 / 8;
                        float speed = 200f + rand.Next(100);
                        float vx = (float)System.Math.Cos(angle) * speed;
                        float vy = (float)System.Math.Sin(angle) * speed;
                        
                        var debris = CreateBody(
                            body.Position,
                            (float)(body.Radius * 0.3),
                            (float)(body.Mass * 0.2),
                            0.4f);
                            
                            debris.Velocity = new Vector2(vx, vy);
                            debris.BodyType = BodyType.Normal;
                        }
                        
                        // Remove the explosive body after a short delay
                        // For simplicity, we'll remove it immediately
                        RemoveBody(body);
                        break;
                    }
                }
            }
        }
        
        // Handle repulsor bodies - repel nearby bodies
        foreach (var body in _bodies)
        {
            if (body.BodyType == BodyType.Repulsor)
            {
                const float repulsionRadius = 150f;
                const float repulsionStrength = 5000f;
                
                foreach (var other in _bodies)
                 {
                     if (body != other && !other.IsStatic)
                     {
                         Vector2 direction = other.Position - body.Position;
                         float distance = (float)direction.Length;
                         
                         if (distance > 0 && distance < repulsionRadius)
                         {
                             // Calculate repulsion force (stronger when closer)
                             float forceMagnitude = repulsionStrength * (1 - distance / repulsionRadius) / (distance * distance);
                             Vector2 force = direction.Normalized * forceMagnitude;
                             
                             other.ApplyForce(force);
                         }
                     }
                 }
            }
        }
    }

    public void SetBoundaries(double left, double right, double ground)
    {
        _leftBoundary = left;
        _rightBoundary = right;
        _groundY = ground;
    }

    private void SolveBoundaryCollisions()
    {
        foreach (var body in _bodies)
        {
            if (body.IsStatic) continue;

            double bottom = _groundY - body.Radius;
            if (body.Position.Y > bottom)
            {
                body.Position = new Vector2(body.Position.X, bottom);
                if (body.Velocity.Y > 0)
                {
                    body.Velocity = new Vector2(body.Velocity.X * 0.8, -body.Velocity.Y * body.Restitution);
                }
            }

            double left = _leftBoundary + body.Radius;
            if (body.Position.X < left)
            {
                body.Position = new Vector2(left, body.Position.Y);
                if (body.Velocity.X < 0)
                {
                    body.Velocity = new Vector2(-body.Velocity.X * body.Restitution, body.Velocity.Y);
                }
            }

            double right = _rightBoundary - body.Radius;
            if (body.Position.X > right)
            {
                body.Position = new Vector2(right, body.Position.Y);
                if (body.Velocity.X > 0)
                {
                    body.Velocity = new Vector2(-body.Velocity.X * body.Restitution, body.Velocity.Y);
                }
            }

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

            if (_damping > 0 && !body.IsStatic)
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
        foreach (var body in _bodies.ToList())
        {
            if (!body.IsValid())
            {
                body.Velocity = Vector2.Zero;
                body.Acceleration = Vector2.Zero;
            }
        }
    }

    public void SpawnAtPosition(Vector2 position, double radius, double mass, double restitution)
    {
        if (radius < 1) radius = 20;
        if (mass < 0.1) mass = 1;

        foreach (var existing in _bodies)
        {
            if (Vector2.Distance(position, existing.Position) < radius + existing.Radius)
            {
                return;
            }
        }

        CreateBody(position, radius, mass, restitution);
    }
}