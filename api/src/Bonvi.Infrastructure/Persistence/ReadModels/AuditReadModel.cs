using Bonvi.Application.Audit;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// <see cref="IAuditReadModel"/> — jurnalni o'qish: ko'rinish SQL'da, yashirish va nomlar
/// sahifa qatorlari ustida (SPEC-rollar-va-audit §7.10, §8.3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Qator kimga ko'rinadi</b> — <see cref="Visible"/>: entity o'zgarishi faqat
/// so'rovchiga ochiq bo'limlardagi entity'lar orasidan (bo'lim — <see cref="AuditCatalog"/>
/// dagi qoidadan), Excel eksporti — hisobotlar bo'limi ochiq bo'lsa, kirish va xavfsizlik
/// hodisalari — <c>users.manage</c> bilan. Shart bitta <c>WHERE</c> da: sanoq ham, sahifa
/// ham bir xil to'plamdan.
/// </para>
/// <para>
/// <b>Har bir so'rov chegaralangan</b>: ro'yxat — davr bilan (<c>ix_audit_logs_occurred_at</c>),
/// xodim tarixi — xodim bilan (<c>ix_audit_logs_subject_employee_id_occurred_at</c>), «shu
/// amal ichida» — guruh bilan (<c>ix_audit_logs_correlation_id</c>). Yangi indeks kerak
/// bo'lmadi.
/// </para>
/// </remarks>
public sealed class AuditReadModel : IAuditReadModel
{
    /// <summary>«Shu amal ichida» qatorlarining eng ko'p soni.</summary>
    /// <remarks>
    /// ETL qadami (<c>etl:employees</c>) bitta guruhda minglab qator yozadi; tafsilot oynasi
    /// ularning hammasini tortib kelmasin. Oddiy so'rov 2–10 qator yozadi.
    /// </remarks>
    public const int MaxRelated = 50;

    private readonly AppDbContext _context;

    public AuditReadModel(AppDbContext context) => _context = context;

    public string? FindEntityName(string entityName)
        => AuditCatalog.FindByName(entityName)?.EntityType.Name;

