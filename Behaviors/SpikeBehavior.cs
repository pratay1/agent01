using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;

namespace PhysicsSandbox.Behaviors;

public class SpikeBehavior : BodyBehavior
{
    private const double DAMAGE = 50.0;
    private const double EXPLOSION_RADIUS = 100.0;
    private const double EXPLOSION_FORCE = 12000.0;
    private const double COOLDOWN = 3.0;
    private const double RETRACT_DURATION = 1.0;
    private const double EXTEND_DURATION = 0.5;
    private const double ROTATION_SPEED = 90.0;
    private const double HOMING_RANGE = 200.0;
    private const double HOMING_STRENGTH = 500.0;
    private const int MAX_DEBRIS = 16;

    public enum SpikeState { Idle, Charging, Extended, Retracted, Exploding, Cooldown, Stuck }
    public enum SpikeType { Static, Retractable, Rotating, Timed }

    public class SpikeProfile
    {
        public string Name = "";
        public double Damage = 50.0;
        public double Radius = 15.0;
        public double Mass = 6.0;
        public double Restitution = 0.3;
        public double ExplosionRadius = 100.0;
        public double ExplosionForce = 12000.0;
        public string ColorHex = "#9C27B0";
    }

    private static readonly Dictionary<SpikeType, SpikeProfile> _profiles = new()
    {
        { SpikeType.Static, new() { Name = "Static Spike", Damage = 60.0, Radius = 15.0, Mass = 8.0, Restitution = 0.2, ExplosionRadius = 100.0, ExplosionForce = 15000.0, ColorHex = "#9C27B0" } },
        { SpikeType.Retractable, new() { Name = "Retractable", Damage = 50.0, Radius = 15.0, Mass = 6.0, Restitution = 0.3, ExplosionRadius = 100.0, ExplosionForce = 12000.0, ColorHex = "#673AB7" } },
        { SpikeType.Rotating, new() { Name = "Rotating", Damage = 80.0, Radius = 18.0, Mass = 5.0, Restitution = 0.4, ExplosionRadius = 120.0, ExplosionForce = 18000.0, ColorHex = "#E040FB" } },
        { SpikeType.Timed, new() { Name = "Timed", Damage = 100.0, Radius = 20.0, Mass = 10.0, Restitution = 0.1, ExplosionRadius = 150.0, ExplosionForce = 20000.0, ColorHex = "#D500F9" } }
    };

    private SpikeType _type = SpikeType.Static;
    private SpikeProfile _profile = _profiles[SpikeType.Static];
    private SpikeState _state = SpikeState.Idle;
    private double _stateTimer = 0.0;
    private int _hitCount = 0;
    private int _kills = 0;
    private double _cooldownRemaining = 0.0;
    private double _rotationAngle = 0.0;

    public override BodyType Type => BodyType.Spike;
    public override string Name => "Spike";
    public override string Description => "Damages bodies on contact, can explode";
    public override string ColorHex => _profile.ColorHex;
    public override double DefaultRadius => _profile.Radius;
    public override double DefaultMass => _profile.Mass;
    public override double DefaultRestitution => _profile.Restitution;

    public SpikeBehavior() : this(SpikeType.Static) { }
    public SpikeBehavior(SpikeType type)
    {
        _type = type;
        _profile = _profiles[type];
    }

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.IsStatic || body.IsFrozen) return;
        _stateTimer += dt;
        if (_cooldownRemaining > 0) _cooldownRemaining -= dt;
        UpdateState(body, dt, world);
    }

    private void UpdateState(RigidBody body, double dt, PhysicsWorld world)
    {
        switch (_state)
        {
            case SpikeState.Idle:
                break;
            case SpikeState.Charging:
                if (_stateTimer >= EXTEND_DURATION) _state = SpikeState.Extended;
                break;
            case SpikeState.Extended:
                if (_type == SpikeType.Rotating)
                {
                    _rotationAngle += ROTATION_SPEED * dt;
                    body.AngularVelocity = (float)(ROTATION_SPEED * Math.PI / 180);
                }
                if (_stateTimer >= COOLDOWN) _state = SpikeState.Retracted;
                break;
            case SpikeState.Retracted:
                if (_stateTimer >= RETRACT_DURATION) _state = SpikeState.Idle;
                break;
            case SpikeState.Cooldown:
                if (_cooldownRemaining <= 0) _state = SpikeState.Idle;
                break;
        }
    }

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        if (_state == SpikeState.Cooldown || _state == SpikeState.Exploding) return;
        if (other.IsStatic) return;

        _hitCount++;
        ApplyDamage(body, other);

        if (_profile.ExplosionForce > 0)
            TriggerExplosion(body, world);

        _state = SpikeState.Cooldown;
        _cooldownRemaining = COOLDOWN;
    }

    private void ApplyDamage(RigidBody body, RigidBody other)
    {
        if (other.Behavior is NormalBehavior normal)
            normal.SetAffectedByGravity(false);
    }

    private void TriggerExplosion(RigidBody body, PhysicsWorld world)
    {
        _state = SpikeState.Exploding;
        foreach (var other in SpatialQuery(body.Position, _profile.ExplosionRadius, world))
        {
            if (other == body || other.IsStatic) continue;
            double dist = Vector2.Distance(body.Position, other.Position);
            double falloff = 1.0 - dist / _profile.ExplosionRadius;
            double force = _profile.ExplosionForce * falloff;
            if (force > 10)
            {
                Vector2 dir = (other.Position - body.Position).Normalized;
                other.ApplyImpulse(dir * force);
                _kills++;
            }
        }
        _stateTimer = 0.0;
    }

    public void SetType(SpikeType type)
    {
        _type = type;
        _profile = _profiles[type];
    }

    public SpikeType GetType() => _type;
    public SpikeState GetState() => _state;
    public int GetHitCount() => _hitCount;
}