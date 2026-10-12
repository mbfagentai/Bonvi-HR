using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
    /// Tashqi API (<c>/api/public/v1</c>) — kalit PREFIKSI bo'yicha.
    /// </summary>
    /// <remarks>
    /// Nega IP bo'yicha emas: holdingning bir necha loyihasi bitta ofis
    /// IP'sidan chiqishi mumkin va o'shanda ular bir-birining chegarasini
    /// yeb qo'yardi. Kalit esa har bir loyihada o'zi.
    /// </remarks>
    public const string PublicApi = "public-api";

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

    /// <summary>
    /// Xodim ilovasiga kirish: bitta IP dan daqiqada 60 urinish — HR kirishidan ALOHIDA bo'linma.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Zavodning Wi-Fi'si bitta ommaviy IP: smena boshida o'nlab xodim
    /// bir daqiqada kiradi (birinchi kuni — yuzlab). HR ning daqiqasiga
    /// beshtalik chegarasi bilan bitta bo'linmada ular bir-birini ham, HR
    /// ni ham 429 bilan to'sardi, muvaffaqiyatli kirishlar ham hisobga
    /// tushgani uchun — aynan eng gavjum daqiqada.
    /// </para>
    /// <para>
    /// Parol terib ko'rishga qarshi asosiy to'siq bu son EMAS: bitta login
    /// bitta IP dan 5 xatodan keyin 15 daqiqaga bloklanadi
    /// (<c>EmployeeLoginThrottle</c>). Bu chegara faqat bitta manzildan
    /// ko'p loginlarni ketma-ket sinashni (spraying) sekinlashtiradi.
    /// </para>
    /// </remarks>
    public RateLimitWindow EmployeeLogin { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    /// <summary>
    /// Xodim sessiyasini yangilash/chiqish: bitta IP dan daqiqada 600 — umumiy 120 lik bo'linma O'RNIGA.
    /// </summary>
    /// <remarks>
    /// Yangilash anonim so'rov, ya'ni umumiy cheklovda u IP bo'yicha
    /// sanalardi: 950 xodimning ilovasi bitta zavod manzilidan har 15
    /// daqiqada yangilanadi va smena boshida 120 lik chegara to'lib qolardi.
    /// Bu keng «shift» faqat bitta manzildan tasodifiy tokenlar bilan
    /// bazani band qilishni chegaralaydi; bitta tokenning o'zi esa
    /// <see cref="RefreshToken"/> bo'yicha cheklanadi.
    /// </remarks>
    public RateLimitWindow EmployeeSession { get; set; } = new() { PermitLimit = 600, WindowSeconds = 60 };

    /// <summary>
    /// Bitta yangilash tokeni bilan daqiqada 10 so'rov — bo'linma kaliti tokenning xeshi.
    /// </summary>
    /// <remarks>
    /// Odatiy mijoz tokenni 15 daqiqada bir marta ishlatadi (javobi
    /// yo'qolsa — yana bir-ikki marta). Bir xil token bilan tinimsiz
    /// so'rayotgan mijoz — buzilgan sikl yoki hujum; unga IP emas, aynan
    /// shu token to'sig'i qo'yiladi va qo'shnilari ishlashda davom etadi.
    /// </remarks>
    public RateLimitWindow RefreshToken { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Umumiy API: daqiqada 120 so'rov, siljiydigan oyna.</summary>
    public RateLimitWindow Api { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60, SegmentsPerWindow = 6 };

    /// <summary>Eksport/hisobot: daqiqada 5 ta.</summary>
    public RateLimitWindow Export { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    /// <summary>Tashqi API, KALIT bilan: daqiqada 120 so'rov, siljiydigan oyna (SPEC §3).</summary>
    public RateLimitWindow PublicApi { get; set; } = new()
    {
        PermitLimit = 120,
        WindowSeconds = 60,
        SegmentsPerWindow = 6,
    };

    /// <summary>
    /// Tashqi API, KALITSIZ: daqiqada 20 urinish, IP bo'yicha.
    /// </summary>
    /// <remarks>
    /// Ancha kichik chegara ataylab (SPEC §3): kalitsiz so'rov faqat ikki
    /// holatda bo'ladi — iste'molchi sarlavhani sozlamagan (bir-ikki
    /// urinish yetadi) yoki kimdir kalit tanlab ko'ryapti. Ikkinchisiga
    /// 120 ta urinish/daqiqa berish o'rinsiz.
    /// </remarks>
    public RateLimitWindow PublicApiAnonymous { get; set; } = new()
    {
        PermitLimit = 20,
        WindowSeconds = 60,
    };

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
    /// Xodim kirishining parol tekshiruvi navbati — IP'dan qat'i nazar, HR nikidan ALOHIDA.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ 2026-10-12, xodim ilovasi internetga ochilgach (2026-09-30 xavfsizlik
    /// ko'rigining bandi). Har bir kirish urinishi PBKDF2 bilan protsessorni
    /// o'nlab millisekund to'liq band qiladi. IP bo'yicha oynalar ko'p manzildan
    /// kelgan oqimni to'xtatmaydi: 2 GB / 2 yadroli serverda yuzlab parallel urinish
    /// API'ni HR uchun ham sekinlatardi.
    /// </para>
    /// <para>
    /// Bir vaqtda ko'pi bilan <c>PermitLimit</c> ta kirish ishlanadi, <c>QueueLimit</c>
    /// tasi navbatda kutadi (eng eskisi birinchi), qolgani darhol 429 oladi —
    /// server esa ishlashda davom etadi. Oddiy kunda (smena boshida o'nlab xodim)
    /// navbat bir soniyadan kam.
    /// </para>
    /// <para>
    /// ⚠️ <b>Global zanjirda EMAS, endpoint filtrida</b>
    /// (<see cref="RateLimitingExtensions.RequirePasswordHashingQueue{TBuilder}"/>).
    /// Zanjirda turganda navbat band bo'lsa, middleware avval «urinib ko'rish», keyin
    /// «kutish» bilan chaqiradi va oldingi bo'g'in (IP oynasi) ruxsatni IKKI marta
    /// yeydi — oynali ruxsatlar qaytarilmaydi. Natijada zavod IP'sining chegarasi
    /// yarimga tushardi.
    /// </para>
    /// </remarks>
    public RateLimitConcurrency PasswordHashing { get; set; } = new() { PermitLimit = 2, QueueLimit = 20 };

    /// <summary>
    /// HR kirishining parol tekshiruvi navbati — xodimnikidan ALOHIDA.
    /// </summary>
    /// <remarks>
    /// Internetga ochiq xodim kirishiga tarqoq hujum xodim navbatini to'ldirsa ham, HR
    /// tizimga kira oladi. HR kirishi kamdan-kam — bitta ish joyi va kichik navbat yetarli.
    /// </remarks>
    public RateLimitConcurrency PasswordHashingHr { get; set; } = new() { PermitLimit = 1, QueueLimit = 5 };

    /// <summary>
    /// Qat'iy kirish cheklovi avtomatik qo'llanadigan yo'llar.
    /// </summary>
    /// <remarks>
    /// Bu "himoya to'ri": endpoint muallifi <c>RequireRateLimiting</c> yozishni
    /// unutsa ham, parol tanlashga qarshi cheklov baribir ishlaydi.
    /// </remarks>
    /// <remarks>
    /// ⚠️ Xodim ilovasining kirishi bu yerda EMAS — u
    /// <see cref="EmployeeLoginPaths"/> da, o'z bo'linmasida (sababi
    /// <see cref="EmployeeLogin"/> izohida). Bitta bo'linmada zavod manzili
    /// HR ni ham to'sib qo'yardi.
    /// </remarks>
    public IList<string> LoginPaths { get; } = ["/api/auth/login"];

    /// <summary>Xodim ilovasiga kirish — <see cref="EmployeeLogin"/> bo'linmasi (IP bo'yicha).</summary>
    public IList<string> EmployeeLoginPaths { get; } = ["/api/employee-auth/login"];

    /// <summary>
    /// Xodim sessiyasini yangilash va chiqish — umumiy bo'linma o'rniga <see cref="EmployeeSession"/>.
    /// </summary>
    public IList<string> EmployeeSessionPaths { get; } = ["/api/employee-auth/refresh", "/api/employee-auth/logout"];

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
    /// <para>
    /// Qurilma AGENTI ham xuddi shu sababdan (<c>/api/attendance/agent</c>):
    /// u qurilma tokeni bilan tasdiqlanadi va o'z siyosati bor
    /// (<see cref="RateLimitPolicies.Ingest"/>). Terminalga odam yozishda u har
    /// bir yozuv uchun rasmni alohida so'raydi — yuzlab xodimni yozish umumiy
    /// daqiqasiga 120 lik IP chegarasiga urilib, yarmida 429 bilan to'xtab
    /// qolardi.
    /// </para>
    /// <para>
    /// AI ulanish (<c>/mcp</c>) ham: uning ulanish (grant) bo'yicha o'z siyosati
    /// bor (daqiqasiga 120, <c>McpSetup.RateLimitPolicy</c>). Umumiy cheklov
    /// foydalanuvchi bo'yicha bo'linadi — rahbar AI bilan ishlaganda o'zining
    /// VEB-panel byudjetini yeb, ekranda 429 olardi.
    /// </para>
    /// </remarks>
    public IList<string> ExcludedPaths { get; } = ["/health", "/api/attendance/ingest", "/api/attendance/agent", "/mcp"];
}

/// <summary>Bir vaqtdagi so'rovlar soni va navbat — oynasiz cheklov sozlamalari.</summary>
public sealed class RateLimitConcurrency
{
    /// <summary>Bir vaqtda ishlanadigan so'rovlar soni.</summary>
    public int PermitLimit { get; set; } = 2;

    /// <summary>Navbatda kutishi mumkin bo'lgan so'rovlar soni; undan ortig'i darhol 429.</summary>
    public int QueueLimit { get; set; } = 20;
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
                // Parol tekshiruvi navbati bu yerda EMAS (sababi
                // `RateLimitingOptions.PasswordHashing` izohida).
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

            /*
              Tashqi API — IKKITA bo'linma, ikki xil chegara:

                • KALIT bilan kelgan so'rov prefiks bo'yicha ajratiladi va
                  120 so'rov/daqiqa oladi (SPEC §3). Prefiks o'sha so'rov
                  uchun `UseBonviApiKeyAuthentication` da allaqachon
                  aniqlangan — cheklov avtorizatsiyadan OLDIN ishlaydi,
                  sababi o'sha metodning izohida;

                • KALITSIZ so'rov IP bo'yicha ajratiladi va ancha kichik
                  chegara oladi. Aynan shu shox kalit tanlab ko'rishni
                  to'sadi: u avtorizatsiya 401 bergan so'rovlarni ham
                  sanaydi.
            */
            options.AddPolicy(RateLimitPolicies.PublicApi, context =>
            {
                var prefiks = context.User.FindFirstValue(ApiKeyClaims.Prefix);

                return string.IsNullOrEmpty(prefiks)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        "tashqi-anonim:" + Manba(context),
                        _ => QatiyOyna(sozlama.PublicApiAnonymous))
                    : RateLimitPartition.GetSlidingWindowLimiter(
                        "tashqi:" + prefiks,
                        _ => SiljiydiganOyna(sozlama.PublicApi));
            });

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

        // Yangilash tokeni bo'yicha cheklov — endpoint filtri o'qiydi
        // (`RequireRefreshTokenRateLimiting`). Global cheklovdan farqli
        // o'laroq har doim yoqiq: u IP ni emas, bitta tokenni to'sadi, ya'ni
        // testlar bitta «IP» dan kelishi unga xalaqit bermaydi.
        // Parol tekshiruvi navbatlari — global cheklov bilan birga yoqiladi/o'chadi
        // (Testing muhitida o'chiq; testlar o'z sozlamasi bilan yoqadi).
        services.AddSingleton(new PasswordHashingLanes(
            globalYoqilgan,
            Navbat(sozlama.PasswordHashing),
            Navbat(sozlama.PasswordHashingHr)));

        services.AddSingleton(new RefreshTokenRateLimiter(
            PartitionedRateLimiter.Create<string, string>(kalit =>
                RateLimitPartition.GetFixedWindowLimiter(kalit, _ => QatiyOyna(sozlama.RefreshToken)))));

        return services;
    }

    /// <summary>
    /// Yangilash tokeni bilan keladigan endpointga TOKEN XESHI bo'yicha cheklov qo'yadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega endpoint filtri, bo'linma emas.</b> Tezlik cheklovining
    /// bo'linma tanlovchisi sinxron va so'rov TANASINI o'qiy olmaydi — token esa
    /// aynan tanada. Filtr bog'langan argumentni (<see cref="IRefreshTokenBody"/>)
    /// oladi, ya'ni tana ikkinchi marta o'qilmaydi.
    /// </para>
    /// <para>
    /// Kalit — tokenning SHA-256 xeshi (qisqartirilgan): ochiq token xotiradagi
    /// lug'atda ham yashamasligi kerak.
    /// </para>
    /// </remarks>
    /// <typeparam name="TBuilder">Endpoint quruvchisi.</typeparam>
    /// <param name="builder">Endpoint.</param>
    public static TBuilder RequireRefreshTokenRateLimiting<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.AddEndpointFilter(TokenBoyichaCheklovAsync);

    private static async ValueTask<object?> TokenBoyichaCheklovAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var token = context.Arguments.OfType<IRefreshTokenBody>().FirstOrDefault()?.RefreshToken;

        var kalit = string.IsNullOrWhiteSpace(token)
            ? "bosh"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))[..32];

        var limiter = context.HttpContext.RequestServices.GetRequiredService<RefreshTokenRateLimiter>();

        using var lease = limiter.Limiter.AttemptAcquire(kalit);

        if (lease.IsAcquired)
        {
            return await next(context).ConfigureAwait(false);
        }

        var httpContext = context.HttpContext;
        var kutish = KutishVaqti(lease);

        httpContext.Response.Headers.RetryAfter = kutish.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture);

        TezlikLoglari.CheklovOshdi(
            httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Bonvi.Api.RateLimiting"),
            httpContext.Request.Method,
            httpContext.Request.Path.Value ?? "/",
            Manba(httpContext));

        return TypedResults.Problem(RadEtishJavobi(httpContext, kutish));
    }

    /// <summary>
    /// Kirish endpointini parol tekshiruvi navbatiga qo'yadi — bir vaqtda ishlanadigan
    /// tekshiruvlar soni chegaralanadi (<see cref="RateLimitingOptions.PasswordHashing"/>).
    /// </summary>
    /// <remarks>
    /// Navbat egasi ishlovchi tugaguncha ruxsatni ushlab turadi; navbat to'lsa — darhol 429
    /// (global cheklov bilan bir xil javob). Tana allaqachon bog'langan (16 KB chegarasi bilan),
    /// ya'ni kutishda faqat kichik so'rovlar turadi.
    /// </remarks>
    /// <typeparam name="TBuilder">Endpoint quruvchisi.</typeparam>
    /// <param name="builder">Endpoint.</param>
    /// <param name="queue">Qaysi navbat — HR yoki xodim.</param>
    public static TBuilder RequirePasswordHashingQueue<TBuilder>(this TBuilder builder, PasswordHashingLane queue)
        where TBuilder : IEndpointConventionBuilder
        => builder.AddEndpointFilter((context, next) => NavbatBilanAsync(context, next, queue));

    private static async ValueTask<object?> NavbatBilanAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        PasswordHashingLane queue)
    {
        var httpContext = context.HttpContext;
        var navbatlar = httpContext.RequestServices.GetRequiredService<PasswordHashingLanes>();

        if (!navbatlar.Enabled)
        {
            return await next(context).ConfigureAwait(false);
        }

        using var lease = await navbatlar.For(queue)
            .AcquireAsync(1, httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (lease.IsAcquired)
        {
            return await next(context).ConfigureAwait(false);
        }

        // Navbat to'lgan — bir necha soniyadan keyin bo'shaydi.
        var kutish = TimeSpan.FromSeconds(5);
        httpContext.Response.Headers.RetryAfter = kutish.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture);

        TezlikLoglari.CheklovOshdi(
            httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Bonvi.Api.RateLimiting"),
            httpContext.Request.Method,
            httpContext.Request.Path.Value ?? "/",
            Manba(httpContext));

        return TypedResults.Problem(RadEtishJavobi(httpContext, kutish));
    }

    private static ConcurrencyLimiter Navbat(RateLimitConcurrency sozlama) => new(new ConcurrencyLimiterOptions
    {
        PermitLimit = sozlama.PermitLimit,
        QueueLimit = sozlama.QueueLimit,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    });

    // ────────────────────────────────────────────────────────────
    //  Bo'linmalar (partition)
    // ────────────────────────────────────────────────────────────

    /// <summary>Kirish yo'llari: HR kirishi va xodim kirishi — har biri o'z bo'linmasida.</summary>
    private static PartitionedRateLimiter<HttpContext> KirishQoriqchisi(RateLimitingOptions sozlama)
        => PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var yol = context.Request.Path;

            if (YolMoslashadimi(yol, sozlama.EmployeeLoginPaths))
            {
                return RateLimitPartition.GetFixedWindowLimiter(
                    "employee-login:" + Manba(context),
                    _ => QatiyOyna(sozlama.EmployeeLogin));
            }

            return YolMoslashadimi(yol, sozlama.LoginPaths)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    "login:" + Manba(context),
                    _ => QatiyOyna(sozlama.Login))
                : RateLimitPartition.GetNoLimiter<string>("chetlab-otildi");
        });

    /// <summary>
    /// Qolgan hamma so'rov: umumiy bo'linma — xodim kirishi va sessiya yo'llari bundan TASHQARI.
    /// </summary>
    /// <remarks>
    /// Xodim kirishi o'z bo'linmasida allaqachon sanalgan (yuqorida), sessiya
    /// yo'llari (yangilash, chiqish) esa anonim va umumiy bo'linmada IP bo'yicha
    /// tushib, zavod manzilining 120 lik chegarasini to'ldirardi — ular o'z
    /// keng «shift» ida sanaladi.
    /// </remarks>
    private static PartitionedRateLimiter<HttpContext> UmumiyQoriqchi(RateLimitingOptions sozlama)
        => PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var yol = context.Request.Path;

            if (YolMoslashadimi(yol, sozlama.ExcludedPaths) || YolMoslashadimi(yol, sozlama.EmployeeLoginPaths))
            {
                return RateLimitPartition.GetNoLimiter<string>("chetlab-otildi");
            }

            return YolMoslashadimi(yol, sozlama.EmployeeSessionPaths)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    "employee-session:" + Manba(context),
                    _ => QatiyOyna(sozlama.EmployeeSession))
                : RateLimitPartition.GetSlidingWindowLimiter(
                    "api:" + Kim(context),
                    _ => SiljiydiganOyna(sozlama.Api));
        });

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
    /// <para>
    /// Foydalanuvchi bo'yicha ajratish adolatliroq: bitta ofisdagi 50 xodim
    /// bitta NAT IP orqali chiqadi va IP bo'yicha cheklansa bir-birini to'sadi.
    /// </para>
    /// <para>
    /// Tashqi API kaliti ALOHIDA bo'linma oladi va u BIRINCHI tekshiriladi.
    /// Kalitda <c>ClaimTypes.NameIdentifier</c> ATAYLAB yo'q (sababi
    /// <see cref="ApiKeyClaims"/> izohida), ya'ni usiz kalit bilan kelgan
    /// barcha loyihalar bitta ofis IP'siga tushib, umumiy cheklovda
    /// bir-birini to'sardi.
    /// </para>
    /// </remarks>
    private static string Kim(HttpContext context)
    {
        if (context.User.FindFirstValue(ApiKeyClaims.Prefix) is { Length: > 0 } prefiks)
        {
            return "k:" + prefiks;
        }

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

        await httpContext.Response
            .WriteAsJsonAsync(
                RadEtishJavobi(httpContext, kutish),
                options: null,
                contentType: "application/problem+json",
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>429 javobining tanasi — global cheklov ham, token filtri ham BIR XIL shaklda.</summary>
    private static ProblemDetails RadEtishJavobi(HttpContext httpContext, TimeSpan kutish)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = XatoKodi,
            Detail = "So'rovlar soni ruxsat etilgan chegaradan oshdi. Biroz kutib, qayta urinib ko'ring.",
            Instance = httpContext.Request.Path,
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        problem.Extensions["retryAfterSeconds"] = (int)kutish.TotalSeconds;

        return problem;
    }

    private static TimeSpan KutishVaqti(RateLimitLease lease)
        => lease.TryGetMetadata(MetadataName.RetryAfter, out var qiymat)
            ? qiymat
            : TimeSpan.FromSeconds(60);
}

