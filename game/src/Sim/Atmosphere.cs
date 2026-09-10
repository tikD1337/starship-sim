using System;
namespace Starship.Physics;
public readonly struct Air {
    public readonly double Rho, P, T, A;
    public Air(double rho, double p, double t, double a) { Rho = rho; P = p; T = t; A = a; }
}
public static class Atmosphere {
    private static readonly double[,] Layers = {
        {     0, 288.15, -0.0065, 101325      },
        { 11000, 216.65,  0,       22632.06   },
        { 20000, 216.65,  0.001,   5474.889   },
        { 32000, 228.65,  0.0028,  868.0187   },
        { 47000, 270.65,  0,       110.9063   },
        { 51000, 270.65, -0.0028,  66.93887   },
        { 71000, 214.65, -0.002,   3.956420   },
        { 84852, 186.95,  0,       0.3734     },
    };
    private static readonly double[] HighH = {
        90e3, 100e3, 110e3, 120e3, 130e3, 150e3, 180e3, 200e3, 250e3, 300e3,
        350e3, 400e3, 450e3, 500e3, 600e3, 700e3, 800e3, 900e3, 1000e3,
    };
    private static readonly double[] HighRho = {
        3.4160e-6, 5.6040e-7, 9.7080e-8, 2.2220e-8, 8.1520e-9, 2.0760e-9, 5.1940e-10,
        2.7890e-10, 7.2480e-11, 2.4180e-11, 9.5180e-12, 3.7250e-12, 1.5850e-12,
        6.9670e-13, 1.4540e-13, 3.6140e-14, 1.1700e-14, 5.2450e-15, 3.0190e-15,
    };
    private const double ExoT = 1000, BaseT = 195.08, ThermoScale = 40e3, TopScale = 181045;
    private static int Below(double[] tab, double x, int n) {
        int lo = 0, hi = n - 2;
        while (lo < hi) {
            int mid = (lo + hi + 1) / 2;
            if (x >= tab[mid]) lo = mid; else hi = mid - 1;
        }
        return lo;
    }
    private static int BelowRow(double[,] tab, double x) {
        int lo = 0, hi = tab.GetLength(0) - 1;
        while (lo < hi) {
            int mid = (lo + hi + 1) / 2;
            if (x >= tab[mid, 0]) lo = mid; else hi = mid - 1;
        }
        return lo;
    }
    private static double HighTemp(double h) {
        if (h <= 100e3) return Const.Lerp(186.95, BaseT, (h - 90e3) / 10e3);
        return ExoT - (ExoT - BaseT) * Math.Exp(-(h - 100e3) / ThermoScale);
    }
    private static double HighRhoAt(double h) {
        int n = HighH.Length;
        if (h >= HighH[n - 1])
            return HighRho[n - 1] * Math.Exp(-(h - HighH[n - 1]) / TopScale);
        int i = Below(HighH, h, n);
        double t = (h - HighH[i]) / (HighH[i + 1] - HighH[i]);
        return Math.Exp(Math.Log(HighRho[i]) + t * (Math.Log(HighRho[i + 1]) - Math.Log(HighRho[i])));
    }
    public static Air At(double h, double rhoK = 1.0) {
        if (double.IsNaN(h)) h = 0;
        if (h > 90000) {
            double th = HighTemp(h);
            double rh = HighRhoAt(h);
            return new Air(rh * rhoK, rh * Const.RAIR * th, th, Math.Sqrt(1.4 * Const.RAIR * th));
        }
        if (h < 0) h = 0;
        int li = BelowRow(Layers, h);
        double h0 = Layers[li, 0], t0 = Layers[li, 1], lr = Layers[li, 2], p0 = Layers[li, 3];
        double t = t0 + lr * (h - h0);
        double p = lr == 0
            ? p0 * Math.Exp(-Const.G0 * (h - h0) / (Const.RAIR * t0))
            : p0 * Math.Pow(t / t0, -Const.G0 / (lr * Const.RAIR));
        if (h > 84852) { p = 0.3734 * Math.Exp(-(h - 84852) / 7200); t = 186.95; }
        double rho = p / (Const.RAIR * t);
        return new Air(rho * rhoK, p, t, Math.Sqrt(1.4 * Const.RAIR * t));
    }
    private static readonly double[,] CdTab = {
        { 0, 0.28 }, { 0.6, 0.28 }, { 0.85, 0.35 }, { 1.05, 0.62 }, { 1.3, 0.58 },
        { 2, 0.40 }, { 3, 0.30 }, { 5, 0.24 }, { 8, 0.21 }, { 25, 0.20 },
    };
    private static readonly double[,] CdEngTab = {
        { 0, 1.00 }, { 0.8, 1.00 }, { 1.0, 1.15 }, { 1.2, 1.25 },
        { 2, 1.55 }, { 3, 1.75 }, { 4, 1.85 }, { 5, 1.85 },
    };
    private static double CdTable(double[,] tab, double mach) {
        double m = Math.Abs(mach);
        int n = tab.GetLength(0);
        if (double.IsNaN(m)) return tab[0, 1];
        for (int i = 1; i < n; i++) {
            if (m <= tab[i, 0]) {
                double m0 = tab[i - 1, 0], c0 = tab[i - 1, 1];
                double m1 = tab[i, 0], c1 = tab[i, 1];
                return Const.Lerp(c0, c1, (m - m0) / (m1 - m0));
            }
        }
        return tab[n - 1, 1];
    }
    public static double CdEngine(double mach) => CdTable(CdEngTab, mach);
    public static double CdAxial(double mach, double axialProj)
        => axialProj < 0 ? CdEngine(mach) : Cd0(mach);
    public static double Cd0(double mach) => CdTable(CdTab, mach);
}
