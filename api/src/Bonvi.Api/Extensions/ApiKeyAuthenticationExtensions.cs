using System.Security.Claims;
using System.Text.Encodings.Web;
using Bonvi.Application.PublicApi.Common;
using Bonvi.Application.PublicApi.Keys;
using Bonvi.Domain.PublicApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Bonvi.Api.Extensions;

/// <summary>
/// Tashqi API kaliti orqali tanilgan so'rov qanday claim'lar bilan yuradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b><see cref="ClaimTypes.NameIdentifier"/> ATAYLAB QO'YILMAYDI.</b>
/// U butun tizimda «bu FOYDALANUVCHI» degan ma'noni bildiradi:
/// <c>CurrentUser.UserId</c> uni o'qiydi va audit interceptori
/// <c>created_by</c> / <c>updated_by</c> ga yozadi. Kalit esa foydalanuvchi
/// emas — u odamga tegishli emas va u nomidan hech narsa yozilmaydi.
/// Qo'yilsa, bir kun kelib kalitning identifikatori xodim kartasining
/// «kim o'zgartirdi» ustunida paydo bo'lardi.
/// </para>
/// <para>
/// Shu sababli claim'lar ATAYLAB o'z nom fazosida (<c>tashqi.</c>) va
/// ulardan biri — <see cref="Prefix"/> — avtorizatsiya siyosatida
/// MAJBURIY: shundagina «kalit bilan kelgan» so'rovni «JWT bilan kelgan»
/// dan siyosat darajasida ajratib bo'ladi.
/// </para>
/// </remarks>
public static class ApiKeyClaims
{
    /// <summary>Kalitning ochiq prefiksi — tezlik cheklovining bo'linmasi.</summary>
    public const string Prefix = "tashqi.prefiks";

    /// <summary>Kalit yozuvining identifikatori.</summary>
    public const string KeyId = "tashqi.kalit";

    /// <summary>Kalit nomi (loyiha nomi) — <c>/ping</c> javobida qaytadi.</summary>
    public const string KeyName = "tashqi.nom";

    /// <summary>Kalit doirasi (<c>Basic</c> yoki <c>FullRead</c>) — <c>/full</c> shu bo'yicha ochiladi.</summary>
    public const string Scope = "tashqi.doira";
}

/// <summary>
/// <c>X-API-Key</c> sxemasining sozlamalari.
/// </summary>
/// <remarks>
/// Bo'sh: sxemada sozlanadigan hech narsa yo'q va bo'lishi ham kerak emas.
/// Sarlavha nomi, kalit formati va chegaralar — <b>shartnoma</b>, sozlama
/// emas: ularni muhitga qarab o'zgartirish imkoni iste'molchilarni bir
/// kun jimgina uzib qo'yardi.
/// </remarks>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions;

