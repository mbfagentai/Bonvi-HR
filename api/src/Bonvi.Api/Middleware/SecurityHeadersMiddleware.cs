using System.Globalization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Bonvi.Api.Middleware;

/// <summary>
/// Har bir javobga xavfsizlik sarlavhalarini qo'shadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega middleware, endpoint filtri emas:</b> sarlavhalar <i>hamma</i> javobda
/// bo'lishi kerak — 404, 429, 500 va statik faylda ham. Endpoint filtri faqat
/// muvaffaqiyatli marshrutlangan so'rovga tegadi, ya'ni eng xavfli holatlarni
/// (marshrut topilmadi, istisno) qamrab olmaydi.
/// </para>
/// <para>
/// <b>Nega <see cref="HttpResponse.OnStarting(Func{Task})"/>:</b> istisno ishlovchisi
/// (<c>UseExceptionHandler</c>) xatoni tutganda javobni <c>Clear()</c> qiladi va
/// sarlavhalarni o'chiradi. Agar biz sarlavhalarni oldindan yozsak — ular yo'qoladi.
/// <c>OnStarting</c> qayta chaqiruvi esa javob tanasi yozila boshlaganda, ya'ni
/// <c>Clear()</c> dan KEYIN ishlaydi. Shu sababli 500-javobda ham sarlavhalar joyida.
/// </para>
/// <para>
/// <b>Konveyerdagi o'rni:</b> eng birinchi (faqat <c>UseForwardedHeaders</c> dan keyin).
/// HSTS <c>Request.IsHttps</c> ga qaraydi, u esa <c>X-Forwarded-Proto</c> qayta ishlangach
/// to'g'ri bo'ladi.
/// </para>
/// </remarks>
public sealed class SecurityHeadersMiddleware
{
    /// <summary>
    /// API javoblari uchun eng qat'iy siyosat.
    /// </summary>
    /// <remarks>
    /// JSON API hech qanday resurs yuklamaydi — shuning uchun <c>default-src 'none'</c>.
    /// Bu sarlavha JSON'ning o'ziga ta'sir qilmaydi, lekin agar kimdir javobni
    /// brauzerda HTML sifatida ochishga majburlasa (masalan XSS orqali reflected
    /// kontent), skript ishga tushmaydi. <c>frame-ancestors 'none'</c> —
    /// clickjacking'ga qarshi <c>X-Frame-Options</c> ning zamonaviy ekvivalenti.
    /// </remarks>
    private const string ApiCsp =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    /// <summary>
    /// Scalar hujjat sahifasi uchun yumshatilgan siyosat (FAQAT Development).
    /// </summary>
    /// <remarks>
    /// Scalar o'z JS/CSS'ini jsDelivr CDN'idan yuklaydi va inline stil ishlatadi.
    /// <c>default-src 'none'</c> bilan sahifa bo'm-bo'sh ochiladi. Bu yon berish
    /// Production'da YO'Q: u yerda hujjat sahifasi umuman ulanmaydi.
    /// </remarks>
    private const string HujjatCsp =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
        "font-src 'self' data: https://fonts.gstatic.com https://cdn.jsdelivr.net; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self'; " +
        "worker-src 'self' blob:; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    /// <summary>
    /// Brauzer qurilma imkoniyatlarini o'chirish.
    /// </summary>
    /// <remarks>
    /// HR API'ga kamera, mikrofon yoki geolokatsiya hech qachon kerak emas.
    /// Bo'sh ro'yxat <c>()</c> — "hech kimga ruxsat yo'q, o'zimga ham" degani.
    /// </remarks>
    private const string PermissionsPolicy =
        "accelerometer=(), autoplay=(), camera=(), display-capture=(), " +
        "encrypted-media=(), geolocation=(), gyroscope=(), magnetometer=(), " +
        "microphone=(), midi=(), payment=(), usb=(), xr-spatial-tracking=()";

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;
    private readonly SecurityHeadersOptions _options;
    private readonly string _hstsQiymati;
    private readonly PathString _apiPrefiksi;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IHostEnvironment environment,
        IOptions<SecurityHeadersOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _next = next;
        _environment = environment;
        _options = options.Value;
        _hstsQiymati = HstsQiymatiniYig(_options);
        _apiPrefiksi = new PathString(_options.ApiPathPrefix);
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(() =>
        {
            Qollash(context);
            return Task.CompletedTask;
        });

