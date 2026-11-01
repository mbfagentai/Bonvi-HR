using Bonvi.Domain.Common;

namespace Bonvi.Application.Recruiting.Forms;

/// <summary>
/// Onlayn anketaning xatolari (SPEC-planshet-anketa §2.1).
/// </summary>
/// <remarks>
/// <para>
/// Kodlar KAM va UMUMIY — har maydonga alohida kod emas: qaysi maydon ekani
/// xato tafsilotidagi <c>field</c> da (<c>familyMembers[0].fullName</c>).
/// Forma tarjima matnini kod bo'yicha, maydonni esa yo'l bo'yicha
/// ko'rsatadi. Yuzga yaqin maydonning har biriga kod — to'rt tilda yuzlab
/// tarjima va ularning hech biri yangi ma'lumot bermasdi.
/// </para>
/// <para>
/// Har bir kod shu yerda to'g'ridan-to'g'ri <c>Error.Validation</c> chaqiruvining
/// birinchi argumenti — tarjima tekshiruvi (<c>./bonvi check</c>) kodlarni aynan
/// shu shakldan topadi (yordamchi metod orqali yozilsa, ko'rmay qolardi).
/// </para>
/// </remarks>
public static class QuestionnaireFormErrors
{
    public static Error Unreadable { get; } =
        Error.Validation("anketa.json", "Anketa javoblari o'qilmadi.");

    public static Error Required { get; } =
        Error.Validation("anketa.majburiy", "Majburiy maydon to'ldirilmagan.");

    public static Error Invalid { get; } =
        Error.Validation("anketa.notogri", "Maydon qiymati noto'g'ri.");

    /// <remarks>Chegara tafsilotda — <c>limit</c>.</remarks>
    public static Error TooLong { get; } =
        Error.Validation("anketa.uzun", "Matn ruxsat etilganidan uzun.");

    public static Error ConsentMissing { get; } =
        Error.Validation("anketa.rozilik", "Tilxatning barcha bandlariga rozilik belgilanishi shart.");

    public static Error SignatureNameMismatch { get; } =
        Error.Validation("anketa.imzo_ism", "Imzo o'rnidagi ism anketadagi familiya va ism bilan mos kelmadi.");

    public static Error PhotoMissing { get; } =
        Error.Validation("anketa.rasm_yoq", "Rasm olinmagan.");

    public static Error SignatureMissing { get; } =
        Error.Validation("anketa.imzo_yoq", "Imzo chizilmagan.");

    public static Error SignatureInvalid { get; } =
        Error.Validation("anketa.imzo_yaroqsiz", "Imzo fayli yaroqsiz.");

    /// <remarks>
    /// Forma suratining BITTA kodi — hajm, tur yoki o'qib bo'lmaslik: forma suratni o'zi
    /// 1600 px JPEG qilib yuboradi, ya'ni bu holatlar faqat qo'lda yasalgan so'rovda bo'ladi va
    /// nomzodga hammasi bir xil «qayta oling» degani.
    /// </remarks>
    public static Error PhotoInvalid { get; } =
        Error.Validation("anketa.rasm_yaroqsiz", "Rasm 5 MB gacha JPEG yoki PNG bo'lishi kerak.");

    /// <remarks>
    /// Boshqarish belgilari, yo'nalishni almashtiruvchi (bidi) va ko'rinmas belgilar ism va
    /// javoblarni HR ekranida va PDF'da yashirincha boshqacha ko'rsatishi mumkin; NUL belgisi esa
    /// bazaga yozilmaydi (500). Bir qatorli maydonda yangi qator ham shu kod. To'ldirilgan
    /// «tuzoq» maydon (<c>website</c>, SPEC §2) ham — botga alohida sabab aytilmaydi.
    /// </remarks>
    public static Error ForbiddenCharacter { get; } =
        Error.Validation("anketa.belgi", "Matnda ruxsat etilmagan belgi bor.");

    /// <remarks>
    /// Bot tekshiruvi (Turnstile, SPEC §2.3.2) o'tmadi: token yo'q, yaroqsiz, muddati o'tgan yoki
    /// Cloudflare javob bermadi — BITTA kod, sababini botga aytmaymiz. <c>field</c> —
    /// <c>turnstileToken</c>; forma vidjetni yangilab, qayta yuborishni taklif qiladi.
    /// </remarks>
    public static Error BotCheckFailed { get; } =
        Error.Validation("anketa.bot_tekshiruvi", "Tekshiruvdan o'tmadi. Sahifani yangilab, qayta yuboring.");

    /// <remarks>
    /// Konstruktor anketasi (SPEC-anketa-konstruktori §5.3): forma yuborgan <c>definitionVersion</c>
    /// bazada yo'q. Forma sahifani yangilaydi, qoralama saqlanib qoladi. <c>field</c> —
    /// <c>definitionVersion</c>.
    /// </remarks>
    public static Error DefinitionOutdated { get; } =
        Error.Validation("anketa.versiya", "Anketa yangilandi. Sahifani yangilang.");

    /// <summary>Xatoga maydon yo'lini qo'shadi.</summary>
    /// <remarks>
    /// ⭐ Yo'l IKKI joyda: javobning <c>detail</c> ida (forma shu bo'yicha tegishli
    /// qadamni ochadi va maydonni belgilaydi — SPEC §2.1) va <c>field</c> kengaytmasida.
    /// Inson o'qiydigan matn formada kod bo'yicha tarjimadan olinadi, ya'ni
    /// <c>detail</c> dagi o'zbekcha jumla u yerda kerak emas.
    /// </remarks>
    /// <param name="error">Xato.</param>
    /// <param name="field">Maydon yo'li — <c>familyMembers[0].fullName</c>.</param>
    /// <param name="limit">Matn chegarasi (faqat <see cref="TooLong"/> uchun).</param>
    public static Error At(this Error error, string field, int? limit = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var details = new Dictionary<string, object>(StringComparer.Ordinal) { ["field"] = field };

        if (limit is { } max)
        {
            details["limit"] = max;
        }

        return (error with { Message = field }).WithDetails(details);
    }
}
