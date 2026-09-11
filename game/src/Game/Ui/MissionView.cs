using System;
using System.Collections.Generic;
using Godot;
using Starship.Physics;
namespace Starship.Game.Ui;
public sealed class MissionView {
    private Label _caption, _score, _rest;
    private VBoxContainer _rows;
    private readonly List<(GoalDot Dot, Label Name, Label Pts)> _goal = new();
    private readonly List<Label> _note = new();
    private readonly List<Button> _pick = new(), _scriptTabs = new();
    private PanelContainer _final, _recs, _choose;
    private VBoxContainer _finalBody, _recsBody;
    private bool _shown;
    public static readonly (string Key, string Name)[] Scripts = {
        (null, "случайно"),
        ("ignFail", "незапуск"),
        ("engOut", "выключение"),
        ("pumpLow", "ТНА"),
        ("copvLeak", "наддув"),
        ("ctrlJam", "плоскость"),
        ("tileLoss", "плитки"),
    };
    public string Script;
    public bool RecordsShown => _recs.Visible;
    public static MissionView Build(VBoxContainer card, Label caption, Screens host, Action<string> onPick,
                                    Action onAnom, Action<string> onScript) {
        var v = new MissionView { _caption = caption };
        v._rows = new VBoxContainer();
        v._rows.AddThemeConstantOverride("separation", 9);
        card.AddChild(v._rows);
        var sum = new HBoxContainer();
        sum.AddThemeConstantOverride("separation", 5);
        card.AddChild(sum);
        Look.Caption(sum, "Очки", 14, 400, () => Look.Lab);
        v._score = Look.Caption(sum, "0", 14, 600, () => Look.Ink);
        v._rest = Look.Caption(sum, "", 14, 400, () => Look.Lab);
        Button ch = Widgets.Pill((Control)caption.GetParent(), "сменить", 13, true);
        ch.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        v._choose = host.AddOverlay();
        ch.Pressed += () => v._choose.Visible = !v._choose.Visible;
        v.BuildChooser(onPick, onAnom, onScript);
        v._recs = host.AddOverlay();
        v._recsBody = Body(v._recs, 4);
        v._final = host.AddOverlay();
        v._finalBody = Body(v._final, 6);
        return v;
    }
    private static VBoxContainer Body(PanelContainer p, int sep) {
        var b = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        b.AddThemeConstantOverride("separation", sep);
        p.AddChild(b);
        return b;
    }
    private void BuildChooser(Action<string> onPick, Action onAnom, Action<string> onScript) {
        VBoxContainer b = Body(_choose, 10);
        Look.Caption(b, "Задание", 22, 600, () => Look.Ink);
        foreach (string key in Mission.Keys) {
            string k = key;
            Button p = Widgets.Pill(b, Mission.NameOf(k), 15, true);
            p.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            p.Pressed += () => { _choose.Visible = false; onPick(k); };
            _pick.Add(p);
        }
        Look.Caption(b, "Отказы", 22, 600, () => Look.Ink);
        Button an = Widgets.Pill(b, "включить или выключить отказы", 15, true);
        an.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        an.Pressed += () => { _choose.Visible = false; onAnom(); };
        _pick.Add(an);
        Look.Caption(b, "Нештатная ситуация", 15, 400, () => Look.Lab);
        var flow = new HFlowContainer { CustomMinimumSize = new Vector2(520, 0) };
        flow.AddThemeConstantOverride("h_separation", 8);
        flow.AddThemeConstantOverride("v_separation", 8);
        b.AddChild(flow);
        foreach ((string key, string name) in Scripts) {
            string kk = key;
            Button s = Widgets.Pill(flow, name, 15, true);
            s.Pressed += () => { _choose.Visible = false; onScript(kk); };
            _scriptTabs.Add(s);
        }
        Look.Caption(b, "F6 — следующее задание, F7 — отказы", 13, 400, () => Look.Lab);
    }
    public void Reset(Mission m) {
        foreach (Node n in _rows.GetChildren()) n.QueueFree();
        _goal.Clear();
        _note.Clear();
        _shown = false;
        _final.Visible = false;
        foreach (Goal g in m.Goals) {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 10);
            _rows.AddChild(h);
            var dot = new GoalDot {
                CustomMinimumSize = new Vector2(12, 12), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            Look.Bind(dot, x => x.QueueRedraw());
            h.AddChild(dot);
            Label name = Look.Caption(h, g.Name, 14, 400, null);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            _goal.Add((dot, name, Look.Caption(h, "", 14, 400, () => Look.Lab)));
            Label note = Look.Caption(_rows, "", 12, 400, () => Look.Lab);
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            note.Visible = false;
            _note.Add(note);
        }
        Look.Set(_caption, m.Title);
    }
    public void HideFinal() => _final.Visible = false;
    public void ShowFinalAgain() { if (_shown) _final.Visible = true; }
    public void ToggleRecords(string key) {
        if (_recs.Visible) { _recs.Visible = false; ShowFinalAgain(); return; }
        _final.Visible = false;
        foreach (Node n in _recsBody.GetChildren()) n.QueueFree();
        Look.Caption(_recsBody, "Рекорды", 22, 600, () => Look.Ink);
        Look.Caption(_recsBody, "лучший результат для каждой пары «задание — нештатная ситуация»", 13, 400,
                     () => Look.Lab);
        foreach (string mk in Mission.Keys) {
            string m = mk;
            _recsBody.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = Control.MouseFilterEnum.Ignore });
            Look.Caption(_recsBody, Mission.NameOf(m), 15, 600, () => m == key ? Look.Ink : Look.Ink2);
            var grid = new GridContainer { Columns = 4, MouseFilter = Control.MouseFilterEnum.Ignore };
            grid.AddThemeConstantOverride("h_separation", 18);
            grid.AddThemeConstantOverride("v_separation", 3);
            _recsBody.AddChild(grid);
            Row(grid, "без отказов", Mission.RecordOf(Mission.RecKeyOf(m, false, null)));
            foreach ((string sk, string name) in Scripts)
                Row(grid, name, Mission.RecordOf(Mission.RecKeyOf(m, true, sk)));
        }
        _recsBody.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = Control.MouseFilterEnum.Ignore });
        Look.Caption(_recsBody, "«без отказов» — штатный полёт, «случайно» — набор неполадок наугад", 13, 400,
                     () => Look.Lab);
        Look.Caption(_recsBody, "F8 — закрыть", 13, 400, () => Look.Lab);
        _recs.Visible = true;
    }
    private static void Row(Control grid, string what, Mission.Rec r) {
        Cell(grid, what, 14, 400, () => Look.Lab, 130);
        Cell(grid, r.Score > 0 ? r.Score.ToString() : "—", 14, 500, () => r.Score > 0 ? Look.Ink : Look.Lab, 46,
             HorizontalAlignment.Right);
        Cell(grid, r.Score > 0 && !string.IsNullOrEmpty(r.Grade) ? r.Grade : "", 14, 600, () => GradeColor(r.Grade), 20,
             HorizontalAlignment.Center);
        Cell(grid, r.When ?? "", 13, 400, () => Look.Lab, 88);
    }
    private static void Cell(Control parent, string text, int size, int weight, Func<Color> ink, int width,
                             HorizontalAlignment al = HorizontalAlignment.Left) {
        Label l = Look.Caption(parent, text, size, weight, ink);
        l.CustomMinimumSize = new Vector2(width, 0);
        l.HorizontalAlignment = al;
    }
    private static Color GradeColor(string g) => g switch {
        "S" or "A" => Look.Accent, "B" => Look.Ink, "F" => Look.Crit, null or "" => Look.Lab, _ => Look.Warn,
    };
    public void Update(Mission m, bool anomOn) {
        for (int i = 0; i < _scriptTabs.Count; i++)
            Look.Tint(_scriptTabs[i], Scripts[i].Key == Script ? Look.Accent : Look.Ink2);
        for (int i = 0; i < _pick.Count; i++) {
            bool on = i < Mission.Keys.Length ? Mission.Keys[i] == m.Key : anomOn;
            Look.Tint(_pick[i], on ? (i < Mission.Keys.Length ? Look.Accent : Look.Warn) : Look.Ink2);
        }
        for (int i = 0; i < _goal.Count && i < m.Goals.Count; i++) {
            Goal g = m.Goals[i];
            (GoalDot dot, Label name, Label pts) = _goal[i];
            dot.Set(g.State);
            Look.Tint(name, g.State == Aim.Done ? Look.Ink : g.State == Aim.Fail ? Look.Crit : Look.Ink2);
            Look.Set(pts, g.State == Aim.Done ? $"+{g.Score}" : "");
            Look.Set(_note[i], g.Note);
            _note[i].Visible = g.Note.Length > 0 && g.State != Aim.Done;
        }
        Look.Set(_score, m.Total.ToString());
        Look.Set(_rest, $"из {m.MaxTotal}" + (m.Best > 0 ? $", рекорд {m.Best}" : ""));
        if (!m.Over || _shown) return;
        _shown = true;
        ShowFinal(m);
    }
    private void ShowFinal(Mission m) {
        foreach (Node n in _finalBody.GetChildren()) n.QueueFree();
        Look.Caption(_finalBody, "Полёт завершён", 22, 600, () => Look.Ink);
        Look.Caption(_finalBody, $"Оценка {m.Grade}, {m.Score} из {m.MaxTotal}", 30, 300,
                     () => m.Grade == "F" ? Look.Crit : Look.Ink);
        Look.Caption(_finalBody, m.Title, 14, 400, () => Look.Lab);
        _finalBody.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = Control.MouseFilterEnum.Ignore });
        foreach (Goal g in m.Goals) {
            Aim st = g.State;
            Label l = Look.Caption(_finalBody, g.Name + (st == Aim.Done ? $"   +{g.Score}" : "   0")
                                               + (g.Note.Length > 0 ? "   " + g.Note : ""), 14, 400,
                                   () => st == Aim.Done ? Look.Ink : st == Aim.Fail ? Look.Crit : Look.Lab);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(560, 0);
        }
        _finalBody.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = Control.MouseFilterEnum.Ignore });
        Look.Caption(_finalBody, m.NewBest ? $"Новый рекорд задания: {m.Best}" : $"Рекорд задания: {m.Best}", 14, 600,
                     () => m.NewBest ? Look.Accent : Look.Lab);
        Look.Caption(_finalBody, "R — тот же полёт заново, F6 — другое задание, F7 — отказы, F8 — рекорды", 13, 400,
                     () => Look.Lab);
        _final.Visible = true;
    }
}
