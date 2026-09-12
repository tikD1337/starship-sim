using System;
using System.Collections.Generic;
using Godot;
namespace Starship.Game.Ui;
public partial class Seg : PanelContainer {
    private readonly List<Button> _items = new();
    public int Selected { get; private set; } = -1;
    public event Action<int> Picked;
    public static Seg Make(Control parent, IReadOnlyList<string> names, int size = 15) {
        var s = new Seg { SizeFlagsHorizontal = SizeFlags.ShrinkBegin, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 2);
        s.AddChild(row);
        for (int i = 0; i < names.Count; i++) {
            int k = i;
            var b = new Button {
                Text = names[i], FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand,
            };
            b.AddThemeFontSizeOverride("font_size", size);
            b.Pressed += () => { s.Select(k); s.Picked?.Invoke(k); };
            row.AddChild(b);
            s._items.Add(b);
        }
        parent?.AddChild(s);
        Look.Bind(s, x => {
            x.AddThemeStyleboxOverride("panel", Look.Box(Look.Field, 10, 3, 3));
            x.Select(x.Selected);
        });
        return s;
    }
    public void Select(int i) {
        Selected = i;
        for (int k = 0; k < _items.Count; k++) Widgets.Chip(_items[k], k == i);
    }
}
