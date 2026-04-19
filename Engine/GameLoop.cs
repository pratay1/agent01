namespace PhysicsSandbox.Engine;

public class GameLoop
{
    private readonly Action<double> _update;
    private readonly Action<double> _render;
    private readonly Action _lateUpdate;

    private double _accumulator;
    private readonly double _fixedDeltaTime;
    private double _deltaTime;
    private bool _isRunning;
    private bool _wasPaused;
    private int _frameCount;
    private double _time;

    public double DeltaTime => _deltaTime;
    public double Time => _time;
    public int FrameCount => _frameCount;
    public bool IsRunning => _isRunning;
    public double TargetFPS => 1.0 / _fixedDeltaTime;

    public event Action? OnFixedUpdate;
    public event Action? OnLateUpdate;

    public GameLoop(Action<double> update, Action<double> render, Action? lateUpdate = null, double targetFps = 60.0)
    {
        _update = update ?? throw new ArgumentNullException(nameof(update));
        _render = render ?? throw new ArgumentNullException(nameof(render));

        _lateUpdate = () =>
        {
            lateUpdate?.Invoke();
            OnLateUpdate?.Invoke();
        };

        _fixedDeltaTime = 1.0 / targetFps;
        _accumulator = 0;
        _deltaTime = 0;
        _frameCount = 0;
        _isRunning = false;
    }

    public void Start()
    {
        _isRunning = true;
        _accumulator = 0;
        _time = 0;
        _frameCount = 0;
    }

    public void Stop()
    {
        _isRunning = false;
    }

    public void Tick(double deltaTime)
    {
        if (!_isRunning) return;

        _deltaTime = System.Math.Min(deltaTime, 0.25);
        _accumulator += _deltaTime;
        _time += _deltaTime;

        while (_accumulator >= _fixedDeltaTime)
        {
            _update(_fixedDeltaTime);
            OnFixedUpdate?.Invoke();
            _accumulator -= _fixedDeltaTime;
        }

        _render(_deltaTime);
        _lateUpdate();
        _frameCount++;
    }

    public double GetInterpolation()
    {
        return _accumulator / _fixedDeltaTime;
    }

    public void SetPaused(bool paused)
    {
        if (_wasPaused && !paused)
        {
            _accumulator = 0;
        }
        _wasPaused = paused;
    }
}