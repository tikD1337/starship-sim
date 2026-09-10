using Godot;
namespace Starship.Game;
public sealed class PadView {
    public const float DeckH = 16.0f, MouthZ = 29.0f, MouthY = -10.5f;
    private Node3D _root;
    public static PadView Build(ModelLibrary lib, Node parent) {
        var p = new PadView();
        p._root = new Node3D { Name = "Pad" };
        parent.AddChild(p._root);
        p._root.RotateY(Mathf.Pi / 2f);
        lib.Make("pad_apron", p._root);
        lib.Make("pad_berm", p._root);
        lib.Make("pad_deck", p._root);
        return p;
    }
    public void Update(double ox, double oy) {
        _root.Position = new Vector3(-(float)ox, -(float)oy, 0);
        _root.Visible = ox < 9000.0 && oy < 26000.0;
    }
}
