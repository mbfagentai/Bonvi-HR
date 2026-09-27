using Bonvi.Domain.Attendance;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Abstractions;

/// <summary>Xodim ilovasining hisoblari uchun saqlash porti.</summary>
public interface IEmployeeAccountRepository : IRepository<EmployeeAccount>
{
    /// <summary>Login bo'yicha topadi — kirish yo'lidagi YAGONA qidiruv.</summary>
    /// <remarks>
    /// <c>ux_employee_accounts_login</c> bo'yicha aniq tenglik. Topilgan hisob
    /// darhol o'zgaradi (muvaffaqiyatli yoki muvaffaqiyatsiz urinish
    /// yoziladi), shu sababli <c>AsNoTracking</c> ATAYLAB yo'q.
    /// </remarks>
    /// <param name="login">Telefon raqami.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<EmployeeAccount?> FindByLoginAsync(PhoneNumber login, CancellationToken cancellationToken = default);

    /// <summary>Shu xodimning hisobi — HR kartasi va «bitta hisob» qoidasi uchun.</summary>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<EmployeeAccount?> FindByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Bunday login band emasligini tekshiradi.</summary>
    /// <remarks>
    /// ⚠️ Bazadagi unikal indeksni ALMASHTIRMAYDI: ikki HR bir vaqtda bir xil
    /// raqamni kiritsa, tekshiruv ikkalasida ham o'tadi va to'qnashuvni indeks
    /// tutadi. Maqsad — odatiy holatda 500 emas, tushunarli 409 berish.
    /// </remarks>
    /// <param name="login">Telefon raqami.</param>
    /// <param name="exceptAccountId">Tahrirlanayotgan hisob — o'zi takror sanalmaydi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<bool> LoginExistsAsync(
        PhoneNumber login,
        Guid? exceptAccountId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hisob qatorini SHU TRANZAKSIYA uchun qulflaydi (<c>SELECT … FOR UPDATE</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Faqat bitta joy uchun: telefondan belgilash. Takrorni to'suvchi
    /// unikal indeks bu yerda YORDAM BERMAYDI — kalitdagi vaqt soniyagacha
    /// yaxlitlanadi, ya'ni 1,2 soniya farq bilan kelgan ikki bosish ikki XIL
    /// kalit beradi va ikkalasi ham indeksdan o'tib ketadi. Ularni faqat
    /// 60 soniyalik takror qoidasi to'sadi, u esa «o'qib, keyin yozadi»:
    /// ikki parallel so'rov «yaqinda belgi yo'q» degan javobni BIRGA o'qishi
    /// mumkin.
    /// </para>
    /// <para>
    /// Qulf aynan hisob qatoriga qo'yiladi, chunki u bitta xodimning
    /// belgilarini FAQAT bir-biriga nisbatan navbatga qo'yadi: qator bitta,
    /// allaqachon qo'lda va boshqa hech qaysi kod yo'li unga tegmaydi.
    /// </para>
    /// <para>
    /// Rad etilgan muqobil — <c>date_trunc('minute', created_at)</c> ustidagi
    /// qisman unikal indeks: uning daqiqa chegarasida teshigi bor
    /// (:59,9 va :00,0) va biznes qoidasini hech kim qaramaydigan joyga —
    /// indeks ifodasiga — yashirardi.
    /// </para>
    /// </remarks>
    /// <param name="accountId">Hisob.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Qulflangan hisob; topilmasa <c>null</c>.</returns>
    Task<EmployeeAccount?> LockAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hisobda «bir martalik parolni almashtirish shart» bayrog'i turibdimi.
    /// </summary>
    /// <remarks>
    /// Faqat bitta ustun, kuzatuvsiz (<c>AsNoTracking</c>): bu savol
    /// <c>/api/me/*</c> ning HAR bir so'rovida beriladi va keyingi
    /// ishlovchining o'z o'qishiga (xususan belgilashdagi qulfga) xalaqit
    /// bermasligi kerak.
    /// </remarks>
    /// <param name="accountId">Hisob.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Bayroq; hisob topilmasa <c>null</c>.</returns>
    Task<bool?> MustChangePasswordAsync(Guid accountId, CancellationToken cancellationToken = default);
}

/// <summary>Xodim ilovasining yangilash tokenlari uchun saqlash porti.</summary>
/// <remarks>
/// <c>IRefreshTokenRepository</c> ning aynan nusxasi, faqat boshqa jadval
/// ustida — sababi <see cref="EmployeeRefreshToken"/> izohida.
/// </remarks>
public interface IEmployeeRefreshTokenRepository : IRepository<EmployeeRefreshToken>
{
    /// <summary>Xesh bo'yicha topadi (ochiq token hech qachon saqlanmaydi).</summary>
    /// <param name="tokenHash">SHA-256 xeshi (hex).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<EmployeeRefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Hisobning barcha tirik tokenlarini bekor qiladi.</summary>
    /// <remarks>
    /// Uch holatda chaqiriladi: chiqishda, parol almashganda va TOKEN QAYTA
    /// ISHLATILGANDA — oxirgisi o'g'irlik belgisi.
    /// </remarks>
    /// <param name="employeeAccountId">Hisob.</param>
    /// <param name="at">Lahza.</param>
    /// <param name="byIp">So'rov manzili.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<int> RevokeAllForAccountAsync(
        Guid employeeAccountId,
        DateTimeOffset at,
        string? byIp,
        CancellationToken cancellationToken = default);

    /// <summary>Muddati tugagan tokenlarni tozalaydi (fon vazifasi uchun).</summary>
    /// <param name="before">Shu lahzadan oldin tugaganlari.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<int> DeleteExpiredAsync(DateTimeOffset before, CancellationToken cancellationToken = default);
}

/// <summary>Telefondan kelgan belgilarning koordinatalari uchun saqlash porti.</summary>
/// <remarks>
/// <see cref="MobileCheckIn"/> agregat ildizi EMAS (unga faqat o'z o'tishi
/// orqali murojaat qilinadi), shu sababli bu port <c>IRepository&lt;&gt;</c>
/// dan meros olmaydi va faqat yozishni biladi. O'qish — jurnal uchun —
/// o'qish modelida.
/// </remarks>
public interface IMobileCheckInRepository
{
    /// <summary>Yangi qatorni qo'shadi.</summary>
    /// <param name="checkIn">Qabul qilingan belgining koordinatasi.</param>
    void Add(MobileCheckIn checkIn);
}
