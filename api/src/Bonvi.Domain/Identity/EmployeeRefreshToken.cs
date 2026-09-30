using Bonvi.Domain.Common;

namespace Bonvi.Domain.Identity;

/// <summary>
/// Xodim ilovasining yangilash tokeni.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega <see cref="RefreshToken"/> dan ALOHIDA jadval.</b> U yerdagi
/// <c>user_id</c> — <c>NOT NULL</c> va <c>users(id)</c> ga qaraydi. Xodim
/// hisobi esa ataylab <c>users</c> da emas (<see cref="EmployeeAccount"/>
/// izohi), ya'ni u qatordan foydalana olmaydi.
/// </para>
/// <para>
/// <b>Rad etilgan muqobil</b> — <c>refresh_tokens</c> ga
/// <c>employee_account_id</c> qo'shib, <c>user_id</c> ni nullable qilish. U
/// jadvalning shaklini o'zgartirardi, «token foydalanuvchiga tegishli»
/// degan invariantni buzardi va <c>token.UserId</c> ga tegadigan HAR BIR
/// yo'lni (aylantirish, chiqish, hammasini bekor qilish, tozalash) ikkinchi
/// subyekt turi haqida o'ylashga majburlardi. Ikkita kichik jadval — ikki
/// ma'noli bitta jadvaldan yaxshi.
/// </para>
/// <para>
/// <b>Rad etilgan muqobil</b> — yangilash tokenisiz, uzoq muddatli access
/// token. JWT ni serverda bekor qilib bo'lmaydi
/// (<c>JwtTokenService</c> izohi), ya'ni «telefonimni yo'qotdim» degan
/// gapga imzolash kalitini almashtirib, butun korxonani tizimdan
/// chiqarishdan boshqa javob qolmasdi.
/// </para>
/// <para>
/// Maydonlari <see cref="RefreshToken"/> bilan bir xil va aylantirish ham
/// bir xil: har yangilashda eskisi bekor qilinadi, bekor qilingani qayta
/// ishlatilsa — bu o'g'irlik belgisi va hisobning barcha tokenlari yopiladi.
/// </para>
/// </remarks>
public sealed class EmployeeRefreshToken : AggregateRoot
{
    private EmployeeRefreshToken(
        Guid id,
        Guid employeeAccountId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        string? createdByIp)
        : base(id)
    {
        EmployeeAccountId = employeeAccountId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        CreatedByIp = createdByIp;
    }

    /// <summary>EF Core uchun.</summary>
    private EmployeeRefreshToken() => TokenHash = string.Empty;

    /// <summary>Qaysi hisobga tegishli.</summary>
    public Guid EmployeeAccountId { get; private set; }

    /// <summary>Tokenning SHA-256 xeshi (hex). Ochiq token saqlanmaydi.</summary>
    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedByIp { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedByIp { get; private set; }

    /// <summary>Shu tokenning o'rniga berilgan yangisining xeshi (aylantirish zanjiri).</summary>
    public string? ReplacedByTokenHash { get; private set; }

    /// <summary>Yangi token beradi.</summary>
    /// <param name="employeeAccountId">Hisob.</param>
    /// <param name="tokenHash">Xesh.</param>
    /// <param name="expiresAt">Muddati.</param>
    /// <param name="createdAt">Berilgan lahza.</param>
    /// <param name="createdByIp">So'rov kelgan manzil, bo'lsa.</param>
    public static EmployeeRefreshToken Issue(
        Guid employeeAccountId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        string? createdByIp = null)
        => new(NewId(), employeeAccountId, tokenHash, expiresAt, createdAt, createdByIp);

    /// <summary>Token shu lahzada ishlaydimi.</summary>
    /// <param name="moment">Tekshirish lahzasi.</param>
    public bool IsActiveAt(DateTimeOffset moment) => RevokedAt is null && ExpiresAt > moment;

    /// <summary>Tokenni bekor qiladi. Ikkinchi chaqiruv hech narsa qilmaydi.</summary>
    /// <param name="at">Lahza.</param>
    /// <param name="byIp">So'rov manzili.</param>
    /// <param name="replacedByHash">O'rniga berilgan token xeshi.</param>
    public void Revoke(DateTimeOffset at, string? byIp = null, string? replacedByHash = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = at;
        RevokedByIp = byIp;
        ReplacedByTokenHash = replacedByHash;
    }
}
