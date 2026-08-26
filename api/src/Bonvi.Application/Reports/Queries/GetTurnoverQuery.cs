using Bonvi.Application.Reports.Abstractions;
using Bonvi.Application.Reports.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Reports.Queries;

/// <summary>Kadrlar oqimi hisobotini so'rash.</summary>
/// <param name="Months">
/// So'nggi nechta OY (kalendar oylar, joriy — hali tugamagan — oy ham hisobga
/// kiradi). Berilmasa — <see cref="GetTurnoverQueryHandler.DefaultMonths"/>.
/// </param>
public sealed record GetTurnoverQuery(int? Months = null);

/// <summary>
/// <see cref="GetTurnoverQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <b>Chegara nega bor (1–24).</b> Grafik cheksiz oyni sig'dira olmaydi —
/// 24 oydan (2 yil) ortig'i ustunlarni o'qib bo'lmas darajada mayda qilib
/// yuborardi. Pastki chegara (1) esa manfiy yoki nolni to'sadi: ular
/// «oy oralig'i teskari» degan tushunarsiz natija berardi.
/// </remarks>
public sealed class GetTurnoverQueryHandler
{
    /// <summary>Sukut bo'yicha — so'nggi bir yil.</summary>
    public const int DefaultMonths = 12;

    /// <summary>Eng ko'p — 2 yil.</summary>
    public const int MaxMonths = 24;

    private readonly IReportsReadModel _reports;
    private readonly IClock _clock;

    public GetTurnoverQueryHandler(IReportsReadModel reports, IClock clock)
    {
        _reports = reports;
        _clock = clock;
    }

    public async Task<Result<TurnoverDto>> HandleAsync(
        GetTurnoverQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var months = query.Months ?? DefaultMonths;

        if (months is < 1 or > MaxMonths)
        {
            return Error.Validation(
                "hisobot.oy_notogri",
                $"Oylar soni 1 dan {MaxMonths} gacha bo'lishi kerak.");
        }

        var today = _clock.Today;

        // Joriy oyning 1-sanasidan (months - 1) oy orqaga — natijada
        // JORIY oy ham qatorga kiradi (u hali tugamagan bo'lsa ham HR
        // «bu oy hozircha nima bo'ldi» degan savolga javob kutadi).
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));

        var report = await _reports
            .GetTurnoverAsync(new TurnoverRange(from, today), cancellationToken)
            .ConfigureAwait(false);

        return new TurnoverDto(
            from,
            today,
            [.. report.Months.Select(ToDto)],
            [.. report.Reasons.Select(ToDto)]);
    }

    private static TurnoverMonthDto ToDto(TurnoverMonth month) => new(
        month.MonthStart,
        month.MonthEnd,
        month.Hired,
        month.Left,
        month.HeadcountStart,
        month.HeadcountEnd);

    private static TurnoverReasonDto ToDto(TurnoverReasonBucket bucket) => new(
        bucket.ReasonId,
        bucket.ReasonName,
        bucket.IsVoluntary,
        bucket.Count);
}
