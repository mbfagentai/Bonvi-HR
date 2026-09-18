using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Computation;

/// <summary>
/// Bir kunning o'tishlarini juftlash natijasi — SPEC §5.5, 1–5-qadamlar.
/// </summary>
/// <param name="PassCount">Kunga tegishli BARCHA o'tishlar, sakrashlar ham.</param>
/// <param name="IgnoredPassCount">Sakrash deb tashlanganlari.</param>
/// <param name="FirstInAt">Birinchi kirish (UTC).</param>
/// <param name="LastOutAt">Oxirgi chiqish (UTC).</param>
/// <param name="ClosedMinutes">Yopilgan oraliqlar yig'indisi, tanaffus ayrilmasdan.</param>
/// <param name="BreakTakenMinutes">Tanaffus oynasi ichida binodan tashqarida o'tgan daqiqalar.</param>
/// <param name="PresentInWindowMinutes">Tanaffus oynasi ichida binoda o'tgan daqiqalar (faqat yopilgan oraliqlar).</param>
/// <param name="BreakDeductionMinutes">Ishlangan vaqtdan ayrilgan tanaffus.</param>
/// <param name="WorkedMinutes">Ishlangan vaqt: <paramref name="ClosedMinutes"/> − <paramref name="BreakDeductionMinutes"/>.</param>
/// <param name="MissingEntry">Yetim chiqish topildi.</param>
/// <param name="MissingExit">Yopilmagan oraliq qoldi.</param>
public sealed record PairingResult(
    int PassCount,
    int IgnoredPassCount,
    DateTimeOffset? FirstInAt,
    DateTimeOffset? LastOutAt,
    int ClosedMinutes,
    int BreakTakenMinutes,
    int PresentInWindowMinutes,
    int BreakDeductionMinutes,
    int WorkedMinutes,
    bool MissingEntry,
    bool MissingExit);

