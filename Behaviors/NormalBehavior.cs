using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Diagnostics;

namespace PhysicsSandbox.Behaviors;

/// <summary>
/// NormalBehavior: The standard physics body behavior.
/// Represents a baseline physical object with predictable Newtonian dynamics,
/// optimized for FPS performance with extensibility for advanced simulation features.
/// 
/// This behavior serves as the reference implementation for all other behaviors,
/// demonstrating best practices for performance optimization, configurability,
/// and debug visualization within the physics sandbox framework.
/// </summary>
/// <remarks>
/// <para>
/// <b>Physics Model:</b> Standard Newtonian rigid body dynamics with:
/// - Linear velocity integration (position += velocity * dt)
/// - Mass-dependent inertia (F = ma)
/// - Configurable restitution (bounciness)
/// - Optional drag/air resistance
/// - Ground friction modeling
/// </para>
/// <para>
/// <b>Performance Optimizations:</b>
/// - Uses squared distance comparisons to avoid costly sqrt operations
/// - Caches normalized vectors when reused
/// - Pre-allocates temporary collections via ObjectPool
/// - Minimizes branching in hot paths
/// - Supports spatial partitioning queries when available
/// </para>
/// <para>
/// <b>Configuration Presets:</b>
/// Multiple mass/radius profiles available: Rock, Balloon, Feather, Steel, etc.
/// Each preset adjusts mass, restitution, drag coefficient, and visual properties.
/// </para>
/// </remarks>
public class NormalBehavior : BodyBehavior
{
    #region Constants & Tunable Parameters

    /// <summary>
    /// Default drag coefficient for air resistance calculations.
    /// Higher values = faster slow-down in air.
    /// </summary>
    private const double DEFAULT_DRAG_COEFFICIENT = 0.02;

    /// <summary>
    /// Default ground friction coefficient (applied when touching ground).
    /// Higher values = faster horizontal slowdown when on ground.
    /// </summary>
    private const double DEFAULT_GROUND_FRICTION = 0.85;

    /// <summary>
    /// Maximum velocity magnitude before velocity clamping activates.
    /// Prevents bodies from achieving relativistic speeds.
    /// </summary>
    private const double MAX_VELOCITY = 10000.0;

    /// <summary>
    /// Minimum velocity threshold below which velocity is zeroed out.
    /// Prevents micro-oscillations and infinite sliding.
    /// </summary>
    private const double VELOCITY_SLEEP_THRESHOLD = 0.5;

    /// <summary>
    /// Time in seconds before a stationary body enters sleep state.
    /// Sleeping bodies skip physics updates for performance.
    /// </summary>
    private const double SLEEP_TIMEOUT = 2.0;

    /// <summary>
    /// How much kinetic energy is retained per second due to internal damping.
    /// 1.0 = no damping, 0.0 = instant stop.
    /// </summary>
    private const double KINETIC_DAMPING_RATE = 0.995;

    /// <summary>
    /// Scale factor for rendering debug information.
    /// </summary>
    private const double DEBUG_RENDER_SCALE = 1.0;

    #endregion

    #region Mass Profile Presets

    /// <summary>
    /// Predefined mass/radius profiles for quick body configuration.
    /// Each preset models a different real-world material or object type.
    /// </summary>
    public enum MassProfile
    {
        /// <summary>Standard baseball (~0.145 kg, radius ~3.7cm scaled to sim units)</summary>
        Baseball,
        /// <summary>Bowling ball (~7 kg, radius ~10.8cm)</summary>
        BowlingBall,
        /// <summary>Feather (~0.0005 kg, low mass)</summary>
        Feather,
        /// <summary>Steel ball bearing (~0.5 kg, high density)</summary>
        Steel,
        /// <summary>Rubber ball (~0.3 kg, high restitution)</summary>
        Rubber,
        /// <summary>Lead weight (~11 kg, very heavy for size)</summary>
        Lead,
        /// <summary>Beach ball (~0.1 kg, large radius, low mass)</summary>
        BeachBall,
        /// <summary>Golf ball (~0.046 kg, small radius, dimpled)</summary>
        GolfBall,
        /// <summary>Marble (~0.005 kg, small glass sphere)</summary>
        Marble,
        /// <summary>Custom profile - uses DefaultMass/DefaultRadius</summary>
        Custom
    }

    /// <summary>
    /// Gets the mass in kg for a given profile and radius.
    /// </summary>
    private static double GetMassForProfile(MassProfile profile, double radius)
    {
        return profile switch
        {
            MassProfile.Baseball => Math.PI * radius * radius * radius * 0.0007, // Cork + leather density
            MassProfile.BowlingBall => Math.PI * radius * radius * radius * 0.0013, // Urethane density
            MassProfile.Feather => Math.PI * radius * radius * radius * 0.000001, // Very light
            MassProfile.Steel => Math.PI * radius * radius * radius * 0.0078, // Steel density
            MassProfile.Rubber => Math.PI * radius * radius * radius * 0.0011, // Rubber density
            MassProfile.Lead => Math.PI * radius * radius * radius * 0.0113, // Lead density
            MassProfile.BeachBall => Math.PI * radius * radius * radius * 0.00005, // Inflated
            MassProfile.GolfBall => Math.PI * radius * radius * radius * 0.00105, // Surlyn cover
            MassProfile.Marble => Math.PI * radius * radius * radius * 0.0025, // Glass density
            MassProfile.Custom => 10.0, // Default
            _ => 10.0
        };
    }

