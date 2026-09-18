using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Common;

/// <summary>Davr berilmaganda qaysi oraliq olinadi.</summary>
internal enum PeriodDefault
{
    /// <summary>Bitta kun — <c>to</c> (berilmasa bugun). Jurnal uchun: «bugun kim o'tdi».</summary>
    SingleDay = 0,

    /// <summary><c>to</c> oyining 1-sanasidan <c>to</c> gacha. Hisobotlar uchun: «shu oy hozircha».</summary>
    MonthToDate = 1,
}

/// <summary>
/// Davomat so'rovlarining umumiy parametrlarini tekshiradi va xavfsiz qiymatlarga o'giradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bitta joyda.</b> Jurnal, tabel, ikkala hisobot va to'rtala eksport
/// bir xil qoidalarni (davr, sahifa, saralash) ishlatadi. Eksport ekrandagi
/// so'rov bilan AYNAN bir xil filtrni qurishi kerak (<c>ExportEmployeesQueryHandler</c>
/// precedenti) — qoida ikki joyda yozilsa, ular sekin ajralib ketib, Excel
/// ekrandagidan boshqa qatorlarni bera boshlardi.
/// </para>
/// <para>
/// <b>Filtr — xato, saralash — sukut</b> (<c>RecruitingQueryParsing</c> bilan
/// bir xil qoida): noto'g'ri davr yoki tur natijaning MAZMUNINI o'zgartiradi
/// va aytiladi; noma'lum saralash ustuni jimgina sukutga tushadi.
/// </para>
/// </remarks>
internal static class AttendanceQueryParsing
{
    /// <summary>Sukut bo'yicha sahifa hajmi (SPEC §6: <c>SearchEmployeesQuery</c> bilan bir xil).</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Eng katta sahifa hajmi — undan kattasi eksport orqali olinadi.</summary>
    public const int MaxPageSize = 200;

    /// <summary>Asosiy sahifa panelining sukut sahifa hajmi.</summary>
    /// <remarks>
    /// Boshqa ro'yxatlardagi 25 dan katta: panel bir ekranda diagramma yonidagi
    /// uzun jadval bo'lib o'qiladi (Verifix ekrani ham shunday), va ~540 kishilik
    /// korxona 25 tadan 22 sahifa bo'lib, «kim kelmadi» degan savolni varaqlashga
    /// aylantirardi. Eng katta chegara — umumiy <see cref="MaxPageSize"/>.
    /// </remarks>
    public const int DashboardPageSize = 50;

    /// <summary>Tabel qabul qiladigan eng kichik yil.</summary>
    /// <remarks>
    /// Chegara «haqiqatga to'g'ri kelmaydigan yil» uchun: 0001-yil so'rovi
    /// xato emas, lekin 12 ta bo'sh qatorlik sahifa qaytarib, xatoni
    /// yashirardi.
    /// </remarks>
    public const int MinYear = 2000;

    /// <summary>Tabel qabul qiladigan eng katta yil.</summary>
    public const int MaxYear = 2100;

    public static int Page(int? requested)
        => requested is { } page && page > 0 ? page : 1;

    public static int PageSize(int? requested)
        => Math.Clamp(requested ?? DefaultPageSize, 1, MaxPageSize);

    public static int PageCount(int total, int pageSize)
        => pageSize <= 0 ? 0 : (total + pageSize - 1) / pageSize;

    /// <summary>
    /// Davrni to'ldiradi va tekshiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chegaralar ikki tomondan yopiq (<c>ListIncidents</c> konvensiyasi).
    /// <c>to</c> berilmasa — bugun (Toshkent); <c>from</c> berilmasa —
    /// <paramref name="whenMissing"/> bo'yicha.
    /// </para>
    /// <para>
    /// <see cref="AttendanceRules.MaxRecomputeDays"/> dan uzun davr — xato.
    /// Jurnalga ham qo'llanadi, garchi SPEC uni «hisobot» demasa ham:
    /// jurnal yiliga ~2 mln qator o'sadi va chegarasiz davr butun arxivni
    /// vaqt bo'yicha saralashga olib kelardi.
    /// </para>
    /// </remarks>
    public static Result<(DateOnly From, DateOnly To)> Period(
        DateOnly? from,
        DateOnly? to,
        DateOnly today,
        PeriodDefault whenMissing)
    {
        var end = to ?? today;
        var start = from ?? (whenMissing == PeriodDefault.MonthToDate
            ? new DateOnly(end.Year, end.Month, 1)
            : end);

        if (start > end)
        {
            return AttendanceErrors.PeriodReversed;
        }

        // Ikkala chegara ham kiradi: 1-sentabrdan 1-sentabrgacha — BIR kun.
        if (end.DayNumber - start.DayNumber + 1 > AttendanceRules.MaxRecomputeDays)
        {
            return AttendanceErrors.PeriodTooLong;
        }

        return (start, end);
    }

