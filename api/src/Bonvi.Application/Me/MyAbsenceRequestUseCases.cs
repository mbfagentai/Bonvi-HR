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

/// <summary>
/// Xodim o'zining KUTAYOTGAN so'rovini o'zgartiradi (SPEC-verifikatsiya-sorov-tahriri §3).
/// </summary>
/// <param name="RequestId">So'rov.</param>
/// <param name="Version">Ilova ko'rsatgan <c>version</c> — majburiy (§3.3, 2-qatlam).</param>
/// <param name="AbsenceTypeId">Yo'qlik turi.</param>
/// <param name="FromDate">Davr boshi.</param>
/// <param name="ToDate">Davr oxiri.</param>
/// <param name="FromTime">Kun ichidagi boshlanish — faqat bir kunlik so'rovda.</param>
/// <param name="ToTime">Kun ichidagi tugash — faqat bir kunlik so'rovda.</param>
/// <param name="Note">Izoh.</param>
public sealed record UpdateMyRequestCommand(
    Guid RequestId,
    int Version,
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
/// holatning NUSXASI. Hisob qatori esa har doim joriy haqiqat. «Xodim hali
/// ishlaydimi» degan savol bu yerda EMAS — u butun <c>/api/me/*</c> guruhi
/// uchun bitta to'siqda (<c>EmployeeAccessGate</c>).
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
        var ids = await ResolveAsync(cancellationToken).ConfigureAwait(false);

        return ids.IsFailure ? ids.Error : Result.Success(ids.Value.EmployeeId);
    }

    /// <summary>Hisob VA xodim — yozuvda «kim qildi» kerak bo'lganda.</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<Result<CurrentEmployeeIds>> ResolveAsync(CancellationToken cancellationToken)
    {
        if (_currentEmployee.AccountId is not { } accountId)
        {
            return EmployeeAccountErrors.InvalidRefreshToken;
        }

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken).ConfigureAwait(false);

        return account is null
            ? EmployeeAccountErrors.AccountInactive
            : new CurrentEmployeeIds(account.Id, account.EmployeeId);
    }
}

