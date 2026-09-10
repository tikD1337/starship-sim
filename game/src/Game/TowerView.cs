using System;
using Godot;
namespace Starship.Game;
public sealed class TowerView {
    public const float TowerX = 42.0f;
    private const float ArmLen = 44.0f, HingeX = 2.4f, HingeY = 6.9f;
    private Node3D _root, _carriage, _armL, _armR;
    public static TowerView Build(ModelLibrary lib, Node parent) {
        var t = new TowerView();
        t._root = new Node3D { Name = "Tower" };
        parent.AddChild(t._root);
        t._root.Position = new Vector3(-TowerX, 0, 0);
        lib.Make("tower", t._root);
        t._carriage = new Node3D { Name = "Carriage" };
        t._root.AddChild(t._carriage);
        lib.Make("carriage", t._carriage);
        t._armL = new Node3D { Name = "ArmL" };
        t._carriage.AddChild(t._armL);
        t._armL.Position = new Vector3(HingeX, 0, -HingeY);
        lib.Make("arm_b", t._armL);
        t._armR = new Node3D { Name = "ArmR" };
        t._carriage.AddChild(t._armR);
        t._armR.Position = new Vector3(HingeX, 0, HingeY);
        lib.Make("arm", t._armR);
        t.Set(0f, 0f, 62f);
        return t;
    }
    public void SetOrigin(double ox, double oy) {
        _root.Position = new Vector3(-TowerX - (float)ox, -(float)oy, 0);
    }
    private const float GripDeg = -3.1f, OpenDeg = 46f;
    public string Probe() {
        Node3D a = _armL;
        Vector3 tip = a.GlobalTransform * new Vector3(ArmLen, 0f, 0f);
        return $"armHinge={a.GlobalPosition.X:F1},{a.GlobalPosition.Y:F1},{a.GlobalPosition.Z:F1}"
             + $" armTip={tip.X:F1},{tip.Y:F1},{tip.Z:F1} towerX={_root.Position.X:F1}"
             + $" carY={_carriage.Position.Y:F1}";
    }
    public void Set(double open, double drop, double carriageY) {
        float ang = Mathf.DegToRad(Mathf.Lerp(GripDeg, OpenDeg, (float)open));
        _armL.Rotation = new Vector3(0, ang, 0);
        _armR.Rotation = new Vector3(0, -ang, 0);
        _carriage.Position = new Vector3(0, (float)(carriageY - drop), 0);
    }
}
