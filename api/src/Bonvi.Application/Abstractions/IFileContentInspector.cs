namespace Bonvi.Application.Abstractions;

/// <summary>Faylning MAZMUNIGA qarab aniqlangan haqiqiy turi.</summary>
public enum DetectedFileKind
{
    Pdf = 1,
    Jpeg = 2,
    Png = 3,
    Webp = 4,
}

/// <summary>
/// Fayl mazmunini tekshirish porti — xodim hujjatlari uchun.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <see cref="IImageProcessor"/>dan alohida.</b> Rasm quvuri har
/// doim markazdan kvadrat qirqib, JPEG'ga qayta kodlaydi — bu xodim
/// SURATI uchun to'g'ri, lekin HUJJAT uchun yaroqsiz: shartnoma yoki
/// buyruq skanini qirqib tashlash uning mazmunini buzardi. Bu port faylni
/// O'ZGARTIRMAYDI, faqat mazmuniga qarab turini aniqlaydi — asl baytlar
/// o'zgarmasdan saqlanadi.
/// </para>
/// <para>
/// <b>Kengaytmaga ham, mijoz yozgan <c>Content-Type</c>ga ham ishonilmaydi.</b>
/// Rasm formatlari (JPEG/PNG/WebP) haqiqiy dekoder orqali tekshiriladi —
/// xuddi <see cref="IImageProcessor"/> ichida bo'lgani kabi. PDF esa fayl
/// imzosi (magic bytes, <c>%PDF-</c>) bo'yicha: server PDF'ni hech qachon
/// ochib o'qimaydi yoki bajarmaydi (faqat saqlaydi va qaytaradi), shu
/// sababli to'liq tuzilishni tekshirish ortiqcha — buzilgan PDF foydalanuvchi
/// uni ochganda o'zi ko'rinadi, bu xavfsizlik emas, ma'lumot sifati masalasi.
/// </para>
/// </remarks>
public interface IFileContentInspector
{
    /// <summary>
    /// Fayl baytlarini tekshiradi. Tanilmagan yoki buzilgan bo'lsa —
    /// <c>null</c>.
    /// </summary>
    DetectedFileKind? Detect(byte[] content);
}
