using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Diagnostics;

namespace PhysicsSandbox.Behaviors;

public class PhantomBehavior : BodyBehavior
{
    #region Constants & Tunable Parameters

    private const double DEFAULT_SHAKE_STRENGTH = 8000.0;
    private const double DEFAULT_SHAKE_FREQUENCY = 50.0;
    private const double PHASE_DISTANCE_BUFFER = 10.0;
    private const double PHASE_COOLDOWN = 0.05;
    private const int MAX_SHAKE_TARGETS = 15;
    private const double SMOOTH_PHASE_SPEED = 3.0;
    private const double TELEPORT_CHARGE_TIME = 2.5;
    private const double TELEPORT_RANGE = 400.0;
    private const double PHANTOM_MAX_ENERGY = 150.0;
    private const double ENERGY_REGEN_RATE = 15.0;
    private const double SHAKE_ENERGY_COST = 8.0;
    private const double TELEPORT_ENERGY_COST = 50.0;
    private const double INVISIBILITY_DURATION = 1.5;
    private const double INVISIBILITY_ENERGY_COST = 20.0;
    private const double SHAKE_DECAY_RATE = 0.95;
    private const int MAX_TELEPORT_HISTORY = 10;
    private const double ACHIEVEMENT_SHAKE_MASTER_COUNT = 50;
    private const double ACHIEVEMENT_TELEPORTER_COUNT = 10;
    private const double ACHIEVEMENT_PHASE_KING_COUNT = 100;
    private const double DECOY_SUMMON_COST = 30.0;
    private const double ENERGY_DRAIN_RATE = 5.0;
    private const double DECOY_LIFETIME = 5.0;
    private const double MAX_SHAKE_STRENGTH = 20000.0;
    private const double MIN_SHAKE_STRENGTH = 1000.0;
    private const double MAX_SHAKE_FREQUENCY = 100.0;
    private const double MIN_SHAKE_FREQUENCY = 10.0;

    #endregion

    #region Phantom State Machine

    public enum PhantomState
    {
        Idle,
        Phasing,
        Shaking,
        ChargingTeleport,
        Teleporting,
        CoolingDown,
        Invisible,
        Draining
    }

    public enum PhantomType
    {
        Weak,
        Standard,
        Strong,
        Ghost,
        Spectral,
        Wraith,
        Decoy,
        Custom
    }

    private PhantomState _currentState = PhantomState.Idle;
    private PhantomState _previousState = PhantomState.Idle;
    private double _stateTimer = 0.0;
    private int _stateFrameCount = 0;

    private class PhantomStateMachine
    {
        public PhantomState CurrentState { get; set; } = PhantomState.Idle;
        public PhantomState PreviousState { get; set; } = PhantomState.Idle;
        public double StateTimer { get; set; } = 0.0;
        public int ShakeCount { get; set; } = 0;
        public int TeleportCount { get; set; } = 0;
        public double LastPhaseTime { get; set; } = 0.0;
        public double Energy { get; set; } = 100.0;

        public void TransitionTo(PhantomState newState, double currentTime)
        {
            PreviousState = CurrentState;
            CurrentState = newState;
            StateTimer = 0.0;
        }
    }

    private readonly PhantomStateMachine _stateMachine = new();

    #endregion

    #region Phantom Profiles

    public class PhantomProfile
    {
        public string Name { get; set; } = "";
        public double ShakeStrength { get; set; } = 8000.0;
        public double ShakeFrequency { get; set; } = 50.0;
        public double PhaseDistance { get; set; } = 10.0;
        public double MaxEnergy { get; set; } = 150.0;
        public double EnergyRegen { get; set; } = 15.0;
        public double ShakeCost { get; set; } = 8.0;
        public bool CanTeleport { get; set; } = true;
        public bool CanTurnInvisible { get; set; } = false;
        public double InvisibilityDuration { get; set; } = 1.5;
        public string ColorHex { get; set; } = "#B388FF";
        public double PhasingSpeed { get; set; } = 3.0;
        public double ShakeDecay { get; set; } = 0.95;
    }

