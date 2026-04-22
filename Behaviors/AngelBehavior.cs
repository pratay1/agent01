using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class AngelBehavior : BodyBehavior
{
    private readonly double _flyIntervalMin = 4.0;
    private readonly double _flyIntervalMax = 6.0;
    private readonly double _flyDuration = 1.5;

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
        body.FlyInterval = _flyIntervalMin + Random.Shared.NextDouble() * (_flyIntervalMax - _flyIntervalMin);
        body.FlyTimer = 0;
    }

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        body.FlyTimer += dt;

        if (!body.IsFlying)
        {
            if (body.FlyTimer >= body.FlyInterval)
            {
                body.IsFlying = true;
                body.FlyTimer = 0; // Reset timer when flight starts
            }
        }
        else
        {
            if (body.FlyTimer >= _flyDuration)
            {
                body.IsFlying = false;
                body.FlyTimer = 0;
            }
        }

        if (body.IsFlying)
        {
            body.ApplyForce(new Vector2(0, -2000));
        }
    }
}
