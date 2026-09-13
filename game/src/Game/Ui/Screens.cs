using System.Collections.Generic;
using Godot;
namespace Starship.Game.Ui;
public sealed class Screens {
    private static readonly (string Head, (string Key, string Text)[] Rows)[] HelpLeft = {
        ("Экраны", new[] {
            ("1", "сводка"), ("2", "эфир, ещё раз — следующая камера"), ("3", "свободная камера: WASD, Q и E, Shift"),
            ("Tab", "следующий экран: сводка, эфир, пульт"), ("V", "другая ступень"), ("мышь", "поворот камеры, колесо — приближение"),
        }),
        ("Пульт", new[] { ("F3 F4 F5", "телеметрия, развёртка, коридор входа"), ("F1", "эта карточка") }),
        ("Время", new[] { ("пробел", "пауза"), ("[  ]", "медленнее и быстрее вдвое"), ("Esc", "выход") }),
    };
    private static readonly (string Head, (string Key, string Text)[] Rows)[] HelpRight = {
        ("Задание", new[] {
            ("F6", "другое задание"), ("F7", "отказы включить или выключить"), ("F8", "рекорды"),
            ("F9 F10", "записать прогон, повторить записанный"), ("F11", "разбор полёта"),
            ("R", "полёт заново с тем же зерном"),
        }),
        ("Управление", new[] {
            ("↑ ↓", "тяга"), ("← →", "тангаж"), (",  .", "крен"), ("T", "двигатели ориентации"),
            ("S, Enter", "разделение ступеней"), ("0", "вернуть управление автомату"),
            ("B N M", "створка отсека, один Starlink, десять"),
        }),
    };
    public SubViewport World { get; private set; }
    public int Cur { get; private set; } = 1;
    public Control EngineerRoot => _eng;
    public Control AirRoot => _air;
    public Control HudRoot => _hud;
    private SubViewportContainer _view;
    private Control _eng, _air, _hud, _over;
    private PanelContainer _keys;
    public static Screens Build(Node parent) {
        var s = new Screens();
        var l0 = new CanvasLayer { Layer = 0 };
        parent.AddChild(l0);
        s._view = new SubViewportContainer { Stretch = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        l0.AddChild(s._view);
        s._view.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        s.World = new SubViewport {
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always, HandleInputLocally = false,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        s._view.AddChild(s.World);
        var l1 = new CanvasLayer { Layer = 1 };
        parent.AddChild(l1);
        s._eng = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Look.UiTheme() };
        l1.AddChild(s._eng);
        s._eng.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        s._air = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Look.UiTheme() };
        l1.AddChild(s._air);
        s._air.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        s._hud = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Look.UiTheme() };
        l1.AddChild(s._hud);
        s._hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var l2 = new CanvasLayer { Layer = 2 };
        parent.AddChild(l2);
        s._over = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Look.UiTheme() };
        l2.AddChild(s._over);
        s._over.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        s.BuildHelp();
        s.Show(1);
        return s;
    }
    private void BuildHelp() {
        _keys = AddOverlay();
        var body = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        body.AddThemeConstantOverride("separation", 14);
        _keys.AddChild(body);
        Look.Caption(body, "Клавиши", 22, 600, () => Look.Ink);
        var cols = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        cols.AddThemeConstantOverride("separation", 56);
        body.AddChild(cols);
        foreach (var side in new[] { HelpLeft, HelpRight }) {
            var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            col.AddThemeConstantOverride("separation", 6);
            cols.AddChild(col);
            foreach ((string head, (string Key, string Text)[] rows) in side) {
                Look.Caption(col, head, 15, 600, () => Look.Lab);
                var grid = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Ignore };
                grid.AddThemeConstantOverride("h_separation", 18);
                grid.AddThemeConstantOverride("v_separation", 5);
                col.AddChild(grid);
                foreach ((string key, string text) in rows) {
                    Look.Caption(grid, key, 15, 600, () => Look.Ink).CustomMinimumSize = new Vector2(84, 0);
                    Look.Caption(grid, text, 15, 400, () => Look.Ink2);
                }
                col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = Control.MouseFilterEnum.Ignore });
            }
        }
        Look.Caption(body, "F1 — закрыть", 14, 400, () => Look.Lab);
    }
    public void Show(int n) {
        n = Mathf.Clamp(n, 1, 3);
        Cur = n;
        _eng.Visible = n == 3;
        _air.Visible = n == 1;
        _hud.Visible = n == 2;
        _view.Visible = n == 2;
        World.RenderTargetUpdateMode = n == 2 ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
    }
    public void NextScreen() => Show(Tabs.Next(Cur));
    public void ToggleKeys() => _keys.Visible = !_keys.Visible;
    public PanelContainer AddOverlay() {
        var p = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        Look.Bind(p, x => x.AddThemeStyleboxOverride("panel", Look.Box(new Color(Look.Panel, 0.97f), 16, 30, 26)));
        _over.AddChild(p);
        p.SetAnchorsPreset(Control.LayoutPreset.Center);
        p.GrowHorizontal = Control.GrowDirection.Both;
        p.GrowVertical = Control.GrowDirection.Both;
        return p;
    }
    public void AddBottomStrip(Control c, float h) {
        c.MouseFilter = Control.MouseFilterEnum.Ignore;
        _over.AddChild(c);
        c.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        c.OffsetLeft = 0;
        c.OffsetRight = 0;
        c.OffsetTop = -h;
        c.OffsetBottom = 0;
    }
    public List<string> MouseGrabs() {
        var bad = new List<string>();
        if (_eng.Visible && Cur != 3) bad.Add("пульт виден поверх 3D-вида");
        if (_air.Visible && Cur != 1) bad.Add("сводка видна поверх 3D-вида");
        if (_hud.Visible && Cur != 2) bad.Add("худ борта виден не на своём экране");
        Walk(_over, bad);
        return bad;
    }
    private static void Walk(Node n, List<string> bad) {
        foreach (Node ch in n.GetChildren()) {
            if (ch is CanvasItem ci && !ci.Visible) continue;
            if (ch is Control c && c.MouseFilter == Control.MouseFilterEnum.Stop)
                bad.Add($"{c.GetType().Name} «{c.Name}» поверх обзора");
            Walk(ch, bad);
        }
    }
}
