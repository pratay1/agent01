using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Diagnostics;
using System.Linq;

namespace PhysicsSandbox.Behaviors;

public class GlueBehavior : BodyBehavior
{
    #region Constants & Tunable Parameters

    private const double DEFAULT_ADHESION_STRENGTH = 150.0;
    private const double DEFAULT_MAX_BONDS = 15;
    private const double DEFAULT_GLUE_CONSUMPTION_RATE = 0.1;
    private const double DEFAULT_DRYING_RATE = 0.05;
    private const double DEFAULT_BOND_BREAK_FORCE = 200.0;
    private const double DEFAULT_VISCOSITY = 0.8;
    private const double DEFAULT_INITIAL_GLUE_AMOUNT = 100.0;
    private const double MIN_GLUE_FOR_BOND = 5.0;
    private const double BOND_FORMATION_DISTANCE_FACTOR = 1.2;
    private const double MAX_BOND_DISTANCE = 50.0;
    private const double GLUE_DENSITY = 1.1; // g/cm³, typical for PVA glue
    private const double TEMPERATURE_EFFECT_FACTOR = 0.01;
    private const double SOLVENT_WEAKEN_FACTOR = 0.5;
    private const double BOND_AGE_WEAKEN_FACTOR = 0.001;
    private const double MAX_STATISTICS_ENTRIES = 1000;

    #endregion

    #region Enums

    public enum GlueState
    {
        Fresh,
        Drying,
        Hardened,
        Dissolving,
        Inactive
    }

    public enum AdhesionType
    {
        MechanicalInterlock,
        ChemicalBond,
        VanDerWaals,
        Mixed
    }

    public enum GlueProfileType
    {
        SuperGlue,
        Epoxy,
        RubberCement,
        WhiteGlue,
        HotGlue,
        Tape,
        Custom
    }

    #endregion

    #region Glue Profile System

    public class GlueProfile
    {
        public string Name { get; set; } = "";
        public double AdhesionStrength { get; set; } = 150.0;
        public double DryingTime { get; set; } = 10.0;
        public double Viscosity { get; set; } = 0.8;
        public double MaxBonds { get; set; } = 15;
        public double GlueConsumptionRate { get; set; } = 0.1;
        public double InitialGlueAmount { get; set; } = 100.0;
        public double BondBreakForce { get; set; } = 200.0;
        public AdhesionType AdhesionType { get; set; } = AdhesionType.MechanicalInterlock;
        public string ColorHex { get; set; } = "#76FF03";
        public bool WaterSoluble { get; set; } = false;
        public double TemperatureSensitivity { get; set; } = 1.0;
        public string Description { get; set; } = "";
    }

