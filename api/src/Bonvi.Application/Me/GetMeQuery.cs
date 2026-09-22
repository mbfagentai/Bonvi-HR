using Bonvi.Application.Abstractions;
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
/// Bitta so'rovda ikkita mustaqil narsa qaytadi: profil va yo'qlik turlari
/// ro'yxati. Sabab <see cref="MeDto"/> izohida — xodim tokenida ruxsat
/// da'volari yo'q va u ma'lumotnoma endpointiga bora olmaydi.
/// </remarks>
public sealed class GetMeQueryHandler
{
    private readonly ICurrentEmployee _currentEmployee;
    private readonly IEmployeeAccountRepository _accounts;
    private readonly IEmployeeAppReadModel _readModel;
    private readonly IReferenceRepository<AbsenceType> _absenceTypes;
    private readonly IClock _clock;

    public GetMeQueryHandler(
        ICurrentEmployee currentEmployee,
        IEmployeeAccountRepository accounts,
        IEmployeeAppReadModel readModel,
        IReferenceRepository<AbsenceType> absenceTypes,
        IClock clock)
    {
        _currentEmployee = currentEmployee;
        _accounts = accounts;
        _readModel = readModel;
        _absenceTypes = absenceTypes;
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
        if (account is null || !account.IsActive)
        {
            return EmployeeAccountErrors.AccountInactive;
        }

        var profile = await _readModel
            .ProfileAsync(account.EmployeeId, _clock.Today, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            // Xodim yumshoq o'chirilgan — kirish yo'li buni allaqachon
            // to'sadi (INV-1), lekin access token 15 daqiqa amal qiladi va
            // shu oynada bu holat mumkin.
            return EmployeeAccountErrors.AccountInactive;
        }

        var types = await _absenceTypes.ListAsync(onlyActive: true, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<MyAbsenceTypeDto> absenceTypes =
        [
            .. types
                .OrderBy(type => type.Name, StringComparer.CurrentCulture)
                .Select(type => new MyAbsenceTypeDto(type.Id, type.Name)),
        ];

        return new MeDto(
            account.EmployeeId,
            profile.FullName,
            profile.ShortName,
            profile.PhotoKey,
            profile.Department,
            profile.Position,
            profile.MobileCheckInAllowed,
            account.MustChangePassword,
            absenceTypes);
    }
}
