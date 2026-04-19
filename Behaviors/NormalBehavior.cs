using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class NormalBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Normal;
    public override string Name => "Normal";
    public override string Description => "Standard physics body";
    public override string ColorHex => "#4FC3F7";
    public override double DefaultRadius => 15;
    public override double DefaultMass => 10;
    public override double DefaultRestitution => 0.5;
}