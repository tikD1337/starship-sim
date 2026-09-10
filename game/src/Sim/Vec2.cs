using System;
namespace Starship.Physics;
public readonly struct Vec2 {
    public readonly double X, Y;
    public Vec2(double x, double y) { X = x; Y = y; }
    public double Len => Math.Sqrt(X * X + Y * Y);
    public double Dot(Vec2 o) => X * o.X + Y * o.Y;
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double k) => new(a.X * k, a.Y * k);
}
