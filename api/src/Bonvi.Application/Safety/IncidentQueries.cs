using Bonvi.Application.Safety.Common;
using Bonvi.Application.Safety.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Safety;

namespace Bonvi.Application.Safety;

/// <summary>Baxtsiz hodisalar jurnali — hodisa sanasi bo'yicha kamayish tartibida.</summary>
/// <param name="From">Quyi chegara (o'zi ham kiradi). Bo'sh — chegarasiz.</param>
/// <param name="To">Yuqori chegara (o'zi ham kiradi). Bo'sh — chegarasiz.</param>
/// <param name="Severity">Faqat shu darajadagilar. Bo'sh — hammasi.</param>
/// <param name="Search">
/// Xodim bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki telefon —
/// xodimlar ro'yxatidagi bilan aynan bir xil qoida. Qidiruv SERVERDA
/// bajariladi (barcha ro'yxatlarda bir xil qoida): jurnal yillar davomida
/// o'sadi va klientda kesish bir kun jimgina noto'g'ri natija berardi.
/// </param>
public sealed record ListIncidentsQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    IncidentSeverity? Severity = null,
    string? Search = null);

public sealed class ListIncidentsQueryHandler
{
    private readonly IIncidentRepository _incidents;
    private readonly IncidentMapper _mapper;

    public ListIncidentsQueryHandler(IIncidentRepository incidents, IncidentMapper mapper)
    {
        _incidents = incidents;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<IncidentDto>>> HandleAsync(
        ListIncidentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Bo'sh matn `null` ga aylantiriladi: «?search=» — «qidiruvsiz»,
        // «hech narsa topilmasin» emas (barcha ro'yxatlarda bir xil qoida).
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        var rows = await _incidents
            .ListAsync(query.From, query.To, query.Severity, search, cancellationToken)
            .ConfigureAwait(false);

        var items = new List<IncidentDto>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(await _mapper.ToDtoAsync(row, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success<IReadOnlyList<IncidentDto>>(items);
    }
}

/// <summary>Bitta xodimning barcha baxtsiz hodisalari.</summary>
/// <remarks>
/// <para>
/// <b>Nega jurnalning «search» i yaramaydi.</b> U MATN bo'yicha qidiradi
/// (F.I.Sh., JSHSHIR) va bir xil ismli ikki odamni ajrata olmaydi —
/// xodim kartasida esa aynan SHU odamning yozuvlari kerak, taxminan
/// mos keladiganlari emas. Shu sababli alohida so'rov,
/// <c>GetExitInterviewByEmploymentQuery</c> bilan bir xil naqsh.
/// </para>
/// <para>
/// Ro'yxat BO'SH bo'lishi odatiy hol (ko'pchilikda hodisa yo'q) — bu
/// xato emas, 200 va bo'sh massiv qaytadi.
/// </para>
/// </remarks>
public sealed record ListEmployeeIncidentsQuery(Guid EmployeeId);

public sealed class ListEmployeeIncidentsQueryHandler
{
    private readonly IIncidentRepository _incidents;
    private readonly IncidentMapper _mapper;

    public ListEmployeeIncidentsQueryHandler(IIncidentRepository incidents, IncidentMapper mapper)
    {
        _incidents = incidents;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<IncidentDto>>> HandleAsync(
        ListEmployeeIncidentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = await _incidents
            .GetByEmployeeAsync(query.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        var items = new List<IncidentDto>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(await _mapper.ToDtoAsync(row, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success<IReadOnlyList<IncidentDto>>(items);
    }
}

public sealed record GetIncidentQuery(Guid IncidentId);

public sealed class GetIncidentQueryHandler
{
    private readonly IIncidentRepository _incidents;
    private readonly IncidentMapper _mapper;

    public GetIncidentQueryHandler(IIncidentRepository incidents, IncidentMapper mapper)
    {
        _incidents = incidents;
        _mapper = mapper;
    }

    public async Task<Result<IncidentDto>> HandleAsync(
        GetIncidentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var incident = await _incidents.GetByIdAsync(query.IncidentId, cancellationToken).ConfigureAwait(false);
        if (incident is null)
        {
            return SafetyErrors.NotFound;
        }

        return await _mapper.ToDtoAsync(incident, cancellationToken).ConfigureAwait(false);
    }
}
