using System;
namespace Starship.Physics;
public static class Flight {
    public static double FlapCn(double alpha) {
        double sa = Math.Sin(alpha);
        return 2 * sa * sa * Math.Sin(Const.FLAP_DEF);
    }
    public static double RcsAuth(Vehicle v) => v.Rcs ? (v.Kind == Kind.Booster ? 2.4e6 : 1.1e6) * v.RcsK : 0;
    public static void Quench(Vehicle v) {
        foreach (Engine e in v.Eng) { e.On = false; e.Spool = 0; e.F = 0; e.Md = 0; e.Pc = 0; e.Thr = 0; }
        v.Mdot = 0;
        v.NRun = 0;
    }
    public static void SpoolAttached(Vehicle v, double dt) {
        v.EngAcc += dt;
        if (v.EngAcc >= 0.02) {
            Pressurant.Step(v, v.EngAcc);
            EngineSet.Update(v, v.EngAcc, v.Mate != null ? v.Mate.Pa : 0);
            v.EngAcc = 0;
        }
        EngStats st = EngineSet.Stats(v);
        v.F = st.F; v.Mdot = st.Md; v.NRun = st.Run;
        v.Prop = Math.Max(0, v.Prop - st.Md * dt);
    }
    public static void StepVehicle(SimState sim, Vehicle v, double dt) {
        if (!v.Alive || v.Landed || v.Attached) return;
        double h = v.Alt;
        Air at = Atmosphere.At(h, sim.RhoK);
        Vec2 up = v.Up, ax = v.Axis, sd = v.Side;
        v.WindE = sim.Wind.At(h, sim.T);
        Vec2 vr = v.VAir;
        double sp = vr.Len;
        double M = sp / at.A, q = 0.5 * at.Rho * sp * sp;
        v.Q = q; v.Mach = M; v.Rho = at.Rho; v.Pa = at.P;
        v.Heat = Aero.Heat(at.Rho, sp);
        if (v.Heat > v.MaxHeat) v.MaxHeat = v.Heat;
        if (q > v.MaxQ) v.MaxQ = q;
        if (v.Alt > v.MaxAlt) v.MaxAlt = v.Alt;
        {
            double d = v.BankCmd - v.Bank, lim = Const.BANK_RATE * Const.D2R * dt;
            v.Bank += Const.Clamp(d, -lim, lim);
        }
        if (v.Prop <= 0) { v.Ign = false; v.NEng = 0; }
        Propellant.Sequence(v);
        if (v.Ullage && !v.UllLog) sim.LogMsg($"{v.Tag}: осадка топлива — ДМТ толкают вперёд перед запуском", 1);
        v.UllLog = v.Ullage;
        v.EngAcc += dt;
        if (v.EngAcc >= 0.02) {
            Pressurant.Step(v, v.EngAcc);
            EngineSet.Update(v, v.EngAcc, at.P);
            v.EngAcc = 0;
        }
        EngStats st = EngineSet.Stats(v);
        v.F = st.F; v.Mdot = st.Md; v.NRun = st.Run;
        v.HdrLeft = Math.Min(v.HdrLeft, v.Prop);
        if (v.OnHeader) v.HdrLeft = Math.Max(0, v.HdrLeft - st.Md * dt);
        v.Prop = Math.Max(0, v.Prop - st.Md * dt);
        double m = v.Mass;
        double L = v.FullLen, A = v.A;
        AeroForce af = Aero.Compute(v, q, M);
        v.Alpha = af.Alpha;
        double Fax = af.Fax, Fsd = af.Fsd;
        v.Drag = af.Drag;
        double I = v.Inertia, cm = v.Cm;
        if (v.Kind == Kind.Booster) {
            double want = (v.Mode == "idle" || v.Mode == "ascent" || v.Mode == "meco") ? 0 : 1;
            v.FinDep = Const.Clamp(v.FinDep + Const.Clamp(want - v.FinDep, -0.3 * dt, 0.3 * dt), 0, 1);
        }
        double flapAuth;
        if (v.Kind == Kind.Ship) {
            double dCn = FlapCn(v.Alpha);
            double swept = 2 * Const.FLAP_S_FWD * Math.Abs(Const.FLAP_Y_FWD - cm)
                         + 2 * Const.FLAP_S_AFT * Math.Abs(Const.FLAP_Y_AFT - cm);
            flapAuth = q * swept * dCn * v.CtrlK;
        }
        else {
            double dCn = Const.FIN_CN * Math.Sin(Const.FLAP_DEF);
            flapAuth = q * 3 * Const.FIN_S * Math.Abs(Const.FIN_Y - cm) * dCn * v.FinDep * v.CtrlK;
        }
        double rcsAuth = RcsAuth(v);
        double gimLim = double.IsNaN(v.GimLim) ? v.Spec.Gimbal : v.GimLim * Const.D2R;
        double gimAuth = v.F > 1e3 ? v.F * Math.Sin(gimLim) * cm : 0;
        double tqEng = 0;
        foreach (Engine e in v.Eng) tqEng -= e.F * e.Arm;
        double err = Vehicle.AngDiff(v.ThCmd, v.Th);
        double aAvail = Math.Max(0.015,
            (gimAuth + flapAuth + rcsAuth - Math.Abs(Fsd * (v.Cp - cm)) + Math.Sign(err) * tqEng) / I);
        double aLim = Math.Min(v.Mode == "flipS" ? Const.OM_ACC_FLIP : Const.OM_ACC_MAX, aAvail);
        double lag = aLim * gimLim / (Const.GIM_RATE * Const.D2R);
        double wCap = Math.Sqrt(lag * lag + 2 * Const.OM_BRAKE * aLim * Math.Abs(err)) - lag;
        double aDes = v.Mode == "flipS"
            ? Const.Clamp((Math.Sign(err) * Math.Sqrt(2 * Const.FLIP_BRAKE * aLim * Math.Abs(err)) - v.Om) * Const.FLIP_KW,
                          -aLim, aLim)
            : Const.Clamp(v.Kd * (Const.Clamp(v.Kp / Math.Max(v.Kd, 0.1) * err, -wCap, wCap) - v.Om), -aLim, aLim);
        double need = aDes * I;
        need -= Fsd * (v.Cp - cm) + tqEng + Propellant.Trim(v, (v.F * Math.Sin(v.Gimbal) + Fsd) / m);
        double gWant = v.Direct ? v.GimCmd : 0;
        if (!v.Direct && v.F > 1e3) gWant = Const.Clamp(Math.Asin(Const.Clamp(-need / (v.F * cm), -1, 1)), -gimLim, gimLim);
        double gStep = Const.GIM_RATE * Const.D2R * dt;
        double g = Const.Clamp(v.Gimbal + Const.Clamp(gWant - v.Gimbal, -gStep, gStep), -gimLim, gimLim);
        if (v.F > 1e3) need += v.F * Math.Sin(g) * cm;
        v.Gimbal = g;
        double fl = v.Direct ? Const.Clamp(v.FinCmd, -1, 1) : 0;
        if (!v.Direct && flapAuth > 1e3) { fl = Const.Clamp(need / flapAuth, -1, 1); need -= fl * flapAuth; }
        if (v.Kind == Kind.Ship) v.Flap = fl; else v.Fin = fl;
        double rcsT = v.Direct ? Const.Clamp(v.RcsCmd, -1, 1) * rcsAuth : Const.Clamp(need, -rcsAuth, rcsAuth);
        v.RcsUse = rcsAuth > 1 ? rcsT / rcsAuth : 0;
        double torque = Fsd * (v.Cp - cm) + tqEng - v.F * Math.Sin(g) * cm + fl * flapAuth + rcsT;
        Vec2 fa = Aero.World(v, af, vr);
        double FaX = fa.X, FaY = fa.Y;
        Nav.Observe(sim, v, FaX, FaY, af.Drag, h, dt);
        double fUll = (v.Ullage ? Propellant.UllageF(v) : 0) + (v.Stacked && v.Mate != null ? v.Mate.F : 0);
        double tvx = (ax.X * Math.Cos(g) + sd.X * Math.Sin(g)) * v.F + ax.X * fUll;
        double tvy = (ax.Y * Math.Cos(g) + sd.Y * Math.Sin(g)) * v.F + ax.Y * fUll;
        double gr = Const.MU / (v.R * v.R);
        double aAx = (v.F * Math.Cos(g) + Fax + fUll) / m;
        if (!v.Launched) aAx = Math.Max(aAx, gr);
        Propellant.Settle(v, aAx, dt);
        if (v.Stacked && v.Mate != null) { v.Mate.AAx = v.AAx; v.Mate.Settled = v.Settled; }
        torque += Propellant.Slosh(v, cm, (v.F * Math.Sin(g) + Fsd) / m, dt);
        double Fx = tvx + FaX, Fy = tvy + FaY;
        v.Acc = Math.Sqrt(Fx * Fx + Fy * Fy) / m / Const.G0;
        if (v.Acc > v.MaxG) v.MaxG = v.Acc;
        v.Vx += (Fx / m - gr * up.X) * dt;
        v.Vy += (Fy / m - gr * up.Y) * dt;
        v.X += v.Vx * dt; v.Y += v.Vy * dt;
        v.OmDot = (torque - Aero.PitchDamping(v, at.Rho, sp, af.Alpha, cm) * v.Om) / I;
        v.Om += v.OmDot * dt;
        v.Th += v.Om * dt;
        if (v.Th > Math.PI) v.Th -= 2 * Math.PI; else if (v.Th < -Math.PI) v.Th += 2 * Math.PI;
        if (v.Catch && !v.Caught && !v.Crashed && v.Launched &&
            v.Alt <= Const.CATCH_H && v.Alt > Const.CATCH_H - Const.CATCH_WIN &&
            (v.Mode == "landB" || v.Mode == "landS")) {
            double miss = sim.Downrange(v), vd = -v.VVert, vh = Math.Abs(v.VHor);
            bool closed = sim.ArmGapL + miss < Const.CATCH_RAIL && sim.ArmGapR - miss < Const.CATCH_RAIL;
            double tilt = Math.Abs(Vehicle.AngDiff(v.Th, 0)) * Const.R2D;
            if (Math.Abs(miss) < Const.ARM_GAP_READY && closed && vd < Const.CATCH_VV && vd > -2.5 &&
                vh < Const.CATCH_VH && tilt < Const.CATCH_TILT && Math.Abs(v.Om) * Const.R2D < Const.CATCH_OM) {
                v.HeldVh = v.VHor; v.HeldOm = v.Om; v.CatchH = v.Alt;
                v.Vx = -Const.W * v.Y; v.Vy = Const.W * v.X; v.Om = 0;
                v.Caught = true; v.Landed = true; v.Mode = "caught"; v.CatchVd = vd;
                v.Ign = false; v.NEng = 0; v.F = 0;
                Quench(v);
                string side = Math.Abs(miss) < 0.5 ? "по центру"
                    : $"промах {Math.Abs(miss):F1} м к {(miss > 0 ? "морю" : "суше")}: дальняя рука прошла на {2 * Math.Abs(miss):F0} м больше";
                sim.LogMsg($"{v.Tag}: ЗАХВАТ БАШНЕЙ — руки сомкнулись ({vd:F1} м/с, {side})", 1);
            }
        }
        if (v.Alt > 3) v.Launched = true;
        if (v.Alt <= 0 && !v.Launched) {
            double ang = Math.Atan2(v.X, v.Y);
            v.X = Const.RE * Math.Sin(ang); v.Y = Const.RE * Math.Cos(ang);
            Vec2 u = v.Up;
            double vv = v.Vx * u.X + v.Vy * u.Y;
            if (vv < 0) { v.Vx -= vv * u.X; v.Vy -= vv * u.Y; }
            v.Om = 0; v.Th = 0; v.Acc = 0;
            return;
        }
        double gdr = sim.Downrange(v), gnd = SimState.Surface(gdr);
        if (v.Alt <= gnd) {
            double vd = -v.VVert, vh = Math.Abs(v.VHor), tilt = Math.Abs(Vehicle.AngDiff(v.Th, 0)) * Const.R2D;
            double ang = Math.Atan2(v.X, v.Y);
            v.X = (Const.RE + gnd) * Math.Sin(ang); v.Y = (Const.RE + gnd) * Math.Cos(ang);
            v.Vx = -Const.W * v.Y; v.Vy = Const.W * v.X; v.Om = 0;
            v.Landed = true; v.Ign = false; v.NEng = 0; v.F = 0;
            v.Splash = SimState.Water(gdr); v.HeldVh = v.VHor;
            Quench(v);
            bool soft = vd < 7 && vh < 6 && tilt < 12;
            if (soft && v.Splash) {
                v.Mode = "landed";
                string tail = Math.Abs(gdr) > 120e3 ? $", {gdr / 1000:F0} км от старта" : $", {gdr / 1000:F1} км от башни";
                sim.LogMsg($"{v.Tag}: ПРИВОДНЕНИЕ — посадка выполнена ({vd:F1} м/с{tail})", 1);
            }
            else if (soft) {
                v.Crashed = true; v.Mode = "crashed";
                sim.LogMsg($"{v.Tag}: КАСАНИЕ СУШИ — опор нет, корпус опрокинулся ({vd:F1} м/с, {gdr / 1000:F1} км от башни)", 3);
            }
            else {
                v.Crashed = true; v.Mode = "crashed";
                sim.LogMsg($"{v.Tag}: РАЗРУШЕНИЕ при ударе о {(v.Splash ? "воду" : "поверхность")} ({vd:F0} м/с, крен {tilt:F0}°)", 3);
            }
        }
        double aoaDev = Math.Min(Math.Abs(v.Alpha), Math.PI - Math.Abs(v.Alpha));
        v.AoaDev = aoaDev;
        if (v.Q > 200e3 && !v.Crashed && v.Alt > 0) {
            v.Crashed = true; v.Alive = false; v.Mode = "crashed";
            sim.LogMsg($"{v.Tag}: разрушение конструкции — скоростной напор {(v.Q / 1000):F0} кПа", 3);
        }
        if (v.Q > 90e3 && aoaDev > 28 * Const.D2R && !v.Crashed && v.Alt > 0) {
            v.Crashed = true; v.Alive = false; v.Mode = "crashed";
            sim.LogMsg($"{v.Tag}: разрушение конструкции — скор. напор {(v.Q / 1000):F0} кПа при α={(v.Alpha * Const.R2D):F0}°", 3);
        }
    }
}
