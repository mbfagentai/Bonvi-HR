using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Me;
using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Common;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Recruiting.VacancyRequests;

/*
  ⭐ Bo'lim boshlig'i so'rovni O'ZI yuboradi (egasi, 2026-10-31): xodim ilovasidagi «Jamoa» tabidan
  va bo'lim kaliti bilan rahbar botidan (`/api/public/v1/team/vacancy-requests`). Ikkala yuza bir xil
  qoida — shu sinf; farqi faqat qamrov qayerdan kelishi (boshliqlik yoki kalit) va so'rovchi kimligi.
  So'rov HR menyusiga «Yangi» holatida tushadi, HR qarori o'zgarmagan (SPEC-vakansiya-sorovi §2).
*/

/// <summary>Boshliq yoki bot yuboradigan so'rov.</summary>
/// <param name="DepartmentId">Qamrovdagi bo'lim; bo'sh — qamrovning yagona ildizi.</param>
/// <param name="Title">Lavozim.</param>
/// <param name="Openings">Nechta odam.</param>
/// <param name="NeededBy">Qachondan kerak.</param>
/// <param name="Note">Sabab va talablar.</param>
public sealed record SubmitTeamVacancyRequestCommand(
    Guid? DepartmentId,
    string? Title,
    int Openings = 1,
    DateOnly? NeededBy = null,
    string? Note = null);

/// <summary>Qamrovdagi so'rovlar ro'yxati.</summary>
/// <param name="Status">Holat: Pending, Approved, Rejected, Cancelled; bo'sh — hammasi.</param>
/// <param name="Page">Sahifa.</param>
/// <param name="PageSize">Sahifa hajmi (sukut 50, eng ko'pi 200).</param>
public sealed record ListTeamVacancyRequestsQuery(string? Status = null, int? Page = null, int? PageSize = null);

/// <summary>Qamrov ichidagi so'rovlar — «Jamoa» tabi va bo'lim kaliti uchun umumiy mantiq.</summary>
/// <remarks>
/// Qamrovdan tashqaridagi so'rov yoki bo'lim — «yo'q» bilan BIR XIL javob (<c>TeamErrors</c> qoidasi):
/// identifikatorlarni sinab, boshqa bo'limlarning so'rovlarini bilib bo'lmasin.
/// </remarks>
public sealed class TeamVacancyRequests
{
    /// <summary>Sukut sahifa hajmi.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>Eng katta sahifa.</summary>
    public const int MaxPageSize = 200;

    private readonly IVacancyRequestRepository _requests;
    private readonly IVacancyRequestReadModel _readModel;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public TeamVacancyRequests(
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

    public async Task<Result<PagedResult<VacancyRequestDto>>> ListAsync(
        TeamScope scope,
        ListTeamVacancyRequestsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);

        var status = RecruitingQueryParsing.ParseVacancyRequestStatus(query.Status);
        if (status.IsFailure)
        {
            return status.Error;
        }

        var page = query.Page is { } requested && requested > 0 ? requested : 1;
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var found = await _readModel
            .SearchAsync(
                new VacancyRequestFilter(
                    status.Value,
                    Search: null,
                    CreatedFrom: null,
                    CreatedBefore: null,
                    page,
                    pageSize,
                    [.. scope.Departments.Select(department => department.Id)]),
                cancellationToken)
            .ConfigureAwait(false);

        var pageCount = (found.Total + pageSize - 1) / pageSize;

        return new PagedResult<VacancyRequestDto>(found.Items, page, pageSize, found.Total, pageCount);
    }

    /// <summary>Yangi so'rov — «Kutilmoqda» holatida.</summary>
    /// <param name="scope">So'rovchining qamrovi.</param>
    /// <param name="requesterId">So'ragan xodim; bo'sh — bo'limning (yoki eng yaqin ustki bo'limning) boshlig'i.</param>
    /// <param name="command">Maydonlar.</param>
    /// <param name="departmentNotFound">Yuzaning «bo'lim yo'q» xatosi.</param>
    /// <param name="headMissing">So'rovchini aniqlab bo'lmasa — yuzaning xatosi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<Result<VacancyRequestDto>> SubmitAsync(
        TeamScope scope,
        Guid? requesterId,
        SubmitTeamVacancyRequestCommand command,
        Error departmentNotFound,
        Error headMissing,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(command);

        // Bitta bo'limli boshliq (odatiy holat) bo'limni tanlab o'tirmaydi.
        var departmentId = command.DepartmentId ?? (scope.Roots.Count == 1 ? scope.Roots[0] : null);
        if (departmentId is not { } id)
        {
            return VacancyRequestErrors.DepartmentRequired;
        }

        if (!scope.Contains(id))
        {
            return departmentNotFound;
        }

        if ((requesterId ?? HeadOf(scope, id)) is not { } employeeId)
        {
            return headMissing;
        }

        var created = VacancyRequest.Create(id, employeeId, command.Title, command.Openings, command.NeededBy, command.Note);
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

    /// <summary>Qaror qilinmagan so'rovni qaytarib olish.</summary>
    public async Task<Result<VacancyRequestDto>> CancelAsync(
        TeamScope scope,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var request = await _requests.GetByIdAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (request is null || !scope.Contains(request.DepartmentId))
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

    /// <summary>Bo'limning boshlig'i, bo'lmasa qamrov ichida eng yaqin ustki bo'limning boshlig'i.</summary>
    private static Guid? HeadOf(TeamScope scope, Guid departmentId)
    {
        var byId = scope.Departments.ToDictionary(department => department.Id);
        var seen = new HashSet<Guid>();
        Guid? current = departmentId;

        while (current is { } id && byId.TryGetValue(id, out var department) && seen.Add(id))
        {
            if (department.HeadEmployeeId is { } head)
            {
                return head;
            }

            current = department.ParentId;
        }

        return null;
    }
}
