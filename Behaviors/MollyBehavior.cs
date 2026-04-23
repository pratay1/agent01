using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class MollyBehavior : BodyBehavior
{
    private const double AngelDetectionRadius = 150;
    private const double ExplosionForce = 15000;

    public override BodyType Type => BodyType.Molly;
    public override string Name => "Molly";
    public override string Description => "Explodes on contact unless Angel is nearby - attracts to Angel and latches";
    public override string ColorHex => "#FF4081";
    public override double DefaultRadius => 16;
    public override double DefaultMass => 7;
    public override double DefaultRestitution => 0.4;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.HasExploded) return;

        RigidBody? nearestAngel = null;
        double closestDist = double.MaxValue;

        var bodies = world.Bodies;
        for (int i = 0; i < bodies.Count; i++)
        {
            var other = bodies[i];
            if (other.BodyType == BodyType.Angel && other != body)
            {
                double dist = Vector2.Distance(body.Position, other.Position);
                if (dist < AngelDetectionRadius && dist < closestDist)
                {
                    closestDist = dist;
                    nearestAngel = other;
                }
            }
        }

        if (nearestAngel != null)
        {
            // Check if already latched
            if (body.LatchedPartnerId.HasValue)
            {
                if (!world.TryGetBodyById(body.LatchedPartnerId.Value, out var partner) || partner.BodyType != BodyType.Angel)
                {
                    body.LatchedPartnerId = null;
                }
                else
                {
                    // Check for third body approaching to break latch
                    bool thirdBodyNear = false;
                    for (int j = 0; j < bodies.Count; j++)
                    {
                        var other = bodies[j];
                        if (other == body || other == partner) continue;
                        double dist = Vector2.Distance(body.Position, other.Position);
                        if (dist < body.Radius + other.Radius + 20)
                        {
                            thirdBodyNear = true;
                            break;
                        }
                    }

                    if (thirdBodyNear)
                    {
                        partner.LatchedPartnerId = null;
                        body.LatchedPartnerId = null;
                    }
                    else
                    {
                        body.Velocity = partner.Velocity;
                        var dirFromPartner = (body.Position - partner.Position).Normalized;
                        if (dirFromPartner.LengthSquared < 1e-6)
                            dirFromPartner = new Vector2(1, 0);
                        body.Position = partner.Position + dirFromPartner * (body.Radius + partner.Radius);
                        return;
                    }
                }
            }

            var dirToAngel = (nearestAngel.Position - body.Position).Normalized;
            body.ApplyForce(dirToAngel * 5000); // Incredibly strong attraction

            if (Vector2.Distance(body.Position, nearestAngel.Position) < body.Radius + nearestAngel.Radius + 5)
            {
                body.LatchedPartnerId = nearestAngel.Id;
                nearestAngel.LatchedPartnerId = body.Id;
                body.Velocity = nearestAngel.Velocity;
            }
        }
        else
        {
            if (body.LatchedPartnerId.HasValue)
            {
                if (world.TryGetBodyById(body.LatchedPartnerId.Value, out var partner))
                {
                    partner.LatchedPartnerId = null;
                }
                body.LatchedPartnerId = null;
            }

            for (int j = 0; j < bodies.Count; j++)
            {
                var other = bodies[j];
                if (body == other) continue;

                double dist = Vector2.Distance(body.Position, other.Position);
                if (dist < body.Radius + other.Radius)
                {
                    TriggerExplosion(body, world);
                    return;
                }
            }
        }
        }

        private void TriggerExplosion(RigidBody body, PhysicsWorld world)
    {
        if (body.LatchedPartnerId.HasValue && world.TryGetBodyById(body.LatchedPartnerId.Value, out var partner))
        {
            partner.LatchedPartnerId = null;
            body.LatchedPartnerId = null;
        }

        body.HasExploded = true;
        world.ForceManager.Explosion.Trigger(body.Position);

        for (int i = 0; i < 10; i++)
        {
            float angle = i * (float)System.Math.PI * 2 / 10;
            float speed = 250f + (float)(Random.Shared.NextDouble() * 150);
            var vel = new Vector2(
                (float)System.Math.Cos(angle) * speed,
                (float)System.Math.Sin(angle) * speed);

            var debris = world.CreateBody(body.Position, body.Radius * 0.2, body.Mass * 0.1, 0.5);
            debris.Velocity = vel;
            debris.BodyType = BodyType.Fire;
        }

        var bodies = world.Bodies;
        for (int j = 0; j < bodies.Count; j++)
        {
            var other2 = bodies[j];
            if (body == other2) continue;
            var dir = (other2.Position - body.Position).Normalized;
            var dist = (float)Vector2.Distance(body.Position, other2.Position);
            var force = ExplosionForce / (dist * dist + 1);
            other2.ApplyImpulse(dir * force * 30);
        }

        try { world.RemoveBody(body); } catch { }
    }
}