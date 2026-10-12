using Bonvi.Domain.Common;

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
/// ⭐ <b>Login bu yerda SAQLANMAYDI</b> (egasining qarori, 2026-09-27).
/// Login — xodim profilidagi ASOSIY telefon (<c>employees.phone</c>), va
/// hisob kirishda aynan shu ustun orqali topiladi. Sabab: haqiqat bitta
/// joyda turishi kerak — HR profilda raqamni tuzatsa, ertasi kuni emas,
/// SHU ZAHOTI yangi raqam bilan kiriladi, eskisi esa ishlamaydi. Ilgari
/// login shu jadvalda NUSXA edi va ikki raqam jimgina ajralib ketardi.
/// </para>
/// <para>
/// ⚠️ Raqam bo'sh yoki boshqa o'chirilmagan xodimda ham bo'lsa, kirish
/// RAD etiladi (umumiy javob bilan), hisobning o'zi esa yashayveradi: HR
/// profilni tuzatgan lahzada u ishlab ketadi. <c>users.login</c> bilan bir
/// xil raqam bo'lishi mumkin va bu to'g'ri — qaysi jadval o'qilishini
/// endpoint hal qiladi.
/// </para>
/// <para>
/// ⭐ <b>Hisob AVTOMATIK ochiladi</b> — har bir o'chirilmagan xodimga
/// (fon vazifasi va HR «Ilova» bo'limini ochgan lahza). Qo'lda «hisob
/// ochish», «hisobni yopish» va majburiy parol almashtirish YO'Q. Yagona
/// o'chirgich — ishdan ketish: bugungi mehnat munosabati bo'lmasa kirish,
/// yangilash va <c>/api/me/*</c> to'xtaydi (INV-1, INV-2), qayta ishga
/// olinsa — o'sha parol bilan yana ishlaydi.
/// </para>
/// <para>
/// ⭐ <b>Parolni faqat HR beradi</b> va uni HAR DOIM ko'ra oladi: xesh
/// tekshiruv uchun, <see cref="PasswordProtected"/> esa shifrlangan nusxa —
/// HR ko'rishi uchun. Xodim parolni almashtira olmaydi.
/// </para>
/// <para>
/// <b>Yumshoq o'chirish YO'Q.</b> Hisobni xodim yashatadi: xodim
/// o'chirilsa, uning hisobi kirish yo'lida umuman topilmaydi. Tarix —
/// <c>audit_logs</c> da.
/// </para>
/// </remarks>
public sealed class EmployeeAccount : AggregateRoot, IAuditable
{
    /// <summary>Ketma-ket necha xatodan keyin kirish bloklanadi.</summary>
    /// <remarks>
    /// Son <see cref="User.RecordFailedLogin"/> dagi bilan bir xil bo'lishi
    /// SHART: ikki xil hisob turi uchun ikki xil qoida qo'yilsa, «nega meni
    /// bloklab qo'ydi» degan savolga ikki xil javob bo'lardi. Blokning o'zi
    /// hisob qatorida EMAS — (login, IP) juftligida
    /// (<c>EmployeeLoginThrottle</c>).
    /// </remarks>
    public const int MaxFailedAttempts = 5;

    /// <summary>Blok necha daqiqa turadi.</summary>
    public const int LockoutMinutes = 15;

    private EmployeeAccount(
        Guid id,
        Guid employeeId,
        string passwordHash,
        string? passwordProtected,
        DateTimeOffset passwordSetAt)
        : base(id)
    {
        EmployeeId = employeeId;
        PasswordHash = passwordHash;
        PasswordProtected = passwordProtected;
        PasswordSetAt = passwordSetAt;
    }

    /// <summary>EF Core uchun.</summary>
    private EmployeeAccount() => PasswordHash = string.Empty;

    /// <summary>Kimning hisobi. Bitta xodimda bitta hisob (unikal indeks).</summary>
    public Guid EmployeeId { get; private set; }

    /// <summary>Parol xeshi — kirishda FAQAT shu tekshiriladi.</summary>
    public string PasswordHash { get; private set; }

    /// <summary>
    /// Ochiq parolning SHIFRLANGAN nusxasi (<c>ISecretProtector</c>) — HR ko'rishi uchun.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Egasining qarori (2026-09-27): parolni HR beradi va uni istalgan
    /// payt ko'ra oladi — xodim unutsa, HR qaytadan aytib beradi. Xesh
    /// bir tomonlama, shu sababli ikkinchi, QAYTARILADIGAN nusxa kerak.
    /// U bazaning nusxasidan himoyalangan (kalit muhitda), serverga to'liq
    /// kirgan odamdan esa yo'q — Telegram tokeni bilan bir xil chegara.
    /// </para>
    /// <para>
    /// <c>null</c> — nusxa yo'q (bu qaror oldidan berilgan parol). Kalit
    /// almashsa nusxa ochilmay qoladi; ikkala holatda ham HR «Yangi parol
    /// yasash» ni bosadi.
    /// </para>
    /// </remarks>
    public string? PasswordProtected { get; private set; }

    /// <summary>
    /// Joriy parol qachon berilgan.
    /// </summary>
    /// <remarks>
    /// <c>updated_at</c> buning o'rnini BOSMAYDI: u har muvaffaqiyatli
    /// kirishda ham yangilanadi (<see cref="LastLoginAt"/>) va «parol
    /// qachon berilgan» degan javob yo'qoladi.
    /// </remarks>
    public DateTimeOffset PasswordSetAt { get; private set; }

