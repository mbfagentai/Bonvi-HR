using Bonvi.Application.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Infrastructure.Persistence;

namespace Bonvi.Infrastructure.Services;

/// <summary>
/// <see cref="IAuditLogger"/> ning EF Core orqali amalga oshirilishi.
/// </summary>
/// <remarks>
/// Repozitoriy emas, to'g'ridan-to'g'ri <see cref="AppDbContext"/> bilan
/// ishlaydi — xuddi <c>IdentitySeeder</c>/<c>CatalogSeeder</c> kabi.
/// <see cref="AuditLog"/> uchun alohida repozitoriy interfeysi ataylab yo'q:
/// unga hech qachon o'qish so'rovi bo'lmaydi (faqat qo'shiladi), ya'ni
/// <c>IRepository&lt;T&gt;</c> ning o'qish metodlari bu yerda keraksiz bo'lardi.
/// </remarks>
public sealed class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public AuditLogger(AppDbContext context, ICurrentUser currentUser, IClock clock)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
    }

    public void Record(
        AuditAction action,
        string entityName,
        string? entityId,
        string? oldValues = null,
        string? newValues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        var log = AuditLog.Record(
            _clock.UtcNow,
            action,
            entityName,
            entityId,
            _currentUser.UserId,
            _currentUser.UserName,
            oldValues,
            newValues);

        _context.AuditLogs.Add(log);
    }
}
