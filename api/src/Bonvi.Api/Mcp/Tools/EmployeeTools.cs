using System.ComponentModel;
using System.Text;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Bonvi.Api.Mcp.Tools;

/// <summary>
/// Xodimlar: qidiruv, to'liq karta, yaqin sanalar va yetishmayotgan hujjatlar.
/// </summary>
/// <remarks>
/// Pasport, JSHSHIR, rasm va fayllar HECH QAYERDA o'qilmaydi — hujjatlardan
/// faqat turi va yuklangan sanasi (<c>docs/SPEC-mcp.md</c> §0).
/// </remarks>
[McpServerToolType]
public sealed class EmployeeTools(McpToolContext t)
{
    private const int MaxUpcomingDays = 90;

    private const int SectionLimit = McpToolContext.MaxPageSize;

    private const int AmbiguousLimit = 20;

    private enum StatusFilter
    {
        Active,
        Probation,
        Terminated,
        All,
    }

    // =====================================================================
    //  search_employees
    // =====================================================================

    [McpServerTool(Name = "search_employees", Title = "Xodimlarni qidirish",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Xodimlar ro'yxati filtrlar bilan: ism yoki telefon, holat (ishlayotgan / sinovda / ishdan ketgan / hammasi), "
        + "yuridik shaxs, bo'lim (ichki bo'limlari bilan), lavozim, yo'nalish, kuchdagi belgi (rezerv, qora ro'yxat, nazorat, qobiliyatli), "
        + "til va uning darajasi, jins, yosh, ishga kirgan va ketgan sana, «tekshirish kerak». Sahifalangan: jami soni va qisqa qatorlar "
        + "(yosh, telefon, joriy ish joyi, holat, belgilar, tillar). Oklad bu ro'yxatda yo'q — to'liq karta uchun «get_employee». "
        + "Paged employee search with filters; returns the total and compact rows (no salary).")]
    public async Task<string> SearchEmployees(
        [Description("Ism-familiya yoki telefon. Ismlar bazada KIRILL yozuvida (masalan «Каримов Азиз») — lotincha ismni kirillga "
            + "o'girib yuboring; so'zlar istalgan tartibda, har biri ism ichida bo'lishi kerak. Faqat raqamlar — telefon bo'yicha.")]
        string? query = null,
        [Description("Holat: active — ishlayotganlar, sinovdagilar bilan (sukut); probation — faqat sinov muddatidagilar; "
            + "terminated — ishdan ketgan va hozir hech qayerda ishlamaydiganlar; all — hammasi.")]
        string? status = null,
        [Description("Yuridik shaxs nomi yoki id.")] string? legalEntity = null,
        [Description("Bo'lim nomi yoki id — ichki bo'limlari ham kiradi.")] string? department = null,
        [Description("Lavozim nomi yoki id.")] string? position = null,
        [Description("Yo'nalish nomi yoki id.")] string? direction = null,
        [Description("Hozir kuchda bo'lgan belgi: Reserve (rezerv), Blacklist (qora ro'yxat), Monitoring (nazorat), Capable (qobiliyatli).")]
        string? flag = null,
        [Description("Til: Uzbek, Russian, English, Karakalpak, Kazakh, Kyrgyz, Tajik, Turkmen, Azerbaijani, Turkish, Arabic, Persian, "
            + "Chinese, Korean, Japanese, German, French, Spanish, Italian, Ukrainian, Hindi.")]
        string? language = null,
        [Description("Tilning eng past darajasi («language» bilan): Elementary, Intermediate, UpperIntermediate, Advanced, Native.")]
        string? minLevel = null,
        [Description("Jins: Male yoki Female.")] string? gender = null,
        [Description("Eng kichik yosh (to'liq yil).")] int? minAge = null,
        [Description("Eng katta yosh (to'liq yil).")] int? maxAge = null,
        [Description("Ishga kirgan sana, dan (YYYY-MM-DD).")] string? hiredFrom = null,
        [Description("Ishga kirgan sana, gacha (YYYY-MM-DD).")] string? hiredTo = null,
        [Description("Ishdan ketgan sana, dan (YYYY-MM-DD). Berilsa holat sukut bo'yicha «terminated».")] string? terminatedFrom = null,
        [Description("Ishdan ketgan sana, gacha (YYYY-MM-DD).")] string? terminatedTo = null,
        [Description("true — faqat «tekshirish kerak» belgisi borlar, false — belgisizlar.")] bool? needsReview = null,
        [Description("Sahifa raqami, 1 dan.")] int? page = null,
        [Description("Sahifadagi qatorlar soni (sukut 50, ko'pi bilan 200).")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.EmployeesView, "Xodimlar");

        var db = t.Db;
        var today = McpToolContext.Today;
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);

        var hiredStart = EmployeeQueryKit.OptionalDate(hiredFrom, "hiredFrom");
        var hiredEnd = EmployeeQueryKit.OptionalDate(hiredTo, "hiredTo");
        var leftStart = EmployeeQueryKit.OptionalDate(terminatedFrom, "terminatedFrom");
        var leftEnd = EmployeeQueryKit.OptionalDate(terminatedTo, "terminatedTo");
        var byTermination = leftStart is not null || leftEnd is not null;

        if (hiredStart > hiredEnd || leftStart > leftEnd)
        {
            throw new McpException("Sana oralig'ida «dan» sanasi «gacha» dan keyin bo'lishi mumkin emas.");
        }

        var statusFilter = ParseStatus(status, byTermination ? StatusFilter.Terminated : StatusFilter.Active);

        if (byTermination && statusFilter is StatusFilter.Active or StatusFilter.Probation)
        {
            throw new McpException("Ishdan ketgan sana filtri faqat status = terminated yoki all bilan ishlaydi.");
        }

        var flagType = EmployeeQueryKit.ParseEnum<EmployeeFlagType>(flag, "flag");
        var languageCode = EmployeeQueryKit.ParseEnum<LanguageCode>(language, "language");
        var level = EmployeeQueryKit.ParseEnum<LanguageLevel>(minLevel, "minLevel");
        var genderValue = EmployeeQueryKit.ParseEnum<Gender>(gender, "gender");

        if (level is not null && languageCode is null)
        {
            throw new McpException("«minLevel» faqat «language» bilan birga beriladi.");
        }

        if (minAge is < 0 or > 120 || maxAge is < 0 or > 120 || minAge > maxAge)
        {
            throw new McpException("Yosh oralig'i 0–120 ichida va minAge ≤ maxAge bo'lsin.");
        }

        var names = await EmployeeQueryKit.OrgNamesAsync(t, cancellationToken).ConfigureAwait(false);
        var directions = await t.DirectionsAsync(cancellationToken).ConfigureAwait(false);

        var legalEntityIds = EmployeeQueryKit.Resolve(names.LegalEntities, legalEntity, "Yuridik shaxs");
        var departmentRoots = EmployeeQueryKit.Resolve(names.Departments, department, "Bo'lim");
        var positionIds = EmployeeQueryKit.Resolve(names.Positions, position, "Lavozim");
        var directionIds = EmployeeQueryKit.Resolve(directions, direction, "Yo'nalish");

        var departmentIds = departmentRoots is null
            ? null
            : await EmployeeQueryKit.BranchAsync(t, departmentRoots, cancellationToken).ConfigureAwait(false);

        // Mehnat munosabati sharti: ish joyi filtrlari va holat BITTA
        // munosabatga qo'llanadi («X bo'limida ishlayotgan»), turli
        // munosabatlarga bo'linib emas.
        var employments = db.Employments.AsQueryable();
        var employmentRequired = statusFilter != StatusFilter.All;

        if (legalEntityIds is not null)
        {
            var list = legalEntityIds.ToList();
            employments = employments.Where(employment => list.Contains(employment.LegalEntityId));
            employmentRequired = true;
        }

        if (departmentIds is not null)
        {
            var list = departmentIds.ToList();
            employments = employments.Where(employment =>
                employment.DepartmentId != null && list.Contains(employment.DepartmentId.Value));
            employmentRequired = true;
        }

        if (positionIds is not null)
        {
            var list = positionIds.ToList();
            employments = employments.Where(employment =>
                employment.PositionId != null && list.Contains(employment.PositionId.Value));
            employmentRequired = true;
        }

        if (hiredStart is { } hs)
        {
            employments = employments.Where(employment => employment.HireDate >= hs);
            employmentRequired = true;
        }

        if (hiredEnd is { } he)
        {
            employments = employments.Where(employment => employment.HireDate <= he);
            employmentRequired = true;
        }

        if (leftStart is { } ls)
        {
            employments = employments.Where(employment => employment.TerminationDate >= ls);
            employmentRequired = true;
        }

        if (leftEnd is { } le)
        {
            employments = employments.Where(employment => employment.TerminationDate <= le);
            employmentRequired = true;
        }

        employments = statusFilter switch
        {
            StatusFilter.Active => employments.Where(employment => employment.Status != EmploymentStatus.Terminated),
            StatusFilter.Probation => employments.Where(employment => employment.Status == EmploymentStatus.Probation),
            StatusFilter.Terminated => employments.Where(employment => employment.Status == EmploymentStatus.Terminated),
            _ => employments,
        };

