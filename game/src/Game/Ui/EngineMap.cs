using System;
using Godot;
using Starship.Physics;
using PEngine = Starship.Physics.Engine;
namespace Starship.Game.Ui;
public partial class EngineMap : Control {
    public bool Mini;
    public int Selected = -1;
    public event Action<int> Picked;
    private Vehicle _v;
    private Spot[] _spots = Array.Empty<Spot>();
    private double _drawn = -99;
    public void Feed(Vehicle v, double now) {
        if (_v != v || _spots.Length == 0) _spots = EngineLayout.Of(v.Kind == Kind.Ship);
        _v = v;
        if (now - _drawn < 0.1) return;
        _drawn = now;
        QueueRedraw();
    }
    private float Fit => Mini ? Size.Y / 412f : Mathf.Min(Size.X / 470f, Size.Y / 412f);
    public override void _GuiInput(InputEvent e) {
        if (Mini || e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
        Vector2 p = (mb.Position - Size * 0.5f) / Fit;
        int i = EngineLayout.Hit(_spots, p.X, p.Y);
        if (i < 0) return;
        Selected = i;
        Picked?.Invoke(i);
        QueueRedraw();
        AcceptEvent();
    }
    public override void _Draw() {
        if (_v == null) return;
        float k = Fit;
        Vector2 c = Size * 0.5f;
        Font num = Look.Font(400), val = Look.Font(600);
        for (int i = 0; i < _spots.Length && i < _v.Eng.Count; i++) {
            Spot s = _spots[i];
            Vector2 p = c + new Vector2((float)s.X, (float)s.Y) * k;
            float r = (float)s.R * k;
            PEngine e = _v.Eng[i];
            EngLook st = EngineState.Of(e);
            bool lit = st is EngLook.On or EngLook.Warn or EngLook.Crit;
            if (lit && !Mini) {
                Color g = Look.Glow;
                for (int j = 4; j >= 1; j--) DrawCircle(p, r + 2 * j, new Color(g, g.A * 0.35f), true, -1, true);
            }
            if (lit) {
                DrawCircle(p, r - 1, Mini ? Look.Ink : Look.Disc, true, -1, true);
                Color ring = st == EngLook.Warn ? Look.Warn : st == EngLook.Crit ? Look.Crit : Look.DiscRing;
                if (!Mini) DrawCircle(p, r - 1, new Color(ring, st == EngLook.On ? 0.55f : 1f), false, 1.2f, true);
            }
            else DrawCircle(p, r - 1, st == EngLook.Failed ? Look.Crit : Mini ? Look.Lab : Look.Off, false, 1.5f, true);
            if (Mini) continue;
            if (i == Selected) DrawCircle(p, r + 6, Look.Accent, false, 3f, true);
            Color ink = lit ? Look.DiscInk : st == EngLook.Failed ? Look.Crit : Look.Off;
            string id = e.Name[(e.Name.IndexOf('-') + 1)..];
            Centered(num, id, p + new Vector2(0, -r * 0.2f), (int)(r * 0.4f), new Color(ink, 0.65f));
            Centered(val, lit ? NumFmt.F(e.Pc, 1) : st == EngLook.Failed ? "отказ" : "—",
                     p + new Vector2(0, r * 0.44f), (int)(r * 0.58f), ink);
        }
    }
    private void Centered(Font f, string s, Vector2 at, int size, Color c) {
        float w = f.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;
        DrawString(f, new Vector2(at.X - w * 0.5f, at.Y), s, HorizontalAlignment.Left, -1, size, c);
    }
}
