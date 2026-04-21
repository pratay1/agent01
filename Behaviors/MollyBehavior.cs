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

        foreach (var other in world.Bodies)
        {
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
                var partner = world.Bodies.FirstOrDefault(b => b.Id == body.LatchedPartnerId);
                // Verify partner is still valid and is an Angel
                if (partner == null || partner.BodyType != BodyType.Angel)
                {
                    body.LatchedPartnerId = null;
                }
                else
                {
                    // Check for third body approaching to break latch
                    bool thirdBodyNear = false;
                    foreach (var other in world.Bodies)
                    {
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
                        // Break latch
                        partner.LatchedPartnerId = null;
                        body.LatchedPartnerId = null;
                    }
                    else
                    {
                        // Maintain latch: sync velocity and position
                        body.Velocity = partner.Velocity;
                        var dir = (body.Position - partner.Position).Normalized;
                        if (dir.LengthSquared < 1e-6)
                            dir = new Vector2(1, 0);
                        body.Position = partner.Position + dir * (float)(body.Radius + partner.Radius);
                        return;
                    }
                }
            }

            // Attraction force towards Angel
            var dirToAngel = (nearestAngel.Position - body.Position).Normalized;
            body.ApplyForce(dirToAngel * 800);

            // If very close, latch
            if (Vector2.Distance(body.Position, nearestAngel.Position) < body.Radius + nearestAngel.Radius + 5)
            {
                body.LatchedPartnerId = nearestAngel.Id;
                nearestAngel.LatchedPartnerId = body.Id;
                body.Velocity = nearestAngel.Velocity;
            }
        }
        else // No Angel nearby
        {
            // If was latched, clear latch
            if (body.LatchedPartnerId.HasValue)
            {
                var partner = world.Bodies.FirstOrDefault(b => b.Id == body.LatchedPartnerId);
                if (partner != null)
                    partner.LatchedPartnerId = null;
                body.LatchedPartnerId = null;
            }

            // Check collision with any other body (including other Mollys)
            foreach (var other in world.Bodies)
            {
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
        // Clear any existing latch
        if (body.LatchedPartnerId.HasValue)
        {
            var partner = world.Bodies.FirstOrDefault(b => b.Id == body.LatchedPartnerId);
            if (partner != null)
                partner.LatchedPartnerId = null;
            body.LatchedPartnerId = null;
        }

        body.HasExploded = true;
        world.ForceManager.Explosion.Trigger(body.Position);

        var rand = new Random();
        for (int i = 0; i < 10; i++)
        {
            float angle = i * (float)System.Math.PI * 2 / 10;
            float speed = 250f + rand.Next(150);
            var vel = new Vector2(
                (float)System.Math.Cos(angle) * speed,
                (float)System.Math.Sin(angle) * speed);

            var debris = world.CreateBody(body.Position, body.Radius * 0.2, body.Mass * 0.1, 0.5);
            debris.Velocity = vel;
            debris.BodyType = BodyType.Fire;
        }

        foreach (var other2 in world.Bodies)
        {
            if (body == other2) continue;
            var dir = (other2.Position - body.Position).Normalized;
            var dist = (float)Vector2.Distance(body.Position, other2.Position);
            var force = ExplosionForce / (dist * dist + 1);
            other2.ApplyImpulse(dir * force * 30);
        }

        try { world.RemoveBody(body); } catch { }
    }
}