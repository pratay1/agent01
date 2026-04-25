using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace PhysicsSandbox.Behaviors;

public class PhantomBehavior : BodyBehavior
{
    private const double PHASE_DISTANCE = 10.0;
    private const double SHAKE_STRENGTH = 8000.0;
    private const double SHAKE_FREQ = 50.0;
    private const double ENERGY_REGEN = 15.0;
    private const double SHAKE_COST = 8.0;
    private const double TELEPORT_RANGE = 400.0;
    private const int MAX_TARGETS = 15;
    private const double SHAKE_DT = 1.0 / 60.0;

    public enum PhantomState { Idle, Phasing, Shaking, ChargingTeleport, Teleporting, CoolingDown, Draining }

    public enum PhantomType { Weak, Standard, Strong, Ghost, Spectral, Wraith, Decoy }

    public class PhantomProfile
    {
        public string Name = "";
        public double ShakeStrength = 8000.0;
        public double ShakeFreq = 50.0;
        public double PhaseDist = 10.0;
        public double MaxEnergy = 150.0;
        public double EnergyRegen = 15.0;
        public double ShakeCost = 8.0;
        public bool CanTeleport = true;
        public string ColorHex = "#B388FF";
    }

    private static readonly Dictionary<PhantomType, PhantomProfile> _profiles = new()
    {
        { PhantomType.Weak, new() { Name = "Weak", ShakeStrength = 4000.0, ShakeFreq = 30.0, PhaseDist = 5.0, MaxEnergy = 80.0, EnergyRegen = 10.0, ShakeCost = 12.0, CanTeleport = false, ColorHex = "#CE93D8" } },
        { PhantomType.Standard, new() { Name = "Standard", ShakeStrength = 8000.0, ShakeFreq = 50.0, PhaseDist = 10.0, MaxEnergy = 150.0, EnergyRegen = 15.0, ShakeCost = 8.0, CanTeleport = true, ColorHex = "#B388FF" } },
        { PhantomType.Strong, new() { Name = "Strong", ShakeStrength = 12000.0, ShakeFreq = 70.0, PhaseDist = 15.0, MaxEnergy = 200.0, EnergyRegen = 20.0, ShakeCost = 6.0, CanTeleport = true, ColorHex = "#7C4DFF" } },
        { PhantomType.Ghost, new() { Name = "Ghost", ShakeStrength = 3000.0, ShakeFreq = 20.0, PhaseDist = 20.0, MaxEnergy = 100.0, EnergyRegen = 25.0, ShakeCost = 5.0, CanTeleport = false, ColorHex = "#E1BEE7" } },
        { PhantomType.Spectral, new() { Name = "Spectral", ShakeStrength = 10000.0, ShakeFreq = 60.0, PhaseDist = 12.0, MaxEnergy = 180.0, EnergyRegen = 18.0, ShakeCost = 7.0, CanTeleport = true, ColorHex = "#AA00FF" } },
        { PhantomType.Wraith, new() { Name = "Wraith", ShakeStrength = 15000.0, ShakeFreq = 80.0, PhaseDist = 20.0, MaxEnergy = 250.0, EnergyRegen = 10.0, ShakeCost = 10.0, CanTeleport = true, ColorHex = "#4A148C" } }
    };

    private PhantomType _type = PhantomType.Standard;
    private PhantomProfile _profile = _profiles[PhantomType.Standard];
    private PhantomState _state = PhantomState.Idle;
    private PhantomState _prevState = PhantomState.Idle;
    private double _stateTimer = 0.0;
    private double _lifeTime = 0.0;
    private double _energy = 150.0;
    private int _shakeCount = 0;
    private int _phaseCount = 0;
    private int _teleportCount = 0;
    private Vector2 _teleportTarget = Vector2.Zero;
    private Vector2 _lastPosition = Vector2.Zero;
    private double _peakShake = 0.0;
    private double _totalDistance = 0.0;
    private bool _enableTeleport = true;
    private bool _enableAchievements = true;
    private readonly Dictionary<string, double> _achievements = new();

    public override BodyType Type => BodyType.Phantom;
    public override string Name => "Phantom";
    public override string Description => "Phases through bodies & shakes them";
    public override string ColorHex => _profile.ColorHex;
    public override double DefaultRadius => 18;
    public override double DefaultMass => 4;
    public override double DefaultRestitution => 0.5;

