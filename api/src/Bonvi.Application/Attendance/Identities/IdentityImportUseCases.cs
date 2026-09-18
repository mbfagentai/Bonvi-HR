using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Identities;

/// <summary>Verifix importining umumiy chegaralari.</summary>
public static class IdentityImportLimits
{
    /// <summary>Fayl hajmining chegarasi — 10 MB (SPEC §4.4).</summary>
    /// <remarks>
    /// 5 000 qatorlik ro'yxat xlsx da ~300 KB. 10 MB — tasodifan tanlangan
    /// boshqa fayl (surat, arxiv) xotirada ochilmasligi uchun chegara.
    /// </remarks>
    public const long MaxFileBytes = 10L * 1024 * 1024;

    /// <summary>Noaniq moslikda ko'rsatiladigan eng ko'p nomzod (SPEC §4.4: «2…5»).</summary>
    /// <remarks>
    /// Undan ko'pi «noaniq» emas — ism shunchalik keng tarqalganki, ro'yxatdan
    /// tanlash taxmin bo'lardi; bunday qator «topilmadi» bo'ladi va operator
    /// xodimni qidiruv orqali o'zi tanlaydi.
    /// </remarks>
    public const int MaxCandidates = 5;
}

/// <summary>Import faylini oldindan ko'radi — HECH NARSA yozmaydi (<c>POST /identities/import</c>).</summary>
/// <param name="Content">Fayl mazmuni.</param>
/// <param name="FileName">Mijozdagi fayl nomi — faqat kengaytmasi o'qiladi.</param>
/// <param name="SizeBytes">Fayl hajmi.</param>
/// <param name="Provider">Qaysi raqamlar fazosiga moslanadi.</param>
public sealed record PreviewIdentityImportQuery(
    Stream Content,
    string? FileName,
    long SizeBytes,
    AttendanceIdentityProvider Provider);

/// <summary>Operator tasdiqlagan bitta qaror.</summary>
/// <param name="RawPersonId">Shaxs raqami.</param>
/// <param name="EmployeeId">Bog'lanadigan xodim.</param>
/// <param name="DisplayNameInDevice">Fayldagi F.I.Sh. — moslik yozuvida manba matni bo'lib qoladi.</param>
public sealed record IdentityImportDecision(string? RawPersonId, Guid EmployeeId, string? DisplayNameInDevice);

/// <summary>Tasdiqlangan qatorlardan mosliklar yaratadi (<c>POST /identities/import/confirm</c>).</summary>
/// <param name="Provider">Oldindan ko'rishdagi bilan bir xil fazo.</param>
/// <param name="Rows">Qarorlar.</param>
public sealed record ConfirmIdentityImportCommand(
    AttendanceIdentityProvider Provider,
    IReadOnlyList<IdentityImportDecision>? Rows);

/// <summary><see cref="PreviewIdentityImportQuery"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// ⭐ <b>Birinchi mos kelgan qoida yutadi</b> (SPEC §4.4): allaqachon
/// bog'langan → JSHSHIR bitta xodimga aniq mos → F.I.Sh. bitta xodimga aniq
/// mos → F.I.Sh. 2…5 xodimga mos (noaniq) → topilmadi. Tartib muhim:
/// JSHSHIR ism o'zgarishiga (turmushga chiqish, transliteratsiya) chidamli,
/// ism esa emas.
/// </para>
/// <para>
/// <b>So'rovlar soni qatorlarga bog'liq emas</b>: mavjud mosliklar — bitta
/// so'rov, nomzodlar — o'qish modelining bitta chaqiruvi.
/// </para>
/// </remarks>
public sealed class PreviewIdentityImportQueryHandler
{
    private readonly IAttendanceImportReader _reader;
    private readonly IAttendanceIdentityRepository _identities;
    private readonly IAttendanceReadModel _readModel;

    public PreviewIdentityImportQueryHandler(
        IAttendanceImportReader reader,
        IAttendanceIdentityRepository identities,
        IAttendanceReadModel readModel)
    {
        _reader = reader;
        _identities = identities;
        _readModel = readModel;
    }

    public async Task<Result<IdentityImportPreviewDto>> HandleAsync(
        PreviewIdentityImportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.SizeBytes > IdentityImportLimits.MaxFileBytes)
        {
            return AttendanceErrors.ImportFileTooLarge;
        }

