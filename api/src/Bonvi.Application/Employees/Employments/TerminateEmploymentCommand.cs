using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Employments;

/// <summary>
/// Ishdan bo'shatish.
/// </summary>
/// <param name="EmploymentId">Yakunlanayotgan mehnat munosabati.</param>
/// <param name="TerminationDate">Ishdan ketish sanasi. Bo'sh bo'lsa — bugun.</param>
/// <param name="ReasonId">Ketish sababi (ma'lumotnomadan).</param>
/// <param name="OrderNo">Buyruq raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
/// <param name="Note">Izoh.</param>
/// <param name="Confirm">
/// Belgi (qora ro'yxat / nazorat) bo'lgan xodimni bo'shatishga rozilik.
/// <c>false</c> bo'lsa amal 409 bilan to'xtaydi va ogohlantirish qaytadi.
/// </param>
public sealed record TerminateEmploymentCommand(
    Guid EmploymentId,
    DateOnly? TerminationDate = null,
    Guid? ReasonId = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null,
    string? Note = null,
    bool Confirm = false);

/// <summary>
/// Bo'shatish use-case'i.
/// </summary>
/// <remarks>
/// <para>
/// <b>Blok qoidasi — HR aynan shuni so'radi.</b> Qora ro'yxatdagi yoki
/// nazoratdagi odam «jimgina» ketib qolmasligi kerak: uning bo'yicha
/// xavfsizlik xizmatining ochiq savoli yoki moddiy javobgarligi bo'lishi
/// mumkin. Shu sababli birinchi so'rov 409 bilan qaytadi va nima uchun
/// to'xtaganini aytadi; HR ataylab <c>confirm=true</c> yuborsa — o'tadi.
/// </para>
/// <para>
/// <b>Nega taqiq emas, tasdiq.</b> Odam haqiqatda ketayotgan bo'lsa,
/// tizim uni ushlab tura olmaydi — aks holda HR yozuvni umuman
/// yangilamay qo'yadi va baza haqiqatdan uzoqlashadi.
/// </para>
/// </remarks>
public sealed class TerminateEmploymentCommandHandler
{
    private readonly IEmploymentRepository _employments;
    private readonly IProbationRepository _probations;
    private readonly IReferenceRepository<TerminationReason> _reasons;
    private readonly EmployeeFlagGuard _flagGuard;
    private readonly EmployeeMapper _mapper;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public TerminateEmploymentCommandHandler(
        IEmploymentRepository employments,
        IProbationRepository probations,
        IReferenceRepository<TerminationReason> reasons,
        EmployeeFlagGuard flagGuard,
        EmployeeMapper mapper,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _employments = employments;
        _probations = probations;
        _reasons = reasons;
        _flagGuard = flagGuard;
        _mapper = mapper;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmploymentChangeDto>> HandleAsync(
        TerminateEmploymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employment = await _employments
            .GetWithEventsAsync(command.EmploymentId, cancellationToken)
            .ConfigureAwait(false);

        if (employment is null)
        {
            return EmployeeErrors.EmploymentNotFound;
        }

        if (employment.Status == EmploymentStatus.Terminated)
        {
            return EmployeeErrors.EmploymentClosed;
        }

        if (command.ReasonId is { } reasonId
            && !await _reasons.ExistsAsync(reasonId, cancellationToken).ConfigureAwait(false))
        {
            return Error.NotFound("ketish_sababi.topilmadi", "Ishdan ketish sababi topilmadi.");
        }

        var flags = await _flagGuard.InspectAsync(employment.EmployeeId, cancellationToken).ConfigureAwait(false);

        var warnings = new List<HrWarning>();

        if (flags.Blacklisted)
        {
            warnings.Add(HrWarning.From(EmployeeErrors.Blacklisted));
        }

        if (flags.Monitored)
        {
            warnings.Add(HrWarning.From(EmployeeErrors.UnderMonitoring));
        }

        if (!flags.IsClean && !command.Confirm)
        {
            // Qaysi belgi jiddiyroq — o'shanisi kodda qaytadi, chunki
            // interfeys xato kodiga qarab matn va rang tanlaydi. Qolgan
            // sabablar `Related` da: xodim bir vaqtda ham qora ro'yxatda,
            // ham nazoratda bo'lishi mumkin va ikkalasi ham qarorga
            // ta'sir qiladi. Ularsiz HR birinchi to'siqni yopib,
            // ikkinchisiga urilib, «yana nima bor?» deb qolardi.
            return flags.Blacklisted
                ? EmployeeErrors.Blacklisted.With(
                    [.. warnings.Select(warning => warning.Code)])
                : EmployeeErrors.UnderMonitoring;
        }

        var terminationDate = command.TerminationDate ?? _clock.Today;

        var terminated = employment.Terminate(
            terminationDate,
            command.ReasonId,
            command.OrderNo,
            command.OrderDate,
            command.Note);

        if (terminated.IsFailure)
        {
            return terminated.Error;
        }

        // Ochiq sinov muddati yopiladi: odam sinovni tugatmasdan ketdi.
        // Aks holda «sinovdagilar» ro'yxatida ishdan ketgan odam turib qolardi —
        // Excel'da aynan shunday bo'lgan.
        var probation = await _probations
            .GetByEmploymentAsync(employment.Id, cancellationToken)
            .ConfigureAwait(false);

        if (probation is { Status: ProbationStatus.InProgress })
        {
            var resolved = probation.Resolve(
                ProbationStatus.LeftEarly,
                terminationDate,
                command.Note ?? "Sinov muddati tugamasdan ishdan bo'shadi");

            if (resolved.IsFailure)
            {
                return resolved.Error;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _mapper.ToDtoAsync(employment, probation, cancellationToken).ConfigureAwait(false);

        return Result.Success(new EmploymentChangeDto(dto, warnings));
    }
}
