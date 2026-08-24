using Bonvi.Api.Common;
using Bonvi.Api.Extensions;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Identity.Commands.Login;
using Bonvi.Application.Identity.Commands.Logout;
using Bonvi.Application.Identity.Commands.RefreshSession;
using Bonvi.Application.Identity.Dtos;
using Bonvi.Application.Identity.Photos;
using Bonvi.Application.Identity.Queries.GetCurrentUser;
using Microsoft.AspNetCore.Mvc;

namespace Bonvi.Api.Endpoints;

/// <summary>Kirish so'rovi tanasi.</summary>
/// <param name="Login">Telefon raqami: 9 xona yoki +998 bilan.</param>
/// <param name="Password">Parol.</param>
public sealed record LoginRequest(string Login, string Password);

/// <summary>Sessiyani yangilash so'rovi tanasi.</summary>
/// <param name="RefreshToken">Kirishda berilgan yangilash tokeni.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Chiqish so'rovi tanasi.</summary>
/// <param name="RefreshToken">Bekor qilinadigan token.</param>
/// <param name="AllSessions">Barcha qurilmalardagi sessiyalarni yopishmi.</param>
public sealed record LogoutRequest(string? RefreshToken, bool AllSessions = false);

/// <summary>
/// Autentifikatsiya endpoint'lari.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // ---------- Kirish ----------
        group.MapPost("/login", async (
                LoginRequest request,
                LoginCommandHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new LoginCommand(request.Login, request.Password, ClientIp(http)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Tizimga kirish")
            .WithDescription("Telefon raqami va parol bilan kirish. Access va refresh token qaytaradi.")
            .Produces<AuthResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // ---------- Sessiyani yangilash ----------
        group.MapPost("/refresh", async (
                RefreshRequest request,
                RefreshSessionCommandHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new RefreshSessionCommand(request.RefreshToken, ClientIp(http)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .AllowAnonymous()
            .WithName("RefreshSession")
            .WithSummary("Sessiyani yangilash")
            .WithDescription("Access token muddati tugaganda yangi juftlik oladi. Eski token bekor qilinadi.")
            .Produces<AuthResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // ---------- Chiqish ----------
        group.MapPost("/logout", async (
                LogoutRequest request,
                LogoutCommandHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new LogoutCommand(request.RefreshToken, request.AllSessions, ClientIp(http)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .AllowAnonymous()
            .WithName("Logout")
            .WithSummary("Tizimdan chiqish")
            .WithDescription("Yangilash tokenini bekor qiladi. Token bo'lmasa ham muvaffaqiyat qaytaradi.")
            .Produces(StatusCodes.Status204NoContent);

        // ---------- Joriy foydalanuvchi ----------
        group.MapGet("/me", async (
                GetCurrentUserQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Joriy foydalanuvchi")
            .WithDescription("Tizimga kirgan foydalanuvchi, uning rollari va ruxsatlari. Ruxsatlar bazadan o'qiladi.")
            .Produces<CurrentUserDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        MapMyPhoto(group);

        return app;
    }

    /// <summary>
    /// Rasm so'rovi tanasining eng katta hajmi.
    /// </summary>
    /// <remarks>
    /// Faylning o'zi 10 MB bilan cheklangan (<see cref="PhotoLimits.MaxBytes"/>),
    /// lekin <c>multipart/form-data</c> tanasiga chegara satrlari va maydon
    /// sarlavhalari ham qo'shiladi. 64 KB zaxira aynan shu «konvert» uchun.
    /// </remarks>
    private const long MaxPhotoRequestBytes = PhotoLimits.MaxBytes + (64 * 1024);

    /// <summary>
    /// Hisob surati: berish, yuklash, o'chirish.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega «me» va nega identifikatorsiz.</b> Uchala endpoint ham faqat
    /// SO'ROVCHINING o'z hisobiga tegadi va foydalanuvchi identifikatori
    /// manzilda umuman qatnashmaydi — u tokendan olinadi. Shu sababli
    /// «boshqa odamning rasmini almashtirish» degan holat texnik jihatdan
    /// mavjud emas va uni ruxsat kodi bilan qo'riqlash kerak emas:
    /// <c>RequireAuthorization()</c> — «kirgan bo'lsang bo'ldi».
    /// </para>
    /// <para>
    /// <b>Nega xodim rasmidan alohida.</b> Tizimga kiradigan hisoblar xodim
    /// kartasiga bog'lanmagan (<c>employee_id</c> bo'sh), ya'ni xodim
    /// suratini qayta ishlatib bo'lmasdi. Quvurning o'zi esa umumiy —
    /// <c>SquarePhotoStore</c> ikkalasiga ham xizmat qiladi.
    /// </para>
    /// </remarks>
    private static void MapMyPhoto(IEndpointRouteBuilder group)
    {
        group.MapGet("/me/photo", async (
                HttpContext context,
                GetAccountPhotoQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GetAccountPhotoQuery(PhotoHttp.ReadKnownVersion(context.Request)),
                        cancellationToken)
                    .ConfigureAwait(false);

                // JSON emas, BAYT qaytariladi — shu sababli `ToHttpResult()`
                // faqat xato yo'lida ishlatiladi: aks holda u oqimni obyekt
                // deb serializatsiya qilib yuborardi.
                return natija.IsFailure
                    ? natija.ToHttpResult()
                    : PhotoHttp.Write(context, natija.Value);
            })
            .RequireAuthorization()
            .WithName("GetMyPhoto")
            .WithSummary("Hisob — rasm")
            .WithDescription(
                "Joriy foydalanuvchining rasmi (image/jpeg). «ETag» saqlash kalitidan olingan " +
                "barqaror xesh; «If-None-Match» mos kelsa 304 qaytadi. Kesh SHAXSIY (private, 1 kun). " +
                "Rasm yo'q bo'lsa: «hisob.rasm_yoq» (404).")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/me/photo", async (
                IFormFile file,
                UploadAccountPhotoCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                await using var content = file.OpenReadStream();

                var natija = await handler
                    .HandleAsync(
                        new UploadAccountPhotoCommand(content, file.ContentType, file.Length),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization()

            // CSRF himoyasi bu yerda ORTIQCHA: sessiya cookie'da emas, JWT
            // sarlavhasida yuriladi va boshqa saytdagi forma uni qo'sha olmaydi.
            .DisableAntiforgery()

            // Cheklov marshrut darajasida: chegaradan katta tana Kestrel
            // tomonidan umuman o'qilmaydi va serverga yuk tushmaydi.
            .WithMetadata(new RequestSizeLimitAttribute(MaxPhotoRequestBytes))
            .WithName("UploadMyPhoto")
            .WithSummary("Hisob — rasm yuklash")
            .WithDescription(
                "«multipart/form-data», maydon nomi «file». JPEG, PNG yoki WebP, eng ko'pi 10 MB. " +
                "Server rasmni markazdan kvadrat qirqib 512×512 JPEG qiladi va eskisini o'chiradi. " +
                "Faqat O'Z hisobiga tegadi. Xatolar: «hisob.rasm_katta», «hisob.rasm_turi», " +
                "«hisob.rasm_yaroqsiz» (hammasi 400).")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<AccountPhotoDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapDelete("/me/photo", async (
                DeleteAccountPhotoCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);
                return natija.ToHttpResult();
            })
            .RequireAuthorization()
            .WithName("DeleteMyPhoto")
            .WithSummary("Hisob — rasmni o'chirish")
            .WithDescription(
                "Fayl ham o'chadi. Amal IDEMPOTENT: rasm allaqachon yo'q bo'lsa ham 204 qaytadi — " +
                "so'rovning maqsadi rasm qolmasligi, u esa bajarilgan.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// Mijozning haqiqiy IP manzili.
    /// </summary>
    /// <remarks>
    /// Caddy orqasida ishlaganda <c>RemoteIpAddress</c> proksi manzilini beradi.
    /// <c>ForwardedHeaders</c> middleware'i uni to'g'rilaydi — bu yerda esa
    /// allaqachon to'g'rilangan qiymat o'qiladi.
    /// </remarks>
    private static string? ClientIp(HttpContext http)
        => http.Connection.RemoteIpAddress?.ToString();
}
