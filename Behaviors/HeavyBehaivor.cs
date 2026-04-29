using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Windows.Media;

namespace PhysicsSandbox.Behaviors;

public class HeavyBehavior : BodyBehavior
{
    #region Constants & Tunable Parameters

    private const double DEFAULT_DENSITY = 15.0;
    private const double MINIMUM_DENSITY = 1.0;
    private const double MAXIMUM_DENSITY = 100.0;
    private const double CRUSH_THRESHOLD = 0.8;
    private const double STRUCTURAL_INTEGRITY_BASE = 1000.0;
    private const double MOMENTUM_TRANSFER_EFFICIENCY = 0.95;
    private const double AVALANCHE_FORCE_MULTIPLIER = 2.5;
    private const double MIN_AVALANCHE_MASS = 50.0;
    private const double GROUND_IMPACT_DAMPING = 0.90;
    private const double IMPACT_CRATER_DEPTH_FACTOR = 0.3;
    private const double PRESSURE_PROPAGATION_RADIUS = 200.0;
    private const int MAX_STRUCTURAL_STRESS_SAMPLES = 100;
    private const double STRESS_REDUCTION_RATE = 0.95;
    private const double VIBRATION_DAMPING = 0.90;
    private const double SEISMIC_WAVE_SPEED = 5.0;
    private const double HEAVY_FALLBACK_DAMAGE_THRESHOLD = 50.0;

    #endregion

    #region Material Presets

    public enum HeavyMaterial
    {
        Iron,
        Lead,
        Gold,
        Platinum,
        Uranium,
        Tungsten,
        Osmium,
        Neutronium,
        Diamond,
        Concrete,
        Custom
    }

    private static readonly Dictionary<HeavyMaterial, HeavyProfile> _heavyProfiles = new()
    {
        {
            HeavyMaterial.Iron, new HeavyProfile
            {
                Name = "Iron",
                Density = 7.87,
                StructuralIntegrity = 1.0,
                ColorHex = "#B0B0B0",
                ImpactSound = "HeavyMetal",
                CrushResistance = 0.75,
                MomentumTransfer = 0.92
            }
        },
        {
            HeavyMaterial.Lead, new HeavyProfile
            {
                Name = "Lead",
                Density = 11.34,
                StructuralIntegrity = 0.8,
                ColorHex = "#4A4A4A",
                ImpactSound = "DenseThud",
                CrushResistance = 0.65,
                MomentumTransfer = 0.88
            }
        },
        {
            HeavyMaterial.Gold, new HeavyProfile
            {
                Name = "Gold",
                Density = 19.32,
                StructuralIntegrity = 0.5,
                ColorHex = "#FFD700",
                ImpactSound = "SoftThud",
                CrushResistance = 0.30,
                MomentumTransfer = 0.95
            }
        },
        {
            HeavyMaterial.Platinum, new HeavyProfile
            {
                Name = "Platinum",
                Density = 21.45,
                StructuralIntegrity = 1.2,
                ColorHex = "#E5E4E2",
                ImpactSound = "HeavyChime",
                CrushResistance = 0.85,
                MomentumTransfer = 0.94
            }
        },
        {
            HeavyMaterial.Uranium, new HeavyProfile
            {
                Name = "Uranium",
                Density = 19.1,
                StructuralIntegrity = 0.9,
                ColorHex = "#2E8B57",
                ImpactSound = "GlowImpact",
                CrushResistance = 0.70,
                MomentumTransfer = 0.91
            }
        },
        {
            HeavyMaterial.Tungsten, new HeavyProfile
            {
                Name = "Tungsten",
                Density = 19.25,
                StructuralIntegrity = 1.5,
                ColorHex = "#4F4F4F",
                ImpactSound = "SolidClang",
                CrushResistance = 0.90,
                MomentumTransfer = 0.96
            }
        },
        {
            HeavyMaterial.Osmium, new HeavyProfile
            {
                Name = "Osmium",
                Density = 22.59,
                StructuralIntegrity = 0.75,
                ColorHex = "#1E3A8A",
                ImpactSound = "RareElement",
                CrushResistance = 0.50,
                MomentumTransfer = 0.87
            }
        },
        {
            HeavyMaterial.Neutronium, new HeavyProfile
            {
                Name = "Neutronium",
                Density = 100.0,
                StructuralIntegrity = 5.0,
                ColorHex = "#2A2A2A",
                ImpactSound = "VoidCrush",
                CrushResistance = 1.0,
                MomentumTransfer = 1.0
            }
        },
        {
            HeavyMaterial.Diamond, new HeavyProfile
            {
                Name = "Diamond",
                Density = 3.51,
                StructuralIntegrity = 0.95,
                ColorHex = "#B9F2FF",
                ImpactSound = "CrystalChime",
                CrushResistance = 0.60,
                MomentumTransfer = 0.80
            }
        },
        {
            HeavyMaterial.Concrete, new HeavyProfile
            {
                Name = "Concrete",
                Density = 2.4,
                StructuralIntegrity = 0.6,
                ColorHex = "#808080",
                ImpactSound = "RumblingImpact",
                CrushResistance = 0.40,
                MomentumTransfer = 0.85
            }
        }
    };

    #endregion

    #region Structural Integrity System