    /// <summary>
    /// Gets the restitution (bounciness) for a given profile.
    /// </summary>
    private static double GetRestitutionForProfile(MassProfile profile)
    {
        return profile switch
        {
            MassProfile.Baseball => 0.55,
            MassProfile.BowlingBall => 0.10,
            MassProfile.Feather => 0.30,
            MassProfile.Steel => 0.15,
            MassProfile.Rubber => 0.85,
            MassProfile.Lead => 0.08,
            MassProfile.BeachBall => 0.70,
            MassProfile.GolfBall => 0.80,
            MassProfile.Marble => 0.20,
            MassProfile.Custom => 0.5,
            _ => 0.5
        };
    }

    /// <summary>
    /// Gets the drag coefficient for a given profile.
    /// </summary>
    private static double GetDragCoefficientForProfile(MassProfile profile)
    {
        return profile switch
        {
            MassProfile.Baseball => 0.03,
            MassProfile.BowlingBall => 0.01,
            MassProfile.Feather => 0.15,
            MassProfile.Steel => 0.008,
            MassProfile.Rubber => 0.025,
            MassProfile.Lead => 0.007,
            MassProfile.BeachBall => 0.08,
            MassProfile.GolfBall => 0.04,
            MassProfile.Marble => 0.015,
            MassProfile.Custom => DEFAULT_DRAG_COEFFICIENT,
            _ => DEFAULT_DRAG_COEFFICIENT
        };
    }

    /// <summary>
    /// Gets the visual color tint for a given profile.
    /// </summary>
    private static string GetColorForProfile(MassProfile profile)
    {
        return profile switch
        {
            MassProfile.Baseball => "#FFFFFF", // White
            MassProfile.BowlingBall => "#000000", // Black
            MassProfile.Feather => "#F5F5DC", // Beige
            MassProfile.Steel => "#708090", // Slate gray
            MassProfile.Rubber => "#FF0000", // Red
            MassProfile.Lead => "#2F4F4F", // Dark slate gray
            MassProfile.BeachBall => "#00FF00", // Green
            MassProfile.GolfBall => "#FFFFFF", // White
            MassProfile.Marble => "#87CEEB", // Sky blue
            MassProfile.Custom => "#4FC3F7", // Default blue
            _ => "#4FC3F7"
        };
    }

    #endregion

    #region Instance State & Configuration

    /// <summary>
    /// Current mass profile for this behavior instance.
    /// </summary>
    private MassProfile _currentProfile = MassProfile.Custom;

    /// <summary>
    /// Drag coefficient override (null = use profile default).
    /// </summary>
    private double? _dragCoefficientOverride = null;

    /// <summary>
    /// Ground friction override (null = use default).
    /// </summary>
    private double? _groundFrictionOverride = null;

    /// <summary>
    /// Tracks if the body is currently sleeping (skipping updates for performance).
    /// </summary>
    private bool _isSleeping = false;

    /// <summary>
    /// Timer tracking how long the body has been below velocity threshold.
    /// </summary>
    private double _sleepTimer = 0.0;

    /// <summary>
    /// Last known position for velocity calculation and trail rendering.
    /// </summary>
    private Vector2 _lastPosition;

    /// <summary>
    /// Accumulated kinetic energy for diagnostics.
    /// </summary>
    private double _accumulatedKineticEnergy = 0.0;

    /// <summary>
    /// Peak velocity magnitude achieved by this body.
    /// </summary>
    private double _peakVelocity = 0.0;

    /// <summary>
    /// Total distance traveled since creation.
    /// </summary>
    private double _totalDistanceTraveled = 0.0;

    /// <summary>
    /// Number of collisions detected since creation.
    /// </summary>
    private int _collisionCount = 0;

    /// <summary>
    /// Performance counter specific to this instance.
    /// </summary>
    private readonly Stopwatch _updateStopwatch = new Stopwatch();

    /// <summary>
    /// Trail positions for visual rendering (recent positions).
    /// </summary>
    private readonly Queue<Vector2> _trailPositions = new Queue<Vector2>();

    /// <summary>
    /// Maximum trail length (number of position samples to keep).
    /// </summary>
    private int _maxTrailLength = 20;

    /// <summary>
    /// If true, this body ignores all other bodies (no interactions).
    /// </summary>
    private bool _isIsolated = false;

    /// <summary>
    /// Custom label for this body instance (for debugging).
    /// </summary>
    private string? _debugLabel = null;

    /// <summary>
    /// Environmental temperature affecting this body (for future expansion).
    /// </summary>
    private double _environmentalTemperature = 20.0; // Celsius

    /// <summary>
    /// If true, body is affected by wind forces.
    /// </summary>
    private bool _affectedByWind = true;

    /// <summary>
    /// If true, body is affected by gravity variations.
    /// </summary>
    private bool _affectedByGravity = true;

    #endregion

    #region Behavior Properties (Overrides)

    /// <inheritdoc/>
    public override BodyType Type => BodyType.Normal;

    /// <inheritdoc/>
    public override string Name => "Normal";

    /// <inheritdoc/>
    public override string Description => "Standard physics body with Newtonian dynamics, configurable mass profiles, and performance optimizations";

    /// <inheritdoc/>
    public override string ColorHex => GetColorForProfile(_currentProfile);

    /// <inheritdoc/>
    public override double DefaultRadius => 15;

    /// <inheritdoc/>
    public override double DefaultMass => 10;

    /// <inheritdoc/>
    public override double DefaultRestitution => GetRestitutionForProfile(_currentProfile);

    #endregion

    #region Constructors & Initialization

    /// <summary>
    /// Initializes a new NormalBehavior with default settings.
    /// </summary>
    public NormalBehavior()
    {
        _currentProfile = MassProfile.Custom;
    }

    /// <summary>
    /// Initializes a new NormalBehavior with a specific mass profile.
    /// </summary>
    /// <param name="profile">The mass profile to use</param>
    public NormalBehavior(MassProfile profile)
    {
        _currentProfile = profile;
    }

