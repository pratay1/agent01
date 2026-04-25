using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Diagnostics;
using System.Text;

namespace PhysicsSandbox.Behaviors;

public class FireBehavior : BodyBehavior
{
    #region Constants & Tunable Parameters

    private const double DEFAULT_BASE_HEAT = 500.0;
    private const double DEFAULT_HEAT_VELOCITY_SCALE = 10.0;
    private const double MIN_BURN_RADIUS = 2.0;
    private const double MAX_BURN_RADIUS_MULTIPLIER = 3.0;
    private const double HEAT_DECAY_RATE = 0.95;
    private const double IGNITION_TEMPERATURE = 300.0;
    private const double MAX_FIRE_TEMPERATURE = 1500.0;
    private const double CONVECTION_STRENGTH = 50.0;
    private const double RADIATION_STRENGTH = 5.0;
    private const double SPREAD_PROBABILITY_BASE = 0.3;
    private const double FUEL_CONSUMPTION_RATE = 0.1;
    private const double FLICKER_SPEED = 8.0;
    private const double PARTICLE_EMISSION_RATE = 10.0;
    private const double SMOKE_CHANCE = 0.15;
    private const double SPARK_CHANCE = 0.05;
    private const double MAX_RADIUS_TEMP_MULTIPLIER = 0.1;
    private const double BODY_IGNITION_THRESHOLD = 400.0;
    private const double HEAT_TRANSMISSION_FACTOR = 0.15;

    #endregion

    #region Fire Presets & Profiles

    public enum FireType { Candle, Campfire, Torch, Fireball, Inferno, BlueFlame, Dying, Explosive, Eternal, Wildfire }

    public class FireProfile
    {
        public string Name { get; set; } = "";
        public double BaseHeat { get; set; } = DEFAULT_BASE_HEAT;
        public double HeatVelocityScale { get; set; } = 10.0;
        public double IgnitionTemperature { get; set; } = IGNITION_TEMPERATURE;
        public double MaxTemperature { get; set; } = MAX_FIRE_TEMPERATURE;
        public double ConvectionStrength { get; set; } = CONVECTION_STRENGTH;
        public double RadiationStrength { get; set; } = RADIATION_STRENGTH;
        public double SpreadProbability { get; set; } = SPREAD_PROBABILITY_BASE;
        public double FuelCapacity { get; set; } = 100.0;
        public double BurnRate { get; set; } = FUEL_CONSUMPTION_RATE;
        public double MinimumRadius { get; set; } = MIN_BURN_RADIUS;
        public double MaxRadiusMultiplier { get; set; } = 3.0;
        public double HeatDecayRate { get; set; } = HEAT_DECAY_RATE;
        public string CoreColorHex { get; set; } = "#FF5722";
        public string MidColorHex { get; set; } = "#FF9800";
        public string OuterColorHex { get; set; } = "#FFEB3B";
        public string SmokeColorHex { get; set; } = "#424242";
        public bool CanSpread { get; set; } = true;
        public bool ProducesSmoke { get; set; } = true;
        public bool ProducesSparks { get; set; } = true;
        public bool ConsumesFuel { get; set; } = true;
        public bool SelfSustaining { get; set; } = false;
        public bool LeavesAsh { get; set; } = true;
    }