    public PhantomBehavior() : this(PhantomType.Standard) { }
    public PhantomBehavior(PhantomType type)
    {
        _type = type;
        _profile = _profiles[type];
        _energy = _profile.MaxEnergy;
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        body.Restitution = DefaultRestitution;
        body.Mass = DefaultMass;
        body.Radius = DefaultRadius;
        _lastPosition = body.Position;
    }

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.IsStatic || body.IsFrozen) return;
        _lifeTime += dt;
        UpdateEnergyRegen(dt);
        UpdateStateMachine(body, dt, world);
        ProcessPhasing(body, dt, world);
        ProcessTeleport(body, dt, world);
    }

    private void UpdateEnergyRegen(double dt)
    {
        _energy = Math.Min(_profile.MaxEnergy, _energy + _profile.EnergyRegen * dt);
    }

    private void UpdateStateMachine(RigidBody body, double dt, PhysicsWorld world)
    {
        _stateTimer += dt;
        switch (_state)
        {
            case PhantomState.Idle:
                if (_energy < _profile.ShakeCost)
                    _state = PhantomState.Draining;
                CheckNearby(body, world);
                break;
            case PhantomState.Phasing:
                if (_stateTimer > 0.5) _state = PhantomState.CoolingDown;
                break;
            case PhantomState.Shaking:
                if (_stateTimer > 0.3) _state = PhantomState.CoolingDown;
                break;
            case PhantomState.ChargingTeleport:
                if (_stateTimer >= 2.5) ExecuteTeleport(body);
                break;
            case PhantomState.Teleporting:
                if (_stateTimer > 0.2) _state = PhantomState.CoolingDown;
                break;
            case PhantomState.CoolingDown:
                if (_stateTimer > 0.05) _state = PhantomState.Idle;
                break;
            case PhantomState.Draining:
                if (_energy >= _profile.MaxEnergy * 0.5) _state = PhantomState.Idle;
                break;
        }
    }

    private void CheckNearby(RigidBody body, PhysicsWorld world)
    {
        if (_state == PhantomState.Shaking || _state == PhantomState.Phasing) return;
        foreach (var other in world.Bodies)
        {
            if (body == other || other.IsStatic) continue;
            double dist = Vector2.Distance(body.Position, other.Position);
            double phaseDist = body.Radius + other.Radius + _profile.PhaseDist;
            if (dist < phaseDist && _energy >= _profile.ShakeCost)
            {
                _state = PhantomState.Shaking;
                _stateTimer = 0.0;
                break;
            }
        }
    }

    private void ProcessPhasing(RigidBody body, double dt, PhysicsWorld world)
    {
        if (_state != PhantomState.Phasing && _state != PhantomState.Shaking) return;
        int targets = 0;
        foreach (var other in world.Bodies)
        {
            if (targets >= MAX_TARGETS) break;
            if (body == other || other.IsStatic) continue;
            double dist = Vector2.Distance(body.Position, other.Position);
            double phaseDist = body.Radius + other.Radius + _profile.PhaseDist;
            if (dist < phaseDist)
            {
                ApplyShake(body, other);
                targets++;
                _phaseCount++;
            }
        }
    }

    private void ApplyShake(RigidBody body, RigidBody other)
    {
        if (_energy < _profile.ShakeCost) return;
        double angle = Random.Shared.NextDouble() * Math.PI * 2;
        double shakePhase = _lifeTime * _profile.ShakeFreq * 2 * Math.PI;
        double factor = Math.Abs(Math.Sin(shakePhase));
        double strength = Math.Clamp(_profile.ShakeStrength * factor, 1000.0, 20000.0);
        Vector2 dir = new((float)Math.Cos(angle), (float)Math.Sin(angle));
        other.ApplyForce(dir * strength);
        _energy -= _profile.ShakeCost;
        _shakeCount++;
        if (strength > _peakShake) _peakShake = strength;
    }

    private void ProcessTeleport(RigidBody body, double dt, PhysicsWorld world)
    {
        if (!_enableTeleport || !_profile.CanTeleport) return;
    }

    public void StartTeleport(Vector2 target)
    {
        if (!_enableTeleport || !_profile.CanTeleport || _energy < 50.0) return;
        _teleportTarget = target;
        _state = PhantomState.ChargingTeleport;
        _stateTimer = 0.0;
    }

    private void ExecuteTeleport(RigidBody body)
    {
        double dist = Vector2.Distance(body.Position, _teleportTarget);
        if (dist > TELEPORT_RANGE)
            _teleportTarget = body.Position + (_teleportTarget - body.Position).Normalized * TELEPORT_RANGE;
        body.Position = _teleportTarget;
        _energy -= 50.0;
        _teleportCount++;
        _state = PhantomState.Teleporting;
        _stateTimer = 0.0;
    }

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        _phaseCount++;
        if (_state != PhantomState.Shaking && _state != PhantomState.Phasing && _energy >= _profile.ShakeCost)
        {
            _state = PhantomState.Shaking;
            _stateTimer = 0.0;
            ApplyShake(body, other);
        }
    }

    public void SetType(PhantomType type)
    {
        _type = type;
        _profile = _profiles[type];
        _energy = _profile.MaxEnergy;
    }

    public PhantomType GetType() => _type;
    public PhantomState GetState() => _state;
    public double GetEnergy() => _energy;
    public int GetShakeCount() => _shakeCount;
    public int GetTeleportCount() => _teleportCount;
}