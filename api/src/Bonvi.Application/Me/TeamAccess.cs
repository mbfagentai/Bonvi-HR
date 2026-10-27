using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Me;

/// <summary>«Jamoam» so'rovchisi — kim (tokendan) va nimani ko'ra oladi (bazadan).</summary>
/// <param name="Ids">Ilova hisobi va xodim — hisob qatoridan.</param>
/// <param name="Scope">Shu lahzadagi qamrov; bo'sh emas.</param>
public sealed record TeamCaller(CurrentEmployeeIds Ids, TeamScope Scope);

/// <summary>
/// <c>/api/me/team/*</c> ning birinchi qadami: so'rovchini aniqlaydi va boshliq EMASLIGINI rad etadi.
/// </summary>
/// <remarks>
/// <para>
/// To'rtala endpoint shu bitta joydan o'tadi — «boshliqmi» degan savolning ikkinchi
/// ta'rifi yozilmaydi. Ishdan ketgan yoki o'chirilgan boshliq bu yerga umuman yetib
/// kelmaydi: butun guruh filtri (<c>EmployeeAccessGate</c>) uni oldinroq to'xtatadi.
/// </para>
/// <para>
/// Qamrov keshlanmaydi: HR boshliqni olib tashlasa, KEYINGI so'rovdayoq 403 (§B).
/// </para>
/// </remarks>
public sealed class TeamAccess
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly IDepartmentRepository _departments;

    public TeamAccess(CurrentEmployeeResolver resolver, IDepartmentRepository departments)
    {
        _resolver = resolver;
        _departments = departments;
    }

    /// <summary>So'rovchi va uning qamrovi; boshliq bo'lmasa — <c>jamoa.boshliq_emas</c> (403).</summary>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<Result<TeamCaller>> ResolveAsync(CancellationToken cancellationToken)
    {
        var ids = await _resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (ids.IsFailure)
        {
            return ids.Error;
        }

        var departments = await _departments.ListAsync(onlyActive: false, cancellationToken).ConfigureAwait(false);
        var scope = TeamScope.Of(ids.Value.EmployeeId, departments);

        return scope.IsHead ? new TeamCaller(ids.Value, scope) : TeamErrors.NotTeamLead;
    }
}