/// <summary>Tanasida yangilash tokeni bor so'rov — token bo'yicha cheklov shu orqali o'qiydi.</summary>
public interface IRefreshTokenBody
{
    /// <summary>Mijoz yuborgan yangilash tokeni.</summary>
    string? RefreshToken { get; }
}

/// <summary>Parol tekshiruvi navbatlaridan qaysi biri.</summary>
public enum PasswordHashingLane
{
    /// <summary>Xodim ilovasiga kirish.</summary>
    Employee = 1,

    /// <summary>HR tizimiga kirish.</summary>
    Hr = 2,
}

/// <summary>Parol tekshiruvi navbatlari — bitta nusxa (singleton).</summary>
/// <param name="Enabled">O'chiq bo'lsa filtr hech narsani kutdirmaydi (Testing).</param>
/// <param name="Employee">Xodim kirishi navbati.</param>
/// <param name="Hr">HR kirishi navbati.</param>
public sealed record PasswordHashingLanes(bool Enabled, ConcurrencyLimiter Employee, ConcurrencyLimiter Hr)
{
    /// <summary>Navbatni tanlaydi.</summary>
    /// <param name="queue">Qaysi navbat.</param>
    public ConcurrencyLimiter For(PasswordHashingLane queue) => queue == PasswordHashingLane.Hr ? Hr : Employee;
}

/// <summary>Token xeshi bo'yicha bo'lingan cheklovchi — bitta nusxa (singleton).</summary>
/// <param name="Limiter">Kalit — tokenning qisqartirilgan xeshi.</param>
public sealed record RefreshTokenRateLimiter(PartitionedRateLimiter<string> Limiter);

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
