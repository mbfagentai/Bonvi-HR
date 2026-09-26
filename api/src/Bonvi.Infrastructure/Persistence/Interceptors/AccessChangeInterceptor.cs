using Bonvi.Domain.Identity;
using Bonvi.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bonvi.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Rol yoki hisobning kirish huquqiga tegadigan saqlashdan keyin
/// <see cref="AccessVersion"/> ni oshiradi — keshlangan huquqlar darhol eskiradi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega interceptor, use-case'larda qo'lda emas</b> —
/// <see cref="DataChangeInterceptor"/> bilan AYNAN bir xil sabab. Huquqqa
/// tegadigan yo'llar ko'p: rol yaratish/tahrirlash/o'chirish, hisob
/// yaratish, rolini almashtirish, faolsizlantirish, faollashtirish,
/// seeder. Qo'lda chaqiruv bittasida unutilsa, o'sha yo'l bilan olingan
/// huquq 30 soniya «eski» bo'lib yurardi — aynan faolsizlantirilgan hisob
/// ishlashda davom etadigan xato. Bu yerda chetlab o'tish yo'li yo'q.
/// </para>
/// <para>
/// <b>Filtr BOR</b> (<see cref="DataChangeInterceptor"/> dan farqli):
/// kirishda har safar hisobning oxirgi kirish vaqti yoziladi va har bir
/// shunday saqlash butun keshni bekorga tozalardi. Shu sababli faqat
/// huquqni o'zgartiradigan maydonlar sanaladi — rol (istalgan o'zgarish),
/// hisobning roli, faolligi va o'chirilganligi.
/// </para>
/// <para>
/// <b>Nega saqlangandan KEYIN.</b> Oldin oshirilsa, parallel so'rov hali
/// saqlanmagan eski qatorni o'qib, uni yangi versiya kaliti ostiga yozib
/// qo'yardi. Muvaffaqiyatsiz saqlashda esa belgi shunchaki tashlanadi.
/// </para>
/// </remarks>
public sealed class AccessChangeInterceptor : SaveChangesInterceptor
{
    private readonly AccessVersion _version;

    /// <summary>Joriy saqlash huquqqa tegadimi — «saqlanyapti» va «saqlandi» orasida.</summary>
    private bool _pending;

    public AccessChangeInterceptor(AccessVersion version) => _version = version;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        _pending = eventData.Context is not null && TouchesAccess(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        _pending = eventData.Context is not null && TouchesAccess(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Complete();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Complete();
        return base.SavedChanges(eventData, result);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pending = false;
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pending = false;
        base.SaveChangesFailed(eventData);
    }

    private void Complete()
    {
        if (!_pending)
        {
            return;
        }

        _pending = false;
        _version.Bump();
    }

    private static bool TouchesAccess(DbContext context)
        => context.ChangeTracker.Entries().Any(entry => entry.Entity switch
        {
            Role => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted,
            User => entry.State is EntityState.Added or EntityState.Deleted
                    || (entry.State == EntityState.Modified && UserAccessModified(entry)),
            _ => false,
        });

    /// <summary>
    /// Hisobning huquqqa tegadigan maydonlari. Yumshoq o'chirish
    /// (<c>AuditableInterceptor</c>) <c>Deleted</c> ni <c>Modified</c> +
    /// <c>DeletedAt</c> ga aylantiradi — shu sababli u ham shu yerda.
    /// </summary>
    private static bool UserAccessModified(EntityEntry entry)
        => entry.Property(nameof(User.RoleIds)).IsModified
           || entry.Property(nameof(User.IsActive)).IsModified
           || entry.Property(nameof(User.DeletedAt)).IsModified;
}
