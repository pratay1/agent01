using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class GlueBehavior : BodyBehavior
{
    public override BodyType Type => BodyType.Glue;
    public override string Name => "Glue";
    public override string Description => "Sticks to anything it touches";
    public override string ColorHex => "#76FF03";
    public override double DefaultRadius => 17;
    public override double DefaultMass => 12;
    public override double DefaultRestitution => 0.02;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        var bodies = world.Bodies.ToList(); // Snapshot
        foreach (var other in bodies)
        {
            if (body == other || other.IsStatic) continue;
            
            var dist = (float)Vector2.Distance(body.Position, other.Position);
            if (dist < body.Radius + other.Radius + 15)
            {
                other.Velocity = other.Velocity * 0.85f;
                body.Velocity = body.Velocity * 0.85f;
            }
        }
        
        var leftWall = world.LeftBoundary + body.Radius + 5;
        var rightWall = world.RightBoundary - body.Radius - 5;
        var ground = world.GroundY - body.Radius - 5;
        
        if (body.Position.X < leftWall + 10 || 
            body.Position.X > rightWall - 10 ||
            body.Position.Y > ground - 10)
        {
            body.Velocity = Vector2.Zero;
            body.IsStuck = true;
        }
        else
        {
            body.IsStuck = false;
        }
        
        if (body.IsStuck)
        {
            body.Velocity = Vector2.Zero;
        }
    }
}