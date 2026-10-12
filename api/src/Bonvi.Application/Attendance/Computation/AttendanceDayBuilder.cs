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
/// <param name="FirstPassAt">
/// Shu KUNGA tushgan birinchi o'tish (UTC), yo'nalishidan qat'i nazar; o'tish yo'q bo'lsa <c>null</c>.
/// Saqlanmaydi — panel kirishsiz kunning «kelgan» lahzasini shundan oladi, ya'ni o'tishning
/// qaysi kunga tushishini (kech chiqish istisnosi bilan) ikkinchi marta hisoblamaydi.
/// </param>
public sealed record ComputedAttendanceDay(
    Guid EmployeeId,
    DateOnly LocalDate,
    AttendanceDayFacts Facts,
    bool ShouldStore,
    ScheduleScope? ScheduleTier,
    bool HasBreakWindow,
    DateTimeOffset? FirstPassAt = null);

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
    private readonly DateOnly _today;
    private readonly DateTimeOffset _now;

    /// <summary>Hisoblovchini to'plamning jadval hal qiluvchisi ustida quradi.</summary>
    /// <param name="schedules">Bir to'plam uchun qurilgan hal qiluvchi.</param>
    /// <param name="today">
    /// Toshkent vaqti bo'yicha BUGUN — kelajak kunlarni ajratish uchun.
    /// </param>
    /// <param name="now">
    /// Hozirgi lahza — rejasi hali TUGAMAGAN kunda erta ketishni sanamaslik uchun.
    /// </param>
    /// <remarks>
    /// <para>
    /// ⭐ «Bugun» ham, «hozir» ham tashqaridan beriladi (<c>IClock</c>),
    /// <c>DateTime.Now</c> dan olinmaydi: sinf toza funksiya bo'lib qolishi
    /// kerak (sinf izohi), aks holda yarim tunda o'tgan test goh o'tib, goh
    /// yiqilardi.
    /// </para>
    /// <para>
    /// Ikkalasi bitta soatdan: <paramref name="today"/> — <paramref name="now"/>
    /// ning Toshkent sanasi. Alohida berilishi sababi — ikki xil savol: kelajak
    /// kun SANA bo'yicha hal qilinadi, «reja tugadimi» esa LAHZA bo'yicha
    /// (<see cref="DashboardStatusRule.Classify"/> ham shunday oladi).
    /// </para>
    /// <para>
    /// ⚠️ Shu sababli bugungi kunning natijasi hisoblash paytiga bog'liq: reja
    /// tugashidan oldin yozilgan qatorda erta ketish 0 bo'lib turadi va uni
    /// keyingi qayta hisoblash to'ldiradi (<c>AttendanceNightlyRecomputeJob</c>).
    /// </para>
    /// </remarks>
    public AttendanceDayBuilder(ScheduleResolver schedules, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(schedules);

        _schedules = schedules;
        _today = today;
        _now = now;
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
    /// <param name="excusedDates">
    /// Tasdiqlangan yo'qlik so'rovi qamragan sanalar (SPEC-so'rovlar §B3).
    /// Shu sanalardagi <see cref="AttendanceDayStatus.Absent"/>
    /// <see cref="AttendanceDayStatus.Excused"/> ga aylanadi — BOSHQA hech
    /// narsa o'zgarmaydi. <c>null</c> — so'rovlar hisobga olinmaydi.
    /// </param>
    public IReadOnlyList<ComputedAttendanceDay> Build(
        Guid employeeId,
        IReadOnlyCollection<Employment> employments,
        IReadOnlyCollection<PassReading> passes,
        IEnumerable<DateOnly> dates,
        IReadOnlySet<DateOnly>? excusedDates = null)
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
        var passesByDate = Attribute(passes, calendar);

        return dates
            .Distinct()
            .Order()
            .Select(date => BuildDay(
                employeeId,
                date,
                calendar.For(date),
                passesByDate.GetValueOrDefault(date) ?? [],
                excusedDates?.Contains(date) ?? false,
                _today,
                _now))
            .ToList();
    }

    /// <summary>Har bir o'tishni o'z kuniga taqsimlaydi — SPEC §5.3 va kech chiqish istisnosi.</summary>
    /// <remarks>
    /// <para>
    /// Avval kun oynalari (<see cref="PassAttribution.WindowDateFor(PassReading, Func{DateOnly, DayPlan})"/>). Hech bir oynaga
    /// tushmagan, lekin kechagi smenaning kech chiqishi bo'la oladigan o'tish
    /// (<see cref="PassAttribution.LateExitDateFor"/>) keyin, VAQT TARTIBIDA qaraladi:
    /// kecha ochiq kirish bilan tugagan bo'lsa — kechaga, aks holda o'z sanasiga
    /// (egasining qarori, 2026-09-27). Ikkinchi kech o'tish birinchisi yopgan kunni
    /// ochiq deb ko'rmaydi.
    /// </para>
    /// <para>
    /// ⚠️ «Kecha ochiqmi» savoliga faqat shu o'tishlar javob beradi: kechaning
    /// OYNASIGA tushgan va <c>local_date</c> i kecha yoki undan keyingi o'tishlar,
    /// hamda kechaga allaqachon qo'shilgan kech chiqishlar. Oynasiz qolib o'z
    /// sanasiga tushgan o'tish ham, kechagi oynaga tushgan undan oldingi kungi
    /// o'tish ham hisobga olinmaydi. Sabab — yuklash shartnomasi
    /// (<see cref="PassDatesNeeded"/>, ±1 kun): o'tishning o'z sanasini hisoblayotgan
    /// chaqiruv kechaning undan oldingi kunidagi o'tishlarni yuklamagan. Qaror
    /// qaysi oraliq hisoblanayotganiga bog'liq bo'lib qolsa (masalan ketma-ket ikki
    /// kechaning kech chiqishlari zanjiri), bitta o'tish ikki saqlangan kunda
    /// sanalib ketardi. Shu cheklov bilan qaror ikkala hisoblashda ham aynan bir
    /// xil, yuklangan ma'lumotdan chiqadi.
    /// </para>
    /// </remarks>
    private static Dictionary<DateOnly, List<PassReading>> Attribute(
        IReadOnlyCollection<PassReading> passes,
        EmployeeCalendar calendar)
    {
        var byDate = new Dictionary<DateOnly, List<PassReading>>();

        // «Kun ochiq kirish bilan tugadimi» savoli uchun o'tishlar (remarks'dagi cheklov).
        var anchors = new Dictionary<DateOnly, List<PassReading>>();
        var lateExits = new List<(PassReading Pass, DateOnly Previous)>();

        foreach (var pass in passes)
        {
            if (PassAttribution.WindowDateFor(pass, calendar.PlanFor) is { } windowDate)
            {
                Add(byDate, windowDate, pass);

                if (pass.LocalDate >= windowDate)
                {
                    Add(anchors, windowDate, pass);
                }

                continue;
            }

            if (PassAttribution.LateExitDateFor(pass, calendar.PlanFor) is { } previous)
            {
                lateExits.Add((pass, previous));
                continue;
            }

            Add(byDate, pass.LocalDate, pass);
        }

        foreach (var (pass, previous) in lateExits.OrderBy(item => item.Pass.OccurredAt).ThenBy(item => item.Pass.Id))
        {
            if (PassPairing.EndsOpen(anchors.GetValueOrDefault(previous) ?? []))
            {
                Add(byDate, previous, pass);
                Add(anchors, previous, pass);
            }
            else
            {
                Add(byDate, pass.LocalDate, pass);
            }
        }

        return byDate;

        static void Add(Dictionary<DateOnly, List<PassReading>> target, DateOnly date, PassReading pass)
        {
            if (!target.TryGetValue(date, out var list))
            {
                list = [];
                target[date] = list;
            }

            list.Add(pass);
        }
    }

    /// <summary>§5.5 6–10-qadamlar, §5.6 holati va §5.4 saqlash qoidasi.</summary>
    private static ComputedAttendanceDay BuildDay(
        Guid employeeId,
        DateOnly date,
        DayContext context,
        List<PassReading> passes,
        bool excused,
        DateOnly today,
        DateTimeOffset now)
    {
        var employment = context.Employment;
        var schedule = context.Schedule;
        var plan = schedule?.Day;

        /*
          Jadvalsiz kun — standart 24 soat (egasining qarori, 2026-10-12): terminaldan
          o'tmay ketgan odamning ochiq kirishi 22:00 da «ketdi» bo'lib yopiladi. Bugun
          faqat 22:00 o'tgach — undan oldin u hali ishda bo'lishi mumkin.
        */
        var autoExitAt = employment is not null && plan is null
            ? AttendanceRules.NoScheduleAutoExitAt(date)
            : (DateTimeOffset?)null;
        var pairing = PassPairing.Pair(passes, plan, autoExitAt <= now ? autoExitAt : null);

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

            /*
              Qachon ketgani noma'lum bo'lsa, «erta ketdi» deb ayblanmaydi.
              Reja hali tugamagan bo'lsa ham (egasining qarori, 2026-09-23):
              kun o'rtasidagi oxirgi chiqish «ketdi» degani emas — 11:30 da
              tushlikka chiqqan odam ekranda «380 daqiqa erta ketdi» bo'lib
              ko'rinardi. Reja tugagach qiymat qayta hisoblashda to'ladi.
            */
            var planEnded = now >= planEnd;

            if (planEnded && !pairing.MissingExit && pairing.LastOutAt is { } lastOut)
            {
                early = WholeMinutes.AtLeastZero(planEnd.AddMinutes(-plan.GraceOutMinutes) - lastOut);
            }
        }

        var status = StatusOf(employment, plan, pairing);

        /*
          Tasdiqlangan so'rov FAQAT «kelmadi» ni oqlaydi (§B3). Kelgan kun,
          dam olish kuni yoki jadvalsiz kun tegilmaydi: so'rov kelmaslikni
          tushuntiradi, kelishni emas. Shu tor qoida tufayli ishlangan
          daqiqa ham, kechikish ham o'zgarmaydi — tizim HR kiritmagan
          raqamni yasamaydi.
        */
        if (excused && status == AttendanceDayStatus.Absent)
        {
            status = AttendanceDayStatus.Excused;
        }
        else if (status == AttendanceDayStatus.Absent && DashboardStatusRule.HasNotStarted(plan!, today, now))
        {
            /*
              Smenasi hali BOSHLANMAGAN kun — «kelmadi» EMAS: kelajak kun
              ham, bugungi smenasi hali boshlanmagan kun ham. Qoida
              paneldagi «Hali boshlanmagan» bilan AYNAN bitta
              (`DashboardStatusRule.HasNotStarted`): ilgari 08:00 da
              09:00 lik smenaning odami tabelda, ilovada va hisobotlarda
              «Kelmadi», panelda esa «Hali boshlanmagan» bo'lib turardi;
              20:00 lik tungi smena xodimi esa kun bo'yi «Kelmadi» edi.
              Reja boshlangach kun `AttendanceStartedShiftsJob` bilan 15
              daqiqa ichida «Kelmadi» bo'lib yoziladi.

              Tartib muhim: tasdiqlangan so'rov ustun turadi, ya'ni
              keyingi haftaga ruxsat olingan kun tabelda bugundanoq
              «Sababli» bo'lib ko'rinadi. Qolgan boshlanmagan ish kunlari
              esa bo'sh qoladi va hech qaysi sanoqqa kirmaydi.
            */
            status = AttendanceDayStatus.Planned;
        }

        var facts = new AttendanceDayFacts(
            EmploymentId: employment?.Id,
            LegalEntityId: employment?.LegalEntityId,
            // Surat SHU SANADAGI joydan — jadval ham aynan shu qiymat bilan
            // hal qilingan (`ScheduleResolver.Resolve`), ya'ni ko'chirishdan
            // oldingi kun hisobotda eski bo'limda qoladi (SPEC §2.6).
            DepartmentId: context.Placement?.DepartmentId,
            PositionId: context.Placement?.PositionId,
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
        /*
          Smenasi hali boshlanmagan kun (`Planned`) uchun qator
          YARATILMAYDI: kelajak kunlar har kecha qayta hisoblanib
          turadigan minglab bo'sh qatorni keltirib chiqarardi, bugungi
          boshlanmagan smena esa «Kelmadi» bo'lib yozilib qolardi.
          Istisno — tasdiqlangan so'rov: u BUGUN qabul qilingan qaror va
          yozuvi bo'lishi kerak.
        */
        var shouldStore = pairing.PassCount > 0
            || (plan is { IsWorking: true } && status != AttendanceDayStatus.Planned);

        return new ComputedAttendanceDay(
            employeeId,
            date,
            facts,
            shouldStore,
            schedule?.Tier,
            plan?.HasBreakWindow ?? false,
            passes.Count == 0 ? null : passes.Min(pass => pass.OccurredAt).ToUniversalTime());
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

    /// <summary>Bir sanadagi asosiy munosabat, uning SHU SANADAGI joyi va hal qilingan jadval.</summary>
    private readonly record struct DayContext(
        Employment? Employment,
        EmploymentPlacement? Placement,
        ScheduleResolution? Schedule);

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
                EmploymentPlacement? placement = employment is null ? null : EmploymentPlacement.On(employment, date);
                var schedule = employment is null ? null : schedules.Resolve(employment, date);

                context = new DayContext(employment, placement, schedule);
                _days[date] = context;
            }

            return context;
        }

        public DayPlan? PlanFor(DateOnly date) => For(date).Schedule?.Day;
    }
}
