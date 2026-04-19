using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;

namespace PhysicsSandbox.Rendering;

public class Renderer
{
    private readonly Canvas _canvas;
    private readonly Dictionary<int, Ellipse> _bodyShapes = new();
    private readonly Dictionary<int, Line> _velocityLines = new();
    private readonly HashSet<UIElement> _debugShapes = new();

    private const double VelocityScale = 0.1;

    public Renderer(Canvas canvas)
    {
        _canvas = canvas;
        _canvas.Background = new SolidColorBrush(Color.FromRgb(30, 30, 35));
    }

    public void ClearDebugShapes()
    {
        foreach (var shape in _debugShapes)
        {
            _canvas.Children.Remove(shape);
        }
        _debugShapes.Clear();
    }

    public void UpdateBodies(IEnumerable<RigidBody> bodies)
    {
        var activeIds = new HashSet<int>();

        foreach (var body in bodies)
        {
            activeIds.Add(body.Id);

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
                    Opacity = 0.5
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
    }

    private Ellipse CreateBodyShape(RigidBody body)
    {
        Color baseColor;
        if (body.Mass < 1)
            baseColor = Color.FromRgb(255, 100, 100);
        else if (body.Mass < 5)
            baseColor = Color.FromRgb(100, 200, 255);
        else if (body.Mass < 20)
            baseColor = Color.FromRgb(100, 255, 150);
        else
            baseColor = Color.FromRgb(255, 200, 100);

        return new Ellipse
        {
            Width = body.Radius * 2,
            Height = body.Radius * 2,
            Fill = new SolidColorBrush(Color.FromArgb(200, baseColor.R, baseColor.G, baseColor.B)),
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            Opacity = 0.9
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
        line.Opacity = System.Math.Min(0.3 + speed * 0.001, 0.8);
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
            X1 = start.X,
            Y1 = start.Y,
            X2 = end.X,
            Y2 = end.Y,
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
    }

    public void DrawUI(string text)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            FontSize = 11,
            FontFamily = new FontFamily("Consolas")
        };
        Canvas.SetLeft(textBlock, 10);
        Canvas.SetTop(textBlock, 10);
        _canvas.Children.Add(textBlock);
    }
}