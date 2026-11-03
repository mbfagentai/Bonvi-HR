using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Bonvi.Api.Mcp.Tools;

/// <summary>Blankalar — HR hujjat blankalari, ularning to'liqligi va bo'shliqlar (faqat o'qish), SPEC-blankalar §11.</summary>
/// <remarks>
/// <para>
/// Hamma so'rov PROYEKSIYA bilan: <c>blank_attachments.file_name</c>/<c>storage_key</c>,
/// <c>blank_submissions.final_pdf_storage_key</c> va <c>blank_submission_steps.signature_storage_key</c>
/// o'qish roliga ochilmagan — butun entity o'qilsa PostgreSQL rad etadi. Fayl maydonlari
/// faqat SONI va TURI bilan beriladi; imzo rasmi va qadamning <c>answers_patch</c> i
/// (huquqiy iz, javoblarning nusxasi) umuman o'qilmaydi.
/// </para>
/// <para>
/// Javoblar xom JSON emas — shablon ta'rifi bo'yicha YORLIQLANADI (bo'lim, maydon nomi,
/// tanlov yorlig'i, havola — ism): AI kalitlarni taxmin qilmasin.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed partial class BlankTools(McpToolContext t)
{
    private const string Area = "Blankalar";

    /// <summary><c>blank_gaps</c> da har bir qoida bo'yicha ko'pi bilan nechta qator beriladi (jami soni baribir to'liq).</summary>
    private const int GapLimit = McpToolContext.MaxPageSize;

    /// <summary>Sinov tugashiga necha kun qolganda HR-F07 kutiladi (SPEC §11: «today+5»).</summary>
    private const int ProbationLeadDays = 5;

    private const string F06 = "HR-F06";
    private const string F07 = "HR-F07";
    private const string F08 = "HR-F08";
    private const string F09 = "HR-F09";

    // ─────────────────────────────────────────────────────────────────
    //  blank_templates
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "blank_templates", Title = "Blanka shablonlari",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("HR blanka shablonlari (HR-F06 moslashtirish rejasi, HR-F07 sinov bahosi, HR-F08 chiqish suhbati, HR-F09 ishlarni "
        + "topshirish-qabul qilish akti, HR-F13 mehnat intizomi va boshqalar): kodi, nomi, kim haqida (xodim/nomzod), amaldagi versiya, maydonlar "
        + "(bo'lim, nomi, turi, majburiyligi; jadval ustunlari) va imzo qadamlari (kim imzolaydi). Shablon kodi blanks va "
        + "blank_gaps uchun kerak. Blank templates with their fields and approval steps.")]
    public async Task<string> BlankTemplates(CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.BlanksView, Area);

        var db = t.Db;

        var templates = await db.BlankTemplates
            .OrderBy(template => template.SortOrder)
            .ThenBy(template => template.Code)
            .Select(template => new { template.Id, template.Code, template.Title, template.SubjectKind, template.IsActive })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ids = templates.Select(template => template.Id).ToList();

        // Har shablonning eng so'nggi E'LON QILINGAN versiyasi — yangi blanka shu bilan ochiladi.
        var published = await db.BlankTemplateVersions
            .Where(version => ids.Contains(version.TemplateId) && version.Status == BlankTemplateVersionStatus.Published)
            .Select(version => new { version.TemplateId, version.Version, version.Definition, version.PublishedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var current = published
            .GroupBy(version => version.TemplateId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(version => version.PublishedAt).First());

        var items = templates.Select(template =>
        {
            var version = current.GetValueOrDefault(template.Id);
            var definition = BlankDefinition.Parse(version?.Definition);

            return new
            {
                code = template.Code,
                title = template.Title,
                subject = template.SubjectKind,
                active = template.IsActive,
                version = version?.Version,
                fields = definition.Fields.Select(field => new
                {
                    id = field.Id,
                    label = field.Label,
                    type = field.Type,
                    required = field.Required,
                    section = field.SectionTitle,
                    columns = field.Columns.Count == 0
                        ? null
                        : field.Columns.Select(column => new
                        {
                            id = column.Id,
                            label = column.Label,
                            type = column.Type,
                            required = column.Required,
                        }),
                }),
                steps = definition.Steps.Select(step => new { id = step.Id, title = step.Title, actor = step.Actor, order = step.Order }),
            };
        }).ToList();

        return t.Json(new { total = items.Count, items }, items.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    //  blanks
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "blanks", Title = "Blankalar ro'yxati",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("To'ldirilgan blankalar ro'yxati (yangilari birinchi): shablon, raqam, holat (Draft — qoralama, InApproval — "
        + "imzolanmoqda, Approved — tasdiqlangan, Rejected — rad etilgan, Cancelled — bekor qilingan), kim haqida, bo'lim, hozir kim "
        + "imzolashi kerak, to'ldirilmagan majburiy maydonlar soni (missingCount), muddat va muddati o'tganmi. Filtrlar: shablon, holat, "
        + "xodim, bo'lim (ichki bo'limlari bilan), yaratilgan sana, faqat to'liq emaslar, faqat muddati o'tganlar. Javoblar va "
        + "fayllar bu yerda yo'q — to'liq blanka: blank. Lists filled blanks with status, subject, pending signers and completeness; paged.")]
    public async Task<string> Blanks(
        [Description("Shablon kodi, masalan HR-F07 (blank_templates dan).")] string? templateCode = null,
        [Description("Holat: Draft, InApproval, Approved, Rejected yoki Cancelled. Bo'sh — hammasi.")] string? status = null,
        [Description("Xodim identifikatori (search_employees natijasidagi id) — shu xodim haqidagi blankalar.")] Guid? employeeId = null,
        [Description("Bo'lim identifikatori — ichki bo'limlari bilan birga.")] Guid? departmentId = null,
        [Description("Yaratilgan sana (dan), YYYY-MM-DD. Ikkalasi bo'sh — sana filtri yo'q.")] string? from = null,
        [Description("Yaratilgan sana (gacha), YYYY-MM-DD. Ko'pi bilan 366 kunlik oraliq.")] string? to = null,
        [Description("true — faqat to'ldirilmagan majburiy maydoni borlar; false — faqat to'liqlari.")] bool? incomplete = null,
        [Description("true — faqat muddati o'tgan va hali yakunlanmaganlar (qoralama yoki imzolanmoqda); false — qolganlari.")] bool? overdue = null,
        [Description("Sahifa raqami (1 dan).")] int? page = null,
        [Description("Sahifa hajmi (sukut 50, ko'pi bilan 200).")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.BlanksView, Area);

        var db = t.Db;
        var pageNo = McpToolContext.Page(page);
        var size = McpToolContext.PageSize(pageSize);
        var today = McpToolContext.Today;

        var query = db.BlankSubmissions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(templateCode))
        {
            var code = templateCode.Trim().ToUpperInvariant();
            query = query.Where(submission => submission.TemplateCode == code);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = EmploymentTools.ParseEnum<BlankSubmissionStatus>(status, "status");
            query = query.Where(submission => submission.Status == wanted);
        }

        if (employeeId is { } employee)
        {
            query = query.Where(submission => submission.EmployeeId == employee);
        }

        var branch = await EmploymentTools.DepartmentBranchAsync(t, departmentId, cancellationToken).ConfigureAwait(false);

        if (branch is not null)
        {
            query = query.Where(submission => submission.DepartmentId != null && branch.Contains(submission.DepartmentId.Value));
        }

        if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
        {
            var (start, end) = McpToolContext.Range(from, to);
            var fromUtc = StartOfDayUtc(start);
            var toUtc = StartOfDayUtc(end.AddDays(1));
            query = query.Where(submission => submission.CreatedAt >= fromUtc && submission.CreatedAt < toUtc);
        }

        if (incomplete is { } onlyIncomplete)
        {
            query = onlyIncomplete
                ? query.Where(submission => submission.MissingCount > 0)
                : query.Where(submission => submission.MissingCount == 0);
        }

        if (overdue is { } onlyOverdue)
        {
            query = onlyOverdue
                ? query.Where(submission => submission.DueDate < today
                    && (submission.Status == BlankSubmissionStatus.Draft || submission.Status == BlankSubmissionStatus.InApproval))
                : query.Where(submission => !(submission.DueDate < today
                    && (submission.Status == BlankSubmissionStatus.Draft || submission.Status == BlankSubmissionStatus.InApproval)));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await query
            .OrderByDescending(submission => submission.CreatedAt)
            .ThenByDescending(submission => submission.Id)
            .Skip((pageNo - 1) * size)
            .Take(size)
            .Select(Header)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ids = rows.Select(row => row.Id).ToList();

        var openSteps = await db.BlankSubmissionSteps
            .Where(step => ids.Contains(step.SubmissionId) && step.State == BlankStepState.Open)
            .OrderBy(step => step.OrderNo)
            .ThenBy(step => step.StepCode)
            .Select(step => new { step.SubmissionId, step.StepCode, step.Title, step.ActorRole, step.DueDate })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lookups = await LookupsAsync(rows, cancellationToken).ConfigureAwait(false);

        var items = rows.Select(row => new
        {
            id = row.Id,
            templateCode = row.TemplateCode,
            templateTitle = lookups.Templates.Name(row.TemplateId),
            number = row.Number,
            status = row.Status,
            subject = lookups.Subject(row),
            department = lookups.Departments.Name(row.DepartmentId),
            currentSteps = openSteps
                .Where(step => step.SubmissionId == row.Id)
                .Select(step => new
                {
                    stepCode = step.StepCode,
                    title = step.Title,
                    actorRole = step.ActorRole,
                    dueDate = McpToolContext.Iso(step.DueDate),
                }),
            missingCount = row.MissingCount,
            dueDate = McpToolContext.Iso(row.DueDate),
            overdue = IsOverdue(row, today),
            createdAt = McpToolContext.Local(row.CreatedAt),
            submittedAt = McpToolContext.Local(row.SubmittedAt),
            completedAt = McpToolContext.Local(row.CompletedAt),
        }).ToList();

        return t.Json(new { total, page = pageNo, pageSize = size, items }, items.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    //  blank
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "blank", Title = "Blanka",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Bitta blanka to'liq: sarlavha (shablon, raqam, holat, kim haqida, bo'lim, muddat), javoblar bo'lim va maydon NOMI "
        + "bilan (tanlovlar — yorlig'i, havolalar — ism, jadvallar — qatorma-qator, fayl maydonlari — faqat soni va turi), hisoblangan "
        + "qiymatlar (masalan o'rtacha ball), to'ldirilmagan majburiy maydonlar (missing) va imzo qadamlari (kim, holat, qaror, izoh, "
        + "imzolagan, qachon, qanday — elektron yoki qo'lda). Fayl, fayl nomi va imzo rasmi berilmaydi. "
        + "One blank with labelled answers, computed values, missing fields and signature steps.")]
    public async Task<string> Blank(
        [Description("Blanka identifikatori (blanks natijasidagi id).")] string id,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.BlanksView, Area);

        if (!Guid.TryParse(id?.Trim(), out var submissionId))
        {
            throw new McpException("«id» — blanka identifikatori (GUID) bo'lishi kerak.");
        }

        var db = t.Db;

        var row = await db.BlankSubmissions
            .Where(submission => submission.Id == submissionId)
            .Select(submission => new
            {
                Header = new SubmissionHeader(
                    submission.Id,
                    submission.TemplateId,
                    submission.TemplateCode,
                    submission.Number,
                    submission.Status,
                    submission.SubjectKind,
                    submission.EmployeeId,
                    submission.CandidateId,
                    submission.DepartmentId,
                    submission.MissingCount,
                    submission.DueDate,
                    submission.CreatedAt,
                    submission.SubmittedAt,
                    submission.CompletedAt),
                submission.TemplateVersionId,
                submission.Answers,
                submission.Computed,
                submission.Missing,
                submission.CancelledAt,
                submission.CancelReason,
                submission.ReturnComment,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new McpException("Blanka topilmadi.");

        var version = await db.BlankTemplateVersions
            .Where(item => item.Id == row.TemplateVersionId)
            .Select(item => new { item.Version, item.Definition })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var definition = BlankDefinition.Parse(version?.Definition);

        // Imzo rasmi kaliti va answers_patch — o'qilmaydi (§11).
        var steps = await db.BlankSubmissionSteps
            .Where(step => step.SubmissionId == submissionId)
            .OrderBy(step => step.OrderNo)
            .ThenBy(step => step.StepCode)
            .Select(step => new
            {
                step.StepCode,
                step.OrderNo,
                step.Title,
                step.ActorRole,
                step.Mode,
                step.State,
                step.Decision,
                step.Comment,
                step.DueDate,
                step.Method,
                step.SignerName,
                step.SignerEmployeeId,
                step.ActedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Fayl: faqat qaysi maydonda va qanday turda — nomi va ombor kaliti emas.
        var files = (await db.BlankAttachments
                .Where(attachment => attachment.SubmissionId == submissionId && attachment.RemovedAt == null)
                .Select(attachment => new { attachment.FieldPath, attachment.Kind })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .GroupBy(attachment => attachment.FieldPath, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new FileSummary(group.Count(), [.. group.Select(item => item.Kind).Distinct().Order()]),
                StringComparer.Ordinal);

        var lookups = await LookupsAsync([row.Header], cancellationToken).ConfigureAwait(false);
        var signers = await t.EmployeeNamesAsync(
                steps.Where(step => step.SignerName is null && step.SignerEmployeeId != null).Select(step => step.SignerEmployeeId!.Value),
                cancellationToken)
            .ConfigureAwait(false);

        var answers = ParseObject(row.Answers);
        var computed = ParseObject(row.Computed);
        var header = row.Header;

        var result = new
        {
            id = header.Id,
            templateCode = header.TemplateCode,
            templateTitle = lookups.Templates.Name(header.TemplateId),
            templateVersion = version?.Version,
            number = header.Number,
            status = header.Status,
            subject = lookups.Subject(header),
            department = lookups.Departments.Name(header.DepartmentId),
            dueDate = McpToolContext.Iso(header.DueDate),
            overdue = IsOverdue(header, McpToolContext.Today),
            createdAt = McpToolContext.Local(header.CreatedAt),
            submittedAt = McpToolContext.Local(header.SubmittedAt),
            completedAt = McpToolContext.Local(header.CompletedAt),
            cancelledAt = McpToolContext.Local(row.CancelledAt),
            cancelReason = row.CancelReason,
            returnComment = row.ReturnComment,
            answers = LabelledAnswers(definition, answers, computed, files),
            computed = LabelledComputed(definition, computed),
            missingCount = header.MissingCount,
            missing = MissingLabels(definition, row.Missing),
            steps = steps.Select(step => new
            {
                stepCode = step.StepCode,
                order = step.OrderNo,
                title = step.Title,
                actor = step.ActorRole,
                mode = step.Mode,
                state = step.State,
                decision = definition.DecisionLabel(step.StepCode, step.Decision),
                comment = step.Comment,
                dueDate = McpToolContext.Iso(step.DueDate),
                method = step.Method,
                signerName = step.SignerName ?? signers.Name(step.SignerEmployeeId),
                actedAt = McpToolContext.Local(step.ActedAt),
            }),
        };

        return t.Json(result, 1);
    }

    // ─────────────────────────────────────────────────────────────────
    //  blank_gaps
    // ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "blank_gaps", Title = "Blanka bo'shliqlari",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Qaysi blanka yo'q yoki chala — qoidalar bo'yicha: oraliqda ishga olingan, lekin HR-F06 (moslashtirish rejasi) "
        + "yo'q; sinovi 5 kun ichida tugaydigan (yoki tugagan), lekin HR-F07 (sinov bahosi) yo'q; oraliqda ishdan ketgan, lekin HR-F08 "
        + "(chiqish suhbati) yo'q / tasdiqlangan HR-F09 (ishlarni topshirish akti) yo'q; HR-F07 tasdiqlangan, lekin sinov tugash sanasidan "
        + "keyin ham «davom etmoqda»; muddati o'tgan blankalar; tasdiqlangan, lekin majburiy maydoni bo'sh blankalar. Har qoidada "
        + "jami soni va ko'pi bilan 200 qator. Bu faqat hisobot — tizim hech narsani o'zgartirmaydi. "
        + "Rule-based report of missing or incomplete HR blanks.")]
    public async Task<string> BlankGaps(
        [Description("Oraliq boshi (ishga olish / ishdan ketish sanasi), YYYY-MM-DD. Bo'sh — so'nggi 30 kun.")] string? from = null,
        [Description("Oraliq oxiri, YYYY-MM-DD. Bo'sh — bugun. Ko'pi bilan 366 kun.")] string? to = null,
        [Description("Bo'lim identifikatori — ichki bo'limlari bilan birga.")] Guid? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.BlanksView, Area);

        var db = t.Db;
        var (start, end) = McpToolContext.Range(from, to);
        var today = McpToolContext.Today;
        var probationHorizon = today.AddDays(ProbationLeadDays);
        var branch = await EmploymentTools.DepartmentBranchAsync(t, departmentId, cancellationToken).ConfigureAwait(false);

        // Bekor qilingan blanka «bor» hisoblanmaydi.
        var live = db.BlankSubmissions.Where(submission => submission.Status != BlankSubmissionStatus.Cancelled);

        var employments = db.Employments.AsQueryable();
        var submissions = db.BlankSubmissions.AsQueryable();

        if (branch is not null)
        {
            employments = employments.Where(employment =>
                employment.DepartmentId != null && branch.Contains(employment.DepartmentId.Value));
            submissions = submissions.Where(submission =>
                submission.DepartmentId != null && branch.Contains(submission.DepartmentId.Value));
        }

        // Blanka mehnat munosabatiga bog'lanadi (§2); munosabatsiz eski yozuv — xodim bo'yicha.
        var hiredWithoutF06 = await EmploymentGapAsync(
            employments.Where(employment => employment.HireDate >= start && employment.HireDate <= end
                && !live.Any(submission => submission.TemplateCode == F06
                    && (submission.EmploymentId == employment.Id
                        || (submission.EmploymentId == null && submission.EmployeeId == employment.EmployeeId)))),
            byTermination: false,
            cancellationToken).ConfigureAwait(false);

        var terminatedWithoutF08 = await EmploymentGapAsync(
            employments.Where(employment => employment.TerminationDate >= start && employment.TerminationDate <= end
                && !live.Any(submission => submission.TemplateCode == F08
                    && (submission.EmploymentId == employment.Id
                        || (submission.EmploymentId == null && submission.EmployeeId == employment.EmployeeId)))),
            byTermination: true,
            cancellationToken).ConfigureAwait(false);

        var terminatedWithoutApprovedF09 = await EmploymentGapAsync(
            employments.Where(employment => employment.TerminationDate >= start && employment.TerminationDate <= end
                && !db.BlankSubmissions.Any(submission => submission.TemplateCode == F09
                    && submission.Status == BlankSubmissionStatus.Approved
                    && (submission.EmploymentId == employment.Id
                        || (submission.EmploymentId == null && submission.EmployeeId == employment.EmployeeId)))),
            byTermination: true,
            cancellationToken).ConfigureAwait(false);

        // Sinov hali davom etmoqda, odam ishda va tugashiga ≤ 5 kun (yoki o'tib ketgan) — HR-F07 kerak.
        var probationQuery =
            from probation in db.Probations
            join employment in employments on probation.EmploymentId equals employment.Id
            where probation.Status == ProbationStatus.InProgress
                && probation.EndDate <= probationHorizon
                && employment.Status != EmploymentStatus.Terminated
                && !live.Any(submission => submission.TemplateCode == F07
                    && (submission.EmploymentId == employment.Id
                        || (submission.EmploymentId == null && submission.EmployeeId == employment.EmployeeId)))
            select new { employment.EmployeeId, EmploymentId = employment.Id, employment.DepartmentId, probation.EndDate };

        var probationTotal = await probationQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var probationRows = await probationQuery
            .OrderBy(row => row.EndDate)
            .ThenBy(row => row.EmploymentId)
            .Take(GapLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // HR-F07 tasdiqlangan, lekin sinov yozuvi yopilmagan (buyruq kerak, §2) — sana ham o'tgan.
        var f07Approved = submissions.Where(submission => submission.TemplateCode == F07
            && submission.Status == BlankSubmissionStatus.Approved
            && db.Probations.Any(probation => probation.EmploymentId == submission.EmploymentId
                && probation.Status == ProbationStatus.InProgress
                && probation.EndDate < today));

        var f07ApprovedProbationOpen = await SubmissionGapAsync(f07Approved, cancellationToken).ConfigureAwait(false);

        var overdueBlanks = await SubmissionGapAsync(
            submissions.Where(submission => submission.DueDate < today
                && (submission.Status == BlankSubmissionStatus.Draft || submission.Status == BlankSubmissionStatus.InApproval)),
            cancellationToken).ConfigureAwait(false);

        var incompleteApproved = await SubmissionGapAsync(
            submissions.Where(submission => submission.Status == BlankSubmissionStatus.Approved && submission.MissingCount > 0),
            cancellationToken).ConfigureAwait(false);

        // Ism va bo'lim — hamma qoidalar uchun bitta so'rov bilan.
        var employeeIds = hiredWithoutF06.Rows.Select(row => row.EmployeeId)
            .Concat(terminatedWithoutF08.Rows.Select(row => row.EmployeeId))
            .Concat(terminatedWithoutApprovedF09.Rows.Select(row => row.EmployeeId))
            .Concat(probationRows.Select(row => row.EmployeeId));

        var names = await t.EmployeeNamesAsync(employeeIds, cancellationToken).ConfigureAwait(false);
        var departments = await t.DepartmentsAsync(cancellationToken).ConfigureAwait(false);
        var lookups = await LookupsAsync(
                [.. f07ApprovedProbationOpen.Rows, .. overdueBlanks.Rows, .. incompleteApproved.Rows],
                cancellationToken)
            .ConfigureAwait(false);

        object EmploymentRule(string rule, string title, EmploymentGap gap) => new
        {
            rule,
            title,
            total = gap.Total,
            items = gap.Rows.Select(row => new
            {
                employeeId = row.EmployeeId,
                fullName = names.Name(row.EmployeeId),
                department = departments.Name(row.DepartmentId),
                hireDate = McpToolContext.Iso(row.HireDate),
                terminationDate = McpToolContext.Iso(row.TerminationDate),
            }),
        };

        object SubmissionRule(string rule, string title, SubmissionGap gap) => new
        {
            rule,
            title,
            total = gap.Total,
            items = gap.Rows.Select(row => new
            {
                id = row.Id,
                templateCode = row.TemplateCode,
                templateTitle = lookups.Templates.Name(row.TemplateId),
                number = row.Number,
                status = row.Status,
                subject = lookups.Subject(row),
                department = lookups.Departments.Name(row.DepartmentId),
                dueDate = McpToolContext.Iso(row.DueDate),
                missingCount = row.MissingCount,
            }),
        };

        var rules = new[]
        {
            EmploymentRule("hired_without_f06", "Oraliqda ishga olingan, HR-F06 (moslashtirish rejasi) ochilmagan", hiredWithoutF06),
            new
            {
                rule = "probation_ending_without_f07",
                title = $"Sinov {ProbationLeadDays} kun ichida tugaydi yoki tugagan, HR-F07 (sinov bahosi) ochilmagan",
                total = probationTotal,
                items = probationRows.Select(row => new
                {
                    employeeId = row.EmployeeId,
                    fullName = names.Name(row.EmployeeId),
                    department = departments.Name(row.DepartmentId),
                    probationEndDate = McpToolContext.Iso(row.EndDate),
                    daysLeft = row.EndDate.DayNumber - today.DayNumber,
                }),
            },
            EmploymentRule("terminated_without_f08", "Oraliqda ishdan ketgan, HR-F08 (chiqish suhbati) ochilmagan", terminatedWithoutF08),
            EmploymentRule(
                "terminated_without_approved_f09",
                "Oraliqda ishdan ketgan, tasdiqlangan HR-F09 (ishlarni topshirish akti) yo'q",
                terminatedWithoutApprovedF09),
            SubmissionRule(
                "f07_approved_probation_open",
                "HR-F07 tasdiqlangan, lekin sinov tugash sanasidan keyin ham «davom etmoqda» (sinov natijasi buyrug'i kerak)",
                f07ApprovedProbationOpen),
            SubmissionRule("overdue", "Muddati o'tgan, hali yakunlanmagan blankalar", overdueBlanks),
            SubmissionRule("incomplete_approved", "Tasdiqlangan, lekin majburiy maydoni bo'sh blankalar", incompleteApproved),
        };

        var rows = hiredWithoutF06.Rows.Count + terminatedWithoutF08.Rows.Count + terminatedWithoutApprovedF09.Rows.Count
            + probationRows.Count + f07ApprovedProbationOpen.Rows.Count + overdueBlanks.Rows.Count + incompleteApproved.Rows.Count;

        return t.Json(
            new
            {
                from = McpToolContext.Iso(start),
                to = McpToolContext.Iso(end),
                today = McpToolContext.Iso(today),
                rules,
            },
            rows);
    }

    // ─────────────────────────────────────────────────────────────────
    //  Yordamchilar — so'rovlar
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Ro'yxat qatori — faqat ochiq ustunlar.</summary>
    private sealed record SubmissionHeader(
        Guid Id,
        Guid TemplateId,
        string TemplateCode,
        int? Number,
        BlankSubmissionStatus Status,
        BlankSubjectKind SubjectKind,
        Guid? EmployeeId,
        Guid? CandidateId,
        Guid? DepartmentId,
        int MissingCount,
        DateOnly? DueDate,
        DateTimeOffset CreatedAt,
        DateTimeOffset? SubmittedAt,
        DateTimeOffset? CompletedAt);

    private static readonly System.Linq.Expressions.Expression<Func<BlankSubmission, SubmissionHeader>> Header =
        submission => new SubmissionHeader(
            submission.Id,
            submission.TemplateId,
            submission.TemplateCode,
            submission.Number,
            submission.Status,
            submission.SubjectKind,
            submission.EmployeeId,
            submission.CandidateId,
            submission.DepartmentId,
            submission.MissingCount,
            submission.DueDate,
            submission.CreatedAt,
            submission.SubmittedAt,
            submission.CompletedAt);

    private sealed record EmploymentRow(Guid EmployeeId, Guid? DepartmentId, DateOnly HireDate, DateOnly? TerminationDate);

    private sealed record EmploymentGap(int Total, IReadOnlyList<EmploymentRow> Rows);

    private sealed record SubmissionGap(int Total, IReadOnlyList<SubmissionHeader> Rows);

    private sealed record FileSummary(int Count, IReadOnlyList<BlankAttachmentKind> Kinds);

    private static async Task<EmploymentGap> EmploymentGapAsync(
        IQueryable<Employment> query,
        bool byTermination,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var ordered = byTermination
            ? query.OrderBy(employment => employment.TerminationDate)
            : query.OrderBy(employment => employment.HireDate);

        var rows = await ordered
            .ThenBy(employment => employment.Id)
            .Take(GapLimit)
            .Select(employment => new EmploymentRow(
                employment.EmployeeId,
                employment.DepartmentId,
                employment.HireDate,
                employment.TerminationDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new EmploymentGap(total, rows);
    }

    private static async Task<SubmissionGap> SubmissionGapAsync(
        IQueryable<BlankSubmission> query,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await query
            .OrderBy(submission => submission.DueDate)
            .ThenByDescending(submission => submission.CreatedAt)
            .ThenBy(submission => submission.Id)
            .Take(GapLimit)
            .Select(Header)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SubmissionGap(total, rows);
    }

    /// <summary>Sarlavhalar uchun nomlar: shablon, bo'lim, xodim va nomzod ismi.</summary>
    private sealed class Lookups(
        Dictionary<Guid, string> templates,
        Dictionary<Guid, string> departments,
        Dictionary<Guid, string> employees,
        Dictionary<Guid, string> candidates)
    {
        public Dictionary<Guid, string> Templates { get; } = templates;

        public Dictionary<Guid, string> Departments { get; } = departments;

        public SubjectInfo? Subject(SubmissionHeader row) => row.SubjectKind switch
        {
            BlankSubjectKind.Employee => new SubjectInfo(row.SubjectKind, row.EmployeeId, employees.Name(row.EmployeeId)),
            BlankSubjectKind.Candidate => new SubjectInfo(row.SubjectKind, row.CandidateId, candidates.Name(row.CandidateId)),
            _ => null,
        };
    }

    /// <summary>Blanka kim haqida: xodim yoki nomzod (<c>None</c> — sub'yektsiz, chiqmaydi).</summary>
    private sealed record SubjectInfo(BlankSubjectKind Kind, Guid? Id, string? FullName);

    private async Task<Lookups> LookupsAsync(IReadOnlyCollection<SubmissionHeader> rows, CancellationToken cancellationToken)
    {
        var db = t.Db;

        var templates = await db.BlankTemplates
            .Select(template => new { template.Id, template.Title })
            .ToDictionaryAsync(template => template.Id, template => template.Title, cancellationToken)
            .ConfigureAwait(false);

        var departments = await t.DepartmentsAsync(cancellationToken).ConfigureAwait(false);

        var employees = await t.EmployeeNamesAsync(
                rows.Where(row => row.EmployeeId != null).Select(row => row.EmployeeId!.Value),
                cancellationToken)
            .ConfigureAwait(false);

        var candidateIds = rows.Where(row => row.CandidateId != null).Select(row => row.CandidateId!.Value).Distinct().ToList();

        // Faqat ism ustunlari (rasm, Telegram chat — yopiq).
        var candidates = candidateIds.Count == 0
            ? []
            : (await db.Candidates.IgnoreQueryFilters()
                    .Where(candidate => candidateIds.Contains(candidate.Id))
                    .Select(candidate => new { candidate.Id, candidate.FullName })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .ToDictionary(candidate => candidate.Id, candidate => candidate.FullName.Full);

        return new Lookups(templates, departments, employees, candidates);
    }

    private static bool IsOverdue(SubmissionHeader row, DateOnly today) =>
        row.DueDate < today && row.Status is BlankSubmissionStatus.Draft or BlankSubmissionStatus.InApproval;

    /// <summary>Toshkent kunining boshi — UTC lahza (Npgsql <c>timestamptz</c> uchun nol siljish shart).</summary>
    private static DateTimeOffset StartOfDayUtc(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset).ToUniversalTime();

    // ─────────────────────────────────────────────────────────────────
    //  Yordamchilar — javoblarni yorliqlash
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Bo'lim tartibida, faqat qiymati borlar: <c>[{section, field, label, value}]</c>.</summary>
    private static JsonArray LabelledAnswers(
        BlankDefinition definition,
        JsonObject answers,
        JsonObject computed,
        Dictionary<string, FileSummary> files)
    {
        var result = new JsonArray();

        foreach (var field in definition.Fields.Where(field => field.Type != "computed"))
        {
            var value = field.Type switch
            {
                "file" => Files(files, field.Id),
                "table" => TableRows(field, answers[field.Id], computed, files),
                _ => Display(field, answers[field.Id]),
            };

            if (!IsEmpty(value))
            {
                result.Add(Entry(field, value));
            }
        }

        return result;
    }

    /// <summary>Jadvaldan tashqaridagi hisoblangan maydonlar (jadval ichidagilari — qator ichida).</summary>
    private static JsonArray LabelledComputed(BlankDefinition definition, JsonObject computed)
    {
        var result = new JsonArray();

        foreach (var field in definition.Fields.Where(field => field.Type == "computed"))
        {
            var value = computed[field.Id]?.DeepClone();

            if (!IsEmpty(value))
            {
                result.Add(Entry(field, value));
            }
        }

        return result;
    }

    private static JsonObject Entry(BlankField field, JsonNode? value) => new()
    {
        ["section"] = field.SectionTitle,
        ["field"] = field.Id,
        ["label"] = field.Label,
        ["value"] = value,
    };

    private static JsonArray? TableRows(
        BlankField table,
        JsonNode? value,
        JsonObject computed,
        Dictionary<string, FileSummary> files)
    {
        if (value is not JsonArray rows)
        {
            return null;
        }

        var result = new JsonArray();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] as JsonObject;
            var cells = new JsonArray();

            foreach (var column in table.Columns)
            {
                var path = $"{table.Id}[{i.ToString(CultureInfo.InvariantCulture)}].{column.Id}";
                var cell = column.Type switch
                {
                    "file" => Files(files, path),
                    "computed" => computed[path]?.DeepClone(),
                    _ => Display(column, row?[column.Id]),
                };

                if (!IsEmpty(cell))
                {
                    cells.Add(new JsonObject { ["field"] = column.Id, ["label"] = column.Label, ["value"] = cell });
                }
            }

            if (cells.Count == 0)
            {
                continue;
            }

            var label = row?["_row"] is { } fixedRow && Text(fixedRow) is { } rowId ? table.FixedRows.GetValueOrDefault(rowId) : null;
            result.Add(new JsonObject { ["row"] = label ?? $"{(i + 1).ToString(CultureInfo.InvariantCulture)}-qator", ["cells"] = cells });
        }

        return result;
    }

    private static JsonObject? Files(Dictionary<string, FileSummary> files, string path) =>
        files.TryGetValue(path, out var summary)
            ? new JsonObject
            {
                ["attachments"] = summary.Count,
                ["kinds"] = new JsonArray([.. summary.Kinds.Select(kind => (JsonNode)kind.ToString())]),
            }
            : null;

    /// <summary>Saqlangan qiymat — o'qiladigan ko'rinishda (tanlov — yorliq, havola — ism).</summary>
    private static JsonNode? Display(BlankField field, JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        switch (field.Type)
        {
            case "static":
                return null;
            case "ref":
                return Text(value["label"]) is { } name ? JsonValue.Create(name) : null;
            case "choice":
                return ChoiceText(field, value) is { } choice ? JsonValue.Create(choice) : null;
            case "multi":
                return value is JsonArray selected
                    ? new JsonArray([.. selected.Select(item => ChoiceText(field, item)).OfType<string>().Select(text => (JsonNode)text)])
                    : null;
            case "docRef":
                return DocRefText(value) is { } document ? JsonValue.Create(document) : null;
            case "order":
                return value is JsonObject order
                    ? JsonValue.Create(Join($"№{Text(order["number"])}", Text(order["date"])))
                    : null;
            case "dateRange":
                return value is JsonObject range
                    ? JsonValue.Create($"{Text(range["from"])} — {Text(range["to"])}")
                    : null;
            default:
                return value.DeepClone();
        }
    }

    private static string? ChoiceText(BlankField field, JsonNode? value)
    {
        if (Text(value?["value"]) is not { } selected)
        {
            return null;
        }

        var option = field.Options.GetValueOrDefault(selected);
        var label = option?.Label ?? selected;

        var extra = value!["extra"] switch
        {
            null => null,
            JsonObject reference when reference["label"] is { } name => Text(name),
            JsonObject document => DocRefText(document),
            var plain => Text(plain),
        };

        return string.IsNullOrWhiteSpace(extra) ? label : $"{label} ({option?.ExtraLabel ?? "izoh"}: {extra})";
    }

    private static string? DocRefText(JsonNode? value) =>
        value is JsonObject document && Text(document["templateCode"]) is { } code
            ? Join(code, Text(document["number"]) is { } number ? $"№{number}" : null)
            : null;

    private static string Join(string first, string? second) =>
        string.IsNullOrWhiteSpace(second) ? first : $"{first}, {second}";

    private static string? Text(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
        _ => node.ToJsonString(),
    };

    private static bool IsEmpty(JsonNode? value) => value switch
    {
        null => true,
        JsonArray array => array.Count == 0,
        JsonValue text when text.GetValueKind() == JsonValueKind.String => string.IsNullOrWhiteSpace(text.GetValue<string>()),
        _ => false,
    };

    /// <summary>Bo'sh majburiy maydon yo'llari — nomi bilan (<c>criteria[2].score</c> → «Mezon — 3-qator: Baho»).</summary>
    private static JsonArray MissingLabels(BlankDefinition definition, string? missing)
    {
        var result = new JsonArray();

        if (ParseNode(missing) is not JsonArray paths)
        {
            return result;
        }

        foreach (var path in paths.Select(Text).OfType<string>())
        {
            result.Add(new JsonObject { ["field"] = path, ["label"] = definition.PathLabel(path) });
        }

        return result;
    }

    private static JsonObject ParseObject(string? json) => ParseNode(json) as JsonObject ?? [];

    private static JsonNode? ParseNode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Shablon ta'rifi (SPEC-blankalar §4) — faqat yorliqlar uchun kerakli qismi
    // ─────────────────────────────────────────────────────────────────

    private sealed record BlankOption(string Label, string? ExtraLabel);

    private sealed record BlankField(
        string Id,
        string Type,
        string Label,
        bool Required,
        string? SectionTitle,
        IReadOnlyDictionary<string, BlankOption> Options,
        IReadOnlyList<BlankField> Columns,
        IReadOnlyDictionary<string, string> FixedRows);

    private sealed record BlankStepDefinition(
        string Id,
        string? Title,
        string? Actor,
        int? Order,
        IReadOnlyDictionary<string, BlankOption> Decisions);

    /// <summary>
    /// Ta'rifni yorliqlar uchun o'qiydi. Ta'rif saqlashda tekshirilgan (§4.8) — bu yerda
    /// bardoshli o'qish: buzilgan yoki noma'lum qism shunchaki tushib qoladi, xato emas.
    /// </summary>
    private sealed partial class BlankDefinition
    {
        private static readonly BlankDefinition Empty = new([], []);

        private readonly Dictionary<string, BlankField> _byId;

        private BlankDefinition(IReadOnlyList<BlankField> fields, IReadOnlyList<BlankStepDefinition> steps)
        {
            Fields = fields;
            Steps = steps;
            _byId = fields.GroupBy(field => field.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }

        /// <summary>Bo'lim tartibida barcha maydonlar (<c>static</c> — yo'q).</summary>
        public IReadOnlyList<BlankField> Fields { get; }

        public IReadOnlyList<BlankStepDefinition> Steps { get; }

        public static BlankDefinition Parse(string? json)
        {
            if (ParseNode(json) is not JsonObject root)
            {
                return Empty;
            }

            var fields = new List<BlankField>();

            foreach (var section in Objects(root["sections"]))
            {
                var sectionTitle = Text(section["title"]);
                fields.AddRange(Objects(section["fields"])
                    .Select(field => Field(field, sectionTitle))
                    .OfType<BlankField>());
            }

            var steps = Objects(root["steps"])
                .Where(step => Text(step["id"]) is not null)
                .Select(step => new BlankStepDefinition(
                    Text(step["id"])!,
                    Text(step["title"]),
                    Text(step["actor"]),
                    step["order"] is JsonValue order && order.TryGetValue(out int number) ? number : null,
                    Options(step["decision"]?["options"])))
                .ToList();

            return new BlankDefinition(fields, steps);
        }

        /// <summary>Qadam qarorining yorlig'i; ta'rifda topilmasa — xom qiymat.</summary>
        public string? DecisionLabel(string stepCode, string? decision) =>
            decision is null
                ? null
                : Steps.FirstOrDefault(step => step.Id == stepCode)?.Decisions.GetValueOrDefault(decision)?.Label ?? decision;

        /// <summary>Maydon yo'li nomi: <c>id</c> yoki <c>jadval[i].ustun</c>.</summary>
        public string PathLabel(string path)
        {
            if (_byId.TryGetValue(path, out var field))
            {
                return field.Label;
            }

            var match = TablePath().Match(path);

            if (!match.Success || !_byId.TryGetValue(match.Groups["table"].Value, out var table))
            {
                return path;
            }

            var index = int.Parse(match.Groups["row"].Value, CultureInfo.InvariantCulture);
            var column = table.Columns.FirstOrDefault(item => item.Id == match.Groups["column"].Value)?.Label
                ?? match.Groups["column"].Value;

            return $"{table.Label} — {(index + 1).ToString(CultureInfo.InvariantCulture)}-qator: {column}";
        }

        private static BlankField? Field(JsonObject node, string? sectionTitle)
        {
            if (Text(node["id"]) is not { } id || Text(node["type"]) is not { } type || type == "static")
            {
                return null;
            }

            var fixedRows = Objects(node["fixedRows"])
                .Where(row => Text(row["id"]) is not null)
                .GroupBy(row => Text(row["id"])!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => Text(group.First()["label"]) ?? group.Key, StringComparer.Ordinal);

            return new BlankField(
                id,
                type,
                Text(node["label"]) ?? id,
                node["required"] is JsonValue required && required.TryGetValue(out bool flag) && flag,
                sectionTitle,
                Options(node["options"]),
                [.. Objects(node["columns"]).Select(column => Field(column, sectionTitle)).OfType<BlankField>()],
                fixedRows);
        }

        private static Dictionary<string, BlankOption> Options(JsonNode? node) =>
            Objects(node)
                .Where(option => Text(option["value"]) is not null)
                .GroupBy(option => Text(option["value"])!, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => new BlankOption(
                        Text(group.First()["label"]) ?? group.Key,
                        Text(group.First()["extra"]?["label"])),
                    StringComparer.Ordinal);

        private static IEnumerable<JsonObject> Objects(JsonNode? node) =>
            node is JsonArray array ? array.OfType<JsonObject>() : [];

        [GeneratedRegex(@"^(?<table>[A-Za-z0-9]+)\[(?<row>\d{1,3})\]\.(?<column>[A-Za-z0-9]+)$", RegexOptions.CultureInvariant)]
        private static partial Regex TablePath();
    }
}
