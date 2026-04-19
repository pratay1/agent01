using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class HeavyBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Heavy;
    public override string Name => "Heavy";
    public override string Description => "Massive weight - pushes through everything";
    public override string ColorHex => "#FFB74D";
    public override double DefaultRadius => 20;
    public override double DefaultMass => 35;
    public override double DefaultRestitution => 0.15;

    public override void OnCreate(RigidBody body)
    {
        body.Mass = 30;
        body.Restitution = 0.15;
    }
}