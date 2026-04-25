using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Diagnostics;

namespace PhysicsSandbox.Behaviors;

public class BouncyBehavior : BodyBehavior
{
    #region Constants & Tunable Parameters

    private const double MAX_BOUNCE_ENERGY = 50000.0;
    private const double MIN_BOUNCE_VELOCITY = 0.5;
    private const double BOUNCE_DECAY_RATE = 0.98;
    private const double DEFAULT_SPIN_TRANSFER = 0.3;
    private const double MAX_CHAIN_REACTION_DISTANCE = 200.0;
    private const int MAX_CHAIN_REACTIONS = 10;
    private const double COMBO_WINDOW_SECONDS = 2.0;
    private const double TEMPERATURE_BOUNCE_FACTOR = 0.002;
    private const double AIR_RESISTANCE_BOUNCE_DAMPING = 0.001;
    private const int MAX_TRAJECTORY_PREDICTION_STEPS = 100;
    private const double TRAJECTORY_PREDICTION_DT = 0.016;
    private const int MAX_COLLISION_HISTORY = 50;
    private const double ACHIEVEMENT_SUPER_BOUNCE_VELOCITY = 500.0;
    private const double ACHIEVEMENT_CHAIN_MASTER_COUNT = 5;
    private const double ACHIEVEMENT_MARATHON_BOUNCES = 100;

    #endregion

    #region Bounce State Machine

    public enum BounceState
    {
        Idle,
        Charging,
        Bouncing,
        SuperBounce,
        Dampened,
        ChainReacting,
        Sliding,
        Stuck,
        Spinning,
        Airborne
    }

    public enum BounceCurveType
    {
        Linear,
        Quadratic,
        Cubic,
        Elastic,
        BounceEaseOut,
        ExponentialDecay,
        SineWave,
        Custom
    }

    private BounceState _currentState = BounceState.Idle;
    private BounceState _previousState = BounceState.Idle;
    private double _stateTimer = 0.0;
    private int _stateFrameCount = 0;
    private BounceCurveType _currentCurveType = BounceCurveType.Elastic;

    private class BounceStateMachine
    {
        public BounceState CurrentState { get; set; } = BounceState.Idle;
        public BounceState PreviousState { get; set; } = BounceState.Idle;
        public double StateTimer { get; set; } = 0.0;
        public int ConsecutiveBounces { get; set; } = 0;
        public double LastBounceTime { get; set; } = 0.0;
        public Vector2 LastBounceNormal { get; set; } = Vector2.Zero;
        public double StateEnergy { get; set; } = 0.0;

        public void TransitionTo(BounceState newState, double currentTime)
        {
            PreviousState = CurrentState;
            CurrentState = newState;
            StateTimer = 0.0;
            if (newState == BounceState.Bouncing)
                ConsecutiveBounces++;
            else if (newState != BounceState.SuperBounce)
                ConsecutiveBounces = 0;
        }
    }

    private readonly BounceStateMachine _stateMachine = new();

    #endregion

    #region Material Presets & Bounce Profiles

    public enum BounceMaterial
    {
        SuperBall,
        Rubber,
        TennisBall,
        Basketball,
        GolfBall,
        Trampoline,
        MemoryFoam,
        BouncyCastle,
        SpringSteel,
        GelPad,
        Custom
    }

    public class BounceProfile
    {
        public string Name { get; set; } = "";
        public double Restitution { get; set; } = 0.95;
        public double DampingDecay { get; set; } = 0.98;
        public double SpinTransfer { get; set; } = 0.3;
        public double MassMultiplier { get; set; } = 1.0;
        public double RadiusMultiplier { get; set; } = 1.0;
        public double EnergyRetention { get; set; } = 0.90;
        public double MaxBounceVelocity { get; set; } = 1000.0;
        public double BounceFrequency { get; set; } = 1.0;
        public double SurfaceGrip { get; set; } = 0.5;
        public double ElasticModulus { get; set; } = 1.0;
        public string ColorHex { get; set; } = "#81C784";
        public bool CanChainReact { get; set; } = false;
        public double ChainReactionForce { get; set; } = 0.0;
        public BounceCurveType DefaultCurve { get; set; } = BounceCurveType.Elastic;
    }

