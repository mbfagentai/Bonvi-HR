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

    /// <summary>
    /// Ishni BITTA ochiq tranzaksiya ichida bajaradi: o'qish, qulf va saqlash
    /// bir tranzaksiyada, oxirida bitta <c>COMMIT</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega kerak.</b> <see cref="SaveChangesAsync"/> o'zining ichki
    /// tranzaksiyasini ochadi va darhol yopadi. Undan OLDIN olingan qator
    /// qulfi (<c>SELECT … FOR UPDATE</c>) esa tranzaksiyasiz so'rovda
    /// olinadi va so'rov tugashi bilan bo'shaydi — ya'ni hech narsani
    /// navbatga qo'ymaydi. «O'qib, keyin yoz» qoidasi (masalan telefondan
    /// belgilashdagi 60 soniyalik takror) faqat shu metod ichida haqiqatan
    /// himoyalangan: qulf <c>COMMIT</c> gacha turadi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Ish QAYTA bajarilishi mumkin.</b> Baza vaqtinchalik nosozlik
    /// bersa (uzilish, deadlock), butun ish boshidan takrorlanadi va undan
    /// oldin kuzatuvdagi (tracked) o'zgarishlar tozalanadi. Shu sababli ish
    /// kerakli hamma narsani O'ZI o'qishi kerak — tashqarida yuklangan
    /// obyektga tayanmasligi.
    /// </para>
    /// <para>
    /// Ichma-ich chaqiruvda yangi tranzaksiya ochilmaydi: ish tashqi
    /// tranzaksiyaning ichida bajariladi.
    /// </para>
    /// <para>
    /// Sukut amalga oshirish — test o'rinbosarlari (fake) uchun: ular bu
    /// metodni ishlatmaydi va o'zgarmasdan kompilyatsiya bo'lishi kerak.
    /// Yagona haqiqiy amalga oshirish — <c>AppDbContext</c>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">Ishning natijasi.</typeparam>
    /// <param name="work">Tranzaksiya ichida bajariladigan ish.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            "Bu IUnitOfWork tranzaksiyani qo'llab-quvvatlamaydi — haqiqiy amalga oshirish AppDbContext da.");
}
