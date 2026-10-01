using System.ComponentModel;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Feedback;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Safety;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Bonvi.Api.Mcp.Tools;

/// <summary>
/// Kadrlar yozuvlari: rag'bat va jarimalar, ishdan ketuvchilar bilan suhbat
/// (qayta aloqa), baxtsiz hodisalar.
/// </summary>
/// <remarks>
/// Rag'bat/jarima va baxtsiz hodisa SHAXSGA bog'langan (mehnat munosabatiga
/// emas), shu sababli ularning bo'limi yozuvning o'zida yo'q: u xodimning
/// O'SHA SANADA amalda bo'lgan munosabatidan olinadi (<see cref="PlacementAt"/>).
/// </remarks>
[McpServerToolType]
public sealed class HrRecordTools(McpToolContext t)
{
    /// <summary>Sukut bo'yicha davr — so'nggi 12 oy (bu yozuvlar kam uchraydi).</summary>
    private const int DefaultDays = 365;

    // ---------------------------------------------------------------------
    //  incentives
    // ---------------------------------------------------------------------

    [McpServerTool(Name = "incentives", Title = "Rag'bat va jarimalar",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Rag'bat (mukofot) va jarimalar davr bo'yicha: tur, bo'lim yoki xodim filtri bilan. Har qatorda sana, "
        + "rag'bat/jarima, tur nomi, xodim va uning o'sha sanadagi bo'limi, summa, sovg'a, sabab, buyruq raqami va sanasi. "
        + "Tur va rag'bat/jarima bo'yicha jami soni va summasi ham beriladi. "
        + "Awards and penalties in a period with totals per kind and type.")]
    public async Task<string> Incentives(
        [Description("Davr boshi, YYYY-MM-DD. Bo'sh — so'nggi 365 kun.")] string? from = null,
        [Description("Davr oxiri, YYYY-MM-DD. Bo'sh — bugun. Oraliq ko'pi bilan 366 kun.")] string? to = null,
        [Description("Award — rag'bat, Penalty — jarima. Bo'sh — ikkalasi.")] string? kind = null,
        [Description("Rag'bat yoki jarima turining identifikatori (org_structure vositasidagi award/penalty turlari).")] Guid? typeId = null,
        [Description("Bo'lim identifikatori — ichki bo'limlari bilan; xodimning shu shoxdagi istalgan mehnat munosabati hisobga olinadi.")] Guid? departmentId = null,
        [Description("Xodim identifikatori — faqat shu xodimning yozuvlari.")] Guid? employeeId = null,
        [Description("Sahifa raqami, 1 dan boshlanadi.")] int? page = null,
        [Description("Sahifadagi qatorlar soni: sukut 50, ko'pi bilan 200.")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.IncentivesView, "Rag'bat va jarima");

        var (start, end) = McpToolContext.Range(from, to, DefaultDays);
        IncentiveKind? kindFilter = string.IsNullOrWhiteSpace(kind) ? null : EmploymentTools.ParseEnum<IncentiveKind>(kind, "kind");
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);

        var db = t.Db;
        var branch = await EmploymentTools.DepartmentBranchAsync(t, departmentId, cancellationToken).ConfigureAwait(false);

        var incentives = db.Incentives.Where(incentive => incentive.Date >= start && incentive.Date <= end);

        if (kindFilter is { } wantedKind)
        {
            incentives = incentives.Where(incentive => incentive.Kind == wantedKind);
        }

        if (typeId is { } wantedType)
        {
            incentives = incentives.Where(incentive => incentive.TypeId == wantedType);
        }

        if (employeeId is { } wantedEmployee)
        {
            incentives = incentives.Where(incentive => incentive.EmployeeId == wantedEmployee);
        }

        if (branch is not null)
        {
            // Veb-ro'yxatdagi kabi: xodimning shu shoxdagi ISTALGAN munosabati (o'rindoshlar ham).
            var owners = db.Employments
                .Where(employment => employment.DepartmentId != null && branch.Contains(employment.DepartmentId.Value))
                .Select(employment => employment.EmployeeId);

            incentives = incentives.Where(incentive => owners.Contains(incentive.EmployeeId));
        }

