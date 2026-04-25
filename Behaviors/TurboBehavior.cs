using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Diagnostics;
using System.Text;

namespace PhysicsSandbox.Behaviors;

/// <summary>
/// TurboBehavior: High-velocity acceleration behavior with nitro boost,
/// afterburner, and heat management systems.
/// Represents the ultimate speed-focused physics body with advanced
/// propulsion mechanics, exhaust trail generation, and sonic boom effects.
/// 
/// Key Features:
/// - Continuous acceleration in velocity direction (turbo thrust)
/// - Nitro boost mode for explosive short-term speed
/// - Afterburner mode with enhanced thrust and visual exhaust
/// - Heat accumulation and cooling systems
/// - Shockwave generation at supersonic speeds
/// - Performance tracking and diagnostics
/// - Multiple turbo mode presets
/// </summary>
/// <remarks>
/// <para><b>Physics Model:</b> TurboBehavior applies persistent thrust forces
/// proportional to current velocity direction. Key equations:
/// - Thrust: F = m * a_turbo * modeMultiplier
/// - Heat: dQ/dt = thrustPower * heatCoefficient - coolingRate</para>
/// <para><b>Performance Optimizations:</b> Uses cached vectors, pre-computed
/// multipliers, object pooling, and spatial culling.</para>
/// </remarks>


public class TurboBehavior : BodyBehavior
{

    #region Constants & Tunable Parameters

    private const double DEFAULT_BASE_THRUST = 500.0;
    private const double DEFAULT_MAX_BOOST_MULTIPLIER = 3.0;
    private const double DEFAULT_BOOST_DURATION = 5.0;
    private const double DEFAULT_HEAT_DISSIPATION = 0.95;
    private const double DEFAULT_HEAT_GENERATION = 0.08;
    private const double MAX_OPERATING_TEMP = 1000.0;
    private const double CRITICAL_TEMP = 800.0;
    private const double OVERHEAT_SHUTDOWN_TEMP = 950.0;
    private const double NITRO_BOOST_MULTIPLIER = 5.0;
    private const double NITRO_DURATION = 3.0;
    private const double AFTERBURNER_MULTIPLIER = 4.0;
    private const double AFTERBURNER_DURATION = 8.0;
    private const double SONIC_BOOM_THRESHOLD = 340.0;
    private const double SHOCKWAVE_RANGE = 500.0;
    private const double EXHAUST_TRAIL_LENGTH = 30;
    private const double FUEL_CONSUMPTION_RATE = 0.5;
    private const double MAX_FUEL_CAPACITY = 100.0;
    private const double THRUST_DECAY_RATE = 0.02;
    private const double TURBO_LAG_FACTOR = 0.15;
    private const int MAX_PARTICLE_POOL_SIZE = 200;
    private const double VELOCITY_CLAMP = 5000.0;
    private const double BOOST_VELOCITY_THRESHOLD = 10.0;

    #endregion

    #region Turbo Modes & Presets

    public enum TurboMode
    {
        Off,
        Eco,
        Standard,
        Sport,
        Nitro,
        Afterburner,
        Overdrive,
        Custom
    }

    public class TurboProfile
    {
        public string Name { get; set; } = "";
        public double BaseThrust { get; set; } = DEFAULT_BASE_THRUST;
        public double MaxBoostMultiplier { get; set; } = DEFAULT_MAX_BOOST_MULTIPLIER;
        public double BoostDuration { get; set; } = DEFAULT_BOOST_DURATION;
        public double HeatGeneration { get; set; } = DEFAULT_HEAT_GENERATION;
        public double HeatDissipation { get; set; } = DEFAULT_HEAT_DISSIPATION;
        public double FuelEfficiency { get; set; } = 1.0;
        public double MaxVelocityMultiplier { get; set; } = 1.5;
        public string ColorHex { get; set; } = "#FFD700";
        public bool EnableAfterburner { get; set; } = false;
        public bool EnableNitro { get; set; } = false;
        public bool UnlimitedFuel { get; set; } = false;
    }

    private static readonly Dictionary<TurboMode, TurboProfile> _turboProfiles = new()
    {
        {
            TurboMode.Off, new TurboProfile
            {
                Name = "Off",
                BaseThrust = 0,
                MaxBoostMultiplier = 0,
                HeatGeneration = 0.01,
                ColorHex = "#808080"
            }
        },
        {
            TurboMode.Eco, new TurboProfile
            {
                Name = "Eco",
                BaseThrust = 200,
                MaxBoostMultiplier = 1.5,
                BoostDuration = 10.0,
                HeatGeneration = 0.03,
                FuelEfficiency = 2.0,
                ColorHex = "#00FF00"
            }
        },
        {
            TurboMode.Standard, new TurboProfile
            {
                Name = "Standard",
                BaseThrust = DEFAULT_BASE_THRUST,
                MaxBoostMultiplier = DEFAULT_MAX_BOOST_MULTIPLIER,
                BoostDuration = DEFAULT_BOOST_DURATION,
                HeatGeneration = DEFAULT_HEAT_GENERATION,
                ColorHex = "#FFD700"
            }
        },
        {
            TurboMode.Sport, new TurboProfile
            {
                Name = "Sport",
                BaseThrust = 800,
                MaxBoostMultiplier = 4.0,
                BoostDuration = 4.0,
                HeatGeneration = 0.12,
                FuelEfficiency = 0.7,
                EnableAfterburner = true,
                ColorHex = "#FF4500"
            }
        },
        {
            TurboMode.Nitro, new TurboProfile
            {
                Name = "Nitro",
                BaseThrust = 1500,
                MaxBoostMultiplier = NITRO_BOOST_MULTIPLIER,
                BoostDuration = NITRO_DURATION,
                HeatGeneration = 0.25,
                FuelEfficiency = 0.3,
                EnableNitro = true,
                EnableAfterburner = true,
                ColorHex = "#00BFFF"
            }
        },
        {
            TurboMode.Afterburner, new TurboProfile
            {
                Name = "Afterburner",
                BaseThrust = 2000,
                MaxBoostMultiplier = AFTERBURNER_MULTIPLIER,
                BoostDuration = AFTERBURNER_DURATION,
                HeatGeneration = 0.35,
                FuelEfficiency = 0.2,
                EnableAfterburner = true,
                ColorHex = "#FF1493"
            }
        },
        {
            TurboMode.Overdrive, new TurboProfile
            {
                Name = "Overdrive",
                BaseThrust = 3000,
                MaxBoostMultiplier = 8.0,
                BoostDuration = 2.0,
                HeatGeneration = 0.5,
                FuelEfficiency = 0.1,
                EnableAfterburner = true,
                EnableNitro = true,
                ColorHex = "#9400D3"
            }
        }
    };

