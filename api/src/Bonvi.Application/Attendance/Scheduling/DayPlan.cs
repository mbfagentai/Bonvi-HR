using Bonvi.Application.Attendance.Computation;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Scheduling;

/// <summary>
/// Jadval kunining ANIQ sanaga qo'yilgan holi — soat ko'rsatkichlari emas, lahzalar.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega jadval kunining o'zi yetmaydi.</b> <see cref="WorkScheduleDay"/>
/// «20:00 dan 08:00 gacha» deydi, ya'ni ikkita SOAT ko'rsatkichi.
/// Hisoblash esa o'tishning LAHZASINI rejaning lahzasi bilan
/// solishtiradi, va tungi smenada 08:00 ertangi kunga tegishli. Bu
/// aylantirish shu yerda BIR MARTA bajariladi: kun oynasi (§5.3),
/// kechikish, erta ketish va tanaffus oynasi — hammasi shu obyektdagi
/// lahzalarni o'qiydi. Har biri o'zi aylantirsa, bittasi yarim tunni
/// unutadi.
/// </para>
/// <para>
/// ⭐ <b>Smena tugashi = boshlanish + smena uzunligi</b>, «tugash soati
/// ertasiga tushadimi» degan shart bilan emas. <c>TimeOnly</c> ayirmasi
/// yarim tundan o'tib hisoblanadi va doim [0, 24 soat) oralig'ida
/// bo'ladi, ya'ni tungi smena alohida tarmoq talab qilmaydi — xato
/// yashirinadigan tarmoq ham yo'q. O'zbekistonda yozgi vaqt yo'q
/// (<see cref="BonviTime"/> izohi), shuning uchun lahzaga soat qo'shish
/// aniq.
/// </para>
/// <para>
/// Lahzalar UTC da qaytadi. Kun chegarasi Toshkent bo'yicha, saqlash esa
/// UTC — ikkalasi aralashsa <c>timestamptz</c> ustuniga besh soat
/// surilgan lahza yoziladi.
/// </para>
/// </remarks>
public sealed class DayPlan
{
    private DayPlan(DateOnly date, WorkSchedule schedule)
    {
        Date = date;
        GraceInMinutes = schedule.GraceInMinutes;
        GraceOutMinutes = schedule.GraceOutMinutes;
        EarlyArrivalThresholdMinutes = schedule.EarlyArrivalThresholdMinutes;
        LateDepartureThresholdMinutes = schedule.LateDepartureThresholdMinutes;
    }

    /// <summary>Qaysi sana uchun (smena BOSHLANADIGAN Toshkent sanasi).</summary>
    public DateOnly Date { get; }

    /// <summary>Jadval bo'yicha ish kunimi.</summary>
    public bool IsWorking { get; private init; }

    /// <summary>Rejadagi boshlanish lahzasi (UTC). Dam olish kunida <c>null</c>.</summary>
    public DateTimeOffset? PlanStartAt { get; private init; }

    /// <summary>Rejadagi tugash lahzasi (UTC) — tungi smenada ertangi kunga tushadi.</summary>
    public DateTimeOffset? PlanEndAt { get; private init; }

    /// <summary>
    /// Rejadagi daqiqalar: smena uzunligi − <see cref="BreakMinutes"/>.
    /// </summary>
    /// <remarks>
    /// Tanaffus oynasi bor-yo'qligidan qat'i nazar ayriladi (SPEC §5.5,
    /// 6-qadam): reja ham, fakt ham tanaffusni BIR XIL shartda chiqarsa
    /// ular to'g'ridan-to'g'ri solishtiriladi va ortiqcha ish o'z
    /// ma'nosini saqlaydi. Pastki chegara 0 — domen smenadan uzun
    /// tanaffusni rad etmaydi, manfiy reja esa ortiqcha ishni ishlangan
    /// vaqtdan ham katta qilib ko'rsatardi.
    /// </remarks>
    public int PlannedMinutes { get; private init; }

    /// <summary>Haq to'lanmaydigan tanaffus uzunligi (daqiqa). Dam olish kunida 0.</summary>
    public int BreakMinutes { get; private init; }