    private class StructuralState
    {
        public double CurrentIntegrity { get; set; }
        public double MaxIntegrity { get; set; }
        public bool IsIntact { get; set; } = true;
        public List<CrackPoint> Cracks { get; set; } = new();
        public double StressAccumulation { get; set; }
        public Vector2 LastImpactDirection { get; set; }
    }

    public class CrackPoint
    {
        public Vector2 Position { get; set; }
        public double Length { get; set; }
        public double Depth { get; set; }
        public double PropagationSpeed { get; set; }
        public bool IsActive { get; set; }
    }

    #endregion

    #region Momentum & Impact Tracking

     public class ImpactRecord
    {
        public double ImpactForce { get; set; }
        public double KineticEnergy { get; set; }
        public Vector2 ContactPoint { get; set; }
        public Vector2 ImpactNormal { get; set; }
        public double Timestamp { get; set; }
        public string OtherMaterial { get; set; } = "";
    }

    #endregion

    #region Seismic & Ground Interaction

    public class SeismicWave
    {
        public Vector2 Origin { get; set; }
        public double Radius { get; set; }
        public double Power { get; set; }
        public double Speed { get; set; }
        public double DecayRate { get; set; }
        public int AffectedBodies { get; set; }
    }

    #endregion

    #region Instance State Fields

    private HeavyMaterial _currentMaterial = HeavyMaterial.Iron;
    private HeavyProfile _activeProfile = _heavyProfiles[HeavyMaterial.Iron];
    private double _density = DEFAULT_DENSITY;
    private double _customDensity = DEFAULT_DENSITY;
    private bool _isIndestructible = false;
    private bool _crushMode = false;
    private bool _avalancheMode = false;
    private bool _groundAnchor = false;
    private StructuralState _structuralState = new();
    private int _impactCount = 0;
    private int _crushCount = 0;
    private double _totalImpactEnergy = 0.0;
    private double _totalMomentumTransferred = 0.0;
    private double _peakImpactForce = 0.0;
    private Vector2 _lastImpactPoint = Vector2.Zero;
    private readonly List<ImpactRecord> _impactHistory = new();
    private readonly List<SeismicWave> _seismicWaves = new();
    private readonly Queue<Vector2> _recentPositions = new();
    private readonly Stopwatch _impactTimer = new();
    private double _timeSinceLastImpact = double.MaxValue;
    private bool _enableStructuralFailure = true;
    private bool _enableMomentumConservation = true;
    private bool _enableSeismicEffects = true;
    private bool _enableCrushPhysics = true;
    private bool _enableAvalancheMode = true;
    private double _massMultiplierOverride = 1.0;
    private double _restitutionOverride = -1.0;
    private readonly StringBuilder _physicsDebugLog = new();
    private bool _groundInteractionEnabled = true;
    private double _groundImpactDepth = 0.0;
    private double _vibrationAmplitude = 0.0;
    private double _crushForceAccumulator = 0.0;
    private int _consecutiveHeavyImpacts = 0;
    private bool _isPiercing = false;
    private double _piercingForce = 0.0;

    #endregion

    #region Heavy Profile Data Structure

    public class HeavyProfile
    {
        public string Name { get; set; } = "";
        public double Density { get; set; }
        public double StructuralIntegrity { get; set; }
        public string ColorHex { get; set; } = "#FFFFFF";
        public string ImpactSound { get; set; } = "";
        public double CrushResistance { get; set; }
        public double MomentumTransfer { get; set; }
    }

    #endregion

    #region Behavior Properties (Overrides)

    public override BodyType Type => BodyType.Heavy;
    public override string Name => $"Heavy ({_activeProfile.Name})";
    public override string Description => GetHeavyDescription();
    public override string ColorHex => _activeProfile.ColorHex;
    public override double DefaultRadius => 20;
    public override double DefaultMass => CalculateMassFromDensity();
    public override double DefaultRestitution => _restitutionOverride >= 0 ? _restitutionOverride : 0.15;

    #endregion

    #region Constructors

    public HeavyBehavior() : this(HeavyMaterial.Iron) { }

    public HeavyBehavior(HeavyMaterial material)
    {
        _currentMaterial = material;
        _activeProfile = _heavyProfiles[material];
        _density = _activeProfile.Density;
        _structuralState.MaxIntegrity = STRUCTURAL_INTEGRITY_BASE * _activeProfile.StructuralIntegrity;
        _structuralState.CurrentIntegrity = _structuralState.MaxIntegrity;
        _structuralState.IsIntact = true;
    }

    public HeavyBehavior(double customDensity) : this(HeavyMaterial.Custom)
    {
        _customDensity = Math.Clamp(customDensity, MINIMUM_DENSITY, MAXIMUM_DENSITY);
        _density = _customDensity;
    }

    #endregion

