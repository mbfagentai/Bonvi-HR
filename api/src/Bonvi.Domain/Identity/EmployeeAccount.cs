using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Identity;

/// <summary>
/// Xodimning ILOVAGA kirish hisobi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega <see cref="User"/> emas, alohida jadval.</b> <c>users</c> —
/// HR tizimining hisoblari: ularda rol, ruxsatlar to'plami va xodim
/// kartasiga ixtiyoriy bog'lanish bor. Bu yerdagi ~950 hisobda esa rol ham,
/// ruxsat ham YO'Q va bo'lishi ham kerak emas — ular faqat o'z
/// ma'lumotini ko'radi. Ikkisini bitta jadvalga qo'shish har bir ruxsat
/// so'rovini «bu odam xodimmi yoki foydalanuvchimi?» degan shart bilan
/// og'irlashtirardi va bitta xatolik butun HR tizimini 950 kishiga ochib
/// yuborardi.
/// </para>
/// <para>
/// <b>Login — telefon raqami, <see cref="User.Login"/> bilan AYNAN bir xil
/// qoidada</b> (<see cref="PhoneNumber"/>). Ikki jadvalda bir xil raqam
/// bo'lishi mumkin va bu to'g'ri: bir vaqtda HR menejer ham, xodim ham
/// bo'lgan odamning ikkita paroli bo'ladi, qaysi jadval o'qilishini esa
/// endpoint hal qiladi.
/// </para>
/// <para>
/// ⚠️ <b>Xodimning o'chirilgani yoki ishdan ketgani BU YERDA
/// ko'rinmaydi.</b> Hisob login bo'yicha topiladi, xodim orqali emas, ya'ni
/// EF ning yumshoq o'chirish filtri bu yo'lga umuman tushmaydi. Shu sababli
/// kirish ishlovchisi xodimni ALOHIDA yuklaydi va bugungi mehnat
/// munosabatini tekshiradi (INV-1, INV-2). <see cref="IsActive"/> esa
/// BOSHQA savol: «ishlaydi, lekin vaqtincha to'xtatilgan».
/// </para>
/// <para>
/// <b>Yumshoq o'chirish YO'Q.</b> <see cref="IsActive"/> — o'chirgich,
/// <c>IAuditable</c> kimligini yozadi, tarix esa <c>audit_logs</c> da.
/// <c>deleted_at</c> qo'shilsa, ikkala unikal indeksga
/// <c>WHERE deleted_at IS NULL</c> kerak bo'lardi — ya'ni «bu login band»
/// degan haqiqat o'chirilgandan keyin yo'qolardi, holbuki hisob qatorida
/// u aynan saqlanishi kerak (<c>RefreshToken</c>, <c>AttendancePass</c>
/// bilan bir xil qaror).
/// </para>
/// </remarks>
public sealed class EmployeeAccount : AggregateRoot, IAuditable
{
    /// <summary>Ketma-ket necha xatodan keyin bloklanadi.</summary>
    /// <remarks>
    /// Son <see cref="User.RecordFailedLogin"/> dagi bilan bir xil bo'lishi
    /// SHART: ikki xil hisob turi uchun ikki xil qoida qo'yilsa, «nega meni
    /// bloklab qo'ydi» degan savolga ikki xil javob bo'lardi.
    /// </remarks>
    public const int MaxFailedAttempts = 5;

    /// <summary>Blok necha daqiqa turadi.</summary>
    public const int LockoutMinutes = 15;

    private EmployeeAccount(
        Guid id,
        Guid employeeId,
        PhoneNumber login,
        string passwordHash,
        DateTimeOffset passwordSetAt)
        : base(id)
    {
        EmployeeId = employeeId;
        Login = login;
        PasswordHash = passwordHash;
        PasswordSetAt = passwordSetAt;
        MustChangePassword = true;
        IsActive = true;
    }

    /// <summary>EF Core uchun.</summary>
    private EmployeeAccount()
    {
        Login = null!;
        PasswordHash = string.Empty;
    }

    /// <summary>Kimning hisobi. Bitta xodimda bitta hisob (unikal indeks).</summary>
    public Guid EmployeeId { get; private set; }

    /// <summary>Kirish uchun telefon raqami. Unikal.</summary>
    /// <remarks>
    /// ⚠️ <c>employees.phone</c> dan NUSXA, unga havola emas. HR formasi uni
    /// xodim kartasidan oldindan to'ldiradi, keyin ikkalasi mustaqil yashaydi.
    /// Rad etilgan muqobil — loginni o'qish paytida xodimdan olish: u holda
    /// kadrlar maydonidagi xatoni tuzatish JIMGINA autentifikatsiya hodisasiga
    /// aylanardi va odam nima uchun kira olmayotganini hech kim bilmasdi.
    /// </remarks>
    public PhoneNumber Login { get; private set; }

    /// <summary>Parol xeshi. Ochiq parol hech qayerda saqlanmaydi.</summary>
    public string PasswordHash { get; private set; }

    /// <summary>Birinchi kirishda parolni almashtirish MAJBURIYmi.</summary>
    /// <remarks>
    /// HR bergan bir martalik parol — o'qib yozib olinadigan satr. U
    /// almashtirilmaguncha hisob faqat parol o'zgartirish endpointiga
    /// ruxsat beradi.
    /// </remarks>
    public bool MustChangePassword { get; private set; }

