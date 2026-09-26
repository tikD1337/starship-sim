using System;
using System.Collections.Generic;
namespace Starship.Physics;
public sealed class GfoldSetup {
    public double X0, Y0, Vx0, Vy0, Mass0, MassMin, Alpha, Rho1, Rho2, G, Tvx, Tvy;
    public double ThetaMax = 20 * Const.D2R, Glide = 45 * Const.D2R, GlideDepth = 5, Step = 0.5;
    public Func<double, (double X, double Y)> Bias;
    public int Nodes = 20;
    public GfoldSetup With(int nodes) {
        var c = (GfoldSetup)MemberwiseClone();
        c.Nodes = nodes;
        return c;
    }
}
public sealed class GfoldPlan {
    public SocpStatus Status = SocpStatus.Infeasible;
    public int Nodes, Iterations;
    public double Step, Fuel = double.NaN, Miss = double.PositiveInfinity, Slack;
    public double[] Ux, Uy, Sigma, X, Y, Vx, Vy, Mass;
    public double Time => Nodes * Step;
    public (double Ux, double Uy, double X, double Y, double Vx, double Vy) At(double t) {
        double f = Math.Clamp(t / Step, 0, Nodes);
        int k = Math.Min((int)Math.Floor(f), Nodes - 1);
        double a = f - k;
        double L(double[] v) => v[k] + (v[k + 1] - v[k]) * a;
        return (L(Ux), L(Uy), L(X), L(Y), L(Vx), L(Vy));
    }
}
public static class Gfold {
    public static (double[,] Wv, double[,] Wr) Weights(int nodes) {
        var wv = new double[nodes + 1, nodes + 1];
        var wr = new double[nodes + 1, nodes + 1];
        for (int k = 1; k <= nodes; k++)
            for (int j = 0; j < k; j++) {
                wv[k, j] += 0.5; wv[k, j + 1] += 0.5;
                wr[k, j] += (k - j) / 2.0 - 1 / 6.0;
                wr[k, j + 1] += (k - j) / 2.0 - 1 / 3.0;
            }
        return (wv, wr);
    }
    public static GfoldPlan Solve(GfoldSetup s) {
        int n = s.Nodes;
        if (n < 2) return new GfoldPlan { Nodes = n, Step = s.Step };
        (double[,] wv, double[,] wr) = Weights(n);
        double len = Math.Max(Math.Sqrt(s.X0 * s.X0 + s.Y0 * s.Y0), 10), tim = Math.Sqrt(len / s.G), ms = s.Mass0;
        double acs = len / (tim * tim), vs = len / tim, dt = s.Step / tim, alpha = s.Alpha * len / tim;
        var ax = new double[n + 1];
        var ay = new double[n + 1];
        var z0 = new double[n + 1];
        var mu1 = new double[n + 1];
        var mu2 = new double[n + 1];
        for (int k = 0; k <= n; k++) {
            (double bx, double by) = s.Bias != null ? s.Bias(k * s.Step) : (0, 0);
            ax[k] = bx / acs; ay[k] = (by - s.G) / acs;
            double mmin = Math.Max(s.Mass0 - s.Alpha * s.Rho2 * k * s.Step, 0.5 * s.MassMin);
            z0[k] = Math.Log(mmin / ms);
            double e = Math.Exp(-z0[k]) / (ms * acs);
            mu1[k] = s.Rho1 * e; mu2[k] = s.Rho2 * e;
        }
        var rox = new double[n + 1];
        var roy = new double[n + 1];
        var vox = new double[n + 1];
        var voy = new double[n + 1];
        double x0 = s.X0 / len, y0 = s.Y0 / len, u0 = s.Vx0 / vs, w0 = s.Vy0 / vs;
        for (int k = 0; k <= n; k++) {
            double sv = 0, svy = 0, sr = 0, sry = 0;
            for (int i = 0; i <= k; i++) {
                sv += wv[k, i] * ax[i]; svy += wv[k, i] * ay[i];
                sr += wr[k, i] * ax[i]; sry += wr[k, i] * ay[i];
            }
            vox[k] = u0 + dt * sv; voy[k] = w0 + dt * svy;
            rox[k] = x0 + k * dt * u0 + dt * dt * sr; roy[k] = y0 + k * dt * w0 + dt * dt * sry;
        }
        int nv = 3 * (n + 1);
        int UX(int k) => 3 * k;
        int UY(int k) => 3 * k + 1;
        int SG(int k) => 3 * k + 2;
        bool glide = s.Glide > 0 && s.Glide < Math.PI / 2;
        double tg = glide ? Math.Tan(s.Glide) : 0, depth = s.GlideDepth / len, cth = Math.Cos(s.ThetaMax);
        bool point = s.ThetaMax < Math.PI / 2 - 1e-9;
        var lin = new List<(Dictionary<int, double> Row, double H)>();
        for (int k = 0; k <= n; k++) {
            var up = new Dictionary<int, double> { [SG(k)] = 1 };
            for (int i = 0; i <= k; i++) Acc(up, SG(i), -mu2[k] * alpha * dt * wv[k, i]);
            lin.Add((up, mu2[k] * (1 + z0[k])));
            if (point) lin.Add((new Dictionary<int, double> { [UY(k)] = -1, [SG(k)] = cth }, 0));
            if (glide && k >= 1) {
                var a = new Dictionary<int, double>();
                var b = new Dictionary<int, double>();
                for (int i = 0; i <= k; i++) {
                    double c = dt * dt * wr[k, i];
                    if (c == 0) continue;
                    Acc(a, UX(i), tg * c); Acc(a, UY(i), -c);
                    Acc(b, UX(i), -tg * c); Acc(b, UY(i), -c);
                }
                lin.Add((a, depth - tg * rox[k] + roy[k]));
                lin.Add((b, depth + tg * rox[k] + roy[k]));
            }
        }
        var fin = new Dictionary<int, double>();
        for (int i = 0; i <= n; i++) Acc(fin, SG(i), alpha * dt * wv[n, i]);
        lin.Add((fin, -Math.Log(s.MassMin / ms)));
        int ml = lin.Count, m = ml + 6 * (n + 1);
        var g = new double[m, nv];
        var h = new double[m];
        for (int r = 0; r < ml; r++) {
            foreach (var kv in lin[r].Row) g[r, kv.Key] = kv.Value;
            h[r] = lin[r].H;
        }
        int row = ml;
        for (int k = 0; k <= n; k++) {
            g[row, SG(k)] = -1; g[row + 1, UX(k)] = -1; g[row + 2, UY(k)] = -1;
            row += 3;
        }
        for (int k = 0; k <= n; k++) {
            for (int i = 0; i <= k; i++) {
                double c = -alpha * dt * wv[k, i];
                g[row, SG(i)] -= c; g[row + 1, SG(i)] -= c; g[row + 2, SG(i)] -= c;
            }
            g[row, SG(k)] -= 1 / mu1[k]; g[row + 2, SG(k)] -= 1 / mu1[k];
            h[row] = -0.5 - z0[k]; h[row + 1] = -z0[k]; h[row + 2] = -1.5 - z0[k];
            row += 3;
        }
        var a4 = new double[4, nv];
        var b4 = new double[4];
        for (int i = 0; i <= n; i++) {
            a4[0, UX(i)] = dt * wv[n, i]; a4[1, UY(i)] = dt * wv[n, i];
            a4[2, UX(i)] = dt * dt * wr[n, i]; a4[3, UY(i)] = dt * dt * wr[n, i];
        }
        b4[0] = s.Tvx / vs - vox[n]; b4[1] = s.Tvy / vs - voy[n];
        b4[2] = -rox[n]; b4[3] = -roy[n];
        var cvec = new double[nv];
        for (int i = 0; i <= n; i++) cvec[SG(i)] = dt * wv[n, i];
        var soc = new int[2 * (n + 1)];
        Array.Fill(soc, 3);
        SocpResult res = Socp.Solve(new SocpProblem { C = cvec, A = a4, B = b4, G = g, H = h, Linear = ml, Soc = soc });
        var plan = new GfoldPlan { Status = res.Status, Nodes = n, Step = s.Step, Iterations = res.Iterations };
        if (res.Status != SocpStatus.Optimal) return plan;
        double[] xv = res.X;
        plan.Ux = new double[n + 1]; plan.Uy = new double[n + 1]; plan.Sigma = new double[n + 1];
        plan.X = new double[n + 1]; plan.Y = new double[n + 1]; plan.Vx = new double[n + 1]; plan.Vy = new double[n + 1];
        plan.Mass = new double[n + 1];
        for (int k = 0; k <= n; k++) {
            plan.Ux[k] = xv[UX(k)] * acs; plan.Uy[k] = xv[UY(k)] * acs; plan.Sigma[k] = xv[SG(k)] * acs;
            double vx = vox[k], vy = voy[k], px = rox[k], py = roy[k], zk = 0;
            for (int i = 0; i <= k; i++) {
                vx += dt * wv[k, i] * xv[UX(i)]; vy += dt * wv[k, i] * xv[UY(i)];
                px += dt * dt * wr[k, i] * xv[UX(i)]; py += dt * dt * wr[k, i] * xv[UY(i)];
                zk -= alpha * dt * wv[k, i] * xv[SG(i)];
            }
            plan.Vx[k] = vx * vs; plan.Vy[k] = vy * vs; plan.X[k] = px * len; plan.Y[k] = py * len;
            plan.Mass[k] = ms * Math.Exp(zk);
        }
        Executable(plan, s);
        return plan;
    }
    private static void Executable(GfoldPlan p, GfoldSetup s) {
        int n = p.Nodes;
        double dt = p.Step;
        for (int k = 0; k <= n; k++) {
            double norm = Math.Sqrt(p.Ux[k] * p.Ux[k] + p.Uy[k] * p.Uy[k]);
            p.Slack = Math.Max(p.Slack, (p.Sigma[k] - norm) / Math.Max(p.Sigma[k], 1e-9));
            double lo = s.Rho1 / p.Mass[k], hi = s.Rho2 / p.Mass[k], f = 1;
            if (norm < 1e-9) { p.Ux[k] = 0; p.Uy[k] = lo; norm = lo; }
            else if (norm < lo) f = lo / norm;
            else if (norm > hi) f = hi / norm;
            p.Ux[k] *= f; p.Uy[k] *= f;
            p.Sigma[k] = norm * f;
        }
        double m = s.Mass0, x = s.X0, y = s.Y0, vx = s.Vx0, vy = s.Vy0;
        for (int k = 0; k <= n; k++) {
            p.Mass[k] = m; p.X[k] = x; p.Y[k] = y; p.Vx[k] = vx; p.Vy[k] = vy;
            if (k == n) break;
            (double b0x, double b0y) = s.Bias != null ? s.Bias(k * dt) : (0, 0);
            (double b1x, double b1y) = s.Bias != null ? s.Bias((k + 1) * dt) : (0, 0);
            double a0x = p.Ux[k] + b0x, a0y = p.Uy[k] + b0y - s.G, a1x = p.Ux[k + 1] + b1x, a1y = p.Uy[k + 1] + b1y - s.G;
            x += vx * dt + dt * dt * (a0x / 3 + a1x / 6); y += vy * dt + dt * dt * (a0y / 3 + a1y / 6);
            vx += dt * (a0x + a1x) / 2; vy += dt * (a0y + a1y) / 2;
            m *= Math.Exp(-s.Alpha * dt * (p.Sigma[k] + p.Sigma[k + 1]) / 2);
        }
        p.Miss = Math.Sqrt(p.X[n] * p.X[n] + p.Y[n] * p.Y[n]);
        p.Fuel = s.Mass0 - p.Mass[n];
    }
    private static void Acc(Dictionary<int, double> row, int col, double v) {
        row.TryGetValue(col, out double old);
        row[col] = old + v;
    }
    private static bool Better(GfoldPlan a, GfoldPlan b)
        => b.Status == SocpStatus.Optimal && (a == null || a.Status != SocpStatus.Optimal
            || (Math.Abs(a.Miss - b.Miss) > 1 ? b.Miss < a.Miss : b.Fuel < a.Fuel));
    public static GfoldPlan Plan(GfoldSetup s, int minNodes, int maxNodes) {
        minNodes = Math.Max(minNodes, 2);
        var cache = new Dictionary<int, GfoldPlan>();
        GfoldPlan Eval(int nodes) {
            if (!cache.TryGetValue(nodes, out GfoldPlan p)) cache[nodes] = p = Solve(s.With(nodes));
            return p;
        }
        GfoldPlan best = null;
        int bestN = -1;
        const int coarse = 6;
        for (int i = 0; i < coarse; i++) {
            int nodes = minNodes + (int)Math.Round((maxNodes - minNodes) * i / (double)(coarse - 1));
            GfoldPlan p = Eval(nodes);
            if (Better(best, p)) { best = p; bestN = nodes; }
        }
        if (bestN < 0) return Eval(maxNodes);
        int step = Math.Max(1, (maxNodes - minNodes) / (coarse - 1));
        int lo = Math.Max(minNodes, bestN - step), hi = Math.Min(maxNodes, bestN + step);
        const double phi = 0.6180339887498949;
        for (int i = 0; i < 6 && hi - lo > 2; i++) {
            int a = (int)Math.Round(hi - phi * (hi - lo)), b = (int)Math.Round(lo + phi * (hi - lo));
            if (a == b) b = Math.Min(hi, a + 1);
            GfoldPlan pa = Eval(a), pb = Eval(b);
            if (Better(best, pa)) best = pa;
            if (Better(best, pb)) best = pb;
            if (Fuel(pa) <= Fuel(pb)) hi = b; else lo = a;
        }
        for (int nodes = lo; nodes <= hi && hi - lo <= 2; nodes++) {
            GfoldPlan p = Eval(nodes);
            if (Better(best, p)) best = p;
        }
        return best;
    }
    private static double Fuel(GfoldPlan p) => p.Status == SocpStatus.Optimal ? p.Fuel + 1e3 * Math.Max(p.Miss - 1, 0) : double.PositiveInfinity;
    public static GfoldPlan Replan(GfoldSetup s, int nodesHint, int minNodes, int maxNodes) {
        GfoldPlan best = null;
        for (int d = -1; d <= 1; d++) {
            int nodes = Math.Clamp(nodesHint + d, Math.Max(minNodes, 2), maxNodes);
            GfoldPlan p = Solve(s.With(nodes));
            if (Better(best, p)) best = p;
        }
        return best != null && best.Status == SocpStatus.Optimal ? best : Plan(s, minNodes, maxNodes);
    }
}