    private static readonly Dictionary<PhantomType, PhantomProfile> _phantomProfiles = new()
    {
        {
            PhantomType.Weak, new PhantomProfile
            {
                Name = "Weak Phantom",
                ShakeStrength = 4000.0,
                ShakeFrequency = 30.0,
                PhaseDistance = 5.0,
                MaxEnergy = 80.0,
                EnergyRegen = 10.0,
                ShakeCost = 12.0,
                CanTeleport = false,
                ColorHex = "#CE93D8"
            }
        },
        {
            PhantomType.Standard, new PhantomProfile
            {
                Name = "Standard Phantom",
                ShakeStrength = 8000.0,
                ShakeFrequency = 50.0,
                PhaseDistance = 10.0,
                MaxEnergy = 150.0,
                EnergyRegen = 15.0,
                ShakeCost = 8.0,
                CanTeleport = true,
                ColorHex = "#B388FF"
            }
        },
        {
            PhantomType.Strong, new PhantomProfile
            {
                Name = "Strong Phantom",
                ShakeStrength = 12000.0,
                ShakeFrequency = 70.0,
                PhaseDistance = 15.0,
                MaxEnergy = 200.0,
                EnergyRegen = 20.0,
                ShakeCost = 6.0,
                CanTeleport = true,
                CanTurnInvisible = true,
                ColorHex = "#7C4DFF"
            }
        },
        {
            PhantomType.Ghost, new PhantomProfile
            {
                Name = "Ghost",
                ShakeStrength = 3000.0,
                ShakeFrequency = 20.0,
                PhaseDistance = 20.0,
                MaxEnergy = 100.0,
                EnergyRegen = 25.0,
                ShakeCost = 5.0,
                CanTeleport = false,
                CanTurnInvisible = true,
                InvisibilityDuration = 3.0,
                ColorHex = "#E1BEE7"
            }
        },
        {
            PhantomType.Spectral, new PhantomProfile
            {
                Name = "Spectral Phantom",
                ShakeStrength = 10000.0,
                ShakeFrequency = 60.0,
                PhaseDistance = 12.0,
                MaxEnergy = 180.0,
                EnergyRegen = 18.0,
                ShakeCost = 7.0,
                CanTeleport = true,
                CanTurnInvisible = true,
                ColorHex = "#AA00FF"
            }
        },
        {
            PhantomType.Wraith, new PhantomProfile
            {
                Name = "Wraith",
                ShakeStrength = 15000.0,
                ShakeFrequency = 80.0,
                PhaseDistance = 20.0,
                MaxEnergy = 250.0,
                EnergyRegen = 10.0,
                ShakeCost = 10.0,
                CanTeleport = true,
                CanTurnInvisible = true,
                InvisibilityDuration = 2.0,
                ColorHex = "#4A148C"
            }
        },
        {
            PhantomType.Decoy, new PhantomProfile
            {
                Name = "Decoy Phantom",
                ShakeStrength = 2000.0,
                ShakeFrequency = 10.0,
                PhaseDistance = 2.0,
                MaxEnergy = 50.0,
                EnergyRegen = 5.0,
                ShakeCost = 15.0,
                CanTeleport = false,
                CanTurnInvisible = false,
                ColorHex = "#FFCDD2"
            }
        }
    };

    #endregion

    #region Instance State & Configuration

