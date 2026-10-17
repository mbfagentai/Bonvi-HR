using Bonvi.Application.Attendance.Computation;
using Bonvi.Application.Attendance.Scheduling;
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
    /// <param name="from">Yillik to'r kataklari uchun oraliq boshi.</param>
    /// <param name="to">Oraliq oxiri.</param>
    Task<ScheduleCatalog> LoadScheduleCatalogAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Xodimlarning BARCHA mehnat munosabatlari (tugaganlari ham), xodim bo'yicha.</summary>
    /// <remarks>
    /// Mehnat yo'li hodisalari BILAN: sanadagi bo'lim va lavozim ko'chirish
    /// hodisalaridan o'qiladi (<c>EmploymentPlacement</c>).
    /// </remarks>
    /// <param name="employeeIds">Xodimlar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Employment>>> LoadEmploymentsAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// [from, to] da munosabati bo'lgan HAR BIR xodimning BARCHA munosabatlari (hodisalari bilan), xodim bo'yicha.
    /// </summary>
    /// <remarks>
    /// Yillik to'r tahririda «bu jadval kimga tegadi» savoli uchun
    /// (<c>AttendanceScheduleDatesJob</c>): jadval munosabati bor odamgagina
    /// tegishli bo'ladi, munosabatsiz kun esa jadvaldan qat'i nazar <c>NotEmployed</c>.
    /// Asosiy munosabat sanaga qarab tanlanadi, shu sababli xodimning qolgan
    /// munosabatlari ham kerak — <see cref="LoadEmploymentsAsync"/> dagi bilan bir xil shakl.
    /// </remarks>
    /// <param name="from">Davr boshi.</param>
    /// <param name="to">Davr oxiri.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Employment>>> LoadEmploymentsCoveringAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <paramref name="since"/> dan beri o'zgargan mehnat munosabatlari va mehnat yo'li hodisalari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Iflos navbat faqat YANGI o'tishni ko'radi; orqaga sana bilan kiritilgan ishdan
    /// bo'shatish, ishga qabul yoki ko'chirish esa o'tishlarga tegmaydi. Bu ro'yxat
    /// aynan o'shalarni topadi (<c>AttendanceEmploymentChangesJob</c>).
    /// </para>
    /// <para>
    /// O'chirilgan (soft delete) munosabatlar ham kiradi — o'chirish ham o'zgarish.
    /// </para>
    /// </remarks>
    /// <param name="since">Shu lahzadan (o'zi ham) keyin yozilgan o'zgarishlar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<EmploymentChange>> ListEmploymentChangesAsync(
        DateTimeOffset since,
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

    /// <summary>
    /// Tasdiqlangan yo'qlik so'rovlari qamragan sanalar — xodim bo'yicha
    /// (SPEC-so'rovlar §B3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Natija sanalar TO'PLAMI bo'lib qaytadi, so'rovlar ro'yxati emas:
    /// hisoblovchiga faqat «shu kun oqlanadimi» degan javob kerak va u
    /// so'rovning shaklini (bir kunmi, oraliqmi, vaqti bormi) bilmasligi
    /// kerak.
    /// </para>
    /// <para>
    /// Vaqt oralig'i bu yerda e'tiborga OLINMAYDI — har qanday tasdiqlangan
    /// so'rov sanasi «kelmadi» ni oqlaydi. Soatlar faqat «ish vaqti» turida
    /// (<see cref="LoadWorkCreditsAsync"/>).
    /// </para>
    /// </remarks>
    /// <param name="employeeIds">Xodimlar.</param>
    /// <param name="from">Davr boshi (o'zi ham kiradi).</param>
    /// <param name="to">Davr oxiri (o'zi ham kiradi).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyDictionary<Guid, IReadOnlySet<DateOnly>>> LoadExcusedDatesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// «Ish vaqti hisoblanadigan» turdagi tasdiqlangan so'rovlar (SPEC-so'rovlar §B3 AMENDED).
    /// </summary>
    /// <param name="employeeIds">Xodimlar.</param>
    /// <param name="from">Davr boshi (o'zi ham kiradi).</param>
    /// <param name="to">Davr oxiri (o'zi ham kiradi).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<WorkCredit>>> LoadWorkCreditsAsync(
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

/// <summary>Bitta o'zgargan munosabat yoki hodisa — qaysi xodim va qaysi sanadan ta'sir qiladi.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="HireDate">O'zgargan munosabatning ishga kirgan sanasi.</param>
/// <param name="EffectiveDate">
/// YANGI yozilgan hodisaning kuchga kirish sanasi. <c>null</c> — munosabat qatorining o'zi
/// o'zgargan yoki mavjud hodisa tahrirlangan: bunda o'zgarish qaysi sanadan boshlab ta'sir
/// qilishi noma'lum (eski sana saqlanmaydi), ya'ni <paramref name="HireDate"/> dan boshlab.
/// </param>
public sealed record EmploymentChange(Guid EmployeeId, DateOnly HireDate, DateOnly? EffectiveDate)
{
    /// <summary>Shu o'zgarish ta'sir qilishi mumkin bo'lgan eng erta sana.</summary>
    public DateOnly AffectsFrom => EffectiveDate ?? HireDate;
}

/// <summary>Jadval hal qiluvchisi quriladigan katalog.</summary>
/// <param name="Assignments">Tirik biriktirmalar.</param>
/// <param name="Schedules">Jadvallar — kunlari bilan, o'chirilganlari ham.</param>
/// <param name="Departments">Bo'limlar — o'chirilganlari ham (daraxt oraliq bo'limda uzilmasin).</param>
/// <param name="Dates">
/// Yillik to'rning kataklari — FAQAT so'ralgan oraliq uchun
/// (SPEC-jadval-to'ri §B2). Biriktirma va jadvaldan farqli o'laroq bu
/// ro'yxat yillar bo'yicha o'sadi, shu sababli u sana bo'yicha kesiladi.
/// </param>
/// <param name="Overrides">
/// Tasdiqlangan jadval so'rovlarining kunlari — xodimning o'sha sanadagi rejasi
/// o'rniga turadi (SPEC-verifikatsiya-jadval-sorovi §C2). Sana bo'yicha kesiladi.
/// </param>
public sealed record ScheduleCatalog(
    IReadOnlyList<ScheduleAssignment> Assignments,
    IReadOnlyList<WorkSchedule> Schedules,
    IReadOnlyList<Department> Departments,
    IReadOnlyList<WorkScheduleDate> Dates,
    IReadOnlyList<EmployeeDayOverride>? Overrides = null);