    private static readonly Dictionary<BounceMaterial, BounceProfile> _bounceProfiles = new()
    {
        {
            BounceMaterial.SuperBall, new BounceProfile
            {
                Name = "Super Ball",
                Restitution = 0.97,
                DampingDecay = 0.985,
                SpinTransfer = 0.4,
                MassMultiplier = 0.8,
                EnergyRetention = 0.94,
                MaxBounceVelocity = 2000.0,
                BounceFrequency = 2.5,
                SurfaceGrip = 0.3,
                ElasticModulus = 1.5,
                ColorHex = "#FF0000",
                CanChainReact = true,
                ChainReactionForce = 50.0,
                DefaultCurve = BounceCurveType.Elastic
            }
        },
        {
            BounceMaterial.Rubber, new BounceProfile
            {
                Name = "Rubber",
                Restitution = 0.85,
                DampingDecay = 0.96,
                SpinTransfer = 0.25,
                MassMultiplier = 1.0,
                EnergyRetention = 0.82,
                MaxBounceVelocity = 800.0,
                BounceFrequency = 1.5,
                SurfaceGrip = 0.7,
                ElasticModulus = 0.01,
                ColorHex = "#8B4513",
                CanChainReact = false,
                DefaultCurve = BounceCurveType.Quadratic
            }
        },
        {
            BounceMaterial.TennisBall, new BounceProfile
            {
                Name = "Tennis Ball",
                Restitution = 0.82,
                DampingDecay = 0.95,
                SpinTransfer = 0.35,
                MassMultiplier = 0.4,
                EnergyRetention = 0.78,
                MaxBounceVelocity = 600.0,
                BounceFrequency = 1.8,
                SurfaceGrip = 0.6,
                ElasticModulus = 0.5,
                ColorHex = "#FFFF00",
                CanChainReact = false,
                DefaultCurve = BounceCurveType.Cubic
            }
        },
        {
            BounceMaterial.Basketball, new BounceProfile
            {
                Name = "Basketball",
                Restitution = 0.75,
                DampingDecay = 0.92,
                SpinTransfer = 0.2,
                MassMultiplier = 1.2,
                EnergyRetention = 0.72,
                MaxBounceVelocity = 700.0,
                BounceFrequency = 1.2,
                SurfaceGrip = 0.8,
                ElasticModulus = 0.3,
                ColorHex = "#FF8C00",
                CanChainReact = false,
                DefaultCurve = BounceCurveType.BounceEaseOut
            }
        },
        {
            BounceMaterial.GolfBall, new BounceProfile
            {
                Name = "Golf Ball",
                Restitution = 0.80,
                DampingDecay = 0.97,
                SpinTransfer = 0.45,
                MassMultiplier = 0.5,
                EnergyRetention = 0.76,
                MaxBounceVelocity = 1500.0,
                BounceFrequency = 3.0,
                SurfaceGrip = 0.4,
                ElasticModulus = 2.0,
                ColorHex = "#FFFFFF",
                CanChainReact = true,
                ChainReactionForce = 30.0,
                DefaultCurve = BounceCurveType.Cubic
            }
        },
        {
            BounceMaterial.Trampoline, new BounceProfile
            {
                Name = "Trampoline",
                Restitution = 0.92,
                DampingDecay = 0.90,
                SpinTransfer = 0.1,
                MassMultiplier = 0.3,
                EnergyRetention = 0.88,
                MaxBounceVelocity = 1200.0,
                BounceFrequency = 0.8,
                SurfaceGrip = 0.2,
                ElasticModulus = 0.05,
                ColorHex = "#00FF00",
                CanChainReact = true,
                ChainReactionForce = 100.0,
                DefaultCurve = BounceCurveType.SineWave
            }
        },
        {
            BounceMaterial.MemoryFoam, new BounceProfile
            {
                Name = "Memory Foam",
                Restitution = 0.30,
                DampingDecay = 0.85,
                SpinTransfer = 0.05,
                MassMultiplier = 1.5,
                EnergyRetention = 0.35,
                MaxBounceVelocity = 300.0,
                BounceFrequency = 0.5,
                SurfaceGrip = 0.9,
                ElasticModulus = 0.001,
                ColorHex = "#FFB6C1",
                CanChainReact = false,
                DefaultCurve = BounceCurveType.ExponentialDecay
            }
        },
        {
            BounceMaterial.BouncyCastle, new BounceProfile
            {
                Name = "Bouncy Castle",
                Restitution = 0.88,
                DampingDecay = 0.93,
                SpinTransfer = 0.15,
                MassMultiplier = 0.6,
                EnergyRetention = 0.82,
                MaxBounceVelocity = 900.0,
                BounceFrequency = 0.9,
                SurfaceGrip = 0.3,
                ElasticModulus = 0.02,
                ColorHex = "#FF69B4",
                CanChainReact = true,
                ChainReactionForce = 80.0,
                DefaultCurve = BounceCurveType.BounceEaseOut
            }
        },
        {
            BounceMaterial.SpringSteel, new BounceProfile
            {
                Name = "Spring Steel",
                Restitution = 0.70,
                DampingDecay = 0.99,
                SpinTransfer = 0.5,
                MassMultiplier = 2.0,
                EnergyRetention = 0.68,
                MaxBounceVelocity = 1800.0,
                BounceFrequency = 4.0,
                SurfaceGrip = 0.25,
                ElasticModulus = 200.0,
                ColorHex = "#708090",
                CanChainReact = true,
                ChainReactionForce = 40.0,
                DefaultCurve = BounceCurveType.Linear
            }
        },
        {
            BounceMaterial.GelPad, new BounceProfile
            {
                Name = "Gel Pad",
                Restitution = 0.60,
                DampingDecay = 0.88,
                SpinTransfer = 0.08,
                MassMultiplier = 1.8,
                EnergyRetention = 0.55,
                MaxBounceVelocity = 400.0,
                BounceFrequency = 0.6,
                SurfaceGrip = 0.85,
                ElasticModulus = 0.005,
                ColorHex = "#DDA0DD",
                CanChainReact = false,
                DefaultCurve = BounceCurveType.ExponentialDecay
            }
        }
    };

    #endregion

    #region Bounce Coefficient Matrix

