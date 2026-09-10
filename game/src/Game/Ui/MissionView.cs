using System;
using System.Collections.Generic;
using Godot;
using Starship.Physics;
namespace Starship.Game.Ui;
public sealed class MissionView {
    private VBoxContainer _card;
    private Label _title, _sum;
    private VBoxContainer _rows;
    private readonly List<Label> _goal = new(), _note = new();
    private readonly List<Button> _pick = new();
    private PanelContainer _final, _recs;
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
    private readonly List<Button> _scriptTabs = new();
    public static MissionView Build(ConsoleFull ui, Action<string> onPick, Action onAnom,
                                    Action<string> onScript) {
        var v = new MissionView();
        v._card = ui.AddLeftCard("ЗАДАНИЕ", 0, 1);
        v._title = Lab(v._card, "", ConsoleFull.Val, 11);
        v._title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var tabs = new HFlowContainer();
        tabs.AddThemeConstantOverride("h_separation", 3);
        tabs.AddThemeConstantOverride("v_separation", 3);
        v._card.AddChild(tabs);
        foreach (string key in Mission.Keys) {
            string k = key;
            Button b = ConsoleFull.Tab(tabs, ShortOf(k));
            b.Pressed += () => onPick(k);
            v._pick.Add(b);
        }
        Button an = ConsoleFull.Tab(tabs, "отказы");
        an.Pressed += () => onAnom();
        v._pick.Add(an);
        Lab(v._card, "нештатная ситуация", ConsoleFull.Dim, 9);
        var sc = new HFlowContainer();
        sc.AddThemeConstantOverride("h_separation", 3);
        sc.AddThemeConstantOverride("v_separation", 3);
        v._card.AddChild(sc);
        foreach ((string key, string name) in Scripts) {
            string kk = key;
            Button b = ConsoleFull.Tab(sc, name);
            b.Pressed += () => onScript(kk);
            v._scriptTabs.Add(b);
        }
        v._rows = new VBoxContainer();
        v._rows.AddThemeConstantOverride("separation", 1);
        v._card.AddChild(v._rows);
        v._sum = Lab(v._card, "", ConsoleFull.Dim, 10);
        v._recs = ui.AddOverlay();
        v._recsBody = new VBoxContainer();
        v._recsBody.AddThemeConstantOverride("separation", 3);
        v._recsBody.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        v._recs.AddChild(v._recsBody);
        v._final = ui.AddOverlay();
        v._finalBody = new VBoxContainer();
        v._finalBody.AddThemeConstantOverride("separation", 4);
        v._finalBody.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        v._final.AddChild(v._finalBody);
        return v;
    }
    private static string ShortOf(string key) => key switch {
        "trans" => "трансатм.",
        "high" => "520 км",
        _ => "220 км",
    };
    private static Label Lab(Control parent, string text, Color c, int size) {
        var l = new Label { Text = text };
        l.AddThemeColorOverride("font_color", c);
        l.AddThemeFontSizeOverride("font_size", size);
        parent.AddChild(l);
        return l;
    }
    public void Reset(Mission m) {
        foreach (Node n in _rows.GetChildren()) n.QueueFree();
        _goal.Clear();
        _note.Clear();
        _shown = false;
        _final.Visible = false;
        foreach (Goal g in m.Goals) {
            _goal.Add(Lab(_rows, "· " + g.Name, ConsoleFull.Dim, 11));
            Label n = Lab(_rows, "", ConsoleFull.Dim, 9);
            n.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _note.Add(n);
        }
        _title.Text = m.Title;
    }
    private static Color ColorOf(Aim st) => st switch {
        Aim.Done => ConsoleFull.Good, Aim.Fail => ConsoleFull.Bad, _ => ConsoleFull.Dim,
    };
    private static string MarkOf(Aim st) => st switch {
        Aim.Done => "+ ",
        Aim.Fail => "x ",
        _ => "· ",
    };
    public string Script;
    public bool RecordsShown => _recs.Visible;
    public void HideFinal() => _final.Visible = false;
    public void ShowFinalAgain() { if (_shown) _final.Visible = true; }
    public void ToggleRecords(string key) {
        if (_recs.Visible) { _recs.Visible = false; ShowFinalAgain(); return; }
        _final.Visible = false;
        foreach (Node n in _recsBody.GetChildren()) n.QueueFree();
        Lab(_recsBody, "ТАБЛИЦА РЕКОРДОВ", ConsoleFull.Accent, 20);
        Lab(_recsBody, "лучший результат для каждой пары «задание — нештатная ситуация»",
            ConsoleFull.Dim, 10);
        Lab(_recsBody, "", ConsoleFull.Dim, 4);
        foreach (string mk in Mission.Keys) {
            Lab(_recsBody, Mission.NameOf(mk), mk == key ? ConsoleFull.Val : ConsoleFull.Dim, 13);
            var grid = new GridContainer { Columns = 4 };
            grid.AddThemeConstantOverride("h_separation", 18);
            grid.AddThemeConstantOverride("v_separation", 2);
            _recsBody.AddChild(grid);
            Row(grid, "без отказов", Mission.RecordOf(Mission.RecKeyOf(mk, false, null)));
            foreach ((string sk, string name) in Scripts)
                Row(grid, name, Mission.RecordOf(Mission.RecKeyOf(mk, true, sk)));
            Lab(_recsBody, "", ConsoleFull.Dim, 4);
        }
        Lab(_recsBody, "«без отказов» — штатный полёт, «случайно» — набор неполадок наугад",
            ConsoleFull.Dim, 10);
        Lab(_recsBody, "F8 — закрыть", ConsoleFull.Accent, 12);
        _recs.Visible = true;
    }
    private static void Row(Control grid, string what, Mission.Rec r) {
        Cell(grid, what, ConsoleFull.Dim, 11, 130);
        Cell(grid, r.Score > 0 ? r.Score.ToString() : "—",
             r.Score > 0 ? ConsoleFull.Val : ConsoleFull.Dim, 11, 46, HorizontalAlignment.Right);
        Cell(grid, r.Score > 0 && !string.IsNullOrEmpty(r.Grade) ? r.Grade : "",
             GradeColor(r.Grade), 11, 20, HorizontalAlignment.Center);
        Cell(grid, r.When ?? "", ConsoleFull.Dim, 10, 88);
    }
    private static Label Cell(Control parent, string text, Color c, int size, int width,
                              HorizontalAlignment al = HorizontalAlignment.Left) {
        Label l = Lab(parent, text, c, size);
        l.CustomMinimumSize = new Vector2(width, 0);
        l.HorizontalAlignment = al;
        return l;
    }
    private static Color GradeColor(string g) => g switch {
        "S" => ConsoleFull.Good, "A" => ConsoleFull.Good, "B" => ConsoleFull.Val,
        "F" => ConsoleFull.Bad, null or "" => ConsoleFull.Dim, _ => ConsoleFull.Warn,
    };
    public void Update(Mission m, bool anomOn) {
        for (int i = 0; i < _scriptTabs.Count; i++)
            ConsoleFull.Tint(_scriptTabs[i], Scripts[i].Key == Script ? ConsoleFull.Accent : ConsoleFull.Dim);
        for (int i = 0; i < _pick.Count; i++) {
            bool on = i < Mission.Keys.Length ? Mission.Keys[i] == m.Key : anomOn;
            ConsoleFull.Tint(_pick[i],
                on ? (i < Mission.Keys.Length ? ConsoleFull.Accent : ConsoleFull.Warn) : ConsoleFull.Dim);
        }
        for (int i = 0; i < _goal.Count && i < m.Goals.Count; i++) {
            Goal g = m.Goals[i];
            ConsoleFull.Write(_goal[i], MarkOf(g.State) + g.Name +
                              (g.State == Aim.Done ? $"   +{g.Score}" : ""));
            ConsoleFull.Tint(_goal[i], ColorOf(g.State));
            ConsoleFull.Write(_note[i], g.Note.Length > 0 ? "    " + g.Note : "");
        }
        ConsoleFull.Write(_sum, $"очки {m.Total} из {m.MaxTotal}"
                                + (m.Best > 0 ? $"   ·   рекорд {m.Best}" : ""));
        if (!m.Over || _shown) return;
        _shown = true;
        ShowFinal(m);
    }
    private void ShowFinal(Mission m) {
        foreach (Node n in _finalBody.GetChildren()) n.QueueFree();
        Lab(_finalBody, "ПОЛЁТ ЗАВЕРШЁН", ConsoleFull.Accent, 20);
        Label grade = Lab(_finalBody, $"оценка {m.Grade}   ·   {m.Score} из {m.MaxTotal}",
                          m.Grade == "F" ? ConsoleFull.Bad : ConsoleFull.Val, 28);
        grade.HorizontalAlignment = HorizontalAlignment.Center;
        Lab(_finalBody, m.Title, ConsoleFull.Dim, 12);
        Lab(_finalBody, "", ConsoleFull.Dim, 6);
        foreach (Goal g in m.Goals) {
            Label l = Lab(_finalBody, MarkOf(g.State) + g.Name +
                          (g.State == Aim.Done ? $"   +{g.Score}" : "   0") +
                          (g.Note.Length > 0 ? "   " + g.Note : ""), ColorOf(g.State), 13);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }
        Lab(_finalBody, "", ConsoleFull.Dim, 6);
        Lab(_finalBody, m.NewBest ? $"новый рекорд задания: {m.Best}" : $"рекорд задания: {m.Best}",
            m.NewBest ? ConsoleFull.Good : ConsoleFull.Dim, 13);
        Lab(_finalBody, "R — тот же полёт заново   ·   F6 — другое задание   ·   F7 — отказы"
            + "   ·   F8 — рекорды", ConsoleFull.Accent, 12);
        _final.Visible = true;
    }
}