        return _next(context);
    }

    private static string HstsQiymatiniYig(SecurityHeadersOptions options)
    {
        var soniya = (long)TimeSpan.FromDays(options.HstsMaxAgeDays).TotalSeconds;
        var qiymat = string.Create(
            CultureInfo.InvariantCulture,
            $"max-age={soniya}");

        if (options.HstsIncludeSubDomains)
        {
            qiymat += "; includeSubDomains";
        }

        // preload — brauzerlarning tayyor ro'yxatiga kirish. Bu QAYTARIB
        // BO'LMAYDIGAN qadam: domen yillar davomida faqat HTTPS'da qoladi.
        // Shu sababli standart holatda o'chirilgan, ataylab yoqiladi.
        if (options.HstsPreload)
        {
            qiymat += "; preload";
        }

        return qiymat;
    }

    private void Qollash(HttpContext context)
    {
        var request = context.Request;
        var headers = context.Response.Headers;

        // Kestrel «Server: Kestrel» yozadi — bu server turini oshkor qiladi va
        // hujumchiga ma'lum zaifliklarni qidirish uchun boshlang'ich beradi.
        // Asosiy o'chirish Kestrel sozlamasida (AddServerHeader = false),
        // bu yerda esa teskari proksi qo'shib qo'ygan bo'lsa ham olib tashlanadi.
        headers.Remove(HeaderNames.Server);
        headers.Remove("X-Powered-By");
        headers.Remove("X-AspNet-Version");

        // MIME-sniffing'ni to'xtatadi: brauzer JSON'ni HTML deb "taxmin qilmaydi".
        headers["X-Content-Type-Options"] = "nosniff";

        // Eski brauzerlar uchun clickjacking himoyasi (CSP frame-ancestors dubli).
        headers["X-Frame-Options"] = "DENY";

        // Tashqi saytga o'tganda to'liq URL (unda xodim ID'si bo'lishi mumkin)
        // Referer'da ketmasin — faqat domen qoladi.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        headers["Permissions-Policy"] = PermissionsPolicy;

        // Adobe Flash/PDF crossdomain.xml siyosatini butunlay taqiqlaydi.
        headers["X-Permitted-Cross-Domain-Policies"] = "none";

        // Boshqa oynadan (window.opener) bu kontekstga tegishni to'xtatadi.
        headers["Cross-Origin-Opener-Policy"] = "same-origin";

        headers["Content-Security-Policy"] = HujjatSahifasimi(request.Path) ? HujjatCsp : ApiCsp;

        // HSTS faqat HTTPS'da va faqat Production'da. Development'da http://localhost
        // uchun yuborilsa — brauzer domenni eslab qoladi va keyin mahalliy ishlash
        // buziladi (localhost hamma loyihada bir xil domen!).
        if (_options.EnableHsts && request.IsHttps && _environment.IsProduction())
        {
            headers[HeaderNames.StrictTransportSecurity] = _hstsQiymati;
        }

        // HR javoblarida pasport, JSHSHIR, oylik bor. Ular brauzer diskiga yoki
        // oradagi proksiga yozilmasligi kerak — umumiy kompyuterda keyingi
        // foydalanuvchi "orqaga" tugmasi bilan ko'rib qolmasin.
        // Endpoint o'zi Cache-Control belgilagan bo'lsa (masalan ma'lumotnoma) tegmaymiz.
        if (_options.NoStoreForApi
            && request.Path.StartsWithSegments(_apiPrefiksi, StringComparison.OrdinalIgnoreCase)
            && StringValues.IsNullOrEmpty(headers.CacheControl))
        {
            headers.CacheControl = "no-store, no-cache, must-revalidate";
            headers.Pragma = "no-cache";
        }
    }

    private bool HujjatSahifasimi(PathString path)
    {
        // Hujjat sahifasi umuman Production'da ochilmaydi, shu sababli
        // yumshatilgan CSP ham faqat Development'da beriladi.
        if (!_environment.IsDevelopment())
        {
            return false;
        }

        foreach (var prefiks in _options.DocumentationPathPrefixes)
        {
            if (path.StartsWithSegments(prefiks, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Xavfsizlik sarlavhalari sozlamalari (<c>Security:Headers</c> bo'limi).
/// </summary>
public sealed class SecurityHeadersOptions
{
    /// <summary>appsettings.json dagi bo'lim yo'li.</summary>
    public const string SectionName = "Security:Headers";

    /// <summary>HSTS yuborilsinmi (baribir faqat HTTPS + Production'da qo'llanadi).</summary>
    public bool EnableHsts { get; set; } = true;

    /// <summary>HSTS amal qilish muddati (kun). 365 kun — sanoat odati.</summary>
    public int HstsMaxAgeDays { get; set; } = 365;

    /// <summary>Quyi domenlar ham HTTPS'ga majburlansinmi.</summary>
    public bool HstsIncludeSubDomains { get; set; } = true;

    /// <summary>Brauzerlarning preload ro'yxatiga da'vo. Qaytarib bo'lmaydi — ehtiyot bo'ling.</summary>
    public bool HstsPreload { get; set; }

    /// <summary>API javoblariga <c>Cache-Control: no-store</c> qo'yilsinmi.</summary>
    public bool NoStoreForApi { get; set; } = true;

    /// <summary>Qaysi yo'l prefiksi "API" hisoblanadi.</summary>
    public string ApiPathPrefix { get; set; } = "/api";

    /// <summary>Yumshatilgan CSP beriladigan hujjat sahifalari (faqat Development).</summary>
    public IList<string> DocumentationPathPrefixes { get; } = ["/scalar", "/openapi"];
}
