using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Lists;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.VacancyRequests;

/// <summary>HR ro'yxati — «Vakansiya so'rovlari» (docs/SPEC-vakansiya-sorovi.md §2).</summary>
/// <param name="Status">Holat: Pending, Approved, Rejected, Cancelled; bo'sh — hammasi.</param>
/// <param name="Search">Lavozim, bo'lim yoki so'rovchi.</param>
/// <param name="From">Yuborilgan sana — boshi.</param>
/// <param name="To">Yuborilgan sana — oxiri (kiradi).</param>
/// <param name="Page">Sahifa.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record ListVacancyRequestsQuery(
    string? Status = null,
    string? Search = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int? Page = null,
    int? PageSize = null);

/// <summary>
/// HR so'rovni tasdiqlaydi: vakansiya OCHILADI va so'rovga bog'lanadi — bitta saqlashda.
/// </summary>
/// <remarks>
/// Maydonlar — <c>POST /api/recruiting/vacancies</c> dagi bilan bir xil: HR so'rovdan to'ldirilgan
/// formani tahrirlab yuboradi (egasi: «qoralama ochilsin», ASSUMPTIONS 2026-10-30).
/// </remarks>
/// <param name="RequestId">So'rov.</param>
/// <param name="Title">Vakansiya nomi.</param>
/// <param name="Openings">O'rinlar soni.</param>
/// <param name="OpenedAt">Ochilish sanasi; bo'sh — bugun.</param>
/// <param name="Note">Vakansiya izohi.</param>
public sealed record ApproveVacancyRequestCommand(
    Guid RequestId,
    string? Title,
    int Openings = 1,
    DateOnly? OpenedAt = null,
    string? Note = null);

/// <summary>HR so'rovni rad etadi — sabab majburiy.</summary>
/// <param name="RequestId">So'rov.</param>
/// <param name="Reason">Sabab — boshliq ilovada o'qiydi.</param>
public sealed record RejectVacancyRequestCommand(Guid RequestId, string? Reason);

/// <summary>Yangi so'rovni kiritish — «Vakansiya so'rovlari» menyusidagi forma.</summary>
/// <param name="DepartmentId">Bo'lim.</param>
/// <param name="RequestedByEmployeeId">So'ragan xodim; bo'sh — bo'lim boshlig'i.</param>
/// <param name="Title">Lavozim.</param>
/// <param name="Openings">Nechta odam.</param>
/// <param name="NeededBy">Qachondan kerak.</param>
/// <param name="Note">Sabab va talablar.</param>
public sealed record CreateVacancyRequestCommand(
    Guid DepartmentId,
    Guid? RequestedByEmployeeId,
    string? Title,
    int Openings = 1,
    DateOnly? NeededBy = null,
    string? Note = null);

/// <summary>So'rovni qaytarib olish — qaror qilinmasidan oldin.</summary>
/// <param name="RequestId">So'rov.</param>
public sealed record CancelVacancyRequestCommand(Guid RequestId);

public sealed class CreateVacancyRequestCommandHandler
{
    private readonly IVacancyRequestRepository _requests;
    private readonly IDepartmentRepository _departments;
    private readonly IEmployeeRepository _employees;
    private readonly IVacancyRequestReadModel _readModel;
    private readonly IUnitOfWork _unitOfWork;

