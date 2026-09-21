using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Schedules;

/// <summary>Jadvalning yillik to'ri (<c>GET /schedules/{id}/year</c>).</summary>
/// <param name="ScheduleId">Jadval.</param>
/// <param name="Year">Yil.</param>
public sealed record GetScheduleYearQuery(Guid ScheduleId, int Year);

/// <summary>
/// Yillik to'rning kataklarini yozadi yoki tozalaydi
/// (<c>PUT /schedules/{id}/dates</c>).
/// </summary>
/// <remarks>
/// Bitta buyruq bilan BIR NECHTA sana yoziladi: ekranda bitta katak ham,
/// oyning butun qatori ham, butun yil ham bir xil amal — va ular uchun
/// uchta endpoint yozish uchta xil xatti-harakat degani bo'lardi.
/// </remarks>
/// <param name="ScheduleId">Jadval.</param>
/// <param name="Dates">Qaysi sanalar.</param>
/// <param name="Reset">Kataklar O'CHIRILADIMI (kun naqshga qaytadi).</param>
/// <param name="IsWorking">Ish kunimi (<paramref name="Reset"/> bo'lsa e'tiborga olinmaydi).</param>
/// <param name="StartTime">Boshlanish.</param>
/// <param name="EndTime">Tugash.</param>
/// <param name="BreakMinutes">Tanaffus.</param>
/// <param name="BreakStart">Tanaffus oynasining boshi.</param>
/// <param name="BreakEnd">Tanaffus oynasining oxiri.</param>
public sealed record SetScheduleDatesCommand(
    Guid ScheduleId,
    IReadOnlyList<DateOnly> Dates,
    bool Reset = false,
    bool IsWorking = true,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int BreakMinutes = 0,
    TimeOnly? BreakStart = null,
    TimeOnly? BreakEnd = null);

/// <summary><see cref="GetScheduleYearQuery"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// Katak qiymati <see cref="DayPlan"/> orqali hisoblanadi — tabel va
/// panel ishlatadigan AYNAN o'sha kod. Shu sababli to'rda ko'ringan soat
/// bilan tabeldagi reja hech qachon farq qila olmaydi.
/// </para>
/// <para>
/// Yilning HAR BIR kuni qaytadi, tirqishsiz (grafik o'z o'qini o'zi
/// to'qib chiqarmasligi kerak — davr grafigidagi bilan bir xil qoida).
/// </para>
/// </remarks>
public sealed class GetScheduleYearQueryHandler
{
    /// <summary>Eng erta va eng kech yil — qo'lda yozilgan manzildan himoya.</summary>
    private const int MinYear = 2000;
    private const int MaxYear = 2100;

    private readonly IWorkScheduleRepository _schedules;
    private readonly IWorkScheduleDateRepository _dates;

    public GetScheduleYearQueryHandler(
        IWorkScheduleRepository schedules,
        IWorkScheduleDateRepository dates)
    {
        _schedules = schedules;
        _dates = dates;
    }

    public async Task<Result<ScheduleYearDto>> HandleAsync(
        GetScheduleYearQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Year is < MinYear or > MaxYear)
        {
            return Error.Validation("davomat.jadval_yili_notogri", "Yil noto'g'ri ko'rsatilgan.");
        }

        var schedule = await _schedules.GetByIdAsync(query.ScheduleId, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return AttendanceErrors.ScheduleNotFound;
        }

        var from = new DateOnly(query.Year, 1, 1);
        var to = new DateOnly(query.Year, 12, 31);

        var overrides = (await _dates
                .ListAsync(schedule.Id, from, to, cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(date => date.Date);

        var days = new List<ScheduleYearDayDto>(to.DayNumber - from.DayNumber + 1);

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var exact = overrides.GetValueOrDefault(date);
            var plan = DayPlan.For(schedule, date, exact);

            days.Add(new ScheduleYearDayDto(
                date,
                plan.IsWorking,
                exact?.StartTime ?? PatternTime(schedule, date, start: true),
                exact?.EndTime ?? PatternTime(schedule, date, start: false),
                plan.BreakMinutes,
                plan.PlannedMinutes,
                exact is null ? ScheduleDaySource.Pattern : ScheduleDaySource.Date));
        }

        return new ScheduleYearDto(schedule.Id, schedule.Name, query.Year, days);
    }