/// <summary>
/// O'tishlarni kirish–chiqish oraliqlariga juftlaydi va ishlangan vaqtni hisoblaydi — SPEC §5.5, §5.5.1.
/// </summary>
/// <remarks>
/// <para>
/// Tartib me'yoriy: saralash (lahza, keyin identifikator) → sakrashlarni
/// tashlash → noma'lum yo'nalishni navbat bilan to'ldirish → oraliqlar →
/// tanaffus → ishlangan vaqt. Qadamlarning o'rni almashsa natija
/// o'zgaradi: masalan navbat sakrash tashlanishidan OLDIN qo'yilsa, ikki
/// marta o'qilgan bitta kirish «kirdi-chiqdi» bo'lib qolardi.
/// </para>
/// <para>
/// ⭐ <b>Hech narsa o'tishga qaytib yozilmaydi.</b> Taxmin qilingan
/// yo'nalish va tashlangan sakrash faqat shu hisoblashda yashaydi — jadval
/// tuzatilib qayta hisoblanganda tarix o'zgarmasligi kerak (SPEC §2.3).
/// </para>
/// </remarks>
public static class PassPairing
{
    /// <summary>Bir kunning o'tishlarini juftlaydi.</summary>
    /// <remarks>
    /// <para>
    /// <b>Oraliqlar.</b> Kirish oraliq ochadi, chiqish uni yopadi. Ochiq
    /// oraliqsiz chiqish — yetim (<c>MissingEntry</c>). Kun oxirigacha
    /// yopilmagan oraliq — <c>MissingExit</c>. Ikkalasi ham daqiqa bermaydi:
    /// qachon kirgani yoki chiqqani noma'lum vaqtni «ishlangan» deb
    /// bo'lmaydi.
    /// </para>
    /// <para>
    /// <b>Ochiq oraliq ustiga yana kirish</b> (sakrash oynasidan tashqarida)
    /// — oldingi oraliqning chiqishi yozilmagan. U tashlab ketiladi, daqiqa
    /// bermaydi va <c>MissingExit</c> belgilanadi; ketma-ket ikki chiqishda
    /// <c>MissingEntry</c> belgilangani bilan aynan simmetrik. SPEC bu holatni
    /// alohida aytmaydi. Rad etilgan muqobil — ikkinchi kirishni e'tiborsiz
    /// qoldirib, oraliqni birinchisidan hisoblash: u biz bilmagan vaqtni
    /// ishlangan deb yozardi va anomaliyani ham yashirardi.
    /// </para>
    /// <para>
    /// ⭐ <b>Tanaffus</b> (egasining qarori, SPEC §5.5.1 va §13 #14):
    /// </para>
    /// <code>
    /// breakTaken      = oyna ichida TASHQARIDA o'tgan daqiqalar
    /// presentInWindow = oyna ichida YOPILGAN oraliqlarda o'tgan daqiqalar
    /// breakDeduction  = max(0, min(break_minutes − breakTaken, presentInWindow))
    /// worked          = yopilgan oraliqlar − breakDeduction
    /// </code>
    /// <para>
    /// «Tashqarida» — faqat ikki KETMA-KET YOPILGAN oraliq orasidagi bo'shliq.
    /// Birinchi kirishdan oldingi bosh ham, oxirgi chiqishdan keyingi dum
    /// ham hisoblanmaydi: 13:30 da kelgan odam 13:00 dan tanaffusda emas
    /// edi — hali kelmagan edi; 13:45 da chiqib ketgan odam uzun tushlik
    /// qilmadi — uyiga ketdi, aks holda erta ketish sonni YAXSHILARDI.
    /// Yopilmay qolgan oraliq ham bo'shliqni yopmaydi: «09:00, 13:00, 14:00,
    /// chiqish yo'q» kunida <c>breakTaken = 0</c> (SPEC §5.7, §11.1 #59).
    /// </para>
    /// <para>
    /// <b>Nega <c>presentInWindow</c> bilan cheklanadi.</b> Hech kimdan
    /// binoda bo'lmagan paytidagi tushlik ushlanmaydi: tushlikdan oldin
    /// uyiga ketgan odamdan 0, oynaning yarmida kelgandan 30. Faqat
    /// YOPILGAN oraliqlar sanaladi, shuning uchun ushlanma hech qachon
    /// ishlangan vaqtdan oshmaydi va chiqishi yozilmagan oraliq na daqiqa,
    /// na ushlanma beradi. Oyna yo'q bo'lsa — ushlanma umuman yo'q: oynasiz
    /// «13:10 dagi chiqish tushlikmi yoki ish bilanmi» degan savolga javob
    /// yo'q (SPEC §5.5.1).
    /// </para>
    /// <para>
    /// ⚠️ Har bir yig'indi AVVAL aniq lahzalarda (tick) qo'shiladi, KEYIN
    /// bir marta butun daqiqaga kesiladi (<see cref="WholeMinutes"/>).
    /// Bo'laklarni alohida kesish nafaqat daqiqalarni yo'qotadi, balki
    /// <c>presentInWindow</c> ni yopilgan yig'indidan bir daqiqaga KATTA
    /// qilib qo'yishi mumkin — va o'shanda ushlanma ishlangan vaqtdan
    /// oshib, <c>breakDeduction ≤ presentInWindow ≤ yopilgan yig'indi</c>
    /// kafolati buziladi.
    /// </para>
    /// </remarks>
    /// <param name="passes">Shu kunga tegishli o'tishlar, istalgan tartibda.</param>
    /// <param name="plan">Kun rejasi; jadval yo'q bo'lsa <c>null</c>. Tanaffus faqat shu yerdan olinadi.</param>
    public static PairingResult Pair(IEnumerable<PassReading> passes, DayPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(passes);

        // 1-qadam — yo'nalish o'tishning o'zida turibdi; `Unknown` hozircha shundayligicha.
        var ordered = passes.OrderBy(pass => pass.OccurredAt).ThenBy(pass => pass.Id).ToList();
        var kept = CollapseBounces(ordered);
        var directed = InferDirections(kept);

        BreakWindow? window = plan is { BreakWindowStartAt: { } windowFrom, BreakWindowEndAt: { } windowTo }
            ? new BreakWindow(windowFrom, windowTo, plan.BreakMinutes)
            : null;

        DateTimeOffset? openedAt = null;
        DateTimeOffset? leftAt = null;
        DateTimeOffset? firstInAt = null;
        DateTimeOffset? lastOutAt = null;
        long closedTicks = 0;
        long outsideInWindowTicks = 0;
        long presentInWindowTicks = 0;
        var missingEntry = false;
        var missingExit = false;

        foreach (var pass in directed)
        {
            if (pass.Direction == PassDirection.In)
            {
                firstInAt ??= pass.At;

                // Oldingi oraliq hali ochiq — uning chiqishi yozilmagan.
                missingExit |= openedAt is not null;
                openedAt = pass.At;
                continue;
            }

            lastOutAt = pass.At;

            if (openedAt is not { } enteredAt)
            {
                missingEntry = true;
                continue;
            }

            if (window is { } breakWindow)
            {
                // Bo'shliq faqat SHU oraliq yopilganda sanaladi: oldingi yopilgan
                // oraliqning oxiridan shu oraliqning boshigacha. Orasidagi yetim
                // chiqish yoki tashlab ketilgan kirish chegarani siljitmaydi.
                if (leftAt is { } outsideFrom)
                {
                    outsideInWindowTicks += breakWindow.OverlapTicks(outsideFrom, enteredAt);
                }

                presentInWindowTicks += breakWindow.OverlapTicks(enteredAt, pass.At);
            }

            closedTicks += (pass.At - enteredAt).Ticks;
            leftAt = pass.At;
            openedAt = null;
        }

        if (openedAt is not null)
        {
            missingExit = true;
        }

        var closedMinutes = WholeMinutes.AtLeastZero(TimeSpan.FromTicks(closedTicks));
        var breakTaken = WholeMinutes.AtLeastZero(TimeSpan.FromTicks(outsideInWindowTicks));
        var presentInWindow = WholeMinutes.AtLeastZero(TimeSpan.FromTicks(presentInWindowTicks));

        var breakDeduction = window is { } deductible
            ? Math.Max(0, Math.Min(deductible.BreakMinutes - breakTaken, presentInWindow))
            : 0;

        return new PairingResult(
            PassCount: ordered.Count,
            IgnoredPassCount: ordered.Count - kept.Count,
            FirstInAt: firstInAt,
            LastOutAt: lastOutAt,
            ClosedMinutes: closedMinutes,
            BreakTakenMinutes: breakTaken,
            PresentInWindowMinutes: presentInWindow,
            BreakDeductionMinutes: breakDeduction,
            // Ayirma manfiy bo'lolmaydi: ushlanma `presentInWindow` dan oshmaydi,
            // u esa yopilgan oraliqlarning bir qismi.
            WorkedMinutes: closedMinutes - breakDeduction,
            MissingEntry: missingEntry,
            MissingExit: missingExit);
    }

