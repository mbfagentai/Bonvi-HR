using System.Globalization;
using Bonvi.Application.Attendance.Computation;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Tests.Attendance.Computation;

/// <summary>
/// Davomat hisoblashi uchun sahna: bitta xodim, uning munosabatlari, jadvallari va o'tishlari.
/// </summary>
/// <remarks>
/// <para>
/// Mock yo'q, baza yo'q — hamma narsa HAQIQIY domen obyektlari
/// (<see cref="Employment.Hire"/>, <see cref="WorkSchedule.Create"/>,
/// <see cref="ScheduleAssignment.Assign"/>). Sabab: hisoblash aynan shu
/// obyektlarning invariantlariga tayanadi (yettita kun, yarim tundan
/// o'tuvchi smena, tanaffus oynasining tekshiruvi). Soxta obyekt bu
/// invariantlarni chetlab o'tib, ishlab chiqarishda uchramaydigan holatni
/// sinab qo'yardi.
/// </para>
/// <para>
/// Barcha vaqtlar TOSHKENT soatida yoziladi («09:00» — ertalab to'qqiz),
/// chunki SPEC dagi har bir misol shunday. UTC ga aylantirishni
/// hisoblashning o'zi bajaradi va shu aylantirish ham tekshiriladigan narsa.
/// </para>
/// </remarks>
internal sealed class AttendanceScenario
{
    /// <summary>2026-09-14 — dushanba. Barcha misollar shu haftada.</summary>
    public static readonly DateOnly Monday = new(2026, 9, 14);

    public static readonly DateOnly Tuesday = Monday.AddDays(1);

    /// <summary>Munosabat va biriktirmalarning sukut boshlanishi — tekshirilayotgan haftadan ancha oldin.</summary>
    public static readonly DateOnly LongAgo = new(2026, 1, 1);

    private readonly List<Employment> _employments = [];
    private readonly List<WorkSchedule> _schedules = [];
    private readonly List<ScheduleAssignment> _assignments = [];
    private readonly List<Department> _departments = [];
    private readonly List<PassReading> _passes = [];
    private readonly List<WorkScheduleDate> _dates = [];

    /*
      Sahnaning «bugun»i — sukut bo'yicha ENG UZOQ kelajak, ya'ni
      tekshirilayotgan hamma kun O'TGAN sanaladi. Sabab: misollarning
      hammasi o'tmish haqida va ularga kelajak qoidasi
      (`AttendanceDayStatus.Planned`) tegishli emas. Aynan o'sha qoidani
      sinaydigan testlar `Today(...)` bilan buni ochiq o'zgartiradi.
    */
    private DateOnly _today = DateOnly.MaxValue;

    /*
      «Hozir» ham xuddi shunday — ENG UZOQ kelajak: har bir reja allaqachon
      tugagan, ya'ni erta ketish odatdagidek sanaladi. Reja tugashidan
      oldingi qoidani sinaydigan testlar `Now(...)` bilan buni o'zgartiradi.
    */
    private DateTimeOffset _now = DateTimeOffset.MaxValue;

    public Guid EmployeeId { get; } = Guid.CreateVersion7();

    /// <summary>Ofis xodimi: sukut jadval 09:00–18:00, har kuni ish kuni.</summary>
    public static AttendanceScenario Office(
        int breakMinutes = 0,
        string? breakStart = null,
        string? breakEnd = null)
    {
        var scenario = new AttendanceScenario();
        scenario.Employ();
        scenario.Assign(Schedule("09:00", "18:00", breakMinutes, breakStart, breakEnd));
        return scenario;
    }

    /// <summary>
    /// «Erkin ish jadvali» xodimi (SPEC-erkin-jadval §2.3): sukut 08:30–18:00, tanaffus 120 daqiqa
    /// 12:00–14:00 oynasida, norma berilmasa — sukut (7:30). Har kuni ish kuni.
    /// </summary>
    public static AttendanceScenario FlexibleOffice(
        string start = "08:30",
        string end = "18:00",
        int breakMinutes = 120,
        string? breakStart = "12:00",
        string? breakEnd = "14:00",
        int? requiredMinutes = null)
    {
        var scenario = new AttendanceScenario();
        scenario.Employ();
        scenario.Assign(FlexibleSchedule(start, end, breakMinutes, breakStart, breakEnd, requiredMinutes));
        return scenario;
    }

