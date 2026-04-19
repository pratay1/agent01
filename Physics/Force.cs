using PhysicsSandbox.Math;

namespace PhysicsSandbox.Physics;

public interface IForce
{
    string Name { get; }
    Vector2 GetForce(RigidBody body);
}

public class GravityForce : IForce
{
    public string Name => "Gravity";
    private Vector2 _direction;
    private double _strength;

    public Vector2 Direction
    {
        get => _direction;
        set => _direction = value;
    }

    public double Strength
    {
        get => _strength;
        set => _strength = value;
    }

    public GravityForce(double strength = 980.0, Vector2? direction = null)
    {
        _strength = strength;
        _direction = direction ?? Vector2.Down;
    }

    public Vector2 GetForce(RigidBody body)
    {
        if (body.IsStatic) return Vector2.Zero;
        return _direction * _strength * body.Mass;
    }

    public void SetDirection(Vector2 direction)
    {
        _direction = direction.Normalized;
    }
}

public class ExplosionForce : IForce
{
    public string Name => "Explosion";
    private Vector2 _origin;
    private double _radius;
    private double _strength;
    private bool _isActive;

    public Vector2 Origin
    {
        get => _origin;
        set => _origin = value;
    }

    public double Radius
    {
        get => _radius;
        set => _radius = value;
    }

    public double Strength
    {
        get => _strength;
        set => _strength = value;
    }

    public bool IsActive => _isActive;

    public ExplosionForce(double radius = 200, double strength = 50000)
    {
        _origin = Vector2.Zero;
        _radius = radius;
        _strength = strength;
        _isActive = false;
    }

    public void Trigger(Vector2 position)
    {
        _origin = position;
        _isActive = true;
    }

    public void Clear()
    {
        _isActive = false;
    }

    public Vector2 GetForce(RigidBody body)
    {
        if (!_isActive || body.IsStatic) return Vector2.Zero;

        Vector2 direction = body.Position - _origin;
        double distance = direction.Length;

        if (distance > _radius || distance < 1) return Vector2.Zero;

        double falloff = 1 - (distance / _radius);
        double forceMagnitude = _strength * falloff * falloff;

        return direction.Normalized * forceMagnitude;
    }
}

public class WindForce : IForce
{
    public string Name => "Wind";
    private Vector2 _direction;
    private double _strength;
    private bool _isActive;

    public Vector2 Direction
    {
        get => _direction;
        set => _direction = value;
    }

    public double Strength
    {
        get => _strength;
        set => _strength = value;
    }

    public bool IsActive
    {
        get => _isActive;
        set => _isActive = value;
    }

    public WindForce(double strength = 100, Vector2? direction = null)
    {
        _direction = direction ?? Vector2.Right;
        _strength = strength;
        _isActive = false;
    }

    public Vector2 GetForce(RigidBody body)
    {
        if (!_isActive || body.IsStatic) return Vector2.Zero;
        return _direction * _strength * body.Mass;
    }
}

public class ForceManager
{
    private readonly List<IForce> _forces = new();

    public IReadOnlyList<IForce> Forces => _forces;

    public GravityForce Gravity { get; }
    public ExplosionForce Explosion { get; }
    public WindForce Wind { get; }

    public ForceManager()
    {
        Gravity = new GravityForce();
        Explosion = new ExplosionForce();
        Wind = new WindForce();

        _forces.Add(Gravity);
        _forces.Add(Explosion);
        _forces.Add(Wind);
    }

    public Vector2 GetTotalForce(RigidBody body)
    {
        Vector2 total = Vector2.Zero;
        foreach (var force in _forces)
        {
            total = total + force.GetForce(body);
        }
        return total;
    }

    public void ClearExplosion()
    {
        Explosion.Clear();
    }
}