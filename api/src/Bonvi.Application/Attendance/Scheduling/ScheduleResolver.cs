using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Attendance.Scheduling;

/// <summary>
/// Hal qilingan jadval — qaysi biriktirma yutgani va u shu sanada nimani kutadi.
/// </summary>
/// <remarks>
/// Biriktirma (va demak pog'ona) natija bilan BIRGA qaytadi:
/// <c>GET /schedules/resolve</c> «nega unda aynan shu jadval» degan
/// savolga javob beradi (SPEC §6.4) va bu javobni dasturchisiz faqat
/// yutgan pog'onani ko'rsatib berish mumkin.
/// </remarks>
/// <param name="Assignment">Yutgan biriktirma.</param>
/// <param name="Schedule">Biriktirmadagi jadval.</param>
/// <param name="Day">Jadvalning shu sanaga qo'yilgan kuni.</param>
/// <param name="DateOverride">
/// <paramref name="Day"/> yillik to'rning shu SANAGA yozilgan katagidan kelgan bo'lsa — o'sha katak;
/// haftalik naqshdan kelgan bo'lsa <c>null</c>. «Nega shu soat» savoliga javob beruvchi ekran
/// (<c>GET /schedules/resolve</c>) kunni aynan shu manbadan ko'rsatadi.
/// </param>
public sealed record ScheduleResolution(
    ScheduleAssignment Assignment,
    WorkSchedule Schedule,
    DayPlan Day,
    WorkScheduleDate? DateOverride = null)
{
    /// <summary>Qaysi pog'ona yutdi.</summary>
    public ScheduleScope Tier => Assignment.Scope;
}

/// <summary>
/// Xodimning shu sanadagi jadvalini topadi — SPEC §5.2.
/// </summary>
/// <remarks>
/// <para>
/// Pog'onalar: xodim → lavozim → bo'lim (eng yaqin ota-bo'lim birinchi)
/// → sukut. Birinchi natija bergan pog'ona yutadi; pog'ona ichida eng
/// kech <c>effective_from</c>, u ham teng bo'lsa eng kech
/// <c>created_at</c>. Nega lavozim bo'limdan ustun —
/// <see cref="ScheduleScope"/> izohida.
/// </para>
/// <para>
/// <b>To'plam uchun bir marta quriladi.</b> Biriktirmalar
/// (pog'ona, obyekt) bo'yicha oldindan guruhlanadi va saralanadi, ya'ni
/// 540 xodim × 31 kun uchun har bir hal qilish — bir nechta lug'at
/// qidiruvi, ro'yxat bo'ylab qayta-qayta yurish emas.
/// </para>
/// <para>
/// ⚠️ <b><see cref="WorkSchedule.IsActive"/> hisobga OLINMAYDI.</b> SPEC
/// §5.2 bunday shart qo'ymaydi va uni qo'shish jimgina xavfli:
/// faolsizlantirilgan jadvalning xodimlari bir kechada pastki pog'onaga
/// (masalan ofis 9–18 ga) tushib, soatlari o'zgarardi va sababini hech
/// kim ko'rmasdi. Faolsizlantirish — tanlov ro'yxatidan yashirish;
/// amaldagi biriktirmani to'xtatish uchun biriktirmaning o'zi yopiladi.
/// </para>
/// </remarks>
public sealed class ScheduleResolver
{
    private readonly Dictionary<(ScheduleScope Scope, Guid? TargetId), ScheduleAssignment[]> _assignments;
    private readonly Dictionary<Guid, WorkSchedule> _schedules;
    private readonly DepartmentTree _departments;

    /// <summary>Yillik to'rning kataklari — (jadval, sana) bo'yicha.</summary>
    /// <remarks>
    /// Hal qiluvchi FAQAT so'ralgan oraliqning kataklarini oladi
    /// (chaqiruvchi shunday yuklaydi): 26 jadval × bir necha yil = o'n
    /// minglab qator va ularning hammasi hech qachon kerak bo'lmaydi.
    /// </remarks>
    private readonly Dictionary<(Guid ScheduleId, DateOnly Date), WorkScheduleDate> _dates;

    /// <summary>Hal qiluvchini bir to'plam ma'lumoti ustida quradi.</summary>
    /// <remarks>
    /// <para>
    /// Tirik biriktirma ko'rsatgan jadval berilmagan bo'lsa — ISTISNO,
    /// shu yerning o'zida. Jim yo'l (bunday biriktirmani o'tkazib yuborish)
    /// xodimni pastki pog'onaga tushirib, soatlarini hech kim sezmaydigan
    /// qilib o'zgartirardi. Jadval o'chirilishi tirik biriktirma bor
    /// paytda rad etiladi (SPEC §6.4), ya'ni bu holat faqat yuklovchi
    /// jadvallarni to'liq o'qimaganda yuz beradi — va bu yuklovchining
    /// xatosi, ma'lumotniki emas.
    /// </para>
    /// <para>
    /// O'chirilgan (soft delete) biriktirmalar chetga olinadi — EF filtri
    /// ularni baribir tashlab ketadi, bu shart filtrni chetlab o'qigan
    /// chaqiruvchidan himoya.
    /// </para>
    /// </remarks>
    /// <param name="assignments">To'plam sanalarini qamraydigan biriktirmalar (hammasini berish ham mumkin).</param>
    /// <param name="schedules">Biriktirmalar ko'rsatgan jadvallar — KUNLARI bilan.</param>
    /// <param name="departments">Bo'limlar daraxti.</param>
    /// <param name="dates">
    /// Yillik to'rning kataklari — hisoblanadigan oraliq uchun
    /// (SPEC-jadval-to'ri §B2). Berilmasa, faqat haftalik naqsh ishlaydi.
    /// </param>
    public ScheduleResolver(
        IEnumerable<ScheduleAssignment> assignments,
        IEnumerable<WorkSchedule> schedules,
        DepartmentTree departments,
        IEnumerable<WorkScheduleDate>? dates = null)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(schedules);
        ArgumentNullException.ThrowIfNull(departments);

