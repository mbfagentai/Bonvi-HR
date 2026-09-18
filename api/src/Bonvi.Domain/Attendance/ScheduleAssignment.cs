using Bonvi.Domain.Common;

namespace Bonvi.Domain.Attendance;

/// <summary>
/// Jadvalning kimgadir biriktirilishi — muddat bilan.
/// </summary>
/// <remarks>
/// <para>
/// To'rt pog'ona: xodim → lavozim → bo'lim → sukut
/// (<see cref="ScheduleScope"/>). Birinchi mos kelgani yutadi, pog'ona
/// ichida esa eng kech <see cref="EffectiveFrom"/>.
/// </para>
/// <para>
/// ⭐ <b>Nega uchta tashqi kalit, bitta «scope_id» emas.</b> Bitta
/// universal identifikator ustuniga tashqi kalit QO'YIB BO'LMAYDI. Ya'ni
/// o'chirilgan lavozim biriktirmani hech narsaga ishora qiladigan
/// qilib qoldirardi va u jimgina mos kelmay qo'yardi — xodim esa
/// «jadvali yo'q» bo'lib chiqardi, sababi hech qayerda ko'rinmasdi.
/// Uchta bo'sh bo'lishi mumkin bo'lgan kalit + tekshiruv cheklovi
/// haqiqiy yaxlitlikni beradi, narxi — ikkita ortiqcha ustun.
/// </para>
/// <para>
/// <b>«Falon bo'limdagi falon lavozim» pog'onasi ATAYLAB yo'q.</b> Egasi
/// to'rtta pog'onani ta'rifladi; beshinchisi ustuvorlik jadvalini ikki
/// barobar kattalashtiradi va so'ralgan holati yo'q. Kerak bo'lsa —
/// <c>scope = 5</c>, ikkala kalit ham to'ldirilgan va hal qiluvchida
/// bitta shart.
/// </para>
/// </remarks>
public sealed class ScheduleAssignment : AggregateRoot, IAuditable, ISoftDeletable
{
    private ScheduleAssignment(
        Guid id,
        Guid workScheduleId,
        ScheduleScope scope,
        DateOnly effectiveFrom)
        : base(id)
    {
        WorkScheduleId = workScheduleId;
        Scope = scope;
        EffectiveFrom = effectiveFrom;
    }

    /// <summary>EF Core uchun.</summary>
    private ScheduleAssignment() { }

    public Guid WorkScheduleId { get; private set; }

    /// <summary>Biriktirma qaysi pog'onada.</summary>
    public ScheduleScope Scope { get; private set; }

    public Guid? EmployeeId { get; private set; }

    public Guid? PositionId { get; private set; }

    public Guid? DepartmentId { get; private set; }

    /// <summary>Qaysi sanadan kuchga kiradi.</summary>
    public DateOnly EffectiveFrom { get; private set; }

    /// <summary>Qaysi sanagacha (SHU kun ham kiradi). <c>null</c> ⇒ muddatsiz.</summary>
    public DateOnly? EffectiveTo { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Pog'onaga mos keluvchi obyekt identifikatori. Sukut pog'onada <c>null</c>.</summary>
    public Guid? TargetId => Scope switch
    {
        ScheduleScope.Employee => EmployeeId,
        ScheduleScope.Position => PositionId,
        ScheduleScope.Department => DepartmentId,
        _ => null,
    };

    /// <summary>Biriktirma shu sanada kuchdami.</summary>
    public bool Covers(DateOnly date)
        => EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo >= date);

    /// <summary>Jadvalni biriktiradi.</summary>
    /// <param name="workScheduleId">Qaysi jadval.</param>
    /// <param name="scope">Pog'ona.</param>
    /// <param name="targetId">
    /// Pog'onaga mos keluvchi obyekt: xodim, lavozim yoki bo'lim.
    /// Sukut pog'onada <c>null</c> bo'lishi SHART.
    /// </param>
    /// <param name="effectiveFrom">Kuchga kirish sanasi.</param>
    /// <param name="effectiveTo">Tugash sanasi (shu kun ham kiradi) yoki <c>null</c>.</param>
    public static Result<ScheduleAssignment> Assign(
        Guid workScheduleId,
        ScheduleScope scope,
        Guid? targetId,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo = null)
    {
        if (workScheduleId == Guid.Empty)
        {
            return Error.Validation("davomat.jadval_yoq", "Jadval ko'rsatilishi shart.");
        }

        var hasTarget = targetId is { } id && id != Guid.Empty;

        if (scope == ScheduleScope.Default ? hasTarget : !hasTarget)
        {
            return Error.Validation(
                "davomat.biriktirma_obyekt_yoq",
                "Biriktirma pog'onasi va ko'rsatilgan obyekt bir-biriga mos kelmaydi.");
        }

        if (effectiveTo is { } end && end < effectiveFrom)
        {
            return Error.Validation(
                "davomat.davr_notogri",
                "Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.");
        }

        return new ScheduleAssignment(NewId(), workScheduleId, scope, effectiveFrom)
        {
            EmployeeId = scope == ScheduleScope.Employee ? targetId : null,
            PositionId = scope == ScheduleScope.Position ? targetId : null,
            DepartmentId = scope == ScheduleScope.Department ? targetId : null,
            EffectiveTo = effectiveTo,
        };
    }

    /// <summary>
    /// Muddatini o'zgartiradi.
    /// </summary>
    /// <remarks>
    /// Pog'ona va obyekt O'ZGARMAYDI: boshqa bo'limga biriktirish —
    /// tuzatish emas, boshqa qaror. To'g'ri yo'l: bu yozuvni yopish va
    /// yangisini ochish, shunda tarix ikkala qarorni ham saqlaydi.
    /// </remarks>
    public Result Reschedule(DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        if (effectiveTo is { } end && end < effectiveFrom)
        {
            return Error.Validation(
                "davomat.davr_notogri",
                "Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.");
        }

        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;

        return Result.Success();
    }
}
