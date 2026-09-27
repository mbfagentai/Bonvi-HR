using Bonvi.Application.Abstractions;
using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Probations;

/// <summary>
/// Sinov muddatini yakunlaydi va — o'tgan bo'lsa — xodimni bir amalda
/// doimiy holatga o'tkazadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bitta amal, ikkita emas.</b> HR talabi so'zma-so'z: «sinovdan
/// o'tgan bo'lsa, bir bosishda xodimlarga o'tkazib, unga kerakli
/// hamma narsani — lavozim, oklad, bo'lim — qo'yib yuborsak». Ilgari bu
/// ikki qadam edi: avval sinov yopiladi, keyin xodim kartasidan
/// «lavozim o'zgarishi» yoziladi. Ikkinchi qadam unutilardi va odam
/// doimiy holatga o'tgani bilan eski, «stajyor» lavozimida qolib ketardi.
/// </para>
/// <para>
/// <b>Maydonlar ixtiyoriy va bo'sh qoldirilgani «o'zgarmadi» degani</b> —
/// <see cref="Employments.TransferEmployeeCommand"/> bilan bir xil qoida:
/// forma avvalgi qiymatlarni oldindan to'ldiradi va HR faqat o'zgargan
/// qatorga tegadi.
/// </para>
/// <para>
/// <b>Tarixda ikki hodisa qoladi</b>, bitta emas: «sinov natijasi» va
/// «lavozim o'zgarishi» (yoki «oklad o'zgarishi»). Ular boshqa-boshqa
/// faktlar va hisobotda ham alohida sanaladi; bittaga qo'shib yuborsak,
/// «sinovdan keyin okladi qancha oshdi» degan savolga javob yo'qolardi.
/// </para>
/// </remarks>
/// <param name="ProbationId">Sinov muddati identifikatori.</param>
/// <param name="Status">Natija: o'tdi / o'tmadi / erta ketdi / bekor qilindi.</param>
/// <param name="ResolvedAt">Yakunlangan sana. Bo'sh bo'lsa — bugun.</param>
/// <param name="Conclusion">Xulosa matni.</param>
/// <param name="ReviewerEmployeeId">Xulosa bergan mas'ul.</param>
/// <param name="PositionId">Yangi lavozim. Faqat <c>Passed</c> bilan.</param>
/// <param name="DepartmentId">Yangi bo'lim. Faqat <c>Passed</c> bilan.</param>
/// <param name="SalaryAmount">Yangi oklad. Faqat <c>Passed</c> bilan.</param>
/// <param name="SalaryCurrency">Oklad valyutasi. Bo'sh bo'lsa — so'm.</param>
/// <param name="Rate">Yangi stavka (1.0 — to'liq, 0.5 — yarim).</param>
/// <param name="OrderNo">Buyruq raqami.</param>
/// <param name="OrderDate">Buyruq sanasi.</param>
public sealed record ResolveProbationCommand(
    Guid ProbationId,
    ProbationStatus Status,
    DateOnly? ResolvedAt = null,
    string? Conclusion = null,
    Guid? ReviewerEmployeeId = null,
    Guid? PositionId = null,
    Guid? DepartmentId = null,
    decimal? SalaryAmount = null,
    string? SalaryCurrency = null,
    decimal? Rate = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null)
{
    /// <summary>Doimiy holatga o'tkazishda biror maydon to'ldirilganmi.</summary>
    /// <remarks>
    /// Ular faqat <see cref="ProbationStatus.Passed"/> bilan ma'noga ega:
    /// sinovdan o'tmagan odamga yangi lavozim tayinlash — kiritish xatosi,
    /// va uni jimgina bajarish o'rniga to'xtatib, HR ga aytish kerak.
    /// </remarks>
    public bool HasAppointment =>
        PositionId is not null
        || DepartmentId is not null
        || SalaryAmount is not null
        || Rate is not null;
}

