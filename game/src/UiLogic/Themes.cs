namespace Starship.Game.Ui;
public sealed record Palette(string Key, string Name, uint Bg, uint Panel, uint Field, uint Line,
    uint Ink, uint Ink2, uint Lab, uint Accent, uint AccentInk, uint Disc, uint DiscInk, uint DiscRing,
    uint Glow, uint Off, uint Trace, uint L1, uint L2, uint L3, uint L4);
public static class Themes {
    public const uint Warn = 0xF2B544FF, Crit = 0xF0625AFF;
    public static readonly Palette[] All = {
        new("a", "Графит и лёд", 0x0D0F12FF, 0x15181CFF, 0x1C2026FF, 0x262B32FF,
            0xF3F5F7FF, 0xC4CAD2FF, 0x8A929CFF, 0x7CC4FFFF, 0x0D0F12FF, 0x232C37FF, 0xF3F5F7FF, 0xF3F5F7FF,
            0x7CC4FF47, 0x4A515BFF, 0xF3F5F71F, 0xF3F5F7FF, 0x7CC4FFFF, 0xC4CAD2FF, 0x5F8FB8FF),
        new("b", "Чёрный эфир", 0x000000FF, 0x0C0C0DFF, 0x151517FF, 0x232326FF,
            0xFFFFFFFF, 0xBEBEC3FF, 0x7E7E85FF, 0xFFFFFFFF, 0x000000FF, 0xFFFFFFFF, 0x000000FF, 0xFFFFFFFF,
            0xFFFFFF38, 0x3C3C41FF, 0xFFFFFF1F, 0xFFFFFFFF, 0x9AA0A8FF, 0xD6D6DAFF, 0x6E6E75FF),
        new("c", "Сталь и бирюза", 0x0B1417FF, 0x112027FF, 0x16292FFF, 0x21363EFF,
            0xEEF6F7FF, 0xBCD0D4FF, 0x7C979EFF, 0x4FD1C5FF, 0x062024FF, 0x163A40FF, 0xEEF6F7FF, 0xBFEFEAFF,
            0x4FD1C54D, 0x3F555BFF, 0xEEF6F71F, 0xEEF6F7FF, 0x4FD1C5FF, 0xBCD0D4FF, 0x5C8C93FF),
    };
    public static int Parse(string s, int fallback) {
        string k = s?.Trim().ToLowerInvariant();
        if (k is not { Length: 1 }) return fallback;
        int i = "abc".IndexOf(k[0]);
        return i >= 0 ? i : fallback;
    }
}
