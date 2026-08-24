using Bonvi.Domain.Common;

namespace Bonvi.Domain.Abstractions;

/// <summary>
/// Ma'lumotnomalar uchun umumiy saqlash porti.
/// </summary>
/// <remarks>
/// Barcha ma'lumotnomalar bir xil ishlaydi: ro'yxat, qidiruv, nom bo'yicha
/// takrorni tekshirish. Shu sababli har biri uchun alohida interfeys
/// yozilmaydi — bittasi yetarli.
/// </remarks>
public interface IReferenceRepository<TEntity> : IRepository<TEntity>
    where TEntity : ReferenceEntity
{
    /// <summary>Barcha yozuvlar. <paramref name="onlyActive"/> — faqat faollari.</summary>
    Task<IReadOnlyList<TEntity>> ListAsync(
        bool onlyActive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nom bo'yicha topadi (registr va bo'shliqlarga sezgir emas).
    /// Import va takrorni tekshirish uchun.
    /// </summary>
    Task<TEntity?> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Bunday nom band emasligini tekshiradi.</summary>
    Task<bool> NameExistsAsync(
        string name,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default);

}
