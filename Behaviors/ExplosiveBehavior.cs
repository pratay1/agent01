using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Diagnostics;

namespace PhysicsSandbox.Behaviors;

public class ExplosiveBehavior : BodyBehavior
{
    #region Constants & Tunable Parameters

private const double DEFAULT_BLAST_RADIUS = 60.0;
    private const double DEFAULT_BLAST_FORCE = 8000.0;
    private const int DEFAULT_DEBRIS_COUNT = 8;
    private const double DEFAULT_FUSE_TIME = 0.0;
    private const double MIN_DEBRIS_SPEED = 150.0;
    private const double MAX_DEBRIS_SPEED = 400.0;
    private const int MAX_CHAIN_REACTIONS = 3;
    private const double CHAIN_REACTION_RADIUS = 60.0;
    private const double BLAST_FALLOFF_EXPONENT = 2.0;
    private const int MAX_DEBRIS_TRACKING = 100;
    private const double SHOCKWAVE_SPEED = 300.0;
    private const double FUSE_HISS_INTERVAL = 0.5;
    private const double EXPLOSION_COOLDOWN = 1.0;
    private const int MAX_SPAWN_DEBRIS = 50;
    private const double DEBRIS_MASS_MULTIPLIER = 0.15;
    private const double DEBRIS_RADIUS_MULTIPLIER = 0.25;
    private const double MAX_FUSE_TIME = 60.0;
    private const double MIN_FUSE_TIME = 0.0;

    #endregion

    #region Explosive State Machine

    public enum ExplosionState
    {
        Armed,
        FuseBurning,
        Detonated,
        Cooldown,
        ChainReacting,
        Disarmed
    }

    public enum ExplosiveType
    {
        Firecracker,
        Dynamite,
        C4,
        Grenade,
        Nuke,
        Molotov,
        TNT,
        Flashbang,
        Custom
    }

    private ExplosionState _currentState = ExplosionState.Armed;
    private ExplosionState _previousState = ExplosionState.Armed;
    private double _stateTimer = 0.0;
    private int _stateFrameCount = 0;

    private class ExplosiveStateMachine
    {
        public ExplosionState CurrentState { get; set; } = ExplosionState.Armed;
        public ExplosionState PreviousState { get; set; } = ExplosionState.Armed;
        public double FuseTimer { get; set; } = 0.0;
        public int ChainReactionCount { get; set; } = 0;
        public double LastDetonationTime { get; set; } = 0.0;

        public void TransitionTo(ExplosionState newState, double currentTime)
        {
            PreviousState = CurrentState;
            CurrentState = newState;
            StateTimer = 0.0;
            if (newState == ExplosionState.Detonated)
                LastDetonationTime = currentTime;
        }

        public double StateTimer { get; set; }
    }

    private readonly ExplosiveStateMachine _stateMachine = new();

    #endregion

    #region Explosive Presets

    public class ExplosiveProfile
    {
        public string Name { get; set; } = "";
        public double BlastRadius { get; set; } = DEFAULT_BLAST_RADIUS;
        public double BlastForce { get; set; } = DEFAULT_BLAST_FORCE;
        public int DebrisCount { get; set; } = DEFAULT_DEBRIS_COUNT;
        public double FuseTime { get; set; } = DEFAULT_FUSE_TIME;
        public bool ChainReactionEnabled { get; set; } = false;
        public BodyType DebrisType { get; set; } = BodyType.Fire;
        public string ColorHex { get; set; } = "#E53935";
        public double ShockwaveStrength { get; set; } = 1.0;
        public double DebrisSpread { get; set; } = 360.0;
        public bool CausesFire { get; set; } = false;
        public double ParticleLifetime { get; set; } = 2.0;
        public double SoundPitchMin { get; set; } = 0.8;
        public double SoundPitchMax { get; set; } = 1.2;
        public int ParticleCount { get; set; } = 10;
    }