    private static readonly double[,] _bounceCoefficientMatrix = new double[11, 11]
    {
        {0.97, 0.85, 0.82, 0.75, 0.80, 0.92, 0.30, 0.88, 0.70, 0.60, 0.95},
        {0.85, 0.85, 0.78, 0.72, 0.76, 0.88, 0.28, 0.84, 0.68, 0.58, 0.83},
        {0.82, 0.78, 0.82, 0.70, 0.80, 0.86, 0.25, 0.80, 0.65, 0.55, 0.80},
        {0.75, 0.72, 0.70, 0.75, 0.73, 0.82, 0.22, 0.78, 0.62, 0.52, 0.73},
        {0.80, 0.76, 0.80, 0.73, 0.80, 0.84, 0.24, 0.79, 0.66, 0.54, 0.78},
        {0.92, 0.88, 0.86, 0.82, 0.84, 0.92, 0.28, 0.88, 0.68, 0.56, 0.90},
        {0.30, 0.28, 0.25, 0.22, 0.24, 0.28, 0.30, 0.26, 0.20, 0.18, 0.28},
        {0.88, 0.84, 0.80, 0.78, 0.79, 0.88, 0.26, 0.88, 0.66, 0.54, 0.86},
        {0.70, 0.68, 0.65, 0.62, 0.66, 0.68, 0.20, 0.66, 0.70, 0.50, 0.68},
        {0.60, 0.58, 0.55, 0.52, 0.54, 0.56, 0.18, 0.54, 0.50, 0.60, 0.58},
        {0.95, 0.83, 0.80, 0.73, 0.78, 0.90, 0.28, 0.86, 0.68, 0.58, 0.95}
    };

    #endregion

    #region Instance State & Configuration

    private BounceMaterial _currentMaterial = BounceMaterial.SuperBall;
    private BounceProfile _activeProfile = _bounceProfiles[BounceMaterial.SuperBall];
    private double _customRestitution = 0.95;
    private double _customDamping = 0.98;

    private Vector2 _velocityBeforeBounce = Vector2.Zero;
    private Vector2 _velocityAfterBounce = Vector2.Zero;
    private double _bounceEnergy = 0.0;
    private double _totalBounceEnergy = 0.0;
    private int _bounceCount = 0;
    private int _consecutiveBounces = 0;
    private double _lastBounceTime = 0.0;
    private double _timeSinceLastBounce = double.MaxValue;

    private readonly List<Vector2> _predictedTrajectory = new();
    private readonly List<CollisionRecord> _collisionHistory = new();
    private readonly Dictionary<string, double> _achievements = new();

    private double _environmentalTemperature = 20.0;
    private double _bouncinessTemperatureFactor = 1.0;
    private bool _isInVacuum = false;

    private int _currentCombo = 0;
    private double _comboTimer = 0.0;
    private int _maxCombo = 0;
    private double _comboMultiplier = 1.0;

    private readonly Stopwatch _updateStopwatch = new();
    private double _peakBounceVelocity = 0.0;
    private double _totalDistanceTraveled = 0.0;
    private Vector2 _lastPosition = Vector2.Zero;

    private bool _enableChainReactions = true;
    private bool _enableBounceCombo = true;
    private bool _enableTrajectoryPrediction = false;
    private bool _enableAchievementTracking = true;

    private class CollisionRecord
    {
        public Vector2 Position { get; set; }
        public Vector2 Normal { get; set; }
        public double ImpactForce { get; set; }
        public double Time { get; set; }
        public int OtherBodyId { get; set; }
        public BounceState StateAtCollision { get; set; }
    }

    #endregion

    #region Particle & Sound Effect System (Stubs)

    public enum ParticleEffectType
    {
        BounceSpark,
        DustPuff,
        Deformation,
        EnergyBurst,
        ChainReaction,
        SuperBounce,
        SpinTrail
    }

    public enum SoundEffectType
    {
        BounceSoft,
        BounceHard,
        BounceSuper,
        ChainReaction,
        ComboActivate,
        AchievementUnlock,
        SurfaceSlide
    }

    private readonly Queue<ParticleEffectRequest> _pendingParticles = new();
    private readonly Queue<SoundEffectRequest> _pendingSounds = new();

    private class ParticleEffectRequest
    {
        public ParticleEffectType Type { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public double Intensity { get; set; }
        public double Time { get; set; }
    }

    private class SoundEffectRequest
    {
        public SoundEffectType Type { get; set; }
        public Vector2 Position { get; set; }
        public double Volume { get; set; }
        public double Pitch { get; set; }
        public double Time { get; set; }
    }

    public void TriggerParticleEffect(ParticleEffectType type, Vector2 position, Vector2 velocity, double intensity)
    {
        _pendingParticles.Enqueue(new ParticleEffectRequest
        {
            Type = type,
            Position = position,
            Velocity = velocity,
            Intensity = intensity,
            Time = 0.0
        });
    }

    public void TriggerSoundEffect(SoundEffectType type, Vector2 position, double volume, double pitch)
    {
        _pendingSounds.Enqueue(new SoundEffectRequest
        {
            Type = type,
            Position = position,
            Volume = volume,
            Pitch = pitch,
            Time = 0.0
        });
    }

    #endregion

    #region Behavior Properties (Overrides)

    public override BodyType Type => BodyType.Bouncy;
    public override string Name => "Bouncy";
    public override string Description => "Super bouncy body with advanced physics, state machines, chain reactions, and energy tracking";
    public override string ColorHex => _activeProfile.ColorHex;
    public override double DefaultRadius => 12;
    public override double DefaultMass => 6;
    public override double DefaultRestitution => _activeProfile.Restitution;

    #endregion

    #region Constructors & Initialization

    public BouncyBehavior() : this(BounceMaterial.SuperBall) { }

