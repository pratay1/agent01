using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Behaviors;

namespace PhysicsSandbox.Physics;

public enum CollisionLayer
{
    None = 0,
    Default = 1 << 0,
    Particle = 1 << 1,
}

public enum BodyType
{
    None = 0,
    Normal = 1,
    Bouncy = 2,
    Heavy = 3,
    Explosive = 4,
    Repulsor = 5,
    GravityWell = 6,
    Turbo = 7,
    Phantom = 8,
    Spike = 9,
    Glue = 10,
    Plasma = 11,
    BlackHole = 12,
    Lightning = 13,
    Fire = 14,
    Angel = 15,
    Molly = 16,
}

public class RigidBody
{
    private static int _nextId = 0;

    public int Id { get; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public Vector2 Acceleration { get; set; }
    public double AngularVelocity { get; set; }
    public double Angle { get; set; }
    public double Radius { get; set; }
    public double Mass { get; set; }
    public double InverseMass { get; private set; }
    public double Restitution { get; set; }
    public BodyType BodyType { get; set; }
    public CollisionLayer CollisionLayer { get; set; } = CollisionLayer.Default;
    public int CollisionMask { get; set; } = (int)CollisionLayer.Default | (int)CollisionLayer.Particle;
    public bool HasExploded { get; set; }
    public bool IsFrozen { get; set; }
    public bool IsStuck { get; set; }
    public double LifeTime { get; set; }
    public double FlyTimer { get; set; }
    public int? LatchedPartnerId { get; set; }
    public PhysicsWorld? World { get; set; }
    public BodyBehavior? Behavior { get; set; }

    public bool IsStatic => Mass <= 0;

    public RigidBody(Vector2 position, double radius, double mass, double restitution, BodyType bodyType)
    {
        Id = _nextId++;
        Position = position;
        Radius = radius;
        Mass = mass;
        Restitution = System.Math.Clamp(restitution, 0, 1);
        BodyType = bodyType;
        InverseMass = Mass > 0 ? 1.0 / Mass : 0;
    }

    public void ApplyForce(Vector2 force)
    {
        if (!IsStatic && !IsFrozen)
            Acceleration += force * InverseMass;
    }

    public void ApplyImpulse(Vector2 impulse)
    {
        if (!IsStatic && !IsFrozen)
            Velocity += impulse * InverseMass;
    }

    public void ClearForces() => Acceleration = Vector2.Zero;

    public void Integrate(double dt)
    {
        if (IsStatic || IsFrozen) return;
        Velocity += Acceleration * dt;
        Position += Velocity * dt;
        Angle += AngularVelocity * dt;
        Acceleration = Vector2.Zero;
    }

    public bool IsValid()
    {
        return !double.IsNaN(Position.X) && !double.IsNaN(Position.Y) &&
               !double.IsNaN(Velocity.X) && !double.IsNaN(Velocity.Y) &&
               !double.IsInfinity(Position.X) && !double.IsInfinity(Position.Y);
    }
}

public static class BodyTypeExtensions
{
    public static string GetDescription(this BodyType type) => type switch
    {
        BodyType.None => "Unknown",
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
        BodyType.None => "#4FC3F7",
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

