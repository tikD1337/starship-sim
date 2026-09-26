using System;
using Starship.Physics;
namespace Starship.Tests;
internal static partial class Program {
    private static void SocpChecks() {
        Head("Решатель SOCP: задачи с известным ответом");
        SocpProblem P(double[] c, double[,] g, double[] h, int linear, params int[] soc)
            => new SocpProblem { C = c, A = new double[0, c.Length], G = g, H = h, Linear = linear, Soc = soc };
        SocpResult lp = Socp.Solve(P(new[] { -1.0, -1 }, new double[,] { { 1, 1 }, { -1, 0 }, { 0, -1 } }, new[] { 1.0, 0, 0 }, 3));
        SocpResult pr = Socp.Solve(P(new[] { 0.0, 0, 0, 1 },
            new double[,] { { 0, 0, 0, -1 }, { -1, 0, 0, 0 }, { 0, -1, 0, 0 }, { 0, 0, -1, 0 }, { -1, 0, 0, 0 }, { 0, -1, 0, 0 }, { 0, 0, -1, 0 } },
            new[] { 0.0, -3, -4, 0, 0, 0, 0 }, 0, 4, 3));
        SocpResult inf = Socp.Solve(P(new[] { 1.0 }, new double[,] { { -1 }, { 1 } }, new[] { -1.0, 0 }, 2));
        SocpResult unb = Socp.Solve(P(new[] { -1.0 }, new double[,] { { -1 } }, new[] { 0.0 }, 1));
        double[] s = { 0.7, 1.3, 2.0, 0.4, -0.9, 3.0, 1.1, 0.5, -0.8 }, z = { 1.9, 0.2, 1.5, -0.6, 0.3, 2.2, -0.4, 1.0, 0.9 };
        (double[] wz, double[] wis) = Socp.Scale(s, z, 2, new[] { 3, 4 });
        double nt = wz.Length == s.Length && wis.Length == s.Length ? 0 : double.NaN;
        for (int i = 0; i < s.Length && !double.IsNaN(nt); i++) nt = Math.Max(nt, Math.Abs(wz[i] - wis[i]));
        double X(SocpResult r, int i) => r.X == null ? double.NaN : r.X[i];
        Group("решатель SOCP", $"ЛП {lp.Status} {N(lp.Objective)} за {lp.Iterations}; проекция {pr.Status} ({N(X(pr, 0))}, {N(X(pr, 1))}, {N(X(pr, 2))}); недопустимая {inf.Status}; неограниченная {unb.Status}; НТ {N(nt)}",
              ("линейная задача: −1", lp.Status == SocpStatus.Optimal && Math.Abs(lp.Objective + 1) < 1e-6),
              ("проекция (3,4,0) на конус — (3,5; 3,5; 0)", pr.Status == SocpStatus.Optimal && Math.Abs(X(pr, 0) - 3.5) < 1e-5 && Math.Abs(X(pr, 1) - 3.5) < 1e-5 && Math.Abs(X(pr, 2)) < 1e-5),
              ("недопустимая распознана", inf.Status == SocpStatus.Infeasible),
              ("неограниченная распознана", unb.Status == SocpStatus.Unbounded),
              ("масштабирование Нестерова—Тодда: W z = W⁻¹ s", nt < 1e-9));
    }
    private static void GfoldChecks() {
        Head("G-FOLD: посадочный прожиг как задача оптимизации");
        (double[,] wv, double[,] wr) = Gfold.Weights(12);
        double idv = 0, idr = 0;
        for (int k = 0; k <= 12; k++) {
            double sv = 0, sr = 0;
            for (int i = 0; i <= 12; i++) { sv += wv[k, i]; sr += wr[k, i]; }
            idv = Math.Max(idv, Math.Abs(sv - k)); idr = Math.Max(idr, Math.Abs(sr - k * k / 2.0));
        }
        double[] acc = new double[13];
        for (int i = 0; i <= 12; i++) acc[i] = Math.Sin(0.7 * i) + 0.3 * i;
        double dt = 0.8, pv = 0, pr = 0, prop = 0;
        for (int k = 1; k <= 12; k++) {
            for (int j = 0; j < 1000; j++) {
                double f = (j + 0.5) / 1000, a = acc[k - 1] + (acc[k] - acc[k - 1]) * f, h = dt / 1000;
                pr += pv * h + 0.5 * a * h * h; pv += a * h;
            }
            double av = 0, ar = 0;
            for (int i = 0; i <= 12; i++) { av += wv[k, i] * acc[i] * dt; ar += wr[k, i] * acc[i] * dt * dt; }
            prop = Math.Max(prop, Math.Max(Math.Abs(av - pv), Math.Abs(ar - pr)));
        }
        GfoldSetup Booster(double x0, double y0, double vx0, double vy0) => new GfoldSetup {
            X0 = x0, Y0 = y0, Vx0 = vx0, Vy0 = vy0, Mass0 = 300e3, MassMin = 250e3, Alpha = 1 / (330 * Const.G0),
            Rho1 = 3 * 0.4 * 2.3e6, Rho2 = 3 * 2.3e6, G = 9.81, Tvy = -1.5, Step = 0.5 };
        GfoldPlan v = Gfold.Plan(Booster(0, 2000, 0, -100), 6, 80);
        int n = v.Nodes;
        bool ok = v.Status == SocpStatus.Optimal;
        double tLo = double.PositiveInfinity, tHi = 0;
        for (int k = 0; ok && k <= n; k++) {
            double t = Math.Sqrt(v.Ux[k] * v.Ux[k] + v.Uy[k] * v.Uy[k]) * v.Mass[k];
            tLo = Math.Min(tLo, t); tHi = Math.Max(tHi, t);
        }
        bool inBounds = ok && tLo > 3 * 0.4 * 2.3e6 * 0.999 && tHi < 3 * 2.3e6 * 1.001;
        GfoldSetup free = Booster(300, 2000, 20, -100); free.ThetaMax = Math.PI / 2; free.Glide = 0;
        GfoldPlan l = Gfold.Plan(free, 6, 80);
        double gap = l.Status == SocpStatus.Optimal ? l.Slack : double.NaN;
        GfoldPlan far = Gfold.Plan(Booster(40000, 2000, 0, -100), 6, 80);
        double P(double[] a, int i) => ok ? a[i] : double.NaN;
        Group("G-FOLD: дискретизация, попадание, границы тяги, релаксация без потерь, недостижимость",
              $"тождества {N(idv)}/{N(idr)}, пошагово {N(prop)}; отвесно {v.Status}, {n} узлов: {N(P(v.X, n))}/{N(P(v.Y, n))} м, {N(P(v.Vy, n))} м/с, топливо {N(v.Fuel)} кг, невязка до поправки {N(v.Slack)}, тяга {N(tLo / 1e6)}…{N(tHi / 1e6)} МН; ‖u‖ − σ до {N(gap)}; 40 км вбок {far.Status}",
              ("веса: Σwᵛ = k, Σwʳ = k²/2", idv < 1e-12 && idr < 1e-12),
              ("веса совпадают с пошаговым интегрированием", prop < 1e-6),
              ("отвесная посадка приходит в точку", ok && Math.Abs(v.X[n]) < 1 && Math.Abs(v.Y[n]) < 1 && Math.Abs(v.Vy[n] + 1.5) < 0.5),
              ("тяга в паспорте, масса не ниже минимальной", inBounds && v.Mass[n] >= 250e3 && v.Fuel > 0),
              ("релаксация без потерь: ‖u‖ = σ", l.Status == SocpStatus.Optimal && gap < 1e-4),
              ("недостижимая цель — не Optimal", far.Status != SocpStatus.Optimal));
    }
}
