using Bonvi.Api.Extensions;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>«Telefondan belgilash» o'chirgichining tanasi.</summary>
/// <param name="Allowed">Ruxsat berilsinmi.</param>
public sealed record MobileCheckInAllowedRequest(bool Allowed);

/// <summary>HR o'zi yozgan parolning tanasi.</summary>
/// <param name="Password">Yangi parol — 6–64 belgi, chetdagi bo'shliqlar olib tashlanadi.</param>
public sealed record SetEmployeePasswordRequest(string? Password);

/// <summary>
/// Xodim kartasining «Ilova» bo'limi — <c>/api/employees/{id}/app-access</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Ruxsat — <c>attendance.manage</c>, <c>users.manage</c> emas.</b>
/// Sabab: <c>users.manage</c> TIZIM hisoblariga (rol berish, HR
/// foydalanuvchilarini yaratish) tegadi va u faqat rahbariyatda. Bu yerdagi
/// hisob esa rolsiz, ruxsatsiz va faqat o'z davomatini ko'radi — uni
/// yuritish kundalik kadrlar ishi. <c>attendance.manage</c> to'g'ri
/// chegarada: qurilmalarni va davomat sozlamalarini yurituvchi odam telefon
/// belgisini ham yuritadi. Parolni ko'rish ham AYNAN shu ruxsat ostida.
/// </para>
/// <para>
/// ⭐ <b>Egasining qarori (2026-09-27):</b> hisob har bir xodimga
/// AVTOMATIK ochiladi, login — profildagi asosiy telefon, parol — 6 raqam
/// (yoki HR yozgani) va HR uni HAR DOIM ko'ra oladi. Qo'lda «hisob ochish»,
/// «hisobni yopish» va xodimning o'zi parol almashtirishi YO'Q; yagona
/// o'chirgich — ishdan ketish.
/// </para>
/// <para>
/// ⚠️ <b>Marshrut xodim ostida</b> (<c>/api/employees/{id}/…</c>), hisob
/// identifikatori ostida emas: HR ekranda XODIMni ochadi.
/// </para>
/// </remarks>
public static class EmployeeAccountEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeAccountEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/employees/{id:guid}").WithTags("EmployeeApp");

        group.MapGet("/app-access", async (
                Guid id,
                GetEmployeeAppAccessQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetEmployeeAppAccessQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("GetEmployeeAppAccess")
            .WithSummary("Xodim — «Ilova» bo'limi")
            .WithDescription(
                "Hisob HAR DOIM bor: yo'q bo'lsa shu so'rovda ochiladi (6 xonali parol bilan). " +
                "«login» — profildagi asosiy telefon; «loginIssue»: «None» — kirsa bo'ladi, " +
                "«NoPhone» — profilda raqam yo'q, «SharedPhone» — raqam boshqa o'chirilmagan " +
                "xodimda ham bor («sharedWith» ro'yxati); oxirgi ikkisida kirish rad etiladi. " +
                "«account.passwordAvailable» — parolni ko'rsatish mumkinmi. Parolning o'zi bu " +
                "javobda YO'Q. Xato: «davomat.xodim_topilmadi» (404).")
            .Produces<EmployeeAppAccessDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/app-access/password", async (
                Guid id,
                RevealEmployeePasswordQueryHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new RevealEmployeePasswordQuery(id), cancellationToken)
                    .ConfigureAwait(false);

                // Ochiq parol hech qanday keshda (brauzer, proksi) qolmasin.
                http.Response.Headers.CacheControl = "no-store";

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("RevealEmployeePassword")
            .WithSummary("Xodim — ilova parolini ko'rish")
            .WithDescription(
                "Joriy parol ochiq holda — HR xodimga aytib berishi uchun. Har bir ko'rish " +
                "auditga yoziladi. Xatolar: «xodim.parol_korinmaydi» (409 — shifrlangan nusxa " +
                "yo'q yoki kalit almashgan: yangi parol yasang), «davomat.xodim_topilmadi» (404).")
            .Produces<EmployeePasswordDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/app-access/password/generate", async (
                Guid id,
                EmployeePasswordCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new RegenerateEmployeePasswordCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("RegenerateEmployeePassword")
            .WithSummary("Xodim — yangi 6 xonali parol")
            .WithDescription(
                "Tasodifiy 6 raqamli parol beriladi (arzimas birikmalarsiz) va xodimning BARCHA " +
                "sessiyalari yopiladi. Javob — yangilangan «Ilova» bo'limi; parolning o'zi " +
                "«GET …/app-access/password» dan. Xato: «davomat.xodim_topilmadi» (404).")
            .Produces<EmployeeAppAccessDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/app-access/password", async (
                Guid id,
                SetEmployeePasswordRequest request,
                EmployeePasswordCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SetEmployeePasswordCommand(id, request.Password), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("SetEmployeePassword")
            .WithSummary("Xodim — o'z parolini qo'yish")
            .WithDescription(
                "HR yozgan parol (6–64 belgi; chetdagi bo'shliqlar olib tashlanadi). Xodimning " +
                "BARCHA sessiyalari yopiladi. Xatolar: «xodim.parol_bosh», «xodim.parol_qisqa», " +
                "«xodim.parol_uzun» (400); «davomat.xodim_topilmadi» (404).")
            .Produces<EmployeeAppAccessDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/mobile-check-in", async (
                Guid id,
                MobileCheckInAllowedRequest request,
                SetMobileCheckInAllowedCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SetMobileCheckInAllowedCommand(id, request.Allowed), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("SetMobileCheckInAllowed")
            .WithSummary("Xodim — «Telefondan belgilash» o'chirgichi")
            .WithDescription(
                "Sukut bo'yicha HAMMADA o'chiq: telefon belgisi soxtalashtirilishi mumkin va " +
                "uni hammaga ochish yuz terminalini ixtiyoriy qurilmaga aylantirardi. " +
                "Ruxsat XODIMGA beriladi, hisobga emas. " +
                "Xato: «davomat.xodim_topilmadi» (404).")
            .Produces<EmployeeAppAccessDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
