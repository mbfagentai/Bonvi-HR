using Bonvi.Application.Attendance.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Schedules;

/// <summary>
/// Yilni haftalik naqshdan to'ldiradi (<c>POST /schedules/{id}/year/fill</c>,
/// SPEC-jadval-to'ri §D).
/// </summary>
/// <param name="ScheduleId">Jadval.</param>
/// <param name="Year">Qaysi yil.</param>
public sealed record FillScheduleYearCommand(Guid ScheduleId, int Year);

/// <summary><see cref="FillScheduleYearCommand"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// ⭐ <b>Faqat BO'SH sanalar yoziladi.</b> Yilning katagi yozilmagan har bir
/// kuniga naqshdagi hafta kunining qiymati (vaqtlar, tanaffus va uning
/// OYNASI) nusxalanadi; qo'lda yozilgan katak — bayram, qisqa kun —
/// TEGILMAYDI. «Hammasini naqshdan qaytadan yozish» kerak bo'lsa, avval
/// «Yilni tozalash» (u o'chiruvchi amal va tasdiq bilan ketadi).
/// </para>
/// <para>
/// <b>Nima o'zgaradi.</b> Hisob — HECH NARSA: yozilgan katak naqshdagi kun
/// bilan aynan bir xil, <c>DayPlan.For</c> esa ikkala manbani bitta
/// metoddan quradi. Shu sababli bu amal qayta hisoblashni NAVBATGA
/// QO'YMAYDI — natija o'zgarmaydigan 365 kunni qayta hisoblash bekorchi
/// yuk bo'lardi. O'zgaradigani — kelajak: shu yil endi haftalik naqshga
/// ERGASHMAYDI, naqsh keyin tahrirlansa ham bu yil o'z qiymatida qoladi
/// (Verifix'da yil to'ri aynan shunday ishlaydi).
/// </para>
/// </remarks>
public sealed class FillScheduleYearCommandHandler
{
    /// <summary>Yil chegaralari — <see cref="GetScheduleYearQueryHandler"/> dagi bilan bir xil.</summary>
    private const int MinYear = 2000;
    private const int MaxYear = 2100;

    private readonly IWorkScheduleRepository _schedules;
    private readonly IWorkScheduleDateRepository _dates;
    private readonly IUnitOfWork _unitOfWork;

    public FillScheduleYearCommandHandler(
        IWorkScheduleRepository schedules,
        IWorkScheduleDateRepository dates,
        IUnitOfWork unitOfWork)
    {
        _schedules = schedules;
        _dates = dates;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        FillScheduleYearCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Year is < MinYear or > MaxYear)
        {
            return Error.Validation("davomat.jadval_yili_notogri", "Yil noto'g'ri ko'rsatilgan.");
        }

        var schedule = await _schedules.GetByIdAsync(command.ScheduleId, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return AttendanceErrors.ScheduleNotFound;
        }

        // Aylanma jadvalning hafta kuni yo'q — nusxalanadigan naqsh ham yo'q.
        if (schedule.Kind != WorkScheduleKind.Weekly)
        {
            return Error.Validation(
                "davomat.jadval_turi_qollanmaydi",
                "Hozircha faqat haftalik jadval qo'llab-quvvatlanadi.");
        }

        var from = new DateOnly(command.Year, 1, 1);
        var to = new DateOnly(command.Year, 12, 31);

        var written = (await _dates
                .ListAsync(schedule.Id, from, to, cancellationToken)
                .ConfigureAwait(false))
            .Select(date => date.Date)
            .ToHashSet();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (written.Contains(date))
            {
                continue;
            }

            var dayIndex = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            var day = schedule.Days.FirstOrDefault(item => item.DayIndex == dayIndex)
                ?? throw new InvalidOperationException(
                    $"«{schedule.Name}» jadvalining {dayIndex}-kuni yuklanmagan. Jadval kunlari bilan birga o'qilishi shart.");

            var created = WorkScheduleDate.Create(
                schedule.Id,
                new WorkScheduleDateInput(
                    date,
                    day.IsWorking,
                    day.StartTime,
                    day.EndTime,
                    day.BreakMinutes,
                    day.BreakStart,
                    day.BreakEnd));

            // Naqsh domen tekshiruvidan o'tib saqlangan — bu yerda rad etilishi
            // bazaga qo'lda yozilgan buzuq kunni bildiradi va jim o'tkazilmaydi.
            if (created.IsFailure)
            {
                return created.Error;
            }

            _dates.Add(created.Value);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