        _schedules = schedules.ToDictionary(schedule => schedule.Id);
        _dates = dates?.ToDictionary(date => (date.WorkScheduleId, date.Date)) ?? [];
        _departments = departments;

        var live = assignments.Where(assignment => assignment.DeletedAt is null).ToList();

        var orphan = live.FirstOrDefault(assignment => !_schedules.ContainsKey(assignment.WorkScheduleId));
        if (orphan is not null)
        {
            throw new InvalidOperationException(
                $"Biriktirma {orphan.Id} ko'rsatgan jadval {orphan.WorkScheduleId} yuklanmagan.");
        }

        _assignments = live
            .GroupBy(assignment => (assignment.Scope, assignment.TargetId))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(assignment => assignment.EffectiveFrom)
                    .ThenByDescending(assignment => assignment.CreatedAt)
                    // SPEC tartibi shu yerda tugaydi; identifikator faqat
                    // natija hech qachon o'qish tartibiga bog'liq bo'lmasligi uchun.
                    .ThenByDescending(assignment => assignment.Id)
                    .ToArray());
    }

    /// <summary>Asosiy munosabat bo'yicha shu sanadagi jadval; topilmasa <c>null</c> (<c>NoSchedule</c>).</summary>
    /// <remarks>
    /// <para>
    /// Munosabat SHART: SPEC §5.2 hal qilishni asosiy munosabat orqali
    /// ta'riflaydi, munosabati yo'q sana esa <c>NotEmployed</c> — u
    /// uchun jadval so'ralmaydi.
    /// </para>
    /// <para>
    /// ⭐ Lavozim va bo'lim pog'onasi munosabatning JORIY maydonidan emas, shu
    /// SANADAGI joyidan (<see cref="EmploymentPlacement.On"/>) o'qiladi: ko'chirish
    /// kuchga kirishidan oldingi kun eski lavozimning jadvali bilan, oldinga
    /// sanalangan ko'chirish esa faqat o'z sanasidan qo'llanadi. Kun yozuvining
    /// bo'lim/lavozim surati ham aynan shu qiymatdan — ikkalasi bir-biridan
    /// ajralib qolmaydi.
    /// </para>
    /// </remarks>
    /// <param name="primaryEmployment">Shu sanadagi asosiy munosabat (<see cref="PrimaryEmployment.Select"/>) — hodisalari bilan.</param>
    /// <param name="date">Smena boshlanadigan Toshkent sanasi.</param>
    public ScheduleResolution? Resolve(Employment primaryEmployment, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(primaryEmployment);

        var placement = EmploymentPlacement.On(primaryEmployment, date);

        var winner = Find(ScheduleScope.Employee, primaryEmployment.EmployeeId, date)
            ?? (placement.PositionId is { } positionId
                ? Find(ScheduleScope.Position, positionId, date)
                : null)
            ?? FindInDepartments(placement.DepartmentId, date)
            ?? Find(ScheduleScope.Default, null, date);

        if (winner is null)
        {
            return null;
        }

        var schedule = _schedules[winner.WorkScheduleId];
        var exact = _dates.GetValueOrDefault((schedule.Id, date));

        return new ScheduleResolution(
            winner,
            schedule,
            DayPlan.For(schedule, date, exact),
            exact);
    }

    /// <summary>Bo'lim pog'onasi: o'zidan boshlab yuqoriga, birinchi mos kelgan bo'lim yutadi.</summary>
    /// <remarks>
    /// «Eng yaqin ota-bo'lim birinchi» — pog'ona ichidagi «eng kech
    /// sana» qoidasidan OLDIN qo'llanadi: bo'limning o'z biriktirmasi
    /// departamentning keyinroq yozilgan biriktirmasidan kuchli. Aks
    /// holda departamentga yangi jadval qo'yish uning ichidagi har bir
    /// bo'limning maxsus jadvalini jimgina bosib ketardi.
    /// </remarks>
    private ScheduleAssignment? FindInDepartments(Guid? departmentId, DateOnly date)
    {
        if (departmentId is not { } start)
        {
            return null;
        }

        foreach (var id in _departments.SelfAndAncestors(start))
        {
            if (Find(ScheduleScope.Department, id, date) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private ScheduleAssignment? Find(ScheduleScope scope, Guid? targetId, DateOnly date)
        => _assignments.TryGetValue((scope, targetId), out var candidates)
            ? Array.Find(candidates, assignment => assignment.Covers(date))
            : null;
}