    private static readonly Dictionary<GlueProfileType, GlueProfile> _glueProfiles = new()
    {
        {
            GlueProfileType.SuperGlue, new GlueProfile
            {
                Name = "Super Glue (Cyanoacrylate)",
                AdhesionStrength = 300.0,
                DryingTime = 0.5,
                Viscosity = 0.3,
                MaxBonds = 20,
                GlueConsumptionRate = 0.15,
                InitialGlueAmount = 80.0,
                BondBreakForce = 400.0,
                AdhesionType = AdhesionType.ChemicalBond,
                ColorHex = "#00BFFF",
                WaterSoluble = false,
                TemperatureSensitivity = 1.2,
                Description = "Fast-setting, strong chemical bond. Bonds almost instantly on contact."
            }
        },
        {
            GlueProfileType.Epoxy, new GlueProfile
            {
                Name = "Epoxy",
                AdhesionStrength = 500.0,
                DryingTime = 30.0,
                Viscosity = 0.9,
                MaxBonds = 10,
                GlueConsumptionRate = 0.25,
                InitialGlueAmount = 150.0,
                BondBreakForce = 600.0,
                AdhesionType = AdhesionType.Mixed,
                ColorHex = "#DAA520",
                WaterSoluble = false,
                TemperatureSensitivity = 0.8,
                Description = "Two-part adhesive with exceptional strength. Takes time to cure fully."
            }
        },
        {
            GlueProfileType.RubberCement, new GlueProfile
            {
                Name = "Rubber Cement",
                AdhesionStrength = 80.0,
                DryingTime = 2.0,
                Viscosity = 0.6,
                MaxBonds = 25,
                GlueConsumptionRate = 0.08,
                InitialGlueAmount = 120.0,
                BondBreakForce = 150.0,
                AdhesionType = AdhesionType.MechanicalInterlock,
                ColorHex = "#808080",
                WaterSoluble = true,
                TemperatureSensitivity = 1.0,
                Description = "Flexible bond, repositionable when fresh. dissolves with solvent."
            }
        },
        {
            GlueProfileType.WhiteGlue, new GlueProfile
            {
                Name = "White Glue (PVA)",
                AdhesionStrength = 120.0,
                DryingTime = 15.0,
                Viscosity = 0.7,
                MaxBonds = 18,
                GlueConsumptionRate = 0.12,
                InitialGlueAmount = 100.0,
                BondBreakForce = 200.0,
                AdhesionType = AdhesionType.MechanicalInterlock,
                ColorHex = "#F5F5DC",
                WaterSoluble = true,
                TemperatureSensitivity = 1.0,
                Description = "All-purpose craft glue. Bonds porous materials well."
            }
        },
        {
            GlueProfileType.HotGlue, new GlueProfile
            {
                Name = "Hot Glue",
                AdhesionStrength = 200.0,
                DryingTime = 0.1,
                Viscosity = 0.2,
                MaxBonds = 12,
                GlueConsumptionRate = 0.18,
                InitialGlueAmount = 90.0,
                BondBreakForce = 300.0,
                AdhesionType = AdhesionType.MechanicalInterlock,
                ColorHex = "#FFD700",
                WaterSoluble = false,
                TemperatureSensitivity = 1.5,
                Description = "Thermoplastic adhesive. Sets quickly but softens with heat."
            }
        },
        {
            GlueProfileType.Tape, new GlueProfile
            {
                Name = "Tape Adhesive",
                AdhesionStrength = 60.0,
                DryingTime = 1.0,
                Viscosity = 0.95,
                MaxBonds = 30,
                GlueConsumptionRate = 0.05,
                InitialGlueAmount = 200.0,
                BondBreakForce = 100.0,
                AdhesionType = AdhesionType.VanDerWaals,
                ColorHex = "#C0C0C0",
                WaterSoluble = false,
                TemperatureSensitivity = 0.7,
                Description = "Pressure-sensitive adhesive. Moderate strength, removable."
            }
        }
    };

    #endregion

    #region Bond Data Structure

    private class BondInfo
    {
        public int OtherBodyId { get; set; }
        public double Strength { get; set; }
        public double Age { get; set; }
        public double GlueUsed { get; set; }
        public Vector2 LastRelativePosition { get; set; }
        public double DryingProgress { get; set; }
        public bool IsBreaking { get; set; }
        public double BreakProgress { get; set; }

        public BondInfo(int otherId, double initialStrength, double glueAmount)
        {
            OtherBodyId = otherId;
            Strength = initialStrength;
            Age = 0.0;
            GlueUsed = glueAmount;
            DryingProgress = 0.0;
            IsBreaking = false;
            BreakProgress = 0.0;
        }

        public double GetEffectiveStrength(double temperatureFactor = 1.0)
        {
            double ageFactor = Math.Max(0.1, 1.0 - (Age * BOND_AGE_WEAKEN_FACTOR));
            double dryFactor = DryingProgress > 1.0 ? 1.2 : 0.5 + (DryingProgress * 0.7);
            return Strength * ageFactor * dryFactor * temperatureFactor;
        }
    }

    #endregion

    #region Instance State & Configuration

    private GlueProfileType _currentProfile = GlueProfileType.WhiteGlue;
    private GlueProfile _activeProfile = _glueProfiles[GlueProfileType.WhiteGlue];