        if (FormatOf(query.FileName) is not { } format)
        {
            return AttendanceErrors.ImportFileType;
        }

        var rows = await _reader
            .ReadAsync(query.Content, format, IdentityImportSheet.RowsToRead, cancellationToken)
            .ConfigureAwait(false);

        if (rows is null)
        {
            // Kengaytma to'g'ri, mazmun emas (masalan .xlsx deb nomlangan PDF):
            // foydalanuvchi uchun bu ham «fayl turi noto'g'ri».
            return AttendanceErrors.ImportFileType;
        }

        var parsed = IdentityImportSheet.Parse(rows);
        if (parsed.IsFailure)
        {
            return parsed.Error;
        }

        var lines = parsed.Value;

        var mapped = (await _identities
                .FindManyAsync(query.Provider, [.. lines.Select(line => line.RawPersonId)], cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(identity => identity.ExternalPersonId, identity => identity.EmployeeId, StringComparer.Ordinal);

        var matches = await _readModel
            .FindEmployeeCandidatesAsync(
                [
                    .. lines.Select(line => new ImportCandidateLookup(
                        line.FullName,
                        line.Pinfl,
                        mapped.TryGetValue(line.RawPersonId, out var employeeId) ? employeeId : null)),
                ],
                IdentityImportLimits.MaxCandidates + 1,
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<IdentityImportRowDto> items =
        [
            .. lines.Select((line, index) => ToRow(line, matches[index], mapped.GetValueOrDefault(line.RawPersonId))),
        ];

        return new IdentityImportPreviewDto(query.Provider, items.Count, items);
    }

    /// <summary>Kengaytmadan format; noma'lum bo'lsa <c>null</c>.</summary>
    /// <remarks>
    /// Faqat kengaytma: brauzer <c>.csv</c> uchun <c>text/csv</c>,
    /// <c>application/vnd.ms-excel</c> yoki umuman bo'sh tur yuboradi — unga
    /// tayanib bo'lmaydi. Mazmunning haqiqatan shu format ekanini o'quvchi hal
    /// qiladi.
    /// </remarks>
    private static ImportFileFormat? FormatOf(string? fileName)
        => Path.GetExtension(fileName ?? string.Empty).ToUpperInvariant() switch
        {
            ".XLSX" => ImportFileFormat.Xlsx,
            ".CSV" => ImportFileFormat.Csv,
            _ => null,
        };

    private static IdentityImportRowDto ToRow(IdentityImportLine line, ImportCandidateMatch match, Guid mappedEmployeeId)
    {
        if (mappedEmployeeId != Guid.Empty)
        {
            return Row(
                IdentityMatchKind.AlreadyMapped,
                mappedEmployeeId,
                match.Mapped?.FullName,
                match.Mapped is null ? [] : [match.Mapped]);
        }

        if (match.ByPinfl.Count == 1)
        {
            return Row(IdentityMatchKind.ExactPinfl, match.ByPinfl[0].EmployeeId, match.ByPinfl[0].FullName, match.ByPinfl);
        }

        return match.ByName.Count switch
        {
            1 => Row(IdentityMatchKind.ExactName, match.ByName[0].EmployeeId, match.ByName[0].FullName, match.ByName),
            >= 2 and <= IdentityImportLimits.MaxCandidates => Row(IdentityMatchKind.Ambiguous, null, null, match.ByName),
            _ => Row(IdentityMatchKind.None, null, null, []),
        };

        IdentityImportRowDto Row(
            IdentityMatchKind kind,
            Guid? suggestedId,
            string? suggestedName,
            IReadOnlyList<ImportCandidateDto> candidates)
            => new(
                line.RowNumber,
                line.RawPersonId,
                line.FullName,
                line.Pinfl,
                kind,
                suggestedId,
                suggestedName,
                candidates);
    }
}

/// <summary><see cref="ConfirmIdentityImportCommand"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// <b>Qator — mustaqil qaror.</b> Bittasining xatosi qolganlarini to'xtatmaydi:
/// 540 qatorlik ro'yxatda bitta noto'g'ri xodim butun importni qaytarib
/// yubormasligi kerak. Yaratilmagani sababi bilan <c>failed</c> da qaytadi.
/// </para>
/// <para>
/// Allaqachon bog'langan raqam (yoki shu so'rovda ikkinchi marta kelgani)
/// XATO emas — <c>skipped</c>: oldindan ko'rish va tasdiq orasida boshqa
/// operator bog'lab qo'ygan bo'lishi mumkin va natija baribir istalgani.
/// </para>
/// <para>
/// Barcha mosliklar BITTA <c>SaveChanges</c> bilan yoziladi, keyin har biri
/// keshdan o'chiriladi va o'z to'ldirish vazifasini oladi — xuddi qo'lda
/// yaratishdagidek (SPEC §4.3).
/// </para>
/// </remarks>
public sealed class ConfirmIdentityImportCommandHandler
{
    private readonly IAttendanceIdentityRepository _identities;
    private readonly IAttendanceReadModel _readModel;
    private readonly AttendanceIdentityCache _cache;
    private readonly IBackgroundJobQueue _jobs;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmIdentityImportCommandHandler(
        IAttendanceIdentityRepository identities,
        IAttendanceReadModel readModel,
        AttendanceIdentityCache cache,
        IBackgroundJobQueue jobs,
        IUnitOfWork unitOfWork)
    {
        _identities = identities;
        _readModel = readModel;
        _cache = cache;
        _jobs = jobs;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IdentityImportResultDto>> HandleAsync(
        ConfirmIdentityImportCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rows = command.Rows ?? [];

        if (rows.Count > AttendanceRules.MaxImportRows)
        {
            return AttendanceErrors.ImportTooManyRows;
        }

        if (rows.Count == 0)
        {
            return new IdentityImportResultDto(0, 0, []);
        }

        var personIds = rows
            .Select(row => row.RawPersonId?.Trim())
            .OfType<string>()
            .Where(id => id.Length > 0)
            .ToList();

        var taken = (await _identities
                .FindManyAsync(command.Provider, personIds, cancellationToken)
                .ConfigureAwait(false))
            .Select(identity => identity.ExternalPersonId)
            .ToHashSet(StringComparer.Ordinal);

        var employees = await _readModel
            .ExistingEmployeeIdsAsync([.. rows.Select(row => row.EmployeeId)], cancellationToken)
            .ConfigureAwait(false);

        var created = new List<AttendanceIdentity>();
        var failed = new List<IdentityImportFailureDto>();
        var skipped = 0;

        foreach (var row in rows)
        {
            if (row is null)
            {
                continue;
            }

            var linked = AttendanceIdentity.Link(
                command.Provider,
                row.RawPersonId,
                row.EmployeeId,
                IdentitySource.Import,
                row.DisplayNameInDevice);

            var rawPersonId = row.RawPersonId?.Trim() ?? string.Empty;

            if (linked.IsFailure)
            {
                failed.Add(new IdentityImportFailureDto(rawPersonId, linked.Error.Code));
                continue;
            }

            var identity = linked.Value;

            if (identity.ExternalPersonId.Length > AttendanceIdentity.MaxExternalPersonIdLength)
            {
                failed.Add(new IdentityImportFailureDto(rawPersonId, AttendanceErrors.PersonIdTooLong.Code));
                continue;
            }

            // Band raqam xodim tekshiruvidan OLDIN: allaqachon bog'langan qator
            // qanday xodim ko'rsatilganidan qat'i nazar «o'tkazib yuborildi».
            if (taken.Contains(identity.ExternalPersonId))
            {
                skipped++;
                continue;
            }

            if (!employees.Contains(identity.EmployeeId))
            {
                failed.Add(new IdentityImportFailureDto(rawPersonId, AttendanceErrors.EmployeeNotFound.Code));
                continue;
            }

            // Shu so'rovda ikkinchi marta kelgan raqam ham endi «band».
            taken.Add(identity.ExternalPersonId);

            _identities.Add(identity);
            created.Add(identity);
        }

        if (created.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var identity in created)
        {
            _cache.Evict(identity.Provider, identity.ExternalPersonId);

            await _jobs
                .EnqueueAsync<AttendanceBackfillJob>(job => job.RunAsync(
                    identity.Provider,
                    identity.ExternalPersonId,
                    identity.EmployeeId,
                    CancellationToken.None))
                .ConfigureAwait(false);
        }

        return new IdentityImportResultDto(created.Count, skipped, failed);
    }
}
