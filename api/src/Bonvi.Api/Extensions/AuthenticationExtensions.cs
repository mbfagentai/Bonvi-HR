using System.Security.Claims;
using System.Text;
using Bonvi.Api.Common;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Bonvi.Api.Extensions;

/// <summary>
/// JWT autentifikatsiyasi va ruxsatga asoslangan avtorizatsiya.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// JWT tekshiruvini sozlaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ClockSkew = TimeSpan.Zero</c> — standart holatda .NET 5 daqiqalik
    /// "chalkashlik zaxirasi" beradi, ya'ni muddati tugagan token yana
    /// 5 daqiqa amal qiladi. Bizda access token umuman 15 daqiqa —
    /// 5 daqiqa qo'shimcha muddat uni uchdan bir barobar uzaytiradi.
    /// </para>
    /// <para>
    /// Xato javoblari ham kod bilan qaytariladi (<c>auth.token_yaroqsiz</c>),
    /// chunki frontend xabarni o'zi tarjima qiladi.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddBonviAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
        {
            throw new InvalidOperationException(
                "«Jwt:SigningKey» sozlamasi yo'q yoki 32 belgidan qisqa. " +
                ".env faylini tekshiring — bu xavfsizlik talabi.");
        }

        var hrAudience = configuration["Jwt:Audience"];
        var employeeAudience = configuration["EmployeeJwt:Audience"] ?? DefaultEmployeeAudience;

        /*
          ⭐ IKKI AUDITORIYA TENG BO'LIB QOLMASLIGI — ishga tushishda.

          Butun ajratish shu ikki satrning FARQIGA tayanadi: xodim tokeni
          HR endpointlariga kira olmasligi ham, HR tokeni `/api/me/*` ga
          kira olmasligi ham shundan kelib chiqadi. Ular tasodifan (yoki
          noto'g'ri `.env` bilan) tenglashib qolsa, tizim ochilib ketardi
          va buni HECH QANDAY test ushlamasdi — chunki testlarda sozlama
          to'g'ri. Shu sababli tekshiruv shu yerda va u dasturni ishga
          tushirmaydi.
        */
        if (string.Equals(hrAudience, employeeAudience, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "«Jwt:Audience» va «EmployeeJwt:Audience» bir xil bo'lishi mumkin emas — " +
                "xodim tokeni HR endpointlariga kirib qolardi.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],

                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,

                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ApplyCurrentAccessAsync,

                    OnChallenge = async context =>
                    {
                        // Standart javobni bekor qilamiz va o'z formatimizni beramiz
                        context.HandleResponse();

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";

                        await context.Response.WriteAsJsonAsync(new
                        {
                            status = 401,
                            title = "auth.token_yaroqsiz",
                            detail = "Sessiya muddati tugadi yoki token yaroqsiz.",
                        }).ConfigureAwait(false);
                    },

                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/problem+json";

                        await context.Response.WriteAsJsonAsync(new
                        {
                            status = 403,
                            title = "auth.ruxsat_yoq",
                            detail = "Bu amalni bajarish uchun ruxsatingiz yo'q.",
                        }).ConfigureAwait(false);
                    },
                };
            });

        /*
          ⭐ XODIM ILOVASI — IKKINCHI SXEMA, alohida auditoriya bilan.

          Nega ikkinchi sxema, bitta sxemaga ikkinchi auditoriyani
          qo'shish emas: `ValidAudiences` ga ikkalasini yozish har ikkala
          tokenni HAR BIR endpointda qabul qilardi va ajratish faqat
          ruxsat da'volariga qolardi. Ruxsat esa «yo'qligi» bilan
          qo'riqlaydi — ya'ni ruxsat kodisiz e'lon qilingan bitta
          endpoint (masalan `RequireAuthorization()` bilan) xodimga
          ochilib qolardi. Ikki sxemada bunday holat tug'ilmaydi: token
          umuman qabul qilinmaydi.

          Imzolash kaliti bir xil — sababi `EmployeeJwtOptions` izohida.
        */
        services
            .AddAuthentication()
            .AddJwtBearer(EmployeeScheme, options =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],

                    ValidateAudience = true,
                    ValidAudience = employeeAudience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,

                    // Rol da'vosi bu tokenda YO'Q va bo'lmaydi ham; nom
                    // turlari baribir aniq ko'rsatiladi, chunki sukut
                    // qiymatlar `MapInboundClaims = false` bilan boshqa
                    // natija berardi.
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                };

                // Javob formati HR sxemasi bilan AYNAN bir xil: ilova ham,
                // HR interfeysi ham xato KODINI o'qiydi va o'zi tarjima
                // qiladi.
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";

                        await context.Response.WriteAsJsonAsync(new
                        {
                            status = 401,
                            title = "auth.token_yaroqsiz",
                            detail = "Sessiya muddati tugadi yoki token yaroqsiz.",
                        }).ConfigureAwait(false);
                    },
                };
            });

        return services;
    }

    /// <summary>Xodim ilovasi tokenlari uchun autentifikatsiya sxemasining nomi.</summary>
    /// <remarks>
    /// Marshrutlarda <c>.RequireAuthorization(EmployeePolicy)</c> orqali
    /// ishlatiladi — siyosat aynan shu sxemani talab qiladi.
    /// </remarks>
    public const string EmployeeScheme = "EmployeeBearer";

    /// <summary>Faqat xodim tokeni o'tadigan siyosat.</summary>
    /// <remarks>
    /// Nomida nuqta YO'Q — ruxsat kodlari bilan bir nom fazosida yashaydi
    /// va ulardan bir qarashda ajralib turishi kerak
    /// (<see cref="BotServicePolicy"/> bilan bir xil qoida).
    /// </remarks>
    public const string EmployeePolicy = "employee-app";

    /// <summary>Sozlama berilmaganda ishlatiladigan xodim auditoriyasi.</summary>
    /// <remarks>
    /// <c>EmployeeJwtOptions.Audience</c> ning sukut qiymati bilan bir xil
    /// bo'lishi SHART: bu yerda tekshiriladigan qiymat bilan tokenga
    /// yoziladigan qiymat ajralib qolsa, birorta xodim tokeni o'tmasdi.
    /// </remarks>
    private const string DefaultEmployeeAudience = "bonvi-employee";

    /// <summary>
    /// Imzosi to'g'ri tokendagi rol va ruxsatlarni bazadagi JORIY holat bilan almashtiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega.</b> Ruxsatlar access token ichida yuradi va token 15 daqiqa
    /// yashaydi. Egasining qarori: rolga kiritilgan o'zgarish (berilgan
    /// ham, olingan ham) va hisobni faolsizlantirish deploy va qayta
    /// kirishsiz, KEYINGI so'rovdayoq kuchga kirsin. Shu sababli token endi
    /// «kim» degan savolga javob beradi, «nimaga ruxsati bor» ga esa baza.
    /// </para>
    /// <para>
    /// Barcha <c>RequireAuthorization(...)</c> tekshiruvlari va
    /// <c>ICurrentUser.HasPermission</c> o'zgarmaydi — ular hamon
    /// <c>perm</c> claim'larini o'qiydi, faqat claim'larning manbai endi
    /// yangi. Narxi — ≤ 30 soniyalik xotira keshi (<see cref="IUserAccessReader"/>).
    /// </para>
    /// <para>
    /// Hisob o'chirilgan yoki faolsizlantirilgan bo'lsa — autentifikatsiya
    /// RAD ETILADI va javob odatdagi 401 <c>auth.token_yaroqsiz</c>
    /// (<c>OnChallenge</c>). Klient refresh qilishga urinadi va u yerda
    /// ham rad etiladi (<c>auth.hisob_faol_emas</c>) — sessiya yopiladi.
    /// </para>
    /// </remarks>
    private static async Task ApplyCurrentAccessAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var raw = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (principal is null || !Guid.TryParse(raw, out var userId))
        {
            context.Fail("Tokenda foydalanuvchi identifikatori yo'q.");
            return;
        }

        var reader = context.HttpContext.RequestServices.GetRequiredService<IUserAccessReader>();

        var access = await reader
            .ReadAsync(userId, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (access is null)
        {
            context.Fail("Hisob topilmadi yoki faol emas.");
            return;
        }

        var original = principal.Identity as ClaimsIdentity;
        var roleClaimType = original?.RoleClaimType ?? ClaimTypes.Role;

        // Tokendagi qolgan claim'lar (sub, login, jti, emp) o'z joyida —
        // faqat rol va ruxsatlar almashtiriladi.
        var claims = principal.Claims
            .Where(claim =>
                claim.Type != CurrentUser.PermissionClaimType &&
                claim.Type != roleClaimType)
            .ToList();

        claims.AddRange(access.RoleCodes.Select(code => new Claim(roleClaimType, code)));
        claims.AddRange(access.Permissions.Select(permission => new Claim(CurrentUser.PermissionClaimType, permission)));

        var identity = new ClaimsIdentity(
            claims,
            original?.AuthenticationType ?? JwtBearerDefaults.AuthenticationScheme,
            original?.NameClaimType ?? ClaimTypes.Name,
            roleClaimType);

        context.Principal = new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Har bir ruxsat uchun avtorizatsiya siyosatini yaratadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Siyosat nomi ruxsat kodining o'zi: <c>.RequireAuthorization("employees.view")</c>.
    /// Ruxsatlar 15 ta va konstanta — shu sababli ularni ishga tushishda
    /// bir marta ro'yxatga olish kifoya, dinamik siyosat provayderi kerak emas.
    /// </para>
    /// <para>
    /// Tekshiruv <c>perm</c> claim'lari bo'yicha xotirada bajariladi.
    /// Claim'lar esa tokendan emas, bazadagi joriy roldan olinadi
    /// (<see cref="ApplyCurrentAccessAsync"/>, ≤ 30 s kesh bilan).
    /// </para>
    /// </remarks>
    /// <summary>
    /// Faqat Telegram bot xizmat hisobiga ochiq endpoint'lar siyosati.
    /// </summary>
    /// <remarks>
    /// Nomi ruxsat kodiga O'XSHAMAYDI (nuqta yo'q, chiziqcha bor) —
    /// ataylab: siyosatlar bitta nom fazosida yashaydi va bu yerdagi
    /// yagona rol siyosatini ruxsat siyosatlaridan bir qarashda
    /// ajratib turish kerak.
    /// </remarks>
    public const string BotServicePolicy = "bot-service";

    /// <summary>
    /// Rollar RO'YXATI siyosati: <c>users.manage</c> YOKI <c>roles.manage</c>.
    /// </summary>
    /// <remarks>
    /// Ro'yxat ikki ekranga kerak: «Rollar» menyusi (<c>roles.manage</c>) va
    /// foydalanuvchi formasidagi rol tanlash (<c>users.manage</c>). Ikkala
    /// ruxsat odatda boshqa-boshqa odamda — bittasini talab qilish
    /// ikkinchi ekranni buzardi. Rollarni O'ZGARTIRISH esa faqat
    /// <c>roles.manage</c> bilan. Nomi <see cref="BotServicePolicy"/> kabi
    /// chiziqchali — ruxsat kodi bilan adashmasligi uchun.
    /// </remarks>
    public const string RolesReadPolicy = "roles-read";

    public static IServiceCollection AddBonviAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Sukut bo'yicha: barcha endpoint'lar autentifikatsiya talab qiladi.
            // Ochiq endpoint'lar ataylab `.AllowAnonymous()` bilan belgilanadi —
            // himoyani unutib qoldirishdan ko'ra, ochiqlikni ataylab e'lon qilish xavfsizroq.
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        foreach (var permission in Permissions.All)
        {
            services.AddAuthorizationBuilder()
                .AddPolicy(permission, policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(CurrentUser.PermissionClaimType, permission));
        }

        /*
          ═══════════════════════════════════════════════════════════
           YAGONA ROL BO'YICHA SIYOSAT — qolganlari ruxsat bo'yicha.

           ⭐ Nega bu yerda istisno bor. Tizimda kirish har doim
           RUXSAT bilan tekshiriladi: rol — bu shunchaki ruxsatlar
           to'plamining nomi va endpoint uning nomini bilishi shart
           emas. Bitta joyda bu qoida ishlamaydi — bot Telegram
           tokenini o'qiydigan endpoint.

           Ruxsat bilan qilinganda yangi ruxsat kodi kerak bo'lardi
           («settings.botsecrets» kabi). Lekin `SystemRoles.Defaults`
           da super administrator `Permissions.All` ni, bosh rahbar esa
           `All` dan `settings.manage` ni chiqarib tashlagan holini
           OLADI — ya'ni yangi ruxsat ikkalasiga ham O'ZI yetib borardi
           va tokenni ikki kishi o'qiy oladigan bo'lardi. Holbuki
           tokenni QO'YGAN super administratorning o'zi ham uni
           qaytarib o'qiy olmaydi (`SystemSettingsReader.Describe`).
           Ularni ro'yxatdan qo'lda chiqarish esa `RoleTests` dagi
           ikkita qat'iy testni buzardi va «rahbariyat nimani ko'ra
           olmaydi?» degan ro'yxatni ikkiga ko'paytirardi.

           Rol bilan esa savol to'g'ri qo'yiladi: bu endpoint odam
           uchun EMAS, JARAYON uchun. Uni bot hisobidan boshqa hech
           kim — na HR, na bosh rahbar, na super administrator —
           chaqira olmaydi.
          ═══════════════════════════════════════════════════════════
        */
        services.AddAuthorizationBuilder()
            .AddPolicy(BotServicePolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(SystemRoles.RecruitingBot));

        // `RequireClaim` bir nechta qiymat olsa — ULARDAN BIRI yetarli.
        services.AddAuthorizationBuilder()
            .AddPolicy(RolesReadPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(CurrentUser.PermissionClaimType, Permissions.UsersManage, Permissions.RolesManage));

        /*
          ⭐ XODIM SIYOSATI — ruxsat kodi YO'Q, AUDITORIYA bor.

          `/api/me/*` endpointlari ruxsat bilan qo'riqlanmaydi va
          qo'riqlanishi ham mumkin emas: xodim tokenida ruxsat da'volari
          umuman yo'q va bo'lmasligi kerak. Ularning butun himoyasi ikki
          narsada: (1) so'rov AYNAN xodim sxemasi bilan tasdiqlanadi —
          HR tokeni bu sxemadan o'tmaydi (auditoriya boshqa); (2) har bir
          ishlovchi xodim identifikatorini TOKENDAN oladi, so'rovdan
          emas, ya'ni «boshqa odamning ma'lumoti» degan yo'l yo'q.

          `AuthenticationSchemes` — siyosatning eng muhim qatori: usiz
          sukut sxema (HR) ham sinab ko'rilardi.
        */
        services.AddAuthorizationBuilder()
            .AddPolicy(EmployeePolicy, policy => policy
                .AddAuthenticationSchemes(EmployeeScheme)
                .RequireAuthenticatedUser());

        return services;
    }
}
