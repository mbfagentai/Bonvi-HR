using Bonvi.Api.Extensions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Employees.Incentives;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;

namespace Bonvi.Api.Endpoints;

/// <summary>Rag'bat/jarima berish so'rovi tanasi.</summary>
/// <param name="Kind">Rag'bat yoki jarima.</param>
/// <param name="TypeId">Tur — <c>Kind</c>ga qarab <c>award-types</c> yoki <c>penalty-types</c> ro'yxatidan.</param>
/// <param name="Date">Sana.</param>
/// <param name="Amount">Summa — faqat tur pul bilan bog'liq bo'lsa.</param>
/// <param name="GiftDescription">Sovg'a tavsifi (faqat rag'bat uchun).</param>
/// <param name="Reason">Sabab. Jarima uchun MAJBURIY.</param>
/// <param name="OrderNo">Buyruq raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
public sealed record GrantIncentiveRequest(
    IncentiveKind Kind,
    Guid TypeId,
    DateOnly Date,
    decimal? Amount = null,
    string? GiftDescription = null,
    string? Reason = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null);

/// <summary>Rag'bat/jarima yozuvini tuzatish so'rovi tanasi — <c>Kind</c> o'zgarmaydi.</summary>
public sealed record UpdateIncentiveRequest(
    Guid TypeId,
    DateOnly Date,
    decimal? Amount = null,
    string? GiftDescription = null,
    string? Reason = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null);

/// <summary>
/// Rag'bat va jarima endpoint'lari (M8).
/// </summary>
/// <remarks>
/// Ruxsatlar ALOHIDA guruh (<see cref="Permissions.IncentivesView"/> /
/// <see cref="Permissions.IncentivesManage"/>), <c>employees.*</c> emas:
/// pul (jarima/mukofot summasi) va intizomiy chora bilan bog'liq nozik
/// ma'lumot — sababi <c>Permissions.IncentivesView</c> izohida.
/// </remarks>
public static class IncentiveEndpoints
{
    public static IEndpointRouteBuilder MapIncentiveEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        MapAllIncentives(app);

        var group = app.MapGroup("/api/employees/{employeeId:guid}/incentives").WithTags("Incentives");

