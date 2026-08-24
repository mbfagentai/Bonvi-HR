using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Common;

/// <summary>
/// Oklad maydonlarini ruxsatga qarab yashiradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega alohida joyda.</b> HR aniq talab qildi: Excel'da oklad faylni
/// ochgan har kimga ko'rinardi, tizimda esa faqat <c>salary.view</c> ruxsati
/// borlarga. Agar bu tekshiruv har bir handler ichida takrorlansa, ertami-kechmi
/// bittasida unutiladi va oklad «tasodifan» tarqaladi.
/// </para>
/// <para>
/// <b>Nega DTO'da <c>null</c>, «0» emas.</b> Nol — bu haqiqiy qiymat
/// («oklad belgilanmagan»). Ruxsat yo'qligi esa boshqa narsa: maydon
/// umuman yo'q. Interfeys ikkalasini farqlay olishi kerak.
/// </para>
/// </remarks>
public static class SalaryAccess
{
    /// <summary>Foydalanuvchi oklad ko'ra oladimi.</summary>
    public static bool CanSeeSalary(this ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.HasPermission(Permissions.SalaryView);
    }

    /// <summary>
    /// Oklad DTO'si — ruxsat bo'lmasa <c>null</c>.
    /// </summary>
    public static MoneyDto? Mask(this ICurrentUser user, Money? salary)
        => user.CanSeeSalary() && salary is not null
            ? new MoneyDto(salary.Amount, salary.Currency, salary.Formatted)
            : null;
}
