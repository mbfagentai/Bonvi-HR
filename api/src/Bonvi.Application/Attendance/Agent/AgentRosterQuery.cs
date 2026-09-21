using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Agent;

/// <summary>Agent so'raydigan ro'yxat — qurilmada KIM bo'lishi kerak.</summary>
/// <param name="Token">Qurilma tokeni (<c>X-Bonvi-Device-Token</c>).</param>
/// <param name="Signature">Imzo sarlavhasi — imzo rejimidagi qurilma uchun.</param>
/// <param name="SourceIp">So'rov manzili.</param>
public sealed record GetAgentRosterQuery(string? Token, string? Signature, string? SourceIp);

/// <summary>
/// Kerakli ro'yxatni tuzadi va yetishmagan shaxs raqamlarini ajratadi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>O'qish so'rovi YOZADI — ataylab.</b> Xodimga qurilma raqami aynan
/// shu yerda, u ro'yxatga birinchi marta tushgan lahzada ajratiladi. Muqobil
/// yo'l — raqamni xodim yaratilganda berish — har bir yangi xodimga qurilma
/// bilan bog'liq yozuv qo'shardi, hatto korxonada bitta ham terminal
/// bo'lmasa ham. Ajratish idempotent: raqam bir marta beriladi va keyin
/// mosliklar jadvalidan o'qiladi.
/// </para>
/// <para>
/// ⚠️ <b>Rasmi yo'q xodim ro'yxatga TUSHMAYDI.</b> Yuz terminaliga rasmsiz
/// odam yozilmaydi, agent esa uni har 60 soniyada qayta urinib, qurilmadan
/// bir xil xatoni olib turardi. HR ekranida u «Rasm yo'q» bo'lib
/// ko'rinadi — bu holatni <c>GetEmployeeEnrollmentsQuery</c> hisoblaydi.
/// </para>
/// <para>
/// Raqam ajratilsa ham, ro'yxat qatori qurilmaga hali YOZILGAN emas:
/// <c>device_enrollments</c> qatorini agentning HISOBOTI yaratadi
/// (<see cref="ReportAgentEnrollmentsCommandHandler"/>). Shu sababli bu
/// yerda holat jadvali faqat O'QILADI — «Qayta yozish» bosilganmi, ya'ni
/// <c>forceVersion</c> qancha.
/// </para>
/// </remarks>
public sealed class GetAgentRosterQueryHandler
{
    private readonly AttendanceDeviceAuthenticator _authenticator;
    private readonly IEnrollmentReadModel _roster;
    private readonly IDeviceEnrollmentRepository _enrollments;
    private readonly IAttendanceIdentityRepository _identities;
    private readonly IDevicePersonNumbers _numbers;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public GetAgentRosterQueryHandler(
        AttendanceDeviceAuthenticator authenticator,
        IEnrollmentReadModel roster,
        IDeviceEnrollmentRepository enrollments,
        IAttendanceIdentityRepository identities,
        IDevicePersonNumbers numbers,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _authenticator = authenticator;
        _roster = roster;
        _enrollments = enrollments;
        _identities = identities;
        _numbers = numbers;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AgentRosterDto>> HandleAsync(
        GetAgentRosterQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var authenticated = await _authenticator
            .AuthenticateAsync(query.Token, [], query.Signature, query.SourceIp, cancellationToken)
            .ConfigureAwait(false);

        if (authenticated.IsFailure)
        {
            return authenticated.Error;
        }

        var device = authenticated.Value;
        var now = _clock.UtcNow;

        // «Agent gapirdi» — hodisa bo'lmasa ham. Qurilmalar sahifasidagi
        // «oxirgi aloqa» aynan shu ustundan o'qiladi.
        device.NoteSeen(now, query.SourceIp);

        var candidates = await _roster.RosterAsync(now.TashkentDate(), cancellationToken).ConfigureAwait(false);

        var withPhoto = candidates.Where(candidate => candidate.PhotoKey is not null).ToList();
        var ids = withPhoto.Select(candidate => candidate.EmployeeId).ToList();

        var numbers = await _roster
            .PersonNumbersAsync(device.IdentityProvider, ids, cancellationToken)
            .ConfigureAwait(false);

        var enrollments = await _enrollments
            .ListByDeviceAsync(device.Id, cancellationToken)
            .ConfigureAwait(false);

        var forceVersions = enrollments.ToDictionary(row => row.EmployeeId, row => row.ForceVersion);
        var applied = enrollments.ToDictionary(row => row.EmployeeId, row => row.AppliedVersion);

        var entries = new List<AgentRosterEntryDto>(withPhoto.Count);

        foreach (var candidate in withPhoto)
        {
            var existing = numbers.TryGetValue(candidate.EmployeeId, out var list) ? list : null;
            var personNo = DevicePersonNo.Prefer(existing);

            if (personNo is null)
            {
                var allocated = await AllocateAsync(device, candidate, cancellationToken).ConfigureAwait(false);
                if (allocated.IsFailure)
                {
                    return allocated.Error;
                }

                personNo = allocated.Value;
            }

            var force = forceVersions.GetValueOrDefault(candidate.EmployeeId);
            var version = EnrollmentVersion.Of(candidate.PhotoKey, force);

            entries.Add(new AgentRosterEntryDto(
                candidate.EmployeeId,
                personNo,
                candidate.FullName,
                candidate.PhotoKey,
                version,
                Force: applied.GetValueOrDefault(candidate.EmployeeId) != version));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AgentRosterDto(device.Id, device.Name, now, entries);
    }

    /// <summary>
    /// Xodimga yangi shaxs raqamini beradi va uni MOSLIK sifatida yozadi.
    /// </summary>
    /// <remarks>
    /// Moslik shu zahotiyoq yaratiladi, qurilma tasdiqlashini kutmasdan:
    /// raqam ajratilgandan keyin o'tish har qanday lahzada kelishi mumkin
    /// va u bog'lanmasdan qolmasligi kerak.
    /// </remarks>
    private async Task<Result<string>> AllocateAsync(
        AttendanceDevice device,
        RosterCandidate candidate,
        CancellationToken cancellationToken)
    {
        var sequence = await _numbers.NextAsync(cancellationToken).ConfigureAwait(false);
        var personNo = DevicePersonNo.From(sequence);

        var identity = AttendanceIdentity.Link(
            device.IdentityProvider,
            personNo,
            candidate.EmployeeId,
            IdentitySource.Manual,
            candidate.FullName);

        if (identity.IsFailure)
        {
            return identity.Error;
        }

        _identities.Add(identity.Value);
        return personNo;
    }
}

/// <summary>Qurilmaga yozilgan holatning belgisi — <c>rasm:majburiyat</c>.</summary>
/// <remarks>
/// Ikkala tomon ham (ro'yxat va hisobot) shu yerdan o'qiydi: format ikki
/// joyda yozilsa, birinchi o'zgarishdayoq butun ro'yxat «kutilmoqda» bo'lib
/// qolardi.
/// </remarks>
public static class EnrollmentVersion
{
    /// <summary>Belgini tuzadi.</summary>
    public static string Of(string? photoKey, int forceVersion)
        => $"{photoKey}:{forceVersion}";
}
