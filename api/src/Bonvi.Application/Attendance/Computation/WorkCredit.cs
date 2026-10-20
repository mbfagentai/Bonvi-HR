using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Abstractions;

namespace Bonvi.Application.Attendance.Computation;

/// <summary>
/// Kun hisobiga ta'sir qiladigan TASDIQLANGAN so'rov — hisob uchun kerakli qismi: «ish vaqti
/// hisoblanadigan» tur (SPEC-so'rovlar §B3 AMENDED 2026-10-17) yoki vaqtli SHAXSIY so'rov
/// (SPEC-sorov-tahriri §5.2).
/// </summary>
/// <param name="FromDate">Boshlanish sanasi (Toshkent).</param>
/// <param name="ToDate">Tugash sanasi (Toshkent), o'zi ham kiradi.</param>
/// <param name="FromTime">Kun ichidagi boshlanish — faqat bir kunlik so'rovda; <c>null</c> — butun kun.</param>
/// <param name="ToTime">Kun ichidagi tugash.</param>
/// <param name="CountsAsWorkTime">
/// Turi «ish vaqti hisoblanadi»mi. <c>false</c> — shaxsiy tur: so'rov ishlangan vaqtga
/// QO'SHILMAYDI (<see cref="WorkCreditRule.IntervalsFor"/> uni tashlaydi), aksincha o'z soatlarini
/// ishlangan vaqtdan ayiradi (<see cref="PersonalTimeRule"/>). Yuklovchi bunday so'rovni faqat
/// vaqtli bo'lsa beradi.
/// </param>
public sealed record WorkCredit(
    DateOnly FromDate,
    DateOnly ToDate,
    TimeOnly? FromTime,
    TimeOnly? ToTime,
    bool CountsAsWorkTime = true);

/// <summary>Bir kunga tushgan so'rov ulushi — <see cref="WorkCreditRule.Apply"/> natijasi.</summary>
/// <param name="CreditedMinutes">Ishlangan vaqtga QO'SHILGAN daqiqalar (terminal o'tishlari ustiga).</param>
/// <param name="WorkedMinutes">Jami ishlangan vaqt — o'tishlar + so'rov.</param>
/// <param name="BreakTakenMinutes">Birlashgan oraliqlar bo'yicha olingan tanaffus.</param>
/// <param name="BreakDeductionMinutes">Birlashgan oraliqlar bo'yicha ushlangan tanaffus.</param>
/// <param name="FirstPresenceAt">Kundagi birinchi «borliq» — o'tish yoki so'rov.</param>
/// <param name="LastPresenceAt">Oxirgi borliq — chiqish yoki so'rov oxiri.</param>
/// <param name="CreditedFromAt">So'rov qamragan birinchi lahza — panelning «keldi» si uchun.</param>
public sealed record CreditedDay(
    int CreditedMinutes,
    int WorkedMinutes,
    int BreakTakenMinutes,
    int BreakDeductionMinutes,
    DateTimeOffset? FirstPresenceAt,
    DateTimeOffset? LastPresenceAt,
    DateTimeOffset? CreditedFromAt);

