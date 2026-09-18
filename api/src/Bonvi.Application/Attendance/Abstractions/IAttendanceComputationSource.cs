using Bonvi.Application.Attendance.Computation;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Attendance.Abstractions;

/// <summary>
/// Kunlarni QAYTA HISOBLASH uchun ma'lumot yuklovchisi — B4 ning yuklash shartnomasi, kodda.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega alohida port, repozitoriylar emas.</b> Hisoblash
/// (<see cref="AttendanceDayBuilder"/>) to'liq to'plamni kutadi va shartnoma
/// buzilsa natija jimgina noto'g'ri emas, istisno bo'ladi. Shartlar bitta joyda
/// yig'ilgan: moslik yaratish, qo'lda qayta hisoblash, «nega unda shu jadval»
/// savoli va keyinroq fon vazifalari (B7) — hammasi SHU yuklovchidan o'qiydi.
/// </para>
/// <para>
/// ⚠️ <b>O'qish modeli bilan bir xil qoidalar</b> (<c>AttendanceReadModel</c>):
/// jadvallar va bo'limlar O'CHIRILGANLARI BILAN. Ular farq qilsa, saqlangan
/// katak va o'qish paytida hosil qilingan katak bir kun ikki xil holat
/// ko'rsatardi — tirik biriktirma ko'rsatgan o'chirilgan jadval bittasida
/// hisobga olinib, ikkinchisida istisno otardi.
/// </para>
/// </remarks>
public interface IAttendanceComputationSource
{
    /// <summary>
    /// Hal qiluvchi uchun butun katalog: tirik biriktirmalarning HAMMASI, jadvallar
    /// KUNLARI BILAN (o'chirilganlari ham), bo'limlarning HAMMASI (o'chirilganlari ham).
    /// </summary>
    /// <remarks>
    /// Hammasi, sana bo'yicha kesilmagan: biriktirmalar yuzlab, jadvallar
    /// o'nlab, bo'limlar ~73 — bir to'plam uchun bir marta o'qiladi va xotirada
    /// qoladi. Kesish esa qo'shni sanalarning rejasi bilan bog'liq nozik
    /// shartni (B4: [D − 2, D + 2]) har bir chaqiruvchiga yuklardi.
    /// </remarks>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<ScheduleCatalog> LoadScheduleCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>Xodimlarning BARCHA mehnat munosabatlari (tugaganlari ham), xodim bo'yicha.</summary>
    /// <param name="employeeIds">Xodimlar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Employment>>> LoadEmploymentsAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>Xodimlarning BOG'LANGAN o'tishlari — <c>local_date</c> ∈ [from, to], xodim bo'yicha.</summary>
    /// <remarks>
    /// Oraliqni chaqiruvchi <see cref="AttendanceDayBuilder.PassDatesNeeded"/>
    /// dan oladi (tungi smenaning chiqishi ertangi sanada). Bazadan to'g'ridan-
    /// to'g'ri <see cref="PassReading"/> ga proyeksiya — <c>raw_event</c> o'qilmaydi.
    /// </remarks>
    /// <param name="employeeIds">Xodimlar.</param>
    /// <param name="from">Birinchi <c>local_date</c> (o'zi ham kiradi).</param>
    /// <param name="to">Oxirgi <c>local_date</c> (o'zi ham kiradi).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<PassReading>>> LoadPassesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>[from, to] ni qayta hisoblashda qaysi xodimlarga tegish kerak.</summary>
    /// <remarks>
    /// Uch manbaning birlashmasi: davrda munosabati bo'lganlar (§5.4 (b)
    /// qatorlari), davr uchun kerakli sanalarda bog'langan o'tishi borlar
    /// (§5.4 (a)) va davrda kun yozuvi BORlar. Uchinchisi bo'lmasa, endi
    /// bo'lmasligi kerak bo'lgan eski qator (masalan munosabati o'chirilgan
    /// odamniki) hech qachon tozalanmasdi.
    /// </remarks>
    /// <param name="from">Davr boshi.</param>
    /// <param name="to">Davr oxiri.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<Guid>> ListEmployeesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

/// <summary>Jadval hal qiluvchisi quriladigan katalog.</summary>
/// <param name="Assignments">Tirik biriktirmalar.</param>
/// <param name="Schedules">Jadvallar — kunlari bilan, o'chirilganlari ham.</param>
/// <param name="Departments">Bo'limlar — o'chirilganlari ham (daraxt oraliq bo'limda uzilmasin).</param>
public sealed record ScheduleCatalog(
    IReadOnlyList<ScheduleAssignment> Assignments,
    IReadOnlyList<WorkSchedule> Schedules,
    IReadOnlyList<Department> Departments);
