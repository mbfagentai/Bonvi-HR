using System.Security.Cryptography;
using System.Text;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
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
/// <param name="TerminalSerial">
/// Agent terminaldan o'qigan seriya (<c>X-Bonvi-Terminal-Serial</c>) — tokenni
/// jismoniy terminalga bog'laydi (<see cref="AttendanceDevice.ConfirmTerminal"/>).
/// </param>
public sealed record GetAgentRosterQuery(string? Token, string? Signature, string? SourceIp, string? TerminalSerial);

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
/// mosliklar jadvalidan o'qiladi. Ikki so'rov bir vaqtda ajratmoqchi
/// bo'lsa, bazadagi indeks ikkinchisini to'xtatadi
/// (<see cref="AttendanceErrors.AgentRosterBusy"/>).
/// </para>
/// <para>
/// ⚠️ <b>Rasmi yo'q xodim ro'yxatga TUSHMAYDI — agar u terminalda hali
/// yo'q bo'lsa.</b> Yuz terminaliga rasmsiz odam yozilmaydi, agent esa uni
/// har 60 soniyada qayta urinib, qurilmadan bir xil xatoni olib turardi.
/// HR ekranida u «Rasm yo'q» bo'lib ko'rinadi — bu holatni
/// <c>GetEmployeeEnrollmentsQuery</c> hisoblaydi. Terminalga ALLAQACHON
/// yozilgan odam esa rasmi o'chirilsa ham ro'yxatda QOLADI (o'sha belgi
/// bilan, qayta yozishsiz): aks holda rasmni almashtirish uchun o'chirish
/// odamni hamma terminaldan olib tashlab, eshikni yopib qo'yardi. Terminaldan
/// faqat ishdan ketganda chiqadi.
/// </para>
/// <para>
/// Raqam ajratilsa ham, ro'yxat qatori qurilmaga hali YOZILGAN emas:
/// <c>device_enrollments</c> qatorini agentning HISOBOTI yaratadi
/// (<see cref="ReportAgentEnrollmentsCommandHandler"/>). Shu sababli bu
/// yerda holat jadvali faqat O'QILADI — «Qayta yozish» bosilganmi, ya'ni
/// <c>forceVersion</c> qancha, va qaysi belgi rad etilgan.
/// </para>
/// </remarks>
public sealed class GetAgentRosterQueryHandler
{
    private readonly AttendanceDeviceAuthenticator _authenticator;
    private readonly IEnrollmentReadModel _roster;
    private readonly IDeviceEnrollmentRepository _enrollments;
    private readonly IAttendanceIdentityRepository _identities;
    private readonly IDevicePersonNumbers _numbers;
    private readonly IAttendanceIngestJournal _journal;
    private readonly IDatabaseFault _fault;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public GetAgentRosterQueryHandler(
        AttendanceDeviceAuthenticator authenticator,
        IEnrollmentReadModel roster,
        IDeviceEnrollmentRepository enrollments,
        IAttendanceIdentityRepository identities,
        IDevicePersonNumbers numbers,
        IAttendanceIngestJournal journal,
        IDatabaseFault fault,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _authenticator = authenticator;
        _roster = roster;
        _enrollments = enrollments;
        _identities = identities;
        _numbers = numbers;
        _journal = journal;
        _fault = fault;
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

        /*
          Terminal bog'lanishi — HAMMA narsadan oldin. Boshqa terminalning
          agenti bu ro'yxatni olsa, u yerdagi Bonvi odamlarini «ro'yxatda
          yo'q» deb o'chirib tashlardi. Rad etilgan so'rov hech narsa
          yozmaydi, hatto «oxirgi aloqa» ni ham.
        */
        var bound = device.ConfirmTerminal(query.TerminalSerial, required: true);
        if (bound.IsFailure)
        {
            _journal.TerminalMismatch(device.Id, query.TerminalSerial);
            return bound.Error;
        }

        // «Agent gapirdi» — hodisa bo'lmasa ham. Terminal bilan aloqa esa
        // bu yerda YOZILMAYDI: agent terminalga yeta olmasa ham ro'yxat
        // so'rayveradi (`AttendanceDevice.TerminalSeenAt` izohi).
        device.NoteAgent(now, query.SourceIp);

        var candidates = await _roster.RosterAsync(now.TashkentDate(), cancellationToken).ConfigureAwait(false);

        var enrollments = await _enrollments
            .ListByDeviceAsync(device.Id, cancellationToken)
            .ConfigureAwait(false);

        var byEmployee = enrollments.ToDictionary(row => row.EmployeeId);

        // Rasmi bor — yoki terminalda allaqachon bor (yuqoridagi izoh).
        var members = candidates
            .Where(candidate => candidate.PhotoKey is not null
                || byEmployee.GetValueOrDefault(candidate.EmployeeId)?.AppliedVersion is not null)
            .ToList();

        var numbers = await _roster
            .PersonNumbersAsync(device.IdentityProvider, [.. members.Select(candidate => candidate.EmployeeId)], cancellationToken)
            .ConfigureAwait(false);

        var entries = new List<AgentRosterEntryDto>(members.Count);

        foreach (var candidate in members)
        {
            var existing = numbers.TryGetValue(candidate.EmployeeId, out var list) ? list : null;
            var personNo = DevicePersonNo.Prefer(existing);
            var row = byEmployee.GetValueOrDefault(candidate.EmployeeId);

            if (candidate.PhotoKey is null)
            {
                // Rasmi o'chirilgan, lekin terminalda turibdi: o'sha belgi
                // bilan qoladi — qayta yozilmaydi (yozish uchun rasm yo'q).
                // Raqami topilmasa (mosligi qo'lda uzilgan) — qatorni
                // chiqarib bo'lmaydi.
                if (personNo is null || row?.AppliedVersion is not { } written)
                {
                    continue;
                }

                entries.Add(new AgentRosterEntryDto(
                    candidate.EmployeeId,
                    personNo,
                    candidate.FullName,
                    PhotoVersion: null,
                    written,
                    Force: false,
                    Hold: string.Equals(row.RejectedVersion, written, StringComparison.Ordinal)));

                continue;
            }

            if (personNo is null)
            {
                var allocated = await AllocateAsync(device, candidate, cancellationToken).ConfigureAwait(false);
                if (allocated.IsFailure)
                {
                    return allocated.Error;
                }

                personNo = allocated.Value;
            }

            var version = EnrollmentVersion.Of(candidate.PhotoKey, row?.ForceVersion ?? 0, candidate.FullName);
            var hold = string.Equals(row?.RejectedVersion, version, StringComparison.Ordinal);

            entries.Add(new AgentRosterEntryDto(
                candidate.EmployeeId,
                personNo,
                candidate.FullName,
                candidate.PhotoKey,
                version,
                Force: !hold && !string.Equals(row?.AppliedVersion, version, StringComparison.Ordinal),
                Hold: hold));
        }

        /*
          O'chiriladiganlar — FAQAT shu server ajratgan raqamlar. Bonvi
          fazasidagi boshqa raqam (masalan sinov serveri yozgan odam) begona:
          agent uni o'chirmaydi va HR ga «begona» deb aytadi. Ilgari agent
          fazadagi har qanday ortiqchani o'chirardi va bitta terminalga ulangan
          ikki server bir-birining odamlarini har daqiqada o'chirib turardi.
        */
        var listed = entries.Select(entry => entry.PersonNo).ToHashSet(StringComparer.Ordinal);

        var managed = await _roster
            .ManagedNumbersAsync(device.IdentityProvider, cancellationToken)
            .ConfigureAwait(false);

        var remove = managed.Where(number => !listed.Contains(number)).Distinct(StringComparer.Ordinal).ToList();

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_fault.IsDuplicateDevicePersonNo(exception))
        {
            return AttendanceErrors.AgentRosterBusy;
        }

