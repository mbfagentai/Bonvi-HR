using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Computation;

/// <summary>
/// O'tish qaysi kunga tegishli — SPEC §5.3.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Kalendar sanasi emas, smena.</b> Dushanba 20:00 da boshlangan
/// tungi smena seshanba 08:00 da tugaydi; ikkala o'tish ham DUSHANBAGA
/// tushishi kerak, aks holda bitta smena ikki kunga bo'linib, ikkalasi
/// ham «chiqish yo'q» va «kirish yo'q» bo'lib ko'rinardi.
/// </para>
/// <para>
/// Nomzod sanalar: o'tishning o'z sanasi va uning ikki qo'shnisi. Har
/// biri uchun oyna <c>[reja boshlanishi − 6 soat, reja tugashi + 6
/// soat]</c> (ikkala chekka ham ichida). Oyna o'tishni o'z ichiga olgan
/// sana yutadi; ikkitasi olsa — MARKAZI yaqinrog'i; u ham teng bo'lsa —
/// erta sana. Olti soat erta kelishni va uzun ortiqcha ishni qoplaydi,
/// lekin 14:00 dagi o'tishni kechagi tungi smena o'ziniki qilib
/// ololmaydi.
/// </para>
/// <para>
/// <b>Oyna faqat ISH kunida bor.</b> Dam olish kunida reja boshlanishi
/// yo'q, jadvali yoki munosabati yo'q sanada esa umuman reja yo'q. Hech
/// bir oyna o'tishni qamramasa, u o'z sanasiga qoladi — SPEC «jadval
/// yo'q ⇒ o'z sanasi» deydi.
/// </para>
/// <para>
/// ⭐ <b>Bitta istisno — kechagi ochiq smenaning kech chiqishi</b> (egasining
/// qarori, 2026-09-27, <see cref="LateExitDateFor"/>). 09:00–18:00 da ishlab,
/// seshanba 00:20 da chiqqan odamning chiqishi dushanbaning oynasidan (reja
/// tugashi + 6 soat = 00:00) tashqariga tushadi. Ilgari u seshanbaga qolardi:
/// dushanba «Chiqish yo'q», seshanba esa 00:20, 08:55, 18:05 — «kirdi, chiqdi,
/// kirdi» bo'lib 515 daqiqa noto'g'ri hisoblanardi. Endi oynasiz o'tish
/// kechagi kunning chiqishi bo'ladi — faqat kecha ochiq kirish bilan tugagan
/// va bugungi smenaning oynasi hali boshlanmagan bo'lsa. Aks holda eski
/// qoida: o'z sanasi.
/// </para>
/// </remarks>
public static class PassAttribution
{
    /// <summary>O'tish tegishli bo'lgan sana — faqat kun oynalari bo'yicha, kech chiqish istisnosisiz.</summary>
    /// <remarks>
    /// Kech chiqish istisnosi kunning qolgan o'tishlarini bilishni talab qiladi,
    /// shu sababli uni <see cref="AttendanceDayBuilder"/> qo'llaydi; bu metod faqat
    /// oynalarni ko'radi.
    /// </remarks>
    /// <param name="pass">O'tish.</param>
    /// <param name="planFor">
    /// Xodimning shu sanadagi rejasi; munosabati yoki jadvali yo'q sanada <c>null</c>.
    /// </param>
    public static DateOnly DateFor(PassReading pass, Func<DateOnly, DayPlan?> planFor)
        => WindowDateFor(pass, planFor) ?? pass.LocalDate;

    /// <summary>
    /// Oynasi o'tishni qamragan sana; hech bir oyna qamramasa <c>null</c>.
    /// </summary>
    /// <param name="pass">O'tish.</param>
    /// <param name="planFor">Xodimning sanadagi rejasi.</param>
    public static DateOnly? WindowDateFor(PassReading pass, Func<DateOnly, DayPlan?> planFor)
    {
        ArgumentNullException.ThrowIfNull(planFor);

        var own = pass.LocalDate;
        DateOnly? best = null;
        var bestDistance = long.MaxValue;

        // Erta sanadan boshlab yuriladi va faqat QAT'IY yaqinrog'i
        // almashtiradi — «teng bo'lsa erta sana» qoidasi shu tartibning o'zi.
        for (var offset = -1; offset <= 1; offset++)
        {
            var candidate = own.AddDays(offset);

            if (planFor(candidate) is not { AttributionFrom: { } from, AttributionTo: { } to }
                || pass.OccurredAt < from
                || pass.OccurredAt > to)
            {
                continue;
            }

            // Markazgacha masofa IKKI BARAVAR qilib olinadi: markazning o'zi
            // yarim tickka tushishi mumkin, ikki baravar masofa esa butun son
            // bo'lib qoladi va teng holat aniq tutiladi.
            var distance = Math.Abs((2 * pass.OccurredAt.UtcTicks) - from.UtcTicks - to.UtcTicks);

            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// Oynasiz o'tish kechagi smenaning kech chiqishi bo'la oladigan sana (kecha); bo'la olmasa <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Faqat REJA bo'yicha shartlar (chaqiruvchi o'tish hech bir oynaga tushmaganini
    /// allaqachon bilgan; kechagi kun OCHIQ kirish bilan tugaganini esa u o'zi,
    /// <see cref="PassPairing.EndsOpen"/> bilan tekshiradi):
    /// </para>
    /// <list type="number">
    ///   <item>o'tish kirish EMAS (noma'lum yoki chiqish) — kirish hech qachon
    ///         kechagi kunning chiqishi bo'lmaydi;</item>
    ///   <item>kecha (o'tishning o'z sanasi − 1) ish kuni va uning oynasi
    ///         o'tishdan oldin tugagan;</item>
    ///   <item>bugungi smenaning oynasi (reja boshlanishi − <see cref="AttendanceRules.DayWindowPreHours"/>)
    ///         hali boshlanmagan. Bugun ish kuni bo'lmasa, chegara — kechagi oynaning
    ///         boshlanishidan roppa-rosa bir sutka keyin, ya'ni «bugun ham xuddi kechagi
    ///         vaqtda boshlanardi»: aks holda dam olish kunining kechqurungi o'tishi ham
    ///         bir kun oldingi smenaga yopishib, 30 soatlik «ish» bo'lardi.</item>
    /// </list>
    /// <para>
    /// Faqat bitta sutka orqaga: o'tish o'z sanasida qolsa ham, kechagi kunga o'tsa ham,
    /// bu faqat yarim tundan oshgan oraliq uchun farq qiladi. Kechagi oyna kechagi
    /// sanada tugab, o'tish ham o'sha sanada bo'lsa, «kecha» bilan «o'z sanasi» bitta kun.
    /// </para>
    /// </remarks>
    /// <param name="pass">Hech bir kun oynasiga tushmagan o'tish.</param>
    /// <param name="planFor">Xodimning sanadagi rejasi.</param>
    public static DateOnly? LateExitDateFor(PassReading pass, Func<DateOnly, DayPlan?> planFor)
    {
        ArgumentNullException.ThrowIfNull(planFor);

        if (pass.Direction == PassDirection.In)
        {
            return null;
        }

        var own = pass.LocalDate;
        var previous = own.AddDays(-1);

        if (planFor(previous) is not { AttributionFrom: { } previousFrom, AttributionTo: { } previousTo }
            || pass.OccurredAt <= previousTo)
        {
            return null;
        }

        var nextWindowFrom = planFor(own) is { AttributionFrom: { } ownFrom }
            ? ownFrom
            : previousFrom.AddDays(1);

        return pass.OccurredAt < nextWindowFrom ? previous : null;
    }
}
