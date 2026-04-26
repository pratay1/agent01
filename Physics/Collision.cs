using PhysicsSandbox.Mathematics;

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
        // Phantom bodies pass through everything - skip collision detection
        if (a.BodyType == BodyType.Phantom || b.BodyType == BodyType.Phantom)
            return null;

        // Collision layer filtering: skip if layers don't interact
        // Check if a can collide with b's layer
        if ((a.CollisionMask & (1 << (int)b.CollisionLayer)) == 0)
            return null;
        // Check if b can collide with a's layer
        if ((b.CollisionMask & (1 << (int)a.CollisionLayer)) == 0)
            return null;

        Vector2 diff = b.Position - a.Position;
        double distanceSq = diff.LengthSquared;
        double radiusSum = a.Radius + b.Radius;

        if (distanceSq >= radiusSum * radiusSum)
            return null;

        double distance = System.Math.Sqrt(distanceSq);
        if (distance < 0.0001)
        {
            // Use deterministic direction for perfectly overlapping bodies
            double angle = DeterministicAngle(a, b);
            return new Manifold(a, b, new Vector2(System.Math.Cos(angle), System.Math.Sin(angle)), radiusSum);
        }

        double depth = radiusSum - distance;
        Vector2 normal = diff / distance;

        return new Manifold(a, b, normal, depth);
    }

    private static double DeterministicAngle(RigidBody a, RigidBody b)
    {
        int min = Math.Min(a.Id, b.Id);
        int max = Math.Max(a.Id, b.Id);
        int seed = (min << 16) ^ max;
        var rng = new System.Random(seed);
        return rng.NextDouble() * Math.PI * 2;
    }

    public static List<Manifold> DetectAll(List<RigidBody> bodies)
    {
        List<Manifold> manifolds = new();

        int count = bodies.Count;
        if (count == 0) return manifolds;

        // Compute cell size as ~2x the largest body radius
        double maxRadius = 0;
        foreach (var body in bodies)
        {
            if (body.Radius > maxRadius)
                maxRadius = body.Radius;
        }
        double cellSize = System.Math.Max(maxRadius * 2, 100.0);

        // Build spatial hash
        var spatialHash = new SpatialHash(cellSize);
        foreach (var body in bodies)
        {
            spatialHash.Insert(body);
        }

        // For each body, query potential neighbors from spatial hash
        // Only test pairs where bodyB.Id > bodyA.Id to avoid duplicate pairs
        for (int i = 0; i < count; i++)
        {
            var a = bodies[i];
            var candidates = spatialHash.Query(a.Position, a.Radius);

            foreach (var b in candidates)
            {
                // Avoid duplicate pairs: only check when bodyB.Id > bodyA.Id
                if (b.Id <= a.Id)
                    continue;

                var manifold = DetectCircleCircle(a, b);
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

        double correction = (System.Math.Max(depth - slop, 0) * percent) / invMassSum;
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
