using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bonvi.Infrastructure.Persistence.Auditing;

/// <summary>
/// Bitta saqlashning xodim va yorliq qidiruvlari — avval kuzatuvdagi obyektlardan, keyin har
/// jadvalga BITTA so'rov bilan.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega avval kuzatuvdan.</b> Ko'pincha kerakli yozuv shu saqlashning o'zida
/// bor (ishga qabulda xodim, mehnat munosabati va buyruq birga yaratiladi) va
/// bazada hali yo'q. Ustiga-ustak yorliq «o'sha lahzadagi» nom bo'lishi kerak:
/// shu saqlashda xodim qayta nomlangan bo'lsa — yangi nomi.
/// </para>
/// <para>
/// <b>Nega to'plab.</b> ETL bitta saqlashda minglab qator yozadi; har biriga
/// alohida so'rov N+1 bo'lardi. Bu yerda jadval bo'yicha bitta
/// <c>WHERE id = ANY(@ids)</c>. O'chirilgan (yumshoq) yozuvlar ham topiladi —
/// ishdan ketgan xodimning nomi tarixda turaverishi kerak.
/// </para>
/// </remarks>
internal sealed class AuditLookups
{
    private readonly DbContext _context;
    private readonly Dictionary<Type, Dictionary<Guid, object>> _tracked;
    private readonly Dictionary<AuditLookup, HashSet<Guid>> _pending = [];
    private readonly Dictionary<(AuditLookup Lookup, Guid Key), object?> _resolved = [];

    public AuditLookups(DbContext context, IEnumerable<EntityEntry> trackedEntries)
    {
        _context = context;
        _tracked = Index(trackedEntries);
    }

    /// <summary>Qidiruvni navbatga qo'yadi.</summary>
    /// <param name="lookup">Qaysi jadval.</param>
    /// <param name="key">Kalit.</param>
    public void Request(AuditLookup lookup, Guid key)
    {
        if (_resolved.ContainsKey((lookup, key)))
        {
            return;
        }

        if (!_pending.TryGetValue(lookup, out var keys))
        {
            keys = [];
            _pending[lookup] = keys;
        }

        keys.Add(key);
    }

    /// <summary>Navbatdagi hamma qidiruvni bajaradi.</summary>
    /// <param name="async">Asinxron so'rovmi (sinxron <c>SaveChanges</c> da — yo'q).</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task ResolveAsync(bool async, CancellationToken cancellationToken)
    {
        foreach (var (lookup, keys) in _pending)
        {
            var missing = new List<Guid>();

            foreach (var key in keys)
            {
                if (FromTracked(lookup, key) is { } found)
                {
                    _resolved[(lookup, key)] = found;
                }
                else
                {
                    missing.Add(key);
                }
            }

            if (missing.Count == 0)
            {
                continue;
            }

            var loaded = await QueryAsync(lookup, missing, async, cancellationToken).ConfigureAwait(false);

            foreach (var key in missing)
            {
                _resolved[(lookup, key)] = loaded.GetValueOrDefault(key);
            }
        }

        _pending.Clear();
    }

    /// <summary>
    /// Topilgan xodim (<see cref="AuditLookup.EmploymentEmployee"/>, <see cref="AuditLookup.CompetencyEmployee"/>,
    /// <see cref="AuditLookup.ScheduleChangeRequestEmployee"/>).
    /// </summary>
    public Guid? EmployeeOf(AuditLookup lookup, Guid key)
        => _resolved.GetValueOrDefault((lookup, key)) as Guid?;

    /// <summary>Topilgan nom.</summary>
    public string? LabelOf(AuditLookup lookup, Guid key)
        => _resolved.GetValueOrDefault((lookup, key)) as string;

    private static readonly Type[] IndexedTypes =
    [
        typeof(Employee), typeof(Employment), typeof(EmployeeCompetency),
        typeof(Candidate), typeof(Position), typeof(WorkSchedule), typeof(ScheduleChangeRequest),
    ];

    private static Dictionary<Type, Dictionary<Guid, object>> Index(IEnumerable<EntityEntry> entries)
    {
        var index = IndexedTypes.ToDictionary(type => type, _ => new Dictionary<Guid, object>());

        foreach (var entry in entries)
        {
            if (entry.Entity is Entity entity && index.TryGetValue(entry.Metadata.ClrType, out var byId))
            {
                byId[entity.Id] = entity;
            }
        }

        return index;
    }

    private object? FromTracked(AuditLookup lookup, Guid key)
    {
        object? Tracked<T>() => _tracked[typeof(T)].GetValueOrDefault(key);

