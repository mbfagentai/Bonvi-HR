using Bonvi.Domain.Common;

namespace Bonvi.Application.Audit;

/// <summary>
/// «Kim» filtrining tanlovlari — jurnalda kamida bir marta uchragan tizim foydalanuvchilari.
/// </summary>
/// <remarks>
/// <para>
/// Ro'yxat so'rovchining bo'limlari bilan TORAYTIRILMAYDI: bu 3–4 HR va bot hisobi,
/// ularning ismi sir emas, qatorlarning o'zi esa baribir ko'rinish bo'yicha filtrlanadi
/// (ko'rinmaydigan bo'limdagi odam tanlansa — sahifa bo'sh). Toraytirish har ochilishda
/// butun jurnalni o'qishni talab qilardi.
/// </para>
/// <para>
/// Xodim ilovasi va qurilmalar bu ro'yxatda yo'q: ularda <c>user_id</c> bo'sh, filtr esa
/// faqat foydalanuvchi yoki <c>system</c>.
/// </para>
/// </remarks>
public sealed class ListAuditActorsQueryHandler
{
    private readonly IAuditReadModel _readModel;

    public ListAuditActorsQueryHandler(IAuditReadModel readModel) => _readModel = readModel;

    public async Task<Result<IReadOnlyList<AuditActorOptionDto>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var actors = await _readModel.ListActorsAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(actors);
    }
}
