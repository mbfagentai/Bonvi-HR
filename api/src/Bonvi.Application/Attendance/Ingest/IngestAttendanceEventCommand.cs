using System.Globalization;
using Bonvi.Application.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Ingest;

/// <summary>Qurilma yuborgan hodisalarni qabul qilish buyrug'i.</summary>
/// <remarks>
/// Ikkala marshrut (<c>/ingest/{token}</c> va <c>X-Bonvi-Device-Token</c>
/// sarlavhali <c>/ingest</c>) AYNAN shu buyruqqa tushadi — token qayerdan
/// kelgani bu yerda ahamiyatsiz.
/// </remarks>
/// <param name="Token">Qurilma tokeni; berilmagan bo'lsa <c>null</c>.</param>
/// <param name="Payload">So'rov tanasi.</param>
/// <param name="Signature"><c>X-Bonvi-Signature</c> sarlavhasi, bo'lsa.</param>
/// <param name="SourceIp">So'rov manzili.</param>
public sealed record IngestAttendanceEventCommand(
    string? Token,
    AttendanceIngestPayload Payload,
    string? Signature,
    string? SourceIp);

/// <summary>
/// Qurilma hodisalarini tasdiqlaydi, o'qiydi va o'tish sifatida yozadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bu tizimdagi yagona anonim YOZISH yo'li</b> — qurilmaning o'zi
/// token bilan tasdiqlanadi (<see cref="AttendanceDeviceAuthenticator"/>).
/// </para>
/// <para>
/// ⭐ <b>Hamma-yoki-hech narsa tekshiruvi, keyin yozish.</b> Avval BARCHA
/// hodisalar tekshiriladi (soni, shaxs raqami, vaqti), keyingina birinchisi
/// yoziladi. Aks holda 200 ta hodisali so'rovning 150-sida vaqt buzuq
/// chiqsa, 149 tasi yozilib, javob 400 bo'lardi — agent «hech narsa
/// o'tmadi» deb o'ylab qayta yuboradi, yozilganlar takror bo'lib chiqadi,
/// buzuq hodisa esa yana 400 oladi: tushunib bo'lmaydigan yarim holat.
/// </para>
/// <para>
/// ⭐ <b>Takror — zararsiz.</b> Har bir o'tish ALOHIDA saqlanadi va
/// <c>ux_attendance_passes_device_dedupe</c> buzilishi «takror» deb
/// hisoblanadi (<see cref="IDatabaseFault.IsDuplicatePass"/>). Takror
/// <c>last_event_at</c> ni surmaydi, suratni ikkinchi marta saqlamaydi va
/// kunni qayta hisoblash navbatiga qo'ymaydi (SPEC §3.6).
/// </para>
/// <para>
/// ⭐ <b>Bog'lanmagan shaxs TASHLANMAYDI.</b> Moslik topilmasa o'tish
/// <c>resolved_employee_id = NULL</c> bilan saqlanadi: xom raqam va surat —
/// aynan HR ga «bu kim edi» degan savolga kerak bo'ladigan narsa, moslik
/// keyin kiritilganda esa tarixiy o'tishlar o'zi bog'lanadi (§4.1–§4.3).
/// </para>
/// </remarks>
public sealed class IngestAttendanceEventCommandHandler
{
    private readonly AttendanceDeviceAuthenticator _authenticator;
    private readonly IEnumerable<IAttendanceEventParser> _parsers;
    private readonly AttendanceIdentityResolver _identities;
    private readonly IAttendancePassRepository _passes;
    private readonly PassPhotoStore _photos;
    private readonly IDatabaseFault _databaseFault;
    private readonly IAttendanceIngestJournal _journal;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public IngestAttendanceEventCommandHandler(
        AttendanceDeviceAuthenticator authenticator,
        IEnumerable<IAttendanceEventParser> parsers,
        AttendanceIdentityResolver identities,
        IAttendancePassRepository passes,
        PassPhotoStore photos,
        IDatabaseFault databaseFault,
        IAttendanceIngestJournal journal,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _authenticator = authenticator;
        _parsers = parsers;
        _identities = identities;
        _passes = passes;
        _photos = photos;
        _databaseFault = databaseFault;
        _journal = journal;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AttendanceIngestResultDto>> HandleAsync(
        IngestAttendanceEventCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Payload);

        var authenticated = await _authenticator
            .AuthenticateAsync(command.Token, command.Payload.Body, command.Signature, command.SourceIp, cancellationToken)
            .ConfigureAwait(false);

        if (authenticated.IsFailure)
        {
            return authenticated.Error;
        }

        var device = authenticated.Value;
        var receivedAt = _clock.UtcNow;

        // «Qurilma gapiryapti» — natija qanday bo'lishidan qat'i nazar.
        // `last_seen_at` bilan `last_event_at` ajralib qolsa, bu HR uchun
        // aniq tashxis: terminal ulangan, lekin hodisalari rad etilyapti.
        device.NoteSeen(receivedAt, command.SourceIp);

        var parsed = await ParseAsync(device, command.Payload, receivedAt, cancellationToken).ConfigureAwait(false);
        if (parsed.IsFailure)
        {
            _journal.PayloadRejected(device.Id, parsed.Error.Code);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return parsed.Error;
        }

        var stored = 0;
        var duplicates = 0;
        int? lastSkewSeconds = null;

        foreach (var passEvent in parsed.Value)
        {
            var pass = await StoreAsync(device, passEvent, receivedAt, cancellationToken).ConfigureAwait(false);

            if (pass is null)
            {
                duplicates++;
                continue;
            }

            stored++;
            lastSkewSeconds = pass.ClockSkewSeconds;
        }

        // Kuzatuv ustunlari O'TISHDAN KEYIN yoziladi: `NoteEvent` ni
        // saqlashdan oldin chaqirib, keyin takror bo'lib chiqsa, uni
        // qaytarishning yo'li yo'q va takror `last_event_at` ni surib
        // qo'ygan bo'lardi. Narxi — bitta qisqa UPDATE.
        if (lastSkewSeconds is { } skew)
        {
            device.NoteEvent(receivedAt, command.SourceIp, skew);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AttendanceIngestResultDto(Ok: true, Stored: stored, Duplicates: duplicates);
    }

    /// <summary>
    /// Tahlilchini qurilma yozuvi bo'yicha tanlaydi va BARCHA hodisalarni oldindan tekshiradi.
    /// </summary>
    private async Task<Result<IReadOnlyList<RawAttendanceEvent>>> ParseAsync(
        AttendanceDevice device,
        AttendanceIngestPayload payload,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken)
    {
        // `Single`: har bir turga aynan bitta tahlilchi. Topilmasa yoki ikkita
        // bo'lsa — bu DI xatosi, so'rov xatosi emas, va 500 bo'lib ko'rinishi
        // kerak (jimgina «o'qib bo'lmadi» emas).
        var parser = _parsers.Single(candidate => candidate.Provider == device.Provider);

        var parsed = await parser.ParseAsync(payload, cancellationToken).ConfigureAwait(false);
        if (parsed.IsFailure)
        {
            return parsed;
        }

        var events = parsed.Value;

        if (events.Count > IngestLimits.MaxEventsPerRequest)
        {
            return IngestErrors.TooManyEvents;
        }

        foreach (var passEvent in events)
        {
            if (passEvent.RawPersonId.Length > AttendanceIdentity.MaxExternalPersonIdLength
                || passEvent.ExternalEventId is { Length: > IngestLimits.MaxExternalEventIdLength })
            {
                return IngestErrors.Unreadable;
            }

            // Bu tekshiruv `AttendancePass.Record` da ham bor va u yerda
            // QOLADI — domen o'z invariantini o'zi qo'riqlaydi. Bu yerda u
            // yozishdan OLDIN takrorlanadi, chunki birinchi hodisa yozilib
            // bo'lgach rad etish «hamma-yoki-hech narsa» qoidasini buzardi.
            if (Math.Abs((receivedAt - passEvent.OccurredAt).TotalDays) > AttendanceRules.MaxClockSkewDays)
            {
                _journal.ClockRejected(device.Id, passEvent.OccurredAt);
                return Error.Validation("davomat.vaqt_yaroqsiz", "Hodisa vaqti haqiqatga to'g'ri kelmaydi.");
            }
        }

        return Result.Success(events);
    }

    /// <summary>Bitta o'tishni yozadi.</summary>
    /// <returns>Yozilgan o'tish; takror bo'lib chiqsa <c>null</c>.</returns>
    private async Task<AttendancePass?> StoreAsync(
        AttendanceDevice device,
        RawAttendanceEvent passEvent,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken)
    {
        var (direction, directionSource) = ResolveDirection(device.DirectionRole, passEvent);

        var employeeId = await _identities
            .ResolveAsync(device.IdentityProvider, passEvent.RawPersonId, cancellationToken)
            .ConfigureAwait(false);

        // Surat qatordan OLDIN: qatorga kalit kerak. Takror bo'lsa, fayl
        // shu so'rovning o'zida o'chiriladi (quyida). Ikkisi orasida jarayon
        // yiqilsa yetim fayl qoladi — uni surat tozalash ishi supuradi.
        var photoKey = await _photos.TrySaveAsync(passEvent.PhotoBytes, cancellationToken).ConfigureAwait(false);

        var recorded = AttendancePass.Record(
            device.Id,
            device.IdentityProvider,
            passEvent.RawPersonId,
            passEvent.OccurredAt,
            receivedAt,
            DedupeKey(passEvent),
            direction,
            directionSource,
            passEvent.Method,
            passEvent.OffsetAssumed,
            photoKey,
            passEvent.RawEventJson);

        if (recorded.IsFailure)
        {
            // Oldindan tekshiruvdan o'tgan hodisada bu bo'lmasligi kerak:
            // bo'lsa — ikki tekshiruv ajralib ketgan, ya'ni dastur xatosi.
            await DiscardPhotoAsync(photoKey, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Oldindan tekshirilgan o'tish domen tomonidan rad etildi: {recorded.Error.Code}");
        }

        var pass = recorded.Value;

        if (employeeId is { } resolved)
        {
            pass.Resolve(resolved, receivedAt);
        }

        _passes.Add(pass);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_databaseFault.IsDuplicatePass(exception))
        {
            // Qo'shilgan (hali bazada yo'q) yozuvni «o'chirish» uni kuzatuvdan
            // chiqaradi: keyingi saqlash uni qayta INSERT qilmaydi. Qurilmadagi
            // `NoteSeen` o'zgarishi esa kuzatuvda qoladi va keyingi saqlashda
            // yoziladi — muvaffaqiyatsiz saqlash uni yo'qotmaydi.
            _passes.Remove(pass);
            await DiscardPhotoAsync(photoKey, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch
        {
            // Takror EMAS — haqiqiy nosozlik (500). Qatorsiz qolgan surat
            // shu yerda o'chiriladi: aks holda har bir qayta urinish diskka
            // yana bitta yetim fayl qo'shib borardi.
            await DiscardPhotoAsync(photoKey, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        return pass;
    }

    /// <summary>
    /// Yo'nalish: avval qurilma aytgani, keyin qurilma roli, keyin «noma'lum».
    /// </summary>
    /// <remarks>
    /// Rol faqat qurilma yo'nalishni AYTMAGANDA ishlatiladi: qurilma «chiqdi»
    /// desa-yu, turniket «faqat kirish» deb belgilangan bo'lsa, qurilmaga
    /// ishoniladi — noto'g'ri qo'yilgan rol tarixni qayta yozmasligi kerak
    /// (SPEC §3.7).
    /// </remarks>
    private static (PassDirection Direction, DirectionSource Source) ResolveDirection(
        DeviceDirectionRole role,
        RawAttendanceEvent passEvent)
    {
        if (passEvent.Direction is PassDirection.In or PassDirection.Out)
        {
            return (passEvent.Direction, DirectionSource.Device);
        }

        return role switch
        {
            DeviceDirectionRole.EntryOnly => (PassDirection.In, DirectionSource.DeviceRole),
            DeviceDirectionRole.ExitOnly => (PassDirection.Out, DirectionSource.DeviceRole),
            _ => (PassDirection.Unknown, DirectionSource.Unknown),
        };
    }

    /// <summary>
    /// Takrorni ushlash kaliti (SPEC §3.6).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hodisa raqami bor ⇒ <c>s:&lt;raqam&gt;</c>: Hikvision <c>serialNo</c> —
    /// qurilma bo'yicha o'suvchi hisoblagich va qayta yuborilgan hodisa aynan
    /// o'sha raqamni olib keladi. Yo'q ⇒ hodisani jismonan aniqlaydigan uchlik:
    /// <c>t:&lt;shaxs&gt;|&lt;UTC, soniyagacha&gt;|&lt;yo'nalish&gt;</c>.
    /// Soniyagacha qisqartirish ataylab — ba'zi proshivkalar millisekund
    /// farqi bilan qayta yuboradi.
    /// </para>
    /// <para>
    /// ⚠️ Yo'nalish — QURILMA AYTGANI, rol bo'yicha to'ldirilgani emas. Rol
    /// ikki yuborish orasida o'zgartirilsa (HR qurilmani «faqat kirish» qildi),
    /// to'ldirilgan yo'nalish boshqacha chiqib, bitta hodisa ikki marta
    /// yozilardi. Qurilma aytgani esa qayta yuborishda o'zgarmaydi.
    /// </para>
    /// </remarks>
    private static string DedupeKey(RawAttendanceEvent passEvent)
        => passEvent.ExternalEventId is { } eventId
            ? "s:" + eventId
            : string.Create(
                CultureInfo.InvariantCulture,
                $"t:{passEvent.RawPersonId}|{passEvent.OccurredAt.ToUniversalTime():yyyyMMdd'T'HHmmss}|{(int)passEvent.Direction}");

    private async Task DiscardPhotoAsync(string? photoKey, CancellationToken cancellationToken)
    {
        if (photoKey is not null)
        {
            await _photos.DeleteAsync(photoKey, cancellationToken).ConfigureAwait(false);
        }
    }
}
