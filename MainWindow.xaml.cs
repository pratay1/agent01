using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PhysicsSandbox.Engine;
using PhysicsSandbox.Input;
using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;
using PhysicsSandbox.Rendering;

namespace PhysicsSandbox;

public partial class MainWindow : Window
{
    private readonly PhysicsWorld _world;
    private readonly Renderer _renderer;
    private readonly InputHandler _inputHandler;
    private readonly GameLoop _gameLoop;
    private readonly DispatcherTimer _timer;

    private double _canvasWidth = 1280;
    private double _canvasHeight = 600;
    private int _fpsCounter;
    private double _lastFpsTime;
    private double _currentFps;
    
    // Toolbar state
    private bool _isToolbarVisible = true;
    private BodyType _selectedBodyType = BodyType.Normal;
    
    // Fling mechanics
    private bool _isDragging = false;
    private Point _dragStartPosition;
    private DateTime _dragStartTime;

public MainWindow()
{
    InitializeComponent();

    _canvasWidth = 1280;
    _canvasHeight = 600;

    _world = new PhysicsWorld();
    _renderer = new Renderer(GameCanvas);
    _inputHandler = new InputHandler();
    _gameLoop = new GameLoop(
        OnUpdate,
        OnRender,
        null,
        60.0
    );

    _timer = new DispatcherTimer
    {
        Interval = TimeSpan.FromMilliseconds(1)
    };
    _timer.Tick += Timer_Tick;

    // Initialize toolbar
    UpdateToolbarVisibility(true);
    SelectBodyType(BodyType.Normal);

    SetupInput();
    StartGame();
}

private void SetupInput()
{
    _inputHandler.RegisterKeyDown(Key.Space, TogglePause);
    _inputHandler.RegisterKeyDown(Key.C, ClearWorld);
    _inputHandler.RegisterKeyDown(Key.G, ToggleGravity);
    _inputHandler.RegisterKeyDown(Key.W, ToggleWind);
    _inputHandler.RegisterKeyDown(Key.OemPlus, IncreaseTimeScale);
    _inputHandler.RegisterKeyDown(Key.OemMinus, DecreaseTimeScale);

    _inputHandler.OnMouseLeftDown += OnMouseLeftDown;
    _inputHandler.OnMouseLeftUp += OnMouseLeftUp;
    _inputHandler.OnMouseRightClick += OnMouseRightClick;
}

    private void StartGame()
    {
        _timer.Start();
        _gameLoop.Start();

        _world.SetBoundaries(0, _canvasWidth, _canvasHeight);

        _world.CreateBody(new Vector2(_canvasWidth / 2, _canvasHeight / 2), 30, 50, 0.7);

        for (int i = 0; i < 10; i++)
        {
            SpawnRandomBody();
        }

        _lastFpsTime = 0;
    }

    private void SpawnRandomBody()
    {
        double x = 100 + Random.Shared.NextDouble() * (_canvasWidth - 200);
        double y = 50 + Random.Shared.NextDouble() * 150;
        double radius = 10 + Random.Shared.NextDouble() * 25;
        double mass = radius * 0.5 + Random.Shared.NextDouble() * 5;

        _world.CreateBody(
            new Vector2(x, y),
            radius,
            mass,
            0.3 + Random.Shared.NextDouble() * 0.5
        );
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var start = DateTime.Now;

        double deltaTime = 1.0 / 60.0;
        _gameLoop.Tick(deltaTime);

        CalculateFps(deltaTime);
        UpdateStatus();
    }

    private void CalculateFps(double deltaTime)
    {
        _lastFpsTime += deltaTime;
        _fpsCounter++;

        if (_lastFpsTime >= 1.0)
        {
            _currentFps = _fpsCounter / _lastFpsTime;
            _fpsCounter = 0;
            _lastFpsTime = 0;
        }
    }

    private void OnUpdate(double dt)
    {
        _world.Step(dt);
    }

