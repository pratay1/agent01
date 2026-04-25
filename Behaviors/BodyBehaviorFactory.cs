using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Behaviors;

/// <summary>
/// Factory for creating behavior instances based on body type.
/// </summary>
public static class BodyBehaviorFactory
{
    /// <summary>
    /// Creates a new behavior instance for the given body type.
    /// </summary>
    public static BodyBehavior Get(BodyType type)
    {
        return type switch
        {
            BodyType.Normal => new NormalBehavior(),
            BodyType.Bouncy => new BouncyBehavior(),
            BodyType.Heavy => new HeavyBehavior(),
            BodyType.Explosive => new ExplosiveBehavior(),
            BodyType.Repulsor => new RepulsorBehavior(),
            BodyType.GravityWell => new GravityWellBehavior(),
            BodyType.Turbo => new TurboBehavior(),
            BodyType.Phantom => new PhantomBehavior(),
            BodyType.Spike => new SpikeBehavior(),
            BodyType.Glue => new GlueBehavior(),
            BodyType.Plasma => new PlasmaBehavior(),
            BodyType.BlackHole => new BlackHoleBehavior(),
            BodyType.Lightning => new LightningBehavior(),
            BodyType.Fire => new FireBehavior(),
            BodyType.Angel => new AngelBehavior(),
            BodyType.Molly => new MollyBehavior(),
            _ => new NormalBehavior()
        };
    }
}
