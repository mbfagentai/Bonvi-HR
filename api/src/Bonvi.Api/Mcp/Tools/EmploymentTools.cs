using System.ComponentModel;
using System.Globalization;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Bonvi.Api.Mcp.Tools;

/// <summary>
/// Mehnat munosabatlari: buyruqlar jurnali, kadrlar oqimi va sinov muddatlari.
/// </summary>
/// <remarks>
/// Ta'riflar veb-paneldagi bilan BIR XIL: buyruqlar jurnali —
/// <c>EmployeeReadModel.ListEmploymentEventsAsync</c>, kadrlar oqimi —
/// <c>ReportsReadModel.GetTurnoverAsync</c>, oklad yashirish —
/// <c>EmployeeMapper</c> / <c>SalaryAccess</c>. AI va HR bitta savolga
/// ikki xil son ko'rmasligi kerak.
/// </remarks>
[McpServerToolType]
public sealed class EmploymentTools(McpToolContext t)
{
    private const string Area = "Mehnat munosabatlari";

    /// <summary>Kesimdagi eng ko'p guruh — lavozimlar 500 ga yaqin bo'lishi mumkin.</summary>
    private const int MaxGroups = 100;

    /// <summary>«Erta ketgan» chegarasi — ishlagan kun.</summary>
    private const int EarlyLeaverDays = 90;

    private const string GroupByLegalEntity = "legal_entity";
    private const string GroupByDepartment = "department";
    private const string GroupByPosition = "position";

    private const string ViewInProgress = "in_progress";
    private const string ViewEnding = "ending";
    private const string ViewOverdue = "overdue";
    private const string ViewResults = "results";

    private static readonly string[] GroupByValues = [GroupByLegalEntity, GroupByDepartment, GroupByPosition];

    private static readonly string[] ProbationViews = [ViewInProgress, ViewEnding, ViewOverdue, ViewResults];

    // ---------------------------------------------------------------------
    //  employment_events
    // ---------------------------------------------------------------------

    [McpServerTool(Name = "employment_events", Title = "Buyruqlar jurnali",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Buyruqlar jurnali: ishga qabul, ko'chirish, lavozim o'zgarishi, qo'shimcha vazifa, oklad o'zgarishi, "
        + "sinov natijasi, ishdan bo'shatish — davr, tur, yuridik shaxs, bo'lim yoki xodim bo'yicha. Har qatorda sana, xodim, "
        + "qaysi bo'lim/lavozimdan qaysisiga, buyruq raqami va sanasi, izoh; summalar faqat oklad ruxsati bo'lsa "
        + "(qo'shimcha vazifa haqi — alohida ruxsat bilan). Davrdagi har bir tur soni ham beriladi. "
        + "Order journal (hires, transfers, promotions, duties, salary changes, probation results, terminations) with salary masked by permissions.")]
    public async Task<string> EmploymentEvents(
        [Description("Davr boshi (buyruq kuchga kirgan sana), YYYY-MM-DD. Bo'sh — so'nggi 30 kun.")] string? from = null,
        [Description("Davr oxiri, YYYY-MM-DD. Bo'sh — bugun. Oraliq ko'pi bilan 366 kun.")] string? to = null,
        [Description("Hodisa turlari (bir nechta bo'lishi mumkin): Hire, Transfer, Promotion, AdditionalDuty, SalaryChange, "
            + "ProbationResult, Termination, Rehire. Bo'sh — hammasi.")] string[]? types = null,
        [Description("Yuridik shaxs identifikatori (org_structure vositasidan).")] Guid? legalEntityId = null,
        [Description("Bo'lim identifikatori — ichki bo'limlari bilan birga. Hodisa shu bo'limDAN yoki shu bo'limGA bo'lsa kiradi; "
            + "bo'lim ko'rsatilmagan hodisada (oklad, bo'shatish, vazifa) mehnat munosabatining bo'limi olinadi.")] Guid? departmentId = null,
        [Description("Xodim identifikatori — faqat shu xodimning buyruqlari.")] Guid? employeeId = null,
        [Description("Sahifa raqami, 1 dan boshlanadi.")] int? page = null,
        [Description("Sahifadagi qatorlar soni: sukut 50, ko'pi bilan 200.")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.EmploymentView, Area);

        var (start, end) = McpToolContext.Range(from, to);
        var wanted = ParseEnums<EmploymentEventType>(types, "types");
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);

