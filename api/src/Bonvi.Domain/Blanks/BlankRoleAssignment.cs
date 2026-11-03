using Bonvi.Domain.Common;

namespace Bonvi.Domain.Blanks;

/// <summary>
/// Lavozim rolini foydalanuvchiga biriktirish («Blankalar → Rollar»), SPEC-blankalar §3.6, §6.2.
/// </summary>
/// <remarks>
/// Rolga hech kim biriktirilmagan bo'lsa, uning qadamini istalgan <c>blanks.approve</c> egasi
/// imzolaydi; biriktirilgan bo'lsa — faqat biriktirilganlar. Tahrir yo'q: biriktirish olib
/// tashlanadi yoki qo'shiladi. Foydalanuvchi o'chirilsa yozuv ham o'chadi (CASCADE).
/// </remarks>
public sealed class BlankRoleAssignment : AggregateRoot, IAuditable
{
    private BlankRoleAssignment(Guid id, string roleCode, Guid userId)
        : base(id)
    {
        RoleCode = roleCode;
        UserId = userId;
    }

    private BlankRoleAssignment() { }

    /// <summary>Lavozim roli (<see cref="BlankActorRoles.OfficeRoles"/>).</summary>
    public string RoleCode { get; private set; } = string.Empty;

    /// <summary>Panel foydalanuvchisi.</summary>
    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>Yangi biriktirish — faqat lavozim roli (shaxs rollari blankaning o'zidan aniqlanadi).</summary>
    /// <param name="roleCode">Rol.</param>
    /// <param name="userId">Foydalanuvchi.</param>
    public static Result<BlankRoleAssignment> Create(string? roleCode, Guid userId)
    {
        if (!BlankActorRoles.IsOfficeRole(roleCode))
        {
            return BlankErrors.InvalidRole;
        }

        if (userId == Guid.Empty)
        {
            return BlankErrors.UserNotFound;
        }

        return new BlankRoleAssignment(NewId(), roleCode!, userId);
    }
}
