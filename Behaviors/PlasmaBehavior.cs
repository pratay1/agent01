using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Diagnostics;
using System.Text;

namespace PhysicsSandbox.Behaviors;

public class PlasmaBehavior : BodyBehavior
{
    #region Constants & Tunable Parameters

    private const double PLASMA_CORE_RADIUS = 15.0;
    private const double PLASMA_FIELD_RADIUS = 200.0;
    private const double PLASMA_FORCE_SCALE = 3500.0;
    private const double PLASMA_CHAIN_LIGHTNING_CHANCE = 0.15;
    private const double PLASMA_CHAIN_MAX_DISTANCE = 180.0;
    private const int PLASMA_CHAIN_MAX_JUMPS = 5;
    private const double PLASMA_ENERGY_RETENTION = 0.75;
    private const double PLASMA_TEMPERATURE_BASE = 5000.0;
    private const int PLASMA_MAX_PARTICLES = 100;
    private const double PLASMA_PARTICLE_LIFETIME = 2.0;
    private const double PLASMA_VISUAL_PULSE_RATE = 4.0;
    private const double PLASMA_FIELD_FLUCTUATION = 0.1;
    private const double MIN_PLASMA_INTERACTION_DIST = 5.0;
    private const double PLASMA_IONIZATION_THRESHOLD = 0.3;
    private const double PLASMA_DISCHARGE_COOLDOWN = 0.1;
    private const int PLASMA_MAX_DISCHARGE_HISTORY = 20;
    private const double PLASMA_ENERGY_DISSIPATION_RATE = 0.95;
    private const double PLASMA_MIN_INTERACTION_ENERGY = 10.0;
    private const double PLASMA_CORE_TEMPERATURE_MULTIPLIER = 2.5;

    #endregion

    #region Plasma State Fields

    private double _plasmaEnergy = 100.0;
    private double _coreTemperature = PLASMA_CORE_TEMPERATURE_MULTIPLIER * 1000.0;
    private double _fieldFluctuationPhase = 0.0;
    private double _dischargeCooldownTimer = 0.0;
    private int _chainLightningCount = 0;
    private bool _isDischarging = false;
    private Vector2 _lastDischargePosition = Vector2.Zero;
    private double _totalEnergyDischarged = 0.0;
    private int _totalDischarges = 0;
    private readonly List<PlasmaParticle> _plasmaParticles = new();
    private readonly Queue<DischargeRecord> _dischargeHistory = new();
    private readonly List<Vector2> _recentPositions = new();
    private readonly Stopwatch _updateStopwatch = new();

    #endregion

    #region Plasma Particle System

