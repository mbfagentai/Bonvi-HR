using Bonvi.Application.Common.Photos;

namespace Bonvi.Api.Common;

/// <summary>
/// Surat endpoint'larining HTTP tafsilotlari — bitta joyda.
/// </summary>
/// <remarks>
/// Xodim surati va hisob surati bir xil sarlavhalar bilan beriladi: bir xil
/// <c>ETag</c>, bir xil kesh siyosati, bir xil 304. Bu qoidalar har bir
/// endpoint'da qo'lda yozilsa, biri o'zgarganda ikkinchisi eskirib qolardi —
/// va «rasm almashtirdim, lekin eskisi turibdi» degan xato tug'ilardi.
/// </remarks>
internal static class PhotoHttp
{
    /// <summary>
    /// Suratni javobga yozadi: 200 (baytlar) yoki 304 (mijozdagi nusxa dolzarb).
    /// </summary>
    /// <remarks>
    /// Sarlavhalar 200 va 304 uchun BIR XIL: 304 javobida ham <c>ETag</c>
    /// bo'lishi kerak, aks holda mijoz uni qayta so'rayveradi.
    /// Kesh SHAXSIY (<c>private</c>) — surat shaxsiy ma'lumot va umumiy
    /// proksida saqlanmasligi kerak.
    /// </remarks>
    public static IResult Write(HttpContext context, PhotoContent photo)
        => Write(context, photo, "private, max-age=86400");

    /// <summary>
    /// Xodim ilovasi uchun: <see cref="Write(HttpContext, PhotoContent)"/> ning o'zi, lekin kesh
    /// HAR SAFAR serverdan so'raladi (<c>private, no-cache</c>).
    /// </summary>
    /// <remarks>
    /// Telefon nusxani saqlaydi, lekin ishlatishdan oldin <c>If-None-Match</c> bilan so'raydi —
    /// rasm o'zgarmagan bo'lsa javob baribir baytsiz 304, ya'ni arzon. Bir kunlik
    /// <c>max-age</c> esa huquq olingandan keyin ham ishlardi: boshliqlikdan olingan yoki
    /// qamrovdan chiqqan odamning rasmi, ishdan ketgan xodimning o'z rasmi bir kun davomida
    /// so'rovsiz ko'rinib turardi — rad etish (403/404) faqat so'rovda ishlaydi. HR rasmlari
    /// o'zgarmaydi (<see cref="Write(HttpContext, PhotoContent)"/>).
    /// </remarks>
    public static IResult WriteRevalidated(HttpContext context, PhotoContent photo)
        => Write(context, photo, "private, no-cache");

    private static IResult Write(HttpContext context, PhotoContent photo, string cacheControl)
    {
        context.Response.Headers.ETag = $"\"{photo.Version}\"";
        context.Response.Headers.CacheControl = cacheControl;

        return photo.Content is null
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Stream(photo.Content, photo.ContentType);
    }

    /// <summary>
    /// <c>If-None-Match</c> sarlavhasidan versiya belgisini ajratib oladi.
    /// </summary>
    /// <remarks>
    /// Sarlavha qiymati qo'shtirnoq ichida keladi, «zaif» belgi esa <c>W/</c>
    /// bilan boshlanadi. Use-case bu formatni bilmasligi kerak — tozalash
    /// HTTP qatlamida qoladi.
    /// </remarks>
    public static string? ReadKnownVersion(HttpRequest request)
    {
        var header = request.Headers.IfNoneMatch;

        if (header.Count == 0 || string.IsNullOrWhiteSpace(header[0]))
        {
            return null;
        }

        var tag = header[0]!.Trim();

        if (tag.StartsWith("W/", StringComparison.Ordinal))
        {
            tag = tag[2..];
        }

        return tag.Trim('"');
    }
}
