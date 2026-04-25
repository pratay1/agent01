using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace PhysicsSandbox.Behaviors;

public class ExplosiveBehavior : BodyBehavior
{
    private const double DEFAULT_BLAST_RADIUS = 60.0;
    private const double DEFAULT_BLAST_FORCE = 8000.0;
    private const int DEFAULT_DEBRIS_COUNT = 8;
    private const double MIN_DEBRIS_SPEED = 150.0;
    private const double MAX_DEBRIS_SPEED = 400.0;
    private const double CHAIN_RADIUS = 60.0;
    private const int MAX_CHAIN = 3;
    private const int MAX_SPAWN_DEBRIS = 50;

    public enum ExplosionState { Armed, FuseBurning, Detonated, Cooldown, ChainReacting, Disarmed }
    public enum ExplosiveType { Firecracker, Dynamite, C4, Grenade, Nuke, Molotov, TNT, Flashbang }

    public class ExplosiveProfile
    {
        public string Name = "";
        public double BlastRadius = 60.0;
        public double BlastForce = 8000.0;
        public int DebrisCount = 8;
        public double FuseTime = 0.0;
        public bool ChainReaction = false;
        public BodyType DebrisType = BodyType.Fire;
        public string ColorHex = "#E53935";
    }

    private static readonly Dictionary<ExplosiveType, ExplosiveProfile> _profiles = new()
    {
        { ExplosiveType.Firecracker, new() { Name = "Firecracker", BlastRadius = 50.0, BlastForce = 5000.0, DebrisCount = 6, FuseTime = 1.5, ChainReaction = false, DebrisType = BodyType.Fire, ColorHex = "#FFD700" } },
        { ExplosiveType.Dynamite, new() { Name = "Dynamite", BlastRadius = 120.0, BlastForce = 12000.0, DebrisCount = 12, FuseTime = 3.0, ChainReaction = true, DebrisType = BodyType.Fire, ColorHex = "#8B4513" } },
        { ExplosiveType.C4, new() { Name = "C4", BlastRadius = 200.0, BlastForce = 25000.0, DebrisCount = 20, FuseTime = 5.0, ChainReaction = true, DebrisType = BodyType.Fire, ColorHex = "#FFA500" } },
        { ExplosiveType.Grenade, new() { Name = "Grenade", BlastRadius = 80.0, BlastForce = 18000.0, DebrisCount = 8, FuseTime = 2.0, ChainReaction = false, DebrisType = BodyType.Normal, ColorHex = "#708090" } },
        { ExplosiveType.Nuke, new() { Name = "Nuke", BlastRadius = 500.0, BlastForce = 100000.0, DebrisCount = 50, FuseTime = 10.0, ChainReaction = true, DebrisType = BodyType.Fire, ColorHex = "#FFFF00" } },
        { ExplosiveType.Molotov, new() { Name = "Molotov", BlastRadius = 60.0, BlastForce = 3000.0, DebrisCount = 4, FuseTime = 1.0, ChainReaction = false, DebrisType = BodyType.Fire, ColorHex = "#FF0000" } },
        { ExplosiveType.TNT, new() { Name = "TNT", BlastRadius = 180.0, BlastForce = 22000.0, DebrisCount = 18, FuseTime = 4.0, ChainReaction = true, DebrisType = BodyType.Fire, ColorHex = "#FFA500" } },
        { ExplosiveType.Flashbang, new() { Name = "Flashbang", BlastRadius = 100.0, BlastForce = 8000.0, DebrisCount = 4, FuseTime = 1.5, ChainReaction = false, DebrisType = BodyType.Normal, ColorHex = "#FFFFFF" } }
    };

    private ExplosiveType _type = ExplosiveType.Firecracker;
    private ExplosiveProfile _profile = _profiles[ExplosiveType.Firecracker];
    private ExplosionState _state = ExplosionState.Armed;
    private double _fuseTimeRemaining = 0.0;
    private int _totalExplosions = 0;
    private int _debrisSpawned = 0;
    private int _bodiesAffected = 0;
    private double _peakForce = 0.0;
    private bool _hasDetonated = false;
    private bool _isArmed = true;
    private double _stateTimer = 0.0;
    private Vector2 _detonationPos = Vector2.Zero;
    private bool _enableDebris = true;
    private bool _enableChain = true;
    private readonly List<Vector2> _debrisPositions = new();

    public override BodyType Type => BodyType.Explosive;
    public override string Name => "Explosive";
    public override string Description => "Explodes on contact or fuse";
    public override string ColorHex => _profile.ColorHex;
    public override double DefaultRadius => 20;
    public override double DefaultMass => 8;
    public override double DefaultRestitution => 0.4;

