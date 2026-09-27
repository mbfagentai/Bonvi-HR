using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Computation;
using Bonvi.Application.Attendance.Retention;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Recompute;

/// <summary>Qayta hisoblash natijasi.</summary>
/// <param name="EmployeeCount">Nechta xodim ko'rib chiqildi.</param>
/// <param name="StoredDays">Nechta kun yozuvi yozildi (yangi yoki yangilangan).</param>
/// <param name="RemovedDays">Nechta eskirgan kun yozuvi o'chirildi.</param>
/// <param name="FrozenDates">
/// So'ralgan sanalardan nechtasi muzlagan (o'tishlari saqlash muddati bilan o'chirilgan bo'lishi
/// mumkin) va shu sababli umuman hisoblanmadi.
/// </param>
public sealed record AttendanceRecomputeOutcome(int EmployeeCount, int StoredDays, int RemovedDays, int FrozenDates = 0)
{
    /// <summary>Hech narsa qilinmagan natija.</summary>
    public static AttendanceRecomputeOutcome Nothing { get; } = new(0, 0, 0);
}

/// <summary>
/// <c>attendance_days</c> ni o'tishlar, munosabatlar va jadvallardan QAYTADAN quradi — SPEC §5, §9.3.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Yagona yozuvchi.</b> Moslik yaratilgandagi to'ldirish, qo'lda qayta
/// hisoblash va keyinroq fon vazifalari (B7) — hammasi shu sinf orqali
/// yozadi. Hisoblashning o'zi B4 ning <see cref="AttendanceDayBuilder"/> i,
/// yuklash — <see cref="IAttendanceComputationSource"/>; bu yerda faqat
/// «natijani bazaga yetkazish» qoidasi.
/// </para>
/// <para>
/// ⭐ <b>Noldan hisoblash bilan BIR XIL holatga keladi.</b>
/// <c>attendance_days</c> — hosila ma'lumot: hisoblash «bu kun saqlanmasin»
/// desa-yu (<see cref="ComputedAttendanceDay.ShouldStore"/> = <c>false</c>),
/// qator bor bo'lsa — qator O'CHIRILADI (koordinator qarori, 2026-09-17).
/// Aks holda jadval dam olish kuniga o'zgartirilgan kunda eski «Kelmadi»
/// abadiy qolardi va ikkinchi qayta hisoblash ham uni tuzatmasdi.
/// </para>
/// <para>
/// ⚠️ <b>DELETE + INSERT emas.</b> Bor qator joyida yangilanadi
/// (<see cref="AttendanceDay.Recompute"/>), yo'g'i qo'shiladi: parallel
/// o'qilayotgan tabel bir lahza bo'sh katak ko'rmasligi kerak (§9.3). O'chirish
/// faqat endi umuman bo'lmasligi kerak bo'lgan bitta qatorga tegadi — bu
/// o'sha taqiqning ma'nosiga zid emas.
/// </para>
/// <para>
/// ⚠️ <b>Muzlagan kunlarga TEGMAYDI</b> (<see cref="AttendanceRetention.FrozenThroughAsync"/>).
/// O'tishlari saqlash muddati bilan o'chirilgan kun qayta hisoblansa, u omon qolgan
/// o'tishlardan qurilib, bir yillik tabel jimgina NOLGA aylanardi (SPEC §9.2.1).
/// To'siq shu yerda, chunki bu yagona yozuvchi: qo'lda qayta hisoblash, to'ldirish,
/// iflos navbatning ±1 qo'shnisi — hammasi shu metoddan o'tadi.
/// </para>
/// <para>
/// ⭐ <b>Parallel yozuvchi — 500 emas.</b> Ikki vazifa bir (xodim, sana) ni bir
/// vaqtda BIRINCHI marta yozsa, ikkinchisi <c>ux_attendance_days_employee_date</c>
/// ga uriladi; bitta eskirgan qatorni ikkalasi o'chirsa, ikkinchisining buyrug'i
/// nol qatorga tegadi. Ikkalasida ham to'plam kuzatuvdan unutiladi va bazadan
/// QAYTA o'qib yoziladi (<see cref="MaxWriteAttempts"/> gacha): hisoblash
/// ma'lumotdan aniq kelib chiqadi, ya'ni ikkinchi urinish g'olib yozganini
/// ko'rib, o'sha natijani yangilaydi. Hangfire qayta urinishi ham buni tiklardi,
/// lekin sinxron navbatda (moslik yaratish so'rovi) foydalanuvchi 500 olardi.
/// </para>
/// <para>
/// <b>Hajm.</b> Sanalar <see cref="AttendanceRules.MaxRecomputeDays"/> kunlik
/// OYNALARGA bo'linadi, har bir oynada xodimlar <see cref="EmployeesPerBatch"/>
/// tadan olinadi: har bir to'plam — o'zgarmas sondagi so'rov (munosabatlar,
/// o'tishlar, kun yozuvlari) va bitta <c>SaveChanges</c>, keyin uning qatorlari
/// KUZATUVDAN UNUTILADI. Ikkalasi ham shart: oynasiz bir yillik chaqiruv (yillik
/// to'rni «butun yilni to'ldirish») bir to'plam uchun butun yilning o'tishlarini
/// yuklardi, unutilmagan qatorlar esa kontekstda to'planib, 540 xodim × 270 kunda
/// ~146 ming obyekt bo'lib 2 GB li serverni bo'g'ardi. Shu tufayli xotirada doim
/// ko'pi bilan bitta to'plam × bitta oyna turadi.
/// </para>
/// </remarks>
public sealed class AttendanceDayRecomputer
{
    /// <summary>Bir to'plamdagi xodimlar soni.</summary>
    /// <remarks>
    /// 100 xodim × 92 kun × kuniga ~10 o'tish ≈ 92 ming <see cref="PassReading"/>
    /// (~5 MB) — 2 GB xotirali serverda bir vaqtda ikki vazifa bo'lsa ham
    /// sezilmaydi, so'rovlar soni esa 540 xodimga atigi oltita to'plam.
    /// </remarks>
    public const int EmployeesPerBatch = 100;