    private static readonly Dictionary<ExplosiveType, ExplosiveProfile> _explosivePresets = new()
    {
        {
            ExplosiveType.Firecracker, new ExplosiveProfile
            {
                Name = "Firecracker",
                BlastRadius = 50.0,
                BlastForce = 5000.0,
                DebrisCount = 6,
                FuseTime = 1.5,
                ChainReactionEnabled = false,
                DebrisType = BodyType.Fire,
                ColorHex = "#FFD700",
                ShockwaveStrength = 0.3,
                DebrisSpread = 180.0,
                CausesFire = false,
                ParticleLifetime = 1.0,
                ParticleCount = 5
            }
        },
        {
            ExplosiveType.Dynamite, new ExplosiveProfile
            {
                Name = "Dynamite",
                BlastRadius = 120.0,
                BlastForce = 12000.0,
                DebrisCount = 12,
                FuseTime = 3.0,
                ChainReactionEnabled = true,
                DebrisType = BodyType.Fire,
                ColorHex = "#8B4513",
                ShockwaveStrength = 0.8,
                DebrisSpread = 360.0,
                CausesFire = true,
                ParticleLifetime = 2.5,
                ParticleCount = 15
            }
        },
        {
            ExplosiveType.C4, new ExplosiveProfile
            {
                Name = "C4",
                BlastRadius = 200.0,
                BlastForce = 25000.0,
                DebrisCount = 20,
                FuseTime = 5.0,
                ChainReactionEnabled = true,
                DebrisType = BodyType.Fire,
                ColorHex = "#FFA500",
                ShockwaveStrength = 1.2,
                DebrisSpread = 360.0,
                CausesFire = true,
                ParticleLifetime = 3.0,
                ParticleCount = 25
            }
        },
        {
            ExplosiveType.Grenade, new ExplosiveProfile
            {
                Name = "Grenade",
                BlastRadius = 80.0,
                BlastForce = 18000.0,
                DebrisCount = 8,
                FuseTime = 2.0,
                ChainReactionEnabled = false,
                DebrisType = BodyType.Normal,
                ColorHex = "#708090",
                ShockwaveStrength = 1.0,
                DebrisSpread = 360.0,
                CausesFire = false,
                ParticleLifetime = 1.5,
                ParticleCount = 10
            }
        },
        {
            ExplosiveType.Nuke, new ExplosiveProfile
            {
                Name = "Nuke",
                BlastRadius = 500.0,
                BlastForce = 100000.0,
                DebrisCount = 50,
                FuseTime = 10.0,
                ChainReactionEnabled = true,
                DebrisType = BodyType.Fire,
                ColorHex = "#FFFF00",
                ShockwaveStrength = 5.0,
                DebrisSpread = 360.0,
                CausesFire = true,
                ParticleLifetime = 10.0,
                ParticleCount = 100
            }
        },
        {
            ExplosiveType.Molotov, new ExplosiveProfile
            {
                Name = "Molotov Cocktail",
                BlastRadius = 60.0,
                BlastForce = 3000.0,
                DebrisCount = 4,
                FuseTime = 1.0,
                ChainReactionEnabled = false,
                DebrisType = BodyType.Fire,
                ColorHex = "#FF0000",
                ShockwaveStrength = 0.2,
                DebrisSpread = 90.0,
                CausesFire = true,
                ParticleLifetime = 4.0,
                ParticleCount = 8
            }
        },
        {
            ExplosiveType.TNT, new ExplosiveProfile
            {
                Name = "TNT",
                BlastRadius = 180.0,
                BlastForce = 22000.0,
                DebrisCount = 18,
                FuseTime = 4.0,
                ChainReactionEnabled = true,
                DebrisType = BodyType.Fire,
                ColorHex = "#FFA500",
                ShockwaveStrength = 1.1,
                DebrisSpread = 360.0,
                CausesFire = true,
                ParticleLifetime = 2.8,
                ParticleCount = 20
            }
        },
        {
            ExplosiveType.Flashbang, new ExplosiveProfile
            {
                Name = "Flashbang",
                BlastRadius = 100.0,
                BlastForce = 8000.0,
                DebrisCount = 4,
                FuseTime = 1.5,
                ChainReactionEnabled = false,
                DebrisType = BodyType.Normal,
                ColorHex = "#FFFFFF",
                ShockwaveStrength = 0.5,
                DebrisSpread = 360.0,
                CausesFire = false,
                ParticleLifetime = 0.5,
                ParticleCount = 3
            }
        }
    };

    #endregion

    #region Instance State & Configuration

    private ExplosiveType _currentType = ExplosiveType.Firecracker;
    private ExplosiveProfile _activeProfile = _explosivePresets[ExplosiveType.Firecracker];
    private double _customBlastRadius = DEFAULT_BLAST_RADIUS;
    private double _customBlastForce = DEFAULT_BLAST_FORCE;
    private double _customFuseTime = DEFAULT_FUSE_TIME;
    private int _customDebrisCount = DEFAULT_DEBRIS_COUNT;

    private double _fuseTimeRemaining = 0.0;
    private bool _isArmed = true;
    private bool _hasDetonated = false;
    private double _detonationTime = 0.0;
    private int _totalExplosions = 0;
    private int _totalDebrisSpawned = 0;
    private int _totalBodiesAffected = 0;
    private double _peakBlastForce = 0.0;
    private Vector2 _detonationPosition = Vector2.Zero;

    private readonly List<Vector2> _debrisPositions = new();
    private readonly Dictionary<string, double> _achievements = new();

    private bool _enableChainReactions = true;
    private bool _enableShockwave = true;
    private bool _enableDebris = true;
    private bool _enableParticles = true;
    private bool _enableSounds = true;

