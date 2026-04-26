using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Physics;

public static class Collision
{
    public static void ResolveAll(List<RigidBody> bodies, SpatialHash hash)
    {
        hash.Clear();
        foreach (var body in bodies)
            hash.Insert(body);

        for (int i = 0; i < bodies.Count; i++)
        {
            var a = bodies[i];
            if (a.IsStatic) continue;

            var candidates = hash.Query(a);
            foreach (var b in candidates)
            {
                if (b.Id <= a.Id || b.IsStatic && a.IsStatic) continue;
                Resolve(a, b);
            }
        }
    }

    public static void Resolve(RigidBody a, RigidBody b)
    {
        if (a.LatchedPartnerId == b.Id || b.LatchedPartnerId == a.Id)
            return;

        double dx = b.Position.X - a.Position.X;
        double dy = b.Position.Y - a.Position.Y;
        double distSq = dx * dx + dy * dy;
        double minDist = a.Radius + b.Radius;

        if (distSq >= minDist * minDist) return;

        double dist = System.Math.Sqrt(distSq);
        if (dist < 0.001)
        {
            dx = 1; dy = 0;
        }
        else
        {
            dx /= dist;
            dy /= dist;
        }

        double overlap = minDist - dist;
        double totalMass = a.Mass + b.Mass;

        if (!a.IsStatic && !b.IsStatic)
        {
            a.Position = new Vector2(a.Position.X - dx * overlap * 0.5, a.Position.Y - dy * overlap * 0.5);
            b.Position = new Vector2(b.Position.X + dx * overlap * 0.5, b.Position.Y + dy * overlap * 0.5);
        }
        else if (!a.IsStatic)
        {
            a.Position = new Vector2(a.Position.X - dx * overlap, a.Position.Y - dy * overlap);
        }
        else if (!b.IsStatic)
        {
            b.Position = new Vector2(b.Position.X + dx * overlap, b.Position.Y + dy * overlap);
        }

        double rvx = b.Velocity.X - a.Velocity.X;
        double rvy = b.Velocity.Y - a.Velocity.Y;
        double velAlongNormal = rvx * dx + rvy * dy;

        if (velAlongNormal > 0) return;

        double e = System.Math.Min(a.Restitution, b.Restitution);
        double j = -(1 + e) * velAlongNormal;
        if (!a.IsStatic && !b.IsStatic)
            j /= a.InverseMass + b.InverseMass;
        else if (!a.IsStatic)
            j /= a.InverseMass;
        else if (!b.IsStatic)
            j /= b.InverseMass;
        else
            return;

        double jx = dx * j;
        double jy = dy * j;

        if (!a.IsStatic)
        {
            a.Velocity = new Vector2(a.Velocity.X - jx * a.InverseMass, a.Velocity.Y - jy * a.InverseMass);
        }
        if (!b.IsStatic)
        {
            b.Velocity = new Vector2(b.Velocity.X + jx * b.InverseMass, b.Velocity.Y + jy * b.InverseMass);
        }
    }
}
