using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

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
            // Kiritilgan tartibda: Id — UUIDv7, ya'ni vaqt bo'yicha o'sadi.
            // Alifbo ATAYLAB emas — nomlar til kodidan mijozda hosil bo'ladi
            // va har bir interfeys tilida boshqa tartib berardi.
            [.. competency.LanguageSkills
                .OrderBy(language => language.Id)
                .Select(language => new EmployeeCompetencyLanguageDto(language.Language, language.Level))],
            competency.ComputerLiteracyPercent,
            competency.Specialization,
            competency.OtherAbilities,
            competency.Interest,
            competency.EducationPlace,
            competency.CandidateNote);
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

    /// <remarks>
    /// Faol vazifalar munosabatning YUKLANGAN hodisalaridan olinadi — o'zgarish
    /// amallari (vazifa berish, ko'chirish, bo'shatish) javobi shunday to'liq
    /// bo'ladi. Hodisalarsiz yuklangan munosabatda ro'yxat bo'sh; xodim kartasi
    /// vazifalarni alohida so'rov bilan beradi (quyidagi overload).
    /// </remarks>
    public Task<EmploymentDto> ToDtoAsync(
        Employment employment,
        Probation? probation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employment);

        var today = _clock.Today;
        return ToDtoAsync(
            employment,
            probation,
            [.. employment.Events.Where(item => item.IsActiveDuty(today))],
            cancellationToken);
    }

    /// <summary>
    /// Mehnat munosabati — faol qo'shimcha vazifalari va jami haq bilan.
    /// </summary>
    /// <param name="employment">Munosabat.</param>
    /// <param name="probation">Sinov muddati.</param>
    /// <param name="activeDuties">
    /// Shu munosabatning HOZIR davom etayotgan qo'shimcha vazifalari
    /// (<c>EmploymentEvent.IsActiveDuty</c>). Karta so'rovi ularni alohida
    /// yuklaydi — munosabat bu yerga hodisalarsiz keladi.
    /// </param>
    /// <param name="cancellationToken">Bekor qilish.</param>
    public async Task<EmploymentDto> ToDtoAsync(
        Employment employment,
        Probation? probation,
        IReadOnlyList<EmploymentEvent> activeDuties,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employment);
        ArgumentNullException.ThrowIfNull(activeDuties);

        // Ishdan ketgan munosabatda faol vazifa yo'q — eski yozuvlarda tugash
        // sanasi qo'yilmagan bo'lishi mumkin (`Employment.Terminate` endi o'zi
        // yopadi, 2026-09-27).
        List<EmploymentEvent> duties = employment.Status == EmploymentStatus.Terminated
            ? []
            : [
                .. activeDuties
                    .Where(duty => duty.EmploymentId == employment.Id)
                    .OrderBy(duty => duty.EffectiveDate),
            ];

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
            probation is null ? null : ToDto(probation),
            [
                .. duties.Select(duty => new EmploymentDutyDto(
                    duty.Id,
                    duty.Note ?? string.Empty,
                    duty.EffectiveDate,
                    _currentUser.MaskExtraPay(duty.ToSalary))),
            ],
            TotalPay(employment.Salary, duties));
    }

    /// <summary>
    /// Oklad + faol qo'shimcha haqlar — ikkala ruxsat bo'lsagina.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Faqat oklad ruxsati bilan jami ko'rsatilmaydi: jamidan okladni ayirib,
    /// yopiq qo'shimcha haqni hisoblab olish mumkin edi.
    /// </para>
    /// <para>
    /// Haqli faol vazifa bo'lmasa — <c>null</c> (jami okladning o'zi, uni
    /// takrorlash ekranni shovqinga to'ldirardi). Valyutasi okladnikidan
    /// farq qiladigan summa qo'shilmaydi — so'm va dollarni qo'shib bo'lmaydi.
    /// </para>
    /// </remarks>
    private MoneyDto? TotalPay(Money? salary, IReadOnlyList<EmploymentEvent> duties)
    {
        // Oklad kiritilmagan bo'lsa jami YO'Q: «oklad: kiritilmagan» ostida
        // faqat qo'shimcha haqdan iborat «jami» chalg'itardi (tekshiruv, 2026-09-27).
        if (salary is null || !_currentUser.CanSeeExtraPay())
        {
            return null;
        }

        var pays = duties.Select(duty => duty.ToSalary).OfType<Money>().ToList();

        // Boshqa valyutadagi haqni jimgina tashlab, jamini kam ko'rsatgandan
        // ko'ra uni umuman ko'rsatmaslik to'g'ri — qatorlar baribir ko'rinadi
        if (pays.Count == 0 || pays.Any(pay => pay.Currency != salary.Currency))
        {
            return null;
        }

        var total = Money.Create(salary.Amount + pays.Sum(pay => pay.Amount), salary.Currency);
        return total.IsSuccess ? _currentUser.Mask(total.Value) : null;
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

            // Qo'shimcha vazifadagi summa — okladdan alohida ruxsat bilan
            employmentEvent.Type == EmploymentEventType.AdditionalDuty
                ? _currentUser.MaskExtraPay(employmentEvent.ToSalary)
                : _currentUser.Mask(employmentEvent.ToSalary),
            employmentEvent.Note,
            employmentEvent.RecordedAt,
            employmentEvent.UpdatedAt,
            employmentEvent.IsImported,
            DateLocked(employmentEvent),
            employmentEvent.DutyEndDate);
    }

    /// <summary>So'rovchi shu yozuvning sanasini tuzata olmaydimi.</summary>
    /// <remarks>
    /// Pul qarori — <c>employment.salary</c>; qo'shimcha vazifa haqi esa
    /// undan tashqari <c>salary.extra.view</c> ni ham talab qiladi
    /// (<c>SalaryAccess.EnsureCanSendExtraPay</c>).
    /// </remarks>
    private bool DateLocked(EmploymentEvent employmentEvent)
    {
        if (!employmentEvent.IsPayDecision)
        {
            return false;
        }

        return employmentEvent.Type == EmploymentEventType.AdditionalDuty
            ? _currentUser.EnsureCanSendExtraPay(amountSent: true).IsFailure
            : !_currentUser.HasPermission(Permissions.EmploymentSalary);
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
