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
    public string[] Legend() {
        Vehicle v = _sim.FocusVeh();
        string alt = NumFmt.Dist(v.Alt, out string u);
        return new[] {
            $"высота {alt} {u}", $"скорость {NumFmt.F(v.Speed, 0)} м/с",
            $"напор {NumFmt.F(v.Q / 1000, 1)} кПа", $"перегрузка {NumFmt.F(v.Acc, 2)} g",
        };
    }
    public override void _Draw() {
        if (_sim == null || _log == null) return;
        switch (Mode) {
            case Kind.Map: Map(); break;
            case Kind.Entry: Entry(); break;
            default: Tele(); break;
        }
    }
    private void Text(string s, float x, float y, int fs, Color c) =>
        DrawString(GetThemeDefaultFont(), new Vector2(x, y), s, HorizontalAlignment.Left, -1, fs, c);
    private void RightText(string s, float x, float y, int fs, Color c) {
        Font f = GetThemeDefaultFont();
        float wd = f.GetStringSize(s, HorizontalAlignment.Left, -1, fs).X;
        DrawString(f, new Vector2(x - wd, y), s, HorizontalAlignment.Left, -1, fs, c);
    }
    private static string Sec(double t) => "T" + (t < 0 ? NumFmt.Minus : '+') + NumFmt.F(Math.Abs(t), 0) + " с";
    private static float Nice(float v) {
        if (v <= 0) return 1;
        float p = Mathf.Pow(10, Mathf.Floor(Mathf.Log(v) / Mathf.Log(10)));
        float m = v / p;
        float s = m <= 1.5f ? 1.5f : m <= 2 ? 2 : m <= 3 ? 3 : m <= 5 ? 5 : m <= 8 ? 8 : 10;
        return s * p;
    }
    private void Dashed(List<Vector2> pts, Color c, float w, float dash, float gap) {
        float left = dash;
        bool on = true;
        for (int i = 1; i < pts.Count; i++) {
            Vector2 a = pts[i - 1], b = pts[i];
            float len = a.DistanceTo(b), pos = 0;
            while (len > 1e-3f && pos < len) {
                float step = Mathf.Min(left, len - pos);
                if (on) DrawLine(a.Lerp(b, pos / len), a.Lerp(b, (pos + step) / len), c, w, true);
                pos += step;
                left -= step;
                if (left <= 1e-3f) { on = !on; left = on ? dash : gap; }
            }
        }
    }
    private void Fill(List<Vector2> pts, Color c, float floor) {
        Color top = new(c, 0.20f), bottom = new(c, 0f);
        for (int i = 1; i < pts.Count; i++) {
            if (pts[i].X - pts[i - 1].X < 0.01f) continue;
            float y0 = Mathf.Min(pts[i - 1].Y, floor - 0.5f), y1 = Mathf.Min(pts[i].Y, floor - 0.5f);
            DrawPolygon(new[] {
                new Vector2(pts[i - 1].X, y0), new Vector2(pts[i].X, y1),
                new Vector2(pts[i].X, floor), new Vector2(pts[i - 1].X, floor),
            }, new[] { top, top, bottom, bottom });
        }
    }
    private void Tele() {
        float w = Size.X, h = Size.Y, tp = 8, bt = 24;
        for (int i = 1; i <= 3; i++) {
            float y = tp + (h - tp - bt) * i / 4f;
            DrawLine(new Vector2(0, y), new Vector2(w, y), Look.Line, 1f);
        }
        List<TeleLog.Pt> H = _log.Hist[_sim.Focus == "ship" ? 1 : 0];
        if (H.Count < 2) {
            Text("запись начнётся со старта", 0, h * 0.5f, 13, Look.Lab);
            return;
        }
        float t0 = H[0].T, t1 = Math.Max(H[^1].T, t0 + 1);
        float mh = 1, mv = 1, mq = 1, mg = 1;
        foreach (TeleLog.Pt p in H) {
            if (p.H > mh) mh = p.H;
            if (p.V > mv) mv = p.V;
            if (p.Q > mq) mq = p.Q;
            if (p.G > mg) mg = p.G;
        }
        float[] max = { Nice(mh), Nice(mv), Nice(mq), Nice(mg) };
        float Tx(double tt) => (float)(w * (tt - t0) / (t1 - t0));
        float Vy(float val, int s) => h - bt - (h - tp - bt) * Math.Clamp(val / max[s], 0f, 1f);
        int slot = 0;
        foreach ((string key, string name) in Marks) {
            if (!_sim.Events.TryGetValue(key, out double te) || te < t0 || te > t1) continue;
            float x = Tx(te);
            DrawLine(new Vector2(x, tp), new Vector2(x, h - bt), new Color(Look.Lab, 0.35f), 1f);
            Text(name, x + 4, tp + 12 + slot % 3 * 14, 11, Look.Lab);
            slot++;
        }
        Color[] ser = Look.Series;
        int step = Math.Max(1, H.Count / Math.Max(48, (int)(w * 1.5f)));
        for (int s = 0; s < 4; s++) {
            var pts = new List<Vector2>(H.Count / step + 2);
            for (int j = 0; j < H.Count; j += step) {
                TeleLog.Pt p = H[j];
                pts.Add(new Vector2(Tx(p.T), Vy(s == 0 ? p.H : s == 1 ? p.V : s == 2 ? p.Q : p.G, s)));
            }
            if (pts.Count < 2) continue;
            if (s == 0) Fill(pts, ser[0], h - bt);
            if (s == 2) Dashed(pts, ser[s], 2f, 6, 4);
            else if (s == 3) Dashed(pts, ser[s], 3f, 2, 4);
            else DrawPolyline(pts.ToArray(), ser[s], 2f, true);
        }
        Text(Sec(t0), 0, h - 6, 12, Look.Lab);
        RightText(Sec(t1), w, h - 6, 12, Look.Lab);
    }
    private void Map() {
        float w = Size.X, h = Size.Y;
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
        DrawCircle(c, r0, Look.Field);
        DrawArc(c, r0, Mathf.Pi * 1.15f, Mathf.Pi * 1.85f, 96, Look.Line, 1.5f, true);
        foreach (int km in new[] { 100, 300 }) {
            float rr = Rp(km * 1000.0);
            if (rr > r0 + amp * 1.38f) continue;
            DrawArc(c, rr, Mathf.Pi * 1.2f, Mathf.Pi * 1.8f, 96, new Color(Look.Lab, 0.35f), 1f, true);
            Text($"{km} км", 0, c.Y - rr + 4, 11, Look.Lab);
        }
        Color[] ser = Look.Series;
        for (int i = 0; i < 2; i++) {
            List<double> tr = _log.Trail[i];
            Vehicle v = _sim.Veh[i];
            if (tr.Count >= 6) {
                int step = Math.Max(2, tr.Count / 2 / 900 * 2);
                var pts = new List<Vector2>(tr.Count / step + 2);
                for (int j = 0; j + 1 < tr.Count; j += step) pts.Add(Pos(tr[j], tr[j + 1]));
                pts.Add(Pos(v.X, v.Y));
                DrawPolyline(pts.ToArray(), ser[i], 2f, true);
            }
            DrawCircle(Pos(v.X, v.Y), v == _sim.FocusVeh() ? 4.5f : 3f, v.Crashed ? Look.Crit : ser[i]);
        }
        Vec2 pp = SimState.PadPos(_sim.T);
        DrawCircle(Pos(pp.X, pp.Y), 3.5f, Look.Warn);
        Text($"Дальность {NumFmt.F(_sim.Downrange(_sim.FocusVeh()) / 1000.0, 0)} км, высота в условном масштабе",
             0, 14, 13, Look.Lab);
        Text("● ускоритель", 0, 34, 12, ser[0]);
        Text("● корабль", 0, 50, 12, ser[1]);
        Text("● стартовый стол", 0, 66, 12, Look.Warn);
        RightText($"апогей {NumFmt.F(Math.Max(b.MaxAlt, s.MaxAlt) / 1000.0, 0)} км", w, 14, 12, Look.Lab);
    }
    private void Entry() {
        float w = Size.X, h = Size.Y, l = 46f, r = 8f, tp = 38f, bt = 24f;
        const double VM = 8200, HM = 160e3;
        float X(double v) => l + (w - l - r) * (float)Math.Clamp(v / VM, 0, 1);
        float Y(double a) => h - bt - (h - tp - bt) * (float)Math.Clamp(a / HM, 0, 1);
        for (double v = 0; v <= VM; v += 1000)
            DrawLine(new Vector2(X(v), tp), new Vector2(X(v), h - bt), Look.Line, 1f);
        for (double a = 0; a <= HM; a += 20e3)
            DrawLine(new Vector2(l, Y(a)), new Vector2(w - r, Y(a)), Look.Line, 1f);
        for (double v = 0; v <= VM; v += 2000) Text($"{v / 1000:F0} км/с", X(v) - 14, h - 6, 11, Look.Lab);
        for (double a = 0; a <= HM; a += 40e3) Text($"{a / 1000:F0} км", 0, Y(a) + 4, 11, Look.Lab);
        var lines = new (double Lv, Color C)[] { (200, Look.Lab), (600, Look.Warn), (1200, Look.Crit) };
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
            if (pts.Count > 1) DrawPolyline(pts.ToArray(), col, 1.5f, true);
            RightText($"{lv:F0} кВт/м²", w - r - 6, Y(HM * 0.88) + k * 15, 11, col);
            k++;
        }
        Vehicle v2 = _sim.FocusVeh();
        List<TeleLog.Pt> H = _log.Hist[_sim.Focus == "ship" ? 1 : 0];
        if (H.Count > 1) {
            int step = Math.Max(1, H.Count / Math.Max(48, (int)((w - l - r) * 1.5f)));
            var pts = new List<Vector2>(H.Count / step + 2);
            for (int j = 0; j < H.Count; j += step) pts.Add(new Vector2(X(H[j].V), Y(H[j].H)));
            DrawPolyline(pts.ToArray(), Look.Series[1], 2f, true);
        }
        var cur = new Vector2(X(v2.Speed), Y(v2.Alt));
        DrawCircle(cur, 4f, Look.Ink);
        DrawArc(cur, 9f, 0, Mathf.Tau, 32, Look.Ink, 1f, true);
        Text($"Высота от скорости, {v2.Name}", 0, 14, 13, Look.Lab);
        Text($"поток {NumFmt.F(v2.Heat, 0)} кВт/м², плитки {NumFmt.F(v2.TTile, 0)} K, напор {NumFmt.F(v2.Q / 1000.0, 2)} кПа",
             0, 30, 12, Look.Ink2);
    }
}