    private PhantomType _currentType = PhantomType.Standard;
    private PhantomProfile _activeProfile = _phantomProfiles[PhantomType.Standard];
    private double _customShakeStrength = 8000.0;
    private double _customShakeFrequency = 50.0;
    private double _lifeTime = 0.0;
    private Vector2 _lastPosition = Vector2.Zero;
    private int _shakeCount = 0;
    private int _phaseCount = 0;
    private int _teleportCount = 0;
    private double _lastShakeTime = 0.0;
    private double _phantomEnergy = 150.0;
    private bool _isInvisible = false;
    private double _invisibilityTimer = 0.0;
    private Vector2 _teleportTarget = Vector2.Zero;
    private readonly List<Vector2> _teleportHistory = new();
    private readonly List<ShakeRecord> _shakeHistory = new();
    private readonly Dictionary<string, double> _achievements = new();
    private readonly Stopwatch _updateStopwatch = new();
    private double _peakShakeStrength = 0.0;
    private double _totalDistanceTraveled = 0.0;
    private bool _enableTeleport = true;
    private bool _enableInvisibility = true;
    private bool _enableAchievements = true;
    private bool _enableEffects = true;
    private bool _decoySummoned = false;
    private double _decoyLifetime = 0.0;

    private class ShakeRecord
    {
        public Vector2 Position { get; set; }
        public double Strength { get; set; }
        public double Time { get; set; }
        public int TargetCount { get; set; }
    }

    #endregion

    #region Visual Effect System

    public enum PhantomParticleType
    {
        PhasingTrail,
        ShakeWave,
        TeleportBurst,
        InvisibilityFade,
        PhaseSpark,
        EnergyDrain
    }

    public enum PhantomSoundType
    {
        Phasing,
        Shake,
        Teleport,
        InvisibilityOn,
        InvisibilityOff,
        AchievementUnlock
    }

    private readonly Queue<PhantomParticleRequest> _pendingParticles = new();
    private readonly Queue<PhantomSoundRequest> _pendingSounds = new();

    private class PhantomParticleRequest
    {
        public PhantomParticleType Type { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public double Intensity { get; set; }
        public double Time { get; set; }
    }

    private class PhantomSoundRequest
    {
        public PhantomSoundType Type { get; set; }
        public Vector2 Position { get; set; }
        public double Volume { get; set; }
        public double Pitch { get; set; }
        public double Time { get; set; }
    }

    public void TriggerParticleEffect(PhantomParticleType type, Vector2 position, Vector2 velocity, double intensity)
    {
        if (!_enableEffects) return;
        _pendingParticles.Enqueue(new PhantomParticleRequest
        {
            Type = type,
            Position = position,
            Velocity = velocity,
            Intensity = intensity,
            Time = 0.0
        });
    }

