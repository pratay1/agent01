using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Diagnostics;

namespace PhysicsSandbox.Behaviors;

public class SpikeBehavior : BodyBehavior
{
    #region Constants & Tunable Parameters
    private const double DEFAULT_DAMAGE = 50.0;
    private const double DEFAULT_EXPLOSION_RADIUS = 100.0;
    private const double DEFAULT_EXPLOSION_FORCE = 12000.0;
    private const double SPIKE_COOLDOWN = 3.0;
    private const double RETRACT_DURATION = 1.0;
    private const double EXTEND_DURATION = 0.5;
    private const double ROTATION_SPEED = 90.0;
    private const double HOMING_RANGE = 200.0;
    private const double HOMING_STRENGTH = 500.0;
    private const int MAX_DEBRIS = 16;
    private const double DEBRIS_LIFETIME = 2.0;
    private const double INVINCIBILITY_DURATION = 0.5;
    private const double MAX_DAMAGE_PER_FRAME = 1000.0;
    private const double STATUS_EFFECT_DURATION = 5.0;
    private const double BURN_DAMAGE_PER_SECOND = 10.0;
    private const double POISON_DAMAGE_PER_SECOND = 5.0;
    private const int MAX_CHAIN_EXPLOSIONS = 5;
    private const double CHAIN_REACTION_FALLOFF = 0.7;
    #endregion

    #region Enums
    public enum SpikeState
    {
        Idle,
        Charging,
        Extended,
        Retracted,
        Exploding,
        Cooldown,
        Stuck
    }

    public enum SpikeType
    {
        Static,
        Retractable,
        Rotating,
        Timed,
        Homing,
        Custom
    }

    public enum DamageType
    {
        Physical,
        Fire,
        Poison,
        Explosive,
        Mixed
    }

    public enum SpikeParticleEffect
    {
        Explosion,
        Spark,
        FireTrail,
        PoisonCloud,
        Debris,
        Shockwave
    }

    public enum SpikeSoundEffect
    {
        Explode,
        Extend,
        Retract,
        Charge,
        Hit,
        Burn
    }
    #endregion

    #region Instance State & Configuration
    private SpikeType _currentType = SpikeType.Static;
    private SpikeState _currentState = SpikeState.Idle;
    private SpikeState _previousState = SpikeState.Idle;
    private double _stateTimer = 0.0;
    private int _stateFrameCount = 0;

    private double _damage = DEFAULT_DAMAGE;
    private double _explosionRadius = DEFAULT_EXPLOSION_RADIUS;
    private double _explosionForce = DEFAULT_EXPLOSION_FORCE;
    private DamageType _damageType = DamageType.Explosive;

    private double _cooldownRemaining = 0.0;
    private double _extendTimer = 0.0;
    private double _retractTimer = 0.0;
    private double _chargeTimer = 0.0;

    private int _explosionCount = 0;
    private int _damageDealt = 0;
    private int _collisionCount = 0;
    private double _totalExplosionEnergy = 0.0;

    private readonly Stopwatch _updateStopwatch = new();

    private bool _isInvincible = false;
    private double _invincibilityTimer = 0.0;

    private readonly Queue<SpikeParticleEffect> _pendingParticles = new();
    private readonly Queue<SpikeSoundEffect> _pendingSounds = new();

    private double _rotationAngle = 0.0;
    private Vector2 _homingTarget = Vector2.Zero;
    private bool _hasHomingTarget = false;

    private readonly List<StatusEffect> _activeStatusEffects = new();
    private class StatusEffect
    {
        public DamageType Type { get; set; }
        public double DurationRemaining { get; set; }
        public double Magnitude { get; set; }
    }

    private bool _enableChainReactions = true;
    private bool _enableStatusEffects = true;
    private bool _enableRotation = false;
    private bool _enableHoming = false;
    #endregion

