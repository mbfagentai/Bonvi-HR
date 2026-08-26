using Bonvi.Application.Abstractions;

namespace Bonvi.Application.Common.Documents;

/// <summary>
/// Xodim hujjatiga qo'yilgan cheklovlar — bitta joyda.
/// </summary>
/// <remarks>
/// <para>
/// <b>20 MB nega rasmdan (10 MB, <c>PhotoLimits</c>) katta.</b> Ko'p
/// sahifali skanlangan shartnoma yoki buyruq PDF holida bir necha
/// megabaytga, ba'zan 15-20 MB ga yetadi. Bundan kattasi odatda xato
/// fayl (butun papka yoki video tasodifan tanlangan) — cheksiz qoldirilsa,
/// bitta noto'g'ri yuklash butun diskni band qilib qo'yishi mumkin edi.
/// </para>
/// <para>
/// <b>Qabul qilinadigan turlar.</b> HR hujjatlari amalda ikki toifa:
/// skanerdan yoki telefondan olingan RASM (JPEG/PNG/WebP — xodim rasmi
/// bilan bir xil ro'yxat) va tayyor PDF (shartnoma, buyruq nusxasi).
/// Word/Excel ataylab YO'Q: ular ZIP asosida va mazmun bo'yicha
/// (kengaytmasiz) ishonchli ajratish uchun ichki tuzilmani (masalan
/// <c>[Content_Types].xml</c>) ochib o'qish kerak bo'lardi — HR jarayonida
/// bunday fayl deyarli uchramaydi (buyruqlar ham skan qilinadi), shu
/// sababli bu murakkablik hozircha kerak emas.
/// </para>
/// </remarks>
public static class DocumentLimits
{
    /// <summary>Bitta faylning eng katta hajmi (bayt) — 20 MB.</summary>
    public const long MaxBytes = 20L * 1024 * 1024;

    /// <summary>Saqlashga beriladigan kengaytma — aniqlangan turdan, mijoz nomidan EMAS.</summary>
    public static string ExtensionFor(DetectedFileKind kind) => kind switch
    {
        DetectedFileKind.Pdf => ".pdf",
        DetectedFileKind.Jpeg => ".jpg",
        DetectedFileKind.Png => ".png",
        DetectedFileKind.Webp => ".webp",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Noma'lum hujjat turi."),
    };

    /// <summary>Saqlanadigan MIME turi — aniqlangan turdan.</summary>
    public static string ContentTypeFor(DetectedFileKind kind) => kind switch
    {
        DetectedFileKind.Pdf => "application/pdf",
        DetectedFileKind.Jpeg => "image/jpeg",
        DetectedFileKind.Png => "image/png",
        DetectedFileKind.Webp => "image/webp",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Noma'lum hujjat turi."),
    };
}