    /// <summary>Naqshdagi vaqt — katak yozilmagan kun uchun.</summary>
    /// <remarks>
    /// <see cref="DayPlan"/> vaqtni LAHZA qilib beradi (UTC), ekranga esa
    /// Toshkent SOATI kerak. Naqshdan o'qish shu sababli: lahzani qayta
    /// mintaqaga o'girish uchinchi o'girish nuqtasi bo'lardi.
    /// </remarks>
    private static TimeOnly? PatternTime(WorkSchedule schedule, DateOnly date, bool start)
    {
        if (schedule.Kind != WorkScheduleKind.Weekly)
        {
            return null;
        }

        var dayIndex = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
        var day = schedule.Days.FirstOrDefault(item => item.DayIndex == dayIndex);

        return start ? day?.StartTime : day?.EndTime;
    }
}

/// <summary><see cref="SetScheduleDatesCommand"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// ⭐ <b>Yozuvdan keyin kunlar QAYTA HISOBLANADI</b> (SPEC-jadval-to'ri §B3):
/// jadval o'zgarishi kechagi tabelni ham o'zgartiradi. Tabelning o'zi
/// qo'lda tahrirlanmaydi — modulda bunday yo'l umuman yo'q.
/// </para>
/// <para>
/// ⚠️ Qayta hisoblash faqat BUGUNGACHA bo'lgan kunlar uchun: kelajakdagi
/// kunning saqlangan yozuvi yo'q (tabel uni o'qiyotganda hosil qiladi),
/// ya'ni butun yilni to'ldirish 365 kunlik og'ir vazifa tug'dirmaydi.
/// </para>
/// </remarks>
public sealed class SetScheduleDatesCommandHandler
{
    /// <summary>Bitta chaqiruvdagi eng ko'p sana — butun yil + zaxira.</summary>
    private const int MaxDates = 400;

    private readonly IWorkScheduleRepository _schedules;
    private readonly IWorkScheduleDateRepository _dates;
    private readonly IBackgroundJobQueue _jobs;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SetScheduleDatesCommandHandler(
        IWorkScheduleRepository schedules,
        IWorkScheduleDateRepository dates,
        IBackgroundJobQueue jobs,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _schedules = schedules;
        _dates = dates;
        _jobs = jobs;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        SetScheduleDatesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var wanted = command.Dates.Distinct().OrderBy(date => date).ToList();

        if (wanted.Count == 0)
        {
            return Error.Validation("davomat.jadval_sana_bosh", "Kamida bitta sana ko'rsatilishi shart.");
        }

        if (wanted.Count > MaxDates)
        {
            return Error.Validation(
                "davomat.jadval_sana_kop",
                $"Bitta so'rovda {MaxDates} tadan ko'p sana bo'lishi mumkin emas.");
        }

        var schedule = await _schedules.GetByIdAsync(command.ScheduleId, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return AttendanceErrors.ScheduleNotFound;
        }

        var existing = (await _dates
                .ListForUpdateAsync(schedule.Id, wanted, cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(date => date.Date);

        if (command.Reset)
        {
            foreach (var row in existing.Values)
            {
                _dates.Remove(row);
            }
        }
        else
        {
            foreach (var date in wanted)
            {
                var input = new WorkScheduleDateInput(
                    date,
                    command.IsWorking,
                    command.StartTime,
                    command.EndTime,
                    command.BreakMinutes,
                    command.BreakStart,
                    command.BreakEnd);

                if (existing.TryGetValue(date, out var row))
                {
                    var updated = row.Update(input);
                    if (updated.IsFailure)
                    {
                        return updated.Error;
                    }

                    continue;
                }

                var created = WorkScheduleDate.Create(schedule.Id, input);
                if (created.IsFailure)
                {
                    return created.Error;
                }

                _dates.Add(created.Value);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await EnqueueRecomputeAsync(wanted, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>O'zgargan kunlarni qayta hisoblashga qo'yadi — faqat o'tgan kunlar.</summary>
    private async Task EnqueueRecomputeAsync(List<DateOnly> dates, CancellationToken cancellationToken)
    {
        var today = _clock.Today;
        var past = dates.Where(date => date <= today).ToList();

        if (past.Count == 0)
        {
            return;
        }

        /*
          Xodim ko'rsatilmaydi (`employeeId: null`): jadval bo'limga
          biriktirilgan bo'lishi mumkin va u kimlarga tegishini hal
          qiluvchining o'zi biladi. Oraliq — eng erta va eng kech sana:
          orasidagi tegilmagan kunlar qayta hisoblanadi, lekin natija
          o'zgarmaydi (hisob determinlashgan).
        */
        // Ifoda daraxtida `^1` ishlatib bo'lmaydi (Hangfire chaqiruvni
        // shunday o'qiydi) — chegaralar oldindan hisoblanadi.
        var first = past[0];
        var last = past[past.Count - 1];

        await _jobs
            .EnqueueAsync<AttendanceRecomputeRangeJob>(job => job.RunAsync(
                first,
                last,
                null,
                CancellationToken.None))
            .ConfigureAwait(false);
    }
}
