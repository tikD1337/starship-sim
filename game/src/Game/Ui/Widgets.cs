using Godot;
namespace Starship.Game.Ui;
public static class Widgets {
    private static readonly string[] BoxStates = { "normal", "hover", "pressed", "hover_pressed", "disabled" };
    private static readonly string[] InkStates = {
        "font_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color", "font_disabled_color",
    };
    public static (PanelContainer Box, VBoxContainer Body, Label Small) Section(Control parent, string title,
                                                                                 string small = null) {
        var box = new PanelContainer();
        Look.Bind(box, p => p.AddThemeStyleboxOverride("panel", Look.Box(Look.Panel, 16, 22, 18)));
        parent.AddChild(box);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 7);
        box.AddChild(body);
        Label sm = null;
        if (title != null) {
            var head = new HBoxContainer { CustomMinimumSize = new Vector2(0, 24) };
            head.AddThemeConstantOverride("separation", 10);
            body.AddChild(head);
            Look.Caption(head, title, 18, 600, () => Look.Ink).VerticalAlignment = VerticalAlignment.Bottom;
            sm = Look.Caption(head, small ?? "", 14, 400, () => Look.Lab);
            sm.VerticalAlignment = VerticalAlignment.Bottom;
            sm.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            sm.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 5), MouseFilter = Control.MouseFilterEnum.Ignore });
        }
        return (box, body, sm);
    }
    public static void Chip(Button b, bool on) {
        StyleBoxFlat sb = Look.Box(on ? Look.Accent : Colors.Transparent, 8, 14, 6);
        foreach (string st in BoxStates) b.AddThemeStyleboxOverride(st, sb);
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        Color ink = on ? Look.AccentInk : Look.Ink2;
        foreach (string st in InkStates) b.AddThemeColorOverride(st, ink);
        b.AddThemeColorOverride("font_hover_color", on ? ink : Look.Ink);
        b.AddThemeFontOverride("font", Look.Font(on ? 600 : 400));
    }
    public static Button Pill(Control parent, string text, int size = 14, bool raised = false) {
        var b = new Button {
            Text = text, FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        b.AddThemeFontSizeOverride("font_size", size);
        Look.Bind(b, x => {
            Color fill = raised ? Look.Field : Look.Panel, hover = raised ? Look.Line : Look.Field;
            foreach (string st in BoxStates)
                x.AddThemeStyleboxOverride(st, Look.Box(st == "normal" || st == "disabled" ? fill : hover, 999, 14, 6));
            x.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            foreach (string st in InkStates) x.AddThemeColorOverride(st, Look.Ink2);
            x.AddThemeColorOverride("font_hover_color", Look.Ink);
        });
        parent?.AddChild(b);
        return b;
    }
    public static Label Tag(Control parent, string text, string key = null) {
        var p = new PanelContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        Look.Bind(p, x => x.AddThemeStyleboxOverride("panel", Look.Box(Look.Panel, 999, 14, 6)));
        parent.AddChild(p);
        var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        h.AddThemeConstantOverride("separation", 6);
        p.AddChild(h);
        if (key != null) Look.Caption(h, key, 14, 600, () => Look.Ink);
        return Look.Caption(h, text, 14, 400, () => Look.Ink2);
    }
}
public sealed class KvRow {
    public Label Key, Val, Unit;
    public static KvRow Make(Control parent, string key) {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 0);
        parent.AddChild(h);
        var r = new KvRow {
            Key = Look.Caption(h, key, 14, 400, () => Look.Lab),
            Val = Look.Caption(h, "—", 17, 500, null),
            Unit = Look.Caption(h, "", 13, 400, () => Look.Lab),
        };
        r.Key.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        r.Key.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        foreach (Label l in new[] { r.Key, r.Val, r.Unit }) l.VerticalAlignment = VerticalAlignment.Bottom;
        return r;
    }
    public void Set(string v, string unit = "", Color? ink = null) {
        Look.Set(Val, v);
        Look.Set(Unit, unit.Length > 0 ? " " + unit : "");
        Look.Tint(Val, ink ?? Look.Ink);
    }
}
