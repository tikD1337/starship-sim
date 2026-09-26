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
    public static Orbit Orb(Vehicle v) => Orb(new Vec2(v.X, v.Y), new Vec2(v.Vx, v.Vy));
    public static Orbit Orb(Vec2 p, Vec2 u) {
        double r = p.Len, v2 = u.Dot(u);
        double e0 = v2 / 2 - Const.MU / r, a = -Const.MU / (2 * e0);
        double h = p.X * u.Y - p.Y * u.X;
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
    public static double BellyTilt(Vehicle v, double dr, double vE, double tgo) {
        double lim = Const.BELLY_TILT * Const.D2R;
        double aA = Math.Abs(v.Alpha), sa = Math.Sin(aA), ca = Math.Cos(aA);
        double cn = 2 * sa * Math.Abs(ca) + 1.15 * (v.FullLen * v.Dia / v.A) * sa * sa + Surfaces.FlapCnA(v, aA, v.FlapFwd, v.FlapAft);
        double aN = Math.Max(v.Q * v.A * cn / v.Mass, 0.5 * Const.MU / (v.R * v.R));
        double aLat = Zem0(-dr, vE, Const.BELLY_VF, Math.Max(tgo, Const.BELLY_TMIN));
        return Math.Asin(Const.Clamp(aLat / aN, -Math.Sin(lim), Math.Sin(lim)));
    }
    public static double FlipTgo(Vehicle v, double hStop) {
        double g = Const.MU / (v.R * v.R), a3 = Math.Max(3 * Spec.RaptorSL.Fv / v.Mass - g, 5);
        double vv = Math.Max(-v.VVert, 10);
        return Math.Max(v.Alt - hStop - vv * vv / (2 * a3), 0) / vv;
    }
    public static double GlideAlpha(double lead)
        => Const.GLIDE_KA > 0 ? Const.Clamp(58 + lead / Const.GLIDE_KA, Const.GLIDE_A_LO, Const.GLIDE_A_HI) : 58;
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
    public static double StopAlt(Vehicle v, int nEng, double aCap = double.PositiveInfinity, double cd = 0.12) {
        double h = v.Alt, vv = v.VVert, m = v.Mass, t = 0;
        double tau = v.Ign ? 0 : Math.Max(0.05, v.Eng[0].P.Tau);
        const double dt = 0.4;
        for (int i = 0; i < 800 && vv < 0 && h > 0; i++) {
            Air at = Atmosphere.At(h);
            double spool = tau > 0 ? 1 - tau / dt * (Math.Exp(-t / tau) - Math.Exp(-(t + dt) / tau)) : 1;
            double f = spool * nEng * Math.Max(0, Spec.RaptorSL.Fv - Spec.RaptorSL.Ae * at.P);
            double d = 0.5 * at.Rho * vv * vv * v.A * cd;
            double fUse = Math.Clamp(aCap * m - d, Const.LAND_THR_MIN * f, f);
            double a = (fUse + d) / m - Const.MU / ((Const.RE + h) * (Const.RE + h));
            double share = f > 0 ? fUse / f : 0;
            vv += a * dt; h += vv * dt; m -= share * spool * nEng * Spec.RaptorSL.Mdot * dt; t += dt;
        }
        return h;
    }
    public static double LandAim(SimState sim, Vehicle v, double maxTilt, double aMaxIn) {
        double g = Const.MU / (v.R * v.R), h = v.NAlt, vv = v.NVv;
        double hT = v.Catch ? Const.CATCH_H : SimState.Surface(v.SeekPad ? v.AimDr : sim.Downrange(v));
        double dh = Math.Max(h - hT, 0.5);
        double tgo = Math.Max(2 * dh / Math.Max(-vv, 1), 7);
        double aVert = Math.Max(vv * vv / (2 * dh) + g, 0.5);
        if (!v.SeekPad) {
            double a2 = Const.Clamp(-v.NVh * 0.55, -3, 3);
            return Steer(v, Const.Clamp(Math.Atan2(a2, aVert), -maxTilt, maxTilt));
        }
        double dr = sim.NavDr(v), vh = v.NVh;
        double aMax = aMaxIn > 0 ? aMaxIn : aVert * 1.6;
        double lat = Math.Sqrt(Math.Max(aMax * aMax - aVert * aVert, 0.04));
        if (dh < 500) lat = Math.Min(lat, Const.LAND_ALAT);
        else if (dh < 3000) lat = Math.Min(lat, 6.5);
        double aLat;
        if (dh < Const.LAND_DHPD && v.Kind == Kind.Booster) {
            double p = Const.LAND_POLE;
            aLat = -p * p / 6 * dr - 2 * p / 3 * vh;
        }
        else if (dh < Const.LAND_DHPD) {
            double aL = Math.Min(lat, Const.LAND_ALAT);
            double tau = v.Kd / Math.Max(v.Kp, 0.1);
            double k1 = Math.Min(aL / 35, Const.LAND_KLAT / (tau * tau));
            double k2 = Const.LAND_KDAMP * Math.Sqrt(Math.Max(k1, 1e-4));
            aLat = -k1 * dr - k2 * vh;
        }
        else aLat = -6 * (dr + vh * tgo) / (tgo * tgo) + 2 * vh / tgo;
        if (v.WindEst != 0 && dh < Const.LAND_WIND_H) {
            double rho = Atmosphere.At(h, v.RhoEst).Rho, u = v.WindEst - vh;
            aLat -= Const.LAND_WIND_K * 0.5 * rho * u * Math.Abs(u) * v.Dia * v.FullLen / v.Mass;
        }
        aLat = Const.Clamp(aLat, -lat, lat);
        return Steer(v, Const.Clamp(Math.Atan2(aLat, aVert), -maxTilt, maxTilt));
    }
    private static double Steer(Vehicle v, double want) {
        if (v.Kind != Kind.Booster) return want;
        return SteerP(v, want, Const.LAND_POLE);
    }
    private static double SteerP(Vehicle v, double want, double p) {
        double kp = Math.Max(v.Kp, 0.1), th = Vehicle.AngDiff(v.Th, 0);
        return th + 6 * p * p / kp * (want - th) - (4 * p - v.Kd) / kp * v.Om;
    }
    private static double LatTime(double dr, double vh, double aLat)
        => Const.LAND_TLAG + Math.Sqrt(4 * Math.Abs(dr) / aLat) + Math.Abs(vh) / aLat;
    private readonly struct Burn {
        public readonly double H, HT, DhS, Dh, G, FOne, Drag, Vv, VDes, FNow0, Dr, Tgo;
        public Burn(SimState sim, Vehicle v, int nEng) {
            H = v.NAlt;
            Air at = Atmosphere.At(H, v.RhoEst);
            G = Const.MU / (v.R * v.R);
            FOne = Math.Max(1, Spec.RaptorSL.Fv - Spec.RaptorSL.Ae * at.P);
            Drag = 0.5 * at.Rho * v.NVv * v.NVv * v.A * 0.75;
            FNow0 = (v.Ign ? v.NEng : nEng) * FOne;
            HT = v.Catch ? CatchHT(v) : SimState.Surface(v.SeekPad ? v.AimDr : sim.Downrange(v));
            double f = (v.Kind == Kind.Booster ? Const.LAND_B_END : nEng)
                       * Math.Max(0, Spec.RaptorSL.Fv - Spec.RaptorSL.Ae * at.P);
            double anet = Math.Max(1, f / v.Mass - G);
            VDes = -(2.5 + Math.Sqrt(2 * anet * 0.72 * Math.Max(H - HT - 8, 0)));
            Vv = v.NVv;
            DhS = H - HT;
            Dh = Math.Max(DhS, 0.5);
            Dr = v.SeekPad ? sim.NavDr(v) : 0;
            double aLat = Math.Max(0.5, Math.Min(Const.LAND_ALAT,
                nEng * FOne / v.Mass * Math.Sin(20 * Const.D2R)));
            Tgo = Math.Max(Dh / (0.5 * (Math.Max(-Vv, 0) + Const.LAND_VTD)),
                           LatTime(Dr, v.NVh, aLat));
        }
    }
    public static double CatchHT(Vehicle v) => v.Kind == Kind.Booster ? Const.CATCH_H - Const.CATCH_WIN / 2 : Const.CATCH_H;
    private static void Ignite(SimState sim, Vehicle v, int nEng, in Burn e) {
        double stopH = (v.Kind == Kind.Ship ? Const.LAND_STOP_S : Const.CATCH_H + Const.BOOST_SWITCH_H)
                       + (v.Catch ? 0 : e.HT - CatchHT(v));
        if (v.Ign || v.Prop <= 0) return;
        double stop = v.Kind == Kind.Ship
            ? StopAlt(v, nEng)
            : StopAlt(v, nEng, Const.LAND_B_GMAX * Const.G0,
                      Const.Clamp(v.Drag / Math.Max(v.Q * v.A, 1), 0.12, 2.5));
        if (stop >= stopH) return;
        v.Ign = true;
        v.NEng = nEng;
        v.IgnBurn = true;
        if (v.BurnLogged) return;
        v.BurnLogged = true;
        sim.LogMsg($"{v.Tag}: посадочная жига — {nEng} {Engines(nEng)}, H={e.H:F0} м", 1);
    }
    private static string Engines(int n)
        => n % 10 == 1 && n % 100 != 11 ? "двигатель"
         : n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 12 || n % 100 > 14) ? "двигателя" : "двигателей";
    private static bool Enough(Vehicle v, in Burn e, int k, double margin)
        => k * e.FOne / v.Mass - e.G >= e.Vv * e.Vv / (2 * e.Dh) * margin;
    private static int EnginesFor(Vehicle v, in Burn e, int nEng, double dt) {
        double aNeed = e.Vv * e.Vv / (2 * e.Dh);
        int need = nEng;
        for (int k = 1; k <= nEng; k++)
            if (k * e.FOne / v.Mass - e.G >= aNeed * 1.35 + 1.5) { need = k; break; }
        if (v.Kind == Kind.Booster) {
            int nb = v.NEng > 0 && v.NEng <= Const.LAND_B_END || need <= Const.LAND_B_END ? Math.Min(Const.LAND_B_END, nEng) : nEng;
            if (!v.Catch && nb <= Const.LAND_B_END)
                while (nb > 1 && nb * e.FOne * Const.LAND_THR_MIN > v.Mass * e.G * 0.95) nb--;
            if (v.Catch && nb <= Const.LAND_B_END && nb > 1 && v.NRun == nb && e.DhS > 0 && e.Vv > -0.5
                && v.Throttle <= Const.LAND_THR_MIN + 1e-3 && v.F > v.Mass * e.G) v.LandCut = true;
            if (v.LandCut) nb = Math.Min(nb, Const.LAND_B_END - 1);
            return nb;
        }
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
        return Math.Max(e.VDes, -e.Dh / Math.Max(LatTime(dr, v.NVh, aLat), 3));
    }
    private static double Align(Vehicle v, in Burn e, double dt, double dr) {
        if (v.Kind == Kind.Ship && v.Catch && e.DhS < 3 && e.DhS > -Const.CATCH_WIN + 3
            && (Math.Abs(dr) > 0.75 * Const.CATCH_DR || Math.Abs(v.NVh) > 0.7 * Const.CATCH_VH))
            return 0;
        double vSafe = -Math.Max(Const.LAND_VTD, e.DhS * 0.34);
        double vNeed = -e.DhS / Math.Max(LatTime(dr, v.NVh, Const.LAND_ALAT), 1.5);
        if (vNeed <= vSafe) return vSafe;
        v.HoldT += dt;
        return v.HoldT < Const.HOLD_MAX ? vNeed : vSafe;
    }
    private static double ThrottleFor(Vehicle v, in Burn e, double vT, double fnow) {
        double pr = Math.Cos(v.Th);
        if (pr < Const.LAND_PROJ) return v.Mode == "flipS" ? FlipThrottle(v, e, fnow, Const.FLIP_TW) : Const.LAND_THR_MIN;
        double kv = e.DhS < 60 ? 1.3 : 2.2;
        double aCmd = e.G - e.Drag / v.Mass + Const.Clamp((vT - e.Vv) * kv, -60, 90);
        if (v.Kind == Kind.Booster) aCmd = Math.Min(aCmd, Const.LAND_B_GMAX * Const.G0 - v.Drag / v.Mass);
        return Const.Clamp(aCmd * v.Mass / (fnow * pr), Const.LAND_THR_MIN, 1);
    }
    private static void AimBody(SimState sim, Vehicle v, in Burn e) {
        double aMax = e.FNow0 / v.Mass;
        if (v.Kind == Kind.Booster) aMax = Math.Min(aMax, Const.LAND_B_GMAX * Const.G0 - v.Drag / v.Mass);
        double aNeedV = e.Vv * e.Vv / (2 * Math.Max(e.H - e.HT - 8, 2)) + e.G;
        double tiltMax = Math.Acos(Const.Clamp(aNeedV / Math.Max(aMax, 0.1), 0, 1));
        double tLim = e.H > 3000
            ? 42
            : 7 + (Const.LAND_TILT_NEAR - 7) * Const.Clamp((e.DhS - 18) / 70, 0, 1);
        v.ThCmd = LandAim(sim, v, Math.Min(tiltMax, tLim * Const.D2R), aMax);
        if (e.DhS < Const.LAND_DH_END) {
            double end = EndTilt(v) * Const.D2R;
            v.ThCmd = Const.Clamp(v.ThCmd, -end, end);
        }
    }
    private static double EndTilt(Vehicle v)
        => v.Catch && v.Alt < Const.CATCH_H + 3 ? Const.CATCH_TILT_CMD : Const.LAND_TILT_END;
    private static double Zem0(double dz, double vel, double vT, double T) => 6 * dz / (T * T) - 2 * (2 * vel + vT) / T;
    private static double ZemT(double dz, double vel, double vT, double T) => -6 * dz / (T * T) + 2 * (vel + 2 * vT) / T;
    private static double FallTime(double h, double v0, double vG, double aD, double aB) {
        double v1 = Math.Sqrt(Math.Max((h + v0 * v0 / (2 * aD) + vG * vG / (2 * aB)) / (1 / (2 * aD) + 1 / (2 * aB)), 0));
        if (v1 <= v0) return 2 * h / Math.Max(v0 + vG, 1);
        return (v1 - v0) / aD + (v1 - vG) / aB;
    }
    private static double FlipThrottle(Vehicle v, in Burn e, double fnow, double tw)
        => Const.Clamp(tw * v.Mass * e.G / fnow, Const.LAND_THR_MIN, 1);
    private static int ShipEngines(Vehicle v, in Burn e, double need, int nEng, double dt) {
        v.CutT += dt;
        double one = e.FOne / v.Mass;
        int want = nEng;
        for (int k = 1; k <= nEng; k++)
            if (k * one >= need * Const.SHIP_ENG_K) { want = k; break; }
        while (want > 1 && want * one * Const.LAND_THR_MIN > need) want--;
        if (v.NEng > 0 && want != v.NEng && v.CutT < Const.LAND_CUT_HOLD) return v.NEng;
        if (want < v.NEng) want = v.NEng - 1;
        if (want != v.NEng) v.CutT = 0;
        return want;
    }
    private static double FinalBrake(Vehicle v, in Burn e)
        => Math.Max(Const.SHIP_FINAL_A, 1.3 * (Const.LAND_THR_MIN * e.FOne / v.Mass - e.G) + 0.3);
    private static void ShipApproach(SimState sim, Vehicle v, in Burn e, int nEng, double dt) {
        double aB = FinalBrake(v, e);
        double vGate = Math.Sqrt(Const.SHIP_CATCH_V * Const.SHIP_CATCH_V + 2 * aB * Const.SHIP_GATE);
        double dz = e.HT + Const.SHIP_GATE - e.H, vx = v.NVh;
        double aDown = Math.Max(Math.Min(Const.SHIP_AD, e.G - Const.LAND_THR_MIN * e.FOne / v.Mass), 0.3);
        double hG = Math.Max(-dz, 0), v0 = Math.Max(-e.Vv, 0);
        double vG = Math.Min(vGate, Math.Sqrt(v0 * v0 + 2 * aDown * hG));
        double tV = FallTime(hG, v0, vG, aDown, Const.SHIP_AB);
        double tL = 1, dOut = Math.Sign(e.Dr) * Math.Max(Math.Abs(e.Dr) - Const.SHIP_HOLD_DR, 0);
        for (int i = 0; i < 120 && Math.Max(Math.Abs(Zem0(-dOut, vx, 0, tL)), Math.Abs(ZemT(-dOut, vx, 0, tL))) > Const.SHIP_ALAT; i++)
            tL *= 1.04;
        double T = Math.Max(Math.Max(tV, tL), 1.5);
        double aX = !v.Catch && e.DhS < Const.LAND_DH_END ? -Const.SHIP_KV * vx : Zem0(-e.Dr, vx, 0, Math.Max(T, 6 / Const.SHIP_POLE));
        if (v.WindEst != 0 && e.H < Const.LAND_WIND_H) {
            double rho = Atmosphere.At(e.H, v.RhoEst).Rho;
            aX -= Const.LAND_WIND_K * 0.5 * rho * v.WindEst * Math.Abs(v.WindEst) * v.Dia * v.FullLen / v.Mass;
        }
        double aUp = T > tV + Const.SHIP_WAIT_T && dz < 0
            ? Const.Clamp((dz / T - e.Vv) * Const.SHIP_KV, -Const.SHIP_AD, Const.SHIP_AB)
            : Zem0(dz, e.Vv, -vG, T);
        if (dz < 0 && e.Vv > -0.5) aUp = Math.Min(aUp, -Const.SHIP_KV * (e.Vv + 0.5));
        double aZ = Math.Max(aUp + e.G - e.Drag / v.Mass, 0.5);
        if (v.Mode == "flipS") {
            v.NEng = Math.Clamp((int)Math.Floor(Const.SHIP_FLIP_TW * v.Mass * e.G / (Const.LAND_THR_MIN * e.FOne)), 1, nEng);
            double fl = v.NEng * e.FOne, pr = Math.Cos(v.Th);
            v.Throttle = pr < Const.LAND_PROJ ? FlipThrottle(v, e, fl, Const.SHIP_FLIP_TW) : Const.Clamp(aZ * v.Mass / (fl * pr), Const.LAND_THR_MIN, 1);
            return;
        }
        double want;
        if (v.ShipHold || e.DhS <= Const.SHIP_GATE || tV <= 2) {
            v.ShipHold = true;
            v.NEng = ShipEngines(v, e, e.G + aB, nEng, dt);
            double fn = v.NEng * e.FOne;
            double vT = -Math.Sqrt(Const.SHIP_CATCH_V * Const.SHIP_CATCH_V + 2 * aB * Math.Max(e.DhS, 0));
            double ff = aB * Const.Clamp(e.DhS / 2, 0, 1);
            bool canHover = Const.LAND_THR_MIN * fn / v.Mass < e.G - 0.3;
            if (canHover && e.DhS > 3 && e.DhS < Const.SHIP_HOLD_H && (Math.Abs(e.Dr) > Const.SHIP_HOLD_DR || Math.Abs(vx) > Const.SHIP_HOLD_VH)
                && vT < -Const.LAND_VTD) { vT = -Const.LAND_VTD; ff = 0; }
            double aCmd = e.G - e.Drag / v.Mass + ff + Const.Clamp((vT - e.Vv) * Const.SHIP_KV, -20, 20);
            v.Throttle = Const.Clamp(aCmd * v.Mass / (fn * Math.Max(Math.Cos(v.Th), 0.3)), Const.LAND_THR_MIN, 1);
            want = Math.Atan2(aX, e.G);
        }
        else {
            v.NEng = ShipEngines(v, e, Math.Sqrt(aX * aX + aZ * aZ), nEng, dt);
            v.Throttle = Const.Clamp(aZ * v.Mass / (v.NEng * e.FOne * Math.Max(Math.Cos(v.Th), 0.3)), Const.LAND_THR_MIN, 1);
            want = Math.Atan2(aX, aZ);
        }
        double lim = (e.DhS < Const.LAND_DH_END ? EndTilt(v) : Const.SHIP_TILT) * Const.D2R;
        v.ThCmd = Const.Clamp(SteerP(v, Const.Clamp(want, -lim, lim), Const.SHIP_POLE), -2 * lim, 2 * lim);
    }
    private static int Avail(Vehicle v) {
        int n = 0;
        foreach (Engine en in v.Eng) if (!en.Failed && EngineSet.Relights(v, en)) n++;
        return n;
    }
    public static void LandingBurn(SimState sim, Vehicle v, double dt, int nEng) {
        nEng = Math.Max(1, Math.Min(nEng, Avail(v)));
        var e = new Burn(sim, v, nEng);
        Ignite(sim, v, nEng, e);
        if (!v.Ign) return;
        if (v.Kind == Kind.Ship && v.SeekPad && (v.Mode == "landS" || v.Mode == "flipS")) {
            ShipApproach(sim, v, e, nEng, dt);
            return;
        }
        v.NEng = EnginesFor(v, e, nEng, dt);
        double fnow = v.NEng * e.FOne;
        v.Throttle = ThrottleFor(v, e, DescentRate(v, e, dt, fnow / v.Mass), fnow);
        AimBody(sim, v, e);
    }
    public static double BoosterMiss(SimState sim, Vehicle v, double lift) {
        double x = v.X, y = v.Y, vx = v.Vx, vy = v.Vy, t = 0, m = v.Mass;
        double A = v.A, LD = v.FullLen * v.Dia / A, a0 = Const.BOOST_AOA * Const.D2R;
        void Acc(double px, double py, double pvx, double pvy, Air at, out double ax, out double ay) {
            double r = Math.Sqrt(px * px + py * py), h = r - Const.RE;
            double ux = px / r, uy = py / r, ex = -uy, ey = ux;
            double g = Const.MU / (r * r);
            ax = -g * ux; ay = -g * uy;
            double rvx = pvx + Const.W * py, rvy = pvy - Const.W * px;
            double sp = Math.Sqrt(rvx * rvx + rvy * rvy);
            if (sp < 1 || at.Rho <= 0) return;
            double th = Math.Atan2(-rvx * ex - rvy * ey, -rvx * ux - rvy * uy)
                        + (h > Const.BOOST_STRAIGHT_H ? a0 : 0);
            double c = Math.Cos(th), s = Math.Sin(th);
            double axX = ux * c + ex * s, axY = uy * c + ey * s;
            double sdX = -ux * s + ex * c, sdY = -uy * s + ey * c;
            double va = (rvx * axX + rvy * axY) / sp, vs = (rvx * sdX + rvy * sdY) / sp;
            double al = Math.Atan2(vs, va), sa = Math.Sin(al), ca = Math.Cos(al);
            double q = 0.5 * at.Rho * sp * sp;
            double CA = Atmosphere.CdAxial(sp / at.A, va) * ca * ca + 0.06 + 3 * Const.FIN_S * Const.FIN_CD / A;
            double CN = 2 * sa * Math.Abs(ca) + 1.15 * LD * sa * Math.Abs(sa);
            double cp = v.FullLen * (0.5 + 0.16 * ca * Math.Abs(ca)), cmv = v.Cm;
            double fax = -q * A * CA * Math.Sign(va != 0 ? va : 1), fsd = -q * A * CN * Surfaces.FinTrim(cp, cmv);
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
            Air at = Atmosphere.At(h, v.RhoEst);
            double qq = 0.5 * at.Rho * sp * sp;
            double dt = qq < 50 ? 2 : (qq < 5e3 ? 0.5 : 0.2);
            Acc(x, y, vx, vy, at, out double ax1, out double ay1);
            double hd = dt / 2;
            double xm = x + vx * hd, ym = y + vy * hd;
            Acc(xm, ym, vx + ax1 * hd, vy + ay1 * hd, Atmosphere.At(Math.Sqrt(xm * xm + ym * ym) - Const.RE, v.RhoEst), out double ax2, out double ay2);
            x += (vx + ax1 * hd) * dt; y += (vy + ay1 * hd) * dt;
            vx += ax2 * dt; vy += ay2 * dt; t += dt;
            if (h > 200e3 && t > 1200) break;
        }
        return (SimState.PadAngle(sim.T + t) - Math.Atan2(x, y)) * Const.RE - v.AimDr;
    }
    public static double BoosterLift(SimState sim, Vehicle v, double guess) {
        double x = Secant(l => BoosterMiss(sim, v, l), Const.Clamp(guess, -1, 1), 0.05, -1, 1, 0.002);
        return double.IsNaN(x) ? BoosterLift(sim, v) : x;
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
    public static double DeorbitDv(Vehicle v, double rp) => DeorbitDv(new Vec2(v.X, v.Y), new Vec2(v.Vx, v.Vy), rp);
    public static double DeorbitDv(Vec2 pos, Vec2 vel, double rp) {
        double sp = vel.Len, lo = 0, hi = sp;
        if (Orb(pos, vel).Peri + Const.RE <= rp) return 0;
        for (int i = 0; i < 40; i++) {
            double mid = (lo + hi) / 2;
            if (Orb(pos, vel * (1 - mid / sp)).Peri + Const.RE > rp) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }
    public static (Vec2 Pos, Vec2 Vel) Coast(Vehicle v, double t) {
        Vec2 p = new(v.X, v.Y), u = new(v.Vx, v.Vy);
        static Vec2 G(Vec2 q) { double r = q.Len; return q * (-Const.MU / (r * r * r)); }
        for (double c = 0; c < t; ) {
            double h = Math.Min(1, t - c);
            Vec2 a1 = G(p), a2 = G(p + u * (h / 2));
            p += (u + a1 * (h / 2)) * h;
            u += a2 * h;
            c += h;
        }
        return (p, u);
    }
    public static double DeorbitSolve(SimState sim, Vehicle v) {
        double aim = Const.DEO_AIM;
        double wait = Propellant.SettleLeft(v);
        (Vec2 bp, Vec2 bv) = Coast(v, wait);
        double lo = DeorbitDv(bp, bv, Const.RE + 60e3), hi = DeorbitDv(bp, bv, Const.RE + 5e3);
        if (PredictEntry(sim, v, lo, 62, Const.ENTRY_BANK0, wait).Miss < aim) return lo;
        if (PredictEntry(sim, v, hi, 62, Const.ENTRY_BANK0, wait).Miss > aim) return hi;
        for (int i = 0; i < 11; i++) {
            double mid = (lo + hi) / 2;
            if (PredictEntry(sim, v, mid, 62, Const.ENTRY_BANK0, wait).Miss > aim) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }
    public static double DeorbitSolve(SimState sim, Vehicle v, double guess) {
        double wait = Propellant.SettleLeft(v);
        (Vec2 bp, Vec2 bv) = Coast(v, wait);
        double lo = DeorbitDv(bp, bv, Const.RE + 60e3), hi = DeorbitDv(bp, bv, Const.RE + 5e3);
        double F(double dv) => PredictEntry(sim, v, dv, 62, Const.ENTRY_BANK0, wait).Miss - Const.DEO_AIM;
        double x = Secant(F, guess, 0.2, lo, hi, 0.005);
        return double.IsNaN(x) ? DeorbitSolve(sim, v) : x;
    }
    private static double Secant(Func<double, double> f, double x0, double step, double lo, double hi, double tol) {
        if (x0 < lo || x0 > hi) return double.NaN;
        double f0 = f(x0), x1 = x0 + (x0 + step <= hi ? step : -step), f1 = f(x1);
        for (int i = 0; i < 6; i++) {
            if (f1 == f0) return double.NaN;
            double x2 = x1 - f1 * (x1 - x0) / (f1 - f0);
            if (!double.IsFinite(x2) || x2 < lo || x2 > hi) return double.NaN;
            x0 = x1; f0 = f1; x1 = x2;
            if (Math.Abs(x1 - x0) < tol) return x1;
            f1 = f(x1);
        }
        return double.NaN;
    }
    public static EntryPred PredictEntry(SimState sim, Vehicle v, double dv, double alphaDeg, double bankDeg, double coast = 0)
    {
        (Vec2 cp, Vec2 cv) = Coast(v, coast);
        double x = cp.X, y = cp.Y, vx = cv.X, vy = cv.Y, t = coast, phi = v.Bank;
        double a0 = alphaDeg, bk = bankDeg * Const.D2R;
        double K = v.EntK, m = v.Mass, A = v.A, LD = v.FullLen * v.Dia / A;
        double span = Surfaces.FlapSpan(40 * Const.D2R, 55 * Const.D2R);
        void Coef(double h, double sp, Air at, out double dOut, out double lOut) {
            double aa = (h > 60e3 ? a0 : (h > Const.GLIDE_H ? 70 : 58)) * Const.D2R;
            double sa = Math.Abs(Math.Sin(aa)), ca = Math.Abs(Math.Cos(aa));
            double CN = 2 * sa * ca + 1.15 * LD * sa * sa + Surfaces.FlapCn(Math.Abs(aa)) * span / A;
            double CA = Atmosphere.Cd0(sp / at.A) * ca * ca + 0.06;
            double q = 0.5 * at.Rho * sp * sp;
            dOut = q * A * K * (CN * sa + CA * ca) / m;
            lOut = q * A * K * Math.Max(0, CN * ca - CA * sa) / m;
        }
        Air AirAt(double px, double py) => Atmosphere.At(Math.Sqrt(px * px + py * py) - Const.RE, v.RhoEst);
        void Acc(double px, double py, double pvx, double pvy, double ph, Air at, out double ax, out double ay) {
            double r = Math.Sqrt(px * px + py * py), h = r - Const.RE;
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
        if (dv > 0) { double sp0 = Math.Sqrt(vx * vx + vy * vy), k = (sp0 - dv) / sp0; vx *= k; vy *= k; }
        double gx = x, gy = y, gt = t;
        for (int i = 0; i < 24000; i++) {
            double r = Math.Sqrt(x * x + y * y), h = r - Const.RE;
            if (!double.IsFinite(r) || h > Const.PRED_H_MAX || t > Const.PRED_T_MAX) break;
            gx = x; gy = y; gt = t;
            if (h <= Const.FLIP_H) break;
            Air at = Atmosphere.At(h, v.RhoEst);
            double rvx = vx + Const.W * y, rvy = vy - Const.W * x;
            double sp = Math.Sqrt(rvx * rvx + rvy * rvy);
            double q = 0.5 * at.Rho * sp * sp;
            double dt = q < 20 ? 4 : (q < 2e3 ? 1.5 : (q < 2e4 ? 0.5 : 0.25));
            double phiCmd = bk;
            if (h <= Const.GLIDE_H && sp > 1 && at.Rho > 0) {
                double ux = x / r, uy = y / r, ex = -uy, ey = ux;
                double dx = rvx / sp, dy = rvy / sp, rx = -dy, ry = dx;
                double dr = (SimState.PadAngle(sim.T + t) - Math.Atan2(x, y)) * Const.RE + Const.FLIP_D - v.AimDr;
                Coef(h, sp, at, out _, out double cl);
                phiCmd = GlideBank(dr, rvx * ex + rvy * ey, h, rvx * ux + rvy * uy,
                                   cl, Math.Abs(rx * ex + ry * ey));
            }
            phi += Const.Clamp(phiCmd - phi, -Const.BANK_RATE * Const.D2R * dt, Const.BANK_RATE * Const.D2R * dt);
            Acc(x, y, vx, vy, phi, at, out double ax1, out double ay1);
            double hd = dt / 2;
            double xm = x + vx * hd, ym = y + vy * hd;
            Acc(xm, ym, vx + ax1 * hd, vy + ay1 * hd, phi, AirAt(xm, ym), out double ax2, out double ay2);
            x += (vx + ax1 * hd) * dt; y += (vy + ay1 * hd) * dt;
            vx += ax2 * dt; vy += ay2 * dt; t += dt;
        }
        return new EntryPred((SimState.PadAngle(sim.T + gt) - Math.Atan2(gx, gy)) * Const.RE + Const.FLIP_D - v.AimDr, gt);
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
