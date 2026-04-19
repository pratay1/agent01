using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class FireBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Fire;
    public override string Name => "Fire";
    public override string Description => "Rising flames - disappears after 3 seconds";
    public override string ColorHex => "#FF5722";
    public override double DefaultRadius => 8;
    public override double DefaultMass => 2;
    public override double DefaultRestitution => 0.5;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        body.ApplyForce(new Vector2(0, -300));
        body.LifeTime += dt;
        if (body.LifeTime > 3)
        {
            world.RemoveBody(body);
        }
    }
}