    public BouncyBehavior(BounceMaterial material)
    {
        _currentMaterial = material;
        _activeProfile = _bounceProfiles[material];
        _currentCurveType = _activeProfile.DefaultCurve;
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);

        body.Restitution = _activeProfile.Restitution;
        body.Mass = DefaultMass * _activeProfile.MassMultiplier;
        body.Radius = DefaultRadius * _activeProfile.RadiusMultiplier;

        _lastPosition = body.Position;
        _stateMachine.CurrentState = BounceState.Idle;

        LogDebug(body, $"BouncyBehavior initialized: Material={_currentMaterial}, Restitution={body.Restitution:F2}, Mass={body.Mass:F2}");
    }

    #endregion

    #region Main Update Loop

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        _updateStopwatch.Restart();

        try
        {
            if (body.IsStatic || body.IsFrozen)
                return;

            RaisePreUpdate(body, dt);

            UpdateTemperatureEffects(dt);
            UpdateStateMachine(body, dt, world);
            ApplyBounceDecay(body, dt);
            UpdateTrajectoryPrediction(body, dt, world);
            UpdateComboSystem(body, dt);
            ProcessChainReactions(body, dt, world);
            UpdateAchievementTracking(body, dt);
            ProcessPendingEffects(body);
            TrackStatistics(body, dt);
            ClampBounceVelocity(body);

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
        _timeSinceLastBounce += dt;

        switch (_stateMachine.CurrentState)
        {
            case BounceState.Idle:
                if (body.Velocity.LengthSquared > MIN_BOUNCE_VELOCITY * MIN_BOUNCE_VELOCITY)
                {
                    _stateMachine.TransitionTo(BounceState.Airborne, 0.0);
                }
                break;

            case BounceState.Airborne:
                if (_timeSinceLastBounce < 0.1)
                {
                    _stateMachine.TransitionTo(BounceState.Bouncing, 0.0);
                }
                if (body.Velocity.Length > _activeProfile.MaxBounceVelocity)
                {
                    _stateMachine.TransitionTo(BounceState.SuperBounce, 0.0);
                }
                break;

            case BounceState.Bouncing:
                if (_stateMachine.ConsecutiveBounces >= 3)
                {
                    _stateMachine.TransitionTo(BounceState.Spinning, 0.0);
                }
                if (_timeSinceLastBounce > 1.0)
                {
                    _stateMachine.TransitionTo(BounceState.Dampened, 0.0);
                }
                break;

            case BounceState.SuperBounce:
                if (body.Velocity.Length < _activeProfile.MaxBounceVelocity * 0.5)
                {
                    _stateMachine.TransitionTo(BounceState.Bouncing, 0.0);
                }
                break;

            case BounceState.Dampened:
                if (body.Velocity.LengthSquared < MIN_BOUNCE_VELOCITY * MIN_BOUNCE_VELOCITY)
                {
                    _stateMachine.TransitionTo(BounceState.Idle, 0.0);
                }
                break;

            case BounceState.Spinning:
                if (Math.Abs(body.AngularVelocity) < 0.1)
                {
                    _stateMachine.TransitionTo(BounceState.Airborne, 0.0);
                }
                break;

            case BounceState.ChainReacting:
                if (_stateMachine.StateTimer > 0.5)
                {
                    _stateMachine.TransitionTo(BounceState.Airborne, 0.0);
                }
                break;

            case BounceState.Stuck:
                if (body.Velocity.LengthSquared > MIN_BOUNCE_VELOCITY * MIN_BOUNCE_VELOCITY)
                {
                    _stateMachine.TransitionTo(BounceState.Airborne, 0.0);
                }
                break;
        }

        _currentState = _stateMachine.CurrentState;
        _stateFrameCount++;
    }

    #endregion

    #region Bounce Physics Engine

    private double ApplyBounceCurve(double input, BounceCurveType curveType)
    {
        return curveType switch
        {
            BounceCurveType.Linear => input,
            BounceCurveType.Quadratic => input * input,
            BounceCurveType.Cubic => input * input * input,
            BounceCurveType.Elastic => input * (1.0 + 0.3 * Math.Sin(input * Math.PI * 2)),
            BounceCurveType.BounceEaseOut => EaseOutBounce(input),
            BounceCurveType.ExponentialDecay => 1.0 - Math.Exp(-3.0 * input),
            BounceCurveType.SineWave => (Math.Sin(input * Math.PI * 2 - Math.PI / 2) + 1.0) / 2.0,
            BounceCurveType.Custom => ApplyCustomBounceCurve(input),
            _ => input
        };
    }

    private double EaseOutBounce(double x)
    {
        if (x < 1 / 2.75)
            return 7.5625 * x * x;
        else if (x < 2 / 2.75)
            return 7.5625 * (x -= 1.5 / 2.75) * x + 0.75;
        else if (x < 2.5 / 2.75)
            return 7.5625 * (x -= 2.25 / 2.75) * x + 0.9375;
        else
            return 7.5625 * (x -= 2.625 / 2.75) * x + 0.984375;
    }

    private double ApplyCustomBounceCurve(double input)
    {
        double a = 1.5;
        double b = 2.0;
        return Math.Pow(input, a) * (b - (b - 1) * input);
    }

