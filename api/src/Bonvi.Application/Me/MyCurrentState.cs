using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Me;

/// <summary>
/// Belgilash kartasining «hozir» holati — oxirgi belgilardan.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Qaysi belgilar.</b> Oxirgi <see cref="AttendanceRules.BoardLookbackHours"/>
/// soatdagilari — «Hozir binoda» taxtasidagi bilan bir xil oyna: 18 soatdan
/// eski kirish «ichkarida» degani emas (chiqish yozilmagan yoki turniket
/// aylanib o'tilgan). Kalendar kuni bo'yicha EMAS: tungi smenada 22:00 dagi
/// kirish ertalab 06:00 da ham «ichkaridasiz» degan javobni berishi kerak.
/// </para>
/// <para>
/// <b>Yo'nalishi noma'lum o'tish</b> (turniket aytmagan) holatni ALMASHTIRADI
/// — hisoblashdagi «navbat bilan» taxminning o'zi: kirishdan keyingisi chiqish,
/// chiqishdan keyingisi kirish.
/// </para>
/// </remarks>
public static class MyCurrentState
{
    /// <summary>
    /// Yopilgan smena shuncha vaqt davomida hali «joriy» katak bo'lib turadi.
    /// </summary>
    /// <remarks>
    /// Kun oynasining keyingi cheti bilan bir xil son
    /// (<see cref="AttendanceRules.DayWindowPostHours"/>): 08:00 da tugagan tungi
    /// smena 14:00 gacha bosh ekranda turadi — xodim hozirgina bosgan chiqishini
    /// ko'radi, keyin esa bugungi (bo'sh) katak ochiladi.
    /// </remarks>
    private static readonly TimeSpan ClosedShiftGrace = TimeSpan.FromHours(AttendanceRules.DayWindowPostHours);

    /// <summary>Oxirgi belgilardan «hozir» ni chiqaradi.</summary>
    /// <param name="recent">Qarash oynasidagi belgilar (katagi bilan), vaqt tartibida.</param>
    /// <param name="now">Joriy lahza.</param>
    /// <param name="today">Bugungi Toshkent sanasi.</param>
    public static MyCurrentStateDto From(IReadOnlyList<MyAttributedMark> recent, DateTimeOffset now, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(recent);

        var inside = false;
        MyAttributedMark? last = null;

        foreach (var mark in recent.OrderBy(item => item.Mark.At))
        {
            inside = mark.Mark.Direction switch
            {
                PassDirection.In => true,
                PassDirection.Out => false,
                _ => !inside,
            };

            last = mark;
        }

        if (last is null)
        {
            return new MyCurrentStateDto(today, PassDirection.In, null);
        }

        // Ochiq smena — o'z katagida; yaqinda yopilgani ham (yuqoridagi izoh).
        // Aks holda — bugun: kechagi 18:00 dagi chiqish ertalabki bosh ekranni
        // kechagi kun bilan to'ldirmasligi kerak.
        var date = inside || now - last.Mark.At <= ClosedShiftGrace ? last.Date : today;

        return new MyCurrentStateDto(date, inside ? PassDirection.Out : PassDirection.In, last.Mark);
    }
}
