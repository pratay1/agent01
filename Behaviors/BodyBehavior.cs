using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace PhysicsSandbox.Behaviors;

public abstract class BodyBehavior
{
    private static readonly Dictionary<BodyType, BehaviorStats> _globalStats = new();
    private static long _globalFrameCount = 0;
    private static double _lastFrameTimeMs = 0;
    private static BehaviorSystemConfig _globalConfig = new();

    public abstract BodyType Type { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string ColorHex { get; }
    public abstract double DefaultRadius { get; }
    public abstract double DefaultMass { get; }
    public abstract double DefaultRestitution { get; }

    public virtual void OnCreate(RigidBody body) => RegisterBody(body);

    public virtual void OnUpdate(RigidBody body, double dt, PhysicsWorld world) { }

    public virtual void OnCollision(RigidBody body, RigidBody other, PhysicsWorld world) { }

    public class BehaviorStats
    {
        public long TotalUpdates;
        public double TotalTimeMs;
        public double AvgTimeMs => TotalUpdates > 0 ? TotalTimeMs / TotalUpdates : 0;
    }

    protected BehaviorStats Stats => _globalStats[Type];

    public class BehaviorSystemConfig
    {
        public bool EnablePerformanceTracking = true;
        public double MaxDeltaTime = 0.033;
        public bool EnableDebugVisualization = false;
        public Action<string>? Logger;
        public bool EnableMultithreading = false;
    }

    public static BehaviorSystemConfig GlobalConfig
    {
        get => _globalConfig;
        set => _globalConfig = value ?? throw new ArgumentNullException(nameof(value));
    }

    protected BehaviorConfig Config { get; private set; } = new();

    public class BehaviorConfig
    {
        public bool Enabled = true;
        public int UpdatePriority;
        public double InteractionRadius;
        public double TimeScale = 1.0;
        public bool DebugMode;
    }

    public BehaviorConfig GetConfig() => Config;

    protected void LogDebug(RigidBody body, string message)
    {
        if (Config.DebugMode && _globalConfig.Logger != null)
            _globalConfig.Logger($"[{Type}] Body {body.Id}: {message}");
    }

    protected static double DistanceSquared(RigidBody a, RigidBody b) => (a.Position - b.Position).LengthSquared;
    protected static double DistanceSquared(Vector2 a, Vector2 b) => (a - b).LengthSquared;

    protected static double Distance(RigidBody a, RigidBody b) => Math.Sqrt(DistanceSquared(a, b));

    protected bool IsInInteractionRange(RigidBody self, RigidBody other)
    {
        double r = Config.InteractionRadius;
        return r <= 0 || DistanceSquared(self, other) <= r * r;
    }

    protected static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);

    protected static double Lerp(double a, double b, double t) => a + (b - a) * Clamp(t, 0, 1);

    protected static Vector2 DirectionTo(RigidBody from, RigidBody to)
    {
        var diff = to.Position - from.Position;
        double len = diff.Length;
        return len > 0 ? diff / len : Vector2.Zero;
    }

    protected IEnumerable<RigidBody> SpatialQuery(Vector2 center, double radius, PhysicsWorld world)
    {
        foreach (var body in world.Bodies)
            if (DistanceSquared(center, body.Position) <= radius * radius)
                yield return body;
    }

    protected bool IsCollidingWith(RigidBody a, RigidBody b)
    {
        double distSq = (a.Position - b.Position).LengthSquared;
        double radiusSum = a.Radius + b.Radius;
        return distSq < radiusSum * radiusSum;
    }

    protected void ApplyExplosion(RigidBody source, Vector2 epicenter, double strength, double radius, PhysicsWorld world)
    {
        foreach (var other in world.Bodies)
        {
            if (other == source || other.IsStatic) continue;
            Vector2 dir = other.Position - epicenter;
            double dist = dir.Length;
            if (dist > radius || dist < 0.1) continue;
            double falloff = 1.0 - dist / radius;
            other.ApplyImpulse(dir.Normalized * strength * falloff * falloff);
        }
    }

    protected void ApplyAttraction(RigidBody source, Vector2 center, double strength, double radius, PhysicsWorld world)
    {
        foreach (var other in world.Bodies)
        {
            if (other == source || other.IsStatic) continue;
            Vector2 dir = center - other.Position;
            double dist = dir.Length;
            if (dist > radius || dist < 0.1) continue;
            other.ApplyForce(dir.Normalized * strength * (1.0 - dist / radius));
        }
    }

    protected static readonly HashSet<int> _trackedBodyIds = new();
    private static readonly Dictionary<int, object?> _bodyState = new();

    protected object? GetBodyState(RigidBody body) => _bodyState.TryGetValue(body.Id, out var s) ? s : null;
    protected void SetBodyState(RigidBody body, object? state) => _bodyState[body.Id] = state;
    protected void ClearBodyState(RigidBody body) => _bodyState.Remove(body.Id);

    private void RegisterBody(RigidBody body)
    {
        if (!_trackedBodyIds.Contains(body.Id))
        {
            _trackedBodyIds.Add(body.Id);
            _bodyState[body.Id] = null;
        }
    }

    protected void RecordPerformanceMetric(string op, double timeMs)
    {
        if (!_globalStats.TryGetValue(Type, out var stats))
        {
            stats = new BehaviorStats();
            _globalStats[Type] = stats;
        }
        stats.TotalUpdates++;
        stats.TotalTimeMs += timeMs;
    }

    protected virtual void RenderDebugOverlay(RigidBody body, DrawingContext dc) { }

    protected void RenderDebugVisualization(RigidBody body, DrawingContext? dc)
    {
        if (GlobalConfig.EnableDebugVisualization && dc != null)
            RenderDebugOverlay(body, dc);
    }

    protected virtual void RaiseInitialized(RigidBody body) { }
    protected virtual void RaisePreUpdate(RigidBody body, double dt) { }
    protected virtual void RaisePostUpdate(RigidBody body, double dt) { }
    protected virtual void RaiseCollision(RigidBody body, RigidBody other) { }
    protected virtual void RaiseDestroyed(RigidBody body) { }

    public static void EndFrame()
    {
        _globalFrameCount++;
        _lastFrameTimeMs = 0;
    }
}

public class PerformanceCounter
{
    public long UpdateCount;
    public double TotalTime;
    public double AverageTime => UpdateCount > 0 ? TotalTime / UpdateCount : 0;
    public void Reset() { UpdateCount = 0; TotalTime = 0; }
}

public struct BehaviorConfigSummary
{
    public string Name;
    public bool Enabled;
    public int ActiveCount;
    public double AvgUpdateTimeMs;
    public double TotalUpdateTimeMs;
}