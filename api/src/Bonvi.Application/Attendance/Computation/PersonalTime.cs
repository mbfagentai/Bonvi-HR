using Bonvi.Application.Attendance.Scheduling;

namespace Bonvi.Application.Attendance.Computation;

/// <summary>
/// Kunning «Shaxsiy vaqt» i haqida panel va hisoblovchiga kerakli narsa — SAQLANMAYDI
/// (SPEC-verifikatsiya-sorov-tahriri §5.9).
/// </summary>
/// <param name="Intervals">P — rejaga kesilgan, birlashtirilgan shaxsiy oraliqlar (UTC), vaqt tartibida.</param>
/// <param name="Minutes"><c>personal_minutes</c> — tanaffus tuzatishidan keyin (§5.3, 4-qadam).</param>
/// <param name="ExpectedStartAt">
/// Qat'iy kunda kutilgan kelish (ES): reja boshini qoplagan oraliqning oxiri, aks holda reja
/// boshi. Erkin kunda <c>null</c>.
/// </param>
/// <param name="ExpectedEndAt">Qat'iy kunda kutilgan ketish (EE): reja oxirini qoplagan oraliqning boshi, aks holda reja oxiri.</param>
/// <param name="NotStartedUntil">«Hali boshlanmagan» shu lahzagacha (§5.5); <c>null</c> — odatdagi qoida.</param>
/// <param name="ExcusesDay">Butun reja shaxsiy (erkin kunda: shaxsiy ≥ norma) — kelmagan kun «Sababli».</param>
public sealed record PersonalTime(
    IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> Intervals,
    int Minutes,
    DateTimeOffset? ExpectedStartAt,
    DateTimeOffset? ExpectedEndAt,
    DateTimeOffset? NotStartedUntil,
    bool ExcusesDay);

/// <summary>Shaxsiy vaqtli kunning sonlari — <see cref="PersonalTimeRule.Apply"/> natijasi.</summary>
/// <param name="WorkedMinutes">Ishlangan vaqt — shaxsiy oraliqlar olib tashlangan, qat'iy kunda cheklangan (6-qadam).</param>
/// <param name="CreditedMinutes">Shundan «ish vaqti» so'rovidan kelgani.</param>
/// <param name="BreakTakenMinutes">Olingan tanaffus (o'tishlar + so'rov bo'yicha).</param>
/// <param name="BreakDeductionMinutes">Ushlangan tanaffus (o'tishlar + so'rov bo'yicha).</param>
/// <param name="FirstPresenceAt">Birinchi borliq — kirish yoki shaxsiy vaqtdan tashqaridagi so'rov.</param>
/// <param name="LastPresenceAt">Oxirgi borliq; chiqish yozilmagan kunda <c>null</c>.</param>
/// <param name="CreditedFromAt">Shaxsiy vaqtdan tashqaridagi «ish vaqti» so'rovining birinchi lahzasi.</param>
/// <param name="Personal">Panel va holat uchun tavsif.</param>
public sealed record PersonalDay(
    int WorkedMinutes,
    int CreditedMinutes,
    int BreakTakenMinutes,
    int BreakDeductionMinutes,
    DateTimeOffset? FirstPresenceAt,
    DateTimeOffset? LastPresenceAt,
    DateTimeOffset? CreditedFromAt,
    PersonalTime Personal);

