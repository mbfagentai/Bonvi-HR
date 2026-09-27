using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Agent;

/// <summary>Agentning bitta solishtirish aylanishi natijasi.</summary>
/// <param name="Token">Qurilma tokeni.</param>
/// <param name="Body">So'rovning xom baytlari — imzo shular ustida.</param>
/// <param name="Signature">Imzo sarlavhasi.</param>
/// <param name="SourceIp">So'rov manzili.</param>
/// <param name="HeldRemovals">Ommaviy o'chirish to'sig'i ushlab qolgan o'chirishlar.</param>
/// <param name="ForeignPersons">Terminaldagi begona Bonvi-raqamli odamlar.</param>
/// <param name="TerminalSerial">Agent o'qigan terminal seriyasi.</param>
public sealed record ReportAgentStatusCommand(
    string? Token,
    byte[] Body,
    string? Signature,
    string? SourceIp,
    int HeldRemovals,
    int ForeignPersons,
    string? TerminalSerial);

/// <summary>
/// Agent solishtirish natijasini qurilma kartasiga yozadi (SPEC-agent §L).
/// </summary>
/// <remarks>
/// <para>
/// Hisobotdan (<see cref="ReportAgentEnrollmentsCommandHandler"/>) ALOHIDA:
/// u xodim bo'yicha qatorlar va bo'laklab keladi, bu esa aylanishga bitta
/// va qurilma haqida. Ikki son — HR hal qilishi kerak bo'lgan ikki holat:
/// </para>
/// <list type="bullet">
///   <item>agent ATAYLAB bajarmagan o'chirishlar (ro'yxat bo'sh yoki
///         terminaldagi Bonvi odamlarining beshdan biridan ko'pi o'chishi
///         kerak edi) — HR tekshirib tasdiqlaydi;</item>
///   <item>terminaldagi begona Bonvi-raqamli odamlar — shu terminalga
///         boshqa Bonvi o'rnatmasi ham yozyapti.</item>
/// </list>
/// </remarks>
public sealed class ReportAgentStatusCommandHandler
{
    private readonly AttendanceDeviceAuthenticator _authenticator;
    private readonly IAttendanceIngestJournal _journal;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ReportAgentStatusCommandHandler(
        AttendanceDeviceAuthenticator authenticator,
        IAttendanceIngestJournal journal,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _authenticator = authenticator;
        _journal = journal;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        ReportAgentStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var authenticated = await _authenticator
            .AuthenticateAsync(command.Token, command.Body, command.Signature, command.SourceIp, cancellationToken)
            .ConfigureAwait(false);

        if (authenticated.IsFailure)
        {
            return authenticated.Error;
        }

        var device = authenticated.Value;

        var bound = device.ConfirmTerminal(command.TerminalSerial, required: true);
        if (bound.IsFailure)
        {
            _journal.TerminalMismatch(device.Id, command.TerminalSerial);
            return bound.Error;
        }

        device.NoteReconcile(_clock.UtcNow, command.SourceIp, command.HeldRemovals, command.ForeignPersons);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>HR ushlab qolingan o'chirishlarni tasdiqlaydi.</summary>
/// <param name="DeviceId">Qurilma.</param>
public sealed record ApproveDeviceRemovalsCommand(Guid DeviceId);

/// <summary>
/// Ommaviy o'chirish to'sig'ini BIR MARTA ochadi — qisqa muddatga.
/// </summary>
/// <remarks>
/// <para>
/// Agent to'siqni qo'llaganda (masalan, filial yopilib 150 kishi bir kunda
/// ishdan ketgan) o'chirishlar shu tugmasiz hech qachon bajarilmasdi. Tugma
/// ruxsatni <see cref="ApprovalWindow"/> ga beradi: agent har daqiqada
/// solishtiradi, ya'ni ruxsat keyingi bir-ikki aylanishda ishlatiladi va
/// o'zi tugaydi.
/// </para>
/// <para>
/// ⚠️ Doimiy «to'siqni o'chirish» sozlamasi ATAYLAB yo'q: u keyingi safar
/// xato ro'yxat kelganda (bo'sh baza, tiklangan zaxira) yana hammani
/// o'chirib yuborardi.
/// </para>
/// </remarks>
public sealed class ApproveDeviceRemovalsCommandHandler
{
    /// <summary>Ruxsat amal qiladigan oraliq.</summary>
    public static readonly TimeSpan ApprovalWindow = TimeSpan.FromMinutes(10);

    private readonly IAttendanceDeviceRepository _devices;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveDeviceRemovalsCommandHandler(
        IAttendanceDeviceRepository devices,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        ApproveDeviceRemovalsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var device = await _devices.GetByIdAsync(command.DeviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return AttendanceErrors.DeviceNotFound;
        }

        device.ApproveRemovals(_clock.UtcNow + ApprovalWindow);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
