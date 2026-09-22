using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Requests;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Me;

/// <summary>Xodimning o'z yo'qlik so'rovlari.</summary>
/// <param name="Status">Faqat shu holat; berilmasa — hammasi.</param>
/// <param name="Page">Sahifa.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record GetMyRequestsQuery(
    AbsenceRequestStatus? Status = null,
    int? Page = null,
    int? PageSize = null);

/// <summary>Xodim o'ziga yo'qlik so'rovi yaratadi.</summary>
/// <param name="AbsenceTypeId">Yo'qlik turi.</param>
/// <param name="FromDate">Davr boshi.</param>
/// <param name="ToDate">Davr oxiri.</param>
/// <param name="FromTime">Kun ichidagi boshlanish — faqat bir kunlik so'rovda.</param>
/// <param name="ToTime">Kun ichidagi tugash — faqat bir kunlik so'rovda.</param>
/// <param name="Note">Izoh.</param>
public sealed record CreateMyRequestCommand(
    Guid AbsenceTypeId,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeOnly? FromTime = null,
    TimeOnly? ToTime = null,
    string? Note = null);

/// <summary>Xodim o'z so'rovini bekor qiladi.</summary>
/// <param name="RequestId">So'rov.</param>
public sealed record CancelMyRequestCommand(Guid RequestId);

/// <summary>
/// Xodim tokenidan xodim identifikatorini oladi.
/// </summary>
/// <remarks>
/// ⭐ <b>Identifikator HISOBDAN o'qiladi, tokendagi <c>emp</c> da'vosidan
/// emas.</b> Da'vo ham bor va u qulay, lekin u token berilgan lahzadagi
/// holatning NUSXASI: hisob qayta biriktirilsa yoki yopilsa, eski token
/// 15 daqiqa davomida eski javobni olib yurardi. Hisob qatori esa har doim
/// joriy haqiqat, va uning faolligi shu yerda bir joyda tekshiriladi —
/// uchala use-case ham shu tekshiruvdan o'tadi va birortasi uni
/// unutishi mumkin emas.
/// </remarks>
/// <remarks>
/// ⚠️ Sinf ATAYLAB <c>public</c>, garchi u faqat shu fayl ichida
/// ishlatilsa ham: DI konteyneri ishlovchini FAQAT ochiq konstruktor
/// orqali yasaydi, ochiq konstruktorda esa ichki (internal) tipni
/// parametr qilib bo'lmaydi (CS0051).
/// </remarks>
public sealed class CurrentEmployeeResolver
{
    private readonly ICurrentEmployee _currentEmployee;
    private readonly IEmployeeAccountRepository _accounts;

    public CurrentEmployeeResolver(ICurrentEmployee currentEmployee, IEmployeeAccountRepository accounts)
    {
        _currentEmployee = currentEmployee;
        _accounts = accounts;
    }

    public async Task<Result<Guid>> EmployeeIdAsync(CancellationToken cancellationToken)
    {
        if (_currentEmployee.AccountId is not { } accountId)
        {
            return EmployeeAccountErrors.InvalidRefreshToken;
        }

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken).ConfigureAwait(false);

        return account is null || !account.IsActive
            ? EmployeeAccountErrors.AccountInactive
            : Result.Success(account.EmployeeId);
    }
}