        return lookup switch
        {
            AuditLookup.EmployeeName => (Tracked<Employee>() as Employee)?.FullName.Full,
            AuditLookup.CandidateName => (Tracked<Candidate>() as Candidate)?.FullName.Full,
            AuditLookup.PositionName => (Tracked<Position>() as Position)?.Name,
            AuditLookup.WorkScheduleName => (Tracked<WorkSchedule>() as WorkSchedule)?.Name,
            AuditLookup.EmploymentEmployee => (Tracked<Employment>() as Employment)?.EmployeeId,
            AuditLookup.CompetencyEmployee => (Tracked<EmployeeCompetency>() as EmployeeCompetency)?.EmployeeId,
            AuditLookup.ScheduleChangeRequestEmployee => (Tracked<ScheduleChangeRequest>() as ScheduleChangeRequest)?.EmployeeId,
            _ => null,
        };
    }

    private async Task<Dictionary<Guid, object>> QueryAsync(
        AuditLookup lookup,
        List<Guid> keys,
        bool async,
        CancellationToken cancellationToken)
    {
        switch (lookup)
        {
            case AuditLookup.EmployeeName:
            {
                var rows = await ListAsync(
                    Set<Employee>()
                        .Where(row => keys.Contains(row.Id))
                        .Select(row => new NameRow(row.Id, row.FullName.LastName, row.FullName.FirstName, row.FullName.MiddleName)),
                    async,
                    cancellationToken).ConfigureAwait(false);

                return rows.ToDictionary(row => row.Id, row => (object)row.Full);
            }

            case AuditLookup.CandidateName:
            {
                var rows = await ListAsync(
                    Set<Candidate>()
                        .Where(row => keys.Contains(row.Id))
                        .Select(row => new NameRow(row.Id, row.FullName.LastName, row.FullName.FirstName, row.FullName.MiddleName)),
                    async,
                    cancellationToken).ConfigureAwait(false);

                return rows.ToDictionary(row => row.Id, row => (object)row.Full);
            }

            case AuditLookup.PositionName:
            {
                var rows = await ListAsync(
                    Set<Position>().Where(row => keys.Contains(row.Id)).Select(row => new TextRow(row.Id, row.Name)),
                    async,
                    cancellationToken).ConfigureAwait(false);

                return rows.ToDictionary(row => row.Id, row => (object)row.Text);
            }

            case AuditLookup.WorkScheduleName:
            {
                var rows = await ListAsync(
                    Set<WorkSchedule>().Where(row => keys.Contains(row.Id)).Select(row => new TextRow(row.Id, row.Name)),
                    async,
                    cancellationToken).ConfigureAwait(false);

                return rows.ToDictionary(row => row.Id, row => (object)row.Text);
            }

            case AuditLookup.EmploymentEmployee:
            {
                var rows = await ListAsync(
                    Set<Employment>().Where(row => keys.Contains(row.Id)).Select(row => new LinkRow(row.Id, row.EmployeeId)),
                    async,
                    cancellationToken).ConfigureAwait(false);

                return rows.ToDictionary(row => row.Id, row => (object)row.EmployeeId);
            }

            case AuditLookup.CompetencyEmployee:
            {
                var rows = await ListAsync(
                    Set<EmployeeCompetency>().Where(row => keys.Contains(row.Id)).Select(row => new LinkRow(row.Id, row.EmployeeId)),
                    async,
                    cancellationToken).ConfigureAwait(false);

                return rows.ToDictionary(row => row.Id, row => (object)row.EmployeeId);
            }

            case AuditLookup.ScheduleChangeRequestEmployee:
            {
                var rows = await ListAsync(
                    Set<ScheduleChangeRequest>().Where(row => keys.Contains(row.Id)).Select(row => new LinkRow(row.Id, row.EmployeeId)),
                    async,
                    cancellationToken).ConfigureAwait(false);

                return rows.ToDictionary(row => row.Id, row => (object)row.EmployeeId);
            }

            default:
                return [];
        }
    }

    private IQueryable<T> Set<T>()
        where T : class
        => _context.Set<T>().IgnoreQueryFilters().AsNoTracking();

    /// <summary>Sinxron saqlash yo'li sinxron so'rov bilan ketadi — asinxronni bloklab kutmaydi.</summary>
    internal static async Task<List<T>> ListAsync<T>(IQueryable<T> query, bool async, CancellationToken cancellationToken)
        => async
            ? await query.ToListAsync(cancellationToken).ConfigureAwait(false)
            : [.. query];

    private sealed record NameRow(Guid Id, string LastName, string FirstName, string? MiddleName)
    {
        public string Full => FullName.FromTrusted(LastName, FirstName, MiddleName).Full;
    }

    private sealed record TextRow(Guid Id, string Text);

    private sealed record LinkRow(Guid Id, Guid EmployeeId);
}
