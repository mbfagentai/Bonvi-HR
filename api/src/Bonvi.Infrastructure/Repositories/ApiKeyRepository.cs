using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.PublicApi;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IApiKeyRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class ApiKeyRepository : Repository<ApiKey>, IApiKeyRepository
{
    public ApiKeyRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// <c>AsNoTracking</c> ataylab: bu so'rov autentifikatsiya yo'lida, har
    /// bir ommaviy so'rovda bajariladi va natija HECH QACHON
    /// o'zgartirilmaydi. «Oxirgi ishlatilgan» belgisi butunlay boshqa
    /// doirada yoziladi (<c>IApiKeyUsageTracker</c>), shu sababli kuzatuv
    /// bu yerda faqat ortiqcha ish bo'lardi.
    /// </remarks>
    public Task<ApiKey?> FindByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        return Set
            .AsNoTracking()
            .FirstOrDefaultAsync(key => key.KeyPrefix == prefix, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Taqqoslash KICHIK HARFDA: bazadagi qisman unikal indeks ham
    /// <c>lower(name)</c> ustida (sababi <c>ApiKeyConfiguration</c> da).
    /// Ikkisi bir xil qoidada bo'lmasa, tekshiruvdan o'tib ketgan nom
    /// bazada 23505 bilan yiqilardi va mijoz 409 o'rniga 500 olardi.
    /// </remarks>
    public Task<bool> ActiveNameExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Set
            .AsNoTracking()
            .Where(key => key.RevokedAt == null)
            .AnyAsync(NameEquals(name.Trim().ToLowerInvariant()), cancellationToken);
    }

    /// <summary>
    /// «Nomi shunga teng» sharti — registrga sezgir EMAS.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ToLower()</c> bu yerda .NET'da HECH QACHON bajarilmaydi: ifoda
    /// daraxti EF tomonidan PostgreSQL'ning <c>lower(name)</c> chaqiruviga
    /// aylantiriladi va migratsiyadagi qisman unikal indeks aynan shu ifoda
    /// ustida.
    /// </para>
    /// <para>
    /// Analizator qoidalari o'chirilgan — <c>ReferenceRepository.NameEquals</c>
    /// dagi bilan AYNAN bir xil sabab: ular jarayon ichidagi satr amallariga
    /// mo'ljallangan, taklif qilgan <c>StringComparison</c> variantini esa EF
    /// Core SQL'ga tarjima qila olmaydi va so'rov mijoz tomonida bajarilib
    /// ketardi.
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
    private static Expression<Func<ApiKey, bool>> NameEquals(string normalizedName)
        => key => key.Name.ToLower() == normalizedName;

    /// <inheritdoc />
    /// <remarks>
    /// Tartib: avval KUCHDAGILAR, keyin bekor qilinganlar; ichida
    /// yangisidan eskisiga. Sabab ekranda: kundalik ish kuchdagi
    /// kalitlar bilan, bekor qilinganlar esa arxiv va ular pastda
    /// turishi kerak.
    /// </remarks>
    public async Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .OrderBy(key => key.RevokedAt == null ? 0 : 1)
            .ThenByDescending(key => key.CreatedAt)
            .ThenByDescending(key => key.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
