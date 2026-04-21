using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows;
using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Rendering;

public class Renderer
{
    private IReadOnlyList<RigidBody>? _currentBodies;
    private readonly List<Particle> _particles = new();
    private double _particleTimer = 0;

    // Static brush/pixel caches
    private static readonly Dictionary<BodyType, Brush> _bodyBrushes = new();
    private static readonly Dictionary<BodyType, Pen> _bodyPens = new();
    private static readonly Dictionary<BodyType, Brush> _particleBrushes = new();
    private static readonly Random _rand = new Random();

    static Renderer()
    {
        foreach (BodyType type in Enum.GetValues(typeof(BodyType)))
        {
            var colorHex = type.GetColorHex();
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var fill = new SolidColorBrush(Color.FromArgb(230, color.R, color.G, color.B));
            fill.Freeze();
            _bodyBrushes[type] = fill;

            // Determine stroke
            Color strokeColor;
            double thickness = 1.5;
            switch (type)
            {
                case BodyType.BlackHole:
                    strokeColor = (Color)ColorConverter.ConvertFromString("#AB47BC");
                    thickness = 3;
                    break;
                case BodyType.Spike:
                    strokeColor = (Color)ColorConverter.ConvertFromString("#FFCDD2");
                    thickness = 2;
                    break;
                case BodyType.Explosive:
                    strokeColor = (Color)ColorConverter.ConvertFromString("#FF8A80");
                    thickness = 2;
                    break;
                case BodyType.Lightning:
                    strokeColor = (Color)ColorConverter.ConvertFromString("#FFD54F");
                    thickness = 2;
                    break;
                case BodyType.Plasma:
                    strokeColor = (Color)ColorConverter.ConvertFromString("#F48FB1");
                    thickness = 2;
                    break;
                default:
                    strokeColor = Colors.White;
                    thickness = 1.5;
                    break;
            }
            var pen = new Pen(new SolidColorBrush(strokeColor), thickness);
            pen.Freeze();
            _bodyPens[type] = pen;

            // Particle brushes (fully opaque)
            var pBrush = new SolidColorBrush(color);
            pBrush.Freeze();
            _particleBrushes[type] = pBrush;
        }
    }

    public void Update(IReadOnlyList<RigidBody> bodies, double dt)
    {
        _currentBodies = bodies;

        // Spawn particles at fixed interval
        _particleTimer += dt;
        if (_particleTimer >= 0.1)
        {
            SpawnParticlesForAllBodies();
            _particleTimer = 0;
        }

        UpdateParticles(dt);
    }

    private void SpawnParticlesForAllBodies()
    {
        if (_currentBodies == null) return;
        foreach (var body in _currentBodies)
        {
            int count = body.BodyType switch
            {
                BodyType.Explosive => 1,
                BodyType.Lightning => 1,
                BodyType.Plasma => 1,
                BodyType.BlackHole => 1,
                BodyType.Turbo => 1,
                BodyType.GravityWell => 1,
                BodyType.Repulsor => 1,
                BodyType.Fire => 1,
                BodyType.Angel => 1,
                BodyType.Molly => 1,
                _ => 0
            };

            if (count == 0) continue;

            for (int i = 0; i < count; i++)
            {
                var angle = _rand.NextDouble() * System.Math.PI * 2;
                var dist = body.Radius + _rand.NextDouble() * 10;
                var speed = _rand.NextDouble() * 50 + 20;

                var particle = new Particle
                {
                    X = body.Position.X + System.Math.Cos(angle) * dist,
                    Y = body.Position.Y + System.Math.Sin(angle) * dist,
                    VX = System.Math.Cos(angle + System.Math.PI) * speed,
                    VY = System.Math.Sin(angle + System.Math.PI) * speed,
                    Life = 1.0,
                    MaxLife = 0.5 + _rand.NextDouble() * 0.5,
                    Size = 2 + _rand.NextDouble() * 4,
                    Type = body.BodyType
                };
                _particles.Add(particle);
            }
        }
    }

    private void UpdateParticles(double dt)
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
            p.Life -= dt;

            if (p.Life <= 0)
            {
                _particles.RemoveAt(i);
                i--;
            }
        }
    }

    public void Render(DrawingContext dc)
    {
        if (_currentBodies != null)
        {
            foreach (var body in _currentBodies)
            {
                var brush = _bodyBrushes[body.BodyType];
                var pen = _bodyPens[body.BodyType];
                var center = new Point(body.Position.X, body.Position.Y);
                dc.DrawEllipse(brush, pen, center, body.Radius, body.Radius);
            }
        }

        // Particles
        foreach (var p in _particles)
        {
            double opacity = p.Life / p.MaxLife;
            dc.PushOpacity(opacity);
            try
            {
                var brush = _particleBrushes[p.Type];
                var center = new Point(p.X, p.Y);
                double radius = p.Size * 0.5;
                dc.DrawEllipse(brush, null, center, radius, radius);
            }
            finally
            {
                dc.Pop();
            }
        }
    }

    public void Clear()
    {
        _particles.Clear();
    }
}

public class Particle
{
    public double X, Y;
    public double VX, VY;
    public double Life;
    public double MaxLife;
    public BodyType Type;
    public double Size;
}
