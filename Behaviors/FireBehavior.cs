using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace PhysicsSandbox.Behaviors;

public class FireBehavior : BodyBehavior
{
    private const double MIN_RADIUS = 2.0;
    private const double IGNITION_TEMP = 300.0;
    private const double MAX_TEMP = 1500.0;
    private const double MAX_RADIUS_MULT = 3.0;

    public enum FireType { Candle, Campfire, Torch, Fireball, Inferno, BlueFlame, Dying, Explosive, Eternal, Wildfire }

    public class FireProfile
    {
        public string Name = "";
        public double BaseHeat = 500.0;
        public double HeatVelocityScale = 10.0;
        public double MaxTemp = 1500.0;
        public double FuelCapacity = 100.0;
        public double BurnRate = 0.1;
        public double MinRadius = 3.0;
        public double MaxRadiusMult = 2.5;
        public double Convection = 50.0;
        public double Radiation = 5.0;
        public double SpreadProb = 0.3;
        public string CoreColor = "#FF5722";
        public string MidColor = "#FF9800";
        public string OuterColor = "#FFEB3B";
        public bool CanSpread = true;
    }

    private static readonly Dictionary<FireType, FireProfile> _profiles = new()
    {
        { FireType.Candle, new() { Name = "Candle", BaseHeat = 800.0, FuelCapacity = 20.0, BurnRate = 0.02, MinRadius = 3.0, MaxRadiusMult = 1.5, Convection = 20.0, CoreColor = "#FFFFFF", MidColor = "#FFD54F", OuterColor = "#FF9800", SpreadProb = 0.05 } },
        { FireType.Campfire, new() { Name = "Campfire", BaseHeat = 600.0, FuelCapacity = 150.0, BurnRate = 0.08, MinRadius = 8.0, MaxRadiusMult = 2.5, Convection = 40.0, CoreColor = "#FF5722", MidColor = "#FF9800", OuterColor = "#FFC107", SpreadProb = 0.25 } },
        { FireType.Torch, new() { Name = "Torch", BaseHeat = 1000.0, FuelCapacity = 60.0, BurnRate = 0.05, MinRadius = 6.0, MaxRadiusMult = 2.0, Convection = 35.0, CoreColor = "#FFD740", MidColor = "#FF9800", OuterColor = "#FF5722", SpreadProb = 0.15 } },
        { FireType.Fireball, new() { Name = "Fireball", BaseHeat = 2000.0, FuelCapacity = 50.0, BurnRate = 2.0, MinRadius = 15.0, MaxRadiusMult = 3.0, Convection = 80.0, CoreColor = "#FF0000", MidColor = "#FF9800", OuterColor = "#FFFF00", SpreadProb = 0.4 } },
        { FireType.Inferno, new() { Name = "Inferno", BaseHeat = 3000.0, FuelCapacity = 500.0, BurnRate = 1.5, MinRadius = 20.0, MaxRadiusMult = 4.0, Convection = 100.0, CoreColor = "#FF0000", MidColor = "#FF5722", OuterColor = "#FFC107", SpreadProb = 0.6 } },
        { FireType.BlueFlame, new() { Name = "Blue Flame", BaseHeat = 2500.0, FuelCapacity = 40.0, BurnRate = 0.3, MinRadius = 5.0, MaxRadiusMult = 1.8, Convection = 60.0, CoreColor = "#00BFFF", MidColor = "#1E90FF", OuterColor = "#4169E1", SpreadProb = 0.1 } },
        { FireType.Dying, new() { Name = "Dying", BaseHeat = 200.0, FuelCapacity = 5.0, BurnRate = 0.5, MinRadius = 2.0, MaxRadiusMult = 1.2, Convection = 5.0, CoreColor = "#8B4513", MidColor = "#A0522D", OuterColor = "#CD853F", SpreadProb = 0.0 } },
        { FireType.Explosive, new() { Name = "Explosive", BaseHeat = 4000.0, FuelCapacity = 100.0, BurnRate = 5.0, MinRadius = 25.0, MaxRadiusMult = 5.0, Convection = 150.0, CoreColor = "#FF0000", MidColor = "#FF4500", OuterColor = "#FFA500", SpreadProb = 0.8 } },
        { FireType.Eternal, new() { Name = "Eternal", BaseHeat = 1000.0, FuelCapacity = 9999.0, BurnRate = 0.0, MinRadius = 10.0, MaxRadiusMult = 2.0, Convection = 30.0, CoreColor = "#FFD700", MidColor = "#FF8C00", OuterColor = "#FF4500", SpreadProb = 0.2 } },
        { FireType.Wildfire, new() { Name = "Wildfire", BaseHeat = 1500.0, FuelCapacity = 1000.0, BurnRate = 0.5, MinRadius = 30.0, MaxRadiusMult = 6.0, Convection = 70.0, CoreColor = "#FF4500", MidColor = "#FF6347", OuterColor = "#FFD700", SpreadProb = 0.5 } }
    };