    public void TriggerSoundEffect(PhantomSoundType type, Vector2 position, double volume, double pitch)
    {
        if (!_enableEffects) return;
        _pendingSounds.Enqueue(new PhantomSoundRequest
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

    public override BodyType Type => BodyType.Phantom;
    public override string Name => "Phantom";
    public override string Description => "Phases through bodies & violently shakes them with energy-based abilities";
    public override string ColorHex => _activeProfile.ColorHex;
    public override double DefaultRadius => 18;
    public override double DefaultMass => 4;
    public override double DefaultRestitution => 0.5;

    #endregion

    #region Constructors & Initialization

    public PhantomBehavior() : this(PhantomType.Standard) { }

    public PhantomBehavior(PhantomType type)
    {
        _currentType = type;
        _activeProfile = _phantomProfiles[type];
        _phantomEnergy = _activeProfile.MaxEnergy;
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        body.Restitution = DefaultRestitution;
        body.Mass = DefaultMass;
        body.Radius = DefaultRadius;
        _lastPosition = body.Position;
        _stateMachine.CurrentState = PhantomState.Idle;
        _stateMachine.Energy = _activeProfile.MaxEnergy;
        LogDebug(body, $"PhantomBehavior initialized: Type={_currentType}, Energy={_phantomEnergy:F2}");
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
            _lifeTime += dt;
            UpdateEnergyRegen(dt);
            UpdateStateMachine(body, dt, world);
            ProcessPhasing(body, dt, world);
            ProcessTeleport(body, dt, world);
            ProcessInvisibility(body, dt);
            UpdateDecoy(body, dt, world);
            UpdateAchievements(body, dt);
            ProcessPendingEffects(body);
            TrackStatistics(body, dt);
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
        _currentState = _stateMachine.CurrentState;
        switch (_currentState)
        {
            case PhantomState.Idle:
                if (_stateMachine.Energy < _activeProfile.ShakeCost)
                    _stateMachine.TransitionTo(PhantomState.Draining, 0.0);
                break;
            case PhantomState.Phasing:
                if (_stateMachine.StateTimer > 0.5)
                    _stateMachine.TransitionTo(PhantomState.CoolingDown, 0.0);
                break;
            case PhantomState.Shaking:
                if (_stateMachine.StateTimer > 0.3)
                    _stateMachine.TransitionTo(PhantomState.CoolingDown, 0.0);
                break;
            case PhantomState.ChargingTeleport:
                if (_stateMachine.StateTimer >= TELEPORT_CHARGE_TIME)
                    ExecuteTeleport(body, world);
                break;
            case PhantomState.Teleporting:
                if (_stateMachine.StateTimer > 0.2)
                    _stateMachine.TransitionTo(PhantomState.CoolingDown, 0.0);
                break;
            case PhantomState.CoolingDown:
                if (_stateMachine.StateTimer > PHASE_COOLDOWN)
                    _stateMachine.TransitionTo(PhantomState.Idle, 0.0);
                break;
            case PhantomState.Invisible:
                if (_stateMachine.StateTimer >= _activeProfile.InvisibilityDuration)
                    DisableInvisibility(body);
                break;
            case PhantomState.Draining:
                if (_stateMachine.Energy >= _activeProfile.MaxEnergy * 0.5)
                    _stateMachine.TransitionTo(PhantomState.Idle, 0.0);
                if (_stateMachine.Energy <= 0)
                    _stateMachine.TransitionTo(PhantomState.Idle, 0.0);
                break;
        }
        _stateFrameCount++;
    }

    #endregion

    #region Phasing & Shake Physics

    private void ProcessPhasing(RigidBody body, double dt, PhysicsWorld world)
    {
        if (_currentState != PhantomState.Phasing && _currentState != PhantomState.Shaking)
            return;
        var bodies = world.Bodies;
        int targetsHit = 0;
        foreach (var other in bodies)
        {
            if (body == other || other.IsStatic)
                continue;
            if (targetsHit >= MAX_SHAKE_TARGETS)
                break;
            double dist = Vector2.Distance(body.Position, other.Position);
            double phaseDist = body.Radius + other.Radius + _activeProfile.PhaseDistance;
            if (dist < phaseDist)
            {
                ApplyShake(body, other, dt);
                targetsHit++;
                _phaseCount++;
            }
        }
        if (targetsHit > 0)
            _stateMachine.ShakeCount++;
    }

    private void ApplyShake(RigidBody body, RigidBody other, double dt)
    {
        if (_stateMachine.Energy < _activeProfile.ShakeCost)
            return;
        double shakeAngle = Random.Shared.NextDouble() * Math.PI * 2;
        double shakePhase = _lifeTime * _activeProfile.ShakeFrequency * 2 * Math.PI;
        double shakeFactor = Math.Abs(Math.Sin(shakePhase));
        double shakeStrength = Math.Clamp(_activeProfile.ShakeStrength * shakeFactor, MIN_SHAKE_STRENGTH, MAX_SHAKE_STRENGTH);
        Vector2 shakeDir = new Vector2(
            (float)Math.Cos(shakeAngle),
            (float)Math.Sin(shakeAngle));
        other.ApplyForce(shakeDir * shakeStrength);
        body.ApplyForce(-shakeDir * 200.0);
        _stateMachine.Energy -= _activeProfile.ShakeCost;
        _shakeCount++;
        if (shakeStrength > _peakShakeStrength)
            _peakShakeStrength = shakeStrength;
        RecordShake(body, other, shakeStrength);
        TriggerShakeEffects(body, shakeStrength);
    }

    private void RecordShake(RigidBody body, RigidBody other, double strength)
    {
        _shakeHistory.Add(new ShakeRecord
        {
            Position = body.Position,
            Strength = strength,
            Time = _lifeTime,
            TargetCount = 1
        });
        while (_shakeHistory.Count > 50)
            _shakeHistory.RemoveAt(0);
    }

    private void TriggerShakeEffects(RigidBody body, double intensity)
    {
        TriggerParticleEffect(PhantomParticleType.ShakeWave, body.Position, body.Velocity, intensity / 100.0);
        PhantomSoundType sound = intensity > 10000 ? PhantomSoundType.Shake : PhantomSoundType.Phasing;
        TriggerSoundEffect(sound, body.Position, Math.Min(1.0, intensity / 15000.0), 1.0 + intensity / 20000.0);
    }

    #endregion

    #region Teleport System

    private void ProcessTeleport(RigidBody body, double dt, PhysicsWorld world)
    {
        if (!_enableTeleport || !_activeProfile.CanTeleport)
            return;
        if (_currentState != PhantomState.ChargingTeleport)
            return;
    }

    public void StartTeleportCharge(RigidBody body, Vector2 target)
    {
        if (!_enableTeleport || !_activeProfile.CanTeleport)
            return;
        if (_stateMachine.Energy < TELEPORT_ENERGY_COST)
            return;
        _teleportTarget = target;
        _stateMachine.TransitionTo(PhantomState.ChargingTeleport, 0.0);
    }

    private void ExecuteTeleport(RigidBody body, PhysicsWorld world)
    {
        if (_stateMachine.Energy < TELEPORT_ENERGY_COST)
            return;
        double dist = Vector2.Distance(body.Position, _teleportTarget);
        if (dist > TELEPORT_RANGE)
            _teleportTarget = body.Position + (_teleportTarget - body.Position).Normalized * TELEPORT_RANGE;
        _teleportHistory.Add(body.Position);
        while (_teleportHistory.Count > MAX_TELEPORT_HISTORY)
            _teleportHistory.RemoveAt(0);
        body.Position = _teleportTarget;
        _stateMachine.Energy -= TELEPORT_ENERGY_COST;
        _teleportCount++;
        _stateMachine.TeleportCount++;
        _stateMachine.TransitionTo(PhantomState.Teleporting, 0.0);
        TriggerParticleEffect(PhantomParticleType.TeleportBurst, body.Position, Vector2.Zero, 1.0);
        TriggerSoundEffect(PhantomSoundType.Teleport, body.Position, 1.0, 1.5);
    }

    #endregion

    #region Invisibility System

    private void ProcessInvisibility(RigidBody body, double dt)
    {
        if (!_enableInvisibility || !_activeProfile.CanTurnInvisible)
            return;
        if (_isInvisible)
        {
            _invisibilityTimer += dt;
            if (_invisibilityTimer >= _activeProfile.InvisibilityDuration)
                DisableInvisibility(body);
        }
    }

    public void EnableInvisibility(RigidBody body)
    {
        if (!_enableInvisibility || !_activeProfile.CanTurnInvisible)
            return;
        if (_stateMachine.Energy < INVISIBILITY_ENERGY_COST)
            return;
        _isInvisible = true;
        _invisibilityTimer = 0.0;
        _stateMachine.Energy -= INVISIBILITY_ENERGY_COST;
        _stateMachine.TransitionTo(PhantomState.Invisible, 0.0);
        TriggerParticleEffect(PhantomParticleType.InvisibilityFade, body.Position, Vector2.Zero, 1.0);
        TriggerSoundEffect(PhantomSoundType.InvisibilityOn, body.Position, 0.5, 1.2);
    }

    private void DisableInvisibility(RigidBody body)
    {
        _isInvisible = false;
        _invisibilityTimer = 0.0;
        if (_stateMachine.CurrentState == PhantomState.Invisible)
            _stateMachine.TransitionTo(PhantomState.Idle, 0.0);
        TriggerSoundEffect(PhantomSoundType.InvisibilityOff, body.Position, 0.5, 0.8);
    }

    #endregion

    #region Energy System

    private void UpdateEnergyRegen(double dt)
    {
        double regen = _activeProfile.EnergyRegen * dt;
        _stateMachine.Energy = Math.Min(_activeProfile.MaxEnergy, _stateMachine.Energy + regen);
        _phantomEnergy = _stateMachine.Energy;
    }

    private void DrainEnergy(RigidBody body, RigidBody target, double amount)
    {
        if (target.IsStatic) return;
        double drain = Math.Min(amount, _activeProfile.MaxEnergy - _phantomEnergy);
        _phantomEnergy += drain;
        target.ApplyForce((body.Position - target.Position).Normalized * -drain * 10);
        TriggerParticleEffect(PhantomParticleType.EnergyDrain, target.Position, Vector2.Zero, drain / 100.0);
    }

    #endregion

    #region Collision Handling

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        if (_isInvisible)
            return;
        _stateMachine.LastPhaseTime = _lifeTime;
        _phaseCount++;
        LogDebug(body, $"Phantom collided with {other.Id}, phasing through");
        RaiseCollision(body, other);
    }

    #endregion

    #region Effect Processing

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

    #region Public API

    public void SetPhantomType(RigidBody body, PhantomType type)
    {
        _currentType = type;
        _activeProfile = _phantomProfiles[type];
        _phantomEnergy = _activeProfile.MaxEnergy;
        _stateMachine.Energy = _activeProfile.MaxEnergy;
        LogDebug(body, $"Phantom type changed to {type}");
    }

    public void SetCustomShakeStrength(double strength)
    {
        _customShakeStrength = Math.Max(0, strength);
        _currentType = PhantomType.Custom;
    }

    public void SetCustomShakeFrequency(double freq)
    {
        _activeProfile.ShakeFrequency = Math.Clamp(freq, MIN_SHAKE_FREQUENCY, MAX_SHAKE_FREQUENCY);
    }

    public void SetTeleportEnabled(bool enabled) => _enableTeleport = enabled;
    public void SetInvisibilityEnabled(bool enabled) => _enableInvisibility = enabled;
    public void SetEffectsEnabled(bool enabled) => _enableEffects = enabled;
    public void SetDecoyEnabled(bool enabled) => _decoySummoned = enabled;

    public PhantomType GetCurrentType() => _currentType;
    public int GetShakeCount() => _shakeCount;
    public int GetTeleportCount() => _teleportCount;
    public double GetCurrentEnergy() => _phantomEnergy;
    public PhantomState GetCurrentState() => _currentState;
    public IReadOnlyList<Vector2> GetTeleportHistory() => _teleportHistory.AsReadOnly();
    public double GetPhaseEfficiency() => _phaseCount > 0 ? (double)_shakeCount / _phaseCount : 0;

    public static List<PhantomProfile> GetAllPhantomProfiles() => new List<PhantomProfile>(_phantomProfiles.Values);
    public static PhantomProfile? GetProfileForType(PhantomType type) =>
        _phantomProfiles.TryGetValue(type, out var profile) ? profile : null;

    public void SummonDecoy(RigidBody body, PhysicsWorld world)
    {
        if (_phantomEnergy < DECOY_SUMMON_COST) return;
        _phantomEnergy -= DECOY_SUMMON_COST;
        _decoySummoned = true;
        _decoyLifetime = 0.0;
        LogDebug(body, "Summoned decoy (stub)");
    }

    #endregion

    #region Statistics Tracking

    private void TrackStatistics(RigidBody body, double dt)
    {
        double dist = Vector2.Distance(body.Position, _lastPosition);
        _totalDistanceTraveled += dist;
        _lastPosition = body.Position;
    }

    #endregion

    #region Achievement System

    private void UpdateAchievements(RigidBody body, double dt)
    {
        if (!_enableAchievements)
            return;
        if (_shakeCount >= ACHIEVEMENT_SHAKE_MASTER_COUNT && !_achievements.ContainsKey("ShakeMaster"))
        {
            _achievements["ShakeMaster"] = 1.0;
            TriggerSoundEffect(PhantomSoundType.AchievementUnlock, body.Position, 1.0, 1.5);
        }
        if (_teleportCount >= ACHIEVEMENT_TELEPORTER_COUNT && !_achievements.ContainsKey("Teleporter"))
        {
            _achievements["Teleporter"] = 1.0;
            TriggerSoundEffect(PhantomSoundType.AchievementUnlock, body.Position, 1.0, 1.8);
        }
        if (_phaseCount >= ACHIEVEMENT_PHASE_KING_COUNT && !_achievements.ContainsKey("PhaseKing"))
        {
            _achievements["PhaseKing"] = 1.0;
            TriggerSoundEffect(PhantomSoundType.AchievementUnlock, body.Position, 1.0, 2.0);
        }
    }

    #endregion

    #region Debug Visualization

    protected override void RenderDebugOverlay(RigidBody body, DrawingContext dc)
    {
        if (dc == null || !GlobalConfig.EnableDebugVisualization)
            return;
        DrawPhantomStateIndicator(body, dc);
        DrawTeleportHistory(body, dc);
        DrawEnergyBar(body, dc);
        DrawPhasingRange(body, dc);
    }

    private void DrawPhantomStateIndicator(RigidBody body, DrawingContext dc)
    {
        var stateColors = new Dictionary<PhantomState, Brush>
        {
            { PhantomState.Idle, Brushes.Gray },
            { PhantomState.Phasing, Brushes.Purple },
            { PhantomState.Shaking, Brushes.Red },
            { PhantomState.ChargingTeleport, Brushes.Cyan },
            { PhantomState.Teleporting, Brushes.Blue },
            { PhantomState.CoolingDown, Brushes.Orange },
            { PhantomState.Invisible, Brushes.Transparent },
            { PhantomState.Draining,Brushes.DarkRed }
        };
        if (stateColors.TryGetValue(_currentState, out var brush))
        {
            dc.DrawEllipse(brush, new Pen(Brushes.Black, 1), new Point(body.Position.X, body.Position.Y), 5, 5);
        }
    }

    private void DrawTeleportHistory(RigidBody body, DrawingContext dc)
    {
        for (int i = 0; i < _teleportHistory.Count; i++)
        {
            double opacity = (double)(i + 1) / _teleportHistory.Count;
            var brush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 128), 0, 255, 255));
            dc.DrawEllipse(brush, null, new Point(_teleportHistory[i].X, _teleportHistory[i].Y), 3, 3);
        }
    }

    private void DrawEnergyBar(RigidBody body, DrawingContext dc)
    {
        double energy = _phantomEnergy / _activeProfile.MaxEnergy;
        double barWidth = 20;
        double barHeight = 5;
        var brush = energy > 0.8 ? Brushes.Green : energy > 0.5 ? Brushes.Yellow : Brushes.Red;
        dc.DrawRectangle(brush, null, new Rect(body.Position.X - barWidth / 2, body.Position.Y - body.Radius - 10, barWidth * energy, barHeight));
    }

    private void DrawPhasingRange(RigidBody body, DrawingContext dc)
    {
        double range = body.Radius + _activeProfile.PhaseDistance;
        dc.DrawEllipse(null, new Pen(Brushes.Purple, 1), new Point(body.Position.X, body.Position.Y), range, range);
    }

    #endregion

    #region Decoy System

    private void UpdateDecoy(RigidBody body, double dt, PhysicsWorld world)
    {
        if (!_decoySummoned) return;
        _decoyLifetime += dt;
        if (_decoyLifetime >= DECOY_LIFETIME)
        {
            _decoySummoned = false;
            _decoyLifetime = 0.0;
            LogDebug(body, "Decoy expired");
        }
    }

    #endregion

    #region Serialization Support

    public string SerializeState()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"PhantomType:{_currentType}");
        sb.AppendLine($"ShakeCount:{_shakeCount}");
        sb.AppendLine($"TeleportCount:{_teleportCount}");
        sb.AppendLine($"PhaseCount:{_phaseCount}");
        sb.AppendLine($"Energy:{_phantomEnergy}");
        sb.AppendLine($"CurrentState:{_currentState}");
        sb.AppendLine($"EnableTeleport:{_enableTeleport}");
        sb.AppendLine($"EnableInvisibility:{_enableInvisibility}");
        sb.AppendLine($"EnableEffects:{_enableEffects}");
        sb.AppendLine($"TeleportHistoryCount:{_teleportHistory.Count}");
        sb.AppendLine($"DecoySummoned:{_decoySummoned}");
        sb.AppendLine($"DecoyLifetime:{_decoyLifetime}");
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
                    case "PhantomType":
                        if (Enum.TryParse(parts[1], out PhantomType type))
                            _currentType = type;
                        break;
                    case "ShakeCount":
                        _shakeCount = int.Parse(parts[1]);
                        break;
                    case "TeleportCount":
                        _teleportCount = int.Parse(parts[1]);
                        break;
                    case "PhaseCount":
                        _phaseCount = int.Parse(parts[1]);
                        break;
                    case "Energy":
                        _phantomEnergy = double.Parse(parts[1]);
                        break;
                    case "CurrentState":
                        if (Enum.TryParse(parts[1], out PhantomState parsedState))
                            _currentState = parsedState;
                        break;
                    case "EnableTeleport":
                        _enableTeleport = bool.Parse(parts[1]);
                        break;
                    case "EnableInvisibility":
                        _enableInvisibility = bool.Parse(parts[1]);
                        break;
                    case "EnableEffects":
                        _enableEffects = bool.Parse(parts[1]);
                        break;
                    case "DecoySummoned":
                        _decoySummoned = bool.Parse(parts[1]);
                        break;
                    case "DecoyLifetime":
                        _decoyLifetime = double.Parse(parts[1]);
                        break;
                }
            }
            catch { }
        }
    }

    #endregion

    #region Utility & Diagnostics

    public string GetDiagnosticsReport(RigidBody body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== PhantomBehavior Diagnostics ===");
        sb.AppendLine($"Type: {_currentType}");
        sb.AppendLine($"State: {_currentState} (Timer: {_stateTimer:F2}s)");
        sb.AppendLine($"Shake Count: {_shakeCount}");
        sb.AppendLine($"Teleport Count: {_teleportCount}");
        sb.AppendLine($"Phase Count: {_phaseCount}");
        sb.AppendLine($"Energy: {_phantomEnergy:F2}/{_activeProfile.MaxEnergy:F2}");
        sb.AppendLine($"Peak Shake Strength: {_peakShakeStrength:F2}");
        sb.AppendLine($"Total Distance: {_totalDistanceTraveled:F2}");
        sb.AppendLine($"Invisible: {_isInvisible}");
        sb.AppendLine($"Teleport History: {_teleportHistory.Count}");
        sb.AppendLine($"Achievements: {_achievements.Count}");
        sb.AppendLine($"Decoy Summoned: {_decoySummoned}");
        sb.AppendLine($"Decoy Lifetime: {_decoyLifetime:F2}");
        sb.AppendLine($"Phase Efficiency: {GetPhaseEfficiency():F2}");
        return sb.ToString();
    }

    #endregion
}