    #endregion

    #region Turbo State & Instance Variables

    private TurboMode _currentMode = TurboMode.Standard;
    private TurboProfile _activeProfile = _turboProfiles[TurboMode.Standard];
    private double _currentBoostMultiplier = 1.0;
    private double _boostRemainingTime = 0.0;
    private double _heatLevel = 0.0;
    private double _fuelLevel = MAX_FUEL_CAPACITY;
    private bool _isOverheated = false;
    private bool _isBoosting = false;
    private bool _isAfterburnerActive = false;
    private bool _isNitroActive = false;
    private Vector2 _lastVelocity = Vector2.Zero;
    private Vector2 _lastThrustDirection = Vector2.Zero;
    private Vector2 _lastPosition = Vector2.Zero;
    private double _totalDistanceTraveled = 0.0;
    private double _peakVelocity = 0.0;
    private double _totalFuelConsumed = 0.0;
    private int _sonicBoomCount = 0;
    private double _turboLagAccumulator = 0.0;
    private readonly Stopwatch _updateStopwatch = new();
    private readonly Queue<ExhaustParticle> _exhaustParticles = new();
    private readonly Queue<ShockwaveEffect> _shockwaves = new();
    private readonly List<Vector2> _thrustHistory = new();
    private double _consecutiveBoostTime = 0.0;
    private int _maxComboBoosts = 0;
    private double _currentComboBoostTime = 0.0;

