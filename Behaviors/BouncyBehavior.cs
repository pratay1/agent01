using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace PhysicsSandbox.Behaviors;

public class BouncyBehavior : BodyBehavior
{
    private const double MAX_BOUNCE_ENERGY = 50000.0;
    private const double MIN_BOUNCE_VELOCITY = 0.5;
    private const double DEFAULT_SPIN_TRANSFER = 0.3;
    private const double MAX_CHAIN_DISTANCE = 200.0;
    private const int MAX_CHAIN_REACTIONS = 10;
    private const double COMBO_WINDOW = 2.0;
    private const double TEMP_FACTOR = 0.002;
    private const int MAX_HISTORY = 50;

    public enum BounceState { Idle, Airborne, Bouncing, SuperBounce, Dampened, Spinning, ChainReacting, Stuck }
    public enum BounceMaterial { SuperBall, Rubber, TennisBall, Basketball, GolfBall, Trampoline, MemoryFoam, BouncyCastle, SpringSteel, GelPad, Custom }

    public class BounceProfile
    {
        public string Name = "";
        public double Restitution = 0.95;
        public double DampingDecay = 0.98;
        public double SpinTransfer = 0.3;
        public double MassMult = 1.0;
        public double RadiusMult = 1.0;
        public double MaxVelocity = 1000.0;
        public double SurfaceGrip = 0.5;
        public string ColorHex = "#81C784";
        public bool CanChainReact = false;
        public double ChainForce = 0.0;
    }

    private static readonly Dictionary<BounceMaterial, BounceProfile> _profiles = new()
    {
        { BounceMaterial.SuperBall, new() { Name = "Super Ball", Restitution = 0.97, DampingDecay = 0.985, SpinTransfer = 0.4, MassMult = 0.8, MaxVelocity = 2000.0, SurfaceGrip = 0.3, ColorHex = "#FF0000", CanChainReact = true, ChainForce = 50.0 } },
        { BounceMaterial.Rubber, new() { Name = "Rubber", Restitution = 0.85, DampingDecay = 0.96, SpinTransfer = 0.25, MassMult = 1.0, MaxVelocity = 800.0, SurfaceGrip = 0.7, ColorHex = "#8B4513" } },
        { BounceMaterial.TennisBall, new() { Name = "Tennis Ball", Restitution = 0.82, DampingDecay = 0.95, SpinTransfer = 0.35, MassMult = 0.4, MaxVelocity = 600.0, SurfaceGrip = 0.6, ColorHex = "#FFFF00" } },
        { BounceMaterial.Basketball, new() { Name = "Basketball", Restitution = 0.75, DampingDecay = 0.92, SpinTransfer = 0.2, MassMult = 1.2, MaxVelocity = 700.0, SurfaceGrip = 0.8, ColorHex = "#FF8C00" } },
        { BounceMaterial.GolfBall, new() { Name = "Golf Ball", Restitution = 0.80, DampingDecay = 0.97, SpinTransfer = 0.45, MassMult = 0.5, MaxVelocity = 1500.0, SurfaceGrip = 0.4, ColorHex = "#FFFFFF", CanChainReact = true, ChainForce = 30.0 } },
        { BounceMaterial.Trampoline, new() { Name = "Trampoline", Restitution = 0.92, DampingDecay = 0.90, SpinTransfer = 0.1, MassMult = 0.3, MaxVelocity = 1200.0, SurfaceGrip = 0.2, ColorHex = "#00FF00", CanChainReact = true, ChainForce = 100.0 } },
        { BounceMaterial.MemoryFoam, new() { Name = "Memory Foam", Restitution = 0.30, DampingDecay = 0.85, SpinTransfer = 0.05, MassMult = 1.5, MaxVelocity = 300.0, SurfaceGrip = 0.9, ColorHex = "#FFB6C1" } },
        { BounceMaterial.BouncyCastle, new() { Name = "Bouncy Castle", Restitution = 0.88, DampingDecay = 0.93, SpinTransfer = 0.15, MassMult = 0.6, MaxVelocity = 900.0, SurfaceGrip = 0.3, ColorHex = "#FF69B4", CanChainReact = true, ChainForce = 80.0 } },
        { BounceMaterial.SpringSteel, new() { Name = "Spring Steel", Restitution = 0.70, DampingDecay = 0.99, SpinTransfer = 0.5, MassMult = 2.0, MaxVelocity = 1800.0, SurfaceGrip = 0.25, ColorHex = "#708090", CanChainReact = true, ChainForce = 40.0 } },
        { BounceMaterial.GelPad, new() { Name = "Gel Pad", Restitution = 0.60, DampingDecay = 0.88, SpinTransfer = 0.08, MassMult = 1.8, MaxVelocity = 400.0, SurfaceGrip = 0.85, ColorHex = "#DDA0DD" } }
    };

    private BounceMaterial _material = BounceMaterial.SuperBall;
    private BounceProfile _profile = _profiles[BounceMaterial.SuperBall];
    private BounceState _state = BounceState.Idle;
    private double _stateTimer = 0.0;
    private int _bounceCount = 0;
    private double _lastBounceTime = double.MaxValue;
    private double _bounceEnergy = 0.0;
    private double _totalEnergy = 0.0;
    private int _combo = 0;
    private double _comboTimer = 0.0;
    private double _temperature = 20.0;
    private double _tempFactor = 1.0;
    private bool _inVacuum = false;
    private double _peakVelocity = 0.0;
    private readonly List<Vector2> _collisionHistory = new();