    /// <summary>Hamma kuni bir xil ERKIN smena bo'lgan haftalik jadval.</summary>
    public static WorkSchedule FlexibleSchedule(
        string start = "08:30",
        string end = "18:00",
        int breakMinutes = 120,
        string? breakStart = "12:00",
        string? breakEnd = "14:00",
        int? requiredMinutes = null)
        => Schedule(
        [
            .. Enumerable.Range(1, WorkSchedule.WeeklyDayCount)
                .Select(index => WorkingDay(index, start, end, breakMinutes, breakStart, breakEnd) with
                {
                    IsFlexible = true,
                    RequiredMinutes = requiredMinutes,
                }),
        ]);

    /// <summary>Toshkent soatidagi lahza.</summary>
    public static DateTimeOffset At(DateOnly date, string time)
        => new(date.ToDateTime(Time(time)), BonviTime.TashkentOffset);

    public static TimeOnly Time(string value) => TimeOnly.Parse(value, CultureInfo.InvariantCulture);

    /// <summary>Haftalik jadval: <paramref name="daysOff"/> dan boshqa hamma kun bir xil smena.</summary>
    public static WorkSchedule Schedule(
        string start,
        string end,
        int breakMinutes = 0,
        string? breakStart = null,
        string? breakEnd = null,
        int graceIn = 10,
        int graceOut = 10,
        IReadOnlyCollection<int>? daysOff = null)
    {
        var days = Enumerable.Range(1, WorkSchedule.WeeklyDayCount)
            .Select(index => daysOff?.Contains(index) == true
                ? new WorkScheduleDayInput(index, IsWorking: false)
                : WorkingDay(index, start, end, breakMinutes, breakStart, breakEnd))
            .ToList();

        return Schedule(days, graceIn, graceOut);
    }

    /// <summary>Kunlari alohida berilgan jadval.</summary>
    public static WorkSchedule Schedule(IReadOnlyCollection<WorkScheduleDayInput> days, int graceIn = 10, int graceOut = 10)
    {
        var result = WorkSchedule.Create(
            $"Jadval {Guid.CreateVersion7()}",
            days,
            graceInMinutes: graceIn,
            graceOutMinutes: graceOut);

        result.IsSuccess.ShouldBeTrue("sahnadagi jadval domen tekshiruvidan o'tishi kerak");
        return result.Value;
    }

    public static WorkScheduleDayInput WorkingDay(
        int dayIndex,
        string start,
        string end,
        int breakMinutes = 0,
        string? breakStart = null,
        string? breakEnd = null)
        => new(
            dayIndex,
            IsWorking: true,
            Time(start),
            Time(end),
            breakMinutes,
            breakStart is null ? null : Time(breakStart),
            breakEnd is null ? null : Time(breakEnd));

    public Employment Employ(DateOnly? hired = null, Guid? departmentId = null, Guid? positionId = null)
    {
        var employment = Employment.Hire(EmployeeId, Guid.CreateVersion7(), hired ?? LongAgo, departmentId, positionId).Value;
        _employments.Add(employment);
        return employment;
    }

    public ScheduleAssignment Assign(
        WorkSchedule schedule,
        ScheduleScope scope = ScheduleScope.Default,
        Guid? targetId = null,
        DateOnly? from = null,
        DateOnly? to = null)
    {
        if (!_schedules.Contains(schedule))
        {
            _schedules.Add(schedule);
        }

        var assignment = ScheduleAssignment.Assign(schedule.Id, scope, targetId, from ?? LongAgo, to).Value;
        _assignments.Add(assignment);
        return assignment;
    }

    public Department Department(string name, Department? parent = null)
    {
        var department = Domain.Organization.Department.Create(name, parent?.Id).Value;
        _departments.Add(department);
        return department;
    }

    public void In(DateOnly date, string time) => Pass(date, time, PassDirection.In);

    public void Out(DateOnly date, string time) => Pass(date, time, PassDirection.Out);

    public void Unknown(DateOnly date, string time) => Pass(date, time, PassDirection.Unknown);

    public void Pass(DateOnly date, string time, PassDirection direction)
        => _passes.Add(new PassReading(Guid.CreateVersion7(), EmployeeId, At(date, time), direction));