    private void ApplyBounceDecay(RigidBody body, double dt)
    {
        if (_timeSinceLastBounce > 0.5)
        {
            double decayFactor = Math.Pow(_activeProfile.DampingDecay, dt * 60);
            body.Velocity *= decayFactor;

            if (!_isInVacuum && body.Velocity.Length > 50)
            {
                double airDamping = 1.0 - (AIR_RESISTANCE_BOUNCE_DAMPING * body.Velocity.Length * 0.01);
                body.Velocity *= Math.Max(0.8, airDamping);
            }
        }
    }

    private void CalculateBounceResponse(RigidBody body, RigidBody other, out Vector2 bounceImpulse, out double energyLoss)
    {
        Vector2 collisionNormal = (other.Position - body.Position).Normalized;
        double restitution = GetBounceCoefficient(body, other);

        Vector2 relativeVel = other.Velocity - body.Velocity;
        double closingSpeed = Vector2.Dot(relativeVel, collisionNormal);

        if (closingSpeed > 0)
        {
            bounceImpulse = Vector2.Zero;
            energyLoss = 0;
            return;
        }

        double invMassSum = body.InverseMass + other.InverseMass;
        if (invMassSum < 1e-6)
        {
            bounceImpulse = Vector2.Zero;
            energyLoss = 0;
            return;
        }

        double j = -(1 + restitution) * closingSpeed / invMassSum;
        bounceImpulse = collisionNormal * j;

        double preEnergy = 0.5 * body.Mass * body.Velocity.LengthSquared + 0.5 * other.Mass * other.Velocity.LengthSquared;
        double postEnergy = preEnergy - (0.5 * j * j / invMassSum);
        energyLoss = Math.Max(0, preEnergy - postEnergy);

        double curvedLoss = ApplyBounceCurve(energyLoss / Math.Max(preEnergy, 1.0), _currentCurveType);
        energyLoss = preEnergy * curvedLoss;
    }

    private double GetBounceCoefficient(RigidBody body, RigidBody other)
    {
        if (other.BodyType == BodyType.Bouncy && other.Behavior is BouncyBehavior otherBouncy)
        {
            int mat1 = (int)_currentMaterial;
            int mat2 = (int)otherBouncy._currentMaterial;
            return _bounceCoefficientMatrix[mat1, mat2] * _bouncinessTemperatureFactor;
        }
        return body.Restitution * _bouncinessTemperatureFactor;
    }

    #endregion

    #region Collision Handling

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        _bounceCount++;
        _stateMachine.ConsecutiveBounces++;
        _timeSinceLastBounce = 0.0;
        _lastBounceTime = 0.0;

        _velocityBeforeBounce = body.Velocity;

        CalculateBounceResponse(body, other, out Vector2 impulse, out double energyLoss);

        if (impulse != Vector2.Zero)
        {
            if (!body.IsStatic)
                body.ApplyImpulse(impulse);
            if (!other.IsStatic)
                other.ApplyImpulse(-impulse);

            _bounceEnergy = energyLoss;
            _totalBounceEnergy += energyLoss;
        }

        ApplySpinTransfer(body, other);
        RecordCollision(body, other, impulse.Length);
        TriggerBounceEffects(body, impulse.Length);

        if (_enableChainReactions && _activeProfile.CanChainReact)
        {
            TriggerChainReaction(body, world, impulse.Length);
        }

        _velocityAfterBounce = body.Velocity;
        double speed = body.Velocity.Length;
        if (speed > _peakBounceVelocity)
            _peakBounceVelocity = speed;

        LogDebug(body, $"Bounce #{_bounceCount}: EnergyLoss={energyLoss:F2}, Speed={speed:F2}, Consecutive={_stateMachine.ConsecutiveBounces}");

