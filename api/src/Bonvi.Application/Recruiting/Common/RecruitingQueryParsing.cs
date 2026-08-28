using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.Common;

/// <summary>
/// So'rov satridagi matnli parametrlarni xavfsiz qiymatlarga o'giradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega sanoq emas, SATR qabul qilinadi.</b> Minimal API sanoqni
/// registrga sezgir bog'laydi va bo'sh qiymatni umuman tushunmaydi:
/// <c>?status=open</c> ham, <c>?status=</c> ham 400 bo'lib qaytardi.
/// Ikkalasi ham oddiy hol — birinchisi qo'lda yozilgan havola, ikkinchisi
/// «filtr tozalandi». Batafsil sabab <c>SearchEmployeesQuery.ParseStatus</c>
/// izohida; bu yerda AYNAN o'sha qoida rekruting filtrlariga qo'llanadi.
/// </para>
/// <para>
/// <b>Filtr — xato, saralash — sukut.</b> Noma'lum holat XATO bo'lib
/// qaytadi (u natijaning mazmunini o'zgartiradi va jimgina butun ro'yxatni
/// ko'rsatsa, HR buni sezmasdi), noma'lum saralash ustuni esa jimgina
/// sukutga tushadi (jadval sarlavhasini bosgan odam 400 ko'rishi kerak
/// emas).
/// </para>
/// </remarks>
internal static class RecruitingQueryParsing
{
    public static Result<VacancyStatus?> ParseVacancyStatus(string? raw)
        => ParseEnum<VacancyStatus>(
            raw,
            "vakansiya.holat_notogri",
            "Holat noto'g'ri. Ruxsat etilgan qiymatlar: Open, Paused, Closed.");

    public static Result<CandidateStatus?> ParseCandidateStatus(string? raw)
        => ParseEnum<CandidateStatus>(
            raw,
            "nomzod.holat_notogri",
            "Holat noto'g'ri. Ruxsat etilgan qiymatlar: InProgress, Rejected, Hired, Withdrawn.");

    public static Result<CandidateSource?> ParseCandidateSource(string? raw)
        => ParseEnum<CandidateSource>(
            raw,
            "nomzod.manba_notogri",
            "Manba noto'g'ri. Ruxsat etilgan qiymatlar: Manual, TelegramBot.");

    public static (VacancySortField Sort, bool Descending) ParseVacancySort(string? raw)
    {
        var (value, descending, empty) = SplitSort(raw);

        return empty
            ? (VacancySortField.OpenedAt, true)
            : value switch
            {
                "POSITION" => (VacancySortField.Position, descending),
                _ => (VacancySortField.OpenedAt, descending),
            };
    }

    public static (CandidateSortField Sort, bool Descending) ParseCandidateSort(string? raw)
    {
        var (value, descending, empty) = SplitSort(raw);

        return empty
            ? (CandidateSortField.CreatedAt, true)
            : value switch
            {
                "NAME" => (CandidateSortField.Name, descending),
                "STAGE" => (CandidateSortField.Stage, descending),
                _ => (CandidateSortField.CreatedAt, descending),
            };
    }

    /// <summary>
    /// Saralash matnini ustun nomiga va yo'nalishga ajratadi.
    /// </summary>
    /// <remarks>
    /// Ikki shakl ham qo'llab-quvvatlanadi (<c>-openedAt</c> va
    /// <c>openedAt:desc</c>) — xodimlar ro'yxatidagi bilan AYNAN bir xil,
    /// aks holda mijozda ikki xil qoida bo'lardi.
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

    /// <summary>
    /// Matnni sanoqqa o'giradi: bo'sh — «filtrsiz», noma'lum — xato.
    /// </summary>
    /// <remarks>
    /// <c>Enum.TryParse</c> RAQAMNI ham qabul qiladi («99» muvaffaqiyatli
    /// bog'lanadi va sanoqda mavjud bo'lmagan qiymat hosil bo'ladi), shu
    /// sababli <c>IsDefined</c> ham kerak.
    /// </remarks>
    private static Result<TEnum?> ParseEnum<TEnum>(string? raw, string code, string message)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Success<TEnum?>(null);
        }

        return !Enum.TryParse<TEnum>(raw.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed)
            ? Error.Validation(code, message)
            : Result.Success<TEnum?>(parsed);
    }
}
