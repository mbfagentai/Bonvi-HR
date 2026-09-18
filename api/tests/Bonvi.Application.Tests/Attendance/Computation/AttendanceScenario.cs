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

    public ScheduleResolver Resolver() => new(_assignments, _schedules, new DepartmentTree(_departments));

    /// <summary>Bitta sanani hisoblaydi.</summary>
    public ComputedAttendanceDay Day(DateOnly date) => Days(date).ShouldHaveSingleItem();

    /// <summary>Bir nechta sanani BITTA chaqiruvda hisoblaydi — qayta hisoblash ishi aynan shunday chaqiradi.</summary>
    public IReadOnlyList<ComputedAttendanceDay> Days(params DateOnly[] dates)
        => new AttendanceDayBuilder(Resolver()).Build(EmployeeId, _employments, _passes, dates);
}
