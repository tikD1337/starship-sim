using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
namespace Starship.Game.Ui;
public static class Look {
    private const string Cfg = "user://settings.cfg";
    private static readonly string[] Families = {
        "Segoe UI Variable Display", "Segoe UI", "SF Pro Display", ".AppleSystemUIFont", "Noto Sans", "DejaVu Sans",
    };
    private static readonly Dictionary<int, SystemFont> Fonts = new();
    private static readonly List<(WeakReference<Control> C, Action<Control> Paint)> Binds = new();
    private static readonly ConditionalWeakTable<Control, StrongBox<Color>> Inked = new();
    private static Theme _ui;
    public static int Current { get; private set; }
    public static event Action Changed;
    public static Palette P => Themes.All[Current];
    public static Color Bg => new(P.Bg);
    public static Color Panel => new(P.Panel);
    public static Color Field => new(P.Field);
    public static Color Line => new(P.Line);
    public static Color Ink => new(P.Ink);
    public static Color Ink2 => new(P.Ink2);
    public static Color Lab => new(P.Lab);
    public static Color Accent => new(P.Accent);
    public static Color AccentInk => new(P.AccentInk);
    public static Color Disc => new(P.Disc);
    public static Color DiscInk => new(P.DiscInk);
    public static Color DiscRing => new(P.DiscRing);
    public static Color Glow => new(P.Glow);
    public static Color Off => new(P.Off);
    public static Color Trace => new(P.Trace);
    public static Color[] Series => new[] { new Color(P.L1), new Color(P.L2), new Color(P.L3), new Color(P.L4) };
    public static readonly Color Warn = new(Themes.Warn), Crit = new(Themes.Crit);
    public static Font Font(int weight) {
        if (Fonts.TryGetValue(weight, out SystemFont f)) return f;
        f = new SystemFont { FontNames = Families, FontWeight = weight };
        Fonts[weight] = f;
        return f;
    }
    public static Theme UiTheme() => _ui ??= new Theme { DefaultFont = Font(400), DefaultFontSize = 14 };
    public static void Load() {
        var cf = new ConfigFile();
        if (cf.Load(Cfg) != Error.Ok) return;
        Current = Themes.Parse((string)cf.GetValue("ui", "theme", "a"), 0);
    }
    public static void SetTheme(int i, bool save) {
        Current = Math.Clamp(i, 0, Themes.All.Length - 1);
        if (save) {
            var cf = new ConfigFile();
            cf.Load(Cfg);
            cf.SetValue("ui", "theme", P.Key);
            cf.Save(Cfg);
        }
        Inked.Clear();
        for (int k = Binds.Count - 1; k >= 0; k--) {
            if (Binds[k].C.TryGetTarget(out Control c) && GodotObject.IsInstanceValid(c)) Binds[k].Paint(c);
            else Binds.RemoveAt(k);
        }
        Changed?.Invoke();
    }
    public static T Bind<T>(T c, Action<T> paint) where T : Control {
        paint(c);
        Binds.Add((new WeakReference<Control>(c), x => paint((T)x)));
        return c;
    }
    public static StyleBoxFlat Box(Color fill, int radius, int padX, int padY) {
        var sb = new StyleBoxFlat {
            BgColor = fill, AntiAliasing = true,
            ContentMarginLeft = padX, ContentMarginRight = padX, ContentMarginTop = padY, ContentMarginBottom = padY,
        };
        sb.SetCornerRadiusAll(radius);
        return sb;
    }
    public static Label Caption(Control parent, string text, int size, int weight, Func<Color> ink) {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", Font(weight));
        l.AddThemeFontSizeOverride("font_size", size);
        parent?.AddChild(l);
        if (ink != null) Bind(l, x => x.AddThemeColorOverride("font_color", ink()));
        return l;
    }
    public static void Set(Label l, string s) {
        if (l.Text != s) l.Text = s;
    }
    public static void Tint(Control c, Color col) {
        if (Inked.TryGetValue(c, out StrongBox<Color> b)) {
            if (b.Value == col) return;
            b.Value = col;
        }
        else Inked.Add(c, new StrongBox<Color>(col));
        c.AddThemeColorOverride("font_color", col);
    }
}
