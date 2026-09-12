using Godot;
namespace Starship.Game.Ui;
public partial class GoalDot : Control {
    private Aim _state = Aim.Wait;
    public void Set(Aim s) {
        if (s == _state) return;
        _state = s;
        QueueRedraw();
    }
    public override void _Draw() {
        Vector2 c = Size * 0.5f;
        if (_state == Aim.Done) DrawCircle(c, 6f, Look.Accent, true, -1, true);
        else if (_state == Aim.Fail) DrawCircle(c, 6f, Look.Crit, true, -1, true);
        else DrawCircle(c, 5.25f, Look.Lab, false, 1.5f, true);
    }
}
