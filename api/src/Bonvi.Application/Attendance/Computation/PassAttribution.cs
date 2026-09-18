using Bonvi.Application.Attendance.Scheduling;

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
/// yo'q ⇒ o'z sanasi» deydi, va oynalar orasiga tushgan o'tish uchun
/// ham bu yagona halol javob: uni eng yaqin smenaga «yopishtirish»
/// hech kim ko'rmaydigan taxmin bo'lardi.
/// </para>
/// </remarks>
public static class PassAttribution
{
    /// <summary>O'tish tegishli bo'lgan sana.</summary>
    /// <param name="pass">O'tish.</param>
    /// <param name="planFor">
    /// Xodimning shu sanadagi rejasi; munosabati yoki jadvali yo'q sanada <c>null</c>.
    /// </param>
    public static DateOnly DateFor(PassReading pass, Func<DateOnly, DayPlan?> planFor)
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

        return best ?? own;
    }
}
