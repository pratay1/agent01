using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class SpikeBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Spike;
    public override string Name => "Spike";
    public override string Description => "Violent bounce - super bouncy spike!";
    public override string ColorHex => "#F44336";
    public override double DefaultRadius => 14;
    public override double DefaultMass => 7;
    public override double DefaultRestitution => 0.98;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        var bodies = world.Bodies.ToList(); // Snapshot
        foreach (var other in bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var dist = (float)Vector2.Distance(body.Position, other.Position);
            if (dist < body.Radius + other.Radius)
            {
                var normal = (body.Position - other.Position).Normalized;
                var bounceForce = 3000f;
                body.ApplyImpulse(normal * bounceForce / (float)body.Mass);
                other.ApplyImpulse(-normal * bounceForce / (float)other.Mass);
            }
        }
    }
}