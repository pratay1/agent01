using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class LightningBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Lightning;
    public override string Name => "Lightning";
    public override string Description => "Zaps nearby objects with electric force!";
    public override string ColorHex => "#FF9800";
    public override double DefaultRadius => 11;
    public override double DefaultMass => 3;
    public override double DefaultRestitution => 0.7;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        const float chainRadius = 180f;
        const float chainStrength = 6000f;

        foreach (var other in world.Bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var direction = other.Position - body.Position;
            var distance = (float)direction.Length;

            if (distance > 0 && distance < chainRadius)
            {
                var forceMagnitude = chainStrength * (1 - distance / chainRadius);
                var force = direction.Normalized * forceMagnitude;
                other.ApplyForce(force);
            }
        }
    }
}