        var employees = await EmployeeQueryKit.MatchTextAsync(t, db.Employees, query, cancellationToken).ConfigureAwait(false);

        if (employmentRequired)
        {
            employees = employees.Where(employee => employments.Any(employment => employment.EmployeeId == employee.Id));
        }

        // «Ishdan ketgan» — hozir hech qayerda ishlamaydigan: qayta ishga
        // olingan odam bu ro'yxatda turmasligi kerak.
        if (statusFilter == StatusFilter.Terminated)
        {
            employees = employees.Where(employee => !db.Employments.Any(employment =>
                employment.EmployeeId == employee.Id && employment.Status != EmploymentStatus.Terminated));
        }

        if (needsReview is { } review)
        {
            employees = employees.Where(employee => employee.NeedsReview == review);
        }

        if (genderValue is { } g)
        {
            employees = employees.Where(employee => employee.Gender == g);
        }

        if (minAge is { } youngest)
        {
            var bornBy = today.AddYears(-youngest);
            employees = employees.Where(employee => employee.BirthDate != null && employee.BirthDate <= bornBy);
        }

        if (maxAge is { } oldest)
        {
            var bornAfter = today.AddYears(-(oldest + 1));
            employees = employees.Where(employee => employee.BirthDate != null && employee.BirthDate > bornAfter);
        }

        if (directionIds is not null)
        {
            var list = directionIds.ToList();
            employees = employees.Where(employee => employee.DirectionIds.Any(id => list.Contains(id)));
        }

        if (flagType is { } ft)
        {
            employees = employees.Where(employee => db.EmployeeFlags.Any(item =>
                item.EmployeeId == employee.Id
                && item.Type == ft
                && item.StartDate <= today
                && (item.EndDate == null || item.EndDate > today)));
        }

        if (languageCode is { } lc)
        {
            var lowest = level ?? LanguageLevel.Elementary;

            var speakers =
                from competency in db.EmployeeCompetencies
                join skill in db.EmployeeCompetencyLanguages on competency.Id equals skill.EmployeeCompetencyId
                where skill.Language == lc && skill.Level >= lowest
                select competency.EmployeeId;

            employees = employees.Where(employee => speakers.Contains(employee.Id));
        }

        var total = await employees.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await employees
            .OrderBy(employee => employee.FullName.LastName)
            .ThenBy(employee => employee.FullName.FirstName)
            .ThenBy(employee => employee.Id)
            .Skip((pageNo - 1) * size)
            .Take(size)
            .Select(employee => new
            {
                employee.Id,
                employee.FullName,
                employee.BirthDate,
                employee.Gender,
                employee.Phone,
                employee.NeedsReview,
                employee.DirectionIds,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ids = rows.Select(row => row.Id).ToList();

        // Ko'rsatiladigan ish joyi — filtrga MOS kelgan munosabatlardan
        // tanlanadi («X bo'limi» deb qidirilganda Y dagi ikkinchi ishi emas).
        var jobs = await EmployeeQueryKit
            .JobsAsync(employmentRequired ? employments : db.Employments, ids, cancellationToken)
            .ConfigureAwait(false);

        var jobByEmployee = jobs
            .GroupBy(job => job.EmployeeId)
            .ToDictionary(group => group.Key, group => EmployeeQueryKit.Pick(group));

        var flags = await EmployeeQueryKit.ActiveFlagsAsync(t, ids, cancellationToken).ConfigureAwait(false);
        var languages = await LanguagesAsync(ids, cancellationToken).ConfigureAwait(false);

        var items = rows.Select(row => new
        {
            id = row.Id,
            fullName = row.FullName.Full,
            age = McpLookups.Age(row.BirthDate),
            gender = row.Gender,
            phone = McpLookups.Phone(row.Phone),
            job = EmployeeQueryKit.Summary(jobByEmployee.GetValueOrDefault(row.Id), names),
            flags = flags.GetValueOrDefault(row.Id),
            languages = languages.TryGetValue(row.Id, out var skills)
                ? skills.Select(skill => $"{skill.Language}: {skill.Level}").ToList()
                : null,
            directions = row.DirectionIds.Count == 0
                ? null
                : row.DirectionIds.Select(id => directions.Name(id)).OfType<string>().ToList(),
            needsReview = row.NeedsReview ? true : (bool?)null,
        }).ToList();

        return t.Json(
            new
            {
                total,
                page = pageNo,
                pageSize = size,
                status = statusFilter.ToString().ToLowerInvariant(),
                matchedLegalEntities = NamesOf(legalEntityIds, names.LegalEntities),
                matchedDepartments = NamesOf(departmentRoots, names.Departments),
                matchedPositions = NamesOf(positionIds, names.Positions),
                matchedDirections = NamesOf(directionIds, directions),
                items,
            },
            items.Count);
    }

    // =====================================================================
    //  get_employee
    // =====================================================================

    [McpServerTool(Name = "get_employee", Title = "Xodim kartasi",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Bitta xodimning to'liq kartasi (pasport, JSHSHIR, rasm va fayllarsiz): shaxsiy ma'lumotlar, telefonlar, manzil, "
        + "qobiliyatlar va tillar, belgilar, barcha mehnat munosabatlari, sinov muddatlari, hujjatlar (faqat turi va sanasi). "
        + "Ruxsatga qarab: oklad, buyruqlar lentasi, rag'bat/jarima, baxtsiz hodisalar, exit interview, nomzodlik tarixi "
        + "(anketa, bosqichlar, kengash xulosalari), so'nggi 30 kun davomati va yo'qlik so'rovlari. Ism berilsa va bir nechta odam "
        + "mos kelsa — nomzodlar ro'yxati qaytadi, id bilan qayta chaqiring. "
        + "Full employee card by id (or name), sections filtered by the caller's permissions.")]
    public async Task<string> GetEmployee(
        [Description("Xodim id si (search_employees natijasidan). Id bo'lmasa — to'liq ism yoki telefon ham bo'ladi; ism kirill yozuvida "
            + "(masalan «Каримов Азиз»).")]
        string id,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.EmployeesView, "Xodimlar");

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new McpException("Xodim id si yoki ismini bering.");
        }

        var db = t.Db;
        var names = await EmployeeQueryKit.OrgNamesAsync(t, cancellationToken).ConfigureAwait(false);

        var matching = await EmployeeQueryKit.MatchTextAsync(t, db.Employees, id, cancellationToken).ConfigureAwait(false);

        var total = await matching.CountAsync(cancellationToken).ConfigureAwait(false);

