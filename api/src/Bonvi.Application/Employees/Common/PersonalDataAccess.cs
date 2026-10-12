using Bonvi.Application.Abstractions;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Employees.Common;

/// <summary>
/// Xodimning shaxsiy ma'lumotini kim ko'radi — tizimdagi YAGONA qoida.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <c>HasPermission(employees.personal.view)</c> ning o'zi emas.</b> SPEC §3.4:
/// ruxsat «ko'rish» darajasida alohida beriladi, «o'zgartirish» darajasida esa O'Z-O'ZIDAN
/// bor — kartani tahrirlagan odam JSHSHIR va pasportni qayta yozadi va ularni ko'rmasa,
/// bo'sh qiymat yuborib o'chirib yuborardi. Rollar bazada kod ro'yxati bo'lib yotadi va
/// bugungi rollarda yangi kod yo'q; qoida shu yerda bo'lmasa, deploy kuni «HR menejeri»
/// o'zi tahrirlaydigan maydonlarni jurnalda yashirin ko'rardi.
/// </para>
/// <para>
/// <see cref="SalaryAccess"/> bilan bir xil sabab bilan alohida sinf: har bir handler
/// tekshiruvni o'zi yozsa, ertami-kechmi bittasida «yoki tahrirlash» qismi unutiladi.
/// Hozir audit jurnali ishlatadi (A2); xodim kartasi, ro'yxat va eksport (P1) ham shu
/// metodga tayanadi.
/// </para>
/// </remarks>
public static class PersonalDataAccess
{
    /// <summary>Foydalanuvchi xodimning shaxsiy ma'lumotini ko'ra oladimi.</summary>
    public static bool CanSeePersonalData(this ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.HasPermission(Permissions.EmployeesPersonalView)
               || user.HasPermission(Permissions.EmployeesEdit);
    }
}