    /// <summary>Bir to'plamni parallel yozuvchi bilan to'qnashganda necha marta urinish.</summary>
    /// <remarks>
    /// Uchinchi to'qnashuv — endi tasodif emas (bir to'plamga uchta yozuvchi),
    /// istisno chaqiruvchiga o'tadi va Hangfire uni keyinroq qaytaradi.
    /// </remarks>
    public const int MaxWriteAttempts = 3;

    private readonly IAttendanceComputationSource _source;
    private readonly IAttendanceDayRepository _days;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDatabaseFault _databaseFault;
    private readonly AttendanceRetention _retention;
    private readonly IClock _clock;

    public AttendanceDayRecomputer(
        IAttendanceComputationSource source,
        IAttendanceDayRepository days,
        IUnitOfWork unitOfWork,
        IDatabaseFault databaseFault,
        AttendanceRetention retention,
        IClock clock)
    {
        _source = source;
        _days = days;
        _unitOfWork = unitOfWork;
        _databaseFault = databaseFault;
        _retention = retention;
        _clock = clock;
    }

    /// <summary>Berilgan xodimlarning berilgan sanalarini qayta hisoblaydi va yozadi.</summary>
    /// <remarks>
    /// Sanalar ketma-ket bo'lishi SHART EMAS: moslik to'ldirishi oylar bo'ylab
    /// tarqalgan kunlarni beradi. Faqat shu sanalarga tegiladi — oraliqdagi
    /// boshqa kunlarning qatorlari o'zgarmaydi.
    /// </remarks>
    /// <param name="employeeIds">Xodimlar.</param>
    /// <param name="dates">Qayta hisoblanadigan sanalar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public Task<AttendanceRecomputeOutcome> RecomputeAsync(
        IReadOnlyCollection<Guid> employeeIds,
        IReadOnlyCollection<DateOnly> dates,
        CancellationToken cancellationToken = default)
        => WriteAsync(employeeIds, dates, WriteMode.Rewrite, cancellationToken);

    /// <summary>
    /// Faqat YO'Q, lekin bo'lishi kerak bo'lgan qatorlarni qo'shadi — bor qatorga tegmaydi, hech narsani o'chirmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Boshlangan smenalar vazifasi uchun (<c>AttendanceStartedShiftsJob</c>):
    /// smenasi hali boshlanmagan kun saqlanmaydi (<c>Planned</c>), reja boshlangach esa
    /// o'tishsiz kun «Kelmadi» qatori bo'lib paydo bo'lishi kerak. Har 15 daqiqada butun
    /// korxonaning bugungi qatorlarini QAYTA yozish (yangi <c>computed_at</c> bilan)
    /// bazani behuda aylantirardi va tabeldagi «oxirgi hisoblash» vaqtini ma'nosiz
    /// qilardi — bor qatorni esa iflos navbat va tungi hisoblash allaqachon yuritadi.
    /// </para>
    /// <para>
    /// Hisoblash, muzlagan sanalar va parallel yozuvchi qoidalari
    /// <see cref="RecomputeAsync"/> niki bilan AYNAN bir xil — farq faqat yozishda.
    /// </para>
    /// </remarks>
    /// <param name="employeeIds">Xodimlar.</param>
    /// <param name="dates">Tekshiriladigan sanalar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public Task<AttendanceRecomputeOutcome> FillMissingAsync(
        IReadOnlyCollection<Guid> employeeIds,
        IReadOnlyCollection<DateOnly> dates,
        CancellationToken cancellationToken = default)
        => WriteAsync(employeeIds, dates, WriteMode.MissingOnly, cancellationToken);

    /// <summary>Ikkala ochiq metodning umumiy yo'li — sanalar oynalarga, xodimlar to'plamlarga bo'linadi.</summary>
    private async Task<AttendanceRecomputeOutcome> WriteAsync(
        IReadOnlyCollection<Guid> employeeIds,
        IReadOnlyCollection<DateOnly> dates,
        WriteMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(employeeIds);
        ArgumentNullException.ThrowIfNull(dates);

        var employees = employeeIds.Distinct().ToList();
        var requested = dates.Distinct().ToList();

        if (employees.Count == 0 || requested.Count == 0)
        {
            return AttendanceRecomputeOutcome.Nothing;
        }

        // ⚠️ Birinchi qadam — muzlagan sanalarni chiqarib tashlash (sabab sinf izohida).
        var frozenThrough = await _retention.FrozenThroughAsync(cancellationToken).ConfigureAwait(false);

        List<DateOnly> ordered =
        [
            .. requested
                .Where(date => frozenThrough is not { } horizon || date > horizon)
                .Order(),
        ];

        var frozen = requested.Count - ordered.Count;

        if (ordered.Count == 0)
        {
            return new AttendanceRecomputeOutcome(0, 0, 0, frozen);
        }

        // Bitta chaqiruv uchun bitta «hozir»: oynalar orasida soat o'tib, bir
        // chaqiruvning kunlari ikki xil «bugun» bilan hisoblanmasin.
        var now = _clock.UtcNow;

        var stored = 0;
        var removed = 0;

        foreach (var window in Windows(ordered))
        {
            var catalog = await _source
                .LoadScheduleCatalogAsync(window[0], window[^1], cancellationToken)
                .ConfigureAwait(false);

            var builder = new AttendanceDayBuilder(
                new ScheduleResolver(
                    catalog.Assignments,
                    catalog.Schedules,
                    new DepartmentTree(catalog.Departments),
                    catalog.Dates),
                now.TashkentDate(),
                now);

            foreach (var batch in employees.Chunk(EmployeesPerBatch))
            {
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        var (batchStored, batchRemoved) = await WriteBatchAsync(builder, batch, window, mode, cancellationToken)
                            .ConfigureAwait(false);

                        stored += batchStored;
                        removed += batchRemoved;
                        break;
                    }
                    catch (Exception exception) when (
                        attempt < MaxWriteAttempts && _databaseFault.IsConcurrentDayWrite(exception))
                    {
                        // Yiqilgan urinish hech narsa saqlamadi (bitta tranzaksiya).
                        // Uning kuzatuvdagi nusxalari unutiladi — qayta urinish
                        // g'olib yozuvchi qoldirgan holatni bazadan o'qiydi.
                        _days.ForgetTracked();
                    }
                }

                // Saqlangan to'plam kuzatuvdan unutiladi: keyingi to'plamga uning
                // qatorlari kerak emas, qolib ketsa esa kontekst har to'plam bilan
                // o'sib boraverardi (sinf izohidagi «Hajm»).
                _days.ForgetTracked();
            }
        }

        return new AttendanceRecomputeOutcome(employees.Count, stored, removed, frozen);
    }

    /// <summary>
    /// Tartiblangan sanalarni ko'pi bilan <see cref="AttendanceRules.MaxRecomputeDays"/> kunlik oynalarga bo'ladi.
    /// </summary>
    /// <remarks>
    /// Chegara — qo'lda qayta hisoblash va hisobotlardagi AYNAN o'sha chorak: to'plam
    /// o'tishlari oyna oralig'ida yuklanadi (<see cref="AttendanceDayBuilder.PassDatesNeeded"/>),
    /// ya'ni bir yillik tarqoq sanalar ham bitta to'plamga yil emas, chorak yuklatadi.
    /// Har bir oyna hisoblashning yuklash shartnomasini o'zi to'liq bajaradi
    /// (o'tishlar va to'r kataklari oyna chekkasidan ±1 kun), shuning uchun oyna
    /// chegarasidagi kun ham bo'linmasdan hisoblangani bilan bir xil chiqadi.
    /// </remarks>
    private static IEnumerable<List<DateOnly>> Windows(List<DateOnly> ordered)
    {
        var window = new List<DateOnly>();

        foreach (var date in ordered)
        {
            if (window.Count > 0 && date.DayNumber - window[0].DayNumber >= AttendanceRules.MaxRecomputeDays)
            {
                yield return window;
                window = [];
            }

            window.Add(date);
        }

        if (window.Count > 0)
        {
            yield return window;
        }
    }

    /// <summary>Bitta to'plamni o'qiydi, hisoblaydi va BITTA saqlash bilan yozadi.</summary>
    /// <remarks>
    /// O'qish ham shu metodda: to'qnashuvdan keyingi urinish o'tishlarni va kun
    /// yozuvlarini qaytadan, g'olib yozuvchi saqlagandan keyingi holatda ko'rishi kerak.
    /// </remarks>
    /// <returns>Yozilgan va o'chirilgan kun yozuvlari soni.</returns>
    private async Task<(int Stored, int Removed)> WriteBatchAsync(
        AttendanceDayBuilder builder,
        Guid[] batch,
        List<DateOnly> ordered,
        WriteMode mode,
        CancellationToken cancellationToken)
    {
        var from = ordered[0];
        var to = ordered[^1];
        var wanted = ordered.ToHashSet();
        var (passFrom, passTo) = AttendanceDayBuilder.PassDatesNeeded(from, to);

        var employments = await _source.LoadEmploymentsAsync(batch, cancellationToken).ConfigureAwait(false);
        var passes = await _source
            .LoadPassesAsync(batch, passFrom, passTo, cancellationToken)
            .ConfigureAwait(false);

        // Tasdiqlangan yo'qlik so'rovlari: «Kelmadi» ni «Sababli» ga
        // aylantiradigan YAGONA manba (SPEC-so'rovlar §B3).
        var excused = await _source
            .LoadExcusedDatesAsync(batch, from, to, cancellationToken)
            .ConfigureAwait(false);

        var existing = (await _days.ListForUpdateAsync(batch, from, to, cancellationToken).ConfigureAwait(false))
            .Where(day => wanted.Contains(day.LocalDate))
            .ToDictionary(day => (day.EmployeeId, day.LocalDate));

        var computedAt = _clock.UtcNow;
        var stored = 0;
        var removed = 0;

        foreach (var employeeId in batch)
        {
            var results = builder.Build(
                employeeId,
                employments.GetValueOrDefault(employeeId) ?? [],
                passes.GetValueOrDefault(employeeId) ?? [],
                ordered,
                excused.GetValueOrDefault(employeeId));

            foreach (var result in results)
            {
                existing.TryGetValue((employeeId, result.LocalDate), out var row);

                if (mode == WriteMode.MissingOnly)
                {
                    // Faqat yo'q qatorni qo'shish — bor qator ham, eskirgani ham tegilmaydi.
                    if (result.ShouldStore && row is null)
                    {
                        _days.Add(AttendanceDay.Compute(employeeId, result.LocalDate, result.Facts, computedAt));
                        stored++;
                    }

                    continue;
                }

                if (result.ShouldStore)
                {
                    if (row is null)
                    {
                        _days.Add(AttendanceDay.Compute(employeeId, result.LocalDate, result.Facts, computedAt));
                    }
                    else
                    {
                        row.Recompute(result.Facts, computedAt);
                    }

                    stored++;
                }
                else if (row is not null)
                {
                    _days.Remove(row);
                    removed++;
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (stored, removed);
    }

    /// <summary>Oraliqdagi barcha kunlar — ikki chekka ham kiradi.</summary>
    /// <param name="from">Birinchi sana.</param>
    /// <param name="to">Oxirgi sana.</param>
    public static IReadOnlyList<DateOnly> DaysBetween(DateOnly from, DateOnly to)
        => from > to
            ? []
            : [.. Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(from.AddDays)];

    /// <summary>Hisoblangan natija bazaga qanday yetkaziladi.</summary>
    private enum WriteMode
    {
        /// <summary>Qayta hisoblash: yo'g'i qo'shiladi, bori yangilanadi, eskirgani o'chiriladi.</summary>
        Rewrite = 1,

        /// <summary>Faqat yo'q, lekin bo'lishi kerak bo'lgan qator qo'shiladi.</summary>
        MissingOnly = 2,
    }
}
