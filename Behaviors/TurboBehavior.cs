using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class TurboBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Turbo;
    public override string Name => "Turbo";
    public override string Description => "Accelerates continuously - goes supersonic!";
    public override string ColorHex => "#FFEB3B";
    public override double DefaultRadius => 10;
    public override double DefaultMass => 3;
    public override double DefaultRestitution => 0.8;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.Velocity.Length > 1)
        {
            var boost = body.Velocity.Normalized * 500;
            body.ApplyForce(boost);
        }
    }
}