/// <summary>
/// <c>X-API-Key</c> sarlavhasi bo'yicha autentifikatsiya — <b>faqat ommaviy
/// yuza uchun</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida SXEMA, endpoint filtri emas.</b> Sxema bilan
/// «kim kirishi mumkin» savoli avtorizatsiya siyosati darajasida hal
/// bo'ladi: ommaviy siyosat FAQAT shu sxemani sanaydi, ichki
/// siyosatlar esa faqat <c>Bearer</c> ni (standart sxema). Natijada
/// ikkala yo'nalish ham KONFIGURATSIYA bilan yopiladi, tekshiruv
/// yozishni unutish bilan emas:
/// </para>
/// <list type="bullet">
///   <item>kalit bilan <c>/api/employees</c> ga kirilmaydi — u yerda
///         <c>Bearer</c> tekshiriladi va sarlavha unga mos kelmaydi;</item>
///   <item>foydalanuvchi tokeni bilan <c>/api/public/v1/...</c> ga
///         kirilmaydi — u yerda faqat bu sxema o'qiladi va
///         <c>Authorization</c> sarlavhasiga umuman qaralmaydi.</item>
/// </list>
/// <para>
/// Ikkalasi ham test bilan qo'riqlanadi (SPEC §9).
/// </para>
/// <para>
/// ⚠️ <b>Kalit hech qayerda log qilinmaydi.</b> Bu sinfda <c>ILogger</c>
/// yo'q, <see cref="AuthenticateResult.Fail(string)"/> ga uzatiladigan
/// matnda esa faqat xato KODI bor — sarlavha qiymatining bironta ham
/// bo'lagi u yerga tushmaydi. Muvaffaqiyatsiz autentifikatsiya sababi
/// Serilog'ning so'rov logida ham ko'rinmaydi: u sarlavhalarni umuman
/// yozmaydi.
/// </para>
/// </remarks>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    /// <summary>Sxema nomi — avtorizatsiya siyosatlarida shu bilan ko'rsatiladi.</summary>
    public const string SchemeName = "ApiKey";

    /// <summary>
    /// Kalit KELADIGAN yagona joy.
    /// </summary>
    /// <remarks>
    /// So'rov qatori (<c>?apiKey=</c>) va cookie ATAYLAB qabul qilinmaydi:
    /// so'rov qatoridagi kalit teskari proksining kirish logiga, brauzer
    /// tarixiga va <c>Referer</c> sarlavhasiga tushadi — ya'ni sir
    /// uchtala joyda ochiq matn bo'lib yotib qoladi.
    /// </remarks>
    public const string HeaderName = "X-API-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values) || values.Count == 0)
        {
            // `NoResult` — «bu sxema bu so'rovga tegishli emas». Shu tufayli
            // sarlavhasiz so'rov boshqa sxemalarga to'sqinlik qilmaydi.
            return AuthenticateResult.NoResult();
        }

        var authenticator = Context.RequestServices.GetRequiredService<ApiKeyAuthenticator>();

        var identity = await authenticator
            .AuthenticateAsync(
                values[0],
                Context.Connection.RemoteIpAddress?.ToString(),
                Context.RequestAborted)
            .ConfigureAwait(false);

        if (identity is null)
        {
            // Yo'q, buzuq, noma'lum va bekor qilingan kalit — BITTA javob
            // (sababi `PublicApiErrors` izohida).
            return AuthenticateResult.Fail(PublicApiErrors.InvalidKeyCode);
        }

        var claims = new ClaimsIdentity(
            [
                new Claim(ApiKeyClaims.KeyId, identity.Id.ToString()),
                new Claim(ApiKeyClaims.KeyName, identity.Name),
                new Claim(ApiKeyClaims.Prefix, identity.Prefix),
                new Claim(ApiKeyClaims.Scope, identity.Scope.ToString()),
            ],
            SchemeName,

            // `nameType` — `ClaimTypes.Name` EMAS, balki kalit nomi claim'i:
            // `ClaimsPrincipal.Identity.Name` shu qiymatni qaytaradi, lekin
            // tizimdagi «foydalanuvchi ismi» claim'i band qilinmaydi.
            nameType: ApiKeyClaims.KeyName,

            // Rol claim'i ATAYLAB mavjud bo'lmagan turga qo'yilgan: kalitda
            // rol yo'q va `RequireRole(...)` hech qachon unga mos
            // kelmasligi kerak.
            roleType: "tashqi.rol_yoq");

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(claims), SchemeName));
    }

    /// <summary>
    /// 401 javobini loyihaning umumiy shaklida yozadi.
    /// </summary>
    /// <remarks>
    /// Shakl JWT sxemasidagi bilan bir xil (<c>status</c> / <c>title</c> /
    /// <c>detail</c>, <c>application/problem+json</c>): iste'molchi
    /// xatolarni bitta o'quvchi bilan qayta ishlaydi.
    /// <para>
    /// <c>WWW-Authenticate</c> sarlavhasi ATAYLAB yo'q: u brauzerda
    /// parol so'rash oynasini chiqarishi mumkin, bu esa mashina-mashina
    /// integratsiyasida ma'nosiz.
    /// </para>
    /// </remarks>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";

        await Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status401Unauthorized,
            title = PublicApiErrors.InvalidKeyCode,
            detail = PublicApiErrors.InvalidKeyMessage,
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Kalit to'g'ri, lekin doirasi yetmaydi (<c>Basic</c> kalit bilan <c>/full</c>).
    /// </summary>
    /// <remarks>
    /// 401 emas, 403: kalit haqiqiy va iste'molchi buni bilishi kerak — «kalitni
    /// almashtiring» emas, «kengroq doirali kalit so'rang».
    /// </remarks>
    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/problem+json";

        await Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status403Forbidden,
            title = "tashqi.doira_yetmaydi",
            detail = "Bu kalit faqat xodimlar va ma'lumotnomalarni o'qiydi. Barcha bo'limlar uchun «to'liq o'qish» doirasidagi kalit kerak.",
        }).ConfigureAwait(false);
    }
}

/// <summary>
/// Kalit sxemasini va ommaviy avtorizatsiya siyosatini ulaydi.
/// </summary>
public static class ApiKeyAuthenticationExtensions
{
    /// <summary>
    /// Ommaviy yuzaning YAGONA avtorizatsiya siyosati.
    /// </summary>
    /// <remarks>
    /// Nomi ruxsat kodlariga o'xshamaydi (nuqta yo'q, chiziqcha bor) —
    /// <c>AuthenticationExtensions.BotServicePolicy</c> dagi bilan bir xil
    /// qoida: siyosatlar bitta nom fazosida yashaydi va ruxsatga
    /// asoslanmagan siyosatlar bir qarashda ajralib turishi kerak.
    /// </remarks>
    public const string PublicApiPolicy = "tashqi-api";

