using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bonvi.Api.Extensions;

/// <summary>
/// Tezlik cheklovi (rate limiting) siyosatlarining nomlari.
/// </summary>
/// <remarks>
/// Nomlar satr sifatida endpoint'larda yozilsa — bitta harf xatosi cheklovni
/// jimgina o'chirib qo'yadi (noma'lum siyosat nomi ishga tushishda xato beradi,
/// lekin <c>RequireRateLimiting</c> ni umuman yozishni unutish sezilmaydi).
/// Konstanta orqali kamida imlo xatosi kompilyatsiyada tutiladi.
/// </remarks>
public static class RateLimitPolicies
{
    /// <summary>Kirish (login) — bitta IP uchun juda qat'iy.</summary>
    public const string Login = "login";

    /// <summary>Oddiy API so'rovlari — foydalanuvchi yoki IP bo'yicha.</summary>
    public const string Api = "api";

    /// <summary>Eksport/hisobot — og'ir amallar uchun alohida, kichik chegara.</summary>
    public const string Export = "export";

    /// <summary>
    /// Turniket/yuz terminallarining hodisa qabuli — manba IP bo'yicha.
    /// </summary>
    /// <remarks>
    /// Kalit IP, foydalanuvchi emas: bu yo'lda JWT yo'q, token esa bo'linma
    /// tanlanadigan paytda hali tekshirilmagan — unga qarab bo'lish
    /// hujumchiga har so'rovda yangi «bo'linma» ochish imkonini berardi.
    /// </remarks>
    public const string Ingest = "ingest";
}

