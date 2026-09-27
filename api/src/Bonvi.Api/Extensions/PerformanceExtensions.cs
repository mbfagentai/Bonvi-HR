using System.IO.Compression;
using System.Security.Claims;
using Bonvi.Api.Common;
using Bonvi.Application.System.Queries.GetSystemInfo;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Primitives;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Bonvi.Api.Extensions;

/// <summary>Chiqish keshi (output cache) siyosatlarining nomlari.</summary>
public static class OutputCachePolicies
{
    /// <summary>
    /// O'zgarmas ma'lumotnomalar: bo'limlar, lavozimlar, mamlakatlar.
    /// </summary>
    /// <remarks>
    /// <b>DIQQAT:</b> bu siyosat FAQAT shaxsiy ma'lumot bo'lmagan, hamma uchun
    /// bir xil ro'yxatlarga qo'yiladi. Xodim kartochkasi, ish haqi yoki hujjat
    /// endpoint'iga qo'yilsa — bir foydalanuvchining ma'lumoti boshqasiga
    /// ko'rsatiladi. Kesh kaliti rollar bo'yicha ajratiladi, lekin bu
    /// foydalanuvchi darajasidagi himoyani ALMASHTIRMAYDI.
    /// </remarks>
    public const string Malumotnoma = "malumotnoma";
}

/// <summary>So'rov vaqti cheklovi siyosatlarining nomlari.</summary>
public static class RequestTimeoutPolicies
{
    /// <summary>Uzoq davom etadigan amallar: eksport, ommaviy import, hisobot.</summary>
    public const string Uzun = "uzun";
}

/// <summary>Yuklamaga chidamlilik sozlamalari (<c>Performance</c> bo'limi).</summary>
public sealed class PerformanceOptions
{
    /// <summary>appsettings.json dagi bo'lim nomi.</summary>
    public const string SectionName = "Performance";

    /// <summary>Oddiy so'rovning maksimal davomiyligi (soniya).</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>Og'ir amallar uchun (<see cref="RequestTimeoutPolicies.Uzun"/>) davomiylik.</summary>
    public int LongRequestTimeoutSeconds { get; set; } = 180;

    /// <summary>Ma'lumotnoma keshining amal qilish muddati (soniya).</summary>
    public int ReferenceCacheSeconds { get; set; } = 300;

    /// <summary>Javob siqilsinmi.</summary>
    public bool CompressResponses { get; set; } = true;

    /// <summary>
    /// HTTPS ustida ham siqilsinmi.
    /// </summary>
    /// <remarks>
    /// HTTPS + siqish BREACH hujumiga yo'l ochishi mumkin. Bizda xavf past:
    /// autentifikatsiya <b>Bearer token</b> orqali (cookie emas), shu sababli
    /// hujumchi brauzerni o'zi nomidan autentifikatsiyalangan so'rov yuborishga
    /// majburlay olmaydi. Agar kelajakda cookie'ga o'tilsa — buni <c>false</c>
    /// qilish kerak.
    /// </remarks>
    public bool CompressOverHttps { get; set; } = true;

    /// <summary>Kestrel darajasidagi cheklovlar.</summary>
    public KestrelLimitsOptions Kestrel { get; set; } = new();
}

/// <summary>Kestrel cheklovlari.</summary>
public sealed class KestrelLimitsOptions
{
    /// <summary>Bir vaqtda ochiq TCP ulanishlar soni.</summary>
    /// <remarks>
    /// 540 xodimlik tashkilot uchun 500 ulanish yetarlidan ortiq. Cheklov
    /// bo'lmasa, ulanish "sel"i xotirani tugatib butun konteynerni yiqitadi;
    /// cheklov bilan ortiqcha ulanish shunchaki navbatda kutadi.
    /// </remarks>
    public int MaxConcurrentConnections { get; set; } = 500;

    /// <summary>WebSocket/SSE kabi yuksaltirilgan ulanishlar soni.</summary>
    public int MaxConcurrentUpgradedConnections { get; set; } = 100;