    public ExplosiveBehavior() : this(ExplosiveType.Firecracker) { }
    public ExplosiveBehavior(ExplosiveType type)
    {
        _type = type;
        _profile = _profiles[type];
        _fuseTimeRemaining = _profile.FuseTime;
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        body.Restitution = DefaultRestitution;
        body.Mass = DefaultMass;
        body.Radius = DefaultRadius;
        _state = _fuseTimeRemaining > 0 ? ExplosionState.FuseBurning : ExplosionState.Armed;
    }

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.IsStatic || body.IsFrozen || _hasDetonated) return;
        UpdateStateMachine(body, dt);
        UpdateFuseTimer(body, dt);
    }

    private void UpdateStateMachine(RigidBody body, double dt)
    {
        _stateTimer += dt;
        switch (_state)
        {
            case ExplosionState.Armed:
                if (_fuseTimeRemaining > 0)
                    _state = ExplosionState.FuseBurning;
                break;
            case ExplosionState.FuseBurning:
                if (_fuseTimeRemaining <= 0)
                    Detonate(body, null);
                break;
            case ExplosionState.Detonated:
                if (_stateTimer > 1.0)
                    _state = ExplosionState.Cooldown;
                break;
            case ExplosionState.ChainReacting:
                if (_stateTimer > 0.5)
                    _state = ExplosionState.Detonated;
                break;
        }
    }

    private void UpdateFuseTimer(RigidBody body, double dt)
    {
        if (!_isArmed || _fuseTimeRemaining <= 0) return;
        _fuseTimeRemaining -= dt;
        if (_fuseTimeRemaining < 0) _fuseTimeRemaining = 0;
    }

    private void Detonate(RigidBody body, PhysicsWorld world)
    {
        if (_hasDetonated) return;
        _hasDetonated = true;
        _detonationPos = body.Position;
        _totalExplosions++;

        double radius = _profile.BlastRadius;
        double force = _profile.BlastForce;

        if (world != null)
        {
            ApplyBlast(body, world, radius, force);
            SpawnDebris(body, world);
            if (_profile.ChainReaction)
                TriggerChainReaction(body, world);
        }

        _state = ExplosionState.Detonated;
        _stateTimer = 0.0;
    }

    private void ApplyBlast(RigidBody source, PhysicsWorld world, double radius, double force)
    {
        foreach (var other in SpatialQuery(source.Position, radius, world))
        {
            if (other == source || other.IsStatic) continue;
            Vector2 dir = (other.Position - source.Position).Normalized;
            double dist = Vector2.Distance(source.Position, other.Position);
            double falloff = 1.0 - Math.Pow(dist / radius, 2.0);
            double appliedForce = force * falloff;
            if (appliedForce < 1.0) continue;
            other.ApplyImpulse(dir * appliedForce);
            _bodiesAffected++;
            if (appliedForce > _peakForce) _peakForce = appliedForce;
            if (other.BodyType == BodyType.Explosive && other.Behavior is ExplosiveBehavior exp)
                exp.TriggerChainReaction(source, world);
        }
    }

    private void SpawnDebris(RigidBody source, PhysicsWorld world)
    {
        if (!_enableDebris) return;
        int count = _profile.DebrisCount;
        double mass = source.Mass * 0.15;
        double radius = source.Radius * 0.25;
        for (int i = 0; i < count; i++)
        {
            if (_debrisSpawned >= MAX_SPAWN_DEBRIS) break;
            double angle = Random.Shared.NextDouble() * Math.PI * 2;
            double speed = MIN_DEBRIS_SPEED + Random.Shared.NextDouble() * (MAX_DEBRIS_SPEED - MIN_DEBRIS_SPEED);
            var vel = new Vector2(Math.Cos(angle) * speed, Math.Sin(angle) * speed);
            var debris = world.CreateBody(source.Position, radius, mass, 0.5, _profile.DebrisType);
            debris.Velocity = vel;
            _debrisSpawned++;
            _debrisPositions.Add(debris.Position);
        }
    }

    private void TriggerChainReaction(RigidBody body, PhysicsWorld world)
    {
        if (!_profile.ChainReaction || !_enableChain) return;
        int chainCount = 0;
        foreach (var other in SpatialQuery(body.Position, CHAIN_RADIUS, world))
        {
            if (chainCount >= MAX_CHAIN) break;
            if (other == body || other.IsStatic) continue;
            if (other.Behavior is ExplosiveBehavior exp && !exp._hasDetonated)
            {
                exp.Detonate(other, world);
                chainCount++;
            }
        }
    }

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        if (_hasDetonated || !_isArmed) return;
        if (_state == ExplosionState.Armed || _state == ExplosionState.FuseBurning)
            Detonate(body, world);
        SpawnFireDebrisOnCollision(body, world);
    }

    private void SpawnFireDebrisOnCollision(RigidBody source, PhysicsWorld world)
    {
        if (!_enableDebris) return;
        const int FIRE_BODY_COUNT = 10;
        const double FIRE_DEBRIS_SPEED = 200.0;
        const double FIRE_DEBRIS_RADIUS = 6.0;
        const double FIRE_DEBRIS_MASS = 2.0;

        for (int i = 0; i < FIRE_BODY_COUNT; i++)
        {
            if (_debrisSpawned >= MAX_SPAWN_DEBRIS) break;
            double angle = Random.Shared.NextDouble() * Math.PI * 2;
            double speed = FIRE_DEBRIS_SPEED * (0.5 + Random.Shared.NextDouble() * 0.5);
            var velocity = new Vector2(Math.Cos(angle) * speed, Math.Sin(angle) * speed);
            var fireDebris = world.CreateBody(source.Position, FIRE_DEBRIS_RADIUS, FIRE_DEBRIS_MASS, 0.3, BodyType.Fire);
            fireDebris.Velocity = velocity;
            _debrisSpawned++;
        }
    }

    public ExplosiveType GetType() => _type;
    public bool HasDetonated() => _hasDetonated;
    public int GetExplosionCount() => _totalExplosions;
    public int GetDebrisSpawned() => _debrisSpawned;
}