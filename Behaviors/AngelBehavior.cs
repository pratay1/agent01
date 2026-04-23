using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class AngelBehavior : BodyBehavior
{
    private const double FlingInterval = 3.0;
    private const double FlingForce = 5000;

    public override BodyType Type => BodyType.Angel;
    public override string Name => "Angel";
    public override string Description => "Flies periodically - gentle & light";
    public override string ColorHex => "#FFFFFF";
    public override double DefaultRadius => 18;
    public override double DefaultMass => 4;
    public override double DefaultRestitution => 0.6;

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        body.FlyTimer = 0;
    }

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        body.FlyTimer += dt;

        if (body.FlyTimer >= FlingInterval)
        {
            // Random fling direction every 3 seconds
            double angle = Random.Shared.NextDouble() * System.Math.PI * 2;
            var dir = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));
            body.ApplyImpulse(dir * FlingForce);
            body.FlyTimer = 0;
        }
    }
}
