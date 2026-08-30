using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Common;

/// <summary>
/// Domen obyektlarini DTO'ga o'giradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida sinf.</b> O'girish ikki narsani biladi: oklad ruxsati
/// (<see cref="SalaryAccess"/>) va ma'lumotnoma nomlari
/// (<see cref="CatalogNameResolver"/>). Agar bu har bir handler ichida
/// takrorlansa, ulardan bittasida oklad yashirilmay qolishi mumkin —
/// aynan HR eng qo'rqadigan xato.
/// </para>
/// <para>
/// Scoped: ichidagi nom keshi bitta HTTP so'rovi doirasida yashaydi.
/// </para>
/// </remarks>
public sealed class EmployeeMapper
{
    private readonly ICurrentUser _currentUser;
    private readonly CatalogNameResolver _names;
    private readonly IClock _clock;

    public EmployeeMapper(ICurrentUser currentUser, CatalogNameResolver names, IClock clock)
    {
        _currentUser = currentUser;
        _names = names;
        _clock = clock;
    }

    public static EmployeeDto ToDto(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        return new EmployeeDto(
            employee.Id,
            employee.FullName.LastName,
            employee.FullName.FirstName,
            employee.FullName.MiddleName,
            employee.FullName.Full,
            employee.FullName.ShortForm,
            employee.BirthDate,
            employee.Gender,
            employee.Pinfl?.Value,
            employee.Passport?.Value,
            employee.Phone?.Value,
            employee.SecondPhone?.Value,
            employee.Address,
            employee.Speciality,
            employee.TelegramUrl,
            employee.PhotoKey,
            employee.HasCriminalRecord,
            employee.CriminalRecordNote,
            employee.MehnatUzUrl,
            employee.MehnatUzScreenshotKey,
            employee.DirectionIds,
            employee.NeedsReview,
            employee.ReviewNote);
    }

    public static EmployeeCompetencyDto ToDto(EmployeeCompetency competency)
    {
        ArgumentNullException.ThrowIfNull(competency);

        return new EmployeeCompetencyDto(
            competency.Id,
            competency.EmployeeId,
            competency.Languages,
            competency.ComputerLiteracyPercent,
            competency.Specialization,
            competency.OtherAbilities,
            competency.Interest,
            competency.EducationPlace);
    }

    public static EmployeeFlagDto ToDto(EmployeeFlag flag, DateOnly on)
    {
        ArgumentNullException.ThrowIfNull(flag);

        return new EmployeeFlagDto(
            flag.Id,
            flag.EmployeeId,
            flag.Type,
            flag.StartDate,
            flag.EndDate,
            flag.Reason,
            flag.IsActiveOn(on));
    }

    public ProbationDto ToDto(Probation probation)
    {
        ArgumentNullException.ThrowIfNull(probation);

        var today = _clock.Today;

        return new ProbationDto(
            probation.Id,
            probation.EmploymentId,
            probation.StartDate,
            probation.EndDate,
            probation.Status,
            probation.ResolvedAt,
            probation.Conclusion,
            probation.ReviewerEmployeeId,
            probation.DaysLeft(today),
            probation.NeedsWarning(today));
    }

    public async Task<EmploymentDto> ToDtoAsync(
        Employment employment,
        Probation? probation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employment);

        return new EmploymentDto(
            employment.Id,
            employment.EmployeeId,
            employment.LegalEntityId,
            await _names.LegalEntityAsync(employment.LegalEntityId, cancellationToken).ConfigureAwait(false),
            employment.DepartmentId,
            await _names.DepartmentAsync(employment.DepartmentId, cancellationToken).ConfigureAwait(false),
            employment.PositionId,
            await _names.PositionAsync(employment.PositionId, cancellationToken).ConfigureAwait(false),
            employment.HireDate,
            employment.HireOrderNo,
            employment.HireOrderDate,
            _currentUser.Mask(employment.Salary),
            employment.Rate,
            employment.IsPrimary,
            employment.Status,
            employment.TerminationDate,
            employment.TerminationReasonId,
            await _names
                .TerminationReasonAsync(employment.TerminationReasonId, cancellationToken)
                .ConfigureAwait(false),
            employment.TerminationOrderNo,
            employment.TerminationNote,
            employment.ResponsibleEmployeeId,
            employment.NeedsReview,
            employment.ReviewNote,
            employment.WorkedDays(_clock.Today),
            probation is null ? null : ToDto(probation));
    }

    public async Task<EmploymentEventDto> ToDtoAsync(
        EmploymentEvent employmentEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employmentEvent);

        return new EmploymentEventDto(
            employmentEvent.Id,
            employmentEvent.EmploymentId,
            employmentEvent.Type,
            employmentEvent.EffectiveDate,
            employmentEvent.OrderNo,
            employmentEvent.OrderDate,
            employmentEvent.FromDepartmentId,
            await _names.DepartmentAsync(employmentEvent.FromDepartmentId, cancellationToken).ConfigureAwait(false),
            employmentEvent.ToDepartmentId,
            await _names.DepartmentAsync(employmentEvent.ToDepartmentId, cancellationToken).ConfigureAwait(false),
            employmentEvent.FromPositionId,
            await _names.PositionAsync(employmentEvent.FromPositionId, cancellationToken).ConfigureAwait(false),
            employmentEvent.ToPositionId,
            await _names.PositionAsync(employmentEvent.ToPositionId, cancellationToken).ConfigureAwait(false),
            _currentUser.Mask(employmentEvent.FromSalary),
            _currentUser.Mask(employmentEvent.ToSalary),
            employmentEvent.Note,
            employmentEvent.RecordedAt,
            employmentEvent.IsImported);
    }

    /// <summary>
    /// Tur nomi <see cref="Incentive.Kind"/>ga qarab TO'G'RI ma'lumotnomadan
    /// olinadi: <c>TypeId</c> bitta ustun bo'lsa ham, u yoki rag'bat, yoki
    /// jarima turiga ishora qiladi — hech qachon ikkalasiga birga emas.
    /// </summary>
    public async Task<IncentiveDto> ToDtoAsync(Incentive incentive, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incentive);

        var typeName = incentive.Kind == IncentiveKind.Award
            ? await _names.AwardTypeAsync(incentive.TypeId, cancellationToken).ConfigureAwait(false)
            : await _names.PenaltyTypeAsync(incentive.TypeId, cancellationToken).ConfigureAwait(false);

        return new IncentiveDto(
            incentive.Id,
            incentive.EmployeeId,
            incentive.Kind,
            incentive.TypeId,
            typeName,
            incentive.Date,
            incentive.Amount is { } amount ? new MoneyDto(amount.Amount, amount.Currency, amount.Formatted) : null,
            incentive.GiftDescription,
            incentive.Reason,
            incentive.OrderNo,
            incentive.OrderDate);
    }

    public async Task<EmployeeDocumentDto> ToDtoAsync(
        EmployeeDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var typeName = await _names
            .DocumentTypeAsync(document.DocumentTypeId, cancellationToken)
            .ConfigureAwait(false);

        return new EmployeeDocumentDto(
            document.Id,
            document.EmployeeId,
            document.DocumentTypeId,
            typeName,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.Note,
            document.CreatedAt);
    }
}