    private static readonly Dictionary<FireType, FireProfile> _fireProfiles = new()
    {
        { FireType.Candle, new FireProfile { Name = "Candle", BaseHeat = 800.0, HeatVelocityScale = 5.0, FuelCapacity = 20.0, BurnRate = 0.02, MinimumRadius = 3.0, MaxRadiusMultiplier = 1.5, ConvectionStrength = 20.0, RadiationStrength = 3.0, SpreadProbability = 0.05, CoreColorHex = "#FFFFFF", MidColorHex = "#FFD54F", OuterColorHex = "#FF9800", SmokeColorHex = "#212121", ProducesSmoke = true, ProducesSparks = false, ConsumesFuel = true, SelfSustaining = false, LeavesAsh = false } },
        { FireType.Campfire, new FireProfile { Name = "Campfire", BaseHeat = 600.0, HeatVelocityScale = 8.0, FuelCapacity = 150.0, BurnRate = 0.08, MinimumRadius = 8.0, MaxRadiusMultiplier = 2.5, ConvectionStrength = 40.0, RadiationStrength = 8.0, SpreadProbability = 0.25, CoreColorHex = "#FF5722", MidColorHex = "#FF9800", OuterColorHex = "#FFC107", SmokeColorHex = "#424242", ProducesSmoke = true, ProducesSparks = true, ConsumesFuel = true, SelfSustaining = false, LeavesAsh = true } },
        { FireType.Torch, new FireProfile { Name = "Torch", BaseHeat = 1000.0, HeatVelocityScale = 12.0, FuelCapacity = 60.0, BurnRate = 0.05, MinimumRadius = 6.0, MaxRadiusMultiplier = 2.0, ConvectionStrength = 35.0, RadiationStrength = 6.0, SpreadProbability = 0.15, CoreColorHex = "#FFD740", MidColorHex = "#FF9800", OuterColorHex = "#FF5722", SmokeColorHex = "#616161", ProducesSmoke = false, ProducesSparks = true, ConsumesFuel = true, SelfSustaining = false, LeavesAsh = false } },
        { FireType.Fireball, new FireProfile { Name = "Fireball", BaseHeat = 2000.0, HeatVelocityScale = 25.0, FuelCapacity = 50.0, BurnRate = 2.0, MinimumRadius = 15.0, MaxRadiusMultiplier = 3.0, ConvectionStrength = 80.0, RadiationStrength = 20.0, SpreadProbability = 0.4, CoreColorHex = "#FF0000", MidColorHex = "#FF9800", OuterColorHex = "#FFFF00", SmokeColorHex = "#333333", ProducesSmoke = false, ProducesSparks = true, ConsumesFuel = true, SelfSustaining = false, LeavesAsh = false } },
        { FireType.Inferno, new FireProfile { Name = "Inferno", BaseHeat = 3000.0, HeatVelocityScale = 40.0, FuelCapacity = 500.0, BurnRate = 1.5, MinimumRadius = 20.0, MaxRadiusMultiplier = 4.0, ConvectionStrength = 100.0, RadiationStrength = 30.0, SpreadProbability = 0.6, CoreColorHex = "#FF0000", MidColorHex = "#FF5722", OuterColorHex = "#FFC107", SmokeColorHex = "#212121", ProducesSmoke = true, ProducesSparks = true, ConsumesFuel = true, SelfSustaining = true, LeavesAsh = true } },
        { FireType.BlueFlame, new FireProfile { Name = "BlueFlame", BaseHeat = 2500.0, HeatVelocityScale = 15.0, FuelCapacity = 40.0, BurnRate = 0.3, MinimumRadius = 5.0, MaxRadiusMultiplier = 1.8, ConvectionStrength = 60.0, RadiationStrength = 15.0, SpreadProbability = 0.1, CoreColorHex = "#00BFFF", MidColorHex = "#1E90FF", OuterColorHex = "#4169E1", SmokeColorHex = "#2F2F2F", ProducesSmoke = false, ProducesSparks = false, ConsumesFuel = true, SelfSustaining = false, LeavesAsh = false } },
        { FireType.Dying, new FireProfile { Name = "Dying", BaseHeat = 200.0, HeatVelocityScale = 2.0, FuelCapacity = 5.0, BurnRate = 0.5, MinimumRadius = 2.0, MaxRadiusMultiplier = 1.2, ConvectionStrength = 5.0, RadiationStrength = 1.0, SpreadProbability = 0.0, CoreColorHex = "#8B4513", MidColorHex = "#A0522D", OuterColorHex = "#CD853F", SmokeColorHex = "#696969", ProducesSmoke = true, ProducesSparks = false, ConsumesFuel = true, SelfSustaining = false, LeavesAsh = true } },
        { FireType.Explosive, new FireProfile { Name = "Explosive", BaseHeat = 4000.0, HeatVelocityScale = 60.0, FuelCapacity = 100.0, BurnRate = 5.0, MinimumRadius = 25.0, MaxRadiusMultiplier = 5.0, ConvectionStrength = 150.0, RadiationStrength = 50.0, SpreadProbability = 0.8, CoreColorHex = "#FF0000", MidColorHex = "#FF4500", OuterColorHex = "#FFA500", SmokeColorHex = "#1C1C1C", ProducesSmoke = true, ProducesSparks = true, ConsumesFuel = true, SelfSustaining = false, LeavesAsh = false } },
        { FireType.Eternal, new FireProfile { Name = "Eternal", BaseHeat = 1000.0, HeatVelocityScale = 10.0, FuelCapacity = 9999.0, BurnRate = 0.0, MinimumRadius = 10.0, MaxRadiusMultiplier = 2.0, ConvectionStrength = 30.0, RadiationStrength = 10.0, SpreadProbability = 0.2, CoreColorHex = "#FFD700", MidColorHex = "#FF8C00", OuterColorHex = "#FF4500", SmokeColorHex = "#2F2F2F", ProducesSmoke = false, ProducesSparks = false, ConsumesFuel = false, SelfSustaining = true, LeavesAsh = false } },
        { FireType.Wildfire, new FireProfile { Name = "Wildfire", BaseHeat = 1500.0, HeatVelocityScale = 30.0, FuelCapacity = 1000.0, BurnRate = 0.5, MinimumRadius = 30.0, MaxRadiusMultiplier = 6.0, ConvectionStrength = 70.0, RadiationStrength = 25.0, SpreadProbability = 0.5, CoreColorHex = "#FF4500", MidColorHex = "#FF6347", OuterColorHex = "#FFD700", SmokeColorHex = "#2F2F2F", ProducesSmoke = true, ProducesSparks = true, ConsumesFuel = true, SelfSustaining = true, LeavesAsh = true } }
    };

