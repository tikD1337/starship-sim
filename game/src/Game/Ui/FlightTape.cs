using System;
using System.Collections.Generic;
using Godot;
using Starship.Physics;
namespace Starship.Game.Ui;
public sealed class FlightTape {
    private TapeBar _bar;
    private PanelContainer _sum;
    private VBoxContainer _sumBody;
    private double _span = 600;
    public static FlightTape Build(Screens host) {
        var t = new FlightTape { _bar = new TapeBar() };
        host.AddBottomStrip(t._bar, 46);
        Look.Bind(t._bar, x => x.QueueRedraw());
        t._sum = host.AddOverlay();
        t._sumBody = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        t._sumBody.AddThemeConstantOverride("separation", 4);
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
        Look.Caption(_sumBody, "Разбор полёта", 22, 600, () => Look.Ink);
        Look.Caption(_sumBody, mis.Title, 14, 400, () => Look.Lab);
        Gap(6);
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        Line("Наибольший напор", $"{NumFmt.F(b.MaxQ / 1000, 0)} кПа у ускорителя, {NumFmt.F(s.MaxQ / 1000, 0)} кПа у корабля");
        Line("Наибольшая перегрузка", $"{NumFmt.F(b.MaxG, 1)} g у ускорителя, {NumFmt.F(s.MaxG, 1)} g у корабля");
        Line("Плитка нагрелась до", $"{NumFmt.F(s.MaxTile, 0)} K");
        Line("Топливо в баках", $"{NumFmt.F(b.Prop / 1000, 1)} т у ускорителя, {NumFmt.F(s.Prop / 1000, 1)} т у корабля");
        Line("Ускоритель", Where(b));
        Line("Корабль", Where(s));
        Gap(6);
        Look.Caption(_sumBody, "Вехи полёта", 15, 600, () => Look.Ink);
        var grid = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 1);
        _sumBody.AddChild(grid);
        foreach (LogEntry m in sim.Marks) {
            Label tl = Look.Caption(grid, NumFmt.Clock(m.T), 12, 400, () => Look.Lab);
            tl.CustomMinimumSize = new Vector2(84, 0);
            tl.HorizontalAlignment = HorizontalAlignment.Right;
            Look.Caption(grid, m.M, 12, 400, () => Look.Ink2);
        }
        Gap(6);
        Look.Caption(_sumBody, "F11 — закрыть, «,» и «.» — прыжок по вехам, «/» — один шаг", 13, 400, () => Look.Lab);
        _sum.Visible = true;
    }
    private void Gap(float h) =>
        _sumBody.AddChild(new Control { CustomMinimumSize = new Vector2(0, h), MouseFilter = Control.MouseFilterEnum.Ignore });
    private static string Where(Vehicle v) => v.Caught ? "пойман башней"
        : v.Crashed ? "разбился" : v.Landed ? "сел вне башни" : "остался в полёте";
    private void Line(string what, string val) {
        var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        h.AddThemeConstantOverride("separation", 12);
        _sumBody.AddChild(h);
        Look.Caption(h, what, 14, 400, () => Look.Lab).CustomMinimumSize = new Vector2(200, 0);
        Look.Caption(h, val, 14, 500, () => Look.Ink);
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
    private float XOf(double t) => (float)((t + 10) / (_span + 10)) * (Size.X - 32) + 16;
    public override void _Draw() {
        float w = Size.X, h = Size.Y;
        DrawRect(new Rect2(0, 0, w, h), new Color(Look.Panel, 0.94f));
        float y = h * 0.40f;
        DrawLine(new Vector2(16, y), new Vector2(w - 16, y), Look.Line, 1);
        foreach (LogEntry m in _marks) {
            float x = XOf(m.T);
            bool past = m.T <= _now;
            DrawLine(new Vector2(x, y - 6), new Vector2(x, y + 6), past ? Look.Accent : Look.Lab, past ? 2 : 1);
        }
        float xn = XOf(_now);
        DrawLine(new Vector2(xn, 3), new Vector2(xn, h - 3), Look.Ink, 2);
        Font f = Look.Font(400);
        float ty = h - 7;
        DrawString(f, new Vector2(16, ty), NumFmt.Clock(_now), HorizontalAlignment.Left, -1, 13, Look.Ink);
        string near = Near();
        if (near.Length > 0)
            DrawString(f, new Vector2(110, ty), near, HorizontalAlignment.Left, (int)(w - 330), 13, Look.Ink2);
        DrawString(f, new Vector2(w - 200, ty), "«,» и «.» — прыжок по вехам", HorizontalAlignment.Left, -1, 12, Look.Lab);
    }
    private string Near() {
        string best = "";
        double dt = 12;
        foreach (LogEntry m in _marks)
            if (Math.Abs(m.T - _now) < dt) { dt = Math.Abs(m.T - _now); best = m.M; }
        return best;
    }
}
