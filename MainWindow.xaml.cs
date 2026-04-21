using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PhysicsSandbox.Engine;
using PhysicsSandbox.Math;
using PhysicsSandbox.Physics;
using PhysicsSandbox.Rendering;

namespace PhysicsSandbox;

public partial class MainWindow : Window
{
    private readonly PhysicsWorld _world;
    private readonly Renderer _renderer;
    private readonly GameLoop _gameLoop;

    private double _canvasWidth = 1280;
    private double _canvasHeight = 680; // Subtract bottom bar height
    
    private BodyType _selectedBodyType = BodyType.Normal;
    private RigidBody? _grabbedBody;
    private Point _grabOffset;
    private bool _isPaused = false;
    
    private readonly Dictionary<BodyType, (string Name, string Color)> _bodyInfo = new()
    {
        { BodyType.Normal, ("Normal", "#4FC3F7") },
        { BodyType.Bouncy, ("Bouncy", "#81C784") },
        { BodyType.Heavy, ("Heavy", "#FFB74D") },
        { BodyType.Explosive, ("Explosive", "#EF5350") },
        { BodyType.Repulsor, ("Repulsor", "#BA68C8") },
        { BodyType.GravityWell, ("Gravity Well", "#26C6DA") },
        { BodyType.AntiGravity, ("Anti Gravity", "#00BCD4") },
        { BodyType.Freezer, ("Freezer", "#81D4FA") },
        { BodyType.Turbo, ("Turbo", "#FFEB3B") },
        { BodyType.Phantom, ("Phantom", "#9575CD") },
        { BodyType.Spike, ("Spike", "#F44336") },
        { BodyType.Glue, ("Glue", "#AED581") },
        { BodyType.Plasma, ("Plasma", "#E91E63") },
        { BodyType.BlackHole, ("Black Hole", "#1A1A1A") },
        { BodyType.Lightning, ("Lightning", "#FF9800") },
        { BodyType.Fire, ("Fire", "#FF5722") },
        { BodyType.Angel, ("Angel", "#FFFFFF") },
        { BodyType.Molly, ("Molly", "#FF4081") }
    };

    public MainWindow()
    {
        DebugLog.Init();
        
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            DebugLog.LogError("InitializeComponent failed", ex);
            throw;
        }

        _world = new PhysicsWorld();
        _renderer = new Renderer(GameCanvas);
        _gameLoop = new GameLoop(OnUpdate, OnRender, 60.0);

