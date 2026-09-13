using System;
using Godot;
using Starship.Game.Ui;
using Starship.Physics;
namespace Starship.Game;
public sealed class TowerView {
    public const float TowerX = 42.0f;
    private const float TopY = 128.5f, CableTop = TopY + 0.4f, CableLug = 2.7f;
    private static readonly float HingeX = (float)(TowerX - ArmGeom.Reach), HingeZ = (float)ArmGeom.HingeZ;
    private static readonly Vector3 RamArm = new(5.0f, -3.2f, -1.2f), RamBase = new(7.2f, -3.2f, -10.13f);
    private static readonly Vector2[] Cables = { new(7.9f, -3.9f), new(7.9f, -3.1f), new(7.9f, 3.1f), new(7.9f, 3.9f) };
    private Node3D _root, _carriage;
    private readonly Node3D[] _arm = new Node3D[2], _cart = new Node3D[2], _barrel = new Node3D[2], _rod = new Node3D[2];
    private readonly Node3D[] _cable = new Node3D[Cables.Length];
    public static TowerView Build(Node parent) {
        ModelLibrary lib = ModelLibrary.Load("res://assets/tower.glb");
        var t = new TowerView();
        t._root = Node(parent, "Tower", new Vector3(-TowerX, 0, 0));
        lib.Make("tower2", t._root);
        t._carriage = Node(t._root, "Carriage", Vector3.Zero);
        lib.Make("carriage2", t._carriage);
        for (int i = 0; i < 2; i++) {
            float side = i == 0 ? -1 : 1;
            t._arm[i] = Node(t._carriage, i == 0 ? "ArmL" : "ArmR", new Vector3(HingeX, 0, side * HingeZ));
            lib.Make(i == 0 ? "arm2_l" : "arm2_r", t._arm[i]);
            t._cart[i] = Node(t._arm[i], "Cart", Vector3.Zero);
            lib.Make("arm2_cart", t._cart[i]);
            t._barrel[i] = Node(t._carriage, "Ram", Vector3.Zero);
            lib.Make("ram2_barrel", t._barrel[i]);
            t._rod[i] = Node(t._carriage, "Rod", Vector3.Zero);
            lib.Make("ram2_rod", t._rod[i]);
        }
        for (int i = 0; i < Cables.Length; i++) {
            t._cable[i] = Node(t._root, "Cable", Vector3.Zero);
            lib.Make("cable2", t._cable[i]);
        }
        t.Set(ArmGeom.Park, 0, Const.ARM_PARK);
        return t;
    }
    private static Node3D Node(Node parent, string name, Vector3 at) {
        var n = new Node3D { Name = name, Position = at };
        parent.AddChild(n);
        return n;
    }
    public void SetOrigin(double ox, double oy) {
        _root.Position = new Vector3(-TowerX - (float)ox, -(float)oy, 0);
    }
    public string Probe() {
        Node3D a = _arm[0];
        Vector3 tip = a.GlobalTransform * new Vector3((float)ArmGeom.Length, 0f, 0f);
        return $"armHinge={a.GlobalPosition.X:F1},{a.GlobalPosition.Y:F1},{a.GlobalPosition.Z:F1}"
             + $" armTip={tip.X:F1},{tip.Y:F1},{tip.Z:F1} towerX={_root.Position.X:F1}"
             + $" carY={_carriage.Position.Y:F1} armDeg={Mathf.RadToDeg(a.Rotation.Y):F1}";
    }
    public void Set(double gap, double drop, double carriageY) {
        float ang = (float)ArmGeom.Angle(gap);
        float cy = (float)(carriageY - drop);
        _carriage.Position = new Vector3(0, cy, 0);
        float along = (float)(ArmGeom.Reach * Math.Cos(ang) - ArmGeom.HingeZ * Math.Sin(ang));
        for (int i = 0; i < 2; i++) {
            float side = i == 0 ? -1 : 1;
            float phi = -side * ang;
            _arm[i].Rotation = new Vector3(0, phi, 0);
            _cart[i].Position = new Vector3(along, 0, -side * (float)ArmGeom.Inset);
            Vector3 lug = _arm[i].Position + new Basis(Vector3.Up, phi) * new Vector3(RamArm.X, RamArm.Y, -side * RamArm.Z);
            Vector3 bas = new(RamBase.X, RamBase.Y, side * -RamBase.Z);
            Vector3 d = lug - bas;
            float len = d.Length();
            float yaw = Mathf.Atan2(-d.Z, d.X);
            _barrel[i].Position = bas;
            _barrel[i].Rotation = new Vector3(0, yaw, 0);
            _rod[i].Position = bas + d / Math.Max(len, 0.01f) * 0.4f;
            _rod[i].Rotation = new Vector3(0, yaw, 0);
            _rod[i].Scale = new Vector3(Math.Max(len - 0.4f, 0.05f), 1, 1);
        }
        for (int i = 0; i < Cables.Length; i++) {
            float top = cy + CableLug;
            _cable[i].Position = new Vector3(Cables[i].X, top, Cables[i].Y);
            _cable[i].Scale = new Vector3(1, Math.Max(CableTop - top, 0.05f), 1);
        }
    }
}