    public async Task<AuditPage> ListAsync(AuditListFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var logs = Visible(filter.Viewer, filter.Changes, filter.Security, filter.Module);

        if (filter.From is { } from)
        {
            logs = logs.Where(log => log.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            logs = logs.Where(log => log.OccurredAt < to);
        }

        if (filter.ActorSystem)
        {
            logs = logs.Where(log => log.ActorKind == AuditActorKind.System);
        }

        if (filter.ActorUserId is { } userId)
        {
            logs = logs.Where(log => log.UserId == userId);
        }

        if (filter.Action is { } action)
        {
            logs = logs.Where(log => log.Action == action);
        }

        if (filter.EmployeeId is { } employeeId)
        {
            logs = logs.Where(log => log.SubjectEmployeeId == employeeId);
        }

        if (filter.Entity is { } entity)
        {
            logs = logs.Where(log => log.EntityName == entity);
        }

        if (filter.EntityId is { } entityId)
        {
            logs = logs.Where(log => log.EntityId == entityId);
        }

        var total = await logs.CountAsync(cancellationToken).ConfigureAwait(false);

        // `long` — juda katta sahifa raqami ko'paytmada toshib, manfiy «o'tkazib yuborish»
        // bo'lib qolmasin.
        var skip = (long)(filter.Page - 1) * filter.PageSize;
        if (skip >= total)
        {
            return new AuditPage([], total);
        }

        var rows = await logs
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.Id)
            .Skip((int)skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await new AuditEntryAssembler(_context, filter.Viewer)
            .BuildAsync(rows, cancellationToken)
            .ConfigureAwait(false);

        return new AuditPage(items, total);
    }

    public async Task<AuditEntryDetailsDto?> GetAsync(
        Guid id,
        AuditViewer viewer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var visible = Visible(viewer, changes: true, security: true, module: null);

        var row = await visible
            .FirstOrDefaultAsync(log => log.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        List<AuditLog> related = row.CorrelationId is { } correlationId
            ? await visible
                .Where(log => log.CorrelationId == correlationId && log.Id != id)
                .OrderBy(log => log.OccurredAt)
                .ThenBy(log => log.Id)
                .Take(MaxRelated)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : [];

        // Bitta yig'uvchi — havola nomlari qator va guruh uchun BITTA to'plamda o'qiladi.
        var built = await new AuditEntryAssembler(_context, viewer)
            .BuildAsync([row, .. related], cancellationToken)
            .ConfigureAwait(false);

        return new AuditEntryDetailsDto(built[0], row.IpAddress, row.UserAgent, [.. built.Skip(1)]);
    }

    public async Task<IReadOnlyList<AuditActorOptionDto>> ListActorsAsync(CancellationToken cancellationToken = default)
    {
        // Foydalanuvchilar jadvalidan, «jurnalda bormi» — `user_id` indeksi bo'yicha har
        // biriga bitta tekshiruv (hisoblar 3–4 ta). Jurnalning o'zidan DISTINCT esa butun
        // jadvalni o'qirdi. O'chirilgan va faolsiz hisoblar ham — ularning tarixi bor.
        var rows = await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => _context.AuditLogs.Any(log => log.UserId == user.Id))
            .Select(user => new
            {
                user.Id,
                user.FullName.LastName,
                user.FullName.FirstName,
                user.FullName.MiddleName,
                user.Login,
                user.IsActive,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows
                .Select(row => new AuditActorOptionDto(
                    row.Id,
                    FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                    row.Login.Value,
                    row.IsActive))
                .OrderBy(actor => actor.FullName, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>So'rovchiga ko'rinadigan qatorlar — ro'yxat, tafsilot va guruh uchun BITTA shart.</summary>
    /// <param name="viewer">Ko'rinish.</param>
    /// <param name="changes">O'zgarishlar va eksport kerakmi.</param>
    /// <param name="security">Kirish hodisalari kerakmi (ruxsat bo'lsagina).</param>
    /// <param name="module">Faqat shu bo'lim (<c>null</c> — hammasi).</param>
    private IQueryable<AuditLog> Visible(AuditViewer viewer, bool changes, bool security, string? module)
    {
        // Shaxsiy QATORLAR (hujjat, qobiliyat, tekshiruv materiali) — bo'lim ochiq bo'lsa ham
        // shaxsiy ma'lumot ruxsatisiz ro'yxatga tushmaydi.
        string[] entities = changes
            ?
            [
                .. AuditCatalog.Audited.Values
                    .Where(rule => viewer.Modules.Contains(rule.Module)
                        && (module is null || rule.Module == module)
                        && (viewer.PersonalData || !rule.RequiresPersonalView))
                    .Select(rule => rule.EntityType.Name),
            ]
            : [];

        var exports = changes
            && viewer.Modules.Contains(AccessModuleKeys.Reports)
            && (module is null || module == AccessModuleKeys.Reports);

        // Ilova parolini ko'rish ham `Export`, lekin davomat bo'liminiki — «kim kimning
        // parolini ko'rdi» ni hisobot egasi emas, davomat egasi ko'radi.
        var passwordViews = changes
            && viewer.Modules.Contains(AccessModuleKeys.Attendance)
            && (module is null || module == AccessModuleKeys.Attendance);
        const string passwordViewEntity = AuditActionGroups.PasswordViewEntity;

        var securityEvents = security
            && viewer.Security
            && (module is null || module == AccessModuleKeys.Users);

        AuditAction[] changeActions = [.. AuditActionGroups.Changes];
        AuditAction[] securityActions = [.. AuditActionGroups.Security];

        return _context.AuditLogs
            .AsNoTracking()
            .Where(log =>
                (changeActions.Contains(log.Action) && entities.Contains(log.EntityName))
                || (exports && log.Action == AuditAction.Export && log.EntityName != passwordViewEntity)
                || (passwordViews && log.Action == AuditAction.Export && log.EntityName == passwordViewEntity)
                || (securityEvents && securityActions.Contains(log.Action)));
    }
}
