using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class ExplosiveBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Explosive;
    public override string Name => "Explosive";
    public override string Description => "BOOM! Explodes on contact with anything";
    public override string ColorHex => "#E53935";
    public override double DefaultRadius => 20;
    public override double DefaultMass => 8;
    public override double DefaultRestitution => 0.4;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.HasExploded) return;

        var bodies = world.Bodies;
        for (int i = 0; i < bodies.Count; i++)
        {
            var other = bodies[i];
            if (body == other) continue;
            if (Vector2.Distance(body.Position, other.Position) < body.Radius + other.Radius)
            {
                body.HasExploded = true;
                world.ForceManager.Explosion.Trigger(body.Position);

                for (int j = 0; j < 12; j++)
                {
                    float angle = j * (float)System.Math.PI * 2 / 12;
                    float speed = 300f + (float)(_rand.NextDouble() * 200);
                    var vel = new Vector2(
                        (float)System.Math.Cos(angle) * speed,
                        (float)System.Math.Sin(angle) * speed);

                    var debris = world.CreateBody(body.Position, body.Radius * 0.25, body.Mass * 0.15, 0.5);
                    debris.Velocity = vel;
                    debris.BodyType = BodyType.Fire;
                }

                for (int j = 0; j < bodies.Count; j++)
                {
                    var other2 = bodies[j];
                    if (body == other2) continue;
                    var dir = (other2.Position - body.Position).Normalized;
                    var dist = (float)Vector2.Distance(body.Position, other2.Position);
                    var force = 15000f / (dist * dist + 1);
                    other2.ApplyImpulse(dir * force * 50);
                }

                try { world.RemoveBody(body); } catch { }
                break;
            }
        }
    }

    private static readonly Random _rand = new Random();
}