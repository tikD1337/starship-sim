using System;
using Starship.Physics;
namespace Starship.Game.Ui;
public static class Rail {
    public sealed record Mark(string Name, bool Past, bool Next);
    public const int Window = 4;
    public static Mark[] Of(SimState sim) {
        string[] names = Arc.NamesOf(sim.Mission, sim);
        bool[] past = Arc.Now(sim);
        int last = -1, next = -1;
        for (int i = 0; i < past.Length; i++) {
            if (past[i]) last = i;
            else if (next < 0) next = i;
        }
        int first = Math.Clamp(last - 1, 0, names.Length - Window);
        var marks = new Mark[Window];
        for (int i = 0; i < Window; i++)
            marks[i] = new Mark(names[first + i], past[first + i], first + i == next);
        return marks;
    }
}
