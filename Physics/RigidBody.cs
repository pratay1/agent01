using PhysicsSandbox.Math;

namespace PhysicsSandbox.Physics;

public enum BodyType
{
    Normal,
    Bouncy,
    Heavy,
    Explosive,
    Repulsor
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
    public double Mass { get; set; }
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

    public bool IsStatic => Mass == 0 || InverseMass == 0;

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
        HasExploded = false;

        InverseMass = mass > 0 ? 1.0 / mass : 0;
    }

    public void ApplyImpulse(Vector2 impulse)
    {
        if (!IsStatic)
        {
            _velocity = _velocity + impulse * InverseMass;
        }
    }

    public void ApplyForce(Vector2 force)
    {
        if (InverseMass > 0)
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
        if (IsStatic) return;

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