using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Diagnostics;

namespace PhysicsSandbox.Behaviors;

public class LightningBehavior : BodyBehavior
{
    #region Constants & Tunable Parameters

    private const float DEFAULT_ARC_RADIUS = 250f;
    private const float DEFAULT_CHAIN_STRENGTH = 15000f;
    private const float DEFAULT_MAX_ENERGY = 1000f;
    private const float DEFAULT_RECHARGE_RATE = 50f;
    private const float DEFAULT_DISCHARGE_RATE = 200f;
    private const float MAX_CHAIN_HOPS = 5;
    private const float CHAIN_RESISTANCE_FALLOFF = 0.7f;
    private const float STUN_DURATION = 0.5f;
    private const float ARC_JITTER_AMPLITUDE = 8f;
    private const float ARC_BRANCH_PROBABILITY = 0.3f;
    private const float MAX_ARC_ANGLE = 45f;
    private const float OVERLOAD_THRESHOLD = 0.9f;
    private const float OVERLOAD_RECOVERY_RATE = 0.1f;
    private const float MAX_VELOCITY_FOR_HIT = 8f;
    private const float MIN_IMPACT_FORCE = 100f;

    #endregion

    #region Electric Material Profiles

    public enum ElectricMaterial
    {
        TeslaCoil,
        CopperArc,
        Plasma,
        StormCloud,
        StaticSpark,
        IndustrialArc,
        VanDeGraaff,
        NeonGlow,
        LightningBolt,
        EelShock,
        Custom
    }

    public class ElectricProfile
    {
        public string Name { get; set; } = "";
        public float ArcRadius { get; set; } = 250f;
        public float ChainStrength { get; set; } = 15000f;
        public float EnergyCapacity { get; set; } = 1000f;
        public float RechargeRate { get; set; } = 50f;
        public float DischargeRate { get; set; } = 200f;
        public float StunDuration { get; set; } = 0.5f;
        public float JitterAmplitude { get; set; } = 8f;
        public float BranchProbability { get; set; } = 0.3f;
        public ColorArc Color { get; set; } = ColorArc.BlueWhite;
        public bool CausesOverload { get; set; } = false;
        public float ThermalHeating { get; set; } = 0f;
        public string ColorHex { get; set; } = "#00BFFF";
        public string Description { get; set; } = "";
    }

    public enum ColorArc
    {
        BlueWhite,
        GoldYellow,
        PurpleIon,
        GreenPlasma,
        RedStark,
        WhiteHot,
        CyanAura,
        MagentaPulse
    }

