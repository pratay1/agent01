using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;

namespace PhysicsSandbox.Behaviors;

public class GlueBehavior : BodyBehavior
{
    private const double ADHESION_STRENGTH = 150.0;
    private const double MAX_BONDS = 15;
    private const double GLUE_CONSUMPTION = 0.1;
    private const double DRYING_RATE = 0.05;
    private const double BOND_BREAK_FORCE = 200.0;
    private const double MIN_GLUE = 5.0;
    private const double BOND_DISTANCE = 50.0;

    public enum GlueState { Fresh, Drying, Hardened, Dissolving, Inactive }
    public enum GlueType { SuperGlue, Epoxy, RubberCement, WhiteGlue, HotGlue, Tape }

    public class GlueProfile
    {
        public string Name = "";
        public double AdhesionStrength = 150.0;
        public double DryingTime = 10.0;
        public double Viscosity = 0.8;
        public double MaxBonds = 15;
        public double GlueConsumption = 0.1;
        public double BondBreakForce = 200.0;
        public string ColorHex = "#76FF03";
    }

    private static readonly Dictionary<GlueType, GlueProfile> _profiles = new()
    {
        { GlueType.SuperGlue, new() { Name = "Super Glue", AdhesionStrength = 300.0, DryingTime = 0.5, MaxBonds = 20, BondBreakForce = 400.0, ColorHex = "#00BFFF" } },
        { GlueType.Epoxy, new() { Name = "Epoxy", AdhesionStrength = 500.0, DryingTime = 30.0, MaxBonds = 10, BondBreakForce = 600.0, ColorHex = "#DAA520" } },
        { GlueType.RubberCement, new() { Name = "Rubber Cement", AdhesionStrength = 80.0, DryingTime = 2.0, MaxBonds = 25, BondBreakForce = 150.0, ColorHex = "#808080" } },
        { GlueType.WhiteGlue, new() { Name = "White Glue", AdhesionStrength = 120.0, DryingTime = 15.0, MaxBonds = 18, BondBreakForce = 200.0, ColorHex = "#F5F5DC" } },
        { GlueType.HotGlue, new() { Name = "Hot Glue", AdhesionStrength = 200.0, DryingTime = 0.1, MaxBonds = 12, BondBreakForce = 300.0, ColorHex = "#FFD700" } },
        { GlueType.Tape, new() { Name = "Tape", AdhesionStrength = 60.0, DryingTime = 1.0, MaxBonds = 30, BondBreakForce = 100.0, ColorHex = "#C0C0C0" } }
    };

    private class BondInfo
    {
        public int OtherId;
        public double Strength;
        public double Age;
        public double DryingProgress;

        public BondInfo(int id, double strength)
        {
            OtherId = id;
            Strength = strength;
            Age = 0.0;
            DryingProgress = 0.0;
        }
    }

    private GlueType _type = GlueType.WhiteGlue;
    private GlueProfile _profile = _profiles[GlueType.WhiteGlue];
    private double _glueAmount = 100.0;
    private readonly Dictionary<int, BondInfo> _bonds = new();
    private GlueState _state = GlueState.Fresh;
    private double _dryingTimer = 0.0;
    private int _bondsFormed = 0;
    private int _bondsBroken = 0;
    private bool _autoBond = true;

    public override BodyType Type => BodyType.Glue;
    public override string Name => "Glue";
    public override string Description => "Sticks to other bodies";
    public override string ColorHex => _profile.ColorHex;
    public override double DefaultRadius => 17;
    public override double DefaultMass => 12;
    public override double DefaultRestitution => 0.02;

