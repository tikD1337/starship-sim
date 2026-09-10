using System.Collections.Generic;
using Godot;
namespace Starship.Game;
public sealed class ModelLibrary {
    private readonly Dictionary<string, Mesh> _meshes = new();
    public IReadOnlyDictionary<string, Mesh> Meshes => _meshes;
    public static ModelLibrary Load(string path, string spare = null) {
        var lib = new ModelLibrary();
        var packed = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
        if (packed == null && spare != null && ResourceLoader.Exists(spare)) {
            GD.Print($"MODEL_SPARE {path} не нашлась, берём {spare}");
            packed = GD.Load<PackedScene>(spare);
        }
        if (packed == null) {
            GD.PushError($"не загрузилась модель {path}");
            return lib;
        }
        Node root = packed.Instantiate();
        lib.Collect(root);
        root.QueueFree();
        return lib;
    }
    private void Collect(Node n) {
        if (n is MeshInstance3D mi && mi.Mesh != null)
            _meshes[mi.Name] = mi.Mesh;
        foreach (Node c in n.GetChildren())
            Collect(c);
    }
    public MeshInstance3D Make(string name, Node parent, bool required = true) {
        if (!_meshes.TryGetValue(name, out Mesh m)) {
            if (required) GD.PushWarning($"нет детали {name}");
            return null;
        }
        var mi = new MeshInstance3D { Mesh = m, Name = name };
        parent.AddChild(mi);
        return mi;
    }
}