    private static readonly Dictionary<ElectricMaterial, ElectricProfile> _electricProfiles = new()
    {
        {
            ElectricMaterial.TeslaCoil, new ElectricProfile
            {
                Name = "Tesla Coil",
                ArcRadius = 350f,
                ChainStrength = 18000f,
                EnergyCapacity = 1200f,
                RechargeRate = 60f,
                DischargeRate = 250f,
                StunDuration = 0.6f,
                JitterAmplitude = 12f,
                BranchProbability = 0.4f,
                Color = ColorArc.BlueWhite,
                CausesOverload = true,
                ThermalHeating = 0.5f,
                ColorHex = "#00BFFF",
                Description = "High-voltage arcs with many branches"
            }
        },
        {
            ElectricMaterial.CopperArc, new ElectricProfile
            {
                Name = "Copper Arc",
                ArcRadius = 200f,
                ChainStrength = 12000f,
                EnergyCapacity = 800f,
                RechargeRate = 80f,
                DischargeRate = 180f,
                StunDuration = 0.3f,
                JitterAmplitude = 4f,
                BranchProbability = 0.2f,
                Color = ColorArc.GoldYellow,
                CausesOverload = false,
                ThermalHeating = 0.2f,
                ColorHex = "#FFD700",
                Description = "Stable welding-style arc"
            }
        },
        {
            ElectricMaterial.Plasma, new ElectricProfile
            {
                Name = "Plasma",
                ArcRadius = 280f,
                ChainStrength = 16000f,
                EnergyCapacity = 1000f,
                RechargeRate = 40f,
                DischargeRate = 220f,
                StunDuration = 0.8f,
                JitterAmplitude = 15f,
                BranchProbability = 0.5f,
                Color = ColorArc.GreenPlasma,
                CausesOverload = true,
                ThermalHeating = 1.2f,
                ColorHex = "#00FF00",
                Description = "Superheated ionized gas"
            }
        },
        {
            ElectricMaterial.StormCloud, new ElectricProfile
            {
                Name = "Storm Cloud",
                ArcRadius = 400f,
                ChainStrength = 20000f,
                EnergyCapacity = 1500f,
                RechargeRate = 30f,
                DischargeRate = 280f,
                StunDuration = 1.0f,
                JitterAmplitude = 20f,
                BranchProbability = 0.35f,
                Color = ColorArc.PurpleIon,
                CausesOverload = true,
                ThermalHeating = 0.3f,
                ColorHex = "#9370DB",
                Description = "Slow-charging but devastating"
            }
        },
        {
            ElectricMaterial.StaticSpark, new ElectricProfile
            {
                Name = "Static Spark",
                ArcRadius = 80f,
                ChainStrength = 4000f,
                EnergyCapacity = 200f,
                RechargeRate = 120f,
                DischargeRate = 100f,
                StunDuration = 0.1f,
                JitterAmplitude = 2f,
                BranchProbability = 0.1f,
                Color = ColorArc.CyanAura,
                CausesOverload = false,
                ThermalHeating = 0f,
                ColorHex = "#00FFFF",
                Description = "Quick tiny discharges"
            }
        },
        {
            ElectricMaterial.IndustrialArc, new ElectricProfile
            {
                Name = "Industrial Arc",
                ArcRadius = 300f,
                ChainStrength = 17000f,
                EnergyCapacity = 1100f,
                RechargeRate = 45f,
                DischargeRate = 240f,
                StunDuration = 0.7f,
                JitterAmplitude = 10f,
                BranchProbability = 0.25f,
                Color = ColorArc.RedStark,
                CausesOverload = false,
                ThermalHeating = 0.8f,
                ColorHex = "#FF4500",
                Description = "Heavy-duty arc welding"
            }
        },
        {
            ElectricMaterial.VanDeGraaff, new ElectricProfile
            {
                Name = "Van de Graaff",
                ArcRadius = 150f,
                ChainStrength = 8000f,
                EnergyCapacity = 500f,
                RechargeRate = 100f,
                DischargeRate = 150f,
                StunDuration = 0.25f,
                JitterAmplitude = 6f,
                BranchProbability = 0.15f,
                Color = ColorArc.MagentaPulse,
                CausesOverload = false,
                ThermalHeating = 0.1f,
                ColorHex = "#FF00FF",
                Description = "Smooth hair-raising arcs"
            }
        },
        {
            ElectricMaterial.NeonGlow, new ElectricProfile
            {
                Name = "Neon Glow",
                ArcRadius = 120f,
                ChainStrength = 6000f,
                EnergyCapacity = 400f,
                RechargeRate = 90f,
                DischargeRate = 120f,
                StunDuration = 0.2f,
                JitterAmplitude = 3f,
                BranchProbability = 0.08f,
                Color = ColorArc.CyanAura,
                CausesOverload = false,
                ThermalHeating = 0f,
                ColorHex = "#40E0D0",
                Description = "Soft glowing discharge"
            }
        },
        {
            ElectricMaterial.LightningBolt, new ElectricProfile
            {
                Name = "Lightning Bolt",
                ArcRadius = 500f,
                ChainStrength = 25000f,
                EnergyCapacity = 2000f,
                RechargeRate = 25f,
                DischargeRate = 300f,
                StunDuration = 1.5f,
                JitterAmplitude = 25f,
                BranchProbability = 0.6f,
                Color = ColorArc.WhiteHot,
                CausesOverload = true,
                ThermalHeating = 2.0f,
                ColorHex = "#FFFFFF",
                Description = "Natural lightning simulation"
            }
        },
        {
            ElectricMaterial.EelShock, new ElectricProfile
            {
                Name = "Electric Eel",
                ArcRadius = 100f,
                ChainStrength = 9000f,
                EnergyCapacity = 450f,
                RechargeRate = 70f,
                DischargeRate = 160f,
                StunDuration = 0.4f,
                JitterAmplitude = 5f,
                BranchProbability = 0.2f,
                Color = ColorArc.GreenPlasma,
                CausesOverload = false,
                ThermalHeating = 0.3f,
                ColorHex = "#ADFF2F",
                Description = "Pulsing aquatic shock"
            }
        },
        {
            ElectricMaterial.Custom, new ElectricProfile
            {
                Name = "Custom",
                ArcRadius = DEFAULT_ARC_RADIUS,
                ChainStrength = DEFAULT_CHAIN_STRENGTH,
                EnergyCapacity = DEFAULT_MAX_ENERGY,
                RechargeRate = DEFAULT_RECHARGE_RATE,
                DischargeRate = DEFAULT_DISCHARGE_RATE,
                StunDuration = STUN_DURATION,
                JitterAmplitude = ARC_JITTER_AMPLITUDE,
                BranchProbability = ARC_BRANCH_PROBABILITY,
                Color = ColorArc.BlueWhite,
                CausesOverload = false,
                ThermalHeating = 0f,
                ColorHex = "#FFD600",
                Description = "User-defined electric behavior"
            }
        }
    };