/// <summary>So'rov yuborayotgan xodim — ilova hisobi va xodim.</summary>
/// <param name="AccountId">Ilova hisobi (tokendagi <c>sub</c>).</param>
/// <param name="EmployeeId">Hisob bog'langan xodim — HISOB QATORIDAN.</param>
public sealed record CurrentEmployeeIds(Guid AccountId, Guid EmployeeId);

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
/// <para>
/// Yozuvning O'ZI HR kiritgani bilan bir xil: bir xil domen tekshiruvlari,
/// bir xil kesishish qoidasi, bir xil «yaratish hech narsani qayta
/// hisoblatmaydi» xulqi. Bu ataylab — SPEC-so'rovlar §A8 aynan shuni
/// ko'zda tutgan: ikkinchi bosqichda o'sha buyruqni ilova chaqiradi va
/// yozuv o'zgarmaydi.
/// </para>
/// <para>
/// Farqi bitta: kim yuborgani yoziladi (<c>SubmittedByAccountId</c> — SPEC
/// §B2 «CreatedBy: … or the employee themselves»). Ilgari ilovadan kelgan
/// so'rovning muallifi bo'sh qolardi, chunki audit uni HR foydalanuvchisidan
/// oladi.
/// </para>
/// </remarks>
public sealed class CreateMyRequestCommandHandler
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly CreateAbsenceRequestCommandHandler _create;
    private readonly IAbsenceTypeLookup _types;

    public CreateMyRequestCommandHandler(
        CurrentEmployeeResolver resolver,
        CreateAbsenceRequestCommandHandler create,
        IAbsenceTypeLookup types)
    {
        _resolver = resolver;
        _create = create;
        _types = types;
    }

    public async Task<Result<AbsenceRequestDto>> HandleAsync(
        CreateMyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ids = await _resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (ids.IsFailure)
        {
            return ids.Error;
        }

        // «Faqat HR» turi: ilova uni ko'rsatmaydi, eski ilova yoki qo'lda yuborilgan
        // so'rov ham o'tmasligi kerak. Yo'q yoki faolsiz turni HR ishlovchisi o'zi rad etadi.
        if (await _types.FindAsync(command.AbsenceTypeId, cancellationToken).ConfigureAwait(false)
            is { EmployeeCanRequest: false })
        {
            return AbsenceRequestErrors.TypeClosedForEmployees;
        }

        return await _create
            .HandleAsync(
                new CreateAbsenceRequestCommand(
                    ids.Value.EmployeeId,
                    command.AbsenceTypeId,
                    command.FromDate,
                    command.ToDate,
                    command.FromTime,
                    command.ToTime,
                    command.Note,
                    SubmittedByAccountId: ids.Value.AccountId),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// <see cref="UpdateMyRequestCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Yaratishdagi qoidalarning O'ZI</b> (SPEC-sorov-tahriri §3.1): xodim tekshiruvi shu yerda
/// (egalik, holat, versiya, «faqat HR» turi), qolgani — HR tahrir ishlovchisida (tur bor/faolligi,
/// kesishish so'rovning o'zisiz, domen davr qoidalari, izoh uzunligi). Ikki nusxa yozilsa, ilova va
/// HR bir so'rovni ikki xil qoidada tahrirlardi.
/// </para>
/// <para>
/// ⭐ <b>Egalik — «topilmadi»</b>, bekor qilishdagi kabi: begona so'rov 404, 403 emas. HR xodim nomidan
/// kiritgan so'rov ham xodimniki — uni ham o'zgartira oladi (§12 Q8).
/// </para>
/// <para>
/// Tartib (§3.3): 404 → 409 holat → 409 versiya → 400 «faqat HR» turi → HR ishlovchisi. Holat versiyadan
/// oldin: HR qaror qilgan so'rovni ochgan ilova «qaror qabul qilingan» deb eshitishi kerak, «o'zgargan» emas.
/// Tahrir hech narsani qayta hisoblatmaydi — kutayotgan so'rov kunlarga ta'sir qilmaydi.
/// </para>
/// </remarks>
public sealed class UpdateMyRequestCommandHandler
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly IAbsenceRequestRepository _requests;
    private readonly IAbsenceTypeLookup _types;
    private readonly UpdateAbsenceRequestCommandHandler _update;

    public UpdateMyRequestCommandHandler(
        CurrentEmployeeResolver resolver,
        IAbsenceRequestRepository requests,
        IAbsenceTypeLookup types,
        UpdateAbsenceRequestCommandHandler update)
    {
        _resolver = resolver;
        _requests = requests;
        _types = types;
        _update = update;
    }

    public async Task<Result<AbsenceRequestDto>> HandleAsync(
        UpdateMyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ids = await _resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (ids.IsFailure)
        {
            return ids.Error;
        }

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null || request.EmployeeId != ids.Value.EmployeeId)
        {
            return AbsenceRequestErrors.NotFound;
        }

        if (AbsenceRequestErrors.PendingAt(request.Status, request.Version, command.Version) is { } notEditable)
        {
            return notEditable;
        }

        // «Faqat HR» turi: yaratishdagi to'siqning o'zi — ilova bunday turdagi so'rovda tugmani ko'rsatmaydi.
        if (await _types.FindAsync(command.AbsenceTypeId, cancellationToken).ConfigureAwait(false)
            is { EmployeeCanRequest: false })
        {
            return AbsenceRequestErrors.TypeClosedForEmployees;
        }

        return await _update
            .HandleAsync(
                new UpdateAbsenceRequestCommand(
                    request.Id,
                    command.AbsenceTypeId,
                    command.FromDate,
                    command.ToDate,
                    command.FromTime,
                    command.ToTime,
                    command.Note,
                    ExpectedVersion: command.Version,
                    EditedByAccountId: ids.Value.AccountId),
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
/// ⭐ <b>Faqat qaror KUTAYOTGAN so'rov</b>
/// (<see cref="AbsenceRequest.CancelBySubmitter"/>): HR tasdiqlagan yoki rad
/// etgani — <c>davomat.sorov_qaror_qabul_qilingan</c> (409). Ilgari bu yo'l
/// HR ishlovchisiga topshirilardi va u tasdiqlangan so'rovni ham bekor
/// qilib, HR ning qaror yozuvini <c>Guid.Empty</c> bilan ustidan yozardi —
/// ya'ni xodim o'zining rad etilgan so'rovini «bekor qilingan» ga,
/// tasdiqlanganini esa tabeldan jimgina olib tashlay olardi.
/// </para>
/// <para>
/// Qayta hisoblash YO'Q va kerak ham emas: kutayotgan so'rov kunlarga hech
/// qachon ta'sir qilmagan (SPEC-so'rovlar §B3), ya'ni uni bekor qilish ham
/// ta'sir qilmaydi.
/// </para>
/// </remarks>
public sealed class CancelMyRequestCommandHandler
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly IAbsenceRequestRepository _requests;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IDatabaseFault _fault;

    public CancelMyRequestCommandHandler(
        CurrentEmployeeResolver resolver,
        IAbsenceRequestRepository requests,
        IUnitOfWork unitOfWork,
        IClock clock,
        IDatabaseFault fault)
    {
        _resolver = resolver;
        _requests = requests;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _fault = fault;
    }

    public async Task<Result> HandleAsync(
        CancelMyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ids = await _resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (ids.IsFailure)
        {
            return ids.Error;
        }

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null || request.EmployeeId != ids.Value.EmployeeId)
        {
            // Mavjud kod qayta ishlatiladi: xodim uchun «bunday so'rov
            // yo'q» va «bu so'rov sizniki emas» bir xil javob bo'lishi
            // kerak, ya'ni ikkinchi kod ikkinchi tarjima va yangi
            // ma'lumot sizishi bo'lardi.
            return AbsenceRequestErrors.NotFound;
        }

        var cancelled = request.CancelBySubmitter(ids.Value.AccountId, _clock.UtcNow);
        if (cancelled.IsFailure)
        {
            return cancelled.Error;
        }

        // HR shu lahzada qaror qilgan bo'lsa — 409, 500 emas (SPEC-sorov-tahriri §3.3, 1-qatlam).
        if (await UpdateAbsenceRequestCommandHandler.SaveAsync(_unitOfWork, _fault, cancellationToken).ConfigureAwait(false)
            is { } stale)
        {
            return stale;
        }

        return Result.Success();
    }
}
