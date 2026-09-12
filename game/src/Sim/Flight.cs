using System;
namespace Starship.Physics;
public static class Flight {
    public static void Quench(Vehicle v) {
        foreach (Engine e in v.Eng) { e.On = false; e.Spool = 0; e.F = 0; e.Md = 0; e.Pc = 0; e.Thr = 0; }
        v.Mdot = 0;
    }
    public static void StepVehicle(SimState sim, Vehicle v, double dt) {
        if (!v.Alive || v.Landed || v.Attached) return;
        double h = v.Alt;
        Air at = Atmosphere.At(h, sim.RhoK);
        Vec2 up = v.Up, ax = v.Axis, sd = v.Side;
        v.WindE = sim.Wind.At(h);
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
            double dCn = 2 * Math.Abs(Math.Sin(2 * v.Alpha)) * Math.Sin(Const.FLAP_DEF);
            double swept = 2 * Const.FLAP_S_FWD * Math.Abs(Const.FLAP_Y_FWD - cm)
                         + 2 * Const.FLAP_S_AFT * Math.Abs(Const.FLAP_Y_AFT - cm);
            flapAuth = q * swept * dCn * v.CtrlK;
        }
        else {
            double dCn = Const.FIN_CN * Math.Sin(Const.FLAP_DEF);
            flapAuth = q * 3 * Const.FIN_S * Math.Abs(Const.FIN_Y - cm) * dCn * v.FinDep * v.CtrlK;
        }
        double rcsAuth = v.Rcs ? (v.Kind == Kind.Booster ? 2.4e6 : 1.1e6) * v.RcsK : 0;
        double gimLim = double.IsNaN(v.GimLim) ? v.Spec.Gimbal : v.GimLim * Const.D2R;
        double gimAuth = v.F > 1e3 ? v.F * Math.Sin(gimLim) * cm : 0;
        double aAvail = Math.Max(0.015,
            (gimAuth + flapAuth + rcsAuth - Math.Abs(Fsd * (v.Cp - cm))) / I);
        double aLim = Math.Min(v.Mode == "flipS" ? Const.OM_ACC_FLIP : Const.OM_ACC_MAX, aAvail);
        double err = Vehicle.AngDiff(v.ThCmd, v.Th);
        double aDes = Const.Clamp(v.Kp * err - v.Kd * v.Om, -aLim, aLim);
        double need = aDes * I;
        need -= Fsd * (v.Cp - cm);
        double g = 0;
        if (v.F > 1e3) {
            g = Const.Clamp(Math.Asin(Const.Clamp(-need / (v.F * cm), -1, 1)), -gimLim, gimLim);
            need += v.F * Math.Sin(g) * cm;
        }
        v.Gimbal = g;
        double fl = 0;
        if (flapAuth > 1e3) { fl = Const.Clamp(need / flapAuth, -1, 1); need -= fl * flapAuth; }
        if (v.Kind == Kind.Ship) v.Flap = fl; else v.Fin = fl;
        double rcsT = Const.Clamp(need, -rcsAuth, rcsAuth);
        v.RcsUse = rcsAuth > 1 ? rcsT / rcsAuth : 0;
        double torque = Fsd * (v.Cp - cm) - v.F * Math.Sin(g) * cm + fl * flapAuth + rcsT;
        double FaX = Fax * ax.X + Fsd * sd.X, FaY = Fax * ax.Y + Fsd * sd.Y;
        if (v.Bank > 0.01 && sp > 1) {
            double dx = vr.X / sp, dy = vr.Y / sp;
            double alng = FaX * dx + FaY * dy;
            double lx = FaX - alng * dx, ly = FaY - alng * dy;
            double cb = Math.Cos(v.Bank);
            FaX = alng * dx + lx * cb; FaY = alng * dy + ly * cb;
        }
        double tvx = (ax.X * Math.Cos(g) + sd.X * Math.Sin(g)) * v.F;
        double tvy = (ax.Y * Math.Cos(g) + sd.Y * Math.Sin(g)) * v.F;
        double Fx = tvx + FaX, Fy = tvy + FaY;
        double gr = Const.MU / (v.R * v.R);
        v.Acc = Math.Sqrt(Fx * Fx + Fy * Fy) / m / Const.G0;
        if (v.Acc > v.MaxG) v.MaxG = v.Acc;
        v.Vx += (Fx / m - gr * up.X) * dt;
        v.Vy += (Fy / m - gr * up.Y) * dt;
        v.X += v.Vx * dt; v.Y += v.Vy * dt;
        v.Om += torque / I * dt;
        v.Om *= 1 - Const.OM_DAMP * dt;
        v.Th += v.Om * dt;
        if (v.Th > Math.PI) v.Th -= 2 * Math.PI; else if (v.Th < -Math.PI) v.Th += 2 * Math.PI;
        if (v.SeekPad && !v.Caught && !v.Crashed && v.Launched &&
            v.Alt <= Const.CATCH_H && v.Alt > Const.CATCH_H - Const.CATCH_WIN &&
            (v.Mode == "landB" || v.Mode == "landS")) {
            double dr = Math.Abs(sim.Downrange(v)), vd = -v.VVert, vh = Math.Abs(v.VHor);
            double tilt = Math.Abs(Vehicle.AngDiff(v.Th, 0)) * Const.R2D;
            if (dr < Const.CATCH_DR && vd < Const.CATCH_VV && vd > -2.5 &&
                vh < Const.CATCH_VH && tilt < Const.CATCH_TILT) {
                double ang = Math.Atan2(v.X, v.Y), rr = Const.RE + Const.CATCH_H;
                v.X = rr * Math.Sin(ang); v.Y = rr * Math.Cos(ang);
                v.Vx = -Const.W * v.Y; v.Vy = Const.W * v.X; v.Om = 0; v.Th = 0;
                v.Caught = true; v.Landed = true; v.Mode = "caught";
                v.Ign = false; v.NEng = 0; v.F = 0;
                Quench(v);
                sim.LogMsg($"{v.Tag}: ЗАХВАТ БАШНЕЙ — руки сомкнулись ({vd:F1} м/с, промах {dr:F1} м)", 1);
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
        if (v.Alt <= 0) {
            double vd = -v.VVert, vh = Math.Abs(v.VHor), tilt = Math.Abs(Vehicle.AngDiff(v.Th, 0)) * Const.R2D;
            double ang = Math.Atan2(v.X, v.Y);
            v.X = Const.RE * Math.Sin(ang); v.Y = Const.RE * Math.Cos(ang);
            v.Vx = -Const.W * v.Y; v.Vy = Const.W * v.X; v.Om = 0;
            v.Landed = true; v.Ign = false; v.NEng = 0; v.F = 0;
            Quench(v);
            bool far = Math.Abs(sim.Downrange(v)) > 120e3;
            if (vd < 7 && vh < 6 && tilt < 12) {
                v.Mode = "landed";
                string tail = far ? ", " + (sim.Downrange(v) / 1000).ToString("F0") + " км от старта" : "";
                sim.LogMsg($"{v.Tag}: {(far ? "ПРИВОДНЕНИЕ" : "КАСАНИЕ")} — посадка выполнена ({vd:F1} м/с{tail})", 1);
            }
            else {
                v.Crashed = true; v.Mode = "crashed";
                sim.LogMsg($"{v.Tag}: РАЗРУШЕНИЕ при ударе о поверхность ({vd:F0} м/с, крен {tilt:F0}°)", 3);
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