    #endregion

    #region Instance State & Configuration

    private ElectricMaterial _currentMaterial = ElectricMaterial.Custom;
    private ElectricProfile _activeProfile = _electricProfiles[ElectricMaterial.Custom];

    private float _currentEnergy = 0f;
    private float _energyRechargeAccumulator = 0f;
    private float _dischargeAccumulator = 0f;
    private bool _isOverloaded = false;
    private float _overloadTimer = 0f;

    private ZapState _currentState = ZapState.Idle;
    private float _stateTimer = 0f;
    private int _consecutiveDischarges = 0;
    private readonly List<ZapRecord> _zapHistory = new();

    private readonly Queue<PendingZap> _pendingZaps = new();
    private readonly Queue<PendingArc> _pendingArcs = new();

    private int _totalZaps = 0;
    private int _totalChainHops = 0;
    private float _totalZapEnergy = 0f;
    private int _criticalZaps = 0;
    private float _peakDischargePower = 0f;

    private float _lastZapTime = 0f;
    private float _timeSinceLastZap = 0f;

    private readonly Stopwatch _updateStopwatch = new();
    private Vector2 _lastPosition = Vector2.Zero;
    private Random _rand = new Random();
    private float _totalDistanceTraveled = 0f;

    private float? _arcRadiusOverride = null;
    private float? _chainStrengthOverride = null;
    private float? _stunDurationOverride = null;
    private bool _disableChaining = false;
    private bool _stunOnlyMode = false;

    private float _chargeVisualPulse = 0f;

    private class ZapRecord
    {
        public Vector2 TargetPosition { get; set; } = Vector2.Zero;
        public float ImpactForce { get; set; }
        public float Time { get; set; }
        public int TargetId { get; set; }
        public int ChainIndex { get; set; }
        public bool WasCritical { get; set; }
        public ZapState CurrentState { get; set; }
    }

    private class PendingZap
    {
        public RigidBody Target { get; set; } = null!;
        public float Force { get; set; }
        public Vector2 Direction { get; set; }
        public float ChainFactor { get; set; }
    }

    private class PendingArc
    {
        public Vector2 Start { get; set; }
        public Vector2 End { get; set; }
        public float Intensity { get; set; }
        public float Width { get; set; }
        public List<Vector2> BranchPoints { get; set; } = new();
    }

    public enum ZapState
    {
        Idle,
        Charging,
        Ready,
        Discharging,
        Arcing,
        Overloaded,
        Recharging,
        Stunned
    }

    #endregion

    #region Behavior Properties (Overrides)

    public override BodyType Type => BodyType.Lightning;
    public override string Name => "Lightning";
    public override string Description => "Electric discharge behavior with arcing, chaining, stun effects, and energy management";
    public override string ColorHex => _activeProfile.ColorHex;
    public override double DefaultRadius => 14;
    public override double DefaultMass => 4;
    public override double DefaultRestitution => 0.7;

    #endregion

    #region Constructors & Initialization

    public LightningBehavior() : this(ElectricMaterial.Custom) { }

    public LightningBehavior(ElectricMaterial material)
    {
        _currentMaterial = material;
        _activeProfile = _electricProfiles[material];
        _currentEnergy = _activeProfile.EnergyCapacity * 0.5f;
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);

        _currentEnergy = _activeProfile.EnergyCapacity * 0.5f;
        _lastPosition = body.Position;

        LogDebug(body, $"LightningBehavior initialized: Material={_currentMaterial}, Energy={_currentEnergy:F1}/{_activeProfile.EnergyCapacity:F1}");
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

            UpdateEnergy(dt);
            UpdateStateMachine(body, dt);
            ProcessDischarges(body, world);
            UpdatePendingEffects(body);
            TrackStatistics(body, dt);
            UpdateChargeVisuals(dt);

            if (GlobalConfig.EnableDebugVisualization && Config.DebugMode)
            {
                RenderDebugOverlay(body, null);
            }

