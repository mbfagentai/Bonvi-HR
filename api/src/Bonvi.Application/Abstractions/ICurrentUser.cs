namespace Bonvi.Application.Abstractions;

/// <summary>
/// Joriy so'rovni bajarayotgan foydalanuvchi.
/// Audit jurnalida "kim o'zgartirdi" ni to'ldirish uchun ishlatiladi.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Foydalanuvchi identifikatori. Tizimga kirmagan bo'lsa — <c>null</c>.</summary>
    Guid? UserId { get; }

    /// <summary>Login (foydalanuvchi nomi).</summary>
    string? UserName { get; }

    /// <summary>Tizimga kirganmi.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Berilgan ruxsat mavjudmi.</summary>
    bool HasPermission(string permission);
}