    /// <summary>
    /// Joriy parolni HR birinchi marta QACHON bilib oldi — ko'rdi yoki o'zi yozdi.
    /// <c>null</c> — parolni hech kim bilmaydi va hisob bilan KIRIB BO'LMAYDI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega kerak</b> (2026-10-12, ilova internetga ochilgach; 2026-09-30
    /// xavfsizlik ko'rigining bandi «HR bermagan hisob kira olmasin»). Hisob har
    /// bir xodimga AVTOMATIK ochiladi va 6 xonali parol oladi — ~2000 hisob.
    /// Ularning ko'pchiligining parolini hech kim bilmaydi: xodim ilovadan
    /// foydalanmaydi, HR esa parolni unga aytmagan. Lekin hammasi internetdan
    /// taxmin qilishga ochiq edi: ko'p raqamga bir vaqtda urinish millionlik
    /// PIN fazosini tez qisqartiradi. Endi faqat HR haqiqatan bergan (ko'rgan
    /// yoki o'zi yozgan) parol ishlaydi — hujum yuzasi ilovani ishlatayotgan
    /// odamlar soniga tushadi.
    /// </para>
    /// <para>
    /// Avtomatik yasalgan parol (<see cref="Issue"/>, HR «Yangi parol yasash»)
    /// bu belgini TOZALAYDI: yangi parolni hali hech kim bilmaydi — HR uni
    /// ko'rgan lahzada (<see cref="MarkPasswordHandedOut"/>) kirish ochiladi.
    /// </para>
    /// </remarks>
    public DateTimeOffset? PasswordHandedOutAt { get; private set; }

    /// <summary><c>null</c> — hech qachon kirmagan. Parol almashsa ham TOZALANMAYDI.</summary>
    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>Yangi hisob — xodimga avtomatik ochiladi.</summary>
    /// <param name="employeeId">Kimga.</param>
    /// <param name="passwordHash">Tayyor xesh (yasash Infrastructure ishi).</param>
    /// <param name="passwordProtected">Ochiq parolning shifrlangan nusxasi.</param>
    /// <param name="at">Parol berilgan lahza.</param>
    public static Result<EmployeeAccount> Issue(
        Guid employeeId,
        string passwordHash,
        string? passwordProtected,
        DateTimeOffset at)
    {
        if (employeeId == Guid.Empty)
        {
            return Error.Validation("xodim_hisobi.xodim_yoq", "Xodim tanlanishi shart.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Error.Validation("xodim.parol_bosh", "Parol kiritilmagan.");
        }

        return new EmployeeAccount(NewId(), employeeId, passwordHash, passwordProtected, at);
    }

    /// <summary>
    /// HR yangi parol beradi — avtomatik yasalgan yoki o'zi yozgan.
    /// </summary>
    /// <remarks>
    /// <see cref="LastLoginAt"/> ATAYLAB tegilmaydi: «bu odam umuman kirganmi»
    /// degan savol parol almashganidan keyin ham javobini saqlashi kerak.
    /// </remarks>
    /// <param name="passwordHash">Yangi xesh.</param>
    /// <param name="passwordProtected">Yangi parolning shifrlangan nusxasi.</param>
    /// <param name="at">Lahza.</param>
    /// <param name="knownToHr">
    /// HR parolni o'zi yozdimi (<c>true</c> — u allaqachon biladi, kirish darhol
    /// ochiq) yoki u avtomatik yasaldimi (<c>false</c> — HR ko'rmaguncha yopiq,
    /// <see cref="PasswordHandedOutAt"/>).
    /// </param>
    public void SetPassword(string passwordHash, string passwordProtected, DateTimeOffset at, bool knownToHr)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordProtected);

        PasswordHash = passwordHash;
        PasswordProtected = passwordProtected;
        PasswordSetAt = at;
        PasswordHandedOutAt = knownToHr ? at : null;
    }

    /// <summary>
    /// HR joriy parolni ko'rdi — endi uni xodimga aytishi mumkin, kirish ochiladi.
    /// </summary>
    /// <remarks>
    /// Faqat BIRINCHI marta yoziladi: keyingi ko'rishlar «qachondan beri ishlaydi»
    /// degan javobni o'zgartirmaydi (har bir ko'rish baribir auditda).
    /// </remarks>
    /// <param name="at">Lahza.</param>
    public void MarkPasswordHandedOut(DateTimeOffset at) => PasswordHandedOutAt ??= at;

    /// <summary>Joriy parol bilan kirish mumkinmi — HR uni bilib olganmi.</summary>
    public bool CanSignIn => PasswordHandedOutAt is not null;

    /// <summary>
    /// Xeshni yangi algoritm parametrlari bilan qayta yozadi — parolning O'ZI o'zgarmaydi.
    /// </summary>
    /// <remarks>
    /// Kirishda xeshlagich «qayta xeshlash kerak» desa chaqiriladi.
    /// <see cref="PasswordSetAt"/> va shifrlangan nusxa tegilmaydi: HR
    /// nuqtai nazaridan parol almashmagan.
    /// </remarks>
    /// <param name="passwordHash">Yangi xesh.</param>
    public void RehashPassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    /// <summary>Muvaffaqiyatli kirish.</summary>
    /// <param name="at">Lahza.</param>
    public void RecordSuccessfulLogin(DateTimeOffset at) => LastLoginAt = at;
}
