using Bonvi.Domain.Employees;

namespace Bonvi.Application.Attendance.Scheduling;

/// <summary>
/// Kun qaysi mehnat munosabatiga tegishli — SPEC §5.1.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Kun XODIMGA tegishli, lekin jadval va tashkiliy surat BITTA
/// munosabatdan olinadi.</b> Bu holdingda bitta odam bir vaqtda ikki
/// firmada ishlaydi («VIKOR + Вело унион 0,5 ставка ўриндош»), turniket
/// esa bitta borliqni yozadi. Qaysi munosabat tanlanishi tasodifga
/// qolsa, bir xil kun ikki hisoblashda ikki xil bo'lim va ikki xil
/// jadval bilan chiqardi.
/// </para>
/// <para>
/// Tartib: <c>is_primary</c> belgilangani → eng erta ishga kirgani →
/// kichikroq identifikator. Oxirgi qadam hech qachon tasodifiy emas:
/// <see cref="Guid.CompareTo(Guid)"/> satr ko'rinishi tartibida
/// solishtiradi, PostgreSQL <c>uuid</c> tartibi ham aynan shu.
/// </para>
/// </remarks>
public static class PrimaryEmployment
{
    /// <summary>Munosabat shu sanani qamraydimi — ishdan ketgan kun HAM qamraladi.</summary>
    /// <remarks>
    /// <c>[hire_date, termination_date]</c> — ikkala chekka ham ichida:
    /// buyruqdagi oxirgi ish kuni hali ish kuni, «ishlamaydi» degan holat
    /// undan KEYINGI sanadan boshlanadi (SPEC §5.7).
    /// </remarks>
    /// <param name="employment">Tekshiriladigan munosabat.</param>
    /// <param name="date">Toshkent sanasi.</param>
    public static bool Covers(Employment employment, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(employment);

        return employment.HireDate <= date
            && (employment.TerminationDate is not { } terminated || terminated >= date);
    }

    /// <summary>Sanani qamraydigan asosiy munosabat; hech biri qamramasa <c>null</c>.</summary>
    /// <remarks>
    /// O'chirilgan (soft delete) munosabat hisobga olinmaydi. EF filtri
    /// ularni baribir tashlab ketadi; bu yerdagi shart filtrni chetlab
    /// o'qigan chaqiruvchi o'chirilgan yozuvni «asosiy» qilib qo'ymasligi
    /// uchun.
    /// </remarks>
    /// <param name="employments">Bitta xodimning munosabatlari.</param>
    /// <param name="date">Toshkent sanasi.</param>
    public static Employment? Select(IEnumerable<Employment> employments, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(employments);

        return employments
            .Where(employment => employment.DeletedAt is null && Covers(employment, date))
            .OrderByDescending(employment => employment.IsPrimary)
            .ThenBy(employment => employment.HireDate)
            .ThenBy(employment => employment.Id)
            .FirstOrDefault();
    }
}
