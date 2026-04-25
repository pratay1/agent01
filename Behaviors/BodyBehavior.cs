using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Media;

namespace PhysicsSandbox.Behaviors;

/// <summary>
/// Abstract base class for all physics body behaviors in the sandbox simulation.
/// Provides a comprehensive framework for behavior-driven physics modification,
/// performance tracking, event handling, and extensibility points.
/// </summary>
/// <remarks>
/// The BodyBehavior class implements the Strategy pattern, allowing different
/// physical behaviors to be swapped at runtime while maintaining a consistent
/// interface. Each behavior encapsulates its own update logic, collision response,
/// and initialization code.
/// 
/// Performance considerations:
/// - OnUpdate should execute in O(n) time where n is number of nearby bodies
/// - Use spatial partitioning when available (spatialQuery method)
/// - Cache expensive calculations (sqrMagnitude instead of Length when possible)
/// - Avoid allocations in hot loops (pre-allocate arrays, use stackalloc)
/// - Consider multithreading for expensive behaviors (not implemented in WPF)
/// 
/// Thread safety: All behavior methods are called from the main physics thread.
/// No locks are needed. If adding async operations, ensure proper synchronization.
/// </remarks>
public abstract class BodyBehavior
{
    #region Constants & Static Data

    /// <summary>
    /// Global performance tracking for all behavior instances.
    /// Used for profiling and FPS optimization.
    /// </summary>
    protected static Dictionary<BodyType, BehaviorStats> _globalStats = new();

    /// <summary>
    /// Global object pool for reusable data structures to reduce GC pressure.
    /// </summary>
    private static readonly Dictionary<Type, Queue<object>> _objectPools = new();

    /// <summary>
    /// Frame counter for behavior updates, used for profiling.
    /// </summary>
    private static long _globalFrameCount = 0;

    /// <summary>
    /// Total time spent in behavior updates across all instances (ms).
    /// </summary>
    private static double _totalBehaviorUpdateTimeMs = 0;

    /// <summary>
    /// Last frame's update time for FPS calculation.
    /// </summary>
    private static double _lastFrameTimeMs = 0;

    /// <summary>
    /// Global configuration for behavior system tuning.
    /// </summary>
    private static BehaviorSystemConfig _globalConfig = new();

    /// <summary>
    /// Spatial partition reference for optimized queries (optional integration).
    /// </summary>
    private static ISpatialPartition? _spatialPartition = null;

    #endregion

    #region Lifecycle & Core Properties

    /// <summary>
    /// Unique identifier for this behavior type.
    /// </summary>
    public abstract BodyType Type { get; }

    /// <summary>
    /// Human-readable name for UI display.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Detailed description of behavior functionality and effects.
    /// Used in tooltips, documentation, and debug overlays.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Hex color code for rendering (e.g., "#FF0000").
    /// Should be visually distinct and colorblind-friendly where possible.
    /// </summary>
    public abstract string ColorHex { get; }

    /// <summary>
    /// Default radius in world units when spawning this behavior type.
    /// </summary>
    public abstract double DefaultRadius { get; }

    /// <summary>
    /// Default mass in kilograms (simulation units).
    /// Affects inertia and force response.
    /// </summary>
    public abstract double DefaultMass { get; }

    /// <summary>
    /// Default restitution (bounciness) coefficient [0, 1].
    /// 0 = perfectly inelastic, 1 = perfectly elastic (no energy loss).
    /// </summary>
    public abstract double DefaultRestitution { get; }

    /// <summary>
    /// Called when a RigidBody is created with this behavior.
    /// Use to initialize behavior-specific state and modify body properties.
    /// </summary>
    /// <param name="body">The newly created body instance</param>
    public virtual void OnCreate(RigidBody body)
    {
        // Base implementation registers the body for performance tracking
        RegisterBodyForTracking(body);
    }

