using Godot;
namespace Starship.Game.Ui;
public partial class Swatch : Control {
    public int Series;
    public override void _Draw() {
        Color c = Look.Series[Series];
        float y = Size.Y * 0.5f;
        if (Series == 2)
            for (float x = 0; x < 20; x += 10) DrawLine(new Vector2(x, y), new Vector2(Mathf.Min(x + 6, 20), y), c, 2);
        else if (Series == 3)
            for (float x = 0; x < 20; x += 6) DrawLine(new Vector2(x, y), new Vector2(x + 2, y), c, 3);
        else DrawLine(new Vector2(0, y), new Vector2(20, y), c, 2);
    }
}
