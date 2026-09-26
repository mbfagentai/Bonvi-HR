using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Common;
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
    /// Pul qoidasi: amal ichida summa yuborilgan bo'lsa, <c>employment.salary</c> shart.
    /// </summary>
    /// <param name="user">So'rovchi.</param>
    /// <param name="amountSent">So'rovda summa bormi (<c>null</c> emasmi).</param>
    /// <remarks>
    /// <para>
    /// Ko'rish (<c>salary.view</c>) va O'ZGARTIRISH (<c>employment.salary</c>)
    /// — ikki xil huquq. Summa ko'chirish, qo'shimcha vazifa, sinov yakuni
    /// yoki yozuvni tuzatish ICHIDA kelsa ham, u baribir oklad o'zgarishi:
    /// aks holda «o'sha bo'limga ko'chirib, okladni oshirish» degan aylanma
    /// yo'l qolardi (sabab <c>Permissions.EmploymentActions</c> oldidagi izohda).
    /// </para>
    /// <para>
    /// Ishga qabuldagi boshlang'ich oklad bu qoidaga TUSHMAYDI — u qabul
    /// buyrug'ining o'zi, o'zgartiriladigan eski oklad yo'q.
    /// </para>
    /// </remarks>
    public static Result EnsureCanSendSalary(this ICurrentUser user, bool amountSent)
    {
        ArgumentNullException.ThrowIfNull(user);

        return !amountSent || user.HasPermission(Permissions.EmploymentSalary)
            ? Result.Success()
            : EmployeeErrors.SalaryPermissionRequired;
    }

    /// <summary>
    /// Oklad DTO'si — ruxsat bo'lmasa <c>null</c>.
    /// </summary>
    public static MoneyDto? Mask(this ICurrentUser user, Money? salary)
        => user.CanSeeSalary() && salary is not null
            ? new MoneyDto(salary.Amount, salary.Currency, salary.Formatted)
            : null;
}
