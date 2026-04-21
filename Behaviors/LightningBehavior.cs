using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class LightningBehavior : BodyBehavior
{
    private float _zapTimer = 0;
    
    public override BodyType Type => BodyType.Lightning;
    public override string Name => "Lightning";
    public override string Description => "Zaps nearby objects with electric force!";
    public override string ColorHex => "#FFD600";
    public override double DefaultRadius => 14;
    public override double DefaultMass => 4;
    public override double DefaultRestitution => 0.7;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        const float chainRadius = 200f;
        const float chainStrength = 10000f;
        
        _zapTimer += (float)dt;
        
        if (_zapTimer > 0.5f) _zapTimer = 0;

        var bodies = world.Bodies.ToList(); // Snapshot
        foreach (var other in bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var direction = other.Position - body.Position;
            var distance = (float)direction.Length;

            if (distance > 0 && distance < chainRadius)
            {
                var forceMagnitude = chainStrength * (1 - distance / chainRadius);
                var force = direction.Normalized * forceMagnitude;
                other.ApplyForce(force);
                
                var perp = new Vector2(-direction.Y * 0.5f, direction.X * 0.5f);
                var zap = perp * forceMagnitude * 0.4f * System.Math.Abs(System.Math.Sin(_zapTimer * 30));
                other.ApplyForce(zap);
            }
        }
    }
}