    /// <summary>Kalit sxemasini autentifikatsiya quvuriga qo'shadi.</summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Bu chaqiruv STANDART sxemani O'ZGARTIRMAYDI: u
    /// <c>AddBonviAuthentication</c> da <c>Bearer</c> bo'lib qoladi.
    /// Argumentsiz <c>AddAuthentication()</c> ataylab: u faqat yadro
    /// servislarini ta'minlaydi va standart sxema sozlamasiga tegmaydi.
    /// Natijada sxemani ko'rsatmagan har bir endpoint avvalgidek JWT
    /// talab qiladi — mavjud himoya oqimiga tegilmaydi.
    /// </para>
    /// <para>
    /// Alohida chaqiruv (mavjud metodning imzosini o'zgartirish emas):
    /// <c>AddBonviAuthentication</c> ni ishlatadigan joylar — shu jumladan
    /// testlar — o'zgarishsiz qoladi.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddBonviApiKeyScheme(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddAuthentication()
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                configureOptions: null);

        return services;
    }

    /// <summary>Ommaviy yuzaning avtorizatsiya siyosatini qo'shadi.</summary>
    /// <remarks>
    /// <para>
    /// Siyosatda uchta shart bor va har biri kerak:
    /// </para>
    /// <list type="number">
    ///   <item><c>AddAuthenticationSchemes(ApiKey)</c> — faqat kalit
    ///         sxemasi o'qiladi. Foydalanuvchi JWT'si bu yerda UMUMAN
    ///         ko'rilmaydi, ya'ni ichki token bilan ommaviy endpoint'ga
    ///         kirib bo'lmaydi;</item>
    ///   <item><c>RequireAuthenticatedUser</c> — kalitsiz so'rov 401;</item>
    ///   <item><c>RequireClaim(prefiks)</c> — zaxira to'r. Agar kelajakda
    ///         kimdir sxemalar ro'yxatini kengaytirib qo'ysa, boshqa
    ///         sxemadan kelgan principal'da bu claim BO'LMAYDI va
    ///         siyosat baribir rad etadi.</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddBonviPublicApiAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthorizationBuilder()
            .AddPolicy(PublicApiPolicy, policy => policy
                .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(ApiKeyClaims.Prefix))

            // «To'liq o'qish» — yuqoridagi shartlar USTIGA doira claim'i.
            .AddPolicy(Endpoints.PublicFullReadEndpoints.Policy, policy => policy
                .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(ApiKeyClaims.Prefix)
                .RequireClaim(ApiKeyClaims.Scope, nameof(ApiKeyScope.FullRead)));

        return services;
    }

    /// <summary>
    /// Ommaviy yo'lda kalitni <b>tezlik cheklovidan OLDIN</b> aniqlaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega bu kerak.</b> Tezlik cheklovi kalit PREFIKSI bo'yicha
    /// bo'linadi (SPEC §3: bir nechta loyiha bitta ofis IP'sidan chiqishi
    /// mumkin). Prefiks esa claim'da, ya'ni cheklov ishlaganda
    /// autentifikatsiya allaqachon bajarilgan bo'lishi SHART. Standart
    /// <c>UseAuthentication</c> faqat STANDART sxemani (JWT) bajaradi,
    /// nomli sxemani esa avtorizatsiya vaqtida — cheklovdan keyin.
    /// </para>
    /// <para>
    /// Natija <c>AuthenticateAsync</c> ichida so'rov davomida keshlanadi,
    /// shu sababli avtorizatsiya bosqichida baza SO'ROVI QAYTA KETMAYDI —
    /// bitta so'rovga bitta kalit tekshiruvi.
    /// </para>
    /// <para>
    /// Faqat ommaviy prefiksdagi yo'llarga tegadi: qolgan hamma so'rov
    /// bu middleware'dan o'zgarishsiz o'tadi.
    /// </para>
    /// </remarks>
    public static IApplicationBuilder UseBonviApiKeyAuthentication(
        this IApplicationBuilder app,
        string pathPrefix)
    {
        ArgumentNullException.ThrowIfNull(app);

        var prefix = new PathString(pathPrefix);

        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var result = await context
                    .AuthenticateAsync(ApiKeyAuthenticationHandler.SchemeName)
                    .ConfigureAwait(false);

                if (result.Succeeded)
                {
                    context.User = result.Principal;
                }
            }

            await next(context).ConfigureAwait(false);
        });
    }
}
