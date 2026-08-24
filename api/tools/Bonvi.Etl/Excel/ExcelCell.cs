using System.Globalization;

namespace Bonvi.Etl.Excel;

/// <summary>
/// Excel katagining ko'chirishga kerak bo'lgan qismi.
/// </summary>
/// <remarks>
/// <para>
/// Nega ClosedXML tipi to'g'ridan-to'g'ri ishlatilmaydi: ko'chirish qoidalari
/// (sana, telefon, alias qidiruvi) <b>sof funksiya</b> bo'lishi kerak — ular
/// bazasiz va Excel faylsiz test qilinadi. ClosedXML tipi testga kirsa,
/// har bir test uchun haqiqiy fayl kerak bo'lardi.
/// </para>
/// <para>
/// <see cref="Number"/> alohida saqlanadi, chunki Excel'da sana ham, JSHSHIR
/// ham, telefon ham <b>son</b> bo'lib yotibdi. Ularni matnga aylantirish
/// yo'lida ma'lumot yo'qoladi: <c>9.05088888E8</c> ko'rinishidagi eksponenta
/// telefon raqami emas.
/// </para>
/// </remarks>
public readonly record struct ExcelCell(string Text, double? Number, DateOnly? Date)
{
    /// <summary>Bo'sh katak.</summary>
    public static readonly ExcelCell Empty = new(string.Empty, null, null);

    /// <summary>Katakda hech narsa yo'q.</summary>
    public bool IsEmpty => Text.Length == 0 && Number is null && Date is null;

    /// <summary>Katakda matn bor (bo'sh emas).</summary>
    public bool HasValue => !IsEmpty;

    /// <summary>Butun son katakmi (JSHSHIR, telefon, sana seriyasi shunday saqlanadi).</summary>
    public bool IsWholeNumber => Number is { } n && Math.Abs(n - Math.Round(n)) < 1e-9;

    /// <summary>
    /// Sondan yasalgan katakni yaratadi.
    /// </summary>
    /// <remarks>
    /// Matn ko'rinishi ATAYLAB eksponentasiz yoziladi: <c>3.15E13</c> emas,
    /// <c>31501914320045</c>. Aks holda JSHSHIR va telefon buziladi.
    /// </remarks>
    public static ExcelCell FromNumber(double value)
        => new(value.ToString("0.############", CultureInfo.InvariantCulture), value, null);

    /// <summary>Matndan yasalgan katak.</summary>
    public static ExcelCell FromText(string? value)
    {
        var text = Normalize(value);
        return text.Length == 0 ? Empty : new ExcelCell(text, null, null);
    }

    /// <summary>Haqiqiy sana tipidagi katak.</summary>
    public static ExcelCell FromDate(DateOnly value)
        => new(value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), null, value);

    /// <summary>
    /// Matnni tozalaydi: chetdagi va ichidagi ortiqcha bo'shliqlar olib tashlanadi.
    /// </summary>
    /// <remarks>
    /// Excel eksportida tabulyatsiya va uzilmaydigan bo'shliq (U+00A0) tez-tez
    /// uchraydi. Ular ko'zga oddiy probeldek ko'rinadi, lekin alias jadvalidagi
    /// kalitga mos kelmaydi va qiymat «noma'lum» bo'lib qolardi.
    /// </remarks>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? string.Empty : string.Join(' ', parts);
    }

    public override string ToString() => Text;
}
