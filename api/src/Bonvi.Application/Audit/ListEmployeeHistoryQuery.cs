using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Audit;

/// <summary>
/// Xodim kartasidagi «O'zgarishlar tarixi» — shu xodimga tegishli o'zgarishlar
/// (SPEC-rollar-va-audit §8.3, §9.6).
/// </summary>
/// <remarks>
/// <para>
/// Davr filtri ATAYLAB yo'q: kartada butun tarix kerak («okladni kim o'zgartirdi va oldin
/// qancha edi» — yillar oldingi qaror bo'lishi mumkin). So'rov baribir arzon — u
/// <c>(subject_employee_id, occurred_at)</c> qisman indeksidan o'qiydi.
/// </para>
/// <para>
/// Faqat o'zgarishlar: kirish hodisalari va eksport xodimga bog'lanmaydi. Ko'rinish va
/// yashirish ro'yxatdagi bilan AYNAN bir xil. Notanish xodim — xato emas, bo'sh sahifa:
/// o'chirilgan kartaning tarixi ham shu yo'ldan o'qiladi.
/// </para>
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Module">Faqat shu bo'lim (kartadagi bo'lim chiplari).</param>
/// <param name="Page">Sahifa, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi (sukut 50, eng ko'pi 200).</param>
public sealed record ListEmployeeHistoryQuery(
    Guid EmployeeId,
    string? Module = null,
    int? Page = null,
    int? PageSize = null);

public sealed class ListEmployeeHistoryQueryHandler
{
    private readonly IAuditReadModel _readModel;
    private readonly ICurrentUser _currentUser;

    public ListEmployeeHistoryQueryHandler(IAuditReadModel readModel, ICurrentUser currentUser)
    {
        _readModel = readModel;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<AuditEntryDto>>> HandleAsync(
        ListEmployeeHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var module = AuditFilterRules.ParseModule(query.Module);
        if (module.IsFailure)
        {
            return module.Error;
        }

        var (page, pageSize) = AuditFilterRules.Paging(query.Page, query.PageSize);

        var filter = new AuditListFilter(
            AuditVisibility.ViewerOf(_currentUser),
            From: null,
            To: null,
            Changes: true,
            Security: false,
            ActorUserId: null,
            ActorSystem: false,
            module.Value,
            Action: null,
            query.EmployeeId,
            Entity: null,
            EntityId: null,
            page,
            pageSize);

        var found = await _readModel.ListAsync(filter, cancellationToken).ConfigureAwait(false);

        return AuditFilterRules.ToPaged(found, page, pageSize);
    }
}