/// <summary>
/// Tasdiqlangan «ish vaqti» so'rovini kun hisobiga qo'shadi — SPEC-so'rovlar §B3 AMENDED.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Qoida.</b> So'ralgan vaqt xodim BINODA bo'lgandek hisoblanadi: uning
/// oraliqlari haqiqiy yopilgan oraliqlar bilan birlashtiriladi va §5.5 / §5.5.1
/// ning O'ZI (ishlangan vaqt, tanaffus) birlashgan to'plamda qayta yuritiladi.
/// Shu sababli so'rov ichiga tushgan tushlik soati baribir ushlanadi — so'rov
/// ishni «qo'shadi», tanaffusni «bekor qilmaydi».
/// </para>
/// <para>
/// <b>Uchta chegara</b>, hammasi tizim HR kiritmagan raqamni yasamasligi uchun:
/// </para>
/// <list type="bullet">
///   <item>so'rov faqat REJADAGI smena ichida hisoblanadi — dam olish kuni,
///         jadvalsiz kun va smenadan tashqari soatlar qo'shilmaydi;</item>
///   <item>hali kelmagan vaqt qo'shilmaydi (<c>now</c> gacha) — ertangi so'rov
///         bugun «ishlandi» bo'lib ko'rinmaydi, kun tungi qayta hisoblashda
///         to'ladi;</item>
///   <item>qo'shilgan daqiqa kunni REJADAN oshirmaydi — so'rov ortiqcha ish
///         yasamaydi (tanaffus oynasi yo'q jadvalda ham).</item>
/// </list>
/// <para>
/// O'tishlar va <c>FirstInAt</c> / <c>LastOutAt</c> tegilmaydi: ular terminal
/// yozgan fakt bo'lib qoladi. So'rovdan kelgan ulush alohida
/// (<see cref="CreditedDay.CreditedMinutes"/>) saqlanadi va ko'rsatiladi.
/// </para>
/// </remarks>
public static class WorkCreditRule
{
    /// <summary>
    /// D sanani hisoblash uchun qaysi sanalardagi so'rovlar kerak.
    /// </summary>
    /// <remarks>
    /// Tungi smena ertasiga o'tadi: D ning 20:00–08:00 smenasiga D + 1 dagi
    /// 02:00–04:00 so'rov tushadi. Shu sababli yuklash bir kun keyinni ham oladi.
    /// </remarks>
    /// <param name="from">Hisoblanadigan birinchi sana.</param>
    /// <param name="to">Hisoblanadigan oxirgi sana.</param>
    public static (DateOnly From, DateOnly To) RequestDatesNeeded(DateOnly from, DateOnly to)
        => (from, to.AddDays(1));

    /// <summary>
    /// Kun rejasiga tushadigan so'rov oraliqlari — rejaga va <paramref name="now"/> ga
    /// kesilgan, birlashtirilgan, vaqt tartibida.
    /// </summary>
    /// <param name="date">Hisoblanayotgan sana (smena boshlangan sana).</param>
    /// <param name="plan">Kun rejasi; ish kuni bo'lmasa — bo'sh natija.</param>
    /// <param name="credits">Xodimning so'rovlari (istalgan sanalarda).</param>
    /// <param name="now">Hozirgi lahza.</param>
    public static IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> IntervalsFor(
        DateOnly date,
        DayPlan? plan,
        IReadOnlyCollection<WorkCredit>? credits,
        DateTimeOffset now)
    {
        if (credits is null || credits.Count == 0
            || plan is not { IsWorking: true, PlanStartAt: { } planStart, PlanEndAt: { } planEnd })
        {
            return [];
        }

        var raw = new List<(DateTimeOffset From, DateTimeOffset To)>();

        foreach (var credit in credits)
        {
            // ⚠️ Shaxsiy tur — kredit EMAS: uning soatlari ishlangan vaqtdan ayriladi
            // (`PersonalTimeRule`). Bu yerda olinsa, shaxsiy so'rov ish bo'lib hisoblanardi.
            if (!credit.CountsAsWorkTime)
            {
                continue;
            }

            if (credit.FromTime is { } fromTime && credit.ToTime is { } toTime)
            {
                // Vaqtli so'rov — o'z sanasining aniq oralig'i; qaysi smenaga
                // tushishini reja oynasi bilan kesishma hal qiladi.
                raw.Add((At(credit.FromDate, fromTime), At(credit.FromDate, toTime)));
            }
            else if (credit.FromDate <= date && date <= credit.ToDate)
            {
                // Butun kunlik — shu sanada boshlangan smenaning o'zi.
                raw.Add((planStart, planEnd));
            }
        }

        var limit = now < planEnd ? now : planEnd;

        var clipped = raw
            .Select(item => (From: item.From > planStart ? item.From : planStart, To: item.To < limit ? item.To : limit))
            .Where(item => item.To > item.From)
            .OrderBy(item => item.From)
            .ToList();

        return Merge(clipped);
    }

