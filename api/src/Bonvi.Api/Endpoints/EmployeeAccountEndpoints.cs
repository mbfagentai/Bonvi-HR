using Bonvi.Api.Extensions;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Ilova hisobini ochish tanasi.</summary>
/// <param name="Login">
/// Telefon raqami. Bo'sh qoldirilsa — xodim kartasidagi asosiy raqam.
/// </param>
public sealed record IssueEmployeeAccountRequest(string? Login = null);

/// <summary>«Telefondan belgilash» o'chirgichining tanasi.</summary>
/// <param name="Allowed">Ruxsat berilsinmi.</param>
public sealed record MobileCheckInAllowedRequest(bool Allowed);

/// <summary>
/// Xodim kartasining «Ilova» bo'limi — <c>/api/employees/{id}/app-access</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Ruxsat — <c>attendance.manage</c>, <c>users.manage</c> emas.</b>
/// Sabab: <c>users.manage</c> TIZIM hisoblariga (rol berish, HR
/// foydalanuvchilarini yaratish) tegadi va u faqat rahbariyatda. Bu yerdagi
/// hisob esa rolsiz, ruxsatsiz va faqat o'z davomatini ko'radi — uni
/// ochish kundalik kadrlar ishi va 950 ta hisobni rahbar qo'li bilan
/// ochtirib bo'lmaydi. <c>attendance.manage</c> to'g'ri chegarada:
/// qurilmalarni va davomat sozlamalarini yurituvchi odam telefon
/// belgisini ham yuritadi.
/// </para>
/// <para>
/// ⚠️ <b>Marshrut xodim ostida</b> (<c>/api/employees/{id}/…</c>), hisob
/// identifikatori ostida emas: HR ekranda XODIMni ochadi va hisob
/// bor-yo'qligini oldindan bilmaydi. Hisobi yo'q odamda ham javob
/// to'liq keladi (<c>account: null</c>), 404 emas.
/// </para>
/// <para>
/// ⭐ <b>Bir martalik parol FAQAT ochish va tiklash javobida ko'rinadi.</b>
/// Uni qaytarib beradigan endpoint YO'Q — qurilma qabul tokeni bilan
/// aynan bir xil marosim, ya'ni HR uchun tanish.
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
                "«Telefondan belgilash» bayrog'i va ilova hisobi. Hisobi yo'q xodimda " +
                "«account» — null (404 emas): ekran «hisob ochish» holatini ko'rsatishi kerak. " +
                "Parol HECH QACHON qaytmaydi. Xato: «davomat.xodim_topilmadi» (404).")
            .Produces<EmployeeAppAccessDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/app-access", async (
                Guid id,
                IssueEmployeeAccountRequest request,
                IssueEmployeeAccountCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new IssueEmployeeAccountCommand(id, request.Login), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("IssueEmployeeAccount")
            .WithSummary("Xodim — ilova hisobini ochish")
            .WithDescription(
                "Javob { account, password } — parol FAQAT SHU javobda bir marta ko'rsatiladi " +
                "va boshqa hech qachon qaytmaydi (yo'qolsa — «reset-password»). " +
                "«login» berilmasa xodim kartasidagi telefon olinadi. " +
                "Xatolar: «telefon.bosh», «telefon.uzunlik» (400); «davomat.xodim_topilmadi» (404); " +
                "«xodim.hisob_bor», «xodim.login_band» (409 — xodimlarda takroriy telefon raqami " +
                "ko'p uchraydi).")
            .Produces<EmployeeAccountCreatedDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/app-access/reset-password", async (
                Guid id,
                ResetEmployeeAccountPasswordCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new ResetEmployeeAccountPasswordCommand(id), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("ResetEmployeeAccountPassword")
            .WithSummary("Xodim — ilova parolini tiklash")
            .WithDescription(
                "Yangi bir martalik parol beriladi va xodimning BARCHA sessiyalari yopiladi. " +
                "«Bu odam umuman kirganmi» ma'lumoti («lastLoginAt») saqlanib qoladi. " +
                "Xato: «xodim.hisob_topilmadi» (404).")
            .Produces<EmployeeAccountCreatedDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/app-access/deactivate", async (
                Guid id,
                SetEmployeeAccountActiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SetEmployeeAccountActiveCommand(id, Active: false), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("DeactivateEmployeeAccount")
            .WithSummary("Xodim — ilova hisobini yopish")
            .WithDescription(
                "Hisob yopiladi VA barcha ochiq sessiyalar bekor qilinadi — aks holda ochiq " +
                "ilova yana bir necha hafta ishlayverardi. Parol o'zgarmaydi. " +
                "⚠️ Bu «ishdan ketdi» degani EMAS: ishdan ketish mehnat munosabati sanalari " +
                "bo'yicha o'zi kuchga kiradi va hisobni yopishni talab qilmaydi.")
            .Produces<EmployeeAccountDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/app-access/activate", async (
                Guid id,
                SetEmployeeAccountActiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new SetEmployeeAccountActiveCommand(id, Active: true), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.AttendanceManage)
            .WithName("ActivateEmployeeAccount")
            .WithSummary("Xodim — ilova hisobini qaytarish")
            .WithDescription("Eski parol kuchida qoladi; blok va xato hisoblagichi tozalanadi.")
            .Produces<EmployeeAccountDto>(StatusCodes.Status200OK)
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
                "Ruxsat XODIMGA beriladi, hisobga emas — ya'ni hisobi hali yo'q odamga ham " +
                "oldindan yoqib qo'yish mumkin va hisob qayta berilganda u nolga qaytmaydi. " +
                "Xato: «davomat.xodim_topilmadi» (404).")
            .Produces<EmployeeAppAccessDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
