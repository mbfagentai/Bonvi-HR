using System.Text.Json;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Jurnaldagi havola maydonlarining NOMLARI — har bir jadvalga bitta so'rov
/// (<c>WHERE id = ANY(@ids)</c>), o'chirilgan va faolsizlantirilganlari bilan birga.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <c>IgnoreQueryFilters</c>.</b> Tarix aynan o'chirilgan narsalar haqida ham:
/// «ishdan bo'shatildi — sababi X», X esa keyin ro'yxatdan olib tashlangan. Filtr bilan
/// u nomsiz identifikatorga aylanardi (SPEC §8.3).
/// </para>
/// <para>
/// Topilmagan identifikator (qattiq o'chirilgan rol) — nomsiz qoladi; qiymatning o'zi
/// (identifikator) baribir javobda bor.
/// </para>
/// </remarks>
internal sealed class AuditLabelLoader
{
    private readonly AppDbContext _context;
    private readonly Dictionary<AuditRefTarget, HashSet<Guid>> _requested = [];
    private readonly Dictionary<(AuditRefTarget Target, Guid Id), string> _labels = [];

    public AuditLabelLoader(AppDbContext context) => _context = context;

    /// <summary>Qiymatdagi identifikator(lar)ni navbatga qo'yadi — satr yoki satrlar massivi.</summary>
    public void Request(AuditRefTarget target, JsonElement? value)
    {
        foreach (var id in Ids(value))
        {
            Request(target, id);
        }
    }

    /// <summary>Bitta identifikatorni navbatga qo'yadi.</summary>
    public void Request(AuditRefTarget target, Guid id)
    {
        if (!_requested.TryGetValue(target, out var ids))
        {
            ids = [];
            _requested[target] = ids;
        }

        ids.Add(id);
    }

    /// <summary>Navbatdagi hamma nomni o'qiydi — jadval bo'yicha bittadan so'rov.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        foreach (var (target, ids) in _requested)
        {
            ids.RemoveWhere(id => _labels.ContainsKey((target, id)));
            if (ids.Count == 0)
            {
                continue;
            }

            var rows = await QueryAsync(target, [.. ids], cancellationToken).ConfigureAwait(false);

            foreach (var row in rows)
            {
                _labels[(target, row.Id)] = row.Text;
            }
        }

