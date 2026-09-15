using System;
using System.Collections.Generic;
namespace Starship.Physics;
public sealed class Sat {
    public double X, Y, Vx, Vy, T, Rot, Spin;
}
public sealed class Bay {
    public double Open, Want;
    public int Sats;
    public List<Sat> Out = new();
}
public static class Sim {
    public const double SAT_MASS = 1.5e3;
    public static void BayInit(Vehicle v, double payload) {
        v.BayS = new Bay {
            Sats = v.Kind == Kind.Ship ? (int)Math.Round(payload / SAT_MASS) : 0,
        };
    }
    public static void BayStep(Vehicle v, double dt) {
        Bay b = v.BayS;
        if (b == null) return;
        double rate = dt * 0.14;
        b.Open += Const.Clamp(b.Want - b.Open, -rate, rate);
        b.Open = Const.Clamp(b.Open, 0, 1);
        foreach (Sat s in b.Out) {
            double r = Math.Sqrt(s.X * s.X + s.Y * s.Y), g = Const.MU / (r * r);
            s.Vx -= g * s.X / r * dt; s.Vy -= g * s.Y / r * dt;
            s.X += s.Vx * dt; s.Y += s.Vy * dt; s.T += dt;
            s.Rot += s.Spin * dt;
        }
    }
    public static bool BaySet(SimState sim, Vehicle v, bool want) {
        Bay b = v.BayS;
        if (b == null) return false;
        if (want && v.Q > 60) {
            sim.LogMsg("К: створку нельзя открыть под напором " + (v.Q / 1000).ToString("F1") + " кПа", 2);
            return false;
        }
        b.Want = want ? 1 : 0;
        sim.LogMsg("К: " + (want ? "открытие" : "закрытие") + " створки грузового отсека", 1);
        return true;
    }
    public static int DeploySat(SimState sim, Vehicle v, int n) {
        Bay b = v.BayS;
        if (b == null) return 0;
        if (b.Open < 0.75) { sim.LogMsg("К: сначала откройте створку отсека", 2); return 0; }
        if (b.Sats <= 0) { sim.LogMsg("К: отсек пуст", 2); return 0; }
        int k = 0;
        for (int i = 0; i < n && b.Sats > 0; i++) {
            Vec2 ax = v.Axis, sd = v.Side;
            double kick = 0.55 + 0.3 * (((b.Out.Count * 37) % 7) / 7.0);
            double along = Const.BAY_Z0 + (Const.BAY_Z1 - Const.BAY_Z0) * ((i % 5) + 0.5) / 5.0;
            double outR = 5.4 + 0.25 * (i % 3);
            b.Out.Add(new Sat {
                X = v.X + ax.X * along - sd.X * outR, Y = v.Y + ax.Y * along - sd.Y * outR,
                Vx = v.Vx - sd.X * kick + ax.X * 0.05, Vy = v.Vy - sd.Y * kick + ax.Y * 0.05, T = 0,
                Rot = (b.Out.Count * 0.7) % 6.28, Spin = 0.05 + 0.02 * (i % 3),
            });
            b.Sats--;
            v.Dry = Math.Max(v.Dry - SAT_MASS, Spec.Of(Kind.Ship).Dry);
            k++;
        }
        if (k > 0) sim.LogMsg("К: отделено " + k + " Starlink V3, в отсеке осталось " + b.Sats, 1);
        return k;
    }
    public static void SetThrottle(SimState sim, double x) {
        sim.ManThr = Const.Clamp(x, 0, 1);
    }
    public static void ManualGuide(SimState sim, Vehicle v, double dt) {
        if (v != sim.FocusVeh()) { Guide.Step(sim, v, dt); return; }
        v.Throttle = sim.ManThr;
        if (sim.ManEng.HasValue) v.NEng = sim.ManEng.Value;
        v.Ign = v.Throttle > 0.001 && v.NEng > 0 && v.Prop > 0;
        if (sim.ManPitchAxis != 0) v.ThCmd += 0.6 * dt * sim.ManPitchAxis;
        if (sim.ManThrAxis != 0) SetThrottle(sim, sim.ManThr + 0.5 * dt * sim.ManThrAxis);
        if (sim.ManBankAxis != 0)
            v.BankCmd = Const.Clamp(v.BankCmd + 0.6 * dt * sim.ManBankAxis, 0, 120 * Const.D2R);
    }
    public static void ManualStage(SimState sim) {
        Vehicle b = sim.Veh[0];
        if (b.Mode != "ascent" && b.Mode != "meco") return;
        Vehicle s = b.Mate;
        s.Ign = true; s.NEng = 6; s.Throttle = 1;
        Guide.Separate(sim);
    }
    public static void Thermal(SimState sim, Vehicle v, double dt) {
        const double sb = 5.67e-8;
        double q = v.Heat * 1000 * v.TileK;
        double sa = Math.Abs(Math.Sin(v.Alpha));
        double face = q * Math.Pow(sa, 1.5);
        double shade = q * Const.LEE_SHADE;
        bool tiles = v.Kind == Kind.Ship;
        bool tileToFlow = v.Alpha >= 0;
        double qWind = tileToFlow ? face : shade;
        double qLee = tileToFlow ? shade : face;
        double capWind = tiles ? Const.TILE_CAP : Const.SKIN_CAP;
        double epsWind = tiles ? Const.TILE_EPS : Const.SKIN_EPS;
        double limWind = tiles ? Const.TILE_LIMIT : Const.SKIN_LIMIT;
        double radT = epsWind * sb * Math.Pow(v.TTile, 4);
        double cond = 26 * (v.TTile - v.TSkin);
        v.TTile += (qWind - radT - cond) / capWind * dt;
        v.TSkin += (cond - 3 * (v.TSkin - 250)) / Const.BODY_CAP * dt;
        v.TLee += (qLee - Const.SKIN_EPS * sb * Math.Pow(v.TLee, 4)) / Const.SKIN_CAP * dt;
        v.TTile = Const.Clamp(v.TTile, 90, 2600);
        v.TSkin = Const.Clamp(v.TSkin, 90, 1400);
        v.TLee = Const.Clamp(v.TLee, 90, 2600);
        if (v.TTile > v.MaxTile) v.MaxTile = v.TTile;
        if (v.TLee > v.MaxLee) v.MaxLee = v.TLee;
        if (v.Crashed || !v.Alive) return;
        double overW = Math.Max(0, v.TTile - limWind);
        double overL = Math.Max(0, v.TLee - Const.SKIN_LIMIT);
        double over = Math.Max(overW, overL);
        if (over <= 0) return;
        bool wind = overW >= overL;
        sim.Once("burn" + v.Tag, () => sim.LogMsg(
            $"{v.Tag}: перегрев {(wind ? "наветренного" : "подветренного")} борта — " +
            $"{(wind ? v.TTile : v.TLee):F0} К при пределе {(wind ? limWind : Const.SKIN_LIMIT):F0} К", 2));
        v.Dmg += Const.BURN_RATE * over * over * dt;
        if (v.Dmg < 1) return;
        v.Crashed = true;
        v.Alive = false;
        v.Mode = "crashed";
        sim.LogMsg($"{v.Tag}: ПРОГАР теплозащиты — корпус разрушен на входе", 3);
    }
    public static void MakeVehicles(SimState sim) {
        var b = new Vehicle(Kind.Booster, sim.Payload);
        var s = new Vehicle(Kind.Ship, sim.Payload);
        b.Mate = s; s.Mate = b; b.Stacked = true; s.Attached = true;
        b.SeekPad = true; s.SeekPad = sim.Mission != "trans";
        b.Kp = 0.9; b.Kd = 2.6; s.Kp = 1.4; s.Kd = 2.8;
        b.GimLim = 13; s.GimLim = 15; b.RcsK = 1; s.RcsK = 1;
        foreach (Vehicle v in new[] { b, s }) {
            v.X = 0; v.Y = Const.RE; v.Vx = -Const.W * Const.RE; v.Vy = 0;
            v.Th = 0; v.Om = 0; v.ThCmd = 0;
            BayInit(v, sim.Payload);
        }
        sim.Veh = new List<Vehicle> { b, s };
    }
    public static void Reset(SimState sim, uint seed) {
        sim.Payload = sim.Mission == "high" ? 20e3 : (sim.Mission == "trans" ? 0 : 67e3);
        sim.MecoFill = 0.12;
        sim.MecoV = 1800;
        sim.SecoPeri = sim.Mission == "trans" ? -150e3 : Const.SECO_PERI;
        sim.Seed = seed;
        sim.Rng.Seed(seed);
        sim.Anom = Anomalies.Roll(sim, sim.Rng);
        sim.RhoK = 1;
        var windRng = new Rng();
        windRng.Seed(unchecked(seed * 2654435761u + 1u));
        sim.Wind = Wind.Roll(windRng);
        MakeVehicles(sim);
        sim.Disp = sim.Disperse || sim.AnomOn ? Dispersion.Roll(seed) : null;
        sim.Disp?.Apply(sim);
        sim.T = -10;
        sim.Log.Clear();
        sim.Marks.Clear();
        sim.Events.Clear();
        Anomalies.Apply(sim);
        sim.Focus = "stack";
        sim.Mode = "auto";
        sim.ManThr = 1;
        sim.ManEng = null;
        sim.ManPitchAxis = 0;
        sim.ManThrAxis = 0;
        sim.ManBankAxis = 0;
        sim.ArmGap = Const.ARM_GAP_PARK; sim.ArmDrop = 0; sim.ArmY = Const.ARM_PARK;
        sim.ArmSag = 0; sim.ArmSagV = 0; sim.ArmHeldT = double.NaN;
        sim.LogMsg("Предстартовая подготовка. Баки заправлены, зажигание по нулю.", 1);
    }
    public static void ArmsTick(SimState sim, double dt) {
        Vehicle b = sim.Veh[0], held = null, serve = null;
        foreach (Vehicle v in sim.Veh)
            if (v.Caught && v.SeekPad && !v.Stowed) { held = v; break; }
        if (held == null)
            foreach (Vehicle v in sim.Veh)
                if (v.SeekPad && !v.Landed && !v.Crashed && v.Launched && v.Alt < Const.ARM_APPROACH_H && v.VVert < 0)
                { serve = v; break; }
        Vehicle at = held ?? serve;
        double armWant = at == null && !b.Launched ? Const.ARM_PARK
            : (held != null ? held.CatchH : Const.CATCH_H) + (at ?? b).CatchPinY;
        sim.ArmY += Const.Clamp(armWant - sim.ArmY, -5.0 * dt, 5.0 * dt);
        double gapWant = Const.ARM_GAP_PARK;
        if (held != null) gapWant = 0;
        else if (serve != null) {
            double s = Const.Clamp((serve.Alt - Const.CATCH_H) / serve.CatchPinY, 0, 1);
            gapWant = Const.ARM_GAP_READY * s * s * (3 - 2 * s);
        }
        sim.ArmGap += Const.Clamp(gapWant - sim.ArmGap, -Const.ARM_GAP_RATE * dt, Const.ARM_GAP_RATE * dt);
        if (held != null) {
            if (double.IsNaN(sim.ArmHeldT)) { sim.ArmHeldT = sim.T; sim.ArmSagV = held.CatchVd; }
            double w = 2 * Math.PI / Const.ARM_SAG_PERIOD;
            double acc = -w * w * (sim.ArmSag - Const.ARM_SAG_REST) - 2 * Const.ARM_SAG_ZETA * w * sim.ArmSagV;
            sim.ArmSagV += acc * dt;
            double ds = sim.ArmSagV * dt;
            sim.ArmSag += ds;
            Lower(held, ds);
            if (sim.T - sim.ArmHeldT > Const.ARM_HOLD_T && Math.Abs(sim.ArmSagV) < 0.05) {
                double d = Math.Min(Const.ARM_LOWER * dt, Math.Max(held.Alt, 0));
                if (d > 0) {
                    sim.ArmDrop += d;
                    Lower(held, d);
                }
                if (held.Alt <= 0.005) {
                    held.Stowed = true;
                    held.StowT = sim.T;
                    sim.LogMsg($"{held.Tag}: установлен на стартовый стол", 1);
                }
            }
        }
        else {
            sim.ArmHeldT = double.NaN;
            sim.ArmSagV = 0;
            sim.ArmSag = Math.Max(0, sim.ArmSag - 0.5 * dt);
            if (sim.ArmDrop > 0) sim.ArmDrop = Math.Max(0, sim.ArmDrop - Const.ARM_LOWER * dt);
        }
    }
    private static void Settle(Vehicle v, double dt, ref double ang, double r) {
        ang -= v.HeldVh * dt / r;
        v.HeldVh *= Math.Exp(-dt / Const.ARM_SLIDE_TAU);
        double w = 2 * Math.PI / Const.ARM_TILT_PERIOD, th = Vehicle.AngDiff(v.Th, 0);
        v.HeldOm += (-w * w * th - 2 * Const.ARM_TILT_ZETA * w * v.HeldOm) * dt;
        v.Th += v.HeldOm * dt;
    }
    private static void Lower(Vehicle v, double d) {
        double k = (v.R - d) / v.R;
        v.X *= k; v.Y *= k;
    }
    public static void Tick(SimState sim, double dt) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        double prev = sim.T;
        sim.T += dt;
        Anomalies.Tick(sim);
        ArmsTick(sim, dt);
        if (prev < 0 && sim.T >= 0) {
            b.Mode = "ascent"; b.Ign = true; b.NEng = 33; b.Throttle = 1;
            sim.LogMsg("ЗАЖИГАНИЕ · ПОДЪЁМ. 33 двигателя Raptor", 2);
        }
        foreach (int t in new[] { -8, -5, -3, -2, -1 })
            if (prev < t && sim.T >= t) sim.LogMsg($"Отсчёт: T{t} с", 2);
        if (sim.T < 0) return;
        foreach (Vehicle v in sim.Veh) BayStep(v, dt);
        foreach (Vehicle v in sim.Veh)
            if (v.Stowed && !v.Hauled && sim.T - v.StowT > Const.ARM_HAUL) {
                v.Hauled = true;
                sim.LogMsg($"{v.Tag}: снят со стола и увезён в цех", 1);
            }
        foreach (Vehicle v in sim.Veh) {
            if (v.Landed) {
                double a = Math.Atan2(v.X, v.Y) - Const.W * dt;
                double rr = Math.Sqrt(v.X * v.X + v.Y * v.Y);
                if (v.Caught && !v.Stowed) Settle(v, dt, ref a, rr);
                v.X = rr * Math.Sin(a); v.Y = rr * Math.Cos(a);
                v.Vx = -Const.W * v.Y; v.Vy = Const.W * v.X;
                v.Heat = 0; v.Q = 0; v.Acc = 0;
                continue;
            }
            if (v.Attached) continue;
            if (sim.Mode == "auto") Guide.Step(sim, v, dt);
            else ManualGuide(sim, v, dt);
            Flight.StepVehicle(sim, v, dt);
            Thermal(sim, v, dt);
            if (v.Landed || v.Crashed) sim.Once("over" + v.Tag);
        }
        if (s.Attached) {
            s.X = b.X; s.Y = b.Y; s.Vx = b.Vx; s.Vy = b.Vy; s.Th = b.Th; s.Om = b.Om;
            s.Q = b.Q; s.Mach = b.Mach; s.Acc = b.Acc; s.Alpha = b.Alpha; s.Heat = b.Heat; s.F = 0;
        }
        if (b.Alt > 80) sim.Once("tower", () => sim.LogMsg("Носитель прошёл башню", 1));
        if (b.Alt > 100e3) sim.Once("karman", () => sim.LogMsg("Пересечена линия Кармана — 100 км", 1));
        if (b.Mach > 1 && b.Alt < 40e3)
            sim.Once("mach1", () => sim.LogMsg($"Переход звукового барьера, H={(b.Alt / 1000):F1} км", 1));
    }
}