    #region Behavior Properties (Overrides)
    public override BodyType Type => BodyType.Spike;
    public override string Name => "Spike";
    public override string Description => "Violent spike that bounces and explodes on contact, with configurable types and damage";
    public override string ColorHex => GetColorForType(_currentType);
    public override double DefaultRadius => 14;
    public override double DefaultMass => 7;
    public override double DefaultRestitution => 0.98;
    #endregion

    #region Constructors & Initialization
    public SpikeBehavior() : this(SpikeType.Static) { }

    public SpikeBehavior(SpikeType type)
    {
        _currentType = type;
        ConfigureForType(type);
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        ConfigureForType(_currentType);
        _currentState = SpikeState.Idle;
        LogDebug(body, $"SpikeBehavior initialized: Type={_currentType}, Damage={_damage:F2}, Radius={_explosionRadius:F2}");
    }

    private void ConfigureForType(SpikeType type)
    {
        switch (type)
        {
            case SpikeType.Static:
                _damage = DEFAULT_DAMAGE;
                _explosionRadius = DEFAULT_EXPLOSION_RADIUS;
                _enableRotation = false;
                _enableHoming = false;
                break;
            case SpikeType.Retractable:
                _damage = DEFAULT_DAMAGE * 0.8;
                _explosionRadius = DEFAULT_EXPLOSION_RADIUS * 0.9;
                _enableRotation = false;
                _enableHoming = false;
                break;
            case SpikeType.Rotating:
                _damage = DEFAULT_DAMAGE * 1.2;
                _explosionRadius = DEFAULT_EXPLOSION_RADIUS * 1.1;
                _enableRotation = true;
                _enableHoming = false;
                break;
            case SpikeType.Timed:
                _damage = DEFAULT_DAMAGE * 1.5;
                _explosionRadius = DEFAULT_EXPLOSION_RADIUS * 1.2;
                _enableRotation = false;
                _enableHoming = false;
                break;
            case SpikeType.Homing:
                _damage = DEFAULT_DAMAGE * 1.3;
                _explosionRadius = DEFAULT_EXPLOSION_RADIUS;
                _enableRotation = true;
                _enableHoming = true;
                break;
            case SpikeType.Custom:
                break;
        }
    }
    #endregion

    #region Main Update Loop
    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        _updateStopwatch.Restart();

