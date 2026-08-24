using System.Globalization;

namespace Bonvi.Etl.Excel;

/// <summary>
/// Excel sana seriyasini <see cref="DateOnly"/> ga aylantiradi.
/// </summary>
/// <remarks>
/// <para>
/// Bonvi bloknotida sanalar <b>son</b> bo'lib yotibdi: <c>46212</c>.
/// Bu Excel'ning ichki ko'rinishi — 1900-yildan boshlab o'tgan kunlar soni.
/// </para>
/// <para>
/// <b>Nega ikkita boshlang'ich nuqta.</b> Excel'da mashhur xato bor:
/// u 1900-yilni kabisa yili deb hisoblaydi va mavjud bo'lmagan
/// <c>1900-02-29</c> sanasini 60-seriya sifatida saqlaydi. Shu sababli:
/// </para>
/// <list type="bullet">
///   <item>1…59 — haqiqiy sana, boshlanish nuqtasi <c>1899-12-31</c>;</item>
///   <item>60 — <b>mavjud bo'lmagan sana</b>, ETL uni rad etadi;</item>
///   <item>61 va undan katta — boshlanish nuqtasi <c>1899-12-30</c>
///         (Excel'ning ortiqcha kuni shu tarzda qoplanadi).</item>
/// </list>
/// <para>
/// Ko'p kutubxona faqat <c>1899-12-30</c> ni ishlatadi va 1900-yil
/// fevralidagi sanalarni bir kun surib yuboradi. Bizda bunday sana yo'q,
/// lekin qoida to'g'ri yozilgani muhim: keyin kim bu kodni ko'chirsa,
/// xato birga ko'chmasin.
/// </para>
/// </remarks>
public static class ExcelDate
{
    /// <summary>1…59 oralig'idagi seriyalar uchun boshlanish nuqtasi.</summary>
    private static readonly DateOnly EarlyEpoch = new(1899, 12, 31);

    /// <summary>61 va undan katta seriyalar uchun boshlanish nuqtasi.</summary>
    private static readonly DateOnly Epoch = new(1899, 12, 30);

    /// <summary>Excel'ning mavjud bo'lmagan «1900-02-29» seriyasi.</summary>
    private const int PhantomLeapDaySerial = 60;

    /// <summary>
    /// HR ma'lumotida ishonchli deb qabul qilinadigan eng kichik seriya
    /// (<c>1927-05-18</c>).
    /// </summary>
    /// <remarks>
    /// Bonvi bloknotida sana ustuniga ba'zan boshqa qiymat tushib qolgan:
    /// «ishlagan kunlar soni» (69), tartib raqami (5). Ular ham to'g'ri
    /// seriya bo'lib chiqadi va 1900-yilning boshiga aylanadi. Bunday
    /// «sana» yolg'on ma'lumot bo'lardi — shu sababli rad etiladi.
    /// </remarks>
    public const int MinimumPlausibleSerial = 10_000;

    /// <summary>Excel qo'llab-quvvatlaydigan eng katta seriya (<c>9999-12-31</c>).</summary>
    private const int MaximumSerial = 2_958_465;

    /// <summary>
    /// Seriya raqamini sanaga aylantiradi. Qiymat haqiqiy sana bermasa —
    /// <c>null</c> (ETL uni bo'sh qoldiradi va hisobotga yozadi).
    /// </summary>
    public static DateOnly? FromSerial(double serial)
    {
        // Kasr qism — kun ichidagi vaqt. Sana uchun ahamiyatsiz.
        var days = (int)Math.Floor(serial);

        if (days is < 1 or > MaximumSerial)
        {
            return null;
        }

        if (days == PhantomLeapDaySerial)
        {
            return null;
        }

        return days < PhantomLeapDaySerial
            ? EarlyEpoch.AddDays(days)
            : Epoch.AddDays(days);
    }

    /// <summary>
    /// Katakdan sanani o'qiydi: haqiqiy sana tipi, seriya raqami yoki
    /// <c>«12.08.2017»</c> ko'rinishidagi matn.
    /// </summary>
    /// <param name="cell">Excel katagi.</param>
    /// <param name="requirePlausible">
    /// <c>true</c> bo'lsa <see cref="MinimumPlausibleSerial"/> dan kichik
    /// seriyalar rad etiladi. Ko'chirishda doim <c>true</c>.
    /// </param>
    public static DateOnly? Read(ExcelCell cell, bool requirePlausible = true)
    {
        if (cell.Date is { } typed)
        {
            return typed;
        }

        if (cell.Number is { } number)
        {
            if (requirePlausible && number < MinimumPlausibleSerial)
            {
                return null;
            }

            return FromSerial(number);
        }

        if (cell.Text.Length == 0)
        {
            return null;
        }

        // Matn ko'rinishidagi sana: «17.02.2025», «2025-02-17», «17/02/2025».
        string[] formats = ["dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy"];

        if (DateOnly.TryParseExact(cell.Text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        // Matn bo'lib saqlangan seriya: «45413».
        if (double.TryParse(cell.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var textSerial))
        {
            if (requirePlausible && textSerial < MinimumPlausibleSerial)
            {
                return null;
            }

            return FromSerial(textSerial);
        }

        return null;
    }
}