        SetupInput();
        InitializeBodyTypeButtons();
        SelectBody(BodyType.Normal);
        StartGame();
    }

    private void SelectBody(BodyType type)
    {
        _selectedBodyType = type;
        UpdateSelectedBodyUI();
    }

    private void UpdateSelectedBodyUI()
    {
        var (name, colorHex) = _bodyInfo[_selectedBodyType];
        SelectedBodyName.Text = name;
        SelectedBodyColor.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        
        // Update button states
        foreach (BodyType type in Enum.GetValues<BodyType>())
        {
            var btnName = $"Btn{type}";
            var btn = FindName(btnName) as System.Windows.Controls.Button;
            if (btn != null)
            {
                btn.Background = type == _selectedBodyType 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(_bodyInfo[type].Color)) 
                    : new SolidColorBrush(Color.FromRgb(31, 31, 31));
            }
        }
    }

    private void InitializeBodyTypeButtons()
    {
        foreach (BodyType type in Enum.GetValues<BodyType>())
        {
            var btnName = $"Btn{type}";
            var btn = FindName(btnName) as System.Windows.Controls.Button;
            if (btn != null)
            {
                btn.Content = _bodyInfo[type].Name;
            }
        }
    }

    private void BodyTypeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tag)
        {
            if (Enum.TryParse<BodyType>(tag, out var type))
            {
                SelectBody(type);
            }
        }
    }

    private void SetupInput()
    {
        // Keyboard shortcuts
        KeyDown += (s, e) =>
        {
            switch (e.Key)
            {
                case Key.Space: TogglePause(); break;
                case Key.C: ClearWorld(); break;
                case Key.G: ToggleGravity(); break;
                case Key.OemComma: ToggleWind(); break;
                case Key.OemPlus: _world.TimeScale = System.Math.Min(2.0, _world.TimeScale + 0.1); break;
                case Key.OemMinus: _world.TimeScale = System.Math.Max(0.1, _world.TimeScale - 0.1); break;
                case Key.D1: SelectBody(BodyType.Normal); break;
                case Key.D2: SelectBody(BodyType.Bouncy); break;
                case Key.D3: SelectBody(BodyType.Heavy); break;
                case Key.D4: SelectBody(BodyType.Explosive); break;
                case Key.D5: SelectBody(BodyType.Repulsor); break;
                case Key.D6: SelectBody(BodyType.GravityWell); break;
                case Key.D7: SelectBody(BodyType.AntiGravity); break;
                case Key.D8: SelectBody(BodyType.Freezer); break;
                case Key.D9: SelectBody(BodyType.Turbo); break;
                case Key.D0: SelectBody(BodyType.Phantom); break;
                case Key.Q: SelectBody(BodyType.Spike); break;
                case Key.W: SelectBody(BodyType.Glue); break;
                case Key.E: SelectBody(BodyType.Plasma); break;
                case Key.R: SelectBody(BodyType.BlackHole); break;
                case Key.T: SelectBody(BodyType.Lightning); break;
                case Key.Y: SelectBody(BodyType.Fire); break;
                case Key.U: SelectBody(BodyType.Angel); break;
                case Key.I: SelectBody(BodyType.Molly); break;
            }
        };

        // Mouse handling
        GameCanvas.MouseLeftButtonDown += (s, e) =>
        {
            var pos = e.GetPosition(GameCanvas);
            
            // Try grab existing body
            _grabbedBody = null;
            foreach (var body in _world.Bodies)
            {
                if (Vector2.Distance(body.Position, new Vector2(pos.X, pos.Y)) < body.Radius)
                {
                    _grabbedBody = body;
                    _grabOffset = new Point(pos.X - body.Position.X, pos.Y - body.Position.Y);
                    return;
                }
            }
            
            // Spawn new body
            SpawnBody(pos);
        };

        GameCanvas.MouseLeftButtonUp += (s, e) => _grabbedBody = null;
        
        GameCanvas.MouseMove += (s, e) =>
        {
            if (_grabbedBody != null)
            {
                var pos = e.GetPosition(GameCanvas);
                _grabbedBody.Position = new Vector2(pos.X - _grabOffset.X, pos.Y - _grabOffset.Y);
                _grabbedBody.Velocity = Vector2.Zero;
            }
        };

        GameCanvas.MouseRightButtonDown += (s, e) =>
        {
            var pos = e.GetPosition(GameCanvas);
            _world.ForceManager.Explosion.Trigger(new Vector2(pos.X, pos.Y));
        };
    }

    private void StartGame()
    {
        _gameLoop.Start();
        _world.SetBoundaries(0, _canvasWidth, _canvasHeight);

        // Spawn initial bodies
        for (int i = 0; i < 3; i++)
            SpawnRandomBody();
    }

    private void SpawnRandomBody()
    {
        double x = 100 + Random.Shared.NextDouble() * (_canvasWidth - 200);
        double y = 50 + Random.Shared.NextDouble() * 150;
        double radius = 10 + Random.Shared.NextDouble() * 25;
        double mass = radius * 0.5 + Random.Shared.NextDouble() * 5;
        _world.CreateBody(new Vector2(x, y), radius, mass, 0.3 + Random.Shared.NextDouble() * 0.5);
    }

    private void SpawnBody(Point position)
    {
        var behavior = Behaviors.BodyBehaviorFactory.Get(_selectedBodyType);
        var body = _world.CreateBody(
            new Vector2(position.X, position.Y),
            behavior.DefaultRadius,
            behavior.DefaultMass,
            behavior.DefaultRestitution,
            _selectedBodyType
        );
    }

    private void OnUpdate(double dt) 
    {
        if (!_isPaused)
            _world.Step(dt);
    }

    private void OnRender(double dt)
    {
        _renderer.UpdateBodies(_world.Bodies, dt);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        string status = $"{_world.Bodies.Count} bodies";
        if (_isPaused) status += " | PAUSED";
        StatusText.Text = status;
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        _world.IsPaused = _isPaused;
        PauseButton.Content = _isPaused ? "RESUME" : "PAUSE";
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e) => TogglePause();
    private void ClearButton_Click(object sender, RoutedEventArgs e) => ClearWorld();
    private void ClearWorld() => _world.Clear();
    private void ToggleGravity() => _world.ToggleGravityDirection();
    private void ToggleWind() 
    { 
        _world.ForceManager.Wind.IsActive = !_world.ForceManager.Wind.IsActive; 
        if (_world.ForceManager.Wind.IsActive) 
        { 
            _world.ForceManager.Wind.Direction = new Vector2(1, 0); 
            _world.ForceManager.Wind.Strength = 200; 
        } 
    }

    protected override void OnClosed(EventArgs e)
    {
        _gameLoop.Stop();
        DebugLog.Shutdown();
        base.OnClosed(e);
    }
}