    public GlueBehavior() : this(GlueType.WhiteGlue) { }
    public GlueBehavior(GlueType type)
    {
        _type = type;
        _profile = _profiles[type];
        _glueAmount = _profile.AdhesionStrength > 0 ? 100 : 100;
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);
        _state = GlueState.Fresh;
    }

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.IsStatic || body.IsFrozen || _glueAmount <= 0) return;
        UpdateGlueState(dt);
        MaintainBonds(body, dt);
        ProcessBreakage(body, dt);
        ConsumeGlue(dt);
        if (_autoBond && _state != GlueState.Dissolving)
            AttemptBonds(body, world);
    }

    private void UpdateGlueState(double dt)
    {
        _dryingTimer += dt;
        if (_state == GlueState.Fresh && _dryingTimer > _profile.DryingTime * 0.3)
            _state = GlueState.Drying;
        else if (_state == GlueState.Drying && _dryingTimer >= _profile.DryingTime)
            _state = GlueState.Hardened;
    }

    private void MaintainBonds(RigidBody body, double dt)
    {
        var toRemove = new List<int>();
        foreach (var kvp in _bonds)
        {
            kvp.Value.Age += dt;
            if (kvp.Value.DryingProgress < 1.0)
                kvp.Value.DryingProgress = Math.Min(1.0, kvp.Value.DryingProgress + dt / Math.Max(0.1, _profile.DryingTime));

            var other = FindBody(body, kvp.Key);
            if (other == null) { toRemove.Add(kvp.Key); continue; }

            Vector2 offset = other.Position - body.Position;
            if (offset.LengthSquared > 1.0)
            {
                double strength = kvp.Value.Strength * (1.0 - kvp.Value.Age * 0.001);
                body.ApplyForce(offset * strength * 0.5 * body.Mass * dt);
            }
        }
        foreach (int id in toRemove) _bonds.Remove(id);
    }

    private void ProcessBreakage(RigidBody body, double dt)
    {
        var toBreak = new List<int>();
        foreach (var kvp in _bonds)
        {
            var other = FindBody(body, kvp.Key);
            if (other == null) continue;
            Vector2 relVel = other.Velocity - body.Velocity;
            double separationSpeed = Vector2.Dot(relVel, (other.Position - body.Position).Normalized);
            if (separationSpeed > 0 && separationSpeed * body.Mass > _profile.BondBreakForce)
                toBreak.Add(kvp.Key);
        }
        foreach (int id in toBreak) { _bonds.Remove(id); _bondsBroken++; }
    }

    private void ConsumeGlue(double dt)
    {
        double consumption = GLUE_CONSUMPTION * _bonds.Count * dt;
        _glueAmount -= consumption;
        if (_glueAmount <= 0) { _glueAmount = 0; _state = GlueState.Inactive; }
    }

    private void AttemptBonds(RigidBody body, PhysicsWorld world)
    {
        if (_bonds.Count >= _profile.MaxBonds || _glueAmount < MIN_GLUE) return;
        foreach (var other in SpatialQuery(body.Position, BOND_DISTANCE, world))
        {
            if (_bonds.Count >= _profile.MaxBonds || _glueAmount < MIN_GLUE) break;
            if (other == body || other.IsStatic || _bonds.ContainsKey(other.Id)) continue;
            double dist = Vector2.Distance(body.Position, other.Position);
            if (dist <= (body.Radius + other.Radius) * 1.2)
                FormBond(body, other);
        }
    }

    private void FormBond(RigidBody body, RigidBody other)
    {
        double glueUsed = _profile.GlueConsumption * 5;
        if (_glueAmount < glueUsed) return;
        double strength = _profile.AdhesionStrength * (1.0 - _bonds.Count / _profile.MaxBonds * 0.3);
        _bonds[other.Id] = new BondInfo(other.Id, strength);
        _glueAmount -= glueUsed;
        _bondsFormed++;
        body.IsStuck = true;
        other.IsStuck = true;
    }

    private RigidBody? FindBody(RigidBody body, int id) => body.World?.Bodies.FirstOrDefault(b => b.Id == id);

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        if (_state == GlueState.Dissolving || other.IsStatic || !_autoBond) return;
        if (!_bonds.ContainsKey(other.Id) && _bonds.Count < _profile.MaxBonds && _glueAmount >= MIN_GLUE)
            FormBond(body, other);
    }

    public void SetType(GlueType type)
    {
        _type = type;
        _profile = _profiles[type];
    }

    public void BreakAllBonds()
    {
        var ids = new List<int>(_bonds.Keys);
        foreach (int id in ids) { _bonds.Remove(id); _bondsBroken++; }
    }

    public GlueType GetType() => _type;
    public GlueState GetState() => _state;
    public int GetBondCount() => _bonds.Count;
}