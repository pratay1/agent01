using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Linq;
using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;
using PhysicsSandbox;

namespace PhysicsSandbox.Rendering;

public class Particle
{
    public double X, Y;
    public double VX, VY;
    public double Life;
    public double MaxLife;
    public Color Color;
    public double Size;
}

public class Renderer
{
    private readonly Canvas _canvas;
    private readonly Dictionary<int, Ellipse> _bodyShapes = new();
    private readonly Dictionary<int, Line> _velocityLines = new();
    private readonly HashSet<UIElement> _debugShapes = new();
    private readonly List<Particle> _particles = new();
    private readonly List<UIElement> _particleShapes = new();
    private readonly Random _rand = new Random();
    private const double VelocityScale = 0.1;
    private double _particleTimer = 0;

    public Renderer(Canvas canvas)
    {
        _canvas = canvas;
        _canvas.Background = new SolidColorBrush(Color.FromRgb(20, 20, 25));
    }

    public void UpdateBodies(IEnumerable<RigidBody> bodies)
    {
        var activeIds = new HashSet<int>();

        foreach (var body in bodies)
        {
            activeIds.Add(body.Id);
            SpawnParticlesForBody(body);
            UpdateBodyParticles(body);

            double screenX = body.Position.X;
            double screenY = body.Position.Y;

            if (!_bodyShapes.TryGetValue(body.Id, out var ellipse))
            {
                ellipse = CreateBodyShape(body);
                _bodyShapes[body.Id] = ellipse;
                _canvas.Children.Add(ellipse);

                var velocityLine = new Line
                {
                    Stroke = Brushes.Yellow,
                    StrokeThickness = 2,
                    Opacity = 0.3
                };
                _velocityLines[body.Id] = velocityLine;
                _canvas.Children.Add(velocityLine);
            }

            Canvas.SetLeft(ellipse, screenX - body.Radius);
            Canvas.SetTop(ellipse, screenY - body.Radius);
            ellipse.Width = body.Radius * 2;
            ellipse.Height = body.Radius * 2;

            UpdateVelocityLine(body, screenX, screenY);
        }

        foreach (var id in _bodyShapes.Keys.Except(activeIds).ToList())
        {
            if (_bodyShapes.TryGetValue(id, out var shape))
            {
                _canvas.Children.Remove(shape);
                _bodyShapes.Remove(id);
            }
            if (_velocityLines.TryGetValue(id, out var line))
            {
                _canvas.Children.Remove(line);
                _velocityLines.Remove(id);
            }
        }

        UpdateParticles();
    }