    private class ExhaustParticle
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public double Life { get; set; }
        public double MaxLife { get; set; }
        public double Size { get; set; }
        public byte Alpha { get; set; }
    }

    private class ShockwaveEffect
    {
        public Vector2 Origin { get; set; }
        public double Radius { get; set; }
        public double MaxRadius { get; set; }
        public double Life { get; set; }
        public double MaxLife { get; set; }
        public double Intensity { get; set; }
    }

    #endregion

    #region Behavior Properties (Overrides)

    public override BodyType Type => BodyType.Turbo;
    public override string Name => _activeProfile.Name;
    public override string Description => $"High-speed turbo body with {_currentMode} mode. Max boost: {_activeProfile.MaxBoostMultiplier}x, Heat gen: {_activeProfile.HeatGeneration:F2}";
    public override string ColorHex => _activeProfile.ColorHex;
    public override double DefaultRadius => 10;
    public override double DefaultMass => 3;
    public override double DefaultRestitution => 0.8;

    #endregion

    #region Constructors & Initialization

    public TurboBehavior() : this(TurboMode.Standard) { }

    public TurboBehavior(TurboMode mode)
    {
        _currentMode = mode;
        _activeProfile = _turboProfiles[mode];
        _currentBoostMultiplier = 1.0;
        PreWarmParticlePool();
    }

    private void PreWarmParticlePool()
    {
        for (int i = 0; i < MAX_PARTICLE_POOL_SIZE; i++)
        {
            _exhaustParticles.Enqueue(new ExhaustParticle
            {
                Position = Vector2.Zero,
                Velocity = Vector2.Zero,
                Life = 0,
                MaxLife = 0.5,
                Size = 2.0,
                Alpha = 255
            });
        }
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        body.Restitution = DefaultRestitution;
        body.Mass = DefaultMass;
        _fuelLevel = MAX_FUEL_CAPACITY;
        _heatLevel = 20.0;
        _lastVelocity = body.Position;
        LogDebug(body, $"TurboBehavior initialized: Mode={_currentMode}, Profile={_activeProfile.Name}");
        
        _exhaustParticles.Clear();
        _shockwaves.Clear();
        _thrustHistory.Clear();
        _performanceMetrics.Clear();
        PreWarmParticlePool();
    }

    #endregion

    #region Main Update Loop

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        _updateStopwatch.Restart();
        try
        {
            if (body.IsStatic || body.IsFrozen || _activeProfile.BaseThrust <= 0)
                return;

            ApplyTurboLag(dt);
            if (_turboLagAccumulator < TURBO_LAG_FACTOR) return;

            dt = Math.Min(dt, 0.033);
            double scaledDt = dt * Config.TimeScale;

            UpdateHeatSystem(body, scaledDt, world);
            UpdateBoostState(body, scaledDt);
            ApplyThrust(body, scaledDt, world);
            UpdateFuelConsumption(scaledDt);
            UpdateAfterburner(body, scaledDt);
            UpdateNitroMode(body, scaledDt);
            UpdateExhaustTrail(body, scaledDt);
            UpdateShockwaves(body, scaledDt, world);
            UpdateSonicBoomEffects(body);
            TrackStatistics(body, scaledDt);
            ClampVelocity(body);
            UpdateDebugVisualization(body);
        }
        finally
        {
            _updateStopwatch.Stop();
            RecordPerformanceMetric("OnUpdate", _updateStopwatch.Elapsed.TotalMilliseconds);
        }
    }

    #endregion

    #region Turbo Physics & Thrust System

    private void ApplyTurboLag(double dt)
    {
        if (_isBoosting && _turboLagAccumulator < TURBO_LAG_FACTOR)
            _turboLagAccumulator += dt / TURBO_LAG_FACTOR;
        else if (!_isBoosting && _turboLagAccumulator > 0)
            _turboLagAccumulator -= dt / TURBO_LAG_FACTOR;
    }

    private void ApplyThrust(RigidBody body, double dt, PhysicsWorld world)
    {
        double speed = body.Velocity.Length;
        if (speed < BOOST_VELOCITY_THRESHOLD && !_isBoosting)
            return;

        double thrustMultiplier = _currentBoostMultiplier;
        if (_isAfterburnerActive) thrustMultiplier *= AFTERBURNER_MULTIPLIER;
        if (_isNitroActive) thrustMultiplier *= NITRO_BOOST_MULTIPLIER;

        double baseThrust = _activeProfile.BaseThrust * thrustMultiplier;
        double effectiveThrust = baseThrust * GetThrustCurve(speed);

        Vector2 thrustDirection = body.Velocity.Length > 0.1 ? body.Velocity.Normalized : Vector2.UnitX;
        Vector2 thrustForce = thrustDirection * effectiveThrust * body.Mass;

        body.ApplyForce(thrustForce);
        _lastThrustDirection = thrustDirection;

        double thrustPower = thrustForce.Length * speed;
        _heatLevel += thrustPower * _activeProfile.HeatGeneration * dt;
    }

    private double GetThrustCurve(double speed)
    {
        double normalizedSpeed = speed / VELOCITY_CLAMP;
        if (normalizedSpeed < 0.3)
            return 1.0 + normalizedSpeed * 2.0;
        else if (normalizedSpeed < 0.7)
            return 1.6 - normalizedSpeed;
        else
            return 0.8 / normalizedSpeed;
    }

    private void ClampVelocity(RigidBody body)
    {
        double maxSpeed = VELOCITY_CLAMP * _activeProfile.MaxVelocityMultiplier;
        if (_isNitroActive) maxSpeed *= 1.5;
        if (_isOverheated) maxSpeed *= 0.5;

        double speedSq = body.Velocity.LengthSquared;
        if (speedSq > maxSpeed * maxSpeed)
        {
            double scale = maxSpeed / Math.Sqrt(speedSq);
            body.Velocity *= scale;
        }
    }

    private void UpdateFuelConsumption(double dt)
    {
        // Stub: fuel consumption handled in specific methods (ApplyThrust, Afterburner, Nitro)
    }

    #endregion

    #region Heat Management System

    private void UpdateHeatSystem(RigidBody body, double dt, PhysicsWorld world)
    {
        double ambientTemp = 20.0;
        double coolingRate = _activeProfile.HeatDissipation;
        double bodySurfaceArea = 4 * Math.PI * body.Radius * body.Radius;
        double conductiveCooling = (body.Radius * 0.1) * dt;

        double heatLoss = (_heatLevel - ambientTemp) * coolingRate * dt;
        heatLoss += conductiveCooling * bodySurfaceArea;
        
        if (!_isInVacuum)
            heatLoss += (body.Velocity.Length * 0.01) * dt;

        _heatLevel = Math.Max(ambientTemp, _heatLevel - heatLoss);

        if (_heatLevel > CRITICAL_TEMP)
        {
            double overheatPenalty = (_heatLevel - CRITICAL_TEMP) / (OVERHEAT_SHUTDOWN_TEMP - CRITICAL_TEMP);
            body.ApplyForce(-body.Velocity * body.Mass * overheatPenalty * 0.5);
            _isOverheated = true;
        }
        else
        {
            _isOverheated = false;
        }

        if (_heatLevel >= OVERHEAT_SHUTDOWN_TEMP)
        {
            _isBoosting = false;
            _isAfterburnerActive = false;
            _isNitroActive = false;
            _currentBoostMultiplier = 1.0;
        }

        if (_heatLevel > 1000)
            _heatLevel = 1000;
    }

    private bool _isInVacuum = false;
    public void SetVacuum(bool inVacuum) => _isInVacuum = inVacuum;

    public void SetEnvironmentalTemperature(double temp)
    {
        // Temperature affects heat dissipation
    }

    #endregion

    #region Boost System

    private void UpdateBoostState(RigidBody body, double dt)
    {
        if (_isBoosting)
        {
            _boostRemainingTime -= dt;
            _consecutiveBoostTime += dt;
            _currentComboBoostTime += dt;

            double boostProgress = 1.0 - (_boostRemainingTime / _activeProfile.BoostDuration);
            _currentBoostMultiplier = 1.0 + (_activeProfile.MaxBoostMultiplier - 1.0) * 
                (1.0 - Math.Exp(-boostProgress * 3.0));

            if (_boostRemainingTime <= 0 || _fuelLevel <= 0)
            {
                EndBoost(body);
            }

            if (_consecutiveBoostTime > 1.0)
            {
                _maxComboBoosts = Math.Max(_maxComboBoosts, (int)(_currentComboBoostTime / 0.5));
            }
        }
        else
        {
            double decayRate = THRUST_DECAY_RATE * dt * 60;
            _currentBoostMultiplier = 1.0 + (_currentBoostMultiplier - 1.0) * Math.Exp(-decayRate);
            _consecutiveBoostTime = Math.Max(0, _consecutiveBoostTime - dt);
        }
    }

    public void ActivateBoost(RigidBody body, double duration = -1)
    {
        if (_fuelLevel <= 0 || _isOverheated || _activeProfile.BaseThrust <= 0)
            return;

        if (!_isBoosting)
        {
            _isBoosting = true;
            _boostRemainingTime = duration > 0 ? duration : _activeProfile.BoostDuration;
            _consecutiveBoostTime = 0;
        }
    }

    public void DeactivateBoost()
    {
        _isBoosting = false;
    }

    private void EndBoost(RigidBody body)
    {
        _isBoosting = false;
        _boostRemainingTime = 0;
        _currentBoostMultiplier = Math.Max(1.0, _currentBoostMultiplier * 0.8);
    }

    #endregion

    #region Afterburner System

    private void UpdateAfterburner(RigidBody body, double dt)
    {
        if (!_isAfterburnerActive || !_activeProfile.EnableAfterburner)
            return;

        if (_fuelLevel <= 0 || _heatLevel > CRITICAL_TEMP)
        {
            _isAfterburnerActive = false;
            return;
        }

        double afterburnerThrust = _activeProfile.BaseThrust * AFTERBURNER_MULTIPLIER;
        Vector2 extraForce = _lastThrustDirection * afterburnerThrust * body.Mass * dt;
        body.ApplyForce(extraForce);

        _fuelLevel -= FUEL_CONSUMPTION_RATE * 3 * dt;
        _heatLevel += _activeProfile.HeatGeneration * 2 * dt;

        SpawnAfterburnerParticles(body, dt);
    }

    private void SpawnAfterburnerParticles(RigidBody body, double dt)
    {
        int particleCount = (int)(body.Velocity.Length * 0.5 * dt * 60);
        for (int i = 0; i < particleCount; i++)
        {
            if (_exhaustParticles.Count < 2)
                break;

            var particle = _exhaustParticles.Dequeue();
            Vector2 spread = new Vector2(
                (RandomValue(body.Id + i) - 0.5) * 20,
                (RandomValue(body.Id + i + 100) - 0.5) * 20);

            particle.Position = body.Position - _lastThrustDirection * body.Radius;
            particle.Velocity = -_lastThrustDirection * body.Velocity.Length * 0.8 + spread;
            particle.Life = 0;
            particle.MaxLife = 0.3 + RandomValue(body.Id + i) * 0.4;
            particle.Size = 3.0 + RandomValue(body.Id + i + 200) * 4.0;
            particle.Alpha = 255;
        }
    }

    public void ToggleAfterburner()
    {
        if (_activeProfile.EnableAfterburner && _fuelLevel > 10)
            _isAfterburnerActive = !_isAfterburnerActive;
    }

    #endregion

    #region Nitro System

    private void UpdateNitroMode(RigidBody body, double dt)
    {
        if (!_isNitroActive || !_activeProfile.EnableNitro)
            return;

        if (_fuelLevel <= 0 || _heatLevel > CRITICAL_TEMP * 0.8)
        {
            _isNitroActive = false;
            return;
        }

        _fuelLevel -= FUEL_CONSUMPTION_RATE * 5 * dt;
        _heatLevel += _activeProfile.HeatGeneration * 3 * dt;

        if (_boostRemainingTime <= 0)
        {
            _isNitroActive = false;
        }
    }

    public void ActivateNitro(RigidBody body)
    {
        if (_activeProfile.EnableNitro && _fuelLevel > 20 && !_isOverheated)
        {
            _isNitroActive = true;
            ActivateBoost(body, NITRO_DURATION);
        }
    }

    #endregion

    #region Exhaust Trail & Visual Effects

    private void UpdateExhaustTrail(RigidBody body, double dt)
    {
        if (!_isBoosting && !_isAfterburnerActive)
            return;

        int particleCount = _isAfterburnerActive ? 8 : 4;
        double speedFactor = Math.Min(body.Velocity.Length / 100.0, 3.0);
        particleCount = (int)(particleCount * speedFactor);

        for (int i = 0; i < particleCount; i++)
        {
            if (_exhaustParticles.Count == 0) break;

            var particle = _exhaustParticles.Dequeue();
            Vector2 offset = new Vector2(
                (RandomValue(body.Id + i + Environment.TickCount) - 0.5) * body.Radius,
                (RandomValue(body.Id + i + Environment.TickCount + 1000) - 0.5) * body.Radius);

            particle.Position = body.Position - _lastThrustDirection * body.Radius + offset;
            particle.Velocity = -_lastThrustDirection * (body.Velocity.Length * 0.3 + 10) + offset * 0.5;
            particle.Life = 0;
            particle.MaxLife = 0.2 + RandomValue(body.Id + i) * (_isAfterburnerActive ? 0.4 : 0.2);
            particle.Size = _isAfterburnerActive ? 4.0 : 2.5;
            particle.Size += RandomValue(body.Id + i + 200) * 2.0;
            particle.Alpha = _isAfterburnerActive ? (byte)200 : (byte)150;
        }
    }

    private void UpdateShockwaves(RigidBody body, double dt, PhysicsWorld world)
    {
        double speed = body.Velocity.Length;
        if (speed < SONIC_BOOM_THRESHOLD)
            return;

        if (_shockwaves.Count == 0 || _shockwaves.Peek().Life >= _shockwaves.Peek().MaxLife)
        {
            var shockwave = new ShockwaveEffect
            {
                Origin = body.Position,
                Radius = body.Radius,
                MaxRadius = SHOCKWAVE_RANGE,
                Life = 0,
                MaxLife = 0.5,
                Intensity = (speed - SONIC_BOOM_THRESHOLD) / SONIC_BOOM_THRESHOLD
            };
            _shockwaves.Enqueue(shockwave);
            _sonicBoomCount++;
            TriggerSonicBoomEffects(body, world, shockwave.Intensity);
        }

        foreach (var shockwave in _shockwaves)
        {
            shockwave.Life += dt;
            shockwave.Radius = shockwave.MaxRadius * (shockwave.Life / shockwave.MaxLife);
        }

        while (_shockwaves.Count > 0 && _shockwaves.Peek().Life >= _shockwaves.Peek().MaxLife)
        {
            _shockwaves.Dequeue();
        }
    }

    private void TriggerSonicBoomEffects(RigidBody body, PhysicsWorld world, double intensity)
    {
        float distance = 100f;
        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic) continue;

            double dist = Vector2.Distance(body.Position, other.Position);
            if (dist < distance)
            {
                double force = intensity * 5000 / (dist + 1);
                Vector2 direction = (other.Position - body.Position).Normalized;
                other.ApplyImpulse(direction * force);
            }
        }
    }

    private void UpdateSonicBoomEffects(RigidBody body)
    {
        float speed = (float)body.Velocity.Length;
        if (speed > SONIC_BOOM_THRESHOLD * 1.5)
        {
            // Screen shake effect would go here
        }
    }

    private void UpdateDebugVisualization(RigidBody body)
    {
        // No-op: debug visualization handled by RenderDebugOverlay
    }

    #endregion

    #region Collision Handling

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        double impactSpeed = Vector2.Distance(body.Velocity, other.Velocity);

        if (impactSpeed > 500)
        {
            CreateCollisionShockwave(body, impactSpeed);
        }

        _heatLevel += impactSpeed * 0.1;

        if (_isNitroActive && impactSpeed > 100)
        {
            Vector2 bounceImpulse = (body.Position - other.Position).Normalized * impactSpeed * 2;
            if (!body.IsStatic) body.ApplyImpulse(bounceImpulse);
        }

        if (impactSpeed > 200 || _heatLevel > CRITICAL_TEMP)
        {
            TriggerParticleEffect(body, Vector2.Zero, "collision_spark");
        }

        LogDebug(body, $"Collision: impact={impactSpeed:F2}, heat={_heatLevel:F2}, fuel={_fuelLevel:F2}");
    }

    private void CreateCollisionShockwave(RigidBody body, double impactSpeed)
    {
        var shockwave = new ShockwaveEffect
        {
            Origin = body.Position,
            Radius = body.Radius,
            MaxRadius = Math.Min(impactSpeed * 2, 300),
            Life = 0,
            MaxLife = 0.3,
            Intensity = impactSpeed / 1000
        };
        _shockwaves.Enqueue(shockwave);
    }

    private void TriggerParticleEffect(RigidBody body, Vector2 offset, string type)
    {
        // Particle system integration point
    }

    #endregion

    #region Statistics Tracking

    private void TrackStatistics(RigidBody body, double dt)
    {
        double speed = body.Velocity.Length;
        double distanceDelta = Vector2.Distance(body.Position, _lastVelocity);
        _totalDistanceTraveled += distanceDelta;

        if (speed > _peakVelocity)
        {
            _peakVelocity = speed;
        }

        if (speed > SONIC_BOOM_THRESHOLD)
        {
            _totalFuelConsumed += FUEL_CONSUMPTION_RATE * 2 * dt;
            _fuelLevel -= FUEL_CONSUMPTION_RATE * 2 * dt;
        }
        else if (_isBoosting || _isAfterburnerActive)
        {
            _totalFuelConsumed += FUEL_CONSUMPTION_RATE * dt;
            _fuelLevel -= FUEL_CONSUMPTION_RATE * dt;
        }
        else if (body.Velocity.Length > 50)
        {
            _totalFuelConsumed += FUEL_CONSUMPTION_RATE * 0.1 * dt;
        }

        _fuelLevel = Math.Max(0, _fuelLevel);
        _lastVelocity = body.Position;
    }

    #endregion

    #region Public API for Runtime Modification

    public void SetMode(TurboMode mode, RigidBody body = null)
    {
        if (!_turboProfiles.TryGetValue(mode, out var profile))
            return;

        _currentMode = mode;
        _activeProfile = profile;
        _currentBoostMultiplier = 1.0;

        if (body != null && mode != TurboMode.Off)
        {
            body.Restitution = 0.7 + (profile.MaxBoostMultiplier - 1) * 0.1;
        }

        LogDebug(body, $"Turbo mode changed to: {mode}");
    }

    public void SetCustomProfile(TurboProfile profile)
    {
        _activeProfile = profile ?? _turboProfiles[TurboMode.Standard];
        _currentMode = TurboMode.Custom;
        _currentBoostMultiplier = 1.0;
    }

    public void StartBoost() => _isBoosting = true;
    public void StopBoost() => _isBoosting = false;
    public void ToggleBoost() => _isBoosting = !_isBoosting;

    public void SetFuelLevel(double amount)
    {
        _fuelLevel = Math.Clamp(amount, 0, MAX_FUEL_CAPACITY);
    }

    public void AddFuel(double amount)
    {
        _fuelLevel = Math.Min(_fuelLevel + amount, MAX_FUEL_CAPACITY);
    }

    public void ForceCoolDown()
    {
        _heatLevel = 20.0;
        _isOverheated = false;
    }

    public void ClearHeat() => _heatLevel = 20.0;

    public void ResetStats()
    {
        _totalDistanceTraveled = 0;
        _peakVelocity = 0;
        _totalFuelConsumed = 0;
        _sonicBoomCount = 0;
        _maxComboBoosts = 0;
        _fuelLevel = MAX_FUEL_CAPACITY;
        _heatLevel = 20.0;
        _exhaustParticles.Clear();
        _shockwaves.Clear();
        PreWarmParticlePool();
    }

    #endregion

    #region State Queries

    public TurboMode GetCurrentMode() => _currentMode;
    public double GetCurrentBoostMultiplier() => _currentBoostMultiplier;
    public double GetHeatLevel() => _heatLevel;
    public double GetFuelLevel() => _fuelLevel;
    public double GetTotalDistance() => _totalDistanceTraveled;
    public double GetPeakVelocity() => _peakVelocity;
    public double GetTotalFuelConsumed() => _totalFuelConsumed;
    public int GetSonicBoomCount() => _sonicBoomCount;
    public int GetMaxComboBoosts() => _maxComboBoosts;
    public bool IsBoosting() => _isBoosting;
    public bool IsOverheated() => _isOverheated;
    public bool IsAfterburnerActive() => _isAfterburnerActive;
    public bool IsNitroActive() => _isNitroActive;
    public double GetBoostRemainingTime() => _boostRemainingTime;
    public string GetDiagnosticsReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== TurboBehavior Diagnostics ===");
        sb.AppendLine($"Mode: {_currentMode}");
        sb.AppendLine($"Profile: {_activeProfile.Name}");
        sb.AppendLine($"Boost Multiplier: {_currentBoostMultiplier:F2}x");
        sb.AppendLine($"Heat Level: {_heatLevel:F1}/{OVERHEAT_SHUTDOWN_TEMP}");
        sb.AppendLine($"Fuel Level: {_fuelLevel:F1}/{MAX_FUEL_CAPACITY}");
        sb.AppendLine($"Is Boosting: {_isBoosting}");
        sb.AppendLine($"Is Overheated: {_isOverheated}");
        sb.AppendLine($"Total Distance: {_totalDistanceTraveled:F1}");
        sb.AppendLine($"Peak Velocity: {_peakVelocity:F1}");
        sb.AppendLine($"Sonic Booms: {_sonicBoomCount}");
        sb.AppendLine($"Max Combo: {_maxComboBoosts}");
        sb.AppendLine($"Fuel Consumed: {_totalFuelConsumed:F1}");
        return sb.ToString();
    }

    public static IReadOnlyDictionary<TurboMode, TurboProfile> GetAllProfiles()
    {
        return new Dictionary<TurboMode, TurboProfile>(_turboProfiles);
    }

    #endregion

    #region Debug Visualization

    protected override void RenderDebugOverlay(RigidBody body, DrawingContext dc)
    {
        if (dc == null) return;

        DrawBoostIndicator(body, dc);
        DrawHeatBar(body, dc);
        DrawFuelBar(body, dc);
        DrawExhaustTrail(dc);
        DrawShockwaves(dc);
        DrawVelocityVector(body, dc);
    }

    private void DrawBoostIndicator(RigidBody body, System.Windows.Media.DrawingContext dc)
    {
        if (_isBoosting || _isAfterburnerActive || _isNitroActive)
        {
            var color = _isNitroActive ? System.Windows.Media.Brushes.Cyan :
                        _isAfterburnerActive ? System.Windows.Media.Brushes.Red :
                        System.Windows.Media.Brushes.Yellow;
            double size = body.Radius + (_currentBoostMultiplier - 1) * 5;
            dc.DrawEllipse(null, new System.Windows.Media.Pen(color, 2),
                new System.Windows.Point(body.Position.X, body.Position.Y), size, size);
        }
    }

    private void DrawHeatBar(RigidBody body, System.Windows.Media.DrawingContext dc)
    {
        double barWidth = 30;
        double barHeight = 4;
        double x = body.Position.X - barWidth / 2;
        double y = body.Position.Y + body.Radius + 10;

        double heatRatio = _heatLevel / OVERHEAT_SHUTDOWN_TEMP;
        var heatColor = heatRatio > 0.8 ? System.Windows.Media.Brushes.Red :
                       heatRatio > 0.5 ? System.Windows.Media.Brushes.Yellow :
                       System.Windows.Media.Brushes.Green;

        dc.DrawRectangle(System.Windows.Media.Brushes.DarkGray, null,
            new System.Windows.Rect(x, y, barWidth, barHeight));
        dc.DrawRectangle(heatColor, null,
            new System.Windows.Rect(x, y, barWidth * heatRatio, barHeight));
    }

    private void DrawFuelBar(RigidBody body, System.Windows.Media.DrawingContext dc)
    {
        double barWidth = 30;
        double barHeight = 3;
        double x = body.Position.X - barWidth / 2;
        double y = body.Position.Y + body.Radius + 16;

        double fuelRatio = _fuelLevel / MAX_FUEL_CAPACITY;
        var fuelColor = fuelRatio > 0.3 ? System.Windows.Media.Brushes.LimeGreen :
                       System.Windows.Media.Brushes.Red;

        dc.DrawRectangle(System.Windows.Media.Brushes.DarkGray, null,
            new System.Windows.Rect(x, y, barWidth, barHeight));
        dc.DrawRectangle(fuelColor, null,
            new System.Windows.Rect(x, y, barWidth * fuelRatio, barHeight));
    }

    private void DrawExhaustTrail(System.Windows.Media.DrawingContext dc)
    {
        foreach (var particle in _exhaustParticles)
        {
            if (particle.Life >= particle.MaxLife) continue;
            double opacity = 1.0 - (particle.Life / particle.MaxLife);
            var color = System.Windows.Media.Color.FromArgb(
                (byte)(particle.Alpha * opacity),
                255, 200, 50);
            var brush = new System.Windows.Media.SolidColorBrush(color);
            dc.DrawEllipse(brush, null,
                new System.Windows.Point(particle.Position.X, particle.Position.Y),
                particle.Size, particle.Size);
        }
    }

    private void DrawShockwaves(System.Windows.Media.DrawingContext dc)
    {
        foreach (var wave in _shockwaves)
        {
            double opacity = 1.0 - (wave.Life / wave.MaxLife);
            var color = System.Windows.Media.Color.FromArgb(
                (byte)(150 * opacity * wave.Intensity),
                255, 255, 255);
            var pen = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(color), 2);
            dc.DrawEllipse(null, pen,
                new System.Windows.Point(wave.Origin.X, wave.Origin.Y),
                wave.Radius, wave.Radius);
        }
    }

    private void DrawVelocityVector(RigidBody body, System.Windows.Media.DrawingContext dc)
    {
        double speed = body.Velocity.Length;
        if (speed > 50)
        {
            var endPoint = body.Position + body.Velocity.Normalized * Math.Min(speed / 10, 100);
            var color = speed > SONIC_BOOM_THRESHOLD ? System.Windows.Media.Brushes.Red :
                       speed > 200 ? System.Windows.Media.Brushes.Orange :
                       System.Windows.Media.Brushes.Yellow;
            var pen = new System.Windows.Media.Pen(color, 2);
            dc.DrawLine(pen,
                new System.Windows.Point(body.Position.X, body.Position.Y),
                new System.Windows.Point(endPoint.X, endPoint.Y));
        }
    }

    #endregion

    #region Utility Methods

    private static double RandomValue(int seed)
    {
        var rng = new Random(seed);
        return rng.NextDouble();
    }

    public void AddThrust(double amount)
    {
        _activeProfile.BaseThrust += amount;
    }

    public void SetThrustMultiplier(double multiplier)
    {
        _activeProfile.MaxBoostMultiplier = Math.Max(1.0, multiplier);
    }

    public double CalculateEfficiency()
    {
        if (_totalFuelConsumed <= 0) return 0;
        return _totalDistanceTraveled / _totalFuelConsumed;
    }

    public string GetStatusReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Mode: {_currentMode}");
        sb.AppendLine($"Boost: {_currentBoostMultiplier:F2}x");
        sb.AppendLine($"Heat: {_heatLevel:F0}/{OVERHEAT_SHUTDOWN_TEMP}");
        sb.AppendLine($"Fuel: {_fuelLevel:F0}/{MAX_FUEL_CAPACITY}");
        sb.AppendLine($"Distance: {_totalDistanceTraveled:F0}");
        sb.AppendLine($"Efficiency: {CalculateEfficiency():F2}");
        return sb.ToString();
    }

    #endregion

    #region Serialization

    public string SerializeState()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Mode:{_currentMode}");
        sb.AppendLine($"BoostMultiplier:{_currentBoostMultiplier}");
        sb.AppendLine($"BoostRemaining:{_boostRemainingTime}");
        sb.AppendLine($"HeatLevel:{_heatLevel}");
        sb.AppendLine($"FuelLevel:{_fuelLevel}");
        sb.AppendLine($"IsBoosting:{_isBoosting}");
        sb.AppendLine($"IsAfterburner:{_isAfterburnerActive}");
        sb.AppendLine($"IsNitro:{_isNitroActive}");
        sb.AppendLine($"Overheated:{_isOverheated}");
        sb.AppendLine($"TotalDistance:{_totalDistanceTraveled}");
        sb.AppendLine($"PeakVelocity:{_peakVelocity}");
        sb.AppendLine($"SonicBooms:{_sonicBoomCount}");
        sb.AppendLine($"MaxCombo:{_maxComboBoosts}");
        sb.AppendLine($"FuelConsumed:{_totalFuelConsumed}");
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
                    case "Mode":
                        if (Enum.TryParse<TurboMode>(parts[1], out var mode))
                            _currentMode = mode;
                        break;
                    case "BoostMultiplier":
                        _currentBoostMultiplier = double.Parse(parts[1]);
                        break;
                    case "HeatLevel":
                        _heatLevel = double.Parse(parts[1]);
                        break;
                    case "FuelLevel":
                        _fuelLevel = double.Parse(parts[1]);
                        break;
                    case "IsBoosting":
                        _isBoosting = bool.Parse(parts[1]);
                        break;
                    case "IsAfterburner":
                        _isAfterburnerActive = bool.Parse(parts[1]);
                        break;
                    case "IsNitro":
                        _isNitroActive = bool.Parse(parts[1]);
                        break;
                    case "Overheated":
                        _isOverheated = bool.Parse(parts[1]);
                        break;
                    case "TotalDistance":
                        _totalDistanceTraveled = double.Parse(parts[1]);
                        break;
                    case "PeakVelocity":
                        _peakVelocity = double.Parse(parts[1]);
                        break;
                    case "SonicBooms":
                        _sonicBoomCount = int.Parse(parts[1]);
                        break;
                    case "MaxCombo":
                        _maxComboBoosts = int.Parse(parts[1]);
                        break;
                    case "FuelConsumed":
                        _totalFuelConsumed = double.Parse(parts[1]);
                        break;
                }
            }
            catch { }
        }
    }

    #endregion

    #region Advanced Turbo Features

    private readonly Dictionary<string, double> _performanceMetrics = new();
    private bool _enableTurboDrift = false;
    private double _driftBoostMultiplier = 1.2;
    private double _corneringBoost = 1.0;
    private Vector2 _lastFrameVelocity = Vector2.Zero;

    public void EnableTurboDrift(bool enable)
    {
        _enableTurboDrift = enable;
    }

    private void UpdateAdvancedFeatures(RigidBody body, double dt)
    {
        if (!_enableTurboDrift) return;

        if (_lastFrameVelocity.Length > 0.1 && body.Velocity.Length > 100)
        {
            Vector2 velChange = body.Velocity - _lastFrameVelocity;
            double angleChange = Math.Atan2(velChange.Y, velChange.X);
            if (Math.Abs(angleChange) > 0.1)
            {
                _corneringBoost = 1.0 + Math.Abs(angleChange) * 0.5;
                if (_isBoosting)
                {
                    Vector2 driftForce = body.Velocity.Normalized * _driftBoostMultiplier * dt;
                    body.ApplyForce(driftForce * body.Mass);
                }
            }
        }
        _lastFrameVelocity = body.Velocity;
    }

    public void AddPerformanceMetric(string key, double value)
    {
        _performanceMetrics[key] = value;
    }

    public double GetPerformanceMetric(string key)
    {
        return _performanceMetrics.TryGetValue(key, out double val) ? val : 0;
    }

    #endregion

    #region Turbo Combo System

    private bool _enableComboSystem = true;
    private double _comboTimer = 0.0;
    private const double COMBO_WINDOW = 2.0;

    private void UpdateTurboCombo(double dt)
    {
        if (!_enableComboSystem) return;

        if (_isBoosting || _isAfterburnerActive || _isNitroActive)
        {
            _comboTimer = Math.Max(0, _comboTimer - dt);
        }
        else
        {
            _comboTimer += dt;
            if (_comboTimer > COMBO_WINDOW)
            {
                _comboTimer = 0;
                _currentComboBoostTime = 0;
            }
        }
    }

    public int GetTurboComboScore()
    {
        return (int)(_maxComboBoosts * 100 + _sonicBoomCount * 50);
    }

    #endregion

    #region Environmental Interactions

    private void ApplyEnvironmentalEffects(RigidBody body, double dt, PhysicsWorld world)
    {
        double altitudeFactor = 1.0 - (body.Position.Y * 0.0001);
        altitudeFactor = Math.Clamp(altitudeFactor, 0.8, 1.2);

        _activeProfile.HeatGeneration *= altitudeFactor;

        if (body.Position.Y > 1000)
        {
            _heatLevel -= dt * 5;
        }

        if (!_isInVacuum && body.Velocity.Length > 500)
        {
            double airResistance = body.Velocity.LengthSquared * 0.00001;
            body.ApplyForce(-body.Velocity.Normalized * airResistance * body.Mass);
        }
    }

    #endregion

    #region Extended Statistics

    public string GetExtendedDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine(GetDiagnosticsReport());
        sb.AppendLine($"Efficiency: {CalculateEfficiency():F2}");
        sb.AppendLine($"Combo Score: {GetTurboComboScore()}");
        sb.AppendLine($"Avg Boost Time: {GetAverageBoostTime():F2}s");
        sb.AppendLine($"Total Boosts: {_maxComboBoosts}");
        sb.AppendLine($"Turbo Drift: {_enableTurboDrift}");
        sb.AppendLine($"Cornering Boost: {_corneringBoost:F2}");
        return sb.ToString();
    }

    private double GetAverageBoostTime()
    {
        return _maxComboBoosts > 0 ? _totalDistanceTraveled / (_maxComboBoosts * 100) : 0;
    }

    public Dictionary<string, object> GetStatisticsDictionary()
    {
        return new Dictionary<string, object>
        {
            ["mode"] = _currentMode.ToString(),
            ["boostMultiplier"] = _currentBoostMultiplier,
            ["heatLevel"] = _heatLevel,
            ["fuelLevel"] = _fuelLevel,
            ["isBoosting"] = _isBoosting,
            ["isOverheated"] = _isOverheated,
            ["totalDistance"] = _totalDistanceTraveled,
            ["peakVelocity"] = _peakVelocity,
            ["sonicBooms"] = _sonicBoomCount,
            ["maxCombo"] = _maxComboBoosts,
            ["fuelConsumed"] = _totalFuelConsumed,
            ["efficiency"] = CalculateEfficiency(),
            ["comboScore"] = GetTurboComboScore()
        };
    }

    #endregion

    #region Event Handlers

    protected override void RaiseInitialized(RigidBody body)
    {
        base.RaiseInitialized(body);
        LogDebug(body, $"TurboBehavior fully initialized for body {body.Id}");
    }

    protected override void RaiseCollision(RigidBody body, RigidBody other)
    {
        base.RaiseCollision(body, other);
        if (other.Behavior?.Type == BodyType.Turbo)
        {
            _sonicBoomCount++;
        }
    }

    protected override void RaisePostUpdate(RigidBody body, double dt)
    {
        base.RaisePostUpdate(body, dt);
        _lastPosition = body.Position;
    }

    #endregion

    #region Final Cleanup

    ~TurboBehavior()
    {
        _exhaustParticles?.Clear();
        _shockwaves?.Clear();
    }

    #endregion

}
