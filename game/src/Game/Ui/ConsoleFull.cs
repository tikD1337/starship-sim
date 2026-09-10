using System;
using System.Collections.Generic;
using Godot;
using Starship.Physics;
using PEngine = Starship.Physics.Engine;
namespace Starship.Game.Ui;
public sealed class ConsoleFull {
    public static readonly Color Bg = new(0.043f, 0.055f, 0.075f);
    public static readonly Color Card = new(0.063f, 0.078f, 0.102f);
    public static readonly Color Edge = new(0.13f, 0.19f, 0.26f);
    public static readonly Color Dim = new(0.44f, 0.54f, 0.64f);
    public static readonly Color Val = new(0.82f, 0.89f, 0.96f);
    public static readonly Color Accent = new(0.30f, 0.66f, 0.95f);
    public static readonly Color Warn = new(0.98f, 0.72f, 0.25f);
    public static readonly Color Bad = new(0.95f, 0.35f, 0.32f);
    public static readonly Color Good = new(0.36f, 0.85f, 0.55f);
    public SubViewport World;
    private SubViewportContainer _viewBox;
    private Control _root;
    private HBoxContainer _cols;
    private VBoxContainer _left, _mid, _right;
    private bool _full;
    private int _group;
    private readonly List<Button> _groupTabs = new();
    private VBoxContainer _paramBody;
    private readonly List<(ParamRow row, Label val, LineEdit edit)> _paramCells = new();
    private Label _phase, _clock, _seed, _log;
    private ScrollContainer _logScroll;
    private int _logShown = -1;
    private Label _mode, _camLab;
    private readonly Dictionary<string, Label> _rows = new();
    private static readonly Dictionary<Control, Color> Painted = new();
    private static readonly string[] Fmt = { "F0", "F1", "F2", "F3", "F4", "F5", "F6" };
    private static readonly StyleBoxFlat[] EngBox = {
        Box(new Color(0.085f, 0.105f, 0.135f), Edge, 1, 2, 4),
        Box(new Color(0.09f, 0.24f, 0.15f), Edge, 1, 2, 4),
        Box(new Color(0.32f, 0.10f, 0.10f), Edge, 1, 2, 4),
    };
    private readonly int[] _engState = new int[64];
    private readonly List<PanelContainer> _engCells = new();
    private readonly List<Label> _engName = new(), _engVal = new(), _pumpCells = new();
    private ProgressBar _thrBar, _fuelB, _fuelS;
    private Label _thrTxt;
    private SimState _sim;
    private Vehicle _focus;
    public string CamName = "Орбита";
    public bool Cockpit;
    public double Speed = 1.0;
    public bool Paused;
    private VBoxContainer _cockHud;
    private PanelContainer _keys;
    private readonly TeleLog _tele = new();
    private PlotView _plotBig, _plotSmall;
    private int _tab;
    private static readonly string[] KeyHelp = {
        "— обзор",
        "1 орбитальная камера · 2 камеры на корпусе · 3 свободная (WASD, Q/E, Shift) · 4 кабина",
        "мышь — поворот, колесо — зум · Tab обзор во весь экран · V сменить ступень",
        "— приборы",
        "F2 обзор · F3 графики · F4 развёртка · F5 коридор входа · F1 эта карточка",
        "— задание",
        "F6 сменить задание · F7 отказы вкл/выкл · R полёт заново (то же зерно)",
        "F9 записать прогон · F10 повторить записанный полёт",
        "— время",
        "пробел пауза · ] быстрее вдвое · [ медленнее вдвое · R перезапуск · Esc выход",
        "— управление полётом",
        "↑/↓ тяга · ←/→ тангаж · , / . крен · T ДМТ · S или Enter разделение · 0 вернуть наведению",
        "— полезная нагрузка",
        "B створка отсека · N выпустить один Starlink · M выпустить десять",
    };
    public static StyleBoxFlat Box(Color fill, Color border, int w = 1, int r = 3, int pad = 8) {
        var sb = new StyleBoxFlat {
            BgColor = fill, BorderColor = border, CornerRadiusTopLeft = r, CornerRadiusTopRight = r,
            CornerRadiusBottomLeft = r, CornerRadiusBottomRight = r,
            ContentMarginLeft = pad, ContentMarginRight = pad,
            ContentMarginTop = pad - 2, ContentMarginBottom = pad - 2,
        };
        sb.SetBorderWidthAll(w);
        return sb;
    }
    public static Button Tab(Control parent, string text) {
        var b = new Button { Text = text, Flat = true };
        b.AddThemeFontSizeOverride("font_size", 11);
        b.AddThemeStyleboxOverride("normal", Box(new Color(0.09f, 0.11f, 0.15f), Edge, 1, 2, 6));
        b.AddThemeStyleboxOverride("hover", Box(new Color(0.12f, 0.16f, 0.21f), Accent, 1, 2, 6));
        b.AddThemeStyleboxOverride("pressed", Box(new Color(0.10f, 0.22f, 0.34f), Accent, 1, 2, 6));
        parent.AddChild(b);
        return b;
    }
    private static Label Lab(string s, Color c, int size, Control parent = null) {
        var l = new Label { Text = s };
        l.AddThemeColorOverride("font_color", c);
        l.AddThemeFontSizeOverride("font_size", size);
        parent?.AddChild(l);
        return l;
    }
    private static VBoxContainer CardBox(Control parent, string title, float minH = 0, bool grow = false) {
        var p = new PanelContainer();
        if (grow) p.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        p.AddThemeStyleboxOverride("panel", Box(Card, Edge));
        if (minH > 0) p.CustomMinimumSize = new Vector2(0, minH);
        p.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        parent.AddChild(p);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 4);
        p.AddChild(v);
        if (title != null) Lab(title, Accent, 11, v);
        return v;
    }
    public static ConsoleFull Build(Node parent) {
        var ui = new ConsoleFull();
        var layer = new CanvasLayer();
        parent.AddChild(layer);
        var back = new ColorRect { Color = Bg };
        back.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        back.MouseFilter = Control.MouseFilterEnum.Ignore;
        layer.AddChild(back);
        ui._root = new Control();
        ui._root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ui._root.MouseFilter = Control.MouseFilterEnum.Ignore;
        layer.AddChild(ui._root);
        ui._cols = new HBoxContainer();
        ui._cols.AddThemeConstantOverride("separation", 8);
        ui._cols.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ui._cols.MouseFilter = Control.MouseFilterEnum.Ignore;
        ui._cols.OffsetLeft = 10; ui._cols.OffsetTop = 10;
        ui._cols.OffsetRight = -10; ui._cols.OffsetBottom = -10;
        ui._root.AddChild(ui._cols);
        ui.BuildLeft();
        ui.BuildMid();
        ui.BuildRight();
        ui.SelectGroup(0);
        return ui;
    }
    private void BuildLeft() {
        _left = new VBoxContainer { CustomMinimumSize = new Vector2(316, 0) };
        _left.AddThemeConstantOverride("separation", 8);
        _cols.AddChild(_left);
        VBoxContainer head = CardBox(_left, null, 108);
        _phase = Lab("ПРЕДСТАРТОВАЯ", Accent, 15, head);
        _clock = Lab("T−00:10,0", Val, 21, head);
        _seed = Lab("зерно —", Dim, 10, head);
        _mode = Lab("уставку держит наведение", Dim, 10, head);
        _camLab = Lab("камера: Орбита", Dim, 10, head);
        VBoxContainer ctl = CardBox(_left, "УПРАВЛЕНИЕ", 0, true);
        var tabs = new HFlowContainer();
        tabs.AddThemeConstantOverride("h_separation", 3);
        tabs.AddThemeConstantOverride("v_separation", 3);
        ctl.AddChild(tabs);
        for (int i = 0; i < ParamDefs.Groups.Count; i++) {
            int idx = i;
            Button b = Tab(tabs, ParamDefs.Groups[i].Name);
            b.Pressed += () => SelectGroup(idx);
            _groupTabs.Add(b);
        }
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        ctl.AddChild(scroll);
        _paramBody = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _paramBody.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(_paramBody);
    }
    public void SelectGroup(int idx) {
        _group = idx;
        for (int i = 0; i < _groupTabs.Count; i++) {
            _groupTabs[i].AddThemeColorOverride("font_color", i == idx ? Accent : Dim);
        }
        foreach (Node c in _paramBody.GetChildren()) c.QueueFree();
        _paramCells.Clear();
        foreach (ParamSection sec in ParamDefs.Groups[idx].Sections) {
            Label t = Lab(sec.Title.ToUpperInvariant(), Dim, 9, _paramBody);
            t.AddThemeConstantOverride("line_spacing", 0);
            foreach (ParamRow r in sec.Rows) {
                var h = new HBoxContainer();
                h.AddThemeConstantOverride("separation", 6);
                _paramBody.AddChild(h);
                Label nm = Lab(r.Label, Dim, 11, h);
                nm.CustomMinimumSize = new Vector2(178, 0);
                nm.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                nm.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                Label v = null;
                LineEdit ed = null;
                if (r.Editable) {
                    ed = new LineEdit { CustomMinimumSize = new Vector2(74, 20), Alignment = HorizontalAlignment.Right };
                    ed.AddThemeFontSizeOverride("font_size", 11);
                    ed.AddThemeStyleboxOverride("normal", Box(new Color(0.08f, 0.10f, 0.14f), Edge, 1, 2, 4));
                    ed.AddThemeStyleboxOverride("focus", Box(new Color(0.08f, 0.12f, 0.18f), Accent, 1, 2, 4));
                    h.AddChild(ed);
                    ParamRow rr = r;
                    ed.TextSubmitted += s => Apply(rr, s);
                    ed.FocusExited += () => Apply(rr, ed.Text);
                }
                else {
                    v = Lab("—", Val, 11, h);
                    v.CustomMinimumSize = new Vector2(74, 0);
                    v.HorizontalAlignment = HorizontalAlignment.Right;
                }
                Label u = Lab(r.Unit, Dim, 9, h);
                u.CustomMinimumSize = new Vector2(38, 0);
                _paramCells.Add((r, v, ed));
            }
        }
    }
    public void SetParam(Vehicle v, string key, string text) {
        foreach (ParamGroup g in ParamDefs.Groups)
            foreach (ParamSection sec in g.Sections)
                foreach (ParamRow r in sec.Rows)
                    if (r.Key == key) { Apply(v, r, text); return; }
    }
    private void Apply(ParamRow r, string text) => Apply(_focus, r, text);
    private void Apply(Vehicle v, ParamRow r, string text) {
        if (v == null) return;
        if (!double.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out double x)) return;
        x = Math.Clamp(x, r.Lo, r.Hi);
        if (r.Scope == Scope.Vehicle) r.SetVeh?.Invoke(v, x);
        else if (r.Scope == Scope.Engine)
            foreach (PEngine e in v.Eng) r.Set?.Invoke(e.P, x);
        _sim?.LogMsg($"Параметр «{r.Label}» → {x:0.###}", 1);
    }
    private void Row(VBoxContainer parent, string key, string label, int labW = 150, int fs = 11,
                     Color? labCol = null) {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 6);
        parent.AddChild(h);
        Label l = Lab(label, labCol ?? Dim, fs, h);
        l.CustomMinimumSize = new Vector2(labW, 0);
        Label v = Lab("—", Val, fs + 1, h);
        v.HorizontalAlignment = HorizontalAlignment.Right;
        v.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _rows[key] = v;
    }
    public static void Write(Label l, string v) {
        if (l.Text != v) l.Text = v;
    }
    public static void Tint(Control l, Color c) {
        if (Painted.TryGetValue(l, out Color prev) && prev == c) return;
        l.AddThemeColorOverride("font_color", c);
        Painted[l] = c;
    }
    private static void Txt(Label l, string v) => Write(l, v);
    private void Col(Label l, Color c) => Tint(l, c);
    private void Set(string key, string v, Color? c = null) {
        if (!_rows.TryGetValue(key, out Label l)) return;
        Txt(l, v);
        Col(l, c ?? Val);
    }
    private void BuildMid() {
        _mid = new VBoxContainer { CustomMinimumSize = new Vector2(430, 0) };
        _mid.AddThemeConstantOverride("separation", 8);
        _mid.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _cols.AddChild(_mid);
        VBoxContainer eng = CardBox(_mid, "БЛОК ДВИГАТЕЛЕЙ", 0);
        GridContainer grid = Grid(eng, 6, 4, 4);
        for (int i = 0; i < 39; i++) {
            var p = new PanelContainer { CustomMinimumSize = new Vector2(66, 34) };
            p.AddThemeStyleboxOverride("panel", Box(new Color(0.085f, 0.105f, 0.135f), Edge, 1, 2, 4));
            grid.AddChild(p);
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 0);
            p.AddChild(v);
            Label n = Lab("", Dim, 9, v);
            Label q = Lab("", Val, 11, v);
            _engCells.Add(p);
            _engName.Add(n);
            _engVal.Add(q);
        }
        VBoxContainer thr = CardBox(_mid, "ТЯГА ДВИГАТЕЛЬНОЙ УСТАНОВКИ", 0);
        _thrBar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 0, ShowPercentage = false };
        _thrBar.CustomMinimumSize = new Vector2(0, 12);
        _thrBar.AddThemeStyleboxOverride("background", Box(new Color(0.08f, 0.10f, 0.13f), Edge, 1, 2, 0));
        _thrBar.AddThemeStyleboxOverride("fill", Box(Accent, Accent, 0, 2, 0));
        thr.AddChild(_thrBar);
        _thrTxt = Lab("—", Val, 11, thr);
        Row(thr, "gim", "Отклонение сопел");
        Row(thr, "aoa", "Угол атаки");
        Row(thr, "th", "Тангаж / команда");
        VBoxContainer pmp = CardBox(_mid, "ТУРБОНАСОСНЫЙ АГРЕГАТ · S1-1", 0);
        GridContainer pg = Grid(pmp, 5, 8, 2);
        foreach (string h in new[] { "", "УСТАВКА", "КОМАНДА", "ФАКТ", "ИЗМЕРЕНО" }) {
            Label l = Lab(h, Dim, 9, pg);
            l.CustomMinimumSize = new Vector2(h == "" ? 118 : 62, 0);
            l.HorizontalAlignment = h == "" ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        }
        foreach (string name in new[] { "Обороты, об/мин", "Напор, МПа", "Расход, кг/с", "КПД", "Вибрация, g", "Запас NPSH" })
        {
            Lab(name, Dim, 10, pg);
            for (int k = 0; k < 4; k++) {
                Label c = Lab("—", Val, 10, pg);
                c.HorizontalAlignment = HorizontalAlignment.Right;
                c.CustomMinimumSize = new Vector2(62, 0);
                _pumpCells.Add(c);
            }
        }
        VBoxContainer fl = CardBox(_mid, "ПОЛЁТ", 0);
        var fg = new HBoxContainer();
        fg.AddThemeConstantOverride("separation", 14);
        fl.AddChild(fg);
        var c1 = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var c2 = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        c1.AddThemeConstantOverride("separation", 1);
        c2.AddThemeConstantOverride("separation", 1);
        fg.AddChild(c1); fg.AddChild(c2);
        Row(c1, "alt", "Высота", 110);
        Row(c1, "spd", "Скорость", 110);
        Row(c1, "vv", "Вертикальная", 110);
        Row(c1, "dr", "Дальность", 110);
        Row(c1, "mach", "Число Маха", 110);
        Row(c2, "q", "Скор. напор", 110);
        Row(c2, "g", "Перегрузка", 110);
        Row(c2, "orb", "Апогей × перигей", 110);
        Row(c2, "heat", "Тепловой поток", 110);
        Row(c2, "tile", "Борт: наветр. / подветр.", 110);
        Lab("остаток топлива: ускоритель / корабль", Dim, 9, fl);
        _fuelB = MakeBar(fl, Accent);
        _fuelS = MakeBar(fl, Good);
    }
    private static GridContainer Grid(Control parent, int cols, int hsep, int vsep) {
        var g = new GridContainer { Columns = cols };
        g.AddThemeConstantOverride("h_separation", hsep);
        g.AddThemeConstantOverride("v_separation", vsep);
        parent.AddChild(g);
        return g;
    }
    private static ProgressBar MakeBar(Control parent, Color c) {
        var b = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 100, ShowPercentage = false };
        b.CustomMinimumSize = new Vector2(0, 8);
        b.AddThemeStyleboxOverride("background", Box(new Color(0.08f, 0.10f, 0.13f), Edge, 1, 2, 0));
        b.AddThemeStyleboxOverride("fill", Box(c, c, 0, 2, 0));
        parent.AddChild(b);
        return b;
    }
    private void BuildRight() {
        _right = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        _right.AddThemeConstantOverride("separation", 8);
        _right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _right.MouseFilter = Control.MouseFilterEnum.Ignore;
        _cols.AddChild(_right);
        var holder = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        holder.AddThemeStyleboxOverride("panel", Box(Card, Edge, 1, 3, 4));
        holder.MouseFilter = Control.MouseFilterEnum.Ignore;
        _right.AddChild(holder);
        _viewBox = new SubViewportContainer { Stretch = true };
        _viewBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _viewBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _viewBox.MouseFilter = Control.MouseFilterEnum.Ignore;
        holder.AddChild(_viewBox);
        World = new SubViewport {
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always, HandleInputLocally = false,
            Msaa3D = Viewport.Msaa.Msaa4X, Size = new Vector2I(760, 720),
        };
        _viewBox.AddChild(World);
        _plotBig = new PlotView { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.AddChild(_plotBig);
        _cockHud = new VBoxContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
            Visible = false,
        };
        holder.AddChild(_cockHud);
        Row(_cockHud, "cBank", "Крен", 150, 15, Val);
        Row(_cockHud, "cAoa", "Угол атаки", 150, 15, Val);
        Row(_cockHud, "cRcs", "ДМТ", 150, 15, Val);
        Row(_cockHud, "cThr", "Тяга", 150, 15, Val);
        _keys = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _keys.AddThemeStyleboxOverride("panel", Box(new Color(0.04f, 0.05f, 0.07f, 0.90f), Accent, 1, 4, 18));
        holder.AddChild(_keys);
        var kv = new VBoxContainer();
        kv.AddThemeConstantOverride("separation", 3);
        kv.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _keys.AddChild(kv);
        Lab("КЛАВИШИ  ·  F1 — убрать", Accent, 14, kv);
        foreach (string s in KeyHelp) Lab(s, s.StartsWith("—") ? Accent : Val, 12, kv);
        VBoxContainer tel = CardBox(_right, "ТЕЛЕМЕТРИЯ   ·   F2 обзор · F3 графики · F4 развёртка · F5 вход", 176);
        _plotSmall = new PlotView {
            Compact = true, MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 146),
        };
        tel.AddChild(_plotSmall);
        VBoxContainer lg = CardBox(_right, "ЖУРНАЛ", 178);
        _logScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _logScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        lg.AddChild(_logScroll);
        _log = Lab("", Dim, 10, _logScroll);
        _log.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _log.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }
    public void ToggleKeys() => _keys.Visible = !_keys.Visible;
    public void PushTele(SimState sim) => _tele.Push(sim, sim.Veh[0], sim.Veh[1]);
    public void ClearTele() => _tele.Clear();
    public VBoxContainer AddLeftCard(string title, float minH = 0, int at = -1) {
        VBoxContainer v = CardBox(_left, title, minH);
        if (at >= 0) _left.MoveChild(v.GetParent(), at);
        return v;
    }
    public void AddBottomStrip(Control c, float height) {
        var wrap = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        wrap.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        wrap.AddChild(new Control {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        c.CustomMinimumSize = new Vector2(0, height);
        c.MouseFilter = Control.MouseFilterEnum.Ignore;
        wrap.AddChild(c);
        _viewBox.GetParent().AddChild(wrap);
    }
    public PanelContainer AddOverlay() {
        var p = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", Box(new Color(0.04f, 0.05f, 0.07f, 0.93f), Accent, 1, 4, 22));
        _viewBox.GetParent().AddChild(p);
        return p;
    }
    public List<string> MouseGrabs() {
        var bad = new List<string>();
        for (Node n = _viewBox; n != null; n = n.GetParent()) {
            if (n is Control c && c.MouseFilter != Control.MouseFilterEnum.Ignore)
                bad.Add($"{c.GetType().Name} «{c.Name}» на пути к обзору");
            if (n == _root) break;
        }
        foreach (Node ch in _viewBox.GetParent().GetChildren())
            if (ch is Control c2 && c2 != _viewBox && c2.MouseFilter != Control.MouseFilterEnum.Ignore)
                bad.Add($"{c2.GetType().Name} «{c2.Name}» поверх обзора");
        return bad;
    }
    public void SetTab(int t) {
        _tab = t;
        _plotBig.Visible = t > 0;
        _plotBig.Mode = t == 2 ? PlotView.Kind.Map
                      : t == 3 ? PlotView.Kind.Entry : PlotView.Kind.Telemetry;
        World.RenderTargetUpdateMode = t > 0
            ? SubViewport.UpdateMode.Disabled : SubViewport.UpdateMode.Always;
    }
    public string TabName => _tab switch {
        1 => "графики", 2 => "развёртка", 3 => "коридор входа", _ => "обзор",
    };
    public void ToggleFull() {
        _full = !_full;
        _left.Visible = !_full;
        _mid.Visible = !_full;
        foreach (Node c in _right.GetChildren())
            if (c is Control ctrl && ctrl != _viewBox.GetParent())
                ctrl.Visible = !_full;
        _cols.OffsetLeft = _full ? 0 : 10;
        _cols.OffsetTop = _full ? 0 : 10;
        _cols.OffsetRight = _full ? 0 : -10;
        _cols.OffsetBottom = _full ? 0 : -10;
    }
    private static string Clock(double t) {
        string sign = t < 0 ? "−" : "+";
        double a = Math.Abs(t);
        return $"T{sign}{(int)(a / 60):00}:{a % 60:00.0}";
    }
    private static readonly Dictionary<string, string> Modes = new() {
        ["idle"] = "ПРЕДСТАРТОВАЯ", ["ascent"] = "ВЫВЕДЕНИЕ", ["meco"] = "ГОРЯЧЕЕ РАЗДЕЛЕНИЕ",
        ["flip"] = "РАЗВОРОТ", ["boostback"] = "ТОРМОЗНОЙ ИМПУЛЬС", ["coastB"] = "ПАССИВНЫЙ УЧАСТОК",
        ["entryB"] = "ИМПУЛЬС ВХОДА", ["landB"] = "ПОСАДКА УСКОРИТЕЛЯ", ["caught"] = "ЗАХВАЧЕН БАШНЕЙ",
        ["landed"] = "ПОСАДКА ВЫПОЛНЕНА", ["crashed"] = "РАЗРУШЕНИЕ",
        ["ascent2"] = "РАБОТА ВТОРОЙ СТУПЕНИ", ["coastS"] = "ПАССИВНЫЙ УЧАСТОК",
        ["circ"] = "ДОВЫВЕДЕНИЕ", ["orbit"] = "ОРБИТА", ["deorbit"] = "СХОД С ОРБИТЫ",
        ["coastD"] = "СПУСК", ["entryS"] = "ВХОД В АТМОСФЕРУ", ["flipS"] = "ПЕРЕВОРОТ",
        ["landS"] = "ПОСАДКА КОРАБЛЯ",
    };
    public void Update(SimState sim, Vehicle b, Vehicle s) {
        _sim = sim;
        Vehicle v = sim.FocusVeh();
        _focus = v;
        _tele.Push(sim, b, s);
        double now = Time.GetTicksMsec() / 1000.0;
        _plotSmall.Feed(sim, _tele, now);
        if (_plotBig.Visible) _plotBig.Feed(sim, _tele, now);
        Txt(_clock, Clock(sim.T) + (Speed != 1.0 ? $"   ×{Speed:0.###}" : "") + (Paused ? "   ПАУЗА" : ""));
        Col(_clock, Paused ? Warn : Val);
        Txt(_phase, Modes.TryGetValue(v.Mode, out string m) ? m : v.Mode);
        Txt(_seed, $"зерно {sim.Seed:x8}   отказы {(sim.AnomOn ? "вкл" : "выкл")}   {v.Name}");
        Txt(_mode, sim.Mode == "man" ? "РУЧНОЙ РЕЖИМ" : "уставку держит наведение");
        Col(_mode, sim.Mode == "man" ? Warn : Dim);
        Txt(_camLab, $"камера: {CamName}   ·   вид: {TabName}   ·   F1 — клавиши");
        _cockHud.Visible = Cockpit;
        if (Cockpit) {
            Set("cBank", $"{v.Bank * Const.R2D:F0}° / {v.BankCmd * Const.R2D:F0}°");
            Set("cAoa", $"{v.Alpha * Const.R2D:F0}° / {v.AlphaCmd:F0}°",
                Math.Abs(v.Alpha * Const.R2D) > 80 ? Warn : Val);
            Set("cRcs", v.Rcs ? $"вкл, нагрузка {100 * Math.Abs(v.RcsUse):F0} %" : "ВЫКЛЮЧЕН",
                v.Rcs ? Val : Bad);
            Set("cThr", $"{v.F / 1e6:F2} МН   {v.NRun}/{v.MaxEng}   {100 * v.Throttle:F0} %");
        }
        Orbit o = Guidance.Orb(v);
        Set("alt", PlotView.Dist(v.Alt));
        Set("spd", $"{v.Speed:F0} м/с");
        Set("vv", $"{v.VVert:F0} м/с", v.VVert < -80 ? Warn : Val);
        Set("dr", PlotView.Dist(sim.Downrange(v)));
        Set("mach", $"{v.Mach:F2}");
        Set("q", $"{v.Q / 1000.0:F1} кПа", v.Q > 60e3 ? Warn : Val);
        Set("g", $"{v.Acc:F2} g", v.Acc > 4 ? Warn : Val);
        Set("orb", o.Apo > 0 && o.Peri > -400e3 ? $"{o.Apo / 1000.0:F0} × {o.Peri / 1000.0:F0} км" : "—");
        Set("heat", $"{v.Heat:F0} кВт/м²", v.Heat > 250 ? Warn : Val);
        double wLim = v.Kind == Kind.Ship ? Const.TILE_LIMIT : Const.SKIN_LIMIT;
        bool hot = v.TTile > wLim * 0.95 || v.TLee > Const.SKIN_LIMIT * 0.95;
        Set("tile", $"{v.TTile:F0} / {v.TLee:F0} К" + (v.Dmg > 0 ? $"  прогар {100 * v.Dmg:F0} %" : ""),
            hot ? Bad : (v.TTile > 800 || v.TLee > 700 ? Warn : Val));
        Set("gim", $"{v.Gimbal * Const.R2D:F1}°");
        Set("aoa", $"{v.Alpha * Const.R2D:F0}°", Math.Abs(v.Alpha * Const.R2D) > 80 ? Warn : Val);
        Set("th", $"{v.Th * Const.R2D:F0}° / {v.ThCmd * Const.R2D:F0}°");
        double thrMax = Math.Max(1, v.MaxEng * Spec.RaptorSL.Fv);
        _thrBar.Value = 100 * Math.Clamp(v.F / thrMax, 0, 1);
        Txt(_thrTxt, $"{v.F / 1e6:F2} МН   работают {v.NRun} из {v.MaxEng}   уставка {100 * v.Throttle:F0} %");
        Col(_thrTxt, v.NRun < v.NEng ? Warn : Val);
        _fuelB.Value = 100 * Math.Clamp(b.Fill, 0, 1);
        _fuelS.Value = 100 * Math.Clamp(s.Fill, 0, 1);
        PaintEngines(b, s);
        PaintPump(v.Eng.Count > 0 ? v.Eng[0] : null);
        for (int i = 0; i < _paramCells.Count; i++) {
            (ParamRow r, Label lv, LineEdit ed) = _paramCells[i];
            double x;
            if (r.Scope == Scope.ReadOnly) {
                x = r.FromVehicle != null ? r.FromVehicle(v)
                    : (r.FromEngine != null && v.Eng.Count > 0 ? r.FromEngine(v.Eng[0]) : 0);
                if (lv != null) {
                    Txt(lv, x.ToString(Fmt[r.Digits]));
                    string zone = ParamDefs.ZoneOf(r, x);
                    Col(lv, zone == "crit" ? Bad : zone == "warn" ? Warn : Val);
                    if (zone.Length > 0 && lv.TooltipText != r.Note) lv.TooltipText = r.Note;
                }
            }
            else if (ed != null && !ed.HasFocus()) {
                x = r.Scope == Scope.Vehicle ? (r.GetVeh?.Invoke(v) ?? 0)
                    : (v.Eng.Count > 0 ? (r.Get?.Invoke(v.Eng[0].P) ?? 0) : 0);
                string txt = x.ToString(Fmt[r.Digits]);
                if (ed.Text != txt) ed.Text = txt;
            }
        }
        if (sim.Log.Count != _logShown) {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < sim.Log.Count; i++)
                sb.Append(Clock(sim.Log[i].T)).Append("  ").Append(sim.Log[i].M).Append('\n');
            _log.Text = sb.ToString();
            _logShown = sim.Log.Count;
        }
    }
    private void PaintEngines(Vehicle b, Vehicle s) {
        for (int i = 0; i < _engCells.Count; i++) {
            bool boost = i < 33;
            Vehicle veh = boost ? b : s;
            int k = boost ? i : i - 33;
            PEngine e = k < veh.Eng.Count ? veh.Eng[k] : null;
            int st;
            Color txt;
            string val;
            if (e == null) { st = 0; txt = Dim; val = ""; }
            else if (e.Failed) { st = 2; txt = Bad; val = "ОТКАЗ"; }
            else if (e.On && e.Pc > 1) { st = 1; txt = Good; val = $"{e.Pc:F1} МПа"; }
            else { st = 0; txt = Dim; val = "стоит"; }
            if (_engState[i] != st) {
                _engCells[i].AddThemeStyleboxOverride("panel", EngBox[st]);
                _engState[i] = st;
            }
            Txt(_engName[i], e?.Name ?? "");
            Txt(_engVal[i], val);
            Col(_engVal[i], txt);
        }
    }
    private void PaintPump(PEngine e) {
        if (e == null) return;
        double[,] t = {
            { e.P.RpmSet, e.P.RpmSet * (0.45 + 0.55 * e.Spool), e.Pf.Rpm, e.Po.Rpm },
            { Pump.NomF.DP / 1e6, Pump.NomO.DP / 1e6, e.Pf.DP / 1e6, e.Po.DP / 1e6 },
            { e.Pf.MdotNom, e.Po.MdotNom, e.Pf.Q * Pump.RHO_F, e.Po.Q * Pump.RHO_OX },
            { Pump.NomF.Eta, Pump.NomO.Eta, e.Pf.Eta, e.Po.Eta },
            { 0, 0, e.Pf.Vib, e.Po.Vib },
            { Pump.NomF.Npsh, Pump.NomO.Npsh, e.Pf.Cav * 100, e.Po.Cav * 100 },
        };
        int[] dig = { 0, 1, 1, 3, 2, 0 };
        for (int r = 0; r < 6; r++)
            for (int c = 0; c < 4; c++) {
                int idx = r * 4 + c;
                if (idx >= _pumpCells.Count) continue;
                Txt(_pumpCells[idx], t[r, c].ToString(Fmt[dig[r]]));
                bool bad = (r == 4 && t[r, c] > 6) || (r == 5 && c >= 2 && t[r, c] < 90);
                Col(_pumpCells[idx], bad ? Warn : (c < 2 ? Dim : Val));
            }
    }
}