    /// <summary>2-qadam: sakrashlarni tashlaydi.</summary>
    /// <remarks>
    /// Oxirgi SAQLANGAN o'tish bilan solishtiriladi, oxirgi o'qilgan bilan
    /// emas: aks holda har 50 soniyada qayta o'qilgan uchta kirish
    /// zanjir bo'lib, ikki daqiqalik farqni ham «bitta o'tish» qilardi.
    /// Chegara ichida: roppa-rosa <see cref="AttendanceRules.BounceSeconds"/>
    /// soniya — hali sakrash. <c>Unknown</c> hech narsani yutmaydi va
    /// yutilmaydi — yo'nalishi noma'lum ikki o'qish bir xil ekani ma'lum emas.
    /// </remarks>
    private static List<PassReading> CollapseBounces(List<PassReading> ordered)
    {
        var bounce = TimeSpan.FromSeconds(AttendanceRules.BounceSeconds);
        var kept = new List<PassReading>(ordered.Count);

        foreach (var pass in ordered)
        {
            if (kept.Count > 0
                && pass.Direction != PassDirection.Unknown
                && pass.Direction == kept[^1].Direction
                && pass.OccurredAt - kept[^1].OccurredAt <= bounce)
            {
                continue;
            }

            kept.Add(pass);
        }

        return kept;
    }

    /// <summary>3-qadam: noma'lum yo'nalishni navbat bilan to'ldiradi.</summary>
    /// <remarks>
    /// Kunning birinchi hodisasi — kirish, keyin chiqish, kirish… Ma'lum
    /// yo'nalish HECH QACHON ustidan yozilmaydi; navbat faqat bo'shliqni
    /// to'ldiradi va juftlikni oxirgi ma'lum qiymatdan qayta boshlaydi.
    /// </remarks>
    private static List<DirectedPass> InferDirections(List<PassReading> kept)
    {
        var directed = new List<DirectedPass>(kept.Count);
        var expected = PassDirection.In;

        foreach (var pass in kept)
        {
            var direction = pass.Direction == PassDirection.Unknown ? expected : pass.Direction;
            expected = direction == PassDirection.In ? PassDirection.Out : PassDirection.In;

            // UTC ga keltiriladi: natija `timestamptz` ga yoziladi, u esa faqat UTC lahzani qabul qiladi.
            directed.Add(new DirectedPass(pass.OccurredAt.ToUniversalTime(), direction));
        }

        return directed;
    }

    private readonly record struct DirectedPass(DateTimeOffset At, PassDirection Direction);

    private readonly record struct BreakWindow(DateTimeOffset From, DateTimeOffset To, int BreakMinutes)
    {
        /// <summary>[from, to] oralig'ining oyna bilan kesishmasi (tick).</summary>
        public long OverlapTicks(DateTimeOffset from, DateTimeOffset to)
        {
            var start = from > From ? from : From;
            var end = to < To ? to : To;
            return end > start ? (end - start).Ticks : 0;
        }
    }
}
