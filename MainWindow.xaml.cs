using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    private double _canvasWidth = 1140;
    private double _canvasHeight = 600;
    private int _fpsCounter;
    private double _lastFpsTime;
    private double _currentFps;
    private double _tickRate = 1000;
    private int _tickCounter;
    private double _lastTickTime;
    
    private BodyType _selectedBodyType = BodyType.Normal;
    private RigidBody? _grabbedBody;
    private Point _grabOffset;
    private bool _isPaused = false;

    public MainWindow()
    {
        DebugLog.Init();
        DebugLog.Log("MainWindow initializing...");
        
        try
        {
            InitializeComponent();
            DebugLog.Log("InitializeComponent completed");
        }
        catch (Exception ex)
        {
            DebugLog.LogError("InitializeComponent failed", ex);
            throw;
        }

        _canvasWidth = 1140;
        _canvasHeight = 600;

        _world = new PhysicsWorld();
        _renderer = new Renderer(GameCanvas);
        _inputHandler = new InputHandler();
        _gameLoop = new GameLoop(OnUpdate, OnRender, null, 60.0);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1) };
        _timer.Tick += Timer_Tick;

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

        for (int i = 0; i < 3; i++)
            SpawnRandomBody();

        _lastFpsTime = 0;
    }

    private void SpawnRandomBody()
    {
        double x = 100 + Random.Shared.NextDouble() * (_canvasWidth - 200);
        double y = 50 + Random.Shared.NextDouble() * 150;
        double radius = 10 + Random.Shared.NextDouble() * 25;
        double mass = radius * 0.5 + Random.Shared.NextDouble() * 5;
        _world.CreateBody(new Vector2(x, y), radius, mass, 0.3 + Random.Shared.NextDouble() * 0.5);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        try
        {
            double deltaTime = 1.0 / 60.0;
            _gameLoop.Tick(deltaTime);
            CalculateFps(deltaTime);
            UpdateStatus();
            
            if (_grabbedBody != null)
            {
                var mousePos = _inputHandler.GetMousePosition();
                _grabbedBody.Position = new Vector2((float)(mousePos.X - _grabOffset.X), (float)(mousePos.Y - _grabOffset.Y));
                _grabbedBody.Velocity = Vector2.Zero;
            }
        }
        catch (Exception ex)
        {
            DebugLog.LogError("Timer_Tick failed", ex);
        }
    }

    private void CalculateFps(double deltaTime)
    {
        _lastFpsTime += deltaTime;
        _fpsCounter++;
        
        _tickCounter++;
        _lastTickTime += deltaTime;
        if (_lastTickTime >= 1.0)
        {
            _tickRate = _tickCounter;
            _tickCounter = 0;
            _lastTickTime = 0;
        }

        if (_lastFpsTime >= 1.0 && _lastFpsTime > 0)
        {
            _currentFps = _fpsCounter / _lastFpsTime;
            _fpsCounter = 0;
            _lastFpsTime = 0;
        }
    }

    private void OnUpdate(double dt) 
    {
        try
        {
            if (!_isPaused)
                _world.Step(dt);
        }
        catch (Exception ex)
        {
            DebugLog.LogError("OnUpdate physics failed", ex);
        }
    }

    private void OnRender(double dt)
    {
        _renderer.UpdateBodies(_world.Bodies);
    }

    private void UpdateStatus()
    {
        string status = $"{_world.Bodies.Count} bodies | Tick: {_tickRate}/1000";
        if (_isPaused) status += " | PAUSED";
        StatusText.Text = status;
    }

    private void SelectBodyType(BodyType type) => _selectedBodyType = type;

    #region Body Buttons
    private void NormalBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Normal);
    private void BouncyBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Bouncy);
    private void HeavyBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Heavy);
    private void ExplosiveBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Explosive);
    private void RepulsorBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Repulsor);
    private void GravityWellBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.GravityWell);
    private void AntiGravityBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.AntiGravity);
    private void FreezerBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Freezer);
    private void TurboBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Turbo);
    private void PhantomBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Phantom);
    private void SpikeBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Spike);
    private void GlueBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Glue);
    private void PlasmaBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Plasma);
    private void BlackHoleBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.BlackHole);
    private void LightningBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Lightning);
    private void AngelBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Angel);
    private void MollyBodyButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SelectBodyType(BodyType.Molly);
    #endregion

    private void OnMouseLeftDown(Point position)
    {
        _grabbedBody = null;
        foreach (var body in _world.Bodies)
        {
            if (Vector2.Distance(body.Position, new Vector2((float)position.X, (float)position.Y)) < body.Radius)
            {
                _grabbedBody = body;
                _grabOffset = new Point(position.X - body.Position.X, position.Y - body.Position.Y);
                return;
            }
        }
        SpawnBodyInternal(position, Vector2.Zero);
    }

    private void OnMouseLeftUp(Point position)
    {
        _grabbedBody = null;
    }

    private void SpawnBodyInternal(Point position, Vector2 velocity)
    {
        double radius, mass, restitution;
        switch (_selectedBodyType)
        {
            case BodyType.Bouncy: radius = 12; mass = 6; restitution = 0.95; break;
            case BodyType.Heavy: radius = 20; mass = 30; restitution = 0.15; break;
            case BodyType.Explosive: radius = 20; mass = 8; restitution = 0.4; break;
            case BodyType.Repulsor: radius = 16; mass = 8; restitution = 0.6; break;
            case BodyType.GravityWell: radius = 18; mass = 8; restitution = 0.3; break;
            case BodyType.AntiGravity: radius = 16; mass = 5; restitution = 0.7; break;
            case BodyType.Freezer: radius = 15; mass = 10; restitution = 0.3; break;
            case BodyType.Turbo: radius = 10; mass = 3; restitution = 0.8; break;
            case BodyType.Phantom: radius = 18; mass = 4; restitution = 0.5; break;
            case BodyType.Spike: radius = 14; mass = 7; restitution = 0.98; break;
            case BodyType.Glue: radius = 17; mass = 12; restitution = 0.02; break;
            case BodyType.Plasma: radius = 12; mass = 4; restitution = 0.6; break;
            case BodyType.BlackHole: radius = 15; mass = 15; restitution = 0; break;
            case BodyType.Lightning: radius = 14; mass = 4; restitution = 0.7; break;
            case BodyType.Angel: radius = 18; mass = 4; restitution = 0.6; break;
            case BodyType.Molly: radius = 16; mass = 7; restitution = 0.4; break;
            default: radius = 15; mass = 10; restitution = 0.5; break;
        }
        var body = _world.CreateBody(new Vector2((float)position.X, (float)position.Y), (float)radius, (float)mass, (float)restitution);
        body.BodyType = _selectedBodyType;
        body.Velocity = velocity;
    }

    private void OnMouseRightClick(Point position)
    {
        if (position.X < 0 || position.X > _canvasWidth || position.Y < 0 || position.Y > _canvasHeight) return;
        _world.ForceManager.Explosion.Trigger(new Vector2(position.X, position.Y));
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        _world.IsPaused = _isPaused;
        UpdatePauseUI();
    }

    private void UpdatePauseUI()
    {
        if (_isPaused)
        {
            PauseButton.Background = new SolidColorBrush(Color.FromRgb(229, 57, 53));
            PauseButton.Foreground = new SolidColorBrush(Colors.White);
            PauseButton.Content = "PAUSED";
        }
        else
        {
            PauseButton.Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
            PauseButton.Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
            PauseButton.Content = "PAUSE";
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e) => TogglePause();
    private void ClearWorld() => _world.Clear();
    private void ToggleGravity() => _world.ToggleGravityDirection();
    private void ToggleWind() { _world.ForceManager.Wind.IsActive = !_world.ForceManager.Wind.IsActive; if (_world.ForceManager.Wind.IsActive) { _world.ForceManager.Wind.Direction = new Vector2(1, 0); _world.ForceManager.Wind.Strength = 200; } }
    private void DecreaseTimeScale() => _world.TimeScale = System.Math.Max(0.1, _world.TimeScale - 0.1);
    private void IncreaseTimeScale() => _world.TimeScale = System.Math.Min(2.0, _world.TimeScale + 0.1);

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) { TogglePause(); return; }
        _inputHandler.HandleKeyDown(e.Key);
    }

    private void Window_KeyUp(object sender, KeyEventArgs e) => _inputHandler.HandleKeyUp(e.Key);
    private void Window_MouseMove(object sender, MouseEventArgs e) => _inputHandler.HandleMouseMove(e.GetPosition(GameCanvas));
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(GameCanvas);
        _grabbedBody = null;
        foreach (var body in _world.Bodies)
        {
            if (Vector2.Distance(body.Position, new Vector2((float)position.X, (float)position.Y)) < body.Radius)
            {
                _grabbedBody = body;
                _grabOffset = new Point(position.X - body.Position.X, position.Y - body.Position.Y);
                return;
            }
        }
        SpawnBodyInternal(position, Vector2.Zero);
    }
    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => _grabbedBody = null;
    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e) => _inputHandler.HandleMouseRightDown(e.GetPosition(GameCanvas));
    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e) => _inputHandler.HandleMouseRightUp(e.GetPosition(GameCanvas));
}