    private void OnRender(double dt)
    {
        _renderer.UpdateBodies(_world.Bodies);
    }

private void UpdateStatus()
{
    string status = $"Bodies: {_world.Bodies.Count} | FPS: {_currentFps:F0}";

    if (_world.IsPaused)
        status += " | PAUSED";

    Vector2 gravity = _world.Gravity;
    if (gravity.Y > 0)
        status += " | Gravity: DOWN";
    else if (gravity.Y < 0)
        status += " | Gravity: UP";

    if (_world.ForceManager.Wind.IsActive)
        status += " | Wind: ON";

    status += $" | Time: {_world.TimeScale:F1}x";

    StatusText.Text = status;
}

#region Toolbar Event Handlers

private void ToolbarToggleButton_Click(object sender, RoutedEventArgs e)
{
    _isToolbarVisible = !_isToolbarVisible;
    UpdateToolbarVisibility(_isToolbarVisible);
}

private void UpdateToolbarVisibility(bool isVisible)
{
    var toolbar = (Border)this.FindName("BodyToolbar");
    if (toolbar != null)
    {
        toolbar.Visibility = isVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }
}

private void SelectBodyType(BodyType type)
{
    _selectedBodyType = type;
    
    // Reset all button backgrounds
    var normalBtn = (Border)this.FindName("NormalBodyButton");
    var bouncyBtn = (Border)this.FindName("BouncyBodyButton");
    var heavyBtn = (Border)this.FindName("HeavyBodyButton");
    var explosiveBtn = (Border)this.FindName("ExplosiveBodyButton");
    var repulsorBtn = (Border)this.FindName("RepulsorBodyButton");
    
    if (normalBtn != null) normalBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 58, 58, 69));
    if (bouncyBtn != null) bouncyBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 58, 58, 69));
    if (heavyBtn != null) heavyBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 58, 58, 69));
    if (explosiveBtn != null) explosiveBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 58, 58, 69));
    if (repulsorBtn != null) repulsorBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 58, 58, 69));
    
    // Highlight selected button
    switch (type)
    {
        case BodyType.Normal:
            if (normalBtn != null) normalBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 79, 195, 247));
            break;
        case BodyType.Bouncy:
            if (bouncyBtn != null) bouncyBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 129, 199, 132));
            break;
        case BodyType.Heavy:
            if (heavyBtn != null) heavyBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 183, 77));
            break;
        case BodyType.Explosive:
            if (explosiveBtn != null) explosiveBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 229, 115, 115));
            break;
        case BodyType.Repulsor:
            if (repulsorBtn != null) repulsorBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 186, 104, 200));
            break;
    }
}

private void BodyButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
{
    if (sender is Border border)
    {
        border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 58, 58, 69)); // Slightly lighter
    }
}

private void BodyButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
{
    if (sender is Border border)
    {
        // Reset to default color based on body type
        border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 58, 58, 69));
    }
}

private void NormalBodyButton_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    SelectBodyType(BodyType.Normal);
}

private void BouncyBodyButton_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    SelectBodyType(BodyType.Bouncy);
}

private void HeavyBodyButton_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    SelectBodyType(BodyType.Heavy);
}

private void ExplosiveBodyButton_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    SelectBodyType(BodyType.Explosive);
}

private void RepulsorBodyButton_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    SelectBodyType(BodyType.Repulsor);
}

#endregion

#region Fling Mechanics

private void OnMouseLeftDown(Point position)
{
    if (position.X < 0 || position.X > _canvasWidth ||
        position.Y < 0 || position.Y > _canvasHeight)
        return;
        
    _isDragging = true;
    _dragStartPosition = position;
    _dragStartTime = DateTime.Now;
}

private void OnMouseLeftUp(Point position)
{
    if (!_isDragging) return;
    
    _isDragging = false;
    
    // Calculate drag duration and distance
    var dragEndTime = DateTime.Now;
    var dragDuration = (dragEndTime - _dragStartTime).TotalSeconds;
    var dragDistance = Point.Subtract(position, _dragStartPosition);
    
    // Only fling if drag was quick enough (less than 0.3 seconds) and far enough
    if (dragDuration < 0.3 && dragDistance.Length > 5.0)
    {
        // Calculate velocity based on drag distance and time
        var velocity = new Vector2(
            dragDistance.X / (float)dragDuration * 100, 
            dragDistance.Y / (float)dragDuration * 100);
            
        // Cap maximum velocity
        const float maxVelocity = 1000f;
        if (velocity.Length > maxVelocity)
        {
            velocity = velocity.Normalized * maxVelocity;
        }
        
        // Spawn body with initial velocity
        SpawnBodyAtPositionWithVelocity(position, velocity);
    }
    else
    {
        // Normal click - just spawn body at position
        SpawnBodyAtPosition(position);
    }
}

