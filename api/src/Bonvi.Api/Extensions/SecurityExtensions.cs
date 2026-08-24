using System.Net;
using Bonvi.Api.Middleware;
using Microsoft.AspNetCore.HttpOverrides;

namespace Bonvi.Api.Extensions;

/// <summary>
/// Xavfsizlik sozlamalari (<c>Security</c> bo'limi).
/// </summary>
public sealed class SecurityOptions
{
    /// <summary>appsettings.json dagi bo'lim nomi.</summary>
    public const string SectionName = "Security";

    /// <summary>
    /// So'rov tanasining maksimal hajmi (bayt). Standart — 30 MB.
    /// </summary>
    /// <remarks>
    /// Cheklov bo'lmasa bitta so'rov bilan diskni to'ldirish yoki xotirani
    /// tugatish mumkin. 30 MB tanlandi, chunki <c>Storage:MaxFileSizeBytes</c>
    /// 25 MB — qolgan 5 MB multipart konvert va metama'lumot uchun zaxira.
    /// </remarks>
    public long MaxRequestBodyBytes { get; set; } = 30L * 1024 * 1024;

    /// <summary>
    /// Nechta proksi qatlamiga ishoniladi (<c>X-Forwarded-For</c> zanjirining oxiridan).
    /// </summary>
    /// <remarks>
    /// Bizda faqat bitta qatlam — Caddy. 1 dan katta qiymat mijoz o'zi qo'shgan
    /// soxta manzilni "haqiqiy" deb qabul qilishiga olib keladi.
    /// </remarks>
    public int ForwardLimit { get; set; } = 1;

    /// <summary>
    /// Ishonchli proksi IP manzillari. Bo'sh bo'lsa — har qanday proksiga ishoniladi.
    /// </summary>
    /// <remarks>
    /// Docker compose'da Caddy konteynerining IP'si har ishga tushishda o'zgaradi,
    /// shu sababli standart holatda ro'yxat bo'sh. Bu XAVFSIZ, chunki prod
    /// compose'da API porti tashqariga umuman chiqarilmagan — so'rov faqat
    /// Caddy orqali kela oladi. Agar API to'g'ridan-to'g'ri ochilsa, bu yerga
    /// proksi manzilini yozish SHART: aks holda hujumchi <c>X-Forwarded-For</c>
    /// ni o'zi yozib, IP bo'yicha cheklov va auditni chetlab o'tadi.
    /// </remarks>
    public IList<string> KnownProxies { get; } = [];
}

/// <summary>
/// Xavfsizlik qatlamini ulash: sarlavhalar, teskari proksi, so'rov hajmi.
/// </summary>
public static class SecurityExtensions
{
    /// <summary>
    /// Xavfsizlik servislarini va Kestrel'ning xavfsizlikka oid cheklovlarini ro'yxatga oladi.
    /// </summary>
    public static WebApplicationBuilder AddBonviSecurity(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<SecurityHeadersOptions>(
            builder.Configuration.GetSection(SecurityHeadersOptions.SectionName));

        var sozlama = new SecurityOptions();
        builder.Configuration.GetSection(SecurityOptions.SectionName).Bind(sozlama);

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Host sarlavhasi ATAYLAB olinmaydi: X-Forwarded-Host ga ishonish
            // "parolni tiklash" havolasini hujumchi domeniga yo'naltirish
            // (host header poisoning) imkonini beradi. Domen sozlamadan olinadi.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = sozlama.ForwardLimit;

            // Standart ro'yxatda faqat localhost bor — Docker tarmog'idagi Caddy
            // unga tushmaydi va sarlavha jimgina e'tiborsiz qoldiriladi.
            // Natijada audit va IP cheklovi noto'g'ri IP bilan ishlagan bo'lardi.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var proksi in sozlama.KnownProxies)
            {
                if (IPAddress.TryParse(proksi, out var manzil))
                {
                    options.KnownProxies.Add(manzil);
                }
            }
        });

        builder.WebHost.ConfigureKestrel(options =>
        {
            // «Server: Kestrel» — bepul razvedka ma'lumoti. O'chiramiz.
            options.AddServerHeader = false;

            options.Limits.MaxRequestBodySize = sozlama.MaxRequestBodyBytes;
        });

        return builder;
    }

    /// <summary>
    /// Xavfsizlik middleware'larini konveyerga qo'shadi. Konveyerda ENG BIRINCHI chaqiriladi.
    /// </summary>
    /// <remarks>
    /// Tartib muhim: avval <c>UseForwardedHeaders</c>, keyin sarlavhalar.
    /// HSTS <c>Request.IsHttps</c> ga qaraydi, u esa <c>X-Forwarded-Proto</c>
    /// qayta ishlangandan keyingina to'g'ri qiymat beradi.
    /// </remarks>
    public static IApplicationBuilder UseBonviSecurity(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseForwardedHeaders();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        return app;
    }
}
