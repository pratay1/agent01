using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class GlueBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Glue;
    public override string Name => "Glue";
    public override string Description => "Sticks to anything it touches";
    public override string ColorHex => "#AED581";
    public override double DefaultRadius => 17;
    public override double DefaultMass => 10;
    public override double DefaultRestitution => 0.05;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        foreach (var other in world.Bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var dist = (float)Vector2.Distance(body.Position, other.Position);
            if (dist < body.Radius + other.Radius + 10)
            {
                other.Velocity = other.Velocity * 0.9f;
                body.Velocity = body.Velocity * 0.9f;
            }
        }
    }
}