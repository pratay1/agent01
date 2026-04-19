namespace PhysicsSandbox.Math;

public struct Vector2
{
    public double X { get; set; }
    public double Y { get; set; }

    public Vector2(double x, double y) => (X, Y) = (x, y);

    public static Vector2 Zero => new(0, 0);
    public static Vector2 One => new(1, 1);
    public static Vector2 Up => new(0, -1);
    public static Vector2 Down => new(0, 1);
    public static Vector2 Left => new(-1, 0);
    public static Vector2 Right => new(1, 0);

    public double Length => System.Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;

    public Vector2 Normalized
    {
        get
        {
            double len = Length;
            return len > 0 ? new Vector2(X / len, Y / len) : Zero;
        }
    }

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator -(Vector2 a) => new(-a.X, -a.Y);
    public static Vector2 operator *(Vector2 a, double s) => new(a.X * s, a.Y * s);
    public static Vector2 operator *(double s, Vector2 a) => new(a.X * s, a.Y * s);
    public static Vector2 operator /(Vector2 a, double s) => new(a.X / s, a.Y / s);
    public static bool operator ==(Vector2 a, Vector2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Vector2 a, Vector2 b) => a.X != b.X || a.Y != b.Y;

    public static double Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;
    public static double Distance(Vector2 a, Vector2 b) => (a - b).Length;
    public static double DistanceSquared(Vector2 a, Vector2 b) => (a - b).LengthSquared;

    public static Vector2 Lerp(Vector2 a, Vector2 b, double t) => a + (b - a) * t;
    public static Vector2 Reflect(Vector2 v, Vector2 n) => v - 2 * Dot(v, n) * n;

    public Vector2 Perpendicular() => new(-Y, X);

    public override bool Equals(object? obj) => obj is Vector2 v && this == v;
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X:F2}, {Y:F2})";
}