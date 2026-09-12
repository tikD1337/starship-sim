using System.Collections.Generic;
namespace Starship.Game.Ui;
public static class Phases {
    private static readonly Dictionary<string, string> Con = new() {
        ["idle"] = "Предстартовая", ["ascent"] = "Выведение", ["meco"] = "Горячее разделение",
        ["flip"] = "Разворот", ["boostback"] = "Тормозной импульс", ["coastB"] = "Пассивный участок",
        ["landB"] = "Посадка ускорителя", ["caught"] = "Захвачен башней", ["landed"] = "Посадка выполнена",
        ["crashed"] = "Разрушение", ["ascent2"] = "Работа второй ступени", ["coastS"] = "Пассивный участок",
        ["circ"] = "Довыведение", ["orbit"] = "Орбита", ["deorbit"] = "Сход с орбиты", ["coastD"] = "Спуск",
        ["entryS"] = "Вход в атмосферу", ["flipS"] = "Переворот", ["landS"] = "Посадка корабля",
    };
    private static readonly Dictionary<string, string> Eth = new() {
        ["idle"] = "Предстартовая подготовка", ["ascent"] = "Работа первой ступени",
        ["meco"] = "Горячее разделение ступеней", ["flip"] = "Разворот ускорителя",
        ["boostback"] = "Тормозной импульс", ["coastB"] = "Пассивный участок",
        ["landB"] = "Посадочная жига ускорителя", ["caught"] = "Ускоритель пойман башней",
        ["landed"] = "Посадка выполнена", ["crashed"] = "Потеря аппарата",
        ["ascent2"] = "Работа второй ступени", ["coastS"] = "Пассивный участок",
        ["circ"] = "Довыведение", ["orbit"] = "На орбите", ["deorbit"] = "Сход с орбиты",
        ["coastD"] = "Спуск", ["entryS"] = "Вход в атмосферу", ["flipS"] = "Переворот корабля",
        ["landS"] = "Посадка корабля",
    };
    public static string Console(string mode) => Con.TryGetValue(mode, out string s) ? s : mode;
    public static string Air(string mode) => Eth.TryGetValue(mode, out string s) ? s : mode;
}