    private double _adhesionStrengthOverride = -1;
    private double _glueAmountRemaining = DEFAULT_INITIAL_GLUE_AMOUNT;
    private readonly Dictionary<int, BondInfo> _activeBonds = new();
    private PhysicsWorld? _physicsWorld;
    private GlueState _currentState = GlueState.Fresh;
    private double _dryingTimer = 0.0;
    private double _totalGlueConsumed = 0.0;
    private int _bondsFormed = 0;
    private int _bondsBroken = 0;

    private bool _enableAutoBonding = true;
    private bool _enableBondDecay = true;
    private bool _enableTemperatureEffects = true;
    private double _environmentalTemperature = 20.0;
    private bool _isInVacuum = false;

    private Vector2 _lastPosition;
    private readonly Stopwatch _updateStopwatch = new();
    private readonly Queue<double> _glueConsumptionHistory = new();
    private double _peakBondCount = 0;
    private double _totalBondDuration = 0.0;

    private string? _debugLabel = null;

    #endregion

    #region Behavior Properties (Overrides)

    public override BodyType Type => BodyType.Glue;
    public override string Name => "Glue";
    public override string Description => "Sticky behavior that bonds with other bodies upon contact";
    public override string ColorHex => _activeProfile.ColorHex;
    public override double DefaultRadius => 17;
    public override double DefaultMass => 12;
    public override double DefaultRestitution => 0.02;

    #endregion

    #region Constructors & Initialization

    public GlueBehavior() : this(GlueProfileType.WhiteGlue) { }

