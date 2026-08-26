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
}
