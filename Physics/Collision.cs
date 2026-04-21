using PhysicsSandbox.Math;

namespace PhysicsSandbox.Physics;

public class Manifold
{
    public RigidBody BodyA { get; }
    public RigidBody BodyB { get; }
    public Vector2 Normal { get; }
    public double Depth { get; }
    public double Restitution { get; }

    public Manifold(RigidBody a, RigidBody b, Vector2 normal, double depth)
    {
        BodyA = a;
        BodyB = b;
        Normal = normal;
        Depth = depth;
        Restitution = System.Math.Min(a.Restitution, b.Restitution);
    }
}

public static class Collision
{
    public static Manifold? DetectCircleCircle(RigidBody a, RigidBody b)
    {
        Vector2 diff = b.Position - a.Position;
        double distanceSq = diff.LengthSquared;
        double radiusSum = a.Radius + b.Radius;

        if (distanceSq >= radiusSum * radiusSum)
            return null;

        double distance = System.Math.Sqrt(distanceSq);
        if (distance < 0.0001)
        {
            return new Manifold(a, b, Vector2.Right, radiusSum);
        }

        double depth = radiusSum - distance;
        Vector2 normal = diff / distance;

        return new Manifold(a, b, normal, depth);
    }

    public static List<Manifold> DetectAll(List<RigidBody> bodies)
    {
        List<Manifold> manifolds = new();

        for (int i = 0; i < bodies.Count; i++)
        {
            for (int j = i + 1; j < bodies.Count; j++)
            {
                var manifold = DetectCircleCircle(bodies[i], bodies[j]);
                if (manifold != null)
                {
                    manifolds.Add(manifold);
                }
            }
        }

        return manifolds;
    }

public static void Resolve(Manifold manifold)
{
    var (a, b, normal, depth, restitution) = 
        (manifold.BodyA, manifold.BodyB, manifold.Normal, manifold.Depth, manifold.Restitution);

    if (a.IsStatic && b.IsStatic) return;

    // Skip collision resolution for latched pairs (Molly-Angel)
    if (a.LatchedPartnerId == b.Id || b.LatchedPartnerId == a.Id)
        return;

    Vector2 relativeVelocity = b.Velocity - a.Velocity;
    double velocityAlongNormal = Vector2.Dot(relativeVelocity, normal);

    if (velocityAlongNormal > 0) return;

    double e = restitution;
    double invMassA = a.InverseMass;
    double invMassB = b.InverseMass;
    double invMassSum = invMassA + invMassB;

    if (invMassSum < 0.0001) return;

    double j = -(1 + e) * velocityAlongNormal;
    j /= invMassSum;

    Vector2 impulse = normal * j;

    if (!a.IsStatic)
        a.Velocity = a.Velocity - impulse * invMassA;
    if (!b.IsStatic)
        b.Velocity = b.Velocity + impulse * invMassB;
}

public static void PositionalCorrection(Manifold manifold, double percent = 0.8, double slop = 0.01)
{
    var (a, b, normal, depth) = 
        (manifold.BodyA, manifold.BodyB, manifold.Normal, manifold.Depth);

    // Skip positional correction for latched pairs
    if (a.LatchedPartnerId == b.Id || b.LatchedPartnerId == a.Id)
        return;

    double invMassA = a.InverseMass;
    double invMassB = b.InverseMass;
    double invMassSum = invMassA + invMassB;

    if (invMassSum < 0.0001) return;

    double correction = System.Math.Max(depth - slop, 0) / invMassSum * percent;
    Vector2 correctionVector = normal * correction;

    if (!a.IsStatic)
        a.Position = a.Position - correctionVector * invMassA;
    if (!b.IsStatic)
        b.Position = b.Position + correctionVector * invMassB;
}

    public static void ResolveAll(List<Manifold> manifolds)
    {
        foreach (var manifold in manifolds)
        {
            Resolve(manifold);
        }

        foreach (var manifold in manifolds)
        {
            PositionalCorrection(manifold);
        }
    }
}