    /// <summary>
    /// So'rov tanasini o'qishning minimal tezligi (bayt/soniya).
    /// </summary>
    /// <remarks>
    /// Slowloris hujumi: mijoz ulanadi va tanani bir baytdan yuboradi —
    /// ulanish soatlab band bo'lib qoladi. Minimal tezlik shart qilinsa,
    /// bunday ulanish uziladi. 240 bayt/s — ASP.NET Core standarti.
    /// </remarks>
    public int MinRequestBodyDataRateBytesPerSecond { get; set; } = 240;

    /// <summary>Minimal tezlik talab qilinmaydigan boshlang'ich davr (soniya).</summary>
    public int MinDataRateGracePeriodSeconds { get; set; } = 10;

    /// <summary>Bo'sh turgan ulanish qancha ushlab turiladi (soniya).</summary>
    public int KeepAliveTimeoutSeconds { get; set; } = 120;

    /// <summary>So'rov sarlavhalari to'liq kelishi uchun beriladigan vaqt (soniya).</summary>
    public int RequestHeadersTimeoutSeconds { get; set; } = 20;

    /// <summary>Barcha sarlavhalarning umumiy hajmi (bayt).</summary>
    public int MaxRequestHeadersTotalSizeBytes { get; set; } = 32 * 1024;
}

/// <summary>
/// Yuklamaga chidamlilik: siqish, kesh, sog'liq tekshiruvi, vaqt cheklovi.
/// </summary>
public static class PerformanceExtensions
{
    /// <summary>Sog'liq tekshiruvi tegi — <c>/health/ready</c> shu teg bo'yicha filtrlaydi.</summary>
    private const string TayyorTegi = "ready";

    private static readonly string[] TayyorTeglari = [TayyorTegi];

    /// <summary>Siqiladigan qo'shimcha MIME turlari.</summary>
    /// <remarks>
    /// XLSX/PDF/rasm ATAYLAB yo'q: ular allaqachon siqilgan, qayta siqish
    /// protsessorni yeydi va hajmni kamaytirmaydi.
    /// </remarks>
    private static readonly string[] QoshimchaMimeTurlari =
    [
        "application/problem+json",
        "application/x-ndjson",
        "text/csv",
    ];

    public static WebApplicationBuilder AddBonviPerformance(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var sozlama = new PerformanceOptions();
        builder.Configuration.GetSection(PerformanceOptions.SectionName).Bind(sozlama);

        builder.KestrelCheklovlari(sozlama.Kestrel);
        builder.Services.JavobSiqish(sozlama);
        builder.Services.ChiqishKeshi(builder.Configuration, sozlama);
        builder.Services.SorovVaqtiCheklovi(sozlama);
        builder.Services.SoglikTekshiruvlari();

        return builder;
    }

    // ────────────────────────────────────────────────────────────
    //  Kestrel
    // ────────────────────────────────────────────────────────────

    private static void KestrelCheklovlari(this WebApplicationBuilder builder, KestrelLimitsOptions sozlama)
        => builder.WebHost.ConfigureKestrel(options =>
        {
            var limits = options.Limits;

            limits.MaxConcurrentConnections = sozlama.MaxConcurrentConnections;
            limits.MaxConcurrentUpgradedConnections = sozlama.MaxConcurrentUpgradedConnections;
            limits.MaxRequestHeadersTotalSize = sozlama.MaxRequestHeadersTotalSizeBytes;
            limits.KeepAliveTimeout = TimeSpan.FromSeconds(sozlama.KeepAliveTimeoutSeconds);
            limits.RequestHeadersTimeout = TimeSpan.FromSeconds(sozlama.RequestHeadersTimeoutSeconds);

            var grace = TimeSpan.FromSeconds(sozlama.MinDataRateGracePeriodSeconds);

            limits.MinRequestBodyDataRate = new MinDataRate(
                bytesPerSecond: sozlama.MinRequestBodyDataRateBytesPerSecond,
                gracePeriod: grace);

            // Javobni ataylab sekin o'qib ulanishni band qilish ham xuddi shunday hujum.
            limits.MinResponseDataRate = new MinDataRate(
                bytesPerSecond: sozlama.MinRequestBodyDataRateBytesPerSecond,
                gracePeriod: grace);
        });

