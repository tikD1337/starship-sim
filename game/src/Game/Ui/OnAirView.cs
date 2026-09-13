using System;
using Godot;
using Starship.Physics;
using PEngine = Starship.Physics.Engine;
namespace Starship.Game.Ui;
public partial class OnAirView : Control {
    private const double BoostLen = 71.1;
    private SimState _sim;
    public static OnAirView Build(Screens scr) {
        var v = new OnAirView { MouseFilter = MouseFilterEnum.Ignore };
        scr.AirRoot.AddChild(v);
        v.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return v;
    }
    public void Update(SimState sim) {
        _sim = sim;
        if (Visible) QueueRedraw();
    }
    public override void _Draw() {
        if (_sim == null) return;
        float k = Size.Y / 1080f, w = Size.X, h = Size.Y;
        DrawRect(new Rect2(0, 0, w, h), new Color(OnAir.Sky));
        Vehicle b = _sim.Veh[0], s = _sim.Veh[1], f = _sim.FocusVeh();
        float strip = 60 * k, yd = h - strip, yb = yd - 232 * k;
        Rocket(f, s.Attached, new Vector2(w * 0.5f, yb * 0.5f), 600 * k);
        DrawRect(new Rect2(0, yb, w, 232 * k), new Color(OnAir.Band));
        Clock(k);
        float mid = yd - 92 * k;
        Stage(b, "Ускоритель", 52 * k, mid, 108 * k, k, false);
        Stage(s, "Корабль", w - 52 * k, mid, 74 * k, k, true);
        Marks(k, w, yd);
        DrawRect(new Rect2(0, yd, w, strip), new Color(OnAir.Strip));
        Strip(f, k, yd + strip * 0.5f);
    }
    private float Text(string s, float x, float baseline, float size, int weight, uint color, bool right = false) {
        Font f = Look.Font(weight);
        int fs = Math.Max(1, (int)Math.Round(size));
        float wd = f.GetStringSize(s, HorizontalAlignment.Left, -1, fs).X;
        DrawString(f, new Vector2(right ? x - wd : x, baseline), s, HorizontalAlignment.Left, -1, fs,
                   new Color(color));
        return wd;
    }
    private void Clock(float k) {
        string t = NumFmt.Clock(_sim.T);
        Text(t[..^2], 52 * k, 44 * k + 38 * k, 46 * k, 500, OnAir.Ink);
        Text(Phases.Air(_sim.FocusVeh().Mode), 52 * k, 44 * k + 38 * k + 36 * k, 24 * k, 400, OnAir.Ink2);
    }
    private void Rocket(Vehicle f, bool stacked, Vector2 at, float box) {
        Part[] parts = stacked ? RocketArt.Stack()
                     : f.Kind == Kind.Booster ? RocketArt.Booster() : RocketArt.Ship();
        double len = stacked ? RocketArt.ShipLen + BoostLen
                   : f.Kind == Kind.Booster ? BoostLen : RocketArt.ShipLen;
        float scale = box / (float)len;
        DrawSetTransform(at, (float)f.Th, new Vector2(scale, scale));
        double fmax = Math.Max(1, f.MaxEng * Spec.RaptorSL.Fv);
        double flame = len * 0.34 * Math.Clamp(f.F / fmax, 0, 1.2);
        double nozzle = stacked ? RocketArt.ShipLen + 69.4
                      : f.Kind == Kind.Booster ? 69.4 : RocketArt.ShipLen;
        if (flame > 0.5)
            for (int layer = 0; layer < 3; layer++) Flame(flame, layer, nozzle, len);
        foreach (Part p in parts) Shape(p, len);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
    private void Shape(Part p, double len) {
        var pts = new Vector2[p.Pts.Length];
        for (int i = 0; i < pts.Length; i++)
            pts[i] = new Vector2((float)p.Pts[i].X, (float)(p.Pts[i].Y - len * 0.5));
        if ((p.Fill & 0xFF) != 0) DrawColoredPolygon(pts, new Color(p.Fill));
        if ((p.Line & 0xFF) == 0) return;
        Vector2[] loop = pts;
        if (pts.Length > 2) {
            loop = new Vector2[pts.Length + 1];
            Array.Copy(pts, loop, pts.Length);
            loop[^1] = pts[0];
        }
        DrawPolyline(loop, new Color(p.Line), (float)p.Width, true);
    }
    private void Flame(double len, int layer, double nozzle, double total) {
        Pt[] src = RocketArt.Flame(len, layer);
        var pts = new Vector2[src.Length];
        var cols = new Color[src.Length];
        var top = new Color(RocketArt.FlameColor(layer));
        for (int i = 0; i < src.Length; i++) {
            pts[i] = new Vector2((float)src[i].X, (float)(nozzle + src[i].Y - total * 0.5));
            float t = (float)Math.Clamp(src[i].Y / len, 0, 1);
            cols[i] = new Color(top, top.A * (1 - t));
        }
        DrawPolygon(pts, cols);
    }
    private void Stage(Vehicle v, string who, float edge, float mid, float d, float k, bool right) {
        float cx = right ? edge - d * 0.5f : edge + d * 0.5f;
        Ring(v, new Vector2(cx, mid), d);
        float x = right ? edge - d - 22 * k : edge + d + 22 * k;
        Text(who, x, mid - 30 * k, 21 * k, 400, OnAir.Ink2, right);
        string speed = NumFmt.F(v.Speed * 3.6, 0);
        float bl = mid + 22 * k;
        if (right) {
            float uw = Text(" км/ч", x, bl, 25 * k, 400, OnAir.Ink, true);
            Text(speed, x - uw, bl, 56 * k, 500, OnAir.Ink, true);
        }
        else {
            float sw = Text(speed, x, bl, 56 * k, 500, OnAir.Ink, false);
            Text(" км/ч", x + sw, bl, 25 * k, 400, OnAir.Ink, false);
        }
        string alt = v.Alt >= 1000 ? "высота " + NumFmt.F(v.Alt / 1000, 0) + " км"
                                   : "высота " + NumFmt.F(v.Alt, 0) + " м";
        Text(alt, x, mid + 52 * k, 21 * k, 400, OnAir.Ink2, right);
    }
    private void Ring(Vehicle v, Vector2 c, float d) => Widgets.EngineRing(this, v, c, d, OnAir.Ink, OnAir.Rail);
    private void Marks(float k, float w, float yd) {
        Rail.Mark[] marks = Rail.Of(_sim);
        float x0 = w * 0.31f, x1 = w * 0.69f, y = yd - 76 * k;
        DrawLine(new Vector2(x0, y), new Vector2(x1, y), new Color(OnAir.Rail), 2 * k, true);
        for (int i = 0; i < marks.Length; i++) {
            float x = x0 + (x1 - x0) * i / (marks.Length - 1);
            if (marks[i].Past) DrawCircle(new Vector2(x, y), 9 * k, new Color(OnAir.Ink), true, -1, true);
            else {
                DrawCircle(new Vector2(x, y), 9 * k, new Color(OnAir.Band), true, -1, true);
                DrawCircle(new Vector2(x, y), 8 * k, new Color(marks[i].Next ? OnAir.Ink : OnAir.Rail), false,
                           2 * k, true);
            }
            Font f = Look.Font(400);
            int fs = Math.Max(1, (int)Math.Round(20 * k));
            float wd = f.GetStringSize(marks[i].Name, HorizontalAlignment.Left, -1, fs).X;
            DrawString(f, new Vector2(x - wd * 0.5f, y + 45 * k), marks[i].Name, HorizontalAlignment.Left, -1, fs,
                       new Color(OnAir.Ink3));
        }
    }
    private void Strip(Vehicle v, float k, float mid) {
        if (v.Eng.Count == 0) return;
        PEngine e = v.Eng[EngineState.Worst(v)];
        ParamRow tb = ParamDefs.Row("_tb"), vb = ParamDefs.Row("_vb");
        double tbx = tb.FromEngine(e), vbx = vb.FromEngine(e);
        float baseline = mid + 8 * k, x = 52 * k;
        x += Text("У предела:", x, baseline, 22 * k, 400, OnAir.StripDim) + 60 * k;
        x = Pair(x, baseline, k, "Обороты вала", NumFmt.F(e.Pf.Rpm, 0), false);
        x = Pair(x, baseline, k, "Подшипники", NumFmt.F(tbx, 0) + " K", ParamDefs.ZoneOf(tb, tbx).Length > 0);
        Pair(x, baseline, k, "Вибрация", NumFmt.F(vbx, 2) + " g", ParamDefs.ZoneOf(vb, vbx).Length > 0);
    }
    private float Pair(float x, float baseline, float k, string what, string value, bool warn) {
        x += Text(what + " ", x, baseline, 22 * k, 400, OnAir.StripInk);
        x += Text(value, x, baseline, 22 * k, 500, warn ? Themes.Warn : OnAir.StripInk);
        return x + 60 * k;
    }
}