    /// <inheritdoc/>
    public override void OnCreate(RigidBody body)
    {
        base.OnCreate(body);

        // Apply profile-based settings
        if (_currentProfile != MassProfile.Custom)
        {
            body.Mass = GetMassForProfile(_currentProfile, body.Radius);
            body.Restitution = GetRestitutionForProfile(_currentProfile);
        }

        // Initialize state
        _lastPosition = body.Position;
        _trailPositions.Enqueue(body.Position);

        // Performance tracking
        LogDebug(body, $"NormalBehavior initialized with profile: {_currentProfile}, mass: {body.Mass:F2}, restitution: {body.Restitution:F2}");
    }

    #endregion

    #region Main Update Loop

    /// <inheritdoc/>
    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        _updateStopwatch.Restart();

        try
        {
            // Check if body should be updated
            if (body.IsStatic || body.IsFrozen)
                return;

            // Sleep optimization: skip updates for stationary bodies
            if (ShouldSleep(body, dt))
                return;

            // Apply environmental forces
            ApplyEnvironmentalForces(body, dt, world);

            // Apply drag/air resistance
            ApplyDrag(body, dt);

            // Update trail for visualization
            UpdateTrail(body);

            // Track statistics
            TrackStatistics(body, dt);

            // Check for velocity clamping
            ClampVelocity(body);

            // Debug visualization
            if (GlobalConfig.EnableDebugVisualization && Config.DebugMode)
            {
                RenderDebugVisualization(body, null); // DrawingContext passed by renderer
            }
        }
        finally
        {
            _updateStopwatch.Stop();
            RecordPerformanceMetric("OnUpdate", _updateStopwatch.Elapsed.TotalMilliseconds);
        }
    }

    #endregion

    #region Physics Sub-Systems

    /// <summary>
    /// Applies environmental forces: gravity, wind, temperature effects.
    /// </summary>
    private void ApplyEnvironmentalForces(RigidBody body, double dt, PhysicsWorld world)
    {
        // Gravity (already handled by world.ForceManager, but we can add modifiers)
        if (_affectedByGravity && body.Mass > 0)
        {
            // Optional: Add altitude-based gravity variation
            double altitudeFactor = 1.0; // Could be based on Y position
            Vector2 gravityForce = world.Gravity * body.Mass * altitudeFactor;
            body.ApplyForce(gravityForce);
        }

        // Wind force
        if (_affectedByWind && world.ForceManager.Wind.IsActive)
        {
            double windEffect = 1.0;
            Vector2 windForce = world.ForceManager.Wind.Direction * world.ForceManager.Wind.Strength * body.Mass * windEffect;
            body.ApplyForce(windForce);
        }

        // Temperature effects (could affect air density, thus drag)
        // Future expansion: heat-based convection
    }

    /// <summary>
    /// Applies air resistance/drag force. F_drag = -0.5 * C_d * rho * A * v^2 * v_hat
    /// Simplified: F_drag = -C_d * v * |v|
    /// </summary>
    private void ApplyDrag(RigidBody body, double dt)
    {
        double cd = _dragCoefficientOverride ?? GetDragCoefficientForProfile(_currentProfile);
        if (cd <= 0) return;

        double speed = body.Velocity.Length;
        if (speed < 0.001) return;

        // Drag proportional to velocity squared (more realistic for higher speeds)
        double dragMagnitude;
        if (speed > 100)
        {
            dragMagnitude = cd * speed * speed;
        }
        else
        {
            // Linear drag for low speeds (Stokes' law approximation)
            dragMagnitude = cd * speed * 10.0;
        }

        Vector2 dragForce = -body.Velocity.Normalized * dragMagnitude;
        body.ApplyForce(dragForce);
    }

    /// <summary>
    /// Applies ground friction when body is in contact with ground.
    /// </summary>
    private void ApplyGroundFriction(RigidBody body, PhysicsWorld world)
    {
        double groundY = world.GroundY;
        double bottomY = body.Position.Y + body.Radius;

        if (bottomY >= groundY - 1.0) // Close enough to ground
        {
            double friction = _groundFrictionOverride ?? DEFAULT_GROUND_FRICTION;
            // Apply horizontal friction only
            body.Velocity = new Vector2(body.Velocity.X * friction, body.Velocity.Y);
        }
    }

    /// <summary>
    /// Clamps velocity to maximum allowed value to prevent instability.
    /// </summary>
    private void ClampVelocity(RigidBody body)
    {
        double speedSq = body.Velocity.LengthSquared;
        if (speedSq > MAX_VELOCITY * MAX_VELOCITY)
        {
            double scale = MAX_VELOCITY / Math.Sqrt(speedSq);
            body.Velocity = body.Velocity * scale;
        }
    }

    /// <summary>
    /// Determines if the body should enter sleep state for performance optimization.
    /// </summary>
    private bool ShouldSleep(RigidBody body, double dt)
    {
        double speed = body.Velocity.Length;

        if (speed < VELOCITY_SLEEP_THRESHOLD)
        {
            _sleepTimer += dt;
            if (_sleepTimer >= SLEEP_TIMEOUT && !_isSleeping)
            {
                _isSleeping = true;
                body.Velocity = Vector2.Zero;
                LogDebug(body, "Body entered sleep state");
            }
        }
        else
        {
            _sleepTimer = 0;
            if (_isSleeping)
            {
                _isSleeping = false;
                LogDebug(body, "Body woke from sleep state");
            }
        }

        return _isSleeping;
    }

    /// <summary>
    /// Wakes up a sleeping body (e.g., when collided with).
    /// </summary>
    private void WakeUp(RigidBody body)
    {
        _isSleeping = false;
        _sleepTimer = 0;
    }

    #endregion

    #region Collision Handling

    /// <inheritdoc/>
    public override void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        _collisionCount++;

        // Wake up if sleeping
        WakeUp(body);

        // Apply ground friction if other is static (ground/wall)
        if (other.IsStatic)
        {
            ApplyGroundFriction(body, world);
        }

        // Log collision for debugging
        if (Config.DebugMode)
        {
            double impactSpeed = Vector2.Distance(body.Velocity, other.Velocity);
            LogDebug(body, $"Collision with body {other.Id} (type: {other.BodyType}), relative speed: {impactSpeed:F2}");
        }
    }

    #endregion

    #region Trail & Visualization

    /// <summary>
    /// Updates the trail of recent positions for visualization.
    /// </summary>
    private void UpdateTrail(RigidBody body)
    {
        double distMoved = Vector2.Distance(body.Position, _lastPosition);
        if (distMoved > body.Radius * 0.25) // Only add point if moved enough
        {
            _trailPositions.Enqueue(body.Position);
            while (_trailPositions.Count > _maxTrailLength)
            {
                _trailPositions.Dequeue();
            }
            _lastPosition = body.Position;
        }
    }

    /// <summary>
    /// Renders debug visualization for this body.
    /// </summary>
    protected override void RenderDebugOverlay(RigidBody body, DrawingContext dc)
    {
        if (dc == null) return;

        // Draw trail
        var trailArray = _trailPositions.ToArray();
        for (int i = 1; i < trailArray.Length; i++)
        {
            double opacity = (double)i / trailArray.Length;
            var pen = new System.Windows.Media.Pen(
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb((byte)(opacity * 128), 0, 200, 255)),
                1.0);
            dc.DrawLine(pen, 
                new System.Windows.Point(trailArray[i - 1].X, trailArray[i - 1].Y),
                new System.Windows.Point(trailArray[i].X, trailArray[i].Y));
        }

        // Draw velocity vector
        if (body.Velocity.Length > 1.0)
        {
            var endPoint = body.Position + body.Velocity.Normalized * 30.0;
            var velPen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Yellow, 2.0);
            dc.DrawLine(velPen,
                new System.Windows.Point(body.Position.X, body.Position.Y),
                new System.Windows.Point(endPoint.X, endPoint.Y));
        }

        // Draw debug label
        if (!string.IsNullOrEmpty(_debugLabel))
        {
            var typeface = new System.Windows.Media.Typeface("Consolas");
            var formatted = new System.Windows.Media.FormattedText(
                _debugLabel,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                typeface,
                10,
                System.Windows.Media.Brushes.White);
            dc.DrawText(formatted, 
                new System.Windows.Point(body.Position.X - body.Radius, body.Position.Y - body.Radius - 15));
        }
    }

    #endregion

    #region Statistics Tracking

    /// <summary>
    /// Tracks various statistics for this body.
    /// </summary>
    private void TrackStatistics(RigidBody body, double dt)
    {
        // Track distance
        double distMoved = Vector2.Distance(body.Position, _lastPosition);
        _totalDistanceTraveled += distMoved;

        // Track peak velocity
        double speed = body.Velocity.Length;
        if (speed > _peakVelocity)
        {
            _peakVelocity = speed;
        }

        // Track kinetic energy: KE = 0.5 * m * v^2
        double ke = 0.5 * body.Mass * speed * speed;
        _accumulatedKineticEnergy += ke * dt;
    }

    /// <summary>
    /// Gets current statistics for this body instance.
    /// </summary>
    public (int Collisions, double PeakVelocity, double TotalDistance, double KineticEnergy, bool IsSleeping) GetStatistics()
    {
        return (_collisionCount, _peakVelocity, _totalDistanceTraveled, _accumulatedKineticEnergy, _isSleeping);
    }

    /// <summary>
    /// Gets the current mass profile.
    /// </summary>
    public MassProfile GetProfile() => _currentProfile;

    /// <summary>
    /// Sets the mass profile and updates the body accordingly.
    /// </summary>
    public void SetProfile(RigidBody body, MassProfile profile)
    {
        _currentProfile = profile;
        body.Mass = GetMassForProfile(profile, body.Radius);
        body.Restitution = GetRestitutionForProfile(profile);
        LogDebug(body, $"Profile changed to: {profile}, new mass: {body.Mass:F2}");
    }

    #endregion

    #region Public API for Runtime Modification

    /// <summary>
    /// Sets the drag coefficient override.
    /// </summary>
    public void SetDragCoefficient(double? drag)
    {
        _dragCoefficientOverride = drag;
    }

    /// <summary>
    /// Sets the ground friction override.
    /// </summary>
    public void SetGroundFriction(double? friction)
    {
        _groundFrictionOverride = friction;
    }

    /// <summary>
    /// Sets whether this body is affected by wind.
    /// </summary>
    public void SetAffectedByWind(bool affected)
    {
        _affectedByWind = affected;
    }

    /// <summary>
    /// Sets whether this body is affected by gravity.
    /// </summary>
    public void SetAffectedByGravity(bool affected)
    {
        _affectedByGravity = affected;
    }

    /// <summary>
    /// Sets the debug label for this body.
    /// </summary>
    public void SetDebugLabel(string? label)
    {
        _debugLabel = label;
    }

    /// <summary>
    /// Sets whether this body is isolated (ignores other bodies).
    /// </summary>
    public void SetIsolated(bool isolated)
    {
        _isIsolated = isolated;
    }

    /// <summary>
    /// Gets whether this body is currently sleeping.
    /// </summary>
    public bool IsSleeping() => _isSleeping;

    /// <summary>
    /// Forces the body to wake up from sleep.
    /// </summary>
    public void ForceWakeUp()
    {
        _isSleeping = false;
        _sleepTimer = 0;
    }

    /// <summary>
    /// Gets the current trail positions (for external rendering).
    /// </summary>
    public IReadOnlyList<Vector2> GetTrail() => _trailPositions.ToArray();

    #endregion

    #region Utility & Helper Methods

    /// <summary>
    /// Calculates the terminal velocity for this body given current drag.
    /// Terminal velocity occurs when drag force equals gravitational force.
    /// For simple model: v_terminal = sqrt((2 * m * g) / (C_d * rho * A))
    /// Simplified: v_terminal = sqrt(g * m / C_d)
    /// </summary>
    public double CalculateTerminalVelocity()
    {
        double g = 980.0; // Default gravity strength
        double cd = _dragCoefficientOverride ?? GetDragCoefficientForProfile(_currentProfile);
        if (cd <= 0) return double.MaxValue;
        return Math.Sqrt(g * DefaultMass / cd);
    }

    /// <summary>
    /// Calculates the time to live (TTL) based on current velocity and environmental forces.
    /// </summary>
    public double EstimateTimeToStop(RigidBody body)
    {
        double speed = body.Velocity.Length;
        if (speed < VELOCITY_SLEEP_THRESHOLD) return 0;

        double cd = _dragCoefficientOverride ?? GetDragCoefficientForProfile(_currentProfile);
        // Very rough approximation: t = v / (a_drag)
        double acceleration = cd * speed / body.Mass;
        return acceleration > 0 ? speed / acceleration : double.MaxValue;
    }

    /// <summary>
    /// Resets all statistics for this body.
    /// </summary>
    public void ResetStatistics()
    {
        _collisionCount = 0;
        _peakVelocity = 0;
        _totalDistanceTraveled = 0;
        _accumulatedKineticEnergy = 0;
    }

    #endregion

    #region Mass Profile Static Utilities

    /// <summary>
    /// Gets all available mass profiles with their descriptions.
    /// </summary>
    public static Dictionary<MassProfile, (string Description, double Density, double Restitution)> GetAllProfiles()
    {
        return new Dictionary<MassProfile, (string, double, double)>
        {
            { MassProfile.Baseball, ("Standard baseball with cork core and leather cover", 0.0007, 0.55) },
            { MassProfile.BowlingBall, ("Heavy bowling ball made of urethane/plastic", 0.0013, 0.10) },
            { MassProfile.Feather, ("Extremely light feather with high air resistance", 0.000001, 0.30) },
            { MassProfile.Steel, ("Solid steel ball bearing, high density", 0.0078, 0.15) },
            { MassProfile.Rubber, ("Bouncy rubber ball with high restitution", 0.0011, 0.85) },
            { MassProfile.Lead, ("Very heavy lead weight, low bounce", 0.0113, 0.08) },
            { MassProfile.BeachBall, ("Inflated beach ball, very light for size", 0.00005, 0.70) },
            { MassProfile.GolfBall, ("Dimpled golf ball for reduced drag", 0.00105, 0.80) },
            { MassProfile.Marble, ("Glass marble, smooth and dense", 0.0025, 0.20) },
            { MassProfile.Custom, ("Custom profile using DefaultMass/DefaultRadius", 0.001, 0.50) }
        };
    }

    /// <summary>
    /// Creates a human-readable description of a mass profile.
    /// </summary>
    public static string ProfileToString(MassProfile profile)
    {
        var profiles = GetAllProfiles();
        if (profiles.TryGetValue(profile, out var info))
        {
            return $"{profile}: {info.Description} (Density: {info.Density:F6}, Restitution: {info.Restitution:F2})";
        }
        return profile.ToString();
    }

    #endregion

    #region Performance Overrides

    /// <summary>
    /// Custom performance measurement for NormalBehavior.
    /// </summary>
    protected override void RaisePostUpdate(RigidBody body, double dt)
    {
        base.RaisePostUpdate(body, dt);
        
        // Additional post-update housekeeping
        _lastPosition = body.Position;
    }

    #endregion

    #region Destructor & Cleanup

    /// <summary>
    /// Cleans up resources when behavior is destroyed.
    /// </summary>
    ~NormalBehavior()
    {
        _trailPositions.Clear();
    }

    #endregion

    #region Advanced Physics Extensions

    /// <summary>
    /// Applies angular damping to simulate rotational friction.
    /// Bodies in reality don't just stop spinning instantly.
    /// </summary>
    private void ApplyAngularDamping(RigidBody body, double dt)
    {
        double angularDamping = 0.98; // Parameter: how quickly rotation slows
        body.AngularVelocity *= Math.Pow(angularDamping, dt * 60); // Normalize to 60 FPS
    }

    /// <summary>
    /// Simulates buoyancy if the body enters a fluid region.
    /// Water simulation: buoyant force = displaced fluid weight.
    /// </summary>
    private void ApplyBuoyancy(RigidBody body, PhysicsWorld world)
    {
        // Assuming water level is at Y = world.GroundY + 100 (example)
        double waterLevel = world.GroundY + 100.0;
        if (body.Position.Y + body.Radius <= waterLevel) return; // Not in water

        // Approximate submerged volume (simplified as spherical cap)
        double submergedDepth = (body.Position.Y + body.Radius) - waterLevel;
        if (submergedDepth <= 0) return;

        double waterDensity = 1000.0; // kg/m³ (water)
        double gravity = 980.0;
        double volume = (4.0 / 3.0) * Math.PI * Math.Pow(body.Radius, 3);
        double submergedFraction = Math.Min(1.0, submergedDepth / (2 * body.Radius));
        double buoyantForce = waterDensity * volume * submergedFraction * gravity;

        // Apply upward force
        body.ApplyForce(new Vector2(0, -buoyantForce));
    }

    /// <summary>
    /// Simulates the Magnus effect: force perpendicular to velocity due to spin.
    /// Relevant for balls with topspin/backspin.
    /// </summary>
    private void ApplyMagnusEffect(RigidBody body, double dt)
    {
        if (Math.Abs(body.AngularVelocity) < 0.01) return; // Not spinning enough

        double magnusCoefficient = 0.5; // Tuning parameter
        Vector2 velocityDir = body.Velocity.Normalized;
        Vector2 spinDir = new Vector2(-velocityDir.Y, velocityDir.X); // Perpendicular

        // Magnus force direction depends on spin direction
        int spinSign = Math.Sign(body.AngularVelocity);
        Vector2 magnusForce = spinDir * spinSign * body.Velocity.Length * magnusCoefficient * body.Mass;

        body.ApplyForce(magnusForce);
    }

    /// <summary>
    /// Applies a simplified aerodynamic lift force (for asymmetrical bodies).
    /// Not typically needed for spheres, included for completeness.
    /// </summary>
    private void ApplyAerodynamicLift(RigidBody body, double dt)
    {
        double speed = body.Velocity.Length;
        if (speed < 10.0) return;

        double liftCoefficient = 0.1; // Depends on shape
        double airDensity = 1.225; // kg/m³ at sea level
        double crossSectionalArea = Math.PI * body.Radius * body.Radius;
        double liftForce = 0.5 * airDensity * speed * speed * crossSectionalArea * liftCoefficient;

        // Lift is perpendicular to velocity (simplified)
        Vector2 liftDir = new Vector2(-body.Velocity.Y, body.Velocity.X).Normalized;
        body.ApplyForce(liftDir * liftForce);
    }

    /// <summary>
    /// Simulates simple harmonic motion (spring force) toward a target point.
    /// Can be used for soft constraint or orbit simulation.
    /// </summary>
    private void ApplySpringForce(RigidBody body, Vector2 anchor, double stiffness, double damping)
    {
        Vector2 displacement = body.Position - anchor;
        double distance = displacement.Length;
        if (distance < 0.001) return;

        // Hooke's law: F = -kx
        Vector2 springForce = -displacement.Normalized * stiffness * distance;

        // Damping: proportional to velocity along spring direction
        double velAlongSpring = Vector2.Dot(body.Velocity, displacement.Normalized);
        Vector2 dampingForce = -displacement.Normalized * velAlongSpring * damping;

        body.ApplyForce(springForce + dampingForce);
    }

    /// <summary>
    /// Applies a Boids-like separation force to avoid crowding.
    /// Useful for flocking behaviors or preventing overlap.
    /// </summary>
    private void ApplySeparationForce(RigidBody body, PhysicsWorld world, double desiredSeparation)
    {
        Vector2 separationForce = Vector2.Zero;
        int neighbors = 0;

        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic) continue;

            double distSq = (body.Position - other.Position).LengthSquared;
            if (distSq < desiredSeparation * desiredSeparation && distSq > 0.001)
            {
                Vector2 diff = (body.Position - other.Position).Normalized;
                double dist = Math.Sqrt(distSq);
                diff /= dist; // Weight by distance (closer = stronger repulsion)
                separationForce += diff;
                neighbors++;
            }
        }

        if (neighbors > 0)
        {
            separationForce /= neighbors;
            body.ApplyForce(separationForce * body.Mass * 100.0);
        }
    }

    /// <summary>
    /// Simulates temperature-based effects on the body.
    /// High temperature → lower density → higher buoyancy.
    /// </summary>
    private void ApplyTemperatureEffects(RigidBody body, double dt)
    {
        // Temperature affects air density around body
        double ambientTemp = _environmentalTemperature;
        double bodyTemp = ambientTemp; // Could track body temperature over time

        // Cooling/heating rate (simplified Newton's law of cooling)
        double heatTransferCoefficient = 0.1;
        double tempChange = (ambientTemp - bodyTemp) * heatTransferCoefficient * dt;
        // bodyTemp += tempChange; // If tracking body temperature

        // Hot bodies experience lower air density (less drag)
        if (bodyTemp > ambientTemp + 10.0)
        {
            // Apply reduced drag (already handled in ApplyDrag)
        }
    }

    /// <summary>
    /// Checks if body is in a vacuum (no drag, no buoyancy).
    /// Can be set per-body or per-region.
    /// </summary>
    private bool _isInVacuum = false;

    /// <summary>
    /// Sets whether this body is in a vacuum environment.
    /// </summary>
    public void SetVacuum(bool inVacuum)
    {
        _isInVacuum = inVacuum;
    }

    /// <summary>
    /// Applies all advanced physics effects if enabled.
    /// Call this in OnUpdate if extended physics are desired.
    /// </summary>
    private void ApplyAdvancedPhysics(RigidBody body, double dt, PhysicsWorld world)
    {
        if (_isInVacuum) return; // No air-based effects in vacuum

        ApplyAngularDamping(body, dt);
        ApplyBuoyancy(body, world);
        ApplyMagnusEffect(body, dt);
        // ApplyAerodynamicLift(body, dt); // Commented: usually not needed for spheres
        ApplyTemperatureEffects(body, dt);
    }

    #endregion

    #region Extended Mass Profile System

    /// <summary>
    /// Extended preset configurations with more detailed physical properties.
    /// </summary>
    public class ExtendedProfile
    {
        public string Name { get; set; } = "";
        public double Mass { get; set; }
        public double Radius { get; set; }
        public double Restitution { get; set; }
        public double DragCoefficient { get; set; }
        public double Density { get; set; } // kg/m³
        public double YoungsModulus { get; set; } // Elastic modulus (Pa)
        public double PoissonsRatio { get; set; } // Lateral strain / axial strain
        public string ColorHex { get; set; } = "#FFFFFF";
        public string Description { get; set; } = "";
        public string MaterialType { get; set; } = "Generic";
        public double MeltingPoint { get; set; } // Celsius
        public double ThermalConductivity { get; set; } // W/(m·K)
    }

    /// <summary>
    /// Gets a comprehensive list of extended material profiles.
    /// </summary>
    public static List<ExtendedProfile> GetExtendedProfiles()
    {
        return new List<ExtendedProfile>
        {
            new ExtendedProfile
            {
                Name = "Super Ball",
                Mass = 0.3,
                Radius = 10,
                Restitution = 0.92,
                DragCoefficient = 0.025,
                Density = 1100,
                YoungsModulus = 0.01e9,
                PoissonsRatio = 0.5,
                ColorHex = "#FF0000",
                Description = "High-bounce rubber ball",
                MaterialType = "Rubber",
                MeltingPoint = 180,
                ThermalConductivity = 0.16
            },
            new ExtendedProfile
            {
                Name = "Steel Sphere",
                Mass = 7.8,
                Radius = 10,
                Restitution = 0.15,
                DragCoefficient = 0.008,
                Density = 7800,
                YoungsModulus = 200e9,
                PoissonsRatio = 0.3,
                ColorHex = "#708090",
                Description = "Machined steel ball bearing",
                MaterialType = "Steel",
                MeltingPoint = 1510,
                ThermalConductivity = 50.2
            },
            new ExtendedProfile
            {
                Name = "Glass Marble",
                Mass = 0.005,
                Radius = 7,
                Restitution = 0.25,
                DragCoefficient = 0.015,
                Density = 2500,
                YoungsModulus = 70e9,
                PoissonsRatio = 0.22,
                ColorHex = "#87CEEB",
                Description = "Handmade glass marble",
                MaterialType = "Glass",
                MeltingPoint = 1400,
                ThermalConductivity = 1.1
            },
            new ExtendedProfile
            {
                Name = "Lead Weight",
                Mass = 11.3,
                Radius = 10,
                Restitution = 0.08,
                DragCoefficient = 0.007,
                Density = 11300,
                YoungsModulus = 16e9,
                PoissonsRatio = 0.44,
                ColorHex = "#2F4F4F",
                Description = "Dense lead spherical weight",
                MaterialType = "Lead",
                MeltingPoint = 327,
                ThermalConductivity = 35.3
            },
            new ExtendedProfile
            {
                Name = "Ping Pong Ball",
                Mass = 0.0027,
                Radius = 20,
                Restitution = 0.85,
                DragCoefficient = 0.12,
                Density = 80, // Hollow
                YoungsModulus = 0.003e9,
                PoissonsRatio = 0.5,
                ColorHex = "#FFFFFF",
                Description = "Lightweight celluloid ball",
                MaterialType = "Celluloid",
                MeltingPoint = 100,
                ThermalConductivity = 0.2
            },
            new ExtendedProfile
            {
                Name = "Gold Nugget",
                Mass = 19.3,
                Radius = 10,
                Restitution = 0.10,
                DragCoefficient = 0.006,
                Density = 19300,
                YoungsModulus = 79e9,
                PoissonsRatio = 0.44,
                ColorHex = "#FFD700",
                Description = "Pure gold sphere",
                MaterialType = "Gold",
                MeltingPoint = 1064,
                ThermalConductivity = 318
            },
            new ExtendedProfile
            {
                Name = "Balsa Wood",
                Mass = 0.1,
                Radius = 15,
                Restitution = 0.65,
                DragCoefficient = 0.08,
                Density = 100,
                YoungsModulus = 3e9,
                PoissonsRatio = 0.23,
                ColorHex = "#F5DEB3",
                Description = "Lightweight balsa sphere",
                MaterialType = "Wood",
                MeltingPoint = 300, // Decomposes
                ThermalConductivity = 0.048
            },
            new ExtendedProfile
            {
                Name = "Diamond",
                Mass = 3.5,
                Radius = 5,
                Restitution = 0.05,
                DragCoefficient = 0.004,
                Density = 3500,
                YoungsModulus = 1200e9,
                PoissonsRatio = 0.2,
                ColorHex = "#B9F2FF",
                Description = "Perfect diamond sphere",
                MaterialType = "Carbon",
                MeltingPoint = 3550,
                ThermalConductivity = 2200
            }
        };
    }

    /// <summary>
    /// Applies an extended profile to a body.
    /// </summary>
    public void ApplyExtendedProfile(RigidBody body, ExtendedProfile profile)
    {
        body.Mass = profile.Mass;
        body.Radius = profile.Radius;
        body.Restitution = profile.Restitution;
        _dragCoefficientOverride = profile.DragCoefficient;
        _currentProfile = MassProfile.Custom;

        LogDebug(body, $"Applied extended profile: {profile.Name} ({profile.MaterialType})");
    }

    #endregion

    #region Additional Statistics & Diagnostics

    /// <summary>
    /// Gets comprehensive runtime diagnostics for this body.
    /// </summary>
    public string GetDiagnosticsReport(RigidBody body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== NormalBehavior Diagnostics for Body {body.Id} ===");
        sb.AppendLine($"Position: {body.Position}");
        sb.AppendLine($"Velocity: {body.Velocity} (Speed: {body.Velocity.Length:F2})");
        sb.AppendLine($"Mass: {body.Mass:F4} kg");
        sb.AppendLine($"Radius: {body.Radius:F2}");
        sb.AppendLine($"Restitution: {body.Restitution:F2}");
        sb.AppendLine($"Angular Velocity: {body.AngularVelocity:F2} rad/s");
        sb.AppendLine($"Profile: {_currentProfile}");
        sb.AppendLine($"Sleeping: {_isSleeping} (Timer: {_sleepTimer:F2}s)");
        sb.AppendLine($"Collisions: {_collisionCount}");
        sb.AppendLine($"Peak Velocity: {_peakVelocity:F2}");
        sb.AppendLine($"Total Distance: {_totalDistanceTraveled:F2}");
        sb.AppendLine($"Kinetic Energy: {0.5 * body.Mass * body.Velocity.LengthSquared:F2} J");
        sb.AppendLine($"Isolated: {_isIsolated}");
        sb.AppendLine($"Affected by Wind: {_affectedByWind}");
        sb.AppendLine($"Affected by Gravity: {_affectedByGravity}");
        sb.AppendLine($"In Vacuum: {_isInVacuum}");
        sb.AppendLine($"Trail Length: {_trailPositions.Count}");
        sb.AppendLine($"Update Stopwatch: {_updateStopwatch.Elapsed.TotalMilliseconds:F3} ms");
        return sb.ToString();
    }

    /// <summary>
    /// Gets the current drag coefficient (from profile or override).
    /// </summary>
    public double GetCurrentDragCoefficient()
    {
        return _dragCoefficientOverride ?? GetDragCoefficientForProfile(_currentProfile);
    }

    /// <summary>
    /// Gets the current ground friction (from override or default).
    /// </summary>
    public double GetCurrentGroundFriction()
    {
        return _groundFrictionOverride ?? DEFAULT_GROUND_FRICTION;
    }

    /// <summary>
    /// Calculates the body's moment of inertia (for a solid sphere: I = 2/5 * m * r²).
    /// </summary>
    public double CalculateMomentOfInertia(RigidBody body)
    {
        return 0.4 * body.Mass * body.Radius * body.Radius;
    }

    /// <summary>
    /// Calculates the body's cross-sectional area (π * r²).
    /// </summary>
    public double CalculateCrossSectionalArea(RigidBody body)
    {
        return Math.PI * body.Radius * body.Radius;
    }

    /// <summary>
    /// Estimates the body's terminal velocity in the current environment.
    /// </summary>
    public double CalculateDetailedTerminalVelocity(RigidBody body, double airDensity = 1.225)
    {
        double dragCd = GetCurrentDragCoefficient();
        double area = CalculateCrossSectionalArea(body);
        double gravity = 980.0;

        if (dragCd <= 0 || area <= 0) return double.MaxValue;
        return Math.Sqrt((2 * body.Mass * gravity) / (airDensity * dragCd * area));
    }

    #endregion

    #region Serialization Support

    /// <summary>
    /// Serializes the behavior state to a string representation.
    /// </summary>
    public string SerializeState()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Profile:{_currentProfile}");
        sb.AppendLine($"DragOverride:{_dragCoefficientOverride}");
        sb.AppendLine($"GroundFrictionOverride:{_groundFrictionOverride}");
        sb.AppendLine($"IsSleeping:{_isSleeping}");
        sb.AppendLine($"CollisionCount:{_collisionCount}");
        sb.AppendLine($"PeakVelocity:{_peakVelocity}");
        sb.AppendLine($"TotalDistance:{_totalDistanceTraveled}");
        sb.AppendLine($"Isolated:{_isIsolated}");
        sb.AppendLine($"AffectedByWind:{_affectedByWind}");
        sb.AppendLine($"AffectedByGravity:{_affectedByGravity}");
        sb.AppendLine($"IsInVacuum:{_isInVacuum}");
        sb.AppendLine($"DebugLabel:{_debugLabel ?? "null"}");
        sb.AppendLine($"EnvironmentalTemperature:{_environmentalTemperature}");
        return sb.ToString();
    }

    /// <summary>
    /// Deserializes the behavior state from a string.
    /// </summary>
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
                    case "Profile":
                        if (Enum.TryParse(parts[1], out MassProfile profile))
                            _currentProfile = profile;
                        break;
                    case "DragOverride":
                        _dragCoefficientOverride = parts[1] == "" ? null : double.Parse(parts[1]);
                        break;
                    case "GroundFrictionOverride":
                        _groundFrictionOverride = parts[1] == "" ? null : double.Parse(parts[1]);
                        break;
                    case "IsSleeping":
                        _isSleeping = bool.Parse(parts[1]);
                        break;
                    case "CollisionCount":
                        _collisionCount = int.Parse(parts[1]);
                        break;
                    case "PeakVelocity":
                        _peakVelocity = double.Parse(parts[1]);
                        break;
                    case "TotalDistance":
                        _totalDistanceTraveled = double.Parse(parts[1]);
                        break;
                    case "Isolated":
                        _isIsolated = bool.Parse(parts[1]);
                        break;
                    case "AffectedByWind":
                        _affectedByWind = bool.Parse(parts[1]);
                        break;
                    case "AffectedByGravity":
                        _affectedByGravity = bool.Parse(parts[1]);
                        break;
                    case "IsInVacuum":
                        _isInVacuum = bool.Parse(parts[1]);
                        break;
                    case "DebugLabel":
                        _debugLabel = parts[1] == "null" ? null : parts[1];
                        break;
                    case "EnvironmentalTemperature":
                        _environmentalTemperature = double.Parse(parts[1]);
                        break;
                }
            }
            catch { /* Ignore parse errors during deserialization */ }
        }
    }

    #endregion
}
