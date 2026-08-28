using Bonvi.Application.Feedback.Common;
using Bonvi.Application.Feedback.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Feedback;

/// <summary>
/// «Qayta aloqa» jurnali — barcha suhbatlar, suhbat sanasi bo'yicha
/// kamayish tartibida.
/// </summary>
/// <param name="From">Quyi chegara (o'zi ham kiradi). Bo'sh — chegarasiz.</param>
/// <param name="To">Yuqori chegara (o'zi ham kiradi). Bo'sh — chegarasiz.</param>
/// <param name="Search">
/// Xodim bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki telefon —
/// xodimlar ro'yxatidagi bilan aynan bir xil qoida. Qidiruv SERVERDA
/// bajariladi: jurnal ishdan ketgan har bir odam uchun o'sib boradi va
/// klientda kesish bir kun jimgina noto'g'ri natija berardi.
/// </param>
public sealed record ListExitInterviewsQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    string? Search = null);

public sealed class ListExitInterviewsQueryHandler
{
    private readonly IExitInterviewRepository _interviews;
    private readonly ExitInterviewMapper _mapper;

    public ListExitInterviewsQueryHandler(IExitInterviewRepository interviews, ExitInterviewMapper mapper)
    {
        _interviews = interviews;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<ExitInterviewDto>>> HandleAsync(
        ListExitInterviewsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Bo'sh matn `null` ga aylantiriladi: «?search=» — «qidiruvsiz»,
        // «hech narsa topilmasin» emas (barcha ro'yxatlarda bir xil qoida).
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        var rows = await _interviews
            .ListAsync(query.From, query.To, search, cancellationToken)
            .ConfigureAwait(false);

        var items = new List<ExitInterviewDto>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(await _mapper.ToDtoAsync(row, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success<IReadOnlyList<ExitInterviewDto>>(items);
    }
}

/// <summary>
/// Bitta mehnat munosabatiga tegishli suhbat — xodim kartasidagi
/// «mehnat tarixi» tabida ko'rsatish uchun.
/// </summary>
public sealed record GetExitInterviewByEmploymentQuery(Guid EmploymentId);

/// <remarks>
/// Yozuv yo'q bo'lsa — <c>404</c> (<see cref="FeedbackErrors.NotFound"/>),
/// bo'sh <c>200</c> EMAS: minimal API'da <c>Results.Ok(null)</c> JSON
/// «null» YOZMAYDI, balki TANASIZ 200 qaytaradi — mijoz tomonida
/// <c>response.json()</c> bo'sh tanada istisno tashlaydi. 404 — bu
/// xatolikni butunlay yo'qotadi va baribir kutilgan holatning o'zi
/// (frontend uni «suhbat hali yo'q» deb, xatosiz ko'rsatishi kerak).
/// </remarks>
public sealed class GetExitInterviewByEmploymentQueryHandler
{
    private readonly IExitInterviewRepository _interviews;
    private readonly ExitInterviewMapper _mapper;

    public GetExitInterviewByEmploymentQueryHandler(IExitInterviewRepository interviews, ExitInterviewMapper mapper)
    {
        _interviews = interviews;
        _mapper = mapper;
    }

    public async Task<Result<ExitInterviewDto>> HandleAsync(
        GetExitInterviewByEmploymentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var interview = await _interviews
            .GetByEmploymentAsync(query.EmploymentId, cancellationToken)
            .ConfigureAwait(false);

        if (interview is null)
        {
            return FeedbackErrors.NotFound;
        }

        return await _mapper.ToDtoAsync(interview, cancellationToken).ConfigureAwait(false);
    }
}
