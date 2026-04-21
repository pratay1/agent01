using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class AngelBehavior : BodyBehavior
{
    private double _flyTimer;
    private bool _isFlying;
    private readonly double _flyInterval = 2.0;
    private readonly double _flyDuration = 1.5;

    public override BodyType Type => BodyType.Angel;
    public override string Name => "Angel";
    public override string Description => "Flies periodically - gentle & light";
    public override string ColorHex => "#FFFFFF";
    public override double DefaultRadius => 18;
    public override double DefaultMass => 4;
    public override double DefaultRestitution => 0.6;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        _flyTimer += dt;

        if (!_isFlying && _flyTimer >= _flyInterval)
        {
            _isFlying = true;
            _flyTimer = 0;
        }

        if (_isFlying && _flyTimer >= _flyDuration)
        {
            _isFlying = false;
            _flyTimer = 0;
        }

        if (_isFlying)
        {
            body.ApplyForce(new Vector2(0, -1800));
            body.ApplyForce(new Vector2(0, -300));
        }
    }
}