using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Agent;

/// <summary>Agent o'lchagan qurilma soati.</summary>
/// <param name="Token">Qurilma tokeni.</param>
/// <param name="Body">So'rovning xom baytlari — imzo shular ustida.</param>
/// <param name="Signature">Imzo sarlavhasi.</param>
/// <param name="SourceIp">So'rov manzili.</param>
/// <param name="DeviceTime">Terminalning O'Z joriy vaqti (siljishi bilan).</param>
/// <param name="TerminalSerial">Agent o'qigan terminal seriyasi (<c>X-Bonvi-Terminal-Serial</c>).</param>
public sealed record ReportDeviceClockCommand(
    string? Token,
    byte[] Body,
    string? Signature,
    string? SourceIp,
    DateTimeOffset DeviceTime,
    string? TerminalSerial);

/// <summary>
/// Qurilma soatining og'ishini TO'G'RIDAN-TO'G'RI yozadi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega o'tishdan olingan son yetmaydi.</b> Ilgari og'ish
/// <c>qabul qilingan − sodir bo'lgan</c> dan hisoblanardi va qurilmaning
/// o'zi push qilganda bu haqiqatan soat farqi edi. Agent esa hodisalarni
/// TORTIB oladi: u bir necha daqiqa o'chib tursa, qaytganda eski
/// hodisalarni yuboradi va o'sha ayirma o'n daqiqaga chiqib ketadi —
/// soati soniyagacha to'g'ri turgan terminal «nosoz» bo'lib ko'rinardi
/// (2026-09-21 da aynan shunday bo'ldi).
/// </para>
/// <para>
/// Bu yerda esa ikkita JORIY vaqt solishtiriladi va natija navbatga ham,
/// tarmoqqa ham bog'liq emas. Tarmoq kechikishi (bir necha o'n millisoniya)
/// besh daqiqalik chegara oldida sezilmaydi.
/// </para>
/// </remarks>
public sealed class ReportDeviceClockCommandHandler
{
    /// <summary>Ishonarli og'ishning chegarasi — bundan kattasi yozilmaydi.</summary>
    /// <remarks>
    /// Bir yildan katta farq — soat emas, buzuq qiymat (masalan 1970 yil
    /// yoki noto'g'ri tahlil qilingan satr). Uni yozib qo'yish qurilma
    /// kartasida ma'nosiz raqam qoldirardi.
    /// </remarks>
    public const int MaxOffsetSeconds = 366 * 24 * 60 * 60;

    private readonly AttendanceDeviceAuthenticator _authenticator;
    private readonly IAttendanceIngestJournal _journal;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ReportDeviceClockCommandHandler(
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

    public async Task<Result<DeviceClockDto>> HandleAsync(
        ReportDeviceClockCommand command,
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

        var now = _clock.UtcNow;
        var offset = (command.DeviceTime - now).TotalSeconds;

        if (double.IsNaN(offset) || Math.Abs(offset) > MaxOffsetSeconds)
        {
            return AttendanceErrors.DeviceClockUnreadable;
        }

        var device = authenticated.Value;

        // Boshqa terminalning soati bu qurilmaning kartasiga yozilmasin —
        // va uning «terminal javob beryapti» belgisini yoqmasin.
        var bound = device.ConfirmTerminal(command.TerminalSerial, required: true);
        if (bound.IsFailure)
        {
            _journal.TerminalMismatch(device.Id, command.TerminalSerial);
            return bound.Error;
        }

        var seconds = (int)Math.Round(offset);

        device.NoteClock(seconds, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new DeviceClockDto(seconds, device.HasClockDrift);
    }
}