        var rows =
            from incentive in incentives
            join person in db.Employees on incentive.EmployeeId equals person.Id
            select new
            {
                incentive.Id,
                incentive.Kind,
                incentive.TypeId,
                incentive.Date,
                incentive.Amount,
                incentive.GiftDescription,
                incentive.Reason,
                incentive.OrderNo,
                incentive.OrderDate,
                incentive.EmployeeId,
                person.FullName,
            };

        // Jami — bazada guruhlanadi (tur + valyuta), summa valyuta bo'yicha alohida.
        var sums = await rows
            .GroupBy(row => new { row.Kind, row.TypeId, Currency = (string?)row.Amount!.Currency })
            .Select(group => new
            {
                group.Key.Kind,
                group.Key.TypeId,
                group.Key.Currency,
                Count = group.Count(),
                Amount = group.Sum(row => (decimal?)row.Amount!.Amount),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = sums.Sum(item => item.Count);

        var items = await rows
            .OrderByDescending(row => row.Date)
            .ThenByDescending(row => row.Id)
            .Skip((pageNo - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var awardTypes = await db.AwardTypes.IgnoreQueryFilters()
            .Select(type => new { type.Id, type.Name })
            .ToDictionaryAsync(type => type.Id, type => type.Name, cancellationToken)
            .ConfigureAwait(false);

        var penaltyTypes = await db.PenaltyTypes.IgnoreQueryFilters()
            .Select(type => new { type.Id, type.Name })
            .ToDictionaryAsync(type => type.Id, type => type.Name, cancellationToken)
            .ConfigureAwait(false);

        // TypeId bitta ustun, lekin Kind ga qarab IKKI xil ma'lumotnomaga ishora qiladi.
        string? TypeName(IncentiveKind incentiveKind, Guid id) =>
            (incentiveKind == IncentiveKind.Award ? awardTypes : penaltyTypes).Name(id);

        var placements = await PlacementsAsync(items.Select(row => row.EmployeeId), cancellationToken).ConfigureAwait(false);
        var legalEntities = await t.LegalEntitiesAsync(cancellationToken).ConfigureAwait(false);
        var departments = await t.DepartmentsAsync(cancellationToken).ConfigureAwait(false);

        return t.Json(
            new
            {
                from = McpToolContext.Iso(start),
                to = McpToolContext.Iso(end),
                totals = new
                {
                    byKind = Enum.GetValues<IncentiveKind>().Select(incentiveKind => new
                    {
                        kind = incentiveKind,
                        count = sums.Where(item => item.Kind == incentiveKind).Sum(item => item.Count),
                        amounts = Amounts(sums.Where(item => item.Kind == incentiveKind).Select(item => (item.Currency, item.Amount))),
                    }),
                    byType = sums
                        .GroupBy(item => new { item.Kind, item.TypeId })
                        .Select(group => new
                        {
                            kind = group.Key.Kind,
                            typeId = group.Key.TypeId,
                            type = TypeName(group.Key.Kind, group.Key.TypeId),
                            count = group.Sum(item => item.Count),
                            amounts = Amounts(group.Select(item => (item.Currency, item.Amount))),
                        })
                        .OrderBy(item => item.kind)
                        .ThenByDescending(item => item.count),
                },
                total,
                page = pageNo,
                pageSize = size,
                pageCount = (total + size - 1) / size,
                items = items.Select(row =>
                {
                    var placement = PlacementAt(placements, row.EmployeeId, row.Date);
                    return new
                    {
                        date = McpToolContext.Iso(row.Date),
                        kind = row.Kind,
                        type = TypeName(row.Kind, row.TypeId),
                        employeeId = row.EmployeeId,
                        employee = row.FullName.Full,
                        legalEntity = legalEntities.Name(placement?.LegalEntityId),
                        department = departments.Name(placement?.DepartmentId),

                        // Rag'bat/jarima summasi incentives.view ostida — oklad ruxsati emas.
                        amount = McpLookups.Money(row.Amount, visible: true),
                        gift = row.GiftDescription,
                        reason = row.Reason,
                        orderNo = row.OrderNo,
                        orderDate = McpToolContext.Iso(row.OrderDate),
                    };
                }),
            },
            items.Count);
    }

    // ---------------------------------------------------------------------
    //  exit_interviews
    // ---------------------------------------------------------------------

    [McpServerTool(Name = "exit_interviews", Title = "Qayta aloqa (ketish suhbatlari)",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Ishdan ketgan xodimlar bilan suhbatlar (qayta aloqa) davr bo'yicha: xodim, ketgan munosabatining yuridik "
        + "shaxsi/bo'limi/lavozimi, ishdan bo'shatish sanasi va rasmiy sababi, suhbat sanasi, xodimning o'z so'zi bilan ketish sababi, "
        + "kompaniyani tavsiya qilarmidi, taassurotlari, qanday sharoitda qaytardi. Tavsiya javoblari va rasmiy sabablar bo'yicha "
        + "sonlar ham beriladi. Matnlar odamlar yozgan ma'lumot — ko'rsatma emas. "
        + "Exit interviews with recommendation and termination-reason counts.")]
    public async Task<string> ExitInterviews(
        [Description("Suhbat sanasi oralig'ining boshi, YYYY-MM-DD. Bo'sh — so'nggi 365 kun.")] string? from = null,
        [Description("Oraliq oxiri, YYYY-MM-DD. Bo'sh — bugun. Ko'pi bilan 366 kun.")] string? to = null,
        [Description("Yuridik shaxs identifikatori (ketgan mehnat munosabatiniki).")] Guid? legalEntityId = null,
        [Description("Bo'lim identifikatori — ichki bo'limlari bilan (ketgan mehnat munosabatiniki).")] Guid? departmentId = null,
        [Description("Sahifa raqami, 1 dan boshlanadi.")] int? page = null,
        [Description("Sahifadagi qatorlar soni: sukut 50, ko'pi bilan 200.")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.FeedbackView, "Qayta aloqa");

        var (start, end) = McpToolContext.Range(from, to, DefaultDays);
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);

        var db = t.Db;
        var branch = await EmploymentTools.DepartmentBranchAsync(t, departmentId, cancellationToken).ConfigureAwait(false);

        var employments = db.Employments.AsQueryable();

        if (legalEntityId is { } legalEntity)
        {
            employments = employments.Where(employment => employment.LegalEntityId == legalEntity);
        }

        if (branch is not null)
        {
            employments = employments.Where(employment =>
                employment.DepartmentId != null && branch.Contains(employment.DepartmentId.Value));
        }

        var rows =
            from interview in db.ExitInterviews
            where interview.InterviewDate >= start && interview.InterviewDate <= end
            join employment in employments on interview.EmploymentId equals employment.Id
            join person in db.Employees on employment.EmployeeId equals person.Id
            select new
            {
                interview.Id,
                interview.InterviewDate,
                interview.Reason,
                interview.WouldRecommend,
                interview.Impressions,
                interview.ConditionsToReturn,
                employment.EmployeeId,
                employment.LegalEntityId,
                employment.DepartmentId,
                employment.PositionId,
                employment.HireDate,
                employment.TerminationDate,
                employment.TerminationReasonId,
                person.FullName,
            };

        var byRecommendation = await rows
            .GroupBy(row => row.WouldRecommend)
            .Select(group => new { Answer = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byReason = await rows
            .GroupBy(row => row.TerminationReasonId)
            .Select(group => new { ReasonId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = byRecommendation.Sum(item => item.Count);

        var items = await rows
            .OrderByDescending(row => row.InterviewDate)
            .ThenByDescending(row => row.Id)
            .Skip((pageNo - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var reasons = await EmploymentTools.TerminationReasonDetailsAsync(t, cancellationToken).ConfigureAwait(false);
        var legalEntities = await t.LegalEntitiesAsync(cancellationToken).ConfigureAwait(false);
        var departments = await t.DepartmentsAsync(cancellationToken).ConfigureAwait(false);
        var positions = await t.PositionsAsync(cancellationToken).ConfigureAwait(false);

        string ReasonName(Guid? id) =>
            id is null ? "Ko'rsatilmagan" : reasons.TryGetValue(id.Value, out var reason) ? reason.Name : "Noma'lum sabab";

        var recommendation = Enum.GetValues<RecommendationAnswer>()
            .ToDictionary(
                answer => answer.ToString(),
                answer => byRecommendation.Where(item => item.Answer == answer).Sum(item => item.Count));
        recommendation["NotAnswered"] = byRecommendation.Where(item => item.Answer is null).Sum(item => item.Count);

        return t.Json(
            new
            {
                from = McpToolContext.Iso(start),
                to = McpToolContext.Iso(end),
                wouldRecommendCounts = recommendation,
                byTerminationReason = byReason
                    .Select(item => new
                    {
                        reason = ReasonName(item.ReasonId),
                        isVoluntary = item.ReasonId is { } id && reasons.TryGetValue(id, out var reason) ? reason.IsVoluntary : (bool?)null,
                        count = item.Count,
                    })
                    .OrderByDescending(item => item.count)
                    .ThenBy(item => item.reason, StringComparer.Ordinal),
                total,
                page = pageNo,
                pageSize = size,
                pageCount = (total + size - 1) / size,
                items = items.Select(row => new
                {
                    employeeId = row.EmployeeId,
                    employee = row.FullName.Full,
                    legalEntity = legalEntities.Name(row.LegalEntityId),
                    department = departments.Name(row.DepartmentId),
                    position = positions.Name(row.PositionId),
                    hireDate = McpToolContext.Iso(row.HireDate),
                    terminationDate = McpToolContext.Iso(row.TerminationDate),
                    terminationReason = ReasonName(row.TerminationReasonId),
                    interviewDate = McpToolContext.Iso(row.InterviewDate),
                    reason = row.Reason,
                    wouldRecommend = row.WouldRecommend,
                    impressions = row.Impressions,
                    conditionsToReturn = row.ConditionsToReturn,
                }),
            },
            items.Count);
    }

    // ---------------------------------------------------------------------
    //  incidents
    // ---------------------------------------------------------------------

    [McpServerTool(Name = "incidents", Title = "Baxtsiz hodisalar",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Ish joyidagi baxtsiz hodisalar davr va jabrlanish darajasi bo'yicha: sana, smena/vaqt, joy, xodim va uning "
        + "o'sha sanadagi bo'limi, tavsif, sabab, ko'rilgan chora, daraja, tibbiy yordam, komissiya xulosasi va sanasi, mas'ul xodim. "
        + "Daraja bo'yicha sonlar ham beriladi. "
        + "Workplace safety incidents by period and severity, with counts per severity.")]
    public async Task<string> Incidents(
        [Description("Davr boshi, YYYY-MM-DD. Bo'sh — so'nggi 365 kun.")] string? from = null,
        [Description("Davr oxiri, YYYY-MM-DD. Bo'sh — bugun. Oraliq ko'pi bilan 366 kun.")] string? to = null,
        [Description("Jabrlanish darajasi (bir nechta bo'lishi mumkin): Minor — yengil, Moderate — o'rtacha og'ir, "
            + "Severe — og'ir, Fatal — o'lim. Bo'sh — hammasi.")] string[]? severity = null,
        [Description("Sahifa raqami, 1 dan boshlanadi.")] int? page = null,
        [Description("Sahifadagi qatorlar soni: sukut 50, ko'pi bilan 200.")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.SafetyView, "Baxtsiz hodisalar");

        var (start, end) = McpToolContext.Range(from, to, DefaultDays);
        var wanted = EmploymentTools.ParseEnums<IncidentSeverity>(severity, "severity");
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);

        var db = t.Db;

        var rows =
            from incident in db.Incidents
            where incident.IncidentDate >= start && incident.IncidentDate <= end
            join person in db.Employees on incident.EmployeeId equals person.Id
            select new
            {
                incident.Id,
                incident.IncidentDate,
                incident.Shift,
                incident.Location,
                incident.Description,
                incident.Cause,
                incident.CorrectiveAction,
                incident.Severity,
                incident.MedicalAidProvided,
                incident.MedicalAidNote,
                incident.CommissionConclusion,
                incident.ConcludedAt,
                incident.ResponsibleEmployeeId,
                incident.EmployeeId,
                person.FullName,
            };

        // Daraja bo'yicha son — daraja filtrisiz: davrning to'liq manzarasi ko'rinsin.
        var bySeverity = await rows
            .GroupBy(row => row.Severity)
            .Select(group => new { Severity = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Severity, item => item.Count, cancellationToken)
            .ConfigureAwait(false);

        if (wanted.Count > 0)
        {
            rows = rows.Where(row => wanted.Contains(row.Severity));
        }

        var total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await rows
            .OrderByDescending(row => row.IncidentDate)
            .ThenByDescending(row => row.Id)
            .Skip((pageNo - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var placements = await PlacementsAsync(items.Select(row => row.EmployeeId), cancellationToken).ConfigureAwait(false);
        var responsible = await t
            .EmployeeNamesAsync(items.Where(row => row.ResponsibleEmployeeId != null).Select(row => row.ResponsibleEmployeeId!.Value), cancellationToken)
            .ConfigureAwait(false);
        var legalEntities = await t.LegalEntitiesAsync(cancellationToken).ConfigureAwait(false);
        var departments = await t.DepartmentsAsync(cancellationToken).ConfigureAwait(false);
        var positions = await t.PositionsAsync(cancellationToken).ConfigureAwait(false);

        return t.Json(
            new
            {
                from = McpToolContext.Iso(start),
                to = McpToolContext.Iso(end),
                countsBySeverity = Enum.GetValues<IncidentSeverity>()
                    .ToDictionary(level => level.ToString(), level => bySeverity.GetValueOrDefault(level)),
                total,
                page = pageNo,
                pageSize = size,
                pageCount = (total + size - 1) / size,
                items = items.Select(row =>
                {
                    var placement = PlacementAt(placements, row.EmployeeId, row.IncidentDate);
                    return new
                    {
                        date = McpToolContext.Iso(row.IncidentDate),
                        shift = row.Shift,
                        location = row.Location,
                        employeeId = row.EmployeeId,
                        employee = row.FullName.Full,
                        legalEntity = legalEntities.Name(placement?.LegalEntityId),
                        department = departments.Name(placement?.DepartmentId),
                        position = positions.Name(placement?.PositionId),
                        description = row.Description,
                        cause = row.Cause,
                        correctiveAction = row.CorrectiveAction,
                        severity = row.Severity,
                        medicalAidProvided = row.MedicalAidProvided,
                        medicalAidNote = row.MedicalAidNote,
                        commissionConclusion = row.CommissionConclusion,
                        concludedAt = McpToolContext.Iso(row.ConcludedAt),
                        responsible = responsible.Name(row.ResponsibleEmployeeId),
                    };
                }),
            },
            items.Count);
    }

    // ---------------------------------------------------------------------
    //  Yordamchilar
    // ---------------------------------------------------------------------

    /// <summary>Sahifadagi xodimlarning mehnat munosabatlari — bitta so'rov, faqat kerakli ustunlar.</summary>
    private async Task<ILookup<Guid, Placement>> PlacementsAsync(IEnumerable<Guid> employeeIds, CancellationToken cancellationToken)
    {
        var ids = employeeIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return Array.Empty<Placement>().ToLookup(placement => placement.EmployeeId);
        }

        var rows = await t.Db.Employments
            .Where(employment => ids.Contains(employment.EmployeeId))
            .Select(employment => new Placement(
                employment.EmployeeId,
                employment.LegalEntityId,
                employment.DepartmentId,
                employment.PositionId,
                employment.IsPrimary,
                employment.HireDate,
                employment.TerminationDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToLookup(row => row.EmployeeId);
    }

    /// <summary>
    /// Xodimning shu sanadagi munosabati: avval amalda bo'lgani (asosiysi oldin),
    /// bo'lmasa sanadan oldin boshlanganlarning eng oxirgisi, bo'lmasa istalgani.
    /// </summary>
    private static Placement? PlacementAt(ILookup<Guid, Placement> placements, Guid employeeId, DateOnly date) =>
        placements[employeeId]
            .OrderByDescending(placement => placement.HireDate <= date
                && (placement.TerminationDate == null || placement.TerminationDate >= date))
            .ThenByDescending(placement => placement.HireDate <= date)
            .ThenByDescending(placement => placement.IsPrimary)
            .ThenByDescending(placement => placement.HireDate)
            .FirstOrDefault();

    /// <summary>Valyuta bo'yicha jami summa; summasiz yozuvlar tushib qoladi.</summary>
    private static List<object> Amounts(IEnumerable<(string? Currency, decimal? Amount)> parts) =>
    [
        .. parts
            .Where(part => part.Currency is not null && part.Amount is not null)
            .GroupBy(part => part.Currency!, StringComparer.Ordinal)
            .Select(group => (object)new { currency = group.Key, total = group.Sum(part => part.Amount!.Value) }),
    ];

    private sealed record Placement(
        Guid EmployeeId,
        Guid LegalEntityId,
        Guid? DepartmentId,
        Guid? PositionId,
        bool IsPrimary,
        DateOnly HireDate,
        DateOnly? TerminationDate);
}