    public override BodyType Type => BodyType.Bouncy;
    public override string Name => "Bouncy";
    public override string Description => "Super bouncy body with chain reactions";
    public override string ColorHex => _profile.ColorHex;
    public override double DefaultRadius => 12;
    public override double DefaultMass => 6;
    public override double DefaultRestitution => _profile.Restitution;

    public BouncyBehavior() : this(BounceMaterial.SuperBall) { }
    public BouncyBehavior(BounceMaterial material)
    {
        _material = material;
        _profile = _profiles[material];
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        body.Restitution = _profile.Restitution;
        body.Mass = DefaultMass * _profile.MassMult;
        body.Radius = DefaultRadius * _profile.RadiusMult;
    }

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.IsStatic || body.IsFrozen) return;
        UpdateStateMachine(body, dt);
        ApplyDecay(body, dt);
        UpdateCombo(body, dt);
        _stateTimer += dt;
    }

    private void UpdateStateMachine(RigidBody body, double dt)
    {
        _lastBounceTime += dt;
        switch (_state)
        {
            case BounceState.Idle:
                if (body.Velocity.LengthSquared > MIN_BOUNCE_VELOCITY * MIN_BOUNCE_VELOCITY)
                    _state = BounceState.Airborne;
                break;
            case BounceState.Airborne:
                if (_lastBounceTime < 0.1) _state = BounceState.Bouncing;
                if (body.Velocity.Length > _profile.MaxVelocity) _state = BounceState.SuperBounce;
                break;
            case BounceState.Bouncing:
                if (_lastBounceTime > 1.0) _state = BounceState.Dampened;
                break;
            case BounceState.Dampened:
                if (body.Velocity.LengthSquared < MIN_BOUNCE_VELOCITY * MIN_BOUNCE_VELOCITY)
                    _state = BounceState.Idle;
                break;
            case BounceState.ChainReacting:
                if (_stateTimer > 0.5) _state = BounceState.Airborne;
                break;
        }
    }

    private void ApplyDecay(RigidBody body, double dt)
    {
        if (_lastBounceTime > 0.5)
        {
            double decay = Math.Pow(_profile.DampingDecay, dt * 60);
            body.Velocity *= decay;
        }
    }

    private void UpdateCombo(RigidBody body, double dt)
    {
        if (_lastBounceTime < COMBO_WINDOW)
        {
            _comboTimer += dt;
            _combo = Math.Max(_combo, 1);
        }
        else
        {
            _combo = 0;
            _comboTimer = 0.0;
        }
    }

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        _bounceCount++;
        _lastBounceTime = 0.0;
        _stateTimer = 0.0;

        Vector2 normal = (other.Position - body.Position).Normalized;
        double restitution = body.Restitution * _tempFactor;
        Vector2 relVel = other.Velocity - body.Velocity;
        double closingSpeed = Vector2.Dot(relVel, normal);

        if (closingSpeed > 0) return;

        double invMassSum = body.InverseMass + other.InverseMass;
        if (invMassSum < 1e-6) return;

        double j = -(1 + restitution) * closingSpeed / invMassSum;
        Vector2 impulse = normal * j;
        body.ApplyImpulse(impulse);

        double preEnergy = 0.5 * body.Mass * body.Velocity.LengthSquared;
        _bounceEnergy = Math.Abs(0.5 * j * j / invMassSum);
        _totalEnergy += _bounceEnergy;

        ApplySpin(body, other, normal);

        double speed = body.Velocity.Length;
        if (speed > _peakVelocity) _peakVelocity = speed;

        if (_profile.CanChainReact)
            TriggerChainReaction(body, world);

        _state = BounceState.Bouncing;
    }

    private void ApplySpin(RigidBody body, RigidBody other, Vector2 normal)
    {
        if (body.IsStatic || other.IsStatic) return;
        Vector2 tangent = new(-normal.Y, normal.X);
        double tangentVel = Vector2.Dot(body.Velocity - other.Velocity, tangent);
        double spinImpulse = tangentVel * _profile.SpinTransfer * DEFAULT_SPIN_TRANSFER * body.Mass;
        body.AngularVelocity += spinImpulse / (0.4 * body.Mass * body.Radius * body.Radius);
    }

    private void TriggerChainReaction(RigidBody body, PhysicsWorld world)
    {
        _state = BounceState.ChainReacting;
        int count = 0;
        foreach (var other in SpatialQuery(body.Position, MAX_CHAIN_DISTANCE, world))
        {
            if (count >= MAX_CHAIN_REACTIONS) break;
            if (other == body || other.IsStatic) continue;
            double dist = (body.Position - other.Position).Length;
            double falloff = 1.0 - dist / MAX_CHAIN_DISTANCE;
            double force = _profile.ChainForce * falloff;
            if (force > 10)
            {
                Vector2 dir = (other.Position - body.Position).Normalized;
                other.ApplyImpulse(dir * force);
                count++;
            }
        }
    }

    public void SetMaterial(RigidBody body, BounceMaterial mat)
    {
        _material = mat;
        _profile = _profiles[mat];
        body.Restitution = _profile.Restitution;
        body.Mass = DefaultMass * _profile.MassMult;
    }

    public BounceMaterial GetMaterial() => _material;
    public int GetBounceCount() => _bounceCount;
    public double GetTotalEnergy() => _totalEnergy;
    public BounceState GetState() => _state;
    public int GetCombo() => _combo;
}