    private FireType _fireType = FireType.Campfire;
    private FireProfile _profile = _profiles[FireType.Campfire];
    private double _temperature = 600.0;
    private double _fuel = 100.0;
    private double _currentRadius = 3.2;
    private double _flickerPhase = 0.0;
    private double _sizeVariation = 1.0;
    private double _intensityMult = 1.0;
    private bool _isIgnited = true;
    private bool _isDying = false;
    private double _peakTemp = 600.0;
    private double _avgTemp = 600.0;
    private double _tempSamples = 1.0;
    private int _ignitionsCaused = 0;
    private int _bodiesIgnited = 0;
    private int _collisionCount = 0;
    private double _maxSize = 0.0;
    private double _minSize = 100.0;
    private double _totalWork = 0.0;
    private double _maxHeatForce = 0.0;
    private Vector2 _lastPosition = Vector2.Zero;
    private readonly List<Vector2> _trail = new();
    private double _flicker = 0.0;
    private Random _rng = new Random();

    public override BodyType Type => BodyType.Fire;
    public override string Name => _profile.Name + " Fire";
    public override string Description => _profile.Name + " fire at " + _temperature + "K";
    public override string ColorHex => _profile.CoreColor;
    public override double DefaultRadius => Math.Max(MIN_RADIUS, _profile.MinRadius) * 0.4;
    public override double DefaultMass => Math.Clamp(_profile.BaseHeat / 200.0, 0.5, 50.0);
    public override double DefaultRestitution => 0.1;

