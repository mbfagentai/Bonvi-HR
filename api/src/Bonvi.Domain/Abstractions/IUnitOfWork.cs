namespace Bonvi.Domain.Abstractions;

/// <summary>
/// Tranzaksiya chegarasi: bir nechta o'zgarish — bitta atomik saqlash.
/// </summary>
/// <remarks>
/// Masalan "ishdan bo'shatish" amalida bir vaqtda:
/// mehnat munosabati yopiladi, hodisa yoziladi, sinov muddati bekor qilinadi.
/// Ularning biri saqlanib, boshqasi saqlanmay qolishi mumkin emas.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>Barcha o'zgarishlarni bitta tranzaksiyada saqlaydi.</summary>
    /// <returns>O'zgargan yozuvlar soni.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
