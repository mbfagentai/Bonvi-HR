using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Photos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Me;

/// <summary>Jamoa a'zosining rasmi — <c>GET /api/me/team/members/{employeeId}/photo</c>.</summary>
/// <param name="EmployeeId">A'zo.</param>
/// <param name="KnownVersion">Mijozdagi nusxaning versiyasi (<c>If-None-Match</c>).</param>
public sealed record GetTeamMemberPhotoQuery(Guid EmployeeId, string? KnownVersion = null);

/// <summary>
/// <see cref="GetTeamMemberPhotoQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// <para>
/// Qamrov — BUGUN (SPEC-jamoam §C4): panel avatarlari bugungi jamoa uchun. Faylni ochish,
/// versiya va «rasm yo'q» — HR yo'lidagi ishlovchining o'zi (<see cref="GetEmployeePhotoQueryHandler"/>),
/// <c>/api/me/photo</c> dagi kabi ikkinchi nusxasiz.
/// </para>
/// <para>
/// O'chirilgan xodimning «xodim.topilmadi» si «jamoa.xodim_topilmadi» ga aylanadi — begona
/// identifikator bilan bir xil javob. «xodim.rasm_yoq» esa qoladi: u faqat qamrovdagi odamda
/// chiqadi va boshliqqa yangi narsa aytmaydi (panelda <c>hasPhoto</c> bor).
/// </para>
/// </remarks>
public sealed class GetTeamMemberPhotoQueryHandler
{
    private readonly TeamAccess _access;
    private readonly IAttendanceComputationSource _employments;
    private readonly GetEmployeePhotoQueryHandler _photos;
    private readonly IClock _clock;

    public GetTeamMemberPhotoQueryHandler(
        TeamAccess access,
        IAttendanceComputationSource employments,
        GetEmployeePhotoQueryHandler photos,
        IClock clock)
    {
        _access = access;
        _employments = employments;
        _photos = photos;
        _clock = clock;
    }

    public async Task<Result<PhotoContent>> HandleAsync(
        GetTeamMemberPhotoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = await _access.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (caller.IsFailure)
        {
            return caller.Error;
        }

        // Bugungi bo'lim — ko'chirish hodisalaridan (`TeamScope.CoversOn`), shu sababli hodisalari bilan.
        var employments = await _employments
            .LoadEmploymentsAsync([query.EmployeeId], cancellationToken)
            .ConfigureAwait(false);

        if (!caller.Value.Scope.CoversOn(employments.GetValueOrDefault(query.EmployeeId) ?? [], _clock.Today))
        {
            return TeamErrors.MemberNotFound;
        }

        var photo = await _photos
            .HandleAsync(new GetEmployeePhotoQuery(query.EmployeeId, query.KnownVersion), cancellationToken)
            .ConfigureAwait(false);

        return photo.IsFailure && photo.Error.Code == EmployeeErrors.NotFound.Code
            ? TeamErrors.MemberNotFound
            : photo;
    }
}
