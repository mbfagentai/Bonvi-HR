namespace Bonvi.Application.Recruiting.Forms;

/// <summary>
/// Onlayn anketaning bot tekshiruvi — port (SPEC-planshet-anketa §2.3.2).
/// </summary>
/// <remarks>
/// <para>
/// Amalga oshirilishi — Cloudflare Turnstile (Infrastructure): forma oxirgi ekranda ko'rinmas
/// vidjetdan token oladi, server uni Cloudflare'da tekshiradi. Nega port: Application qatlami
/// HTTP mijozini bilmaydi, testlar esa Cloudflare'siz soxta tekshiruvchi qo'yadi.
/// </para>
/// <para>
/// <b>O'chiq holat</b> — kalitlar berilmagan (dev, testlar, egasi kalit qo'ygunicha):
/// <see cref="SiteKey"/> <c>null</c>, seans javobida <c>turnstileSiteKey: null</c> va yuborish
/// token so'ramaydi. Faqat bitta kalit berilgan bo'lsa ham o'chiq (ishga tushishda ogohlantirish).
/// </para>
/// </remarks>
public interface IQuestionnaireBotCheck
{
    /// <summary>Vidjetning OCHIQ kaliti (formaga beriladi); <c>null</c> — tekshiruv o'chiq.</summary>
    string? SiteKey { get; }

    /// <summary>
    /// Token haqiqiymi — faqat <see cref="SiteKey"/> bo'lganda chaqiriladi.
    /// </summary>
    /// <remarks>
    /// YOPIQ-SUKUT: token yo'q, buzuq, muddati o'tgan yoki ishlatilgan, Cloudflare javob bermadi
    /// yoki vaqt tugadi — hammasi <c>false</c> (egasi maksimal himoya so'ragan; qoralama saqlanadi,
    /// nomzod qayta yuboradi). Tekshiruv o'chiq bo'lsa ham <c>false</c> — xato chaqiruv eshikni
    /// ochib qo'ymasin. Token va maxfiy kalit hech qachon jurnalga tushmaydi.
    /// </remarks>
    /// <param name="token">Vidjet bergan token (multipart <c>turnstileToken</c>).</param>
    /// <param name="clientIp">Mijozning haqiqiy IP'si (Cloudflare'ga qo'shimcha belgi), bo'lmasa <c>null</c>.</param>
    /// <param name="idempotencyKey">
    /// Yuborish identifikatori (<c>clientSubmissionId</c>): AYNAN o'sha yuborishni qayta tekshirish
    /// (poyga, javobi yo'qolgan so'rov) «token ishlatilgan» deb rad etilmasin.
    /// </param>
    /// <param name="cancellationToken">Bekor qilish.</param>
    Task<bool> VerifyAsync(string? token, string? clientIp, Guid idempotencyKey, CancellationToken cancellationToken);
}
