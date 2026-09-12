using Godot;
namespace Starship.Game.Ui;
public partial class ThemeDot : Control {
    public int Index;
    public static ThemeDot Make(Control parent, int index) {
        var d = new ThemeDot {
            Index = index, CustomMinimumSize = new Vector2(28, 28), SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = Themes.All[index].Name,
        };
        parent.AddChild(d);
        Look.Bind(d, x => x.QueueRedraw());
        return d;
    }
    public override void _GuiInput(InputEvent e) {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
        Look.SetTheme(Index, true);
        AcceptEvent();
    }
    public override void _Draw() {
        Palette p = Themes.All[Index];
        Vector2 c = Size * 0.5f;
        const float r = 10f;
        DrawCircle(c, r, new Color(p.Panel), true, -1, true);
        var half = new Vector2[19];
        for (int i = 0; i < half.Length; i++) {
            float a = Mathf.Pi * (-0.25f + i / (float)(half.Length - 1));
            half[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        DrawColoredPolygon(half, new Color(p.Accent));
        DrawCircle(c, r - 0.5f, new Color(1, 1, 1, 0.18f), false, 1f, true);
        if (Index == Look.Current) DrawCircle(c, r + 4, Look.Ink, false, 2f, true);
    }
}
