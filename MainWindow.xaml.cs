using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PhysicsSandbox.Engine;
using PhysicsSandbox.Mathematics;
using PhysicsSandbox.Physics;
using PhysicsSandbox.Rendering;

namespace PhysicsSandbox;

public partial class MainWindow : Window
{
    private PhysicsWorld _world;
    private Renderer _renderer;
    private GameLoop _gameLoop;

    private double _canvasWidth;
    private double _canvasHeight;
    
    private BodyType _selectedBodyType = BodyType.Normal;
    private RigidBody? _grabbedBody;
    private Point _grabOffset;
    private bool _isPaused = false;
    
    // Shift-click spawning
    private bool _isShiftSpawning = false;
    private double _shiftSpawnAccumulator = 0.0;
    private Point _shiftSpawnPosition;
    private const double ShiftSpawnRate = 14.0; // objects per second
    private const int MaxShiftSpawnsPerFrame = 10;
    
    private readonly Dictionary<BodyType, (string Name, string Color)> _bodyInfo = new()
    {
        { BodyType.Normal, ("Normal", "#4FC3F7") },
        { BodyType.Bouncy, ("Bouncy", "#81C784") },
        { BodyType.Heavy, ("Heavy", "#FFB74D") },
        { BodyType.Explosive, ("Explosive", "#EF5350") },
        { BodyType.Repulsor, ("Repulsor", "#BA68C8") },
        { BodyType.GravityWell, ("Gravity Well", "#26C6DA") },
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
            Logger.LogInfo("Starting main window initialization");
            InitializeComponent();
            Logger.LogInfo("Main window initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError("InitializeComponent failed", ex);
            throw;
        }

        // Setup - we'll initialize these when game starts
        Loaded += (s, e) => OnGameLoaded();
    }

    private void OnGameLoaded()
    {
        try
        {
            Logger.LogInfo("Game loaded, initializing components");
            // Initialize game components when window is ready
            GameCanvas.SizeChanged += (s, e) => UpdateCanvasSize();
            
            _world = new PhysicsWorld();
            _renderer = new Renderer();
            GameCanvas.Renderer = _renderer;
            _gameLoop = new GameLoop(OnUpdate, OnRender, 60.0);
            
            SetupInput();
            InitializeBodyTypeButtons();
            SelectBody(BodyType.Normal);
            
            Logger.LogInfo("Game components initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to initialize game components", ex);
            throw;
        }
    }

    private bool _gameStarted = false;

    private void UpdateCanvasSize()
    {
        if (GameCanvas == null) return;
        _canvasWidth = GameCanvas.ActualWidth;
        _canvasHeight = GameCanvas.ActualHeight;
        
        if (_canvasWidth > 0 && _canvasHeight > 0)
        {
            _world.SetCanvasSize(_canvasWidth, _canvasHeight);
            if (!_gameStarted)
            {
                _gameStarted = true;
                StartGame();
            }
        }
    }

    private string GetBodyTypeDescription()
    {
        return _selectedBodyType switch
        {
            BodyType.Normal => "Standard physics body. Basic collision with normal bounce.",
            BodyType.Bouncy => "Super bouncy! Collides with all bodies and bounces with high restitution. Great for ricochets.",
            BodyType.Heavy => "Massive weight! Pushes lighter bodies around on impact. High mass reduces velocity change.",
            BodyType.Explosive => "BOOM! Explodes on contact with any body, creating a radial blast force.",
            BodyType.Repulsor => "Pushes everything away with strong repulsive force. Creates an anti-gravity field.",
            BodyType.GravityWell => "Attracts nearby bodies with gravitational pull. Like a mini black hole that pulls things in.",
            BodyType.Turbo => "Accelerates continuously in direction of movement. Goes supersonic fast!",
            BodyType.Phantom => "Phases through walls and bounces off bodies. Applies strong shake force on collision.",
            BodyType.Spike => "Bouncy with explosive contact. Spawns debris shards on impact radially.",
            BodyType.Glue => "Sticks to first body it touches. Bonds two bodies together permanently.",
            BodyType.Plasma => "Zaps nearby bodies with electric chains. Creates lightning arcs to closest targets.",
            BodyType.BlackHole => "Sucks in everything with extreme gravity. Grows larger as it consumes bodies!",
            BodyType.Lightning => "Zaps nearest body with electric force. Chain lightning can jump between targets.",
            BodyType.Fire => "Rising flames that disappear after 3 seconds. No direct collision effects.",
            BodyType.Angel => "Flies periodically with gentle upward force. Very light mass.",
            BodyType.Molly => "Explodes on contact unless Angel is nearby. Attracts and latches to Angel bodies.",
            _ => "Unknown body type"
        };
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
        DescriptionText.Text = GetBodyTypeDescription();

        // Update button states: selected uses body color, unselected are muted
        foreach (BodyType type in Enum.GetValues<BodyType>())
        {
            var btnName = $"Btn{type}";
            var btn = FindName(btnName) as System.Windows.Controls.Button;
            if (btn != null)
            {
                bool isSelected = type == _selectedBodyType;
                var (_, btnColorHex) = _bodyInfo[type];

                var bg = isSelected
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(btnColorHex))
                    : new SolidColorBrush(Color.FromRgb(26, 26, 26)); // #1A1A1A

                var border = isSelected
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(btnColorHex))
                    : new SolidColorBrush(Color.FromRgb(42, 42, 42)); // #2A2A2A

                var fg = isSelected
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Color.FromRgb(170, 170, 170)); // #AAAAAA

