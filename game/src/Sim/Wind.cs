using System;
namespace Starship.Physics;
public sealed class Wind {
    private const double JetAlt = 11e3, JetWidth = 4e3, SurfTop = 1.2e3;
    public const double TopAlt = 60e3;
    private double _jet, _surf;
    private readonly bool _still;
    private readonly double[] _layAlt = new double[4], _layThick = new double[4], _laySpeed = new double[4];
    public double Jet => _jet;
    public double Surface => _surf;
    private static double Norm(Rng rng) {
        double s = 0;
        for (int i = 0; i < 12; i++) s += rng.Next();
        return s - 6.0;
    }
    public static Wind Roll(Rng rng) {
        var w = new Wind();
        w._jet = 25 + Norm(rng) * 12;
        w._surf = 4 + Norm(rng) * 3;
        if (rng.Next() < 0.5) w._jet = -w._jet;
        if (rng.Next() < 0.5) w._surf = -w._surf;
        for (int i = 0; i < 4; i++) {
            w._layAlt[i] = 2e3 + rng.Next() * 26e3;
            w._layThick[i] = 900 + rng.Next() * 2600;
            w._laySpeed[i] = (rng.Next() * 2 - 1) * 6;
        }
        return w;
    }
    private Wind(bool still) { _still = still; }
    public Wind() { }
    public static Wind Calm() => new Wind(true);
    public static Wind Steady(double surf, double jet = 0) {
        var w = new Wind();
        w._surf = surf;
        w._jet = jet;
        return w;
    }
    public double At(double h) {
        if (_still || double.IsNaN(h) || h < 0 || h > TopAlt) return 0;
        double d = (h - JetAlt) / JetWidth;
        double v = _jet * Math.Exp(-d * d);
        if (h < SurfTop) v += _surf * (1 - h / SurfTop);
        else v += _surf * 0.35 * Math.Exp(-(h - SurfTop) / 4e3);
        for (int i = 0; i < 4; i++) {
            if (_layThick[i] <= 0 || _laySpeed[i] == 0) continue;
            double t = (h - _layAlt[i]) / _layThick[i];
            v += _laySpeed[i] * Math.Exp(-t * t);
        }
        if (h > TopAlt - 10e3) v *= (TopAlt - h) / 10e3;
        return v;
    }
    public double MaxShear(double from, double to) {
        double worst = 0;
        for (double h = from; h < to; h += 100) {
            double s = Math.Abs(At(h + 500) - At(h)) / 0.5;
            if (s > worst) worst = s;
        }
        return worst;
    }
}
