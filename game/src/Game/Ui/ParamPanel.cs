using System;
using System.Collections.Generic;
using Godot;
using Starship.Physics;
using PEngine = Starship.Physics.Engine;
namespace Starship.Game.Ui;
public sealed class ParamPanel {
    private Label _title, _small;
    private Seg _tabs;
    private HBoxContainer _cols;
    private int _group = -1;
    private bool _engineGroup;
    private readonly List<(ParamRow Row, Label Val, LineEdit Edit)> _cells = new();
    private SimState _sim;
    private Vehicle _v;
    private PEngine _e;
    public event Action<int> Picked;
    public static ParamPanel Build(Control parent) {
        var p = new ParamPanel();
        (PanelContainer box, VBoxContainer body, _) = Widgets.Section(parent, null);
        box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        box.GuiInput += e => {
            if (e is InputEventMouseButton { Pressed: true }) box.GetViewport().GuiReleaseFocus();
        };
        var head = new HBoxContainer { CustomMinimumSize = new Vector2(0, 24) };
        head.AddThemeConstantOverride("separation", 10);
        body.AddChild(head);
        p._title = Look.Caption(head, "Параметры", 18, 600, () => Look.Ink);
        p._small = Look.Caption(head, "", 14, 400, () => Look.Lab);
        p._title.VerticalAlignment = VerticalAlignment.Bottom;
        p._small.VerticalAlignment = VerticalAlignment.Bottom;
        body.AddChild(Gap(5));
        var names = new List<string>();
        foreach (ParamGroup g in ParamDefs.Groups) names.Add(g.Name);
        p._tabs = Seg.Make(body, names);
        p._tabs.Picked += i => p.Picked?.Invoke(i);
        body.AddChild(Gap(7));
        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
        };
        body.AddChild(scroll);
        p._cols = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        p._cols.AddThemeConstantOverride("separation", 40);
        scroll.AddChild(p._cols);
        return p;
    }
    private static Control Gap(float h) =>
        new() { CustomMinimumSize = new Vector2(0, h), MouseFilter = Control.MouseFilterEnum.Ignore };
    private void Select(int g) {
        _group = g;
        if (_tabs.Selected != g) _tabs.Select(g);
        foreach (Node n in _cols.GetChildren()) {
            _cols.RemoveChild(n);
            n.QueueFree();
        }
        _cells.Clear();
        ParamGroup grp = ParamDefs.Groups[g];
        _engineGroup = false;
        int split = ParamDefs.Split(grp);
        VBoxContainer col = null;
        for (int s = 0; s < grp.Sections.Count; s++) {
            if (s == 0 || s == split) {
                col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                col.AddThemeConstantOverride("separation", 2);
                _cols.AddChild(col);
            }
            else col.AddChild(Gap(6));
            Look.Caption(col, grp.Sections[s].Title, 13, 400, () => Look.Lab);
            foreach (ParamRow r in grp.Sections[s].Rows) {
                if (r.Scope == Scope.Engine && !r.Stage) _engineGroup = true;
                AddRow(col, r);
            }
        }
    }
    private void AddRow(VBoxContainer col, ParamRow r) {
        var h = new HBoxContainer { CustomMinimumSize = new Vector2(0, 28) };
        h.AddThemeConstantOverride("separation", 10);
        col.AddChild(h);
        Label k = Look.Caption(h, r.Label, 14, 400, () => Look.Ink2);
        k.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        k.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        k.VerticalAlignment = VerticalAlignment.Center;
        Label val = null;
        LineEdit ed = null;
        if (r.Editable) {
            ed = new LineEdit {
                CustomMinimumSize = new Vector2(108, 26), Alignment = HorizontalAlignment.Right,
                SelectAllOnFocus = true, ContextMenuEnabled = false,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            ed.AddThemeFontOverride("font", Look.Font(500));
            ed.AddThemeFontSizeOverride("font_size", 15);
            Look.Bind(ed, x => {
                x.AddThemeStyleboxOverride("normal", Look.Box(Look.Field, 8, 11, 2));
                x.AddThemeStyleboxOverride("focus", Look.Box(Look.Line, 8, 11, 2));
                x.AddThemeStyleboxOverride("read_only", Look.Box(Look.Field, 8, 11, 2));
                x.AddThemeColorOverride("font_color", Look.Ink);
                x.AddThemeColorOverride("caret_color", Look.Ink);
                x.AddThemeColorOverride("font_selected_color", Look.Ink);
                x.AddThemeColorOverride("selection_color", new Color(Look.Accent, 0.35f));
            });
            h.AddChild(ed);
            ed.TextSubmitted += s => { Apply(r, s); ed.ReleaseFocus(); };
            ed.FocusExited += () => Apply(r, ed.Text);
        }
        else {
            var m = new MarginContainer { CustomMinimumSize = new Vector2(108, 0) };
            m.AddThemeConstantOverride("margin_right", 11);
            h.AddChild(m);
            val = Look.Caption(m, "—", 15, 500, null);
            val.HorizontalAlignment = HorizontalAlignment.Right;
            val.VerticalAlignment = VerticalAlignment.Center;
            if (r.Note != null) {
                val.MouseFilter = Control.MouseFilterEnum.Pass;
                val.TooltipText = r.Note;
            }
        }
        Label u = Look.Caption(h, r.Unit, 13, 400, () => Look.Lab);
        u.CustomMinimumSize = new Vector2(56, 0);
        u.VerticalAlignment = VerticalAlignment.Center;
        _cells.Add((r, val, ed));
    }
    private void Apply(ParamRow r, string text) {
        if (_v == null || !NumFmt.TryParse(text, out double x)) return;
        x = Math.Clamp(x, r.Lo, r.Hi);
        double cur = r.Scope == Scope.Vehicle ? (r.GetVeh?.Invoke(_v) ?? 0)
                   : _e != null ? (r.Get?.Invoke(_e.P) ?? 0) : 0;
        if (Math.Abs(x - cur) <= Math.Pow(10, -r.Digits) * 0.5) return;
        if (r.Scope == Scope.Vehicle) r.SetVeh?.Invoke(_v, x);
        else if (r.Stage) foreach (PEngine e in _v.Eng) r.Set?.Invoke(e.P, x);
        else if (_e != null) r.Set?.Invoke(_e.P, x);
        else return;
        string who = r.Scope == Scope.Vehicle || r.Stage ? Widgets.StageName(_v) : _e.Name;
        _sim?.LogMsg($"{who}: «{r.Label}» → {NumFmt.F(x, r.Digits)}", 1);
    }
    public void Update(SimState sim, Vehicle v, PEngine e, int group) {
        _sim = sim;
        _v = v;
        _e = e;
        if (group != _group) Select(group);
        Look.Set(_title, _engineGroup && e != null ? $"Параметры {e.Name}" : $"Параметры {Widgets.StageName(v)}");
        Look.Set(_small, _engineGroup ? "правка меняет выбранный двигатель" : "правка меняет ступень");
        foreach ((ParamRow r, Label val, LineEdit ed) in _cells) {
            if (val != null) {
                double x = r.FromVehicle != null ? r.FromVehicle(v)
                         : r.FromEngine != null && e != null ? r.FromEngine(e) : 0;
                Look.Set(val, NumFmt.F(x, r.Digits));
                string z = ParamDefs.ZoneOf(r, x);
                Look.Tint(val, z == "crit" ? Look.Crit : z == "warn" ? Look.Warn : Look.Ink);
            }
            else if (ed != null && !ed.HasFocus()) {
                double x = r.Scope == Scope.Vehicle ? (r.GetVeh?.Invoke(v) ?? 0)
                         : e != null ? (r.Get?.Invoke(e.P) ?? 0) : 0;
                string t = NumFmt.F(x, r.Digits);
                if (ed.Text != t) ed.Text = t;
            }
        }
    }
}