    public CreateVacancyRequestCommandHandler(
        IVacancyRequestRepository requests,
        IDepartmentRepository departments,
        IEmployeeRepository employees,
        IVacancyRequestReadModel readModel,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _departments = departments;
        _employees = employees;
        _readModel = readModel;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VacancyRequestDto>> HandleAsync(
        CreateVacancyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var department = await _departments.GetByIdAsync(command.DepartmentId, cancellationToken).ConfigureAwait(false);
        if (department is null)
        {
            return VacancyRequestErrors.DepartmentNotFound;
        }

        // So'rovchi ko'rsatilmasa — bo'lim boshlig'i: odatda aynan u so'raydi va HR uni har
        // safar qidirib o'tirmaydi. Boshliqsiz bo'limda esa kimligini HR o'zi tanlashi shart.
        var requesterId = command.RequestedByEmployeeId ?? department.HeadEmployeeId;
        if (requesterId is not { } employeeId)
        {
            return VacancyRequestErrors.RequesterRequired;
        }

        if (!await _employees.ExistsAsync(employeeId, cancellationToken).ConfigureAwait(false))
        {
            return VacancyRequestErrors.RequesterNotFound;
        }

        var created = VacancyRequest.Create(
            department.Id,
            employeeId,
            command.Title,
            command.Openings,
            command.NeededBy,
            command.Note);

        if (created.IsFailure)
        {
            return created.Error;
        }

        _requests.Add(created.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _readModel.GetAsync(created.Value.Id, cancellationToken).ConfigureAwait(false) is { } dto
            ? dto
            : VacancyRequestErrors.NotFound;
    }
}

public sealed class CancelVacancyRequestCommandHandler
{
    private readonly IVacancyRequestRepository _requests;
    private readonly IVacancyRequestReadModel _readModel;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CancelVacancyRequestCommandHandler(
        IVacancyRequestRepository requests,
        IVacancyRequestReadModel readModel,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _readModel = readModel;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VacancyRequestDto>> HandleAsync(
        CancelVacancyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return VacancyRequestErrors.NotFound;
        }

        var cancelled = request.Cancel(_clock.UtcNow);
        if (cancelled.IsFailure)
        {
            return cancelled.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _readModel.GetAsync(request.Id, cancellationToken).ConfigureAwait(false) is { } dto
            ? dto
            : VacancyRequestErrors.NotFound;
    }
}

public sealed class ListVacancyRequestsQueryHandler
{
    /// <summary>Sukut sahifa hajmi.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// Eng katta sahifa — «Barchasi» va eksport (<c>ExcelExport.MaxRows + 1</c>) undan kichik
    /// bo'lishi shart (<c>ListExportRowLimitTests</c>).
    /// </summary>
    public const int MaxPageSize = 10_000;

    private readonly IVacancyRequestReadModel _readModel;

    public ListVacancyRequestsQueryHandler(IVacancyRequestReadModel readModel) => _readModel = readModel;

    public async Task<Result<PagedResult<VacancyRequestDto>>> HandleAsync(
        ListVacancyRequestsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var status = RecruitingQueryParsing.ParseVacancyRequestStatus(query.Status);
        if (status.IsFailure)
        {
            return status.Error;
        }

        var range = ListDateRange.Validate(query.From, query.To);
        if (range.IsFailure)
        {
            return range.Error;
        }

        var page = query.Page is { } requested && requested > 0 ? requested : 1;
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var found = await _readModel
            .SearchAsync(
                new VacancyRequestFilter(
                    status.Value,
                    string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
                    ListDateRange.StartUtc(query.From),
                    ListDateRange.EndUtcExclusive(query.To),
                    page,
                    pageSize),
                cancellationToken)
            .ConfigureAwait(false);

        var pageCount = (found.Total + pageSize - 1) / pageSize;

        return new PagedResult<VacancyRequestDto>(found.Items, page, pageSize, found.Total, pageCount);
    }
}

public sealed class ApproveVacancyRequestCommandHandler
{
    private readonly IVacancyRequestRepository _requests;
    private readonly IVacancyRepository _vacancies;
    private readonly IVacancyRequestReadModel _readModel;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveVacancyRequestCommandHandler(
        IVacancyRequestRepository requests,
        IVacancyRepository vacancies,
        IVacancyRequestReadModel readModel,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _vacancies = vacancies;
        _readModel = readModel;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VacancyRequestDto>> HandleAsync(
        ApproveVacancyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return VacancyRequestErrors.NotFound;
        }

        // Holat AVVAL tekshiriladi: qaror qilingan so'rovga vakansiya ochilib, keyin rad
        // etilsa ham, bitta saqlash bo'lgani uchun hech narsa yozilmasdi — lekin HR noto'g'ri
        // xatoni («nom band») ko'rardi.
        if (request.Status != VacancyRequestStatus.Pending)
        {
            return VacancyRequestErrors.AlreadyDecided;
        }

        // Vakansiya qoidalari `OpenVacancyCommandHandler` dagi bilan bir xil: domen, keyin nom bandligi.
        var opened = Vacancy.Open(command.Title, command.OpenedAt ?? _clock.Today, command.Openings, command.Note);
        if (opened.IsFailure)
        {
            return opened.Error;
        }

        if (await _vacancies.TitleTakenAsync(opened.Value.Title, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return RecruitingErrors.VacancyTitleTaken;
        }

        var approved = request.Approve(opened.Value.Id, _currentUser.UserId ?? Guid.Empty, _clock.UtcNow);
        if (approved.IsFailure)
        {
            return approved.Error;
        }

        _vacancies.Add(opened.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _readModel.GetAsync(request.Id, cancellationToken).ConfigureAwait(false) is { } dto
            ? dto
            : VacancyRequestErrors.NotFound;
    }
}

public sealed class RejectVacancyRequestCommandHandler
{
    private readonly IVacancyRequestRepository _requests;
    private readonly IVacancyRequestReadModel _readModel;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RejectVacancyRequestCommandHandler(
        IVacancyRequestRepository requests,
        IVacancyRequestReadModel readModel,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _readModel = readModel;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<VacancyRequestDto>> HandleAsync(
        RejectVacancyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return VacancyRequestErrors.NotFound;
        }

        var rejected = request.Reject(command.Reason, _currentUser.UserId ?? Guid.Empty, _clock.UtcNow);
        if (rejected.IsFailure)
        {
            return rejected.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _readModel.GetAsync(request.Id, cancellationToken).ConfigureAwait(false) is { } dto
            ? dto
            : VacancyRequestErrors.NotFound;
    }
}
