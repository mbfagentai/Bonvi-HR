using Bonvi.Application.Processes.Engine;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Processes.Engine;

/// <summary>
/// <see cref="IProcessEngineLock"/> over a PostgreSQL advisory lock (docs/SPEC-mening-ishlarim.md §7.4).
/// </summary>
/// <remarks>
/// Runs on the connection of the scoped <see cref="AppDbContext"/>, so the pass that takes the lock holds it
/// until its own transaction ends and no release can be forgotten. Outside a transaction the lock would be
/// gone as soon as the statement finished and two passes could overlap with nobody noticing, hence the throw.
/// </remarks>
public sealed class ProcessEngineLock : IProcessEngineLock
{
    /// <summary>The one engine-wide key: the ASCII of «BonviPrc». Nothing else in the system takes advisory locks.</summary>
    private const long LockKey = 0x426F6E7669507263;

    private readonly AppDbContext _context;

    public ProcessEngineLock(AppDbContext context) => _context = context;

    public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The process engine lock is transaction-scoped: take it inside ExecuteInTransactionAsync.");
        }

        var acquired = await _context.Database
            .SqlQuery<bool>($"""SELECT pg_try_advisory_xact_lock({LockKey}) AS "Value" """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return acquired.Single();
    }
}
