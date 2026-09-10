using System;
using System.Collections.Generic;
using Godot;
using Starship.Game;
using Starship.Physics;
namespace Starship.Game.Ui;
public sealed class FlightTape {
    private TapeBar _bar;
    private PanelContainer _sum;
    private VBoxContainer _sumBody;
    private double _span = 600;
    public static FlightTape Build(ConsoleFull ui) {
        var t = new FlightTape();
        t._bar = new TapeBar();
        ui.AddBottomStrip(t._bar, 46);
        t._sum = ui.AddOverlay();
        t._sumBody = new VBoxContainer();
        t._sumBody.AddThemeConstantOverride("separation", 2);
        t._sumBody.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        t._sum.AddChild(t._sumBody);
        return t;
    }
    public bool Visible {
        get => _bar.Visible;
        set => _bar.Visible = value;
    }
    public void Update(SimState sim) {
        if (!_bar.Visible) return;
        _span = Math.Max(_span, sim.T + 30);
        _bar.Feed(sim.Marks, sim.T, _span);
    }
    public void Widen(double span) => _span = Math.Max(span, 600);
    public static double? Step(SimState sim, int dir) {
        double best = double.NaN;
        foreach (LogEntry m in sim.Marks) {
            if (dir > 0 && m.T > sim.T + 0.05 && (double.IsNaN(best) || m.T < best)) best = m.T;
            if (dir < 0 && m.T < sim.T - 0.05 && (double.IsNaN(best) || m.T > best)) best = m.T;
        }
        return double.IsNaN(best) ? null : best;
    }
    public bool SummaryShown => _sum.Visible;
    public void ToggleSummary(SimState sim, Mission mis) {
        if (_sum.Visible) { _sum.Visible = false; return; }
        foreach (Node n in _sumBody.GetChildren()) n.QueueFree();
        Lab(_sumBody, "РАЗБОР ПОЛЁТА", ConsoleFull.Accent, 20);
        Lab(_sumBody, mis.Title, ConsoleFull.Dim, 11);
        Lab(_sumBody, "", ConsoleFull.Dim, 4);
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        Line(_sumBody, "наибольший напор", $"{b.MaxQ / 1000:F0} кПа у носителя, "
                                         + $"{s.MaxQ / 1000:F0} кПа у корабля");
        Line(_sumBody, "наибольшая перегрузка", $"{b.MaxG:F1} g у носителя, {s.MaxG:F1} g у корабля");
        Line(_sumBody, "плитка нагрелась до", $"{s.MaxTile:F0} К при пределе прогара");
        Line(_sumBody, "топливо в баках", $"{b.Prop / 1000:F1} т у носителя, "
                                        + $"{s.Prop / 1000:F1} т у корабля");
        Line(_sumBody, "ускоритель", Where(b));
        Line(_sumBody, "корабль", Where(s));
        Lab(_sumBody, "", ConsoleFull.Dim, 4);
        Lab(_sumBody, "вехи полёта", ConsoleFull.Val, 12);
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 1);
        _sumBody.AddChild(grid);
        foreach (LogEntry m in sim.Marks) {
            Label tl = Lab(grid, Clock(m.T), ConsoleFull.Accent, 10);
            tl.CustomMinimumSize = new Vector2(60, 0);
            tl.HorizontalAlignment = HorizontalAlignment.Right;
            Lab(grid, m.M, ConsoleFull.Dim, 10);
        }
        Lab(_sumBody, "", ConsoleFull.Dim, 4);
        Lab(_sumBody, "F11 — закрыть   ·   , и . — прыжок по вехам   ·   / — один шаг",
            ConsoleFull.Accent, 11);
        _sum.Visible = true;
    }
    private static string Where(Vehicle v) => v.Caught ? "пойман башней"
        : v.Crashed ? "разбился" : v.Landed ? "сел вне башни" : "остался в полёте";
    public static string Clock(double t) {
        string sign = t < 0 ? "T−" : "T+";
        double a = Math.Abs(t);
        return $"{sign}{(int)(a / 60):00}:{a % 60:00.0}";
    }
    private static void Line(Control parent, string what, string val) {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 10);
        parent.AddChild(h);
        Label a = Lab(h, what, ConsoleFull.Dim, 11);
        a.CustomMinimumSize = new Vector2(190, 0);
        Lab(h, val, ConsoleFull.Val, 11);
    }
    private static Label Lab(Control parent, string text, Color c, int size) {
        var l = new Label { Text = text };
        l.AddThemeColorOverride("font_color", c);
        l.AddThemeFontSizeOverride("font_size", size);
        parent.AddChild(l);
        return l;
    }
}
public partial class TapeBar : Control {
    private List<LogEntry> _marks = new();
    private double _now, _span = 600;
    public void Feed(List<LogEntry> marks, double now, double span) {
        _marks = marks;
        _now = now;
        _span = Math.Max(span, 60);
        QueueRedraw();
    }
    private float XOf(double t) => (float)((t + 10) / (_span + 10)) * (Size.X - 16) + 8;
    public override void _Draw() {
        float w = Size.X, h = Size.Y;
        DrawRect(new Rect2(0, 0, w, h), new Color(0.04f, 0.05f, 0.07f, 0.72f));
        float y = h * 0.42f;
        DrawLine(new Vector2(8, y), new Vector2(w - 8, y), ConsoleFull.Dim, 1);
        foreach (LogEntry m in _marks) {
            float x = XOf(m.T);
            bool past = m.T <= _now;
            DrawLine(new Vector2(x, y - 6), new Vector2(x, y + 6),
                     past ? ConsoleFull.Accent : ConsoleFull.Dim, past ? 2 : 1);
        }
        float xn = XOf(_now);
        DrawLine(new Vector2(xn, 2), new Vector2(xn, h - 2), ConsoleFull.Val, 2);
        Font f = ThemeDB.FallbackFont;
        float ty = h - 6;
        DrawString(f, new Vector2(8, ty), FlightTape.Clock(_now),
                   HorizontalAlignment.Left, -1, 11, ConsoleFull.Val);
        string near = Near();
        if (near.Length > 0)
            DrawString(f, new Vector2(80, ty), near,
                       HorizontalAlignment.Left, (int)(w - 200), 11, ConsoleFull.Dim);
        DrawString(f, new Vector2(w - 118, ty), ", .  прыжок по вехам",
                   HorizontalAlignment.Left, -1, 10, ConsoleFull.Dim);
    }
    private string Near() {
        string best = "";
        double dt = 12;
        foreach (LogEntry m in _marks)
            if (Math.Abs(m.T - _now) < dt) { dt = Math.Abs(m.T - _now); best = m.M; }
        return best;
    }
}
