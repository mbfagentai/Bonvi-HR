using Bonvi.Domain.Common;

namespace Bonvi.Domain.Identity;

/// <summary>
/// Rol — ruxsatlar to'plami. Foydalanuvchiga rol beriladi, ruxsat emas.
/// </summary>
public sealed class Role : AggregateRoot, IAuditable
{
    private readonly List<string> _permissions = [];

    private Role(Guid id, string code, string name, bool isSystem)
        : base(id)
    {
        Code = code;
        Name = name;
        IsSystem = isSystem;
    }

    /// <summary>EF Core uchun.</summary>
    private Role()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    /// <summary>Mashina uchun kod: <c>hr_manager</c>. O'zgarmaydi.</summary>
    public string Code { get; private set; }

    /// <summary>Foydalanuvchiga ko'rinadigan nom.</summary>
    public string Name { get; private set; }

    /// <summary>Tizim roli — o'chirib bo'lmaydi.</summary>
    public bool IsSystem { get; private set; }

    public IReadOnlyList<string> Permissions => _permissions.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<Role> Create(string? code, string? name, bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Error.Validation("rol.kod_bosh", "Rol kodi kiritilishi shart.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("rol.nom_bosh", "Rol nomi kiritilishi shart.");
        }

        return new Role(NewId(), code.Trim().ToLowerInvariant(), name.Trim(), isSystem);
    }

    public Result Grant(string permission)
    {
        if (!Domain.Identity.Permissions.Exists(permission))
        {
            return Error.Validation("rol.notanish_ruxsat", $"«{permission}» — bunday ruxsat mavjud emas.");
        }

        if (!_permissions.Contains(permission, StringComparer.Ordinal))
        {
            _permissions.Add(permission);
        }

        return Result.Success();
    }

    public Result Revoke(string permission)
    {
        if (IsSystem && Code == SystemRoles.Admin)
        {
            return Error.Conflict(
                "rol.admin_ozgartirilmaydi",
                "Administrator rolidan ruxsat olib tashlab bo'lmaydi.");
        }

        _permissions.Remove(permission);
        return Result.Success();
    }

    public bool Has(string permission) => _permissions.Contains(permission, StringComparer.Ordinal);

    public Result Rename(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("rol.nom_bosh", "Rol nomi kiritilishi shart.");
        }

        Name = name.Trim();
        return Result.Success();
    }
}
