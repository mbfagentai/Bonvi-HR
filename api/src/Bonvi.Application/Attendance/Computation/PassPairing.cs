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
/// <param name="ClosedMinutes">
/// Yopilgan oraliqlar yig'indisi, tanaffus ayrilmasdan — erta kelish va kech ketish chegaralari
/// (<see cref="DayPlan.UncountedBands"/>) kesib tashlangandan keyin.
/// </param>
/// <param name="BreakTakenMinutes">Tanaffus oynasi ichida binodan tashqarida o'tgan daqiqalar.</param>
/// <param name="PresentInWindowMinutes">Tanaffus oynasi ichida binoda o'tgan daqiqalar (faqat yopilgan oraliqlar).</param>
/// <param name="BreakDeductionMinutes">Ishlangan vaqtdan ayrilgan tanaffus.</param>
/// <param name="WorkedMinutes">Ishlangan vaqt: <paramref name="ClosedMinutes"/> − <paramref name="BreakDeductionMinutes"/>.</param>
/// <param name="MissingEntry">Yetim chiqish topildi.</param>
/// <param name="MissingExit">Yopilmagan oraliq qoldi.</param>
/// <param name="AutoExitAt">
/// Oxirgi ochiq oraliq shu lahzada AVTOMATIK yopildi (jadvalsiz kunning 22:00 i —
/// <see cref="AttendanceRules.NoScheduleAutoExitTime"/>); yopilmagan bo'lsa <c>null</c>.
/// </param>
/// <param name="ClosedIntervals">
/// Yopilgan oraliqlarning HISOBLANADIGAN qismlari (UTC), vaqt tartibida — chegara bo'laklari
/// kesilgan. «Ish vaqti» so'rovi va shaxsiy vaqt shular bilan ishlaydi
/// (<see cref="WorkCreditRule.Apply"/>, <see cref="PersonalTimeRule.Apply"/>). Saqlanmaydi.
/// </param>
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
    bool MissingExit,
    DateTimeOffset? AutoExitAt = null,
    IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)>? ClosedIntervals = null);