private void SpawnBodyAtPosition(Point position)
{
    // Use the selected body type from toolbar
    SpawnBodyInternal(position, 0, 0);
}

private void SpawnBodyAtPositionWithVelocity(Point position, Vector2 velocity)
{
    // Use the selected body type from toolbar
    SpawnBodyInternal(position, velocity.X, velocity.Y);
}

private void SpawnBodyInternal(Point position, double initialVelocityX = 0, double initialVelocityY = 0)
{
    // Set properties based on selected body type
    double radius = 15;
    double mass = 10;
    double restitution = 0.5;
    BodyType bodyType = _selectedBodyType;
    
    switch (bodyType)
    {
        case BodyType.Normal:
            radius = 15 + Random.Shared.NextDouble() * 10;
            mass = radius * 0.5;
            restitution = 0.5 + Random.Shared.NextDouble() * 0.3;
            break;
        case BodyType.Bouncy:
            radius = 12 + Random.Shared.NextDouble() * 8;
            mass = radius * 0.4;
            restitution = 0.8 + Random.Shared.NextDouble() * 0.15; // Very bouncy
            break;
        case BodyType.Heavy:
            radius = 18 + Random.Shared.NextDouble() * 12;
            mass = radius * 0.8; // Much heavier
            restitution = 0.2 + Random.Shared.NextDouble() * 0.2; // Less bouncy
            break;
        case BodyType.Explosive:
            radius = 20 + Random.Shared.NextDouble() * 10;
            mass = radius * 0.3;
            restitution = 0.4 + Random.Shared.NextDouble() * 0.3;
            break;
        case BodyType.Repulsor:
            radius = 16 + Random.Shared.NextDouble() * 8;
            mass = radius * 0.5;
            restitution = 0.5 + Random.Shared.NextDouble() * 0.3;
            break;
    }
    
    var body = _world.CreateBody(
        new Vector2((float)position.X, (float)position.Y),
        (float)radius,
        (float)mass,
        (float)restitution);
        
    body.BodyType = bodyType;
    
    // Apply initial velocity for fling
    if (initialVelocityX != 0 || initialVelocityY != 0)
    {
        body.Velocity = new Vector2((float)initialVelocityX, (float)initialVelocityY);
    }
}

#endregion

private void OnMouseRightClick(Point position)
{
    if (position.X < 0 || position.X > _canvasWidth ||
        position.Y < 0 || position.Y > _canvasHeight)
        return;

    _world.ForceManager.Explosion.Trigger(new Vector2(position.X, position.Y));
}

    private void TogglePause()
    {
        _world.IsPaused = !_world.IsPaused;
    }

    private void ClearWorld()
    {
        _world.Clear();
    }

    private void ToggleGravity()
    {
        _world.ToggleGravityDirection();
    }

    private void ToggleWind()
    {
        _world.ForceManager.Wind.IsActive = !_world.ForceManager.Wind.IsActive;
        if (_world.ForceManager.Wind.IsActive)
        {
            _world.ForceManager.Wind.Direction = new Vector2(1, 0);
            _world.ForceManager.Wind.Strength = 200;
        }
    }

    private void DecreaseTimeScale()
    {
        _world.TimeScale = System.Math.Max(0.1, _world.TimeScale - 0.1);
    }

    private void IncreaseTimeScale()
    {
        _world.TimeScale = System.Math.Min(2.0, _world.TimeScale + 0.1);
    }

    private void OnMouseLeftClick(Point position)
    {
        if (position.X < 0 || position.X > _canvasWidth ||
            position.Y < 0 || position.Y > _canvasHeight)
            return;

        double radius = 15 + Random.Shared.NextDouble() * 20;
        double mass = radius * 0.5;

        _world.CreateBody(
            new Vector2(position.X, position.Y),
            radius,
            mass,
            0.5 + Random.Shared.NextDouble() * 0.3
        );
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        _inputHandler.HandleKeyDown(e.Key);
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        _inputHandler.HandleKeyUp(e.Key);
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(GameCanvas);
        _inputHandler.HandleMouseMove(position);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(GameCanvas);
        _inputHandler.HandleMouseLeftDown(position);
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(GameCanvas);
        _inputHandler.HandleMouseLeftUp(position);
    }

    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(GameCanvas);
        _inputHandler.HandleMouseRightDown(position);
    }

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(GameCanvas);
        _inputHandler.HandleMouseRightUp(position);
    }
}