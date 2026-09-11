using Starship.Physics;
namespace Starship.Game.Ui;
public sealed record PumpRow(string Label, string Fuel, string Ox, string Unit,
                             bool FuelWarn = false, bool OxWarn = false);
public static class PumpTable {
    public static readonly string[] Labels = {
        "Обороты вала", "Напор / номинал", "Расход", "КПД насоса", "Запас по кавитации",
    };
    public static PumpRow[] Of(Engine e) => new[] {
        new PumpRow(Labels[0], NumFmt.F(e.Pf.Rpm, 0), NumFmt.F(e.Po.Rpm, 0), "об/мин"),
        new PumpRow(Labels[1],
            NumFmt.F(e.Pf.DP / 1e6, 1) + " / " + NumFmt.F(Pump.NomF.DP / 1e6, 1),
            NumFmt.F(e.Po.DP / 1e6, 1) + " / " + NumFmt.F(Pump.NomO.DP / 1e6, 1), "МПа"),
        new PumpRow(Labels[2], NumFmt.F(e.Pf.Q * Pump.RHO_F, 1), NumFmt.F(e.Po.Q * Pump.RHO_OX, 1), "кг/с"),
        new PumpRow(Labels[3], NumFmt.F(e.Pf.Eta, 3), NumFmt.F(e.Po.Eta, 3), ""),
        new PumpRow(Labels[4], NumFmt.F(e.Pf.Cav * 100, 0), NumFmt.F(e.Po.Cav * 100, 0), "%",
                    e.Pf.Cav < 0.9, e.Po.Cav < 0.9),
    };
}
