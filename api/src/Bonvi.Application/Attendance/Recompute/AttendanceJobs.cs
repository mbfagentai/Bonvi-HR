using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Recompute;

/// <summary>
/// <c>attendance:backfill-identity</c> — moslik yaratilgach eski o'tishlarni bog'lash va kunlarini qayta qurish (SPEC §4.3).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>«Hech narsa tashlab yuborilmaydi» degan va'daning ikkinchi yarmi.</b>
/// Qabul yo'li tanimagan o'tishni saqlaydi (§4.1); bu vazifa esa HR raqamni
/// xodimga bog'lagan lahzada o'sha o'tishlarni xodimga beradi va tabelni
/// shu zahoti tiklaydi. Alohida «moslikdan keyin qayta hisobla» tugmasi yo'q
/// — uni bosishni unutish imkoni ham yo'q.
/// </para>
/// <para>
/// <b>Uch qadam va har biri idempotent.</b> (1) bog'lash — faqat hali
/// bog'lanmaganlar; (2) ta'sirlangan har bir sana va uning ikki qo'shnisi
/// qayta hisoblanadi (tungi smena o'tishi oldingi sanaga tegishli bo'lishi
/// mumkin, §5.3); (3) o'sha o'tishlar «hisobga olingan» deb belgilanadi.
/// (2) da yiqilsa, o'tishlar <c>processed_at IS NULL</c> bilan qoladi va
/// iflos navbat (B7, §9.3) ularni o'zi tiklaydi; ikkinchi yurish esa bog'lash
/// uchun hech narsa topmaydi va hech narsani buzmaydi.
/// </para>
/// <para>
/// Argumentlar oddiy qiymatlar (enum, satr, <see cref="Guid"/>): Hangfire
/// ularni saqlab, keyin qayta o'qiydi. Obyekt yoki ro'yxat uzatilmaydi.
/// </para>
/// </remarks>
public sealed class AttendanceBackfillJob
{
    private readonly IAttendancePassRepository _passes;
    private readonly AttendanceDayRecomputer _recomputer;
    private readonly IClock _clock;

    public AttendanceBackfillJob(
        IAttendancePassRepository passes,
        AttendanceDayRecomputer recomputer,
        IClock clock)
    {
        _passes = passes;
        _recomputer = recomputer;
        _clock = clock;
    }

