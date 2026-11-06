using Microsoft.AspNetCore.Http.Features;

namespace Bonvi.Api.Extensions;

/// <summary>
/// Tokensiz (anonim) kirish yo'llarida so'rov tanasining hajmi — 16 KB.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida chegara</b> (2026-10-12, xodim ilovasi internetga ochilgach;
/// 2026-09-30 xavfsizlik ko'rigining bandi). Kestrel'ning umumiy chegarasi
/// (<c>SecurityExtensions</c>) fayl yuklash uchun katta. Kirish, yangilash va
/// chiqish esa bir necha yuz baytlik JSON — lekin ularni HAR KIM, tokensiz
/// chaqira oladi. Katta tana bilan yuborilgan minglab urinish xotira va tarmoqni
/// bekorga band qilardi; bu yerda ular o'qilmasdan rad etiladi.
/// </para>
/// <para>
/// <c>Content-Length</c> oshsa — darhol 413. Sarlavhasiz (chunked) tana uchun
/// esa Kestrel chegarasi pasaytiriladi: o'qish chegaradan oshgan lahzada uziladi
/// (<c>BadHttpRequestException</c> 413 → <c>GlobalExceptionHandler</c>).
/// MCP OAuth endpointlaridagi bilan bir xil usul va bir xil son.
/// </para>
/// </remarks>
public static class AnonymousBodyLimitExtensions
{
    /// <summary>Tananing eng katta hajmi (bayt).</summary>
    public const long MaxBodyBytes = 16 * 1024;

    /// <summary>Chegara qo'llanadigan yo'llar (prefiks bo'yicha).</summary>
    /// <remarks>
    /// ⚠️ <c>/api/auth</c> butunligicha EMAS: <c>/api/auth/me/photo</c> — profil rasmini
    /// yuklash (tokenli, megabaytlar). Faqat tokensiz chaqiriladigan uchtasi.
    /// </remarks>
    private static readonly string[] Paths =
    [
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/logout",
        "/api/employee-auth",

        // JSON is bound before the endpoint filter checks the launch data, so it is read without a login.
        "/api/council-app",
    ];

    /// <summary>Konveyerga chegarani qo'shadi — istisno ushlagichidan keyin, tana o'qilishidan oldin.</summary>
    /// <param name="app">Ilova.</param>
    public static IApplicationBuilder UseBonviAnonymousBodyLimit(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            if (Applies(context.Request.Path))
            {
                if (context.Request.ContentLength > MaxBodyBytes)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }

                if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
                {
                    feature.MaxRequestBodySize = MaxBodyBytes;
                }
            }

            await next(context).ConfigureAwait(false);
        });
    }

    private static bool Applies(PathString path)
    {
        foreach (var prefix in Paths)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