/// <summary>
/// <see cref="GetMyRequestsQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// HR ro'yxatining AYNAN o'sha o'qish yo'li, faqat xodim bo'yicha qattiq
/// cheklangan holda. Ikkinchi so'rov yozilsa, ikki ekran bitta so'rovni
/// boshqa-boshqa holatda ko'rsatishi mumkin bo'lardi.
/// </remarks>
public sealed class GetMyRequestsQueryHandler
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly SearchAbsenceRequestsQueryHandler _search;

    public GetMyRequestsQueryHandler(
        CurrentEmployeeResolver resolver,
        SearchAbsenceRequestsQueryHandler search)
    {
        _resolver = resolver;
        _search = search;
    }

    public async Task<Result<PagedResult<AbsenceRequestDto>>> HandleAsync(
        GetMyRequestsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employeeId = await _resolver.EmployeeIdAsync(cancellationToken).ConfigureAwait(false);
        if (employeeId.IsFailure)
        {
            return employeeId.Error;
        }

        // ⚠️ `EmployeeId` SO'ROVDAN emas, tokendan: aks holda manzildagi
        // bitta parametr butun korxonaning so'rovlarini ochib berardi.
        return await _search
            .HandleAsync(
                new SearchAbsenceRequestsQuery(
                    employeeId.Value,
                    query.Status,
                    Decided: null,
                    From: null,
                    To: null,
                    Search: null,
                    query.Page,
                    query.PageSize),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// <see cref="CreateMyRequestCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// Yozuvning O'ZI HR kiritgani bilan bir xil: bir xil domen tekshiruvlari,
/// bir xil kesishish qoidasi, bir xil «yaratish hech narsani qayta
/// hisoblatmaydi» xulqi. Bu ataylab — SPEC-so'rovlar §A8 aynan shuni
/// ko'zda tutgan: ikkinchi bosqichda o'sha buyruqni ilova chaqiradi va
/// yozuv o'zgarmaydi.
/// </remarks>
public sealed class CreateMyRequestCommandHandler
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly CreateAbsenceRequestCommandHandler _create;

    public CreateMyRequestCommandHandler(
        CurrentEmployeeResolver resolver,
        CreateAbsenceRequestCommandHandler create)
    {
        _resolver = resolver;
        _create = create;
    }

    public async Task<Result<AbsenceRequestDto>> HandleAsync(
        CreateMyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employeeId = await _resolver.EmployeeIdAsync(cancellationToken).ConfigureAwait(false);
        if (employeeId.IsFailure)
        {
            return employeeId.Error;
        }

        return await _create
            .HandleAsync(
                new CreateAbsenceRequestCommand(
                    employeeId.Value,
                    command.AbsenceTypeId,
                    command.FromDate,
                    command.ToDate,
                    command.FromTime,
                    command.ToTime,
                    command.Note),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// <see cref="CancelMyRequestCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Egalik tekshiruvi — boshqa odamning so'rovi «topilmadi».</b> 403
/// emas, 404: «bu so'rov bor, lekin sizniki emas» degan javob
/// identifikatorlarni tergan odamga tizimda qancha so'rov borligini
/// aytib berardi. Xodim uchun natija ham bir xil — u bu so'rovga hech
/// qanday yo'l bilan tega olmaydi.
/// </para>
/// <para>
/// Bekor qilishning O'ZI HR yo'lidagi ishlovchiga topshiriladi: tasdiqlangan
/// so'rov bekor qilinganda kunlarni qayta hisoblash navbatiga qo'yish
/// mantig'i o'sha yerda va uning ikkinchi nusxasi bo'lmasligi kerak.
/// </para>
/// </remarks>
public sealed class CancelMyRequestCommandHandler
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly IAbsenceRequestRepository _requests;
    private readonly CancelAbsenceRequestCommandHandler _cancel;

    public CancelMyRequestCommandHandler(
        CurrentEmployeeResolver resolver,
        IAbsenceRequestRepository requests,
        CancelAbsenceRequestCommandHandler cancel)
    {
        _resolver = resolver;
        _requests = requests;
        _cancel = cancel;
    }

    public async Task<Result> HandleAsync(
        CancelMyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var employeeId = await _resolver.EmployeeIdAsync(cancellationToken).ConfigureAwait(false);
        if (employeeId.IsFailure)
        {
            return employeeId.Error;
        }

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null || request.EmployeeId != employeeId.Value)
        {
            // Mavjud kod qayta ishlatiladi: xodim uchun «bunday so'rov
            // yo'q» va «bu so'rov sizniki emas» bir xil javob bo'lishi
            // kerak, ya'ni ikkinchi kod ikkinchi tarjima va yangi
            // ma'lumot sizishi bo'lardi.
            return AbsenceRequestErrors.NotFound;
        }

        return await _cancel
            .HandleAsync(new CancelAbsenceRequestCommand(command.RequestId), cancellationToken)
            .ConfigureAwait(false);
    }
}
