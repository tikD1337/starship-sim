using System;
using System.Collections.Generic;
using Godot;
using Starship.Physics;
namespace Starship.Game;
public sealed class BayView {
    private Node3D _door;
    private readonly List<Node3D> _packs = new();
    private const float DoorAzim = 126.4f, DoorR = 4.40f, DoorZ = 34.31f, DoorLift = 1.05f,
        PackAzim = 179.7f, PackOuter = 4.07f, PackHalf = 0.455f, Z0 = 21.6f, Z1 = 35.2f;
    private const int PackCount = 10;
    public static BayView Build(ModelLibrary lib, Node3D shipRoot) {
        var v = new BayView();
        float aH = Mathf.DegToRad(DoorAzim);
        v._door = new Node3D { Name = "BayDoor" };
        shipRoot.AddChild(v._door);
        v._door.Rotation = new Vector3(0, aH, 0);
        v._door.Position = new Vector3(Mathf.Cos(aH) * DoorR, DoorZ, -Mathf.Sin(aH) * DoorR);
        lib.Make("bay_door", v._door, required: false);
        float aC = Mathf.DegToRad(PackAzim);
        float rP = PackOuter - PackHalf;
        for (int k = 0; k < PackCount; k++) {
            float z = Mathf.Lerp(Z0 + 1.0f, Z1 - 1.0f, k / (float)(PackCount - 1));
            var holder = new Node3D { Name = $"BayPack{k}" };
            shipRoot.AddChild(holder);
            holder.Position = new Vector3(Mathf.Cos(aC) * rP, z, -Mathf.Sin(aC) * rP);
            holder.RotateY(aC);
            MeshInstance3D mi = lib.Make("bay_pack", holder);
            if (mi != null) mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            v._packs.Add(holder);
        }
        return v;
    }
    public Vector3 DoorNormal => _door.GlobalBasis.X.Normalized();
    public Vector3 DoorPos => _door.GlobalPosition;
    public int Shown { get; private set; }
    public void Update(SimState sim, Vehicle ship) {
        Bay bay = ship.BayS;
        float open = bay == null ? 0f : (float)bay.Open;
        float aH = Mathf.DegToRad(DoorAzim);
        _door.Position = new Vector3(Mathf.Cos(aH) * DoorR, DoorZ + DoorLift * open,
                                     -Mathf.Sin(aH) * DoorR);
        int total = Math.Max(1, (int)Math.Round(sim.Payload / Physics.Sim.SAT_MASS));
        int left = bay?.Sats ?? 0;
        int shown = (int)Math.Ceiling(left / (double)total * PackCount);
        Shown = shown;
        for (int k = 0; k < _packs.Count; k++)
            _packs[k].Visible = k < shown;
    }
}
