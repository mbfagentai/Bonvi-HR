using System.ComponentModel;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Bonvi.Api.Mcp.Tools;

/// <summary>Ro'yxatlar: rezerv, qora ro'yxat, nazorat, qobiliyatli.</summary>
[McpServerToolType]
public sealed class ListTools(McpToolContext t)
{
    [McpServerTool(Name = "list_flags", Title = "Ro'yxatlar (belgilar)",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Belgilangan xodimlar ro'yxati: rezerv (Reserve), qora ro'yxat (Blacklist), nazorat (Monitoring), qobiliyatli (Capable). "
        + "Har qatorda xodim, belgi turi, boshlanish va tugash sanasi, sabab, kim kiritgani va xodimning joriy (yoki oxirgi) ish joyi. "
        + "Sukut bo'yicha faqat hozir kuchdagi belgilar; tur bo'yicha jami sonlar ham qaytadi. Sahifalangan. "
        + "Reserve / blacklist / monitoring / capable lists with reason, dates and current employment.")]
    public async Task<string> ListFlags(
        [Description("Belgi turi: Reserve, Blacklist, Monitoring yoki Capable. Bo'sh — to'rttasi ham.")] string? type = null,
        [Description("true — tugagan (yopilgan) belgilarni ham ko'rsatish; sukut — faqat hozir kuchdagilar.")] bool? includeEnded = null,
        [Description("Xodim ismi yoki telefoni bo'yicha qidiruv (ism kirill yozuvida, masalan «Каримов»).")] string? query = null,
        [Description("Sahifa raqami, 1 dan.")] int? page = null,
        [Description("Sahifadagi qatorlar soni (sukut 50, ko'pi bilan 200).")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.ListsView, "Ro'yxatlar");

        var db = t.Db;
        var today = McpToolContext.Today;
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);
        var flagType = EmployeeQueryKit.ParseEnum<EmployeeFlagType>(type, "type");
        var withEnded = includeEnded ?? false;

        // Xodim orqali birlashtiriladi: o'chirilgan xodimning belgisi ro'yxatga
        // tushmaydi (veb-ro'yxatdagi kabi). Qidiruv BELGI EGASIGA qo'llanadi —
        // sabab matnida ism uchrashi mumkin, begona odam topilmasin.
        var employees = await EmployeeQueryKit.MatchTextAsync(t, db.Employees, query, cancellationToken).ConfigureAwait(false);

        var flags = db.EmployeeFlags.AsQueryable();

        if (flagType is { } ft)
        {
            flags = flags.Where(flag => flag.Type == ft);
        }

        if (!withEnded)
        {
            flags = flags.Where(flag => flag.StartDate <= today && (flag.EndDate == null || flag.EndDate > today));
        }

        var joined =
            from flag in flags
            join employee in employees on flag.EmployeeId equals employee.Id
            select new { Flag = flag, Employee = employee };

        var total = await joined.CountAsync(cancellationToken).ConfigureAwait(false);

        var byType = await joined
            .GroupBy(row => row.Flag.Type)
            .Select(group => new { Type = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await joined
            .OrderByDescending(row => row.Flag.StartDate)
            .ThenBy(row => row.Employee.FullName.LastName)
            .ThenBy(row => row.Employee.FullName.FirstName)
            .ThenBy(row => row.Flag.Id)
            .Skip((pageNo - 1) * size)
            .Take(size)
            .Select(row => new
            {
                row.Flag.EmployeeId,
                row.Employee.FullName,
                row.Employee.Phone,
                row.Flag.Type,
                row.Flag.StartDate,
                row.Flag.EndDate,
                row.Flag.Reason,
                row.Flag.CreatedAt,
                row.Flag.CreatedBy,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = await EmployeeQueryKit.OrgNamesAsync(t, cancellationToken).ConfigureAwait(false);
        var jobs = await EmployeeQueryKit.CurrentJobsAsync(t, [.. rows.Select(row => row.EmployeeId).Distinct()], cancellationToken)
            .ConfigureAwait(false);
        var authors = await t.UserNamesAsync(rows.Select(row => row.CreatedBy).OfType<Guid>(), cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(row => new
        {
            employeeId = row.EmployeeId,
            name = row.FullName.Full,
            phone = McpLookups.Phone(row.Phone),
            type = row.Type,
            startDate = McpToolContext.Iso(row.StartDate),
            endDate = McpToolContext.Iso(row.EndDate),
            active = row.StartDate <= today && (row.EndDate == null || row.EndDate >= today),
            reason = row.Reason,
            addedBy = authors.Name(row.CreatedBy),
            addedAt = McpToolContext.Local(row.CreatedAt),
            job = EmployeeQueryKit.Summary(jobs.GetValueOrDefault(row.EmployeeId), names),
        }).ToList();

        return t.Json(
            new
            {
                total,
                page = pageNo,
                pageSize = size,
                onlyActive = !withEnded,
                byType = byType.OrderBy(item => item.Type).ToDictionary(item => item.Type.ToString(), item => item.Count),
                items,
            },
            items.Count);
    }
}