/// <summary>
/// Tasdiqlangan SHAXSIY so'rovni kun hisobiga qo'llaydi — «sababli, lekin haq to'lanmaydi»
/// (SPEC-verifikatsiya-sorov-tahriri §5, egasining qarori 2026-10-20).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Egasining misoli.</b> Reja 08:30–17:30 (8:00), shaxsiy 14:00–16:00, 08:25 da keldi,
/// 17:35 da ketdi, oraliqda chiqmadi → <b>ishladi 6:00, shaxsiy 2:00</b>, kechikish va erta ketish
/// yo'q. Shaxsiy oraliqdagi borliq (chiqmagan yoki erta qaytgan) ishlangan vaqtdan olib
/// tashlanadi, oraliqdagi bo'shliq esa «tanaffus olingan» emas.
/// </para>
/// <para>
/// <b>Shaxsiy tur — bayroq bo'yicha</b>, nomi bo'yicha emas: <c>counts_as_work_time = false</c>
/// bo'lgan har bir turning VAQTLI tasdiqlangan so'rovi. Butun va ko'p kunlik so'rov oraliq emas —
/// avvalgidek faqat «Kelmadi» ni «Sababli» qiladi.
/// </para>
/// <para>
/// ⚠️ <b>Shaxsiy oraliq yo'q kun AYNAN avvalgidek</b> hisoblanadi: bu sinf faqat oraliq bo'lganda
/// chaqiriladi (<c>AttendanceDayBuilder</c>), qolgan yo'l — <see cref="WorkCreditRule"/> va
/// <see cref="PassPairing"/> ning o'zi.
/// </para>
/// <para>
/// Har bir yig'indi aniq lahzalarda (tick) qo'shiladi va BIR MARTA butun daqiqaga kesiladi
/// (<see cref="WholeMinutes"/>) — <see cref="PassPairing"/> dagi bilan bir xil sabab.
/// </para>
/// </remarks>
public static class PersonalTimeRule
{
    /// <summary>
    /// Kun rejasiga tushadigan shaxsiy oraliqlar (§5.1 P) — rejaga kesilgan, birlashtirilgan
    /// (tutashganlari ham), vaqt tartibida.
    /// </summary>
    /// <remarks>
    /// <c>now</c> ga KESILMAYDI: tasdiqlangan oraliq — reja ma'lumoti, borliq emas. Yuklovchi
    /// D … D + 1 so'rovlarini beradi (<see cref="WorkCreditRule.RequestDatesNeeded"/>), ya'ni tungi
    /// smena ertalabki so'rovni ham ko'radi. Dam olish kuni, jadvalsiz kun va rejadan tashqaridagi
    /// oraliq — bo'sh natija.
    /// </remarks>
    /// <param name="date">Hisoblanayotgan sana (smena boshlangan sana).</param>
    /// <param name="plan">Kun rejasi.</param>
    /// <param name="requests">Xodimning tasdiqlangan so'rovlari (istalgan sanalarda).</param>
    public static IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> IntervalsFor(
        DateOnly date,
        DayPlan? plan,
        IReadOnlyCollection<WorkCredit>? requests)
    {
        if (requests is null || requests.Count == 0
            || plan is not { IsWorking: true, PlanStartAt: { } planStart, PlanEndAt: { } planEnd })
        {
            return [];
        }

        // Smena 24 soatdan qisqa va D da boshlanadi — unga faqat D va D + 1 dagi so'rov tusha oladi.
        var clipped = requests
            .Where(request => !request.CountsAsWorkTime && request.FromTime is not null && request.ToTime is not null)
            .Where(request => request.FromDate >= date && request.FromDate <= date.AddDays(1))
            .Select(request => (
                From: WorkCreditRule.At(request.FromDate, request.FromTime!.Value),
                To: WorkCreditRule.At(request.FromDate, request.ToTime!.Value)))
            .Select(item => (From: item.From > planStart ? item.From : planStart, To: item.To < planEnd ? item.To : planEnd))
            .Where(item => item.To > item.From)
            .OrderBy(item => item.From)
            .ToList();

        return WorkCreditRule.Merge(clipped);
    }

