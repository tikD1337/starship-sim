using System.Linq;
using Starship.Physics;
namespace Starship.Game.Ui;
public static class FlapText {
    public static (string Text, int Level) Row(FlapState[] f) {
        string text = string.Join(" · ", f.Select(x => $"{NumFmt.F(x.TEdge, 0)}/{NumFmt.F(x.THinge, 0)}"
                                                        + (x.Burn > 0 ? $" прогар {NumFmt.F(x.Burn * 100, 0)} %" : "")
                                                        + (x.Jammed ? " заклинен" : "")));
        int level = f.Any(x => x.Burn > 0 || x.Jammed) ? 2
            : f.Any(x => x.TEdge > 0.95 * Const.TILE_LIMIT || x.THinge > 0.95 * Const.SKIN_LIMIT) ? 1 : 0;
        return (text, level);
    }
    public static string Tape(FlapState[] f)
        => $"кромки до {NumFmt.F(f.Max(x => x.MaxEdge), 0)} K" + string.Concat(f.Select((x, i) =>
            (x.Burn > 0 ? $", прогар {FlapHeat.Names[i]} {NumFmt.F(x.Burn * 100, 0)} %" : "")
            + (x.Jammed ? $", заклинен привод {FlapHeat.Names[i]}" : "")));
}
