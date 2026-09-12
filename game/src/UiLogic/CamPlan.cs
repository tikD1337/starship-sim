namespace Starship.Game.Ui;
public static class CamPlan {
    public static int[] For(string mode) => mode switch {
        "ascent" => new[] { 6, 4, -1 },
        "meco" => new[] { 2, 4 },
        "flip" or "boostback" => new[] { 4, 3 },
        "coastB" => new[] { 3, 5 },
        "landB" => new[] { 4, 5 },
        "ascent2" or "coastS" or "circ" or "orbit" => new[] { 0, 2 },
        "deorbit" or "coastD" => new[] { 0, 1 },
        "entryS" => new[] { 1, 2 },
        "flipS" or "landS" => new[] { 2, 1 },
        _ => new[] { -1 },
    };
    private static int Rank(string mode) => mode switch {
        "landB" or "landS" or "flipS" or "entryS" or "caught" => 4,
        "boostback" or "flip" or "deorbit" or "meco" => 3,
        "ascent" or "ascent2" or "circ" => 2,
        "coastB" or "coastS" or "coastD" or "orbit" => 1,
        _ => 0,
    };
    public static bool Ship(string boosterMode, string shipMode) => Rank(shipMode) > Rank(boosterMode);
}
