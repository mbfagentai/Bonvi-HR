using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Abstractions;

/// <summary>Xodim ilovasining hisoblari uchun saqlash porti.</summary>
public interface IEmployeeAccountRepository : IRepository<EmployeeAccount>
{
    /// <summary>Kirishdagi login (telefon raqami) bo'yicha hisobni topadi — kirish yo'lidagi YAGONA qidiruv.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ Login hisob qatorida YO'Q: raqam o'chirilmagan xodimlarning ASOSIY
    /// telefonidan (<c>employees.phone</c>) qidiriladi va topilgan xodimning
    /// hisobi qaytadi. Profil tahrirlangan lahzadan yangi raqam ishlaydi,
    /// eskisi esa ishlamaydi.
    /// </para>
    /// <para>
    /// ⚠️ Raqam IKKI yoki undan ko'p o'chirilmagan xodimda bo'lsa —
    /// <c>null</c>: kimga kirish berilayotgani noaniq, taxmin qilish esa
    /// begona odamni boshqaning hisobiga kiritishi mumkin edi.
    /// </para>
    /// <para>
    /// Topilgan hisob darhol o'zgaradi (muvaffaqiyatli kirish yoziladi),
    /// shu sababli <c>AsNoTracking</c> ATAYLAB yo'q.
    /// </para>
    /// </remarks>
    /// <param name="login">Telefon raqami.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<EmployeeAccount?> FindByLoginAsync(PhoneNumber login, CancellationToken cancellationToken = default);

    /// <summary>Shu xodimning hisobi — HR kartasi va «bitta hisob» qoidasi uchun.</summary>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<EmployeeAccount?> FindByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Asosiy telefoni shu raqam bo'lgan O'CHIRILMAGAN xodimlar (kuzatuvsiz).</summary>
    /// <remarks>
    /// HR «Ilova» bo'limida «bu raqam boshqa xodimda ham bor» degan
    /// ogohlantirish uchun. Odatda bitta yoki ikkita; <paramref name="take"/>
    /// ekranni cheksiz ro'yxatdan saqlaydi.
    /// </remarks>
    /// <param name="phone">Telefon raqami.</param>
    /// <param name="take">Eng ko'p nechta.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<Employee>> PhoneHoldersAsync(
        PhoneNumber phone,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Hisobi hali yo'q o'chirilmagan xodimlar — birinchi <paramref name="take"/> tasi.</summary>
    /// <remarks>
    /// Fon vazifasi uchun bo'lak: hisob ochilgan xodim keyingi so'rovda
    /// o'zi tushib qoladi, ya'ni «qayerda to'xtadim» degan holat kerak emas.
    /// </remarks>
    /// <param name="take">Bo'lak hajmi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<Guid>> EmployeesWithoutAccountAsync(
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hisob bog'langan xodim — faqat u O'CHIRILMAGAN bo'lsa (kuzatuvsiz).
    /// </summary>
    /// <remarks>
    /// Faqat bitta ustun: bu savol <c>/api/me/*</c> ning HAR bir so'rovida
    /// beriladi (<c>EmployeeAccessGate</c>) va keyingi ishlovchining o'z
    /// o'qishiga (xususan belgilashdagi qulfga) xalaqit bermasligi kerak.
    /// </remarks>
    /// <param name="accountId">Hisob.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Xodim; hisob yo'q yoki xodim o'chirilgan bo'lsa <c>null</c>.</returns>
    Task<Guid?> LiveEmployeeIdAsync(Guid accountId, CancellationToken cancellationToken = default);

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
    /// Uch holatda chaqiriladi: chiqishda, HR yangi parol berganda va TOKEN
    /// QAYTA ISHLATILGANDA — oxirgisi o'g'irlik belgisi.
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