        _requested.Clear();
    }

    /// <summary>Bitta identifikatorning nomi.</summary>
    public string? NameOf(AuditRefTarget target, Guid id)
        => _labels.GetValueOrDefault((target, id));

    /// <summary>
    /// Qiymatning yorlig'i: bitta havola — nomi; ro'yxat — topilgan nomlar vergul bilan,
    /// qiymat tartibida.
    /// </summary>
    public string? LabelOf(AuditRefTarget target, JsonElement? value)
    {
        var names = Ids(value)
            .Select(id => NameOf(target, id))
            .OfType<string>()
            .ToList();

        return names.Count == 0 ? null : string.Join(", ", names);
    }

    private static IEnumerable<Guid> Ids(JsonElement? value)
    {
        switch (value?.ValueKind)
        {
            case JsonValueKind.String when Guid.TryParse(value.Value.GetString(), out var id):
                yield return id;
                break;

            case JsonValueKind.Array:
                foreach (var item in value.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var itemId))
                    {
                        yield return itemId;
                    }
                }

                break;
        }
    }

    private async Task<List<LabelRow>> QueryAsync(AuditRefTarget target, List<Guid> ids, CancellationToken cancellationToken)
    {
        switch (target)
        {
            case AuditRefTarget.Employee:
                return People(await Set<Employee>()
                    .Where(row => ids.Contains(row.Id))
                    .Select(row => new NameRow(row.Id, row.FullName.LastName, row.FullName.FirstName, row.FullName.MiddleName))
                    .ToListAsync(cancellationToken).ConfigureAwait(false));

            case AuditRefTarget.User:
                return People(await Set<User>()
                    .Where(row => ids.Contains(row.Id))
                    .Select(row => new NameRow(row.Id, row.FullName.LastName, row.FullName.FirstName, row.FullName.MiddleName))
                    .ToListAsync(cancellationToken).ConfigureAwait(false));

            case AuditRefTarget.Candidate:
                return People(await Set<Candidate>()
                    .Where(row => ids.Contains(row.Id))
                    .Select(row => new NameRow(row.Id, row.FullName.LastName, row.FullName.FirstName, row.FullName.MiddleName))
                    .ToListAsync(cancellationToken).ConfigureAwait(false));

            case AuditRefTarget.Role:
                return await Set<Role>()
                    .Where(row => ids.Contains(row.Id))
                    .Select(row => new LabelRow(row.Id, row.Name))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

            case AuditRefTarget.Vacancy:
                return await Set<Vacancy>()
                    .Where(row => ids.Contains(row.Id))
                    .Select(row => new LabelRow(row.Id, row.Title))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

            case AuditRefTarget.WorkSchedule:
                return await Set<WorkSchedule>()
                    .Where(row => ids.Contains(row.Id))
                    .Select(row => new LabelRow(row.Id, row.Name))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

            case AuditRefTarget.Department:
                return await NamesAsync<Department>(ids, cancellationToken).ConfigureAwait(false);

            case AuditRefTarget.Position:
                return await NamesAsync<Position>(ids, cancellationToken).ConfigureAwait(false);

            case AuditRefTarget.LegalEntity:
                return await NamesAsync<LegalEntity>(ids, cancellationToken).ConfigureAwait(false);

            case AuditRefTarget.Direction:
                return await NamesAsync<Direction>(ids, cancellationToken).ConfigureAwait(false);

            case AuditRefTarget.TerminationReason:
                return await NamesAsync<TerminationReason>(ids, cancellationToken).ConfigureAwait(false);

            case AuditRefTarget.DocumentType:
                return await NamesAsync<DocumentType>(ids, cancellationToken).ConfigureAwait(false);

            case AuditRefTarget.PipelineStage:
                return await NamesAsync<PipelineStage>(ids, cancellationToken).ConfigureAwait(false);

            case AuditRefTarget.AbsenceType:
                return await NamesAsync<AbsenceType>(ids, cancellationToken).ConfigureAwait(false);

            // `Incentive.TypeId` — rag'bat YOKI jarima turi; identifikatorlar takrorlanmaydi
            // (Guid v7), ya'ni ikkala jadvaldan qidirish bir ma'noli.
            case AuditRefTarget.IncentiveType:
                return
                [
                    .. await NamesAsync<AwardType>(ids, cancellationToken).ConfigureAwait(false),
                    .. await NamesAsync<PenaltyType>(ids, cancellationToken).ConfigureAwait(false),
                ];

            case AuditRefTarget.Employment:
                return People(await (
                        from employment in Set<Employment>()
                        join employee in Set<Employee>() on employment.EmployeeId equals employee.Id
                        where ids.Contains(employment.Id)
                        select new NameRow(employment.Id, employee.FullName.LastName, employee.FullName.FirstName, employee.FullName.MiddleName))
                    .ToListAsync(cancellationToken).ConfigureAwait(false));

            case AuditRefTarget.EmployeeCompetency:
                return People(await (
                        from competency in Set<EmployeeCompetency>()
                        join employee in Set<Employee>() on competency.EmployeeId equals employee.Id
                        where ids.Contains(competency.Id)
                        select new NameRow(competency.Id, employee.FullName.LastName, employee.FullName.FirstName, employee.FullName.MiddleName))
                    .ToListAsync(cancellationToken).ConfigureAwait(false));

            case AuditRefTarget.EmployeeAccount:
                return People(await (
                        from account in Set<EmployeeAccount>()
                        join employee in Set<Employee>() on account.EmployeeId equals employee.Id
                        where ids.Contains(account.Id)
                        select new NameRow(account.Id, employee.FullName.LastName, employee.FullName.FirstName, employee.FullName.MiddleName))
                    .ToListAsync(cancellationToken).ConfigureAwait(false));

            default:
                return [];
        }
    }

    private Task<List<LabelRow>> NamesAsync<T>(List<Guid> ids, CancellationToken cancellationToken)
        where T : ReferenceEntity
        => Set<T>()
            .Where(row => ids.Contains(row.Id))
            .Select(row => new LabelRow(row.Id, row.Name))
            .ToListAsync(cancellationToken);

    private IQueryable<T> Set<T>()
        where T : class
        => _context.Set<T>().IgnoreQueryFilters().AsNoTracking();

    private static List<LabelRow> People(List<NameRow> rows)
        => [.. rows.Select(row => new LabelRow(row.Id, FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full))];

    private sealed record LabelRow(Guid Id, string Text);

    private sealed record NameRow(Guid Id, string LastName, string FirstName, string? MiddleName);
}