    private readonly Stopwatch _updateStopwatch = new();
    private double _lastFuseHissTime = 0.0;

    private readonly Queue<ParticleEffectRequest> _pendingParticles = new();
    private readonly Queue<SoundEffectRequest> _pendingSounds = new();

    private class ParticleEffectRequest
    {
        public ParticleEffectType Type { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public double Intensity { get; set; }
    }

    private class SoundEffectRequest
    {
        public SoundEffectType Type { get; set; }
        public Vector2 Position { get; set; }
        public double Volume { get; set; }
        public double Pitch { get; set; }
    }

    public enum ParticleEffectType
    {
        ExplosionFireball,
        Smoke,
        Shrapnel,
        Shockwave,
        Spark,
        FireTrail
    }

    public enum SoundEffectType
    {
        FuseHiss,
        ExplosionSmall,
        ExplosionLarge,
        ExplosionNuke,
        ChainReaction,
        DebrisHit
    }

    public enum DebrisMaterial
    {
        Fire,
        Steel,
        Wood,
        Glass,
        Plastic
    }

    private DebrisMaterial _debrisMaterial = DebrisMaterial.Fire;

    #endregion

    #region Behavior Properties (Overrides)

    public override BodyType Type => BodyType.Explosive;
    public override string Name => "Explosive";
    public override string Description => "Explodes on contact or after fuse timer, spawns debris and affects nearby bodies";
    public override string ColorHex => _activeProfile.ColorHex;
    public override double DefaultRadius => 20;
    public override double DefaultMass => 8;
    public override double DefaultRestitution => 0.4;

    #endregion

    #region Constructors & Initialization

    public ExplosiveBehavior() : this(ExplosiveType.Firecracker) { }

    public ExplosiveBehavior(ExplosiveType type)
    {
        _currentType = type;
        _activeProfile = _explosivePresets[type];
        _fuseTimeRemaining = _activeProfile.FuseTime;
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);

        body.Restitution = DefaultRestitution;
        body.Mass = DefaultMass;
        body.Radius = DefaultRadius;

        _fuseTimeRemaining = _activeProfile.FuseTime;
        _stateMachine.CurrentState = _fuseTimeRemaining > 0 ? ExplosionState.FuseBurning : ExplosionState.Armed;

        LogDebug(body, $"ExplosiveBehavior initialized: Type={_currentType}, BlastRadius={_activeProfile.BlastRadius:F2}, FuseTime={_fuseTimeRemaining:F2}s");
    }

    #endregion

    #region Main Update Loop

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        _updateStopwatch.Restart();