    #endregion

    #region Particle & Sound Effect System

    public enum ParticleEffectType { FlameCore, FlameMid, FlameOuter, Smoke, Ember, Spark, Ash, HeatWave, FireTrail, IgnitionFlash }
    public enum SoundEffectType { Crackle, Pop, Roar, Hiss, Ignite, Extinguish, Spread, IntenseHeat, LowBurn, Fizzle }

    private class ParticleEffectRequest { public ParticleEffectType Type; public Vector2 Position; public Vector2 Velocity; public double Intensity; public double Lifetime; public double Size; public string Color; }
    private class SoundEffectRequest { public SoundEffectType Type; public Vector2 Position; public double Volume; public double Pitch; public double Radius; }

    private readonly Queue<ParticleEffectRequest> _pendingParticles = new();
    private readonly Queue<SoundEffectRequest> _pendingSounds = new();

    #endregion

    #region Instance State & Configuration

    private FireType _currentType = FireType.Campfire;
    private FireProfile _activeProfile = _fireProfiles[FireType.Campfire];
    private double _currentTemperature;
    private double _currentFuel;
    private double _burnTimer;
    private bool _isIgnited;
    private bool _isDying;
    private Vector2 _convectionDirection;
    private readonly HashSet<int> _spreadHistory = new();
    private double _flickerPhase;
    private double _sizeVariation;
    private double _intensityMultiplier = 1.0;
    private double _currentRadius;
    private bool _isInVacuumPrevFrame;
    private double _timeSinceLastSpread;

    #endregion

    #region Performance & Diagnostics

    private readonly Stopwatch _updateStopwatch = new Stopwatch();
    private double _peakTemperature;
    private double _totalHeatOutput;
    private double _totalFuelConsumed;
    private readonly List<Vector2> _trailPositions = new();
    private readonly Dictionary<int, double> _ignitedBodies = new();
    private int _ignitionsCaused;
    private int _bodiesIgnitedTotal;
    private double _totalSpreadDistance;
    private readonly Queue<double> _temperatureHistory = new();
    private const int MAX_TEMP_HISTORY = 60;
    private Vector2 _lastPosition;
    private readonly List<FireStateRecord> _stateHistory = new();
    private double _timeIgnited;
    private double _heatEmittedTotal;
    private double _convectionWorkDone;
    private readonly Dictionary<int, double> _ignitionChainDepth = new();
    private double _maxSizeAchieved;
    private double _minSizeAchieved = double.MaxValue;
    private double _averageTemperature;
    private int _temperatureSamples;
    private double _timeInSuperHotState;
    private double _timeInNormalState;
    private int _extinguishCount;
    private int _reigniteCount;
    private double _totalDistanceTraveled;
    private int _collisionCount;
    private readonly List<Vector2> _convectionVectors = new();
    private double _maxHeatForce;
    private double _totalWorkDone;
    private int _particleEmissionCounter;
    private int _particlesEmitted;

    #endregion

    #region Behavior Properties (Overrides)

    public override BodyType Type => BodyType.Fire;
    public override string Name => $"{_activeProfile.Name} Fire";
    public override string Description => $"{_activeProfile.Name} fire burning at {_currentTemperature:F0}K";
    public override string ColorHex => _activeProfile.CoreColorHex;
    public override double DefaultRadius => Math.Max(MIN_BURN_RADIUS, _activeProfile.MinimumRadius);
    public override double DefaultMass => Math.Clamp(_activeProfile.BaseHeat / 200.0, 0.5, 50.0);
    public override double DefaultRestitution => 0.1;

    #endregion

    #region Constructors

