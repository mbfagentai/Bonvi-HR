using Bonvi.Domain.Common;

namespace Bonvi.Application.Catalog.Common;

/// <summary>
/// Ma'lumotnoma yozuvi TAHRIRLANIB saqlangandan keyin bajariladigan qo'shimcha ish.
/// </summary>
/// <remarks>
/// <para>
/// Generik tahrirlash handleri (<c>UpdateReferenceCommandHandler</c>) boshqa
/// modullarni bilmasligi kerak, lekin ba'zi maydon o'zgarishi boshqa joyda
/// iz qoldiradi: yo'qlik turining «ish vaqti hisoblanadi» bayrog'i o'zgarsa,
/// o'sha turdagi so'rovlar kunlari qayta hisoblanishi shart
/// (SPEC-so'rovlar §B3 AMENDED). Ilgak shu bog'liqlikni handler'ga
/// tegmasdan ulaydi; ilgagi yo'q ma'lumotnomalar avvalgidek ishlaydi.
/// </para>
/// <para>
/// Ikki qadam: <see cref="Snapshot"/> o'zgarishdan OLDIN holatni oladi,
/// <see cref="OnSavedAsync"/> saqlangandan KEYIN uni yangisi bilan solishtiradi.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">Ma'lumotnoma entity'si.</typeparam>
public interface IReferenceSavedHook<in TEntity>
    where TEntity : ReferenceEntity
{
    /// <summary>Tahrirlashdan oldingi kerakli holat.</summary>
    /// <param name="entity">Hali o'zgartirilmagan yozuv.</param>
    object? Snapshot(TEntity entity);

    /// <summary>Saqlangandan keyin — eski holat bilan solishtirib ishlaydi.</summary>
    /// <param name="entity">Saqlangan yozuv.</param>
    /// <param name="snapshot"><see cref="Snapshot"/> qaytargan qiymat.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task OnSavedAsync(TEntity entity, object? snapshot, CancellationToken cancellationToken);
}