/// <summary>
/// Tezlik cheklovi sozlamalari (<c>RateLimiting</c> bo'limi).
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>appsettings.json dagi bo'lim nomi.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Umumiy (global) cheklov yoqilganmi. <c>null</c> — muhitga qarab hal qilinadi:
    /// <c>Testing</c> dan tashqari hamma joyda yoqiq.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>Kirish endpoint'i uchun: daqiqada 5 urinish.</summary>
    public RateLimitWindow Login { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    /// <summary>Umumiy API: daqiqada 120 so'rov, siljiydigan oyna.</summary>
    public RateLimitWindow Api { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60, SegmentsPerWindow = 6 };

    /// <summary>Eksport/hisobot: daqiqada 5 ta.</summary>
    public RateLimitWindow Export { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    /// <summary>
    /// Qurilma hodisalari qabuli: bitta IP dan daqiqada 300 so'rov.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hisob: kuniga ~5 400 o'tish, smena almashinuvida sekundiga 4–6 tagacha.
    /// 300/daqiqa — o'rtacha oqimdan ~50 barobar katta, ya'ni NAT ortidagi
    /// butun bino bitta manzildan kelsa ham smena almashinuvida o'zini o'zi
    /// to'smaydi, lekin suiiste'molni baribir chegaralaydi.
    /// </para>
    /// <para>
    /// ⚠️ Chegaradan oshgan qurilma 429 oladi va hodisani o'z xotirasida
    /// saqlab, keyinroq qayta yuboradi — o'tish yo'qolmaydi, takror esa
    /// zararsiz (<c>ux_attendance_passes_device_dedupe</c>).
    /// </para>
    /// </remarks>
    public RateLimitWindow Ingest { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };

    /// <summary>
    /// Qat'iy kirish cheklovi avtomatik qo'llanadigan yo'llar.
    /// </summary>
    /// <remarks>
    /// Bu "himoya to'ri": endpoint muallifi <c>RequireRateLimiting</c> yozishni
    /// unutsa ham, parol tanlashga qarshi cheklov baribir ishlaydi.
    /// </remarks>
    /// <remarks>
    /// ⚠️ Xodim ilovasining kirishi HAM shu yerda, va u bu ro'yxatdagi eng
    /// muhim yozuv: 950 ta hisobning logini bir-biriga ma'lum telefon
    /// raqamlari, blok esa 5 xatodan keyin yonadi. Ya'ni cheklovsiz
    /// «istalgan hamkasbni 15 daqiqaga qulflab qo'yish» ochiq kanal bo'lardi
    /// (ma'lumot modeli §B.3). Sxema buni to'sa olmaydi — faqat shu ro'yxat.
    /// </remarks>
    public IList<string> LoginPaths { get; } = ["/api/auth/login", "/api/employee-auth/login"];

    /// <summary>
    /// Cheklovdan butunlay ozod yo'llar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sog'liq tekshiruvini cheklab bo'lmaydi: orkestrator uni har necha soniyada
    /// so'raydi va 429 olsa konteynerni "o'lik" deb qayta ishga tushiradi.
    /// </para>
    /// <para>
    /// Qurilma qabuli ham bu yerda — lekin «cheklovsiz» EMAS: uning o'z,
    /// kengroq siyosati bor (<see cref="RateLimitPolicies.Ingest"/>). Umumiy
    /// cheklov (daqiqada 120, IP bo'yicha) ustiga qo'shilsa, bitta NAT
    /// manzilidan chiqadigan barcha terminallar smena almashinuvida bir-birini
    /// to'sib qo'yardi — va buni aynan eng ko'p odam kirayotgan daqiqada.
    /// </para>
    /// </remarks>
    public IList<string> ExcludedPaths { get; } = ["/health", "/api/attendance/ingest"];
}

/// <summary>Bitta oyna cheklovining sozlamalari.</summary>
public sealed class RateLimitWindow
{
    /// <summary>Oyna ichida ruxsat etilgan so'rovlar soni.</summary>
    public int PermitLimit { get; set; } = 60;

    /// <summary>Oyna uzunligi (soniya).</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Siljiydigan oyna uchun segmentlar soni (faqat sliding window'da).</summary>
    public int SegmentsPerWindow { get; set; } = 6;
}

/// <summary>
/// Tezlik cheklovini DI'ga ulash.
/// </summary>
/// <remarks>
/// <para>
/// .NET ichidagi <c>Microsoft.AspNetCore.RateLimiting</c> ishlatiladi — tashqi
/// paket kerak emas.
/// </para>
/// <para>
/// <b>Navbat yo'q (<c>QueueLimit = 0</c>):</b> chegaradan oshgan so'rov kutib
/// turmasdan darhol 429 oladi. Navbat qoldirilsa, hujum paytida minglab ulanish
/// ochiq qolib serverni yiqitadi — ya'ni cheklovning o'zi hujum vositasiga aylanadi.
/// </para>
/// </remarks>
public static class RateLimitingExtensions
{
    /// <summary>Xato kodi — frontend uni tarjima qiladi.</summary>
    private const string XatoKodi = "rate_limit.oshib_ketdi";

    public static IServiceCollection AddBonviRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var sozlama = new RateLimitingOptions();
        configuration.GetSection(RateLimitingOptions.SectionName).Bind(sozlama);

        // Integratsiya testlari yuzlab so'rovni bir necha soniyada yuboradi va
        // hammasi bitta "IP" dan keladi. Global cheklov yoqiq bo'lsa testlar
        // o'zaro to'siq bo'lib qoladi — nomli siyosatlar esa ishlashda davom etadi.
        var globalYoqilgan = sozlama.Enabled ?? !environment.IsEnvironment("Testing");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            if (globalYoqilgan)
            {
                // Zanjir: birinchi bo'g'in kirish yo'llarini qo'riqlaydi,
                // ikkinchisi qolgan hamma so'rovni. Ikkalasi ham hisoblanadi.
                options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                    KirishQoriqchisi(sozlama),
                    UmumiyQoriqchi(sozlama));
            }

            // Kirish uchun cheklov qat'iyroq, chunki bu yagona parol tanlash
            // mumkin bo'lgan nuqta. Kalit — IP: hujumchida hali foydalanuvchi
            // identifikatori yo'q, shu sababli faqat manba manzili bo'yicha ajratamiz.
            options.AddPolicy(RateLimitPolicies.Login, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    "login:" + Manba(context),
                    _ => QatiyOyna(sozlama.Login)));

            options.AddPolicy(RateLimitPolicies.Api, context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    "api:" + Kim(context),
                    _ => SiljiydiganOyna(sozlama.Api)));

            // Eksport bitta so'rovda butun jadvalni o'qiydi va Excel yig'adi —
            // bu eng qimmat amal. Uni oddiy so'rov bilan bir xil chegarada
            // qoldirish 540 xodimlik hisobotni ketma-ket so'rab bazani band
            // qilib qo'yish imkonini beradi.
            options.AddPolicy(RateLimitPolicies.Export, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    "export:" + Kim(context),
                    _ => QatiyOyna(sozlama.Export)));

            // Qurilma qabuli — faqat manba IP bo'yicha (sababi
            // `RateLimitPolicies.Ingest` izohida). Umumiy cheklovdan bu yo'l
            // chiqarilgan (`ExcludedPaths`), ya'ni ikki cheklov ustma-ust
            // tushmaydi.
            options.AddPolicy(RateLimitPolicies.Ingest, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    "ingest:" + Manba(context),
                    _ => QatiyOyna(sozlama.Ingest)));

            options.OnRejected = RadEtildiAsync;
        });

        return services;
    }

    // ────────────────────────────────────────────────────────────
    //  Bo'linmalar (partition)
    // ────────────────────────────────────────────────────────────

    private static PartitionedRateLimiter<HttpContext> KirishQoriqchisi(RateLimitingOptions sozlama)
        => PartitionedRateLimiter.Create<HttpContext, string>(context =>
            YolMoslashadimi(context.Request.Path, sozlama.LoginPaths)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    "login:" + Manba(context),
                    _ => QatiyOyna(sozlama.Login))
                : RateLimitPartition.GetNoLimiter<string>("chetlab-otildi"));

    private static PartitionedRateLimiter<HttpContext> UmumiyQoriqchi(RateLimitingOptions sozlama)
        => PartitionedRateLimiter.Create<HttpContext, string>(context =>
            YolMoslashadimi(context.Request.Path, sozlama.ExcludedPaths)
                ? RateLimitPartition.GetNoLimiter<string>("chetlab-otildi")
                : RateLimitPartition.GetSlidingWindowLimiter(
                    "api:" + Kim(context),
                    _ => SiljiydiganOyna(sozlama.Api)));

    private static FixedWindowRateLimiterOptions QatiyOyna(RateLimitWindow oyna) => new()
    {
        PermitLimit = oyna.PermitLimit,
        Window = TimeSpan.FromSeconds(oyna.WindowSeconds),
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    };

    private static SlidingWindowRateLimiterOptions SiljiydiganOyna(RateLimitWindow oyna) => new()
    {
        PermitLimit = oyna.PermitLimit,

        // Siljiydigan oyna qat'iy oynadan yaxshiroq: qat'iy oynada foydalanuvchi
        // oyna chegarasida ikki barobar so'rov yuborishi mumkin (oxirgi soniyada
        // 120 + yangi oynaning birinchi soniyasida yana 120).
        Window = TimeSpan.FromSeconds(oyna.WindowSeconds),
        SegmentsPerWindow = Math.Max(1, oyna.SegmentsPerWindow),
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    };

    // ────────────────────────────────────────────────────────────
    //  Kalit yasash
    // ────────────────────────────────────────────────────────────

    /// <summary>Autentifikatsiyadan o'tgan foydalanuvchi, aks holda manba IP.</summary>
    /// <remarks>
    /// Foydalanuvchi bo'yicha ajratish adolatliroq: bitta ofisdagi 50 xodim
    /// bitta NAT IP orqali chiqadi va IP bo'yicha cheklansa bir-birini to'sadi.
    /// </remarks>
    private static string Kim(HttpContext context)
    {
        var foydalanuvchi = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        return string.IsNullOrEmpty(foydalanuvchi)
            ? "ip:" + Manba(context)
            : "u:" + foydalanuvchi;
    }

    /// <summary>
    /// Haqiqiy manba IP.
    /// </summary>
    /// <remarks>
    /// <c>UseForwardedHeaders</c> konveyerda BIRINCHI turishi shart: aks holda
    /// Caddy orqasida hamma so'rov proksining IP'si bilan keladi va bitta
    /// bo'linmaga tushib, butun tashkilotni birdaniga bloklaydi.
    /// </remarks>
    private static string Manba(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "nomalum";

    private static bool YolMoslashadimi(PathString path, IList<string> yollar)
    {
        for (var i = 0; i < yollar.Count; i++)
        {
            if (path.StartsWithSegments(yollar[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // ────────────────────────────────────────────────────────────
    //  Rad etilgan so'rov javobi
    // ────────────────────────────────────────────────────────────

    private static async ValueTask RadEtildiAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        // Javob boshlanib bo'lgan bo'lsa sarlavha qo'shib bo'lmaydi.
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var kutish = KutishVaqti(context.Lease);

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.Headers.RetryAfter =
            kutish.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture);

        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Bonvi.Api.RateLimiting");

        // Bu log xavfsizlik uchun: ketma-ket 429 lar parol tanlash urinishining
        // asosiy belgisi. Audit uchun manba IP saqlanadi.
        TezlikLoglari.CheklovOshdi(
            logger,
            httpContext.Request.Method,
            httpContext.Request.Path.Value ?? "/",
            Manba(httpContext));

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = XatoKodi,
            Detail = "So'rovlar soni ruxsat etilgan chegaradan oshdi. Biroz kutib, qayta urinib ko'ring.",
            Instance = httpContext.Request.Path,
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        problem.Extensions["retryAfterSeconds"] = (int)kutish.TotalSeconds;

        await httpContext.Response
            .WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken)
            .ConfigureAwait(false);
    }

    private static TimeSpan KutishVaqti(RateLimitLease lease)
        => lease.TryGetMetadata(MetadataName.RetryAfter, out var qiymat)
            ? qiymat
            : TimeSpan.FromSeconds(60);
}

/// <summary>
/// Tezlik cheklovi loglari.
/// </summary>
/// <remarks>
/// <c>[LoggerMessage]</c> manba-generatori: satr formatlash faqat log darajasi
/// yoqilgan bo'lsa bajariladi va qutilash (boxing) bo'lmaydi. CA1848 shuni talab qiladi.
/// </remarks>
internal static partial class TezlikLoglari
{
    [LoggerMessage(
        EventId = 5100,
        Level = LogLevel.Warning,
        Message = "Tezlik cheklovi ishga tushdi: {Method} {Path} — manba {Manba}")]
    public static partial void CheklovOshdi(ILogger logger, string method, string path, string manba);
}
