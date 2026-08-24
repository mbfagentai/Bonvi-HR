using Bonvi.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.Seed;

/// <summary>
/// Standart rollarni bazaga yozadi (<see cref="SystemRoles.Defaults"/>).
/// </summary>
/// <remarks>
/// <para>
/// Nega migratsiyada emas: rol — ma'lumot, sxema emas. Migratsiyaga yozilsa,
/// administrator ruxsatni o'zgartirgan zahoti keyingi migratsiya uni ortga
/// qaytarib qo'yadi.
/// </para>
/// <para>
/// Amal <b>idempotent</b>: bir necha marta chaqirilsa ham mavjud rollarga
/// tegmaydi. Shu sababli uni har bir ishga tushishda chaqirish xavfsiz —
/// yangi standart rol qo'shilsa, o'zi paydo bo'ladi.
/// </para>
/// <para>
/// Foydalanuvchi bu yerda yaratilmaydi: parol xeshlash Infrastructure'ning
/// boshqa qismiga tegishli va urug'lantirishni parol siyosatiga bog'lab qo'yardi.
/// </para>
/// </remarks>
public sealed class IdentitySeeder
{
    private readonly AppDbContext _context;

    public IdentitySeeder(AppDbContext context) => _context = context;

    /// <summary>
    /// Yetishmayotgan standart rollarni yaratadi. Mavjudlari o'zgartirilmaydi.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var mavjudKodlar = await _context.Roles
            .Select(role => role.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var yangiRollar = new List<Role>();

        foreach (var (kod, tavsif) in SystemRoles.Defaults)
        {
            if (mavjudKodlar.Contains(kod, StringComparer.Ordinal))
            {
                continue;
            }

            yangiRollar.Add(Yarat(kod, tavsif.Name, tavsif.Permissions));
        }

        if (yangiRollar.Count == 0)
        {
            return;
        }

        _context.Roles.AddRange(yangiRollar);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tizim rolini yig'adi. Xatolik bu yerda istisno: <see cref="SystemRoles"/>
    /// kodda yozilgan, ya'ni noto'g'ri qiymat — dastur xatosi, foydalanuvchi xatosi emas.
    /// </summary>
    private static Role Yarat(string kod, string nom, IReadOnlyList<string> ruxsatlar)
    {
        var natija = Role.Create(kod, nom, isSystem: true);

        if (natija.IsFailure)
        {
            throw new InvalidOperationException(
                $"«{kod}» tizim roli yaratilmadi: {natija.Error}");
        }

        var role = natija.Value;

        foreach (var ruxsat in ruxsatlar)
        {
            var berish = role.Grant(ruxsat);

            if (berish.IsFailure)
            {
                throw new InvalidOperationException(
                    $"«{kod}» roliga «{ruxsat}» ruxsati berilmadi: {berish.Error}");
            }
        }

        return role;
    }
}
