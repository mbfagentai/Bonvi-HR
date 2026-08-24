using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Common;

/// <summary>Xodimda faol qora ro'yxat / nazorat belgisi bormi.</summary>
/// <param name="Blacklisted">Qora ro'yxatda.</param>
/// <param name="Monitored">Nazoratda.</param>
public sealed record FlagState(bool Blacklisted, bool Monitored)
{
    /// <summary>Ikkalasidan biri ham yo'q.</summary>
    public bool IsClean => !Blacklisted && !Monitored;
}

/// <summary>
/// Ishga olish va bo'shatishdagi belgi tekshiruvi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bitta joyda.</b> HR ikkita aniq qoida so'radi va ular bir xil
/// ma'lumotga tayanadi:
/// </para>
/// <list type="number">
///   <item><b>Qayta ishga olish</b> — qora ro'yxat amalni to'xtatadi,
///   nazorat esa faqat ogohlantiradi.</item>
///   <item><b>Bo'shatish</b> — ikkalasi ham tasdiqsiz o'tmaydi, chunki
///   ketayotgan odam bo'yicha xavfsizlik xizmatining ochiq savoli bo'lishi mumkin.</item>
/// </list>
/// <para>
/// Qoidalar farq qiladi, lekin manbasi bitta — shu sababli o'qish shu yerda,
/// qaror esa har bir use-case'da.
/// </para>
/// </remarks>
public sealed class EmployeeFlagGuard
{
    private readonly IEmployeeFlagRepository _flags;
    private readonly IClock _clock;

    public EmployeeFlagGuard(IEmployeeFlagRepository flags, IClock clock)
    {
        _flags = flags;
        _clock = clock;
    }

    public async Task<FlagState> InspectAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        var blacklisted = await _flags
            .HasActiveFlagAsync(employeeId, EmployeeFlagType.Blacklist, today, cancellationToken)
            .ConfigureAwait(false);

        var monitored = await _flags
            .HasActiveFlagAsync(employeeId, EmployeeFlagType.Monitoring, today, cancellationToken)
            .ConfigureAwait(false);

        return new FlagState(blacklisted, monitored);
    }
}