    public GlueBehavior(GlueProfileType profile)
    {
        _currentProfile = profile;
        _activeProfile = _glueProfiles[profile];
        _glueAmountRemaining = _activeProfile.InitialGlueAmount;
    }

    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);

        _lastPosition = body.Position;
        _currentState = GlueState.Fresh;

        LogDebug(body, $"GlueBehavior initialized: Profile={_currentProfile}, InitialGlue={_glueAmountRemaining:F1}, MaxBonds={_activeProfile.MaxBonds}");
    }

    #endregion

    #region Main Update Loop

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        _physicsWorld = world;
        _updateStopwatch.Restart();

        try
        {
            if (body.IsStatic || body.IsFrozen || _glueAmountRemaining <= 0)
                return;

            RaisePreUpdate(body, dt);

            UpdateGlueState(dt);
            UpdateDryingProcess(body, dt);
            MaintainActiveBonds(body, dt);
            ProcessBondBreakage(body, dt);
            ConsumeGlueOverTime(dt);
            CheckEnvironmentalEffects(dt);

            if (_enableAutoBonding && _currentState != GlueState.Dissolving)
            {
                AttemptNewBonds(body, world);
            }

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

    #region Glue State & Drying

    private void UpdateGlueState(double dt)
    {
        _dryingTimer += dt;

        if (_currentState == GlueState.Fresh && _dryingTimer > _activeProfile.DryingTime * 0.3)
        {
            _currentState = GlueState.Drying;
        }
        else if (_currentState == GlueState.Drying && _dryingTimer >= _activeProfile.DryingTime)
        {
            _currentState = GlueState.Hardened;
        }
    }

    private void UpdateDryingProcess(RigidBody body, double dt)
    {
        if (_currentState == GlueState.Drying || _currentState == GlueState.Dissolving)
        {
            double dryingRate = _currentState == GlueState.Drying ? DEFAULT_DRYING_RATE : -DEFAULT_DRYING_RATE * 2;
            foreach (var bond in _activeBonds.Values)
            {
                bond.DryingProgress = Math.Clamp(bond.DryingProgress + dryingRate * dt, 0.0, 1.5);
            }
        }
    }

    private void CheckEnvironmentalEffects(double dt)
    {
        if (!_enableTemperatureEffects) return;

        double tempDiff = _environmentalTemperature - 20.0;
        double tempFactor = 1.0 + (tempDiff * TEMPERATURE_EFFECT_FACTOR * _activeProfile.TemperatureSensitivity);

        if (_environmentalTemperature > 40 && _activeProfile.WaterSoluble)
        {
            _currentState = GlueState.Dissolving;
        }
    }

    #endregion

    #region Bond Formation

    private void AttemptNewBonds(RigidBody body, PhysicsWorld world)
    {
        if (_activeBonds.Count >= _activeProfile.MaxBonds) return;
        if (_glueAmountRemaining < MIN_GLUE_FOR_BOND) return;

        var nearbyBodies = SpatialQuery(body.Position, MAX_BOND_DISTANCE, world)
            .Where(b => b != body && !b.IsStatic && !_activeBonds.ContainsKey(b.Id))
            .ToList();

        foreach (var other in nearbyBodies)
        {
            if (_activeBonds.Count >= _activeProfile.MaxBonds) break;
            if (_glueAmountRemaining < MIN_GLUE_FOR_BOND) break;

            double dist = Vector2.Distance(body.Position, other.Position);
            double bondDistance = (body.Radius + other.Radius) * BOND_FORMATION_DISTANCE_FACTOR;

            if (dist <= bondDistance)
            {
                FormBond(body, other);
            }
        }
    }

    private void FormBond(RigidBody body, RigidBody other)
    {
        double glueUsed = _activeProfile.GlueConsumptionRate * 5;
        if (_glueAmountRemaining < glueUsed) return;

        double bondStrength = _activeProfile.AdhesionStrength * (1.0 - (_activeBonds.Count / (double)_activeProfile.MaxBonds * 0.3));
        var bond = new BondInfo(other.Id, bondStrength, glueUsed);
        bond.LastRelativePosition = other.Position - body.Position;

        _activeBonds[other.Id] = bond;
        _glueAmountRemaining -= glueUsed;
        _totalGlueConsumed += glueUsed;
        _bondsFormed++;

        body.IsStuck = true;
        other.IsStuck = true;

        LogDebug(body, $"Bond formed with body {other.Id}. Strength={bondStrength:F1}, Glue remaining={_glueAmountRemaining:F1}");
    }

    #endregion

    #region Bond Maintenance & Forces

    private void MaintainActiveBonds(RigidBody body, double dt)
    {
        var bondsToRemove = new List<int>();

        foreach (var kvp in _activeBonds)
        {
            int otherId = kvp.Key;
            BondInfo bond = kvp.Value;

            bond.Age += dt;

            if (bond.DryingProgress < 1.0)
            {
                bond.DryingProgress = Math.Min(1.0, bond.DryingProgress + (_activeProfile.DryingTime > 0 ? dt / _activeProfile.DryingTime : 0));
            }

            // Apply bonding force
            RigidBody? otherBody = FindBodyById(otherId);
            if (otherBody == null || otherBody.IsFrozen)
            {
                bondsToRemove.Add(otherId);
                continue;
            }

            Vector2 desiredOffset = bond.LastRelativePosition;
            Vector2 currentOffset = otherBody.Position - body.Position;
            Vector2 error = desiredOffset - currentOffset;

            if (error.LengthSquared > 1.0)
            {
                double effectiveStrength = bond.GetEffectiveStrength(GetTemperatureFactor());
                Vector2 correctionForce = error * (effectiveStrength * 0.5);

                body.ApplyForce(correctionForce * body.Mass * dt);
                otherBody.ApplyForce(-correctionForce * otherBody.Mass * dt);
            }
        }

        foreach (int id in bondsToRemove)
        {
            _activeBonds.Remove(id);
        }
    }

    private void ProcessBondBreakage(RigidBody body, double dt)
    {
        var bondsToBreak = new List<int>();

        foreach (var kvp in _activeBonds)
        {
            int otherId = kvp.Key;
            BondInfo bond = kvp.Value;

            if (bond.IsBreaking)
            {
                bond.BreakProgress += dt * 2;
                if (bond.BreakProgress >= 1.0)
                {
                    bondsToBreak.Add(otherId);
                }
                continue;
            }

            RigidBody? otherBody = FindBodyById(otherId);
            if (otherBody == null) continue;

            Vector2 relativeVel = otherBody.Velocity - body.Velocity;
            double separationSpeed = Vector2.Dot(relativeVel, (otherBody.Position - body.Position).Normalized);

            if (separationSpeed > 0)
            {
                double forceMagnitude = separationSpeed * (body.Mass * otherBody.Mass) / (body.Mass + otherBody.Mass);
                double threshold = bond.GetEffectiveStrength(GetTemperatureFactor()) * 3;

                if (forceMagnitude > threshold)
                {
                    bond.IsBreaking = true;
                    bond.BreakProgress = 0.0;
                }
            }
        }

        foreach (int id in bondsToBreak)
        {
            BreakBond(body, id);
        }
    }

    private void BreakBond(RigidBody body, int otherId)
    {
        if (_activeBonds.TryGetValue(otherId, out BondInfo bond))
        {
            _activeBonds.Remove(otherId);
            _bondsBroken++;
            _totalBondDuration += bond.Age;

            LogDebug(body, $"Bond with body {otherId} broke. Age={bond.Age:F2}s, Strength={bond.Strength:F1}");
        }
    }

    #endregion

    #region Collision Handling

    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        if (_currentState == GlueState.Dissolving) return;
        if (other.IsStatic) return;
        if (!_enableAutoBonding) return;

        double impactSpeed = Vector2.Distance(body.Velocity, other.Velocity);
        if (impactSpeed > 50)
        {
            TryCreateBondOnImpact(body, other, impactSpeed);
        }

        RaiseCollision(body, other);
    }

    private void TryCreateBondOnImpact(RigidBody body, RigidBody other, double impactSpeed)
    {
        if (_activeBonds.ContainsKey(other.Id)) return;
        if (_activeBonds.Count >= _activeProfile.MaxBonds) return;
        if (_glueAmountRemaining < MIN_GLUE_FOR_BOND) return;

        double requiredGlue = _activeProfile.GlueConsumptionRate * (impactSpeed / 50.0);
        if (_glueAmountRemaining < requiredGlue) return;

        double bondStrength = _activeProfile.AdhesionStrength * Math.Min(1.0, impactSpeed / 100.0);
        var bond = new BondInfo(other.Id, bondStrength, requiredGlue);
        bond.DryingProgress = 0.3;

        _activeBonds[other.Id] = bond;
        _glueAmountRemaining -= requiredGlue;
        _totalGlueConsumed += requiredGlue;
        _bondsFormed++;

        LogDebug(body, $"Impact bond created with body {other.Id}. ImpactSpeed={impactSpeed:F1}, Strength={bondStrength:F1}");
    }

    #endregion

    #region Glue Consumption & Replenishment

    private void ConsumeGlueOverTime(double dt)
    {
        if (_glueAmountRemaining <= 0) return;

        double consumption = _activeProfile.GlueConsumptionRate * _activeBonds.Count * dt;
        _glueAmountRemaining -= consumption;

        if (_glueAmountRemaining <= 0)
        {
            _glueAmountRemaining = 0;
            _currentState = GlueState.Inactive;
            LogDebug(null, "Glue depleted. Behavior inactive.");
        }

        _glueConsumptionHistory.Enqueue(consumption);
        if (_glueConsumptionHistory.Count > 100) _glueConsumptionHistory.Dequeue();
    }

    public void AddGlue(double amount)
    {
        _glueAmountRemaining += amount;
        if (_currentState == GlueState.Inactive && _glueAmountRemaining > MIN_GLUE_FOR_BOND)
        {
            _currentState = GlueState.Fresh;
            _dryingTimer = 0.0;
        }
        LogDebug(null, $"Added {amount:F1} glue. Total={_glueAmountRemaining:F1}");
    }

    public void DissolveGlue(double amount)
    {
        _glueAmountRemaining = Math.Max(0, _glueAmountRemaining - amount);
        if (_glueAmountRemaining < _activeProfile.InitialGlueAmount * 0.2)
        {
            _currentState = GlueState.Dissolving;
        }
    }

    #endregion

    #region Statistics Tracking

    private void TrackStatistics(RigidBody body, double dt)
    {
        if (_activeBonds.Count > _peakBondCount)
        {
            _peakBondCount = _activeBonds.Count;
        }
    }

    public (int ActiveBonds, int TotalFormed, int TotalBroken, double GlueRemaining, double PeakBonds) GetStatistics()
    {
        return (_activeBonds.Count, _bondsFormed, _bondsBroken, _glueAmountRemaining, _peakBondCount);
    }

    public double GetAverageBondDuration()
    {
        int completedBonds = _bondsBroken;
        return completedBonds > 0 ? _totalBondDuration / completedBonds : 0.0;
    }

    public double GetGlueEfficiency()
    {
        return _bondsFormed > 0 ? _totalGlueConsumed / _bondsFormed : 0.0;
    }

    #endregion

    #region Public API for Runtime Modification

    public void SetProfile(GlueProfileType profile)
    {
        _currentProfile = profile;
        _activeProfile = _glueProfiles[profile];
        _glueAmountRemaining = _activeProfile.InitialGlueAmount;
        _currentState = GlueState.Fresh;
        _dryingTimer = 0.0;
    }

    public void SetCustomProfile(GlueProfile profile)
    {
        _currentProfile = GlueProfileType.Custom;
        _activeProfile = profile;
        _glueProfiles[GlueProfileType.Custom] = profile;
    }

    public void SetAdhesionStrength(double strength)
    {
        _adhesionStrengthOverride = Math.Max(0, strength);
    }

    public void SetAutoBonding(bool enabled) => _enableAutoBonding = enabled;
    public void SetBondDecay(bool enabled) => _enableBondDecay = enabled;
    public void SetTemperatureEffects(bool enabled) => _enableTemperatureEffects = enabled;

    public void SetEnvironmentalTemperature(double temp)
    {
        _environmentalTemperature = temp;
    }

    public void BreakAllBonds()
    {
        var bondIds = _activeBonds.Keys.ToList();
        foreach (int id in bondIds)
        {
            _activeBonds.Remove(id);
            _bondsBroken++;
        }
    }

    public bool IsBodyStuck(int bodyId)
    {
        return _activeBonds.ContainsKey(bodyId);
    }

    public IReadOnlyList<int> GetStuckBodyIds()
    {
        return _activeBonds.Keys.ToList();
    }

    public GlueState GetCurrentState() => _currentState;
    public double GetGlueAmountRemaining() => _glueAmountRemaining;
    public double GetDryingProgress() => _activeBonds.Values.Any() ? _activeBonds.Values.Average(b => b.DryingProgress) : 0.0;

    #endregion

    #region Collision & Contact Management (Additional Helpers)

    private RigidBody? FindBodyById(int id)
    {
        return _physicsWorld?.Bodies.FirstOrDefault(b => b.Id == id);
    }

    private bool IsBondValid(int otherId, out BondInfo? bond)
    {
        if (_activeBonds.TryGetValue(otherId, out bond))
        {
            return bond.DryingProgress > 0.1;
        }
        return false;
    }

    #endregion

    #region Serialization Support

    public string SerializeState()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Profile:{_currentProfile}");
        sb.AppendLine($"GlueAmount:{_glueAmountRemaining}");
        sb.AppendLine($"State:{_currentState}");
        sb.AppendLine($"DryingTimer:{_dryingTimer}");
        sb.AppendLine($"TotalConsumed:{_totalGlueConsumed}");
        sb.AppendLine($"BondsFormed:{_bondsFormed}");
        sb.AppendLine($"BondsBroken:{_bondsBroken}");
        sb.AppendLine($"ActiveBonds:{_activeBonds.Count}");
        sb.AppendLine($"Temperature:{_environmentalTemperature}");
        sb.AppendLine($"AutoBonding:{_enableAutoBonding}");
        sb.AppendLine($"BondDecay:{_enableBondDecay}");

        foreach (var kvp in _activeBonds)
        {
            sb.AppendLine($"Bond:{kvp.Key}:{kvp.Value.Strength}:{kvp.Value.Age}:{kvp.Value.DryingProgress}");
        }

        return sb.ToString();
    }

    public void DeserializeState(string state)
    {
        _activeBonds.Clear();

        var lines = state.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            try
            {
                switch (parts[0])
                {
                    case "Profile":
                        if (Enum.TryParse(parts[1], out GlueProfileType profile))
                        {
                            _currentProfile = profile;
                            _activeProfile = _glueProfiles.GetValueOrDefault(profile, _glueProfiles[GlueProfileType.WhiteGlue]);
                        }
                        break;
                    case "GlueAmount":
                        _glueAmountRemaining = double.Parse(parts[1]);
                        break;
                    case "State":
                        if (Enum.TryParse(parts[1], out GlueState parsedState))
                            _currentState = parsedState;
                        break;
                    case "DryingTimer":
                        _dryingTimer = double.Parse(parts[1]);
                        break;
                    case "TotalConsumed":
                        _totalGlueConsumed = double.Parse(parts[1]);
                        break;
                    case "BondsFormed":
                        _bondsFormed = int.Parse(parts[1]);
                        break;
                    case "BondsBroken":
                        _bondsBroken = int.Parse(parts[1]);
                        break;
                    case "ActiveBonds":
                        int count = int.Parse(parts[1]);
                        break;
                    case "Temperature":
                        _environmentalTemperature = double.Parse(parts[1]);
                        break;
                    case "AutoBonding":
                        _enableAutoBonding = bool.Parse(parts[1]);
                        break;
                    case "BondDecay":
                        _enableBondDecay = bool.Parse(parts[1]);
                        break;
                    case "Bond":
                        if (parts.Length >= 5 && int.TryParse(parts[1], out int bodyId))
                        {
                            double strength = double.Parse(parts[2]);
                            double age = double.Parse(parts[3]);
                            double drying = double.Parse(parts[4]);
                            var bond = new BondInfo(bodyId, strength, 0) { Age = age, DryingProgress = drying };
                            _activeBonds[bodyId] = bond;
                        }
                        break;
                }
            }
            catch { /* Ignore parse errors */ }
        }
    }

    #endregion

    #region Debug Visualization Support

    protected override void RenderDebugOverlay(RigidBody body, DrawingContext dc)
    {
        if (dc == null || !GlobalConfig.EnableDebugVisualization) return;

        DrawBondConnections(body, dc);
        DrawGlueAmountIndicator(body, dc);
        DrawStateIndicator(body, dc);
    }

    private void DrawBondConnections(RigidBody body, System.Windows.Media.DrawingContext dc)
    {
        foreach (var kvp in _activeBonds)
        {
            RigidBody? other = FindBodyById(kvp.Key);
            if (other == null) continue;

            BondInfo bond = kvp.Value;
            double opacity = Math.Min(1.0, bond.DryingProgress);
            var color = Color.FromArgb((byte)(opacity * 200), 117, 255, 3);
            var brush = new SolidColorBrush(color);
            var pen = new System.Windows.Media.Pen(brush, 2.0);

            dc.DrawLine(pen,
                new System.Windows.Point(body.Position.X, body.Position.Y),
                new System.Windows.Point(other.Position.X, other.Position.Y));
        }
    }

    private void DrawGlueAmountIndicator(RigidBody body, System.Windows.Media.DrawingContext dc)
    {
        double percentage = _glueAmountRemaining / _activeProfile.InitialGlueAmount;
        double width = 30 * percentage;
        var rect = new System.Windows.Rect(body.Position.X - 15, body.Position.Y - body.Radius - 25, 30, 5);

        dc.DrawRectangle(Brushes.Gray, null, rect);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(255, 117, 255, 3)), null,
            new System.Windows.Rect(rect.X, rect.Y, width, rect.Height));
    }

    private void DrawStateIndicator(RigidBody body, System.Windows.Media.DrawingContext dc)
    {
        var stateBrush = _currentState switch
        {
            GlueState.Fresh => Brushes.LightGreen,
            GlueState.Drying => Brushes.Yellow,
            GlueState.Hardened => Brushes.DarkGreen,
            GlueState.Dissolving => Brushes.Orange,
            _ => Brushes.Gray
        };

        dc.DrawEllipse(stateBrush, new System.Windows.Media.Pen(Brushes.Black, 1),
            new System.Windows.Point(body.Position.X, body.Position.Y), 4, 4);
    }

    #endregion

    #region Utility & Helper Methods

    private double GetTemperatureFactor()
    {
        if (!_enableTemperatureEffects) return 1.0;
        double deviation = _environmentalTemperature - 20.0;
        return 1.0 + (deviation * TEMPERATURE_EFFECT_FACTOR * _activeProfile.TemperatureSensitivity);
    }

    public double GetBondStrength(int otherId)
    {
        return _activeBonds.TryGetValue(otherId, out var bond) ? bond.GetEffectiveStrength(GetTemperatureFactor()) : 0.0;
    }

    public int GetBondCount() => _activeBonds.Count;
    public GlueProfile GetActiveProfile() => _activeProfile;
    public GlueState GetGlueState() => _currentState;

    private double CalculateMaxPossibleBonds()
    {
        return Math.Floor(_glueAmountRemaining / _activeProfile.GlueConsumptionRate);
    }

    #endregion

    #region Performance Tracking Overrides

    protected override void RaisePostUpdate(RigidBody body, double dt)
    {
        base.RaisePostUpdate(body, dt);
        // Additional per-frame cleanup
    }

    #endregion

    #region Extended Serialization (Detailed State)

    public string SerializeDetailedState()
    {
        var sb = new StringBuilder();
        sb.AppendLine(SerializeState());
        sb.AppendLine($"--- Bond Details ---");
        foreach (var kvp in _activeBonds)
        {
            var b = kvp.Value;
            sb.AppendLine($"Bond {kvp.Key}: Strength={b.Strength:F1}, Age={b.Age:F2}s, Dry={b.DryingProgress:P0}");
        }
        return sb.ToString();
    }

    public void LoadBondData(List<(int bodyId, double strength, double age)> bonds)
    {
        _activeBonds.Clear();
        foreach (var (bodyId, strength, age) in bonds)
        {
            _activeBonds[bodyId] = new BondInfo(bodyId, strength, 0) { Age = age, DryingProgress = 1.0 };
        }
    }

    #endregion

    #region Diagnostics

    public string GetDiagnosticsReport(RigidBody body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== GlueBehavior Diagnostics for Body {body.Id} ===");
        sb.AppendLine($"Glue State: {_currentState}");
        sb.AppendLine($"Glue Remaining: {_glueAmountRemaining:F1}/{_activeProfile.InitialGlueAmount:F1} ({_glueAmountRemaining / _activeProfile.InitialGlueAmount:P0})");
        sb.AppendLine($"Active Bonds: {_activeBonds.Count}/{_activeProfile.MaxBonds}");
        sb.AppendLine($"Drying Progress: {GetDryingProgress():P0}");
        sb.AppendLine($"Environmental Temp: {_environmentalTemperature:F1}C");
        sb.AppendLine($"Bonds Formed: {_bondsFormed}, Broken: {_bondsBroken}");
        sb.AppendLine($"Total Glue Consumed: {_totalGlueConsumed:F1}");
        sb.AppendLine($"Temperature Factor: {GetTemperatureFactor():F3}");
        sb.AppendLine($"Auto-bonding: {_enableAutoBonding}");
        return sb.ToString();
    }

    #endregion
}
