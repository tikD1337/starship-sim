using System.Collections.Generic;
using Godot;
namespace Starship.Game;
public static class ReplayFiles {
    public const string Dir = "user://replays";
    public static string Save(Replay r) {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        string name = r.FileName(System.DateTime.Now), path = Dir + "/" + name;
        for (int i = 2; FileAccess.FileExists(path) && i < 100; i++)
            path = Dir + "/" + name.Replace(".txt", "-" + i + ".txt");
        using FileAccess f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (f == null) return null;
        f.StoreString(r.Text());
        return path;
    }
    public static Replay Load(string path) {
        using FileAccess f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return null;
        return Replay.FromText(f.GetAsText().Split('\n'), Mission.Keys);
    }
    public static string Newest() {
        using DirAccess d = DirAccess.Open(Dir);
        if (d == null) return null;
        var files = new List<(string, ulong)>();
        foreach (string name in d.GetFiles()) files.Add((name, FileAccess.GetModifiedTime(Dir + "/" + name)));
        string best = Replay.Newest(files);
        return best == null ? null : Dir + "/" + best;
    }
}
