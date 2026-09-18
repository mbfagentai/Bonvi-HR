using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Attendance.Computation;

/// <summary>
/// Bitta (xodim, sana) uchun hisoblangan kun.
/// </summary>
/// <remarks>
/// <see cref="Facts"/> to'g'ridan-to'g'ri <see cref="AttendanceDay.Compute"/>
/// yoki <see cref="AttendanceDay.Recompute"/> ga beriladi. Qolgan maydonlar
/// bazaga tushmaydi — ular «nega aynan shu son» degan savolga javob
/// (<c>GET /schedules/resolve</c>, tabel katagidagi izoh).
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="LocalDate">Smena boshlangan Toshkent sanasi.</param>
/// <param name="Facts">Yoziladigan natija.</param>
/// <param name="ShouldStore">
/// Shu kun uchun <c>attendance_days</c> qatori bo'lishi kerakmi (SPEC §5.4). <c>false</c> bo'lsa
/// katak tabel so'rovida hosil qilinadi (§5.7.1) va <see cref="Facts"/> faqat ma'lumot uchun.
/// </param>
/// <param name="ScheduleTier">Jadvalni bergan pog'ona; jadval topilmasa <c>null</c>.</param>
/// <param name="HasBreakWindow">
/// Kun rejasida tanaffus oynasi bormi. ⚠️ Saqlanmaydi: «oyna belgilanmagan» va «oyna bor, lekin
/// ushlanadigan narsa yo'q» saqlangan ikki ustunda bir xil (0, 0) ko'rinadi — katak izohi ularni
/// ajratishi kerak bo'lsa, bu bayroq ham qatorga yozilishi kerak.
/// </param>
public sealed record ComputedAttendanceDay(
    Guid EmployeeId,
    DateOnly LocalDate,
    AttendanceDayFacts Facts,
    bool ShouldStore,
    ScheduleScope? ScheduleTier,
    bool HasBreakWindow);

/// <summary>
/// Xodimning kunlarini o'tishlar, munosabatlar va jadvallardan hisoblaydi — SPEC §5.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Sof mantiq.</b> Baza, HTTP, fon vazifasi yo'q: chaqiruvchi
/// ma'lumotni yuklaydi, bu sinf faqat hisoblaydi. Shuning uchun har
/// qanday hisob-kitob tortishuvi bazasiz testda qayta o'ynatiladi — tabel
/// esa aynan HR odamlar bilan tortishadigan son.
/// </para>
/// <para>
/// <b>Toza funksiya.</b> Bir xil kirish — bir xil <see cref="AttendanceDayFacts"/>
/// (yozuv tengligi bilan). Qayta hisoblashning idempotentligi (SPEC §9.3)
/// shunga tayanadi; hisoblash vaqti (<c>computed_at</c>) ataylab bu yerga
/// kirmaydi — uni chaqiruvchi <c>IClock</c> dan beradi.
/// </para>
/// <para>
/// ⚠️ <b>Yuklash shartnomasi.</b> D sanani hisoblash uchun
/// <c>local_date</c> i [D − 1, D + 1] oralig'idagi BARCHA o'tishlar
/// kerak (tungi smenaning ertalabki chiqishi ertangi sanada yotadi) —
/// <see cref="PassDatesNeeded"/>. Jadval biriktirmalari esa
/// [D − 2, D + 2] ni qamrashi kerak: qo'shni sanadagi o'tishning kuni
/// uning o'z qo'shnilarining rejasiga qarab aniqlanadi. Kichik jadval,
/// shuning uchun eng oddiysi — tirik biriktirmalarning hammasi.
/// </para>
/// </remarks>
public sealed class AttendanceDayBuilder
{
    private readonly ScheduleResolver _schedules;

    /// <summary>Hisoblovchini to'plamning jadval hal qiluvchisi ustida quradi.</summary>
    /// <param name="schedules">Bir to'plam uchun qurilgan hal qiluvchi.</param>
    public AttendanceDayBuilder(ScheduleResolver schedules)
    {
        ArgumentNullException.ThrowIfNull(schedules);

        _schedules = schedules;
    }

