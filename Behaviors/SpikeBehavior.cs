using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class SpikeBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Spike;
    public override string Name => "Spike";
    public override string Description => "Violent bounce & explodes on contact!";
    public override string ColorHex => "#F44336";
    public override double DefaultRadius => 14;
    public override double DefaultMass => 7;
    public override double DefaultRestitution => 0.98;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.HasExploded) return;

        var bodies = world.Bodies.ToList(); // Snapshot
        foreach (var other in bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var dist = (float)Vector2.Distance(body.Position, other.Position);
             if (dist < body.Radius + other.Radius)
             {
                 // Violent bounce
                 var normal = (body.Position - other.Position).Normalized;
                 var bounceForce = 3000f;
                 body.ApplyImpulse(normal * bounceForce / (float)body.Mass);
                 other.ApplyImpulse(-normal * bounceForce / (float)other.Mass);

                // Trigger explosion
                TriggerExplosion(body, world);
                break; // Only explode once per frame
             }
        }
    }

    private void TriggerExplosion(RigidBody body, PhysicsWorld world)
    {
        body.HasExploded = true;
        world.ForceManager.Explosion.Trigger(body.Position);

        // Spawn debris
        for (int i = 0; i < 8; i++)
        {
            float angle = i * (float)System.Math.PI * 2 / 8;
            float speed = 200f + (float)(Random.Shared.NextDouble() * 100);
            var vel = new Vector2(
                (float)System.Math.Cos(angle) * speed,
                (float)System.Math.Sin(angle) * speed);

            var debris = world.CreateBody(body.Position, body.Radius * 0.2, body.Mass * 0.1, 0.5);
            debris.Velocity = vel;
            debris.BodyType = BodyType.Fire;
        }

        // Apply explosion force to nearby bodies
        var bodies = world.Bodies;
        for (int j = 0; j < bodies.Count; j++)
        {
            var other = bodies[j];
            if (body == other) continue;
            var dir = (other.Position - body.Position).Normalized;
            var dist = (float)Vector2.Distance(body.Position, other.Position);
            float safeDist = dist > 0.1f ? dist : 0.1f; // Prevent extreme forces at very close range
            var force = 12000f / (safeDist * safeDist + 1);
            other.ApplyImpulse(dir * force * 30);
        }

        world.RemoveBody(body);
    }
}