    // ────────────────────────────────────────────────────────────
    //  Javob siqish
    // ────────────────────────────────────────────────────────────

    private static void JavobSiqish(this IServiceCollection services, PerformanceOptions sozlama)
    {
        if (!sozlama.CompressResponses)
        {
            return;
        }

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = sozlama.CompressOverHttps;

            // Brotli birinchi: uni qo'llab-quvvatlaydigan brauzer shuni tanlaydi,
            // gzip esa eskilari uchun zaxira bo'lib qoladi.
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();

            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(QoshimchaMimeTurlari);

            /*
              JONLI OQIM SIQILMAYDI. Siqish bufer talab qiladi: kelgan
              baytlar to'planib turadi va faqat blok to'lganda yoki javob
              yopilganda yuboriladi. `text/event-stream` esa hech qachon
              yopilmaydi — natijada bildirishnoma soatlab yo'lda qolib
              ketardi.

              Bugun bu tur standart ro'yxatda YO'Q, ya'ni qator amalda
              hech narsani o'zgartirmaydi. U ataylab yozilgan: ro'yxat
              kelajakda kengaytirilsa yoki .NET standartga yangi tur
              qo'shsa, xato JIMGINA paydo bo'lardi — oqim ishlamay
              qo'yardi va sababi bu fayldan izlanmasdi.
            */
            options.ExcludedMimeTypes = ["text/event-stream"];
        });

        // Standart daraja (Optimal) Brotli uchun 11-sifat — u JSON'ni bir necha
        // foizga yaxshiroq siqadi, lekin protsessor vaqtini o'nlab barobar yeydi.
        // Yuklama ostida bu tarmoqdan emas, protsessordan to'siq yasaydi.
        services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
    }

    // ────────────────────────────────────────────────────────────
    //  Chiqish keshi
    // ────────────────────────────────────────────────────────────

    private static void ChiqishKeshi(
        this IServiceCollection services,
        IConfiguration configuration,
        PerformanceOptions sozlama)
    {
        services.AddOutputCache(options =>
        {
            options.DefaultExpirationTimeSpan = TimeSpan.FromSeconds(sozlama.ReferenceCacheSeconds);

            // excludeDefaultPolicy: standart siyosat autentifikatsiyalangan
            // so'rovni umuman keshlamaydi. Bizda esa hamma so'rov tokenli —
            // ya'ni standart siyosat bilan kesh hech qachon ishlamas edi.
            // Uning o'rniga o'z siyosatimiz qo'yiladi (rollar bo'yicha ajratish +
            // faqat 200-javob + Set-Cookie bo'lmasligi sharti).
            options.AddPolicy(
                OutputCachePolicies.Malumotnoma,
                policy => policy
                    .AddPolicy<MalumotnomaCachePolicy>()
                    .Expire(TimeSpan.FromSeconds(sozlama.ReferenceCacheSeconds)),
                excludeDefaultPolicy: true);
        });

        // Redis bo'lsa — kesh barcha nusxalar uchun umumiy bo'ladi. Aks holda
        // (masalan testda) xotiradagi standart saqlagich qoladi.
        var redis = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.Replace(ServiceDescriptor.Singleton<IOutputCacheStore, TaqsimlanganChiqishKeshi>());
        }
    }

    // ────────────────────────────────────────────────────────────
    //  So'rov vaqti cheklovi
    // ────────────────────────────────────────────────────────────

    private static void SorovVaqtiCheklovi(this IServiceCollection services, PerformanceOptions sozlama)
        => services.AddRequestTimeouts(options =>
        {
            // Osilib qolgan so'rov ulanishni, EF ulanish hovuzidagi joyni va
            // xotirani ushlab turadi. Uzib qo'yish — resursni qaytarishning
            // yagona ishonchli yo'li.
            options.DefaultPolicy = Siyosat(sozlama.RequestTimeoutSeconds);
            options.AddPolicy(RequestTimeoutPolicies.Uzun, Siyosat(sozlama.LongRequestTimeoutSeconds));
        });

    private static RequestTimeoutPolicy Siyosat(int soniya) => new()
    {
        Timeout = TimeSpan.FromSeconds(soniya),
        TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
        WriteTimeoutResponse = VaqtTugadiAsync,
    };

    private static async Task VaqtTugadiAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status504GatewayTimeout,
            Title = "sorov.vaqt_tugadi",
            Detail = "So'rov belgilangan vaqt ichida bajarilmadi. Qayta urinib ko'ring.",
            Instance = context.Request.Path,
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;

        // RequestAborted allaqachon bekor qilingan — javobni yozish uchun
        // bekor qilinmagan token kerak.
        await context.Response
            .WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", CancellationToken.None)
            .ConfigureAwait(false);
    }

    // ────────────────────────────────────────────────────────────
    //  Sog'liq tekshiruvi
    // ────────────────────────────────────────────────────────────

    private static void SoglikTekshiruvlari(this IServiceCollection services)
        => services.AddHealthChecks()
            .AddCheck<BazaSogligi>(
                "baza",
                HealthStatus.Unhealthy,
                TayyorTeglari,
                TimeSpan.FromSeconds(3))
            .AddCheck<KeshSogligi>(
                "kesh",
                HealthStatus.Unhealthy,
                TayyorTeglari,
                TimeSpan.FromSeconds(3));

    /// <summary>
    /// <c>/health/live</c> va <c>/health/ready</c> endpoint'larini ulaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ikkalasi ATAYLAB alohida, chunki orkestrator ularni turlicha ishlatadi:
    /// </para>
    /// <list type="bullet">
    /// <item><c>live</c> yiqilsa — konteyner O'LDIRILADI va qaytadan ko'tariladi.</item>
    /// <item><c>ready</c> yiqilsa — konteyner tirik qoladi, faqat unga trafik yuborilmaydi.</item>
    /// </list>
    /// <para>
    /// Agar bazaga ulanish <c>live</c> ga qo'shilsa, baza bir daqiqaga uzilganda
    /// butun API cheksiz qayta ishga tushish tsikliga tushib qoladi — bu baza
    /// tiklanganda ham servisni ko'tarilmaydigan qiladi.
    /// </para>
    /// </remarks>
    public static IEndpointRouteBuilder MapBonviHealthChecks(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            // Hech qanday tekshiruv bajarilmaydi: javob kelayotgan bo'lsa,
            // demak jarayon tirik. Aynan shu savolga javob berish kerak.
            Predicate = static _ => false,
        })
        .AllowAnonymous()
        .ExcludeFromDescription();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = static registration => registration.Tags.Contains(TayyorTegi),
        })
        .AllowAnonymous()
        .ExcludeFromDescription();

        return app;
    }
}