    private class PlasmaParticle
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public double LifeTime { get; set; }
        public double MaxLifeTime { get; set; }
        public double Energy { get; set; }
        public Color Color { get; set; }
        public double Size { get; set; }
        public bool IsActive { get; set; }
    }

    private class DischargeRecord
    {
        public Vector2 StartPosition { get; set; }
        public Vector2 EndPosition { get; set; }
        public double Energy { get; set; }
        public double Time { get; set; }
        public int TargetBodyId { get; set; }
    }

    #endregion

    #region Behavior Properties (Overrides)

    public override BodyType Type => BodyType.Plasma;
    public override string Name => "Plasma";
    public override string Description => "Electric plasma body that creates field interactions, chain lightning, and energy-based effects with nearby bodies";
    public override string ColorHex => "#E91E63";
    public override double DefaultRadius => PLASMA_CORE_RADIUS;
    public override double DefaultMass => 4;
    public override double DefaultRestitution => 0.6;

    #endregion

    #region Constructors & Initialization

    public PlasmaBehavior()
    {
        _plasmaEnergy = 100.0;
        _coreTemperature = PLASMA_TEMPERATURE_BASE;
        _fieldFluctuationPhase = 0.0;
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        body.Restitution = DefaultRestitution;
        body.Mass = DefaultMass;
        _lastDischargePosition = body.Position;
        LogDebug(body, $"PlasmaBehavior initialized");
    }

    #endregion

    #region Main Update Loop

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        _updateStopwatch.Restart();
        try
        {
            if (body.IsStatic || body.IsFrozen) return;
            if (!Config.Enabled) return;
            RaisePreUpdate(body, dt);
            UpdateFieldFluctuation(dt);
            UpdatePlasmaEnergy(dt);
            UpdateParticleSystem(body, dt);
            UpdateDischargeCooldown(dt);
            UpdateRecentPositions(body);
            ApplyPlasmaField(body, dt, world);
            AttemptChainLightning(body, dt, world);
            UpdateCoreTemperature(body, dt);
            SpawnPlasmaParticles(body, dt);
            UpdatePositionTracking(body);
            ClampPlasmaEnergy();
            ClampVelocityIfOverheated(body);
            RaisePostUpdate(body, dt);
        }
        finally
        {
            _updateStopwatch.Stop();
            RecordPerformanceMetric("OnUpdate", _updateStopwatch.Elapsed.TotalMilliseconds);
        }
    }

    #endregion

    #region Plasma Field Physics

    private void ApplyPlasmaField(RigidBody body, double dt, PhysicsWorld world)
    {
        double currentRadius = GetEffectiveFieldRadius();
        double currentStrength = GetFieldStrength();
        foreach (var other in world.Bodies)
        {
            if (body == other || other.IsStatic) continue;
            Vector2 direction = other.Position - body.Position;
            double distance = direction.Length;
            if (distance < MIN_PLASMA_INTERACTION_DIST || distance > currentRadius) continue;
            double normalizedDist = distance / currentRadius;
            double forceFactor = 1.0 - normalizedDist;
            double forceMagnitude = currentStrength * forceFactor * forceFactor;
            Vector2 mainForce = direction.Normalized * forceMagnitude;
            Vector2 perpendicularForce = new Vector2(-direction.Y, direction.X).Normalized * forceMagnitude * 0.3;
            other.ApplyForce(mainForce);
            other.ApplyForce(perpendicularForce);
            TransferPlasmaEnergy(body, other, forceMagnitude * dt);
            TriggerIonizationEffect(body, other, distance, normalizedDist);
        }
    }

    private void TransferPlasmaEnergy(RigidBody body, RigidBody other, double energyTransfer)
    {
        if (energyTransfer < PLASMA_MIN_INTERACTION_ENERGY) return;
        _plasmaEnergy -= energyTransfer * 0.05;
    }

    private void TriggerIonizationEffect(RigidBody body, RigidBody other, double distance, double normalizedDist)
    {
        if (normalizedDist < PLASMA_IONIZATION_THRESHOLD && _plasmaEnergy > 50.0)
        {
            if (other.BodyType == BodyType.Normal) _plasmaEnergy -= 5.0 * (1.0 - normalizedDist);
        }
    }

    #endregion

    #region Chain Lightning System

    private void AttemptChainLightning(RigidBody body, double dt, PhysicsWorld world)
    {
        if (_dischargeCooldownTimer > 0.0) return;
        if (_plasmaEnergy < 20.0) return;
        double chainChance = PLASMA_CHAIN_LIGHTNING_CHANCE * GetDischargeProbability();
        if (StaticRandom.NextDouble() >= chainChance) return;
        TriggerChainLightning(body, dt, world);
    }

    private void TriggerChainLightning(RigidBody body, double dt, PhysicsWorld world)
    {
        _isDischarging = true;
        _chainLightningCount = 0;
        _lastDischargePosition = body.Position;
        List<RigidBody> affectedBodies = new();
        RigidBody currentBody = body;
        double remainingEnergy = _plasmaEnergy * 0.3;
        for (int i = 0; i < PLASMA_CHAIN_MAX_JUMPS; i++)
        {
            RigidBody? target = FindLightningTarget(currentBody, world, affectedBodies);
            if (target == null) break;
            double distance = Vector2.Distance(currentBody.Position, target.Position);
            double energyCost = distance / PLASMA_CHAIN_MAX_DISTANCE * remainingEnergy;
            if (energyCost > remainingEnergy * 0.1)
            {
                ApplyLightningImpact(currentBody, target, energyCost, distance);
                RecordDischarge(currentBody.Position, target.Position, energyCost, target.Id);
                affectedBodies.Add(target);
                currentBody = target;
                remainingEnergy -= energyCost;
                _chainLightningCount++;
                _totalEnergyDischarged += energyCost;
                SpawnLightningParticles(currentBody.Position, target.Position, energyCost);
            }
            else break;
            if (remainingEnergy < 5.0) break;
        }
        _plasmaEnergy -= (_plasmaEnergy * 0.3) - remainingEnergy;
        _totalDischarges++;
        _dischargeCooldownTimer = PLASMA_DISCHARGE_COOLDOWN;
        _isDischarging = false;
        LogDebug(body, $"Chain lightning: jumps={_chainLightningCount}");
    }

    private RigidBody? FindLightningTarget(RigidBody currentBody, PhysicsWorld world, List<RigidBody> excluded)
    {
        RigidBody? bestTarget = null;
        double bestScore = double.MaxValue;
        foreach (var other in world.Bodies)
        {
            if (other == currentBody || other.IsStatic || excluded.Contains(other)) continue;
            double distance = Vector2.Distance(currentBody.Position, other.Position);
            if (distance > PLASMA_CHAIN_MAX_DISTANCE) continue;
            double score = distance / PLASMA_CHAIN_MAX_DISTANCE;
            if (score < bestScore) { bestScore = score; bestTarget = other; }
        }
        return bestTarget;
    }

    private void ApplyLightningImpact(RigidBody from, RigidBody to, double energy, double distance)
    {
        Vector2 direction = (to.Position - from.Position).Normalized;
        double impulseMagnitude = energy * 0.1;
        if (!to.IsStatic) to.ApplyImpulse(direction * impulseMagnitude);
        if (!from.IsStatic) from.ApplyImpulse(-direction * impulseMagnitude * 0.1);
    }

    private void RecordDischarge(Vector2 start, Vector2 end, double energy, int targetId)
    {
        var record = new DischargeRecord { StartPosition = start, EndPosition = end, Energy = energy, Time = 0.0, TargetBodyId = targetId };
        _dischargeHistory.Enqueue(record);
        while (_dischargeHistory.Count > PLASMA_MAX_DISCHARGE_HISTORY) _dischargeHistory.Dequeue();
    }

    #endregion

    #region Particle System

    private void SpawnPlasmaParticles(RigidBody body, double dt)
    {
        if (_plasmaParticles.Count >= PLASMA_MAX_PARTICLES) return;
        double spawnRate = _plasmaEnergy > 80.0 ? 5.0 : 2.0;
        int particlesToSpawn = StaticRandom.Next(0, (int)(spawnRate * dt * 60) + 1);
        for (int i = 0; i < particlesToSpawn; i++)
        {
            if (_plasmaParticles.Count >= PLASMA_MAX_PARTICLES) break;
            double angle = StaticRandom.NextDouble() * Math.PI * 2;
            double radius = StaticRandom.NextDouble() * body.Radius * 0.5;
            double speed = StaticRandom.NextDouble() * 50.0 + 10.0;
            _plasmaParticles.Add(new PlasmaParticle
            {
                Position = body.Position + new Vector2(Math.Cos(angle), Math.Sin(angle)) * radius,
                Velocity = new Vector2(Math.Cos(angle), Math.Sin(angle)) * speed,
                MaxLifeTime = PLASMA_PARTICLE_LIFETIME * StaticRandom.NextDouble(0.5, 1.0),
                LifeTime = PLASMA_PARTICLE_LIFETIME * StaticRandom.NextDouble(0.5, 1.0),
                Energy = _plasmaEnergy * StaticRandom.NextDouble(0.1, 0.5),
                Size = StaticRandom.NextDouble(2.0, 6.0),
                Color = ColorFromTemperature(_coreTemperature),
                IsActive = true
            });
        }
    }

    private void UpdateParticleSystem(RigidBody body, double dt)
    {
        for (int i = _plasmaParticles.Count - 1; i >= 0; i--)
        {
            var p = _plasmaParticles[i];
            if (!p.IsActive) { _plasmaParticles.RemoveAt(i); continue; }
            p.LifeTime -= dt;
            if (p.LifeTime <= 0) { _plasmaParticles.RemoveAt(i); continue; }
            p.Velocity *= 0.98;
            p.Position += p.Velocity * dt;
            p.Color = ColorFromTemperature(_coreTemperature * (p.LifeTime / p.MaxLifeTime));
            p.Size *= 0.995;
        }
    }

    #endregion

    #region Temperature & Energy Management

    private void UpdateCoreTemperature(RigidBody body, double dt)
    {
        double activityLevel = body.Velocity.Length / 100.0;
        double targetTemperature = PLASMA_TEMPERATURE_BASE + (activityLevel * 2000.0);
        double tempDiff = targetTemperature - _coreTemperature;
        _coreTemperature += tempDiff * dt * 2.0;
        _coreTemperature = Math.Max(1000.0, _coreTemperature);
    }

    private void UpdatePlasmaEnergy(double dt)
    {
        if (_plasmaEnergy < 100.0) _plasmaEnergy += 10.0 * dt;
        else _plasmaEnergy -= 5.0 * dt * 0.1;
        if (_coreTemperature > 1500.0) _plasmaEnergy -= (_coreTemperature - 1500.0) / 5000.0 * dt * 10.0;
    }

    private void ReceiveEnergy(double energy) { _plasmaEnergy += energy; _coreTemperature += energy * 0.1; }

    private void ClampPlasmaEnergy()
    {
        _plasmaEnergy = Math.Clamp(_plasmaEnergy, 0.0, 500.0);
        _coreTemperature = Math.Clamp(_coreTemperature, 1000.0, 15000.0);
    }

    private void ClampVelocityIfOverheated(RigidBody body)
    {
        if (_coreTemperature > 10000.0)
        {
            double overheatFactor = (_coreTemperature - 10000.0) / 5000.0;
            body.Velocity *= (1.0 - Math.Min(overheatFactor, 0.5));
        }
    }

    #endregion

    #region Visual Effects

    protected override void RenderDebugOverlay(RigidBody body, DrawingContext dc)
    {
        if (dc == null || !GlobalConfig.EnableDebugVisualization) return;
        DrawPlasmaField(body, dc); DrawParticles(dc); DrawDischargeHistory(dc);
        DrawEnergyOverlay(body, dc); DrawCoreGlow(body, dc);
    }

    private void DrawPlasmaField(RigidBody body, DrawingContext dc)
    {
        double radius = GetEffectiveFieldRadius();
        double opacity = 0.1 + (_plasmaEnergy / 500.0) * 0.2;
        double fluctuation = Math.Sin(_fieldFluctuationPhase) * PLASMA_FIELD_FLUCTUATION;
        radius *= (1.0 + fluctuation);
        var brush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 233, 30, 99));
        var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 150), 233, 30, 99)), 1.0);
        dc.DrawEllipse(brush, pen, new Point(body.Position.X, body.Position.Y), radius, radius);
        if (_isDischarging)
        {
            var db = new SolidColorBrush(Color.FromArgb(100, 255, 255, 0));
            dc.DrawEllipse(null, new Pen(db, 2.0), new Point(body.Position.X, body.Position.Y), radius * 1.2, radius * 1.2);
        }
    }

    private void DrawParticles(DrawingContext dc)
    {
        foreach (var p in _plasmaParticles)
        {
            if (!p.IsActive) continue;
            var brush = new SolidColorBrush(p.Color);
            dc.DrawEllipse(brush, null, new Point(p.Position.X, p.Position.Y), p.Size, p.Size);
        }
    }

    private void DrawDischargeHistory(DrawingContext dc)
    {
        foreach (var r in _dischargeHistory)
        {
            double opacity = Math.Max(0, 1.0 - (r.Time / 0.5));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 200), 255, 255, 0)), 2.0);
            dc.DrawLine(pen, new Point(r.StartPosition.X, r.StartPosition.Y), new Point(r.EndPosition.X, r.EndPosition.Y));
        }
    }

    private void DrawEnergyOverlay(RigidBody body, DrawingContext dc)
    {
        double er = _plasmaEnergy / 500.0;
        double bw = 40, bh = 6;
        double x = body.Position.X - bw / 2, y = body.Position.Y + body.Radius + 10;
        var bg = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
        var fill = er > 0.8 ? Brushes.Red : er > 0.5 ? Brushes.Yellow : Brushes.LimeGreen;
        dc.DrawRectangle(bg, null, new Rect(x, y, bw, bh));
        dc.DrawRectangle(fill, null, new Rect(x, y, bw * er, bh));
        var tf = new Typeface("Consolas");
        var fmt = new FormattedText($"{_plasmaEnergy:F0}", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 9, Brushes.White);
        dc.DrawText(fmt, new Point(body.Position.X - 15, y + bh + 2));
    }

    private void DrawCoreGlow(RigidBody body, DrawingContext dc)
    {
        double pulse = Math.Sin(_fieldFluctuationPhase * 2.0) * 0.3 + 0.7;
        double gr = body.Radius * (1.5 + pulse * 0.5);
        var cc = ColorFromTemperature(_coreTemperature);
        var gradient = new RadialGradientBrush { Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5, GradientOrigin = new Point(0.5, 0.5) };
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * pulse), cc.R, cc.G, cc.B), 0.0));
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(100 * pulse), cc.R, cc.G, cc.B), 0.7));
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, cc.R, cc.G, cc.B), 1.0));
        dc.DrawEllipse(gradient, null, new Point(body.Position.X, body.Position.Y), gr, gr);
    }

    private Color ColorFromTemperature(double temp)
    {
        double t = temp / 15000.0; t = Math.Clamp(t, 0.0, 1.0);
        byte r, g, b;
        if (t < 0.33) { r = (byte)(t / 0.33 * 255); g = 0; b = (byte)(255 - t / 0.33 * 255); }
        else if (t < 0.66) { r = 255; g = (byte)((t - 0.33) / 0.33 * 255); b = 0; }
        else { r = 255; g = 255; b = (byte)((t - 0.66) / 0.34 * 255); }
        return Color.FromArgb(255, r, g, b);
    }

    #endregion

    #region Configuration

    public double GetEffectiveFieldRadius() { return PLASMA_FIELD_RADIUS * (1.0 + Math.Sin(_fieldFluctuationPhase) * PLASMA_FIELD_FLUCTUATION); }
    public double GetFieldStrength() { return PLASMA_FORCE_SCALE * (_plasmaEnergy / 100.0); }
    public double GetDischargeProbability() { return Math.Clamp(_plasmaEnergy / 500.0, 0.0, 1.0); }
    public void SetPlasmaEnergy(double e) { _plasmaEnergy = Math.Clamp(e, 0.0, 500.0); }
    public void SetCoreTemperature(double t) { _coreTemperature = Math.Clamp(t, 1000.0, 15000.0); }
    public void TriggerManualDischarge(RigidBody body, double energy)
    {
        if (_dischargeCooldownTimer > 0.0 || energy > _plasmaEnergy) return;
        _plasmaEnergy -= energy; _isDischarging = true; _dischargeCooldownTimer = PLASMA_DISCHARGE_COOLDOWN;
        RecordDischarge(body.Position, body.Position + new Vector2(body.Radius * 2, 0), energy, -1);
        _totalEnergyDischarged += energy; _totalDischarges++;
        LogDebug(body, $"Manual discharge: e={energy:F2}");
    }

    #endregion

    #region Collision Handling

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        double impactSpeed = (body.Velocity - other.Velocity).Length;
        if (impactSpeed > 50.0)
        {
            double collisionEnergy = 0.5 * body.Mass * impactSpeed * impactSpeed;
            _plasmaEnergy += collisionEnergy * 0.1;
            double dischargeChance = Math.Clamp(collisionEnergy / 1000.0, 0.0, 0.8);
            if (StaticRandom.NextDouble() < dischargeChance && _dischargeCooldownTimer <= 0.0)
            {
                TriggerImpactDischarge(body, other, collisionEnergy);
            }
        }
        RaiseCollision(body, other);
    }

    private void TriggerImpactDischarge(RigidBody body, RigidBody other, double energy)
    {
        double de = Math.Min(energy * 0.2, _plasmaEnergy);
        _plasmaEnergy -= de;
        _dischargeCooldownTimer = PLASMA_DISCHARGE_COOLDOWN;
        Vector2 ed = (other.Position - body.Position).Normalized;
        Vector2 ee = body.Position + ed * body.Radius * 2;
        RecordDischarge(body.Position, ee, de, other.Id);
        SpawnLightningParticles(body.Position, ee, de);
        _totalEnergyDischarged += de;
        _totalDischarges++;
        other.ApplyImpulse(ed * de * 0.05);
    }

    #endregion

    #region Utilities

    private void UpdateFieldFluctuation(double dt) { _fieldFluctuationPhase += PLASMA_VISUAL_PULSE_RATE * dt; if (_fieldFluctuationPhase > Math.PI * 2) _fieldFluctuationPhase -= (float)(Math.PI * 2); }
    private void UpdateDischargeCooldown(double dt) { if (_dischargeCooldownTimer > 0.0) _dischargeCooldownTimer -= dt; }
    private void UpdateRecentPositions(RigidBody body) { if (_recentPositions.Count == 0 || Vector2.Distance(_recentPositions[_recentPositions.Count - 1], body.Position) > 5.0) { _recentPositions.Add(body.Position); if (_recentPositions.Count > 50) _recentPositions.RemoveAt(0); } }
    private void UpdatePositionTracking(RigidBody body) { if (_recentPositions.Count > 0) _recentPositions[_recentPositions.Count - 1] = body.Position; }
    private void SpawnLightningParticles(Vector2 start, Vector2 end, double energy)
    {
        // Stub for particle effects
    }

    #endregion

    #region Diagnostics

    public string GetDiagnosticsReport(RigidBody body)
    {
        var sb = new StringBuilder(); sb.AppendLine($"=== PlasmaBehavior Diagnostics ===");
        sb.AppendLine($"Core Temperature: {_coreTemperature:F1}K");
        sb.AppendLine($"Plasma Energy: {_plasmaEnergy:F2}");
        sb.AppendLine($"Field Radius: {GetEffectiveFieldRadius():F2}");
        sb.AppendLine($"Field Strength: {GetFieldStrength():F2}");
        sb.AppendLine($"Discharge Cooldown: {_dischargeCooldownTimer:F3}s");
        sb.AppendLine($"Chain Count: {_chainLightningCount}");
        sb.AppendLine($"Total Discharges: {_totalDischarges}");
        sb.AppendLine($"Total Energy: {_totalEnergyDischarged:F2}");
        sb.AppendLine($"Particles: {_plasmaParticles.Count}");
        return sb.ToString();
    }

    public (double plasmaEnergy, double coreTemp, int dischargeCount, int particleCount) GetStatus()
    { return (_plasmaEnergy, _coreTemperature, _totalDischarges, _plasmaParticles.Count); }

    #endregion

    #region Public API

    public double GetPlasmaEnergy() => _plasmaEnergy;
    public double GetCoreTemperature() => _coreTemperature;
    public int GetDischargeCount() => _totalDischarges;
    public int GetParticleCount() => _plasmaParticles.Count;
    public bool IsDischarging() => _isDischarging;
    public double GetFieldRadius() => GetEffectiveFieldRadius();
    public double GetDischargeCooldown() => _dischargeCooldownTimer;

    #endregion

    #region Serialization

    public string SerializeState()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"PlasmaEnergy:{_plasmaEnergy}");
        sb.AppendLine($"CoreTemperature:{_coreTemperature}");
        sb.AppendLine($"FieldPhase:{_fieldFluctuationPhase}");
        sb.AppendLine($"Cooldown:{_dischargeCooldownTimer}");
        sb.AppendLine($"ChainCount:{_chainLightningCount}");
        sb.AppendLine($"TotalEnergy:{_totalEnergyDischarged}");
        sb.AppendLine($"TotalDischarges:{_totalDischarges}");
        sb.AppendLine($"IsDischarging:{_isDischarging}");
        return sb.ToString();
    }

    public void DeserializeState(string state)
    {
        if (string.IsNullOrEmpty(state)) return;
        var lines = state.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(':');
            if (parts.Length != 2) continue;
            try
            {
                switch (parts[0])
                {
                    case "PlasmaEnergy": _plasmaEnergy = double.Parse(parts[1]); break;
                    case "CoreTemperature": _coreTemperature = double.Parse(parts[1]); break;
                    case "FieldPhase": _fieldFluctuationPhase = double.Parse(parts[1]); break;
                    case "Cooldown": _dischargeCooldownTimer = double.Parse(parts[1]); break;
                    case "ChainCount": _chainLightningCount = int.Parse(parts[1]); break;
                    case "TotalEnergy": _totalEnergyDischarged = double.Parse(parts[1]); break;
                    case "TotalDischarges": _totalDischarges = int.Parse(parts[1]); break;
                    case "IsDischarging": _isDischarging = bool.Parse(parts[1]); break;
                }
            }
            catch { }
        }
    }

    #endregion
}

internal static class StaticRandom
{
    private static Random rng = new Random();
    public static double NextDouble() => rng.NextDouble();
    public static double NextDouble(double min, double max) => min + (max - min) * rng.NextDouble();
    public static int Next(int min, int max) => rng.Next(min, max);
    public static int Next(int max) => rng.Next(max);
}

internal static class Vector2Extensions
{
    public static Vector2 Lerp(Vector2 a, Vector2 b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return new Vector2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }
}
