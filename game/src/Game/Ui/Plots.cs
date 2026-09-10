using System;
using System.Collections.Generic;
using Godot;
using Starship.Physics;
namespace Starship.Game.Ui;
public sealed class TeleLog {
    public struct Pt {
        public float T, H, V, Q, G;
    }
    public readonly List<Pt>[] Hist = { new List<Pt>(), new List<Pt>() };
    public readonly List<double>[] Trail = { new List<double>(), new List<double>() };
    private double _next = double.NegativeInfinity, _last = double.NegativeInfinity;
    public void Push(SimState sim, Vehicle b, Vehicle s) {
        if (sim.T < _last - 0.5) Clear();
        _last = sim.T;
        if (sim.T < _next) return;
        _next = sim.T + 0.5;
        for (int i = 0; i < 2; i++) {
            Vehicle v = i == 0 ? b : s;
            Hist[i].Add(new Pt {
                T = (float)sim.T, H = (float)v.Alt, V = (float)v.Speed, Q = (float)v.Q, G = (float)v.Acc,
            });
            if (Hist[i].Count > 4000) Hist[i].RemoveAt(0);
            Trail[i].Add(v.X);
            Trail[i].Add(v.Y);
            if (Trail[i].Count > 12000) Trail[i].RemoveRange(0, 2);
        }
    }
    public void Clear() {
        for (int i = 0; i < 2; i++) { Hist[i].Clear(); Trail[i].Clear(); }
        _next = double.NegativeInfinity;
        _last = double.NegativeInfinity;
    }
}
public partial class PlotView : Control {
    public enum Kind { Telemetry, Map, Entry }
    public Kind Mode = Kind.Telemetry;
    public bool Compact;
    public static string Dist(double m) {
        double a = Math.Abs(m);
        if (a < 0.5) return "0 м";
        if (a < 1000) return $"{m:F0} м";
        if (a < 10000) return $"{m / 1000.0:F2} км";
        return $"{m / 1000.0:F0} км";
    }
    private static string Alt(double m) => "высота " + Dist(m);
    private static readonly Color Grid = new(0.09f, 0.13f, 0.18f);
    private static readonly Color Ink = new(0.43f, 0.51f, 0.60f);
    private static readonly Color Faint = new(0.31f, 0.39f, 0.48f);
    private static readonly Color Sea = new(0.055f, 0.133f, 0.200f);
    private static readonly Color Coast = new(0.114f, 0.290f, 0.388f);
    private static readonly Color BoostC = new(0.216f, 0.760f, 0.812f);
    private static readonly Color ShipC = new(1.000f, 0.616f, 0.235f);
    private static readonly Color PadC = new(0.910f, 0.827f, 0.478f);
    private static readonly Color Crash = new(0.878f, 0.357f, 0.357f);
    private static readonly Color[] Ser = {
        new(0.216f, 0.760f, 0.812f), new(1.000f, 0.616f, 0.235f),
        new(0.980f, 0.800f, 0.082f), new(0.655f, 0.545f, 0.980f),
    };
    private static readonly (string Key, string Name)[] Marks = {
        ("tower", "башня"), ("mach1", "M 1"), ("maxq", "max Q"), ("karman", "100 км"),
    };
    private SimState _sim;
    private TeleLog _log;
    private double _drawn = -99;
    public void Feed(SimState sim, TeleLog log, double now) {
        _sim = sim;
        _log = log;
        if (now - _drawn < 0.1) return;
        _drawn = now;
        QueueRedraw();
    }
    public override void _Draw() {
        if (_sim == null || _log == null) return;
        DrawRect(new Rect2(Vector2.Zero, Size), ConsoleFull.Bg, true);
        switch (Mode) {
            case Kind.Map: Map(); break;
            case Kind.Entry: Entry(); break;
            default: Tele(); break;
        }
    }
    private void Head(string s, Color c) {
        DrawString(GetThemeDefaultFont(), new Vector2(8, Compact ? 11 : 14), s,
                   HorizontalAlignment.Left, -1, Compact ? 9 : 11, c);
    }
    private void RightText(string s, float x, float y, int fs, Color c) {
        Font f = GetThemeDefaultFont();
        float wd = f.GetStringSize(s, HorizontalAlignment.Left, -1, fs).X;
        DrawString(f, new Vector2(x - wd, y), s, HorizontalAlignment.Left, -1, fs, c);
    }
    private static float Nice(float v) {
        if (v <= 0) return 1;
        float p = Mathf.Pow(10, Mathf.Floor(Mathf.Log(v) / Mathf.Log(10)));
        float m = v / p;
        float s = m <= 1.5f ? 1.5f : m <= 2 ? 2 : m <= 3 ? 3 : m <= 5 ? 5 : m <= 8 ? 8 : 10;
        return s * p;
    }
    private void Tele() {
        Font f = GetThemeDefaultFont();
        int fs = Compact ? 9 : 11;
        float w = Size.X, h = Size.Y;
        float l = Compact ? 32f : 52f, r = 10f;
        float tp = Compact ? 22f : 40f, bt = Compact ? 16f : 26f;
        Vehicle v = _sim.FocusVeh();
        int fi = _sim.Focus == "ship" ? 1 : 0;
        List<TeleLog.Pt> H = _log.Hist[fi];
        for (int i = 0; i <= 4; i++) {
            float y = tp + (h - tp - bt) * i / 4f;
            DrawLine(new Vector2(l, y), new Vector2(w - r, y), Grid, 1f);
        }
        if (Compact) RightText(v.Name, w - 10, 11, 9, Faint);
        else Head("ТЕЛЕМЕТРИЯ · " + v.Name, Ink);
        if (H.Count < 2) {
            DrawString(f, new Vector2(l, h * 0.5f), "запись начнётся со старта",
                       HorizontalAlignment.Left, -1, fs, Faint);
            return;
        }
        float t0 = H[0].T, t1 = Math.Max(H[H.Count - 1].T, t0 + 1);
        float mh = 1, mv = 1, mq = 1, mg = 1;
        foreach (TeleLog.Pt p in H) {
            if (p.H > mh) mh = p.H;
            if (p.V > mv) mv = p.V;
            if (p.Q > mq) mq = p.Q;
            if (p.G > mg) mg = p.G;
        }
        mh = Nice(mh); mv = Nice(mv); mq = Nice(mq); mg = Nice(mg);
        int step = Math.Max(1, H.Count / Math.Max(48, (int)((w - l - r) * 1.5f)));
        float[] max = { mh, mv, mq, mg };
        float Tx(float tt) => l + (w - l - r) * (tt - t0) / (t1 - t0);
        double span = t1 - t0;
        double grid = span > 3000 ? 600 : span > 1200 ? 300 : span > 500 ? 120 : span > 200 ? 60 : 30;
        for (double tt = Math.Ceiling(t0 / grid) * grid; tt <= t1; tt += grid) {
            float x = Tx((float)tt);
            DrawLine(new Vector2(x, tp), new Vector2(x, h - bt), Grid, 1f);
            if (!Compact)
                DrawString(f, new Vector2(x - 12, h - 4), $"{tt / 60:F0}м",
                           HorizontalAlignment.Left, -1, 9, Faint);
        }
        for (int s = 0; s < 4; s++) {
            var pts = new List<Vector2>(H.Count / step + 2);
            for (int j = 0; j < H.Count; j += step) {
                TeleLog.Pt p = H[j];
                float val = s == 0 ? p.H : s == 1 ? p.V : s == 2 ? p.Q : p.G;
                float x = l + (w - l - r) * (p.T - t0) / (t1 - t0);
                float y = h - bt - (h - tp - bt) * Math.Clamp(val / max[s], 0f, 1f);
                pts.Add(new Vector2(x, y));
            }
            if (pts.Count > 1) DrawPolyline(pts.ToArray(), Ser[s], Compact ? 1.2f : 1.8f, true);
        }
        string[] lab = {
            Alt(v.Alt),
            $"скорость {v.Speed:F0} м/с",
            $"напор {v.Q / 1000.0:F1} кПа",
            $"перегрузка {v.Acc:F2} g",
        };
        if (!Compact) {
            int slot = 0;
            foreach ((string key, string name) in Marks) {
                if (!_sim.Events.TryGetValue(key, out double te)) continue;
                if (te < t0 || te > t1) continue;
                float x = Tx((float)te);
                DrawLine(new Vector2(x, tp), new Vector2(x, h - bt), new Color(0.30f, 0.36f, 0.44f, 0.55f), 1f);
                DrawString(f, new Vector2(x + 3, tp + 12 + (slot % 3) * 12), name,
                           HorizontalAlignment.Left, -1, 9, Faint);
                slot++;
            }
        }
        if (Compact) {
            float x = 8;
            for (int s = 0; s < 4; s++) {
                DrawString(f, new Vector2(x, 11), lab[s], HorizontalAlignment.Left, -1, 9, Ser[s]);
                x += f.GetStringSize(lab[s], HorizontalAlignment.Left, -1, 9).X + 10;
            }
        }
        else for (int s = 0; s < 4; s++) RightText(lab[s], w - 12, 16 + s * 14, 11, Ser[s]);
        string[] axis = { $"{mh / 1000:F0} км", $"{mv:F0} м/с", $"{mq / 1000:F0} кПа", $"{mg:F0} g" };
        if (!Compact)
            for (int s = 0; s < 4; s++)
                DrawString(f, new Vector2(6, tp + 12 + s * 13), axis[s], HorizontalAlignment.Left, -1, 9, Ser[s]);
        DrawString(f, new Vector2(l, h - 4), $"T+{t0:F0} с", HorizontalAlignment.Left, -1, fs - 1, Faint);
        RightText($"T+{t1:F0} с", w - r, h - 4, fs - 1, Faint);
    }
    private void Map() {
        Font f = GetThemeDefaultFont();
        float w = Size.X, h = Size.Y, mn = Math.Min(w, h);
        Vehicle b = _sim.Veh[0], s = _sim.Veh[1];
        double aMax = 1000;
        foreach (Vehicle v in _sim.Veh) aMax = Math.Max(aMax, Math.Max(v.MaxAlt, v.Alt));
        aMax *= 1.15;
        double rng = 3e4;
        foreach (Vehicle v in _sim.Veh) rng = Math.Max(rng, Math.Abs(_sim.Downrange(v)) * 1.3);
        double sprd = Math.Clamp(2.2e6 / rng, 1, 40);
        var c = new Vector2(w * 0.5f, h * 1.34f);
        float r0 = h * 0.60f, amp = h * 0.55f;
        double pad = SimState.PadAngle(_sim.T);
        float Rp(double a) => r0 + (float)(Math.Clamp(a / aMax, 0, 1.4) * amp);
        Vector2 Pos(double x, double y) {
            double rel = -(Math.Atan2(x, y) - pad) * sprd;
            float rr = Rp(Math.Sqrt(x * x + y * y) - Const.RE);
            return c + new Vector2(rr * (float)Math.Sin(rel), -rr * (float)Math.Cos(rel));
        }
        DrawCircle(c, r0, Sea);
        DrawArc(c, r0, Mathf.Pi * 1.15f, Mathf.Pi * 1.85f, 96, Coast, 1.5f);
        foreach (int km in new[] { 100, 300 }) {
            float rr = Rp(km * 1000.0);
            if (rr > r0 + amp * 1.38f) continue;
            DrawArc(c, rr, Mathf.Pi * 1.2f, Mathf.Pi * 1.8f, 96, new Color(0.24f, 0.59f, 0.75f, 0.28f), 1f);
            DrawString(GetThemeDefaultFont(), new Vector2(8, c.Y - rr + 3), $"{km} км",
                       HorizontalAlignment.Left, -1, 9, new Color(0.24f, 0.44f, 0.55f));
        }
        for (int i = 0; i < 2; i++) {
            List<double> tr = _log.Trail[i];
            Vehicle v = _sim.Veh[i];
            Color col = i == 0 ? BoostC : ShipC;
            if (tr.Count >= 6) {
                int step = Math.Max(2, (tr.Count / 2) / 900 * 2);
                var pts = new List<Vector2>(tr.Count / step + 2);
                for (int j = 0; j + 1 < tr.Count; j += step) pts.Add(Pos(tr[j], tr[j + 1]));
                pts.Add(Pos(v.X, v.Y));
                DrawPolyline(pts.ToArray(), col, 1.7f, true);
            }
            Vector2 p = Pos(v.X, v.Y);
            DrawCircle(p, v == _sim.FocusVeh() ? 4.5f : 3f, v.Crashed ? Crash : col);
        }
        Vec2 pp = SimState.PadPos(_sim.T);
        DrawCircle(Pos(pp.X, pp.Y), 3.5f, PadC);
        Head($"РАЗВЁРТКА · дальность {_sim.Downrange(_sim.FocusVeh()) / 1000.0:F0} км · масштаб высоты условный", Ink);
        if (!Compact) {
            DrawString(f, new Vector2(8, 32), "■ ускоритель", HorizontalAlignment.Left, -1, 10, BoostC);
            DrawString(f, new Vector2(8, 46), "■ корабль", HorizontalAlignment.Left, -1, 10, ShipC);
            DrawString(f, new Vector2(8, 60), "● стартовый стол", HorizontalAlignment.Left, -1, 10, PadC);
            RightText($"апогей {Math.Max(b.MaxAlt, s.MaxAlt) / 1000.0:F0} км", w - 12, 16, 10, Faint);
        }
    }
    private void Entry() {
        Font f = GetThemeDefaultFont();
        float w = Size.X, h = Size.Y;
        float l = Compact ? 34f : 52f, r = 14f, tp = Compact ? 26f : 34f, bt = Compact ? 16f : 30f;
        const double VM = 8200, HM = 160e3;
        float X(double v) => l + (w - l - r) * (float)Math.Clamp(v / VM, 0, 1);
        float Y(double a) => h - bt - (h - tp - bt) * (float)Math.Clamp(a / HM, 0, 1);
        for (double v = 0; v <= VM; v += 1000)
            DrawLine(new Vector2(X(v), tp), new Vector2(X(v), h - bt), Grid, 1f);
        for (double a = 0; a <= HM; a += 20e3)
            DrawLine(new Vector2(l, Y(a)), new Vector2(w - r, Y(a)), Grid, 1f);
        if (!Compact) {
            for (double v = 0; v <= VM; v += 2000)
                DrawString(f, new Vector2(X(v) - 14, h - 10), $"{v / 1000:F0} км/с",
                           HorizontalAlignment.Left, -1, 9, Faint);
            for (double a = 0; a <= HM; a += 40e3)
                DrawString(f, new Vector2(6, Y(a) + 3), $"{a / 1000:F0} км",
                           HorizontalAlignment.Left, -1, 9, Faint);
        }
        var lines = new (double Lv, Color C)[] {
            (200, new Color(0.17f, 0.42f, 0.27f)),
            (600, new Color(0.54f, 0.35f, 0.07f)),
            (1200, new Color(0.49f, 0.16f, 0.18f)),
        };
        int k = 0;
        foreach ((double lv, Color col) in lines) {
            var pts = new List<Vector2>();
            for (double a = 0; a <= HM; a += 2000) {
                double rho = Atmosphere.At(a, _sim.RhoK).Rho;
                if (rho <= 0) continue;
                double sp = Math.Cbrt(lv * 1000.0 / (1.83e-4 * Math.Sqrt(rho / 4.5)));
                if (sp > VM || double.IsNaN(sp) || double.IsInfinity(sp)) continue;
                pts.Add(new Vector2(X(sp), Y(a)));
            }
            if (pts.Count > 1) DrawPolyline(pts.ToArray(), col, 1.4f, true);
            if (!Compact) RightText($"{lv:F0} кВт/м²", w - r - 8, Y(HM * 0.88) + k * 14, 9, col);
            k++;
        }
        Vehicle v2 = _sim.FocusVeh();
        int fi = _sim.Focus == "ship" ? 1 : 0;
        List<TeleLog.Pt> H = _log.Hist[fi];
        if (H.Count > 1) {
            int step = Math.Max(1, H.Count / Math.Max(48, (int)((w - l - r) * 1.5f)));
            var pts = new List<Vector2>(H.Count / step + 2);
            for (int j = 0; j < H.Count; j += step) pts.Add(new Vector2(X(H[j].V), Y(H[j].H)));
            DrawPolyline(pts.ToArray(), BoostC, 1.8f, true);
        }
        var cur = new Vector2(X(v2.Speed), Y(v2.Alt));
        DrawCircle(cur, 4f, Colors.White);
        DrawArc(cur, 9f, 0, Mathf.Tau, 32, Colors.White, 1f);
        Head("ВХОД В АТМОСФЕРУ · высота от скорости · " + v2.Name, Ink);
        if (!Compact)
            DrawString(f, new Vector2(l, tp - 8),
                       $"поток {v2.Heat:F0} кВт/м² · плитки {v2.TTile:F0} К · напор {v2.Q / 1000.0:F2} кПа",
                       HorizontalAlignment.Left, -1, 10, new Color(0.66f, 0.86f, 0.89f));
    }
}
