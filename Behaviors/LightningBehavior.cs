using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PhysicsSandbox.Behaviors;

public class LightningBehavior : BodyBehavior
{
    private const float ARC_RADIUS = 250f;
    private const float CHAIN_STRENGTH = 15000f;
    private const float MAX_ENERGY = 1000f;
    private const float RECHARGE_RATE = 50f;
    private const float DISCHARGE_RATE = 200f;
    private const float MAX_CHAIN_HOPS = 5f;
    private const float CHAIN_FALLOFF = 0.7f;
    private const float STUN_DURATION = 0.5f;

    public enum ZapState { Idle, Charging, Ready, Discharging, Arcing, Overloaded, Recharging, Stunned }
    public enum ElectricType { TeslaCoil, CopperArc, Plasma, StormCloud, StaticSpark, IndustrialArc, VanDeGraaff, NeonGlow, LightningBolt }

    public class ElectricProfile
    {
        public string Name = "";
        public float ArcRadius = 250f;
        public float ChainStrength = 15000f;
        public float EnergyCapacity = 1000f;
        public float RechargeRate = 50f;
        public float DischargeRate = 200f;
        public float StunDuration = 0.5f;
        public string ColorHex = "#00BFFF";
    }

    private static readonly Dictionary<ElectricType, ElectricProfile> _profiles = new()
    {
        { ElectricType.TeslaCoil, new() { Name = "Tesla Coil", ArcRadius = 350f, ChainStrength = 18000f, EnergyCapacity = 1200f, RechargeRate = 60f, DischargeRate = 250f, StunDuration = 0.6f, ColorHex = "#00BFFF" } },
        { ElectricType.CopperArc, new() { Name = "Copper Arc", ArcRadius = 200f, ChainStrength = 12000f, EnergyCapacity = 800f, RechargeRate = 80f, DischargeRate = 180f, StunDuration = 0.3f, ColorHex = "#FFD700" } },
        { ElectricType.Plasma, new() { Name = "Plasma", ArcRadius = 280f, ChainStrength = 16000f, EnergyCapacity = 1000f, RechargeRate = 40f, DischargeRate = 220f, StunDuration = 0.8f, ColorHex = "#00FF00" } },
        { ElectricType.StormCloud, new() { Name = "Storm Cloud", ArcRadius = 400f, ChainStrength = 20000f, EnergyCapacity = 1500f, RechargeRate = 30f, DischargeRate = 280f, StunDuration = 1.0f, ColorHex = "#9370DB" } },
        { ElectricType.StaticSpark, new() { Name = "Static Spark", ArcRadius = 80f, ChainStrength = 4000f, EnergyCapacity = 200f, RechargeRate = 120f, DischargeRate = 100f, StunDuration = 0.1f, ColorHex = "#00FFFF" } },
        { ElectricType.IndustrialArc, new() { Name = "Industrial Arc", ArcRadius = 300f, ChainStrength = 17000f, EnergyCapacity = 1100f, RechargeRate = 45f, DischargeRate = 240f, StunDuration = 0.7f, ColorHex = "#FF4500" } },
        { ElectricType.VanDeGraaff, new() { Name = "Van de Graaff", ArcRadius = 150f, ChainStrength = 8000f, EnergyCapacity = 500f, RechargeRate = 100f, DischargeRate = 150f, StunDuration = 0.25f, ColorHex = "#FF00FF" } },
        { ElectricType.NeonGlow, new() { Name = "Neon Glow", ArcRadius = 120f, ChainStrength = 6000f, EnergyCapacity = 400f, RechargeRate = 90f, DischargeRate = 120f, StunDuration = 0.2f, ColorHex = "#40E0D0" } },
        { ElectricType.LightningBolt, new() { Name = "Lightning Bolt", ArcRadius = 500f, ChainStrength = 25000f, EnergyCapacity = 2000f, RechargeRate = 25f, DischargeRate = 300f, StunDuration = 1.5f, ColorHex = "#FFFFFF" } }
    };

    private ElectricType _type = ElectricType.TeslaCoil;
    private ElectricProfile _profile = _profiles[ElectricType.TeslaCoil];
    private float _energy = 500f;
    private ZapState _state = ZapState.Idle;
    private float _stateTimer = 0f;
    private int _totalZaps = 0;
    private float _peakPower = 0f;
    private float _consecutiveDischarges = 0;
    private readonly Queue<PendingZap> _pendingZaps = new();