    /// <summary>
    /// «Erkin ish jadvali» — kun boshlanish/tugash vaqti bilan emas, ishlangan vaqt bilan
    /// baholanadi (SPEC-erkin-jadval §2). Dam olish kunida doim <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Shunda <see cref="PlannedMinutes"/> — normaning o'zi («Ish vaqti»), kechikish va erta
    /// ketish yo'q. ⭐ Borliq oynaga KESILMAYDI (egasining qarori, 2026-10-20): 08:00 da kelib
    /// 17:30 da ketgan odam 08:30–18:00 oynasida ham normani bajargan. Oyna
    /// [<see cref="PlanStartAt"/>, <see cref="PlanEndAt"/>] faqat yo'l-yo'riq: kun oynasi
    /// (§5.3), tanaffus oynasi va «eng kech kelish» (<see cref="LatestStartAt"/>) shundan.
    /// </remarks>
    public bool IsFlexible { get; private init; }

    /// <summary>
    /// Kelish uchun eng kech lahza: erkin kunda <c>PlanEndAt − PlannedMinutes</c>, qat'iy kunda
    /// <see cref="PlanStartAt"/>. Dam olish kunida <c>null</c>.
    /// </summary>
    /// <remarks>
    /// «Hali boshlanmagan» shu lahzagacha (<see cref="DashboardStatusRule.HasNotStarted"/>):
    /// 08:30–18:00 oynasida 7:30 normali xodim 10:30 gacha «Kelmadi» emas. Tanaffus hisobga
    /// olinmaydi — optimistik chegara hech kimni erta ayblamaydi (SPEC-erkin-jadval §14 Q3).
    /// </remarks>
    public DateTimeOffset? LatestStartAt
        => IsFlexible && PlanEndAt is { } planEnd ? planEnd.AddMinutes(-PlannedMinutes) : PlanStartAt;

    /// <summary>Tanaffus oynasining boshlanish lahzasi (UTC). Oyna yo'q bo'lsa <c>null</c>.</summary>
    public DateTimeOffset? BreakWindowStartAt { get; private init; }

    /// <summary>Tanaffus oynasining tugash lahzasi (UTC). Oyna yo'q bo'lsa <c>null</c>.</summary>
    public DateTimeOffset? BreakWindowEndAt { get; private init; }

    /// <summary>Kelishdagi yon berish (daqiqa).</summary>
    public int GraceInMinutes { get; }

    /// <summary>Ketishdagi yon berish (daqiqa).</summary>
    public int GraceOutMinutes { get; }

    /// <summary>Erta kelish chegarasi (daqiqa) — <see cref="WorkSchedule.EarlyArrivalThresholdMinutes"/>.</summary>
    public int EarlyArrivalThresholdMinutes { get; }

    /// <summary>Kech ketish chegarasi (daqiqa) — <see cref="WorkSchedule.LateDepartureThresholdMinutes"/>.</summary>
    public int LateDepartureThresholdMinutes { get; }

    /// <summary>
    /// Ishlangan vaqtga QO'SHILMAYDIGAN bo'laklar (UTC): <c>[PlanStart − erta, PlanStart)</c> va
    /// <c>[PlanEnd, PlanEnd + kech)</c>. Chegarasi 0 bo'lgan tomon ro'yxatga tushmaydi.
    /// </summary>
    /// <remarks>
    /// Faqat QAT'IY ish kunida. Dam olish kunida reja yo'q — u yerdagi ish to'liq ortiqcha;
    /// erkin kunda esa borliq oynaga kesilmaydi (egasining qarori, 2026-10-20). Bo'laklar
    /// smena tashqarisida, tanaffus oynasi esa doim smena ichida
    /// (<c>WorkScheduleDay.ValidateBreakWindow</c>) — ya'ni ular tanaffus hisobiga tegmaydi.
    /// </remarks>
    public IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> UncountedBands
    {
        get
        {
            if (this is not { IsWorking: true, IsFlexible: false, PlanStartAt: { } planStart, PlanEndAt: { } planEnd })
            {
                return [];
            }

            var bands = new List<(DateTimeOffset From, DateTimeOffset To)>(2);

            if (EarlyArrivalThresholdMinutes > 0)
            {
                bands.Add((planStart.AddMinutes(-EarlyArrivalThresholdMinutes), planStart));
            }

            if (LateDepartureThresholdMinutes > 0)
            {
                bands.Add((planEnd, planEnd.AddMinutes(LateDepartureThresholdMinutes)));
            }

            return bands;
        }
    }

    /// <summary>Tanaffus oynasi belgilanganmi. Yo'q bo'lsa — tanaffus UMUMAN ayrilmaydi (SPEC §5.5.1).</summary>
    public bool HasBreakWindow => BreakWindowStartAt is not null && BreakWindowEndAt is not null;

