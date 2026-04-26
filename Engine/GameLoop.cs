using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace PhysicsSandbox.Engine;

public class GameLoop
{
    private readonly Action<double> _update;
    private readonly Action<double> _render;
    private readonly DispatcherTimer _timer;
    private readonly double _fixedDeltaTime;
    
    private double _accumulator;
    private double _lastTime;
    private bool _isRunning;
    private double _lastFrameTimeMs;

    public double TargetFPS => 1.0 / _fixedDeltaTime;
    public bool IsRunning => _isRunning;
    public double LastTickMs => _lastFrameTimeMs;

    public GameLoop(Action<double> update, Action<double> render, double targetFps = 60.0)
    {
        _update = update ?? throw new ArgumentNullException(nameof(update));
        _render = render ?? throw new ArgumentNullException(nameof(render));
        
        _fixedDeltaTime = 1.0 / targetFps;
        
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.0 / targetFps)
        };
        _timer.Tick += (s, e) => Tick();
    }

    public void Start()
    {
        _isRunning = true;
        _accumulator = 0;
        _lastTime = Stopwatch.GetTimestamp();
        _timer.Start();
    }

    public void Stop()
    {
        _isRunning = false;
        _timer.Stop();
    }

    private void Tick()
    {
        if (!_isRunning) return;

        double currentTime = Stopwatch.GetTimestamp();
        double frameTime = (currentTime - _lastTime) / Stopwatch.Frequency;
        _lastTime = currentTime;
        _lastFrameTimeMs = frameTime * 1000.0;
        
        _accumulator += frameTime;

        // Fixed timestep updates
        int maxSteps = (int)Math.Ceiling(0.1 / _fixedDeltaTime);
        int steps = 0;
        while (_accumulator >= _fixedDeltaTime && steps < maxSteps)
        {
            _update(_fixedDeltaTime);
            _accumulator -= _fixedDeltaTime;
            steps++;
        }
        // Cap accumulator to prevent runaway growth if we hit maxSteps
        if (steps == maxSteps && _accumulator > _fixedDeltaTime)
        {
            _accumulator = Math.Min(_accumulator, maxSteps * _fixedDeltaTime);
        }
        // Don't call _render here - that happens after this loop at line 72

        // Render
        _render(frameTime);
    }
}
