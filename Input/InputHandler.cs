using System.Windows;
using System.Windows.Input;
using PhysicsSandbox.Math;

namespace PhysicsSandbox.Input;

public class InputHandler
{
    private readonly Dictionary<Key, Action> _keyDownActions = new();
    private readonly Dictionary<Key, Action> _keyUpActions = new();
    private readonly HashSet<Key> _pressedKeys = new();
    private Point _mousePosition;
    private Point _previousMousePosition;
    private bool _isMouseLeftDown;
    private bool _isMouseRightDown;
    private bool _isMouseLeftPressed;
    private bool _isMouseRightPressed;

    public Point MousePosition => _mousePosition;
    public Point PreviousMousePosition => _previousMousePosition;
    public bool IsMouseLeftDown => _isMouseLeftDown;
    public bool IsMouseRightDown => _isMouseRightDown;
    public bool IsMouseLeftPressed => _isMouseLeftPressed;
    public bool IsMouseRightPressed => _isMouseRightPressed;

    public event Action<Point>? OnMouseLeftClick;
    public event Action<Point>? OnMouseRightClick;
    public event Action<Point>? OnMouseLeftHold;
    public event Action<Point>? OnMouseMove;
    public event Action<Point>? OnMouseLeftDown;
    public event Action<Point>? OnMouseLeftUp;

    public void Clear()
    {
        _keyDownActions.Clear();
        _keyUpActions.Clear();
        _pressedKeys.Clear();
    }

    public void RegisterKeyDown(Key key, Action action)
    {
        _keyDownActions[key] = action;
    }

    public void RegisterKeyUp(Key key, Action action)
    {
        _keyUpActions[key] = action;
    }

    public void HandleKeyDown(Key key)
    {
        if (_keyDownActions.TryGetValue(key, out var action))
        {
            action();
        }
        _pressedKeys.Add(key);
    }

    public void HandleKeyUp(Key key)
    {
        if (_keyUpActions.TryGetValue(key, out var action))
        {
            action();
        }
        _pressedKeys.Remove(key);
    }

    public bool IsKeyDown(Key key)
    {
        return _pressedKeys.Contains(key);
    }

    public void HandleMouseMove(Point position)
    {
        _previousMousePosition = _mousePosition;
        _mousePosition = position;

        if (_mousePosition != _previousMousePosition)
        {
            OnMouseMove?.Invoke(_mousePosition);
        }
    }

    public void HandleMouseLeftDown(Point position)
    {
        _mousePosition = position;
        _isMouseLeftDown = true;
        _isMouseLeftPressed = true;
        OnMouseLeftDown?.Invoke(position);
    }

    public void HandleMouseLeftUp(Point position)
    {
        _mousePosition = position;
        _isMouseLeftDown = false;
        OnMouseLeftClick?.Invoke(position);
        OnMouseLeftUp?.Invoke(position);
    }

    public void HandleMouseRightDown(Point position)
    {
        _mousePosition = position;
        _isMouseRightDown = true;
        _isMouseRightPressed = true;
    }

    public void HandleMouseRightUp(Point position)
    {
        _mousePosition = position;
        _isMouseRightDown = false;
        OnMouseRightClick?.Invoke(position);
    }

    public void Update()
    {
        _isMouseLeftPressed = false;
        _isMouseRightPressed = false;

        if (_isMouseLeftDown)
        {
            OnMouseLeftHold?.Invoke(_mousePosition);
        }
    }
}