        RaiseCollision(body, other);
    }

    private void ApplySpinTransfer(RigidBody body, RigidBody other)
    {
        if (body.IsStatic || other.IsStatic)
            return;

        Vector2 collisionNormal = (other.Position - body.Position).Normalized;
        Vector2 tangent = new Vector2(-collisionNormal.Y, collisionNormal.X);
        double tangentVelocity = Vector2.Dot(body.Velocity - other.Velocity, tangent);

        double spinTransfer = _activeProfile.SpinTransfer * DEFAULT_SPIN_TRANSFER;
        double angularImpulse = tangentVelocity * spinTransfer * body.Mass;

        body.AngularVelocity += angularImpulse / (0.4 * body.Mass * body.Radius * body.Radius);
    }

    private void RecordCollision(RigidBody body, RigidBody other, double impactForce)
    {
        var record = new CollisionRecord
        {
            Position = body.Position,
            Normal = (other.Position - body.Position).Normalized,
            ImpactForce = impactForce,
            Time = 0.0,
            OtherBodyId = other.Id,
            StateAtCollision = _stateMachine.CurrentState
        };

        _collisionHistory.Add(record);
        while (_collisionHistory.Count > MAX_COLLISION_HISTORY)
            _collisionHistory.RemoveAt(0);
    }

    private void TriggerBounceEffects(RigidBody body, double intensity)
    {
        ParticleEffectType particleType = intensity > 500 ? ParticleEffectType.SuperBounce :
                                        intensity > 200 ? ParticleEffectType.BounceSpark :
                                        ParticleEffectType.DustPuff;

        TriggerParticleEffect(particleType, body.Position, body.Velocity, intensity / 100.0);

        SoundEffectType soundType = intensity > 500 ? SoundEffectType.BounceSuper :
                                     intensity > 200 ? SoundEffectType.BounceHard :
                                     SoundEffectType.BounceSoft;

        TriggerSoundEffect(soundType, body.Position, Math.Min(1.0, intensity / 500.0), 1.0 + intensity / 1000.0);
    }

    #endregion

    #region Chain Reaction System

    private void TriggerChainReaction(RigidBody body, PhysicsWorld world, double initialForce)
    {
        if (!_activeProfile.CanChainReact)
            return;

        _stateMachine.TransitionTo(BounceState.ChainReacting, 0.0);

        int reactions = 0;
        double currentForce = initialForce;

        foreach (var other in SpatialQuery(body.Position, MAX_CHAIN_REACTION_DISTANCE, world))
        {
            if (other == body || other.IsStatic)
                continue;
            if (reactions >= MAX_CHAIN_REACTIONS)
                break;

            double distSq = (body.Position - other.Position).LengthSquared;
            double falloff = 1.0 - Math.Sqrt(distSq) / MAX_CHAIN_REACTION_DISTANCE;
            double chainForce = currentForce * _activeProfile.ChainReactionForce * falloff / 100.0;

            if (chainForce > 10.0)
            {
                Vector2 dir = (other.Position - body.Position).Normalized;
                other.ApplyImpulse(dir * chainForce);
                reactions++;

                if (other.Behavior is BouncyBehavior otherBouncy && otherBouncy._activeProfile.CanChainReact)
                {
                    currentForce *= 0.7;
                }
            }
        }

        if (reactions > 0)
        {
            TriggerParticleEffect(ParticleEffectType.ChainReaction, body.Position, Vector2.Zero, reactions);
            TriggerSoundEffect(SoundEffectType.ChainReaction, body.Position, 0.5, 1.2);
        }
    }

    private void ProcessChainReactions(RigidBody body, double dt, PhysicsWorld world)
    {
        if (_stateMachine.CurrentState != BounceState.ChainReacting)
            return;

        if (_stateMachine.StateTimer > 0.5)
        {
            _stateMachine.TransitionTo(BounceState.Airborne, 0.0);
        }
    }

    #endregion

    #region Combo & Achievement System

    private void UpdateComboSystem(RigidBody body, double dt)
    {
        if (!_enableBounceCombo)
            return;

        if (_timeSinceLastBounce < COMBO_WINDOW_SECONDS)
        {
            _comboTimer += dt;
            _currentCombo = _stateMachine.ConsecutiveBounces;
            _comboMultiplier = 1.0 + (_currentCombo * 0.1);
            if (_currentCombo > _maxCombo)
                _maxCombo = _currentCombo;
        }
        else
        {
            _currentCombo = 0;
            _comboMultiplier = 1.0;
            _comboTimer = 0.0;
        }
    }

    private void UpdateAchievementTracking(RigidBody body, double dt)
    {
        if (!_enableAchievementTracking)
            return;

        double speed = body.Velocity.Length;

        if (speed > ACHIEVEMENT_SUPER_BOUNCE_VELOCITY && !_achievements.ContainsKey("SuperBouncer"))
        {
            _achievements["SuperBouncer"] = 1.0;
            TriggerSoundEffect(SoundEffectType.AchievementUnlock, body.Position, 1.0, 1.5);
        }

        if (_stateMachine.ConsecutiveBounces >= ACHIEVEMENT_CHAIN_MASTER_COUNT && !_achievements.ContainsKey("ChainMaster"))
        {
            _achievements["ChainMaster"] = 1.0;
            TriggerSoundEffect(SoundEffectType.AchievementUnlock, body.Position, 1.0, 1.8);
        }

        if (_bounceCount >= ACHIEVEMENT_MARATHON_BOUNCES && !_achievements.ContainsKey("MarathonBouncer"))
        {
            _achievements["MarathonBouncer"] = 1.0;
            TriggerSoundEffect(SoundEffectType.AchievementUnlock, body.Position, 1.0, 2.0);
        }
    }

    #endregion

    #region Trajectory Prediction

    private void UpdateTrajectoryPrediction(RigidBody body, double dt, PhysicsWorld world)
    {
        if (!_enableTrajectoryPrediction)
            return;

        _predictedTrajectory.Clear();
        Vector2 pos = body.Position;
        Vector2 vel = body.Velocity;
        double simDt = TRAJECTORY_PREDICTION_DT;

        for (int i = 0; i < MAX_TRAJECTORY_PREDICTION_STEPS; i++)
        {
            _predictedTrajectory.Add(pos);
            vel += world.Gravity * simDt;
            vel *= _activeProfile.DampingDecay;
            pos += vel * simDt;

            if (pos.Y + body.Radius >= world.GroundY)
            {
                pos.Y = world.GroundY - body.Radius;
                vel.Y = -vel.Y * body.Restitution;
                vel.X *= _activeProfile.SurfaceGrip;
            }

            if (vel.LengthSquared < 1.0)
                break;
        }
    }

    #endregion

    #region Temperature Effects

    private void UpdateTemperatureEffects(double dt)
    {
        double referenceTemp = 20.0;
        double tempDiff = _environmentalTemperature - referenceTemp;
        _bouncinessTemperatureFactor = 1.0 + (tempDiff * TEMPERATURE_BOUNCE_FACTOR);

        if (_bouncinessTemperatureFactor < 0.5)
            _bouncinessTemperatureFactor = 0.5;
        if (_bouncinessTemperatureFactor > 1.5)
            _bouncinessTemperatureFactor = 1.5;
    }

    #endregion

    #region Statistics Tracking

    private void TrackStatistics(RigidBody body, double dt)
    {
        double distMoved = Vector2.Distance(body.Position, _lastPosition);
        _totalDistanceTraveled += distMoved;
        _lastPosition = body.Position;
    }

    private void ClampBounceVelocity(RigidBody body)
    {
        double speedSq = body.Velocity.LengthSquared;
        if (speedSq > MAX_BOUNCE_ENERGY)
        {
            double scale = Math.Sqrt(MAX_BOUNCE_ENERGY) / Math.Sqrt(speedSq);
            body.Velocity *= scale;
        }
    }

    #endregion

    #region Visual Feedback & Debug Visualization

    protected override void RenderDebugOverlay(RigidBody body, DrawingContext dc)
    {
        if (dc == null || !GlobalConfig.EnableDebugVisualization)
            return;

        DrawBounceStateIndicator(body, dc);
        DrawTrajectoryPrediction(body, dc);
        DrawCollisionHistory(body, dc);
        DrawEnergyBar(body, dc);
        DrawComboIndicator(body, dc);
    }

    private void DrawBounceStateIndicator(RigidBody body, DrawingContext dc)
    {
        var stateColors = new Dictionary<BounceState, Brush>
        {
            { BounceState.Idle, Brushes.Gray },
            { BounceState.Airborne, Brushes.LightBlue },
            { BounceState.Bouncing, Brushes.Yellow },
            { BounceState.SuperBounce, Brushes.Red },
            { BounceState.Dampened, Brushes.Orange },
            { BounceState.Spinning, Brushes.Purple },
            { BounceState.ChainReacting, Brushes.Green },
            { BounceState.Stuck, Brushes.DarkGray },
            { BounceState.Charging, Brushes.Cyan },
            { BounceState.Sliding, Brushes.Brown }
        };

        if (stateColors.TryGetValue(_currentState, out var brush))
        {
            dc.DrawEllipse(brush, new Pen(Brushes.Black, 1), new Point(body.Position.X, body.Position.Y), 5, 5);
        }
    }

    private void DrawTrajectoryPrediction(RigidBody body, DrawingContext dc)
    {
        if (!_enableTrajectoryPrediction || _predictedTrajectory.Count < 2)
            return;

        for (int i = 1; i < _predictedTrajectory.Count; i++)
        {
            double opacity = 1.0 - (double)i / _predictedTrajectory.Count;
            var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 128), 0, 255, 255)), 1.0);
            dc.DrawLine(pen,
                new Point(_predictedTrajectory[i - 1].X, _predictedTrajectory[i - 1].Y),
                new Point(_predictedTrajectory[i].X, _predictedTrajectory[i].Y));
        }
    }

    private void DrawCollisionHistory(RigidBody body, DrawingContext dc)
    {
        for (int i = 0; i < _collisionHistory.Count; i++)
        {
            var record = _collisionHistory[i];
            double opacity = (double)(i + 1) / _collisionHistory.Count;
            var brush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 100), 255, 0, 0));
            dc.DrawEllipse(brush, null, new Point(record.Position.X, record.Position.Y), 3, 3);
        }
    }

    private void DrawEnergyBar(RigidBody body, DrawingContext dc)
    {
        double energy = _bounceEnergy / MAX_BOUNCE_ENERGY;
        double barWidth = 20;
        double barHeight = 5;
        var brush = energy > 0.8 ? Brushes.Red : energy > 0.5 ? Brushes.Yellow : Brushes.Green;
        dc.DrawRectangle(brush, null, new Rect(body.Position.X - barWidth / 2, body.Position.Y - body.Radius - 10, barWidth * energy, barHeight));
    }

    private void DrawComboIndicator(RigidBody body, DrawingContext dc)
    {
        if (_currentCombo <= 1)
            return;

        var typeface = new Typeface("Consolas");
        var formatted = new FormattedText(
            $"x{_currentCombo} COMBO!",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            12,
            Brushes.Gold);
        dc.DrawText(formatted, new Point(body.Position.X, body.Position.Y - body.Radius - 25));
    }

    #endregion

    #region Public API for Runtime Modification

    public void SetMaterial(RigidBody body, BounceMaterial material)
    {
        _currentMaterial = material;
        _activeProfile = _bounceProfiles[material];
        _currentCurveType = _activeProfile.DefaultCurve;

        body.Restitution = _activeProfile.Restitution;
        body.Mass = DefaultMass * _activeProfile.MassMultiplier;
    }

    public void SetCustomRestitution(double restitution)
    {
        _customRestitution = Math.Clamp(restitution, 0.0, 1.0);
        _currentMaterial = BounceMaterial.Custom;
    }

    public void SetChainReactionsEnabled(bool enabled) => _enableChainReactions = enabled;
    public void SetComboEnabled(bool enabled) => _enableBounceCombo = enabled;
    public void SetTrajectoryPredictionEnabled(bool enabled) => _enableTrajectoryPrediction = enabled;
    public void SetAchievementTrackingEnabled(bool enabled) => _enableAchievementTracking = enabled;

    public void SetEnvironmentalTemperature(double temp)
    {
        _environmentalTemperature = temp;
    }

    public void SetVacuum(bool inVacuum) => _isInVacuum = inVacuum;

    public BounceMaterial GetCurrentMaterial() => _currentMaterial;
    public int GetBounceCount() => _bounceCount;
    public double GetTotalBounceEnergy() => _totalBounceEnergy;
    public int GetCurrentCombo() => _currentCombo;
    public double GetComboMultiplier() => _comboMultiplier;
    public BounceState GetCurrentState() => _currentState;
    public IReadOnlyList<Vector2> GetPredictedTrajectory() => _predictedTrajectory.AsReadOnly();
    public IReadOnlyDictionary<string, double> GetAchievements() => _achievements;

    #endregion

    #region Utility & Helper Methods

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

    public static BounceProfile? GetProfileForMaterial(BounceMaterial material)
    {
        return _bounceProfiles.TryGetValue(material, out var profile) ? profile : null;
    }

    public static List<BounceProfile> GetAllBounceProfiles()
    {
        return new List<BounceProfile>(_bounceProfiles.Values);
    }

    public string GetDiagnosticsReport(RigidBody body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== BouncyBehavior Diagnostics ===");
        sb.AppendLine($"Material: {_currentMaterial}");
        sb.AppendLine($"State: {_currentState} (Timer: {_stateTimer:F2}s)");
        sb.AppendLine($"Bounce Count: {_bounceCount}");
        sb.AppendLine($"Consecutive Bounces: {_stateMachine.ConsecutiveBounces}");
        sb.AppendLine($"Peak Velocity: {_peakBounceVelocity:F2}");
        sb.AppendLine($"Total Distance: {_totalDistanceTraveled:F2}");
        sb.AppendLine($"Bounce Energy: {_bounceEnergy:F2}");
        sb.AppendLine($"Total Bounce Energy: {_totalBounceEnergy:F2}");
        sb.AppendLine($"Combo: {_currentCombo} (Multiplier: {_comboMultiplier:F2})");
        sb.AppendLine($"Temperature: {_environmentalTemperature:F1}C (Factor: {_bouncinessTemperatureFactor:F3})");
        sb.AppendLine($"In Vacuum: {_isInVacuum}");
        sb.AppendLine($"Collision History: {_collisionHistory.Count}");
        sb.AppendLine($"Achievements: {_achievements.Count}");
        return sb.ToString();
    }

    #endregion

    #region Performance Tracking

    public class BouncyPerformanceCounters
    {
        public long TotalBounces { get; set; }
        public double TotalBounceTimeMs { get; set; }
        public double AverageBounceTimeMs => TotalBounces > 0 ? TotalBounceTimeMs / TotalBounces : 0;
        public int ActiveChainReactions { get; set; }
        public int PeakCombo { get; set; }
        public double PeakBounceEnergy { get; set; }
    }

    private readonly BouncyPerformanceCounters _perfCounters = new();

    public BouncyPerformanceCounters GetPerformanceCounters() => _perfCounters;

    #endregion

    #region Serialization Support

    public string SerializeState()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Material:{_currentMaterial}");
        sb.AppendLine($"BounceCount:{_bounceCount}");
        sb.AppendLine($"TotalBounceEnergy:{_totalBounceEnergy}");
        sb.AppendLine($"PeakVelocity:{_peakBounceVelocity}");
        sb.AppendLine($"Combo:{_currentCombo}");
        sb.AppendLine($"MaxCombo:{_maxCombo}");
        sb.AppendLine($"Temperature:{_environmentalTemperature}");
        sb.AppendLine($"IsInVacuum:{_isInVacuum}");
        sb.AppendLine($"EnableChainReactions:{_enableChainReactions}");
        sb.AppendLine($"EnableCombo:{_enableBounceCombo}");
        sb.AppendLine($"EnableTrajectory:{_enableTrajectoryPrediction}");
        sb.AppendLine($"CurrentState:{_currentState}");
        sb.AppendLine($"ConsecutiveBounces:{_stateMachine.ConsecutiveBounces}");
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
                    case "Material":
                        if (Enum.TryParse(parts[1], out BounceMaterial mat))
                            _currentMaterial = mat;
                        break;
                    case "BounceCount":
                        _bounceCount = int.Parse(parts[1]);
                        break;
                    case "TotalBounceEnergy":
                        _totalBounceEnergy = double.Parse(parts[1]);
                        break;
                    case "PeakVelocity":
                        _peakBounceVelocity = double.Parse(parts[1]);
                        break;
                    case "Combo":
                        _currentCombo = int.Parse(parts[1]);
                        break;
                    case "MaxCombo":
                        _maxCombo = int.Parse(parts[1]);
                        break;
                    case "Temperature":
                        _environmentalTemperature = double.Parse(parts[1]);
                        break;
                    case "IsInVacuum":
                        _isInVacuum = bool.Parse(parts[1]);
                        break;
                    case "EnableChainReactions":
                        _enableChainReactions = bool.Parse(parts[1]);
                        break;
                    case "EnableCombo":
                        _enableBounceCombo = bool.Parse(parts[1]);
                        break;
                    case "EnableTrajectory":
                        _enableTrajectoryPrediction = bool.Parse(parts[1]);
                        break;
                    case "CurrentState":
                        if (Enum.TryParse(parts[1], out BounceState parsedState))
                            _currentState = parsedState;
                        break;
                    case "ConsecutiveBounces":
                        _stateMachine.ConsecutiveBounces = int.Parse(parts[1]);
                        break;
                }
            }
            catch { }
        }
    }

    #endregion
}