    public FireBehavior() : this(FireType.Campfire) { }
    public FireBehavior(FireType type)
    {
        _fireType = type;
        _profile = _profiles[type];
        _temperature = _profile.BaseHeat;
        _fuel = _profile.FuelCapacity;
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        _rng = new Random(body.Id);
        body.Restitution = DefaultRestitution;
        body.Mass = DefaultMass;
        body.Radius = DefaultRadius;
        body.CollisionLayer = CollisionLayer.Particle;
        body.CollisionMask = (int)CollisionLayer.Default;
        _currentRadius = DefaultRadius;
        _maxSize = DefaultRadius;
        _minSize = DefaultRadius;
        _lastPosition = body.Position;
    }

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.IsStatic || body.IsFrozen || !_isIgnited) return;

        UpdateFuel(dt);
        UpdateTemperature(dt);
        ApplyFirePhysics(body, dt, world);
        ApplyConvection(body, dt, world);
        ApplyRadiation(body, world);
        UpdateSize(body, dt);
        SpreadFire(body, dt, world);
        IgniteNearby(body, world);
        UpdateTrail(body, dt);

        _flickerPhase = (_flickerPhase + 8.0 * dt) % 1.0;
        _flicker = Math.Sin(_flickerPhase * Math.PI * 2) * 0.15 + Math.Sin(_flickerPhase * Math.PI * 2 * 2.3) * 0.05;
        _sizeVariation = 1.0 + _flicker * _intensityMult;

        CheckExtinguish();
        ClampProperties(body);
    }

    private void UpdateFuel(double dt)
    {
        if (_profile.BurnRate > 0 && _fuel > 0)
        {
            _fuel -= _profile.BurnRate * dt;
            if (_fuel < 0) _fuel = 0;
        }
    }

    private void UpdateTemperature(double dt)
    {
        if (_profile == null) return;
        double target = _profile.BaseHeat * (_fuel / Math.Max(1.0, _profile.FuelCapacity)) * _intensityMult;
        target = Math.Clamp(target, 200.0, _profile.MaxTemp);
        if (_isDying) target = Math.Max(200.0, target * 0.92);

        _temperature += (target - _temperature) * 5.0 * dt;
        _temperature *= 0.95;
        if (_temperature > _peakTemp) _peakTemp = _temperature;
        _avgTemp = (_avgTemp * _tempSamples + _temperature) / (_tempSamples + 1.0);
        _tempSamples++;
    }

    private void ApplyFirePhysics(RigidBody body, double dt, PhysicsWorld world)
    {
        if (_profile == null || world == null) return;
        Vector2 antiGravity = -world.Gravity.Normalized;
        double heatForce = _temperature * _profile.HeatVelocityScale * body.Mass / 5000.0;
        double flickerX = (_flicker * 0.2 - 0.1) * heatForce * 0.1;
        double flickerY = heatForce * 1.5;
        Vector2 force = new((float)(flickerX * antiGravity.X), (float)(flickerY * antiGravity.Y));
        body.ApplyForce(force);
        body.ApplyForce(-body.Velocity.Normalized * (body.Velocity.Length * 0.1 * body.Mass));
        _totalWork += force.Length * body.Velocity.Length * dt;
        if (heatForce > _maxHeatForce) _maxHeatForce = heatForce;
    }

    private void ApplyConvection(RigidBody body, double dt, PhysicsWorld world)
    {
        if (world == null || _profile == null) return;
        Vector2 antiGravity = -world.Gravity.Normalized;
        double convForce = _temperature / 100.0 * _profile.Convection * 0.3;
        double convX = Math.Sin(_flickerPhase * 3.0 + body.Id) * 0.3 * convForce * 0.2;
        double convY = convForce * 1.5;
        Vector2 force = new((float)(convX * antiGravity.X), (float)(convY * antiGravity.Y));
        body.ApplyForce(force);
    }

    private void ApplyRadiation(RigidBody body, PhysicsWorld world)
    {
        if (_profile == null || world == null) return;
        double range = _currentRadius * 3.0;
        double strength = _temperature / 100.0 * _profile.Radiation;
        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic) continue;
            double dist = (body.Position - other.Position).Length;
            if (dist > range || dist < 0.1) continue;
            double effect = strength * (1.0 - dist / range);
            other.ApplyForce((other.Position - body.Position).Normalized * effect);
        }
    }

    private void UpdateSize(RigidBody body, double dt)
    {
        if (_profile == null) return;
        double size = DefaultRadius * _sizeVariation * Math.Clamp(_fuel / Math.Max(1.0, _profile.FuelCapacity) * 2.0, 0.3, 2.0);
        _currentRadius = size;
        _intensityMult = 0.98 * _intensityMult + 0.02 * (1.0 + _temperature / Math.Max(1.0, _peakTemp) * 0.5);
        if (_currentRadius > _maxSize) _maxSize = _currentRadius;
        if (_currentRadius < _minSize) _minSize = _currentRadius;
        body.Radius = _currentRadius;
    }

    private void SpreadFire(RigidBody body, double dt, PhysicsWorld world)
    {
        if (_rng == null || _profile == null || !_profile.CanSpread || _fuel < 10 || world == null) return;
        if (_rng.NextDouble() > _profile.SpreadProb) return;

        foreach (var other in world.Bodies)
        {
            if (other == body || other.BodyType == BodyType.Fire || other.IsStatic) continue;
            double dist = Vector2.Distance(body.Position, other.Position);
            if (dist < _currentRadius * 2)
            {
                _fuel -= 1.0;
                break;
            }
        }
    }

    private void IgniteNearby(RigidBody body, PhysicsWorld world)
    {
        if (_profile == null || world == null || _rng == null) return;
        foreach (var other in world.Bodies)
        {
            if (other == body || other.BodyType == BodyType.Fire || other.IsStatic || _temperature < IGNITION_TEMP * 1.5) continue;
            double dist = Vector2.Distance(body.Position, other.Position);
            if (dist < _currentRadius * 1.5)
            {
                _ignitionsCaused++;
                _bodiesIgnited++;
            }
        }
    }

    private void UpdateTrail(RigidBody body, double dt)
    {
        _trail.Add(body.Position);
        if (_trail.Count > 30) _trail.RemoveAt(0);
        _lastPosition = body.Position;
    }

    private void CheckExtinguish()
    {
        if (_fuel <= 0) _isDying = true;
        if (_isDying && _temperature < 250.0) _isIgnited = false;
        if (!_isIgnited && _fuel > 10.0 && _temperature > 300.0) { _isIgnited = true; _isDying = false; }
    }

    private void ClampProperties(RigidBody body)
    {
        if (_profile == null) return;
        double mult = _profile.MaxRadiusMult > 0 ? _profile.MaxRadiusMult : MAX_RADIUS_MULT;
        double maxR = Math.Max(MIN_RADIUS, _currentRadius * mult);
        body.Radius = Math.Clamp(_currentRadius, MIN_RADIUS, maxR);
        if (body.Velocity.Length > 1000) body.Velocity = body.Velocity.Normalized * 1000;
        if (double.IsNaN(body.Position.X) || double.IsNaN(body.Position.Y))
        {
            body.Position = _lastPosition;
            body.Velocity = Vector2.Zero;
        }
    }

    public void SetType(FireType type)
    {
        _fireType = type;
        _profile = _profiles[type];
        _temperature = _profile.BaseHeat;
    }

    public FireType GetType() => _fireType;
    public double GetTemperature() => _temperature;
    public double GetFuel() => _fuel;
    public bool IsIgnited() => _isIgnited;
    public int GetIgnitionsCaused() => _ignitionsCaused;
}