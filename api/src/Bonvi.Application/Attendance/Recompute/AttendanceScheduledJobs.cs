using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Domain.Abstractions;

namespace Bonvi.Application.Attendance.Recompute;

/// <summary>
/// <c>attendance:recompute-dirty</c> — har 15 daqiqada iflos navbatni bo'shatadi (SPEC §9.3).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Yangi hisoblash yo'li EMAS.</b> Kunlarni faqat
/// <see cref="AttendanceDayRecomputer"/> yozadi, kechikib bog'lash esa
/// <see cref="AttendanceBackfillJob"/> ning o'zi — bu sinf ularga nimani
/// berishni hal qiladi, xolos.
/// </para>
/// <para>
/// <b>Qadamlar.</b> (1) Bog'lanmagan o'tishi bor, lekin mosligi ENDI mavjud
/// raqamlar uchun to'ldirish qayta yuriladi: qabul moslik saqlanishidan oldin
/// o'qib, to'ldirishdan keyin yozgan o'tish boshqa hech qayerda ushlanmaydi —
/// iflos navbat bog'lanmagan o'tishga qaramaydi. (2) Eng eski kutayotgan
/// <see cref="MaxDaysPerRun"/> ta (xodim, sana) juftligi olinadi. (3) Har bir
/// sana ±1 kunga kengaytiriladi — tungi smenaning o'tishi qo'shni sanaga
/// tegishli bo'lishi mumkin (§5.3). (4) Qayta hisoblanadi. (5) AYNAN (2) da
/// o'qilgan o'tishlar «hisobga olindi» deb belgilanadi.
/// </para>
/// <para>
/// <b>Nega xodimlar sana TO'PLAMI bo'yicha guruhlanadi.</b> Qayta hisoblovchi
/// «xodimlar × sanalar» ning hammasini hisoblaydi va o'tishlarni eng chekka
/// sanalar oralig'ida yuklaydi. Bugungi xodim bilan bir yil oldingi o'tishi
/// kechikib bog'langan xodim bitta chaqiruvga tushsa, har ikkisi uchun butun
/// yil yuklanib, har biri boshqasining sanalarida ham hisoblanardi. Odatiy
/// holatda (hamma bugun o'tgan) guruh bitta.
/// </para>
/// <para>
/// ⚠️ <c>DELETE</c> + <c>INSERT</c> yo'q — yozuvchi joyida yangilaydi (§9.3 #5).
/// Ikki marta yurish bir xil qatorlarni beradi: ikkinchi yurishda navbat bo'sh.
/// Muzlagan sana (<c>attendance.frozenThrough</c>) hisoblanmaydi, uning o'tishlari
/// esa baribir belgilanadi — aks holda ular navbatda abadiy qolib, o'tish
/// tozalashida har kecha «buzilgan navbat» ogohlantirishini chiqarardi.
/// </para>
/// </remarks>
public sealed class AttendanceDirtyRecomputeJob
{
    /// <summary>Bir yurishda eng ko'p nechta (xodim, sana) juftligi (SPEC §9.3).</summary>
    public const int MaxDaysPerRun = 5000;

    /// <summary>Bir yurishda eng ko'p nechta shaxs raqami kechikib bog'lanadi.</summary>
    /// <remarks>
    /// Oyna tor, ya'ni odatda nol yoki bitta. Chegara faqat g'ayrioddiy holatda
    /// (masalan qabul bir necha daqiqa eski keshdan o'qigan paytda katta import)
    /// bitta yurish cho'zilib ketmasligi uchun; qolgani keyingi yurishda.
    /// </remarks>
    public const int MaxLateAttachesPerRun = 200;

    private readonly IAttendancePassRepository _passes;
    private readonly AttendanceBackfillJob _backfill;
    private readonly AttendanceDayRecomputer _recomputer;
    private readonly IAttendanceJobJournal _journal;
    private readonly IClock _clock;

    public AttendanceDirtyRecomputeJob(
        IAttendancePassRepository passes,
        AttendanceBackfillJob backfill,
        AttendanceDayRecomputer recomputer,
        IAttendanceJobJournal journal,
        IClock clock)
    {
        _passes = passes;
        _backfill = backfill;
        _recomputer = recomputer;
        _journal = journal;
        _clock = clock;
    }