    /// <summary>[from, to] kunlarini hisoblash uchun qaysi <c>local_date</c> dagi o'tishlar yuklanishi shart.</summary>
    /// <remarks>
    /// Shartnoma izohda emas, KODDA: yuklovchi oraliqni shu yerdan olsa,
    /// «tungi smenaning chiqishini yuklash esdan chiqdi» degan xato
    /// yozilmaydi — u butun smenani <c>MissingExit</c> qilib ko'rsatardi.
    /// </remarks>
    /// <param name="from">Hisoblanadigan birinchi sana.</param>
    /// <param name="to">Hisoblanadigan oxirgi sana.</param>
    public static (DateOnly From, DateOnly To) PassDatesNeeded(DateOnly from, DateOnly to)
        => (from.AddDays(-1), to.AddDays(1));

    /// <summary>Bitta xodimning so'ralgan sanalarini hisoblaydi.</summary>
    /// <remarks>
    /// <para>
    /// Har bir so'ralgan sana uchun BITTA natija qaytadi, saqlanishi kerak
    /// bo'lmasa ham (<see cref="ComputedAttendanceDay.ShouldStore"/>). Yozuv
    /// bor, lekin endi bo'lmasligi kerak bo'lgan kunni (masalan jadval dam
    /// olish kuniga o'zgartirilgan) chaqiruvchi shu bayroqdan biladi.
    /// </para>
    /// <para>
    /// ⚠️ Boshqa xodimning o'tishi yoki munosabati berilsa — ISTISNO.
    /// Bog'lanmagan o'tish ham shu jumladan: u hech qaysi kunga tushmaydi
    /// (SPEC §5.7), va uni jimgina tashlab ketish chaqiruvchining xatosini
    /// yashirardi, qabul qilish esa begona odamning soatlarini shu xodimga
    /// yozardi.
    /// </para>
    /// </remarks>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="employments">Xodimning BARCHA munosabatlari (tugaganlari ham).</param>
    /// <param name="passes">Xodimning o'tishlari — <see cref="PassDatesNeeded"/> oralig'ida.</param>
    /// <param name="dates">Hisoblanadigan sanalar.</param>
    public IReadOnlyList<ComputedAttendanceDay> Build(
        Guid employeeId,
        IReadOnlyCollection<Employment> employments,
        IReadOnlyCollection<PassReading> passes,
        IEnumerable<DateOnly> dates)
    {
        ArgumentNullException.ThrowIfNull(employments);
        ArgumentNullException.ThrowIfNull(passes);
        ArgumentNullException.ThrowIfNull(dates);

        if (employments.Any(employment => employment.EmployeeId != employeeId))
        {
            throw new ArgumentException("Boshqa xodimning mehnat munosabati berildi.", nameof(employments));
        }

        if (passes.Any(pass => pass.EmployeeId != employeeId))
        {
            throw new ArgumentException("Boshqa xodimning yoki bog'lanmagan o'tish berildi.", nameof(passes));
        }

        var calendar = new EmployeeCalendar(_schedules, employments);

        var passesByDate = passes
            .GroupBy(pass => PassAttribution.DateFor(pass, calendar.PlanFor))
            .ToDictionary(group => group.Key, group => group.ToList());

        return dates
            .Distinct()
            .Order()
            .Select(date => BuildDay(
                employeeId,
                date,
                calendar.For(date),
                passesByDate.GetValueOrDefault(date) ?? []))
            .ToList();
    }