        // Oklad qoidasi EmployeeMapper bilan aynan bir xil: eski/yangi oklad —
        // salary.view; qo'shimcha vazifa haqi — salary.view VA salary.extra.view.
        var canSalary = t.Has(Permissions.SalaryView);
        var canExtraPay = canSalary && t.Has(Permissions.SalaryExtraView);

        var db = t.Db;
        var branch = await DepartmentBranchAsync(t, departmentId, cancellationToken).ConfigureAwait(false);

        var employments = db.Employments.AsQueryable();

        if (legalEntityId is { } legalEntity)
        {
            employments = employments.Where(employment => employment.LegalEntityId == legalEntity);
        }

        if (employeeId is { } employee)
        {
            employments = employments.Where(employment => employment.EmployeeId == employee);
        }

        var rows =
            from employmentEvent in db.EmploymentEvents
            where employmentEvent.EffectiveDate >= start && employmentEvent.EffectiveDate <= end
            join employment in employments on employmentEvent.EmploymentId equals employment.Id
            join person in db.Employees on employment.EmployeeId equals person.Id
            select new
            {
                employmentEvent.Id,
                employmentEvent.Type,
                employmentEvent.EffectiveDate,
                employmentEvent.OrderNo,
                employmentEvent.OrderDate,
                employmentEvent.FromDepartmentId,
                employmentEvent.ToDepartmentId,
                employmentEvent.FromPositionId,
                employmentEvent.ToPositionId,
                employmentEvent.FromSalary,
                employmentEvent.ToSalary,
                employmentEvent.Note,
                employmentEvent.DutyEndDate,
                employmentEvent.IsImported,
                employment.EmployeeId,
                employment.LegalEntityId,
                EmploymentDepartmentId = employment.DepartmentId,
                person.FullName,
            };

        if (branch is not null)
        {
            // Veb-jurnaldagi shart: bo'limdan YOKI bo'limga; hodisada bo'lim
            // umuman yo'q bo'lsa — munosabatning bo'limi.
            rows = rows.Where(row =>
                (row.ToDepartmentId != null && branch.Contains(row.ToDepartmentId.Value))
                || (row.FromDepartmentId != null && branch.Contains(row.FromDepartmentId.Value))
                || (row.FromDepartmentId == null
                    && row.ToDepartmentId == null
                    && row.EmploymentDepartmentId != null
                    && branch.Contains(row.EmploymentDepartmentId.Value)));
        }

