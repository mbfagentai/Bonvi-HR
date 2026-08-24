using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary>
/// Barcha ma'lumotnomalar uchun yagona repozitoriy.
/// </summary>
/// <remarks>
/// <para>
/// Nega generik: sakkizta ma'lumotnoma bir xil ishlaydi — ro'yxat, nom
/// bo'yicha qidiruv, kod bo'yicha qidiruv. Har biriga alohida sinf yozish
/// bir xil kodni sakkiz marta takrorlash bo'lardi.
/// </para>
/// <para>
/// DI'da ochiq generik sifatida ro'yxatga olinadi:
/// <c>AddScoped(typeof(IReferenceRepository&lt;&gt;), typeof(ReferenceRepository&lt;&gt;))</c>.
/// Daraxt yoki vazifalar kabi qo'shimchasi borlari (bo'lim, lavozim) bundan
/// meros oladi.
/// </para>
/// </remarks>
public class ReferenceRepository<TEntity> : Repository<TEntity>, IReferenceRepository<TEntity>
    where TEntity : ReferenceEntity
{
    public ReferenceRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// <c>AsNoTracking</c>: ro'yxat faqat ko'rsatish uchun o'qiladi.
    /// Kuzatuv yoqilsa, har bir yozuv uchun nusxa saqlanib, ma'nosiz xotira
    /// va taqqoslash yuki paydo bo'lardi.
    /// </remarks>
    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(
        bool onlyActive = false,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking();

        if (onlyActive)
        {
            query = query.Where(entity => entity.IsActive);
        }

        // Tartib `(is_active, sort_order)` indeksiga mos — saralash bepul
        return await query
            .OrderBy(entity => entity.SortOrder)
            .ThenBy(entity => entity.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Taqqoslash <c>lower(name)</c> bo'yicha: bazada aynan shu ifoda ustida
    /// unikal indeks bor, shu sababli so'rov indeksga tushadi.
    /// Yozuv kuzatiladi (<c>AsNoTracking</c> yo'q) — bu metod odatda
    /// tahrirlashdan oldin chaqiriladi.
    /// </remarks>
    public virtual Task<TEntity?> FindByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(NameEquals(NormalizeName(name)), cancellationToken);

    /// <inheritdoc />
    public virtual Task<bool> NameExistsAsync(
        string name,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set.Where(NameEquals(NormalizeName(name)));

        // Tahrirlashda yozuvning O'ZI takror hisoblanmasligi kerak
        if (exceptId is { } id)
        {
            query = query.Where(entity => entity.Id != id);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual Task<TEntity?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        // Kod — mashina uchun kalit (slug), registri qat'iy.
        // Aynan shu sababli u bo'yicha oddiy unikal indeks yetarli.
        var normalized = code.Trim();

        return Set.FirstOrDefaultAsync(entity => entity.Code == normalized, cancellationToken);
    }

    /// <summary>
    /// «Nomi shunga teng» sharti — registrga sezgir EMAS.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ToLower()</c> bu yerda .NET'da HECH QACHON bajarilmaydi: ifoda daraxti
    /// EF tomonidan PostgreSQL'ning <c>lower(name)</c> chaqiruviga aylantiriladi.
    /// Bazada aynan <c>lower(name)</c> ustida unikal indeks yaratilgan, shu
    /// sababli shart indeksga to'liq tushadi.
    /// </para>
    /// <para>
    /// Analizator qoidalari (CA1304/CA1311/CA1862) shu sababli o'chirilgan:
    /// ular jarayon ichidagi satr amallariga mo'ljallangan. Ular taklif qilgan
    /// <c>StringComparison</c> variantini esa EF Core umuman SQL'ga tarjima
    /// qila olmaydi — so'rov mijoz tomonida bajarilib, butun jadval yuklanardi.
    /// </para>
    /// </remarks>
    [SuppressMessage(
        "Globalization",
        "CA1304:Specify CultureInfo",
        Justification = "Ifoda daraxti SQL'ga tarjima qilinadi, .NET'da bajarilmaydi.")]
    [SuppressMessage(
        "Globalization",
        "CA1311:Specify a culture or use an invariant version",
        Justification = "Ifoda daraxti SQL'ga tarjima qilinadi, .NET'da bajarilmaydi.")]
    [SuppressMessage(
        "Performance",
        "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons",
        Justification = "EF Core StringComparison variantini SQL'ga tarjima qila olmaydi.")]
    private static Expression<Func<TEntity, bool>> NameEquals(string normalizedName)
        => entity => entity.Name.ToLower() == normalizedName;

    /// <summary>
    /// Qidiruv uchun nomni normallashtiradi: chetdagi va ichidagi ortiqcha
    /// bo'shliqlar olib tashlanadi, so'ng kichik registrga o'tkaziladi.
    /// </summary>
    /// <remarks>
    /// <see cref="ReferenceEntity"/> saqlashdan oldin nomni xuddi shunday
    /// tozalaydi. Agar bu yerda tozalanmasa, «Маркетинг  бўлими» (ikki
    /// bo'shliq bilan) bazadagi yozuvni topa olmasdi.
    /// </remarks>
    protected static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join(' ', parts).ToLowerInvariant();
    }
}
