using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Identity;

/// <summary>
/// Tizim foydalanuvchisi.
/// </summary>
/// <remarks>
/// <para>
/// Foydalanuvchi va xodim — bu ikki xil narsa. 540 ta xodim bor, lekin
/// tizimga faqat bir nechtasi kiradi (HR, rahbariyat, bo'lim boshliqlari).
/// Shu sababli <c>EmployeeId</c> ixtiyoriy: foydalanuvchi xodimga
/// bog'langan bo'lishi ham, bo'lmasligi ham mumkin.
/// </para>
/// <para>
/// Login sifatida telefon raqami ishlatiladi — HR xodimlari uchun
/// eng tanish identifikator (elektron pochta hammada ham yo'q).
/// </para>
/// </remarks>
public sealed class User : AggregateRoot, IAuditable, ISoftDeletable
{
    private readonly List<Guid> _roleIds = [];

    private User(Guid id, PhoneNumber login, string passwordHash, FullName fullName)
        : base(id)
    {
        Login = login;
        PasswordHash = passwordHash;
        FullName = fullName;
        IsActive = true;
    }

    /// <summary>EF Core uchun.</summary>
    private User()
    {
        Login = null!;
        PasswordHash = string.Empty;
        FullName = null!;
    }

    /// <summary>Kirish uchun telefon raqami. Unikal.</summary>
    public PhoneNumber Login { get; private set; }

    /// <summary>Parolning xeshi. Ochiq parol HECH QAYERDA saqlanmaydi.</summary>
    public string PasswordHash { get; private set; }

    public FullName FullName { get; private set; }

    /// <summary>Bog'langan xodim. Bo'lmasligi mumkin (masalan tashqi administrator).</summary>
    public Guid? EmployeeId { get; private set; }

    /// <summary>
    /// Hisob rasmining saqlash kaliti. Rasm yo'q bo'lsa — <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega xodim rasmi qayta ishlatilmadi.</b> <see cref="EmployeeId"/>
    /// ixtiyoriy va amalda bo'sh: tizimga kiradigan hisoblarning hech biri
    /// xodim kartasiga bog'lanmagan (boshlang'ich administratorda esa bog'lash
    /// imkoni ham yo'q). Ya'ni «rasmni xodimdan ol» degan yo'l hech kimga
    /// ishlamagan bo'lardi.
    /// </para>
    /// <para>
    /// <b>Ikkita rasm haqida.</b> Hisob bog'langan bo'lsa, bir odamda ikki
    /// surat bo'lib qolishi mumkin — bu ataylab: kadrlar kartasidagi rasm
    /// HR hujjati, hisobdagisi esa foydalanuvchining o'zi tanlagan avatar.
    /// Ularni bitta maydonga qo'shib yuborish HR ma'lumotini foydalanuvchi
    /// qo'liga berardi.
    /// </para>
    /// </remarks>
    public string? PhotoKey { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>Ketma-ket muvaffaqiyatsiz urinishlar soni.</summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>Shu vaqtgacha kirish bloklangan.</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    public IReadOnlyList<Guid> RoleIds => _roleIds.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public static Result<User> Create(PhoneNumber login, string passwordHash, FullName fullName)
    {
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(fullName);

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Error.Validation("foydalanuvchi.parol_bosh", "Parol kiritilishi shart.");
        }

        return new User(NewId(), login, passwordHash, fullName);
    }

    public void AssignRole(Guid roleId)
    {
        if (!_roleIds.Contains(roleId))
        {
            _roleIds.Add(roleId);
        }
    }

    public void RemoveRole(Guid roleId) => _roleIds.Remove(roleId);

