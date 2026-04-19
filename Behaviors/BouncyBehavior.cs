using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class BouncyBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Bouncy;
    public override string Name => "Bouncy";
    public override string Description => "Super bouncy - bounces like crazy!";
    public override string ColorHex => "#81C784";
    public override double DefaultRadius => 12;
    public override double DefaultMass => 6;
    public override double DefaultRestitution => 0.95;

    public override void OnCreate(RigidBody body)
    {
        body.Restitution = 0.95;
    }
}