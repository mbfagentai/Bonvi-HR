using Bonvi.Api.Extensions;
using Bonvi.Application.Identity.EmployeeAuth;

namespace Bonvi.Api.Endpoints;

// ════════════════════════════════════════════════════════════════════
//  So'rov tanalari
//
//  ⚠️ `Guid X = default` BU YERDA HAM YOZILMAYDI — .NET 10 ning OpenAPI
//  generatori bunday yozuvda butun hujjatni 500 bilan yiqitadi
//  (`AttendanceEndpoints` boshidagi izoh).
// ════════════════════════════════════════════════════════════════════

/// <summary>Xodim ilovasiga kirish tanasi.</summary>
/// <param name="Login">Telefon raqami: 9 xona yoki +998 bilan.</param>
/// <param name="Password">Parol.</param>
public sealed record EmployeeLoginRequest(string? Login, string? Password);

/// <summary>Xodim sessiyasini yangilash tanasi.</summary>
/// <param name="RefreshToken">Kirishda berilgan yangilash tokeni.</param>
public sealed record EmployeeRefreshRequest(string? RefreshToken) : IRefreshTokenBody;

/// <summary>Xodim ilovasidan chiqish tanasi.</summary>
/// <param name="RefreshToken">Shu qurilmaning yangilash tokeni — aynan u yopiladi.</param>
public sealed record EmployeeLogoutRequest(string? RefreshToken) : IRefreshTokenBody;

