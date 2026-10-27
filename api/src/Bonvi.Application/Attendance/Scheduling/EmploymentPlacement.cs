using Bonvi.Domain.Employees;

namespace Bonvi.Application.Attendance.Scheduling;

/// <summary>
/// Munosabatning ANIQ SANADAGI bo'limi va lavozimi — mehnat yo'lidagi ko'chirish hodisalaridan.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega munosabatning o'z maydoni yetmaydi.</b> <c>Employment.Transfer</c>
/// <see cref="Employment.DepartmentId"/> va <see cref="Employment.PositionId"/> ni
/// JOYIDA almashtiradi; kuchga kirish sanasi esa faqat
/// <see cref="EmploymentEvent"/> da yashaydi. Joriy qiymat har bir sanaga
/// qo'llansa, 20-sentabrdan 08:00–17:00 dan 09:00–18:00 ga ko'chgan odamning
/// 19-sentabri tungi qayta hisoblashda yangi jadval bilan qurilib, «50 daqiqa
/// erta ketdi» bo'lib qolardi, hisobotlar esa o'tgan kunni yangi bo'limga
/// ko'chirib yuborardi (SPEC §2.6 surati). Oldinga sanalangan ko'chirish
/// esa aksincha — buyruq kuchga kirishidan oldin qo'llanardi.
/// </para>
/// <para>
/// <b>Qoida</b> — har bir maydon (bo'lim, lavozim) ALOHIDA, faqat o'sha maydonni
/// haqiqatan o'zgartirgan <see cref="EmploymentEventType.Transfer"/> /
/// <see cref="EmploymentEventType.Promotion"/> hodisalari bo'yicha (kuchga kirish
/// sanasi, keyin yozilgan vaqti tartibida):
/// </para>
/// <list type="number">
///   <item>bunday hodisa yo'q — joriy qiymat;</item>
///   <item>sana oxirgi o'zgarishning kuchga kirish sanasidan KEYIN yoki o'sha kuni —
///         joriy qiymat (keyin joyida qilingan tuzatish ham shu yerda ko'rinadi);</item>
///   <item>sanagacha kuchga kirgan o'zgarish bor — eng oxirgisining «yangi» qiymati;</item>
///   <item>aks holda (sana birinchi o'zgarishdan oldin) — birinchisining «eski» qiymati.</item>
/// </list>
/// <para>
/// Ko'chirish kuchga kiradigan KUNNING o'zi — allaqachon yangi joy: buyruq
/// «20-sentabrdan» deydi.
/// </para>
/// <para>
/// ⚠️ <b>Hodisalar yuklangan bo'lishi SHART</b> (<c>Include(Events)</c>).
/// Yuklanmasa natija jimgina eski xatti-harakatga — joriy qiymatga — qaytadi:
/// hodisasiz munosabat (masalan Excel'dan ko'chirilgani) ham xuddi shunday
/// ko'rinadi va ularni farqlab bo'lmaydi. Shu sababli jadval hal qiluvchisiga
/// munosabat beradigan HAR BIR yuklovchi hodisalarni ham o'qiydi.
/// </para>
/// <para>
/// Excel'dan ko'chirilgan hodisada «eski» qiymat bo'sh bo'lishi mumkin
/// (noma'lum) — o'shanda oldingi o'zgarishning «yangi» qiymati, u ham bo'lmasa
/// joriy qiymat olinadi: taxmin yasalmaydi, faqat ma'lum narsa ishlatiladi.
/// </para>
/// </remarks>
/// <param name="DepartmentId">Shu sanadagi bo'lim.</param>
/// <param name="PositionId">Shu sanadagi lavozim.</param>
public readonly record struct EmploymentPlacement(Guid? DepartmentId, Guid? PositionId)
{
    /// <summary>Munosabatning <paramref name="date"/> dagi bo'limi va lavozimi.</summary>
    /// <param name="employment">Hodisalari bilan yuklangan munosabat.</param>
    /// <param name="date">Toshkent sanasi.</param>
    public static EmploymentPlacement On(Employment employment, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(employment);

        if (employment.Events.Count == 0)
        {
            return new EmploymentPlacement(employment.DepartmentId, employment.PositionId);
        }

        var moves = employment.Events
            .Where(item => item.Type is EmploymentEventType.Transfer or EmploymentEventType.Promotion)
            .OrderBy(item => item.EffectiveDate)
            .ThenBy(item => item.RecordedAt)
            .ThenBy(item => item.Id)
            .ToList();

        return new EmploymentPlacement(
            ValueOn(date, employment.DepartmentId, moves, item => (item.FromDepartmentId, item.ToDepartmentId)),
            ValueOn(date, employment.PositionId, moves, item => (item.FromPositionId, item.ToPositionId)));
    }

    /// <summary>
    /// Shu sanani qamraydigan munosabatlardan birining SHU SANADAGI bo'limi to'plamdami.
    /// </summary>
    /// <remarks>
    /// Munosabat asosiy bo'lishi shart emas, ishdan ketish kuni ham qamraladi
    /// (<see cref="PrimaryEmployment.Covers"/>); o'chirilgani hisobga olinmaydi. Bo'lim
    /// boshlig'ining «a'zomi» qoidasi (SPEC-jamoam §B) — ilova ham, panel ham shu metoddan.
    /// </remarks>
    /// <param name="employments">Bitta xodimning munosabatlari — hodisalari bilan.</param>
    /// <param name="date">Toshkent sanasi.</param>
    /// <param name="departmentIds">Bo'limlar to'plami.</param>
    public static bool AnyIn(IEnumerable<Employment> employments, DateOnly date, IReadOnlySet<Guid> departmentIds)
    {
        ArgumentNullException.ThrowIfNull(employments);
        ArgumentNullException.ThrowIfNull(departmentIds);

        return employments.Any(employment =>
            employment.DeletedAt is null &&
            PrimaryEmployment.Covers(employment, date) &&
            On(employment, date).DepartmentId is { } departmentId &&
            departmentIds.Contains(departmentId));
    }

    /// <summary>Bitta maydonning sanadagi qiymati — sinf izohidagi to'rt qadam.</summary>
    private static Guid? ValueOn(
        DateOnly date,
        Guid? current,
        List<EmploymentEvent> moves,
        Func<EmploymentEvent, (Guid? From, Guid? To)> field)
    {
        // Faqat shu maydonni haqiqatan o'zgartirganlari: bo'lim ko'chmasdan
        // lavozim almashgan hodisada bo'limning «eski» va «yangi» si teng.
        var changes = moves
            .Select(item => (item.EffectiveDate, Value: field(item)))
            .Where(item => item.Value.To is not null && item.Value.To != item.Value.From)
            .ToList();

        if (changes.Count == 0 || date >= changes[^1].EffectiveDate)
        {
            return current;
        }

        var lastApplied = changes.FindLastIndex(item => item.EffectiveDate <= date);

        if (lastApplied >= 0)
        {
            return changes[lastApplied].Value.To;
        }

        return changes[0].Value.From ?? current;
    }
}
