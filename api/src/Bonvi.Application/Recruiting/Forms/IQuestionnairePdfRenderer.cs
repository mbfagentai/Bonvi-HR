using Bonvi.Domain.Catalog;

namespace Bonvi.Application.Recruiting.Forms;

/// <summary>Bitta variant — PDF'da «☒ Telegram» yoki «☐ OLX.uz».</summary>
/// <param name="Label">Variant matni.</param>
/// <param name="Chosen">Nomzod shuni tanlaganmi.</param>
public sealed record QuestionnairePdfChoice(string Label, bool Chosen)
{
    /// <summary>
    /// «Qayerdan bildingiz?» variantlari: FAOL kanallar alifbo tartibida, tanlangani
    /// faolsizlantirilgan bo'lsa — u ham (SPEC-qayerdan-bilgan §5).
    /// </summary>
    /// <remarks>
    /// Ro'yxat chizish PAYTIDAGI ma'lumotnomadan: HR keyin PDF'ni ochganda yangi qo'shilgan
    /// kanal ☐ bo'lib chiqadi, tanlangani esa nomi o'zgargan bo'lsa ham ☒ — identifikator
    /// bo'yicha. Kanallar o'chirilmaydi (faqat faolsizlantiriladi), ya'ni tanlangan kanal
    /// doim topiladi.
    /// </remarks>
    /// <param name="channels">Ma'lumotnomaning HAMMA kanallari, alifbo tartibida.</param>
    /// <param name="chosen">Anketadagi tanlov.</param>
    public static IReadOnlyList<QuestionnairePdfChoice> ReferralChannels(
        IEnumerable<ReferralChannel> channels,
        Guid? chosen)
    {
        ArgumentNullException.ThrowIfNull(channels);

        return
        [
            .. channels
                .Where(channel => channel.IsActive || channel.Id == chosen)
                .Select(channel => new QuestionnairePdfChoice(channel.Name, channel.Id == chosen)),
        ];
    }
}

/// <summary>
/// Anketani PDF'ga chizish uchun hamma narsa — tayyor, oddiy qiymatlar.
/// </summary>
/// <param name="Answers">TEKSHIRILGAN javoblar.</param>
/// <param name="Number">«Anketa №».</param>
/// <param name="SubmittedAt">Yuborilgan lahza — TOSHKENT vaqtida (chizuvchi vaqt mintaqasini bilmaydi).</param>
/// <param name="VacancyTitle">«Lavozim» katagi: vakansiya nomi yoki «Umumiy».</param>
/// <param name="Language">Nomzod anketani qaysi tilda to'ldirgan.</param>
/// <param name="Photo">Surat baytlari (JPEG); topilmasa <c>null</c> — katak bo'sh qoladi.</param>
/// <param name="Signature">Imzo baytlari (PNG); topilmasa <c>null</c> — chiziq bo'sh qoladi.</param>
/// <param name="ReferralChoices">
/// 8-bo'limning 1-savoli — «qayerdan bildingiz?» variantlari
/// (<see cref="QuestionnairePdfChoice.ReferralChannels"/>). Aniqlik javoblarning o'zida.
/// </param>
public sealed record QuestionnairePdfModel(
    QuestionnaireFormAnswers Answers,
    int Number,
    DateTimeOffset SubmittedAt,
    string VacancyTitle,
    string Language,
    byte[]? Photo,
    byte[]? Signature,
    IReadOnlyList<QuestionnairePdfChoice> ReferralChoices);

/// <summary>
/// HR-F02 anketasini qog'oz blanka ko'rinishida PDF qiladi (SPEC §6.3).
/// </summary>
/// <remarks>
/// Infrastructure'da amalga oshiriladi (PDF kutubxonasi, shriftlar) — Application
/// faqat modelni beradi. Holatsiz: bitta chaqiruv, bitta fayl.
/// </remarks>
public interface IQuestionnairePdfRenderer
{
    /// <summary>PDF baytlarini qaytaradi.</summary>
    /// <remarks>
    /// Asinxron — bir vaqtdagi chizishlar soni cheklangan (amalga oshirishdagi navbat), ya'ni
    /// chaqiruv o'z navbatini kutishi mumkin.
    /// </remarks>
    Task<byte[]> RenderAsync(QuestionnairePdfModel model, CancellationToken cancellationToken = default);
}
