using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class AntiGravityBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.AntiGravity;
    public override string Name => "Anti-Gravity";
    public override string Description => "Floats upward - defies gravity completely";
    public override string ColorHex => "#00BCD4";
    public override double DefaultRadius => 13;
    public override double DefaultMass => 5;
    public override double DefaultRestitution => 0.7;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        body.ApplyForce(new Vector2(0, -1500));
    }
}