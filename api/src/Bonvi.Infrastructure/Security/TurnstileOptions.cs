namespace Bonvi.Infrastructure.Security;

/// <summary>
/// Onlayn anketaning bot tekshiruvi — Cloudflare Turnstile sozlamasi (<c>Turnstile</c> bo'limi,
/// SPEC-planshet-anketa §2.3.2).
/// </summary>
/// <remarks>
/// <para>
/// Muhit: <c>Turnstile__SiteKey</c>, <c>Turnstile__SecretKey</c>. Vidjetni egasi Cloudflare
/// panelida o'zi yaratadi (<c>hr.bonvi.uz</c> va <c>anketa.bonvi.uz</c> uchun) va IKKALA kalitni
/// prod <c>.env</c> ga o'zi yozadi — maxfiy kalit chat, kod va jurnal orqali o'tmaydi.
/// </para>
/// <para>
/// <b>Ikkalasi bo'sh — tekshiruv o'chiq</b> (dev, testlar, egasi kalit qo'ygunicha). Faqat bittasi
/// berilgan bo'lsa ham O'CHIQ: ochiq kalitsiz forma vidjetni chiza olmaydi, maxfiy kalitsiz server
/// tokenni tekshira olmaydi — yoqilsa, birorta ham anketa qabul qilinmasdi. Bu holat ishga
/// tushishda ogohlantirish bilan jurnalga yoziladi (<see cref="Problem"/>).
/// </para>
/// </remarks>
public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";

    /// <summary>Vidjetning OCHIQ kaliti — formaga seans javobida beriladi.</summary>
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>Maxfiy kalit — faqat Cloudflare'ga tekshiruv so'rovida. ⚠️ Hech qachon jurnalga yozilmaydi.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Ikkala kalit ham berilgan — tekshiruv yoqiq.</summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(SiteKey) && !string.IsNullOrWhiteSpace(SecretKey);

    /// <summary>
    /// Yarim sozlama (faqat bitta kalit) — ishga tushishdagi ogohlantirish matni; aks holda <c>null</c>.
    /// Matnda kalitning o'zi YO'Q, faqat qaysi biri yetishmayotgani.
    /// </summary>
    public string? Problem => (string.IsNullOrWhiteSpace(SiteKey), string.IsNullOrWhiteSpace(SecretKey)) switch
    {
        (false, true) => "Turnstile__SecretKey berilmagan",
        (true, false) => "Turnstile__SiteKey berilmagan",
        _ => null,
    };
}
