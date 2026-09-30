using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.EmployeeAuth;

/// <summary>
/// «Bu odam hali ham xodimmi» to'sig'i — <c>/api/me/*</c> ning HAR BIR so'rovi uchun.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega kerak.</b> Qo'lda «hisobni yopish» yo'q (egasining qarori,
/// 2026-09-27): yagona o'chirgich — ishdan ketish. Kirish va yangilash
/// buni allaqachon tekshiradi (INV-1, INV-2), lekin access token 15 daqiqa
/// amal qiladi va usiz ishdan bo'shatilgan odam o'sha oynada belgilash,
/// davomat va so'rovlardan foydalanaverardi. To'siq bilan qaror SHU
/// ZAHOTI kuchga kiradi.
/// </para>
/// <para>
/// <b>Ikki savol, ikkalasi ham bazadan:</b> hisobning xodimi o'chirilmaganmi
/// (INV-1) va unda BUGUN (Toshkent sanasi) amaldagi mehnat munosabati bormi
/// (INV-2 — kirish yo'lidagi bilan AYNAN bir xil qoida). Javob tokendagi
/// da'vodan olinmaydi: da'vo token berilgan lahzaning nusxasi.
/// </para>
/// <para>
/// Rad javobi — <c>xodim.hisob_faol_emas</c> (403). Qayta ishga olingan
/// odamda yangi munosabat paydo bo'lishi bilan hammasi o'zi ochiladi.
/// </para>
/// <para>
/// Tokenda hisob bo'lmasa to'siq hech narsa DEMAYDI: bu boshqa savol va
/// unga ishlovchilarning o'zi javob beradi.
/// </para>
/// </remarks>
public sealed class EmployeeAccessGate
{
    private readonly ICurrentEmployee _currentEmployee;
    private readonly IEmployeeAccountRepository _accounts;
    private readonly IEmploymentRepository _employments;
    private readonly IClock _clock;

    public EmployeeAccessGate(
        ICurrentEmployee currentEmployee,
        IEmployeeAccountRepository accounts,
        IEmploymentRepository employments,
        IClock clock)
    {
        _currentEmployee = currentEmployee;
        _accounts = accounts;
        _employments = employments;
        _clock = clock;
    }

    /// <summary>Hisob hali ishlaydimi — ishlamasa <c>xodim.hisob_faol_emas</c>.</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<Result> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_currentEmployee.AccountId is not { } accountId)
        {
            return Result.Success();
        }

        var employeeId = await _accounts.LiveEmployeeIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (employeeId is null)
        {
            return EmployeeAccountErrors.AccountInactive;
        }

        var employed = await _employments
            .IsEmployedOnAsync(employeeId.Value, _clock.UtcNow.TashkentDate(), cancellationToken)
            .ConfigureAwait(false);

        return employed ? Result.Success() : EmployeeAccountErrors.AccountInactive;
    }
}
