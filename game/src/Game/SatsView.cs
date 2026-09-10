using System;
using System.Collections.Generic;
using Godot;
using Starship.Physics;
namespace Starship.Game;
public sealed class SatsView {
    private ModelLibrary _lib;
    private Node3D _root;
    private readonly List<Node3D> _pool = new();
    private const float Spread = 14f, Clearance = 16f, Tilt = 0.08f;
    public static SatsView Build(ModelLibrary lib, Node parent) {
        var v = new SatsView { _lib = lib };
        v._root = new Node3D { Name = "Starlink" };
        parent.AddChild(v._root);
        return v;
    }
    private Node3D Grow() {
        var holder = new Node3D();
        _root.AddChild(holder);
        MeshInstance3D mi = _lib.Make("sat", holder);
        if (mi != null) mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _pool.Add(holder);
        return holder;
    }
    public void Update(SimState sim, Vehicle ship, double ox, double oy) {
        Bay bay = ship.BayS;
        int n = bay?.Out.Count ?? 0;
        while (_pool.Count < n) Grow();
        double pad = SimState.PadAngle(sim.T);
        for (int i = 0; i < _pool.Count; i++) {
            if (i >= n) { _pool[i].Visible = false; continue; }
            Sat s = bay.Out[i];
            double r = Math.Sqrt(s.X * s.X + s.Y * s.Y);
            double dr = (pad - Math.Atan2(s.X, s.Y)) * Const.RE;
            float lane = Clearance + (i % 5) * Spread;
            float depth = ((i / 5) % 3 - 1) * Spread * 0.6f;
            _pool[i].Visible = true;
            _pool[i].Position = new Vector3(
                (float)(dr - ox) + depth, (float)(r - Const.RE - oy), lane);
            _pool[i].Rotation = new Vector3(0f, (float)s.Rot, Tilt);
        }
    }
}
