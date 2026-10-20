using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Scheduling;

namespace Bonvi.Application.Attendance.Computation;

/// <summary>Panel qarori: holat va (faqat kechikkanda) kechikish daqiqasi.</summary>
/// <param name="Status">Oltita holatdan biri.</param>
/// <param name="LateMinutes">Faqat <see cref="AttendanceDashboardStatus.Late"/> da; qolganlarida <c>null</c>.</param>
public readonly record struct DashboardVerdict(AttendanceDashboardStatus Status, int? LateMinutes);

/// <summary>
/// Asosiy sahifa panelidagi holat — egasi tasdiqlagan qoida, BITTA joyda.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Faqat qaror, hisoblash emas.</b> Qaysi o'tish shu kunga tegishli
/// (tungi smenaning ertalabki chiqishi — oldingi sanaga), birinchi kirish va
/// oxirgi chiqish — <see cref="AttendanceDayBuilder"/> (tungi hisoblash)
/// allaqachon aytgan; reja — <see cref="ScheduleResolver"/>. Bu sinf ularning
/// natijasidan oltita holatdan birini tanlaydi, boshqa hech narsa qilmaydi.
/// Shuning uchun u bazasiz sinaladi va panel o'tishni tabeldan boshqa kunga
/// qo'ya olmaydi.
/// </para>
/// <para>
/// ⚠️ <b>Kechikish daqiqasi tabeldagidan BOSHQACHA o'lchanadi — egasining
/// qarori.</b> Tabelning <c>late_minutes</c> i yon berish TUGAGANIDAN sanaladi
/// (SPEC §5.5, 8-qadam); panel esa REJA BOSHLANISHIDAN: smena 09:00, yon berish
/// 10 daqiqa, 09:33 da kelgan — panelda 33, tabelda 23. Panel Verifix ekranining
/// o'rnini bosadi va HR aynan u ko'rsatgan sonni kutadi; yon berish faqat
/// «kechikdimi» degan savolga javob beradi. Chegara LAHZA bo'yicha: 09:10:30 da
/// kelgan — kechikkan (10 daqiqa), garchi tabelda 0 bo'lsa ham.
/// </para>
/// </remarks>
public static class DashboardStatusRule
{
    /// <summary>Bitta odamning shu sanadagi holati.</summary>
    /// <remarks>
    /// <para>
    /// Tartib: jadval yo'q → <c>NoSchedule</c>; dam olish kuni → <c>DayOff</c>
    /// (o'tish bo'lsa ham — u «dam olish kunida ishladi», kechikish emas); ish
    /// kuni va o'tish bor → <c>OnTime</c> / <c>Late</c>; o'tish yo'q →
    /// <c>NotStarted</c> / <c>Absent</c>.
    /// </para>
    /// <para>
    /// <b>«Hali boshlanmagan» faqat kelajak uchun.</b> O'tgan sana hech qachon
    /// <c>NotStarted</c> emas: kechagi reja boshlanishi har doim hozirdan oldin.
    /// Bugun esa roppa-rosa reja boshlanishi lahzasidan <c>Absent</c>, yon
    /// berish tugashini kutmasdan (egasining qoidasi): smena boshlangan-u odam
    /// yo'q. Holat so'nggi emas — u kelgan zahoti <c>OnTime</c> yoki <c>Late</c>
    /// ga o'tadi, chunki panel har safar xom o'tishlardan qayta hisoblanadi.
    /// </para>
    /// </remarks>
    /// <param name="plan">Shu sanadagi reja (<see cref="ScheduleResolution.Day"/>); jadval topilmasa <c>null</c>.</param>
    /// <param name="arrivedAt">
    /// Kelgan lahza. O'tish bor kunda HECH QACHON <c>null</c> emas (kirish bo'lmasa —
    /// birinchi o'tish); <c>null</c> — shu kunga tegishli o'tish yo'q.
    /// </param>
    /// <param name="today">Bugungi Toshkent sanasi.</param>
    /// <param name="now">Hozirgi lahza.</param>
    /// <param name="excused">Shu kunda tasdiqlangan yo'qlik so'rovi bormi (SPEC-so'rovlar §B3).</param>
    public static DashboardVerdict Classify(
        DayPlan? plan,
        DateTimeOffset? arrivedAt,
        DateOnly today,
        DateTimeOffset now,
        bool excused = false)
    {
        // Jadvalsiz ham kirdi-chiqdi olinadi (2026-10-12): o'tgan bo'lsa — «Keldi», kechikishsiz.
        if (plan is null)
        {
            return new DashboardVerdict(
                arrivedAt is null ? AttendanceDashboardStatus.NoSchedule : AttendanceDashboardStatus.Arrived,
                null);
        }

        if (plan is not { IsWorking: true, PlanStartAt: { } planStart })
        {
            return new DashboardVerdict(AttendanceDashboardStatus.DayOff, null);
        }

        // Erkin kunda kelgan — doim «Vaqtida»: kechikish tushunchasi yo'q (SPEC-erkin-jadval §2.2, 16-qator).
        if (arrivedAt is not null && plan.IsFlexible)
        {
            return new DashboardVerdict(AttendanceDashboardStatus.OnTime, null);
        }

        if (arrivedAt is { } arrived)
        {
            return arrived <= planStart.AddMinutes(plan.GraceInMinutes)
                ? new DashboardVerdict(AttendanceDashboardStatus.OnTime, null)
                : new DashboardVerdict(AttendanceDashboardStatus.Late, WholeMinutes.AtLeastZero(arrived - planStart));
        }

        if (HasNotStarted(plan, today, now))
        {
            return new DashboardVerdict(AttendanceDashboardStatus.NotStarted, null);
        }

        /*
          Tasdiqlangan so'rov «kelmadi» ni oqlaydi — va faqat uni. Smenasi
          hali boshlanmagan kun tegilmaydi: u hali savol emas, ya'ni javob
          ham kerak emas (tabeldagi `AttendanceDayBuilder` bilan bir xil
          tor qoida).
        */
        return new DashboardVerdict(
            excused ? AttendanceDashboardStatus.Excused : AttendanceDashboardStatus.Absent,
            null);
    }

