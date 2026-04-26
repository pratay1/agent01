using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Behaviors;

namespace PhysicsSandbox.Physics;

[Flags]
public enum CollisionLayer
{
    None    = 0,
    Default = 1 << 0,  // 1 - regular bodies
    Particle   = 1 << 1,  // 2 - temporary particles/debris
    Ghost  = 1 << 2,   // 4 - phantom-like bodies that skip detailed collision
}

public enum BodyType
{
    None = 0,
    Normal,
    Bouncy,
    Heavy,
    Explosive,
    Repulsor,
    GravityWell,
    Turbo,
    Phantom,
    Spike,
    Glue,
    Plasma,
    BlackHole,
    Lightning,
    Fire,
    Angel,
    Molly
}

public class RigidBody
{
    private Vector2 _position;
    private Vector2 _velocity;
    private Vector2 _acceleration;
    private double _angularVelocity;
    private double _angle;

    public int Id { get; }
    public Vector2 Position
    {
        get => _position;
        set => _position = value;
    }
    public Vector2 Velocity
    {
        get => _velocity;
        set => _velocity = value;
    }
    public Vector2 Acceleration
    {
        get => _acceleration;
        set => _acceleration = value;
    }
    public double Radius { get; set; }
    public CollisionLayer CollisionLayer { get; set; }
    public int CollisionMask { get; set; }
    private double _mass;
    public double Mass
    {
        get => _mass;
        set
        {
            _mass = value;
            InverseMass = value > 0 ? 1.0 / value : 0;
        }
    }
    public double InverseMass { get; private set; }
    public double Restitution { get; set; }
    public double Angle
    {
        get => _angle;
        set => _angle = value;
    }
    public double AngularVelocity
    {
        get => _angularVelocity;
        set => _angularVelocity = value;
    }
    public BodyType BodyType { get; set; }
    public bool HasExploded { get; set; }
    public bool IsFrozen { get; set; }
    public bool IsStuck { get; set; }
    public double LifeTime { get; set; }
    public double FlyTimer { get; set; }
    public double FlyInterval { get; set; } = 2.0;
    public bool IsFlying { get; set; }
    public int? LatchedPartnerId { get; set; }

    public bool IsStatic => Mass == 0 || InverseMass == 0;

    // Additional properties needed by behaviors
    public BodyBehavior? Behavior { get; set; }
    public PhysicsWorld? World { get; set; }
    public double Lifetime
    {
        get => LifeTime;
        set => LifeTime = value;
    }
    public double Rotation
    {
        get => Angle;
        set => Angle = value;
    }

    private static int _nextId = 0;

    public static void ResetIdCounter() => _nextId = 0;

    public RigidBody(Vector2 position, double radius, double mass = 1.0, double restitution = 0.5)
        : this(position, radius, mass, restitution, BodyType.Normal)
    {
    }

    public RigidBody(Vector2 position, double radius, double mass, double restitution, BodyType bodyType)
    {
        Id = _nextId++;
        _position = position;
        _velocity = Vector2.Zero;
        _acceleration = Vector2.Zero;
        _angularVelocity = 0;
        _angle = 0;

        Radius = radius;
        Mass = mass;
        Restitution = System.Math.Clamp(restitution, 0, 1);
        BodyType = bodyType;
        CollisionLayer = CollisionLayer.Default;
        CollisionMask = (int)CollisionLayer.Default | (int)CollisionLayer.Particle;
        HasExploded = false;
        IsFrozen = false;
        IsStuck = false;
        LifeTime = 0;

        InverseMass = mass <= 0 ? 0 : 1.0 / mass;
    }

    public void ApplyImpulse(Vector2 impulse)
    {
        if (!IsStatic && !IsFrozen)
        {
            _velocity = _velocity + impulse * InverseMass;
        }
    }

    public void ApplyForce(Vector2 force)
    {
        if (InverseMass > 0 && !IsFrozen)
        {
            _acceleration = _acceleration + force * InverseMass;
        }
    }

    public void ClearForces()
    {
        _acceleration = Vector2.Zero;
    }

    public void Integrate(double dt)
    {
        if (IsStatic || IsFrozen) return;

        _velocity = _velocity + _acceleration * dt;
        _position = _position + _velocity * dt;
        _angle += _angularVelocity * dt;

        double maxSpeed = 10000;
        double speed = _velocity.Length;
        if (speed > maxSpeed)
        {
            _velocity = _velocity.Normalized * maxSpeed;
        }
    }

    public bool IsValid()
    {
        return !double.IsNaN(_position.X) && !double.IsNaN(_position.Y) &&
               !double.IsNaN(_velocity.X) && !double.IsNaN(_velocity.Y) &&
               !double.IsInfinity(_position.X) && !double.IsInfinity(_position.Y);
    }
}

public static class BodyTypeExtensions
{
    public static string GetDescription(this BodyType type) => type switch
    {
        BodyType.Normal => "Standard physics body",
        BodyType.Bouncy => "Super bouncy - bounces like crazy!",
        BodyType.Heavy => "Massive weight - pushes through everything",
        BodyType.Explosive => "BOOM! Explodes on contact with anything",
        BodyType.Repulsor => "Pushes away everything nearby with huge force!",
        BodyType.GravityWell => "Attracts nearby objects like a black hole",
        BodyType.Turbo => "Accelerates continuously - goes supersonic!",
        BodyType.Phantom => "Phases through bodies & violently shakes them!",
        BodyType.Spike => "Violent bounce that explodes on contact, spawning debris and applying radial force",
        BodyType.Glue => "Sticks to anything it touches",
        BodyType.Plasma => "Electric zigzag chains to nearby!",
        BodyType.BlackHole => "Sucks in everything & grows!",
        BodyType.Lightning => "Zaps nearby objects with electric force!",
        BodyType.Fire => "Rising flames - disappears after 3 seconds",
        BodyType.Angel => "Flies periodically - gentle & light",
        BodyType.Molly => "Explodes on contact unless Angel is nearby - attracts to Angel and latches",
        _ => "Unknown body type"
    };

    public static string GetColorHex(this BodyType type) => type switch
    {
        BodyType.Normal => "#4FC3F7",
        BodyType.Bouncy => "#81C784",
        BodyType.Heavy => "#FFB74D",
        BodyType.Explosive => "#E53935",
        BodyType.Repulsor => "#BA68C8",
        BodyType.GravityWell => "#4DB6AC",
        BodyType.Turbo => "#FFEB3B",
        BodyType.Phantom => "#9575CD",
        BodyType.Spike => "#F44336",
        BodyType.Glue => "#AED581",
        BodyType.Plasma => "#E91E63",
        BodyType.BlackHole => "#0D0D0D",
        BodyType.Lightning => "#FF9800",
        BodyType.Fire => "#FF5722",
        BodyType.Angel => "#FFFFFF",
        BodyType.Molly => "#FF4081",
        _ => "#4FC3F7"
    };
}