                btn.Background = bg;
                btn.BorderBrush = border;
                btn.Foreground = fg;
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
            }
        };

        // Mouse handling
        GameCanvas.MouseLeftButtonDown += (s, e) =>
        {
            var pos = e.GetPosition(GameCanvas);
            
            // Check if Shift is held for rapid spawning
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                _isShiftSpawning = true;
                _shiftSpawnPosition = pos;
                _shiftSpawnAccumulator = 0.0;
                SpawnBody(pos); // Spawn immediately on click
                return;
            }
            
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

        GameCanvas.MouseLeftButtonUp += (s, e) => 
        {
            _grabbedBody = null;
            _isShiftSpawning = false;
        };
        
        GameCanvas.MouseMove += (s, e) =>
        {
            if (_grabbedBody != null)
            {
                var pos = e.GetPosition(GameCanvas);
                _grabbedBody.Position = new Vector2(pos.X - _grabOffset.X, pos.Y - _grabOffset.Y);
                _grabbedBody.Velocity = Vector2.Zero;
            }
            
            // Update spawn position while shift+clicking
            if (_isShiftSpawning)
            {
                _shiftSpawnPosition = e.GetPosition(GameCanvas);
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

        // Spawn initial bodies
        for (int i = 0; i < 3; i++)
            SpawnRandomBody();
    }

    private void SpawnRandomBody()
    {
        var behavior = Behaviors.BodyBehaviorFactory.Get(BodyType.Normal);
        double x = 100 + Random.Shared.NextDouble() * (_canvasWidth - 200);
        double y = 50 + Random.Shared.NextDouble() * 150;
        double radius = behavior.DefaultRadius + Random.Shared.NextDouble() * 5;
        double mass = behavior.DefaultMass + Random.Shared.NextDouble() * 3;
        double restitution = behavior.DefaultRestitution + (Random.Shared.NextDouble() - 0.5) * 0.2;
        _world.CreateBody(new Vector2(x, y), radius, mass, System.Math.Clamp(restitution, 0.1, 0.9));
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
        {
            // Handle shift+click rapid spawning
            if (_isShiftSpawning)
            {
                _shiftSpawnAccumulator += dt;
                double spawnInterval = 1.0 / ShiftSpawnRate; // ~0.333 seconds
                int spawnsThisFrame = 0;
                while (_shiftSpawnAccumulator >= spawnInterval && spawnsThisFrame < MaxShiftSpawnsPerFrame)
                {
                    SpawnBody(_shiftSpawnPosition);
                    _shiftSpawnAccumulator -= spawnInterval;
                    spawnsThisFrame++;
                }
            }
            
            _world.Step(dt);
        }
    }

    private void OnRender(double dt)
    {
        _renderer.Update(_world.Bodies, dt);
        GameCanvas.InvalidateVisual();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        string status = $"{_world.Bodies.Count} bodies";
        if (_isPaused) status += " | PAUSED";
        status += $" | Speed: {_world.TimeScale:F1}x";
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
    private void ClearWorld()
    {
        _world.Clear();
        _renderer.Clear();
    }
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

    private void Start2DPhysics_Click(object sender, RoutedEventArgs e)
    {
        TitleScreen.Visibility = Visibility.Collapsed;
        GameUI.Visibility = Visibility.Visible;
        StartGame();
    }

    private void Quit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    protected override void OnClosed(EventArgs e)
    {
        _gameLoop.Stop();
        DebugLog.Shutdown();
        base.OnClosed(e);
    }
}