    /// <summary>§5.5 6–10-qadamlar, §5.6 holati va §5.4 saqlash qoidasi.</summary>
    private static ComputedAttendanceDay BuildDay(
        Guid employeeId,
        DateOnly date,
        DayContext context,
        List<PassReading> passes)
    {
        var employment = context.Employment;
        var schedule = context.Schedule;
        var plan = schedule?.Day;
        var pairing = PassPairing.Pair(passes, plan);

        var planned = 0;
        var late = 0;
        var early = 0;

        if (plan is { IsWorking: true, PlanStartAt: { } planStart, PlanEndAt: { } planEnd })
        {
            planned = plan.PlannedMinutes;

            if (pairing.FirstInAt is { } firstIn)
            {
                late = WholeMinutes.AtLeastZero(firstIn - planStart.AddMinutes(plan.GraceInMinutes));
            }

            // Qachon ketgani noma'lum bo'lsa, «erta ketdi» deb ayblanmaydi.
            if (!pairing.MissingExit && pairing.LastOutAt is { } lastOut)
            {
                early = WholeMinutes.AtLeastZero(planEnd.AddMinutes(-plan.GraceOutMinutes) - lastOut);
            }
        }

        var status = StatusOf(employment, plan, pairing);

        var facts = new AttendanceDayFacts(
            EmploymentId: employment?.Id,
            LegalEntityId: employment?.LegalEntityId,
            DepartmentId: employment?.DepartmentId,
            PositionId: employment?.PositionId,
            WorkScheduleId: schedule?.Schedule.Id,
            PlanStartAt: plan?.PlanStartAt,
            PlanEndAt: plan?.PlanEndAt,
            FirstInAt: pairing.FirstInAt,
            LastOutAt: pairing.LastOutAt,
            PlannedMinutes: planned,
            WorkedMinutes: pairing.WorkedMinutes,
            LateMinutes: late,
            EarlyLeaveMinutes: early,
            // Ortiqcha ish faqat JADVAL bor kunda: normasi noma'lum kunda
            // (jadval yoki munosabat yo'q) har bir daqiqa «ortiqcha» bo'lib
            // chiqardi va xulosa hisobotidagi ortiqcha ish ustunini shu
            // kunlar shishirib yuborardi. Dam olish kunida ishlash esa
            // haqiqiy ortiqcha ish — u yerda reja bor, faqat nolga teng.
            OvertimeMinutes: plan is null ? 0 : Math.Max(0, pairing.WorkedMinutes - planned),
            BreakTakenMinutes: pairing.BreakTakenMinutes,
            BreakDeductionMinutes: pairing.BreakDeductionMinutes,
            PassCount: pairing.PassCount,
            IgnoredPassCount: pairing.IgnoredPassCount,
            Status: status);

        // §5.4: o'tish bor — YOKI munosabat bor va jadval shu kunni ish kuni deydi.
        // Ish kunidagi reja faqat munosabat va jadval topilganda bo'ladi.
        var shouldStore = pairing.PassCount > 0 || plan is { IsWorking: true };

        return new ComputedAttendanceDay(
            employeeId,
            date,
            facts,
            shouldStore,
            schedule?.Tier,
            plan?.HasBreakWindow ?? false);
    }

    /// <summary>SPEC §5.6 — birinchi mos kelgani yutadi.</summary>
    /// <remarks>
    /// <c>DayOff</c> §5.6 ro'yxatida yo'q, chunki bunday kun uchun qator
    /// yaratilmaydi (§5.4). Bu yerda u o'tishsiz dam olish kuniga
    /// qaytadi: aks holda qoidalar oxirigacha tushib, saqlanmaydigan
    /// natija <c>Present</c> deb chiqardi va uni o'qigan har qanday kod
    /// adashardi.
    /// </remarks>
    private static AttendanceDayStatus StatusOf(Employment? employment, DayPlan? plan, PairingResult pairing)
    {
        if (employment is null)
        {
            return AttendanceDayStatus.NotEmployed;
        }

        if (plan is null)
        {
            return AttendanceDayStatus.NoSchedule;
        }

        if (!plan.IsWorking)
        {
            return pairing.PassCount > 0 ? AttendanceDayStatus.DayOffWorked : AttendanceDayStatus.DayOff;
        }

        if (pairing.PassCount == 0)
        {
            return AttendanceDayStatus.Absent;
        }

        if (pairing.MissingEntry)
        {
            return AttendanceDayStatus.MissingEntry;
        }

        return pairing.MissingExit ? AttendanceDayStatus.MissingExit : AttendanceDayStatus.Present;
    }

    /// <summary>Bir sanadagi asosiy munosabat va hal qilingan jadval.</summary>
    private readonly record struct DayContext(Employment? Employment, ScheduleResolution? Schedule);

    /// <summary>
    /// Bitta xodim uchun sana → (munosabat, jadval) keshi.
    /// </summary>
    /// <remarks>
    /// Har bir o'tish uchta nomzod sanaga qaraydi, har bir so'ralgan sana
    /// yana bir marta — kesh bo'lmasa, bir oylik hisoblashda bir xil sana
    /// o'nlab marta qayta hal qilinardi.
    /// </remarks>
    private sealed class EmployeeCalendar(ScheduleResolver schedules, IReadOnlyCollection<Employment> employments)
    {
        private readonly Dictionary<DateOnly, DayContext> _days = [];

        public DayContext For(DateOnly date)
        {
            if (!_days.TryGetValue(date, out var context))
            {
                var employment = PrimaryEmployment.Select(employments, date);
                var schedule = employment is null ? null : schedules.Resolve(employment, date);

                context = new DayContext(employment, schedule);
                _days[date] = context;
            }

            return context;
        }

        public DayPlan? PlanFor(DateOnly date) => For(date).Schedule?.Day;
    }
}