/// <summary>Sinov muddatini uzaytiradi.</summary>
public sealed record ExtendProbationCommand(Guid ProbationId, DateOnly NewEndDate);

/// <summary>
/// Sinov muddatini yakunlaydi va natijani mehnat munosabatiga o'tkazadi.
/// </summary>
/// <remarks>
/// Sinovdan o'tgan xodimning munosabati <c>Probation</c> dan <c>Active</c> ga
/// o'tishi KERAK — aks holda ro'yxatda u abadiy «sinovda» bo'lib qolardi va
/// aynan shu Excel'dagi 411 ta «Тугатилган» yozuvining sababi edi.
/// </remarks>
public sealed class ResolveProbationCommandHandler
{
    private readonly IProbationRepository _probations;
    private readonly IEmploymentRepository _employments;
    private readonly IPositionRepository _positions;
    private readonly IDepartmentRepository _departments;
    private readonly EmployeeMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ResolveProbationCommandHandler(
        IProbationRepository probations,
        IEmploymentRepository employments,
        IPositionRepository positions,
        IDepartmentRepository departments,
        EmployeeMapper mapper,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _probations = probations;
        _employments = employments;
        _positions = positions;
        _departments = departments;
        _mapper = mapper;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProbationDto>> HandleAsync(
        ResolveProbationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Pul qoidasi — hamma narsadan OLDIN: sinov yakunidagi yangi oklad
        // ham `employment.salary` ni talab qiladi. Sinovni yakunlashning
        // o'zi (`employment.probation.complete`) okladga huquq bermaydi.
        var salaryAllowed = _currentUser.EnsureCanSendSalary(command.SalaryAmount is not null);
        if (salaryAllowed.IsFailure)
        {
            return salaryAllowed.Error;
        }

        // Ko'chirish qoidasi ham xuddi shunday: sinov yakunidagi yangi lavozim
        // yoki bo'lim — bu ko'chirish va u `employment.transfer` ni talab qiladi.
        // Stavka bu yerga kirmaydi: u shartnoma sharti, alohida amali yo'q
        // (xuddi ishga qabuldagi kabi).
        var moves = command.DepartmentId is not null || command.PositionId is not null;
        if (moves && !_currentUser.HasPermission(Permissions.EmploymentTransfer))
        {
            return EmployeeErrors.TransferPermissionRequired;
        }

        // Tayinlash maydonlari faqat «o'tdi» bilan ma'noga ega. Sinovdan
        // o'tmagan odamga yangi lavozim yozish — kiritish xatosi, va uni
        // jimgina bajarish HR ni butunlay boshqa natijaga olib borardi.
        if (command.HasAppointment && command.Status != ProbationStatus.Passed)
        {
            return Error.Validation(
                "sinov.tayinlash_otmagan",
                "Lavozim, bo'lim, oklad va stavkani faqat sinovdan O'TGAN xodimga tayinlash mumkin.");
        }

        // Ma'lumotnoma yozuvlari OLDIN tekshiriladi — sinov yopilib,
        // keyin lavozim topilmay qolsa, tranzaksiya bekor bo'lardi va HR
        // «nima saqlandi, nima yo'q» degan holatda qolardi.
        if (command.DepartmentId is { } departmentId
            && !await _departments.ExistsAsync(departmentId, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.DepartmentNotFound;
        }

        if (command.PositionId is { } positionId
            && !await _positions.ExistsAsync(positionId, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.PositionNotFound;
        }

        Money? salary = null;
        if (command.SalaryAmount is { } amount)
        {
            var money = Money.Create(amount, command.SalaryCurrency ?? Money.Uzs);
            if (money.IsFailure)
            {
                return money.Error;
            }

            salary = money.Value;
        }

        var probation = await _probations
            .GetByIdAsync(command.ProbationId, cancellationToken)
            .ConfigureAwait(false);

        if (probation is null)
        {
            return EmployeeErrors.ProbationNotFound;
        }

        var at = command.ResolvedAt ?? _clock.Today;

        var resolved = probation.Resolve(command.Status, at, command.Conclusion, command.ReviewerEmployeeId);
        if (resolved.IsFailure)
        {
            return resolved.Error;
        }

        if (command.Status == ProbationStatus.Passed)
        {
            var employment = await _employments
                .GetWithEventsAsync(probation.EmploymentId, cancellationToken)
                .ConfigureAwait(false);

            // Munosabat topilmasa ham sinov natijasi saqlanadi: yozuv
            // yo'qolgandan ko'ra, nomukammal bog'lanish yaxshiroq.
            if (employment is { Status: EmploymentStatus.Probation })
            {
                var completed = employment.CompleteProbation(at, command.Conclusion);
                if (completed.IsFailure)
                {
                    return completed.Error;
                }

                var appointed = Appoint(employment, command, salary, at);
                if (appointed.IsFailure)
                {
                    return appointed.Error;
                }
            }
            else if (command.HasAppointment)
            {
                // Munosabat yo'q yoki allaqachon yopilgan — tayinlashni
                // qo'yadigan joy yo'q. Jimgina tashlab yuborish eng yomon
                // variant: HR oklad qo'ygan deb o'ylab ketardi.
                return Error.Conflict(
                    "sinov.tayinlash_joyi_yoq",
                    "Bu sinov muddatiga bog'liq faol mehnat munosabati topilmadi — "
                        + "lavozim va oklad tayinlanmadi.");
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(_mapper.ToDto(probation));
    }

    /// <summary>
    /// Doimiy holatga o'tgan xodimga yangi shartlarni qo'yadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega ikki xil hodisa.</b> Lavozim yoki bo'lim o'zgarsa —
    /// <c>Transfer</c> (u okladni ham o'z ichiga oladi va eski/yangi
    /// qiymatni yonma-yon yozadi). Faqat oklad o'zgarsa — <c>SetSalary</c>.
    /// Aks holda oddiy oklad oshishi tarixda «lavozim o'zgarishi» bo'lib
    /// ko'rinardi va «kim ko'tarildi» degan hisobot yolg'on chiqardi.
    /// </para>
    /// <para>
    /// <b>Stavka hodisa yozmaydi</b> — u shartnoma sharti, hodisa emas.
    /// Uning o'zgarishi mehnat yo'li lentasida alohida qator bo'lib
    /// turishi shovqin bo'lardi.
    /// </para>
    /// </remarks>
    private static Result Appoint(
        Employment employment,
        ResolveProbationCommand command,
        Money? salary,
        DateOnly at)
    {
        if (command.Rate is { } rate)
        {
            var applied = employment.SetRate(rate);
            if (applied.IsFailure)
            {
                return applied.Error;
            }
        }

        var moved = command.DepartmentId is not null || command.PositionId is not null;

        if (moved)
        {
            return employment.Transfer(
                at,
                command.DepartmentId,
                command.PositionId,
                salary,
                command.OrderNo,
                command.OrderDate,
                command.Conclusion);
        }

        if (salary is not null)
        {
            return employment.SetSalary(salary, at, command.OrderNo, command.OrderDate);
        }

        return Result.Success();
    }
}

/// <summary>Sinov muddatini uzaytiradi (umumiy chegara — 180 kun).</summary>
public sealed class ExtendProbationCommandHandler
{
    private readonly IProbationRepository _probations;
    private readonly EmployeeMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public ExtendProbationCommandHandler(
        IProbationRepository probations,
        EmployeeMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _probations = probations;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProbationDto>> HandleAsync(
        ExtendProbationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var probation = await _probations
            .GetByIdAsync(command.ProbationId, cancellationToken)
            .ConfigureAwait(false);

        if (probation is null)
        {
            return EmployeeErrors.ProbationNotFound;
        }

        var extended = probation.Extend(command.NewEndDate);
        if (extended.IsFailure)
        {
            return extended.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(_mapper.ToDto(probation));
    }
}