    /// <summary>
    /// Shaxsiy oraliqli kunni hisoblaydi — §5.3 1–8 va 12-qadamlar (qat'iy kunda 6-qadam — chegara —
    /// ham shu yerda), erkin kunda §5.4.
    /// </summary>
    /// <param name="pairing">O'tishlar juftlashi (C — yopilgan oraliqlar).</param>
    /// <param name="plan">Ish kunining rejasi.</param>
    /// <param name="credit">K — «ish vaqti» so'rovlari (<see cref="WorkCreditRule.IntervalsFor"/>).</param>
    /// <param name="personal">P — <see cref="IntervalsFor"/> natijasi, bo'sh emas.</param>
    public static PersonalDay Apply(
        PairingResult pairing,
        DayPlan plan,
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> credit,
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> personal)
    {
        ArgumentNullException.ThrowIfNull(pairing);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(credit);
        ArgumentNullException.ThrowIfNull(personal);

        if (plan is not { IsWorking: true, PlanStartAt: { } planStart, PlanEndAt: { } planEnd })
        {
            throw new ArgumentException("Shaxsiy vaqt faqat ish kunining rejasiga qo'llanadi.", nameof(plan));
        }

        // 1-qadam: kredit shaxsiy vaqtni hech qachon qoplamaydi — K′ = K \ P.
        var creditOutside = Subtract(credit, personal);

        // 2-qadam: borliq shaxsiy vaqtsiz. Oraliqdagi bo'shliq ham «tashqarida» emas.
        var closed = pairing.ClosedIntervals ?? [];
        var all = WorkCreditRule.Merge([.. closed.Concat(creditOutside).OrderBy(item => item.From)]);
        var withCredit = Measure(Subtract(all, personal), Subtract(Gaps(all), personal), plan);
        var passesOnly = Measure(Subtract(closed, personal), Subtract(Gaps(closed), personal), plan);

        // 4-qadam: tanaffus oynasining shaxsiy bo'lmagan qismi tanaffusdan qisqa bo'lsa, yetmagani
        // shaxsiy vaqt ichida deb olinadi — u baribir haq to'lanmaydi, ikki marta sanalmaydi (E7, E11).
        var personalMinutes = Minutes(Length(personal));
        if (plan is { BreakWindowStartAt: { } windowFrom, BreakWindowEndAt: { } windowTo })
        {
            var personalInWindow = Minutes(Overlap(personal, windowFrom, windowTo));
            var freeWindow = Minutes((windowTo - windowFrom).Ticks - Overlap(personal, windowFrom, windowTo));
            personalMinutes -= Math.Min(personalInWindow, Math.Max(0, plan.BreakMinutes - freeWindow));
        }

        // 5-qadam: kredit ulushi — reja − shaxsiy dan oshmaydi.
        int credited;
        int worked;
        if (creditOutside.Count == 0)
        {
            credited = 0;
            worked = passesOnly.Worked;
        }
        else
        {
            var room = Math.Max(0, plan.PlannedMinutes - personalMinutes - passesOnly.Worked);
            credited = Math.Clamp(withCredit.Worked - passesOnly.Worked, 0, room);
            worked = passesOnly.Worked + credited;
        }

        // 6-qadam (faqat qat'iy kun): reja tashqarisidagi borliq haq to'lanmaydigan vaqtni «sotib
        // olmaydi» — egasining misoli shuning uchun 6:00 (SPEC §12 Q1). Erkin kunda butun borliq
        // hisoblanadi (egasining qarori) — chegara yo'q.
        if (!plan.IsFlexible && personalMinutes > 0)
        {
            worked = Math.Min(worked, Math.Max(0, plan.PlannedMinutes - personalMinutes));
        }

        // 8-qadam: borliq chegaralari — `WorkCreditRule.Apply` dagi qoida, K′ bilan.
        DateTimeOffset? firstPresence = pairing.FirstInAt;
        DateTimeOffset? lastPresence = pairing.LastOutAt;
        if (creditOutside.Count > 0)
        {
            var firstCredit = creditOutside[0].From;
            var lastCredit = creditOutside[^1].To;
            firstPresence = firstPresence is { } firstIn && firstIn < firstCredit ? firstIn : firstCredit;
            lastPresence = lastPresence is { } lastOut && lastOut > lastCredit ? lastOut : lastCredit;
        }

        if (pairing.MissingExit)
        {
            // Qachon ketgani noma'lum — so'rov ham buni bilmaydi.
            lastPresence = null;
        }

        return new PersonalDay(
            WorkedMinutes: worked,
            CreditedMinutes: credited,
            BreakTakenMinutes: withCredit.Taken,
            BreakDeductionMinutes: withCredit.Deduction,
            FirstPresenceAt: firstPresence,
            LastPresenceAt: lastPresence,
            CreditedFromAt: creditOutside.Count > 0 ? creditOutside[0].From : null,
            Personal: Describe(plan, planStart, planEnd, personal, personalMinutes));
    }

    /// <summary>[from, to] ning shaxsiy oraliqlardan tashqaridagi butun daqiqalari — <c>|[from, to] \ P|</c>.</summary>
    /// <remarks>Kechikish va erta ketish shu bilan o'lchanadi: kun o'rtasidagi shaxsiy vaqt ulardan ayriladi.</remarks>
    /// <param name="from">Boshi.</param>
    /// <param name="to">Oxiri; boshidan oldin bo'lsa — 0.</param>
    /// <param name="personal">P.</param>
    public static int MinutesOutside(
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> personal)
    {
        ArgumentNullException.ThrowIfNull(personal);

        return to <= from ? 0 : Minutes((to - from).Ticks - Overlap(personal, from, to));
    }

