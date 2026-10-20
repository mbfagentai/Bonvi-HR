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
/// <param name="IsFlexible">«Erkin ish jadvali» (SPEC-erkin-jadval §5.1).</param>
/// <param name="RequiredMinutes">«Ish vaqti»; <c>null</c> — sukut (smena − tanaffus).</param>
public sealed record SetScheduleDatesCommand(
    Guid ScheduleId,
    IReadOnlyList<DateOnly> Dates,
    bool Reset = false,
    bool IsWorking = true,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int BreakMinutes = 0,
    TimeOnly? BreakStart = null,
    TimeOnly? BreakEnd = null,
    bool IsFlexible = false,
    int? RequiredMinutes = null);

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

            /*
              ⚠️ Vaqtlar katak BOR bo'lsa faqat katakdan: `exact?.X ?? naqsh`
              oynasiz katakka naqshning oynasini ulab ko'rsatardi — ekran
              «tanaffus ayiriladi» deb, hisob esa ayirmasdi (§5.5.1).
            */
            var pattern = exact is null ? PatternDay(schedule, date) : null;

            days.Add(new ScheduleYearDayDto(
                date,
                plan.IsWorking,
                exact is null ? pattern?.StartTime : exact.StartTime,
                exact is null ? pattern?.EndTime : exact.EndTime,
                plan.BreakMinutes,
                exact is null ? pattern?.BreakStart : exact.BreakStart,
                exact is null ? pattern?.BreakEnd : exact.BreakEnd,
                plan.PlannedMinutes,
                exact is null ? ScheduleDaySource.Pattern : ScheduleDaySource.Date,
                // Erkinlik ham vaqtlar kabi — katak bo'lsa faqat katakdan (SPEC-erkin-jadval §4).
                plan.IsFlexible,
                plan.IsFlexible ? (exact is null ? pattern?.RequiredMinutes : exact.RequiredMinutes) : null));
        }

        return new ScheduleYearDto(schedule.Id, schedule.Name, query.Year, days);
    }

    /// <summary>Naqshdagi kun — katak yozilmagan sana uchun.</summary>
    /// <remarks>
    /// <see cref="DayPlan"/> vaqtni LAHZA qilib beradi (UTC), ekranga esa
    /// Toshkent SOATI kerak. Naqshdan o'qish shu sababli: lahzani qayta
    /// mintaqaga o'girish uchinchi o'girish nuqtasi bo'lardi.
    /// </remarks>
    private static WorkScheduleDay? PatternDay(WorkSchedule schedule, DateOnly date)
    {
        if (schedule.Kind != WorkScheduleKind.Weekly)
        {
            return null;
        }

        var dayIndex = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

        return schedule.Days.FirstOrDefault(item => item.DayIndex == dayIndex);
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
/// ⚠️ Qayta hisoblash FAQAT shu jadval tushadigan xodimlarning FAQAT o'zgargan
/// sanalari (±1 qo'shni bilan) uchun — <see cref="AttendanceScheduleDatesJob"/>.
/// Kelajak sanalar ham kiradi: ularda tasdiqlangan so'rovning «Sababli» qatori
/// turishi mumkin. Butun yilni to'ldirish shu tufayli butun korxonani emas,
/// jadvaldagi odamlarni chorak oynalarda qayta yozadi.
/// </para>
/// </remarks>
public sealed class SetScheduleDatesCommandHandler
{
    /// <summary>Bitta chaqiruvdagi eng ko'p sana — butun yil + zaxira.</summary>
    private const int MaxDates = 400;

    private readonly IWorkScheduleRepository _schedules;
    private readonly IWorkScheduleDateRepository _dates;
    private readonly IBackgroundJobQueue _jobs;
    private readonly IUnitOfWork _unitOfWork;

    public SetScheduleDatesCommandHandler(
        IWorkScheduleRepository schedules,
        IWorkScheduleDateRepository dates,
        IBackgroundJobQueue jobs,
        IUnitOfWork unitOfWork)
    {
        _schedules = schedules;
        _dates = dates;
        _jobs = jobs;
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
                    command.BreakEnd,
                    command.IsFlexible,
                    command.RequiredMinutes);

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

        await EnqueueRecomputeAsync(schedule.Id, wanted).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>O'zgargan kunlarni shu jadvaldagi odamlar uchun qayta hisoblashga qo'yadi.</summary>
    /// <remarks>
    /// Kimga tegishini vazifaning O'ZI hal qiladi (<see cref="AttendanceScheduleDatesJob"/>):
    /// jadval bo'limga yoki lavozimga biriktirilgan bo'lishi mumkin, va navbatda kutgan
    /// vaqt ichida biriktirma o'zgarishi ham mumkin. Sanalar esa ANIQ ro'yxat —
    /// tarqoq kataklar oralig'i emas (sabab vazifa izohida).
    /// </remarks>
    private async Task EnqueueRecomputeAsync(Guid scheduleId, List<DateOnly> dates)
    {
        // Ifoda daraxtida kolleksiya ifodasi yoki metod chaqiruvi emas, tayyor
        // qiymat bo'lsin — Hangfire argumentni shu yerda o'qib saqlaydi.
        var changed = dates.ToArray();

        await _jobs
            .EnqueueAsync<AttendanceScheduleDatesJob>(job => job.RunAsync(
                scheduleId,
                changed,
                CancellationToken.None))
            .ConfigureAwait(false);
    }
}