    /// <summary>
    /// Roldan mahrum qiladi — O'ZINI O'ZI ADMINISTRATORLIKDAN
    /// CHETLASHTIRISHDAN HIMOYA bilan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Foydalanuvchi boshqaruvi endpoint'lari uchun mo'ljallangan. Agar shu
    /// hisobning o'zi (<paramref name="actingUserId"/> == <c>Id</c>)
    /// aynan administrator rolidan (<paramref name="adminRoleId"/>) o'zini
    /// mahrum qilishga urinsa — rad etiladi.
    /// </para>
    /// <para>
    /// Nega muhim: aks holda administrator adashib (yoki ataylab) o'zidan
    /// yagona huquqni olib tashlab, tizimga hech kim kira olmay qoladigan
    /// holatga tushirib qo'yishi mumkin. Bu tekshiruv faqat identifikatorlar
    /// taqqoslanadi — boshqa agregatga (masalan «yana nechta admin bor?»)
    /// murojaat qilmaydi, shu sababli sof domen qoidasi.
    /// </para>
    /// </remarks>
    public Result RemoveRole(Guid roleId, Guid actingUserId, Guid adminRoleId)
    {
        if (Id == actingUserId && roleId == adminRoleId)
        {
            return Error.Forbidden(
                "foydalanuvchi.ozini_adminlikdan_chetlashtirish",
                "O'zingizdan administrator rolini olib tashlay olmaysiz.");
        }

        _roleIds.Remove(roleId);
        return Result.Success();
    }

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    /// <summary>Hisobni faolsizlantiradi. Tekshiruvsiz — faqat ichki/test holatlar uchun.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Hisobni faolsizlantiradi — O'ZINI O'ZI BLOKLASHDAN HIMOYA bilan.
    /// </summary>
    /// <remarks>
    /// Foydalanuvchi boshqaruvi endpoint'i shuni chaqiradi. Agar
    /// <paramref name="actingUserId"/> aynan shu hisobning o'zi bo'lsa —
    /// rad etiladi: aks holda administrator o'z hisobini faolsizlantirib,
    /// tizimga hech kim kira olmay qoladigan holatga tushirib qo'yishi mumkin.
    /// </remarks>
    public Result Deactivate(Guid actingUserId)
    {
        if (Id == actingUserId)
        {
            return Error.Forbidden(
                "foydalanuvchi.ozini_blokash",
                "O'zingizning hisobingizni faolsizlantira olmaysiz.");
        }

        IsActive = false;
        return Result.Success();
    }

    public void Activate()
    {
        IsActive = true;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    public void LinkToEmployee(Guid employeeId) => EmployeeId = employeeId;

    /// <summary>Ism-familiyani yangilaydi.</summary>
    public void Rename(FullName fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        FullName = fullName;
    }

    /// <summary>
    /// Kirish uchun telefon raqamini yangilaydi.
    /// </summary>
    /// <remarks>
    /// Unikallik BU YERDA tekshirilmaydi — bu use-case qatlamining ishi
    /// (bazadan boshqa foydalanuvchini so'rash kerak). Domen faqat
    /// qiymatning o'zi to'g'ri formatda ekanini kafolatlaydi, buni esa
    /// <see cref="PhoneNumber"/> allaqachon qilgan.
    /// </remarks>
    public void ChangeLogin(PhoneNumber login)
    {
        ArgumentNullException.ThrowIfNull(login);
        Login = login;
    }

    /// <summary>Hisob rasmini o'rnatadi yoki (<c>null</c> bilan) olib tashlaydi.</summary>
    public void SetPhoto(string? photoKey) => PhotoKey = photoKey;

    /// <summary>Muvaffaqiyatli kirish qayd etiladi.</summary>
    public void RecordSuccessfulLogin(DateTimeOffset at)
    {
        LastLoginAt = at;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    /// <summary>
    /// Muvaffaqiyatsiz urinish. 5 martadan keyin 15 daqiqaga bloklanadi.
    /// </summary>
    /// <remarks>
    /// Nega kerak: parolni brute-force bilan topishga urinishni sekinlashtiradi.
    /// Butunlay bloklamaydi — aks holda raqib xodimni ataylab qulflab qo'yishi mumkin.
    /// </remarks>
    public void RecordFailedLogin(DateTimeOffset at)
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts >= 5)
        {
            LockedUntil = at.AddMinutes(15);
            FailedLoginAttempts = 0;
        }
    }

    public bool IsLockedAt(DateTimeOffset moment) => LockedUntil is not null && LockedUntil > moment;
}
