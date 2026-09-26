using System;
namespace Starship.Physics;
public enum SocpStatus { Optimal, Infeasible, Unbounded, MaxIterations, Numerical }
public sealed class SocpProblem {
    public double[] C = Array.Empty<double>(), B = Array.Empty<double>(), H = Array.Empty<double>();
    public double[,] A = new double[0, 0], G = new double[0, 0];
    public int Linear;
    public int[] Soc = Array.Empty<int>();
}
public readonly struct SocpResult {
    public readonly SocpStatus Status;
    public readonly double[] X;
    public readonly double Objective;
    public readonly int Iterations;
    public SocpResult(SocpStatus status, double[] x, double objective, int iterations) {
        Status = status; X = x; Objective = objective; Iterations = iterations;
    }
}
public static class Socp {
    public const double TOL = 1e-8, TOL_LOOSE = 1e-6;
    public static (double[] Wz, double[] Winvs) Scale(double[] s, double[] z, int linear, int[] soc) {
        var w = new Nt(new Cones(linear, soc), s, z);
        return (w.Mul(z), w.InvMul(s));
    }
    public static SocpResult Solve(SocpProblem p, int maxIter = 60) {
        int n = p.C.Length, pr = p.B.Length, m = p.H.Length;
        var k = new Cones(p.Linear, p.Soc);
        var kkt = new Kkt(p, k);
        kkt.Factor(null);
        var (x, _, zp) = kkt.Solve(new double[n], p.B, p.H);
        double[] s = k.Shift(Neg(zp));
        var (_, y, zd) = kkt.Solve(Neg(p.C), new double[pr], new double[m]);
        double[] z = k.Shift(zd);
        double tau = 1, kap = 1;
        double[] loose = null;
        double looseObj = double.NaN;
        SocpResult Fail(int it) => loose != null ? new SocpResult(SocpStatus.Optimal, loose, looseObj, it) : new SocpResult(SocpStatus.Numerical, null, double.NaN, it);
        double nb = Math.Max(1, Norm(p.B) + Norm(p.H)), nc = Math.Max(1, Norm(p.C));
        for (int it = 0; it < maxIter; it++) {
            double[] rx = Add(Add(Mt(p.A, y), Mt(p.G, z)), Scal(p.C, tau));
            double[] ry = Add(Neg(Mv(p.A, x)), Scal(p.B, tau));
            double[] rz = Sub(Add(Neg(Mv(p.G, x)), Scal(p.H, tau)), s);
            double cx = Dot(p.C, x), by = Dot(p.B, y), hz = Dot(p.H, z);
            double rt = -cx - by - hz - kap;
            double sz = Dot(s, z), mu = (sz + tau * kap) / (k.Degree + 1);
            double pres = Math.Max(Norm(ry), Norm(rz)) / (tau * nb), dres = Norm(rx) / (tau * nc);
            double pc = cx / tau, dc = -(by + hz) / tau, gap = sz / (tau * tau);
            double rel = Math.Min(Math.Abs(pc), Math.Abs(dc)) > TOL ? gap / Math.Min(Math.Abs(pc), Math.Abs(dc)) : double.PositiveInfinity;
            if (pres < TOL && dres < TOL && (gap < TOL || rel < TOL))
                return new SocpResult(SocpStatus.Optimal, Scal(x, 1 / tau), pc, it);
            if (pres < TOL_LOOSE && dres < TOL_LOOSE && (gap < TOL_LOOSE || rel < TOL_LOOSE)) { loose = Scal(x, 1 / tau); looseObj = pc; }
            if (by + hz < 0 && Norm(Add(Mt(p.A, y), Mt(p.G, z))) / -(by + hz) < TOL)
                return new SocpResult(SocpStatus.Infeasible, null, double.NaN, it);
            if (cx < 0 && Math.Max(Norm(Mv(p.A, x)), Norm(Add(Mv(p.G, x), s))) / -cx < TOL)
                return new SocpResult(SocpStatus.Unbounded, null, double.NaN, it);
            var w = new Nt(k, s, z);
            if (!w.Ok) return Fail(it);
            double[] lam = w.Mul(z);
            if (!kkt.Factor(w)) return Fail(it);
            var (dx1, dy1, dz1) = kkt.Solve(Neg(p.C), p.B, p.H);
            double den = kap / tau - Dot(p.C, dx1) - Dot(p.B, dy1) - Dot(p.H, dz1);
            (double[] dx, double[] dy, double[] dz, double[] ds, double dt, double dk) Dir(double sig, double[] rc, double rk) {
                double[] ul = k.Div(lam, rc), wul = w.Mul(ul);
                var (dx2, dy2, dz2) = kkt.Solve(Scal(rx, -(1 - sig)), Scal(ry, 1 - sig), Sub(Scal(rz, 1 - sig), wul));
                double d = (-(1 - sig) * rt + Dot(p.C, dx2) + Dot(p.B, dy2) + Dot(p.H, dz2) + rk / tau) / den;
                double[] ddx = Add(dx2, Scal(dx1, d)), ddy = Add(dy2, Scal(dy1, d)), ddz = Add(dz2, Scal(dz1, d));
                double[] dds = Sub(wul, w.Mul(w.Mul(ddz)));
                return (ddx, ddy, ddz, dds, d, (rk - kap * d) / tau);
            }
            double[] ll = k.Prod(lam, lam);
            var af = Dir(0, Neg(ll), -tau * kap);
            double aa = Math.Min(1, Math.Min(Math.Min(k.Step(s, af.ds), k.Step(z, af.dz)), Math.Min(Pos(tau, af.dt), Pos(kap, af.dk))));
            double sg = Math.Pow(Math.Max(0, 1 - aa), 3);
            double[] corr = k.Prod(w.InvMul(af.ds), w.Mul(af.dz));
            double[] rc = Sub(Add(Neg(ll), k.E(sg * mu)), corr);
            var d = Dir(sg, rc, -tau * kap + sg * mu - af.dt * af.dk);
            double a = Math.Min(Math.Min(k.Step(s, d.ds), k.Step(z, d.dz)), Math.Min(Pos(tau, d.dt), Pos(kap, d.dk)));
            a = Math.Min(1, 0.99 * a);
            if (!(a > 1e-8)) return Fail(it);
            x = Add(x, Scal(d.dx, a)); y = Add(y, Scal(d.dy, a)); z = Add(z, Scal(d.dz, a)); s = Add(s, Scal(d.ds, a));
            tau += a * d.dt; kap += a * d.dk;
            if (!double.IsFinite(tau) || !double.IsFinite(kap)) return Fail(it);
        }
        return loose != null ? new SocpResult(SocpStatus.Optimal, loose, looseObj, maxIter)
            : new SocpResult(SocpStatus.MaxIterations, Scal(x, 1 / tau), Dot(p.C, x) / tau, maxIter);
    }
    private static double Pos(double v, double dv) => dv < 0 ? -v / dv : double.PositiveInfinity;
    private sealed class Cones {
        public readonly int Linear, Dim, Degree;
        public readonly int[] Soc, Start;
        public Cones(int linear, int[] soc) {
            Linear = linear; Soc = soc; Start = new int[soc.Length];
            int at = linear;
            for (int i = 0; i < soc.Length; i++) { Start[i] = at; at += soc[i]; }
            Dim = at; Degree = linear + soc.Length;
        }
        public double[] E(double v) {
            var e = new double[Dim];
            for (int i = 0; i < Linear; i++) e[i] = v;
            foreach (int st in Start) e[st] = v;
            return e;
        }
        public double[] Shift(double[] u) {
            double worst = double.NegativeInfinity;
            for (int i = 0; i < Linear; i++) worst = Math.Max(worst, -u[i]);
            for (int c = 0; c < Soc.Length; c++) {
                int st = Start[c];
                double t = 0;
                for (int j = 1; j < Soc[c]; j++) t += u[st + j] * u[st + j];
                worst = Math.Max(worst, Math.Sqrt(t) - u[st]);
            }
            return worst < 0 ? (double[])u.Clone() : Add(u, E(1 + worst));
        }
        public double[] Prod(double[] u, double[] v) {
            var r = new double[Dim];
            for (int i = 0; i < Linear; i++) r[i] = u[i] * v[i];
            for (int c = 0; c < Soc.Length; c++) {
                int st = Start[c], q = Soc[c];
                double d = 0;
                for (int j = 0; j < q; j++) d += u[st + j] * v[st + j];
                r[st] = d;
                for (int j = 1; j < q; j++) r[st + j] = u[st] * v[st + j] + v[st] * u[st + j];
            }
            return r;
        }
        public double[] Div(double[] l, double[] r) {
            var u = new double[Dim];
            for (int i = 0; i < Linear; i++) u[i] = r[i] / l[i];
            for (int c = 0; c < Soc.Length; c++) {
                int st = Start[c], q = Soc[c];
                double l1r1 = 0, l1l1 = 0;
                for (int j = 1; j < q; j++) { l1r1 += l[st + j] * r[st + j]; l1l1 += l[st + j] * l[st + j]; }
                double u0 = (l[st] * r[st] - l1r1) / (l[st] * l[st] - l1l1);
                u[st] = u0;
                for (int j = 1; j < q; j++) u[st + j] = (r[st + j] - u0 * l[st + j]) / l[st];
            }
            return u;
        }
        public double Step(double[] u, double[] du) {
            double a = double.PositiveInfinity;
            for (int i = 0; i < Linear; i++) if (du[i] < 0) a = Math.Min(a, -u[i] / du[i]);
            for (int c = 0; c < Soc.Length; c++) {
                int st = Start[c], q = Soc[c];
                double qa = du[st] * du[st], qb = u[st] * du[st], qc = u[st] * u[st];
                for (int j = 1; j < q; j++) {
                    qa -= du[st + j] * du[st + j]; qb -= u[st + j] * du[st + j]; qc -= u[st + j] * u[st + j];
                }
                if (du[st] < 0) a = Math.Min(a, -u[st] / du[st]);
                if (Math.Abs(qa) < 1e-300) { if (qb < 0) a = Math.Min(a, -qc / (2 * qb)); continue; }
                double disc = qb * qb - qa * qc;
                if (disc < 0) continue;
                double sq = Math.Sqrt(disc), r1 = (-qb - sq) / qa, r2 = (-qb + sq) / qa;
                if (r1 > 0) a = Math.Min(a, r1);
                if (r2 > 0) a = Math.Min(a, r2);
            }
            return a;
        }
    }
    private sealed class Nt {
        private readonly Cones k;
        private readonly double[] lw;
        private readonly double[] eta;
        private readonly double[][] wb;
        public readonly bool Ok = true;
        public Nt(Cones k, double[] s, double[] z) {
            this.k = k;
            lw = new double[k.Linear];
            for (int i = 0; i < k.Linear; i++) {
                if (!(s[i] > 0 && z[i] > 0)) Ok = false;
                lw[i] = Math.Sqrt(s[i] / z[i]);
            }
            eta = new double[k.Soc.Length];
            wb = new double[k.Soc.Length][];
            for (int c = 0; c < k.Soc.Length; c++) {
                int st = k.Start[c], q = k.Soc[c];
                double sj = s[st] * s[st], zj = z[st] * z[st];
                for (int j = 1; j < q; j++) { sj -= s[st + j] * s[st + j]; zj -= z[st + j] * z[st + j]; }
                if (!(sj > 0 && zj > 0 && s[st] > 0 && z[st] > 0)) { Ok = false; wb[c] = new double[q]; eta[c] = 1; continue; }
                double rs = Math.Sqrt(sj), rz = Math.Sqrt(zj), dot = 0;
                for (int j = 0; j < q; j++) dot += s[st + j] / rs * (z[st + j] / rz);
                double gam = Math.Sqrt((1 + dot) / 2);
                var w = new double[q];
                w[0] = (s[st] / rs + z[st] / rz) / (2 * gam);
                for (int j = 1; j < q; j++) w[j] = (s[st + j] / rs - z[st + j] / rz) / (2 * gam);
                wb[c] = w;
                eta[c] = Math.Sqrt(rs / rz);
            }
        }
        private double[] Apply(double[] v, bool inv) {
            var r = new double[k.Dim];
            for (int i = 0; i < k.Linear; i++) r[i] = inv ? v[i] / lw[i] : v[i] * lw[i];
            for (int c = 0; c < k.Soc.Length; c++) {
                int st = k.Start[c], q = k.Soc[c];
                double[] w = wb[c];
                double sgn = inv ? -1 : 1, e = inv ? 1 / eta[c] : eta[c], qv = 0;
                for (int j = 1; j < q; j++) qv += w[j] * v[st + j];
                r[st] = e * (w[0] * v[st] + sgn * qv);
                double f = sgn * v[st] + qv / (1 + w[0]);
                for (int j = 1; j < q; j++) r[st + j] = e * (v[st + j] + f * w[j]);
            }
            return r;
        }
        public double[] Mul(double[] v) => Apply(v, false);
        public double[] InvMul(double[] v) => Apply(v, true);
    }
    private sealed class Kkt {
        private readonly SocpProblem p;
        private readonly Cones k;
        private readonly int n, pr;
        private readonly double[,] hm, sm;
        private Nt w;
        public Kkt(SocpProblem p, Cones k) {
            this.p = p; this.k = k;
            n = p.C.Length; pr = p.B.Length;
            hm = new double[n, n]; sm = new double[pr, pr];
        }
        private double[] WInvSq(double[] v) => w == null ? v : w.InvMul(w.InvMul(v));
        public bool Factor(Nt nt) {
            w = nt;
            Array.Clear(hm);
            int m = k.Dim;
            var gt = new double[n][];
            var col = new double[m];
            for (int a = 0; a < n; a++) {
                for (int i = 0; i < m; i++) col[i] = p.G[i, a];
                gt[a] = w == null ? (double[])col.Clone() : w.InvMul(col);
            }
            for (int a = 0; a < n; a++)
                for (int b = 0; b <= a; b++) {
                    double t = 0;
                    double[] ga = gt[a], gb = gt[b];
                    for (int i = 0; i < m; i++) t += ga[i] * gb[i];
                    hm[a, b] = t;
                }
            for (int a = 0; a < n; a++) hm[a, a] += 1e-12 * (1 + hm[a, a]);
            if (!Chol(hm, n)) return false;
            if (pr == 0) return true;
            Array.Clear(sm);
            var arow = new double[n];
            var hinvat = new double[n, pr];
            for (int j = 0; j < pr; j++) {
                for (int a = 0; a < n; a++) arow[a] = p.A[j, a];
                double[] v = ChSolve(hm, n, arow);
                for (int a = 0; a < n; a++) hinvat[a, j] = v[a];
            }
            for (int i = 0; i < pr; i++)
                for (int j = 0; j <= i; j++) {
                    double t = 0;
                    for (int a = 0; a < n; a++) t += p.A[i, a] * hinvat[a, j];
                    sm[i, j] = t;
                }
            for (int i = 0; i < pr; i++) sm[i, i] += 1e-13 * (1 + sm[i, i]);
            return Chol(sm, pr);
        }
        private (double[], double[], double[]) Raw(double[] r1, double[] r2, double[] r3) {
            double[] f = Add(r1, Mt(p.G, WInvSq(r3)));
            double[] dy = new double[pr];
            if (pr > 0) {
                double[] hf = ChSolve(hm, n, f);
                dy = ChSolve(sm, pr, Sub(Mv(p.A, hf), r2));
            }
            double[] dx = ChSolve(hm, n, Sub(f, Mt(p.A, dy)));
            double[] dz = WInvSq(Sub(Mv(p.G, dx), r3));
            return (dx, dy, dz);
        }
        public (double[] dx, double[] dy, double[] dz) Solve(double[] r1, double[] r2, double[] r3) {
            var (dx, dy, dz) = Raw(r1, r2, r3);
            for (int i = 0; i < 3; i++) {
                double[] e1 = Sub(r1, Add(Mt(p.A, dy), Mt(p.G, dz)));
                double[] e2 = Sub(r2, Mv(p.A, dx));
                double[] e3 = Sub(r3, Sub(Mv(p.G, dx), WSq(dz)));
                var (cx, cy, cz) = Raw(e1, e2, e3);
                dx = Add(dx, cx); dy = Add(dy, cy); dz = Add(dz, cz);
            }
            return (dx, dy, dz);
        }
        private double[] WSq(double[] v) => w == null ? v : w.Mul(w.Mul(v));
    }
    private static bool Chol(double[,] a, int n) {
        for (int j = 0; j < n; j++) {
            double d = a[j, j];
            for (int t = 0; t < j; t++) d -= a[j, t] * a[j, t];
            if (!(d > 0)) return false;
            d = Math.Sqrt(d);
            a[j, j] = d;
            for (int i = j + 1; i < n; i++) {
                double v = a[i, j];
                for (int t = 0; t < j; t++) v -= a[i, t] * a[j, t];
                a[i, j] = v / d;
            }
        }
        return true;
    }
    private static double[] ChSolve(double[,] l, int n, double[] b) {
        var y = new double[n];
        for (int i = 0; i < n; i++) {
            double v = b[i];
            for (int t = 0; t < i; t++) v -= l[i, t] * y[t];
            y[i] = v / l[i, i];
        }
        for (int i = n - 1; i >= 0; i--) {
            double v = y[i];
            for (int t = i + 1; t < n; t++) v -= l[t, i] * y[t];
            y[i] = v / l[i, i];
        }
        return y;
    }
    private static double[] Mv(double[,] a, double[] x) {
        int r = a.GetLength(0), c = a.GetLength(1);
        var y = new double[r];
        for (int i = 0; i < r; i++) {
            double t = 0;
            for (int j = 0; j < c; j++) t += a[i, j] * x[j];
            y[i] = t;
        }
        return y;
    }
    private static double[] Mt(double[,] a, double[] x) {
        int r = a.GetLength(0), c = a.GetLength(1);
        var y = new double[c];
        for (int i = 0; i < r; i++) {
            double xi = x[i];
            if (xi == 0) continue;
            for (int j = 0; j < c; j++) y[j] += a[i, j] * xi;
        }
        return y;
    }
    private static double Dot(double[] a, double[] b) {
        double t = 0;
        for (int i = 0; i < a.Length; i++) t += a[i] * b[i];
        return t;
    }
    private static double Norm(double[] a) => Math.Sqrt(Dot(a, a));
    private static double[] Add(double[] a, double[] b) {
        var r = new double[a.Length];
        for (int i = 0; i < a.Length; i++) r[i] = a[i] + b[i];
        return r;
    }
    private static double[] Sub(double[] a, double[] b) {
        var r = new double[a.Length];
        for (int i = 0; i < a.Length; i++) r[i] = a[i] - b[i];
        return r;
    }
    private static double[] Scal(double[] a, double f) {
        var r = new double[a.Length];
        for (int i = 0; i < a.Length; i++) r[i] = a[i] * f;
        return r;
    }
    private static double[] Neg(double[] a) => Scal(a, -1);
}
