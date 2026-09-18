using System;
namespace Starship.Physics;
public sealed class Wind {
    private const double JetAlt = 11e3, JetWidth = 4e3, SurfTop = 1.2e3;
    public const double TopAlt = 60e3, GustTop = 40e3, GustFull = 15e3;
    private double _jet, _surf;
    private readonly bool _still;
    private readonly double[] _layAlt = new double[4], _layThick = new double[4], _laySpeed = new double[4];
    private readonly double[] _gW = new double[4], _gK = new double[4], _gP = new double[4], _gA = new double[4];
    private double _gust, _fcK = 1, _fcB;
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
        if (h < SurfTop) v += _surf * (1 - 0.65 * h / SurfTop);
        else v += _surf * 0.35 * Math.Exp(-(h - SurfTop) / 4e3);
        for (int i = 0; i < 4; i++) {
            if (_layThick[i] <= 0 || _laySpeed[i] == 0) continue;
            double t = (h - _layAlt[i]) / _layThick[i];
            v += _laySpeed[i] * Math.Exp(-t * t);
        }
        if (h > TopAlt - 10e3) v *= (TopAlt - h) / 10e3;
        return v;
    }
    public void Sound(double k, double b) { _fcK = k; _fcB = b; }
    public double Forecast(double h) => _still || double.IsNaN(h) || h < 0 || h > TopAlt ? 0 : At(h) * _fcK + _fcB;
    public void Gusts(double k, uint seed) {
        var rng = new Rng();
        rng.Seed(unchecked(seed * 2246822519u + 3266489917u));
        double norm = 0;
        for (int i = 0; i < 4; i++) {
            _gW[i] = 2 * Math.PI / (4 + rng.Next() * 30);
            _gK[i] = 2 * Math.PI / (300 + rng.Next() * 1500);
            _gP[i] = rng.Next() * 2 * Math.PI;
            _gA[i] = 0.5 + rng.Next();
            norm += _gA[i] * _gA[i] / 2;
        }
        for (int i = 0; i < 4; i++) _gA[i] /= Math.Sqrt(norm);
        _gust = k;
    }
    public double At(double h, double t) {
        double v = At(h);
        if (_gust <= 0 || _still || double.IsNaN(h) || h < 0 || h >= GustTop) return v;
        double s = 0;
        for (int i = 0; i < 4; i++) s += _gA[i] * Math.Sin(_gW[i] * t + _gK[i] * h + _gP[i]);
        double fade = h < GustFull ? 1 : (GustTop - h) / (GustTop - GustFull);
        return v + _gust * (0.6 + 0.08 * Math.Abs(v)) * fade * s;
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