    #region Initialization & Creation

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);

        double mass = CalculateMassFromDensity();
        body.Mass = mass;
        body.Restitution = DefaultRestitution;
        body.Radius = DefaultRadius;

        InitializeStructuralState(body);
        InitializeImpactTracking(body);
        InitializeGroundInteraction(body);

        LogDebug(body, $"HeavyBehavior created: Material={_activeProfile.Name}, Density={_density:F2}, Mass={mass:F2}, StructuralIntegrity={_structuralState.MaxIntegrity:F2}");
    }

    private void InitializeStructuralState(RigidBody body)
    {
        _structuralState = new StructuralState
        {
            CurrentIntegrity = _structuralState.MaxIntegrity,
            MaxIntegrity = STRUCTURAL_INTEGRITY_BASE * _activeProfile.StructuralIntegrity,
            IsIntact = true,
            StressAccumulation = 0.0,
            LastImpactDirection = Vector2.Zero
        };
        _structuralState.Cracks.Clear();
    }

    private void InitializeImpactTracking(RigidBody body)
    {
        _impactHistory.Clear();
        _totalImpactEnergy = 0.0;
        _totalMomentumTransferred = 0.0;
        _impactCount = 0;
        _peakImpactForce = 0.0;
        _lastImpactPoint = Vector2.Zero;
        _timeSinceLastImpact = double.MaxValue;
        _consecutiveHeavyImpacts = 0;
        _recentPositions.Clear();
        _recentPositions.Enqueue(body.Position);
    }

    private void InitializeGroundInteraction(RigidBody body)
    {
        _groundImpactDepth = 0.0;
        _vibrationAmplitude = 0.0;
    }

    #endregion

    #region Main Update Loop

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        _impactTimer.Restart();

        try
        {
            if (body.IsStatic || body.IsFrozen)
                return;

            RaisePreUpdate(body, dt);

            UpdateStructuralIntegrity(body, dt);
            UpdateMomentumTracking(body, dt);
            UpdateHeavyPhysics(body, dt, world);
            ProcessCrushPhysics(body, dt);
            ProcessAvalancheMode(body, dt, world);
            UpdateGroundInteraction(body, dt, world);
            UpdateSeismicEffects(body, dt, world);
            UpdateImpactCooldown(dt);
            TrackHeavyMovement(body, dt);
            ApplyMassModifiers(body);
            ProcessStructuralFailure(body, world);
            LogPhysicsDebug(body, dt);
            UpdatePerformanceMetrics(body, dt);

            RaisePostUpdate(body, dt);
        }
        finally
        {
            _impactTimer.Stop();
            RecordPerformanceMetric("OnUpdate", _impactTimer.Elapsed.TotalMilliseconds);
        }
    }

    #endregion

    #region Structural Integrity System

    private void UpdateStructuralIntegrity(RigidBody body, double dt)
    {
        if (!_enableStructuralFailure || _isIndestructible)
            return;

        _structuralState.StressAccumulation *= Math.Pow(STRESS_REDUCTION_RATE, dt * 60);

        if (_structuralState.StressAccumulation > _structuralState.MaxIntegrity * 0.9)
        {
            SpawnCrack(body);
        }

        if (_structuralState.StressAccumulation >= _structuralState.MaxIntegrity)
        {
            TriggerStructuralFailure(body);
        }

        foreach (var crack in _structuralState.Cracks.ToArray())
        {
            if (crack.IsActive)
            {
                crack.Length += crack.PropagationSpeed * dt;
                crack.Depth += crack.PropagationSpeed * dt * 0.5;
                _structuralState.StressAccumulation += crack.PropagationSpeed * 0.1;

                if (crack.Length > body.Radius * 0.5 || crack.Depth > body.Radius * 0.3)
                {
                    _structuralState.IsIntact = false;
                    PropagateCrack(crack, body);
                }
            }
        }

        _structuralState.Cracks.RemoveAll(c => c.Length > body.Radius * 2 || c.Depth > body.Radius);
    }

    private void SpawnCrack(RigidBody body)
    {
        Random rng = new Random(body.Id + _impactCount);
        var crack = new CrackPoint
        {
            Position = new Vector2(
                body.Position.X + (float)((rng.NextDouble() - 0.5) * body.Radius * 0.8),
                body.Position.Y + (float)((rng.NextDouble() - 0.5) * body.Radius * 0.8)
            ),
            Length = 1.0,
            Depth = 0.5,
            PropagationSpeed = _activeProfile.Density * 0.1f * ((float)rng.NextDouble() + 0.5f),
            IsActive = true
        };
        _structuralState.Cracks.Add(crack);
        LogDebug(body, $"Crack spawned at {crack.Position}, length={crack.Length:F2}");
    }

    private void PropagateCrack(CrackPoint crack, RigidBody body)
    {
        Random rng = new Random(body.Id + _structuralState.Cracks.Count);
        if (rng.NextDouble() < 0.3)
        {
            var branch = new CrackPoint
            {
                Position = crack.Position,
                Length = 0.5,
                Depth = 0.2,
                PropagationSpeed = crack.PropagationSpeed * 0.7f,
                IsActive = true
            };
            _structuralState.Cracks.Add(branch);
        }
    }

    private void TriggerStructuralFailure(RigidBody body)
    {
        _structuralState.IsIntact = false;
        _crushMode = true;
        LogDebug(body, "Structural integrity compromised - entering Crush Mode!");
    }

    #endregion

    #region Heavy Physics Calculations

    private void UpdateHeavyPhysics(RigidBody body, double dt, PhysicsWorld world)
    {
        ApplyEnhancedGravity(body, world);
        ApplyMomentumConservation(body, dt);
        ApplyInertiaEffects(body, dt);
        ProcessPiercingPhysics(body, world);
    }

    private void ApplyEnhancedGravity(RigidBody body, PhysicsWorld world)
    {
        double gravityScale = 2.0 + (_density / MAXIMUM_DENSITY) * 3.0;
        body.ApplyForce(world.Gravity * (float)gravityScale * body.Mass);
    }

    private void ApplyMomentumConservation(RigidBody body, double dt)
    {
        double momentumMagnitude = body.Mass * body.Velocity.Length;
        if (momentumMagnitude > body.Mass * 10.0)
        {
            double stabilityFactor = 1.0 - Math.Min(0.3, momentumMagnitude / (body.Mass * 100.0));
            body.Velocity *= (float)stabilityFactor;
        }
    }

    private void ApplyInertiaEffects(RigidBody body, double dt)
    {
        double inertia = body.Mass * body.Radius * body.Radius * 0.4;
        double angularDamping = 1.0 / (1.0 + inertia * 0.0001);
        body.AngularVelocity *= (float)angularDamping;
    }

    private void ProcessPiercingPhysics(RigidBody body, PhysicsWorld world)
    {
        if (!_isPiercing)
            return;

        double piercingThreshold = _piercingForce;
        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic)
                continue;

            double distSq = (body.Position - other.Position).LengthSquared;
            double radiusSum = body.Radius + other.Radius;
            
            if (distSq < radiusSum * radiusSum * 0.5)
            {
                double relativeMass = body.Mass / other.Mass;
                if (relativeMass > 2.0 && body.Velocity.Length > piercingThreshold)
                {
                    Vector2 penetrationForce = body.Velocity.Normalized * body.Mass * 50.0;
                    other.ApplyImpulse(penetrationForce);
                    _crushForceAccumulator += penetrationForce.Length;
                }
            }
        }
    }

    #endregion

    #region Crush Physics System

    private void ProcessCrushPhysics(RigidBody body, double dt)
    {
        if (!_enableCrushPhysics || !_crushMode)
            return;

        _crushForceAccumulator *= Math.Pow(0.95, dt * 60);
        
        if (_crushForceAccumulator > 1000.0)
        {
            body.Mass *= 1.05f;
            _density *= 1.02;
            _crushForceAccumulator = 0.0;
        }

        body.Restitution *= 0.995f;
    }

    #endregion

    #region Avalanche Mode

    private void ProcessAvalancheMode(RigidBody body, double dt, PhysicsWorld world)
    {
        if (!_avalancheMode || !_enableAvalancheMode)
            return;

        if (body.Mass < MIN_AVALANCHE_MASS)
            return;

        Vector2 slopeDirection = CalculateSlopeDirection(world);
        double avalancheForce = AVALANCHE_FORCE_MULTIPLIER * body.Mass * slopeDirection.Length;
        
        body.ApplyForce(slopeDirection * (float)avalancheForce);

        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic)
                continue;

            double dist = Vector2.Distance(body.Position, other.Position);
            double influenceRadius = body.Radius * 3.0;
            
            if (dist < influenceRadius)
            {
                double pushForce = (1.0 - dist / influenceRadius) * avalancheForce * 0.3;
                Vector2 pushDir = (other.Position - body.Position).Normalized;
                
                if (other.BodyType == BodyType.Heavy)
                {
                    pushForce *= 0.8;
                }

                other.ApplyImpulse(pushDir * pushForce);
            }
        }
    }

    private Vector2 CalculateSlopeDirection(PhysicsWorld world)
    {
        return new Vector2(0, 1);
    }

    #endregion

    #region Ground Interaction & Seismic System

    private void UpdateGroundInteraction(RigidBody body, double dt, PhysicsWorld world)
    {
        if (!_groundInteractionEnabled)
            return;

        double groundY = world.GroundY;
        double bottomY = body.Position.Y + body.Radius;
        double velocityImpact = Math.Abs(body.Velocity.Y) * body.Mass;

        if (bottomY >= groundY - 2.0)
        {
            if (velocityImpact > HEAVY_FALLBACK_DAMAGE_THRESHOLD)
            {
                _groundImpactDepth = Math.Min(50.0, _groundImpactDepth + velocityImpact * IMPACT_CRATER_DEPTH_FACTOR * 0.01);
                _vibrationAmplitude = Math.Min(10.0, _vibrationAmplitude + velocityImpact * 0.05);
                TriggerSeismicWave(body, velocityImpact * 0.1, world);
                
                if (velocityImpact > 500.0)
                {
                    CreateImpactShockwave(body, world, velocityImpact);
                }
            }

            body.Velocity *= (float)GROUND_IMPACT_DAMPING;
        }

        _vibrationAmplitude *= VIBRATION_DAMPING;
    }

    private void UpdateSeismicEffects(RigidBody body, double dt, PhysicsWorld world)
    {
        if (!_enableSeismicEffects || _seismicWaves.Count == 0)
            return;

        for (int i = _seismicWaves.Count - 1; i >= 0; i--)
        {
            var wave = _seismicWaves[i];
            wave.Radius += wave.Speed * dt;
            wave.Power *= wave.DecayRate;

            foreach (var other in world.Bodies)
            {
                if (other == body || other.IsStatic)
                    continue;

                double dist = Vector2.Distance(wave.Origin, other.Position);
                if (dist < wave.Radius && dist > wave.Radius - wave.Speed * dt)
                {
                    double waveForce = wave.Power / (dist + 1.0);
                    Vector2 forceDir = (other.Position - wave.Origin).Normalized;
                    other.ApplyImpulse(forceDir * waveForce * other.Mass);
                    wave.AffectedBodies++;
                }
            }

            if (wave.Power < 1.0)
            {
                _seismicWaves.RemoveAt(i);
            }
        }
    }

    private void TriggerSeismicWave(RigidBody body, double power, PhysicsWorld world)
    {
        if (!_enableSeismicEffects)
            return;

        var wave = new SeismicWave
        {
            Origin = body.Position,
            Radius = 0,
            Power = power * body.Mass * 10.0,
            Speed = SEISMIC_WAVE_SPEED,
            DecayRate = 0.95,
            AffectedBodies = 0
        };
        _seismicWaves.Add(wave);
        LogDebug(body, $"Seismic wave triggered at {body.Position} with power {power:F2}");
    }

    private void CreateImpactShockwave(RigidBody body, PhysicsWorld world, double intensity)
    {
        double shockwaveRadius = PRESSURE_PROPAGATION_RADIUS * (intensity / 1000.0);
        foreach (var other in world.Bodies)
        {
            if (other == body) continue;

            double dist = Vector2.Distance(body.Position, other.Position);
            if (dist < shockwaveRadius && dist > 0.1)
            {
                double pressureForce = intensity / (dist * dist + 1.0);
                Vector2 pressureDir = (other.Position - body.Position).Normalized;
                other.ApplyForce(pressureDir * pressureForce);
            }
        }

        LogDebug(body, $"Shockwave created: radius={shockwaveRadius:F2}, intensity={intensity:F2}");
    }

    #endregion

    #region Impact Tracking & Collision Handling

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        _impactCount++;
        _timeSinceLastImpact = 0.0;
        _consecutiveHeavyImpacts++;

        Vector2 collisionNormal = (other.Position - body.Position).Normalized;
        Vector2 relativeVel = body.Velocity - other.Velocity;
        double impactSpeed = relativeVel.Length;
        double impactForce = impactSpeed * body.Mass;
        double kineticEnergy = 0.5 * body.Mass * body.Velocity.LengthSquared;

        _lastImpactPoint = body.Position;
        _peakImpactForce = Math.Max(_peakImpactForce, impactForce);
        _totalImpactEnergy += kineticEnergy;

        _structuralState.StressAccumulation += impactForce * 0.01 / _activeProfile.CrushResistance;
        _structuralState.LastImpactDirection = collisionNormal;

        RecordImpact(body, other, impactForce, kineticEnergy, collisionNormal);
        ApplyMomentumTransfer(body, other, collisionNormal, impactForce);
        ProcessHeavyImpactEffects(body, other, impactForce, world);
        UpdateStructuralDamage(body, impactForce, other);
        TriggerAchievementChecks(body, impactForce);

        _recentPositions.Enqueue(body.Position);
        while (_recentPositions.Count > 20)
            _recentPositions.Dequeue();

        base.OnCollision(body, other, world);
    }

    private void RecordImpact(RigidBody body, RigidBody other, double force, double energy, Vector2 normal)
    {
        var record = new ImpactRecord
        {
            ImpactForce = force,
            KineticEnergy = energy,
            ContactPoint = body.Position,
            ImpactNormal = normal,
            Timestamp = 0.0,
            OtherMaterial = other.Behavior?.GetType().Name ?? "Unknown"
        };
        _impactHistory.Add(record);

        while (_impactHistory.Count > MAX_STRUCTURAL_STRESS_SAMPLES)
            _impactHistory.RemoveAt(0);
    }

    private void ApplyMomentumTransfer(RigidBody body, RigidBody other, Vector2 normal, double impactForce)
    {
        if (!_enableMomentumConservation)
            return;

        double momentumTransferEfficiency = _activeProfile.MomentumTransfer * MOMENTUM_TRANSFER_EFFICIENCY;
        double transferredMomentum = impactForce * momentumTransferEfficiency;

        if (!other.IsStatic)
        {
            Vector2 momentumTransfer = normal * transferredMomentum;
            other.ApplyImpulse(momentumTransfer);
            _totalMomentumTransferred += momentumTransfer.Length;
        }

        if (_consecutiveHeavyImpacts > 3)
        {
            double chainFactor = (_consecutiveHeavyImpacts - 3) * 0.5;
            body.ApplyImpulse(-normal * transferredMomentum * chainFactor * 0.3);
        }
    }

    private void ProcessHeavyImpactEffects(RigidBody body, RigidBody other, double impactForce, PhysicsWorld world)
    {
        if (impactForce > 2000.0)
        {
            TriggerSeismicWave(body, impactForce * 0.05, world);
            if (_avalancheMode)
            {
                TriggerAvalancheChainReaction(body, world, impactForce * 0.2);
            }
        }
    }

    private void UpdateStructuralDamage(RigidBody body, double impactForce, RigidBody other)
    {
        if (impactForce > _activeProfile.CrushResistance * 5000.0)
        {
            _structuralState.CurrentIntegrity -= impactForce * 0.05;
            if (_structuralState.CurrentIntegrity < 0)
            {
                _structuralState.CurrentIntegrity = 0;
                TriggerStructuralFailure(body);
            }
        }
    }

    #endregion

    #region Avalanche Chain Reaction

    private void TriggerAvalancheChainReaction(RigidBody body, PhysicsWorld world, double triggerForce)
    {
        if (!body.IsStatic && body.Velocity.Length > 2.0)
        {
            foreach (var other in world.Bodies)
            {
                if (other == body || other.IsStatic)
                    continue;

                double dist = Vector2.Distance(body.Position, other.Position);
                double triggerRadius = body.Radius * 4.0;

                if (dist < triggerRadius)
                {
                    double pushForce = triggerForce * (1.0 - dist / triggerRadius) * 0.5;
                    Vector2 pushDir = (other.Position - body.Position).Normalized;
                    
                    if (other.BodyType == BodyType.Heavy)
                    {
                        pushForce *= 0.8;
                    }

                    other.ApplyImpulse(pushDir * pushForce);
                }
            }
        }
    }

    #endregion

    #region Achievement System

    private void TriggerAchievementChecks(RigidBody body, double impactForce)
    {
        CheckImpactForceAchievement(impactForce);
        CheckCrushCountAchievement();
        CheckMomentumAchievement(body);
        CheckConsecutiveImpactAchievement();
    }

    private void CheckImpactForceAchievement(double impactForce)
    {
        if (impactForce > 1000.0)
            LogDebug(null, "Achievement: Thunder Strike - Impact force exceeded 1000!");
        if (impactForce > 5000.0)
            LogDebug(null, "Achievement: Seismic Event - Impact force exceeded 5000!");
    }

    private void CheckCrushCountAchievement()
    {
        if (_crushCount >= 10)
            LogDebug(null, "Achievement: Crusher - Crushed 10 objects!");
        if (_crushCount >= 50)
            LogDebug(null, "Achievement: Demolisher - Crushed 50 objects!");
    }

    private void CheckMomentumAchievement(RigidBody body)
    {
        double momentum = body.Mass * body.Velocity.Length;
        if (momentum > 1000.0)
            LogDebug(null, "Achievement: Unstoppable Force - Momentum exceeded 1000!");
    }

    private void CheckConsecutiveImpactAchievement()
    {
        if (_consecutiveHeavyImpacts >= 5)
            LogDebug(null, "Achievement: Avalanche Starter - 5 consecutive heavy impacts!");
    }

    #endregion

    #region Movement Tracking

    private void TrackHeavyMovement(RigidBody body, double dt)
    {
        _timeSinceLastImpact += dt;
        if (_timeSinceLastImpact > 2.0)
        {
            _consecutiveHeavyImpacts = 0;
        }

        _recentPositions.Enqueue(body.Position);
        while (_recentPositions.Count > 30)
            _recentPositions.Dequeue();
    }

    #endregion

    #region Mass & Property Modifiers

    private void ApplyMassModifiers(RigidBody body)
    {
        if (_massMultiplierOverride != 1.0)
        {
            body.Mass = CalculateMassFromDensity() * _massMultiplierOverride;
        }
    }

    private double CalculateMassFromDensity()
    {
        double volume = (4.0 / 3.0) * Math.PI * Math.Pow(DefaultRadius, 3);
        return volume * _density;
    }

    #endregion

    #region Debug Logging

    private void LogPhysicsDebug(RigidBody body, double dt)
    {
        if (Config.DebugMode && GlobalConfig.Logger != null)
        {
            _physicsDebugLog.Clear();
            _physicsDebugLog.AppendLine($"[Heavy] Pos:{body.Position} Vel:{body.Velocity.Length:F2}");
            _physicsDebugLog.AppendLine($"Mass:{body.Mass:F2} Density:{_density:F2} Integrity:{_structuralState.CurrentIntegrity:F2}");
            _physicsDebugLog.AppendLine($"Cracks:{_structuralState.Cracks.Count} ImpactCount:{_impactCount}");
            _physicsDebugLog.AppendLine($"CrushMode:{_crushMode} Avalanche:{_avalancheMode}");
            LogDebug(body, _physicsDebugLog.ToString());
        }
    }

    private void UpdatePerformanceMetrics(RigidBody body, double dt)
    {
        _perfCounters.TotalImpacts = _impactCount;
        _perfCounters.PeakImpactForce = _peakImpactForce;
        _perfCounters.TotalCrushEvents = _crushCount;
        _perfCounters.PeakConcurrentWaves = Math.Max(_perfCounters.PeakConcurrentWaves, _seismicWaves.Count);
    }

    private void RecordPerformanceMetric(string key, double value)
    {
        if (key == "OnUpdate")
        {
            _perfCounters.TotalImpactTimeMs += value;
        }
    }

    #endregion

    #region Public API - Material & Profile Management

    public void SetMaterial(HeavyMaterial material)
    {
        _currentMaterial = material;
        _activeProfile = _heavyProfiles[material];
        _density = _activeProfile.Density;
        _structuralState.MaxIntegrity = STRUCTURAL_INTEGRITY_BASE * _activeProfile.StructuralIntegrity;
    }

    public void SetMaterial(string materialName)
    {
        foreach (var kvp in _heavyProfiles)
        {
            if (kvp.Value.Name.Equals(materialName, StringComparison.OrdinalIgnoreCase))
            {
                SetMaterial(kvp.Key);
                return;
            }
        }
    }

    public void SetCustomDensity(double density)
    {
        _customDensity = Math.Clamp(density, MINIMUM_DENSITY, MAXIMUM_DENSITY);
        _density = _customDensity;
        _currentMaterial = HeavyMaterial.Custom;
        _activeProfile = new HeavyProfile
        {
            Name = $"Custom ({_density:F1})",
            Density = _density,
            StructuralIntegrity = 0.8,
            ColorHex = "#8B7355",
            ImpactSound = "CustomHeavy",
            CrushResistance = 0.6,
            MomentumTransfer = 0.88
        };
    }

    public HeavyMaterial GetCurrentMaterial() => _currentMaterial;
    public HeavyProfile GetActiveProfile() => _activeProfile;
    public double GetCurrentDensity() => _density;

    #endregion

    #region Public API - Mode Toggles

    public void SetIndestructible(bool indestructible)
    {
        _isIndestructible = indestructible;
        if (indestructible)
        {
            _structuralState.CurrentIntegrity = _structuralState.MaxIntegrity;
            _structuralState.IsIntact = true;
        }
    }

    public void SetCrushMode(bool enabled) => _crushMode = enabled;
    public void SetAvalancheMode(bool enabled) => _avalancheMode = enabled;
    public void SetGroundAnchor(bool anchored) => _groundAnchor = anchored;
    public void SetPiercingMode(bool enabled, double forceThreshold = 100.0)
    {
        _isPiercing = enabled;
        _piercingForce = forceThreshold;
    }

    public void SetStructuralFailureEnabled(bool enabled) => _enableStructuralFailure = enabled;
    public void SetMomentumConservationEnabled(bool enabled) => _enableMomentumConservation = enabled;
    public void SetSeismicEffectsEnabled(bool enabled) => _enableSeismicEffects = enabled;
    public void SetCrushPhysicsEnabled(bool enabled) => _enableCrushPhysics = enabled;
    public void SetAvalancheModeEnabled(bool enabled) => _enableAvalancheMode = enabled;
    public void SetGroundInteractionEnabled(bool enabled) => _groundInteractionEnabled = enabled;

    #endregion

    #region Public API - Mass & Restitution

    public void SetMassMultiplier(double multiplier)
    {
        _massMultiplierOverride = Math.Max(0.1, multiplier);
    }

    public void SetRestitutionOverride(double restitution)
    {
        _restitutionOverride = Math.Clamp(restitution, 0.0, 1.0);
    }

    public void ResetOverrides()
    {
        _massMultiplierOverride = 1.0;
        _restitutionOverride = -1.0;
    }

    #endregion

    #region Public API - Structural Integrity

    public double GetStructuralIntegrity() => _structuralState.CurrentIntegrity;
    public double GetMaxStructuralIntegrity() => _structuralState.MaxIntegrity;
    public bool IsStructurallyIntact() => _structuralState.IsIntact;
    public IReadOnlyList<CrackPoint> GetCracks() => _structuralState.Cracks.AsReadOnly();
    public double GetStressAccumulation() => _structuralState.StressAccumulation;

    public void RepairStructuralIntegrity(double amount)
    {
        _structuralState.CurrentIntegrity = Math.Min(
            _structuralState.MaxIntegrity,
            _structuralState.CurrentIntegrity + amount
        );
        if (_structuralState.CurrentIntegrity >= _structuralState.MaxIntegrity * 0.8)
        {
            _crushMode = false;
            _structuralState.IsIntact = true;
        }
    }

    public void ClearCracks()
    {
        _structuralState.Cracks.Clear();
    }

    #endregion

    #region Public API - Impact & Momentum

    public int GetImpactCount() => _impactCount;
    public double GetTotalImpactEnergy() => _totalImpactEnergy;
    public double GetTotalMomentumTransferred() => _totalMomentumTransferred;
    public int GetCrushCount() => _crushCount;
    public double GetPeakImpactForce() => _peakImpactForce;
    public double GetGroundImpactDepth() => _groundImpactDepth;
    public double GetVibrationAmplitude() => _vibrationAmplitude;
    public IReadOnlyList<ImpactRecord> GetImpactHistory() => _impactHistory.AsReadOnly();
    public int GetConsecutiveHeavyImpacts() => _consecutiveHeavyImpacts;
    public IReadOnlyList<SeismicWave> GetActiveSeismicWaves() => _seismicWaves.AsReadOnly();

    #endregion

    #region Utility & Helper Methods

    private void UpdateImpactCooldown(double dt)
    {
        if (_consecutiveHeavyImpacts > 0 && _timeSinceLastImpact > 2.0)
        {
            _consecutiveHeavyImpacts = 0;
        }
    }

    private void UpdateMomentumTracking(RigidBody body, double dt)
    {
        // Tracking for momentum-based achievements
    }

    private void ProcessStructuralFailure(RigidBody body, PhysicsWorld world)
    {
        if (!_structuralState.IsIntact && _enableCrushPhysics)
        {
            double fragmentationForce = body.Mass * 10.0;
            if (fragmentationForce > 5000.0 && !_isIndestructible)
            {
                _crushForceAccumulator += fragmentationForce * 0.1;
            }
        }
    }

    private string GetHeavyDescription()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Massive weight with density-based physics:");
        sb.AppendLine($"- Material: {_activeProfile.Name}");
        sb.AppendLine($"- Density: {_density:F2}x standard");
        sb.AppendLine("- Enhanced gravity, momentum, and impact");
        sb.AppendLine("- Structural integrity with crack propagation");
        sb.AppendLine("- Seismic wave generation on heavy impacts");
        sb.AppendLine("- Avalanche mode for chain reactions");
        return sb.ToString();
    }

    public static List<HeavyProfile> GetAllHeavyProfiles()
    {
        return new List<HeavyProfile>(_heavyProfiles.Values);
    }

    public static HeavyProfile? GetProfileForMaterial(HeavyMaterial material)
    {
        return _heavyProfiles.TryGetValue(material, out var profile) ? profile : null;
    }

    #endregion

    #region Performance Tracking

    public class HeavyPerformanceCounters
    {
        public long TotalImpacts { get; set; }
        public double TotalImpactTimeMs { get; set; }
        public double AverageImpactTimeMs => TotalImpacts > 0 ? TotalImpactTimeMs / TotalImpacts : 0;
        public int PeakConcurrentWaves { get; set; }
        public double PeakImpactForce { get; set; }
        public int TotalCrushEvents { get; set; }
        public double TotalEnergyDissipated { get; set; }
    }

    private readonly HeavyPerformanceCounters _perfCounters = new();

    public HeavyPerformanceCounters GetPerformanceCounters() => _perfCounters;

    #endregion

    #region Serialization Support

    public string SerializeState()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Material:{_currentMaterial}");
        sb.AppendLine($"Density:{_density}");
        sb.AppendLine($"Indestructible:{_isIndestructible}");
        sb.AppendLine($"CrushMode:{_crushMode}");
        sb.AppendLine($"AvalancheMode:{_avalancheMode}");
        sb.AppendLine($"ImpactCount:{_impactCount}");
        sb.AppendLine($"CrushCount:{_crushCount}");
        sb.AppendLine($"TotalImpactEnergy:{_totalImpactEnergy}");
        sb.AppendLine($"TotalMomentumTransferred:{_totalMomentumTransferred}");
        sb.AppendLine($"PeakImpactForce:{_peakImpactForce}");
        sb.AppendLine($"ConsecutiveImpacts:{_consecutiveHeavyImpacts}");
        sb.AppendLine($"GroundImpactDepth:{_groundImpactDepth}");
        sb.AppendLine($"StructuralIntegrity:{_structuralState.CurrentIntegrity}");
        sb.AppendLine($"IsStructurallyIntact:{_structuralState.IsIntact}");
        sb.AppendLine($"MassMultiplier:{_massMultiplierOverride}");
        sb.AppendLine($"RestitutionOverride:{_restitutionOverride}");
        sb.AppendLine($"CrackCount:{_structuralState.Cracks.Count}");
        return sb.ToString();
    }

    public void DeserializeState(string data)
    {
        var lines = data.Split('\n');
        foreach (var line in lines)
        {
            var parts = line.Split(':');
            if (parts.Length < 2) continue;

            try
            {
                switch (parts[0])
                {
                    case "Material":
                        _currentMaterial = (HeavyMaterial)Enum.Parse(typeof(HeavyMaterial), parts[1]);
                        break;
                    case "Density":
                        _density = double.Parse(parts[1]);
                        break;
                    case "Indestructible":
                        _isIndestructible = bool.Parse(parts[1]);
                        break;
                    case "CrushMode":
                        _crushMode = bool.Parse(parts[1]);
                        break;
                    case "AvalancheMode":
                        _avalancheMode = bool.Parse(parts[1]);
                        break;
                    case "ImpactCount":
                        _impactCount = int.Parse(parts[1]);
                        break;
                    case "CrushCount":
                        _crushCount = int.Parse(parts[1]);
                        break;
                    case "TotalImpactEnergy":
                        _totalImpactEnergy = double.Parse(parts[1]);
                        break;
                    case "TotalMomentumTransferred":
                        _totalMomentumTransferred = double.Parse(parts[1]);
                        break;
                    case "PeakImpactForce":
                        _peakImpactForce = double.Parse(parts[1]);
                        break;
                    case "ConsecutiveImpacts":
                        _consecutiveHeavyImpacts = int.Parse(parts[1]);
                        break;
                    case "GroundImpactDepth":
                        _groundImpactDepth = double.Parse(parts[1]);
                        break;
                    case "StructuralIntegrity":
                        _structuralState.CurrentIntegrity = double.Parse(parts[1]);
                        break;
                    case "IsStructurallyIntact":
                        _structuralState.IsIntact = bool.Parse(parts[1]);
                        break;
                    case "MassMultiplier":
                        _massMultiplierOverride = double.Parse(parts[1]);
                        break;
                    case "RestitutionOverride":
                        _restitutionOverride = double.Parse(parts[1]);
                        break;
                    case "CrackCount":
                        break;
                }
            }
            catch (Exception ex) { DebugLog.WriteLine($"Failed to parse state line in HeavyBehaivor: {ex}"); }
        }

        if (_heavyProfiles.TryGetValue(_currentMaterial, out var profile))
        {
            _activeProfile = profile;
        }
    }

    #endregion
}
