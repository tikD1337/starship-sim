using System;
using System.Collections.Generic;
using Godot;
namespace Starship.Game;
public sealed class FlapBurnView {
    private readonly List<ShaderMaterial> _mats = new();
    private static readonly Shader Sh = new() { Code = Code };
    private const string Code = @"
shader_type spatial;
uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap, repeat_enable;
uniform bool has_tex = false;
uniform bool has_normal = false;
uniform vec4 albedo : source_color = vec4(1.0);
uniform vec3 uv_scale = vec3(1.0);
uniform vec3 uv_offset = vec3(0.0);
uniform float normal_scale = 1.0;
uniform float roughness = 0.9;
uniform float metallic = 0.0;
uniform vec3 corner = vec3(0.0);
uniform float depth = 1.0;
uniform float r_in = 0.0;
uniform float r_out = 1.0;
uniform float burn = 0.0;
uniform vec3 edge_col : source_color = vec3(0.0);
uniform float edge_e = 0.0;
uniform vec3 tile_col : source_color = vec3(0.0);
uniform float tile_e = 0.0;
varying vec3 pl;
float hash(vec3 p) {
    p = fract(p * 0.3183099 + 0.1);
    p *= 17.0;
    return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}
float noise(vec3 x) {
    vec3 i = floor(x);
    vec3 f = fract(x);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(hash(i), hash(i + vec3(1, 0, 0)), f.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
               mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}
void vertex() {
    pl = VERTEX;
    UV = UV * uv_scale.xy + uv_offset.xy;
}
void fragment() {
    float d = length(pl - corner) / depth;
    float n = noise(pl * 1.7) * 0.6 + noise(pl * 5.3) * 0.4;
    float on = step(0.001, burn);
    float front = on * (burn * 1.1 + (n - 0.5) * 0.25);
    if (d < front) discard;
    vec4 base = has_tex ? texture(albedo_tex, UV) * albedo : albedo;
    float charK = mix(1.0, smoothstep(front, front + 0.2, d), on);
    ALBEDO = base.rgb * mix(0.15, 1.0, charK);
    ROUGHNESS = roughness;
    METALLIC = metallic;
    if (has_normal) {
        NORMAL_MAP = texture(normal_tex, UV).rgb;
        NORMAL_MAP_DEPTH = normal_scale;
    }
    float fade = smoothstep(mix(r_in, r_out, 0.55), r_out, length(pl.xz));
    float rim = on * (1.0 - smoothstep(front, front + 0.05, d));
    EMISSION = mix(tile_col * tile_e, edge_col * edge_e, max(fade, rim));
}
";
    public static FlapBurnView Dress(MeshInstance3D mi, Func<Material, int, bool> isTile, Action<StandardMaterial3D> tile) {
        var view = new FlapBurnView();
        if (mi?.Mesh == null) return view;
        Aabb box = mi.Mesh.GetAabb();
        float rIn = float.MaxValue, rOut = 0f, top = box.End.Y;
        Vector3 corner = Vector3.Zero, far = Vector3.Zero;
        var pts = new List<Vector3>();
        for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            if (mi.Mesh is ArrayMesh am && am.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array() is Vector3[] vs)
                pts.AddRange(vs);
        foreach (Vector3 p in pts) {
            float r = new Vector2(p.X, p.Z).Length();
            rIn = Mathf.Min(rIn, r);
            rOut = Mathf.Max(rOut, r);
        }
        float best = float.MaxValue;
        foreach (Vector3 p in pts) {
            float k = (top - p.Y) + new Vector2(p.X, p.Z).Length() - rIn;
            if (k < best) { best = k; corner = p; }
        }
        float depth = 0.1f;
        foreach (Vector3 p in pts) depth = Mathf.Max(depth, (p - corner).Length());
        int ns = mi.Mesh.GetSurfaceCount();
        for (int s = 0; s < ns; s++) {
            Material raw = mi.Mesh.SurfaceGetMaterial(s);
            StandardMaterial3D src = raw is StandardMaterial3D m ? (StandardMaterial3D)m.Duplicate() : new StandardMaterial3D();
            if (isTile(raw, ns)) tile(src);
            var sm = new ShaderMaterial { Shader = Sh };
            sm.SetShaderParameter("albedo", src.AlbedoColor);
            sm.SetShaderParameter("has_tex", src.AlbedoTexture != null);
            if (src.AlbedoTexture != null) sm.SetShaderParameter("albedo_tex", src.AlbedoTexture);
            sm.SetShaderParameter("has_normal", src.NormalEnabled && src.NormalTexture != null);
            if (src.NormalTexture != null) sm.SetShaderParameter("normal_tex", src.NormalTexture);
            sm.SetShaderParameter("normal_scale", src.NormalScale);
            sm.SetShaderParameter("uv_scale", src.Uv1Scale);
            sm.SetShaderParameter("uv_offset", src.Uv1Offset);
            sm.SetShaderParameter("roughness", src.Roughness);
            sm.SetShaderParameter("metallic", src.Metallic);
            sm.SetShaderParameter("corner", corner);
            sm.SetShaderParameter("depth", depth);
            sm.SetShaderParameter("r_in", rIn);
            sm.SetShaderParameter("r_out", rOut);
            mi.SetSurfaceOverrideMaterial(s, sm);
            view._mats.Add(sm);
        }
        return view;
    }
    public void Set(double burn, Color edge, float edgeE, Color tile, float tileE) {
        foreach (ShaderMaterial m in _mats) {
            m.SetShaderParameter("burn", (float)burn);
            m.SetShaderParameter("edge_col", edge);
            m.SetShaderParameter("edge_e", edgeE);
            m.SetShaderParameter("tile_col", tile);
            m.SetShaderParameter("tile_e", tileE);
        }
    }
}