        var matches = await matching
            .OrderBy(item => item.FullName.LastName)
            .ThenBy(item => item.FullName.FirstName)
            .Select(item => new { item.Id, item.FullName, item.BirthDate })
            .Take(AmbiguousLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // To'liq ism aynan berilgan bo'lsa, uni o'z ichiga olgan uzunroq
        // ismlar («Каримов Азизбек») tanlovni buzmasin.
        if (matches.Count > 1)
        {
            var wanted = EmployeeQueryKit.Fold(string.Join(' ', id.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
            var exact = matches.Where(item => string.Equals(EmployeeQueryKit.Fold(item.FullName.Full), wanted, StringComparison.Ordinal)).ToList();

            if (exact.Count == 1)
            {
                matches = exact;
            }
        }

        if (matches.Count == 0)
        {
            return t.Json(
                new
                {
                    found = 0,
                    note = "Bunday xodim topilmadi. Ism bazada kirill yozuvida saqlanadi; «search_employees» bilan qidirib ko'ring.",
                },
                0);
        }

        if (matches.Count > 1)
        {
            var jobs = await EmployeeQueryKit.CurrentJobsAsync(t, [.. matches.Select(item => item.Id)], cancellationToken)
                .ConfigureAwait(false);

            return t.Json(
                new
                {
                    found = total,
                    note = total > matches.Count
                        ? $"Bir nechta xodim mos keldi, birinchi {matches.Count} tasi ko'rsatildi — aniqroq yozing yoki kerakli odamning id si bilan qayta chaqiring."
                        : "Bir nechta xodim mos keldi — kerakli odamning id si bilan qayta chaqiring.",
                    candidates = matches.Select(item => new
                    {
                        id = item.Id,
                        fullName = item.FullName.Full,
                        birthDate = McpToolContext.Iso(item.BirthDate),
                        job = EmployeeQueryKit.Summary(jobs.GetValueOrDefault(item.Id), names),
                    }),
                },
                matches.Count);
        }

        return await CardAsync(matches[0].Id, names, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> CardAsync(Guid id, McpOrgNames names, CancellationToken cancellationToken)
    {
        var db = t.Db;
        var today = McpToolContext.Today;
        var canSalary = t.Has(Permissions.SalaryView);
        var canExtra = canSalary && t.Has(Permissions.SalaryExtraView);
        var omitted = new List<string>();

        var person = await db.Employees
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.FullName,
                item.BirthDate,
                item.Gender,
                item.Phone,
                item.SecondPhone,
                item.Address,
                item.Speciality,
                item.TelegramUrl,
                item.HasCriminalRecord,
                item.CriminalRecordNote,
                item.MehnatUzUrl,
                item.NeedsReview,
                item.ReviewNote,
                item.DirectionIds,
                item.MobileCheckInAllowed,
                item.CreatedAt,
            })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var directions = await t.DirectionsAsync(cancellationToken).ConfigureAwait(false);

        // ---- Qobiliyatlar va tillar
        var competency = await db.EmployeeCompetencies
            .Where(item => item.EmployeeId == id)
            .Select(item => new
            {
                item.Id,
                item.Languages,
                item.ComputerLiteracyPercent,
                item.Specialization,
                item.OtherAbilities,
                item.Interest,
                item.EducationPlace,
                item.CandidateNote,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var languages = (await LanguagesAsync([id], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(id);

        // ---- Belgilar (hammasi, tarix bilan)
        var flags = await db.EmployeeFlags
            .Where(item => item.EmployeeId == id)
            .OrderByDescending(item => item.StartDate)
            .Select(item => new { item.Type, item.StartDate, item.EndDate, item.Reason })
            .Take(SectionLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // ---- Mehnat munosabatlari
        var employments = await db.Employments
            .Where(item => item.EmployeeId == id)
            .OrderByDescending(item => item.Status != EmploymentStatus.Terminated)
            .ThenByDescending(item => item.HireDate)
            .Select(item => new
            {
                item.Id,
                item.LegalEntityId,
                item.DepartmentId,
                item.PositionId,
                item.HireDate,
                item.HireOrderNo,
                item.HireOrderDate,
                item.Salary,
                item.Rate,
                item.IsPrimary,
                item.Status,
                item.TerminationDate,
                item.TerminationReasonId,
                item.TerminationOrderNo,
                item.TerminationNote,
                item.ResponsibleEmployeeId,
                item.NeedsReview,
                item.ReviewNote,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var employmentIds = employments.Select(item => item.Id).ToList();

        var events = await db.EmploymentEvents
            .Where(item => employmentIds.Contains(item.EmploymentId))
            .OrderBy(item => item.EffectiveDate)
            .ThenBy(item => item.RecordedAt)
            .Select(item => new
            {
                item.EmploymentId,
                item.Type,
                item.EffectiveDate,
                item.OrderNo,
                item.OrderDate,
                item.FromDepartmentId,
                item.ToDepartmentId,
                item.FromPositionId,
                item.ToPositionId,
                item.FromSalary,
                item.ToSalary,
                item.Note,
                item.DutyEndDate,
                item.CandidateId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var probations = await db.Probations
            .Where(item => employmentIds.Contains(item.EmploymentId))
            .OrderBy(item => item.StartDate)
            .Select(item => new
            {
                item.EmploymentId,
                item.StartDate,
                item.EndDate,
                item.Status,
                item.ResolvedAt,
                item.Conclusion,
                item.ReviewerEmployeeId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var people = await t.EmployeeNamesAsync(
                employments.Select(item => item.ResponsibleEmployeeId)
                    .Concat(probations.Select(item => item.ReviewerEmployeeId))
                    .OfType<Guid>(),
                cancellationToken)
            .ConfigureAwait(false);

        var employmentLabel = employments.ToDictionary(
            item => item.Id,
            item => names.LegalEntities.Name(item.LegalEntityId));

        // Faol qo'shimcha vazifalar — veb-kartadagi kabi (ishdan ketgan
        // munosabatda faol vazifa yo'q).
        var activeDuties = events
            .Where(item => item.Type == EmploymentEventType.AdditionalDuty
                           && item.EffectiveDate <= today
                           && (item.DutyEndDate == null || item.DutyEndDate > today))
            .ToList();

        var employmentItems = employments.Select(item =>
        {
            var duties = item.Status == EmploymentStatus.Terminated
                ? []
                : activeDuties.Where(duty => duty.EmploymentId == item.Id).ToList();

            return new
            {
                legalEntity = names.LegalEntities.Name(item.LegalEntityId),
                department = names.Departments.Name(item.DepartmentId),
                position = names.Positions.Name(item.PositionId),
                status = item.Status,
                isPrimary = item.IsPrimary,
                rate = item.Rate,
                hireDate = McpToolContext.Iso(item.HireDate),
                hireOrderNo = item.HireOrderNo,
                hireOrderDate = McpToolContext.Iso(item.HireOrderDate),
                workedDays = (item.TerminationDate ?? today).DayNumber - item.HireDate.DayNumber,
                salary = McpLookups.Money(item.Salary, canSalary),
                activeDuties = duties.Count == 0
                    ? null
                    : duties.Select(duty => new
                    {
                        note = duty.Note,
                        since = McpToolContext.Iso(duty.EffectiveDate),
                        until = McpToolContext.Iso(duty.DutyEndDate),
                        pay = McpLookups.Money(duty.ToSalary, canExtra),
                    }).ToList(),
                totalPay = TotalPay(item.Salary, duties.Select(duty => duty.ToSalary), canExtra),
                terminationDate = McpToolContext.Iso(item.TerminationDate),
                terminationReason = names.TerminationReasons.Name(item.TerminationReasonId),
                terminationOrderNo = item.TerminationOrderNo,
                terminationNote = item.TerminationNote,
                responsible = people.Name(item.ResponsibleEmployeeId),
                needsReview = item.NeedsReview ? true : (bool?)null,
                reviewNote = item.ReviewNote,
            };
        }).ToList();

        if (!canSalary)
        {
            omitted.Add("oklad va pul summalari (salary.view)");
        }
        else if (!canExtra)
        {
            omitted.Add("qo'shimcha vazifa haqi (salary.extra.view)");
        }

        // ---- Buyruqlar lentasi: vebda «employment.view» ostida
        object? timeline = null;

        if (t.Has(Permissions.EmploymentView))
        {
            timeline = events.Select(item => new
            {
                date = McpToolContext.Iso(item.EffectiveDate),
                type = item.Type,
                legalEntity = employments.Count > 1 ? employmentLabel.GetValueOrDefault(item.EmploymentId) : null,
                orderNo = item.OrderNo,
                orderDate = McpToolContext.Iso(item.OrderDate),
                fromDepartment = names.Departments.Name(item.FromDepartmentId),
                toDepartment = names.Departments.Name(item.ToDepartmentId),
                fromPosition = names.Positions.Name(item.FromPositionId),
                toPosition = names.Positions.Name(item.ToPositionId),
                fromSalary = McpLookups.Money(item.FromSalary, canSalary),

                // Qo'shimcha vazifadagi summa — okladdan alohida ruxsat bilan
                toSalary = McpLookups.Money(
                    item.ToSalary,
                    item.Type == EmploymentEventType.AdditionalDuty ? canExtra : canSalary),
                dutyEndDate = McpToolContext.Iso(item.DutyEndDate),
                note = item.Note,
            }).TakeLast(SectionLimit).ToList();
        }
        else
        {
            omitted.Add("buyruqlar lentasi (employment.view)");
        }

        // ---- Hujjatlar: FAQAT turi va sanasi
        var documentTypes = await db.DocumentTypes.IgnoreQueryFilters()
            .Select(item => new { item.Id, item.Name, item.IsRequired, item.IsActive, item.DeletedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var documents = await db.EmployeeDocuments
            .Where(item => item.EmployeeId == id)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new { item.DocumentTypeId, item.CreatedAt })
            .Take(SectionLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var documentTypeNames = documentTypes.ToDictionary(item => item.Id, item => item.Name);
        var present = documents.Select(item => item.DocumentTypeId).ToHashSet();

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["person"] = new
            {
                id = person.Id,
                fullName = person.FullName.Full,
                lastName = person.FullName.LastName,
                firstName = person.FullName.FirstName,
                middleName = person.FullName.MiddleName,
                birthDate = McpToolContext.Iso(person.BirthDate),
                age = McpLookups.Age(person.BirthDate),
                gender = person.Gender,
                phone = McpLookups.Phone(person.Phone),
                secondPhone = McpLookups.Phone(person.SecondPhone),
                address = person.Address,
                speciality = person.Speciality,
                telegramUrl = person.TelegramUrl,
                hasCriminalRecord = person.HasCriminalRecord,
                criminalRecordNote = person.CriminalRecordNote,
                mehnatUzUrl = person.MehnatUzUrl,
                directions = person.DirectionIds.Select(item => directions.Name(item)).OfType<string>().ToList(),
                mobileCheckInAllowed = person.MobileCheckInAllowed,
                needsReview = person.NeedsReview,
                reviewNote = person.ReviewNote,
                createdAt = McpToolContext.Local(person.CreatedAt),
            },
            ["competency"] = competency is null && languages is null
                ? null
                : new
                {
                    languages = languages?.Select(item => new { language = item.Language, level = item.Level }).ToList(),
                    languagesText = competency?.Languages,
                    computerLiteracyPercent = competency?.ComputerLiteracyPercent,
                    specialization = competency?.Specialization,
                    otherAbilities = competency?.OtherAbilities,
                    interest = competency?.Interest,
                    educationPlace = competency?.EducationPlace,
                    candidateNote = competency?.CandidateNote,
                },
            ["flags"] = flags.Select(item => new
            {
                type = item.Type,
                startDate = McpToolContext.Iso(item.StartDate),
                endDate = McpToolContext.Iso(item.EndDate),
                active = item.StartDate <= today && (item.EndDate == null || item.EndDate >= today),
                reason = item.Reason,
            }).ToList(),
            ["employments"] = employmentItems,
            ["probations"] = probations.Select(item => new
            {
                legalEntity = employments.Count > 1 ? employmentLabel.GetValueOrDefault(item.EmploymentId) : null,
                startDate = McpToolContext.Iso(item.StartDate),
                endDate = McpToolContext.Iso(item.EndDate),
                status = item.Status,
                daysLeft = item.Status == ProbationStatus.InProgress ? item.EndDate.DayNumber - today.DayNumber : (int?)null,
                resolvedAt = McpToolContext.Iso(item.ResolvedAt),
                conclusion = item.Conclusion,
                reviewer = people.Name(item.ReviewerEmployeeId),
            }).ToList(),
            ["documents"] = new
            {
                uploaded = documents.Select(item => new
                {
                    type = documentTypeNames.Name(item.DocumentTypeId),
                    uploadedAt = McpToolContext.Iso(item.CreatedAt.TashkentDate()),
                }).ToList(),
                missingRequired = documentTypes
                    .Where(item => item.IsRequired && item.IsActive && item.DeletedAt == null && !present.Contains(item.Id))
                    .Select(item => item.Name)
                    .Order(StringComparer.Ordinal)
                    .ToList(),
            },
        };

        if (timeline is not null)
        {
            payload["timeline"] = timeline;
        }

        if (payload["competency"] is null)
        {
            payload.Remove("competency");
        }

        if (t.Has(Permissions.IncentivesView))
        {
            payload["incentives"] = await IncentivesAsync(id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            omitted.Add("rag'bat va jarimalar (incentives.view)");
        }

        if (t.Has(Permissions.SafetyView))
        {
            payload["incidents"] = await IncidentsAsync(id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            omitted.Add("baxtsiz hodisalar (safety.view)");
        }

        if (t.Has(Permissions.FeedbackView))
        {
            payload["exitInterviews"] = await ExitInterviewsAsync(employmentIds, employmentLabel, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            omitted.Add("exit interview (feedback.view)");
        }

        if (t.Has(Permissions.RecruitingView))
        {
            var candidateIds = events.Select(item => item.CandidateId).OfType<Guid>().Distinct().ToList();
            payload["candidateHistory"] = await CandidateHistoryAsync(candidateIds, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            omitted.Add("nomzodlik tarixi (recruiting.view)");
        }

        if (t.Has(Permissions.AttendanceView))
        {
            payload["attendance"] = await AttendanceAsync(id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            omitted.Add("davomat va yo'qlik so'rovlari (attendance.view)");
        }

        payload["sections"] = new
        {
            omitted = omitted.Count == 0 ? null : omitted,
            note = "Pasport, JSHSHIR, rasm va yuklangan fayllar MCP orqali berilmaydi.",
        };

        return t.Json(payload, 1);
    }

    /// <summary>
    /// Oklad + faol qo'shimcha haqlar — ikkala ruxsat bo'lsagina (vebdagi qoida:
    /// faqat oklad ruxsati bilan jami berilmaydi, aks holda yopiq haq ayirib topilardi).
    /// </summary>
    private static object? TotalPay(Money? salary, IEnumerable<Money?> duties, bool canExtra)
    {
        if (salary is null || !canExtra)
        {
            return null;
        }

        var pays = duties.OfType<Money>().ToList();

        if (pays.Count == 0 || pays.Any(pay => !string.Equals(pay.Currency, salary.Currency, StringComparison.Ordinal)))
        {
            return null;
        }

        return new { amount = salary.Amount + pays.Sum(pay => pay.Amount), currency = salary.Currency };
    }

    private async Task<object> IncentivesAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var db = t.Db;

        var rows = await db.Incentives
            .Where(item => item.EmployeeId == employeeId)
            .OrderByDescending(item => item.Date)
            .Select(item => new
            {
                item.Kind,
                item.TypeId,
                item.Date,
                item.Amount,
                item.GiftDescription,
                item.Reason,
                item.OrderNo,
                item.OrderDate,
            })
            .Take(SectionLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var awards = await db.AwardTypes.IgnoreQueryFilters()
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
            .ConfigureAwait(false);

        var penalties = await db.PenaltyTypes.IgnoreQueryFilters()
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(item => new
        {
            kind = item.Kind,
            type = (item.Kind == IncentiveKind.Award ? awards : penalties).Name(item.TypeId),
            date = McpToolContext.Iso(item.Date),
            amount = McpLookups.Money(item.Amount, visible: true),
            gift = item.GiftDescription,
            reason = item.Reason,
            orderNo = item.OrderNo,
            orderDate = McpToolContext.Iso(item.OrderDate),
        }).ToList();
    }

    private async Task<object> IncidentsAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var rows = await t.Db.Incidents
            .Where(item => item.EmployeeId == employeeId)
            .OrderByDescending(item => item.IncidentDate)
            .Select(item => new
            {
                item.IncidentDate,
                item.Shift,
                item.Location,
                item.Description,
                item.Cause,
                item.CorrectiveAction,
                item.Severity,
                item.MedicalAidProvided,
                item.MedicalAidNote,
                item.CommissionConclusion,
                item.ConcludedAt,
                item.ResponsibleEmployeeId,
            })
            .Take(SectionLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var people = await t.EmployeeNamesAsync(rows.Select(item => item.ResponsibleEmployeeId).OfType<Guid>(), cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(item => new
        {
            date = McpToolContext.Iso(item.IncidentDate),
            shift = item.Shift,
            location = item.Location,
            description = item.Description,
            cause = item.Cause,
            correctiveAction = item.CorrectiveAction,
            severity = item.Severity,
            medicalAidProvided = item.MedicalAidProvided,
            medicalAidNote = item.MedicalAidNote,
            commissionConclusion = item.CommissionConclusion,
            concludedAt = McpToolContext.Iso(item.ConcludedAt),
            responsible = people.Name(item.ResponsibleEmployeeId),
        }).ToList();
    }

    private async Task<object> ExitInterviewsAsync(
        List<Guid> employmentIds,
        Dictionary<Guid, string?> employmentLabel,
        CancellationToken cancellationToken)
    {
        var rows = await t.Db.ExitInterviews
            .Where(item => employmentIds.Contains(item.EmploymentId))
            .OrderByDescending(item => item.InterviewDate)
            .Select(item => new
            {
                item.EmploymentId,
                item.InterviewDate,
                item.Reason,
                item.WouldRecommend,
                item.Impressions,
                item.ConditionsToReturn,
            })
            .Take(SectionLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(item => new
        {
            legalEntity = employmentLabel.GetValueOrDefault(item.EmploymentId),
            date = McpToolContext.Iso(item.InterviewDate),
            reason = item.Reason,
            wouldRecommend = item.WouldRecommend,
            impressions = item.Impressions,
            conditionsToReturn = item.ConditionsToReturn,
        }).ToList();
    }

    /// <summary>
    /// Nomzodlik tarixi — ishga qabul hodisasidagi <c>candidate_id</c> orqali
    /// (veb-kartadagi «Bosqichlar» bilan bir xil bog'lanish).
    /// </summary>
    private async Task<object> CandidateHistoryAsync(List<Guid> candidateIds, CancellationToken cancellationToken)
    {
        if (candidateIds.Count == 0)
        {
            return new List<object>();
        }

        var db = t.Db;

        var candidates = await db.Candidates
            .Where(item => candidateIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.VacancyId,
                item.CurrentStageId,
                item.Status,
                item.Source,
                item.Speciality,
                item.Note,
                item.CreatedAt,
                item.InterviewAt,
                item.ClosedAt,
                item.ClosingNote,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vacancies = await db.Vacancies.IgnoreQueryFilters()
            .Where(item => candidates.Select(candidate => candidate.VacancyId).Contains(item.Id))
            .Select(item => new { item.Id, item.Title })
            .ToDictionaryAsync(item => item.Id, item => item.Title, cancellationToken)
            .ConfigureAwait(false);

        var stages = await db.PipelineStages.IgnoreQueryFilters()
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
            .ConfigureAwait(false);

        var entries = await db.CandidateStageEntries
            .Where(item => candidateIds.Contains(item.CandidateId))
            .OrderBy(item => item.Date)
            .ThenBy(item => item.RecordedAt)
            .Select(item => new { item.CandidateId, item.StageId, item.Date, item.Conclusion, item.RecordedBy })
            .Take(SectionLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var notes = await db.CandidateNotes
            .Where(item => candidateIds.Contains(item.CandidateId))
            .OrderBy(item => item.CreatedAt)
            .Select(item => new { item.CandidateId, item.Kind, item.Text, item.CreatedAt, item.CreatedBy })
            .Take(SectionLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var questionnaires = await db.CandidateQuestionnaires
            .Where(item => candidateIds.Contains(item.CandidateId))
            .Select(item => new
            {
                item.CandidateId,
                item.Education,
                item.WorkExperience,
                item.MaritalStatus,
                item.RussianLevel,
                item.OtherLanguages,
                item.OfficeSkills,
                item.LastSalary,
                item.ExtraNote,
                item.HasDriverLicense,
                item.DriverLicenseCategories,
                item.ExpectedSalary,
                item.ReferralSource,
                item.OfferAcceptedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var users = await t.UserNamesAsync(
                entries.Select(item => item.RecordedBy).Concat(notes.Select(item => item.CreatedBy)).OfType<Guid>(),
                cancellationToken)
            .ConfigureAwait(false);

        return candidates.OrderBy(item => item.CreatedAt).Select(item =>
        {
            var questionnaire = questionnaires.FirstOrDefault(row => row.CandidateId == item.Id);

            return new
            {
                candidateId = item.Id,
                vacancy = vacancies.Name(item.VacancyId),
                status = item.Status,
                source = item.Source,
                currentStage = stages.Name(item.CurrentStageId),
                speciality = item.Speciality,
                appliedAt = McpToolContext.Local(item.CreatedAt),
                interviewAt = McpToolContext.Local(item.InterviewAt),
                closedAt = McpToolContext.Iso(item.ClosedAt),
                closingNote = item.ClosingNote,
                note = item.Note,
                stages = entries.Where(row => row.CandidateId == item.Id).Select(row => new
                {
                    stage = stages.Name(row.StageId),
                    date = McpToolContext.Iso(row.Date),
                    conclusion = row.Conclusion,
                    recordedBy = users.Name(row.RecordedBy),
                }).ToList(),
                notes = notes.Where(row => row.CandidateId == item.Id).Select(row => new
                {
                    kind = row.Kind,
                    text = row.Text,
                    at = McpToolContext.Local(row.CreatedAt),
                    by = users.Name(row.CreatedBy),
                }).ToList(),
                questionnaire = questionnaire is null
                    ? null
                    : new
                    {
                        education = questionnaire.Education,
                        workExperience = questionnaire.WorkExperience,
                        maritalStatus = questionnaire.MaritalStatus,
                        russianLevel = questionnaire.RussianLevel,
                        otherLanguages = questionnaire.OtherLanguages,
                        officeSkills = questionnaire.OfficeSkills,
                        lastSalary = questionnaire.LastSalary,
                        extraNote = questionnaire.ExtraNote,
                        hasDriverLicense = questionnaire.HasDriverLicense,
                        driverLicenseCategories = questionnaire.DriverLicenseCategories,
                        expectedSalary = questionnaire.ExpectedSalary,
                        referralSource = questionnaire.ReferralSource,
                        offerAcceptedAt = McpToolContext.Local(questionnaire.OfferAcceptedAt),
                    },
            };
        }).ToList();
    }

    /// <summary>So'nggi 30 kun davomati (kunma-kun va jami) va yo'qlik so'rovlari.</summary>
    private async Task<object> AttendanceAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var db = t.Db;
        var today = McpToolContext.Today;
        var from = today.AddDays(-29);

        var days = await db.AttendanceDays
            .Where(item => item.EmployeeId == employeeId && item.LocalDate >= from && item.LocalDate <= today)
            .OrderBy(item => item.LocalDate)
            .Select(item => new
            {
                item.LocalDate,
                item.Status,
                item.FirstInAt,
                item.LastOutAt,
                item.PlannedMinutes,
                item.WorkedMinutes,
                item.LateMinutes,
                item.EarlyLeaveMinutes,
                item.OvertimeMinutes,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var requestsFrom = today.AddDays(-90);

        var requests = await db.AbsenceRequests
            .Where(item => item.EmployeeId == employeeId && item.ToDate >= requestsFrom)
            .OrderByDescending(item => item.FromDate)
            .Select(item => new
            {
                item.AbsenceTypeId,
                item.FromDate,
                item.ToDate,
                item.FromTime,
                item.ToTime,
                item.Status,
                item.Note,
                item.DecisionNote,
                item.DecidedBy,
                item.DecidedAt,
                item.CreatedAt,
            })
            .Take(SectionLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var absenceTypes = await db.AbsenceTypes.IgnoreQueryFilters()
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
            .ConfigureAwait(false);

        var deciders = await t.UserNamesAsync(requests.Select(item => item.DecidedBy).OfType<Guid>(), cancellationToken)
            .ConfigureAwait(false);

        return new
        {
            from = McpToolContext.Iso(from),
            to = McpToolContext.Iso(today),
            summary = new
            {
                byStatus = days.GroupBy(item => item.Status).ToDictionary(group => group.Key.ToString(), group => group.Count()),
                lateDays = days.Count(item => item.LateMinutes > 0),
                lateMinutes = days.Sum(item => item.LateMinutes),
                earlyLeaveDays = days.Count(item => item.EarlyLeaveMinutes > 0),
                workedHours = Math.Round(days.Sum(item => item.WorkedMinutes) / 60.0, 1),
                plannedHours = Math.Round(days.Sum(item => item.PlannedMinutes) / 60.0, 1),
                overtimeHours = Math.Round(days.Sum(item => item.OvertimeMinutes) / 60.0, 1),
            },
            days = days.Select(item => new
            {
                date = McpToolContext.Iso(item.LocalDate),
                status = item.Status,
                firstIn = McpToolContext.Local(item.FirstInAt),
                lastOut = McpToolContext.Local(item.LastOutAt),
                workedMinutes = item.WorkedMinutes,
                lateMinutes = item.LateMinutes > 0 ? item.LateMinutes : (int?)null,
                earlyLeaveMinutes = item.EarlyLeaveMinutes > 0 ? item.EarlyLeaveMinutes : (int?)null,
            }).ToList(),
            absenceRequests = requests.Select(item => new
            {
                type = absenceTypes.Name(item.AbsenceTypeId),
                from = McpToolContext.Iso(item.FromDate),
                to = McpToolContext.Iso(item.ToDate),
                fromTime = item.FromTime?.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                toTime = item.ToTime?.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                status = item.Status,
                note = item.Note,
                decisionNote = item.DecisionNote,
                decidedBy = deciders.Name(item.DecidedBy),
                decidedAt = McpToolContext.Local(item.DecidedAt),
                createdAt = McpToolContext.Local(item.CreatedAt),
            }).ToList(),
        };
    }

    // =====================================================================
    //  upcoming
    // =====================================================================

    [McpServerTool(Name = "upcoming", Title = "Yaqin sanalar",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Keyingi N kun ichidagi sanalar, sana bo'yicha tartiblangan: ishlayotgan xodimlarning tug'ilgan kunlari, sinov muddati "
        + "tugashlari; rekruting ruxsati bo'lsa — nomzodlar suhbatlari va rejalashtirilgan aloqa sanalari. Muddati o'tib ketgan "
        + "(yakunlanmagan) sinovlar va aloqalar alohida ko'rsatiladi. "
        + "Upcoming birthdays, probation ends, interviews and candidate contact dates in the next N days.")]
    public async Task<string> Upcoming(
        [Description("Necha kun oldinga qarash (sukut 14, ko'pi bilan 90).")] int? days = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.EmployeesView, "Xodimlar");

        var window = Math.Clamp(days ?? 14, 1, MaxUpcomingDays);
        var db = t.Db;
        var today = McpToolContext.Today;
        var until = today.AddDays(window - 1);
        var names = await EmployeeQueryKit.OrgNamesAsync(t, cancellationToken).ConfigureAwait(false);

        var items = new List<(DateOnly Date, int Order, object Item)>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        // ---- Tug'ilgan kunlar (faqat ishlayotganlar)
        var people = await db.Employees
            .Where(employee => employee.BirthDate != null
                               && db.Employments.Any(employment => employment.EmployeeId == employee.Id
                                                                   && employment.Status != EmploymentStatus.Terminated))
            .Select(employee => new { employee.Id, employee.FullName, employee.BirthDate })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var birthdays = people
            .Select(person => new { person.Id, person.FullName, Birth = person.BirthDate!.Value, Next = NextBirthday(person.BirthDate!.Value, today) })
            .Where(person => person.Next <= until)
            .ToList();

        var birthdayJobs = await EmployeeQueryKit.CurrentJobsAsync(t, [.. birthdays.Select(person => person.Id)], cancellationToken)
            .ConfigureAwait(false);

        foreach (var person in birthdays)
        {
            var job = birthdayJobs.GetValueOrDefault(person.Id);

            items.Add((person.Next, 0, new
            {
                date = McpToolContext.Iso(person.Next),
                kind = "Birthday",
                employeeId = person.Id,
                name = person.FullName.Full,
                turnsAge = person.Next.Year - person.Birth.Year,
                department = names.Departments.Name(job?.DepartmentId),
                position = names.Positions.Name(job?.PositionId),
            }));
        }

        counts["birthdays"] = birthdays.Count;

        // ---- Sinov muddatlari (davom etayotganlar; ishdan ketganlarniki emas)
        var probations = await (
                from probation in db.Probations
                join employment in db.Employments on probation.EmploymentId equals employment.Id
                join employee in db.Employees on employment.EmployeeId equals employee.Id
                where probation.Status == ProbationStatus.InProgress
                      && probation.EndDate <= until
                      && employment.Status != EmploymentStatus.Terminated
                orderby probation.EndDate
                select new
                {
                    employee.Id,
                    employee.FullName,
                    probation.StartDate,
                    probation.EndDate,
                    employment.LegalEntityId,
                    employment.DepartmentId,
                    employment.PositionId,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var probation in probations.Where(item => item.EndDate >= today))
        {
            items.Add((probation.EndDate, 1, new
            {
                date = McpToolContext.Iso(probation.EndDate),
                kind = "ProbationEnd",
                employeeId = probation.Id,
                name = probation.FullName.Full,
                probationStart = McpToolContext.Iso(probation.StartDate),
                legalEntity = names.LegalEntities.Name(probation.LegalEntityId),
                department = names.Departments.Name(probation.DepartmentId),
                position = names.Positions.Name(probation.PositionId),
            }));
        }

        var overdueProbations = probations.Where(item => item.EndDate < today).ToList();
        counts["probationEnds"] = probations.Count - overdueProbations.Count;
        counts["overdueProbations"] = overdueProbations.Count;

        object? overdueContacts = null;

        // ---- Rekruting: suhbatlar va aloqa sanalari
        if (t.Has(Permissions.RecruitingView))
        {
            var start = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset).ToUniversalTime();
            var end = new DateTimeOffset(until.AddDays(1).ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset).ToUniversalTime();

            var interviews = await db.Candidates
                .Where(candidate => candidate.Status == CandidateStatus.InProgress
                                    && candidate.InterviewAt != null
                                    && candidate.InterviewAt >= start
                                    && candidate.InterviewAt < end)
                .OrderBy(candidate => candidate.InterviewAt)
                .Select(candidate => new
                {
                    candidate.Id,
                    candidate.FullName,
                    candidate.Phone,
                    candidate.VacancyId,
                    candidate.CurrentStageId,
                    candidate.InterviewAt,
                })
                .Take(SectionLimit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // Rezervdagi nomzod ham: aloqa sanasi aynan «o'rin bo'shaganda
            // chaqiramiz» uchun qo'yiladi (veb-qo'ng'iroqchadagi qoida).
            var planned = db.Candidates
                .Where(candidate => (candidate.Status == CandidateStatus.InProgress || candidate.Status == CandidateStatus.Reserve)
                                    && candidate.NextContactDate != null);

            var upcomingContacts = await planned
                .Where(candidate => candidate.NextContactDate >= today && candidate.NextContactDate <= until)
                .OrderBy(candidate => candidate.NextContactDate)
                .Select(candidate => new
                {
                    candidate.Id,
                    candidate.FullName,
                    candidate.Phone,
                    candidate.VacancyId,
                    candidate.Status,
                    candidate.NextContactDate,
                })
                .Take(SectionLimit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var lateCount = await planned
                .CountAsync(candidate => candidate.NextContactDate < today, cancellationToken)
                .ConfigureAwait(false);

            var late = await planned
                .Where(candidate => candidate.NextContactDate < today)
                .OrderBy(candidate => candidate.NextContactDate)
                .Select(candidate => new
                {
                    candidate.Id,
                    candidate.FullName,
                    candidate.Phone,
                    candidate.VacancyId,
                    candidate.Status,
                    candidate.NextContactDate,
                })
                .Take(50)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var vacancyIds = interviews.Select(item => item.VacancyId)
                .Concat(upcomingContacts.Select(item => item.VacancyId))
                .Concat(late.Select(item => item.VacancyId))
                .Distinct()
                .ToList();

            var vacancies = await db.Vacancies.IgnoreQueryFilters()
                .Where(vacancy => vacancyIds.Contains(vacancy.Id))
                .Select(vacancy => new { vacancy.Id, vacancy.Title })
                .ToDictionaryAsync(vacancy => vacancy.Id, vacancy => vacancy.Title, cancellationToken)
                .ConfigureAwait(false);

            var stages = await db.PipelineStages.IgnoreQueryFilters()
                .Select(stage => new { stage.Id, stage.Name })
                .ToDictionaryAsync(stage => stage.Id, stage => stage.Name, cancellationToken)
                .ConfigureAwait(false);

            foreach (var interview in interviews)
            {
                var date = interview.InterviewAt!.Value.TashkentDate();

                items.Add((date, 2, new
                {
                    date = McpToolContext.Iso(date),
                    kind = "Interview",
                    candidateId = interview.Id,
                    name = interview.FullName.Full,
                    at = McpToolContext.Local(interview.InterviewAt),
                    vacancy = vacancies.Name(interview.VacancyId),
                    stage = stages.Name(interview.CurrentStageId),
                    phone = McpLookups.Phone(interview.Phone),
                }));
            }

            foreach (var contact in upcomingContacts)
            {
                items.Add((contact.NextContactDate!.Value, 3, new
                {
                    date = McpToolContext.Iso(contact.NextContactDate),
                    kind = "CandidateContact",
                    candidateId = contact.Id,
                    name = contact.FullName.Full,
                    candidateStatus = contact.Status,
                    vacancy = vacancies.Name(contact.VacancyId),
                    phone = McpLookups.Phone(contact.Phone),
                }));
            }

            counts["interviews"] = interviews.Count;
            counts["candidateContacts"] = upcomingContacts.Count;
            counts["overdueCandidateContacts"] = lateCount;

            overdueContacts = late.Count == 0
                ? null
                : late.Select(item => new
                {
                    date = McpToolContext.Iso(item.NextContactDate),
                    candidateId = item.Id,
                    name = item.FullName.Full,
                    candidateStatus = item.Status,
                    vacancy = vacancies.Name(item.VacancyId),
                    phone = McpLookups.Phone(item.Phone),
                }).ToList();
        }

        var ordered = items
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Order)
            .Select(item => item.Item)
            .ToList();

        var shown = ordered.Take(McpToolContext.MaxPageSize).ToList();

        return t.Json(
            new
            {
                from = McpToolContext.Iso(today),
                to = McpToolContext.Iso(until),
                counts,
                truncated = ordered.Count > shown.Count ? true : (bool?)null,
                items = shown,
                overdueProbations = overdueProbations.Count == 0
                    ? null
                    : overdueProbations.Take(50).Select(item => new
                    {
                        endDate = McpToolContext.Iso(item.EndDate),
                        employeeId = item.Id,
                        name = item.FullName.Full,
                        department = names.Departments.Name(item.DepartmentId),
                        position = names.Positions.Name(item.PositionId),
                    }).ToList(),
                overdueCandidateContacts = overdueContacts,
                note = t.Has(Permissions.RecruitingView) ? null : "Suhbatlar va nomzod aloqalari ko'rsatilmadi (recruiting.view ruxsati yo'q).",
            },
            shown.Count);
    }

    /// <summary>Keyingi tug'ilgan kun (bugun ham kiradi); 29-fevral kabisa bo'lmagan yilda 28-fevralga tushadi.</summary>
    private static DateOnly NextBirthday(DateOnly birth, DateOnly today)
    {
        var next = OnYear(birth, today.Year);
        return next >= today ? next : OnYear(birth, today.Year + 1);
    }

    private static DateOnly OnYear(DateOnly birth, int year) =>
        birth is { Month: 2, Day: 29 } && !DateTime.IsLeapYear(year)
            ? new DateOnly(year, 2, 28)
            : new DateOnly(year, birth.Month, birth.Day);

    // =====================================================================
    //  missing_documents
    // =====================================================================

    [McpServerTool(Name = "missing_documents", Title = "Yetishmayotgan hujjatlar",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Majburiy hujjat turlaridan biri yoki bir nechtasi yuklanmagan ishlayotgan xodimlar: har biri uchun yetishmayotgan "
        + "tur NOMLARI (fayllar emas). Yuridik shaxs, bo'lim yoki bitta hujjat turi bo'yicha toraytirish mumkin; tur bo'yicha "
        + "jami sonlar ham qaytadi. Sahifalangan. "
        + "Active employees missing one or more required document types (type names only).")]
    public async Task<string> MissingDocuments(
        [Description("Yuridik shaxs nomi yoki id.")] string? legalEntity = null,
        [Description("Bo'lim nomi yoki id — ichki bo'limlari ham kiradi.")] string? department = null,
        [Description("Faqat shu majburiy hujjat turi (nomi yoki id). Bo'sh — barcha majburiy turlar.")] string? documentType = null,
        [Description("Sahifa raqami, 1 dan.")] int? page = null,
        [Description("Sahifadagi qatorlar soni (sukut 50, ko'pi bilan 200).")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.EmployeesView, "Xodimlar");

        var db = t.Db;
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);
        var names = await EmployeeQueryKit.OrgNamesAsync(t, cancellationToken).ConfigureAwait(false);

        var required = await db.DocumentTypes
            .Where(type => type.IsRequired && type.IsActive)
            .OrderBy(type => type.Name)
            .Select(type => new { type.Id, type.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var requiredNames = required.ToDictionary(type => type.Id, type => type.Name);
        var onlyTypes = EmployeeQueryKit.Resolve(requiredNames, documentType, "Majburiy hujjat turi");

        if (onlyTypes is not null)
        {
            required = [.. required.Where(type => onlyTypes.Contains(type.Id))];
        }

        if (required.Count == 0)
        {
            return t.Json(new { note = "Majburiy deb belgilangan hujjat turi yo'q.", total = 0 }, 0);
        }

        var legalEntityIds = EmployeeQueryKit.Resolve(names.LegalEntities, legalEntity, "Yuridik shaxs");
        var departmentRoots = EmployeeQueryKit.Resolve(names.Departments, department, "Bo'lim");

        var employments = db.Employments.Where(employment => employment.Status != EmploymentStatus.Terminated);

        if (legalEntityIds is not null)
        {
            var list = legalEntityIds.ToList();
            employments = employments.Where(employment => list.Contains(employment.LegalEntityId));
        }

        if (departmentRoots is not null)
        {
            var list = (await EmployeeQueryKit.BranchAsync(t, departmentRoots, cancellationToken).ConfigureAwait(false)).ToList();
            employments = employments.Where(employment =>
                employment.DepartmentId != null && list.Contains(employment.DepartmentId.Value));
        }

        // O'chirilgan xodim (yumshoq) ro'yxatga tushmasin — filtr Employees orqali
        var jobs = await employments
            .Where(employment => db.Employees.Any(employee => employee.Id == employment.EmployeeId))
            .Select(employment => new McpJobRow(
                employment.Id,
                employment.EmployeeId,
                employment.LegalEntityId,
                employment.DepartmentId,
                employment.PositionId,
                employment.HireDate,
                employment.Status,
                employment.IsPrimary,
                employment.TerminationDate,
                employment.TerminationReasonId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var current = jobs
            .GroupBy(job => job.EmployeeId)
            .ToDictionary(group => group.Key, group => EmployeeQueryKit.Pick(group)!);

        var requiredIds = required.Select(type => type.Id).ToList();

        // Faqat (xodim, tur) juftligi — fayl nomi, kaliti, izohi o'qilmaydi
        var uploaded = await db.EmployeeDocuments
            .Where(document => requiredIds.Contains(document.DocumentTypeId))
            .Select(document => new { document.EmployeeId, document.DocumentTypeId })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var have = uploaded.ToLookup(item => item.EmployeeId, item => item.DocumentTypeId);

        var missing = current.Keys
            .Select(employeeId => new
            {
                EmployeeId = employeeId,
                Missing = requiredIds.Where(typeId => !have[employeeId].Contains(typeId)).ToList(),
            })
            .Where(row => row.Missing.Count > 0)
            .ToList();

        var fullNames = await t.EmployeeNamesAsync(missing.Select(row => row.EmployeeId), cancellationToken).ConfigureAwait(false);

        var ordered = missing
            .OrderByDescending(row => row.Missing.Count)
            .ThenBy(row => fullNames.GetValueOrDefault(row.EmployeeId), StringComparer.CurrentCulture)
            .ToList();

        var items = ordered
            .Skip((pageNo - 1) * size)
            .Take(size)
            .Select(row =>
            {
                var job = current[row.EmployeeId];

                return new
                {
                    employeeId = row.EmployeeId,
                    name = fullNames.GetValueOrDefault(row.EmployeeId),
                    legalEntity = names.LegalEntities.Name(job.LegalEntityId),
                    department = names.Departments.Name(job.DepartmentId),
                    position = names.Positions.Name(job.PositionId),
                    hireDate = McpToolContext.Iso(job.HireDate),
                    missing = row.Missing.Select(typeId => requiredNames[typeId]).ToList(),
                };
            })
            .ToList();

        return t.Json(
            new
            {
                requiredTypes = required.Select(type => type.Name).ToList(),
                employeesChecked = current.Count,
                total = ordered.Count,
                page = pageNo,
                pageSize = size,
                missingByType = required.Select(type => new
                {
                    type = type.Name,
                    employees = missing.Count(row => row.Missing.Contains(type.Id)),
                }).ToList(),
                items,
            },
            items.Count);
    }

    // =====================================================================
    //  Yordamchilar
    // =====================================================================

    private static StatusFilter ParseStatus(string? value, StatusFilter fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "active" => StatusFilter.Active,
            "probation" => StatusFilter.Probation,
            "terminated" => StatusFilter.Terminated,
            "all" => StatusFilter.All,
            _ => throw new McpException($"«status» qiymati noto'g'ri: «{value.Trim()}». Mumkin: active, probation, terminated, all."),
        };
    }

    private static List<string>? NamesOf(HashSet<Guid>? ids, Dictionary<Guid, string> map) =>
        ids is null ? null : [.. ids.Select(id => map.Name(id)).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>Xodimlarning til ko'nikmalari (raqamli daraja bilan).</summary>
    private async Task<Dictionary<Guid, List<LanguageSkill>>> LanguagesAsync(
        List<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        var db = t.Db;

        var rows = await (
                from competency in db.EmployeeCompetencies
                join skill in db.EmployeeCompetencyLanguages on competency.Id equals skill.EmployeeCompetencyId
                where employeeIds.Contains(competency.EmployeeId)
                select new { competency.EmployeeId, skill.Language, skill.Level })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(row => row.Level)
                    .Select(row => new LanguageSkill(row.Language, row.Level))
                    .ToList());
    }
}

/// <summary>Joriy ish joyi — xodimning tanlangan mehnat munosabati (proyeksiya).</summary>
internal sealed record McpJobRow(
    Guid Id,
    Guid EmployeeId,
    Guid LegalEntityId,
    Guid? DepartmentId,
    Guid? PositionId,
    DateOnly HireDate,
    EmploymentStatus Status,
    bool IsPrimary,
    DateOnly? TerminationDate,
    Guid? TerminationReasonId);

/// <summary>Tashkiliy nomlar lug'ati — bir chaqiruvda bir marta yuklanadi.</summary>
internal sealed record McpOrgNames(
    Dictionary<Guid, string> LegalEntities,
    Dictionary<Guid, string> Departments,
    Dictionary<Guid, string> Positions,
    Dictionary<Guid, string> TerminationReasons);

/// <summary>
/// Xodim vositalari (<c>EmployeeTools</c>, <c>ListTools</c>, <c>OrgTools</c>)
/// uchun umumiy so'rov bo'laklari: matnli qidiruv, nom bo'yicha topish,
/// bo'lim shoxi va joriy ish joyini tanlash.
/// </summary>
/// <remarks>
/// <para>
/// Qidiruv qoidasi veb-ro'yxatdagi bilan bir xil yig'ishni ishlatadi
/// (<c>қғҳўё</c> → <c>кгхуе</c>, transliteratsiya YO'Q), lekin ikki farqi bor:
/// JSHSHIR va pasport bo'yicha QIDIRILMAYDI (MCP roli u ustunlarni o'qiy
/// olmaydi, va moslik faktining o'zi ham ma'lumot — tashqi API'dagi bilan bir
/// xil qaror), so'zlar esa istalgan tartibda yozilishi mumkin — AI «Aziz
/// Karimov» deb so'rashi tabiiy.
/// </para>
/// <para>
/// Veb-qidiruvdagi xom SQL bu yerda ishlatilmaydi: u <c>SELECT x.*</c> qiladi
/// (yopiq ustunlar) va vositalarga xom SQL taqiqlangan — shart LINQ'da.
/// </para>
/// </remarks>
internal static class EmployeeQueryKit
{
    private const string Escape = "\\";

    /// <summary>Ism va nomlarni solishtirish uchun yig'ish: kichik harf + bir xil ko'rinadigan harflar.</summary>
    public static string Fold(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);

        foreach (var symbol in value.Trim().ToLowerInvariant())
        {
            _ = builder.Append(symbol switch
            {
                'қ' => 'к',
                'ғ' => 'г',
                'ҳ' => 'х',
                'ў' => 'у',
                'ё' => 'е',
                _ => symbol,
            });
        }

        return builder.ToString();
    }

    /// <summary>
    /// Xodimlarni matn bo'yicha toraytiradi: id, ism so'zlari (har biri F.I.Sh.
    /// ichida bo'lishi shart) va telefon raqamlari.
    /// </summary>
    public static async Task<IQueryable<Employee>> MatchTextAsync(
        McpToolContext t,
        IQueryable<Employee> employees,
        string? query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(t);

        if (string.IsNullOrWhiteSpace(query))
        {
            return employees;
        }

        var text = query.Trim();

        if (Guid.TryParse(text, out var id))
        {
            return employees.Where(employee => employee.Id == id);
        }

        var digits = new StringBuilder();

        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Any(char.IsLetter))
            {
                employees = WhereNameLike(employees, "%" + EscapeLike(Fold(token)) + "%");
            }
            else
            {
                foreach (var symbol in token.Where(char.IsAsciiDigit))
                {
                    _ = digits.Append(symbol);
                }
            }
        }

        if (digits.Length == 0)
        {
            return employees;
        }

        // Telefon qiymat obyekti (konvertor bilan satr) — uning «ichi» LINQ'da
        // SQL'ga tarjima qilinmaydi, xom SQL esa taqiqlangan. Shu sababli
        // raqamlar xotirada solishtiriladi: faqat id va ikki telefon ustuni,
        // bir necha ming qator.
        var wanted = digits.ToString();

        var phones = await t.Db.Employees
            .Where(employee => employee.Phone != null || employee.SecondPhone != null)
            .Select(employee => new { employee.Id, employee.Phone, employee.SecondPhone })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var matched = phones
            .Where(row => Digits(row.Phone?.Value).Contains(wanted, StringComparison.Ordinal)
                          || Digits(row.SecondPhone?.Value).Contains(wanted, StringComparison.Ordinal))
            .Select(row => row.Id)
            .ToList();

        return employees.Where(employee => matched.Contains(employee.Id));
    }

    /// <summary>
    /// F.I.Sh. yig'ilgan holda <c>ILIKE</c>. Katta va kichik harflar ikkalasi
    /// almashtiriladi — <c>lower()</c> kerak emas, <c>ILIKE</c> o'zi registrga befarq.
    /// </summary>
    private static IQueryable<Employee> WhereNameLike(IQueryable<Employee> employees, string pattern) =>
        employees.Where(employee => EF.Functions.ILike(
            (employee.FullName.LastName + " " + employee.FullName.FirstName + " " + (employee.FullName.MiddleName ?? string.Empty))
                .Replace("қ", "к").Replace("Қ", "К")
                .Replace("ғ", "г").Replace("Ғ", "Г")
                .Replace("ҳ", "х").Replace("Ҳ", "Х")
                .Replace("ў", "у").Replace("Ў", "У")
                .Replace("ё", "е").Replace("Ё", "Е"),
            pattern,
            Escape));

    /// <summary><c>ILIKE</c> naqshidagi maxsus belgilar ekranlanadi («50%» butun jadvalni olib kelmasin).</summary>
    public static string EscapeLike(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static string Digits(string? value) =>
        value is null ? string.Empty : new string([.. value.Where(char.IsAsciiDigit)]);

    /// <summary>
    /// Nom yoki id bo'yicha lug'atdan topadi. Bo'sh qiymat — <c>null</c> (filtr yo'q).
    /// </summary>
    /// <remarks>
    /// Avval aniq moslik (yig'ilgan holda), keyin «ichida». Bir xil nomli bir
    /// nechta yozuv (masalan turli bo'limlardagi «Бухгалтер») — hammasi olinadi;
    /// turli nomlarga mos kelsa — aniqlashtirish so'raladi.
    /// </remarks>
    public static HashSet<Guid>? Resolve(Dictionary<Guid, string> map, string? value, string what)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();

        if (Guid.TryParse(text, out var id))
        {
            return map.ContainsKey(id)
                ? [id]
                : throw new McpException($"{what}: «{text}» identifikatorli yozuv topilmadi.");
        }

        var wanted = Fold(text);

        var exact = map.Where(pair => string.Equals(Fold(pair.Value), wanted, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToHashSet();

        if (exact.Count > 0)
        {
            return exact;
        }

        var partial = map.Where(pair => Fold(pair.Value).Contains(wanted, StringComparison.Ordinal)).ToList();

        if (partial.Count == 0)
        {
            throw new McpException(
                $"{what}: «{text}» topilmadi. Nomlar bazada kirill yozuvida; ro'yxatni «org_structure» vositasi beradi.");
        }

        var names = partial.Select(pair => pair.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        if (names.Count == 1)
        {
            return [.. partial.Select(pair => pair.Key)];
        }

        throw new McpException(
            $"{what}: «{text}» bir nechta yozuvga mos keldi, aniqroq yozing yoki id bering: "
            + string.Join("; ", names.Take(15))
            + (names.Count > 15 ? $" … (yana {names.Count - 15})" : string.Empty));
    }

    /// <summary>Enum qiymatini nomidan o'qiydi (katta-kichik harf farqsiz); bo'sh — <c>null</c>.</summary>
    public static TEnum? ParseEnum<TEnum>(string? value, string name)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();

        // Faqat NOM qabul qilinadi: «Enum.TryParse» raqamni ham («7») jimgina o'tkazib yuborardi.
        var match = Enum.GetNames<TEnum>().FirstOrDefault(item => string.Equals(item, text, StringComparison.OrdinalIgnoreCase));

        return match is not null
            ? Enum.Parse<TEnum>(match)
            : throw new McpException($"«{name}» qiymati noto'g'ri: «{text}». Mumkin: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }

    /// <summary>Ixtiyoriy sana: bo'sh — <c>null</c>.</summary>
    public static DateOnly? OptionalDate(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? null : McpToolContext.Date(value, default, name);

    /// <summary>Bo'lim(lar) va ularning BARCHA ichki bo'limlari (veb-ro'yxatdagi bilan bir xil qoida).</summary>
    public static async Task<HashSet<Guid>> BranchAsync(
        McpToolContext t,
        IEnumerable<Guid> roots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(t);

        var parents = await t.Db.Departments.IgnoreQueryFilters()
            .Select(department => new { department.Id, department.ParentId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var children = parents
            .Where(row => row.ParentId != null)
            .ToLookup(row => row.ParentId!.Value, row => row.Id);

        var branch = new HashSet<Guid>();
        var queue = new Queue<Guid>(roots);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!branch.Add(current))
            {
                continue;
            }

            foreach (var child in children[current])
            {
                queue.Enqueue(child);
            }
        }

        return branch;
    }

    public static async Task<McpOrgNames> OrgNamesAsync(McpToolContext t, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(t);

        var legalEntities = await t.LegalEntitiesAsync(cancellationToken).ConfigureAwait(false);
        var departments = await t.DepartmentsAsync(cancellationToken).ConfigureAwait(false);
        var positions = await t.PositionsAsync(cancellationToken).ConfigureAwait(false);
        var reasons = await t.TerminationReasonsAsync(cancellationToken).ConfigureAwait(false);

        return new McpOrgNames(legalEntities, departments, positions, reasons);
    }

    /// <summary>Mehnat munosabatlari proyeksiyasi — faqat kerakli ustunlar.</summary>
    public static Task<List<McpJobRow>> JobsAsync(
        IQueryable<Employment> employments,
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken) =>
        employments
            .Where(employment => employeeIds.Contains(employment.EmployeeId))
            .Select(employment => new McpJobRow(
                employment.Id,
                employment.EmployeeId,
                employment.LegalEntityId,
                employment.DepartmentId,
                employment.PositionId,
                employment.HireDate,
                employment.Status,
                employment.IsPrimary,
                employment.TerminationDate,
                employment.TerminationReasonId))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Joriy ish joyi: ishlayotgan munosabat (asosiysi, keyin eng yangisi),
    /// bo'lmasa — eng oxirgi tugagani. Veb-ro'yxatdagi tartib bilan bir xil.
    /// </summary>
    public static McpJobRow? Pick(IEnumerable<McpJobRow> jobs)
    {
        var list = jobs as IReadOnlyCollection<McpJobRow> ?? [.. jobs];

        return list
                   .Where(job => job.Status != EmploymentStatus.Terminated)
                   .OrderByDescending(job => job.IsPrimary)
                   .ThenByDescending(job => job.HireDate)
                   .FirstOrDefault()
               ?? list
                   .OrderByDescending(job => job.TerminationDate)
                   .ThenByDescending(job => job.HireDate)
                   .FirstOrDefault();
    }

    /// <summary>Har bir xodim uchun joriy ish joyi.</summary>
    public static async Task<Dictionary<Guid, McpJobRow>> CurrentJobsAsync(
        McpToolContext t,
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(t);

        if (employeeIds.Count == 0)
        {
            return [];
        }

        var jobs = await JobsAsync(t.Db.Employments, employeeIds, cancellationToken).ConfigureAwait(false);

        return jobs
            .GroupBy(job => job.EmployeeId)
            .ToDictionary(group => group.Key, group => Pick(group)!);
    }

    /// <summary>Ish joyining qisqa ko'rinishi (ro'yxat qatorlari uchun).</summary>
    public static object? Summary(McpJobRow? job, McpOrgNames names)
    {
        ArgumentNullException.ThrowIfNull(names);

        if (job is null)
        {
            return null;
        }

        var terminated = job.Status == EmploymentStatus.Terminated;

        return new
        {
            legalEntity = names.LegalEntities.Name(job.LegalEntityId),
            department = names.Departments.Name(job.DepartmentId),
            position = names.Positions.Name(job.PositionId),
            status = job.Status,
            hireDate = McpToolContext.Iso(job.HireDate),
            terminationDate = terminated ? McpToolContext.Iso(job.TerminationDate) : null,
            terminationReason = terminated ? names.TerminationReasons.Name(job.TerminationReasonId) : null,
        };
    }

    /// <summary>Bugun kuchda bo'lgan belgilar (veb-ro'yxatdagi shart: tugash sanasi bugundan keyin).</summary>
    public static async Task<Dictionary<Guid, List<EmployeeFlagType>>> ActiveFlagsAsync(
        McpToolContext t,
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(t);

        if (employeeIds.Count == 0)
        {
            return [];
        }

        var today = McpToolContext.Today;

        var rows = await t.Db.EmployeeFlags
            .Where(flag => employeeIds.Contains(flag.EmployeeId)
                           && flag.StartDate <= today
                           && (flag.EndDate == null || flag.EndDate > today))
            .Select(flag => new { flag.EmployeeId, flag.Type })
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Type).Order().ToList());
    }
}