    /// <summary>Navbatni bir marta bo'shatadi.</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi (Hangfire beradi).</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var attachable = await _passes
            .ListAttachableUnmatchedAsync(MaxLateAttachesPerRun, cancellationToken)
            .ConfigureAwait(false);

        foreach (var identity in attachable)
        {
            await _backfill
                .RunAsync(identity.Provider, identity.RawPersonId, identity.EmployeeId, cancellationToken)
                .ConfigureAwait(false);
        }

        var pending = await _passes.ListPendingAsync(MaxDaysPerRun, cancellationToken).ConfigureAwait(false);

        var pendingDays = pending.Select(pass => (pass.EmployeeId, pass.LocalDate)).Distinct().Count();

        var groups = pending
            .GroupBy(pass => pass.EmployeeId)
            .Select(employee => (
                EmployeeId: employee.Key,
                Dates: WithNeighbours(employee.Select(pass => pass.LocalDate)),
                PassIds: employee.Select(pass => pass.Id).ToList()))
            .GroupBy(employee => DateSetKey(employee.Dates));

        int stored = 0, removed = 0, frozen = 0, marked = 0;

        foreach (var group in groups)
        {
            var employees = group.Select(employee => employee.EmployeeId).ToList();
            var dates = group.First().Dates;

            var outcome = await _recomputer
                .RecomputeAsync(employees, dates, cancellationToken)
                .ConfigureAwait(false);

            stored += outcome.StoredDays;
            removed += outcome.RemovedDays;
            frozen += outcome.FrozenDates;

            // Guruh bo'yicha belgilash: keyingi guruh yiqilsa ham, bu guruhning
            // ishi navbatda qayta turmaydi.
            List<Guid> passIds = [.. group.SelectMany(employee => employee.PassIds)];

            await _passes.MarkProcessedAsync(passIds, _clock.UtcNow, cancellationToken).ConfigureAwait(false);

            marked += passIds.Count;
        }

        _journal.DirtyRecomputeFinished(
            new DirtyRecomputeReport(attachable.Count, pendingDays, stored, removed, frozen, marked));
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

    /// <summary>Tartiblangan sanalar to'plamining kaliti — bir xil to'plamli xodimlar bitta chaqiruvga.</summary>
    private static string DateSetKey(List<DateOnly> dates)
        => string.Join(',', dates.Select(date => date.DayNumber));
}

/// <summary>
/// <c>attendance:nightly-recompute</c> — har kecha oxirgi uch kunni HAMMA uchun qayta hisoblaydi (SPEC §9.2).
/// </summary>
/// <remarks>
/// <para>
/// Iflos navbat faqat YANGI o'tishni ko'radi. Jadval, biriktirma yoki mehnat
/// munosabati orqaga sana bilan tahrirlansa, o'tishlar o'zgarmaydi va navbat
/// jim turadi — shu vazifa o'sha tahrirni uch kun ichida tabelga yetkazadi.
/// Uzoqroq o'tmish qo'lda qayta hisoblash bilan tuzatiladi (§5.8).
/// </para>
/// <para>
/// Sanalar YURISH paytida olinadi: takroriy vazifa ifodasi ro'yxatga olinganda
/// bir marta saqlanadi, ya'ni argumentga yozilgan «bugun» abadiy o'sha kun
/// bo'lib qolardi. Hisoblashning o'zi <see cref="AttendanceRecomputeRangeJob"/> —
/// <c>POST /recompute</c> bilan bir yo'l.
/// </para>
/// </remarks>
public sealed class AttendanceNightlyRecomputeJob
{
    /// <summary>Bugundan necha kun orqaga: bugun − 2 … bugun — uch kun.</summary>
    /// <remarks>
    /// ⚠️ <b>Erta ketishni ham shu son to'ldiradi.</b> Reja hali tugamagan kunda erta
    /// ketish 0 yoziladi (<c>AttendanceDayBuilder</c>), va keyin o'tish kelmasa, uni
    /// faqat shu vazifa qayta hisoblaydi. D sanasi D, D + 1 va D + 2 kunlarining
    /// 03:10 idagi yurishiga tushadi; smena 24 soatdan qisqa (domen boshlanish =
    /// tugashni rad etadi), ya'ni D ning rejasi D + 2 yarim tunigacha albatta
    /// tugaydi — 03:10 dan keyin tugaydigan tungi smena ham. 1 ga tushirilsa, o'sha
    /// tungi smenalarning erta ketishi abadiy 0 qolardi.
    /// </remarks>
    public const int DaysBack = 2;

