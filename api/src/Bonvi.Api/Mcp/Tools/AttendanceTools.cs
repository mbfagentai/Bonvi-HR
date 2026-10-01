using System.ComponentModel;
using System.Globalization;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Bonvi.Api.Mcp.Tools;

/// <summary>Davomat — kunlik holat, davr yig'indisi va yo'qlik so'rovlari (faqat o'qish).</summary>
/// <remarks>
/// <para>
/// Manba — hisoblangan kunlar (<c>attendance_days</c>), tabel va «Yig'indi» ekranlari
/// bilan BIR XIL: kechikish = <c>late_minutes &gt; 0</c> (yon berishdan keyin), «kelmagan» =
/// <c>Absent</c>, «sababli» = <c>Excused</c> (kelmagan kunga tasdiqlangan so'rov).
/// O'tishlar jurnali (<c>attendance_passes</c>) o'qilmaydi — u yerda terminal surati va
/// xom hodisa bor, kunlik raqamlar uchun esa kerak emas.
/// </para>
/// <para>
/// ⚠️ Dam olish kuni va hali boshlanmagan smena uchun qator odatda YARATILMAYDI
/// (<c>AttendanceDayStatus.DayOff</c> / <c>Planned</c> izohlari): ular «yozuvsiz» bo'lib
/// sanaladi, «kelmagan» bo'lib emas.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class AttendanceTools(McpToolContext t)
{
    private const string Area = "Davomat";

    private const string Escape = "\\";

    private const string NoDepartment = "(bo'limsiz)";

    /// <summary>Yig'indi oralig'ining eng katta uzunligi — tabel bilan bir xil (uch oy).</summary>
    private const int MaxSummaryDays = 92;

    // ─────────────────────────────────────────────────────────────────
    //  attendance_day
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "attendance_day", Title = "Kunlik davomat",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Bitta kun davomati bo'limlar kesimida: kelgan, kechikkan, kelmagan, sababli, dam olish kunida ishlagan, "
        + "chiqishi/kirishi yozilmagan, jadvalsiz, yozuvsiz (dam olish kuni yoki smena hali boshlanmagan); hamda ism-sharif "
        + "ro'yxatlari — kechikkanlar, kelmaganlar, sabablilar (yo'qlik turi bilan), chiqishi yozilmaganlar (birinchi kirish, "
        + "oxirgi chiqish, kechikish daqiqasi). Sukut: bugun (Toshkent). "
        + "One day's attendance per department with name lists (late, absent, excused, missing exit).")]
    public async Task<string> AttendanceDay(
        [Description("Sana YYYY-MM-DD (sukut: bugun).")] string? date = null,
        [Description("Bo'lim: identifikator yoki nomi (ichki bo'limlari bilan).")] string? department = null,
        [Description("Yuridik shaxs (firma): identifikator yoki nomi.")] string? legalEntity = null,
        [Description("Har bir ism ro'yxatining eng ko'p uzunligi (sukut 100, ko'pi bilan 200).")] int? listLimit = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.AttendanceView, Area);

        var db = t.Db;
        var today = McpToolContext.Today;
        var day = McpToolContext.Date(date, today, "date");
        var limit = Math.Clamp(listLimit ?? 100, 1, McpToolContext.MaxPageSize);

        var departments = await DepartmentsAsync(cancellationToken).ConfigureAwait(false);
        var legalEntities = await t.LegalEntitiesAsync(cancellationToken).ConfigureAwait(false);
        var positions = await t.PositionsAsync(cancellationToken).ConfigureAwait(false);

        var departmentFilter = ResolveDepartments(departments, department);
        var legalEntityFilter = ResolveLegalEntities(legalEntities, legalEntity);

        var query = db.AttendanceDays.Where(item => item.LocalDate == day);
        query = ApplyOrgFilter(query, departmentFilter?.Ids, legalEntityFilter?.Ids);

        var rows = await query
            .Select(item => new DayRow(
                item.EmployeeId,
                item.DepartmentId,
                item.PositionId,
                item.Status,
                item.LateMinutes,
                item.EarlyLeaveMinutes,
                item.WorkedMinutes,
                item.PlanStartAt,
                item.FirstInAt,
                item.LastOutAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Yozuvsizlar: shu sanada ishlaydigan (asosiy munosabati bor), lekin kun qatori yo'q odamlar.
        var primaries = await PrimaryEmploymentsAsync(day, cancellationToken).ConfigureAwait(false);
        var withRow = rows.Select(row => row.EmployeeId).ToHashSet();

        var noRecord = primaries.Values
            .Where(employment => !withRow.Contains(employment.EmployeeId)
                && (departmentFilter is null || (employment.DepartmentId is { } id && departmentFilter.Ids.Contains(id)))
                && (legalEntityFilter is null || legalEntityFilter.Ids.Contains(employment.LegalEntityId)))
            .ToList();

        var tallies = new Dictionary<Guid, DayTally>();
        var unassigned = new DayTally();

        DayTally TallyOf(Guid? departmentId)
        {
            if (departmentId is not { } key)
            {
                return unassigned;
            }

            if (!tallies.TryGetValue(key, out var tally))
            {
                tally = new DayTally();
                tallies[key] = tally;
            }

            return tally;
        }

        foreach (var row in rows)
        {
            TallyOf(row.DepartmentId).Add(row.Status, row.LateMinutes > 0);
        }

        foreach (var employment in noRecord)
        {
            TallyOf(employment.DepartmentId).AddNoRecord();
        }

        var total = new DayTally();

        foreach (var tally in tallies.Values.Append(unassigned))
        {
            total.Merge(tally);
        }

        string DepartmentName(Guid? id) => id is { } key ? departments.GetValueOrDefault(key)?.Name ?? "?" : NoDepartment;

        var byDepartment = tallies
            .Select(pair => (Name: DepartmentName(pair.Key), Tally: pair.Value))
            .Append((Name: NoDepartment, Tally: unassigned))
            .Where(item => item.Tally.Employees > 0)
            .OrderByDescending(item => item.Tally.Employees)
            .ThenBy(item => item.Name, StringComparer.InvariantCulture)
            .Select(item => item.Tally.ToOutput(item.Name, compact: true))
            .ToList();

        // Ro'yxatlar chegarasi: kechikkanlar — eng ko'p kechikkanidan, qolganlari — bo'lim bo'yicha.
        List<DayRow> Pick(IEnumerable<DayRow> list) =>
            [.. list.OrderBy(row => DepartmentName(row.DepartmentId), StringComparer.InvariantCulture).Take(limit)];

        var late = rows.Where(row => row.LateMinutes > 0).OrderByDescending(row => row.LateMinutes).ToList();
        var absent = rows.Where(row => row.Status == AttendanceDayStatus.Absent).ToList();
        var excused = rows.Where(row => row.Status == AttendanceDayStatus.Excused).ToList();
        var missingExit = rows.Where(row => row.Status == AttendanceDayStatus.MissingExit).ToList();
        var notEmployed = rows.Where(row => row.Status == AttendanceDayStatus.NotEmployed).ToList();

        var latePick = late.Take(limit).ToList();
        var absentPick = Pick(absent);
        var excusedPick = Pick(excused);
        var missingExitPick = Pick(missingExit);
        var notEmployedPick = Pick(notEmployed);

        var listed = latePick
            .Concat(absentPick)
            .Concat(excusedPick)
            .Concat(missingExitPick)
            .Concat(notEmployedPick)
            .Select(row => row.EmployeeId);

        var names = await t.EmployeeNamesAsync(listed, cancellationToken).ConfigureAwait(false);

        // Sababli kunning turi — kun qatorida saqlanmaydi, tasdiqlangan so'rovdan olinadi.
        var excusedIds = excusedPick.Select(row => row.EmployeeId).ToList();
        var excuses = excusedIds.Count == 0
            ? []
            : await db.AbsenceRequests
                .Where(request => request.Status == AbsenceRequestStatus.Approved
                    && request.FromDate <= day
                    && request.ToDate >= day
                    && excusedIds.Contains(request.EmployeeId))
                .Select(request => new { request.EmployeeId, request.AbsenceTypeId, request.FromDate, request.ToDate })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var absenceTypes = excuses.Count == 0
            ? []
            : await AbsenceTypeNamesAsync(cancellationToken).ConfigureAwait(false);

        object Person(DayRow row) => new
        {
            employeeId = row.EmployeeId,
            name = names.Name(row.EmployeeId),
            department = DepartmentName(row.DepartmentId),
            position = positions.Name(row.PositionId),
            planStart = McpToolContext.Local(row.PlanStartAt),
            firstIn = McpToolContext.Local(row.FirstInAt),
            lastOut = McpToolContext.Local(row.LastOutAt),
            lateMinutes = row.LateMinutes > 0 ? row.LateMinutes : (int?)null,
            earlyLeaveMinutes = row.EarlyLeaveMinutes > 0 ? row.EarlyLeaveMinutes : (int?)null,
        };

        IEnumerable<object> Names(List<DayRow> picked) => picked
            .OrderBy(row => DepartmentName(row.DepartmentId), StringComparer.InvariantCulture)
            .ThenBy(row => names.Name(row.EmployeeId), StringComparer.InvariantCulture)
            .Select(Person);

        var notes = new List<string>
        {
            "late — kechikish daqiqasi > 0 (yon berishdan keyin); u kelgan/chiqishi yozilmagan bilan ustma-ust tushishi mumkin.",
            "noRecord — shu sanada ishlaydigan, lekin kun qatori yo'q: jadval bo'yicha dam olish kuni yoki smena hali boshlanmagan.",
            "byDepartment qatorlarida nolga teng sanoqlar ko'rsatilmaydi (yo'q maydon = 0).",
        };

        if (day > today)
        {
            notes.Add("Kelajak sana: kunlar hali hisoblanmagan, faqat tasdiqlangan yo'qlik so'rovlari (excused) ko'rinadi.");
        }
        else if (day == today)
        {
            notes.Add("Bugungi raqamlar fon vazifasi bilan taxminan har 15 daqiqada yangilanadi; smenasi boshlangan, lekin kelmaganlar shundan keyin «absent» bo'ladi.");
        }

        if (rows.Count == 0 && day <= today)
        {
            notes.Add("Bu sana uchun davomat hali hisoblanmagan yoki o'tishlar umuman yo'q.");
        }

        return t.Json(
            new
            {
                date = McpToolContext.Iso(day),
                department = departmentFilter?.Names,
                legalEntity = legalEntityFilter?.Names,
                totals = total.ToOutput(null, compact: false),
                byDepartment,
                late = new { total = late.Count, items = latePick.Select(Person) },
                absent = new { total = absent.Count, items = Names(absentPick) },
                excused = new
                {
                    total = excused.Count,
                    items = excusedPick
                        .OrderBy(row => DepartmentName(row.DepartmentId), StringComparer.InvariantCulture)
                        .ThenBy(row => names.Name(row.EmployeeId), StringComparer.InvariantCulture)
                        .Select(row =>
                        {
                            var excuse = excuses.FirstOrDefault(item => item.EmployeeId == row.EmployeeId);

                            return new
                            {
                                employeeId = row.EmployeeId,
                                name = names.Name(row.EmployeeId),
                                department = DepartmentName(row.DepartmentId),
                                position = positions.Name(row.PositionId),
                                absenceType = excuse is null ? null : absenceTypes.Name(excuse.AbsenceTypeId),
                                from = McpToolContext.Iso(excuse?.FromDate),
                                to = McpToolContext.Iso(excuse?.ToDate),
                            };
                        }),
                },
                missingExit = new { total = missingExit.Count, items = Names(missingExitPick) },
                notEmployed = notEmployed.Count == 0 ? null : new { total = notEmployed.Count, items = Names(notEmployedPick) },
                notes,
            },
            rows.Count + noRecord.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    //  attendance_summary
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "attendance_summary", Title = "Davomat yig'indisi",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Davr bo'yicha davomat yig'indisi xodim (sukut) yoki bo'lim kesimida: ishlangan va reja soatlari, kechikishlar soni "
        + "va daqiqasi, erta ketish, ortiqcha ishlangan vaqt, kelmagan kunlar, sababli kunlar, chiqishi/kirishi yozilmagan kunlar. "
        + "Istalgan ko'rsatkich bo'yicha saralanadi (masalan eng ko'p kechikkanlar). Sukut: joriy oy, ko'pi bilan 92 kun. "
        + "Attendance totals per employee or department over a period, sortable by any metric.")]
    public async Task<string> AttendanceSummary(
        [Description("Davr boshi YYYY-MM-DD (sukut: oyning 1-kuni).")] string? from = null,
        [Description("Davr oxiri YYYY-MM-DD (sukut: bugun).")] string? to = null,
        [Description("Kesim: employee (sukut) yoki department.")] string? groupBy = null,
        [Description("Bo'lim: identifikator yoki nomi (ichki bo'limlari bilan).")] string? department = null,
        [Description("Yuridik shaxs (firma): identifikator yoki nomi.")] string? legalEntity = null,
        [Description("Xodim: identifikator yoki ism-familiyasi.")] string? employee = null,
        [Description("Saralash: name (sukut), worked, planned, lateCount, lateMinutes, earlyLeave, overtime, absent, excused, missing.")] string? sort = null,
        [Description("Sahifa raqami (1 dan) — xodim kesimida.")] int? page = null,
        [Description("Sahifa hajmi (sukut 50, ko'pi bilan 200).")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.AttendanceView, Area);

        var db = t.Db;
        var today = McpToolContext.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var (start, end) = McpToolContext.Range(
            from ?? (to is null ? McpToolContext.Iso(monthStart) : null),
            to,
            defaultDays: 31,
            maxDays: MaxSummaryDays);

        var byDepartment = (groupBy?.Trim().ToUpperInvariant()) switch
        {
            null or "" or "EMPLOYEE" => false,
            "DEPARTMENT" => true,
            _ => throw new McpException("«groupBy» qiymati: employee yoki department."),
        };

        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);

        var departments = await DepartmentsAsync(cancellationToken).ConfigureAwait(false);
        var legalEntities = await t.LegalEntitiesAsync(cancellationToken).ConfigureAwait(false);

        var departmentFilter = ResolveDepartments(departments, department);
        var legalEntityFilter = ResolveLegalEntities(legalEntities, legalEntity);
        var employeeFilter = await ResolveEmployeesAsync(employee, cancellationToken).ConfigureAwait(false);

        var query = db.AttendanceDays.Where(item => item.LocalDate >= start && item.LocalDate <= end);
        query = ApplyOrgFilter(query, departmentFilter?.Ids, legalEntityFilter?.Ids);

        if (employeeFilter is not null)
        {
            var employeeIds = employeeFilter.Keys.ToList();
            query = query.Where(item => employeeIds.Contains(item.EmployeeId));
        }

        // Yig'ish BAZADA; natija qatorlari soni xodimlar (yoki bo'limlar) sonidan oshmaydi.
        var aggregates = await query
            .GroupBy(item => byDepartment ? item.DepartmentId : (Guid?)item.EmployeeId)
            .Select(group => new SummaryAggregate
            {
                Key = group.Key,
                Employees = group.Select(item => item.EmployeeId).Distinct().Count(),
                Records = group.Count(),
                PresentDays = group.Count(item => item.Status == AttendanceDayStatus.Present),
                WorkedMinutes = group.Sum(item => item.WorkedMinutes),
                PlannedMinutes = group.Sum(item => item.PlannedMinutes),
                LateCount = group.Count(item => item.LateMinutes > 0),
                LateMinutes = group.Sum(item => item.LateMinutes),
                EarlyLeaveCount = group.Count(item => item.EarlyLeaveMinutes > 0),
                EarlyLeaveMinutes = group.Sum(item => item.EarlyLeaveMinutes),
                OvertimeMinutes = group.Sum(item => item.OvertimeMinutes),
                AbsentDays = group.Count(item => item.Status == AttendanceDayStatus.Absent),
                ExcusedDays = group.Count(item => item.Status == AttendanceDayStatus.Excused),
                DayOffWorkedDays = group.Count(item => item.Status == AttendanceDayStatus.DayOffWorked),
                MissingExitDays = group.Count(item => item.Status == AttendanceDayStatus.MissingExit),
                MissingEntryDays = group.Count(item => item.Status == AttendanceDayStatus.MissingEntry),
                NoScheduleDays = group.Count(item => item.Status == AttendanceDayStatus.NoSchedule),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = byDepartment
            ? aggregates.ToDictionary(row => row.Key ?? Guid.Empty, row => row.Key is { } id ? departments.GetValueOrDefault(id)?.Name ?? "?" : NoDepartment)
            : await t.EmployeeNamesAsync(aggregates.Select(row => row.Key!.Value), cancellationToken).ConfigureAwait(false);

        string NameOf(SummaryAggregate row) => names.GetValueOrDefault(row.Key ?? Guid.Empty) ?? "?";

        Func<SummaryAggregate, double>? metric = (sort?.Trim().ToUpperInvariant()) switch
        {
            null or "" or "NAME" => null,
            "WORKED" => row => row.WorkedMinutes,
            "PLANNED" => row => row.PlannedMinutes,
            "LATECOUNT" or "LATE" => row => row.LateCount,
            "LATEMINUTES" => row => row.LateMinutes,
            "EARLYLEAVE" => row => row.EarlyLeaveMinutes,
            "OVERTIME" => row => row.OvertimeMinutes,
            "ABSENT" => row => row.AbsentDays,
            "EXCUSED" => row => row.ExcusedDays,
            "MISSING" => row => row.MissingExitDays + row.MissingEntryDays,
            _ => throw new McpException("«sort» qiymati: name, worked, planned, lateCount, lateMinutes, earlyLeave, overtime, absent, excused, missing."),
        };

        var ordered = metric is null
            ? aggregates.OrderBy(NameOf, StringComparer.InvariantCulture)
            : aggregates.OrderByDescending(metric).ThenBy(NameOf, StringComparer.InvariantCulture);

        // Bo'lim kesimi kichik (o'nlab qator) — sahifalanmaydi, faqat chegara.
        var pageRows = byDepartment
            ? ordered.Take(McpToolContext.MaxPageSize).ToList()
            : ordered.Skip((pageNo - 1) * size).Take(size).ToList();

        // Xodimning bo'limi va lavozimi — davrdagi ENG SO'NGGI kun suratidan («Yig'indi» ekranidagidek).
        Dictionary<Guid, (Guid? DepartmentId, Guid? PositionId)> snapshots = [];
        Dictionary<Guid, string> positions = [];

        if (!byDepartment && pageRows.Count > 0)
        {
            var pageIds = pageRows.Select(row => row.Key!.Value).ToList();

            var latest = await query
                .Where(item => pageIds.Contains(item.EmployeeId))
                .Select(item => new { item.EmployeeId, item.LocalDate, item.DepartmentId, item.PositionId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            snapshots = latest
                .GroupBy(item => item.EmployeeId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var last = group.MaxBy(item => item.LocalDate)!;
                        return (last.DepartmentId, last.PositionId);
                    });

            positions = await t.PositionsAsync(cancellationToken).ConfigureAwait(false);
        }

        var items = pageRows.Select(row =>
        {
            var snapshot = !byDepartment && snapshots.TryGetValue(row.Key!.Value, out var found) ? found : default;

            return new
            {
                employeeId = byDepartment ? null : row.Key,
                departmentId = byDepartment ? row.Key : null,
                name = NameOf(row),
                department = byDepartment ? null : snapshot.DepartmentId is { } departmentId ? departments.GetValueOrDefault(departmentId)?.Name : null,
                position = byDepartment ? null : positions.Name(snapshot.PositionId),
                metrics = row.ToOutput(byDepartment),
            };
        }).ToList();

        var totals = new SummaryAggregate
        {
            Employees = byDepartment ? aggregates.Sum(row => row.Employees) : aggregates.Count,
            Records = aggregates.Sum(row => row.Records),
            PresentDays = aggregates.Sum(row => row.PresentDays),
            WorkedMinutes = aggregates.Sum(row => row.WorkedMinutes),
            PlannedMinutes = aggregates.Sum(row => row.PlannedMinutes),
            LateCount = aggregates.Sum(row => row.LateCount),
            LateMinutes = aggregates.Sum(row => row.LateMinutes),
            EarlyLeaveCount = aggregates.Sum(row => row.EarlyLeaveCount),
            EarlyLeaveMinutes = aggregates.Sum(row => row.EarlyLeaveMinutes),
            OvertimeMinutes = aggregates.Sum(row => row.OvertimeMinutes),
            AbsentDays = aggregates.Sum(row => row.AbsentDays),
            ExcusedDays = aggregates.Sum(row => row.ExcusedDays),
            DayOffWorkedDays = aggregates.Sum(row => row.DayOffWorkedDays),
            MissingExitDays = aggregates.Sum(row => row.MissingExitDays),
            MissingEntryDays = aggregates.Sum(row => row.MissingEntryDays),
            NoScheduleDays = aggregates.Sum(row => row.NoScheduleDays),
        };

        var notes = new List<string>
        {
            "Manba — hisoblangan kunlar (tabel bilan bir xil). Kechikish soni — kechikish daqiqasi > 0 bo'lgan kunlar.",
            "absentDays — ish kuni, birorta o'tish yo'q va tasdiqlangan so'rov yo'q; excusedDays — tasdiqlangan yo'qlik so'rovi bilan kelmagan kunlar (reja soati joyida qoladi).",
            "Dam olish kunlari va hali boshlanmagan smenalar yozuvsiz — ular hech qaysi sanoqqa kirmaydi.",
        };

        if (end > today)
        {
            notes.Add("Davr kelajakka cho'zilgan: kelajak kunlardan faqat tasdiqlangan yo'qlik so'rovlari (excused) sanaladi.");
        }

        if (aggregates.Count == 0)
        {
            notes.Add("Bu davr (va filtr) uchun hisoblangan davomat yozuvi yo'q.");
        }

        if (byDepartment)
        {
            notes.Add("Bo'lim — kun qatoridagi SURAT (o'sha kundagi bo'lim); ichki bo'limlar alohida qator bo'lib chiqadi.");
        }

        return t.Json(
            new
            {
                from = McpToolContext.Iso(start),
                to = McpToolContext.Iso(end),
                groupBy = byDepartment ? "department" : "employee",
                department = departmentFilter?.Names,
                legalEntity = legalEntityFilter?.Names,
                employee = employeeFilter?.Values,
                total = aggregates.Count,
                page = byDepartment ? (int?)null : pageNo,
                pageSize = byDepartment ? (int?)null : size,
                totals = totals.ToOutput(true),
                items,
                notes,
            },
            items.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    //  absence_requests
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "absence_requests", Title = "Yo'qlik so'rovlari",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Xodimlarning yo'qlik so'rovlari (ta'til, kasallik, ruxsat va h.k.): xodim, turi, sanalar (va soatlar), izoh, holat, "
        + "qaror izohi, kim va qachon qaror qilgan, yuborilgan vaqt. Davr — so'rov sanalari oraliqqa TEGADIGANLAR (qisman ham). "
        + "Sukut: bugundan 30 kun oldin va 30 kun keyin. Holat va tur bo'yicha sanoq ham beriladi. "
        + "Absence requests overlapping a period, filterable by status, type, department, employee.")]
    public async Task<string> AbsenceRequests(
        [Description("Davr boshi YYYY-MM-DD (sukut: 30 kun oldin).")] string? from = null,
        [Description("Davr oxiri YYYY-MM-DD (sukut: 30 kun keyin).")] string? to = null,
        [Description("Holat: Pending (kutilmoqda), Approved (tasdiqlangan), Rejected (rad etilgan), Cancelled (bekor qilingan).")] string? status = null,
        [Description("Yo'qlik turi: identifikator yoki nomi.")] string? absenceType = null,
        [Description("Bo'lim (xodimning bugungi asosiy bo'limi, ichki bo'limlari bilan): identifikator yoki nomi.")] string? department = null,
        [Description("Yuridik shaxs (firma): identifikator yoki nomi.")] string? legalEntity = null,
        [Description("Xodim: identifikator yoki ism-familiyasi.")] string? employee = null,
        [Description("Saralash: created (sukut, yangilari birinchi) yoki from (boshlanish sanasi bo'yicha).")] string? sort = null,
        [Description("Sahifa raqami (1 dan).")] int? page = null,
        [Description("Sahifa hajmi (sukut 50, ko'pi bilan 200).")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.AttendanceView, Area);

        var db = t.Db;
        var today = McpToolContext.Today;
        var start = McpToolContext.Date(from, today.AddDays(-30), "from");
        var end = McpToolContext.Date(to, today.AddDays(30), "to");
        (start, end) = McpToolContext.Range(McpToolContext.Iso(start), McpToolContext.Iso(end));

        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);

        // Davr QAMROV bo'yicha — «So'rovlar» ekranidagidek: oraliqqa qisman tegadigani ham kiradi.
        var query = db.AbsenceRequests.Where(request => request.ToDate >= start && request.FromDate <= end);

        if (ParseEnum<AbsenceRequestStatus>(status, "status") is { } statusValue)
        {
            query = query.Where(request => request.Status == statusValue);
        }

        var absenceTypes = await AbsenceTypeNamesAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(absenceType))
        {
            var typeIds = MatchNames(absenceTypes, absenceType, "Yo'qlik turi", listNames: true);
            query = query.Where(request => typeIds.Contains(request.AbsenceTypeId));
        }

        var employeeFilter = await ResolveEmployeesAsync(employee, cancellationToken).ConfigureAwait(false);

        if (employeeFilter is not null)
        {
            var employeeIds = employeeFilter.Keys.ToList();
            query = query.Where(request => employeeIds.Contains(request.EmployeeId));
        }

        var departments = await DepartmentsAsync(cancellationToken).ConfigureAwait(false);
        var legalEntities = await t.LegalEntitiesAsync(cancellationToken).ConfigureAwait(false);
        var departmentFilter = ResolveDepartments(departments, department);
        var legalEntityFilter = ResolveLegalEntities(legalEntities, legalEntity);

        // Bo'lim va lavozim — BUGUNGI asosiy munosabatdan (ekrandagidek: odam bugungi bo'limi bilan tanilsin).
        var primaries = await PrimaryEmploymentsAsync(today, cancellationToken).ConfigureAwait(false);

        if (departmentFilter is not null || legalEntityFilter is not null)
        {
            var inScope = primaries.Values
                .Where(employment => (departmentFilter is null || (employment.DepartmentId is { } id && departmentFilter.Ids.Contains(id)))
                    && (legalEntityFilter is null || legalEntityFilter.Ids.Contains(employment.LegalEntityId)))
                .Select(employment => employment.EmployeeId)
                .ToList();

            query = query.Where(request => inScope.Contains(request.EmployeeId));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var byStatus = await query
            .GroupBy(request => request.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byType = await query
            .GroupBy(request => request.AbsenceTypeId)
            .Select(group => new { TypeId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ordered = (sort?.Trim().ToUpperInvariant()) switch
        {
            null or "" or "CREATED" => query.OrderByDescending(request => request.CreatedAt),
            "FROM" => query.OrderBy(request => request.FromDate).ThenBy(request => request.ToDate),
            _ => throw new McpException("«sort» qiymati: created yoki from."),
        };

        var rows = await ordered
            .ThenByDescending(request => request.Id)
            .Skip((pageNo - 1) * size)
            .Take(size)
            .Select(request => new
            {
                request.Id,
                request.EmployeeId,
                request.AbsenceTypeId,
                request.FromDate,
                request.ToDate,
                request.FromTime,
                request.ToTime,
                request.Note,
                request.Status,
                request.DecisionNote,
                request.DecidedBy,
                request.DecidedAt,
                request.CreatedAt,
                SubmittedByEmployee = request.SubmittedByAccountId != null,
                CancelledByEmployee = request.CancelledByAccountId != null,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = await t.EmployeeNamesAsync(rows.Select(row => row.EmployeeId), cancellationToken).ConfigureAwait(false);
        var deciders = await t.UserNamesAsync(rows.Select(row => row.DecidedBy).OfType<Guid>(), cancellationToken).ConfigureAwait(false);
        var positions = rows.Count == 0 ? [] : await t.PositionsAsync(cancellationToken).ConfigureAwait(false);

        var items = rows.Select(row =>
        {
            var employment = primaries.GetValueOrDefault(row.EmployeeId);

            return new
            {
                id = row.Id,
                employeeId = row.EmployeeId,
                employee = names.Name(row.EmployeeId),
                department = employment?.DepartmentId is { } departmentId ? departments.GetValueOrDefault(departmentId)?.Name : null,
                position = positions.Name(employment?.PositionId),
                absenceType = absenceTypes.Name(row.AbsenceTypeId),
                fromDate = McpToolContext.Iso(row.FromDate),
                toDate = McpToolContext.Iso(row.ToDate),
                fromTime = row.FromTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
                toTime = row.ToTime?.ToString("HH:mm", CultureInfo.InvariantCulture),
                days = row.ToDate.DayNumber - row.FromDate.DayNumber + 1,
                note = row.Note,
                status = row.Status,
                decisionNote = row.DecisionNote,
                decidedBy = deciders.Name(row.DecidedBy),
                decidedAt = McpToolContext.Local(row.DecidedAt),
                createdAt = McpToolContext.Local(row.CreatedAt),
                submittedByEmployee = row.SubmittedByEmployee,
                cancelledByEmployee = row.CancelledByEmployee ? true : (bool?)null,
            };
        }).ToList();

        return t.Json(
            new
            {
                from = McpToolContext.Iso(start),
                to = McpToolContext.Iso(end),
                total,
                page = pageNo,
                pageSize = size,
                employee = employeeFilter?.Values,
                department = departmentFilter?.Names,
                legalEntity = legalEntityFilter?.Names,
                byStatus = byStatus.ToDictionary(item => item.Status.ToString(), item => item.Count),
                byType = byType
                    .OrderByDescending(item => item.Count)
                    .Select(item => new { absenceType = absenceTypes.Name(item.TypeId), count = item.Count }),
                items,
            },
            items.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ─────────────────────────────────────────────────────────────────

    private sealed record DayRow(
        Guid EmployeeId,
        Guid? DepartmentId,
        Guid? PositionId,
        AttendanceDayStatus Status,
        int LateMinutes,
        int EarlyLeaveMinutes,
        int WorkedMinutes,
        DateTimeOffset? PlanStartAt,
        DateTimeOffset? FirstInAt,
        DateTimeOffset? LastOutAt);

    private sealed record DepartmentInfo(Guid Id, string Name, Guid? ParentId, bool Deleted);

    private sealed record EmploymentSlice(
        Guid Id,
        Guid EmployeeId,
        Guid LegalEntityId,
        Guid? DepartmentId,
        Guid? PositionId,
        DateOnly HireDate,
        bool IsPrimary);

    /// <summary>Filtr: tanlangan identifikatorlar va foydalanuvchiga ko'rsatiladigan nomlar.</summary>
    private sealed record Scope(HashSet<Guid> Ids, List<string> Names);

    /// <summary>Bir bo'limning kunlik sanog'i.</summary>
    private sealed class DayTally
    {
        public int Employees { get; private set; }

        public int Present { get; private set; }

        public int Late { get; private set; }

        public int Absent { get; private set; }

        public int Excused { get; private set; }

        public int DayOff { get; private set; }

        public int DayOffWorked { get; private set; }

        public int MissingExit { get; private set; }

        public int MissingEntry { get; private set; }

        public int NoSchedule { get; private set; }

        public int NotEmployed { get; private set; }

        public int NoRecord { get; private set; }

        public void Add(AttendanceDayStatus status, bool late)
        {
            Employees++;

            if (late)
            {
                Late++;
            }

            switch (status)
            {
                case AttendanceDayStatus.Present: Present++; break;
                case AttendanceDayStatus.Absent: Absent++; break;
                case AttendanceDayStatus.Excused: Excused++; break;
                case AttendanceDayStatus.DayOff: DayOff++; break;
                case AttendanceDayStatus.DayOffWorked: DayOffWorked++; break;
                case AttendanceDayStatus.MissingExit: MissingExit++; break;
                case AttendanceDayStatus.MissingEntry: MissingEntry++; break;
                case AttendanceDayStatus.NoSchedule: NoSchedule++; break;
                case AttendanceDayStatus.NotEmployed: NotEmployed++; break;
                default: NoRecord++; break;
            }
        }

        public void AddNoRecord()
        {
            Employees++;
            NoRecord++;
        }

        public void Merge(DayTally other)
        {
            Employees += other.Employees;
            Present += other.Present;
            Late += other.Late;
            Absent += other.Absent;
            Excused += other.Excused;
            DayOff += other.DayOff;
            DayOffWorked += other.DayOffWorked;
            MissingExit += other.MissingExit;
            MissingEntry += other.MissingEntry;
            NoSchedule += other.NoSchedule;
            NotEmployed += other.NotEmployed;
            NoRecord += other.NoRecord;
        }

        /// <summary>Chiqish shakli; <paramref name="compact"/> — nol sanoqlar tushirib qoldiriladi (o'nlab bo'lim × 12 ustun).</summary>
        public object ToOutput(string? department, bool compact) => new
        {
            department,
            employees = Employees,
            present = Value(Present, compact),
            late = Value(Late, compact),
            absent = Value(Absent, compact),
            excused = Value(Excused, compact),
            dayOff = Value(DayOff, compact),
            dayOffWorked = Value(DayOffWorked, compact),
            missingExit = Value(MissingExit, compact),
            missingEntry = Value(MissingEntry, compact),
            noSchedule = Value(NoSchedule, compact),
            notEmployed = Value(NotEmployed, compact),
            noRecord = Value(NoRecord, compact),
        };

        private static int? Value(int count, bool compact) => compact && count == 0 ? null : count;
    }

    /// <summary>Yig'indi qatori — bazada hisoblanadi (<c>GROUP BY</c>).</summary>
    private sealed class SummaryAggregate
    {
        public Guid? Key { get; init; }

        public int Employees { get; init; }

        public int Records { get; init; }

        public int PresentDays { get; init; }

        public int WorkedMinutes { get; init; }

        public int PlannedMinutes { get; init; }

        public int LateCount { get; init; }

        public int LateMinutes { get; init; }

        public int EarlyLeaveCount { get; init; }

        public int EarlyLeaveMinutes { get; init; }

        public int OvertimeMinutes { get; init; }

        public int AbsentDays { get; init; }

        public int ExcusedDays { get; init; }

        public int DayOffWorkedDays { get; init; }

        public int MissingExitDays { get; init; }

        public int MissingEntryDays { get; init; }

        public int NoScheduleDays { get; init; }

        public object ToOutput(bool withEmployees) => new
        {
            employees = withEmployees ? Employees : (int?)null,
            recordedDays = Records,
            presentDays = PresentDays,
            workedHours = Hours(WorkedMinutes),
            plannedHours = Hours(PlannedMinutes),
            lateCount = LateCount,
            lateMinutes = LateMinutes,
            earlyLeaveCount = EarlyLeaveCount,
            earlyLeaveMinutes = EarlyLeaveMinutes,
            overtimeMinutes = OvertimeMinutes,
            absentDays = AbsentDays,
            excusedDays = ExcusedDays,
            dayOffWorkedDays = DayOffWorkedDays,
            missingExitDays = MissingExitDays,
            missingEntryDays = MissingEntryDays,
            noScheduleDays = NoScheduleDays,
        };

        private static double Hours(int minutes) => Math.Round(minutes / 60.0, 1);
    }

    private static IQueryable<AttendanceDay> ApplyOrgFilter(
        IQueryable<AttendanceDay> query,
        HashSet<Guid>? departmentIds,
        HashSet<Guid>? legalEntityIds)
    {
        if (departmentIds is not null)
        {
            var ids = departmentIds.ToList();
            query = query.Where(item => item.DepartmentId != null && ids.Contains(item.DepartmentId.Value));
        }

        if (legalEntityIds is not null)
        {
            var ids = legalEntityIds.ToList();
            query = query.Where(item => item.LegalEntityId != null && ids.Contains(item.LegalEntityId.Value));
        }

        return query;
    }

    /// <summary>Hamma bo'limlar (o'chirilganlari ham — tarixdagi surat nomsiz qolmasin).</summary>
    private async Task<Dictionary<Guid, DepartmentInfo>> DepartmentsAsync(CancellationToken cancellationToken) =>
        await t.Db.Departments.IgnoreQueryFilters()
            .Select(item => new DepartmentInfo(item.Id, item.Name, item.ParentId, item.DeletedAt != null))
            .ToDictionaryAsync(item => item.Id, cancellationToken)
            .ConfigureAwait(false);

    private Task<Dictionary<Guid, string>> AbsenceTypeNamesAsync(CancellationToken cancellationToken) =>
        t.Db.AbsenceTypes.IgnoreQueryFilters()
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

    /// <summary>
    /// Sanani qamraydigan ASOSIY munosabat har bir xodim uchun — <c>PrimaryEmployment.Select</c>
    /// bilan bir xil tartib (asosiysi, keyin eng avval ishga kirgani), lekin proyeksiya bilan.
    /// </summary>
    private async Task<Dictionary<Guid, EmploymentSlice>> PrimaryEmploymentsAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var rows = await t.Db.Employments
            .Where(item => item.HireDate <= date && (item.TerminationDate == null || item.TerminationDate >= date))
            .Select(item => new EmploymentSlice(
                item.Id,
                item.EmployeeId,
                item.LegalEntityId,
                item.DepartmentId,
                item.PositionId,
                item.HireDate,
                item.IsPrimary))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(item => item.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.IsPrimary)
                    .ThenBy(item => item.HireDate)
                    .ThenBy(item => item.Id)
                    .First());
    }

    /// <summary>Bo'lim: identifikator yoki nom → o'zi va BARCHA ichki bo'limlari.</summary>
    private static Scope? ResolveDepartments(Dictionary<Guid, DepartmentInfo> all, string? department)
    {
        if (string.IsNullOrWhiteSpace(department))
        {
            return null;
        }

        var live = all.Values.Where(item => !item.Deleted).ToDictionary(item => item.Id, item => item.Name);
        var roots = MatchNames(live, department, "Bo'lim");

        var ids = new HashSet<Guid>(roots);
        var queue = new Queue<Guid>(roots);

        while (queue.TryDequeue(out var parent))
        {
            foreach (var child in all.Values.Where(item => item.ParentId == parent))
            {
                if (ids.Add(child.Id))
                {
                    queue.Enqueue(child.Id);
                }
            }
        }

        return new Scope(ids, [.. roots.Select(id => all[id].Name)]);
    }

    private static Scope? ResolveLegalEntities(Dictionary<Guid, string> all, string? legalEntity)
    {
        if (string.IsNullOrWhiteSpace(legalEntity))
        {
            return null;
        }

        var ids = MatchNames(all, legalEntity, "Yuridik shaxs");

        return new Scope([.. ids], [.. ids.Select(id => all[id])]);
    }

    /// <summary>
    /// Identifikator yoki nom: avval aynan mos (katta-kichik harf farqsiz), bo'lmasa nom ichida.
    /// </summary>
    /// <param name="all">Nomlar lug'ati.</param>
    /// <param name="value">Foydalanuvchi bergan qiymat.</param>
    /// <param name="what">Xato matnidagi nom («Bo'lim»).</param>
    /// <param name="listNames">Topilmasa mavjud nomlarni xato matnida sanash (kichik ma'lumotnomalar uchun).</param>
    private static List<Guid> MatchNames(Dictionary<Guid, string> all, string value, string what, bool listNames = false)
    {
        var wanted = value.Trim();

        if (Guid.TryParse(wanted, out var id))
        {
            return all.ContainsKey(id)
                ? [id]
                : throw new McpException($"{what} topilmadi: {wanted}.");
        }

        var exact = all.Where(pair => pair.Value.Equals(wanted, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToList();

        if (exact.Count > 0)
        {
            return exact;
        }

        var partial = all.Where(pair => pair.Value.Contains(wanted, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToList();

        if (partial.Count > 0)
        {
            return partial;
        }

        var hint = listNames
            ? all.Count == 0 ? " Ma'lumotnoma bo'sh." : $" Mavjudlari: {string.Join(", ", all.Values.Order(StringComparer.InvariantCulture))}."
            : " Ro'yxat uchun org_structure vositasini chaqiring.";

        throw new McpException($"{what} topilmadi: «{wanted}».{hint}");
    }

    /// <summary>Xodim: identifikator yoki ism-familiya (har bir so'z topilishi kerak) → <c>{ id: ism }</c>.</summary>
    private async Task<Dictionary<Guid, string>?> ResolveEmployeesAsync(string? employee, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employee))
        {
            return null;
        }

        Dictionary<Guid, string> found;

        if (Guid.TryParse(employee.Trim(), out var id))
        {
            found = await t.EmployeeNamesAsync([id], cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var query = t.Db.Employees.AsQueryable();

            foreach (var token in employee.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4))
            {
                var pattern = Like(token);
                query = query.Where(item => EF.Functions.ILike(
                    item.FullName.LastName + " " + item.FullName.FirstName + " " + (item.FullName.MiddleName ?? string.Empty),
                    pattern,
                    Escape));
            }

            var ids = await query
                .OrderBy(item => item.FullName.LastName)
                .Take(50)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            found = await t.EmployeeNamesAsync(ids, cancellationToken).ConfigureAwait(false);
        }

        return found.Count > 0
            ? found
            : throw new McpException($"Xodim topilmadi: «{employee.Trim()}».");
    }

    private static string Like(string text) =>
        "%" + text.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal) + "%";

    private static TEnum? ParseEnum<TEnum>(string? value, string name)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new McpException($"«{name}» qiymati noto'g'ri: «{value.Trim()}». Mumkin: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }
}
