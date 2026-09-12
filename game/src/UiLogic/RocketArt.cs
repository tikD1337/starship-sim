using System;
using System.Collections.Generic;
namespace Starship.Game.Ui;
public readonly record struct Pt(double X, double Y);
public sealed record Part(uint Fill, Pt[] Pts, uint Line = 0, double Width = 0);
public static class RocketArt {
    public const double ShipLen = 54.6;
    private const uint Hull = 0xD9DEE4FF, Lit = 0xEEF1F4FF, Mid = 0xB5BEC8FF, Dark = 0x9CA6B1FF,
                       Tile = 0x1C2026FF, TileEdge = 0x3A434EFF, Flap = 0x262C34FF, FlapEdge = 0x4A5562FF,
                       FinL = 0x5E6772FF, FinR = 0x4E5661FF, Ring = 0x7F8893FF, Slot = 0x262B31FF,
                       Skirt = 0xA2ABB5FF, SkirtDark = 0x89939EFF, SkirtLine = 0x5D6570FF,
                       Bell = 0x6E7782FF, Weld = 0xA9B2BCFF, ChineL = 0xC3CAD2FF, ChineR = 0x8F99A5FF;
    private static readonly uint[] FlameInk = { 0xFFD9C461, 0xFFE7D6CC, 0xFFFBF5FF };
    private static readonly double[][] Shapes = {
        new[] { 4.2, 10.0, 10.0, 12.5, 26.0, 7.0, 42.0, 0.0, 48.0 },
        new[] { 3.8, 7.0, 8.0, 8.0, 21.0, 4.5, 33.0, 0.0, 37.0 },
        new[] { 3.2, 3.9, 7.0, 3.6, 15.0, 1.6, 21.0, 0.0, 23.0 },
    };
    public static uint FlameColor(int layer) => FlameInk[Math.Clamp(layer, 0, 2)];
    private static Part Rect(uint fill, double x, double y, double w, double h) =>
        new(fill, new[] { new Pt(x, y), new Pt(x + w, y), new Pt(x + w, y + h), new Pt(x, y + h) });
    private static Part Poly(uint fill, uint line, double width, params double[] xy) {
        var p = new Pt[xy.Length / 2];
        for (int i = 0; i < p.Length; i++) p[i] = new Pt(xy[2 * i], xy[2 * i + 1]);
        return new Part(fill, p, line, width);
    }
    private static Part Stroke(uint line, double width, double x0, double y0, double x1, double y1) =>
        new(0, new[] { new Pt(x0, y0), new Pt(x1, y1) }, line, width);
    private static void Cubic(List<Pt> to, Pt a, Pt c1, Pt c2, Pt b, int n) {
        for (int i = 1; i <= n; i++) {
            double t = (double)i / n, u = 1 - t;
            to.Add(new Pt(u * u * u * a.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * b.X,
                          u * u * u * a.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * b.Y));
        }
    }
    private static void Quad(List<Pt> to, Pt a, Pt c, Pt b, int n) {
        for (int i = 1; i <= n; i++) {
            double t = (double)i / n, u = 1 - t;
            to.Add(new Pt(u * u * a.X + 2 * u * t * c.X + t * t * b.X,
                          u * u * a.Y + 2 * u * t * c.Y + t * t * b.Y));
        }
    }
    private static Part Nose(bool right) {
        double k = right ? 1 : -1;
        var p = new List<Pt> { new(0, 0) };
        Cubic(p, new Pt(0, 0), new Pt(k * 2.2, 1.5), new Pt(k * 4.5, 8), new Pt(k * 4.5, 18), 10);
        p.Add(new Pt(k > 0 ? 0.2 : 0.2, 18));
        p.Add(new Pt(0.2, 0.25));
        return new Part(right ? Tile : Hull, p.ToArray());
    }
    private static Part Wing(double k, double x0, double y0, double x1, double y1,
                             double x2, double y2, double x3, double y3) =>
        Poly(Flap, FlapEdge, 0.3, k * x0, y0, k * x1, y1, k * x2, y2, k * x3, y3);
    public static Part[] Ship() {
        var l = new List<Part> { Rect(Ring, -4.5, 52, 9, 2.6) };
        for (int i = 0; i < 6; i++) l.Add(Rect(Slot, -3.9 + i * 1.4, 52.5, 0.7, 1.6));
        l.Add(Wing(-1, 4.5, 37.6, 8.3, 41.6, 8.5, 50.6, 4.5, 51.9));
        l.Add(Wing(1, 4.5, 37.6, 8.3, 41.6, 8.5, 50.6, 4.5, 51.9));
        l.Add(Rect(Hull, -4.5, 18, 4.7, 34));
        l.Add(Rect(Lit, -3.7, 18, 1.4, 34));
        l.Add(Rect(Tile, 0.2, 18, 4.3, 34));
        l.Add(Rect(TileEdge, 4.05, 18, 0.45, 34));
        l.Add(Nose(false));
        l.Add(Nose(true));
        l.Add(Wing(-1, 3.35, 8.6, 6.3, 11.2, 6.5, 16.6, 4.45, 17.3));
        l.Add(Wing(1, 3.35, 8.6, 6.3, 11.2, 6.5, 16.6, 4.45, 17.3));
        return l.ToArray();
    }
    public static Part[] Booster() {
        (double X0, double X1, double D)[] bells = {
            (-4.1, -3.1, 70.8), (-2.65, -1.65, 71.0), (-1.2, -0.2, 71.1),
            (0.2, 1.2, 71.1), (1.65, 2.65, 71.0), (3.1, 4.1, 70.8),
        };
        var l = new List<Part>();
        foreach ((double x0, double x1, double d) in bells)
            l.Add(Poly(Bell, 0, 0, x0, 67.8, x1, 67.8, x1 + 0.2, d, x0 - 0.2, d));
        l.Add(Poly(ChineL, 0, 0, -4.5, 37.4, -5.15, 39.9, -5.15, 58.4, -4.5, 60.9));
        l.Add(Poly(ChineR, 0, 0, 4.5, 37.4, 5.15, 39.9, 5.15, 58.4, 4.5, 60.9));
        l.Add(Rect(Hull, -4.5, 0, 9, 61.6));
        l.Add(Rect(Lit, -3.7, 0, 1.4, 61.6));
        l.Add(Rect(Mid, 1.7, 0, 2.8, 61.6));
        l.Add(Rect(Dark, 3.7, 0, 0.8, 61.6));
        foreach (double y in new[] { 11.4, 31.4, 47.4 }) l.Add(Stroke(Weld, 0.16, -4.5, y, 4.5, y));
        l.Add(Rect(Skirt, -4.5, 61.6, 9, 6.3));
        l.Add(Rect(SkirtDark, 2.2, 61.6, 2.3, 6.3));
        l.Add(Stroke(SkirtLine, 0.2, -4.5, 61.6, 4.5, 61.6));
        l.Add(Rect(FinL, -7.4, 2, 2.9, 5));
        l.Add(Rect(FinR, 4.5, 2, 2.9, 5));
        return l.ToArray();
    }
    public static Part[] Stack() {
        var l = new List<Part>();
        foreach (Part p in Booster()) {
            var pts = new Pt[p.Pts.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Pt(p.Pts[i].X, p.Pts[i].Y + ShipLen);
            l.Add(p with { Pts = pts });
        }
        l.AddRange(Ship());
        return l.ToArray();
    }
    public static Pt[] Flame(double len, int layer) {
        double[] s = Shapes[Math.Clamp(layer, 0, 2)];
        double k = len / 45.0;
        var p = new List<Pt> { new(-s[0] * k, 0) };
        Cubic(p, new Pt(-s[0] * k, 0), new Pt(-s[1] * k, s[2] * k), new Pt(-s[3] * k, s[4] * k),
              new Pt(-s[5] * k, s[6] * k), 8);
        Quad(p, new Pt(-s[5] * k, s[6] * k), new Pt(s[7] * k, s[8] * k), new Pt(s[5] * k, s[6] * k), 8);
        Cubic(p, new Pt(s[5] * k, s[6] * k), new Pt(s[3] * k, s[4] * k), new Pt(s[1] * k, s[2] * k),
              new Pt(s[0] * k, 0), 8);
        return p.ToArray();
    }
}
