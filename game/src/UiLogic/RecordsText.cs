using System;
using System.Collections.Generic;
using System.Text.Json;
namespace Starship.Game.Ui;
public static class RecordsText {
    public readonly record struct Entry(int Score, string Grade, string When);
    public static Dictionary<string, Entry> Parse(string text) {
        var r = new Dictionary<string, Entry>();
        if (string.IsNullOrWhiteSpace(text)) return r;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(text); }
        catch (JsonException) { return r; }
        using (doc) {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return r;
            foreach (JsonProperty p in doc.RootElement.EnumerateObject()) {
                JsonElement v = p.Value;
                if (Score(v, out int s0)) r[p.Name] = new Entry(s0, "", "");
                else if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("s", out JsonElement s) && Score(s, out int sc))
                    r[p.Name] = new Entry(sc, Str(v, "g"), Str(v, "t"));
            }
        }
        return r;
    }
    private static bool Score(JsonElement e, out int x) {
        x = 0;
        if (e.ValueKind != JsonValueKind.Number || !e.TryGetDouble(out double d) || !double.IsFinite(d)
            || d < 0 || d > int.MaxValue) return false;
        x = (int)d;
        return true;
    }
    private static string Str(JsonElement o, string k) =>
        o.TryGetProperty(k, out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";
}
