namespace Bonvi.Bot.Conversation;

/// <summary>
/// Bot suhbatining tili.
/// </summary>
/// <remarks>
/// <para>
/// Ikkitasi ATAYLAB: tizimdagi ma'lumotning deyarli hammasi kirillda
/// (lavozimlar, bo'limlar, mavjud xodimlarning F.I.Sh.), lekin yosh
/// nomzodlar lotinda yozadi. Til birinchi qadamda so'raladi va suhbat
/// oxirigacha o'zgarmaydi.
/// </para>
/// <para>
/// <b>Rus tili qo'shildi (2026-08-30, foydalanuvchi qarori).</b> MBF ning
/// hozirgi boti ham ruscha gapiradi va uning tavsifi butunlay ruscha —
/// ya'ni ruschada yozadigan nomzodlar haqiqatan bor. Uchinchi til
/// alifbo emas, boshqa TIL: kirill va lotin bir xil gapning ikki
/// yozuvi, ruscha esa alohida tarjima.
/// </para>
/// <para>
/// ⚠️ <b>Matnlarda <c>_ =></c> tarmog'i lotinni bildiradi</b> va bu
/// yerda qo'shilgan har bir yangi til uchun ALOHIDA tarmoq yozilishi
/// shart. Aks holda yangi til jimgina lotincha matn ko'rsatib
/// qo'yardi — kompilyator bunday xatoni ushlay olmaydi.
/// </para>
/// </remarks>
public enum BotLanguage
{
    /// <summary>Ўзбекча (кирилл).</summary>
    Cyrillic = 1,

    /// <summary>O'zbekcha (lotin).</summary>
    Latin = 2,

    /// <summary>Русский.</summary>
    Russian = 3,
}

/// <summary>
/// Til va tizimdagi kodi orasidagi moslik.
/// </summary>
/// <remarks>
/// Kodlar tizimning qolgan qismidagi bilan AYNAN bir xil
/// (<c>web/src/shared/i18n</c> dagi fayl nomlari). Bot o'ziga alohida
/// belgi o'ylab topsa, nomzodning tili bazada boshqa shaklda yotardi
/// va uni bir kun solishtirish kerak bo'lganda ikkalasini moslashtirish
/// kerak bo'lardi.
/// </remarks>
public static class BotLanguages
{
    /// <summary>Tilning tizimdagi kodi.</summary>
    public static string Code(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "uz-Cyrl",
        BotLanguage.Russian => "ru",
        _ => "uz-Latn",
    };

    /// <summary>
    /// Kodni tilga qaytaradi. Tanimasa — kirill.
    /// </summary>
    /// <remarks>
    /// Kirill tayanch til: tizimdagi ma'lumotning deyarli hammasi shu
    /// yozuvda. Noma'lum kod uchun istisno tashlash noto'g'ri bo'lardi —
    /// nomzod rad etilgani haqidagi xabarni umuman olmay qolardi.
    /// </remarks>
    public static BotLanguage Parse(string? code) => code?.Trim() switch
    {
        "uz-Latn" => BotLanguage.Latin,
        "ru" => BotLanguage.Russian,
        _ => BotLanguage.Cyrillic,
    };
}
