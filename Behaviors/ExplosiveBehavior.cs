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

        foreach (var other in world.Bodies)
        {
            if (body == other) continue;
            if (Vector2.Distance(body.Position, other.Position) < body.Radius + other.Radius)
            {
                body.HasExploded = true;
                world.ForceManager.Explosion.Trigger(body.Position);

                var rand = new Random();
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * (float)System.Math.PI * 2 / 12;
                    float speed = 300f + rand.Next(200);
                    var vel = new Vector2(
                        (float)System.Math.Cos(angle) * speed,
                        (float)System.Math.Sin(angle) * speed);

                    var debris = world.CreateBody(body.Position, body.Radius * 0.25, body.Mass * 0.15, 0.5);
                    debris.Velocity = vel;
                    debris.BodyType = BodyType.Fire;
                }

                foreach (var other2 in world.Bodies)
                {
                    if (body == other2) continue;
                    var dir = (other2.Position - body.Position).Normalized;
                    var dist = (float)Vector2.Distance(body.Position, other2.Position);
                    var force = 15000f / (dist * dist + 1);
                    other2.ApplyImpulse(dir * force * 50);
                }

                world.RemoveBody(body);
                break;
            }
        }
    }
}