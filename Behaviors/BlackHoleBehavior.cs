using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class BlackHoleBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.BlackHole;
    public override string Name => "Black Hole";
    public override string Description => "Sucks in everything & grows!";
    public override string ColorHex => "#0D0D0D";
    public override double DefaultRadius => 15;
    public override double DefaultMass => 10;
    public override double DefaultRestitution => 0;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        const float suckRadius = 300f;
        const float suckStrength = 12000f;

        foreach (var other in world.Bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var direction = body.Position - other.Position;
            var distance = (float)direction.Length;

            if (distance > 0 && distance < suckRadius)
            {
                var forceMagnitude = suckStrength * (1 - distance / suckRadius) / (distance * distance);
                var force = direction.Normalized * forceMagnitude;
                other.ApplyForce(force);

                if (distance < body.Radius + other.Radius + 20)
                {
                    other.Radius = System.Math.Max(5, other.Radius * 0.999);
                }
            }
        }

        body.Radius = System.Math.Min(100, body.Radius * 1.001);
    }
}