        group.MapGet("/", async (
                Guid employeeId,
                IncentiveKind? kind,
                GetEmployeeIncentivesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new GetEmployeeIncentivesQuery(employeeId, kind), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.IncentivesView)
            .WithName("GetEmployeeIncentives")
            .WithSummary("Xodim — rag'bat va jarima yozuvlari")
            .WithDescription(
                "Sana bo'yicha kamayish tartibida. «kind» = Award | Penalty; bo'sh bo'lsa — hammasi.")
            .Produces<IReadOnlyList<IncentiveDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                Guid employeeId,
                GrantIncentiveRequest request,
                GrantIncentiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new GrantIncentiveCommand(
                            employeeId,
                            request.Kind,
                            request.TypeId,
                            request.Date,
                            request.Amount,
                            request.GiftDescription,
                            request.Reason,
                            request.OrderNo,
                            request.OrderDate),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.IncentivesManage)
            .WithName("GrantIncentive")
            .WithSummary("Xodim — rag'bat/jarima berish")
            .WithDescription(
                "Jarima sababsiz berilmaydi: «ragbat_jarima.sabab_bosh» (400). Tanlangan turning " +
                "«hasAmount» belgisiga qarab summa MAJBURIY yoki MUMKIN EMAS: " +
                "«ragbat_jarima.summa_kerak» / «ragbat_jarima.summa_kerak_emas» (400).")
            .Produces<IncentiveDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{incentiveId:guid}", async (
                Guid incentiveId,
                UpdateIncentiveRequest request,
                UpdateIncentiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new UpdateIncentiveCommand(
                            incentiveId,
                            request.TypeId,
                            request.Date,
                            request.Amount,
                            request.GiftDescription,
                            request.Reason,
                            request.OrderNo,
                            request.OrderDate),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.IncentivesManage)
            .WithName("UpdateIncentive")
            .WithSummary("Xodim — rag'bat/jarima yozuvini tuzatish")
            .WithDescription(
                "Kiritish xatosini tuzatish uchun: tur, sana, summa, sovg'a, sabab, buyruq. " +
                "Rag'bat/jarima (Kind) o'zgartirilmaydi — noto'g'ri turga yozilgan bo'lsa, " +
                "yozuv o'chirilib yangisi kiritiladi.")
            .Produces<IncentiveDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{incentiveId:guid}", async (
                Guid incentiveId,
                DeleteIncentiveCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(new DeleteIncentiveCommand(incentiveId), cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.IncentivesManage)
            .WithName("DeleteIncentive")
            .WithSummary("Xodim — rag'bat/jarima yozuvini o'chirish")
            .WithDescription("Yumshoq o'chirish: yozuv bazada qoladi, faqat ro'yxatlarda ko'rinmay qoladi.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // ════════════════════════════════════════════════════════════
    //  Umumiy ro'yxat — butun tizim bo'yicha
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Butun tizim bo'yicha umumiy ro'yxat — <c>/api/incentives</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega ikkinchi guruh, yuqoridagisining ichida emas.</b> Yuqoridagi
    /// guruh yo'li <c>/api/employees/{employeeId}/incentives</c> — u xodim
    /// kartasining bir qismi va har bir so'rovi bitta odam haqida. Bu
    /// ro'yxat esa xodimdan qat'i nazar butun tizimni ko'radi, ya'ni
    /// boshqa manba. Uni «xodimsiz xodim yo'li» qilib yozish (masalan
    /// ixtiyoriy <c>employeeId</c>) marshrutni ham, ruxsat tekshiruvini
    /// ham chalkashtirardi.
    /// </para>
    /// <para>
    /// <b>Nega <c>Program.cs</c> ga tegilmadi.</b> Mavjud
    /// <see cref="MapIncentiveEndpoints"/> allaqachon ro'yxatga olingan va
    /// u endi ikkala guruhni ham chizadi — bitta modulning ikkita yo'li
    /// bitta joydan boshlanadi. Ikkinchi <c>Map…</c> metodi
    /// <c>Program.cs</c> da ham qayd etilishi kerak bo'lardi va kimdir uni
    /// unutsa, endpoint jimgina yo'qolib qolardi.
    /// </para>
    /// <para>
    /// Ruxsat ikkalasida ham bir xil — <see cref="Permissions.IncentivesView"/>:
    /// umumiy ro'yxat xodim kartasidagi tabga qaraganda ko'proq ma'lumot
    /// ochmaydi, faqat boshqa savolga javob beradi.
    /// </para>
    /// </remarks>
    private static void MapAllIncentives(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/incentives").WithTags("Incentives");

        group.MapGet("/", async (
                string? search,
                IncentiveKind? kind,
                DateOnly? from,
                DateOnly? to,
                Guid? departmentId,
                Guid? legalEntityId,
                int? page,
                int? pageSize,
                string? sort,
                ListIncentivesQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var natija = await handler
                    .HandleAsync(
                        new ListIncentivesQuery(
                            search,
                            kind,
                            from,
                            to,
                            departmentId,
                            legalEntityId,
                            page,
                            pageSize,
                            sort),
                        cancellationToken)
                    .ConfigureAwait(false);

                return natija.ToHttpResult();
            })
            .RequireAuthorization(Permissions.IncentivesView)
            .WithName("ListIncentives")
            .WithSummary("Rag'bat va jarima — barcha xodimlar bo'yicha umumiy ro'yxat")
            .WithDescription(
                "Excel'dagi «91.14 Тақдирланган ходимлар» varag'ining o'rniga, jarima bilan " +
                "birga: bitta oqim va uning filtrlangan ko'rinishlari. HAR BIR YOZUV alohida " +
                "qator — bitta xodim ro'yxatda bir necha marta ko'rinishi mumkin, yig'ma " +
                "ko'rsatkich berilmaydi. " +
                "«kind» = Award | Penalty, bo'sh bo'lsa — ikkalasi; noma'lum qiymat 400 beradi. " +
                "«search» — XODIM F.I.Sh. bo'yicha (buyruq raqami emas), «қ/ғ/ҳ/ў/ё» harflari " +
                "hisobga olinmaydi. «from»/«to» — sananing ikki tomondan yopiq oralig'i. " +
                "«departmentId» — bo'lim ICHKI bo'limlari bilan birga; bo'lim va yuridik shaxs " +
                "xodimning MUNOSABATLARI orqali qo'llanadi (o'rindoshlik ham hisobga olinadi). " +
                "«sort» = date | employee, oldiga «-» qo'yilsa kamayish tartibida; sukut " +
                "bo'yicha «-date» (yangidan eskiga). «pageSize» eng ko'pi 200. " +
                "Summa faqat pul bilan bog'liq turlarda to'ldiriladi, qolganlarida null — " +
                "u «salary.view» ga BOG'LIQ EMAS (sababi «ListIncentivesQuery» izohida).")
            .Produces<PagedResult<IncentiveListItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
