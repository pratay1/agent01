using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class PlasmaBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Plasma;
    public override string Name => "Plasma";
    public override string Description => "Electric zigzag chains to nearby!";
    public override string ColorHex => "#E91E63";
    public override double DefaultRadius => 12;
    public override double DefaultMass => 4;
    public override double DefaultRestitution => 0.6;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        const float plasmaRadius = 150f;
        const float plasmaStrength = 4000f;

        var bodies = world.Bodies.ToList(); // Snapshot
        foreach (var other in bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var direction = other.Position - body.Position;
            var distance = (float)direction.Length;

            if (distance > 0 && distance < plasmaRadius)
            {
                var forceMagnitude = plasmaStrength * (1 - distance / plasmaRadius);
                var force = direction.Normalized * forceMagnitude;
                other.ApplyForce(force);

                var perp = new Vector2(-direction.Y, direction.X);
                other.ApplyForce(perp * forceMagnitude * 0.3f);
            }
        }
    }
}