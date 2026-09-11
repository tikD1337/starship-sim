using System;
using Godot;
namespace Starship.Game.Ui;
public partial class ZoneGauge : Control {
    private ParamRow _row;
    private double _x = double.NaN;
    private StyleBoxFlat _track, _warn, _crit, _mark;
    public static ZoneGauge Make(Control parent, ParamRow row) {
        var g = new ZoneGauge {
            _row = row, CustomMinimumSize = new Vector2(0, 16),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        parent.AddChild(g);
        Look.Bind(g, x => x.Paint());
        return g;
    }
    private void Paint() {
        _track = Look.Box(Look.Trace, 3, 0, 0);
        _warn = Look.Box(new Color(Look.Warn, 0.6f), 0, 0, 0);
        _crit = Look.Box(new Color(Look.Crit, 0.75f), 0, 0, 0);
        if (_row.LowIsBad) { _crit.CornerRadiusTopLeft = 3; _crit.CornerRadiusBottomLeft = 3; }
        else { _crit.CornerRadiusTopRight = 3; _crit.CornerRadiusBottomRight = 3; }
        _mark = Look.Box(Look.Ink, 2, 0, 0);
        QueueRedraw();
    }
    public void Set(double x) {
        if (!double.IsNaN(_x) && Math.Abs(x - _x) < _row.Max * 0.002) return;
        _x = x;
        QueueRedraw();
    }
    public override void _Draw() {
        if (_track == null) return;
        float w = Size.X, y = Size.Y * 0.5f - 3;
        var (w0, w1, c0, c1, mark) = ParamDefs.Bands(_row, double.IsNaN(_x) ? 0 : _x);
        DrawStyleBox(_track, new Rect2(0, y, w, 6));
        if (w1 > w0) DrawStyleBox(_warn, new Rect2(w * (float)w0, y, w * (float)(w1 - w0), 6));
        if (c1 > c0) DrawStyleBox(_crit, new Rect2(w * (float)c0, y, w * (float)(c1 - c0), 6));
        DrawStyleBox(_mark, new Rect2(w * (float)mark - 2, y - 5, 4, 16));
    }
}
