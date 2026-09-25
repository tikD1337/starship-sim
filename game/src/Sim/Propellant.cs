using System;
namespace Starship.Physics;
public static class Propellant {
    public const double XI = 1.841;
    public static readonly double OxVol = Pump.MR / Pump.RHO_OX / (Pump.MR / Pump.RHO_OX + 1 / Pump.RHO_F);
    public static double Main(Vehicle v) => Math.Max(0, v.Prop - Math.Min(v.HdrLeft, v.Prop));
    public static double MainFill(Vehicle v) => Const.Clamp(Main(v) / Math.Max(v.PropMax - v.Hdr, 1), 0, 1);
    public static (double Z0, double H) TankOf(Vehicle v, bool ox) {
        double col = 0.76 * v.Len, z0 = 0.12 * v.Len;
        return ox ? (z0, col * OxVol) : (z0 + col * OxVol, col * (1 - OxVol));
    }
    public static double Feed(Vehicle v) => v.OnHeader ? 1 : v.Settled;
    public static double UllageF(Vehicle v) => Rcs.UllageF(v);
    public static double UllageThrust(Vehicle v, double m) => Math.Min(UllageF(v), m * Const.ULLAGE_A);
    public static double SettleLeft(Vehicle v) {
        if (Feed(v) >= Const.SETTLE_GO) return 0;
        double a = UllageThrust(v, v.Mass) / v.Mass;
        return SettleTau(v, a) * Math.Log((1 - v.Settled) / (1 - Const.SETTLE_GO));
    }
    public static void Sequence(Vehicle v) {
        bool want = v.Ign && v.Prop > 0 && UllageF(v) > 0;
        if (!want) v.Ullage = false;
        else if (!v.Ullage) v.Ullage = v.NRun == 0 && Feed(v) < Const.SETTLE_GO;
        else v.Ullage = v.F < Const.ULLAGE_OFF;
        v.IgnHold = v.Ullage && v.NRun == 0 && Feed(v) < Const.SETTLE_GO;
    }
    public static double SettleTau(Vehicle v, double a) {
        double gap = Math.Max(v.Dia / 2, (1 - MainFill(v)) * TankOf(v, true).H);
        return Const.Clamp(Math.Sqrt(2 * gap / Math.Max(a, 1e-3)), Const.SETTLE_TMIN, Const.SETTLE_TMAX);
    }
    public static void Settle(Vehicle v, double a, double dt) {
        v.AAx = a;
        bool on = a > Const.SETTLE_A;
        double tgt = on ? 1 : 0, tau = on ? SettleTau(v, a) : Const.UNSETTLE_T;
        v.Settled += (tgt - v.Settled) * (1 - Math.Exp(-dt / tau));
        if (Math.Abs(v.Settled - tgt) < 1e-4) v.Settled = tgt;
    }
    public static double Omega(Vehicle v, bool ox) {
        double R = v.Dia / 2, h = MainFill(v) * TankOf(v, ox).H;
        return Math.Sqrt(XI * Math.Max(0, v.AAx) * Math.Tanh(XI * h / R) / R);
    }
    public static bool Sloshing(Vehicle v) {
        double f = MainFill(v);
        return v.AAx > Const.SETTLE_A && f >= 0.02 && f <= 0.995;
    }
    public static double SloshMass(Vehicle v, bool ox) {
        double R = v.Dia / 2, h = MainFill(v) * TankOf(v, ox).H;
        if (h <= 0) return 0;
        return Main(v) * (ox ? Pump.MR : 1) / (1 + Pump.MR) * 2 * Math.Tanh(XI * h / R) / (XI * (XI * XI - 1) * h / R);
    }
    public static double Trim(Vehicle v, double aSd) {
        if (!Sloshing(v)) return 0;
        double T = 0, lim = Const.SLOSH_MAX * v.Dia / 2;
        for (int j = 0; j < 2; j++) {
            double w = Omega(v, j == 0);
            if (w > 0) T += SloshMass(v, j == 0) * v.AAx * Const.Clamp(-aSd / (w * w), -lim, lim);
        }
        return v.Settled * T;
    }
    public static double Slosh(Vehicle v, double cm, double aSd, double dt) {
        double R = v.Dia / 2, f = MainFill(v), T = 0;
        bool on = Sloshing(v);
        for (int j = 0; j < 2; j++) {
            bool ox = j == 0;
            if (!on) {
                double k = Math.Exp(-dt / 2);
                v.SloshY[j] *= k;
                v.SloshV[j] *= k;
                continue;
            }
            (double z0, double H) = TankOf(v, ox);
            double h = f * H, m1 = SloshMass(v, ox), w = Omega(v, ox);
            double d = z0 + h - 2 * R / XI * Math.Tanh(XI * h / (2 * R)) - cm;
            double acc = -w * w * v.SloshY[j] - 2 * Const.SLOSH_ZETA * w * v.SloshV[j] - (aSd + v.OmDot * d);
            v.SloshV[j] += acc * dt;
            v.SloshY[j] += v.SloshV[j] * dt;
            double lim = Const.SLOSH_MAX * R;
            if (Math.Abs(v.SloshY[j]) > lim) {
                v.SloshY[j] = Math.Sign(v.SloshY[j]) * lim;
                v.SloshV[j] = 0;
                acc = 0;
            }
            T += v.Settled * m1 * (v.AAx * v.SloshY[j] - acc * d);
        }
        v.SloshT = T;
        return T;
    }
    public static double Head(Vehicle v, bool fuel) {
        double f = MainFill(v), hOx = TankOf(v, true).H;
        double h = fuel ? hOx + f * TankOf(v, false).H : f * hOx;
        return (fuel ? Pump.RHO_F : Pump.RHO_OX) * Math.Max(0, v.AAx) * h * v.Settled;
    }
}
