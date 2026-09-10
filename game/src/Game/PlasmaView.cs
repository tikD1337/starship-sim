using System;
using System.Collections.Generic;
using Godot;
namespace Starship.Game;
public sealed class PlasmaView {
    private Node3D _flowRoot, _bodyRoot;
    private MeshInstance3D _sheath, _halo;
    private OmniLight3D _lamp;
    private readonly List<MeshInstance3D> _wake = new();
    private ShaderMaterial _sheathMat, _haloMat;
    private readonly List<ShaderMaterial> _wakeMats = new();
    private StandardMaterial3D _sparkMat;
    private GpuParticles3D _sparks;
    private const int WakeCount = 6;
    private static readonly Shader Sh = new() { Code = Code };
    private static ShaderMaterial Skin() => new() { Shader = Sh };
    private const double HeatRef = 260.0;
    private const string Code = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;
uniform vec3 flow_local = vec3(0.0, 0.0, 1.0);
uniform vec3 core_col : source_color = vec3(0.90, 0.86, 1.0);
uniform vec3 edge_col : source_color = vec3(0.26, 0.24, 0.92);
uniform float power = 1.0;
uniform float flick = 0.0;
uniform float floor_lit = 0.6;
uniform float streaks = 0.0;
uniform float soft = 1.5;
uniform float inside_floor = 0.0;
uniform float inside_flat = 0.40;
uniform float inside_gain = 0.55;
varying vec3 nl;
varying vec3 pl;
void vertex() {
    nl = NORMAL;
    pl = VERTEX;
}
void fragment() {
    float rim = 1.0 - abs(dot(normalize(NORMAL), normalize(VIEW)));
    float face = clamp(dot(normalize(nl), normalize(flow_local)), 0.0, 1.0);
    float thick = clamp(1.0 - rim, 0.0, 1.0);
    float lit = 0.35 + 0.65 * floor_lit;
    float body;
    float rayk = 0.55;
    if (FRONT_FACING) {
        body = pow(face, 0.9) * pow(thick, soft) * lit;
    } else {
        body = mix(inside_floor, 1.0, face) * mix(inside_flat, 1.0, thick) * lit * inside_gain;
        rayk = 0.07;
    }
    float ang = atan(pl.z, pl.x);
    float ray = 1.0 + streaks * rayk * sin(ang * 9.0 + pl.y * 0.7 + flick * 1.7);
    float ripple = 0.86 + 0.14 * sin(pl.y * 1.7 + flick * 5.1) * sin(pl.x * 2.3 - flick * 3.7);
    vec3 c = mix(edge_col, core_col, pow(face, 1.35));
    ALBEDO = c * body * ripple * ray * power;
    ALPHA = 1.0;
}
";
    private static MeshInstance3D Blob(Node parent, ShaderMaterial m, int seg, int rings) {
        var mi = new MeshInstance3D {
            Mesh = new SphereMesh { Radius = 1.0f, Height = 2.0f, RadialSegments = seg, Rings = rings },
            MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(mi);
        return mi;
    }
    public static PlasmaView Build(Node world, Node3D shipRoot) {
        var p = new PlasmaView();
        p._flowRoot = new Node3D { Name = "PlasmaWake", Visible = false };
        world.AddChild(p._flowRoot);
        p._bodyRoot = new Node3D { Name = "PlasmaSheath", Visible = false };
        shipRoot.AddChild(p._bodyRoot);
        p._sheathMat = Skin();
        p._sheath = Blob(p._bodyRoot, p._sheathMat, 46, 28);
        p._haloMat = Skin();
        p._halo = Blob(p._bodyRoot, p._haloMat, 30, 18);
        p._lamp = new OmniLight3D {
            LightColor = new Color(0.72f, 0.66f, 1.0f), LightEnergy = 0f, OmniRange = 190f,
            ShadowEnabled = false,
        };
        p._bodyRoot.AddChild(p._lamp);
        for (int k = 0; k < WakeCount; k++) {
            var m = Skin();
            p._wake.Add(Blob(p._flowRoot, m, 22, 14));
            p._wakeMats.Add(m);
        }
        p._sparkMat = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = new Color(0.92f, 0.88f, 1.0f),
            AlbedoTexture = LaunchSmoke.Puff(64), EmissionEnabled = true,
            Emission = new Color(0.86f, 0.82f, 1.0f), EmissionEnergyMultiplier = 6.0f,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, BillboardKeepScale = true,
            DisableReceiveShadows = true,
        };
        var pm = new ParticleProcessMaterial {
            Direction = new Vector3(0, 0, -1), Spread = 24f, Gravity = Vector3.Zero,
            InitialVelocityMin = 70f, InitialVelocityMax = 210f, ScaleMin = 0.30f, ScaleMax = 0.95f,
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(8f, 24f, 6f),
        };
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.95f, 0.92f, 1.0f, 1.0f));
        grad.SetColor(1, new Color(0.34f, 0.28f, 0.95f, 0.0f));
        pm.ColorRamp = new GradientTexture1D { Gradient = grad };
        p._sparks = new GpuParticles3D {
            Amount = 460, Lifetime = 1.0, ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.55f, 0.55f), Material = p._sparkMat },
            VisibilityAabb = new Aabb(new Vector3(-300, -300, -300), new Vector3(600, 600, 600)),
            Emitting = false,
        };
        p._flowRoot.AddChild(p._sparks);
        return p;
    }
    public void Update(Vector3 midWorld, Vector3 flowWorld, Vector3 flowLocal,
                       double heatKw, double clock, bool alive) {
        double f = Math.Clamp(heatKw / HeatRef, 0.0, 2.6);
        bool on = alive && f > 0.03;
        _flowRoot.Visible = on;
        _bodyRoot.Visible = on;
        _sparks.Emitting = on && f > 0.20;
        if (!on) return;
        float pw = (float)Math.Pow(f, 0.85);
        float vio = Mathf.Clamp((float)(f - 0.16) / 0.62f, 0f, 1f);
        Color core = new Color(1.0f, 0.52f, 0.30f).Lerp(new Color(0.92f, 0.88f, 1.0f), vio);
        Color edge = new Color(0.86f, 0.22f, 0.20f).Lerp(new Color(0.26f, 0.24f, 0.95f), vio);
        Vector3 wind = flowLocal.LengthSquared() > 1e-6f ? flowLocal.Normalized() : Vector3.Down;
        Vector3 windward = -wind;
        _sheath.Position = windward * (2.2f + 0.7f * pw) + new Vector3(0, 24f, 0);
        _sheath.Scale = new Vector3(6.6f + 1.3f * pw, 25f + 3.5f * pw, 7.2f + 1.5f * pw);
        _sheathMat.SetShaderParameter("flow_local", windward);
        _sheathMat.SetShaderParameter("core_col", core);
        _sheathMat.SetShaderParameter("edge_col", edge);
        _sheathMat.SetShaderParameter("power", 1.75f * pw);
        _sheathMat.SetShaderParameter("floor_lit", 0.66f);
        _sheathMat.SetShaderParameter("streaks", 0.9f);
        _sheathMat.SetShaderParameter("inside_gain", 1.05f);
        _sheathMat.SetShaderParameter("inside_floor", 0.02f);
        _sheathMat.SetShaderParameter("soft", 2.25f);
        _sheathMat.SetShaderParameter("flick", (float)clock);
        _halo.Position = windward * (4.5f + 1.6f * pw) + new Vector3(0, 24f, 0);
        _halo.Scale = new Vector3(13f + 4f * pw, 34f + 6f * pw, 14f + 4f * pw);
        _haloMat.SetShaderParameter("flow_local", windward);
        _haloMat.SetShaderParameter("core_col", edge.Lerp(core, 0.35f));
        _haloMat.SetShaderParameter("edge_col", edge * 0.55f);
        _haloMat.SetShaderParameter("power", 0.44f * pw);
        _haloMat.SetShaderParameter("soft", 3.1f);
        _haloMat.SetShaderParameter("floor_lit", 0.85f);
        _haloMat.SetShaderParameter("streaks", 0.35f);
        _haloMat.SetShaderParameter("inside_gain", 0.38f);
        _haloMat.SetShaderParameter("inside_floor", 0.0f);
        _haloMat.SetShaderParameter("flick", (float)clock * 0.7f);
        _lamp.Position = windward * 7.5f + new Vector3(0, 24f, 0);
        _lamp.LightColor = core;
        _lamp.LightEnergy = 5.5f * pw;
        _flowRoot.Position = midWorld;
        Vector3 up = Math.Abs(flowWorld.Y) > 0.985f ? Vector3.Right : Vector3.Up;
        _flowRoot.Basis = Basis.LookingAt(flowWorld, up);
        for (int k = 0; k < WakeCount; k++) {
            float u = (k + 1) / (float)WakeCount;
            float dist = 11f + u * (68f + 40f * pw);
            float rad = 7.8f + u * (9.5f + 6f * pw);
            _wake[k].Position = new Vector3(0, 0, -dist);
            _wake[k].Scale = new Vector3(rad, rad * 0.82f, rad * 3.1f);
            _wakeMats[k].SetShaderParameter("flow_local", new Vector3(0, 0, -1));
            _wakeMats[k].SetShaderParameter("core_col", core.Lerp(edge, 0.45f));
            _wakeMats[k].SetShaderParameter("edge_col", edge * 0.7f);
            _wakeMats[k].SetShaderParameter("power", 1.15f * pw * (1f - u) * (1f - u * 0.5f));
            _wakeMats[k].SetShaderParameter("floor_lit", 0.72f);
            _wakeMats[k].SetShaderParameter("streaks", 0.5f);
            _wakeMats[k].SetShaderParameter("soft", 2.4f);
            _wakeMats[k].SetShaderParameter("flick", (float)clock + k * 1.7f);
            _wakeMats[k].SetShaderParameter("inside_gain", 0.18f);
            _wakeMats[k].SetShaderParameter("inside_floor", 0.0f);
        }
        _sparkMat.AlbedoColor = core * (0.30f + 0.30f * pw);
    }
}
