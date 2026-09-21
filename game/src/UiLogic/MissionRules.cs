using Starship.Physics;
namespace Starship.Game;
public enum Aim { Wait, Done, Fail, Part }
public static class MissionRules {
    public static bool Fallback(Vehicle v) => v.SeekPad && (v.Site == "sea" || v.Site == "pad");
    public static Aim Catch(Vehicle v) {
        if (v.Crashed) return Aim.Fail;
        if (v.Caught) return Aim.Done;
        if (!v.Landed) return Aim.Wait;
        return Fallback(v) ? Aim.Part : Aim.Fail;
    }
    public static string Where(Vehicle v) => !Fallback(v) ? ""
        : v.Site == "sea" ? "запасная посадка в море" : "запасная посадка на площадке у башни";
    public static int Score(Aim state, int bas, int bonus) =>
        state == Aim.Done ? bas + bonus : state == Aim.Part ? bas / 2 : 0;
    public static string Mark(Vehicle v, string who) =>
        v.Crashed ? "потеря " + who
        : v.Caught ? "захват " + who
        : v.Site == "sea" && v.SeekPad ? "приводнение " + who
        : v.Site == "pad" && v.SeekPad ? "посадка " + who + " на площадку"
        : v.SeekPad ? "захват " + who : "посадка " + who;
}