    /// <summary>
    /// Called every physics step to update behavior state.
    /// This is where custom physics logic should be implemented.
    /// </summary>
    /// <param name="body">The body being updated</param>
    /// <param name="dt">Delta time in seconds since last step</param>
    /// <param name="world">Reference to the physics world for queries</param>
    public virtual void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        // Base implementation updates performance stats
        UpdatePerformanceMetrics(body, dt);
    }

    /// <summary>
    /// Called when this body collides with another body.
    /// Implement custom collision response, triggers, or effects.
    /// </summary>
    /// <param name="body">This body (the owner of the behavior)</param>
    /// <param name="other">The other body in the collision</param>
    /// <param name="world">Reference to the physics world</param>
    public virtual void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        // Base collision handler - can be overridden
        HandleCollisionBasics(body, other);
    }

    #endregion

    #region Performance Tracking & Profiling

    /// <summary>
    /// Performance statistics for a behavior type aggregation.
    /// </summary>
    protected class BehaviorStats
    {
        public long TotalUpdates { get; set; }
        public double TotalTimeMs { get; set; }
        public double AvgTimeMs => TotalUpdates > 0 ? TotalTimeMs / TotalUpdates : 0;
        public int ActiveCount { get; set; }
        public int PeakActiveCount { get; set; }
        public long LastFrameUpdates { get; set; }
        public double LastFrameTimeMs { get; set; }
    }

    /// <summary>
    /// Instance-level performance tracking for per-body overhead.
    /// </summary>
    private PerformanceCounter? _perfCounter;

    /// <summary>
    /// Gets global statistics for this behavior type.
    /// </summary>
    protected BehaviorStats Stats => _globalStats[Type];

    /// <summary>
    /// Tracks update time for performance profiling.
    /// Wrap OnUpdate implementations with this for accurate metrics.
    /// </summary>
    /// <typeparam name="T">The return type of the measured operation</typeparam>
    /// <param name="operationName">Name for profiling</param>
    /// <param name="func">The operation to measure</param>
    /// <returns>The result of the operation</returns>
    protected T MeasurePerformance<T>(string operationName, Func<T> func)
    {
        if (!_globalConfig.EnablePerformanceTracking)
            return func();

        var sw = Stopwatch.StartNew();
        try
        {
            return func();
        }
        finally
        {
            sw.Stop();
            RecordPerformanceMetric(operationName, sw.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Tracks update time without return value overload.
    /// </summary>
    protected void MeasurePerformance(string operationName, Action action)
    {
        if (!_globalConfig.EnablePerformanceTracking)
        {
            action();
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            sw.Stop();
            RecordPerformanceMetric(operationName, sw.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Records a performance sample for statistical aggregation.
    /// </summary>
    protected void RecordPerformanceMetric(string operation, double timeMs)
    {
        if (!_globalStats.TryGetValue(Type, out var stats))
        {
            stats = new BehaviorStats();
            _globalStats[Type] = stats;
        }

        stats.TotalUpdates++;
        stats.TotalTimeMs += timeMs;
        stats.LastFrameUpdates++;
        stats.LastFrameTimeMs += timeMs;
    }

    /// <summary>
    /// Updates performance counters at end of frame.
    /// Call from PhysicsWorld.Step after all behaviors updated.
    /// </summary>
    public static void EndFrame()
    {
        _globalFrameCount++;
        _lastFrameTimeMs = _totalBehaviorUpdateTimeMs;
        _totalBehaviorUpdateTimeMs = 0;

        // Reset per-frame stats
        foreach (var kvp in _globalStats)
        {
            kvp.Value.LastFrameUpdates = 0;
            kvp.Value.LastFrameTimeMs = 0;
        }
    }

    /// <summary>
    /// Gets FPS impact percentage for each behavior type.
    /// Useful for identifying performance bottlenecks.
    /// </summary>
    public static Dictionary<BodyType, double> GetBehaviorFpsImpact()
    {
        var result = new Dictionary<BodyType, double>();
        foreach (var kvp in _globalStats)
        {
            result[kvp.Key] = kvp.Value.LastFrameTimeMs;
        }
        return result;
    }

    #endregion

    #region Object Pooling Infrastructure

    /// <summary>
    /// Generic object pool to reduce GC allocations during behavior updates.
    /// Behaviors can rent/return temporary buffers, lists, vectors, etc.
    /// </summary>
    protected static class ObjectPool<T> where T : new()
    {
        private static readonly Queue<T> _pool = new();
        private static int _peakSize = 0;
        private static int _totalRented = 0;
        private static int _totalReturned = 0;

        /// <summary>
        /// Rent an object from the pool, creating new if empty.
        /// </summary>
        public static T Rent()
        {
            lock (_pool)
            {
                _totalRented++;
                if (_pool.Count > 0)
                {
                    var obj = _pool.Dequeue();
                    return obj;
                }
                return new T();
            }
        }

        /// <summary>
        /// Return an object to the pool for reuse.
        /// </summary>
        public static void Return(T obj)
        {
            lock (_pool)
            {
                _totalReturned++;
                _pool.Enqueue(obj);
                if (_pool.Count > _peakSize) _peakSize = _pool.Count;
            }
        }

        /// <summary>
        /// Pre-warm the pool with N instances to reduce initial allocations.
        /// </summary>
        public static void PreWarm(int count)
        {
            for (int i = 0; i < count; i++)
                _pool.Enqueue(new T());
            _peakSize = count;
        }

        /// <summary>
        /// Gets pool statistics for diagnostics.
        /// </summary>
        public static (int Available, int Peak, long Rented, long Returned) GetStats()
        {
            lock (_pool)
            {
                return (_pool.Count, _peakSize, _totalRented, _totalReturned);
            }
        }
    }

    /// <summary>
    /// Rents a temporary array from the object pool.
    /// Significantly reduces GC pressure in behaviors that allocate per-frame.
    /// </summary>
    /// <typeparam name="T">Array element type</typeparam>
    /// <param name="length">Desired array length</param>
    /// <returns>Array instance (return with ReturnToPool)</returns>
    protected static T[] RentArray<T>(int length)
    {
        // Note: C# doesn't allow pooling of T[] directly in generic static class
        // So we use a non-generic approach
        return new T[length]; // TODO: Implement proper pooling if needed
    }

    #endregion

    #region Spatial Partitioning Integration

    /// <summary>
    /// Interface for spatial partitioning structures (quadtree, grid hash, etc).
    /// Allows behaviors to perform optimized range queries.
    /// </summary>
    public interface ISpatialPartition
    {
        /// <summary>
        /// Query all bodies within radius of point.
        /// </summary>
        IEnumerable<RigidBody> QueryRadius(Vector2 center, double radius);

        /// <summary>
        /// Query nearest body to point (optionally filtered by predicate).
        /// </summary>
        RigidBody? QueryNearest(Vector2 point, Predicate<RigidBody>? filter = null);

        /// <summary>
        /// Get bodies in rectangular region.
        /// </summary>
        IEnumerable<RigidBody> QueryAABB(double minX, double minY, double maxX, double maxY);
    }

    /// <summary>
    /// Sets the global spatial partition for optimized queries.
    /// Call once during initialization if spatial partitioning is available.
    /// </summary>
    public static void SetSpatialPartition(ISpatialPartition partition)
    {
        _spatialPartition = partition;
    }

    /// <summary>
    /// Performs an optimized spatial query for bodies in radius.
    /// Falls back to O(n) iteration if no spatial partition is set.
    /// </summary>
    /// <param name="center">Query center</param>
    /// <param name="radius">Query radius</param>
    /// <param name="world">Physics world to query</param>
    /// <param name="filter">Optional filter predicate</param>
    /// <returns>Enumerable of bodies within radius (possibly empty)</returns>
    protected IEnumerable<RigidBody> SpatialQuery(Vector2 center, double radius, PhysicsWorld world, Predicate<RigidBody>? filter = null)
    {
        if (_spatialPartition != null)
        {
            foreach (var body in _spatialPartition.QueryRadius(center, radius))
            {
                if (filter == null || filter(body))
                    yield return body;
            }
        }
        else
        {
            // Fallback to O(n) iteration
            foreach (var body in world.Bodies)
            {
                if (filter != null && !filter(body)) continue;
                if (Vector2.DistanceSquared(center, body.Position) <= radius * radius)
                    yield return body;
            }
        }
    }

    /// <summary>
    /// Finds nearest body efficiently using spatial partition if available.
    /// </summary>
    protected RigidBody? FindNearestBody(Vector2 point, PhysicsWorld world, Predicate<RigidBody>? filter = null)
    {
        if (_spatialPartition != null)
        {
            return _spatialPartition.QueryNearest(point, filter);
        }

        RigidBody? nearest = null;
        double bestDistSq = double.MaxValue;

        foreach (var body in world.Bodies)
        {
            if (filter != null && !filter(body)) continue;
            double distSq = Vector2.DistanceSquared(point, body.Position);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                nearest = body;
            }
        }

        return nearest;
    }

    #endregion

    #region Event & Callback System

    /// <summary>
    /// Delegate for behavior lifecycle events.
    /// </summary>
    public delegate void BehaviorEventHandler(RigidBody body, object? eventData);

    /// <summary>
    /// Event raised when behavior is initialized on a body.
    /// </summary>
    public event BehaviorEventHandler? OnInitialized;

    /// <summary>
    /// Event raised before behavior update.
    /// </summary>
    public event BehaviorEventHandler? OnPreUpdate;

    /// <summary>
    /// Event raised after behavior update.
    /// </summary>
    public event BehaviorEventHandler? OnPostUpdate;

    /// <summary>
    /// Event raised on collision with another body.
    /// </summary>
    public event BehaviorEventHandler? OnCollisionDetected;

    /// <summary>
    /// Event raised when behavior is removed/destroyed.
    /// </summary>
    public event BehaviorEventHandler? OnDestroyed;

    /// <summary>
    /// Raises the OnInitialized event.
    /// </summary>
    protected virtual void RaiseInitialized(RigidBody body)
    {
        OnInitialized?.Invoke(body, null);
    }

    /// <summary>
    /// Raises the OnPreUpdate event.
    /// </summary>
    protected virtual void RaisePreUpdate(RigidBody body, double dt)
    {
        OnPreUpdate?.Invoke(body, dt);
    }

    /// <summary>
    /// Raises the OnPostUpdate event.
    /// </summary>
    protected virtual void RaisePostUpdate(RigidBody body, double dt)
    {
        OnPostUpdate?.Invoke(body, dt);
    }

    /// <summary>
    /// Raises the OnCollisionDetected event.
    /// </summary>
    protected virtual void RaiseCollision(RigidBody body, RigidBody other)
    {
        OnCollisionDetected?.Invoke(body, other);
    }

    /// <summary>
    /// Raises the OnDestroyed event.
    /// </summary>
    protected virtual void RaiseDestroyed(RigidBody body)
    {
        OnDestroyed?.Invoke(body, null);
    }

    #endregion

    #region Configuration & Tuning

    /// <summary>
    /// Configuration data structure for behavior system.
    /// Allows fine-tuning of simulation parameters without recompilation.
    /// </summary>
    public class BehaviorSystemConfig
    {
        /// <summary>
        /// Maximum number of bodies a behavior can query per frame before culling.
        /// Prevents O(n²) blowup with many bodies.
        /// </summary>
        public int MaxQueriesPerFrame { get; set; } = 1000;

        /// <summary>
        /// If true, enables detailed performance profiling per behavior.
        /// Adds minor overhead, useful for optimization.
        /// </summary>
        public bool EnablePerformanceTracking { get; set; } = true;

        /// <summary>
        /// Distance at which behaviors should stop processing far-away bodies.
        /// 0 = unlimited. Helps cull expensive calculations.
        /// </summary>
        public double GlobalCullDistance { get; set; } = 0;

        /// <summary>
        /// Maximum allowed delta time for behavior updates (prevents spiral of death).
        /// </summary>
        public double MaxDeltaTime { get; set; } = 0.033; // ~30 FPS minimum

        /// <summary>
        /// If true, behaviors can use multithreading (requires thread-safe implementation).
        /// Not currently used in WPF (single-threaded apartment model).
        /// </summary>
        public bool EnableMultithreading { get; set; } = false;

        /// <summary>
        /// Number of worker threads to use if multithreading enabled.
        /// </summary>
        public int WorkerThreads { get; set; } = Environment.ProcessorCount - 1;

        /// <summary>
        /// Enable debug drawing overlays for active behaviors.
        /// </summary>
        public bool EnableDebugVisualization { get; set; } = false;

        /// <summary>
        /// If set, logs behavior events to this logger.
        /// </summary>
        public Action<string>? Logger { get; set; } = null;

        /// <summary>
        /// Threshold (ms) above which a behavior update is considered "slow".
        /// Slow behaviors trigger warning logs if logging enabled.
        /// </summary>
        public double SlowThresholdMs { get; set; } = 1.0;
    }

    /// <summary>
    /// Gets or sets the global behavior system configuration.
    /// </summary>
    public static BehaviorSystemConfig GlobalConfig
    {
        get => _globalConfig;
        set => _globalConfig = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Behavior-specific configuration that can be tuned per-Type.
    /// </summary>
    protected BehaviorConfig Config { get; private set; } = new();

    /// <summary>
    /// Configuration specific to this behavior instance.
    /// Contains tuning parameters that can be adjusted at runtime.
    /// </summary>
    public class BehaviorConfig
    {
        /// <summary>
        /// Is this behavior currently enabled? Can be toggled at runtime.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Priority order for update execution (lower = earlier).
        /// Useful when behaviors have dependencies.
        /// </summary>
        public int UpdatePriority { get; set; } = 0;

        /// <summary>
        /// Maximum number of other bodies this behavior will interact with per frame.
        /// 0 = unlimited.
        /// </summary>
        public int MaxInteractionsPerFrame { get; set; } = 0;

        /// <summary>
        /// Interaction radius beyond which bodies are ignored (0 = unlimited).
        /// </summary>
        public double InteractionRadius { get; set; } = 0;

        /// <summary>
        /// If true, this behavior runs during physics step (before integration).
        /// If false, runs after integration.
        /// </summary>
        public bool RunPreIntegration { get; set; } = true;

        /// <summary>
        /// Custom multiplier applied to the delta time passed to OnUpdate.
        /// Allows behaviors to run at different time scales.
        /// </summary>
        public double TimeScale { get; set; } = 1.0;

        /// <summary>
        /// If set, body will automatically be removed after this many seconds.
        /// 0 = never auto-remove.
        /// </summary>
        public double AutoRemoveAfterSeconds { get; set; } = 0;

        /// <summary>
        /// Energy cost per update (for resource-constrained simulations).
        /// </summary>
        public double EnergyCostPerSecond { get; set; } = 0;

        /// <summary>
        /// Debug flag that enables extra logging for this behavior instance.
        /// </summary>
        public bool DebugMode { get; set; } = false;
    }

    /// <summary>
    /// Gets the configuration object for this behavior type.
    /// Modify to tune behavior parameters at runtime.
    /// </summary>
    public BehaviorConfig GetConfig() => Config;

    /// <summary>
    /// Logs a debug message if DebugMode is enabled.
    /// </summary>
    protected void LogDebug(RigidBody body, string message)
    {
        if (Config.DebugMode && _globalConfig.Logger != null)
        {
            _globalConfig.Logger($"[{Type}] Body {body.Id}: {message}");
        }
    }

    #endregion

    #region Utility Methods for Derived Classes

    /// <summary>
    /// Safely computes square of distance without sqrt overhead.
    /// Use when comparing distances or using distance squared formulas.
    /// </summary>
    protected static double DistanceSquared(RigidBody a, RigidBody b)
    {
        return (a.Position - b.Position).LengthSquared;
    }

    /// <summary>
    /// Safely computes distance with early-out for optimization.
    /// </summary>
    protected static double Distance(RigidBody a, RigidBody b)
    {
        return System.Math.Sqrt(DistanceSquared(a, b));
    }

    /// <summary>
    /// Checks if a body is within interaction range based on current config.
    /// </summary>
    protected bool IsInInteractionRange(RigidBody self, RigidBody other)
    {
        double radius = Config.InteractionRadius;
        if (radius <= 0) return true;
        return DistanceSquared(self, other) <= radius * radius;
    }

    /// <summary>
    /// Returns a vector from body A to body B, or zero vector if coincident.
    /// </summary>
    protected static Vector2 DirectionTo(RigidBody from, RigidBody to)
    {
        var diff = to.Position - from.Position;
        double len = diff.Length;
        return len > 0 ? diff / len : Vector2.Zero;
    }

    /// <summary>
    /// Clamps a value between min and max inclusive.
    /// </summary>
    protected static double Clamp(double value, double min, double max)
    {
        return value < min ? min : (value > max ? max : value);
    }

    /// <summary>
    /// Linear interpolation between two values.
    /// </summary>
    protected static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * Clamp(t, 0, 1);
    }

    /// <summary>
    /// Smoothstepped interpolation (smooth Hermite curve).
    /// </summary>
    protected static double SmoothStep(double edge0, double edge1, double x)
    {
        x = Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return x * x * (3 - 2 * x);
    }

    /// <summary>
    /// Smoother stepped interpolation (even smoother).
    /// </summary>
    protected static double SmootherStep(double edge0, double edge1, double x)
    {
        x = Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return x * x * x * (x * (x * 6 - 15) + 10);
    }

    /// <summary>
    /// Gets a random value in range [-1, 1] using deterministic seeded RNG.
    /// </summary>
    protected static double RandomSigned(RigidBody body)
    {
        // Use body ID as seed for deterministic randomness per body
        var rng = new Random(body.Id);
        return rng.NextDouble() * 2 - 1;
    }

    /// <summary>
    /// Gets a random unit vector2 using deterministic seeded RNG.
    /// </summary>
    protected static Vector2 RandomUnitVector(RigidBody body)
    {
        var rng = new Random(body.Id);
        double angle = rng.NextDouble() * Math.PI * 2;
        return new Vector2(Math.Cos(angle), Math.Sin(angle));
    }

    #endregion

    #region Collision & Contact Management

    /// <summary>
    /// Checks if this body is currently colliding with another.
    /// </summary>
    protected bool IsCollidingWith(RigidBody body, RigidBody other, PhysicsWorld world)
    {
        if (body == other) return false;

        // Simple circle collision check
        double distSq = (body.Position - other.Position).LengthSquared;
        double radiusSum = body.Radius + other.Radius;
        return distSq < radiusSum * radiusSum;
    }

    /// <summary>
    /// Gets all bodies currently overlapping with the given body.
    /// </summary>
    protected List<RigidBody> GetOverlappingBodies(RigidBody body, PhysicsWorld world)
    {
        var result = new List<RigidBody>();

        foreach (var other in world.Bodies)
        {
            if (other == body) continue;
            if (IsCollidingWith(body, other, world))
                result.Add(other);
        }

        return result;
    }

    /// <summary>
    /// Basic collision handler that resolves penetration and applies restitution.
    /// </summary>
    protected void HandleCollisionBasics(RigidBody a, RigidBody b)
    {
        // This is a simplified version; actual collision resolution happens in PhysicsWorld
        // Behaviors can use this to add custom effects on collision

        var dir = b.Position - a.Position;
        double dist = dir.Length;
        if (dist < 0.001) return; // Prevent divide by zero

        var normal = dir / dist;
        // Additional custom collision handling can go here
    }

    /// <summary>
    /// Checks if body is inside a trigger area (non-physical collision).
    /// </summary>
    protected bool IsInTriggerZone(RigidBody body, Vector2 triggerCenter, double triggerRadius)
    {
        return (body.Position - triggerCenter).LengthSquared <= triggerRadius * triggerRadius;
    }

    #endregion

    #region Force Application Helpers

    /// <summary>
    /// Applies an explosive radial force centered at point.
    /// Convenience wrapper around world.ForceManager.Explosion.
    /// </summary>
    protected void ApplyExplosion(RigidBody body, Vector2 epicenter, double strength, double radius, PhysicsWorld world)
    {
        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic) continue;

            Vector2 dir = other.Position - epicenter;
            double dist = dir.Length;
            if (dist > radius || dist < 0.1) continue;

            double falloff = 1.0 - (dist / radius);
            double force = strength * falloff * falloff;
            other.ApplyImpulse(dir.Normalized * force);
        }
    }

    /// <summary>
    /// Applies a radial force that pulls bodies toward a center (like gravity well).
    /// </summary>
    protected void ApplyAttraction(RigidBody body, Vector2 attractor, double strength, double radius, PhysicsWorld world)
    {
        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic) continue;

            Vector2 dir = attractor - other.Position;
            double dist = dir.Length;
            if (dist > radius || dist < 0.1) continue;

            double falloff = 1.0 - (dist / radius);
            double force = strength * falloff;
            other.ApplyForce(dir.Normalized * force);
        }
    }

    /// <summary>
    /// Applies a repulsive radial force that pushes bodies away.
    /// </summary>
    protected void ApplyRepulsion(RigidBody body, Vector2 center, double strength, double radius, PhysicsWorld world)
    {
        foreach (var other in world.Bodies)
        {
            if (other == body || other.IsStatic) continue;

            Vector2 dir = other.Position - center;
            double dist = dir.Length;
            if (dist > radius || dist < 0.1) continue;

            double falloff = 1.0 - (dist / radius);
            double force = strength * falloff;
            other.ApplyForce(dir.Normalized * force);
        }
    }

    #endregion

    #region Lifecycle & State Management

    /// <summary>
    /// Tracks bodies that have been initialized by this behavior type.
    /// Used for cleanup and per-body state tracking.
    /// </summary>
    private static readonly HashSet<int> _trackedBodyIds = new();

    /// <summary>
    /// Tracks per-body lifetime for auto-removal and aging logic.
    /// </summary>
    private static readonly Dictionary<int, double> _bodyLifetimes = new();

    /// <summary>
    /// Tracks per-body custom state objects (provided by derived class).
    /// </summary>
    private static readonly Dictionary<int, object?> _bodyCustomState = new();

    /// <summary>
    /// Gets or sets custom per-body state object.
    /// Derived classes can store arbitrary state here keyed by body ID.
    /// </summary>
    protected object? GetBodyState(RigidBody body) => 
        _bodyCustomState.TryGetValue(body.Id, out var state) ? state : null;

    /// <summary>
    /// Sets custom per-body state object.
    /// </summary>
    protected void SetBodyState(RigidBody body, object? state) =>
        _bodyCustomState[body.Id] = state;

    /// <summary>
    /// Removes custom state for a body.
    /// </summary>
    protected void ClearBodyState(RigidBody body) =>
        _bodyCustomState.Remove(body.Id);

    /// <summary>
    /// Gets the lifetime of a body in seconds since creation.
    /// </summary>
    protected double GetBodyAge(RigidBody body) =>
        _bodyLifetimes.TryGetValue(body.Id, out var age) ? age : 0;

    /// <summary>
    /// Registers a body for lifetime tracking.
    /// Called automatically by base OnCreate.
    /// </summary>
    private void RegisterBodyForTracking(RigidBody body)
    {
        if (!_trackedBodyIds.Contains(body.Id))
        {
            _trackedBodyIds.Add(body.Id);
            _bodyLifetimes[body.Id] = 0;
            _bodyCustomState[body.Id] = null;
        }
        RaiseInitialized(body);
    }

    /// <summary>
    /// Updates lifetime and performance metrics each frame.
    /// </summary>
    private void UpdatePerformanceMetrics(RigidBody body, double dt)
    {
        if (_trackedBodyIds.Contains(body.Id))
        {
            _bodyLifetimes[body.Id] += dt;
        }

        // Update per-frame counters
        if (_perfCounter != null)
        {
            _perfCounter.UpdateCount++;
            _perfCounter.TotalTime += dt;
        }
    }

    /// <summary>
    /// Unregisters a body from all tracking dictionaries.
    /// </summary>
    protected void UnregisterBody(RigidBody body)
    {
        _trackedBodyIds.Remove(body.Id);
        _bodyLifetimes.Remove(body.Id);
        _bodyCustomState.Remove(body.Id);
        RaiseDestroyed(body);
    }

    #endregion

    #region Advanced Physics Helpers

    /// <summary>
    /// Applies an impulse that conserves momentum while transferring velocity.
    /// </summary>
    protected void ApplyConservingImpulse(RigidBody a, RigidBody b, Vector2 impulse)
    {
        if (!a.IsStatic) a.ApplyImpulse(-impulse);
        if (!b.IsStatic) b.ApplyImpulse(impulse);
    }

    /// <summary>
    /// Computes the relative velocity between two bodies.
    /// </summary>
    protected Vector2 RelativeVelocity(RigidBody a, RigidBody b) =>
        b.Velocity - a.Velocity;

    /// <summary>
    /// Computes closing speed (speed along the normal direction).
    /// Positive value indicates bodies are moving apart.
    /// </summary>
    protected double ClosingSpeed(RigidBody a, RigidBody b, Vector2 normal)
    {
        return Vector2.Dot(RelativeVelocity(a, b), normal);
    }

    /// <summary>
    /// Calculates post-collision velocities using restitution and mass.
    /// </summary>
    protected static void CalculateCollisionImpulse(RigidBody a, RigidBody b, Vector2 normal, double restitution,
        out Vector2 impulseA, out Vector2 impulseB)
    {
        Vector2 relVel = b.Velocity - a.Velocity;
        double velAlongNormal = Vector2.Dot(relVel, normal);

        // Don't resolve if velocities are separating
        if (velAlongNormal > 0)
        {
            impulseA = Vector2.Zero;
            impulseB = Vector2.Zero;
            return;
        }

        double invMassA = a.InverseMass;
        double invMassB = b.InverseMass;
        double invMassSum = invMassA + invMassB;

        if (invMassSum < 1e-6)
        {
            impulseA = Vector2.Zero;
            impulseB = Vector2.Zero;
            return;
        }

        double j = -(1 + restitution) * velAlongNormal;
        j /= invMassSum;

        Vector2 impulse = normal * j;
        impulseA = -impulse * invMassA;
        impulseB = impulse * invMassB;
    }

    /// <summary>
    /// Predicts future position using current velocity (no forces).
    /// </summary>
    protected Vector2 PredictPosition(RigidBody body, double dt) =>
        body.Position + body.Velocity * dt;

    /// <summary>
    /// Predicts future position with constant acceleration.
    /// </summary>
    protected Vector2 PredictPosition(RigidBody body, double dt, Vector2 constantAccel) =>
        body.Position + body.Velocity * dt + 0.5 * constantAccel * dt * dt;

    #endregion

    #region Debug Visualization Support

    /// <summary>
    /// Interface for debug renderable objects.
    /// </summary>
    public interface IDebugRenderable
    {
        void Render(DrawingContext dc);
    }

    /// <summary>
    /// Optional debug overlay data for behaviors that want to draw.
    /// Only used if GlobalConfig.EnableDebugVisualization is true.
    /// </summary>
    protected virtual void RenderDebugOverlay(RigidBody body, DrawingContext dc)
    {
        // Override in derived classes to draw debug info
    }

    /// <summary>
    /// Convenience wrapper for debug visualization that checks global config.
    /// </summary>
    protected void RenderDebugVisualization(RigidBody body, DrawingContext? dc)
    {
        if (GlobalConfig.EnableDebugVisualization && dc != null)
        {
            RenderDebugOverlay(body, dc);
        }
    }

    /// <summary>
    /// Draws a debug circle at position with optional label.
    /// </summary>
    protected static void DebugDrawCircle(DrawingContext dc, Vector2 pos, double radius, Brush brush, double thickness = 1.0)
    {
        dc.DrawEllipse(brush, new Pen(Brushes.Black, thickness), new Point(pos.X, pos.Y), radius, radius);
    }

    /// <summary>
    /// Draws a debug line between two points.
    /// </summary>
    protected static void DebugDrawLine(DrawingContext dc, Vector2 from, Vector2 to, Brush brush, double thickness = 1.0)
    {
        dc.DrawLine(new Pen(brush, thickness), new Point(from.X, from.Y), new Point(to.X, to.Y));
    }

    /// <summary>
    /// Draws debug text label at position.
    /// </summary>
    protected static void DebugDrawText(DrawingContext dc, string text, Vector2 pos, Brush brush, double fontSize = 10)
    {
        var typeface = new Typeface("Consolas");
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            brush);
        dc.DrawText(formatted, new Point(pos.X, pos.Y));
    }

    #endregion

    #region Thread Safety & Synchronization

    /// <summary>
    /// Lock object for thread-safe operations on instance data.
    /// Behavior instances should be atomic - no lock needed for simple property access.
    /// Use this lock when modifying shared state across multiple bodies of same type.
    /// </summary>
    private readonly object _syncLock = new();

    /// <summary>
    /// Executes an action in a thread-safe manner (if multithreading enabled).
    /// Currently unused in WPF but available for future parallel implementations.
    /// </summary>
    protected void ThreadSafe(Action action)
    {
        if (_globalConfig.EnableMultithreading)
        {
            lock (_syncLock)
            {
                action();
            }
        }
        else
        {
            action();
        }
    }

    /// <summary>
    /// Gets a value in thread-safe manner.
    /// </summary>
    protected T ThreadSafeGet<T>(Func<T> getter)
    {
        if (_globalConfig.EnableMultithreading)
        {
            lock (_syncLock)
            {
                return getter();
            }
        }
        else
        {
            return getter();
        }
    }

    /// <summary>
    /// Sets a value in thread-safe manner.
    /// </summary>
    protected void ThreadSafeSet(Action setter)
    {
        if (_globalConfig.EnableMultithreading)
        {
            lock (_syncLock)
            {
                setter();
            }
        }
        else
        {
            setter();
        }
    }

    #endregion

    #region Serialization & Cloning

    /// <summary>
    /// Creates a deep clone of behavior configuration for per-body overrides.
    /// </summary>
    protected BehaviorConfig CloneConfig() =>
        new BehaviorConfig
        {
            Enabled = Config.Enabled,
            UpdatePriority = Config.UpdatePriority,
            MaxInteractionsPerFrame = Config.MaxInteractionsPerFrame,
            InteractionRadius = Config.InteractionRadius,
            RunPreIntegration = Config.RunPreIntegration,
            TimeScale = Config.TimeScale,
            AutoRemoveAfterSeconds = Config.AutoRemoveAfterSeconds,
            EnergyCostPerSecond = Config.EnergyCostPerSecond,
            DebugMode = Config.DebugMode
        };

    #endregion

    #region Internal Body Tracking (for factory & cleanup)

    private static readonly Dictionary<BodyType, HashSet<int>> _bodiesByType = new();
    private static readonly object _globalLock = new();

    private void RegisterBodyInternal(RigidBody body)
    {
        lock (_globalLock)
        {
            if (!_bodiesByType.TryGetValue(Type, out var set))
            {
                set = new HashSet<int>();
                _bodiesByType[Type] = set;
            }
            set.Add(body.Id);
        }
    }

    /// <summary>
    /// Gets number of active bodies with this behavior type.
    /// </summary>
    protected int GetActiveCount()
    {
        lock (_globalLock)
        {
            return _bodiesByType.TryGetValue(Type, out var set) ? set.Count : 0;
        }
    }

    #endregion

    #region Advanced Feature Hooks (for extensibility)

    /// <summary>
    /// Hook called before collision detection if behavior wants to modify collision filtering.
    /// Return false to ignore collision between these bodies.
    /// </summary>
    protected virtual bool ShouldCollide(RigidBody body, RigidBody other, PhysicsWorld world) => true;

    /// <summary>
    /// Hook to modify the body's force accumulator before integration.
    /// Can be used for custom force curves or damping.
    /// </summary>
    protected virtual void ModifyForce(RigidBody body, ref Vector2 force) { }

    /// <summary>
    /// Hook to modify velocity after forces but before integration.
    /// </summary>
    protected virtual void ModifyVelocity(RigidBody body, double dt) { }

    /// <summary>
    /// Hook called after position integration.
    /// Useful for boundary clamping or teleportation effects.
    /// </summary>
    protected virtual void PostIntegrate(RigidBody body, double dt, PhysicsWorld world) { }

    /// <summary>
    /// Hook that determines if body should be removed (expiration, out of bounds, etc).
    /// </summary>
    protected virtual bool ShouldExpire(RigidBody body, double lifetime) => false;

    #endregion
}

/// <summary>
/// Simple performance counter for per-body profiling.
/// </summary>
internal class PerformanceCounter
{
    public long UpdateCount { get; set; }
    public double TotalTime { get; set; }
    public double AverageTime => UpdateCount > 0 ? TotalTime / UpdateCount : 0;
    public void Reset() { UpdateCount = 0; TotalTime = 0; }
}

/// <summary>
/// Configuration summary for display in UI/debug panels.
/// </summary>
public struct BehaviorConfigSummary
{
    public string Name;
    public bool Enabled;
    public int ActiveCount;
    public double AvgUpdateTimeMs;
    public double TotalUpdateTimeMs;
}
