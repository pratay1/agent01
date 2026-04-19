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
        foreach (var body in _bodies.ToList())
        {
            // Handle GravityWell - attracts nearby objects
            if (body.BodyType == BodyType.GravityWell)
            {
                const float attractRadius = 200f;
                const float attractStrength = 8000f;
                
                foreach (var other in _bodies)
                {
                    if (body != other && !other.IsStatic)
                    {
                        Vector2 direction = body.Position - other.Position;
                        float distance = (float)direction.Length;
                        
                        if (distance > 0 && distance < attractRadius)
                        {
                            float forceMagnitude = attractStrength * (1 - distance / attractRadius) / (distance * distance);
                            Vector2 force = direction.Normalized * forceMagnitude;
                            other.ApplyForce(force);
                        }
                    }
                }
            }

            // Handle AntiGravity - floats upward
            if (body.BodyType == BodyType.AntiGravity)
            {
                body.ApplyForce(new Vector2(0, -1500));
            }

            // Handle Turbo - accelerates in velocity direction
            if (body.BodyType == BodyType.Turbo)
            {
                if (body.Velocity.Length > 1)
                {
                    Vector2 boost = body.Velocity.Normalized * 500;
                    body.ApplyForce(boost);
                }
            }

            // Handle BlackHole - sucks in everything and grows
            if (body.BodyType == BodyType.BlackHole)
            {
                const float suckRadius = 300f;
                const float suckStrength = 12000f;
                
                foreach (var other in _bodies)
                {
                    if (body != other && !other.IsStatic)
                    {
                        Vector2 direction = body.Position - other.Position;
                        float distance = (float)direction.Length;
                        
                        if (distance > 0 && distance < suckRadius)
                        {
                            float forceMagnitude = suckStrength * (1 - distance / suckRadius) / (distance * distance);
                            Vector2 force = direction.Normalized * forceMagnitude;
                            other.ApplyForce(force);
                            
                            // Also shrink the other body
                            if (distance < body.Radius + other.Radius + 20)
                            {
                                other.Radius = System.Math.Max(5, other.Radius * 0.999);
                            }
                        }
                    }
                }
                
                // Black hole slowly grows
                body.Radius = System.Math.Min(100, body.Radius * 1.001);
            }

            // Handle Lightning - chains force to nearby objects
            if (body.BodyType == BodyType.Lightning)
            {
                const float chainRadius = 180f;
                const float chainStrength = 6000f;
                
                foreach (var other in _bodies)
                {
                    if (body != other && !other.IsStatic)
                    {
                        Vector2 direction = other.Position - body.Position;
                        float distance = (float)direction.Length;
                        
                        if (distance > 0 && distance < chainRadius)
                        {
                            float forceMagnitude = chainStrength * (1 - distance / chainRadius);
                            Vector2 force = direction.Normalized * forceMagnitude;
                            other.ApplyForce(force);
                        }
                    }
                }
            }

            // Handle Spike - bounces violently off everything
            if (body.BodyType == BodyType.Spike)
            {
                foreach (var other in _bodies)
                {
                    if (body != other && !other.IsStatic)
                    {
                        float dist = (float)Vector2.Distance(body.Position, other.Position);
                        if (dist < body.Radius + other.Radius)
                        {
                            Vector2 normal = (body.Position - other.Position).Normalized;
                            float bounceForce = 3000f;
                            body.ApplyImpulse(normal * bounceForce / body.Mass);
                            other.ApplyImpulse(-normal * bounceForce / other.Mass);
                        }
                    }
                }
            }

            // Handle Glue - sticks to anything
            if (body.BodyType == BodyType.Glue)
            {
                foreach (var other in _bodies)
                {
                    if (body != other && !other.IsStatic)
                    {
                        float dist = (float)Vector2.Distance(body.Position, other.Position);
                        if (dist < body.Radius + other.Radius + 10)
                        {
                            // Stick together - reduce velocity
                            other.Velocity = other.Velocity * 0.9;
                            body.Velocity = body.Velocity * 0.9;
                        }
                    }
                }
            }

            // Handle Phantom - passes through but affects
            if (body.BodyType == BodyType.Phantom)
            {
                foreach (var other in _bodies)
                {
                    if (body != other && !other.IsStatic)
                    {
                        float dist = (float)Vector2.Distance(body.Position, other.Position);
                        if (dist < body.Radius + other.Radius)
                        {
                            // Push other body away gently
                            Vector2 direction = (other.Position - body.Position).Normalized;
                            other.ApplyForce(direction * 500);
                        }
                    }
                }
            }

            // Handle Plasma - electric chains to nearby
            if (body.BodyType == BodyType.Plasma)
            {
                const float plasmaRadius = 150f;
                const float plasmaStrength = 4000f;
                
                foreach (var other in _bodies)
                {
                    if (body != other && !other.IsStatic)
                    {
                        Vector2 direction = other.Position - body.Position;
                        float distance = (float)direction.Length;
                        
                        if (distance > 0 && distance < plasmaRadius)
                        {
                            float forceMagnitude = plasmaStrength * (1 - distance / plasmaRadius);
                            Vector2 force = direction.Normalized * forceMagnitude;
                            other.ApplyForce(force);
                            
                            // Also apply perpendicular force for zigzag effect
                            Vector2 perp = new Vector2(-direction.Y, direction.X);
                            other.ApplyForce(perp * forceMagnitude * 0.3);
                        }
                    }
                }
            }

            // Handle Repulsor - push away everything
            if (body.BodyType == BodyType.Repulsor)
            {
                const float repulsionRadius = 200f;
                const float repulsionStrength = 8000f;
                
                foreach (var other in _bodies)
                {
                    if (body != other && !other.IsStatic)
                    {
                        Vector2 direction = other.Position - body.Position;
                        float distance = (float)direction.Length;
                        
                        if (distance > 0 && distance < repulsionRadius)
                        {
                            float forceMagnitude = repulsionStrength * (1 - distance / repulsionRadius);
                            Vector2 force = direction.Normalized * forceMagnitude;
                            other.ApplyForce(force);
                        }
                    }
                }
            }

            // Handle Explosive - BOOM on contact
            if (body.BodyType == BodyType.Explosive && !body.HasExploded)
            {
                foreach (var other in _bodies)
                {
                    if (body != other && 
                        Vector2.Distance(body.Position, other.Position) < body.Radius + other.Radius)
                    {
                        body.HasExploded = true;
                        ForceManager.Explosion.Trigger(body.Position);
                        
                        Random rand = new Random();
                        for (int i = 0; i < 12; i++)
                        {
                            float angle = i * (float)System.Math.PI * 2 / 12;
                            float speed = 300f + rand.Next(200);
                            float vx = (float)System.Math.Cos(angle) * speed;
                            float vy = (float)System.Math.Sin(angle) * speed;
                            
                            var debris = CreateBody(
                                body.Position,
                                body.Radius * 0.25,
                                body.Mass * 0.15,
                                0.5);
                            debris.Velocity = new Vector2(vx, vy);
                            debris.BodyType = BodyType.Fire;
                        }
                        
                        // Massive explosion force
                        foreach (var other2 in _bodies)
                        {
                            if (body != other2)
                            {
                                Vector2 dir = (other2.Position - body.Position).Normalized;
                                float dist = (float)Vector2.Distance(body.Position, other2.Position);
                                float force = 15000f / (dist * dist + 1);
                                other2.ApplyImpulse(dir * force * 50);
                            }
                        }
                        
                        RemoveBody(body);
                        break;
                    }
                }
            }

            // Handle Freezer - slows down anything it touches
            if (body.BodyType == BodyType.Freezer)
            {
                foreach (var other in _bodies)
                {
                    if (body != other && !other.IsStatic)
                    {
                        float dist = (float)Vector2.Distance(body.Position, other.Position);
                        if (dist < body.Radius + other.Radius + 30)
                        {
                            other.Velocity = other.Velocity * 0.92;
                        }
                    }
                }
            }

            // Handle Fire (from explosions) - rises up
            if (body.BodyType == BodyType.Fire)
            {
                body.ApplyForce(new Vector2(0, -300));
                body.LifeTime += 0.016;
                if (body.LifeTime > 3)
                {
                    RemoveBody(body);
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