    private class PendingZap { public RigidBody Target = null!; public float Force; public Vector2 Direction; public float ChainFactor = 1f; }

    public override BodyType Type => BodyType.Lightning;
    public override string Name => "Lightning";
    public override string Description => "Electric discharge with chaining";
    public override string ColorHex => _profile.ColorHex;
    public override double DefaultRadius => 14;
    public override double DefaultMass => 4;
    public override double DefaultRestitution => 0.7;

    public LightningBehavior() : this(ElectricType.TeslaCoil) { }
    public LightningBehavior(ElectricType type)
    {
        _type = type;
        _profile = _profiles[type];
        _energy = _profile.EnergyCapacity * 0.5f;
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        _energy = _profile.EnergyCapacity * 0.5f;
    }

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.IsStatic || body.IsFrozen) return;
        _stateTimer += (float)dt;
        UpdateEnergy(dt);
        UpdateStateMachine(body, dt, world);
    }

    private void UpdateEnergy(double dt)
    {
        if (_energy < _profile.EnergyCapacity)
            _energy += _profile.RechargeRate * (float)dt;
    }

    private void UpdateStateMachine(RigidBody body, double dt, PhysicsWorld world)
    {
        switch (_state)
        {
            case ZapState.Idle:
                if (_energy >= _profile.EnergyCapacity * 0.8f) _state = ZapState.Ready;
                else if (_energy > 0) _state = ZapState.Charging;
                break;
            case ZapState.Charging:
                if (_energy >= _profile.EnergyCapacity * 0.8f) _state = ZapState.Ready;
                break;
              case ZapState.Ready:
                 InitiateDischarge(body, world);
                 break;
            case ZapState.Discharging:
                if (_pendingZaps.Count == 0)
                {
                    _consecutiveDischarges++;
                    _state = _consecutiveDischarges >= 3 ? ZapState.Overloaded : ZapState.Recharging;
                }
                break;
            case ZapState.Recharging:
                if (_energy >= _profile.EnergyCapacity * 0.5f) _state = ZapState.Charging;
                break;
            case ZapState.Overloaded:
                _energy = _profile.EnergyCapacity * 0.1f;
                _consecutiveDischarges = 0;
                _state = ZapState.Recharging;
                break;
        }
    }

    private void InitiateDischarge(RigidBody body, PhysicsWorld world)
    {
        if (_energy < _profile.DischargeRate || world == null) { _state = ZapState.Recharging; return; }
        _energy -= _profile.DischargeRate;
        _state = ZapState.Discharging;
        _totalZaps++;

        var targets = FindTargets(body, world);
        if (targets.Count == 0) { _state = ZapState.Recharging; return; }

        var target = SelectTarget(body, targets);
        float force = _profile.ChainStrength * (float)new System.Random(body.Id * 31 + target.Id).NextDouble();
        _pendingZaps.Enqueue(new PendingZap { Target = target, Force = force, Direction = (target.Position - body.Position).Normalized });

        if (force > _peakPower) _peakPower = force;
    }

    private List<RigidBody> FindTargets(RigidBody body, PhysicsWorld world)
    {
        var result = new List<RigidBody>();
        if (world == null) return result;
        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic || other.IsFrozen) continue;
            float dist = (float)(body.Position - other.Position).Length;
            if (dist <= _profile.ArcRadius) result.Add(other);
        }
        return result;
    }

    private RigidBody SelectTarget(RigidBody body, List<RigidBody> candidates)
    {
        if (candidates.Count == 0) return body;
        float best = float.MinValue;
        RigidBody bestTarget = candidates[0];
        foreach (var c in candidates)
        {
            float dist = (float)(c.Position - body.Position).Length;
            float score = _profile.ArcRadius - dist;
            if (score > best) { best = score; bestTarget = c; }
        }
        return bestTarget;
    }

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        // No need to store world reference
    }

    public void SetType(ElectricType type)
    {
        _type = type;
        _profile = _profiles[type];
    }

    public ElectricType GetType() => _type;
    public ZapState GetState() => _state;
    public float GetEnergy() => _energy;
}