    public void Pass(PassReading pass) => _passes.Add(pass);

    /// <summary>Bir kunda ketma-ket kirish, chiqish, kirish… — SPEC misollaridagi yozuv shakli.</summary>
    public void Alternating(DateOnly date, params string[] times)
    {
        for (var i = 0; i < times.Length; i++)
        {
            Pass(date, times[i], i % 2 == 0 ? PassDirection.In : PassDirection.Out);
        }
    }

    /// <summary>
    /// Bir kunda yo'nalishi NOMA'LUM o'tishlar — ishlab chiqarishda agent aynan shunday yuboradi
    /// (SPEC-agent H8): yo'nalishni server navbat bilan beradi.
    /// </summary>
    public void Unknowns(DateOnly date, params string[] times)
    {
        foreach (var time in times)
        {
            Unknown(date, time);
        }
    }

    /// <summary>Yillik to'rning katagi — jadvalning shu SANAGA yozilgan kuni.</summary>
    public WorkScheduleDate ScheduleDate(WorkSchedule schedule, WorkScheduleDateInput input)
    {
        var created = WorkScheduleDate.Create(schedule.Id, input);
        created.IsSuccess.ShouldBeTrue("sahnadagi katak domen tekshiruvidan o'tishi kerak");
        _dates.Add(created.Value);
        return created.Value;
    }

    private readonly List<EmployeeDayOverride> _overrides = [];

    /// <summary>Tasdiqlangan jadval so'rovining kuni (SPEC-verifikatsiya-jadval-sorovi §C2).</summary>
    /// <param name="date">Sana.</param>
    /// <param name="start">Boshlanish; <c>null</c> — dam olish kuni.</param>
    /// <param name="end">Tugash.</param>
    /// <param name="breakStart">Tanaffus oynasi boshi.</param>
    /// <param name="breakEnd">Tanaffus oynasi oxiri.</param>
    /// <param name="breakMinutes">Ayiriladigan tanaffus; <c>null</c> — oynaning uzunligi (Verifix formasidagidek).</param>
    public AttendanceScenario Override(
        DateOnly date,
        string? start,
        string? end,
        string? breakStart = null,
        string? breakEnd = null,
        int? breakMinutes = null)
        => Override(date, start, end, isFlexible: false, requiredMinutes: null, breakStart, breakEnd, breakMinutes);

    /// <summary>
    /// Tasdiqlangan jadval so'rovining kuni — «Erkin ish jadvali» belgisi bilan (SPEC-erkin-jadval §4).
    /// Norma berilmasa — bazadagidek sukut yoziladi (smena − tanaffus).
    /// </summary>
    public AttendanceScenario Override(
        DateOnly date,
        string? start,
        string? end,
        bool isFlexible,
        int? requiredMinutes = null,
        string? breakStart = null,
        string? breakEnd = null,
        int? breakMinutes = null)
    {
        var windowMinutes = breakStart is not null && breakEnd is not null
            ? (int)(Time(breakEnd) - Time(breakStart)).TotalMinutes
            : 0;

        var working = start is not null;
        var deducted = working ? breakMinutes ?? windowMinutes : 0;
        var flexible = working && isFlexible;

        _overrides.Add(new EmployeeDayOverride(
            EmployeeId,
            date,
            working,
            start is null ? null : Time(start),
            end is null ? null : Time(end),
            breakStart is null ? null : Time(breakStart),
            breakEnd is null ? null : Time(breakEnd),
            deducted,
            Guid.CreateVersion7(),
            IsFlexible: flexible,
            RequiredMinutes: flexible
                ? requiredMinutes ?? WorkScheduleDay.MaxRequiredMinutes(Time(start!), Time(end!), deducted)
                : null));
        return this;
    }

    public ScheduleResolver Resolver() => new(_assignments, _schedules, new DepartmentTree(_departments), _dates, _overrides);

    /// <summary>Sahnaning «bugun»ini belgilaydi — kelajak kunlar qoidasi uchun.</summary>
    public AttendanceScenario Today(DateOnly today)
    {
        _today = today;
        return this;
    }

