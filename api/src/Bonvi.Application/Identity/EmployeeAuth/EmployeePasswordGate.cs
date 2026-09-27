using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.EmployeeAuth;

/// <summary>
/// «Bir martalik parol hali almashtirilmagan» to'sig'i — <c>/api/me/*</c> ning har bir so'rovi uchun.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega kerak.</b> HR bergan parol — o'qib yozib olinadigan satr
/// (qog'ozda, suhbatda, HR ning ekranida). U almashtirilmaguncha hisob faqat
/// parolni almashtirish endpointiga ruxsat berishi kerak
/// (<see cref="EmployeeAccountErrors.PasswordChangeRequired"/>). Ilgari bu
/// qoida faqat ILOVADA edi: ilova foydalanuvchini parol ekraniga
/// yo'naltirardi, server esa bir martalik parol bilan olingan tokenga
/// belgilash, davomat va so'rovlarni cheksiz ochib berardi.
/// </para>
/// <para>
/// ⚠️ <b>Bayroq HISOB QATORIDAN o'qiladi, tokendan emas.</b> Tokenga
/// da'vo sifatida yozilsa, HR parolni tiklagan lahzadagi eski (bayroqsiz)
/// token 15 daqiqa amal qilishda davom etardi. Narxi — har so'rovda birlamchi
/// kalit bo'yicha bitta ustunni o'qish.
/// </para>
/// <para>
/// Hisob topilmasa yoki tokenda hisob bo'lmasa to'siq hech narsa DEMAYDI:
/// bu boshqa savol va unga ishlovchilarning o'zi (<c>xodim.hisob_faol_emas</c>)
/// javob beradi — ikkita joyda ikki xil javob bo'lmasligi uchun.
/// </para>
/// </remarks>
public sealed class EmployeePasswordGate
{
    private readonly ICurrentEmployee _currentEmployee;
    private readonly IEmployeeAccountRepository _accounts;

    public EmployeePasswordGate(ICurrentEmployee currentEmployee, IEmployeeAccountRepository accounts)
    {
        _currentEmployee = currentEmployee;
        _accounts = accounts;
    }

    /// <summary>Parol o'rnatilganmi — o'rnatilmagan bo'lsa <c>xodim.parol_almashtirilsin</c>.</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<Result> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_currentEmployee.AccountId is not { } accountId)
        {
            return Result.Success();
        }

        var mustChange = await _accounts
            .MustChangePasswordAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        return mustChange == true
            ? EmployeeAccountErrors.PasswordChangeRequired
            : Result.Success();
    }
}
