using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using System.Diagnostics;

namespace PhysicsSandbox.Behaviors;

public class AngelBehavior : BodyBehavior
{
    private const double FLIGHT_INTERVAL = 3.0;
    private const double FLIGHT_FORCE = 5000;
    private const double MAX_WING_SPAN = 25;
    private const double MIN_WING_SPAN = 10;
    private const double BASE_LIFT_COEFFICIENT = 0.8;
    private const double GRACE_FACTOR = 0.95;
    private const double DESCENT_RATE = 0.3;
    private const double HALO_RADIUS_FACTOR = 1.5;
    private const double AURA_INTENSITY = 0.7;
    private const double BLESSING_RADIUS = 100;
    private const double HEALING_FACTOR = 0.1;
    private const double SOUL_LIGHT_THRESHOLD = 50;
    private int _flightCount = 0;
    private double _totalFlightTime = 0;
    private bool _isFlying = false;
    private double _wingSpan = 18;
    private double _haloIntensity = 0;
    private double _auraRadius = 50;
    private double _divineEnergy = 100;
    private readonly List<Vector2> _trailPositions = new();
    private readonly List<Vector2> _wingPositions = new();
    private readonly Dictionary<int, double> _blessedBodies = new();
    private readonly Dictionary<int, double> _protectedBodies = new();
    private int _blessingCount = 0;
    private int _healingCount = 0;
    private int _protectionCount = 0;
    private bool _haloActive = false;
    private bool _auraActive = false;
    private double _ascensionTimer = 0;
    private Random _rng;
    private enum CelestialEffect { Halo, Aura, Trail, Blessing, Ascension }
    private readonly List<CelestialEffect> _celestialEffects = new();
    public enum WingType { Feather, Light, Ethereal, Golden, Silver, Crystal, Starlight }
    public enum HaloType { Classic, Radiant, Spiral, DoubleRing, AuraRing }
    private WingType _wingType = WingType.Feather;
    private HaloType _haloType = HaloType.Classic;
    private string _haloColor = "#FFFF00";
    private string _auraColor = "#ADD8E6";
    private string _wingColor = "#FFFFFF";
    public override BodyType Type => BodyType.Angel;
    public override string Name => "Angel";
    public override string Description => "Flies periodically - gentle & light";
    public override string ColorHex => "#FFFFFF";
    public override double DefaultRadius => 18;
    public override double DefaultMass => 4;
    public override double DefaultRestitution => 0.6;
    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        _rng = new Random(body.Id);
        body.FlyTimer = 0;
        _wingSpan = DefaultRadius;
        _divineEnergy = 100;
        LogDebug(body, "AngelBehavior initialized");
    }
    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        base.OnUpdate(body, dt, world);
        if (body.IsStatic || body.IsFrozen) return;
        body.FlyTimer += dt;
        if (body.FlyTimer >= FLIGHT_INTERVAL && !_isFlying)
        {
            StartFlight(body, world);
        }
        if (_isFlying)
        {
            UpdateFlight(body, dt, world);
        }
        else
        {
            ApplyGentleDescent(body, dt);
            UpdateTrail(body);
        }
        UpdateHaloAndAura(body, dt);
        ApplyBlessingEffect(body, world, dt);
        TrackStatistics(body, dt);
    }
    private void StartFlight(RigidBody body, PhysicsWorld world)
    {
        _isFlying = true;
        _flightCount++;
        body.FlyTimer = 0;
        _totalFlightTime = 0;
        _wingSpan = MAX_WING_SPAN;
        _haloIntensity = 1.0;
        double angle = _rng.NextDouble() * System.Math.PI * 2;
        double forceVariation = 0.8 + _rng.NextDouble() * 0.4;
        var dir = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));
        body.ApplyImpulse(dir * FLIGHT_FORCE * forceVariation);
        LogDebug(body, "Starting celestial flight");
    }
    private void UpdateFlight(RigidBody body, double dt, PhysicsWorld world)
    {
        _totalFlightTime += dt;
        double liftForce = CalculateLift(body);
        var liftVector = new Vector2(0, (float)-liftForce);
        body.ApplyForce(liftVector);
        ApplyGracefulMotion(body, dt);
        UpdateWingPositions(body, dt);
        if (_totalFlightTime > 5.0 || body.Velocity.Length < 10)
        {
            EndFlight(body);
        }
    }
    private void EndFlight(RigidBody body)
    {
        _isFlying = false;
        _totalFlightTime = 0;
        _wingSpan = MIN_WING_SPAN;
        _haloIntensity = 0.3;
        body.Velocity = new Vector2(body.Velocity.X * 0.5f, body.Velocity.Y * 0.5f);
        LogDebug(body, "Flight ended");
    }
    private double CalculateLift(RigidBody body)
    {
        double velocity = body.Velocity.Length;
        double area = System.Math.PI * _wingSpan * _wingSpan;
        double airDensity = 1.225;
        return BASE_LIFT_COEFFICIENT * 0.5 * airDensity * velocity * velocity * area * 0.001;
    }
    private void ApplyGracefulMotion(RigidBody body, double dt)
    {
        double damping = System.Math.Pow(GRACE_FACTOR, dt * 60);
        body.Velocity *= (float)damping;
        body.AngularVelocity *= (float)0.98;
    }
    private void UpdateWingPositions(RigidBody body, double dt)
    {
        double wingFlap = System.Math.Sin(_totalFlightTime * 10) * _wingSpan * 0.3;
        _wingPositions.Clear();
        _wingPositions.Add(body.Position + new Vector2(-_wingSpan, wingFlap));
        _wingPositions.Add(body.Position + new Vector2(_wingSpan, -wingFlap));
    }
    private void ApplyGentleDescent(RigidBody body, double dt)
    {
        body.Velocity = new Vector2(body.Velocity.X * 0.5f, body.Velocity.Y * 0.5f);
    }
    private void UpdateTrail(RigidBody body)
    {
        _trailPositions.Add(body.Position);
        while (_trailPositions.Count > 20) { _trailPositions.RemoveAt(0); }
    }
    protected override void RenderDebugOverlay(RigidBody body, DrawingContext dc)
    {
        if (dc == null || !GlobalConfig.EnableDebugVisualization) return;
        base.RenderDebugOverlay(body, dc);
        for (int i = 1; i < _trailPositions.Count; i++)
        {
            double opacity = (double)i / _trailPositions.Count;
            var pen = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)(opacity * 128), 200, 230, 255)), 1.5);
            dc.DrawLine(pen, new System.Windows.Point(_trailPositions[i - 1].X, _trailPositions[i - 1].Y), new System.Windows.Point(_trailPositions[i].X, _trailPositions[i].Y));
        }
    }
    private void UpdateHaloAndAura(RigidBody body, double dt)
    {
        _haloIntensity = _isFlying ? 1.0 : 0.3;
        _auraRadius = _isFlying ? 70 : 50;
    }
    private void ApplyBlessingEffect(RigidBody body, PhysicsWorld world, double dt)
    {
        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic) continue;
            double dist = Vector2.Distance(body.Position, other.Position);
            if (dist < BLESSING_RADIUS)
            {
                double blessingStrength = (1.0 - dist / BLESSING_RADIUS) * dt * HEALING_FACTOR;
                BlessBody(other, blessingStrength);
            }
        }
    }
    private void BlessBody(RigidBody body, double strength)
    {
        if (!_blessedBodies.ContainsKey(body.Id)) _blessedBodies[body.Id] = 0;
        _blessedBodies[body.Id] += strength;
        if (_blessedBodies[body.Id] > 10.0)
        {
            _blessingCount++;
            _healingCount++;
            _blessedBodies[body.Id] = 0;
        }
    }
    private void TrackStatistics(RigidBody body, double dt) { }
    public string GetDiagnosticsReport(RigidBody body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== AngelBehavior Diagnostics ===");
        sb.AppendLine($"Flight Count: {_flightCount}");
        sb.AppendLine($"Is Flying: {_isFlying}");
        sb.AppendLine($"Divine Energy: {_divineEnergy}");
        return sb.ToString();
    }
    public int GetFlightCount() => _flightCount;
    public bool IsFlying() => _isFlying;
}