    /// <summary>
    /// So'rov oraliqlarini o'tishlar natijasiga qo'shadi. Oraliq bo'lmasa — <c>null</c>
    /// (kun avvalgidek, faqat o'tishlardan hisoblanadi).
    /// </summary>
    /// <param name="pairing">O'tishlar juftlashi.</param>
    /// <param name="plan">Kun rejasi.</param>
    /// <param name="credit"><see cref="IntervalsFor"/> natijasi.</param>
    public static CreditedDay? Apply(
        PairingResult pairing,
        DayPlan plan,
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> credit)
    {
        ArgumentNullException.ThrowIfNull(pairing);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(credit);

        if (credit.Count == 0)
        {
            return null;
        }

        var merged = Merge([.. (pairing.ClosedIntervals ?? []).Concat(credit).OrderBy(item => item.From)]);

        long closedTicks = 0;
        long outsideInWindowTicks = 0;
        long presentInWindowTicks = 0;

        var hasWindow = plan is { BreakWindowStartAt: not null, BreakWindowEndAt: not null };
        var windowFrom = plan.BreakWindowStartAt ?? default;
        var windowTo = plan.BreakWindowEndAt ?? default;

        for (var i = 0; i < merged.Count; i++)
        {
            var (from, to) = merged[i];
            closedTicks += (to - from).Ticks;

            if (!hasWindow)
            {
                continue;
            }

            presentInWindowTicks += Overlap(from, to, windowFrom, windowTo);

            // «Tashqarida» — faqat ikki ketma-ket oraliq orasidagi bo'shliq (PassPairing bilan bir xil).
            if (i > 0)
            {
                outsideInWindowTicks += Overlap(merged[i - 1].To, from, windowFrom, windowTo);
            }
        }

        var closed = WholeMinutes.AtLeastZero(TimeSpan.FromTicks(closedTicks));
        var breakTaken = WholeMinutes.AtLeastZero(TimeSpan.FromTicks(outsideInWindowTicks));
        var presentInWindow = WholeMinutes.AtLeastZero(TimeSpan.FromTicks(presentInWindowTicks));

        var breakDeduction = hasWindow
            ? Math.Max(0, Math.Min(plan.BreakMinutes - breakTaken, presentInWindow))
            : 0;

        // So'rov kunni rejadan oshirmaydi — ortiqcha ish faqat terminaldan.
        var room = Math.Max(0, plan.PlannedMinutes - pairing.WorkedMinutes);
        var credited = Math.Clamp(closed - breakDeduction - pairing.WorkedMinutes, 0, room);

        var firstCredit = credit[0].From;
        var lastCredit = credit[^1].To;

        return new CreditedDay(
            CreditedMinutes: credited,
            WorkedMinutes: pairing.WorkedMinutes + credited,
            BreakTakenMinutes: breakTaken,
            BreakDeductionMinutes: breakDeduction,
            FirstPresenceAt: pairing.FirstInAt is { } firstIn && firstIn < firstCredit ? firstIn : firstCredit,
            // Chiqishi yozilmagan kunda «qachon ketdi» noma'lum — so'rov ham buni bilmaydi.
            LastPresenceAt: pairing.MissingExit
                ? null
                : pairing.LastOutAt is { } lastOut && lastOut > lastCredit ? lastOut : lastCredit,
            CreditedFromAt: firstCredit);
    }

    internal static DateTimeOffset At(DateOnly date, TimeOnly time)
        => new DateTimeOffset(date.ToDateTime(time), BonviTime.TashkentOffset).ToUniversalTime();

    internal static long Overlap(DateTimeOffset from, DateTimeOffset to, DateTimeOffset windowFrom, DateTimeOffset windowTo)
    {
        var start = from > windowFrom ? from : windowFrom;
        var end = to < windowTo ? to : windowTo;
        return end > start ? (end - start).Ticks : 0;
    }

    /// <summary>Vaqt tartibidagi oraliqlarni kesishgan va tutashganlarini birlashtirib qaytaradi.</summary>
    internal static List<(DateTimeOffset From, DateTimeOffset To)> Merge(List<(DateTimeOffset From, DateTimeOffset To)> ordered)
    {
        var result = new List<(DateTimeOffset From, DateTimeOffset To)>(ordered.Count);

        foreach (var item in ordered)
        {
            if (result.Count > 0 && item.From <= result[^1].To)
            {
                if (item.To > result[^1].To)
                {
                    result[^1] = (result[^1].From, item.To);
                }

                continue;
            }

            result.Add(item);
        }

        return result;
    }
}
