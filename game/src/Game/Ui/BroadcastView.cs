using System;
using Godot;
using Starship.Physics;
namespace Starship.Game.Ui;
public partial class BroadcastView : Control {
    private const uint Shade = 0x02050AFF;
    private SimState _sim;
    private Mission _mission;
    private Arc _arc = new("orbital");
    private string _cam = "";
    private double _camAt = -99, _speed = 1;
    private bool _paused;
    public static BroadcastView Build(Screens scr) {
        var v = new BroadcastView { MouseFilter = MouseFilterEnum.Ignore };
        scr.HudRoot.AddChild(v);
        v.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return v;
    }
    public void Reset(string mission) {
        _arc = new Arc(mission);
        _camAt = -99;
    }
    public void Update(SimState sim, Mission mission, string cam, double speed, bool paused) {
        _sim = sim;
        _mission = mission;
        _speed = speed;
        _paused = paused;
        _arc.Track(sim);
        if (cam != _cam) {
            _cam = cam;
            _camAt = Time.GetTicksMsec() / 1000.0;
        }
        if (Visible) QueueRedraw();
    }
    public override void _Draw() {
        if (_sim == null) return;
        float k = Size.Y / 1080f, w = Size.X, h = Size.Y;
        Gradient(w, h, k);
        Vehicle b = _sim.Veh[0], s = _sim.Veh[1];
        float bottom = h - 46 * k;
        Group(b, "SUPER HEAVY", 56 * k, bottom, 96 * k, k, false);
        Group(s, "STARSHIP", w - 56 * k, bottom, 68 * k, k, true);
        Mid(w, h, k);
        if (Time.GetTicksMsec() / 1000.0 - _camAt < 3)
            Text(_cam, 56 * k, 40 * k + 18 * k, 18 * k, 400, 0xF2F5F8EB);
        string mark = _paused ? "пауза" : _speed != 1 ? "×" + _speed.ToString("0.###").Replace('.', ',') : null;
        if (mark != null) Text(mark, w - 56 * k, 40 * k + 18 * k, 18 * k, 500, Themes.Warn, true);
    }
    private void Gradient(float w, float h, float k) {
        var dark = new Color(new Color(Shade), 0.78f);
        var mid = new Color(new Color(Shade), 0.55f);
        var clear = new Color(new Color(Shade), 0f);
        Quad(0, h - 150 * k, w, h, mid, dark);
        Quad(0, h - 380 * k, w, h - 150 * k, clear, mid);
    }
    private void Quad(float x0, float y0, float x1, float y1, Color top, Color bottom) {
        DrawPolygon(new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) },
                    new[] { top, top, bottom, bottom });
    }
    private float Text(string s, float x, float baseline, float size, int weight, uint color, bool right = false,
                       float extra = 0) {
        Font f = Look.Font(weight);
        int fs = Math.Max(1, (int)Math.Round(size));
        float wd = extra == 0 ? f.GetStringSize(s, HorizontalAlignment.Left, -1, fs).X : Wide(f, s, fs, extra);
        float at = right ? x - wd : x;
        var shadow = new Color(0, 0, 0, 0.55f);
        if (extra == 0) {
            DrawString(f, new Vector2(at + 1, baseline + 1), s, HorizontalAlignment.Left, -1, fs, shadow);
            DrawString(f, new Vector2(at, baseline), s, HorizontalAlignment.Left, -1, fs, new Color(color));
            return wd;
        }
        float cx = at;
        foreach (char c in s) {
            string g = c.ToString();
            DrawString(f, new Vector2(cx + 1, baseline + 1), g, HorizontalAlignment.Left, -1, fs, shadow);
            DrawString(f, new Vector2(cx, baseline), g, HorizontalAlignment.Left, -1, fs, new Color(color));
            cx += f.GetStringSize(g, HorizontalAlignment.Left, -1, fs).X + extra;
        }
        return wd;
    }
    private static float Wide(Font f, string s, int fs, float extra) {
        float wd = 0;
        foreach (char c in s) wd += f.GetStringSize(c.ToString(), HorizontalAlignment.Left, -1, fs).X + extra;
        return wd;
    }
    private void Group(Vehicle v, string name, float edge, float bottom, float ring, float k, bool right) {
        float dir = right ? -1 : 1;
        Widgets.EngineRing(this, v, new Vector2(edge + dir * ring * 0.5f, bottom - 62 * k), ring,
                           OnAir.Ink, OnAir.Ink3);
        float blockRight = right ? edge - ring - 24 * k : edge + ring + 24 * k + 272 * k;
        float left = blockRight - 272 * k;
        Text(name, right ? blockRight : left, bottom - 112 * k, 15 * k, 600, OnAir.Ink2, right, 2.4f * k);
        Row(left, bottom - 74 * k, k, "СКОРОСТЬ", NumFmt.F(v.Speed * 3.6, 0), "км/ч");
        Row(left, bottom - 36 * k, k, "ВЫСОТА",
            v.Alt >= 1000 ? NumFmt.F(v.Alt / 1000, 0) : NumFmt.F(v.Alt, 0), v.Alt >= 1000 ? "км" : "м");
        float fill = (float)Math.Clamp(v.Fill, 0, 1);
        Tank(left, bottom - 16 * k, k, "LOX", fill);
        Tank(left, bottom - 4 * k, k, "CH4", fill);
        Icon(v, new Vector2(right ? left - 88 * k : blockRight + 88 * k, bottom - 64 * k), 128 * k, k);
    }
    private void Row(float left, float baseline, float k, string key, string val, string unit) {
        Text(key, left, baseline, 14 * k, 400, OnAir.Ink3, false, 1.4f * k);
        Text(val, left + 220 * k, baseline, 34 * k, 600, OnAir.Ink, true);
        Text(unit, left + 228 * k, baseline, 17 * k, 400, OnAir.Ink2);
    }
    private void Tank(float left, float y, float k, string name, float fill) {
        Text(name, left, y + 4 * k, 13 * k, 400, OnAir.Ink3, false, 1.1f * k);
        float x0 = left + 44 * k, wd = 180 * k;
        DrawRect(new Rect2(x0, y - 2 * k, wd, 5 * k), new Color(new Color(OnAir.Ink), 0.22f));
        DrawRect(new Rect2(x0, y - 2 * k, wd * fill, 5 * k), new Color(OnAir.Ink));
    }
    private void Icon(Vehicle v, Vector2 at, float box, float k) {
        bool ship = v.Kind == Kind.Ship;
        Part[] parts = ship ? RocketArt.Ship() : RocketArt.Booster();
        double len = ship ? RocketArt.ShipLen : 71.1;
        float scale = box / (float)len;
        DrawSetTransform(at, (float)v.Th, new Vector2(scale * 1.07f, scale * 1.07f));
        foreach (Part p in parts) Silhouette(p, len, new Color(0.016f, 0.031f, 0.055f, 0.75f));
        DrawSetTransform(at, (float)v.Th, new Vector2(scale, scale));
        foreach (Part p in parts) Silhouette(p, len, null);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
    private void Silhouette(Part p, double len, Color? flat) {
        if ((p.Fill & 0xFF) == 0) return;
        var pts = new Vector2[p.Pts.Length];
        for (int i = 0; i < pts.Length; i++)
            pts[i] = new Vector2((float)p.Pts[i].X, (float)(p.Pts[i].Y - len * 0.5));
        DrawColoredPolygon(pts, flat ?? new Color(p.Fill));
    }
    private void Mid(float w, float h, float k) {
        float top = h - 249 * k, cx = w * 0.5f, cy = top + 820 * k, r = 780 * k;
        double frac = _arc.Frac(_sim.T);
        float nowA = (float)(-24 + 48 * frac);
        Band(cx, cy, r, nowA, 24, new Color(new Color(OnAir.Ink), 0.38f), 2 * k);
        Band(cx, cy, r, -24, nowA, new Color(OnAir.Ink), 3 * k);
        int next = -1;
        for (int i = 0; i < _arc.Passed.Length; i++)
            if (!_arc.Passed[i]) { next = i; break; }
        for (int i = 0; i < _arc.Names.Length; i++) {
            float a = -24 + 48f * i / (_arc.Names.Length - 1);
            Vector2 p = At(cx, cy, r, a);
            if (_arc.Passed[i]) DrawCircle(p, 6.5f * k, new Color(OnAir.Ink), true, -1, true);
            else {
                DrawCircle(p, 6 * k, new Color(0.016f, 0.031f, 0.055f, 0.55f), true, -1, true);
                DrawCircle(p, 6 * k, new Color(i == next ? OnAir.Ink : OnAir.Ink3), false, 2 * k, true);
            }
            Font f = Look.Font(400);
            int fs = Math.Max(1, (int)Math.Round(15 * k));
            float wd = f.GetStringSize(_arc.Names[i], HorizontalAlignment.Left, -1, fs).X;
            Text(_arc.Names[i], p.X - wd * 0.5f, p.Y - 18 * k, 15 * k, 400,
                 _arc.Passed[i] ? OnAir.Ink2 : OnAir.Ink3);
        }
        Vector2 np = At(cx, cy, r, nowA);
        DrawCircle(np, 11 * k, new Color(new Color(OnAir.Ink), 0.5f), false, 1.5f * k, true);
        DrawCircle(np, 4.5f * k, new Color(OnAir.Ink), true, -1, true);
        float clock = top + 118 * k + 6 * k + 40 * k;
        Font big = Look.Font(600);
        int cs = Math.Max(1, (int)Math.Round(46 * k));
        string time = NumFmt.Hms(_sim.T);
        float cw = big.GetStringSize(time, HorizontalAlignment.Left, -1, cs).X;
        Text(time, cx - cw * 0.5f, clock, 46 * k, 600, OnAir.Ink);
        string title = _mission?.Title ?? "";
        Font small = Look.Font(400);
        int ts = Math.Max(1, (int)Math.Round(17 * k));
        float tw = small.GetStringSize(title, HorizontalAlignment.Left, -1, ts).X;
        Text(title, cx - tw * 0.5f, clock + 30 * k, 17 * k, 400, OnAir.Ink2);
    }
    private static Vector2 At(float cx, float cy, float r, float deg) {
        double a = deg * Math.PI / 180;
        return new Vector2(cx + r * (float)Math.Sin(a), cy - r * (float)Math.Cos(a));
    }
    private void Band(float cx, float cy, float r, float a0, float a1, Color c, float width) {
        if (a1 <= a0) return;
        int n = Math.Max(2, (int)((a1 - a0) * 2));
        var pts = new Vector2[n + 1];
        for (int i = 0; i <= n; i++) pts[i] = At(cx, cy, r, a0 + (a1 - a0) * i / n);
        DrawPolyline(pts, c, width, true);
    }
}
