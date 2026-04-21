using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class FreezerBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Freezer;
    public override string Name => "Freezer";
    public override string Description => "Slows down anything it touches";
    public override string ColorHex => "#81D4FA";
    public override double DefaultRadius => 15;
    public override double DefaultMass => 10;
    public override double DefaultRestitution => 0.3;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        var bodies = world.Bodies.ToList(); // Snapshot to avoid concurrent modification
        foreach (var other in bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var dist = (float)Vector2.Distance(body.Position, other.Position);
            if (dist < body.Radius + other.Radius + 30)
            {
                other.Velocity = other.Velocity * 0.92f;
            }
        }
    }
}