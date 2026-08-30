using System.Security.Claims;
using System.Text;
using Bonvi.Api.Common;
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

        return services;
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
    /// Tekshiruv token ichidagi <c>perm</c> claim'lari bo'yicha xotirada
    /// bajariladi — bazaga so'rov yo'q.
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

        return services;
    }
}