            RaisePostUpdate(body, dt);
        }
        finally
        {
            _updateStopwatch.Stop();
            RecordPerformanceMetric("OnUpdate", _updateStopwatch.Elapsed.TotalMilliseconds);
        }
    }

    #endregion

    #region Energy Management

    private void UpdateEnergy(double dt)
    {
        float adjustedRecharge = _activeProfile.RechargeRate * (float)dt;
        _energyRechargeAccumulator += adjustedRecharge;
        if (_energyRechargeAccumulator >= 1f)
        {
            float added = MathF.Floor(_energyRechargeAccumulator);
            _currentEnergy = MathF.Min(_currentEnergy + added, _activeProfile.EnergyCapacity);
            _energyRechargeAccumulator -= added;
        }

        _dischargeAccumulator += _activeProfile.DischargeRate * (float)dt;
        _timeSinceLastZap += (float)dt;

        if (_isOverloaded)
        {
            _overloadTimer += (float)dt;
            if (_overloadTimer >= 1f / OVERLOAD_RECOVERY_RATE)
            {
                _isOverloaded = false;
                _overloadTimer = 0f;
                _currentEnergy = _activeProfile.EnergyCapacity * 0.1f;
            }
        }
    }

    private bool CanDischarge(float requiredEnergy)
    {
        if (_isOverloaded) return false;
        return _currentEnergy >= requiredEnergy;
    }

    private void ConsumeEnergy(float amount)
    {
        _currentEnergy = MathF.Max(0f, _currentEnergy - amount);
        _dischargeAccumulator = 0f;
        _timeSinceLastZap = 0f;
        _lastZapTime = 0f;
    }

    private float GetDischargePower()
    {
        return MathF.Min(1f, _dischargeAccumulator) * _activeProfile.DischargeRate;
    }

    #endregion

    #region State Machine

    private void UpdateStateMachine(RigidBody body, double dt)
    {
        _stateTimer += (float)dt;

        switch (_currentState)
        {
            case ZapState.Idle:
                if (_currentEnergy >= _activeProfile.EnergyCapacity * 0.8f)
                {
                    TransitionTo(ZapState.Ready, body);
                }
                else if (_energyRechargeAccumulator > 0.5f)
                {
                    TransitionTo(ZapState.Charging, body);
                }
                break;

            case ZapState.Charging:
                _chargeVisualPulse = 0.5f + 0.5f * MathF.Sin(_stateTimer * 10f);
                if (_currentEnergy >= _activeProfile.EnergyCapacity * 0.8f)
                {
                    TransitionTo(ZapState.Ready, body);
                }
                break;

            case ZapState.Ready:
                _chargeVisualPulse = 1f;
                if (_dischargeAccumulator >= 1f)
                {
                    TransitionTo(ZapState.Discharging, body);
                }
                break;

            case ZapState.Discharging:
                if (_dischargeAccumulator < 0.3f)
                {
                    if (_consecutiveDischarges >= 3)
                    {
                        TransitionTo(ZapState.Overloaded, body);
                    }
                    else
                    {
                        TransitionTo(ZapState.Arcing, body);
                    }
                }
                break;

            case ZapState.Arcing:
                if (_pendingZaps.Count == 0 && _pendingArcs.Count == 0)
                {
                    _consecutiveDischarges++;
                    TransitionTo(ZapState.Recharging, body);
                }
                break;

            case ZapState.Recharging:
                if (_currentEnergy >= _activeProfile.EnergyCapacity * 0.5f)
                {
                    TransitionTo(ZapState.Charging, body);
                }
                break;

            case ZapState.Overloaded:
                if (!_isOverloaded)
                {
                    _consecutiveDischarges = 0;
                    TransitionTo(ZapState.Recharging, body);
                }
                break;

            case ZapState.Stunned:
                if (_stateTimer > _activeProfile.StunDuration)
                {
                    _currentState = ZapState.Idle;
                    _stateTimer = 0f;
                }
                break;
        }
    }

    private void TransitionTo(ZapState newState, RigidBody body)
    {
        ZapState prev = _currentState;
        _currentState = newState;
        _stateTimer = 0f;

        if (newState == ZapState.Discharging)
        {
            InitiateDischarge(body);
        }
        else if (newState == ZapState.Stunned)
        {
            body.Velocity *= 0.1f;
            body.AngularVelocity *= 0.1f;
        }

        LogDebug(body, $"State transition: {prev} -> {newState}");
    }

    #endregion

    #region Discharge Logic

    private void InitiateDischarge(RigidBody body)
    {
        if (!CanDischarge(GetDischargePower()))
        {
            TransitionTo(ZapState.Recharging, body);
            return;
        }

        float dischargePower = GetDischargePower();
        ConsumeEnergy(dischargePower);

        float baseStrength = _activeProfile.ChainStrength * dischargePower;
        if (baseStrength > _peakDischargePower) _peakDischargePower = baseStrength;

        var nearbyBodies = FindDischargeTargets(body);
        if (nearbyBodies.Count == 0)
        {
            TransitionTo(ZapState.Recharging, body);
            return;
        }

        RigidBody primaryTarget = SelectPrimaryTarget(body, nearbyBodies);
        Vector2 direction = (primaryTarget.Position - body.Position).Normalized;

        float hitFactor = CalculateHitFactor(body, primaryTarget);
        float actualForce = baseStrength * hitFactor;

        var zap = new PendingZap
        {
            Target = primaryTarget,
            Force = actualForce,
            Direction = direction,
            ChainFactor = 1f
        };

        _pendingZaps.Enqueue(zap);
        CreateArcVisual(body.Position, primaryTarget.Position, 1f, body, primaryTarget);

        _totalZaps++;
        _totalZapEnergy += actualForce;
        _lastZapTime = 0f;
        _consecutiveDischarges++;

        if (hitFactor > 0.9f) _criticalZaps++;
    }

    private List<RigidBody> FindDischargeTargets(RigidBody body)
    {
        var result = new List<RigidBody>();
        float radius = _arcRadiusOverride ?? _activeProfile.ArcRadius;

        foreach (var other in SpatialQuery(body.Position, radius, body.World))
        {
            if (other == body || other.IsStatic) continue;
            if (other.IsFrozen) continue;

            float dist = (float)(body.Position - other.Position).Length;
            if (dist <= radius)
            {
                result.Add(other);
            }
        }

        return result;
    }

    private RigidBody SelectPrimaryTarget(RigidBody body, List<RigidBody> candidates)
    {
        float bestScore = float.MinValue;
        RigidBody best = candidates[0];

        foreach (var candidate in candidates)
        {
            float dist = (float)(candidate.Position - body.Position).Length;
            float speedScore = (float)candidate.Velocity.Length * 0.5f;
            float massScore = (float)candidate.Mass * 0.3f;
            float proximityBonus = MathF.Max(0f, (_activeProfile.ArcRadius - dist) * 2f);

            float total = speedScore + massScore + proximityBonus;
            if (total > bestScore)
            {
                bestScore = total;
                best = candidate;
            }
        }

        return best;
    }

    private float CalculateHitFactor(RigidBody body, RigidBody target)
    {
        float velocityFactor = MathF.Min(1f, (float)body.Velocity.Length / MAX_VELOCITY_FOR_HIT);
        float angleFactor = MathF.Abs((float)Vector2.Dot(body.Velocity.Normalized, (target.Position - body.Position).Normalized));
        float distanceFactor = 1f - MathF.Min(1f, (float)(body.Position - target.Position).Length / (_arcRadiusOverride ?? _activeProfile.ArcRadius));
        return 0.3f + 0.7f * (velocityFactor * 0.4f + angleFactor * 0.3f + distanceFactor * 0.3f);
    }

    private void ProcessDischarges(RigidBody body, PhysicsWorld world)
    {
        while (_pendingZaps.Count > 0)
        {
            var zap = _pendingZaps.Dequeue();
            ApplyZapForce(body, zap.Target, zap.Force, zap.Direction, zap.ChainFactor);
            RecordZap(body, zap.Target, zap.Force, 0, false);
        }

        while (_pendingArcs.Count > 0)
        {
            var arc = _pendingArcs.Dequeue();
            RegisterArcForRender(arc);
        }
    }

    private void ApplyZapForce(RigidBody source, RigidBody target, float force, Vector2 direction, float chainFactor)
    {
        if (target.IsStatic) return;

        Vector2 impulse = direction * force * chainFactor;
        target.ApplyImpulse(impulse);
        target.ApplyForce(impulse * 0.1f);

        float stunMult = _stunDurationOverride.HasValue ? _stunDurationOverride.Value : _activeProfile.StunDuration;
        ApplyStun(target, stunMult);

        if (_activeProfile.ThermalHeating > 0f)
        {
            ApplyThermalEffect(target, _activeProfile.ThermalHeating * force * 0.0001f);
        }

        if (_disableChaining || chainFactor >= 0.95f) return;

        PropagateChainLightning(source, target, force, chainFactor);
    }

    private void ApplyStun(RigidBody target, float duration)
    {
        if (_stunOnlyMode)
        {
            target.Velocity *= 0.05f;
            target.AngularVelocity *= 0.05f;
        }
        else
        {
            target.Velocity *= 0.3f;
            target.AngularVelocity *= 0.2f;
        }

        if (target.Behavior is NormalBehavior normal)
        {
            normal.SetAffectedByGravity(false);
        }
    }

    private void ApplyThermalEffect(RigidBody target, float heatAmount)
    {
    }

    private void PropagateChainLightning(RigidBody previousSource, RigidBody currentTarget, float remainingForce, float currentChainFactor)
    {
        if (MAX_CHAIN_HOPS <= 0) return;

        float newChainFactor = currentChainFactor * CHAIN_RESISTANCE_FALLOFF;
        float nextForce = remainingForce * newChainFactor;

        var candidates = FindDischargeTargets(currentTarget);
        candidates.RemoveAll(b => b == previousSource);

        if (candidates.Count == 0) return;

        RigidBody nextTarget = SelectPrimaryTarget(currentTarget, candidates);
        Vector2 dir = (nextTarget.Position - currentTarget.Position).Normalized;

        var zap = new PendingZap
        {
            Target = nextTarget,
            Force = nextForce,
            Direction = dir,
            ChainFactor = newChainFactor
        };

        _pendingZaps.Enqueue(zap);
        CreateArcVisual(currentTarget.Position, nextTarget.Position, newChainFactor, currentTarget, nextTarget);

        _totalChainHops++;
        RecordZap(previousSource, nextTarget, nextForce, _pendingZaps.Count, false);
    }

    private void CreateArcVisual(Vector2 start, Vector2 end, float intensity, RigidBody? source, RigidBody? target)
    {
        var arc = new PendingArc
        {
            Start = start,
            End = end,
            Intensity = intensity,
            Width = 2f + intensity * 4f
        };

        float arcLength = (float)(end - start).Length;
        int segmentCount = Math.Max(3, (int)MathF.Ceiling(arcLength / 15f));
        float jitter = (_activeProfile.JitterAmplitude * intensity);

        Vector2 prev = start;
        for (int i = 1; i < segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            Vector2 basePoint = start + (end - start) * t;

            float angle = (_stateTimer * 30f + i * 1.7f) % (MathF.PI * 2);
            Vector2 offset = new Vector2(
                MathF.Sin(angle) * jitter,
                MathF.Cos(angle * 0.7f) * jitter
            );

            if (i < segmentCount - 1 && (float)_rand.NextDouble() < _activeProfile.BranchProbability)
            {
                Vector2 branchDir = (end - start).Normalized.Perpendicular();
                Vector2 branchEnd = basePoint + branchDir * (15f + (float)_rand.NextDouble() * 20f);
                arc.BranchPoints.Add(branchEnd);
            }

            prev = basePoint + offset;
        }

        _pendingArcs.Enqueue(arc);
    }

    #endregion

    #region State Machine Helpers

    private void UpdatePendingEffects(RigidBody body)
    {
    }

    private void RegisterArcForRender(PendingArc arc)
    {
    }

    #endregion

    #region Statistics Tracking

    private void TrackStatistics(RigidBody body, double dt)
    {
        float distMoved = (float)(body.Position - _lastPosition).Length;
        _totalDistanceTraveled += distMoved;
        _lastPosition = body.Position;
    }

    private void RecordZap(RigidBody source, RigidBody target, float force, int chainIdx, bool wasCritical)
    {
        var rec = new ZapRecord
        {
            TargetPosition = target.Position,
            ImpactForce = force,
            Time = 0f,
            TargetId = target.Id,
            ChainIndex = chainIdx,
            WasCritical = wasCritical,
            CurrentState = _currentState
        };
        _zapHistory.Add(rec);
        while (_zapHistory.Count > 50) _zapHistory.RemoveAt(0);
    }

    public (int TotalZaps, int ChainHops, float TotalEnergy, int CriticalZaps, float PeakPower, float EnergyPct) GetStats()
    {
        return (_totalZaps, _totalChainHops, _totalZapEnergy, _criticalZaps, _peakDischargePower, _currentEnergy / _activeProfile.EnergyCapacity);
    }

    public float GetEnergyRatio() => _currentEnergy / _activeProfile.EnergyCapacity;
    public ZapState GetCurrentZapState() => _currentState;
    public int GetConsecutiveDischarges() => _consecutiveDischarges;

    #endregion

    #region Public API for Runtime Modification

    public void SetMaterial(RigidBody body, ElectricMaterial material)
    {
        _currentMaterial = material;
        _activeProfile = _electricProfiles[material];
        LogDebug(body, $"Material switched to: {material}");
    }

    public void SetArcRadius(float? radius) => _arcRadiusOverride = radius;
    public void SetChainStrength(float? strength) => _chainStrengthOverride = strength;
    public void SetStunDuration(float? duration) => _stunDurationOverride = duration;
    public void DisableChaining(bool disable) => _disableChaining = disable;
    public void SetStunOnlyMode(bool stunOnly) => _stunOnlyMode = stunOnly;

    public ElectricMaterial GetCurrentMaterial() => _currentMaterial;
    public float GetCurrentEnergy() => _currentEnergy;
    public float GetEnergyCapacity() => _activeProfile.EnergyCapacity;
    public bool IsOverloaded() => _isOverloaded;

    public void ForceDischarge(RigidBody body, RigidBody target, float customForce)
    {
        var dir = (target.Position - body.Position).Normalized;
        _pendingZaps.Enqueue(new PendingZap { Target = target, Force = customForce, Direction = dir, ChainFactor = 1f });
        TransitionTo(ZapState.Discharging, body);
    }

    #endregion

    #region Debug Visualization

    protected override void RenderDebugOverlay(RigidBody body, DrawingContext dc)
    {
        if (dc == null) return;

        DrawArcRadius(body, dc);
        DrawEnergyBar(body, dc);
        DrawStateIndicator(body, dc);
        DrawRecentZaps(body, dc);
        DrawChargePulse(body, dc);
    }

    private static void DrawArcRadius(RigidBody body, DrawingContext dc)
    {
        float radius = 250f;
        dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.DarkBlue, 1), new Point(body.Position.X, body.Position.Y), radius, radius);
    }

    private void DrawEnergyBar(RigidBody body, DrawingContext dc)
    {
        float ratio = _currentEnergy / _activeProfile.EnergyCapacity;
        double barW = 30, barH = 6;
        double x = body.Position.X - barW / 2;
        double y = body.Position.Y - body.Radius - 12;

        dc.DrawRectangle(Brushes.Black, null, new Rect(x, y, barW, barH));
        dc.DrawRectangle(GetEnergyColor(ratio), null, new Rect(x, y, barW * ratio, barH));
    }

    private Brush GetEnergyColor(float ratio)
    {
        return ratio switch
        {
            < 0.3f => Brushes.Red,
            < 0.6f => Brushes.Yellow,
            < 0.9f => Brushes.LimeGreen,
            _ => Brushes.Cyan
        };
    }

    private void DrawStateIndicator(RigidBody body, DrawingContext dc)
    {
        Brush stateBrush = _currentState switch
        {
            ZapState.Idle => Brushes.Gray,
            ZapState.Charging => Brushes.Yellow,
            ZapState.Ready => Brushes.Lime,
            ZapState.Discharging => Brushes.OrangeRed,
            ZapState.Arcing => Brushes.Cyan,
            ZapState.Overloaded => Brushes.DarkRed,
            ZapState.Recharging => Brushes.MediumPurple,
            ZapState.Stunned => Brushes.Blue,
            _ => Brushes.Black
        };

        dc.DrawEllipse(stateBrush, new Pen(Brushes.Black, 1), new Point(body.Position.X, body.Position.Y), 6, 6);
    }

    private void DrawRecentZaps(RigidBody body, DrawingContext dc)
    {
        foreach (var zap in _zapHistory)
        {
            if (zap.Time > 1f) continue;
            double opacity = 1.0 - zap.Time;
            var brush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 200), 255, 215, 0));
            dc.DrawEllipse(brush, null, new Point(zap.TargetPosition.X, zap.TargetPosition.Y), 2, 2);
        }
    }

    private void DrawChargePulse(RigidBody body, DrawingContext dc)
    {
        if (_currentState != ZapState.Charging && _currentState != ZapState.Ready) return;

        float pulseRadius = (float)body.Radius + 3f + 2f * _chargeVisualPulse;
        var glow = new SolidColorBrush(Color.FromArgb((byte)(_chargeVisualPulse * 100), 255, 255, 0));
        dc.DrawEllipse(glow, null, new Point(body.Position.X, body.Position.Y), pulseRadius, pulseRadius);
    }

    #endregion

    #region Serialization Support

    public string SerializeState()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Material:{_currentMaterial}");
        sb.AppendLine($"CurrentEnergy:{_currentEnergy}");
        sb.AppendLine($"EnergyCapacity:{_activeProfile.EnergyCapacity}");
        sb.AppendLine($"State:{_currentState}");
        sb.AppendLine($"TotalZaps:{_totalZaps}");
        sb.AppendLine($"TotalChainHops:{_totalChainHops}");
        sb.AppendLine($"TotalZapEnergy:{_totalZapEnergy}");
        sb.AppendLine($"CriticalZaps:{_criticalZaps}");
        sb.AppendLine($"PeakDischargePower:{_peakDischargePower}");
        sb.AppendLine($"ConsecutiveDischarges:{_consecutiveDischarges}");
        sb.AppendLine($"Overloaded:{_isOverloaded}");
        sb.AppendLine($"ArcRadiusOverride:{_arcRadiusOverride}");
        sb.AppendLine($"ChainStrengthOverride:{_chainStrengthOverride}");
        sb.AppendLine($"StunDurationOverride:{_stunDurationOverride}");
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
                        if (Enum.TryParse(parts[1], out ElectricMaterial mat))
                            _currentMaterial = mat;
                        break;
                    case "CurrentEnergy":
                        _currentEnergy = float.Parse(parts[1]);
                        break;
                    case "State":
                        if (Enum.TryParse(parts[1], out ZapState st))
                            _currentState = st;
                        break;
                    case "TotalZaps":
                        _totalZaps = int.Parse(parts[1]);
                        break;
                    case "TotalChainHops":
                        _totalChainHops = int.Parse(parts[1]);
                        break;
                    case "TotalZapEnergy":
                        _totalZapEnergy = float.Parse(parts[1]);
                        break;
                    case "CriticalZaps":
                        _criticalZaps = int.Parse(parts[1]);
                        break;
                    case "PeakDischargePower":
                        _peakDischargePower = float.Parse(parts[1]);
                        break;
                    case "ConsecutiveDischarges":
                        _consecutiveDischarges = int.Parse(parts[1]);
                        break;
                    case "Overloaded":
                        _isOverloaded = bool.Parse(parts[1]);
                        break;
                    case "ArcRadiusOverride":
                        _arcRadiusOverride = parts[1] == "" ? null : float.Parse(parts[1]);
                        break;
                    case "ChainStrengthOverride":
                        _chainStrengthOverride = parts[1] == "" ? null : float.Parse(parts[1]);
                        break;
                    case "StunDurationOverride":
                        _stunDurationOverride = parts[1] == "" ? null : float.Parse(parts[1]);
                        break;
                }
            }
            catch { }
        }
    }

    #endregion

    #region Utility & Helper Methods

    private void UpdateChargeVisuals(double dt)
    {
        if (_currentState == ZapState.Charging || _currentState == ZapState.Ready)
        {
            _chargeVisualPulse = 0.5f + 0.5f * MathF.Sin((float)(_stateTimer * 8f));
        }
        else
        {
            _chargeVisualPulse = MathF.Max(0f, _chargeVisualPulse - (float)dt * 2f);
        }
    }

    public string GetDiagnosticsReport(RigidBody body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== LightningBehavior Diagnostics ===");
        sb.AppendLine($"Material: {_currentMaterial}");
        sb.AppendLine($"State: {_currentState} (Timer: {_stateTimer:F2}s)");
        sb.AppendLine($"Energy: {_currentEnergy:F1} / {_activeProfile.EnergyCapacity:F1}");
        sb.AppendLine($"Total Zaps: {_totalZaps}");
        sb.AppendLine($"Chain Hops: {_totalChainHops}");
        sb.AppendLine($"Total Energy: {_totalZapEnergy:F1}");
        sb.AppendLine($"Critical Zaps: {_criticalZaps}");
        sb.AppendLine($"Peak Discharge: {_peakDischargePower:F1}");
        sb.AppendLine($"Consecutive Discharges: {_consecutiveDischarges}");
        sb.AppendLine($"Overloaded: {_isOverloaded}");
        sb.AppendLine($"Arc Radius: {_arcRadiusOverride ?? _activeProfile.ArcRadius}");
        sb.AppendLine($"Chain Strength: {_chainStrengthOverride ?? _activeProfile.ChainStrength}");
        sb.AppendLine($"Stun Duration: {_stunDurationOverride ?? _activeProfile.StunDuration}");
        sb.AppendLine($"Position: {body.Position}");
        sb.AppendLine($"Velocity: {body.Velocity}");
        return sb.ToString();
    }

    public static List<ElectricProfile> GetAllProfiles()
    {
        return new List<ElectricProfile>(_electricProfiles.Values);
    }

    public ElectricProfile GetActiveProfile() => _activeProfile;

    #endregion
}