/// <summary>
/// O'tishlarni kirish–chiqish oraliqlariga juftlaydi va ishlangan vaqtni hisoblaydi — SPEC §5.5, §5.5.1.
/// </summary>
/// <remarks>
/// <para>
/// Tartib me'yoriy: saralash (lahza, keyin identifikator) → yo'nalishi
/// ma'lum takrorlarni tashlash → noma'lum yo'nalishni navbat bilan
/// to'ldirish → oraliqlar → tanaffus → ishlangan vaqt. Qadamlarning o'rni
/// almashsa natija o'zgaradi: masalan navbat takror tashlanishidan OLDIN
/// qo'yilsa, ikki marta o'qilgan bitta kirish «kirdi-chiqdi» bo'lib qolardi.
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
    /// <b>Ketma-ket ikki kirish yoki ikki chiqish</b> — yo'nalishi MA'LUM bo'lsa,
    /// ular 2-qadamda bittaga qisqaradi (birinchi kirish, oxirgi chiqish —
    /// egasining qarori, 2026-09-27, <see cref="CollapseBounces"/>). Bu yerga
    /// faqat yo'nalishi navbat bilan TAXMIN qilingan o'tish aralashgan holat
    /// yetib keladi (masalan telefonda «Kirish», keyin umumiy terminal): ochiq
    /// oraliq ustiga yana kirish — oldingi oraliq tashlanadi va
    /// <c>MissingExit</c>, oraliqsiz chiqish — <c>MissingEntry</c>. Bu aralash
    /// holat ataylab o'zgartirilmagan (ochiq savol).
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
    /// ⭐ <b>Erta kelish va kech ketish chegarasi</b> (egasining qarori, 2026-10-28): reja
    /// boshlanishidan oldingi va tugashidan keyingi chegara bo'laklari
    /// (<see cref="DayPlan.UncountedBands"/>) yopilgan oraliqlardan kesib tashlanadi — ular na
    /// ishlangan vaqtga, na ortiqcha ishga tushadi. Kelish, ketish, kechikish va erta ketish
    /// lahzalari o'zgarmaydi: chegara faqat SANALADIGAN daqiqalarga tegadi.
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
    /// <param name="closeOpenAt">
    /// Kun OXIRIDAGI ochiq oraliqni shu lahzada yopish (jadvalsiz kunning avtomatik «ketdi»si):
    /// oraliq ishlangan vaqtga qo'shiladi, lahza <c>LastOutAt</c> bo'ladi. Oraliq bu lahzadan
    /// keyin ochilgan bo'lsa — yopilmaydi. Kun o'rtasida tashlab ketilgan kirish (ketma-ket ikki
    /// kirish) bunga tegishli emas: u <c>MissingExit</c> bo'lib qoladi.
    /// </param>
    public static PairingResult Pair(IEnumerable<PassReading> passes, DayPlan? plan, DateTimeOffset? closeOpenAt = null)
    {
        ArgumentNullException.ThrowIfNull(passes);

        // 1-qadam — yo'nalish o'tishning o'zida turibdi; `Unknown` hozircha shundayligicha.
        var ordered = passes.OrderBy(pass => pass.OccurredAt).ThenBy(pass => pass.Id).ToList();
        var kept = CollapseBounces(ordered);
        var directed = InferDirections(kept);

        BreakWindow? window = plan is { BreakWindowStartAt: { } windowFrom, BreakWindowEndAt: { } windowTo }
            ? new BreakWindow(windowFrom, windowTo, plan.BreakMinutes)
            : null;
        var uncounted = plan?.UncountedBands ?? [];

        DateTimeOffset? openedAt = null;
        DateTimeOffset? leftAt = null;
        DateTimeOffset? firstInAt = null;
        DateTimeOffset? lastOutAt = null;
        long closedTicks = 0;
        var closedIntervals = new List<(DateTimeOffset From, DateTimeOffset To)>();
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

            closedTicks += AddCounted(closedIntervals, enteredAt, pass.At, uncounted);
            leftAt = pass.At;
            openedAt = null;
        }

        DateTimeOffset? autoExitAt = null;

        if (openedAt is { } stillOpen)
        {
            if (closeOpenAt is { } closeAt && closeAt > stillOpen)
            {
                if (window is { } breakWindow)
                {
                    if (leftAt is { } outsideFrom)
                    {
                        outsideInWindowTicks += breakWindow.OverlapTicks(outsideFrom, stillOpen);
                    }

                    presentInWindowTicks += breakWindow.OverlapTicks(stillOpen, closeAt);
                }

                closedTicks += AddCounted(closedIntervals, stillOpen, closeAt, uncounted);
                lastOutAt = closeAt;
                autoExitAt = closeAt;
            }
            else
            {
                missingExit = true;
            }
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
            MissingExit: missingExit,
            AutoExitAt: autoExitAt,
            ClosedIntervals: closedIntervals);
    }

    /// <summary>
    /// O'tishlar OCHIQ oraliq bilan tugaydimi — takrorlar tashlanib, yo'nalish to'ldirilgandan
    /// keyin oxirgisi kirish.
    /// </summary>
    /// <remarks>
    /// Hech bir kun oynasiga tushmagan o'tish kechagi smenaning chiqishimi degan savol
    /// uchun (<see cref="PassAttribution.LateExitDateFor"/>). Qadamlar <see cref="Pair"/>
    /// dagi bilan AYNAN bir xil — «ochiq» ikki joyda ikki xil tushunilmasin.
    /// </remarks>
    /// <param name="passes">Bir kunning o'tishlari, istalgan tartibda.</param>
    public static bool EndsOpen(IEnumerable<PassReading> passes)
    {
        ArgumentNullException.ThrowIfNull(passes);

        var ordered = passes.OrderBy(pass => pass.OccurredAt).ThenBy(pass => pass.Id).ToList();
        var directed = InferDirections(CollapseBounces(ordered));

        return directed.Count > 0 && directed[^1].Direction == PassDirection.In;
    }

    /// <summary>2-qadam: yo'nalishi ma'lum ketma-ket takrorlarni tashlaydi — birinchi kirish, oxirgi chiqish.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Egasining qarori (2026-09-27).</b> Kirish va chiqish alohida
    /// terminallarda bo'lsa, «kirish, kirish» yoki «chiqish, chiqish» ketma-ket
    /// ikki voqea bo'la olmaydi — orasidagi vaqtdan QAT'I NAZAR (ilgari faqat
    /// 60 soniya ichidagisi tashlanardi). Qoida:
    /// </para>
    /// <list type="bullet">
    ///   <item>chiqishsiz takror KIRISH — birinchi kirishning nusxasi: ENG ERTASI
    ///         qoladi (odam 09:00 da kirgan, 09:40 dagi o'qish uni kechroq kelgan
    ///         qilmasligi kerak);</item>
    ///   <item>kirishsiz takror CHIQISH — oldingisining o'rnini oladi: ENG KECHI
    ///         qoladi (17:00 da chiqib, qaytib, 18:05 da yana chiqqan odam 18:05
    ///         da ketgan).</item>
    /// </list>
    /// <para>
    /// Tashlangan o'tish o'chirilmaydi: u jurnalda qoladi va
    /// <see cref="PairingResult.IgnoredPassCount"/> ga tushadi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Yo'nalishi <c>Unknown</c> o'qish ma'lum yo'nalishni yutmaydi va undan yutilmaydi</b>
    /// (egasining qarori, 2026-09-27): bitta umumiy terminalda navbat bilan kirish/chiqish
    /// bo'laveradi. <b>Bitta istisno</b> (egasi, 2026-10-30): oldingi o'qish ham noma'lum va
    /// orasi <see cref="AttendanceRules.UnknownRepeatSeconds"/> (15 s) dan oshmasa — bu bitta
    /// o'tishning takror o'qilishi, tashlanadi (zanjir: har o'qish o'zidan oldingisi bilan
    /// solishtiriladi). 20 soniya farq — hamon ikki o'tish. Ma'lum va
    /// noma'lum yo'nalish aralash kelgan holat (telefonda «Kirish», keyin
    /// umumiy terminal) ham o'zgartirilmadi — u ochiq savol
    /// (<c>docs/ASSUMPTIONS.md</c>, 2026-09-27).
    /// </para>
    /// </remarks>
    private static List<PassReading> CollapseBounces(List<PassReading> ordered)
    {
        var kept = new List<PassReading>(ordered.Count);
        PassReading? previous = null;

        foreach (var pass in ordered)
        {
            var repeat = previous is { } prior
                && pass.Direction == PassDirection.Unknown
                && prior.Direction == PassDirection.Unknown
                && pass.OccurredAt - prior.OccurredAt <= TimeSpan.FromSeconds(AttendanceRules.UnknownRepeatSeconds);

            previous = pass;

            if (repeat)
            {
                continue;
            }

            if (kept.Count > 0
                && pass.Direction != PassDirection.Unknown
                && pass.Direction == kept[^1].Direction)
            {
                if (pass.Direction == PassDirection.Out)
                {
                    // Oxirgi chiqish yutadi — oldingisi tashlanganlar qatoriga o'tadi.
                    kept[^1] = pass;
                }

                continue;
            }

            kept.Add(pass);
        }

        return kept;
    }

    /// <summary>
    /// Hisob-kitobda ishlatiladigan o'tishlar — <see cref="Pair"/> ning 2-qadamidan o'tganlari (SPEC-qolda-belgi §B).
    /// </summary>
    /// <remarks>
    /// Tashlangan takrorlar (15 soniyalik qayta o'qish, ketma-ket bir xil ma'lum yo'nalish) bu
    /// to'plamga kirmaydi — kun oynasida ular «hisobga olinmagan» bo'lib ko'rinadi.
    /// </remarks>
    /// <param name="passes">Bitta xodimning bitta kundagi o'tishlari (tartib shart emas).</param>
    public static IReadOnlySet<Guid> CountedPassIds(IEnumerable<PassReading> passes)
    {
        ArgumentNullException.ThrowIfNull(passes);

        var ordered = passes.OrderBy(pass => pass.OccurredAt).ThenBy(pass => pass.Id).ToList();
        return CollapseBounces(ordered).Select(pass => pass.Id).ToHashSet();
    }

    /// <summary>
    /// Har bir o'tishning hisob-kitobdagi yo'nalishi — <see cref="Pair"/> bilan AYNAN bir xil qadamlar.
    /// </summary>
    /// <remarks>
    /// «O'tishlar» ro'yxati uchun (egasi, 2026-10-12): yo'nalishi berilmagan terminalda
    /// har bir qatorda «Noma'lum» emas, navbat bilan aniqlangan yo'nalish ko'rinsin.
    /// Bir kishining bir kunlik o'tishlari beriladi. Qurilma aytgan yo'nalish
    /// o'zgarmaydi; tashlangan takror (bir xil ma'lum yo'nalish) ham o'z yo'nalishida qoladi.
    /// </remarks>
    /// <param name="passes">Bitta xodimning bitta kundagi o'tishlari (tartib shart emas).</param>
    /// <returns>O'tish → yo'nalish.</returns>
    public static IReadOnlyDictionary<Guid, PassDirection> EffectiveDirections(IEnumerable<PassReading> passes)
    {
        ArgumentNullException.ThrowIfNull(passes);

        var ordered = passes.OrderBy(pass => pass.OccurredAt).ThenBy(pass => pass.Id).ToList();
        var result = ordered.ToDictionary(pass => pass.Id, pass => pass.Direction);
        var expected = PassDirection.In;

        foreach (var pass in CollapseBounces(ordered))
        {
            var direction = pass.Direction == PassDirection.Unknown ? expected : pass.Direction;
            expected = direction == PassDirection.In ? PassDirection.Out : PassDirection.In;
            result[pass.Id] = direction;
        }

        // Tashlangan noma'lum takror — o'zidan oldingi o'qishning yo'nalishida: 08:50:13 «Kirish»
        // bo'lsa, 08:50:16 dagi takror ham «Kirish» (ro'yxatda «Noma'lum» emas)
        for (var index = 1; index < ordered.Count; index++)
        {
            if (result[ordered[index].Id] == PassDirection.Unknown)
            {
                result[ordered[index].Id] = result[ordered[index - 1].Id];
            }
        }

        return result;
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

    /// <summary>
    /// Oraliqning chegara bo'laklaridan tashqaridagi qismlarini <paramref name="counted"/> ga qo'shadi
    /// va ularning uzunligini (tick) qaytaradi.
    /// </summary>
    /// <remarks>
    /// 09:00–18:00 smena, chegaralar 30 va 60 daqiqa: 08:00–18:30 oralig'idan 08:00–08:30 va
    /// 09:00–18:00 qoladi — 08:30–09:00 va 18:00–18:30 sanalmaydi. Bo'laklar reja tashqarisida
    /// va bir-birini kesmaydi, shuning uchun ketma-ket kesish yetadi.
    /// </remarks>
    private static long AddCounted(
        List<(DateTimeOffset From, DateTimeOffset To)> counted,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> uncounted)
    {
        var pieces = new List<(DateTimeOffset From, DateTimeOffset To)> { (from, to) };

        foreach (var band in uncounted)
        {
            var rest = new List<(DateTimeOffset From, DateTimeOffset To)>(pieces.Count + 1);

            foreach (var piece in pieces)
            {
                if (band.To <= piece.From || band.From >= piece.To)
                {
                    rest.Add(piece);
                    continue;
                }

                if (piece.From < band.From)
                {
                    rest.Add((piece.From, band.From));
                }

                if (band.To < piece.To)
                {
                    rest.Add((band.To, piece.To));
                }
            }

            pieces = rest;
        }

        long ticks = 0;

        foreach (var piece in pieces)
        {
            ticks += (piece.To - piece.From).Ticks;
            counted.Add(piece);
        }

        return ticks;
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