        try
        {
            if (body.IsStatic || body.IsFrozen || body.HasExploded)
                return;

            RaisePreUpdate(body, dt);

            UpdateStateMachine(body, dt, world);
            UpdateCooldowns(body, dt);
            UpdateRotation(body, dt);
            UpdateHoming(body, dt, world);
            UpdateStatusEffects(body, dt);
            ProcessPendingEffects(body);
            TrackStatistics(body, dt);
            CheckForCollisions(body, world);

            RaisePostUpdate(body, dt);
        }
        finally
        {
            _updateStopwatch.Stop();
            RecordPerformanceMetric("OnUpdate", _updateStopwatch.Elapsed.TotalMilliseconds);
        }
    }
    #endregion

    #region State Machine
    private void UpdateStateMachine(RigidBody body, double dt, PhysicsWorld world)
    {
        _stateTimer += dt;
        _stateFrameCount++;

        switch (_currentState)
        {
            case SpikeState.Idle:
                if (_currentType == SpikeType.Retractable && _stateTimer >= 2.0)
                {
                    TransitionToState(SpikeState.Extended, body);
                }
                if (_currentType == SpikeType.Timed && _stateTimer >= 5.0)
                {
                    TransitionToState(SpikeState.Charging, body);
                }
                break;

            case SpikeState.Charging:
                _chargeTimer += dt;
                if (_chargeTimer >= 1.0)
                {
                    TriggerExplosion(body, world);
                }
                break;

            case SpikeState.Extended:
                if (_currentType == SpikeType.Retractable && _stateTimer >= 3.0)
                {
                    TransitionToState(SpikeState.Retracted, body);
                }
                break;

            case SpikeState.Retracted:
                _retractTimer += dt;
                if (_retractTimer >= RETRACT_DURATION)
                {
                    TransitionToState(SpikeState.Extended, body);
                }
                break;

            case SpikeState.Exploding:
                if (_stateTimer >= 0.5)
                {
                    TransitionToState(SpikeState.Cooldown, body);
                }
                break;

            case SpikeState.Cooldown:
                _cooldownRemaining -= dt;
                if (_cooldownRemaining <= 0)
                {
                    TransitionToState(SpikeState.Idle, body);
                }
                break;
        }
    }

    private void TransitionToState(SpikeState newState, RigidBody body)
    {
        _previousState = _currentState;
        _currentState = newState;
        _stateTimer = 0.0;

        switch (newState)
        {
            case SpikeState.Extended:
                _extendTimer = 0.0;
                TriggerParticleEffect(SpikeParticleEffect.Shockwave);
                TriggerSoundEffect(SpikeSoundEffect.Extend);
                break;
            case SpikeState.Retracted:
                _retractTimer = 0.0;
                body.Radius = DefaultRadius * 0.3;
                TriggerSoundEffect(SpikeSoundEffect.Retract);
                break;
            case SpikeState.Charging:
                _chargeTimer = 0.0;
                TriggerSoundEffect(SpikeSoundEffect.Charge);
                break;
            case SpikeState.Exploding:
                break;
            case SpikeState.Cooldown:
                _cooldownRemaining = SPIKE_COOLDOWN;
                break;
            case SpikeState.Idle:
                body.Radius = DefaultRadius;
                break;
        }

        LogDebug(body, $"State transition: {_previousState} -> {_currentState}");
    }
    #endregion

    #region Collision Handling
    private void CheckForCollisions(RigidBody body, PhysicsWorld world)
    {
        if (_currentState == SpikeState.Cooldown || _currentState == SpikeState.Retracted)
            return;

        foreach (var other in world.Bodies)
        {
            if (body == other || other.IsStatic)
                continue;

            double distSq = (body.Position - other.Position).LengthSquared;
            if (distSq < (body.Radius + other.Radius) * (body.Radius + other.Radius))
            {
                HandleCollision(body, other, world);
                break;
            }
        }
    }

    private void HandleCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        _collisionCount++;

        if (_isInvincible)
            return;

        Vector2 normal = (body.Position - other.Position).Normalized;
        double bounceForce = 3000.0;
        body.ApplyImpulse(normal * bounceForce / body.Mass);
        other.ApplyImpulse(-normal * bounceForce / other.Mass);

        ApplyDamage(body, other);
        TriggerExplosion(body, world);

        _isInvincible = true;
        _invincibilityTimer = INVINCIBILITY_DURATION;

        LogDebug(body, $"Collision with body {other.Id}, damage dealt: {_damage:F2}");
    }

    private void ApplyDamage(RigidBody body, RigidBody other)
    {
        double damage = _damage;
        if (_damageType == DamageType.Fire || _damageType == DamageType.Mixed)
        {
            damage *= 1.5;
        }
        if (_damageType == DamageType.Poison || _damageType == DamageType.Mixed)
        {
            damage *= 1.2;
        }

        _damageDealt += (int)damage;
    }
    #endregion

    #region Explosion System
    private void TriggerExplosion(RigidBody body, PhysicsWorld world)
    {
        if (_currentState == SpikeState.Exploding || body.HasExploded)
            return;

        TransitionToState(SpikeState.Exploding, body);
        body.HasExploded = true;
        _explosionCount++;

        world.ForceManager.Explosion.Trigger(body.Position);
        TriggerParticleEffect(SpikeParticleEffect.Explosion);
        TriggerSoundEffect(SpikeSoundEffect.Explode);

        SpawnDebris(body, world);
        ApplyExplosionForce(body, world);
        ApplyStatusEffects(body, world);

        if (_enableChainReactions)
        {
            TriggerChainReaction(body, world);
        }

        _totalExplosionEnergy += _explosionForce * _explosionRadius;
        world.RemoveBody(body);
    }

    private void SpawnDebris(RigidBody body, PhysicsWorld world)
    {
        int debrisCount = Math.Min(MAX_DEBRIS, 8 + (int)(_explosionRadius / 20));
        for (int i = 0; i < debrisCount; i++)
        {
            double angle = i * Math.PI * 2 / debrisCount + Random.Shared.NextDouble() * 0.5;
            double speed = 200.0 + Random.Shared.NextDouble() * 100.0;
            var vel = new Vector2(
                Math.Cos(angle) * speed,
                Math.Sin(angle) * speed);

            var debris = world.CreateBody(body.Position, body.Radius * 0.2, body.Mass * 0.1, 0.5);
            debris.Velocity = vel;
            debris.BodyType = BodyType.Fire;
            debris.Lifetime = DEBRIS_LIFETIME;
        }
    }

    private void ApplyExplosionForce(RigidBody body, PhysicsWorld world)
    {
        foreach (var other in world.Bodies)
        {
            if (body == other)
                continue;

            Vector2 dir = (other.Position - body.Position).Normalized;
            double dist = Vector2.Distance(body.Position, other.Position);
            double safeDist = Math.Max(dist, 0.1);
            double force = _explosionForce / (safeDist * safeDist + 1);
            other.ApplyImpulse(dir * force * 30);
        }
    }

    private void ApplyStatusEffects(RigidBody body, PhysicsWorld world)
    {
        if (!_enableStatusEffects)
            return;

        foreach (var other in world.Bodies)
        {
            if (body == other)
                continue;

            double dist = Vector2.Distance(body.Position, other.Position);
            if (dist > _explosionRadius)
                continue;

            if (_damageType == DamageType.Fire || _damageType == DamageType.Mixed)
            {
                // other.ApplyStatusEffect(DamageType.Fire, STATUS_EFFECT_DURATION, BURN_DAMAGE_PER_SECOND);
            }
            if (_damageType == DamageType.Poison || _damageType == DamageType.Mixed)
            {
                // other.ApplyStatusEffect(DamageType.Poison, STATUS_EFFECT_DURATION, POISON_DAMAGE_PER_SECOND);
            }
        }
    }
    #endregion

    #region Chain Reaction System
    private void TriggerChainReaction(RigidBody body, PhysicsWorld world)
    {
        int reactions = 0;
        double currentForce = _explosionForce;

        foreach (var other in SpatialQuery(body.Position, _explosionRadius * 2, world))
        {
            if (other == body || other.IsStatic || reactions >= MAX_CHAIN_EXPLOSIONS)
                continue;

            if (other.Behavior is SpikeBehavior otherSpike && otherSpike._currentState != SpikeState.Exploding)
            {
                otherSpike.TriggerExplosion(other, world);
                currentForce *= CHAIN_REACTION_FALLOFF;
                reactions++;
            }
        }

        if (reactions > 0)
        {
            TriggerParticleEffect(SpikeParticleEffect.Shockwave);
            TriggerSoundEffect(SpikeSoundEffect.Explode);
        }
    }
    #endregion

    #region Type-Specific Logic
    private void UpdateRotation(RigidBody body, double dt)
    {
        if (!_enableRotation)
            return;

        _rotationAngle += ROTATION_SPEED * dt;
        if (_rotationAngle > 360) _rotationAngle -= 360;
        body.Rotation = _rotationAngle;
    }

    private void UpdateHoming(RigidBody body, double dt, PhysicsWorld world)
    {
        if (!_enableHoming || _currentState != SpikeState.Extended)
            return;

        if (!_hasHomingTarget)
        {
            var nearest = FindNearestBody(body.Position, world, b => !b.IsStatic && b != body);
            if (nearest != null && Vector2.Distance(body.Position, nearest.Position) < HOMING_RANGE)
            {
                _homingTarget = nearest.Position;
                _hasHomingTarget = true;
            }
        }

        if (_hasHomingTarget)
        {
            Vector2 dir = (_homingTarget - body.Position).Normalized;
            body.ApplyForce(dir * HOMING_STRENGTH * dt);
        }
    }

    private void UpdateCooldowns(RigidBody body, double dt)
    {
        if (_isInvincible)
        {
            _invincibilityTimer -= dt;
            if (_invincibilityTimer <= 0)
            {
                _isInvincible = false;
            }
        }
    }
    #endregion

    #region Status Effects
    private void UpdateStatusEffects(RigidBody body, double dt)
    {
        for (int i = _activeStatusEffects.Count - 1; i >= 0; i--)
        {
            var effect = _activeStatusEffects[i];
            effect.DurationRemaining -= dt;
            if (effect.DurationRemaining <= 0)
            {
                _activeStatusEffects.RemoveAt(i);
            }
            else
            {
                if (effect.Type == DamageType.Fire)
                {
                    // body.ApplyDamage(effect.Magnitude * dt, effect.Type);
                }
                else if (effect.Type == DamageType.Poison)
                {
                    // body.ApplyDamage(effect.Magnitude * dt, effect.Type);
                }
            }
        }
    }
    #endregion

    #region Particle & Sound Effects
    private void TriggerParticleEffect(SpikeParticleEffect type)
    {
        _pendingParticles.Enqueue(type);
    }

    private void TriggerSoundEffect(SpikeSoundEffect type)
    {
        _pendingSounds.Enqueue(type);
    }

    private void ProcessPendingEffects(RigidBody body)
    {
        while (_pendingParticles.Count > 0)
        {
            var particle = _pendingParticles.Dequeue();
        }

        while (_pendingSounds.Count > 0)
        {
            var sound = _pendingSounds.Dequeue();
        }
    }
    #endregion

    #region Utility & Helper Methods
    private static string GetColorForType(SpikeType type)
    {
        return type switch
        {
            SpikeType.Static => "#F44336",
            SpikeType.Retractable => "#FF9800",
            SpikeType.Rotating => "#4CAF50",
            SpikeType.Timed => "#F44336",
            SpikeType.Homing => "#9C27B0",
            SpikeType.Custom => "#F44336",
            _ => "#F44336"
        };
    }

    private void TrackStatistics(RigidBody body, double dt)
    {
    }
    #endregion

    #region Statistics Tracking
    public (int Explosions, int DamageDealt, int Collisions, double TotalExplosionEnergy) GetStatistics()
    {
        return (_explosionCount, _damageDealt, _collisionCount, _totalExplosionEnergy);
    }

    public string GetDiagnosticsReport(RigidBody body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== SpikeBehavior Diagnostics ===");
        sb.AppendLine($"Type: {_currentType}");
        sb.AppendLine($"State: {_currentState} (Timer: {_stateTimer:F2}s)");
        sb.AppendLine($"Damage: {_damage:F2}");
        sb.AppendLine($"Explosion Radius: {_explosionRadius:F2}");
        sb.AppendLine($"Explosion Count: {_explosionCount}");
        sb.AppendLine($"Damage Dealt: {_damageDealt}");
        sb.AppendLine($"Collision Count: {_collisionCount}");
        sb.AppendLine($"Total Explosion Energy: {_totalExplosionEnergy:F2}");
        sb.AppendLine($"Invincible: {_isInvincible} (Timer: {_invincibilityTimer:F2}s)");
        sb.AppendLine($"Rotation Enabled: {_enableRotation}");
        sb.AppendLine($"Homing Enabled: {_enableHoming}");
        return sb.ToString();
    }
    #endregion

    #region Performance Counters
    public class SpikePerformanceCounters
    {
        public long TotalExplosions { get; set; }
        public double TotalExplosionTimeMs { get; set; }
        public double AverageExplosionTimeMs => TotalExplosions > 0 ? TotalExplosionTimeMs / TotalExplosions : 0;
        public int ActiveChainReactions { get; set; }
    }

    private readonly SpikePerformanceCounters _perfCounters = new();

    public SpikePerformanceCounters GetPerformanceCounters() => _perfCounters;
    #endregion

    #region Public API
    public void SetSpikeType(RigidBody body, SpikeType type)
    {
        _currentType = type;
        ConfigureForType(type);
        LogDebug(body, $"Spike type changed to {type}");
    }

    public void SetDamage(double damage)
    {
        _damage = Math.Clamp(damage, 0, MAX_DAMAGE_PER_FRAME);
    }

    public void SetExplosionRadius(double radius)
    {
        _explosionRadius = Math.Max(10, radius);
    }

    public void SetDamageType(DamageType type)
    {
        _damageType = type;
    }

    public void SetChainReactionsEnabled(bool enabled) => _enableChainReactions = enabled;
    public void SetStatusEffectsEnabled(bool enabled) => _enableStatusEffects = enabled;
    public void SetRotationEnabled(bool enabled) => _enableRotation = enabled;
    public void SetHomingEnabled(bool enabled) => _enableHoming = enabled;

    public SpikeState GetCurrentState() => _currentState;
    public int GetExplosionCount() => _explosionCount;
    public int GetDamageDealt() => _damageDealt;
    #endregion

    #region Debug Visualization
    protected override void RenderDebugOverlay(RigidBody body, DrawingContext dc)
    {
        if (dc == null || !GlobalConfig.EnableDebugVisualization)
            return;

        DebugDrawCircle(dc, body.Position, _explosionRadius, Brushes.Red, 1.0);

        var stateColors = new Dictionary<SpikeState, Brush>
        {
            { SpikeState.Idle, Brushes.Gray },
            { SpikeState.Charging, Brushes.Yellow },
            { SpikeState.Extended, Brushes.Red },
            { SpikeState.Retracted, Brushes.Blue },
            { SpikeState.Exploding, Brushes.Orange },
            { SpikeState.Cooldown, Brushes.Green }
        };

        if (stateColors.TryGetValue(_currentState, out var brush))
        {
            dc.DrawEllipse(brush, new Pen(Brushes.Black, 1), new Point(body.Position.X, body.Position.Y), 5, 5);
        }
    }
    #endregion

    #region Serialization
    public string SerializeState()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Type:{_currentType}");
        sb.AppendLine($"State:{_currentState}");
        sb.AppendLine($"Damage:{_damage}");
        sb.AppendLine($"ExplosionRadius:{_explosionRadius}");
        sb.AppendLine($"ExplosionCount:{_explosionCount}");
        sb.AppendLine($"DamageDealt:{_damageDealt}");
        sb.AppendLine($"CollisionCount:{_collisionCount}");
        sb.AppendLine($"EnableChainReactions:{_enableChainReactions}");
        sb.AppendLine($"EnableStatusEffects:{_enableStatusEffects}");
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
                        if (Enum.TryParse(parts[1], out SpikeType type))
                            _currentType = type;
                        break;
                    case "State":
                        if (Enum.TryParse(parts[1], out SpikeState spikeState))
                            _currentState = spikeState;
                        break;
                    case "Damage":
                        _damage = double.Parse(parts[1]);
                        break;
                    case "ExplosionRadius":
                        _explosionRadius = double.Parse(parts[1]);
                        break;
                    case "ExplosionCount":
                        _explosionCount = int.Parse(parts[1]);
                        break;
                    case "DamageDealt":
                        _damageDealt = int.Parse(parts[1]);
                        break;
                    case "CollisionCount":
                        _collisionCount = int.Parse(parts[1]);
                        break;
                    case "EnableChainReactions":
                        _enableChainReactions = bool.Parse(parts[1]);
                        break;
                    case "EnableStatusEffects":
                        _enableStatusEffects = bool.Parse(parts[1]);
                        break;
                }
            }
            catch { }
        }
    }
    #endregion
}
