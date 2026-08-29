using Bonvi.Application.Safety.Common;
using Bonvi.Application.Safety.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Safety;

namespace Bonvi.Application.Safety;

/// <summary>Baxtsiz hodisani yozib qo'yadi. Komissiya xulosasi bu yerda YO'Q — u <see cref="ConcludeIncidentCommand"/> orqali.</summary>
/// <remarks>
/// <b>Lavozim bu yerda YO'Q va u so'ralmaydi.</b> Xodimga lavozim
/// allaqachon biriktirilgan, uni formada ikkinchi marta so'rash ortiqcha
/// ish edi va xato kiritishning yo'lini ochardi. Yozuvga u baribir
/// tushadi — hodisa KUNIDAGI munosabatdan o'zi olinadi
/// (<see cref="IncidentPositionResolver"/>).
/// </remarks>
public sealed record RecordIncidentCommand(
    Guid EmployeeId,
    DateOnly IncidentDate,
    string? Location,
    string? Description,
    IncidentSeverity Severity,
    string? Shift = null,
    string? Cause = null,
    string? CorrectiveAction = null,
    bool MedicalAidProvided = false,
    string? MedicalAidNote = null,
    Guid? ResponsibleEmployeeId = null);

/// <summary>Kiritish xatosini tuzatadi — <c>EmployeeId</c> va komissiya xulosasi O'ZGARMAYDI.</summary>
/// <remarks>
/// Lavozim bu yerda ham YO'Q (<see cref="RecordIncidentCommand"/> izohi).
/// U hodisa SANASIGA bog'liq, sana esa tuzatilishi mumkin — shu sababli
/// har tuzatishda qaytadan hisoblanadi va eski qiymat ustiga yoziladi.
/// Aks holda «sanani tuzatdim, lekin lavozim eskiligicha qoldi» degan
/// jimgina nomuvofiqlik paydo bo'lardi.
/// </remarks>
public sealed record UpdateIncidentCommand(
    Guid IncidentId,
    DateOnly IncidentDate,
    string? Location,
    string? Description,
    IncidentSeverity Severity,
    string? Shift = null,
    string? Cause = null,
    string? CorrectiveAction = null,
    bool MedicalAidProvided = false,
    string? MedicalAidNote = null,
    Guid? ResponsibleEmployeeId = null);

/// <summary>Komissiya xulosasini yozadi (yoki tuzatadi).</summary>
public sealed record ConcludeIncidentCommand(Guid IncidentId, string? Conclusion, DateOnly? ConcludedAt = null);

public sealed record DeleteIncidentCommand(Guid IncidentId);

public sealed class RecordIncidentCommandHandler
{
    private readonly IIncidentRepository _incidents;
    private readonly IEmployeeRepository _employees;
    private readonly IncidentPositionResolver _positions;
    private readonly IncidentMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public RecordIncidentCommandHandler(
        IIncidentRepository incidents,
        IEmployeeRepository employees,
        IncidentPositionResolver positions,
        IncidentMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _incidents = incidents;
        _employees = employees;
        _positions = positions;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IncidentDto>> HandleAsync(
        RecordIncidentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employeeExists = await _employees.ExistsAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (!employeeExists)
        {
            return SafetyErrors.EmployeeNotFound;
        }

        if (command.ResponsibleEmployeeId is { } responsibleId
            && !await _employees.ExistsAsync(responsibleId, cancellationToken).ConfigureAwait(false))
        {
            return SafetyErrors.ResponsibleNotFound;
        }

        // Lavozim so'ralmaydi — hodisa kunidagi munosabatdan olinadi.
        // Ma'lumotnomada bor-yo'qligini tekshirish ham SHART EMAS: qiymat
        // foydalanuvchidan emas, o'z bazamizdan keladi.
        var positionId = await _positions
            .ResolveAsync(command.EmployeeId, command.IncidentDate, cancellationToken)
            .ConfigureAwait(false);

        var recorded = Incident.Record(
            command.EmployeeId,
            command.IncidentDate,
            command.Location,
            command.Description,
            command.Severity,
            command.Shift,
            positionId,
            command.Cause,
            command.CorrectiveAction,
            command.MedicalAidProvided,
            command.MedicalAidNote,
            command.ResponsibleEmployeeId);

        if (recorded.IsFailure)
        {
            return recorded.Error;
        }

        _incidents.Add(recorded.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(recorded.Value, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class UpdateIncidentCommandHandler
{
    private readonly IIncidentRepository _incidents;
    private readonly IEmployeeRepository _employees;
    private readonly IncidentPositionResolver _positions;
    private readonly IncidentMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateIncidentCommandHandler(
        IIncidentRepository incidents,
        IEmployeeRepository employees,
        IncidentPositionResolver positions,
        IncidentMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _incidents = incidents;
        _employees = employees;
        _positions = positions;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IncidentDto>> HandleAsync(
        UpdateIncidentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var incident = await _incidents.GetByIdAsync(command.IncidentId, cancellationToken).ConfigureAwait(false);
        if (incident is null)
        {
            return SafetyErrors.NotFound;
        }

        if (command.ResponsibleEmployeeId is { } responsibleId
            && !await _employees.ExistsAsync(responsibleId, cancellationToken).ConfigureAwait(false))
        {
            return SafetyErrors.ResponsibleNotFound;
        }

        // Sana tuzatilgan bo'lishi mumkin — lavozim YANGI sanaga qarab
        // qaytadan hisoblanadi (`UpdateIncidentCommand` izohi).
        var positionId = await _positions
            .ResolveAsync(incident.EmployeeId, command.IncidentDate, cancellationToken)
            .ConfigureAwait(false);

        var updated = incident.Update(
            command.IncidentDate,
            command.Location,
            command.Description,
            command.Severity,
            command.Shift,
            positionId,
            command.Cause,
            command.CorrectiveAction,
            command.MedicalAidProvided,
            command.MedicalAidNote,
            command.ResponsibleEmployeeId);

        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(incident, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ConcludeIncidentCommandHandler
{
    private readonly IIncidentRepository _incidents;
    private readonly IncidentMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ConcludeIncidentCommandHandler(
        IIncidentRepository incidents,
        IncidentMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _incidents = incidents;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IncidentDto>> HandleAsync(
        ConcludeIncidentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var incident = await _incidents.GetByIdAsync(command.IncidentId, cancellationToken).ConfigureAwait(false);
        if (incident is null)
        {
            return SafetyErrors.NotFound;
        }

        var concluded = incident.Conclude(command.Conclusion, command.ConcludedAt ?? _clock.Today);
        if (concluded.IsFailure)
        {
            return concluded.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(incident, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class DeleteIncidentCommandHandler
{
    private readonly IIncidentRepository _incidents;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteIncidentCommandHandler(IIncidentRepository incidents, IUnitOfWork unitOfWork)
    {
        _incidents = incidents;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteIncidentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var incident = await _incidents.GetByIdAsync(command.IncidentId, cancellationToken).ConfigureAwait(false);
        if (incident is null)
        {
            return SafetyErrors.NotFound;
        }

        _incidents.Remove(incident);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