    private readonly AttendanceRecomputeRangeJob _range;
    private readonly IAttendanceJobJournal _journal;
    private readonly IClock _clock;

    public AttendanceNightlyRecomputeJob(
        AttendanceRecomputeRangeJob range,
        IAttendanceJobJournal journal,
        IClock clock)
    {
        _range = range;
        _journal = journal;
        _clock = clock;
    }

    /// <summary>Oxirgi uch kunni qayta hisoblaydi.</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi (Hangfire beradi).</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var to = _clock.Today;
        var from = to.AddDays(-DaysBack);

        var outcome = await _range.ExecuteAsync(from, to, null, cancellationToken).ConfigureAwait(false);

        _journal.NightlyRecomputeFinished(from, to, outcome);
    }
}

/// <summary>
/// <c>attendance:started-shifts</c> — har 15 daqiqada smenasi BOSHLANGAN, lekin qatori hali yo'q ish kunlarini yozadi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega kerak.</b> Smenasi hali boshlanmagan kun saqlanmaydi (<c>Planned</c> —
/// panelning «Hali boshlanmagan» qoidasi, <c>DashboardStatusRule.HasNotStarted</c>):
/// aks holda 03:10 dagi tungi hisoblash bugungi 09:00 lik smenaning HAMMA odamini
/// «Kelmadi» qilib yozib qo'yardi. Lekin reja boshlangach kelmagan odamning
/// «Kelmadi» qatori paydo bo'lishi SHART — hisobotlar faqat saqlangan qatorni
/// o'qiydi. O'tishsiz kunni hech qanday o'tish «iflos» qilmaydi, ya'ni iflos
/// navbat uni ko'rmaydi; shu vazifa esa uni reja boshlanganidan ko'pi bilan
/// 15 daqiqa keyin yozadi (20:00 lik tungi smenani ham).
/// </para>
/// <para>
/// <b>Faqat YO'Q qatorlar qo'shiladi</b> (<see cref="AttendanceDayRecomputer.FillMissingAsync"/>):
/// bor qatorni iflos navbat va tungi hisoblash yuritadi, uni har 15 daqiqada
/// qayta yozish esa behuda yuk bo'lardi. Kecha ham qaraladi — 23:50 da boshlangan
/// smena yarim tundan keyingi birinchi yurishda «kecha» bo'lib qoladi.
/// </para>
/// </remarks>
public sealed class AttendanceStartedShiftsJob
{
    /// <summary>Bugundan necha kun orqaga qaraladi: kecha va bugun.</summary>
    public const int DaysBack = 1;

    private readonly IAttendanceComputationSource _source;
    private readonly AttendanceDayRecomputer _recomputer;
    private readonly IAttendanceJobJournal _journal;
    private readonly IClock _clock;

    public AttendanceStartedShiftsJob(
        IAttendanceComputationSource source,
        AttendanceDayRecomputer recomputer,
        IAttendanceJobJournal journal,
        IClock clock)
    {
        _source = source;
        _recomputer = recomputer;
        _journal = journal;
        _clock = clock;
    }

    /// <summary>Kecha va bugunning boshlangan, lekin yozilmagan ish kunlarini yozadi.</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi (Hangfire beradi).</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var to = _clock.Today;
        var from = to.AddDays(-DaysBack);

        var employees = await _source.ListEmployeesAsync(from, to, cancellationToken).ConfigureAwait(false);

        var outcome = await _recomputer
            .FillMissingAsync(employees, AttendanceDayRecomputer.DaysBetween(from, to), cancellationToken)
            .ConfigureAwait(false);

        _journal.StartedShiftsFilled(from, to, outcome);
    }
}
