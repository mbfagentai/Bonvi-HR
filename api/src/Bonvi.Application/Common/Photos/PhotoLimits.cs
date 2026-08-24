using System.Security.Cryptography;
using System.Text;
using Bonvi.Application.Abstractions;

namespace Bonvi.Application.Common.Photos;

/// <summary>
/// Kvadrat suratga qo'yilgan cheklovlar — bitta joyda.
/// </summary>
/// <remarks>
/// <para>
/// Qiymatlar shu yerda turadi, chunki ular <b>ikki qatlamda</b> kerak:
/// use-case yuklangan faylni tekshiradi, HTTP qatlami esa shu chegaradan
/// katta tanani umuman o'qimaslik uchun so'rov hajmini cheklaydi. Ikki joyda
/// alohida raqam yozilsa, ular ertami-kechmi bir-biridan uzoqlashadi.
/// </para>
/// <para>
/// <b>Nega bo'limga bog'liq emas.</b> Xodim surati ham, hisob surati ham
/// bir xil quvurdan o'tadi: bir xil chegara, bir xil o'lcham, bir xil
/// versiya belgisi. Cheklovlar bo'lim ichida qolsa, ikkinchi bo'lim uni
/// nusxalashga majbur bo'lardi — va bir kun kelib «xodimda 10 MB, hisobda
/// 5 MB» degan tushunarsiz farq paydo bo'lardi.
/// </para>
/// <para>
/// <b>512×512 nega yetarli.</b> Rasm faqat kartada, ro'yxat qatoridagi
/// kichik doirada va yuqori paneldagi avatarda ko'rinadi; bundan kattasi
/// diskni va trafikni behuda band qiladi. Manba surat esa saqlanmaydi.
/// </para>
/// </remarks>
public static class PhotoLimits
{
    /// <summary>Yuklanadigan faylning eng katta hajmi (bayt) — 10 MB.</summary>
    public const long MaxBytes = 10L * 1024 * 1024;

    /// <summary>Saqlanadigan rasmning tomoni (piksel).</summary>
    public const int SquareSize = 512;

    /// <summary>Saqlangan rasmning MIME turi — hamma surat JPEG'ga keltiriladi.</summary>
    public const string StoredContentType = "image/jpeg";

    /// <summary>
    /// Saqlashga beriladigan fayl nomi.
    /// </summary>
    /// <remarks>
    /// Foydalanuvchi bergan nom bu yerga HECH QACHON tushmaydi:
    /// <c>IFileStorage</c> kalitni o'zi yasaydi va bu nomdan faqat
    /// kengaytmani oladi. Aks holda «../../etc/passwd» kabi nom
    /// saqlash yo'liga qo'shilib ketishi mumkin edi.
    /// </remarks>
    public const string StoredFileName = "photo.jpg";

    /// <summary>Qabul qilinadigan MIME turlari.</summary>
    /// <remarks>
    /// Bu — faqat BIRINCHI, arzon filtr: mijoz yuborgan sarlavha dalil emas.
    /// Haqiqiy tekshiruv <see cref="IImageProcessor"/> ichida, fayl mazmuni
    /// bo'yicha bajariladi.
    /// </remarks>
    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
        };

    /// <summary>E'lon qilingan MIME turi ro'yxatdami.</summary>
    /// <remarks>
    /// Sarlavhada «image/jpeg; charset=binary» kabi qo'shimcha parametr
    /// bo'lishi mumkin — solishtirishdan oldin u kesib tashlanadi.
    /// </remarks>
    public static bool IsAllowedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var separator = contentType.IndexOf(';', StringComparison.Ordinal);
        var value = separator < 0 ? contentType : contentType[..separator];

        return AllowedContentTypes.Contains(value.Trim());
    }

    /// <summary>
    /// Saqlash kalitidan barqaror versiya belgisi (ETag) yasaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kalitning O'ZI berilmaydi: u ichki saqlash manzili va uni sarlavhada
    /// oshkor qilish saqlash tuzilishini tashqariga chiqaradi.
    /// </para>
    /// <para>
    /// Xesh aynan kalitdan olinadi, fayl mazmunidan emas: har bir yangi
    /// yuklash yangi kalit beradi, ya'ni belgi ham o'zgaradi — mijozdagi
    /// eski nusxa o'zidan-o'zi eskiradi. Fayl mazmunini xeshlash uchun esa
    /// uni har so'rovda to'liq o'qish kerak bo'lardi.
    /// </para>
    /// </remarks>
    public static string VersionOf(string photoKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(photoKey));

        // 16 bayt (128 bit) tasodifiy to'qnashuv uchun ortig'i bilan yetarli,
        // sarlavha esa ikki barobar qisqa bo'ladi.
        return Convert.ToHexStringLower(hash.AsSpan(0, 16));
    }
}
