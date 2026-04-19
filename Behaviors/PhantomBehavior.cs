using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class PhantomBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Phantom;
    public override string Name => "Phantom";
    public override string Description => "Passes through but affects objects";
    public override string ColorHex => "#9575CD";
    public override double DefaultRadius => 16;
    public override double DefaultMass => 6;
    public override double DefaultRestitution => 0.5;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        foreach (var other in world.Bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var dist = (float)Vector2.Distance(body.Position, other.Position);
            if (dist < body.Radius + other.Radius)
            {
                var direction = (other.Position - body.Position).Normalized;
                other.ApplyForce(direction * 500);
            }
        }
    }
}