using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using System;
using System.Collections.Generic;

namespace PhysicsSandbox.Behaviors;

public class NormalBehavior : BodyBehavior
{
    private const double DEFAULT_DRAG = 0.02;
    private const double MIN_MASS = 0.1;
    private const double MAX_MASS = 100.0;

    public enum NormalType { Rock, Balloon, Feather, Steel, Wood, Plastic, Rubber, Glass, Custom }

    public class NormalProfile
    {
        public string Name = "";
        public double Mass = 5.0;
        public double Radius = 10.0;
        public double Restitution = 0.5;
        public double Drag = 0.02;
        public string ColorHex = "#808080";
    }

    private static readonly Dictionary<NormalType, NormalProfile> _profiles = new()
    {
        { NormalType.Rock, new() { Name = "Rock", Mass = 10.0, Radius = 12.0, Restitution = 0.3, Drag = 0.03, ColorHex = "#696969" } },
        { NormalType.Balloon, new() { Name = "Balloon", Mass = 0.2, Radius = 15.0, Restitution = 0.9, Drag = 0.001, ColorHex = "#FF69B4" } },
        { NormalType.Feather, new() { Name = "Feather", Mass = 0.1, Radius = 8.0, Restitution = 0.6, Drag = 0.05, ColorHex = "#F0E68C" } },
        { NormalType.Steel, new() { Name = "Steel", Mass = 20.0, Radius = 8.0, Restitution = 0.2, Drag = 0.01, ColorHex = "#4682B4" } },
        { NormalType.Wood, new() { Name = "Wood", Mass = 5.0, Radius = 10.0, Restitution = 0.4, Drag = 0.02, ColorHex = "#DEB887" } },
        { NormalType.Plastic, new() { Name = "Plastic", Mass = 3.0, Radius = 10.0, Restitution = 0.5, Drag = 0.02, ColorHex = "#00CED1" } },
        { NormalType.Rubber, new() { Name = "Rubber", Mass = 4.0, Radius = 10.0, Restitution = 0.8, Drag = 0.015, ColorHex = "#2F4F4F" } },
        { NormalType.Glass, new() { Name = "Glass", Mass = 8.0, Radius = 10.0, Restitution = 0.1, Drag = 0.01, ColorHex = "#87CEEB" } }
    };

    private NormalType _type = NormalType.Rock;
    private NormalProfile _profile = _profiles[NormalType.Rock];
    private double _customDrag = -1;

    public override BodyType Type => BodyType.Normal;
    public override string Name => "Normal";
    public override string Description => "Standard physics body";
    public override string ColorHex => _profile.ColorHex;
    public override double DefaultRadius => _profile.Radius;
    public override double DefaultMass => _profile.Mass;
    public override double DefaultRestitution => _profile.Restitution;

    public NormalBehavior() : this(NormalType.Rock) { }
    public NormalBehavior(NormalType type)
    {
        _type = type;
        _profile = _profiles[type];
    }

    public override void OnUpdate(RigidBody body, double dt, PhysicsWorld world)
    {
        if (body.IsStatic || body.IsFrozen) return;
        ApplyDrag(body, dt);
    }

    private void ApplyDrag(RigidBody body, double dt)
    {
        double drag = _customDrag > 0 ? _customDrag : _profile.Drag;
        if (drag <= 0) return;
        double speed = body.Velocity.Length;
        if (speed < 0.01) return;
        double dragForce = drag * speed * speed;
        body.ApplyForce(-body.Velocity.Normalized * dragForce);
    }

    public void SetType(NormalType type)
    {
        _type = type;
        _profile = _profiles[type];
    }

    public void SetCustomDrag(double drag) => _customDrag = Math.Max(0, drag);
    public void SetAffectedByGravity(bool affected) { }
    public NormalType GetType() => _type;
}