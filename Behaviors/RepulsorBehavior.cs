using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class RepulsorBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Repulsor;
    public override string Name => "Repulsor";
    public override string Description => "Pushes away everything nearby with huge force!";
    public override string ColorHex => "#BA68C8";
    public override double DefaultRadius => 16;
    public override double DefaultMass => 8;
    public override double DefaultRestitution => 0.6;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        const float repulsionRadius = 200f;
        const float repulsionStrength = 8000f;

        var bodies = world.Bodies.ToList(); // Snapshot
        foreach (var other in bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var direction = other.Position - body.Position;
            var distance = (float)direction.Length;

            if (distance > 0 && distance < repulsionRadius)
            {
                var forceMagnitude = repulsionStrength * (1 - distance / repulsionRadius);
                var force = direction.Normalized * forceMagnitude;
                other.ApplyForce(force);
            }
        }
    }
}