    /// <summary>
    /// Sahnaning «hozir»ini Toshkent soatida belgilaydi — «bugun» ham shu lahzaning sanasiga o'tadi,
    /// chunki ishlab chiqarishda ikkalasi bitta soatdan olinadi.
    /// </summary>
    public AttendanceScenario Now(DateOnly date, string time)
    {
        _now = At(date, time);
        _today = date;
        return this;
    }

    /// <summary>Bitta sanani hisoblaydi.</summary>
    public ComputedAttendanceDay Day(DateOnly date) => Days(date).ShouldHaveSingleItem();

    /// <summary>
    /// Bir kunlik sahnaning juftlash natijasi (SPEC §5.5 1–5-qadamlar) — kun yozuviga tushmaydigan
    /// oraliq sonlari uchun (yopilgan yig'indi, oynada binoda bo'lgan vaqt). Sahnadagi HAMMA o'tish
    /// shu kunniki deb olinadi.
    /// </summary>
    public PairingResult Pairing(DateOnly date)
    {
        var employment = PrimaryEmployment.Select(_employments, date).ShouldNotBeNull();
        return PassPairing.Pair(_passes, Resolver().Resolve(employment, date)?.Day);
    }

    /// <summary>Bir nechta sanani BITTA chaqiruvda hisoblaydi — qayta hisoblash ishi aynan shunday chaqiradi.</summary>
    public IReadOnlyList<ComputedAttendanceDay> Days(params DateOnly[] dates)
        => new AttendanceDayBuilder(Resolver(), _today, _now).Build(EmployeeId, _employments, _passes, dates);

    /// <summary>
    /// Yuklovchi kabi hisoblaydi: faqat <see cref="AttendanceDayBuilder.PassDatesNeeded"/> oralig'idagi
    /// o'tishlar beriladi — natija qaysi oraliq hisoblanayotganiga bog'liq emasligini tekshirish uchun.
    /// </summary>
    public IReadOnlyList<ComputedAttendanceDay> DaysAsLoaded(params DateOnly[] dates)
    {
        var (from, to) = AttendanceDayBuilder.PassDatesNeeded(dates.Min(), dates.Max());
        var loaded = _passes.Where(pass => pass.LocalDate >= from && pass.LocalDate <= to).ToList();

        return new AttendanceDayBuilder(Resolver(), _today, _now).Build(EmployeeId, _employments, loaded, dates);
    }

    /// <summary>Tasdiqlangan yo'qlik so'rovi qamragan sanalar bilan hisoblaydi.</summary>
    public IReadOnlyList<ComputedAttendanceDay> DaysExcused(IReadOnlySet<DateOnly> excused, params DateOnly[] dates)
        => new AttendanceDayBuilder(Resolver(), _today, _now).Build(EmployeeId, _employments, _passes, dates, excused);

    private readonly List<WorkCredit> _credits = [];

    /// <summary>
    /// «Ish vaqti hisoblanadigan» tasdiqlangan so'rov (SPEC-so'rovlar §B3 AMENDED). Vaqt
    /// berilmasa — butun kun(lar).
    /// </summary>
    public AttendanceScenario WorkCredit(DateOnly from, DateOnly? to = null, string? fromTime = null, string? toTime = null)
    {
        _credits.Add(new WorkCredit(
            from,
            to ?? from,
            fromTime is null ? null : Time(fromTime),
            toTime is null ? null : Time(toTime)));
        return this;
    }

    /// <summary>
    /// Ishlab chiqarishdagidek hisoblaydi: «ish vaqti» so'rovlari ham beriladi, ularning sanalari
    /// esa «oqlangan» sanalar qatorida ham turadi (har qanday tasdiqlangan so'rov kabi).
    /// </summary>
    public IReadOnlyList<ComputedAttendanceDay> DaysCredited(params DateOnly[] dates)
    {
        var excused = _credits
            .SelectMany(credit => Enumerable
                .Range(0, credit.ToDate.DayNumber - credit.FromDate.DayNumber + 1)
                .Select(credit.FromDate.AddDays))
            .ToHashSet();

        return new AttendanceDayBuilder(Resolver(), _today, _now)
            .Build(EmployeeId, _employments, _passes, dates, excused, _credits);
    }

    /// <summary>Bitta sanani «ish vaqti» so'rovlari bilan hisoblaydi.</summary>
    public ComputedAttendanceDay DayCredited(DateOnly date) => DaysCredited(date).ShouldHaveSingleItem();
}
