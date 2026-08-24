using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Catalog.Queries;

/// <summary>Ma'lumotnoma ro'yxatini so'rash.</summary>
/// <param name="OnlyActive">
/// <c>true</c> — faqat faol yozuvlar. Tanlash ro'yxatlari (masalan ishga qabul
/// formasi) shu rejimda so'raydi; ma'lumotnomani boshqarish sahifasi esa
/// hammasini so'raydi, aks holda faolsizlantirilgan yozuvni qayta yoqib
/// bo'lmaydi.
/// </param>
public sealed record ListReferenceQuery(bool OnlyActive = false);

/// <summary>
/// Har qanday ma'lumotnomaning ro'yxatini qaytaradi.
/// </summary>
/// <remarks>
/// <para>
/// Bu sinf sakkizta ma'lumotnomaga xizmat qiladi. U <c>LegalEntity</c> yoki
/// <c>PenaltyType</c> haqida hech narsa bilmaydi — faqat ikkita narsani biladi:
/// yozuvlarni qayerdan olish (<see cref="IReferenceRepository{TEntity}"/>) va
/// ularni qanday ko'rsatish (<see cref="IReferenceDefinition{TEntity, TDto}"/>).
/// </para>
/// <para>
/// Yangi ma'lumotnoma qo'shilganda bu fayl umuman o'zgarmaydi.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">Ma'lumotnoma entity'si.</typeparam>
/// <typeparam name="TDto">Tashqariga chiqadigan ko'rinish.</typeparam>
public sealed class ListReferenceQueryHandler<TEntity, TDto>
    where TEntity : ReferenceEntity
{
    private readonly IReferenceRepository<TEntity> _repository;
    private readonly IReferenceDefinition<TEntity, TDto> _definition;

    public ListReferenceQueryHandler(
        IReferenceRepository<TEntity> repository,
        IReferenceDefinition<TEntity, TDto> definition)
    {
        _repository = repository;
        _definition = definition;
    }

    public async Task<Result<IReadOnlyList<TDto>>> HandleAsync(
        ListReferenceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var items = await _repository.ListAsync(query.OnlyActive, cancellationToken).ConfigureAwait(false);

        // Tartib APPLICATION qatlamida qat'iylashtiriladi: repozitoriy
        // qaysi tartibda qaytarishi kafolatlanmagan, foydalanuvchi esa
        // ro'yxatni har safar bir xil ko'rishi kerak.
        // Alifbo tartibi madaniyatga bog'liq (kirill va lotin aralash) —
        // shu sababli ordinal emas, joriy madaniyat solishtiruvi.
        IReadOnlyList<TDto> result =
        [
            .. items
                .OrderBy(e => e.SortOrder)
                .ThenBy(e => e.Name, StringComparer.CurrentCulture)
                .Select(_definition.ToDto),
        ];

        return Result.Success(result);
    }
}