/// <summary>
/// Ma'lumotnoma javoblarini keshlash siyosati.
/// </summary>
/// <remarks>
/// Standart siyosat o'chirilgani uchun (<c>excludeDefaultPolicy</c>) uning
/// himoyalari shu yerda qaytadan yoziladi: faqat GET/HEAD, faqat 200-javob,
/// <c>Set-Cookie</c> bo'lsa keshlanmaydi.
/// </remarks>
internal sealed class MalumotnomaCachePolicy : IOutputCachePolicy
{
    ValueTask IOutputCachePolicy.CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var request = context.HttpContext.Request;
        var mumkin = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);

        context.EnableOutputCaching = mumkin;
        context.AllowCacheLookup = mumkin;
        context.AllowCacheStorage = mumkin;

        // Bir xil so'rovni bir vaqtda 50 kishi yuborsa, bazaga faqat bittasi
        // boradi — qolganlari natijani kutadi ("cache stampede" himoyasi).
        context.AllowLocking = true;

        // Kesh kaliti RUXSATLARGA bog'lanadi: HR menejeri va oddiy xodim bir xil
        // ma'lumotnomani so'raganda ham serverda ro'yxat kesilgan bo'lishi mumkin.
        // Ruxsatlar kalitga kirmasa, biri ikkinchisining kengaytirilgan ro'yxatini
        // oladi. Rol kodi yetmaydi: rollar endi ekrandan tahrirlanadi va bir xil
        // kodli rolning ruxsatlari ertaga boshqacha bo'lishi mumkin (2026-09-26).
        context.CacheVaryByRules.VaryByValues["ruxsatlar"] = RuxsatlarKaliti(context.HttpContext.User);

        // Sahifalash va filtr parametrlari boshqa natija beradi — hammasi kalitga kiradi.
        context.CacheVaryByRules.QueryKeys = "*";

        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    ValueTask IOutputCachePolicy.ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;

        // Xato javobini keshlash eng yomon holat: bir marta yuz bergan 500
        // keyingi besh daqiqa davomida hammaga qaytariladi.
        if (response.StatusCode != StatusCodes.Status200OK
            || !StringValues.IsNullOrEmpty(response.Headers.SetCookie))
        {
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }

    private static string RuxsatlarKaliti(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return "anonim";
        }

        var ruxsatlar = user.FindAll(CurrentUser.PermissionClaimType).Select(claim => claim.Value).ToArray();

        if (ruxsatlar.Length == 0)
        {
            return "ruxsatsiz";
        }

        // Da'volar tartibi kafolatlanmagan — saralanmasa bir xil ruxsat to'plami
        // uchun har xil kesh kaliti chiqadi va kesh deyarli ishlamay qoladi.
        Array.Sort(ruxsatlar, StringComparer.Ordinal);

        return string.Join('|', ruxsatlar);
    }
}