/// <summary>Parolni almashtirish tanasi.</summary>
/// <param name="CurrentPassword">Joriy parol.</param>
/// <param name="NewPassword">Yangi parol.</param>
public sealed record ChangeEmployeePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>
/// Xodim ilovasining autentifikatsiya endpointlari.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Uchalasi ham <c>bonvi-employee</c> auditoriyasidagi token beradi
/// va uning ichida RUXSAT DA'VOLARI YO'Q</b> (SPEC §D2). Bunday token HR
/// endpointiga ko'rsatilsa, imzosi to'g'ri bo'lsa ham auditoriya
/// tekshiruvidan o'tmaydi — «deyarli ruxsatli» degan holat mavjud emas.
/// </para>
/// <para>
/// ⚠️ <b>Tezlik cheklovi BU YERDA yozilmagan</b> — yo'llar
/// <c>RateLimitingOptions</c> ro'yxatlarida turadi, ya'ni cheklov marshrut
/// muallifi uni yozishni unutsa ham ishlaydi. Xodim kirishi HR kirishidan
/// ALOHIDA bo'linmada (<c>EmployeeLoginPaths</c>): zavodning bitta Wi-Fi
/// manzilidan smena boshida o'nlab odam kiradi va HR ning daqiqasiga
/// beshtalik chegarasi ularni ham, HR ni ham 429 bilan to'sib qo'yardi.
/// Parol terib ko'rishga qarshi asosiy to'siq — (login, IP) bo'yicha blok
/// (<c>EmployeeLoginThrottle</c>). Yangilash va chiqish esa tokenning
/// xeshi bo'yicha cheklanadi (<see cref="RateLimitingExtensions.RequireRefreshTokenRateLimiting{TBuilder}"/>).
/// </para>
/// </remarks>
public static class EmployeeAuthEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/employee-auth").WithTags("EmployeeAuth");

        group.MapPost("/login", async (
                EmployeeLoginRequest request,
                EmployeeLoginCommandHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new EmployeeLoginCommand(request.Login, request.Password, ClientIp(http)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .AllowAnonymous()
            .WithName("EmployeeLogin")
            .WithSummary("Xodim ilovasi — kirish")
            .WithDescription(
                "HR bergan telefon raqami va parol bilan kirish. Javob: token juftligi va " +
                "«mustChangePassword» — bir martalik parol hali almashtirilmagan bo'lsa «true», " +
                "u holda ilova faqat parol o'rnatish ekranini ochadi. " +
                "Xatolar: «xodim.notogri_malumot» (400 — login, parol, ishdan ketgan yoki " +
                "o'chirilgan xodim; hammasi BITTA javob), «xodim.bloklangan» (403 — shu " +
                "login va shu manzildan 5 ta xato, 15 daqiqa; javobda «retryAfterMinutes»; " +
                "hisob bor-yo'qligidan qat'i nazar bir xil), «xodim.hisob_faol_emas» (403).")
            .Produces<EmployeeAuthResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/refresh", async (
                EmployeeRefreshRequest request,
                EmployeeRefreshCommandHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new EmployeeRefreshCommand(request.RefreshToken, ClientIp(http)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .AllowAnonymous()
            .RequireRefreshTokenRateLimiting()
            .WithName("EmployeeRefresh")
            .WithSummary("Xodim ilovasi — sessiyani yangilash")
            .WithDescription(
                "Eski token bekor qilinadi va yangi juftlik beriladi. Bekor qilingan token " +
                "qayta ishlatilsa — o'g'irlik belgisi: hisobning BARCHA sessiyalari yopiladi. " +
                "ISTISNO: hozirgina (60 soniya ichida) almashtirilgan token, o'rniga " +
                "berilgani hali ishlatilmagan bo'lsa, AYNAN O'SHA juftlikni qayta oladi — " +
                "javobi yo'lda yo'qolgan so'rov. Ishdan ketgan yoki hisobi yopilgan xodimda " +
                "yangilash ishlamaydi. Xato: «xodim.token_yaroqsiz» (403), " +
                "«rate_limit.oshib_ketdi» (429).")
            .Produces<EmployeeAuthResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/logout", async (
                EmployeeLogoutRequest request,
                EmployeeLogoutCommandHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new EmployeeLogoutCommand(request.RefreshToken, ClientIp(http)), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })

            // Anonim — access token muddati tugagan bo'lsa ham chiqish ishlashi
            // kerak. Ishonchnoma — tanadagi yangilash tokeni (ishlovchi izohi).
            .AllowAnonymous()
            .RequireRefreshTokenRateLimiting()
            .WithName("EmployeeLogout")
            .WithSummary("Xodim ilovasi — chiqish")
            .WithDescription(
                "Shu qurilmaning yangilash tokenini bekor qiladi — server tomonidagi sessiya " +
                "ham yopiladi, hisobning boshqa qurilmalari esa tegilmaydi. Javob HAR DOIM 204: " +
                "noma'lum yoki allaqachon yopilgan token ham (holatini tekshiradigan oracle " +
                "bo'lmasligi uchun).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/password", async (
                ChangeEmployeePasswordRequest request,
                ChangeEmployeePasswordCommandHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ChangeEmployeePasswordCommand(
                            request.CurrentPassword,
                            request.NewPassword,
                            ClientIp(http)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })

            // ⚠️ Xodim SXEMASI, lekin `/api/me/*` dagi «parol almashtirilgan
            // bo'lsin» filtridan TASHQARIDA — aks holda majburiy birinchi
            // almashtirish umuman bajarilmas edi (sababi ishlovchida).
            .RequireAuthorization(AuthenticationExtensions.EmployeePolicy)
            .WithName("ChangeEmployeePassword")
            .WithSummary("Xodim ilovasi — parolni almashtirish")
            .WithDescription(
                "Joriy parol MAJBURIY, garchi so'rov tasdiqlangan token bilan kelsa ham: " +
                "ochiq qolgan telefon parolni almashtirishning eng oson yo'li bo'lib qolmasligi kerak " +
                "(birinchi kirishda — HR bergan bir martalik parol). " +
                "Almashgandan keyin barcha eski sessiyalar yopiladi va javobda YANGI juftlik keladi — " +
                "mijoz uni saqlab, eski yangilash tokenini tashlashi SHART. " +
                "Xatolar: «xodim.parol_notogri», «foydalanuvchi.parol_bosh», «foydalanuvchi.parol_qisqa» (400); " +
                "«xodim.hisob_faol_emas», «xodim.bloklangan» (403 — 5 ta noto'g'ri joriy parol, " +
                "15 daqiqa).")
            .Produces<EmployeeAuthResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    /// <summary>Mijozning haqiqiy IP manzili.</summary>
    /// <remarks>
    /// Caddy orqasida <c>ForwardedHeaders</c> middleware'i uni allaqachon
    /// to'g'rilagan — bu yerda tayyor qiymat o'qiladi (<c>AuthEndpoints</c>
    /// dagi bilan bir xil).
    /// </remarks>
    private static string? ClientIp(HttpContext http)
        => http.Connection.RemoteIpAddress?.ToString();
}
