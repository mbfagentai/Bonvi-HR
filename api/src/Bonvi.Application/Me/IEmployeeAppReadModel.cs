namespace Bonvi.Application.Me;

/// <summary>Xodim profilining ekranga chiqadigan qismi.</summary>
/// <param name="FullName">To'liq ism.</param>
/// <param name="ShortName">Qisqa ko'rinish.</param>
/// <param name="PhotoKey">Rasm kaliti.</param>
/// <param name="Department">Bugungi bo'lim nomi.</param>
/// <param name="Position">Bugungi lavozim nomi.</param>
/// <param name="MobileCheckInAllowed">Telefondan belgilash yoqilganmi.</param>
public sealed record MyProfileRow(
    string FullName,
    string ShortName,
    string? PhotoKey,
    string? Department,
    string? Position,
    bool MobileCheckInAllowed);

/// <summary>
/// Xodim ilovasi uchun o'qish modeli.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega <c>IAttendanceReadModel</c> ga qo'shilmadi.</b> U ~3 300
/// qatorlik HR o'qish modeli va uning har bir metodi <c>attendance.view</c>
/// ruxsati ostidagi ekranga xizmat qiladi. Bu yerdagi ikkita metod esa
/// BOSHQA xavfsizlik chegarasida yashaydi: ularning ikkalasi ham xodim
/// identifikatorini MAJBURIY argument sifatida oladi va «hamma xodimlar»
/// degan variantni umuman taklif qilmaydi. Alohida interfeysda bu qoida
/// imzoning o'zida ko'rinib turadi.
/// </para>
/// <para>
/// Oyning kataklari bu yerda YO'Q: ular HR tabeli bilan AYNAN bir xil
/// bo'lishi kerak, ya'ni <c>IAttendanceReadModel.GetTimesheetAsync</c>
/// dan, bitta xodimga cheklangan filtr bilan olinadi. Ikkinchi nusxa
/// yozilsa, ikki ekran bir kunni ikki xil ko'rsatadigan kun albatta
/// kelardi.
/// </para>
/// </remarks>
public interface IEmployeeAppReadModel
{
    /// <summary>Xodimning profili — <c>GET /api/me</c> uchun.</summary>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="today">Bo'lim va lavozim QAYSI kunga ko'ra olinadi (Toshkent).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<MyProfileRow?> ProfileAsync(
        Guid employeeId,
        DateOnly today,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Davrdagi belgilar — kun bo'yicha guruhlangan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bitta so'rov butun oyga (<c>ix_attendance_passes_employee_time</c>),
    /// ustiga telefon belgilarining obyekt nomlari uchun bitta
    /// <c>WHERE pass_id = ANY(…)</c>. Kun bo'yicha aylanib so'rash 31 ta
    /// so'rov bo'lardi.
    /// </para>
    /// <para>
    /// Guruhlash <c>local_date</c> bo'yicha — o'tishning O'ZIDAGI Toshkent
    /// sanasi. Bu tabel katagining sanasi bilan bir xil kalit, ya'ni
    /// belgilar o'z katagiga tushadi va yarim tundan keyingi belgi ekranda
    /// «ertangi kunga» ko'chib ketmaydi.
    /// </para>
    /// </remarks>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="from">Davr boshi (kiradi).</param>
    /// <param name="to">Davr oxiri (kiradi).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<MyMarkDto>>> MarksAsync(
        Guid employeeId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
