using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Starship.Physics;
using PEngine = Starship.Physics.Engine;
namespace Starship.Game.Ui;
public sealed class EngineerView {
    private Control _root;
    private readonly TeleLog _tele = new();
    private PlotView _plot;
    private Seg _plotSeg, _stageSeg;
    private Label _clock, _phase, _speed, _anom, _seed, _guide;
    private readonly Dictionary<string, KvRow> _rows = new();
    private HFlowContainer _legend;
    private readonly List<Label> _legendText = new();
    private VBoxContainer _log;
    private readonly List<(HBoxContainer Row, Label Text, int Lv)> _logRows = new();
    private LogEntry _top;
    private EngineMap _map, _mini;
    private EngineDetail _detail;
    private ParamPanel _params;
    private TankPanel _tanks;
    private Label _thrF, _thrN, _thrS, _other;
    private readonly int[] _sel = { 0, 0 };
    private int _stage, _pin = -1;
    private SimState _sim;
    public Action<bool> Focus;
    public int Group { get; private set; }
    public VBoxContainer MissionCard { get; private set; }
    public Label MissionCaption { get; private set; }
    public PEngine Selected => _sim == null ? null : Pick(_sim.Veh[_stage]);
    public static EngineerView Build(Screens scr) {
        var v = new EngineerView { _root = scr.EngineerRoot };
        var bg = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore };
        Look.Bind(bg, x => x.Color = Look.Bg);
        v._root.AddChild(bg);
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 0);
        v._root.AddChild(page);
        page.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        v.BuildHead(page);
        var pad = new MarginContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        pad.AddThemeConstantOverride("margin_left", 20);
        pad.AddThemeConstantOverride("margin_right", 20);
        pad.AddThemeConstantOverride("margin_bottom", 20);
        page.AddChild(pad);
        var cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 16);
        pad.AddChild(cols);
        v.BuildLeft(Column(cols, 410));
        v.BuildCenter(Column(cols, 0));
        v.BuildRight(Column(cols, 500));
        return v;
    }
    private static VBoxContainer Column(Control parent, float w) {
        var c = new VBoxContainer {
            CustomMinimumSize = new Vector2(w, 0),
            SizeFlagsHorizontal = w > 0 ? Control.SizeFlags.Fill : Control.SizeFlags.ExpandFill,
        };
        c.AddThemeConstantOverride("separation", 16);
        parent.AddChild(c);
        return c;
    }
    private void BuildHead(Control page) {
        var m = new MarginContainer { CustomMinimumSize = new Vector2(0, 76) };
        m.AddThemeConstantOverride("margin_left", 30);
        m.AddThemeConstantOverride("margin_right", 30);
        page.AddChild(m);
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 30);
        m.AddChild(h);
        _clock = Look.Caption(h, "T+00:00,0", 40, 300, () => Look.Ink);
        _clock.VerticalAlignment = VerticalAlignment.Center;
        _phase = Look.Caption(h, "", 20, 400, () => Look.Ink2);
        _phase.VerticalAlignment = VerticalAlignment.Center;
        _stageSeg = Seg.Make(h, new[] { "Super Heavy", "Starship" });
        _stageSeg.Picked += PickStage;
        h.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        var th = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        Look.Bind(th, x => x.AddThemeStyleboxOverride("panel", Look.Box(Look.Panel, 999, 14, 2)));
        h.AddChild(th);
        var dots = new HBoxContainer();
        dots.AddThemeConstantOverride("separation", 4);
        th.AddChild(dots);
        Look.Caption(dots, "тема", 14, 400, () => Look.Ink2).VerticalAlignment = VerticalAlignment.Center;
        for (int i = 0; i < Themes.All.Length; i++) ThemeDot.Make(dots, i);
        _speed = Widgets.Tag(h, "скорость ×1");
        _anom = Widgets.Tag(h, "отказы выключены");
        _seed = Widgets.Tag(h, "зерно");
        Widgets.Tag(h, "сводка", "Tab");
        Widgets.Tag(h, "клавиши", "F1");
    }
    private void BuildLeft(VBoxContainer col) {
        VBoxContainer f = Widgets.Section(col, "Полёт").Body;
        Row(f, "alt", "Высота");
        Row(f, "spd", "Скорость");
        Row(f, "vv", "Вертикальная");
        Row(f, "dr", "Дальность");
        Row(f, "mach", "Число Маха");
        Row(f, "q", "Скоростной напор");
        Row(f, "g", "Перегрузка");
        Row(f, "heat", "Тепловой поток");
        Row(f, "tile", "Обшивка, наветр. и подветр.");
        (_, VBoxContainer g, Label gs) = Widgets.Section(col, "Наведение", "уставку держит автомат");
        _guide = gs;
        Row(g, "th", "Тангаж и команда");
        Row(g, "aoa", "Угол атаки");
        Row(g, "gim", "Отклонение сопел");
        Row(g, "fin", "Решётчатые рули");
        VBoxContainer o = Widgets.Section(col, "Орбита").Body;
        Row(o, "orb", "Апогей и перигей");
        Row(o, "aim", "Цель");
        (PanelContainer mb, VBoxContainer mc, Label ms) = Widgets.Section(col, "Задание");
        mb.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        MissionCard = mc;
        MissionCaption = ms;
    }
    private void Row(VBoxContainer parent, string key, string label) => _rows[key] = KvRow.Make(parent, label);
    private void BuildCenter(VBoxContainer col) {
        (PanelContainer eb, VBoxContainer e, _) = Widgets.Section(col, null);
        eb.CustomMinimumSize = new Vector2(0, 560);
        var row = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 26);
        e.AddChild(row);
        var maps = new VBoxContainer { CustomMinimumSize = new Vector2(500, 0) };
        maps.AddThemeConstantOverride("separation", 0);
        row.AddChild(maps);
        var head = new HBoxContainer { CustomMinimumSize = new Vector2(0, 24) };
        head.AddThemeConstantOverride("separation", 10);
        maps.AddChild(head);
        Look.Caption(head, "Двигатели", 18, 600, () => Look.Ink).VerticalAlignment = VerticalAlignment.Bottom;
        Look.Caption(head, "давление в камере, МПа", 14, 400, () => Look.Lab).VerticalAlignment = VerticalAlignment.Bottom;
        maps.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = Control.MouseFilterEnum.Ignore });
        _map = new EngineMap {
            CustomMinimumSize = new Vector2(470, 404), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        _map.Picked += i => _sel[_stage] = i;
        Look.Bind(_map, x => x.QueueRedraw());
        maps.AddChild(_map);
        maps.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        var thr = new HBoxContainer();
        thr.AddThemeConstantOverride("separation", 28);
        maps.AddChild(thr);
        _thrF = Pair(thr, "Тяга");
        _thrN = Pair(thr, "работают");
        _thrS = Pair(thr, "уставка");
        var other = new HBoxContainer { CustomMinimumSize = new Vector2(0, 44) };
        other.AddThemeConstantOverride("separation", 10);
        maps.AddChild(other);
        _mini = new EngineMap {
            Mini = true, CustomMinimumSize = new Vector2(44, 44), MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        Look.Bind(_mini, x => x.QueueRedraw());
        other.AddChild(_mini);
        _other = Look.Caption(other, "", 14, 400, () => Look.Lab);
        _other.VerticalAlignment = VerticalAlignment.Center;
        _detail = EngineDetail.Build(row);
        _params = ParamPanel.Build(col);
        _params.Picked += i => Group = i;
    }
    private static Label Pair(Control parent, string name) {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 6);
        parent.AddChild(h);
        Look.Caption(h, name, 14, 400, () => Look.Lab).VerticalAlignment = VerticalAlignment.Bottom;
        Label v = Look.Caption(h, "—", 26, 300, null);
        v.VerticalAlignment = VerticalAlignment.Bottom;
        return v;
    }
    private void BuildRight(VBoxContainer col) {
        _tanks = TankPanel.Build(col);
        (PanelContainer pb, VBoxContainer p, _) = Widgets.Section(col, null);
        pb.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _plotSeg = Seg.Make(p, new[] { "Телеметрия F3", "Развёртка F4", "Коридор входа F5" });
        _plotSeg.Picked += SetPlot;
        p.AddChild(new Control { CustomMinimumSize = new Vector2(0, 7), MouseFilter = Control.MouseFilterEnum.Ignore });
        _plot = new PlotView {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
        };
        Look.Bind(_plot, x => x.QueueRedraw());
        p.AddChild(_plot);
        _legend = new HFlowContainer();
        _legend.AddThemeConstantOverride("h_separation", 16);
        _legend.AddThemeConstantOverride("v_separation", 4);
        p.AddChild(_legend);
        for (int i = 0; i < 4; i++) {
            var item = new HBoxContainer();
            item.AddThemeConstantOverride("separation", 7);
            _legend.AddChild(item);
            var sw = new Swatch {
                Series = i, CustomMinimumSize = new Vector2(20, 14), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            Look.Bind(sw, x => x.QueueRedraw());
            item.AddChild(sw);
            _legendText.Add(Look.Caption(item, "", 13, 400, () => Look.Ink2));
        }
        SetPlot(0);
        (PanelContainer lb, VBoxContainer l, _) = Widgets.Section(col, "Журнал");
        lb.CustomMinimumSize = new Vector2(0, 256);
        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
        };
        l.AddChild(scroll);
        _log = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _log.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_log);
        Look.Bind(_log, _ => PaintLog());
    }
    public void SetPlot(int kind) {
        kind = Math.Clamp(kind, 0, 2);
        _plot.Mode = kind == 1 ? PlotView.Kind.Map : kind == 2 ? PlotView.Kind.Entry : PlotView.Kind.Telemetry;
        if (_plotSeg.Selected != kind) _plotSeg.Select(kind);
        _plot.QueueRedraw();
    }
    public void PushTele(SimState sim) => _tele.Push(sim, sim.Veh[0], sim.Veh[1]);
    public void ClearTele() => _tele.Clear();
    public void SelectGroup(int idx) => Group = Math.Clamp(idx, 0, ParamDefs.Groups.Count - 1);
    public void SelectEngine(Vehicle v, int n) =>
        _sel[v.Kind == Kind.Ship ? 1 : 0] = Math.Clamp(n, 0, Math.Max(0, v.Eng.Count - 1));
    public void SetParam(Vehicle v, string key, string text) {
        ParamRow r = ParamDefs.Row(key);
        if (r == null || v == null || !NumFmt.TryParse(text, out double x)) return;
        x = Math.Clamp(x, r.Lo, r.Hi);
        if (r.Scope == Scope.Vehicle) r.SetVeh?.Invoke(v, x);
        else if (r.Scope == Scope.Engine)
            foreach (PEngine e in v.Eng) r.Set?.Invoke(e.P, x);
        _sim?.LogMsg($"Параметр «{r.Label}» → {NumFmt.F(x, r.Digits)}", 1);
    }
    private PEngine Pick(Vehicle v) => v.Eng.Count == 0 ? null : v.Eng[Math.Clamp(_sel[_stage], 0, v.Eng.Count - 1)];
    private void PickStage(int i) {
        if (_sim == null) return;
        if (_sim.Veh[1].Attached) _pin = i;
        else { _pin = -1; Focus?.Invoke(i == 1); }
    }
    public void Update(SimState sim, double speed, bool paused) {
        _sim = sim;
        _tele.Push(sim, sim.Veh[0], sim.Veh[1]);
        if (!_root.Visible) return;
        Vehicle v = sim.FocusVeh();
        if (!sim.Veh[1].Attached) _pin = -1;
        _stage = _pin >= 0 ? _pin : v.Kind == Kind.Ship ? 1 : 0;
        double now = Time.GetTicksMsec() / 1000.0;
        _plot.Feed(sim, _tele, now);
        Head(sim, v, speed, paused);
        Flight(sim, v);
        Engines(sim, now);
        _params.Update(sim, sim.Veh[_stage], Pick(sim.Veh[_stage]), Group);
        _tanks.Update(sim, _stage);
        Legend();
        Journal(sim);
    }
    private void Head(SimState sim, Vehicle v, double speed, bool paused) {
        Look.Set(_clock, NumFmt.Clock(sim.T));
        Look.Set(_phase, Phases.Console(v.Mode));
        if (_stageSeg.Selected != _stage) _stageSeg.Select(_stage);
        Look.Set(_speed, paused ? "пауза"
            : "скорость ×" + speed.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ','));
        Look.Tint(_speed, paused || speed != 1 ? Look.Warn : Look.Ink2);
        Look.Set(_anom, sim.AnomOn ? "отказы включены" : "отказы выключены");
        Look.Tint(_anom, sim.AnomOn ? Look.Warn : Look.Ink2);
        Look.Set(_seed, $"зерно {sim.Seed:x8}");
    }
    private void Flight(SimState sim, Vehicle v) {
        _rows["alt"].Set(NumFmt.Dist(v.Alt, out string ua), ua);
        _rows["spd"].Set(NumFmt.F(v.Speed, 0), "м/с");
        _rows["vv"].Set(NumFmt.F(v.VVert, 0), "м/с", v.VVert < -80 ? Look.Warn : null);
        _rows["dr"].Set(NumFmt.Dist(sim.Downrange(v), out string ud), ud);
        _rows["mach"].Set(NumFmt.F(v.Mach, 2));
        _rows["q"].Set(NumFmt.F(v.Q / 1000, 1), "кПа", v.Q > 60e3 ? Look.Warn : null);
        _rows["g"].Set(NumFmt.F(v.Acc, 2), "g", v.Acc > 4 ? Look.Warn : null);
        _rows["heat"].Set(NumFmt.F(v.Heat, 0), "кВт/м²", v.Heat > 250 ? Look.Warn : null);
        double wLim = v.Kind == Kind.Ship ? Const.TILE_LIMIT : Const.SKIN_LIMIT;
        bool hot = v.TTile > wLim * 0.95 || v.TLee > Const.SKIN_LIMIT * 0.95;
        _rows["tile"].Set($"{NumFmt.F(v.TTile, 0)} / {NumFmt.F(v.TLee, 0)}"
                          + (v.Dmg > 0 ? $", прогар {NumFmt.F(100 * v.Dmg, 0)} %" : ""), "K",
                          hot ? Look.Crit : v.TTile > 800 || v.TLee > 700 ? Look.Warn : null);
        bool man = sim.Mode == "man";
        Look.Set(_guide, man ? "ручной режим" : "уставку держит автомат");
        Look.Tint(_guide, man ? Look.Warn : Look.Lab);
        _rows["th"].Set($"{NumFmt.F(v.Th * Const.R2D, 0)}° / {NumFmt.F(v.ThCmd * Const.R2D, 0)}°");
        _rows["aoa"].Set(NumFmt.F(v.Alpha * Const.R2D, 0) + "°", "",
                         Math.Abs(v.Alpha * Const.R2D) > 80 ? Look.Warn : null);
        _rows["gim"].Set(NumFmt.F(v.Gimbal * Const.R2D, 1) + "°");
        bool boost = v.Kind == Kind.Booster;
        KvRow fin = _rows["fin"];
        Look.Set(fin.Key, boost ? "Решётчатые рули" : "Двигатели ориентации");
        fin.Set(boost ? (v.FinDep < 0.5 ? "сложены" : "раскрыты") : (v.Rcs ? "включены" : "отключены"));
        Orbit o = Guidance.Orb(v);
        if (o.Apo > 0 && o.Peri > -400e3)
            _rows["orb"].Set($"{NumFmt.F(o.Apo / 1000, 0)} × {NumFmt.F(o.Peri / 1000, 0)}", "км");
        else _rows["orb"].Set("суборбита");
        _rows["aim"].Set($"{NumFmt.F(sim.TargetApo / 1000, 0)} × {NumFmt.F(sim.TargetPeri / 1000, 0)}", "км");
    }
    private void Engines(SimState sim, double now) {
        Vehicle sv = sim.Veh[_stage], ov = sim.Veh[1 - _stage];
        _map.Selected = Math.Clamp(_sel[_stage], 0, Math.Max(0, sv.Eng.Count - 1));
        _map.Feed(sv, now);
        _mini.Feed(ov, now);
        PEngine e = Pick(sv);
        if (e != null) _detail.Update(e);
        Look.Set(_thrF, NumFmt.F(sv.F / 1e6, 2) + " МН");
        Look.Tint(_thrF, Look.Ink);
        Look.Set(_thrN, $"{sv.NRun} из {sv.MaxEng}");
        Look.Tint(_thrN, sv.NRun < sv.NEng ? Look.Warn : Look.Ink);
        Look.Set(_thrS, NumFmt.F(100 * sv.Throttle, 0) + " %");
        Look.Tint(_thrS, Look.Ink);
        Look.Set(_other, $"{(ov.Kind == Kind.Booster ? "Super Heavy" : "Starship")}: работают {ov.NRun} из {ov.MaxEng}, "
                         + (ov.Fill >= 0.999 ? "баки полные" : $"топливо {NumFmt.F(ov.Fill * 100, 0)} %"));
    }
    private void Legend() {
        bool tele = _plot.Mode == PlotView.Kind.Telemetry;
        _legend.Visible = tele;
        if (!tele) return;
        string[] s = _plot.Legend();
        for (int i = 0; i < 4; i++) Look.Set(_legendText[i], s[i]);
    }
    private void Journal(SimState sim) {
        List<LogEntry> log = sim.Log;
        if (log.Count == 0 ? _top == null : log[0] == _top) return;
        int fresh = _top == null ? log.Count : log.IndexOf(_top);
        if (fresh < 0) {
            foreach ((HBoxContainer row, _, _) in _logRows) row.QueueFree();
            _logRows.Clear();
            fresh = log.Count;
        }
        for (int i = fresh - 1; i >= 0; i--) {
            LogEntry e = log[i];
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 10);
            _log.AddChild(h);
            _log.MoveChild(h, 0);
            Look.Caption(h, NumFmt.Clock(e.T), 13, 400, () => Look.Lab).CustomMinimumSize = new Vector2(82, 0);
            Label m = Look.Caption(h, e.M, 13, 400, null);
            m.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            m.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _logRows.Insert(0, (h, m, e.Lv));
        }
        while (_logRows.Count > 200) {
            _logRows[^1].Row.QueueFree();
            _logRows.RemoveAt(_logRows.Count - 1);
        }
        _top = log.Count > 0 ? log[0] : null;
        PaintLog();
    }
    private void PaintLog() {
        for (int i = 0; i < _logRows.Count; i++) {
            (_, Label m, int lv) = _logRows[i];
            Look.Tint(m, lv >= 3 ? Look.Crit : i == 0 ? Look.Ink : Look.Ink2);
        }
    }
}