    /// <summary>§5.3 9–10 va §5.5 uchun tavsif: kutilgan kelish va ketish, «hali boshlanmagan» chegarasi, kunni oqlashi.</summary>
    private static PersonalTime Describe(
        DayPlan plan,
        DateTimeOffset planStart,
        DateTimeOffset planEnd,
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> personal,
        int personalMinutes)
    {
        if (plan.IsFlexible)
        {
            // Erkin kunda kechikish yo'q; eng kech kelish normaning shaxsiy bo'lmagan qismidan (§5.4).
            return new PersonalTime(
                personal,
                personalMinutes,
                ExpectedStartAt: null,
                ExpectedEndAt: null,
                NotStartedUntil: personalMinutes > 0
                    ? planEnd.AddMinutes(-Math.Max(0, plan.PlannedMinutes - personalMinutes))
                    : null,
                ExcusesDay: personalMinutes >= plan.PlannedMinutes);
        }

        // Reja boshini qoplagan oraliq — kutilgan kelish uning oxiriga suriladi (yon berish shundan).
        var expectedStart = personal
            .Where(item => item.From <= planStart && planStart < item.To)
            .Select(item => (DateTimeOffset?)item.To)
            .FirstOrDefault() ?? planStart;

        // Reja oxirini qoplagan oraliq — kutilgan ketish uning boshiga.
        var expectedEnd = personal
            .Where(item => item.From < planEnd && planEnd <= item.To)
            .Select(item => (DateTimeOffset?)item.From)
            .FirstOrDefault() ?? planEnd;

        return new PersonalTime(
            personal,
            personalMinutes,
            ExpectedStartAt: expectedStart,
            ExpectedEndAt: expectedEnd,
            NotStartedUntil: expectedStart > planStart ? expectedStart : null,
            ExcusesDay: personal.Any(item => item.From <= planStart && item.To >= planEnd));
    }

    /// <summary>§5.3 3-qadam: tanaffus formulasi (§5.5.1) kamaytirilgan to'plamlar ustida.</summary>
    /// <param name="present">R — borliq, shaxsiy vaqtsiz.</param>
    /// <param name="outside">O — oraliqlar orasidagi bo'shliq, shaxsiy vaqtsiz.</param>
    /// <param name="plan">Reja — tanaffus oynasi va uzunligi.</param>
    private static (int Worked, int Taken, int Deduction) Measure(
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> present,
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> outside,
        DayPlan plan)
    {
        var presentMinutes = Minutes(Length(present));

        if (plan is not { BreakWindowStartAt: { } windowFrom, BreakWindowEndAt: { } windowTo })
        {
            // Oynasiz kunda tanaffus umuman ushlanmaydi (SPEC §5.5.1).
            return (presentMinutes, 0, 0);
        }

        var taken = Minutes(Overlap(outside, windowFrom, windowTo));
        var inWindow = Minutes(Overlap(present, windowFrom, windowTo));
        var deduction = Math.Max(0, Math.Min(plan.BreakMinutes - taken, inWindow));

        return (presentMinutes - deduction, taken, deduction);
    }

    /// <summary>Ketma-ket oraliqlar orasidagi bo'shliqlar — <see cref="PassPairing"/> dagi «tashqarida» ning o'zi.</summary>
    private static List<(DateTimeOffset From, DateTimeOffset To)> Gaps(IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> ordered)
    {
        var gaps = new List<(DateTimeOffset From, DateTimeOffset To)>();

        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].From > ordered[i - 1].To)
            {
                gaps.Add((ordered[i - 1].To, ordered[i].From));
            }
        }

        return gaps;
    }

    /// <summary>X \ P — har bir oraliqdan shaxsiy oraliqlar kesib tashlanadi (P birlashtirilgan, tartibda).</summary>
    private static List<(DateTimeOffset From, DateTimeOffset To)> Subtract(
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> intervals,
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> personal)
    {
        var result = new List<(DateTimeOffset From, DateTimeOffset To)>(intervals.Count);

        foreach (var (from, to) in intervals)
        {
            var cursor = from;

            foreach (var (personalFrom, personalTo) in personal)
            {
                if (personalTo <= cursor)
                {
                    continue;
                }

                if (personalFrom >= to)
                {
                    break;
                }

                if (personalFrom > cursor)
                {
                    result.Add((cursor, personalFrom));
                }

                cursor = personalTo;

                if (cursor >= to)
                {
                    break;
                }
            }

            if (cursor < to)
            {
                result.Add((cursor, to));
            }
        }

        return result;
    }

    private static long Length(IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> intervals)
        => intervals.Sum(item => (item.To - item.From).Ticks);

    private static long Overlap(
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> intervals,
        DateTimeOffset from,
        DateTimeOffset to)
        => intervals.Sum(item => WorkCreditRule.Overlap(item.From, item.To, from, to));

    private static int Minutes(long ticks) => WholeMinutes.AtLeastZero(TimeSpan.FromTicks(ticks));
}