        try
        {
            if (body.IsStatic || body.IsFrozen || _hasDetonated)
                return;

            RaisePreUpdate(body, dt);

            UpdateStateMachine(body, dt, world);
            UpdateFuseTimer(body, dt);
            CheckCollisionDetonation(body, dt, world);
            ProcessChainReactions(body, dt, world);
            UpdateShockwave(body, dt, world);
            ProcessPendingEffects(body);
            TrackStatistics(body, dt);
            UpdateAchievementTracking();

            RaisePostUpdate(body, dt);
        }
        finally
        {
            _updateStopwatch.Stop();
            RecordPerformanceMetric("OnUpdate", _updateStopwatch.Elapsed.TotalMilliseconds);
        }
    }

    #endregion

    #region State Machine Update

    private void UpdateStateMachine(RigidBody body, double dt, PhysicsWorld world)
    {
        _stateMachine.StateTimer += dt;
        _stateTimer += dt;

        switch (_stateMachine.CurrentState)
        {
            case ExplosionState.Armed:
                if (_fuseTimeRemaining > 0)
                    _stateMachine.TransitionTo(ExplosionState.FuseBurning, 0.0);
                break;

            case ExplosionState.FuseBurning:
                if (_fuseTimeRemaining <= 0)
                    Detonate(body, world);
                if (_stateMachine.StateTimer >= FUSE_HISS_INTERVAL && _enableSounds)
                {
                    TriggerSoundEffect(SoundEffectType.FuseHiss, body.Position, 0.3, 1.0);
                    _stateMachine.StateTimer = 0.0;
                }
                break;

            case ExplosionState.Detonated:
                if (_stateMachine.StateTimer >= EXPLOSION_COOLDOWN)
                    _stateMachine.TransitionTo(ExplosionState.Cooldown, 0.0);
                break;

            case ExplosionState.Cooldown:
                break;

            case ExplosionState.ChainReacting:
                if (_stateMachine.StateTimer > 0.5)
                    _stateMachine.TransitionTo(ExplosionState.Detonated, 0.0);
                break;

            case ExplosionState.Disarmed:
                break;
        }

        _currentState = _stateMachine.CurrentState;
        _stateFrameCount++;
    }

    #endregion

    #region Explosion Physics

    private void Detonate(RigidBody body, PhysicsWorld world)
    {
        if (_hasDetonated) return;

        _hasDetonated = true;
        _detonationTime = 0.0;
        _detonationPosition = body.Position;
        _totalExplosions++;

        double blastRadius = _activeProfile.BlastRadius;
        double blastForce = _activeProfile.BlastForce;

        ApplyBlastToNearbyBodies(body, world, blastRadius, blastForce);
        if (_enableDebris) SpawnDebris(body, world);
        if (_enableShockwave) TriggerShockwave(body.Position, blastRadius, world);
        if (_enableParticles) TriggerExplosionParticles(body.Position, blastForce);
        if (_enableSounds) TriggerExplosionSound(blastForce);

        _stateMachine.TransitionTo(ExplosionState.Detonated, 0.0);

        try { world.RemoveBody(body); } catch { }

        LogDebug(body, $"Detonated: BlastRadius={blastRadius:F2}, Force={blastForce:F2}, Debris={_activeProfile.DebrisCount}");
    }

    private void ApplyBlastToNearbyBodies(RigidBody source, PhysicsWorld world, double radius, double force)
    {
        foreach (var other in SpatialQuery(source.Position, radius, world))
        {
            if (other == source || other.IsStatic) continue;

            Vector2 dir = (other.Position - source.Position).Normalized;
            double dist = Vector2.Distance(source.Position, other.Position);
            double falloff = 1.0 - Math.Pow(dist / radius, BLAST_FALLOFF_EXPONENT);
            double appliedForce = force * falloff;

            if (appliedForce < 1.0) continue;

            other.ApplyImpulse(dir * appliedForce);
            _totalBodiesAffected++;

            if (appliedForce > _peakBlastForce)
                _peakBlastForce = appliedForce;

            if (other.BodyType == BodyType.Explosive && other.Behavior is ExplosiveBehavior otherExplosive)
                otherExplosive.TriggerChainReaction(world, other);
        }
    }

    private void SpawnDebris(RigidBody source, PhysicsWorld world)
    {
        int debrisCount = _activeProfile.DebrisCount;
        double debrisMass = source.Mass * DEBRIS_MASS_MULTIPLIER * (_debrisMaterial == DebrisMaterial.Steel ? 2.0 : 1.0);
        double debrisRadius = source.Radius * DEBRIS_RADIUS_MULTIPLIER;
        double spread = _activeProfile.DebrisSpread * Math.PI / 180.0;

        for (int i = 0; i < debrisCount; i++)
        {
            if (_totalDebrisSpawned >= MAX_SPAWN_DEBRIS) break;

            double angle = Random.Shared.NextDouble() * spread - spread / 2;
            double speed = MIN_DEBRIS_SPEED + Random.Shared.NextDouble() * (MAX_DEBRIS_SPEED - MIN_DEBRIS_SPEED);
            var vel = new Vector2(Math.Cos(angle) * speed, Math.Sin(angle) * speed);

            var debris = world.CreateBody(source.Position, debrisRadius, debrisMass, 0.5, _activeProfile.DebrisType);
            debris.Velocity = vel;
            _totalDebrisSpawned++;

            if (_debrisPositions.Count >= MAX_DEBRIS_TRACKING)
                _debrisPositions.RemoveAt(0);
            _debrisPositions.Add(debris.Position);
        }
    }

    private void TriggerShockwave(Vector2 position, double radius, PhysicsWorld world)
    {
        double shockwaveForce = _activeProfile.ShockwaveStrength * 1000.0;
        foreach (var other in SpatialQuery(position, radius, world))
        {
            if (other == null || other.IsStatic) continue;
            Vector2 dir = (other.Position - position).Normalized;
            other.ApplyForce(dir * shockwaveForce * _activeProfile.ShockwaveStrength);
        }
    }

    private void UpdateShockwave(RigidBody body, double dt, PhysicsWorld world)
    {
        if (!_enableShockwave || !_hasDetonated) return;

        double shockwaveRadius = _activeProfile.BlastRadius * (_detonationTime / EXPLOSION_COOLDOWN);
        if (shockwaveRadius > _activeProfile.BlastRadius)
            shockwaveRadius = _activeProfile.BlastRadius;

        foreach (var other in SpatialQuery(_detonationPosition, shockwaveRadius, world))
        {
            if (other == null || other.IsStatic) continue;
            Vector2 dir = (other.Position - _detonationPosition).Normalized;
            double dist = Vector2.Distance(_detonationPosition, other.Position);
            double force = _activeProfile.ShockwaveStrength * 100.0 * (1.0 - dist / shockwaveRadius);
            other.ApplyForce(dir * force);
        }
    }

    private double CalculateBlastRadius(double force)
    {
        return Math.Sqrt(force / (Math.PI * 1000.0));
    }

    private double CalculateBlastForce(double radius)
    {
        return Math.PI * radius * radius * 1000.0;
    }

    #endregion

    #region Collision Handling

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        if (_hasDetonated || !_isArmed) return;

        if (_currentState == ExplosionState.Armed || _currentState == ExplosionState.FuseBurning)
            Detonate(body, world);

        SpawnFireDebrisOnCollision(body, other, world);

        RaiseCollision(body, other);
    }

    private void SpawnFireDebrisOnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        const int FIRE_BODY_COUNT = 10;
        const double FIRE_DEBRIS_SPEED = 200.0;
        const double FIRE_DEBRIS_RADIUS = 6.0;
        const double FIRE_DEBRIS_MASS = 2.0;

        for (int i = 0; i < FIRE_BODY_COUNT; i++)
        {
            double angle = Random.Shared.NextDouble() * Math.PI * 2;
            double speed = FIRE_DEBRIS_SPEED * (0.5 + Random.Shared.NextDouble() * 0.5);
            var velocity = new Vector2((float)(Math.Cos(angle) * speed), (float)(Math.Sin(angle) * speed));

            var fireDebris = world.CreateBody(body.Position, FIRE_DEBRIS_RADIUS, FIRE_DEBRIS_MASS, 0.3, BodyType.Fire);
            fireDebris.Velocity = velocity;
        }
    }

    private void CheckCollisionDetonation(RigidBody body, double dt, PhysicsWorld world)
    {
        if (_hasDetonated || !_isArmed) return;

        var bodies = world.Bodies;
        for (int i = 0; i < bodies.Count; i++)
        {
            var other = bodies[i];
            if (body == other) continue;
            if (Vector2.Distance(body.Position, other.Position) < body.Radius + other.Radius)
            {
                Detonate(body, world);
                break;
            }
        }
    }

    #endregion

    #region Fuse System

    private void UpdateFuseTimer(RigidBody body, double dt)
    {
        if (!_isArmed || _fuseTimeRemaining <= 0) return;

        _fuseTimeRemaining -= dt;
        if (_fuseTimeRemaining < 0) _fuseTimeRemaining = 0;
    }

    private double ValidateFuseTime(double fuseTime)
    {
        return Math.Clamp(fuseTime, MIN_FUSE_TIME, MAX_FUSE_TIME);
    }

    public void Arm(double fuseTime = -1)
    {
        _isArmed = true;
        _fuseTimeRemaining = fuseTime >= 0 ? ValidateFuseTime(fuseTime) : _activeProfile.FuseTime;
        _stateMachine.TransitionTo(_fuseTimeRemaining > 0 ? ExplosionState.FuseBurning : ExplosionState.Armed, 0.0);
    }

    public void Disarm()
    {
        _isArmed = false;
        _stateMachine.TransitionTo(ExplosionState.Disarmed, 0.0);
    }

    public void TriggerDetonation(PhysicsWorld world)
    {
        if (_hasDetonated) return;
        var body = world.Bodies.FirstOrDefault(b => b.Behavior == this);
        if (body != null) Detonate(body, world);
    }

    public void ExplodeAtPosition(Vector2 position, PhysicsWorld world)
    {
        var tempBody = world.CreateBody(position, DefaultRadius, DefaultMass, DefaultRestitution, BodyType.Explosive);
        tempBody.Behavior = this;
        OnCreate(tempBody);
        Detonate(tempBody, world);
    }

    #endregion

    #region Chain Reaction System

    public void TriggerChainReaction(PhysicsWorld world, RigidBody triggerBody)
    {
        if (!_activeProfile.ChainReactionEnabled || !_enableChainReactions) return;
        if (_stateMachine.ChainReactionCount >= MAX_CHAIN_REACTIONS) return;

        _stateMachine.TransitionTo(ExplosionState.ChainReacting, 0.0);
        _stateMachine.ChainReactionCount++;

        int reactions = 0;
        foreach (var other in SpatialQuery(_detonationPosition, CHAIN_REACTION_RADIUS, world))
        {
            if (other == null || other.IsStatic) continue;
            if (reactions >= MAX_CHAIN_REACTIONS) break;

            if (other.Behavior is ExplosiveBehavior otherExplosive && !otherExplosive.HasDetonated())
            {
                otherExplosive.Detonate(other, world);
                reactions++;
            }
        }

        if (reactions > 0 && _enableSounds)
            TriggerSoundEffect(SoundEffectType.ChainReaction, _detonationPosition, 0.5, 1.2);
    }

    private void ProcessChainReactions(RigidBody body, double dt, PhysicsWorld world)
    {
        if (_stateMachine.CurrentState != ExplosionState.ChainReacting) return;

        if (_stateMachine.StateTimer > 0.5)
            _stateMachine.TransitionTo(ExplosionState.Detonated, 0.0);
    }

    #endregion

    #region Particle & Sound Effects

    private void TriggerExplosionParticles(Vector2 position, double intensity)
    {
        ParticleEffectType type = intensity > 50000 ? ParticleEffectType.ExplosionFireball :
                                 intensity > 10000 ? ParticleEffectType.Shockwave :
                                 ParticleEffectType.Smoke;

        _pendingParticles.Enqueue(new ParticleEffectRequest
        {
            Type = type,
            Position = position,
            Velocity = Vector2.Zero,
            Intensity = intensity / 1000.0
        });

        for (int i = 0; i < _activeProfile.ParticleCount; i++)
        {
            _pendingParticles.Enqueue(new ParticleEffectRequest
            {
                Type = ParticleEffectType.Spark,
                Position = position,
                Velocity = new Vector2((float)(Random.Shared.NextDouble() * 200 - 100), (float)(Random.Shared.NextDouble() * 200 - 100)),
                Intensity = 0.5
            });
        }
    }

    private void TriggerExplosionSound(double intensity)
    {
        SoundEffectType type = intensity > 50000 ? SoundEffectType.ExplosionNuke :
                               intensity > 10000 ? SoundEffectType.ExplosionLarge :
                               SoundEffectType.ExplosionSmall;

        _pendingSounds.Enqueue(new SoundEffectRequest
        {
            Type = type,
            Position = _detonationPosition,
            Volume = Math.Min(1.0, intensity / 50000.0),
            Pitch = 1.0 - intensity / 100000.0
        });
    }

    private void TriggerParticleEffect(ParticleEffectType type, Vector2 position, Vector2 velocity, double intensity)
    {
        _pendingParticles.Enqueue(new ParticleEffectRequest
        {
            Type = type,
            Position = position,
            Velocity = velocity,
            Intensity = intensity
        });
    }

    private void TriggerSoundEffect(SoundEffectType type, Vector2 position, double volume, double pitch)
    {
        _pendingSounds.Enqueue(new SoundEffectRequest
        {
            Type = type,
            Position = position,
            Volume = volume,
            Pitch = pitch
        });
    }

    private void ProcessPendingEffects(RigidBody body)
    {
        while (_pendingParticles.Count > 0)
            _pendingParticles.Dequeue();

        while (_pendingSounds.Count > 0)
            _pendingSounds.Dequeue();
    }

    #endregion

    #region Achievement Tracking

    private void UpdateAchievementTracking()
    {
        if (_totalExplosions >= 1 && !_achievements.ContainsKey("FirstExplosion"))
        {
            _achievements["FirstExplosion"] = 1.0;
            if (_enableSounds)
                TriggerSoundEffect(SoundEffectType.ExplosionSmall, _detonationPosition, 1.0, 1.5);
        }

        if (_stateMachine.ChainReactionCount >= 3 && !_achievements.ContainsKey("ChainMaster"))
        {
            _achievements["ChainMaster"] = 1.0;
            if (_enableSounds)
                TriggerSoundEffect(SoundEffectType.ChainReaction, _detonationPosition, 1.0, 1.8);
        }

        if (_totalDebrisSpawned >= 100 && !_achievements.ContainsKey("DebrisKing"))
        {
            _achievements["DebrisKing"] = 1.0;
        }
    }

    #endregion

    #region Statistics Tracking

    private void TrackStatistics(RigidBody body, double dt)
    {
        if (_hasDetonated)
            _detonationTime += dt;
    }

    public (int TotalExplosions, int TotalDebris, int TotalBodiesAffected, double PeakForce) GetStatistics()
        => (_totalExplosions, _totalDebrisSpawned, _totalBodiesAffected, _peakBlastForce);

    public void ResetStatistics()
    {
        _totalExplosions = 0;
        _totalDebrisSpawned = 0;
        _totalBodiesAffected = 0;
        _peakBlastForce = 0.0;
        _achievements.Clear();
    }

    #endregion

    #region Public API

    public void SetPreset(RigidBody body, ExplosiveType type)
    {
        _currentType = type;
        _activeProfile = _explosivePresets[type];
        _fuseTimeRemaining = _activeProfile.FuseTime;
        _stateMachine.TransitionTo(_fuseTimeRemaining > 0 ? ExplosionState.FuseBurning : ExplosionState.Armed, 0.0);
    }

    public void SetCustomStats(double blastRadius, double blastForce, int debrisCount, double fuseTime)
    {
        _activeProfile.BlastRadius = blastRadius;
        _activeProfile.BlastForce = blastForce;
        _activeProfile.DebrisCount = debrisCount;
        _activeProfile.FuseTime = fuseTime;
        _currentType = ExplosiveType.Custom;
    }

    public void SetBlastRadius(double radius)
    {
        _activeProfile.BlastRadius = radius;
        _currentType = ExplosiveType.Custom;
    }

    public void SetBlastForce(double force)
    {
        _activeProfile.BlastForce = force;
        _currentType = ExplosiveType.Custom;
    }

    public void SetDebrisCount(int count)
    {
        _activeProfile.DebrisCount = count;
        _currentType = ExplosiveType.Custom;
    }

    public void SetDebrisMaterial(DebrisMaterial material)
    {
        _debrisMaterial = material;
        _activeProfile.DebrisType = material switch
        {
            DebrisMaterial.Fire => BodyType.Fire,
            DebrisMaterial.Steel => BodyType.Heavy,
            DebrisMaterial.Wood => BodyType.Normal,
            DebrisMaterial.Glass => BodyType.Normal,
            DebrisMaterial.Plastic => BodyType.Normal,
            _ => BodyType.Fire
        };
    }

    public void SetChainReactionsEnabled(bool enabled) => _enableChainReactions = enabled;
    public void SetShockwaveEnabled(bool enabled) => _enableShockwave = enabled;
    public void SetDebrisEnabled(bool enabled) => _enableDebris = enabled;
    public void SetParticlesEnabled(bool enabled) => _enableParticles = enabled;
    public void SetSoundsEnabled(bool enabled) => _enableSounds = enabled;

    public ExplosiveType GetCurrentType() => _currentType;
    public ExplosionState GetCurrentState() => _currentState;
    public bool HasDetonated() => _hasDetonated;
    public double GetFuseTimeRemaining() => _fuseTimeRemaining;
    public ExplosiveProfile GetActiveProfile() => _activeProfile;
    public IReadOnlyList<Vector2> GetDebrisPositions() => _debrisPositions.AsReadOnly();
    public IReadOnlyDictionary<string, double> GetAchievements() => _achievements;

    public static ExplosiveProfile? GetProfileForType(ExplosiveType type)
    {
        return _explosivePresets.TryGetValue(type, out var profile) ? profile : null;
    }

    public static List<ExplosiveProfile> GetAllPresets()
    {
        return new List<ExplosiveProfile>(_explosivePresets.Values);
    }

    #endregion

    #region Debug Visualization

    protected override void RenderDebugOverlay(RigidBody body, DrawingContext dc)
    {
        if (dc == null || !GlobalConfig.EnableDebugVisualization) return;

        DrawBlastRadiusIndicator(body, dc);
        DrawFuseTimerIndicator(body, dc);
        DrawStateIndicator(body, dc);
        DrawExplosionRadius(dc);
        DrawShockwave(body, dc);
        DrawDebrisPositions(dc);
    }

    private void DrawBlastRadiusIndicator(RigidBody body, DrawingContext dc)
    {
        if (_hasDetonated) return;
        var pen = new Pen(Brushes.Orange, 1.0);
        dc.DrawEllipse(null, pen, new Point(body.Position.X, body.Position.Y), _activeProfile.BlastRadius, _activeProfile.BlastRadius);
    }

    private void DrawFuseTimerIndicator(RigidBody body, DrawingContext dc)
    {
        if (!_isArmed || _fuseTimeRemaining <= 0) return;
        double progress = 1.0 - (_fuseTimeRemaining / _activeProfile.FuseTime);
        var brush = new SolidColorBrush(Color.FromArgb((byte)(progress * 255), 255, 0, 0));
        dc.DrawRectangle(brush, null, new Rect(body.Position.X - 10, body.Position.Y - body.Radius - 15, 20 * progress, 5));
    }

    private void DrawStateIndicator(RigidBody body, DrawingContext dc)
    {
        var stateColors = new Dictionary<ExplosionState, Brush>
        {
            { ExplosionState.Armed, Brushes.Green },
            { ExplosionState.FuseBurning, Brushes.Yellow },
            { ExplosionState.Detonated, Brushes.Red },
            { ExplosionState.Cooldown, Brushes.Gray },
            { ExplosionState.ChainReacting, Brushes.Purple },
            { ExplosionState.Disarmed, Brushes.Black }
        };

        if (stateColors.TryGetValue(_currentState, out var brush))
            dc.DrawEllipse(brush, new Pen(Brushes.Black, 1), new Point(body.Position.X, body.Position.Y), 5, 5);
    }

    private void DrawExplosionRadius(DrawingContext dc)
    {
        if (!_hasDetonated) return;
        var brush = new SolidColorBrush(Color.FromArgb(50, 255, 0, 0));
        dc.DrawEllipse(brush, null, new Point(_detonationPosition.X, _detonationPosition.Y), _activeProfile.BlastRadius, _activeProfile.BlastRadius);
    }

    private void DrawShockwave(RigidBody body, DrawingContext dc)
    {
        if (!_hasDetonated) return;
        double shockwaveRadius = _activeProfile.BlastRadius * (_detonationTime / EXPLOSION_COOLDOWN);
        var pen = new Pen(Brushes.Blue, 1.0);
        dc.DrawEllipse(null, pen, new Point(_detonationPosition.X, _detonationPosition.Y), shockwaveRadius, shockwaveRadius);
    }

    private void DrawDebrisPositions(DrawingContext dc)
    {
        foreach (var pos in _debrisPositions)
            dc.DrawEllipse(Brushes.Red, null, new Point(pos.X, pos.Y), 2, 2);
    }

    #endregion

    #region Serialization Support

    public string SerializeState()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Type:{_currentType}");
        sb.AppendLine($"HasDetonated:{_hasDetonated}");
        sb.AppendLine($"TotalExplosions:{_totalExplosions}");
        sb.AppendLine($"TotalDebris:{_totalDebrisSpawned}");
        sb.AppendLine($"TotalBodiesAffected:{_totalBodiesAffected}");
        sb.AppendLine($"FuseTimeRemaining:{_fuseTimeRemaining}");
        sb.AppendLine($"IsArmed:{_isArmed}");
        sb.AppendLine($"EnableChainReactions:{_enableChainReactions}");
        sb.AppendLine($"CurrentState:{_currentState}");
        sb.AppendLine($"DebrisMaterial:{_debrisMaterial}");
        sb.AppendLine($"Achievements:{_achievements.Count}");
        return sb.ToString();
    }

    public void DeserializeState(string state)
    {
        var lines = state.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(':');
            if (parts.Length != 2) continue;

            try
            {
                switch (parts[0])
                {
                    case "Type":
                        if (Enum.TryParse(parts[1], out ExplosiveType type))
                            _currentType = type;
                        break;
                    case "HasDetonated":
                        _hasDetonated = bool.Parse(parts[1]);
                        break;
                    case "TotalExplosions":
                        _totalExplosions = int.Parse(parts[1]);
                        break;
                    case "TotalDebris":
                        _totalDebrisSpawned = int.Parse(parts[1]);
                        break;
                    case "TotalBodiesAffected":
                        _totalBodiesAffected = int.Parse(parts[1]);
                        break;
                    case "FuseTimeRemaining":
                        _fuseTimeRemaining = double.Parse(parts[1]);
                        break;
                    case "IsArmed":
                        _isArmed = bool.Parse(parts[1]);
                        break;
                    case "EnableChainReactions":
                        _enableChainReactions = bool.Parse(parts[1]);
                        break;
                    case "CurrentState":
                        if (Enum.TryParse(parts[1], out ExplosionState parsedState))
                            _currentState = parsedState;
                        break;
                    case "DebrisMaterial":
                        if (Enum.TryParse(parts[1], out DebrisMaterial material))
                            _debrisMaterial = material;
                        break;
                }
            }
            catch { }
        }
    }

    #endregion

    #region Performance Tracking

    public class ExplosivePerformanceCounters
    {
        public int TotalExplosions { get; set; }
        public int TotalDebrisSpawned { get; set; }
        public double TotalBlastForce { get; set; }
        public int ActiveChainReactions { get; set; }
        public int TotalParticlesSpawned { get; set; }
        public int TotalSoundsSpawned { get; set; }
        public double TotalShockwaveForce { get; set; }
    }

    private readonly ExplosivePerformanceCounters _perfCounters = new();

    public ExplosivePerformanceCounters GetPerformanceCounters() => _perfCounters;

    #endregion

    #region Utility & Helper Methods

    public string GetDiagnosticsReport(RigidBody body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== ExplosiveBehavior Diagnostics ===");
        sb.AppendLine($"Type: {_currentType}");
        sb.AppendLine($"State: {_currentState} (Timer: {_stateTimer:F2}s)");
        sb.AppendLine($"Fuse Time Remaining: {_fuseTimeRemaining:F2}s");
        sb.AppendLine($"Total Explosions: {_totalExplosions}");
        sb.AppendLine($"Total Debris Spawned: {_totalDebrisSpawned}");
        sb.AppendLine($"Total Bodies Affected: {_totalBodiesAffected}");
        sb.AppendLine($"Peak Blast Force: {_peakBlastForce:F2}");
        sb.AppendLine($"Has Detonated: {_hasDetonated}");
        sb.AppendLine($"Is Armed: {_isArmed}");
        sb.AppendLine($"Chain Reactions Enabled: {_enableChainReactions}");
        sb.AppendLine($"Particle Lifetime: {_activeProfile.ParticleLifetime:F2}s");
        sb.AppendLine($"Sound Pitch Range: {_activeProfile.SoundPitchMin:F2} - {_activeProfile.SoundPitchMax:F2}");
        return sb.ToString();
    }

    #endregion
}
