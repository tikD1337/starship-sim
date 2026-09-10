using System;
using System.Collections.Generic;
using Starship.Physics;
using PEngine = Starship.Physics.Engine;
namespace Starship.Game.Ui;
public enum Scope { Engine, Vehicle, ReadOnly }
public sealed class ParamRow {
    public string Key, Label, Unit;
    public Scope Scope = Scope.Engine;
    public int Digits = 2;
    public double Lo = double.NegativeInfinity, Hi = double.PositiveInfinity;
    public double Warn = double.NaN, Crit = double.NaN;
    public bool LowIsBad;
    public string Note;
    public Func<PEngine, double> FromEngine;
    public Func<Vehicle, double> FromVehicle;
    public Func<EngineParams, double> Get;
    public Action<EngineParams, double> Set;
    public Func<Vehicle, double> GetVeh;
    public Action<Vehicle, double> SetVeh;
    public bool Editable => Scope != Scope.ReadOnly;
}
public sealed class ParamSection {
    public string Title;
    public List<ParamRow> Rows = new();
}
public sealed class ParamGroup {
    public string Name;
    public List<ParamSection> Sections = new();
}
public static class ParamDefs {
    private static ParamRow Eng(string key, string label, string unit, int d, double lo, double hi,
                                Func<EngineParams, double> get, Action<EngineParams, double> set)
        => new() { Key = key, Label = label, Unit = unit, Digits = d, Lo = lo, Hi = hi,
                   Scope = Scope.Engine, Get = get, Set = set };
    private static ParamRow Veh(string key, string label, string unit, int d, double lo, double hi,
                                Func<Vehicle, double> get, Action<Vehicle, double> set)
        => new() { Key = key, Label = label, Unit = unit, Digits = d, Lo = lo, Hi = hi,
                   Scope = Scope.Vehicle, GetVeh = get, SetVeh = set };
    private static ParamRow RoE(string key, string label, string unit, int d, Func<PEngine, double> f)
        => new() { Key = key, Label = label, Unit = unit, Digits = d, Scope = Scope.ReadOnly, FromEngine = f };
    private static ParamRow RoV(string key, string label, string unit, int d, Func<Vehicle, double> f)
        => new() { Key = key, Label = label, Unit = unit, Digits = d, Scope = Scope.ReadOnly, FromVehicle = f };
    private static ParamRow Zone(this ParamRow r, double warn, double crit, string note,
                                 bool lowIsBad = false) {
        r.Warn = warn;
        r.Crit = crit;
        r.Note = note;
        r.LowIsBad = lowIsBad;
        return r;
    }
    public static string ZoneOf(ParamRow r, double x) {
        if (double.IsNaN(r.Crit)) return "";
        bool crit = r.LowIsBad ? x <= r.Crit : x >= r.Crit;
        if (crit) return "crit";
        bool warn = r.LowIsBad ? x <= r.Warn : x >= r.Warn;
        return warn ? "warn" : "";
    }
    public static readonly List<ParamGroup> Groups = new() {
        new ParamGroup { Name = "Турбонасос", Sections = {
            new ParamSection { Title = "Вал", Rows = {
                Eng("rpmSet", "Уставка оборотов вала", "об/мин", 0, 0, 46000, p => p.RpmSet, (p, v) => p.RpmSet = v),
                Eng("accLim", "Ограничение углового ускорения", "рад/с²", 2, 0, 1e5, p => p.AccLim, (p, v) => p.AccLim = v),
                Eng("mech", "Множитель механических потерь", "", 2, 0, 12, p => p.Mech, (p, v) => p.Mech = v) } },
            new ParamSection { Title = "Турбина", Rows = {
                Eng("torque", "Множитель момента турбины", "", 2, 0, 2.5, p => p.Torque, (p, v) => p.Torque = v) } },
            new ParamSection { Title = "Подшипники", Rows = {
                Eng("heat", "Множитель теплоотвода", "", 2, 0, 5, p => p.Heat, (p, v) => p.Heat = v),
                Eng("vibAdd", "Добавка вибрации", "g", 2, 0, 30, p => p.VibAdd, (p, v) => p.VibAdd = v),
                RoE("_tb", "Температура подшипников", "K", 0, e => e.Pf.T).Zone(550, 650, "при 700 K подшипники клинит и двигатель гаснет: на красном есть считаные секунды"),
                RoE("_vb", "Вибрация", "g", 2, e => Math.Max(e.Pf.Vib, e.Po.Vib)).Zone(2.5, 9, "выше 2,5 g крыльчатка изнашивается, при 12 g разрушается") } },
            new ParamSection { Title = "Насос горючего", Rows = {
                Eng("fHead", "Множитель напора", "", 2, 0, 2, p => p.FHead, (p, v) => p.FHead = v),
                Eng("fEff", "Множитель КПД", "", 2, 0.2, 1.3, p => p.FEff, (p, v) => p.FEff = v) } },
            new ParamSection { Title = "Насос окислителя", Rows = {
                Eng("oHead", "Множитель напора", "", 2, 0, 2, p => p.OHead, (p, v) => p.OHead = v),
                Eng("oEff", "Множитель КПД", "", 2, 0.2, 1.3, p => p.OEff, (p, v) => p.OEff = v) } } } },
        new ParamGroup { Name = "Клапаны", Sections = {
            new ParamSection { Title = "Магистраль горючего", Rows = {
                Eng("valveF", "Положение клапана", "", 2, 0, 1.2, p => p.ValveF, (p, v) => p.ValveF = v) } },
            new ParamSection { Title = "Магистраль окислителя", Rows = {
                Eng("valveOx", "Положение клапана", "", 2, 0, 1.2, p => p.ValveOx, (p, v) => p.ValveOx = v) } },
            new ParamSection { Title = "Контроль", Rows = {
                RoE("_mr", "Соотношение компонентов", "", 2,
                    e => e.Po.Q * Pump.RHO_OX / Math.Max(1e-6, e.Pf.Q * Pump.RHO_F)),
                RoE("_qf", "Расход горючего", "кг/с", 1, e => e.Pf.Q * Pump.RHO_F),
                RoE("_qo", "Расход окислителя", "кг/с", 1, e => e.Po.Q * Pump.RHO_OX) } } } },
        new ParamGroup { Name = "Баки", Sections = {
            new ParamSection { Title = "Наддув", Rows = {
                Eng("pTankF", "Уставка наддува горючего", "кПа", 1, 80, 900, p => p.PTankF, (p, v) => p.PTankF = v),
                Eng("pTankOx", "Уставка наддува окислителя", "кПа", 1, 80, 900, p => p.PTankOx, (p, v) => p.PTankOx = v),
                RoV("_pf", "Фактическое давление горючего", "кПа", 1, v => v.Tanks.F.P / 1000).Zone(280, 200, "паспортное 350 кПа: ниже насос начинает кавитировать", true),
                RoV("_po", "Фактическое давление окислителя", "кПа", 1, v => v.Tanks.O.P / 1000).Zone(300, 220, "паспортное 380 кПа: ниже насос начинает кавитировать", true),
                RoV("_gas", "Газ в подушках", "кг", 0, v => v.Tanks.F.Mg + v.Tanks.O.Mg),
                RoV("_copv", "Остаток баллонов", "%", 1, v => v.Tanks.Copv / v.Tanks.Copv0 * 100).Zone(25, 8, "без наддува давление на входе насосов падает и начинается кавитация", true) } },
            new ParamSection { Title = "Остатки", Rows = {
                RoV("_fill", "Заправка ступени", "%", 1, v => v.Fill * 100),
                RoV("_prop", "Топливо на борту", "т", 1, v => v.Prop / 1000),
                RoV("_hdr", "Головные баки", "т", 1, v => v.HdrLeft / 1000),
                RoV("_dv", "Запас характеристической скорости", "м/с", 0,
                    v => (v.Kind == Kind.Booster ? 347 : 373) * Const.G0 *
                         Math.Log(v.Mass / Math.Max(1, v.Mass - v.Prop))) } } } },
        new ParamGroup { Name = "Камера", Sections = {
            new ParamSection { Title = "Режимы", Rows = {
                Eng("minThr", "Предел дросселирования камеры", "%", 1, 20, 100, p => p.MinThr, (p, v) => p.MinThr = v),
                Eng("cf", "Множитель коэффициента тяги", "", 3, 0.5, 1.2, p => p.Cf, (p, v) => p.Cf = v) } },
            new ParamSection { Title = "Состояние", Rows = {
                RoE("_pc", "Давление в камере", "МПа", 2, e => e.Pc),
                RoE("_F", "Тяга двигателя", "кН", 1, e => e.F / 1000) } } } },
        new ParamGroup { Name = "Сопло", Sections = {
            new ParamSection { Title = "Геометрия", Rows = {
                Eng("aeK", "Множитель площади среза", "", 3, 0.5, 2, p => p.AeK, (p, v) => p.AeK = v) } },
            new ParamSection { Title = "Состояние", Rows = {
                RoV("_pa", "Внешнее давление", "кПа", 3, v => v.Pa / 1000),
                RoE("_los", "Потеря тяги на срезе", "кН", 1,
                    e => e.Spec.Ae * e.P.AeK * e.Veh.Pa / 1000) } } } },
        new ParamGroup { Name = "Двигатель", Sections = {
            new ParamSection { Title = "Выход на режим", Rows = {
                Eng("tau", "Постоянная выхода на режим", "с", 2, 0.05, 5, p => p.Tau, (p, v) => p.Tau = v),
                Eng("ignDelay", "Задержка запуска", "с", 2, 0, 5, p => p.IgnDelay, (p, v) => p.IgnDelay = v) } },
            new ParamSection { Title = "Ресурс", Rows = {
                RoE("_wf", "Износ крыльчатки горючего", "", 3, e => e.Pf.Wear).Zone(0.3, 0.7, "износ съедает напор и КПД: при 100 % насос теряет треть напора"),
                RoE("_wo", "Износ крыльчатки окислителя", "", 3, e => e.Po.Wear).Zone(0.3, 0.7, "износ съедает напор и КПД: при 100 % насос теряет треть напора"),
                RoE("_sp", "Уровень режима", "", 2, e => e.Spool) } } } },
        new ParamGroup { Name = "Рулевой тракт", Sections = {
            new ParamSection { Title = "Привод качания", Rows = {
                Veh("gimLim", "Предел отклонения", "°", 1, 0, 25,
                    v => double.IsNaN(v.GimLim) ? v.Spec.Gimbal * Const.R2D : v.GimLim,
                    (v, x) => v.GimLim = x),
                Veh("rcsK", "Множитель тяги ДМТ", "", 2, 0, 4, v => v.RcsK, (v, x) => v.RcsK = x) } },
            new ParamSection { Title = "Автомат стабилизации", Rows = {
                Veh("kp", "Коэффициент по углу", "", 2, 0, 6, v => v.Kp, (v, x) => v.Kp = x),
                Veh("kd", "Коэффициент по угловой скорости", "", 2, 0, 8, v => v.Kd, (v, x) => v.Kd = x) } },
            new ParamSection { Title = "Состояние", Rows = {
                RoV("_gim", "Текущее отклонение", "°", 2, v => v.Gimbal * Const.R2D),
                RoV("_om", "Угловая скорость", "°/с", 2, v => v.Om * Const.R2D),
                RoV("_th", "Тангаж", "°", 2, v => v.Th * Const.R2D) } } } },
    };
}
