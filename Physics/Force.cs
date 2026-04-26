using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Physics;

public interface IForce
{
    Vector2 GetForce(RigidBody body);
}

public class GravityForce : IForce
{
    public Vector2 Direction { get; set; } = Vector2.Down;
    public double Strength { get; set; } = 980;

    public Vector2 GetForce(RigidBody body)
    {
        if (body.IsStatic) return Vector2.Zero;
        return Direction * Strength * body.Mass;
    }
}

public class ExplosionForce : IForce
{
    public Vector2 Origin { get; set; }
    public double Radius { get; set; } = 200;
    public double Strength { get; set; } = 50000;
    public bool IsActive { get; private set; }

    public void Trigger(Vector2 pos)
    {
        Origin = pos;
        IsActive = true;
    }

    public void Clear() => IsActive = false;

    public Vector2 GetForce(RigidBody body)
    {
        if (!IsActive || body.IsStatic) return Vector2.Zero;
        Vector2 dir = body.Position - Origin;
        double dist = dir.Length;
        if (dist > Radius || dist < 1) return Vector2.Zero;
        double falloff = 1 - dist / Radius;
        return dir.Normalized * Strength * falloff * falloff;
    }
}

public class WindForce : IForce
{
    public Vector2 Direction { get; set; } = Vector2.Right;
    public double Strength { get; set; } = 100;
    public bool IsActive { get; set; }

    public Vector2 GetForce(RigidBody body)
    {
        if (!IsActive || body.IsStatic) return Vector2.Zero;
        return Direction * Strength * body.Mass;
    }
}

public class ForceManager
{
    public GravityForce Gravity { get; } = new();
    public ExplosionForce Explosion { get; } = new();
    public WindForce Wind { get; } = new();

    public Vector2 GetTotalForce(RigidBody body)
    {
        return Gravity.GetForce(body) + Explosion.GetForce(body) + Wind.GetForce(body);
    }

    public void ClearExplosion() => Explosion.Clear();
}