    /// <summary>
    /// Kun oynasining boshi: reja boshlanishidan
    /// <see cref="AttendanceRules.DayWindowPreHours"/> soat oldin (SPEC §5.3).
    /// </summary>
    public DateTimeOffset? AttributionFrom => PlanStartAt?.AddHours(-AttendanceRules.DayWindowPreHours);

    /// <summary>
    /// Kun oynasining oxiri: reja tugashidan
    /// <see cref="AttendanceRules.DayWindowPostHours"/> soat keyin (SPEC §5.3).
    /// </summary>
    public DateTimeOffset? AttributionTo => PlanEndAt?.AddHours(AttendanceRules.DayWindowPostHours);

    /// <summary>Jadvalning shu sanaga to'g'ri keladigan kunini lahzalarga aylantiradi.</summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Tanaffus oynasi SANAGA emas, REJA BOSHLANISHIGA qo'shiladi</b>:
    /// <c>PlanStartAt + (break_start − start_time)</c>. «Sana + 00:30»
    /// dushanbadagi 20:00–08:00 smenaning 00:30–01:30 oynasini DUSHANBA
    /// 00:30 ga — smenadan o'n to'qqiz yarim soat OLDINGA — qo'yardi. Unga
    /// hech qanday chiqish to'g'ri kelmaydi va har bir tungi xodimdan har
    /// kecha to'liq tanaffus ushlanardi, kunduzgi smenaning testlari esa
    /// yashil bo'lib qolardi (SPEC §5.5.1, §11.1 #61). Bu yerdagi ayirma
    /// <c>WorkScheduleDay.ValidateBreakWindow</c> dagi bilan aynan bir
    /// xil, ya'ni tekshiruv va hisoblash oynaning joyi haqida hech qachon
    /// kelisha olmay qolmaydi.
    /// </para>
    /// <para>
    /// Jadval kunlari yuklanmagan bo'lsa ISTISNO otiladi, «dam olish kuni»
    /// deb jim qaytilmaydi: repozitoriy <c>Include(Days)</c> ni unutsa,
    /// butun korxona bir kechada «dam oldi» bo'lib, birorta ham
    /// <c>Absent</c> yozilmasdi va buni hech kim sezmasdi.
    /// </para>
    /// </remarks>
    /// <param name="schedule">Kunlari bilan birga yuklangan jadval.</param>
    /// <param name="date">Smena boshlanadigan Toshkent sanasi.</param>
    /// <param name="exact">
    /// Shu SANAGA yozilgan katak (yillik to'r, SPEC-jadval-to'ri §B2).
    /// Berilsa, haftalik naqsh o'qilmaydi ham.
    /// </param>
    public static DayPlan For(WorkSchedule schedule, DateOnly date, WorkScheduleDate? exact = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        /*
          ⭐ Sana katagi naqshdan USTUN. Bayram, qisqa kun, mavsumiy
          o'zgarish — hammasi shu qatlamda yashaydi va naqshni
          o'zgartirmaydi: «dushanba 09:00–18:00» degan qoida joyida
          qolaveradi, faqat 21-mart boshqacha bo'ladi.

          ⚠️ Tekshiruv naqshdan OLDIN: aks holda haftalik kuni
          yuklanmagan jadvalda (Cycle turi) sana katagi ham ishlamasdi.
        */
        if (exact is not null)
        {
            if (exact.WorkScheduleId != schedule.Id)
            {
                throw new ArgumentException(
                    $"«{schedule.Name}» jadvaliga boshqa jadvalning sanasi berildi.", nameof(exact));
            }

            if (exact.Date != date)
            {
                throw new ArgumentException(
                    $"{date:yyyy-MM-dd} uchun {exact.Date:yyyy-MM-dd} sanasining katagi berildi.", nameof(exact));
            }

            return exact.IsWorking
                ? Working(schedule, date, exact.StartTime, exact.EndTime, exact.BreakMinutes, exact.BreakStart, exact.BreakEnd,
                    exact.IsFlexible, exact.RequiredMinutes,
                    $"«{schedule.Name}» jadvalining {date:yyyy-MM-dd} katagi ish kuni, lekin vaqtlari ko'rsatilmagan.")
                : new DayPlan(date, schedule);
        }

        if (schedule.Kind != WorkScheduleKind.Weekly)
        {
            // Domen bu turni yaratishning o'zida rad etadi (SPEC §2.4.2);
            // bu yerga yetib kelgan bo'lsa, bazaga qo'lda yozilgan.
            throw new NotSupportedException(
                $"«{schedule.Name}» jadvali: faqat haftalik jadval hisoblanadi, {schedule.Kind} hali qo'llanmaydi.");
        }

        var dayIndex = IsoDayIndex(date);
        var day = schedule.Days.FirstOrDefault(item => item.DayIndex == dayIndex)
            ?? throw new InvalidOperationException(
                $"«{schedule.Name}» jadvalining {dayIndex}-kuni yuklanmagan. Jadval kunlari bilan birga o'qilishi shart.");

        return day.IsWorking
            ? Working(schedule, date, day.StartTime, day.EndTime, day.BreakMinutes, day.BreakStart, day.BreakEnd,
                day.IsFlexible, day.RequiredMinutes,
                $"«{schedule.Name}» jadvalining {dayIndex}-kuni ish kuni, lekin vaqtlari ko'rsatilmagan.")
            : new DayPlan(date, schedule);
    }