    /// <summary>Tabel oyi to'g'rimi.</summary>
    public static Result Month(int year, int month)
        => year is < MinYear or > MaxYear || month is < 1 or > 12
            ? AttendanceErrors.MonthInvalid
            : Result.Success();

    /// <summary>
    /// Chetlanish turini o'giradi: bo'sh ham, noma'lum ham — xato (tur majburiy).
    /// </summary>
    /// <remarks>
    /// <c>Enum.TryParse</c> RAQAMNI ham qabul qiladi («99»), shu sababli
    /// <c>IsDefined</c> ham kerak — aks holda sanoqda yo'q qiymat so'rovga
    /// tushib, jimgina bo'sh ro'yxat qaytarardi.
    /// </remarks>
    public static Result<ExceptionKind> ParseExceptionKind(string? raw)
        => !string.IsNullOrWhiteSpace(raw)
            && Enum.TryParse<ExceptionKind>(raw.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
                ? parsed
                : AttendanceErrors.ExceptionKindInvalid;

    /// <summary>Jurnal faqat vaqt bo'yicha saralanadi; sukut — eng yangisi birinchi.</summary>
    /// <remarks>
    /// Boshqa ustun ATAYLAB yo'q: jurnal — xronologiya, va vaqtdan boshqa
    /// tartib 2 mln qatorda indeksga tushmaydigan saralash bo'lardi.
    /// </remarks>
    public static bool PassesDescending(string? raw)
    {
        var (_, descending, empty) = SplitSort(raw);
        return empty || descending;
    }

    /// <summary>Panel faqat ism bo'yicha saralanadi: <c>-name</c> — teskari, qolgan hammasi — A→Z.</summary>
    /// <remarks>
    /// Holat yoki kelish vaqti bo'yicha tartib ATAYLAB yo'q (taxtadagi bilan bir
    /// xil sabab): ekran yangilanganda holat o'zgarib, qatorlar sakrab ketardi va
    /// HR ko'z bilan topgan odamni yo'qotardi. Holat bo'yicha ajratish — filtr.
    /// </remarks>
    public static bool DashboardDescending(string? raw)
    {
        var (value, descending, empty) = SplitSort(raw);
        return !empty && descending && value == "NAME";
    }

    public static (ExceptionSortField Sort, bool Descending) ExceptionSort(string? raw)
    {
        var (value, descending, empty) = SplitSort(raw);

        return empty
            ? (ExceptionSortField.Date, true)
            : value switch
            {
                "EMPLOYEE" => (ExceptionSortField.Employee, descending),
                "MINUTES" => (ExceptionSortField.Minutes, descending),
                _ => (ExceptionSortField.Date, descending),
            };
    }

    public static (SummarySortField Sort, bool Descending) SummarySort(string? raw)
    {
        var (value, descending, empty) = SplitSort(raw);

        return empty
            ? (SummarySortField.Employee, false)
            : value switch
            {
                "WORKEDMINUTES" => (SummarySortField.WorkedMinutes, descending),
                "LATEMINUTES" => (SummarySortField.LateMinutes, descending),
                "ABSENTDAYS" => (SummarySortField.AbsentDays, descending),
                "PROBLEMDAYS" => (SummarySortField.ProblemDays, descending),
                _ => (SummarySortField.Employee, descending),
            };
    }

    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Saralash matnini ustun nomiga va yo'nalishga ajratadi.
    /// </summary>
    /// <remarks>
    /// Ikki shakl (<c>-date</c> va <c>date:desc</c>) — xodimlar va rekruting
    /// ro'yxatlaridagi bilan AYNAN bir xil qoida, aks holda mijozda uch xil
    /// saralash sintaksisi bo'lardi.
    /// </remarks>
    private static (string Value, bool Descending, bool Empty) SplitSort(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (string.Empty, false, true);
        }

        var value = raw.Trim();
        var descending = false;

        if (value.StartsWith('-'))
        {
            descending = true;
            value = value[1..];
        }

        var separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator >= 0)
        {
            descending = value[(separator + 1)..].Equals("desc", StringComparison.OrdinalIgnoreCase);
            value = value[..separator];
        }

        return (value.ToUpperInvariant(), descending, false);
    }
}