        // Turlar bo'yicha son — tur filtrisiz: «shu davrda nima qancha bo'ldi» ko'rinib tursin.
        var byType = await rows
            .GroupBy(row => row.Type)
            .Select(group => new { Type = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Type, item => item.Count, cancellationToken)
            .ConfigureAwait(false);

        if (wanted.Count > 0)
        {
            rows = rows.Where(row => wanted.Contains(row.Type));
        }

        var total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await rows
            .OrderByDescending(row => row.EffectiveDate)
            .ThenByDescending(row => row.Id)
            .Skip((pageNo - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var legalEntities = await t.LegalEntitiesAsync(cancellationToken).ConfigureAwait(false);
        var departments = await t.DepartmentsAsync(cancellationToken).ConfigureAwait(false);
        var positions = await t.PositionsAsync(cancellationToken).ConfigureAwait(false);

        return t.Json(
            new
            {
                from = McpToolContext.Iso(start),
                to = McpToolContext.Iso(end),
                salaryVisible = canSalary,
                extraPayVisible = canExtraPay,
                countsByType = Enum.GetValues<EmploymentEventType>()
                    .ToDictionary(type => type.ToString(), type => byType.GetValueOrDefault(type)),
                total,
                page = pageNo,
                pageSize = size,
                pageCount = (total + size - 1) / size,
                items = items.Select(row => new
                {
                    date = McpToolContext.Iso(row.EffectiveDate),
                    type = row.Type,
                    employeeId = row.EmployeeId,
                    employee = row.FullName.Full,
                    legalEntity = legalEntities.Name(row.LegalEntityId),
                    fromDepartment = departments.Name(row.FromDepartmentId),
                    toDepartment = departments.Name(row.ToDepartmentId),

                    // Hodisada bo'lim yo'q bo'lsa — munosabatning HOZIRGI bo'limi (kontekst uchun).
                    employmentDepartment = row.FromDepartmentId is null && row.ToDepartmentId is null
                        ? departments.Name(row.EmploymentDepartmentId)
                        : null,
                    fromPosition = positions.Name(row.FromPositionId),
                    toPosition = positions.Name(row.ToPositionId),
                    fromSalary = McpLookups.Money(row.FromSalary, canSalary),
                    toSalary = McpLookups.Money(
                        row.ToSalary,
                        row.Type == EmploymentEventType.AdditionalDuty ? canExtraPay : canSalary),
                    orderNo = row.OrderNo,
                    orderDate = McpToolContext.Iso(row.OrderDate),
                    note = row.Note,
                    dutyEndDate = McpToolContext.Iso(row.DutyEndDate),
                    imported = row.IsImported ? true : (bool?)null,
                }),
            },
            items.Count);
    }

    // ---------------------------------------------------------------------
    //  turnover
    // ---------------------------------------------------------------------

    [McpServerTool(Name = "turnover", Title = "Kadrlar oqimi",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Kadrlar oqimi davr bo'yicha: har oy kelgan va ketganlar (sof o'zgarish, oy boshi/oxiridagi shtat), "
        + "yuridik shaxs / bo'lim / lavozim kesimida kelgan-ketgan va oqim foizi, ketish sabablari (ixtiyoriy ulushi bilan), "
        + "ketganlarning o'rtacha ishlagan muddati, erta ketganlar (sinov muddatida yoki 90 kun ichida), davr boshi va oxiridagi shtat. "
        + "«Hisobotlar» sahifasidagi kadrlar oqimi bilan bir xil ta'riflar. "
        + "Turnover: monthly hires vs terminations, breakdown, termination reasons, voluntary share, tenure of leavers, early leavers.")]
    public async Task<string> Turnover(
        [Description("Davr boshi, YYYY-MM-DD. Bo'sh — joriy oy bilan birga so'nggi 12 kalendar oy (11 oy oldingi oyning 1-sanasi).")] string? from = null,
        [Description("Davr oxiri, YYYY-MM-DD. Bo'sh — bugun. Oraliq ko'pi bilan 366 kun.")] string? to = null,
        [Description("Kesim: legal_entity (sukut) — yuridik shaxs, department — bo'lim, position — lavozim.")] string? groupBy = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.EmploymentView, Area);

        var end = McpToolContext.Date(to, McpToolContext.Today, "to");
        var defaultStart = new DateOnly(end.Year, end.Month, 1).AddMonths(-11);
        var (start, finish) = McpToolContext.Range(
            string.IsNullOrWhiteSpace(from) ? McpToolContext.Iso(defaultStart) : from,
            McpToolContext.Iso(end));
        var grouping = Choice(groupBy, "groupBy", GroupByLegalEntity, GroupByValues);

        var db = t.Db;

        // Davr bilan kesishgan HAMMA munosabat — bitta so'rov. Undan kelgan/ketgan
        // ham, istalgan sanadagi shtat ham hisoblanadi (hisobotdagi HeadcountAtAsync
        // ta'rifi: HireDate <= sana <= TerminationDate, odam bo'yicha, o'chirilgan
        // xodim shtatga kirmaydi; kelgan/ketgan esa munosabat bo'yicha).
        var rows = await db.Employments
            .Where(employment => employment.HireDate <= finish
                && (employment.TerminationDate == null || employment.TerminationDate >= start))
            .Select(employment => new TurnoverRow(
                employment.Id,
                employment.EmployeeId,
                employment.LegalEntityId,
                employment.DepartmentId,
                employment.PositionId,
                employment.HireDate,
                employment.TerminationDate,
                employment.TerminationReasonId,
                db.Employees.Any(person => person.Id == employment.EmployeeId)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Qayta kelganlar: shu davrdagi qabuldan OLDIN tugagan boshqa munosabati bor odam.
        // O'rindoshlik (bir vaqtda ikkinchi firma) bu yerga tushmaydi.
        var rehires = await db.Employments
            .Where(employment => employment.HireDate >= start && employment.HireDate <= finish)
            .CountAsync(
                employment => db.Employments.Any(other =>
                    other.EmployeeId == employment.EmployeeId
                    && other.Id != employment.Id
                    && other.TerminationDate != null
                    && other.TerminationDate <= employment.HireDate),
                cancellationToken)
            .ConfigureAwait(false);

        var leavers = rows
            .Where(row => row.TerminationDate is { } left && left >= start && left <= finish)
            .ToList();

        var leaverIds = leavers.Select(row => row.Id).ToList();

        var probations = leaverIds.Count == 0
            ? []
            : await db.Probations
                .Where(probation => leaverIds.Contains(probation.EmploymentId))
                .GroupBy(probation => probation.EmploymentId)
                .Select(group => new
                {
                    EmploymentId = group.Key,
                    EndDate = group.Max(probation => probation.EndDate),
                    NotPassed = group.Count(probation =>
                        probation.Status == ProbationStatus.Failed || probation.Status == ProbationStatus.LeftEarly),
                })
                .ToDictionaryAsync(item => item.EmploymentId, cancellationToken)
                .ConfigureAwait(false);

        var reasons = await TerminationReasonDetailsAsync(t, cancellationToken).ConfigureAwait(false);

        // ── Oylar ─────────────────────────────────────────────────────────
        var months = new List<object>();

        for (var cursor = new DateOnly(start.Year, start.Month, 1); cursor <= finish; cursor = cursor.AddMonths(1))
        {
            var monthStart = cursor < start ? start : cursor;
            var monthEnd = cursor.AddMonths(1).AddDays(-1) < finish ? cursor.AddMonths(1).AddDays(-1) : finish;
            var hired = rows.Count(row => row.HireDate >= monthStart && row.HireDate <= monthEnd);
            var left = rows.Count(row => row.TerminationDate >= monthStart && row.TerminationDate <= monthEnd);

            months.Add(new
            {
                month = cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                from = McpToolContext.Iso(monthStart),
                to = McpToolContext.Iso(monthEnd),
                hires = hired,
                terminations = left,
                net = hired - left,
                headcountStart = Headcount(rows, monthStart),
                headcountEnd = Headcount(rows, monthEnd),
            });
        }

        // ── Jami ──────────────────────────────────────────────────────────
        var hires = rows.Count(row => row.HireDate >= start && row.HireDate <= finish);
        var headcountStart = Headcount(rows, start);
        var headcountEnd = Headcount(rows, finish);

        bool? Voluntary(TurnoverRow row) =>
            row.TerminationReasonId is { } id && reasons.TryGetValue(id, out var reason) ? reason.IsVoluntary : null;

        var voluntary = leavers.Count(row => Voluntary(row) == true);
        var involuntary = leavers.Count(row => Voluntary(row) == false);

        // ── Kesim ─────────────────────────────────────────────────────────
        var names = grouping switch
        {
            GroupByDepartment => await t.DepartmentsAsync(cancellationToken).ConfigureAwait(false),
            GroupByPosition => await t.PositionsAsync(cancellationToken).ConfigureAwait(false),
            _ => await t.LegalEntitiesAsync(cancellationToken).ConfigureAwait(false),
        };

        Guid? KeyOf(TurnoverRow row) => grouping switch
        {
            GroupByDepartment => row.DepartmentId,
            GroupByPosition => row.PositionId,
            _ => row.LegalEntityId,
        };

        var groups = rows
            .GroupBy(KeyOf)
            .Select(group =>
            {
                var groupRows = group.ToList();
                var groupLeft = groupRows.Count(row => row.TerminationDate >= start && row.TerminationDate <= finish);
                var groupStart = Headcount(groupRows, start);
                var groupEnd = Headcount(groupRows, finish);

                return new
                {
                    id = group.Key,
                    name = group.Key is { } key ? names.GetValueOrDefault(key, "?") : "(ko'rsatilmagan)",
                    hires = groupRows.Count(row => row.HireDate >= start && row.HireDate <= finish),
                    terminations = groupLeft,
                    voluntaryTerminations = groupRows.Count(row =>
                        row.TerminationDate >= start && row.TerminationDate <= finish && Voluntary(row) == true),
                    headcountStart = groupStart,
                    headcountEnd = groupEnd,
                    turnoverRatePercent = Rate(groupLeft, groupStart, groupEnd),
                };
            })
            .Where(group => group.hires > 0 || group.terminations > 0 || group.headcountStart > 0 || group.headcountEnd > 0)
            .OrderByDescending(group => group.terminations)
            .ThenByDescending(group => group.headcountEnd)
            .ThenBy(group => group.name, StringComparer.Ordinal)
            .ToList();

        // ── Sabablar ──────────────────────────────────────────────────────
        var reasonRows = leavers
            .GroupBy(row => row.TerminationReasonId)
            .Select(group =>
            {
                var known = group.Key is { } id && reasons.ContainsKey(id);
                return new
                {
                    reason = group.Key is null ? "Ko'rsatilmagan" : known ? reasons[group.Key.Value].Name : "Noma'lum sabab",
                    isVoluntary = known ? reasons[group.Key!.Value].IsVoluntary : (bool?)null,
                    count = group.Count(),
                    percent = Percent(group.Count(), leavers.Count),
                };
            })
            .OrderByDescending(item => item.count)
            .ThenBy(item => item.reason, StringComparer.Ordinal)
            .ToList();

        // ── Ishlagan muddat va erta ketganlar ─────────────────────────────
        var tenures = leavers
            .Select(row => row.TerminationDate!.Value.DayNumber - row.HireDate.DayNumber)
            .Order()
            .ToList();

        var withinProbation = leavers.Count(row => probations.TryGetValue(row.Id, out var probation)
            && (row.TerminationDate <= probation.EndDate || probation.NotPassed > 0));
        var within90Days = leavers.Count(row => row.TerminationDate!.Value.DayNumber - row.HireDate.DayNumber <= EarlyLeaverDays);
        var early = leavers.Count(row =>
            row.TerminationDate!.Value.DayNumber - row.HireDate.DayNumber <= EarlyLeaverDays
            || (probations.TryGetValue(row.Id, out var probation)
                && (row.TerminationDate <= probation.EndDate || probation.NotPassed > 0)));

        return t.Json(
            new
            {
                from = McpToolContext.Iso(start),
                to = McpToolContext.Iso(finish),
                groupBy = grouping,
                definitions = new
                {
                    hires = "Davrda boshlangan mehnat munosabatlari soni (munosabat bo'yicha: qayta qabul va ikkinchi firmaga qabul ham alohida qabul).",
                    terminations = "Davrda tugagan mehnat munosabatlari soni (ishdan bo'shatish sanasi bo'yicha).",
                    headcount = "Shu sanada ishlayotgan xodimlar, odam bo'yicha (bo'shatilgan kunning o'zida hali ishlagan hisoblanadi).",
                    turnoverRatePercent = "ketganlar / ((davr boshidagi shtat + oxiridagi shtat) / 2) × 100 — butun davr uchun.",
                    voluntarySharePercent = "Ixtiyoriy ketganlar / sababi ma'lum ketganlar × 100 (termination_reasons.is_voluntary).",
                    earlyLeaver = "Ishlagan muddati 90 kundan oshmagan YOKI sinov muddati tugashidan oldin ketgan (sinov natijasi Failed/LeftEarly ham).",
                    rehires = "Davrdagi qabullardan — avval boshqa munosabati tugagan odamlar (qaytib kelganlar).",
                    groups = "Kesim mehnat munosabatining hozirgi (ketganlarda — oxirgi) yuridik shaxsi/bo'limi/lavozimi bo'yicha.",
                },
                totals = new
                {
                    hires,
                    rehires,
                    terminations = leavers.Count,
                    net = hires - leavers.Count,
                    headcountStart,
                    headcountEnd,
                    turnoverRatePercent = Rate(leavers.Count, headcountStart, headcountEnd),
                    voluntary,
                    involuntary,
                    reasonUnknown = leavers.Count - voluntary - involuntary,
                    voluntarySharePercent = Percent(voluntary, voluntary + involuntary),
                },
                months,
                groups = groups.Take(MaxGroups),
                groupsTotal = groups.Count,
                groupsTruncated = groups.Count > MaxGroups ? true : (bool?)null,
                reasons = reasonRows,
                leaverTenure = new
                {
                    leavers = tenures.Count,
                    averageDays = tenures.Count == 0 ? (double?)null : Math.Round(tenures.Average(), 1),
                    averageMonths = tenures.Count == 0 ? (double?)null : Math.Round(tenures.Average() / 30.44, 1),
                    medianDays = tenures.Count == 0
                        ? (double?)null
                        : (tenures[(tenures.Count - 1) / 2] + tenures[tenures.Count / 2]) / 2.0,
                    buckets = new[]
                    {
                        new { bucket = "0–30 kun", count = tenures.Count(days => days <= 30) },
                        new { bucket = "31–90 kun", count = tenures.Count(days => days is > 30 and <= 90) },
                        new { bucket = "91–180 kun", count = tenures.Count(days => days is > 90 and <= 180) },
                        new { bucket = "181–365 kun", count = tenures.Count(days => days is > 180 and <= 365) },
                        new { bucket = "1–3 yil", count = tenures.Count(days => days is > 365 and <= 1095) },
                        new { bucket = "3 yildan ortiq", count = tenures.Count(days => days > 1095) },
                    },
                },
                earlyLeavers = new
                {
                    count = early,
                    sharePercent = Percent(early, leavers.Count),
                    withinProbation,
                    within90Days,
                },
            },
            months.Count);
    }

    // ---------------------------------------------------------------------
    //  probations
    // ---------------------------------------------------------------------

    [McpServerTool(Name = "probations", Title = "Sinov muddatlari",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Sinov muddatlari: davom etayotganlar (sukut), yaqin kunlarda tugaydiganlar, muddati o'tib ketgan "
        + "lekin yakunlanmaganlar yoki davrda yakunlanganlar (natija, xulosa, baholovchi). Har qatorda xodim, yuridik shaxs, "
        + "bo'lim, lavozim, boshlanish/tugash sanasi, qolgan kunlar. Umumiy sonlar ham beriladi. "
        + "Probations: in progress, ending soon, overdue, or resolved in a period.")]
    public async Task<string> Probations(
        [Description("Ko'rinish: in_progress (sukut) — davom etayotgan barcha sinovlar; ending — `days` kun ichida tugaydiganlar; "
            + "overdue — tugash sanasi o'tgan, lekin hali yakunlanmagan; results — `from`–`to` oralig'ida yakunlanganlar.")] string? view = null,
        [Description("Faqat ending uchun: bugundan boshlab necha kun ichida tugaydi (sukut 14, 1 dan 180 gacha).")] int? days = null,
        [Description("Faqat results uchun: yakunlangan sana oralig'ining boshi, YYYY-MM-DD. Bo'sh — so'nggi 30 kun.")] string? from = null,
        [Description("Faqat results uchun: oraliq oxiri, YYYY-MM-DD. Bo'sh — bugun. Ko'pi bilan 366 kun.")] string? to = null,
        [Description("Yuridik shaxs identifikatori (org_structure vositasidan).")] Guid? legalEntityId = null,
        [Description("Bo'lim identifikatori — ichki bo'limlari bilan birga.")] Guid? departmentId = null,
        [Description("Sahifa raqami, 1 dan boshlanadi.")] int? page = null,
        [Description("Sahifadagi qatorlar soni: sukut 50, ko'pi bilan 200.")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.EmploymentView, Area);

        var mode = Choice(view, "view", ViewInProgress, ProbationViews);
        var within = days ?? 14;

        if (within is < 1 or > Probation.MaxDays)
        {
            throw new McpException($"«days» 1 dan {Probation.MaxDays} gacha bo'lishi kerak.");
        }

        var (start, end) = mode == ViewResults ? McpToolContext.Range(from, to) : (McpToolContext.Today, McpToolContext.Today);
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);
        var today = McpToolContext.Today;
        var limit = today.AddDays(within);

        var db = t.Db;
        var branch = await DepartmentBranchAsync(t, departmentId, cancellationToken).ConfigureAwait(false);

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

        // Veb-ro'yxatdagi kabi: sinov → munosabat → xodim (o'chirilganlar tushib qoladi).
        var all =
            from probation in db.Probations
            join employment in employments on probation.EmploymentId equals employment.Id
            join person in db.Employees on employment.EmployeeId equals person.Id
            select new
            {
                probation.Id,
                probation.StartDate,
                probation.EndDate,
                probation.Status,
                probation.ResolvedAt,
                probation.Conclusion,
                probation.ReviewerEmployeeId,
                employment.EmployeeId,
                employment.LegalEntityId,
                employment.DepartmentId,
                employment.PositionId,
                employment.HireDate,
                EmploymentStatus = employment.Status,
                person.FullName,
            };

        var running = all.Where(row => row.Status == ProbationStatus.InProgress);

        var inProgressCount = await running.CountAsync(cancellationToken).ConfigureAwait(false);
        var overdueCount = await running.CountAsync(row => row.EndDate < today, cancellationToken).ConfigureAwait(false);
        var endingCount = await running
            .CountAsync(row => row.EndDate >= today && row.EndDate <= limit, cancellationToken)
            .ConfigureAwait(false);

        // Sinov «davom etmoqda», lekin odam allaqachon ketgan — HR yopmay qoldirgan yozuv,
        // amaliy ish emas. Veb-ro'yxatdagi kabi ro'yxatdan chiqarilmaydi, faqat sanab ko'rsatiladi.
        var leftCount = await running
            .CountAsync(row => row.EmploymentStatus == EmploymentStatus.Terminated, cancellationToken)
            .ConfigureAwait(false);

        var selected = mode switch
        {
            ViewEnding => running.Where(row => row.EndDate >= today && row.EndDate <= limit),
            ViewOverdue => running.Where(row => row.EndDate < today),
            ViewResults => all.Where(row => row.Status != ProbationStatus.InProgress
                && row.ResolvedAt != null && row.ResolvedAt >= start && row.ResolvedAt <= end),
            _ => running,
        };

        Dictionary<string, int>? resultCounts = null;

        if (mode == ViewResults)
        {
            var byStatus = await selected
                .GroupBy(row => row.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            resultCounts = byStatus.ToDictionary(item => item.Status.ToString(), item => item.Count);
        }

        var total = await selected.CountAsync(cancellationToken).ConfigureAwait(false);

        var ordered = mode == ViewResults
            ? selected.OrderByDescending(row => row.ResolvedAt).ThenBy(row => row.FullName.LastName).ThenBy(row => row.Id)
            : selected.OrderBy(row => row.EndDate).ThenBy(row => row.FullName.LastName).ThenBy(row => row.Id);

        var items = await ordered
            .Skip((pageNo - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var legalEntities = await t.LegalEntitiesAsync(cancellationToken).ConfigureAwait(false);
        var departments = await t.DepartmentsAsync(cancellationToken).ConfigureAwait(false);
        var positions = await t.PositionsAsync(cancellationToken).ConfigureAwait(false);
        var reviewers = await t
            .EmployeeNamesAsync(items.Where(row => row.ReviewerEmployeeId != null).Select(row => row.ReviewerEmployeeId!.Value), cancellationToken)
            .ConfigureAwait(false);

        return t.Json(
            new
            {
                today = McpToolContext.Iso(today),
                view = mode,
                from = mode == ViewResults ? McpToolContext.Iso(start) : null,
                to = mode == ViewResults ? McpToolContext.Iso(end) : null,
                summary = new
                {
                    inProgress = inProgressCount,
                    overdue = overdueCount,
                    endingWithinDays = within,
                    ending = endingCount,
                    inProgressButEmploymentTerminated = leftCount,
                },
                resultCounts,
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
                    startDate = McpToolContext.Iso(row.StartDate),
                    endDate = McpToolContext.Iso(row.EndDate),
                    daysLeft = row.Status == ProbationStatus.InProgress ? row.EndDate.DayNumber - today.DayNumber : (int?)null,
                    status = row.Status,
                    employmentStatus = row.EmploymentStatus,
                    resolvedAt = McpToolContext.Iso(row.ResolvedAt),
                    conclusion = row.Conclusion,
                    reviewer = reviewers.Name(row.ReviewerEmployeeId),
                }),
            },
            items.Count);
    }

    // ---------------------------------------------------------------------
    //  Yordamchilar (HrRecordTools ham ishlatadi)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Bo'lim va uning BUTUN shoxi (ichki bo'limlar) — veb-ro'yxatlardagi bo'lim
    /// filtri bilan bir xil (<c>DepartmentRepository.GetDescendantIdsAsync</c>:
    /// o'chirilmagan bolalar). Xom SQL emas — daraxt kichik, xotirada yuriladi.
    /// </summary>
    /// <returns><c>null</c> — filtr berilmagan.</returns>
    internal static async Task<List<Guid>?> DepartmentBranchAsync(
        McpToolContext t,
        Guid? departmentId,
        CancellationToken cancellationToken)
    {
        if (departmentId is not { } root)
        {
            return null;
        }

        var tree = await t.Db.Departments
            .Where(department => department.ParentId != null)
            .Select(department => new { department.Id, ParentId = department.ParentId!.Value })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var children = tree.ToLookup(item => item.ParentId, item => item.Id);
        var branch = new HashSet<Guid> { root };
        var queue = new Queue<Guid>([root]);

        while (queue.TryDequeue(out var current))
        {
            foreach (var child in children[current])
            {
                // Halqa bo'lsa ham to'xtaydi: har bo'lim bir marta.
                if (branch.Add(child))
                {
                    queue.Enqueue(child);
                }
            }
        }

        return [.. branch];
    }

    /// <summary>Ishdan ketish sabablari — nom va «ixtiyoriymi» (o'chirilganlari ham).</summary>
    internal static async Task<Dictionary<Guid, (string Name, bool IsVoluntary)>> TerminationReasonDetailsAsync(
        McpToolContext t,
        CancellationToken cancellationToken)
    {
        var rows = await t.Db.TerminationReasons.IgnoreQueryFilters()
            .Select(reason => new { reason.Id, reason.Name, reason.IsVoluntary })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(row => row.Id, row => (row.Name, row.IsVoluntary));
    }

    /// <summary>Enum qiymatlari ro'yxatini o'qiydi (katta-kichik harf farqsiz); noto'g'ri qiymat — tushunarli xato.</summary>
    internal static List<TEnum> ParseEnums<TEnum>(string[]? values, string name)
        where TEnum : struct, Enum
    {
        var result = new List<TEnum>();

        foreach (var value in values ?? [])
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result.Add(ParseEnum<TEnum>(value, name));
        }

        return [.. result.Distinct()];
    }

    /// <summary>Bitta enum qiymati (katta-kichik harf farqsiz).</summary>
    internal static TEnum ParseEnum<TEnum>(string value, string name)
        where TEnum : struct, Enum
    {
        var text = value.Trim();

        // Raqam qabul qilinmaydi — faqat nom: «5» qaysi tur ekani AI uchun ham, jurnal uchun ham noaniq.
        if (text.Length > 0 && !char.IsDigit(text[0]) && text[0] != '-'
            && Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new McpException(
            $"«{name}» qiymati noto'g'ri: «{Short(text)}». Mumkin: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }

    /// <summary>Ruxsat etilgan matnli qiymatlardan biri (katta-kichik harf farqsiz); bo'sh — <paramref name="fallback"/>.</summary>
    internal static string Choice(string? value, string name, string fallback, string[] allowed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim();

        return allowed.FirstOrDefault(item => string.Equals(item, text, StringComparison.OrdinalIgnoreCase))
            ?? throw new McpException($"«{name}» qiymati noto'g'ri: «{Short(text)}». Mumkin: {string.Join(", ", allowed)}.");
    }

    /// <summary>Foiz, bir kasr xona; maxraj nol — <c>null</c>.</summary>
    internal static double? Percent(int part, int whole) =>
        whole == 0 ? null : Math.Round(100.0 * part / whole, 1, MidpointRounding.AwayFromZero);

    /// <summary>Oqim foizi: ketganlar / o'rtacha shtat (hisobot izohidagi standart ta'rif).</summary>
    private static double? Rate(int left, int headcountStart, int headcountEnd)
    {
        var average = (headcountStart + headcountEnd) / 2.0;
        return average <= 0 ? null : Math.Round(100.0 * left / average, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>Sanadagi shtat — odam bo'yicha, o'chirilgan xodimsiz.</summary>
    private static int Headcount(List<TurnoverRow> rows, DateOnly date) =>
        rows.Where(row => row.EmployeeExists
                && row.HireDate <= date
                && (row.TerminationDate == null || row.TerminationDate >= date))
            .Select(row => row.EmployeeId)
            .Distinct()
            .Count();

    /// <summary>Xatoda foydalanuvchi matnini qisqa qaytarish.</summary>
    private static string Short(string text) => text.Length <= 40 ? text : text[..40] + "…";

    /// <summary>Kadrlar oqimi uchun munosabatning eng kam ustunlari.</summary>
    private sealed record TurnoverRow(
        Guid Id,
        Guid EmployeeId,
        Guid LegalEntityId,
        Guid? DepartmentId,
        Guid? PositionId,
        DateOnly HireDate,
        DateOnly? TerminationDate,
        Guid? TerminationReasonId,
        bool EmployeeExists);
}
