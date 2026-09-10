using System;
using System.Collections.Generic;
using Godot;
namespace Starship.Game;
public sealed class Plume {
    private sealed class Shell {
        public MeshInstance3D Node;
        public StandardMaterial3D Mat;
        public float Energy, WideK, LenK;
    }
    private readonly List<MeshInstance3D> _tips = new();
    private readonly List<Shell> _shells = new();
    private OmniLight3D _light;
    private Node3D _mount;
    private static Mesh Cone(float rTop, float rBot, float len, float aTop, float aBot, int seg = 26) {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < seg; i++) {
            float a0 = i / (float)seg * Mathf.Tau;
            float a1 = (i + 1) / (float)seg * Mathf.Tau;
            var t0 = new Vector3(Mathf.Cos(a0) * rTop, 0, Mathf.Sin(a0) * rTop);
            var t1 = new Vector3(Mathf.Cos(a1) * rTop, 0, Mathf.Sin(a1) * rTop);
            var b0 = new Vector3(Mathf.Cos(a0) * rBot, -len, Mathf.Sin(a0) * rBot);
            var b1 = new Vector3(Mathf.Cos(a1) * rBot, -len, Mathf.Sin(a1) * rBot);
            var cT = new Color(1, 1, 1, aTop);
            var cB = new Color(1, 1, 1, aBot);
            st.SetColor(cT); st.AddVertex(t0);
            st.SetColor(cT); st.AddVertex(t1);
            st.SetColor(cB); st.AddVertex(b1);
            st.SetColor(cT); st.AddVertex(t0);
            st.SetColor(cB); st.AddVertex(b1);
            st.SetColor(cB); st.AddVertex(b0);
        }
        st.GenerateNormals();
        return st.Commit();
    }
    private static StandardMaterial3D Mat(Color c, float energy) {
        return new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true, AlbedoColor = c, EmissionEnabled = true, Emission = c,
            EmissionEnergyMultiplier = energy, DisableReceiveShadows = true,
        };
    }
    private void MakeShell(Mesh mesh, Color c, float energy, float wideK, float lenK) {
        StandardMaterial3D mat = Mat(c, energy);
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat };
        _mount.AddChild(mi);
        _shells.Add(new Shell { Node = mi, Mat = mat, Energy = energy, WideK = wideK, LenK = lenK });
    }
    public static Plume Attach(IEnumerable<Node3D> bells, float exitR, float clusterR, Node3D root) {
        var p = new Plume();
        p._mount = new Node3D();
        root.AddChild(p._mount);
        Mesh tip = Cone(exitR * 0.95f, exitR * 1.25f, 1f, 1.0f, 0.35f, 16);
        StandardMaterial3D tipMat = Mat(new Color(1.0f, 0.93f, 0.72f), 1.9f);
        foreach (Node3D bell in bells) {
            var mi = new MeshInstance3D { Mesh = tip, MaterialOverride = tipMat };
            root.AddChild(mi);
            mi.Position = bell.Position + new Vector3(0, -0.15f, 0);
            p._tips.Add(mi);
        }
        p.MakeShell(Cone(clusterR * 1.20f, clusterR * 3.60f, 1f, 0.10f, 0.0f, 28),
                    new Color(0.80f, 0.46f, 0.92f), 0.35f, 1.00f, 1.25f);
        p.MakeShell(Cone(clusterR * 0.90f, clusterR * 2.40f, 1f, 0.22f, 0.0f, 30),
                    new Color(0.95f, 0.48f, 0.80f), 0.55f, 1.00f, 1.00f);
        p.MakeShell(Cone(clusterR * 0.62f, clusterR * 0.22f, 1f, 0.45f, 0.0f, 24),
                    new Color(1.0f, 0.68f, 0.38f), 1.1f, 0.90f, 0.30f);
        p._light = new OmniLight3D {
            LightColor = new Color(1.0f, 0.58f, 0.34f), ShadowEnabled = false,
        };
        root.AddChild(p._light);
        p._light.Position = new Vector3(0, -8f, 0);
        return p;
    }
    public void Update(double thrustFrac, double ambientPa, int running, double bellY, float clip = 0f) {
        bool on = thrustFrac > 0.01 && running > 0;
        float vac = 1f - (float)Mathf.Clamp(ambientPa / 101325.0, 0, 1);
        float f = (float)Mathf.Clamp(thrustFrac, 0, 1.2);
        foreach (MeshInstance3D t in _tips) t.Visible = false;
        for (int i = 0; i < Mathf.Min(running, _tips.Count); i++) {
            _tips[i].Visible = on;
            float j = 1f + 0.05f * Mathf.Sin(Time.GetTicksMsec() * 0.03f + i * 2.1f);
            _tips[i].Scale = new Vector3(1f + 0.7f * vac, (2.2f + 3.4f * f) * j, 1f + 0.7f * vac);
        }
        _mount.Position = new Vector3(0, (float)bellY - 2.4f, 0);
        float lenBase = 120f + 190f * f;
        float part = Mathf.Sqrt(running / 33f);
        float len = lenBase * (0.55f + 1.10f * vac) * part;
        if (clip > 0f) len = Mathf.Min(len, clip);
        float wide = (0.85f + 0.35f * f) * (1f + 1.5f * vac) * Mathf.Sqrt(part);
        foreach (Shell sh in _shells) sh.Node.Visible = on;
        if (on) {
            float jj = 1f + 0.03f * Mathf.Sin(Time.GetTicksMsec() * 0.018f);
            float dim = 0.45f + 0.55f * part;
            foreach (Shell sh in _shells) {
                float wk = wide * sh.WideK;
                sh.Node.Scale = new Vector3(wk, len * sh.LenK * jj, wk);
                sh.Mat.EmissionEnergyMultiplier = sh.Energy * dim;
            }
        }
        _light.Visible = on;
        _light.LightEnergy = on ? 6.0f * f * part : 0f;
        _light.OmniRange = 60f + 260f * f * part;
    }
}
