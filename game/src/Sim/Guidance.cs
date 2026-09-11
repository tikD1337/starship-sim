using System;
namespace Starship.Physics;
public readonly struct Orbit {
    public readonly double A, E, Apo, Peri, Per;
    public Orbit(double a, double e, double apo, double peri, double per)
    { A = a; E = e; Apo = apo; Peri = peri; Per = per; }
}
public readonly struct LiftRefV {
    public readonly double X, Y, Up, East, Vp;
    public LiftRefV(double x, double y, double up, double east, double vp)
    { X = x; Y = y; Up = up; East = east; Vp = vp; }
}
public readonly struct EntryPred {
    public readonly double Miss, T;
    public EntryPred(double miss, double t) { Miss = miss; T = t; }
}
public static class Guidance {
    private static readonly double[,] PitchTab = {
        { 0, 0 }, { 50, 1.2 }, { 90, 6 }, { 160, 12 }, { 300, 21 }, { 500, 31 },
        { 800, 41 }, { 1200, 52 }, { 1700, 62 }, { 2200, 69 }, { 2800, 74 }, { 3400, 78 },
    };
    public static double PitchProg(double sp) {
        int n = PitchTab.GetLength(0);
        for (int i = 1; i < n; i++) {
            if (sp <= PitchTab[i, 0]) {
                double a = PitchTab[i - 1, 0], b = PitchTab[i - 1, 1];
                double c = PitchTab[i, 0], d = PitchTab[i, 1];
                return Const.Lerp(b, d, (sp - a) / (c - a)) * Const.D2R;
            }
        }
        return 70 * Const.D2R;
    }
    public static Orbit Orb(Vehicle v) {
        double r = v.R, v2 = v.Vx * v.Vx + v.Vy * v.Vy;
        double e0 = v2 / 2 - Const.MU / r, a = -Const.MU / (2 * e0);
        double h = v.X * v.Vy - v.Y * v.Vx;
        double e = Math.Sqrt(Math.Max(0, 1 + 2 * e0 * h * h / (Const.MU * Const.MU)));
        return new Orbit(a, e, a * (1 + e) - Const.RE, a * (1 - e) - Const.RE,
                         a > 0 ? 2 * Math.PI * Math.Sqrt(a * a * a / Const.MU) : 0);
    }
    public static double PitchOf(Vec2 d, Vehicle veh) {
        Vec2 u = veh.Up, e = veh.East;
        return Math.Atan2(d.X * e.X + d.Y * e.Y, d.X * u.X + d.Y * u.Y);
    }
    public static double AimRetro(Vehicle v) { Vec2 r = v.VRel; return PitchOf(new Vec2(-r.X, -r.Y), v); }
    public static double AimPro(Vehicle v) => PitchOf(v.VRel, v);
    public static double AimAlpha(Vehicle v, double degs) {
        double vp = AimPro(v), a = degs * Const.D2R;
        double c1 = vp - a, c2 = vp + a;
        return Math.Abs(Vehicle.AngDiff(c1, v.Th)) < Math.Abs(Vehicle.AngDiff(c2, v.Th)) ? c1 : c2;
    }
    public static LiftRefV LiftRef(Vehicle v) {
        double vp = AimPro(v);
        Vec2 u = v.Up, e = v.East;
        double s = Math.Sin(vp), c = Math.Cos(vp);
        return new LiftRefV(-u.X * s + e.X * c, -u.Y * s + e.Y * c, -s, c, vp);
    }
    public static double AimLift(Vehicle v, double degs, double sgn)
        => AimPro(v) + (sgn == 0 ? 1 : sgn) * degs * Const.D2R;
    public static double LiftSign(Vehicle v, string key, double proj) {
        if (!v.Sgn.TryGetValue(key, out double cur) || Math.Abs(proj) > 0.15) {
            cur = proj >= 0 ? 1 : -1;
            v.Sgn[key] = cur;
        }
        return cur;
    }
    public static void VentProp(SimState sim, Vehicle v, double dt) {
        double lim = Math.Max(Const.ENTRY_PROP, v.HdrLeft);
        if (v.Prop > lim + 1) {
            v.Prop = Math.Max(lim, v.Prop - 450 * dt);
            v.Venting = true;
            sim.Once("vent" + v.Tag, () =>
                sim.LogMsg($"{v.Tag}: слив остатков топлива перед входом — центровка под «брюхо»", 1));
        }
        else v.Venting = false;
    }
    public static double FlipDrift(Vehicle v) {
        double a = 3 * Math.Max(1, Spec.RaptorSL.Fv - Spec.RaptorSL.Ae * Const.P0) / v.Mass;
        const double tf = 4;
        return Const.Clamp(a * tf * tf * Const.FLIP_DRIFT_K, 150, 1200);
    }
    public static double GlideBank(double dr, double vE, double h, double vv, double lacc, double ePerp) {
        double aL = Math.Max(Math.Min(lacc * Math.Abs(ePerp), 6), 0.5);
        const double tau = 6;
        double vMax = aL * (-tau + Math.Sqrt(tau * tau + 2 * Math.Abs(dr) / aL));
        double vWant = -Math.Sign(dr) * Math.Min(vMax, 260);
        double aLat = (vWant - vE) * 0.3;
        double den = lacc * ePerp;
        double c = Math.Abs(den) > 1e-3 ? aLat / den : (aLat >= 0 ? 1 : -1);
        return Math.Acos(Const.Clamp(c, -1, 1));
    }
    public static double StopAlt(Vehicle v, int nEng) {
        double h = v.Alt, vv = v.VVert, m = v.Mass, t = 0;
        double tau = v.Ign ? 0 : Math.Max(0.05, v.Eng[0].P.Tau);
        const double dt = 0.4;
        for (int i = 0; i < 800 && vv < 0 && h > 0; i++) {
            Air at = Atmosphere.At(h);
            double spool = tau > 0 ? 1 - tau / dt * (Math.Exp(-t / tau) - Math.Exp(-(t + dt) / tau)) : 1;
            double f = spool * nEng * Math.Max(0, Spec.RaptorSL.Fv - Spec.RaptorSL.Ae * at.P);
            double d = 0.5 * at.Rho * vv * vv * v.A * 0.12;
            double a = (f + d) / m - Const.MU / ((Const.RE + h) * (Const.RE + h));
            vv += a * dt; h += vv * dt; m -= spool * nEng * Spec.RaptorSL.Mdot * dt; t += dt;
        }
        return h;
    }
    public static double LandAim(SimState sim, Vehicle v, double maxTilt, double aMaxIn) {
        double g = Const.MU / (v.R * v.R), h = v.Alt, vv = v.VVert;
        double hT = v.SeekPad ? Const.CATCH_H : 0;
        double dh = Math.Max(h - hT, 0.5);
        double tgo = Math.Max(2 * dh / Math.Max(-vv, 1), 7);
        double aVert = Math.Max(vv * vv / (2 * dh) + g, 0.5);
        if (!v.SeekPad) {
            double a2 = Const.Clamp(-v.VHor * 0.55, -3, 3);
            return Const.Clamp(Math.Atan2(a2, aVert), -maxTilt, maxTilt);
        }
        double dr = sim.Downrange(v), vh = v.VHor;
        double aMax = aMaxIn > 0 ? aMaxIn : aVert * 1.6;
        double lat = Math.Sqrt(Math.Max(aMax * aMax - aVert * aVert, 0.04));
        if (dh < 500) lat = Math.Min(lat, Const.LAND_ALAT);
        else if (dh < 3000) lat = Math.Min(lat, 6.5);
        double aLat;
        if (dh < Const.LAND_DHPD) {
            double aL = Math.Min(lat, Const.LAND_ALAT);
            double tau = v.Kd / Math.Max(v.Kp, 0.1);
            double k1 = Math.Min(aL / 35, Const.LAND_KLAT / (tau * tau));
            double k2 = Const.LAND_KDAMP * Math.Sqrt(Math.Max(k1, 1e-4));
            aLat = -k1 * dr - k2 * vh;
        }
        else aLat = -6 * (dr + vh * tgo) / (tgo * tgo) + 2 * vh / tgo;
        if (v.WindE != 0 && dh < Const.LAND_WIND_H) {
            double rho = Atmosphere.At(h, sim.RhoK).Rho;
            aLat -= Const.LAND_WIND_K * 0.5 * rho * v.WindE * Math.Abs(v.WindE)
                    * v.Dia * v.FullLen / v.Mass;
        }
        aLat = Const.Clamp(aLat, -lat, lat);
        return Const.Clamp(Math.Atan2(aLat, aVert), -maxTilt, maxTilt);
    }
    private static double LatTime(double dr, double vh, double aLat)
        => Const.LAND_TLAG + Math.Sqrt(4 * Math.Abs(dr) / aLat) + Math.Abs(vh) / aLat;
    private readonly struct Burn {
        public readonly double H, HT, DhS, Dh, G, FOne, Drag, Vv, VDes, FNow0, Dr, Tgo;
        public Burn(SimState sim, Vehicle v, int nEng) {
            H = v.Alt;
            Air at = Atmosphere.At(H, sim.RhoK);
            G = Const.MU / (v.R * v.R);
            FOne = Math.Max(1, Spec.RaptorSL.Fv - Spec.RaptorSL.Ae * at.P);
            Drag = 0.5 * at.Rho * v.VVert * v.VVert * v.A * 0.75;
            FNow0 = (v.Ign ? v.NEng : nEng) * FOne;
            HT = !v.SeekPad ? 0 : v.Kind == Kind.Booster ? Const.CATCH_H - Const.CATCH_WIN / 2 : Const.CATCH_H;
            double f = (v.Kind == Kind.Booster ? Const.LAND_B_END : nEng)
                       * Math.Max(0, Spec.RaptorSL.Fv - Spec.RaptorSL.Ae * at.P);
            double anet = Math.Max(1, f / v.Mass - G);
            VDes = -(2.5 + Math.Sqrt(2 * anet * 0.72 * Math.Max(H - HT - 8, 0)));
            Vv = v.VVert;
            DhS = H - HT;
            Dh = Math.Max(DhS, 0.5);
            Dr = v.SeekPad ? sim.Downrange(v) : 0;
            double aLat = Math.Max(0.5, Math.Min(Const.LAND_ALAT,
                nEng * FOne / v.Mass * Math.Sin(20 * Const.D2R)));
            Tgo = Math.Max(Dh / (0.5 * (Math.Max(-Vv, 0) + Const.LAND_VTD)),
                           LatTime(Dr, v.VHor, aLat));
        }
    }
    private static void Ignite(SimState sim, Vehicle v, int nEng, in Burn e) {
        double stopH = v.Kind == Kind.Ship ? Const.LAND_STOP_S : Const.CATCH_H + Const.BOOST_SWITCH_H;
        if (v.Ign || v.Prop <= 0 || StopAlt(v, nEng) >= stopH) return;
        v.Ign = true;
        v.NEng = nEng;
        v.IgnBurn = true;
        if (v.BurnLogged) return;
        v.BurnLogged = true;
        sim.LogMsg($"{v.Tag}: посадочная жига — {nEng} двигателя, H={e.H:F0} м", 1);
    }
    private static bool Enough(Vehicle v, in Burn e, int k, double margin)
        => k * e.FOne / v.Mass - e.G >= e.Vv * e.Vv / (2 * e.Dh) * margin;
    private static int EnginesFor(Vehicle v, in Burn e, int nEng, double dt) {
        double aNeed = e.Vv * e.Vv / (2 * e.Dh);
        int need = nEng;
        for (int k = 1; k <= nEng; k++)
            if (k * e.FOne / v.Mass - e.G >= aNeed * 1.35 + 1.5) { need = k; break; }
        if (v.Kind == Kind.Booster)
            return v.NEng == Const.LAND_B_END || need <= Const.LAND_B_END ? Const.LAND_B_END : nEng;
        v.CutT += dt;
        int want = need;
        int two = Math.Min(2, nEng);
        if (e.Tgo <= Const.LAND_CUT2 && Math.Abs(e.Vv) < 25 && want > two
            && Enough(v, e, two, 1.15)) want = two;
        if (e.DhS < 40 && want > 1 && e.FOne * 0.85 / v.Mass >= e.G * 1.15
            && Math.Abs(e.Vv) <= Const.CATCH_VV * 0.7) want = 1;
        want = Math.Max(1, want);
        if (v.NEng > 0 && want != v.NEng) {
            if (want > v.NEng) {
                if (Enough(v, e, v.NEng, 1.0)) return v.NEng;
            }
            else if (v.CutT < Const.LAND_CUT_HOLD) return v.NEng;
            if (want < v.NEng) want = v.NEng - 1;
        }
        if (want != v.NEng) v.CutT = 0;
        return want;
    }
    private static double DescentRate(Vehicle v, in Burn e, double dt, double aMaxN) {
        double dr = e.Dr;
        double aLat = Math.Max(0.5, Math.Min(Const.LAND_ALAT,
            aMaxN * Math.Sin(20 * Const.D2R)));
        if (e.DhS < 40) return Align(v, e, dt, dr);
        return Math.Max(e.VDes, -e.Dh / Math.Max(LatTime(dr, v.VHor, aLat), 3));
    }
    private static double Align(Vehicle v, in Burn e, double dt, double dr) {
        double vSafe = -Math.Max(Const.LAND_VTD, e.DhS * 0.34);
        double vNeed = -e.DhS / Math.Max(LatTime(dr, v.VHor, Const.LAND_ALAT), 1.5);
        if (vNeed <= vSafe) return vSafe;
        v.HoldT += dt;
        return v.HoldT < Const.HOLD_MAX ? vNeed : vSafe;
    }
    private static double ThrottleFor(Vehicle v, in Burn e, double vT, double fnow) {
        double pr = Math.Cos(v.Th);
        if (pr < Const.LAND_PROJ) return Const.LAND_THR_MIN;
        double kv = e.DhS < 60 ? 1.3 : 2.2;
        double aCmd = e.G - e.Drag / v.Mass + Const.Clamp((vT - e.Vv) * kv, -60, 90);
        return Const.Clamp(aCmd * v.Mass / (fnow * pr), Const.LAND_THR_MIN, 1);
    }
    private static void AimBody(SimState sim, Vehicle v, in Burn e) {
        double aMax = e.FNow0 / v.Mass;
        double aNeedV = e.Vv * e.Vv / (2 * Math.Max(e.H - e.HT - 8, 2)) + e.G;
        double tiltMax = Math.Acos(Const.Clamp(aNeedV / Math.Max(aMax, 0.1), 0, 1));
        double tLim = e.H > 3000
            ? 42
            : 7 + (Const.LAND_TILT_NEAR - 7) * Const.Clamp((e.DhS - 18) / 70, 0, 1);
        v.ThCmd = LandAim(sim, v, Math.Min(tiltMax, tLim * Const.D2R), aMax);
        if (e.DhS < Const.LAND_DH_END)
            v.ThCmd = Const.Clamp(v.ThCmd, -Const.LAND_TILT_END * Const.D2R,
                                  Const.LAND_TILT_END * Const.D2R);
    }
    public static void LandingBurn(SimState sim, Vehicle v, double dt, int nEng) {
        var e = new Burn(sim, v, nEng);
        Ignite(sim, v, nEng, e);
        if (!v.Ign) return;
        v.NEng = EnginesFor(v, e, nEng, dt);
        double fnow = v.NEng * e.FOne;
        v.Throttle = ThrottleFor(v, e, DescentRate(v, e, dt, fnow / v.Mass), fnow);
        AimBody(sim, v, e);
    }
    public static double BoosterMiss(SimState sim, Vehicle v, double lift) {
        double x = v.X, y = v.Y, vx = v.Vx, vy = v.Vy, t = 0, m = v.Mass;
        double A = v.A, LD = v.FullLen * v.Dia / A, a0 = Const.BOOST_AOA * Const.D2R;
        void Acc(double px, double py, double pvx, double pvy, out double ax, out double ay) {
            double r = Math.Sqrt(px * px + py * py), h = r - Const.RE;
            double ux = px / r, uy = py / r, ex = -uy, ey = ux;
            double g = Const.MU / (r * r);
            ax = -g * ux; ay = -g * uy;
            double rvx = pvx + Const.W * py, rvy = pvy - Const.W * px;
            double sp = Math.Sqrt(rvx * rvx + rvy * rvy);
            Air at = Atmosphere.At(h, sim.RhoK);
            if (sp < 1 || at.Rho <= 0) return;
            double th = Math.Atan2(-rvx * ex - rvy * ey, -rvx * ux - rvy * uy)
                        + (h > Const.BOOST_STRAIGHT_H ? a0 : 0);
            double c = Math.Cos(th), s = Math.Sin(th);
            double axX = ux * c + ex * s, axY = uy * c + ey * s;
            double sdX = -ux * s + ex * c, sdY = -uy * s + ey * c;
            double va = (rvx * axX + rvy * axY) / sp, vs = (rvx * sdX + rvy * sdY) / sp;
            double al = Math.Atan2(vs, va), sa = Math.Sin(al), ca = Math.Cos(al);
            double q = 0.5 * at.Rho * sp * sp;
            double CA = Atmosphere.CdAxial(sp / at.A, va) * ca * ca + 0.06;
            double CN = 2 * sa * Math.Abs(ca) + 1.15 * LD * sa * Math.Abs(sa);
            double fax = -q * A * CA * Math.Sign(va != 0 ? va : 1), fsd = -q * A * CN;
            double fx = fax * axX + fsd * sdX, fy = fax * axY + fsd * sdY;
            double dx = rvx / sp, dy = rvy / sp, alng = fx * dx + fy * dy;
            fx = alng * dx + (fx - alng * dx) * lift;
            fy = alng * dy + (fy - alng * dy) * lift;
            ax += fx / m; ay += fy / m;
        }
        for (int i = 0; i < 20000; i++) {
            double r = Math.Sqrt(x * x + y * y), h = r - Const.RE;
            if (h <= Const.CATCH_H) break;
            double rvx = vx + Const.W * y, rvy = vy - Const.W * x;
            double sp = Math.Sqrt(rvx * rvx + rvy * rvy);
            double qq = 0.5 * Atmosphere.At(h, sim.RhoK).Rho * sp * sp;
            double dt = qq < 50 ? 2 : (qq < 5e3 ? 0.5 : 0.2);
            Acc(x, y, vx, vy, out double ax1, out double ay1);
            double hd = dt / 2;
            Acc(x + vx * hd, y + vy * hd, vx + ax1 * hd, vy + ay1 * hd, out double ax2, out double ay2);
            x += (vx + ax1 * hd) * dt; y += (vy + ay1 * hd) * dt;
            vx += ax2 * dt; vy += ay2 * dt; t += dt;
            if (h > 200e3 && t > 1200) break;
        }
        return (SimState.PadAngle(sim.T + t) - Math.Atan2(x, y)) * Const.RE;
    }
    public static double BoosterLift(SimState sim, Vehicle v) {
        double lo = -1, hi = 1;
        double mLo = BoosterMiss(sim, v, lo), mHi = BoosterMiss(sim, v, hi);
        if (Math.Sign(mLo) == Math.Sign(mHi)) return Math.Abs(mLo) < Math.Abs(mHi) ? lo : hi;
        for (int i = 0; i < 8; i++) {
            double mid = 0.5 * (lo + hi), mm = BoosterMiss(sim, v, mid);
            if (Math.Sign(mm) == Math.Sign(mLo)) { lo = mid; mLo = mm; } else hi = mid;
        }
        return 0.5 * (lo + hi);
    }
    public static double DeorbitDv(Vehicle v, double rp) {
        double r = v.R, sp = Math.Sqrt(v.Vx * v.Vx + v.Vy * v.Vy);
        double vNew = Math.Sqrt(2 * Const.MU * rp / (r * (r + rp)));
        return Math.Max(0, sp - vNew);
    }
    public static double DeorbitSolve(SimState sim, Vehicle v) {
        double aim = Const.DEO_AIM;
        double lo = DeorbitDv(v, Const.RE + 60e3), hi = DeorbitDv(v, Const.RE + 5e3);
        if (PredictEntry(sim, v, lo, 62, Const.ENTRY_BANK0).Miss < aim) return lo;
        if (PredictEntry(sim, v, hi, 62, Const.ENTRY_BANK0).Miss > aim) return hi;
        for (int i = 0; i < 11; i++) {
            double mid = (lo + hi) / 2;
            if (PredictEntry(sim, v, mid, 62, Const.ENTRY_BANK0).Miss > aim) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }
    public static EntryPred PredictEntry(SimState sim, Vehicle v, double dv, double alphaDeg, double bankDeg)
    {
        double x = v.X, y = v.Y, vx = v.Vx, vy = v.Vy, t = 0, phi = v.Bank;
        double a0 = alphaDeg, bk = bankDeg * Const.D2R;
        double K = v.EntK, m = v.Mass, A = v.A, LD = v.FullLen * v.Dia / A;
        if (dv > 0) { double sp0 = Math.Sqrt(vx * vx + vy * vy), k = (sp0 - dv) / sp0; vx *= k; vy *= k; }
        void Coef(double h, double sp, Air at, out double dOut, out double lOut) {
            double aa = (h > 60e3 ? a0 : (h > Const.GLIDE_H ? 70 : 58)) * Const.D2R;
            double sa = Math.Abs(Math.Sin(aa)), ca = Math.Abs(Math.Cos(aa));
            double CN = 2 * sa * ca + 1.15 * LD * sa * sa;
            double CA = Atmosphere.Cd0(sp / at.A) * ca * ca + 0.06;
            double q = 0.5 * at.Rho * sp * sp;
            dOut = q * A * K * (CN * sa + CA * ca) / m;
            lOut = q * A * K * Math.Max(0, CN * ca - CA * sa) / m;
        }
        void Acc(double px, double py, double pvx, double pvy, double ph, out double ax, out double ay) {
            double r = Math.Sqrt(px * px + py * py), h = r - Const.RE;
            Air at = Atmosphere.At(h, sim.RhoK);
            double g = Const.MU / (r * r);
            ax = -g * px / r; ay = -g * py / r;
            double rvx = pvx + Const.W * py, rvy = pvy - Const.W * px;
            double sp = Math.Sqrt(rvx * rvx + rvy * rvy);
            if (sp > 1 && at.Rho > 0) {
                Coef(h, sp, at, out double cd, out double cl);
                double dx = rvx / sp, dy = rvy / sp;
                ax -= cd * dx; ay -= cd * dy;
                double ux = px / r, uy = py / r, ex = -uy, ey = ux;
                double rx = -dy, ry = dx;
                double sg = (h > Const.GLIDE_H ? (rx * ux + ry * uy) : (rx * ex + ry * ey)) >= 0 ? 1 : -1;
                double lp = cl * Math.Cos(ph) * sg;
                ax += lp * rx; ay += lp * ry;
            }
        }
        double gx = x, gy = y, gt = t;
        for (int i = 0; i < 24000; i++) {
            double r = Math.Sqrt(x * x + y * y), h = r - Const.RE;
            if (!double.IsFinite(r) || h > Const.PRED_H_MAX || t > Const.PRED_T_MAX) break;
            gx = x; gy = y; gt = t;
            if (h <= Const.FLIP_H) break;
            Air at = Atmosphere.At(h, sim.RhoK);
            double rvx = vx + Const.W * y, rvy = vy - Const.W * x;
            double sp = Math.Sqrt(rvx * rvx + rvy * rvy);
            double q = 0.5 * at.Rho * sp * sp;
            double dt = q < 20 ? 4 : (q < 2e3 ? 1.5 : (q < 2e4 ? 0.5 : 0.25));
            double phiCmd = bk;
            if (h <= Const.GLIDE_H && sp > 1 && at.Rho > 0) {
                double ux = x / r, uy = y / r, ex = -uy, ey = ux;
                double dx = rvx / sp, dy = rvy / sp, rx = -dy, ry = dx;
                double dr = (SimState.PadAngle(sim.T + t) - Math.Atan2(x, y)) * Const.RE + FlipDrift(v);
                Coef(h, sp, at, out _, out double cl);
                phiCmd = GlideBank(dr, rvx * ex + rvy * ey, h, rvx * ux + rvy * uy,
                                   cl, Math.Abs(rx * ex + ry * ey));
            }
            phi += Const.Clamp(phiCmd - phi, -Const.BANK_RATE * Const.D2R * dt, Const.BANK_RATE * Const.D2R * dt);
            Acc(x, y, vx, vy, phi, out double ax1, out double ay1);
            double hd = dt / 2;
            Acc(x + vx * hd, y + vy * hd, vx + ax1 * hd, vy + ay1 * hd, phi, out double ax2, out double ay2);
            x += (vx + ax1 * hd) * dt; y += (vy + ay1 * hd) * dt;
            vx += ax2 * dt; vy += ay2 * dt; t += dt;
        }
        return new EntryPred((SimState.PadAngle(sim.T + gt) - Math.Atan2(gx, gy)) * Const.RE + FlipDrift(v), gt);
    }
    public static double BbNeed(SimState sim, Vehicle v) {
        double g = Const.MU / (v.R * v.R), vv = v.VVert, h = v.Alt;
        double tf = (vv + Math.Sqrt(Math.Max(0, vv * vv + 2 * g * h))) / g;
        return -sim.Downrange(v) / Math.Max(tf, 1);
    }
    public static double BoostbackAim(Vehicle v) {
        Vec2 u = v.Up, e = v.East;
        double s = v.MissPred > 0 ? -1 : 1;
        double beta = Const.Clamp(0.12 - (v.VVert - 450) * 0.0004, -0.15, 0.30);
        double cb = Math.Cos(beta), sb = Math.Sin(beta);
        return PitchOf(new Vec2(s * e.X * cb + u.X * sb, s * e.Y * cb + u.Y * sb), v);
    }
}
