using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class PhantomBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Phantom;
    public override string Name => "Phantom";
    public override string Description => "Passes through objects but affects them";
    public override string ColorHex => "#B388FF";
    public override double DefaultRadius => 18;
    public override double DefaultMass => 4;
    public override double DefaultRestitution => 0.5;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        foreach (var other in world.Bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var dist = (float)Vector2.Distance(body.Position, other.Position);
            if (dist < body.Radius + other.Radius + 5)
            {
                var direction = (other.Position - body.Position).Normalized;
                other.ApplyForce(direction * 800);
                body.ApplyForce(-direction * 50);
            }
        }
    }
}