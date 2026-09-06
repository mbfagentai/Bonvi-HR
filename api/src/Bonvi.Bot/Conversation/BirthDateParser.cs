using System.Globalization;

namespace Bonvi.Bot.Conversation;

/// <summary>
/// Nomzod yozgan tug'ilgan sanani o'qiydi (4-qadam).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega qat'iy naqsh.</b> <c>DateTime.Parse</c> muhitning
/// madaniyatiga qarab <c>10.11.1990</c> ni ba'zan 11-oktabr, ba'zan
/// 10-noyabr deb o'qiydi — konteynerning til sozlamasi o'zgargan kuni
/// bot jimgina boshqa sanani yozib qo'yardi. Shu sababli naqsh
/// ATAYLAB sanab berilgan va madaniyat <c>Invariant</c>.
/// </para>
/// <para>
/// <b>Ajratgichlar uch xil.</b> Nomzod nuqta o'rniga chiziqcha yoki
/// qiya chiziq yozishi odatiy hol va bu xato emas — savolni qayta
/// so'rash uchun sabab ham emas.
/// </para>
/// <para>
/// ⚠️ <b>Natija <c>DateOnly</c> va u HECH QACHON siljitilmaydi</b>
/// (<see cref="BotClock"/> izohi): tug'ilgan kun — kun qiymati, unga
/// vaqt mintaqasi tegishli emas. Vaqt mintaqasi faqat «bugun»ni
/// aniqlashda ishlatiladi, ya'ni yoshni sanashda.
/// </para>
/// </remarks>
public static class BirthDateParser
{
    /// <summary>
    /// Eng kichik yosh.
    /// </summary>
    /// <remarks>
    /// O'zbekiston Mehnat kodeksi bo'yicha ishga qabul qilishning quyi
    /// chegarasi. Undan kichik sana — deyarli har doim terish xatosi
    /// (yil o'rniga joriy yil yozilgan).
    /// </remarks>
    public const int MinAge = 14;

    /// <summary>
    /// Eng katta yosh.
    /// </summary>
    /// <remarks>
    /// Chegara ataylab keng: maqsad qariyani rad etish emas, terish
    /// xatosini («1090») ushlash. Serverning o'z chegarasi yanada keng
    /// (100 yil) — bot esa nomzod EKRAN OLDIDA turganda so'raydi va
    /// xatoni shu yerda tuzatish arzonroq.
    /// </remarks>
    public const int MaxAge = 80;

    /// <summary>Qabul qilinadigan yozuvlar — kun, oy, yil tartibida.</summary>
    private static readonly string[] Patterns =
        ["dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy"];

    /// <summary>
    /// Matnni sanaga aylantiradi.
    /// </summary>
    /// <returns>
    /// Tushunilmasa yoki chegaradan chiqsa <c>null</c> — chaqiruvchi
    /// savolni qaytadan beradi.
    /// </returns>
    public static DateOnly? TryParse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        if (!DateTime.TryParseExact(
                input.Trim(),
                Patterns,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return null;
        }

        var birthDate = DateOnly.FromDateTime(parsed);

        // «Bugun» Toshkent bo'yicha: yarim tundan ertalab soat 5 gacha
        // UTC hali KECHAGI kunda turadi va chegara bir kunga surilib
        // ketardi (Toshkent UTC dan besh soat OLDINDA).
        var today = DateOnly.FromDateTime(BotClock.Now.Date);

        if (birthDate > today)
        {
            return null;
        }

        var age = today.Year - birthDate.Year;

        // Tug'ilgan kun bu yil hali kelmagan bo'lsa yosh bir yil kam.
        if (birthDate > today.AddYears(-age))
        {
            age--;
        }

        return age is >= MinAge and <= MaxAge ? birthDate : null;
    }
}
