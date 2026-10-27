using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Requests;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Me;

/// <summary>Xodimning o'zi haqidagi ma'lumot.</summary>
public sealed record GetMeQuery;

/// <summary>
/// <see cref="GetMeQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// Bitta so'rovda ikkita mustaqil narsa qaytadi: profil va yo'qlik turlari
/// ro'yxati. Sabab <see cref="MeDto"/> izohida — xodim tokenida ruxsat
/// da'volari yo'q va u ma'lumotnoma endpointiga bora olmaydi.
/// </para>
/// <para>
/// «Boshliqmi» va «Rahbar» (SPEC-jamoam §C5, §D) bitta bo'limlar ro'yxatidan: ikkalasi ham
/// butun daraxtni talab qiladi, ro'yxat esa yuztacha qator.
/// </para>
/// </remarks>
public sealed class GetMeQueryHandler
{
    private readonly ICurrentEmployee _currentEmployee;
    private readonly IEmployeeAccountRepository _accounts;
    private readonly IEmployeeAppReadModel _readModel;
    private readonly IReferenceRepository<AbsenceType> _absenceTypes;
    private readonly RequestBreakRuleReader _breakRule;
    private readonly IDepartmentRepository _departments;
    private readonly IEmploymentRepository _employments;
    private readonly EmployeeManagerResolver _manager;
    private readonly IClock _clock;

    public GetMeQueryHandler(
        ICurrentEmployee currentEmployee,
        IEmployeeAccountRepository accounts,
        IEmployeeAppReadModel readModel,
        IReferenceRepository<AbsenceType> absenceTypes,
        RequestBreakRuleReader breakRule,
        IDepartmentRepository departments,
        IEmploymentRepository employments,
        EmployeeManagerResolver manager,
        IClock clock)
    {
        _currentEmployee = currentEmployee;
        _accounts = accounts;
        _readModel = readModel;
        _absenceTypes = absenceTypes;
        _breakRule = breakRule;
        _departments = departments;
        _employments = employments;
        _manager = manager;
        _clock = clock;
    }

    public async Task<Result<MeDto>> HandleAsync(
        GetMeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_currentEmployee.AccountId is not { } accountId)
        {
            return EmployeeAccountErrors.InvalidRefreshToken;
        }

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return EmployeeAccountErrors.AccountInactive;
        }

        var profile = await _readModel
            .ProfileAsync(account.EmployeeId, _clock.Today, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            // Xodim yumshoq o'chirilgan — `/api/me` to'sig'i buni allaqachon
            // ushlaydi (`EmployeeAccessGate`); bu — ikkinchi, arzon himoya.
            return EmployeeAccountErrors.AccountInactive;
        }

        var types = await _absenceTypes.ListAsync(onlyActive: true, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<MyAbsenceTypeDto> absenceTypes =
        [
            .. types
                // Faqat HR kiritadigan tur ilovada chiqmaydi (SPEC-jadval-so'rovi §A).
                .Where(type => type.EmployeeCanRequest)
                .OrderBy(type => type.Name, StringComparer.CurrentCulture)
                .Select(type => new MyAbsenceTypeDto(type.Id, type.Name, type.CountsAsWorkTime)),
        ];

        // Tanaffus qoidasi ham shu yerda — xodim tokeni HR endpointiga (`attendance.view`) bora olmaydi.
        var breakRule = await _breakRule.ReadAsync(cancellationToken).ConfigureAwait(false);

        var departments = await _departments.ListAsync(onlyActive: false, cancellationToken).ConfigureAwait(false);
        var employments = await _employments.GetByEmployeeAsync(account.EmployeeId, cancellationToken).ConfigureAwait(false);

        var manager = await _manager
            .ResolveAsync(account.EmployeeId, employments, _clock.Today, departments, cancellationToken)
            .ConfigureAwait(false);

        return new MeDto(
            account.EmployeeId,
            profile.FullName,
            profile.ShortName,
            profile.PhotoKey,
            profile.Department,
            profile.Position,
            profile.MobileCheckInAllowed,
            absenceTypes,
            RequestBreakRuleDto.FromRule(breakRule),
            TeamScope.Of(account.EmployeeId, departments).IsHead,
            manager is null ? null : new MyManagerDto(manager.FullName));
    }
}
