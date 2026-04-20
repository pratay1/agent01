using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class BlackHoleBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.BlackHole;
    public override string Name => "Black Hole";
    public override string Description => "Sucks in everything & grows!";
    public override string ColorHex => "#1A1A1A";
    public override double DefaultRadius => 15;
    public override double DefaultMass => 15;
    public override double DefaultRestitution => 0;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        const float suckRadius = 350f;
        const float suckStrength = 20000f;

        var bodiesToRemove = new List<RigidBody>();
        
        foreach (var other in world.Bodies.ToList())
        {
            if (body == other || other.IsStatic) continue;
            
            var direction = body.Position - other.Position;
            var distance = (float)direction.Length;

            if (distance > 0 && distance < suckRadius)
            {
                var forceMagnitude = suckStrength * (1 - distance / suckRadius) / (distance * 0.5f + 1);
                var force = direction.Normalized * forceMagnitude;
                other.ApplyForce(force);

                if (distance < body.Radius + other.Radius + 30)
                {
                    other.Radius = System.Math.Max(3, other.Radius * 0.998);
                    if (other.Radius <= 3)
                    {
                        bodiesToRemove.Add(other);
                    }
                }
            }
        }

        foreach (var toRemove in bodiesToRemove)
        {
            try { world.RemoveBody(toRemove); } catch { }
        }

        body.Radius = System.Math.Min(80, body.Radius * 1.002);
    }
}