/// <summary>
/// Chiqish keshini <see cref="IDistributedCache"/> (Redis) ustida saqlaydi.
/// </summary>
/// <remarks>
/// <para>
/// Nega o'zimizniki: rasmiy Redis-saqlagichi alohida NuGet paketini talab qiladi,
/// <see cref="IDistributedCache"/> esa allaqachon Infrastructure qatlamida
/// Redis'ga ulangan. Bitta interfeys — bitta ulanish hovuzi.
/// </para>
/// <para>
/// Nega umuman taqsimlangan kesh: API bir nechta nusxada ishlaganda xotiradagi
/// kesh har nusxada alohida bo'ladi — foydalanuvchi qaysi nusxaga tushishiga
/// qarab eski yoki yangi ma'lumot oladi.
/// </para>
/// </remarks>
internal sealed class TaqsimlanganChiqishKeshi : IOutputCacheStore
{
    private const string KalitPrefiksi = "oc:";
    private const string TegPrefiksi = "oc-teg:";

    private static readonly char[] Ajratgich = ['\n'];

    private readonly IDistributedCache _cache;

    public TaqsimlanganChiqishKeshi(IDistributedCache cache) => _cache = cache;

    public async ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken)
        => await _cache.GetAsync(KalitPrefiksi + key, cancellationToken).ConfigureAwait(false);

    public async ValueTask SetAsync(
        string key,
        byte[] value,
        string[]? tags,
        TimeSpan validFor,
        CancellationToken cancellationToken)
    {
        await _cache.SetAsync(
            KalitPrefiksi + key,
            value,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = validFor },
            cancellationToken).ConfigureAwait(false);

        if (tags is null)
        {
            return;
        }

        foreach (var teg in tags)
        {
            await TegniYangilaAsync(teg, key, validFor, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
    {
        var tegKaliti = TegPrefiksi + tag;
        var xom = await _cache.GetStringAsync(tegKaliti, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(xom))
        {
            return;
        }

        foreach (var kalit in xom.Split(Ajratgich, StringSplitOptions.RemoveEmptyEntries))
        {
            await _cache.RemoveAsync(KalitPrefiksi + kalit, cancellationToken).ConfigureAwait(false);
        }

        await _cache.RemoveAsync(tegKaliti, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Teg → kalitlar indeksini yangilaydi.</summary>
    /// <remarks>
    /// Indeks "o'qi-o'zgartir-yoz" tarzida yangilanadi, ya'ni bir vaqtda ikkita
    /// yozuv qo'shilsa biri indeksga tushmay qolishi mumkin. Eng yomon oqibat —
    /// o'sha yozuv teg bo'yicha o'chirilmay, o'z muddatigacha yashab qolishi.
    /// Ma'lumotnoma keshi uchun bu maqbul; buning uchun Redis'ning to'plam
    /// (SET) amallarini ochish butun boshli bog'liqlikni jalb qilishga arzimaydi.
    /// </remarks>
    private async Task TegniYangilaAsync(
        string teg,
        string kalit,
        TimeSpan muddat,
        CancellationToken cancellationToken)
    {
        var tegKaliti = TegPrefiksi + teg;

        var mavjud = await _cache.GetStringAsync(tegKaliti, cancellationToken).ConfigureAwait(false)
            ?? string.Empty;

        if (mavjud.Split(Ajratgich, StringSplitOptions.RemoveEmptyEntries).Contains(kalit, StringComparer.Ordinal))
        {
            return;
        }

        var yangi = mavjud.Length == 0 ? kalit : mavjud + '\n' + kalit;

        // Indeks kesh yozuvidan uzoqroq yashaydi: aks holda hali amal qilayotgan
        // yozuvni teg bo'yicha o'chirib bo'lmay qoladi.
        await _cache.SetStringAsync(
            tegKaliti,
            yangi,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = muddat + TimeSpan.FromMinutes(5) },
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Ma'lumotlar bazasi tayyorligini tekshiradi.</summary>
/// <remarks>
/// Istisno ataylab tutilmaydi: <c>HealthCheckService</c> uni o'zi ushlab,
/// tekshiruvni "Unhealthy" deb belgilaydi va xatoni logga yozadi.
/// </remarks>
internal sealed class BazaSogligi : IHealthCheck
{
    private readonly IDatabaseProbe _probe;

    public BazaSogligi(IDatabaseProbe probe) => _probe = probe;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var ulandi = await _probe.CanConnectAsync(cancellationToken).ConfigureAwait(false);

        return ulandi
            ? HealthCheckResult.Healthy("Ma'lumotlar bazasi javob bermoqda")
            : HealthCheckResult.Unhealthy("Ma'lumotlar bazasiga ulanib bo'lmadi");
    }
}

/// <summary>Kesh (Redis) tayyorligini tekshiradi.</summary>
/// <remarks>
/// Mavjud bo'lmagan kalitni o'qish — eng arzon "ping": ma'lumot o'zgartirmaydi,
/// lekin ulanish, autentifikatsiya va tarmoqni to'liq sinaydi.
/// Redis sozlanmagan bo'lsa xotiradagi kesh ishlaydi va tekshiruv doim yashil —
/// bu to'g'ri, chunki u holda tashqi bog'liqlik ham yo'q.
/// </remarks>
internal sealed class KeshSogligi : IHealthCheck
{
    private const string ProbaKaliti = "bonvi:soglik-probasi";

    private readonly IDistributedCache _cache;

    public KeshSogligi(IDistributedCache cache) => _cache = cache;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await _cache.GetAsync(ProbaKaliti, cancellationToken).ConfigureAwait(false);

        return HealthCheckResult.Healthy("Kesh javob bermoqda");
    }
}
