using System;
using System.Collections.Generic;
namespace Starship.Game.Ui;
public readonly record struct Spot(double X, double Y, double R);
public static class EngineLayout {
    public static Spot[] Booster() {
        var s = new List<Spot>(33);
        (int n, double r, double a0)[] rings = {
            (3, 40, -Math.PI / 2), (10, 108, -Math.PI / 2 + 0.31), (20, 180, -Math.PI / 2),
        };
        foreach ((int n, double r, double a0) in rings)
            for (int i = 0; i < n; i++) {
                double a = a0 + 2 * Math.PI * i / n;
                s.Add(new Spot(r * Math.Cos(a), r * Math.Sin(a), 24));
            }
        return s.ToArray();
    }
    public static Spot[] Ship() {
        var s = new Spot[6];
        for (int i = 0; i < 3; i++) {
            double a = -Math.PI / 2 + 2 * Math.PI * i / 3, b = Math.PI / 2 + 2 * Math.PI * i / 3;
            s[i] = new Spot(62 * Math.Cos(a), 62 * Math.Sin(a), 26);
            s[i + 3] = new Spot(150 * Math.Cos(b), 150 * Math.Sin(b), 40);
        }
        return s;
    }
    public static Spot[] Of(bool ship) => ship ? Ship() : Booster();
    public static int Hit(Spot[] s, double x, double y) {
        for (int i = 0; i < s.Length; i++) {
            double dx = x - s[i].X, dy = y - s[i].Y, r = s[i].R + 4;
            if (dx * dx + dy * dy <= r * r) return i;
        }
        return -1;
    }
}
