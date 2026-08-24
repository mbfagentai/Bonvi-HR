using Bonvi.Application.System.Queries.GetSystemInfo;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence;

/// <summary>
/// <see cref="IDatabaseProbe"/> ning EF Core orqali amalga oshirilishi.
/// </summary>
/// <remarks>
/// Application qatlami faqat interfeysni ko'radi. Agar ertaga EF Core
/// o'rniga Dapper ishlatilsa — faqat shu fayl almashadi.
/// </remarks>
public sealed class EfDatabaseProbe : IDatabaseProbe
{
    private readonly AppDbContext _context;

    public EfDatabaseProbe(AppDbContext context) => _context = context;

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        => _context.Database.CanConnectAsync(cancellationToken);
}
