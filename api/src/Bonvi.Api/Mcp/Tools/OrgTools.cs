using System.ComponentModel;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Bonvi.Api.Mcp.Tools;

/// <summary>Tashkiliy tuzilma va ma'lumotnomalar.</summary>
[McpServerToolType]
public sealed class OrgTools(McpToolContext t)
{
    [McpServerTool(Name = "org_structure", Title = "Tashkiliy tuzilma",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Tashkiliy tuzilma va ma'lumotnomalar. section=overview (sukut): yuridik shaxslar, bo'limlar daraxti (yuqori bo'lim, "
        + "daraja, to'liq yo'l, rahbar, ishlayotgan xodimlar soni — bo'limning o'zida va ichki bo'limlari bilan, lavozimlar soni), "
        + "yo'nalishlar, ma'lumotnomalar (ishdan ketish sabablari — ixtiyoriy/qayta olinadigan, mukofot va jarima turlari, hujjat turlari — "
        + "majburiymi, yo'qlik turlari). section=positions: lavozimlar (bo'lim, daraja, sinov kunlari, vazifalar tartib bilan, xodimlar soni), "
        + "sahifalangan, bo'lim yoki nom bo'yicha toraytiriladi. Boshqa vositalardagi nom filtrlari uchun aniq nomlarni shu yerdan oling. "
        + "Org structure: legal entities, department tree, directions, catalogs; positions with duties via section=positions.")]
    public async Task<string> OrgStructure(
        [Description("Bo'lim: overview (sukut) — tuzilma va ma'lumotnomalar; positions — lavozimlar ro'yxati vazifalari bilan.")]
        string? section = null,
        [Description("Faqat shu bo'lim (nomi yoki id) va uning ichki bo'limlari.")] string? department = null,
        [Description("Lavozim nomi bo'yicha qidiruv (faqat section=positions).")] string? query = null,
        [Description("true — faolsizlantirilgan yozuvlarni ham ko'rsatish (sukut: faqat faollar).")] bool? includeInactive = null,
        [Description("Sahifa raqami (section=positions).")] int? page = null,
        [Description("Sahifadagi qatorlar soni (section=positions; sukut 50, ko'pi bilan 200).")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.CatalogView, "Ma'lumotnomalar");

        var mode = string.IsNullOrWhiteSpace(section) ? "overview" : section.Trim().ToLowerInvariant();

        if (mode is not ("overview" or "positions"))
        {
            throw new McpException($"«section» qiymati noto'g'ri: «{section}». Mumkin: overview, positions.");
        }

        var withInactive = includeInactive ?? false;
        var db = t.Db;

        var departments = await db.Departments
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.ParentId,
                item.Level,
                item.LegalEntityId,
                item.HeadEmployeeId,
                item.IsActive,
                item.Note,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var departmentNames = departments.ToDictionary(item => item.Id, item => item.Name);
        var roots = EmployeeQueryKit.Resolve(departmentNames, department, "Bo'lim");

        HashSet<Guid>? branch = roots is null
            ? null
            : await EmployeeQueryKit.BranchAsync(t, roots, cancellationToken).ConfigureAwait(false);

        // Ishlayotganlar: (bo'lim, lavozim, xodim) — bir odamning ikki ishi
        // bitta bo'limda bo'lsa ham bir marta sanaladi.
        var staff = await db.Employments
            .Where(employment => employment.Status != EmploymentStatus.Terminated
                                 && db.Employees.Any(employee => employee.Id == employment.EmployeeId))
            .Select(employment => new { employment.EmployeeId, employment.LegalEntityId, employment.DepartmentId, employment.PositionId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // To'liq yo'l («Bosh ofis / Moliya / Buxgalteriya») — AI daraxtni
        // tiklamasdan ham bo'limni aniq nomlay olsin.
        var parentOf = departments.ToDictionary(item => item.Id, item => item.ParentId);

        string PathOf(Guid id)
        {
            var parts = new List<string>();
            var seen = new HashSet<Guid>();
            Guid? current = id;

            while (current is { } node && seen.Add(node))
            {
                parts.Add(departmentNames.GetValueOrDefault(node, "?"));
                current = parentOf.GetValueOrDefault(node);
            }

            parts.Reverse();
            return string.Join(" / ", parts);
        }

        return mode == "positions"
            ? await PositionsAsync(departmentNames, branch, query, withInactive, staff.Select(item => (item.EmployeeId, item.PositionId)), PathOf, page, pageSize, cancellationToken)
                .ConfigureAwait(false)
            : await OverviewAsync(cancellationToken).ConfigureAwait(false);

        async Task<string> OverviewAsync(CancellationToken token)
        {
            var legalEntities = await db.LegalEntities
                .Where(item => withInactive || item.IsActive)
                .OrderBy(item => item.Name)
                .Select(item => new { item.Id, item.Name, item.FullName, item.TaxId, item.IsActive })
                .ToListAsync(token)
                .ConfigureAwait(false);

            var positionCounts = await db.Positions
                .Where(item => item.DepartmentId != null && (withInactive || item.IsActive))
                .GroupBy(item => item.DepartmentId)
                .Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Key!.Value, item => item.Count, token)
                .ConfigureAwait(false);

            var heads = await t.EmployeeNamesAsync(departments.Select(item => item.HeadEmployeeId).OfType<Guid>(), token)
                .ConfigureAwait(false);

            var legalEntityNames = await t.LegalEntitiesAsync(token).ConfigureAwait(false);

            var children = departments
                .Where(item => item.ParentId != null)
                .ToLookup(item => item.ParentId!.Value);

            var direct = staff
                .Where(item => item.DepartmentId != null)
                .GroupBy(item => item.DepartmentId!.Value)
                .ToDictionary(group => group.Key, group => group.Select(item => item.EmployeeId).ToHashSet());

            HashSet<Guid> Subtree(Guid id, HashSet<Guid> visited)
            {
                var people = new HashSet<Guid>(direct.GetValueOrDefault(id) ?? []);

                foreach (var child in children[id])
                {
                    if (visited.Add(child.Id))
                    {
                        people.UnionWith(Subtree(child.Id, visited));
                    }
                }

                return people;
            }

            // Daraxt tartibi: ota — keyin uning bolalari (nom bo'yicha), chuqurlikka
            var ordered = new List<Guid>();
            var placed = new HashSet<Guid>();

            void Walk(Guid id)
            {
                if (!placed.Add(id))
                {
                    return;
                }

                ordered.Add(id);

                foreach (var child in children[id].OrderBy(item => item.Name, StringComparer.CurrentCulture))
                {
                    Walk(child.Id);
                }
            }

            foreach (var top in departments
                         .Where(item => item.ParentId is null || !departmentNames.ContainsKey(item.ParentId.Value))
                         .OrderBy(item => item.Name, StringComparer.CurrentCulture))
            {
                Walk(top.Id);
            }

            // Halqa bo'lib qolgan (ota-bola noto'g'ri) yozuvlar ham tushib qolmasin
            foreach (var rest in departments.Where(item => !placed.Contains(item.Id)))
            {
                Walk(rest.Id);
            }

            var byId = departments.ToDictionary(item => item.Id);

            var departmentItems = ordered
                .Select(id => byId[id])
                .Where(item => (withInactive || item.IsActive) && (branch is null || branch.Contains(item.Id)))
                .Select(item => new
                {
                    id = item.Id,
                    name = item.Name,
                    path = PathOf(item.Id),
                    parent = departmentNames.Name(item.ParentId),
                    level = item.Level,
                    legalEntity = legalEntityNames.Name(item.LegalEntityId),
                    head = heads.Name(item.HeadEmployeeId),
                    activeEmployees = direct.GetValueOrDefault(item.Id)?.Count ?? 0,
                    activeEmployeesWithSubdepartments = Subtree(item.Id, [item.Id]).Count,
                    positions = positionCounts.GetValueOrDefault(item.Id),
                    isActive = item.IsActive ? (bool?)null : false,
                    note = item.Note,
                })
                .ToList();

            var directions = await db.Directions
                .Where(item => withInactive || item.IsActive)
                .OrderBy(item => item.Name)
                .Select(item => new { item.Id, item.Name, item.IsActive })
                .ToListAsync(token)
                .ConfigureAwait(false);

            var activeIds = staff.Select(item => item.EmployeeId).Distinct().ToList();

            var directionSets = await db.Employees
                .Where(employee => activeIds.Contains(employee.Id) && employee.DirectionIds.Count > 0)
                .Select(employee => employee.DirectionIds)
                .ToListAsync(token)
                .ConfigureAwait(false);

            var directionCounts = directionSets
                .SelectMany(set => set.Distinct())
                .GroupBy(id => id)
                .ToDictionary(group => group.Key, group => group.Count());

            var catalogs = await CatalogsAsync(withInactive, token).ConfigureAwait(false);

            var rowCount = legalEntities.Count + departmentItems.Count + directions.Count;

            return t.Json(
                new
                {
                    legalEntities = legalEntities.Select(item => new
                    {
                        id = item.Id,
                        name = item.Name,
                        fullName = item.FullName,
                        taxId = item.TaxId,
                        activeEmployees = staff.Where(row => row.LegalEntityId == item.Id).Select(row => row.EmployeeId).Distinct().Count(),
                        isActive = item.IsActive ? (bool?)null : false,
                    }).ToList(),
                    departments = departmentItems,
                    directions = directions.Select(item => new
                    {
                        id = item.Id,
                        name = item.Name,
                        activeEmployees = directionCounts.GetValueOrDefault(item.Id),
                        isActive = item.IsActive ? (bool?)null : false,
                    }).ToList(),
                    catalogs,
                    note = "Lavozimlar ro'yxati (vazifalari bilan) — section=positions.",
                },
                rowCount);
        }
    }

    private async Task<string> PositionsAsync(
        Dictionary<Guid, string> departmentNames,
        HashSet<Guid>? branch,
        string? query,
        bool withInactive,
        IEnumerable<(Guid EmployeeId, Guid? PositionId)> staff,
        Func<Guid, string> pathOf,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var db = t.Db;
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);

        // Lavozimlar bir necha yuz qator — nom bo'yicha qidiruv xotirada,
        // ismlardagi bilan bir xil yig'ish bilan.
        var positions = await db.Positions
            .Where(item => withInactive || item.IsActive)
            .Select(item => new { item.Id, item.Name, item.DepartmentId, item.Grade, item.ProbationDays, item.IsActive, item.Note })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (branch is not null)
        {
            positions = [.. positions.Where(item => item.DepartmentId is { } id && branch.Contains(id))];
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(EmployeeQueryKit.Fold).ToList();
            positions = [.. positions.Where(item => words.TrueForAll(word => EmployeeQueryKit.Fold(item.Name).Contains(word, StringComparison.Ordinal)))];
        }

        var paths = positions
            .Select(item => item.DepartmentId)
            .OfType<Guid>()
            .Distinct()
            .ToDictionary(id => id, pathOf);

        var ordered = positions
            .OrderBy(item => item.DepartmentId is { } id ? paths[id] : "￿", StringComparer.CurrentCulture)
            .ThenByDescending(item => item.Grade)
            .ThenBy(item => item.Name, StringComparer.CurrentCulture)
            .ToList();

        var pageRows = ordered.Skip((pageNo - 1) * size).Take(size).ToList();
        var ids = pageRows.Select(item => item.Id).ToList();

        var duties = await db.PositionDuties
            .Where(item => ids.Contains(item.PositionId))
            .OrderBy(item => item.SortOrder)
            .Select(item => new { item.PositionId, item.Text })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dutiesOf = duties.ToLookup(item => item.PositionId, item => item.Text);

        var counts = staff
            .Where(item => item.PositionId != null)
            .GroupBy(item => item.PositionId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(item => item.EmployeeId).Distinct().Count());

        var items = pageRows.Select(item => new
        {
            id = item.Id,
            name = item.Name,
            department = departmentNames.Name(item.DepartmentId),
            departmentPath = item.DepartmentId is { } id ? paths[id] : null,
            grade = item.Grade,
            probationDays = item.ProbationDays,
            activeEmployees = counts.GetValueOrDefault(item.Id),
            duties = dutiesOf[item.Id].ToList() is { Count: > 0 } list ? list : null,
            isActive = item.IsActive ? (bool?)null : false,
            note = item.Note,
        }).ToList();

        return t.Json(
            new
            {
                total = ordered.Count,
                page = pageNo,
                pageSize = size,
                items,
            },
            items.Count);
    }

    private async Task<object> CatalogsAsync(bool withInactive, CancellationToken cancellationToken)
    {
        var db = t.Db;

        var terminationReasons = await db.TerminationReasons
            .Where(item => withInactive || item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new { item.Name, item.IsVoluntary, item.IsRehireable, item.IsActive })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var awardTypes = await db.AwardTypes
            .Where(item => withInactive || item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new { item.Name, item.HasAmount, item.IsActive })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var penaltyTypes = await db.PenaltyTypes
            .Where(item => withInactive || item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new { item.Name, item.HasAmount, item.Severity, item.IsActive })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var documentTypes = await db.DocumentTypes
            .Where(item => withInactive || item.IsActive)
            .OrderByDescending(item => item.IsRequired)
            .ThenBy(item => item.Name)
            .Select(item => new { item.Name, item.IsRequired, item.IsActive })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var absenceTypes = await db.AbsenceTypes
            .Where(item => withInactive || item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new { item.Name, item.IsActive })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new
        {
            terminationReasons = terminationReasons.Select(item => new
            {
                name = item.Name,
                voluntary = item.IsVoluntary,
                rehireable = item.IsRehireable,
                isActive = item.IsActive ? (bool?)null : false,
            }).ToList(),
            awardTypes = awardTypes.Select(item => new
            {
                name = item.Name,
                hasAmount = item.HasAmount,
                isActive = item.IsActive ? (bool?)null : false,
            }).ToList(),
            penaltyTypes = penaltyTypes.Select(item => new
            {
                name = item.Name,
                hasAmount = item.HasAmount,
                severity = item.Severity,
                isActive = item.IsActive ? (bool?)null : false,
            }).ToList(),
            documentTypes = documentTypes.Select(item => new
            {
                name = item.Name,
                required = item.IsRequired,
                isActive = item.IsActive ? (bool?)null : false,
            }).ToList(),
            absenceTypes = absenceTypes.Select(item => new
            {
                name = item.Name,
                isActive = item.IsActive ? (bool?)null : false,
            }).ToList(),
        };
    }
}
