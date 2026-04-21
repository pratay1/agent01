using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

public abstract class BodyBehavior
{
    public abstract BodyType Type { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string ColorHex { get; }
    public abstract double DefaultRadius { get; }
    public abstract double DefaultMass { get; }
    public abstract double DefaultRestitution { get; }
    
    public virtual void OnCreate(RigidBody body) { }
    public virtual void OnUpdate(RigidBody body, double dt, PhysicsWorld world) { }
    public virtual void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world) { }
}

public static class BodyBehaviorFactory
{
    private static readonly Dictionary<BodyType, BodyBehavior> _behaviors = new()
    {
        { BodyType.Normal, new NormalBehavior() },
        { BodyType.Bouncy, new BouncyBehavior() },
        { BodyType.Heavy, new HeavyBehavior() },
        { BodyType.Explosive, new ExplosiveBehavior() },
        { BodyType.Repulsor, new RepulsorBehavior() },
        { BodyType.GravityWell, new GravityWellBehavior() },
        { BodyType.AntiGravity, new AntiGravityBehavior() },
        { BodyType.Freezer, new FreezerBehavior() },
        { BodyType.Turbo, new TurboBehavior() },
        { BodyType.Phantom, new PhantomBehavior() },
        { BodyType.Spike, new SpikeBehavior() },
        { BodyType.Glue, new GlueBehavior() },
        { BodyType.Plasma, new PlasmaBehavior() },
        { BodyType.BlackHole, new BlackHoleBehavior() },
        { BodyType.Lightning, new LightningBehavior() },
        { BodyType.Fire, new FireBehavior() },
        { BodyType.Angel, new AngelBehavior() },
        { BodyType.Molly, new MollyBehavior() }
    };

    public static BodyBehavior Get(BodyType type) => _behaviors[type];
    public static BodyBehavior Get(string name) => _behaviors.Values.FirstOrDefault(b => b.Name == name) ?? _behaviors[BodyType.Normal];
}