    /// <summary>
    /// Tasdiqlangan jadval so'rovining kuni (SPEC-verifikatsiya-jadval-sorovi §C2) — yon
    /// berish daqiqalari xodimning jadvalidan.
    /// </summary>
    /// <param name="schedule">Xodimning shu sanadagi jadvali.</param>
    /// <param name="requested">So'ralgan kun.</param>
    public static DayPlan ForOverride(WorkSchedule schedule, EmployeeDayOverride requested)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(requested);

        return requested.IsWorking
            ? Working(
                schedule,
                requested.Date,
                requested.StartTime,
                requested.EndTime,
                requested.BreakMinutes,
                requested.BreakStart,
                requested.BreakEnd,
                requested.IsFlexible,
                requested.RequiredMinutes,
                $"Jadval so'rovining {requested.Date:yyyy-MM-dd} kuni ish kuni, lekin vaqtlari ko'rsatilmagan.")
            : new DayPlan(requested.Date, schedule);
    }

    /// <summary>Ish kunining rejasi — naqshdan ham, sana katagidan ham SHU yerda quriladi.</summary>
    /// <remarks>
    /// <para>
    /// Ikkala manba uchun bitta metod: hisoblash (tungi smena, tanaffus
    /// oynasi, rejadagi daqiqalar) ikki joyda yozilsa, ular vaqt o'tib
    /// ajralib ketardi va bir xil kun ikki xil hisoblanardi.
    /// </para>
    /// <para>
    /// Erkin kunda reja — saqlangan norma («Ish vaqti»); u yo'q bo'lsa (faqat qo'lda
    /// yozilgan qatorda) qat'iy kunning sanog'i. Normaning yuqori chegarasi bu yerda
    /// QISILMAYDI: saqlangan qiymat ishlatiladi, xato esa keyingi saqlashda chiqadi
    /// (SPEC-erkin-jadval §2.4).
    /// </para>
    /// </remarks>
    private static DayPlan Working(
        WorkSchedule schedule,
        DateOnly date,
        TimeOnly? startTime,
        TimeOnly? endTime,
        int breakMinutes,
        TimeOnly? breakStart,
        TimeOnly? breakEnd,
        bool isFlexible,
        int? requiredMinutes,
        string missingTimesMessage)
    {
        if (startTime is not { } start || endTime is not { } end)
        {
            throw new InvalidOperationException(missingTimesMessage);
        }

        var planStart = new DateTimeOffset(date.ToDateTime(start), BonviTime.TashkentOffset).ToUniversalTime();
        var shiftLength = end - start;
        var fixedMinutes = Math.Max(0, WholeMinutes.AtLeastZero(shiftLength) - breakMinutes);

        return new DayPlan(date, schedule)
        {
            IsWorking = true,
            PlanStartAt = planStart,
            PlanEndAt = planStart + shiftLength,
            PlannedMinutes = isFlexible ? requiredMinutes ?? fixedMinutes : fixedMinutes,
            BreakMinutes = breakMinutes,
            BreakWindowStartAt = breakStart is { } window ? planStart + (window - start) : null,
            BreakWindowEndAt = breakEnd is { } windowEnd ? planStart + (windowEnd - start) : null,
            IsFlexible = isFlexible,
        };
    }

    /// <summary>ISO hafta kuni: 1 — dushanba … 7 — yakshanba.</summary>
    /// <remarks>
    /// <see cref="DayOfWeek"/> yakshanbani 0 deb sanaydi — jadvaldagi
    /// <c>day_index</c> bilan to'g'ridan-to'g'ri solishtirilsa, yakshanba
    /// hech qachon topilmasdi.
    /// </remarks>
    private static int IsoDayIndex(DateOnly date)
        => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
}