    private void SpawnParticlesForBody(RigidBody body)
    {
        _particleTimer += 0.016;
        if (_particleTimer < 0.05) return;
        _particleTimer = 0;

        int count = body.BodyType switch
        {
            BodyType.Explosive => 3,
            BodyType.Lightning => 3,
            BodyType.Plasma => 3,
            BodyType.BlackHole => 2,
            BodyType.Turbo => 2,
            BodyType.GravityWell => 2,
            BodyType.Repulsor => 2,
            BodyType.Fire => 3,
            BodyType.Spike => 1,
            BodyType.AntiGravity => 1,
            BodyType.Bouncy => 1,
            BodyType.Glue => 1,
            BodyType.Freezer => 1,
            BodyType.Phantom => 1,
            BodyType.Heavy => 0,
            BodyType.Normal => 0,
            BodyType.Angel => 1,
            BodyType.Molly => 2,
            _ => 0
        };

        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            var angle = _rand.NextDouble() * System.Math.PI * 2;
            var dist = body.Radius + _rand.NextDouble() * 10;
            var speed = _rand.NextDouble() * 50 + 20;
            
            var particle = new Particle
            {
                X = body.Position.X + System.Math.Cos(angle) * dist,
                Y = body.Position.Y + System.Math.Sin(angle) * dist,
                VX = (float)(System.Math.Cos(angle + System.Math.PI) * speed),
                VY = (float)(System.Math.Sin(angle + System.Math.PI) * speed),
                Life = 1.0,
                MaxLife = 0.5 + _rand.NextDouble() * 0.5,
                Size = 2 + _rand.NextDouble() * 4,
                Color = GetParticleColor(body.BodyType)
            };
            _particles.Add(particle);
        }
    }

    private Color GetParticleColor(BodyType type) => type switch
    {
        BodyType.Normal => Color.FromRgb(79, 195, 247),
        BodyType.Bouncy => Color.FromRgb(129, 199, 132),
        BodyType.Heavy => Color.FromRgb(255, 183, 77),
        BodyType.Explosive => Color.FromRgb(239, 83, 80),
        BodyType.Repulsor => Color.FromRgb(186, 104, 200),
        BodyType.GravityWell => Color.FromRgb(38, 198, 218),
        BodyType.AntiGravity => Color.FromRgb(0, 188, 212),
        BodyType.Freezer => Color.FromRgb(79, 195, 247),
        BodyType.Turbo => Color.FromRgb(255, 238, 88),
        BodyType.Phantom => Color.FromRgb(179, 136, 255),
        BodyType.Spike => Color.FromRgb(244, 67, 54),
        BodyType.Glue => Color.FromRgb(118, 255, 3),
        BodyType.Plasma => Color.FromRgb(236, 64, 122),
        BodyType.BlackHole => Color.FromRgb(171, 71, 188),
        BodyType.Lightning => Color.FromRgb(255, 202, 40),
        BodyType.Fire => Color.FromRgb(255, 87, 34),
        BodyType.Angel => Color.FromRgb(255, 255, 255),
        BodyType.Molly => Color.FromRgb(255, 64, 129),
        _ => Color.FromRgb(255, 255, 255)
    };

    private void UpdateBodyParticles(RigidBody body)
    {
    }

    private void UpdateParticles()
    {
        foreach (var shape in _particleShapes)
        {
            _canvas.Children.Remove(shape);
        }
        _particleShapes.Clear();

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.X += p.VX * 0.016;
            p.Y += p.VY * 0.016;
            p.Life -= 0.016;

            if (p.Life <= 0)
            {
                _particles.RemoveAt(i);
                continue;
            }

            double opacity = p.Life / p.MaxLife;
            var ellipse = new Ellipse
            {
                Width = p.Size * opacity,
                Height = p.Size * opacity,
                Fill = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), p.Color.R, p.Color.G, p.Color.B)),
                Opacity = opacity * 0.8
            };
            Canvas.SetLeft(ellipse, p.X - p.Size / 2);
            Canvas.SetTop(ellipse, p.Y - p.Size / 2);
            _canvas.Children.Add(ellipse);
            _particleShapes.Add(ellipse);
        }
    }

    private Ellipse CreateBodyShape(RigidBody body)
    {
        var colorHex = body.BodyType.GetColorHex();
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        
        Brush stroke = Brushes.White;
        double strokeThickness = 1.5;
        
        switch (body.BodyType)
        {
            case BodyType.BlackHole:
                stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AB47BC"));
                strokeThickness = 3;
                break;
            case BodyType.Spike:
                stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCDD2"));
                strokeThickness = 2;
                break;
            case BodyType.Explosive:
                stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8A80"));
                strokeThickness = 2;
                break;
            case BodyType.Lightning:
                stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD54F"));
                strokeThickness = 2;
                break;
            case BodyType.Plasma:
                stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F48FB1"));
                strokeThickness = 2;
                break;
        }

        return new Ellipse
        {
            Width = body.Radius * 2,
            Height = body.Radius * 2,
            Fill = new SolidColorBrush(Color.FromArgb(230, color.R, color.G, color.B)),
            Stroke = stroke,
            StrokeThickness = strokeThickness,
            Opacity = 0.95
        };
    }

    private void UpdateVelocityLine(RigidBody body, double x, double y)
    {
        if (!_velocityLines.TryGetValue(body.Id, out var line)) return;
        line.X1 = x;
        line.Y1 = y;
        line.X2 = x + body.Velocity.X * VelocityScale;
        line.Y2 = y + body.Velocity.Y * VelocityScale;
        double speed = body.Velocity.Length;
        line.Opacity = System.Math.Min(0.2 + speed * 0.0005, 0.5);
    }

    public void ClearDebugShapes()
    {
        foreach (var shape in _debugShapes)
        {
            _canvas.Children.Remove(shape);
        }
        _debugShapes.Clear();
    }

    public void DrawDebugPoint(Vector2 position, Color color, double size = 5)
    {
        var ellipse = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(color)
        };
        Canvas.SetLeft(ellipse, position.X - size / 2);
        Canvas.SetTop(ellipse, position.Y - size / 2);
        _canvas.Children.Add(ellipse);
        _debugShapes.Add(ellipse);
    }

    public void DrawDebugLine(Vector2 start, Vector2 end, Color color, double thickness = 1)
    {
        var line = new Line
        {
            X1 = start.X, Y1 = start.Y,
            X2 = end.X, Y2 = end.Y,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness
        };
        _canvas.Children.Add(line);
        _debugShapes.Add(line);
    }

    public void DrawDebugCircle(Vector2 center, double radius, Color color, double thickness = 1)
    {
        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(ellipse, center.X - radius);
        Canvas.SetTop(ellipse, center.Y - radius);
        _canvas.Children.Add(ellipse);
        _debugShapes.Add(ellipse);
    }

    public void DrawDebugText(string text, Vector2 position, Color color, double fontSize = 12)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(color),
            FontSize = fontSize
        };
        Canvas.SetLeft(textBlock, position.X);
        Canvas.SetTop(textBlock, position.Y);
        _canvas.Children.Add(textBlock);
        _debugShapes.Add(textBlock);
    }

    public void Clear()
    {
        _canvas.Children.Clear();
        _bodyShapes.Clear();
        _velocityLines.Clear();
        _debugShapes.Clear();
        _particles.Clear();
    }
}