    public FireBehavior() : this(FireType.Campfire) { }
    public FireBehavior(FireType type)
    {
        _currentType = type;
        _activeProfile = _fireProfiles[type];
        _currentTemperature = _activeProfile.BaseHeat;
        _currentFuel = _activeProfile.FuelCapacity;
        _isIgnited = true;
        _convectionDirection = new Vector2(0, -1);
        _currentRadius = DefaultRadius;
        _maxSizeAchieved = DefaultRadius;
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        body.Restitution = DefaultRestitution;
        body.Mass = DefaultMass;
        body.Radius = DefaultRadius;
        _currentFuel = _activeProfile.FuelCapacity;
        _currentTemperature = _activeProfile.BaseHeat;
        _isIgnited = true;
        _isDying = false;
        _peakTemperature = _currentTemperature;
        _lastPosition = body.Position;
        _ignitionsCaused = 0;
        LogDebug(body, $"FireBehavior {Name} created: T={_currentTemperature:F0}K Fuel={_currentFuel:F1}");
    }

    #endregion

    #region Main Update Loop

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        _updateStopwatch.Restart();
        try
        {
            if (body.IsStatic || body.IsFrozen || !_isIgnited) return;
            RaisePreUpdate(body, dt);
            UpdateFuel(dt);
            UpdateTemperature(dt, world);
            ApplyFirePhysics(body, dt, world);
            ApplyConvection(body, dt);
            ApplyRadiation(body, world);
            UpdateFlickerAndSize(dt);
            SpreadFire(body, dt, world);
            UpdateIgnitionChainEffects(body, world, dt);
            EmitParticles(body, dt);
            UpdateTrail(body, dt);
            UpdateStateHistory(body, dt);
            UpdateStatistics(body, dt);
            CheckSelfExtinguishment(body, dt);
            ClampBodyProperties(body);
            RaisePostUpdate(body, dt);
        }
        finally { _updateStopwatch.Stop(); RecordPerformanceMetric("OnUpdate", _updateStopwatch.Elapsed.TotalMilliseconds); }
    }

    #endregion

    #region Fuel System

    private void UpdateFuel(double dt)
    {
        if (!_activeProfile.ConsumesFuel) return;
        double rate = _activeProfile.BurnRate * _intensityMultiplier * (1.0 + (_currentTemperature - _activeProfile.BaseHeat) / 1000.0);
        _currentFuel -= rate * dt;
        _totalFuelConsumed += rate * dt;
        if (_currentFuel <= 0) { _currentFuel = 0; _isDying = true; _activeProfile = _fireProfiles[FireType.Dying]; }
        _burnTimer += dt;
    }

    #endregion

    #region Temperature System

    private void UpdateTemperature(double dt, PhysicsWorld world)
    {
        double baseTemp = _activeProfile.BaseHeat;
        double fuelMod = _currentFuel / Math.Max(1.0, _activeProfile.FuelCapacity);
        double convBoost = _convectionDirection.Y < -0.5 ? _activeProfile.ConvectionStrength * 0.1 : 0.0;
        double intensityMod = _intensityMultiplier * 100.0;
        double target = baseTemp * fuelMod + intensityMod + convBoost;
        target = Math.Clamp(target, 200.0, _activeProfile.MaxTemperature);
        if (_isDying) target = Math.Max(200.0, target * 0.92);
        _currentTemperature += (target - _currentTemperature) * 5.0 * dt;
        _currentTemperature *= _activeProfile.HeatDecayRate;
        if (_currentTemperature > _peakTemperature) _peakTemperature = _currentTemperature;
        _temperatureHistory.Enqueue(_currentTemperature);
        if (_temperatureHistory.Count > 60) _temperatureHistory.Dequeue();
        _averageTemperature = (_averageTemperature * _temperatureSamples + _currentTemperature) / (_temperatureSamples + 1);
        _temperatureSamples++;
        if (_currentTemperature < IGNITION_TEMPERATURE * 0.5) { _isIgnited = false; _isDying = true; }
        if (_currentTemperature > _activeProfile.MaxTemperature * 0.75) _timeInSuperHotState += dt;
        else _timeInNormalState += dt;
    }

    #endregion

    #region Physics

    private void ApplyFirePhysics(RigidBody body, double dt, PhysicsWorld world)
    {
        double heatForce = _currentTemperature * _activeProfile.HeatVelocityScale * body.Mass / 1000.0;
        Vector2 flickerVec = new Vector2((Math.Sin(_flickerPhase * 3.0) * 0.2 - 0.1) * heatForce * 0.1, -heatForce - body.Mass * world.Gravity.Length);
        body.ApplyForce(flickerVec);
        double dragForce = body.Velocity.Length * 0.1 * body.Mass;
        body.ApplyForce(-body.Velocity.Normalized * dragForce);
        _convectionDirection = (_convectionDirection * 0.8 + new Vector2(Math.Sin(_flickerPhase * 3.0) * 0.2, -1.0).Normalized * 0.2).Normalized;
        _convectionVectors.Add(_convectionDirection);
        if (_convectionVectors.Count > 30) _convectionVectors.RemoveAt(0);
        _lastPosition = body.Position;
        double work = flickerVec.Length * body.Velocity.Length * dt;
        _totalWorkDone += work;
        if (heatForce > _maxHeatForce) _maxHeatForce = heatForce;
    }

    private void ApplyConvection(RigidBody body, double dt)
    {
        double convForce = _currentTemperature / 100.0 * _activeProfile.ConvectionStrength;
        Vector2 convVec = new Vector2((Math.Sin(_updateStopwatch.Elapsed.TotalSeconds * 3.0 + body.Id) * 0.3 + 0.1) * convForce, -convForce * 1.5);
        body.ApplyForce(convVec);
        _convectionWorkDone += convVec.Length * body.Velocity.Length * dt;
    }

    private void ApplyRadiation(RigidBody body, PhysicsWorld world)
    {
        double range = _currentRadius * 3.0;
        double strength = _currentTemperature / 100.0 * _activeProfile.RadiationStrength;
        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic) continue;
            double dist = (body.Position - other.Position).Length;
            if (dist > range || dist < 0.1) continue;
            double effect = strength * (1.0 - dist / range);
            other.ApplyForce((other.Position - body.Position).Normalized * effect);
        }
    }

    private void UpdateFlickerAndSize(double dt)
    {
        _flickerPhase = (_flickerPhase + FLICKER_SPEED * dt) % 1.0;
        double flicker = Math.Sin(_flickerPhase * Math.PI * 2) * 0.15 + Math.Sin(_flickerPhase * Math.PI * 2 * 2.3) * 0.05 + Math.Sin(_flickerPhase * Math.PI * 2 * 5.7) * 0.02;
        _sizeVariation = 1.0 + flicker * _intensityMultiplier;
        _currentRadius = DefaultRadius * _sizeVariation * Math.Clamp(_currentFuel / Math.Max(1.0, _activeProfile.FuelCapacity) * 2.0, 0.3, 2.0);
        _intensityMultiplier = 0.98 * _intensityMultiplier + 0.02 * (1.0 + _currentTemperature / Math.Max(1.0, _peakTemperature) * 0.5);
        if (_currentRadius > _maxSizeAchieved) _maxSizeAchieved = _currentRadius;
        if (_currentRadius < _minSizeAchieved) _minSizeAchieved = _currentRadius;
    }

    #endregion

    #region Fire Spread

    private void SpreadFire(RigidBody body, double dt, PhysicsWorld world)
    {
        if (!_activeProfile.CanSpread || _isDying || _currentTemperature < IGNITION_TEMPERATURE) return;
        _timeSinceLastSpread += dt;
        double spreadChance = _activeProfile.SpreadProbability * dt * _intensityMultiplier;
        if (_currentFuel / Math.Max(1.0, _activeProfile.FuelCapacity) < 0.2) spreadChance *= 0.5;
        double spreadRange = _currentRadius * _activeProfile.MaxRadiusMultiplier * 2.0;
        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic || _spreadHistory.Contains(other.Id)) continue;
            double dist = (body.Position - other.Position).Length;
            if (dist > spreadRange) continue;
            if (new Random(body.Id + other.Id).NextDouble() < spreadChance * (1.0 - dist / spreadRange))
            {
                IgniteBody(other, world, 1);
                _spreadHistory.Add(other.Id);
                _totalSpreadDistance += dist;
                _timeSinceLastSpread = 0;
                LogDebug(body, $"Spread fire to body {other.Id} at dist {dist:F1}");
            }
        }
    }

    private void IgniteBody(RigidBody body, PhysicsWorld world, int depth)
    {
        if (_ignitedBodies.ContainsKey(body.Id)) return;
        _ignitedBodies[body.Id] = 0.0;
        _ignitionChainDepth[body.Id] = depth;
        _bodiesIgnitedTotal++;
        if (depth == 1) _ignitionsCaused++;
        body.Restitution = Math.Max(0.05, body.Restitution * 0.5);
        _spreadHistory.Add(body.Id);
    }

    #endregion

    #region Ignition Effects

    private void UpdateIgnitionChainEffects(RigidBody body, PhysicsWorld world, double dt)
    {
        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic) continue;
            double dist = (body.Position - other.Position).Length;
            if (dist > _currentRadius * 4.0) continue;
            if (_currentTemperature > BODY_IGNITION_THRESHOLD && !_ignitedBodies.ContainsKey(other.Id))
            {
                double heatTransfer = _currentTemperature * HEAT_TRANSMISSION_FACTOR / Math.Max(1.0, dist) * dt;
                _heatEmittedTotal += heatTransfer;
            }
        }
    }

    private void EmitParticles(RigidBody body, double dt)
    {
        _particleEmissionCounter++;
        if (_particleEmissionCounter < 3) return;
        _particleEmissionCounter = 0;
        if (_isInVacuumPrevFrame || dt > 0.1) return;
        int count = (int)(PARTICLE_EMISSION_RATE * _intensityMultiplier * dt);
        for (int i = 0; i < count; i++)
        {
            _pendingParticles.Enqueue(new ParticleEffectRequest
            {
                Type = ParticleEffectType.FlameCore,
                Position = body.Position + new Vector2((new Random().NextDouble() - 0.5) * _currentRadius, (new Random().NextDouble() - 0.5) * _currentRadius),
                Velocity = new Vector2((new Random().NextDouble() - 0.5) * 20, -new Random().NextDouble() * 30 - 10),
                Intensity = _intensityMultiplier,
                Lifetime = 0.5 + new Random().NextDouble(),
                Size = _currentRadius * (0.1 + new Random().NextDouble() * 0.2),
                Color = _activeProfile.CoreColorHex
            });
            _particlesEmitted++;
        }
    }

    #endregion

    #region Trail & History

    private void UpdateTrail(RigidBody body, double dt)
    {
        double dist = (body.Position - _lastPosition).Length;
        if (dist > _currentRadius * 0.2)
        {
            _trailPositions.Add(body.Position);
            while (_trailPositions.Count > 20) _trailPositions.RemoveAt(0);
            _lastPosition = body.Position;
        }
    }

    private void UpdateStateHistory(RigidBody body, double dt)
    {
        _stateHistory.Add(new FireStateRecord { Time = _burnTimer, Temperature = _currentTemperature, Fuel = _currentFuel, Radius = _currentRadius, State = _isDying ? "Dying" : _isIgnited ? "Burning" : "Extinguished" });
        while (_stateHistory.Count > 100) _stateHistory.RemoveAt(0);
    }

    #endregion

    #region Statistics

    private void UpdateStatistics(RigidBody body, double dt)
    {
        double speed = (body.Position - _lastPosition).Length / Math.Max(0.001, dt);
        _totalDistanceTraveled += speed * dt;
        if (speed > 1.0) _collisionCount++;
        _totalHeatOutput += _currentTemperature * dt;
    }

    private void CheckSelfExtinguishment(RigidBody body, double dt)
    {
        if (_currentFuel <= 0) _isDying = true;
        if (_isDying && _currentTemperature < 250.0) { _isIgnited = false; _extinguishCount++; }
        if (!_isIgnited && _currentFuel > 10.0 && _currentTemperature > 300.0) { _isIgnited = true; _isDying = false; _reigniteCount++; }
        if (_currentRadius < MIN_BURN_RADIUS * 0.5) _isIgnited = false;
    }

    private void ClampBodyProperties(RigidBody body)
    {
        double maxRadius = Math.Max(MIN_BURN_RADIUS, _currentRadius * MAX_RADIUS_TEMP_MULTIPLIER);
        body.Radius = Math.Clamp(_currentRadius, MIN_BURN_RADIUS, maxRadius * 2);
        if (body.Velocity.Length > 1000) body.Velocity = body.Velocity.Normalized * 1000;
        if (double.IsNaN(body.Position.X) || double.IsNaN(body.Position.Y))
        {
            body.Position = _lastPosition;
            body.Velocity = Vector2.Zero;
        }
    }

    #endregion

    #region Public API

    public void SetFireType(FireType type) { _currentType = type; _activeProfile = _fireProfiles[type]; _currentTemperature = _activeProfile.BaseHeat; _peakTemperature = _currentTemperature; }
    public void SetFuel(double fuel) { _currentFuel = Math.Clamp(fuel, 0, _activeProfile.FuelCapacity); }
    public void SetTemperature(double temp) { _currentTemperature = Math.Clamp(temp, 200, _activeProfile.MaxTemperature); }
    public void Ignite() { _isIgnited = true; _isDying = false; _timeIgnited = 0; }
    public void Extinguish() { _isIgnited = false; _isDying = true; }
    public void TriggerExplosion() { _currentTemperature *= 1.5; _currentFuel *= 0.5; _intensityMultiplier *= 2.0; }
    public void SetSpreadEnabled(bool enabled) { _activeProfile.CanSpread = enabled; }
    public void SetVacuum(bool inVacuum) { _isInVacuumPrevFrame = inVacuum; }

    public FireType GetFireType() => _currentType;
    public double GetTemperature() => _currentTemperature;
    public double GetFuel() => _currentFuel;
    public double GetRadius() => _currentRadius;
    public bool IsIgnited() => _isIgnited;
    public bool IsDying() => _isDying;
    public int GetIgnitionsCaused() => _ignitionsCaused;
    public int GetBodiesIgnitedTotal() => _bodiesIgnitedTotal;
    public double GetTotalSpreadDistance() => _totalSpreadDistance;
    public double GetPeakTemperature() => _peakTemperature;
    public double GetAverageTemperature() => _averageTemperature;
    public double GetTotalHeatOutput() => _totalHeatOutput;
    public double GetTotalFuelConsumed() => _totalFuelConsumed;
    public double GetMaxSizeAchieved() => _maxSizeAchieved;
    public double GetMinSizeAchieved() => _minSizeAchieved;
    public double GetTimeIgnited() => _timeIgnited;
    public double GetMaxHeatForce() => _maxHeatForce;
    public double GetTotalWorkDone() => _totalWorkDone;
    public double GetConvectionWorkDone() => _convectionWorkDone;
    public double GetHeatEmittedTotal() => _heatEmittedTotal;
    public double GetIntensityMultiplier() => _intensityMultiplier;
    public double GetTimeInSuperHotState() => _timeInSuperHotState;
    public double GetTimeInNormalState() => _timeInNormalState;
    public int GetExtinguishCount() => _extinguishCount;
    public int GetReigniteCount() => _reigniteCount;
    public double GetTotalDistanceTraveled() => _totalDistanceTraveled;
    public int GetCollisionCount() => _collisionCount;
    public int GetParticlesEmitted() => _particlesEmitted;
    public IReadOnlyList<Vector2> GetTrail() => _trailPositions.AsReadOnly();
    public IReadOnlyList<Vector2> GetConvectionVectors() => _convectionVectors.AsReadOnly();
    public IReadOnlyList<FireStateRecord> GetStateHistory() => _stateHistory.AsReadOnly();
    public Queue<double> GetTemperatureHistory() => new Queue<double>(_temperatureHistory);
    public FireProfile? GetProfileForType(FireType type) => _fireProfiles.TryGetValue(type, out var p) ? p : null;
    public static List<FireProfile> GetAllFireProfiles() => new List<FireProfile>(_fireProfiles.Values);
    public static Dictionary<FireType, FireProfile> GetFireProfilesDict() => new Dictionary<FireType, FireProfile>(_fireProfiles);
    public FireProfile GetActiveProfile() => _activeProfile;
    public void SetActiveProfile(FireProfile profile) { _activeProfile = profile; _currentType = (FireType)99; }
    public PerformanceMetrics GetPerformanceMetrics() => new PerformanceMetrics { AverageUpdateTime = _updateStopwatch.Elapsed.TotalMilliseconds, PeakTemperature = _peakTemperature, Bounces = _collisionCount, TotalHeat = _totalHeatOutput };

    public class FireStateRecord { public double Time; public double Temperature; public double Fuel; public double Radius; public string State; }
    public class PerformanceMetrics { public double AverageUpdateTime; public double PeakTemperature; public int Bounces; public double TotalHeat; }

    #endregion

    #region Diagnostics & Serialization

    public string GetDiagnosticsReport(RigidBody body)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== FireBehavior Diagnostics ===");
        sb.AppendLine($"Type: {_currentType}");
        sb.AppendLine($"Profile: {_activeProfile.Name}");
        sb.AppendLine($"Temperature: {_currentTemperature:F1}K (Peak: {_peakTemperature:F1}K)");
        sb.AppendLine($"Fuel: {_currentFuel:F1}/{_activeProfile.FuelCapacity:F1}");
        sb.AppendLine($"Radius: {_currentRadius:F2} (Min: {_minSizeAchieved:F2}, Max: {_maxSizeAchieved:F2})");
        sb.AppendLine($"Ignited: {_isIgnited}, Dying: {_isDying}");
        sb.AppendLine($"Burn Time: {_burnTimer:F1}s");
        sb.AppendLine($"Ignitions Caused: {_ignitionsCaused}");
        sb.AppendLine($"Bodies Ignited: {_bodiesIgnitedTotal}");
        sb.AppendLine($"Spread Distance: {_totalSpreadDistance:F1}");
        sb.AppendLine($"Avg Temp: {_averageTemperature:F1}K");
        sb.AppendLine($"Total Heat: {_totalHeatOutput:F1}");
        sb.AppendLine($"Total Fuel Used: {_totalFuelConsumed:F1}");
        sb.AppendLine($"Distance Traveled: {_totalDistanceTraveled:F1}");
        sb.AppendLine($"Collisions: {_collisionCount}");
        sb.AppendLine($"Extinguish/Reignite: {_extinguishCount}/{_reigniteCount}");
        sb.AppendLine($"Particles Emitted: {_particlesEmitted}");
        sb.AppendLine($"Max Heat Force: {_maxHeatForce:F1}");
        sb.AppendLine($"Total Work: {_totalWorkDone:F1}");
        sb.AppendLine($"Intensity: {_intensityMultiplier:F2}x");
        sb.AppendLine($"Convection Vectors: {_convectionVectors.Count}");
        sb.AppendLine($"Trail Positions: {_trailPositions.Count}");
        sb.AppendLine($"State History: {_stateHistory.Count}");
        return sb.ToString();
    }

    public string SerializeState()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Type:{_currentType}");
        sb.AppendLine($"Temperature:{_currentTemperature}");
        sb.AppendLine($"Fuel:{_currentFuel}");
        sb.AppendLine($"PeakTemp:{_peakTemperature}");
        sb.AppendLine($"Ignited:{_isIgnited}");
        sb.AppendLine($"Dying:{_isDying}");
        sb.AppendLine($"BurnTimer:{_burnTimer}");
        sb.AppendLine($"Intensity:{_intensityMultiplier}");
        sb.AppendLine($"IgnitionsCaused:{_ignitionsCaused}");
        sb.AppendLine($"BodiesIgnited:{_bodiesIgnitedTotal}");
        sb.AppendLine($"SpreadDistance:{_totalSpreadDistance}");
        sb.AppendLine($"TotalHeat:{_totalHeatOutput}");
        sb.AppendLine($"FuelConsumed:{_totalFuelConsumed}");
        sb.AppendLine($"DistanceTraveled:{_totalDistanceTraveled}");
        sb.AppendLine($"Collisions:{_collisionCount}");
        sb.AppendLine($"ExtinguishCount:{_extinguishCount}");
        sb.AppendLine($"ReigniteCount:{_reigniteCount}");
        sb.AppendLine($"ParticlesEmitted:{_particlesEmitted}");
        sb.AppendLine($"AvgTemperature:{_averageTemperature}");
        sb.AppendLine($"MaxSize:{_maxSizeAchieved}");
        sb.AppendLine($"MinSize:{_minSizeAchieved}");
        return sb.ToString();
    }

    public void DeserializeState(string state)
    {
        string[] lines = state.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            string[] parts = line.Split(':');
            if (parts.Length != 2) continue;
            try
            {
                switch (parts[0])
                {
                    case "Type": if (Enum.TryParse(parts[1], out FireType ft)) _currentType = ft; break;
                    case "Temperature": _currentTemperature = double.Parse(parts[1]); break;
                    case "Fuel": _currentFuel = double.Parse(parts[1]); break;
                    case "PeakTemp": _peakTemperature = double.Parse(parts[1]); break;
                    case "Ignited": _isIgnited = bool.Parse(parts[1]); break;
                    case "Dying": _isDying = bool.Parse(parts[1]); break;
                    case "BurnTimer": _burnTimer = double.Parse(parts[1]); break;
                    case "Intensity": _intensityMultiplier = double.Parse(parts[1]); break;
                    case "IgnitionsCaused": _ignitionsCaused = int.Parse(parts[1]); break;
                    case "BodiesIgnited": _bodiesIgnitedTotal = int.Parse(parts[1]); break;
                    case "SpreadDistance": _totalSpreadDistance = double.Parse(parts[1]); break;
                    case "TotalHeat": _totalHeatOutput = double.Parse(parts[1]); break;
                    case "FuelConsumed": _totalFuelConsumed = double.Parse(parts[1]); break;
                    case "DistanceTraveled": _totalDistanceTraveled = double.Parse(parts[1]); break;
                    case "Collisions": _collisionCount = int.Parse(parts[1]); break;
                    case "ExtinguishCount": _extinguishCount = int.Parse(parts[1]); break;
                    case "ReigniteCount": _reigniteCount = int.Parse(parts[1]); break;
                    case "ParticlesEmitted": _particlesEmitted = int.Parse(parts[1]); break;
                    case "AvgTemperature": _averageTemperature = double.Parse(parts[1]); break;
                    case "MaxSize": _maxSizeAchieved = double.Parse(parts[1]); break;
                    case "MinSize": _minSizeAchieved = double.Parse(parts[1]); break;
                }
            }
            catch { }
        }
        if (_fireProfiles.TryGetValue(_currentType, out var profile))
            _activeProfile = profile;
    }

    protected override void RenderDebugOverlay(RigidBody body, System.Windows.Media.DrawingContext dc) { }

    #endregion
}