        return new AgentRosterDto(device.Id, device.Name, now, entries, remove, device.RemovalsApprovedAt(now));
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

/// <summary>Qurilmaga yozilgan holatning belgisi — <c>rasm:majburiyat:ism</c>.</summary>
/// <remarks>
/// <para>
/// Ikkala tomon ham (ro'yxat va hisobot) shu yerdan o'qiydi: format ikki
/// joyda yozilsa, birinchi o'zgarishdayoq butun ro'yxat «kutilmoqda» bo'lib
/// qolardi.
/// </para>
/// <para>
/// ⭐ <b>Ism ham belgining qismi</b> (SPEC-agent §E, 2026-09-27): ilgari
/// belgi faqat rasm va «Qayta yozish» sanog'idan iborat edi va familiya
/// o'zgarganda terminaldagi ism eskicha qolardi. Ism belgiga to'liq emas,
/// qisqa XESH bo'lib kiradi — ustun 200 belgi, ism esa uzun bo'lishi mumkin.
/// </para>
/// <para>
/// ⚠️ Format o'zgargan kuni (2026-09-27) ilgari yozilgan HAR BIR odam bir
/// marta «eskirgan» bo'lib ko'rinadi va agent uni o'sha raqam ostida bir marta
/// qayta yozadi (joyida, o'chirmasdan). Bu zararsiz va bir martalik.
/// </para>
/// </remarks>
public static class EnrollmentVersion
{
    /// <summary>Belgini tuzadi.</summary>
    public static string Of(string? photoKey, int forceVersion, string? fullName)
        => $"{photoKey}:{forceVersion}:{NameMark(fullName)}";

    /// <summary>Ismning 8 belgilik izi — bo'sh joylari tozalangan holida.</summary>
    private static string NameMark(string? fullName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes((fullName ?? string.Empty).Trim()));
        return Convert.ToHexStringLower(bytes)[..8];
    }
}
