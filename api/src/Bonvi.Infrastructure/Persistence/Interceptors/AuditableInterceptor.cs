using Bonvi.Application.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bonvi.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Saqlashdan oldin audit maydonlarini avtomatik to'ldiradi va
/// <c>Delete</c> ni "yumshoq o'chirish"ga aylantiradi.
/// </summary>
/// <remarks>
/// <para>
/// Nega interceptor: agar buni har bir use-case'da qo'lda yozsak, bitta joyda
/// unutiladi va o'sha yozuv "kim o'zgartirdi" ma'lumotisiz qoladi.
/// Bu yerda esa chetlab o'tish imkoni yo'q.
/// </para>
/// <para>
/// HR tizimida bu majburiy: har bir buyruq, har bir lavozim o'zgarishi
/// javobgar shaxsga bog'langan bo'lishi kerak.
/// </para>
/// </remarks>
public sealed class AuditableInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public AuditableInterceptor(ICurrentUser currentUser, IClock clock)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            ApplyAudit(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            ApplyAudit(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void ApplyAudit(DbContext context)
    {
        var now = _clock.UtcNow;
        var userId = _currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Yumshoq o'chirish: DELETE bajarilmaydi, faqat belgi qo'yiladi
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable softDeletable)
            {
                entry.State = EntityState.Modified;
                softDeletable.DeletedAt = now;
                softDeletable.DeletedBy = userId;
                continue;
            }

            if (entry.Entity is not IAuditable auditable)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.CreatedAt = now;
                    auditable.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy = userId;
                    break;

                default:
                    break;
            }
        }
    }
}
