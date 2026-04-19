using System.Windows.Media;
using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class GravityWellBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.GravityWell;
    public override string Name => "Gravity Well";
    public override string Description => "Attracts nearby objects like a black hole";
    public override string ColorHex => "#00E5FF";
    public override double DefaultRadius => 18;
    public override double DefaultMass => 8;
    public override double DefaultRestitution => 0.3;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        const float attractRadius = 250f;
        const float attractStrength = 15000f;

        foreach (var other in world.Bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var direction = body.Position - other.Position;
            var distance = (float)direction.Length;

            if (distance > 0 && distance < attractRadius)
            {
                var forceMagnitude = attractStrength * (1 - distance / attractRadius);
                var force = direction.Normalized * forceMagnitude;
                other.ApplyForce(force);
            }
        }
    }
}