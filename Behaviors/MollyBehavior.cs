using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public class MollyBehavior : BodyBehavior
{
    private static readonly Dictionary<int, int> _latchedBodies = new();
    private const double AngelDetectionRadius = 150;
    private const double ExplosionForce = 15000;

    public override BodyType Type => BodyType.Molly;
    public override string Name => "Molly";
    public override string Description => "Explodes on contact unless Angel is nearby - attracts to Angel and latches";
    public override string ColorHex => "#FF4081";
    public override double DefaultRadius => 16;
    public override double DefaultMass => 7;
    public override double DefaultRestitution => 0.4;

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.HasExploded) return;

        RigidBody? nearbyAngel = null;
        double closestAngelDist = double.MaxValue;

        foreach (var other in world.Bodies)
        {
            if (other.BodyType == BodyType.Angel && other != body)
            {
                var dist = Vector2.Distance(body.Position, other.Position);
                if (dist < AngelDetectionRadius && dist < closestAngelDist)
                {
                    closestAngelDist = dist;
                    nearbyAngel = other;
                }
            }
        }

        if (nearbyAngel != null)
        {
            if (_latchedBodies.ContainsKey(body.Id))
            {
                int latchedToId = _latchedBodies[body.Id];
                var latchedBody = world.Bodies.FirstOrDefault(b => b.Id == latchedToId);
                if (latchedBody == null || latchedBody.BodyType != BodyType.Angel)
                {
                    _latchedBodies.Remove(body.Id);
                }
                else
                {
                    foreach (var other in world.Bodies)
                    {
                        if (other == body || other == latchedBody) continue;
                        var dist = Vector2.Distance(body.Position, other.Position);
                        if (dist < body.Radius + other.Radius + 20)
                        {
                            _latchedBodies.Remove(body.Id);
                            break;
                        }
                    }

                    if (_latchedBodies.ContainsKey(body.Id))
                    {
                        body.Velocity = latchedBody.Velocity;
                        body.Position = latchedBody.Position + (body.Position - latchedBody.Position).Normalized * (float)(body.Radius + latchedBody.Radius);
                        return;
                    }
                }
            }

            var dirToAngel = (nearbyAngel.Position - body.Position).Normalized;
            body.ApplyForce(dirToAngel * 800);

            if (closestAngelDist < body.Radius + nearbyAngel.Radius + 5)
            {
                _latchedBodies[body.Id] = nearbyAngel.Id;
                body.Velocity = nearbyAngel.Velocity;
            }
        }
        else
        {
            if (_latchedBodies.ContainsKey(body.Id))
            {
                _latchedBodies.Remove(body.Id);
            }

            foreach (var other in world.Bodies)
            {
                if (body == other || other.BodyType == BodyType.Molly || other.BodyType == BodyType.Angel) continue;

                var dist = (float)Vector2.Distance(body.Position, other.Position);
                if (dist < body.Radius + other.Radius)
                {
                    TriggerExplosion(body, world);
                    return;
                }
            }
        }
    }

    private void TriggerExplosion(RigidBody body, PhysicsWorld world)
    {
        body.HasExploded = true;
        world.ForceManager.Explosion.Trigger(body.Position);

        var rand = new Random();
        for (int i = 0; i < 10; i++)
        {
            float angle = i * (float)System.Math.PI * 2 / 10;
            float speed = 250f + rand.Next(150);
            var vel = new Vector2(
                (float)System.Math.Cos(angle) * speed,
                (float)System.Math.Sin(angle) * speed);

            var debris = world.CreateBody(body.Position, body.Radius * 0.2, body.Mass * 0.1, 0.5);
            debris.Velocity = vel;
            debris.BodyType = BodyType.Fire;
        }

        foreach (var other2 in world.Bodies)
        {
            if (body == other2) continue;
            var dir = (other2.Position - body.Position).Normalized;
            var dist = (float)Vector2.Distance(body.Position, other2.Position);
            var force = ExplosionForce / (dist * dist + 1);
            other2.ApplyImpulse(dir * force * 30);
        }

        try { world.RemoveBody(body); } catch { }
    }
}