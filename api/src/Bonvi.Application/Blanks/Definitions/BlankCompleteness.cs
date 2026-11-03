using System.Text.Json.Nodes;

namespace Bonvi.Application.Blanks.Definitions;

/// <summary>
/// Arizaning to'liqligi — <c>missing</c> ro'yxati (SPEC-blankalar §5.3). AI agentlar «to'liq emas» ni shundan o'qiydi.
/// </summary>
/// <remarks>
/// Hisob saqlangan javoblar ustida va YUMSHOQ: endi chegaradan chiqib qolgan eski qiymat (masalan,
/// <c>max: today</c> o'tib ketgan) xato bermaydi — to'ldirilgan hisoblanadi.
/// </remarks>
public static class BlankCompleteness
{
    /// <summary>
    /// Qaysi bo'limlar to'liqlikka kiradi: (a) qoralama bo'limlari, (b) imzolangan va ochiq qadamlar
    /// bo'limlari, (c) oxirgi guruh ochiq bo'lsa — «open» bo'limlar.
    /// </summary>
    /// <param name="definition">Ta'rif.</param>
    /// <param name="signedOrOpenStepIds">Holati Signed yoki Open bo'lgan qadamlar id'lari.</param>
    /// <param name="lastGroupOpen">Eng katta tartibli qadamlar guruhi ochiqmi.</param>
    public static IReadOnlySet<string> SectionsInScope(BlankDefinition definition, IEnumerable<string> signedOrOpenStepIds, bool lastGroupOpen)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(signedOrOpenStepIds);

        var steps = signedOrOpenStepIds.ToHashSet(StringComparer.Ordinal);

        return definition.Sections
            .Where(section => section.Fill.At switch
            {
                BlankFillAt.Draft => true,
                BlankFillAt.Step => section.Fill.Step is { } step && steps.Contains(step),
                _ => lastGroupOpen,
            })
            .Select(section => section.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Ko'rinadigan, majburiy va bo'sh maydonlar yo'llari (hujjat tartibida): <c>mentor</c>,
    /// <c>day30Decision.extra</c>, <c>questions[0].date</c>. Bo'sh = null, <c>""</c>, <c>[]</c> yoki
    /// <c>minRows</c> dan kam qatorli jadval.
    /// </summary>
    public static IReadOnlyList<string> Missing(BlankDefinition definition, JsonObject answers, IReadOnlySet<string> sectionIds)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(sectionIds);

        return BlankAnswersValidator.CollectMissing(definition, answers, sectionIds);
    }
}
