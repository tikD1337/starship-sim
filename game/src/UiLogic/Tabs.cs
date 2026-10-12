namespace Starship.Game.Ui;
public static class Tabs {
    public static int Next(int cur) => cur % 3 + 1;
    public static bool ScriptLit(string tab, string script, bool anomOn) => anomOn && tab == script;
}