    /// <summary>Shaxs raqamining bog'lanmagan o'tishlarini xodimga beradi va kunlarini qayta quradi.</summary>
    /// <param name="provider">Raqamlar fazosi.</param>
    /// <param name="rawPersonId">Qurilmadagi shaxs raqami.</param>
    /// <param name="employeeId">Moslik ko'rsatgan xodim.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task RunAsync(
        AttendanceIdentityProvider provider,
        string rawPersonId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var attached = await _passes
            .AttachUnmatchedAsync(provider, rawPersonId, employeeId, _clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        if (attached.Count == 0)
        {
            return;
        }

        var dates = attached
            .Select(pass => pass.LocalDate)
            .Distinct()
            .SelectMany(date => new[] { date.AddDays(-1), date, date.AddDays(1) })
            .ToHashSet();

        await _recomputer.RecomputeAsync([employeeId], dates, cancellationToken).ConfigureAwait(false);

        await _passes
            .MarkProcessedAsync([.. attached.Select(pass => pass.Id)], _clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// <c>attendance:recompute-range</c> — qo'lda so'ralgan davrni qayta hisoblash (SPEC §6.7, §5.8).
/// </summary>
/// <remarks>
/// <para>
/// Xodimlar ro'yxati vazifaning O'ZIDA aniqlanadi, so'rovda emas: navbatda
/// kutgan vaqt ichida yangi munosabat yoki o'tish paydo bo'lishi mumkin, va
/// 540 ta identifikatorni vazifa argumentiga yozish saqlanadigan yukni
/// behuda shishirardi.
/// </para>
/// <para>
/// ⚠️ <c>processed_at</c> ga TEGMAYDI. Davr chetidagi o'tish davrdan tashqaridagi
/// qo'shni sanaga tegishli bo'lishi mumkin va u sana bu yerda hisoblanmaydi;
/// belgilash iflos navbatdan haqiqiy ishni yashirardi. Belgilanmagan o'tish
/// esa keyingi navbatda yana bir marta, xuddi shu natija bilan hisoblanadi.
/// </para>
/// </remarks>
public sealed class AttendanceRecomputeRangeJob
{
    private readonly IAttendanceComputationSource _source;
    private readonly AttendanceDayRecomputer _recomputer;

    public AttendanceRecomputeRangeJob(IAttendanceComputationSource source, AttendanceDayRecomputer recomputer)
    {
        _source = source;
        _recomputer = recomputer;
    }

    /// <summary>Davrni bitta xodim yoki hamma uchun qayta hisoblaydi.</summary>
    /// <param name="from">Davr boshi (o'zi ham kiradi).</param>
    /// <param name="to">Davr oxiri (o'zi ham kiradi).</param>
    /// <param name="employeeId">Faqat shu xodim; <c>null</c> — davrga tegishli hamma.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public Task RunAsync(DateOnly from, DateOnly to, Guid? employeeId, CancellationToken cancellationToken)
        => ExecuteAsync(from, to, employeeId, cancellationToken);

    /// <summary><see cref="RunAsync"/> ning o'zi — natijasi bilan (tungi vazifa sanoqlarni jurnalga yozadi).</summary>
    /// <remarks>
    /// Navbatga <see cref="RunAsync"/> qo'yiladi, bu metod emas: Hangfire
    /// qaytarilgan qiymatni vazifa holatiga yozib saqlaydi, bu natija esa faqat
    /// log uchun kerak.
    /// </remarks>
    /// <param name="from">Davr boshi (o'zi ham kiradi).</param>
    /// <param name="to">Davr oxiri (o'zi ham kiradi).</param>
    /// <param name="employeeId">Faqat shu xodim; <c>null</c> — davrga tegishli hamma.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<AttendanceRecomputeOutcome> ExecuteAsync(
        DateOnly from,
        DateOnly to,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> employees = employeeId is { } single
            ? [single]
            : await _source.ListEmployeesAsync(from, to, cancellationToken).ConfigureAwait(false);

        return await _recomputer
            .RecomputeAsync(employees, AttendanceDayRecomputer.DaysBetween(from, to), cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// <c>attendance:schedule-dates</c> — yillik to'r tahriridan keyin FAQAT shu jadvaldagi odamlarning
/// o'zgargan kunlarini qayta hisoblaydi (SPEC-jadval-to'ri §B3).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida vazifa.</b> Ilgari to'r tahriri <see cref="AttendanceRecomputeRangeJob"/> ni
/// «eng erta — eng kech sana, HAMMA xodim» bilan navbatga qo'yardi: 27-sentabrda 2026-yilni
/// «butun yilni to'ldirish» 270 kun × ~540 xodim ≈ 146 ming kunni qayta yozardi — jadvalda
/// bo'lmagan odamlarniki ham, tegilmagan kunlarniki ham. Endi:
/// </para>
/// <list type="bullet">
///   <item>faqat SHU jadval tushadigan xodimlar — hal qiluvchining o'zi aytadi
///         (<see cref="ScheduleResolver"/>, tabeldagi bilan bir xil pog'onalar);</item>
///   <item>faqat o'zgargan sanalar va ularning ±1 qo'shnisi: tungi smena kunining
///         rejasi o'zgarsa, ertangi kunning o'tishi kimga tegishli ekani ham o'zgaradi
///         (§5.3) — qo'shnisiz D ning yangi rejasi D + 1 ning chiqishini o'zinikiga olib,
///         o'sha soatlar ikki kunda sanalardi;</item>
///   <item>kelajak sanalar ham — ularda faqat tasdiqlangan so'rovning «Sababli» qatori
///         bo'ladi va kun dam olishga aylansa, u o'chishi kerak;</item>
///   <item>yozuvchining o'zi sanalarni chorak oynalarga, xodimlarni to'plamlarga bo'ladi
///         (<see cref="AttendanceDayRecomputer"/>) — xotirada doim bitta to'plam.</item>
/// </list>
/// <para>
/// Sanalar argumentda — ro'yxat bo'lib (ko'pi bilan 400 ta, to'r buyrug'ining
/// chegarasi): tarqoq kataklar (masalan faqat shanbalar) oralig'i bilan berilsa,
/// orasidagi hamma kun ham qayta hisoblanardi.
/// </para>
/// </remarks>
public sealed class AttendanceScheduleDatesJob
{
    private readonly IAttendanceComputationSource _source;
    private readonly AttendanceDayRecomputer _recomputer;

    public AttendanceScheduleDatesJob(IAttendanceComputationSource source, AttendanceDayRecomputer recomputer)
    {
        _source = source;
        _recomputer = recomputer;
    }

    /// <summary>Jadvalning o'zgargan sanalarini shu jadvaldagi xodimlar uchun qayta hisoblaydi.</summary>
    /// <param name="scheduleId">Tahrirlangan jadval.</param>
    /// <param name="dates">To'rda yozilgan yoki tozalangan sanalar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public Task RunAsync(Guid scheduleId, DateOnly[] dates, CancellationToken cancellationToken)
        => ExecuteAsync(scheduleId, dates, cancellationToken);

    /// <summary><see cref="RunAsync"/> ning o'zi — natijasi bilan (sinov uchun).</summary>
    /// <param name="scheduleId">Tahrirlangan jadval.</param>
    /// <param name="dates">To'rda yozilgan yoki tozalangan sanalar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<AttendanceRecomputeOutcome> ExecuteAsync(
        Guid scheduleId,
        IReadOnlyCollection<DateOnly> dates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dates);

        List<DateOnly> changed = [.. dates.Distinct().Order()];

        if (changed.Count == 0)
        {
            return AttendanceRecomputeOutcome.Nothing;
        }

        var from = changed[0];
        var to = changed[^1];

        var employments = await _source
            .LoadEmploymentsCoveringAsync(from, to, cancellationToken)
            .ConfigureAwait(false);

        var catalog = await _source.LoadScheduleCatalogAsync(from, to, cancellationToken).ConfigureAwait(false);

        var resolver = new ScheduleResolver(
            catalog.Assignments,
            catalog.Schedules,
            new DepartmentTree(catalog.Departments),
            catalog.Dates);

        /*
          Kim shu jadvalda — sanama-sana: biriktirma yil o'rtasida boshlangan
          yoki odam boshqa lavozimga ko'chgan bo'lishi mumkin. Tozalangan
          (naqshga qaytgan) sanada ham javob o'sha: jadval biriktirmadan
          keladi, katakdan emas.
        */
        var targets = new List<(Guid EmployeeId, List<DateOnly> Dates)>();

        foreach (var (employeeId, list) in employments)
        {
            List<DateOnly> touched =
            [
                .. changed.Where(date =>
                    PrimaryEmployment.Select(list, date) is { } primary
                    && resolver.Resolve(primary, date)?.Schedule.Id == scheduleId),
            ];

            if (touched.Count > 0)
            {
                targets.Add((employeeId, WithNeighbours(touched)));
            }
        }

        int employees = 0, stored = 0, removed = 0, frozen = 0;

        // Bir xil sana to'plamli xodimlar bitta chaqiruvga — odatda hammasi bitta guruh.
        foreach (var group in targets.GroupBy(target => string.Join(',', target.Dates.Select(date => date.DayNumber))))
        {
            var outcome = await _recomputer
                .RecomputeAsync([.. group.Select(target => target.EmployeeId)], group.First().Dates, cancellationToken)
                .ConfigureAwait(false);

            employees += outcome.EmployeeCount;
            stored += outcome.StoredDays;
            removed += outcome.RemovedDays;
            frozen += outcome.FrozenDates;
        }

        return new AttendanceRecomputeOutcome(employees, stored, removed, frozen);
    }

    /// <summary>Har bir sana va uning ikki qo'shnisi, tartiblangan.</summary>
    private static List<DateOnly> WithNeighbours(IEnumerable<DateOnly> dates)
        =>
        [
            .. dates
                .SelectMany(date => new[] { date.AddDays(-1), date, date.AddDays(1) })
                .Distinct()
                .Order(),
        ];
}