    /// <summary>Kunning rejasi hali boshlanmaganmi — «Hali boshlanmagan» ning o'zi.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Panel VA tabel uchun BITTA qoida.</b> <see cref="AttendanceDayBuilder"/>
    /// o'tishsiz ish kunini shu shart bilan «Kelmadi» emas, <c>Planned</c> deb
    /// hisoblaydi. Ikki joyda ikki xil yozilganda ertalab 08:00 da panel «Hali
    /// boshlanmagan», tabel, xodim ilovasi va hisobotlar esa «Kelmadi»
    /// ko'rsatardi.
    /// </para>
    /// <para>
    /// Kelajak sana — doim boshlanmagan. Bugun — reja boshlanishi lahzasigacha
    /// (yon berish kutilmaydi, <see cref="Classify"/> izohidagi egasining
    /// qoidasi). O'tgan sana — hech qachon: kechagi reja boshlanishi har doim
    /// hozirdan oldin.
    /// </para>
    /// <para>
    /// ⭐ <b>Erkin kun</b> (SPEC-erkin-jadval §2.2, 13-qator) — eng kech kelish lahzasigacha
    /// (<see cref="DayPlan.LatestStartAt"/> = oyna tugashi − norma): 08:30–18:00, 7:30 da
    /// 10:30 gacha «Hali boshlanmagan», keyin «Kelmadi». Sana emas, LAHZA bo'yicha: tungi
    /// smenaning eng kech kelishi ertangi sanaga tushishi mumkin (20:00–08:00, 4 soat →
    /// seshanba 04:00). Borliq oynaga kesilmasa ham (egasi, 2026-10-20) chegara shu: oynadan
    /// keyin ham ishlab normani to'ldirish mumkin, lekin kelmagan odam qachondir «Kelmadi»
    /// bo'lishi kerak — keyinroq kelsa, holat o'tishning o'zidan qayta hisoblanadi.
    /// </para>
    /// </remarks>
    /// <param name="plan">Kun rejasi.</param>
    /// <param name="today">Bugungi Toshkent sanasi.</param>
    /// <param name="now">Hozirgi lahza.</param>
    public static bool HasNotStarted(DayPlan plan, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Date > today)
        {
            return true;
        }

        return plan.IsFlexible
            ? plan.LatestStartAt is { } latestStart && now < latestStart
            : plan.Date == today && plan.PlanStartAt is { } planStart && now < planStart;
    }
}
