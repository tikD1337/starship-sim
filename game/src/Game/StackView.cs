using System;
using System.Collections.Generic;
using Godot;
namespace Starship.Game;
public sealed class StackView {
    public Node3D BoosterRoot, ShipRoot;
    public MeshInstance3D ShipTiles;
    private readonly List<Node3D> _gridFins = new();
    private readonly List<float> _finAzim = new();
    private readonly List<Node3D> _flapsFwd = new(), _flapsAft = new();
    private readonly List<float> _flapFwdAzim = new(), _flapAftAzim = new();
    private static readonly float[] _flapSide = { 1f, -1f };
    private readonly List<MeshInstance3D> _glow = new();
    private float _flapFwdNow, _flapAftNow;
    public (float Fwd, float Aft) FlapAngles => (_flapFwdNow, _flapAftNow);
    public const float FlapFwdMax = 42f, FlapAftMax = 68f;
    private const float FlapRate = 26f;
    public Plume BoosterPlume, ShipPlume;
    private readonly List<Node3D> _boosterBells = new(), _shipBells = new();
    private Node _world;
    private bool _attached = true;
    private const float BoosterLen = 72.3f, GridFinY = 61.2f, FlapFwdY = 47.641f, FlapAftY = 5.913f,
        FwdAzimA = 115.150f, FwdAzimB = 247.650f, AftAzimA = 92.580f, AftAzimB = 270.220f,
        FlapFwdR = 2.880f, FlapFwdRB = 2.880f, FlapAftR = 4.559f, FlapAftRB = 4.559f, FinR = 4.40f,
        BellY = 2.9f, BellVacY = 4.5f;
    private static void Ring(ModelLibrary lib, Node3D root, string part, int n, float rad, float y,
                             float phase, List<Node3D> into) {
        for (int k = 0; k < n; k++) {
            float a = k / (float)n * Mathf.Tau + phase;
            MeshInstance3D m = lib.Make(part, root);
            if (m == null) continue;
            m.Position = new Vector3(Mathf.Cos(a) * rad, y, -Mathf.Sin(a) * rad);
            m.RotateX(Mathf.Pi);
            into?.Add(m);
        }
    }
    private static (Node3D Pivot, MeshInstance3D Mesh) Mount(ModelLibrary lib, Node3D root,
                               string part, float azimDeg, float rad, float y) {
        float a = Mathf.DegToRad(azimDeg);
        var p = new Node3D { Name = part };
        root.AddChild(p);
        p.Position = new Vector3(Mathf.Cos(a) * rad, y, -Mathf.Sin(a) * rad);
        MeshInstance3D mi = lib.Make(part, p);
        if (mi != null) mi.Position = -p.Position;
        return (p, mi);
    }
    private static (Node3D Pivot, MeshInstance3D Mesh) Hinge(ModelLibrary lib, Node3D root, string part,
                                                             float aDeg, float rad, float y) {
        float a = Mathf.DegToRad(aDeg);
        var p = new Node3D { Name = $"{part}{aDeg:F0}" };
        root.AddChild(p);
        p.Position = new Vector3(Mathf.Cos(a) * rad, y, -Mathf.Sin(a) * rad);
        p.RotateY(a);
        return (p, lib.Make(part, p));
    }
    public static StackView Build(ModelLibrary lib, Node parent) {
        var v = new StackView();
        v.BoosterRoot = new Node3D { Name = "Booster" };
        parent.AddChild(v.BoosterRoot);
        lib.Make("hull_booster", v.BoosterRoot);
        MeshInstance3D hs = lib.Make("hotstage", v.BoosterRoot, required: false);
        if (hs != null) hs.Position = new Vector3(0, BoosterLen, 0);
        foreach (float adeg in new[] { 90f, 270f, 180f }) {
            (Node3D pivot, MeshInstance3D fin) = Hinge(lib, v.BoosterRoot, "gridfin", adeg, FinR, GridFinY);
            if (fin == null) continue;
            v._gridFins.Add(pivot);
            v._finAzim.Add(Mathf.DegToRad(adeg));
        }
        foreach ((int n, float rad) in new[] { (3, 1.02f), (10, 2.40f), (20, 3.55f) })
            Ring(lib, v.BoosterRoot, "bell_sl", n, rad, BellY, 0f, v._boosterBells);
        v.ShipRoot = new Node3D { Name = "Ship" };
        v.BoosterRoot.AddChild(v.ShipRoot);
        v._world = parent;
        lib.Make("hull_ship", v.ShipRoot);
        v.ShipTiles = lib.Make("tiles_ship", v.ShipRoot);
        if (v.ShipTiles != null) v._glow.Add(v.ShipTiles);
        for (int k = 0; k < 2; k++) {
            float fa = k == 0 ? FwdAzimA : FwdAzimB;
            float aa = k == 0 ? AftAzimA : AftAzimB;
            (Node3D pf, MeshInstance3D mf) = Mount(lib, v.ShipRoot,
                k == 0 ? "flap_fwd" : "flap_fwd_b", fa, k == 0 ? FlapFwdR : FlapFwdRB, FlapFwdY);
            (Node3D pa, MeshInstance3D ma) = Mount(lib, v.ShipRoot,
                k == 0 ? "flap_aft" : "flap_aft_b", aa, k == 0 ? FlapAftR : FlapAftRB, FlapAftY);
            v._flapsFwd.Add(pf);
            v._flapsAft.Add(pa);
            if (mf != null) v._glow.Add(mf);
            if (ma != null) v._glow.Add(ma);
            v._flapFwdAzim.Add(Mathf.DegToRad(fa));
            v._flapAftAzim.Add(Mathf.DegToRad(aa));
        }
        Ring(lib, v.ShipRoot, "bell_sl", 3, 0.87f, BellY, 0f, v._shipBells);
        Ring(lib, v.ShipRoot, "bell_vac", 3, 3.06f, BellVacY, Mathf.Pi / 3f, v._shipBells);
        FixHullNormals(v.BoosterRoot);
        FixHullNormals(v.ShipRoot);
        v.BoosterPlume = Plume.Attach(v._boosterBells, 0.60f, 4.3f, v.BoosterRoot);
        v.ShipPlume = Plume.Attach(v._shipBells, 0.85f, 2.9f, v.ShipRoot);
        return v;
    }
    public void SetStacked(bool stacked) {
        if (stacked) {
            if (!_attached) {
                _attached = true;
                _world.RemoveChild(ShipRoot);
                BoosterRoot.AddChild(ShipRoot);
            }
            ShipRoot.Position = new Vector3(0, BoosterLen, 0);
            ShipRoot.Rotation = Vector3.Zero;
            return;
        }
        if (_attached) {
            _attached = false;
            BoosterRoot.RemoveChild(ShipRoot);
            _world.AddChild(ShipRoot);
        }
    }
    private readonly List<StandardMaterial3D> _tileMats = new();
    private static bool IsTile(Material src, int surfaces) {
        if (surfaces <= 1) return true;
        string n = src?.ResourceName ?? "";
        return n.Contains("Heatshield") || n.Contains("Tile") || n.Contains("плит");
    }
    private static Texture2D _tileAlbedo, _tileNormal, _hullNormal;
    private static void FixHullNormals(Node root) {
        _hullNormal ??= GD.Load<Texture2D>("res://assets/textures/hull_normal.png");
        if (_hullNormal == null) return;
        if (root is MeshInstance3D mi && mi.Mesh != null)
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++) {
                if (mi.Mesh.SurfaceGetMaterial(i) is not StandardMaterial3D src) continue;
                if (!(src.ResourceName ?? "").StartsWith("Cryo Section")) continue;
                var m = (StandardMaterial3D)src.Duplicate();
                m.NormalEnabled = true;
                m.NormalTexture = _hullNormal;
                m.NormalScale = 0.5f;
                mi.SetSurfaceOverrideMaterial(i, m);
            }
        foreach (Node n in root.GetChildren())
            FixHullNormals(n);
    }
    private static void Dress(StandardMaterial3D m) {
        _tileAlbedo ??= GD.Load<Texture2D>("res://assets/textures/tiles_albedo.png");
        _tileNormal ??= GD.Load<Texture2D>("res://assets/textures/tiles_normal.png");
        if (_tileAlbedo == null) return;
        m.AlbedoTexture = _tileAlbedo;
        m.AlbedoColor = Colors.White;
        m.Metallic = 0f;
        m.Roughness = 0.94f;
        if (_tileNormal == null) return;
        m.NormalEnabled = true;
        m.NormalTexture = _tileNormal;
        m.NormalScale = 0.7f;
    }
    public void SetTileGlow(double tTile) {
        if (_glow.Count == 0) return;
        if (_tileMats.Count == 0) {
            foreach (MeshInstance3D part in _glow) {
                int ns = part.Mesh.GetSurfaceCount();
                for (int i = 0; i < ns; i++) {
                    Material raw = part.Mesh.SurfaceGetMaterial(i);
                    if (!IsTile(raw, ns)) continue;
                    StandardMaterial3D m = raw is StandardMaterial3D src
                        ? (StandardMaterial3D)src.Duplicate()
                        : new StandardMaterial3D { AlbedoColor = new Color(0.045f, 0.046f, 0.05f), Roughness = 0.85f };
                    Dress(m);
                    m.EmissionEnabled = true;
                    m.Emission = new Color(0f, 0f, 0f);
                    m.EmissionEnergyMultiplier = 0f;
                    part.SetSurfaceOverrideMaterial(i, m);
                    _tileMats.Add(m);
                }
            }
        }
        float x = Mathf.Clamp((float)(tTile - 640.0) / 1180f, 0f, 1.45f);
        float e = Mathf.Pow(x, 3.4f) * 1.35f;
        Color c = x < 0.62f
            ? new Color(0.58f, 0.045f, 0.008f).Lerp(new Color(1.0f, 0.30f, 0.035f), x / 0.62f)
            : new Color(1.0f, 0.30f, 0.035f).Lerp(new Color(1.0f, 0.86f, 0.66f), Mathf.Min((x - 0.62f) / 0.7f, 1f));
        foreach (StandardMaterial3D m in _tileMats) {
            m.Emission = c;
            m.EmissionEnergyMultiplier = e;
        }
    }
    public void SetFins(float deploy, float deflect) {
        for (int i = 0; i < _gridFins.Count; i++) {
            float fold = Mathf.DegToRad(70f * (1f - deploy));
            float defl = Mathf.DegToRad(18f * deflect);
            _gridFins[i].Basis =
                Basis.FromEuler(new Vector3(0, _finAzim[i], 0)) *
                Basis.FromEuler(new Vector3(0, 0, -fold)) *
                Basis.FromEuler(new Vector3(defl, 0, 0));
        }
    }
    public void StepFlaps(double dt, float tgtFwd, float tgtAft) {
        float lim = FlapRate * (float)Mathf.Max(dt, 0.0);
        tgtFwd = Mathf.Clamp(tgtFwd, 0f, FlapFwdMax);
        tgtAft = Mathf.Clamp(tgtAft, 0f, FlapAftMax);
        _flapFwdNow += Mathf.Clamp(tgtFwd - _flapFwdNow, -lim, lim);
        _flapAftNow += Mathf.Clamp(tgtAft - _flapAftNow, -lim, lim);
        SetFlaps(_flapFwdNow, _flapAftNow);
    }
    public void SetFlaps(float fwdDeg, float aftDeg) {
        for (int i = 0; i < _flapsFwd.Count; i++)
            _flapsFwd[i].Basis = Basis.FromEuler(
                new Vector3(0, _flapSide[i] * Mathf.DegToRad(fwdDeg), 0));
        for (int i = 0; i < _flapsAft.Count; i++)
            _flapsAft[i].Basis = Basis.FromEuler(
                new Vector3(0, _flapSide[i] * Mathf.DegToRad(aftDeg), 0));
    }
}
