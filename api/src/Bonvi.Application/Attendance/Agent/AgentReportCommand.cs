using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Agent;

/// <summary>Agentning hisoboti — qurilmada nima bo'ldi.</summary>
/// <param name="Token">Qurilma tokeni.</param>
/// <param name="Body">So'rovning XOM baytlari — imzo shular ustida.</param>
/// <param name="Signature">Imzo sarlavhasi.</param>
/// <param name="SourceIp">So'rov manzili.</param>
/// <param name="Items">Qatorlar.</param>
public sealed record ReportAgentEnrollmentsCommand(
    string? Token,
    byte[] Body,
    string? Signature,
    string? SourceIp,
    IReadOnlyList<AgentReportItemDto> Items);

/// <summary>
/// Agent hisobotini holat jadvaliga yozadi (SPEC-agent §D3, §D4).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Holatni FAQAT shu buyruq o'zgartiradi.</b> Server hech qachon
/// «yozilgan bo'lsa kerak» deb taxmin qilmaydi: ro'yxat berilgani xodim
/// qurilmaga tushganini anglatmaydi — agent yiqilishi, qurilma rad etishi
/// yoki tarmoq uzilishi mumkin. Ekranda ko'rinadigan «Yozildi» faqat
/// qurilmaning O'ZI tasdiqlagan yozuv.
/// </para>
/// <para>
/// Noma'lum xodim qatori JIMGINA tashlanadi (<c>ignored</c> sanog'iga
/// tushadi): agent ro'yxatni olgandan keyin xodim o'chirilgan bo'lishi
/// mumkin va bu xato emas, poyga.
/// </para>
/// </remarks>
public sealed class ReportAgentEnrollmentsCommandHandler
{
    /// <summary>Bitta hisobotdagi eng ko'p qator.</summary>
    /// <remarks>
    /// Qurilma sig'imi 3000 kishi; bitta tekshiruvda hammasi o'zgarishi
    /// nazariy jihatdan mumkin, shu sababli chegara sig'imdan biroz
    /// yuqori olingan.
    /// </remarks>
    public const int MaxItems = 4000;

    private readonly AttendanceDeviceAuthenticator _authenticator;
    private readonly IDeviceEnrollmentRepository _enrollments;
    private readonly IEmployeeRepository _employees;
    private readonly IAttendanceIdentityRepository _identities;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ReportAgentEnrollmentsCommandHandler(
        AttendanceDeviceAuthenticator authenticator,
        IDeviceEnrollmentRepository enrollments,
        IEmployeeRepository employees,
        IAttendanceIdentityRepository identities,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _authenticator = authenticator;
        _enrollments = enrollments;
        _employees = employees;
        _identities = identities;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AgentReportResultDto>> HandleAsync(
        ReportAgentEnrollmentsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Items);

        var authenticated = await _authenticator
            .AuthenticateAsync(command.Token, command.Body, command.Signature, command.SourceIp, cancellationToken)
            .ConfigureAwait(false);

        if (authenticated.IsFailure)
        {
            return authenticated.Error;
        }

        if (command.Items.Count > MaxItems)
        {
            return AttendanceErrors.AgentReportTooLarge;
        }

        var device = authenticated.Value;
        var now = _clock.UtcNow;

        device.NoteSeen(now, command.SourceIp);

        var existing = await _enrollments.ListByDeviceAsync(device.Id, cancellationToken).ConfigureAwait(false);
        var byEmployee = existing.ToDictionary(row => row.EmployeeId);

        var accepted = 0;
        var ignored = 0;

        foreach (var item in command.Items)
        {
            var employeeId = await ResolveAsync(device, item, cancellationToken).ConfigureAwait(false);

            if (employeeId is not { } id)
            {
                ignored++;
                continue;
            }

            if (!byEmployee.TryGetValue(id, out var row))
            {
                row = DeviceEnrollment.Start(device.Id, id);
                _enrollments.Add(row);
                byEmployee[id] = row;
            }

            Apply(row, item, now);
            accepted++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AgentReportResultDto(accepted, ignored);
    }

    /// <summary>
    /// Qator qaysi xodimga tegishli.
    /// </summary>
    /// <remarks>
    /// Xodim ko'rsatilgan bo'lsa — o'sha (lekin mavjudligi tekshiriladi:
    /// ro'yxat olingandan keyin xodim o'chirilgan bo'lishi mumkin).
    /// Ko'rsatilmagan bo'lsa — shaxs raqami bo'yicha, MOSLIKLAR orqali:
    /// qurilmada boshqa hech narsa yo'q. Ikkalasi ham topilmasa
    /// <c>null</c> va qator jimgina tashlanadi.
    /// </remarks>
    private async Task<Guid?> ResolveAsync(
        AttendanceDevice device,
        AgentReportItemDto item,
        CancellationToken cancellationToken)
    {
        if (item.EmployeeId != Guid.Empty)
        {
            return await _employees.ExistsAsync(item.EmployeeId, cancellationToken).ConfigureAwait(false)
                ? item.EmployeeId
                : null;
        }

        if (string.IsNullOrWhiteSpace(item.PersonNo))
        {
            return null;
        }

        var identity = await _identities
            .FindAsync(device.IdentityProvider, item.PersonNo.Trim(), cancellationToken)
            .ConfigureAwait(false);

        return identity?.EmployeeId;
    }

    /// <summary>
    /// Bitta qatorni holatga o'giradi.
    /// </summary>
    /// <remarks>
    /// Belgisiz muvaffaqiyat «yozildi» deb sanalmaydi: agent nimani
    /// yozganini aytmagan bo'lsa, server ham nimani saqlashni bilmaydi va
    /// xodim keyingi tekshiruvda qayta yoziladi. Bu zararsiz — yozish
    /// o'rniga qo'yish emas, joyida yangilash.
    /// </remarks>
    private static void Apply(DeviceEnrollment row, AgentReportItemDto item, DateTimeOffset now)
    {
        if (!item.Ok)
        {
            row.MarkFailed(item.Error, now);
            return;
        }

        if (item.Action == AgentEnrollmentAction.Remove)
        {
            row.MarkRemoved(now);
            return;
        }

        if (string.IsNullOrWhiteSpace(item.Version))
        {
            row.MarkFailed(null, now);
            return;
        }

        row.MarkSynced(item.Version, now);
    }
}
