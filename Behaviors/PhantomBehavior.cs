using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class PhantomBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Phantom;
    public override string Name => "Phantom";
    public override string Description => "Phases through bodies & violently shakes them!";
    public override string ColorHex => "#B388FF";
    public override double DefaultRadius => 18;
    public override double DefaultMass => 4;
    public override double DefaultRestitution => 0.5;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        body.LifeTime += dt; // accumulate time for deterministic shake

        var bodies = world.Bodies.ToList(); // Snapshot
        foreach (var other in bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var dist = (float)Vector2.Distance(body.Position, other.Position);
            if (dist < body.Radius + other.Radius + 10)
            {
                // Violent shake - random direction force that changes rapidly
                var shakeAngle = (float)(Random.Shared.NextDouble() * System.Math.PI * 2);
                var shakeFrequency = 50f;
                var shakePhase = body.LifeTime * shakeFrequency * 2.0 * System.Math.PI;
                var shakeFactor = (float)System.Math.Abs(System.Math.Sin(shakePhase));
                var shakeStrength = 8000f * shakeFactor;
                
                var shakeDir = new Vector2(
                    (float)System.Math.Cos(shakeAngle),
                    (float)System.Math.Sin(shakeAngle));
                
                other.ApplyForce(shakeDir * shakeStrength);
                body.ApplyForce(-shakeDir * 200);
            }
        }
    }
}