    /// <summary>
    /// Joriy xesh qachon yozilgan.
    /// </summary>
    /// <remarks>
    /// <c>updated_at</c> buning o'rnini BOSMAYDI: HR hisobni
    /// faolsizlantirgan lahzada u ham yangilanadi va «parol qachon
    /// berilgan» degan javob yo'qoladi. Bu ustun bilan esa «berilgan,
    /// lekin hech kim ishlatmagan bir martalik parollar» ro'yxatini
    /// tuzish mumkin: <see cref="MustChangePassword"/> va
    /// <see cref="LastLoginAt"/> <c>null</c>.
    /// </remarks>
    public DateTimeOffset PasswordSetAt { get; private set; }

    /// <summary>HR ning qo'l o'chirgichi.</summary>
    public bool IsActive { get; private set; }

    /// <summary><c>null</c> — hech qachon kirmagan. Parol tiklansa ham TOZALANMAYDI.</summary>
    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>Ketma-ket noto'g'ri JORIY parol — parolni almashtirishda.</summary>
    /// <remarks>
    /// ⚠️ KIRISHDAGI xatolar bu yerda SANALMAYDI: ular (login, IP) juftligi
    /// bo'yicha, hisobdan tashqarida olib boriladi (<c>EmployeeLoginThrottle</c>).
    /// Hisob qatoridagi blok faqat tasdiqlangan sessiya ichidan keladigan
    /// urinishlarga qo'yiladi — ularni begona odam yoqa olmaydi.
    /// </remarks>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>Shu lahzagacha parolni almashtirish bloklangan. <c>null</c> — blok yo'q.</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>HR yangi hisob ochadi — bir martalik parol bilan.</summary>
    /// <param name="employeeId">Kimga.</param>
    /// <param name="login">Telefon raqami.</param>
    /// <param name="passwordHash">Tayyor xesh (yasash Infrastructure ishi).</param>
    /// <param name="at">Parol yozilgan lahza.</param>
    public static Result<EmployeeAccount> Issue(
        Guid employeeId,
        PhoneNumber login,
        string passwordHash,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(login);

        if (employeeId == Guid.Empty)
        {
            return Error.Validation("xodim_hisobi.xodim_yoq", "Xodim tanlanishi shart.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Error.Validation("foydalanuvchi.parol_bosh", "Parol kiritilishi shart.");
        }

        return new EmployeeAccount(NewId(), employeeId, login, passwordHash, at);
    }

    /// <summary>
    /// HR parolni tiklaydi — yangi bir martalik parol.
    /// </summary>
    /// <remarks>
    /// <see cref="LastLoginAt"/> ATAYLAB tegilmaydi: «bu odam umuman kirganmi»
    /// degan savol parol tiklanganidan keyin ham javobini saqlashi kerak.
    /// </remarks>
    /// <param name="passwordHash">Yangi xesh.</param>
    /// <param name="at">Lahza.</param>
    public void ResetPassword(string passwordHash, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
        PasswordSetAt = at;
        MustChangePassword = true;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    /// <summary>Xodimning O'ZI parolni almashtirdi.</summary>
    /// <param name="passwordHash">Yangi xesh.</param>
    /// <param name="at">Lahza.</param>
    public void ChangePassword(string passwordHash, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
        PasswordSetAt = at;
        MustChangePassword = false;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    /// <summary>Loginni (telefon raqamini) almashtiradi.</summary>
    /// <remarks>
    /// Unikallik BU YERDA tekshirilmaydi — u boshqa qatorni so'rashni talab
    /// qiladi, ya'ni use-case qatlamining ishi (<see cref="User.ChangeLogin"/>
    /// dagi bilan bir xil chegara).
    /// </remarks>
    /// <param name="login">Yangi raqam.</param>
    public void ChangeLogin(PhoneNumber login)
    {
        ArgumentNullException.ThrowIfNull(login);
        Login = login;
    }

    /// <summary>HR hisobni vaqtincha yopadi.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>HR hisobni qaytaradi — blok va hisoblagich ham tozalanadi.</summary>
    public void Activate()
    {
        IsActive = true;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    /// <summary>Muvaffaqiyatli kirish.</summary>
    /// <param name="at">Lahza.</param>
    public void RecordSuccessfulLogin(DateTimeOffset at)
    {
        LastLoginAt = at;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    /// <summary>
    /// Muvaffaqiyatsiz urinish. <see cref="MaxFailedAttempts"/> tadan keyin blok.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Hisoblagich blok qo'yilgan lahzada NOLGA tushadi, ya'ni blok
    /// KUCHAYMAYDI — har 15 daqiqada yana 5 ta urinish. Bu <see cref="User"/>
    /// dan ko'chirilgan ataylabki qaror: cheksiz kuchayadigan blokda raqib
    /// odamni butunlay qulflab qo'yishi mumkin.
    /// </para>
    /// <para>
    /// Faqat PAROLNI ALMASHTIRISHDA chaqiriladi (noto'g'ri joriy parol):
    /// ochiq qolgan telefondagi sessiya joriy parolni terib topishning
    /// cheksiz kanali bo'lmasligi kerak. Kirishdagi xatolar esa hisobdan
    /// tashqarida, (login, IP) bo'yicha sanaladi — hisob qatoridagi blokni
    /// begona odam 950 ta ma'lum telefon raqamiga yoqib chiqa olardi.
    /// </para>
    /// </remarks>
    /// <param name="at">Lahza.</param>
    public void RecordFailedLogin(DateTimeOffset at)
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts >= MaxFailedAttempts)
        {
            LockedUntil = at.AddMinutes(LockoutMinutes);
            FailedLoginAttempts = 0;
        }
    }

    /// <summary>Shu lahzada bloklanganmi.</summary>
    /// <param name="moment">Tekshirish lahzasi.</param>
    public bool IsLockedAt(DateTimeOffset moment) => LockedUntil is not null && LockedUntil > moment;
}
