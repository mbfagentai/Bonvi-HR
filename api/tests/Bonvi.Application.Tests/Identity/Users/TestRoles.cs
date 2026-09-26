using Bonvi.Domain.Identity;

namespace Bonvi.Application.Tests.Identity.Users;

/// <summary>
/// Testlar uchun rollar — HAQIQIY ruxsatlari bilan.
/// </summary>
/// <remarks>
/// «Kim kimga tega oladi» qoidasi endi rol KODIGA emas, RUXSATLARGA
/// qaraydi (bosh rahbar HR menejerni boshqaradi, chunki HR menejerining
/// ruxsatlari uning ruxsatlarining qism to'plami). Ruxsatsiz yasalgan
/// rol bilan bu qoida sinalmasdi — testlar tasodifan o'tib ketardi.
/// </remarks>
internal static class TestRoles
{
    /// <summary>Standart rol — <see cref="SystemRoles.Defaults"/> dagi nom va ruxsatlar bilan.</summary>
    public static Role Standart(string code)
    {
        var (name, permissions) = SystemRoles.Defaults[code];

        var role = Role.Create(code, name, isSystem: true).Value;
        role.ReplacePermissions(permissions).IsSuccess.ShouldBeTrue();

        return role;
    }

    /// <summary>Egasi muharrirda yaratgandek oddiy rol.</summary>
    public static Role Maxsus(string name, params string[] permissions)
    {
        var role = Role.Create("custom_" + Guid.NewGuid().ToString("N")[..8], name).Value;
        role.ReplacePermissions(permissions).IsSuccess.ShouldBeTrue();

        return role;
    }
}
